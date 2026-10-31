namespace AppWatch.Core.Domain;

/// <summary>
/// Maps raw engine/winget failure details to a loc KEY with a human explanation
/// (what actually happened + what to do). Null when nothing known matches.
/// The UI appends the hint after the raw message: "Сбой установки с кодом
/// выхода: 8006 — установщик прерван…".
/// </summary>
public static class UpdateErrorHints
{
    public static string? HintKey(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail)) return null;

        if (detail.Contains("80070005", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("800702e4", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("access is denied", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("elevation required", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("requires elevation", StringComparison.OrdinalIgnoreCase))
            return "HintElevationRequired";

        if (detail.Contains("80070020", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("sharing violation", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("used by another process", StringComparison.OrdinalIgnoreCase))
            return "HintFileLocked";

        if (detail.Contains("80070070", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("disk full", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("there is not enough space on the disk", StringComparison.OrdinalIgnoreCase))
            return "HintDiskFull";

        if (detail.Contains("8006") || detail.Contains("Сбой установки") || detail.Contains("installer failed"))
            return "HintWingetInstallFailed";

        if (detail.Contains("80072ee2", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("80072efd", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("80072f78", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("80090308", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("InternetOpenUrl", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("timed out", StringComparison.OrdinalIgnoreCase))
            return "HintNetworkTimeout";

        if (detail.Contains("не применяется") || detail.Contains("not applicable"))
            return "HintWingetNotApplicable";

        if (detail.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            return "HintRateLimited";

        if (detail.Contains("не удалось остановить процесс", StringComparison.OrdinalIgnoreCase))
            return "HintKillFailed";

        if (detail.Contains("already in progress"))
            return "HintAlreadyUpdating";

        if (detail.Contains("health check failed", StringComparison.OrdinalIgnoreCase))
            return "HintHealthCheck";

        if (detail.Contains("SHA-256 mismatch", StringComparison.OrdinalIgnoreCase))
            return "HintDigestMismatch";

        return null;
    }

    /// <summary>Raw detail + hint key resolved by the caller, or the raw detail alone.</summary>
    public static string WithHint(string? detail, Func<string, string> loc)
    {
        if (string.IsNullOrWhiteSpace(detail)) return detail ?? "";
        return HintKey(detail) is { } key ? $"{detail}\n{loc(key)}" : detail;
    }
}
