using DigitalRiseModel;
using Microsoft.Xna.Framework;

namespace RealTimeStrategy.Presentation.Rendering;

/// <summary>
/// Assembles the prototype's procedural models from cached <see cref="MeshLibrary"/> primitives.
/// Models are immutable and shared per (model id, palette).
/// </summary>
public sealed class ModelFactory
{
    private const int LowTessellation = 12;
    private const int MidTessellation = 16;
    private const int SmoothTessellation = 32;

    private readonly MeshLibrary _meshes;
    private readonly Dictionary<(ModelId Id, FactionPaletteId Palette), DrModel> _cache = new();

    public ModelFactory(MeshLibrary meshes)
    {
        _meshes = meshes ?? throw new ArgumentNullException(nameof(meshes));
    }

    public int Count => _cache.Count;

    public DrModel Get(ModelId id, FactionPaletteId paletteId)
    {
        var key = (id, paletteId);
        if (!_cache.TryGetValue(key, out var model))
        {
            model = Build(id, FactionPalettes.Get(paletteId));
            _cache.Add(key, model);
        }

        return model;
    }

    private DrModel Build(ModelId id, FactionPalette palette)
    {
        var builder = new ModelBuilder();

        var chassis = new DrMaterial
        {
            Name = "chassis",
            DiffuseColor = palette.Chassis,
            SpecularColor = new Color(24, 24, 24),
            Shininess = 16f,
        };

        var panel = new DrMaterial
        {
            Name = "panel",
            DiffuseColor = palette.Panel,
            SpecularColor = new Color(12, 12, 12),
            Shininess = 8f,
        };

        var accentBase = new Color(
            (int)(palette.Accent.R * 0.35f),
            (int)(palette.Accent.G * 0.35f),
            (int)(palette.Accent.B * 0.35f));

        var accent = new DrMaterial
        {
            Name = "accent",
            DiffuseColor = accentBase,
            EmissiveColor = palette.Accent,
        };

        switch (id)
        {
            case ModelId.Harvester:
                BuildHarvester(builder, chassis, panel, accent);
                break;
            case ModelId.Scout:
                BuildScout(builder, chassis, panel, accent);
                break;
            case ModelId.Striker:
                BuildStriker(builder, chassis, panel, accent);
                break;
            case ModelId.Lancer:
                BuildLancer(builder, chassis, panel, accent);
                break;
            case ModelId.Nexus:
                BuildNexus(builder, chassis, panel, accent);
                break;
            case ModelId.Reactor:
                BuildReactor(builder, chassis, panel, accent);
                break;
            case ModelId.Fabricator:
                BuildFabricator(builder, chassis, panel, accent);
                break;
            case ModelId.Foundry:
                BuildFoundry(builder, chassis, panel, accent);
                break;
            case ModelId.Turret:
                BuildTurret(builder, chassis, panel, accent);
                break;
            case ModelId.AlloyNode:
                BuildAlloyNode(builder, panel, accent);
                break;
            case ModelId.Projectile:
                BuildProjectile(builder, panel, accent);
                break;
            case ModelId.Debris:
                BuildDebris(builder, chassis, panel);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown model id.");
        }

        return builder.Build($"{palette.Name} {id}");
    }

    private void BuildHarvester(ModelBuilder b, DrMaterial chassis, DrMaterial panel, DrMaterial accent)
    {
        b.Add(Box(2.4f, 0.55f, 1.5f), At(0f, 0.85f, 0f), chassis);
        b.Add(Box(0.8f, 0.45f, 1.0f), At(0.65f, 1.35f, 0f), panel);
        b.Add(Box(1.0f, 0.35f, 1.1f), At(-0.75f, 1.25f, 0f), panel);

        b.Add(Cylinder(1.9f, 0.26f, LowTessellation), AlongX(0f, 0.5f, 0.9f), panel);
        b.Add(Cylinder(1.9f, 0.26f, LowTessellation), AlongX(0f, 0.5f, -0.9f), panel);
        b.Add(Sphere(0.18f, LowTessellation), At(-1.0f, 0.5f, 0.9f), accent);
        b.Add(Sphere(0.18f, LowTessellation), At(-1.0f, 0.5f, -0.9f), accent);

        b.Add(Box(0.8f, 0.25f, 1.4f), At(1.45f, 0.3f, 0f), panel);
        b.Add(Box(0.12f, 0.12f, 1.2f), At(1.15f, 0.95f, 0f), accent);
    }

    private void BuildScout(ModelBuilder b, DrMaterial chassis, DrMaterial panel, DrMaterial accent)
    {
        b.Add(Box(2.2f, 0.4f, 0.7f), At(0f, 1.3f, 0f), chassis);
        b.Add(Cone(0.3f, 0.9f, LowTessellation), AlongX(1.45f, 1.3f, 0f), chassis);
        b.Add(Box(0.7f, 0.08f, 2.2f), At(-0.2f, 1.3f, 0f), panel);

        b.Add(Cylinder(0.9f, 0.16f, LowTessellation), AlongX(-0.7f, 1.3f, 0.6f), panel);
        b.Add(Cylinder(0.9f, 0.16f, LowTessellation), AlongX(-0.7f, 1.3f, -0.6f), panel);
        b.Add(Sphere(0.12f, 10), At(-1.2f, 1.3f, 0.6f), accent);
        b.Add(Sphere(0.12f, 10), At(-1.2f, 1.3f, -0.6f), accent);

        b.Add(Box(0.5f, 0.6f, 0.08f), At(-1.0f, 1.65f, 0f), chassis);
        b.Add(Box(0.5f, 0.05f, 0.08f), At(0.25f, 1.52f, 0f), accent);
    }

    private void BuildStriker(ModelBuilder b, DrMaterial chassis, DrMaterial panel, DrMaterial accent)
    {
        b.Add(Box(0.85f, 0.45f, 0.7f), At(0f, 1.2f, 0f), panel);
        b.Add(Box(1.25f, 0.95f, 0.85f), At(0f, 1.9f, 0f), chassis);
        b.Add(Box(0.2f, 0.5f, 0.55f), At(0.68f, 1.95f, 0f), panel);
        b.Add(Box(0.4f, 0.3f, 0.4f), At(0.35f, 2.35f, 0f), panel);
        b.Add(Box(0.08f, 0.14f, 0.4f), At(0.79f, 2.2f, 0f), accent);

        AddLeg(b, panel, chassis, z: 0.32f);
        AddLeg(b, panel, chassis, z: -0.32f);

        b.Add(Box(0.4f, 0.35f, 0.3f), At(0f, 2.15f, 0.62f), panel);
        b.Add(Box(0.4f, 0.35f, 0.3f), At(0f, 2.15f, -0.62f), panel);
        b.Add(Box(0.7f, 0.2f, 0.2f), At(0.4f, 1.95f, 0.62f), panel);
        b.Add(Box(0.7f, 0.2f, 0.2f), At(0.4f, 1.95f, -0.62f), panel);
        b.Add(Cylinder(0.8f, 0.08f, 8), AlongX(1.05f, 1.95f, 0.62f), panel);
        b.Add(Cylinder(0.8f, 0.08f, 8), AlongX(1.05f, 1.95f, -0.62f), panel);
        b.Add(Sphere(0.1f, 8), At(1.47f, 1.95f, 0.62f), accent);
        b.Add(Sphere(0.1f, 8), At(1.47f, 1.95f, -0.62f), accent);

        b.Add(Cylinder(0.9f, 0.035f, 4), At(-0.35f, 2.65f, 0.25f), panel);
    }

    private void BuildLancer(ModelBuilder b, DrMaterial chassis, DrMaterial panel, DrMaterial accent)
    {
        b.Add(Box(1.1f, 0.5f, 0.85f), At(0f, 1.25f, 0f), panel);
        b.Add(Box(1.7f, 1.0f, 1.0f), At(0f, 2.0f, 0f), chassis);
        b.Add(Box(0.4f, 0.9f, 0.7f), At(-0.9f, 2.2f, 0f), panel);
        b.Add(Box(0.45f, 0.35f, 0.5f), At(0.2f, 2.7f, 0f), panel);
        b.Add(Box(0.06f, 0.12f, 0.4f), At(0.44f, 2.7f, 0f), accent);

        AddLeg(b, panel, chassis, z: 0.45f, upperSize: 0.5f, lowerSize: 0.45f, footSize: 0.7f);
        AddLeg(b, panel, chassis, z: -0.45f, upperSize: 0.5f, lowerSize: 0.45f, footSize: 0.7f);

        b.Add(Box(0.7f, 0.45f, 0.5f), At(0f, 2.3f, 0.85f), chassis);
        b.Add(Box(0.7f, 0.45f, 0.5f), At(0f, 2.3f, -0.85f), chassis);
        b.Add(Box(0.6f, 0.5f, 0.55f), At(0.1f, 2.2f, 0f), panel);
        b.Add(Cylinder(2.8f, 0.22f, MidTessellation), AlongX(0.9f, 2.2f, 0f), panel);
        b.Add(Sphere(0.28f, LowTessellation), At(2.35f, 2.2f, 0f), accent);

        b.Add(Cylinder(1.6f, 0.04f, 4), At(-0.7f, 3.15f, 0.4f), panel);
        b.Add(Sphere(0.14f, 8), At(-0.7f, 3.95f, 0.4f), accent);
    }

    private void BuildNexus(ModelBuilder b, DrMaterial chassis, DrMaterial panel, DrMaterial accent)
    {
        b.Add(Cylinder(1.2f, 5.0f, 6), At(0f, 0.6f, 0f), panel);
        b.Add(Cylinder(0.7f, 3.6f, 6), At(0f, 1.55f, 0f), chassis);
        b.Add(Sphere(1.0f, MidTessellation), At(0f, 3.0f, 0f), accent);
        b.Add(Torus(2.8f, 0.14f, SmoothTessellation), At(0f, 2.6f, 0f), accent);
        b.Add(Cone(1.2f, 4.5f, 6), At(0f, 5.7f, 0f), chassis);
        b.Add(Sphere(0.25f, LowTessellation), At(0f, 8.0f, 0f), accent);

        AddPylons(b, panel, accent);
    }

    private void BuildReactor(ModelBuilder b, DrMaterial chassis, DrMaterial panel, DrMaterial accent)
    {
        b.Add(Cylinder(0.8f, 2.2f, MidTessellation), At(0f, 0.4f, 0f), panel);
        b.Add(Cylinder(2.2f, 1.5f, MidTessellation), At(0f, 1.9f, 0f), chassis);
        b.Add(Torus(1.7f, 0.14f, SmoothTessellation), At(0f, 2.7f, 0f), accent);
        b.Add(Sphere(0.85f, MidTessellation), At(0f, 2.9f, 0f), accent);
        b.Add(Cone(0.9f, 0.9f, MidTessellation), At(0f, 3.75f, 0f), panel);

        for (var i = 0; i < 3; i++)
        {
            var angle = i * MathHelper.TwoPi / 3f;
            var x = MathF.Cos(angle) * 1.75f;
            var z = MathF.Sin(angle) * 1.75f;
            b.Add(Box(0.3f, 2.0f, 1.1f), Yaw(x, 1.6f, z, -angle), panel);
        }
    }

    private void BuildFabricator(ModelBuilder b, DrMaterial chassis, DrMaterial panel, DrMaterial accent)
    {
        b.Add(Box(5.2f, 0.7f, 4.2f), At(0f, 0.35f, 0f), panel);
        b.Add(Box(3.6f, 2.0f, 3.0f), At(0f, 1.7f, 0f), chassis);

        b.Add(Box(0.35f, 3.2f, 0.35f), At(-1.2f, 2.8f, 1.5f), panel);
        b.Add(Box(0.35f, 3.2f, 0.35f), At(-1.2f, 2.8f, -1.5f), panel);
        b.Add(Box(0.35f, 0.35f, 3.35f), At(-1.2f, 4.35f, 0f), chassis);

        b.Add(Box(0.2f, 1.6f, 0.2f), At(0.7f, 3.4f, 0f), panel);
        b.Add(Box(0.6f, 0.3f, 0.6f), At(0.7f, 2.5f, 0f), accent);

        b.Add(Cylinder(2.0f, 0.6f, LowTessellation), At(2.0f, 1.3f, 1.0f), panel);
        b.Add(Cylinder(2.0f, 0.6f, LowTessellation), At(2.0f, 1.3f, -1.0f), panel);
        b.Add(Box(0.1f, 0.5f, 1.8f), At(1.82f, 1.8f, 0f), accent);
    }

    private void BuildFoundry(ModelBuilder b, DrMaterial chassis, DrMaterial panel, DrMaterial accent)
    {
        b.Add(Box(5.6f, 0.7f, 4.2f), At(0f, 0.35f, 0f), panel);
        b.Add(Box(4.6f, 2.8f, 3.6f), At(0f, 2.0f, 0f), chassis);
        b.Add(Box(4.9f, 0.4f, 3.9f), At(0f, 3.6f, 0f), panel);

        b.Add(Cylinder(2.2f, 0.4f, LowTessellation), At(-1.2f, 4.7f, 1.0f), panel);
        b.Add(Cylinder(2.2f, 0.4f, LowTessellation), At(-1.2f, 4.7f, -1.0f), panel);
        b.Add(Torus(0.5f, 0.08f, MidTessellation), At(-1.2f, 5.25f, 1.0f), accent);
        b.Add(Torus(0.5f, 0.08f, MidTessellation), At(-1.2f, 5.25f, -1.0f), accent);

        b.Add(Box(0.1f, 1.2f, 1.8f), At(2.32f, 1.8f, 0f), accent);
        b.Add(Cylinder(3.0f, 0.15f, 8), AlongZ(0f, 1.0f, 1.9f), panel);
    }

    private void BuildTurret(ModelBuilder b, DrMaterial chassis, DrMaterial panel, DrMaterial accent)
    {
        b.Add(Cylinder(0.6f, 1.5f, MidTessellation), At(0f, 0.3f, 0f), panel);
        b.Add(Cylinder(0.9f, 0.55f, LowTessellation), At(0f, 1.05f, 0f), chassis);
        b.Add(Box(1.5f, 0.8f, 1.3f), At(0f, 1.9f, 0f), chassis);

        b.Add(Cylinder(1.5f, 0.12f, 8), AlongX(1.05f, 1.9f, 0.35f), panel);
        b.Add(Cylinder(1.5f, 0.12f, 8), AlongX(1.05f, 1.9f, -0.35f), panel);
        b.Add(Sphere(0.16f, 8), At(1.85f, 1.9f, 0.35f), accent);
        b.Add(Sphere(0.16f, 8), At(1.85f, 1.9f, -0.35f), accent);
        b.Add(Sphere(0.22f, LowTessellation), At(0f, 2.45f, 0.4f), accent);
    }

    private void BuildAlloyNode(ModelBuilder b, DrMaterial rock, DrMaterial accent)
    {
        b.Add(Sphere(1.2f, LowTessellation), Scaled(-0.5f, 0.25f, 0.4f, 1.3f, 0.5f, 1.1f), rock);
        b.Add(Sphere(0.8f, LowTessellation), Scaled(0.9f, 0.2f, -0.3f, 1.0f, 0.45f, 0.9f), rock);
        b.Add(Sphere(0.6f, LowTessellation), Scaled(0.2f, 0.15f, 1.1f, 1.1f, 0.4f, 0.8f), rock);

        b.Add(Cone(0.46f, 3.0f, 6), Tilt(-0.8f, 1.45f, 0.3f, 0.15f, 0.05f, 0.12f), accent);
        b.Add(Cone(0.38f, 2.3f, 6), Tilt(0.2f, 1.1f, 0.2f, -0.2f, -0.08f, -0.18f), accent);
        b.Add(Cone(0.3f, 1.8f, 6), Tilt(0.9f, 0.85f, 0.1f, 0.3f, 0.1f, 0.2f), accent);
        b.Add(Cone(0.22f, 1.3f, 6), Tilt(-0.2f, 0.6f, 1.0f, 0.1f, 0.2f, -0.1f), accent);
        b.Add(Cone(0.26f, 1.6f, 6), Tilt(1.3f, 0.75f, -0.6f, -0.25f, -0.1f, 0.18f), accent);
    }

    private void BuildProjectile(ModelBuilder b, DrMaterial panel, DrMaterial accent)
    {
        b.Add(Sphere(0.22f, MidTessellation), At(0f, 0f, 0f), accent);
        b.Add(Cylinder(0.5f, 0.08f, 8), AlongX(-0.28f, 0f, 0f), panel);
    }

    private void BuildDebris(ModelBuilder b, DrMaterial chassis, DrMaterial panel)
    {
        b.Add(Box(0.45f, 0.3f, 0.5f), Tilt(-0.3f, 0.15f, 0f, 0.4f, 0.2f, 0.1f), panel);
        b.Add(Box(0.3f, 0.22f, 0.35f), Tilt(0.35f, 0.1f, 0.25f, -0.3f, 0.1f, -0.2f), chassis);
        b.Add(Box(0.22f, 0.18f, 0.28f), Tilt(0.1f, 0.1f, -0.35f, 0.7f, -0.15f, 0.3f), panel);
    }

    private void AddLeg(
        ModelBuilder b,
        DrMaterial legMaterial,
        DrMaterial footMaterial,
        float z,
        float upperSize = 0.35f,
        float lowerSize = 0.3f,
        float footSize = 0.55f)
    {
        b.Add(Box(upperSize, 0.7f, upperSize), At(0f, 1.15f, z), legMaterial);
        b.Add(Box(lowerSize, 0.7f, lowerSize), At(0.05f, 0.45f, z), legMaterial);
        b.Add(Box(footSize, 0.18f, footSize * 0.75f), At(0.15f, 0.09f, z), footMaterial);
    }

    private void AddPylons(ModelBuilder b, DrMaterial panel, DrMaterial accent)
    {
        float[] offsets = { 3.3f, -3.3f };
        foreach (var offset in offsets)
        {
            b.Add(Box(0.4f, 3.0f, 0.4f), At(offset, 2.1f, 0f), panel);
            b.Add(Box(0.4f, 3.0f, 0.4f), At(0f, 2.1f, offset), panel);
            b.Add(Sphere(0.18f, 8), At(offset, 3.75f, 0f), accent);
            b.Add(Sphere(0.18f, 8), At(0f, 3.75f, offset), accent);
        }
    }

    private DrMesh Box(float x, float y, float z) => _meshes.GetBox(new Vector3(x, y, z));

    private DrMesh Sphere(float radius, int tessellation) => _meshes.GetSphere(radius, tessellation);

    private DrMesh Cylinder(float height, float radius, int tessellation) =>
        _meshes.GetCylinder(height, radius, tessellation);

    private DrMesh Cone(float radius, float height, int tessellation) =>
        _meshes.GetCone(radius, height, tessellation);

    private DrMesh Torus(float majorRadius, float minorRadius, int tessellation) =>
        _meshes.GetTorus(majorRadius, minorRadius, tessellation);

    private static Matrix At(float x, float y, float z) => Matrix.CreateTranslation(x, y, z);

    private static Matrix AlongX(float x, float y, float z) =>
        Matrix.CreateRotationZ(-MathHelper.PiOver2) * Matrix.CreateTranslation(x, y, z);

    private static Matrix AlongZ(float x, float y, float z) =>
        Matrix.CreateRotationX(MathHelper.PiOver2) * Matrix.CreateTranslation(x, y, z);

    private static Matrix Yaw(float x, float y, float z, float yaw) =>
        Matrix.CreateRotationY(yaw) * Matrix.CreateTranslation(x, y, z);

    private static Matrix Tilt(float x, float y, float z, float yaw, float pitch, float roll) =>
        Matrix.CreateFromYawPitchRoll(yaw, pitch, roll) * Matrix.CreateTranslation(x, y, z);

    private static Matrix Scaled(float x, float y, float z, float scaleX, float scaleY, float scaleZ) =>
        Matrix.CreateScale(scaleX, scaleY, scaleZ) * Matrix.CreateTranslation(x, y, z);

    private sealed class ModelBuilder
    {
        private readonly List<DrModelBone> _bones = new();

        public void Add(DrMesh template, Matrix localTransform, DrMaterial material)
        {
            var part = template.MeshParts[0].Clone();
            part.Material = material;

            var mesh = new DrMesh(part);
            var bone = new DrModelBone($"part{_bones.Count}", mesh)
            {
                DefaultPose = new SrtTransform(localTransform),
            };

            _bones.Add(bone);
        }

        public DrModel Build(string name)
        {
            var root = new DrModelBone("root")
            {
                Children = _bones.ToArray(),
            };

            return new DrModel(root) { Tag = name };
        }
    }
}
