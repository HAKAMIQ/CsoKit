#pragma once

#include <cstdint>
#include <cstddef>

#if defined(_WIN32)
    #if defined(CSOKIT_NATIVE_EXPORTS)
        #define CSOKIT_API __declspec(dllexport)
    #else
        #define CSOKIT_API __declspec(dllimport)
    #endif
#else
    #define CSOKIT_API
#endif

extern "C"
{
    enum CsoKitNativeStatus : int32_t
    {
        CSOKIT_NATIVE_OK = 0,
        CSOKIT_NATIVE_UNSUPPORTED_PLATFORM = 1,
        CSOKIT_NATIVE_INVALID_ARGUMENT = 2,
        CSOKIT_NATIVE_OUTPUT_TOO_SMALL = 3,
        CSOKIT_NATIVE_CODEC_UNAVAILABLE = 4,
        CSOKIT_NATIVE_INTERNAL_ERROR = 100
    };

    enum CsoKitCodec : int32_t
    {
        CSOKIT_CODEC_ZLIB_DEFAULT = 1,
        CSOKIT_CODEC_ZLIB_FILTERED = 2,
        CSOKIT_CODEC_ZLIB_HUFFMAN_ONLY = 3,
        CSOKIT_CODEC_ZLIB_RLE = 4,

        CSOKIT_CODEC_LIBDEFLATE = 10,
        CSOKIT_CODEC_ZOPFLI = 20,
        CSOKIT_CODEC_7Z_DEFLATE = 30
    };

    struct CsoKitNativeVersion
    {
        uint32_t abi_version;
        uint32_t major;
        uint32_t minor;
        uint32_t patch;
    };

    struct CsoKitNativeCapabilities
    {
        uint32_t abi_version;
        uint32_t has_zlib;
        uint32_t has_libdeflate;
        uint32_t has_zopfli;
        uint32_t has_7z_deflate;
        uint32_t has_lz4;
    };

    CSOKIT_API int32_t csokit_native_probe();

    CSOKIT_API int32_t csokit_native_get_version(
        CsoKitNativeVersion* version
    );

    CSOKIT_API int32_t csokit_native_get_capabilities(
        CsoKitNativeCapabilities* capabilities
    );

    CSOKIT_API int32_t csokit_native_deflate_raw(
        int32_t codec,
        int32_t level,
        int32_t strategy,
        const uint8_t* input,
        size_t input_size,
        uint8_t* output,
        size_t output_capacity,
        size_t* output_size
    );

    CSOKIT_API int32_t csokit_native_inflate_raw(
        const uint8_t* input,
        size_t input_size,
        uint8_t* output,
        size_t output_capacity,
        size_t* output_size
    );

    CSOKIT_API int32_t csokit_native_deflate_zopfli(
        const uint8_t* input,
        size_t input_size,
        int32_t iterations,
        uint8_t* output,
        size_t output_capacity,
        size_t* output_size
    );
}
