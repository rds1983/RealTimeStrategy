using Microsoft.Xna.Framework;

namespace RealTimeStrategy.Presentation.Rendering;

/// <summary>Chassis, panel and emissive accent colors for one faction.</summary>
public sealed record FactionPalette(string Name, Color Chassis, Color Panel, Color Accent);
