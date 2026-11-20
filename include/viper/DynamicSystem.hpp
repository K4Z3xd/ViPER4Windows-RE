#pragma once

#include "viper/BiquadFilter.hpp"
#include <cstdint>
#include <span>

namespace viper {

enum class DynamicDeviceType : uint32_t {
    CommonEarphone = 0,
    ExtremeHeadphone = 1,
    HighEndHeadphone = 2,
    StudioMonitor = 3
};

// Hi-Res ViPER Dynamic System:
// Transient-aware envelope follower that dynamically boosts sub-bass & impact
// scaled cleanly across 44.1kHz - 384kHz.
class DynamicSystem {
public:
    void prepare(double sampleRate) noexcept;
    void reset() noexcept;
    void configure(DynamicDeviceType device, float dynamicBassPercent) noexcept;

    void processInterleavedStereo(std::span<float> interleavedSamples) noexcept;

private:
    double sampleRate_ = 48000.0;
    DynamicDeviceType device_ = DynamicDeviceType::CommonEarphone;
    double strength_ = 0.25;
    double attackCoeff_ = 0.0;
    double releaseCoeff_ = 0.0;
    double env_ = 0.0;

    BiquadFilter lowBandL_{};
    BiquadFilter lowBandR_{};
    BiquadFilter highShelfL_{};
    BiquadFilter highShelfR_{};
};

} // namespace viper
