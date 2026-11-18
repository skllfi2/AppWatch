using System;
using System.Collections.Generic;

namespace AppWatch.Core.Domain;

public record PackageToolUpdate(
    PackageRunnerType RunnerType,
    string PackageName,
    string CurrentVersion,
    string AvailableVersion,
    string? CommandName = null
)
{
    public bool HasUpdate => !string.IsNullOrWhiteSpace(AvailableVersion) 
                             && !string.Equals(CurrentVersion, AvailableVersion, StringComparison.OrdinalIgnoreCase);
}
