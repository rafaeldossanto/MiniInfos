using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace MiniInfos.Updates;

/// <summary>Where an update stands, as the banner shows it.</summary>
public enum UpdateStage
{
    /// <summary>Nothing newer: the banner stays away.</summary>
    None,
    Available,
    Downloading,

    /// <summary>The installer runs and the app quits; the installer opens the new version.</summary>
    Installing,

    /// <summary>The download or its check failed: asking again retries.</summary>
    Failed,
}

/// <summary>
/// Looks for a newer MiniInfos now and then, and installs it when asked: downloads the installer, checks it
/// against the release's SHA-256, then hands it to <c>launch</c>, which runs it silently and quits.
/// </summary>
public sealed class Updater(IUpdateSource source, HttpClient http, Version current, Action<string> launch) : IDisposable
{
    /// <summary>What the installer is run with: no windows, and open the new version when done.</summary>
    public const string InstallerArguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /RELAUNCH=1";

    public static readonly TimeSpan CheckEvery = TimeSpan.FromHours(6);
    private static readonly TimeSpan FirstCheckAfter = TimeSpan.FromSeconds(20);

    private readonly CancellationTokenSource _stop = new();

    public UpdateStage Stage { get; private set; }

    public AvailableUpdate? Available { get; private set; }

    /// <summary>How much of the installer has come, from 0 to 1, while downloading.</summary>
    public double Progress { get; private set; }

    public event Action? Changed;

    /// <summary>Runs the downloaded installer for real and quits the app: what the app hands as <c>launch</c>.</summary>
    public static Action<string> RunInstaller(Action quit) => installer =>
    {
        Process.Start(new ProcessStartInfo(installer, InstallerArguments) { UseShellExecute = false });
        quit();
    };

    /// <summary>Checks a little after the app opens, then every few hours, from the thread that starts it.</summary>
    public async void Start()
    {
        try
        {
            // An installer from an update already made is not needed anymore.
            DeleteDownloads();
            await Task.Delay(FirstCheckAfter, _stop.Token);
            using var timer = new PeriodicTimer(CheckEvery);
            do await CheckAsync();
            while (await timer.WaitForNextTickAsync(_stop.Token));
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async Task CheckAsync()
    {
        if (Stage is UpdateStage.Downloading or UpdateStage.Installing) return;

        var update = await source.NewerThanAsync(current, _stop.Token);
        if (update is null || update == Available) return;

        Available = update;
        Stage = UpdateStage.Available;
        Changed?.Invoke();
    }

    /// <summary>Downloads, checks and launches the installer; a failure leaves the banner offering to try again.</summary>
    public async Task InstallAsync()
    {
        if (Available is not { } update || Stage is UpdateStage.Downloading or UpdateStage.Installing) return;

        Move(UpdateStage.Downloading, progress: 0);
        try
        {
            var installer = await DownloadAsync(update);
            Move(UpdateStage.Installing, progress: 1);
            launch(installer);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException
            or UnauthorizedAccessException or System.ComponentModel.Win32Exception or OperationCanceledException)
        {
            Move(UpdateStage.Failed, progress: 0);
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    private static string Downloads => Path.Combine(Path.GetTempPath(), "MiniInfos-update");

    private static void DeleteDownloads()
    {
        try
        {
            if (Directory.Exists(Downloads)) Directory.Delete(Downloads, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private async Task<string> DownloadAsync(AvailableUpdate update)
    {
        Directory.CreateDirectory(Downloads);
        var installer = Path.Combine(Downloads, $"MiniInfos-Setup-{update.Version}.exe");

        using (var response = await http.GetAsync(update.Installer, HttpCompletionOption.ResponseHeadersRead, _stop.Token))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;
            await using var body = await response.Content.ReadAsStreamAsync(_stop.Token);
            await using var file = File.Create(installer);
            var buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await body.ReadAsync(buffer, _stop.Token)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), _stop.Token);
                received += read;
                if (total > 0) Move(UpdateStage.Downloading, (double)received / total.Value);
            }
        }

        // The file runs with the person's rights: only the exact bytes the release announced may.
        await using (var written = File.OpenRead(installer))
        {
            if (Convert.ToHexString(await SHA256.HashDataAsync(written, _stop.Token)) == update.Sha256) return installer;
        }

        File.Delete(installer);
        throw new InvalidDataException("The downloaded installer does not match the release's SHA-256.");
    }

    /// <summary>Moves on, telling the banner; while downloading, only each whole percent is told.</summary>
    private void Move(UpdateStage stage, double progress)
    {
        var isNews = stage != Stage || (int)(progress * 100) != (int)(Progress * 100);
        Stage = stage;
        Progress = progress;
        if (isNews) Changed?.Invoke();
    }
}
