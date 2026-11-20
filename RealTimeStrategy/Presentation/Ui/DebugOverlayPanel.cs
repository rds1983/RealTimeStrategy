using System.Globalization;
using Microsoft.Xna.Framework;
using RealTimeStrategy.Core;
using RealTimeStrategy.Core.Simulation;
using RealTimeStrategy.Core.World;

namespace RealTimeStrategy.Presentation.Ui;

/// <summary>
/// Debug overlay panel. The widget tree comes from <c>DebugOverlay.xmmp</c> (generated
/// <c>BuildUI</c>); this hand-written file owns the runtime behavior and is never overwritten
/// by <c>myra-export</c>.
/// </summary>
public partial class DebugOverlayPanel
{
    private const double RefreshIntervalSeconds = 0.25;
    private const int FrameSampleCapacity = 120;

    private readonly double[] _frameSamples = new double[FrameSampleCapacity];

    private double _frameTimeSum;
    private double _refreshTimer = RefreshIntervalSeconds;
    private int _sampleIndex;
    private int _sampleCount;
    private long _tickCount;
    private float _interpolationAlpha;
    private float _tickRate = SimulationClock.DefaultTickRate;
    private Vector3 _cameraFocus;
    private float _cameraDistance;
    private int _entityCount;

    public DebugOverlayPanel()
    {
        BuildUI();

        _titleLabel.Text = $"{GameInfo.Title} {GameInfo.Version}";
        RefreshText();
        Visible = false;
    }

    public void SetVisible(bool visible)
    {
        Visible = visible;
        if (visible)
        {
            _refreshTimer = RefreshIntervalSeconds;
        }
    }

    public void Toggle() => SetVisible(!Visible);

    public void UpdateStatistics(
        double elapsedSeconds,
        long tickCount,
        float interpolationAlpha,
        float tickRate,
        Vector3 cameraFocus,
        float cameraDistance,
        int entityCount)
    {
        _tickCount = tickCount;
        _interpolationAlpha = interpolationAlpha;
        _tickRate = tickRate;
        _cameraFocus = cameraFocus;
        _cameraDistance = cameraDistance;
        _entityCount = entityCount;

        if (elapsedSeconds > 0 && !double.IsNaN(elapsedSeconds) && !double.IsInfinity(elapsedSeconds))
        {
            AddFrameSample(elapsedSeconds);
            _refreshTimer += elapsedSeconds;
        }

        if (_refreshTimer >= RefreshIntervalSeconds)
        {
            _refreshTimer = 0;
            RefreshText();
        }
    }

    private void AddFrameSample(double elapsedSeconds)
    {
        var previous = _frameSamples[_sampleIndex];
        if (_sampleCount == FrameSampleCapacity)
        {
            _frameTimeSum -= previous;
        }
        else
        {
            _sampleCount++;
        }

        _frameSamples[_sampleIndex] = elapsedSeconds;
        _frameTimeSum += elapsedSeconds;
        _sampleIndex = (_sampleIndex + 1) % FrameSampleCapacity;
    }

    private void RefreshText()
    {
        var fps = _sampleCount > 0 ? _sampleCount / _frameTimeSum : 0;
        var frameMs = _sampleCount > 0 ? _frameTimeSum / _sampleCount * 1000.0 : 0;

        _statsLabel.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"FPS {fps:0}  |  {frameMs:0.0} ms/frame");

        _simulationLabel.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"Sim {_tickRate:0} Hz  |  tick {_tickCount}  |  alpha {_interpolationAlpha:0.00}  |  entities {_entityCount}");

        _cameraLabel.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"Focus {_cameraFocus.X:0.0}, {_cameraFocus.Z:0.0}  |  cell {MapGrid.WorldToCell(_cameraFocus).X},{MapGrid.WorldToCell(_cameraFocus).Y}  |  dist {_cameraDistance:0}");
    }
}
