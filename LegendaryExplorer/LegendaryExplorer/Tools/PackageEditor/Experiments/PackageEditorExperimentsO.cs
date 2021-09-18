using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Matinee;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.TLK.ME2ME3;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using HuffmanCompression = LegendaryExplorerCore.TLK.ME1.HuffmanCompression;

namespace LegendaryExplorer.Tools.PackageEditor.Experiments
{
    /// <summary>
    /// Class for 'Others' package experiments who aren't main devs
    /// </summary>
    class PackageEditorExperimentsO
    {
        public static void DumpPackageToT3D(IMEPackage package)
        {
            var levelExport = package.Exports.FirstOrDefault(x => x.ObjectName == "Level" && x.ClassName == "PersistentLevel");
            if (levelExport != null)
            {
                var level = ObjectBinary.From<Level>(levelExport);
                foreach (var actoruindex in level.Actors)
                {
                    if (package.TryGetUExport(actoruindex.value, out var actorExport))
                    {
                        switch (actorExport.ClassName)
                        {
                            case "StaticMesh":
                                var sm = ObjectBinary.From<StaticMesh>(actorExport);
                                
                                // Look at vars in sm to find what you need
                                //ExportT3D(sm, "FILENAMEHERE.txt", null); //??
                                break;
                            case "StaticMeshCollectionActor":

                                break;
                        }

                    }
                }
            }
        }

        // Might already be in ME3EXP?
        //A function for converting radians to unreal rotation units (necessary for UDK)
        private static float RadianToUnrealDegrees(float Angle)
        {
            return Angle * (32768 / 3.1415f);
        }

        // Might already be in ME3EXP?
        //A function for converting radians to degrees
        private static float RadianToDegrees(float Angle)
        {
            return Angle * (180 / 3.1415f);
        }

        public static void ExportT3D(StaticMesh staticMesh, string Filename, Matrix4x4 m, Vector3 IncScale3D)
        {
            StreamWriter Writer = new StreamWriter(Filename, true);

            Vector3 Rotator = new Vector3((float)Math.Atan2(m.M32, m.M33), (float)Math.Asin(-1 * m.M31), (float)Math.Atan2(-1 * m.M21, m.M11));
            float RotatorX = Rotator.X;
            RotatorX = RadianToDegrees(RotatorX);
            float RotatorY = Rotator.Y;
            RotatorY = RadianToDegrees(RotatorY);
            float RotatorZ = Rotator.Z;
            RotatorZ = RadianToDegrees(RotatorZ);

            Vector3 Location = new Vector3(m.M41, m.M42, m.M43);

            //Only rotation, location, scale, actor name and model name are needed for a level recreation, everything else is just a placeholder
            //Need to override ToString to use US CultureInfo to avoid "commas instead of dots" bug
            //Indexes here is just to make names unique
            if (staticMesh != null)
            {
                Writer.WriteLine($"Begin Actor Class=StaticMeshActor Name={staticMesh.Export.ObjectName} Archetype=StaticMeshActor'/Script/Engine.Default__StaticMeshActor'");
                Writer.WriteLine("        Begin Object Class=StaticMeshComponent Name=\"StaticMeshComponent0\" Archetype=StaticMeshComponent'/Script/Engine.Default__StaticMeshActor:StaticMeshComponent0'");
                Writer.WriteLine("        End Object");
                Writer.WriteLine("        Begin Object Name=\"StaticMeshComponent0\"");
                Writer.WriteLine("            StaticMesh=StaticMesh'/Game/ME3/ME3Architecture/Static/" + staticMesh.Export.ObjectName + "." + staticMesh.Export.ObjectName + "'"); //oriignal code was duplicated
                Writer.WriteLine("            RelativeLocation=(X=" + Location.X.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Y=" + Location.Y.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Z=" + Location.Z.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")));
                Writer.WriteLine("            RelativeRotation=(Pitch=" + RotatorY.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Yaw=" + RotatorZ.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Roll=" + RotatorX.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + ")");
                Writer.WriteLine("            RelativeScale3D=(X=" + IncScale3D.X.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Y=" + IncScale3D.Y.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Z=" + IncScale3D.Z.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + ")");
                Writer.WriteLine("        End Object");
                Writer.WriteLine("        StaticMeshComponent=StaticMeshComponent0");
                Writer.WriteLine($"        ActorLabel=\"{staticMesh.Export.ObjectName}\"");
                Writer.WriteLine("End Actor");
            }
            Writer.Close();
        }


        //UDK version, need to figure out how to apply rotation properly
        public static void ExportT3D_UDK(StaticMesh STM, string Filename, Matrix4x4 m, Vector3 IncScale3D)
        {
            StreamWriter Writer = new StreamWriter(Filename, true);

            Vector3 Rotator = new Vector3((float)Math.Atan2(m.M32, m.M33), (float)Math.Asin(-1 * m.M31), (float)Math.Atan2(-1 * m.M21, m.M11));
            float RotatorX = Rotator.X;
            RotatorX = RadianToUnrealDegrees(RotatorX);
            float RotatorY = Rotator.Y;
            RotatorY = RadianToUnrealDegrees(RotatorY);
            float RotatorZ = Rotator.Z;
            RotatorZ = RadianToUnrealDegrees(RotatorZ);

            Vector3 Location = new Vector3(m.M41, m.M42, m.M43);

            if (STM != null)
            {
                Writer.WriteLine("      Begin Actor Class=StaticMeshActor Name=STMC_" + STM.Export.ObjectName.Number + " Archetype=StaticMeshActor'Engine.Default__StaticMeshActor'");
                Writer.WriteLine("          Begin Object Class=StaticMeshComponent Name=STMC_" + STM.Export.ObjectName.Number + " ObjName=" + STM.Export.ObjectName.Instanced + " Archetype=StaticMeshComponent'Engine.Default__StaticMeshActor:StaticMeshComponent0'");
                Writer.WriteLine("              StaticMesh=StaticMesh'A_Cathedral.Static." + STM.Export.ObjectName + "'");
                Writer.WriteLine("              LODData(0)=");
                Writer.WriteLine("              VertexPositionVersionNumber=1");
                Writer.WriteLine("              ReplacementPrimitive=None");
                Writer.WriteLine("              bAllowApproximateOcclusion=True");
                Writer.WriteLine("              bForceDirectLightMap=True");
                Writer.WriteLine("              bUsePrecomputedShadows=True");
                Writer.WriteLine("              LightingChannels=(bInitialized=True,Static=True)");
                Writer.WriteLine("              Name=\"" + STM.Export.ObjectName + "_" + STM.Export.ObjectName.Number + "\"");
                Writer.WriteLine("              ObjectArchetype=StaticMeshComponent'Engine.Default__StaticMeshActor:StaticMeshComponent0'");
                Writer.WriteLine("          End Object");
                Writer.WriteLine("          StaticMeshComponent=StaticMeshComponent'" + STM.Export.ObjectName.Instanced + "'");
                Writer.WriteLine("          Components(0)=StaticMeshComponent'" + STM.Export.ObjectName.Instanced + "'");
                Writer.WriteLine("          Location=(X=" + Location.X.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Y=" + Location.Y.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Z=" + Location.Z.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")));
                Writer.WriteLine("          Rotation=(Pitch=" + RotatorY.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                "Yaw=" + RotatorZ.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Roll=" + RotatorX.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + ")");
                Writer.WriteLine("          DrawScale=(X=" + IncScale3D.X.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Y=" + IncScale3D.Y.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Z=" + IncScale3D.Z.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + ")");
                Writer.WriteLine("          CreationTime=1.462282");
                Writer.WriteLine("          Tag=\"StaticMeshActor\"");
                Writer.WriteLine("          CollisionComponent=StaticMeshComponent'" + STM.Export.ObjectName + "'");
                Writer.WriteLine("          Name=\"STMC_" + STM.Export.ObjectName.Number.ToString("D") + "\"");
                Writer.WriteLine("          ObjectArchetype=StaticMeshActor'Engine.Default__StaticMeshActor'");
                Writer.WriteLine("      End Actor");
            }
            Writer.Close();
        }



        //an attempt to recreate the assembling process in MaxScript similar to unreal t3d
        //Rotation is buggy, doesn't properly for now
        public static void ExportT3D_MS(StaticMesh STM, string Filename, Matrix4x4 m, Vector3 IncScale3D)
        {
            StreamWriter Writer = new StreamWriter(Filename, true);

            Vector3 Rotator = new Vector3((float)Math.Atan2(m.M32, m.M33), (float)Math.Asin(-1 * m.M31), (float)Math.Atan2(-1 * m.M21, m.M11));
            float RotatorX = Rotator.X;
            RotatorX = RadianToDegrees(RotatorX);
            float RotatorY = Rotator.Y;
            RotatorY = RadianToDegrees(RotatorY);
            float RotatorZ = Rotator.Z;
            RotatorZ = RadianToDegrees(RotatorZ);

            Vector3 Location = new Vector3(m.M41, m.M42, m.M43);

            if (STM != null)
            {
                Writer.WriteLine($"{STM.Export.ObjectName} = instance ${STM.Export.ObjectName}");
                Writer.WriteLine($"{STM.Export.ObjectName}.name = \"{STM.Export.ObjectName}\" --name the copy as \"{STM.Export.ObjectName}\"");
                Writer.WriteLine("$" + STM.Export.ObjectName + ".Position=[" + Location.X.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                    ", " + Location.Y.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                    ", " + Location.Z.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "]");
                Writer.WriteLine("$" + STM.Export.ObjectName + ".scale=[" + IncScale3D.X.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                    ", " + IncScale3D.Y.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                    ", " + IncScale3D.Z.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "]");
                Writer.WriteLine("--Setting the rotation");
                Writer.WriteLine("fn SetObjectRotation obj rx ry rz =");
                Writer.WriteLine("(");
                Writer.WriteLine("-- Reset the object's transformation matrix so that");
                Writer.WriteLine("-- it only includes position and scale information.");
                Writer.WriteLine("-- Doing this clears out any previous object rotation.");
                Writer.WriteLine("local translateMat = transMatrix obj.transform.pos");
                Writer.WriteLine("local scaleMat = scaleMatrix obj.transform.scale");
                Writer.WriteLine("obj.transform = scaleMat * translateMat");
                Writer.WriteLine("-- Perform each axis rotation individually");
                Writer.WriteLine("rotate obj (angleaxis rx [1,0,0])");
                Writer.WriteLine("rotate obj (angleaxis ry [0,1,0])");
                Writer.WriteLine("rotate obj (angleaxis rz [0,0,1])");
                Writer.WriteLine(")");
                Writer.WriteLine("-- Set currently selected Object's rotation to " + RotatorX.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                    " " + RotatorY.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                    " " + RotatorZ.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")));
                Writer.WriteLine("SetObjectRotation $" + STM.Export.ObjectName +
                    " " + RotatorX.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                    " " + RotatorY.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                    " " + RotatorZ.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")));
                Writer.WriteLine("-------------------------------------------------------");
                Writer.WriteLine("-------------------------------------------------------");
            }
            Writer.Close();
        }

        /// <summary>
        /// Collects all TLK exports from the entire ME1 game and exports them into a single GlobalTLK file
        /// </summary>
        /// <param name="pew">Instance of Package Editor</param>
        public static void BuildME1SuperTLKFile (PackageEditorWindow pew)
        {
            string myBasePath = ME1Directory.DefaultGamePath;
            string searchDir = ME1Directory.CookedPCPath;

            CommonOpenFileDialog d = new CommonOpenFileDialog { Title = "Select folder to search", IsFolderPicker = true, InitialDirectory = myBasePath };
            if (d.ShowDialog() == CommonFileDialogResult.Ok)
            {
                searchDir = d.FileName;
            }

            Microsoft.Win32.OpenFileDialog outputFileDialog = new () { 
                Title = "Select GlobalTlk file to output to (GlobalTlk exports will be completely overwritten)", 
                Filter = "*.upk|*.upk" };
            bool? result = outputFileDialog.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                Debug.WriteLine("No output file specified");
                return;
            }
            string outputFilePath = outputFileDialog.FileName;

            string[] extensions = { ".u", ".upk" };

            pew.IsBusy = true;

            var tlkLines = new SortedDictionary<int, string>();
            var tlkLines_m = new SortedDictionary<int, string>();

            Task.Run(() =>
            {
                FileInfo[] files = new DirectoryInfo(searchDir)
                    .EnumerateFiles("*", SearchOption.AllDirectories)
                    .Where(f => extensions.Contains(f.Extension.ToLower()))
                    .ToArray();
                int i = 1;
                foreach (FileInfo f in files)
                {
                    pew.BusyText = $"[{i}/{files.Length}] Scanning Packages for TLK Exports";
                    int basePathLen = myBasePath.Length;
                    using (IMEPackage pack = MEPackageHandler.OpenMEPackage(f.FullName))
                    {
                        List<ExportEntry> tlkExports = pack.Exports.Where(x =>
                            (x.ObjectName == "tlk" || x.ObjectName == "tlk_M" || x.ObjectName == "GlobalTlk_tlk" || x.ObjectName == "GlobalTlk_tlk_M") && x.ClassName == "BioTlkFile").ToList();
                        if (tlkExports.Count > 0)
                        {
                            string subPath = f.FullName.Substring(basePathLen);
                            foreach (ExportEntry exp in tlkExports)
                            {
                                var stringMapping = ((exp.ObjectName == "tlk" || exp.ObjectName == "GlobalTlk_tlk") ? tlkLines : tlkLines_m);
                                var talkFile = new ME1TalkFile(exp);
                                foreach (var sref in talkFile.StringRefs)
                                {
                                    if (sref.StringID == 0) continue; //skip blank
                                    if (sref.Data == null || sref.Data == "-1" || sref.Data == "") continue; //skip blank

                                    if (!stringMapping.TryGetValue(sref.StringID, out var dictEntry))
                                    {
                                        stringMapping[sref.StringID] = sref.Data;
                                    }

                                }
                            }
                        }

                        i++;
                    }
                }

                int total = tlkLines.Count;

                using (IMEPackage o = MEPackageHandler.OpenMEPackage(outputFilePath))
                {
                    List<ExportEntry> tlkExports = o.Exports.Where(x =>
                            (x.ObjectName == "GlobalTlk_tlk" || x.ObjectName == "GlobalTlk_tlk_M") && x.ClassName == "BioTlkFile").ToList();
                    if (tlkExports.Count > 0)
                    {
                        foreach (ExportEntry exp in tlkExports)
                        {
                            var stringMapping = (exp.ObjectName == "GlobalTlk_tlk" ? tlkLines : tlkLines_m);
                            var talkFile = new ME1TalkFile(exp);
                            var LoadedStrings = new List<ME1TalkFile.TLKStringRef>();
                            foreach (var tlkString in stringMapping)
                            {
                                // Do the important part
                                LoadedStrings.Add(new ME1TalkFile.TLKStringRef(tlkString.Key, 1, tlkString.Value));
                            }

                            HuffmanCompression huff = new HuffmanCompression();
                            huff.LoadInputData(LoadedStrings);
                            huff.serializeTalkfileToExport(exp);
                        }
                    }
                    o.Save();

                }

                return total;

            }).ContinueWithOnUIThread((total) =>
            {
                pew.IsBusy = false;
                pew.StatusBar_LeftMostText.Text = $"Wrote {total} lines to {outputFilePath}";
            });

        }

        public static void AssociateAllExtensions()
        {
            FileAssociations.AssociatePCCSFM();
            FileAssociations.AssociateUPKUDK();
            FileAssociations.AssociateOthers();
        }

        public static void CreateAudioSizeInfo(PackageEditorWindow pew, MEGame game = MEGame.ME3)
        {
            pew.IsBusy = true;
            pew.BusyText = $"Creating audio size info for {game}";

            CaseInsensitiveDictionary<long> audioSizes = new();

            Task.Run(() =>
            {
                foreach (string filePath in MELoadedFiles.GetOfficialFiles(game, includeAFCs:true).Where(f => f.EndsWith(".afc", StringComparison.OrdinalIgnoreCase)))
                {
                    var info = new FileInfo(filePath);
                    audioSizes.Add(info.Name.Split('.')[0], info.Length);
                }
            }).ContinueWithOnUIThread((prevTask) =>
            {
                pew.IsBusy = false;

                var outFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    $"{game}-vanillaaudiosizes.json");
                File.WriteAllText(outFile, JsonConvert.SerializeObject(audioSizes));

            });
        }

        [DllImport(@"C:\Program Files (x86)\Audiokinetic\Wwise 2019.1.6.7110\SDK\x64_vc140\Release\bin\AkSoundEngineDLL.dll")]
        public static extern uint GetIDFromString(string str);

        public static void GenerateWwiseId(PackageEditorWindow pew)
        {
            if (pew.TryGetSelectedExport(out var exp) && File.Exists(Misc.AppSettings.Settings.Wwise_7110Path))
            {
                string name = exp.ObjectName.Name;
                MessageBox.Show(GetIDFromString(name).ToString());
            }
        }

        public static void CreateTestTLKWithStringIDs(PackageEditorWindow pew)
        {
            Microsoft.Win32.OpenFileDialog outputFileDialog = new () { 
                Title = "Select .XML file to import", 
                Filter = "*.xml|*.xml" };
            bool? result = outputFileDialog.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                Debug.WriteLine("No output file specified");
                return;
            }

            string inputXmlFile = outputFileDialog.FileName;
            string outputTlkFile = Path.ChangeExtension(inputXmlFile, "tlk");
            try
            {
                LegendaryExplorerCore.TLK.ME2ME3.HuffmanCompression hc =
                    new LegendaryExplorerCore.TLK.ME2ME3.HuffmanCompression();
                hc.LoadInputData(inputXmlFile, true);
                hc.SaveToFile(outputTlkFile);
                MessageBox.Show("Done.");
            }
            catch
            {
                MessageBox.Show("Unable to create test TLK file.");
            }
        }

        public static void UpdateLocalFunctions(PackageEditorWindow pew)
        {
            if (pew.TryGetSelectedExport(out var export) && ObjectBinary.From(export) is UStruct uStruct)
            {
                uStruct.UpdateChildrenChain(relinkChildrenStructs: false);
                if (uStruct is UClass uClass)
                {
                    uClass.UpdateLocalFunctions();
                }
                export.WriteBinary(uStruct);
            }
        }

        public static void DumpTOC()
        {
            Microsoft.Win32.OpenFileDialog outputFileDialog = new () { 
                Title = "Select TOC File", 
                Filter = "*.bin|*.bin" };
            bool? result = outputFileDialog.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                Debug.WriteLine("No output file specified");
                return;
            }
            string inputFile = outputFileDialog.FileName;
            string outputFile = Path.ChangeExtension(inputFile, "txt");

            var toc = new TOCBinFile(inputFile);
            toc.DumpTOCToTxtFile(outputFile);
        }

        public static void AddPresetGroup(string preset, PackageEditorWindow pew)
        {
            if (pew.SelectedItem.Entry.ClassName != "InterpData")
            {
                MessageBox.Show("InterpData not selected.", "Warning", MessageBoxButton.OK);
                return;
            }

            if (pew.SelectedItem.Entry is not ExportEntry interp)
                return;

            switch (preset)
            {
                case "Director":
                    MatineeHelper.AddPresetDirectorGroup(interp);
                    break;

                case "Camera":
                    if (PromptDialog.Prompt(null, "Name of camera actor:") is string camName)
                    {
                        if (string.IsNullOrEmpty(camName))
                        {
                            MessageBox.Show("Not a valid camera actor name.", "Warning", MessageBoxButton.OK);
                            return;
                        }
                        MatineeHelper.AddPresetCameraGroup(interp, camName);
                    }
                    break;
            }

            return;
        }
    }
}