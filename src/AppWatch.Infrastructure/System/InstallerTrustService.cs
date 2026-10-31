using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AppWatch.Core.Domain;

namespace AppWatch.Infrastructure.System;

/// <summary>
/// B7 — Authenticode signature verification.
/// Implemented via `Get-AuthenticodeSignature` (PowerShell) which drives the same
/// WinVerifyTrust machinery and handles both embedded and catalog signatures.
/// The direct WinVerifyTrust P/Invoke returns TRUST_E_SYSTEM_ERROR (0x800B0003)
/// for every file on this machine — including catalog-signed system binaries and
/// embedded-signed dotnet.exe — while Get-AuthenticodeSignature verifies the same
/// files correctly from the same session (probed 2026-10-14; PS Status codes:
/// 0 = Valid, 2 = NotSigned). Spawn cost ~1s, paid once per installer run — fine.
/// </summary>
public sealed class InstallerTrustService
{
    public InstallerTrustLevel Verify(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext is not (".exe" or ".msi"))
            return InstallerTrustLevel.UnsupportedFile;
        if (!File.Exists(filePath))
            return InstallerTrustLevel.UnsupportedFile;

        try
        {
            // -LiteralPath + doubled single-quotes: no injection through the file path
            var escaped = filePath.Replace("'", "''");
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments =
                    "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " +
                    // NB: [int] cast must wrap the WHOLE member access — `$sig.Status` —
                    // `[int](expr).Status` parses as ([int](expr)).Status and yields null.
                    $"\"$sig = Get-AuthenticodeSignature -LiteralPath '{escaped}'; [int]$sig.Status\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
                return InstallerTrustLevel.InvalidSignature;

            var stderrTask = Task.Run(() => process.StandardError.ReadToEnd());
            var output = process.StandardOutput.ReadToEnd();
            try { stderrTask.Wait(3000); } catch { }
            if (!process.WaitForExit(20_000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return InstallerTrustLevel.InvalidSignature;
            }

            var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var code = lines.LastOrDefault() ?? "";
            return code switch
            {
                "0" => InstallerTrustLevel.TrustedSignature,   // Valid
                "2" => InstallerTrustLevel.Unsigned,           // NotSigned
                _ => InstallerTrustLevel.InvalidSignature,      // UnknownError, HashMismatch, NotTrusted…
            };
        }
        catch
        {
            // verification machinery broken — never pretend the file is fine
            return InstallerTrustLevel.InvalidSignature;
        }
    }
}
