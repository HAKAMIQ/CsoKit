#define CSOKIT_NATIVE_EXPORTS

#include "csokit_native.h"

#include <cstdlib>
#include <cstring>

#include "libdeflate.h"
#include "zlib.h"
#include "zopfli/zopfli.h"
#include "csokit_version.h"

namespace
{
    int32_t deflate_raw_zlib(
        int32_t level,
        int32_t strategy,
        const uint8_t* input,
        size_t input_size,
        uint8_t* output,
        size_t output_capacity,
        size_t* output_size)
    {
        if ((input == nullptr && input_size != 0) ||
            output == nullptr ||
            output_size == nullptr ||
            level < Z_NO_COMPRESSION ||
            level > Z_BEST_COMPRESSION ||
            input_size > UINT_MAX ||
            output_capacity > UINT_MAX)
        {
            return CSOKIT_NATIVE_INVALID_ARGUMENT;
        }

        z_stream stream{};
        int init = deflateInit2(
            &stream,
            level,
            Z_DEFLATED,
            -15,
            9,
            strategy);

        if (init != Z_OK)
        {
            return CSOKIT_NATIVE_INTERNAL_ERROR;
        }

        stream.next_in = const_cast<Bytef*>(reinterpret_cast<const Bytef*>(input));
        stream.avail_in = static_cast<uInt>(input_size);
        stream.next_out = reinterpret_cast<Bytef*>(output);
        stream.avail_out = static_cast<uInt>(output_capacity);

        int result = deflate(&stream, Z_FINISH);

        if (result != Z_STREAM_END)
        {
            deflateEnd(&stream);
            return result == Z_OK || result == Z_BUF_ERROR
                ? CSOKIT_NATIVE_OUTPUT_TOO_SMALL
                : CSOKIT_NATIVE_INTERNAL_ERROR;
        }

        *output_size = stream.total_out;

        int end = deflateEnd(&stream);
        return end == Z_OK
            ? CSOKIT_NATIVE_OK
            : CSOKIT_NATIVE_INTERNAL_ERROR;
    }

    int32_t deflate_raw_libdeflate(
        int32_t level,
        const uint8_t* input,
        size_t input_size,
        uint8_t* output,
        size_t output_capacity,
        size_t* output_size)
    {
        if ((input == nullptr && input_size != 0) ||
            output == nullptr ||
            output_size == nullptr)
        {
            return CSOKIT_NATIVE_INVALID_ARGUMENT;
        }

        if (level < 1)
        {
            level = 1;
        }

        if (level > 12)
        {
            level = 12;
        }

        libdeflate_compressor* compressor = libdeflate_alloc_compressor(level);

        if (compressor == nullptr)
        {
            return CSOKIT_NATIVE_INTERNAL_ERROR;
        }

        size_t written = libdeflate_deflate_compress(
            compressor,
            input,
            input_size,
            output,
            output_capacity);

        libdeflate_free_compressor(compressor);

        if (written == 0)
        {
            return CSOKIT_NATIVE_OUTPUT_TOO_SMALL;
        }

        *output_size = written;
        return CSOKIT_NATIVE_OK;
    }
}

extern "C"
{
    int32_t csokit_native_probe()
    {
        return CSOKIT_NATIVE_OK;
    }

    int32_t csokit_native_get_version(
        CsoKitNativeVersion* version
    )
    {
        if (version == nullptr)
        {
            return CSOKIT_NATIVE_INVALID_ARGUMENT;
        }

        version->abi_version = CSOKIT_NATIVE_ABI_VERSION;
        version->major = CSOKIT_VERSION_MAJOR;
        version->minor = CSOKIT_VERSION_MINOR;
        version->patch = CSOKIT_VERSION_PATCH;

        return CSOKIT_NATIVE_OK;
    }

    int32_t csokit_native_get_capabilities(
        CsoKitNativeCapabilities* capabilities
    )
    {
        if (capabilities == nullptr)
        {
            return CSOKIT_NATIVE_INVALID_ARGUMENT;
        }

        capabilities->abi_version = CSOKIT_NATIVE_ABI_VERSION;
        capabilities->has_zlib = 1;
        capabilities->has_libdeflate = 1;
        capabilities->has_zopfli = 1;
        capabilities->has_7z_deflate = 0;
        capabilities->has_lz4 = 0;

        return CSOKIT_NATIVE_OK;
    }

    int32_t csokit_native_deflate_raw(
        int32_t codec,
        int32_t level,
        int32_t strategy,
        const uint8_t* input,
        size_t input_size,
        uint8_t* output,
        size_t output_capacity,
        size_t* output_size
    )
    {
        (void)strategy;

        switch (codec)
        {
            case CSOKIT_CODEC_ZLIB_DEFAULT:
                return deflate_raw_zlib(level, Z_DEFAULT_STRATEGY, input, input_size, output, output_capacity, output_size);

            case CSOKIT_CODEC_ZLIB_FILTERED:
                return deflate_raw_zlib(level, Z_FILTERED, input, input_size, output, output_capacity, output_size);

            case CSOKIT_CODEC_ZLIB_HUFFMAN_ONLY:
                return deflate_raw_zlib(level, Z_HUFFMAN_ONLY, input, input_size, output, output_capacity, output_size);

            case CSOKIT_CODEC_ZLIB_RLE:
                return deflate_raw_zlib(level, Z_RLE, input, input_size, output, output_capacity, output_size);

            case CSOKIT_CODEC_LIBDEFLATE:
                return deflate_raw_libdeflate(level, input, input_size, output, output_capacity, output_size);

            case CSOKIT_CODEC_ZOPFLI:
                break;

            default:
                return CSOKIT_NATIVE_CODEC_UNAVAILABLE;
        }

        int32_t iterations = level;

        if (iterations < 1)
        {
            iterations = 15;
        }

        if (iterations > 100)
        {
            return CSOKIT_NATIVE_INVALID_ARGUMENT;
        }

        return csokit_native_deflate_zopfli(
            input,
            input_size,
            iterations,
            output,
            output_capacity,
            output_size);
    }

    int32_t csokit_native_inflate_raw(
        const uint8_t* input,
        size_t input_size,
        uint8_t* output,
        size_t output_capacity,
        size_t* output_size
    )
    {
        if ((input == nullptr && input_size != 0) ||
            output == nullptr ||
            output_size == nullptr ||
            input_size > UINT_MAX ||
            output_capacity > UINT_MAX)
        {
            return CSOKIT_NATIVE_INVALID_ARGUMENT;
        }

        z_stream stream{};
        int init = inflateInit2(&stream, -15);

        if (init != Z_OK)
        {
            return CSOKIT_NATIVE_INTERNAL_ERROR;
        }

        stream.next_in = const_cast<Bytef*>(reinterpret_cast<const Bytef*>(input));
        stream.avail_in = static_cast<uInt>(input_size);
        stream.next_out = reinterpret_cast<Bytef*>(output);
        stream.avail_out = static_cast<uInt>(output_capacity);

        int result = inflate(&stream, Z_FINISH);

        if (result != Z_STREAM_END)
        {
            inflateEnd(&stream);
            return result == Z_OK || result == Z_BUF_ERROR
                ? CSOKIT_NATIVE_OUTPUT_TOO_SMALL
                : CSOKIT_NATIVE_INTERNAL_ERROR;
        }

        *output_size = stream.total_out;

        int end = inflateEnd(&stream);
        return end == Z_OK
            ? CSOKIT_NATIVE_OK
            : CSOKIT_NATIVE_INTERNAL_ERROR;
    }

    int32_t csokit_native_deflate_zopfli(
        const uint8_t* input,
        size_t input_size,
        int32_t iterations,
        uint8_t* output,
        size_t output_capacity,
        size_t* output_size
    )
    {
        if ((input == nullptr && input_size != 0) ||
            output == nullptr ||
            output_size == nullptr ||
            iterations < 1 ||
            iterations > 100)
        {
            return CSOKIT_NATIVE_INVALID_ARGUMENT;
        }

        ZopfliOptions options;
        ZopfliInitOptions(&options);
        options.numiterations = iterations;
        options.blocksplitting = 1;
        options.blocksplittinglast = 1;
        options.blocksplittingmax = 15;

        unsigned char* zopfli_output = nullptr;
        size_t zopfli_output_size = 0;

        ZopfliCompress(
            &options,
            ZOPFLI_FORMAT_DEFLATE,
            input,
            input_size,
            &zopfli_output,
            &zopfli_output_size);

        *output_size = zopfli_output_size;

        if (zopfli_output == nullptr && zopfli_output_size != 0)
        {
            return CSOKIT_NATIVE_INTERNAL_ERROR;
        }

        if (zopfli_output_size > output_capacity)
        {
            std::free(zopfli_output);
            return CSOKIT_NATIVE_OUTPUT_TOO_SMALL;
        }

        if (zopfli_output_size > 0)
        {
            std::memcpy(output, zopfli_output, zopfli_output_size);
        }

        std::free(zopfli_output);

        return CSOKIT_NATIVE_OK;
    }
}
