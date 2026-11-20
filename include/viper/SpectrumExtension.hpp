#pragma once

#include "viper/BiquadFilter.hpp"
#include <span>

namespace viper {

// Hi-Res ViPER Spectrum Extension (VSE)
// Reconstructs lost upper harmonics for compressed audio (MP3, YouTube, Spotify)
// scaled accurately across 44.1kHz - 384kHz.
class SpectrumExtension {
public:
    void prepare(double sampleRate) noexcept;
    void reset() noexcept;
    void configure(float strength) noexcept;

    void processInterleavedStereo(std::span<float> interleavedSamples) noexcept;

private:
    double sampleRate_ = 48000.0;
    double strength_ = 0.5;

    BiquadFilter bandPassL_{};
    BiquadFilter bandPassR_{};
    BiquadFilter highPassHarmL_{};
    BiquadFilter highPassHarmR_{};
};

} // namespace viper
