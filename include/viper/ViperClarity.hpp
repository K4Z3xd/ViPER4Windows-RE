#pragma once

#include "viper/BiquadFilter.hpp"
#include "viper/DspTypes.hpp"
#include <span>

namespace viper {

// Hi-Res ViPER Clarity processor:
// - Natural: Gentle anti-mud mid-bass cleanup + smooth vocal/air presence lift
// - OZone+: Anti-mud de-masking + envelope-scaled high-band harmonic exciter
// - XHiFi: Anti-mud cleanup + transient-aware micro-detail enhancer + extended Hi-Res air
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

    // Fast & slow envelopes for transient micro-detail extraction (XHiFi / OZone+)
    double fastCoeff_ = 0.0;
    double slowCoeff_ = 0.0;
    double fastEnvL_ = 0.0;
    double slowEnvL_ = 0.0;
    double fastEnvR_ = 0.0;
    double slowEnvR_ = 0.0;

    BiquadFilter antiMudL_{};
    BiquadFilter antiMudR_{};
    BiquadFilter highPassL_{};
    BiquadFilter highPassR_{};
    BiquadFilter highShelfL_{};
    BiquadFilter highShelfR_{};
    BiquadFilter presencePeakL_{};
    BiquadFilter presencePeakR_{};
};

} // namespace viper
