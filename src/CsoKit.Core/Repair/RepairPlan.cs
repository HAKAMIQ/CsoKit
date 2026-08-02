using CsoKit.Core.Formats.DiscImage;

namespace CsoKit.Core.Repair;

public sealed record RepairPlan(
    DetectedDiscFormat InputFormat,
    RepairMode Mode,
    bool WritesTempIso,
    string? FallbackReason = null);
