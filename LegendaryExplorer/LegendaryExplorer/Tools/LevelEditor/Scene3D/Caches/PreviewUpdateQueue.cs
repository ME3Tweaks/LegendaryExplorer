using System.Collections.Generic;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>Coalesces notifications until the UI can safely update rendering resources between frames.</summary>
public sealed class PreviewUpdateQueue
{
    private readonly Dictionary<IMEPackage, HashSet<int>> Updates = [];
    public bool HasUpdates => Updates.Count != 0;

    public void Add(IMEPackage package, IEnumerable<PackageUpdate> updates)
    {
        foreach (var update in updates)
        {
            if ((update.Change & PackageChange.Entry) == 0 || update.Index == 0) continue;
            if (!Updates.TryGetValue(package, out var indices))
                Updates.Add(package, indices = []);
            indices.Add(update.Index);
        }
    }

    public Dictionary<IMEPackage, HashSet<int>> Drain()
    {
        var result = new Dictionary<IMEPackage, HashSet<int>>(Updates);
        Updates.Clear();
        return result;
    }

    public void Clear() => Updates.Clear();
}

/// <summary>
/// Which mesh previews a resource invalidation requires rebuilding
/// </summary>
public enum PreviewRebuild
{
    None,
    /// <summary>
    /// The light environments were forgotten, so the meshes that joined them must rejoin
    /// </summary>
    LightEnvironmentMeshes,
    /// <summary>
    /// The shader caches were released
    /// </summary>
    AllMeshes,
}
