using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace RealTimeStrategy.Presentation.Rendering;

/// <summary>
/// Generates the ground grid and the translucent build-grid overlay. One texture tile covers
/// exactly one map cell, so the grid aligns with <c>MapGrid.CellSize</c> by construction.
/// </summary>
public static class GridTextureFactory
{
    private const int TilePixels = 128;

    public static Texture2D CreateGroundGrid(GraphicsDevice device)
    {
        var pixels = new Color[TilePixels * TilePixels];
        var ground = new Color(30, 38, 56);
        var line = new Color(46, 58, 80);

        for (var y = 0; y < TilePixels; y++)
        {
            for (var x = 0; x < TilePixels; x++)
            {
                var isLine =
                    x < 2 || y < 2 ||
                    x >= TilePixels - 2 || y >= TilePixels - 2;
                pixels[y * TilePixels + x] = isLine ? line : ground;
            }
        }

        var texture = new Texture2D(device, TilePixels, TilePixels);
        texture.SetData(pixels);
        return texture;
    }

    public static Texture2D CreateBuildGrid(GraphicsDevice device)
    {
        var pixels = new Color[TilePixels * TilePixels];
        var line = new Color(95, 227, 255, 120);

        for (var y = 0; y < TilePixels; y++)
        {
            for (var x = 0; x < TilePixels; x++)
            {
                var isLine =
                    x < 2 || y < 2 ||
                    x >= TilePixels - 2 || y >= TilePixels - 2;
                pixels[y * TilePixels + x] = isLine ? line : Color.Transparent;
            }
        }

        var texture = new Texture2D(device, TilePixels, TilePixels);
        texture.SetData(pixels);
        return texture;
    }
}
