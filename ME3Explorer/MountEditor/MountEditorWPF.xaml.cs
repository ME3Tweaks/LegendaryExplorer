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
        public ObservableCollectionExtended<UIMountFlag> MountIDValues { get; } = new ObservableCollectionExtended<UIMountFlag>();
        private readonly List<UIMountFlag> ME2MountFlags = new List<UIMountFlag>();
        private readonly List<UIMountFlag> ME3MountFlags = new List<UIMountFlag>();
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
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Mount Editor", new WeakReference(this));

            ME2MountFlags.Add(new UIMountFlag(EMountFileFlag.ME2_NoSaveFileDependency, "0x01 | No save file dependency on DLC"));
            ME2MountFlags.Add(new UIMountFlag(EMountFileFlag.ME2_SaveFileDependency, "0x02 | Save file dependency on DLC"));

            ME3MountFlags.Add(new UIMountFlag(EMountFileFlag.ME3_SPOnly_NoSaveFileDependency, "0x08 - SP only | No file dependency on DLC"));
            ME3MountFlags.Add(new UIMountFlag(EMountFileFlag.ME3_SPOnly_SaveFileDependency, "0x09 - SP only | Save file dependency on DLC"));
            ME3MountFlags.Add(new UIMountFlag(EMountFileFlag.ME3_SPMP_SaveFileDependency, "0x1C - SP & MP | No save file dependency on DLC"));
            ME3MountFlags.Add(new UIMountFlag(EMountFileFlag.ME3_MPOnly_Patch, "0x0C - MP only | Loads in MP (PATCH)"));
            ME3MountFlags.Add(new UIMountFlag(EMountFileFlag.ME3_MPOnly_2, "0x14 - MP only | Loads in MP"));
            ME3MountFlags.Add(new UIMountFlag(EMountFileFlag.ME3_MPOnly_2, "0x34 - MP only | Loads in MP"));
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

            // If parsing is successful, set Handled to false
            e.Handled = !double.TryParse(fullText, out double _);
        }

        public class UIMountFlag
        {
            public UIMountFlag(EMountFileFlag flag, string displayString)
            {
                this.Flag = flag;
                this.DisplayString = displayString;
            }

            public EMountFileFlag Flag { get; }
            public string DisplayString { get; }
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
                MountFile mf = new MountFile(m.FileName);
                ME2CheckBox.IsChecked = mf.IsME2;
                DLCFolder_TextBox.Text = mf.IsME2 ? mf.ME2Only_DLCFolderName : "Not used in ME3";
                HumanReadable_TextBox.Text = mf.IsME2 ? mf.ME2Only_DLCHumanName : "Not used in ME3";
                var flag = (IsME2 ? ME2MountFlags : ME3MountFlags).First(x => x.Flag == mf.MountFlag);
                MountComboBox.SelectedItem = flag;
                TLKID_TextBox.Text = mf.TLKID.ToString();
                MountPriority_TextBox.Text = mf.MountPriority.ToString();

                CurrentMountFileText = m.FileName;
            }

        }

        private void SaveMountFile_Click(object sender, RoutedEventArgs e)
        {
            if (validate())
            {
                CommonSaveFileDialog m = new CommonSaveFileDialog
                {
                    EnsurePathExists = true,
                    Title = "Select Mount.dlc file save destination",
                    DefaultExtension = "dlc",
                    AlwaysAppendDefaultExtension = true,
                    DefaultFileName = "mount.dlc"
                };
                m.Filters.Add(new CommonFileDialogFilter("Mount files", ".dlc"));
                if (m.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    MountFile mf = new MountFile(); //We will write this to disk
                    mf.IsME2 = IsME2;
                    if (mf.IsME2)
                    {
                        mf.ME2Only_DLCFolderName = DLCFolder_TextBox.Text;
                        mf.ME2Only_DLCHumanName = HumanReadable_TextBox.Text;
                    }
                    mf.MountPriority = ushort.Parse(MountPriority_TextBox.Text.Trim());
                    mf.TLKID = int.Parse(TLKID_TextBox.Text.Trim());
                    mf.MountFlag = ((UIMountFlag)MountComboBox.SelectedItem).Flag;
                    mf.WriteMountFile(m.FileName);
                    MessageBox.Show("Done.");
                }
            }
        }

        private bool validate()
        {
            if (!ushort.TryParse(MountPriority_TextBox.Text, out ushort _))
            {
                Xceed.Wpf.Toolkit.MessageBox.Show("Mount priority must be a value between 1 and " + ushort.MaxValue + ".", "Validation error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!int.TryParse(TLKID_TextBox.Text, out int valuex) || valuex <= 0)
            {
                Xceed.Wpf.Toolkit.MessageBox.Show("TLK ID must be between 1 and " + (UInt32.MaxValue / 2) + ".", "Validation error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (IsME2)
            {
                if (HumanReadable_TextBox.Text.Length < 5)
                {
                    Xceed.Wpf.Toolkit.MessageBox.Show("Human readable name must be at least 5 characters.\nUse the full name of your mod to prevent end-user confusion.", "Validation error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                if (!DLCFolder_TextBox.Text.StartsWith("DLC_"))
                {
                    Xceed.Wpf.Toolkit.MessageBox.Show("DLC Folder Name must start with \"DLC_\".\nMass Effect 2 will not load a DLC that does not start with this prefix.", "Validation error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            return true;
        }

        private void PreviewShortInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            // Use SelectionStart property to find the caret position.
            // Insert the previewed text into the existing text in the textbox.
            var fullText = textBox.Text.Insert(textBox.SelectionStart, e.Text);

            //Handled means it won't appear.
            var handled = double.TryParse(fullText, out double _);

            //Validate it
            if (handled)
            {
                if (int.TryParse(fullText, out int value))
                {
                    //logic is backwards. Handled means the character won't appear
                    e.Handled = value <= 0 || value > short.MaxValue; //16-bit limit
                    return;
                }
            }
            e.Handled = true;
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
            MountIDValues.ClearEx();
            MountIDValues.AddRange(IsME2 ? ME2MountFlags : ME3MountFlags);
            MountComboBox.SelectedIndex = 0;
            TLKID_TextChanged(null, null);
        }
    }

    public enum EMountFileFlag
    {
        ME2_NoSaveFileDependency = 0x1,
        ME2_SaveFileDependency = 0x2,
        ME3_SPOnly_NoSaveFileDependency = 0x8,
        ME3_SPOnly_SaveFileDependency = 0x9,
        ME3_SPMP_SaveFileDependency = 0x1C,
        ME3_MPOnly_Patch = 0x0C,
        ME3_MPOnly_1 = 0x14,
        ME3_MPOnly_2 = 0x34
    }

    public class MountFile
    {
        public bool IsME2 { get; set; }
        public ushort MountPriority { get; set; }
        public string ME2Only_DLCFolderName { get; set; }
        public string ME2Only_DLCHumanName { get; set; }
        public int TLKID { get; set; }
        public EMountFileFlag MountFlag { get; set; }
        /// <summary>
        /// Instantiates an empty mount file. Used for creating a new mount.
        /// </summary>
        public MountFile()
        {

        }

        /// <summary>
        /// Instantiates a mount file from a mount file on disk.
        /// </summary>
        /// <param name="filepath">Mountfile to load</param>
        public MountFile(string filepath)
        {
            using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(filepath)))
            {
                byte b = (byte)ms.ReadByte();
                if (b == 0)
                {
                    LoadMountFileME2(ms);
                }
                else
                {
                    LoadMountFileME3(ms);
                }
            }
        }

        private void LoadMountFileME2(MemoryStream ms)
        {
            IsME2 = true;
            ms.Seek(0x1, SeekOrigin.Begin);
            MountFlag = (EMountFileFlag)ms.ReadByte();
            ms.Seek(0xC, SeekOrigin.Begin);
            MountPriority = ms.ReadUInt16();
            ms.Seek(0x2C, SeekOrigin.Begin);
            ME2Only_DLCHumanName = ms.ReadString(ms.ReadUInt32(), true);
            TLKID = ms.ReadInt32();
            ME2Only_DLCFolderName = ms.ReadString(ms.ReadUInt32(), true);

        }

        private void LoadMountFileME3(MemoryStream ms)
        {
            ms.Seek(0x10, SeekOrigin.Begin);
            MountPriority = ms.ReadUInt16();
            ms.Seek(0x18, SeekOrigin.Begin);
            MountFlag = (EMountFileFlag)ms.ReadByte();
            ms.Seek(0x1C, SeekOrigin.Begin);
            TLKID = ms.ReadInt32();
        }

        /// <summary>
        /// Writes this Mountfile to the specified path.
        /// </summary>
        /// <param name="path">Path to write mount file to</param>
        public void WriteMountFile(string path)
        {
            MemoryStream ms = new MemoryStream();
            if (IsME2)
            {
                ms.WriteByte(0x0);

                //@ 0x01 - Mount Flag
                ms.WriteByte((byte)MountFlag);
                ms.WriteInt16(0x0);

                //@ 0x04
                ms.WriteInt32(0x82);
                ms.WriteInt32(0x40);

                //@ 0x0C - Mount Priority
                ms.WriteUInt16(MountPriority);
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
                ms.WriteStringASCII(ME2Only_DLCHumanName);

                //@ 0x00 After CommonName - TLK ID
                ms.WriteInt32(TLKID);

                //@ 0x00 After TLKID - FolderName
                //ms.WriteInt32(dlcfolder.Length);
                ms.WriteStringASCII(ME2Only_DLCFolderName);

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
                ms.WriteUInt16(MountPriority);
                ms.WriteUInt16(0x0);
                ms.WriteInt32(0x0);

                //@ 0x18 - Mount Flag
                ms.WriteInt32((byte)MountFlag); //Write as 32-bit since the rest is just zeros anyways.

                //@ 0x1C - TLK ID (x2)
                ms.WriteInt32(TLKID);
                ms.WriteInt32(TLKID);
                ms.WriteInt32(0x0);
                ms.WriteInt32(0x0);

                //@ 0x2C - Unknown, Possible double GUID?
                // Also all remaining zeros.
                var guidbytes = new byte[] { 0x5A, 0x7B, 0xBD, 0x26, 0xDD, 0x41, 0x7E, 0x49, 0x9C, 0xC6, 0x60, 0xD2, 0x58, 0x72, 0x78, 0xEB, 0x2E, 0x2C, 0x6A, 0x06, 0x13, 0x0A, 0xE4, 0x47, 0x83, 0xEA, 0x08, 0xF3, 0x87, 0xA0, 0xE2, 0xDA, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                ms.WriteBytes(guidbytes);

            }
            File.WriteAllBytes(path, ms.ToArray());
        }
    }
}
