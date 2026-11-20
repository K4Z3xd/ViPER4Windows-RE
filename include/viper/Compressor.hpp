#pragma once

#include <span>

namespace viper {

// Hi-Res Soft-Knee Stereo Compressor / Playback Gain Control.
// Sample-rate invariant attack/release envelopes up to 384kHz.
class Compressor {
public:
    void prepare(double sampleRate) noexcept;
    void reset() noexcept;
    void configure(float thresholdDb, float ratio, float attackMs, float releaseMs, float makeupGainDb) noexcept;

    void processInterleavedStereo(std::span<float> interleavedSamples) noexcept;

private:
    double sampleRate_ = 48000.0;
    double thresholdDb_ = -18.0;
    double ratio_ = 3.0;
    double attackCoeff_ = 0.0;
    double releaseCoeff_ = 0.0;
    double makeupLinear_ = 1.0;
    double envDb_ = -96.0;
};

} // namespace viper
