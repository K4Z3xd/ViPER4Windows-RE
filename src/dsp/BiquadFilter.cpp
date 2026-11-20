#include "viper/BiquadFilter.hpp"
#include <algorithm>
#include <cmath>
#include <numbers>

namespace viper {

void BiquadFilter::reset() noexcept {
    z1_ = 0.0;
    z2_ = 0.0;
}

void BiquadFilter::configure(BiquadType type,
                             double sampleRate,
                             double frequencyHz,
                             double q,
                             double gainDb) noexcept {
    const double safeSr = std::max(sampleRate, 8000.0);
    // Keep cutoff safely below Nyquist (even at 44.1kHz -> max ~19.8kHz; at 384kHz -> up to 172kHz)
    const double safeFreq = std::clamp(frequencyHz, 10.0, safeSr * 0.45);
    const double safeQ = std::max(q, 0.1);

    const double w0 = 2.0 * std::numbers::pi * (safeFreq / safeSr);
    const double cosW0 = std::cos(w0);
    const double sinW0 = std::sin(w0);
    const double alpha = sinW0 / (2.0 * safeQ);
    const double A = std::pow(10.0, gainDb / 40.0);

    double b0 = 1.0, b1 = 0.0, b2 = 0.0;
    double a0 = 1.0, a1 = 0.0, a2 = 0.0;

    switch (type) {
        case BiquadType::LowPass:
            b0 = (1.0 - cosW0) * 0.5;
            b1 = 1.0 - cosW0;
            b2 = (1.0 - cosW0) * 0.5;
            a0 = 1.0 + alpha;
            a1 = -2.0 * cosW0;
            a2 = 1.0 - alpha;
            break;

        case BiquadType::HighPass:
            b0 = (1.0 + cosW0) * 0.5;
            b1 = -(1.0 + cosW0);
            b2 = (1.0 + cosW0) * 0.5;
            a0 = 1.0 + alpha;
            a1 = -2.0 * cosW0;
            a2 = 1.0 - alpha;
            break;

        case BiquadType::BandPass:
            b0 = alpha;
            b1 = 0.0;
            b2 = -alpha;
            a0 = 1.0 + alpha;
            a1 = -2.0 * cosW0;
            a2 = 1.0 - alpha;
            break;

        case BiquadType::PeakingEQ:
            b0 = 1.0 + alpha * A;
            b1 = -2.0 * cosW0;
            b2 = 1.0 - alpha * A;
            a0 = 1.0 + alpha / A;
            a1 = -2.0 * cosW0;
            a2 = 1.0 - alpha / A;
            break;

        case BiquadType::LowShelf: {
            const double twoSqrtAAlpha = 2.0 * std::sqrt(A) * alpha;
            b0 = A * ((A + 1.0) - (A - 1.0) * cosW0 + twoSqrtAAlpha);
            b1 = 2.0 * A * ((A - 1.0) - (A + 1.0) * cosW0);
            b2 = A * ((A + 1.0) - (A - 1.0) * cosW0 - twoSqrtAAlpha);
            a0 = (A + 1.0) + (A - 1.0) * cosW0 + twoSqrtAAlpha;
            a1 = -2.0 * ((A - 1.0) + (A + 1.0) * cosW0);
            a2 = (A + 1.0) + (A - 1.0) * cosW0 - twoSqrtAAlpha;
            break;
        }

        case BiquadType::HighShelf: {
            const double twoSqrtAAlpha = 2.0 * std::sqrt(A) * alpha;
            b0 = A * ((A + 1.0) + (A - 1.0) * cosW0 + twoSqrtAAlpha);
            b1 = -2.0 * A * ((A - 1.0) + (A + 1.0) * cosW0);
            b2 = A * ((A + 1.0) + (A - 1.0) * cosW0 - twoSqrtAAlpha);
            a0 = (A + 1.0) - (A - 1.0) * cosW0 + twoSqrtAAlpha;
            a1 = 2.0 * ((A - 1.0) - (A + 1.0) * cosW0);
            a2 = (A + 1.0) - (A - 1.0) * cosW0 - twoSqrtAAlpha;
            break;
        }
    }

    const double invA0 = 1.0 / a0;
    b0_ = b0 * invA0;
    b1_ = b1 * invA0;
    b2_ = b2 * invA0;
    a1_ = a1 * invA0;
    a2_ = a2 * invA0;
}

} // namespace viper
