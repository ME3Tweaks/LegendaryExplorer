using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.Misc;
using LegendaryExplorer.UnrealExtensions;
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

        public bool PathCorrect { get; set; }

        public SetWwisePathDialog() : base()
        {
            DataContext = this;
            Select3773Command = new GenericCommand(() => { Wwise3773Path = SelectExePath(); });
            Select7110Command = new GenericCommand(() => { Wwise7110Path = SelectExePath(); });
            InitializeComponent();
        }

        public SetWwisePathDialog(Window w) : this()
        {
            Owner = w;
        }

        private string SelectExePath()
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
            PathCorrect = false;
            Close();
        }

        private void SetPaths_OnClick(object sender, RoutedEventArgs e)
        {
            if (WwiseCliHandler.EnsureWwiseVersions(Wwise7110Path, Wwise3773Path))
            {
                if(File.Exists(Wwise3773Path)) Misc.AppSettings.Settings.Wwise_3773Path = Wwise3773Path;
                if(File.Exists(Wwise7110Path)) Misc.AppSettings.Settings.Wwise_7110Path = Wwise7110Path;
                Misc.AppSettings.Settings.Save();
                PathCorrect = true;
                Close();
            }
        }
    }
}
