#pragma once

#include "viper/DspTypes.hpp"
#include <cstdint>
#include <string>

namespace viper {

// Memory-mapped shared configuration backed by %ProgramData%\ViPERHiRes\live_params.bin.
// Works seamlessly across Session 0 (audiodg.exe / LOCAL SERVICE) and Session 1+ (User UI)
// with zero syscalls and zero heap allocations during real-time APOProcess() reads.
class SharedConfig {
public:
    SharedConfig() = default;
    ~SharedConfig();

    SharedConfig(const SharedConfig&) = delete;
    SharedConfig& operator=(const SharedConfig&) = delete;

    // Opens (or creates if writable) the shared parameter mapping.
    [[nodiscard]] bool open(bool writable);
    void close() noexcept;

    [[nodiscard]] bool isOpen() const noexcept { return view_ != nullptr; }

    // Real-time safe lock-free read (seqlock pattern).
    // Returns true if parameters changed since lastSequence, updating outParams and lastSequence.
    [[nodiscard]] bool pollChanges(uint32_t& lastSequence, DspParams& outParams) const noexcept;

    // Reads current parameters regardless of sequence.
    [[nodiscard]] bool readSnapshot(DspParams& outParams) const noexcept;

    // Writes updated parameters (for UI / CLI; requires writable == true).
    // Automatically increments the sequence counter so the APO picks it up immediately.
    [[nodiscard]] bool writeParams(const DspParams& params) noexcept;

    [[nodiscard]] static std::wstring defaultConfigFilePath();

private:
    void* fileHandle_ = nullptr;
    void* mappingHandle_ = nullptr;
    void* view_ = nullptr;
    bool writable_ = false;
};

} // namespace viper
