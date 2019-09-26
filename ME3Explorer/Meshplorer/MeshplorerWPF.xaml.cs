using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ByteSizeLib;
using ME3Explorer.PackageEditorWPFControls;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
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
        private ExportEntry _currentExport;
        public ExportEntry CurrentExport
        {
            get => _currentExport;
            set
            {
                SetProperty(ref _currentExport, value);
                if (value == null)
                {
                    BinaryInterpreterTab_BinaryInterpreter.UnloadExport();
                    InterpreterTab_Interpreter.UnloadExport();
                    Mesh3DViewer.UnloadExport();

                }
                else
                {
                    BinaryInterpreterTab_BinaryInterpreter.LoadExport(CurrentExport);
                    InterpreterTab_Interpreter.LoadExport(CurrentExport);
                    Mesh3DViewer.LoadExport(CurrentExport);
                }
            }
        }

        private string FileQueuedForLoad;
        private ExportEntry ExportQueuedForFocusing;

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

        public MeshplorerWPF(ExportEntry exportToLoad) : this()
        {
            FileQueuedForLoad = exportToLoad.FileRef.FilePath;
            ExportQueuedForFocusing = exportToLoad;
        }

        public ICommand OpenFileCommand { get; set; }
        public ICommand SaveFileCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }
        public ICommand FindCommand { get; set; }
        public ICommand GotoCommand { get; set; }
        public ICommand ConvertToStaticMeshCommand { get; set; }
        public ICommand ImportFromUDKCommand { get; set; }
        public ICommand ReplaceFromUDKCommand { get; set; }
        public ICommand ExportToUDKCommand { get; set; }
        private void LoadCommands()
        {
            OpenFileCommand = new GenericCommand(OpenFile);
            SaveFileCommand = new GenericCommand(SaveFile, PackageIsLoaded);
            SaveAsCommand = new GenericCommand(SaveFileAs, PackageIsLoaded);
            //FindCommand = new GenericCommand(FocusSearch, PackageIsLoaded);
            //GotoCommand = new GenericCommand(FocusGoto, PackageIsLoaded);
            ConvertToStaticMeshCommand = new GenericCommand(ConvertToStaticMesh, CanConvertToStaticMesh);
            ImportFromUDKCommand = new GenericCommand(ImportFromUDK, PackageIsLoaded);
            ReplaceFromUDKCommand = new GenericCommand(ReplaceFromUDK, IsMeshSelected);
            ExportToUDKCommand = new GenericCommand(ExportToUDK, IsMeshSelected);
        }

        private void ExportToUDK()
        {
            SaveFileDialog d = new SaveFileDialog {Filter = App.UDKFileFilter};
            if (d.ShowDialog() == true)
            {
                try
                {

                    MEPackageHandler.CreateAndSaveUDKPackage(d.FileName);
                    using (IMEPackage upk = MEPackageHandler.OpenUDKPackage(d.FileName))
                    {
                        byte[] dataBackup = CurrentExport.Data;
                        ObjectBinary objBin = ObjectBinary.From(CurrentExport);
                        foreach ((UIndex uIndex, var _) in objBin.GetUIndexes(CurrentExport.Game))
                        {
                            uIndex.value = 0;
                        }
                        CurrentExport.setBinaryData(objBin.ToBytes(CurrentExport.FileRef));
                        CurrentExport.WriteProperties(new PropertyCollection());

                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, CurrentExport, upk, null, true,
                                                             out IEntry newEntry);
                        CurrentExport.Data = dataBackup;
                        ExportEntry newExport = (ExportEntry)newEntry;

                        upk.Save();
                    }
                    MessageBox.Show(this, "Done!");
                }
                catch (Exception e)
                {
                    new ExceptionHandlerDialogWPF(e).ShowDialog();
                }
            }
        }

        private void ReplaceFromUDK()
        {
            OpenFileDialog d = new OpenFileDialog { Filter = App.UDKFileFilter };
            if (d.ShowDialog() == true)
            {
                try
                {
                    using (IMEPackage udk = MEPackageHandler.OpenUDKPackage(d.FileName))
                    {
                        string className = CurrentExport.ClassName;
                        if (EntrySelector.GetEntry<ExportEntry>(this, udk, $"Select {className} to import:", exp => exp.ClassName == className) is ExportEntry meshExport)
                        {
                            if (className == "SkeletalMesh")
                            {
                                SkeletalMesh newMesh = ObjectBinary.From<SkeletalMesh>(meshExport);
                                SkeletalMesh originalMesh = ObjectBinary.From<SkeletalMesh>(CurrentExport);
                                if (newMesh.RefSkeleton.Length != originalMesh.RefSkeleton.Length)
                                {
                                    MessageBox.Show(this, "Cannot replace a SkeletalMesh with one that has a different number of bones!");
                                    return;
                                }

                                newMesh.Materials = originalMesh.Materials.TypedClone();
                                CurrentExport.setBinaryData(newMesh.ToBytes(Pcc));
                            }
                            else
                            {
                                StaticMesh newMesh;
                                StaticMesh originalMesh;
                                if (className == "FracturedStaticMesh")
                                {
                                    newMesh = ObjectBinary.From<FracturedStaticMesh>(meshExport);
                                    originalMesh = ObjectBinary.From<FracturedStaticMesh>(CurrentExport);
                                }
                                else
                                {
                                    newMesh = ObjectBinary.From<StaticMesh>(meshExport);
                                    originalMesh = ObjectBinary.From<StaticMesh>(CurrentExport);
                                }

                                newMesh.BodySetup = 0;
                                if (originalMesh.LODModels.Any())
                                {
                                    UIndex[] mats = originalMesh.LODModels[0].Elements.Select(el => el.Material).ToArray();
                                    foreach (StaticMeshRenderData lodModel in newMesh.LODModels)
                                    {
                                        for (int i = 0; i < lodModel.Elements.Length; i++)
                                        {
                                            UIndex matIndex = 0;
                                            if (i < mats.Length)
                                            {
                                                matIndex = mats[i];
                                            }
                                            lodModel.Elements[i].Material = matIndex;
                                        }
                                    }
                                }
                                CurrentExport.setBinaryData(newMesh.ToBytes(Pcc));
                            }
                            MessageBox.Show(this, "Done!");
                        }
                    }
                }
                catch (Exception e)
                {
                    new ExceptionHandlerDialogWPF(e).ShowDialog();
                }
            }
        }

        private void ImportFromUDK()
        {
            OpenFileDialog d = new OpenFileDialog { Filter = App.UDKFileFilter };
            if (d.ShowDialog() == true)
            {
                try
                {
                    using (IMEPackage udk = MEPackageHandler.OpenUDKPackage(d.FileName))
                    {
                        string[] meshClasses = { "StaticMesh", "FracturedStaticMesh", "SkeletalMesh" };
                        if (EntrySelector.GetEntry<ExportEntry>(this, udk, "Select mesh to import:", exp => meshClasses.Contains(exp.ClassName)) is ExportEntry meshExport)
                        {
                            ObjectBinary objBin = ObjectBinary.From(meshExport);
                            foreach ((UIndex uIndex, var _) in objBin.GetUIndexes(MEGame.UDK))
                            {
                                uIndex.value = 0;
                            }
                            meshExport.setBinaryData(objBin.ToBytes(udk));
                            meshExport.WriteProperties(new PropertyCollection());
                            var results = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, meshExport, Pcc,
                                                                               null, true, out _);
                            if (results.Any())
                            {
                                ListDialog ld = new ListDialog(results, "Relink report",
                                                               "The following items failed to relink.(This does not mean the import was unsuccesful, " +
                                                               "just that the listed values will have to be corrected in the Interpreter and BinaryInterpreter)", this);
                                ld.Show();
                            }
                            else
                            {
                                MessageBox.Show("Mesh has been imported with no reported issues.");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    new ExceptionHandlerDialogWPF(e).ShowDialog();
                }
            }
        }

        private bool IsMeshSelected() => Mesh3DViewer.IsStaticMesh || Mesh3DViewer.IsSkeletalMesh;

        private bool CanConvertToStaticMesh() => Mesh3DViewer.IsSkeletalMesh && Pcc.Game == MEGame.ME3;

        private void ConvertToStaticMesh()
        {
            if (CurrentExport.ClassName == "SkeletalMesh")
            {
                StaticMesh stm = ObjectBinary.From<SkeletalMesh>(CurrentExport).ConvertToME3StaticMesh();
                CurrentExport.WriteProperties(new PropertyCollection
                {
                    new BoolProperty(true, "UseSimpleBoxCollision"),
                    new NoneProperty()
                });
                CurrentExport.setBinaryData(stm.ToBytes(Pcc));
                CurrentExport.Class = Pcc.getEntryOrAddImport("Engine.StaticMesh");
            }
        }

        private bool PackageIsLoaded() => Pcc != null;



        private void SaveFile()
        {
            Pcc.Save();
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
                Pcc.Save(d.FileName);
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

                MeshExports.ReplaceAll(Pcc.Exports.Where(Mesh3DViewer.CanParse));

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
            if (updates.Any(update => update.change == PackageChange.ExportData && update.index == CurrentExport.Index)
             && Mesh3DViewer.CanParse(CurrentExport))
            {
                CurrentExport = CurrentExport;//trigger propertyset stuff
            }

            List<PackageUpdate> exportUpdates = updates.Where(upd => upd.change == PackageChange.ExportData || upd.change == PackageChange.ExportHeader ||
                                                                     upd.change == PackageChange.ExportRemove || upd.change == PackageChange.ExportAdd).ToList();
            bool shouldUpdateList = false;
            foreach (ExportEntry meshExport in MeshExports)
            {
                if (exportUpdates.Any(upd => upd.index == meshExport.Index))
                {
                    shouldUpdateList = true;
                    break;
                }
            }

            foreach (PackageUpdate update in exportUpdates)
            {
                if (!MeshExports.Contains(Pcc.GetEntry(update.index)))
                {
                    shouldUpdateList = true;
                    break;
                }
            }

            if (shouldUpdateList)
            {
                MeshExports.ReplaceAll(Pcc.Exports.Where(Mesh3DViewer.CanParse));
            }
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

        private void OpenInPackageEditor_Clicked(object sender, RoutedEventArgs e)
        {
            if (MeshExportsList.SelectedItem is ExportEntry export){
                PackageEditorWPF p = new PackageEditorWPF();
                p.Show();
                p.LoadFile(export.FileRef.FilePath, export.UIndex);
                p.Activate(); //bring to front
            }
        }

        private void MeshplorerWPF_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(FileQueuedForLoad))
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    //Wait for all children to finish loading
                    LoadFile(FileQueuedForLoad);
                    FileQueuedForLoad = null;

                    if (MeshExports.Contains(ExportQueuedForFocusing))
                    {
                        CurrentExport = ExportQueuedForFocusing;
                    }
                    ExportQueuedForFocusing = null;

                    Activate();
                }));
            }
        }
    }
}
