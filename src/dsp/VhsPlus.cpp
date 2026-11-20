#include "viper/VhsPlus.hpp"
#include <algorithm>
#include <cmath>

namespace viper {

void VhsPlus::prepare(double sampleRate) {
    sampleRate_ = std::max(sampleRate, 8000.0);
    // Max 45 ms early reflection ring buffer (17,280 samples @ 384kHz)
    const size_t bufLen = static_cast<size_t>(std::ceil(sampleRate_ * 0.045)) + 16;
    bufL_.assign(bufLen, 0.0f);
    bufR_.assign(bufLen, 0.0f);
    writeIdx_ = 0;

    dampL_.configure(BiquadType::LowPass, sampleRate_, 6200.0, 0.7071);
    dampR_.configure(BiquadType::LowPass, sampleRate_, 6200.0, 0.7071);
    configure(level_);
    reset();
}

void VhsPlus::reset() noexcept {
    std::fill(bufL_.begin(), bufL_.end(), 0.0f);
    std::fill(bufR_.begin(), bufR_.end(), 0.0f);
    writeIdx_ = 0;
    dampL_.reset();
    dampR_.reset();
}

void VhsPlus::configure(uint32_t level) noexcept {
    level_ = std::clamp(level, 1u, 5u);
    const double scale = 0.7 + static_cast<double>(level_) * 0.22; // 0.92 .. 1.80
    wetMix_ = 0.12f + static_cast<float>(level_) * 0.045f;

    if (bufL_.empty()) return;
    const size_t maxIdx = bufL_.size() - 1;
    tap1_ = std::min<size_t>(maxIdx, std::max<size_t>(1, static_cast<size_t>(std::lround(sampleRate_ * 0.007 * scale))));
    tap2_ = std::min<size_t>(maxIdx, std::max<size_t>(1, static_cast<size_t>(std::lround(sampleRate_ * 0.014 * scale))));
    tap3_ = std::min<size_t>(maxIdx, std::max<size_t>(1, static_cast<size_t>(std::lround(sampleRate_ * 0.022 * scale))));
}

void VhsPlus::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    if (bufL_.empty()) return;

    const size_t sz = bufL_.size();
    const size_t total = interleavedSamples.size() & ~size_t{1};

    for (size_t i = 0; i < total; i += 2) {
        const float l = interleavedSamples[i];
        const float r = interleavedSamples[i + 1];

        const float dL = static_cast<float>(dampL_.processSample(l));
        const float dR = static_cast<float>(dampR_.processSample(r));
        bufL_[writeIdx_] = dL;
        bufR_[writeIdx_] = dR;

        const size_t r1 = (writeIdx_ + sz - tap1_) % sz;
        const size_t r2 = (writeIdx_ + sz - tap2_) % sz;
        const size_t r3 = (writeIdx_ + sz - tap3_) % sz;

        // Asymmetric binaural early reflections (cross-coupled L <-> R)
        const float reflL = 0.50f * bufR_[r1] + 0.32f * bufL_[r2] - 0.18f * bufR_[r3];
        const float reflR = 0.50f * bufL_[r1] + 0.32f * bufR_[r2] - 0.18f * bufL_[r3];

        writeIdx_ = (writeIdx_ + 1) % sz;

        interleavedSamples[i] = l + reflL * wetMix_;
        interleavedSamples[i + 1] = r + reflR * wetMix_;
    }
}

} // namespace viper
