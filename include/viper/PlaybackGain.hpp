#pragma once

#include <span>

namespace viper {

// ViPER Playback Gain Control (AGC)
// Smooth RMS-tracking Automatic Gain Control to keep quiet and loud songs consistent.
class PlaybackGain {
public:
    void prepare(double sampleRate) noexcept;
    void reset() noexcept;
    void configure(float strength, float maxGainDb, float outputThresholdDb) noexcept;

    void processInterleavedStereo(std::span<float> interleavedSamples) noexcept;

private:
    double sampleRate_ = 48000.0;
    double strength_ = 0.5;
    double maxGainLinear_ = 4.0;
    double targetLinear_ = 0.5;
    double rmsCoeff_ = 0.999;
    double gainSmoothCoeff_ = 0.999;

    double rmsSq_ = 0.01;
    double currentGain_ = 1.0;
};

} // namespace viper
