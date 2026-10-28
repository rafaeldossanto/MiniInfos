#if AVALONIA
using Avalonia;
#else
using System.Windows;
#endif
using System.Text.Json.Serialization;

namespace MiniInfos.Mascot;

public sealed record PetPoint(double X, double Y)
{
    [JsonIgnore] public Point Point => new(X, Y);
}

public sealed record PetHitRegion(PetBodyPart Part, double X, double Y, double RadiusX, double RadiusY)
{
    public bool Contains(Point point) => Math.Pow((point.X - X) / RadiusX, 2) + Math.Pow((point.Y - Y) / RadiusY, 2) <= 1;
}

/// <summary>Character-specific grips and mass centers, separate from shared gestures and physics.</summary>
public sealed record PetAnatomy(PetHitRegion[] Regions, Dictionary<PetState, PetPoint> Anchors,
    PetPoint MassCenter, PetPoint UpsideDownMassCenter)
{
    public static PetAnatomy Athena { get; } = new(
        [new(PetBodyPart.LeftEye, 130, 194, 22, 25), new(PetBodyPart.RightEye, 192, 194, 22, 25)],
        new()
        {
            [PetState.HeldHead] = new(160, 82), [PetState.HeldLeftWing] = new(85, 38),
            [PetState.HeldRightWing] = new(235, 38), [PetState.HeldFeet] = new(160, 24), [PetState.HeldBody] = new(160, 225),
        }, new(160, 230), new(160, 90));

    public PetBodyPart PartAt(Point point)
    {
        foreach (var region in Regions) if (region.Contains(point)) return region.Part;
        if (point.Y >= 278) return PetBodyPart.Feet;
        if (point.Y < 232 && new PetHitRegion(PetBodyPart.Head, 160, 165, 85, 95).Contains(point)) return PetBodyPart.Head;
        return point.X < 122 ? PetBodyPart.LeftWing : point.X > 198 ? PetBodyPart.RightWing : PetBodyPart.Body;
    }

    public Point AnchorFor(PetState state)
    {
        if (Anchors.TryGetValue(state, out var anchor)) return anchor.Point;
        if (state == PetState.HeldRightWing && Anchors.TryGetValue(PetState.HeldLeftWing, out var left)) return new(320 - left.X, left.Y);
        return (Athena.Anchors.GetValueOrDefault(state) ?? Athena.Anchors[PetState.HeldBody]).Point;
    }
}
