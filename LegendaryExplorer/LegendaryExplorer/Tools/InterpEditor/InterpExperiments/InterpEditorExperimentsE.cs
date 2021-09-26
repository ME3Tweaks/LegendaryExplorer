using System.Windows;
using LegendaryExplorer.Dialogs;
using LegendaryExplorerCore.Matinee;
using LegendaryExplorerCore.Packages;

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

            if (currExp != null && iew.Pcc != null)
            {
                var game = iew.Pcc.Game;

                if (!(game.IsGame3() || game.IsGame2()))
                {
                    MessageBox.Show("This experiment is not available for ME1/LE1 files.", "Warning", MessageBoxButton.OK);
                    return;
                }

                if (currExp.ClassName != "InterpData")
                {
                    MessageBox.Show("InterpData not selected.", "Warning", MessageBoxButton.OK);
                    return;
                }

                switch (preset)
                {
                    case "Director":
                        MatineeHelper.AddPreset(preset, currExp, game);
                        break;

                    case "Camera":
                        var camActor = promptForActor("Name of camera actor:", "Not a valid camera actor name.");
                        if (!string.IsNullOrEmpty(camActor))
                        {
                            MatineeHelper.AddPreset(preset, currExp, game, camActor);
                        }
                        break;

                    case "Actor":
                        var actActor = promptForActor("Name of actor:", "Not a valid actor name.");
                        if (!string.IsNullOrEmpty(actActor))
                        {
                            MatineeHelper.AddPreset(preset, currExp, game, actActor);
                        }
                        break;
                }
            }
            return;
        }

        public static void AddPresetTrack(string preset, InterpEditorWindow iew)
        {
            var currExp = iew.Properties_InterpreterWPF.CurrentLoadedExport;

            if (currExp != null && iew.Pcc != null)
            {
                var game = iew.Pcc.Game;

                if (!(game.IsGame3() || game.IsGame2()))
                {
                    MessageBox.Show("This experiment is not available for ME1/LE1 files.", "Warning", MessageBoxButton.OK);
                    return;
                }

                if (currExp.ClassName != "InterpGroup")
                {
                    MessageBox.Show("InterpGroup not selected.", "Warning", MessageBoxButton.OK);
                    return;
                }

                switch (preset)
                {
                    case "Gesture":
                    case "Gesture2":
                        var actor = promptForActor("Name of gesture actor:", "Not a valid gesture actor name.");
                        if (!string.IsNullOrEmpty(actor))
                        {
                            MatineeHelper.AddPreset(preset, currExp, game, actor);
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
