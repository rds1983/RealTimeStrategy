using System.Diagnostics;
using AssetManagementBase;
using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using RealTimeStrategy.Core;
using RealTimeStrategy.Core.Definitions;
using RealTimeStrategy.Core.Simulation;
using RealTimeStrategy.Core.World;
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
    private DefinitionRegistry _definitions = null!;
    private World _world = null!;
    private DrMesh _groundMesh = null!;
    private DrMesh _shadowMesh = null!;
    private Texture2D _groundTexture = null!;
    private Texture2D _buildGridTexture = null!;
    private readonly List<SceneObject> _sceneObjects = new();
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private int _previousScrollValue;
    private bool _showBuildGrid;
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
        _showBuildGrid = args?.Contains("--build-grid") == true;
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

        CreateWorldScene();

        base.LoadContent();
    }

    protected override void UnloadContent()
    {
        _gameUi.Dispose();
        _sceneRenderer.Dispose();
        _meshLibrary.Dispose();
        _groundTexture.Dispose();
        _buildGridTexture.Dispose();

        base.UnloadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();

        if (keyboard.IsKeyDown(Keys.F1) && _previousKeyboard.IsKeyUp(Keys.F1))
        {
            _gameUi.ToggleDebugOverlay();
        }

        if (keyboard.IsKeyDown(Keys.G) && _previousKeyboard.IsKeyUp(Keys.G))
        {
            _showBuildGrid = !_showBuildGrid;
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
            _rtsCamera.Distance,
            _world.Entities.Count);

        _previousKeyboard = keyboard;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(ClearColor);

        base.Draw(gameTime);

        _sceneRenderer.BeginFrame(_camera);

        _sceneRenderer.Submit(_groundMesh, Matrix.Identity, Color.White, _groundTexture);

        foreach (var sceneObject in _sceneObjects)
        {
            _sceneRenderer.Submit(sceneObject.Mesh, sceneObject.World, sceneObject.Color);
        }

        var interpolationAlpha = _simulationClock.Alpha;

        foreach (var entity in _world.Entities)
        {
            var position = Vector3.Lerp(entity.PreviousPosition, entity.Position, interpolationAlpha);
            var paletteId = EntityModels.GetPalette(_world.GetPlayer(entity.Owner).Faction);
            var model = _modelFactory.Get(EntityModels.GetModel(entity), paletteId);

            _sceneRenderer.Submit(model, Matrix.CreateTranslation(position));
        }

        foreach (var entity in _world.Entities)
        {
            var position = Vector3.Lerp(entity.PreviousPosition, entity.Position, interpolationAlpha);
            var shadowRadius = ModelShadow.GetRadius(EntityModels.GetModel(entity));
            var shadowWorld =
                Matrix.CreateScale(shadowRadius * 2f, 1f, shadowRadius * 2f) *
                Matrix.CreateTranslation(position.X, 0.015f, position.Z);

            _sceneRenderer.SubmitTransparent(_shadowMesh, shadowWorld, Color.Black, alpha: 0.4f);
        }

        if (_showBuildGrid)
        {
            _sceneRenderer.SubmitTransparent(
                _groundMesh,
                Matrix.CreateTranslation(0f, 0.03f, 0f),
                Color.White,
                _buildGridTexture,
                alpha: 1f);
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
                    $"cached meshes {_meshLibrary.Count}; models {_modelFactory.Count}; entities {_world.Entities.Count}.");

                if (_screenshotPath != null)
                {
                    CaptureBackBuffer(_screenshotPath);
                }

                Exit();
            }
        }
    }

    /// <summary>
    /// Temporary skirmish scene used until match setup/AI arrive. Spawns all MVP model types
    /// through the world command queue so entity rendering is exercised end to end.
    /// </summary>
    private void CreateWorldScene()
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
            new Vector2(-MapGrid.HalfSize, -MapGrid.HalfSize),
            new Vector2(MapGrid.HalfSize, MapGrid.HalfSize));
        _rtsCamera.Zoom(-6f);
        _rtsCamera.Update(10f);

        _previousMouse = Mouse.GetState();
        _previousScrollValue = _previousMouse.ScrollWheelValue;

        _groundTexture = GridTextureFactory.CreateGroundGrid(GraphicsDevice);
        _buildGridTexture = GridTextureFactory.CreateBuildGrid(GraphicsDevice);

        _groundMesh = _meshLibrary.GetPlane(
            new Vector2(MapGrid.MapSize, MapGrid.MapSize),
            uScale: MapGrid.CellCount,
            vScale: MapGrid.CellCount);
        _shadowMesh = _meshLibrary.GetDisc(0.5f, 24);

        AddMapBorder();

        _definitions = DefinitionRegistry.CreateDefault();
        _world = new World(_definitions, seed: 12345);
        _world.GetPlayer(PlayerId.Player).SetResources(500, 100);
        _world.GetPlayer(PlayerId.Enemy).SetResources(500, 100);

        QueueSpawn(PlayerId.Player, BuildingId.Nexus, -12f, -12f);
        QueueSpawn(PlayerId.Player, BuildingId.Reactor, -17f, -17f);
        QueueSpawn(PlayerId.Player, BuildingId.Fabricator, -5f, -12f);
        QueueSpawn(PlayerId.Player, BuildingId.Foundry, -12f, -4f);
        QueueSpawn(PlayerId.Player, BuildingId.Turret, -3f, -18f);
        QueueSpawn(PlayerId.Player, UnitId.Harvester, -10f, -9f);
        QueueSpawn(PlayerId.Player, UnitId.Scout, -6f, -8f);
        QueueSpawn(PlayerId.Player, UnitId.Striker, -2f, -11f);
        QueueSpawn(PlayerId.Player, UnitId.Lancer, 1f, -7f);

        QueueSpawn(PlayerId.Enemy, BuildingId.Nexus, 12f, 12f);
        QueueSpawn(PlayerId.Enemy, BuildingId.Reactor, 17f, 17f);
        QueueSpawn(PlayerId.Enemy, BuildingId.Fabricator, 5f, 12f);
        QueueSpawn(PlayerId.Enemy, BuildingId.Foundry, 12f, 4f);
        QueueSpawn(PlayerId.Enemy, BuildingId.Turret, 3f, 18f);
        QueueSpawn(PlayerId.Enemy, UnitId.Harvester, 10f, 9f);
        QueueSpawn(PlayerId.Enemy, UnitId.Scout, 6f, 8f);
        QueueSpawn(PlayerId.Enemy, UnitId.Striker, 2f, 11f);
        QueueSpawn(PlayerId.Enemy, UnitId.Lancer, -1f, 7f);

        _world.Step(1f / 20f);
    }

    private void QueueSpawn(PlayerId owner, BuildingId building, float x, float z)
    {
        _world.Enqueue(new SpawnBuildingCommand(owner, building, new Vector3(x, 0f, z)));
    }

    private void QueueSpawn(PlayerId owner, UnitId unit, float x, float z)
    {
        _world.Enqueue(new SpawnUnitCommand(owner, unit, new Vector3(x, 0f, z)));
    }

    private void AddMapBorder()
    {
        var color = new Color(70, 150, 180);
        var horizontal = _meshLibrary.GetBox(new Vector3(MapGrid.MapSize + 0.8f, 0.3f, 0.5f));
        var vertical = _meshLibrary.GetBox(new Vector3(0.5f, 0.3f, MapGrid.MapSize + 0.8f));

        _sceneObjects.Add(new SceneObject(
            horizontal,
            Matrix.CreateTranslation(0f, 0.15f, -MapGrid.HalfSize - 0.1f),
            color));
        _sceneObjects.Add(new SceneObject(
            horizontal,
            Matrix.CreateTranslation(0f, 0.15f, MapGrid.HalfSize + 0.1f),
            color));
        _sceneObjects.Add(new SceneObject(
            vertical,
            Matrix.CreateTranslation(-MapGrid.HalfSize - 0.1f, 0.15f, 0f),
            color));
        _sceneObjects.Add(new SceneObject(
            vertical,
            Matrix.CreateTranslation(MapGrid.HalfSize + 0.1f, 0.15f, 0f),
            color));
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
        _world.Step(deltaSeconds);
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
}
