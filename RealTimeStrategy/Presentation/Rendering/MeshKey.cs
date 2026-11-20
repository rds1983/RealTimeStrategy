using DigitalRiseModel.Primitives;

namespace RealTimeStrategy.Presentation.Rendering;

/// <summary>Identifies one procedural primitive shape.</summary>
public enum MeshPrimitiveKind
{
    Box,
    Sphere,
    Cylinder,
    Cone,
    Capsule,
    Torus,
    Disc,
    Plane,
}

/// <summary>
/// Value key for the mesh cache. Fields that a primitive does not use stay at their defaults,
/// so two requests with identical parameters always produce equal keys.
/// </summary>
public readonly record struct MeshKey
{
    public MeshKey()
    {
    }

    public MeshPrimitiveKind Kind { get; init; }

    public float SizeX { get; init; }

    public float SizeY { get; init; }

    public float SizeZ { get; init; }

    public float Radius { get; init; }

    public float Height { get; init; }

    /// <summary>Cylinder body length, excluding the hemispherical caps.</summary>
    public float Length { get; init; }

    public float MajorRadius { get; init; }

    public float MinorRadius { get; init; }

    public float SectorAngle { get; init; } = MathF.PI * 2f;

    public int Tessellation { get; init; }

    public int TessellationX { get; init; } = 1;

    public int TessellationY { get; init; } = 1;

    public float UScale { get; init; } = 1f;

    public float VScale { get; init; } = 1f;

    public bool Capped { get; init; } = true;

    public bool GenerateBackFace { get; init; }

    public bool ToLeftHanded { get; init; }

    public NormalDirection NormalDirection { get; init; }
}
