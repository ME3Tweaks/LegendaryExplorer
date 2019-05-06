using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using Microsoft.VisualBasic.Logging;
using Path = System.IO.Path;

namespace ME3Explorer.Sequence_Editor
{
    /// <summary>
    /// Interaction logic for KismetLogParser.xaml
    /// </summary>
    public partial class KismetLogParser : NotifyPropertyChangedControlBase
    {
        public Action<string, int> ExportFound { get; set; } 

        public KismetLogParser()
        {
            InitializeComponent();
            DataContext = this;
        }

        public ObservableCollectionExtended<LoggerInfo> LogLines { get; } = new ObservableCollectionExtended<LoggerInfo>();

        private IMEPackage Pcc;

        public void LoadLog(string filePath, IMEPackage pcc = null)
        {
            Pcc = pcc;
            LogLines.ClearEx();
            LogLines.AddRange(File.ReadLines(filePath).Skip(4).Select(ParseLoggerLine).NonNull().ToList());
        }

        private LoggerInfo ParseLoggerLine(string line)
        {
            string[] args = line.Split(' ');
            if (args.Length == 4)
            {
                string nameAndNetIndex = args[3].Split('.').Last();
                string packageName = args[1].Trim('(', ')').ToLower();
                if (Pcc == null || Path.GetFileNameWithoutExtension(Pcc.FileName).ToLower() == packageName)
                {
                    if (int.TryParse(nameAndNetIndex.Substring(nameAndNetIndex.LastIndexOf('_') + 1), out int netIndex))
                    {
                        return new LoggerInfo
                        {
                            fullLine = line,
                            packageName = packageName,
                            className = args[2],
                            objectName = nameAndNetIndex.Substring(0, nameAndNetIndex.LastIndexOf('_')),
                            netIndex = netIndex
                        };
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
            public string objectName;
            public int netIndex;
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 1 && e.AddedItems[0] is LoggerInfo info)
            {
                foreach (var kvp in ME3LoadedFiles.GetFilesLoadedInGame())
                {
                    if (Path.GetFileNameWithoutExtension(kvp.Key.ToLower()) == info.packageName)
                    {
                        using (var package = MEPackageHandler.OpenME3Package(kvp.Value))
                        {
                            foreach (IExportEntry exp in package.Exports)
                            {
                                if (exp.ClassName == info.className && exp.ObjectName == info.objectName && exp.NetIndex == info.netIndex)
                                {
                                    ExportFound(kvp.Value, exp.UIndex);
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
