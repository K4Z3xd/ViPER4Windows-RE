#include "viper/FieldSurround.hpp"
#include <algorithm>
#include <cmath>

namespace viper {

void FieldSurround::prepare(double sampleRate) noexcept {
    sampleRate_ = std::max(sampleRate, 8000.0);
    // Keep sub-bass mono (< 120 Hz) while widening side channel cleanly
    sideHpL_.configure(BiquadType::HighPass, sampleRate_, 120.0, 0.7071);
    midPresence_.configure(BiquadType::PeakingEQ, sampleRate_, 1800.0, 0.9, 0.0);
    reset();
}

void FieldSurround::reset() noexcept {
    sideHpL_.reset();
    midPresence_.reset();
}

void FieldSurround::configure(float strength, float midImage) noexcept {
    const double s = std::clamp(static_cast<double>(strength), 0.0, 1.0);
    const double m = std::clamp(static_cast<double>(midImage), 0.0, 1.0);

    sideGain_ = 1.0 + s * 0.95;           // 1.0 .. 1.95x side width
    midGain_ = 0.80 + m * 0.50;           // 0.80 .. 1.30x center vocal focus
    const double vocalBoostDb = (m - 0.5) * 3.0;
    midPresence_.configure(BiquadType::PeakingEQ, sampleRate_, 1800.0, 0.9, vocalBoostDb);
}

void FieldSurround::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    const size_t total = interleavedSamples.size() & ~size_t{1};

    for (size_t i = 0; i < total; i += 2) {
        const double l = static_cast<double>(interleavedSamples[i]);
        const double r = static_cast<double>(interleavedSamples[i + 1]);

        const double mid = midPresence_.processSample(0.5 * (l + r)) * midGain_;
        const double sideRaw = 0.5 * (l - r);
        const double sideHi = sideHpL_.processSample(sideRaw);
        const double sideLow = sideRaw - sideHi;
        const double side = sideLow + sideHi * sideGain_;

        interleavedSamples[i] = static_cast<float>(mid + side);
        interleavedSamples[i + 1] = static_cast<float>(mid - side);
    }
}

} // namespace viper
