namespace AppWatch.Core.Domain.Models;

public enum UpdateKind
{
    PortableZip,
    /// <summary>Single self-contained .exe release (e.g. optimizerDuck): copy as-is
    /// into the install directory, no extraction. The "просто exe" brief case.</summary>
    PortableExe,
    Installer,
    WingetPackage,
}
