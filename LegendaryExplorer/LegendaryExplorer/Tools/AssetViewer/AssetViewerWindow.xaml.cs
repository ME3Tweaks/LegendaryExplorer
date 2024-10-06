


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FontAwesome5;
using LegendaryExplorer.GameInterop;
using LegendaryExplorer.GameInterop.InteropTargets;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.Controls;
using LegendaryExplorer.Tools.AnimationViewer;
using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Paths;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Path = System.IO.Path;

namespace LegendaryExplorer.Tools.AssetViewer
{

    /// <summary>
    /// ASI-based asset viewer - Particle Systems, Animations, and more - Mgamerz
    /// </summary>
    public partial class AssetViewerWindow : TrackingNotifyPropertyChangedWindowBase
    {
        public ExportEntry AssetToView { get; set; }


        /// <summary>
        /// If game has told us it's ready for anim commands
        /// </summary>
        private new bool Initialized;
        /// <summary>
        /// Game this instance is for
        /// </summary>
        public MEGame Game { get; set; }
        private static readonly Dictionary<MEGame, AssetViewerWindow> Instances = new();
        public static AssetViewerWindow Instance(MEGame game)
        {
            if (!game.IsLEGame())
                throw new ArgumentException(@"Asset Viewer does not support this game!", nameof(game));

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
                    if (Game == MEGame.LE2)
                    {
                        PrepChangeLE2Actor(value);
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


        /// <summary>
        /// Result of polling. Do not poll too often or this could break.
        /// </summary>
        private bool MapPollResult = false;
        private void TestIfOnAssetViewerMap(Action isOnPreviewMap, Action isNoOnPreviewMap)
        {
            Task.Run(() =>
            {
                MapPollResult = false; // Will be set by incoming message
                InteropHelper.RemoteEvent("re_IsOnAssetViewerMap", Game);
                Thread.Sleep(1000); // Give it one second
            }).ContinueWithOnUIThread(x =>
            {
                var result = MapPollResult;
                MapPollResult = false; // We reset here to make sure we don't wait for the following methods
                if (result)
                    isOnPreviewMap?.Invoke();
                else
                    isNoOnPreviewMap?.Invoke();
            });
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

        #region LE2 SPECIFIC

        private List<string> CurrentParentPackages;
        private void PrepChangeLE2Actor(string actorFullPath)
        {
            if (!Initialized)
                return; // We haven't received the init message yet
            var packageName = actorFullPath.Split('.').FirstOrDefault(); // 1
            var memoryName = string.Join('.', actorFullPath.Split('.').Skip(1)); // The rest

            // LE2: Load parent packages for memory
            if (CurrentParentPackages?.Count > 0)
            {
                // Unload packages.
                foreach (var parentPackage in CurrentParentPackages)
                {
                    InteropHelper.SendMessageToGame($"STREAMLEVELOUT {Path.GetFileNameWithoutExtension(parentPackage)}", Game);
                }
            }

            // Get new list.
            CurrentParentPackages = EntryImporter.GetBioXParentFiles(MEGame.LE2, packageName, true, false, ".pcc", "INT");
            CurrentParentPackages.Reverse(); // We want BioP as top not bottom
            foreach (var parentPackage in CurrentParentPackages)
            {
                InteropHelper.SendMessageToGame($"ONLYLOADLEVEL {Path.GetFileNameWithoutExtension(parentPackage)}", Game);
                Thread.Sleep(100); // Allow command to run for a moment.
            }

            // Tell game to load package and change the pawn
            InteropHelper.SendMessageToGame($"ANIMV_CHANGE_PAWN {packageName}.{memoryName}", Game);
            InteropHelper.SendMessageToGame($"CAUSEEVENT ChangeActor", Game);
        }

        #endregion

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


        public AssetViewerWindow(MEGame game, bool loadDb) : base("Asset Viewer", true)
        {
            if (Instance(game) is not null)
            {
                throw new Exception($"Can only have one instance of {game} Asset Viewer open!");
            }

            LoadDB = loadDb;
            Instances[game] = this;

            Game = game;
            GameTarget = GameController.GetInteropTargetForGame(game);
            InitializeComponent();
            LoadCommands();
            GameTarget.GameReceiveMessage += ReceivedGameMessage;
            GameOpenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            GameOpenTimer.Tick += CheckIfGameOpen;
        }

        private void AnimationExplorerWPF_Loaded(object sender, RoutedEventArgs e)
        {
            if (!LoadDB)
            {
                LoadAssetViewerMap();
            }
            else
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
            Debug.WriteLine($"Message: {msg}");

            if (!msg.StartsWith("ASSETVIEWER"))
                return;
            if (msg == "ASSETVIEWER READY") // POLL OK
            {
                MapPollResult = true;
                return;
            }
            else if (msg == "ASSETVIEWER LOADED") // STAGE LOADED
            {
                Initialized = true;
                InteropHelper.SendMessageToGame("ASSETV_DISALLOW_WINDOW_PAUSE", Game);

                // Load the initial pawn.
                // TODO: Implement this.

                if (GameController.TryGetMEProcess(Game, out Process gameProcess))
                {
                    gameProcess.MainWindowHandle.RestoreAndBringToFront();
                }

                this.RestoreAndBringToFront();
                GameOpenTimer.Start();
                GameStartingUp = false;
                LoadingAsset = false;
                ReadyToView = true;
                animTab.IsSelected = true;

                noUpdate = true;
                ShouldFollowActor = false;
                noUpdate = false;

                LoadPendingAsset();

                EndBusy();
            }
            else if (msg is "ASSETVIEWER ANIMATIONLOADED" or "ASSETVIEWER ACTORLOADED") // Streamed asset package has completed loading into the game
            {
                LoadingAsset = false;
                IsBusy = false; // Not busy
            }
            else if (msg.StartsWith("AssetViewer string AssetLoaded"))
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
                LoadingAsset = false;
                EndBusy();
            }
            else if (msg == "AnimViewer string HenchLoaded")
            {
                LoadAnimation(SelectedAnimation);
            }
        }

        private void LoadPendingAsset()
        {
            if (AssetToView != null)
            {
                LoadAsset(AssetToView);
                AssetToView = null; // Unset
            }
        }

        private void LoadAsset(ExportEntry assetToView)
        {
            // Todo: Unify SupportsAsset and this.
            if (assetToView.IsA("ParticleSystem") ||
                assetToView.IsA("SkeletalMesh") ||
                assetToView.IsA("StaticMesh"))
            {
                LoadActor(assetToView);
            }

            if (assetToView.IsA("AnimSequence"))
            {
                LoadAnimation(assetToView);
            }
        }

        private void LoadAnimation(ExportEntry animationToView)
        {
            var package = AnimStreamPackageBuilder.BuildAnimationPackage(animationToView);
            InteropHelper.SendMessageToGame($"STREAMLEVELOUT {Path.GetFileNameWithoutExtension(AnimStreamPackageBuilder.GetStreamingPackageName(Game))}", Game);
            InteropHelper.SendFileToGame(package);
            Thread.Sleep(50); // Give it just a tiny bit of time to stream out - this can probably be fixed with states and handshake from game - I'm kinda lazy
            InteropHelper.SendMessageToGame($"STREAMLEVELIN {Path.GetFileNameWithoutExtension(AnimStreamPackageBuilder.GetStreamingPackageName(Game))}", Game);
        }

        private void LoadActor(ExportEntry assetToView)
        {
            var package = ActorStreamPackageBuilder.BuildActorPackage(assetToView);
            InteropHelper.SendMessageToGame($"STREAMLEVELOUT {Path.GetFileNameWithoutExtension(ActorStreamPackageBuilder.GetStreamingPackageName(Game))}", Game);
            InteropHelper.SendFileToGame(package);
            Thread.Sleep(50); // Give it just a tiny bit of time to stream out - this can probably be fixed with states and handshake from game - I'm kinda lazy
            InteropHelper.SendMessageToGame($"STREAMLEVELIN {Path.GetFileNameWithoutExtension(ActorStreamPackageBuilder.GetStreamingPackageName(Game))}", Game);
        }

        private readonly DispatcherTimer GameOpenTimer;
        private void CheckIfGameOpen(object sender, EventArgs e)
        {
            if (!GameController.TryGetMEProcess(Game, out _))
            {
                GameStartingUp = false;
                LoadingAsset = false;
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

        private bool _loadingAsset;

        public bool LoadingAsset
        {
            get => _loadingAsset;
            set => SetProperty(ref _loadingAsset, value);
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

        public Requirement.RequirementCommand GameInstalledRequirementCommand { get; set; }
        public Requirement.RequirementCommand ASILoaderInstalledRequirementCommand { get; set; }
        public Requirement.RequirementCommand InteropASIInstalledRequirementCommand { get; set; }
        public Requirement.RequirementCommand DatabaseLoadedRequirementCommand { get; set; }
        public ICommand LoadAnimViewerCommand { get; set; }
        void LoadCommands()
        {
            GameInstalledRequirementCommand = new Requirement.RequirementCommand(() => InteropHelper.IsGameInstalled(Game), () => InteropHelper.SelectGamePath(Game));
            ASILoaderInstalledRequirementCommand = new Requirement.RequirementCommand(() => InteropHelper.IsASILoaderInstalled(Game), () => InteropHelper.OpenASILoaderDownload(Game));
            InteropASIInstalledRequirementCommand = new Requirement.RequirementCommand(() => App.IsDebug || InteropHelper.IsInteropASIInstalled(Game), () => InteropHelper.OpenInteropASIDownload(Game));

            DatabaseLoadedRequirementCommand = new Requirement.RequirementCommand(IsDatabaseLoaded, TryLoadDatabase);
            LoadAnimViewerCommand = new GenericCommand(LoadAssetViewerMap, CanLoadAssetViewerMap);
        }

        private bool IsDatabaseLoaded() => !LoadDB || Enumerable.Any(Animations);

        private void TryLoadDatabase()
        {
            if (!LoadDB)
                return; // Do not load the database.

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
                if (Game is MEGame.LE1 or MEGame.LE2)
                {
                    var classNameToCheck = Game == MEGame.LE1 ? "BioPawnChallengeScaledType" : "BioPawnType";
                    // Object Instance DB doesn't include class name, and AssetDB doesn't store name of usage
                    // Object -> Package containing it
                    var foundActorTypes = new Dictionary<string, string>();
                    foreach (var cr in db.ClassRecords)
                    {
                        if (cr.Class == classNameToCheck)
                        {
                            // Types of actors that can be spawned
                            foreach (var usage in cr.Usages) // Usages of the class (per file)
                            {
                                var file = db.FileList[usage.FileKey];
                                if (MELoadedFiles.GetFilesLoadedInGame(Game)
                                    .TryGetValue(file.FileName, out var fullPath))
                                {
                                    var p = MEPackageHandler.UnsafePartialLoad(fullPath,
                                        _ => false); // Just load the tables
                                    var exp = p.GetUExport(usage.UIndex);
                                    if (!exp.IsDefaultObject && !foundActorTypes.ContainsKey(exp.InstancedFullPath))
                                    {
                                        foundActorTypes[exp.InstancedFullPath] = file.FileName; // Set in dictionary
                                    }
                                }
                            }
                        }

                        // Must be done on UI thread
                        Application.Current.Dispatcher.Invoke(() =>
                            ActorOptions.ReplaceAll(foundActorTypes.Select(x =>
                                $"{Path.GetFileNameWithoutExtension(x.Value)}.{x.Key}")));
                    }
                }
                else
                {
                    // Object Instance DB doesn't include class name, and AssetDB doesn't store name of usage
                    // Object -> Package containing it
                    var foundActorTypes = new CaseInsensitiveConcurrentDictionary<string>();
                    object syncObj = new object();
                    foreach (var cr in db.ClassRecords)
                    {
                        if (GlobalUnrealObjectInfo.IsA(cr.Class, "SFXPawn", MEGame.LE3))
                        {
                            // Types of actors that can be spawned
                            // Usages of the class (per file)
                            Parallel.ForEach(cr.Usages, usage =>
                            {
                                //    foreach (var usage in cr.Usages) 
                                //  {
                                var file = db.FileList[usage.FileKey];
                                if (MELoadedFiles.GetFilesLoadedInGame(Game)
                                    .TryGetValue(file.FileName, out var fullPath))
                                {
                                    IMEPackage p = null;
                                    lock (syncObj)
                                    {
                                        p = MEPackageHandler.UnsafePartialLoad(fullPath,
                                            _ => false); // Just load the tables
                                    }

                                    var exp = p.GetUExport(usage.UIndex);
                                    if (!exp.IsDefaultObject && exp.IsArchetype &&
                                        !foundActorTypes.ContainsKey(exp.InstancedFullPath))
                                    {
                                        foundActorTypes[exp.InstancedFullPath] = file.FileName; // Set in dictionary
                                    }
                                }
                                //}
                            });

                            // Must be done on UI thread
                            Application.Current.Dispatcher.Invoke(() =>
                                ActorOptions.ReplaceAll(foundActorTypes.Select(x =>
                                    $"{Path.GetFileNameWithoutExtension(x.Value)}.{x.Key}")));
                        }
                    }
                }
            }).ContinueWithOnUIThread(x =>
                    {
                        // Todo: Other games.
                        CommandManager.InvalidateRequerySuggested();
                        EndBusy();
                    });
        }

        private bool CanLoadAssetViewerMap() => gameInstalledReq.IsFullfilled && asiLoaderInstalledReq.IsFullfilled && interopASIInstalledReq.IsFullfilled && GameController.IsGameOpen(Game);

        public void LoadAssetViewerMap()
        {
            SetBusy("Preparing asset viewer", () =>
            {
                // Do nothing, just close.
            });

            void mapIsReady()
            {
                // If there's anything to load, do it now
                IsBusy = false;
                LoadPendingAsset();
            }

            void mapIsntReady()
            {
                // We need to load the map
                Task.Run(() =>
                {
                    var mapPcc = PreviewLevelBuilder.BuildAssetViewerLevel(Game);
                    InteropHelper.SendFileToGame(mapPcc);
                    GameTarget.ModernExecuteConsoleCommand($"at {Path.GetFileNameWithoutExtension(PreviewLevelBuilder.GetMapName(Game))}");
                    // Asset viewer will now wait for game to signal to LEX map is loaded
                    // Game will send: ASSETVIEWER LOADED
                });
            }

            TestIfOnAssetViewerMap(mapIsReady, mapIsntReady);
        }

        #endregion

        public void LoadAnimation(AnimationRecord anim)
        {
            if (!LoadingAsset && GameController.TryGetMEProcess(Game, out Process _))
            {
                LoadingAsset = true;
                SetBusy("Loading Animation", () => LoadingAsset = false);
                int animUIndex = 0;
                string filePath = null;
                if (anim != null && Enumerable.Any(anim.Usages))
                {
                    //CameraState = ECameraState.Fixed;
                    (int fileListIndex, animUIndex, _) = anim.Usages[0];
                    (string filename, string contentdir) = FileListExtended[fileListIndex];
                    string rootPath = MEDirectories.GetDefaultGamePath(Game);
                    filename = $"{filename}.*";
                    filePath = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault(f => f.Contains(contentdir));
                }

                if (filePath != null)
                {
                    Task.Run(() =>
                    {
                        using var sourcePackage = MEPackageHandler.OpenMEPackage(filePath);
                        var package = AnimStreamPackageBuilder.BuildAnimationPackage(sourcePackage.GetUExport(animUIndex));
                        InteropHelper.SendMessageToGame($"STREAMLEVELOUT {Path.GetFileNameWithoutExtension(AnimStreamPackageBuilder.GetStreamingPackageName(Game))}", Game);
                        InteropHelper.SendFileToGame(package);
                        Thread.Sleep(50); // Give it just a tiny bit of time to stream out - this can probably be fixed with states and handshake from game - I'm kinda lazy
                        InteropHelper.SendMessageToGame($"STREAMLEVELIN {Path.GetFileNameWithoutExtension(AnimStreamPackageBuilder.GetStreamingPackageName(Game))}", Game);
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
            GameTarget.ModernExecuteConsoleCommand(VarCmd(rot.X, FloatVarIndexes.XRotComponent));
            GameTarget.ModernExecuteConsoleCommand(VarCmd(rot.Y, FloatVarIndexes.YRotComponent));
            GameTarget.ModernExecuteConsoleCommand(VarCmd(rot.Z, FloatVarIndexes.ZRotComponent));
            GameTarget.ModernExecuteConsoleCommand("ce SetActorRotation");
        }

        private void UpdateOffset()
        {
            if (noUpdate) return;
            GameTarget.ModernExecuteConsoleCommand(VarCmd(RemoveOffset, BoolVarIndexes.RemoveOffset));
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

        private void QuitGame_Click(object sender, RoutedEventArgs e)
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
                    InteropHelper.RemoteEvent("re_PauseAnimation", Game);
                    break;
                case PlaybackState.Stopped:
                case PlaybackState.Paused:
                    playbackState = PlaybackState.Playing;
                    PlayPauseIcon = EFontAwesomeIcon.Solid_Pause;
                    InteropHelper.RemoteEvent("re_StartAnimation", Game);
                    break;
            }
        }

        private void StopAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (noUpdate) return;
            playbackState = PlaybackState.Stopped;
            PlayPauseIcon = EFontAwesomeIcon.Solid_Play;
            InteropHelper.RemoteEvent("re_StopAnimation", Game);
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
                        GameTarget.ModernExecuteConsoleCommand("ce StartCameraFollow");
                    }
                    else
                    {
                        GameTarget.ModernExecuteConsoleCommand("ce StopCameraFollow");
                    }
                }
            }
        }

        private void UpdateCamRotation()
        {
            if (noUpdate) return;

            var rot = new Rotator(((float)CamPitch).DegreesToUnrealRotationUnits(), ((float)CamYaw).DegreesToUnrealRotationUnits(), 0).GetDirectionalVector();
            GameTarget.ModernExecuteConsoleCommand(VarCmd(rot.X, FloatVarIndexes.CamXRotComponent));
            GameTarget.ModernExecuteConsoleCommand(VarCmd(rot.Y, FloatVarIndexes.CamYRotComponent));
            GameTarget.ModernExecuteConsoleCommand(VarCmd(rot.Z, FloatVarIndexes.CamZRotComponent));
            GameTarget.ModernExecuteConsoleCommand("ce SetCameraRotation");
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

        /// <summary>
        /// If databases should be loaded.
        /// </summary>
        private readonly bool LoadDB;

        public AssetViewerWindow(ExportEntry currentExport) : this(currentExport.Game, false)
        {
            AssetToView = currentExport;
        }

        public static MEGame[] SupportedGames { get; } = [MEGame.LE1, MEGame.LE2, MEGame.LE3];

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            ReadyToView = false;
        }

        /// <summary>
        /// If AssetPreview supports this asset
        /// </summary>
        /// <param name="asset"></param>
        /// <returns></returns>
        public static bool SupportsAsset(ExportEntry asset)
        {
            if (asset.IsA("ParticleSystem"))
                return true;
            if (asset.IsA("AnimSequence"))
                return true;
            if (asset.IsA("SkeletalMesh"))
                return true;
            if (asset.IsA("StaticMesh"))
                return true;

            return false;
        }

        /// <summary>
        /// Set up AssetViewerWindow for previewing.
        /// </summary>
        /// <param name="export"></param>
        public static void PreviewAsset(ExportEntry export)
        {
            if (Instance(export.Game) != null)
            {
                Instance(export.Game).AssetToView = export;
                Instance(export.Game).LoadAssetViewerMap();
            }
            else
            {
                AssetViewerWindow avw = new AssetViewerWindow(export);
                avw.Show();
            }
        }
    }
}
