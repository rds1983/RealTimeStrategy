using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RealTimeStrategy.Core;

namespace RealTimeStrategy;

/// <summary>
/// Hosts the MonoGame window and the frame loop. Gameplay systems are added in later tasks.
/// </summary>
public sealed class VectorFrontGame : Game
{
    private static readonly Color ClearColor = new(8, 10, 18);

    private readonly GraphicsDeviceManager _graphics;
    private readonly int? _smokeTestFrameLimit;
    private int _drawnFrames;

    public VectorFrontGame(string[]? args = null)
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1600,
            PreferredBackBufferHeight = 900,
            SynchronizeWithVerticalRetrace = true,
            GraphicsProfile = GraphicsProfile.HiDef,
        };

        Content.RootDirectory = "Assets";
        IsMouseVisible = true;
        Window.Title = GameInfo.Title;
        Window.AllowUserResizing = false;

        _smokeTestFrameLimit = ParseSmokeTestFrameLimit(args);
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(ClearColor);

        base.Draw(gameTime);

        if (_smokeTestFrameLimit is int limit)
        {
            _drawnFrames++;
            if (_drawnFrames >= limit)
            {
                Console.WriteLine($"Smoke test complete: drew {_drawnFrames} frames.");
                Exit();
            }
        }
    }

    /// <summary>
    /// <c>--smoke-test [frames]</c> renders a fixed number of frames and exits with code 0.
    /// Used to verify the window and graphics device without manual interaction.
    /// </summary>
    private static int? ParseSmokeTestFrameLimit(string[]? args)
    {
        if (args is null)
        {
            return null;
        }

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] != "--smoke-test")
            {
                continue;
            }

            if (i + 1 < args.Length && int.TryParse(args[i + 1], out var frames) && frames > 0)
            {
                return frames;
            }

            return 60;
        }

        return null;
    }
}
