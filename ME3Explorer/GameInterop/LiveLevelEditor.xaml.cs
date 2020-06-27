﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FontAwesome5;
using Gammtek.Conduit.Collections.ObjectModel;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.BinaryConverters;
using ME3Explorer.Unreal.ME3Enums;
using Microsoft.AppCenter.Analytics;
using SharpDX;

namespace ME3Explorer.GameInterop
{
    /// <summary>
    /// Interaction logic for LiveLevelEditor.xaml
    /// </summary>
    public partial class LiveLevelEditor : NotifyPropertyChangedWindowBase
    {
        public static LiveLevelEditor Instance;
        private enum FloatVarIndexes
        {
            XPos = 1,
            YPos = 2,
            ZPos = 3,
            XRotComponent = 4,
            YRotComponent = 5,
            ZRotComponent = 6,
        }

        private enum BoolVarIndexes
        {
        }

        private enum IntVarIndexes
        {
            ActorArrayIndex = 1
        }

        private bool _readyToView;
        public bool ReadyToView
        {
            get => _readyToView;
            set => SetProperty(ref _readyToView, value);
        }

        private bool _readyToInitialize;
        public bool ReadyToInitialize
        {
            get => _readyToInitialize;
            set => SetProperty(ref _readyToInitialize, value);
        }

        public LiveLevelEditor()
        {
            if (Instance != null)
            {
                throw new Exception("Can only have one instance of LiveLevelEditor open!");
            }

            Instance = this;
            DataContext = this;
            LoadCommands();
            InitializeComponent();
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Live Level Editor", new WeakReference(this));
            Analytics.TrackEvent("Used tool", new Dictionary<string, string>
            {
                { "Toolname", "Live Level Editor" }
            });
            GameController.RecieveME3Message += GameControllerOnRecieveMe3Message;
            ME3OpenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            ME3OpenTimer.Tick += CheckIfME3Open;
            RetryLoadTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            RetryLoadTimer.Tick += RetryLoadLiveEditor;
        }

        private void LiveLevelEditor_OnClosing(object sender, CancelEventArgs e)
        {
            DisposeCamPath();
            GameController.RecieveME3Message -= GameControllerOnRecieveMe3Message;
            DataContext = null;
            Instance = null;
        }

        public RequirementCommand ME3InstalledRequirementCommand { get; set; }
        public RequirementCommand ASILoaderInstalledRequirementCommand { get; set; }
        public RequirementCommand SupportFilesInstalledRequirementCommand { get; set; }
        public ICommand LoadLiveEditorCommand { get; set; }
        public ICommand OpenPackageCommand { get; set; }
        public ICommand OpenActorInPackEdCommand { get; set; }
        public ICommand RegenActorListCommand { get; set; }
        void LoadCommands()
        {
            ME3InstalledRequirementCommand = new RequirementCommand(InteropHelper.IsME3Installed, InteropHelper.SelectME3Path);
            ASILoaderInstalledRequirementCommand = new RequirementCommand(InteropHelper.IsASILoaderInstalled, InteropHelper.OpenASILoaderDownload);
            SupportFilesInstalledRequirementCommand = new RequirementCommand(AreSupportFilesInstalled, InstallSupportFiles);
            LoadLiveEditorCommand = new GenericCommand(LoadLiveEditor, CanLoadLiveEditor);
            OpenPackageCommand = new GenericCommand(OpenPackage, CanOpenPackage);
            OpenActorInPackEdCommand = new GenericCommand(OpenActorInPackEd, CanOpenInPackEd);
            RegenActorListCommand = new GenericCommand(RegenActorList);
        }

        private void RegenActorList()
        {
            SetBusy("Building Actor List");
            GameController.ExecuteME3ConsoleCommands("ce DumpActors");
        }

        private bool CanOpenInPackEd() => SelectedActor != null;

        private void OpenActorInPackEd()
        {
            if (SelectedActor != null)
            {
                OpenInPackEd(SelectedActor.FileName, SelectedActor.UIndex);
            }
        }

        private bool CanOpenPackage() => listBoxPackages.SelectedItem != null;

        private void OpenPackage()
        {
            if (listBoxPackages.SelectedItem is KeyValuePair<string, List<ActorEntry>> kvp)
            {
                OpenInPackEd(kvp.Key);
            }
        }

        private void OpenInPackEd(string fileName, int uIndex = 0)
        {
            if (MELoadedFiles.GetFilesLoadedInGame(MEGame.ME3).TryGetValue(fileName, out string filePath))
            {
                if (MEPackageHandler.TryOpenInExisting(filePath, out PackageEditorWPF packEd))
                {
                    packEd.GoToNumber(uIndex);
                }
                else
                {
                    PackageEditorWPF p = new PackageEditorWPF();
                    p.Show();
                    p.LoadFile(filePath, uIndex);
                }
            }
            else
            {
                MessageBox.Show(this, $"Cannot Find pcc named {fileName}!");
            }
        }

        private bool CanLoadLiveEditor() => ReadyToInitialize && me3InstalledReq.IsFullfilled && asiLoaderInstalledReq.IsFullfilled && supportFilesInstalledReq.IsFullfilled && 
                                            GameController.TryGetME3Process(out _);

        private void LoadLiveEditor()
        {
            SetBusy("Loading Live Editor", () => {});
            GameController.ExecuteME3ConsoleCommands("ce LoadLiveEditor");
            RetryLoadTimer.Start();
        }

        private static bool AreSupportFilesInstalled()
        {
            if (!InteropHelper.IsME3Installed())
            {
                return false;
            }
            string installedASIPath = InteropHelper.GetInteropAsiWritePath();
            if (!File.Exists(installedASIPath))
            {
                return false;
            }

            string newAsiPath = Path.Combine(App.ExecFolder, GameController.Me3ExplorerinteropAsiName);
            string newAsiMD5 = InteropHelper.CalculateMD5(newAsiPath);
            string installedAsiMD5 = InteropHelper.CalculateMD5(installedASIPath);

            return newAsiMD5 == installedAsiMD5 && LiveEditHelper.IsModInstalledAndUpToDate();
        }

        private void InstallSupportFiles()
        {
            SetBusy("Installing Support Files");
            Task.Run(() =>
            {
                InteropHelper.InstallInteropASI();
                LiveEditHelper.InstallDLC_MOD_Interop();
                EndBusy();
                CommandManager.InvalidateRequerySuggested();
            });
        }

        private void GameControllerOnRecieveMe3Message(string msg)
        {
            if (msg == LiveEditHelper.LoaderLoadedMessage)
            {
                ReadyToView = false;
                ReadyToInitialize = true;
                instructionsTab.IsSelected = true;
                if (!ME3OpenTimer.IsEnabled)
                {
                    ME3OpenTimer.Start();
                }
            }
            else if (msg == "LiveEditor string Loaded")
            {
                RetryLoadTimer.Stop();
                BusyText = "Building Actor list";
                ReadyToView = true;
                actorTab.IsSelected = true;
                InitializeCamPath();
                EndBusy();
            }
            else if (msg == "LiveEditor string ActorsDumped")
            {
                BuildActorDict();
                EndBusy();
            }
            else if (msg == "LiveEditCamPath string CamPathComplete")
            {
                playbackState = PlaybackState.Paused;
                PlayPauseIcon = EFontAwesomeIcon.Solid_Play;
            }
            else if (msg.StartsWith("LiveEditor string ActorSelected"))
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
                            floats = defaultPosition.ToArray();
                            break;
                        }
                    }
                    pos = new Vector3(floats);


                }
                noUpdate = true;
                XPos = (int)pos.X;
                YPos = (int)pos.Y;
                ZPos = (int)pos.Z;
                noUpdate = false;
                EndBusy();
            }
            else if (msg.StartsWith("LiveEditor string ActorRotation"))
            {
                Rotator rot = defaultRotation;
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
                            floats = defaultPosition.ToArray();
                            break;
                        }
                    }
                    rot = new Rotator((floats[0]).ToUnrealRotationUnits(), (floats[1]).ToUnrealRotationUnits(), (floats[2]).ToUnrealRotationUnits());
                }
                noUpdate = true;
                Yaw = (int)rot.Yaw;
                Pitch = (int)rot.Pitch;
                Roll = (int)rot.Roll;
                noUpdate = false;
                EndBusy();
            }
        }

        private readonly DispatcherTimer ME3OpenTimer;
        private void CheckIfME3Open(object sender, EventArgs e)
        {
            if (!GameController.TryGetME3Process(out _))
            {
                ReadyToInitialize = false;
                EndBusy();
                ReadyToView = false;
                SelectedActor = null;
                instructionsTab.IsSelected = true;
                ME3OpenTimer.Stop();
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
                GameController.ExecuteME3ConsoleCommands("ce LoadLiveEditor");
            }
        }

        public ObservableDictionary<string, ObservableCollectionExtended<ActorEntry>> ActorDict { get; } = new ObservableDictionary<string, ObservableCollectionExtended<ActorEntry>>();

        private void BuildActorDict()
        {
            string actorDumpPath = Path.Combine(ME3Directory.gamePath, "Binaries", "Win32", "ME3ExpActorDump.txt");
            if (!File.Exists(actorDumpPath))
            {
                ActorDict.Clear();
                return;
            }

            var actors = new Dictionary<string, List<ActorEntry>>();
            string[] lines = File.ReadAllLines(actorDumpPath);
            Dictionary<string, string> gameFiles = MELoadedFiles.GetFilesLoadedInGame(MEGame.ME3);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split(':');
                if (parts.Length == 2 && gameFiles.ContainsKey($"{parts[0]}.pcc"))
                {
                    actors.AddToListAt($"{parts[0]}.pcc", new ActorEntry
                    {
                        ActorName = parts[1],
                        FileName = $"{parts[0]}.pcc",
                        ActorListIndex = i
                    });
                }
            }

            foreach ((string fileName, List<ActorEntry> actorEntries) in actors)
            {
                using IMEPackage pcc = MEPackageHandler.OpenMEPackage(gameFiles[fileName]);
                if (pcc.Exports.FirstOrDefault(exp => exp.ClassName == "Level") is { } levelExport)
                {
                    Level levelBin = levelExport.GetBinaryData<Level>();
                    var uIndices = levelBin.Actors.Where(uIndex => pcc.IsUExport(uIndex)).ToList();
                    List<string> instancedNames = uIndices.Select(uIndex => pcc.GetUExport(uIndex).ObjectName.Instanced).ToList();
                    foreach (ActorEntry actorEntry in actorEntries)
                    {
                        if (instancedNames.IndexOf(actorEntry.ActorName) is int idx && idx >= 0)
                        {
                            actorEntry.UIndex = uIndices[idx];
                            ActorDict.AddToListAt(fileName, actorEntry);
                        }
                    }
                }
            }
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

        private ActorEntry _selectedActor;

        public ActorEntry SelectedActor
        {
            get => _selectedActor;
            set 
            {
                if (SetProperty(ref _selectedActor, value) && !noUpdate && value != null)
                {
                    SetBusy($"Selecting {value.ActorName}", () => {});
                    GameController.ExecuteME3ConsoleCommands(VarCmd(value.ActorListIndex, IntVarIndexes.ActorArrayIndex), "ce SelectActor");
                }
            }
        }

        #region Position/Rotation
        private static readonly Vector3 defaultPosition = Vector3.Zero;
        private readonly Rotator defaultRotation = new Rotator(0,0,0);
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

        private int _posIncrement = 10;
        public int PosIncrement
        {
            get => _posIncrement;
            set => SetProperty(ref _posIncrement, value);
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

        private bool noUpdate;
        private void UpdateLocation()
        {
            if (noUpdate) return;
            GameController.ExecuteME3ConsoleCommands(VarCmd(XPos, FloatVarIndexes.XPos),
                                                     VarCmd(YPos, FloatVarIndexes.YPos),
                                                     VarCmd(ZPos, FloatVarIndexes.ZPos),
                                                     "ce SetLocation");
        }

        private void UpdateRotation()
        {
            if (noUpdate) return;

            (float x, float y, float z) = new Rotator(((float)Pitch).ToUnrealRotationUnits(), ((float)Yaw).ToUnrealRotationUnits(), ((float)Roll).ToUnrealRotationUnits()).GetDirectionalVector();
            GameController.ExecuteME3ConsoleCommands(VarCmd(x, FloatVarIndexes.XRotComponent),
                                                     VarCmd(y, FloatVarIndexes.YRotComponent),
                                                     VarCmd(z, FloatVarIndexes.ZRotComponent),
                                                     "ce SetRotation");
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

        #endregion

        #region CamPath

        private IMEPackage camPathPackage;

        public ExportEntry interpTrackMove { get; set; }

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
            GameController.ExecuteME3ConsoleCommands("ce playcam");
        }

        private void pauseCam()
        {
            playbackState = PlaybackState.Paused;
            PlayPauseIcon = EFontAwesomeIcon.Solid_Play;
            GameController.ExecuteME3ConsoleCommands("ce pausecam");
        }

        private bool _shouldLoop;

        public bool ShouldLoop
        {
            get => _shouldLoop;
            set
            {
                if (SetProperty(ref _shouldLoop, value) && !noUpdate)
                {
                    GameController.ExecuteME3ConsoleCommands(_shouldLoop ? "ce loopcam" : "ce noloopcam");
                }
            }
        }

        private void StopAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (noUpdate) return;
            playbackState = PlaybackState.Stopped;
            PlayPauseIcon = EFontAwesomeIcon.Solid_Play;
            GameController.ExecuteME3ConsoleCommands("ce stopcam");
        }

        private void SaveCamPath(object sender, RoutedEventArgs e)
        {
            camPathPackage.GetUExport(63).WriteProperty(new FloatProperty(CurveTab_CurveEditor.Time, "InterpLength"));
            camPathPackage.GetUExport(77).WriteProperty(new BoolProperty(ShouldLoop, "bOpen"));
            camPathPackage.Save();
            LiveEditHelper.PadCamPathFile();
            GameController.ExecuteME3ConsoleCommands("ce stopcam", "ce LoadCamPath");
            playbackState = PlaybackState.Stopped;
            PlayPauseIcon = EFontAwesomeIcon.Solid_Play;
        }

        private void InitializeCamPath()
        {
            camPathPackage = MEPackageHandler.OpenME3Package(LiveEditHelper.CamPathFilePath);
            interpTrackMove = camPathPackage.GetUExport(70);
            CurveTab_CurveEditor.LoadExport(interpTrackMove);

            savedCamsFileWatcher = new FileSystemWatcher(ME3Directory.BinariesPath, "savedCams") {NotifyFilter = NotifyFilters.LastWrite};
            savedCamsFileWatcher.Changed += SavedCamsFileWatcher_Changed;
            savedCamsFileWatcher.EnableRaisingEvents = true;

            ReloadCams();
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

        public ObservableCollectionExtended<POV> SavedCams { get; } = new ObservableCollectionExtended<POV>();

        private void AddSavedCamAsKey(object sender, RoutedEventArgs e)
        {
            string timeStr = PromptDialog.Prompt(this, "Add key at what time?", "", "0", true);

            if (float.TryParse(timeStr, out float time))
            {
                var pov = (POV)((System.Windows.Controls.Button)sender).DataContext;

                var props = interpTrackMove.GetProperties();
                var interpCurvePos = InterpCurve<Vector3>.FromStructProperty(props.GetProp<StructProperty>("PosTrack"));
                var interpCurveRot = InterpCurve<Vector3>.FromStructProperty(props.GetProp<StructProperty>("EulerTrack"));

                interpCurvePos.AddPoint(time, pov.Position, Vector3.Zero, Vector3.Zero, EInterpCurveMode.CIM_CurveUser);
                interpCurveRot.AddPoint(time, pov.Rotation, Vector3.Zero, Vector3.Zero, EInterpCurveMode.CIM_CurveUser);

                props.AddOrReplaceProp(interpCurvePos.ToStructProperty(MEGame.ME3, "PosTrack"));
                props.AddOrReplaceProp(interpCurveRot.ToStructProperty(MEGame.ME3, "EulerTrack"));
                interpTrackMove.WriteProperties(props);
                CurveTab_CurveEditor.LoadExport(interpTrackMove);
            }
        }

        #endregion

        private void ClearKeys(object sender, RoutedEventArgs e)
        {
            if (MessageBoxResult.Yes == MessageBox.Show("Are you sure you want to clear all keys from the Curve Editor?", "Clear Keys confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning))
            {
                var props = interpTrackMove.GetProperties();
                props.AddOrReplaceProp(new InterpCurve<Vector3>().ToStructProperty(MEGame.ME3, "PosTrack"));
                props.AddOrReplaceProp(new InterpCurve<Vector3>().ToStructProperty(MEGame.ME3, "EulerTrack"));
                interpTrackMove.WriteProperties(props);
                CurveTab_CurveEditor.LoadExport(interpTrackMove);
            }
        }
    }
    public class ActorEntry
    {
        public int ActorListIndex;
        public string FileName;
        public string ActorName { get; set; }
        public int UIndex;
        public bool Moveable;
    }
}
