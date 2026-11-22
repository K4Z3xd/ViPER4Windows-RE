#pragma once

#include "viper/BiquadFilter.hpp"
#include "viper/DspTypes.hpp"
#include <array>
#include <complex>
#include <span>

namespace viper {

// Hi-Res 10-Band / 18-Band Equalizer supporting two selectable topologies:
// - IIR Minimum-Phase (Zero-Latency): 64-bit Biquad Peaking EQ cascade
// - FIR Linear-Phase: 2048-tap symmetric frequency-sampled FIR via 4096-pt FFT Overlap-Add
//
// eqMode bit encoding (preserves 344-byte DspParams v5 ABI):
//   bit 0 (0x01): 0 = 10-Band, 1 = 18-Band
//   bit 1 (0x02): 0 = IIR (Zero-Latency), 1 = FIR (Linear-Phase)
class ParametricEq {
public:
    void prepare(double sampleRate) noexcept;
    void reset() noexcept;
    void updateGains(uint32_t eqMode,
                     const std::array<float, kEqBandCount>& bands10Db,
                     const std::array<float, kEqBand18Count>& bands18Db) noexcept;

    // Interleaved stereo processing (L, R, L, R, ...)
    void processInterleavedStereo(std::span<float> interleavedSamples) noexcept;

private:
    static constexpr size_t kFirTaps = 2048;
    static constexpr size_t kBlockSize = 2048;
    static constexpr size_t kFftSize = 4096; // kBlockSize + kFirTaps

    using Complex = std::complex<float>;

    static void fft(std::array<Complex, kFftSize>& a, bool inverse) noexcept;
    void rebuildFirKernel() noexcept;
    void processFirBlock() noexcept;

    double sampleRate_ = 48000.0;
    uint32_t eqMode_ = 0; // bit 0: 10/18-band, bit 1: IIR/FIR
    bool firFlat_ = true;

    // IIR Minimum-Phase state (Zero-Latency)
    std::array<float, kEqBandCount> lastGains10Db_{};
    std::array<BiquadFilter, kEqBandCount> filters10L_{};
    std::array<BiquadFilter, kEqBandCount> filters10R_{};

    std::array<float, kEqBand18Count> lastGains18Db_{};
    std::array<BiquadFilter, kEqBand18Count> filters18L_{};
    std::array<BiquadFilter, kEqBand18Count> filters18R_{};

    // FIR Linear-Phase state (pre-allocated inline, zero RT heap allocations)
    std::array<Complex, kFftSize> firSpec_{};
    std::array<Complex, kFftSize> scratchL_{};
    std::array<Complex, kFftSize> scratchR_{};
    std::array<float, kBlockSize> inBufL_{};
    std::array<float, kBlockSize> inBufR_{};
    std::array<float, kBlockSize> outBufL_{};
    std::array<float, kBlockSize> outBufR_{};
    std::array<float, kBlockSize> overlapL_{};
    std::array<float, kBlockSize> overlapR_{};
    size_t firPos_ = 0;
};

} // namespace viper
