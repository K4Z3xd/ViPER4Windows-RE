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
    void updateGains(uint32_t eqMode,
                     const std::array<float, kEqBandCount>& bands10Db,
                     const std::array<float, kEqBand18Count>& bands18Db) noexcept;

    // Interleaved stereo processing (L, R, L, R, ...)
    void processInterleavedStereo(std::span<float> interleavedSamples) noexcept;

private:
    double sampleRate_ = 48000.0;
    uint32_t eqMode_ = 0; // 0 = 10-band, 1 = 18-band

    std::array<float, kEqBandCount> lastGains10Db_{};
    std::array<BiquadFilter, kEqBandCount> filters10L_{};
    std::array<BiquadFilter, kEqBandCount> filters10R_{};

    std::array<float, kEqBand18Count> lastGains18Db_{};
    std::array<BiquadFilter, kEqBand18Count> filters18L_{};
    std::array<BiquadFilter, kEqBand18Count> filters18R_{};
};

} // namespace viper
