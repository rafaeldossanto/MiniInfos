using MiniInfos.Infrastructure.Storage;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace MiniInfos.Desktop.Platform;

/// <summary>One writer per user/data directory; subsequent launches only bring the cards back.</summary>
internal sealed class SingleInstance : IDisposable
{
    private readonly string _name = "MiniInfos-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(AppPaths.Data))))[..24];
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _stop = new();
    public bool IsPrimary { get; }
    public SingleInstance()
    {
        _mutex = new Mutex(false, _name);
        try { IsPrimary = _mutex.WaitOne(0); }
        catch (AbandonedMutexException) { IsPrimary = true; }
    }
    public async Task ActivateAsync()
    {
        using var pipe = new NamedPipeClientStream(".", _name, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(3000);
        await pipe.WriteAsync(new byte[] { 1 });
    }
    public async Task ListenAsync(Action activate)
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(_name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(_stop.Token);
                var signal = new byte[1];
                if (await server.ReadAsync(signal, _stop.Token) == 1 && signal[0] == 1) activate();
            }
            catch (OperationCanceledException) { break; }
            catch (IOException) { if (!_stop.IsCancellationRequested) await Task.Delay(200, _stop.Token).ConfigureAwait(false); }
        }
    }
    public void Dispose()
    {
        _stop.Cancel();
        if (IsPrimary) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
