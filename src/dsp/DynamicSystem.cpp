#include "viper/DynamicSystem.hpp"
#include <algorithm>
#include <cmath>

namespace viper {

void DynamicSystem::prepare(double sampleRate) noexcept {
    sampleRate_ = std::max(sampleRate, 8000.0);
    // Low-band transient tracker (6 ms attack, 95 ms release)
    lowAttackCoeff_ = std::exp(-1.0 / (sampleRate_ * 0.006));
    lowReleaseCoeff_ = std::exp(-1.0 / (sampleRate_ * 0.095));
    // High-band presence transient tracker (4 ms attack, 70 ms release)
    highAttackCoeff_ = std::exp(-1.0 / (sampleRate_ * 0.004));
    highReleaseCoeff_ = std::exp(-1.0 / (sampleRate_ * 0.070));
    reset();
    configure(device_, static_cast<float>(strength_ * 100.0));
}

void DynamicSystem::reset() noexcept {
    envLow_ = 0.0;
    envHigh_ = 0.0;
    lowBandL_.reset();
    lowBandR_.reset();
    lowResPeakL_.reset();
    lowResPeakR_.reset();
    highBandL_.reset();
    highBandR_.reset();
    highShelfL_.reset();
    highShelfR_.reset();
}

void DynamicSystem::configure(DynamicDeviceType device, float dynamicBassPercent) noexcept {
    device_ = device;
    strength_ = std::clamp(static_cast<double>(dynamicBassPercent) / 100.0, 0.0, 1.0);

    double lowCutoffHz = 95.0;
    double resFreqHz = 65.0;
    double resGainDb = 1.6 * strength_;
    double airGainDb = 1.2 * strength_;
    lowPunchScale_ = 1.25;
    highPunchScale_ = 0.28;

    switch (device_) {
        case DynamicDeviceType::CommonEarphone:
            lowCutoffHz = 115.0;
            resFreqHz = 78.0;
            resGainDb = 2.4 * strength_;
            airGainDb = 1.6 * strength_;
            lowPunchScale_ = 1.45;
            highPunchScale_ = 0.34;
            break;
        case DynamicDeviceType::ExtremeHeadphone:
            lowCutoffHz = 90.0;
            resFreqHz = 60.0;
            resGainDb = 3.0 * strength_;
            airGainDb = 2.1 * strength_;
            lowPunchScale_ = 1.65;
            highPunchScale_ = 0.40;
            break;
        case DynamicDeviceType::HighEndHeadphone:
            lowCutoffHz = 72.0;
            resFreqHz = 50.0;
            resGainDb = 1.5 * strength_;
            airGainDb = 1.1 * strength_;
            lowPunchScale_ = 1.15;
            highPunchScale_ = 0.24;
            break;
        case DynamicDeviceType::StudioMonitor:
            lowCutoffHz = 58.0;
            resFreqHz = 42.0;
            resGainDb = 0.9 * strength_;
            airGainDb = 0.7 * strength_;
            lowPunchScale_ = 0.90;
            highPunchScale_ = 0.16;
            break;
    }

    lowBandL_.configure(BiquadType::LowPass, sampleRate_, lowCutoffHz, 0.7071);
    lowBandR_.configure(BiquadType::LowPass, sampleRate_, lowCutoffHz, 0.7071);
    lowResPeakL_.configure(BiquadType::PeakingEQ, sampleRate_, resFreqHz, 1.25, resGainDb);
    lowResPeakR_.configure(BiquadType::PeakingEQ, sampleRate_, resFreqHz, 1.25, resGainDb);
    highBandL_.configure(BiquadType::HighPass, sampleRate_, 3800.0, 0.7071);
    highBandR_.configure(BiquadType::HighPass, sampleRate_, 3800.0, 0.7071);
    highShelfL_.configure(BiquadType::HighShelf, sampleRate_, 8000.0, 0.7071, airGainDb);
    highShelfR_.configure(BiquadType::HighShelf, sampleRate_, 8000.0, 0.7071, airGainDb);
}

void DynamicSystem::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    if (strength_ <= 0.005) {
        return;
    }

    const size_t total = interleavedSamples.size() & ~size_t{1};
    for (size_t i = 0; i < total; i += 2) {
        const double l = static_cast<double>(interleavedSamples[i]);
        const double r = static_cast<double>(interleavedSamples[i + 1]);

        // 1. Low-band transient envelope follower
        const double lowL = lowBandL_.processSample(l);
        const double lowR = lowBandR_.processSample(r);
        const double lowPeak = std::max(std::abs(lowL), std::abs(lowR));
        envLow_ = (lowPeak > envLow_)
            ? (lowAttackCoeff_ * envLow_ + (1.0 - lowAttackCoeff_) * lowPeak)
            : (lowReleaseCoeff_ * envLow_ + (1.0 - lowReleaseCoeff_) * lowPeak);
        if (envLow_ < 1e-18) envLow_ = 0.0;

        // 2. High-band transient envelope follower
        const double hiL = highBandL_.processSample(l);
        const double hiR = highBandR_.processSample(r);
        const double hiPeak = std::max(std::abs(hiL), std::abs(hiR));
        envHigh_ = (hiPeak > envHigh_)
            ? (highAttackCoeff_ * envHigh_ + (1.0 - highAttackCoeff_) * hiPeak)
            : (highReleaseCoeff_ * envHigh_ + (1.0 - highReleaseCoeff_) * hiPeak);
        if (envHigh_ < 1e-18) envHigh_ = 0.0;

        // 3. Dynamic 2-band punch + transducer enclosure voicing
        const double dynLow = std::min(envLow_ * 2.6, 1.0) * strength_ * lowPunchScale_;
        const double dynHi = std::min(envHigh_ * 2.2, 1.0) * strength_ * highPunchScale_;

        double outL = l + std::tanh(lowL * dynLow * 1.1) / 1.1 + hiL * dynHi;
        double outR = r + std::tanh(lowR * dynLow * 1.1) / 1.1 + hiR * dynHi;

        outL = highShelfL_.processSample(lowResPeakL_.processSample(outL));
        outR = highShelfR_.processSample(lowResPeakR_.processSample(outR));

        interleavedSamples[i] = static_cast<float>(outL);
        interleavedSamples[i + 1] = static_cast<float>(outR);
    }
}

} // namespace viper
