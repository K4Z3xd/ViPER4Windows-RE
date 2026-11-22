#include "ViPERHiResAPO.hpp"

#include <atomic>
#include <cstdio>
#include <string>

namespace {

void dllLog(const char* msg) noexcept {
    const std::wstring path = viper::apo::getApoLogPath(L"apo_dll.txt");
    HANDLE h = CreateFileW(path.c_str(),
                           FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE,
                           nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h != INVALID_HANDLE_VALUE) {
        DWORD written = 0;
        WriteFile(h, msg, static_cast<DWORD>(std::strlen(msg)), &written, nullptr);
        WriteFile(h, "\r\n", 2, &written, nullptr);
        CloseHandle(h);
    }
}

HMODULE g_moduleHandle = nullptr;

class ViPERClassFactory final : public IClassFactory {
public:
    ViPERClassFactory() {
        viper::apo::g_dllRefCount.fetch_add(1, std::memory_order_relaxed);
    }

    ~ViPERClassFactory() {
        viper::apo::g_dllRefCount.fetch_sub(1, std::memory_order_relaxed);
    }

    STDMETHODIMP QueryInterface(REFIID riid, void** ppv) override {
        if (!ppv) return E_POINTER;
        if (riid == __uuidof(IUnknown) || riid == __uuidof(IClassFactory)) {
            *ppv = static_cast<IClassFactory*>(this);
            AddRef();
            return S_OK;
        }
        *ppv = nullptr;
        return E_NOINTERFACE;
    }

    STDMETHODIMP_(ULONG) AddRef() override {
        return refCount_.fetch_add(1, std::memory_order_relaxed) + 1;
    }

    STDMETHODIMP_(ULONG) Release() override {
        const ULONG c = refCount_.fetch_sub(1, std::memory_order_acq_rel) - 1;
        if (c == 0) {
            delete this;
        }
        return c;
    }

    STDMETHODIMP CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppvObject) override {
        if (!ppvObject) return E_POINTER;
        *ppvObject = nullptr;
        // COM Aggregation rule: when pUnkOuter != nullptr, riid MUST be IID_IUnknown
        if (pUnkOuter != nullptr && riid != __uuidof(IUnknown)) {
            return CLASS_E_NOAGGREGATION;
        }

        auto* apo = new (std::nothrow) viper::apo::ViPERHiResAPO(pUnkOuter);
        if (!apo) return E_OUTOFMEMORY;

        const HRESULT hr = apo->nonDelegatingQueryInterface(riid, ppvObject);
        apo->nonDelegatingRelease();
        return hr;
    }

    STDMETHODIMP LockServer(BOOL fLock) override {
        if (fLock) {
            viper::apo::g_dllRefCount.fetch_add(1, std::memory_order_relaxed);
        } else {
            viper::apo::g_dllRefCount.fetch_sub(1, std::memory_order_relaxed);
        }
        return S_OK;
    }

private:
    std::atomic<ULONG> refCount_{1};
};

HRESULT setRegString(HKEY root, const std::wstring& subKey, const wchar_t* valueName, const std::wstring& data) {
    HKEY hKey = nullptr;
    LONG res = RegCreateKeyExW(root, subKey.c_str(), 0, nullptr, REG_OPTION_NON_VOLATILE,
                               KEY_WRITE, nullptr, &hKey, nullptr);
    if (res != ERROR_SUCCESS) {
        return HRESULT_FROM_WIN32(res);
    }
    res = RegSetValueExW(
        hKey,
        valueName,
        0,
        REG_SZ,
        reinterpret_cast<const BYTE*>(data.c_str()),
        static_cast<DWORD>((data.size() + 1) * sizeof(wchar_t)));
    RegCloseKey(hKey);
    return HRESULT_FROM_WIN32(res);
}

} // namespace

BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID /*lpvReserved*/) {
    if (fdwReason == DLL_PROCESS_ATTACH) {
        g_moduleHandle = hinstDLL;
        DisableThreadLibraryCalls(hinstDLL);
    }
    return TRUE;
}

STDAPI DllCanUnloadNow() {
    return (viper::apo::g_dllRefCount.load(std::memory_order_acquire) == 0) ? S_OK : S_FALSE;
}

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, LPVOID* ppv) {
    dllLog("DllGetClassObject called");
    if (!ppv) return E_POINTER;
    *ppv = nullptr;

    if (rclsid != viper::apo::CLSID_ViPERHiResAPO) {
        return CLASS_E_CLASSNOTAVAILABLE;
    }

    auto* factory = new (std::nothrow) ViPERClassFactory();
    if (!factory) return E_OUTOFMEMORY;

    const HRESULT hr = factory->QueryInterface(riid, ppv);
    factory->Release();
    return hr;
}

STDAPI DllRegisterServer() {
    wchar_t modulePath[MAX_PATH] = {};
    if (GetModuleFileNameW(g_moduleHandle, modulePath, MAX_PATH) == 0) {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    const std::wstring clsidKey = std::wstring(L"CLSID\\") + viper::apo::kClsidString;
    const std::wstring inprocKey = clsidKey + L"\\InprocServer32";

    HRESULT hr = setRegString(HKEY_CLASSES_ROOT, clsidKey, nullptr, viper::apo::kApoFriendlyName);
    if (FAILED(hr)) return hr;

    hr = setRegString(HKEY_CLASSES_ROOT, inprocKey, nullptr, modulePath);
    if (FAILED(hr)) return hr;

    hr = setRegString(HKEY_CLASSES_ROOT, inprocKey, L"ThreadingModel", L"Both");
    if (FAILED(hr)) return hr;

    // Re-register cleanly with Windows Audio Engine APO registry (3 core interfaces)
    (void)UnregisterAPO(viper::apo::CLSID_ViPERHiResAPO);
    hr = RegisterAPO(&viper::apo::getApoRegProperties());
    return hr;
}

STDAPI DllUnregisterServer() {
    (void)UnregisterAPO(viper::apo::CLSID_ViPERHiResAPO);

    const std::wstring clsidKey = std::wstring(L"CLSID\\") + viper::apo::kClsidString;
    RegDeleteTreeW(HKEY_CLASSES_ROOT, clsidKey.c_str());
    return S_OK;
}
