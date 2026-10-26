namespace MiniInfos.UiCheck;

internal static partial class Program
{
    private static void CheckPet(CardSet cards, Preferences preferences, SettingsWindow settings, string pictures)
    {
        Check(cards.Pet is null && !preferences.PetEnabled, "the desktop pet is optional and stays unloaded while disabled");
        settings.ShowPetPage();
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
        Render(pet, System.IO.Path.Combine(pictures, "desktop-owl.png"));
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
        TimeSpan Now() => Stopwatch.GetElapsedTime(started);
        void Press(Point canvas)
        {
            var local = new Point(canvas.X / PetFrame.CanvasSize * pet.Width, canvas.Y / PetFrame.CanvasSize * pet.Height);
            var screen = pet.PointToScreen(sprite.RenderTransform.Transform(local));
            typeof(PetWindow).GetField("_grabMatrix", flags)!.SetValue(pet, sprite.RenderTransform.Value);
            typeof(PetWindow).GetField("_releasedAt", flags)!.SetValue(pet, null);
            interaction.Press(canvas, screen, Now());
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
            Render(pet, System.IO.Path.Combine(pictures, $"pet-poked-{x}.png"));
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
            var hold = interaction.HoldAt(Now());
            Check(pet.State == expected && hold is not null, $"grabbing the {name} selects its hanging pose");
            if (hold is { } grip)
            {
                var local = new Point(grip.Anchor.X / PetFrame.CanvasSize * pet.Width, grip.Anchor.Y / PetFrame.CanvasSize * pet.Height);
                var anchor = pet.PointToScreen(sprite.RenderTransform.Transform(local));
                Check((anchor - interaction.PointerScreen).Length < 2, "the grabbed part stays attached to the cursor as the pose changes");
            }
            Render(pet, System.IO.Path.Combine(pictures, $"pet-held-{name}.png"));
            Release();
            Check(!interaction.IsPressed && !interaction.IsHeld && (int)pet.State < (int)PetState.ProtectEyes,
                "releasing returns to the observed usage state");
            Pump(TimeSpan.FromMilliseconds(350));
        }
    }
}
