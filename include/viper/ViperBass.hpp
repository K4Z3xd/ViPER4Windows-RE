#pragma once

#include "viper/BiquadFilter.hpp"
#include "viper/DspTypes.hpp"
#include <span>

namespace viper {

// Hi-Res ViPER Bass processor:
// - Natural: Clean low-shelf / resonant sub-bass boost scaled to any sample rate
// - PureBass+: Tight sub-bass punch with subtle 2nd harmonic warmth
// - Subwoofer: Deep sub-harmonic synthesis + low-pass saturation for headphones/speakers
class ViperBass {
public:
    void prepare(double sampleRate) noexcept;
    void reset() noexcept;
    void configure(BassMode mode, float frequencyHz, float gainDb) noexcept;

    void processInterleavedStereo(std::span<float> interleavedSamples) noexcept;

private:
    double sampleRate_ = 48000.0;
    BassMode mode_ = BassMode::Natural;
    float frequencyHz_ = 60.0f;
    float gainDb_ = 0.0f;
    double wetGain_ = 0.0;

    BiquadFilter lowPassL_{};
    BiquadFilter lowPassR_{};
    BiquadFilter highPassHarmonicL_{};
    BiquadFilter highPassHarmonicR_{};
    BiquadFilter shelfL_{};
    BiquadFilter shelfR_{};
};

} // namespace viper
