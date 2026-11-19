using CsoKit.Core.Formats.Cso;
using CsoKit.Core.Formats.DiscImage;

namespace CsoKit.Core.Formats.Containers;

public sealed class ZsoContainerReader(string inputPath) : CsoLikeContainerReader(
    inputPath,
    "ZISO",
    static version => version is 0 or 1 or 2,
    "ZSO")
{
    public override DetectedDiscFormat Format => DetectedDiscFormat.Zso;

    protected override bool FinalBlockMayEndPastSentinel => true;

    protected override int ReadPayload(
        FileStream input,
        CsoIndexEntry current,
        ulong physicalSize,
        int blockIndex,
        Span<byte> output)
    {
        if (current.HasFlag)
        {
            return ReadStored(input, physicalSize, blockIndex, output, "ZSO");
        }

        byte[] payload = ReadPayloadBytes(input, physicalSize, blockIndex, "ZSO");
        return DecodeLz4Payload(payload, blockIndex, output, "ZSO");
    }
}