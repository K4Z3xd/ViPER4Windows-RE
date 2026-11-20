#include "viper/SharedConfig.hpp"

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>

#include <atomic>
#include <cstring>

namespace viper {

namespace {

struct SharedConfigBlock {
    std::atomic<uint32_t> seqlock; // Odd = write in progress, Even = stable
    DspParams params;
};

static_assert(std::atomic<uint32_t>::is_always_lock_free,
              "seqlock must be lock-free for real-time audio");

void ensureDirectoryExists(const std::wstring& dirPath) {
    CreateDirectoryW(dirPath.c_str(), nullptr);
}

} // namespace

std::wstring SharedConfig::defaultConfigFilePath() {
    // Direct path avoids shell32.dll / COM calls inside audiodg.exe (Session 0)
    return L"C:\\ProgramData\\ViPERHiRes\\live_params.bin";
}

SharedConfig::~SharedConfig() {
    close();
}

bool SharedConfig::open(bool writable) {
    close();
    writable_ = writable;

    const std::wstring filePath = defaultConfigFilePath();
    const size_t slashPos = filePath.find_last_of(L'\\');
    if (slashPos != std::wstring::npos) {
        ensureDirectoryExists(filePath.substr(0, slashPos));
    }

    const DWORD desiredAccess = writable ? (GENERIC_READ | GENERIC_WRITE) : GENERIC_READ;
    const DWORD creationDisp = writable ? OPEN_ALWAYS : OPEN_EXISTING;

    HANDLE hFile = CreateFileW(
        filePath.c_str(),
        desiredAccess,
        FILE_SHARE_READ | FILE_SHARE_WRITE,
        nullptr,
        creationDisp,
        FILE_ATTRIBUTE_NORMAL,
        nullptr);

    if (hFile == INVALID_HANDLE_VALUE) {
        return false;
    }

    LARGE_INTEGER fileSize{};
    if (!GetFileSizeEx(hFile, &fileSize) || fileSize.QuadPart < static_cast<LONGLONG>(sizeof(SharedConfigBlock))) {
        if (!writable) {
            CloseHandle(hFile);
            return false;
        }
        // Initialize default block on first creation
        SharedConfigBlock initial{};
        initial.seqlock.store(2, std::memory_order_relaxed);
        initial.params = DspParams{};
        initial.params.sequence = 1;

        DWORD written = 0;
        SetFilePointer(hFile, 0, nullptr, FILE_BEGIN);
        if (!WriteFile(hFile, &initial, sizeof(initial), &written, nullptr) || written != sizeof(initial)) {
            CloseHandle(hFile);
            return false;
        }
        FlushFileBuffers(hFile);
    }

    const DWORD protect = writable ? PAGE_READWRITE : PAGE_READONLY;
    HANDLE hMap = CreateFileMappingW(
        hFile,
        nullptr,
        protect,
        0,
        static_cast<DWORD>(sizeof(SharedConfigBlock)),
        nullptr);

    if (!hMap) {
        CloseHandle(hFile);
        return false;
    }

    const DWORD mapAccess = writable ? FILE_MAP_ALL_ACCESS : FILE_MAP_READ;
    void* view = MapViewOfFile(hMap, mapAccess, 0, 0, sizeof(SharedConfigBlock));
    if (!view) {
        CloseHandle(hMap);
        CloseHandle(hFile);
        return false;
    }

    fileHandle_ = hFile;
    mappingHandle_ = hMap;
    view_ = view;
    return true;
}

void SharedConfig::close() noexcept {
    if (view_) {
        UnmapViewOfFile(view_);
        view_ = nullptr;
    }
    if (mappingHandle_) {
        CloseHandle(static_cast<HANDLE>(mappingHandle_));
        mappingHandle_ = nullptr;
    }
    if (fileHandle_) {
        CloseHandle(static_cast<HANDLE>(fileHandle_));
        fileHandle_ = nullptr;
    }
    writable_ = false;
}

bool SharedConfig::pollChanges(uint32_t& lastSequence, DspParams& outParams) const noexcept {
    if (!view_) {
        return false;
    }

    const auto* block = static_cast<const SharedConfigBlock*>(view_);
    const uint32_t seq1 = block->seqlock.load(std::memory_order_acquire);
    if ((seq1 & 1u) != 0u) {
        // Writer currently updating; skip this audio buffer and catch it on the next 10ms frame
        return false;
    }

    if (seq1 == lastSequence) {
        return false;
    }

    DspParams temp{};
    std::memcpy(&temp, &block->params, sizeof(DspParams));

    const uint32_t seq2 = block->seqlock.load(std::memory_order_acquire);
    if (seq1 != seq2 || temp.magic != 0x56504852u) {
        return false;
    }

    outParams = temp;
    lastSequence = seq1;
    return true;
}

bool SharedConfig::readSnapshot(DspParams& outParams) const noexcept {
    uint32_t dummySeq = 0;
    for (int attempt = 0; attempt < 8; ++attempt) {
        if (pollChanges(dummySeq, outParams)) {
            return true;
        }
        Sleep(0);
    }
    return false;
}

bool SharedConfig::writeParams(const DspParams& params) noexcept {
    if (!view_ || !writable_) {
        return false;
    }

    auto* block = static_cast<SharedConfigBlock*>(view_);
    uint32_t seq = block->seqlock.load(std::memory_order_relaxed);
    if ((seq & 1u) != 0u) {
        ++seq; // Recover if a previous writer crashed mid-write
    }

    // Mark write in progress (odd)
    block->seqlock.store(seq + 1, std::memory_order_release);

    DspParams toWrite = params;
    toWrite.magic = kParamsMagic;
    toWrite.version = kParamsVersion;
    toWrite.sequence = (seq + 2) / 2;
    std::memcpy(&block->params, &toWrite, sizeof(DspParams));

    // Mark write complete (even)
    block->seqlock.store(seq + 2, std::memory_order_release);
    return true;
}

} // namespace viper
