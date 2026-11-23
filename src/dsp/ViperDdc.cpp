#include "viper/ViperDdc.hpp"

#include <algorithm>
#include <cmath>
#include <fstream>
#include <sstream>
#ifdef _WIN32
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#endif

namespace viper {

ViperDdc::SosSection ViperDdc::transformSampleRate(const SosSection& src, double srcRate, double dstRate) noexcept {
    if (std::abs(srcRate - dstRate) < 1.0) {
        return src;
    }

    // Exact bilinear all-pass frequency mapping: z1^-1 = (z2^-1 - alpha) / (1 - alpha * z2^-1)
    const double r = dstRate / srcRate;
    const double alpha = (r - 1.0) / (r + 1.0);
    const double a2 = alpha * alpha;

    // Numerator: b0 + b1*z1^-1 + b2*z1^-2
    const double nb0 = src.b0 - src.b1 * alpha + src.b2 * a2;
    const double nb1 = -2.0 * src.b0 * alpha + src.b1 * (1.0 + a2) - 2.0 * src.b2 * alpha;
    const double nb2 = src.b0 * a2 - src.b1 * alpha + src.b2;

    // Denominator: 1 + a1*z1^-1 + a2*z1^-2 (note: ViPER .vdc stores negated a1, a2 in some files;
    // we normalize to standard Direct Form II Transposed y = b0*x + z1; z1 = b1*x - a1*y + z2)
    const double da0 = 1.0 - src.a1 * alpha + src.a2 * a2;
    const double da1 = -2.0 * alpha + src.a1 * (1.0 + a2) - 2.0 * src.a2 * alpha;
    const double da2 = a2 - src.a1 * alpha + src.a2;

    if (std::abs(da0) < 1e-12) {
        return SosSection{};
    }

    const double invA0 = 1.0 / da0;
    SosSection dst{};
    dst.b0 = nb0 * invA0;
    dst.b1 = nb1 * invA0;
    dst.b2 = nb2 * invA0;
    dst.a1 = da1 * invA0;
    dst.a2 = da2 * invA0;
    return dst;
}

void ViperDdc::prepare(double sampleRate) {
    sampleRate_ = std::max(sampleRate, 8000.0);
    loadedRevision_ = 0;
    (void)loadActiveVdcFromDisk();
}

void ViperDdc::reset() noexcept {
    for (auto& s : sections_) {
        s.z1L = 0.0;
        s.z2L = 0.0;
        s.z1R = 0.0;
        s.z2R = 0.0;
    }
}

void ViperDdc::configure(uint32_t ddcRevision) {
    if (ddcRevision != loadedRevision_) {
        loadedRevision_ = ddcRevision;
        (void)loadActiveVdcFromDisk();
    }
}

bool ViperDdc::loadActiveVdcFromDisk() {
    std::ifstream meta("C:\\ProgramData\\ViPERHiRes\\active_ddc.txt");
    if (!meta) {
        sections_.clear();
        return false;
    }

    std::string utf8Path;
    std::getline(meta, utf8Path);
    while (!utf8Path.empty() && (utf8Path.back() == '\r' || utf8Path.back() == '\n')) {
        utf8Path.pop_back();
    }
    if (utf8Path.empty()) {
        sections_.clear();
        return false;
    }

    std::wstring widePath;
#ifdef _WIN32
    const int wlen = MultiByteToWideChar(CP_UTF8, 0, utf8Path.c_str(), -1, nullptr, 0);
    if (wlen > 1) {
        widePath.resize(static_cast<size_t>(wlen - 1));
        MultiByteToWideChar(CP_UTF8, 0, utf8Path.c_str(), -1, widePath.data(), wlen);
    }
#else
    widePath.assign(utf8Path.begin(), utf8Path.end());
#endif
    return parseVdcFile(widePath);
}

bool ViperDdc::parseVdcFile(const std::wstring& path) {
    std::ifstream in(path);
    if (!in) {
        sections_.clear();
        return false;
    }

    std::string sr44Line, sr48Line;
    std::string line;
    while (std::getline(in, line)) {
        if (line.rfind("SR_44100:", 0) == 0) {
            sr44Line = line.substr(9);
        } else if (line.rfind("SR_48000:", 0) == 0) {
            sr48Line = line.substr(9);
        }
    }

    double sourceSr = 48000.0;
    std::string chosen;
    if (std::abs(sampleRate_ - 44100.0) < 1.0 && !sr44Line.empty()) {
        sourceSr = 44100.0;
        chosen = sr44Line;
    } else if (!sr48Line.empty()) {
        sourceSr = 48000.0;
        chosen = sr48Line;
    } else if (!sr44Line.empty()) {
        sourceSr = 44100.0;
        chosen = sr44Line;
    } else {
        sections_.clear();
        return false;
    }

    std::vector<double> coeffs;
    std::stringstream ss(chosen);
    std::string token;
    while (std::getline(ss, token, ',')) {
        try {
            coeffs.push_back(std::stod(token));
        } catch (...) {
            // ignore whitespace/empty tokens
        }
    }

    const size_t numSections = coeffs.size() / 5;
    std::vector<SosSection> parsed;
    parsed.reserve(numSections);

    for (size_t i = 0; i < numSections; ++i) {
        // ViPER .vdc format stores [b0, b1, b2, -a1, -a2] (feedback signs negated vs DF2T subtraction)
        SosSection raw{};
        raw.b0 = coeffs[i * 5 + 0];
        raw.b1 = coeffs[i * 5 + 1];
        raw.b2 = coeffs[i * 5 + 2];
        raw.a1 = -coeffs[i * 5 + 3];
        raw.a2 = -coeffs[i * 5 + 4];
        parsed.push_back(transformSampleRate(raw, sourceSr, sampleRate_));
    }

    sections_ = std::move(parsed);
    reset();
    return !sections_.empty();
}

void ViperDdc::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    if (sections_.empty()) {
        return;
    }

    const size_t total = interleavedSamples.size() & ~size_t{1};
    for (size_t i = 0; i < total; i += 2) {
        double l = static_cast<double>(interleavedSamples[i]);
        double r = static_cast<double>(interleavedSamples[i + 1]);

        for (auto& s : sections_) {
            const double outL = s.b0 * l + s.z1L;
            s.z1L = s.b1 * l - s.a1 * outL + s.z2L;
            s.z2L = s.b2 * l - s.a2 * outL;
            if (std::abs(s.z1L) < 1e-18) s.z1L = 0.0;
            if (std::abs(s.z2L) < 1e-18) s.z2L = 0.0;
            l = outL;

            const double outR = s.b0 * r + s.z1R;
            s.z1R = s.b1 * r - s.a1 * outR + s.z2R;
            s.z2R = s.b2 * r - s.a2 * outR;
            if (std::abs(s.z1R) < 1e-18) s.z1R = 0.0;
            if (std::abs(s.z2R) < 1e-18) s.z2R = 0.0;
            r = outR;
        }

        interleavedSamples[i] = static_cast<float>(l);
        interleavedSamples[i + 1] = static_cast<float>(r);
    }
}

} // namespace viper
