cmake_minimum_required(VERSION 3.25)

file(READ "${CMAKE_CURRENT_SOURCE_DIR}/../../VERSION" CSOKIT_VERSION)
string(STRIP "${CSOKIT_VERSION}" CSOKIT_VERSION)

if (NOT CSOKIT_VERSION MATCHES "^[0-9]+[.][0-9]+[.][0-9]+$")
    message(FATAL_ERROR "VERSION must contain a stable semantic version. Actual: ${CSOKIT_VERSION}")
endif()

project(CsoKit.Native
    VERSION ${CSOKIT_VERSION}
    LANGUAGES C CXX
)

configure_file(
    ${CMAKE_CURRENT_SOURCE_DIR}/include/csokit_version.h.in
    ${CMAKE_CURRENT_BINARY_DIR}/generated/csokit_version.h
    @ONLY
)

include(FetchContent)

set(ZLIB_BUILD_EXAMPLES OFF CACHE BOOL "" FORCE)
set(ZLIB_BUILD_MINIZIP OFF CACHE BOOL "" FORCE)
set(ZLIB_BUILD_SHARED OFF CACHE BOOL "" FORCE)
set(ZLIB_BUILD_STATIC ON CACHE BOOL "" FORCE)
set(ZLIB_BUILD_TESTING OFF CACHE BOOL "" FORCE)
set(ZLIB_INSTALL OFF CACHE BOOL "" FORCE)
FetchContent_Declare(
    zlib
    GIT_REPOSITORY https://github.com/madler/zlib.git
    GIT_TAG v1.3.2
    GIT_SHALLOW TRUE
)

set(LIBDEFLATE_BUILD_GZIP OFF CACHE BOOL "" FORCE)
set(LIBDEFLATE_BUILD_TESTS OFF CACHE BOOL "" FORCE)
set(LIBDEFLATE_BUILD_EXAMPLES OFF CACHE BOOL "" FORCE)
set(LIBDEFLATE_BUILD_SHARED_LIB OFF CACHE BOOL "" FORCE)
set(LIBDEFLATE_BUILD_STATIC_LIB ON CACHE BOOL "" FORCE)
FetchContent_Declare(
    libdeflate
    GIT_REPOSITORY https://github.com/ebiggers/libdeflate.git
    GIT_TAG v1.25
    GIT_SHALLOW TRUE
)

FetchContent_MakeAvailable(zlib libdeflate)

set(ZOPFLI_ROOT ${CMAKE_CURRENT_SOURCE_DIR}/../third_party/zopfli)
set(ZOPFLI_SOURCES
    ${ZOPFLI_ROOT}/src/zopfli/blocksplitter.c
    ${ZOPFLI_ROOT}/src/zopfli/cache.c
    ${ZOPFLI_ROOT}/src/zopfli/deflate.c
    ${ZOPFLI_ROOT}/src/zopfli/gzip_container.c
    ${ZOPFLI_ROOT}/src/zopfli/hash.c
    ${ZOPFLI_ROOT}/src/zopfli/katajainen.c
    ${ZOPFLI_ROOT}/src/zopfli/lz77.c
    ${ZOPFLI_ROOT}/src/zopfli/squeeze.c
    ${ZOPFLI_ROOT}/src/zopfli/tree.c
    ${ZOPFLI_ROOT}/src/zopfli/util.c
    ${ZOPFLI_ROOT}/src/zopfli/zlib_container.c
    ${ZOPFLI_ROOT}/src/zopfli/zopfli_lib.c
)

add_library(csokit_zopfli STATIC
    ${ZOPFLI_SOURCES}
)

target_include_directories(csokit_zopfli
    PUBLIC
        ${ZOPFLI_ROOT}/src
)

add_library(CsoKit.Native SHARED
    src/csokit_native.cpp
)

target_include_directories(CsoKit.Native
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/include
        ${CMAKE_CURRENT_BINARY_DIR}/generated
        ${ZOPFLI_ROOT}/src
        ${zlib_SOURCE_DIR}
        ${zlib_BINARY_DIR}
)

if (TARGET zlibstatic)
    set(CSOKIT_ZLIB_TARGET zlibstatic)
elseif (TARGET zlib)
    set(CSOKIT_ZLIB_TARGET zlib)
else()
    message(FATAL_ERROR "zlib CMake target was not found.")
endif()

if (TARGET libdeflate_static)
    set(CSOKIT_LIBDEFLATE_TARGET libdeflate_static)
elseif (TARGET libdeflate)
    set(CSOKIT_LIBDEFLATE_TARGET libdeflate)
else()
    message(FATAL_ERROR "libdeflate CMake target was not found.")
endif()

target_link_libraries(CsoKit.Native
    PRIVATE
        csokit_zopfli
        ${CSOKIT_ZLIB_TARGET}
        ${CSOKIT_LIBDEFLATE_TARGET}
)

target_compile_features(CsoKit.Native PRIVATE cxx_std_20)

if (MSVC)
    target_compile_options(CsoKit.Native PRIVATE
        /W4
        /WX
        /permissive-
        /EHsc
        /utf-8
    )

    target_compile_definitions(CsoKit.Native PRIVATE
        WIN32_LEAN_AND_MEAN
        NOMINMAX
    )
endif()

set_target_properties(CsoKit.Native PROPERTIES
    OUTPUT_NAME "CsoKit.Native"
)
