namespace AppWatch.Core.Domain.Models;

public enum UpdateKind
{
    PortableZip,
    /// <summary>Single self-contained .exe release (e.g. optimizerDuck): copy as-is
    /// into the install directory, no extraction. The "просто exe" brief case.</summary>
    PortableExe,
    Installer,
    WingetPackage,
    /// <summary>P3: app managed by its own updater (Battle.net, Steam, Epic).
    /// AppWatch only detects and displays the version — no GitHub checks, no downloads.</summary>
    External,
}
