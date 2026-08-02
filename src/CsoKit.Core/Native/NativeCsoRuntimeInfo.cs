namespace CsoKit.Core.Native;

public sealed record NativeCsoRuntimeInfo(
    bool IsAvailable,
    string BackendName,
    string? NativeVersion,
    string? FailureReason);
