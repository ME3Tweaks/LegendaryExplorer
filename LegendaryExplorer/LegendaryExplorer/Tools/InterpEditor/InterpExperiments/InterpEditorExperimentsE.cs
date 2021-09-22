using System.Windows;
using LegendaryExplorer.Dialogs;
using LegendaryExplorerCore.Matinee;

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
