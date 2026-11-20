#include "viper/ViperEngine.hpp"
#include <algorithm>
#include <cmath>

namespace viper {

void ViperEngine::prepare(uint32_t sampleRate) {
    sampleRate_ = std::clamp(sampleRate, kMinSampleRate, kMaxSampleRate);
    const double sr = static_cast<double>(sampleRate_);

    eq_.prepare(sr);
    bass_.prepare(sr);
    dynamic_.prepare(sr);
    clarity_.prepare(sr);
    tube_.prepare(sr);
    compressor_.prepare(sr);
    cure_.prepare(sr);
    surround_.prepare(sr);
    reverb_.prepare(sr);
    limiter_.prepare(sr);

    applyParamsToModules();
}

void ViperEngine::reset() noexcept {
    eq_.reset();
    bass_.reset();
    dynamic_.reset();
    clarity_.reset();
    tube_.reset();
    compressor_.reset();
    cure_.reset();
    surround_.reset();
    reverb_.reset();
    limiter_.reset();
}

void ViperEngine::setParams(const DspParams& params) noexcept {
    params_ = params;
    applyParamsToModules();
}

void ViperEngine::applyParamsToModules() noexcept {
    eq_.updateGains(params_.eqBandsDb);
    bass_.configure(params_.bassMode, params_.bassFrequencyHz, params_.bassGainDb);
    dynamic_.configure(static_cast<DynamicDeviceType>(params_.dynamicDevice), params_.dynamicBass);
    clarity_.configure(params_.clarityMode, params_.clarityGainDb);
    tube_.setDrive(params_.tubeDrive);
    compressor_.configure(params_.compThresholdDb, params_.compRatio,
                          params_.compAttackMs, params_.compReleaseMs, params_.compMakeupDb);
    cure_.configure(static_cast<CureLevel>(params_.cureLevel));
    surround_.configure(params_.surroundDelayMs, params_.stereoWidth);
    reverb_.configure(params_.reverbRoomSize, params_.reverbWidth,
                      params_.reverbDamping, params_.reverbWet, params_.reverbDry);
    limiter_.setThresholdDb(params_.limiterThresholdDb);
}

void ViperEngine::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    if (!params_.masterEnabled || interleavedSamples.size() < 2) {
        return;
    }

    if (params_.eqEnabled) {
        eq_.processInterleavedStereo(interleavedSamples);
    }

    if (params_.bassEnabled) {
        bass_.processInterleavedStereo(interleavedSamples);
    }

    if (params_.dynamicEnabled) {
        dynamic_.processInterleavedStereo(interleavedSamples);
    }

    if (params_.clarityEnabled) {
        clarity_.processInterleavedStereo(interleavedSamples);
    }

    if (params_.tubeEnabled) {
        tube_.processInterleavedStereo(interleavedSamples);
    }

    if (params_.compEnabled) {
        compressor_.processInterleavedStereo(interleavedSamples);
    }

    if (params_.cureEnabled) {
        cure_.processInterleavedStereo(interleavedSamples);
    }

    if (params_.surroundEnabled) {
        surround_.processInterleavedStereo(interleavedSamples);
    }

    if (params_.reverbEnabled) {
        reverb_.processInterleavedStereo(interleavedSamples);
    }

    // Master Gain & Output Balance (Pan)
    const float clampedGainDb = std::clamp(params_.masterGainDb, -24.0f, 12.0f);
    const float clampedPan = std::clamp(params_.outputPan, -1.0f, 1.0f);

    if (std::abs(clampedGainDb) > 1e-4f || std::abs(clampedPan) > 1e-4f) {
        const float masterLinear = std::pow(10.0f, clampedGainDb / 20.0f);
        const float gainL = masterLinear * (clampedPan > 0.0f ? (1.0f - clampedPan) : 1.0f);
        const float gainR = masterLinear * (clampedPan < 0.0f ? (1.0f + clampedPan) : 1.0f);

        const size_t total = interleavedSamples.size() & ~size_t{1};
        for (size_t i = 0; i < total; i += 2) {
            interleavedSamples[i] *= gainL;
            interleavedSamples[i + 1] *= gainR;
        }
    }

    if (params_.limiterEnabled) {
        limiter_.processInterleavedStereo(interleavedSamples);
    }
}

} // namespace viper
