#include "ViPERHiResAPO.hpp"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <span>

namespace viper::apo {

std::atomic<long> g_dllRefCount{0};

namespace {

// Registration properties for Windows Audio Engine (SFX/MFX/EFX compatible)
struct ApoRegPropsHolder {
    APO_REG_PROPERTIES props{};

    ApoRegPropsHolder() noexcept {
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
        props.u32NumAPOInterfaces = 1;
        props.iidAPOInterfaceList[0] = __uuidof(IAudioProcessingObject);
    }
};

} // namespace

const APO_REG_PROPERTIES& getApoRegProperties() noexcept {
    static const ApoRegPropsHolder holder{};
    return holder.props;
}

ViPERHiResAPO::ViPERHiResAPO()
    : CBaseAudioProcessingObject(&getApoRegProperties()) {
    g_dllRefCount.fetch_add(1, std::memory_order_relaxed);
}

ViPERHiResAPO::~ViPERHiResAPO() {
    sharedConfig_.close();
    g_dllRefCount.fetch_sub(1, std::memory_order_relaxed);
}

STDMETHODIMP ViPERHiResAPO::QueryInterface(REFIID riid, void** ppvObject) {
    if (!ppvObject) {
        return E_POINTER;
    }

    if (riid == __uuidof(IUnknown) || riid == __uuidof(IAudioProcessingObject)) {
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

STDMETHODIMP_(ULONG) ViPERHiResAPO::AddRef() {
    return refCount_.fetch_add(1, std::memory_order_relaxed) + 1;
}

STDMETHODIMP_(ULONG) ViPERHiResAPO::Release() {
    const ULONG count = refCount_.fetch_sub(1, std::memory_order_acq_rel) - 1;
    if (count == 0) {
        delete this;
    }
    return count;
}

STDMETHODIMP ViPERHiResAPO::Initialize(UINT32 cbDataSize, BYTE* pbyData) {
    if (!pbyData && cbDataSize > 0) {
        return E_POINTER;
    }

    if (pbyData && cbDataSize >= sizeof(APOInitBaseStruct)) {
        const auto* initBase = reinterpret_cast<const APOInitBaseStruct*>(pbyData);
        if (initBase->clsid != CLSID_ViPERHiResAPO) {
            return APOERR_INVALID_APO_CLSID;
        }
    }

    // Attempt to open shared config early (will also retry on LockForProcess if not created yet)
    if (sharedConfig_.open(false)) {
        if (sharedConfig_.readSnapshot(cachedParams_)) {
            lastConfigSeq_ = 0;
        }
    }

    m_bIsInitialized = TRUE;
    return S_OK;
}

STDMETHODIMP ViPERHiResAPO::IsInputFormatSupported(
    IAudioMediaType* pOppositeFormat,
    IAudioMediaType* pRequestedInputFormat,
    IAudioMediaType** ppSupportedInputFormat) {
    if (!pRequestedInputFormat) {
        return E_POINTER;
    }
    if (ppSupportedInputFormat) {
        *ppSupportedInputFormat = nullptr;
    }

    UNCOMPRESSEDAUDIOFORMAT requested{};
    HRESULT hr = pRequestedInputFormat->GetUncompressedAudioFormat(&requested);
    if (FAILED(hr)) {
        return APOERR_FORMAT_NOT_SUPPORTED;
    }

    // Check if opposite format imposes constraints
    if (pOppositeFormat) {
        UNCOMPRESSEDAUDIOFORMAT opposite{};
        if (SUCCEEDED(pOppositeFormat->GetUncompressedAudioFormat(&opposite))) {
            if (opposite.dwSamplesPerFrame != requested.dwSamplesPerFrame ||
                std::abs(opposite.fFramesPerSecond - requested.fFramesPerSecond) > 1.0f) {
                return APOERR_FORMAT_NOT_SUPPORTED;
            }
        }
    }

    // Accept Stereo (2 ch) or pass-through multichannel; Hi-Res sample rates 44.1kHz - 384kHz
    const float clampedSr = std::clamp(
        requested.fFramesPerSecond,
        static_cast<float>(kMinSampleRate),
        static_cast<float>(kMaxSampleRate));

    const bool isFloat32 =
        (requested.guidFormatType == KSDATAFORMAT_SUBTYPE_IEEE_FLOAT) &&
        (requested.dwBytesPerSampleContainer == 4) &&
        (requested.dwValidBitsPerSample == 32);

    const bool isRateSupported =
        (requested.fFramesPerSecond >= static_cast<float>(kMinSampleRate)) &&
        (requested.fFramesPerSecond <= static_cast<float>(kMaxSampleRate));

    const bool isChannelsSupported = (requested.dwSamplesPerFrame >= 1 && requested.dwSamplesPerFrame <= 8);

    if (isFloat32 && isRateSupported && isChannelsSupported) {
        if (ppSupportedInputFormat) {
            *ppSupportedInputFormat = pRequestedInputFormat;
            pRequestedInputFormat->AddRef();
        }
        return S_OK;
    }

    // Suggest 32-bit IEEE float at the clamped Hi-Res sample rate
    if (ppSupportedInputFormat) {
        UNCOMPRESSEDAUDIOFORMAT preferred = requested;
        preferred.guidFormatType = KSDATAFORMAT_SUBTYPE_IEEE_FLOAT;
        preferred.dwBytesPerSampleContainer = 4;
        preferred.dwValidBitsPerSample = 32;
        preferred.fFramesPerSecond = clampedSr;
        preferred.dwSamplesPerFrame = std::clamp<DWORD>(requested.dwSamplesPerFrame, 1ul, 8ul);

        hr = CreateAudioMediaTypeFromUncompressedAudioFormat(&preferred, ppSupportedInputFormat);
        if (FAILED(hr)) {
            return APOERR_FORMAT_NOT_SUPPORTED;
        }
        return S_FALSE;
    }

    return APOERR_FORMAT_NOT_SUPPORTED;
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

    // Prepare Hi-Res DSP engine for exact negotiated sample rate (e.g. 48k, 96k, 192k, 384k)
    engine_.prepare(sampleRate_);

    if (!sharedConfig_.isOpen()) {
        (void)sharedConfig_.open(false);
    }
    if (sharedConfig_.isOpen() && sharedConfig_.readSnapshot(cachedParams_)) {
        engine_.setParams(cachedParams_);
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

    // Lock-free poll for real-time parameter updates from UI
    if (sharedConfig_.isOpen() && sharedConfig_.pollChanges(lastConfigSeq_, cachedParams_)) {
        engine_.setParams(cachedParams_);
    }

    // Process stereo Hi-Res DSP (44.1kHz - 384kHz, 32-bit float)
    if (channelCount_ == 2) {
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
