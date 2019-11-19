using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ME3Explorer;
using ME3Explorer.Packages;
using ME3Explorer.Sequence_Editor;
using ME3Explorer.SharedUI;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace ME3Explorer.Matinee
{
    /// <summary>
    /// Interaction logic for InterpEditor.xaml
    /// </summary>
    public partial class InterpEditor : WPFBase
    {

        public InterpEditor()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Interp Viewer", new WeakReference(this));
            LoadCommands();
            DataContext = this;
            StatusText = "Select package file to load";
            InitializeComponent();

            LoadRecentList();

            timelineControl.SelectionChanged += TimelineControlOnSelectionChanged;
        }

        private void TimelineControlOnSelectionChanged(ExportEntry export)
        {
            Properties_InterpreterWPF.LoadExport(export);
        }

        public ObservableCollectionExtended<ExportEntry> InterpDataExports { get; } = new ObservableCollectionExtended<ExportEntry>();
        public ObservableCollectionExtended<string> Animations { get; } = new ObservableCollectionExtended<string>();
        
        #region Properties and Bindings
        public ICommand OpenCommand { get; set; }
        public ICommand SaveCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }
        public ICommand GotoCommand { get; set; }

        private void LoadCommands()
        {
            OpenCommand = new GenericCommand(OpenPackage);
            SaveCommand = new GenericCommand(SavePackage, PackageIsLoaded);
            SaveAsCommand = new GenericCommand(SavePackageAs, PackageIsLoaded);
            GotoCommand = new GenericCommand(GoTo, PackageIsLoaded);
        }

        private void GoTo()
        {
        }

        public string CurrentFile => Pcc != null ? Path.GetFileName(Pcc.FilePath) : "";

        private string _statusText;

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, $"{CurrentFile} {value}");
        }

        private ExportEntry _selectedInterpData;

        public ExportEntry SelectedInterpData
        {
            get => _selectedInterpData;
            set
            {
                if (SetProperty(ref _selectedInterpData, value) && value != null)
                {
                    LoadInterpData(value);
                }
            }
        }

        private void SavePackageAs()
        {
            string extension = Path.GetExtension(Pcc.FilePath);
            SaveFileDialog d = new SaveFileDialog { Filter = $"*{extension}|*{extension}" };
            if (d.ShowDialog() == true)
            {
                Pcc.Save(d.FileName);
                MessageBox.Show("Done.");
            }
        }

        private void SavePackage()
        {
            Pcc.Save();
        }

        private void OpenPackage()
        {
            OpenFileDialog d = new OpenFileDialog { Filter = App.FileFilter };
            if (d.ShowDialog() == true)
            {
                try
                {
                    LoadFile(d.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open file:\n" + ex.Message);
                }
            }
        }

        private bool PackageIsLoaded()
        {
            return Pcc != null;
        }

        #endregion Properties and Bindings

        #region Recents

        private static readonly string InterpEditorDataFolder = Path.Combine(App.AppDataFolder, @"InterpEditor\");
        private readonly List<Button> RecentButtons = new List<Button>();
        public List<string> RFiles;
        private const string RECENTFILES_FILE = "RECENTFILES";

        private void LoadRecentList()
        {
            RecentButtons.AddRange(new[] { RecentButton1, RecentButton2, RecentButton3, RecentButton4, RecentButton5, RecentButton6, RecentButton7, RecentButton8, RecentButton9, RecentButton10 });
            Recents_MenuItem.IsEnabled = false;
            RFiles = new List<string>();
            RFiles.Clear();
            string path = InterpEditorDataFolder + RECENTFILES_FILE;
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
            RefreshRecent(false);
        }

        private void SaveRecentList()
        {
            if (!Directory.Exists(InterpEditorDataFolder))
            {
                Directory.CreateDirectory(InterpEditorDataFolder);
            }
            string path = InterpEditorDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
                File.Delete(path);
            File.WriteAllLines(path, RFiles);
        }

        public void RefreshRecent(bool propogate, List<string> recents = null)
        {
            if (propogate && recents != null)
            {
                //we are posting an update to other instances of InterpEd
                foreach (var window in Application.Current.Windows)
                {
                    if (window is InterpEditor wpf && this != wpf)
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
                foreach (Button recentButton in RecentButtons)
                {
                    recentButton.Visibility = Visibility.Collapsed;
                }
                return;
            }
            Recents_MenuItem.IsEnabled = true;

            int i = 0;
            foreach (string filepath in RFiles)
            {
                MenuItem fr = new MenuItem
                {
                    Header = filepath.Replace("_", "__"),
                    Tag = filepath
                };
                RecentButtons[i].Visibility = Visibility.Visible;
                RecentButtons[i].Content = Path.GetFileName(filepath.Replace("_", "__"));
                RecentButtons[i].Click -= RecentFile_click;
                RecentButtons[i].Click += RecentFile_click;
                RecentButtons[i].Tag = filepath;
                RecentButtons[i].ToolTip = filepath;
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
                MessageBox.Show("File does not exist: " + s);
            }
        }

        public void AddRecent(string s, bool loadingList = false)
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
            SaveRecentList();
        }

        #endregion Recents

        public void LoadFile(string fileName)
        {
            InterpDataExports.ClearEx();
            Animations.ClearEx();
            LoadMEPackage(fileName);
            AddRecent(fileName);
            InterpDataExports.AddRange(Pcc.Exports.Where(exp => exp.ClassName == "InterpData"));
            Animations.AddRange(Pcc.Exports.Where(exp => exp.ClassName == "AnimSequence").Select(a => a.ObjectNameString));
            Title = $"Interp Viewer - {Pcc.FilePath}";
        }

        private void LoadInterpData(ExportEntry value)
        {
            timelineControl.InterpDataExport = value;
            Properties_InterpreterWPF.LoadExport(value);
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            IEnumerable<PackageUpdate> exportUpdates = updates.Where(update => update.change == PackageChange.ExportAdd || 
                                                                                       update.change == PackageChange.ExportData ||
                                                                                       update.change == PackageChange.ExportHeader);
            foreach (var update in exportUpdates)
            {
                var changedExport = Pcc.getExport(update.index);
                if (InterpDataExports.Contains(changedExport))
                {
                    if (changedExport.ClassName != "InterpData")
                    {
                        InterpDataExports.Remove(changedExport);
                    }
                    else if (SelectedInterpData == changedExport)
                    {
                        LoadInterpData(changedExport);
                    }
                }
                else if (changedExport.ClassName == "InterpData")
                {
                    InterpDataExports.Add(changedExport);
                }
                else if (changedExport.IsDescendantOf(SelectedInterpData))
                {
                    timelineControl.RefreshInterpData(changedExport);
                }
            }
        }

        private void WPFBase_Closing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
                return;

            timelineControl.SelectionChanged -= TimelineControlOnSelectionChanged;
        }
    }
}
