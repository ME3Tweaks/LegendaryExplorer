using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using LegendaryExplorer.Misc.AppSettings;
using System.Diagnostics;
using System.IO;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System.Security.Cryptography;
using System.Windows.Controls;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.PackageEditor;

namespace LegendaryExplorer.Tools.CustomFilesManager
{
    public class CustomStartupFileInfo
    {
        /// <summary>
        /// Game this startup file is registered for
        /// </summary>
        public MEGame Game { get; set; }

        /// <summary>
        /// The filepath of the startup file
        /// </summary>
        public string FilePath { get; set; }

        protected bool Equals(CustomStartupFileInfo other)
        {
            return Game == other.Game && FilePath == other.FilePath;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((CustomStartupFileInfo)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Game, FilePath);
        }
    }


    /// <summary>
    /// Interaction logic for SafeToImportFromEditorWindow.xaml
    /// </summary>
    public partial class CustomFilesManagerWindow : TrackingNotifyPropertyChangedWindowBase
    {
        // TODO: Probably need a custom Game+String pair for startup files so we can add them to specific games without having to open the package to
        // know which file the game was for (for when we remove it).

        /// <summary>
        /// This editor's list of custom startup files.
        /// </summary>
        public ObservableCollectionExtended<CustomStartupFileInfo> CustomStartupFiles { get; } = new();
        /// <summary>
        /// This editor's list of custom class directories.
        /// </summary>
        public ObservableCollectionExtended<string> CustomClassDirectories { get; } = new();

        private CustomStartupFileInfo _selectedStartupFile;
        private string _selectedCustomClassDirectory;

        public CustomStartupFileInfo SelectedStartupFile
        {
            get => _selectedStartupFile;
            set => SetProperty(ref _selectedStartupFile, value);
        }

        public string SelectedCustomClassDirectory
        {
            get => _selectedCustomClassDirectory;
            set => SetProperty(ref _selectedCustomClassDirectory, value);
        }


        public CustomFilesManagerWindow() : base("LEX Custom Files Manager", true)
        {
            LoadCommands();

            CustomClassDirectories.ReplaceAll(Settings.CustomClassDirectories);

            foreach (var sf in Settings.CustomStartupFiles)
            {
                if (File.Exists(sf))
                {
                    using var p = MEPackageHandler.QuickOpenMEPackage(sf);
                    CustomStartupFiles.Add(new CustomStartupFileInfo() { FilePath = sf, Game = p.Game });
                }
            }

            InitializeComponent();
        }

        private void LoadCommands()
        {
            // Column 0
            RemoveStartupFileCommand = new GenericCommand(RemoveSelectedStartupFile, StartupFileIsSelected);
            AddStartupFileCommand = new GenericCommand(AddStartupFile, () => true);

            // Column 1
            RemoveCustomDirectoryCommand = new GenericCommand(RemoveCustomClassDirectory, () => true);
            AddCustomDirectoryCommand = new GenericCommand(AddCustomClassDirectory, () => true);
        }

        #region STARTUP FILES
        private void AddStartupFile()
        {
            var ofd = AppDirectories.GetOpenPackageDialog();
            if (ofd.ShowDialog() == true)
            {
                if (!CustomStartupFiles.Any(x => x.FilePath.CaseInsensitiveEquals(ofd.FileName)))
                {
                    var p = MEPackageHandler.QuickOpenMEPackage(ofd.FileName);
                    EntryImporter.AddUserSafeToImportFromFile(p.Game, ofd.FileName);
                    CustomStartupFiles.Add(new CustomStartupFileInfo() { Game = p.Game, FilePath = ofd.FileName });

                    // Persist the setting for next boot
                    Settings.CustomStartupFiles = CustomStartupFiles.Select(x => x.FilePath).ToList();
                    Settings.Save();
                }
            }
        }

        private void RemoveSelectedStartupFile()
        {
            var selected = SelectedStartupFile;
            CustomStartupFiles.Remove(selected);

            EntryImporter.RemoveSafeToImportFromFile(selected.Game, selected.FilePath);

            // Persist the setting for next boot
            Settings.CustomStartupFiles = CustomStartupFiles.Select(x => x.FilePath).ToList();
            Settings.Save();
        }
        #endregion

        #region CUSTOM CLASSES
        private void AddCustomClassDirectory()
        {
            CommonOpenFileDialog ofd = new CommonOpenFileDialog();
            ofd.IsFolderPicker = true;
            ofd.EnsurePathExists = true;
            var result = ofd.ShowDialog();
            if (result == CommonFileDialogResult.Ok)
            {
                if (!CustomClassDirectories.Contains(ofd.FileName))
                {
                    CustomClassDirectories.Add(ofd.FileName);

                    // Persist the setting for next boot
                    Settings.CustomClassDirectories = CustomClassDirectories.ToList();
                    Settings.Save();
                }
            }
        }


        private void RemoveCustomClassDirectory()
        {
            var selected = SelectedCustomClassDirectory;
            CustomClassDirectories.Remove(selected);

            // Persist the setting for next boot
            Settings.CustomClassDirectories = CustomClassDirectories.ToList();
            Settings.Save();
        }
        #endregion

        public GenericCommand RemoveStartupFileCommand { get; set; }
        public GenericCommand AddStartupFileCommand { get; set; }
        public GenericCommand RemoveCustomDirectoryCommand { get; set; }
        public GenericCommand AddCustomDirectoryCommand { get; set; }

        private bool StartupFileIsSelected()
        {
            return SelectedStartupFile != null;
        }

        /// <summary>
        /// Inven
        /// </summary>
        internal static void InventoryCustomClassDirectories()
        {
            foreach (var dir in Settings.CustomClassDirectories)
            {
                try
                {
                    if (Directory.Exists(dir))
                    {
                        var files = Directory.GetFiles(dir).Where(x => x.RepresentsPackageFilePath());
                        foreach (var f in files)
                        {
                            try
                            {
                                using var p = MEPackageHandler.OpenMEPackage(f, forceLoadFromDisk: true);
                                if (p.Platform != MEPackage.GamePlatform.PC || (!p.Game.IsOTGame() && !p.Game.IsLEGame()))
                                    continue; // Do not inventory

                                foreach (var e in p.Exports.Where(x => x.IsClass))
                                {
                                    if (GlobalUnrealObjectInfo.GetClasses(p.Game).ContainsKey(e.ObjectName.Name))
                                        continue; // This class is already inventoried

                                    Debug.WriteLine($@"Inventorying {e.InstancedFullPath}");
                                    var classInfo = GlobalUnrealObjectInfo.generateClassInfo(e);
                                    GlobalUnrealObjectInfo.InstallCustomClassInfo(e.ObjectName, classInfo, e.Game);
                                    if (e.InheritsFrom("SequenceObject"))
                                    {
                                        // Add to Kismet library
                                        var defaults = p.GetUExport(ObjectBinary.From<UClass>(e).Defaults);
                                        GlobalUnrealObjectInfo.GenerateSequenceObjectInfoForClassDefaults(defaults);
                                    }
                                }
                            }
                            catch (Exception ie)
                            {
                                Debug.WriteLine($@"Failed to inventory directory {f}: {ie.Message}");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($@"Failed to inventory directory {dir}: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Installs the list of files into the toolset for use
        /// </summary>
        internal static void InstallCustomStartupFiles()
        {
            foreach (var v in Settings.CustomStartupFiles)
            {
                if (v != null && File.Exists(v) && v.RepresentsPackageFilePath())
                {
                    var p = MEPackageHandler.QuickOpenMEPackage(v);
                    EntryImporter.AddUserSafeToImportFromFile(p.Game, v);
                }
            }
        }

        private void CustomStartupFile_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Debug.WriteLine(e.ClickCount);
            if (e.ClickCount == 2 && sender is StackPanel sp && sp.DataContext is CustomStartupFileInfo csfi)
            {
                if (File.Exists(csfi.FilePath))
                {
                    var pe = new PackageEditorWindow();
                    pe.Show();
                    pe.LoadFile(csfi.FilePath);
                    pe.Activate();
                }
            }
        }
    }
}
