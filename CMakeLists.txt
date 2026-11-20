cmake_minimum_required(VERSION 3.24)
project(ViPERHiRes VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# DSP Core Static Library (Real-time safe, 44.1kHz - 384kHz, 32-bit float I/O, 64-bit internal math)
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

target_include_directories(viper_dsp
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/include
)

if(MSVC)
    target_compile_options(viper_dsp PRIVATE
        /W4
        /permissive-
        /Zc:__cplusplus
        /GS
        /sdl
        $<$<CONFIG:Release>:/O2 /fp:fast>
    )
else()
    target_compile_options(viper_dsp PRIVATE
        -Wall -Wextra -Wpedantic
        $<$<CONFIG:Release>:-O3>
    )
endif()

# Stage 1 Verification CLI: WAV Processor & Hi-Res Self-Test
add_executable(viper_wav_tester
    tools/wav_tester/main.cpp
)
target_link_libraries(viper_wav_tester PRIVATE viper_dsp)

if(MSVC)
    target_compile_options(viper_wav_tester PRIVATE /W4 /permissive- /Zc:__cplusplus)
endif()
