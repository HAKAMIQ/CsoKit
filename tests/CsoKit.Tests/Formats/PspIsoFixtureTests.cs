using CsoKit.Core.Formats.Iso;
using CsoKit.Tests.Fixtures;

namespace CsoKit.Tests.Formats;

public sealed class PspIsoFixtureTests
{
    [Fact]
    public void Analyze_WithValidParamSfo_ReturnsIdentity()
    {
        string path = ContainerFixtures.CreateMinimalPspIsoPath(
            umdDiscId: "ULUS-12345",
            paramDiscId: "ULUS-12345");

        try
        {
            PspIsoValidationResult result = PspIsoValidator.Validate(path);

            Assert.True(result.Success);
            Assert.True(result.HasPspGame);
            Assert.Equal("ULUS-12345", result.DiscIdFromUmdData);
            Assert.Equal("ULUS-12345", result.DiscIdFromParamSfo);
            Assert.Equal("CsoKit Fixture", result.Title);
            Assert.DoesNotContain(result.Warnings, static warning => warning.Contains("mismatch", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Analyze_WithMissingParamSfo_WarnsButDoesNotFailStructure()
    {
        string path = ContainerFixtures.CreateMinimalPspIsoPath(includeParamSfo: false);

        try
        {
            PspIsoValidationResult result = PspIsoValidator.Validate(path);

            Assert.True(result.Success);
            Assert.False(result.HasParamSfo);
            Assert.Contains(result.Warnings, warning => warning.Contains("PARAM.SFO", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Analyze_WithCorruptParamSfo_WarnsButDoesNotCrash()
    {
        string path = ContainerFixtures.CreateMinimalPspIsoPath(corruptParamSfo: true);

        try
        {
            PspIsoValidationResult result = PspIsoValidator.Validate(path);

            Assert.True(result.Success);
            Assert.Contains(result.Warnings, warning => warning.Contains("PARAM.SFO", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Analyze_WithDiscIdMismatch_WarnsButDoesNotFail()
    {
        string path = ContainerFixtures.CreateMinimalPspIsoPath(
            umdDiscId: "ULUS-12345",
            paramDiscId: "ULES-99999");

        try
        {
            PspIsoValidationResult result = PspIsoValidator.Validate(path);

            Assert.True(result.Success);
            Assert.Contains(result.Warnings, warning => warning.Contains("mismatch", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(path);
        }
    }
}