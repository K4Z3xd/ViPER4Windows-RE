#pragma once

#include <span>

namespace viper {

// Sample-rate-aware Peak Limiter with smooth envelope release to prevent
// digital clipping above 0 dBFS after Bass/Clarity/EQ boosts.
class Limiter {
public:
    void prepare(double sampleRate) noexcept;
    void reset() noexcept;
    void setThresholdDb(float thresholdDb) noexcept;

    void processInterleavedStereo(std::span<float> interleavedSamples) noexcept;

private:
    double sampleRate_ = 48000.0;
    double thresholdLinear_ = 0.966; // ~ -0.3 dBFS
    double attackCoeff_ = 0.0;
    double releaseCoeff_ = 0.0;
    double envelope_ = 0.0;
};

} // namespace viper
