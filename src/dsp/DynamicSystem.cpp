#include "viper/DynamicSystem.hpp"
#include <algorithm>
#include <cmath>

namespace viper {

void DynamicSystem::prepare(double sampleRate) noexcept {
    sampleRate_ = std::max(sampleRate, 8000.0);
    // 8ms attack, 120ms release envelope follower (sample-rate invariant)
    attackCoeff_ = std::exp(-1.0 / (sampleRate_ * 0.008));
    releaseCoeff_ = std::exp(-1.0 / (sampleRate_ * 0.120));
    reset();
    configure(device_, static_cast<float>(strength_ * 100.0));
}

void DynamicSystem::reset() noexcept {
    env_ = 0.0;
    lowBandL_.reset();
    lowBandR_.reset();
    highShelfL_.reset();
    highShelfR_.reset();
}

void DynamicSystem::configure(DynamicDeviceType device, float dynamicBassPercent) noexcept {
    device_ = device;
    strength_ = std::clamp(static_cast<double>(dynamicBassPercent) / 100.0, 0.0, 1.0);

    double cutoffHz = 95.0;
    double airGainDb = 1.2 * strength_;

    switch (device_) {
        case DynamicDeviceType::CommonEarphone:
            cutoffHz = 110.0;
            airGainDb = 1.5 * strength_;
            break;
        case DynamicDeviceType::ExtremeHeadphone:
            cutoffHz = 85.0;
            airGainDb = 2.0 * strength_;
            break;
        case DynamicDeviceType::HighEndHeadphone:
            cutoffHz = 70.0;
            airGainDb = 1.0 * strength_;
            break;
        case DynamicDeviceType::StudioMonitor:
            cutoffHz = 60.0;
            airGainDb = 0.6 * strength_;
            break;
    }

    lowBandL_.configure(BiquadType::LowPass, sampleRate_, cutoffHz, 0.7071);
    lowBandR_.configure(BiquadType::LowPass, sampleRate_, cutoffHz, 0.7071);
    highShelfL_.configure(BiquadType::HighShelf, sampleRate_, 7500.0, 0.7071, airGainDb);
    highShelfR_.configure(BiquadType::HighShelf, sampleRate_, 7500.0, 0.7071, airGainDb);
}

void DynamicSystem::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    if (strength_ <= 0.005) {
        return;
    }

    const size_t total = interleavedSamples.size() & ~size_t{1};
    for (size_t i = 0; i < total; i += 2) {
        const double l = static_cast<double>(interleavedSamples[i]);
        const double r = static_cast<double>(interleavedSamples[i + 1]);

        const double lowL = lowBandL_.processSample(l);
        const double lowR = lowBandR_.processSample(r);
        const double lowPeak = std::max(std::abs(lowL), std::abs(lowR));

        if (lowPeak > env_) {
            env_ = attackCoeff_ * env_ + (1.0 - attackCoeff_) * lowPeak;
        } else {
            env_ = releaseCoeff_ * env_ + (1.0 - releaseCoeff_) * lowPeak;
        }
        if (env_ < 1e-18) env_ = 0.0;

        // Dynamic punch scales smoothly with transient energy (capped to stay clean)
        const double dynFactor = std::min(env_ * 2.5, 1.0) * strength_ * 1.15;

        const double outL = highShelfL_.processSample(l + lowL * dynFactor);
        const double outR = highShelfR_.processSample(r + lowR * dynFactor);

        interleavedSamples[i] = static_cast<float>(outL);
        interleavedSamples[i + 1] = static_cast<float>(outR);
    }
}

} // namespace viper
