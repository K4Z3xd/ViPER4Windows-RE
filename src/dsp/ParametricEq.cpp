#include "viper/ParametricEq.hpp"
#include <algorithm>
#include <cmath>

namespace viper {

void ParametricEq::prepare(double sampleRate) noexcept {
    sampleRate_ = sampleRate;
    reset();
    // Force coefficient computation for current gains at new sample rate
    for (size_t i = 0; i < kEqBandCount; ++i) {
        constexpr double kOctaveQ = 1.41421356237; // 1-octave bandwidth
        const double gain = std::clamp(static_cast<double>(lastGainsDb_[i]), -12.0, 12.0);
        filtersL_[i].configure(BiquadType::PeakingEQ, sampleRate_, kEqFrequencies[i], kOctaveQ, gain);
        filtersR_[i].configure(BiquadType::PeakingEQ, sampleRate_, kEqFrequencies[i], kOctaveQ, gain);
    }
}

void ParametricEq::reset() noexcept {
    for (size_t i = 0; i < kEqBandCount; ++i) {
        filtersL_[i].reset();
        filtersR_[i].reset();
    }
}

void ParametricEq::updateGains(const std::array<float, kEqBandCount>& bandsDb) noexcept {
    constexpr double kOctaveQ = 1.41421356237;
    for (size_t i = 0; i < kEqBandCount; ++i) {
        const float clamped = std::clamp(bandsDb[i], -12.0f, 12.0f);
        if (std::abs(clamped - lastGainsDb_[i]) > 1e-4f) {
            lastGainsDb_[i] = clamped;
            filtersL_[i].configure(BiquadType::PeakingEQ, sampleRate_, kEqFrequencies[i], kOctaveQ, clamped);
            filtersR_[i].configure(BiquadType::PeakingEQ, sampleRate_, kEqFrequencies[i], kOctaveQ, clamped);
        }
    }
}

void ParametricEq::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    const size_t total = interleavedSamples.size() & ~size_t{1};
    for (size_t i = 0; i < total; i += 2) {
        double l = static_cast<double>(interleavedSamples[i]);
        double r = static_cast<double>(interleavedSamples[i + 1]);

        for (size_t b = 0; b < kEqBandCount; ++b) {
            if (std::abs(lastGainsDb_[b]) > 1e-4f) {
                l = filtersL_[b].processSample(l);
                r = filtersR_[b].processSample(r);
            }
        }

        interleavedSamples[i] = static_cast<float>(l);
        interleavedSamples[i + 1] = static_cast<float>(r);
    }
}

} // namespace viper
