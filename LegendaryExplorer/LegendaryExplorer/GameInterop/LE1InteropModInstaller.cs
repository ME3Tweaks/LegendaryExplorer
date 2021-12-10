using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorer.GameInterop.InteropTargets;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;

namespace LegendaryExplorer.GameInterop
{
    /// <summary>
    /// Subclass of InteropModInstaller for LE1 to select a map and clone in the live edit sequence
    /// </summary>
    public class LE1InteropModInstaller : InteropModInstaller
    {
        private Func<IEnumerable<string>, IEnumerable<string>> SelectMap;

        public LE1InteropModInstaller(InteropTarget target, Func<IEnumerable<string>, IEnumerable<string>> selectMap) :
            base(target)
        {
            SelectMap = selectMap;
        }

        private List<string> le1MapNames = new()
        {
            "BIOA_CRD00", "BIOA_END00", "BIOA_FRE31", "BIOA_FRE32", "BIOA_FRE33", "BIOA_FRE34", "BIOA_FRE35",
            "BIOA_ICE00", "BIOA_JUG00", "BIOA_LAV00", "BIOA_LOS00", "BIOA_NOR00", "BIOA_PRO00", "BIOA_STA00",
            "BIOA_UNC10", "BIOA_UNC11", "BIOA_UNC13", "BIOA_UNC17", "BIOA_UNC20", "BIOA_UNC21", "BIOA_UNC24",
            "BIOA_UNC25", "BIOA_UNC30", "BIOA_UNC31", "BIOA_UNC42", "BIOA_UNC51", "BIOA_UNC52", "BIOA_UNC53",
            "BIOA_UNC54", "BIOA_UNC55", "BIOA_UNC61", "BIOA_UNC62", "BIOA_UNC71", "BIOA_UNC73", "BIOA_UNC80",
            "BIOA_UNC81", "BIOA_UNC82", "BIOA_UNC83", "BIOA_UNC84", "BIOA_UNC90", "BIOA_UNC92", "BIOA_UNC93",
            "BIOA_WAR00", "CUSTOM"
        };

        protected override IEnumerable<string> GetFilesToAugment()
        {
            var maps = SelectMap(le1MapNames).ToList();
            if (!maps.Any()) CancelInstallation = true;
            return maps.Select(s => Path.ChangeExtension(s, ".pcc"));
        }

        protected override void AugmentMapToLoadLiveEditor(IMEPackage pcc)
        {
            if (Target.Game is not MEGame.LE1 || pcc.Game is not MEGame.LE1) throw new Exception("Cannot augment non-LE1 map for LE1 Interop Mod.");

            var mainSequence = pcc.Exports.First(exp => exp.ObjectName == "Main_Sequence" && exp.ClassName == "Sequence");

            // Load the LE1LiveEditor pcc. This is just a donor file for a sequence, it will never be loaded by the game.
            var liveEditorFilename = Path.ChangeExtension(Target.ModInfo.LiveEditorFilename, ".pcc");
            var liveEditorFile = Path.Combine(ModInstallPath, Target.Game.CookedDirName(), liveEditorFilename ?? "");
            if (liveEditorFilename is null || liveEditorFilename == "" || !File.Exists(liveEditorFile)) throw new Exception("Cannot find Live Editor file for LE1.");

            using IMEPackage liveEditor = MEPackageHandler.OpenMEPackage(liveEditorFile);
            var liveEditorSequence = liveEditor.FindExport(@"TheWorld.PersistentLevel.LE1LiveEditor");

            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, liveEditorSequence,
                pcc, mainSequence,
                true, new RelinkerOptionsPackage(), out var clonedEditorSequence);
            KismetHelper.AddObjectToSequence((ExportEntry)clonedEditorSequence, mainSequence);
        }
    }
}