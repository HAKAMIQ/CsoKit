using CsoKit.Core.Formats.DiscImage;

namespace CsoKit.Core.Formats.Containers;

public interface IBlockContainerReader : IDisposable
{
    DetectedDiscFormat Format { get; }

    ulong UncompressedSize { get; }

    uint BlockSize { get; }

    int BlockCount { get; }

    int ReadBlock(int blockIndex, Span<byte> output);
}
