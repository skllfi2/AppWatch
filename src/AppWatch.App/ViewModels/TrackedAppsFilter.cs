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
    External = 7,
    GitVerse = 8,
    GitLab = 9,
    LocalInPlace = 10,
    CategoryDev = 11,
    CategoryAi = 12,
    CategorySystem = 13
}

public enum TrackedAppsSort
{
    ByName = 0,
    ByStatus = 1,
    BySource = 2,
    ByCategory = 3
}

public enum TrackedAppsViewMode
{
    Cards = 0,
    Compact = 1
}
