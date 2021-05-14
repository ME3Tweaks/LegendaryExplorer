using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using Microsoft.AppCenter.Analytics;
using Path = System.IO.Path;

namespace LegendaryExplorer.Tools.Sequence_Editor
{
    /// <summary>
    /// Interaction logic for KismetLogParser.xaml
    /// </summary>
    public partial class KismetLogParser : NotifyPropertyChangedControlBase
    {
        static string KismetLogME3Path => ME3Directory.DefaultGamePath != null ? Path.Combine(ME3Directory.DefaultGamePath, "Binaries", "Win32", "KismetLog.txt") : "";
        static string KismetLogME2Path => ME2Directory.DefaultGamePath != null ? Path.Combine(ME2Directory.DefaultGamePath, "Binaries", "KismetLog.txt") : "";
        static string KismetLogME1Path => ME1Directory.DefaultGamePath != null ? Path.Combine(ME1Directory.DefaultGamePath, "Binaries", "KismetLog.txt") : "";
        public static string KismetLogPath(MEGame game) => game == MEGame.ME3 ? KismetLogME3Path :
                                                           game == MEGame.ME2 ? KismetLogME2Path :
                                                           game == MEGame.ME1 ? KismetLogME1Path : null;

        public Action<string, int> ExportFound { get; set; }


        public KismetLogParser()
        {
            InitializeComponent();
            DataContext = this;
        }

        public ObservableCollectionExtended<LoggerInfo> LogLines { get; } = new();

        private IMEPackage Pcc;
        private MEGame Game;
        private ExportEntry SequenceToFilterTo;


        public void LoadLog(MEGame game, IMEPackage pcc = null, ExportEntry filterToSequence = null)
        {
            if (Settings.Analytics_Enabled)
            {
                Analytics.TrackEvent("Used feature", new Dictionary<string, string>() { { "Feature name", "Kismet Logger for " + game } });
            }
            Pcc = pcc;
            PccFileName = Pcc == null ? null :Path.GetFileNameWithoutExtension(Pcc.FilePath);
            SequenceToFilterTo = filterToSequence;
            Game = game;
            LogLines.ClearEx();
            if (File.Exists(KismetLogPath(Game)))
            {
            
                LogLines.AddRange(File.ReadLines(KismetLogPath(Game)).Skip(4).Select(ParseLoggerLine).NonNull().ToList());
            }
        }

        public string PccFileName { get; set; }

        private LoggerInfo ParseLoggerLine(string line)
        {
            string[] args = line.Split(' ');
            if (args.Length == 4)
            {
                string[] path = args[3].Split('.');
                if (path.Length < 2) return null;
                string nameAndIndex = path.Last();
                string sequence = path[^2];
                string packageName = args[1].Trim('(', ')');
                if (Pcc == null || PccFileName.Equals(packageName, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (int.TryParse(nameAndIndex.Substring(nameAndIndex.LastIndexOf('_') + 1), out int nameIndex))
                    {
                        var newInfo = new LoggerInfo
                        {
                            fullLine = line,
                            packageName = packageName,
                            className = args[2],
                            objectName = new NameReference(nameAndIndex.Substring(0, nameAndIndex.LastIndexOf('_')), nameIndex),
                            sequenceName = sequence,
                        };

                        if (Pcc != null && SequenceToFilterTo != null)
                        {
                            // This is wildly inefficient
                            var referencedEntry = Pcc.Exports.FirstOrDefault(exp => exp.ClassName == newInfo.className && exp.ObjectName == newInfo.objectName && exp.ParentName == sequence);
                            if (referencedEntry != null && referencedEntry.Parent.InstancedFullPath == SequenceToFilterTo.InstancedFullPath)
                            {
                                return newInfo;
                            }
                            return null;
                        }

                        return newInfo;
                    }
                }
            }

            return null;
        }

        public class LoggerInfo
        {
            public string fullLine { get; set; }
            public string packageName;
            public string className;
            public NameReference objectName;
            public string sequenceName;
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 1 && e.AddedItems[0] is LoggerInfo info)
            {
                foreach ((string fileName, string filePath) in MELoadedFiles.GetFilesLoadedInGame(Game))
                {
                    if (Path.GetFileNameWithoutExtension(fileName).Equals(info.packageName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        using var package = MEPackageHandler.OpenMEPackage(filePath);
                        foreach (ExportEntry exp in package.Exports)
                        {
                            if (exp.ClassName == info.className && exp.ObjectName == info.objectName &&
                                exp.ParentName == info.sequenceName)
                            {
                                ExportFound(filePath, exp.UIndex);
                                return;
                            }
                        }
                    }
                }
                MessageBox.Show("Could not find matching export!");
            }
        }
    }
}
