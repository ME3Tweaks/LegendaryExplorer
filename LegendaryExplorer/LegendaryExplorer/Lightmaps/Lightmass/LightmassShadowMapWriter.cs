using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorer.Lightmaps.Codec;
using LegendaryExplorer.Lightmaps.Game;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorer.Lightmaps.Lightmass;

/// <summary>
/// Replaces the shadow maps of a package's baked primitives with Lightmass's (UE3's FStaticMeshStaticLightingTextureMapping::Apply and
/// UShadowMap2D::EncodeTextures, laid out as LE3's are):
/// <list type="bullet">
/// <item>Texture mappings get a ShadowMap2D per light, the same size as the primitive's lightmap allocation, packed into new PF_G8
/// ShadowMapTexture2D atlases (up to 1024). Visibility atlases hold gamma-encoded bytes (SRGB) and are stored in the package (NeverStream);
/// distance field atlases hold linear bytes and go to the mod TFC.</item>
/// <item>Vertex mappings get a ShadowMap1D per light: a visibility per vertex.</item>
/// <item>Each primitive's statically irrelevant lights are updated: the lights that reach it but are in neither its lightmap nor a shadow
/// map. The renderer would otherwise light it with them dynamically, unshadowed.</item>
/// </list>
/// The old shadow maps of those primitives, and atlases nothing uses any more, are trashed. Run it after <see cref="LightmapRewriter"/>,
/// which writes the same components.
/// </summary>
public sealed class LightmassShadowMapWriter(IMEPackage package, TfcWriter tfc, LightmassScene scene, IReadOnlyDictionary<Guid, LightmassMappingResult> results)
{
    public sealed class Summary
    {
        public int TextureShadowMaps, VertexShadowMaps, Atlases, Removed, IrrelevantLightLists;
    }

    /// <summary>Where a 2D shadow map's texels come from: its mapping's result, and the light.</summary>
    sealed record Pending(LightmassMapping Mapping, LightmassMappingResult Result, LightmassShadowMap Shadow, Guid LightGuid, List<LightmapRef> Targets);

    readonly Dictionary<ExportEntry, ObjectBinary> binaries = new();
    readonly Dictionary<ExportEntry, PropertyCollection> properties = new();
    readonly Dictionary<LightmapRef, List<ExportEntry>> newShadowMaps = new();
    /// <summary>Per new export's name, the next free number, and per class the flags new exports copy (both scan every export once).</summary>
    readonly Dictionary<string, int> nextNameNumber = new();
    readonly Dictionary<string, EObjectFlags?> templateFlags = new();

    /// <param name="appliedLightmaps">Only targets whose new lightmaps were successfully applied may replace their shadow data.</param>
    public Summary Write(IReadOnlySet<(ExportEntry Owner, string Slot)> appliedLightmaps)
    {
        var summary = new Summary();
        var lights = scene.Lights.ToDictionary(l => l.Guid);
        var pending = new List<Pending>();
        var targets = new List<(LightmassMapping Mapping, LightmassMappingResult Result, LightmapRef Target)>();
        foreach (var mapping in scene.Mappings)
        {
            if (!results.TryGetValue(mapping.Guid, out var result)) continue;
            var mine = mapping.Targets.Where(t => t.Owner.FileRef == package && appliedLightmaps.Contains((t.Owner, t.Slot)))
                .Select(Fresh).ToList();
            if (mine.Count == 0) continue;
            foreach (var t in mine)
            {
                targets.Add((mapping, result, t));
                newShadowMaps[t] = [];
            }
            foreach (var shadow in result.ShadowMaps)
            {
                if (!lights.TryGetValue(shadow.LightGuid, out var light)) continue;
                if (result.IsTextureMapping) pending.Add(new Pending(mapping, result, shadow, light.Light.LightGuid, mine));
                else
                {
                    foreach (var t in mine) newShadowMaps[t].Add(ShadowMap1D(t.Owner, shadow, light.Light.LightGuid));
                    summary.VertexShadowMaps++;
                }
            }
        }
        if (targets.Count == 0) return summary;

        // The old shadow maps and the atlases they use.
        var oldShadowMaps = new HashSet<ExportEntry>();
        var oldTextures = new Dictionary<LightmapRef, HashSet<int>>();
        foreach (var (_, _, t) in targets)
        {
            oldTextures[t] = [];
            foreach (var s in PackageShadowMaps.Of(t))
            {
                oldShadowMaps.Add(s.Export);
                if (!s.PerVertex && s.Export.GetProperty<ObjectProperty>("Texture") is { Value: > 0 } tex) oldTextures[t].Add(tex.Value);
            }
        }

        var newTextures = WriteAtlases(pending, summary);
        summary.TextureShadowMaps = pending.Count;

        foreach (var (mapping, result, t) in targets)
        {
            SetShadowMaps(t, newShadowMaps[t], result.IsTextureMapping);
            if (UpdateIrrelevantLights(t, mapping, result, lights)) summary.IrrelevantLightLists++;
        }
        foreach (var (export, binary) in binaries) export.WriteBinary(binary);
        foreach (var (export, props) in properties) export.WriteProperties(props);

        // The replaced shadow maps go, and so does any atlas no shadow map uses any more.
        var stillUsed = package.Exports.Where(e => e.ClassName == "ShadowMap2D" && !oldShadowMaps.Contains(e))
            .Select(e => e.GetProperty<ObjectProperty>("Texture")?.Value ?? 0).ToHashSet();
        var unused = oldTextures.Values.SelectMany(u => u).Where(u => !stillUsed.Contains(u)).ToHashSet();

        // Streaming: a distance field atlas inherits the instances of the atlases its primitives' shadow maps were in, and of their lightmaps'.
        InheritStreamingInstances(newTextures, oldTextures, unused);

        var trash = oldShadowMaps.Cast<IEntry>().Concat(unused.Select(u => (IEntry)package.GetUExport(u))).ToList();
        summary.Removed = oldShadowMaps.Count;
        if (trash.Count > 0) EntryPruner.TrashEntries(package, trash);
        return summary;
    }

    /// <summary>The target with the component's current binary (the lightmap rewriter has written it since the scene was built).</summary>
    LightmapRef Fresh(LightmapRef t)
    {
        if (!binaries.TryGetValue(t.Owner, out var binary))
            binaries[t.Owner] = binary = t.Owner.ClassName switch
            {
                "ModelComponent" => ObjectBinary.From<ModelComponent>(t.Owner),
                "TerrainComponent" => ObjectBinary.From<TerrainComponent>(t.Owner),
                _ => ObjectBinary.From<StaticMeshComponent>(t.Owner),
            };
        var lightmap = binary switch
        {
            StaticMeshComponent smc when Index(t.Slot, "LOD") is int lod && lod < smc.LODData.Length => smc.LODData[lod].LightMap,
            ModelComponent mc when Index(t.Slot, "Element") is int e && e < mc.Elements.Length => mc.Elements[e].LightMap,
            TerrainComponent tc => tc.LightMap,
            _ => t.LightMap,
        };
        return t with { Binary = binary, LightMap = lightmap };
    }

    // ================= texture shadow maps =================

    /// <summary>Packs the pending shadow maps into atlases, writes the textures and makes a ShadowMap2D for each. Returns the new textures.</summary>
    List<(ExportEntry Texture, Atlas Atlas)> WriteAtlases(List<Pending> pending, Summary summary)
    {
        var requests = pending.Select(p =>
        {
            bool padded = p.Mapping.Padded && p.Result.SizeX > 2 && p.Result.SizeY > 2;
            int pad = padded ? 1 : 0;
            bool sdf = p.Shadow.SignedDistanceField;
            return new AtlasRequest(p, sdf ? "sdf" : "vis", p.Result.SizeX - 2 * pad, p.Result.SizeY - 2 * pad, Streamed: sdf, Bounds(p.Mapping), pad);
        });
        var created = new List<(ExportEntry, Atlas)>();
        foreach (var atlas in AtlasPacker.Pack(requests))
        {
            bool sdf = atlas.Group == "sdf";
            // Coverage is the lightmap's (a texel on the surface): Lightmass leaves fully shadowed texels out of a shadow map's own.
            var top = atlas.Compose(placement =>
            {
                var p = (Pending)placement.Request.Key;
                var level = MipLevel.Create(p.Result.SizeX, p.Result.SizeY);
                for (int i = 0; i < p.Shadow.Values.Length; i++)
                {
                    byte v = p.Shadow.Values[i];
                    (level.Rgb[i * 3], level.Rgb[i * 3 + 1], level.Rgb[i * 3 + 2]) = (v, v, v);
                    level.Coverage[i] = p.Result.Coverage[i];
                }
                return level;
            });
            // UE3 averages the stored bytes for shadow map mips (UShadowMap2D::EncodeSingleTexture).
            var levels = MipChain.BuildDilated(top, MipAveraging.Bytes, minSize: 1);
            var encoded = levels.Select(l =>
            {
                var g8 = new byte[l.Width * l.Height];
                for (int i = 0; i < g8.Length; i++) g8[i] = l.Rgb[i * 3];
                return g8;
            }).ToList();
            var texture = NewTexture();
            LightmapTextureWriter.Write(texture, "PF_G8", atlas.SizeX, atlas.SizeY, encoded, sdf ? tfc : null, srgb: sdf ? false : null);
            var props = texture.GetProperties();
            props.AddOrReplaceProp(new IntProperty(1, "ShadowmapFlags")); // SMF_Streamed
            if (!sdf) props.AddOrReplaceProp(new BoolProperty(true, "NeverStream"));
            texture.WriteProperties(props);
            created.Add((texture, atlas));
            summary.Atlases++;

            foreach (var placement in atlas.Placements)
            {
                var p = (Pending)placement.Request.Key;
                var (scale, bias) = placement.Coordinates(atlas.SizeX, atlas.SizeY);
                var shadowMap = ShadowMap2D(p.Targets[0].Owner, texture, scale, bias, p.LightGuid, sdf);
                foreach (var t in p.Targets) newShadowMaps[t].Add(shadowMap);
            }
        }
        return created;
    }

    static (Vector3 Center, float Radius)? Bounds(LightmassMapping m)
    {
        var (min, max) = m switch
        {
            LightmassInstance i => (i.BoundsMin, i.BoundsMax),
            LightmassBspMapping b => (b.BoundsMin, b.BoundsMax),
            LightmassTerrainMapping t => (t.BoundsMin, t.BoundsMax),
            _ => (Vector3.Zero, Vector3.Zero),
        };
        return min == max ? null : ((min + max) / 2, Vector3.Distance(min, max) / 2);
    }

    ExportEntry NewTexture()
    {
        var texture = new ExportEntry(package, 0, NextName("ShadowMapTexture2D_LE3LM"),
            binary: new UTexture2D { Mips = [], TextureGuid = Guid.NewGuid() })
        {
            Class = EntryImporter.EnsureClassIsInFile(package, "ShadowMapTexture2D", new RelinkerOptionsPackage()),
        };
        if (TemplateFlags("ShadowMapTexture2D", "LightMapTexture2D") is { } flags) texture.ObjectFlags = flags;
        package.AddExport(texture);
        return texture;
    }

    ExportEntry ShadowMap2D(ExportEntry owner, ExportEntry texture, Vector2 scale, Vector2 bias, Guid lightGuid, bool sdf)
    {
        var props = new PropertyCollection
        {
            new ObjectProperty(texture, "Texture"),
            CommonStructs.Vector2Prop(scale, "CoordinateScale"),
            CommonStructs.Vector2Prop(bias, "CoordinateBias"),
            CommonStructs.GuidProp(lightGuid, "LightGuid"),
        };
        if (sdf) props.Add(new BoolProperty(false, "bIsShadowFactorTexture"));
        return NewObject(owner, "ShadowMap2D", props, null);
    }

    ExportEntry ShadowMap1D(ExportEntry owner, LightmassShadowMap shadow, Guid lightGuid) =>
        NewObject(owner, "ShadowMap1D", null, new ShadowMap1D
        {
            Samples = shadow.Samples.Select(BitConverter.SingleToInt32Bits).ToArray(),
            LightGuid = lightGuid,
        });

    ExportEntry NewObject(ExportEntry owner, string className, PropertyCollection props, ObjectBinary binary)
    {
        var export = new ExportEntry(package, owner, NextName(className), properties: props, binary: binary)
        {
            Class = EntryImporter.EnsureClassIsInFile(package, className, new RelinkerOptionsPackage()),
        };
        if (TemplateFlags(className) is { } flags) export.ObjectFlags = flags;
        package.AddExport(export);
        return export;
    }

    /// <summary>GetNextIndexedName, counting on from the first call instead of rescanning the exports for each new object.</summary>
    NameReference NextName(string name)
    {
        if (!nextNameNumber.TryGetValue(name, out int number)) number = package.GetNextIndexedName(name).Number;
        nextNameNumber[name] = number + 1;
        return new NameReference(name, number);
    }

    /// <summary>The flags of the package's first export of these classes (in order of preference), if it has any.</summary>
    EObjectFlags? TemplateFlags(params string[] classNames)
    {
        string key = string.Join("|", classNames);
        if (!templateFlags.TryGetValue(key, out var flags))
        {
            var template = classNames.Select(c => package.Exports.FirstOrDefault(e => e.ClassName == c)).FirstOrDefault(e => e != null);
            templateFlags[key] = flags = template?.ObjectFlags;
        }
        return flags;
    }

    // ================= components =================

    void SetShadowMaps(LightmapRef t, List<ExportEntry> maps, bool texture)
    {
        int[] twoD = texture ? maps.Select(m => m.UIndex).ToArray() : [];
        int[] oneD = texture ? [] : maps.Select(m => m.UIndex).ToArray();
        switch (t.Binary)
        {
            case StaticMeshComponent smc when Index(t.Slot, "LOD") is int lod && lod < smc.LODData.Length:
                smc.LODData[lod].ShadowMaps = twoD;
                smc.LODData[lod].ShadowVertexBuffers = oneD;
                break;
            case ModelComponent mc when Index(t.Slot, "Element") is int e && e < mc.Elements.Length:
                mc.Elements[e].ShadowMaps = twoD;
                break;
            case TerrainComponent:
                var props = Properties(t.Owner);
                var array = new ArrayProperty<ObjectProperty>(twoD.Select(u => new ObjectProperty(u)), "ShadowMaps");
                if (array.Count > 0) props.AddOrReplaceProp(array);
                else props.RemoveNamedProperty("ShadowMaps");
                break;
        }
    }

    /// <summary>
    /// UE3's IrrelevantLights (FStaticMeshStaticLightingTextureMapping::Apply): the lights that reach the primitive but are in neither its
    /// lightmap nor its shadow maps. Static meshes keep one list for all LODs, so only LOD 0 sets it. Lights already listed stay listed
    /// unless the bake put them in the lightmap or a shadow map.
    /// </summary>
    bool UpdateIrrelevantLights(LightmapRef t, LightmassMapping mapping, LightmassMappingResult result, Dictionary<Guid, LightmassLight> lights)
    {
        var relevant = (mapping switch
        {
            LightmassInstance i => i.RelevantLights,
            LightmassBspMapping b => b.RelevantLights,
            LightmassTerrainMapping m => m.RelevantLights,
            _ => [],
        }).Select(l => l.Light.LightGuid);
        var used = result.LightGuids.Concat(result.ShadowMaps.Select(s => s.LightGuid))
            .Select(g => lights.TryGetValue(g, out var l) ? l.Light.LightGuid : Guid.Empty).ToHashSet();
        IEnumerable<Guid> Update(IEnumerable<Guid> old) => old.Concat(relevant).Where(g => !used.Contains(g)).Distinct();

        switch (t.Binary)
        {
            case StaticMeshComponent when t.Slot == "LOD0":
            case TerrainComponent:
            {
                var props = Properties(t.Owner);
                var old = props.GetProp<ArrayProperty<StructProperty>>("IrrelevantLights")?.Select(CommonStructs.GetGuid) ?? [];
                var array = new ArrayProperty<StructProperty>(Update(old).Select(g => CommonStructs.GuidProp(g)), "IrrelevantLights");
                if (array.Count > 0) props.AddOrReplaceProp(array);
                else props.RemoveNamedProperty("IrrelevantLights");
                return true;
            }
            case ModelComponent mc when Index(t.Slot, "Element") is int e && e < mc.Elements.Length:
                mc.Elements[e].IrrelevantLights = Update(mc.Elements[e].IrrelevantLights ?? []).ToArray();
                return true;
            default:
                return false;
        }
    }

    PropertyCollection Properties(ExportEntry export) => properties.TryGetValue(export, out var p) ? p : properties[export] = export.GetProperties();

    static int? Index(string slot, string prefix) => slot.StartsWith(prefix) && int.TryParse(slot.AsSpan(prefix.Length), out int i) ? i : null;

    /// <summary>The level's texture streaming map: each new streamed atlas gets the instances of the textures its primitives used.</summary>
    void InheritStreamingInstances(List<(ExportEntry Texture, Atlas Atlas)> created, Dictionary<LightmapRef, HashSet<int>> oldTextures, HashSet<int> unused)
    {
        var levelExport = package.Exports.FirstOrDefault(e => e.ClassName == "Level" && e.ObjectName.Name == "PersistentLevel");
        if (levelExport == null) return;
        var level = ObjectBinary.From<Level>(levelExport);
        var map = level.TextureToInstancesMap;
        bool changed = false;
        foreach (var (texture, atlas) in created.Where(c => c.Atlas.Streamed))
        {
            var owners = atlas.Placements.SelectMany(p => ((Pending)p.Request.Key).Targets).ToHashSet();
            var from = new HashSet<int>();
            foreach (var t in owners)
            {
                from.UnionWith(oldTextures[t]);
                if (t.LightMap is LightMap_2D d) from.Add(d.Texture1);
                else if (t.LightMap is LightMap_4or6 v) from.Add(v.Texture1);
            }
            var instances = map.Where(kv => from.Contains(kv.Key)).SelectMany(kv => kv.Value.Instances)
                .GroupBy(i => (i.BoundingSphere.Center, i.BoundingSphere.W, i.TexelFactor)).Select(g => g.First()).ToArray();
            if (instances.Length == 0) continue;
            map[texture.UIndex] = new StreamableTextureInstanceList { Instances = instances };
            changed = true;
        }
        foreach (int u in unused)
            changed |= map.Remove(u);
        if (changed) levelExport.WriteBinary(level);
    }
}
