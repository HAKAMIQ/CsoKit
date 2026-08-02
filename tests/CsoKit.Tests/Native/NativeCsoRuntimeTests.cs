using CsoKit.Core.Native;

namespace CsoKit.Tests.Native;

public sealed class NativeCsoRuntimeTests
{
    [Fact]
    public void NativeBackend_IsPresent_AndUsesSupportedAbi()
    {
        NativeCsoRuntimeInfo info = NativeCsoRuntime.GetInfo();
        Assert.True(info.IsAvailable, info.FailureReason ?? "Native backend was not available.");

        NativeCsoCapabilities capabilities = NativeCsoRuntime.GetCapabilities();
        Assert.Equal(NativeCsoRuntime.SupportedAbiVersion, capabilities.AbiVersion);
        Assert.True(capabilities.HasZlib);
        Assert.True(capabilities.HasLibDeflate);
        Assert.True(capabilities.HasZopfli);
    }

    [Fact]
    public void NativeZlibRawDeflate_RoundtripsEachStrategy()
    {
        NativeCsoCapabilities capabilities = NativeCsoRuntime.GetCapabilities();
        Assert.True(capabilities.HasZlib, "Native zlib capability is required by the integration suite.");

        byte[] original = CreateCompressibleSampleBlock();

        NativeCsoRawCodec[] codecs =
        [
            NativeCsoRawCodec.ZlibDefault,
            NativeCsoRawCodec.ZlibFiltered,
            NativeCsoRawCodec.ZlibHuffmanOnly,
            NativeCsoRawCodec.ZlibRle,
        ];

        foreach (NativeCsoRawCodec codec in codecs)
        {
            Assert.True(NativeCsoRuntime.TryDeflateRaw(codec, level: 9, strategy: 0, original, out byte[] compressed));
            Assert.True(NativeCsoRuntime.TryInflateRaw(compressed, original.Length, out byte[] restored));
            Assert.Equal(original, restored);
        }
    }

    [Fact]
    public void NativeLibDeflateRawDeflate_RoundtripsRequestedLevels()
    {
        NativeCsoCapabilities capabilities = NativeCsoRuntime.GetCapabilities();
        Assert.True(capabilities.HasLibDeflate, "Native libdeflate capability is required by the integration suite.");

        byte[] original = CreateCompressibleSampleBlock();
        int[] levels = [1, 6, 9, 12];

        foreach (int level in levels)
        {
            Assert.True(NativeCsoRuntime.TryDeflateRaw(NativeCsoRawCodec.LibDeflate, level, strategy: 0, original, out byte[] compressed));
            Assert.True(NativeCsoRuntime.TryInflateRaw(compressed, original.Length, out byte[] restored));
            Assert.Equal(original, restored);
        }
    }

    [Fact]
    public void NativeZopfli_RoundtripsPublishedAbi()
    {
        NativeCsoCapabilities capabilities = NativeCsoRuntime.GetCapabilities();
        Assert.True(capabilities.HasZopfli, "Native Zopfli capability is required by the integration suite.");

        byte[] original = CreateCompressibleSampleBlock();
        Assert.True(NativeCsoRuntime.TryDeflateZopfli(original, iterations: 2, out byte[] compressed));
        Assert.True(NativeCsoRuntime.TryInflateRaw(compressed, original.Length, out byte[] restored));
        Assert.Equal(original, restored);
    }

    [Theory]
    [InlineData(NativeCsoRawCodec.ZlibHuffmanOnly)]
    [InlineData(NativeCsoRawCodec.ZlibRle)]
    public void NativeZlibRawDeflate_WhenCandidateWouldGrow_ReturnsFalseWithoutOutput(
        NativeCsoRawCodec codec)
    {
        NativeCsoCapabilities capabilities = NativeCsoRuntime.GetCapabilities();
        Assert.True(capabilities.HasZlib, "Native zlib capability is required by the integration suite.");

        byte[] original = CreateExpansionProneSampleBlock();

        bool success = NativeCsoRuntime.TryDeflateRaw(
            codec,
            level: 9,
            strategy: 0,
            original,
            out byte[] compressed);

        // The production API intentionally provides an output buffer no larger than
        // the input. Native OUTPUT_TOO_SMALL therefore means that this codec did not
        // produce a useful CSO candidate and must be rejected without partial output.
        Assert.False(success);
        Assert.Empty(compressed);
    }

    private static byte[] CreateCompressibleSampleBlock()
    {
        byte[] data = new byte[4096];

        for (int index = 0; index < data.Length; index++)
        {
            data[index] = (byte)((index / 512) % 4);
        }

        return data;
    }

    private static byte[] CreateExpansionProneSampleBlock()
    {
        byte[] data = new byte[4096];

        for (int index = 0; index < data.Length; index++)
        {
            data[index] = (byte)((index * 17) % 251);
        }

        return data;
    }
}
