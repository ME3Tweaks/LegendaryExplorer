using Gammtek.Conduit.Extensions.IO;
using ME3Explorer.SharedUI;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
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

namespace ME3Explorer.MountEditor
{
    /// <summary>
    /// Interaction logic for MountEditorWPF.xaml
    /// </summary>
    public partial class MountEditorWPF : NotifyPropertyChangedWindowBase
    {
        public ObservableCollectionExtended<MountFlag> MountIDValues { get; } = new ObservableCollectionExtended<MountFlag>();
        private List<MountFlag> ME2MountFlags = new List<MountFlag>();
        private List<MountFlag> ME3MountFlags = new List<MountFlag>();
        private bool _isME2;
        public bool IsME2
        {
            get => _isME2;
            set => SetProperty(ref _isME2, value);
        }
        private string _currentTLKIDString;
        public string CurrentTLKIDString
        {
            get => _currentTLKIDString;
            set => SetProperty(ref _currentTLKIDString, value);
        }

        private string _currentMountFileText;
        public string CurrentMountFileText
        {
            get => _currentMountFileText;
            set => SetProperty(ref _currentMountFileText, value);
        }


        public MountEditorWPF()
        {
            ME2MountFlags.Add(new MountFlag(0x1, "0x01 - No save file dependency on DLC"));
            ME2MountFlags.Add(new MountFlag(0x2, "0x02 - Save file dependency on DLC"));

            ME3MountFlags.Add(new MountFlag(0x8,  "0x08 - SP only | Does not require DLC in save file"));
            ME3MountFlags.Add(new MountFlag(0x9,  "0x09 - SP only | No save file dependency on DLC"));
            ME3MountFlags.Add(new MountFlag(0x1C, "0x1C - SP & MP | No save file dependency on DLC"));
            ME3MountFlags.Add(new MountFlag(0x0C, "0x0C - MP only | Loads in MP (PATCH)"));
            ME3MountFlags.Add(new MountFlag(0x14, "0x14 - MP only | Loads in MP"));
            ME3MountFlags.Add(new MountFlag(0x34, "0x34 - MP only | Loads in MP"));
            CurrentMountFileText = "No mount file loaded. Mouse over fields for descriptions of their values.";
            MountIDValues.AddRange(ME3MountFlags);
            DataContext = this;
            InitializeComponent();
            IsME2Checkbox_Click(null, null);
            MountComboBox.SelectedIndex = 0;
        }

        private void PreviewIntegerInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            // Use SelectionStart property to find the caret position.
            // Insert the previewed text into the existing text in the textbox.
            var fullText = textBox.Text.Insert(textBox.SelectionStart, e.Text);

            double val;
            // If parsing is successful, set Handled to false
            e.Handled = !double.TryParse(fullText, out val);
        }

        public class MountFlag
        {
            public MountFlag(byte flag, string displayString)
            {
                this.Flag = flag;
                this.DisplayString = displayString;
            }

            public byte Flag { get; private set; }
            public string DisplayString { get; private set; }
        }

        private void LoadMountFile_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog m = new CommonOpenFileDialog
            {
                EnsurePathExists = true,
                Title = "Select Mount.dlc file",
            };
            m.Filters.Add(new CommonFileDialogFilter("Mount files", ".dlc"));
            if (m.ShowDialog() == CommonFileDialogResult.Ok)
            {
                MemoryStream ms = new MemoryStream(File.ReadAllBytes(m.FileName));
                byte b = (byte)ms.ReadByte();
                if (b == 0)
                {
                    LoadMountFileME2(ms);
                }
                else
                {
                    LoadMountFileME3(ms);
                }
                CurrentMountFileText = m.FileName;
            }

        }

        private void LoadMountFileME2(MemoryStream ms)
        {
            IsME2 = true;
            MountIDValues.ClearEx();
            MountIDValues.AddRange(ME2MountFlags);

            ms.Seek(0x1, SeekOrigin.Begin);
            var flag = (byte)ms.ReadByte();
            var correspondingFlag = ME2MountFlags.FirstOrDefault(x => x.Flag == flag);
            if (correspondingFlag != null)
            {
                MountComboBox.SelectedItem = correspondingFlag;
            }

            ms.Seek(0xC, SeekOrigin.Begin);
            MountPriority_TextBox.Text = ms.ReadInt16().ToString(); //Wiki states 32-bit, but I am pretty sure this is actually in-game 16-bit

            ms.Seek(0x2C, SeekOrigin.Begin);
            HumanReadable_TextBox.Text = ms.ReadString(ms.ReadUInt32(), true);

            TLKID_TextBox.Text = ms.ReadInt32().ToString();

            DLCFolder_TextBox.Text = ms.ReadString(ms.ReadUInt32(), true);
        }

        private void LoadMountFileME3(MemoryStream ms)
        {
            IsME2 = false;
            MountIDValues.ClearEx();
            MountIDValues.AddRange(ME3MountFlags);

            ms.Seek(0x10, SeekOrigin.Begin);
            MountPriority_TextBox.Text = ms.ReadInt16().ToString();

            ms.Seek(0x18, SeekOrigin.Begin);
            var flag = (byte)ms.ReadByte();
            var correspondingFlag = ME3MountFlags.FirstOrDefault(x => x.Flag == flag);
            if (correspondingFlag != null)
            {
                MountComboBox.SelectedItem = correspondingFlag;
            }

            ms.Seek(0x1C, SeekOrigin.Begin);
            TLKID_TextBox.Text = ms.ReadInt32().ToString();
        }

        private void SaveMountFile_Click(object sender, RoutedEventArgs e)
        {
            if (validate())
            {
                CommonSaveFileDialog m = new CommonSaveFileDialog
                {
                    EnsurePathExists = true,
                    Title = "Select Mount.dlc file save destination",
                };
                m.Filters.Add(new CommonFileDialogFilter("Mount files", ".dlc"));
                if (m.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    MountFlag mf = (MountFlag)MountComboBox.SelectedItem;
                    ushort priority = ushort.Parse(MountPriority_TextBox.Text.Trim());
                    int tlkid = int.Parse(TLKID_TextBox.Text.Trim());
                    string commonname = HumanReadable_TextBox.Text;
                    string dlcfolder = DLCFolder_TextBox.Text;
                    MemoryStream ms = new MemoryStream();
                    if (IsME2)
                    {
                        ms.WriteByte(0x0);

                        //@ 0x01 - Mount Flag
                        ms.WriteByte(mf.Flag);
                        ms.WriteInt16(0x0);

                        //@ 0x04
                        ms.WriteInt32(0x82);
                        ms.WriteInt32(0x40);

                        //@ 0x0C - Mount Priority
                        ms.WriteUInt16(priority);
                        ms.WriteInt16(0x0);

                        //@ 0x10
                        ms.WriteInt32(0x03);

                        //@ 0x14 - Appears to be a GUID. Common across all DLC though. Maybe some sort of magic GUID or something.
                        var guidbytes = new byte[] { 0xAE, 0x0F, 0x43, 0xDD, 0x0B, 0x52, 0x5D, 0x4C, 0x9E, 0x28, 0x0D, 0x77, 0x6D, 0x86, 0x91, 0x55 };
                        ms.WriteBytes(guidbytes);
                        ms.WriteInt32(0x0);
                        ms.WriteInt32(0x2);

                        //@ 0x2C - Common Name
                        //ms.WriteInt32(commonname.Length);
                        ms.WriteStringASCII(commonname);

                        //@ 0x00 After CommonName - TLK ID
                        ms.WriteInt32(tlkid);

                        //@ 0x00 After TLKID - FolderName
                        //ms.WriteInt32(dlcfolder.Length);
                        ms.WriteStringASCII(dlcfolder);

                        //@ Final 4 bytes
                        ms.WriteInt32(0x0);
                    }
                    else
                    {
                        ms.WriteInt32(0x1);
                        ms.WriteInt32(0x2AC);
                        ms.WriteInt32(0xC2);
                        ms.WriteInt32(0x3006B);

                        //@ 0x10 - Mount Priority
                        ms.WriteUInt16(priority);
                        ms.WriteUInt16(0x0);
                        ms.WriteInt32(0x0);

                        //@ 0x18 - Mount Flag
                        ms.WriteInt32(mf.Flag); //Write as 32-bit since the rest is just zeros anyways.

                        //@ 0x1C - TLK ID (x2)
                        ms.WriteInt32(tlkid);
                        ms.WriteInt32(tlkid);
                        ms.WriteInt32(0x0);
                        ms.WriteInt32(0x0);

                        //@ 0x2C - Unknown, Possible double GUID?
                        // Also all remaining zeros.
                        var guidbytes = new byte[] { 0x5A, 0x7B, 0xBD, 0x26, 0xDD, 0x41, 0x7E, 0x49, 0x9C, 0xC6, 0x60, 0xD2, 0x58, 0x72, 0x78, 0xEB, 0x2E, 0x2C, 0x6A, 0x06, 0x13, 0x0A, 0xE4, 0x47, 0x83, 0xEA, 0x08, 0xF3, 0x87, 0xA0, 0xE2, 0xDA, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                        ms.WriteBytes(guidbytes);

                    }
                    File.WriteAllBytes(m.FileName, ms.ToArray());
                }
            }
        }

        private bool validate()
        {
            return true;
        }

        private void PreviewShortInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            // Use SelectionStart property to find the caret position.
            // Insert the previewed text into the existing text in the textbox.
            var fullText = textBox.Text.Insert(textBox.SelectionStart, e.Text);

            double val;
            // If parsing is successful, set Handled to false
            var handled = !double.TryParse(fullText, out val);

            if (handled)
            {
                int value = int.Parse(fullText);
                e.Handled = value > 0 && value < short.MaxValue; //16-bit limit
            }
        }

        private void TLKID_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(TLKID_TextBox.Text, out int tlkValue))
            {
                if (IsME2)
                {
                    CurrentTLKIDString = ME2Explorer.ME2TalkFiles.findDataById(tlkValue);
                }
                else
                {
                    CurrentTLKIDString = ME3TalkFiles.findDataById(tlkValue);
                }
            }
        }

        private void IsME2Checkbox_Click(object sender, RoutedEventArgs e)
        {
            IsME2 = ME2CheckBox.IsChecked.Value;
            DLCFolder_TextBox.Watermark = IsME2 ? "DLC Folder Name (e.g. DLC_MOD_MYMOD)" : "Not used in ME3";
            HumanReadable_TextBox.Watermark = IsME2 ? "DLC Human Readable Name (e.g. Superpowers Pack)" : "Not used in ME3";
        }
    }
}
