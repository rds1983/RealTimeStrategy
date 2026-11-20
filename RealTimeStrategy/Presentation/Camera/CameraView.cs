using Microsoft.Xna.Framework;

namespace RealTimeStrategy.Presentation.Camera;

/// <summary>
/// Holds the view/projection matrices and a frustum for culling. Pure math, no graphics device,
/// so RTS camera behavior and culling can be unit tested.
/// </summary>
public sealed class CameraView
{
    private readonly BoundingFrustum _frustum = new(Matrix.Identity);

    private float _aspectRatio = 16f / 9f;

    public CameraView(float fieldOfView = MathHelper.PiOver4, float nearPlane = 0.5f, float farPlane = 500f)
    {
        if (fieldOfView <= 0 || fieldOfView >= MathHelper.Pi)
        {
            throw new ArgumentOutOfRangeException(nameof(fieldOfView), fieldOfView, "Field of view must be between 0 and Pi.");
        }

        if (nearPlane <= 0 || farPlane <= nearPlane)
        {
            throw new ArgumentOutOfRangeException(nameof(nearPlane), nearPlane, "Near plane must be positive and smaller than the far plane.");
        }

        FieldOfView = fieldOfView;
        NearPlane = nearPlane;
        FarPlane = farPlane;

        RebuildProjection();
        RebuildFrustum();
    }

    public Matrix View { get; private set; } = Matrix.Identity;

    public Matrix Projection { get; private set; } = Matrix.Identity;

    public Vector3 Position { get; private set; }

    public float FieldOfView { get; }

    public float NearPlane { get; }

    public float FarPlane { get; }

    public float AspectRatio => _aspectRatio;

    public void SetViewport(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Viewport width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Viewport height must be positive.");
        }

        _aspectRatio = (float)width / height;
        RebuildProjection();
        RebuildFrustum();
    }

    public void LookAt(Vector3 position, Vector3 target)
    {
        View = Matrix.CreateLookAt(position, target, Vector3.Up);
        Position = position;
        RebuildFrustum();
    }

    public bool IsVisible(BoundingSphere bounds) => _frustum.Intersects(bounds);

    /// <summary>
    /// Tests local-space bounds transformed into world space, as used by the scene renderer.
    /// </summary>
    public bool IsVisible(BoundingBox localBounds, Matrix world)
    {
        var worldBounds = BoundingSphere.CreateFromBoundingBox(localBounds).Transform(world);
        return _frustum.Intersects(worldBounds);
    }

    private void RebuildProjection()
    {
        Projection = Matrix.CreatePerspectiveFieldOfView(FieldOfView, _aspectRatio, NearPlane, FarPlane);
    }

    private void RebuildFrustum()
    {
        _frustum.Matrix = View * Projection;
    }
}
