#include <SDK/foobar2000.h>

#include <atomic>
#include <chrono>
#include <cstdint>
#include <cstdio>
#include <string>

DECLARE_COMPONENT_VERSION(
    "ViPER4Windows RE Status Reporter",
    "0.4.1",
    "Bit-transparent telemetry reporter for ViPER4Windows RE.\n"
    "Reports instant Play/Pause/Stop state, track info, and audio quality with zero sample modification."
);

VALIDATE_COMPONENT_FILENAME("foo_dsp_viperhires.dll");

namespace {

// {5C3E9A21-8B4D-4F7A-912E-7D4A3B6C8E10}
constexpr GUID kViperHiResDspGuid = {
    0x5c3e9a21, 0x8b4d, 0x4f7a,
    { 0x91, 0x2e, 0x7d, 0x4a, 0x3b, 0x6c, 0x8e, 0x10 }
};

std::atomic<bool> g_isPlaying{false};

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

void writeStoppedStatusJson() noexcept {
    FILE* f = nullptr;
    if (_wfopen_s(&f, L"C:\\ProgramData\\ViPERHiRes\\foobar_status.json", L"wb") == 0 && f) {
        std::fprintf(f, "{\"source\":\"foobar2000\",\"playing\":false,\"timestampMs\":%lld}",
                     static_cast<long long>(nowEpochMs()));
        std::fclose(f);
    }
}

// Instant Play / Pause / Stop listener so UI never gets confused between Shared/Exclusive on pause
class viper_play_callback : public play_callback_static {
public:
    unsigned get_flags() override {
        return flag_on_playback_pause | flag_on_playback_stop | flag_on_playback_starting;
    }

    void on_playback_starting(play_control::t_track_command, bool p_paused) override {
        g_isPlaying.store(!p_paused, std::memory_order_release);
        if (p_paused) writeStoppedStatusJson();
    }

    void on_playback_pause(bool p_state) override {
        g_isPlaying.store(!p_state, std::memory_order_release);
        if (p_state) {
            writeStoppedStatusJson();
        }
    }

    void on_playback_stop(play_control::t_stop_reason) override {
        g_isPlaying.store(false, std::memory_order_release);
        writeStoppedStatusJson();
    }

    void on_playback_new_track(metadb_handle_ptr) override {}
    void on_playback_seek(double) override {}
    void on_playback_edited(metadb_handle_ptr) override {}
    void on_playback_dynamic_info(const file_info&) override {}
    void on_playback_dynamic_info_track(const file_info&) override {}
    void on_playback_time(double) override {}
    void on_volume_change(float) override {}
};

static play_callback_static_factory_t<viper_play_callback> g_viper_play_callback_factory;

// Pure bit-transparent telemetry pass-through (zero DSP processing, zero sample modification)
class dsp_viperhires : public dsp_impl_base {
public:
    dsp_viperhires() = default;

    static GUID g_get_guid() {
        return kViperHiResDspGuid;
    }

    static void g_get_name(pfc::string_base& p_out) {
        p_out = "ViPER4Windows RE Status Reporter";
    }

    bool on_chunk(audio_chunk* chunk, abort_callback&) override {
        if (!chunk) {
            return true;
        }

        if (!g_isPlaying.load(std::memory_order_acquire)) {
            return true;
        }

        const uint32_t sr = static_cast<uint32_t>(chunk->get_srate());
        const uint32_t ch = chunk->get_channels();

        bool telemetryDirty = (sr != lastSampleRate_);
        if (telemetryDirty) {
            lastSampleRate_ = sr;
        }

        dsp_track_t curTrack = get_cur_file();
        if (curTrack != lastTrack_) {
            lastTrack_ = curTrack;
            refreshTrackMetadata(curTrack);
            telemetryDirty = true;
        }

        const int64_t nowMs = nowEpochMs();
        if (telemetryDirty || (nowMs - lastTelemetryWriteMs_) >= 400) {
            lastTelemetryWriteMs_ = nowMs;
            writeLiveStatusJson(sr, ch, nowMs);
        }

        return true;
    }

    void on_endofplayback(abort_callback&) override {
        g_isPlaying.store(false, std::memory_order_release);
        writeStoppedStatusJson();
    }

    void on_endoftrack(abort_callback&) override {}
    void flush() override {}
    double get_latency() override { return 0.0; }
    bool need_track_change_mark() override { return true; }

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
                "{\"source\":\"foobar2000\",\"playing\":true,\"sampleRate\":%u,\"channels\":%u,"
                "\"bitDepth\":%u,\"bitrateKbps\":%u,\"codec\":\"%s\","
                "\"title\":\"%s\",\"artist\":\"%s\",\"timestampMs\":%lld}",
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

    uint32_t lastSampleRate_ = 0;
    int64_t lastTelemetryWriteMs_ = 0;
    dsp_track_t lastTrack_{};
    std::string trackTitle_{};
    std::string trackArtist_{};
    std::string trackCodec_{};
    uint32_t trackBitDepth_ = 0;
    uint32_t trackBitrateKbps_ = 0;
};

static dsp_factory_nopreset_t<dsp_viperhires> g_dsp_viperhires_factory;

} // namespace
