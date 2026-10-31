using System.Text.RegularExpressions;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Domain;

/// <summary>
/// Parses SHA-256 checksums from companion assets (checksums.txt, SHA256SUMS, .sha256)
/// or release notes markdown descriptions.
/// </summary>
public static class ChecksumParser
{
    private static readonly Regex Sha256Regex = new(@"\b[a-fA-F0-9]{64}\b", RegexOptions.Compiled);

    /// <summary>
    /// Parses a checksum file content (GNU sha256sum, BSD style, colon-separated, or single bare hash)
    /// and extracts mapping from fileName -> sha256.
    /// </summary>
    public static Dictionary<string, string> ParseChecksumFile(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(content)) return result;

        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        // If entire content is just a single 64-char hash (e.g. from app.zip.sha256)
        if (lines.Length == 1)
        {
            var singleTrim = lines[0].Trim();
            if (Sha256Regex.IsMatch(singleTrim) && singleTrim.Length == 64)
            {
                result["*"] = singleTrim.ToLowerInvariant();
                return result;
            }
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length < 64 || line.StartsWith('#')) continue;

            // 1. BSD style: SHA256 (filename) = hash
            if (line.StartsWith("SHA256", StringComparison.OrdinalIgnoreCase))
            {
                var eqIdx = line.IndexOf('=');
                if (eqIdx > 0)
                {
                    var left = line[..eqIdx].Trim();
                    var hashPart = line[(eqIdx + 1)..].Trim();
                    var matchHash = Sha256Regex.Match(hashPart);
                    if (matchHash.Success)
                    {
                        var openParen = left.IndexOf('(');
                        var closeParen = left.LastIndexOf(')');
                        if (openParen >= 0 && closeParen > openParen)
                        {
                            var fn = Path.GetFileName(left.Substring(openParen + 1, closeParen - openParen - 1).Trim());
                            result[fn] = matchHash.Value.ToLowerInvariant();
                            continue;
                        }
                    }
                }
            }

            // 2. Standard GNU: <hash> [* ]<filename>
            var match = Sha256Regex.Match(line);
            if (match.Success)
            {
                var hash = match.Value.ToLowerInvariant();
                // Check if hash is at start of line
                if (match.Index == 0 || string.IsNullOrWhiteSpace(line[..match.Index]))
                {
                    var rest = line[(match.Index + match.Length)..].TrimStart();
                    if (rest.StartsWith('*')) rest = rest[1..];
                    rest = rest.Trim();
                    var fn = Path.GetFileName(rest.Trim('"').Trim('`'));
                    if (!string.IsNullOrWhiteSpace(fn))
                    {
                        result[fn] = hash;
                        continue;
                    }
                }

                // 3. Reverse: <filename>[: ] <hash>
                if (match.Index + match.Length == line.Length || string.IsNullOrWhiteSpace(line[(match.Index + match.Length)..]))
                {
                    var left = line[..match.Index].Trim().TrimEnd(':').Trim();
                    var fn = Path.GetFileName(left.Trim('"').Trim('`'));
                    if (!string.IsNullOrWhiteSpace(fn))
                    {
                        result[fn] = hash;
                        continue;
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Searches the release body for a SHA-256 hash associated with the target asset name.
    /// Supports markdown tables, lists, and direct pairings.
    /// </summary>
    public static string? FindChecksumInReleaseBody(string? body, string assetName)
    {
        if (string.IsNullOrWhiteSpace(body) || string.IsNullOrWhiteSpace(assetName))
            return null;

        var cleanTarget = Path.GetFileName(assetName).Trim();
        var lines = body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Contains(cleanTarget, StringComparison.OrdinalIgnoreCase))
            {
                var match = Sha256Regex.Match(line);
                if (match.Success)
                    return match.Value.ToLowerInvariant();
            }
        }

        return null;
    }

    /// <summary>
    /// Identifies companion checksum assets from release assets.
    /// E.g. app.zip.sha256, checksums.txt, SHA256SUMS, etc.
    /// </summary>
    public static bool IsChecksumAsset(string assetName)
    {
        var fn = Path.GetFileName(assetName).ToLowerInvariant();
        return fn.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase) ||
               fn.EndsWith(".sha256sum", StringComparison.OrdinalIgnoreCase) ||
               fn.EndsWith(".sha256.txt", StringComparison.OrdinalIgnoreCase) ||
               fn == "checksums.txt" ||
               fn == "sha256sums" ||
               fn == "sha256sums.txt" ||
               fn == "shasums256.txt" ||
               fn == "hashes.txt";
    }

    /// <summary>
    /// Finds the best companion checksum asset for targetAssetName among all release assets.
    /// Prioritizes specific ({targetAssetName}.sha256) over collective (checksums.txt, SHA256SUMS).
    /// </summary>
    public static ReleaseAsset? FindCompanionChecksumAsset(
        IReadOnlyList<ReleaseAsset> assets, string targetAssetName)
    {
        if (assets.Count == 0 || string.IsNullOrWhiteSpace(targetAssetName))
            return null;

        var cleanTarget = Path.GetFileName(targetAssetName);

        // 1. Direct companion: {asset}.sha256, {asset}.sha256sum, {asset}.sha256.txt
        var specific = assets.FirstOrDefault(a =>
            a.Name.Equals($"{cleanTarget}.sha256", StringComparison.OrdinalIgnoreCase) ||
            a.Name.Equals($"{cleanTarget}.sha256sum", StringComparison.OrdinalIgnoreCase) ||
            a.Name.Equals($"{cleanTarget}.sha256.txt", StringComparison.OrdinalIgnoreCase));
        if (specific is not null) return specific;

        // 2. Collective checksum files: checksums.txt, SHA256SUMS, sha256sums.txt, SHASUMS256.txt
        var collectiveNames = new[] { "sha256sums", "sha256sums.txt", "checksums.txt", "shasums256.txt", "hashes.txt" };
        foreach (var col in collectiveNames)
        {
            var found = assets.FirstOrDefault(a =>
                Path.GetFileName(a.Name).Equals(col, StringComparison.OrdinalIgnoreCase));
            if (found is not null) return found;
        }

        // 3. Any asset ending with .sha256 / .sha256sum
        var anyChecksum = assets.FirstOrDefault(a => IsChecksumAsset(a.Name));
        return anyChecksum;
    }
}
