using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Wordprocessing;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.ExperimentsTools;
using LegendaryExplorer.Tools.PathfindingEditor;
using LegendaryExplorer.Tools.Sequence_Editor.Experiments;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using TerraFX.Interop.Windows;

namespace LegendaryExplorer.Tools.AssetViewer
{
    /// <summary>
    /// Level builder for the Preview Asset system. This ensures consistent results across games.
    /// </summary>
    public static class PreviewLevelBuilder
    {
        #region Game Specific Assets
        /// <summary>
        /// Gets asset used for the floor of the level.
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        private static LEXOpenable GetFloorAsset(MEGame game) => game switch
        {
            MEGame.LE1 => new LEXOpenable()
            {
                EntryClass = "StaticMesh",
                EntryPath = "BIOA_ICE50_S.ice70_bigfloor01",
                FilePath = "BIOA_ICE50_13_LAY.pcc"
            },
            MEGame.LE2 => new LEXOpenable()
            {
                EntryClass = "StaticMesh",
                EntryPath = "BioS_AncientRuins_Ext.Floor_8x8_Bend",
                FilePath = "BioA_KroHub_130_MainHubArea.pcc"
            },
            MEGame.LE3 => new LEXOpenable()
            {
                EntryClass = "StaticMesh",
                EntryPath = "BioS_AncientCity.Floor.Floor_8x8",
                FilePath = "BioA_Kro002_500city.pcc"
            },
            _ => throw new NotImplementedException(),
        };

        private static LEXOpenable GetSkyboxAsset(MEGame game) => game switch
        {
            MEGame.LE1 => new LEXOpenable()
            {
                EntryClass = "StaticMesh",
                EntryPath = "BIOG__SKIES__.PRO10.PRO00_skybox",
                FilePath = "BIOA_PRO00.pcc"
            },
            MEGame.LE2 => new LEXOpenable()
            {
                EntryClass = "StaticMesh",
                EntryPath = "BIOA_GalaxyMap_T.Meshes.Space_Skybox",
                FilePath = "BioA_QuaTlL_100.pcc"
            },
            MEGame.LE3 => new LEXOpenable()
            {
                EntryClass = "StaticMesh",
                EntryPath = "BioS_Matrix.SkyBox_01",
                FilePath = "BioA_GthLeg_510.pcc"
            },
            _ => throw new NotImplementedException()
        };

        private static LEXOpenable GetSkyboxMat(MEGame game) => game switch
        {
            MEGame.LE1 => null, // Use mat on mesh.
            MEGame.LE2 => null, // Use mat on mesh.
            //MEGame.LE2 => new LEXOpenable()
            //{
            //    EntryClass = "Material",
            //    EntryPath = "biot_council_int.STA70_SKYB",
            //    FilePath = "BioA_CitHub_400Tower.pcc"
            //},
            MEGame.LE3 => new LEXOpenable()
            {
                EntryClass = "Material",
                EntryPath = "BioT_Matrix.Leg_Void_04",
                FilePath = "BioA_GthLeg_510.pcc"
            },
            _ => throw new NotImplementedException()
        };
        #endregion

        public static IMEPackage BuildAssetViewerLevel(MEGame game)
        {
            var name = GetMapName(game);
            var packageStream = MEPackageHandler.CreateEmptyLevelStream(Path.GetFileNameWithoutExtension(name), game);
            var package = MEPackageHandler.OpenMEPackageFromStream(packageStream, name);

            // Skybox
            var skyboxSMA = AddStaticMeshActor(package, GetSkyboxAsset(game), new Point3D(0, 0, 0), "SkyBox", material: GetSkyboxMat(game));
            PathEdUtils.SetDrawScale3D(skyboxSMA, 375, 375, 375);

            // Player Start
            AddStaticMeshActor(package, GetFloorAsset(game), new Point3D(0, 0, 100000), "PlayerFloor");
            if (game == MEGame.LE1)
            {
                AddPlayerStart(package, new Point3D(2078, -1878, 92)); // middle of the high mesh.
                //AddPlayerStart(package, new Point3D(-883, -5256, 1200)); // Next to 0 0 0
            }
            if (game == MEGame.LE2)
            {
                AddPlayerStart(package, new Point3D(2078, -1878, 92)); // middle of the high mesh.
                //AddPlayerStart(package, new Point3D(-883, -5256, 1200)); // Next to 0 0 0
            }
            else if (game == MEGame.LE3)
            {
                //AddPlayerStart(package, new Point3D(-415, -420, 100100)); // middle of the high mesh.
                AddPlayerStart(package, new Point3D(-404, -151, 91)); // Next to 0 0 0
            }

            // Static mesh lighting
            AddDominantLight(package);

            // Dynamic mesh lighting
            AddSkyLight(package);

            // Animation area
            // This should be adjusted for each game as each asset will have different origin.
            var animationFloor = AddStaticMeshActor(package, GetFloorAsset(game), GetAnimationFloorPosition(game), "AnimationFloor");
            PathEdUtils.SetDrawScale3D(animationFloor, 20, 20, 1);

            BuildKismet(package);

            // Until we have a way to stream in/out what's not in the level list we have to do this
            PackageAutomations.AddStreamingKismet(package, Path.GetFileNameWithoutExtension(AnimStreamPackageBuilder.GetStreamingPackageName(game)), true);
            PackageAutomations.AddStreamingKismet(package, Path.GetFileNameWithoutExtension(ActorStreamPackageBuilder.GetStreamingPackageName(game)), true);

            PackageAutomations.AddStreamingKismet(package, Path.GetFileNameWithoutExtension(AnimStreamPackageBuilder.GetStreamingPackageName(game, true)), true);
            PackageAutomations.AddStreamingKismet(package, Path.GetFileNameWithoutExtension(ActorStreamPackageBuilder.GetStreamingPackageName(game, true)), true);


            // For debugging.
            if (game == MEGame.LE1)
            {
                package.Save(MEDirectories.GetCookedPath(game) + @"\BIOA_AssetViewerDebug.pcc");
            }
            else
            {
                package.Save(MEDirectories.GetCookedPath(game) + @"\BioP_AssetViewerDebug.pcc");
            }
            return package;
        }

        private static Point3D GetAnimationFloorPosition(MEGame game)
        {
            switch (game)
            {
                case MEGame.LE1:
                case MEGame.LE3:
                    return new Point3D(8000, 8000, 0);
                case MEGame.LE2:
                    return new Point3D(0, 8000, 0);
            }

            return new Point3D();
        }

        /// <summary>
        /// Sets 1 material in the Materials property array for this target
        /// </summary>
        private static void SetMaterial(ExportEntry target, LEXOpenable materialOpenable)
        {
            var exp = GetOpenableExport(target.FileRef, materialOpenable);
            EntryExporter.ExportExportToPackage(exp, target.FileRef, out var portedMat);
            target.WriteProperty(new ArrayProperty<ObjectProperty>("Materials")
            {
                new ObjectProperty(portedMat)
            });
        }

        private static void AddDominantLight(IMEPackage package)
        {
            // Dominant Lights only affect static
            var level = package.GetLevel();
            var dominantLight = ExportCreator.CreateExport(package, "DominantLight", "DominantDirectionalLight", level, createWithStack: true);
            var dominantLightC = ExportCreator.CreateExport(package, "DominantDirectionalLightComponent", "DominantDirectionalLightComponent", dominantLight, prePropBinary: new byte[12]);
            dominantLightC.WriteBinary(LightComponent.Create());

            // Setup actor
            dominantLight.WriteProperty(new ObjectProperty(dominantLightC, "LightComponent"));
            PathEdUtils.SetLocation(dominantLight, 0, 0, 150000);

            // Setup component
            dominantLightC.WriteProperty(new FloatProperty(2, "Brightness"));

            PropertyCollection channels = new PropertyCollection();
            channels.Add(new BoolProperty(true, "Static"));
            dominantLightC.WriteProperty(new StructProperty("LightingChannelContainer", channels, "LightingChannels"));

            AddActorToLevel(dominantLight);
        }

        /// <summary>
        /// Generates a lighting channels struct with the given channels set to true. They are all bool property names.
        /// </summary>
        /// <param name="export"></param>
        /// <param name="channels"></param>
        internal static void SetLightingChannels(ExportEntry export, params string[] channels)
        {
            PropertyCollection channelsP = new PropertyCollection();
            channelsP.ReplaceAll(channels.Select(x => new BoolProperty(true, x)));
            export.WriteProperty(new StructProperty("LightingChannelContainer", channelsP, "LightingChannels"));
        }

        private static void AddSkyLight(IMEPackage package)
        {
            var level = package.GetLevel();
            var skyLight = ExportCreator.CreateExport(package, "SkyLight", "SkyLight", level, createWithStack: true);
            var skyLightC = ExportCreator.CreateExport(package, "SkyLightComponent", "SkyLightComponent", skyLight, prePropBinary: new byte[8]);
            skyLightC.WriteBinary(LightComponent.Create());
            skyLightC.Archetype = GetImportArchetype(package, "Engine", "Default__SkyLight.SkyLightComponent0");

            // Setup actor
            skyLight.WriteProperty(new ObjectProperty(skyLightC, "LightComponent"));
            PathEdUtils.SetLocation(skyLight, 0, 0, 100000);

            // Setup component
            skyLightC.WriteProperty(new FloatProperty(1.1f, "Brightness"));

            PropertyCollection channels = new PropertyCollection();
            channels.Add(new BoolProperty(true, "Dynamic"));
            channels.Add(new BoolProperty(true, "CompositeDynamic"));
            skyLightC.WriteProperty(new StructProperty("LightingChannelContainer", channels, "LightingChannels"));

            AddActorToLevel(skyLight);
        }

        /// <summary>
        /// Required for level to load
        /// </summary>
        /// <param name="package"></param>
        /// <param name="loc">Where to place the start position</param>
        private static void AddPlayerStart(IMEPackage package, Point3D loc)
        {
            var level = package.GetLevel();
            ExportEntry startLoc = null;
            if (package.Game is MEGame.LE1 or MEGame.LE2)
            {
                startLoc = ExportCreator.CreateExport(package, "PlayerStart", "PlayerStart", level, createWithStack: true);

                // Start locations are done via looking at navigation. BioStartLocation exists, but it doesn't work for map load.
                var levelBin = package.GetLevelBinary();
                levelBin.NavListStart = startLoc.UIndex;
                levelBin.NavListEnd = startLoc.UIndex;
                level.WriteBinary(levelBin);
            }
            else if (package.Game is MEGame.LE3)
            {
                startLoc = ExportCreator.CreateExport(package, "BioStartLocation", "BioStartLocation", level, createWithStack: true);
            }

            var cylinderComp = SharedMethods.CreateExport(package, "CylinderComponent", "CylinderComponent", startLoc, prePropBinary: new byte[8]); // NetIndex & TemplatedOwnerClass
            cylinderComp.ObjectFlags |= UnrealFlags.EObjectFlags.Transactional;
            cylinderComp.Archetype = GetImportArchetype(package, (startLoc.Class as ImportEntry).GetRootName(), $"Default__{startLoc.ClassName}.CollisionCylinder");

            cylinderComp.WriteProperty(new ObjectProperty(0, "ReplacementPrimitive"));
            startLoc.WriteProperty(new ObjectProperty(cylinderComp, "CollisionComponent"));
            PathEdUtils.SetLocation(startLoc, loc);

            AddActorToLevel(startLoc);
        }



        private static ExportEntry AddStaticMeshActor(IMEPackage package, LEXOpenable meshAsset, Point3D location,
            string name,
            LEXOpenable material = null)
        {
            var level = package.GetLevel();
            var meshSMA = ExportCreator.CreateExport(package, name, "StaticMeshActor", level, createWithStack: true);
            PathEdUtils.SetLocation(meshSMA, location);
            var meshSMC = ExportCreator.CreateExport(package, "StaticMeshComponent", "StaticMeshComponent", meshSMA);
            meshSMC.ObjectFlags |= UnrealFlags.EObjectFlags.Transactional;
            meshSMA.WriteProperty(new ObjectProperty(meshSMC, "StaticMeshComponent"));
            meshSMA.WriteProperty(new ObjectProperty(meshSMC, "CollisionComponent"));
            meshSMC.WriteProperty(new ObjectProperty(0, "ReplacementPrimitive"));
            meshSMC.Archetype = GetImportArchetype(package, "Engine", "Default__StaticMeshActor.StaticMeshComponent0");

            var smcb = ObjectBinary.From<StaticMeshComponent>(meshSMC);
            smcb.LODData = new StaticMeshComponentLODInfo[]
            {
                new StaticMeshComponentLODInfo()
                {
                    LightMap = new LightMap()
                    {
                        LightMapType = ELightMapType.LMT_None
                    }
                }
            };
            meshSMC.WriteBinary(smcb);

            var exp = GetOpenableExport(package, meshAsset);
            EntryExporter.ExportExportToPackage(exp, package, out var staticMesh);
            meshSMC.WriteProperty(new ObjectProperty(staticMesh, "StaticMesh"));

            if (material != null)
                SetMaterial(meshSMC, material);


            AddActorToLevel(meshSMA);
            return meshSMA;
        }

        internal static IEntry GetImportArchetype(IMEPackage package, string packageFile, string ifp)
        {
            IEntry result = package.FindExport($"{packageFile}.{ifp}");
            if (result != null)
                return result;

            var file = $"{packageFile}.pcc";
            var fullPath = Path.Combine(MEDirectories.GetCookedPath(package.Game), file);
            using var lookupPackage = MEPackageHandler.UnsafePartialLoad(fullPath, x => false);
            var entry = lookupPackage.FindExport(ifp) as IEntry;
            if (entry == null)
                Debugger.Break();

            Stack<IEntry> children = new Stack<IEntry>();
            children.Push(entry); // Must port at least the found IFP.
            while (entry.Parent != null && package.FindEntry(entry.ParentInstancedFullPath) == null)
            {
                children.Push(entry.Parent);
                entry = entry.Parent;
            }

            // Create imports from top down.
            var packageExport = (IEntry)ExportCreator.CreatePackageExport(package, packageFile);

            // This doesn't work if the part of the parents already exist.
            var attachParent = packageExport;
            foreach (var item in children)
            {
                ImportEntry imp = new ImportEntry(item as ExportEntry, packageExport.UIndex, package);
                imp.idxLink = attachParent.UIndex;
                package.AddImport(imp);
                attachParent = imp;
                result = imp;
            }

            return result;
        }


        private static ExportEntry GetOpenableExport(IMEPackage package, LEXOpenable meshAsset)
        {
            var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(package.Game);
            if (loadedFiles.TryGetValue(meshAsset.FilePath, out var path))
            {
                using var assetPackage = MEPackageHandler.OpenMEPackage(path);
                return assetPackage.FindExport(meshAsset.EntryPath);
            }
            else
            {
                Debugger.Break();
            }

            return null;
        }

        private static void SetVector(ExportEntry export, string name, float x, float y, float z)
        {
            export.WriteProperty(CommonStructs.Vector3Prop(x, y, z, name));
        }

        internal static void AddActorToLevel(ExportEntry actor)
        {
            // This would be nice as an extension
            var levelBin = actor.FileRef.GetLevelBinary();
            levelBin.Actors.Add(actor.UIndex);
            levelBin.Export.WriteBinary(levelBin);
        }

        /// <summary>
        /// Gets the map filename, with the extension included.
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static string GetMapName(MEGame game)
        {
            return game == MEGame.LE1 ? "BIOA_AssetViewer.pcc" : "BioP_AssetViewer.pcc"; // for LE3 BIO_COMMON.
        }

        private static void BuildKismet(IMEPackage package)
        {
            // This is going to be ugly...

            var lexPackage = ExportCreator.CreatePackageExport(package, "SFXGameContent_LEX");

            // Get list of classes to compile
            #region CLASS COMPILE
            var classes = new List<UnrealScriptCompiler.LooseClass>();
            foreach (string ucFilePath in Directory.EnumerateFiles(Path.Combine(AppDirectories.ExecFolder, "AssetViewerKismetClasses"), "*.uc", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(ucFilePath);
                //files must be named Classname.uc
                classes.Add(new UnrealScriptCompiler.LooseClass(Path.GetFileNameWithoutExtension(ucFilePath), source));
            }


            var looseClassPackages = new List<UnrealScriptCompiler.LooseClassPackage>
            {
                new UnrealScriptCompiler.LooseClassPackage(lexPackage.ObjectName, classes)
            };

            UnrealScriptOptionsPackage usop = new UnrealScriptOptionsPackage();
            MessageLog log = UnrealScriptCompiler.CompileLooseClasses(package, looseClassPackages, usop);

            if (log.HasErrors)
            {
                Debugger.Break();
            }

            // Inventory classes so we can use them
            SequenceEditorExperimentsM.LoadCustomClassesFromPackage(package);
            #endregion

            PackageCache cache = new PackageCache();

            var mainSeq = package.FindExport("TheWorld.PersistentLevel.Main_Sequence");
            var animationTarget = SequenceObjectCreator.CreateObject(mainSeq, null, cache);

            // Initial load
            {
                var loaded = SequenceObjectCreator.CreateLevelLoaded(mainSeq, cache);
                var player = SequenceObjectCreator.CreatePlayerObject(mainSeq, false, cache);
                var setObject = SequenceObjectCreator.CreateSetObject(mainSeq, animationTarget, player, cache);
                var toggleHud = SequenceObjectCreator.CreateToggleHUD(mainSeq, player, cache);
                var sendLoaded = SequenceObjectCreator.CreateSendMessageToLEX(mainSeq, "ASSETVIEWER LOADED", cache);

                // Initial load - Logic
                KismetHelper.CreateOutputLink(loaded, "Loaded and Visible", setObject);
                KismetHelper.CreateOutputLink(setObject, "Out", toggleHud, 1); // -> Hide
                KismetHelper.CreateOutputLink(toggleHud, "Out", sendLoaded);
            }

            // LEX polling
            {
                // When we poll if asset is loaded we send READY, not LOADED.
                var pollEvent = SequenceObjectCreator.CreateSeqEventRemoteActivated(mainSeq, "re_IsOnAssetViewerMap", cache);
                var sendLoaded = SequenceObjectCreator.CreateSendMessageToLEX(mainSeq, "ASSETVIEWER READY", cache);
                KismetHelper.CreateOutputLink(pollEvent, "Out", sendLoaded);
            }

            // Asset stream listeners
            CreateLoadingListener(package, "re_StreamAnimLoaded", "re_StartAnimation", instigatorOnEventToFire: animationTarget, lexMessage: "ASSETVIEWER ANIMATIONLOADED", cache: cache);
            CreateLoadingListener(package, "re_StreamActorLoaded", instigatorOnSignal: animationTarget, lexMessage: "ASSETVIEWER ACTORLOADED", cache: cache);
        }

        /// <summary>
        /// Creates a loading handshake that notifies via remote event that the package has loaded. LevelLoaded -> Remote Event with the given name.
        /// </summary>
        /// <param name="package">Package to make handshake in</param>
        /// <param name="eventName">The name of the event to invoke that another file is listening for</param>
        /// <param name="instigator">Optional: The object to connect to the instigator terminal</param>
        /// <param name="cache">Cache to improve performance</param>
        public static void CreateLoadingHandshake(IMEPackage package, string eventName, ExportEntry instigator = null, PackageCache cache = null)
        {
            var mainSeq = package.FindExport("TheWorld.PersistentLevel.Main_Sequence");
            var loaded = SequenceObjectCreator.CreateSequenceObject(mainSeq, "SeqEvent_LevelLoaded", cache);
            var fileLoadedRE = SequenceObjectCreator.CreateActivateRemoteEvent(mainSeq, eventName, cache);
            if (instigator != null)
            {
                KismetHelper.CreateVariableLink(fileLoadedRE, "Instigator", instigator);
            }

            KismetHelper.CreateOutputLink(loaded, "Loaded and Visible", fileLoadedRE);
        }

        /// <summary>
        /// Creates a listener for a loading handshake - RemoteEventListener -> Remote Event with the given name.
        /// </summary>
        /// <param name="package"></param>
        /// <param name="eventName"></param>
        /// <param name="cache"></param>
        public static void CreateLoadingListener(IMEPackage package, string eventName, string eventToFire = null, 
            ExportEntry instigatorOnSignal = null,
            ExportEntry instigatorOnEventToFire = null,
            string lexMessage = null, 
            PackageCache cache = null)
        {
            var mainSeq = package.FindExport("TheWorld.PersistentLevel.Main_Sequence");
            var loaded = SequenceObjectCreator.CreateSeqEventRemoteActivated(mainSeq, eventName, cache); // fires when package has loaded
            if (instigatorOnSignal != null)
            {
                KismetHelper.CreateVariableLink(loaded, "Instigator", instigatorOnSignal);
            }

            if (eventToFire != null)
            {
                var fileLoadedRE = SequenceObjectCreator.CreateActivateRemoteEvent(mainSeq, eventToFire, cache);
                KismetHelper.CreateOutputLink(loaded, "Out", fileLoadedRE);
                if (instigatorOnEventToFire != null)
                {
                    KismetHelper.CreateVariableLink(fileLoadedRE, "Instigator", instigatorOnEventToFire);
                }

                loaded = fileLoadedRE;
            }

            if (lexMessage != null)
            {
                var sendLoaded = SequenceObjectCreator.CreateSequenceObject(mainSeq, "SeqAct_SendMessageToLEX", cache);
                var sendLoadedString = SequenceObjectCreator.CreateString(mainSeq, lexMessage, cache);

                // Initial load - Logic
                KismetHelper.CreateOutputLink(loaded, "Out", sendLoaded);
                KismetHelper.CreateVariableLink(sendLoaded, "MessageName", sendLoadedString);
                loaded = sendLoaded;
            }
        }

        /// <summary>
        /// Sets up a Remote Event -> Send Message To LEX, so LEX can invoke a RemoteEvent to determine if file is loaded or not.
        /// </summary>
        /// <param name="package"></param>
        /// <param name="eventName"></param>
        /// <param name="cache"></param>
        public static void CreateLEXLoadedPoll(IMEPackage package, string eventName, PackageCache cache = null)
        {
            var mainSeq = package.FindExport("TheWorld.PersistentLevel.Main_Sequence");
            var loaded = SequenceObjectCreator.CreateSeqEventRemoteActivated(mainSeq, "re_PreviewLevelPoll", cache);
            var fileLoadedRE = SequenceObjectCreator.CreateActivateRemoteEvent(mainSeq, eventName, cache);
            KismetHelper.CreateOutputLink(loaded, "Loaded and Visible", fileLoadedRE);
        }
    }
}
