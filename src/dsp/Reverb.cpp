#include "viper/Reverb.hpp"
#include <algorithm>
#include <cmath>

namespace viper {

namespace {
// Classic Schroeder/Freeverb prime tuning lengths @ 44100 Hz
constexpr std::array<size_t, 8> kCombTunings44k = {1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617};
constexpr std::array<size_t, 4> kAllPassTunings44k = {556, 441, 341, 225};
constexpr size_t kStereoSpread44k = 23;
} // namespace

void Reverb::prepare(double sampleRate) {
    const double scale = std::max(sampleRate, 8000.0) / 44100.0;
    const size_t spread = std::max<size_t>(1, static_cast<size_t>(std::lround(kStereoSpread44k * scale)));

    for (size_t i = 0; i < kNumCombs; ++i) {
        const size_t lenL = std::max<size_t>(16, static_cast<size_t>(std::lround(kCombTunings44k[i] * scale)));
        combL_[i].resize(lenL);
        combR_[i].resize(lenL + spread);
    }

    for (size_t i = 0; i < kNumAllPasses; ++i) {
        const size_t lenL = std::max<size_t>(16, static_cast<size_t>(std::lround(kAllPassTunings44k[i] * scale)));
        allPassL_[i].resize(lenL);
        allPassR_[i].resize(lenL + spread);
    }
}

void Reverb::reset() noexcept {
    for (auto& c : combL_) c.clear();
    for (auto& c : combR_) c.clear();
    for (auto& a : allPassL_) a.clear();
    for (auto& a : allPassR_) a.clear();
}

void Reverb::configure(float roomSize, float width, float damping, float wet, float dry) noexcept {
    const float r = std::clamp(roomSize, 0.0f, 1.0f);
    const float w = std::clamp(width, 0.0f, 1.0f);
    const float d = std::clamp(damping, 0.0f, 1.0f);
    const float wetClamped = std::clamp(wet, 0.0f, 1.0f);
    const float dryClamped = std::clamp(dry, 0.0f, 1.0f);

    feedback_ = 0.70f + r * 0.27f; // 0.70 .. 0.97 (always stable!)
    damp1_ = d * 0.55f;
    damp2_ = 1.0f - damp1_;

    const float wetScaled = wetClamped * 0.45f;
    wet1_ = wetScaled * (w * 0.5f + 0.5f);
    wet2_ = wetScaled * ((1.0f - w) * 0.5f);
    dry_ = dryClamped;
}

void Reverb::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    if (combL_[0].buf.empty()) {
        return;
    }

    constexpr float kFixedInputGain = 0.015f;
    const size_t total = interleavedSamples.size() & ~size_t{1};

    for (size_t i = 0; i < total; i += 2) {
        const float inL = interleavedSamples[i];
        const float inR = interleavedSamples[i + 1];
        const float monoIn = (inL + inR) * kFixedInputGain;

        float outL = 0.0f;
        float outR = 0.0f;

        for (size_t c = 0; c < kNumCombs; ++c) {
            outL += combL_[c].process(monoIn, feedback_, damp1_, damp2_);
            outR += combR_[c].process(monoIn, feedback_, damp1_, damp2_);
        }

        for (size_t a = 0; a < kNumAllPasses; ++a) {
            outL = allPassL_[a].process(outL);
            outR = allPassR_[a].process(outR);
        }

        interleavedSamples[i] = inL * dry_ + outL * wet1_ + outR * wet2_;
        interleavedSamples[i + 1] = inR * dry_ + outR * wet1_ + outL * wet2_;
    }
}

} // namespace viper
