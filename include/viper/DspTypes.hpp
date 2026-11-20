#pragma once

#include <array>
#include <cstdint>
#include <type_traits>

namespace viper {

constexpr uint32_t kMinSampleRate = 44100;
constexpr uint32_t kMaxSampleRate = 384000;
constexpr size_t kEqBandCount = 10;

// Standard 10-band ISO center frequencies (Hz)
constexpr std::array<double, kEqBandCount> kEqFrequencies = {
    31.0, 62.0, 125.0, 250.0, 500.0,
    1000.0, 2000.0, 4000.0, 8000.0, 16000.0
};

enum class BassMode : uint32_t {
    Natural = 0,
    PureBassPlus = 1,
    Subwoofer = 2
};

enum class ClarityMode : uint32_t {
    Natural = 0,
    OZonePlus = 1,
    XHiFi = 2
};

// Trivially copyable parameter block designed for lock-free Shared Memory IPC
// between the UI process and the Windows APO (audiodg.exe).
struct DspParams {
    uint32_t magic = 0x56504852; // 'VPHR' (ViPER Hi-Res)
    uint32_t version = 1;
    uint32_t sequence = 0;       // Incremented on every UI update

    // Master Control
    uint32_t masterEnabled = 1;
    float masterGainDb = 0.0f;   // -24.0 to +12.0 dB
    float outputPan = 0.0f;      // -1.0 (Left) to +1.0 (Right)

    // 10-Band Hi-Res Equalizer
    uint32_t eqEnabled = 0;
    std::array<float, kEqBandCount> eqBandsDb = {
        0.0f, 0.0f, 0.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 0.0f, 0.0f, 0.0f
    }; // -12.0 to +12.0 dB per band

    // ViPER Bass
    uint32_t bassEnabled = 0;
    BassMode bassMode = BassMode::Natural;
    float bassFrequencyHz = 60.0f; // 30.0 to 120.0 Hz
    float bassGainDb = 3.5f;       // 0.0 to 15.0 dB

    // ViPER Clarity
    uint32_t clarityEnabled = 0;
    ClarityMode clarityMode = ClarityMode::Natural;
    float clarityGainDb = 3.5f;    // 0.0 to 15.0 dB

    // Tube Sound Simulator (6N1J analog warmth)
    uint32_t tubeEnabled = 0;
    float tubeDrive = 0.25f;       // 0.0 to 1.0

    // Differential Surround (Haas effect)
    uint32_t surroundEnabled = 0;
    float surroundDelayMs = 6.0f;  // 0.5 to 20.0 ms
    float stereoWidth = 1.0f;      // 0.0 (Mono) to 2.0 (Wide)

    // Output Brickwall Limiter (always recommended to prevent digital clipping)
    uint32_t limiterEnabled = 1;
    float limiterThresholdDb = -0.3f; // -6.0 to 0.0 dBFS
};

static_assert(std::is_trivially_copyable_v<DspParams>,
              "DspParams must be trivially copyable for Shared Memory IPC");

} // namespace viper
