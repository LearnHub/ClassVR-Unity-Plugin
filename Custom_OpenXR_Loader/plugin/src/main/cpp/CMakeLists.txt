cmake_minimum_required(VERSION 3.10)
project(OpenXRHook)

# Set C++ standard
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Add your shared library, output named libopenxr_loader.so
add_library(OpenXRHook SHARED OpenXRHook.cpp)

# Rename the output file to libopenxr_loader.so
set_target_properties(OpenXRHook PROPERTIES OUTPUT_NAME "openxr_loader")

# Include directory (use forward slashes)
target_include_directories(OpenXRHook PRIVATE ${CMAKE_SOURCE_DIR}/external)

# Link against Android log and dl
find_library(log-lib log)
find_library(dl-lib dl)

target_link_libraries(OpenXRHook
        ${log-lib}
        ${dl-lib}
)

add_custom_command(TARGET OpenXRHook POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:OpenXRHook>
        ${CMAKE_SOURCE_DIR}/../jniLibs/arm64-v8a/
)
