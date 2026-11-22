#pragma once

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef NOMINMAX
#define NOMINMAX
#endif

#include <windows.h>
#include <audioclient.h>
#include <audioenginebaseapo.h>
#include <audioengineextensionapo.h>
#include <audiomediatype.h>
#include <baseaudioprocessingobject.h>
#include <ks.h>
#include <ksmedia.h>

#include "viper/SharedConfig.hpp"
#include "viper/ViperEngine.hpp"

#include <atomic>
#include <cstdint>
#include <string>

namespace viper::apo {

// Resolves <InstallRoot>\logs\<filename> (from C:\ProgramData\ViPERHiRes\logs_dir.txt),
// falling back to C:\ProgramData\ViPERHiRes\<filename> in dev/standalone mode.
std::wstring getApoLogPath(const wchar_t* filename) noexcept;

// Unique CLSID for ViPERHiRes APO: {8F4E2B1A-7C3D-4E9A-B12F-6A8C4D9E105A}
// clang-format off
inline constexpr GUID CLSID_ViPERHiResAPO = {
    0x8f4e2b1a, 0x7c3d, 0x4e9a,
    { 0xb1, 0x2f, 0x6a, 0x8c, 0x4d, 0x9e, 0x10, 0x5a }
};
// clang-format on

inline constexpr wchar_t kClsidString[] = L"{8F4E2B1A-7C3D-4E9A-B12F-6A8C4D9E105A}";
inline constexpr wchar_t kApoFriendlyName[] = L"ViPERHiRes Audio Processing Object";

extern std::atomic<long> g_dllRefCount;

const APO_REG_PROPERTIES& getApoRegProperties() noexcept;

class ViPERHiResAPO final : public CBaseAudioProcessingObject,
                            public IAudioSystemEffects2 {
public:
    explicit ViPERHiResAPO(IUnknown* pUnkOuter = nullptr);
    ~ViPERHiResAPO() override;

    // Non-delegating IUnknown for COM Aggregation (required by Windows audiodg.exe!)
    STDMETHODIMP nonDelegatingQueryInterface(REFIID riid, void** ppvObject);
    ULONG nonDelegatingAddRef();
    ULONG nonDelegatingRelease();

    // Delegating IUnknown
    STDMETHODIMP QueryInterface(REFIID riid, void** ppvObject) override;
    STDMETHODIMP_(ULONG) AddRef() override;
    STDMETHODIMP_(ULONG) Release() override;

    // IAudioProcessingObject (overrides from CBaseAudioProcessingObject)
    STDMETHODIMP Initialize(UINT32 cbDataSize, BYTE* pbyData) override;
    STDMETHODIMP IsInputFormatSupported(
        IAudioMediaType* pOppositeFormat,
        IAudioMediaType* pRequestedInputFormat,
        IAudioMediaType** ppSupportedInputFormat) override;
    STDMETHODIMP IsOutputFormatSupported(
        IAudioMediaType* pOppositeFormat,
        IAudioMediaType* pRequestedOutputFormat,
        IAudioMediaType** ppSupportedOutputFormat) override;

    // IAudioProcessingObjectConfiguration
    STDMETHODIMP LockForProcess(
        UINT32 u32NumInputConnections,
        APO_CONNECTION_DESCRIPTOR** ppInputConnections,
        UINT32 u32NumOutputConnections,
        APO_CONNECTION_DESCRIPTOR** ppOutputConnections) override;

    STDMETHODIMP UnlockForProcess() override;

    // IAudioProcessingObjectRT
    STDMETHODIMP_(void) APOProcess(
        UINT32 u32NumInputConnections,
        APO_CONNECTION_PROPERTY** ppInputConnections,
        UINT32 u32NumOutputConnections,
        APO_CONNECTION_PROPERTY** ppOutputConnections) override;

    // IAudioSystemEffects2
    STDMETHODIMP GetEffectsList(
        LPGUID* ppEffectsIds,
        UINT* pcEffects,
        HANDLE Event) override;

private:
    class NonDelegatingUnknown final : public IUnknown {
    public:
        explicit NonDelegatingUnknown(ViPERHiResAPO* owner) noexcept : owner_(owner) {}
        STDMETHODIMP QueryInterface(REFIID riid, void** ppvObject) override {
            return owner_->nonDelegatingQueryInterface(riid, ppvObject);
        }
        STDMETHODIMP_(ULONG) AddRef() override { return owner_->nonDelegatingAddRef(); }
        STDMETHODIMP_(ULONG) Release() override { return owner_->nonDelegatingRelease(); }
    private:
        ViPERHiResAPO* owner_;
    };

    NonDelegatingUnknown nonDelegatingUnknown_{this};
    IUnknown* outerUnknown_ = nullptr;
    std::atomic<ULONG> refCount_{1};
    uint32_t sampleRate_ = 48000;
    uint32_t channelCount_ = 2;
    bool dspEligible_ = false;
    uint32_t lastConfigSeq_ = 0;

    DspParams cachedParams_{};
    SharedConfig sharedConfig_{};
    ViperEngine engine_{};
};

} // namespace viper::apo
