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
                        MatineeHelper.AddPreset("Director", currExp);
                        break;

                    case "Camera":
                        var actor = promptForActor("Name of camera actor:", "Not a valid camera actor name.");
                        if (!string.IsNullOrEmpty(actor))
                        {
                            MatineeHelper.AddPreset("Camera", currExp, actor);
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
                        var actor = promptForActor("Name of gesture actor:", "Not a valid gesture actor name.");
                        if (!string.IsNullOrEmpty(actor))
                        {
                            MatineeHelper.AddPreset("Gesture", currExp, actor);
                        }
                        break;
                }
            }
            return;
        }

        private static string promptForActor(string msg, string err)
        {
            if (PromptDialog.Prompt(null, msg) is string actor)
            {
                if (string.IsNullOrEmpty(actor))
                {
                    MessageBox.Show(err, "Warning", MessageBoxButton.OK);
                    return null;
                }
                return actor;
            }
            return null;
        }
    }
}
