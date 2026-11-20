#include "viper/Compressor.hpp"
#include <algorithm>
#include <cmath>

namespace viper {

void Compressor::prepare(double sampleRate) noexcept {
    sampleRate_ = std::max(sampleRate, 8000.0);
    reset();
    configure(static_cast<float>(thresholdDb_), static_cast<float>(ratio_), 10.0f, 120.0f, 0.0f);
}

void Compressor::reset() noexcept {
    envDb_ = -96.0;
}

void Compressor::configure(float thresholdDb, float ratio, float attackMs, float releaseMs, float makeupGainDb) noexcept {
    thresholdDb_ = std::clamp(static_cast<double>(thresholdDb), -40.0, 0.0);
    ratio_ = std::clamp(static_cast<double>(ratio), 1.0, 20.0);
    const double attSec = std::clamp(static_cast<double>(attackMs), 0.5, 200.0) * 0.001;
    const double relSec = std::clamp(static_cast<double>(releaseMs), 10.0, 1000.0) * 0.001;

    attackCoeff_ = std::exp(-1.0 / (sampleRate_ * attSec));
    releaseCoeff_ = std::exp(-1.0 / (sampleRate_ * relSec));

    const double makeup = std::clamp(static_cast<double>(makeupGainDb), 0.0, 18.0);
    makeupLinear_ = std::pow(10.0, makeup / 20.0);
}

void Compressor::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    constexpr double kKneeWidthDb = 6.0;
    const double halfKnee = kKneeWidthDb * 0.5;
    const size_t total = interleavedSamples.size() & ~size_t{1};

    for (size_t i = 0; i < total; i += 2) {
        const double l = static_cast<double>(interleavedSamples[i]);
        const double r = static_cast<double>(interleavedSamples[i + 1]);
        const double peak = std::max(std::abs(l), std::abs(r));
        const double inDb = (peak > 1e-6) ? (20.0 * std::log10(peak)) : -120.0;

        if (inDb > envDb_) {
            envDb_ = attackCoeff_ * envDb_ + (1.0 - attackCoeff_) * inDb;
        } else {
            envDb_ = releaseCoeff_ * envDb_ + (1.0 - releaseCoeff_) * inDb;
        }

        // Soft-knee gain reduction computation
        const double overDb = envDb_ - thresholdDb_;
        double gainReductionDb = 0.0;
        if (overDb > halfKnee) {
            gainReductionDb = overDb * (1.0 - 1.0 / ratio_);
        } else if (overDb > -halfKnee) {
            const double x = overDb + halfKnee;
            gainReductionDb = (1.0 - 1.0 / ratio_) * (x * x) / (2.0 * kKneeWidthDb);
        }

        const double totalGain = std::pow(10.0, -gainReductionDb / 20.0) * makeupLinear_;
        interleavedSamples[i] = static_cast<float>(l * totalGain);
        interleavedSamples[i + 1] = static_cast<float>(r * totalGain);
    }
}

} // namespace viper
