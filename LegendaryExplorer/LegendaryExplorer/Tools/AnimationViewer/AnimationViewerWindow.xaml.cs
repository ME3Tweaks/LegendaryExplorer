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
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Path = System.IO.Path;

namespace LegendaryExplorer.Tools.AnimationViewer
{
    /// <summary>
    /// Interaction logic for AnimationExplorerWPF.xaml
    /// </summary>
    public partial class AnimationViewerWindow : TrackingNotifyPropertyChangedWindowBase
    {
        public static AnimationViewerWindow Instance;

        public AnimationRecord AnimQueuedForFocus;
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

        private enum BoolVarIndexes
        {
            RemoveOffset = 1
        }

        private enum IntVarIndexes
        {
            SquadMember = 1
        }

        public InteropTarget GameTarget { get; private set; }

        public AnimationViewerWindow() : base("Animation Viewer", true)
        {
            if (Instance != null)
            {
                throw new Exception("Can only have one instance of AnimViewer open!");
            }

            Instance = this;
            DataContext = this;
            GameTarget = GameController.GetInteropTargetForGame(MEGame.ME3);
            InitializeComponent();
            LoadCommands();
            GameTarget.GameReceiveMessage += GameController_RecieveME3Message;
            ME3OpenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            ME3OpenTimer.Tick += CheckIfME3Open;
        }

        public AnimationViewerWindow(AssetDB db, AnimationRecord AnimToFocus) : this()
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
                string dbPath = AssetDatabaseWindow.GetDBPath(MEGame.ME3);
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
            ME3OpenTimer.Stop();
            ME3OpenTimer.Tick -= CheckIfME3Open;
            GameTarget.GameReceiveMessage -= GameController_RecieveME3Message;
            DataContext = null;
            Instance = null;
        }

        private void GameController_RecieveME3Message(string msg)
        {
            if (msg == "AnimViewer string Loaded")
            {
                if (GameController.TryGetMEProcess(MEGame.ME3, out Process me3Process))
                {
                    me3Process.MainWindowHandle.RestoreAndBringToFront();
                }

                this.RestoreAndBringToFront();
                ME3OpenTimer.Start();
                ME3StartingUp = false;
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
                if (GameController.TryGetMEProcess(MEGame.ME3, out Process me3Process))
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

        private readonly DispatcherTimer ME3OpenTimer;
        private void CheckIfME3Open(object sender, EventArgs e)
        {
            if (!GameController.TryGetMEProcess(MEGame.ME3, out _))
            {
                ME3StartingUp = false;
                LoadingAnimation = false;
                EndBusy();
                ReadyToView = false;
                SelectedAnimation = null;
                instructionsTab.IsSelected = true;
                ME3OpenTimer.Stop();
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
        public bool ME3StartingUp
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
        public Requirement.RequirementCommand ME3ClosedRequirementCommand { get; set; }
        public Requirement.RequirementCommand DatabaseLoadedRequirementCommand { get; set; }
        public ICommand StartME3Command { get; set; }
        void LoadCommands()
        {
            ME3InstalledRequirementCommand = new Requirement.RequirementCommand(() => InteropHelper.IsGameInstalled(MEGame.ME3), () => InteropHelper.SelectGamePath(MEGame.ME3));
            ASILoaderInstalledRequirementCommand = new Requirement.RequirementCommand(() => InteropHelper.IsASILoaderInstalled(MEGame.ME3), () => InteropHelper.OpenASILoaderDownload(MEGame.ME3));
            InteropASIInstalledRequirementCommand = new Requirement.RequirementCommand(() => InteropHelper.IsInteropASIInstalled(MEGame.ME3), () => InteropHelper.OpenInteropASIDownload(MEGame.ME3));
            ME3ClosedRequirementCommand = new Requirement.RequirementCommand(() => InteropHelper.IsGameClosed(MEGame.ME3), () => InteropHelper.KillGame(MEGame.ME3));
            DatabaseLoadedRequirementCommand = new Requirement.RequirementCommand(IsDatabaseLoaded, TryLoadDatabase);
            StartME3Command = new GenericCommand(StartME3, AllRequirementsMet);
        }

        private bool IsDatabaseLoaded() => Enumerable.Any(Animations);

        private void TryLoadDatabase()
        {
            string dbPath = AssetDatabaseWindow.GetDBPath(MEGame.ME3);
            if (File.Exists(dbPath))
            {
                LoadDatabase(dbPath);
            }
            else
            {
                MessageBox.Show(this, "Generate an ME3 asset database in the Asset Database tool. This could take about 10 minutes.");
            }
        }

        private void LoadDatabase(string dbPath)
        {
            SetBusy("Loading Database...");
            var db = new AssetDB();
            AssetDatabaseWindow.LoadDatabase(dbPath, MEGame.ME3, db, CancellationToken.None).ContinueWithOnUIThread(prevTask =>
            {
                if (db.DatabaseVersion != AssetDatabaseWindow.dbCurrentBuild)
                {
                    MessageBox.Show(this, "ME3 Asset Database is out of date! Please regenerate it in the Asset Database tool. This could take about 10 minutes.");
                    EndBusy();
                    return;
                }
                foreach ((string fileName, int dirIndex) in db.FileList)
                {
                    FileListExtended.Add((fileName, db.ContentDir[dirIndex]));
                }
                Animations.AddRange(db.Animations.Where(a => a.IsAmbPerf == false));
                listBoxAnims.ItemsSource = Animations;
                CommandManager.InvalidateRequerySuggested();
                EndBusy();
            });
        }

        private bool AllRequirementsMet() => me3InstalledReq.IsFullfilled && asiLoaderInstalledReq.IsFullfilled && me3ClosedReq.IsFullfilled && dbLoadedReq.IsFullfilled && interopASIInstalledReq.IsFullfilled;

        private void StartME3()
        {
            ME3StartingUp = true;
            SetBusy("Creating AnimViewer Files...", () =>
            {
                ME3StartingUp = false;
            });
            Task.Run(() =>
            {
                AnimViewer.SetUpAnimStreamFile(MEGame.ME3, null, 0, "AAAME3EXPAVS1"); //placeholder for tocing
                AnimViewer.OpenMapInGame(MEGame.ME3, true, false);
                BusyText = "Launching Mass Effect 3...";
            });
        }

        #endregion

        public void LoadAnimation(AnimationRecord anim)
        {
            if (!LoadingAnimation && GameController.TryGetMEProcess(MEGame.ME3, out Process me3Process))
            {
                LoadingAnimation = true;
                SetBusy("Loading Animation", () => LoadingAnimation = false);
                int animUIndex = 0;
                string filePath = null;
                if (anim != null && Enumerable.Any(anim.Usages))
                {
                    //CameraState = ECameraState.Fixed;
                    (int fileListIndex, animUIndex, bool isMod) = anim.Usages[0];
                    (string filename, string contentdir) = FileListExtended[fileListIndex];
                    string rootPath = ME3Directory.DefaultGamePath;

                    filename = $"{filename}.*";
                    filePath = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault(f => f.Contains(contentdir));
                }
                Task.Run(() =>
                {
                    AnimViewer.SetUpAnimStreamFile(MEGame.ME3, filePath, animUIndex, "AAAME3EXPAVS1");
                    GameTarget.ME3ExecuteConsoleCommands("ce LoadAnim1");
                });
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

        /// <summary>
        /// Prevents updates to the game
        /// </summary>
        private bool noUpdate;
        private void UpdateLocation()
        {
            if (noUpdate) return;
            GameTarget.ME3ExecuteConsoleCommands(VarCmd(XPos, FloatVarIndexes.XPos),
                                                     VarCmd(YPos, FloatVarIndexes.YPos),
                                                     VarCmd(ZPos, FloatVarIndexes.ZPos),
                                                     "ce SetActorLocation");
        }

        private void UpdateRotation()
        {
            if (noUpdate) return;

            var rot = new Rotator(((float)Pitch).DegreesToUnrealRotationUnits(), ((float)Yaw).DegreesToUnrealRotationUnits(), 0).GetDirectionalVector();
            GameTarget.ME3ExecuteConsoleCommands(VarCmd(rot.X, FloatVarIndexes.XRotComponent),
                                                     VarCmd(rot.Y, FloatVarIndexes.YRotComponent),
                                                     VarCmd(rot.Z, FloatVarIndexes.ZRotComponent),
                                                     "ce SetActorRotation");
        }

        private void UpdateOffset()
        {
            if (noUpdate) return;
            GameTarget.ME3ExecuteConsoleCommands(VarCmd(RemoveOffset, BoolVarIndexes.RemoveOffset));
            LoadAnimation(SelectedAnimation);
        }

        private static string VarCmd(float value, FloatVarIndexes index)
        {
            return $"initplotmanagervaluebyindex {(int)index} float {value}";
        }

        private static string VarCmd(bool value, BoolVarIndexes index)
        {
            return $"initplotmanagervaluebyindex {(int)index} bool {(value ? 1 : 0)}";
        }

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
                    GameTarget.ME3ExecuteConsoleCommands("toggledebugcamera");
                    break;
                    //case ECameraState.Shepard when prevCameraState != ECameraState.Shepard:
                    //    LoadAnimation(SelectedAnimation, true);
                    //    break;
            }
        }

        private void QuitME3_Click(object sender, RoutedEventArgs e)
        {
            InteropHelper.KillGame(MEGame.ME3);
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
                    GameTarget.ME3ExecuteConsoleCommands("ce PauseAnimation");
                    break;
                case PlaybackState.Stopped:
                case PlaybackState.Paused:
                    playbackState = PlaybackState.Playing;
                    PlayPauseIcon = EFontAwesomeIcon.Solid_Pause;
                    GameTarget.ME3ExecuteConsoleCommands("ce PlayAnimation");
                    break;
            }
        }

        private void StopAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (noUpdate) return;
            playbackState = PlaybackState.Stopped;
            PlayPauseIcon = EFontAwesomeIcon.Solid_Play;
            GameTarget.ME3ExecuteConsoleCommands("ce StopAnimation");
        }

        private double _playRate = 1.0;

        public double PlayRate
        {
            get => _playRate;
            set
            {
                if (SetProperty(ref _playRate, value))
                {
                    if (noUpdate) return;
                    GameTarget.ME3ExecuteConsoleCommands(VarCmd((float)value, FloatVarIndexes.PlayRate));
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
                        GameTarget.ME3ExecuteConsoleCommands("ce StartCameraFollow");
                    }
                    else
                    {
                        GameTarget.ME3ExecuteConsoleCommands("ce StopCameraFollow");
                    }
                }
            }
        }

        private void UpdateCamRotation()
        {
            if (noUpdate) return;

            var rot = new Rotator(((float)CamPitch).DegreesToUnrealRotationUnits(), ((float)CamYaw).DegreesToUnrealRotationUnits(), 0).GetDirectionalVector();
            GameTarget.ME3ExecuteConsoleCommands(VarCmd(rot.X, FloatVarIndexes.CamXRotComponent),
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
                    GameTarget.ME3ExecuteConsoleCommands(VarCmd((int)value, IntVarIndexes.SquadMember), "ce LoadNewHench");
                }
            }
        }

        public static IEnumerable<ESquadMember> ESquadMemberValues => Enums.GetValues<ESquadMember>();
    }

    public enum ESquadMember : int
    {
        Liara = 1,
        Ashley = 2,
        EDI = 3,
        Garrus = 4,
        Kaidan = 5,
        James = 6,
        Javik = 7,
        Tali = 8
    }

    public enum ECameraState
    {
        Fixed,
        Free,
        //Shepard
    }
}
