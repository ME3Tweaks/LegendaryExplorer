using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        public HexBox Interpreter_Hexbox { get; private set; }

        public FileHexViewerWPF()
        {
            DataContext = this;
            InitializeComponent();
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
                string lowerFilename = System.IO.Path.GetExtension(d.FileName).ToLower();
                if (d.FileName.EndsWith(".pcc") || d.FileName.EndsWith(".u") || d.FileName.EndsWith(".sfm") || d.FileName.EndsWith(".upk"))
                {
                    pcc = MEPackageHandler.OpenMEPackage(d.FileName);
                }

                bytes = File.ReadAllBytes(d.FileName);
                Interpreter_Hexbox.ByteProvider = new DynamicByteProvider(bytes);

                MemoryStream inStream = new MemoryStream(bytes);
                UnusedSpaceList.ClearEx();
                if (pcc != null)
                {
                    List<UsedSpace> used = new List<UsedSpace>();
                    used.Add(new UsedSpace
                    {
                        UsedFor = "Package Header",
                        UsedSpaceStart = 0,
                        UsedSpaceEnd = pcc.getHeader().Length
                    });

                    inStream.Seek(pcc.NameOffset, SeekOrigin.Begin);
                    for (int i = 0; i < pcc.NameCount; i++)
                    {
                        int strLength = inStream.ReadValueS32();
                        inStream.ReadString(strLength * -2, true, Encoding.Unicode);
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
                    for (int i = 0; i < pcc.ExportCount; i++)
                    {
                        inStream.Position += pcc.Exports[i].Header.Length;
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
                        UsedSpaceStart = BitConverter.ToInt32(pcc.getHeader(), 0x3A),
                        UsedSpaceEnd = BitConverter.ToInt32(pcc.getHeader(), 0x3E)
                    });

                    List<UsedSpace> usedExportsSpaces = new List<UsedSpace>();
                    inStream.Seek(pcc.ExportOffset, SeekOrigin.Begin);
                    for (int i = 0; i < pcc.ExportCount; i++)
                    {
                        ExportEntry exp = pcc.Exports[i];
                        usedExportsSpaces.Add(new UsedSpace
                        {
                            UsedFor = $"Export {exp.UIndex}",
                            UsedSpaceStart = exp.DataOffset,
                            UsedSpaceEnd = exp.DataOffset + exp.DataSize
                        });
                    }

                    usedExportsSpaces = usedExportsSpaces.OrderBy(x => x.UsedSpaceStart).ToList();
                    List<UsedSpace> continuousBlocks = new List<UsedSpace>();
                    UsedSpace continuous = new UsedSpace
                    {
                        UsedFor = "Continuous Export Data",
                        UsedSpaceStart = usedExportsSpaces[0].UsedSpaceStart,
                        UsedSpaceEnd = usedExportsSpaces[0].UsedSpaceEnd
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
                            continuousBlocks.Add(continuous);
                            continuous = new UsedSpace
                            {
                                UsedFor = "Continuous Export Data",
                                UsedSpaceStart = u.UsedSpaceStart,
                                UsedSpaceEnd = u.UsedSpaceEnd
                            };
                        }

                    }
                    continuousBlocks.Add(continuous);
                    UnusedSpaceList.AddRange(used);
                    UnusedSpaceList.AddRange(continuousBlocks);
                }
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
                            if (pcc.isName(val))
                            {
                                s += $", Name: {pcc.getNameEntry(val)}";
                            }

                            if (pcc.getEntry(val) is ExportEntry exp)
                            {
                                s += $", Export: {exp.ObjectName}";
                            }
                            else if (pcc.getEntry(val) is ImportEntry imp)
                            {
                                s += $", Import: {imp.ObjectName}";
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

        public class UsedSpace
        {
            public int UsedSpaceStart { get; set; }
            public int UsedSpaceEnd { get; set; }
            public string UsedFor { get; set; }

            public override string ToString() => $"{UsedFor} 0x{UsedSpaceStart:X6} - 0x{UsedSpaceEnd:X6}";
        }
    }
}
