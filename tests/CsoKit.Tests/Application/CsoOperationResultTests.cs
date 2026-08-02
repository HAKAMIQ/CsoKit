using CsoKit.Application;

namespace CsoKit.Tests.Application;

public sealed class CsoOperationResultTests
{
    [Fact]
    public void Result_UsesTypedDetailLinesAsTheSourceOfRenderedDetails()
    {
        CsoOperationDetail[] detailLines =
        [
            CsoOperationDetail.Field("Input", "game.iso"),
            CsoOperationDetail.Blank(),
            CsoOperationDetail.Section("Warnings"),
            CsoOperationDetail.Bullet("Example warning"),
            CsoOperationDetail.Text("Finished"),
        ];

        CsoOperationResult result = CsoOperationResult.Ok(
            "Completed",
            detailLines,
            inputPath: "game.iso",
            originalBytes: 100,
            resultBytes: 50);

        Assert.Collection(
            result.DetailLines,
            line =>
            {
                Assert.Equal(CsoOperationDetailKind.Field, line.Kind);
                Assert.Equal("Input", line.Label);
                Assert.Equal("game.iso", line.Value);
            },
            line => Assert.Equal(CsoOperationDetailKind.Blank, line.Kind),
            line =>
            {
                Assert.Equal(CsoOperationDetailKind.Section, line.Kind);
                Assert.Equal("Warnings", line.Label);
            },
            line =>
            {
                Assert.Equal(CsoOperationDetailKind.Bullet, line.Kind);
                Assert.Equal("Example warning", line.Value);
            },
            line =>
            {
                Assert.Equal(CsoOperationDetailKind.Text, line.Kind);
                Assert.Equal("Finished", line.Value);
            });

        Assert.Equal(
            "Input: game.iso" + Environment.NewLine +
            Environment.NewLine +
            "Warnings:" + Environment.NewLine +
            "- Example warning" + Environment.NewLine +
            "Finished" + Environment.NewLine,
            result.Details);
        Assert.Equal(100, result.OriginalBytes);
        Assert.Equal(50, result.ResultBytes);
    }
}
