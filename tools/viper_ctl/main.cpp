#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef NOMINMAX
#define NOMINMAX
#endif

#include <windows.h>
#include <initguid.h>
#include <propsys.h>
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <audiopolicy.h>
#include <endpointvolume.h>
#include <functiondiscoverykeys_devpkey.h>

#include "viper/DspTypes.hpp"
#include "viper/SharedConfig.hpp"

#include <algorithm>
#include <iostream>
#include <sstream>
#include <string>
#include <vector>

namespace {

std::string wideToUtf8(const wchar_t* wstr) {
    if (!wstr || !*wstr) return {};
    const int needed = WideCharToMultiByte(CP_UTF8, 0, wstr, -1, nullptr, 0, nullptr, nullptr);
    if (needed <= 1) return {};
    std::string out(static_cast<size_t>(needed - 1), '\0');
    WideCharToMultiByte(CP_UTF8, 0, wstr, -1, out.data(), needed, nullptr, nullptr);
    return out;
}

std::string jsonEscape(const std::string& s) {
    std::string out;
    out.reserve(s.size() + 8);
    for (char c : s) {
        switch (c) {
            case '"': out += "\\\""; break;
            case '\\': out += "\\\\"; break;
            case '\n': out += "\\n"; break;
            case '\r': out += "\\r"; break;
            case '\t': out += "\\t"; break;
            default: out += c; break;
        }
    }
    return out;
}

std::string extractGuidFromEndpointId(const std::string& endpointId) {
    const size_t dotPos = endpointId.rfind('.');
    if (dotPos != std::string::npos && dotPos + 1 < endpointId.size()) {
        return endpointId.substr(dotPos + 1);
    }
    return endpointId;
}

struct WindowSearchCtx {
    DWORD targetPid = 0;
    std::wstring targetExeLower;
    std::string bestTitle;
};

std::wstring getProcessExeNameLower(DWORD pid) {
    HANDLE hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
    if (!hProc) return {};
    wchar_t pathBuf[MAX_PATH] = {};
    DWORD pathLen = MAX_PATH;
    std::wstring fname;
    if (QueryFullProcessImageNameW(hProc, 0, pathBuf, &pathLen)) {
        std::wstring fullPath(pathBuf, pathLen);
        const size_t pos = fullPath.find_last_of(L'\\');
        fname = (pos != std::wstring::npos) ? fullPath.substr(pos + 1) : fullPath;
        std::transform(fname.begin(), fname.end(), fname.begin(), ::towlower);
    }
    CloseHandle(hProc);
    return fname;
}

BOOL CALLBACK enumWindowsForPidOrExe(HWND hwnd, LPARAM lParam) {
    auto* ctx = reinterpret_cast<WindowSearchCtx*>(lParam);
    if (!IsWindowVisible(hwnd)) return TRUE;

    DWORD pid = 0;
    GetWindowThreadProcessId(hwnd, &pid);

    bool matched = (ctx->targetPid != 0 && pid == ctx->targetPid);
    if (!matched && !ctx->targetExeLower.empty()) {
        matched = (getProcessExeNameLower(pid) == ctx->targetExeLower);
    }
    if (!matched) return TRUE;

    wchar_t titleBuf[512] = {};
    const int len = GetWindowTextW(hwnd, titleBuf, 512);
    if (len > 0) {
        std::string title = wideToUtf8(titleBuf);
        if (title.size() > ctx->bestTitle.size()) {
            ctx->bestTitle = std::move(title);
        }
    }
    return TRUE;
}

std::string getExeFileDescription(const std::wstring& fullPath) {
    DWORD dummy = 0;
    const DWORD size = GetFileVersionInfoSizeW(fullPath.c_str(), &dummy);
    if (size == 0) return {};

    std::vector<BYTE> data(size);
    if (!GetFileVersionInfoW(fullPath.c_str(), 0, size, data.data())) return {};

    struct LangCodePage { WORD lang; WORD codePage; };
    LangCodePage* trans = nullptr;
    UINT transLen = 0;
    if (!VerQueryValueW(data.data(), L"\\VarFileInfo\\Translation",
                        reinterpret_cast<void**>(&trans), &transLen) || !trans || transLen < sizeof(LangCodePage)) {
        return {};
    }

    wchar_t subBlock[128] = {};
    swprintf_s(subBlock, L"\\StringFileInfo\\%04x%04x\\FileDescription", trans[0].lang, trans[0].codePage);
    wchar_t* desc = nullptr;
    UINT descLen = 0;
    if (VerQueryValueW(data.data(), subBlock, reinterpret_cast<void**>(&desc), &descLen) && desc && descLen > 1) {
        std::string res = wideToUtf8(desc);
        if (!res.empty()) return res;
    }

    swprintf_s(subBlock, L"\\StringFileInfo\\%04x%04x\\ProductName", trans[0].lang, trans[0].codePage);
    if (VerQueryValueW(data.data(), subBlock, reinterpret_cast<void**>(&desc), &descLen) && desc && descLen > 1) {
        return wideToUtf8(desc);
    }
    return {};
}

bool isIgnoredSystemProcess(const std::string& lowerExe) {
    return lowerExe == "explorer.exe" ||
           lowerExe == "svchost.exe" ||
           lowerExe == "shellexperiencehost.exe" ||
           lowerExe == "searchhost.exe" ||
           lowerExe == "runtimebroker.exe" ||
           lowerExe == "audiodg.exe" ||
           lowerExe == "dwm.exe" ||
           lowerExe == "sihost.exe" ||
           lowerExe == "startmenuexperiencehost.exe";
}

std::string getFriendlyAppName(const std::string& exeName, const std::wstring& fullPath) {
    std::string lower = exeName;
    std::transform(lower.begin(), lower.end(), lower.begin(), [](unsigned char c) {
        return static_cast<char>(std::tolower(c));
    });

    if (lower == "chrome.exe") return "Google Chrome";
    if (lower == "msedge.exe") return "Microsoft Edge";
    if (lower == "brave.exe") return "Brave Browser";
    if (lower == "firefox.exe") return "Mozilla Firefox";
    if (lower == "opera.exe") return "Opera Browser";
    if (lower == "vivaldi.exe") return "Vivaldi Browser";
    if (lower == "zen.exe") return "Zen Browser";
    if (lower == "spotify.exe") return "Spotify";
    if (lower == "tidal.exe") return "TIDAL";
    if (lower == "applemusic.exe") return "Apple Music";
    if (lower == "foobar2000.exe") return "foobar2000";
    if (lower == "discord.exe") return "Discord";
    if (lower == "vlc.exe") return "VLC Media Player";
    if (lower == "mpv.exe") return "mpv Player";
    if (lower == "musicbee.exe") return "MusicBee";
    if (lower == "aimp.exe") return "AIMP";
    if (lower == "potplayermini64.exe" || lower == "potplayermini.exe") return "PotPlayer";

    // Query Windows PE FileDescription / ProductName for any other real app
    if (!fullPath.empty()) {
        std::string desc = getExeFileDescription(fullPath);
        if (!desc.empty()) return desc;
    }

    if (exeName.size() > 4 && lower.ends_with(".exe")) {
        return exeName.substr(0, exeName.size() - 4);
    }
    return exeName;
}

int cmdListSessions() {
    HRESULT hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    const bool didInit = SUCCEEDED(hr);

    IMMDeviceEnumerator* enumerator = nullptr;
    hr = CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_ALL,
                          __uuidof(IMMDeviceEnumerator), reinterpret_cast<void**>(&enumerator));
    if (FAILED(hr) || !enumerator) {
        std::cout << "[]\n";
        if (didInit) CoUninitialize();
        return 0;
    }

    IMMDevice* defaultDev = nullptr;
    if (FAILED(enumerator->GetDefaultAudioEndpoint(eRender, eMultimedia, &defaultDev)) || !defaultDev) {
        enumerator->Release();
        if (didInit) CoUninitialize();
        std::cout << "[]\n";
        return 0;
    }

    IAudioSessionManager2* mgr = nullptr;
    if (FAILED(defaultDev->Activate(__uuidof(IAudioSessionManager2), CLSCTX_ALL, nullptr,
                                    reinterpret_cast<void**>(&mgr))) || !mgr) {
        defaultDev->Release();
        enumerator->Release();
        if (didInit) CoUninitialize();
        std::cout << "[]\n";
        return 0;
    }

    IAudioSessionEnumerator* sessionEnum = nullptr;
    if (FAILED(mgr->GetSessionEnumerator(&sessionEnum)) || !sessionEnum) {
        mgr->Release();
        defaultDev->Release();
        enumerator->Release();
        if (didInit) CoUninitialize();
        std::cout << "[]\n";
        return 0;
    }

    int count = 0;
    sessionEnum->GetCount(&count);

    struct ActiveApp {
        DWORD pid;
        float peak;
        std::string appName;
        std::string exeName;
        std::string windowTitle;
    };
    std::vector<ActiveApp> activeApps;

    for (int i = 0; i < count; ++i) {
        IAudioSessionControl* ctrl = nullptr;
        if (FAILED(sessionEnum->GetSession(i, &ctrl)) || !ctrl) continue;

        AudioSessionState state = AudioSessionStateInactive;
        ctrl->GetState(&state);

        if (state == AudioSessionStateActive) {
            float peak = 0.0f;
            IAudioMeterInformation* meter = nullptr;
            if (SUCCEEDED(ctrl->QueryInterface(__uuidof(IAudioMeterInformation),
                                               reinterpret_cast<void**>(&meter))) && meter) {
                meter->GetPeakValue(&peak);
                meter->Release();
            }

            IAudioSessionControl2* ctrl2 = nullptr;
            if (SUCCEEDED(ctrl->QueryInterface(__uuidof(IAudioSessionControl2),
                                               reinterpret_cast<void**>(&ctrl2))) && ctrl2) {
                DWORD pid = 0;
                if (SUCCEEDED(ctrl2->GetProcessId(&pid)) && pid != 0) {
                    std::wstring fullPath;
                    std::string exeName = "Unknown";
                    HANDLE hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
                    if (hProc) {
                        wchar_t pathBuf[MAX_PATH] = {};
                        DWORD pathLen = MAX_PATH;
                        if (QueryFullProcessImageNameW(hProc, 0, pathBuf, &pathLen)) {
                            fullPath.assign(pathBuf, pathLen);
                            const size_t pos = fullPath.find_last_of(L'\\');
                            std::wstring fname = (pos != std::wstring::npos) ? fullPath.substr(pos + 1) : fullPath;
                            exeName = wideToUtf8(fname.c_str());
                        }
                        CloseHandle(hProc);
                    }

                    std::string lowerExe = exeName;
                    std::transform(lowerExe.begin(), lowerExe.end(), lowerExe.begin(), [](unsigned char c) {
                        return static_cast<char>(std::tolower(c));
                    });

                    // Skip Windows shell / explorer system sound noise
                    if (!isIgnoredSystemProcess(lowerExe)) {
                        // Deduplicate by exeName (merges multi-process browser audio subprocesses!)
                        auto it = std::find_if(activeApps.begin(), activeApps.end(), [&](const ActiveApp& a) {
                            std::string aLower = a.exeName;
                            std::transform(aLower.begin(), aLower.end(), aLower.begin(), [](unsigned char c) {
                                return static_cast<char>(std::tolower(c));
                            });
                            return aLower == lowerExe;
                        });

                        if (it != activeApps.end()) {
                            it->peak = std::max(it->peak, peak);
                        } else {
                            // First try exact PID, then fallback to matching executable name (for Chrome/Edge audio subprocesses!)
                            WindowSearchCtx wctx{pid, {}, {}};
                            EnumWindows(enumWindowsForPidOrExe, reinterpret_cast<LPARAM>(&wctx));
                            if (wctx.bestTitle.empty()) {
                                std::wstring wLower(lowerExe.begin(), lowerExe.end());
                                WindowSearchCtx exeCtx{0, wLower, {}};
                                EnumWindows(enumWindowsForPidOrExe, reinterpret_cast<LPARAM>(&exeCtx));
                                wctx.bestTitle = std::move(exeCtx.bestTitle);
                            }

                            activeApps.push_back(ActiveApp{
                                pid,
                                peak,
                                getFriendlyAppName(exeName, fullPath),
                                exeName,
                                wctx.bestTitle
                            });
                        }
                    }
                }
                ctrl2->Release();
            }
        }
        ctrl->Release();
    }

    // Sort: apps actively outputting audio (peak > 0.0001) first, highest peak first
    std::stable_sort(activeApps.begin(), activeApps.end(), [](const ActiveApp& a, const ActiveApp& b) {
        const bool aSound = a.peak > 0.0001f;
        const bool bSound = b.peak > 0.0001f;
        if (aSound != bSound) return aSound > bSound;
        return a.peak > b.peak;
    });

    std::cout << "[\n";
    for (size_t i = 0; i < activeApps.size(); ++i) {
        const auto& a = activeApps[i];
        std::cout << "  {"
                  << "\"pid\":" << a.pid << ","
                  << "\"peak\":" << a.peak << ","
                  << "\"appName\":\"" << jsonEscape(a.appName) << "\","
                  << "\"exeName\":\"" << jsonEscape(a.exeName) << "\","
                  << "\"windowTitle\":\"" << jsonEscape(a.windowTitle) << "\""
                  << "}" << (i + 1 < activeApps.size() ? "," : "") << "\n";
    }
    std::cout << "]\n";

    sessionEnum->Release();
    mgr->Release();
    defaultDev->Release();
    enumerator->Release();
    if (didInit) CoUninitialize();
    return 0;
}

int cmdListDevices() {
    HRESULT hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    const bool didInit = SUCCEEDED(hr);

    IMMDeviceEnumerator* enumerator = nullptr;
    hr = CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_ALL,
                          __uuidof(IMMDeviceEnumerator), reinterpret_cast<void**>(&enumerator));
    if (FAILED(hr) || !enumerator) {
        std::cerr << "{\"error\":\"Failed to create MMDeviceEnumerator\"}\n";
        if (didInit) CoUninitialize();
        return 1;
    }

    std::string defaultId;
    IMMDevice* defaultDev = nullptr;
    if (SUCCEEDED(enumerator->GetDefaultAudioEndpoint(eRender, eMultimedia, &defaultDev)) && defaultDev) {
        LPWSTR idStr = nullptr;
        if (SUCCEEDED(defaultDev->GetId(&idStr)) && idStr) {
            defaultId = wideToUtf8(idStr);
            CoTaskMemFree(idStr);
        }
        defaultDev->Release();
    }

    IMMDeviceCollection* collection = nullptr;
    hr = enumerator->EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE, &collection);
    if (FAILED(hr) || !collection) {
        enumerator->Release();
        if (didInit) CoUninitialize();
        std::cout << "[]\n";
        return 0;
    }

    UINT count = 0;
    collection->GetCount(&count);

    std::cout << "[\n";
    for (UINT i = 0; i < count; ++i) {
        IMMDevice* dev = nullptr;
        if (FAILED(collection->Item(i, &dev)) || !dev) continue;

        std::string id;
        LPWSTR idStr = nullptr;
        if (SUCCEEDED(dev->GetId(&idStr)) && idStr) {
            id = wideToUtf8(idStr);
            CoTaskMemFree(idStr);
        }

        std::string name = "Unknown Audio Device";
        uint32_t sampleRate = 0;
        uint32_t channels = 0;
        uint32_t bitsPerSample = 0;

        IPropertyStore* props = nullptr;
        if (SUCCEEDED(dev->OpenPropertyStore(STGM_READ, &props)) && props) {
            PROPVARIANT varName;
            PropVariantInit(&varName);
            if (SUCCEEDED(props->GetValue(PKEY_Device_FriendlyName, &varName)) && varName.vt == VT_LPWSTR) {
                name = wideToUtf8(varName.pwszVal);
            }
            PropVariantClear(&varName);

            // Read true hardware device output format (exact 16/24/32-bit & kHz set in Windows Sound)
            PROPVARIANT varFmt;
            PropVariantInit(&varFmt);
            if (SUCCEEDED(props->GetValue(PKEY_AudioEngine_DeviceFormat, &varFmt)) &&
                varFmt.vt == VT_BLOB && varFmt.blob.cbSize >= sizeof(WAVEFORMATEX) && varFmt.blob.pBlobData) {
                const auto* wfx = reinterpret_cast<const WAVEFORMATEX*>(varFmt.blob.pBlobData);
                sampleRate = wfx->nSamplesPerSec;
                channels = wfx->nChannels;
                bitsPerSample = wfx->wBitsPerSample;
                if (wfx->wFormatTag == WAVE_FORMAT_EXTENSIBLE && varFmt.blob.cbSize >= sizeof(WAVEFORMATEXTENSIBLE)) {
                    const auto* wfex = reinterpret_cast<const WAVEFORMATEXTENSIBLE*>(varFmt.blob.pBlobData);
                    if (wfex->Samples.wValidBitsPerSample > 0) {
                        bitsPerSample = wfex->Samples.wValidBitsPerSample;
                    }
                }
            }
            PropVariantClear(&varFmt);
            props->Release();
        }

        // Fallback to WASAPI mix format only if PKEY_AudioEngine_DeviceFormat was unavailable
        if (sampleRate == 0) {
            IAudioClient* audioClient = nullptr;
            if (SUCCEEDED(dev->Activate(__uuidof(IAudioClient), CLSCTX_ALL, nullptr,
                                        reinterpret_cast<void**>(&audioClient))) && audioClient) {
                WAVEFORMATEX* mixFmt = nullptr;
                if (SUCCEEDED(audioClient->GetMixFormat(&mixFmt)) && mixFmt) {
                    sampleRate = mixFmt->nSamplesPerSec;
                    channels = mixFmt->nChannels;
                    bitsPerSample = mixFmt->wBitsPerSample;
                    CoTaskMemFree(mixFmt);
                }
                audioClient->Release();
            }
        }

        const bool isDefault = (!defaultId.empty() && id == defaultId);
        const std::string guid = extractGuidFromEndpointId(id);

        std::cout << "  {"
                  << "\"id\":\"" << jsonEscape(id) << "\","
                  << "\"guid\":\"" << jsonEscape(guid) << "\","
                  << "\"name\":\"" << jsonEscape(name) << "\","
                  << "\"sampleRate\":" << sampleRate << ","
                  << "\"channels\":" << channels << ","
                  << "\"bitsPerSample\":" << bitsPerSample << ","
                  << "\"isDefault\":" << (isDefault ? "true" : "false")
                  << "}" << (i + 1 < count ? "," : "") << "\n";

        dev->Release();
    }
    std::cout << "]\n";

    collection->Release();
    enumerator->Release();
    if (didInit) CoUninitialize();
    return 0;
}

void printParamsJson(const viper::DspParams& p) {
    std::cout << "{"
              << "\"sequence\":" << p.sequence << ","
              << "\"masterEnabled\":" << (p.masterEnabled ? "true" : "false") << ","
              << "\"masterGainDb\":" << p.masterGainDb << ","
              << "\"outputPan\":" << p.outputPan << ","
              << "\"eqEnabled\":" << (p.eqEnabled ? "true" : "false") << ","
              << "\"eqBandsDb\":[";
    for (size_t i = 0; i < viper::kEqBandCount; ++i) {
        std::cout << p.eqBandsDb[i] << (i + 1 < viper::kEqBandCount ? "," : "");
    }
    std::cout << "],"
              << "\"bassEnabled\":" << (p.bassEnabled ? "true" : "false") << ","
              << "\"bassMode\":" << static_cast<uint32_t>(p.bassMode) << ","
              << "\"bassFrequencyHz\":" << p.bassFrequencyHz << ","
              << "\"bassGainDb\":" << p.bassGainDb << ","
              << "\"clarityEnabled\":" << (p.clarityEnabled ? "true" : "false") << ","
              << "\"clarityMode\":" << static_cast<uint32_t>(p.clarityMode) << ","
              << "\"clarityGainDb\":" << p.clarityGainDb << ","
              << "\"tubeEnabled\":" << (p.tubeEnabled ? "true" : "false") << ","
              << "\"tubeDrive\":" << p.tubeDrive << ","
              << "\"surroundEnabled\":" << (p.surroundEnabled ? "true" : "false") << ","
              << "\"surroundDelayMs\":" << p.surroundDelayMs << ","
              << "\"stereoWidth\":" << p.stereoWidth << ","
              << "\"limiterEnabled\":" << (p.limiterEnabled ? "true" : "false") << ","
              << "\"limiterThresholdDb\":" << p.limiterThresholdDb
              << "}\n";
}

int cmdGetParams() {
    viper::SharedConfig cfg;
    if (!cfg.open(true)) {
        std::cerr << "{\"error\":\"Failed to open SharedConfig\"}\n";
        return 1;
    }
    viper::DspParams p{};
    if (!cfg.readSnapshot(p)) {
        p = viper::DspParams{};
    }
    printParamsJson(p);
    return 0;
}

int cmdSetParams(int argc, char** argv) {
    viper::SharedConfig cfg;
    if (!cfg.open(true)) {
        std::cerr << "{\"error\":\"Failed to open SharedConfig for writing\"}\n";
        return 1;
    }

    viper::DspParams p{};
    if (!cfg.readSnapshot(p)) {
        p = viper::DspParams{};
    }

    for (int i = 2; i + 1 < argc; i += 2) {
        const std::string key = argv[i];
        const std::string val = argv[i + 1];

        if (key == "--master") p.masterEnabled = (val == "1" || val == "true") ? 1u : 0u;
        else if (key == "--gain") p.masterGainDb = std::stof(val);
        else if (key == "--pan") p.outputPan = std::stof(val);
        else if (key == "--eq") p.eqEnabled = (val == "1" || val == "true") ? 1u : 0u;
        else if (key == "--eq-bands") {
            std::stringstream ss(val);
            std::string item;
            size_t idx = 0;
            while (std::getline(ss, item, ',') && idx < viper::kEqBandCount) {
                p.eqBandsDb[idx++] = std::stof(item);
            }
        }
        else if (key == "--bass") p.bassEnabled = (val == "1" || val == "true") ? 1u : 0u;
        else if (key == "--bass-mode") p.bassMode = static_cast<viper::BassMode>(std::stoul(val));
        else if (key == "--bass-freq") p.bassFrequencyHz = std::stof(val);
        else if (key == "--bass-gain") p.bassGainDb = std::stof(val);
        else if (key == "--clarity") p.clarityEnabled = (val == "1" || val == "true") ? 1u : 0u;
        else if (key == "--clarity-mode") p.clarityMode = static_cast<viper::ClarityMode>(std::stoul(val));
        else if (key == "--clarity-gain") p.clarityGainDb = std::stof(val);
        else if (key == "--tube") p.tubeEnabled = (val == "1" || val == "true") ? 1u : 0u;
        else if (key == "--tube-drive") p.tubeDrive = std::stof(val);
        else if (key == "--surround") p.surroundEnabled = (val == "1" || val == "true") ? 1u : 0u;
        else if (key == "--surround-delay") p.surroundDelayMs = std::stof(val);
        else if (key == "--stereo-width") p.stereoWidth = std::stof(val);
        else if (key == "--limiter") p.limiterEnabled = (val == "1" || val == "true") ? 1u : 0u;
        else if (key == "--limiter-thresh") p.limiterThresholdDb = std::stof(val);
    }

    if (!cfg.writeParams(p)) {
        std::cerr << "{\"error\":\"Failed to write SharedConfig\"}\n";
        return 1;
    }

    (void)cfg.readSnapshot(p);
    printParamsJson(p);
    return 0;
}

} // namespace

int main(int argc, char** argv) {
    if (argc < 2) {
        std::cout << "Usage:\n"
                  << "  viper_ctl devices\n"
                  << "  viper_ctl get\n"
                  << "  viper_ctl set [--master 0|1] [--gain dB] [--bass 0|1] [--bass-gain dB] ...\n";
        return 0;
    }

    const std::string cmd = argv[1];
    if (cmd == "devices") return cmdListDevices();
    if (cmd == "sessions") return cmdListSessions();
    if (cmd == "get") return cmdGetParams();
    if (cmd == "set") return cmdSetParams(argc, argv);

    std::cerr << "Unknown command: " << cmd << "\n";
    return 1;
}
