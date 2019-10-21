using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ME3Explorer.AssetDatabase;
using ME3Explorer.AutoTOC;
using ME3Explorer.GameInterop;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace ME3Explorer.AnimationExplorer
{
    /// <summary>
    /// Interaction logic for AnimationExplorerWPF.xaml
    /// </summary>
    public partial class AnimationExplorerWPF : NotifyPropertyChangedWindowBase
    {
        private const string Me3ExplorerinteropAsiName = "ME3ExplorerInterop.asi";

        public AnimationExplorerWPF()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Animation Viewer", new WeakReference(this));
            DataContext = this;
            InitializeComponent();
            LoadCommands();
            GameController.RecieveME3Message += GameController_RecieveME3Message;
        }

        private void AnimationExplorerWPF_Loaded(object sender, RoutedEventArgs e)
        {
            string dbPath = AssetDB.GetDBPath(MEGame.ME3);
            if (File.Exists(dbPath))
            {
                LoadDatabase(dbPath);
            }
        }

        private void GameController_RecieveME3Message(string msg)
        {
            if (msg == "Loaded string AnimViewer")
            {
                ME3StartingUp = false;
                LoadingAnimation = false;
                IsBusy = false;
                ReadyToView = true;
                animTab.IsSelected = true;
            }
        }

        public ObservableCollectionExtended<Animation> Animations { get; } = new ObservableCollectionExtended<Animation>();
        private readonly List<(string fileName, string directory)> FileListExtended = new List<(string fileName, string directory)>();

        private Animation _selectedAnimation;

        public Animation SelectedAnimation
        {
            get => _selectedAnimation;
            set
            {
                if (SetProperty(ref _selectedAnimation, value))
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

        private void SearchBox_OnTextChanged(SearchBox sender, string newtext)
        {
            throw new NotImplementedException();
        }

        #region Busy variables

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
                MessageBox.Show(this, "Generate an ME3 asset database in the Asset Database tool. This should take about 10 minutes");
            }
        }

        private void LoadDatabase(string dbPath)
        {
            BusyText = "Loading Database...";
            IsBusy = true;
            PropsDataBase db = new PropsDataBase();
            AssetDB.LoadDatabase(dbPath, MEGame.ME3, db).ContinueWithOnUIThread(prevTask =>
            {
                foreach ((string fileName, int dirIndex) in db.FileList)
                {
                    FileListExtended.Add((fileName, db.ContentDir[dirIndex]));
                }
                Animations.AddRange(db.Animations);
                IsBusy = false;
            });
            CommandManager.InvalidateRequerySuggested();
        }

        private bool AllRequirementsMet() => me3InstalledReq.IsFullfilled && asiLoaderInstalledReq.IsFullfilled && me3ClosedReq.IsFullfilled && dbLoadedReq.IsFullfilled;

        private void StartME3()
        {
            BusyText = "Launching Mass Effect 3...";
            IsBusy = true;
            ME3StartingUp = true;
            Task.Run(() =>
            {
                InstallInteropASI();
                AutoTOCWPF.GenerateAllTOCs();

                string animViewerBaseFilePath = Path.Combine(App.ExecFolder, "ME3AnimViewer.pcc");

                using IMEPackage animViewerBase = MEPackageHandler.OpenMEPackage(animViewerBaseFilePath);
                AnimViewer.OpenFileInME3(animViewerBase, false);
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
            if (!LoadingAnimation && anim != null && anim.AnimUsages.Any() && GameController.TryGetME3Process(out Process me3Process))
            {
                BusyText = "Loading Animation";
                IsBusy = true;

                (int fileListIndex, int animUIndex) = anim.AnimUsages[0];
                (string filename, string contentdir) = FileListExtended[fileListIndex];
                string rootPath = ME3Directory.gamePath;

                filename = $"{filename}.*";
                var filePath = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault(f => f.Contains(contentdir));
                Task.Run(() => AnimViewer.ViewAnimInGame(filePath, animUIndex));
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
            GameController.RecieveME3Message -= GameController_RecieveME3Message;
            DataContext = null;
        }
    }
}
