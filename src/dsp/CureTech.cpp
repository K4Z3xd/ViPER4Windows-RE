#include "viper/CureTech.hpp"
#include <algorithm>
#include <cmath>

namespace viper {

void CureTech::prepare(double sampleRate) {
    sampleRate_ = std::max(sampleRate, 8000.0);
    // ~0.26 ms Interaural Time Delay (ITD) for natural binaural speaker crossfeed
    const size_t maxItd = static_cast<size_t>(std::ceil(sampleRate_ * 0.001)) + 8;
    delayL_.assign(maxItd, 0.0f);
    delayR_.assign(maxItd, 0.0f);
    writeIdx_ = 0;

    itdSamples_ = std::clamp<size_t>(
        static_cast<size_t>(std::lround(sampleRate_ * 0.00026)),
        1,
        maxItd - 1);

    // Acoustic head-shadow low-pass filter (~700 Hz)
    lpL_.configure(BiquadType::LowPass, sampleRate_, 700.0, 0.7071);
    lpR_.configure(BiquadType::LowPass, sampleRate_, 700.0, 0.7071);
    reset();
}

void CureTech::reset() noexcept {
    lpL_.reset();
    lpR_.reset();
    std::fill(delayL_.begin(), delayL_.end(), 0.0f);
    std::fill(delayR_.begin(), delayR_.end(), 0.0f);
    writeIdx_ = 0;
}

void CureTech::configure(CureLevel level) noexcept {
    switch (level) {
        case CureLevel::Slight:
            crossfeedGain_ = 0.16;
            directGain_ = 0.92;
            break;
        case CureLevel::Moderate:
            crossfeedGain_ = 0.26;
            directGain_ = 0.85;
            break;
        case CureLevel::Extreme:
            crossfeedGain_ = 0.38;
            directGain_ = 0.78;
            break;
    }
}

void CureTech::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    if (delayL_.empty()) {
        return;
    }

    const size_t bufSize = delayL_.size();
    const size_t total = interleavedSamples.size() & ~size_t{1};

    for (size_t i = 0; i < total; i += 2) {
        const float l = interleavedSamples[i];
        const float r = interleavedSamples[i + 1];

        const float crossFromL = static_cast<float>(lpL_.processSample(l));
        const float crossFromR = static_cast<float>(lpR_.processSample(r));

        delayL_[writeIdx_] = crossFromL;
        delayR_[writeIdx_] = crossFromR;

        const size_t readIdx = (writeIdx_ + bufSize - itdSamples_) % bufSize;
        const float delayedL = delayL_[readIdx];
        const float delayedR = delayR_[readIdx];

        writeIdx_ = (writeIdx_ + 1) % bufSize;

        interleavedSamples[i] = static_cast<float>(l * directGain_ + delayedR * crossfeedGain_);
        interleavedSamples[i + 1] = static_cast<float>(r * directGain_ + delayedL * crossfeedGain_);
    }
}

} // namespace viper
