using System;
using LegendaryExplorer.Misc.Themes;
using System.Windows;
using System.Windows.Controls;

namespace LegendaryExplorer.Tools.InterpEditor.InterpExperiments
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
            Loaded += (_, _) => ApplyMenuItemTheme();
            ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;
            Unloaded += (_, _) => ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
        }

        private void ThemeManager_ThemeChanged(object sender, EventArgs e) => ApplyMenuItemTheme();

        // MenuItem subclasses don't reliably pick up the implicit {x:Type MenuItem} theme Style via normal
        // DefaultStyleKey inheritance (same unexplained issue seen with ListBoxScroll : ListBox elsewhere in
        // this app), so it has to be applied explicitly - TryFindResource does a live runtime lookup (unlike
        // BasedOn/StaticResource), so this correctly finds the theme dictionaries merged into the window's
        // resources whether or not dark mode is currently on. Must run on Loaded, not the constructor - the
        // constructor runs while the parent window's own InitializeComponent() is still executing, before
        // ThemeManager has merged the theme dictionaries into the window's resources on its own Loaded, so
        // TryFindResource would find nothing that early.
        private void ApplyMenuItemTheme()
        {
            Style = TryFindResource(typeof(MenuItem)) as Style;
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
        private void InsertTrackMoveKey_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.InsertTrackMoveKeyExperiment(GetIEWindow());
        }
        private void DeleteTrackMoveKey_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.DeleteTrackMoveKey(GetIEWindow());
        }
        private void InsertDOFKey_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.InsertDOFKey(GetIEWindow());
        }
        private void DeleteDOFKey_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.DeleteDOFKey(GetIEWindow());
        }
        private void InsertGestureKey_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.InsertGestureKey(GetIEWindow());
        }
        private void DeleteGestureKey_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.DeleteGestureKey(GetIEWindow());
        }

        private void AddPresetDirectorGroup_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.AddPresetGroup("Director", GetIEWindow());
        }

        private void AddPresetCameraGroup_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.AddPresetGroup("Camera", GetIEWindow());
        }
        private void AddPresetCameraGroupWithKeys_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.AddPresetCameraWithKeys(GetIEWindow());
        }

        private void AddPresetActorGroup_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.AddPresetGroup("Actor", GetIEWindow());
        }

        private void AddPresetGestureTrack_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.AddPresetTrack("Gesture", GetIEWindow());
        }

        private void AddPresetGestureTrack2_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.AddPresetTrack("Gesture2", GetIEWindow());
        }

        private void SetStartingPose_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.SetStartingPose(GetIEWindow());
        } 
        #endregion

        // EXPERIMENTS: HenBagle------------------------------------------------------------
        #region HenBagle's Experiments
        private void OpenFovoLineAudio_Click(object sender, RoutedEventArgs e)
        {
            bool isMale = (string) (sender as FrameworkElement)?.Tag == "M";
            InterpEditorExperimentsH.OpenFovoLineAudio(isMale, GetIEWindow());
        }

        private void OpenFovoLineFXA_Click(object sender, RoutedEventArgs e)
        {
            bool isMale = (string) (sender as FrameworkElement)?.Tag == "M";
            InterpEditorExperimentsH.OpenFovoLineFXA(isMale, GetIEWindow());
        }

        private void OpenFovoLineDlg_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsH.OpenFovoLineDialogueEditor(GetIEWindow());
        }
        #endregion
    }
}
