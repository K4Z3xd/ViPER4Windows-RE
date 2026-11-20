#pragma once

#include <cmath>
#include <numbers>

namespace viper {

enum class BiquadType {
    LowPass,
    HighPass,
    BandPass,
    PeakingEQ,
    LowShelf,
    HighShelf
};

// High-precision 64-bit internal Biquad Filter (Direct Form II Transposed).
// Using double precision prevents coefficient quantization noise & instability
// at high sample rates (192kHz / 384kHz) for low-frequency bands (31Hz - 60Hz).
class BiquadFilter {
public:
    void reset() noexcept;

    void configure(BiquadType type,
                   double sampleRate,
                   double frequencyHz,
                   double q,
                   double gainDb = 0.0) noexcept;

    [[nodiscard]] inline double processSample(double x) noexcept {
        const double y = b0_ * x + z1_;
        z1_ = b1_ * x - a1_ * y + z2_;
        z2_ = b2_ * x - a2_ * y;

        // Flush denormals to zero to prevent CPU spikes on silent passages
        if (std::abs(z1_) < 1e-18) z1_ = 0.0;
        if (std::abs(z2_) < 1e-18) z2_ = 0.0;
        return y;
    }

private:
    double b0_ = 1.0;
    double b1_ = 0.0;
    double b2_ = 0.0;
    double a1_ = 0.0;
    double a2_ = 0.0;

    double z1_ = 0.0;
    double z2_ = 0.0;
};

} // namespace viper
