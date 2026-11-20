using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RealTimeStrategy.Presentation.Camera;

namespace RealTimeStrategy.Presentation.Rendering;

/// <summary>
/// Immediate-mode 3D renderer for DigitalRiseModel meshes and models. Callers submit a draw
/// list, then render it with frustum culling against a <see cref="CameraView"/>.
/// </summary>
public sealed class SceneRenderer : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
    private readonly RasterizerState _rasterizerState;
    private readonly RasterizerState _transparentRasterizerState;
    private readonly List<MeshDrawCommand> _commands = new(256);
    private readonly Dictionary<DrModel, Matrix[]> _boneTransforms = new();

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

        _transparentRasterizerState = new RasterizerState
        {
            CullMode = CullMode.None,
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
        Submit(mesh, world, diffuseColor, Color.Black);
    }

    public void Submit(DrMesh mesh, Matrix world, Color diffuseColor, Color emissiveColor)
    {
        Submit(mesh, world, diffuseColor, emissiveColor, null);
    }

    public void Submit(DrMesh mesh, Matrix world, Color diffuseColor, Texture2D texture)
    {
        Submit(mesh, world, diffuseColor, Color.Black, texture);
    }

    public void Submit(
        DrMesh mesh,
        Matrix world,
        Color diffuseColor,
        Color emissiveColor,
        Texture2D? texture)
    {
        foreach (var part in mesh.MeshParts)
        {
            AddCommand(part, world, diffuseColor, emissiveColor, texture, 1f, transparent: false);
        }
    }

    /// <summary>Submits a translucent mesh; drawn after all opaque geometry without depth writes.</summary>
    public void SubmitTransparent(
        DrMesh mesh,
        Matrix world,
        Color diffuseColor,
        Texture2D? texture = null,
        float alpha = 0.4f)
    {
        foreach (var part in mesh.MeshParts)
        {
            AddCommand(part, world, diffuseColor, Color.Black, texture, alpha, transparent: true);
        }
    }

    /// <summary>Submits one mesh part using its <see cref="DrMaterial"/> colors.</summary>
    public void Submit(DrMeshPart part, Matrix world)
    {
        var material = part.Material;
        Submit(
            part,
            world,
            material?.DiffuseColor ?? Color.White,
            material?.EmissiveColor ?? Color.Black);
    }

    public void Submit(DrMeshPart part, Matrix world, Color diffuseColor, Color emissiveColor)
    {
        AddCommand(part, world, diffuseColor, emissiveColor, null, 1f, transparent: false);
    }

    /// <summary>
    /// Submits a <see cref="DrModel"/>: computes absolute bone transforms once and uses each
    /// mesh's parent bone to place it.
    /// </summary>
    public void Submit(DrModel model, Matrix world)
    {
        if (!_boneTransforms.TryGetValue(model, out var boneTransforms))
        {
            boneTransforms = new Matrix[model.Bones.Length];
            _boneTransforms.Add(model, boneTransforms);
        }

        model.CopyAbsoluteBoneTransformsTo(boneTransforms);

        foreach (var mesh in model.Meshes)
        {
            var parentBone = mesh.ParentBone;
            var meshWorld = parentBone is null ? world : boneTransforms[parentBone.Index] * world;

            foreach (var part in mesh.MeshParts)
            {
                Submit(part, meshWorld);
            }
        }
    }

    public void Render()
    {
        if (_camera is null)
        {
            throw new InvalidOperationException("BeginFrame must be called before Render.");
        }

        _device.RasterizerState = _rasterizerState;
        _device.RasterizerState = _rasterizerState;
        _device.SamplerStates[0] = SamplerState.LinearWrap;

        _effect.View = _camera.View;
        _effect.Projection = _camera.Projection;

        RenderPass(transparent: false);
        RenderPass(transparent: true);
    }

    private void RenderPass(bool transparent)
    {
        _device.BlendState = transparent ? BlendState.AlphaBlend : BlendState.Opaque;
        _device.DepthStencilState = transparent ? DepthStencilState.DepthRead : DepthStencilState.Default;
        _device.RasterizerState = transparent ? _transparentRasterizerState : _rasterizerState;
        _effect.LightingEnabled = !transparent;

        foreach (var command in _commands)
        {
            if (command.Transparent != transparent)
            {
                continue;
            }

            var part = command.Part;
            if (!_camera!.IsVisible(part.BoundingBox, command.World))
            {
                CulledCount++;
                continue;
            }

            _effect.World = command.World;
            _effect.DiffuseColor = command.DiffuseColor.ToVector3();
            _effect.EmissiveColor = command.EmissiveColor.ToVector3();
            _effect.TextureEnabled = command.Texture is not null;
            _effect.Texture = command.Texture;
            _effect.Alpha = command.Alpha;
            _effect.CurrentTechnique.Passes[0].Apply();

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

            DrawnCount++;
        }
    }

    private void AddCommand(
        DrMeshPart part,
        Matrix world,
        Color diffuseColor,
        Color emissiveColor,
        Texture2D? texture,
        float alpha,
        bool transparent)
    {
        _commands.Add(new MeshDrawCommand(
            part,
            world,
            diffuseColor,
            emissiveColor,
            texture,
            alpha,
            transparent));
        SubmittedCount++;
    }

    public void Dispose()
    {
        _effect.Dispose();
        _rasterizerState.Dispose();
        _transparentRasterizerState.Dispose();
        _boneTransforms.Clear();
    }

    private readonly record struct MeshDrawCommand(
        DrMeshPart Part,
        Matrix World,
        Color DiffuseColor,
        Color EmissiveColor,
        Texture2D? Texture,
        float Alpha,
        bool Transparent);
}
