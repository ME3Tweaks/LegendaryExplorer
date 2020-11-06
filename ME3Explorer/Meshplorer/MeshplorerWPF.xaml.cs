using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using ME3Explorer.ME3ExpMemoryAnalyzer;
using ME3Explorer.Meshplorer;
using ME3Explorer.SharedUI;
using ME3Explorer.SharedUI.Interfaces;
using ME3ExplorerCore.Gammtek.Extensions.Collections.Generic;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages.CloningImportingAndRelinking;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;
using Microsoft.AppCenter.Analytics;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for MeshplorerWPF.xaml
    /// </summary>
    public partial class MeshplorerWPF : WPFBase, IRecents
    {
        private bool _isRendererBusy;
        public bool IsRendererBusy
        {
            get => _isRendererBusy;
            set => SetProperty(ref _isRendererBusy, value);
        }

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
        public MeshRendererWPF BindableMesh3DViewer => Mesh3DViewer;


        /// <summary>
        /// Inits a new instance of Meshplorer. If you are auto loading an export use the ExportEntry constructor instead.
        /// </summary>
        public MeshplorerWPF() : base("Meshplorer")
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
            Mesh3DViewer.IsBusyChanged += RendererIsBusyChanged;
            MeshesView.Filter = FilterExportList;
            RecentsController.InitRecentControl(Toolname, Recents_MenuItem, fileToOpen => LoadFile(fileToOpen));
        }

        private void RendererIsBusyChanged(object sender, EventArgs e)
        {
            IsRendererBusy = Mesh3DViewer.IsBusy;
        }

        public MeshplorerWPF(ExportEntry exportToLoad) : this()
        {
            FileQueuedForLoad = exportToLoad.FileRef.FilePath;
            ExportQueuedForFocusing = exportToLoad;
        }

        private bool _showStaticMeshes = true;
        private bool _showSkeletalMeshes = true;
        private bool _showBrushes = true;

        public bool ShowStaticMeshes
        {
            get => _showStaticMeshes;
            set
            {
                SetProperty(ref _showStaticMeshes, value);
                MeshesView.Refresh();
            }
        }
        public bool ShowSkeletalMeshes
        {
            get => _showSkeletalMeshes;
            set
            {
                SetProperty(ref _showSkeletalMeshes, value);
                MeshesView.Refresh();
            }
        }

        public bool ShowBrushes
        {
            get => _showBrushes;
            set
            {
                SetProperty(ref _showBrushes, value);
                MeshesView.Refresh();
            }
        }

        public ICollectionView MeshesView => CollectionViewSource.GetDefaultView(MeshExports);
        private bool FilterExportList(object obj)
        {
            if (obj is ExportEntry exp)
            {
                if (exp.ClassName == "SkeletalMesh" && ShowSkeletalMeshes) return true;
                if (exp.ClassName == "Brush" && ShowBrushes) return true;
                if (exp.ClassName == "StaticMesh" && ShowStaticMeshes) return true;
            }

            return false;
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
        public ICommand ReplaceLODFromUDKCommand { get; set; }
        public ICommand ExportToPSKUModelCommand { get; set; }
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
            ReplaceLODFromUDKCommand = new GenericCommand(ImportLODFromUDK, IsSkeletalMeshSelected);
            ExportToPSKUModelCommand = new GenericCommand(ExportUsingUModel, IsMeshSelected);
        }

        private void ExportUsingUModel()
        {
            Mesh3DViewer.EnsureUModel();
        }

        private void ImportLODFromUDK()
        {
            ReplaceFromUDK(true);
        }

        private void ExportToUDK()
        {
            SaveFileDialog d = new SaveFileDialog { Filter = App.UDKFileFilter };
            if (d.ShowDialog() == true)
            {
                try
                {

                    MEPackageHandler.CreateAndSavePackage(d.FileName, MEGame.UDK);
                    using (IMEPackage upk = MEPackageHandler.OpenUDKPackage(d.FileName))
                    {
                        bool isAlreadyChanged = CurrentExport.DataChanged;
                        byte[] dataBackup = CurrentExport.Data;
                        ObjectBinary objBin = ObjectBinary.From(CurrentExport);
                        foreach ((UIndex uIndex, _) in objBin.GetUIndexes(CurrentExport.Game))
                        {
                            uIndex.value = 0;
                        }
                        CurrentExport.WritePropertiesAndBinary(new PropertyCollection(), objBin);

                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, CurrentExport, upk, null, true,
                                                             out IEntry _);
                        CurrentExport.Data = dataBackup;
                        if (!isAlreadyChanged)
                        {
                            CurrentExport.DataChanged = false;
                        }

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
            ReplaceFromUDK(false);
        }


        private void ReplaceFromUDK(bool lodOnly)
        {
            OpenFileDialog d = new OpenFileDialog { Filter = App.UDKFileFilter };
            if (d.ShowDialog() == true)
            {
                try
                {
                    using IMEPackage udk = MEPackageHandler.OpenUDKPackage(d.FileName);
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

                            if (newMesh.RotOrigin.Pitch != originalMesh.RotOrigin.Pitch ||
                                newMesh.RotOrigin.Yaw != originalMesh.RotOrigin.Yaw ||
                                newMesh.RotOrigin.Roll != originalMesh.RotOrigin.Roll)
                            {
                                MessageBox.Show("The rotation origin of this mesh has changed. The original value is:" +
                                                $"\nPitch {originalMesh.RotOrigin.Roll}, Yaw {originalMesh.RotOrigin.Yaw}, Roll {originalMesh.RotOrigin.Roll}\n" +
                                                "The new value is:\n" +
                                                $"Pitch {newMesh.RotOrigin.Roll}, Yaw {newMesh.RotOrigin.Yaw}, Roll {newMesh.RotOrigin.Roll}\n" +
                                                "These values may need to be adjusted to be accurate.");
                            }


                            newMesh.Materials = originalMesh.Materials.TypedClone();

                            var lods = CurrentExport.GetProperty<ArrayProperty<StructProperty>>("LODInfo");
                            if (!lodOnly)
                            {
                                CurrentExport.WriteBinary(newMesh);

                                //Check LODs count
                                if (lods != null)
                                {
                                    if (lods.Count != originalMesh.LODModels.Length)
                                    {
                                        MessageBox.Show("ASSERT: The amount of items in the LODInfo array (in the export properties) doesn't match the amount of LODs in the original mesh! You need to correct this. LODInfo count should match the amount of LODModels in the binary.");
                                    }
                                }

                                if (newMesh.LODModels.Length < originalMesh.LODModels.Length)
                                {
                                    // we need to update the LOD models
                                    var newlods = lods.Take(newMesh.LODModels.Length).ToList();
                                    lods.Clear();
                                    lods.AddRange(newlods);
                                    CurrentExport.WriteProperty(lods);
                                }

                                if (newMesh.LODModels.Length > originalMesh.LODModels.Length)
                                {
                                    MessageBox.Show("ASSERT: The amount of LODs has increased for this mesh. You must adjust the amount of items in the LODInfo struct to match.");
                                }


                            }
                            else
                            {
                                //Transfer the top LOD in. This is due to some weird shit going on in UDK that makes it blow up.

                                //Build map of new bone names to old bone names so we can translate them
                                List<int> newToOldBoneListIndexMapping = new List<int>(); //Maps old index to new index. This is WV code... but seems to work in a roundabout way
                                Dictionary<string, string> incomingToExistingBoneMapping = new Dictionary<string, string>();
                                for (int i = 0; i < originalMesh.RefSkeleton.Length; i++)
                                {
                                    var incomingName = newMesh.RefSkeleton[i].Name.Name;
                                    //var existingName = originalMesh.RefSkeleton[i].Name.Name;
                                    //incomingToExistingBoneMapping[incomingName] = existingName;
                                    var mappedIndex = originalMesh.RefSkeleton.FindIndex(x => x.Name.Name == incomingName);
                                    if (mappedIndex < 0) Debug.WriteLine("Could not map bone! Name: " + incomingName);
                                    newToOldBoneListIndexMapping.Add(mappedIndex);
                                }

                                var incomingLOD = newMesh.LODModels[0];

                                //Map ActiveBoneIndexes
                                for (int i = 0; i < incomingLOD.ActiveBoneIndices.Length; i++)
                                {
                                    var existingId = incomingLOD.ActiveBoneIndices[i];
                                    var mappedId = newToOldBoneListIndexMapping[existingId];
                                    incomingLOD.ActiveBoneIndices[i] = (ushort)mappedId;
                                }

                                foreach (var chunk in incomingLOD.Chunks)
                                {
                                    //Map the BoneMap to the existing map
                                    for (int i = 0; i < chunk.BoneMap.Length; i++)
                                    {
                                        var existingId = chunk.BoneMap[i];
                                        var mappedId = newToOldBoneListIndexMapping[existingId];
                                        chunk.BoneMap[i] = (ushort)mappedId;
                                    }
                                }
                                //Remove the other LODs, otherwise this could look really weird when it tries to change lods. I don't know anyone who is adding LOD levels
                                originalMesh.LODModels = new[] { incomingLOD };

                                //write it out
                                CurrentExport.WriteBinary(originalMesh); //used to be NEW MESH
                                if (lods.Count > 1)
                                {
                                    // we need to update the LOD models
                                    var newlods = lods.Take(1).ToList();
                                    lods.Clear();
                                    lods.AddRange(newlods);
                                    CurrentExport.WriteProperty(lods);
                                }
                            }

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
                            CurrentExport.WriteBinary(newMesh);
                        }
                        MessageBox.Show(this, "Done!");
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
                    using IMEPackage udk = MEPackageHandler.OpenUDKPackage(d.FileName);
                    string[] meshClasses = { "StaticMesh", "FracturedStaticMesh", "SkeletalMesh" };
                    if (EntrySelector.GetEntry<ExportEntry>(this, udk, "Select mesh to import:", exp => meshClasses.Contains(exp.ClassName)) is ExportEntry meshExport)
                    {
                        ObjectBinary objBin = ObjectBinary.From(meshExport);
                        foreach ((UIndex uIndex, var _) in objBin.GetUIndexes(MEGame.UDK))
                        {
                            uIndex.value = 0;
                        }
                        meshExport.WritePropertiesAndBinary(new PropertyCollection(), objBin);
                        var results = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, meshExport, Pcc,
                                                                           null, true, out _);
                        if (results.Any())
                        {
                            ListDialog ld = new ListDialog(results, "Relink report",
                                                           "The following items failed to relink.(This does not mean the import was unsuccessful, " +
                                                           "just that the listed values will have to be corrected in the Interpreter and BinaryInterpreter)", this);
                            ld.Show();
                        }
                        else
                        {
                            MessageBox.Show("Mesh has been imported with no reported issues.");
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
        private bool IsSkeletalMeshSelected() => Mesh3DViewer.IsSkeletalMesh;

        private bool CanConvertToStaticMesh() => Mesh3DViewer.IsSkeletalMesh && Pcc.Game == MEGame.ME3;

        private void ConvertToStaticMesh()
        {
            if (CurrentExport.ClassName == "SkeletalMesh")
            {
                StaticMesh stm = CurrentExport.GetBinaryData<SkeletalMesh>().ConvertToME3StaticMesh();
                CurrentExport.Class = Pcc.getEntryOrAddImport("Engine.StaticMesh");
                CurrentExport.WritePropertiesAndBinary(new PropertyCollection
                {
                    new BoolProperty(true, "UseSimpleBoxCollision"),
                    new NoneProperty()
                }, stm);
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
                    fileFilter = App.ME1SaveFileFilter;
                    break;
                case MEGame.ME2:
                case MEGame.ME3:
                    fileFilter = App.ME3ME2SaveFileFilter;
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
            OpenFileDialog d = new OpenFileDialog { Filter = App.OpenFileFilter };
            if (d.ShowDialog() == true)
            {
#if !DEBUG
                try
                {
#endif
                LoadFile(d.FileName);
                RecentsController.AddRecent(d.FileName, false);
                RecentsController.SaveRecentList(true);
#if !DEBUG
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open file:\n" + ex.Message);
                }
#endif
            }
        }

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
                StatusBar_LeftMostText.Text =
                    $"Loading {Path.GetFileName(s)} ({FileSize.FormatSize(new FileInfo(s).Length)})";
                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
                LoadMEPackage(s);

                MeshExports.ReplaceAll(Pcc.Exports.Where(Mesh3DViewer.CanParse));

                StatusBar_LeftMostText.Text = Path.GetFileName(s);
                Title = $"Meshplorer - {s}";

                RecentsController.AddRecent(s, false);
                RecentsController.SaveRecentList(true);
                if (goToIndex != 0)
                {
                    CurrentExport = MeshExports.FirstOrDefault(x => x.UIndex == goToIndex);
                    ExportQueuedForFocusing = CurrentExport;
                }
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
            if (updates.Any(update => update.Change == PackageChange.ExportData && update.Index == CurrentExport.UIndex)
             && Mesh3DViewer.CanParse(CurrentExport))
            {
                CurrentExport = CurrentExport;//trigger propertyset stuff
            }

            List<PackageUpdate> exportUpdates = updates.Where(upd => upd.Change.HasFlag(PackageChange.Export)).ToList();
            bool shouldUpdateList = false;
            foreach (ExportEntry meshExport in MeshExports)
            {
                if (exportUpdates.Any(upd => upd.Index == meshExport.UIndex))
                {
                    shouldUpdateList = true;
                    break;
                }
            }

            if (!shouldUpdateList)
            {
                foreach (PackageUpdate update in exportUpdates)
                {
                    if (Pcc.GetEntry(update.Index) is ExportEntry exp && Mesh3DViewer.CanParse(exp))
                    {
                        shouldUpdateList = true;
                        break;
                    }
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
            Mesh3DViewer.IsBusyChanged -= RendererIsBusyChanged;
            BinaryInterpreterTab_BinaryInterpreter.Dispose();
            InterpreterTab_Interpreter.Dispose();
            Mesh3DViewer.Dispose();
        }

        private void OpenInPackageEditor_Clicked(object sender, RoutedEventArgs e)
        {
            if (MeshExportsList.SelectedItem is ExportEntry export)
            {
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

        public void PropogateRecentsChange(IEnumerable<string> newRecents)
        {
            RecentsController.PropogateRecentsChange(false, newRecents);
        }

        public string Toolname => "Meshplorer";
    }
}
