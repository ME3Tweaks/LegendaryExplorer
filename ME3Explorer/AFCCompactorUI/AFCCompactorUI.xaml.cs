using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ME3Explorer.SharedUI;
using ME3ExplorerCore.Audio;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace ME3Explorer.AFCCompactorUI
{
    /// <summary>
    /// Interaction logic for AFCCompactor.xaml
    /// </summary>
    public partial class AFCCompactorUI : NotifyPropertyChangedWindowBase
    {
        public AFCCompactorUI()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            ScanForReferencesCommand = new GenericCommand(ScanForReferences, CanScanForReferences);
            SelectDLCInputFolderCommand = new GenericCommand(SelectDLCInputFolder, () => !IsBusy);
        }

        #region Commands
        public GenericCommand ScanForReferencesCommand { get; set; }
        public GenericCommand SelectDLCInputFolderCommand { get; set; }
        #endregion


        #region Binding Vars
        public ObservableCollectionExtended<MEGame> GameOptions { get; } = new ObservableCollectionExtended<MEGame>(new[] { MEGame.ME2, MEGame.ME3 });
        public ObservableCollectionExtended<AFCCompactor.ReferencedAudio> AudioReferences { get; } = new ObservableCollectionExtended<AFCCompactor.ReferencedAudio>();

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private string _newAFCName;
        public string NewAFCName
        {
            get => _newAFCName;
            set => SetProperty(ref _newAFCName, value);
        }

        private string _dlcInputFolder;
        public string DLCInputFolder
        {
            get => _dlcInputFolder;
            set
            {
                SetProperty(ref _dlcInputFolder, value);
                if (!string.IsNullOrWhiteSpace(_dlcInputFolder))
                {
                    string foldername = Path.GetFileName(DLCInputFolder);
                    if (foldername.ToLower().StartsWith("cookedpc"))
                    {
                        foldername = Path.GetFileName(Directory.GetParent(DLCInputFolder).FullName);
                    }

                    NewAFCName = $"{foldername}_Audio";
                }
            }
        }



        private MEGame _selectedGame;
        public MEGame SelectedGame
        {
            get => _selectedGame;
            set => SetProperty(ref _selectedGame, value);
        }

        private bool _includeBasegameAudio;
        public bool IncludeBasegameAudio
        {
            get => _includeBasegameAudio;
            set => SetProperty(ref _includeBasegameAudio, value);
        }

        private bool _includeOfficialDLCAudio = true;
        public bool IncludeOfficialDLCAudio
        {
            get => _includeOfficialDLCAudio;
            set => SetProperty(ref _includeOfficialDLCAudio, value);
        }

        private bool _scanCompleted = true;
        public bool ScanCompleted
        {
            get => _scanCompleted;
            set => SetProperty(ref _scanCompleted, value);
        }

        #endregion

        private void ScanForReferences()
        {
            if (Directory.Exists(DLCInputFolder))
            {

                string[] afcFiles = Directory.GetFiles(DLCInputFolder, "*.afc", SearchOption.AllDirectories);
                string[] pccFiles = Directory.GetFiles(DLCInputFolder, "*.pcc", SearchOption.AllDirectories);

                if (afcFiles.Any() && pccFiles.Any())
                {
                    List<AFCCompactor.ReferencedAudio> referencedAudio =
                        new List<AFCCompactor.ReferencedAudio>();
                    //Task.Run(() => AFCCompactor.GetReferencedAudio(game, DLCInputFolder, includeBasegameAudio: false)).ContinueWithOnUIThread(prevTask =>
                    //{
                    //    IsBusy = false;
                    //    IsBusyTaskbar = false;
                    //    ListDialog ld = new ListDialog(prevTask.Result.Select(x => $"{x.uiSourceName} in {x.afcName} @ 0x{x.audioOffset:X8}, length {x.audioSize}"), "Referenced audio", "Here is the list of referenced audio by files in the specified folder.", this);
                    //    ld.Show();
                    //});

                    Task.Run(() =>
                    {
                        referencedAudio = AFCCompactor.GetReferencedAudio(SelectedGame, DLCInputFolder, IncludeBasegameAudio, IncludeOfficialDLCAudio);

                        // Determine what audio is not in basegame. We would somehow need to know what the original sizes of AFC are
                        // Maybe use mem DB?

                        return referencedAudio;
                    }).ContinueWithOnUIThread(prevTask =>
                    {
                        AudioReferences.ReplaceAll(prevTask.Result);
                        IsBusy = false;
                    });

                    //Analytics.TrackEvent("Used tool", new Dictionary<string, string>()
                    //{
                    //    { "Toolname", "AFC Compactor" }
                    //});
                }
            }
        }

        private void Extras()
        {
            //    string result = PromptDialog.Prompt(this,
            //            "Enter an AFC filename that all mod referenced items will be repointed to.\n\nCompacting AFC folder: " +
            //            foldername, "Enter an AFC filename");
            //    if (result != null)
            //{
            //    var regex = new Regex(@"^[a-zA-Z0-9_]+$");

            //    if (regex.IsMatch(result))
            //    {
            //        //BusyText = "Finding all referenced audio";
            //        //IsBusy = true;
            //        //IsBusyTaskbar = true;
            //        //soundPanel.FreeAudioResources(); // stop playing

            //        else
            //        {
            //            MessageBox.Show(
            //                "Only alphanumeric characters and underscores are allowed for the AFC filename.",
            //                "Error creating AFC");
            //        }
            //    }
        }

        private bool CanScanForReferences()
        {
            return !IsBusy;
        }

        public void SelectDLCInputFolder()
        {
            var dlg = new CommonOpenFileDialog("Select your mod's CookedPC/CookedPCConsole folder")
            {
                IsFolderPicker = true
            };

            if (dlg.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                string[] pccFiles = Directory.GetFiles(dlg.FileName, "*.pcc", SearchOption.AllDirectories);
                string[] mountFiles = Directory.GetFiles(dlg.FileName, "*.dlc", SearchOption.AllDirectories);

                if (mountFiles.Length != 1)
                {
                    MessageBox.Show("Selected folder must contain a Mount.dlc file to indicate it is a DLC.", "Error");
                    return;
                }
                else if (!pccFiles.Any())
                {
                    MessageBox.Show("Selected folder must contain package files.", "Error");
                    return;
                }

                DLCInputFolder = dlg.FileName;
            }
        }

        private void AFCCompactorUI_OnContentRendered(object sender, EventArgs e)
        {
            SelectedGame = MEGame.ME3;
        }
    }
}
