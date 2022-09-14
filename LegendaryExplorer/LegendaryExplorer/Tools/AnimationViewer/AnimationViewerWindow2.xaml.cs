using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Numerics;
using FontAwesome5;
using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.GameInterop;
using LegendaryExplorer.GameInterop.InteropTargets;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.Controls;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Path = System.IO.Path;

namespace LegendaryExplorer.Tools.AnimationViewer
{
    /// <summary>
    /// ASI-based Animation Viewer - Mgamerz
    /// </summary>
    public partial class AnimationViewerWindow2 : TrackingNotifyPropertyChangedWindowBase
    {
        /// <summary>
        /// If game has told us it's ready for anim commands
        /// </summary>
        private bool Initialized;
        /// <summary>
        /// Game this instance is for
        /// </summary>
        public MEGame Game { get; set; }
        private static readonly Dictionary<MEGame, AnimationViewerWindow2> Instances = new();
        public static AnimationViewerWindow2 Instance(MEGame game)
        {
            if (!GameController.GetInteropTargetForGame(game)?.CanUseLLE ?? true)
                throw new ArgumentException(@"Animation Viewer 2 does not support this game!", nameof(game));

            return Instances.TryGetValue(game, out var lle) ? lle : null;
        }

        /// <summary>
        /// The bound list of actor options
        /// </summary>
        public ObservableCollectionExtended<string> ActorOptions { get; } = new();

        private string _selectedActor;
        public string SelectedActor
        {
            get => _selectedActor;
            set
            {
                if (SetProperty(ref _selectedActor, value))
                {
                    // Change pawn.
                    if (Game == MEGame.LE1)
                    {
                        PrepChangeLE1Actor(value);
                        return;
                    }

                    Debug.WriteLine("Pawn change not implemented for this game");
                }
            }
        }

        private bool _greenScreenOn;

        public bool GreenScreenOn
        {
            get => _greenScreenOn;
            set
            {
                if (SetProperty(ref _greenScreenOn, value))
                {
                    if (value)
                    {
                        InteropHelper.SendMessageToGame("CAUSEEVENT GreenScreenOn", Game);
                    }
                    else
                    {
                        InteropHelper.SendMessageToGame("CAUSEEVENT GreenScreenOff", Game);
                    }
                }
            }
        }

        #region LE1 SPECIFIC
        private void PrepChangeLE1Actor(string actorFullPath)
        {
            if (!Initialized)
                return; // We haven't received the init message yet
            var packageName = actorFullPath.Split('.').FirstOrDefault(); // 1
            var memoryName = string.Join('.', actorFullPath.Split('.').Skip(1)); // The rest

            // Tell game to load package and change the pawn
            InteropHelper.SendMessageToGame($"ANIMV_CHANGE_PAWN {packageName}.{memoryName}", Game);
            InteropHelper.SendMessageToGame($"CAUSEEVENT ChangeActor", Game);
        }

        #endregion

        public AnimationRecord AnimQueuedForFocus;

        /// <summary>
        /// Plot-indexes in kismet that can be changed to change stuff in-game. The value is the plot index for the specified type
        /// </summary>
        private enum FloatVarIndexes
        {
            XPos = 1,
            YPos = 2,
            ZPos = 3,
            XRotComponent = 4,
            YRotComponent = 5,
            ZRotComponent = 6,
            PlayRate = 7,
            CamXRotComponent = 8,
            CamYRotComponent = 9,
            CamZRotComponent = 10,
        }

        /// <summary>
        /// Plot-indexes in kismet that can be changed to change stuff in-game. The value is the plot index for the specified type
        /// </summary>
        private enum BoolVarIndexes
        {
            RemoveOffset = 1
        }

        /// <summary>
        /// Plot-indexes in kismet that can be changed to change stuff in-game. The value is the plot index for the specified type
        /// </summary>
        private enum IntVarIndexes
        {
            SquadMember = 1
        }

        public InteropTarget GameTarget { get; private set; }

        public AnimationViewerWindow2(MEGame game) : base("Animation Viewer 2", true)
        {
            if (Instance(game) is not null)
            {
                throw new Exception($"Can only have one instance of {game} Animation Viewer 2 open!");
            }
            Instances[game] = this;

            Game = game;
            GameTarget = GameController.GetInteropTargetForGame(game);
            InitializeComponent();
            LoadCommands();
            GameTarget.GameReceiveMessage += ReceivedGameMessage;
            GameOpenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            GameOpenTimer.Tick += CheckIfGameOpen;
        }

        public AnimationViewerWindow2(MEGame game, AssetDB db, AnimationRecord AnimToFocus) : this(game)
        {
            AnimQueuedForFocus = AnimToFocus;
            foreach ((string fileName, int dirIndex) in db.FileList)
            {
                FileListExtended.Add((fileName, db.ContentDir[dirIndex]));
            }
            Animations.AddRange(db.Animations.Where(a => a.IsAmbPerf == false));

        }

        private void AnimationExplorerWPF_Loaded(object sender, RoutedEventArgs e)
        {
            if (Animations.IsEmpty())
            {
                string dbPath = AssetDatabaseWindow.GetDBPath(Game);
                if (File.Exists(dbPath))
                {
                    LoadDatabase(dbPath);
                }
            }
            else
            {
                listBoxAnims.ItemsSource = Animations;
            }
        }

        private void AnimationExplorerWPF_OnClosing(object sender, CancelEventArgs e)
        {
            //if (!GameController.TryGetME3Process(out _))
            //{
            //    string asiPath = InteropHelper.GetInteropAsiWritePath();
            //    if (File.Exists(asiPath))
            //    {
            //        File.Delete(asiPath);
            //    }
            //}
            GameOpenTimer.Stop();
            GameOpenTimer.Tick -= CheckIfGameOpen;
            GameTarget.GameReceiveMessage -= ReceivedGameMessage;
            DataContext = null;
            Instances.Remove(Game);
        }

        private void ReceivedGameMessage(string msg)
        {
            // Check message is for us
            if (!msg.StartsWith("ANIMVIEWER"))
                return;
            Debug.WriteLine($"Message: {msg}");
            if (msg == "ANIMVIEWER LOADED")
            {
                Initialized = true;
                InteropHelper.SendMessageToGame("ANIMV_DISALLOW_WINDOW_PAUSE", Game);

                // Load the initial pawn.
                if (Game == MEGame.LE1)
                {
                    SelectedActor = ActorOptions.FirstOrDefault();
                }

                if (GameController.TryGetMEProcess(Game, out Process me3Process))
                {
                    me3Process.MainWindowHandle.RestoreAndBringToFront();
                }

                this.RestoreAndBringToFront();
                GameOpenTimer.Start();
                GameStartingUp = false;
                LoadingAnimation = false;
                ReadyToView = true;
                animTab.IsSelected = true;

                noUpdate = true;
                ShouldFollowActor = false;
                SelectedSquadMember = ESquadMember.Liara;
                noUpdate = false;

                EndBusy();
                if (AnimQueuedForFocus != null)
                {
                    SelectedAnimation = Animations.FirstOrDefault(a => a.AnimSequence == AnimQueuedForFocus.AnimSequence);
                    AnimQueuedForFocus = null;
                }
            }
            else if (msg == "ANIMVIEWER ANIMSTARTED")
            {
                LoadingAnimation = false;
                IsBusy = false; // Not busy
            }
            else if (msg.StartsWith("AnimViewer string AnimLoaded"))
            {
                Vector3 pos = defaultPosition;
                if (msg.IndexOf("vector") is int idx && idx > 0 &&
                    msg.Substring(idx + 7).Split(' ') is string[] strings && strings.Length == 3)
                {
                    var floats = new float[3];
                    for (int i = 0; i < 3; i++)
                    {
                        if (float.TryParse(strings[i], out float f))
                        {
                            floats[i] = f;
                        }
                        else
                        {
                            defaultPosition.CopyTo(floats);
                            break;
                        }
                    }

                    pos = new Vector3(floats[0], floats[1], floats[2]);
                }
                noUpdate = true;
                XPos = (int)pos.X;
                YPos = (int)pos.Y;
                ZPos = (int)pos.Z;
                Yaw = 180;
                Pitch = 0;
                playbackState = PlaybackState.Playing;
                PlayPauseIcon = EFontAwesomeIcon.Solid_Pause;
                noUpdate = false;
                if (GameController.TryGetMEProcess(Game, out Process me3Process))
                {
                    me3Process.MainWindowHandle.RestoreAndBringToFront();
                }
                this.RestoreAndBringToFront();
                LoadingAnimation = false;
                EndBusy();
            }
            else if (msg == "AnimViewer string HenchLoaded")
            {
                LoadAnimation(SelectedAnimation);
            }
        }

        private readonly DispatcherTimer GameOpenTimer;
        private void CheckIfGameOpen(object sender, EventArgs e)
        {
            if (!GameController.TryGetMEProcess(Game, out _))
            {
                GameStartingUp = false;
                LoadingAnimation = false;
                EndBusy();
                ReadyToView = false;
                SelectedAnimation = null;
                instructionsTab.IsSelected = true;
                GameOpenTimer.Stop();
            }
        }

        public List<AnimationRecord> Animations { get; } = new();
        private readonly List<(string fileName, string directory)> FileListExtended = new();

        private AnimationRecord _selectedAnimation;

        public AnimationRecord SelectedAnimation
        {
            get => _selectedAnimation;
            set
            {
                if (value != _selectedAnimation && SetProperty(ref _selectedAnimation, value) && value != null && !IsBusy)
                {
                    LoadAnimation(value);
                }
            }
        }

        private bool _readyToView;
        public bool ReadyToView
        {
            get => _readyToView;
            set => SetProperty(ref _readyToView, value);
        }

        private bool _mE3StartingUp;
        public bool GameStartingUp
        {
            get => _mE3StartingUp;
            set => SetProperty(ref _mE3StartingUp, value);
        }

        private bool _loadingAnimation;

        public bool LoadingAnimation
        {
            get => _loadingAnimation;
            set => SetProperty(ref _loadingAnimation, value);
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
                    IsBusy = false;
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

        #region Commands

        public Requirement.RequirementCommand ME3InstalledRequirementCommand { get; set; }
        public Requirement.RequirementCommand ASILoaderInstalledRequirementCommand { get; set; }
        public Requirement.RequirementCommand InteropASIInstalledRequirementCommand { get; set; }
        public Requirement.RequirementCommand GameClosedRequirementCommand { get; set; }
        public Requirement.RequirementCommand DatabaseLoadedRequirementCommand { get; set; }
        public ICommand StartGameCommand { get; set; }
        void LoadCommands()
        {
            ME3InstalledRequirementCommand = new Requirement.RequirementCommand(() => InteropHelper.IsGameInstalled(Game), () => InteropHelper.SelectGamePath(Game));
            ASILoaderInstalledRequirementCommand = new Requirement.RequirementCommand(() => true /*InteropHelper.IsASILoaderInstalled(Game)*/, () => InteropHelper.OpenASILoaderDownload(Game));
            InteropASIInstalledRequirementCommand = new Requirement.RequirementCommand(() => true /*InteropHelper.IsInteropASIInstalled(Game)*/, () => InteropHelper.OpenInteropASIDownload(Game));

            // I don't think game even needs to be closed
            GameClosedRequirementCommand = new Requirement.RequirementCommand(() => InteropHelper.IsGameClosed(Game), () => InteropHelper.KillGame(Game));
            DatabaseLoadedRequirementCommand = new Requirement.RequirementCommand(IsDatabaseLoaded, TryLoadDatabase);
            StartGameCommand = new GenericCommand(StartGame, AllRequirementsMet);
        }

        private bool IsDatabaseLoaded() => Enumerable.Any(Animations);

        private void TryLoadDatabase()
        {
            string dbPath = AssetDatabaseWindow.GetDBPath(Game);
            if (File.Exists(dbPath))
            {
                LoadDatabase(dbPath);
            }
            else
            {
                MessageBox.Show(this, $"Generate the asset database for {Game} in the Asset Database tool first.");
            }
        }

        private async void LoadDatabase(string dbPath)
        {
            SetBusy("Loading Database...");
            var db = new AssetDB();
            await AssetDatabaseWindow.LoadDatabase(dbPath, Game, db, CancellationToken.None).ContinueWithOnUIThread(prevTask =>
            {
                if (db.DatabaseVersion != AssetDatabaseWindow.dbCurrentBuild)
                {
                    MessageBox.Show(this, $"{Game} Asset Database is out of date! Please regenerate it in the Asset Database tool. This could take about 10 minutes.");
                    EndBusy();
                    return;
                }

                foreach ((string fileName, int dirIndex) in db.FileList)
                {
                    FileListExtended.Add((fileName, db.ContentDir[dirIndex]));
                }

                Animations.AddRange(db.Animations.Where(a => a.IsAmbPerf == false));
                listBoxAnims.ItemsSource = Animations;
            }).ContinueWith(x =>
            {
                if (Game == MEGame.LE1)
                {
                    // Object Instance DB doesn't include class name, and AssetDB doesn't store name of usage
                    // Object -> Package containing it
                    var foundActorTypes = new Dictionary<string, string>();
                    foreach (var cr in db.ClassRecords)
                    {
                        if (cr.Class == "BioPawnChallengeScaledType")
                        {
                            // Types of actors that can be spawned
                            foreach (var usage in cr.Usages)
                            {

                                var file = db.FileList[usage.FileKey];
                                if (MELoadedFiles.GetFilesLoadedInGame(Game).TryGetValue(file.FileName, out var fullPath))
                                {
                                    using var p = MEPackageHandler.UnsafePartialLoad(fullPath, x => false); // Just load the tables
                                    var exp = p.GetUExport(usage.UIndex);
                                    if (!exp.IsDefaultObject && !foundActorTypes.ContainsKey(exp.InstancedFullPath))
                                    {
                                        foundActorTypes[exp.InstancedFullPath] = file.FileName; // Set in dictionary
                                    }
                                }
                            }
                        }
                        // Must be done on UI thread
                        Application.Current.Dispatcher.Invoke(() => ActorOptions.ReplaceAll(foundActorTypes.Select(x => $"{Path.GetFileNameWithoutExtension(x.Value)}.{x.Key}")));
                    }
                }
            }).ContinueWithOnUIThread(x =>
            {
                // Todo: Other games.
                CommandManager.InvalidateRequerySuggested();
                EndBusy();
            });

        }

        private bool AllRequirementsMet() => me3InstalledReq.IsFullfilled && asiLoaderInstalledReq.IsFullfilled && me3ClosedReq.IsFullfilled && dbLoadedReq.IsFullfilled && interopASIInstalledReq.IsFullfilled;

        private void StartGame()
        {

            // This doesn't work...
            //InteropHelper.SendMessageToGame($"CACHEPACKAGE {animViewerStagePath}", Game);
            //Thread.Sleep(50); // Give it a sec...
            //GameTarget.ModernExecuteConsoleCommand($"at {animLevelBaseName}");

            //GameStartingUp = true;
            //SetBusy("Creating AnimViewer Files...", () =>
            //{
            //    GameStartingUp = false;
            //});
            Task.Run(() =>
            {
                var animLevelBaseName = $"{Game}LiveAnimViewerStage";
                var animViewerStagePath = Path.Combine(AppDirectories.ExecFolder, $"{animLevelBaseName}.pcc");

                using var mapPcc = MEPackageHandler.OpenMEPackage(animViewerStagePath);
                AnimViewer.OpenMapInGame(mapPcc, true, false, animLevelBaseName);
                BusyText = "Launching game...";

                AnimViewer.SetUpAnimStreamFile(Game, null, 0, $"{Game}AnimViewer_StreamAnim"); //placeholder for making sure file is in TOC
            });
        }


        #endregion

        public void LoadAnimation(AnimationRecord anim)
        {
            if (!LoadingAnimation && GameController.TryGetMEProcess(Game, out Process me3Process))
            {
                LoadingAnimation = true;
                SetBusy("Loading Animation", () => LoadingAnimation = false);
                int animUIndex = 0;
                string filePath = null;
                if (anim != null && Enumerable.Any(anim.Usages))
                {
                    //CameraState = ECameraState.Fixed;
                    int fileListIndex;
                    bool isMod;
                    (fileListIndex, animUIndex, isMod) = anim.Usages[0];
                    (string filename, string contentdir) = FileListExtended[fileListIndex];
                    string rootPath = MEDirectories.GetDefaultGamePath(Game);
                    filename = $"{filename}.*";
                    filePath = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault(f => f.Contains(contentdir));
                }

                if (filePath != null)
                {
                    Task.Run(() =>
                    {
                        var streamAnimFile = $"{Game}AnimViewer_StreamAnim";
                        InteropHelper.SendMessageToGame($"STREAMLEVELOUT {streamAnimFile}", Game);
                        AnimViewer.SetUpAnimStreamFile(Game, filePath, animUIndex, streamAnimFile);
                        Thread.Sleep(50); // Give it just a tiny bit of time to stream out - this can probably be fixed with states and handshake from game - I'm kinda lazy
                        InteropHelper.SendMessageToGame($"STREAMLEVELIN {streamAnimFile}", Game);
                    });
                }
            }
        }

        #region Position/Rotation
        private static readonly Vector3 defaultPosition = new(0f, 0f, 85f);

        private int _xPos = (int)defaultPosition.X;
        public int XPos
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

        private int _yPos = (int)defaultPosition.Y;
        public int YPos
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

        private int _zPos = (int)defaultPosition.Z;
        public int ZPos
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

        private int _yaw = 180;
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

        private bool _removeOffset;
        public bool RemoveOffset
        {
            get => _removeOffset;
            set
            {
                if (SetProperty(ref _removeOffset, value))
                {
                    UpdateOffset();
                }
            }
        }

        private bool noUpdate;
        private void UpdateLocation()
        {
            if (noUpdate) return;

            // Set values and then call update
            GameTarget.ModernExecuteConsoleCommand(VarCmd(XPos, FloatVarIndexes.XPos));
            GameTarget.ModernExecuteConsoleCommand(VarCmd(YPos, FloatVarIndexes.YPos));
            GameTarget.ModernExecuteConsoleCommand(VarCmd(ZPos, FloatVarIndexes.ZPos));
            InteropHelper.CauseEvent("UpdateActorPos", Game);
        }

        private void UpdateRotation()
        {
            if (noUpdate) return;

            var rot = new Rotator(((float)Pitch).DegreesToUnrealRotationUnits(), ((float)Yaw).DegreesToUnrealRotationUnits(), 0).GetDirectionalVector();
            GameTarget.ExecuteConsoleCommands(VarCmd(rot.X, FloatVarIndexes.XRotComponent),
                                                     VarCmd(rot.Y, FloatVarIndexes.YRotComponent),
                                                     VarCmd(rot.Z, FloatVarIndexes.ZRotComponent),
                                                     "ce SetActorRotation");
        }

        private void UpdateOffset()
        {
            if (noUpdate) return;
            GameTarget.ExecuteConsoleCommands(VarCmd(RemoveOffset, BoolVarIndexes.RemoveOffset));
            LoadAnimation(SelectedAnimation);
        }

        /// <summary>
        /// Sets a story float variable
        /// </summary>
        /// <param name="value"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private static string VarCmd(float value, FloatVarIndexes index)
        {
            return $"initplotmanagervaluebyindex {(int)index} float {value}";
        }

        /// <summary>
        /// Sets a story boolean variable 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private static string VarCmd(bool value, BoolVarIndexes index)
        {
            return $"initplotmanagervaluebyindex {(int)index} bool {(value ? 1 : 0)}";
        }

        /// <summary>
        /// Sets a story integer variable
        /// </summary>
        /// <param name="value"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private static string VarCmd(int value, IntVarIndexes index)
        {
            return $"initplotmanagervaluebyindex {(int)index} int {value}";
        }

        private void SetDefaultPosition_Click(object sender, RoutedEventArgs e)
        {
            noUpdate = true;
            RemoveOffset = false;
            XPos = (int)defaultPosition.X;
            YPos = (int)defaultPosition.Y;
            ZPos = (int)defaultPosition.Z;
            noUpdate = false;
            UpdateLocation();
        }

        private void ResetRotation_Click(object sender, RoutedEventArgs e)
        {
            noUpdate = true;
            Pitch = 0;
            Yaw = 180;
            noUpdate = false;
            UpdateRotation();
        }

        #endregion

        private ECameraState prevCameraState;
        private ECameraState _cameraState;
        public ECameraState CameraState
        {
            get => _cameraState;
            set
            {
                if (SetProperty(ref _cameraState, value))
                {
                    UpdateCameraState();
                    prevCameraState = _cameraState;
                }
            }
        }

        private void UpdateCameraState()
        {
            if (noUpdate) return;
            switch (CameraState)
            {
                //case ECameraState.Fixed when prevCameraState == ECameraState.Shepard:
                //case ECameraState.Free when prevCameraState == ECameraState.Shepard:
                //    LoadAnimation(SelectedAnimation);
                //    break;
                case ECameraState.Fixed when prevCameraState == ECameraState.Free:
                case ECameraState.Free:
                    if (Game != MEGame.LE1)
                    {
                        GameTarget.ModernExecuteConsoleCommand("toggledebugcamera"); // This will kill LE1
                    }

                    break;
                    //case ECameraState.Shepard when prevCameraState != ECameraState.Shepard:
                    //    LoadAnimation(SelectedAnimation, true);
                    //    break;
            }
        }

        private void QuitME3_Click(object sender, RoutedEventArgs e)
        {
            InteropHelper.KillGame(Game);
        }

        #region Playback

        private EFontAwesomeIcon _playPauseImageSource = EFontAwesomeIcon.Solid_Pause;
        public EFontAwesomeIcon PlayPauseIcon
        {
            get => _playPauseImageSource;
            set => SetProperty(ref _playPauseImageSource, value);
        }

        private enum PlaybackState
        {
            Playing, Stopped, Paused
        }

        private PlaybackState playbackState;

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (noUpdate) return;
            switch (playbackState)
            {
                case PlaybackState.Playing:
                    playbackState = PlaybackState.Paused;
                    PlayPauseIcon = EFontAwesomeIcon.Solid_Play;
                    InteropHelper.CauseEvent("re_PauseAnimation", Game);
                    break;
                case PlaybackState.Stopped:
                case PlaybackState.Paused:
                    playbackState = PlaybackState.Playing;
                    PlayPauseIcon = EFontAwesomeIcon.Solid_Pause;
                    InteropHelper.CauseEvent("re_StartAnimation", Game);
                    break;
            }
        }

        private void StopAnimation_Click(object sender, RoutedEventArgs e)
        {

            if (noUpdate) return;
            playbackState = PlaybackState.Stopped;
            PlayPauseIcon = EFontAwesomeIcon.Solid_Play;
            InteropHelper.CauseEvent("re_StopAnimation", Game);
        }

        private float _playRate = 1.0f;

        public float PlayRate
        {
            get => _playRate;
            set
            {
                if (SetProperty(ref _playRate, value))
                {
                    if (noUpdate) return;
                    GameTarget.ModernExecuteConsoleCommand(VarCmd(PlayRate, FloatVarIndexes.PlayRate));
                }
            }
        }

        #endregion

        private void SearchBox_OnTextChanged(SearchBox sender, string newtext)
        {
            listBoxAnims.ItemsSource = Animations.Where(anim => anim.AnimSequence.Contains(newtext, StringComparison.OrdinalIgnoreCase));
        }

        #region Camera

        private int _camYaw = -10;
        public int CamYaw
        {
            get => _camYaw;
            set
            {
                if (SetProperty(ref _camYaw, value))
                {
                    UpdateCamRotation();
                }
            }
        }

        private int _camPitch;
        public int CamPitch
        {
            get => _camPitch;
            set
            {
                if (SetProperty(ref _camPitch, value))
                {
                    UpdateCamRotation();
                }
            }
        }

        private bool _shouldFollowActor;
        public bool ShouldFollowActor
        {
            get => _shouldFollowActor;
            set
            {
                if (SetProperty(ref _shouldFollowActor, value) && !noUpdate)
                {
                    if (value)
                    {
                        GameTarget.ExecuteConsoleCommands("ce StartCameraFollow");
                    }
                    else
                    {
                        GameTarget.ExecuteConsoleCommands("ce StopCameraFollow");
                    }
                }
            }
        }



        private void UpdateCamRotation()
        {
            if (noUpdate) return;

            var rot = new Rotator(((float)CamPitch).DegreesToUnrealRotationUnits(), ((float)CamYaw).DegreesToUnrealRotationUnits(), 0).GetDirectionalVector();
            GameTarget.ExecuteConsoleCommands(VarCmd(rot.X, FloatVarIndexes.CamXRotComponent),
                                                     VarCmd(rot.Y, FloatVarIndexes.CamYRotComponent),
                                                     VarCmd(rot.Z, FloatVarIndexes.CamZRotComponent),
                                                     "ce SetCameraRotation");
        }

        private void ResetCamRotation_Click(object sender, RoutedEventArgs e)
        {
            noUpdate = true;
            CamPitch = 0;
            CamYaw = 0;
            noUpdate = false;
            UpdateCamRotation();
        }

        #endregion

        private ESquadMember _selectedSquadMember = ESquadMember.Liara;

        public ESquadMember SelectedSquadMember
        {
            get => _selectedSquadMember;
            set
            {
                if (SetProperty(ref _selectedSquadMember, value) && !noUpdate)
                {
                    GameTarget.ExecuteConsoleCommands(VarCmd((int)value, IntVarIndexes.SquadMember), "ce LoadNewHench");
                }
            }
        }

        public static IEnumerable<ESquadMember> ESquadMemberValues => Enums.GetValues<ESquadMember>();
    }
}
