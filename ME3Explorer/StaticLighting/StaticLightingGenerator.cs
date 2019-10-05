using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.BinaryConverters;
using SharpDX;

namespace ME3Explorer.StaticLighting
{
    public static class StaticLightingGenerator
    {
        public static void GenerateUDKFileForLevel(IMEPackage pcc)
        {
            string meshPackageName = $"{Path.GetFileNameWithoutExtension(pcc.FilePath)}Meshes";
            string meshFile = Path.Combine(@"C:\UDK\Custom\UDKGame\Content\Shared\", $"{meshPackageName}.upk");
            MEPackageHandler.CreateAndSavePackage(meshFile, MEGame.UDK);
            using IMEPackage meshPackage = MEPackageHandler.OpenUDKPackage(meshFile);
            meshPackage.getEntryOrAddImport("Core.Package");

            IEntry defMat = meshPackage.getEntryOrAddImport("EngineMaterials.DefaultMaterial", "Material", "Engine");

            List<ExportEntry> staticMeshes = pcc.Exports.Where(exp => exp.ClassName == "StaticMesh").ToList();

            foreach (ExportEntry mesh in staticMeshes)
            {
                StaticMesh stm = ObjectBinary.From<StaticMesh>(mesh);
                foreach (StaticMeshRenderData lodModel in stm.LODModels)
                {
                    foreach (StaticMeshElement meshElement in lodModel.Elements)
                    {
                        meshElement.Material = 0;
                    }
                }
                if (pcc.GetEntry(stm.BodySetup) is ExportEntry rbBodySetup)
                {
                    rbBodySetup.RemoveProperty("PhysMaterial");
                }
                mesh.setBinaryData(stm.ToBytes(pcc));
                IEntry newParent = EntryImporter.GetOrAddCrossImportOrPackage(mesh.ParentFullPath, pcc, meshPackage);
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild, mesh, meshPackage, newParent, true, out IEntry ent);
                ExportEntry portedMesh = (ExportEntry)ent;
                stm = ObjectBinary.From<StaticMesh>(portedMesh);
                foreach (StaticMeshRenderData lodModel in stm.LODModels)
                {
                    foreach (StaticMeshElement meshElement in lodModel.Elements)
                    {
                        meshElement.Material = defMat.UIndex;
                    }
                }
                portedMesh.setBinaryData(stm.ToBytes(meshPackage));
            }
            meshPackage.Save();


            var staticMeshActors = new List<ExportEntry>();
            using (IMEPackage udkPackage = MEPackageHandler.OpenUDKPackage(Path.Combine(App.ExecFolder, "empty.udk")))
            {

                var topLevelMeshPackages = new List<IEntry>();
                foreach (ExportEntry exportEntry in staticMeshes)
                {
                    IEntry imp = udkPackage.getEntryOrAddImport($"{exportEntry.FullPath}", "StaticMesh", "Engine");
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
                    if (pcc.GetEntry(uIndex) is ExportEntry stmcExp && stmcExp.ClassName == "StaticMeshCollectionActor")
                    {
                        StaticMeshCollectionActor stmc = ObjectBinary.From<StaticMeshCollectionActor>(stmcExp);
                        var components = stmcExp.GetProperty<ArrayProperty<ObjectProperty>>("StaticMeshComponents");
                        for (int i = 0; i < components.Count; i++)
                        {
                            componentToMatrixMap.Add(components[i].Value, stmc.LocalToWorldTransforms[i]);
                        }
                    }
                }

                var emptySMCBin = new byte[4];
                IEntry staticMeshActorClass = udkPackage.getEntryOrAddImport("Engine.StaticMeshActor");
                udkPackage.getEntryOrAddImport("Engine.Default__StaticMeshActor", "StaticMeshActor", "Engine");
                IEntry staticMeshComponentArchetype = udkPackage.getEntryOrAddImport("Engine.Default__StaticMeshActor.StaticMeshComponent0",
                                                                                     "StaticMeshComponent", "Engine");
                int smaIndex = 2;
                int smcIndex = 2;
                int meshCount = 1000;
                foreach (ExportEntry smc in pcc.Exports.Where(exp => exp.ClassName == "StaticMeshComponent"))
                {
                    if (smc.Parent is ExportEntry parent && actorsInLevel.Contains(parent.UIndex))
                    {
                        meshCount--;
                        if (meshCount < 0)
                        {
                            break;
                        }
                        StructProperty locationProp;
                        StructProperty rotationProp;
                        //StructProperty scaleProp;
                        smc.AddArchetypeProperties();
                        smc.setBinaryData(emptySMCBin);
                        smc.RemoveProperty("bBioForcePreComputedShadows");
                        smc.RemoveProperty("bUsePreComputedShadows");
                        smc.RemoveProperty("bAcceptsLights");
                        smc.RemoveProperty("IrrelevantLights");
                        smc.ObjectName = new NameReference("StaticMeshComponent", smcIndex++);
                        if (parent.ClassName == "StaticMeshCollectionActor")
                        {
                            Matrix m = componentToMatrixMap[smc.UIndex];
                            locationProp = CommonStructs.Vector(m.TranslationVector, "Location");
                            rotationProp = CommonStructs.Rotator(m.GetRotator(), "Rotation");
                            //scaleProp = CommonStructs.Vector(m.ScaleVector, "Scale3D");
                            //smc.WriteProperty(CommonStructs.Matrix(m, "CachedParentToWorld"));
                        }
                        else
                        {
                            locationProp = parent.GetProperty<StructProperty>("Location");
                            rotationProp = parent.GetProperty<StructProperty>("Rotation");
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
                        ((ExportEntry)result).Archetype = staticMeshComponentArchetype;
                        var props = new PropertyCollection
                        {
                            new ObjectProperty(result.UIndex, "StaticMeshComponent"),
                            new NameProperty("StaticMeshActor", "Tag"),
                            new ObjectProperty(result.UIndex, "CollisionComponent"),
                            locationProp,
                            rotationProp
                        };
                        sma.WriteProperties(props);
                        staticMeshActors.Add(sma);
                    }
                }

                IEntry topMeshPackageImport = udkPackage.getEntryOrAddImport(meshPackageName, "Package");
                foreach (IEntry mp in topLevelMeshPackages)
                {
                    mp.Parent = topMeshPackageImport;
                }

                Level level = ObjectBinary.From<Level>(levelExport);
                level.Actors = levelExport.GetChildren().Where(ent => ent.IsOrInheritsFrom("Actor")).Select(ent => new UIndex(ent.UIndex)).ToList();
                levelExport.setBinaryData(level);

                udkPackage.Save(Path.Combine(App.ExecFolder, $"{Path.GetFileNameWithoutExtension(pcc.FilePath)}.udk"));
            }


            using (IMEPackage udkPackage2 = MEPackageHandler.OpenUDKPackage(Path.Combine(App.ExecFolder, "empty.udk")))
            {
                ExportEntry levelExport = udkPackage2.Exports.First(exp => exp.ClassName == "Level");
                Level levelBin = ObjectBinary.From<Level>(levelExport);
                foreach (ExportEntry sma in staticMeshActors)
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild, sma, udkPackage2, levelExport, true, out IEntry newSMA);
                    levelBin.Actors.Add(newSMA.UIndex);
                }
                levelExport.setBinaryData(levelBin);
                udkPackage2.Save(Path.Combine(@"C:\UDK\Custom\UDKGame\Content\Maps\", $"{Path.GetFileNameWithoutExtension(pcc.FilePath)}.udk"));
            }
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
