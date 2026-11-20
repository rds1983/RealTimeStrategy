namespace RealTimeStrategy.Core.Definitions;

/// <summary>Structure footprint on the build grid, in 2 × 2 unit cells.</summary>
public readonly record struct Footprint(int Width, int Height)
{
    public int AreaCells => Width * Height;
}
