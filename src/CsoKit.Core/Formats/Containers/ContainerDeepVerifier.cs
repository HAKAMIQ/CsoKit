using System.Buffers;
using System.Security.Cryptography;
using CsoKit.Core.Formats.Cso;
using CsoKit.Core.Formats.DiscImage;

namespace CsoKit.Core.Formats.Containers;

public static class ContainerDeepVerifier
{
    public static CsoDeepVerifyResult Verify(
        IBlockContainerReader reader,
        bool computeSha256,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        cancellationToken.ThrowIfCancellationRequested();

        if (reader.BlockSize == 0 || reader.BlockSize > int.MaxValue)
        {
            return Decorate(
                CsoDeepVerifyResult.Fail(
                    header: null,
                    blocksChecked: 0,
                    bytesReconstructed: 0,
                    [new CsoDeepVerifyIssue(
                        "InvalidBlockSize",
                        $"{reader.Format} block size is invalid for deep verification.")]),
                reader,
                zeroBlocks: 0,
                payloadBlocksDecoded: 0);
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(checked((int)reader.BlockSize));
        using IncrementalHash? hash = computeSha256
            ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
            : null;

        int blocksChecked = 0;
        int zeroBlocks = 0;
        ulong bytesReconstructed = 0;

        try
        {
            for (int blockIndex = 0; blockIndex < reader.BlockCount; blockIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int bytesRead = reader.ReadBlock(blockIndex, buffer);

                if (bytesRead <= 0)
                {
                    return Decorate(
                        FailAt(
                            reader,
                            blocksChecked,
                            bytesReconstructed,
                            "CorruptCompressedBlock",
                            $"{reader.Format} block {blockIndex:N0} decoded to an empty payload. Re-dump required.",
                            blockIndex),
                        reader,
                        zeroBlocks,
                        payloadBlocksDecoded: blocksChecked);
                }

                ReadOnlySpan<byte> decoded = buffer.AsSpan(0, bytesRead);

                if (IsAllZero(decoded))
                {
                    zeroBlocks++;
                }

                hash?.AppendData(decoded);
                bytesReconstructed = checked(bytesReconstructed + (ulong)bytesRead);
                blocksChecked++;
            }
        }
        catch (BlockContainerReadException ex)
        {
            return Decorate(
                FailAt(
                    reader,
                    blocksChecked,
                    bytesReconstructed,
                    ex.Code,
                    ex.Message,
                    ex.BlockIndex),
                reader,
                zeroBlocks,
                payloadBlocksDecoded: blocksChecked);
        }
        catch (EndOfStreamException ex)
        {
            return Decorate(
                FailAt(
                    reader,
                    blocksChecked,
                    bytesReconstructed,
                    "UnexpectedEndOfFile",
                    ex.Message,
                    blocksChecked),
                reader,
                zeroBlocks,
                payloadBlocksDecoded: blocksChecked);
        }
        catch (IOException ex)
        {
            return Decorate(
                FailAt(
                    reader,
                    blocksChecked,
                    bytesReconstructed,
                    "DeepVerifyIoFailed",
                    ex.Message,
                    blocksChecked),
                reader,
                zeroBlocks,
                payloadBlocksDecoded: blocksChecked);
        }
        catch (InvalidDataException ex)
        {
            return Decorate(
                FailAt(
                    reader,
                    blocksChecked,
                    bytesReconstructed,
                    "CorruptCompressedBlock",
                    ex.Message,
                    blocksChecked),
                reader,
                zeroBlocks,
                payloadBlocksDecoded: blocksChecked);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (bytesReconstructed != reader.UncompressedSize)
        {
            return Decorate(
                CsoDeepVerifyResult.Fail(
                    header: null,
                    blocksChecked,
                    bytesReconstructed,
                    [new CsoDeepVerifyIssue(
                        "ReconstructedSizeMismatch",
                        $"{reader.Format} deep verify reconstructed {bytesReconstructed:N0} bytes, expected {reader.UncompressedSize:N0} bytes.")]),
                reader,
                zeroBlocks,
                payloadBlocksDecoded: blocksChecked);
        }

        string? sha256 = hash is null
            ? null
            : Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();

        return Decorate(
            CsoDeepVerifyResult.Ok(
                header: null,
                blocksChecked,
                bytesReconstructed,
                sha256),
            reader,
            zeroBlocks,
            payloadBlocksDecoded: blocksChecked);
    }

    private static CsoDeepVerifyResult Decorate(
        CsoDeepVerifyResult result,
        IBlockContainerReader reader,
        int zeroBlocks,
        int payloadBlocksDecoded)
    {
        bool isRawIso = reader.Format is DetectedDiscFormat.RawIso;

        return result with
        {
            AlgorithmName = isRawIso ? "Hybrid raw ISO verification" : "Hybrid container verification",
            VerificationScope = isRawIso
                ? "ISO9660 probe + raw sector read + full payload reconstruction"
                : "Container header + block payload reconstruction",
            LegacyLayer = isRawIso
                ? "ISO9660 primary-volume probe and strict 2048-byte sector-alignment validation"
                : "Container header and block-table validation through the reader",
            ModernLayer = isRawIso
                ? "Sequential raw-sector read with pooled output buffers"
                : "Streaming block decode with pooled output buffers",
            ForensicLayer = isRawIso
                ? "Coverage, zero-content, bounds, and reconstruction diagnostics"
                : "Coverage, zero-block, and reconstruction diagnostics",
            TotalBlocks = reader.BlockCount,
            ExpectedReconstructedBytes = reader.UncompressedSize,
            PhysicalPayloadBytes = isRawIso ? reader.UncompressedSize : 0,
            CompressedBlocks = 0,
            StoredBlocks = isRawIso ? reader.BlockCount : 0,
            ZeroBlocks = zeroBlocks,
            PayloadBlocksDecoded = payloadBlocksDecoded,
        };
    }

    private static CsoDeepVerifyResult FailAt(
        IBlockContainerReader reader,
        int blocksChecked,
        ulong bytesReconstructed,
        string code,
        string message,
        int? blockIndex)
    {
        return CsoDeepVerifyResult.Fail(
            header: null,
            blocksChecked,
            bytesReconstructed,
            [new CsoDeepVerifyIssue(
                code,
                string.IsNullOrWhiteSpace(message)
                    ? $"{reader.Format} deep verification failed."
                    : message,
                blockIndex)]);
    }

    private static bool IsAllZero(ReadOnlySpan<byte> data)
    {
        return data.IndexOfAnyExcept((byte)0) < 0;
    }
}
