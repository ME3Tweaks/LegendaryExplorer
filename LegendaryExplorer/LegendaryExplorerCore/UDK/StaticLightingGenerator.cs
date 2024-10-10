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
    class UDKAssetInfo
    {
        /// <summary>
        /// Name of the asset package being generated
        /// </summary>
        public string MeshAssetPackageName { get; set; }

        // SOURCE PACKAGE ==============
        /// <summary>
        /// Source package's level export
        /// </summary>
        public ExportEntry LevelExport { get; set; }

        /// <summary>
        /// List of UIndexes for actors in the level (source package)
        /// </summary>
        public List<int> ActorsInLevel { get; set; }

        /// <summary>
        /// Maps components to their matrix deformations (if they are in a collection) - source package.
        /// </summary>
        public Dictionary<int, Matrix4x4> ComponentToMatrixMap { get; } = new();

        /// <summary>
        /// Package we are converting assets of
        /// </summary>
        public IMEPackage SourcePackage { get; set; }

        // END SOURCE PACKAGE ==============================

        /// <summary>
        /// Intermediate package that we stuff things into before rebuilding package to remove stuff we don't want.
        /// </summary>
        public IMEPackage TempPackage { get; set; }

        /// <summary>
        /// List of static meshes that have been ported into the assets package file
        /// </summary>
        public List<ExportEntry> StaticMeshes { get; } = new();

        /// <summary>
        /// List of newly created static mesh actors (UDK)
        /// </summary>
        public List<ExportEntry> StaticMeshActors { get; } = new();

        /// <summary>
        /// List of newly created light actors (UDK)
        /// </summary>
        public List<ExportEntry> LightActors { get; } = new();

        /// <summary>
        /// Root package exports containing meshes that are exports
        /// </summary>
        public List<NameReference> TopLevelMeshPackages { get; } = new();
    }

    public static class ConvertToUDK
    {
        public static string GenerateUDKFileForLevel(IMEPackage pcc, string mapOutputPath = null,
            string assetsOutputPath = null, string decookedMaterialsFolder = null)
        {
            var assetInfo = GenerateAssetsPackage(pcc, assetsOutputPath, decookedMaterialsFolder);

            MELoadedFiles.InvalidateCaches();

            var packageCache = new PackageCache();

            var terrains = new List<ExportEntry>();
            assetInfo.TempPackage = MEPackageHandler.OpenMEPackageFromStream(MEPackageHandler.CreateEmptyLevelStream(Path.GetFileNameWithoutExtension(pcc.FilePath), MEGame.UDK), pcc.FileNameNoExtension + ".udk");
            {
                assetInfo.LevelExport = assetInfo.TempPackage.Exports.First(exp => exp.ClassName == "Level");
                assetInfo.ActorsInLevel = ObjectBinary.From<Level>(pcc.Exports.First(exp => exp.ClassName == "Level"))
                    .Actors.ToList();
                foreach (int uIndex in assetInfo.ActorsInLevel)
                {
                    if (pcc.GetEntry(uIndex) is ExportEntry stcExp)
                    {
                        if (stcExp.ClassName == "StaticMeshCollectionActor")
                        {
                            StaticMeshCollectionActor stmc = ObjectBinary.From<StaticMeshCollectionActor>(stcExp);
                            var components = stcExp.GetProperty<ArrayProperty<ObjectProperty>>("StaticMeshComponents");
                            for (int i = 0; i < components.Count; i++)
                            {
                                assetInfo.ComponentToMatrixMap[components[i].Value] = stmc.LocalToWorldTransforms[i];
                            }
                        }
                        else if (stcExp.ClassName == "StaticLightCollectionActor")
                        {
                            StaticLightCollectionActor stlc = ObjectBinary.From<StaticLightCollectionActor>(stcExp);
                            var components = stcExp.GetProperty<ArrayProperty<ObjectProperty>>("LightComponents");
                            for (int i = 0; i < components.Count; i++)
                            {
                                assetInfo.ComponentToMatrixMap[components[i].Value] = stlc.LocalToWorldTransforms[i];
                            }
                        }
                    }
                }

                PortStaticMeshActors(assetInfo, packageCache);


                #region Terrain

                // Not sure if we need to port this into temp
                foreach (var t in pcc.Exports.Where(x => x.IsA("Terrain")))
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, t,
                        assetInfo.TempPackage, assetInfo.LevelExport, true,
                        new RelinkerOptionsPackage(packageCache) { PortExportsAsImportsWhenPossible = true },
                        out IEntry result);
                    terrains.Add(result as ExportEntry);
                }

                #endregion

                PortLightActors(assetInfo, packageCache);
                UDKifyLights(assetInfo);


                var level = ObjectBinary.From<Level>(assetInfo.LevelExport);
                level.Actors = assetInfo.LevelExport.GetChildren().Where(ent => ent.IsA("Actor")).Select(ent => ent.UIndex)
                    .ToList();
                assetInfo.LevelExport.WriteBinary(level);
            }


            // Package reform - fixes up imports and dumps trash out of package
            string resultFilePath = Path.Combine(mapOutputPath ?? UDKDirectory.MapsPath, $"{Path.GetFileNameWithoutExtension(pcc.FilePath)}.udk");
            MEPackageHandler.CreateEmptyLevel(resultFilePath, MEGame.UDK);
            using (IMEPackage udkPackage2 = MEPackageHandler.OpenUDKPackage(resultFilePath))
            {
                var finalLevelExport = udkPackage2.Exports.First(exp => exp.ClassName == "Level");
                var levelBin = ObjectBinary.From<Level>(finalLevelExport);

                udkPackage2.Save();
                foreach (ExportEntry actor in assetInfo.StaticMeshActors)
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild, actor,
                        udkPackage2, assetInfo.LevelExport, true, new RelinkerOptionsPackage(packageCache) { PortExportsAsImportsWhenPossible = true }, out IEntry result);
                    levelBin.Actors.Add(result.UIndex);
                }

                foreach (ExportEntry actor in assetInfo.LightActors)
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild, actor,
                        udkPackage2, assetInfo.LevelExport, true, new RelinkerOptionsPackage(packageCache) { PortExportsAsImportsWhenPossible = true }, out IEntry result);
                    levelBin.Actors.Add(result.UIndex);
                }

                foreach (var actor in terrains)
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, actor,
                        udkPackage2, assetInfo.LevelExport, true, new RelinkerOptionsPackage(packageCache) { PortExportsAsImportsWhenPossible = true }, out IEntry result);
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
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingularWithRelink, meModel,
                    udkPackage2, udkModel, true,
                    new RelinkerOptionsPackage() { PortExportsAsImportsWhenPossible = true }, out _);

                List<int> newModelComps = new List<int>();
                foreach (var mc in meLevelBin.ModelComponents)
                {
                    var sourceExp = pcc.GetUExport(mc);
                    var udkModelComp = udkPackage2.FindExport(sourceExp.InstancedFullPath);
                    if (udkModelComp == null)
                    {
                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies,
                            sourceExp, udkPackage2, udkModel, true,
                            new RelinkerOptionsPackage() { PortExportsAsImportsWhenPossible = true }, out var portedmc);
                        udkModelComp = portedmc as ExportEntry;
                    }

                    newModelComps.Add(udkModelComp.UIndex);
                }

                levelBin.ModelComponents = newModelComps.ToArray();

                //LevelTools.RebuildPersistentLevelChildren(levelExport);
                finalLevelExport.WriteBinary(levelBin);

                // Move stuff out from under imports in UDK
                ExportEntry extrasBase = null;
                foreach (var exp in udkPackage2.Exports.Where(x => x.idxLink != 0 && x.GetRoot() is ImportEntry imp && x.Parent == imp).ToList())
                {
                    extrasBase ??= ExportCreator.CreatePackageExport(udkPackage2, "ExtraStuff", null, forcedExport: false);
                    exp.idxLink = extrasBase.UIndex;
                }


                udkPackage2.Save(resultFilePath);
                TestReferences(udkPackage2);
            }

            assetInfo.TempPackage.Dispose();

            return resultFilePath;
        }

        private static void PortLightActors(UDKAssetInfo assetInfo, PackageCache cache)
        {
            {
                IEntry pointLightClass = assetInfo.TempPackage.GetEntryOrAddImport("Engine.PointLight", "Class");
                IEntry spotLightClass = assetInfo.TempPackage.GetEntryOrAddImport("Engine.SpotLight", "Class");
                IEntry directionalLightClass =
                    assetInfo.TempPackage.GetEntryOrAddImport("Engine.DirectionalLight", "Class");

                int plaIndex = 1;
                int plcIndex = 1;
                int slaIndex = 1;
                int slcIndex = 1;
                int dlaIndex = 1;
                int dlcIndex = 1;
                foreach (ExportEntry lightComponent in assetInfo.SourcePackage.Exports)
                {
                    if (!(lightComponent.Parent is ExportEntry parent && assetInfo.ActorsInLevel.Contains(parent.UIndex)))
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
                                if (!assetInfo.ComponentToMatrixMap.TryGetValue(lightComponent.UIndex, out Matrix4x4 m))
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

                            var pla = new ExportEntry(assetInfo.TempPackage, assetInfo.LevelExport,
                                new NameReference("PointLight", plaIndex++),
                                EntryImporter.CreateStack(MEGame.UDK, pointLightClass.UIndex))
                            {
                                Class = pointLightClass,
                            };
                            pla.ObjectFlags |= UnrealFlags.EObjectFlags.HasStack;
                            pla.WriteProperty(
                                new NameProperty(new NameReference(originalIFP, lightComponent.UIndex), "Tag"));

                            assetInfo.TempPackage.AddExport(pla);

                            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild,
                                lightComponent, assetInfo.TempPackage, pla, true,
                                new RelinkerOptionsPackage(cache),
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
                            plce.Archetype = assetInfo.TempPackage.GetEntryOrAddImport(
                                "Engine.Default__PointLight.PointLightComponent0", "PointLightComponent", "Engine");
                            assetInfo.LightActors.Add(pla);
                            break;
                        case "SpotLightComponent":
                            lightComponent.CondenseArchetypes();
                            lightComponent.ObjectName = new NameReference("SpotLightComponent", slcIndex++);
                            if (parent.ClassName == "StaticLightCollectionActor")
                            {
                                if (!assetInfo.ComponentToMatrixMap.TryGetValue(lightComponent.UIndex, out Matrix4x4 m))
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

                            var sla = new ExportEntry(assetInfo.TempPackage, assetInfo.LevelExport,
                                new NameReference("SpotLight", slaIndex++),
                                EntryImporter.CreateStack(MEGame.UDK, spotLightClass.UIndex))
                            {
                                Class = spotLightClass
                            };
                            sla.ObjectFlags |= UnrealFlags.EObjectFlags.HasStack;
                            sla.WriteProperty(
                                new NameProperty(new NameReference(originalIFP, lightComponent.UIndex), "Tag"));
                            assetInfo.TempPackage.AddExport(sla);

                            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild,
                                lightComponent, assetInfo.TempPackage, sla, true,
                                new RelinkerOptionsPackage(cache),
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
                            slce.Archetype = assetInfo.TempPackage.GetEntryOrAddImport(
                                "Engine.Default__SpotLight.SpotLightComponent0", "SpotLightComponent", "Engine");

                            assetInfo.LightActors.Add(sla);
                            break;
                        case "DirectionalLightComponent":
                            lightComponent.CondenseArchetypes();
                            lightComponent.ObjectName = new NameReference("DirectionalLightComponent", dlcIndex++);
                            if (parent.ClassName == "StaticLightCollectionActor")
                            {
                                if (!assetInfo.ComponentToMatrixMap.TryGetValue(lightComponent.UIndex, out Matrix4x4 m))
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

                            var dla = new ExportEntry(assetInfo.TempPackage, assetInfo.LevelExport,
                                new NameReference("DirectionalLight", dlaIndex++),
                                EntryImporter.CreateStack(MEGame.UDK, directionalLightClass.UIndex))
                            {
                                Class = directionalLightClass
                            };
                            dla.ObjectFlags |= UnrealFlags.EObjectFlags.HasStack;
                            dla.WriteProperty(
                                new NameProperty(new NameReference(originalIFP, lightComponent.UIndex), "Tag"));
                            assetInfo.TempPackage.AddExport(dla);

                            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild,
                                lightComponent, assetInfo.TempPackage, dla, true,
                                new RelinkerOptionsPackage(cache),
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
                            dlce.Archetype = assetInfo.TempPackage.GetEntryOrAddImport(
                                "Engine.Default__DirectionalLight.DirectionalLightComponent0",
                                "DirectionalLightComponent", "Engine");

                            assetInfo.LightActors.Add(dla);
                            break;
                    }
                }
            }
        }

        private static void PortStaticMeshActors(UDKAssetInfo assetInfo, PackageCache cache)
        {
            var emptySMCBin = new StaticMeshComponent();
            IEntry staticMeshActorClass = assetInfo.TempPackage.GetEntryOrAddImport("Engine.StaticMeshActor", "Class");
            assetInfo.TempPackage.GetEntryOrAddImport("Engine.Default__StaticMeshActor", "StaticMeshActor", "Engine");
            IEntry staticMeshComponentArchetype = assetInfo.TempPackage.GetEntryOrAddImport("Engine.Default__StaticMeshActor.StaticMeshComponent0",
                                                                                 "StaticMeshComponent", "Engine");
            int smaIndex = 2;
            int smcIndex = 2;
            foreach (ExportEntry smc in assetInfo.SourcePackage.Exports.Where(exp => exp.ClassName == "StaticMeshComponent"))
            {
                if (smc.Parent is ExportEntry parent && assetInfo.ActorsInLevel.Contains(parent.UIndex) && parent.IsA("StaticMeshActorBase"))
                {
                    var originalIFP = smc.InstancedFullPath;

                    // List of things to not port
                    if (parent.IsA("BioLedgeMeshActor"))
                        continue; // Don't port these, they are not really useful in UDK for lighting

                    StructProperty locationProp;
                    StructProperty rotationProp;
                    StructProperty scaleProp = null;
                    smc.CondenseArchetypes();
                    if (smc.GetProperty<ObjectProperty>("StaticMesh") is not { } meshProp || !assetInfo.SourcePackage.IsUExport(meshProp.Value))
                    {
                        continue;
                    }

                    smc.WriteBinary(emptySMCBin);
                    smc.RemoveProperty("bBioIsReceivingDecals");
                    smc.RemoveProperty("bBioForcePrecomputedShadows");
                    //smc.RemoveProperty("bUsePreComputedShadows");
                    smc.RemoveProperty("bAcceptsLights");
                    smc.RemoveProperty("IrrelevantLights");
                    smc.RemoveProperty("PhysMaterialOverride");
                    smc.RemoveProperty("Materials"); //should make use of this?
                    smc.ObjectName = new NameReference("StaticMeshComponent", smcIndex++);
                    if (parent.ClassName == "StaticMeshCollectionActor")
                    {
                        if (!assetInfo.ComponentToMatrixMap.TryGetValue(smc.UIndex, out Matrix4x4 m))
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

                    var sma = new ExportEntry(assetInfo.TempPackage, assetInfo.LevelExport, new NameReference("StaticMeshActor", smaIndex++), EntryImporter.CreateStack(MEGame.UDK, staticMeshActorClass.UIndex))
                    {
                        Class = staticMeshActorClass,
                    };
                    sma.ObjectFlags |= UnrealFlags.EObjectFlags.HasStack;
                    assetInfo.TempPackage.AddExport(sma);
                    var debugprops = smc.GetProperties();
                    var sm = debugprops.GetProp<ObjectProperty>("StaticMesh")?.ResolveToEntry(assetInfo.SourcePackage);
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, smc, assetInfo.TempPackage,
                                                         sma, true, new RelinkerOptionsPackage(cache) { PortExportsAsImportsWhenPossible = true }, out IEntry result);
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
                    assetInfo.StaticMeshActors.Add(sma);
                }
            }

            // Generates the import. Do not remove
            IEntry topMeshPackageImport = assetInfo.TempPackage.GetEntryOrAddImport(assetInfo.MeshAssetPackageName, "Package");
            IEntry udkifyImport = assetInfo.TempPackage.GetEntryOrAddImport($"{assetInfo.MeshAssetPackageName}.UDKifyMeshes", "Package");

            // Reform will convert these to imports
            foreach (var sm in assetInfo.TempPackage.Exports.Where(x => x.ClassName == "StaticMesh"))
            {
                sm.Parent = udkifyImport;
            }
        }

        private static UDKAssetInfo GenerateAssetsPackage(IMEPackage pcc, string assetsOutputPath, string decookedMaterialsFolder)
        {
            var assetInfo = new UDKAssetInfo();

            assetInfo.MeshAssetPackageName = $"{Path.GetFileNameWithoutExtension(pcc.FilePath)}Meshes";
            assetInfo.SourcePackage = pcc;


            var originalName = pcc.FilePath;
            pcc.SetInternalFilepath($"ForceExportsResolver_{pcc.FileNameNoExtension}.pcc");

            string meshFile = Path.Combine(assetsOutputPath ?? UDKDirectory.SharedPath, $"{assetInfo.MeshAssetPackageName}.upk");
            MEPackageHandler.CreateAndSavePackage(meshFile, MEGame.UDK);
            using IMEPackage meshPackage = MEPackageHandler.OpenUDKPackage(meshFile);
            meshPackage.GetEntryOrAddImport("Core.Package", "Class");

            IEntry defMat = meshPackage.GetEntryOrAddImport("EngineMaterials.DefaultMaterial", "Material", "Engine");
            var allMats = new HashSet<int>();
            var relinkerOptionsPackage = new RelinkerOptionsPackage() { Cache = new PackageCache() };
            ListenableDictionary<IEntry, IEntry> relinkMap = relinkerOptionsPackage.CrossPackageMap;
            PackageCache packageCache = relinkerOptionsPackage.Cache;
            #region StaticMeshes

            List<ExportEntry> staticMeshes = pcc.Exports.Where(exp => exp.ClassName.CaseInsensitiveEquals("StaticMesh")).ToList();
            foreach (ExportEntry mesh in staticMeshes)
            {
                if (mesh.ObjectName == "Rock_Bunch_01")
                {

                }
                if (pcc.Game.IsLEGame() && mesh.IsForcedExport)
                {
                    // Attempt to link up to ported content.
                    var meshPath = Path.Combine(decookedMaterialsFolder, mesh.GetRootName() + ".upk");
                    if (File.Exists(meshPath))
                    {
                        // Quickload; test if it exists
                        var upk = MEPackageHandler.UnsafePartialLoad(meshPath, x => false);
                        // This is ugly hack but it's fast.
                        var nonForcedIFP = mesh.InstancedFullPath.Substring(mesh.GetRootName().Length + 1);
                        var foundRef = upk.FindExport(nonForcedIFP, mesh.ClassName);
                        if (foundRef != null)
                        {
                            Debug.WriteLine($"Using decooked asset in UDK: {foundRef.MemoryFullPath}");
                            // Exists - generate import for this
                            var repointedMesh = CreateImportsFor(foundRef, meshPackage);
                            relinkMap[mesh] = repointedMesh;
                            continue;
                        }
                    }
                    Debug.WriteLine($">> Not using decooked asset in UDK for {mesh.MemoryFullPath}");

                }

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
                            new RelinkerOptionsPackage(packageCache) { PortExportsAsImportsWhenPossible = true });
                    }
                }

                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild, mesh, meshPackage, newParent, false, relinkerOptionsPackage, out IEntry ent);
                var portedMesh = (ExportEntry)ent;
                assetInfo.StaticMeshes.Add(portedMesh);
                if (!assetInfo.TopLevelMeshPackages.Contains(portedMesh.GetRootName()))
                {
                    assetInfo.TopLevelMeshPackages.Add(portedMesh.GetRootName());
                }

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


                // Fix imports under exports; these will crash UDK
                foreach (var imp in meshPackage.Imports.Where(x => x.Parent == portedMesh)) // EntryTree doesn't seem to work here for some reason..
                {
                    var sourceVersion = mesh.FileRef.FindEntry(imp.InstancedFullPath);
                    if (sourceVersion != null)
                    {

                    }
                }
            }
            // meshPackage.Save();
            #endregion

            #region Materials
            using (IMEPackage udkResources = MEPackageHandler.OpenMEPackageFromStream(LegendaryExplorerCoreUtilities.GetCustomAppResourceStream(MEGame.UDK)))
            {
                decookedMaterialsFolder ??= Path.Combine(UDKDirectory.ContentPath, "Shared", $"{pcc.Game}MaterialPort");
                ExportEntry normDiffMat = udkResources.Exports.First(exp => exp.ObjectName == "NormDiffMat");
                foreach (int matUIndex in allMats)
                {
                    if (pcc.GetEntry(matUIndex) is ExportEntry matExp)
                    {
                        if (pcc.Game.IsLEGame() && matExp.IsForcedExport)
                        {
                            // Attempt to link up to ported content.
                            var matPath = Path.Combine(decookedMaterialsFolder, matExp.GetRootName() + ".upk");
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

                        ExportEntry diff = null;
                        ExportEntry norm = null;
                        foreach (IEntry texEntry in MaterialInstanceConstant.GetTextures(matExp, packageCache))
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
                            // If texture is local it will be at the root of the package
                            if (meshPackage.FindEntry(texport.ObjectName.Instanced) is ExportEntry existingTexture)
                            {
                                return existingTexture;
                            }

                            // EntryExporter.ExportExportToPackage(texport, meshPackage, out var ent, packageCache, new RelinkerOptionsPackage(packageCache) { PortExportsAsImportsWhenPossible = true});
                            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, texport, meshPackage, null, false, new RelinkerOptionsPackage(packageCache), out IEntry ent);
                            // If imported texture is already in decooked game it should be an import... technically
                            if (ent is ExportEntry importedExport)
                            {
                                PropertyCollection properties = importedExport.GetProperties();
                                properties.RemoveNamedProperty("TextureFileCacheName");
                                properties.RemoveNamedProperty("TFCFileGuid");
                                properties.RemoveNamedProperty("LODGroup");
                                if (texport.GetProperty<EnumProperty>("Format") is { Value.Name: "PF_BC7" })
                                {
                                    var convertedImage = new Texture2D(texport).ToImage(PixelFormat.DXT1);
                                    properties.AddOrReplaceProp(new EnumProperty("PF_DXT1", "EPixelFormat", MEGame.UDK,
                                        "Format"));
                                    properties.AddOrReplaceProp(new EnumProperty("TC_NormalMap",
                                        "TextureCompressionSettings", MEGame.UDK, "CompressionSettings"));
                                    new Texture2D(importedExport) { TextureFormat = "PF_DXT1" }.Replace(convertedImage,
                                        properties, isPackageStored: true);
                                }
                                else
                                {
                                    importedExport.WriteProperties(properties);
                                }

                                //// Move to package root
                                //importedExport.idxLink = 0;
                            }

                            return (ExportEntry) ent;
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
                    if (parent.ClassName.CaseInsensitiveEquals("Package"))
                        break;

                    var nextParent = parent.Parent; // cache cause trashing will change parents
                    if (parent is ImportEntry imp)
                    {
                        var resolved = EntryImporter.ResolveImport(imp, null, unsafeLoad: true);
                        if (resolved == null)
                        {
                            // File is not safe to import from
                            ConvertPackageImportToExport(imp, nextParent == null);
                        }
                    }
                    parent = nextParent;
                }
            }

            // Fix up imports coming over under the linker
            ConvertToUDK.ConvertImportsToExportsUnderLinker(meshPackage, packageCache);


            // Move all static meshes to consistent package system
            List<ExportEntry> packagesToTrash = new List<ExportEntry>();
            foreach (var exp in meshPackage.Exports.Where(x => x.ClassName == "StaticMesh"/* && x.Parent != null && x.Parent.Parent == null*/).ToList())
            {
                var newParent = meshPackage.FindExport("UDKifyMeshes");
                if (newParent == null)
                {
                    newParent = ExportCreator.CreatePackageExport(meshPackage, "UDKifyMeshes", forcedExport: false);
                }

                if (exp.GetRoot() is ExportEntry ex && ex.ClassName == "Package")
                {
                    packagesToTrash.Add(ex);
                }
                exp.idxLink = newParent.UIndex;
            }

            EntryPruner.TrashEntries(meshPackage, packagesToTrash);

            relinkerOptionsPackage = null;
            (meshPackage as UDKPackage).FixupTrash();

            // Restore
            pcc.SetInternalFilepath(originalName);

            if (meshPackage.Exports.Any())
            {
                // Reform package without trash or extra packages
                // This isn't a resynth; this also filters out unreferenced content
                ReformPackage(meshPackage).Save();

                TestReferences(meshPackage);
                // meshPackage.Save();
            }
            else
            {
                // Do not save a file that has no exports.
                File.Delete(meshFile);
            }

            return assetInfo;
        }

        public static void PortNonLinkerReferencesAsImports(IMEPackage package, ExportEntry exp)
        {
            if (package.FindEntry(exp.InstancedFullPath) != null)
                return; // Object is already in the package

            var references = EntryImporter.GetAllReferencesOfExport(exp);
            var externalToLinkerReferences = references.Where(x => !x.GetLinker().CaseInsensitiveEquals(package.FileNameNoExtension)).ToList();

            // Generate all items external to the link as imports so we don't port them
            var topDownOrdering = externalToLinkerReferences.OrderBy(x => x.InstancedFullPath.Count(x => x == '.')).ToList();
            foreach (var eRef in topDownOrdering)
            {
                if (package.FindEntry(eRef.MemoryFullPath, eRef.ClassName) != null)
                    continue; // Object is already in the package

                var eRefEx = eRef as ExportEntry;
                IEntry parent = eRef.Parent != null ? package.FindImport(eRef.Parent.MemoryFullPath) : null;
                if (parent == null && (eRef.Parent != null || (!eRefEx?.ExportFlags.Has(UnrealFlags.EExportFlags.ForcedExport) ?? false)))
                {
                    // Doesn't exist yet. Port it in
                    parent = EntryExporter.PortParents(eRef, package, true);
                }
                ImportEntry imp = null;
                if (eRef is ExportEntry exRef)
                {
                    imp = new ImportEntry(exRef, parent?.UIndex ?? 0, package);
                    package.AddImport(imp);
                }
                else if (eRef is ImportEntry exImpRef)
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, eRef, package, parent, true, new RelinkerOptionsPackage(), out _);
                }
            }
        }

        /// <summary>
        /// Converts imports under the linker to exports; these will be imports nested under packages that are not ForcedExport (as UDK doesn't support this)
        /// </summary>
        /// <param name="package"></param>
        /// <param name="localCache"></param>
        public static void ConvertImportsToExportsUnderLinker(IMEPackage package, PackageCache localCache)
        {
            // If resources are stored in globally importable files we have to convert these imports to exports.
            var relinkMap = new ListenableDictionary<IEntry, IEntry>();

            // We cannot trash as we go because .RemoveTrailingTrash() will delete stuff, and that will cause invalid references
            // We cannot have invalid references until relink is completed so we track what we're going to trash and then trash it
            List<IEntry> trashItems = new List<IEntry>();

            var importsToConvert = package.Imports.Where(x => x.GetLinker() == package.FileNameNoExtension && !x.ObjectName.Name.StartsWith("PENDINGTRASH_", StringComparison.OrdinalIgnoreCase)).ToList();
            while (importsToConvert.Any())
            {
                foreach (var imp in importsToConvert)
                {
                    var exp = EntryImporter.ResolveImport(imp, localCache, instancedFullPathOverride: imp.InstancedFullPath.Replace("PENDINGTRASH_", ""));
                    imp.ObjectName = $"PENDINGTRASH_{imp.ObjectName}";
                    trashItems.Add(imp);
                    PortNonLinkerReferencesAsImports(package, exp); // Ensures we don't port exports outside of the linker
                    EntryExporter.ExportExportToPackage(exp, package, out var portedSingle, localCache,
                        new RelinkerOptionsPackage() { Cache = localCache });
                    var portedExp = (ExportEntry)portedSingle;
                    portedExp.ExportFlags &= ~UnrealFlags.EExportFlags.ForcedExport; // Remove forced export
                    relinkMap[imp] = portedExp;
                    foreach (var child in package.Tree.GetDirectChildrenOf(imp))
                    {
                        // Reparent children which can happen in multi-nested packages
                        var ifpTest = portedExp.InstancedFullPath + "." + child.ObjectName.Instanced;
                        if (package.FindExport(ifpTest, child.ClassName) == null)
                        {
                            // Move out from under object that is going to be trashed
                            child.Parent = portedExp;
                        }
                    }
                }

                // Loop this to convert dependencies that came in
                importsToConvert = package.Imports.Where(x => x.GetLinker() == package.FileNameNoExtension && !x.ObjectName.Name.StartsWith("PENDINGTRASH_", StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (relinkMap.Any())
            {
                Relinker.RelinkSamePackage(package, relinkMap);
            }

            EntryPruner.TrashEntries(package, trashItems);
        }

        [Conditional("DEBUG")]
        private static void TestReferences(IMEPackage meshPackage)
        {
            var rcp = new ReferenceCheckPackage();
            EntryChecker.CheckReferences(rcp, meshPackage, LECLocalizationShim.NonLocalizedStringConverter);
            foreach (var v in rcp.GetBlockingErrors().Concat(rcp.GetSignificantIssues()))
            {
                Debug.WriteLine($"{v.Entry?.InstancedFullPath} {v.Message}");
            }
        }

        private static IMEPackage ReformPackage(IMEPackage meshPackage)
        {
            var package = MEPackageHandler.CreateMemoryEmptyPackage(meshPackage.FilePath, MEGame.UDK);
            var cache = new PackageCache();
            foreach (var exp in meshPackage.Exports.Where(IsAsset))
            {
                EntryExporter.ExportExportToPackage(exp, package, out _, cache, new RelinkerOptionsPackage() { ImportExportDependencies = true, Cache = cache, PortExportsAsImportsWhenPossible = true});
            }

            return package;
        }

        private static bool IsAsset(ExportEntry exportEntry)
        {
            if (exportEntry.ClassName == "StaticMesh")
                return true;

            // Add more here

            return false;
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

                if (imp2.idxLink == imp.UIndex)
                    imp2.idxLink = newExport.UIndex;
            }
            foreach (var exp in imp.FileRef.Exports)
            {
                // Move to new parent.
                if (exp.idxLink == imp.UIndex)
                    exp.idxLink = newExport.UIndex;
            }
            Relinker.RelinkSamePackage(imp.FileRef, relinkMap);

            // Technically we should copy the original export's data... like info on Package export
            EntryPruner.TrashEntries(imp.FileRef, [imp]); // Get rid of the original
        }

        private static void UDKifyLights(UDKAssetInfo assetInfo)
        {
            var pointLightComponents = new List<ExportEntry>();
            var spotLightComponents = new List<ExportEntry>();
            //var directionalLightComponents = new List<ExportEntry>();

            foreach (ExportEntry export in assetInfo.TempPackage.Exports)
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

            UDKifyPointLights(assetInfo, pointLightComponents);
            UDKifySpotLights(assetInfo, spotLightComponents);
        }

        private static void UDKifySpotLights(UDKAssetInfo assetInfo, IEnumerable<ExportEntry> spotLightComponents)
        {
            // This forces these to be created. Due to issues in parent creation of this method
            // they have the wrong package file.
            assetInfo.TempPackage.GetEntryOrAddImport("Engine.Default__PointLight", null, packageFile: "Engine");
            assetInfo.TempPackage.GetEntryOrAddImport("Engine.Default__SpotLight", null, packageFile: "Engine");
            assetInfo.TempPackage.GetEntryOrAddImport("Engine.Default__DirectionalLight", null, packageFile: "Engine");

            var drawLightRadiusComponentClass = assetInfo.TempPackage.GetEntryOrAddImport("Engine.DrawLightRadiusComponent", "Class");
            var drawLightConeComponentClass = assetInfo.TempPackage.GetEntryOrAddImport("Engine.DrawLightConeComponent", "Class");
            var drawLightRadiusArchetype = assetInfo.TempPackage.GetEntryOrAddImport("Engine.Default__SpotLight.DrawLightRadius0", packageFile: "Engine", className: "DrawLightRadiusComponent");
            var drawLightSourceRadiusArchetype = assetInfo.TempPackage.GetEntryOrAddImport("Engine.Default__SpotLight.DrawLightSourceRadius0", packageFile: "Engine", className: "DrawLightRadiusComponent");
            var drawInnerConeArchetype = assetInfo.TempPackage.GetEntryOrAddImport("Engine.Default__SpotLight.DrawInnerCone0", packageFile: "Engine", className: "DrawLightConeComponent");
            var drawOuterConeArchetype = assetInfo.TempPackage.GetEntryOrAddImport("Engine.Default__SpotLight.DrawOuterCone0", packageFile: "Engine", className: "DrawLightConeComponent");
            int dlcIndex = 1;
            int dlrIndex = 1;
            byte[] prePropBinary = new byte[8];
            foreach (ExportEntry slc in spotLightComponents)
            {
                var lightingChannels = slc.GetProperty<StructProperty>("LightingChannels");
                var innerConeAngle = slc.GetProperty<FloatProperty>("InnerConeAngle")?.Value ?? 0f;
                var outerConeAngle = slc.GetProperty<FloatProperty>("OuterConeAngle")?.Value ?? 44f;
                float radius = slc.GetProperty<FloatProperty>("Radius")?.Value ?? 1024f;
                var drawInnerCone = new ExportEntry(assetInfo.TempPackage, slc.Parent, new NameReference("DrawLightConeComponent", dlcIndex++), prePropBinary, new PropertyCollection
                {
                    new FloatProperty(radius, "ConeRadius"),
                    new FloatProperty(innerConeAngle, "ConeAngle")
                })
                {
                    Class = drawLightConeComponentClass,
                    Archetype = drawInnerConeArchetype
                };
                var drawOuterCone = new ExportEntry(assetInfo.TempPackage, slc.Parent, new NameReference("DrawLightConeComponent", dlcIndex++), prePropBinary, new PropertyCollection
                {
                    new FloatProperty(radius, "ConeRadius"),
                    new FloatProperty(outerConeAngle, "ConeAngle")
                })
                {
                    Class = drawLightConeComponentClass,
                    Archetype = drawOuterConeArchetype
                };
                var drawLightRadius = new ExportEntry(assetInfo.TempPackage, slc.Parent, new NameReference("DrawLightRadiusComponent", dlrIndex++), prePropBinary, new PropertyCollection
                {
                    new FloatProperty(radius, "SphereRadius")
                })
                {
                    Class = drawLightRadiusComponentClass,
                    Archetype = drawLightRadiusArchetype
                };
                var drawLightSourceRadius = new ExportEntry(assetInfo.TempPackage, slc.Parent, new NameReference("DrawLightRadiusComponent", dlrIndex++), prePropBinary, new PropertyCollection
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

                assetInfo.TempPackage.AddExport(drawInnerCone);
                assetInfo.TempPackage.AddExport(drawOuterCone);
                assetInfo.TempPackage.AddExport(drawLightRadius);
                assetInfo.TempPackage.AddExport(drawLightSourceRadius);

                slc.WriteProperty(new ObjectProperty(drawInnerCone.UIndex, "PreviewInnerCone"));
                slc.WriteProperty(new ObjectProperty(drawOuterCone.UIndex, "PreviewOuterCone"));
                slc.WriteProperty(new ObjectProperty(drawLightRadius.UIndex, "PreviewLightRadius"));
                slc.WriteProperty(new ObjectProperty(drawLightSourceRadius.UIndex, "PreviewLightSourceRadius"));
            }
        }

        private static void UDKifyPointLights(UDKAssetInfo assetInfo, IEnumerable<ExportEntry> pointLightComponents)
        {
            assetInfo.TempPackage.GetEntryOrAddImport("Engine.Default__PointLight", "PointLight", packageFile: "Engine");
            assetInfo.TempPackage.GetEntryOrAddImport("Engine.Default__SpotLight", "SpotLight", packageFile: "Engine");
            assetInfo.TempPackage.GetEntryOrAddImport("Engine.Default__DirectionalLight", "DirectionalLight", packageFile: "Engine");

            var drawLightRadiusComponentClass = assetInfo.TempPackage.GetEntryOrAddImport("Engine.DrawLightRadiusComponent", "Class");
            var drawLightRadiusArchetype = assetInfo.TempPackage.GetEntryOrAddImport("Engine.Default__PointLight.DrawLightRadius0", packageFile: "Engine", className: "DrawLightRadiusComponent");
            var drawLightSourceRadiusArchetype = assetInfo.TempPackage.GetEntryOrAddImport("Engine.Default__PointLight.DrawLightSourceRadius0", packageFile: "Engine", className: "DrawLightRadiusComponent");
            int dlrIndex = 1;
            byte[] prePropBinary = new byte[8];
            foreach (ExportEntry plc in pointLightComponents)
            {
                var lightingChannels = plc.GetProperty<StructProperty>("LightingChannels");
                float radius = plc.GetProperty<FloatProperty>("Radius")?.Value ?? 1024f;
                var drawLightRadius = new ExportEntry(assetInfo.TempPackage, plc.Parent, new NameReference("DrawLightRadiusComponent", dlrIndex++), prePropBinary, new PropertyCollection
                {
                    new FloatProperty(radius, "SphereRadius")
                })
                {
                    Class = drawLightRadiusComponentClass,
                    Archetype = drawLightRadiusArchetype
                };
                var drawLightSourceRadius = new ExportEntry(assetInfo.TempPackage, plc.Parent, new NameReference("DrawLightRadiusComponent", dlrIndex++), prePropBinary, new PropertyCollection
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

                assetInfo.TempPackage.AddExport(drawLightRadius);
                assetInfo.TempPackage.AddExport(drawLightSourceRadius);

                plc.WriteProperty(new ObjectProperty(drawLightRadius.UIndex, "PreviewLightRadius"));
                plc.WriteProperty(new ObjectProperty(drawLightSourceRadius.UIndex, "PreviewLightSourceRadius"));
            }
        }
    }
}