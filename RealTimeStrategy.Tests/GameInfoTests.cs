using RealTimeStrategy.Core;
using Xunit;

namespace RealTimeStrategy.Tests;

public sealed class GameInfoTests
{
    [Fact]
    public void Title_is_not_empty()
    {
        Assert.False(string.IsNullOrWhiteSpace(GameInfo.Title));
    }

    [Fact]
    public void Version_is_semver_like()
    {
        var parts = GameInfo.Version.Split('.');

        Assert.Equal(3, parts.Length);
        Assert.All(parts, part => Assert.True(int.TryParse(part, out var value) && value >= 0));
    }
}
