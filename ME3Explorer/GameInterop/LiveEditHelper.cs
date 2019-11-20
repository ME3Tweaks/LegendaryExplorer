using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.AutoTOC;
using ME3Explorer.Packages;
using ME3Explorer.Sequence_Editor;
using ME3Explorer.Unreal;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json;

namespace ME3Explorer.GameInterop
{
    public static class LiveEditHelper
    {
        //INCREMENT THIS WHEN CHANGES ARE MADE THAT WOULD REQUIRE REGENERATION OF DLC_MOD_Interop
        public const int CURRENT_VERSION = 1;

        const string liveEditorFileName = "ME3LiveEditor";

        //me3 pcc must be a map, and must have at least one BioTriggerStream and LevelStreamingKismet
        static void AugmentMapToLoadLiveEditor(IMEPackage pcc)
        {
            const string stateName = "SS_LIVEEDITOR";

            var mainSequence = pcc.Exports.First(exp => exp.ObjectName == "Main_Sequence" && exp.ClassName == "Sequence");
            var bioWorldInfo = pcc.Exports.First(exp => exp.ClassName == "BioWorldInfo");

            #region Sequencing

            var consoleEvent = new ExportEntry(pcc, properties: SequenceObjectCreator.GetSequenceObjectDefaults(pcc, UnrealObjectInfo.GetClassOrStructInfo(MEGame.ME3, "SeqEvent_Console")))
            {
                ObjectName = pcc.GetNextIndexedName("SeqEvent_Console"),
                Class = EntryImporter.EnsureClassIsInFile(pcc, "SeqEvent_Console"),
                Parent = mainSequence
            };
            pcc.AddExport(consoleEvent);
            consoleEvent.WriteProperty(new NameProperty("LoadLiveEditor", "ConsoleEventName"));

            var setStreamingState = new ExportEntry(pcc, properties: SequenceObjectCreator.GetSequenceObjectDefaults(pcc, UnrealObjectInfo.GetClassOrStructInfo(MEGame.ME3, "BioSeqAct_SetStreamingState")))
            {
                ObjectName = pcc.GetNextIndexedName("BioSeqAct_SetStreamingState"),
                Class = EntryImporter.EnsureClassIsInFile(pcc, "BioSeqAct_SetStreamingState"),
                Parent = mainSequence
            };
            pcc.AddExport(setStreamingState);
            setStreamingState.WriteProperty(new NameProperty(stateName, "StateName"));
            setStreamingState.WriteProperty(new BoolProperty(true, "NewValue"));

            KismetHelper.AddObjectToSequence(consoleEvent, mainSequence);
            KismetHelper.AddObjectToSequence(setStreamingState, mainSequence);
            KismetHelper.CreateOutputLink(consoleEvent, "Out", setStreamingState, 0);

            #endregion

            ExportEntry lsk = EntryCloner.CloneEntry(pcc.Exports.First(exp => exp.ClassName == "LevelStreamingKismet"));
            lsk.WriteProperty(new NameProperty(liveEditorFileName, "PackageName"));

            var streamingLevels = bioWorldInfo.GetProperty<ArrayProperty<ObjectProperty>>("StreamingLevels");
            streamingLevels.Add(new ObjectProperty(lsk));
            bioWorldInfo.WriteProperty(streamingLevels);

            ExportEntry bts = EntryCloner.CloneTree(pcc.Exports.First(exp => exp.ClassName == "BioTriggerStream"));
            var streamingStates = bts.GetProperty<ArrayProperty<StructProperty>>("StreamingStates");
            while (streamingStates.Count > 1)
            {
                streamingStates.RemoveAt(streamingStates.Count - 1);
            }
            streamingStates.Add(new StructProperty("BioStreamingState", new PropertyCollection
            {
                  new NameProperty(stateName, "StateName"),
                  new ArrayProperty<NameProperty>("VisibleChunkNames")
                  {
                      new NameProperty(liveEditorFileName)
                  }
            }));
            bts.WriteProperty(streamingStates);

            pcc.AddToLevelActorsIfNotThere(bts);
        }

        public const string modName = "DLC_MOD_Interop";
        public static string ModInstallPath => Path.Combine(ME3Directory.DLCPath, modName);
        public static string InstallInfoPath => Path.Combine(ModInstallPath, "InstallInfo.json");

        public static void InstallDLC_MOD_Interop()
        {
            if (Directory.Exists(ModInstallPath))
            {
                Directory.Delete(ModInstallPath, true);
            }
            Dictionary<string, string> fileMap = MELoadedFiles.GetFilesLoadedInGame(MEGame.ME3, true);

            string sourcePath = Path.Combine(App.ExecFolder, modName);
            FileSystem.CopyDirectory(sourcePath, ModInstallPath);


            const string bioPGlobalFileName = "BioP_Global.pcc";
            const string bioPGlobalNCFileName = "BioP_Global_NC.pcc";
            var sourceFiles = new List<(string filePath, string md5)>
            {
                AugmentAndInstall(bioPGlobalFileName),
                AugmentAndInstall(bioPGlobalNCFileName)
            };
            File.WriteAllText(InstallInfoPath, JsonConvert.SerializeObject(new InstallInfo
            {
                InstallTime = DateTime.Now,
                Version = CURRENT_VERSION,
                SourceFiles = sourceFiles
            }));
            AutoTOCWPF.GenerateAllTOCs();

            (string, string) AugmentAndInstall(string fileName)
            {
                string existingFile = fileMap[fileName];
                using (IMEPackage pcc = MEPackageHandler.OpenMEPackage(existingFile))
                {
                    AugmentMapToLoadLiveEditor(pcc);
                    pcc.Save(Path.Combine(ModInstallPath, "CookedPCConsole", fileName));
                }
                return (existingFile, InteropHelper.CalculateMD5(existingFile));
            }
        }

        public static bool IsModInstalledAndUpToDate()
        {
            if (File.Exists(InstallInfoPath))
            {
                InstallInfo info = JsonConvert.DeserializeObject<InstallInfo>(File.ReadAllText(InstallInfoPath));
                if (info.Version == CURRENT_VERSION && info.SourceFiles != null)
                {
                    foreach ((string filePath, string md5) in info.SourceFiles)
                    {
                        if (!File.Exists(filePath) || InteropHelper.CalculateMD5(filePath) != md5)
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            return false;
        }

        public class InstallInfo
        {
            public DateTime InstallTime;
            public int Version;
            public List<(string filePath, string md5)> SourceFiles;
        }

        public static void UninstallDLC_MOD_Interop()
        {
            string installPath = Path.Combine(ME3Directory.DLCPath, modName);
            if (Directory.Exists(installPath))
            {
                Directory.Delete(installPath);
            }
        }
    }
}
