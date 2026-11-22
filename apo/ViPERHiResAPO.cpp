#include "ViPERHiResAPO.hpp"

#include <algorithm>
#include <cmath>
#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <span>

namespace viper::apo {

std::atomic<long> g_dllRefCount{0};

namespace {

void traceLog(const char* fmt, ...) noexcept {
    FILE* f = nullptr;
    if (_wfopen_s(&f, L"C:\\ProgramData\\ViPERHiRes\\apo_trace.txt", L"a") == 0 && f) {
        va_list args;
        va_start(args, fmt);
        std::vfprintf(f, fmt, args);
        va_end(args);
        std::fputc('\n', f);
        std::fclose(f);
    }
}

// Windows Audio Engine requires all 3 core APO interfaces in APO_REG_PROPERTIES!
#pragma pack(push, 1)
struct ApoRegProps3 {
    APO_REG_PROPERTIES props{};
    IID extraInterfaces[2]{};

    ApoRegProps3() noexcept {
        props.clsid = CLSID_ViPERHiResAPO;
        props.Flags = static_cast<APO_FLAG>(
            APO_FLAG_INPLACE |
            APO_FLAG_SAMPLESPERFRAME_MUST_MATCH |
            APO_FLAG_FRAMESPERSECOND_MUST_MATCH |
            APO_FLAG_BITSPERSAMPLE_MUST_MATCH);
        wcscpy_s(props.szFriendlyName, kApoFriendlyName);
        wcscpy_s(props.szCopyrightInfo, L"ViPERHiRes Open Project");
        props.u32MajorVersion = 1;
        props.u32MinorVersion = 0;
        props.u32MinInputConnections = 1;
        props.u32MaxInputConnections = 1;
        props.u32MinOutputConnections = 1;
        props.u32MaxOutputConnections = 1;
        props.u32MaxInstances = 0xFFFFFFFF;
        props.u32NumAPOInterfaces = 3;
        props.iidAPOInterfaceList[0] = __uuidof(IAudioProcessingObject);
        extraInterfaces[0] = __uuidof(IAudioProcessingObjectRT);
        extraInterfaces[1] = __uuidof(IAudioProcessingObjectConfiguration);
    }
};
#pragma pack(pop)

HRESULT checkFormatSupported(
    IAudioMediaType* pOppositeFormat,
    IAudioMediaType* pRequestedFormat,
    IAudioMediaType** ppSupportedFormat) {
    if (!pRequestedFormat) {
        return E_POINTER;
    }
    if (ppSupportedFormat) {
        *ppSupportedFormat = nullptr;
    }

    UNCOMPRESSEDAUDIOFORMAT requested{};
    HRESULT hr = pRequestedFormat->GetUncompressedAudioFormat(&requested);
    if (FAILED(hr)) {
        return APOERR_FORMAT_NOT_SUPPORTED;
    }

    UNCOMPRESSEDAUDIOFORMAT target = requested;
    if (pOppositeFormat) {
        UNCOMPRESSEDAUDIOFORMAT opposite{};
        if (SUCCEEDED(pOppositeFormat->GetUncompressedAudioFormat(&opposite))) {
            target.dwSamplesPerFrame = opposite.dwSamplesPerFrame;
            target.fFramesPerSecond = opposite.fFramesPerSecond;
        }
    }

    // Accept 8 kHz .. 384 kHz (1..8 ch) so telephony / Bluetooth HFP / mono devices never mute;
    // DSP runs on 44.1-384 kHz Stereo and transparently passes through all other formats.
    constexpr float kPassThroughMinSampleRate = 8000.0f;
    const float clampedSr = std::clamp(
        target.fFramesPerSecond,
        kPassThroughMinSampleRate,
        static_cast<float>(kMaxSampleRate));
    const DWORD clampedCh = std::clamp<DWORD>(target.dwSamplesPerFrame, 1ul, 8ul);

    const bool isExactMatch =
        (requested.guidFormatType == KSDATAFORMAT_SUBTYPE_IEEE_FLOAT) &&
        (requested.dwBytesPerSampleContainer == 4) &&
        (requested.dwValidBitsPerSample == 32) &&
        (std::abs(requested.fFramesPerSecond - clampedSr) < 1.0f) &&
        (requested.dwSamplesPerFrame == clampedCh);

    if (isExactMatch) {
        if (ppSupportedFormat) {
            *ppSupportedFormat = pRequestedFormat;
            pRequestedFormat->AddRef();
        }
        return S_OK;
    }

    if (ppSupportedFormat) {
        UNCOMPRESSEDAUDIOFORMAT preferred = requested;
        preferred.guidFormatType = KSDATAFORMAT_SUBTYPE_IEEE_FLOAT;
        preferred.dwBytesPerSampleContainer = 4;
        preferred.dwValidBitsPerSample = 32;
        preferred.fFramesPerSecond = clampedSr;
        preferred.dwSamplesPerFrame = clampedCh;

        hr = CreateAudioMediaTypeFromUncompressedAudioFormat(&preferred, ppSupportedFormat);
        if (FAILED(hr)) {
            return APOERR_FORMAT_NOT_SUPPORTED;
        }
        return S_FALSE;
    }

    return APOERR_FORMAT_NOT_SUPPORTED;
}

} // namespace

const APO_REG_PROPERTIES& getApoRegProperties() noexcept {
    static const ApoRegProps3 holder{};
    return holder.props;
}

ViPERHiResAPO::ViPERHiResAPO(IUnknown* pUnkOuter)
    : CBaseAudioProcessingObject(&getApoRegProperties()),
      outerUnknown_(pUnkOuter) {
    g_dllRefCount.fetch_add(1, std::memory_order_relaxed);
    traceLog("Ctor called (outer=%p)", static_cast<void*>(pUnkOuter));
}

ViPERHiResAPO::~ViPERHiResAPO() {
    sharedConfig_.close();
    g_dllRefCount.fetch_sub(1, std::memory_order_relaxed);
}

STDMETHODIMP ViPERHiResAPO::nonDelegatingQueryInterface(REFIID riid, void** ppvObject) {
    if (!ppvObject) {
        return E_POINTER;
    }

    if (riid == __uuidof(IUnknown)) {
        *ppvObject = static_cast<IUnknown*>(&nonDelegatingUnknown_);
        nonDelegatingAddRef();
        return S_OK;
    }

    if (riid == __uuidof(IAudioProcessingObject)) {
        *ppvObject = static_cast<IAudioProcessingObject*>(this);
    } else if (riid == __uuidof(IAudioProcessingObjectRT)) {
        *ppvObject = static_cast<IAudioProcessingObjectRT*>(this);
    } else if (riid == __uuidof(IAudioProcessingObjectConfiguration)) {
        *ppvObject = static_cast<IAudioProcessingObjectConfiguration*>(this);
    } else if (riid == __uuidof(IAudioSystemEffects)) {
        *ppvObject = static_cast<IAudioSystemEffects*>(this);
    } else if (riid == __uuidof(IAudioSystemEffects2)) {
        *ppvObject = static_cast<IAudioSystemEffects2*>(this);
    } else {
        *ppvObject = nullptr;
        return E_NOINTERFACE;
    }

    AddRef();
    return S_OK;
}

ULONG ViPERHiResAPO::nonDelegatingAddRef() {
    return refCount_.fetch_add(1, std::memory_order_relaxed) + 1;
}

ULONG ViPERHiResAPO::nonDelegatingRelease() {
    const ULONG count = refCount_.fetch_sub(1, std::memory_order_acq_rel) - 1;
    if (count == 0) {
        delete this;
    }
    return count;
}

STDMETHODIMP ViPERHiResAPO::QueryInterface(REFIID riid, void** ppvObject) {
    if (outerUnknown_) {
        return outerUnknown_->QueryInterface(riid, ppvObject);
    }
    return nonDelegatingQueryInterface(riid, ppvObject);
}

STDMETHODIMP_(ULONG) ViPERHiResAPO::AddRef() {
    if (outerUnknown_) {
        return outerUnknown_->AddRef();
    }
    return nonDelegatingAddRef();
}

STDMETHODIMP_(ULONG) ViPERHiResAPO::Release() {
    if (outerUnknown_) {
        return outerUnknown_->Release();
    }
    return nonDelegatingRelease();
}

STDMETHODIMP ViPERHiResAPO::Initialize(UINT32 cbDataSize, BYTE* pbyData) {
    if (!pbyData && cbDataSize > 0) {
        return E_POINTER;
    }

    // Note: APOInitBaseStruct::clsid identifies the init struct type
    // (APOInitSystemEffects / APOInitSystemEffects2 / APOInitSystemEffects3),
    // NOT CLSID_ViPERHiResAPO! Accept all standard Windows 10/11 init structs.
    if (pbyData && cbDataSize < sizeof(APOInitBaseStruct)) {
        return E_INVALIDARG;
    }

    // Attempt to open shared config early (will also retry on LockForProcess if not created yet)
    if (sharedConfig_.open(false)) {
        if (sharedConfig_.readSnapshot(cachedParams_)) {
            lastConfigSeq_ = 0;
        }
    }

    m_bIsInitialized = TRUE;
    traceLog("Initialize OK (cb=%u, ipc=%d)", cbDataSize, sharedConfig_.isOpen() ? 1 : 0);
    return S_OK;
}

STDMETHODIMP ViPERHiResAPO::IsInputFormatSupported(
    IAudioMediaType* pOppositeFormat,
    IAudioMediaType* pRequestedInputFormat,
    IAudioMediaType** ppSupportedInputFormat) {
    const HRESULT hr = checkFormatSupported(pOppositeFormat, pRequestedInputFormat, ppSupportedInputFormat);
    traceLog("IsInputFormatSupported -> 0x%08lX", static_cast<unsigned long>(hr));
    return hr;
}

STDMETHODIMP ViPERHiResAPO::IsOutputFormatSupported(
    IAudioMediaType* pOppositeFormat,
    IAudioMediaType* pRequestedOutputFormat,
    IAudioMediaType** ppSupportedOutputFormat) {
    const HRESULT hr = checkFormatSupported(pOppositeFormat, pRequestedOutputFormat, ppSupportedOutputFormat);
    traceLog("IsOutputFormatSupported -> 0x%08lX", static_cast<unsigned long>(hr));
    return hr;
}

STDMETHODIMP ViPERHiResAPO::LockForProcess(
    UINT32 u32NumInputConnections,
    APO_CONNECTION_DESCRIPTOR** ppInputConnections,
    UINT32 u32NumOutputConnections,
    APO_CONNECTION_DESCRIPTOR** ppOutputConnections) {
    HRESULT hr = CBaseAudioProcessingObject::LockForProcess(
        u32NumInputConnections,
        ppInputConnections,
        u32NumOutputConnections,
        ppOutputConnections);
    traceLog("Base LockForProcess -> 0x%08lX", static_cast<unsigned long>(hr));
    if (FAILED(hr)) {
        return hr;
    }

    if (u32NumInputConnections > 0 && ppInputConnections && ppInputConnections[0] && ppInputConnections[0]->pFormat) {
        UNCOMPRESSEDAUDIOFORMAT fmt{};
        if (SUCCEEDED(ppInputConnections[0]->pFormat->GetUncompressedAudioFormat(&fmt))) {
            sampleRate_ = static_cast<uint32_t>(std::lround(fmt.fFramesPerSecond));
            channelCount_ = fmt.dwSamplesPerFrame;
        }
    }

    // Enable Hi-Res DSP only for 44.1-384 kHz Stereo; pass through all other formats (e.g. Bluetooth HFP 8/16 kHz Mono)
    dspEligible_ = (channelCount_ == 2) &&
                   (sampleRate_ >= kMinSampleRate) &&
                   (sampleRate_ <= kMaxSampleRate);

    if (dspEligible_) {
        engine_.prepare(sampleRate_);
    }

    if (!sharedConfig_.isOpen()) {
        (void)sharedConfig_.open(false);
    }
    if (sharedConfig_.isOpen() && sharedConfig_.readSnapshot(cachedParams_)) {
        if (dspEligible_) {
            engine_.setParams(cachedParams_);
        }
    }

    // Write lightweight non-RT diagnostic line so we can verify active sample rate & IPC status
    if (FILE* f = nullptr; _wfopen_s(&f, L"C:\\ProgramData\\ViPERHiRes\\apo_diag.txt", L"w") == 0 && f) {
        std::fprintf(f, "LockForProcess OK | SR=%u Hz | Ch=%u | DSP=%d | IPC=%d | Seq=%u | Master=%u | EQ=%u | Bass=%u\n",
                     sampleRate_, channelCount_, dspEligible_ ? 1 : 0, sharedConfig_.isOpen() ? 1 : 0,
                     cachedParams_.sequence, cachedParams_.masterEnabled,
                     cachedParams_.eqEnabled, cachedParams_.bassEnabled);
        std::fclose(f);
    }

    return S_OK;
}

STDMETHODIMP ViPERHiResAPO::UnlockForProcess() {
    engine_.reset();
    return CBaseAudioProcessingObject::UnlockForProcess();
}

STDMETHODIMP_(void) ViPERHiResAPO::APOProcess(
    UINT32 u32NumInputConnections,
    APO_CONNECTION_PROPERTY** ppInputConnections,
    UINT32 u32NumOutputConnections,
    APO_CONNECTION_PROPERTY** ppOutputConnections) {
    if (u32NumInputConnections == 0 || u32NumOutputConnections == 0 ||
        !ppInputConnections || !ppOutputConnections ||
        !ppInputConnections[0] || !ppOutputConnections[0]) {
        return;
    }

    APO_CONNECTION_PROPERTY* inConn = ppInputConnections[0];
    APO_CONNECTION_PROPERTY* outConn = ppOutputConnections[0];

    const UINT32 validFrames = inConn->u32ValidFrameCount;
    outConn->u32ValidFrameCount = validFrames;
    outConn->u32BufferFlags = inConn->u32BufferFlags;

    if (validFrames == 0) {
        return;
    }

    const size_t totalSamples = static_cast<size_t>(validFrames) * channelCount_;
    const auto* inSamples = reinterpret_cast<const float*>(inConn->pBuffer);
    auto* outSamples = reinterpret_cast<float*>(outConn->pBuffer);

    if (inConn->u32BufferFlags == BUFFER_SILENT) {
        if (outSamples) {
            std::memset(outSamples, 0, totalSamples * sizeof(float));
        }
        return;
    }

    if (!inSamples || !outSamples) {
        return;
    }

    // Copy input -> output if not running in-place
    if (inSamples != outSamples) {
        std::memcpy(outSamples, inSamples, totalSamples * sizeof(float));
    }

    // Process stereo Hi-Res DSP (44.1kHz - 384kHz, 32-bit float); pass-through untouched otherwise
    if (dspEligible_) {
        if (sharedConfig_.isOpen() && sharedConfig_.pollChanges(lastConfigSeq_, cachedParams_)) {
            engine_.setParams(cachedParams_);
        }
        engine_.processInterleavedStereo(std::span<float>{outSamples, totalSamples});
    }
}

STDMETHODIMP ViPERHiResAPO::GetEffectsList(
    LPGUID* ppEffectsIds,
    UINT* pcEffects,
    HANDLE /*Event*/) {
    if (!ppEffectsIds || !pcEffects) {
        return E_POINTER;
    }

    *ppEffectsIds = nullptr;
    *pcEffects = 0;

    if (cachedParams_.masterEnabled) {
        auto* guids = static_cast<LPGUID>(CoTaskMemAlloc(sizeof(GUID)));
        if (!guids) {
            return E_OUTOFMEMORY;
        }
        // Official Microsoft AUDIO_EFFECT_TYPE_OTHER GUID
        static constexpr GUID kAudioEffectTypeOther = {
            0x9b2e82f9, 0xe09d, 0x4f31,
            { 0x93, 0xb5, 0xa0, 0x37, 0x86, 0xb5, 0xd5, 0xa6 }
        };
        guids[0] = kAudioEffectTypeOther;
        *ppEffectsIds = guids;
        *pcEffects = 1;
    }

    return S_OK;
}

} // namespace viper::apo
