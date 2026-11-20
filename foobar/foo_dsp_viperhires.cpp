#include <SDK/foobar2000.h>

#include "viper/SharedConfig.hpp"
#include "viper/ViperEngine.hpp"

#include <chrono>
#include <cstdint>
#include <cstdio>
#include <span>
#include <string>
#include <type_traits>
#include <vector>

DECLARE_COMPONENT_VERSION(
    "ViPERHiRes DSP (Auto Sample-Rate)",
    "0.3.0",
    "Hi-Res 44.1kHz-384kHz 64-bit internal DSP synced in real time with ViPERHiRes Console.\n"
    "Automatically follows each track's native sample rate and reports live source telemetry."
);

VALIDATE_COMPONENT_FILENAME("foo_dsp_viperhires.dll");

namespace {

// Unique GUID for ViPERHiRes foobar2000 DSP component
// {5C3E9A21-8B4D-4F7A-912E-7D4A3B6C8E10}
constexpr GUID kViperHiResDspGuid = {
    0x5c3e9a21, 0x8b4d, 0x4f7a,
    { 0x91, 0x2e, 0x7d, 0x4a, 0x3b, 0x6c, 0x8e, 0x10 }
};

std::string jsonEscape(const char* s) {
    if (!s) return {};
    std::string out;
    for (const unsigned char* p = reinterpret_cast<const unsigned char*>(s); *p; ++p) {
        switch (*p) {
            case '"': out += "\\\""; break;
            case '\\': out += "\\\\"; break;
            case '\n': out += "\\n"; break;
            case '\r': out += "\\r"; break;
            case '\t': out += "\\t"; break;
            default:
                if (*p >= 0x20) out.push_back(static_cast<char>(*p));
                break;
        }
    }
    return out;
}

int64_t nowEpochMs() noexcept {
    using namespace std::chrono;
    return duration_cast<milliseconds>(system_clock::now().time_since_epoch()).count();
}

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

        bool telemetryDirty = false;
        if (sr != currentSampleRate_) {
            currentSampleRate_ = sr;
            engine_.prepare(sr);
            engine_.setParams(params_);
            telemetryDirty = true;
        }

        // Check if track changed or heartbeat interval (1000ms) elapsed
        dsp_track_t curTrack = get_cur_file();
        if (curTrack != lastTrack_) {
            lastTrack_ = curTrack;
            refreshTrackMetadata(curTrack);
            telemetryDirty = true;
        }

        const int64_t nowMs = nowEpochMs();
        if (telemetryDirty || (nowMs - lastTelemetryWriteMs_) >= 1000) {
            lastTelemetryWriteMs_ = nowMs;
            writeLiveStatusJson(sr, ch, nowMs);
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
        return true;
    }

private:
    void refreshTrackMetadata(const dsp_track_t& track) {
        trackTitle_.clear();
        trackArtist_.clear();
        trackCodec_.clear();
        trackBitDepth_ = 0;
        trackBitrateKbps_ = 0;

        if (!track.is_valid()) {
            return;
        }

        metadb_info_container::ptr infoRef = track->get_info_ref();
        if (!infoRef.is_valid()) {
            return;
        }

        const file_info& fi = infoRef->info();
        if (const char* t = fi.meta_get("TITLE", 0)) trackTitle_ = t;
        if (const char* a = fi.meta_get("ARTIST", 0)) trackArtist_ = a;
        if (const char* c = fi.info_get("codec")) trackCodec_ = c;

        const int64_t bps = fi.info_get_int("bitspersample");
        if (bps > 0) trackBitDepth_ = static_cast<uint32_t>(bps);

        const int64_t br = fi.info_get_bitrate();
        if (br > 0) trackBitrateKbps_ = static_cast<uint32_t>(br);
    }

    void writeLiveStatusJson(uint32_t sr, uint32_t ch, int64_t nowMs) const noexcept {
        FILE* f = nullptr;
        if (_wfopen_s(&f, L"C:\\ProgramData\\ViPERHiRes\\foobar_status.json", L"wb") == 0 && f) {
            std::fprintf(
                f,
                "{\"source\":\"foobar2000\",\"mode\":\"Native DSP · 0%% Resample\","
                "\"sampleRate\":%u,\"channels\":%u,\"bitDepth\":%u,\"bitrateKbps\":%u,"
                "\"codec\":\"%s\",\"title\":\"%s\",\"artist\":\"%s\",\"timestampMs\":%lld}",
                sr,
                ch,
                trackBitDepth_,
                trackBitrateKbps_,
                jsonEscape(trackCodec_.c_str()).c_str(),
                jsonEscape(trackTitle_.c_str()).c_str(),
                jsonEscape(trackArtist_.c_str()).c_str(),
                static_cast<long long>(nowMs));
            std::fclose(f);
        }
    }

    uint32_t currentSampleRate_ = 0;
    uint32_t lastSeq_ = 0;
    int64_t lastTelemetryWriteMs_ = 0;
    dsp_track_t lastTrack_{};
    std::string trackTitle_{};
    std::string trackArtist_{};
    std::string trackCodec_{};
    uint32_t trackBitDepth_ = 0;
    uint32_t trackBitrateKbps_ = 0;

    viper::DspParams params_{};
    viper::SharedConfig sharedConfig_{};
    viper::ViperEngine engine_{};
    std::vector<float> scratchBuf_{};
};

static dsp_factory_nopreset_t<dsp_viperhires> g_dsp_viperhires_factory;

} // namespace
