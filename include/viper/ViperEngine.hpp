#pragma once

#include "viper/AnalogX.hpp"
#include "viper/Compressor.hpp"
#include "viper/Convolver.hpp"
#include "viper/CureTech.hpp"
#include "viper/DifferentialSurround.hpp"
#include "viper/DspTypes.hpp"
#include "viper/DynamicSystem.hpp"
#include "viper/FieldSurround.hpp"
#include "viper/Limiter.hpp"
#include "viper/ParametricEq.hpp"
#include "viper/PlaybackGain.hpp"
#include "viper/Reverb.hpp"
#include "viper/SpectrumExtension.hpp"
#include "viper/TubeSimulator.hpp"
#include "viper/VhsPlus.hpp"
#include "viper/ViperBass.hpp"
#include "viper/ViperClarity.hpp"
#include "viper/ViperDdc.hpp"
#include <span>

namespace viper {

class ViperEngine {
public:
    void prepare(uint32_t sampleRate);
    void reset() noexcept;

    void setParams(const DspParams& params) noexcept;

    [[nodiscard]] uint32_t sampleRate() const noexcept { return sampleRate_; }
    [[nodiscard]] const DspParams& params() const noexcept { return params_; }

    // Real-time safe, zero-allocation audio processing for interleaved 32-bit float stereo
    void processInterleavedStereo(std::span<float> interleavedSamples) noexcept;

private:
    void applyParamsToModules() noexcept;

    uint32_t sampleRate_ = 48000;
    DspParams params_{};

    ViperDdc ddc_{};
    ParametricEq eq_{};
    ViperBass bass_{};
    DynamicSystem dynamic_{};
    SpectrumExtension vse_{};
    ViperClarity clarity_{};
    TubeSimulator tube_{};
    AnalogX analogX_{};
    Compressor compressor_{};
    PlaybackGain agc_{};
    CureTech cure_{};
    FieldSurround fieldSurround_{};
    VhsPlus vhs_{};
    DifferentialSurround surround_{};
    Reverb reverb_{};
    Convolver convolver_{};
    Limiter limiter_{};
};

} // namespace viper
