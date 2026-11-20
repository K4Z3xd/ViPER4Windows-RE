#pragma once

#include <vector>
#include <span>

namespace viper {

// Hi-Res Differential Surround (Haas Delay) + Mid/Side Stereo Widener.
// Dynamically sizes its delay ring buffer to support up to 25ms even at 384kHz
// without any allocation during real-time audio processing.
class DifferentialSurround {
public:
    void prepare(double sampleRate);
    void reset() noexcept;
    void configure(float delayMs, float stereoWidth) noexcept;

    void processInterleavedStereo(std::span<float> interleavedSamples) noexcept;

private:
    double sampleRate_ = 48000.0;
    size_t delaySamples_ = 0;
    float stereoWidth_ = 1.0f;

    std::vector<float> delayBufferR_;
    size_t writeIndex_ = 0;
};

} // namespace viper
