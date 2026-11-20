using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Definitions;
using RealTimeStrategy.Core.World;
using RealTimeStrategy.Presentation.Camera;
using RealTimeStrategy.Presentation.Input;
using Xunit;

namespace RealTimeStrategy.Tests.Input;

public sealed class PickingTests
{
    private const int Width = 800;
    private const int Height = 600;

    private static CameraView CreateCamera(Vector3 position, Vector3 target)
    {
        var camera = new CameraView(MathHelper.PiOver4, 0.5f, 300f);
        camera.SetViewport(Width, Height);
        camera.LookAt(position, target);
        return camera;
    }

    [Fact]
    public void Screen_center_ray_hits_the_ground_focus()
    {
        var camera = CreateCamera(new Vector3(0f, 20f, 20f), Vector3.Zero);

        Assert.True(Picking.TryGetGroundPoint(
            camera,
            new Point(Width / 2, Height / 2),
            Width,
            Height,
            out var point));

        Assert.True(Vector3.Distance(point, Vector3.Zero) < 0.01f);
    }

    [Fact]
    public void Center_ray_picks_a_unit_at_the_focus()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, Vector3.Zero);
        var camera = CreateCamera(new Vector3(0f, 0f, 20f), Vector3.Zero);

        var picked = Picking.PickEntity(world, camera, new Point(Width / 2, Height / 2), Width, Height);

        Assert.Same(unit, picked);
    }

    [Fact]
    public void Ray_misses_entities_outside_the_cursor()
    {
        var world = CreateWorld();
        world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(50f, 0f, 0f));
        var camera = CreateCamera(new Vector3(0f, 0f, 20f), Vector3.Zero);

        var picked = Picking.PickEntity(world, camera, new Point(Width / 2, Height / 2), Width, Height);

        Assert.Null(picked);
    }

    [Fact]
    public void Units_take_priority_over_structures()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, Vector3.Zero);
        world.SpawnBuilding(PlayerId.Player, BuildingId.Nexus, new Vector3(0f, 0f, -5f));
        var camera = CreateCamera(new Vector3(0f, 0f, 20f), new Vector3(0f, 0f, -2f));

        var picked = Picking.PickEntity(world, camera, new Point(Width / 2, Height / 2), Width, Height);

        Assert.Same(unit, picked);
    }

    [Fact]
    public void Closest_unit_wins()
    {
        var world = CreateWorld();
        var near = world.SpawnUnit(PlayerId.Player, UnitId.Striker, Vector3.Zero);
        world.SpawnUnit(PlayerId.Enemy, UnitId.Striker, new Vector3(0f, 0f, -6f));
        var camera = CreateCamera(new Vector3(0f, 0f, 20f), Vector3.Zero);

        var picked = Picking.PickEntity(world, camera, new Point(Width / 2, Height / 2), Width, Height);

        Assert.Same(near, picked);
    }

    [Theory]
    [InlineData(20f)]
    [InlineData(45f)]
    [InlineData(80f)]
    public void Picking_works_at_all_zoom_levels(float distance)
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, Vector3.Zero);
        var camera = CreateCamera(new Vector3(0f, distance * 0.6f, distance), Vector3.Zero);

        var picked = Picking.PickEntity(world, camera, new Point(Width / 2, Height / 2), Width, Height);

        Assert.Same(unit, picked);
    }

    [Fact]
    public void Dead_entities_are_not_pickable()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, Vector3.Zero);
        unit.Health = 0f;
        var camera = CreateCamera(new Vector3(0f, 0f, 20f), Vector3.Zero);

        Assert.Null(Picking.PickEntity(world, camera, new Point(Width / 2, Height / 2), Width, Height));
    }

    [Fact]
    public void Rts_camera_picks_a_projected_unit_at_max_zoom()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, Vector3.Zero);
        var camera = new CameraView(MathHelper.PiOver4, 0.5f, 500f);
        camera.SetViewport(1600, 900);
        var rtsCamera = new RtsCamera(
            camera,
            new Vector2(-60f, -60f),
            new Vector2(60f, 60f));
        rtsCamera.Zoom(-6f);
        rtsCamera.Update(10f);

        Assert.True(Picking.TryProjectToScreen(
            camera,
            unit.Position + new Vector3(0f, 1f, 0f),
            1600,
            900,
            out var screen));

        var picked = Picking.PickEntity(
            world,
            camera,
            new Point((int)screen.X, (int)screen.Y),
            1600,
            900);

        Assert.Same(unit, picked);
    }

    [Fact]
    public void Off_center_ground_point_round_trips_through_projection()
    {
        var camera = CreateCamera(new Vector3(0f, 20f, 20f), Vector3.Zero);
        var worldPoint = new Vector3(10f, 0f, -5f);

        Assert.True(Picking.TryProjectToScreen(
            camera,
            worldPoint,
            Width,
            Height,
            out var screen));
        Assert.True(Picking.TryGetGroundPoint(
            camera,
            new Point((int)MathF.Round(screen.X), (int)MathF.Round(screen.Y)),
            Width,
            Height,
            out var ground));

        Assert.True(Vector3.Distance(ground, worldPoint) < 0.1f);
    }

    [Fact]
    public void Off_center_entity_is_pickable_at_its_projected_position()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-8f, 0f, -4f));
        var camera = CreateCamera(new Vector3(0f, 20f, 20f), Vector3.Zero);

        Assert.True(Picking.TryProjectToScreen(
            camera,
            unit.Position + new Vector3(0f, unit.Radius, 0f),
            Width,
            Height,
            out var screen));

        var picked = Picking.PickEntity(
            world,
            camera,
            new Point((int)MathF.Round(screen.X), (int)MathF.Round(screen.Y)),
            Width,
            Height);

        Assert.Same(unit, picked);
    }

    [Fact]
    public void Empty_world_returns_null()
    {
        var world = CreateWorld();
        var camera = CreateCamera(new Vector3(0f, 0f, 20f), Vector3.Zero);

        Assert.Null(Picking.PickEntity(world, camera, new Point(Width / 2, Height / 2), Width, Height));
    }

    [Theory]
    [InlineData(0, 600)]
    [InlineData(800, 0)]
    public void Invalid_viewport_throws(int width, int height)
    {
        var camera = CreateCamera(new Vector3(0f, 0f, 20f), Vector3.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Picking.GetRay(camera, new Point(1, 1), width, height));
    }

    private static Core.World.World CreateWorld()
    {
        return new Core.World.World(DefinitionRegistry.CreateDefault(), seed: 1);
    }
}
