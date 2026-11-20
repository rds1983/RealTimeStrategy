using Microsoft.Xna.Framework;

namespace RealTimeStrategy.Presentation.Rendering;

/// <summary>The three prototype palettes from the game design document.</summary>
public static class FactionPalettes
{
    /// <summary>Player faction: light alloy chassis with cyan energy accents.</summary>
    public static FactionPalette Concord { get; } = new(
        "Concord",
        new Color(200, 214, 228),
        new Color(92, 112, 132),
        new Color(95, 227, 255));

    /// <summary>AI faction: charcoal chassis with magenta accents.</summary>
    public static FactionPalette Null { get; } = new(
        "Null",
        new Color(62, 62, 72),
        new Color(34, 34, 42),
        new Color(255, 79, 216));

    /// <summary>Environment props: stone/grey chassis with a teal crystal glow.</summary>
    public static FactionPalette Neutral { get; } = new(
        "Neutral",
        new Color(110, 118, 130),
        new Color(58, 66, 78),
        new Color(120, 255, 220));

    public static FactionPalette Get(FactionPaletteId id) => id switch
    {
        FactionPaletteId.Concord => Concord,
        FactionPaletteId.Null => Null,
        FactionPaletteId.Neutral => Neutral,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown faction palette."),
    };
}
