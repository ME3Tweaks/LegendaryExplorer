using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.UserControls.SharedToolControls;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace LegendaryExplorer.Tools.FaceFXEditor
{
    /// <summary>
    /// Interaction logic for FaceFXEditor.xaml
    /// </summary>
    public partial class FaceFXEditorWindow : WPFBase, IRecents
    {

        public ObservableCollectionExtended<ExportEntry> AnimSets { get; } = new();

        public string CurrentFile => Pcc != null ? Path.GetFileName(Pcc.FilePath) : "Select a file to load";

        private ExportEntry _selectedExport;

        public ExportEntry SelectedExport
        {
            get => _selectedExport;
            set => SetProperty(ref _selectedExport, value);
        }

        private string FileQueuedForLoad;
        private ExportEntry ExportQueuedForFocusing;
        private string LineQueuedForFocusing;

        public FaceFXEditorWindow() : base("FaceFX Editor")
        {
            InitializeComponent();
            LoadCommands();
            DataContext = this;

            RecentsController.InitRecentControl(Toolname, Recents_MenuItem, LoadFile);
        }

        public FaceFXEditorWindow(ExportEntry export, string lineName = null) : this()
        {
            FileQueuedForLoad = export.FileRef.FilePath;
            ExportQueuedForFocusing = export;
            LineQueuedForFocusing = lineName;
        }

        public ICommand OpenCommand { get; set; }
        public ICommand SaveCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }
        public ICommand LoadAnimsetCommand { get; set; }

        private void LoadCommands()
        {
            OpenCommand = new GenericCommand(OpenPackage);
            SaveCommand = new GenericCommand(SavePackage, PackageIsLoaded);
            SaveAsCommand = new GenericCommand(SavePackageAs, PackageIsLoaded);
            LoadAnimsetCommand = new GenericCommand(LoadAnimset, CanLoadAnimset);
        }

        private bool CanLoadAnimset()
        {
            return SelectedExport != null;
        }

        public void SelectAnimset(int uIndex, string lineName = null)
        {
            if (Pcc is null || !Pcc.IsUExport(uIndex)) return;
            SelectedExport = Pcc.GetUExport(uIndex);
            LoadAnimset();
            if (lineName != null)
            {
                editorControl.SelectLineByName(lineName);
            }
        }

        private void LoadAnimset()
        {
            editorControl.SaveChanges();
            editorControl.LoadExport(SelectedExport);
        }

        private async void SavePackageAs()
        {
            string extension = Path.GetExtension(Pcc.FilePath);
            var d = new SaveFileDialog { Filter = $"*{extension}|*{extension}" };
            if (d.ShowDialog() == true)
            {
                editorControl.SaveChanges();
                await Pcc.SaveAsync(d.FileName);
                MessageBox.Show("Done.");
            }
        }

        private async void SavePackage()
        {
            editorControl.SaveChanges();
            await Pcc.SaveAsync();
        }

        private void OpenPackage()
        {
            var d = AppDirectories.GetOpenPackageDialog();
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
            System.Diagnostics.Debug.WriteLine("Package Is Loaded.");
            return Pcc != null;
        }

        public void LoadFile(string fileName)
        {
            try
            {
                SelectedExport = null;
                AnimSets.ClearEx();
                LoadMEPackage(fileName);
                editorControl.UnloadExport();
                RefreshComboBox();

                Title = $"FaceFX Editor - {Pcc.FilePath}";
                RecentsController.AddRecent(fileName, false, Pcc?.Game);
                RecentsController.SaveRecentList(true);
                OnPropertyChanged(nameof(CurrentFile));
            }
            catch (Exception ex)
            {
                UnLoadMEPackage();
                MessageBox.Show($"Error:\n{ex.Message}");
            }
        }

        private void RefreshComboBox()
        {
            ExportEntry item = SelectedExport;
            AnimSets.ClearEx();
            AnimSets.AddRange(Pcc.Exports.Where(exp => exp.ClassName == "FaceFXAnimSet" || (exp.ClassName == "FaceFXAsset" && exp.Game != MEGame.ME2)));
            if (AnimSets.Contains(item))
            {
                SelectedExport = item;
            }
            else
            {
                SelectedExport = AnimSets.FirstOrDefault();
            }
        }

        public override void HandleUpdate(List<PackageUpdate> updates)
        {
            IEnumerable<PackageUpdate> relevantUpdates = updates.Where(x => x.Change.HasFlag(PackageChange.Export));
            List<int> updatedExports = relevantUpdates.Select(x => x.Index).ToList();
            if (SelectedExport != null && updatedExports.Contains(SelectedExport.UIndex))
            {
                int index = SelectedExport.UIndex;
                //loaded FaceFXAnimset is no longer a FaceFXAnimset
                if (SelectedExport.ClassName != "FaceFXAnimSet" && SelectedExport.ClassName != "FaceFXAsset")
                {
                    editorControl.UnloadExport();
                }
                else if (!this.IsForegroundWindow())
                {
                    editorControl.LoadExport(SelectedExport);
                }
                updatedExports.Remove(index);
            }
            if (updatedExports.Intersect(AnimSets.Select(exp => exp.UIndex)).Any())
            {
                RefreshComboBox();
            }
            else
            {
                if (updatedExports.Any(uIdx => Pcc.GetEntry(uIdx)?.ClassName == "FaceFXAnimSet" || (Pcc.GetEntry(uIdx)?.ClassName == "FaceFXAsset" && Pcc.Game != MEGame.ME2)))
                {
                    RefreshComboBox();
                }
            }
        }

        private void WPFBase_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(FileQueuedForLoad))
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    //Wait for all children to finish loading
                    LoadFile(FileQueuedForLoad);
                    FileQueuedForLoad = null;

                    if (AnimSets.Contains(ExportQueuedForFocusing))
                    {
                        SelectedExport = ExportQueuedForFocusing;
                        RefreshComboBox();
                        editorControl.LoadExport(SelectedExport);
                        if (LineQueuedForFocusing != null)
                        {
                            editorControl.SelectLineByName(LineQueuedForFocusing);
                        }
                    }
                    ExportQueuedForFocusing = null;
                    LineQueuedForFocusing = null;

                    Activate();
                }));
            }
        }

        public void PropogateRecentsChange(string propogationSource, IEnumerable<RecentsControl.RecentItem> newRecents)
        {
            RecentsController.PropogateRecentsChange(false, newRecents);
        }

        public string Toolname => "FaceFXEditor";

        private void FaceFXEditorWindow_OnClosing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
                return;

            RecentsController?.Dispose();
            SelectedExport = null;
            AnimSets.ClearEx();
            UnLoadMEPackage();
            editorControl?.Dispose();
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string ext = Path.GetExtension(files[0]).ToLower();
                if (ext != ".u" && ext != ".upk" && ext != ".pcc" && ext != ".sfm" && ext != ".udk")
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                }
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // Assuming you have one file that you care about, pass it off to whatever
                // handling code you have defined.
                LoadFile(files[0]);
            }
        }
    }
}
