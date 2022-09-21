using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using DocumentFormat.OpenXml.Drawing;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorerCore;
using LegendaryExplorerCore.GameFilesystem;
using Microsoft.WindowsAPICodePack.Dialogs;
using Path = System.IO.Path;

namespace LegendaryExplorer.MainWindow
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handles clicking on 'Browse' button for a directory text box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DirectoryBrowse_Click(object sender, RoutedEventArgs e)
        {
            // Find the sibling textbox (this is a generic function for all potential browse buttons)
            var browseButton = sender as Button;
            foreach (var objChild in LogicalTreeHelper.GetChildren(browseButton.Parent))
            {
                if (objChild is TextBox t)
                {
                    var dlg = new CommonOpenFileDialog("Select folder") { IsFolderPicker = true };
                    if (dlg.ShowDialog(this) != CommonFileDialogResult.Ok) { return; }
                    t.Text = dlg.FileName;
                }
            }
        }

        /// <summary>
        /// Handles clicking on 'Browse' button for a file selection text box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileBrowse_Click(object sender, RoutedEventArgs e)
        {
            // Find the sibling textbox (this is a generic function for all potential browse buttons)
            var browseButton = sender as Button;
            foreach (var objChild in LogicalTreeHelper.GetChildren(browseButton.Parent))
            {
                if (objChild is TextBox t)
                {
                    var dlg = new CommonOpenFileDialog("Select file")
                        {Filters = {new CommonFileDialogFilter("", browseButton.Tag.ToString() ?? "")}};
                    if (dlg.ShowDialog(this) != CommonFileDialogResult.Ok) { return; }
                    t.Text = dlg.FileName;
                }
            }
        }

        private void Setting_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            // Handle setting of game paths
            var t = sender as TextBox;
            if (t.Parent is StackPanel parentPanel && !string.IsNullOrEmpty(parentPanel.Tag.ToString()))
            {
                switch (parentPanel.Tag.ToString())
                {
                    case "Global_ME1Directory" when ME1Directory.IsValidGameDir(t.Text):
                        LegendaryExplorerCoreLibSettings.Instance.ME1Directory = t.Text;
                        ME1Directory.DefaultGamePath = t.Text;
                        break;
                    case "Global_ME2Directory" when ME2Directory.IsValidGameDir(t.Text):
                        LegendaryExplorerCoreLibSettings.Instance.ME2Directory = t.Text;
                        ME2Directory.DefaultGamePath = t.Text;
                        break;
                    case "Global_ME3Directory" when ME3Directory.IsValidGameDir(t.Text):
                        LegendaryExplorerCoreLibSettings.Instance.ME3Directory = t.Text;
                        ME3Directory.DefaultGamePath = t.Text;
                        break;
                    case "Global_LEDirectory" when LEDirectory.IsValidGameDir(t.Text):
                        LegendaryExplorerCoreLibSettings.Instance.LEDirectory = t.Text;
                        LE1Directory.ReloadDefaultGamePath();
                        LE2Directory.ReloadDefaultGamePath();
                        LE3Directory.ReloadDefaultGamePath();
                        break;
                    case nameof(Settings.Global_UDKCustomDirectory):
                        if (UDKDirectory.IsValidGameDir(t.Text))
                        {
                            LegendaryExplorerCoreLibSettings.Instance.UDKCustomDirectory = t.Text;
                            UDKDirectory.ReloadDefaultGamePath();
                            break;
                        }
                        var rootPath = Path.GetDirectoryName(t.Text);
                        if (UDKDirectory.IsValidGameDir(rootPath))
                        {
                            LegendaryExplorerCoreLibSettings.Instance.UDKCustomDirectory = rootPath;
                            UDKDirectory.ReloadDefaultGamePath();
                        }
                        break;
                }
            }
        }

        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            Settings.Save();
        }

        private void AssociatePCCSFM_Click(object sender, RoutedEventArgs e)
        {
            FileAssociations.AssociatePCCSFM();
        }

        private void AssociateUPKUDK_Click(object sender, RoutedEventArgs e)
        {
            FileAssociations.AssociateUPKUDK();
        }

        private void AssociateOthers_Click(object sender, RoutedEventArgs e)
        {
            FileAssociations.AssociateOthers();
        }

    }
}