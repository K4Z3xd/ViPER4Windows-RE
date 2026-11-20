#pragma once

#include "viper/BiquadFilter.hpp"
#include "viper/DspTypes.hpp"
#include <span>

namespace viper {

// Hi-Res ViPER Clarity processor:
// - Natural: Smooth high-frequency presence lift without harsh sibilance
// - OZone+: Parallel high-band harmonic exciter (adds air & detail)
// - XHiFi: Crisp transient/treble reconstruction scaled accurately up to 384kHz
class ViperClarity {
public:
    void prepare(double sampleRate) noexcept;
    void reset() noexcept;
    void configure(ClarityMode mode, float gainDb) noexcept;

    void processInterleavedStereo(std::span<float> interleavedSamples) noexcept;

private:
    double sampleRate_ = 48000.0;
    ClarityMode mode_ = ClarityMode::Natural;
    float gainDb_ = 0.0f;
    double exciterMix_ = 0.0;

    BiquadFilter highPassL_{};
    BiquadFilter highPassR_{};
    BiquadFilter highShelfL_{};
    BiquadFilter highShelfR_{};
    BiquadFilter presencePeakL_{};
    BiquadFilter presencePeakR_{};
};

} // namespace viper
