#include "viper/ViperBass.hpp"
#include <algorithm>
#include <cmath>

namespace viper {

void ViperBass::prepare(double sampleRate) noexcept {
    sampleRate_ = std::max(sampleRate, 8000.0);
    // 5 ms attack / 45 ms release envelope tracker for Sub-Octave synthesizer
    envAttackCoeff_ = std::exp(-1.0 / (sampleRate_ * 0.005));
    envReleaseCoeff_ = std::exp(-1.0 / (sampleRate_ * 0.045));
    reset();
    configure(mode_, frequencyHz_, gainDb_);
}

void ViperBass::reset() noexcept {
    subEnvL_ = subEnvR_ = 0.0;
    prevBandL_ = prevBandR_ = 0.0;
    subSignL_ = subSignR_ = 1.0;

    lowPassL_.reset();
    lowPassR_.reset();
    highPassHarmonicL_.reset();
    highPassHarmonicR_.reset();
    shelfL_.reset();
    shelfR_.reset();

    octaveSrcBpL_.reset();
    octaveSrcBpR_.reset();
    subSmooth1L_.reset();
    subSmooth1R_.reset();
    subSmooth2L_.reset();
    subSmooth2R_.reset();
    subDcHpL_.reset();
    subDcHpR_.reset();
}

void ViperBass::configure(BassMode mode, float frequencyHz, float gainDb) noexcept {
    mode_ = mode;
    frequencyHz_ = std::clamp(frequencyHz, 30.0f, 150.0f);
    gainDb_ = std::clamp(gainDb, 0.0f, 15.0f);

    const double linearGain = std::pow(10.0, static_cast<double>(gainDb_) / 20.0) - 1.0;
    wetGain_ = std::max(0.0, linearGain);

    // Isolate bass band dynamically at current sampleRate
    lowPassL_.configure(BiquadType::LowPass, sampleRate_, frequencyHz_, 0.7071);
    lowPassR_.configure(BiquadType::LowPass, sampleRate_, frequencyHz_, 0.7071);

    // Remove DC / sub-sonic rumble below 22 Hz from synthesized harmonics
    highPassHarmonicL_.configure(BiquadType::HighPass, sampleRate_, 22.0, 0.7071);
    highPassHarmonicR_.configure(BiquadType::HighPass, sampleRate_, 22.0, 0.7071);

    // Clean low-shelf for Natural mode
    shelfL_.configure(BiquadType::LowShelf, sampleRate_, frequencyHz_, 0.78, gainDb_);
    shelfR_.configure(BiquadType::LowShelf, sampleRate_, frequencyHz_, 0.78, gainDb_);

    // True Sub-Octave synthesizer filters (Subwoofer mode):
    // Track upper-bass fundamental octave (~1.45x cutoff) -> divide frequency by 2 -> smooth 4th-order LP
    const double srcCenter = std::clamp(static_cast<double>(frequencyHz_) * 1.45, 55.0, 180.0);
    octaveSrcBpL_.configure(BiquadType::BandPass, sampleRate_, srcCenter, 1.1);
    octaveSrcBpR_.configure(BiquadType::BandPass, sampleRate_, srcCenter, 1.1);

    const double subCut = std::clamp(static_cast<double>(frequencyHz_) * 1.05, 42.0, 120.0);
    subSmooth1L_.configure(BiquadType::LowPass, sampleRate_, subCut, 0.7071);
    subSmooth1R_.configure(BiquadType::LowPass, sampleRate_, subCut, 0.7071);
    subSmooth2L_.configure(BiquadType::LowPass, sampleRate_, subCut, 0.7071);
    subSmooth2R_.configure(BiquadType::LowPass, sampleRate_, subCut, 0.7071);

    subDcHpL_.configure(BiquadType::HighPass, sampleRate_, 22.0, 0.7071);
    subDcHpR_.configure(BiquadType::HighPass, sampleRate_, 22.0, 0.7071);
}

void ViperBass::processInterleavedStereo(std::span<float> interleavedSamples) noexcept {
    if (gainDb_ <= 0.01f) {
        return;
    }

    const size_t total = interleavedSamples.size() & ~size_t{1};

    if (mode_ == BassMode::Natural) {
        for (size_t i = 0; i < total; i += 2) {
            interleavedSamples[i] = static_cast<float>(shelfL_.processSample(interleavedSamples[i]));
            interleavedSamples[i + 1] = static_cast<float>(shelfR_.processSample(interleavedSamples[i + 1]));
        }
        return;
    }

    if (mode_ == BassMode::PureBassPlus) {
        // PureBass+: Tight sub-bass punch + asymmetric 2nd-harmonic warmth with soft peak control
        constexpr double drive = 1.75;
        const double mix = std::min(wetGain_ * 0.46, 2.2);

        for (size_t i = 0; i < total; i += 2) {
            const double inL = static_cast<double>(interleavedSamples[i]);
            const double inR = static_cast<double>(interleavedSamples[i + 1]);

            const double subL = lowPassL_.processSample(inL);
            const double subR = lowPassR_.processSample(inR);

            // Asymmetric soft saturation generates punchy 2nd+3rd upper bass harmonics
            const double harmL = std::tanh(subL * drive) / drive + 0.14 * subL * subL;
            const double harmR = std::tanh(subR * drive) / drive + 0.14 * subR * subR;

            const double cleanL = highPassHarmonicL_.processSample(harmL);
            const double cleanR = highPassHarmonicR_.processSample(harmR);

            // Soft ceiling on wet bass bus keeps punch tight without muddying mids
            const double wetL = std::tanh(cleanL * mix * 1.15) / 1.15;
            const double wetR = std::tanh(cleanR * mix * 1.15) / 1.15;

            interleavedSamples[i] = static_cast<float>(inL + wetL);
            interleavedSamples[i + 1] = static_cast<float>(inR + wetR);
        }
        return;
    }

    // Subwoofer Mode: True Sub-Octave Synthesizer (f/2 zero-crossing divider + 4th-order LP)
    // blended with warm low-end harmonic weight
    const double subMix = std::min(wetGain_ * 0.58, 2.6);

    for (size_t i = 0; i < total; i += 2) {
        const double inL = static_cast<double>(interleavedSamples[i]);
        const double inR = static_cast<double>(interleavedSamples[i + 1]);

        // 1. Extract fundamental bass octave & follow smooth amplitude envelope
        const double bpL = octaveSrcBpL_.processSample(inL);
        const double bpR = octaveSrcBpR_.processSample(inR);

        const double absL = std::abs(bpL);
        const double absR = std::abs(bpR);
        subEnvL_ = (absL > subEnvL_)
            ? (envAttackCoeff_ * subEnvL_ + (1.0 - envAttackCoeff_) * absL)
            : (envReleaseCoeff_ * subEnvL_ + (1.0 - envReleaseCoeff_) * absL);
        subEnvR_ = (absR > subEnvR_)
            ? (envAttackCoeff_ * subEnvR_ + (1.0 - envAttackCoeff_) * absR)
            : (envReleaseCoeff_ * subEnvR_ + (1.0 - envReleaseCoeff_) * absR);
        if (subEnvL_ < 1e-18) subEnvL_ = 0.0;
        if (subEnvR_ < 1e-18) subEnvR_ = 0.0;

        // 2. Positive zero-crossing flip-flop generates exact half-frequency (1 octave down)
        if (prevBandL_ <= 0.0 && bpL > 0.0 && subEnvL_ > 0.002) subSignL_ = -subSignL_;
        if (prevBandR_ <= 0.0 && bpR > 0.0 && subEnvR_ > 0.002) subSignR_ = -subSignR_;
        prevBandL_ = bpL;
        prevBandR_ = bpR;

        // 3. Reconstruct pure sine-like sub-octave via cascaded 4th-order Low-Pass + 22 Hz HP
        double octL = subSmooth2L_.processSample(subSmooth1L_.processSample(subSignL_ * subEnvL_ * 1.35));
        double octR = subSmooth2R_.processSample(subSmooth1R_.processSample(subSignR_ * subEnvR_ * 1.35));
        octL = subDcHpL_.processSample(octL);
        octR = subDcHpR_.processSample(octR);

        // 4. Parallel direct sub-bass harmonic punch
        const double dirL = highPassHarmonicL_.processSample(std::tanh(lowPassL_.processSample(inL) * 2.1) / 2.1);
        const double dirR = highPassHarmonicR_.processSample(std::tanh(lowPassR_.processSample(inR) * 2.1) / 2.1);

        // 5. Blend true sub-octave + harmonic body with soft anti-clipping ceiling
        const double combinedL = (octL * 0.62 + dirL * 0.45) * subMix;
        const double combinedR = (octR * 0.62 + dirR * 0.45) * subMix;

        interleavedSamples[i] = static_cast<float>(inL + std::tanh(combinedL * 1.1) / 1.1);
        interleavedSamples[i + 1] = static_cast<float>(inR + std::tanh(combinedR * 1.1) / 1.1);
    }
}

} // namespace viper
