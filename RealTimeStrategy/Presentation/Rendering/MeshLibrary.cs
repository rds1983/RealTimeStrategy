using DigitalRiseModel;
using DigitalRiseModel.Primitives;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace RealTimeStrategy.Presentation.Rendering;

/// <summary>
/// Creates and caches procedural primitives from DigitalRiseModel. Every shape shares buffers
/// between callers that request the same parameters.
/// </summary>
public sealed class MeshLibrary : IDisposable
{
    private readonly MeshCache _cache;

    public MeshLibrary(GraphicsDevice device)
    {
        if (device is null)
        {
            throw new ArgumentNullException(nameof(device));
        }

        _cache = new MeshCache(key => CreateMesh(device, key));
    }

    public int Count => _cache.Count;

    public DrMesh Get(MeshKey key) => _cache.Get(key);

    public DrMesh GetBox(
        Vector3 size,
        float uScale = 1f,
        float vScale = 1f,
        bool toLeftHanded = false)
    {
        return Get(new MeshKey
        {
            Kind = MeshPrimitiveKind.Box,
            SizeX = size.X,
            SizeY = size.Y,
            SizeZ = size.Z,
            UScale = uScale,
            VScale = vScale,
            ToLeftHanded = toLeftHanded,
        });
    }

    public DrMesh GetSphere(
        float radius = 0.5f,
        int tessellation = 16,
        float uScale = 1f,
        float vScale = 1f,
        bool toLeftHanded = false)
    {
        return Get(new MeshKey
        {
            Kind = MeshPrimitiveKind.Sphere,
            Radius = radius,
            Tessellation = tessellation,
            UScale = uScale,
            VScale = vScale,
            ToLeftHanded = toLeftHanded,
        });
    }

    public DrMesh GetCylinder(
        float height = 1f,
        float radius = 0.5f,
        int tessellation = 32,
        bool capped = true,
        float uScale = 1f,
        float vScale = 1f,
        bool toLeftHanded = false)
    {
        return Get(new MeshKey
        {
            Kind = MeshPrimitiveKind.Cylinder,
            Height = height,
            Radius = radius,
            Tessellation = tessellation,
            Capped = capped,
            UScale = uScale,
            VScale = vScale,
            ToLeftHanded = toLeftHanded,
        });
    }

    public DrMesh GetCone(
        float radius = 0.5f,
        float height = 1f,
        int tessellation = 16,
        float uScale = 1f,
        float vScale = 1f,
        bool toLeftHanded = false)
    {
        return Get(new MeshKey
        {
            Kind = MeshPrimitiveKind.Cone,
            Radius = radius,
            Height = height,
            Tessellation = tessellation,
            UScale = uScale,
            VScale = vScale,
            ToLeftHanded = toLeftHanded,
        });
    }

    public DrMesh GetCapsule(
        float length = 1f,
        float radius = 0.5f,
        int tessellation = 8,
        float uScale = 1f,
        float vScale = 1f,
        bool toLeftHanded = false)
    {
        return Get(new MeshKey
        {
            Kind = MeshPrimitiveKind.Capsule,
            Length = length,
            Radius = radius,
            Tessellation = tessellation,
            UScale = uScale,
            VScale = vScale,
            ToLeftHanded = toLeftHanded,
        });
    }

    public DrMesh GetTorus(
        float majorRadius = 0.5f,
        float minorRadius = 0.16666f,
        int tessellation = 32,
        float uScale = 1f,
        float vScale = 1f,
        bool toLeftHanded = false)
    {
        return Get(new MeshKey
        {
            Kind = MeshPrimitiveKind.Torus,
            MajorRadius = majorRadius,
            MinorRadius = minorRadius,
            Tessellation = tessellation,
            UScale = uScale,
            VScale = vScale,
            ToLeftHanded = toLeftHanded,
        });
    }

    public DrMesh GetDisc(
        float radius = 0.5f,
        float sectorAngle = MathF.PI * 2f,
        int tessellation = 16,
        float uScale = 1f,
        float vScale = 1f,
        bool toLeftHanded = false)
    {
        return Get(new MeshKey
        {
            Kind = MeshPrimitiveKind.Disc,
            Radius = radius,
            SectorAngle = sectorAngle,
            Tessellation = tessellation,
            UScale = uScale,
            VScale = vScale,
            ToLeftHanded = toLeftHanded,
        });
    }

    /// <summary>
    /// Creates a plane. The default normal points up (Y) because RTS ground is horizontal.
    /// </summary>
    public DrMesh GetPlane(
        Vector2 size,
        NormalDirection normalDirection = NormalDirection.UpY,
        int tessellationX = 1,
        int tessellationY = 1,
        float uScale = 1f,
        float vScale = 1f,
        bool generateBackFace = false,
        bool toLeftHanded = false)
    {
        return Get(new MeshKey
        {
            Kind = MeshPrimitiveKind.Plane,
            SizeX = size.X,
            SizeY = size.Y,
            TessellationX = tessellationX,
            TessellationY = tessellationY,
            UScale = uScale,
            VScale = vScale,
            GenerateBackFace = generateBackFace,
            ToLeftHanded = toLeftHanded,
            NormalDirection = normalDirection,
        });
    }

    public void Dispose() => _cache.Dispose();

    private static DrMesh CreateMesh(GraphicsDevice device, MeshKey key)
    {
        return key.Kind switch
        {
            MeshPrimitiveKind.Box => MeshPrimitives.CreateBoxMesh(
                device,
                new Vector3(key.SizeX, key.SizeY, key.SizeZ),
                uScale: key.UScale,
                vScale: key.VScale,
                toLeftHanded: key.ToLeftHanded),

            MeshPrimitiveKind.Sphere => MeshPrimitives.CreateSphereMesh(
                device,
                radius: key.Radius,
                tessellation: key.Tessellation,
                uScale: key.UScale,
                vScale: key.VScale,
                toLeftHanded: key.ToLeftHanded),

            MeshPrimitiveKind.Cylinder => MeshPrimitives.CreateCylinderMesh(
                device,
                height: key.Height,
                radius: key.Radius,
                tessellation: key.Tessellation,
                uScale: key.UScale,
                vScale: key.VScale,
                capped: key.Capped,
                toLeftHanded: key.ToLeftHanded),

            MeshPrimitiveKind.Cone => MeshPrimitives.CreateConeMesh(
                device,
                radius: key.Radius,
                height: key.Height,
                tessellation: key.Tessellation,
                uScale: key.UScale,
                vScale: key.VScale,
                toLeftHanded: key.ToLeftHanded),

            MeshPrimitiveKind.Capsule => MeshPrimitives.CreateCapsuleMesh(
                device,
                length: key.Length,
                radius: key.Radius,
                tessellation: key.Tessellation,
                uScale: key.UScale,
                vScale: key.VScale,
                toLeftHanded: key.ToLeftHanded),

            MeshPrimitiveKind.Torus => MeshPrimitives.CreateTorusMesh(
                device,
                majorRadius: key.MajorRadius,
                minorRadius: key.MinorRadius,
                tessellation: key.Tessellation,
                uScale: key.UScale,
                vScale: key.VScale,
                toLeftHanded: key.ToLeftHanded),

            MeshPrimitiveKind.Disc => MeshPrimitives.CreateDiscMesh(
                device,
                radius: key.Radius,
                sectorAngle: key.SectorAngle,
                tessellation: key.Tessellation,
                uScale: key.UScale,
                vScale: key.VScale,
                toLeftHanded: key.ToLeftHanded),

            MeshPrimitiveKind.Plane => MeshPrimitives.CreatePlaneMesh(
                device,
                sizeX: key.SizeX,
                sizeY: key.SizeY,
                tessellationX: key.TessellationX,
                tessellationY: key.TessellationY,
                uScale: key.UScale,
                vScale: key.VScale,
                generateBackFace: key.GenerateBackFace,
                toLeftHanded: key.ToLeftHanded,
                normalDirection: key.NormalDirection),

            _ => throw new ArgumentOutOfRangeException(nameof(key), key.Kind, "Unknown primitive kind."),
        };
    }
}
