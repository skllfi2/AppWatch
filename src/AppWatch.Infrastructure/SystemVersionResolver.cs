using AppWatch.Core.Domain.Models;

namespace AppWatch.Infrastructure;

public sealed record InstalledAppRecord(
    string DisplayName,
    string? DisplayVersion,
    string? InstallLocation,
    string? DisplayIcon,
    string? UninstallString,
    string RegistryKeyName);

/// <summary>
/// Resolves the version and location of an application installed on this system by its display name.
/// Chain: Add/Remove Programs registry DisplayVersion → file version of the executable
/// pointed to by InstallLocation. Used for winget packages whose installed version
/// winget cannot determine ("Unknown"), and for auto-detecting installer-based applications.
/// Thread-safe cached implementation: builds an in-memory index in a single pass
/// across registry roots instead of repeated linear scans per query.
/// </summary>
public sealed class SystemVersionResolver
{
    private readonly object _lock = new();
    private List<InstalledAppRecord>? _records;
    private Dictionary<string, InstalledAppRecord>? _byName;
    private DateTimeOffset _lastScanUtc = DateTimeOffset.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public void InvalidateCache()
    {
        lock (_lock)
        {
            _records = null;
            _byName = null;
        }
    }

    /// <summary>
    /// Returns all registered applications discovered in Windows registry uninstall keys.
    /// </summary>
    public Task PrewarmCacheAsync()
    {
        return Task.Run(() => EnsureIndex());
    }

    public IReadOnlyList<InstalledAppRecord> GetAllInstalledApps()
    {
        var (_, records) = EnsureIndex();
        return records;
    }

    public string? ResolveInstalledVersion(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return null;

        var (byName, records) = EnsureIndex();
        var key = displayName.Trim();
        if (byName.TryGetValue(key, out var entry))
        {
            if (entry.DisplayVersion is not null) return entry.DisplayVersion;
            if (entry.InstallLocation is not null)
            {
                var exeVersion = TryGetExecutableVersion(entry.InstallLocation);
                if (exeVersion is not null) return exeVersion;
            }
        }

        // Fallback: search records by partial or fuzzy match
        var fuzzy = FindBestMatch(records, key);
        if (fuzzy is not null)
        {
            if (fuzzy.DisplayVersion is not null) return fuzzy.DisplayVersion;
            if (fuzzy.InstallLocation is not null)
            {
                var exeVersion = TryGetExecutableVersion(fuzzy.InstallLocation);
                if (exeVersion is not null) return exeVersion;
            }
        }

        return null;
    }

    public string? ResolveInstallLocation(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return null;

        var (byName, records) = EnsureIndex();
        var key = displayName.Trim();
        if (byName.TryGetValue(key, out var entry) && !string.IsNullOrWhiteSpace(entry.InstallLocation))
            return entry.InstallLocation;

        var fuzzy = FindBestMatch(records, key);
        if (fuzzy is not null && !string.IsNullOrWhiteSpace(fuzzy.InstallLocation))
            return fuzzy.InstallLocation;

        return null;
    }

    public InstalledAppRecord? FindMatchingInstalledApp(TrackedApp app)
    {
        var (byName, records) = EnsureIndex();

        // 1. RegistryKeyName exact match
        if (!string.IsNullOrWhiteSpace(app.RegistryKeyName))
        {
            if (byName.TryGetValue(app.RegistryKeyName.Trim(), out var byKey))
                return byKey;
            var matchByKey = records.FirstOrDefault(r =>
                r.RegistryKeyName.Equals(app.RegistryKeyName, StringComparison.OrdinalIgnoreCase) ||
                r.DisplayName.Equals(app.RegistryKeyName, StringComparison.OrdinalIgnoreCase));
            if (matchByKey is not null) return matchByKey;
        }

        // 2. Exact match on app.Name
        if (!string.IsNullOrWhiteSpace(app.Name) && byName.TryGetValue(app.Name.Trim(), out var byExactName))
            return byExactName;

        // 3. Exact match on app.RepoName
        if (!string.IsNullOrWhiteSpace(app.RepoName) && byName.TryGetValue(app.RepoName.Trim(), out var byExactRepo))
            return byExactRepo;

        // 4. Fuzzy match by Name or RepoName
        var match = FindBestMatch(records, app.Name);
        if (match is not null) return match;

        if (!string.IsNullOrWhiteSpace(app.RepoName))
        {
            match = FindBestMatch(records, app.RepoName);
            if (match is not null) return match;
        }

        return null;
    }

    public bool AutoDetectInstalledApp(TrackedApp app)
    {
        var rec = FindMatchingInstalledApp(app);
        if (rec is not null)
        {
            var detectedAny = false;

            if (string.IsNullOrWhiteSpace(app.InstallDirectory) && !string.IsNullOrWhiteSpace(rec.InstallLocation) && Directory.Exists(rec.InstallLocation))
            {
                app.InstallDirectory = rec.InstallLocation;
                detectedAny = true;
            }

            if (string.IsNullOrWhiteSpace(app.ExecutableName) && !string.IsNullOrWhiteSpace(app.InstallDirectory))
            {
                var exe = FindPrimaryExecutable(app.InstallDirectory, app.RepoName ?? app.Name);
                if (exe is not null)
                {
                    app.ExecutableName = exe;
                    detectedAny = true;
                }
                else if (!string.IsNullOrWhiteSpace(rec.DisplayIcon))
                {
                    var cleanIcon = rec.DisplayIcon.Split(',')[0].Trim().Trim('"');
                    if (File.Exists(cleanIcon))
                    {
                        app.ExecutableName = Path.GetFileName(cleanIcon);
                        detectedAny = true;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(rec.DisplayVersion))
            {
                if (string.IsNullOrWhiteSpace(app.CurrentVersion) || app.CurrentVersion == "not installed" || app.CurrentVersion == "?")
                {
                    app.CurrentVersion = rec.DisplayVersion;
                    detectedAny = true;
                }
            }
            else if (!string.IsNullOrWhiteSpace(app.InstallDirectory))
            {
                var exeVer = TryGetExecutableVersion(app.InstallDirectory);
                if (!string.IsNullOrWhiteSpace(exeVer))
                {
                    app.CurrentVersion = exeVer;
                    detectedAny = true;
                }
            }

            return detectedAny || !string.IsNullOrWhiteSpace(app.InstallDirectory);
        }

        // Fallback: check standard program directories (Program Files, LocalAppData\Programs)
        foreach (var dir in GetStandardInstallDirectories(app.Name, app.RepoName))
        {
            if (Directory.Exists(dir))
            {
                if (string.IsNullOrWhiteSpace(app.InstallDirectory))
                    app.InstallDirectory = dir;

                if (string.IsNullOrWhiteSpace(app.ExecutableName))
                {
                    var exe = FindPrimaryExecutable(dir, app.RepoName ?? app.Name);
                    if (exe is not null)
                        app.ExecutableName = exe;
                }

                if (string.IsNullOrWhiteSpace(app.CurrentVersion) || app.CurrentVersion == "not installed" || app.CurrentVersion == "?")
                {
                    var ver = TryGetExecutableVersion(dir);
                    if (ver is not null)
                        app.CurrentVersion = ver;
                }

                return true;
            }
        }

        return false;
    }

    public static string? FindPrimaryExecutable(string installLocation, string? preferredName = null)
    {
        if (string.IsNullOrWhiteSpace(installLocation) || !Directory.Exists(installLocation))
            return null;

        try
        {
            var exes = Directory.EnumerateFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly).ToList();

            // If none found in top directory, check immediate common subfolders (bin, App, cmd)
            if (exes.Count == 0)
            {
                foreach (var sub in new[] { "bin", "app", "cmd" })
                {
                    var subDir = Path.Combine(installLocation, sub);
                    if (Directory.Exists(subDir))
                    {
                        exes.AddRange(Directory.EnumerateFiles(subDir, "*.exe", SearchOption.TopDirectoryOnly));
                    }
                }
            }

            if (exes.Count == 0) return null;

            // Exclude helper/uninstaller/updater executables
            var candidates = exes.Where(exe =>
            {
                var fn = Path.GetFileNameWithoutExtension(exe);
                return !fn.Contains("unins", StringComparison.OrdinalIgnoreCase)
                    && !fn.Contains("uninstall", StringComparison.OrdinalIgnoreCase)
                    && !fn.Contains("helper", StringComparison.OrdinalIgnoreCase)
                    && !fn.Contains("crash", StringComparison.OrdinalIgnoreCase)
                    && !fn.Contains("elevate", StringComparison.OrdinalIgnoreCase)
                    && !fn.Contains("update", StringComparison.OrdinalIgnoreCase)
                    && !fn.Contains("setup", StringComparison.OrdinalIgnoreCase);
            }).ToList();

            if (candidates.Count == 0)
                candidates = exes;

            if (!string.IsNullOrWhiteSpace(preferredName))
            {
                var cleanPref = Path.GetFileNameWithoutExtension(preferredName);
                var exact = candidates.FirstOrDefault(e =>
                    Path.GetFileNameWithoutExtension(e).Equals(cleanPref, StringComparison.OrdinalIgnoreCase));
                if (exact is not null) return Path.GetFileName(exact);

                var partial = candidates.FirstOrDefault(e =>
                    Path.GetFileNameWithoutExtension(e).Contains(cleanPref, StringComparison.OrdinalIgnoreCase) ||
                    cleanPref.Contains(Path.GetFileNameWithoutExtension(e), StringComparison.OrdinalIgnoreCase));
                if (partial is not null) return Path.GetFileName(partial);
            }

            // Fallback: pick the largest executable file
            var largest = candidates.OrderByDescending(f =>
            {
                try { return new FileInfo(f).Length; } catch { return 0L; }
            }).FirstOrDefault();

            return largest is not null ? Path.GetFileName(largest) : null;
        }
        catch
        {
            return null;
        }
    }

    public static string? TryGetExecutableVersion(string installLocation)
    {
        try
        {
            if (!Directory.Exists(installLocation)) return null;
            var exes = Directory.EnumerateFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly).ToList();
            if (exes.Count == 0)
            {
                foreach (var sub in new[] { "bin", "app", "cmd" })
                {
                    var subDir = Path.Combine(installLocation, sub);
                    if (Directory.Exists(subDir))
                        exes.AddRange(Directory.EnumerateFiles(subDir, "*.exe", SearchOption.TopDirectoryOnly));
                }
            }
            foreach (var exe in exes)
            {
                var name = Path.GetFileNameWithoutExtension(exe);
                if (name.Contains("unins", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("uninstall", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("helper", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("update", StringComparison.OrdinalIgnoreCase))
                    continue;
                var vi = global::System.Diagnostics.FileVersionInfo.GetVersionInfo(exe);
                var product = vi.ProductVersion?.Trim() ?? vi.FileVersion?.Trim();
                if (!string.IsNullOrWhiteSpace(product)) return product;
            }
        }
        catch
        {
            // access denied / missing files — treat as unresolvable
        }
        return null;
    }

    private (Dictionary<string, InstalledAppRecord> ByName, List<InstalledAppRecord> Records) EnsureIndex()
    {
        lock (_lock)
        {
            if (_byName is not null && _records is not null && (DateTimeOffset.UtcNow - _lastScanUtc) < CacheTtl)
                return (_byName, _records);

            var map = new Dictionary<string, InstalledAppRecord>(StringComparer.OrdinalIgnoreCase);
            var records = new List<InstalledAppRecord>();

            foreach (var root in Roots())
            {
                using var rootKey = root;
                if (rootKey is null) continue;

                foreach (var subKeyName in rootKey.GetSubKeyNames())
                {
                    try
                    {
                        using var key = rootKey.OpenSubKey(subKeyName);
                        if (key is null) continue;

                        if (key.GetValue("DisplayName") is not string name || string.IsNullOrWhiteSpace(name))
                            continue;

                        var trimmedName = name.Trim();
                        string? regVersion = key.GetValue("DisplayVersion") is string v && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
                        string? installLoc = key.GetValue("InstallLocation") is string loc && !string.IsNullOrWhiteSpace(loc) ? loc.Trim() : null;
                        string? displayIcon = key.GetValue("DisplayIcon") is string icon && !string.IsNullOrWhiteSpace(icon) ? icon.Trim() : null;
                        string? uninstallStr = key.GetValue("UninstallString") is string uninst && !string.IsNullOrWhiteSpace(uninst) ? uninst.Trim() : null;

                        // Fallback 1: if InstallLocation is missing/invalid, try DisplayIcon
                        if (string.IsNullOrWhiteSpace(installLoc) || !Directory.Exists(installLoc))
                        {
                            if (!string.IsNullOrWhiteSpace(displayIcon))
                            {
                                var cleanIcon = displayIcon.Split(',')[0].Trim().Trim('"');
                                if (File.Exists(cleanIcon))
                                {
                                    var iconDir = Path.GetDirectoryName(cleanIcon);
                                    if (!string.IsNullOrWhiteSpace(iconDir) && Directory.Exists(iconDir))
                                        installLoc = iconDir;
                                }
                            }
                        }

                        // Fallback 2: try UninstallString
                        if (string.IsNullOrWhiteSpace(installLoc) || !Directory.Exists(installLoc))
                        {
                            if (!string.IsNullOrWhiteSpace(uninstallStr))
                            {
                                var cleanUninst = uninstallStr.Split(" /")[0].Split(" -")[0].Trim().Trim('"');
                                if (File.Exists(cleanUninst))
                                {
                                    var uninstDir = Path.GetDirectoryName(cleanUninst);
                                    if (!string.IsNullOrWhiteSpace(uninstDir) && Directory.Exists(uninstDir))
                                        installLoc = uninstDir;
                                }
                            }
                        }

                        var record = new InstalledAppRecord(trimmedName, regVersion, installLoc, displayIcon, uninstallStr, subKeyName);
                        records.Add(record);

                        if (!map.ContainsKey(trimmedName))
                            map[trimmedName] = record;
                        if (!map.ContainsKey(subKeyName))
                            map[subKeyName] = record;
                    }
                    catch
                    {
                        // Ignore individual unreadable keys
                    }
                }
            }

            _records = records;
            _byName = map;
            _lastScanUtc = DateTimeOffset.UtcNow;
            return (_byName, _records);
        }
    }

    private static InstalledAppRecord? FindBestMatch(IReadOnlyList<InstalledAppRecord> records, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        var q = query.Trim();

        // Exact match
        var exact = records.FirstOrDefault(r => r.DisplayName.Equals(q, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        // Starts with (e.g. "Git" -> "Git version 2.45.1")
        var starts = records.FirstOrDefault(r => r.DisplayName.StartsWith(q, StringComparison.OrdinalIgnoreCase));
        if (starts is not null) return starts;

        // If query is at least 3 chars, check contains
        if (q.Length >= 3)
        {
            var contains = records.FirstOrDefault(r =>
                r.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (q.Length >= 4 && q.Contains(r.DisplayName, StringComparison.OrdinalIgnoreCase)));
            if (contains is not null) return contains;
        }

        return null;
    }

    private static IEnumerable<string> GetStandardInstallDirectories(string appName, string? repoName)
    {
        var names = new List<string>();
        if (!string.IsNullOrWhiteSpace(appName)) names.Add(appName);
        if (!string.IsNullOrWhiteSpace(repoName) && !repoName.Equals(appName, StringComparison.OrdinalIgnoreCase))
            names.Add(repoName);

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var localPrograms = Path.Combine(localAppData, "Programs");

        foreach (var name in names)
        {
            if (!string.IsNullOrWhiteSpace(programFiles))
                yield return Path.Combine(programFiles, name);
            if (!string.IsNullOrWhiteSpace(programFilesX86))
                yield return Path.Combine(programFilesX86, name);
            if (!string.IsNullOrWhiteSpace(localPrograms))
                yield return Path.Combine(localPrograms, name);
        }
    }

    private static IEnumerable<Microsoft.Win32.RegistryKey?> Roots()
    {
        yield return Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
        yield return Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
        yield return Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
    }
}
