using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.TLK;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using BioMorphFace = LegendaryExplorerCore.Unreal.Classes.BioMorphFace;

namespace LegendaryExplorer.Tools.PackageEditor.Experiments
{
    static internal class PackageEditorExperimentsH
    {
        /// <summary>
        /// Collects all TLK exports from the entire ME1 game and exports them into a single GlobalTLK file
        /// </summary>
        /// <param name="pew">Instance of Package Editor</param>
        public static void BuildME1SuperTLKFile (PackageEditorWindow pew)
        {
            string gameString = InputComboBoxDialog.GetValue(pew, "Choose game to create SuperTLK for:",
                "Create SuperTLK file", new[] { "LE1", "ME1" }, "LE1");
            var game = Enum.Parse<MEGame>(gameString);
            if(!game.IsGame1()) return;

            var locPrompt = new PromptDialog("Enter file localization suffix to scan",
                "Create SuperTLK file", "_INT")
            {
                Owner = pew
            };
            locPrompt.ShowDialog();
            if (locPrompt.DialogResult == false) return;
            var locSuffix = locPrompt.ResponseText;

            string myBasePath = MEDirectories.GetDefaultGamePath(game);
            string searchDir = MEDirectories.GetCookedPath(game);

            CommonOpenFileDialog d = new CommonOpenFileDialog { Title = "Select folder to search", IsFolderPicker = true, InitialDirectory = myBasePath };
            if (d.ShowDialog() == CommonFileDialogResult.Ok)
            {
                searchDir = d.FileName;
            }

            var filter = game is MEGame.LE1 ? "*.pcc|*.pcc" : "*.upk|*.upk";
            Microsoft.Win32.OpenFileDialog outputFileDialog = new () { 
                Title = "Select GlobalTlk file to output to (GlobalTlk exports will be completely overwritten)", 
                Filter = filter };
            bool? result = outputFileDialog.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                Debug.WriteLine("No output file specified");
                return;
            }
            string outputFilePath = outputFileDialog.FileName;

            string[] extensions = { ".u", ".upk", ".pcc" };

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
                    if ((f.Name.Contains("LOC") || f.Name.Contains("Startup")) && !f.Name.Contains(locSuffix))
                    {
                        i++;
                        continue;
                    }
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
                            var LoadedStrings = new List<TLKStringRef>();
                            foreach (var tlkString in stringMapping)
                            {
                                // Do the important part
                                LoadedStrings.Add(new TLKStringRef(tlkString.Key, tlkString.Value, 1));
                            }

                            HuffmanCompression huff = new HuffmanCompression();
                            huff.LoadInputData(LoadedStrings);
                            huff.SerializeTalkfileToExport(exp);
                        }
                    }
                    o.Save();

                }

                return total;

            }).ContinueWithOnUIThread(async (total) =>
            {
                var actualTotal = await total;
                pew.IsBusy = false;
                pew.StatusBar_LeftMostText.Text = $"Wrote {actualTotal} lines to {outputFilePath}";
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

        public static void ExportMorphFace(PackageEditorWindow pew)
        {
            if (pew.TryGetSelectedExport(out var export) && export.ClassName == "BioMorphFace")
            {
                if (UModelHelper.GetLocalUModelVersionAsync().Result < UModelHelper.SupportedUModelBuildNum)
                {
                    MessageBox.Show("UModel not installed or incorrect version!");
                    return;
                }
                pew.IsBusy = true;
                pew.BusyText = "Applying MorphFace to head mesh...";
                var morphFace = new BioMorphFace(export);
                var rop = new RelinkerOptionsPackage();

                // Create a new file containing only the headmesh
                var tempFilePath = Path.Combine(Path.GetTempPath(), "HeadMeshExport.pcc");
                if(File.Exists(tempFilePath)) File.Delete(tempFilePath);
                MEPackageHandler.CreateAndSavePackage(tempFilePath, export.Game);
                using var tempFile = MEPackageHandler.OpenMEPackage(tempFilePath);
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies,
                    morphFace.m_oBaseHead, tempFile, null, true, rop, out var clonedHeadEntry);
                var clonedHead = clonedHeadEntry as ExportEntry;
                var appliedHead = morphFace.Apply();

                // Clone materials
                for (var i = 0; i < appliedHead.Materials.Length; i++)
                {
                    var originalMat = export.FileRef.GetEntry(appliedHead.Materials[i]);
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies,
                        originalMat, tempFile, null, true, rop, out var clonedMat);
                    appliedHead.Materials[i] = clonedMat.UIndex;
                }
                clonedHead.WriteBinary(appliedHead);
                clonedHead.ObjectName = new NameReference(export.ObjectNameString);
                tempFile.Save();

                // Ensure UModel
                // Pass error message back
                Task.Run(() =>
                {
                    return UModelHelper.EnsureUModel(
                        () => pew.IsBusy = true,
                        null,
                        null,
                        busyText => pew.BusyText = busyText
                    );
                }).ContinueWithOnUIThread(x =>
                {
                    // Export the cloned headmesh
                    if (x != null)
                    {
                        MessageBox.Show($"Couldn't export via umodel: {x.Result}");
                    }
                    else
                    {
                        pew.BusyText = "Exporting via UModel...";
                        UModelHelper.ExportViaUModel(pew, clonedHead);
                        //File.Delete(tempFilePath);
                    }
                    pew.IsBusy = false;
                });
                
            }
            else
            {
                MessageBox.Show("Must have 'BioMorphFace' export selected in the tree view.");
            }
        }
    }
}