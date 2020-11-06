using System.Collections.Generic;
using System.IO;
using System.Linq;
using ME3Explorer.Unreal.Classes;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages.CloningImportingAndRelinking;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;
using SharpDX;
using Utilities = ME3ExplorerCore.Helpers.Utilities;

namespace ME3Explorer.StaticLighting
{
    public static class StaticLightingGenerator
    {
        public static string GenerateUDKFileForLevel(string udkPath, IMEPackage pcc)
        {
            #region AssetPackage

            string meshPackageName = $"{Path.GetFileNameWithoutExtension(pcc.FilePath)}Meshes";
            string meshFile = Path.Combine(udkPath, @"UDKGame\Content\Shared\", $"{meshPackageName}.upk");
            MEPackageHandler.CreateAndSavePackage(meshFile, MEGame.UDK);
            using IMEPackage meshPackage = MEPackageHandler.OpenUDKPackage(meshFile);
            meshPackage.getEntryOrAddImport("Core.Package");

            IEntry defMat = meshPackage.getEntryOrAddImport("EngineMaterials.DefaultMaterial", "Material", "Engine");
            var allMats = new HashSet<int>();
            var relinkMap = new Dictionary<IEntry, IEntry>();
            #region StaticMeshes

            List<ExportEntry> staticMeshes = pcc.Exports.Where(exp => exp.ClassName == "StaticMesh").ToList();
            foreach (ExportEntry mesh in staticMeshes)
            {
                var mats = new Queue<int>();
                StaticMesh stm = ObjectBinary.From<StaticMesh>(mesh);
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
                IEntry newParent = EntryImporter.GetOrAddCrossImportOrPackage(mesh.ParentFullPath, pcc, meshPackage);
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild, mesh, meshPackage, newParent, false, out IEntry ent, relinkMap);
                ExportEntry portedMesh = (ExportEntry)ent;
                stm = ObjectBinary.From<StaticMesh>(portedMesh);
                foreach (StaticMeshRenderData lodModel in stm.LODModels)
                {
                    foreach (StaticMeshElement meshElement in lodModel.Elements)
                    {
                        meshElement.Material = mats.Dequeue();
                    }
                }
                portedMesh.WriteBinary(stm);
            }

            #endregion

            #region Materials
            using (IMEPackage udkResources = MEPackageHandler.OpenMEPackageFromStream(Utilities.GetCustomAppResourceStream(MEGame.UDK)))
            {
                ExportEntry normDiffMat = udkResources.Exports.First(exp => exp.ObjectName == "NormDiffMat");
                foreach (int matUIndex in allMats)
                {
                    if (pcc.GetEntry(matUIndex) is ExportEntry matExp)
                    {
                        List<IEntry> textures = new MaterialInstanceConstant(matExp).Textures;
                        ExportEntry diff = null;
                        ExportEntry norm = null;
                        foreach (IEntry texEntry in textures)
                        {
                            if (texEntry is ExportEntry texport)
                            {
                                if (texport.ObjectName.Name.ToLower().Contains("diff"))
                                {
                                    diff = texport;
                                }
                                else if (texport.ObjectName.Name.ToLower().Contains("norm"))
                                {
                                    norm = texport;
                                }
                            }
                        }
                        if (diff == null)
                        {
                            relinkMap[matExp] = defMat;
                            continue;
                        }
                        else
                        {
                            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, diff, meshPackage, null, false, out IEntry ent);
                            diff = (ExportEntry)ent;
                            diff.RemoveProperty("TextureFileCacheName");
                            diff.RemoveProperty("TFCFileGuid");
                            diff.RemoveProperty("LODGroup");
                        }
                        if (norm != null)
                        {
                            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, norm, meshPackage, null, false, out IEntry ent);
                            norm = (ExportEntry)ent;
                            norm.RemoveProperty("TextureFileCacheName");
                            norm.RemoveProperty("TFCFileGuid");
                            norm.RemoveProperty("LODGroup");
                        }
                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild, normDiffMat, meshPackage, null, true, out IEntry matEnt);
                        ExportEntry newMat = (ExportEntry)matEnt;
                        newMat.ObjectName = matExp.ObjectName;
                        Material matBin = ObjectBinary.From<Material>(newMat);
                        matBin.SM3MaterialResource.UniformExpressionTextures = new UIndex[]{ norm?.UIndex ?? 0, diff.UIndex };
                        newMat.WriteBinary(matBin);
                        relinkMap[matExp] = newMat;
                        if (newMat.GetProperty<ArrayProperty<ObjectProperty>>("Expressions") is {} expressionsProp && expressionsProp.Count >= 2)
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

                var relinkMapping = new OrderedMultiValueDictionary<IEntry, IEntry>(relinkMap);
                foreach (ExportEntry stmExport in staticMeshes)
                {
                    if (relinkMap.TryGetValue(stmExport, out IEntry destEnt) && destEnt is ExportEntry destExp)
                    {
                        Relinker.Relink(stmExport, destExp, relinkMapping);
                    }
                }
            }
            #endregion


            meshPackage.Save();

            #endregion


            var staticMeshActors = new List<ExportEntry>();
            var lightActors = new List<ExportEntry>();
            string tempPackagePath = Path.Combine(App.ExecFolder, $"{Path.GetFileNameWithoutExtension(pcc.FilePath)}.udk");
            File.Copy(Path.Combine(App.ExecFolder, "empty.udk"), tempPackagePath, true);
            using IMEPackage udkPackage = MEPackageHandler.OpenUDKPackage(tempPackagePath);
            {
                var topLevelMeshPackages = new List<IEntry>();
                foreach (ExportEntry exportEntry in staticMeshes)
                {
                    IEntry imp = udkPackage.getEntryOrAddImport($"{exportEntry.FullPath}", "StaticMesh", "Engine", exportEntry.ObjectName.Number);
                    while (imp.Parent != null)
                    {
                        imp = imp.Parent;
                    }
                    if (!topLevelMeshPackages.Contains(imp))
                    {
                        topLevelMeshPackages.Add(imp);
                    }
                }

                ExportEntry levelExport = udkPackage.Exports.First(exp => exp.ClassName == "Level");
                List<int> actorsInLevel = ObjectBinary.From<Level>(pcc.Exports.First(exp => exp.ClassName == "Level")).Actors.Select(u => u.value).ToList();
                var componentToMatrixMap = new Dictionary<int, Matrix>();
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
                    IEntry staticMeshActorClass = udkPackage.getEntryOrAddImport("Engine.StaticMeshActor");
                    udkPackage.getEntryOrAddImport("Engine.Default__StaticMeshActor", "StaticMeshActor", "Engine");
                    IEntry staticMeshComponentArchetype = udkPackage.getEntryOrAddImport("Engine.Default__StaticMeshActor.StaticMeshComponent0",
                                                                                         "StaticMeshComponent", "Engine");
                    int smaIndex = 2;
                    int smcIndex = 2;
                    foreach (ExportEntry smc in pcc.Exports.Where(exp => exp.ClassName == "StaticMeshComponent"))
                    {
                        if (smc.Parent is ExportEntry parent && actorsInLevel.Contains(parent.UIndex) && parent.IsA("StaticMeshActorBase"))
                        {
                            StructProperty locationProp;
                            StructProperty rotationProp;
                            StructProperty scaleProp = null;
                            smc.CondenseArchetypes();
                            if (!(smc.GetProperty<ObjectProperty>("StaticMesh") is { } meshProp) || !pcc.IsUExport(meshProp.Value))
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
                                if (!componentToMatrixMap.TryGetValue(smc.UIndex, out Matrix m))
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

                            ExportEntry sma = new ExportEntry(udkPackage, EntryImporter.CreateStack(MEGame.UDK, staticMeshActorClass.UIndex))
                            {
                                ObjectName = new NameReference("StaticMeshActor", smaIndex++),
                                Class = staticMeshActorClass,
                                Parent = levelExport
                            };
                            sma.ObjectFlags |= UnrealFlags.EObjectFlags.HasStack;
                            udkPackage.AddExport(sma);

                            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, smc, udkPackage,
                                                                 sma, true, out IEntry result);
                            var props = new PropertyCollection
                            {
                                new ObjectProperty(result.UIndex, "StaticMeshComponent"),
                                new NameProperty(new NameReference(Path.GetFileNameWithoutExtension(smc.FileRef.FilePath), smc.UIndex),
                                                 "Tag"),
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
                    IEntry topMeshPackageImport = udkPackage.getEntryOrAddImport(meshPackageName, "Package");
                    foreach (IEntry mp in topLevelMeshPackages)
                    {
                        mp.Parent = topMeshPackageImport;
                    }
                }
                #endregion

                #region LightActors
                {
                    IEntry pointLightClass = udkPackage.getEntryOrAddImport("Engine.PointLight");
                    IEntry spotLightClass = udkPackage.getEntryOrAddImport("Engine.SpotLight");
                    IEntry directionalLightClass = udkPackage.getEntryOrAddImport("Engine.DirectionalLight");

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
                                    if (!componentToMatrixMap.TryGetValue(lightComponent.UIndex, out Matrix m))
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

                                ExportEntry pla = new ExportEntry(udkPackage, EntryImporter.CreateStack(MEGame.UDK, pointLightClass.UIndex))
                                {
                                    ObjectName = new NameReference("PointLight", plaIndex++),
                                    Class = pointLightClass,
                                    Parent = levelExport
                                };
                                pla.ObjectFlags |= UnrealFlags.EObjectFlags.HasStack;
                                udkPackage.AddExport(pla);

                                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, lightComponent, udkPackage, pla, true,
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
                                lightActors.Add(pla);
                                break;
                            case "SpotLightComponent":
                                lightComponent.CondenseArchetypes();
                                lightComponent.ObjectName = new NameReference("SpotLightComponent", slcIndex++);
                                if (parent.ClassName == "StaticLightCollectionActor")
                                {
                                    if (!componentToMatrixMap.TryGetValue(lightComponent.UIndex, out Matrix m))
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

                                ExportEntry sla = new ExportEntry(udkPackage, EntryImporter.CreateStack(MEGame.UDK, spotLightClass.UIndex))
                                {
                                    ObjectName = new NameReference("SpotLight", slaIndex++),
                                    Class = spotLightClass,
                                    Parent = levelExport
                                };
                                sla.ObjectFlags |= UnrealFlags.EObjectFlags.HasStack;
                                udkPackage.AddExport(sla);

                                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, lightComponent, udkPackage, sla, true,
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
                                lightActors.Add(sla);
                                break;
                            case "DirectionalLightComponent":
                                lightComponent.CondenseArchetypes();
                                lightComponent.ObjectName = new NameReference("DirectionalLightComponent", dlcIndex++);
                                if (parent.ClassName == "StaticLightCollectionActor")
                                {
                                    if (!componentToMatrixMap.TryGetValue(lightComponent.UIndex, out Matrix m))
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

                                ExportEntry dla = new ExportEntry(udkPackage, EntryImporter.CreateStack(MEGame.UDK, directionalLightClass.UIndex))
                                {
                                    ObjectName = new NameReference("DirectionalLight", dlaIndex++),
                                    Class = directionalLightClass,
                                    Parent = levelExport
                                };
                                dla.ObjectFlags |= UnrealFlags.EObjectFlags.HasStack;
                                udkPackage.AddExport(dla);

                                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, lightComponent, udkPackage, dla, true,
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
                                lightActors.Add(dla);
                                break;
                        }
                    }
                }
                UDKifyLights(udkPackage);
                #endregion

                Level level = ObjectBinary.From<Level>(levelExport);
                level.Actors = levelExport.GetChildren().Where(ent => ent.IsA("Actor")).Select(ent => new UIndex(ent.UIndex)).ToList();
                levelExport.WriteBinary(level);

                udkPackage.Save();
            }

            string resultFilePath = Path.Combine(udkPath, @"UDKGame\Content\Maps\", $"{Path.GetFileNameWithoutExtension(pcc.FilePath)}.udk");
            using (IMEPackage udkPackage2 = MEPackageHandler.OpenUDKPackage(Path.Combine(App.ExecFolder, "empty.udk")))
            {
                ExportEntry levelExport = udkPackage2.Exports.First(exp => exp.ClassName == "Level");
                Level levelBin = ObjectBinary.From<Level>(levelExport);
                foreach (ExportEntry actor in staticMeshActors)
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild, actor, udkPackage2, levelExport, true, out IEntry result);
                    levelBin.Actors.Add(result.UIndex);
                }
                foreach (ExportEntry actor in lightActors)
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild, actor, udkPackage2, levelExport, true, out IEntry result);
                    levelBin.Actors.Add(result.UIndex);
                }
                levelExport.WriteBinary(levelBin);
                udkPackage2.Save(resultFilePath);
            }
            File.Delete(tempPackagePath);
            return resultFilePath; 
        }

        public static void UDKifyLights(IMEPackage pcc)
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
            var drawLightRadiusComponentClass = pcc.getEntryOrAddImport("Engine.DrawLightRadiusComponent");
            var drawLightConeComponentClass = pcc.getEntryOrAddImport("Engine.DrawLightConeComponent");
            var drawLightRadiusArchetype = pcc.getEntryOrAddImport("Engine.Default__SpotLight.DrawLightRadius0", packageFile: "Engine", className: "DrawLightRadiusComponent");
            var drawLightSourceRadiusArchetype = pcc.getEntryOrAddImport("Engine.Default__SpotLight.DrawLightSourceRadius0", packageFile: "Engine", className: "DrawLightRadiusComponent");
            var drawInnerConeArchetype = pcc.getEntryOrAddImport("Engine.Default__SpotLight.DrawInnerCone0", packageFile: "Engine", className: "DrawLightConeComponent");
            var drawOuterConeArchetype = pcc.getEntryOrAddImport("Engine.Default__SpotLight.DrawOuterCone0", packageFile: "Engine", className: "DrawLightConeComponent");
            int dlcIndex = 1;
            int dlrIndex = 1;
            foreach (ExportEntry slc in spotLightComponents)
            {
                var lightingChannels = slc.GetProperty<StructProperty>("LightingChannels");
                var innerConeAngle = slc.GetProperty<FloatProperty>("InnerConeAngle")?.Value ?? 0f;
                var outerConeAngle = slc.GetProperty<FloatProperty>("OuterConeAngle")?.Value ?? 44f;
                float radius = slc.GetProperty<FloatProperty>("Radius")?.Value ?? 1024f;
                var drawInnerCone = new ExportEntry(pcc, new byte[8], new PropertyCollection
                {
                    new FloatProperty(radius, "ConeRadius"),
                    new FloatProperty(innerConeAngle, "ConeAngle")
                })
                {
                    ObjectName = new NameReference("DrawLightConeComponent", dlcIndex++),
                    Class = drawLightConeComponentClass,
                    Parent = slc.Parent,
                    Archetype = drawInnerConeArchetype
                };
                var drawOuterCone = new ExportEntry(pcc, new byte[8], new PropertyCollection
                {
                    new FloatProperty(radius, "ConeRadius"),
                    new FloatProperty(outerConeAngle, "ConeAngle")
                })
                {
                    ObjectName = new NameReference("DrawLightConeComponent", dlcIndex++),
                    Class = drawLightConeComponentClass,
                    Parent = slc.Parent,
                    Archetype = drawOuterConeArchetype
                };
                var drawLightRadius = new ExportEntry(pcc, new byte[8], new PropertyCollection {new FloatProperty(radius, "SphereRadius")})
                {
                    ObjectName = new NameReference("DrawLightRadiusComponent", dlrIndex++),
                    Class = drawLightRadiusComponentClass,
                    Parent = slc.Parent,
                    Archetype = drawLightRadiusArchetype
                };
                var drawLightSourceRadius = new ExportEntry(pcc, new byte[8], new PropertyCollection {new FloatProperty(32f, "SphereRadius")})
                {
                    ObjectName = new NameReference("DrawLightRadiusComponent", dlrIndex++),
                    Class = drawLightRadiusComponentClass,
                    Parent = slc.Parent,
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
            var drawLightRadiusComponentClass = pcc.getEntryOrAddImport("Engine.DrawLightRadiusComponent");
            var drawLightRadiusArchetype = pcc.getEntryOrAddImport("Engine.Default__PointLight.DrawLightRadius0", packageFile: "Engine", className: "DrawLightRadiusComponent");
            var drawLightSourceRadiusArchetype = pcc.getEntryOrAddImport("Engine.Default__PointLight.DrawLightSourceRadius0", packageFile: "Engine", className: "DrawLightRadiusComponent");
            int dlrIndex = 1;
            foreach (ExportEntry plc in pointLightComponents)
            {
                var lightingChannels = plc.GetProperty<StructProperty>("LightingChannels");
                float radius = plc.GetProperty<FloatProperty>("Radius")?.Value ?? 1024f;
                var drawLightRadius = new ExportEntry(pcc, new byte[8], new PropertyCollection {new FloatProperty(radius, "SphereRadius")})
                {
                    ObjectName = new NameReference("DrawLightRadiusComponent", dlrIndex++),
                    Class = drawLightRadiusComponentClass,
                    Parent = plc.Parent,
                    Archetype = drawLightRadiusArchetype
                };
                var drawLightSourceRadius = new ExportEntry(pcc, new byte[8], new PropertyCollection {new FloatProperty(32f, "SphereRadius")})
                {
                    ObjectName = new NameReference("DrawLightRadiusComponent", dlrIndex++),
                    Class = drawLightRadiusComponentClass,
                    Parent = plc.Parent,
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
