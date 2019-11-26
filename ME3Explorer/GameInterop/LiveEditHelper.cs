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
        public const int CURRENT_VERSION = 3;

        const string liveEditorFileName = "ME3LiveEditor";

        public const string LoaderLoadedMessage = "BioP_Global";

        //me3 pcc must be a map, and must have at least one BioTriggerStream and LevelStreamingKismet
        static void AugmentMapToLoadLiveEditor(IMEPackage pcc)
        {
            const string stateName = "SS_LIVEEDITOR";

            var mainSequence = pcc.Exports.First(exp => exp.ObjectName == "Main_Sequence" && exp.ClassName == "Sequence");
            var bioWorldInfo = pcc.Exports.First(exp => exp.ClassName == "BioWorldInfo");

            #region Sequencing

            var consoleEvent = SequenceObjectCreator.CreateSequenceObject(pcc, "SeqEvent_Console", MEGame.ME3);
            consoleEvent.WriteProperty(new NameProperty("LoadLiveEditor", "ConsoleEventName"));
            KismetHelper.AddObjectToSequence(consoleEvent, mainSequence);
            var setStreamingState = SequenceObjectCreator.CreateSequenceObject(pcc, "BioSeqAct_SetStreamingState", MEGame.ME3);
            setStreamingState.WriteProperty(new NameProperty(stateName, "StateName"));
            setStreamingState.WriteProperty(new BoolProperty(true, "NewValue"));
            KismetHelper.AddObjectToSequence(setStreamingState, mainSequence);
            KismetHelper.CreateOutputLink(consoleEvent, "Out", setStreamingState);

            var levelLoaded = SequenceObjectCreator.CreateSequenceObject(pcc, "SeqEvent_LevelLoaded", MEGame.ME3);
            KismetHelper.AddObjectToSequence(levelLoaded, mainSequence);
            var sendMessageToME3Exp = SequenceObjectCreator.CreateSequenceObject(pcc, "SeqAct_SendMessageToME3Explorer", MEGame.ME3);
            KismetHelper.AddObjectToSequence(sendMessageToME3Exp, mainSequence);
            KismetHelper.CreateOutputLink(levelLoaded, "Loaded and Visible", sendMessageToME3Exp);
            var stringVar = SequenceObjectCreator.CreateSequenceObject(pcc, "SeqVar_String", MEGame.ME3);
            stringVar.WriteProperty(new StrProperty(LoaderLoadedMessage, "StrValue"));
            KismetHelper.AddObjectToSequence(stringVar, mainSequence);
            KismetHelper.CreateVariableLink(sendMessageToME3Exp, "MessageName", stringVar);

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
                FileSystemHelper.DeleteFilesAndFoldersRecursively(ModInstallPath);
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
