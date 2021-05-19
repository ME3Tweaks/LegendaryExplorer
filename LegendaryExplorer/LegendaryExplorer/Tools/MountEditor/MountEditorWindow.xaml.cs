using System;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK;

namespace LegendaryExplorer.Tools.MountEditor
{
    /// <summary>
    /// Interaction logic for MountEditorWPF.xaml
    /// </summary>
    public partial class MountEditorWindow : TrackingNotifyPropertyChangedWindowBase
    {
        public ObservableCollectionExtended<UIMountFlag> MountIDValues { get; } = new();
        private readonly List<UIMountFlag> ME2MountFlags = new();
        private readonly List<UIMountFlag> ME3MountFlags = new();

        public ObservableCollectionExtended<UIGameID> Games { get; } = new()
        {
            new UIGameID(MEGame.ME2, "Mass Effect 2"),
            new UIGameID(MEGame.ME3, "Mass Effect 3"),
            new UIGameID(MEGame.LE2, "Mass Effect 2 LE"),
            new UIGameID(MEGame.LE3, "Mass Effect 3 LE")
        };

        private UIGameID _selectedGame;
        public UIGameID SelectedGame
        {
            get => _selectedGame;
            set
            {
                if (SetProperty(ref _selectedGame, value))
                {
                    SelectedGameChanged();
                }
            }
        }

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


        public MountEditorWindow() : base("Mount Editor", true)
        {
            ME2MountFlags.Add(new UIMountFlag(EMountFileFlag.ME2_UNKNOWNMOUNTFLAG, "0x00 | Mount Flag (Unknown purpose)"));
            ME2MountFlags.Add(new UIMountFlag(EMountFileFlag.ME2_NoSaveFileDependency, "0x01 | No save file dependency on DLC"));
            ME2MountFlags.Add(new UIMountFlag(EMountFileFlag.ME2_SaveFileDependency, "0x02 | Save file dependency on DLC"));
            ME2MountFlags.Add(new UIMountFlag(EMountFileFlag.ME2_UNKNOWNMOUNTFLAG2, "0x03 | Mount Flag (Unknown purpose)"));

            ME3MountFlags.Add(new UIMountFlag(EMountFileFlag.ME3_SPOnly_NoSaveFileDependency, "0x08 - SP only | No file dependency on DLC"));
            ME3MountFlags.Add(new UIMountFlag(EMountFileFlag.ME3_SPOnly_SaveFileDependency, "0x09 - SP only | Save file dependency on DLC"));
            ME3MountFlags.Add(new UIMountFlag(EMountFileFlag.ME3_SPMP_SaveFileDependency, "0x1C - SP & MP | No save file dependency on DLC"));
            ME3MountFlags.Add(new UIMountFlag(EMountFileFlag.ME3_Patch, "0x0C - PATCH"));
            ME3MountFlags.Add(new UIMountFlag(EMountFileFlag.ME3_MPOnly_1, "0x14 - MP only | Loads in MP"));
            ME3MountFlags.Add(new UIMountFlag(EMountFileFlag.ME3_MPOnly_2, "0x34 - MP only | Loads in MP"));
            CurrentMountFileText = "No mount file loaded. Mouse over fields for descriptions of their values.";
            MountIDValues.AddRange(ME3MountFlags);
            DataContext = this;
            InitializeComponent();
            SelectedGame = Games[0];
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

        public sealed record UIGameID(MEGame Game, string DisplayString);

        private void LoadMountFile_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog m = new()
            {
                EnsurePathExists = true,
                Title = "Select Mount.dlc file",
            };

            m.Filters.Add(new CommonFileDialogFilter("Mount files", ".dlc"));
            if (m.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                LoadFile(m.FileName);
            }

        }

        public void LoadFile(string fileName)
        {
            var mf = new MountFile(fileName);
            SelectedGame = Games.First(uig => uig.Game == mf.Game);
            DLCFolder_TextBox.Text = IsME2 ? mf.ME2Only_DLCFolderName : "Not used in ME3";
            HumanReadable_TextBox.Text = IsME2 ? mf.ME2Only_DLCHumanName : "Not used in ME3";
            MountIDValues.ClearEx();
            MountIDValues.AddRange(IsME2 ? ME2MountFlags : ME3MountFlags);
            TLKID_TextBox.Text = mf.TLKID.ToString();
            MountPriority_TextBox.Text = mf.MountPriority.ToString();
            var flagset = IsME2 ? ME2MountFlags : ME3MountFlags;
            var flag = flagset.First(x => x.Flag == mf.MountFlag);
            MountComboBox.SelectedItem = flag;
            CurrentMountFileText = fileName;
        }

        private void SaveMountFile_Click(object sender, RoutedEventArgs e)
        {
            if (Validate())
            {
                CommonSaveFileDialog m = new()
                {
                    EnsurePathExists = true,
                    Title = "Select Mount.dlc file save destination",
                    DefaultExtension = "dlc",
                    AlwaysAppendDefaultExtension = true,
                    DefaultFileName = "mount.dlc",
                    InitialDirectory = (!string.IsNullOrEmpty(CurrentMountFileText) && File.Exists(CurrentMountFileText) ? Path.GetDirectoryName(CurrentMountFileText) : null)
                };
                m.Filters.Add(new CommonFileDialogFilter("Mount files", ".dlc"));
                if (m.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    var mf = new MountFile() //We will write this to disk
                    {
                        Game = SelectedGame.Game,
                        MountPriority = ushort.Parse(MountPriority_TextBox.Text.Trim()),
                        TLKID = int.Parse(TLKID_TextBox.Text.Trim()),
                        MountFlag = ((UIMountFlag)MountComboBox.SelectedItem).Flag
                    };

                    if (mf.Game is MEGame.ME2 or MEGame.LE2)
                    {
                        mf.ME2Only_DLCFolderName = DLCFolder_TextBox.Text;
                        mf.ME2Only_DLCHumanName = HumanReadable_TextBox.Text;
                    }
                    mf.WriteMountFile(m.FileName);
                    MessageBox.Show("Done.");
                }
            }
        }

        private bool Validate()
        {
            if (!ushort.TryParse(MountPriority_TextBox.Text, out ushort _))
            {
                Xceed.Wpf.Toolkit.MessageBox.Show("Mount priority must be a value between 1 and " + ushort.MaxValue + ".", "Validation error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!int.TryParse(TLKID_TextBox.Text, out int valuex) || valuex <= 0)
            {
                Xceed.Wpf.Toolkit.MessageBox.Show("TLK ID must be between 1 and " + (uint.MaxValue / 2) + ".", "Validation error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (IsME2)
            {
                if (SelectedGame.Game is MEGame.ME2 && HumanReadable_TextBox.Text.Length < 5)
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
                CurrentTLKIDString = IsME2 ? ME2TalkFiles.findDataById(tlkValue) : ME3TalkFiles.findDataById(tlkValue);
            }
        }

        private void SelectedGameChanged()
        {
            IsME2 = SelectedGame.Game is MEGame.ME2 or MEGame.LE2;
            DLCFolder_TextBox.Watermark = IsME2 ? "DLC Folder Name (e.g. DLC_MOD_MYMOD)" : "Not used in ME3";
            HumanReadable_TextBox.Watermark = IsME2 ? "DLC Human Readable Name (e.g. Superpowers Pack)" : "Not used in ME3";
            MountIDValues.ClearEx();
            MountIDValues.AddRange(IsME2 ? ME2MountFlags : ME3MountFlags);
            MountComboBox.SelectedIndex = 0;
            TLKID_TextChanged(null, null);
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
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string ext = Path.GetExtension(files[0]).ToLower();
                if (ext != ".dlc")
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }
    }
}
