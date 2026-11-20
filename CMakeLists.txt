cmake_minimum_required(VERSION 3.24)
project(ViPERHiRes VERSION 0.2.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# 1. DSP Core Static Library (Real-time safe, 44.1kHz - 384kHz, 32-bit float I/O, 64-bit internal math)
add_library(viper_dsp STATIC
    src/dsp/BiquadFilter.cpp
    src/dsp/ParametricEq.cpp
    src/dsp/ViperBass.cpp
    src/dsp/ViperClarity.cpp
    src/dsp/TubeSimulator.cpp
    src/dsp/DifferentialSurround.cpp
    src/dsp/Limiter.cpp
    src/dsp/ViperEngine.cpp
)
target_include_directories(viper_dsp PUBLIC ${CMAKE_CURRENT_SOURCE_DIR}/include)

# 2. Shared Memory IPC Static Library (Session 0 audiodg.exe <-> Session 1+ UI)
add_library(viper_ipc STATIC
    src/ipc/SharedConfig.cpp
)
target_include_directories(viper_ipc PUBLIC ${CMAKE_CURRENT_SOURCE_DIR}/include)
target_link_libraries(viper_ipc PRIVATE shell32 ole32)

# 3. Stage 1 Verification CLI: Hi-Res DSP Self-Test
add_executable(viper_wav_tester
    tools/wav_tester/main.cpp
)
target_link_libraries(viper_wav_tester PRIVATE viper_dsp)

# 4. Stage 2 & 3: Windows 10/11 Hi-Res Audio Processing Object (COM DLL)
add_library(ViPERHiResAPO SHARED
    apo/ViPERHiResAPO.cpp
    apo/DllMain.cpp
    apo/ViPERHiResAPO.def
)
target_link_libraries(ViPERHiResAPO PRIVATE
    viper_dsp
    viper_ipc
    audiobaseprocessingobject
    audiomediatypecrt
    legacy_stdio_definitions
    ole32
    advapi32
    uuid
)
target_compile_definitions(ViPERHiResAPO PRIVATE UNICODE _UNICODE)

# 5. Stage 3 Control & Device Discovery CLI (also used by Stage 4 UI)
add_executable(viper_ctl
    tools/viper_ctl/main.cpp
)
target_link_libraries(viper_ctl PRIVATE
    viper_ipc
    ole32
    propsys
    uuid
)
target_compile_definitions(viper_ctl PRIVATE UNICODE _UNICODE)

if(MSVC)
    set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
    foreach(tgt viper_dsp viper_ipc viper_wav_tester ViPERHiResAPO viper_ctl)
        set_property(TARGET ${tgt} PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
        target_compile_options(${tgt} PRIVATE
            /W4
            /permissive-
            /Zc:__cplusplus
            /GS
            /sdl
            $<$<CONFIG:Release>:/O2 /fp:fast>
        )
    endforeach()
endif()
