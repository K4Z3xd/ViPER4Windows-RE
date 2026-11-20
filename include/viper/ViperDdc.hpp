#pragma once

#include <cstdint>
#include <span>
#include <string>
#include <vector>

namespace viper {

// Hi-Res ViPER-DDC (.vdc) Headphone/IEM Frequency Response Correction Engine.
// - Parses official ViPER4Android / AutoEQ .vdc files (SR_44100 / SR_48000 SOS biquads)
// - Automatically transforms biquad sections via exact bilinear frequency mapping
//   so .vdc profiles stay 100% accurate at 96kHz, 192kHz, and 384kHz!
class ViperDdc {
public:
    void prepare(double sampleRate);
    void reset() noexcept;

    void configure(uint32_t ddcRevision);
    bool loadActiveVdcFromDisk();

    void processInterleavedStereo(std::span<float> interleavedSamples) noexcept;

private:
    struct SosSection {
        double b0 = 1.0, b1 = 0.0, b2 = 0.0;
        double a1 = 0.0, a2 = 0.0;
        double z1L = 0.0, z2L = 0.0;
        double z1R = 0.0, z2R = 0.0;
    };

    static SosSection transformSampleRate(const SosSection& src, double srcRate, double dstRate) noexcept;
    bool parseVdcFile(const std::wstring& path);

    double sampleRate_ = 48000.0;
    uint32_t loadedRevision_ = 0;
    std::vector<SosSection> sections_{};
};

} // namespace viper
