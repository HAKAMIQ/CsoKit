using CsoKit.Core.Formats.Cso;
using CsoKit.Core.Formats.DiscImage;

namespace CsoKit.Core.Formats.Containers;

public sealed class Cso1ContainerReader(string inputPath) : CsoLikeContainerReader(
    inputPath,
    CsoConstants.MagicText,
    static version => version is 0 or 1,
    "CSO1")
{
    public override DetectedDiscFormat Format => DetectedDiscFormat.Cso1;

    protected override int ReadPayload(
        FileStream input,
        CsoIndexEntry current,
        ulong physicalSize,
        int blockIndex,
        Span<byte> output)
    {
        if (current.HasFlag)
        {
            return ReadStored(input, physicalSize, blockIndex, output, "CSO1");
        }

        byte[] payload = ReadPayloadBytes(input, physicalSize, blockIndex, "CSO1");
        return InflateRawDeflate(payload, blockIndex, output, "CSO1");
    }
}