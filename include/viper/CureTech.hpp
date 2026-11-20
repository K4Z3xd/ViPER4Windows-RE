#pragma once

#include "viper/BiquadFilter.hpp"
#include <cstdint>
#include <vector>
#include <span>

namespace viper {

enum class CureLevel : uint32_t {
    Slight = 0,
    Moderate = 1,
    Extreme = 2
};

// ViPER Cure Tech+ (Binaural Headphone Crossfeed)
// Reduces listening fatigue on headphones/IEMs by blending low-pass filtered,
// subtly time-delayed (~0.25ms ITD) contralateral signal like natural speakers.
class CureTech {
public:
    void prepare(double sampleRate);
    void reset() noexcept;
    void configure(CureLevel level) noexcept;

    void processInterleavedStereo(std::span<float> interleavedSamples) noexcept;

private:
    double sampleRate_ = 48000.0;
    double crossfeedGain_ = 0.22;
    double directGain_ = 0.88;
    size_t itdSamples_ = 12;

    BiquadFilter lpL_{};
    BiquadFilter lpR_{};
    std::vector<float> delayL_{};
    std::vector<float> delayR_{};
    size_t writeIdx_ = 0;
};

} // namespace viper
