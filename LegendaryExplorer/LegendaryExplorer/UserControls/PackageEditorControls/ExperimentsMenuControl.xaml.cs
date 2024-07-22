using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.Tools.CustomFilesManager;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.Tools.PackageEditor.Experiments;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;

namespace LegendaryExplorer.UserControls.PackageEditorControls
{
    /// <summary>
    /// Class that holds toolset development experiments. Actual experiment code should be in the Experiments classes
    /// </summary>
    public partial class ExperimentsMenuControl : MenuItem
    {
        public ExperimentsMenuControl()
        {
            LoadCommands();
            InitializeComponent();
        }

        public ICommand ForceReloadPackageCommand { get; set; }

        private void LoadCommands()
        {
            ForceReloadPackageCommand = new GenericCommand(ForceReloadPackageWithoutSharing, CanForceReload);
        }

        private static bool warnedOfReload = false;

        /// <summary>
        /// Forcibly reloads the package from disk. The package loaded in this instance will no longer be shared.
        /// </summary>
        internal void ForceReloadPackageWithoutSharing()
        {
            var peWindow = GetPEWindow();
            var fileOnDisk = peWindow.Pcc.FilePath;
            if (fileOnDisk != null && File.Exists(fileOnDisk))
            {
                if (peWindow.Pcc.IsModified)
                {
                    var warningResult = MessageBox.Show(GetPEWindow(), "The current package is modified. Reloading the package will cause you to lose all changes to this package.\n\nReload anyways?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (warningResult != MessageBoxResult.Yes)
                        return; // Do not continue!
                }

                if (!warnedOfReload)
                {
                    var warningResult = MessageBox.Show(GetPEWindow(), "Forcibly reloading a package will drop it out of tool sharing - making changes to this package in other will not be reflected in this window, and changes to this window will not be reflected in other windows. THIS MEANS SAVING WILL OVERWRITE CHANGES FROM OTHER WINDOWS. Only continue if you know what you are doing.\n\nReload anyways?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Error);
                    if (warningResult != MessageBoxResult.Yes)
                        return; // Do not continue!
                    warnedOfReload = true;
                }

                peWindow.GetSelected(out var selectedIndex);
                using var fStream = File.OpenRead(fileOnDisk);
                peWindow.LoadFileFromStream(fStream, fileOnDisk, selectedIndex);
                peWindow.Title += " (NOT SHARED WITH OTHER WINDOWS)";
            }
        }

        internal bool CanForceReload() => GetPEWindow()?.Pcc != null;

        public PackageEditorWindow GetPEWindow()
        {
            if (Window.GetWindow(this) is PackageEditorWindow pew)
            {
                return pew;
            }

            return null;
        }

        // EXPERIMENTS: GENERAL--------------------------------------------------------------------

        #region General Toolset experiments/debug stuff

        private void RefreshProperties_Clicked(object sender, RoutedEventArgs e)
        {
            var exp = GetPEWindow().InterpreterTab_Interpreter.CurrentLoadedExport;
            var properties = exp?.GetProperties();
        }

        private void BuildME1ObjectInfo_Clicked(object sender, RoutedEventArgs e)
        {
            ME1UnrealObjectInfo.generateInfo(Path.Combine(AppDirectories.ExecFolder, "ME1ObjectInfo.json"));
            GetPEWindow().RestoreAndBringToFront();
            MessageBox.Show(GetPEWindow(), "Done");
        }

        private void BuildME2ObjectInfo_Clicked(object sender, RoutedEventArgs e)
        {
            ME2UnrealObjectInfo.generateInfo(Path.Combine(AppDirectories.ExecFolder, "ME2ObjectInfo.json"));
            GetPEWindow().RestoreAndBringToFront();
            MessageBox.Show(GetPEWindow(), "Done");
        }

        private void BuildME3ObjectInfo_Clicked(object sender, RoutedEventArgs e)
        {
            ME3UnrealObjectInfo.generateInfo(Path.Combine(AppDirectories.ExecFolder, "ME3ObjectInfo.json"));
            GetPEWindow().RestoreAndBringToFront();
            MessageBox.Show(GetPEWindow(), "Done");
        }

        private void BuildAllObjectInfoOT_Clicked(object sender, RoutedEventArgs e)
        {
            ME1UnrealObjectInfo.generateInfo(Path.Combine(AppDirectories.ExecFolder, "ME1ObjectInfo.json"));
            ME2UnrealObjectInfo.generateInfo(Path.Combine(AppDirectories.ExecFolder, "ME2ObjectInfo.json"));
            ME3UnrealObjectInfo.generateInfo(Path.Combine(AppDirectories.ExecFolder, "ME3ObjectInfo.json"));
            GetPEWindow().RestoreAndBringToFront();
            MessageBox.Show(GetPEWindow(), "Done");
        }

        private void BuildLE1ObjectInfo_Clicked(object sender, RoutedEventArgs e)
        {
            var pew = GetPEWindow();
            pew.BusyText = "Building LE1 Object Info";
            pew.IsBusy = true;

            void setProgress(int done, int total)
            {
                Application.Current.Dispatcher.InvokeAsync(() => { pew.BusyText = $"Building LE1 Object Info [{done}/{total}]"; });
            }

            Task.Run(() => { LE1UnrealObjectInfo.generateInfo(Path.Combine(AppDirectories.ExecFolder, "LE1ObjectInfo.json"), true, setProgress); }).ContinueWithOnUIThread(x =>
            {
                pew.IsBusy = false;
                pew.RestoreAndBringToFront();
                MessageBox.Show(GetPEWindow(), "Done");
            });
        }

        private void BuildLE2ObjectInfo_Clicked(object sender, RoutedEventArgs e)
        {
            var pew = GetPEWindow();
            pew.BusyText = "Building LE2 Object Info";
            pew.IsBusy = true;

            void setProgress(int done, int total)
            {
                Application.Current.Dispatcher.InvokeAsync(() => { pew.BusyText = $"Building LE2 Object Info [{done}/{total}]"; });
            }

            Task.Run(() => { LE2UnrealObjectInfo.generateInfo(Path.Combine(AppDirectories.ExecFolder, "LE2ObjectInfo.json"), true, setProgress); }).ContinueWithOnUIThread(x =>
            {
                pew.IsBusy = false;
                pew.RestoreAndBringToFront();
                MessageBox.Show(GetPEWindow(), "Done");
            });
        }

        private void BuildLE3ObjectInfo_Clicked(object sender, RoutedEventArgs e)
        {
            var pew = GetPEWindow();
            pew.BusyText = "Building LE3 Object Info";
            pew.IsBusy = true;

            void setProgress(int done, int total)
            {
                Application.Current.Dispatcher.InvokeAsync(() => { pew.BusyText = $"Building LE3 Object Info [{done}/{total}]"; });
            }

            Task.Run(() => { LE3UnrealObjectInfo.generateInfo(Path.Combine(AppDirectories.ExecFolder, "LE3ObjectInfo.json"), true, setProgress); }).ContinueWithOnUIThread(x =>
            {
                pew.IsBusy = false;
                pew.RestoreAndBringToFront();
                MessageBox.Show(GetPEWindow(), "Done");
            });
        }

        private void BuildUDKObjectInfo_Clicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(UDKDirectory.DefaultGamePath))
            {
                MessageBox.Show(GetPEWindow(), "Specify a UDK path in the settings first.");
                return;
            }
            var pew = GetPEWindow();
            pew.BusyText = "Building UDK Object Info";
            pew.IsBusy = true;

            void setProgress(int done, int total)
            {
                Application.Current.Dispatcher.InvokeAsync(() => { pew.BusyText = $"Building UDK Object Info [{done}/{total}]"; });
            }

            Task.Run(() =>
            {
                UDKUnrealObjectInfo.generateInfo(Path.Combine(AppDirectories.ExecFolder, "UDKObjectInfo.json"), true, setProgress);
            }).ContinueWithOnUIThread(x =>
            {
                pew.IsBusy = false;
                pew.RestoreAndBringToFront();
                MessageBox.Show(GetPEWindow(), "Done");
            });
        }

        private void BuildAllObjectInfoLE_Clicked(object sender, RoutedEventArgs e)
        {
            var pew = GetPEWindow();
            pew.BusyText = "Building LE Object Info";
            pew.IsBusy = true;

            MEGame currentGame = MEGame.LE1;

            void setProgress(int done, int total)
            {
                Application.Current.Dispatcher.InvokeAsync(() => { pew.BusyText = $"Building {currentGame} Object Info [{done}/{total}]"; });
            }

            Stopwatch sw = new Stopwatch();

            Task.Run(() =>
            {
                sw.Start();
                LE1UnrealObjectInfo.generateInfo(Path.Combine(AppDirectories.ExecFolder, "LE1ObjectInfo.json"), true, setProgress);
                currentGame = MEGame.LE2;
                LE2UnrealObjectInfo.generateInfo(Path.Combine(AppDirectories.ExecFolder, "LE2ObjectInfo.json"), true, setProgress);
                currentGame = MEGame.LE3;
                LE3UnrealObjectInfo.generateInfo(Path.Combine(AppDirectories.ExecFolder, "LE3ObjectInfo.json"), true, setProgress);
                sw.Stop();
            }).ContinueWithOnUIThread(x =>
            {
                pew.IsBusy = false;
                pew.RestoreAndBringToFront();
                MessageBox.Show(GetPEWindow(), $"Done. Took {sw.Elapsed.TotalSeconds} seconds");
            });



        }

        private void BuildAllObjectInfo_Clicked(object sender, RoutedEventArgs e)
        {
            var pew = GetPEWindow();
            pew.BusyText = "Building Object Info";
            pew.IsBusy = true;

            var currentGame = MEGame.ME1;
            void setProgress(int done, int total)
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    pew.BusyText = $"Building {currentGame} Object Info [{done}/{total}]";
                });
            }
            var sw = new Stopwatch();

            Task.Run(() =>
            {
                sw.Start();
                ME1UnrealObjectInfo.generateInfo(Path.Combine(AppDirectories.ExecFolder, "ME1ObjectInfo.json"), true, setProgress);
                currentGame = MEGame.ME2;
                ME2UnrealObjectInfo.generateInfo(Path.Combine(AppDirectories.ExecFolder, "ME2ObjectInfo.json"), true, setProgress);
                currentGame = MEGame.ME3;
                ME3UnrealObjectInfo.generateInfo(Path.Combine(AppDirectories.ExecFolder, "ME3ObjectInfo.json"), true, setProgress);
                currentGame = MEGame.LE1;
                LE1UnrealObjectInfo.generateInfo(Path.Combine(AppDirectories.ExecFolder, "LE1ObjectInfo.json"), true, setProgress);
                currentGame = MEGame.LE2;
                LE2UnrealObjectInfo.generateInfo(Path.Combine(AppDirectories.ExecFolder, "LE2ObjectInfo.json"), true, setProgress);
                currentGame = MEGame.LE3;
                LE3UnrealObjectInfo.generateInfo(Path.Combine(AppDirectories.ExecFolder, "LE3ObjectInfo.json"), true, setProgress);
                sw.Stop();
            }).ContinueWithOnUIThread(x =>
            {
                pew.IsBusy = false;
                pew.RestoreAndBringToFront();
                MessageBox.Show(GetPEWindow(), $"Done. Took {sw.Elapsed.TotalSeconds} seconds");
            });
        }
        private void ObjectInfosSearch_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.SearchObjectInfos(GetPEWindow());
        }

        private void ReInventoryCustomClasses_Click(object sender, RoutedEventArgs e)
        {
            // Todo: Move this into a 'general' class
            PackageEditorExperimentsM.RebuildInternalResourceClassInformations(GetPEWindow());
        }

        private void GenerateObjectInfoDiff_Click(object sender, RoutedEventArgs e)
        {
            // SirC experiment?
            var enumsDiff = new Dictionary<string, (List<NameReference>, List<NameReference>)>();
            var structsDiff = new Dictionary<string, (ClassInfo, ClassInfo)>();
            var classesDiff = new Dictionary<string, (ClassInfo, ClassInfo)>();

            var immutableME1Structs = ME1UnrealObjectInfo.ObjectInfo.Structs
                .Where(kvp => ME1UnrealObjectInfo.IsImmutableStruct(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var immutableME2Structs = ME2UnrealObjectInfo.ObjectInfo.Structs
                .Where(kvp => ME2UnrealObjectInfo.IsImmutableStruct(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var immutableME3Structs = ME2UnrealObjectInfo.ObjectInfo.Structs
                .Where(kvp => ME3UnrealObjectInfo.IsImmutableStruct(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            foreach ((string className, ClassInfo classInfo) in immutableME1Structs)
            {
                if (immutableME2Structs.TryGetValue(className, out ClassInfo classInfo2) &&
                    (!classInfo.properties.SequenceEqual(classInfo2.properties) ||
                     classInfo.baseClass != classInfo2.baseClass))
                {
                    structsDiff.Add(className, (classInfo, classInfo2));
                }

                if (immutableME3Structs.TryGetValue(className, out ClassInfo classInfo3) &&
                    (!classInfo.properties.SequenceEqual(classInfo3.properties) ||
                     classInfo.baseClass != classInfo3.baseClass))
                {
                    structsDiff.Add(className, (classInfo, classInfo3));
                }
            }

            foreach ((string className, ClassInfo classInfo) in immutableME2Structs)
            {
                if (immutableME3Structs.TryGetValue(className, out ClassInfo classInfo3) &&
                    (!classInfo.properties.SequenceEqual(classInfo3.properties) ||
                     classInfo.baseClass != classInfo3.baseClass))
                {
                    structsDiff.Add(className, (classInfo, classInfo3));
                }
            }

            File.WriteAllText(System.IO.Path.Combine(AppDirectories.ExecFolder, "Diff.json"),
                JsonConvert.SerializeObject((immutableME1Structs, immutableME2Structs, immutableME3Structs),
                    Formatting.Indented));
            return;

            var srcEnums = ME2UnrealObjectInfo.ObjectInfo.Enums;
            var compareEnums = ME3UnrealObjectInfo.ObjectInfo.Enums;
            var srcStructs = ME2UnrealObjectInfo.ObjectInfo.Structs;
            var compareStructs = ME3UnrealObjectInfo.ObjectInfo.Structs;
            var srcClasses = ME2UnrealObjectInfo.ObjectInfo.Classes;
            var compareClasses = ME3UnrealObjectInfo.ObjectInfo.Classes;

            foreach ((string enumName, List<NameReference> values) in srcEnums)
            {
                if (!compareEnums.TryGetValue(enumName, out var values2) || !values.SubsetOf(values2))
                {
                    enumsDiff.Add(enumName, (values, values2));
                }
            }

            foreach ((string className, ClassInfo classInfo) in srcStructs)
            {
                if (!compareStructs.TryGetValue(className, out var classInfo2) ||
                    !classInfo.properties.SubsetOf(classInfo2.properties) ||
                    classInfo.baseClass != classInfo2.baseClass)
                {
                    structsDiff.Add(className, (classInfo, classInfo2));
                }
            }

            foreach ((string className, ClassInfo classInfo) in srcClasses)
            {
                if (!compareClasses.TryGetValue(className, out var classInfo2) ||
                    !classInfo.properties.SubsetOf(classInfo2.properties) ||
                    classInfo.baseClass != classInfo2.baseClass)
                {
                    classesDiff.Add(className, (classInfo, classInfo2));
                }
            }

            File.WriteAllText(System.IO.Path.Combine(AppDirectories.ExecFolder, "Diff.json"),
                JsonConvert.SerializeObject(new { enumsDiff, structsDiff, classesDiff }, Formatting.Indented));
        }

        #endregion

        // EXPERIMENTS: MGAMERZ---------------------------------------------------

        #region Mgamerz's Experiments
        private void LEXCustomFilesManager_Click(object sender, RoutedEventArgs e)
        {
            new CustomFilesManagerWindow().Show();
        }

        private void ImportWwiseBankTest_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.ImportBankTest(GetPEWindow());
        }

        private void MakeVTestDonor_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.ConvertMaterialToDonor(GetPEWindow());
        }
        private void RunMaterialInstanceScreenshot_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.StartMatScreenshot(GetPEWindow());
        }

        private void OrganizeParticleSystemExports_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.OrganizeParticleSystems(GetPEWindow());
        }

        private void ConvertSLCALightToNonSLCA(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.ConvertSLCALightToNonSLCA(GetPEWindow());
        }

        //private void MakeLE1MakoMap_Click(object sender, RoutedEventArgs e)
        //{
        //    PackageEditorExperimentsM.MakeMakoLevel(GetPEWindow());
        //}

        private void ImportUDKTerrain_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.ImportUDKTerrain(GetPEWindow());
        }

        /// <summary>
        /// If this proves useful, will graduate out of experiments
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FindAppErrorFLocation_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.FindBadReference(GetPEWindow());
        }

        private void DumpUScriptFromPackage_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.DumpUScriptFromPackage(GetPEWindow());
        }

        private void MaterializeModel_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.MaterializeModel(GetPEWindow());
        }

        private void LE2ConvertToSFXPawn_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.LE2ConvertBioPawnToSFXPawn(GetPEWindow());
        }

        private void MScanner_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.MScanner(GetPEWindow());
        }

        private void TestCurrentPackageBinary(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.TestCurrentPackageForUnknownBinary(GetPEWindow());
        }

        private void TestCrossGenClassPort_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.TestCrossGenClassPorting(GetPEWindow());
        }

        private void CheckNeverStream_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.CheckNeverstream(GetPEWindow());
        }

        private void GenerateMaterialInstanceConstant_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.GenerateMaterialInstanceConstantFromMaterial(GetPEWindow());
        }

        private void PrintTextureFormats_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.ShowTextureFormats(GetPEWindow());
        }

        private void MapMaterialIDs_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.MapMaterialIDs(GetPEWindow());
        }

        private void WwiseBankToProject_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.ConvertWwiseBankToProject(GetPEWindow());
        }

        private void CoalesceBioActorTypesLE1_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.CoalesceBioActorTypes(GetPEWindow());
        }

        private void ForceVignetteOff_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.CoalesceBioActorTypes(GetPEWindow());
        }

        private void RebuildSelectedMaterialExpressions(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.UpdateMaterialExpressionsList(GetPEWindow());
        }

        private async void SavePackageUnCompressed_Click(object sender, RoutedEventArgs e)
        {
            await GetPEWindow().Pcc.SaveAsync(compress: false);
        }

        private async void SavePackageCompressed_Click(object sender, RoutedEventArgs e)
        {
            await GetPEWindow().Pcc.SaveAsync(compress: true);
        }

        private void FindEmptyMips_Clicked(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.FindEmptyMips(GetPEWindow());
        }

        private void ExportTerrainCollisionDataToUDK_Clicked(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.ExpertTerrainDataToUDK(GetPEWindow());
        }

        private void DumpLE1TLK_Clicked(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.DumpAllLE1TLK(GetPEWindow());
        }

        private void ResolveAllGameImports_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.CheckAllGameImports(GetPEWindow().Pcc);
        }

        private void BuildME1TLKDB_Clicked(object sender, RoutedEventArgs e)
        {
            var pew = GetPEWindow();
            string myBasePath = ME1Directory.DefaultGamePath;
            string[] extensions = [".u", ".upk"];
            FileInfo[] files = new DirectoryInfo(ME1Directory.CookedPCPath)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Where(f => extensions.Contains(f.Extension.ToLower()))
                .ToArray();
            int i = 1;
            var stringMapping = new SortedDictionary<int, KeyValuePair<string, List<string>>>();
            foreach (FileInfo f in files)
            {
                pew.StatusBar_LeftMostText.Text = $"[{i}/{files.Length}] Scanning {f.FullName}";
                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
                int basePathLen = myBasePath.Length;
                using IMEPackage pack = MEPackageHandler.OpenMEPackage(f.FullName);
                List<ExportEntry> tlkExports = pack.Exports.Where(x =>
                    (x.ObjectName == "tlk" || x.ObjectName == "tlk_M") && x.ClassName == "BioTlkFile").ToList();
                if (tlkExports.Count > 0)
                {
                    string subPath = f.FullName.Substring(basePathLen);
                    Debug.WriteLine($"Found exports in {f.FullName.AsSpan(basePathLen)}");
                    foreach (ExportEntry exp in tlkExports)
                    {
                        var talkFile = new ME1TalkFile(exp);
                        foreach (var sref in talkFile.StringRefs)
                        {
                            if (sref.StringID == 0) continue; //skip blank
                            if (sref.Data is null or "-1" or "") continue; //skip blank

                            if (!stringMapping.TryGetValue(sref.StringID, out var dictEntry))
                            {
                                dictEntry = new KeyValuePair<string, List<string>>(sref.Data, []);
                                stringMapping[sref.StringID] = dictEntry;
                            }

                            if (sref.StringID == 158104)
                            {
                                Debugger.Break();
                            }

                            dictEntry.Value.Add($"{subPath} in uindex {exp.UIndex} \"{exp.ObjectName}\"");
                        }
                    }
                }

                i++;
            }

            int total = stringMapping.Count;
            using (StreamWriter file = new StreamWriter(@"C:\Users\Public\SuperTLK.txt"))
            {
                pew.StatusBar_LeftMostText.Text = "Writing... ";
                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
                foreach (KeyValuePair<int, KeyValuePair<string, List<string>>> entry in stringMapping)
                {
                    // do something with entry.Value or entry.Key
                    file.WriteLine(entry.Key);
                    file.WriteLine(entry.Value.Key);
                    foreach (string fi in entry.Value.Value)
                    {
                        file.WriteLine(" - " + fi);
                    }

                    file.WriteLine();
                }
            }

            pew.StatusBar_LeftMostText.Text = "Done";
        }

        private void ListNetIndexes_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.ListNetIndexes(GetPEWindow());
        }


        private void FindAllFilesWithSpecificName(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.FindNamedObject(GetPEWindow());
        }

        private void FindAllME3PowerCustomAction_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.FindAllME3PowerCustomActions();
        }

        private void FindAllME2PowerCustomAction_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.FindAllME2Powers();
        }

        private void GenerateNewGUIDForPackageFile_Clicked(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.GenerateNewGUIDForFile(GetPEWindow());
        }

        // Probably not useful in legendary since GetPEWindow() only did stuff for MP
        private void GenerateGUIDCacheForFolder_Clicked(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.GenerateGUIDCacheForFolder(GetPEWindow());
        }

        private void MakeAllGrenadesAmmoRespawn_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.MakeAllGrenadesAndAmmoRespawn(GetPEWindow());
        }

        private void PrintTerrainsBySize_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.PrintTerrainsBySize(GetPEWindow());
        }

        private void SetAllWwiseEventDurations_Click(object sender, RoutedEventArgs e)
        {
            var pew = GetPEWindow();
            pew.BusyText = "Scanning audio and updating events";
            pew.IsBusy = true;
            Task.Run(() => PackageEditorExperimentsM.SetAllWwiseEventDurations(pew.Pcc)).ContinueWithOnUIThread(prevTask =>
            {
                pew.IsBusy = false;
                MessageBox.Show("Wwiseevents updated.");
            });
        }

        private void ResetPackageTextures_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.ResetTexturesInFile(GetPEWindow().Pcc, GetPEWindow());
        }

        private void CramLevelFullOfEverything_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.CramLevelFullOfStuff(GetPEWindow().Pcc, GetPEWindow());
        }

        private void ResetVanillaPackagePart_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.ResetPackageVanillaPart(GetPEWindow().Pcc, GetPEWindow());
        }

        private void ResolveAllImports_Clicked(object sender, RoutedEventArgs e)
        {
            var pew = GetPEWindow();
            Task.Run(() => PackageEditorExperimentsM.CheckImports(pew.Pcc)).ContinueWithOnUIThread(prevTask => { pew.IsBusy = false; });
        }

        private void ResolveAllGameImports_Clicked(object sender, RoutedEventArgs e)
        {
            var pew = GetPEWindow();
            Task.Run(() => PackageEditorExperimentsM.CheckAllGameImports(pew.Pcc)).ContinueWithOnUIThread(prevTask => { pew.IsBusy = false; });
        }

        private void TintAllNormalizedAverageColor_Clicked(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.TintAllNormalizedAverageColors(GetPEWindow().Pcc);
        }

        private void RebuildLevelNetindexing_Clicked(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.RebuildFullLevelNetindexes();
        }

        private void BuildNativeTable_OnClick(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.BuildNativeTable(GetPEWindow());
        }

        private void ExtractPackageTextures_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.DumpPackageTextures(GetPEWindow().Pcc, GetPEWindow());
        }

        private void FindExternalizableTextures_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.FindExternalizableTextures(GetPEWindow());
        }

        private void ValidateNavpointChain_Clicked(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.ValidateNavpointChain(GetPEWindow().Pcc);
        }

        private void TriggerObjBinGetNames_Clicked(object sender, RoutedEventArgs e)
        {
            if (GetPEWindow().TryGetSelectedExport(out var exp))
            {
                ObjectBinary bin = ObjectBinary.From(exp);
                var names = bin.GetNames(exp.FileRef.Game);
                foreach (var n in names)
                {
                    Debug.WriteLine($"{n.Item1.Instanced} {n.Item2}");
                }
            }
        }

        private void TriggerObjBinGetUIndexes_Clicked(object sender, RoutedEventArgs e)
        {
            if (GetPEWindow().TryGetSelectedExport(out var exp))
            {
                ObjectBinary.From(exp).ForEachUIndex(exp.FileRef.Game, new UIndexDebugLogger());
            }
        }

        private readonly struct UIndexDebugLogger : IUIndexAction
        {
            public void Invoke(ref int uIndex, string propName)
            {
                Debug.WriteLine($"{uIndex} {propName}");
            }
        }

        private void PrintLoadedPackages_Clicked(object sender, RoutedEventArgs e)
        {
            MEPackageHandler.PrintOpenPackages();
        }

        private void ShiftME1AnimCutScene(object sender, RoutedEventArgs e)
        {
            var selected = GetPEWindow().TryGetSelectedExport(out var export);
            if (selected)
            {
                PackageEditorExperimentsM.ShiftME1AnimCutscene(export);
            }
        }

        private void ShiftInterpTrackMovePackageWide(object sender, RoutedEventArgs e)
        {
            var pccLoaded = GetPEWindow().Pcc != null;
            if (pccLoaded)
            {
                PackageEditorExperimentsM.ShiftInterpTrackMovesInPackage(GetPEWindow().Pcc, null);
            }
        }

        private void ShiftInterpTrackMovePackageWideNoAnchor(object sender, RoutedEventArgs e)
        {
            var pccLoaded = GetPEWindow().Pcc != null;
            if (pccLoaded)
            {
                PackageEditorExperimentsM.ShiftInterpTrackMovesInPackage(GetPEWindow().Pcc, x =>
                {
                    var prop = x.GetProperty<EnumProperty>("MoveFrame");
                    if (prop == null || prop.Value != "IMF_AnchorObject") return true;
                    return false; // IMF_AnchorObject
                });
            }
        }

        private void ShiftInterpTrackMove(object sender, RoutedEventArgs e)
        {
            var selected = GetPEWindow().TryGetSelectedExport(out var export);
            if (selected)
            {
                PackageEditorExperimentsM.ShiftInterpTrackMove(export);
            }
        }

        private void RandomizeTerrain_Click(object sender, RoutedEventArgs e)
        {
            if (GetPEWindow().Pcc == null) return;
            PackageEditorExperimentsM.RandomizeTerrain(GetPEWindow().Pcc);
        }

        private void StripLightmap_Click(object sender, RoutedEventArgs e)
        {
            if (GetPEWindow().Pcc == null) return;
            PackageEditorExperimentsM.StripLightmap(GetPEWindow());
        }

        private void FixFXAMemoryNames_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.FixFXAMemoryNames(GetPEWindow());
        }
        private void ConvertExportToImport_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.ConvertExportToImport(GetPEWindow());
        }

        private void FromPackageUScriptFromFolder_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.CompilePackageUScriptFromFolder(GetPEWindow());
        }

        #endregion

        // EXPERIMENTS: SIRCXYRTYX-----------------------------------------------------

        #region SirCxyrtyx's Experiments

        private void GenerateGhidraStructInsertionScript(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.GenerateGhidraStructInsertionScript(GetPEWindow());
        }

        private void CalculateProbeFuncs_OnClick(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.CalculateProbeNames(GetPEWindow());
        }

        private void MakeME1TextureFileList(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.MakeME1TextureFileList(GetPEWindow());
        }

        private void OpenMapInGame_Click(object sender, RoutedEventArgs e)
        {
            OpenMapInGame();
        }

        private void DumpAllShaders_Click(object sender, RoutedEventArgs e)
        {
            if (GetPEWindow().Pcc == null) return;
            PackageEditorExperimentsS.DumpAllShaders(GetPEWindow().Pcc);
        }

        private void DumpMaterialShaders_Click(object sender, RoutedEventArgs e)
        {
            var pew = GetPEWindow();
            if (pew.TryGetSelectedExport(out ExportEntry matExport) && matExport.IsA("MaterialInterface"))
            {
                PackageEditorExperimentsS.DumpMaterialShaders(matExport);
            }
        }

        void OpenMapInGame()
        {
            const string tempMapName = "__ME3EXPDEBUGLOAD";
            var Pcc = GetPEWindow().Pcc;

            if (Pcc.Exports.All(exp => exp.ClassName != "Level"))
            {
                MessageBox.Show(GetPEWindow(), "GetPEWindow() file is not a map file!");
            }

            //only works for ME3?
            string mapName = System.IO.Path.GetFileNameWithoutExtension(Pcc.FilePath);

            string tempDir = MEDirectories.GetCookedPath(Pcc.Game);
            tempDir = Pcc.Game == MEGame.ME1 ? System.IO.Path.Combine(tempDir, "Maps") : tempDir;
            string tempFilePath = System.IO.Path.Combine(tempDir, $"{tempMapName}.{(Pcc.Game == MEGame.ME1 ? "SFM" : "pcc")}");

            Pcc.Save(tempFilePath);

            using (var tempPcc = MEPackageHandler.OpenMEPackage(tempFilePath, forceLoadFromDisk: true))
            {
                //insert PlayerStart if neccesary
                if (tempPcc.Exports.FirstOrDefault(exp => exp.ClassName == "PlayerStart") is null)
                {
                    var levelExport = tempPcc.Exports.First(exp => exp.ClassName == "Level");
                    Level level = ObjectBinary.From<Level>(levelExport);
                    float x = 0, y = 0, z = 0;
                    if (tempPcc.TryGetUExport(level.NavListStart, out ExportEntry firstNavPoint))
                    {
                        if (firstNavPoint.GetProperty<StructProperty>("Location") is StructProperty locProp)
                        {
                            (x, y, z) = CommonStructs.GetVector3(locProp);
                        }
                        else if (firstNavPoint.GetProperty<StructProperty>("location") is StructProperty locProp2)
                        {
                            (x, y, z) = CommonStructs.GetVector3(locProp2);
                        }
                    }

                    ExportEntry playerStart = new ExportEntry(tempPcc, levelExport, tempPcc.GetNextIndexedName("PlayerStart"), properties:
                    [
                        CommonStructs.Vector3Prop(x, y, z, "location")
                    ])
                    {
                        Class = tempPcc.GetEntryOrAddImport("Engine.PlayerStart", "Class")
                    };
                    tempPcc.AddExport(playerStart);
                    level.Actors.Add(playerStart.UIndex);
                    levelExport.WriteBinary(level);
                }

                tempPcc.Save();
            }

            Process.Start(MEDirectories.GetExecutablePath(Pcc.Game), $"{tempMapName} -nostartupmovies");
        }

        private void ConvertAllDialogueToSkippable_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.ConvertAllDialogueToSkippable(GetPEWindow());
        }

        private void CreateDynamicLighting(object sender, RoutedEventArgs e)
        {
            if (GetPEWindow().Pcc == null) return;
            PackageEditorExperimentsS.CreateDynamicLighting(GetPEWindow().Pcc);
        }

        private void ConvertToDifferentGameFormat_Click(object sender, RoutedEventArgs e)
        {
            // TODO: IMPLEMENT IN LEX
            //var pew = GetPEWindow();
            //if (pew.Pcc is MEPackage pcc)
            //{
            //    var gameString = InputComboBoxDialog.GetValue(GetPEWindow(), "Which game's format do you want to convert to?",
            //        "Game file converter",
            //        new[] { "ME1", "ME2", "ME3" }, "ME2");
            //    if (Enum.TryParse(gameString, out MEGame game))
            //    {
            //        pew.IsBusy = true;
            //        pew.BusyText = "Converting...";
            //        Task.Run(() => { pcc.ConvertTo(game); }).ContinueWithOnUIThread(prevTask =>
            //        {
            //            pew.IsBusy = false;
            //            pew.SaveFileAs();
            //            pew.Close();
            //        });
            //    }
            //}
            //else
            //{
            //    MessageBox.Show(GetPEWindow(), "Can only convert Mass Effect files!");
            //}
        }

        private void ReSerializeExport_Click(object sender, RoutedEventArgs e)
        {
            if (GetPEWindow().TryGetSelectedExport(out ExportEntry export))
            {
                PackageEditorExperimentsS.ReserializeExport(export);
            }
        }

        private void RunPropertyCollectionTest(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.RunPropertyCollectionTest(GetPEWindow());
        }

        private void UDKifyTest(object sender, RoutedEventArgs e)
        {
            if (GetPEWindow().Pcc != null)
            {
                PackageEditorExperimentsS.UDKifyTest(GetPEWindow());
            }
        }

        private void CondenseAllArchetypes(object sender, RoutedEventArgs e)
        {
            var pew = GetPEWindow();
            if (pew.Pcc != null && pew.Pcc.Exports.FirstOrDefault(exp => exp.ClassName == "Level") is ExportEntry level)
            {
                pew.IsBusy = true;
                pew.BusyText = "Condensing Archetypes";
                Task.Run(() =>
                {
                    foreach (ExportEntry export in level.GetAllDescendants().OfType<ExportEntry>())
                    {
                        export.CondenseArchetypes(false);
                    }

                    pew.IsBusy = false;
                });
            }
        }

        private void DumptTaggedWwiseStreams_OnClick(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.DumpSound(GetPEWindow());
        }

        private void ScanStuff_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.ScanStuff(GetPEWindow());
        }

        private void ReSerializeAllProperties_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.ReSerializeAllProperties(GetPEWindow());
        }

        private void CompileCompressionStats_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.CompileCompressionStats(GetPEWindow());
        }

        private void ReSerializeAllObjectBinary_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.ReSerializeAllObjectBinary(GetPEWindow());
        }

        private void ReSerializeAllObjectBinaryInFile_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.ReSerializeAllObjectBinaryInFile(GetPEWindow());
        }

        private void ConvertFileToME3(object sender, RoutedEventArgs e)
        {
            // TODO: IMPLEMENT IN LEX

            // This should be moved into a specific experiments class
            /*
            var pew = GetPEWindow();
            pew.BusyText = "Converting files";
            pew.IsBusy = true;
            string tfc = PromptDialog.Prompt(GetPEWindow(), "Enter Name of Target Textures File Cache (tfc) without extension",
                "Level Conversion Tool", "Textures_DLC_MOD_", false, PromptDialog.InputType.Text);

            if (pew.Pcc == null || tfc == null || tfc == "Textures_DLC_MOD_")
                return;
            tfc = Path.Combine(Path.GetDirectoryName(pew.Pcc.FilePath), $"{tfc}.tfc");

            if (pew.Pcc is MEPackage tgt && pew.Pcc.Game != MEGame.ME3)
            {
                Task.Run(() => tgt.ConvertTo(MEGame.ME3, tfc, true)).ContinueWithOnUIThread(prevTask =>
                {
                    pew.IsBusy = false;
                });
            }*/
        }

        private void RecompileAll_OnClick(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.RecompileAll(GetPEWindow());
        }

        private void FindOpCode_OnClick(object sender, RoutedEventArgs e)
        {
            var pew = GetPEWindow();
            if (pew.Pcc != null)
            {
                if (!short.TryParse(PromptDialog.Prompt(GetPEWindow(), "enter opcode"), out short opCode))
                {
                    return;
                }

                var exportsWithOpcode = new List<EntryStringPair>();
                foreach (ExportEntry export in pew.Pcc.Exports.Where(exp => exp.ClassName == "Function"))
                {
                    if (pew.Pcc.Game is MEGame.ME3)
                    {
                        (List<Token> tokens, _) = Bytecode.ParseBytecode(export.GetBinaryData<UFunction>().ScriptBytes, export);
                        if (tokens.Find(tok => tok.op == opCode) is Token token)
                        {
                            exportsWithOpcode.Add(new EntryStringPair(export, token.posStr));
                        }
                    }
                    else
                    {
                        var func = LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode.UE3FunctionReader.ReadFunction(export);
                        func.Decompile(new LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode.TextBuilder(), false, true);
                        if (func.Statements.statements.Count > 0
                            && func.Statements.statements[0].Reader.ReadTokens.Find(tok => (short)tok.OpCode == opCode) is { })
                        {
                            exportsWithOpcode.Add(new EntryStringPair(export, ""));
                        }
                    }
                }

                var dlg = new ListDialog(exportsWithOpcode, $"functions with opcode 0x{opCode:X}", "", GetPEWindow())
                {
                    DoubleClickEntryHandler = pew.GetEntryDoubleClickAction()
                };
                dlg.Show();
            }
        }

        private void DumpShaderTypes_OnClick(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.DumpShaderTypes(GetPEWindow());
        }

        private void ScanHeaders_OnCLick(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.ScanPackageHeader(GetPEWindow());
        }

        private void PortShadowMaps_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.PortShadowMaps(GetPEWindow());
        }
        private void CompileLooseClassFolder_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.CompileLooseClassFolder(GetPEWindow());
        }
        private void DumpClassSource_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.DumpClassSource(GetPEWindow());
        }
        private void RegenCachedPhysBrushData_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.RegenCachedPhysBrushData(GetPEWindow());
        }

        #endregion

        // EXPERIMENTS: KINKOJIRO ------------------------------------------------------------

        #region Kinkojiro's Experiments

        public void AutoEnumerateClassNetIndex(object sender, RoutedEventArgs e)
        {
            int baseindex = 0;
            var Pcc = GetPEWindow().Pcc;
            if (GetPEWindow().TryGetSelectedExport(out var classexp) && classexp.IsClass)
            {
                baseindex = classexp.NetIndex;
                var classbin = classexp.GetBinaryData<UClass>();
                ExportEntry defaultxp = classexp.FileRef.GetUExport(classbin.Defaults);
                defaultxp.NetIndex = baseindex + 1;
                EnumerateChildNetIndexes(classbin.Children);
            }

            void EnumerateChildNetIndexes(int child)
            {
                if (child > 0 && child <= Pcc.ExportCount)
                {
                    var childexp = Pcc.GetUExport(child);
                    baseindex--;
                    childexp.NetIndex = baseindex;
                    var childbin = ObjectBinary.From(childexp);
                    if (childbin is UFunction funcbin)
                    {
                        EnumerateChildNetIndexes(funcbin.Children);
                        EnumerateChildNetIndexes(funcbin.Next);
                    }
                    else if (childbin is UState statebin)
                    {
                        EnumerateChildNetIndexes(statebin.Children);
                        EnumerateChildNetIndexes(statebin.Next);
                    }
                    else if (childbin is UProperty propbin)
                    {
                        if (childbin is UArrayProperty arraybin)
                        {
                            EnumerateChildNetIndexes(arraybin.ElementType);
                        }

                        EnumerateChildNetIndexes(propbin.Next);
                    }
                }

                return;
            }
        }

        private void TransferLevelBetweenGames(object sender, RoutedEventArgs e)
        {
            var pew = GetPEWindow();
            var Pcc = pew.Pcc;
            if (Pcc is MEPackage pcc && Path.GetFileNameWithoutExtension(pcc.FilePath).StartsWith("BioP") &&
                pcc.Game == MEGame.ME2)
            {
                var cdlg = MessageBox.Show(
                    "GetPEWindow() is a highly experimental method to copy the static art and collision from an ME2 level to an ME3 one.  It will not copy materials or design elements.",
                    "Warning", MessageBoxButton.OKCancel);
                if (cdlg == MessageBoxResult.Cancel)
                    return;

                CommonOpenFileDialog o = new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    EnsurePathExists = true,
                    Title = "Select output folder"
                };
                if (o.ShowDialog(GetPEWindow()) == CommonFileDialogResult.Ok)
                {
                    string tfc = PromptDialog.Prompt(GetPEWindow(),
                        "Enter Name of Target Textures File Cache (tfc) without extension", "Level Conversion Tool",
                        "Textures_DLC_MOD_", false, inputType: PromptDialog.InputType.Text);

                    if (tfc == null || tfc == "Textures_DLC_MOD_")
                        return;

                    pew.BusyText = "Parsing level files";
                    pew.IsBusy = true;
                    Task.Run(() =>
                        PackageEditorExperimentsK.ConvertLevelToGame(MEGame.ME3, pcc, o.FileName, tfc,
                            newText => pew.BusyText = newText)).ContinueWithOnUIThread(prevTask =>
                    {
                        if (Pcc != null)
                            pew.LoadFile(Pcc.FilePath);
                        pew.IsBusy = false;
                        var dlg = new ListDialog(prevTask.Result, $"Conversion errors: ({prevTask?.Result.Count})", "",
                            GetPEWindow())
                        {
                            DoubleClickEntryHandler = pew.GetEntryDoubleClickAction()
                        };
                        dlg.Show();
                    });
                }
            }
            else
            {
                MessageBox.Show(GetPEWindow(),
                    "Load a level's BioP file to start the transfer.\nCurrently can only convert from ME2 to ME3.");
            }
        }

        private void RestartTransferFromJSON(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsK.RestartTransferFromJSON(GetPEWindow(), GetPEWindow().GetEntryDoubleClickAction());
        }

        private void RecookLevelToTestFromJSON(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsK.RecookLevelToTestFromJSON(GetPEWindow(), GetPEWindow().GetEntryDoubleClickAction());
        }

        private void CopyPackageName(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(Path.GetFileNameWithoutExtension(GetPEWindow().Pcc?.FilePath));
        }

        private void SaveAsNewPackage(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsK.SaveAsNewPackage(GetPEWindow());
        }

        private void RunTrashCompactor(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsK.TrashCompactor(GetPEWindow(), GetPEWindow().Pcc);
        }

        private void NewSeekFreeFile(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsK.NewSeekFreeFile(GetPEWindow());
        }

        private void AddAssetsToReferencer(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsK.AddAllAssetsToReferencer(GetPEWindow());
        }

        private void ClassUpgrade(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsK.ChangeClassesGlobally(GetPEWindow());
        }

        private void BlowMeUp(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsK.ShaderDestroyer(GetPEWindow());
        }

        private void AddGrpsToInterpData(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsK.AddNewInterpGroups(GetPEWindow());
        }

        private void ParseMapNamesToObjects(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsK.ParseMapNames(GetPEWindow());
        }
        private void ShiftInterpTrackMovePackageWideScene(object sender, RoutedEventArgs e)
        {
            var pccLoaded = GetPEWindow().Pcc != null;
            if (pccLoaded)
            {
                PackageEditorExperimentsK.ShiftInterpTrackMovesInPackageWithRotation(GetPEWindow().Pcc, x =>
                {
                    var prop = x.GetProperty<EnumProperty>("MoveFrame");
                    if (prop == null || prop.Value != "IMF_AnchorObject") return true;
                    return false; // IMF_AnchorObject
                });
            }
        }
        private void MakeInterpTrackMovesIntoAnchors(object sender, RoutedEventArgs e)
        {
            var pccLoaded = GetPEWindow().Pcc != null;
            if (pccLoaded)
            {
                PackageEditorExperimentsK.MakeInterpTrackMovesStageRelative(GetPEWindow().Pcc, x =>
                {
                    var prop = x.GetProperty<EnumProperty>("MoveFrame");
                    if (prop == null || prop.Value != "IMF_AnchorObject") return true;
                    return false; // IMF_AnchorObject
                });
            }
        }

        #endregion

        // EXPERIMENTS: HENBAGLE ------------------------------------------------------------
        #region HenBagle's Experiments

        private void BuildME1SuperTLK_Clicked(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsH.BuildME1SuperTLKFile(GetPEWindow());
        }

        private void AssociateAllExtensions_Clicked(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsH.AssociateAllExtensions();
        }

        private void GenerateAudioFileInfo_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsH.CreateAudioSizeInfo(GetPEWindow(), MEGame.LE3);
        }

        private void GenerateWwiseId_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsH.GenerateWwiseId(GetPEWindow());
        }

        private void CreateTestTLKWithStringIDs_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsH.CreateTestTLKWithStringIDs(GetPEWindow());
        }

        private void UpdateLocalFunctions_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsH.UpdateLocalFunctions(GetPEWindow());
        }

        private void DumpTOC_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsH.DumpTOC();
        }

        private void ExportBioMorphFace_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsH.ExportMorphFace(GetPEWindow());
        }

        #endregion

        // EXPERIMENTS: OTHER PEOPLE ------------------------------------------------------------
        #region Other people's experiments
        private void ExportLevelToT3D_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.DumpPackageToT3D(GetPEWindow().Pcc);
        }

        private void AddPresetDirectorGroup_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.AddPresetGroup("Director", GetPEWindow());
        }

        private void AddPresetCameraGroup_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.AddPresetGroup("Camera", GetPEWindow());
        }

        private void AddPresetActorGroup_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.AddPresetGroup("Actor", GetPEWindow());
        }

        private void AddPresetGestureTrack_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.AddPresetTrack("Gesture", GetPEWindow());
        }

        private void AddPresetGestureTrack2_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.AddPresetTrack("Gesture2", GetPEWindow());
        }

        private void BatchPatchMaterialsParameters_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.BatchPatchMaterialsParameters(GetPEWindow());
        }

        private void BatchSetBoolPropVal_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.BatchSetBoolPropVal(GetPEWindow());
        }

        private void Baldinator_Click(object sender, RoutedEventArgs a)
        {
            PackageEditorExperimentsO.Baldinator(GetPEWindow());
        }

        private void Rollinator_Click(object sender, RoutedEventArgs a)
        {
            PackageEditorExperimentsO.Rollinator(GetPEWindow());
        }

        private void CopyProperty_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.CopyProperty(GetPEWindow());
        }

        private void CopyMatToBMOorMIC_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.CopyMatToBMOorMIC(GetPEWindow());
        }

        private void SMRefRemover_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.SMRefRemover(GetPEWindow());
        }

        private void CleanConvoDonor_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.CleanConvoDonor(GetPEWindow());
        }

        private void CleanSequence_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.CleanSequenceExperiment(GetPEWindow());
        }

        private void CleanSequenceInterpDatas_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.CleanSequenceInterpDatasExperiment(GetPEWindow());
        }

        private void ChangeConvoIDandConvNodeIDs_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.ChangeConvoIDandConvNodeIDsExperiment(GetPEWindow());
        }

        private void RenameConversation_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.RenameConversationExperiment(GetPEWindow());
        }

        private void RenameAudio_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.RenameAudioExperiment(GetPEWindow());
        }

        private void UpdateAmbPerfClass_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.UpdateAmbPerfClassExperiment(GetPEWindow());
        }

        private void BatchUpdateAmbPerfClass_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.BatchUpdateAmbPerfClassExperiment(GetPEWindow());
        }

        private void Replace1DLightMapColors_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.Replace1DLightMapColors(GetPEWindow());
        } 

        private void Replace1DLightMapColorsOfExports_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.Replace1DLightMapColorsOfExports(GetPEWindow());
        } 

        private void BatchReplace1DLightMapColors_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.BatchReplace1DLightMapColors(GetPEWindow());
        }

        private void MakeExportsForced_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.MakeExportsForced(GetPEWindow());
        }

        private void CollectSMCsintoSMCA_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.CollectSMCsintoSMCA(GetPEWindow());
        }

        private void AddPrefabToLevel_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.AddPrefabToLevel(GetPEWindow());
        }

        private void AddStreamingKismet_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.AddStreamingKismetExperiment(GetPEWindow());
        }

        private void StreamFile_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.StreamFileExperiment(GetPEWindow());
        }
        #endregion

        // EXPERIMENTS: CHONKY DB---------------------------------------------------------
        #region Object Database
        // This is for cross-game porting
        private void ChonkyDB_BuildLE1GameDB(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.BuildAllObjectsGameDB(MEGame.LE1, GetPEWindow());
        }

        private void ChonkyDB_BuildME1GameDB(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.BuildAllObjectsGameDB(MEGame.ME1, GetPEWindow());
        }

        private void ChonkyDB_BuildLE2GameDB(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.BuildAllObjectsGameDB(MEGame.LE2, GetPEWindow());
        }

        private void ChonkyDB_BuildME2GameDB(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.BuildAllObjectsGameDB(MEGame.ME2, GetPEWindow());
        }

        private void ChonkyDB_BuildLE3GameDB(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.BuildAllObjectsGameDB(MEGame.LE3, GetPEWindow());
        }

        private void ChonkyDB_BuildME3GameDB(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.BuildAllObjectsGameDB(MEGame.ME3, GetPEWindow());
        }
        #endregion

        // PLEASE MOVE YOUR EXPERIMENT HANDLER INTO YOUR SECTION ABOVE
    }
}
