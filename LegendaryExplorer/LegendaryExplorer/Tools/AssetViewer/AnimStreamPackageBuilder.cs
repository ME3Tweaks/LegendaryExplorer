using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.Sequence_Editor.Experiments;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript;

namespace LegendaryExplorer.Tools.AssetViewer
{
    /// <summary>
    /// Builds a package for streaming an animation.
    /// </summary>
    internal class AnimStreamPackageBuilder
    {
        public static IMEPackage BuildAnimationPackage(ExportEntry sourceAnimation)
        {
            var package = MEPackageHandler.CreateMemoryEmptyLevel($"{sourceAnimation.Game}AnimViewer_StreamAnim.pcc", sourceAnimation.Game);

            return package;
        }

        private static void BuildKismet(IMEPackage package)
        {
            // This is going to be ugly...
            PackageCache cache = new PackageCache();
            var lexPackage = ExportCreator.CreatePackageExport(package, "SFXGameContent_LEX");

            // Get list of classes to compile.
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

            var mainSeq = package.FindExport("TheWorld.PersistentLevel.Main_Sequence");

            // Create loading handshake
            var loaded = SequenceObjectCreator.CreateSequenceObject(mainSeq, "SeqEvent_LevelLoaded", cache);
            var fileLoadedRE = SequenceObjectCreator.CreateActivateRemoteEvent(mainSeq, "re_StreamAnimLoaded", cache);
            KismetHelper.CreateOutputLink(loaded, "Loaded and Visible", fileLoadedRE);

            // Create control remote events
            var startAnim = SequenceObjectCreator.CreateSeqEventRemoteActivated(mainSeq, "re_StartAnimation", cache);
            var pauseAnim = SequenceObjectCreator.CreateSeqEventRemoteActivated(mainSeq, "re_PauseAnimation", cache);
            var stopAnim = SequenceObjectCreator.CreateSeqEventRemoteActivated(mainSeq, "re_StopAnimation", cache);

            // Sequence shared variables
            var animationTarget = SequenceObjectCreator.CreateObject(mainSeq, null, cache);

            var interpAct = SequenceObjectCreator.CreateInterp(mainSeq, cache);

            // TODO: Hook this up properly for our animation.
            var interpData = SequenceObjectCreator.CreateInterpData(mainSeq, 1, cache: cache);

            // Events to interp input
            KismetHelper.CreateOutputLink(startAnim, "Out", interpAct, 0);
            KismetHelper.CreateOutputLink(stopAnim, "Out", interpAct, 2);
            KismetHelper.CreateOutputLink(pauseAnim, "Out", interpAct, 3);

            // Hook up variables
            KismetHelper.CreateVariableLink(startAnim, "Instigator", animationTarget);





        }
    }
}
