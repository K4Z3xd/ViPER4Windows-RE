#include "viper/ViperClarity.hpp"
#include <algorithm>
#include <cmath>

namespace viper {

void ViperClarity::prepare(double sampleRate) noexcept {
    sampleRate_ = std::max(sampleRate, 8000.0);
    // Fast (1.5 ms) vs Slow (35 ms) envelope trackers for micro-transient clarity
    fastCoeff_ = std::exp(-1.0 / (sampleRate_ * 0.0015));
    slowCoeff_ = std::exp(-1.0 / (sampleRate_ * 0.035));
    reset();
    configure(mode_, gainDb_);
}

void ViperClarity::reset() noexcept {
    fastEnvL_ = slowEnvL_ = 0.0;
    fastEnvR_ = slowEnvR_ = 0.0;
    antiMudL_.reset();
    antiMudR_.reset();
    highPassL_.reset();
    highPassR_.reset();
    highShelfL_.reset();
    highShelfR_.reset();
    presencePeakL_.reset();
    presencePeakR_.reset();
}

void ViperClarity::configure(ClarityMode mode, float gainDb) noexcept {
    mode_ = mode;
    gainDb_ = std::clamp(gainDb, 0.0f, 15.0f);

    const double linearBoost = std::pow(10.0, static_cast<double>(gainDb_) / 20.0) - 1.0;
    exciterMix_ = std::clamp(linearBoost * 0.20, 0.0, 0.90);

    switch (mode_) {
        case ClarityMode::Natural: {
            // Subtle anti-mud dip + smooth vocal/air presence lift
            const double mudDip = -std::min(2.2, static_cast<double>(gainDb_) * 0.14);
            antiMudL_.configure(BiquadType::PeakingEQ, sampleRate_, 250.0, 0.85, mudDip);
            antiMudR_.configure(BiquadType::PeakingEQ, sampleRate_, 250.0, 0.85, mudDip);
            presencePeakL_.configure(BiquadType::PeakingEQ, sampleRate_, 3200.0, 1.05, gainDb_ * 0.30);
            presencePeakR_.configure(BiquadType::PeakingEQ, sampleRate_, 3200.0, 1.05, gainDb_ * 0.30);
            highShelfL_.configure(BiquadType::HighShelf, sampleRate_, 5800.0, 0.7071, gainDb_ * 0.55);
            highShelfR_.configure(BiquadType::HighShelf, sampleRate_, 5800.0, 0.7071, gainDb_ * 0.55);
            break;
        }

        case ClarityMode::OZonePlus: {
            // Anti-mud de-masking + airy harmonic exciter above 4.2 kHz
            const double mudDip = -std::min(2.8, static_cast<double>(gainDb_) * 0.18);
            antiMudL_.configure(BiquadType::PeakingEQ, sampleRate_, 260.0, 0.85, mudDip);
            antiMudR_.configure(BiquadType::PeakingEQ, sampleRate_, 260.0, 0.85, mudDip);
            highPassL_.configure(BiquadType::HighPass, sampleRate_, 4200.0, 0.7071);
            highPassR_.configure(BiquadType::HighPass, sampleRate_, 4200.0, 0.7071);
            highShelfL_.configure(BiquadType::HighShelf, sampleRate_, 8000.0, 0.7071, gainDb_ * 0.48);
            highShelfR_.configure(BiquadType::HighShelf, sampleRate_, 8000.0, 0.7071, gainDb_ * 0.48);
            break;
        }

        case ClarityMode::XHiFi: {
            // Anti-mud cleanup + transient-aware micro-detail above 5.8 kHz + Hi-Res air
            const double mudDip = -std::min(3.2, static_cast<double>(gainDb_) * 0.21);
            antiMudL_.configure(BiquadType::PeakingEQ, sampleRate_, 280.0, 0.90, mudDip);
            antiMudR_.configure(BiquadType::PeakingEQ, sampleRate_, 280.0, 0.90, mudDip);
            highPassL_.configure(BiquadType::HighPass, sampleRate_, 5800.0, 0.7071);
            highPassR_.configure(BiquadType::HighPass, sampleRate_, 5800.0, 0.7071);
            highShelfL_.configure(BiquadType::HighShelf, sampleRate_, 10200.0, 0.7071, gainDb_ * 0.58);
            highShelfR_.configure(BiquadType::HighShelf, sampleRate_, 10200.0, 0.7071, gainDb_ * 0.58);
            break;
        }
    }
}

void ViperClarity::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    if (gainDb_ <= 0.01f) {
        return;
    }

    const size_t total = interleavedSamples.size() & ~size_t{1};

    if (mode_ == ClarityMode::Natural) {
        for (size_t i = 0; i < total; i += 2) {
            double l = antiMudL_.processSample(interleavedSamples[i]);
            double r = antiMudR_.processSample(interleavedSamples[i + 1]);
            l = highShelfL_.processSample(presencePeakL_.processSample(l));
            r = highShelfR_.processSample(presencePeakR_.processSample(r));
            interleavedSamples[i] = static_cast<float>(l);
            interleavedSamples[i + 1] = static_cast<float>(r);
        }
        return;
    }

    const bool isXHiFi = (mode_ == ClarityMode::XHiFi);

    for (size_t i = 0; i < total; i += 2) {
        // 1. Anti-mud lower-mid cleanup unmasks vocals & acoustic detail
        const double cleanL = antiMudL_.processSample(interleavedSamples[i]);
        const double cleanR = antiMudR_.processSample(interleavedSamples[i + 1]);

        const double hiL = highPassL_.processSample(cleanL);
        const double hiR = highPassR_.processSample(cleanR);

        // 2. Soft harmonic shimmer exciter
        const double excL = std::tanh(hiL * 2.4) - hiL * 0.45;
        const double excR = std::tanh(hiR * 2.4) - hiR * 0.45;

        double detailGainL = exciterMix_;
        double detailGainR = exciterMix_;

        // 3. XHiFi: Transient-aware micro-detail boosts crisp attacks without steady sibilance
        if (isXHiFi) {
            const double aL = std::abs(hiL);
            const double aR = std::abs(hiR);
            fastEnvL_ = fastCoeff_ * fastEnvL_ + (1.0 - fastCoeff_) * aL;
            slowEnvL_ = slowCoeff_ * slowEnvL_ + (1.0 - slowCoeff_) * aL;
            fastEnvR_ = fastCoeff_ * fastEnvR_ + (1.0 - fastCoeff_) * aR;
            slowEnvR_ = slowCoeff_ * slowEnvR_ + (1.0 - slowCoeff_) * aR;
            if (fastEnvL_ < 1e-18) fastEnvL_ = 0.0;
            if (slowEnvL_ < 1e-18) slowEnvL_ = 0.0;
            if (fastEnvR_ < 1e-18) fastEnvR_ = 0.0;
            if (slowEnvR_ < 1e-18) slowEnvR_ = 0.0;

            const double transL = std::clamp((fastEnvL_ - slowEnvL_) * 5.0, 0.0, 1.0);
            const double transR = std::clamp((fastEnvR_ - slowEnvR_) * 5.0, 0.0, 1.0);
            detailGainL *= (0.65 + 0.85 * transL);
            detailGainR *= (0.65 + 0.85 * transR);
        }

        const double outL = highShelfL_.processSample(cleanL) + excL * detailGainL;
        const double outR = highShelfR_.processSample(cleanR) + excR * detailGainR;

        interleavedSamples[i] = static_cast<float>(outL);
        interleavedSamples[i + 1] = static_cast<float>(outR);
    }
}

} // namespace viper
