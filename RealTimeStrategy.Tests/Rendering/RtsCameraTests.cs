using Microsoft.Xna.Framework;
using RealTimeStrategy.Presentation.Camera;
using Xunit;

namespace RealTimeStrategy.Tests.Rendering;

public sealed class RtsCameraTests
{
    private static RtsCamera CreateCamera(Vector3? focus = null)
    {
        var view = new CameraView();
        view.SetViewport(1600, 900);
        return new RtsCamera(view, new Vector2(-60f, -60f), new Vector2(60f, 60f), focus);
    }

    [Fact]
    public void Defaults_match_the_design()
    {
        var camera = CreateCamera();

        Assert.Equal(45f, camera.Distance, 4);
        Assert.Equal(MathHelper.PiOver4, camera.Yaw, 4);
        Assert.Equal(0f, camera.Focus.X);
        Assert.Equal(0f, camera.Focus.Z);
    }

    [Fact]
    public void Position_sits_at_the_configured_distance_and_pitch()
    {
        var camera = CreateCamera();

        var offset = camera.Position - camera.Focus;
        var horizontal = new Vector2(offset.X, offset.Z).Length();
        var pitch = MathF.Atan2(offset.Y, horizontal);

        Assert.Equal(camera.Distance, offset.Length(), 3);
        Assert.Equal(RtsCamera.Pitch, pitch, 3);
    }

    [Fact]
    public void Pan_right_moves_focus_along_positive_x_at_zero_yaw()
    {
        var camera = CreateCamera();
        camera.Rotate(-1); // default 45° -> 0°
        camera.Update(2f);

        camera.Pan(new Vector2(1f, 0f), 1f);

        Assert.True(camera.Focus.X > 1f);
        Assert.Equal(0f, camera.Focus.Z, 3);
    }

    [Fact]
    public void Pan_forward_moves_focus_toward_negative_z_at_zero_yaw()
    {
        var camera = CreateCamera();
        camera.Rotate(-1);
        camera.Update(2f);

        camera.Pan(new Vector2(0f, 1f), 1f);

        Assert.True(camera.Focus.Z < -1f);
        Assert.Equal(0f, camera.Focus.X, 3);
    }

    [Fact]
    public void Pan_speed_scales_with_zoom()
    {
        var near = CreateCamera();
        near.Rotate(-1);
        near.Update(2f);
        near.Pan(new Vector2(1f, 0f), 0.25f);

        var far = CreateCamera();
        far.Rotate(-1);
        far.Update(2f);
        far.Zoom(-100f); // clamp to max distance
        far.Update(10f);
        far.Pan(new Vector2(1f, 0f), 0.25f);

        Assert.True(far.Focus.X > near.Focus.X);
    }

    [Fact]
    public void Pan_is_clamped_to_map_bounds()
    {
        var camera = CreateCamera();
        camera.Rotate(-1);
        camera.Update(2f);

        for (var i = 0; i < 10; i++)
        {
            camera.Pan(new Vector2(1f, 0f), 10f);
        }

        Assert.Equal(60f, camera.Focus.X, 3);

        for (var i = 0; i < 10; i++)
        {
            camera.Pan(new Vector2(0f, 1f), 10f);
        }

        Assert.Equal(-60f, camera.Focus.Z, 3);
    }

    [Fact]
    public void Focus_on_clamps_and_flattens_to_the_ground()
    {
        var camera = CreateCamera();

        camera.FocusOn(new Vector3(500f, 30f, -500f));

        Assert.Equal(60f, camera.Focus.X, 3);
        Assert.Equal(-60f, camera.Focus.Z, 3);
        Assert.Equal(0f, camera.Focus.Y);
    }

    [Fact]
    public void Rotate_requests_forty_five_degree_steps_and_wraps()
    {
        var camera = CreateCamera();

        camera.Rotate(1);
        camera.Update(2f);
        Assert.Equal(90f, MathHelper.ToDegrees(camera.Yaw), 2);

        camera.Rotate(7);
        camera.Update(2f);
        Assert.Equal(MathHelper.PiOver4, camera.Yaw, 2);
    }

    [Fact]
    public void Rotation_is_smoothed_towards_the_target()
    {
        var camera = CreateCamera();

        camera.Rotate(1);

        Assert.Equal(45f, MathHelper.ToDegrees(camera.Yaw), 2);
        Assert.Equal(90f, MathHelper.ToDegrees(camera.TargetYaw), 2);

        camera.Update(0.05f);
        Assert.InRange(MathHelper.ToDegrees(camera.Yaw), 45.1f, 89.9f);

        camera.Update(2f);
        Assert.Equal(90f, MathHelper.ToDegrees(camera.Yaw), 2);
    }

    [Fact]
    public void Zoom_clamps_between_minimum_and_maximum()
    {
        var camera = CreateCamera();

        camera.Zoom(100f);
        camera.Update(10f);
        Assert.Equal(RtsCamera.MinDistance, camera.Distance, 3);

        camera.Zoom(-100f);
        camera.Update(10f);
        Assert.Equal(RtsCamera.MaxDistance, camera.Distance, 3);
    }

    [Fact]
    public void Zoom_is_smoothed_towards_the_target()
    {
        var camera = CreateCamera();

        camera.Zoom(1f);
        Assert.Equal(39f, camera.TargetDistance, 3);

        camera.Update(0.02f);
        Assert.InRange(camera.Distance, 39f, 45f);

        camera.Update(2f);
        Assert.Equal(39f, camera.Distance, 3);
    }

    [Fact]
    public void World_units_per_pixel_grows_when_zooming_out()
    {
        var camera = CreateCamera();
        var near = camera.WorldUnitsPerPixel(900);

        camera.Zoom(-100f);
        camera.Update(10f);

        Assert.True(camera.WorldUnitsPerPixel(900) > near);
    }

    [Fact]
    public void Rotation_keeps_view_position_in_sync()
    {
        var camera = CreateCamera();

        camera.Rotate(1);
        camera.Update(2f);

        Assert.Equal(camera.Position, camera.View.Position);
    }

    [Fact]
    public void Invalid_bounds_throw()
    {
        var view = new CameraView();

        Assert.Throws<ArgumentException>(() => new RtsCamera(view, new Vector2(1f, 0f), new Vector2(0f, 1f)));
    }

    [Fact]
    public void Invalid_viewport_height_throws()
    {
        var camera = CreateCamera();

        Assert.Throws<ArgumentOutOfRangeException>(() => camera.WorldUnitsPerPixel(0));
    }
}
