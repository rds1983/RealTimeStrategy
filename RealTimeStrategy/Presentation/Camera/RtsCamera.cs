using Microsoft.Xna.Framework;

namespace RealTimeStrategy.Presentation.Camera;

/// <summary>
/// RTS camera controller. Keeps a ground focus point, fixed 55° pitch, yaw and zoom distance,
/// clamps focus to the map and feeds the shared <see cref="CameraView"/>.
/// </summary>
public sealed class RtsCamera
{
    public const float MinDistance = 20f;
    public const float MaxDistance = 80f;
    public const float DefaultDistance = 45f;
    public const float ZoomStep = 6f;
    public const float PanSpeed = 45f;
    public const float DefaultYaw = MathF.PI / 4f;
    public const float Pitch = 55f * MathF.PI / 180f;

    private const float ZoomSmoothing = 12f;
    private const float YawSmoothing = 14f;
    private const float SnapThreshold = 0.01f;

    private readonly Vector2 _minBounds;
    private readonly Vector2 _maxBounds;
    private float _targetDistance;
    private float _yaw;
    private float _targetYaw;

    public RtsCamera(CameraView view, Vector2 minBounds, Vector2 maxBounds, Vector3? focus = null)
    {
        View = view ?? throw new ArgumentNullException(nameof(view));

        if (minBounds.X >= maxBounds.X || minBounds.Y >= maxBounds.Y)
        {
            throw new ArgumentException("Map bounds must describe a positive area.", nameof(minBounds));
        }

        _minBounds = minBounds;
        _maxBounds = maxBounds;

        Focus = ClampFocus(focus ?? Vector3.Zero);
        _yaw = DefaultYaw;
        _targetYaw = DefaultYaw;
        Distance = DefaultDistance;
        _targetDistance = DefaultDistance;

        SyncView();
    }

    public CameraView View { get; }

    public Vector3 Focus { get; private set; }

    public Vector3 Position { get; private set; }

    public float Yaw => MathHelper.WrapAngle(_yaw);

    public float TargetYaw => MathHelper.WrapAngle(_targetYaw);

    public float Distance { get; private set; }

    public float TargetDistance => _targetDistance;

    /// <summary>World units covered by one vertical screen pixel at the focus plane.</summary>
    public float WorldUnitsPerPixel(int viewportHeight)
    {
        if (viewportHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportHeight), viewportHeight, "Viewport height must be positive.");
        }

        return 2f * Distance * MathF.Tan(View.FieldOfView * 0.5f) / viewportHeight;
    }

    /// <summary>Moves focus relative to the camera. Axis Y is forward, X is screen-right.</summary>
    public void Pan(Vector2 axis, float deltaSeconds)
    {
        if (deltaSeconds <= 0 || axis == Vector2.Zero)
        {
            return;
        }

        if (axis.LengthSquared() > 1f)
        {
            axis.Normalize();
        }

        var speed = PanSpeed * (Distance / DefaultDistance);
        PanBy(axis * speed * deltaSeconds);
    }

    /// <summary>Moves focus by a world-space delta in camera-relative axes.</summary>
    public void PanBy(Vector2 delta)
    {
        if (delta == Vector2.Zero)
        {
            return;
        }

        var forward = new Vector2(-MathF.Sin(Yaw), -MathF.Cos(Yaw));
        var right = new Vector2(MathF.Cos(Yaw), -MathF.Sin(Yaw));
        var offset = right * delta.X + forward * delta.Y;

        Focus = ClampFocus(Focus + new Vector3(offset.X, 0f, offset.Y));
        SyncView();
    }

    /// <summary>Positive notches zoom in; distance changes smoothly in <see cref="Update"/>.</summary>
    public void Zoom(float notches)
    {
        if (notches == 0)
        {
            return;
        }

        _targetDistance = Math.Clamp(
            _targetDistance - notches * ZoomStep,
            MinDistance,
            MaxDistance);
    }

    /// <summary>Requests a smooth rotation by 45° steps.</summary>
    public void Rotate(int steps)
    {
        if (steps == 0)
        {
            return;
        }

        _targetYaw += steps * MathF.PI / 4f;
    }

    public void FocusOn(Vector3 point)
    {
        Focus = ClampFocus(point);
        SyncView();
    }

    public void Update(float deltaSeconds)
    {
        if (deltaSeconds <= 0)
        {
            return;
        }

        var zoomT = 1f - MathF.Exp(-ZoomSmoothing * deltaSeconds);
        Distance = MathHelper.Lerp(Distance, _targetDistance, zoomT);

        if (MathF.Abs(Distance - _targetDistance) <= SnapThreshold)
        {
            Distance = _targetDistance;
        }

        var yawT = 1f - MathF.Exp(-YawSmoothing * deltaSeconds);
        var yawDelta = _targetYaw - _yaw;
        _yaw += yawDelta * yawT;

        if (MathF.Abs(_targetYaw - _yaw) <= SnapThreshold)
        {
            _yaw = MathHelper.WrapAngle(_targetYaw);
            _targetYaw = _yaw;
        }

        SyncView();
    }

    private void SyncView()
    {
        var horizontal = Distance * MathF.Cos(Pitch);
        var offset = new Vector3(
            horizontal * MathF.Sin(Yaw),
            Distance * MathF.Sin(Pitch),
            horizontal * MathF.Cos(Yaw));

        Position = Focus + offset;
        View.LookAt(Position, Focus);
    }

    private Vector3 ClampFocus(Vector3 point)
    {
        return new Vector3(
            Math.Clamp(point.X, _minBounds.X, _maxBounds.X),
            0f,
            Math.Clamp(point.Z, _minBounds.Y, _maxBounds.Y));
    }
}
