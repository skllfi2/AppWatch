namespace AppWatch.Core.Domain;

/// <summary>Result of verifying a downloaded installer's Authenticode signature.</summary>
public enum InstallerTrustLevel
{
    /// <summary>Signed and the certificate chain validates (WinVerifyTrust S_OK).</summary>
    TrustedSignature,
    /// <summary>File carries no signature at all (TRUST_E_NOSIGNATURE).</summary>
    Unsigned,
    /// <summary>Signed but the chain/signature is broken, revoked or untrusted.</summary>
    InvalidSignature,
    /// <summary>Not an .exe/.msi — nothing to verify (treated as unsigned for gating).</summary>
    UnsupportedFile,
}

/// <summary>
/// B7 — pure gating policy for launching downloaded installers:
/// a broken signature never runs; an unsigned one runs only with explicit
/// per-app consent (AllowUnsignedInstaller — most small GitHub tools ship unsigned).
/// </summary>
public static class InstallerTrustPolicy
{
    public static bool CanRun(InstallerTrustLevel level, bool allowUnsigned) => level switch
    {
        InstallerTrustLevel.TrustedSignature => true,
        InstallerTrustLevel.Unsigned or InstallerTrustLevel.UnsupportedFile => allowUnsigned,
        _ => false,
    };

    /// <summary>Human-readable reason for refusing to run the installer.</summary>
    public static string RefusalReason(InstallerTrustLevel level) => level switch
    {
        InstallerTrustLevel.InvalidSignature => "installer signature is invalid or untrusted",
        InstallerTrustLevel.Unsigned => "installer is not signed (enable unsigned installers in app settings)",
        InstallerTrustLevel.UnsupportedFile => "unsupported installer type",
        _ => "unknown trust level",
    };
}