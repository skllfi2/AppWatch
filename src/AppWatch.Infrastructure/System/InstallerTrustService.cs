using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure.Native;

namespace AppWatch.Infrastructure.System;

/// <summary>
/// B7 — Authenticode signature verification and certificate pinning.
/// Uses high-performance native Rust zero-copy PE + WinVerifyTrust when available,
/// verifies Certificate Thumbprint and Subject pinning against supply chain attacks.
/// </summary>
public sealed class InstallerTrustService
{
    public InstallerTrustLevel Verify(string filePath, TrackedApp? app = null)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext is not (".exe" or ".msi"))
            return InstallerTrustLevel.UnsupportedFile;
        if (!File.Exists(filePath))
            return InstallerTrustLevel.UnsupportedFile;

        InstallerTrustLevel baseLevel = InstallerTrustLevel.Unsigned;

        // Try fast native path first (instant zero-copy PE inspection + WinVerifyTrust)
        if (NativeSystemBridge.IsAvailable)
        {
            if (NativeSystemBridge.TryInspectPe(filePath, out _, out var isSigned, out var isTrusted, out _))
            {
                if (isTrusted)
                    baseLevel = InstallerTrustLevel.TrustedSignature;
                else if (!isSigned)
                    baseLevel = InstallerTrustLevel.Unsigned;
                else
                    baseLevel = InstallerTrustLevel.InvalidSignature;
            }
            else
            {
                baseLevel = VerifyFallback(filePath);
            }
        }
        else
        {
            baseLevel = VerifyFallback(filePath);
        }

        // If file is signed and trusted, check Certificate Pinning rules
        if (baseLevel == InstallerTrustLevel.TrustedSignature)
        {
            var cert = ExtractCertificate(filePath);
            if (cert is not null)
            {
                var thumbprint = cert.GetCertHashString();
                var subject = cert.Subject;

                if (app is not null)
                {
                    // If thumbprint was pinned, it must match exactly
                    if (!string.IsNullOrWhiteSpace(app.PinnedCertificateThumbprint) &&
                        !string.Equals(app.PinnedCertificateThumbprint, thumbprint, StringComparison.OrdinalIgnoreCase))
                    {
                        return InstallerTrustLevel.CertificateMismatch;
                    }

                    // If subject was pinned, it must match
                    if (!string.IsNullOrWhiteSpace(app.PinnedCertificateSubject) &&
                        !string.Equals(app.PinnedCertificateSubject, subject, StringComparison.OrdinalIgnoreCase))
                    {
                        return InstallerTrustLevel.CertificateMismatch;
                    }

                    // Auto-pin certificate on first successful trusted verification if empty
                    if (string.IsNullOrWhiteSpace(app.PinnedCertificateThumbprint))
                    {
                        app.PinnedCertificateThumbprint = thumbprint;
                        app.PinnedCertificateSubject = subject;
                    }
                }
            }
        }

        return baseLevel;
    }

    public static X509Certificate2? ExtractCertificate(string filePath)
    {
        try
        {
#pragma warning disable SYSLIB0057
            return new X509Certificate2(X509Certificate.CreateFromSignedFile(filePath));
#pragma warning restore SYSLIB0057
        }
        catch
        {
            return null;
        }
    }

    private static InstallerTrustLevel VerifyFallback(string filePath)
    {
        try
        {
            var escaped = filePath.Replace("'", "''");
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments =
                    "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " +
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
                _ => InstallerTrustLevel.InvalidSignature,
            };
        }
        catch
        {
            return InstallerTrustLevel.InvalidSignature;
        }
    }
}
