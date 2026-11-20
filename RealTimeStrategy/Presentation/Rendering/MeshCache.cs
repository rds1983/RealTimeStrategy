using DigitalRiseModel;

namespace RealTimeStrategy.Presentation.Rendering;

/// <summary>
/// Keyed cache for disposable meshes. The factory is injected so caching and disposal can be
/// tested without a graphics device.
/// </summary>
public sealed class MeshCache : IDisposable
{
    private readonly Func<MeshKey, DrMesh> _factory;
    private readonly Dictionary<MeshKey, DrMesh> _meshes = new();

    private bool _disposed;

    public MeshCache(Func<MeshKey, DrMesh> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public int Count => _meshes.Count;

    public DrMesh Get(MeshKey key)
    {
        ThrowIfDisposed();

        if (!_meshes.TryGetValue(key, out var mesh))
        {
            mesh = _factory(key) ?? throw new InvalidOperationException($"Mesh factory returned null for {key}.");
            _meshes.Add(key, mesh);
        }

        return mesh;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var mesh in _meshes.Values)
        {
            mesh.Dispose();
        }

        _meshes.Clear();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MeshCache));
        }
    }
}
