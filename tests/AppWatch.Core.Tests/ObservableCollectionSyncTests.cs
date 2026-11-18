using System.Collections.ObjectModel;
using System.Linq;
using AppWatch.Core.Domain;
using Xunit;

namespace AppWatch.Core.Tests;

public class ObservableCollectionSyncTests
{
    [Fact]
    public void SyncWith_AppliesDifferentialUpdatesCorrectly()
    {
        var target = new ObservableCollection<string> { "A", "B", "C", "D" };
        var source = new[] { "B", "E", "D" };

        target.SyncWith(source);

        Assert.Equal(3, target.Count);
        Assert.Equal("B", target[0]);
        Assert.Equal("E", target[1]);
        Assert.Equal("D", target[2]);
    }

    [Fact]
    public void SyncWith_WhenSourceEmpty_ClearsTarget()
    {
        var target = new ObservableCollection<string> { "X", "Y" };
        target.SyncWith(Enumerable.Empty<string>());

        Assert.Empty(target);
    }
}
