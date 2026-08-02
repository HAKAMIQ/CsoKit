using CsoKit.Core.Formats.Cso;
using CsoKit.Tests.Formats.Iso;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoRepairerTests
{
    private static readonly string[] UnsupportedContainerRepairErrorCodes =
    [
        "UnsupportedContainer",
        "InvalidBlockSize",
        "RepairNotPossible",
    ];

    [Fact]
    public void Repair_WithAlignedIso_RebuildsGameSafeCso()
    {
        byte[] iso = PspIsoValidatorTests.CreateMinimalPspIso(includeRequiredPaths: true);
        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");
        string outputIsoPath = CreateTempPath(".out.iso");

        try
        {
            File.WriteAllBytes(isoPath, iso);

            CsoRepairResult repair = CsoRepairer.Repair(
                new CsoRepairOptions(
                    isoPath,
                    csoPath,
                    ForceOverwrite: false,
                    DeepVerify: true));

            Assert.True(repair.Success, repair.ErrorMessage);
            Assert.Equal("RawIso", repair.InputFormat);
            Assert.True(File.Exists(csoPath));

            CsoDeepVerifyResult deep = new CsoDeepVerifier().Verify(csoPath, computeSha256: false);
            Assert.True(deep.Success);

            CsoDecompressResult decompress = new CsoDecompressor().Decompress(
                new CsoDecompressOptions(csoPath, outputIsoPath, ForceOverwrite: false));

            Assert.True(decompress.Success, decompress.ErrorMessage);
            Assert.Equal(iso, File.ReadAllBytes(outputIsoPath));
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
            File.Delete(outputIsoPath);
        }
    }

    [Fact]
    public void Repair_WithCodecReport_ReturnsBoundedTrialSummary()
    {
        byte[] original = new byte[4096];
        string inputPath = CsoTestFileFactory.CreateTempZso(original);
        string outputCsoPath = CreateTempPath(".codec-report.cso");

        try
        {
            CsoRepairResult repair = CsoRepairer.Repair(
                new CsoRepairOptions(
                    inputPath,
                    outputCsoPath,
                    ForceOverwrite: false,
                    DeepVerify: true,
                    CollectCodecReport: true,
                    CodecReportBlockLimit: 1));

            Assert.True(repair.Success, repair.ErrorMessage);
            Assert.NotNull(repair.CodecTrialSummary);
            Assert.Equal(2, repair.CodecTrialSummary.BlocksReported);
            Assert.Single(repair.CodecTrialSummary.Blocks);
            Assert.NotEmpty(repair.CodecTrialSummary.CandidateAttempts);
        }
        finally
        {
            File.Delete(inputPath);
            File.Delete(outputCsoPath);
        }
    }

    [Fact]
    public void Repair_WithUnalignedIsoAndNoPadding_ReturnsIsoNotSectorAligned()
    {
        byte[] alignedIso = PspIsoValidatorTests.CreateMinimalPspIso(includeRequiredPaths: true);
        byte[] unalignedIso = new byte[alignedIso.Length + 1];
        alignedIso.CopyTo(unalignedIso, 0);
        unalignedIso[^1] = 0x01;
        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");

        try
        {
            File.WriteAllBytes(isoPath, unalignedIso);

            CsoRepairResult repair = CsoRepairer.Repair(
                new CsoRepairOptions(
                    isoPath,
                    csoPath,
                    ForceOverwrite: false,
                    DeepVerify: true));

            Assert.False(repair.Success);
            Assert.Equal("IsoNotSectorAligned", repair.ErrorCode);
            Assert.False(File.Exists(csoPath));
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
        }
    }

    [Fact]
    public void Repair_WithCorruptCso_ReturnsReDumpRequiredAndNoOutput()
    {
        byte[] original = new byte[4096];
        string inputCsoPath = CsoTestFileFactory.CreateTempCsoV1(original);
        string outputCsoPath = CreateTempPath(".repaired.cso");

        try
        {
            CsoVerificationResult shallow = new CsoVerifier().Verify(inputCsoPath);
            Assert.True(shallow.Success);

            using (FileStream output = new(inputCsoPath, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                output.Position = checked((long)shallow.Entries[0].Offset);
                output.Write([0xFF, 0xFF, 0xFF, 0xFF]);
            }

            CsoRepairResult repair = CsoRepairer.Repair(
                new CsoRepairOptions(
                    inputCsoPath,
                    outputCsoPath,
                    ForceOverwrite: false,
                    DeepVerify: true));

            Assert.False(repair.Success);
            Assert.Equal("ReDumpRequired", repair.ErrorCode);
            Assert.False(File.Exists(outputCsoPath));
        }
        finally
        {
            File.Delete(inputCsoPath);
            File.Delete(outputCsoPath);
        }
    }

    [Theory]
    [InlineData("zso")]
    [InlineData("dax")]
    [InlineData("cso2")]
    public void Repair_WithUnsupportedContainerVariant_ReturnsClearErrorAndNoOutput(string kind)
    {
        string inputPath = kind switch
        {
            "zso" => CsoTestFileFactory.CreateUnsupportedZsoHeader(),
            "dax" => CsoTestFileFactory.CreateUnsupportedDaxHeader(),
            "cso2" => CsoTestFileFactory.CreateUnsupportedCso2Header(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        string outputCsoPath = CreateTempPath(".normalized.cso");

        try
        {
            CsoRepairResult repair = CsoRepairer.Repair(
                new CsoRepairOptions(
                    inputPath,
                    outputCsoPath,
                    ForceOverwrite: false,
                    DeepVerify: true));

            Assert.False(repair.Success);
            Assert.False(File.Exists(outputCsoPath));
            Assert.Contains(repair.ErrorCode, UnsupportedContainerRepairErrorCodes);
        }
        finally
        {
            File.Delete(inputPath);
            File.Delete(outputCsoPath);
        }
    }

    [Theory]
    [InlineData("zso")]
    [InlineData("dax")]
    [InlineData("cso2")]
    public void Repair_WithSupportedContainerSample_RebuildsCso1AndDeepVerifies(string kind)
    {
        byte[] original = new byte[4096];
        string inputPath = kind switch
        {
            "zso" => CsoTestFileFactory.CreateTempZso(original),
            "dax" => CsoTestFileFactory.CreateTempDax(original),
            "cso2" => CsoTestFileFactory.CreateTempCso2(original),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        string outputCsoPath = CreateTempPath(".normalized.cso");
        string outputIsoPath = CreateTempPath(".normalized.iso");

        try
        {
            CsoRepairResult repair = CsoRepairer.Repair(
                new CsoRepairOptions(
                    inputPath,
                    outputCsoPath,
                    ForceOverwrite: false,
                    DeepVerify: true));

            Assert.True(repair.Success, repair.ErrorMessage);
            Assert.True(File.Exists(outputCsoPath));

            CsoDeepVerifyResult deep = new CsoDeepVerifier().Verify(outputCsoPath, computeSha256: false);
            Assert.True(deep.Success);

            CsoDecompressResult decompress = new CsoDecompressor().Decompress(
                new CsoDecompressOptions(outputCsoPath, outputIsoPath, ForceOverwrite: false));

            Assert.True(decompress.Success, decompress.ErrorMessage);
            Assert.Equal(original, File.ReadAllBytes(outputIsoPath));
        }
        finally
        {
            File.Delete(inputPath);
            File.Delete(outputCsoPath);
            File.Delete(outputIsoPath);
        }
    }

    [Theory]
    [InlineData("zso")]
    [InlineData("cso2")]
    public void Repair_WithCorruptLz4Container_ReturnsReDumpRequiredAndNoOutput(string kind)
    {
        byte[] original = CreateModuloPatternBytes(4096, 251);
        string inputPath = kind == "zso"
            ? CsoTestFileFactory.CreateTempZso(original)
            : CsoTestFileFactory.CreateTempCso2(original);
        string outputCsoPath = CreateTempPath(".corrupt.normalized.cso");

        try
        {
            CorruptFirstPayloadByte(inputPath);

            CsoRepairResult repair = CsoRepairer.Repair(
                new CsoRepairOptions(
                    inputPath,
                    outputCsoPath,
                    ForceOverwrite: false,
                    DeepVerify: true));

            Assert.False(repair.Success);
            Assert.Equal("ReDumpRequired", repair.ErrorCode);
            Assert.False(File.Exists(outputCsoPath));
        }
        finally
        {
            File.Delete(inputPath);
            File.Delete(outputCsoPath);
        }
    }

    private static void CorruptFirstPayloadByte(string path)
    {
        const int headerSize = 24;
        const int indexEntryCountFor4096BytesAt2048BlockSize = 3;
        const int firstPayloadOffset = headerSize + (indexEntryCountFor4096BytesAt2048BlockSize * sizeof(uint));

        using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        stream.Position = firstPayloadOffset;

        stream.WriteByte(0x00);
    }

    private static byte[] CreateModuloPatternBytes(int length, int modulo)
    {
        byte[] bytes = new byte[length];

        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = checked((byte)(index % modulo));
        }

        return bytes;
    }

    private static string CreateTempPath(string extension)
    {
        string fileName = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture)[..8];
        return Path.Combine(Path.GetTempPath(), fileName + Path.GetExtension(extension));
    }
}