using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using MiniInfos.Community;
using MiniInfos.Desktop.UI;
using MiniInfos.Infrastructure.Desktop;
using MiniInfos.Mascot;
using MiniInfos.Settings;
using MiniInfos.Updates;
using System.Runtime.InteropServices;
using Avalonia.VisualTree;

[assembly: AvaloniaTestApplication(typeof(MiniInfos.Tests.Desktop.TestApplication))]

namespace MiniInfos.Tests.Desktop;

public static class TestApplication
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<MiniInfos.Desktop.DesktopApp>().UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public sealed class NativeTests
{
    [Theory]
    [InlineData("macos", Architecture.Arm64, "MiniInfos-macos-arm64.dmg")]
    [InlineData("macos", Architecture.X64, "MiniInfos-macos-x64.dmg")]
    [InlineData("linux", Architecture.Arm64, "MiniInfos-linux-arm64.tar.gz")]
    [InlineData("linux", Architecture.X64, "MiniInfos-linux-x64.tar.gz")]
    [InlineData("windows", Architecture.X64, "MiniInfos-Setup.exe")]
    public void UpdatesChooseAnAssetForTheRunningPlatform(string os, Architecture arch, string expected) => Assert.Equal(expected, ReleaseAssets.For(os, arch));

    [Fact]
    public void UnixDoesNotCallWindowsPackageApi()
    {
        if (!OperatingSystem.IsWindows()) Assert.False(InstalledApps.HasStorePackage("example"));
    }

    [Fact]
    public void ExecutablesOnUnixNeedExecutePermission()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "MiniInfos-path-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "codex");
        try
        {
            File.WriteAllText(path, "#!/bin/sh\nexit 0\n");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            Assert.Null(CommandPath.Find(root, "codex"));
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            Assert.Equal(path, CommandPath.Find(root, "codex"));
        }
        finally { File.Delete(path); Directory.Delete(root); }
    }

    [AvaloniaFact]
    public void AllApprovedThemesAndFontsLoadWithOriginalArtworkPreserved()
    {
        var path = Path.Combine(Path.GetTempPath(), "MiniInfos-native-" + Guid.NewGuid().ToString("N") + ".json");
        var preferences = new Preferences(path);
        try
        {
            foreach (var choice in Enum.GetValues<ThemeChoice>())
            {
                preferences.Theme = choice;
                var palette = new Palette(preferences);
                Assert.NotNull(palette["Accent"]);
                if (choice is ThemeChoice.Classic or ThemeChoice.ClassicDark or ThemeChoice.ClassicLight) Assert.NotNull(palette.Artwork);
                if (choice == ThemeChoice.Classic) { Assert.False(palette.Inset); Assert.Equal(96, palette.HeaderHeight); }
                if (choice is ThemeChoice.ClassicDark or ThemeChoice.ClassicLight) Assert.True(palette.Inset);
            }
            foreach (var choice in Enum.GetValues<FontChoice>())
            {
                var family = Palette.Family(choice);
                var typeface = new Avalonia.Media.Typeface(family).GlyphTypeface;
                Assert.True(typeface.GlyphCount > 100);
                Assert.Equal(family.FamilyNames.PrimaryFamilyName, typeface.FamilyName);
            }
        }
        finally { File.Delete(path); }
    }

    [AvaloniaFact]
    public void OwlStatesDecodeAndBlendOnTheNativeRenderer()
    {
        foreach (var state in Enum.GetValues<PetState>())
        {
            var frame = PetSprites.Frame(state, 0);
            Assert.Equal(320 * 320 * 4, frame.Pixels.Length);
            Assert.True(PetPixels.Bounds(frame.Pixels.Span, 320).Height > 20);
        }
        var player = new PetAnimationPlayer((state, frame) => PetSprites.Frame(state, frame).Pixels, 320 * 320 * 4);
        Assert.True(player.Draw(PetState.Idle, TimeSpan.Zero));
        Assert.True(player.Draw(PetState.ProtectEyes, TimeSpan.FromMilliseconds(300)));
        Assert.True(player.Draw(PetState.ProtectEyes, TimeSpan.FromMilliseconds(450)));
        using var preview = PackagePreviews.Mascot(PetSprites.Frame(PetState.Idle, 0));
        Assert.Equal(new PixelSize(128, 128), preview.PixelSize);
    }

    [AvaloniaFact]
    public void NativeCardAndSettingsRenderWithSplitView()
    {
        var root = Path.Combine(Path.GetTempPath(), "MiniInfos-render-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var preferences = new Preferences(Path.Combine(root, "preferences.json"));
        var session = new MiniInfos.Desktop.DesktopSession(null!, preferences, MiniInfos.Desktop.SmokeCheck.Providers);
        foreach (var provider in session.Providers) session.Pages[provider.Id] = provider.ReadAsync(DateTimeOffset.Now, false, false).Result;
        // Extras have a separately owned store, so no installed card settings are written by this check.
        var store = MiniInfos.Infrastructure.Storage.CardStore.NewExtra(new CardSettings(100, 100, Page: "claude"));
        try
        {
            var card = new CardWindow(session, store); card.Show(); card.ToggleSplit();
            using var frame = card.CaptureRenderedFrame();
            Assert.NotNull(frame); Assert.Equal(600, card.Width);
            foreach (var button in card.GetVisualDescendants().OfType<Button>())
            {
                var origin = button.TranslatePoint(new Point(0, 0), card)!.Value;
                Assert.True(origin.Y + button.Bounds.Height <= card.ClientSize.Height + 1, "A card button was clipped below the window.");
            }
            var settings = new SettingsWindow(session); settings.Show();
            settings.Render(); settings.Render(); // Repeated live appearance/language rebuilds must not reuse a parented control.
            using var settingsFrame = settings.CaptureRenderedFrame(); Assert.NotNull(settingsFrame);
            var output = Environment.GetEnvironmentVariable("MINIINFOS_TEST_ARTIFACTS");
            if (output is not null) { Directory.CreateDirectory(output); frame.Save(Path.Combine(output, "native-split.png"), PngBitmapEncoderOptions.Default); settingsFrame.Save(Path.Combine(output, "native-settings.png"), PngBitmapEncoderOptions.Default); }
            settings.Close(); card.Close();
        }
        finally { store.Forget(); File.Delete(Path.Combine(root, "preferences.json")); Directory.Delete(root); }
    }

    [Fact]
    public void DragPhysicsAndLocalDropRemainFiniteAcrossFrameRates()
    {
        foreach (var hz in new[] { 30, 60, 120, 144 })
        {
            var physics = new PetPendulum(); physics.Reset(new Point(100, 100), TimeSpan.Zero, 0.5);
            for (var step = 1; step <= hz; step++)
            {
                var angle = physics.Advance(new Point(100 + 80 * Math.Cos(step * Math.Tau / hz), 100 + 80 * Math.Sin(step * Math.Tau / hz)), new Vector(0, 130), TimeSpan.FromSeconds((double)step / hz));
                Assert.True(double.IsFinite(angle));
            }
            var bounds = new Rect(-50, -100, 100, 120);
            var drop = new PetDrop(physics.Angle, physics.AngularVelocity, new Vector(1, 1), physics.ScreenVelocity, bounds, bounds, 160, 48);
            Assert.True(drop.At(drop.Duration).IsSettled);
            Assert.InRange(Math.Abs(Math.IEEERemainder(drop.At(drop.Duration).Angle, 360)), 0, 0.001);
        }
    }
}
