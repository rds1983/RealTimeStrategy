using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RealTimeStrategy.Presentation.Camera;

namespace RealTimeStrategy.Presentation.Rendering;

/// <summary>
/// Immediate-mode 3D renderer for procedurally generated meshes. Callers submit a draw list,
/// then render it with frustum culling against a <see cref="CameraView"/>.
/// </summary>
public sealed class SceneRenderer : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
    private readonly RasterizerState _rasterizerState;
    private readonly List<MeshDrawCommand> _commands = new(256);

    private CameraView? _camera;

    public SceneRenderer(GraphicsDevice device)
    {
        _device = device;

        _effect = new BasicEffect(device)
        {
            LightingEnabled = true,
            PreferPerPixelLighting = true,
            TextureEnabled = false,
            VertexColorEnabled = false,
            AmbientLightColor = new Vector3(0.42f),
            DiffuseColor = Vector3.One,
            SpecularColor = new Vector3(0.08f),
            SpecularPower = 24f,
        };

        _effect.DirectionalLight0.Enabled = true;
        _effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.45f, -1f, -0.35f));
        _effect.DirectionalLight0.DiffuseColor = new Vector3(0.78f);
        _effect.DirectionalLight1.Enabled = false;
        _effect.DirectionalLight2.Enabled = false;

        _rasterizerState = new RasterizerState
        {
            CullMode = CullMode.CullCounterClockwiseFace,
            FillMode = FillMode.Solid,
        };
    }

    public int SubmittedCount { get; private set; }

    public int CulledCount { get; private set; }

    public int DrawnCount { get; private set; }

    public void BeginFrame(CameraView camera)
    {
        _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        _commands.Clear();
        SubmittedCount = 0;
        CulledCount = 0;
        DrawnCount = 0;
    }

    public void Submit(DrMesh mesh, Matrix world, Color diffuseColor)
    {
        _commands.Add(new MeshDrawCommand(mesh, world, diffuseColor));
        SubmittedCount++;
    }

    public void Render()
    {
        if (_camera is null)
        {
            throw new InvalidOperationException("BeginFrame must be called before Render.");
        }

        _device.BlendState = BlendState.Opaque;
        _device.DepthStencilState = DepthStencilState.Default;
        _device.RasterizerState = _rasterizerState;

        _effect.View = _camera.View;
        _effect.Projection = _camera.Projection;

        foreach (var command in _commands)
        {
            if (!_camera.IsVisible(command.Mesh.BoundingBox, command.World))
            {
                CulledCount++;
                continue;
            }

            _effect.World = command.World;
            _effect.DiffuseColor = command.DiffuseColor.ToVector3();
            _effect.CurrentTechnique.Passes[0].Apply();

            foreach (var part in command.Mesh.MeshParts)
            {
                _device.SetVertexBuffer(part.VertexBuffer);

                if (part.IndexBuffer is not null)
                {
                    _device.Indices = part.IndexBuffer;
                    _device.DrawIndexedPrimitives(
                        part.PrimitiveType,
                        part.VertexOffset,
                        part.StartIndex,
                        part.PrimitiveCount);
                }
                else
                {
                    _device.DrawPrimitives(part.PrimitiveType, part.VertexOffset, part.PrimitiveCount);
                }
            }

            DrawnCount++;
        }
    }

    public void Dispose()
    {
        _effect.Dispose();
        _rasterizerState.Dispose();
    }

    private readonly record struct MeshDrawCommand(DrMesh Mesh, Matrix World, Color DiffuseColor);
}
