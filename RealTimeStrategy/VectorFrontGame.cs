using System.Diagnostics;
using AssetManagementBase;
using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using RealTimeStrategy.Core;
using RealTimeStrategy.Core.Simulation;
using RealTimeStrategy.Presentation.Camera;
using RealTimeStrategy.Presentation.Content;
using RealTimeStrategy.Presentation.Rendering;

namespace RealTimeStrategy;

/// <summary>
/// Hosts the MonoGame window and the frame loop. Gameplay systems are added in later tasks.
/// </summary>
public sealed class VectorFrontGame : Game
{
    private static readonly Color ClearColor = new(8, 10, 18);

    private readonly GraphicsDeviceManager _graphics;
    private readonly int? _smokeTestFrameLimit;
    private readonly bool _startWithOverlay;
    private readonly string? _screenshotPath;
    private readonly Stopwatch _wallClock = new();

    private SimulationClock _simulationClock = null!;
    private GameUi _gameUi = null!;
    private SceneRenderer _sceneRenderer = null!;
    private CameraView _camera = null!;
    private RtsCamera _rtsCamera = null!;
    private MeshLibrary _meshLibrary = null!;
    private ModelFactory _modelFactory = null!;
    private DrMesh _groundMesh = null!;
    private readonly List<SceneObject> _sceneObjects = new();
    private readonly List<GalleryItem> _gallery = new();
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private int _previousScrollValue;
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
        IsFixedTimeStep = false;
        Window.Title = GameInfo.Title;
        Window.AllowUserResizing = false;

        _smokeTestFrameLimit = ParseSmokeTestFrameLimit(args);
        _startWithOverlay = args?.Contains("--overlay") == true;
        _screenshotPath = ParseOptionValue(args, "--screenshot");
        _wallClock.Start();
    }

    protected override void LoadContent()
    {
        Directory.CreateDirectory(AssetPaths.UiRoot);
        MyraEnvironment.Game = this;
        MyraAssets.DefaultAssetManager = AssetManager.CreateFileAssetManager(AssetPaths.UiRoot);

        _simulationClock = new SimulationClock(SimulationClock.DefaultTickRate);
        _gameUi = new GameUi();
        _gameUi.SetDebugOverlayVisible(_startWithOverlay);

        CreateModelGallery();

        base.LoadContent();
    }

    protected override void UnloadContent()
    {
        _gameUi.Dispose();
        _sceneRenderer.Dispose();
        _meshLibrary.Dispose();

        base.UnloadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();

        if (keyboard.IsKeyDown(Keys.F1) && _previousKeyboard.IsKeyUp(Keys.F1))
        {
            _gameUi.ToggleDebugOverlay();
        }

        if (keyboard.IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        var elapsedSeconds = gameTime.ElapsedGameTime.TotalSeconds;
        var steps = _simulationClock.Advance(elapsedSeconds);
        for (var i = 0; i < steps; i++)
        {
            StepSimulation(_simulationClock.TickInterval);
        }

        if (_smokeTestFrameLimit is null)
        {
            UpdateCamera(keyboard, elapsedSeconds);
        }
        else
        {
            // Smoke runs are deterministic: no mouse/keyboard camera input.
            _rtsCamera.Update((float)elapsedSeconds);
        }

        _gameUi.UpdateDebugStats(
            elapsedSeconds,
            _simulationClock.TickCount,
            _simulationClock.Alpha,
            SimulationClock.DefaultTickRate,
            _rtsCamera.Focus,
            _rtsCamera.Distance);

        _previousKeyboard = keyboard;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(ClearColor);

        base.Draw(gameTime);

        _sceneRenderer.BeginFrame(_camera);
        foreach (var sceneObject in _sceneObjects)
        {
            _sceneRenderer.Submit(sceneObject.Mesh, sceneObject.World, sceneObject.Color);
        }

        foreach (var galleryItem in _gallery)
        {
            _sceneRenderer.Submit(galleryItem.Model, galleryItem.World);
        }

        _sceneRenderer.Render();

        _gameUi.Draw();

        if (_smokeTestFrameLimit is int limit)
        {
            _drawnFrames++;
            if (_drawnFrames >= limit)
            {
                var simulatedSeconds = _simulationClock.TickCount * (double)_simulationClock.TickInterval;
                Console.WriteLine(
                    $"Smoke test complete: drew {_drawnFrames} frames in {_wallClock.Elapsed.TotalSeconds:0.00}s; " +
                    $"sim tick {_simulationClock.TickCount} ({simulatedSeconds:0.00}s); " +
                    $"cached meshes {_meshLibrary.Count}; models {_modelFactory.Count}.");

                if (_screenshotPath != null)
                {
                    CaptureBackBuffer(_screenshotPath);
                }

                Exit();
            }
        }
    }

    /// <summary>
    /// Temporary model gallery used to validate silhouettes and palettes. Replaced by the world
    /// renderer once entities exist (T-009).
    /// </summary>
    private void CreateModelGallery()
    {
        _sceneRenderer = new SceneRenderer(GraphicsDevice);
        _meshLibrary = new MeshLibrary(GraphicsDevice);
        _modelFactory = new ModelFactory(_meshLibrary);

        _camera = new CameraView(MathHelper.PiOver4, 0.5f, 500f);
        _camera.SetViewport(
            GraphicsDevice.PresentationParameters.BackBufferWidth,
            GraphicsDevice.PresentationParameters.BackBufferHeight);
        _rtsCamera = new RtsCamera(
            _camera,
            new Vector2(-60f, -60f),
            new Vector2(60f, 60f));
        _rtsCamera.Zoom(-4f);
        _rtsCamera.Update(10f);

        _previousMouse = Mouse.GetState();
        _previousScrollValue = _previousMouse.ScrollWheelValue;

        _groundMesh = _meshLibrary.GetPlane(new Vector2(120f, 120f));
        _sceneObjects.Add(new SceneObject(_groundMesh, Matrix.Identity, new Color(28, 34, 48)));

        var concord = FactionPaletteId.Concord;
        var nullFaction = FactionPaletteId.Null;

        // Buildings, Concord.
        AddGalleryModel(ModelId.Nexus, concord, -24f, -14f);
        AddGalleryModel(ModelId.Reactor, concord, -12f, -14f);
        AddGalleryModel(ModelId.Fabricator, concord, 0f, -14f);
        AddGalleryModel(ModelId.Foundry, concord, 12f, -14f);
        AddGalleryModel(ModelId.Turret, concord, 24f, -14f);

        // Units, both palettes.
        AddGalleryModel(ModelId.Harvester, concord, -18f, -2f);
        AddGalleryModel(ModelId.Scout, concord, -6f, -2f);
        AddGalleryModel(ModelId.Striker, concord, 6f, -2f);
        AddGalleryModel(ModelId.Lancer, concord, 18f, -2f);

        AddGalleryModel(ModelId.Harvester, nullFaction, -18f, 7f);
        AddGalleryModel(ModelId.Scout, nullFaction, -6f, 7f);
        AddGalleryModel(ModelId.Striker, nullFaction, 6f, 7f);
        AddGalleryModel(ModelId.Lancer, nullFaction, 18f, 7f);

        // Props.
        AddGalleryModel(ModelId.AlloyNode, FactionPaletteId.Neutral, -28f, 11f);
        AddGalleryModel(ModelId.Projectile, concord, 22f, 11f, y: 1.6f);
        AddGalleryModel(ModelId.Debris, nullFaction, 25f, 4f);
    }

    private void AddGalleryModel(ModelId id, FactionPaletteId palette, float x, float z, float y = 0f)
    {
        _gallery.Add(new GalleryItem(
            _modelFactory.Get(id, palette),
            Matrix.CreateTranslation(x, y, z)));
    }

    private void UpdateCamera(KeyboardState keyboard, double elapsedSeconds)
    {
        var delta = (float)elapsedSeconds;
        var mouse = Mouse.GetState();
        var viewport = GraphicsDevice.Viewport;

        if (IsActive)
        {
            var pan = Vector2.Zero;
            if (keyboard.IsKeyDown(Keys.Left)) pan.X -= 1f;
            if (keyboard.IsKeyDown(Keys.Right)) pan.X += 1f;
            if (keyboard.IsKeyDown(Keys.Up)) pan.Y += 1f;
            if (keyboard.IsKeyDown(Keys.Down)) pan.Y -= 1f;

            const int edge = 8;
            var bounds = viewport.Bounds;
            var pointerOverHud = _gameUi.IsPointerOverHud(mouse.Position);
            if (bounds.Contains(mouse.Position) && !pointerOverHud)
            {
                if (mouse.X <= bounds.Left + edge) pan.X -= 1f;
                else if (mouse.X >= bounds.Right - 1 - edge) pan.X += 1f;

                if (mouse.Y <= bounds.Top + edge) pan.Y += 1f;
                else if (mouse.Y >= bounds.Bottom - 1 - edge) pan.Y -= 1f;
            }

            if (pan != Vector2.Zero)
            {
                _rtsCamera.Pan(pan, delta);
            }

            if (keyboard.IsKeyDown(Keys.Q) && _previousKeyboard.IsKeyUp(Keys.Q))
            {
                _rtsCamera.Rotate(1);
            }

            if (keyboard.IsKeyDown(Keys.E) && _previousKeyboard.IsKeyUp(Keys.E))
            {
                _rtsCamera.Rotate(-1);
            }

            if (!pointerOverHud &&
                mouse.MiddleButton == ButtonState.Pressed &&
                _previousMouse.MiddleButton == ButtonState.Pressed)
            {
                var dx = mouse.X - _previousMouse.X;
                var dy = mouse.Y - _previousMouse.Y;
                if (dx != 0 || dy != 0)
                {
                    var unitsPerPixel = _rtsCamera.WorldUnitsPerPixel(viewport.Height);
                    _rtsCamera.PanBy(new Vector2(-dx, dy) * unitsPerPixel);
                }
            }
        }

        var scroll = mouse.ScrollWheelValue;
        if (IsActive && scroll != _previousScrollValue)
        {
            _rtsCamera.Zoom((scroll - _previousScrollValue) / 120f);
        }

        _previousScrollValue = scroll;
        _previousMouse = mouse;
        _rtsCamera.Update(delta);
    }

    /// <summary>
    /// Fixed-rate simulation step. Entity and system updates are wired in later tasks.
    /// </summary>
    private void StepSimulation(float deltaSeconds)
    {
    }

    private void CaptureBackBuffer(string path)
    {
        var width = GraphicsDevice.PresentationParameters.BackBufferWidth;
        var height = GraphicsDevice.PresentationParameters.BackBufferHeight;
        var pixels = new Color[width * height];
        GraphicsDevice.GetBackBufferData(pixels);

        using var texture = new Texture2D(GraphicsDevice, width, height);
        texture.SetData(pixels);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(path);
        texture.SaveAsPng(stream, width, height);
        Console.WriteLine($"Screenshot written: {Path.GetFullPath(path)}");
    }

    private static string? ParseOptionValue(string[]? args, string option)
    {
        if (args is null)
        {
            return null;
        }

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == option)
            {
                return args[i + 1];
            }
        }

        return null;
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

    private readonly record struct SceneObject(DrMesh Mesh, Matrix World, Color Color);

    private readonly record struct GalleryItem(DrModel Model, Matrix World);
}
