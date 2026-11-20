using DigitalRiseModel;
using Microsoft.Xna.Framework;

namespace RealTimeStrategy.Presentation.Rendering;

/// <summary>
/// Short-lived ground rings marking issued move/attack destinations. Purely presentation: update
/// ages, drop expired markers, and submit expanding/fading rings to the transparent pass.
/// </summary>
public sealed class OrderMarkerSystem
{
    public const float LifetimeSeconds = 0.65f;
    private const float StartRadius = 0.7f;
    private const float Expansion = 1.3f;

    private readonly List<Marker> _markers = new();

    public int Count => _markers.Count;

    public void Add(Vector3 position, Color color)
    {
        _markers.Add(new Marker(position, color));
    }

    public void Update(float deltaSeconds)
    {
        for (var i = _markers.Count - 1; i >= 0; i--)
        {
            var marker = _markers[i];
            marker.Age += deltaSeconds;
            if (marker.Age >= LifetimeSeconds)
            {
                _markers.RemoveAt(i);
            }
            else
            {
                _markers[i] = marker;
            }
        }
    }

    public void Draw(SceneRenderer renderer, DrMesh ringMesh)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(ringMesh);

        foreach (var marker in _markers)
        {
            var t = Math.Clamp(marker.Age / LifetimeSeconds, 0f, 1f);
            var radius = StartRadius + Expansion * t;
            var alpha = (1f - t) * 0.8f;
            var world =
                Matrix.CreateScale(radius, 1f, radius) *
                Matrix.CreateTranslation(marker.Position.X, 0.06f, marker.Position.Z);

            renderer.SubmitTransparent(ringMesh, world, marker.Color, alpha: alpha);
        }
    }

    private struct Marker
    {
        public Marker(Vector3 position, Color color)
        {
            Position = position;
            Color = color;
            Age = 0f;
        }

        public Vector3 Position { get; }
        public Color Color { get; }
        public float Age { get; set; }
    }
}
