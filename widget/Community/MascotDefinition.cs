using MiniInfos.Mascot;

namespace MiniInfos.Community;

/// <summary>A PNG grid with independently timed tracks; all coordinates of the anatomy use the 320-unit canvas.</summary>
public sealed record MascotDefinition(
    string SpriteSheet,
    int Columns,
    int Rows,
    Dictionary<PetState, MascotTrack> States,
    PetAnatomy? Anatomy = null);

public sealed record MascotTrack(int Row, int FrameDurationMs = 160, int[]? Frames = null,
    int[]? DurationsMs = null, int BlendMs = 120);
