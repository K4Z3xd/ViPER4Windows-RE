#pragma once

#include "viper/BiquadFilter.hpp"
#include "viper/DspTypes.hpp"
#include <array>
#include <span>

namespace viper {

class ParametricEq {
public:
    void prepare(double sampleRate) noexcept;
    void reset() noexcept;
    void updateGains(const std::array<float, kEqBandCount>& bandsDb) noexcept;

    // Interleaved stereo processing (L, R, L, R, ...)
    void processInterleavedStereo(std::span<float> interleavedSamples) noexcept;

private:
    double sampleRate_ = 48000.0;
    std::array<float, kEqBandCount> lastGainsDb_{};
    std::array<BiquadFilter, kEqBandCount> filtersL_{};
    std::array<BiquadFilter, kEqBandCount> filtersR_{};
};

} // namespace viper
