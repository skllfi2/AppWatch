using AppWatch.Core.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace AppWatch.ViewModels;

public partial class PackageToolItem : ObservableObject
{
    public PackageToolUpdate Model { get; }

    public string Name => Model.PackageName;
    public string CurrentVersion => Model.CurrentVersion;
    public string AvailableVersion => Model.AvailableVersion;
    public bool HasUpdate => Model.HasUpdate;

    public string Description => GetKnownToolDescription(Model.PackageName, Model.RunnerType);

    private static string GetKnownToolDescription(string name, PackageRunnerType runner)
    {
        var lower = name.ToLowerInvariant();
        if (lower.StartsWith("chocolatey-compatibility.extension") || lower.StartsWith("chocolatey-core.extension") ||
            lower.StartsWith("chocolatey-dotnetfx.extension") || lower.StartsWith("chocolatey-visualstudio.extension") ||
            lower.StartsWith("chocolatey-windowsupdate.extension"))
        {
            return "Служебное расширение Chocolatey для сценариев установки и интеграции";
        }
        if (lower == "chocolatey") return "Менеджер пакетов командной строки для Windows";
        if (lower.StartsWith("kb") && lower.Length >= 8) return "Системное обновление безопасности / накопительный пакет Windows Update";
        if (lower == "dotnetfx") return "Microsoft .NET Framework Runtime";
        if (lower.StartsWith("vcredist")) return "Распространяемый пакет среды выполнения Microsoft Visual C++";
        if (lower.StartsWith("visualstudio") && lower.Contains("buildtools")) return "Набор компиляторов и инструментов сборки Visual Studio Build Tools";
        if (lower.StartsWith("visualstudio") && lower.Contains("workload")) return "Компоненты рабочей нагрузки C++ Visual Studio";
        if (lower == "visualstudio-installer") return "Установщик Microsoft Visual Studio Installer";
        if (lower.StartsWith("python")) return "Интерпретатор языка программирования Python";
        if (lower == "dotnet-counters") return "Утилита мониторинга производительности и метрик среды .NET";
        if (lower == "dotnet-dump") return "Утилита сбора и анализа дампов памяти процессов .NET";
        if (lower == "dotnet-gcdump") return "Утилита сбора дампов сборщика мусора .NET";
        if (lower == "dotnet-trace") return "Утилита профилирования и трассировки процессов .NET";
        if (lower == "csharpier") return "Форматировщик исходного кода C#";
        return runner switch
        {
            PackageRunnerType.DotnetTool => "Глобальная консольная утилита платформы .NET",
            PackageRunnerType.Chocolatey => "Системный пакет или зависимость Chocolatey",
            PackageRunnerType.Scoop => "Портативная утилита среды Scoop",
            PackageRunnerType.Cargo => "Консольная утилита языка Rust",
            PackageRunnerType.Npm => "Глобальный Node.js CLI пакет",
            PackageRunnerType.Bun => "Глобальный Bun CLI пакет",
            _ => "Инструмент разработчика"
        };
    }
    public PackageRunnerType RunnerType => Model.RunnerType;

    public string RunnerBadge => RunnerType switch
    {
        PackageRunnerType.Npm => "npm",
        PackageRunnerType.Bun => "bun",
        PackageRunnerType.DotnetTool => ".NET Tool",
        PackageRunnerType.Cargo => "cargo",
        PackageRunnerType.Pipx => "pipx",
        _ => RunnerType.ToString()
    };

    public Brush RunnerBrush => RunnerType switch
    {
        PackageRunnerType.Npm => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 203, 56, 55)),     // npm red
        PackageRunnerType.Bun => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 244, 114, 182)),    // bun pink
        PackageRunnerType.DotnetTool => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 81, 43, 212)), // dotnet purple
        PackageRunnerType.Cargo => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 234, 126, 34)),   // cargo orange
        _ => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0, 120, 212))
    };

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool IsUpdating { get; set; }

    [ObservableProperty]
    public partial string? StatusText { get; set; }

    public PackageToolItem(PackageToolUpdate model)
    {
        Model = model;
        IsSelected = model.HasUpdate;
    }
}
