using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Instance = LegendaryExplorer.Tools.LevelEditor.TextureStreamingInstances.Instance;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// Keeps a level's TextureToInstancesMap, which tells the game where in the level each texture is drawn and so how much of it to stream in, up to date with a
/// commit, as UE3's editor rebuilds it on save (ULevel::BuildStreamingData). The instances the commit's static mesh components had before it are taken out
/// and those the written ones have after it put in (<see cref="TextureStreamingInstances"/>, which reproduces shipping levels' instances). The rest of the map
/// is left as it is: skeletal meshes', BSP's and terrain's, and the few static meshes' it can't reproduce (some moved by matinee)
/// </summary>
public sealed class TextureStreamingUpdate
{
    private readonly ExportEntry level;
    private readonly TextureStreamingInstances instances;
    private readonly List<Instance> before;

    private TextureStreamingUpdate(ExportEntry level, TextureStreamingInstances instances, List<Instance> before)
    {
        this.level = level;
        this.instances = instances;
        this.before = before;
    }

    /// <summary>Notes the instances <paramref name="components"/>, the level's package's components a commit will change or delete, have now</summary>
    public static TextureStreamingUpdate Begin(ExportEntry level, IEnumerable<ExportEntry> components, PackageCache cache)
    {
        var instances = new TextureStreamingInstances(cache);
        return new TextureStreamingUpdate(level, instances, [.. StaticMeshes(components).SelectMany(c => Of(instances, c, "its old instances stay in the map"))]);
    }

    /// <summary>Replaces the instances noted by <see cref="Begin"/> with those <paramref name="components"/>, the written ones, have now</summary>
    public void Complete(IEnumerable<ExportEntry> components)
    {
        var after = StaticMeshes(components).SelectMany(c => Of(instances, c, "it has no instances in the map")).ToList();
        if (before.Count == 0 && after.Count == 0) return;
        var package = level.FileRef;
        var binary = level.GetBinaryData<Level>();
        var map = binary.TextureToInstancesMap;
        bool changed = false;
        foreach (var old in before)
        {
            if (KeyFor(package, old.Texture, add: false) is not { } key || !map.TryGetValue(key.UIndex, out var list)) continue;
            int index = Array.FindIndex(list.Instances, i => Same(i, old));
            if (index < 0) continue;
            list.Instances = [.. list.Instances[..index], .. list.Instances[(index + 1)..]];
            if (list.Instances.Length == 0) map.Remove(key.UIndex);
            changed = true;
        }
        foreach (var group in after.GroupBy(i => KeyFor(package, i.Texture, add: true)))
        {
            // (one the level can't refer to has no instances: the game streams it as it would any texture that has none)
            if (group.Key is not { } key) continue;
            var added = group.Select(i => new StreamableTextureInstance { BoundingSphere = new Sphere { Center = i.Center, W = i.Radius }, TexelFactor = i.TexelFactor }).ToArray();
            var others = map.TryGetValue(key.UIndex, out var list) ? list.Instances : [];
            if (group.Any(i => i.Lighting)) ClampLightingFactors(added, others);
            if (list is null) map.Add(key.UIndex, new StreamableTextureInstanceList { Instances = added });
            else list.Instances = [.. others, .. added];
            changed = true;
        }
        if (changed) level.WriteBinary(binary);
    }

    private static IEnumerable<ExportEntry> StaticMeshes(IEnumerable<ExportEntry> components) => components.Where(c => c.IsA("StaticMeshComponent")).Distinct();

    /// <summary>
    /// The component's instances, or none if they can't be worked out, which is logged: the map is the game's guide to streaming, and a commit shouldn't
    /// fail over it (the game streams a texture with no instances as it's drawn)
    /// </summary>
    private static List<Instance> Of(TextureStreamingInstances instances, ExportEntry component, string consequence)
    {
        try { return instances.OfStaticMesh(component); }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            LECLog.Warning($"Couldn't work out where {component.InstancedFullPath} draws its textures, so {consequence}: {e.Message}");
            return [];
        }
    }

    // What Begin noted is what the package had, computed the same way, so it's the same values; a little slack for a shipped one BioWare's tools rounded
    private static bool Same(StreamableTextureInstance shipped, Instance noted) =>
        (shipped.BoundingSphere.Center - noted.Center).Length() < 0.05f && MathF.Abs(shipped.BoundingSphere.W - noted.Radius) <= 1e-3f * noted.Radius + 0.01f;

    /// <summary>
    /// The entry of <paramref name="package"/> that its map keys <paramref name="texture"/> by: the texture itself, or the one the package has at the path the
    /// game has the texture at in memory (an import, as the game's own levels' maps have) or at its own path (a copy). If it has neither and
    /// <paramref name="add"/>, a new import, for a texture in a file the game has loaded with the package; otherwise null
    /// </summary>
    public static IEntry KeyFor(IMEPackage package, ExportEntry texture, bool add)
    {
        if (texture.FileRef == package) return texture;
        if ((package.FindEntry(texture.MemoryFullPath, texture.ClassName) ?? package.FindEntry(texture.InstancedFullPath, texture.ClassName)) is { } existing) return existing;
        return add && EntryImporter.IsSafeToImportFrom(texture.FileRef.FilePath, package) ? AssetPorting.ImportOf(texture, package) : null;
    }

    /// <summary>
    /// BuildStreamingData's clamp of a lightmap or shadow map texture's texel factors, so that very low or high resolution charts don't decide its streaming.
    /// The instances already in the map have been clamped to their 20th and 80th percentile values, and their unclamped values are gone, so the new ones are
    /// clamped to the range they span. A texture with none yet has its new ones clamped to their own percentile values
    /// </summary>
    private static void ClampLightingFactors(StreamableTextureInstance[] added, StreamableTextureInstance[] others)
    {
        float min, max;
        if (others.Length > 0)
        {
            min = others.Min(i => i.TexelFactor);
            max = others.Max(i => i.TexelFactor);
        }
        else
        {
            var sorted = added.Select(i => i.TexelFactor).Order().ToArray();
            (min, max) = (sorted[(int)(sorted.Length * 0.2f)], sorted[(int)(sorted.Length * 0.8f)]);
        }
        foreach (var instance in added) instance.TexelFactor = Math.Clamp(instance.TexelFactor, min, max);
    }
}
