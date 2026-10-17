using System.Globalization;
using System.IO;

namespace AppWatch.Core.Domain;

/// <summary>
/// P5: Pure preflight checks before update application.
/// Disk-space guard: fail fast if insufficient space on target volume.
/// Cancel-cleanliness: not modeled here (engine concern), but the rule is:
/// on cancellation, no partial files remain in the install directory.
/// </summary>
public static class Preflight
{
    /// <summary>
    /// Checks if the volume containing <paramref name="targetPath"/> has at least
    /// <paramref name="requiredBytes"/> free. Returns (ok, reason).
    /// Pure: no side effects, injectable DriveInfo for testing.
    /// </summary>
    public static (bool Ok, string? Reason) CheckDiskSpace(string targetPath, long requiredBytes)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            return (false, "Cannot determine volume root for path");
        if (requiredBytes < 0) requiredBytes = 0;

        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(targetPath));
            if (string.IsNullOrEmpty(root))
                return (false, "Cannot determine volume root for path");

            var drive = new DriveInfo(root);
            if (!drive.IsReady)
                return (false, $"Volume {root} not ready");

            var free = drive.AvailableFreeSpace;
            if (free < requiredBytes)
                return (false, $"Insufficient disk space: {free.ToString(CultureInfo.InvariantCulture)} bytes free, {requiredBytes.ToString(CultureInfo.InvariantCulture)} required on {root}");

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Disk space check failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Overload for unit testing with injected drive info.
    /// </summary>
    public static (bool Ok, string? Reason) CheckDiskSpace(long freeBytes, long requiredBytes)
    {
        if (requiredBytes < 0) requiredBytes = 0;
        if (freeBytes < requiredBytes)
            return (false, $"Insufficient disk space: {freeBytes.ToString(CultureInfo.InvariantCulture)} bytes free, {requiredBytes.ToString(CultureInfo.InvariantCulture)} required");
        return (true, null);
    }
}