using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ByteSizeLib;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal.BinaryConverters;
using Microsoft.Win32;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for MeshplorerWPF.xaml
    /// </summary>
    public partial class MeshplorerWPF : WPFBase
    {


        public static readonly string PackageEditorDataFolder = Path.Combine(App.AppDataFolder, @"Meshplorer\");
        private const string RECENTFILES_FILE = "RECENTFILES";
        public List<string> RFiles;
        readonly List<Button> RecentButtons = new List<Button>();
        public ObservableCollectionExtended<ExportEntry> MeshExports { get; } = new ObservableCollectionExtended<ExportEntry>();
        private ExportEntry CurrentExport;



        public MeshplorerWPF()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Meshplorer WPF", new WeakReference(this));

            DataContext = this;
            LoadCommands();
            InitializeComponent();

            RecentButtons.AddRange(new[] { RecentButton1, RecentButton2, RecentButton3, RecentButton4, RecentButton5, RecentButton6, RecentButton7, RecentButton8, RecentButton9, RecentButton10 });
            LoadRecentList();
            RefreshRecent(false);
        }

        public ICommand OpenFileCommand { get; set; }
        public ICommand SaveFileCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }
        public ICommand FindCommand { get; set; }
        public ICommand GotoCommand { get; set; }
        private void LoadCommands()
        {
            OpenFileCommand = new GenericCommand(OpenFile);
            SaveFileCommand = new GenericCommand(SaveFile, PackageIsLoaded);
            SaveAsCommand = new GenericCommand(SaveFileAs, PackageIsLoaded);
            //FindCommand = new GenericCommand(FocusSearch, PackageIsLoaded);
            //GotoCommand = new GenericCommand(FocusGoto, PackageIsLoaded);
        }
        private bool PackageIsLoaded() => Pcc != null;



        private void SaveFile()
        {
            Pcc.save();
        }

        private void SaveFileAs()
        {
            string fileFilter;
            switch (Pcc.Game)
            {
                case MEGame.ME1:
                    fileFilter = App.ME1FileFilter;
                    break;
                case MEGame.ME2:
                case MEGame.ME3:
                    fileFilter = App.ME3ME2FileFilter;
                    break;
                default:
                    string extension = Path.GetExtension(Pcc.FilePath);
                    fileFilter = $"*{extension}|*{extension}";
                    break;
            }
            SaveFileDialog d = new SaveFileDialog { Filter = fileFilter };
            if (d.ShowDialog() == true)
            {
                Pcc.save(d.FileName);
                MessageBox.Show("Done");
            }
        }

        private void OpenFile()
        {
            OpenFileDialog d = new OpenFileDialog { Filter = App.FileFilter };
            if (d.ShowDialog() == true)
            {
#if !DEBUG
                try
                {
#endif
                LoadFile(d.FileName);
                AddRecent(d.FileName, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
#if !DEBUG
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open file:\n" + ex.Message);
                }
#endif
            }
        }

        #region Recents
        private void LoadRecentList()
        {
            Recents_MenuItem.IsEnabled = false;
            RFiles = new List<string>();
            RFiles.Clear();
            string path = PackageEditorDataFolder + RECENTFILES_FILE;
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
            if (!Directory.Exists(PackageEditorDataFolder))
            {
                Directory.CreateDirectory(PackageEditorDataFolder);
            }
            string path = PackageEditorDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
                File.Delete(path);
            File.WriteAllLines(path, RFiles);
        }

        public void RefreshRecent(bool propogate, List<string> recents = null)
        {
            if (propogate && recents != null)
            {
                //we are posting an update to other instances of packed

                //This code can be removed when non-WPF package editor is removed.
                var forms = System.Windows.Forms.Application.OpenForms;
                foreach (var form in Application.Current.Windows)
                {
                    if (form is MeshplorerWPF wpf && this != wpf)
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
            foreach (string filepath in RFiles)
            {
                MenuItem fr = new MenuItem()
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
        #region Busy variables
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private bool _isBusyTaskbar;
        public bool IsBusyTaskbar
        {
            get => _isBusyTaskbar;
            set => SetProperty(ref _isBusyTaskbar, value);
        }

        private string _busyText;

        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }
        #endregion

        public void LoadFile(string s, int goToIndex = 0)
        {
            try
            {
                //BusyText = "Loading " + Path.GetFileName(s);
                //IsBusy = true;
                StatusBar_LeftMostText.Text = $"Loading {Path.GetFileName(s)} ({ByteSize.FromBytes(new FileInfo(s).Length)})";
                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
                LoadMEPackage(s);

                MeshExports.ReplaceAll(Pcc.Exports.Where(x => (x.ClassName == "SkeletalMesh" || x.ClassName == "StaticMesh") && !x.ObjectName.StartsWith("Default__")));

                StatusBar_LeftMostText.Text = Path.GetFileName(s);
                Title = $"Meshplorer WPF - {s}";

                AddRecent(s, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
            }
            catch (Exception e)
            {
                StatusBar_LeftMostText.Text = "Failed to load " + Path.GetFileName(s);
                MessageBox.Show($"Error loading {Path.GetFileName(s)}:\n{e.Message}");
                IsBusy = false;
                IsBusyTaskbar = false;
                //throw e;
            }
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            // Not implemented yet
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string ext = Path.GetExtension(files[0]).ToLower();
                if (ext != ".upk" && ext != ".pcc" && ext != ".sfm")
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
                if (ext == ".upk" || ext == ".pcc" || ext == ".sfm")
                {
                    LoadFile(files[0]);
                }
            }
        }

        private void MeshplorerWPF_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            CurrentExport = null;
            BinaryInterpreterTab_BinaryInterpreter.Dispose();
            InterpreterTab_Interpreter.Dispose();
            Mesh3DViewer.Dispose();
        }

        private void MeshExportsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MeshExportsList.SelectedIndex < 0)
            {
                CurrentExport = null;
                BinaryInterpreterTab_BinaryInterpreter.UnloadExport();
                InterpreterTab_Interpreter.UnloadExport();
                Mesh3DViewer.UnloadExport();

            }
            else
            {
                CurrentExport = (ExportEntry)MeshExportsList.SelectedItem;
                BinaryInterpreterTab_BinaryInterpreter.LoadExport(CurrentExport);
                InterpreterTab_Interpreter.LoadExport(CurrentExport);
                Mesh3DViewer.LoadExport(CurrentExport);
            }
        }
    }
}
