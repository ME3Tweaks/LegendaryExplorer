using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// A level's lights and the exports they were read from, including disabled lights. Keep this snapshot until
/// the next load so notifications about removed/replaced references still invalidate the old lights.
/// </summary>
public sealed class LevelLightSet
{
    public IReadOnlyList<SceneLight> Lights { get; private set; }
    private readonly HashSet<ExportEntry> Dependencies = [];
    // Imports followed (or that couldn't be resolved), since relinking one changes what it resolves to without changing any export
    private readonly HashSet<ImportEntry> ImportDependencies = [];

    private LevelLightSet() { }

    /// <param name="previewOverride">The level editor's uncommitted edits to a light component, if it has any</param>
    public static LevelLightSet Load(Level level, PackageCache cache, Func<ExportEntry, LightPreviewOverride> previewOverride = null)
    {
        var result = new LevelLightSet();
        result.Lights = SceneLight.LoadLevelLights(level, cache, Track, previewOverride);
        return result;

        void Track(ExportEntry export)
        {
            if (!result.Dependencies.Add(export)) return;
            try
            {
                TrackEntry(export.Archetype);
                TrackEntry(export.Class);
                if (EffectiveLightingProperties.ClassDefault(export, cache) is { } cdo) TrackEntry(cdo);
                // Read each link in its own package; inherited object indices cannot be used in another package.
                var properties = export.GetProperties();
                foreach (string name in new[] { "Function", "SourceMaterial", "Parent" })
                    if (properties.GetProp<ObjectProperty>(name)?.ResolveToEntry(export.FileRef) is { } entry)
                        TrackEntry(entry);
                if (properties.GetProp<ArrayProperty<ObjectProperty>>("Expressions") is { } expressions)
                    foreach (var expression in expressions)
                        if (expression.ResolveToEntry(export.FileRef) is { } entry)
                            TrackEntry(entry);
            }
            catch (Exception e)
            {
                // A missing dependency must not prevent an otherwise readable light from being rendered.
                Debug.WriteLine($"Could not track light dependency {export.InstancedFullPath}: {e.Message}");
            }
        }

        void TrackEntry(IEntry entry)
        {
            if ((entry switch
                {
                    ExportEntry export => export,
                    ImportEntry import when result.ImportDependencies.Add(import) => EntryImporter.ResolveImport(import, cache),
                    _ => null
                }) is { } dependency)
                Track(dependency);
        }
    }

    /// <param name="updatedExports">Updated entries' UIndexes: imports' are negative</param>
    public bool IsAffectedBy(IMEPackage package, IReadOnlySet<int> updatedExports) =>
        Dependencies.Any(export => export.FileRef == package && updatedExports.Contains(export.UIndex))
        || ImportDependencies.Any(import => import.FileRef == package && updatedExports.Contains(import.UIndex));

    /// <summary>
    /// The light read from a component. Null if the component's light is disabled or couldn't be read
    /// </summary>
    public SceneLight Find(ExportEntry lightComponent) => Lights.FirstOrDefault(light => light.Export == lightComponent);

    /// <summary>
    /// Replaces the light read from one component, keeping the others and the tracked dependencies. Either may be null:
    /// a light that becomes disabled is removed, and one that becomes enabled is added
    /// </summary>
    /// <returns>The light that was replaced</returns>
    public SceneLight ReplaceLight(ExportEntry lightComponent, SceneLight replacement)
    {
        var lights = Lights.ToList();
        int index = lights.FindIndex(light => light.Export == lightComponent);
        SceneLight previous = index >= 0 ? lights[index] : null;
        if (index >= 0 && replacement is not null) lights[index] = replacement;
        else if (index >= 0) lights.RemoveAt(index);
        else if (replacement is not null) lights.Add(replacement);
        Lights = lights;
        return previous;
    }
}
