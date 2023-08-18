using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Be.Windows.Forms;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace LegendaryExplorer.ToolsetDev
{
    /// <summary>
    /// Interaction logic for FileHexViewer.xaml
    /// </summary>
    public partial class FileHexViewer : NotifyPropertyChangedWindowBase
    {
        //DO NOT USE WPFBASE - THIS IS NOT AN EDITOR
        private IMEPackage pcc;
        private DLCPackage dlcPackage;

        private byte[] bytes;
        private List<string> RFiles;
        private readonly string FileHexViewerDataFolder = Path.Combine(AppDirectories.AppDataFolder, @"FileHexViewer\");
        private const string RECENTFILES_FILE = "RECENTFILES";

        public HexBox Interpreter_Hexbox { get; private set; }
        public FileHexViewer()
        {
            DataContext = this;
            InitializeComponent();
            LoadRecentList();
            RefreshRecent();
        }

        private void GotoOffset_Click(object sender, RoutedEventArgs e)
        {
            var result = PromptDialog.Prompt(this, "Enter file offset - hex only, no 0x or anything.", "Enter offset");
            if (result != "" && uint.TryParse(result, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint offset))
            {
                Interpreter_Hexbox.SelectionStart = offset;
                Interpreter_Hexbox.SelectionLength = 1;
            }
        }
        public ObservableCollectionExtended<UsedSpace> UnusedSpaceList { get; } = new();

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var d = new OpenFileDialog()
            {
                CustomPlaces = AppDirectories.GameCustomPlaces
            };
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
            else if (lowerFilename.EndsWith(".sfar"))
            {
                dlcPackage = new DLCPackage(fileName);
            }

            bytes = File.ReadAllBytes(fileName);
            Interpreter_Hexbox.ByteProvider = new DynamicByteProvider(bytes);
            Title = "FileHexViewer - " + fileName;
            AddRecent(fileName, false);
            SaveRecentList();
            RefreshRecent();
            var inStream = new MemoryStream(bytes);
            UnusedSpaceList.ClearEx();
            if (pcc != null)
            {
                parsePackage(inStream);
            }
            else if (dlcPackage != null)
            {
                parseDLC(inStream);
            }
        }

        private void parseDLC(MemoryStream inStream)
        {
            var er = EndianReader.SetupForReading(inStream, 0x53464152, out int magic);
            var used = new List<UsedSpace>();
            used.Add(new UsedSpace
            {
                UsedSpaceStart = (int)er.Position,
                UsedFor = $"SFAR Version {er.ReadInt32()}", //Is this actually the version?
                UsedSpaceEnd = (int)er.Position,
            });
            used.Add(new UsedSpace
            {
                UsedSpaceStart = (int)er.Position,
                UsedSpaceEnd = (int)er.Position + 4,
                UsedFor = $"Data Offset 0x{er.ReadInt32():X8}",
            });
            used.Add(new UsedSpace
            {
                UsedSpaceStart = (int)er.Position,
                UsedFor = $"Entry Offset 0x{er.ReadInt32():X8}",
                UsedSpaceEnd = (int)er.Position,
            });
            used.Add(new UsedSpace
            {
                UsedSpaceStart = (int)er.Position,
                UsedFor = $"File count {er.ReadInt32()}",
                UsedSpaceEnd = (int)er.Position,
            });
            used.Add(new UsedSpace
            {
                UsedSpaceStart = (int)er.Position,
                UsedFor = $"Block Table offset 0x{er.ReadInt32():X8}",
                UsedSpaceEnd = (int)er.Position,
            });
            used.Add(new UsedSpace
            {
                UsedSpaceStart = (int)er.Position,
                UsedFor = $"Max block size 0x{er.ReadInt32():X8}",
                UsedSpaceEnd = (int)er.Position,
            });
            used.Add(new UsedSpace
            {
                UsedSpaceStart = (int)er.Position,
                UsedFor = $"Compression scheme {er.ReadStringASCII(4)}",
                UsedSpaceEnd = (int)er.Position,
            });

            er.Position = 8 * 4; //includes magic

            // File entry headers
            foreach (var fes in dlcPackage.Files)
            {
                used.Add(new UsedSpace
                {
                    UsedFor = $"SFAR File Entry {Path.GetFileName(fes.FileName)}",
                    UsedSpaceStart = (int)fes.MyOffset,
                    UsedSpaceEnd = (int)fes.MyOffset + 0x1E //Header struct item
                });
            }

            used.Add(new UsedSpace
            {
                UsedFor = "Block Table",
                UsedSpaceStart = (int)dlcPackage.Header.BlockTableOffset,
                UsedSpaceEnd = (int)(dlcPackage.Header.BlockTableOffset + (2 * dlcPackage.Header.FileCount))//Header struct item
            });

            foreach (var fes in dlcPackage.Files)
            {
                er.Position = fes.BlockOffsets[0];
                int i = 0;
                long left = fes.RealUncompressedSize;
                Debug.WriteLine($"{fes.RealDataOffset:X8}");

                while (left > 0)
                {
                    var uncompressedBlockSize = (uint)Math.Min(left, fes.Header.MaxBlockSize);
                    if (fes.BlockSizeTableIndex == 0xFFFFFFFF)
                    {
                        used.Add(new UsedSpace
                        {
                            UsedFor = $"{Path.GetFileName(fes.FileName)} Data Block {i}",
                            UsedSpaceStart = (int)er.Position,
                            UsedSpaceEnd = (int)(er.Position + fes.RealUncompressedSize)
                        });
                        er.Position += fes.RealUncompressedSize;
                        break;
                    }
                    else
                    {

                        used.Add(new UsedSpace
                        {
                            UsedFor = $"{Path.GetFileName(fes.FileName)} Data Block {i}",
                            UsedSpaceStart = (int)er.Position,
                            UsedSpaceEnd = (int)er.Position + fes.BlockSizes[i]
                        });
                        er.Position += fes.BlockSizes[i];
                    }

                    i++;
                    left -= uncompressedBlockSize;
                }
            }

            used = used.OrderBy(x => x.UsedSpaceStart).ToList();
            List<UsedSpace> unusedSpace = new List<UsedSpace>();
            int endOffset = used[0].UsedSpaceEnd;
            bool first = true;
            foreach (var usedSpace in used)
            {
                if (first)
                {
                    first = false;
                    continue;
                }
                if (endOffset != 0 && usedSpace.UsedSpaceStart != endOffset)
                {
                    //unused space
                    unusedSpace.Add(new UsedSpace()
                    {
                        UsedFor = "Unused space",
                        UsedSpaceStart = endOffset,
                        UsedSpaceEnd = usedSpace.UsedSpaceStart,
                        Unused = true
                    });
                    endOffset = usedSpace.UsedSpaceStart;
                }
                //unusedSpace.Add(usedSpace);
                endOffset = usedSpace.UsedSpaceEnd;
            }


            UnusedSpaceList.AddRange(used);
            UnusedSpaceList.AddRange(unusedSpace);
            UnusedSpaceList.Sort(x => x.UsedSpaceStart);
        }

        private void parsePackage(Stream inStream)
        {
            var used = new List<UsedSpace>();
            used.Add(new UsedSpace
            {
                UsedFor = "Package Header",
                UsedSpaceStart = 0,
                UsedSpaceEnd = pcc.NameOffset
            });

            inStream.Seek(pcc.NameOffset, SeekOrigin.Begin);
            for (int i = 0; i < pcc.NameCount; i++)
            {
                int strLength = inStream.ReadInt32();
                if (strLength < 0)
                {
                    inStream.ReadStringUnicodeNull(strLength * -2);
                    if (pcc.Game == MEGame.ME2)
                    {
                        inStream.ReadInt32();
                    }
                }
                else if (strLength > 0)
                {
                    inStream.ReadStringLatin1Null(strLength); //-1 cause we also read trailing null.
                    if (pcc.Game != MEGame.ME2)
                    {
                        inStream.ReadInt64(); //Read 8 bytes
                    }
                    else
                    {
                        inStream.ReadInt32(); //4 bytes
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
                inStream.Position += exp.HeaderLength;
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
                    UsedFor = $"Export {exp.UIndex} - {exp.ObjectName} ({exp.ClassName})",
                    UsedSpaceStart = exp.DataOffset,
                    UsedSpaceEnd = exp.DataOffset + exp.DataSize,
                    Export = exp
                });
            }

            usedExportsSpaces = usedExportsSpaces.OrderBy(x => x.UsedSpaceStart).ToList();
            int endOffset = 0;
            var displayedUsedSpace = new List<UsedSpace>();
            foreach (var usedSpace in usedExportsSpaces)
            {
                if (endOffset != 0 && usedSpace.UsedSpaceStart != endOffset)
                {
                    //unused space
                    displayedUsedSpace.Add(new UsedSpace()
                    {
                        UsedFor = "Unused space",
                        UsedSpaceStart = endOffset,
                        UsedSpaceEnd = usedSpace.UsedSpaceStart,
                        Unused = true
                    });
                    endOffset = usedSpace.UsedSpaceStart;
                }
                displayedUsedSpace.Add(usedSpace);
                endOffset = usedSpace.UsedSpaceEnd;
            }

            //List<UsedSpace> continuousBlocks = new List<UsedSpace>();
            //UsedSpace continuous = new UsedSpace
            //{
            //    UsedFor = $"Continuous Export Data {usedExportsSpaces[0].Export.UIndex} {usedExportsSpaces[0].Export.ObjectName.Instanced} ({usedExportsSpaces[0].Export.ClassName})",
            //    UsedSpaceStart = usedExportsSpaces[0].UsedSpaceStart,
            //    UsedSpaceEnd = usedExportsSpaces[0].UsedSpaceEnd,
            //    Export = usedExportsSpaces[0].Export
            //};

            //for (int i = 1; i < usedExportsSpaces.Count; i++)
            //{
            //    UsedSpace u = usedExportsSpaces[i];
            //    if (continuous.UsedSpaceEnd == u.UsedSpaceStart)
            //    {
            //        continuous.UsedSpaceEnd = u.UsedSpaceEnd;
            //    }
            //    else
            //    {
            //        if (continuous.UsedSpaceEnd > u.UsedSpaceStart)
            //        {
            //            Debug.WriteLine("Possible overlap detected!");
            //        }
            //        continuousBlocks.Add(continuous);
            //        UsedSpace unused = new UsedSpace()
            //        {
            //            UsedFor = "Unused space",
            //            UsedSpaceStart = continuous.UsedSpaceEnd,
            //            UsedSpaceEnd = u.UsedSpaceStart,
            //            Unused = true
            //        };
            //        continuousBlocks.Add(unused);

            //        continuous = new UsedSpace
            //        {
            //            UsedFor = $"Continuous Export Data {u.Export.UIndex} {u.Export.ObjectName.Instanced} ({u.Export.ClassName})",
            //            UsedSpaceStart = u.UsedSpaceStart,
            //            UsedSpaceEnd = u.UsedSpaceEnd,
            //            Export = u.Export
            //        };
            //    }
            //}
            //continuousBlocks.Add(continuous);
            UnusedSpaceList.AddRange(used);
            UnusedSpaceList.AddRange(displayedUsedSpace);
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
                var fr = new MenuItem
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
