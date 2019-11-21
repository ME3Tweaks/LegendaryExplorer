using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
using FontAwesome5;
using ME3Explorer.AssetDatabase;
using ME3Explorer.AutoTOC;
using ME3Explorer.GameInterop;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.BinaryConverters;
using Microsoft.Win32;
using SharpDX;
using Path = System.IO.Path;

namespace ME3Explorer.AnimationExplorer
{
    /// <summary>
    /// Interaction logic for AnimationExplorerWPF.xaml
    /// </summary>
    public partial class AnimationExplorerWPF : NotifyPropertyChangedWindowBase
    {
        public static AnimationExplorerWPF Instance;

        private const string Me3ExplorerinteropAsiName = "ME3ExplorerInterop.asi";

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

        public AnimationExplorerWPF()
        {
            if (Instance != null)
            {
                throw new Exception("Can only have one instance of AnimViewer open!");
            }

            Instance = this;
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Animation Viewer", new WeakReference(this));
            DataContext = this;
            InitializeComponent();
            LoadCommands();
            GameController.RecieveME3Message += GameController_RecieveME3Message;
            ME3OpenTimer = new DispatcherTimer {Interval = TimeSpan.FromSeconds(1)};
            ME3OpenTimer.Tick += CheckIfME3Open;
        }

        private void AnimationExplorerWPF_Loaded(object sender, RoutedEventArgs e)
        {
            string dbPath = AssetDB.GetDBPath(MEGame.ME3);
            if (File.Exists(dbPath))
            {
                LoadDatabase(dbPath);
            }
        }

        private void AnimationExplorerWPF_OnClosing(object sender, CancelEventArgs e)
        {
            if (!GameController.TryGetME3Process(out _))
            {
                string asiPath = GetInteropAsiWritePath();
                if (File.Exists(asiPath))
                {
                    File.Delete(asiPath);
                }
            }
            ME3OpenTimer.Stop();
            ME3OpenTimer.Tick -= CheckIfME3Open;
            GameController.RecieveME3Message -= GameController_RecieveME3Message;
            DataContext = null;
            Instance = null;
        }

        private void GameController_RecieveME3Message(string msg)
        {
            if (msg == "AnimViewer string Loaded")
            {
                if (GameController.TryGetME3Process(out Process me3Process))
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
                            return;
                        }
                    }
                    pos = new Vector3(floats);
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
                if (GameController.TryGetME3Process(out Process me3Process))
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
            if (!GameController.TryGetME3Process(out _))
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

        public List<Animation> Animations { get; } = new List<Animation>();
        private readonly List<(string fileName, string directory)> FileListExtended = new List<(string fileName, string directory)>();

        private Animation _selectedAnimation;

        public Animation SelectedAnimation
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

        public RequirementCommand ME3InstalledRequirementCommand { get; set; }
        public RequirementCommand ASILoaderInstalledRequirementCommand { get; set; }
        public RequirementCommand ME3ClosedRequirementCommand { get; set; }
        public RequirementCommand DatabaseLoadedRequirementCommand { get; set; }
        public ICommand StartME3Command { get; set; }
        void LoadCommands()
        {
            ME3InstalledRequirementCommand = new RequirementCommand(IsME3Installed, SelectME3Path);
            ASILoaderInstalledRequirementCommand = new RequirementCommand(IsASILoaderInstalled, OpenASILoaderDownload);
            ME3ClosedRequirementCommand = new RequirementCommand(IsME3Closed, KillME3);
            DatabaseLoadedRequirementCommand = new RequirementCommand(IsDatabaseLoaded, TryLoadDatabase);
            StartME3Command = new GenericCommand(StartME3, AllRequirementsMet);
        }

        private bool IsDatabaseLoaded() => Animations.Any();

        private void TryLoadDatabase()
        {
            string dbPath = AssetDB.GetDBPath(MEGame.ME3);
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
            PropsDataBase db = new PropsDataBase();
            AssetDB.LoadDatabase(dbPath, MEGame.ME3, db, CancellationToken.None).ContinueWithOnUIThread(prevTask =>
            {
                if (db.DataBaseversion != AssetDB.dbCurrentBuild)
                {
                    MessageBox.Show(this, "ME3 Asset Database is out of date! Please regenerate it in the Asset Database tool. This could take about 10 minutes.");
                    EndBusy();
                    return;
                }
                foreach ((string fileName, int dirIndex) in db.FileList)
                {
                    FileListExtended.Add((fileName, db.ContentDir[dirIndex]));
                }
                Animations.AddRange(db.Animations);
                listBoxAnims.ItemsSource = Animations;
                CommandManager.InvalidateRequerySuggested();
                EndBusy();
            });
        }

        private bool AllRequirementsMet() => me3InstalledReq.IsFullfilled && asiLoaderInstalledReq.IsFullfilled && me3ClosedReq.IsFullfilled && dbLoadedReq.IsFullfilled;

        private void StartME3()
        {
            ME3StartingUp = true;
            SetBusy("Creating AnimViewer Files...", () =>
            {
                ME3StartingUp = false;
            });
            Task.Run(() =>
            {
                InstallInteropASI();
                

                string animViewerBaseFilePath = Path.Combine(App.ExecFolder, "ME3AnimViewer.pcc");

                using IMEPackage animViewerBase = MEPackageHandler.OpenMEPackage(animViewerBaseFilePath);
                AnimViewer.SetUpAnimStreamFile(null, 0, "AAAME3EXPAVS1"); //placeholder for tocing
                AnimViewer.OpenFileInME3(animViewerBase, true, false);
                BusyText = "Launching Mass Effect 3...";
            });
        }

        private void InstallInteropASI()
        {
            string interopASIWritePath = GetInteropAsiWritePath();
            if (File.Exists(interopASIWritePath))
            {
                File.Delete(interopASIWritePath);
            }
            File.Copy(Path.Combine(App.ExecFolder, Me3ExplorerinteropAsiName), interopASIWritePath);
        }

        private static string GetInteropAsiWritePath()
        {
            string binariesWin32Dir = Path.GetDirectoryName(ME3Directory.ExecutablePath);
            string asiDir = Path.Combine(binariesWin32Dir, "ASI");
            Directory.CreateDirectory(asiDir);
            string interopASIWritePath = Path.Combine(asiDir, Me3ExplorerinteropAsiName);
            return interopASIWritePath;
        }

        private bool IsME3Closed() => !GameController.TryGetME3Process(out Process me3Process);

        private void KillME3()
        {
            if (GameController.TryGetME3Process(out Process me3Process))
            {
                me3Process.Kill();
            }
            CommandManager.InvalidateRequerySuggested();
        }

        private bool IsASILoaderInstalled()
        {
            if (!IsME3Installed())
            {
                return false;
            }
            string binariesWin32Dir = Path.GetDirectoryName(ME3Directory.ExecutablePath);
            string binkw23Path = Path.Combine(binariesWin32Dir, "binkw23.dll");
            string binkw32Path = Path.Combine(binariesWin32Dir, "binkw32.dll");
            const string binkw23MD5 = "128b560ef70e8085c507368da6f26fe6";
            const string binkw32MD5 = "1acccbdae34e29ca7a50951999ed80d5";

            return File.Exists(binkw23Path) && File.Exists(binkw32Path) && binkw23MD5 == CalculateMD5(binkw23Path) && binkw32MD5 == CalculateMD5(binkw32Path);

            //https://stackoverflow.com/a/10520086
            static string CalculateMD5(string filename)
            {
                using var stream = File.OpenRead(filename);
                using var md5 = MD5.Create();
                byte[] hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private void OpenASILoaderDownload()
        {
            Process.Start("https://github.com/Erik-JS/masseffect-binkw32");
        }

        private static bool IsME3Installed() => ME3Directory.ExecutablePath is string exePath && File.Exists(exePath);
        private static void SelectME3Path()
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Title = "Select Mass Effect 3 executable.",
                Filter = "MassEffect3.exe|MassEffect3.exe"
            };
            if (ofd.ShowDialog() == true)
            {
                string gamePath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(ofd.FileName)));

                Properties.Settings.Default.ME3Directory = ME3Directory.gamePath = gamePath;
                Properties.Settings.Default.Save();
                CommandManager.InvalidateRequerySuggested();
            }
        }


        #endregion

        private void LoadAnimation(Animation anim)
        {
            if (!LoadingAnimation && GameController.TryGetME3Process(out Process me3Process))
            {
                LoadingAnimation = true;
                SetBusy("Loading Animation");
                int animUIndex = 0;
                string filePath = null;
                if (anim != null && anim.AnimUsages.Any())
                {
                    //CameraState = ECameraState.Fixed;
                    int fileListIndex;
                    bool isMod;
                    (fileListIndex, animUIndex, isMod) = anim.AnimUsages[0];
                    (string filename, string contentdir) = FileListExtended[fileListIndex];
                    string rootPath = ME3Directory.gamePath;

                    filename = $"{filename}.*";
                    filePath = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault(f => f.Contains(contentdir));
                }
                Task.Run(() =>
                {
                    AnimViewer.SetUpAnimStreamFile(filePath, animUIndex, "AAAME3EXPAVS1");
                    GameController.ExecuteME3ConsoleCommands("ce LoadAnim1");
                });
            }
        }

        #region Position/Rotation
        private static readonly Vector3 defaultPosition = new Vector3(0f, 0f, 85f);

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
            GameController.ExecuteME3ConsoleCommands(VarCmd(XPos, FloatVarIndexes.XPos),
                                                     VarCmd(YPos, FloatVarIndexes.YPos),
                                                     VarCmd(ZPos, FloatVarIndexes.ZPos), 
                                                     "ce SetActorLocation");
        }

        private void UpdateRotation()
        {
            if (noUpdate) return;

            (float x, float y, float z) = new Rotator(((float)Pitch).ToUnrealRotationUnits(), ((float)Yaw).ToUnrealRotationUnits(), 0).GetDirectionalVector();
            GameController.ExecuteME3ConsoleCommands(VarCmd(x, FloatVarIndexes.XRotComponent),
                                                     VarCmd(y, FloatVarIndexes.YRotComponent),
                                                     VarCmd(z, FloatVarIndexes.ZRotComponent),
                                                     "ce SetActorRotation");
        }

        private void UpdateOffset()
        {
            if (noUpdate) return;
            GameController.ExecuteME3ConsoleCommands(VarCmd(RemoveOffset, BoolVarIndexes.RemoveOffset));
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
                    GameController.ExecuteME3ConsoleCommands("toggledebugcamera");
                    break;
                //case ECameraState.Shepard when prevCameraState != ECameraState.Shepard:
                //    LoadAnimation(SelectedAnimation, true);
                //    break;
            }
        }

        private void QuitME3_Click(object sender, RoutedEventArgs e)
        {
            KillME3();
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
                    GameController.ExecuteME3ConsoleCommands("ce PauseAnimation");
                    break;
                case PlaybackState.Stopped:
                case PlaybackState.Paused:
                    playbackState = PlaybackState.Playing;
                    PlayPauseIcon = EFontAwesomeIcon.Solid_Pause;
                    GameController.ExecuteME3ConsoleCommands("ce PlayAnimation");
                    break;
            }
        }

        private void StopAnimation_Click(object sender, RoutedEventArgs e)
        {

            if (noUpdate) return;
            playbackState = PlaybackState.Stopped;
            PlayPauseIcon = EFontAwesomeIcon.Solid_Play;
            GameController.ExecuteME3ConsoleCommands("ce StopAnimation");
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
                    GameController.ExecuteME3ConsoleCommands(VarCmd((float)value, FloatVarIndexes.PlayRate));
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
                        GameController.ExecuteME3ConsoleCommands("ce StartCameraFollow");
                    }
                    else
                    {
                        GameController.ExecuteME3ConsoleCommands("ce StopCameraFollow");
                    }
                }
            }
        }



        private void UpdateCamRotation()
        {
            if (noUpdate) return;

            (float x, float y, float z) = new Rotator(((float)CamPitch).ToUnrealRotationUnits(), ((float)CamYaw).ToUnrealRotationUnits(), 0).GetDirectionalVector();
            GameController.ExecuteME3ConsoleCommands(VarCmd(x, FloatVarIndexes.CamXRotComponent),
                                                     VarCmd(y, FloatVarIndexes.CamYRotComponent),
                                                     VarCmd(z, FloatVarIndexes.CamZRotComponent),
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
                    GameController.ExecuteME3ConsoleCommands(VarCmd((int)value, IntVarIndexes.SquadMember), "ce LoadNewHench");
                }
            }
        }

        public IEnumerable<ESquadMember> ESquadMemberValues => Enums.GetValues<ESquadMember>();
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
