#include "viper/ViperBass.hpp"
#include <algorithm>
#include <cmath>

namespace viper {

void ViperBass::prepare(double sampleRate) noexcept {
    sampleRate_ = sampleRate;
    reset();
    configure(mode_, frequencyHz_, gainDb_);
}

void ViperBass::reset() noexcept {
    lowPassL_.reset();
    lowPassR_.reset();
    highPassHarmonicL_.reset();
    highPassHarmonicR_.reset();
    shelfL_.reset();
    shelfR_.reset();
}

void ViperBass::configure(BassMode mode, float frequencyHz, float gainDb) noexcept {
    mode_ = mode;
    frequencyHz_ = std::clamp(frequencyHz, 30.0f, 150.0f);
    gainDb_ = std::clamp(gainDb, 0.0f, 15.0f);

    const double linearGain = std::pow(10.0, static_cast<double>(gainDb_) / 20.0) - 1.0;
    wetGain_ = std::max(0.0, linearGain);

    // Isolate sub-bass band dynamically at current sampleRate
    lowPassL_.configure(BiquadType::LowPass, sampleRate_, frequencyHz_, 0.7071);
    lowPassR_.configure(BiquadType::LowPass, sampleRate_, frequencyHz_, 0.7071);

    // Remove DC / rumble below 20Hz from synthesized harmonics
    highPassHarmonicL_.configure(BiquadType::HighPass, sampleRate_, 22.0, 0.7071);
    highPassHarmonicR_.configure(BiquadType::HighPass, sampleRate_, 22.0, 0.7071);

    // Clean low-shelf for Natural mode
    shelfL_.configure(BiquadType::LowShelf, sampleRate_, frequencyHz_, 0.75, gainDb_);
    shelfR_.configure(BiquadType::LowShelf, sampleRate_, frequencyHz_, 0.75, gainDb_);
}

void ViperBass::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    if (gainDb_ <= 0.01f) {
        return;
    }

    const size_t total = interleavedSamples.size() & ~size_t{1};

    if (mode_ == BassMode::Natural) {
        for (size_t i = 0; i < total; i += 2) {
            interleavedSamples[i] = static_cast<float>(shelfL_.processSample(interleavedSamples[i]));
            interleavedSamples[i + 1] = static_cast<float>(shelfR_.processSample(interleavedSamples[i + 1]));
        }
        return;
    }

    // PureBass+ & Subwoofer: parallel psychoacoustic harmonic bass synthesis
    const double drive = (mode_ == BassMode::Subwoofer) ? 2.2 : 1.4;
    const double mixScale = (mode_ == BassMode::Subwoofer) ? 0.55 : 0.42;

    for (size_t i = 0; i < total; i += 2) {
        const double inL = static_cast<double>(interleavedSamples[i]);
        const double inR = static_cast<double>(interleavedSamples[i + 1]);

        const double subL = lowPassL_.processSample(inL);
        const double subR = lowPassR_.processSample(inR);

        // Soft nonlinear saturation creates audible upper bass harmonics + punch
        const double satL = std::tanh(subL * drive) / drive;
        const double satR = std::tanh(subR * drive) / drive;

        const double cleanSubL = highPassHarmonicL_.processSample(satL);
        const double cleanSubR = highPassHarmonicR_.processSample(satR);

        interleavedSamples[i] = static_cast<float>(inL + cleanSubL * wetGain_ * mixScale);
        interleavedSamples[i + 1] = static_cast<float>(inR + cleanSubR * wetGain_ * mixScale);
    }
}

} // namespace viper
