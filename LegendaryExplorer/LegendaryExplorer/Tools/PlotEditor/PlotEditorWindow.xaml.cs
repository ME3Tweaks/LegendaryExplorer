using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Gammtek.Conduit.MassEffect3.SFXGame.CodexMap;
using Gammtek.Conduit.MassEffect3.SFXGame.QuestMap;
using Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.ToolsetDev.MemoryAnalyzer;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.UserControls.SharedToolControls;
using LegendaryExplorerCore.Packages;
using Microsoft.Win32;

namespace LegendaryExplorer.Tools.PlotEditor
{
    public partial class PlotEditorWindow : WPFBase, IRecents
    {
        public PlotEditorWindow() : base("Plot Editor")
        {
            GotoCommand = new GenericCommand(FocusGoto, () => Pcc != null);

            InitializeComponent();
            RecentsController.InitRecentControl(Toolname, Recents_MenuItem, LoadFile);

            FindObjectUsagesControl.parentRef = this;
        }

        public string CurrentFile => Pcc != null ? Path.GetFileName(Pcc.FilePath) : "Select a file to load";

        public ICommand GotoCommand { get; set; }

        public void OpenFile()
        {
            var dlg = AppDirectories.GetOpenPackageDialog();

            if (dlg.ShowDialog() != true)
            {
                return;
            }

            LoadFile(dlg.FileName);

        }

        public void LoadFile(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (!File.Exists(path))
            {
                return;
            }

            LoadMEPackage(path);

            CodexMapControl?.Open(Pcc);

            QuestMapControl?.Open(Pcc);

            StateEventMapControl?.Open(Pcc);

            ConsequenceMapControl?.Open(Pcc, "ConsequenceMap");

            RecentsController.AddRecent(path, false, Pcc?.Game);
            RecentsController.SaveRecentList(true);
            Title = $"Plot Editor - {path}";
            OnPropertyChanged(nameof(CurrentFile));

            //Hiding "Recents" panel
            if (MainTabControl.SelectedIndex == 0)
            {
                MainTabControl.SelectedIndex = 1;
            }
        }

        public async void SaveFile(string filepath = null)
        {
            if (Pcc == null)
            {
                return;
            }

            if (CodexMapControl != null)
            {

                if (CodexMapView.TryFindCodexMap(Pcc, out ExportEntry export, out int _))
                {
                    using var stream = new MemoryStream();
                    var codexMap = CodexMapControl.ToCodexMap();
                    var binaryCodexMap = new BinaryBioCodexMap(codexMap.Sections, codexMap.Pages);

                    binaryCodexMap.Save(stream);

                    export.WriteBinary(stream.ToArray());
                }
            }

            if (QuestMapControl != null)
            {

                if (QuestMapControl.TryFindQuestMap(Pcc, out ExportEntry export, out int _))
                {
                    using var stream = new MemoryStream();
                    var questMap = QuestMapControl.ToQuestMap();
                    var binaryQuestMap = new BinaryBioQuestMap(questMap.Quests, questMap.BoolTaskEvals, questMap.IntTaskEvals, questMap.FloatTaskEvals);

                    binaryQuestMap.Save(stream);

                    export.WriteBinary(stream.ToArray());
                }
            }

            if (StateEventMapControl != null)
            {

                if (StateEventMapView.TryFindStateEventMap(Pcc, out ExportEntry export))
                {
                    using var stream = new MemoryStream();
                    var stateEventMap = StateEventMapControl.ToStateEventMap();
                    var binaryStateEventMap = new BinaryBioStateEventMap(stateEventMap.StateEvents);

                    binaryStateEventMap.Save(stream, Pcc.Game);

                    export.WriteBinary(stream.ToArray());
                }
            }

            if (ConsequenceMapControl != null)
            {

                if (StateEventMapView.TryFindStateEventMap(Pcc, out ExportEntry export, "ConsequenceMap"))
                {
                    using var stream = new MemoryStream();
                    var consequenceMap = ConsequenceMapControl.ToStateEventMap();
                    var binaryConsequenceMap = new BinaryBioStateEventMap(consequenceMap.StateEvents);

                    binaryConsequenceMap.Save(stream, Pcc.Game);

                    export.WriteBinary(stream.ToArray());
                }
            }

            filepath ??= Pcc.FilePath;

            await Pcc.SaveAsync(filepath);
        }

        public void SaveFileAs()
        {
            var dlg = new SaveFileDialog { Filter = "Support files|*.pcc;*.upk" };

            if (dlg.ShowDialog() != true)
            {
                return;
            }

            SaveFile(dlg.FileName);
        }

        public override void HandleUpdate(List<PackageUpdate> updates)
        {
            //TODO: implement handleUpdate
        }

        private void Save_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Pcc != null;
        }

        private void Save_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFile();
        }

        private void SaveAs_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFileAs();
        }

        private void Open_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFile();
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string ext = Path.GetExtension(files[0]).ToLower();
                if (ext != ".upk" && ext != ".pcc")
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string ext = Path.GetExtension(files[0]).ToLower();
                if (ext == ".upk" || ext == ".pcc")
                {
                    LoadFile(files[0]);
                }
            }
        }

        public void GoToCodex(int id)
        {
            var targetCodex = CodexMapControl.CodexPages.FirstOrDefault(kvp => kvp.Key == id);
            if (!targetCodex.Equals(default(KeyValuePair<int, BioCodexPage>)))
            {
                MainTabControl.SelectedValue = CodexMapControl;
                CodexMapControl.GoToCodexPage(targetCodex);
            }
        }

        public void GoToQuest(int id)
        {
            var targetQuest = QuestMapControl.Quests.FirstOrDefault(kvp => kvp.Key == id);
            if (!targetQuest.Equals(default(KeyValuePair<int, BioQuest>)))
            {
                MainTabControl.SelectedValue = QuestMapControl;
                QuestMapControl.GoToQuest(targetQuest);
            }
        }

        public void GoToStateEvent(int id)
        {
            var targetEvent = StateEventMapControl.StateEvents.FirstOrDefault(kvp => kvp.Key == id);

            // If the ID is the default, try the consequence map
            if (targetEvent.Equals(default(KeyValuePair<int, BioStateEvent>)))
            {
                targetEvent = ConsequenceMapControl.StateEvents.FirstOrDefault(kvp => kvp.Key == id);
            }

            GoToStateEvent(targetEvent);
        }

        public void GoToStateEvent(KeyValuePair<int, BioStateEvent> targetEvent)
        {
            if ((bool)ConsequenceMapControl?.StateEvents.Contains(targetEvent))
            {
                MainTabControl.SelectedValue = ConsequenceMapControl;
                ConsequenceMapControl.SelectStateEvent(targetEvent);
            }
            else
            {
                MainTabControl.SelectedValue = StateEventMapControl;
                StateEventMapControl.SelectStateEvent(targetEvent);
            }
        }

        public void PropogateRecentsChange(string propogationSource, IEnumerable<RecentsControl.RecentItem> newRecents)
        {
            RecentsController.PropogateRecentsChange(false, newRecents);
        }

        private void FocusGoto()
        {
            Goto_TextBox.Focus();
            Goto_TextBox.SelectAll();
        }

        private void GotoButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(Goto_TextBox.Text, out int n))
            {
                GoToStateEvent(n);
            }
        }
        private void Goto_TextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return && !e.IsRepeat)
            {
                GotoButton_Click(null, null);
            }
        }

        public string Toolname => "NativesEditor";

        private void PlotEditorWindow_OnClosing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
            {
                return;
            }
            RecentsController?.Dispose();
        }
    }
}
