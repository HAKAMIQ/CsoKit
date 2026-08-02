using CsoKit.Core.Formats.DiscImage;

namespace CsoKit.Core.Formats.Containers;

public static class BlockContainerReaderFactory
{
    public static IBlockContainerReader Create(
        string inputPath,
        DetectedDiscFormat format,
        bool allowRawIso = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        return format switch
        {
            DetectedDiscFormat.Cso1 => new Cso1ContainerReader(inputPath),
            DetectedDiscFormat.Cso2 => new Cso2ContainerReader(inputPath),
            DetectedDiscFormat.Zso => new ZsoContainerReader(inputPath),
            DetectedDiscFormat.Dax => new DaxContainerReader(inputPath),
            DetectedDiscFormat.RawIso when allowRawIso => new IsoContainerReader(inputPath),
            _ => throw new BlockContainerReadException(
                "UnsupportedContainer",
                $"{format} is not supported by this container operation.")
        };
    }
}
