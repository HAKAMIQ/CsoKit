using CsoKit.Core.Formats.Cso;
using CsoKit.Core.Formats.DiscImage;

namespace CsoKit.Core.Formats.Containers;

public sealed class Cso2ContainerReader(string inputPath) : CsoLikeContainerReader(
    inputPath,
    CsoConstants.MagicText,
    static version => version == 2,
    "CSO2")
{
    public override DetectedDiscFormat Format => DetectedDiscFormat.Cso2;

    protected override int ReadPayload(
        FileStream input,
        CsoIndexEntry current,
        ulong physicalSize,
        int blockIndex,
        Span<byte> output)
    {
        if (current.HasFlag)
        {
            byte[] lz4Payload = ReadPayloadBytes(input, physicalSize, blockIndex, "CSO2");
            return DecodeLz4Payload(lz4Payload, blockIndex, output, "CSO2");
        }

        if (physicalSize >= (ulong)output.Length)
        {
            return ReadStored(input, physicalSize, blockIndex, output, "CSO2");
        }

        byte[] deflatePayload = ReadPayloadBytes(input, physicalSize, blockIndex, "CSO2");
        return InflateRawDeflate(deflatePayload, blockIndex, output, "CSO2");
    }
}