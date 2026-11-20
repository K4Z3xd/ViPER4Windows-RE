#include "viper/PlaybackGain.hpp"
#include <algorithm>
#include <cmath>

namespace viper {

void PlaybackGain::prepare(double sampleRate) noexcept {
    sampleRate_ = std::max(sampleRate, 8000.0);
    // Smooth 250ms RMS loudness window + 180ms gain slew (no pumping!)
    rmsCoeff_ = std::exp(-1.0 / (sampleRate_ * 0.250));
    gainSmoothCoeff_ = std::exp(-1.0 / (sampleRate_ * 0.180));
    reset();
}

void PlaybackGain::reset() noexcept {
    rmsSq_ = 0.01;
    currentGain_ = 1.0;
}

void PlaybackGain::configure(float strength, float maxGainDb, float outputThresholdDb) noexcept {
    strength_ = std::clamp(static_cast<double>(strength), 0.0, 1.0);
    const double maxDb = std::clamp(static_cast<double>(maxGainDb), 0.0, 24.0);
    const double threshDb = std::clamp(static_cast<double>(outputThresholdDb), -12.0, 0.0);

    maxGainLinear_ = std::pow(10.0, maxDb / 20.0);
    // Target RMS sits ~9 dB below output threshold for natural headroom
    targetLinear_ = std::pow(10.0, (threshDb - 9.0) / 20.0);
}

void PlaybackGain::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    if (strength_ <= 0.005) {
        return;
    }

    const size_t total = interleavedSamples.size() & ~size_t{1};
    for (size_t i = 0; i < total; i += 2) {
        const double l = static_cast<double>(interleavedSamples[i]);
        const double r = static_cast<double>(interleavedSamples[i + 1]);
        const double power = 0.5 * (l * l + r * r);

        rmsSq_ = rmsCoeff_ * rmsSq_ + (1.0 - rmsCoeff_) * power;
        if (rmsSq_ < 1e-12) rmsSq_ = 1e-12;

        const double rms = std::sqrt(rmsSq_);
        // Ignore near-silence noise floor (< -55 dBFS) so background hiss isn't boosted
        double desiredGain = 1.0;
        if (rms > 0.0018) {
            const double rawRatio = targetLinear_ / rms;
            // Blend between 1.0 (no AGC) and full target ratio by strength_
            desiredGain = std::clamp(1.0 + (rawRatio - 1.0) * strength_, 0.25, maxGainLinear_);
        }

        currentGain_ = gainSmoothCoeff_ * currentGain_ + (1.0 - gainSmoothCoeff_) * desiredGain;

        interleavedSamples[i] = static_cast<float>(l * currentGain_);
        interleavedSamples[i + 1] = static_cast<float>(r * currentGain_);
    }
}

} // namespace viper
