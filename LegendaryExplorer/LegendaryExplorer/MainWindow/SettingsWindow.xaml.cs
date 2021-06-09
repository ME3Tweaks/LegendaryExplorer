using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using Microsoft.WindowsAPICodePack.Dialogs;

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