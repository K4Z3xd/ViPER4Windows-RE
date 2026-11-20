#include "viper/Limiter.hpp"
#include <algorithm>
#include <cmath>

namespace viper {

void Limiter::prepare(double sampleRate) noexcept {
    sampleRate_ = std::max(sampleRate, 8000.0);
    // Ultra-fast attack (0.2 ms), musical release (60 ms), sample-rate invariant
    attackCoeff_ = std::exp(-1.0 / (sampleRate_ * 0.0002));
    releaseCoeff_ = std::exp(-1.0 / (sampleRate_ * 0.060));
    reset();
}

void Limiter::reset() noexcept {
    envelope_ = 0.0;
}

void Limiter::setThresholdDb(float thresholdDb) noexcept {
    const float clamped = std::clamp(thresholdDb, -12.0f, 0.0f);
    thresholdLinear_ = std::pow(10.0, static_cast<double>(clamped) / 20.0);
}

void Limiter::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    const size_t total = interleavedSamples.size() & ~size_t{1};

    for (size_t i = 0; i < total; i += 2) {
        const double l = static_cast<double>(interleavedSamples[i]);
        const double r = static_cast<double>(interleavedSamples[i + 1]);
        const double peak = std::max(std::abs(l), std::abs(r));

        if (peak > envelope_) {
            envelope_ = attackCoeff_ * envelope_ + (1.0 - attackCoeff_) * peak;
        } else {
            envelope_ = releaseCoeff_ * envelope_ + (1.0 - releaseCoeff_) * peak;
        }

        if (envelope_ < 1e-18) {
            envelope_ = 0.0;
        }

        double gain = 1.0;
        if (envelope_ > thresholdLinear_) {
            gain = thresholdLinear_ / envelope_;
        }

        // Final hard-safety clamp so no sample ever exceeds 1.0f
        const double outL = std::clamp(l * gain, -thresholdLinear_, thresholdLinear_);
        const double outR = std::clamp(r * gain, -thresholdLinear_, thresholdLinear_);

        interleavedSamples[i] = static_cast<float>(outL);
        interleavedSamples[i + 1] = static_cast<float>(outR);
    }
}

} // namespace viper
