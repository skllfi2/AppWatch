namespace AppWatch.ViewModels;

public enum TrackedAppsFilter
{
    All = 0,
    Updates = 1,
    Errors = 2,
    UpToDate = 3,
    Disabled = 4,
    GitHub = 5,
    Winget = 6,
    External = 7
}

public enum TrackedAppsSort
{
    ByName = 0,
    ByStatus = 1,
    BySource = 2
}
