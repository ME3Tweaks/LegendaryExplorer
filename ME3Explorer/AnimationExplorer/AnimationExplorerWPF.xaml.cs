using System;
using System.Collections.Generic;
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
        public AnimationExplorerWPF()
        {
            DataContext = this;
            InitializeComponent();
            LoadCommands();
            GameController.RecieveME3Message += GameController_RecieveME3Message;
        }

        private void GameController_RecieveME3Message(string msg)
        {

        }

        public PropsDataBase CurrentDataBase { get; } = new PropsDataBase();

        private bool _readyToView;
        public bool ReadyToView
        {
            get => _readyToView;
            set => SetProperty(ref _readyToView, value);
        }
        private void SearchBox_OnTextChanged(SearchBox sender, string newtext)
        {
            throw new NotImplementedException();
        }

        #region Commands

        public RequirementCommand ME3InstalledRequirementCommand { get; set; }
        public RequirementCommand ASILoaderInstalledRequirementCommand { get; set; }
        public RequirementCommand ME3ClosedRequirementCommand { get; set; }
        public ICommand StartME3Command { get; set; }
        void LoadCommands()
        {
            ME3InstalledRequirementCommand = new RequirementCommand(IsME3Installed, SelectME3Path);
            ASILoaderInstalledRequirementCommand = new RequirementCommand(IsASILoaderInstalled, OpenASILoaderDownload);
            ME3ClosedRequirementCommand = new RequirementCommand(IsME3Closed, KillME3);
            StartME3Command = new GenericCommand(StartME3, AllRequirementsMet);
        }

        private bool AllRequirementsMet() => me3InstalledReq.IsFullfilled && asiLoaderInstalledReq.IsFullfilled && me3ClosedInstalledReq.IsFullfilled;

        private void StartME3()
        {
            string animViewerBaseFilePath = Path.Combine(App.ExecFolder, "ME3AnimViewer.pcc");

            using IMEPackage animViewerBase = MEPackageHandler.OpenMEPackage(animViewerBaseFilePath);
            AnimViewer.OpenFileInME3(animViewerBase);
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
    }
}
