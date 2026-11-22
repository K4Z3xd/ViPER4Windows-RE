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

// Hi-Res ViPER Dynamic System (2-Band Dynamic Processor):
// - Low-band transient envelope follower + enclosure resonance voicing
// - High-band transient presence lifter for crisp, lively dynamics
// Scaled accurately across 44.1kHz - 384kHz.
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

    // Low-band & high-band transient envelopes
    double lowAttackCoeff_ = 0.0;
    double lowReleaseCoeff_ = 0.0;
    double highAttackCoeff_ = 0.0;
    double highReleaseCoeff_ = 0.0;
    double envLow_ = 0.0;
    double envHigh_ = 0.0;

    // Device-tuned scaling factors
    double lowPunchScale_ = 1.25;
    double highPunchScale_ = 0.30;

    BiquadFilter lowBandL_{};
    BiquadFilter lowBandR_{};
    BiquadFilter lowResPeakL_{};
    BiquadFilter lowResPeakR_{};
    BiquadFilter highBandL_{};
    BiquadFilter highBandR_{};
    BiquadFilter highShelfL_{};
    BiquadFilter highShelfR_{};
};

} // namespace viper
