using CsoKit.Core.Formats.Cso;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoDecompressorCancellationTests
{
    [Fact]
    public void Decompress_WithAlreadyCanceledToken_ReturnsOperationCanceled()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        CsoDecompressor decompressor = new();

        CsoDecompressResult result = decompressor.Decompress(
            new CsoDecompressOptions(
                InputPath: "input.cso",
                OutputPath: "output.iso",
                ForceOverwrite: false,
                Progress: null,
                CancellationToken: cancellation.Token));

        Assert.False(result.Success);
        Assert.Equal("OperationCanceled", result.ErrorCode);
    }
}