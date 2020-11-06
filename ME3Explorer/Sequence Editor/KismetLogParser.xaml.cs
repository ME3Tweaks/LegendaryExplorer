using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ME3Explorer.SharedUI;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.MEDirectories;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;
using Microsoft.AppCenter.Analytics;
using Path = System.IO.Path;

namespace ME3Explorer.Sequence_Editor
{
    /// <summary>
    /// Interaction logic for KismetLogParser.xaml
    /// </summary>
    public partial class KismetLogParser : NotifyPropertyChangedControlBase
    {
        static string KismetLogME3Path => ME3Directory.gamePath != null ? Path.Combine(ME3Directory.gamePath, "Binaries", "Win32", "KismetLog.txt") : "";
        static string KismetLogME2Path => ME2Directory.gamePath != null ? Path.Combine(ME2Directory.gamePath, "Binaries", "KismetLog.txt") : "";
        static string KismetLogME1Path => ME1Directory.gamePath != null ? Path.Combine(ME1Directory.gamePath, "Binaries", "KismetLog.txt") : "";
        public static string KismetLogPath(MEGame game) => game == MEGame.ME3 ? KismetLogME3Path :
                                                           game == MEGame.ME2 ? KismetLogME2Path :
                                                           game == MEGame.ME1 ? KismetLogME1Path : null;

        public Action<string, int> ExportFound { get; set; }


        public KismetLogParser()
        {
            InitializeComponent();
            DataContext = this;
        }

        public ObservableCollectionExtended<LoggerInfo> LogLines { get; } = new ObservableCollectionExtended<LoggerInfo>();

        private IMEPackage Pcc;
        private MEGame Game;
        private ExportEntry SequenceToFilterTo;

        public void LoadLog(MEGame game, IMEPackage pcc = null, ExportEntry filterToSequence = null)
        {
            Analytics.TrackEvent("Used feature", new Dictionary<string, string>() { { "Feature name", "Kismet Logger for " + game } });
            Pcc = pcc;
            SequenceToFilterTo = filterToSequence;
            Game = game;
            LogLines.ClearEx();
            if (File.Exists(KismetLogPath(Game)))
            {
                LogLines.AddRange(File.ReadLines(KismetLogPath(Game)).Skip(4).Select(ParseLoggerLine).NonNull().ToList());
            }
        }

        private LoggerInfo ParseLoggerLine(string line)
        {
            string[] args = line.Split(' ');
            if (args.Length == 4)
            {
                string[] path = args[3].Split('.');
                if (path.Length < 2) return null;
                string nameAndIndex = path.Last();
                string sequence = path[path.Length - 2];
                string packageName = args[1].Trim('(', ')').ToLower();
                if (Pcc == null || Path.GetFileNameWithoutExtension(Pcc.FilePath).ToLower() == packageName)
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
                    if (Path.GetFileNameWithoutExtension(fileName.ToLower()) == info.packageName)
                    {
                        using (var package = MEPackageHandler.OpenMEPackage(filePath))
                        {
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
                }
                MessageBox.Show("Could not find matching export!");
            }
        }
    }
}
