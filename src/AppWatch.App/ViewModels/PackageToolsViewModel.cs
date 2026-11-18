using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppWatch.Core.Domain;
using AppWatch.Infrastructure.System;
using AppWatch.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppWatch.ViewModels;

public partial class PackageToolsViewModel : ObservableObject
{
    private readonly PackageToolScanner _scanner;
    private readonly ConsoleToolRunner _runner;

    public Loc Loc => Loc.Instance;

    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    [ObservableProperty]
    public partial bool IsUpdatingAny { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial int SelectedFilterIndex { get; set; } = 0; // 0=All, 1=Npm, 2=Bun, 3=Dotnet, 4=Cargo, 5=Scoop, 6=Choco

    [ObservableProperty]
    public partial string NewPackageName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int SelectedInstallManagerIndex { get; set; } = 0; // 0=npm, 1=bun, 2=dotnet, 3=cargo, 4=scoop, 5=choco

    [ObservableProperty]
    public partial bool IsInstallingPackage { get; set; }

    [ObservableProperty]
    public partial string? InstallMessage { get; set; }

    public ObservableCollection<PackageToolItem> AllTools { get; } = [];
    public ObservableCollection<PackageToolItem> FilteredTools { get; } = [];

    public int TotalCount => AllTools.Count;
    public int UpdatesAvailableCount => AllTools.Count(t => t.HasUpdate);

    public PackageToolsViewModel(PackageToolScanner? scanner = null, ConsoleToolRunner? runner = null)
    {
        _scanner = scanner ?? new PackageToolScanner();
        _runner = runner ?? new ConsoleToolRunner();
    }

    partial void OnSelectedFilterIndexChanged(int value) => ApplyFilter();

    public void ApplyFilter()
    {
        FilteredTools.Clear();
        var items = SelectedFilterIndex switch
        {
            1 => AllTools.Where(t => t.RunnerType == PackageRunnerType.Npm),
            2 => AllTools.Where(t => t.RunnerType == PackageRunnerType.Bun),
            3 => AllTools.Where(t => t.RunnerType == PackageRunnerType.DotnetTool),
            4 => AllTools.Where(t => t.RunnerType == PackageRunnerType.Cargo),
            5 => AllTools.Where(t => t.RunnerType == PackageRunnerType.Scoop),
            6 => AllTools.Where(t => t.RunnerType == PackageRunnerType.Chocolatey),
            _ => AllTools
        };

        foreach (var item in items)
        {
            FilteredTools.Add(item);
        }
    }

    [RelayCommand]
    public async Task ScanAsync(CancellationToken cancellationToken = default)
    {
        if (IsScanning) return;

        IsScanning = true;
        StatusMessage = Loc["ScanningPackageTools"];

        try
        {
            var results = await _scanner.ScanAllAsync(cancellationToken);

            AllTools.Clear();
            foreach (var item in results)
            {
                AllTools.Add(new PackageToolItem(item));
            }

            ApplyFilter();
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(UpdatesAvailableCount));
            StatusMessage = string.Format(Loc["PackageToolsFoundCount"], TotalCount);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var tool in FilteredTools)
        {
            if (tool.HasUpdate)
            {
                tool.IsSelected = true;
            }
        }
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var tool in FilteredTools)
        {
            tool.IsSelected = false;
        }
    }

    [RelayCommand]
    private async Task UpdateSelectedAsync(CancellationToken cancellationToken)
    {
        var selected = FilteredTools.Where(t => t.IsSelected && t.HasUpdate).ToList();
        if (selected.Count == 0) return;

        IsUpdatingAny = true;
        try
        {
            foreach (var item in selected)
            {
                item.IsUpdating = true;
                item.StatusText = Loc["Installing"];

                try
                {
                    var (success, detail) = await UpdateSingleToolAsync(item, cancellationToken);
                    item.StatusText = success ? Loc["Updated"] : detail;
                }
                catch (Exception ex)
                {
                    item.StatusText = ex.Message;
                }
                finally
                {
                    item.IsUpdating = false;
                }
            }

            // Rescan after update to refresh versions
            await ScanAsync(cancellationToken);
        }
        finally
        {
            IsUpdatingAny = false;
        }
    }

    private async Task<(bool Success, string Detail)> UpdateSingleToolAsync(PackageToolItem item, CancellationToken ct)
    {
        var probe = _runner.ProbeRunner(item.RunnerType);
        if (!probe.IsAvailable || probe.ExecutablePath is null)
            return (false, $"{item.RunnerType} not available");

        string args = item.RunnerType switch
        {
            PackageRunnerType.Npm => $"install -g {item.Name}@latest",
            PackageRunnerType.Bun => $"add -g {item.Name}@latest",
            PackageRunnerType.DotnetTool => $"tool update -g {item.Name}",
            PackageRunnerType.Cargo => $"install {item.Name}",
            PackageRunnerType.Scoop => $"update {item.Name}",
            PackageRunnerType.Chocolatey => $"upgrade {item.Name} -y",
            _ => throw new NotSupportedException()
        };

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = probe.ExecutablePath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = System.Diagnostics.Process.Start(psi);
        if (proc is null) return (false, "Failed to start process");

        await proc.WaitForExitAsync(ct);
        return (proc.ExitCode == 0, proc.ExitCode == 0 ? "OK" : "Exit code " + proc.ExitCode);
    }

    [RelayCommand]
    private async Task InstallPackageAsync(CancellationToken cancellationToken)
    {
        var rawName = NewPackageName?.Trim();
        if (string.IsNullOrWhiteSpace(rawName)) return;

        IsInstallingPackage = true;
        InstallMessage = null;

        var runnerType = SelectedInstallManagerIndex switch
        {
            0 => PackageRunnerType.Npm,
            1 => PackageRunnerType.Bun,
            2 => PackageRunnerType.DotnetTool,
            3 => PackageRunnerType.Cargo,
            4 => PackageRunnerType.Scoop,
            5 => PackageRunnerType.Chocolatey,
            _ => PackageRunnerType.Npm
        };

        var probe = _runner.ProbeRunner(runnerType);
        if (!probe.IsAvailable || probe.ExecutablePath is null)
        {
            InstallMessage = $"{runnerType} is not installed or not in PATH";
            IsInstallingPackage = false;
            return;
        }

        string args = runnerType switch
        {
            PackageRunnerType.Npm => $"install -g {rawName}",
            PackageRunnerType.Bun => $"add -g {rawName}",
            PackageRunnerType.DotnetTool => $"tool install -g {rawName}",
            PackageRunnerType.Cargo => $"install {rawName}",
            PackageRunnerType.Scoop => $"install {rawName}",
            PackageRunnerType.Chocolatey => $"install {rawName} -y",
            _ => throw new NotSupportedException()
        };

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = probe.ExecutablePath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null)
            {
                InstallMessage = "Failed to launch installer process";
                return;
            }

            await proc.WaitForExitAsync(cancellationToken);
            if (proc.ExitCode == 0)
            {
                NewPackageName = string.Empty;
                InstallMessage = $"{rawName} successfully installed!";
                await ScanAsync(cancellationToken);
            }
            else
            {
                var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
                InstallMessage = string.IsNullOrWhiteSpace(stderr) ? $"Exit code {proc.ExitCode}" : stderr.Trim();
            }
        }
        catch (Exception ex)
        {
            InstallMessage = ex.Message;
        }
        finally
        {
            IsInstallingPackage = false;
        }
    }
}
