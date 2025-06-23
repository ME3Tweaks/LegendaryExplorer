using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Be.Windows.Forms;
using LegendaryExplorer.Audio;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Misc;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.Soundpanel
{
    public partial class HIRCItemEditor : NotifyPropertyChangedControlBase, IDisposable
    {
        public HIRCItemEditor()
        {
            InitializeComponent();
            LoadCommands();
        }
        
        private HexBox SoundpanelHIRC_Hexbox;
        private ReadOptimizedByteProvider hircHexProvider;
        
        private bool ControlLoaded;

        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            if (!ControlLoaded)
            {
                SoundpanelHIRC_Hexbox = (HexBox)HIRC_Hexbox_Host.Child;
                hircHexProvider = new ReadOptimizedByteProvider();

                SoundpanelHIRC_Hexbox.ByteProvider = hircHexProvider;
                SoundpanelHIRC_Hexbox.ByteProvider.Changed += SoundpanelHIRC_Hexbox_BytesChanged;

                this.bind(HexBoxMinWidthProperty, SoundpanelHIRC_Hexbox, nameof(SoundpanelHIRC_Hexbox.MinWidth));
                this.bind(HexBoxMaxWidthProperty, SoundpanelHIRC_Hexbox, nameof(SoundpanelHIRC_Hexbox.MaxWidth));

                ControlLoaded = true;
            }
        }

        public void Dispose()
        {
            SoundpanelHIRC_Hexbox?.Dispose();
            SoundpanelHIRC_Hexbox = null;
            HIRC_Hexbox_Host?.Child?.Dispose();
            HIRC_Hexbox_Host?.Dispose();
        }

        public int HexBoxMinWidth
        {
            get => (int)GetValue(HexBoxMinWidthProperty);
            set => SetValue(HexBoxMinWidthProperty, value);
        }
        public static readonly DependencyProperty HexBoxMinWidthProperty = DependencyProperty.Register(nameof(HexBoxMinWidth), typeof( int ), typeof( Soundpanel ), new PropertyMetadata(0));

        public int HexBoxMaxWidth
        {
            get => (int)GetValue(HexBoxMaxWidthProperty);
            set => SetValue(HexBoxMaxWidthProperty, value);
        }
        public static readonly DependencyProperty HexBoxMaxWidthProperty = DependencyProperty.Register(nameof(HexBoxMaxWidth), typeof( int ), typeof( Soundpanel ), new PropertyMetadata(0));

        public static readonly DependencyProperty SelectedHIRCItemProperty = DependencyProperty.Register(
            nameof(SelectedHIRCItem), typeof(HIRCDisplayObject), typeof(HIRCItemEditor), new PropertyMetadata(null, OnSelectedHIRCItemChanged));

        public HIRCDisplayObject SelectedHIRCItem
        {
            get => (HIRCDisplayObject)GetValue(SelectedHIRCItemProperty);
            set => SetValue(SelectedHIRCItemProperty, value);
        }
        
        private static void OnSelectedHIRCItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            HIRCItemEditor c = (HIRCItemEditor)d;
            HIRCDisplayObject newItem = (HIRCDisplayObject)e.NewValue;

            c.HIRCHexChanged = false;
            
            if (newItem is not null)
            {
                c._originalHIRCHex = (byte[])newItem.Data.Clone();
                c.hircHexProvider.ReplaceBytes(c._originalHIRCHex);
                c.SoundpanelHIRC_Hexbox.Refresh();
            }
            else
            {
                c._originalHIRCHex = null;
                c.hircHexProvider.Clear();
                c.SoundpanelHIRC_Hexbox.Refresh();
            }
        }
        
        private byte[] _originalHIRCHex;
        
        private bool _hircHexChanged;
        public bool HIRCHexChanged
        {
            get => _hircHexChanged;
            private set => SetProperty(ref _hircHexChanged, value);
        }
        
        private void LoadCommands()
        {
            SaveHIRCHexCommand = new GenericCommand(SaveHIRCHex, CanSaveHIRCHex);
        }
        
        public ICommand SaveHIRCHexCommand { get; private set; }

        public void FixBuggyHexBox()
        {
            //This makes the hexbox widen by 1 and then shrink by 1
            //For some rason it won't calculate the scrollbar again unless you do this
            //which is very annoying.
            var currentWidth = HIRC_Hexbox_Host.Width;
            if (currentWidth > 500)
            {
                SoundpanelHIRC_Hexbox.Width -= 1;
                HIRC_Hexbox_Host.UpdateLayout();
                SoundpanelHIRC_Hexbox.Width += 1;
            }
            else
            {
                SoundpanelHIRC_Hexbox.Width += 1;
                HIRC_Hexbox_Host.UpdateLayout();
                SoundpanelHIRC_Hexbox.Width -= 1;
            }

            HIRC_Hexbox_Host.UpdateLayout();
            SoundpanelHIRC_Hexbox.Select(0, 1);
            SoundpanelHIRC_Hexbox.ScrollByteIntoView();
        }

        public void GoToHexAddress(int index, int length)
        {
            if (SoundpanelHIRC_Hexbox != null && hircHexProvider != null && hircHexProvider.Span.Length > 0)
            {
                SoundpanelHIRC_Hexbox.SelectionStart = index;
                SoundpanelHIRC_Hexbox.SelectionLength = length;
                SoundpanelHIRC_Hexbox.ScrollByteIntoView(index);
            }
        }
        
        private bool CanSaveHIRCHex() => HIRCHexChanged;

        private void SaveHIRCHex()
        {
            SelectedHIRCItem.CommitHex(hircHexProvider.Span.ToArray());
            HIRCHexChanged = false;
        }
        
        private void SoundpanelHIRC_Hexbox_BytesChanged(object sender, EventArgs e)
        {
            if (_originalHIRCHex != null)
            {
                HIRCHexChanged = !hircHexProvider.Span.SequenceEqual(_originalHIRCHex);
            }
        }
        
        private void HIRC_ToggleHexboxWidth_Click(object sender, RoutedEventArgs e)
        {
            GridLength len = HexboxColumnDefinition.Width;
            if (len.Value < HexboxColumnDefinition.MaxWidth)
            {
                HexboxColumnDefinition.Width = new GridLength(HexboxColumnDefinition.MaxWidth);
            }
            else
            {
                HexboxColumnDefinition.Width = new GridLength(HexboxColumnDefinition.MinWidth);
            }
        }
        
        private void HIRCNotableItems_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            /*SoundpanelHIRC_Hexbox.UnhighlightAll();
            if (HIRCNotableItems_ListBox.SelectedItem is Soundpanel.HIRCNotableItem h)
            {
                SoundpanelHIRC_Hexbox.Highlight(h.Offset, h.Length);
                SoundpanelHIRC_Hexbox.SelectionStart = h.Offset;
                SoundpanelHIRC_Hexbox.SelectionLength = 1;
            }*/
        }
        
        private void Soundpanel_HIRCHexbox_SelectionChanged(object sender, EventArgs e)
        {
            if (SelectedHIRCItem != null)
            {
                ReadOptimizedByteProvider hbp = (ReadOptimizedByteProvider)SoundpanelHIRC_Hexbox.ByteProvider;
                var memory = hbp.Span;
                int start = (int)SoundpanelHIRC_Hexbox.SelectionStart;
                int len = (int)SoundpanelHIRC_Hexbox.SelectionLength;
                int size = (int)SoundpanelHIRC_Hexbox.ByteProvider.Length;
                try
                {
                    if (memory.Length > 0 && start != -1 && start < size)
                    {
                        string s = $"Byte: {memory[start]}"; //if selection is same as size this will crash.
                        if (start <= memory.Length - 4)
                        {
                            /*int val = EndianReader.ToInt32(memory, start, Pcc.Endian);
                            float fval = EndianReader.ToSingle(memory, start, Pcc.Endian);
                            s += $", Int: {val} (0x{val:X8}) Float: {fval}";
                            var referencedHIRCbyID = HIRCObjects.FirstOrDefault(x => x.ID == val);

                            if (referencedHIRCbyID != null)
                            {
                                s += $", HIRC Object (by ID) Index: {referencedHIRCbyID.Index}";
                            }

                            EmbeddedWEMFile referencedWEMbyID = AllWems.FirstOrDefault(x => x.Id == val);

                            if (referencedWEMbyID != null)
                            {
                                s += $", Embedded WEM Object (by ID): {referencedWEMbyID.DisplayString}";
                            }*/

                            //if (CurrentLoadedExport.FileRef.getEntry(val) is ExportEntry exp)
                            //{
                            //    s += $", Export: {exp.ObjectName}";
                            //}
                            //else if (CurrentLoadedExport.FileRef.getEntry(val) is ImportEntry imp)
                            //{
                            //    s += $", Import: {imp.ObjectName}";
                            //}
                        }

                        s += $" | Start=0x{start:X8} ";
                        if (len > 0)
                        {
                            s += $"Length=0x{len:X8} ";
                            s += $"End=0x{(start + len - 1):X8}";
                        }

                        HIRCStatusBar_LeftMostText.Text = s;
                    }
                    else
                    {
                        HIRCStatusBar_LeftMostText.Text = "Nothing Selected";
                    }
                }
                catch (Exception)
                {
                }

                SoundpanelHIRC_Hexbox.Refresh();
            }
        }
    }
}

