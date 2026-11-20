using DigitalRiseModel;
using DigitalRiseModel.Primitives;
using RealTimeStrategy.Presentation.Rendering;
using Xunit;

namespace RealTimeStrategy.Tests.Rendering;

public sealed class MeshCacheTests
{
    [Fact]
    public void Equal_keys_return_the_same_mesh()
    {
        var created = 0;
        using var cache = new MeshCache(_ =>
        {
            created++;
            return new TrackingMesh();
        });

        var key = new MeshKey { Kind = MeshPrimitiveKind.Box, SizeX = 1f, SizeY = 2f, SizeZ = 3f };
        var first = cache.Get(key);
        var second = cache.Get(key);

        Assert.Same(first, second);
        Assert.Equal(1, created);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Different_keys_return_different_meshes()
    {
        var created = 0;
        using var cache = new MeshCache(_ =>
        {
            created++;
            return new TrackingMesh();
        });

        var small = cache.Get(new MeshKey { Kind = MeshPrimitiveKind.Box, SizeX = 1f, SizeY = 1f, SizeZ = 1f });
        var large = cache.Get(new MeshKey { Kind = MeshPrimitiveKind.Box, SizeX = 2f, SizeY = 1f, SizeZ = 1f });

        Assert.NotSame(small, large);
        Assert.Equal(2, created);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void Dispose_disposes_every_cached_mesh_and_clears_the_cache()
    {
        var meshes = new List<TrackingMesh>();
        var cache = new MeshCache(_ =>
        {
            var mesh = new TrackingMesh();
            meshes.Add(mesh);
            return mesh;
        });

        cache.Get(new MeshKey { Kind = MeshPrimitiveKind.Box });
        cache.Get(new MeshKey { Kind = MeshPrimitiveKind.Sphere, Radius = 1f });
        cache.Get(new MeshKey { Kind = MeshPrimitiveKind.Plane, NormalDirection = NormalDirection.UpY });

        cache.Dispose();

        Assert.Equal(3, meshes.Count);
        Assert.All(meshes, mesh => Assert.Equal(1, mesh.DisposeCount));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var mesh = new TrackingMesh();
        var cache = new MeshCache(_ => mesh);
        cache.Get(new MeshKey { Kind = MeshPrimitiveKind.Cylinder });

        cache.Dispose();
        cache.Dispose();

        Assert.Equal(1, mesh.DisposeCount);
    }

    [Fact]
    public void Get_after_dispose_throws()
    {
        var cache = new MeshCache(_ => new TrackingMesh());
        cache.Dispose();

        Assert.Throws<ObjectDisposedException>(() => cache.Get(new MeshKey { Kind = MeshPrimitiveKind.Box }));
    }

    [Fact]
    public void Null_factory_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MeshCache(null!));
    }

    [Fact]
    public void Key_includes_every_shape_parameter()
    {
        var key = new MeshKey
        {
            Kind = MeshPrimitiveKind.Plane,
            SizeX = 10f,
            SizeY = 20f,
            TessellationX = 2,
            TessellationY = 3,
            NormalDirection = NormalDirection.UpY,
        };

        Assert.Equal(key, key with { });
        Assert.NotEqual(key, key with { Kind = MeshPrimitiveKind.Box });
        Assert.NotEqual(key, key with { SizeX = 11f });
        Assert.NotEqual(key, key with { SizeY = 21f });
        Assert.NotEqual(key, key with { TessellationX = 4 });
        Assert.NotEqual(key, key with { TessellationY = 4 });
        Assert.NotEqual(key, key with { NormalDirection = NormalDirection.UpZ });
        Assert.NotEqual(key, key with { UScale = 2f });
        Assert.NotEqual(key, key with { VScale = 2f });
        Assert.NotEqual(key, key with { Capped = false });
        Assert.NotEqual(key, key with { ToLeftHanded = true });
        Assert.NotEqual(key, key with { GenerateBackFace = true });
    }

    [Fact]
    public void Key_defaults_keep_uv_scales_and_plane_tessellation_neutral()
    {
        var key = new MeshKey { Kind = MeshPrimitiveKind.Box };

        Assert.Equal(1f, key.UScale);
        Assert.Equal(1f, key.VScale);
        Assert.Equal(1, key.TessellationX);
        Assert.Equal(1, key.TessellationY);
        Assert.True(key.Capped);
        Assert.False(key.ToLeftHanded);
        Assert.False(key.GenerateBackFace);
    }

    private sealed class TrackingMesh : DrMesh
    {
        public int DisposeCount { get; private set; }

        public override void Dispose(bool disposing)
        {
            DisposeCount++;
            base.Dispose(disposing);
        }
    }
}
