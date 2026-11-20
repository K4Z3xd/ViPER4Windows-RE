#pragma once

#include "viper/DifferentialSurround.hpp"
#include "viper/DspTypes.hpp"
#include "viper/Limiter.hpp"
#include "viper/ParametricEq.hpp"
#include "viper/TubeSimulator.hpp"
#include "viper/ViperBass.hpp"
#include "viper/ViperClarity.hpp"
#include <span>

namespace viper {

class ViperEngine {
public:
    // Allocates any sample-rate dependent buffers (call from non-RT setup / LockForProcess)
    void prepare(uint32_t sampleRate);
    void reset() noexcept;

    // Applies updated parameters (safe to call from RT thread when sequence changes)
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
    ViperClarity clarity_{};
    TubeSimulator tube_{};
    DifferentialSurround surround_{};
    Limiter limiter_{};
};

} // namespace viper
