using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal;
using System.Numerics;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System.Threading;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Localization;

namespace LegendaryExplorerCore.UDK
{
    public static class ConvertToUDK
    {
        public static string GenerateUDKFileForLevel(IMEPackage pcc)
        {
            #region AssetPackage

            string meshPackageName = $"{Path.GetFileNameWithoutExtension(pcc.FilePath)}Meshes";
            string meshFile = Path.Combine(UDKDirectory.SharedPath, $"{meshPackageName}.upk");
            MEPackageHandler.CreateAndSavePackage(meshFile, MEGame.UDK);
            using IMEPackage meshPackage = MEPackageHandler.OpenUDKPackage(meshFile);
            meshPackage.GetEntryOrAddImport("Core.Package", "Class");

            IEntry defMat = meshPackage.GetEntryOrAddImport("EngineMaterials.DefaultMaterial", "Material", "Engine");
            var allMats = new HashSet<int>();
            var relinkerOptionsPackage = new RelinkerOptionsPackage() { Cache = new PackageCache() };
            ListenableDictionary<IEntry, IEntry> relinkMap = relinkerOptionsPackage.CrossPackageMap;
            PackageCache packageCache = relinkerOptionsPackage.Cache;
            #region StaticMeshes

            List<ExportEntry> staticMeshes = pcc.Exports.Where(exp => exp.ClassName == "StaticMesh").ToList();
            foreach (ExportEntry mesh in staticMeshes)
            {
                var mats = new Queue<int>();
                var stm = ObjectBinary.From<StaticMesh>(mesh);
                foreach (StaticMeshRenderData lodModel in stm.LODModels)
                {
                    foreach (StaticMeshElement meshElement in lodModel.Elements)
                    {
                        mats.Enqueue(meshElement.Material);
                        allMats.Add(meshElement.Material);
                        meshElement.Material = 0;
                    }
                }
                if (pcc.GetEntry(stm.BodySetup) is ExportEntry rbBodySetup)
                {
                    rbBodySetup.RemoveProperty("PhysMaterial");
                }
                mesh.WriteBinary(stm);

                // Change name so it doesn't use same names...
                var meshRoot = mesh.GetRootName();
                IEntry newParent = meshPackage.FindEntry(meshRoot);
                if (newParent == null)
                {
                    // Attempt to link up to ported content.
                    var matPath = Path.Combine(UDKDirectory.ContentPath, "Shared", $"{pcc.Game}MaterialPort", meshRoot + ".upk");
                    if (File.Exists(matPath))
                    {
                        newParent = new ImportEntry(meshPackage, null, meshRoot)
                        {
                            ClassName = "Package",
                            PackageFile = "Core",
                        };
                        meshPackage.AddImport(newParent as ImportEntry);
                    }
                    else
                    {
                        matPath = Path.Combine(UDKDirectory.ContentPath, "Shared", $"{pcc.Game.ToOppositeGeneration()}MaterialPort", meshRoot + ".upk");
                        if (File.Exists(matPath))
                        {
                            newParent = new ImportEntry(meshPackage, null, meshRoot)
                            {
                                ClassName = "Package",
                                PackageFile = "Core",
                            };
                            meshPackage.AddImport(newParent as ImportEntry);
                        }
                    }

                    if (newParent == null)
                    {
                        // Create export
                        newParent = EntryImporter.GetOrAddCrossImportOrPackage(mesh.ParentFullPath, pcc, meshPackage,
                            new RelinkerOptionsPackage(packageCache));
                    }
                }

                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild, mesh, meshPackage, newParent, false, relinkerOptionsPackage, out IEntry ent);
                var portedMesh = (ExportEntry)ent;
                stm = ObjectBinary.From<StaticMesh>(portedMesh);
                foreach (StaticMeshRenderData lodModel in stm.LODModels)
                {
                    foreach (StaticMeshElement meshElement in lodModel.Elements)
                    {
                        meshElement.Material = mats.Dequeue();
                    }
                }

                // UDK expects material indices to be correct
                // If they aren't, it will try to regenerate the vertex buffer, which sometimes result in 0 vertices in UDK
                // LE compiler didn't seem to care for some reason so it isn't always accurate
                int matIndex = 0;
                foreach (StaticMeshRenderData lodModel in stm.LODModels)
                {
                    foreach (StaticMeshElement meshElement in lodModel.Elements)
                    {
                        meshElement.MaterialIndex = matIndex++;
                    }
                }

                portedMesh.WriteBinary(stm);
            }

            #endregion

            #region Materials
            using (IMEPackage udkResources = MEPackageHandler.OpenMEPackageFromStream(LegendaryExplorerCoreUtilities.GetCustomAppResourceStream(MEGame.UDK)))
            {
                ExportEntry normDiffMat = udkResources.Exports.First(exp => exp.ObjectName == "NormDiffMat");
                foreach (int matUIndex in allMats)
                {
                    if (pcc.GetEntry(matUIndex) is ExportEntry matExp)
                    {
                        if (pcc.Game.IsLEGame() && matExp.IsForcedExport)
                        {
                            // Attempt to link up to ported content.
                            var matPath = Path.Combine(UDKDirectory.ContentPath, "Shared", $"{pcc.Game}MaterialPort", matExp.GetRootName() + ".upk");
                            if (File.Exists(matPath))
                            {
                                // Quickload; test if it exists
                                var upk = MEPackageHandler.UnsafePartialLoad(matPath, x => false);
                                // This is ugly hack but it's fast.
                                var foundRef = upk.FindExport(matExp.InstancedFullPath.Substring(matExp.GetRootName().Length + 1), matExp.ClassName);
                                if (foundRef != null)
                                {
                                    // Debug.WriteLine("Using repointed material from ME1 materials port");
                                    // Exists - generate import for this
                                    var repointedMat = CreateImportsFor(foundRef, meshPackage);
                                    relinkMap[matExp] = repointedMat;
                                    continue;
                                }
                            }

                            // Try the opposite generation
                            matPath = Path.Combine(UDKDirectory.ContentPath, "Shared", $"{pcc.Game.ToOppositeGeneration()}MaterialPort", matExp.GetRootName() + ".upk");
                            if (File.Exists(matPath))
                            {
                                // Quickload; test if it exists
                                var upk = MEPackageHandler.UnsafePartialLoad(matPath, x => false);
                                // This is ugly hack but it's fast.
                                var foundRef = upk.FindExport(matExp.InstancedFullPath.Substring(matExp.GetRootName().Length + 1), matExp.ClassName);
                                if (foundRef != null)
                                {
                                    // Debug.WriteLine("Using repointed material from ME1 materials port");
                                    // Exists - generate import for this
                                    var repointedMat = CreateImportsFor(foundRef, meshPackage);
                                    relinkMap[matExp] = repointedMat;
                                    continue;
                                }
                            }
                        }

                        List<IEntry> textures = new MaterialInstanceConstant(matExp, packageCache).Textures;
                        ExportEntry diff = null;
                        ExportEntry norm = null;
                        foreach (IEntry texEntry in textures)
                        {
                            if (texEntry is ExportEntry texport)
                            {
                                if (texport.ObjectName.Name.Contains("diff", StringComparison.OrdinalIgnoreCase))
                                {
                                    diff = texport;
                                }
                                else if (texport.ObjectName.Name.Contains("norm", StringComparison.OrdinalIgnoreCase))
                                {
                                    norm = texport;
                                }
                            }
                        }

                        static ExportEntry ImportTexture(ExportEntry texport, IMEPackage meshPackage, PackageCache packageCache)
                        {
                            if (meshPackage.FindExport(texport.ObjectName.Instanced) is ExportEntry existingTexture)
                            {
                                return existingTexture;
                            }
                            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, texport, meshPackage, null, false, new RelinkerOptionsPackage(packageCache), out IEntry ent);
                            var importedExport = (ExportEntry)ent;
                            PropertyCollection properties = importedExport.GetProperties();
                            properties.RemoveNamedProperty("TextureFileCacheName");
                            properties.RemoveNamedProperty("TFCFileGuid");
                            properties.RemoveNamedProperty("LODGroup");
                            if (texport.GetProperty<EnumProperty>("Format") is { Value.Name: "PF_BC7" })
                            {
                                var convertedImage = new Texture2D(texport).ToImage(PixelFormat.DXT1);
                                properties.AddOrReplaceProp(new EnumProperty("PF_DXT1", "EPixelFormat", MEGame.UDK, "Format"));
                                properties.AddOrReplaceProp(new EnumProperty("TC_NormalMap", "TextureCompressionSettings", MEGame.UDK, "CompressionSettings"));
                                new Texture2D(importedExport) { TextureFormat = "PF_DXT1" }.Replace(convertedImage, properties, isPackageStored: true);
                            }
                            else
                            {
                                importedExport.WriteProperties(properties);
                            }
                            return importedExport;
                        }

                        if (diff == null)
                        {
                            relinkMap[matExp] = defMat;
                            continue;
                        }
                        diff = ImportTexture(diff, meshPackage, packageCache);
                        if (norm != null)
                        {
                            norm = ImportTexture(norm, meshPackage, packageCache);
                        }

                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild, normDiffMat, meshPackage, null, true, new RelinkerOptionsPackage(packageCache), out IEntry matEnt);
                        var newMat = (ExportEntry)matEnt;
                        newMat.ObjectName = matExp.ObjectName;
                        var matBin = ObjectBinary.From<Material>(newMat);
                        matBin.SM3MaterialResource.UniformExpressionTextures = new[] { norm?.UIndex ?? 0, diff.UIndex };
                        newMat.WriteBinary(matBin);
                        relinkMap[matExp] = newMat;
                        if (newMat.GetProperty<ArrayProperty<ObjectProperty>>("Expressions") is { Count: >= 2 } expressionsProp)
                        {
                            ExportEntry diffExpression = meshPackage.GetUExport(expressionsProp[0].Value);
                            ExportEntry normExpression = meshPackage.GetUExport(expressionsProp[1].Value);
                            diffExpression.WriteProperty(new ObjectProperty(diff.UIndex, "Texture"));
                            normExpression.WriteProperty(new ObjectProperty(norm?.UIndex ?? 0, "Texture"));
                        }
                    }
                    else if (pcc.GetEntry(matUIndex) is ImportEntry matImp)
                    {
                        relinkMap[matImp] = defMat;
                    }
                }

                foreach (ExportEntry stmExport in staticMeshes)
                {
                    if (relinkMap.TryGetValue(stmExport, out IEntry destEnt) && destEnt is ExportEntry destExp)
                    {
                        Relinker.Relink(stmExport, destExp, relinkerOptionsPackage);
                    }
                }
            }
            #endregion

            // This is absolutely horrendous performance. We should use EntryTree
            foreach (var export in meshPackage.Exports.OrderBy(x => x.InstancedFullPath.Length).ToList())
            {
                var parent = export.Parent;
                while (parent != null)
                {
                    var nextParent = parent.Parent; // cache cause trashing will change parents
                    if (parent is ImportEntry imp)
                    {
                        var resolved = EntryImporter.ResolveImport(imp, null, unsafeLoad: true);
                        if (resolved == null)
                        {
                            // File is not safe to import from
                            ConvertPackageImportToExport(imp, nextParent == null);
                        }
                        else
                        {
                            var children = export.FileRef.Exports.Where(x => x.idxLink == parent.UIndex);
                            foreach (var child in children)
                            {
                                // Mark forced export... dunno if this is allowed in UDK
                                // its not
                                // Todo: DO NOT DO THIS!
                                child.ExportFlags |= UnrealFlags.EExportFlags.ForcedExport;
                            }
                        }
                    }
                    parent = nextParent;
                }
            }


            // Move all static meshes to consistent package system
            foreach (var exp in meshPackage.Exports.Where(x => x.ClassName == "StaticMesh"/* && x.Parent != null && x.Parent.Parent == null*/).ToList())
            {
                var newParent = meshPackage.FindExport("UDKifyMeshes");
                if (newParent == null)
                {
                    newParent = ExportCreator.CreatePackageExport(meshPackage, "UDKifyMeshes", forcedExport: false);
                }

                exp.idxLink = newParent.UIndex;
            }

            relinkerOptionsPackage = null;
            (meshPackage as UDKPackage).FixupTrash();
            if (meshPackage.Exports.Any())
            {
                meshPackage.Save();
            }
            else
            {
                // Do not save a file that has no exports.
                File.Delete(meshFile);
            }

            #endregion

            var staticMeshActors = new List<ExportEntry>();
            var lightActors = new List<ExportEntry>();
            var terrains = new List<ExportEntry>();
            using IMEPackage tempPackage = MEPackageHandler.OpenMEPackageFromStream(MEPackageHandler.CreateEmptyLevelStream(Path.GetFileNameWithoutExtension(pcc.FilePath), MEGame.UDK), pcc.FileNameNoExtension + ".udk");
            {
                var topLevelMeshPackages = new List<IEntry>();
                foreach (ExportEntry exportEntry in staticMeshes)
                {
                    IEntry imp = tempPackage.GetEntryOrAddImport(exportEntry.InstancedFullPath, "StaticMesh", "Engine");
                    while (imp.Parent != null)
                    {
                        imp = imp.Parent;
                    }
                    if (!topLevelMeshPackages.Contains(imp))
                    {
                        topLevelMeshPackages.Add(imp);
                    }
                }

                ExportEntry levelExport = tempPackage.Exports.First(exp => exp.ClassName == "Level");
                List<int> actorsInLevel = ObjectBinary.From<Level>(pcc.Exports.First(exp => exp.ClassName == "Level")).Actors.ToList();
                var componentToMatrixMap = new Dictionary<int, Matrix4x4>();
                foreach (int uIndex in actorsInLevel)
                {
                    if (pcc.GetEntry(uIndex) is ExportEntry stcExp)
                    {
                        if (stcExp.ClassName == "StaticMeshCollectionActor")
                        {
                            StaticMeshCollectionActor stmc = ObjectBinary.From<StaticMeshCollectionActor>(stcExp);
                            var components = stcExp.GetProperty<ArrayProperty<ObjectProperty>>("StaticMeshComponents");
                            for (int i = 0; i < components.Count; i++)
                            {
                                componentToMatrixMap[components[i].Value] = stmc.LocalToWorldTransforms[i];
                            }
                        }
                        else if (stcExp.ClassName == "StaticLightCollectionActor")
                        {
                            StaticLightCollectionActor stlc = ObjectBinary.From<StaticLightCollectionActor>(stcExp);
                            var components = stcExp.GetProperty<ArrayProperty<ObjectProperty>>("LightComponents");
                            for (int i = 0; i < components.Count; i++)
                            {
                                componentToMatrixMap[components[i].Value] = stlc.LocalToWorldTransforms[i];
                            }
                        }
                    }
                }

                #region StaticMeshActors
                {
                    var emptySMCBin = new StaticMeshComponent();
                    IEntry staticMeshActorClass = tempPackage.GetEntryOrAddImport("Engine.StaticMeshActor", "Class");
                    tempPackage.GetEntryOrAddImport("Engine.Default__StaticMeshActor", "StaticMeshActor", "Engine");
                    IEntry staticMeshComponentArchetype = tempPackage.GetEntryOrAddImport("Engine.Default__StaticMeshActor.StaticMeshComponent0",
                                                                                         "StaticMeshComponent", "Engine");
                    int smaIndex = 2;
                    int smcIndex = 2;
                    foreach (ExportEntry smc in pcc.Exports.Where(exp => exp.ClassName == "StaticMeshComponent"))
                    {
                        if (smc.Parent is ExportEntry parent && actorsInLevel.Contains(parent.UIndex) && parent.IsA("StaticMeshActorBase"))
                        {
                            var originalIFP = smc.InstancedFullPath;

                            // List of things to not port
                            if (parent.IsA("BioLedgeMeshActor"))
                                continue; // Don't port these, they are not really useful in UDK for lighting

                            StructProperty locationProp;
                            StructProperty rotationProp;
                            StructProperty scaleProp = null;
                            smc.CondenseArchetypes();
                            if (smc.GetProperty<ObjectProperty>("StaticMesh") is not { } meshProp || !pcc.IsUExport(meshProp.Value))
                            {
                                continue;
                            }

                            smc.WriteBinary(emptySMCBin);
                            smc.RemoveProperty("bBioIsReceivingDecals");
                            smc.RemoveProperty("bBioForcePrecomputedShadows");
                            //smc.RemoveProperty("bUsePreComputedShadows");
                            smc.RemoveProperty("bAcceptsLights");
                            smc.RemoveProperty("IrrelevantLights");
                            smc.RemoveProperty("Materials"); //should make use of this?
                            smc.ObjectName = new NameReference("StaticMeshComponent", smcIndex++);
                            if (parent.ClassName == "StaticMeshCollectionActor")
                            {
                                if (!componentToMatrixMap.TryGetValue(smc.UIndex, out Matrix4x4 m))
                                {
                                    continue;
                                }
                                (Vector3 posVec, Vector3 scaleVec, Rotator rotator) = m.UnrealDecompose();
                                locationProp = CommonStructs.Vector3Prop(posVec, "Location");
                                rotationProp = CommonStructs.RotatorProp(rotator, "Rotation");
                                scaleProp = CommonStructs.Vector3Prop(scaleVec, "DrawScale3D");
                                //smc.WriteProperty(CommonStructs.Matrix(m, "CachedParentToWorld"));
                            }
                            else
                            {
                                locationProp = parent.GetProperty<StructProperty>("Location");
                                rotationProp = parent.GetProperty<StructProperty>("Rotation");
                                scaleProp = parent.GetProperty<StructProperty>("DrawScale3D");
                                if (parent.GetProperty<FloatProperty>("DrawScale")?.Value is float scale)
                                {
                                    Vector3 scaleVec = Vector3.One;
                                    if (scaleProp != null)
                                    {
                                        scaleVec = CommonStructs.GetVector3(scaleProp);
                                    }
                                    scaleProp = CommonStructs.Vector3Prop(scaleVec * scale, "DrawScale3D");
                                }
                            }

                            var sma = new ExportEntry(tempPackage, levelExport, new NameReference("StaticMeshActor", smaIndex++), EntryImporter.CreateStack(MEGame.UDK, staticMeshActorClass.UIndex))
                            {
                                Class = staticMeshActorClass,
                            };
                            sma.ObjectFlags |= UnrealFlags.EObjectFlags.HasStack;
                            tempPackage.AddExport(sma);

                            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, smc, tempPackage,
                                                                 sma, true, new RelinkerOptionsPackage(packageCache), out IEntry result);
                            ((ExportEntry)result).Archetype = staticMeshComponentArchetype;
                            var props = new PropertyCollection
                            {
                                new ObjectProperty(result.UIndex, "StaticMeshComponent"),
                                // 08/24/2024 - Use InstancedFullPath instead. If the source file changes at all, static lighting import will not be reliable. IFP is more reliable at the cost of more names.
                                // We keep the smc.UIndex for extra info. The filename should match between ME<->UDK files.
                                // new NameProperty(new NameReference(Path.GetFileNameWithoutExtension(smc.FileRef.FilePath), smc.UIndex), "Tag"),
                                new NameProperty(new NameReference(originalIFP, smc.UIndex), "Tag"),
                                new ObjectProperty(result.UIndex, "CollisionComponent")
                            };
                            if (locationProp != null)
                            {
                                props.Add(locationProp);
                            }
                            if (rotationProp != null)
                            {
                                props.Add(rotationProp);
                            }

                            if (scaleProp != null)
                            {
                                props.Add(scaleProp);
                            }
                            sma.WriteProperties(props);
                            staticMeshActors.Add(sma);
                        }
                    }
                    IEntry topMeshPackageImport = tempPackage.GetEntryOrAddImport(meshPackageName, "Package");
                    foreach (IEntry mp in topLevelMeshPackages)
                    {
                        mp.Parent = topMeshPackageImport;
                        mp.ObjectName = "UDKifyMeshes";
                    }

                    foreach (var sm in tempPackage.Imports.Where(x => x.ClassName == "StaticMesh"))
                    {
                        sm.Parent = tempPackage.FindImport($"{meshPackageName}.UDKifyMeshes");
                    }
                }
                #endregion

                #region Terrain
                // Not sure if we need to port this into temp
                foreach (var t in pcc.Exports.Where(x => x.IsA("Terrain")))
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, t, tempPackage, levelExport, true, new RelinkerOptionsPackage(packageCache) { PortExportsAsImportsWhenPossible = true }, out IEntry result);
                    terrains.Add(result as ExportEntry);
                }
                #endregion

                #region LightActors
                {
                    IEntry pointLightClass = tempPackage.GetEntryOrAddImport("Engine.PointLight", "Class");
                    IEntry spotLightClass = tempPackage.GetEntryOrAddImport("Engine.SpotLight", "Class");
                    IEntry directionalLightClass = tempPackage.GetEntryOrAddImport("Engine.DirectionalLight", "Class");

                    int plaIndex = 1;
                    int plcIndex = 1;
                    int slaIndex = 1;
                    int slcIndex = 1;
                    int dlaIndex = 1;
                    int dlcIndex = 1;
                    foreach (ExportEntry lightComponent in pcc.Exports)
                    {
                        if (!(lightComponent.Parent is ExportEntry parent && actorsInLevel.Contains(parent.UIndex)))
                        {
                            continue;
                        }

                        var originalIFP = lightComponent.InstancedFullPath;

                        StructProperty locationProp;
                        StructProperty rotationProp;
                        StructProperty scaleProp;
                        switch (lightComponent.ClassName)
                        {
                            case "PointLightComponent":
                                lightComponent.CondenseArchetypes();
                                lightComponent.ObjectName = new NameReference("PointLightComponent", plcIndex++);
                                if (parent.ClassName == "StaticLightCollectionActor")
                                {
                                    if (!componentToMatrixMap.TryGetValue(lightComponent.UIndex, out Matrix4x4 m))
                                    {
                                        continue;
                                    }
                                    (Vector3 posVec, Vector3 scaleVec, Rotator rotator) = m.UnrealDecompose();
                                    locationProp = CommonStructs.Vector3Prop(posVec, "Location");
                                    rotationProp = CommonStructs.RotatorProp(rotator, "Rotation");
                                    scaleProp = CommonStructs.Vector3Prop(scaleVec, "DrawScale3D");
                                }
                                else
                                {
                                    locationProp = parent.GetProperty<StructProperty>("Location");
                                    rotationProp = parent.GetProperty<StructProperty>("Rotation");
                                    scaleProp = parent.GetProperty<StructProperty>("DrawScale3D");
                                    if (parent.GetProperty<FloatProperty>("DrawScale")?.Value is float scale)
                                    {
                                        Vector3 scaleVec = Vector3.One;
                                        if (scaleProp != null)
                                        {
                                            scaleVec = CommonStructs.GetVector3(scaleProp);
                                        }
                                        scaleProp = CommonStructs.Vector3Prop(scaleVec * scale, "DrawScale3D");
                                    }
                                }

                                var pla = new ExportEntry(tempPackage, levelExport, new NameReference("PointLight", plaIndex++), EntryImporter.CreateStack(MEGame.UDK, pointLightClass.UIndex))
                                {
                                    Class = pointLightClass,
                                };
                                pla.ObjectFlags |= UnrealFlags.EObjectFlags.HasStack;
                                pla.WriteProperty(new NameProperty(new NameReference(originalIFP, lightComponent.UIndex), "Tag"));

                                tempPackage.AddExport(pla);

                                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, lightComponent, tempPackage, pla, true, new RelinkerOptionsPackage(packageCache),
                                                                     out IEntry portedPLC);
                                var plsProps = new PropertyCollection
                                {
                                    new ObjectProperty(portedPLC.UIndex, "LightComponent"),
                                    new NameProperty("PointLight", "Tag"),
                                };
                                if (locationProp != null)
                                {
                                    plsProps.Add(locationProp);
                                }
                                if (rotationProp != null)
                                {
                                    plsProps.Add(rotationProp);
                                }
                                if (scaleProp != null)
                                {
                                    plsProps.Add(scaleProp);
                                }
                                pla.WriteProperties(plsProps);

                                var plce = portedPLC as ExportEntry;
                                plce.ObjectFlags |= UnrealFlags.EObjectFlags.Transactional;
                                plce.Archetype = tempPackage.GetEntryOrAddImport("Engine.Default__PointLight.PointLightComponent0", "PointLightComponent", "Engine");
                                lightActors.Add(pla);
                                break;
                            case "SpotLightComponent":
                                lightComponent.CondenseArchetypes();
                                lightComponent.ObjectName = new NameReference("SpotLightComponent", slcIndex++);
                                if (parent.ClassName == "StaticLightCollectionActor")
                                {
                                    if (!componentToMatrixMap.TryGetValue(lightComponent.UIndex, out Matrix4x4 m))
                                    {
                                        continue;
                                    }
                                    (Vector3 posVec, Vector3 scaleVec, Rotator rotator) = m.UnrealDecompose();
                                    locationProp = CommonStructs.Vector3Prop(posVec, "Location");
                                    rotationProp = CommonStructs.RotatorProp(rotator, "Rotation");
                                    scaleProp = CommonStructs.Vector3Prop(scaleVec, "DrawScale3D");
                                }
                                else
                                {
                                    locationProp = parent.GetProperty<StructProperty>("Location");
                                    rotationProp = parent.GetProperty<StructProperty>("Rotation");
                                    scaleProp = parent.GetProperty<StructProperty>("DrawScale3D");
                                    if (parent.GetProperty<FloatProperty>("DrawScale")?.Value is float scale)
                                    {
                                        Vector3 scaleVec = Vector3.One;
                                        if (scaleProp != null)
                                        {
                                            scaleVec = CommonStructs.GetVector3(scaleProp);
                                        }
                                        scaleProp = CommonStructs.Vector3Prop(scaleVec * scale, "DrawScale3D");
                                    }
                                }

                                var sla = new ExportEntry(tempPackage, levelExport, new NameReference("SpotLight", slaIndex++), EntryImporter.CreateStack(MEGame.UDK, spotLightClass.UIndex))
                                {
                                    Class = spotLightClass
                                };
                                sla.ObjectFlags |= UnrealFlags.EObjectFlags.HasStack;
                                sla.WriteProperty(new NameProperty(new NameReference(originalIFP, lightComponent.UIndex), "Tag"));
                                tempPackage.AddExport(sla);

                                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, lightComponent, tempPackage, sla, true, new RelinkerOptionsPackage(packageCache),
                                                                     out IEntry portedSLC);
                                var slaProps = new PropertyCollection
                                {
                                    new ObjectProperty(portedSLC.UIndex, "LightComponent"),
                                    new NameProperty("SpotLight", "Tag"),
                                };
                                if (locationProp != null)
                                {
                                    slaProps.Add(locationProp);
                                }
                                if (rotationProp != null)
                                {
                                    slaProps.Add(rotationProp);
                                }
                                if (scaleProp != null)
                                {
                                    slaProps.Add(scaleProp);
                                }
                                sla.WriteProperties(slaProps);
                                var slce = portedSLC as ExportEntry;
                                slce.ObjectFlags |= UnrealFlags.EObjectFlags.Transactional;
                                slce.Archetype = tempPackage.GetEntryOrAddImport("Engine.Default__SpotLight.SpotLightComponent0", "SpotLightComponent", "Engine");

                                lightActors.Add(sla);
                                break;
                            case "DirectionalLightComponent":
                                lightComponent.CondenseArchetypes();
                                lightComponent.ObjectName = new NameReference("DirectionalLightComponent", dlcIndex++);
                                if (parent.ClassName == "StaticLightCollectionActor")
                                {
                                    if (!componentToMatrixMap.TryGetValue(lightComponent.UIndex, out Matrix4x4 m))
                                    {
                                        continue;
                                    }
                                    (Vector3 posVec, Vector3 scaleVec, Rotator rotator) = m.UnrealDecompose();
                                    locationProp = CommonStructs.Vector3Prop(posVec, "Location");
                                    rotationProp = CommonStructs.RotatorProp(rotator, "Rotation");
                                    scaleProp = CommonStructs.Vector3Prop(scaleVec, "DrawScale3D");
                                }
                                else
                                {
                                    locationProp = parent.GetProperty<StructProperty>("Location");
                                    rotationProp = parent.GetProperty<StructProperty>("Rotation");
                                    scaleProp = parent.GetProperty<StructProperty>("DrawScale3D");
                                    if (parent.GetProperty<FloatProperty>("DrawScale")?.Value is float scale)
                                    {
                                        Vector3 scaleVec = Vector3.One;
                                        if (scaleProp != null)
                                        {
                                            scaleVec = CommonStructs.GetVector3(scaleProp);
                                        }
                                        scaleProp = CommonStructs.Vector3Prop(scaleVec * scale, "DrawScale3D");
                                    }
                                }

                                var dla = new ExportEntry(tempPackage, levelExport, new NameReference("DirectionalLight", dlaIndex++), EntryImporter.CreateStack(MEGame.UDK, directionalLightClass.UIndex))
                                {
                                    Class = directionalLightClass
                                };
                                dla.ObjectFlags |= UnrealFlags.EObjectFlags.HasStack;
                                dla.WriteProperty(new NameProperty(new NameReference(originalIFP, lightComponent.UIndex), "Tag"));
                                tempPackage.AddExport(dla);

                                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, lightComponent, tempPackage, dla, true, new RelinkerOptionsPackage(packageCache),
                                                                     out IEntry portedDLC);
                                var dlaProps = new PropertyCollection
                                {
                                    new ObjectProperty(portedDLC.UIndex, "LightComponent"),
                                    new NameProperty("DirectionalLight", "Tag"),
                                };
                                if (locationProp != null)
                                {
                                    dlaProps.Add(locationProp);
                                }
                                if (rotationProp != null)
                                {
                                    dlaProps.Add(rotationProp);
                                }
                                if (scaleProp != null)
                                {
                                    dlaProps.Add(scaleProp);
                                }
                                dla.WriteProperties(dlaProps);
                                var dlce = portedDLC as ExportEntry;
                                dlce.ObjectFlags |= UnrealFlags.EObjectFlags.Transactional;
                                dlce.Archetype = tempPackage.GetEntryOrAddImport("Engine.Default__DirectionalLight.DirectionalLightComponent0", "DirectionalLightComponent", "Engine");

                                lightActors.Add(dla);
                                break;
                        }
                    }
                }
                UDKifyLights(tempPackage);
                #endregion

                var level = ObjectBinary.From<Level>(levelExport);
                level.Actors = levelExport.GetChildren().Where(ent => ent.IsA("Actor")).Select(ent => ent.UIndex).ToList();
                levelExport.WriteBinary(level);
            }

            string resultFilePath = Path.Combine(UDKDirectory.MapsPath, $"{Path.GetFileNameWithoutExtension(pcc.FilePath)}.udk");
            MEPackageHandler.CreateEmptyLevel(resultFilePath, MEGame.UDK);
            using (IMEPackage udkPackage2 = MEPackageHandler.OpenUDKPackage(resultFilePath))
            {
                ExportEntry levelExport = udkPackage2.Exports.First(exp => exp.ClassName == "Level");
                var levelBin = ObjectBinary.From<Level>(levelExport);
                foreach (ExportEntry actor in staticMeshActors)
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild, actor, udkPackage2, levelExport, true, new RelinkerOptionsPackage(packageCache), out IEntry result);
                    levelBin.Actors.Add(result.UIndex);
                }
                foreach (ExportEntry actor in lightActors)
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild, actor, udkPackage2, levelExport, true, new RelinkerOptionsPackage(packageCache), out IEntry result);
                    levelBin.Actors.Add(result.UIndex);
                }
                foreach (var actor in terrains)
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, actor, udkPackage2, levelExport, true, new RelinkerOptionsPackage(packageCache), out IEntry result);
                    levelBin.Actors.Add(result.UIndex);
                }


                // Port the Model and ModelComponent as some maps use these
                // Line up the indexing for these so it works properly
                var meLevelBin = pcc.GetLevelBinary();
                var udkModel = udkPackage2.GetUExport(levelBin.Model);
                var meModel = pcc.GetUExport(meLevelBin.Model);
                udkModel.indexValue = meModel.indexValue; // Same name

                var udkModelBin = ObjectBinary.From<Model>(udkModel);
                var meModelBin = ObjectBinary.From<Model>(meModel);

                var mePolys = pcc.GetUExport(meModelBin.Polys);
                var udkPolys = udkPackage2.GetUExport(udkModelBin.Polys);
                udkPolys.indexValue = mePolys.indexValue;

                // Overwrite it
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingularWithRelink, meModel, udkPackage2, udkModel, true,
                    new RelinkerOptionsPackage() { PortExportsAsImportsWhenPossible = true }, out _);

                List<int> newModelComps = new List<int>();
                foreach (var mc in meLevelBin.ModelComponents)
                {
                    var sourceExp = pcc.GetUExport(mc);
                    var udkModelComp = udkPackage2.FindExport(sourceExp.InstancedFullPath);
                    if (udkModelComp == null)
                    {
                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceExp, udkPackage2, udkModel, true,
                            new RelinkerOptionsPackage() { PortExportsAsImportsWhenPossible = true }, out var portedmc);
                        udkModelComp = portedmc as ExportEntry;
                    }
                    newModelComps.Add(udkModelComp.UIndex);
                }

                levelBin.ModelComponents = newModelComps.ToArray();

                //LevelTools.RebuildPersistentLevelChildren(levelExport);
                levelExport.WriteBinary(levelBin);

                // Move stuff out from under imports in UDK
                ExportEntry extrasBase = null;
                foreach (var exp in udkPackage2.Exports.Where(x => x.idxLink != 0 && x.GetRoot() is ImportEntry imp && x.Parent == imp).ToList())
                {
                    extrasBase ??= ExportCreator.CreatePackageExport(udkPackage2, "ExtraStuff", null, forcedExport: false);
                    exp.idxLink = extrasBase.UIndex;
                }


                udkPackage2.Save(resultFilePath);
                var rcp = new ReferenceCheckPackage();
                EntryChecker.CheckReferences(rcp, udkPackage2, LECLocalizationShim.NonLocalizedStringConverter);
                foreach (var v in rcp.GetSignificantIssues())
                {
                    Debug.WriteLine($"{v.Entry?.InstancedFullPath} {v.Message}");
                }
            }
            return resultFilePath;
        }

        /// <summary>
        /// Creates UDK source-side imports.
        /// </summary>
        /// <param name="foundRef"></param>
        /// <param name="meshPackage"></param>
        private static IEntry CreateImportsFor(ExportEntry obj, IMEPackage destPackage)
        {
            Stack<IEntry> parentStack = new Stack<IEntry>();
            parentStack.Add(obj);
            IEntry entry = obj;
            while (entry.Parent != null)
            {
                parentStack.Push(entry.Parent);
                entry = entry.Parent;
            }

            // Create root package import
            var rootPackage = destPackage.FindEntry(obj.FileRef.FileNameNoExtension, "Package");
            if (rootPackage == null)
            {
                rootPackage = new ImportEntry(destPackage, null, obj.FileRef.FileNameNoExtension)
                {
                    ClassName = "Package",
                    PackageFile = "Core",
                };
                destPackage.AddImport(rootPackage as ImportEntry);
            }

            // Create the children of the root import
            IEntry parent = rootPackage;
            while (parentStack.Count > 0)
            {
                var item = parentStack.Pop();
                var ifp = parent.InstancedFullPath + '.' + item.ObjectName;
                var tempParent = destPackage.FindEntry(ifp, item.ClassName);
                if (tempParent != null)
                {
                    parent = tempParent;
                    continue; // Already exists in package
                }

                parent = new ImportEntry(destPackage, parent.UIndex, item.ObjectName)
                {
                    PackageFile = ImportEntry.GetPackageFile(MEGame.UDK, item.ClassName),
                    ClassName = item.ClassName,
                };
                destPackage.AddImport(parent as ImportEntry);
            }

            return parent;
        }

        private static void ConvertPackageImportToExport(ImportEntry imp, bool isRoot)
        {
            var impName = imp.ObjectName;
            imp.ObjectName = "portingTemp";
            var newExport = ExportCreator.CreatePackageExport(imp.FileRef, impName, imp.Parent, forcedExport: true);
            if (isRoot)
            {
                newExport.PackageFlags |= UnrealFlags.EPackageFlags.Cooked;
            }

            var relinkMap = new ListenableDictionary<IEntry, IEntry>();

            foreach (var imp2 in imp.FileRef.Imports)
            {
                if (imp2 == imp)
                {
                    relinkMap[imp2] = newExport;
                }
                else
                {
                    relinkMap[imp2] = imp2; // Nothing is changing.
                }

                if (imp2.idxLink == imp.UIndex)
                    imp2.idxLink = newExport.UIndex;
            }
            foreach (var exp in imp.FileRef.Exports)
            {
                // Move to new parent.
                if (exp.idxLink == imp.UIndex)
                    exp.idxLink = newExport.UIndex;
                relinkMap[exp] = exp; // Nothing is changing.
            }
            var rop = new RelinkerOptionsPackage() { CrossPackageMap = relinkMap };
            Relinker.RelinkAll(rop);



            // Technically we should copy the original export's data... like info on Package export
            EntryPruner.TrashEntries(imp.FileRef, [imp]); // Get rid of the original
        }

        private static void UDKifyLights(IMEPackage pcc)
        {
            var pointLightComponents = new List<ExportEntry>();
            var spotLightComponents = new List<ExportEntry>();
            //var directionalLightComponents = new List<ExportEntry>();

            foreach (ExportEntry export in pcc.Exports)
            {
                switch (export.ClassName)
                {
                    case "PointLightComponent":
                        pointLightComponents.Add(export);
                        break;
                    case "SpotLightComponent":
                        spotLightComponents.Add(export);
                        break;
                        //case "DirectionalLightComponent":
                        //    directionalLightComponents.Add(export);
                        //    break;
                }
            }

            UDKifyPointLights(pcc, pointLightComponents);
            UDKifySpotLights(pcc, spotLightComponents);
        }

        private static void UDKifySpotLights(IMEPackage pcc, IEnumerable<ExportEntry> spotLightComponents)
        {
            // This forces these to be created. Due to issues in parent creation of this method
            // they have the wrong package file.
            pcc.GetEntryOrAddImport("Engine.Default__PointLight", null, packageFile: "Engine");
            pcc.GetEntryOrAddImport("Engine.Default__SpotLight", null, packageFile: "Engine");
            pcc.GetEntryOrAddImport("Engine.Default__DirectionalLight", null, packageFile: "Engine");

            var drawLightRadiusComponentClass = pcc.GetEntryOrAddImport("Engine.DrawLightRadiusComponent", "Class");
            var drawLightConeComponentClass = pcc.GetEntryOrAddImport("Engine.DrawLightConeComponent", "Class");
            var drawLightRadiusArchetype = pcc.GetEntryOrAddImport("Engine.Default__SpotLight.DrawLightRadius0", packageFile: "Engine", className: "DrawLightRadiusComponent");
            var drawLightSourceRadiusArchetype = pcc.GetEntryOrAddImport("Engine.Default__SpotLight.DrawLightSourceRadius0", packageFile: "Engine", className: "DrawLightRadiusComponent");
            var drawInnerConeArchetype = pcc.GetEntryOrAddImport("Engine.Default__SpotLight.DrawInnerCone0", packageFile: "Engine", className: "DrawLightConeComponent");
            var drawOuterConeArchetype = pcc.GetEntryOrAddImport("Engine.Default__SpotLight.DrawOuterCone0", packageFile: "Engine", className: "DrawLightConeComponent");
            int dlcIndex = 1;
            int dlrIndex = 1;
            byte[] prePropBinary = new byte[8];
            foreach (ExportEntry slc in spotLightComponents)
            {
                var lightingChannels = slc.GetProperty<StructProperty>("LightingChannels");
                var innerConeAngle = slc.GetProperty<FloatProperty>("InnerConeAngle")?.Value ?? 0f;
                var outerConeAngle = slc.GetProperty<FloatProperty>("OuterConeAngle")?.Value ?? 44f;
                float radius = slc.GetProperty<FloatProperty>("Radius")?.Value ?? 1024f;
                var drawInnerCone = new ExportEntry(pcc, slc.Parent, new NameReference("DrawLightConeComponent", dlcIndex++), prePropBinary, new PropertyCollection
                {
                    new FloatProperty(radius, "ConeRadius"),
                    new FloatProperty(innerConeAngle, "ConeAngle")
                })
                {
                    Class = drawLightConeComponentClass,
                    Archetype = drawInnerConeArchetype
                };
                var drawOuterCone = new ExportEntry(pcc, slc.Parent, new NameReference("DrawLightConeComponent", dlcIndex++), prePropBinary, new PropertyCollection
                {
                    new FloatProperty(radius, "ConeRadius"),
                    new FloatProperty(outerConeAngle, "ConeAngle")
                })
                {
                    Class = drawLightConeComponentClass,
                    Archetype = drawOuterConeArchetype
                };
                var drawLightRadius = new ExportEntry(pcc, slc.Parent, new NameReference("DrawLightRadiusComponent", dlrIndex++), prePropBinary, new PropertyCollection
                {
                    new FloatProperty(radius, "SphereRadius")
                })
                {
                    Class = drawLightRadiusComponentClass,
                    Archetype = drawLightRadiusArchetype
                };
                var drawLightSourceRadius = new ExportEntry(pcc, slc.Parent, new NameReference("DrawLightRadiusComponent", dlrIndex++), prePropBinary, new PropertyCollection
                {
                    new FloatProperty(32f, "SphereRadius")
                })
                {
                    Class = drawLightRadiusComponentClass,
                    Archetype = drawLightSourceRadiusArchetype
                };
                if (lightingChannels != null)
                {
                    drawInnerCone.WriteProperty(lightingChannels);
                    drawOuterCone.WriteProperty(lightingChannels);
                    drawLightRadius.WriteProperty(lightingChannels);
                    drawLightSourceRadius.WriteProperty(lightingChannels);
                }

                pcc.AddExport(drawInnerCone);
                pcc.AddExport(drawOuterCone);
                pcc.AddExport(drawLightRadius);
                pcc.AddExport(drawLightSourceRadius);

                slc.WriteProperty(new ObjectProperty(drawInnerCone.UIndex, "PreviewInnerCone"));
                slc.WriteProperty(new ObjectProperty(drawOuterCone.UIndex, "PreviewOuterCone"));
                slc.WriteProperty(new ObjectProperty(drawLightRadius.UIndex, "PreviewLightRadius"));
                slc.WriteProperty(new ObjectProperty(drawLightSourceRadius.UIndex, "PreviewLightSourceRadius"));
            }
        }

        private static void UDKifyPointLights(IMEPackage pcc, IEnumerable<ExportEntry> pointLightComponents)
        {
            pcc.GetEntryOrAddImport("Engine.Default__PointLight", "PointLight", packageFile: "Engine");
            pcc.GetEntryOrAddImport("Engine.Default__SpotLight", "SpotLight", packageFile: "Engine");
            pcc.GetEntryOrAddImport("Engine.Default__DirectionalLight", "DirectionalLight", packageFile: "Engine");

            var drawLightRadiusComponentClass = pcc.GetEntryOrAddImport("Engine.DrawLightRadiusComponent", "Class");
            var drawLightRadiusArchetype = pcc.GetEntryOrAddImport("Engine.Default__PointLight.DrawLightRadius0", packageFile: "Engine", className: "DrawLightRadiusComponent");
            var drawLightSourceRadiusArchetype = pcc.GetEntryOrAddImport("Engine.Default__PointLight.DrawLightSourceRadius0", packageFile: "Engine", className: "DrawLightRadiusComponent");
            int dlrIndex = 1;
            byte[] prePropBinary = new byte[8];
            foreach (ExportEntry plc in pointLightComponents)
            {
                var lightingChannels = plc.GetProperty<StructProperty>("LightingChannels");
                float radius = plc.GetProperty<FloatProperty>("Radius")?.Value ?? 1024f;
                var drawLightRadius = new ExportEntry(pcc, plc.Parent, new NameReference("DrawLightRadiusComponent", dlrIndex++), prePropBinary, new PropertyCollection
                {
                    new FloatProperty(radius, "SphereRadius")
                })
                {
                    Class = drawLightRadiusComponentClass,
                    Archetype = drawLightRadiusArchetype
                };
                var drawLightSourceRadius = new ExportEntry(pcc, plc.Parent, new NameReference("DrawLightRadiusComponent", dlrIndex++), prePropBinary, new PropertyCollection
                {
                    new FloatProperty(32f, "SphereRadius")
                })
                {
                    Class = drawLightRadiusComponentClass,
                    Archetype = drawLightSourceRadiusArchetype
                };
                if (lightingChannels != null)
                {
                    drawLightRadius.WriteProperty(lightingChannels);
                    drawLightSourceRadius.WriteProperty(lightingChannels);
                }

                pcc.AddExport(drawLightRadius);
                pcc.AddExport(drawLightSourceRadius);

                plc.WriteProperty(new ObjectProperty(drawLightRadius.UIndex, "PreviewLightRadius"));
                plc.WriteProperty(new ObjectProperty(drawLightSourceRadius.UIndex, "PreviewLightSourceRadius"));
            }
        }
    }
}