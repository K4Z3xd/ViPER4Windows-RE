#pragma once

#include "viper/BiquadFilter.hpp"
#include <span>

namespace viper {

// ViPER Field Surround (Colorful Music)
// Expands stereo side field while independently anchoring center vocal image (Mid Image).
class FieldSurround {
public:
    void prepare(double sampleRate) noexcept;
    void reset() noexcept;
    void configure(float strength, float midImage) noexcept;

    void processInterleavedStereo(std::span<float> interleavedSamples) noexcept;

private:
    double sampleRate_ = 48000.0;
    double sideGain_ = 1.0;
    double midGain_ = 1.0;

    BiquadFilter sideHpL_{};
    BiquadFilter midPresence_{};
};

} // namespace viper
