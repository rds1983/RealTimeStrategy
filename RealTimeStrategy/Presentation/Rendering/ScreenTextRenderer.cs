using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace RealTimeStrategy.Presentation.Rendering;

/// <summary>Small screen-space text renderer for world-anchored labels (group badges).</summary>
public sealed class ScreenTextRenderer : IDisposable
{
    private readonly SpriteBatch _spriteBatch;
    private readonly FontSystem _fontSystem;
    private readonly SpriteFontBase _font;

    public ScreenTextRenderer(GraphicsDevice graphicsDevice, string fontPath, float fontSize = 20f)
    {
        _spriteBatch = new SpriteBatch(graphicsDevice);
        _fontSystem = new FontSystem();
        _fontSystem.AddFont(File.ReadAllBytes(fontPath));
        _font = _fontSystem.GetFont(fontSize);
    }

    public Vector2 Measure(string text) => _font.MeasureString(text);

    public void Begin()
    {
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);
    }

    public void Draw(string text, Vector2 position, Color color)
    {
        _spriteBatch.DrawString(_font, text, position, color);
    }

    public void End() => _spriteBatch.End();

    public void Dispose()
    {
        _spriteBatch.Dispose();
        _fontSystem.Dispose();
    }
}
