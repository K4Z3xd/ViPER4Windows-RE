# ViPER4Windows RE

**Another ViPER4Windows RE** — a modern, clean-room, system-wide Hi-Res sound enhancer for **Windows 10 & Windows 11 (x64)** built with a **64-bit C++23 DSP engine**, a native **Windows MFX Audio Processing Object (APO)**, and a responsive **WinUI 3** desktop interface.

> **AI-Powered Project:** Architected, written, and refined entirely with AI (Google DeepMind / Antigravity), inspired by the classic ViPER4Windows & ViPER4Android FX audio ecosystem.

---

## Key Features

### Hi-Res 64-Bit Audio Engine (44.1 – 384 kHz)
- **Native Windows 10/11 MFX APO (`ViPERHiResAPO.dll`):** Attaches cleanly to headphones, USB DACs, speakers, HDMI audio, and Bluetooth A2DP devices with zero VC++ Runtime dependencies and automatic factory Registry backup/restore.
- **Zero-Allocation Real-Time Audio Thread:** Internal 64-bit (`double`) processing with lock-free seqlock IPC for instant, glitch-free parameter updates.
- **Bluetooth-Safe Architecture:** Automatic pass-through for non-stereo or telephony formats (`8–24 kHz` / Mono Hands-Free) and automatic hiding of Hands-Free endpoints in the app.

### 17 Clean-Room DSP Modules
| Category | Modules |
| :--- | :--- |
| **Tonal & Correction** | **Equalizer** (10-Band & 18-Band with selectable **Zero-Latency IIR** or **2048-tap Linear-Phase FIR**), **ViPER-DDC** (`.vdc` AutoEQ profiles with Bilinear All-Pass frequency mapping up to 384 kHz) |
| **Bass & Detail** | **ViPER Bass** (*Natural*, *PureBass+*, and **True Sub-Octave Synthesizer** *Subwoofer*), **Dynamic System** (2-band low/high dynamic punch), **ViPER Clarity** (Anti-Mud + *Natural* / *OZone+* / transient-aware *XHiFi*), **Spectrum Extension (VSE)** |
| **Analog Character** | **Tube Simulator (6N1J)** (asymmetric triode 2nd harmonics + DC blocker), **AnalogX** (Class-A solid-state warmth) |
| **Dynamics** | **FET Compressor** (6 dB soft-knee), **Playback Gain Control (AGC)** (RMS loudness leveling with noise-floor gate), **Output Gain, Pan & Brickwall Limiter** |
| **Spatial & Convolution** | **Convolver** (Partitioned FFT `.irs`/`.wav` supporting Mono, Stereo, and **True Stereo 4-Channel** with Cubic Hermite Hi-Res resampling), **Field Surround**, **Headphone Surround+ (VHS+)**, **Differential Surround**, **Cure Tech+**, **Reverberation** |

### Modern WinUI 3 Desktop Interface
- **Responsive Masonry Layout:** Automatically scales from 1 to 5 columns based on window width, expanding active modules and compacting inactive ones with zero wasted space.
- **Customizable Appearance:** Native Windows 11 **Acrylic** & **Dark OLED** themes, automatic Windows accent sync, solid & gradient presets, and a **1–3 Color Custom Accent Builder**.
- **Clean Install Layout:**
  ```text
  C:\Program Files\ViPER4Windows RE\
  ├── ViPER4WindowsRE.exe   # Native launcher
  ├── bin\                  # Internal runtime, APO DLL, CLI & scripts
  ├── irs\                  # Convolver .irs / .wav impulse responses
  ├── vdc\                  # ViPER-DDC .vdc headphone profiles
  ├── preset\               # Saved full presets & EQ presets
  └── logs\                 # All APO, installer & diagnostic logs
  ```

---

## Installation

1. Download **`ViPER4Windows-RE-Setup-1.0.0-x64.exe`** from the [Releases](https://github.com/K4Z3xd/ViPER4Windows-RE/releases) page.
2. Run the installer (Administrator privileges are required to register the MFX Audio Processing Object).
3. Launch **ViPER4Windows RE** — open **Settings → Devices & APO** to attach or restore factory audio on any output device with one click.

> **Note:** Because the installer is currently unsigned, Windows SmartScreen may display an *"Unknown Publisher"* prompt. You can inspect the full source code here or build it directly from source below.

---

## Building from Source

### Prerequisites
- **Windows 10 / 11 (x64)**
- **Visual Studio 2022 Build Tools** (MSVC C++23 + Windows 11 SDK) + **CMake** & **Ninja**
- **.NET 8 SDK** (for WinUI 3 / Windows App SDK 1.7)
- **Inno Setup 6** (optional, for building the standalone installer)

### 1. Build Native C++23 DSP Engine, APO & Tools
```powershell
cmake -B build -G Ninja -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release
```

### 2. Publish Native WinUI 3 App
```powershell
cd winui-demo
dotnet publish -c Release -p:Platform=x64 -r win-x64 --self-contained true -o ..\installer\stage\app
```

### 3. Build Standalone Installer (Optional)
```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\V4Wa-Setup.iss
```

---

## Credits & Acknowledgements
- **ViPER4Windows & ViPER4Android FX** (ViPER's Audio / Euphy Wong / Team DeWitt) — original audio DSP concepts, module designs, and the `.irs` / `.vdc` ecosystem.
- **AutoEQ** (Jaakko Pasanen) — open-source headphone frequency correction and ViPER-DDC (`.vdc`) profile ecosystem.
- **Microsoft Windows App SDK & Fluent Design** — WinUI 3 native controls, Acrylic backdrop, and Core Audio APO framework.

---

## License
Distributed under the [MIT License](LICENSE).
