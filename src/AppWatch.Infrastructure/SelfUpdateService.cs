using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain;
using AppWatch.Infrastructure.System;
using Serilog;

namespace AppWatch.Infrastructure;

public sealed class SelfUpdateService
{
    // change when the watcher's own repository exists
    private const string RepoOwner = "skllfi2";
    private const string RepoName = "AppWatch";

    private readonly IGitHubClient _github;
    private readonly FileStore _files;

    public SelfUpdateService(IGitHubClient github, FileStore files)
    {
        _github = github;
        _files = files;
    }

    public sealed record PreparedUpdate(string TempRoot, string ExtractRoot, string LaunchExe, string Version);

    public async Task<PreparedUpdate?> PrepareAsync(
        string currentVersion, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var releases = await _github.GetReleasesAsync(RepoOwner, RepoName, cancellationToken);
            var latest = releases.FirstOrDefault(r => !r.IsPreRelease);
            if (latest is null || !VersionParser.IsNewer(currentVersion, latest.TagName))
                return null;

            var asset = latest.Assets.FirstOrDefault(
                a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            if (asset is null)
            {
                Log.Warning("self-update: no zip asset in release {Tag}", latest.TagName);
                return null;
            }

            var tempRoot = Path.Combine(Path.GetTempPath(), $"appwatch_update_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);

            var zipPath = Path.Combine(tempRoot, asset.Name);
            await _github.DownloadFileAsync(asset.DownloadUrl, zipPath, progress, cancellationToken);

            // verify integrity before anything is applied
            if (asset.DigestSha256 is { } digest)
                await _files.VerifyDigestAsync(zipPath, digest);
            else
                Log.Warning("self-update: no digest for {Asset}; skipping verification", asset.Name);

            var extractRoot = Path.Combine(tempRoot, "payload");
            Directory.CreateDirectory(extractRoot);
            ZipFile.ExtractToDirectory(zipPath, extractRoot);

            var payloadRoot = ResolvePayloadRoot(extractRoot);
            var launchExe = Path.Combine(payloadRoot, "AppWatch.exe");
            if (!File.Exists(launchExe))
            {
                Log.Error("self-update: AppWatch.exe not found in {Path}", payloadRoot);
                return null;
            }

            Log.Information("self-update prepared: {Version} at {Path}", latest.TagName, payloadRoot);
            return new PreparedUpdate(tempRoot, payloadRoot, launchExe, latest.TagName);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "self-update preparation failed");
            return null;
        }
    }

    public async Task<bool> ApplyAsync(PreparedUpdate prepared, CancellationToken cancellationToken = default)
    {
        try
        {
            // Launch the detached PowerShell script that will wait for this process to exit
            // and then replace files and restart the app
            Launch(prepared);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "self-update apply failed");
            return false;
        }
    }

    public bool Launch(PreparedUpdate prepared)
    {
        try
        {
            var installRoot = AppContext.BaseDirectory;
            var currentExe = Environment.ProcessPath ?? "";
            var currentPid = Environment.ProcessId;

            var scriptDir = Path.Combine(Path.GetTempPath(), "appwatch_updates");
            Directory.CreateDirectory(scriptDir);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var scriptPath = Path.Combine(scriptDir, $"apply_update_{timestamp}.ps1");
            var logPath = Path.Combine(scriptDir, $"apply_update_{timestamp}.log");

            var script = BuildPowerShellScript(
                currentPid, prepared.ExtractRoot, installRoot, currentExe, prepared.TempRoot, logPath);
            File.WriteAllText(scriptPath, script, Encoding.UTF8);

            Log.Information("self-update: launching script {Script} (pid {Pid})", scriptPath, currentPid);

            // detached — must outlive the current process
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -NoProfile -File \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "self-update launch failed");
            return false;
        }
    }

    private static string ResolvePayloadRoot(string extractRoot)
    {
        if (File.Exists(Path.Combine(extractRoot, "AppWatch.exe")))
            return extractRoot;

        var namedDir = Path.Combine(extractRoot, "AppWatch");
        if (File.Exists(Path.Combine(namedDir, "AppWatch.exe")))
            return namedDir;

        foreach (var dir in Directory.GetDirectories(extractRoot))
        {
            if (File.Exists(Path.Combine(dir, "AppWatch.exe")))
                return dir;
        }

        return extractRoot;
    }

    private static string BuildPowerShellScript(
        int pid, string srcDir, string dstDir, string launchExe, string tempRoot, string logPath)
    {
        // data folders live next to the exe and must survive the update
        var preserve = new[] { "data", "logs" };
        var preserveList = string.Join(", ", preserve.Select(p => $"'{p}'"));

        srcDir = srcDir.Replace("\\", "`\\");
        dstDir = dstDir.Replace("\\", "`\\");
        launchExe = launchExe.Replace("\\", "`\\");
        tempRoot = tempRoot.Replace("\\", "`\\");
        logPath = logPath.Replace("\\", "`\\");

        return $@"
$ErrorActionPreference = 'SilentlyContinue'
$pidToWait = {pid}
$src = ""{srcDir}""
$dst = ""{dstDir}""
$exe = ""{launchExe}""
$tempRoot = ""{tempRoot}""
$logPath = ""{logPath}""
$preserveFolders = @({preserveList})

function Log {{
    param([string]$msg)
    ""$([DateTime]::Now.ToString('HH:mm:ss')) $msg"" | Out-File -FilePath $logPath -Append -Encoding UTF8
}}

Log ""Waiting for process $pidToWait to exit...""
try {{
    $proc = Get-Process -Id $pidToWait -ErrorAction Stop
    $proc.WaitForExit(30000) | Out-Null
}} catch {{
    Log ""Process $pidToWait not found, continuing.""
}}
Start-Sleep -Seconds 2

Log ""Removing old files in $dst ...""
Get-ChildItem -Path $dst -Exclude $preserveFolders | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Log ""Copying new files from $src to $dst ...""
Copy-Item ""$src\*"" -Destination $dst -Recurse -Force

Log ""Cleaning up $tempRoot ...""
Remove-Item -Path $tempRoot -Recurse -Force -ErrorAction SilentlyContinue

Log ""Starting $exe ...""
Start-Process -FilePath $exe -WorkingDirectory $dst

Log ""Update applied successfully.""
";
    }
}
