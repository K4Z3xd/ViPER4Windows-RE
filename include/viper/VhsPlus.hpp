#pragma once

#include "viper/BiquadFilter.hpp"
#include <cstdint>
#include <span>
#include <vector>

namespace viper {

// ViPER Headphone Surround+ (VHS+)
// 5-level 3D headphone early-reflection acoustic spatializer,
// dynamically scaled across 44.1kHz - 384kHz.
class VhsPlus {
public:
    void prepare(double sampleRate);
    void reset() noexcept;
    void configure(uint32_t level) noexcept; // 1..5

    void processInterleavedStereo(std::span<float> interleavedSamples) noexcept;

private:
    double sampleRate_ = 48000.0;
    uint32_t level_ = 1;
    float wetMix_ = 0.16f;

    size_t tap1_ = 0, tap2_ = 0, tap3_ = 0;
    std::vector<float> bufL_{};
    std::vector<float> bufR_{};
    size_t writeIdx_ = 0;

    BiquadFilter dampL_{};
    BiquadFilter dampR_{};
};

} // namespace viper
