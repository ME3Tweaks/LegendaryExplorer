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
using LegendaryExplorer.Tools.InterpEditor;
using LegendaryExplorer.Tools.InterpEditor.InterpExperiments;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.UserControls.ExportLoaderControls;
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

namespace LegendaryExplorer.UserControls.InterpEditorControls
{
    /// <summary>
    /// Class that holds toolset development experiments. Actual experiment code should be in the Experiments classes
    /// </summary>
    public partial class InterpExperimentsMenuControl : MenuItem
    {
        public InterpExperimentsMenuControl()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands() { }

        public InterpEditorWindow GetIEWindow()
        {
            if (Window.GetWindow(this) is InterpEditorWindow iew)
            {
                return iew;
            }

            return null;
        }

        // EXPERIMENTS: EXKYWOR------------------------------------------------------------
        #region Exkywor's experiments
        private void AddPresetDirectorGroup_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.AddPresetGroup("Director", GetIEWindow());
        }

        private void AddPresetCameraGroup_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.AddPresetGroup("Camera", GetIEWindow());
        }

        private void AddPresetGestureTrack_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.AddPresetTrack("Gesture", GetIEWindow());
        }
        #endregion
    }
}
