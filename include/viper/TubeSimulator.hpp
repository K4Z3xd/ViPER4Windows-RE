#pragma once

#include <span>

namespace viper {

// Analog Tube Sound Simulator (inspired by ViPER 6N1J)
// Asymmetric soft saturation generating warm even-order (2nd) harmonics
// with automatic DC offset removal.
class TubeSimulator {
public:
    void prepare(double sampleRate) noexcept;
    void reset() noexcept;
    void setDrive(float drive) noexcept;

    void processInterleavedStereo(std::span<float> interleavedSamples) noexcept;

private:
    double dcCoeff_ = 0.9995;
    double drive_ = 0.25;

    // 1-pole DC blocker state per channel
    double prevInL_ = 0.0;
    double prevOutL_ = 0.0;
    double prevInR_ = 0.0;
    double prevOutR_ = 0.0;
};

} // namespace viper
