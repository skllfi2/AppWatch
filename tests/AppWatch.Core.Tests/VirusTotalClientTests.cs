using System.Net;
using System.Text.Json;
using AppWatch.Core.Domain;
using Xunit;

namespace AppWatch.Core.Tests;

public class VirusTotalClientTests
{
    [Fact]
    public async Task CheckFileHashAsync_WhenNoApiKey_ReturnsNull()
    {
        using var client = new HttpClient();
        var vt = new VirusTotalClient(client, apiKey: null);
        var res = await vt.CheckFileHashAsync("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

        Assert.Null(res);
    }

    [Fact]
    public async Task CheckFileHashAsync_WhenNotFound_ReturnsUnknownReport()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.NotFound, "{}");
        using var client = new HttpClient(handler);
        var vt = new VirusTotalClient(client, apiKey: "fake-key");

        var res = await vt.CheckFileHashAsync("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

        Assert.NotNull(res);
        Assert.False(res.IsKnown);
        Assert.True(res.IsSafe);
    }

    [Fact]
    public async Task CheckFileHashAsync_WhenClean_ReturnsSafeReport()
    {
        var json = """
        {
          "data": {
            "attributes": {
              "last_analysis_stats": {
                "malicious": 0,
                "suspicious": 0,
                "harmless": 65,
                "undetected": 5
              }
            }
          }
        }
        """;
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, json);
        using var client = new HttpClient(handler);
        var vt = new VirusTotalClient(client, apiKey: "fake-key");

        var res = await vt.CheckFileHashAsync("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

        Assert.NotNull(res);
        Assert.True(res.IsKnown);
        Assert.True(res.IsSafe);
        Assert.Equal(65, res.HarmlessCount);
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _code;
        private readonly string _content;

        public MockHttpMessageHandler(HttpStatusCode code, string content)
        {
            _code = code;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var res = new HttpResponseMessage(_code)
            {
                Content = new StringContent(_content, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(res);
        }
    }
}
