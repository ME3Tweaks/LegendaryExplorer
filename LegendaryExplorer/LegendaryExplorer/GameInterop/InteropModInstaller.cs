using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using LegendaryExplorer.GameInterop.InteropTargets;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.Sequence_Editor;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json;

namespace LegendaryExplorer.GameInterop
{
    /// <summary>
    /// Installation handler for Interop DLC Mod. Can be subclassed to modify installation behavior
    /// </summary>
    public class InteropModInstaller
    {
        protected readonly InteropTarget Target;
        private MEGame Game => Target.Game;

        protected bool CancelInstallation;

        private string InstallInfoPath => Path.Combine(ModInstallPath, "InstallInfo.json");
        protected string ModInstallPath => Path.Combine(MEDirectories.GetDLCPath(Game), Target.ModInfo.InteropModName);

        public InteropModInstaller(InteropTarget target)
        {
            if (target.ModInfo is null)
            {
                throw new ArgumentException(@"Interop Target must have ModInfo to install Interop Mod", nameof(target));
            }
            Target = target;
        }

        public void InstallDLC_MOD_Interop()
        {
            DeleteExistingInstallation();

            Dictionary<string, string> fileMap = MELoadedFiles.GetFilesLoadedInGame(Game, true);

            var modSourcePath = Path.Combine(AppDirectories.ExecFolder, Target.ModInfo.InteropModName);
            FileSystem.CopyDirectory(modSourcePath, ModInstallPath);

            if (Target.ModInfo.CanUseCamPath)
            {
                LiveEditHelper.PadCamPathFile(Game);
            }

            var augmentedFiles = AugmentAndInstall(GetFilesToAugment(), fileMap);

            if (CancelInstallation)
            {
                DeleteExistingInstallation();
                CancelInstallation = false;
                return;
            }

            File.WriteAllText(InstallInfoPath, JsonConvert.SerializeObject(new InstallInfo
            {
                InstallTime = DateTime.Now,
                Version = Target.ModInfo.Version,
                SourceFiles = augmentedFiles.ToList()
            }));

            if (Game is not MEGame.ME1 or MEGame.ME2)
            {
                TOCCreator.CreateTOCForGame(Game);
            }
        }

        private void DeleteExistingInstallation()
        {
            if (Directory.Exists(ModInstallPath))
            {
                DeleteFilesAndFoldersRecursively(ModInstallPath);
            }
        }

        protected virtual IEnumerable<string> GetFilesToAugment()
        {
            const string bioPGlobalFileName = "BioP_Global.pcc";
            const string bioPGlobalNcFileName = "BioP_Global_NC.pcc";

            var filesToAugment = new List<string> {bioPGlobalFileName};
            if (Game.IsGame3()) filesToAugment.Add(bioPGlobalNcFileName);
            return filesToAugment;
        }

        private IEnumerable<(string filePath, string md5)> AugmentAndInstall(IEnumerable<string> filesToAugment, Dictionary<string, string> fileMap)
        {
            var md5Hashes = new List<(string filePath, string md5)>();
            foreach (var fileName in filesToAugment)
            {
                string existingFile = fileMap[fileName];
                string destFile = Path.Combine(ModInstallPath, Game.CookedDirName(), fileName);
                File.Copy(existingFile, destFile, true);
                using IMEPackage pcc = MEPackageHandler.OpenMEPackage(destFile);
                AugmentMapToLoadLiveEditor(pcc);
                pcc.Save();
                md5Hashes.Add((existingFile, InteropHelper.CalculateMD5(existingFile)));
            }
            return md5Hashes;
        }

        protected virtual void AugmentMapToLoadLiveEditor(IMEPackage pcc)
        {
            // Increment target version when making changes that would invalidate DLC_MOD_Interop
            // ME2/ME3 map to augment must have at least one LevelStreamingKismet and BioTriggerStream
            const string stateName = "SS_LIVEEDITOR";

            MEGame game = pcc.Game;

            var mainSequence = pcc.Exports.First(exp => exp.ObjectName == "Main_Sequence" && exp.ClassName == "Sequence");
            var bioWorldInfo = pcc.Exports.First(exp => exp.ClassName == "BioWorldInfo");

            #region Sequencing

            var consoleEvent = LEXSequenceObjectCreator.CreateSequenceObject(pcc, "SeqEvent_Console");
            consoleEvent.WriteProperty(new NameProperty("LoadLiveEditor", "ConsoleEventName"));
            KismetHelper.AddObjectToSequence(consoleEvent, mainSequence);
            var setStreamingState = LEXSequenceObjectCreator.CreateSequenceObject(pcc, "BioSeqAct_SetStreamingState");
            setStreamingState.WriteProperty(new NameProperty(stateName, "StateName"));
            setStreamingState.WriteProperty(new BoolProperty(true, "NewValue"));
            KismetHelper.AddObjectToSequence(setStreamingState, mainSequence);
            KismetHelper.CreateOutputLink(consoleEvent, "Out", setStreamingState);

            var levelLoaded = LEXSequenceObjectCreator.CreateSequenceObject(pcc, "SeqEvent_LevelLoaded");
            KismetHelper.AddObjectToSequence(levelLoaded, mainSequence);

            var sendMessageClassName = game.IsLEGame() ? "SeqAct_SendMessageToLEX" : "SeqAct_SendMessageToME3Explorer";
            var sendMessageToME3Exp = LEXSequenceObjectCreator.CreateSequenceObject(pcc, sendMessageClassName);
            KismetHelper.AddObjectToSequence(sendMessageToME3Exp, mainSequence);
            KismetHelper.CreateOutputLink(levelLoaded, game is MEGame.ME2 ? "Out" : "Loaded and Visible", sendMessageToME3Exp);
            var stringVar = LEXSequenceObjectCreator.CreateSequenceObject(pcc, "SeqVar_String");
            stringVar.WriteProperty(new StrProperty(LiveEditHelper.LoaderLoadedMessage, "StrValue"));
            KismetHelper.AddObjectToSequence(stringVar, mainSequence);
            KismetHelper.CreateVariableLink(sendMessageToME3Exp, "MessageName", stringVar);

            #endregion

            ExportEntry lsk = EntryCloner.CloneEntry(pcc.Exports.First(exp => exp.ClassName == "LevelStreamingKismet"));
            lsk.WriteProperty(new NameProperty(Target.ModInfo.LiveEditorFilename, "PackageName"));

            var streamingLevels = bioWorldInfo.GetProperty<ArrayProperty<ObjectProperty>>("StreamingLevels");
            streamingLevels.Add(new ObjectProperty(lsk));
            bioWorldInfo.WriteProperty(streamingLevels);

            ExportEntry bts = EntryCloner.CloneTree(pcc.Exports.First(exp => exp.ClassName == "BioTriggerStream"), incrementIndex: true);
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
                    new NameProperty(Target.ModInfo.LiveEditorFilename)
                }
            }));
            bts.WriteProperty(streamingStates);

            pcc.AddToLevelActorsIfNotThere(bts);
        }

        public static bool IsModInstalledAndUpToDate(InteropTarget target)
        {
            var installInfoPath = Path.Combine(MEDirectories.GetDLCPath(target.Game), target.ModInfo.InteropModName, "InstallInfo.json");
            if (File.Exists(installInfoPath))
            {
                InstallInfo info = JsonConvert.DeserializeObject<InstallInfo>(File.ReadAllText(installInfoPath));
                if (info is not null && info.Version == target.ModInfo.Version && info.SourceFiles != null)
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

        public static bool DeleteFilesAndFoldersRecursively(string targetDirectory)
        {
            if (!Directory.Exists(targetDirectory))
            {
                Debug.WriteLine("Directory to delete doesn't exist: " + targetDirectory);
                return true;
            }
            bool result = true;
            foreach (string file in Directory.GetFiles(targetDirectory))
            {
                File.SetAttributes(file, FileAttributes.Normal); //remove read only
                try
                {
                    //Debug.WriteLine("Deleting file: " + file);
                    File.Delete(file);
                }
                catch
                {
                    return false;
                }
            }

            foreach (string subDir in Directory.GetDirectories(targetDirectory))
            {
                result &= DeleteFilesAndFoldersRecursively(subDir);
            }

            Thread.Sleep(10); // This makes the difference between whether it works or not. Sleep(0) is not enough.
            try
            {
                //Debug.WriteLine("Deleting directory: " + targetDirectory);

                Directory.Delete(targetDirectory);
            }
            catch
            {
                return false;
            }
            return result;
        }

        public class InstallInfo
        {
            public DateTime InstallTime;
            public int Version;
            public List<(string filePath, string md5)> SourceFiles;
        }
    }
}