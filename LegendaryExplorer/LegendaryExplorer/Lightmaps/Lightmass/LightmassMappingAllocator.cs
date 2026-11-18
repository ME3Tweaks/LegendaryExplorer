using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorer.Lightmaps.Codec;
using LegendaryExplorer.Lightmaps.Game;
using LegendaryExplorer.Tools.LevelEditor;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Color = LegendaryExplorerCore.SharpDX.Color;

namespace LegendaryExplorer.Lightmaps.Lightmass;

/// <summary>Creates standard UE3 directional lightmaps for newly allocated mappings, after successful solves only.</summary>
public static class LightmassMappingAllocator
{
    /// <summary>
    /// Resolution zero selects vertex lighting. Texture resolutions include the padding border. An unset LightMapResolution is
    /// UStaticMesh's intrinsic 0 in LE1 and LE2, whose bakes vertex-light those meshes; LE3's bakes texture-map many of them.
    /// </summary>
    public static (int Width, int Height) MeshSize(MEGame game, PropertyCollection component, PropertyCollection mesh, int lod, bool hasUv)
    {
        int resolution = component.GetProp<BoolProperty>("bOverrideLightMapRes") is { Value: true }
            ? component.GetProp<IntProperty>("OverriddenLightMapRes")?.Value ?? 0
            : mesh.GetProp<IntProperty>("LightMapResolution")?.Value ?? (game is MEGame.LE3 ? 32 : 0);
        if (resolution <= 0 || !hasUv) return (0, 0);
        int size = Math.Clamp(resolution >> Math.Min(lod, 10), 4, 2048);
        size = (size + 3) & ~3;
        return (size, size);
    }

    /// <summary>
    /// Whether a solved new mesh lightmap is written. In LE1 and LE2 it must hold some light's direct lighting (every vanilla lightmap lists
    /// one) or an LE1 GI-only light's bounce; otherwise the primitive gets only its shadow maps, as their bakes have it. The solve, not the
    /// vanilla data, decides which lights reach it. LE3, BSP and terrain keep bounce-only lightmaps.
    /// </summary>
    /// <param name="directLights">The lights whose direct lighting the result holds</param>
    public static bool KeepsNewLightmap(MEGame game, LightmassMapping mapping, IReadOnlyCollection<Guid> directLights) =>
        game is MEGame.LE3 || mapping is not LightmassInstance instance || directLights.Count > 0 || instance.RelevantLights.Any(l => l.IndirectOnly);

    public static bool IsComplete(LightmassMapping mapping, LightmassMappingResult result)
    {
        int count = mapping.Cost;
        return count > 0 && result.IsTextureMapping == mapping.IsTextureMapping
            && (result.IsTextureMapping ? result.SizeX == mapping.SizeX && result.SizeY == mapping.SizeY : result.SizeX == count && result.SizeY == 1)
            && result.Coverage?.Length == count && result.Coverage.Any(c => c != 0)
            && result.Directional?.Length == count && result.Simple?.Length == count
            && result.Directional.All(s => Finite(s.E0) && Finite(s.E1) && Finite(s.E2)) && result.Simple.All(Finite)
            && result.ShadowMaps.All(s => result.IsTextureMapping ? s.Values?.Length == count
                : s.Samples?.Length == count && s.Samples.All(float.IsFinite));
    }

    static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    /// <summary>New texture mappings need finite, normalized, nondegenerate triangles with no overlapping interiors.</summary>
    public static bool HasUsableUv(StaticMeshRenderData lod, int channel)
    {
        if (channel < 0 || channel >= lod.VertexBuffer.NumTexCoords || lod.VertexBuffer.VertexData is not { Length: > 0 } vertices) return false;
        var uv = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            if (lod.VertexBuffer.bUseFullPrecisionUVs)
            {
                if (vertices[i].FullPrecisionUVs == null || channel >= vertices[i].FullPrecisionUVs.Length) return false;
                uv[i] = vertices[i].FullPrecisionUVs[channel];
            }
            else
            {
                if (vertices[i].HalfPrecisionUVs == null || channel >= vertices[i].HalfPrecisionUVs.Length) return false;
                var value = vertices[i].HalfPrecisionUVs[channel]; uv[i] = new Vector2(value.X, value.Y);
            }
            if (!float.IsFinite(uv[i].X + uv[i].Y) || uv[i].X < -0.0001f || uv[i].Y < -0.0001f || uv[i].X > 1.0001f || uv[i].Y > 1.0001f) return false;
        }
        if (lod.IndexBuffer.Length == 0 || lod.IndexBuffer.Length % 3 != 0 || lod.IndexBuffer.Any(i => i >= uv.Length)) return false;
        var triangles = new List<UvTriangle>(lod.IndexBuffer.Length / 3);
        for (int i = 0; i < lod.IndexBuffer.Length; i += 3)
        {
            int a = lod.IndexBuffer[i], b = lod.IndexBuffer[i + 1], c = lod.IndexBuffer[i + 2];
            if (Math.Abs(UvCross(uv[a], uv[b], uv[c])) <= 1e-8) return false;
            triangles.Add(new UvTriangle(uv[a], uv[b], uv[c]));
        }
        // Sweep bounding boxes first so separated charts do not need triangle intersection tests.
        triangles.Sort((a, b) => a.Min.X.CompareTo(b.Min.X));
        var active = new List<UvTriangle>();
        foreach (var triangle in triangles)
        {
            active.RemoveAll(other => other.Max.X <= triangle.Min.X);
            foreach (var other in active)
            {
                if (other.Max.Y <= triangle.Min.Y || triangle.Max.Y <= other.Min.Y) continue;
                if (!HasSeparatingEdge(triangle, other) && !HasSeparatingEdge(other, triangle)) return false;
            }
            active.Add(triangle);
        }
        return true;
    }

    readonly record struct UvTriangle(Vector2 A, Vector2 B, Vector2 C)
    {
        public Vector2 Min { get; } = Vector2.Min(A, Vector2.Min(B, C));
        public Vector2 Max { get; } = Vector2.Max(A, Vector2.Max(B, C));
    }

    static double UvCross(Vector2 a, Vector2 b, Vector2 c) =>
        ((double)b.X - a.X) * ((double)c.Y - a.Y) - ((double)b.Y - a.Y) * ((double)c.X - a.X);

    static bool HasSeparatingEdge(UvTriangle triangle, UvTriangle other)
    {
        // Equality allows shared edges/vertices, but not any positive-area intersection.
        bool Separates(Vector2 a, Vector2 b, Vector2 c) => UvCross(a, b, c) > 0
            ? UvCross(a, b, other.A) <= 0 && UvCross(a, b, other.B) <= 0 && UvCross(a, b, other.C) <= 0
            : UvCross(a, b, other.A) >= 0 && UvCross(a, b, other.B) >= 0 && UvCross(a, b, other.C) >= 0;
        return Separates(triangle.A, triangle.B, triangle.C) || Separates(triangle.B, triangle.C, triangle.A)
            || Separates(triangle.C, triangle.A, triangle.B);
    }

    /// <param name="tfc">Where new static mesh lightmaps stream from; null stores every new lightmap in the package</param>
    /// <param name="cache">Resolves the components' meshes and materials for the level's texture streaming map</param>
    public static void Write(IMEPackage package, TfcWriter tfc, LightmassScene scene,
        IReadOnlyDictionary<Guid, LightmassMappingResult> results, LightmapRewriter.Summary summary, PackageCache cache)
    {
        var candidates = scene.Mappings.Where(m => m.AllocateLightmap && m.Targets.Any(t => t.Owner.FileRef == package)).ToArray();
        var mappings = candidates.Where(m => results.TryGetValue(m.Guid, out var r) && IsComplete(m, r)).ToArray();
        summary.SkippedNewMappings += candidates.Length - mappings.Length;
        var binaries = new Dictionary<ExportEntry, ObjectBinary>();
        Guid[] Guids(LightmassMappingResult result) => result.LightGuids
            .Select(g => scene.Lights.FirstOrDefault(l => l.Guid == g && !l.ShadowOnly && !l.IndirectOnly))
            .Where(l => l != null).Select(l => l.Light.LightmapGuid).Distinct().ToArray();
        // Dominant lights' direct lighting is never baked (their entry brings bounce and shadow maps)
        Guid[] Direct(LightmassMappingResult result) => result.LightGuids
            .Where(g => scene.Lights.Any(l => l.Guid == g && !l.ShadowOnly && !l.IndirectOnly && !l.Light.IsDominant)).ToArray();
        var shadowOnly = mappings.Where(m => !KeepsNewLightmap(package.Game, m, Direct(results[m.Guid]))).ToArray();
        foreach (var target in shadowOnly.SelectMany(m => m.Targets).Where(t => t.Owner.FileRef == package))
        {
            // A LOD without a record gets one, without a lightmap, to hold the solve's shadow maps
            if (target.LightMap == null)
            {
                if (!binaries.TryGetValue(target.Owner, out var binary))
                    binaries[target.Owner] = binary = ObjectBinary.From(target.Owner);
                EnsureLods((StaticMeshComponent)binary, int.Parse(target.Slot.AsSpan(3)) + 1);
            }
            summary.ShadowOnlyAllocations.Add((target.Owner, target.Slot));
        }
        mappings = mappings.Except(shadowOnly).ToArray();

        void Apply(LightmassMapping mapping, Func<LightmapRef, LightMap> create)
        {
            foreach (var target in mapping.Targets.Where(t => t.Owner.FileRef == package))
            {
                if (!binaries.TryGetValue(target.Owner, out var binary))
                    binaries[target.Owner] = binary = ObjectBinary.From(target.Owner);
                var map = create(target);
                switch (binary)
                {
                    case StaticMeshComponent smc:
                        int lod = int.Parse(target.Slot.AsSpan(3));
                        EnsureLods(smc, lod + 1);
                        smc.LODData[lod].LightMap = map;
                        break;
                    case TerrainComponent terrain: terrain.LightMap = map; break;
                    case ModelComponent model: model.Elements[int.Parse(target.Slot.AsSpan(7))].LightMap = map; break;
                    default: throw new NotSupportedException(target.Describe());
                }
                summary.AppliedLightmaps.Add((target.Owner, target.Slot));
                summary.AllocatedLightmaps++;
            }
        }

        foreach (var mapping in mappings.Where(m => !m.IsTextureMapping))
        {
            var result = results[mapping.Guid];
            var split = result.Directional.Select(s => Directional.Split(s)).ToArray();
            var s1 = Quantization.ChooseScale(split.Select(s => s.Colour));
            var s2 = Quantization.ChooseScale(split.Select(s => s.MaxComponents));
            var s3 = Quantization.ChooseScale(result.Simple);
            Apply(mapping, target => new LightMap_1D
            {
                LightMapType = ELightMapType.LMT_1D, Owner = target.Owner.UIndex, LightGuids = Guids(result),
                ScaleVector1 = s1, ScaleVector2 = s2, ScaleVector3 = s3,
                DirectionalSamples = split.Select(s => new QuantizedDirectionalLightSample
                {
                    Coefficient2 = Encode(s.Colour, s1), Coefficient3 = Encode(s.MaxComponents, s2)
                }).ToArray(),
                SimpleSamples = result.Simple.Select(s => new QuantizedSimpleLightSample { Coefficient = Encode(s, s3) }).ToArray()
            });
            summary.VertexLightmaps++;
        }

        // UE3 streams static meshes' lightmaps (LMF_Streamed) from the TFC, and stores BSP's and terrain's in the package (UnStaticMeshLight.cpp,
        // UnModelLight.cpp, TerrainLight.cpp). With no TFC to write to, every new lightmap is stored in the package
        var requests = mappings.Where(m => m.IsTextureMapping).Select(m => new AtlasRequest(m, "Directional", m.SizeX - (m.Padded ? 2 : 0),
            m.SizeY - (m.Padded ? 2 : 0), Streamed: tfc != null && m is LightmassInstance, Bounds: m.BoundingSphere, Padding: m.Padded ? 1 : 0));
        var streamedTextures = new HashSet<ExportEntry>();
        var streamedOwners = new HashSet<ExportEntry>();
        foreach (var atlas in AtlasPacker.Pack(requests, maxSize: 2048))
        {
            // The two directional textures. Shipping LMT_2D lightmaps have no simple lightmap texture (Texture3 0); LE1's and LE2's keep its scale
            var tops = new[] { MipLevel.Create(atlas.SizeX, atlas.SizeY), MipLevel.Create(atlas.SizeX, atlas.SizeY) };
            var maps = new Dictionary<LightmassMapping, LightMap_2D>();
            foreach (var placement in atlas.Placements)
            {
                var mapping = (LightmassMapping)placement.Request.Key;
                var result = results[mapping.Guid];
                var split = result.Directional.Select(s => Directional.Split(s)).ToArray();
                var scales = new[] { Quantization.ChooseScale(split.Select(s => s.Colour)), Quantization.ChooseScale(split.Select(s => s.MaxComponents)) };
                var simpleScale = package.Game.IsGame3() ? Vector3.Zero : Quantization.ChooseScale(result.Simple);
                for (int y = 0; y < result.SizeY; y++)
                for (int x = 0; x < result.SizeX; x++)
                {
                    int source = y * result.SizeX + x, dest = (placement.OffsetY + y) * atlas.SizeX + placement.OffsetX + x;
                    for (int c = 0; c < tops.Length; c++)
                    {
                        var color = Encode(c == 0 ? split[source].Colour : split[source].MaxComponents, scales[c]);
                        (tops[c].Rgb[dest * 3], tops[c].Rgb[dest * 3 + 1], tops[c].Rgb[dest * 3 + 2]) = (color.R, color.G, color.B);
                        tops[c].Coverage[dest] = result.Coverage[source];
                    }
                }
                var (scale, bias) = placement.Coordinates(atlas.SizeX, atlas.SizeY);
                maps[mapping] = new LightMap_2D { LightMapType = ELightMapType.LMT_2D, LightGuids = Guids(result),
                    ScaleVector1 = scales[0], ScaleVector2 = scales[1], ScaleVector3 = simpleScale, CoordinateScale = scale, CoordinateBias = bias };
            }
            var textures = tops.Select(top => WriteTexture(package, top, atlas.Streamed ? tfc : null)).ToArray();
            foreach (var (mapping, map) in maps)
            {
                (map.Texture1, map.Texture2) = (textures[0].UIndex, textures[1].UIndex);
                Apply(mapping, _ => map);
                summary.Allocations++;
                if (atlas.Streamed) streamedOwners.UnionWith(mapping.Targets.Where(t => t.Owner.FileRef == package).Select(t => t.Owner));
            }
            if (atlas.Streamed) streamedTextures.UnionWith(textures);
            summary.TextureSets++; summary.NewAtlases++; summary.Textures += textures.Length;
        }
        foreach (var (owner, binary) in binaries) owner.WriteBinary(binary);
        // Where the streamed ones are drawn, for the level's texture streaming, as UE3's editor lists them when it saves the level
        if (streamedTextures.Count > 0 && package.GetLevel() is { } level)
            TextureStreamingUpdate.AddLighting(level, streamedOwners, streamedTextures, cache);
        foreach (var group in mappings.OfType<LightmassBspMapping>().Where(m => m.ModelExport != null).GroupBy(m => m.ModelExport))
        {
            var model = ObjectBinary.From<Model>(group.Key);
            foreach (var mapping in group)
            foreach (var (vertex, bufferVertex, uv) in mapping.NewCoordinates)
            {
                model.Verts[vertex].ShadowTexCoord = uv;
                model.VertexBuffer[bufferVertex].ShadowTexCoord = uv;
            }
            group.Key.WriteBinary(model);
        }
    }

    public static void EnsureLods(StaticMeshComponent component, int count)
    {
        int oldCount = component.LODData.Length;
        if (count <= oldCount) return;
        Array.Resize(ref component.LODData, count);
        for (int i = oldCount; i < count; i++)
            component.LODData[i] = new StaticMeshComponentLODInfo { LightMap = new LightMap(), ShadowMaps = [], ShadowVertexBuffers = [] };
    }

    static Color Encode(Vector3 value, Vector3 scale)
    {
        var (r, g, b) = Quantization.Encode(value, scale, ByteEncoding.Gamma22);
        return new Color(r, g, b, (byte)255);
    }

    /// <param name="tfc">The cache to stream the texture from, or null to store it in the package</param>
    static ExportEntry WriteTexture(IMEPackage package, MipLevel top, TfcWriter tfc)
    {
        var properties = new PropertyCollection { new EnumProperty("TEXTUREGROUP_Lightmap", "TextureGroup", package.Game, "LODGroup") };
        if (tfc == null) properties.Add(new BoolProperty(true, "NeverStream"));
        var export = new ExportEntry(package, 0, package.GetNextIndexedName("LightMapTexture2D_LEX"), properties: properties,
            binary: new LightMapTexture2D { Mips = [], TextureGuid = Guid.NewGuid(), LightMapFlags = tfc != null ? ELightMapFlags.LMF_Streamed : ELightMapFlags.LMF_None })
        {
            Class = EntryImporter.EnsureClassIsInFile(package, "LightMapTexture2D", new RelinkerOptionsPackage())
        };
        package.AddExport(export);
        var mips = MipChain.BuildDilated(top, MipAveraging.Linear);
        LightmapTextureWriter.Write(export, "PF_DXT1", top.Width, top.Height, LightmapTextureWriter.Encode(mips, "PF_DXT1"), tfc, true);
        return export;
    }
}
