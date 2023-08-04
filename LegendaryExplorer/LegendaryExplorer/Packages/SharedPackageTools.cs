using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Misc.ME3Tweaks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using Microsoft.Win32;
using Xceed.Wpf.Toolkit.Primitives;

namespace LegendaryExplorer.Packages
{
    internal class SharedPackageTools
    {
        /// <summary>
        /// Compares two packages and displays the results in a ListDialog that supports a double click event that is provided by the calling window
        /// </summary>
        /// <param name="wpfBase"></param>
        /// <param name="entryDoubleClick"></param>
        /// <param name="package"></param>
        /// <param name="diskPath"></param>
        /// <param name="packageStream"></param>
        public static void CompareToPackageWrapper(WPFBase wpfBase, Action<EntryStringPair> entryDoubleClick, IMEPackage package = null, string diskPath = null, Stream packageStream = null)
        {
            Task.Run(() =>
            {
                wpfBase.BusyText = "Comparing packages...";
                wpfBase.IsBusy = true;
                try
                {
                    if (package != null) return (object)wpfBase.Pcc.CompareToPackage(package);
                    if (diskPath != null) return (object)wpfBase.Pcc.CompareToPackage(diskPath);
                    if (packageStream != null) return (object)wpfBase.Pcc.CompareToPackage(packageStream);
                    return "CompareToPackageWrapper() requires at least one parameter be set!";
                }
                catch (Exception e)
                {
                    return e.Message;
                }
            }).ContinueWithOnUIThread(result =>
            {
                wpfBase.IsBusy = false;
                if (result.Result is string errorMessage)
                {
                    MessageBox.Show(errorMessage, "Error comparing packages");
                }
                else if (result.Result is List<EntryStringPair> results)
                {
                    if (Enumerable.Any(results))
                    {
                        ListDialog ld = new ListDialog(results, "Changed exports/imports/names between files",
                                "The following exports, imports, and names are different between the files.", wpfBase)
                        { DoubleClickEntryHandler = entryDoubleClick };
                        ld.Show();
                    }
                    else
                    {
                        MessageBox.Show("No changes between names/imports/exports were found between the files.", "Packages seem identical");
                    }
                }
            });
        }

        /// <summary>
        /// Opens a compare to package
        /// </summary>
        /// <param name="wpfBase"></param>
        /// <param name="entryDoubleClickCallback"></param>
        public static void ComparePackageToUnmodded(WPFBase wpfBase, Action<EntryStringPair> entryDoubleClickCallback)
        {
            if (wpfBase.Pcc == null) return;

            if (!wpfBase.Pcc.Game.IsMEGame())
            {
                MessageBox.Show(wpfBase, "Can only compare packages from the Original Trilogy or Legendary Edition.",
                    "Can't compare", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Task.Run(() =>
            {
                wpfBase.BusyText = "Finding unmodded candidates...";
                wpfBase.IsBusy = true;
                return GetUnmoddedCandidatesForPackage(wpfBase);
            }).ContinueWithOnUIThread(foundCandidates =>
            {
                wpfBase.IsBusy = false;
                if (!foundCandidates.Result.Any())
                {
                    MessageBox.Show(wpfBase, "Cannot find any candidates for this file!");
                    return;
                }

                var choices = foundCandidates.Result.DiskFiles.ToList(); //make new list
                choices.AddRange(foundCandidates.Result.SFARPackageStreams.Select(x => x.Key));

                var choice = InputComboBoxDialog.GetValue(wpfBase, "Choose file to compare to:",
                    "Unmodified file comparison", choices, choices.Last());
                if (string.IsNullOrEmpty(choice))
                {
                    return;
                }

                if (foundCandidates.Result.DiskFiles.Contains(choice))
                {
                    CompareToPackageWrapper(wpfBase, entryDoubleClickCallback, diskPath: choice);
                }
                else if (foundCandidates.Result.SFARPackageStreams.TryGetValue(choice, out var packageStream))
                {
                    CompareToPackageWrapper(wpfBase, entryDoubleClickCallback, packageStream: packageStream);
                }
                else
                {
                    MessageBox.Show("Selected candidate not found in the lists! This is a bug", "OH NO");
                }
            });
        }

        public static void ComparePackageToAnother(WPFBase wpfBase, Action<EntryStringPair> entryDoubleClickCallback)
        {
            if (wpfBase.Pcc != null)
            {
                string extension = Path.GetExtension(wpfBase.Pcc.FilePath);
                OpenFileDialog d = new OpenFileDialog { Filter = "*" + extension + "|*" + extension, Title = "Select package file to compare against", CustomPlaces = AppDirectories.GameCustomPlaces };
                if (d.ShowDialog() == true)
                {
                    if (wpfBase.Pcc.FilePath == d.FileName)
                    {
                        MessageBox.Show("You selected the same file as the one already open.");
                        return;
                    }

                    SharedPackageTools.CompareToPackageWrapper(wpfBase, entryDoubleClickCallback, diskPath: d.FileName);
                }
            }
        }

        public static bool CanCompareToUnmodded(WPFBase wpfBase) => wpfBase.Pcc != null && wpfBase.Pcc.Game != MEGame.UDK && (!(wpfBase.Pcc.IsInBasegame() || wpfBase.Pcc.IsInOfficialDLC()) || ME3TweaksBackups.GetGameBackupPath(wpfBase.Pcc.Game) != null);

        public static void CompareUnmodded(WPFBase wpfBase, Action<EntryStringPair> entryDoubleClickCallback)
        {
            SharedPackageTools.ComparePackageToUnmodded(wpfBase, entryDoubleClickCallback);
        }

        public static UnmoddedCandidatesLookup GetUnmoddedCandidatesForPackage(WPFBase wpfBase)
        {
            string lookupFilename = Path.GetFileName(wpfBase.Pcc.FilePath);
            string dlcPath = MEDirectories.GetDLCPath(wpfBase.Pcc.Game);
            string backupPath = ME3TweaksBackups.GetGameBackupPath(wpfBase.Pcc.Game);
            var unmoddedCandidates = new UnmoddedCandidatesLookup();

            // Lookup unmodded ON DISK files
            List<string> unModdedFileLookup(string filename)
            {
                List<string> inGameCandidates = MEDirectories.OfficialDLC(wpfBase.Pcc.Game)
                    .Select(dlcName => Path.Combine(dlcPath, dlcName))
                    .Prepend(MEDirectories.GetCookedPath(wpfBase.Pcc.Game))
                    .Where(Directory.Exists)
                    .Select(cookedPath =>
                        Directory.EnumerateFiles(cookedPath, "*", SearchOption.AllDirectories)
                            .FirstOrDefault(path => Path.GetFileName(path) == filename))
                    .NonNull().ToList();

                if (backupPath != null)
                {
                    var backupDlcPath = MEDirectories.GetDLCPath(wpfBase.Pcc.Game, backupPath);
                    inGameCandidates.AddRange(MEDirectories.OfficialDLC(wpfBase.Pcc.Game)
                        .Select(dlcName => Path.Combine(backupDlcPath, dlcName))
                        .Prepend(MEDirectories.GetCookedPath(wpfBase.Pcc.Game, backupPath))
                        .Where(Directory.Exists)
                        .Select(cookedPath =>
                            Directory.EnumerateFiles(cookedPath, "*", SearchOption.AllDirectories)
                                .FirstOrDefault(path => Path.GetFileName(path) == filename))
                        .NonNull());

                    if (wpfBase.Pcc.Game == MEGame.ME3)
                    {
                        // Check TESTPATCH

                    }
                }

                return inGameCandidates;
            }

            unmoddedCandidates.DiskFiles.AddRange(unModdedFileLookup(lookupFilename));
            if (unmoddedCandidates.DiskFiles.IsEmpty())
            {
                //Try to lookup using info in this file
                var packages = wpfBase.Pcc.Exports.Where(x => x.ClassName == "Package" && x.idxLink == 0).ToList();
                foreach (var p in packages)
                {
                    if ((p.PackageFlags & UnrealFlags.EPackageFlags.Cooked) != 0)
                    {
                        //try this one
                        var objName = p.ObjectName;
                        if (p.indexValue > 0) objName += $"_{p.indexValue - 1}"; //Some ME3 map files are indexed
                        var cookedPackageName = objName + (wpfBase.Pcc.Game == MEGame.ME1 ? ".sfm" : ".pcc");
                        unmoddedCandidates.DiskFiles.ReplaceAll(unModdedFileLookup(cookedPackageName)); //ME1 could be upk/u too I guess, but I think only sfm have packages cooked into them
                        break;
                    }
                }
            }

            //if (filecandidates.Any())
            //{
            //    // Use em'
            //    string filePath = InputComboBoxWPF.GetValue(this, "Choose file to compare to:",
            //        "Unmodified file comparison", filecandidates, filecandidates.Last());

            //    if (string.IsNullOrEmpty(filePath))
            //    {
            //        return null;
            //    }

            //    ComparePackage(filePath);
            //    return true;
            //}

            if (wpfBase.Pcc.Game == MEGame.ME3 && backupPath != null)
            {
                var backupDlcPath = Path.Combine(backupPath, "BIOGame", "DLC");
                if (Directory.Exists(dlcPath))
                {
                    var sfars = Directory.GetFiles(backupDlcPath, "*.sfar", SearchOption.AllDirectories).ToList();

                    var testPatch = Path.Combine(backupPath, "BIOGame", "Patches", "PCConsole", "Patch_001.sfar");
                    if (File.Exists(testPatch))
                    {
                        sfars.Add(testPatch);
                    }

                    foreach (var sfar in sfars)
                    {
                        DLCPackage dlc = new DLCPackage(sfar);
                        // Todo: Port in M3's better SFAR lookup code
                        var sfarIndex = dlc.FindFileEntry(Path.GetFileName(lookupFilename));
                        if (sfarIndex >= 0)
                        {
                            var uiName = Path.GetFileName(sfar) == "Patch_001.sfar" ? "TestPatch" : Directory.GetParent(sfar).Parent.Name;
                            unmoddedCandidates.SFARPackageStreams[$"{uiName} SFAR"] = dlc.DecompressEntry(sfarIndex);
                        }
                    }
                }
            }

            return unmoddedCandidates;
        }

        internal class UnmoddedCandidatesLookup
        {
            public List<string> DiskFiles = new();
            public Dictionary<string, Stream> SFARPackageStreams = new();
            public bool Any() => Enumerable.Any(DiskFiles) || Enumerable.Any(SFARPackageStreams);
        }

        /// <summary>
        /// Logic for Package Editor's 'Extract to file' - for sharing with other tools
        /// </summary>
        /// <param name="export"></param>
        /// <param name="setBusy"></param>
        /// <param name="setBusyText"></param>
        /// <param name="entryDoubleClick"></param>
        /// <param name="window"></param>
        public static void ExtractEntryToNewPackage(ExportEntry export, Action<bool> setBusy = null, Action<string> setBusyText = null, Action<EntryStringPair> entryDoubleClick = null, Window window = null)
        {
            // This method is useful if you need to extract a portable asset easily
            // It's very slow
            string fileFilter;
            switch (export.Game)
            {
                case MEGame.ME1:
                    fileFilter = GameFileFilters.ME1SaveFileFilter;
                    break;
                case MEGame.ME2:
                case MEGame.ME3:
                    fileFilter = GameFileFilters.ME3ME2SaveFileFilter;
                    break;
                default:
                    string extension = Path.GetExtension(export.FileRef.FilePath);
                    fileFilter = $"*{extension}|*{extension}";
                    break;
            }

            var d = new SaveFileDialog { Filter = fileFilter };
            if (d.ShowDialog() == true)
            {
                Func<List<EntryStringPair>> PortFunc = () => EntryExporter.ExportExportToFile(export, d.FileName, out _);
                if (File.Exists(d.FileName))
                {
                    var portIntoExistingRes = MessageBox.Show(window, $"Export the selected export ({export.InstancedFullPath}) into the selected file ({d.FileName})? Or port into a new file, overwriting it?\n\nPress Yes to port into the existing file.\nPress No to port as a new file\nPress cancel to abort", "Port into new or existing file?", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    if (portIntoExistingRes == MessageBoxResult.Yes)
                    {
                        PortFunc = () =>
                        {
                            using var package = MEPackageHandler.OpenMEPackage(d.FileName);
                            var results = EntryExporter.ExportExportToPackage(export, package, out _);
                            package.Save();
                            return results;
                        };
                    }
                    else if (portIntoExistingRes == MessageBoxResult.Cancel)
                    {
                        return;
                    } // No condition changes nothing
                }
                Task.Run(() => PortFunc.Invoke())
                    .ContinueWithOnUIThread(results =>
                        {
                            setBusy?.Invoke(false);
                            var result = results.Result;
                            if (result.Any())
                            {
                                MessageBox.Show("Extraction completed with issues.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                                var ld = new ListDialog(result, "Extraction issues", "The following issues were detected while extracting to a new file", window);
                                ld.DoubleClickEntryHandler = entryDoubleClick;
                                ld.Show();
                            }
                            else
                            {
                                MessageBox.Show("Extracted into a new package.");
                                var nwpf = new PackageEditorWindow();
                                nwpf.LoadFile(d.FileName);
                                nwpf.Show();
                                nwpf.Activate();
                            }

                        }
                    );
                setBusyText?.Invoke("Exporting to new package");
                setBusy?.Invoke(true);
            }
        }
    }
}
