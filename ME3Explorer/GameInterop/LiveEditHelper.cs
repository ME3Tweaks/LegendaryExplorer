using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ME3Explorer.AutoTOC;
using ME3Explorer.Sequence_Editor;
using ME3ExplorerCore.GameFilesystem;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Packages.CloningImportingAndRelinking;
using ME3ExplorerCore.SharpDX;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json;

namespace ME3Explorer.GameInterop
{
    public static class LiveEditHelper
    {
        //INCREMENT THIS WHEN CHANGES ARE MADE THAT WOULD REQUIRE REGENERATION OF DLC_MOD_Interop
        public const int CURRENT_VERSION = 8;

        static string liveEditorFileName(MEGame game) => game switch
        {
            MEGame.ME3 => "ME3LiveEditor",
            MEGame.ME2 => "ME2LiveEditor",
            _ => throw new ArgumentOutOfRangeException(nameof(game), game, null)
        };

        public const string LoaderLoadedMessage = "BioP_Global";

        //me3 or me2 pcc to augment must be a map, and must have at least one BioTriggerStream and LevelStreamingKismet
        static void AugmentMapToLoadLiveEditor(IMEPackage pcc)
        {
            const string stateName = "SS_LIVEEDITOR";

            MEGame game = pcc.Game;

            var mainSequence = pcc.Exports.First(exp => exp.ObjectName == "Main_Sequence" && exp.ClassName == "Sequence");
            var bioWorldInfo = pcc.Exports.First(exp => exp.ClassName == "BioWorldInfo");

            #region Sequencing

            var consoleEvent = SequenceObjectCreator.CreateSequenceObject(pcc, "SeqEvent_Console", game);
            consoleEvent.WriteProperty(new NameProperty("LoadLiveEditor", "ConsoleEventName"));
            KismetHelper.AddObjectToSequence(consoleEvent, mainSequence);
            var setStreamingState = SequenceObjectCreator.CreateSequenceObject(pcc, "BioSeqAct_SetStreamingState", game);
            setStreamingState.WriteProperty(new NameProperty(stateName, "StateName"));
            setStreamingState.WriteProperty(new BoolProperty(true, "NewValue"));
            KismetHelper.AddObjectToSequence(setStreamingState, mainSequence);
            KismetHelper.CreateOutputLink(consoleEvent, "Out", setStreamingState);

            var levelLoaded = SequenceObjectCreator.CreateSequenceObject(pcc, "SeqEvent_LevelLoaded", game);
            KismetHelper.AddObjectToSequence(levelLoaded, mainSequence);
            var sendMessageToME3Exp = SequenceObjectCreator.CreateSequenceObject(pcc, "SeqAct_SendMessageToME3Explorer", game);
            KismetHelper.AddObjectToSequence(sendMessageToME3Exp, mainSequence);
            KismetHelper.CreateOutputLink(levelLoaded, game is MEGame.ME3 ? "Loaded and Visible" : "Out", sendMessageToME3Exp);
            var stringVar = SequenceObjectCreator.CreateSequenceObject(pcc, "SeqVar_String", game);
            stringVar.WriteProperty(new StrProperty(LoaderLoadedMessage, "StrValue"));
            KismetHelper.AddObjectToSequence(stringVar, mainSequence);
            KismetHelper.CreateVariableLink(sendMessageToME3Exp, "MessageName", stringVar);

            #endregion

            ExportEntry lsk = EntryCloner.CloneEntry(pcc.Exports.First(exp => exp.ClassName == "LevelStreamingKismet"));
            lsk.WriteProperty(new NameProperty(liveEditorFileName(game), "PackageName"));

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
                      new NameProperty(liveEditorFileName(game))
                  }
            }));
            bts.WriteProperty(streamingStates);

            pcc.AddToLevelActorsIfNotThere(bts);
        }

        static string modName(MEGame game) => game switch
        {
            MEGame.ME2 => "DLC_MOD_Interop2",
            MEGame.ME3 => "DLC_MOD_Interop",
            _ => throw new ArgumentOutOfRangeException(nameof(game), game, null)
        };
        public static string ModInstallPath(MEGame game) => Path.Combine(MEDirectories.GetDLCPath(game), modName(game));
        public static string InstallInfoPath(MEGame game) => Path.Combine(ModInstallPath(game), "InstallInfo.json");

        private const string camPathFileName = "ME3LiveEditorCamPath.pcc";
        public static string CamPathFilePath(MEGame game) => Path.Combine(ModInstallPath(game), "CookedPCConsole", camPathFileName);

        private const string consoleExtASIName = "ConsoleExtension-v1.0.asi";

        public static void InstallDLC_MOD_Interop(MEGame game)
        {
            if (Directory.Exists(ModInstallPath(game)))
            {
                FileSystemHelper.DeleteFilesAndFoldersRecursively(ModInstallPath(game));
            }
            Dictionary<string, string> fileMap = MELoadedFiles.GetFilesLoadedInGame(game, true);

            string sourcePath = Path.Combine(App.ExecFolder, modName(game));
            FileSystem.CopyDirectory(sourcePath, ModInstallPath(game));
            if (game == MEGame.ME3)
            {
                PadCamPathFile(game);
            }

            InteropHelper.InstallInteropASI(game);
            string consoleExtASIWritePath = Path.Combine(MEDirectories.GetExecutableFolderPath(game), "asi", consoleExtASIName);
            if (File.Exists(consoleExtASIWritePath))
            {
                File.Delete(consoleExtASIWritePath);
            }
            File.Copy(Path.Combine(App.ExecFolder, consoleExtASIName), consoleExtASIWritePath);

            const string bioPGlobalFileName = "BioP_Global.pcc";
            const string bioPGlobalNCFileName = "BioP_Global_NC.pcc";

            var sourceFiles = new List<(string filePath, string md5)>
            {
                AugmentAndInstall(bioPGlobalFileName)
            };
            if (game == MEGame.ME3)
            {
                sourceFiles.Add(AugmentAndInstall(bioPGlobalNCFileName));
            }

            File.WriteAllText(InstallInfoPath(game), JsonConvert.SerializeObject(new InstallInfo
            {
                InstallTime = DateTime.Now,
                Version = CURRENT_VERSION,
                SourceFiles = sourceFiles
            }));
            if (game == MEGame.ME3)
            {
                AutoTOCWPF.GenerateAllTOCs();
            }

            (string, string) AugmentAndInstall(string fileName)
            {
                string existingFile = fileMap[fileName];
                string destFile = Path.Combine(ModInstallPath(game), game is MEGame.ME3 ? "CookedPCConsole" : "CookedPC", fileName);
                File.Copy(existingFile, destFile, true);
                using (IMEPackage pcc = MEPackageHandler.OpenMEPackage(destFile))
                {
                    AugmentMapToLoadLiveEditor(pcc);
                    pcc.Save();
                }
                return (existingFile, InteropHelper.CalculateMD5(existingFile));
            }
        }

        public static bool IsModInstalledAndUpToDate(MEGame game)
        {
            if (File.Exists(InstallInfoPath(game)))
            {
                InstallInfo info = JsonConvert.DeserializeObject<InstallInfo>(File.ReadAllText(InstallInfoPath(game)));
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

        public static void UninstallDLC_MOD_Interop(MEGame game)
        {
            string installPath = Path.Combine(MEDirectories.GetDLCPath(game), modName(game));
            if (Directory.Exists(installPath))
            {
                Directory.Delete(installPath);
            }
        }

        private static string savedCamFilePath => Path.Combine(ME3Directory.ExecutableFolder, "savedCams");
        public static POV[] ReadSavedCamsFile()
        {
            var povs = new POV[10];

            if (File.Exists(savedCamFilePath))
            {
                using var fs = new FileStream(savedCamFilePath, FileMode.Open);

                for (int i = 0; i < 10; i++)
                {
                    povs[i] = new POV
                    {
                        Position = new Vector3(fs.ReadFloat(), fs.ReadFloat(), fs.ReadFloat()),
                        Rotation = new Vector3
                        {
                            Y = (fs.ReadInt32() % 65536).UnrealRotationUnitsToDegrees(),
                            Z = (fs.ReadInt32() % 65536).UnrealRotationUnitsToDegrees(),
                            X = (fs.ReadInt32() % 65536).UnrealRotationUnitsToDegrees()
                        },
                        FOV = fs.ReadFloat(),
                        Index = i
                    };
                }
            }
            else
            {
                for (int i = 0; i < 10; i++)
                {
                    povs[i] = new POV();
                }
            }

            return povs;
        }

        public static void CreateCurveFromSavedCams(ExportEntry export)
        {
            POV[] cams = ReadSavedCamsFile();

            var props = export.GetProperties();
            var posTrack = props.GetProp<StructProperty>("PosTrack").GetProp<ArrayProperty<StructProperty>>("Points");
            var rotTrack = props.GetProp<StructProperty>("EulerTrack").GetProp<ArrayProperty<StructProperty>>("Points");
            var lookupTrack = props.GetProp<StructProperty>("LookupTrack").GetProp<ArrayProperty<StructProperty>>("Points");

            posTrack.Clear();
            rotTrack.Clear();


            for (int i = 0; i < cams.Length; i++)
            {
                POV cam = cams[i];
                if (cam.IsZero)
                {
                    break;
                }

                posTrack.Add(new InterpCurvePoint<Vector3>
                {
                    InVal = i * 2,
                    OutVal = cam.Position,
                    InterpMode = EInterpCurveMode.CIM_CurveUser
                }.ToStructProperty(MEGame.ME3));
                rotTrack.Add(new InterpCurvePoint<Vector3>
                {
                    InVal = i * 2,
                    OutVal = cam.Rotation,
                    InterpMode = EInterpCurveMode.CIM_CurveUser
                }.ToStructProperty(MEGame.ME3));
                lookupTrack.Add(new StructProperty("InterpLookupPoint", false, new NameProperty("None", "GroupName"), new FloatProperty(0, "Time")));
            }
            export.WriteProperties(props);
        }

        public static void PadCamPathFile(MEGame game)
        {
            InteropHelper.TryPadFile(CamPathFilePath(game), 10_485_760);
        }
    }

    public class POV
    {
        public Vector3 Position;
        public Vector3 Rotation; // X = Roll, Y = Pitch, Z = Yaw (in degrees)
        public float FOV;

        public int Index { get; set; }
        public bool IsZero => Position.IsZero && Rotation.IsZero && FOV == 0f;

        public string Str => $"Position: {Position}, Rotation: Roll:{Rotation.X}, Pitch:{Rotation.Y}, Yaw:{Rotation.Z}, FOV: {FOV}";
    }
}
