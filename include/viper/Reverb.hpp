#pragma once

#include <array>
#include <vector>
#include <span>

namespace viper {

// Hi-Res Stereo Reverberation (Schroeder/Freeverb architecture).
// All comb & all-pass delay lines are scaled dynamically by (sampleRate / 44100.0)
// and pre-allocated in prepare() for zero-allocation real-time operation up to 384kHz.
class Reverb {
public:
    void prepare(double sampleRate);
    void reset() noexcept;
    void configure(float roomSize, float width, float damping, float wet, float dry) noexcept;

    void processInterleavedStereo(std::span<float> interleavedSamples) noexcept;

private:
    struct CombFilter {
        std::vector<float> buf;
        size_t idx = 0;
        float filterStore = 0.0f;

        void resize(size_t n) {
            buf.assign(n > 0 ? n : 1, 0.0f);
            idx = 0;
            filterStore = 0.0f;
        }
        void clear() noexcept {
            std::fill(buf.begin(), buf.end(), 0.0f);
            idx = 0;
            filterStore = 0.0f;
        }
        inline float process(float input, float feedback, float damp1, float damp2) noexcept {
            const float output = buf[idx];
            filterStore = (output * damp2) + (filterStore * damp1);
            if (std::abs(filterStore) < 1e-18f) filterStore = 0.0f;
            buf[idx] = input + (filterStore * feedback);
            if (++idx >= buf.size()) idx = 0;
            return output;
        }
    };

    struct AllPassFilter {
        std::vector<float> buf;
        size_t idx = 0;

        void resize(size_t n) {
            buf.assign(n > 0 ? n : 1, 0.0f);
            idx = 0;
        }
        void clear() noexcept {
            std::fill(buf.begin(), buf.end(), 0.0f);
            idx = 0;
        }
        inline float process(float input) noexcept {
            const float bufOut = buf[idx];
            const float output = -input + bufOut;
            buf[idx] = input + (bufOut * 0.5f);
            if (++idx >= buf.size()) idx = 0;
            return output;
        }
    };

    static constexpr size_t kNumCombs = 8;
    static constexpr size_t kNumAllPasses = 4;

    std::array<CombFilter, kNumCombs> combL_{};
    std::array<CombFilter, kNumCombs> combR_{};
    std::array<AllPassFilter, kNumAllPasses> allPassL_{};
    std::array<AllPassFilter, kNumAllPasses> allPassR_{};

    float feedback_ = 0.8f;
    float damp1_ = 0.2f;
    float damp2_ = 0.8f;
    float wet1_ = 0.25f;
    float wet2_ = 0.05f;
    float dry_ = 0.85f;
};

} // namespace viper
