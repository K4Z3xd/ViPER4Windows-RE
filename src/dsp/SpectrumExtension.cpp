#include "viper/SpectrumExtension.hpp"
#include <algorithm>
#include <cmath>

namespace viper {

void SpectrumExtension::prepare(double sampleRate) noexcept {
    sampleRate_ = std::max(sampleRate, 8000.0);
    // Isolate upper-mid source harmonics (5.5 kHz center) & extract synthesized air (> 8.5 kHz)
    bandPassL_.configure(BiquadType::BandPass, sampleRate_, 5500.0, 0.9);
    bandPassR_.configure(BiquadType::BandPass, sampleRate_, 5500.0, 0.9);
    highPassHarmL_.configure(BiquadType::HighPass, sampleRate_, 8500.0, 0.7071);
    highPassHarmR_.configure(BiquadType::HighPass, sampleRate_, 8500.0, 0.7071);
    reset();
}

void SpectrumExtension::reset() noexcept {
    bandPassL_.reset();
    bandPassR_.reset();
    highPassHarmL_.reset();
    highPassHarmR_.reset();
}

void SpectrumExtension::configure(float strength) noexcept {
    strength_ = std::clamp(static_cast<double>(strength), 0.0, 1.0);
}

void SpectrumExtension::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    if (strength_ <= 0.005) {
        return;
    }

    const double mix = strength_ * 0.45;
    const size_t total = interleavedSamples.size() & ~size_t{1};

    for (size_t i = 0; i < total; i += 2) {
        const double l = static_cast<double>(interleavedSamples[i]);
        const double r = static_cast<double>(interleavedSamples[i + 1]);

        const double bpL = bandPassL_.processSample(l);
        const double bpR = bandPassR_.processSample(r);

        // Full-wave soft nonlinear harmonic doubler reconstructs 11kHz-20kHz air
        const double harmL = highPassHarmL_.processSample(std::abs(bpL) * 0.6 + std::tanh(bpL * 2.2) * 0.4);
        const double harmR = highPassHarmR_.processSample(std::abs(bpR) * 0.6 + std::tanh(bpR * 2.2) * 0.4);

        interleavedSamples[i] = static_cast<float>(l + harmL * mix);
        interleavedSamples[i + 1] = static_cast<float>(r + harmR * mix);
    }
}

} // namespace viper
