using System.IO;
using AppWatch.Core.Domain;
using AppWatch.Infrastructure;
using Xunit;

namespace AppWatch.Core.Tests;

public class WingetCacheTests
{
    [Fact]
    public void InvalidateCache_ClearsCacheWithoutExceptions()
    {
        WingetService.InvalidateCache();
    }
}
