using RealTimeStrategy.Presentation.Rendering;
using Xunit;

namespace RealTimeStrategy.Tests.Rendering;

public sealed class FactionPalettesTests
{
    [Fact]
    public void Palettes_have_distinct_accents()
    {
        var accents = new[]
        {
            FactionPalettes.Concord.Accent,
            FactionPalettes.Null.Accent,
            FactionPalettes.Neutral.Accent,
        };

        Assert.Equal(accents.Length, accents.Distinct().Count());
    }

    [Fact]
    public void Get_returns_the_matching_palette()
    {
        Assert.Same(FactionPalettes.Concord, FactionPalettes.Get(FactionPaletteId.Concord));
        Assert.Same(FactionPalettes.Null, FactionPalettes.Get(FactionPaletteId.Null));
        Assert.Same(FactionPalettes.Neutral, FactionPalettes.Get(FactionPaletteId.Neutral));
    }

    [Fact]
    public void Get_unknown_palette_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FactionPalettes.Get((FactionPaletteId)999));
    }
}
