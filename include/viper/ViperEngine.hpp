#pragma once

#include "viper/Compressor.hpp"
#include "viper/Convolver.hpp"
#include "viper/CureTech.hpp"
#include "viper/DifferentialSurround.hpp"
#include "viper/DspTypes.hpp"
#include "viper/DynamicSystem.hpp"
#include "viper/Limiter.hpp"
#include "viper/ParametricEq.hpp"
#include "viper/Reverb.hpp"
#include "viper/TubeSimulator.hpp"
#include "viper/ViperBass.hpp"
#include "viper/ViperClarity.hpp"
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

    ParametricEq eq_{};
    ViperBass bass_{};
    DynamicSystem dynamic_{};
    ViperClarity clarity_{};
    TubeSimulator tube_{};
    Compressor compressor_{};
    CureTech cure_{};
    DifferentialSurround surround_{};
    Reverb reverb_{};
    Convolver convolver_{};
    Limiter limiter_{};
};

} // namespace viper
