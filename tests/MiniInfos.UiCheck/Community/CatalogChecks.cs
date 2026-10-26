using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using MiniInfos.Community;
using MiniInfos.Community.Catalog;
using MiniInfos.Infrastructure.Network;

namespace MiniInfos.UiCheck;

internal static partial class Program
{
    private static CatalogBrowser EmptyCatalog() => new(new CatalogAnswer { Snapshot = new(new(1, DateTimeOffset.UtcNow, []), []) },
        new HttpClient(new CatalogHttp(_ => throw new InvalidOperationException("Unexpected catalog request"))),
        new CatalogCache(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MiniInfos.EmptyCatalog")), PackageLibrary.Shared);

    private static void CheckCatalog(CardSet cards, Preferences preferences, string pictures)
    {
        const string petId = "test.catalog-owl";
        const string themeId = "test.catalog-theme";
        var downloads = new ConcurrentDictionary<string, byte[]>();
        var requested = new ConcurrentDictionary<string, byte>();
        var source = new CatalogAnswer();
        var calls = 0;
        using var http = new HttpClient(new CatalogHttp(request =>
        {
            Interlocked.Increment(ref calls);
            var name = System.IO.Path.GetFileName(request.RequestUri!.AbsolutePath);
            requested.TryAdd(name, 0);
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(downloads[name]) };
        }));
        var cache = new CatalogCache(System.IO.Path.Combine(pictures, "catalog-cache"));
        var browser = new CatalogBrowser(source, http, cache, PackageLibrary.Shared);
        var selectedPet = preferences.PetId;
        var selectedTheme = preferences.CommunityThemeId;
        var settings = new SettingsWindow(preferences, StartupShortcut.Default, cards.Main, () => { }, browser);
        try
        {
            source.Snapshot = Snapshot("1.0.0");
            source.Waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
            settings.Show();
            Check(source.Calls == 0 && calls == 0, "opening settings does not contact the community catalog");
            settings.ShowCommunityPage();
            PumpUntil(() => source.Calls == 1, TimeSpan.FromSeconds(5));
            Check(Find<TextBlock>(settings, "CatalogStatus").Text.Contains("Carregando"), "the catalog has a visible loading state");
            source.Waiting.SetResult(source.Snapshot);
            source.Waiting = null;
            WaitFor(() => Buttons().Any(button => button.Tag as string == petId), "the Mascotes category shows a catalog card");
            var button = Buttons().Single();
            WaitFor(() => Preview() is { IsFrozen: true }, "catalog previews arrive frozen and ready for the interface");
            Check(!requested.Keys.Any(name => name.EndsWith(".miniinfos")), "browsing the catalog never preloads installable packages");
            Render(settings, System.IO.Path.Combine(pictures, "catalog-mascots.png"));
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            WaitFor(() => preferences.PetId == petId && Buttons().Single().Content as string == "Instalado",
                "installing a catalog mascot selects it and changes the button to Instalado");
            Check(Preferences.Load().PetId == petId && cards.Pet?.Title.StartsWith("Corujinha do catálogo") == true,
                "the catalog uses the saved selection and the actual desktop mascot");

            source.Snapshot = Snapshot("1.1.0");
            Refresh();
            Check(Buttons().Single().Content as string == "Atualizar", "a newer catalog version offers Atualizar");
            Buttons().Single().RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            WaitFor(() => PackageLibrary.Shared.Find(PackageKind.Mascot, petId)?.Manifest.Version == "1.1.0"
                && Buttons().Single().Content as string == "Instalado", "updating replaces the installed package and keeps it selected");

            // A download that doesn't match the catalog is refused with a message: the app keeps running.
            source.Snapshot = Snapshot("1.2.0");
            var tampered = (byte[])downloads[$"{petId}-1.2.0.miniinfos"].Clone();
            tampered[^1] ^= 0xFF;
            downloads[$"{petId}-1.2.0.miniinfos"] = tampered;
            Refresh();
            Buttons().Single().RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            WaitFor(() => Buttons().Single().Content as string == "Tentar novamente",
                "a package that fails its SHA-256 check is refused with a message, without closing the app");
            Check(PackageLibrary.Shared.Find(PackageKind.Mascot, petId)?.Manifest.Version == "1.1.0", "and the installed version stays as it was");

            Find<RadioButton>(settings, "CatalogThemes").IsChecked = true;
            Check(Buttons().Single().Tag as string == themeId, "the Temas category displays theme packages separately");
            Buttons().Single().RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            WaitFor(() => preferences.CommunityThemeId == themeId && Buttons().Single().Content as string == "Instalado",
                "installing a catalog theme applies and saves it");
            Render(settings, System.IO.Path.Combine(pictures, "catalog-themes.png"));

            requested.Clear();
            source.Snapshot = Snapshot("1.1.0", extraThemes: 8);
            Refresh();
            WaitFor(() => requested.Keys.Any(name => name.EndsWith(".png")), "visible theme cards request their previews");
            var initialPreviews = requested.Keys.Count(name => name.EndsWith(".png"));
            Check(initialPreviews < 9 && !requested.Keys.Any(name => name.EndsWith(".miniinfos")),
                $"only visible cards download previews ({initialPreviews} of 9); no packages are preloaded");
            Find<ScrollViewer>(settings, "CatalogScroll").ScrollToEnd();
            WaitFor(() => Find<WrapPanel>(settings, "CatalogChoices").Children.OfType<Border>()
                .Last().Child is StackPanel last && last.Children.OfType<Image>().Single().Source is { IsFrozen: true },
                "scrolling requests the newly visible cards");

            source.Failure = new HttpRequestException("limited", null, HttpStatusCode.Forbidden);
            Refresh();
            Check(Find<TextBlock>(settings, "CatalogStatus").Text.Contains("Tente de novo em alguns minutos"),
                "a GitHub rate limit asks the user to retry in a few minutes");
            source.Failure = new HttpRequestException("offline");
            Refresh();
            Check(Find<TextBlock>(settings, "CatalogStatus").Text.Contains("Sem conexão"), "the catalog explains a missing internet connection");
            source.Failure = new System.IO.InvalidDataException("index digest");
            Refresh();
            Check(Find<TextBlock>(settings, "CatalogStatus").Text.Contains("Não foi possível ler o catálogo"),
                "an index that fails its digest check is reported, without closing the app");
            source.Failure = null;
            source.Snapshot = new(new(1, DateTimeOffset.UtcNow, []), []);
            Refresh();
            Check(Find<TextBlock>(settings, "CatalogStatus").Text.Contains("Nenhum pacote") && !Buttons().Any(),
                "an empty catalog displays a clear empty state");
        }
        finally
        {
            settings.Close();
            preferences.PetId = selectedPet;
            preferences.CommunityThemeId = selectedTheme;
            PackageLibrary.Shared.Remove(PackageKind.Mascot, petId);
            PackageLibrary.Shared.Remove(PackageKind.Theme, themeId);
        }

        IEnumerable<Button> Buttons() => Find<WrapPanel>(settings, "CatalogChoices").Children.OfType<Border>()
            .SelectMany(card => ((StackPanel)card.Child).Children.OfType<Button>());
        ImageSource? Preview() => Find<WrapPanel>(settings, "CatalogChoices").Children.OfType<Border>()
            .SelectMany(card => ((StackPanel)card.Child).Children.OfType<Image>()).FirstOrDefault()?.Source;
        void WaitFor(Func<bool> condition, string message)
        {
            var timer = Stopwatch.StartNew();
            PumpUntil(condition, TimeSpan.FromSeconds(10));
            Check(condition(), $"{message} ({timer.Elapsed.TotalMilliseconds:0} ms)");
        }
        void Refresh()
        {
            var task = settings.RefreshCatalogAsync();
            PumpUntil(() => task.IsCompleted, TimeSpan.FromSeconds(5));
            Check(task.IsCompletedSuccessfully, "a catalog refresh completes");
            task.GetAwaiter().GetResult();
        }
        CatalogSnapshot Snapshot(string version, int extraThemes = 0)
        {
            var model = PackageLibrary.Shared.Find(PackageKind.Mascot, "community.meu-mascote")!;
            var pet = model.Manifest with { Id = petId, Name = "Corujinha do catálogo", License = "CC0-1.0", Version = version };
            var theme = new PackageManifest(1, PackageKind.Theme, themeId, "Oceano do catálogo", "Comunidade", "1.0.0", "CC0-1.0",
                Theme: new(Colors: new() { ["Accent"] = "#38C9BA", ["CardBackground"] = "#14262D" }));
            var petSheet = File.ReadAllBytes(System.IO.Path.Combine(model.Directory, model.Manifest.Mascot!.SpriteSheet));
            var assets = new List<GitHubAsset>();
            var entries = new List<CatalogEntry>
            {
                Add(pet, new Dictionary<string, byte[]> { [pet.Mascot!.SpriteSheet] = petSheet },
                    PackageImages.Encode(PackagePreviews.Mascot(PetAppearances.Athena.Frame(PetState.Idle, 0)))),
                Add(theme, new Dictionary<string, byte[]>(), PackageImages.Encode(PackagePreviews.Theme(new(theme, pictures)))),
            };
            for (var index = 0; index < extraThemes; index++)
            {
                var extra = theme with
                {
                    Id = themeId + "-" + index, Name = "Tema da comunidade " + (index + 1),
                    Theme = new(Colors: new() { ["Accent"] = $"#{60 + index * 19:X2}80B2" }),
                };
                entries.Add(Add(extra, new(), PackageImages.Encode(PackagePreviews.Theme(new(extra, pictures)))));
            }
            return new(new(1, DateTimeOffset.UtcNow, entries.ToArray()), assets.ToArray());

            CatalogEntry Add(PackageManifest manifest, Dictionary<string, byte[]> contents, byte[] preview)
            {
                var prefix = $"{manifest.Id}-{manifest.Version}";
                var archive = System.IO.Path.Combine(pictures, prefix + ".miniinfos");
                PackageLibrary.Create(archive, manifest, contents);
                var bytes = File.ReadAllBytes(archive);
                downloads[prefix + ".miniinfos"] = bytes;
                downloads[prefix + ".png"] = preview;
                foreach (var name in new[] { prefix + ".miniinfos", prefix + ".png" })
                    assets.Add(new(name, "https://github.com/test/catalog/releases/download/catalog-1/" + name,
                        "sha256:" + Hash(downloads[name]), downloads[name].Length));
                return new(manifest.Id, manifest.Kind, manifest.Name, manifest.Author, manifest.Version, manifest.License,
                    manifest.Description ?? "", prefix + ".miniinfos", bytes.Length, Hash(bytes), prefix + ".png", Hash(preview));
            }
        }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed class CatalogAnswer : ICatalogSource
    {
        public CatalogSnapshot Snapshot { get; set; } = null!;
        public TaskCompletionSource<CatalogSnapshot>? Waiting { get; set; }
        public Exception? Failure { get; set; }
        public int Calls { get; private set; }
        public Task<CatalogSnapshot> LoadAsync(bool force, CancellationToken cancellation)
        {
            Calls++;
            return Failure is { } error ? Task.FromException<CatalogSnapshot>(error)
                : Waiting is { } waiting ? waiting.Task.WaitAsync(cancellation) : Task.FromResult(Snapshot);
        }
    }

    private sealed class CatalogHttp(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(answer(request));
    }
}
