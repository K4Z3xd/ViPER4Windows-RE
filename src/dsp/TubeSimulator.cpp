#include "viper/TubeSimulator.hpp"
#include <algorithm>
#include <cmath>
#include <numbers>

namespace viper {

void TubeSimulator::prepare(double sampleRate) noexcept {
    // 1-pole DC blocker cutoff at ~10 Hz scaled dynamically to sampleRate
    const double safeSr = std::max(sampleRate, 8000.0);
    dcCoeff_ = 1.0 - (2.0 * std::numbers::pi * 10.0 / safeSr);
    dcCoeff_ = std::clamp(dcCoeff_, 0.9, 0.99995);
    reset();
}

void TubeSimulator::reset() noexcept {
    prevInL_ = 0.0;
    prevOutL_ = 0.0;
    prevInR_ = 0.0;
    prevOutR_ = 0.0;
}

void TubeSimulator::setDrive(float drive) noexcept {
    drive_ = std::clamp(static_cast<double>(drive), 0.0, 1.0);
}

void TubeSimulator::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    if (drive_ <= 0.001) {
        return;
    }

    const double k = 1.0 + drive_ * 3.0;
    const double asymmetry = 0.12 * drive_;
    const double norm = 1.0 / std::tanh(k);
    const size_t total = interleavedSamples.size() & ~size_t{1};

    for (size_t i = 0; i < total; i += 2) {
        const double xL = static_cast<double>(interleavedSamples[i]);
        const double xR = static_cast<double>(interleavedSamples[i + 1]);

        // Asymmetric triode transfer curve (generates warm 2nd harmonics)
        const double satL = std::tanh(k * (xL + asymmetry * xL * xL)) * norm;
        const double satR = std::tanh(k * (xR + asymmetry * xR * xR)) * norm;

        // Remove DC offset introduced by asymmetry
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
