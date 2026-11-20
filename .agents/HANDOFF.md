# Session Handoff
Updated: 2026-11-20 23:59

## Goal
- Build **ViPERHiRes**: a standalone Windows 10/11 Hi-Res Audio Processing Object (APO, 44.1kHz–384kHz, 32-bit float) with ViPER-style DSP effects and a modern Windows 11 Studio UI.

## Completed
- **Stage 1 (`viper_dsp` + `viper_wav_tester`)**: Modern C++23 64-bit internal math DSP engine verified across 44.1kHz–384kHz (~38us @ 48kHz, ~295us @ 384kHz per 10ms block, 0 warnings).
- **Stage 2 (`ViPERHiResAPO.dll`)**: Windows 10/11 SFX COM APO accepting 32-bit IEEE Float 44.1kHz–384kHz + safe 1-click backup/install/restore scripts (`scripts/install_apo.ps1`, `scripts/uninstall_apo.ps1`).
- **Stage 3 (`viper_ipc` + `viper_ctl.exe`)**: Lock-free seqlock memory-mapped SharedConfig (`%ProgramData%\ViPERHiRes\live_params.bin`) + Core Audio device discovery CLI.
- **Stage 4 (`ui/`)**: Studio Obsidian Dark React + Tailwind CSS v4 + Lucide UI (`ui/dist`) + local bridge server (`ui/server.mjs`, `Start-ViPERHiRes.bat`).

## In Progress / Modified Files
- All 4 stages built cleanly and committed.
- Detected user DAC: `Headphones (CX31993+MAX97220 HiFiAudio)` (`{d62a84b9-0a81-4d82-891d-d7a6ef88fb4d}`).

## Known Issues / Blockers
- APO has NOT yet been installed into Windows Registry (waiting for user to run `scripts/install_apo.ps1` as Administrator when ready to test live audio).

## Next Steps
1. User opens `http://127.0.0.1:48192` to preview UI and test live SharedConfig sync.
2. When ready for live audio processing, run `scripts/install_apo.ps1` (Admin) on `{d62a84b9-0a81-4d82-891d-d7a6ef88fb4d}` and switch DAC to 96kHz/192kHz/384kHz in Windows Sound Settings.
3. Optional future enhancements: Convolver (`.irs` impulse response with auto-resampling), Dynamic System, or packaging UI into standalone Tauri/WebView2 `.exe`.
