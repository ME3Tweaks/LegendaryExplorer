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
using LegendaryExplorer.Tools.PackageEditor;
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

namespace LegendaryExplorer.Tools.InterpEditor.InterpExperiments
{
    /// <summary>
    /// Class for Exkywor's preset buttons and stuff
    /// </summary>
    class InterpEditorExperimentsE
    {
        public static void AddPresetGroup(string preset, InterpEditorWindow iew)
        {
            var currExp = iew.Properties_InterpreterWPF.CurrentLoadedExport;

            if (currExp != null)
            {

                if (currExp.ClassName != "InterpData")
                {
                    MessageBox.Show("InterpData not selected.", "Warning", MessageBoxButton.OK);
                    return;
                }

                switch (preset)
                {
                    case "Director":
                        MatineeHelper.AddPresetDirectorGroup(currExp);
                        break;

                    case "Camera":
                        if (PromptDialog.Prompt(null, "Name of camera actor:") is string camName)
                        {
                            if (string.IsNullOrEmpty(camName))
                            {
                                MessageBox.Show("Not a valid camera actor name.", "Warning", MessageBoxButton.OK);
                                return;
                            }
                            MatineeHelper.AddPresetCameraGroup(currExp, camName);
                        }
                        break;
                }
            }
            return;
        }

        public static void AddPresetTrack(string preset, InterpEditorWindow iew)
        {
            var currExp = iew.Properties_InterpreterWPF.CurrentLoadedExport;

            if (currExp != null)
            {
                if (currExp.ClassName != "InterpGroup")
                {
                    MessageBox.Show("InterpGroup not selected.", "Warning", MessageBoxButton.OK);
                    return;
                }

                switch (preset)
                {
                    case "Gesture":
                        if (PromptDialog.Prompt(null, "Name of gesture actor:") is string actor)
                        {
                            if (string.IsNullOrEmpty(actor))
                            {
                                MessageBox.Show("Not a valid gesture actor name.", "Warning", MessageBoxButton.OK);
                                return;
                            }
                            MatineeHelper.AddPresetGestureTrack(currExp, actor);
                        }
                        break;
                }
            }
            return;
        }
    }
}
