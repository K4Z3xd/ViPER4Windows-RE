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
        IPropertyStore* props = nullptr;
        if (SUCCEEDED(dev->OpenPropertyStore(STGM_READ, &props)) && props) {
            PROPVARIANT varName;
            PropVariantInit(&varName);
            if (SUCCEEDED(props->GetValue(PKEY_Device_FriendlyName, &varName)) && varName.vt == VT_LPWSTR) {
                name = wideToUtf8(varName.pwszVal);
            }
            PropVariantClear(&varName);
            props->Release();
        }

        uint32_t sampleRate = 0;
        uint32_t channels = 0;
        uint32_t bitsPerSample = 0;
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
    if (cmd == "get") return cmdGetParams();
    if (cmd == "set") return cmdSetParams(argc, argv);

    std::cerr << "Unknown command: " << cmd << "\n";
    return 1;
}
