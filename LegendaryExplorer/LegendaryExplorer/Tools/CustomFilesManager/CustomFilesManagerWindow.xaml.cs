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

        private void RemoveSelectedStartupFile()
        {
            var selected = SelectedStartupFile;
            CustomStartupFiles.Remove(selected);

            // TODO: COMMIT SETTING
        }

        private void AddCustomClassDirectory()
        {
            CommonOpenFileDialog ofd = new CommonOpenFileDialog();
            ofd.IsFolderPicker = true;
            ofd.EnsurePathExists = true;
            var result = ofd.ShowDialog();
            if (result == CommonFileDialogResult.Ok)
            {
                if (CustomClassDirectories.Contains(ofd.FileName))
                {
                    CustomClassDirectories.Add(ofd.FileName);

                    // TODO: COMMIT SETTING
                }
            }
        }


        private void RemoveCustomClassDirectory()
        {
            var selected = SelectedCustomClassDirectory;
            CustomClassDirectories.Remove(selected);

            // TODO: COMMIT SETTING
        }

        private void AddStartupFile()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            var result = ofd.ShowDialog();
            if (result.HasValue && result.Value)
            {
                if (!CustomStartupFiles.Any(x => x.FilePath.CaseInsensitiveEquals(ofd.FileName)))
                {
                    var p = MEPackageHandler.QuickOpenMEPackage(ofd.FileName);
                    EntryImporter.AddUserSafeToImportFromFile(p.Game, ofd.FileName);
                    CustomStartupFiles.Add(new CustomStartupFileInfo() { Game = p.Game, FilePath = ofd.FileName });

                    // TODO: COMMIT SETTING FOR PERSISTENCE
                }
            }
        }

        public GenericCommand RemoveStartupFileCommand { get; set; }
        public GenericCommand AddStartupFileCommand { get; set; }
        public GenericCommand RemoveCustomDirectoryCommand { get; set; }
        public GenericCommand AddCustomDirectoryCommand { get; set; }

        private bool StartupFileIsSelected()
        {
            return SelectedStartupFile != null;
        }
    }
}
