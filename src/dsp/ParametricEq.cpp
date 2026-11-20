#include "viper/ParametricEq.hpp"
#include <algorithm>
#include <cmath>

namespace viper {

void ParametricEq::prepare(double sampleRate) noexcept {
    sampleRate_ = sampleRate;
    reset();

    constexpr double kQ10 = 1.41421356237; // 1-octave bandwidth
    for (size_t i = 0; i < kEqBandCount; ++i) {
        const double g = std::clamp(static_cast<double>(lastGains10Db_[i]), -12.0, 12.0);
        filters10L_[i].configure(BiquadType::PeakingEQ, sampleRate_, kEqFrequencies[i], kQ10, g);
        filters10R_[i].configure(BiquadType::PeakingEQ, sampleRate_, kEqFrequencies[i], kQ10, g);
    }

    constexpr double kQ18 = 2.6; // ~1/2-octave bandwidth for 18-band precision
    for (size_t i = 0; i < kEqBand18Count; ++i) {
        const double g = std::clamp(static_cast<double>(lastGains18Db_[i]), -12.0, 12.0);
        filters18L_[i].configure(BiquadType::PeakingEQ, sampleRate_, kEq18Frequencies[i], kQ18, g);
        filters18R_[i].configure(BiquadType::PeakingEQ, sampleRate_, kEq18Frequencies[i], kQ18, g);
    }
}

void ParametricEq::reset() noexcept {
    for (size_t i = 0; i < kEqBandCount; ++i) {
        filters10L_[i].reset();
        filters10R_[i].reset();
    }
    for (size_t i = 0; i < kEqBand18Count; ++i) {
        filters18L_[i].reset();
        filters18R_[i].reset();
    }
}

void ParametricEq::updateGains(uint32_t eqMode,
                               const std::array<float, kEqBandCount>& bands10Db,
                               const std::array<float, kEqBand18Count>& bands18Db) noexcept {
    eqMode_ = (eqMode == 1u) ? 1u : 0u;

    constexpr double kQ10 = 1.41421356237;
    for (size_t i = 0; i < kEqBandCount; ++i) {
        const float clamped = std::clamp(bands10Db[i], -12.0f, 12.0f);
        if (std::abs(clamped - lastGains10Db_[i]) > 1e-4f) {
            lastGains10Db_[i] = clamped;
            filters10L_[i].configure(BiquadType::PeakingEQ, sampleRate_, kEqFrequencies[i], kQ10, clamped);
            filters10R_[i].configure(BiquadType::PeakingEQ, sampleRate_, kEqFrequencies[i], kQ10, clamped);
        }
    }

    constexpr double kQ18 = 2.6;
    for (size_t i = 0; i < kEqBand18Count; ++i) {
        const float clamped = std::clamp(bands18Db[i], -12.0f, 12.0f);
        if (std::abs(clamped - lastGains18Db_[i]) > 1e-4f) {
            lastGains18Db_[i] = clamped;
            filters18L_[i].configure(BiquadType::PeakingEQ, sampleRate_, kEq18Frequencies[i], kQ18, clamped);
            filters18R_[i].configure(BiquadType::PeakingEQ, sampleRate_, kEq18Frequencies[i], kQ18, clamped);
        }
    }
}

void ParametricEq::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    const size_t total = interleavedSamples.size() & ~size_t{1};

    if (eqMode_ == 0u) {
        for (size_t i = 0; i < total; i += 2) {
            double l = static_cast<double>(interleavedSamples[i]);
            double r = static_cast<double>(interleavedSamples[i + 1]);
            for (size_t b = 0; b < kEqBandCount; ++b) {
                if (std::abs(lastGains10Db_[b]) > 1e-4f) {
                    l = filters10L_[b].processSample(l);
                    r = filters10R_[b].processSample(r);
                }
            }
            interleavedSamples[i] = static_cast<float>(l);
            interleavedSamples[i + 1] = static_cast<float>(r);
        }
    } else {
        for (size_t i = 0; i < total; i += 2) {
            double l = static_cast<double>(interleavedSamples[i]);
            double r = static_cast<double>(interleavedSamples[i + 1]);
            for (size_t b = 0; b < kEqBand18Count; ++b) {
                if (std::abs(lastGains18Db_[b]) > 1e-4f) {
                    l = filters18L_[b].processSample(l);
                    r = filters18R_[b].processSample(r);
                }
            }
            interleavedSamples[i] = static_cast<float>(l);
            interleavedSamples[i + 1] = static_cast<float>(r);
        }
    }
}

} // namespace viper
