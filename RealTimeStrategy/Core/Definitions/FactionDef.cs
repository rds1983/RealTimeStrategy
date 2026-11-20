namespace RealTimeStrategy.Core.Definitions;

/// <summary>Playable faction identity. Visual palettes live in the presentation layer.</summary>
public sealed record FactionDef
{
    public required FactionId Id { get; init; }

    public required string Name { get; init; }

    public string Description { get; init; } = string.Empty;
}
