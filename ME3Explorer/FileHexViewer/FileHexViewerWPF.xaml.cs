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
            var result = PromptDialog.Prompt(this, "Enter file offset - hex only, no 0x or anything.","Enter offset");
            if (result != "" && UInt32.TryParse(result, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint offset))
            {
                Interpreter_Hexbox.SelectionStart = offset;
                Interpreter_Hexbox.SelectionLength = 1;
            }
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog { Filter = App.FileFilter };
            if (d.ShowDialog() == true)
            {
                pcc = MEPackageHandler.OpenMEPackage(d.FileName);
                bytes = File.ReadAllBytes(d.FileName);
                Interpreter_Hexbox.ByteProvider = new DynamicByteProvider(bytes);
            }
        }


        private void FileHexViewerWPF_OnLoaded(object sender, RoutedEventArgs e)
        {
            Interpreter_Hexbox = (HexBox)Interpreter_Hexbox_Host.Child;
        }

        private void FileHexViewerWPF_OnClosing(object sender, CancelEventArgs e)
        {
            pcc.Release();
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
                        if (pcc.isName(val))
                        {
                            s += $", Name: {pcc.getNameEntry(val)}";
                        }
                        if (pcc.getEntry(val) is IExportEntry exp)
                        {
                            s += $", Export: {exp.ObjectName}";
                        }
                        else if (pcc.getEntry(val) is ImportEntry imp)
                        {
                            s += $", Import: {imp.ObjectName}";
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
    }
}
