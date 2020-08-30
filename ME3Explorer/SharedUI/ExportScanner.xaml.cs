using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using ME3ExplorerCore.MEDirectories;
using ME3ExplorerCore.Packages;

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Dialog that has copy button, designed for showing lists of short lines of text
    /// </summary>
    public partial class ExportScanner : NotifyPropertyChangedWindowBase
    {
        struct ExportOfInterest
        {
            public string FilePath { get;}
            public int UIndex { get; }
            public string Message { get; }

            public ExportOfInterest(string filePath, int uIndex, string message)
            {
                FilePath = filePath;
                UIndex = uIndex;
                Message = message;
            }
        }

        private int _progress;

        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        private readonly Func<ExportEntry, bool> ExportSelector;
        private readonly Func<ExportEntry, string> ExportTransformer;

        private readonly List<string> filePaths;

        public ExportScanner(Func<ExportEntry, bool> exportSelector, Func<ExportEntry, string> exportTransformer)
        {
            InitializeComponent();
            filePaths = MELoadedFiles.GetEnabledDLCFiles(MEGame.ME3).Append(ME3Directory.BIOGamePath)
                                             .SelectMany(dlcDir => Directory.EnumerateFiles(Path.Combine(dlcDir, "CookedPCConsole"), "*.pcc")).ToList();

            progressBar.Maximum = filePaths.Count;

            ExportSelector = exportSelector;
            ExportTransformer = exportTransformer;
        }

        private async void Scan()
        {
            var interestingExports = new ConcurrentBag<ExportOfInterest>();
            var scanner = new ActionBlock<string>(filePath =>
            {
                using (IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath))
                {
                    foreach (ExportEntry export in pcc.Exports.Where(ExportSelector))
                    {
                        string message = ExportTransformer(export);
                        if (message != null)
                        {
                            interestingExports.Add(new ExportOfInterest(filePath, export.UIndex, message));
                        }
                    }
                    Application.Current.Dispatcher.Invoke(() => Progress++);
                }
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = App.CoreCount });

            foreach (string filePath in filePaths)
            {
                scanner.Post(filePath);
            }
            scanner.Complete();
            await scanner.Completion;
            ListDialog_List.ItemsSource = interestingExports.ToList();
            Activate();
        }

        private void ExportScanner_Loaded(object sender, RoutedEventArgs e)
        {
            Scan();
        }

        private void ListDialog_List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement element && element.DataContext is ExportOfInterest exportOfInterest)
            {
                var packEd = new PackageEditorWPF();
                packEd.Show();
                packEd.LoadFile(exportOfInterest.FilePath, exportOfInterest.UIndex);
            }
        }
    }
}
