using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Numerics;
using FontAwesome5;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.GameInterop;
using LegendaryExplorer.GameInterop.InteropTargets;
using LegendaryExplorer.SharedUI.Controls;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Collections.ObjectModel;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using InterpCurveVector = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurve<System.Numerics.Vector3>;
using InterpCurveFloat = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurve<float>;
using LegendaryExplorer.Tools.PackageEditor;
using System.Runtime.InteropServices;

namespace LegendaryExplorer.Tools.LiveLevelEditor
{
    /// <summary>
    /// Interaction logic for LiveLevelEditorWindow.xaml - MGAMERZ PIPE VERSION
    /// </summary>
    public partial class LELiveLevelEditorWindow : TrackingNotifyPropertyChangedWindowBase
    {
        #region Properties and Startup

        private static readonly Dictionary<MEGame, LELiveLevelEditorWindow> Instances = new();
        public static LELiveLevelEditorWindow Instance(MEGame game)
        {
            if (!game.IsLEGame() || !(GameController.GetInteropTargetForGame(game)?.ModInfo?.CanUseLLE ?? false))
                throw new ArgumentException(@"LE Live Level Editor does not support this game!", nameof(game));

            return Instances.TryGetValue(game, out LELiveLevelEditorWindow lle) ? lle : null;
        }

        private bool _readyToView;
        public bool ReadyToView
        {
            get => _readyToView;
            set
            {
                if (SetProperty(ref _readyToView, value))
                {
                    OnPropertyChanged(nameof(CamPathReadyToView));
                }
            }
        }
        
        public bool CamPathReadyToView => _readyToView && Game is MEGame.ME3;

        private bool _readyToInitialize = true;
        public bool ReadyToInitialize
        {
            get => _readyToInitialize;
            set => SetProperty(ref _readyToInitialize, value);
        }

        public MEGame Game { get; }
        public InteropTarget GameTarget { get; }

        public LELiveLevelEditorWindow(MEGame game) : base("LE Live Level Editor", true)
        {
            Game = game;
            GameTarget = GameController.GetInteropTargetForGame(game);
            if (GameTarget is null || !GameTarget.ModInfo.CanUseLLE)
            {
                throw new Exception($"{game} does not support LE Live Level Editor!");
            }

            if (Instance(game) is not null)
            {
                throw new Exception($"Can only have one instance of {game} Live Level Editor open!");
            }
            Instances[game] = this;

            GameTarget.GameReceiveMessage += GameControllerOnReceiveMessage;

            DataContext = this;
            LoadCommands();
            InitializeComponent();

            ActorEditorPanel.DataContext = this;
            //LightEditorPanel.DataContext = this;

            GameOpenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            GameOpenTimer.Tick += CheckIfGameOpen;
            RetryLoadTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            RetryLoadTimer.Tick += RetryLoadLiveEditor;
            Title = $"{Game} Live Level Editor";
            welcomeTextBlock.Text = $"Welcome to {Game} Live Level Editor";
        }

        private void LiveLevelEditor_OnClosing(object sender, CancelEventArgs e)
        {
            DisposeCamPath();
            DataContext = null;
            GameTarget.GameReceiveMessage -= GameControllerOnReceiveMessage;
            Instances.Remove(Game);
        }

        #endregion

        #region Commands
        public Requirement.RequirementCommand GameInstalledRequirementCommand { get; set; }
        public Requirement.RequirementCommand ASILoaderInstalledRequirementCommand { get; set; }
        public Requirement.RequirementCommand InteropASIInstalledRequirementCommand { get; set; }
        public ICommand LoadLiveEditorCommand { get; set; }
        public ICommand OpenPackageCommand { get; set; }
        public ICommand OpenActorInPackEdCommand { get; set; }
        public ICommand RegenActorListCommand { get; set; }

        private void LoadCommands()
        {
            GameInstalledRequirementCommand = new Requirement.RequirementCommand(() => InteropHelper.IsGameInstalled(Game), () => InteropHelper.SelectGamePath(Game));
            ASILoaderInstalledRequirementCommand = new Requirement.RequirementCommand(() => InteropHelper.IsASILoaderInstalled(Game), () => InteropHelper.OpenASILoaderDownload(Game));
            InteropASIInstalledRequirementCommand = new Requirement.RequirementCommand(() => InteropHelper.IsInteropASIInstalled(Game), () => InteropHelper.OpenInteropASIDownload(Game));
            LoadLiveEditorCommand = new GenericCommand(LoadLiveEditor, CanLoadLiveEditor);
            OpenPackageCommand = new GenericCommand(OpenPackage, CanOpenPackage);
            OpenActorInPackEdCommand = new GenericCommand(OpenActorInPackEd, CanOpenInPackEd);
            RegenActorListCommand = new GenericCommand(RegenActorList);
        }

        private void RegenActorList()
        {
            SetBusy("Building Actor List", () => { });
            DumpActors();
        }

        private bool CanOpenInPackEd() => SelectedActor != null;

        private void OpenActorInPackEd()
        {
            if (SelectedActor != null)
            {
                OpenInPackEd(SelectedActor.FileName, SelectedActor.ActorName);
            }
        }

        private bool CanOpenPackage() => listBoxPackages.SelectedItem != null;

        private void OpenPackage()
        {
            if (listBoxPackages.SelectedItem is KeyValuePair<string, ObservableCollectionExtended<ActorEntry2>> kvp)
            {
                OpenInPackEd(kvp.Key);
            }
        }

        private void OpenInPackEd(string fileName, string actorName = null)
        {
            if (MELoadedFiles.GetFilesLoadedInGame(Game).TryGetValue(fileName, out string filePath))
            {
                string entryPath = "TheWorld.PersistentLevel";
                if (actorName is not null)
                {
                    entryPath = $"{entryPath}.{actorName}";
                }

                if (WPFBase.TryOpenInExisting(filePath, out PackageEditorWindow packEd))
                {
                    packEd.GoToEntry(entryPath);
                }
                else
                {
                    PackageEditorWindow p = new();
                    p.Show();
                    p.LoadFile(filePath, 0, entryPath);
                }
            }
            else
            {
                MessageBox.Show(this, $"Cannot Find pcc named {fileName}!");
            }
        }

        private bool CanLoadLiveEditor() => ReadyToInitialize && gameInstalledReq.IsFullfilled && asiLoaderInstalledReq.IsFullfilled && interopASIInstalledReq.IsFullfilled 
                                            && GameController.TryGetMEProcess(Game, out _);

        private void LoadLiveEditor()
        {
            SetBusy("Loading Live Editor", () => RetryLoadTimer.Stop());
            //GameTarget.ExecuteConsoleCommands("ce LoadLiveEditor");
            InteropHelper.SendMessageToGame("LLE_TEST_ACTIVE", Game); // If this response works, we will know we are ready and can now start
            RetryLoadTimer.Start();
        }
        #endregion

        private void GameControllerOnReceiveMessage(string msg)
        {
            var command = msg.Split(" ");
            if (command.Length < 2)
                return;
            if (command[0] != "LIVELEVELEDITOR")
                return; // Not for us

            //Debug.WriteLine($"LLE Command: {msg}");
            var verb = command[1]; // Message Info
            // "READY" is done on first initialize and will automatically 
            if (verb == "READY") // We polled game, and found LLE is available
            {
                RetryLoadTimer.Stop();
                GameOpenTimer.Start();
                BusyText = "Building Actor list";
                actorTab.IsSelected = true;
                // Pull the initial list of actors
                DumpActors();
            }
            else if (verb == "ACTORDUMPSTART") // We're about to receive a list of actors
            {
                ActorDict.Clear();
            }
            else if (verb == "ACTORINFO") // We're about to receive a list of this many actors
            {
                BuildActor(string.Join("", command.Skip(2))); // Skip tool and verb
            }
            else if (verb == "ACTORDUMPFINISHED") // Finished dumping actors
            {
                // We have reached the end of the list
                ReadyToView = true;
                InitializeCamPath();
                EndBusy();
            }
            else if (verb == "ACTORSELECTED")
            {
                InteropHelper.SendMessageToGame("LLE_GET_ACTOR_POSDATA", Game);
            }
            else if (verb == "ACTORLOC" && command.Length == 5)
            {
                noUpdate = true;
                XPos = (int)float.Parse(command[2]);
                YPos = (int)float.Parse(command[3]);
                ZPos = (int)float.Parse(command[4]);
                noUpdate = false;
                EndBusy();
            }
            else if (verb == "ACTORROT" && command.Length == 5)
            {
                var rot = new Rotator(int.Parse(command[2]), int.Parse(command[3]), int.Parse(command[4]));
                
                noUpdate = true;
                Yaw = (int)rot.Yaw.UnrealRotationUnitsToDegrees();
                Pitch = (int)rot.Pitch.UnrealRotationUnitsToDegrees();
                Roll = (int)rot.Roll.UnrealRotationUnitsToDegrees();
                noUpdate = false;
                EndBusy();
            }
            else if (verb == "ACTORSCALE" && command.Length == 6)
            {
                noUpdate = true;
                Scale = float.Parse(command[2]);
                XScale = float.Parse(command[3]);
                YScale = float.Parse(command[4]);
                ZScale = float.Parse(command[5]);
                noUpdate = false;
                EndBusy();
            }


            if (msg == "LiveEditCamPath string CamPathComplete")
            {
                playbackState = PlaybackState.Paused;
                PlayPauseIcon = EFontAwesomeIcon.Solid_Play;
            }
        }

        /// <summary>
        /// Tells the game to dump the list of actors
        /// </summary>
        private void DumpActors()
        {
            // Reload all files in the game to make sure the list is current
            MELoadedFiles.GetFilesLoadedInGame(Game, true);
            InteropHelper.SendMessageToGame("LLE_DUMP_ACTORS", Game);
        }


        private readonly DispatcherTimer GameOpenTimer;
        private void CheckIfGameOpen(object sender, EventArgs e)
        {
            if (!GameController.TryGetMEProcess(Game, out _))
            {
                ReadyToInitialize = false;
                EndBusy();
                ReadyToView = false;
                SelectedActor = null;
                instructionsTab.IsSelected = true;
                GameOpenTimer.Stop();
            }
        }

        private readonly DispatcherTimer RetryLoadTimer;
        private void RetryLoadLiveEditor(object sender, EventArgs e)
        {
            if (ReadyToView)
            {
                EndBusy();
                RetryLoadTimer.Stop();
            }
            else
            {
                InteropHelper.SendMessageToGame("LLE_TEST_ACTIVE", Game);
            }
        }

        public ObservableDictionary<string, ObservableCollectionExtended<ActorEntry2>> ActorDict { get; } = new();

        /// <summary>
        /// Builds actor information based on what's sent from the Interop ASI
        /// </summary>
        /// <param name="actorInfoStr"></param>
        private void BuildActor(string actorInfoStr)
        {
            string[] parts = actorInfoStr.Split(':');

            // We only care about actors that loaded from a package file directly (level files)
            // GetFilesLoadedInGame will return a cached result
            if (parts.Length >= 3)
            {
                string mapName = parts[0];
                if (MELoadedFiles.GetFilesLoadedInGame(Game).ContainsKey($"{mapName}.pcc"))
                {
                    ActorDict.AddToListAt($"{mapName}.pcc", new ActorEntry2
                    {
                        DisplayText = parts.Length == 2 ? parts[1] : $"{parts[1]} ({parts[2]})",
                        ActorName = parts[1],
                        FileName = $"{mapName}.pcc"
                    });
                    return;
                }
            }
            Debug.WriteLine($"SKIPPING {actorInfoStr}");
        }

        #region BusyHost

        private bool _isBusy;

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _busyText;

        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }

        private ICommand _cancelBusyCommand;

        public ICommand CancelBusyCommand
        {
            get => _cancelBusyCommand;
            set => SetProperty(ref _cancelBusyCommand, value);
        }

        public void SetBusy(string busyText, Action onCancel = null)
        {
            BusyText = busyText;
            if (onCancel != null)
            {
                CancelBusyCommand = new GenericCommand(() =>
                {
                    onCancel();
                    EndBusy();
                }, () => true);
            }
            else
            {
                CancelBusyCommand = new DisabledCommand();
            }

            IsBusy = true;
        }

        public void EndBusy()
        {
            IsBusy = false;
        }

        #endregion

        private ActorEntry2 _selectedActor;

        public ActorEntry2 SelectedActor
        {
            get => _selectedActor;
            set
            {
                if (SetProperty(ref _selectedActor, value) && !noUpdate && value != null)
                {
                    SetBusy($"Selecting {value.ActorName}", () => { });
                    InteropHelper.SendMessageToGame($"LLE_SELECT_ACTOR {Path.GetFileNameWithoutExtension(value.FileName)} TheWorld.PersistentLevel.{value.ActorName}", Game);
                }
            }
        }

        private bool _showTraceToActor = true;
        public bool ShowTraceToActor
        {
            get => _showTraceToActor;
            set {
                if (SetProperty(ref _showTraceToActor, value))
                {
                    InteropHelper.SendMessageToGame(_showTraceToActor ? "LLE_SHOW_TRACE" : "LLE_HIDE_TRACE", Game);
                }
            }
        }

        #region Position/Rotation/Scale

        private float _xPos;
        public float XPos
        {
            get => _xPos;
            set
            {
                if (SetProperty(ref _xPos, value))
                {
                    UpdateLocation();
                }
            }
        }

        private float _yPos;
        public float YPos
        {
            get => _yPos;
            set
            {
                if (SetProperty(ref _yPos, value))
                {
                    UpdateLocation();
                }
            }
        }

        private float _zPos;
        public float ZPos
        {
            get => _zPos;
            set
            {
                if (SetProperty(ref _zPos, value))
                {
                    UpdateLocation();
                }
            }
        }

        private float _posIncrement = 10;
        public float PosIncrement
        {
            get => _posIncrement;
            set => SetProperty(ref _posIncrement, value);
        }

        private int _pitch;
        public int Pitch
        {
            get => _pitch;
            set
            {
                if (SetProperty(ref _pitch, value))
                {
                    UpdateRotation();
                }
            }
        }

        private int _yaw;
        public int Yaw
        {
            get => _yaw;
            set
            {
                if (SetProperty(ref _yaw, value))
                {
                    UpdateRotation();
                }
            }
        }

        private int _roll;
        public int Roll
        {
            get => _roll;
            set
            {
                if (SetProperty(ref _roll, value))
                {
                    UpdateRotation();
                }
            }
        }

        private int _rotIncrement = 5;
        public int RotIncrement
        {
            get => _rotIncrement;
            set => SetProperty(ref _rotIncrement, value);
        }


        private float _scale = 1;
        public float Scale
        {
            get => _scale;
            set
            {
                if (SetProperty(ref _scale, value))
                {
                    UpdateScale();
                }
            }
        }


        private float _xScale = 1;
        public float XScale
        {
            get => _xScale;
            set
            {
                if (SetProperty(ref _xScale, value))
                {
                    UpdateScale3D();
                }
            }
        }

        private float _yScale = 1;
        public float YScale
        {
            get => _yScale;
            set
            {
                if (SetProperty(ref _yScale, value))
                {
                    UpdateScale3D();
                }
            }
        }

        private float _zScale = 1;
        public float ZScale
        {
            get => _zScale;
            set
            {
                if (SetProperty(ref _zScale, value))
                {
                    UpdateScale3D();
                }
            }
        }

        private float _scaleIncrement = 0.1f;
        public float ScaleIncrement
        {
            get => _scaleIncrement;
            set => SetProperty(ref _scaleIncrement, value);
        }

        private bool noUpdate;
        private void UpdateLocation()
        {
            if (noUpdate) return;
            InteropHelper.SendMessageToGame($"LLE_UPDATE_ACTOR_POS {XPos} {YPos} {ZPos}", Game);
        }

        private void UpdateRotation()
        {
            if (noUpdate) return;

            int pitch = ((float)Pitch).DegreesToUnrealRotationUnits();
            int yaw = ((float)Yaw).DegreesToUnrealRotationUnits();
            int roll = ((float)Roll).DegreesToUnrealRotationUnits();
            InteropHelper.SendMessageToGame($"LLE_UPDATE_ACTOR_ROT {pitch} {yaw} {roll}", Game);
        }
        private void UpdateScale()
        {
            if (noUpdate) return;
            InteropHelper.SendMessageToGame($"LLE_SET_ACTOR_DRAWSCALE {Scale}", Game);
        }

        private void UpdateScale3D()
        {
            if (noUpdate) return;
            InteropHelper.SendMessageToGame($"LLE_SET_ACTOR_DRAWSCALE3D {XScale} {YScale} {ZScale}", Game);
        }
        
        #endregion

        #region CamPath
        private const int CamPath_InterpDataIDX = 63;
        private const int CamPath_LoopGateIDX = 77;
        private const int CamPath_InterpTrackMoveIDX = 70;
        private const int CamPath_FOVTrackIDX = 82;

        private IMEPackage camPathPackage;

        public ExportEntry interpTrackMove { get; set; }
        public ExportEntry fovTrackExport { get; set; }

        private FileSystemWatcher savedCamsFileWatcher;

        private EFontAwesomeIcon _playPauseImageSource = EFontAwesomeIcon.Solid_Play;
        public EFontAwesomeIcon PlayPauseIcon
        {
            get => _playPauseImageSource;
            set => SetProperty(ref _playPauseImageSource, value);
        }

        private enum PlaybackState
        {
            Playing, Stopped, Paused
        }

        private PlaybackState playbackState = PlaybackState.Stopped;

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (noUpdate) return;
            switch (playbackState)
            {
                case PlaybackState.Playing:
                    pauseCam();
                    break;
                case PlaybackState.Stopped:
                case PlaybackState.Paused:
                    playCam();
                    break;
            }
        }

        private void playCam()
        {
            playbackState = PlaybackState.Playing;
            PlayPauseIcon = EFontAwesomeIcon.Solid_Pause;
            GameTarget.ExecuteConsoleCommands("ce playcam");
        }

        private void pauseCam()
        {
            playbackState = PlaybackState.Paused;
            PlayPauseIcon = EFontAwesomeIcon.Solid_Play;
            GameTarget.ExecuteConsoleCommands("ce pausecam");
        }

        private bool _shouldLoop;

        public bool ShouldLoop
        {
            get => _shouldLoop;
            set
            {
                if (SetProperty(ref _shouldLoop, value) && !noUpdate)
                {
                    GameTarget.ExecuteConsoleCommands(_shouldLoop ? "ce loopcam" : "ce noloopcam");
                }
            }
        }

        private void StopAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (noUpdate) return;
            playbackState = PlaybackState.Stopped;
            PlayPauseIcon = EFontAwesomeIcon.Solid_Play;
            GameTarget.ExecuteConsoleCommands("ce stopcam");
        }

        private void SaveCamPath(object sender, RoutedEventArgs e)
        {
            camPathPackage.GetUExport(CamPath_InterpDataIDX).WriteProperty(new FloatProperty(Math.Max(Move_CurveEditor.Time, FOV_CurveEditor.Time), "InterpLength"));
            camPathPackage.GetUExport(CamPath_LoopGateIDX).WriteProperty(new BoolProperty(ShouldLoop, "bOpen"));
            camPathPackage.Save();
            LiveEditHelper.PadCamPathFile(Game);
            GameTarget.ExecuteConsoleCommands("ce stopcam", "ce LoadCamPath");
            playbackState = PlaybackState.Stopped;
            PlayPauseIcon = EFontAwesomeIcon.Solid_Play;
        }

        private void InitializeCamPath()
        {
            if (Game is not MEGame.ME3)
            {
                return;
            }
            camPathPackage = MEPackageHandler.OpenMEPackage(LiveEditHelper.CamPathFilePath(Game));
            interpTrackMove = camPathPackage.GetUExport(CamPath_InterpTrackMoveIDX);
            fovTrackExport = camPathPackage.GetUExport(CamPath_FOVTrackIDX);
            ReloadCurveEdExports();

            savedCamsFileWatcher = new FileSystemWatcher(MEDirectories.GetExecutableFolderPath(Game), "savedCams") { NotifyFilter = NotifyFilters.LastWrite };
            savedCamsFileWatcher.Changed += SavedCamsFileWatcher_Changed;
            savedCamsFileWatcher.EnableRaisingEvents = true;

            ReloadCams();
        }

        private void ReloadCurveEdExports()
        {
            Move_CurveEditor.LoadExport(interpTrackMove);
            FOV_CurveEditor.LoadExport(fovTrackExport);
        }

        private void SavedCamsFileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            //needs to be on the UI thread 
            Dispatcher.BeginInvoke(new Action(ReloadCams));
        }

        private void ReloadCams() => SavedCams.ReplaceAll(LiveEditHelper.ReadSavedCamsFile());

        private void DisposeCamPath()
        {
            camPathPackage?.Dispose();
            camPathPackage = null;
            savedCamsFileWatcher?.Dispose();
        }

        public ObservableCollectionExtended<POV> SavedCams { get; } = new();

        private void AddSavedCamAsKey(object sender, RoutedEventArgs e)
        {
            string timeStr = PromptDialog.Prompt(this, "Add key at what time?", "", "0", true);

            if (float.TryParse(timeStr, out float time))
            {
                var pov = (POV)((System.Windows.Controls.Button)sender).DataContext;

                var props = interpTrackMove.GetProperties();
                var interpCurvePos = InterpCurveVector.FromStructProperty(props.GetProp<StructProperty>("PosTrack"), Game);
                var interpCurveRot = InterpCurveVector.FromStructProperty(props.GetProp<StructProperty>("EulerTrack"), Game);

                interpCurvePos.AddPoint(time, pov.Position, Vector3.Zero, Vector3.Zero, EInterpCurveMode.CIM_CurveUser);
                interpCurveRot.AddPoint(time, pov.Rotation, Vector3.Zero, Vector3.Zero, EInterpCurveMode.CIM_CurveUser);

                props.AddOrReplaceProp(interpCurvePos.ToStructProperty(Game, "PosTrack"));
                props.AddOrReplaceProp(interpCurveRot.ToStructProperty(Game, "EulerTrack"));
                interpTrackMove.WriteProperties(props);

                var floatTrack = InterpCurveFloat.FromStructProperty(fovTrackExport.GetProperty<StructProperty>("FloatTrack"), Game);
                floatTrack.AddPoint(time, pov.FOV, 0, 0, EInterpCurveMode.CIM_CurveUser);
                fovTrackExport.WriteProperty(floatTrack.ToStructProperty(Game, "FloatTrack"));

                ReloadCurveEdExports();
            }
        }

        private void ClearKeys(object sender, RoutedEventArgs e)
        {
            if (MessageBoxResult.Yes == MessageBox.Show("Are you sure you want to clear all keys from the Curve Editors?", "Clear Keys confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning))
            {
                var props = interpTrackMove.GetProperties();
                props.AddOrReplaceProp(new InterpCurveVector().ToStructProperty(Game, "PosTrack"));
                props.AddOrReplaceProp(new InterpCurveVector().ToStructProperty(Game, "EulerTrack"));
                interpTrackMove.WriteProperties(props);

                fovTrackExport.WriteProperty(new InterpCurveFloat().ToStructProperty(Game, "FloatTrack"));

                ReloadCurveEdExports();
            }
        }

        #endregion
    }
    public class ActorEntry2
    {
        public string FileName;
        public string DisplayText { get; init; }

        public string ActorName;
    }
}
