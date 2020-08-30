using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Gammtek.Conduit.Extensions;
using Gammtek.Conduit.MassEffect3.SFXGame.CodexMap;
using Gammtek.Conduit.MassEffect3.SFXGame.QuestMap;
using Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap;
using ME3Explorer;
using ME3ExplorerCore.Packages;
using Microsoft.AppCenter.Analytics;
using Microsoft.Win32;

namespace MassEffect.NativesEditor.Views
{
    public partial class PlotEditor : WPFBase
    {
        public PlotEditor()
        {
            ME3Explorer.ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Plot Editor", new WeakReference(this));
            Analytics.TrackEvent("Used tool", new Dictionary<string, string>()
            {
                { "Toolname", "Plot Editor" }
            });
            InitializeComponent();
            LoadRecentList();
            RefreshRecent(false);
            FindObjectUsagesControl.parentRef = this;
        }

        public string CurrentFile => Pcc != null ? Path.GetFileName(Pcc.FilePath) : "Select a file to load";

        public void OpenFile()
        {
            var dlg = new OpenFileDialog { Filter = "Support files|*.pcc;*.upk", Multiselect = false };

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

            AddRecent(path, false);
            SaveRecentList();
            RefreshRecent(true, RFiles);
            Title = $"Plot Editor - {path}";
            OnPropertyChanged(nameof(CurrentFile));

            //Hiding "Recents" panel
            if (MainTabControl.SelectedIndex == 0)
            {
                MainTabControl.SelectedIndex = 1; 
            }
        }

        public void SaveFile(string filepath = null)
        {
            if (Pcc == null)
            {
                return;
            }

            if (CodexMapControl != null)
            {

                if (CodexMapView.TryFindCodexMap(Pcc, out ExportEntry export, out int _))
                {
                    using (var stream = new MemoryStream())
                    {
                        var codexMap = CodexMapControl.ToCodexMap();
                        var binaryCodexMap = new BinaryBioCodexMap(codexMap.Sections, codexMap.Pages);

                        binaryCodexMap.Save(stream);

                        export.SetBinaryData(stream.ToArray());
                    }
                }
            }

            if (QuestMapControl != null)
            {

                if (QuestMapControl.TryFindQuestMap(Pcc, out ExportEntry export, out int _))
                {
                    using (var stream = new MemoryStream())
                    {
                        var questMap = QuestMapControl.ToQuestMap();
                        var binaryQuestMap = new BinaryBioQuestMap(questMap.Quests, questMap.BoolTaskEvals, questMap.IntTaskEvals, questMap.FloatTaskEvals);

                        binaryQuestMap.Save(stream);

                        export.SetBinaryData(stream.ToArray());
                    }
                }
            }

            if (StateEventMapControl != null)
            {

                if (StateEventMapView.TryFindStateEventMap(Pcc, out ExportEntry export))
                {
                    using (var stream = new MemoryStream())
                    {
                        var stateEventMap = StateEventMapControl.ToStateEventMap();
                        var binaryStateEventMap = new BinaryBioStateEventMap(stateEventMap.StateEvents);

                        binaryStateEventMap.Save(stream);

                        export.SetBinaryData(stream.ToArray());
                    }
                }
            }

            if (filepath == null)
                filepath = Pcc.FilePath;

            Pcc.Save(filepath);
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

        public override void handleUpdate(List<PackageUpdate> updates)
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

        #region Recents
        private readonly List<Button> RecentButtons = new List<Button>();
        public List<string> RFiles;
        public static readonly string NativesEditorDataFolder = Path.Combine(App.AppDataFolder, @"NativesEditor\");
        private readonly string RECENTFILES_FILE = "RECENTFILES";

        private void LoadRecentList()
        {
            RecentButtons.AddRange(new[] { RecentButton1, RecentButton2, RecentButton3, RecentButton4, RecentButton5, RecentButton6, RecentButton7, RecentButton8, RecentButton9, RecentButton10 });
            Recents_MenuItem.IsEnabled = false;
            RFiles = new List<string>();
            RFiles.Clear();
            string path = NativesEditorDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
            {
                string[] recents = File.ReadAllLines(path);
                foreach (string recent in recents)
                {
                    if (File.Exists(recent))
                    {
                        AddRecent(recent, true);
                    }
                }
            }
        }

        private void SaveRecentList()
        {
            if (!Directory.Exists(NativesEditorDataFolder))
            {
                Directory.CreateDirectory(NativesEditorDataFolder);
            }
            string path = NativesEditorDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
                File.Delete(path);
            File.WriteAllLines(path, RFiles);
        }

        public void RefreshRecent(bool propogate, List<string> recents = null)
        {
            if (propogate && recents != null)
            {
                //we are posting an update to other instances of PathEd
                foreach (var form in Application.Current.Windows)
                {
                    if (form is PlotEditor wpf && this != wpf)
                    {
                        wpf.RefreshRecent(false, RFiles);
                    }
                }
            }
            else if (recents != null)
            {
                //we are receiving an update
                RFiles = new List<string>(recents);
            }
            Recents_MenuItem.Items.Clear();
            if (RFiles.Count <= 0)
            {
                Recents_MenuItem.IsEnabled = false;
                return;
            }
            Recents_MenuItem.IsEnabled = true;

            int i = 0;
            foreach ((string filepath, Button recentButton) in RFiles.ZipTuple(RecentButtons))
            {
                MenuItem fr = new MenuItem
                {
                    Header = filepath.Replace("_", "__"),
                    Tag = filepath
                };
                recentButton.Visibility = Visibility.Visible;
                recentButton.Content = Path.GetFileName(filepath.Replace("_", "__"));
                recentButton.Click -= RecentFile_click;
                recentButton.Click += RecentFile_click;
                recentButton.Tag = filepath;
                recentButton.ToolTip = filepath;
                fr.Click += RecentFile_click;
                Recents_MenuItem.Items.Add(fr);
                i++;
            }
            while (i < 10)
            {
                RecentButtons[i].Visibility = Visibility.Collapsed;
                i++;
            }
        }

        private void RecentFile_click(object sender, EventArgs e)
        {
            string s = ((FrameworkElement)sender).Tag.ToString();
            if (File.Exists(s))
            {
                LoadFile(s);
            }
            else
            {
                MessageBox.Show($"File does not exist: {s}");
            }
        }

        public void AddRecent(string s, bool loadingList)
        {
            RFiles = RFiles.Where(x => !x.Equals(s, StringComparison.InvariantCultureIgnoreCase)).ToList();
            if (loadingList)
            {
                RFiles.Add(s); //in order
            }
            else
            {
                RFiles.Insert(0, s); //put at front
            }
            if (RFiles.Count > 10)
            {
                RFiles.RemoveRange(10, RFiles.Count - 10);
            }
            Recents_MenuItem.IsEnabled = true;
        }

        #endregion

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

        public void GoToStateEvent(int id)
        {
            MainTabControl.SelectedValue = StateEventMapControl;
            StateEventMapControl.SelectedStateEvent = StateEventMapControl.StateEvents.FirstOrDefault(kvp => kvp.Key == id);
            StateEventMapControl.StateEventMapListBox.ScrollIntoView(StateEventMapControl.SelectedStateEvent);
            StateEventMapControl.StateEventMapListBox.Focus();
        }

    }
}
