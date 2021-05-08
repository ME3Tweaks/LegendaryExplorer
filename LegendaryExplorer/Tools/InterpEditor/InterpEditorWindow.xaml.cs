using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.ToolsetDev.MemoryAnalyzer;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace LegendaryExplorer.Tools.InterpEditor
{
    /// <summary>
    /// Interaction logic for InterpEditor.xaml
    /// </summary>
    public partial class InterpEditorWindow : WPFBase, IRecents
    {

        public InterpEditorWindow() : base("Interp Editor")
        {
            LoadCommands();
            DataContext = this;
            StatusText = "Select package file to load";
            InitializeComponent();
            RecentsController.InitRecentControl(Toolname, Recents_MenuItem, LoadFile);

            timelineControl.SelectionChanged += TimelineControlOnSelectionChanged;
        }

        private void TimelineControlOnSelectionChanged(ExportEntry export)
        {
            Properties_InterpreterWPF.LoadExport(export);
            OnPropertyChanged(nameof(LoadedExportIsCurve));
            if (CurveTab_CurveEditor.CanParse(export))
            {
                CurveTab_CurveEditor.LoadExport(export);
                // Select first curve
                var curve = CurveTab_CurveEditor.InterpCurveTracks.FirstOrDefault()?.Curves.FirstOrDefault();
                if (curve != null)
                {
                    curve.IsSelected = true;
                }
            }
            else
            {
                CurveTab_CurveEditor.UnloadExport();
            }
        }
        public bool LoadedExportIsCurve => Properties_InterpreterWPF != null && CurveTab_CurveEditor != null && CurveTab_CurveEditor.CanParse(Properties_InterpreterWPF.CurrentLoadedExport);
        public ObservableCollectionExtended<ExportEntry> InterpDataExports { get; } = new();
        public ObservableCollectionExtended<string> Animations { get; } = new();

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

        private string _statusText;

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
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
            var d = new SaveFileDialog { Filter = $"*{extension}|*{extension}" };
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
            var d = new OpenFileDialog { Filter = GameFileFilters.OpenFileFilter };
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

        public void LoadFile(string fileName)
        {
            Properties_InterpreterWPF?.UnloadExport();
            InterpDataExports.ClearEx();
            Animations.ClearEx();
            LoadMEPackage(fileName);
            RecentsController.AddRecent(fileName, false);
            RecentsController.SaveRecentList(true);
            InterpDataExports.AddRange(Pcc.Exports.Where(exp => exp.ClassName == "InterpData"));
            Animations.AddRange(Pcc.Exports.Where(exp => exp.ClassName == "AnimSequence").Select(a => a.ObjectNameString));
            Title = $"Interp Viewer - {Pcc.FilePath}";
            StatusText = Path.GetFileName(Pcc.FilePath);
            timelineControl.UnloadExport();
        }

        private void LoadInterpData(ExportEntry value)
        {
            timelineControl.LoadExport(value);
            Properties_InterpreterWPF.LoadExport(value);
            OnPropertyChanged(nameof(LoadedExportIsCurve));
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            IEnumerable<PackageUpdate> exportUpdates = updates.Where(update => update.Change.HasFlag(PackageChange.Export));
            foreach (var update in exportUpdates)
            {
                var changedExport = Pcc.GetUExport(update.Index);

                if (InterpDataExports.Contains(changedExport)) //changes, as it already exists in our list
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
                else if (changedExport.ClassName == "InterpData") //adding an export to the list of interps
                {
                    InterpDataExports.Add(changedExport);
                }
                else if (changedExport.IsDescendantOf(SelectedInterpData)) //track was changed or at least a descendant
                {
                    // subcontrol, 
                    timelineControl.RefreshInterpData(changedExport, update.Change);
                }

                if (Properties_InterpreterWPF.CurrentLoadedExport == changedExport)
                {
                    Properties_InterpreterWPF.LoadExport(changedExport);
                }
            }
        }

        private void WPFBase_Closing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
                return;

            timelineControl.SelectionChanged -= TimelineControlOnSelectionChanged;
            timelineControl.Dispose();
            Properties_InterpreterWPF?.Dispose();
            CurveTab_CurveEditor?.Dispose();
        }

        public void PropogateRecentsChange(IEnumerable<string> newRecents)
        {
            RecentsController.PropogateRecentsChange(false, newRecents);
        }

        public string Toolname => "InterpEditor";
    }
}
