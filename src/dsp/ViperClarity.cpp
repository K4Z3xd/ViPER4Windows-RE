#include "viper/ViperClarity.hpp"
#include <algorithm>
#include <cmath>

namespace viper {

void ViperClarity::prepare(double sampleRate) noexcept {
    sampleRate_ = sampleRate;
    reset();
    configure(mode_, gainDb_);
}

void ViperClarity::reset() noexcept {
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
    exciterMix_ = std::clamp(linearBoost * 0.18, 0.0, 0.85);

    switch (mode_) {
        case ClarityMode::Natural:
            // Smooth vocal/instrument clarity without harsh sibilance
            highShelfL_.configure(BiquadType::HighShelf, sampleRate_, 5500.0, 0.7071, gainDb_ * 0.65);
            highShelfR_.configure(BiquadType::HighShelf, sampleRate_, 5500.0, 0.7071, gainDb_ * 0.65);
            presencePeakL_.configure(BiquadType::PeakingEQ, sampleRate_, 3200.0, 1.1, gainDb_ * 0.25);
            presencePeakR_.configure(BiquadType::PeakingEQ, sampleRate_, 3200.0, 1.1, gainDb_ * 0.25);
            break;

        case ClarityMode::OZonePlus:
            // Airy harmonic exciter above 4.5kHz
            highPassL_.configure(BiquadType::HighPass, sampleRate_, 4500.0, 0.7071);
            highPassR_.configure(BiquadType::HighPass, sampleRate_, 4500.0, 0.7071);
            highShelfL_.configure(BiquadType::HighShelf, sampleRate_, 8000.0, 0.7071, gainDb_ * 0.45);
            highShelfR_.configure(BiquadType::HighShelf, sampleRate_, 8000.0, 0.7071, gainDb_ * 0.45);
            break;

        case ClarityMode::XHiFi:
            // Crisp detail reconstruction (leverages extended Hi-Res bandwidth above 10kHz)
            highPassL_.configure(BiquadType::HighPass, sampleRate_, 6500.0, 0.7071);
            highPassR_.configure(BiquadType::HighPass, sampleRate_, 6500.0, 0.7071);
            highShelfL_.configure(BiquadType::HighShelf, sampleRate_, 10000.0, 0.7071, gainDb_ * 0.55);
            highShelfR_.configure(BiquadType::HighShelf, sampleRate_, 10000.0, 0.7071, gainDb_ * 0.55);
            break;
    }
}

void ViperClarity::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    if (gainDb_ <= 0.01f) {
        return;
    }

    const size_t total = interleavedSamples.size() & ~size_t{1};

    if (mode_ == ClarityMode::Natural) {
        for (size_t i = 0; i < total; i += 2) {
            double l = presencePeakL_.processSample(interleavedSamples[i]);
            double r = presencePeakR_.processSample(interleavedSamples[i + 1]);
            l = highShelfL_.processSample(l);
            r = highShelfR_.processSample(r);
            interleavedSamples[i] = static_cast<float>(l);
            interleavedSamples[i + 1] = static_cast<float>(r);
        }
        return;
    }

    // OZone+ / XHiFi: High-shelf + parallel soft-harmonic treble exciter
    for (size_t i = 0; i < total; i += 2) {
        const double inL = static_cast<double>(interleavedSamples[i]);
        const double inR = static_cast<double>(interleavedSamples[i + 1]);

        const double hiL = highPassL_.processSample(inL);
        const double hiR = highPassR_.processSample(inR);

        // Gentle odd+even harmonic shimmer
        const double excL = std::tanh(hiL * 2.5) - hiL * 0.5;
        const double excR = std::tanh(hiR * 2.5) - hiR * 0.5;

        const double outL = highShelfL_.processSample(inL) + excL * exciterMix_;
        const double outR = highShelfR_.processSample(inR) + excR * exciterMix_;

        interleavedSamples[i] = static_cast<float>(outL);
        interleavedSamples[i + 1] = static_cast<float>(outR);
    }
}

} // namespace viper
