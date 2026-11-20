#pragma once

#include <array>
#include <cstdint>
#include <type_traits>

namespace viper {

constexpr uint32_t kMinSampleRate = 44100;
constexpr uint32_t kMaxSampleRate = 384000;
constexpr size_t kEqBandCount = 10;
constexpr uint32_t kParamsMagic = 0x56504852; // 'VPHR'
constexpr uint32_t kParamsVersion = 2;

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
// between the UI process, Windows APO (audiodg.exe), and foobar2000 DSP.
struct DspParams {
    uint32_t magic = kParamsMagic;
    uint32_t version = kParamsVersion;
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

    // Differential Surround (Haas effect) & Stereo Field
    uint32_t surroundEnabled = 0;
    float surroundDelayMs = 6.0f;  // 0.0 to 20.0 ms
    float stereoWidth = 1.0f;      // 0.0 (Mono) to 2.0 (Wide)

    // Output Brickwall Limiter
    uint32_t limiterEnabled = 1;
    float limiterThresholdDb = -0.3f; // -6.0 to 0.0 dBFS

    // --- V2 Classic ViPER4Windows Modules ---

    // Dynamic System
    uint32_t dynamicEnabled = 0;
    uint32_t dynamicDevice = 0;    // 0: Common Earphone, 1: Extreme HP, 2: High-End HP, 3: Studio Monitor
    float dynamicBass = 25.0f;     // 0.0 to 100.0 %

    // Reverberation (Room Simulator)
    uint32_t reverbEnabled = 0;
    float reverbRoomSize = 0.35f;  // 0.0 to 1.0
    float reverbWidth = 0.50f;     // 0.0 to 1.0
    float reverbDamping = 0.25f;   // 0.0 to 1.0
    float reverbWet = 0.20f;       // 0.0 to 1.0
    float reverbDry = 0.85f;       // 0.0 to 1.0

    // Compressor / Playback Gain Control
    uint32_t compEnabled = 0;
    float compThresholdDb = -18.0f; // -40.0 to 0.0 dB
    float compRatio = 3.0f;         // 1.0 to 20.0 : 1
    float compAttackMs = 10.0f;     // 1.0 to 100.0 ms
    float compReleaseMs = 120.0f;   // 20.0 to 500.0 ms
    float compMakeupDb = 3.0f;      // 0.0 to 18.0 dB

    // Cure Tech+ (Binaural Crossfeed / Ear Fatigue Protection)
    uint32_t cureEnabled = 0;
    uint32_t cureLevel = 0;        // 0: Slight, 1: Moderate, 2: Extreme
};

static_assert(std::is_trivially_copyable_v<DspParams>,
              "DspParams must be trivially copyable for Shared Memory IPC");
static_assert(sizeof(DspParams) == 192,
              "DspParams v2 binary layout must be exactly 192 bytes (SharedConfigBlock = 196 bytes)");

} // namespace viper
