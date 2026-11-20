using System.Globalization;

namespace RealTimeStrategy.Core.World;

/// <summary>Stable entity identifier. Zero is reserved for "none".</summary>
public readonly record struct EntityId(int Value)
{
    public static EntityId None => new(0);

    public bool IsNone => Value == 0;

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
