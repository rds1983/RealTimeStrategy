using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using RealTimeStrategy.Presentation.Ui;

namespace RealTimeStrategy.Presentation.Rendering;

/// <summary>
/// Owns the Myra desktop and all in-game UI overlays: the always-on controls HUD and the
/// optional debug panel. UI clicks are tracked so world input can ignore them.
/// </summary>
public sealed class GameUi : IDisposable
{
    private readonly GameHudPanel _hud;
    private readonly DebugOverlayPanel _debugOverlay;
    private readonly Desktop _desktop;

    public GameUi()
    {
        _hud = new GameHudPanel();
        _debugOverlay = new DebugOverlayPanel();

        // A transparent host keeps both panels content-sized and anchored to their own corners.
        var host = new Panel();
        host.Widgets.Add(_hud);
        host.Widgets.Add(_debugOverlay);

        _desktop = new Desktop
        {
            Root = host,
        };

        _debugOverlay.SetVisible(false);
    }

    public bool IsDebugOverlayVisible => _debugOverlay.Visible;

    public void SetDebugOverlayVisible(bool visible) => _debugOverlay.SetVisible(visible);

    public void ToggleDebugOverlay() => _debugOverlay.Toggle();

    public bool IsPointerOverHud(Point point) => _hud.Visible && _hud.Bounds.Contains(point);

    public void UpdateDebugStats(
        double elapsedSeconds,
        long tickCount,
        float interpolationAlpha,
        float tickRate,
        Vector3 cameraFocus,
        float cameraDistance)
    {
        _debugOverlay.UpdateStatistics(
            elapsedSeconds,
            tickCount,
            interpolationAlpha,
            tickRate,
            cameraFocus,
            cameraDistance);
    }

    public void Draw() => _desktop.Render();

    public void Dispose() => _desktop.Dispose();
}
