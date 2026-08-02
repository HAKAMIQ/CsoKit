using System.Globalization;
using System.Text.Json;
using CsoKit.Cli;
using CsoKit.Cli.Commands;
using CsoKit.Core.Formats.Cso;
using CsoKit.Core.Native;
using CsoKit.Tests.Fixtures;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoCliContractTests
{
    [Fact]
    public void Help_ListsPublicProfileContract()
    {
        CapturedRun run = Capture(() => CsoCommandDispatcher.Run(["--help"]));

        Assert.Equal(CliExitCodes.Success, run.ExitCode);
        Assert.Contains($"--profile <{CsoCompressionProfilePolicy.SupportedNamesText}>", run.StdOut, StringComparison.Ordinal);
        Assert.Contains("[--fast]", run.StdOut, StringComparison.Ordinal);
        Assert.Contains("csokit compress <input.iso>", run.StdOut, StringComparison.Ordinal);
        Assert.Contains("Examples:", run.StdOut, StringComparison.Ordinal);
        Assert.Contains("csokit analyze game.iso --psp", run.StdOut, StringComparison.Ordinal);
        Assert.Contains("csokit repair old.zso -o fixed.cso --deep-verify", run.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("--format", run.StdOut, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidProfile_Text_ReturnsInvalidArgumentsAndStableMessage()
    {
        CapturedRun run = Capture(() => CsoCommandDispatcher.Run([
            "compress",
            "missing.iso",
            "--measure",
            "--profile",
            "bad"]));

        Assert.Equal(CliExitCodes.InvalidArguments, run.ExitCode);
        Assert.Empty(run.StdOut);
        Assert.Contains("Invalid compression profile 'bad'.", run.StdErr, StringComparison.Ordinal);
        Assert.Contains($"Supported profiles: {CsoCompressionProfilePolicy.SupportedNamesText}.", run.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidProfile_Json_ReturnsStableArgumentErrorContract()
    {
        CapturedRun run = Capture(() => CsoCommandDispatcher.Run([
            "compress",
            "missing.iso",
            "--measure",
            "--profile",
            "bad",
            "--json"]));

        Assert.Equal(CliExitCodes.InvalidArguments, run.ExitCode);
        Assert.Empty(run.StdErr);

        using JsonDocument document = JsonDocument.Parse(run.StdOut);
        JsonElement root = document.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("compress", root.GetProperty("command").GetString());
        Assert.Equal("arguments", root.GetProperty("mode").GetString());
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("InvalidArguments", root.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains("Invalid compression profile", root.GetProperty("error").GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void FastAlias_WithSmallestProfile_Text_ReturnsStableConflictMessage()
    {
        CapturedRun run = Capture(() => CsoCommandDispatcher.Run([
            "compress",
            "missing.iso",
            "--measure",
            "--profile",
            "smallest",
            "--fast"]));

        Assert.Equal(CliExitCodes.InvalidArguments, run.ExitCode);
        Assert.Empty(run.StdOut);
        Assert.Contains("--fast cannot be combined with --profile smallest.", run.StdErr, StringComparison.Ordinal);
        Assert.Contains("Use --profile fast or remove --fast.", run.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void FastAlias_WithCompatProfile_Json_ReturnsStableConflictContract()
    {
        CapturedRun run = Capture(() => CsoCommandDispatcher.Run([
            "compress",
            "missing.iso",
            "--measure",
            "--fast",
            "--profile",
            "compat",
            "--json"]));

        Assert.Equal(CliExitCodes.InvalidArguments, run.ExitCode);
        Assert.Empty(run.StdErr);

        using JsonDocument document = JsonDocument.Parse(run.StdOut);
        JsonElement root = document.RootElement;

        Assert.Equal("arguments", root.GetProperty("mode").GetString());
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("InvalidArguments", root.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(
            "--fast cannot be combined with --profile compat. Use --profile fast or remove --fast.",
            root.GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public void FastAlias_WithFastProfile_IsAcceptedByParser()
    {
        CapturedRun run = Capture(() => CsoCommandDispatcher.Run([
            "compress",
            "missing.iso",
            "--measure",
            "--profile",
            "fast",
            "--fast",
            "--json"]));

        Assert.Equal(CliExitCodes.InputNotFound, run.ExitCode);
        Assert.Empty(run.StdErr);

        using JsonDocument document = JsonDocument.Parse(run.StdOut);
        JsonElement root = document.RootElement;

        Assert.Equal("measure", root.GetProperty("mode").GetString());
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("fast", root.GetProperty("options").GetProperty("profile").GetProperty("name").GetString());
        Assert.True(root.GetProperty("options").GetProperty("profile").GetProperty("fast").GetBoolean());
        Assert.Equal(1, root.GetProperty("options").GetProperty("profile").GetProperty("level").GetInt32());
        Assert.Equal("InputNotFound", root.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public void FastAliasOnly_JsonSelectsFastProfile()
    {
        CapturedRun run = Capture(() => CsoCommandDispatcher.Run([
            "compress",
            "missing.iso",
            "--measure",
            "--fast",
            "--json"]));

        Assert.Equal(CliExitCodes.InputNotFound, run.ExitCode);

        using JsonDocument document = JsonDocument.Parse(run.StdOut);
        JsonElement profile = document.RootElement.GetProperty("options").GetProperty("profile");

        Assert.Equal("fast", profile.GetProperty("name").GetString());
        Assert.True(profile.GetProperty("fast").GetBoolean());
        Assert.Equal(1, profile.GetProperty("level").GetInt32());
    }

    [Fact]
    public void CompressOptions_JsonExposeThreadsBlockAndZopfli()
    {
        CapturedRun run = Capture(() => CsoCommandDispatcher.Run([
            "compress",
            "missing.iso",
            "--measure",
            "--threads=3",
            "--block=4K",
            "--zopfli",
            "--json"]));

        Assert.Equal(CliExitCodes.InputNotFound, run.ExitCode);
        Assert.Empty(run.StdErr);

        using JsonDocument document = JsonDocument.Parse(run.StdOut);
        JsonElement options = document.RootElement.GetProperty("options");

        Assert.Equal(4096U, options.GetProperty("blockSize").GetUInt32());
        Assert.Equal(3, options.GetProperty("threads").GetInt32());
        Assert.True(options.GetProperty("zopfli").GetBoolean());
    }

    [Theory]
    [InlineData("codecs")]
    [InlineData("native-info")]
    public void CodecCapabilityCommands_DistinguishManagedLz4FromNativeCapabilities(string command)
    {
        NativeCsoCapabilities capabilities = NativeCsoRuntime.GetCapabilities();
        CapturedRun run = Capture(() => CsoCommandDispatcher.Run([command]));

        Assert.Equal(CliExitCodes.Success, run.ExitCode);
        Assert.Contains("Managed LZ4 decode: available", run.StdOut, StringComparison.Ordinal);
        Assert.Contains($"Native LZ4 decode: {Availability(capabilities.HasLz4)}", run.StdOut, StringComparison.Ordinal);
        Assert.Contains("LZ4 encode: unavailable", run.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("  LZ4 decode: available", run.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyDeep_WithCso2Json_UsesContainerVerifier()
    {
        byte[] original = new byte[4096];

        for (int index = 0; index < original.Length; index++)
        {
            original[index] = (byte)(index % 251);
        }

        string path = CsoTestFileFactory.CreateTempCso2(original);

        try
        {
            CapturedRun run = Capture(() => CsoCommandDispatcher.Run([
                "verify",
                path,
                "--deep",
                "--sha256",
                "--json"]));

            Assert.Equal(CliExitCodes.Success, run.ExitCode);
            Assert.Empty(run.StdErr);

            using JsonDocument document = JsonDocument.Parse(run.StdOut);
            JsonElement root = document.RootElement;

            Assert.True(root.GetProperty("success").GetBoolean());
            Assert.Equal("Cso2", root.GetProperty("format").GetString());
            Assert.Equal((ulong)original.Length, root.GetProperty("deep").GetProperty("bytesReconstructed").GetUInt64());
        }
        finally
        {
            File.Delete(path);
        }
    }


    [Fact]
    public void VerifyDeep_WithRawIsoJson_UsesRawIsoContainerVerifier()
    {
        string path = ContainerFixtures.CreateMinimalIso9660();

        try
        {
            CapturedRun run = Capture(() => CsoCommandDispatcher.Run([
                "verify",
                path,
                "--deep",
                "--json"]));

            Assert.Equal(CliExitCodes.Success, run.ExitCode);
            Assert.Empty(run.StdErr);

            using JsonDocument document = JsonDocument.Parse(run.StdOut);
            JsonElement root = document.RootElement;

            Assert.True(root.GetProperty("success").GetBoolean());
            Assert.Equal("RawIso", root.GetProperty("format").GetString());
            Assert.Equal((ulong)new FileInfo(path).Length, root.GetProperty("deep").GetProperty("bytesReconstructed").GetUInt64());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("detect")]
    [InlineData("analyze")]
    [InlineData("info")]
    [InlineData("verify")]
    [InlineData("decompress")]
    [InlineData("compress")]
    [InlineData("repair")]
    public void JsonFailureCommands_UseUnifiedEnvelope(string command)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "csokit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            string missingInput = Path.Combine(tempRoot, "missing.cso");
            string missingIso = Path.Combine(tempRoot, "missing.iso");
            string output = Path.Combine(
                tempRoot,
                command == "decompress" ? "output.iso" : "output.cso");

            string[] args = command switch
            {
                "detect" => ["detect", missingInput, "--json"],
                "analyze" => ["analyze", missingIso, "--psp", "--json"],
                "info" => ["info", missingInput, "--json"],
                "verify" => ["verify", missingInput, "--json"],
                "decompress" => ["decompress", missingInput, "-o", output, "--json"],
                "compress" => ["compress", missingIso, "-o", output, "--json"],
                "repair" => ["repair", missingInput, "-o", output, "--json"],
                _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown command."),
            };

            CapturedRun run = Capture(() => CsoCommandDispatcher.Run(args));

            Assert.NotEqual(CliExitCodes.Success, run.ExitCode);
            Assert.Empty(run.StdErr);

            using JsonDocument document = JsonDocument.Parse(run.StdOut);
            JsonElement root = document.RootElement;

            Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(command, root.GetProperty("command").GetString());
            Assert.False(root.GetProperty("success").GetBoolean());
            Assert.True(root.TryGetProperty("input", out _));
            Assert.True(root.TryGetProperty("output", out _));
            Assert.True(root.TryGetProperty("format", out _));
            Assert.Equal(JsonValueKind.Array, root.GetProperty("warnings").ValueKind);
            Assert.Equal(JsonValueKind.Object, root.GetProperty("diagnostics").ValueKind);
            Assert.Equal(JsonValueKind.Object, root.GetProperty("error").ValueKind);
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("error").GetProperty("code").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("error").GetProperty("message").GetString()));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("compress", "A", "OutputFileNameTooShort", false)]
    [InlineData("compress", "ABCDEFGHIJK", "OutputFileNameTooLong", false)]
    [InlineData("decompress", "A", "OutputFileNameTooShort", false)]
    [InlineData("decompress", "ABCDEFGHIJK", "OutputFileNameTooLong", false)]
    [InlineData("repair", "A", "OutputFileNameTooShort", false)]
    [InlineData("repair", "ABCDEFGHIJK", "OutputFileNameTooLong", false)]
    [InlineData("compress", "A", "OutputFileNameTooShort", true)]
    [InlineData("compress", "ABCDEFGHIJK", "OutputFileNameTooLong", true)]
    [InlineData("decompress", "A", "OutputFileNameTooShort", true)]
    [InlineData("decompress", "ABCDEFGHIJK", "OutputFileNameTooLong", true)]
    [InlineData("repair", "A", "OutputFileNameTooShort", true)]
    [InlineData("repair", "ABCDEFGHIJK", "OutputFileNameTooLong", true)]
    public void OutputFileNameLengthFailure_ReturnsStructuredCliError(
        string command,
        string outputBaseName,
        string expectedErrorCode,
        bool json)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "csokit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            string input = Path.Combine(
                tempRoot,
                command == "compress" ? "missing.iso" : "missing.cso");
            string output = Path.Combine(
                tempRoot,
                outputBaseName + (command == "decompress" ? ".iso" : ".cso"));

            List<string> args = command switch
            {
                "compress" => ["compress", input, "-o", output],
                "decompress" => ["decompress", input, "-o", output],
                "repair" => ["repair", input, "-o", output],
                _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown command."),
            };

            if (json)
            {
                args.Add("--json");
            }

            CapturedRun run = Capture(() => CsoCommandDispatcher.Run([.. args]));

            Assert.Equal(CliExitCodes.CannotWriteOutput, run.ExitCode);

            if (json)
            {
                Assert.Empty(run.StdErr);

                using JsonDocument document = JsonDocument.Parse(run.StdOut);
                JsonElement root = document.RootElement;

                Assert.False(root.GetProperty("success").GetBoolean());
                Assert.Equal(expectedErrorCode, root.GetProperty("error").GetProperty("code").GetString());
                Assert.Equal(JsonValueKind.Null, root.GetProperty("metrics").ValueKind);
            }
            else
            {
                Assert.Contains("Status: FAILED", run.StdErr, StringComparison.Ordinal);
                Assert.Contains(expectedErrorCode, run.StdErr, StringComparison.Ordinal);
            }
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string Availability(bool available)
    {
        return available ? "available" : "unavailable";
    }

    private static CapturedRun Capture(Func<int> run)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;

        using StringWriter outWriter = new(CultureInfo.InvariantCulture);
        using StringWriter errorWriter = new(CultureInfo.InvariantCulture);

        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);

            int exitCode = run();

            return new CapturedRun(
                exitCode,
                outWriter.ToString(),
                errorWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private sealed record CapturedRun(
        int ExitCode,
        string StdOut,
        string StdErr);
}