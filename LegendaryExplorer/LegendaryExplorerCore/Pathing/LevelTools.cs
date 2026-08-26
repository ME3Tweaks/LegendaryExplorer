using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Unreal.Collections;
using LegendaryExplorerCore.Unreal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorerCore.Pathing
{
    /// <summary>
    /// Methods for working with levels
    /// </summary>
    public static class LevelTools
    {
        /// <summary>
        /// Computes a texture to instances map for a level package
        /// </summary>
        /// <param name="package">Package to calculate</param>
        /// <param name="cache">Cache to use when performing import resolution</param>
        public static void CalculateTextureToInstancesMap(IMEPackage package, TieredPackageCache cache)
        {
            LECLog.Information($"Calculating TextureToInstancesMap for {package.FileNameNoExtension}");
            var textureToInstancesMap = new Dictionary<IEntry, List<StreamableTextureInstance>>();

            // Per-mesh cache: the texel factor is a property of the mesh asset itself (mirrors
            // UStaticMesh/USkeletalMesh::CachedStreamingTextureFactors), so it only needs to be
            // computed once even if the mesh is placed many times.
            var texelFactorCache = new Dictionary<ExportEntry, float>();

            // Look at all components
            foreach (var component in package.Exports.Where(x => x.IsA("Component")))
            {
                // Only do ones in the world
                if (!component.GetRootName().CaseInsensitiveEquals("TheWorld"))
                    continue;

                // Use inherited properties too - placed Pawn components (BioH conversation actors, spawned
                // NPCs, etc.) very often don't set StaticMesh/SkeletalMesh/Materials on the instance itself;
                // they inherit them from their archetype chain, same as the engine resolves them at runtime.
                var props = component.GetPropertiesIncludingInherited(cache);

                ObjectProperty meshProp = props.GetProp<ObjectProperty>("StaticMesh");
                meshProp ??= props.GetProp<ObjectProperty>("SkeletalMesh");

                if (meshProp == null)
                    continue; // Not supported by this code.

                // We need the mesh regardless of whether the component overrides its materials,
                // since its Bounds and UV density (texel factor) drive the streaming instance.
                if (!meshProp.TryResolveExport(package, cache, out var mdl))
                    continue;

                // List of materials this mesh uses
                List<IEntry> texturesOnMesh = new List<IEntry>();
                List<ExportEntry> materials = new List<ExportEntry>();
                var matList = props.GetProp<ArrayProperty<ObjectProperty>>("Materials");
                if (matList != null)
                {
                    foreach (var mat in matList)
                    {
                        if (mat.TryResolveExport(package, cache, out var matExp))
                        {
                            materials.Add(matExp);
                        }
                    }
                }

                BoxSphereBounds meshBounds;
                float meshTexelFactor;

                switch (mdl.ClassName)
                {
                    case "StaticMesh":
                        {
                            var bin = ObjectBinary.From<StaticMesh>(mdl);
                            meshBounds = bin.Bounds;

                            if (!texelFactorCache.TryGetValue(mdl, out meshTexelFactor))
                            {
                                meshTexelFactor = ComputeStaticMeshTexelFactor(bin);
                                texelFactorCache[mdl] = meshTexelFactor;
                            }

                            // if we don't have any materials set on component, we have to pull it off the mesh.
                            if (materials.Count == 0)
                            {
                                foreach (var mat in bin.GetMaterials())
                                {
                                    if (mdl.FileRef.TryGetEntry(mat, out var matEntry))
                                    {
                                        if (matEntry is ExportEntry exp)
                                            materials.Add(exp);
                                        else if (matEntry is ImportEntry imp &&
                                                 EntryImporter.TryResolveImport(imp, out var resolvedExp, cache))
                                            materials.Add(resolvedExp);
                                    }
                                }
                            }

                            break;
                        }
                    case "SkeletalMesh":
                        {
                            var bin = ObjectBinary.From<SkeletalMesh>(mdl);
                            meshBounds = bin.Bounds;

                            if (!texelFactorCache.TryGetValue(mdl, out meshTexelFactor))
                            {
                                meshTexelFactor = ComputeSkeletalMeshTexelFactor(bin);
                                texelFactorCache[mdl] = meshTexelFactor;
                            }

                            if (materials.Count == 0)
                            {
                                foreach (var mat in bin.Materials)
                                {
                                    if (mdl.FileRef.TryGetEntry(mat, out var matEntry))
                                    {
                                        if (matEntry is ExportEntry exp)
                                            materials.Add(exp);
                                        else if (matEntry is ImportEntry imp &&
                                                 EntryImporter.TryResolveImport(imp, out var resolvedExp, cache))
                                            materials.Add(resolvedExp);
                                    }
                                }
                            }

                            break;
                        }
                    default:
                        continue; // Not supported by this code.
                }

                // We now have the list of materials.
                // Get the component's actual world transform (translation, rotation, and scale) -
                // not just its location, since bounds and texel factor both depend on scale.
                var worldMatrix = GetComponentWorldTransform(component);

                if (worldMatrix.Translation is { X: 0, Y: 0, Z: 0 })
                    continue;

                float streamingDistanceMultiplier = props.GetProp<FloatProperty>("StreamingDistanceMultiplier")?.Value ?? 1f;
                var worldBounds = meshBounds.TransformBy(worldMatrix);
                float worldTexelFactor = meshTexelFactor * streamingDistanceMultiplier * GetMaximumAxisScale(worldMatrix);

                // Get list of textures on the materials.
                foreach (var mat in materials.Distinct())
                {
                    texturesOnMesh.AddRange(MaterialInstanceConstant.GetTextures(mat, cache, true));
                }

                // Jiminy crickets this is complicated
                // Add imports for all materials so we can reference them if they are in higher tier packages.
                foreach (var tex in texturesOnMesh.ToList())
                {
                    if (tex.ClassName.CaseInsensitiveEquals("TextureCube"))
                    {
                        // Texture cubes are not streamable
                        texturesOnMesh.Remove(tex);
                        continue;
                    }

                    if (tex is ExportEntry tExp)
                    {
                        bool validatePackage = true;
                        if (tExp.GetProperty<BoolProperty>("NeverStream")?.Value == true)
                        {
                            // This texture is not streamed
                            texturesOnMesh.Remove(tex);
                            continue;
                        }
                        var t2d = ObjectBinary.From<UTexture2D>(tExp, cache);
                        if (t2d.Mips.Count <= 6)
                        {
                            // This texture is not streamed.
                            texturesOnMesh.Remove(tex);
                            continue;
                        }

                        if (tex.FileRef != package)
                        {
                            // We must resolve this to local object
                            var existingEntry = package.FindEntry(tex.InstancedFullPath, tex.ClassName);
                            if (existingEntry != null)
                            {
                                texturesOnMesh.Remove(tex);
                                texturesOnMesh.Add(existingEntry);
                                validatePackage = false;
                            }
                            else
                            {
                                // Not found, and in another package
                                var parent = EntryExporter.PortParents(tex, package, true); // Really dicey. This method needs improved
                                ImportEntry imp = new ImportEntry(tExp, parent.UIndex, package);
                                package.AddImport(imp);

                                // Replace the mesh 
                                texturesOnMesh.Remove(tex);
                                texturesOnMesh.Add(imp);
#if DEBUG
                                if (!imp.IsA("Texture"))
                                {
                                    // THIS IS NOT A GOOD SIGN!
                                    Debugger.Break();
                                }
#endif
                                validatePackage = false;
                            }
                        }

                        // We're gonna crash game if we use this
                        if (tex.FileRef != package && validatePackage)
                            Debugger.Break();
                    }
                    else if (tex is ImportEntry tImp)
                    {
                        if (tImp.FileRef != package)
                        {
                            // This is an import in another package. We must copy it over.
                            var impParent = EntryExporter.PortParents(tImp, package, true);
                            var newImp = EntryImporter.GetOrAddCrossImportOrPackage(tImp.InstancedFullPath, tImp.FileRef, package, new RelinkerOptionsPackage(), impParent.UIndex);
                            texturesOnMesh.Remove(tex);
                            texturesOnMesh.Add(newImp);
#if DEBUG
                            if (!newImp.IsA("Texture"))
                            {
                                // THIS IS NOT A GOOD SIGN!
                                Debugger.Break();
                            }
#endif
                        }
                    }
                }

#if DEBUG
                // VALIDATION
                foreach (var tex in texturesOnMesh)
                {
                    if (!tex.IsA("Texture"))
                    {
                        // THIS IS NOT A GOOD SIGN!
                        Debugger.Break();
                    }
                }
#endif

                // Add streaming instances.
                foreach (var tex in texturesOnMesh)
                {
                    // Final validation
                    if (tex.FileRef != package)
                        Debugger.Break();

                    if (!textureToInstancesMap.TryGetValue(tex, out var stil))
                    {
                        stil = new List<StreamableTextureInstance>();
                        textureToInstancesMap[tex] = stil;
                    }

                    stil.Add(new StreamableTextureInstance()
                    {
                        BoundingSphere = new Sphere()
                        {
                            Center = worldBounds.Origin,
                            W = worldBounds.SphereRadius
                        },
                        TexelFactor = worldTexelFactor
                    });
                }
            }
            // We now have location and list of textures
            // Build the map.

            var levelExp = package.GetLevel();
            var level = package.GetLevelBinary();

            level.TextureToInstancesMap = new UMultiMap<int, StreamableTextureInstanceList>(textureToInstancesMap.Count);
            foreach (var tex in textureToInstancesMap)
            {
                level.TextureToInstancesMap[tex.Key.UIndex] = new StreamableTextureInstanceList()
                {
                    Instances = tex.Value.ToArray()
                };
            }

            levelExp.WriteBinary(level);

        }

        /// <summary>
        /// Gets the LocalToWorld transformation matrix for the component - either the actor it's attached to,
        /// or its per-instance transform if it's part of a collection actor (StaticMeshCollectionActor/StaticLightCollectionActor).
        /// </summary>
        private static Matrix4x4 GetComponentWorldTransform(ExportEntry component)
        {
            if (component.HasParent)
            {
                if (component.IsA("StaticMeshComponent") && component.Parent.IsA("StaticMeshCollectionActor")
                 || component.IsA("LightComponent") && component.Parent.IsA("StaticLightCollectionActor"))
                {
                    // collection actor
                    if (StaticCollectionActor.TryGetStaticCollectionActorAndIndex(component,
                            out StaticCollectionActor sca, out int index))
                    {
                        return sca.LocalToWorldTransforms[index];
                    }
                }
                else if (component.Parent is ExportEntry actor)
                {
                    // Use parent of component, which should be the actor
                    return ActorUtils.GetLocalToWorld(actor);
                }
            }

            // Unknown transform... components should always be nested
            Debugger.Break();
            return Matrix4x4.Identity;
        }

        /// <summary>
        /// Equivalent of UE3's FMatrix::GetMaximumAxisScale() - the largest scale factor applied by any of the matrix's axes.
        /// </summary>
        private static float GetMaximumAxisScale(Matrix4x4 m)
        {
            float x = m.M11 * m.M11 + m.M12 * m.M12 + m.M13 * m.M13;
            float y = m.M21 * m.M21 + m.M22 * m.M22 + m.M23 * m.M23;
            float z = m.M31 * m.M31 + m.M32 * m.M32 + m.M33 * m.M33;
            return MathF.Sqrt(Math.Max(x, Math.Max(y, z)));
        }

        // SMALL_NUMBER squared, matching UE3's Square(SMALL_NUMBER) degenerate-UV check.
        private const float SmallNumberSquared = 1e-8f * 1e-8f;

        /// <summary>
        /// Equivalent of UE3's UStaticMesh::GetStreamingTextureFactor(0) - the local-space "world units per UV unit"
        /// ratio used to decide how far away a texture needs to start streaming in at higher resolution.
        /// </summary>
        private static float ComputeStaticMeshTexelFactor(StaticMesh mesh)
        {
            if (mesh.LODModels.Length == 0)
                return 0f;

            var lod = mesh.LODModels[0];
            var positions = lod.PositionVertexBuffer.VertexData;
            var indices = lod.IndexBuffer;
            var vb = lod.VertexBuffer;

            var texelRatios = new List<float>();
            foreach (var element in lod.Elements)
            {
                for (uint t = 0; t < element.NumTriangles; t++)
                {
                    uint baseIndex = element.FirstIndex + t * 3;
                    if (baseIndex + 2 >= indices.Length)
                        break; // Element metadata doesn't match this mesh's index buffer - stop rather than throw.

                    int i0 = indices[(int)baseIndex];
                    int i1 = indices[(int)baseIndex + 1];
                    int i2 = indices[(int)baseIndex + 2];

                    if (i0 >= positions.Length || i1 >= positions.Length || i2 >= positions.Length)
                        continue;

                    float l1 = Vector3.Distance(positions[i0], positions[i1]);
                    float l2 = Vector3.Distance(positions[i0], positions[i2]);

                    Vector2 uv0 = GetVertexUV(vb, i0);
                    Vector2 uv1 = GetVertexUV(vb, i1);
                    Vector2 uv2 = GetVertexUV(vb, i2);
                    float t1 = Vector2.Distance(uv0, uv1);
                    float t2 = Vector2.Distance(uv0, uv2);

                    if (MathF.Abs(t1 * t2) > SmallNumberSquared)
                    {
                        texelRatios.Add(MathF.Max(l1 / t1, l2 / t2));
                    }
                }
            }

            return PercentileTexelRatio(texelRatios);
        }

        private static Vector2 GetVertexUV(StaticMeshVertexBuffer vb, int vertexIndex)
        {
            var vertex = vb.VertexData[vertexIndex];
            if (vb.bUseFullPrecisionUVs)
                return vertex.FullPrecisionUVs[0];
            var uv = vertex.HalfPrecisionUVs[0];
            return new Vector2(uv.X, uv.Y);
        }

        /// <summary>
        /// Equivalent of UE3's USkeletalMesh::GetStreamingTextureFactor(0).
        /// </summary>
        private static float ComputeSkeletalMeshTexelFactor(SkeletalMesh mesh)
        {
            if (mesh.LODModels.Length == 0)
                return 0f;

            var lod = mesh.LODModels[0];
            var indices = lod.IndexBuffer;
            var vertexData = lod.VertexBufferGPUSkin.VertexData;

            var texelRatios = new List<float>();
            foreach (var section in lod.Sections)
            {
                for (int t = 0; t < section.NumTriangles; t++)
                {
                    uint baseIndex = section.BaseIndex + (uint)(t * 3);
                    if (baseIndex + 2 >= indices.Length)
                        break; // Section metadata doesn't match this mesh's index buffer - stop rather than throw.

                    int i0 = indices[(int)baseIndex];
                    int i1 = indices[(int)baseIndex + 1];
                    int i2 = indices[(int)baseIndex + 2];

                    if (i0 >= vertexData.Length || i1 >= vertexData.Length || i2 >= vertexData.Length)
                        continue;

                    float l1 = Vector3.Distance(vertexData[i0].Position, vertexData[i1].Position);
                    float l2 = Vector3.Distance(vertexData[i0].Position, vertexData[i2].Position);

                    Vector2 uv0 = new Vector2(vertexData[i0].UV.X, vertexData[i0].UV.Y);
                    Vector2 uv1 = new Vector2(vertexData[i1].UV.X, vertexData[i1].UV.Y);
                    Vector2 uv2 = new Vector2(vertexData[i2].UV.X, vertexData[i2].UV.Y);
                    float t1 = Vector2.Distance(uv0, uv1);
                    float t2 = Vector2.Distance(uv0, uv2);

                    if (MathF.Abs(t1 * t2) > SmallNumberSquared)
                    {
                        texelRatios.Add(MathF.Max(l1 / t1, l2 / t2));
                    }
                }
            }

            return PercentileTexelRatio(texelRatios);
        }

        // Disregards the upper 25% of texel ratios (backfacing/non-visible surfaces that map a small
        // number of texels to a large area) by taking the value at the 75th percentile, same as the engine.
        private static float PercentileTexelRatio(List<float> texelRatios)
        {
            if (texelRatios.Count == 0)
                return 0f;

            texelRatios.Sort();
            int idx = Math.Clamp((int)(texelRatios.Count * 0.75f), 0, texelRatios.Count - 1);
            return texelRatios[idx];
        }

        // Location based stuff
        public static Point3D GetLocation(ExportEntry export)
        {
            float x = 0, y = 0, z = int.MinValue;
            if (export.ClassName.Contains("Component") && export.HasParent && export.Parent.ClassName.Contains("CollectionActor"))  //Collection component
            {
                var actorCollection = export.Parent as ExportEntry;
                var collection = GetCollectionItems(actorCollection);

                if (!(collection?.IsEmpty() ?? true))
                {
                    var positions = GetCollectionLocationData(actorCollection);
                    var idx = collection.FindIndex(o => o != null && o.UIndex == export.UIndex);
                    if (idx >= 0)
                    {
                        return new Point3D(positions[idx].X, positions[idx].Y, positions[idx].Z);
                    }
                }

            }
            else
            {
                var prop = export.GetProperty<StructProperty>("location");
                if (prop != null)
                {
                    foreach (var locprop in prop.Properties)
                    {
                        switch (locprop)
                        {
                            case FloatProperty fltProp when fltProp.Name == "X":
                                x = fltProp;
                                break;
                            case FloatProperty fltProp when fltProp.Name == "Y":
                                y = fltProp;
                                break;
                            case FloatProperty fltProp when fltProp.Name == "Z":
                                z = fltProp;
                                break;
                        }
                    }
                    return new Point3D(x, y, z);
                }
            }
            return new Point3D(0, 0, 0);
        }

        public static void SetLocation(ExportEntry export, float x, float y, float z)
        {
            if (export.ClassName.Contains("Component"))
            {
                SetCollectionActorLocation(export, x, y, z);
            }
            else
            {
                export.WriteProperty(CommonStructs.Vector3Prop(x, y, z, "location"));
            }
        }

        public static void SetRotation(ExportEntry export, int pitch, int yaw, int roll)
        {
            if (export.ClassName.Contains("Component"))
            {
                SetCollectionActorRotation(export, pitch, yaw, roll);
            }
            else
            {
                export.WriteProperty(CommonStructs.RotatorProp(pitch, yaw, roll, "Rotation"));
            }
        }

        public static void SetLocation(ExportEntry export, Point3D point)
        {
            if (export.ClassName.Contains("Component"))
            {
                SetCollectionActorLocation(export, point.X, point.Y, point.Z);
            }
            else
            {
                export.WriteProperty(CommonStructs.Vector3Prop(point.X, point.Y, point.Z, "location"));
            }
        }

        public static void SetLocation(StructProperty prop, float x, float y, float z)
        {
            prop.GetProp<FloatProperty>("X").Value = x;
            prop.GetProp<FloatProperty>("Y").Value = y;
            prop.GetProp<FloatProperty>("Z").Value = z;
        }

        public static void SetCollectionActorLocation(ExportEntry component, float x, float y, float z, List<ExportEntry> collectionitems = null, ExportEntry collectionactor = null)
        {
            if (collectionactor == null)
            {
                if (!(component.HasParent && component.Parent.ClassName.Contains("CollectionActor")))
                    return;
                collectionactor = (ExportEntry)component.Parent;
            }

            collectionitems ??= GetCollectionItems(collectionactor);

            if (collectionitems?.Count > 0)
            {
                var idx = collectionitems.FindIndex(o => o != null && o.UIndex == component.UIndex);
                if (idx >= 0)
                {
                    var binData = (StaticCollectionActor)ObjectBinary.From(collectionactor);

                    Matrix4x4 m = binData.LocalToWorldTransforms[idx];
                    m.Translation = new Vector3(x, y, z);
                    binData.LocalToWorldTransforms[idx] = m;

                    collectionactor.WriteBinary(binData);
                }
            }
        }

        public static void SetCollectionActorRotation(ExportEntry component, int pitch, int yaw, int roll, List<ExportEntry> collectionitems = null, ExportEntry collectionactor = null)
        {
            if (collectionactor == null)
            {
                if (!(component.HasParent && component.Parent.ClassName.Contains("CollectionActor")))
                    return;
                collectionactor = (ExportEntry)component.Parent;
            }

            collectionitems ??= GetCollectionItems(collectionactor);

            if (collectionitems?.Count > 0)
            {
                var idx = collectionitems.FindIndex(o => o != null && o.UIndex == component.UIndex);
                if (idx >= 0)
                {
                    var binData = (StaticCollectionActor)ObjectBinary.From(collectionactor);

                    Matrix4x4 m = binData.LocalToWorldTransforms[idx];
                    var dsd = m.UnrealDecompose();
                    dsd.rotation = new Rotator(pitch, yaw, roll);
                    binData.LocalToWorldTransforms[idx] = ActorUtils.ComposeLocalToWorld(dsd);

                    collectionactor.WriteBinary(binData);
                }
            }
        }





        #region DRAWSCALE
        public static void SetDrawScale3D(ExportEntry export, float x, float y, float z)
        {
            if (export.ClassName.Contains("Component"))
            {
                SetCollectionActorDrawScale3D(export, x, y, z);
            }
            else
            {
                export.WriteProperty(CommonStructs.Vector3Prop(x, y, z, "DrawScale3D"));
            }
        }

        public static void SetCollectionActorDrawScale3D(ExportEntry component, float x, float y, float z, List<ExportEntry> collectionitems = null, ExportEntry collectionactor = null)
        {
            if (collectionactor == null)
            {
                if (!(component.HasParent && component.Parent.ClassName.Contains("CollectionActor")))
                    return;
                collectionactor = (ExportEntry)component.Parent;
            }

            collectionitems ??= GetCollectionItems(collectionactor);

            if (collectionitems?.Count > 0)
            {
                var idx = collectionitems.FindIndex(o => o != null && o.UIndex == component.UIndex);
                if (idx >= 0)
                {
                    var binData = (StaticCollectionActor)ObjectBinary.From(collectionactor);
                    Matrix4x4 m = binData.LocalToWorldTransforms[idx];
                    var dsd = m.UnrealDecompose();
                    binData.LocalToWorldTransforms[idx] = ActorUtils.ComposeLocalToWorld(dsd.translation, dsd.rotation, new Vector3(x, y, z));
                    collectionactor.WriteBinary(binData);
                }
            }
        }
        #endregion


        #region COLLECTION ACTORS
        public static List<Point3D> GetCollectionLocationData(ExportEntry collectionactor)
        {
            if (!collectionactor.ClassName.Contains("CollectionActor"))
                return null;

            return ((StaticCollectionActor)ObjectBinary.From(collectionactor)).LocalToWorldTransforms
                                                                              .Select(localToWorldTransform => (Point3D)localToWorldTransform.Translation).ToList();
        }

        public static List<ExportEntry> GetCollectionItems(ExportEntry smac)
        {
            var collectionItems = new List<ExportEntry>();
            var smacItems = smac.GetProperty<ArrayProperty<ObjectProperty>>(smac.ClassName == "StaticMeshCollectionActor" ? "StaticMeshComponents" : "LightComponents");
            if (smacItems != null)
            {
                //Read exports...
                foreach (ObjectProperty obj in smacItems)
                {
                    if (obj.Value > 0)
                    {
                        ExportEntry item = smac.FileRef.GetUExport(obj.Value);
                        collectionItems.Add(item);
                    }
                    else
                    {
                        //this is a blank entry, or an import, somehow.
                        collectionItems.Add(null);
                    }
                }
                return collectionItems;
            }
            return null;
        }
        #endregion

        public static void RebuildPersistentLevelChildren(ExportEntry pl)
        {
            ExportEntry[] actorsToAdd = pl.FileRef.Exports.Where(exp => exp.Parent == pl && exp.IsA("Actor")).ToArray();
            Level level = ObjectBinary.From<Level>(pl);
            level.Actors.Clear();
            foreach (var actor in actorsToAdd)
            {
                var lc = actor.GetProperty<ObjectProperty>("LightComponent");
                if (lc != null && pl.FileRef.TryGetUExport(lc.Value, out var lightComp))
                {
                    if (lightComp.Parent != null && lightComp.Parent.ClassName == "StaticLightCollectionActor")
                        continue; // don't add this one
                }

                level.Actors.Add(actor.UIndex);
            }

            // MUST HAVE AT LEAST 2 ITEMS
            // Insert empty brush
            if (pl.Game is > MEGame.ME1 and < MEGame.UDK)
            {
                level.Actors.Insert(1, 0);
            }

            pl.WriteBinary(level);
        }
    }
}
