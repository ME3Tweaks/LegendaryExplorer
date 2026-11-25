using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// What a primitive contributes to its level's TextureToInstancesMap: each texture it draws with, the sphere it's drawn in and how many world units one UV unit
/// spans there (UPrimitiveComponent::GetStreamingTextureInfo). Memoizes meshes and materials, so it's for one operation
/// </summary>
public sealed class TextureStreamingInstances(PackageCache cache)
{
    /// <param name="Texture">A Texture2D, in whichever package it's in</param>
    public readonly record struct Instance(ExportEntry Texture, Vector3 Center, float Radius, float TexelFactor, bool Lighting);

    private readonly Dictionary<ExportEntry, MeshFactors> meshFactors = [];
    private readonly Dictionary<ExportEntry, ExportEntry[]> materialTextures = [];
    private readonly Dictionary<ExportEntry, (string Parameter, ExportEntry Texture)[]> samplers = [];
    private ExportEntry defaultMaterial;
    private bool defaultMaterialRead;

    private sealed record MeshFactors(float Material, float Lighting, bool HasLightmapUVs);

    /// <summary>MAX_TEXCOORDS: the UV channels UStaticMesh::GetStreamingTextureFactor measures</summary>
    private const int MaxTexCoords = 4;

    /// <summary>
    /// The instances a StaticMeshComponent (or a subclass, such as a spline's mesh) has in its level's map: UStaticMeshComponent::GetStreamingTextureInfo's
    /// textures (LOD0's materials' and, if the mesh has lightmap UVs, its streamed LOD0 lightmap and shadow map textures), as ULevel::BuildStreamingData lists
    /// them. LE1 and LE2 list every one. LE3 lists those of a static owner (or a level-placed KActor, or one that streams all its meshes' textures), and of any
    /// other owner only its world and environment textures, as UE3 does (checked against every 10th shipping level)
    /// </summary>
    public List<Instance> OfStaticMesh(ExportEntry component)
    {
        var result = new List<Instance>();
        var props = component.GetCondensedProperties(cache, resolveImports: true, mergeStructs: true);
        if (props.GetProp<BoolProperty>("bIgnoreInstanceForTextureStreaming")?.Value == true) return result;
        if (Resolve(component.FileRef, props.GetProp<ObjectProperty>("StaticMesh")?.Value ?? 0) is not { ClassName: "StaticMesh" } meshExport) return result;
        var mesh = meshExport.GetBinaryData<StaticMesh>();
        if (mesh.LODModels.Length == 0) return result;

        var localToWorld = LocalToWorld(component, props);
        Vector3 center;
        float radius, deformFactor = 1f;
        if (component.IsA("SplineMeshComponent"))
        {
            // USplineMeshComponent::UpdateBounds, and GetStreamingTextureInfo's compensation for the stretching: how much bigger the bounds are than the mesh's
            (center, var extent, radius) = SplineBounds(SplineMeshDeformation.Read(props, mesh.Bounds), mesh.Bounds, localToWorld);
            var ratio = extent / TransformBy(mesh.Bounds, localToWorld).Extent;
            deformFactor = UnrealMax(UnrealMax(ratio.X, ratio.Y), ratio.Z);
        }
        else (center, radius) = StaticMeshBounds(meshExport, mesh, props, localToWorld);
        var factors = Factors(meshExport, mesh);
        float maxAxisScale = MaxAxisScale(localToWorld);
        bool streamsAll = component.Game.IsGame1() || component.Game.IsGame2() || OwnerStreamsAllTextures(component);

        var overrides = props.GetProp<ArrayProperty<ObjectProperty>>("Materials");
        var elements = mesh.LODModels[0].Elements;
        foreach (var element in elements)
        {
            // UStaticMeshComponent::GetMaterial(Element.MaterialIndex): the component's, else the first of the mesh's elements with that index's. Drawn with
            // the engine's default material if neither has one (but one that can't be found is left out: it isn't known to be none)
            int index = element.MaterialIndex;
            var material = overrides is not null && index < overrides.Count && overrides[index].Value != 0 ? Resolve(component.FileRef, overrides[index].Value)
                : elements.FirstOrDefault(e => e.MaterialIndex == index) is { Material: not 0 } first ? Resolve(meshExport.FileRef, first.Material)
                : DefaultMaterial(component.Game);
            foreach (var texture in MaterialTextures(material))
            {
                if (streamsAll || IsWorldTexture(texture)) result.Add(new Instance(texture, center, radius, deformFactor * factors.Material * maxAxisScale, false));
            }
        }

        // (lightmaps and shadow maps aren't world textures)
        if (streamsAll && factors.HasLightmapUVs && component.GetBinaryData<StaticMeshComponent>() is { LODData.Length: > 0 } binary)
        {
            float lightingFactor = deformFactor * factors.Lighting * maxAxisScale;
            var lod = binary.LODData[0];
            void Add(int texture, Vector2 coordinateScale)
            {
                if (coordinateScale.X <= 1e-8f || coordinateScale.Y <= 1e-8f || Resolve(component.FileRef, texture) is not { } export || !IsStreamedLighting(export)) return;
                result.Add(new Instance(export, center, radius, MathF.Max(lightingFactor / coordinateScale.X, lightingFactor / coordinateScale.Y), true));
            }
            // The directional coefficients (GSystemSettings.bAllowDirectionalLightMaps)
            if (lod.LightMap is LightMap_2D lightMap)
            {
                Add(lightMap.Texture1, lightMap.CoordinateScale);
                Add(lightMap.Texture2, lightMap.CoordinateScale);
            }
            foreach (int shadowMap in lod.ShadowMaps)
            {
                if (Resolve(component.FileRef, shadowMap) is not { ClassName: "ShadowMap2D" } shadowMapExport) continue;
                var shadowMapProps = shadowMapExport.GetProperties();
                if (shadowMapProps.GetProp<ObjectProperty>("Texture") is { } texture && shadowMapProps.GetProp<StructProperty>("CoordinateScale") is { } scale
                    && scale.GetProp<FloatProperty>("X") is { } x && scale.GetProp<FloatProperty>("Y") is { } y)
                    Add(texture.Value, new Vector2(x.Value, y.Value));
            }
        }
        return result;
    }

    /// <summary>The engine's DefaultMaterial (GEngine->DefaultMaterial), or null where the game isn't installed</summary>
    private ExportEntry DefaultMaterial(MEGame game)
    {
        if (!defaultMaterialRead)
        {
            defaultMaterial = MaterialUsage.EngineMaterial(game, "DefaultMaterial", cache);
            defaultMaterialRead = true;
        }
        return defaultMaterial;
    }

    /// <summary>Max as UE3 defines it, (A >= B) ? A : B, which a NaN on the left loses to and one on the right wins</summary>
    private static float UnrealMax(float a, float b) => a >= b ? a : b;

    /// <summary>
    /// ULevel::BuildStreamingData's bStreamNonWorldTextures: the component has no owner, or a static one (a collection's members are static), a level-placed
    /// KActor, or one that has bConsiderAllStaticMeshComponentsForStreaming
    /// </summary>
    private bool OwnerStreamsAllTextures(ExportEntry component)
    {
        if (component.Parent is not ExportEntry owner || !owner.IsA("Actor") || owner.IsA("StaticMeshCollectionActor")) return true;
        // (with its class's defaults: a StaticMeshActor's bStatic is its class's)
        var props = EffectiveLightingProperties.Read(owner, cache);
        return props.GetProp<BoolProperty>("bStatic")?.Value == true
            || owner.IsA("KActor") && props.GetProp<BoolProperty>("bNoDelete")?.Value == true
            || props.GetProp<BoolProperty>("bConsiderAllStaticMeshComponentsForStreaming")?.Value == true;
    }

    /// <summary>BuildStreamingData's bIsWorldTexture, with BioWare's environment groups</summary>
    private static bool IsWorldTexture(ExportEntry texture) =>
        (texture.GetProperty<EnumProperty>("LODGroup")?.Value.Instanced ?? "TEXTUREGROUP_World") is var group
        && (group is "TEXTUREGROUP_World" or "TEXTUREGROUP_WorldNormalMap" or "TEXTUREGROUP_WorldSpecular" || group.StartsWith("TEXTUREGROUP_Environment", StringComparison.Ordinal));

    /// <summary>Whether a lightmap or shadow map texture is streamed (LMF_Streamed, SMF_Streamed): BuildStreamingData leaves out those that aren't</summary>
    private static bool IsStreamedLighting(ExportEntry texture) => texture.ClassName switch
    {
        "LightMapTexture2D" => !texture.Game.IsGame3() || texture.GetBinaryData<LightMapTexture2D>().LightMapFlags.HasFlag(ELightMapFlags.LMF_Streamed),
        "ShadowMapTexture2D" => !texture.Game.IsGame3() || (texture.GetProperty<IntProperty>("ShadowmapFlags")?.Value & 1) == 1,
        _ => false
    };

    /// <summary>
    /// USplineMeshComponent::UpdateBounds: its Hermite segment's box (CurveVectorFindIntervalBounds) grown all round by the mesh's largest XY extent at the larger
    /// end scale, in world space
    /// </summary>
    private static (Vector3 Center, Vector3 Extent, float Radius) SplineBounds(SplineMeshDeformation spline, BoxSphereBounds meshBounds, Matrix4x4 localToWorld)
    {
        var (minX, maxX) = HermiteBounds(spline.StartPos.X, spline.StartTangent.X, spline.EndPos.X, spline.EndTangent.X);
        var (minY, maxY) = HermiteBounds(spline.StartPos.Y, spline.StartTangent.Y, spline.EndPos.Y, spline.EndTangent.Y);
        var (minZ, maxZ) = HermiteBounds(spline.StartPos.Z, spline.StartTangent.Z, spline.EndPos.Z, spline.EndTangent.Z);
        float grow = MathF.Max(MathF.Max(spline.StartScale.X, spline.StartScale.Y), MathF.Max(spline.EndScale.X, spline.EndScale.Y))
            * MathF.Max(meshBounds.BoxExtent.X, meshBounds.BoxExtent.Y);
        var min = new Vector3(minX, minY, minZ) - new Vector3(grow);
        var max = new Vector3(maxX, maxY, maxZ) + new Vector3(grow);
        var worldMin = new Vector3(float.MaxValue);
        var worldMax = new Vector3(float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            var corner = Vector3.Transform(new Vector3((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z), localToWorld);
            worldMin = Vector3.Min(worldMin, corner);
            worldMax = Vector3.Max(worldMax, corner);
        }
        var extent = (worldMax - worldMin) / 2;
        return ((worldMin + worldMax) / 2, extent, extent.Length());
    }

    /// <summary>FindBounds for a curve key over a unit interval: the ends, and the curve's turning points between them</summary>
    private static (float Min, float Max) HermiteBounds(float start, float startTangent, float end, float endTangent)
    {
        float low = MathF.Min(start, end), high = MathF.Max(start, end);
        float a = 6f * start + 3f * startTangent + 3f * endTangent - 6f * end;
        float b = -6f * start - 4f * startTangent - 2f * endTangent + 6f * end;
        float c = startTangent;
        float discriminant = b * b - 4f * a * c;
        if (discriminant > 0f)
        {
            float root = MathF.Sqrt(discriminant);
            foreach (float x in new[] { (-b + root) / (2f * a), (-b - root) / (2f * a) })
            {
                if (!(x > 0f && x < 1f)) continue;
                float x2 = x * x, x3 = x2 * x;
                float value = (2 * x3 - 3 * x2 + 1) * start + (x3 - 2 * x2 + x) * startTangent + (x3 - x2) * endTangent + (-2 * x3 + 3 * x2) * end;
                low = MathF.Min(low, value);
                high = MathF.Max(high, value);
            }
        }
        return (low, high);
    }

    /// <summary>
    /// A component's LocalToWorld: its own transform on its collection's matrix for that member, or on its owner's
    /// </summary>
    private Matrix4x4 LocalToWorld(ExportEntry component, PropertyCollection props)
    {
        var translation = props.GetProp<StructProperty>("Translation") is { } t ? CommonStructs.GetVector3(t) : Vector3.Zero;
        var rotation = props.GetProp<StructProperty>("Rotation") is { } r ? CommonStructs.GetRotator(r) : new Rotator(0, 0, 0);
        var scale = (props.GetProp<FloatProperty>("Scale")?.Value ?? 1f) * (props.GetProp<StructProperty>("Scale3D") is { } s ? CommonStructs.GetVector3(s) : Vector3.One);
        var local = ActorUtils.ComposeLocalToWorld(translation, rotation, scale);
        if (component.Parent is not ExportEntry owner) return local;
        if (owner.IsA("StaticMeshCollectionActor"))
        {
            var collection = owner.GetBinaryData<StaticMeshCollectionActor>();
            int index = collection.Components.FindIndex(u => u == component.UIndex);
            return index >= 0 ? local * collection.LocalToWorldTransforms[index] : local;
        }
        return owner.IsA("Actor") ? local * ActorUtils.GetLocalToWorld(owner.GetCondensedProperties(cache, resolveImports: true, mergeStructs: true)) : local;
    }

    /// <summary>
    /// UStaticMeshComponent::UpdateBounds' sphere: the mesh's bounds and its collision's box (LegacyUnion), one unit bigger for collision's nudge, times BoundsScale
    /// </summary>
    private static (Vector3 Center, float Radius) StaticMeshBounds(ExportEntry meshExport, StaticMesh mesh, PropertyCollection props, Matrix4x4 localToWorld)
    {
        var (origin, extent, radius) = TransformBy(mesh.Bounds, localToWorld);
        if (meshExport.GetProperty<ObjectProperty>("BodySetup")?.ResolveToEntry(meshExport.FileRef) is ExportEntry bodySetup
            && bodySetup.GetProperty<StructProperty>("AggGeom") is { } aggGeom && AggGeomBox(aggGeom, localToWorld) is var (min, max))
        {
            // LegacyUnion
            var unionMin = Vector3.Min(origin - extent, min);
            var unionMax = Vector3.Max(origin + extent, max);
            var boxCenter = (unionMin + unionMax) / 2;
            var boxExtent = (unionMax - unionMin) / 2;
            var collisionCenter = (min + max) / 2;
            radius = MathF.Min(boxExtent.Length(), MathF.Max((origin - boxCenter).Length() + radius, (collisionCenter - boxCenter).Length()));
            origin = boxCenter;
        }
        float boundsScale = props.GetProp<FloatProperty>("BoundsScale")?.Value ?? 1f;
        return (origin, (radius + 1f) * boundsScale);
    }

    /// <summary>FBoxSphereBounds::TransformBy</summary>
    private static (Vector3 Origin, Vector3 Extent, float Radius) TransformBy(BoxSphereBounds bounds, Matrix4x4 m)
    {
        var origin = Vector3.Transform(bounds.Origin, m);
        var e = bounds.BoxExtent;
        var extent = new Vector3(
            MathF.Abs(e.X * m.M11) + MathF.Abs(e.Y * m.M21) + MathF.Abs(e.Z * m.M31),
            MathF.Abs(e.X * m.M12) + MathF.Abs(e.Y * m.M22) + MathF.Abs(e.Z * m.M32),
            MathF.Abs(e.X * m.M13) + MathF.Abs(e.Y * m.M23) + MathF.Abs(e.Z * m.M33));
        return (origin, extent, MaxAxisScale(m) * bounds.SphereRadius);
    }

    /// <summary>
    /// FKAggregateGeom::CalcAABB's box of the hulls' ElemBoxes at the component's transform, or null if it has none. BioWare's meshes' collision is hulls: the
    /// spheres, boxes and capsules it also takes at a uniform scale aren't read
    /// </summary>
    private static (Vector3 Min, Vector3 Max)? AggGeomBox(StructProperty aggGeom, Matrix4x4 localToWorld)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        bool any = false;
        void AddPoint(Vector3 p) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); any = true; }
        void AddBox(Vector3 boxMin, Vector3 boxMax, Matrix4x4 m)
        {
            for (int i = 0; i < 8; i++)
                AddPoint(Vector3.Transform(new Vector3((i & 1) == 0 ? boxMin.X : boxMax.X, (i & 2) == 0 ? boxMin.Y : boxMax.Y, (i & 4) == 0 ? boxMin.Z : boxMax.Z), m));
        }
        foreach (var convex in aggGeom.GetProp<ArrayProperty<StructProperty>>("ConvexElems") ?? Enumerable.Empty<StructProperty>())
        {
            if (convex.GetProp<StructProperty>("ElemBox") is { } box && box.GetProp<ByteProperty>("IsValid")?.Value != 0
                && box.GetProp<StructProperty>("Min") is { } boxMin && box.GetProp<StructProperty>("Max") is { } boxMax)
                AddBox(CommonStructs.GetVector3(boxMin), CommonStructs.GetVector3(boxMax), localToWorld);
        }
        return any ? (min, max) : null;
    }

    /// <summary>FMatrix::GetMaximumAxisScale</summary>
    private static float MaxAxisScale(Matrix4x4 m) => MathF.Sqrt(MathF.Max(m.M11 * m.M11 + m.M12 * m.M12 + m.M13 * m.M13,
        MathF.Max(m.M21 * m.M21 + m.M22 * m.M22 + m.M23 * m.M23, m.M31 * m.M31 + m.M32 * m.M32 + m.M33 * m.M33)));

    /// <summary>
    /// UStaticMesh::GetStreamingTextureFactor for UV 0 and the lightmap's UV channel: of LOD0's triangles' world units per UV unit, the value three quarters of
    /// the way down from the largest. Or, for a mesh with bUseMaximumStreamingTexelRatio, the largest over every channel it measures. UV 0's (and every channel's
    /// largest) is times the mesh's StreamingDistanceMultiplier
    /// </summary>
    private MeshFactors Factors(ExportEntry meshExport, StaticMesh mesh)
    {
        if (meshFactors.TryGetValue(meshExport, out var found)) return found;
        var meshProps = meshExport.GetProperties();
        int lightmapIndex = meshProps.GetProp<IntProperty>("LightMapCoordinateIndex")?.Value ?? 0;
        float multiplier = meshProps.GetProp<FloatProperty>("StreamingDistanceMultiplier")?.Value ?? 1f;
        int ignored = meshProps.GetProp<IntProperty>("ElementToIgnoreForTexFactor")?.Value ?? -1;
        bool useMaximum = meshProps.GetProp<BoolProperty>("bUseMaximumStreamingTexelRatio")?.Value == true;
        var lod = mesh.LODModels[0];
        var positions = lod.PositionVertexBuffer.VertexData;
        int numUVs = (int)Math.Min(lod.VertexBuffer.NumTexCoords, MaxTexCoords);
        bool hasLightmapUVs = lightmapIndex >= 0 && lightmapIndex < lod.VertexBuffer.NumTexCoords;
        // One pass over the triangles for every channel, as UE3 makes it: the largest ratio is the largest of any channel's
        var ratios = new List<float>[numUVs];
        for (int uv = 0; uv < numUVs; uv++) ratios[uv] = [];
        float maxRatio = 0f;
        foreach (var element in lod.Elements)
        {
            if (element.MaterialIndex == ignored) continue;
            for (uint t = 0; t < element.NumTriangles; t++)
            {
                uint first = element.FirstIndex + t * 3;
                if (first + 2 >= lod.IndexBuffer.Length) break;
                int i0 = lod.IndexBuffer[first], i1 = lod.IndexBuffer[first + 1], i2 = lod.IndexBuffer[first + 2];
                float l1 = (positions[i0] - positions[i1]).Length(), l2 = (positions[i0] - positions[i2]).Length();
                for (int uv = 0; uv < numUVs; uv++)
                {
                    Vector2 uv0 = UV(lod.VertexBuffer, i0, uv), uv1 = UV(lod.VertexBuffer, i1, uv), uv2 = UV(lod.VertexBuffer, i2, uv);
                    float t1 = (uv0 - uv1).Length(), t2 = (uv0 - uv2).Length();
                    if (MathF.Abs(t1 * t2) <= 1e-8f * 1e-8f) continue;
                    float ratio = MathF.Max(l1 / t1, l2 / t2);
                    ratios[uv].Add(ratio);
                    maxRatio = MathF.Max(maxRatio, ratio);
                }
            }
        }
        float Factor(int uv)
        {
            if (uv >= numUVs) return 0f;
            if (useMaximum) return maxRatio * multiplier;
            var channel = ratios[uv];
            if (channel.Count == 0) return 0f;
            // Sorted largest first, so this disregards the largest three quarters: back faces and other unseen surfaces that map few texels onto a lot of area
            channel.Sort((a, b) => b.CompareTo(a));
            return channel[(int)(channel.Count * 0.75f)] * (uv == 0 ? multiplier : 1f);
        }
        return meshFactors[meshExport] = new MeshFactors(Factor(0), hasLightmapUVs ? Factor(lightmapIndex) : 1f, hasLightmapUVs);
    }

    private static Vector2 UV(StaticMeshVertexBuffer buffer, int vertex, int uv) => buffer.bUseFullPrecisionUVs
        ? buffer.VertexData[vertex].FullPrecisionUVs[uv]
        : new Vector2(buffer.VertexData[vertex].HalfPrecisionUVs[uv].X, buffer.VertexData[vertex].HalfPrecisionUVs[uv].Y);

    /// <summary>
    /// UMaterialInterface::GetUsedTextures' Texture2Ds. A material's are its compiled resource's textures. An instance's are the resource it's drawn with:
    /// the first static permutation resource up its chain of instances, else its base material's (the engine's default material's if the chain ends without
    /// one), with each texture parameter's value up the chain in place of the parameter's default. (UE3 as Infinity Blade has it takes the base material's
    /// resource unless the instance has its own permutation, but then an instance of one with a permutation would list textures, such as the base material's
    /// placeholders, that shipping levels don't: about 3,000 instances in every 10th LE2 level)
    /// </summary>
    public ExportEntry[] MaterialTextures(ExportEntry material)
    {
        if (material is null) return [];
        if (materialTextures.TryGetValue(material, out var found)) return found;
        materialTextures[material] = [];
        var overrides = new Dictionary<string, ExportEntry>(StringComparer.OrdinalIgnoreCase);
        ExportEntry[] resource = null;
        ExportEntry current = material, baseMaterial = null;
        for (int depth = 0; current is not null && depth < 32; depth++)
        {
            if (current.ClassName == "Material")
            {
                baseMaterial = current;
                break;
            }
            if (!current.IsA("MaterialInstance")) break;
            var props = current.GetProperties();
            foreach (var parameter in props.GetProp<ArrayProperty<StructProperty>>("TextureParameterValues") ?? Enumerable.Empty<StructProperty>())
            {
                if (parameter.GetProp<NameProperty>("ParameterName")?.Value.Instanced is { } name && !overrides.ContainsKey(name))
                    overrides[name] = Resolve(current.FileRef, parameter.GetProp<ObjectProperty>("ParameterValue")?.Value ?? 0);
            }
            if (resource is null && props.GetProp<BoolProperty>("bHasStaticPermutationResource")?.Value == true)
                resource = Textures(current, current.GetBinaryData<MaterialInstance>().SM3StaticPermutationResource);
            int parent = props.GetProp<ObjectProperty>("Parent")?.Value ?? 0;
            if (parent == 0)
            {
                // An instance with no material: the default material's (and the default material can't lack one)
                var fallback = DefaultMaterial(material.Game);
                return materialTextures[material] = fallback is null || fallback == material ? [] : MaterialTextures(fallback);
            }
            current = Resolve(current.FileRef, parent);
        }
        // (a parent that can't be found, or isn't a material: what it draws with isn't known)
        if (baseMaterial is null) return [];
        resource ??= Textures(baseMaterial, baseMaterial.GetBinaryData<Material>().SM3MaterialResource);

        // The resource lists each texture its expressions sample once, as their defaults. A texture parameter's is replaced by its value, and stays only if a
        // plain texture sample samples it too, or no parameter that defaults to it is given a value. (A parameter that keeps a default another one replaces
        // usually isn't drawn: shipping levels don't list such defaults, the base material's placeholders, for about 3,000 instances in every 10th LE2 level.
        // Its expression is still there, since the static permutation that drops it isn't the base material's)
        var expressions = Samplers(baseMaterial);
        var textures = new List<ExportEntry>();
        foreach (var texture in resource)
        {
            var sampling = expressions.Where(s => s.Texture == texture).ToList();
            var values = sampling.Where(s => s.Parameter is not null && overrides.ContainsKey(s.Parameter)).Select(s => overrides[s.Parameter]).ToList();
            textures.AddRange(values.Where(v => v is not null));
            if (values.Count == 0 || sampling.Any(s => s.Parameter is null)) textures.Add(texture);
        }
        return materialTextures[material] = [.. textures.Where(t => t.IsA("Texture2D")).Distinct()];
    }

    private ExportEntry[] Textures(ExportEntry holder, MaterialResource resource) =>
        [.. resource.UniformExpressionTextures.Select(u => Resolve(holder.FileRef, u)).Where(t => t is not null)];

    /// <summary>
    /// A base material's TextureSample expressions: the parameter each is (null for a plain sample) and the texture it samples or defaults to. Cooked
    /// materials may have lost them, which leaves their resources' textures as they are
    /// </summary>
    private (string Parameter, ExportEntry Texture)[] Samplers(ExportEntry baseMaterial)
    {
        if (samplers.TryGetValue(baseMaterial, out var found)) return found;
        return samplers[baseMaterial] = [.. baseMaterial.GetChildren<ExportEntry>().Where(x => x.IsA("MaterialExpressionTextureSample")).Select(x =>
        {
            var props = x.GetProperties();
            return (Parameter: x.IsA("MaterialExpressionTextureSampleParameter") ? props.GetProp<NameProperty>("ParameterName")?.Value.Instanced : null,
                Texture: Resolve(baseMaterial.FileRef, props.GetProp<ObjectProperty>("Texture")?.Value ?? 0));
        }).Where(s => s.Texture is not null)];
    }

    private ExportEntry Resolve(IMEPackage package, int uIndex) => uIndex == 0 ? null : package.GetEntry(uIndex) switch
    {
        ExportEntry export => export,
        ImportEntry import when EntryImporter.TryResolveImport(import, out var resolved, cache) => resolved,
        _ => null
    };
}
