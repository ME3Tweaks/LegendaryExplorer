using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Lightmaps.Lightmass;

/// <summary>Where channel files go: Swarm's cache, through the agent (see <see cref="SwarmClient"/>).</summary>
public interface ILightmassChannels
{
    /// <summary>Whether a persistent channel (mesh, material) is already in the cache from an earlier bake.</summary>
    bool PersistentChannelExists(string channelName);
    /// <summary>Opens a channel's file for writing. The data written is the raw channel; compression is the caller's.</summary>
    Stream OpenWrite(string channelName, int flags);
    /// <summary>Closes a channel opened by <see cref="OpenWrite"/> (a persistent one then moves from the staging area into the cache).</summary>
    void Close(string channelName, int flags, Stream stream);
    /// <summary>Discards a channel opened by <see cref="OpenWrite"/> whose writing failed, so that no part of it is committed.</summary>
    void Abort(string channelName, int flags, Stream stream);
}

/// <summary>
/// Writes a <see cref="LightmassScene"/> to Lightmass's input channels in the order FLightmassExporter::WriteToChannel does
/// (IB Lightmass.cpp:635): the scene header, user and level names, importance volumes, visibility data (none), lights, mesh
/// instances and mappings into the job's scene channel, and each mesh and material into its own persistent channel.
/// </summary>
public sealed class LightmassExporter(LightmassScene scene, DuplicatingIni ini, LightmassQuality quality, ILightmassChannels channels)
{
    /// <summary>UE3's FStaticMeshStaticLightingVertexMapping: 1 / SubDivisionStepSize² (default step 16), sampling the surface rather than only the vertices.</summary>
    const float VertexSampleToAreaRatio = 1f / (16 * 16);

    public int MeshesWritten { get; private set; }
    public int MaterialsWritten { get; private set; }
    public int TerrainsWritten { get; private set; }

    public void Write(Guid sceneGuid)
    {
        foreach (var mesh in scene.Meshes) WriteMesh(mesh);
        foreach (var material in scene.Materials) WriteMaterial(material);
        foreach (var terrain in scene.Terrains) WriteTerrain(terrain);
        WriteScene(sceneGuid);
    }

    void WriteCompressed(string channelName, int flags, Action<LightmassWriter> write)
    {
        var stream = channels.OpenWrite(channelName, flags);
        try
        {
            using (var gzip = new GZipStream(stream, CompressionLevel.Fastest, leaveOpen: true))
            using (var buffered = new BufferedStream(gzip, 1 << 20))
            using (var w = new LightmassWriter(buffered))
                write(w);
        }
        catch (Exception e)
        {
            // Disposing the gzip stream above still ended it properly, so a partial channel would read as a whole one, and Swarm's cache
            // would hand a persistent one to every later bake of the same content.
            try { channels.Abort(channelName, flags, stream); }
            catch (Exception abortError) { throw new AggregateException($"Writing the channel {channelName} failed, and so did discarding it", e, abortError); }
            throw;
        }
        channels.Close(channelName, flags, stream);
    }

    // ================= scene =================

    void WriteScene(Guid sceneGuid)
    {
        var instances = scene.Instances;
        // Every instance has a mapping, occluders a token one that gets no task (LightmassSceneBuilder.OccluderMappingSize).
        var textureMappings = instances.Where(i => i.IsTextureMapping).ToList();
        var vertexMappings = instances.Where(i => !i.IsTextureMapping).ToList();
        var byType = scene.Lights.ToLookup(l => l.Light.Type);
        var counts = new LightmassSceneHeader.Counts(scene.ImportanceVolumes.Count,
            byType[SceneLightType.Directional].Count(), byType[SceneLightType.Point].Count(),
            byType[SceneLightType.Spot].Count(), byType[SceneLightType.Sky].Count(),
            scene.Meshes.Count, scene.Terrains.Count, instances.Count, scene.BspMappings.Count, textureMappings.Count, vertexMappings.Count,
            scene.TerrainMappings.Count);

        WriteCompressed(LightmassFormat.ChannelName(sceneGuid, LightmassFormat.SceneVersion, LightmassFormat.SceneExtension), LightmassFormat.SceneChannelFlags, w =>
        {
            LightmassSceneHeader.Write(w, ini, quality, scene.World, counts);
            w.LengthPrefixedString(Environment.UserName);
            w.LengthPrefixedString(scene.Levels.Count > 0 ? scene.Levels[0].FileNameNoExtension : "None");
            foreach (var (min, max) in scene.ImportanceVolumes) w.Box(min, max);
            // WriteVisibilityData: no bucket GUIDs, visibility volumes, override volumes or camera track positions
            w.Int(0); w.Int(0); w.Int(0);

            foreach (var type in (ReadOnlySpan<SceneLightType>)[SceneLightType.Directional,
                         SceneLightType.Point, SceneLightType.Spot, SceneLightType.Sky])
                foreach (var light in byType[type])
                    WriteLight(w, light);

            foreach (var instance in instances) WriteInstance(w, instance);
            // WriteMappings: BSP, static mesh texture, then static mesh vertex mappings
            foreach (var bsp in scene.BspMappings) WriteBspMapping(w, bsp);
            foreach (var mapping in textureMappings)
            {
                WriteMappingBase(w, mapping);
                // FStaticLightingTextureMappingData
                w.Int(mapping.SizeX);
                w.Int(mapping.SizeY);
                w.Int(mapping.HasOutput ? mapping.Mesh.LightMapCoordinateIndex : 0);
                w.Bool(true); // bBilinearFilter (bUseBilinearFilterLightmaps)
            }
            foreach (var mapping in vertexMappings)
            {
                WriteMappingBase(w, mapping);
                // FStaticLightingVertexMappingData
                w.Float(VertexSampleToAreaRatio);
                w.Bool(false); // bSampleVertices
            }
            foreach (var terrain in scene.TerrainMappings) WriteTerrainMapping(w, terrain);
        });
    }

    /// <summary>FLightData (96) and the type's struct (Copy(ULightComponent*, FLightData&amp;), WriteLights).</summary>
    static void WriteLight(LightmassWriter w, LightmassLight light)
    {
        var l = light.Light;
        bool directional = l.Type == SceneLightType.Directional;
        long start = w.Position;
        w.Guid(light.Guid);
        w.UInt(light.Flags);
        w.PadTo(start, 32);
        // GetPosition: point/spot lights their origin (W 1); directional lights far along -direction (W 0); sky lights (0,0,1,0)
        switch (l.Type)
        {
            case SceneLightType.Directional: w.Vector4(-l.Direction * 524288f, 0); break;
            case SceneLightType.Sky: w.Vector4(new Vector3(0, 0, 1), 0); break;
            default: w.Vector4(l.Position, 1); break;
        }
        w.Vector4(l.Direction, 1);
        w.Color(light.Color.R, light.Color.G, light.Color.B, 0);
        w.Float(light.Brightness);
        w.Float(directional ? 0 : light.LightSourceRadius);
        w.Float(light.IndirectLightingScale);
        w.Float(light.IndirectLightingSaturation);
        w.Float(light.ShadowExponent);
        w.PadTo(start, 96);
        switch (l.Type)
        {
            case SceneLightType.Directional:
                w.Float(light.LightSourceAngle);
                break;
            case SceneLightType.Point:
                w.Float(l.Radius);
                w.Float(l.FalloffExponent);
                break;
            case SceneLightType.Spot:
                w.Float(l.Radius);
                w.Float(l.FalloffExponent);
                w.Float(light.InnerConeAngle);
                w.Float(light.OuterConeAngle);
                break;
            case SceneLightType.Sky:
                w.Float(light.LowerBrightness);
                w.Color(light.LowerColor.R, light.LowerColor.G, light.LowerColor.B, 0);
                break;
        }
    }

    /// <summary>
    /// WriteBaseMeshInstanceData: FStaticLightingMeshInstanceData (96), the relevant lights, visibility IDs (none) and an FMaterialElementData
    /// (44) per element. The diffuse boosts are already in the materials' diffuse.
    /// </summary>
    static void WriteBaseInstance(LightmassWriter w, Guid guid, int triangles, int vertices, int meshIndex, int levelId, bool castShadow, bool selfShadowOnly,
        bool castShadowAsTwoSided, List<LightmassLight> relevantLights, Vector3 boundsMin, Vector3 boundsMax, IEnumerable<LightmassElement> elements)
    {
        long start = w.Position;
        w.Guid(guid);
        w.Int(triangles); // NumTriangles
        w.Int(triangles); // NumShadingTriangles
        w.Int(vertices); // NumVertices
        w.Int(vertices); // NumShadingVertices
        w.Int(0); // TextureCoordinateIndex
        w.Int(meshIndex);
        w.Int(levelId);
        // LightingFlags: GI_INSTANCE_CASTSHADOW, GI_INSTANCE_SELFSHADOWONLY. Never GI_INSTANCE_TWOSIDED: UE3 builds static mesh and terrain lighting meshes
        // with bTwoSidedMaterial FALSE (UnStaticMeshLight.cpp, TerrainLight.cpp); materials say whether they're two-sided themselves
        w.UInt((castShadow ? 1u : 0u) | (selfShadowOnly ? 4u : 0u));
        w.Bool(castShadowAsTwoSided);
        // UDK-2015-02 inserts two flags here, unlike IB (same struct size, so NumRelevantLights moves from 52 to 60; UDK.exe's
        // WriteBaseMeshInstanceData): one from the owning actor's flags and one set for a particular component class. Both are
        // false for level static meshes and BSP.
        w.Bool(false);
        w.Bool(false);
        w.Int(relevantLights.Count);
        w.PadTo(start, 64);
        w.Box(boundsMin, boundsMax);
        foreach (var light in relevantLights) w.Guid(light.Guid);
        w.Int(0); // NumVisibilityIds

        var list = elements.ToList();
        w.Int(list.Count);
        foreach (var (material, s) in list)
        {
            w.Guid(material.Guid);
            w.UInt((s.TwoSidedLighting ? 1u : 0u) | (s.ShadowIndirectOnly ? 2u : 0u) | (s.UseEmissiveForStaticLighting ? 4u : 0u));
            w.Float(s.EmissiveLightFalloffExponent);
            w.Float(s.EmissiveLightExplicitInfluenceRadius);
            w.Float(s.EmissiveBoost);
            w.Float(1f); // DiffuseBoost
            w.Float(1f); // SpecularBoost
            w.Float(s.FullyOccludedSamplesFraction);
        }
    }

    /// <summary>WriteBaseMeshInstanceData then FStaticMeshStaticLightingMeshData (272).</summary>
    static void WriteInstance(LightmassWriter w, LightmassInstance instance)
    {
        var lod = instance.Mesh.Mesh.LODModels[instance.LodIndex];
        WriteBaseInstance(w, instance.Guid, lod.IndexBuffer.Length / 3, VertexCount(lod), instance.MeshIndex, instance.LevelId, instance.CastShadow,
            instance.SelfShadowOnly, instance.CastShadowAsTwoSided, instance.RelevantLights, instance.BoundsMin, instance.BoundsMax,
            instance.ElementMaterials.Select(m => new LightmassElement(m, instance.Settings)));

        long start = w.Position;
        w.Int(instance.LodIndex); // EncodedLODIndex (no massive LOD)
        w.PadTo(start, 16);
        w.Matrix(instance.LocalToWorld);
        w.Bool(instance.LocalToWorld.GetDeterminant() < 0); // bReverseWinding
        w.Bool(true); // bShouldSelfShadow
        w.Guid(instance.Mesh.Guid);
        w.Bool(false); // bIsSplineMesh
        w.PadTo(start, 272); // FSplineMeshParams, zeroed
    }

    /// <summary>
    /// WriteMappings' BSP part (FBSPSurfaceStaticLighting::Import): the mesh instance data, the texture mapping (lightmap UVs in
    /// TextureCoordinates[1]), FBSPSurfaceStaticLightingData (192), then the vertices (FStaticLightingVertexData, 96), the triangles'
    /// vertex indices and each triangle's element.
    /// </summary>
    static void WriteBspMapping(LightmassWriter w, LightmassBspMapping m)
    {
        // UE3 gives every BSP mapping MeshIndex INDEX_NONE, so BSP never counts as another LOD of the same mesh.
        WriteBaseInstance(w, m.Guid, m.TriangleElements.Count, m.Vertices.Count, -1, m.LevelId, true, false, m.CastShadowAsTwoSided, m.RelevantLights,
            m.BoundsMin, m.BoundsMax, m.Elements);
        // FStaticLightingMappingData: Lightmass finds a mapping's mesh by the mapping's own GUID; the second is the model's LightingGuid in UE3, unused.
        w.Guid(m.Guid);
        w.Guid(Guid.Empty);
        w.Bool(false); // bForceDirectLightMap
        // FStaticLightingTextureMappingData
        w.Int(m.SizeX);
        w.Int(m.SizeY);
        w.Int(1); // LightmapTextureCoordinateIndex
        w.Bool(true); // bBilinearFilter
        // FBSPSurfaceStaticLightingData: the node group's tangents and map transforms, which Lightmass doesn't read, and a material GUID UE3 leaves unset
        var first = m.Vertices[0];
        w.Vector4(first.TangentX, 0);
        w.Vector4(first.TangentY, 0);
        w.Vector4(first.TangentZ, 0);
        w.Matrix(Matrix4x4.Identity);
        w.Matrix(Matrix4x4.Identity);
        w.Guid(Guid.Empty);
        foreach (var v in m.Vertices)
        {
            w.Vector4(v.Position, 1);
            w.Vector4(v.TangentX, 0);
            w.Vector4(v.TangentY, 0);
            w.Vector4(v.TangentZ, 0);
            w.Vector2(v.TextureUV);
            w.Vector2(v.LightmapUV);
            w.Vector2(Vector2.Zero);
            w.Vector2(Vector2.Zero);
        }
        foreach (int i in m.Indices) w.Int(i);
        foreach (int e in m.TriangleElements) w.Int(e);
    }

    /// <summary>
    /// WriteTerrainMapping (FTerrainComponentStaticLighting::Import): the mesh instance data, the texture mapping (unpadded; lightmap UVs in
    /// TextureCoordinates[1]), FStaticLightingTerrainMappingData (128 in UDK, which aligns its matrix to 16 bytes), the section's
    /// LocalToWorld and bounds, its patch bounds and its quads.
    /// </summary>
    static void WriteTerrainMapping(LightmassWriter w, LightmassTerrainMapping m)
    {
        WriteBaseInstance(w, m.Guid, m.Quads.Count * 2, m.Quads.Count * 4, -1, m.LevelId, m.CastShadow, m.SelfShadowOnly, false, m.RelevantLights,
            m.BoundsMin, m.BoundsMax, [m.Element]);
        w.Guid(m.Guid);
        w.Guid(m.Terrain.Guid);
        w.Bool(false); // bForceDirectLightMap
        w.Int(m.SizeX);
        w.Int(m.SizeY);
        w.Int(1); // LightmapTextureCoordinateIndex
        w.Bool(m.BilinearFilter);
        long start = w.Position;
        w.Guid(m.Terrain.Guid);
        w.Int(m.SectionBaseX);
        w.Int(m.SectionBaseY);
        w.Int(m.SectionSizeX);
        w.Int(m.SectionSizeY);
        w.Int(m.TrueSectionSizeX);
        w.Int(m.TrueSectionSizeY);
        w.Int(m.TrueSectionSizeX); // NumQuadsX
        w.Int(m.TrueSectionSizeY); // NumQuadsY
        w.Int(m.ExpandQuadsX);
        w.Int(m.ExpandQuadsY);
        w.PadTo(start, 64);
        Matrix4x4.Invert(m.LocalToWorld, out var inverse);
        w.Matrix(Matrix4x4.Transpose(inverse)); // LocalToWorldInverseTranspose
        w.Matrix(m.LocalToWorld);
        // FBoxSphereBounds as UE3 writes it: origin and extent as FVector4s, then the radius
        w.Vector4((m.BoundsMin + m.BoundsMax) / 2, 0);
        w.Vector4((m.BoundsMax - m.BoundsMin) / 2, 0);
        w.Float(Vector3.Distance(m.BoundsMin, m.BoundsMax) / 2);
        foreach (var b in m.PatchBounds)
        {
            w.Float(b.MinHeight);
            w.Float(b.MaxHeight);
            w.Float(b.MaxDisplacement);
        }
        w.Int(m.Quads.Count);
        foreach (var (x, y) in m.Quads)
        {
            w.Int(x);
            w.Int(y);
        }
    }

    /// <summary>FStaticLightingMappingData (36): the mesh instance's GUID twice over, as UE3 names a mapping by its mesh.</summary>
    static void WriteMappingBase(LightmassWriter w, LightmassInstance mapping)
    {
        w.Guid(mapping.Guid);
        w.Guid(mapping.Mesh.Guid); // StaticLightingMeshInstance: the source mesh (UE3 SourceMeshGuid)
        w.Bool(false); // bForceDirectLightMap
    }

    // ================= meshes =================

    /// <summary>The LOD's vertex count (UE3 exports RenderData.NumVertices; the buffers can't be shorter).</summary>
    public static int VertexCount(StaticMeshRenderData lod) =>
        Math.Min((int)lod.NumVertices, Math.Min(lod.PositionVertexBuffer.VertexData.Length, lod.VertexBuffer.VertexData.Length));

    /// <summary>WriteStaticMeshes (Lightmass.cpp:1019): FBaseMeshData, FStaticMeshData, then each LOD's elements, indices and vertices.</summary>
    void WriteMesh(LightmassMesh mesh)
    {
        string name = LightmassFormat.ChannelName(mesh.Guid, LightmassFormat.StaticMeshVersion, LightmassFormat.StaticMeshExtension);
        if (channels.PersistentChannelExists(name)) return;
        WriteCompressed(name, LightmassFormat.PersistentChannelFlags, w =>
        {
            w.Guid(mesh.Guid);
            w.UInt((uint)mesh.LightMapCoordinateIndex);
            w.UInt((uint)mesh.Mesh.LODModels.Length);
            foreach (var lod in mesh.Mesh.LODModels)
            {
                int vertexCount = VertexCount(lod);
                w.UInt((uint)lod.Elements.Length);
                w.UInt((uint)(lod.IndexBuffer.Length / 3));
                w.UInt((uint)lod.IndexBuffer.Length);
                w.UInt((uint)vertexCount);
                foreach (var element in lod.Elements)
                {
                    w.UInt(element.FirstIndex);
                    w.UInt(element.NumTriangles);
                    w.UInt(element.bEnableShadowCasting ? 1u : 0u);
                }
                foreach (ushort index in lod.IndexBuffer) w.UShort(index);
                int uvCount = Math.Min((int)lod.VertexBuffer.NumTexCoords, 4);
                for (int v = 0; v < vertexCount; v++)
                {
                    var vtx = lod.VertexBuffer.VertexData[v];
                    // FStaticMeshVertex: UE3's VertexTangentY is cross(TangentZ, TangentX) * sign(TangentZ.W)
                    var x = (Vector3)vtx.TangentX;
                    var z4 = (Vector4)vtx.TangentZ;
                    var z = new Vector3(z4.X, z4.Y, z4.Z);
                    var y = Vector3.Cross(z, x) * (z4.W < 0 ? -1f : 1f);
                    w.Vector4(lod.PositionVertexBuffer.VertexData[v], 1);
                    w.Vector4(x, 1);
                    w.Vector4(y, 1);
                    w.Vector4(z, 1);
                    for (int uv = 0; uv < 4; uv++)
                    {
                        if (uv >= uvCount) w.Vector2(Vector2.Zero);
                        else if (lod.VertexBuffer.bUseFullPrecisionUVs) w.Vector2(vtx.FullPrecisionUVs[uv]);
                        else w.Vector2(new Vector2(vtx.HalfPrecisionUVs[uv].X, vtx.HalfPrecisionUVs[uv].Y));
                    }
                }
            }
        });
        MeshesWritten++;
    }

    // ================= terrain =================

    /// <summary>
    /// WriteTerrains (Lightmass.cpp:1121): FBaseTerrainData, FTerrainData (272 in UDK: IB's packed fields, padded to 16 bytes), then the
    /// heights, info flags, cached displacements (none, as UE3 writes them), no materials and the collision vertices.
    /// </summary>
    void WriteTerrain(LightmassTerrain terrain)
    {
        string name = LightmassFormat.ChannelName(terrain.Guid, LightmassFormat.TerrainVersion, LightmassFormat.TerrainExtension);
        if (channels.PersistentChannelExists(name)) return;
        var p = terrain.Properties;
        int Int(string n, int fallback = 0) => p.GetProp<IntProperty>(n)?.Value ?? fallback;
        float Float(string n, float fallback = 0) => p.GetProp<FloatProperty>(n)?.Value ?? fallback;
        bool Bool(string n, bool fallback) => p.GetProp<BoolProperty>(n)?.Value ?? fallback;
        Vector3 Vec(string n, Vector3 fallback) => p.GetProp<StructProperty>(n) is { } v ? CommonStructs.GetVector3(v) : fallback;
        int nx = terrain.NumVerticesX, ny = terrain.NumVerticesY;
        WriteCompressed(name, LightmassFormat.PersistentChannelFlags, w =>
        {
            w.Guid(terrain.Guid);
            long start = w.Position;
            Matrix4x4.Invert(terrain.LocalToWorld, out var worldToLocal);
            w.Matrix(terrain.LocalToWorld);
            w.Matrix(worldToLocal);
            w.Vector4(Vec("Location", Vector3.Zero), 0);
            var r = p.GetProp<StructProperty>("Rotation") is { } rp ? CommonStructs.GetRotator(rp) : new Rotator(0, 0, 0);
            const float ToDegrees = 360f / 65536f;
            w.Vector4(new Vector3(r.Roll * ToDegrees, r.Pitch * ToDegrees, r.Yaw * ToDegrees), 0); // FRotator::Euler
            w.Vector4(Vec("DrawScale3D", Vector3.One), 0);
            w.Float(Float("DrawScale", 1));
            w.Int(Int("NumSectionsX"));
            w.Int(Int("NumSectionsY"));
            w.Int(0); // SectionSize
            w.Float(0); // MaxCollisionDisplacement
            w.Int(Int("MaxTesselationLevel", 4));
            w.Int(Int("MinTessellationLevel", 1));
            w.Float(Float("TesselationDistanceScale", 1));
            w.Float(Float("TessellationCheckDistance", -1));
            w.Int(nx);
            w.Int(ny);
            w.Int(terrain.NumPatchesX);
            w.Int(terrain.NumPatchesY);
            w.Int(Int("MaxComponentSize", 16));
            w.Int(terrain.StaticLightingResolution);
            w.Bool(Bool("bIsOverridingLightResolution", false));
            w.Bool(Bool("bBilinearFilterLightmapGeneration", true));
            w.Bool(Bool("bCastShadow", true));
            w.Bool(Bool("bForceDirectLightMap", false));
            w.Bool(Bool("bCastDynamicShadow", true));
            w.Bool(Bool("bAcceptsDynamicLights", true));
            w.Int(0); // NumMaterials
            w.Int((nx + 1) * (ny + 1)); // NumCollisionVertices
            w.PadTo(start, 272);
            foreach (ushort h in terrain.Binary.Heights) w.UShort(h);
            foreach (var f in terrain.Binary.InfoData) w.Byte((byte)f);
            for (int i = 0; i < nx * ny; i++) w.Byte(0); // CachedDisplacements
            // Collision vertices (local space, one past the last vertex each way); Lightmass imports them but lights from the heights.
            for (int y = 0; y <= ny; y++)
            for (int x = 0; x <= nx; x++)
                w.Vector4(new Vector3(Math.Min(x, nx - 1), Math.Min(y, ny - 1), (terrain.Height(x, y) - 32768f) * LightmassSceneBuilder.TerrainZScale), 1);
        });
        TerrainsWritten++;
    }

    // ================= materials =================

    /// <summary>WriteMaterials (Lightmass.cpp:1506): FBaseMaterialData, FMaterialData, then the FFloat16Color samples of each property.</summary>
    void WriteMaterial(LightmassMaterial material)
    {
        string name = LightmassFormat.ChannelName(material.Guid, LightmassFormat.MaterialVersion, LightmassFormat.MaterialExtension);
        if (channels.PersistentChannelExists(name)) return;
        WriteCompressed(name, LightmassFormat.PersistentChannelFlags, w =>
        {
            w.Guid(material.Guid);
            w.Int(material.BlendMode);
            w.UInt((material.TwoSided ? 1u : 0u) | (material.CastShadowAsMasked ? 2u : 0u));
            w.Float(material.EmissiveBoost);
            w.Float(1f); // DiffuseBoost: already applied
            w.Float(1f); // SpecularBoost
            w.Float(material.OpacityMaskClipValue);
            w.Int(material.Emissive?.Size ?? 0);
            w.Int(material.Diffuse?.Size ?? 0);
            w.Int(material.Specular?.Size ?? 0);
            w.Int(material.Transmission?.Size ?? 0);
            w.Int(0); // NormalSize
            foreach (var samples in new[] { material.Emissive, material.Diffuse, material.Specular, material.Transmission })
                if (samples != null)
                    foreach (var h in samples.Rgba) w.UShort(BitConverter.HalfToUInt16Bits(h));
        });
        MaterialsWritten++;
    }
}
