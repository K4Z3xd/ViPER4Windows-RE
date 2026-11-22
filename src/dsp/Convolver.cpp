#include "viper/Convolver.hpp"

#include <algorithm>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <fstream>
#include <numbers>

namespace viper {

namespace {

uint16_t readU16(const uint8_t* p) noexcept {
    return static_cast<uint16_t>(p[0] | (p[1] << 8));
}

uint32_t readU32(const uint8_t* p) noexcept {
    return static_cast<uint32_t>(p[0]) |
           (static_cast<uint32_t>(p[1]) << 8) |
           (static_cast<uint32_t>(p[2]) << 16) |
           (static_cast<uint32_t>(p[3]) << 24);
}

// High-quality cubic Hermite resampler for matching IR sample rate to DAC sample rate (up to 384kHz)
std::vector<float> resampleMono(const std::vector<float>& in, double srcRate, double dstRate) {
    if (in.empty() || srcRate <= 0.0 || dstRate <= 0.0) return {};
    if (std::abs(srcRate - dstRate) < 1.0) return in;

    const double ratio = dstRate / srcRate;
    const size_t outLen = std::max<size_t>(1, static_cast<size_t>(std::lround(in.size() * ratio)));
    std::vector<float> out(outLen);

    const double invRatio = srcRate / dstRate;
    const double gainComp = std::min(1.0, srcRate / dstRate); // Preserve energy on upsampling

    for (size_t i = 0; i < outLen; ++i) {
        const double pos = static_cast<double>(i) * invRatio;
        const size_t idx = static_cast<size_t>(pos);
        const float t = static_cast<float>(pos - static_cast<double>(idx));

        auto sampleAt = [&](int64_t k) -> float {
            if (k < 0) return in.front();
            if (static_cast<size_t>(k) >= in.size()) return in.back();
            return in[static_cast<size_t>(k)];
        };

        const float y0 = sampleAt(static_cast<int64_t>(idx) - 1);
        const float y1 = sampleAt(static_cast<int64_t>(idx));
        const float y2 = sampleAt(static_cast<int64_t>(idx) + 1);
        const float y3 = sampleAt(static_cast<int64_t>(idx) + 2);

        const float c0 = y1;
        const float c1 = 0.5f * (y2 - y0);
        const float c2 = y0 - 2.5f * y1 + 2.0f * y2 - 0.5f * y3;
        const float c3 = 0.5f * (y3 - y0) + 1.5f * (y1 - y2);

        out[i] = (((c3 * t + c2) * t + c1) * t + c0) * static_cast<float>(gainComp);
    }

    return out;
}

} // namespace

void Convolver::fft(std::vector<Complex>& a, bool inverse) noexcept {
    const size_t n = a.size();
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
        const float invN = 1.0f / static_cast<float>(n);
        for (auto& x : a) {
            x *= invN;
        }
    }
}

void Convolver::prepare(double sampleRate) {
    sampleRate_ = std::max(sampleRate, 8000.0);

    inAccumL_.assign(kBlockSize, 0.0f);
    inAccumR_.assign(kBlockSize, 0.0f);
    outReadyL_.assign(kBlockSize, 0.0f);
    outReadyR_.assign(kBlockSize, 0.0f);
    overlapL_.assign(kBlockSize, 0.0f);
    overlapR_.assign(kBlockSize, 0.0f);

    fftScratchL_.assign(kFftSize, Complex{0.0f, 0.0f});
    fftScratchR_.assign(kFftSize, Complex{0.0f, 0.0f});
    accumSpecL_.assign(kFftSize, Complex{0.0f, 0.0f});
    accumSpecR_.assign(kFftSize, Complex{0.0f, 0.0f});

    fdlL_.assign(kMaxPartitions, std::vector<Complex>(kFftSize, Complex{0.0f, 0.0f}));
    fdlR_.assign(kMaxPartitions, std::vector<Complex>(kFftSize, Complex{0.0f, 0.0f}));
    irSpecL_.assign(kMaxPartitions, std::vector<Complex>(kFftSize, Complex{0.0f, 0.0f}));
    irSpecR_.assign(kMaxPartitions, std::vector<Complex>(kFftSize, Complex{0.0f, 0.0f}));
    irSpecLR_.assign(kMaxPartitions, std::vector<Complex>(kFftSize, Complex{0.0f, 0.0f}));
    irSpecRL_.assign(kMaxPartitions, std::vector<Complex>(kFftSize, Complex{0.0f, 0.0f}));

    samplePos_ = 0;
    fdlIndex_ = 0;
    loadedRevision_ = 0;
    irLoaded_ = false;
    trueStereo_ = false;
    (void)loadActiveIrFromDisk();
}

void Convolver::reset() noexcept {
    std::fill(inAccumL_.begin(), inAccumL_.end(), 0.0f);
    std::fill(inAccumR_.begin(), inAccumR_.end(), 0.0f);
    std::fill(outReadyL_.begin(), outReadyL_.end(), 0.0f);
    std::fill(outReadyR_.begin(), outReadyR_.end(), 0.0f);
    std::fill(overlapL_.begin(), overlapL_.end(), 0.0f);
    std::fill(overlapR_.begin(), overlapR_.end(), 0.0f);
    for (auto& v : fdlL_) std::fill(v.begin(), v.end(), Complex{0.0f, 0.0f});
    for (auto& v : fdlR_) std::fill(v.begin(), v.end(), Complex{0.0f, 0.0f});
    samplePos_ = 0;
    fdlIndex_ = 0;
}

void Convolver::configure(float crossChannel, float wetMix, uint32_t irRevision) {
    crossChannel_ = std::clamp(crossChannel, 0.0f, 1.0f);
    wetMix_ = std::clamp(wetMix, 0.0f, 1.0f);

    if (irRevision != loadedRevision_) {
        loadedRevision_ = irRevision;
        (void)loadActiveIrFromDisk();
    }
}

bool Convolver::loadActiveIrFromDisk() {
    std::ifstream meta("C:\\ProgramData\\ViPERHiRes\\active_ir.txt");
    if (!meta) {
        irLoaded_ = false;
        return false;
    }

    std::string utf8Path;
    std::getline(meta, utf8Path);
    while (!utf8Path.empty() && (utf8Path.back() == '\r' || utf8Path.back() == '\n')) {
        utf8Path.pop_back();
    }
    if (utf8Path.empty()) {
        irLoaded_ = false;
        return false;
    }

    std::wstring widePath(utf8Path.begin(), utf8Path.end());
    return loadWavFile(widePath);
}

bool Convolver::loadWavFile(const std::wstring& path) {
    FILE* f = nullptr;
    if (_wfopen_s(&f, path.c_str(), L"rb") != 0 || !f) {
        irLoaded_ = false;
        return false;
    }

    std::fseek(f, 0, SEEK_END);
    const long fileLen = std::ftell(f);
    std::fseek(f, 0, SEEK_SET);
    if (fileLen < 44 || fileLen > 32 * 1024 * 1024) {
        std::fclose(f);
        irLoaded_ = false;
        return false;
    }

    std::vector<uint8_t> raw(static_cast<size_t>(fileLen));
    const size_t readBytes = std::fread(raw.data(), 1, raw.size(), f);
    std::fclose(f);

    if (readBytes < 44 || std::memcmp(raw.data(), "RIFF", 4) != 0 || std::memcmp(raw.data() + 8, "WAVE", 4) != 0) {
        irLoaded_ = false;
        return false;
    }

    uint16_t audioFormat = 0;
    uint16_t numChannels = 0;
    uint32_t wavSampleRate = 0;
    uint16_t bitsPerSample = 0;
    const uint8_t* dataPtr = nullptr;
    uint32_t dataSize = 0;

    size_t offset = 12;
    while (offset + 8 <= raw.size()) {
        const uint8_t* chunk = raw.data() + offset;
        const uint32_t chunkSize = readU32(chunk + 4);
        if (offset + 8 + chunkSize > raw.size()) break;

        if (std::memcmp(chunk, "fmt ", 4) == 0 && chunkSize >= 16) {
            audioFormat = readU16(chunk + 8);
            numChannels = readU16(chunk + 10);
            wavSampleRate = readU32(chunk + 12);
            bitsPerSample = readU16(chunk + 22);
            // Support WAVE_FORMAT_EXTENSIBLE (0xFFFE) by inspecting SubFormat GUID first 2 bytes
            if (audioFormat == 0xFFFE && chunkSize >= 40) {
                audioFormat = readU16(chunk + 32);
            }
        } else if (std::memcmp(chunk, "data", 4) == 0) {
            dataPtr = chunk + 8;
            dataSize = chunkSize;
        }
        offset += 8 + chunkSize + (chunkSize & 1u);
    }

    const bool validCh = (numChannels == 1 || numChannels == 2 || numChannels == 4);
    if (!dataPtr || dataSize == 0 || !validCh || wavSampleRate < 8000) {
        irLoaded_ = false;
        return false;
    }

    const size_t bytesPerSample = bitsPerSample / 8;
    if (bytesPerSample == 0) {
        irLoaded_ = false;
        return false;
    }
    const size_t totalFrames = dataSize / (bytesPerSample * numChannels);
    if (totalFrames == 0) {
        irLoaded_ = false;
        return false;
    }

    const bool is4Ch = (numChannels == 4);
    std::vector<float> irLL(totalFrames);
    std::vector<float> irRR(totalFrames);
    std::vector<float> irLR;
    std::vector<float> irRL;
    if (is4Ch) {
        irLR.resize(totalFrames);
        irRL.resize(totalFrames);
    }

    auto decodeSample = [&](const uint8_t* p) -> float {
        if (audioFormat == 3 && bitsPerSample == 32) {
            float v = 0.0f;
            std::memcpy(&v, p, sizeof(float));
            return std::isfinite(v) ? v : 0.0f;
        }
        if (audioFormat == 1 && bitsPerSample == 16) {
            const int16_t s = static_cast<int16_t>(p[0] | (p[1] << 8));
            return static_cast<float>(s) / 32768.0f;
        }
        if (audioFormat == 1 && bitsPerSample == 24) {
            int32_t s = (static_cast<int32_t>(p[0]) << 8) |
                        (static_cast<int32_t>(p[1]) << 16) |
                        (static_cast<int32_t>(p[2]) << 24);
            return static_cast<float>(s >> 8) / 8388608.0f;
        }
        if (audioFormat == 1 && bitsPerSample == 32) {
            int32_t s = static_cast<int32_t>(readU32(p));
            return static_cast<float>(s) / 2147483648.0f;
        }
        return 0.0f;
    };

    for (size_t i = 0; i < totalFrames; ++i) {
        const uint8_t* framePtr = dataPtr + i * bytesPerSample * numChannels;
        if (is4Ch) {
            // Standard True Stereo 4ch layout: [LL, LR, RL, RR]
            irLL[i] = decodeSample(framePtr + 0 * bytesPerSample);
            irLR[i] = decodeSample(framePtr + 1 * bytesPerSample);
            irRL[i] = decodeSample(framePtr + 2 * bytesPerSample);
            irRR[i] = decodeSample(framePtr + 3 * bytesPerSample);
        } else {
            irLL[i] = decodeSample(framePtr);
            irRR[i] = (numChannels == 2) ? decodeSample(framePtr + bytesPerSample) : irLL[i];
        }
    }

    // Resample IR automatically to current DAC sample rate (e.g., 48k -> 192k/384k)
    irLL = resampleMono(irLL, static_cast<double>(wavSampleRate), sampleRate_);
    irRR = resampleMono(irRR, static_cast<double>(wavSampleRate), sampleRate_);
    if (is4Ch) {
        irLR = resampleMono(irLR, static_cast<double>(wavSampleRate), sampleRate_);
        irRL = resampleMono(irRL, static_cast<double>(wavSampleRate), sampleRate_);
    }

    // Trim to max supported partitions
    const size_t maxSamples = kMaxPartitions * kBlockSize;
    if (irLL.size() > maxSamples) irLL.resize(maxSamples);
    if (irRR.size() > maxSamples) irRR.resize(maxSamples);
    if (is4Ch) {
        if (irLR.size() > maxSamples) irLR.resize(maxSamples);
        if (irRL.size() > maxSamples) irRL.resize(maxSamples);
    }

    // Normalize IR energy safely so loudness stays consistent across different .irs files
    double energyL = 0.0, energyR = 0.0;
    for (float s : irLL) energyL += static_cast<double>(s) * static_cast<double>(s);
    for (float s : irRR) energyR += static_cast<double>(s) * static_cast<double>(s);
    if (is4Ch) {
        for (float s : irRL) energyL += static_cast<double>(s) * static_cast<double>(s);
        for (float s : irLR) energyR += static_cast<double>(s) * static_cast<double>(s);
    }
    const double norm = std::sqrt(std::max({energyL, energyR, 1e-6}));
    if (norm > 1.0) {
        const float invNorm = static_cast<float>(1.0 / norm);
        for (float& s : irLL) s *= invNorm;
        for (float& s : irRR) s *= invNorm;
        if (is4Ch) {
            for (float& s : irLR) s *= invNorm;
            for (float& s : irRL) s *= invNorm;
        }
    }

    numPartitions_ = std::min(kMaxPartitions, (irLL.size() + kBlockSize - 1) / kBlockSize);
    for (size_t p = 0; p < numPartitions_; ++p) {
        std::fill(irSpecL_[p].begin(), irSpecL_[p].end(), Complex{0.0f, 0.0f});
        std::fill(irSpecR_[p].begin(), irSpecR_[p].end(), Complex{0.0f, 0.0f});
        const size_t start = p * kBlockSize;
        const size_t count = std::min(kBlockSize, irLL.size() - start);
        for (size_t k = 0; k < count; ++k) {
            irSpecL_[p][k] = Complex{irLL[start + k], 0.0f};
            irSpecR_[p][k] = Complex{irRR[start + k], 0.0f};
        }
        fft(irSpecL_[p], false);
        fft(irSpecR_[p], false);

        if (is4Ch) {
            std::fill(irSpecLR_[p].begin(), irSpecLR_[p].end(), Complex{0.0f, 0.0f});
            std::fill(irSpecRL_[p].begin(), irSpecRL_[p].end(), Complex{0.0f, 0.0f});
            for (size_t k = 0; k < count; ++k) {
                irSpecLR_[p][k] = Complex{irLR[start + k], 0.0f};
                irSpecRL_[p][k] = Complex{irRL[start + k], 0.0f};
            }
            fft(irSpecLR_[p], false);
            fft(irSpecRL_[p], false);
        }
    }

    reset();
    trueStereo_ = is4Ch;
    irLoaded_ = (numPartitions_ > 0);
    return irLoaded_;
}

void Convolver::processBlock() noexcept {
    // 1. FFT of new input block (zero-padded to kFftSize)
    for (size_t i = 0; i < kBlockSize; ++i) {
        fdlL_[fdlIndex_][i] = Complex{inAccumL_[i], 0.0f};
        fdlR_[fdlIndex_][i] = Complex{inAccumR_[i], 0.0f};
    }
    for (size_t i = kBlockSize; i < kFftSize; ++i) {
        fdlL_[fdlIndex_][i] = Complex{0.0f, 0.0f};
        fdlR_[fdlIndex_][i] = Complex{0.0f, 0.0f};
    }
    fft(fdlL_[fdlIndex_], false);
    fft(fdlR_[fdlIndex_], false);

    // 2. Frequency-domain multiply-accumulate across all IR partitions
    std::fill(accumSpecL_.begin(), accumSpecL_.end(), Complex{0.0f, 0.0f});
    std::fill(accumSpecR_.begin(), accumSpecR_.end(), Complex{0.0f, 0.0f});

    if (trueStereo_) {
        for (size_t p = 0; p < numPartitions_; ++p) {
            const size_t histIdx = (fdlIndex_ + kMaxPartitions - p) % kMaxPartitions;
            const auto& xL = fdlL_[histIdx];
            const auto& xR = fdlR_[histIdx];
            const auto& hLL = irSpecL_[p];
            const auto& hRR = irSpecR_[p];
            const auto& hLR = irSpecLR_[p];
            const auto& hRL = irSpecRL_[p];

            for (size_t k = 0; k < kFftSize; ++k) {
                accumSpecL_[k] += xL[k] * hLL[k] + xR[k] * hRL[k];
                accumSpecR_[k] += xL[k] * hLR[k] + xR[k] * hRR[k];
            }
        }
    } else {
        for (size_t p = 0; p < numPartitions_; ++p) {
            const size_t histIdx = (fdlIndex_ + kMaxPartitions - p) % kMaxPartitions;
            const auto& xL = fdlL_[histIdx];
            const auto& xR = fdlR_[histIdx];
            const auto& hL = irSpecL_[p];
            const auto& hR = irSpecR_[p];

            for (size_t k = 0; k < kFftSize; ++k) {
                accumSpecL_[k] += xL[k] * hL[k];
                accumSpecR_[k] += xR[k] * hR[k];
            }
        }
    }

    // 3. Inverse FFT + Overlap-Add
    fft(accumSpecL_, true);
    fft(accumSpecR_, true);

    for (size_t i = 0; i < kBlockSize; ++i) {
        outReadyL_[i] = accumSpecL_[i].real() + overlapL_[i];
        outReadyR_[i] = accumSpecR_[i].real() + overlapR_[i];
        overlapL_[i] = accumSpecL_[i + kBlockSize].real();
        overlapR_[i] = accumSpecR_[i + kBlockSize].real();
    }

    fdlIndex_ = (fdlIndex_ + 1) % kMaxPartitions;
}

void Convolver::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    if (!irLoaded_ || wetMix_ <= 0.001f) {
        return;
    }

    const float dryMix = 1.0f - wetMix_;
    const float cross = crossChannel_ * 0.5f;
    const float direct = 1.0f - cross;
    const size_t total = interleavedSamples.size() & ~size_t{1};

    for (size_t i = 0; i < total; i += 2) {
        const float dryL = interleavedSamples[i];
        const float dryR = interleavedSamples[i + 1];

        inAccumL_[samplePos_] = dryL;
        inAccumR_[samplePos_] = dryR;

        const float convL = outReadyL_[samplePos_];
        const float convR = outReadyR_[samplePos_];

        // Apply ViPER Cross-Channel blend on convolved output
        const float wetL = convL * direct + convR * cross;
        const float wetR = convR * direct + convL * cross;

        interleavedSamples[i] = dryL * dryMix + wetL * wetMix_;
        interleavedSamples[i + 1] = dryR * dryMix + wetR * wetMix_;

        if (++samplePos_ >= kBlockSize) {
            processBlock();
            samplePos_ = 0;
        }
    }
}

} // namespace viper
