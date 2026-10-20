using AppWatch.Core.Domain;

namespace AppWatch.Core.Tests;

/// <summary>UPD-02: leftover-journal decision — restore the backup iff it exists.</summary>
public class PendingUpdatePolicyTests
{
    [Theory]
    [InlineData(true, PendingUpdatePolicy.Action.RestoreBackup)]
    [InlineData(false, PendingUpdatePolicy.Action.ClearOnly)]
    public void Decide_MatchesBackupPresence(bool backupExists, PendingUpdatePolicy.Action expected)
        => Assert.Equal(expected, PendingUpdatePolicy.Decide(backupExists));

    [Fact]
    public void PendingUpdate_SerializesCamelCase()
    {
        // the journal on disk must be camelCase — matches UpdateJournalStore's options
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };
        var pending = new PendingUpdate("App", @"C:\Apps\App", "1.0.0", "1.1.0",
            new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
        var json = System.Text.Json.JsonSerializer.Serialize(pending, options);
        Assert.Contains("\"appName\":", json);
        Assert.Contains("\"installDirectory\":", json);
        Assert.Contains("\"toVersion\":", json);
    }

    [Fact]
    public void PendingUpdate_RoundTrips()
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };
        var pending = new PendingUpdate("App", @"C:\Apps\App", "1.0.0", "1.1.0",
            new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
        var json = System.Text.Json.JsonSerializer.Serialize(pending, options);
        var back = System.Text.Json.JsonSerializer.Deserialize<PendingUpdate>(json, options);
        Assert.Equal(pending, back);
    }
}
