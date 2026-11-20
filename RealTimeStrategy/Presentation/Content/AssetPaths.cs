namespace RealTimeStrategy.Presentation.Content;

/// <summary>
/// Resolves on-disk asset paths used by the client.
/// </summary>
public static class AssetPaths
{
    /// <summary>
    /// Runtime counterpart of the <c>Presentation/Ui</c> source folder (created on startup).
    /// Myra's asset manager is rooted here so the paths authored in <c>.xmmp</c> markup —
    /// for example <c>../../Assets/Fonts/Rajdhani-Regular.ttf:16</c> — resolve at runtime
    /// exactly as they do during <c>myra-export</c>.
    /// </summary>
    public static string UiRoot { get; } = Path.Combine(AppContext.BaseDirectory, "Presentation", "Ui");

    public static string Font(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", fileName);
}
