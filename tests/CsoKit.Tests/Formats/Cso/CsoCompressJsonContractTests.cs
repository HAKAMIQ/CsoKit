using System.Text.Json;
using System.Text.Json.Serialization;
using CsoKit.Application;
using CsoKit.Cli.Commands;
using CsoKit.Core.Formats.Cso;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoCompressJsonContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void Measure_WithFastProfile_EmitsStableProfileContract()
    {
        CsoCompressionProfileSettings settings = CsoCompressionProfilePolicy.Create(CsoCompressionProfile.Fast);
        CsoMeasureResult result = CsoMeasureResult.Ok(
            originalBytes: 4096,
            estimatedBytes: 2048,
            totalBlocks: 2,
            compressedBlocks: 1,
            storedBlocks: 1);

        CsoMeasureJsonOutput output = CsoCompressJsonContract.Measure(
            "D:\\Games\\game.iso",
            settings,
            result);

        Assert.Equal(1, output.SchemaVersion);
        Assert.Equal("compress", output.Command);
        Assert.Equal("measure", output.Mode);
        Assert.True(output.Success);
        Assert.Equal("fast", output.Options.Profile.Name);
        Assert.True(output.Options.Profile.Fast);
        Assert.Equal(1, output.Options.Profile.Level);
        Assert.Equal(4096UL, output.Metrics.OriginalBytes);
        Assert.Equal(2048UL, output.Metrics.EstimatedBytes);
        Assert.Equal(0.5, output.Metrics.EstimatedRatio);
        Assert.Null(output.Error);
    }

    [Fact]
    public void Write_WithSmallestProfile_EmitsStableProfileContract()
    {
        CsoCompressionProfileSettings settings = CsoCompressionProfilePolicy.Create(CsoCompressionProfile.Smallest);
        CsoCompressResult result = CsoCompressResult.Ok(
            bytesRead: 4096,
            bytesWritten: 2048,
            compressedBlocks: 2,
            storedBlocks: 0);

        CsoWriteJsonOutput output = CsoCompressJsonContract.Write(
            "D:\\Games\\game.iso",
            "D:\\Games\\game.cso",
            force: false,
            autoOutput: true,
            settings,
            result);

        Assert.Equal(1, output.SchemaVersion);
        Assert.Equal("compress", output.Command);
        Assert.Equal("write", output.Mode);
        Assert.True(output.Success);
        Assert.Equal("smallest", output.Options.Profile.Name);
        Assert.False(output.Options.Profile.Fast);
        Assert.Equal(9, output.Options.Profile.Level);
        Assert.False(output.Options.Force);
        Assert.True(output.Options.AutoOutput);
        CsoWriteJsonMetrics metrics = Assert.IsType<CsoWriteJsonMetrics>(output.Metrics);
        Assert.Equal(4096UL, metrics.BytesRead);
        Assert.Equal(2048UL, metrics.BytesWritten);
        Assert.Null(output.Error);
    }


    [Fact]
    public void Write_WithEarlyFailureAndNoData_EmitsNullableMetrics()
    {
        CsoCompressionProfileSettings settings = CsoCompressionProfilePolicy.Create(CsoCompressionProfile.Smallest);
        CsoOperationResult operation = CsoOperationResult.Fail(
            "Compress failed",
            [],
            errorCode: "OutputFileNameTooShort",
            inputPath: "D:\\Games\\game.iso",
            outputPath: "D:\\Games\\A.cso");

        CsoWriteJsonOutput output = CsoCompressJsonContract.Write(
            "D:\\Games\\game.iso",
            "D:\\Games\\A.cso",
            force: false,
            autoOutput: false,
            settings,
            operation);

        Assert.False(output.Success);
        Assert.Null(output.Metrics);
        Assert.Null(output.CodecReport);
        Assert.Equal("OutputFileNameTooShort", output.Error?.Code);
    }

    [Fact]
    public void ArgumentError_EmitsStableErrorContract()
    {
        CsoArgumentErrorJsonOutput output = CsoCompressJsonContract.ArgumentError("Invalid compression profile.");

        Assert.Equal(1, output.SchemaVersion);
        Assert.Equal("compress", output.Command);
        Assert.Equal("arguments", output.Mode);
        Assert.False(output.Success);
        Assert.Equal("InvalidArguments", output.Error.Code);
        Assert.Equal("Invalid compression profile.", output.Error.Message);
    }

    [Fact]
    public void MeasureContract_SerializesWithCamelCaseProfileFields()
    {
        CsoCompressionProfileSettings settings = CsoCompressionProfilePolicy.Create(CsoCompressionProfile.Compat);
        CsoMeasureResult result = CsoMeasureResult.Ok(
            originalBytes: 100,
            estimatedBytes: 50,
            totalBlocks: 1,
            compressedBlocks: 1,
            storedBlocks: 0);

        string json = JsonSerializer.Serialize(
            CsoCompressJsonContract.Measure("game.iso", settings, result),
            JsonOptions);

        Assert.Contains("\"schemaVersion\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"profile\":", json, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"compat\"", json, StringComparison.Ordinal);
        Assert.Contains("\"fast\":false", json, StringComparison.Ordinal);
        Assert.Contains("\"level\":9", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"error\":", json, StringComparison.Ordinal);
    }
}
