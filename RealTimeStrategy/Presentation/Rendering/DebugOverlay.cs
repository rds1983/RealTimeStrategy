using Myra.Graphics2D.UI;
using RealTimeStrategy.Presentation.Ui;

namespace RealTimeStrategy.Presentation.Rendering;

/// <summary>
/// Hosts the Myra debug panel on a desktop and exposes it to the game loop.
/// The panel's own behavior lives in <see cref="DebugOverlayPanel"/>.
/// </summary>
public sealed class DebugOverlay : IDisposable
{
    private readonly DebugOverlayPanel _panel;
    private readonly Desktop _desktop;

    public DebugOverlay()
    {
        _panel = new DebugOverlayPanel();

        // A transparent host keeps the stack panel content-sized in the top-left corner.
        var host = new Panel();
        host.Widgets.Add(_panel);

        _desktop = new Desktop
        {
            Root = host,
        };
    }

    public bool IsVisible => _panel.Visible;

    public void SetVisible(bool visible) => _panel.SetVisible(visible);

    public void Toggle() => _panel.Toggle();

    public void Update(double elapsedSeconds, long tickCount, float interpolationAlpha, float tickRate)
    {
        _panel.UpdateStatistics(elapsedSeconds, tickCount, interpolationAlpha, tickRate);
    }

    public void Draw()
    {
        if (!_panel.Visible)
        {
            return;
        }

        _desktop.Render();
    }

    public void Dispose()
    {
        _desktop.Dispose();
    }
}
