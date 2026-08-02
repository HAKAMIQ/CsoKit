using CsoKit.Core.Formats.Containers;
using CsoKit.Core.Formats.Cso;
using CsoKit.Core.Formats.DiscImage;

namespace CsoKit.Tests.Formats.Cso;

public sealed class DeepVerificationCancellationTests
{
    [Fact]
    public void ContainerDeepVerifier_StopsBetweenBlocksWhenCanceled()
    {
        using CancellationTokenSource cancellation = new();
        using CancelingReader reader = new(cancellation);

        Assert.Throws<OperationCanceledException>(() =>
            ContainerDeepVerifier.Verify(reader, computeSha256: false, cancellation.Token));
        Assert.Equal(1, reader.ReadCount);
    }

    [Fact]
    public void CsoDeepVerifier_RejectsAlreadyCanceledOperationBeforeOpeningInput()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            new CsoDeepVerifier().Verify("does-not-matter.cso", computeSha256: false, cancellation.Token));
    }

    private sealed class CancelingReader(CancellationTokenSource cancellation) : IBlockContainerReader
    {
        public DetectedDiscFormat Format => DetectedDiscFormat.RawIso;
        public ulong UncompressedSize => 4;
        public uint BlockSize => 2;
        public int BlockCount => 2;
        public int ReadCount { get; private set; }

        public int ReadBlock(int blockIndex, Span<byte> output)
        {
            output[..2].Fill((byte)(blockIndex + 1));
            ReadCount++;

            if (ReadCount == 1)
            {
                cancellation.Cancel();
            }

            return 2;
        }

        public void Dispose()
        {
        }
    }
}
