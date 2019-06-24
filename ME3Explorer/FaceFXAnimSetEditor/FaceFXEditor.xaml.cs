using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace ME3Explorer.FaceFX
{
    /// <summary>
    /// Interaction logic for FaceFXEditor.xaml
    /// </summary>
    public partial class FaceFXEditor : WPFBase
    {
        public ObservableCollectionExtended<IExportEntry> AnimSets { get; } = new ObservableCollectionExtended<IExportEntry>();

        private IExportEntry _selectedExport;

        public IExportEntry SelectedExport
        {
            get => _selectedExport;
            set => SetProperty(ref _selectedExport, value);
        }

        private string FileQueuedForLoad;
        private IExportEntry ExportQueuedForFocusing;
        private string LineQueuedForFocusing;

        public FaceFXEditor()
        {
            InitializeComponent();
            LoadCommands();
            DataContext = this;
        }

        public FaceFXEditor(IExportEntry export, string lineName = null) : this()
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

        private void LoadAnimset()
        {
            editorControl.SaveChanges();
            editorControl.LoadExport(SelectedExport);
        }

        private void SavePackageAs()
        {
            string extension = Path.GetExtension(Pcc.FilePath);
            SaveFileDialog d = new SaveFileDialog { Filter = $"*{extension}|*{extension}" };
            if (d.ShowDialog() == true)
            {
                editorControl.SaveChanges();
                Pcc.save(d.FileName);
                MessageBox.Show("Done.");
            }
        }

        private void SavePackage()
        {
            editorControl.SaveChanges();
            Pcc.save();
        }

        private void OpenPackage()
        {
            OpenFileDialog d = new OpenFileDialog { Filter = App.FileFilter };
            if (d.ShowDialog() == true)
            {
                try
                {
                    LoadFile(d.FileName);
                    Title = $"FaceFXEditor - {Pcc.FilePath}";
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
                LoadMEPackage(fileName);
                editorControl.UnloadExport();
                RefreshComboBox();
            }
            catch (Exception ex)
            {
                UnLoadMEPackage();
                MessageBox.Show($"Error:\n{ex.Message}");
            }
        }

        private void RefreshComboBox()
        {
            IExportEntry item = SelectedExport;
            AnimSets.ClearEx();
            AnimSets.AddRange(Pcc.Exports.Where(exp => exp.ClassName == "FaceFXAnimSet"));
            if (AnimSets.Contains(item))
            {
                SelectedExport = item;
            }
            else
            {
                SelectedExport = AnimSets.FirstOrDefault();
            }
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            IEnumerable<PackageUpdate> relevantUpdates = updates.Where(x => x.change != PackageChange.Import &&
                                                                            x.change != PackageChange.ImportAdd &&
                                                                            x.change != PackageChange.Names);
            List<int> updatedExports = relevantUpdates.Select(x => x.index).ToList();
            if (SelectedExport != null && updatedExports.Contains(SelectedExport.Index))
            {
                int index = SelectedExport.Index;
                //loaded FaceFXAnimset is no longer a FaceFXAnimset
                if (SelectedExport.ClassName != "FaceFXAnimSet")
                {
                    editorControl.UnloadExport();
                }
                else if (!this.IsForegroundWindow())
                {
                    editorControl.LoadExport(SelectedExport);
                }
                updatedExports.Remove(index);
            }
            if (updatedExports.Intersect(AnimSets.Select(x => x.Index)).Any())
            {
                RefreshComboBox();
            }
            else
            {
                foreach (var i in updatedExports)
                {
                    if (Pcc.getExport(i).ClassName == "FaceFXAnimSet")
                    {
                        RefreshComboBox();
                        break;
                    }
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
    }
}
