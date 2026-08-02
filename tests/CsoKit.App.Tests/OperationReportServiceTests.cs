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
}
