#include "viper/AnalogX.hpp"
#include <algorithm>
#include <cmath>
#include <numbers>

namespace viper {

void AnalogX::prepare(double sampleRate) noexcept {
    sampleRate_ = std::max(sampleRate, 8000.0);
    dcCoeff_ = std::clamp(1.0 - (2.0 * std::numbers::pi * 10.0 / sampleRate_), 0.9, 0.99995);
    reset();
    configure(level_);
}

void AnalogX::reset() noexcept {
    prevInL_ = prevOutL_ = 0.0;
    prevInR_ = prevOutR_ = 0.0;
    warmthShelfL_.reset();
    warmthShelfR_.reset();
}

void AnalogX::configure(AnalogXLevel level) noexcept {
    level_ = level;
    double shelfDb = 0.8;
    switch (level) {
        case AnalogXLevel::Slight:
            drive_ = 0.22;
            shelfDb = 0.8;
            break;
        case AnalogXLevel::Moderate:
            drive_ = 0.45;
            shelfDb = 1.6;
            break;
        case AnalogXLevel::Extreme:
            drive_ = 0.72;
            shelfDb = 2.4;
            break;
    }
    warmthShelfL_.configure(BiquadType::LowShelf, sampleRate_, 220.0, 0.7071, shelfDb);
    warmthShelfR_.configure(BiquadType::LowShelf, sampleRate_, 220.0, 0.7071, shelfDb);
}

void AnalogX::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    const double k = 1.0 + drive_ * 1.8;
    const double invNorm = 1.0 / std::atan(k);
    const size_t total = interleavedSamples.size() & ~size_t{1};

    for (size_t i = 0; i < total; i += 2) {
        double l = warmthShelfL_.processSample(interleavedSamples[i]);
        double r = warmthShelfR_.processSample(interleavedSamples[i + 1]);

        // Class-A arctan harmonic density (smoother odd/even balance than 6N1J triode)
        const double satL = std::atan(k * (l + 0.06 * drive_ * l * l)) * invNorm;
        const double satR = std::atan(k * (r + 0.06 * drive_ * r * r)) * invNorm;

        const double outL = satL - prevInL_ + dcCoeff_ * prevOutL_;
        prevInL_ = satL;
        prevOutL_ = (std::abs(outL) < 1e-18) ? 0.0 : outL;

        const double outR = satR - prevInR_ + dcCoeff_ * prevOutR_;
        prevInR_ = satR;
        prevOutR_ = (std::abs(outR) < 1e-18) ? 0.0 : outR;

        interleavedSamples[i] = static_cast<float>(prevOutL_);
        interleavedSamples[i + 1] = static_cast<float>(prevOutR_);
    }
}

} // namespace viper
