#include "viper/ViperEngine.hpp"

#include <algorithm>
#include <array>
#include <chrono>
#include <cmath>
#include <cstdint>
#include <fstream>
#include <iostream>
#include <numbers>
#include <string>
#include <vector>

namespace {

double computeRmsDb(const std::vector<float>& interleaved) {
    if (interleaved.empty()) return -120.0;
    double sumSq = 0.0;
    for (float s : interleaved) {
        sumSq += static_cast<double>(s) * static_cast<double>(s);
    }
    const double rms = std::sqrt(sumSq / static_cast<double>(interleaved.size()));
    return (rms > 1e-12) ? 20.0 * std::log10(rms) : -120.0;
}

bool hasInvalidSamples(const std::vector<float>& interleaved) {
    return std::any_of(interleaved.begin(), interleaved.end(), [](float s) {
        return !std::isfinite(s);
    });
}

float maxAbsPeak(const std::vector<float>& interleaved) {
    float peak = 0.0f;
    for (float s : interleaved) {
        peak = std::max(peak, std::abs(s));
    }
    return peak;
}

std::vector<float> generateSineStereo(uint32_t sampleRate, double freqHz, double durationSec, float amplitude) {
    const size_t frames = static_cast<size_t>(sampleRate * durationSec);
    std::vector<float> buf(frames * 2);
    for (size_t i = 0; i < frames; ++i) {
        const double phase = 2.0 * std::numbers::pi * freqHz * (static_cast<double>(i) / sampleRate);
        const float sample = static_cast<float>(amplitude * std::sin(phase));
        buf[i * 2] = sample;
        buf[i * 2 + 1] = sample;
    }
    return buf;
}

bool runHiResSelfTest() {
    constexpr std::array<uint32_t, 5> sampleRates = {44100, 48000, 96000, 192000, 384000};
    bool allPassed = true;

    std::cout << "=== ViPERHiRes DSP Engine Self-Test (44.1kHz -> 384kHz) ===\n\n";

    // Test 1: Sample-rate invariance of 62 Hz EQ band (+6 dB boost on 62 Hz tone)
    std::cout << "[Test 1] Sample-Rate Invariance (62 Hz tone + 6.0 dB EQ boost):\n";
    for (uint32_t sr : sampleRates) {
        viper::ViperEngine engine;
        engine.prepare(sr);

        viper::DspParams params{};
        params.masterEnabled = 1;
        params.eqEnabled = 1;
        params.eqBandsDb[1] = 6.0f; // 62 Hz band
        params.limiterEnabled = 0;
        engine.setParams(params);

        auto buf = generateSineStereo(sr, 62.0, 0.5, 0.25f);
        const double inDb = computeRmsDb(buf);
        engine.processInterleavedStereo(buf);
        const double outDb = computeRmsDb(buf);
        const double deltaDb = outDb - inDb;

        const bool ok = !hasInvalidSamples(buf) && std::abs(deltaDb - 6.0) < 0.35;
        allPassed = allPassed && ok;
        std::cout << "  SR=" << sr << " Hz | Gain Delta = " << deltaDb
                  << " dB (expected ~6.0 dB) | " << (ok ? "PASS" : "FAIL") << "\n";
    }

    // Test 2: Full DSP chain stress test + latency benchmark (10ms block)
    std::cout << "\n[Test 2] Full DSP Chain (EQ + Bass + Clarity + Tube + Surround + Limiter):\n";
    for (uint32_t sr : sampleRates) {
        viper::ViperEngine engine;
        engine.prepare(sr);

        viper::DspParams params{};
        params.masterEnabled = 1;
        params.eqEnabled = 1;
        params.eqBandsDb = {3.0f, 4.0f, 1.5f, 0.0f, -1.0f, 0.0f, 1.5f, 2.5f, 3.0f, 2.0f};
        params.bassEnabled = 1;
        params.bassMode = viper::BassMode::PureBassPlus;
        params.bassFrequencyHz = 65.0f;
        params.bassGainDb = 6.0f;
        params.clarityEnabled = 1;
        params.clarityMode = viper::ClarityMode::XHiFi;
        params.clarityGainDb = 5.0f;
        params.tubeEnabled = 1;
        params.tubeDrive = 0.3f;
        params.surroundEnabled = 1;
        params.surroundDelayMs = 6.0f;
        params.stereoWidth = 1.25f;
        params.limiterEnabled = 1;
        params.limiterThresholdDb = -0.3f;
        engine.setParams(params);

        // 10 ms buffer (standard Windows Audio Engine period)
        const size_t blockFrames = sr / 100;
        std::vector<float> block(blockFrames * 2);
        for (size_t i = 0; i < blockFrames; ++i) {
            const double t = static_cast<double>(i) / sr;
            const float s = static_cast<float>(
                0.4 * std::sin(2.0 * std::numbers::pi * 60.0 * t) +
                0.3 * std::sin(2.0 * std::numbers::pi * 1000.0 * t) +
                0.2 * std::sin(2.0 * std::numbers::pi * 12000.0 * t));
            block[i * 2] = s;
            block[i * 2 + 1] = s * 0.9f;
        }

        constexpr int kIterations = 100;
        const auto t0 = std::chrono::steady_clock::now();
        for (int it = 0; it < kIterations; ++it) {
            engine.processInterleavedStereo(block);
        }
        const auto t1 = std::chrono::steady_clock::now();
        const double avgUs = std::chrono::duration<double, std::micro>(t1 - t0).count() / kIterations;
        const float peak = maxAbsPeak(block);
        const bool ok = !hasInvalidSamples(block) && peak <= 1.0f && avgUs < 5000.0;
        allPassed = allPassed && ok;

        std::cout << "  SR=" << sr << " Hz | 10ms block time = " << avgUs
                  << " us | Peak = " << peak << " | " << (ok ? "PASS" : "FAIL") << "\n";
    }

    std::cout << "\nOverall Result: " << (allPassed ? "ALL TESTS PASSED" : "FAILED") << "\n";
    return allPassed;
}

} // namespace

int main() {
    return runHiResSelfTest() ? 0 : 1;
}
