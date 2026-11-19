using CsoKit.App.Localization;
using CsoKit.App.Models;
using CsoKit.App.Services;
using CsoKit.Application;

namespace CsoKit.App.Tests;

public sealed class OperationReportServiceTests
{
    [Fact]
    public void ArabicReport_RendersTypedFieldsWithoutViewModelTextParsing()
    {
        string root = Path.Combine(Path.GetTempPath(), $"csokit-report-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string inputPath = Path.Combine(root, "game.iso");
        File.WriteAllBytes(inputPath, [0]);

        try
        {
            CsoOperationResult result = CsoOperationResult.Ok(
                "Compress completed",
                [
                    CsoOperationDetail.Field("Input", "game.iso"),
                    CsoOperationDetail.Field("Bytes written", "512"),
                ],
                inputPath: inputPath,
                originalBytes: 1024,
                resultBytes: 512);

            OperationReportWriteResult write = OperationReportService.TryWrite(
                new OperationReportRequest(UiOperationKind.Compress, inputPath, string.Empty),
                result,
                "ضغط",
                UiLanguage.Arabic);

            Assert.True(write.Success, write.ErrorMessage);
            string report = File.ReadAllText(write.ReportPath!);
            Assert.Contains("الإدخال: game.iso", report, StringComparison.Ordinal);
            Assert.Contains("البايتات المكتوبة: 512", report, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EnglishReport_IncludesOperationStatusPathsAndDetails()
    {
        using TempDirectory temp = new();
        string input = temp.CreateFile("game.iso");
        string output = temp.Combine("game.cso");
        CsoOperationResult result = CsoOperationResult.Ok(
            "Compress completed",
            [CsoOperationDetail.Field("Bytes written", "512")],
            inputPath: input);

        OperationReportWriteResult write = OperationReportService.TryWrite(
            new OperationReportRequest(UiOperationKind.Compress, input, output),
            result,
            "Compress",
            UiLanguage.English);

        Assert.True(write.Success, write.ErrorMessage);
        Assert.Equal(temp.Combine("game.compress-report.txt"), write.ReportPath);
        string report = File.ReadAllText(write.ReportPath!);
        Assert.Contains("CsoKit Operation Report", report, StringComparison.Ordinal);
        Assert.Contains("Operation: Compress", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Compress (Compress)", report, StringComparison.Ordinal);
        Assert.Contains("Success: True", report, StringComparison.Ordinal);
        Assert.Contains("Status: Compress completed", report, StringComparison.Ordinal);
        Assert.Contains($"Input: {input}", report, StringComparison.Ordinal);
        Assert.Contains($"Output: {output}", report, StringComparison.Ordinal);
        Assert.Contains("Bytes written: 512", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Report_WithoutOutputPath_OmitsOutputLineAndUsesInputDirectory()
    {
        using TempDirectory temp = new();
        string input = temp.CreateFile("game.cso");

        OperationReportWriteResult write = OperationReportService.TryWrite(
            new OperationReportRequest(UiOperationKind.Verify, input, string.Empty),
            CsoOperationResult.Ok("Shallow verify passed; no header/index corruption detected", []),
            "Verify",
            UiLanguage.English);

        Assert.True(write.Success, write.ErrorMessage);
        Assert.Equal(temp.Combine("game.verify-report.txt"), write.ReportPath);
        Assert.DoesNotContain("Output:", File.ReadAllText(write.ReportPath!), StringComparison.Ordinal);
    }

    [Fact]
    public void Report_UsesOutputDirectoryWhenOutputIsElsewhere()
    {
        using TempDirectory temp = new();
        string input = temp.CreateFile("game.iso");
        string outputDirectory = Directory.CreateDirectory(temp.Combine("out")).FullName;

        OperationReportWriteResult write = OperationReportService.TryWrite(
            new OperationReportRequest(UiOperationKind.Compress, input, Path.Combine(outputDirectory, "game.cso")),
            CsoOperationResult.Ok("Compress completed", []),
            "Compress",
            UiLanguage.English);

        Assert.True(write.Success, write.ErrorMessage);
        Assert.Equal(Path.Combine(outputDirectory, "game.compress-report.txt"), write.ReportPath);
    }

    [Fact]
    public void Report_NeverOverwritesAnExistingReport()
    {
        using TempDirectory temp = new();
        string input = temp.CreateFile("game.cso");
        OperationReportRequest request = new(UiOperationKind.Detect, input, string.Empty);
        CsoOperationResult result = CsoOperationResult.Ok("Detect completed", []);

        OperationReportWriteResult first = OperationReportService.TryWrite(request, result, "Detect", UiLanguage.English);
        OperationReportWriteResult second = OperationReportService.TryWrite(request, result, "Detect", UiLanguage.English);
        OperationReportWriteResult third = OperationReportService.TryWrite(request, result, "Detect", UiLanguage.English);

        Assert.Equal(temp.Combine("game.detect-report.txt"), first.ReportPath);
        Assert.Equal(temp.Combine("game.detect-report-2.txt"), second.ReportPath);
        Assert.Equal(temp.Combine("game.detect-report-3.txt"), third.ReportPath);
    }

    [Theory]
    [InlineData("game.repaired.cso")]
    [InlineData("game - CsoKit Repaired.cso")]
    [InlineData("game.repaired.repaired.cso")]
    public void Report_StripsGeneratedRepairSuffixesFromBaseName(string inputName)
    {
        using TempDirectory temp = new();
        string input = temp.CreateFile(inputName);

        OperationReportWriteResult write = OperationReportService.TryWrite(
            new OperationReportRequest(UiOperationKind.Repair, input, string.Empty),
            CsoOperationResult.Ok("Repair completed; recoverable input issues were detected", []),
            "Repair",
            UiLanguage.English);

        Assert.Equal(temp.Combine("game.repair-report.txt"), write.ReportPath);
    }

    [Fact]
    public void ArabicReport_TranslatesStatusSectionsVerdictsAndBullets()
    {
        using TempDirectory temp = new();
        string input = temp.CreateFile("game.cso");
        CsoOperationResult result = CsoOperationResult.Fail(
            "Deep verify failed; corruption detected",
            [
                CsoOperationDetail.Section("Issues:"),
                CsoOperationDetail.Bullet("Block 3 failed to inflate"),
                CsoOperationDetail.Bullet("none"),
                CsoOperationDetail.Blank(),
                CsoOperationDetail.Field("Corruption detected", "True"),
                CsoOperationDetail.Field("Repair needed", "False"),
                CsoOperationDetail.Field("SHA256", "Disabled"),
                CsoOperationDetail.Field("Format", "True"),
                CsoOperationDetail.Field("Physical file size", "4096 bytes"),
                CsoOperationDetail.Field("Custom label", "Passed"),
            ]);

        OperationReportWriteResult write = OperationReportService.TryWrite(
            new OperationReportRequest(UiOperationKind.Verify, input, string.Empty),
            result,
            "فحص السلامة",
            UiLanguage.Arabic);

        Assert.True(write.Success, write.ErrorMessage);
        string report = File.ReadAllText(write.ReportPath!);
        Assert.Contains("العملية: فحص السلامة", report, StringComparison.Ordinal);
        Assert.DoesNotContain("فحص السلامة (فحص السلامة)", report, StringComparison.Ordinal);
        Assert.Contains("النتيجة النهائية: فشل", report, StringComparison.Ordinal);
        Assert.Contains("الحالة: فشل الفحص العميق؛ تم اكتشاف تلف أو مشكلة بنيوية", report, StringComparison.Ordinal);
        Assert.Contains("المشاكل:", report, StringComparison.Ordinal);
        Assert.Contains("- Block 3 failed to inflate", report, StringComparison.Ordinal);
        Assert.Contains("- لا توجد", report, StringComparison.Ordinal);
        Assert.Contains("هل تم اكتشاف تلف: نعم — تم اكتشاف تلف أو خلل بنيوي", report, StringComparison.Ordinal);
        Assert.Contains("هل يحتاج إصلاح: لا — لا يحتاج إصلاح", report, StringComparison.Ordinal);
        Assert.Contains("SHA256: معطّل", report, StringComparison.Ordinal);
        Assert.Contains("الصيغة: True", report, StringComparison.Ordinal);
        Assert.Contains("حجم الملف الفعلي: 4096 بايت", report, StringComparison.Ordinal);
        Assert.Contains("Custom label: سليم", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ArabicReport_KeepsUnknownStatusVerbatim()
    {
        using TempDirectory temp = new();
        string input = temp.CreateFile("game.cso");

        OperationReportWriteResult write = OperationReportService.TryWrite(
            new OperationReportRequest(UiOperationKind.Verify, input, string.Empty),
            CsoOperationResult.Ok("Some future status", []),
            "فحص السلامة",
            UiLanguage.Arabic);

        Assert.Contains("الحالة: Some future status", File.ReadAllText(write.ReportPath!), StringComparison.Ordinal);
    }

    [Fact]
    public void TryWrite_WithInvalidPath_ReturnsErrorInsteadOfThrowing()
    {
        OperationReportWriteResult write = OperationReportService.TryWrite(
            new OperationReportRequest(UiOperationKind.Detect, "bad\0path.iso", string.Empty),
            CsoOperationResult.Ok("Detect completed", []),
            "Detect",
            UiLanguage.English);

        Assert.False(write.Success);
        Assert.Null(write.ReportPath);
        Assert.False(string.IsNullOrWhiteSpace(write.ErrorMessage));
    }
}
