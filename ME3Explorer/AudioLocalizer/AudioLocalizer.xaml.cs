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
using FontAwesome5;
using Gammtek.Conduit.Extensions;
using ME3Explorer.SharedUI;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;
using Microsoft.AppCenter.Analytics;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for AudioLocalizer.xaml
    /// </summary>
    public partial class AudioLocalizer : NotifyPropertyChangedWindowBase
    {
        private readonly object _localizationTasksLock = new object();

        public class ConvoTask : NotifyPropertyChangedBase
        {
            public readonly string FilePath;

            public string Conversation { get; }

            public string FileName => $"{Path.GetFileName(FilePath)}";

            private bool _isChecked;

            public bool IsChecked
            {
                get => _isChecked;
                set => SetProperty(ref _isChecked, value);
            }

            public ConvoTask(string filePath, string conversation)
            {
                FilePath = filePath;
                Conversation = conversation;
            }
        }

        public ObservableCollectionExtended<ListBoxTask> LocalizationTasks { get; } = new ObservableCollectionExtended<ListBoxTask>();

        public ObservableCollectionExtended<ConvoTask> Conversations { get; } = new ObservableCollectionExtended<ConvoTask>();

        public string LocalizedFilesPath { get; set; }

        private bool IsLocalizing;

        public AudioLocalizer()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Audio Localizer", new WeakReference(this));
            Analytics.TrackEvent("Used tool", new Dictionary<string, string>
            {
                { "Toolname", "Audio Localizer" }
            });

            DataContext = this;
            LoadCommands();
            InitializeComponent();

            BindingOperations.EnableCollectionSynchronization(LocalizationTasks, _localizationTasksLock);
        }

        #region Commands

        public ICommand SelectLocalizedFilesPathCommand { get; set; }
        public ICommand LocalizeCommand { get; set; }

        private void LoadCommands()
        {
            SelectLocalizedFilesPathCommand = new GenericCommand(SelectLocalizedFilesPath, CanSelectLocalizedFilesPath);
            LocalizeCommand = new GenericCommand(Localize, CanLocalize);
        }

        private bool CanLocalize()
        {
            bool exists = Directory.Exists(LocalizedFilesPath);
            bool any = Conversations.Any(convoTask => convoTask.IsChecked);
            return exists && any;
        }

        class Wwisestreaminfo
        {
            public string FileName;
            public int Size;
            public int Offset;
        }

        private void Localize()
        {
            IsLocalizing = true;
            LocalizationTasks.ClearEx();
            List<string> convNames = Conversations.Where(conv => conv.IsChecked).Select(conv => conv.FileName + conv.Conversation).ToList();
            Task.Run(() =>
            {
                string resultsBasePath = Path.Combine(LocalizedFilesPath, "AudioLocalizerResults");
                Dictionary<string, Dictionary<string, Wwisestreaminfo>> infoDicts = GetLocalizationInfo();
                foreach ((string langCode, Dictionary<string, Wwisestreaminfo> infoDict) in infoDicts)
                {
                    string resultsdir = Path.Combine(resultsBasePath, langCode);
                    Directory.CreateDirectory(resultsdir);
                    foreach (ConvoTask convoTask in Conversations.Where(conv => conv.IsChecked))
                    {
                        using (IMEPackage pcc = MEPackageHandler.OpenMEPackage(convoTask.FilePath))
                        {
                            string fileName = Path.GetFileName(pcc.FilePath);
                            string localizedFileName = $"{fileName.Substring(0, fileName.LastIndexOf("LOC_") + 4)}{langCode}.pcc";

                            var task = new ListBoxTask($"Localizing {fileName} to {langCode}");
                            LocalizationTasks.Add(task);
                            
                            foreach (ExportEntry wwisestream in pcc.Exports.Where(exp => exp.ClassName == "WwiseStream"))
                            {
                                string[] nameSections = wwisestream.ObjectName.Name.Split(',');
                                if (nameSections.Length == 4 && convNames.Contains(fileName + nameSections[2]) && infoDict.TryGetValue(nameSections[3], out Wwisestreaminfo info))
                                {
                                    var wwiseStreamBinary = wwisestream.GetBinaryData<WwiseStream>();
                                    wwiseStreamBinary.DataSize = info.Size;
                                    wwiseStreamBinary.DataOffset = info.Offset;
                                    wwisestream.WritePropertyAndBinary(new NameProperty(info.FileName, "Filename"), wwiseStreamBinary);
                                }
                            }
                            pcc.Save(Path.Combine(resultsdir, localizedFileName));
                            task.Complete($"{localizedFileName} saved");
                        }
                    }
                }

                return resultsBasePath;
            }).ContinueWithOnUIThread(prevTask =>
            {
                LocalizationTasks.Add(new ListBoxTask
                {
                    Header = $"Localization complete! Files saved to:\n {prevTask.Result}",
                    Icon = EFontAwesomeIcon.Solid_Check,
                    Foreground = Brushes.Green,
                    Spinning = false
                });
                IsLocalizing = false;
            });
        }

        private Dictionary<string, Dictionary<string, Wwisestreaminfo>> GetLocalizationInfo()
        {
            var task = new ListBoxTask("Searching for localized files");
            LocalizationTasks.Add(task);

            var allFiles = Directory.EnumerateFiles(LocalizedFilesPath, "*LOC_*.pcc", SearchOption.AllDirectories).Where(path => Path.GetFileName(path).Contains("LOC_"));
            var langFiles = new Dictionary<string, List<string>>();
            Regex langRegex = new Regex(".+LOC_(.+).pcc");
            foreach (string file in allFiles)
            {
                string fileName = Path.GetFileName(file);
                Match match = langRegex.Match(fileName);
                if (match.Success)
                {
                    langFiles.AddToListAt(match.Groups[1].Value, file);
                }
            }

            task.Complete($"Found {langFiles.Keys.Count} localization(s): {string.Join(", ", langFiles.Keys)}");

            var infoDicts = new Dictionary<string, Dictionary<string, Wwisestreaminfo>>();

            foreach ((string langCode, List<string> files) in langFiles)
            {
                var langTask = new ListBoxTask($"Proccessing {langCode} files");
                LocalizationTasks.Add(langTask);

                var infoDict = new Dictionary<string, Wwisestreaminfo>();

                foreach (string file in files)
                {
                    using var pcc = MEPackageHandler.OpenMEPackage(file);
                    foreach (ExportEntry wwisestream in pcc.Exports.Where(exp => exp.ClassName == "WwiseStream"))
                    {
                        string[] nameSections = wwisestream.ObjectName.Name.Split(',');
                        if (nameSections.Length == 4 && wwisestream.GetProperty<NameProperty>("Filename")?.Value is NameReference fileName)
                        {
                            var wsBin = wwisestream.GetBinaryData<WwiseStream>();
                            int pos = pcc.Game == MEGame.ME3 ? 4 : 12;
                            infoDict[nameSections[3]] = new Wwisestreaminfo
                            {
                                FileName = fileName,
                                Size = wsBin.DataSize,
                                Offset = wsBin.DataOffset
                            };
                        }
                    }
                }

                infoDicts[langCode] = infoDict;

                langTask.Complete($"Proccessed {langCode} files");
            }

            return infoDicts;
        }

        private bool CanSelectLocalizedFilesPath() => !IsLocalizing;

        private void SelectLocalizedFilesPath()
        {
            var dlg = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = "Select Folder Containing Localized Files"
            };
            if (dlg.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                LocalizedFilesPath = dlg.FileName;
                localizedFilesPathTextBox.Text = LocalizedFilesPath;
            }
        }

        #endregion

        void LoadFile(string filePath)
        {
            try
            {
                using (IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath))
                {
                    var bioConvs = pcc.Exports.Where(exp => exp.ClassName == "BioConversation");
                    foreach (ExportEntry bioConv in bioConvs)
                    {
                        //remove _dlg to get conversation name
                        Conversations.Add(new ConvoTask(filePath, bioConv.ObjectName.Name.RemoveRight(4)));
                    }
                }
            }
            catch (Exception e)
            {
                new ExceptionHandlerDialogWPF(e).ShowDialog();
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);

                foreach (string file in files)
                {
                    LoadFile(file);
                }
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Select(file => Path.GetExtension(file)?.ToLower()).Any(ext => ext != ".pcc"))
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }
    }
}
