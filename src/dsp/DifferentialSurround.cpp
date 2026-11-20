#include "viper/DifferentialSurround.hpp"
#include <algorithm>
#include <cmath>

namespace viper {

void DifferentialSurround::prepare(double sampleRate) {
    sampleRate_ = std::max(sampleRate, 8000.0);
    // Pre-allocate ring buffer for up to 25 ms at current sampleRate (9600 samples @ 384kHz)
    const size_t maxDelaySamples = static_cast<size_t>(std::ceil(sampleRate_ * 0.025)) + 16;
    delayBufferR_.assign(maxDelaySamples, 0.0f);
    writeIndex_ = 0;
}

void DifferentialSurround::reset() noexcept {
    std::fill(delayBufferR_.begin(), delayBufferR_.end(), 0.0f);
    writeIndex_ = 0;
}

void DifferentialSurround::configure(float delayMs, float stereoWidth) noexcept {
    const float clampedMs = std::clamp(delayMs, 0.0f, 20.0f);
    stereoWidth_ = std::clamp(stereoWidth, 0.0f, 2.0f);

    if (delayBufferR_.empty()) {
        delaySamples_ = 0;
        return;
    }

    const size_t requested = static_cast<size_t>(std::lround((clampedMs * 0.001) * sampleRate_));
    delaySamples_ = std::min(requested, delayBufferR_.size() - 1);
}

void DifferentialSurround::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    if (delayBufferR_.empty()) {
        return;
    }

    const size_t bufSize = delayBufferR_.size();
    const size_t total = interleavedSamples.size() & ~size_t{1};

    for (size_t i = 0; i < total; i += 2) {
        float l = interleavedSamples[i];
        float r = interleavedSamples[i + 1];

        // 1. Mid/Side Stereo Width adjustment
        if (std::abs(stereoWidth_ - 1.0f) > 1e-4f) {
            const float mid = 0.5f * (l + r);
            const float side = 0.5f * (l - r) * stereoWidth_;
            l = mid + side;
            r = mid - side;
        }

        // 2. Haas Differential Delay on Right channel
        delayBufferR_[writeIndex_] = r;
        if (delaySamples_ > 0) {
            const size_t readIndex = (writeIndex_ + bufSize - delaySamples_) % bufSize;
            r = delayBufferR_[readIndex];
        }
        writeIndex_ = (writeIndex_ + 1) % bufSize;

        interleavedSamples[i] = l;
        interleavedSamples[i + 1] = r;
    }
}

} // namespace viper
