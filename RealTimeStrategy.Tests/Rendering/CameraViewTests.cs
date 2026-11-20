using Microsoft.Xna.Framework;
using RealTimeStrategy.Presentation.Camera;
using Xunit;

namespace RealTimeStrategy.Tests.Rendering;

public sealed class CameraViewTests
{
    private static CameraView CreateCamera(float nearPlane = 0.5f, float farPlane = 100f)
    {
        var camera = new CameraView(MathHelper.PiOver4, nearPlane, farPlane);
        camera.SetViewport(1600, 900);
        camera.LookAt(new Vector3(0f, 0f, 10f), Vector3.Zero);
        return camera;
    }

    [Fact]
    public void Origin_is_visible()
    {
        var camera = CreateCamera();

        Assert.True(camera.IsVisible(new BoundingSphere(Vector3.Zero, 1f)));
    }

    [Fact]
    public void Sphere_behind_camera_is_culled()
    {
        var camera = CreateCamera();

        Assert.False(camera.IsVisible(new BoundingSphere(new Vector3(0f, 0f, 20f), 1f)));
    }

    [Fact]
    public void Sphere_outside_the_field_of_view_is_culled()
    {
        var camera = CreateCamera();

        Assert.False(camera.IsVisible(new BoundingSphere(new Vector3(100f, 0f, 0f), 1f)));
    }

    [Fact]
    public void Sphere_beyond_the_far_plane_is_culled()
    {
        var camera = CreateCamera(farPlane: 100f);

        Assert.False(camera.IsVisible(new BoundingSphere(new Vector3(0f, 0f, -200f), 1f)));
    }

    [Fact]
    public void Sphere_intersecting_the_near_plane_is_visible()
    {
        var camera = CreateCamera(nearPlane: 0.5f);

        Assert.True(camera.IsVisible(new BoundingSphere(new Vector3(0f, 0f, 9.4f), 0.5f)));
    }

    [Fact]
    public void Mesh_bounds_follow_the_world_matrix()
    {
        var camera = CreateCamera();
        var localBounds = new BoundingBox(new Vector3(-1f), new Vector3(1f));

        Assert.True(camera.IsVisible(localBounds, Matrix.Identity));
        Assert.False(camera.IsVisible(localBounds, Matrix.CreateTranslation(0f, 0f, 20f)));
        Assert.False(camera.IsVisible(localBounds, Matrix.CreateTranslation(0f, 0f, -200f)));
    }

    [Fact]
    public void Look_at_updates_visibility()
    {
        var camera = CreateCamera();

        camera.LookAt(new Vector3(0f, 0f, 10f), new Vector3(100f, 0f, 0f));

        Assert.False(camera.IsVisible(new BoundingSphere(Vector3.Zero, 1f)));
        Assert.True(camera.IsVisible(new BoundingSphere(new Vector3(100f, 0f, 0f), 1f)));
    }

    [Fact]
    public void Viewport_updates_aspect_ratio_and_projection()
    {
        var camera = CreateCamera();

        camera.SetViewport(800, 800);

        Assert.Equal(1f, camera.AspectRatio, 4);
        Assert.Equal(
            Matrix.CreatePerspectiveFieldOfView(camera.FieldOfView, 1f, camera.NearPlane, camera.FarPlane),
            camera.Projection);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(MathHelper.Pi)]
    public void Invalid_field_of_view_throws(float fieldOfView)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CameraView(fieldOfView));
    }

    [Theory]
    [InlineData(0f, 100f)]
    [InlineData(-1f, 100f)]
    [InlineData(10f, 5f)]
    public void Invalid_planes_throw(float nearPlane, float farPlane)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CameraView(MathHelper.PiOver4, nearPlane, farPlane));
    }

    [Theory]
    [InlineData(0, 900)]
    [InlineData(1600, 0)]
    [InlineData(-1, -1)]
    public void Invalid_viewport_throws(int width, int height)
    {
        var camera = CreateCamera();

        Assert.Throws<ArgumentOutOfRangeException>(() => camera.SetViewport(width, height));
    }
}
