using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Audio;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace LegendaryExplorer.Dialogs
{
    /// <summary>
    /// Interaction logic for SetWwisePathDialog.xaml
    /// </summary>
    public partial class SetWwisePathDialog : NotifyPropertyChangedWindowBase
    {

        private string _wwise3773Path = Misc.AppSettings.Settings.Wwise_3773Path;
        public string Wwise3773Path
        {
            get => _wwise3773Path;
            set => SetProperty(ref _wwise3773Path, value);
        }

        private string _wwise7110Path = Misc.AppSettings.Settings.Wwise_7110Path;
        public string Wwise7110Path
        {
            get => _wwise7110Path;
            set => SetProperty(ref _wwise7110Path, value);
        }

        public ICommand Select3773Command { get; set; }
        public ICommand Select7110Command { get; set; }

        public SetWwisePathDialog() : base()
        {
            DataContext = this;
            Select3773Command = new GenericCommand(() => { Wwise3773Path = SelectPath(); });
            Select7110Command = new GenericCommand(() => { Wwise7110Path = SelectPath(); });
            InitializeComponent();
        }

        public SetWwisePathDialog(Window w) : this()
        {
            Owner = w;
        }

        private string SelectPath()
        {
            var dlg = new CommonOpenFileDialog("Open File");
            dlg.Filters.Add(new CommonFileDialogFilter("Executable File", "*.exe"));

            if (dlg.ShowDialog(this) != CommonFileDialogResult.Ok)
            {
                return "";
            }
            return dlg.FileName;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SetPaths_OnClick(object sender, RoutedEventArgs e)
        {
            if (EnsureWwiseVersions(Wwise7110Path, Wwise3773Path))
            {
                if(File.Exists(Wwise3773Path)) Misc.AppSettings.Settings.Wwise_3773Path = Wwise3773Path;
                if(File.Exists(Wwise7110Path)) Misc.AppSettings.Settings.Wwise_7110Path = Wwise7110Path;
                Misc.AppSettings.Settings.Save();
                Close();
            }
        }

        /// <summary>
        /// Returns true if the specified WwiseCLI paths are of the correct version,
        /// Shows a dialog box if they are not
        /// </summary>
        /// <param name="Wwise7110">Optional: path to WwiseCLI v7110</param>
        /// <param name="Wwise3773">Optional: path to WwiseCLI v3773</param>
        /// <returns></returns>
        public static bool EnsureWwiseVersions(string Wwise7110 = "", string Wwise3773 = "")
        {
            if (File.Exists(Wwise3773))
            {
                //check that it's a supported version...
                var versionInfo = FileVersionInfo.GetVersionInfo(Wwise3773);
                string version = versionInfo.ProductVersion;
                if (version != WwiseVersions.WwiseFullVersion(MEGame.ME3))
                {
                    //wrong version
                    MessageBox.Show("WwiseCLI.exe found, but it's the wrong version:" + version +
                                    ".\nInstall Wwise Build 3773 64bit to use this feature.");
                    return false;
                }
            }

            if (File.Exists(Wwise7110))
            {
                //check that it's a supported version...
                var versionInfo = FileVersionInfo.GetVersionInfo(Wwise7110);
                string version = versionInfo.ProductVersion;
                if (version != WwiseVersions.WwiseFullVersion(MEGame.LE3))
                {
                    //wrong version
                    MessageBox.Show("WwiseCLI.exe found, but it's the wrong version:" + version +
                                    ".\nInstall Wwise Build 7110 64bit to use this feature.");
                    return false;
                }
            }

            return true;
        }
    }
}
