using ByteSizeLib;
using ME3Explorer.Packages;
using ME3Explorer.Unreal.Classes;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Threading;

namespace ME3Explorer.Soundplorer
{
    /// <summary>
    /// Interaction logic for SoundplorerWPF.xaml
    /// </summary>
    public partial class SoundplorerWPF : WPFBase
    {
        public static readonly string SoundplorerDataFolder = System.IO.Path.Combine(App.AppDataFolder, @"Soundplorer\");
        private readonly string RECENTFILES_FILE = "RECENTFILES";
        public List<string> RFiles;

        WwiseStream w;
        WwiseBank wb;
        public string afcPath = "";
        BackgroundWorker backgroundScanner;
        List<IExportEntry> BindedExportsList { get; set; }

        public SoundplorerWPF()
        {
            InitializeComponent();

            LoadRecentList();
            RefreshRecent(false);
        }

        private void LoadRecentList()
        {
            Recents_MenuItem.IsEnabled = false;
            RFiles = new List<string>();
            RFiles.Clear();
            string path = SoundplorerDataFolder + RECENTFILES_FILE;
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
            if (!Directory.Exists(SoundplorerDataFolder))
            {
                Directory.CreateDirectory(SoundplorerDataFolder);
            }
            string path = SoundplorerDataFolder + RECENTFILES_FILE;
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
                foreach (System.Windows.Forms.Form form in forms)
                {
                    if (form is PackageEditor) //it will never be "this"
                    {
                        ((PackageEditor)form).RefreshRecent(false, RFiles);
                    }
                }
                foreach (var form in App.Current.Windows)
                {
                    if (form is PackageEditorWPF && this != form)
                    {
                        ((PackageEditorWPF)form).RefreshRecent(false, RFiles);
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


            foreach (string filepath in RFiles)
            {
                MenuItem fr = new MenuItem()
                {
                    Header = filepath.Replace("_", "__"),
                    Tag = filepath
                };
                fr.Click += RecentFile_click;
                Recents_MenuItem.Items.Add(fr);
            }
        }

        private void RecentFile_click(object sender, EventArgs e)
        {
            string s = ((MenuItem)sender).Tag.ToString();
            if (File.Exists(s))
            {
                LoadFile(s);
                AddRecent(s, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
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


        private void OpenCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog { Filter = "*.pcc|*.pcc" };
            bool? result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
                try
                {
                    LoadFile(d.FileName);
                    AddRecent(d.FileName, false);
                    SaveRecentList();
                    RefreshRecent(true, RFiles);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open file:\n" + ex.Message);
                }
            }
        }

        private void LoadFile(string fileName)
        {
            try
            {
                StatusBar_GameID_Container.Visibility = Visibility.Collapsed;
                StatusBar_LeftMostText.Text = "Loading " + System.IO.Path.GetFileName(fileName) + " (" + ByteSize.FromBytes(new System.IO.FileInfo(fileName).Length) + ")";
                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
                LoadME3Package(fileName);
                StatusBar_GameID_Container.Visibility = Visibility.Visible;

                switch (Pcc.Game)
                {
                    case MEGame.ME1:
                        StatusBar_GameID_Text.Text = "ME1";
                        StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.Navy);
                        break;
                    case MEGame.ME2:
                        StatusBar_GameID_Text.Text = "ME2";
                        StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.Maroon);
                        break;
                    case MEGame.ME3:
                        StatusBar_GameID_Text.Text = "ME3";
                        StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.DarkSeaGreen);
                        break;
                    case MEGame.UDK:
                        StatusBar_GameID_Text.Text = "UDK";
                        StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.IndianRed);
                        break;
                }

                afcPath = "";
                LoadObjects();
                StatusBar_LeftMostText.Text = System.IO.Path.GetFileName(fileName);

                //Status.Text = "Ready";
                //saveToolStripMenuItem.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message);
            }
        }

        private void LoadObjects()
        {
            BindedExportsList = Pcc.Exports.Where(e => e.ClassName == "WwiseBank" || e.ClassName == "WwiseStream").ToList();
            SoundExports_ListBox.ItemsSource = BindedExportsList;
            //string s = i.ToString("d6") + " : " + e.ClassName + " : \"" + e.ObjectName + "\"";
        }

        private void SaveCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            //save();
            /*
            foreach (var item in AllTreeViewNodes)
            {
                item.Background = null;
                item.ToolTip = null;
            }*/
        }

        private void SaveAsCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            //saveAs();
            /*
            foreach (var item in AllTreeViewNodes)
            {
                item.Background = null;
                item.ToolTip = null;
            }*/
        }


        public override void handleUpdate(List<PackageUpdate> updates)
        {
            /*List<PackageChange> changes = updates.Select(x => x.change).ToList();
            bool importChanges = changes.Contains(PackageChange.Import) || changes.Contains(PackageChange.ImportAdd);
            bool exportNonDataChanges = changes.Contains(PackageChange.ExportHeader) || changes.Contains(PackageChange.ExportAdd);
            int n = 0;
            bool hasSelection = GetSelected(out n);
            if (CurrentView == View.Names && changes.Contains(PackageChange.Names))
            {
                //int scrollTo = LeftSide_ListView..TopIndex + 1;
                //int selected = listBox1.SelectedIndex;
                RefreshView();
                //listBox1.SelectedIndex = selected;
                //listBox1.TopIndex = scrollTo;
            }
            else if (CurrentView == View.Imports && importChanges ||
                     CurrentView == View.Exports && exportNonDataChanges ||
                     CurrentView == View.Tree && (importChanges || exportNonDataChanges))
            {
                RefreshView();
                if (hasSelection)
                {
                    goToNumber(n);
                }
            }
            else if ((CurrentView == View.Exports || CurrentView == View.Tree) &&
                     hasSelection &&
                     updates.Contains(new PackageUpdate { index = n - 1, change = PackageChange.ExportData }))
            {
                //interpreterControl.memory = pcc.getExport(n).Data;
                //interpreterControl.RefreshMem();
                //binaryInterpreterControl.memory = pcc.getExport(n).Data;
                //binaryInterpreterControl.RefreshMem();
                Preview(true);
            }
            RefreshExportChangedStatus();*/
        }

        private void SoundExports_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            IExportEntry export = (IExportEntry) SoundExports_ListBox.SelectedItem;
            if (export == null)
            {
                soundPanel.UnloadExport();
                return;
            }
            soundPanel.LoadExport(export);
        }

        private void Soundplorer_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            soundPanel.FreeAudioResources();
        }
    }
}
