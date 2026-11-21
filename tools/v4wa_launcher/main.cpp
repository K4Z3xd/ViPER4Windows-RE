#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include <string>

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR lpCmdLine, int nCmdShow) {
    wchar_t selfPath[MAX_PATH] = {};
    DWORD len = GetModuleFileNameW(nullptr, selfPath, MAX_PATH);
    if (len == 0 || len >= MAX_PATH) return 1;

    std::wstring dir(selfPath, len);
    const size_t slash = dir.find_last_of(L"\\/");
    if (slash != std::wstring::npos) dir.resize(slash);

    const std::wstring binDir = dir + L"\\bin";
    const std::wstring targetExe = binDir + L"\\V4Wa.exe";

    std::wstring cmdLine = L"\"" + targetExe + L"\"";
    if (lpCmdLine && *lpCmdLine) {
        cmdLine += L" ";
        cmdLine += lpCmdLine;
    }

    STARTUPINFOW si{};
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = static_cast<WORD>(nCmdShow);
    PROCESS_INFORMATION pi{};

    if (!CreateProcessW(targetExe.c_str(), cmdLine.data(), nullptr, nullptr, FALSE,
                        0, nullptr, binDir.c_str(), &si, &pi)) {
        MessageBoxW(nullptr, L"Could not launch V4Wa runtime (bin\\V4Wa.exe).", L"V4Wa", MB_OK | MB_ICONERROR);
        return 1;
    }

    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    return 0;
}
