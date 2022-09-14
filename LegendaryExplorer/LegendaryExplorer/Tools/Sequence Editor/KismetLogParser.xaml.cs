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
        static string KismetLogLE1Path => LE1Directory.DefaultGamePath != null ? Path.Combine(LE1Directory.DefaultGamePath, "Binaries", "Win64", "KismetLog.txt") : "";
        static string KismetLogLE2Path => LE2Directory.DefaultGamePath != null ? Path.Combine(LE2Directory.DefaultGamePath, "Binaries", "Win64", "KismetLog.txt") : "";
        static string KismetLogLE3Path => LE3Directory.DefaultGamePath != null ? Path.Combine(LE3Directory.DefaultGamePath, "Binaries", "Win64", "KismetLog.txt") : "";

        public static string KismetLogPath(MEGame game) => game switch
        {
            MEGame.ME1 => KismetLogME1Path,
            MEGame.ME2 => KismetLogME2Path,
            MEGame.ME3 => KismetLogME3Path,
            MEGame.LE1 => KismetLogLE1Path,
            MEGame.LE2 => KismetLogLE2Path,
            MEGame.LE3 => KismetLogLE3Path,
            _ => null
        };

        public Action<string, int> ExportFound { get; set; }
        public ObservableCollectionExtended<LoggerInfo> LogLines { get; } = new();
        public string FilterPccFileName { get; set; }

        private IMEPackage FilterPcc;
        private MEGame Game;
        private ExportEntry SequenceToFilterTo;

        public KismetLogParser()
        {
            InitializeComponent();
            DataContext = this;
        }

        public void LoadLog(MEGame game, IMEPackage pcc = null, ExportEntry filterToSequence = null)
        {
            Analytics.TrackEvent("Used feature", new Dictionary<string, string>() { { "Feature name", "Kismet Logger for " + game } });
            FilterPcc = pcc;
            FilterPccFileName = FilterPcc == null ? null : Path.GetFileNameWithoutExtension(FilterPcc.FilePath);
            SequenceToFilterTo = filterToSequence;
            Game = game;
            LogLines.ClearEx();
            if (File.Exists(KismetLogPath(Game)))
            {
                LogLines.AddRange(File.ReadLines(KismetLogPath(Game)).Skip(4).Select(ParseLoggerLine).NonNull().ToList());
            }
        }

        /// <summary>
        /// Parses a line from Kismet Logger output
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private LoggerInfo ParseLoggerLine(string line)
        {
            string[] args = line.Split(' ');
            if (args.Length >= 3)
            {
                // Time / Object Class / Instanced Full Path
                string[] path = args[2].Split('.');
                if (path.Length < 2) return null;
                string packageName = path[0];
                string fullInstancedPath = args[2].Substring(packageName.Length + 1);
                if (FilterPcc == null || FilterPccFileName.Equals(packageName, StringComparison.InvariantCultureIgnoreCase))
                {
                    var newInfo = new LoggerInfo
                    {
                        fullLine = line,
                        activationTime = args[0].Trim('[',']'),
                        packageName = packageName,
                        className = args[1],
                        fullInstancedPath = fullInstancedPath
                    };

                    if (args.Length >= 4)
                    {
                        // Input Pin
                        newInfo.inputPin = args[3];
                    }

                    // Filter it (if filters are on)
                    if (FilterPcc != null && SequenceToFilterTo != null)
                    {
                        var referencedEntry = FilterPcc.FindExport(newInfo.fullInstancedPath);
                        if (referencedEntry != null && referencedEntry.Parent.InstancedFullPath == SequenceToFilterTo.InstancedFullPath)
                        {
                            return newInfo;
                        }

                        return null;
                    }

                    // No filters, just return it
                    return newInfo;
                }
            }

            return null;
        }

        public class LoggerInfo
        {
            public string fullLine { get; set; }

            /// <summary>
            /// The package this object is contained in
            /// </summary>
            public string packageName { get; set; }
            /// <summary>
            /// The class name of the object
            /// </summary>
            public string className { get; set; }
            /// <summary>
            /// The instanced full path of the object
            /// </summary>
            public string fullInstancedPath { get; set; }
            /// <summary>
            /// What pin activated this object, if any
            /// </summary>
            public string inputPin { get; set; }
            /// <summary>
            /// The text that denotes how long into the game the activation took place after
            /// </summary>
            public string activationTime { get; set; }
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 1 && e.AddedItems[0] is LoggerInfo info)
            {
                var filesLoadedInGame = MELoadedFiles.GetFilesLoadedInGame(Game);
                string filePath = null;
                string packageWithExtension = Path.ChangeExtension(info.packageName, "pcc");
                if (filesLoadedInGame.ContainsKey(packageWithExtension))
                {
                    filePath = filesLoadedInGame[packageWithExtension];
                }
                else
                {
                    var fileName = filesLoadedInGame.Keys.FirstOrDefault((t =>
                        Path.GetFileNameWithoutExtension(t)
                            .Equals(info.packageName, StringComparison.InvariantCultureIgnoreCase)));
                    if (fileName is not null) filePath = filesLoadedInGame[fileName];
                }

                if (filePath != null)
                {
                    using var package = MEPackageHandler.OpenMEPackage(filePath);
                    var export = package.FindExport(info.fullInstancedPath);
                    if (export != null && export.ClassName == info.className)
                    {
                        ExportFound(filePath, export.UIndex);
                        return;
                    }
                }
                MessageBox.Show("Could not find matching export!");
            }
        }
    }
}
