namespace AppWatch.Core.Domain.Models;

public sealed record UpdateHistoryEntry(
    string AppName,
    string FromVersion,
    string ToVersion,
    DateTimeOffset AtUtc,
    bool Success,
    string? Detail);
