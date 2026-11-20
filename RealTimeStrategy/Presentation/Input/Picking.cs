using Microsoft.Xna.Framework;
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

        var near = Vector3.Transform(new Vector3(ndcX, ndcY, 0f), inverse);
        var far = Vector3.Transform(new Vector3(ndcX, ndcY, 1f), inverse);
        var direction = far - near;

        if (direction.LengthSquared() < MinimumDirectionLengthSquared)
        {
            direction = Vector3.Forward;
        }

        return new Ray(near, Vector3.Normalize(direction));
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
    {
        ArgumentNullException.ThrowIfNull(world);

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
}
