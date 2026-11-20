cmake_minimum_required(VERSION 3.24)
project(ViPERHiRes VERSION 0.2.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# 1. DSP Core Static Library (Real-time safe, 44.1kHz - 384kHz, 32-bit float I/O, 64-bit internal math)
add_library(viper_dsp STATIC
    src/dsp/BiquadFilter.cpp
    src/dsp/ViperDdc.cpp
    src/dsp/ParametricEq.cpp
    src/dsp/ViperBass.cpp
    src/dsp/DynamicSystem.cpp
    src/dsp/SpectrumExtension.cpp
    src/dsp/ViperClarity.cpp
    src/dsp/TubeSimulator.cpp
    src/dsp/AnalogX.cpp
    src/dsp/Compressor.cpp
    src/dsp/PlaybackGain.cpp
    src/dsp/CureTech.cpp
    src/dsp/FieldSurround.cpp
    src/dsp/VhsPlus.cpp
    src/dsp/DifferentialSurround.cpp
    src/dsp/Reverb.cpp
    src/dsp/Convolver.cpp
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
    version
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

# 6. Native foobar2000 v2 x64 Bit-Transparent Status Reporter Component
# Uses /MD (MultiThreadedDLL) to match official foobar2000 v2 x64 SDK runtime.
set(FB2K_SDK_DIR "${CMAKE_CURRENT_SOURCE_DIR}/third_party/foobar2000_sdk")
if(MSVC AND EXISTS "${FB2K_SDK_DIR}/foobar2000/SDK/x64/Release/foobar2000_SDK.lib")
    add_library(foo_dsp_viperhires SHARED
        foobar/foo_dsp_viperhires.cpp
    )
    target_include_directories(foo_dsp_viperhires PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/include
        ${FB2K_SDK_DIR}
        ${FB2K_SDK_DIR}/foobar2000
    )
    target_link_libraries(foo_dsp_viperhires PRIVATE
        "${FB2K_SDK_DIR}/foobar2000/foobar2000_component_client/x64/Release/foobar2000_component_client.lib"
        "${FB2K_SDK_DIR}/foobar2000/SDK/x64/Release/foobar2000_SDK.lib"
        "${FB2K_SDK_DIR}/pfc/x64/Release/pfc.lib"
        "${FB2K_SDK_DIR}/foobar2000/shared/shared-x64.lib"
        shlwapi
        ole32
    )
    target_compile_definitions(foo_dsp_viperhires PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)
    set_property(TARGET foo_dsp_viperhires PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>DLL")
    target_compile_options(foo_dsp_viperhires PRIVATE /W3 /Zc:__cplusplus $<$<CONFIG:Release>:/O2 /fp:fast>)
endif()
