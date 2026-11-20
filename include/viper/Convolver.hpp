#pragma once

#include <complex>
#include <cstdint>
#include <span>
#include <string>
#include <vector>

namespace viper {

// Hi-Res Uniform Partitioned FFT Convolver for ViPER .irs / .wav impulse responses.
// - Supports 16-bit PCM, 24-bit PCM, 32-bit PCM, and 32-bit IEEE Float WAV/IRS files (mono or stereo)
// - Automatically resamples the impulse response to match the active DAC sample rate (44.1kHz - 384kHz)
// - Supports ViPER Cross-Channel blend and Wet/Dry mix
class Convolver {
public:
    void prepare(double sampleRate);
    void reset() noexcept;

    void configure(float crossChannel, float wetMix, uint32_t irRevision);

    // Reloads active IR file from C:\ProgramData\ViPERHiRes\active_ir.txt
    bool loadActiveIrFromDisk();

    void processInterleavedStereo(std::span<float> interleavedSamples) noexcept;

private:
    static constexpr size_t kBlockSize = 512;
    static constexpr size_t kFftSize = kBlockSize * 2; // 1024-point FFT
    static constexpr size_t kMaxPartitions = 64;       // Up to 32,768 samples per channel

    using Complex = std::complex<float>;

    static void fft(std::vector<Complex>& a, bool inverse) noexcept;
    void processBlock() noexcept;
    bool loadWavFile(const std::wstring& path);

    double sampleRate_ = 48000.0;
    float crossChannel_ = 0.0f;
    float wetMix_ = 1.0f;
    uint32_t loadedRevision_ = 0;
    bool irLoaded_ = false;

    // Partitioned IR spectra: [partition][bin]
    size_t numPartitions_ = 0;
    std::vector<std::vector<Complex>> irSpecL_{};
    std::vector<std::vector<Complex>> irSpecR_{};

    // Input history spectra ring buffer (FDL)
    std::vector<std::vector<Complex>> fdlL_{};
    std::vector<std::vector<Complex>> fdlR_{};
    size_t fdlIndex_ = 0;

    // Time-domain framing & overlap-add buffers
    std::vector<float> inAccumL_{};
    std::vector<float> inAccumR_{};
    std::vector<float> outReadyL_{};
    std::vector<float> outReadyR_{};
    std::vector<float> overlapL_{};
    std::vector<float> overlapR_{};
    size_t samplePos_ = 0;

    // Scratch FFT buffers (pre-allocated in prepare() -> zero RT allocation)
    std::vector<Complex> fftScratchL_{};
    std::vector<Complex> fftScratchR_{};
    std::vector<Complex> accumSpecL_{};
    std::vector<Complex> accumSpecR_{};
};

} // namespace viper
