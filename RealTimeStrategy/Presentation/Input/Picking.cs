using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RealTimeStrategy.Core.World;
using RealTimeStrategy.Presentation.Camera;
using WorldState = RealTimeStrategy.Core.World.World;

namespace RealTimeStrategy.Presentation.Input;

/// <summary>
/// Screen-space ray construction and entity/ground picking. Units win over structures when both
/// are under the cursor; the closest unit hit is chosen.
/// </summary>
public static class Picking
{
    private const float MinimumDirectionLengthSquared = 1e-8f;

    public static Ray GetRay(CameraView camera, Point screenPosition, int viewportWidth, int viewportHeight)
    {
        ArgumentNullException.ThrowIfNull(camera);

        if (viewportWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportWidth), viewportWidth, "Viewport width must be positive.");
        }

        if (viewportHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportHeight), viewportHeight, "Viewport height must be positive.");
        }

        var ndcX = 2f * screenPosition.X / viewportWidth - 1f;
        var ndcY = 1f - 2f * screenPosition.Y / viewportHeight;
        var inverse = Matrix.Invert(camera.View * camera.Projection);

        var near = Unproject(new Vector3(ndcX, ndcY, 0f), inverse);
        var far = Unproject(new Vector3(ndcX, ndcY, 1f), inverse);
        var direction = far - near;

        if (direction.LengthSquared() < MinimumDirectionLengthSquared)
        {
            direction = Vector3.Forward;
        }

        return new Ray(near, Vector3.Normalize(direction));
    }

    private static Vector3 Unproject(Vector3 ndc, Matrix inverseViewProjection)
    {
        var clip = Vector4.Transform(new Vector4(ndc, 1f), inverseViewProjection);
        return new Vector3(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W);
    }

    public static bool TryGetGroundPoint(
        CameraView camera,
        Point screenPosition,
        int viewportWidth,
        int viewportHeight,
        out Vector3 point)
    {
        var ray = GetRay(camera, screenPosition, viewportWidth, viewportHeight);

        if (MathF.Abs(ray.Direction.Y) < 1e-5f)
        {
            point = default;
            return false;
        }

        var distance = -ray.Position.Y / ray.Direction.Y;
        if (distance < 0f)
        {
            point = default;
            return false;
        }

        point = ray.Position + ray.Direction * distance;
        point.Y = 0f;
        return true;
    }

    /// <summary>Closest unit hit by the ray, otherwise the closest structure.</summary>
    public static Entity? PickEntity(
        WorldState world,
        CameraView camera,
        Point screenPosition,
        int viewportWidth,
        int viewportHeight)
    {        ArgumentNullException.ThrowIfNull(world);

        var ray = GetRay(camera, screenPosition, viewportWidth, viewportHeight);
        Entity? bestUnit = null;
        Entity? bestBuilding = null;
        var bestUnitDistance = float.PositiveInfinity;
        var bestBuildingDistance = float.PositiveInfinity;

        foreach (var entity in world.Entities)
        {
            if (!entity.IsAlive)
            {
                continue;
            }

            var center = entity.Position + new Vector3(0f, entity.Radius, 0f);
            var bounds = new BoundingSphere(center, entity.Radius * 2f);
            var hit = ray.Intersects(bounds);

            if (hit is not float distance)
            {
                continue;
            }

            if (entity.Kind == EntityKind.Unit)
            {
                if (distance < bestUnitDistance)
                {
                    bestUnitDistance = distance;
                    bestUnit = entity;
                }
            }
            else if (distance < bestBuildingDistance)
            {
                bestBuildingDistance = distance;
                bestBuilding = entity;
            }
        }

        return bestUnit ?? bestBuilding;
    }

    /// <summary>Projects a world position to screen pixels; false when behind the camera.</summary>
    public static bool TryProjectToScreen(
        CameraView camera,
        Vector3 worldPosition,
        int viewportWidth,
        int viewportHeight,
        out Vector2 screenPosition)
    {
        ArgumentNullException.ThrowIfNull(camera);

        var viewport = new Viewport(0, 0, viewportWidth, viewportHeight);
        var projected = viewport.Project(
            worldPosition,
            camera.Projection,
            camera.View,
            Matrix.Identity);

        if (projected.Z < 0f || projected.Z > 1f)
        {
            screenPosition = default;
            return false;
        }

        screenPosition = new Vector2(projected.X, projected.Y);
        return true;
    }

    /// <summary>Fills the list with living units whose screen centers fall inside the rectangle.</summary>
    public static void QueryUnitsInRectangle(
        WorldState world,
        CameraView camera,
        Rectangle rectangle,
        int viewportWidth,
        int viewportHeight,
        List<Entity> results)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(results);
        results.Clear();

        foreach (var entity in world.Entities)
        {
            if (entity.Kind != EntityKind.Unit || !entity.IsAlive)
            {
                continue;
            }

            if (TryProjectToScreen(
                    camera,
                    entity.Position,
                    viewportWidth,
                    viewportHeight,
                    out var screen) &&
                rectangle.Contains(screen))
            {
                results.Add(entity);
            }
        }
    }
}
