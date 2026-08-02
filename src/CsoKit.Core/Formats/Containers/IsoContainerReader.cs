using CsoKit.Core.Formats.Cso;
using CsoKit.Core.Formats.DiscImage;

namespace CsoKit.Core.Formats.Containers;

public sealed class IsoContainerReader : IBlockContainerReader
{
    private readonly FileStream input;

    public IsoContainerReader(string inputPath, uint blockSize = CsoCompressor.DefaultBlockSize)
    {
        ArgumentOutOfRangeException.ThrowIfZero(blockSize);

        input = new FileStream(
            inputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 128,
            FileOptions.SequentialScan);

        BlockSize = blockSize;
        UncompressedSize = checked((ulong)input.Length);
        BlockCount = checked((int)((UncompressedSize + blockSize - 1) / blockSize));
    }

    public DetectedDiscFormat Format => DetectedDiscFormat.RawIso;

    public ulong UncompressedSize { get; }

    public uint BlockSize { get; }

    public int BlockCount { get; }

    public int ReadBlock(int blockIndex, Span<byte> output)
    {
        if ((uint)blockIndex >= (uint)BlockCount)
        {
            throw new ArgumentOutOfRangeException(nameof(blockIndex));
        }

        ulong offset = checked((ulong)blockIndex * BlockSize);
        int expectedBytes = checked((int)Math.Min(BlockSize, UncompressedSize - offset));

        if (output.Length < expectedBytes)
        {
            throw new ArgumentException("Output buffer is smaller than the ISO block.", nameof(output));
        }

        input.Position = checked((long)offset);
        int total = 0;

        while (total < expectedBytes)
        {
            int read = input.Read(output[total..expectedBytes]);

            if (read == 0)
            {
                throw new BlockContainerReadException(
                    "UnexpectedEndOfFile",
                    $"ISO block {blockIndex} ended unexpectedly.",
                    blockIndex);
            }

            total += read;
        }

        return expectedBytes;
    }

    public void Dispose()
    {
        input.Dispose();
    }
}