using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.ExtendedProperties;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.InterpEditor;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.Tools.Sequence_Editor.Experiments;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Collections;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Application = System.Windows.Application;
using DocumentFormat.OpenXml.Wordprocessing;

namespace LegendaryExplorer.Tools.AssetViewer
{
    /// <summary>
    /// Builds a package for streaming an animation.
    /// </summary>
    internal class AnimStreamPackageBuilder
    {
        /// <summary>
        /// Loads the GestureConfig map and finds the mapping for htis
        /// </summary>
        /// <param packageName="game">Game this animation is for</param>
        /// <param packageName="packageName">Name of the package export containing the animation</param>
        /// <returns></returns>
        public static NameReference GetOriginalSetName(ExportEntry animSequence)
        {
            var prop = animSequence.GetProperty<ObjectProperty>("m_pBioAnimSetData");
            if (prop != null)
            {
                var basd = prop.ResolveToEntry(animSequence.FileRef);
                if (basd.ObjectName.Name.EndsWith("_BioAnimSetData"))
                {
                    return basd.ObjectName.Name[..^15];
                }
            }

            /*
            var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(game);

            string gesturesFile = null;
            if (game is MEGame.LE1)
            {
                // It's also in BIOG_GesturesConfig, but this is likely unused as it has same memory path.
                // Technically that would be faster to load; however we should use the one that the game is using.
                loadedFiles.TryGetValue("SFXGame.pcc", out gesturesFile);
            }
            else if (game is MEGame.LE2 or MEGame.LE3)
            {
                loadedFiles.TryGetValue("GesturesConfigDLC.pcc", out gesturesFile);
                if (gesturesFile == null)
                    loadedFiles.TryGetValue("GesturesConfig.pcc", out gesturesFile);
            }
            */



            // This is not reliable as multiple animsetdatas are under one package export
            //using var gesturesPackage = MEPackageHandler.UnsafePartialLoad(gesturesFile, x => !x.IsDefaultObject && x.ClassName == "BioGestureRuntimeData");

            //// packageName can change if it's dlc so we just do this
            //var gestureRuntimeData = gesturesPackage.Exports.FirstOrDefault(x => x.IsDataLoaded());
            //var gestMap = ObjectBinary.From<BioGestureRuntimeData>(gestureRuntimeData);
            //foreach (var map in gestMap.m_mapAnimSetOwners)
            //{
            //    if (map.Value == packageName)
            //    {
            //        return map.Key;
            //    }
            //}
            return NameReference.None;
        }

        public static IMEPackage BuildAnimationPackage(ExportEntry sourceAnimation)
        {
            var package = MEPackageHandler.CreateMemoryEmptyLevel(GetStreamingPackageName(sourceAnimation.Game), sourceAnimation.Game);
            EntryExporter.ExportExportToPackage(sourceAnimation, package, out var newAnim);
            BuildKismet(package, newAnim);
            //Application.Current.Dispatcher.Invoke(() =>
            //{
            //    PackageEditorWindow pe = new PackageEditorWindow();
            //    pe.LoadPackage(package);
            //    pe.Show();
            //});

            // Save package for debugging
            package.Save($@"{MEDirectories.GetCookedPath(sourceAnimation.Game)}\{GetStreamingPackageName(sourceAnimation.Game, true)}");
            return package;
        }

        public static string GetStreamingPackageName(MEGame game, bool isDebug = false)
        {
            if (isDebug)
            {
                return $"{game}AssetViewer_StreamAnimDebug.pcc";
            }
            return $"{game}AssetViewer_StreamAnim.pcc";
        }

        private static void BuildKismet(IMEPackage package, IEntry animSequenceRef)
        {
            // This is going to be ugly...
            PackageCache cache = new PackageCache();
            //var lexPackage = ExportCreator.CreatePackageExport(package, "SFXGameContent_LEX");

            // Get list of classes to compile.
            //#region CLASS COMPILE
            //var classes = new List<UnrealScriptCompiler.LooseClass>();
            //foreach (string ucFilePath in Directory.EnumerateFiles(Path.Combine(AppDirectories.ExecFolder, "AssetViewerKismetClasses"), "*.uc", SearchOption.AllDirectories))
            //{
            //    string source = File.ReadAllText(ucFilePath);
            //    //files must be named Classname.uc
            //    classes.Add(new UnrealScriptCompiler.LooseClass(Path.GetFileNameWithoutExtension(ucFilePath), source));
            //}


            //var looseClassPackages = new List<UnrealScriptCompiler.LooseClassPackage>
            //{
            //    new UnrealScriptCompiler.LooseClassPackage(lexPackage.ObjectName, classes)
            //};

            //UnrealScriptOptionsPackage usop = new UnrealScriptOptionsPackage();
            //MessageLog log = UnrealScriptCompiler.CompileLooseClasses(package, looseClassPackages, usop);

            //if (log.HasErrors)
            //{
            //    Debugger.Break();
            //}

            //// Inventory classes so we can use them
            //SequenceEditorExperimentsM.LoadCustomClassesFromPackage(package);
            //#endregion

            ExportEntry animSequence = animSequenceRef as ExportEntry;
            ;
            if (animSequence is null && animSequenceRef is ImportEntry imp)
            {
                animSequence = EntryImporter.ResolveImport(imp, cache);
            }

            var mainSeq = package.FindExport("TheWorld.PersistentLevel.Main_Sequence");
            var sequenceName = animSequence.GetProperty<NameProperty>("SequenceName").Value;

            var dynAnimSet = ExportCreator.CreateExport(package, "KIS_DYN_AnimSet", "BioDynamicAnimSet", mainSeq);
            dynAnimSet.WriteProperty(new ArrayProperty<ObjectProperty>([animSequence], "Sequences"));
            dynAnimSet.WriteBinary(new BioDynamicAnimSet()
            {
                SequenceNamesToUnkMap = new UMultiMap<NameReference, int>(
                    [
                        new KeyValuePair<NameReference, int>(sequenceName,1)
                    ])
            });
            // We can probably guess this, but we should probably just ensure it via the gesture map.
            dynAnimSet.WriteProperty(new NameProperty(GetOriginalSetName(animSequence), "m_nmOrigSetName"));
            dynAnimSet.WriteProperty(animSequence.GetProperty<ObjectProperty>("m_pBioAnimSetData")); // copy from anim

            if (package.Game is MEGame.LE1)
            {
                // May also be 2
                mainSeq.WriteProperty(new ArrayProperty<ObjectProperty>([dynAnimSet], "m_aBioDynAnimSets"));
            }
            else
            {
                mainSeq.WriteProperty(new ArrayProperty<ObjectProperty>([dynAnimSet], "m_aSFXSharedAnimSets"));
            }

            // Create loading handshake
            PreviewLevelBuilder.CreateLoadingHandshake(package, "re_StreamAnimLoaded");

            // Create control remote events
            var startAnim = SequenceObjectCreator.CreateSeqEventRemoteActivated(mainSeq, "re_StartAnimation", cache);
            var pauseAnim = SequenceObjectCreator.CreateSeqEventRemoteActivated(mainSeq, "re_PauseAnimation", cache);
            var stopAnim = SequenceObjectCreator.CreateSeqEventRemoteActivated(mainSeq, "re_StopAnimation", cache);

            // Sequence shared variables
            var animationTarget = SequenceObjectCreator.CreateObject(mainSeq, null, cache);

            var interpAct = SequenceObjectCreator.CreateInterp(mainSeq, cache);
            interpAct.WriteProperty(new BoolProperty(true, "bLooping"));
            KismetHelper.AddVariableLink(interpAct, "Animation", "SeqVar_Object");

            var interpData = SequenceObjectCreator.CreateInterpData(mainSeq, 1, cache: cache);
            interpData.WriteProperty(new FloatProperty(animSequence.GetProperty<FloatProperty>("SequenceLength"), "InterpLength"));

            // Events to interp input
            KismetHelper.CreateOutputLink(startAnim, "Out", interpAct, 0);
            KismetHelper.CreateOutputLink(stopAnim, "Out", interpAct, 2);
            KismetHelper.CreateOutputLink(pauseAnim, "Out", interpAct, 3);


            // Hook up variables
            KismetHelper.CreateVariableLink(startAnim, "Instigator", animationTarget);
            KismetHelper.CreateVariableLink(interpAct, "Data", interpData);
            KismetHelper.CreateVariableLink(interpAct, "Animation", animationTarget);


            // Hook up animation data.
            var interpGroupEx = ExportCreator.CreateExport(package, "InterpGroup", "InterpGroup", interpData);
            interpData.WriteProperty(new ArrayProperty<ObjectProperty>([interpGroupEx], "InterpGroups"));

            var track = ExportCreator.CreateExport(package, "InterpTrackAnimControl", "InterpTrackAnimControl", interpGroupEx);


            interpGroupEx.WriteProperty(new ArrayProperty<ObjectProperty>([new ObjectProperty(track)], "InterpTracks"));
            interpGroupEx.WriteProperty(new ArrayProperty<ObjectProperty>([new ObjectProperty(dynAnimSet)], "GroupAnimSets"));
            interpGroupEx.WriteProperty(new NameProperty("Animation", "GroupName"));

            var animStructProps = GlobalUnrealObjectInfo.getDefaultStructValue(package.Game, "AnimControlTrackKey", true, package, cache);
            animStructProps.AddOrReplaceProp(new NameProperty(sequenceName, "AnimSeqName"));
            var animStruct = new StructProperty("AnimControlTrackKey", animStructProps);
            track.WriteProperty(new ArrayProperty<StructProperty>([animStruct], "AnimSeqs"));
        }
    }
}
