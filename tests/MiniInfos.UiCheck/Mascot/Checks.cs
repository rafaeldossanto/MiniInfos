namespace MiniInfos.UiCheck;

internal static partial class Program
{
    private static void CheckPet(CardSet cards, Preferences preferences, SettingsWindow settings, string pictures)
    {
        Check(cards.Pet is null && !preferences.PetEnabled, "the desktop pet is optional and stays unloaded while disabled");
        settings.ShowPetPage();
        Pump(TimeSpan.FromMilliseconds(100));
        var add = Find<Button>(settings, "AddPetPackage");
        var athena = Find<RadioButton>(settings, "AthenaPet");
        var athenaPosition = athena.TranslatePoint(new Point(), add);
        Check(Math.Abs(add.ActualWidth - add.ActualHeight) < 1 && add.ActualWidth >= 48
            && athenaPosition.X >= add.ActualWidth && Math.Abs(athenaPosition.Y) < 1,
            "the square add tile precedes selectable mascot previews on the same row");
        Find<CheckBox>(settings, "PetToggle").IsChecked = true;
        Pump(TimeSpan.FromMilliseconds(500));
        var pet = cards.Pet;
        Check(pet is { IsVisible: true, AllowsTransparency: true, ShowInTaskbar: false, Owner: null },
            "enabling the owl opens an independent transparent desktop window");
        if (pet is null) return;
        Check(Preferences.Load().PetEnabled, "the enabled choice is saved");
        Check(Enum.GetValues<PetState>().All(state => Enumerable.Range(0, PetSprites.FrameCount).All(index =>
        {
            var frame = PetSprites.Frame(state, index);
            return frame.Source.IsFrozen && !frame.Hits(new Point(0, 0)) && frame.Hits(new Point(160, 200));
        })), "every animation frame is present, grounded and transparent outside the owl");
        var petHandle = new WindowInteropHelper(pet).Handle;
        var corner = pet.PointToScreen(new Point(2, 2));
        var point = new IntPtr(((int)corner.Y << 16) | ((int)corner.X & 0xFFFF));
        Check(SendMessage(petHandle, WmNcHitTest, IntPtr.Zero, point).ToInt64() == -1,
            "clicks in the transparent padding pass through to the desktop");
        cards.Hide();
        Check(pet.IsVisible && !cards.Main.IsVisible, "hiding the card leaves the owl available on the desktop");
        var sprite = Find<Image>(pet, "Sprite");
        var positionBeforeBreath = new Point(pet.Left, pet.Top);
        var beforeBreath = sprite.RenderTransform.Value;
        Pump(TimeSpan.FromMilliseconds(200));
        Check(sprite.RenderTransform.Value != beforeBreath && new Point(pet.Left, pet.Top) == positionBeforeBreath,
            "the owl breathes continuously between sprite frames without moving its desktop window");
        CheckPetInteractions(pet, pictures);
        var menu = sprite.ContextMenu!;
        menu.PlacementTarget = sprite;
        menu.IsOpen = true;
        Pump(TimeSpan.FromMilliseconds(200));
        var show = menu.Items.OfType<MenuItem>().Single(item => item.Header as string == "Mostrar MiniInfos");
        show.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        menu.IsOpen = false;
        Pump(TimeSpan.FromMilliseconds(300));
        Check(cards.Main.IsVisible, "the owl's menu brings the card back");
        Find<RadioButton>(settings, "LargePet").IsChecked = true;
        Pump(TimeSpan.FromMilliseconds(200));
        Check(pet.Width == PetPlacement.Size(PetSizeChoice.Large) && Preferences.Load().PetSize == PetSizeChoice.Large,
            "the size setting updates the pet immediately and is saved");
        pet.Left = SystemParameters.WorkArea.Left + 80;
        pet.Top = SystemParameters.WorkArea.Top + 100;
        var moved = new Point(pet.Left, pet.Top);
        RenderElement(pet, System.IO.Path.Combine(pictures, "desktop-owl.png"));
        Render(settings, System.IO.Path.Combine(pictures, "settings-pet.png"));
        Find<CheckBox>(settings, "PetToggle").IsChecked = false;
        Check(cards.Pet is null && !pet.IsVisible && !Preferences.Load().PetEnabled,
            "turning it off closes the window and saves the choice");
        Find<CheckBox>(settings, "PetToggle").IsChecked = true;
        Pump(TimeSpan.FromMilliseconds(300));
        Check(cards.Pet is { IsVisible: true } restored && Math.Abs(restored.Left - moved.X) < 1 && Math.Abs(restored.Top - moved.Y) < 1,
            "the owl returns to its saved desktop position");
        preferences.IsTopmost = false;
        Check(cards.Pet?.Topmost == false, "the owl follows Sempre por cima too");
        preferences.IsTopmost = true;
        Find<CheckBox>(settings, "PetToggle").IsChecked = false;
        Find<RadioButton>(settings, "MediumPet").IsChecked = true;
    }

    private static void CheckPetInteractions(PetWindow pet, string pictures)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var interaction = (PetInteraction)typeof(PetWindow).GetField("_interaction", flags)!.GetValue(pet)!;
        var started = (long)typeof(PetWindow).GetField("_startedAt", flags)!.GetValue(pet)!;
        var sprite = Find<Image>(pet, "Sprite");
        var player = (PetAnimationPlayer)typeof(PetWindow).GetField("_player", flags)!.GetValue(pet)!;
        var paint = typeof(PetWindow).GetMethod("Paint", flags)!;
        TimeSpan Now() => Stopwatch.GetElapsedTime(started);
        void PaintAt(TimeSpan at) => paint.Invoke(pet, [at]);
        Point VisibleCenter()
        {
            var bounds = PetPixels.Bounds(player.Pixels.Span, PetFrame.CanvasSize);
            var local = new Point((bounds.Left + bounds.Width / 2) / PetFrame.CanvasSize * sprite.Width,
                (bounds.Top + bounds.Height / 2) / PetFrame.CanvasSize * sprite.Height);
            return pet.PointToScreen(sprite.RenderTransform.Transform(local));
        }
        TimeSpan Press(Point canvas)
        {
            var local = new Point(canvas.X / PetFrame.CanvasSize * sprite.Width, canvas.Y / PetFrame.CanvasSize * sprite.Height);
            var screen = pet.PointToScreen(sprite.RenderTransform.Transform(local));
            typeof(PetWindow).GetField("_grabMatrix", flags)!.SetValue(pet, sprite.RenderTransform.Value);
            var wasDropping = pet.IsDropping;
            typeof(PetWindow).GetMethod("ClearDrop", flags)!.Invoke(pet, null);
            typeof(PetWindow).GetField("_suspendedDrop", flags)!.SetValue(pet, wasDropping);
            var at = Now();
            interaction.Press(canvas, screen, at);
            return at;
        }
        void Release() => pet.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = UIElement.MouseLeftButtonUpEvent });

        pet.Left = SystemParameters.WorkArea.Left + 120;
        pet.Top = SystemParameters.WorkArea.Top + 100;
        foreach (var x in new[] { 130, 192 })
        {
            Press(new Point(x, 194));
            Release();
            Check(pet.State == PetState.ProtectEyes, $"a quick poke in the {(x == 130 ? "left" : "right")} eye raises the protective wings");
            Pump(TimeSpan.FromMilliseconds(500));
            RenderElement(pet, System.IO.Path.Combine(pictures, $"pet-poked-{x}.png"));
        }

        foreach (var (part, expected, name) in new[]
        {
            (new Point(160, 100), PetState.HeldHead, "head"),
            (new Point(90, 245), PetState.HeldLeftWing, "left-wing"),
            (new Point(230, 245), PetState.HeldRightWing, "right-wing"),
            (new Point(144, 292), PetState.HeldFeet, "feet"),
            (new Point(160, 250), PetState.HeldBody, "body"),
        })
        {
            Press(part);
            interaction.Move(interaction.PointerScreen + new Vector(30, 5), Now());
            Pump(TimeSpan.FromMilliseconds(420));
            PaintAt(Now());
            var hold = interaction.HoldAt(Now());
            Check(pet.State == expected && hold is not null, $"grabbing the {name} selects its hanging pose");
            if (hold is { } grip)
            {
                var local = new Point(grip.Anchor.X / PetFrame.CanvasSize * sprite.Width, grip.Anchor.Y / PetFrame.CanvasSize * sprite.Height);
                var anchor = pet.PointToScreen(sprite.RenderTransform.Transform(local));
                Check((anchor - interaction.PointerScreen).Length < 2, "the grabbed part stays attached to the cursor as the pose changes");
            }
            RenderElement(pet, System.IO.Path.Combine(pictures, $"pet-held-{name}.png"));
            var beforeRelease = VisibleCenter();
            Release();
            Check(!interaction.IsPressed && !interaction.IsHeld && (int)pet.State < (int)PetState.ProtectEyes,
                "releasing detaches the grip and starts the fall");
            Check(pet.IsDropping && (VisibleCenter() - beforeRelease).Length < 2,
                $"releasing from the {name} preserves its actual visible position");
            Pump(TimeSpan.FromMilliseconds(100));
            RenderElement(pet, System.IO.Path.Combine(pictures, $"pet-falling-{name}.png"));
            PumpUntil(() => !pet.IsDropping, TimeSpan.FromSeconds(2.5));
            Check(!pet.IsDropping, "the local landing and recovery finish");
        }

        Press(new Point(160, 100));
        var center = interaction.PointerScreen - new Vector(70, 0);
        interaction.Move(interaction.PointerScreen + new Vector(5, 0), Now());
        var orbit = Stopwatch.StartNew();
        double minimum = 0, maximum = 0, gripError = 0;
        var staysInside = true;
        Size? rotatingSize = null;
        var stableSize = true;
        while (orbit.Elapsed.TotalSeconds < 3)
        {
            var phase = orbit.Elapsed.TotalSeconds * Math.Tau / 0.6;
            interaction.Move(center + new Vector(70 * Math.Cos(phase), 70 * Math.Sin(phase)), Now());
            Pump(TimeSpan.FromMilliseconds(16));
            // A message pump need not include a composition frame; paint before sampling its screen pivot.
            PaintAt(Now());
            if (interaction.HoldAt(Now()) is not { } hold) continue;
            minimum = Math.Min(minimum, hold.Angle);
            maximum = Math.Max(maximum, hold.Angle);
            var anchor = new Point(hold.Anchor.X / PetFrame.CanvasSize * sprite.Width, hold.Anchor.Y / PetFrame.CanvasSize * sprite.Height);
            var attached = pet.PointToScreen(sprite.RenderTransform.Transform(anchor));
            gripError = Math.Max(gripError, (attached - interaction.PointerScreen).Length);
            var bounds = sprite.RenderTransform.TransformBounds(new Rect(0, 0, sprite.Width, sprite.Height));
            staysInside &= bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= pet.ActualWidth + 1 && bounds.Bottom <= pet.ActualHeight + 1;
            if (hold.Progress >= 1)
            {
                var size = new Size(pet.ActualWidth, pet.ActualHeight);
                rotatingSize ??= size;
                stableSize &= size == rotatingSize.Value;
            }
        }
        Check(maximum - minimum > 360, "circular mouse motion drives complete revolutions around the grip");
        Check(gripError < 2, $"the pivot remains attached throughout the spin ({gripError:0.0} px)");
        Check(staysInside, "the rotating owl is never clipped by its transparent window");
        Check(rotatingSize is not null && stableSize, "rotation keeps the render surface stable instead of resizing it each frame");
        RenderElement(pet, System.IO.Path.Combine(pictures, "pet-physics-spin.png"));
        Release();
        Pump(TimeSpan.FromMilliseconds(80));
        var caught = VisibleCenter();
        var caughtAt = Press(new Point(160, 100));
        // Sample the exact press instant instead of waiting past the hold threshold on a busy machine.
        PaintAt(caughtAt);
        var drift = (VisibleCenter() - caught).Length;
        Check(!pet.IsDropping && !interaction.IsHeld && drift < 3,
            $"catching the pet during a fall pauses it at its visible position ({drift:0.0} px{(pet.IsDropping ? ", still falling" : "")})");
        interaction.Move(interaction.PointerScreen + new Vector(15, 0), Now());
        Pump(TimeSpan.FromMilliseconds(350));
        Check(interaction.IsHeld, "a falling pet can be picked up again without waiting for its landing");
        var recaught = VisibleCenter();
        Release();
        Check(pet.IsDropping && (VisibleCenter() - recaught).Length < 2,
            "a second release starts a new fall from the new grip");
        PumpUntil(() => !pet.IsDropping, TimeSpan.FromSeconds(2.5));
        Check(Math.Abs(pet.Width - sprite.Width) < 1 && Math.Abs(pet.Height - sprite.Height) < 1
            && sprite.RenderTransform.Value.HasInverse, "dropping the owl restores its normal viewport without collapsing the sprite");
    }
}
