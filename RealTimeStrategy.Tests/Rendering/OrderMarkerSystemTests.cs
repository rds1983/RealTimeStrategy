using Microsoft.Xna.Framework;
using RealTimeStrategy.Presentation.Rendering;
using Xunit;

namespace RealTimeStrategy.Tests.Rendering;

public sealed class OrderMarkerSystemTests
{
    [Fact]
    public void Added_marker_survives_until_its_lifetime_expires()
    {
        var markers = new OrderMarkerSystem();
        markers.Add(new Vector3(3f, 0f, -2f), Color.Cyan);

        Assert.Equal(1, markers.Count);

        markers.Update(OrderMarkerSystem.LifetimeSeconds * 0.5f);
        Assert.Equal(1, markers.Count);

        markers.Update(OrderMarkerSystem.LifetimeSeconds * 0.4f);
        Assert.Equal(1, markers.Count);

        markers.Update(OrderMarkerSystem.LifetimeSeconds * 0.2f);
        Assert.Equal(0, markers.Count);
    }

    [Fact]
    public void Markers_expire_individually()
    {
        var markers = new OrderMarkerSystem();
        markers.Add(Vector3.Zero, Color.Cyan);
        markers.Update(OrderMarkerSystem.LifetimeSeconds * 0.75f);
        markers.Add(Vector3.One, Color.Red);

        markers.Update(OrderMarkerSystem.LifetimeSeconds * 0.3f);
        Assert.Equal(1, markers.Count);

        markers.Update(OrderMarkerSystem.LifetimeSeconds * 0.75f);
        Assert.Equal(0, markers.Count);
    }

    [Fact]
    public void Zero_delta_keeps_every_marker()
    {
        var markers = new OrderMarkerSystem();
        markers.Add(Vector3.Zero, Color.Cyan);
        markers.Add(Vector3.One, Color.Red);

        markers.Update(0f);

        Assert.Equal(2, markers.Count);
    }

    [Fact]
    public void Many_markers_expire_without_leaving_residue()
    {
        var markers = new OrderMarkerSystem();

        for (var i = 0; i < 32; i++)
        {
            markers.Add(new Vector3(i, 0f, i), Color.Cyan);
        }

        Assert.Equal(32, markers.Count);

        markers.Update(OrderMarkerSystem.LifetimeSeconds);

        Assert.Equal(0, markers.Count);
    }
}
