#include <SDK/foobar2000.h>

#include "viper/SharedConfig.hpp"
#include "viper/ViperEngine.hpp"

#include <cstdint>
#include <cstdio>
#include <span>
#include <type_traits>
#include <vector>

DECLARE_COMPONENT_VERSION(
    "ViPERHiRes DSP (Auto Sample-Rate)",
    "0.2.0",
    "Hi-Res 44.1kHz-384kHz 64-bit internal DSP synced in real time with ViPERHiRes Console.\n"
    "Automatically follows each track's native sample rate in WASAPI Exclusive / Bit-Perfect mode."
);

VALIDATE_COMPONENT_FILENAME("foo_dsp_viperhires.dll");

namespace {

// Unique GUID for ViPERHiRes foobar2000 DSP component
// {5C3E9A21-8B4D-4F7A-912E-7D4A3B6C8E10}
constexpr GUID kViperHiResDspGuid = {
    0x5c3e9a21, 0x8b4d, 0x4f7a,
    { 0x91, 0x2e, 0x7d, 0x4a, 0x3b, 0x6c, 0x8e, 0x10 }
};

class dsp_viperhires : public dsp_impl_base {
public:
    dsp_viperhires() {
        if (sharedConfig_.open(false)) {
            if (sharedConfig_.readSnapshot(params_)) {
                engine_.setParams(params_);
            }
        }
    }

    static GUID g_get_guid() {
        return kViperHiResDspGuid;
    }

    static void g_get_name(pfc::string_base& p_out) {
        p_out = "ViPERHiRes DSP (Auto Sample-Rate)";
    }

    bool on_chunk(audio_chunk* chunk, abort_callback&) override {
        if (!chunk) {
            return true;
        }

        const uint32_t sr = static_cast<uint32_t>(chunk->get_srate());
        const uint32_t ch = chunk->get_channels();
        const size_t frames = chunk->get_sample_count();

        if (frames == 0 || ch != 2) {
            return true;
        }

        // Automatically follow track's native sample rate (44.1kHz -> 384kHz) without resampling!
        if (sr != currentSampleRate_) {
            currentSampleRate_ = sr;
            engine_.prepare(sr);
            engine_.setParams(params_);

            if (FILE* f = nullptr; _wfopen_s(&f, L"C:\\ProgramData\\ViPERHiRes\\foobar_diag.txt", L"w") == 0 && f) {
                std::fprintf(f, "foobar2000 Native Track Rate = %u Hz | Ch = %u | IPC = %d\n",
                             sr, ch, sharedConfig_.isOpen() ? 1 : 0);
                std::fclose(f);
            }
        }

        // Lock-free real-time sync with ViPERHiRes Web Console (http://127.0.0.1:48192)
        if (!sharedConfig_.isOpen()) {
            (void)sharedConfig_.open(false);
        }
        if (sharedConfig_.isOpen() && sharedConfig_.pollChanges(lastSeq_, params_)) {
            engine_.setParams(params_);
        }

        const size_t totalSamples = frames * 2;
        if constexpr (std::is_same_v<audio_sample, float>) {
            auto* samples = reinterpret_cast<float*>(chunk->get_data());
            engine_.processInterleavedStereo(std::span<float>{samples, totalSamples});
        } else {
            // foobar2000 v2 x64 uses 64-bit double audio_sample natively
            if (scratchBuf_.size() < totalSamples) {
                scratchBuf_.resize(totalSamples);
            }
            audio_sample* data = chunk->get_data();
            for (size_t i = 0; i < totalSamples; ++i) {
                scratchBuf_[i] = static_cast<float>(data[i]);
            }
            engine_.processInterleavedStereo(std::span<float>{scratchBuf_.data(), totalSamples});
            for (size_t i = 0; i < totalSamples; ++i) {
                data[i] = static_cast<audio_sample>(scratchBuf_[i]);
            }
        }
        return true;
    }

    void on_endofplayback(abort_callback&) override {}
    void on_endoftrack(abort_callback&) override {}

    void flush() override {
        engine_.reset();
    }

    double get_latency() override {
        return 0.0;
    }

    bool need_track_change_mark() override {
        return false;
    }

private:
    uint32_t currentSampleRate_ = 0;
    uint32_t lastSeq_ = 0;
    viper::DspParams params_{};
    viper::SharedConfig sharedConfig_{};
    viper::ViperEngine engine_{};
    std::vector<float> scratchBuf_{};
};

static dsp_factory_nopreset_t<dsp_viperhires> g_dsp_viperhires_factory;

} // namespace
