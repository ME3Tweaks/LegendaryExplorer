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
using System.Text;
using System.Threading.Tasks;
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

            // Look at all components
            foreach (var component in package.Exports.Where(x => x.IsA("Component")))
            {
                // Only do ones in the world
                if (!component.GetRootName().CaseInsensitiveEquals("TheWorld"))
                    continue;

                ObjectProperty meshProp = component.GetProperty<ObjectProperty>("StaticMesh");
                meshProp ??= component.GetProperty<ObjectProperty>("SkeletalMesh");

                if (meshProp == null)
                    continue; // Not supported by this code.

                // List of materials this mesh uses
                List<IEntry> texturesOnMesh = new List<IEntry>();
                List<ExportEntry> materials = new List<ExportEntry>();
                var matList = component.GetProperty<ArrayProperty<ObjectProperty>>("Materials");
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

                // if we don't have any materials set on component, we have to pull it off the mesh.
                // resolve the mesh export
                if (materials.Count == 0 && meshProp.TryResolveExport(package, cache, out var mdl))
                {
                    switch (mdl.ClassName)
                    {
                        case "StaticMesh":
                            {
                                var bin = ObjectBinary.From<StaticMesh>(mdl);
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

                                break;
                            }
                        case "SkeletalMesh":
                            {
                                var bin = ObjectBinary.From<SkeletalMesh>(mdl);
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

                                break;
                            }
                    }
                }

                // We now have the list of materials.
                // Get actor location.
                var actorLocation = GetActorLocationFromComponent(component);

                if (actorLocation is { X: 0, Y: 0, Z: 0 })
                    continue;

                // Get list of textures on the materials.
                foreach (var mat in materials.Distinct())
                {
                    var parsedMat = new MaterialInstanceConstant(mat, cache, true);
                    texturesOnMesh.AddRange(parsedMat.Textures);
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
                            var newImp = EntryExporter.PortParents(tImp, package, true);
                            texturesOnMesh.Remove(tex);
                            texturesOnMesh.Add(newImp);
                        }
                    }
                }

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
                            Center = new Vector3(actorLocation.X, actorLocation.Y, actorLocation.Z),
                            W = 50 // Radius. I guess we could technically take the size of the mesh. But do I care that much?
                        },
                        TexelFactor = 50 // I have no ideal how to calcluate this
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

        // Todo: Merge all the uses of these.
        private static Point3D GetActorLocationFromComponent(ExportEntry exportEntry)
        {
            // I don't like working with structs.
            // So we use Point3D instead.
            if (exportEntry.HasParent && (exportEntry.IsA("StaticMeshComponent")
                                          && exportEntry.Parent.IsA("StaticMeshCollectionActor")
                                          || exportEntry.IsA("LightComponent") &&
                                          exportEntry.Parent.IsA("StaticLightCollectionActor")))
            {
                // collection actor
                if (StaticCollectionActor.TryGetStaticCollectionActorAndIndex(exportEntry,
                        out StaticCollectionActor sca, out int index))
                {
                    float PosX;
                    float PosY;
                    float PosZ;
                    ((PosX, PosY, PosZ), (_, _, _), (_, _, _)) = sca.GetDecomposedTransformationForIndex(index);
                    return new Point3D(PosX, PosY, PosZ);
                }
            }
            else
            {
                return LevelTools.GetLocation(exportEntry);
            }

            // Unknown location...
            Debugger.Break();
            return new Point3D();
        }

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
    }
}
