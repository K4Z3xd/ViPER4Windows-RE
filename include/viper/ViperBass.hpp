#pragma once

#include "viper/BiquadFilter.hpp"
#include "viper/DspTypes.hpp"
#include <span>

namespace viper {

// Hi-Res ViPER Bass processor:
// - Natural: Clean 64-bit low-shelf boost scaled cleanly across 44.1-384 kHz
// - PureBass+: Tight sub-bass punch with asymmetric 2nd-harmonic warmth & dynamic control
// - Subwoofer: True Sub-Octave harmonic synthesizer (zero-crossing octave divider + envelope
//              reconstruction + 4th-order low-pass) blended with tight bass harmonics
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

    // Envelope coefficients for Sub-Octave tracker & anti-clipping
    double envAttackCoeff_ = 0.0;
    double envReleaseCoeff_ = 0.0;
    double subEnvL_ = 0.0;
    double subEnvR_ = 0.0;
    double prevBandL_ = 0.0;
    double prevBandR_ = 0.0;
    double subSignL_ = 1.0;
    double subSignR_ = 1.0;

    // Natural & PureBass+ filters
    BiquadFilter lowPassL_{};
    BiquadFilter lowPassR_{};
    BiquadFilter highPassHarmonicL_{};
    BiquadFilter highPassHarmonicR_{};
    BiquadFilter shelfL_{};
    BiquadFilter shelfR_{};

    // Subwoofer true sub-octave synthesis filters
    BiquadFilter octaveSrcBpL_{};
    BiquadFilter octaveSrcBpR_{};
    BiquadFilter subSmooth1L_{};
    BiquadFilter subSmooth1R_{};
    BiquadFilter subSmooth2L_{};
    BiquadFilter subSmooth2R_{};
    BiquadFilter subDcHpL_{};
    BiquadFilter subDcHpR_{};
};

} // namespace viper
