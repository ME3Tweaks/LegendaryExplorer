using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.Tools.InterpEditor.InterpExperiments;
using LegendaryExplorer.UserControls.SharedToolControls;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
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

            TimelineControl.SelectionChanged += TimelineControlOnSelectionChanged;
            TimelineControl.SetGroupActorRequested += OnSetGroupActorRequested;
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
        public ObservableCollectionExtended<ExportEntry> InterpDataExports { get; } = [];
        public ObservableCollectionExtended<string> Animations { get; } = [];

        #region Properties and Bindings
        public ICommand OpenCommand { get; set; }
        public ICommand SaveCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }
        public ICommand GotoCommand { get; set; }
        public ICommand RenameTrackCommand { get; set; }

        public ICommand InsertKeyCommand { get; set; }
        public ICommand AddPresetDirectorGroupCommand { get; set; }
        public ICommand AddPresetCameraGroupCommand { get; set; }
        public ICommand AddPresetActorGroupCommand { get; set; }

        // Playback commands
        public ICommand PlayCommand { get; set; }
        public ICommand PauseCommand { get; set; }
        public ICommand StopCommand { get; set; }
        public ICommand ConnectLevelEditorCommand { get; set; }

        // Playback state
        private float _currentTime;
        private float _prevTime;
        private float _duration;
        private bool _isPlaying;
        private LevelEditor.LevelEditor _connectedLevelEditor;
        private SeqAct_Interp _currentlyPlayingInterp;

        public float CurrentTime
        {
            get => _currentTime;
            set { SetProperty(ref _currentTime, value); UpdatePlayhead(); }
        }

        public float Duration
        {
            get => _duration;
            set => SetProperty(ref _duration, value);
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set => SetProperty(ref _isPlaying, value);
        }

        public string ConnectedLevelEditorText =>
            _connectedLevelEditor is null ? "Not connected" : "Connected";

        private void LoadCommands()
        {
            OpenCommand = new GenericCommand(OpenPackage);
            SaveCommand = new GenericCommand(SavePackage, PackageIsLoaded);
            SaveAsCommand = new GenericCommand(SavePackageAs, PackageIsLoaded);
            GotoCommand = new GenericCommand(GoTo, PackageIsLoaded);
            RenameTrackCommand = new GenericCommand(RenameTrack);

            InsertKeyCommand = new GenericCommand(InsertKeyAtTime, PackageIsLoaded);
            AddPresetDirectorGroupCommand = new GenericCommand(() => InterpEditorExperimentsE.AddPresetGroup("Director", this), PackageIsLoaded);
            AddPresetCameraGroupCommand = new GenericCommand(() => InterpEditorExperimentsE.AddPresetGroup("Camera", this), PackageIsLoaded);
            AddPresetActorGroupCommand = new GenericCommand(() => InterpEditorExperimentsE.AddPresetGroup("Actor", this), PackageIsLoaded);

            PlayCommand = new GenericCommand(Play, () => !_isPlaying && PackageIsLoaded());
            PauseCommand = new GenericCommand(Pause, () => _isPlaying);
            StopCommand = new GenericCommand(Stop, PackageIsLoaded);
            ConnectLevelEditorCommand = new GenericCommand(ConnectToLevelEditor, PackageIsLoaded);
        }

        private void GoTo()
        {
            string result = PromptDialog.Prompt(this, "Enter export UIndex to navigate to:", "Go To Export");
            if (string.IsNullOrEmpty(result) || !int.TryParse(result, out int uIndex))
            {
                return;
            }

            if (!Pcc.IsUExport(uIndex))
            {
                MessageBox.Show($"Export #{uIndex} does not exist in this package.", "Invalid Export", MessageBoxButton.OK);
                return;
            }

            var export = Pcc.GetUExport(uIndex);

            // If the export is an InterpData, select it in the list
            if (export.ClassName == "InterpData")
            {
                var match = InterpDataExports.FirstOrDefault(e => e.UIndex == uIndex);
                if (match != null)
                {
                    SelectedInterpData = match;
                }
                return;
            }

            // If the export is a descendant of an InterpData, load that InterpData and select the export
            var interpDataParent = InterpDataExports.FirstOrDefault(e => export.IsDescendantOf(e));
            if (interpDataParent != null)
            {
                if (SelectedInterpData != interpDataParent)
                {
                    SelectedInterpData = interpDataParent;
                }

                // Select the track/group in the timeline
                TimelineControl.SelectExport(export);
                return;
            }

            // Otherwise just load the export into the properties panel
            Properties_InterpreterWPF.LoadExport(export);
        }

        private void RenameTrack()
        {
            TimelineControl?.RenameTrack();
        }

        private void InsertKeyAtTime()
        {
            TimelineControl?.InsertKeyAtTime();
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

        private async void SavePackageAs()
        {
            string extension = Path.GetExtension(Pcc.FilePath);
            var d = new SaveFileDialog { Filter = $"*{extension}|*{extension}" };
            if (d.ShowDialog() == true)
            {
                await Pcc.SaveAsync(d.FileName);
                MessageBox.Show("Done.");
            }
        }

        private async void SavePackage()
        {
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
            return Pcc != null;
        }

        #endregion Properties and Bindings

        public void LoadFile(string fileName)
        {
            Stop();
            Properties_InterpreterWPF?.UnloadExport();
            InterpDataExports.ClearEx();
            Animations.ClearEx();
            LoadMEPackage(fileName);
            RecentsController.AddRecent(fileName, false, Pcc?.Game);
            RecentsController.SaveRecentList(true);
            InterpDataExports.AddRange(Pcc.Exports.Where(exp => exp.ClassName == "InterpData"));
            Animations.AddRange(Pcc.Exports.Where(exp => exp.ClassName == "AnimSequence").Select(a => a.ObjectNameString));
            Title = $"Interp Editor - {Pcc.FilePath}";
            StatusText = Path.GetFileName(Pcc.FilePath);
            TimelineControl.UnloadExport();
        }

        private void LoadInterpData(ExportEntry value)
        {
            Stop();
            Duration = value.GetProperty<LegendaryExplorerCore.Unreal.FloatProperty>("InterpLength")?.Value ?? 0f;
            TimelineControl.LoadExport(value);
            Properties_InterpreterWPF.LoadExport(value);
            OnPropertyChanged(nameof(LoadedExportIsCurve));
        }

        #region Playback

        private void Play()
        {
            if (TimelineControl.InterpData is null)
            {
                return;
            }
            if (_connectedLevelEditor is null)
            {
                ConnectToLevelEditor();
            }
            if (_connectedLevelEditor is null) return;
            if (TimelineControl.InterpData.Parent is null)
            {
                ExportEntry seqActInterp = KismetHelper.FindVariableConnectionsToNode(TimelineControl.InterpData.Export).FirstOrDefault(exp => exp.ClassName == "SeqAct_Interp");
                if (seqActInterp is not null)
                {
                    _currentlyPlayingInterp = new SeqAct_Interp(seqActInterp, TimelineControl.InterpData);
                }
            }
            TimelineControl.InterpData.AttachToLevelEditor(_connectedLevelEditor);
            IsPlaying = true;
        }

        private void Pause()
        {
            IsPlaying = false;
        }

        private void Stop()
        {
            bool wasPlaying = IsPlaying;
            Pause();
            _prevTime = 0f;
            CurrentTime = 0f;
            if (wasPlaying)
            {
                TimelineControl.InterpData?.StopMatinee();
            }
            TimelineControl.ClearPlayhead();
        }

        private void OnUpdateScene(object sender, float deltaTime)
        {
            if (!IsPlaying)
            {
                return;
            }
            float newTime = Math.Min(_currentTime + deltaTime, _duration);
            TimelineControl.InterpData?.Step(newTime, _prevTime);
            _prevTime = newTime;
            CurrentTime = newTime;
            if (newTime >= _duration)
            {
                Stop();
            }
        }

        private void ConnectToLevelEditor()
        {
            var openEditors = Application.Current.Windows.OfType<LevelEditor.LevelEditor>().ToList();
            if (openEditors.Count == 0)
            {
                MessageBox.Show("Open a Level Editor window first, then connect.", "No Level Editor", MessageBoxButton.OK);
                return;
            }

            LevelEditor.LevelEditor chosen;
            if (openEditors.Count == 1)
            {
                chosen = openEditors[0];
            }
            else
            {
                var dlg = new Dialogs.DropdownPromptDialog(
                    "Multiple Level Editors are open. Choose one:",
                    "Select Level Editor", "Level Editor",
                    openEditors.Select(e => e.Title), this);
                if (dlg.ShowDialog() != true || dlg.Response is null) return;
                chosen = openEditors.First(e => e.Title == dlg.Response);
            }

            _connectedLevelEditor = chosen;
            _connectedLevelEditor.RenderContext.UpdateScene += OnUpdateScene;
            _connectedLevelEditor.Closed += OnLevelEditorClosed;
            TimelineControl.IsLevelEditorConnected = true;
            OnPropertyChanged(nameof(ConnectedLevelEditorText));
        }

        private void DisconnectLevelEditor()
        {
            if (_connectedLevelEditor is null) return;
            if (_isPlaying) Pause();
            TimelineControl.InterpData?.StopMatinee();
            _connectedLevelEditor.RenderContext.UpdateScene -= OnUpdateScene;
            _connectedLevelEditor.Closed -= OnLevelEditorClosed;
            _connectedLevelEditor = null;
            TimelineControl.IsLevelEditorConnected = false;
            OnPropertyChanged(nameof(ConnectedLevelEditorText));
        }

        private void OnLevelEditorClosed(object sender, EventArgs e) => DisconnectLevelEditor();

        private void OnSetGroupActorRequested(InterpGroup group)
        {
            if (_connectedLevelEditor is null) return;
            var actor = ActorPickerDialog.PickActor(this, _connectedLevelEditor);
            if (actor is not null)
                group.ManualGroupActor = actor;
        }

        public void Scrub(float time)
        {
            float newTime = Math.Clamp(time, 0f, _duration);
            TimelineControl.InterpData?.Step(newTime, _prevTime);
            _prevTime = newTime;
            CurrentTime = newTime;
        }

        private void UpdatePlayhead()
        {
            TimelineControl.SetPlayheadTime(_currentTime);
        }

        #endregion Playback

        public override void HandleUpdate(List<PackageUpdate> updates)
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
                    TimelineControl.RefreshInterpData(changedExport, update.Change);
                }

                if (Properties_InterpreterWPF.CurrentLoadedExport == changedExport)
                {
                    Properties_InterpreterWPF.LoadExport(changedExport);
                    if (LoadedExportIsCurve)
                    {
                        CurveTab_CurveEditor.LoadExport(changedExport);
                    }
                }
            }
        }

        private void WPFBase_Closing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
                return;

            Stop();
            DisconnectLevelEditor();
            TimelineControl.SetGroupActorRequested -= OnSetGroupActorRequested;
            TimelineControl.SelectionChanged -= TimelineControlOnSelectionChanged;
            TimelineControl.Dispose();
            Properties_InterpreterWPF?.Dispose();
            CurveTab_CurveEditor?.Dispose();
            RecentsController?.Dispose();
        }

        public void PropogateRecentsChange(string propogationSource, IEnumerable<RecentsControl.RecentItem> newRecents)
        {
            RecentsController.PropogateRecentsChange(false, newRecents);
        }

        public string Toolname => "InterpEditor";

        private bool _programmaticScrub;

        private void PlaybackScrubber_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            // Only apply scrub when the user drags the slider; skip during engine-driven time updates.
            if (!_isPlaying && !_programmaticScrub && TimelineControl.InterpData is not null)
            {
                _programmaticScrub = true;
                Scrub((float)e.NewValue);
                _programmaticScrub = false;
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string ext = Path.GetExtension(files[0]).ToLower();
                if (ext != ".u" && ext != ".upk" && ext != ".pcc" && ext != ".sfm" && ext != ".xxx" && ext != ".udk")
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
