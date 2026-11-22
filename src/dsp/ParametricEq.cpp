#include "viper/ParametricEq.hpp"
#include <algorithm>
#include <cmath>
#include <numbers>

namespace viper {

void ParametricEq::fft(std::array<Complex, kFftSize>& a, bool inverse) noexcept {
    constexpr size_t n = kFftSize;
    for (size_t i = 1, j = 0; i < n; ++i) {
        size_t bit = n >> 1;
        for (; j & bit; bit >>= 1) {
            j ^= bit;
        }
        j ^= bit;
        if (i < j) {
            std::swap(a[i], a[j]);
        }
    }

    for (size_t len = 2; len <= n; len <<= 1) {
        const float ang = static_cast<float>((inverse ? 2.0 : -2.0) * std::numbers::pi / static_cast<double>(len));
        const Complex wlen(std::cos(ang), std::sin(ang));
        for (size_t i = 0; i < n; i += len) {
            Complex w(1.0f, 0.0f);
            const size_t half = len >> 1;
            for (size_t j = 0; j < half; ++j) {
                const Complex u = a[i + j];
                const Complex v = a[i + j + half] * w;
                a[i + j] = u + v;
                a[i + j + half] = u - v;
                w *= wlen;
            }
        }
    }

    if (inverse) {
        constexpr float invN = 1.0f / static_cast<float>(n);
        for (auto& x : a) {
            x *= invN;
        }
    }
}

void ParametricEq::prepare(double sampleRate) noexcept {
    sampleRate_ = std::max(sampleRate, 8000.0);
    reset();

    constexpr double kQ10 = 1.41421356237; // 1-octave bandwidth
    for (size_t i = 0; i < kEqBandCount; ++i) {
        const double g = std::clamp(static_cast<double>(lastGains10Db_[i]), -12.0, 12.0);
        filters10L_[i].configure(BiquadType::PeakingEQ, sampleRate_, kEqFrequencies[i], kQ10, g);
        filters10R_[i].configure(BiquadType::PeakingEQ, sampleRate_, kEqFrequencies[i], kQ10, g);
    }

    constexpr double kQ18 = 2.6; // ~1/2-octave bandwidth for 18-band precision
    for (size_t i = 0; i < kEqBand18Count; ++i) {
        const double g = std::clamp(static_cast<double>(lastGains18Db_[i]), -12.0, 12.0);
        filters18L_[i].configure(BiquadType::PeakingEQ, sampleRate_, kEq18Frequencies[i], kQ18, g);
        filters18R_[i].configure(BiquadType::PeakingEQ, sampleRate_, kEq18Frequencies[i], kQ18, g);
    }

    rebuildFirKernel();
}

void ParametricEq::reset() noexcept {
    for (size_t i = 0; i < kEqBandCount; ++i) {
        filters10L_[i].reset();
        filters10R_[i].reset();
    }
    for (size_t i = 0; i < kEqBand18Count; ++i) {
        filters18L_[i].reset();
        filters18R_[i].reset();
    }
    inBufL_.fill(0.0f);
    inBufR_.fill(0.0f);
    outBufL_.fill(0.0f);
    outBufR_.fill(0.0f);
    overlapL_.fill(0.0f);
    overlapR_.fill(0.0f);
    firPos_ = 0;
}

void ParametricEq::rebuildFirKernel() noexcept {
    const bool is18 = (eqMode_ & 1u) != 0u;
    const size_t bandCount = is18 ? kEqBand18Count : kEqBandCount;
    const double* freqs = is18 ? kEq18Frequencies.data() : kEqFrequencies.data();
    const float* gains = is18 ? lastGains18Db_.data() : lastGains10Db_.data();

    bool allFlat = true;
    for (size_t b = 0; b < bandCount; ++b) {
        if (std::abs(gains[b]) > 1e-3f) {
            allFlat = false;
            break;
        }
    }
    firFlat_ = allFlat;

    // Helper: smooth log-frequency cosine interpolation across ISO band centers
    auto evalGainDb = [&](double hz) noexcept -> double {
        if (hz <= freqs[0]) return static_cast<double>(gains[0]);
        const double lastF = freqs[bandCount - 1];
        if (hz >= lastF) {
            // Smoothly taper above 16 kHz toward 0 dB near Nyquist to avoid high-freq ringing
            const double nyq = sampleRate_ * 0.49;
            if (nyq <= lastF + 500.0) return static_cast<double>(gains[bandCount - 1]);
            const double t = std::clamp((hz - lastF) / (nyq - lastF), 0.0, 1.0);
            const double w = 0.5 * (1.0 + std::cos(std::numbers::pi * t));
            return static_cast<double>(gains[bandCount - 1]) * w;
        }
        for (size_t b = 0; b + 1 < bandCount; ++b) {
            if (hz <= freqs[b + 1]) {
                const double logF = std::log2(hz);
                const double log0 = std::log2(freqs[b]);
                const double log1 = std::log2(freqs[b + 1]);
                const double t = (logF - log0) / (log1 - log0);
                const double smoothT = 0.5 * (1.0 - std::cos(std::numbers::pi * t));
                return static_cast<double>(gains[b]) * (1.0 - smoothT) +
                       static_cast<double>(gains[b + 1]) * smoothT;
            }
        }
        return 0.0;
    };

    // 1. Construct zero-phase Hermitian-symmetric target magnitude spectrum
    constexpr size_t halfFft = kFftSize / 2;
    for (size_t k = 0; k <= halfFft; ++k) {
        const double hz = (static_cast<double>(k) * sampleRate_) / static_cast<double>(kFftSize);
        const double db = (k == 0) ? 0.0 : evalGainDb(hz); // Preserve clean DC unity
        const float mag = static_cast<float>(std::pow(10.0, db / 20.0));
        scratchL_[k] = Complex{mag, 0.0f};
        if (k > 0 && k < halfFft) {
            scratchL_[kFftSize - k] = Complex{mag, 0.0f};
        }
    }

    // 2. IFFT -> symmetric time-domain impulse -> cyclic center + Blackman window
    fft(scratchL_, true);

    constexpr size_t halfTap = kFirTaps / 2;
    constexpr double invNm1 = 1.0 / static_cast<double>(kFirTaps - 1);
    firSpec_.fill(Complex{0.0f, 0.0f});

    for (size_t i = 0; i < kFirTaps; ++i) {
        const size_t srcIdx = (i + kFftSize - halfTap) % kFftSize;
        const double phase = 2.0 * std::numbers::pi * static_cast<double>(i) * invNm1;
        const double win = 0.42 - 0.5 * std::cos(phase) + 0.08 * std::cos(2.0 * phase);
        firSpec_[i] = Complex{static_cast<float>(scratchL_[srcIdx].real() * win), 0.0f};
    }

    // 3. Forward FFT of windowed linear-phase FIR kernel (zero-padded to kFftSize)
    fft(firSpec_, false);
}

void ParametricEq::updateGains(uint32_t eqMode,
                               const std::array<float, kEqBandCount>& bands10Db,
                               const std::array<float, kEqBand18Count>& bands18Db) noexcept {
    const uint32_t normalizedMode = eqMode & 3u;
    const bool prevFir = (eqMode_ & 2u) != 0u;
    const bool newFir = (normalizedMode & 2u) != 0u;
    bool changed = (normalizedMode != eqMode_);
    eqMode_ = normalizedMode;

    if (prevFir != newFir) {
        inBufL_.fill(0.0f);
        inBufR_.fill(0.0f);
        outBufL_.fill(0.0f);
        outBufR_.fill(0.0f);
        overlapL_.fill(0.0f);
        overlapR_.fill(0.0f);
        firPos_ = 0;
    }

    constexpr double kQ10 = 1.41421356237;
    for (size_t i = 0; i < kEqBandCount; ++i) {
        const float clamped = std::clamp(bands10Db[i], -12.0f, 12.0f);
        if (std::abs(clamped - lastGains10Db_[i]) > 1e-4f) {
            lastGains10Db_[i] = clamped;
            filters10L_[i].configure(BiquadType::PeakingEQ, sampleRate_, kEqFrequencies[i], kQ10, clamped);
            filters10R_[i].configure(BiquadType::PeakingEQ, sampleRate_, kEqFrequencies[i], kQ10, clamped);
            changed = true;
        }
    }

    constexpr double kQ18 = 2.6;
    for (size_t i = 0; i < kEqBand18Count; ++i) {
        const float clamped = std::clamp(bands18Db[i], -12.0f, 12.0f);
        if (std::abs(clamped - lastGains18Db_[i]) > 1e-4f) {
            lastGains18Db_[i] = clamped;
            filters18L_[i].configure(BiquadType::PeakingEQ, sampleRate_, kEq18Frequencies[i], kQ18, clamped);
            filters18R_[i].configure(BiquadType::PeakingEQ, sampleRate_, kEq18Frequencies[i], kQ18, clamped);
            changed = true;
        }
    }

    if (changed) {
        rebuildFirKernel();
    }
}

void ParametricEq::processFirBlock() noexcept {
    for (size_t i = 0; i < kBlockSize; ++i) {
        scratchL_[i] = Complex{inBufL_[i], 0.0f};
        scratchR_[i] = Complex{inBufR_[i], 0.0f};
    }
    for (size_t i = kBlockSize; i < kFftSize; ++i) {
        scratchL_[i] = Complex{0.0f, 0.0f};
        scratchR_[i] = Complex{0.0f, 0.0f};
    }

    fft(scratchL_, false);
    fft(scratchR_, false);

    for (size_t k = 0; k < kFftSize; ++k) {
        scratchL_[k] *= firSpec_[k];
        scratchR_[k] *= firSpec_[k];
    }

    fft(scratchL_, true);
    fft(scratchR_, true);

    for (size_t i = 0; i < kBlockSize; ++i) {
        outBufL_[i] = scratchL_[i].real() + overlapL_[i];
        outBufR_[i] = scratchR_[i].real() + overlapR_[i];
        overlapL_[i] = scratchL_[i + kBlockSize].real();
        overlapR_[i] = scratchR_[i + kBlockSize].real();
    }
}

void ParametricEq::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    const size_t total = interleavedSamples.size() & ~size_t{1};
    const bool isFir = (eqMode_ & 2u) != 0u;
    const bool is18 = (eqMode_ & 1u) != 0u;

    if (isFir) {
        if (firFlat_) {
            return;
        }
        for (size_t i = 0; i < total; i += 2) {
            inBufL_[firPos_] = interleavedSamples[i];
            inBufR_[firPos_] = interleavedSamples[i + 1];

            interleavedSamples[i] = outBufL_[firPos_];
            interleavedSamples[i + 1] = outBufR_[firPos_];

            if (++firPos_ >= kBlockSize) {
                processFirBlock();
                firPos_ = 0;
            }
        }
        return;
    }

    if (!is18) {
        for (size_t i = 0; i < total; i += 2) {
            double l = static_cast<double>(interleavedSamples[i]);
            double r = static_cast<double>(interleavedSamples[i + 1]);
            for (size_t b = 0; b < kEqBandCount; ++b) {
                if (std::abs(lastGains10Db_[b]) > 1e-4f) {
                    l = filters10L_[b].processSample(l);
                    r = filters10R_[b].processSample(r);
                }
            }
            interleavedSamples[i] = static_cast<float>(l);
            interleavedSamples[i + 1] = static_cast<float>(r);
        }
    } else {
        for (size_t i = 0; i < total; i += 2) {
            double l = static_cast<double>(interleavedSamples[i]);
            double r = static_cast<double>(interleavedSamples[i + 1]);
            for (size_t b = 0; b < kEqBand18Count; ++b) {
                if (std::abs(lastGains18Db_[b]) > 1e-4f) {
                    l = filters18L_[b].processSample(l);
                    r = filters18R_[b].processSample(r);
                }
            }
            interleavedSamples[i] = static_cast<float>(l);
            interleavedSamples[i + 1] = static_cast<float>(r);
        }
    }
}

} // namespace viper
