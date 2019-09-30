using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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
using System.Windows.Shapes;
using Be.Windows.Forms;
using Gibbed.IO;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using Microsoft.Win32;
using StreamHelpers;
using Path = System.IO.Path;

namespace ME3Explorer.FileHexViewer
{
    /// <summary>
    /// Interaction logic for FileHexViewerWPF.xaml
    /// </summary>
    public partial class FileHexViewerWPF : NotifyPropertyChangedWindowBase
    {
        //DO NOT USE WPFBASE - THIS IS NOT AN EDITOR
        private IMEPackage pcc;
        private byte[] bytes;
        private List<string> RFiles;
        private readonly string FileHexViewerDataFolder = Path.Combine(App.AppDataFolder, @"FileHexViewerWPF\");
        private const string RECENTFILES_FILE = "RECENTFILES";

        public HexBox Interpreter_Hexbox { get; private set; }
        public FileHexViewerWPF()
        {
            DataContext = this;
            InitializeComponent();
            LoadRecentList();
            RefreshRecent();
        }

        private void GotoOffset_Click(object sender, RoutedEventArgs e)
        {
            var result = PromptDialog.Prompt(this, "Enter file offset - hex only, no 0x or anything.", "Enter offset");
            if (result != "" && UInt32.TryParse(result, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint offset))
            {
                Interpreter_Hexbox.SelectionStart = offset;
                Interpreter_Hexbox.SelectionLength = 1;
            }
        }
        public ObservableCollectionExtended<UsedSpace> UnusedSpaceList { get; } = new ObservableCollectionExtended<UsedSpace>();

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            if (d.ShowDialog() == true)
            {
                LoadFile(d.FileName);
            }
        }

        private void LoadFile(string fileName)
        {
            string lowerFilename = Path.GetExtension(fileName).ToLower();
            if (lowerFilename.EndsWith(".pcc") || lowerFilename.EndsWith(".u") || lowerFilename.EndsWith(".sfm") || lowerFilename.EndsWith(".upk"))
            {
                pcc = MEPackageHandler.OpenMEPackage(fileName);
            }

            bytes = File.ReadAllBytes(fileName);
            Interpreter_Hexbox.ByteProvider = new DynamicByteProvider(bytes);
            Title = "FileHexViewerWPF - " + fileName;
            AddRecent(fileName, false);
            SaveRecentList();
            RefreshRecent();
            MemoryStream inStream = new MemoryStream(bytes);
            UnusedSpaceList.ClearEx();
            if (pcc != null)
            {
                List<UsedSpace> used = new List<UsedSpace>();
                used.Add(new UsedSpace
                {
                    UsedFor = "Package Header",
                    UsedSpaceStart = 0,
                    UsedSpaceEnd = pcc.NameOffset
                });

                inStream.Seek(pcc.NameOffset, SeekOrigin.Begin);
                for (int i = 0; i < pcc.NameCount; i++)
                {
                    int strLength = inStream.ReadValueS32();
                    if (strLength < 0)
                    {
                        inStream.ReadString(strLength * -2, true, Encoding.Unicode);
                        if (pcc.Game == MEGame.ME2)
                        {
                            inStream.ReadValueS32();
                        }
                    }
                    else if (strLength > 0)
                    {
                        inStream.ReadString(strLength, true, Encoding.ASCII); //-1 cause we also read trailing null.
                        if (pcc.Game != MEGame.ME2)
                        {
                            inStream.ReadValueS64(); //Read 8 bytes
                        } else
                        {
                            inStream.ReadValueS32(); //4 bytes
                        }
                    }
                }

                used.Add(new UsedSpace
                {
                    UsedFor = "Name Table",
                    UsedSpaceStart = pcc.NameOffset,
                    UsedSpaceEnd = (int)inStream.Position
                });

                for (int i = 0; i < pcc.ImportCount; i++)
                {
                    inStream.Position += 28;
                }

                used.Add(new UsedSpace
                {
                    UsedFor = "Import Table",
                    UsedSpaceStart = pcc.ImportOffset,
                    UsedSpaceEnd = (int)inStream.Position
                });

                inStream.Seek(pcc.ExportOffset, SeekOrigin.Begin);
                foreach (ExportEntry exp in pcc.Exports)
                {
                    inStream.Position += exp.Header.Length;
                }

                used.Add(new UsedSpace
                {
                    UsedFor = "Export Metadata Table",
                    UsedSpaceStart = pcc.ExportOffset,
                    UsedSpaceEnd = (int)inStream.Position
                });

                used.Add(new UsedSpace
                {
                    UsedFor = "Dependency Table (Unused)",
                    UsedSpaceStart = ((MEPackage)pcc).DependencyTableOffset,
                    UsedSpaceEnd = ((MEPackage)pcc).FullHeaderSize
                });

                List<UsedSpace> usedExportsSpaces = new List<UsedSpace>();
                inStream.Seek(pcc.ExportOffset, SeekOrigin.Begin);
                foreach (ExportEntry exp in pcc.Exports)
                {
                    usedExportsSpaces.Add(new UsedSpace
                    {
                        UsedFor = $"Export {exp.UIndex}",
                        UsedSpaceStart = exp.DataOffset,
                        UsedSpaceEnd = exp.DataOffset + exp.DataSize,
                        Export = exp
                    });
                }

                usedExportsSpaces = usedExportsSpaces.OrderBy(x => x.UsedSpaceStart).ToList();
                List<UsedSpace> continuousBlocks = new List<UsedSpace>();
                UsedSpace continuous = new UsedSpace
                {
                    UsedFor = $"Continuous Export Data {usedExportsSpaces[0].Export.UIndex} {usedExportsSpaces[0].Export.ObjectName.Instanced} ({usedExportsSpaces[0].Export.ClassName})",
                    UsedSpaceStart = usedExportsSpaces[0].UsedSpaceStart,
                    UsedSpaceEnd = usedExportsSpaces[0].UsedSpaceEnd,
                    Export = usedExportsSpaces[0].Export
                };

                for (int i = 1; i < usedExportsSpaces.Count; i++)
                {
                    UsedSpace u = usedExportsSpaces[i];
                    if (continuous.UsedSpaceEnd == u.UsedSpaceStart)
                    {
                        continuous.UsedSpaceEnd = u.UsedSpaceEnd;
                    }
                    else
                    {
                        if (continuous.UsedSpaceEnd > u.UsedSpaceStart)
                        {
                            Debug.WriteLine("Possible overlap detected!");
                        }
                        continuousBlocks.Add(continuous);
                        UsedSpace unused = new UsedSpace()
                        {
                            UsedFor = "Unused space",
                            UsedSpaceStart = continuous.UsedSpaceEnd,
                            UsedSpaceEnd = u.UsedSpaceStart,
                            Unused = true
                        };
                        continuousBlocks.Add(unused);

                        continuous = new UsedSpace
                        {
                            UsedFor = $"Continuous Export Data {u.Export.UIndex} {u.Export.ObjectName.Instanced} ({u.Export.ClassName})",
                            UsedSpaceStart = u.UsedSpaceStart,
                            UsedSpaceEnd = u.UsedSpaceEnd,
                            Export = u.Export
                        };
                    }

                }
                continuousBlocks.Add(continuous);
                UnusedSpaceList.AddRange(used);
                UnusedSpaceList.AddRange(continuousBlocks);
            }
        }

        private void FileHexViewerWPF_OnLoaded(object sender, RoutedEventArgs e)
        {
            Interpreter_Hexbox = (HexBox)Interpreter_Hexbox_Host.Child;
        }

        private void FileHexViewerWPF_OnClosing(object sender, CancelEventArgs e)
        {
            pcc?.Release();
            Interpreter_Hexbox_Host.Dispose();
            Interpreter_Hexbox_Host.Child = null;
        }

        private void hb1_SelectionChanged(object sender, EventArgs e)
        {

            int start = (int)Interpreter_Hexbox.SelectionStart;
            int len = (int)Interpreter_Hexbox.SelectionLength;
            int size = (int)Interpreter_Hexbox.ByteProvider.Length;
            try
            {
                if (bytes != null && start != -1 && start < size)
                {
                    string s = $"Byte: {bytes[start]}"; //if selection is same as size this will crash.
                    if (start <= bytes.Length - 4)
                    {
                        int val = BitConverter.ToInt32(bytes, start);
                        s += $", Int: {val}";
                        s += $", Float: {BitConverter.ToSingle(bytes, start)}";
                        if (pcc != null)
                        {
                            if (pcc.IsName(val))
                            {
                                s += $", Name: {pcc.GetNameEntry(val)}";
                            }

                            if (pcc.GetEntry(val) is ExportEntry exp)
                            {
                                s += $", Export: {exp.ObjectName.Instanced}";
                            }
                            else if (pcc.GetEntry(val) is ImportEntry imp)
                            {
                                s += $", Import: {imp.ObjectName.Instanced}";
                            }
                        }
                    }
                    s += $" | Start=0x{start:X8} ";
                    if (len > 0)
                    {
                        s += $"Length=0x{len:X8} ";
                        s += $"End=0x{(start + len - 1):X8}";
                    }
                    StatusBar_LeftMostText.Text = s;
                }
                else
                {
                    StatusBar_LeftMostText.Text = "Nothing Selected";
                }
            }
            catch
            {
                // ignored
            }
        }

        #region Recents
        private void LoadRecentList()
        {
            Recents_MenuItem.IsEnabled = false;
            RFiles = new List<string>();
            string path = FileHexViewerDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
            {
                string[] recents = File.ReadAllLines(path);
                foreach (string recent in recents)
                {
                    if (File.Exists(recent))
                    {
                        AddRecent(recent, true);
                    }
                }
            }
        }

        private void SaveRecentList()
        {
            if (!Directory.Exists(FileHexViewerDataFolder))
            {
                Directory.CreateDirectory(FileHexViewerDataFolder);
            }
            string path = FileHexViewerDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
                File.Delete(path);
            File.WriteAllLines(path, RFiles);
        }

        public void RefreshRecent()
        {
            Recents_MenuItem.Items.Clear();
            if (RFiles.Count <= 0)
            {
                Recents_MenuItem.IsEnabled = false;
                return;
            }
            Recents_MenuItem.IsEnabled = true;

            int i = 0;
            foreach (string filepath in RFiles)
            {
                MenuItem fr = new MenuItem()
                {
                    Header = filepath.Replace("_", "__"),
                    Tag = filepath
                };
                fr.Click += RecentFile_click;
                Recents_MenuItem.Items.Add(fr);
                i++;
            }
        }

        private void RecentFile_click(object sender, EventArgs e)
        {
            string s = ((FrameworkElement)sender).Tag.ToString();
            if (File.Exists(s))
            {
                LoadFile(s);
            }
            else
            {
                MessageBox.Show("File does not exist: " + s);
            }
        }

        public void AddRecent(string s, bool loadingList)
        {
            RFiles = RFiles.Where(x => !x.Equals(s, StringComparison.InvariantCultureIgnoreCase)).ToList();
            if (loadingList)
            {
                RFiles.Add(s); //in order
            }
            else
            {
                RFiles.Insert(0, s); //put at front
            }
            if (RFiles.Count > 10)
            {
                RFiles.RemoveRange(10, RFiles.Count - 10);
            }
            Recents_MenuItem.IsEnabled = true;
        }

        #endregion

        public class UsedSpace
        {
            internal ExportEntry Export;

            public int UsedSpaceStart { get; set; }
            public int UsedSpaceEnd { get; set; }
            public string UsedFor { get; set; }
            public bool Unused { get; internal set; }
            public long Length => UsedSpaceEnd - UsedSpaceStart;

            public override string ToString() => $"{UsedFor} 0x{UsedSpaceStart:X6} - 0x{UsedSpaceEnd:X6}";
        }

        private void FileHexViewer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var space = ((UsedSpace)e.AddedItems[0]);
                Interpreter_Hexbox.SelectionStart = space.UsedSpaceStart;
                Interpreter_Hexbox.SelectionLength = space.Length;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // Assuming you have one file that you care about, pass it off to whatever
                // handling code you have defined.
                LoadFile(files[0]);
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }
    }
}
