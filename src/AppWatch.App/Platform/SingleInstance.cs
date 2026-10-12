// Ported from ZuiV2 Platform/SingleInstance.cs (our own code).
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace AppWatch.Platform;

/// <summary>
/// Single-instance detection via named Mutex + named pipe communication:
/// a second instance sends "SHOW" to the first, which brings its window to front.
/// </summary>
public static class SingleInstance
{
    private static Mutex? _mutex;
    private static CancellationTokenSource? _pipeCts;
    private static Task? _pipeListener;

    public static string GetInstanceKey()
    {
        var basePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(basePath));
        var digest = Convert.ToHexString(hash).ToLowerInvariant()[..12];
        return $"appwatch_{digest}";
    }

    public static bool TryAcquire()
    {
        var key = GetInstanceKey();
        _mutex = new Mutex(initiallyOwned: true, name: key, createdNew: out var createdNew);
        return createdNew;
    }

    public static async Task<bool> NotifyExistingInstanceAsync(string message = "SHOW")
    {
        var key = GetInstanceKey();
        try
        {
            await using var client = new NamedPipeClientStream(".", key, PipeDirection.Out);
            await client.ConnectAsync(220);
            var bytes = Encoding.UTF8.GetBytes(message);
            await client.WriteAsync(bytes);
            await client.FlushAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void StartListening(Action<string> onMessage)
    {
        var key = GetInstanceKey();
        _pipeCts = new CancellationTokenSource();
        _pipeListener = Task.Run(async () =>
        {
            while (!_pipeCts.Token.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(key, PipeDirection.In);
                    await server.WaitForConnectionAsync(_pipeCts.Token);
                    using var reader = new StreamReader(server);
                    var message = await reader.ReadToEndAsync(_pipeCts.Token);
                    if (!string.IsNullOrEmpty(message))
                    {
                        onMessage(message.Trim());
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(100, _pipeCts.Token);
                }
            }
        }, _pipeCts.Token);
    }

    public static void Stop()
    {
        _pipeCts?.Cancel();
        _pipeCts?.Dispose();
        try { _mutex?.ReleaseMutex(); } catch (ApplicationException) { }
        _mutex?.Dispose();
    }
}
