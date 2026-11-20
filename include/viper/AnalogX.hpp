#pragma once

#include "viper/BiquadFilter.hpp"
#include <cstdint>
#include <span>

namespace viper {

enum class AnalogXLevel : uint32_t {
    Slight = 0,
    Moderate = 1,
    Extreme = 2
};

// ViPER AnalogX: Class-A solid-state analog amplifier warmth & harmonic density.
class AnalogX {
public:
    void prepare(double sampleRate) noexcept;
    void reset() noexcept;
    void configure(AnalogXLevel level) noexcept;

    void processInterleavedStereo(std::span<float> interleavedSamples) noexcept;

private:
    double sampleRate_ = 48000.0;
    AnalogXLevel level_ = AnalogXLevel::Slight;
    double drive_ = 0.2;
    double dcCoeff_ = 0.9995;

    double prevInL_ = 0.0, prevOutL_ = 0.0;
    double prevInR_ = 0.0, prevOutR_ = 0.0;

    BiquadFilter warmthShelfL_{};
    BiquadFilter warmthShelfR_{};
};

} // namespace viper
