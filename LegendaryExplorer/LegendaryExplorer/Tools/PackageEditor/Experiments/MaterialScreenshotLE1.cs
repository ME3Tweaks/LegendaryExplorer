using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorer.GameInterop;
using LegendaryExplorer.Libraries;
using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.Tools.PathfindingEditor;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tools.PackageEditor.Experiments
{
    /// <summary>
    /// Controller class for the material screenshot/video feature for LE1
    /// This can and probably will be ported to t
    /// </summary>
    class MaterialScreenshotLE1
    {
        private Rectangle GetWindowCoordinates()
        {
            string className = "MassEffect1";
            string windowName = "Mass Effect";

            Rectangle rect;
            IntPtr hwnd = WindowsAPI.FindWindow(className, windowName);
            WindowsAPI.GetWindowRect(hwnd, out rect);
            return rect;
        }


        private int currentActorIndex = 0; // The Actor (0 based) to prep
        private List<string> completedMaterials = new List<string>();
        private int currentMaterialIndex = 164; // The cursor pointer
        private List<MaterialRecord> materialAssetList;
        private List<FileNameDirKeyPair> fileList;
        private Dictionary<string, string> loadedFiles = MELoadedFiles.GetFilesLoadedInGame(MEGame.LE1, true); // for lookup
        private PackageCache globalCache = new PackageCache();
        private string materialName;
        public MaterialScreenshotLE1()
        {
            GameController.GetInteropTargetForGame(MEGame.LE1).GameReceiveMessage += ReceivedLE1Message; // probably will leak, needs unset somehow
        }

        private void ReceivedLE1Message(string message)
        {
            Debug.WriteLine($"Received message: {message}");
            var messageCommands = message.Split();
            switch (messageCommands[0])
            {
                case "MS_ORBITFINISHED": // Orbit of object has finished
                    SignalOrbitFinished();
                    break;
                case "MS_LEVELLOADED": // The map is ready to begin capture - a slight delay has occurred to allow mips to stream in
                    SignalLevelLoaded();
                    break;
                case "MS_LEVELUNLOADED": // The map has unloaded and LEX can prepare the next file
                    PrepareNextFile();
                    break;
                case "MS_CAPTUREREADY":
                    BeginCapture();
                    break;
                default:
                    throw new Exception($"UNKNOWN COMMAND: {messageCommands[0]}");
            }
        }

        private void PrepareNextFile()
        {
            if (currentMaterialIndex >= materialAssetList.Count)
                return; // We have finished

            currentActorIndex = 0;
            using var templatePackage = MEPackageHandler.OpenMEPackage(Path.Combine(MEDirectories.GetDLCPath(MEGame.LE1), "DLC_MOD_MaterialScreenshot", "CookedPCConsole", "BIOA_MatScreenshot_DSG_Template.pcc"));
            var donorMatRec = materialAssetList[currentMaterialIndex];

            pe.BusyText = $"Preparing {donorMatRec.MaterialName}";

            using var sourcePackage = MEPackageHandler.OpenMEPackage(loadedFiles[fileList[donorMatRec.Usages[0].FileKey].FileName]);
            var sourceMat = sourcePackage.GetUExport(donorMatRec.Usages[0].UIndex);

            // Check to make sure this isn't a duplicate (sometimes can occur if stored in a top level)
            if (completedMaterials.Contains(sourceMat.InstancedFullPath))
            {
                currentMaterialIndex++;
                PrepareNextFile();
            }

            completedMaterials.Add(sourceMat.InstancedFullPath); // Make sure we don't do this again

            materialName = sourceMat.InstancedFullPath;
            Debug.WriteLine($"Preparing {materialName}, index {currentMaterialIndex}");


            // Port in the donor material.
            EntryExporter.ExportExportToPackage(sourceMat, templatePackage, out var newMatEntry, globalCache);
            if (newMatEntry is ExportEntry exp)
            {
                exp.WriteProperty(new BoolProperty(true, "bUsedWithSkeletalMesh")); // This might break shit
            }

            if (newMatEntry is ExportEntry entry)
            {
                var matBin = ObjectBinary.From<Material>(entry);
                // Check for self referencing. This is due to bad naming
                if (matBin.SM3MaterialResource?.Uniform2DTextureExpressions != null)
                {
                    foreach (var tex in matBin.SM3MaterialResource.Uniform2DTextureExpressions)
                    {
                        if (tex.TextureIndex == newMatEntry.UIndex)
                        {
                            // SKIP
                            currentMaterialIndex++;
                            PrepareNextFile();
                            return; // Do not 
                        }
                    }
                }
            }

            // Hook up the actors to use this material

            // ACTOR 1 (BioPawn)
            var actor1Mat = templatePackage.FindExport(@"biog_hmf_arm_cth_r.CTHc.HMF_ARM_CTHc_MDL");
            var actor1Bin = ObjectBinary.From<SkeletalMesh>(actor1Mat);
            for (int i = 0; i < actor1Bin.Materials.Length; i++)
            {
                actor1Bin.Materials[i] = newMatEntry.UIndex;
            }
            actor1Mat.WriteBinary(actor1Bin);

            // ACTOR 2 (Static Mesh)
            var actor2Mat = templatePackage.FindExport(@"TheWorld.PersistentLevel.StaticMeshActor_322.StaticMeshComponent_26");
            var materials2 = actor2Mat.GetProperty<ArrayProperty<ObjectProperty>>("Materials");
            materials2[0].Value = newMatEntry.UIndex;
            actor2Mat.WriteProperty(materials2);

            // ACTOR 3 (InterpActor)
            var actor3Mat = templatePackage.FindExport(@"TheWorld.PersistentLevel.InterpActor_8.StaticMeshComponent_535");
            var materials3 = actor3Mat.GetProperty<ArrayProperty<ObjectProperty>>("Materials");
            materials3[0].Value = newMatEntry.UIndex;
            actor3Mat.WriteProperty(materials3);

            var outpath = Path.Combine(MEDirectories.GetDLCPath(MEGame.LE1), "DLC_MOD_MaterialScreenshot", "CookedPCConsole", "BIOA_MatScreenshot_DSG.pcc");
            templatePackage.Save(outpath);

            currentMaterialIndex++;

            // Signal that the package is ready
            TriggerGameEvent("LoadNewLevel");
        }

        /// <summary>
        /// The level has loaded in and is ready to begin preparing actors
        /// </summary>
        private void SignalLevelLoaded()
        {
            PrepareNextActor();
        }

        private void BeginCapture()
        {
            Debug.WriteLine("Beginning capture!");

            // Todo: Someday finish this and change to CLIWrap
            //var gameWindowPosition = GetWindowCoordinates();
            //var ffmpegPath = @"X:\Downloads\ffmpeg-4.4-full_build\bin\ffmpeg.exe";
            //var arguments = $"-f gdigrab -y -framerate 30 -i desktop -codec:v libx264 -pix_fmt yuv420p -t 8 B:\\MaterialVideosLE1\\{materialName}-{currentActorIndex}.mp4";

            //ConsoleApp ca = new ConsoleApp(ffmpegPath, arguments);
            //ca.Run();
            //ca.ConsoleOutput += (sender, args) =>
            //{
            //    Debug.WriteLine(args.Line);
            //};
            //Process.Start(ffmpegPath, arguments);
            TriggerGameEvent("StartOrbitCameraC"); //calls MS_ORBITFINISHED when complete
        }

        private void SignalOrbitFinished()
        {
            if (currentActorIndex < 3) // Up to 3 actors
            {
                PrepareNextActor();
            }
            else
            {
                TriggerGameEvent("UnloadLevel"); // calls MS_LEVELUNLOADED when done
            }
        }

        private void PrepareNextActor()
        {
            // Swaps the shown actors and forces a camera near it to force texture streaming
            // Calls MS_CAPTUREREADY when done
            Debug.WriteLine($"Preparing actor {currentActorIndex + 1}");
            TriggerGameEvent($"PrepActor{currentActorIndex + 1}");
            currentActorIndex++;
        }

        private NamedPipeClientStream client;
        private StreamWriter pipeWriter;
        private PackageEditorWindow pe;

        /// <summary>
        /// Issues the CAUSEEVENT command to the game with the specified event name. It will run on the next tick.
        /// </summary>
        /// <param name="eventName"></param>
        private void TriggerGameEvent(string eventName)
        {
            Task.Run(() =>
            {
                client = new NamedPipeClientStream("LEX_LE1_COMM_PIPE");
                client.Connect();
                pipeWriter = new StreamWriter(client);
                pipeWriter.WriteLine("CAUSEEVENT " + eventName);
                pipeWriter.Flush();
                client.Dispose();
            });
        }

        public void StartWorkflow(PackageEditorWindow pe)
        {
            // Load material lists
            var CurrentDBPath = AssetDatabaseWindow.GetDBPath(MEGame.LE1);

            if (CurrentDBPath != null && File.Exists(CurrentDBPath))
            {
                pe.BusyText = "Loading asset database";
                pe.IsBusy = true;
                var db = new AssetDB();
                AssetDatabaseWindow.LoadDatabase(CurrentDBPath, MEGame.LE1, db, CancellationToken.None).ContinueWithOnUIThread(prevTask =>
                {
                    // DATABASE LOADED
                    materialAssetList = db.Materials;
                    fileList = db.FileList;
                    PrepareNextFile();
                });
            }

            this.pe = pe;
        }
    }
}
