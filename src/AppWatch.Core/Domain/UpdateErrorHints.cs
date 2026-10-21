namespace AppWatch.Core.Domain;

/// <summary>
/// Maps raw engine/winget failure details to a loc KEY with a human explanation
/// (what actually happened + what to do). Null when nothing known matches.
/// The UI appends the hint after the raw message: "Сбой установки с кодентом
/// выхода: 8006 — установщик прерван…".
/// </summary>
public static class UpdateErrorHints
{
    public static string? HintKey(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail)) return null;

        if (detail.Contains("8006") || detail.Contains("Сбой установки") || detail.Contains("installer failed"))
            return "HintWingetInstallFailed";

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
