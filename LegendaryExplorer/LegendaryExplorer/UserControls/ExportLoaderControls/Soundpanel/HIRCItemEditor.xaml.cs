using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Be.Windows.Forms;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Misc;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.Soundpanel
{
    public partial class HIRCItemEditor : IDisposable
    {
        public HIRCItemEditor()
        {
            LoadCommands();
            InitializeComponent();
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
        public static readonly DependencyProperty HexBoxMinWidthProperty = DependencyProperty.Register(nameof(HexBoxMinWidth), typeof( int ), typeof( HIRCItemEditor ), new PropertyMetadata(0));

        public int HexBoxMaxWidth
        {
            get => (int)GetValue(HexBoxMaxWidthProperty);
            set => SetValue(HexBoxMaxWidthProperty, value);
        }
        public static readonly DependencyProperty HexBoxMaxWidthProperty = DependencyProperty.Register(nameof(HexBoxMaxWidth), typeof( int ), typeof( HIRCItemEditor ), new PropertyMetadata(0));

        public HIRCDisplayObject SelectedHIRCItem
        {
            get => (HIRCDisplayObject)GetValue(SelectedHIRCItemProperty);
            set => SetValue(SelectedHIRCItemProperty, value);
        }
        
        public static readonly DependencyProperty SelectedHIRCItemProperty = DependencyProperty.Register(
            nameof(SelectedHIRCItem), typeof(HIRCDisplayObject), typeof(HIRCItemEditor), new PropertyMetadata(null, OnSelectedHIRCItemChanged));

        public WwiseBankScans WwiseBinaryScanner
        {
            get => (WwiseBankScans)GetValue(WwiseBinaryScannerProperty);
            set => SetValue(WwiseBinaryScannerProperty, value);
        }
        
        public static readonly DependencyProperty WwiseBinaryScannerProperty = DependencyProperty.Register(
            nameof(WwiseBinaryScanner), typeof(WwiseBankScans), typeof(HIRCItemEditor), new PropertyMetadata(default(WwiseBankScans)));


        
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
                c.RefreshTreeView();
            }
            else
            {
                c._originalHIRCHex = null;
                c.hircHexProvider.Clear();
                c.SoundpanelHIRC_Hexbox.Refresh();
                c.HIRCTreeViewItems.ClearEx();
            }
        }

        private void RefreshTreeView()
        {
            if(SelectedHIRCItem is null || hircHexProvider is null || hircHexProvider.Span.Length == 0)
            {
                return;
            }

            var reader = new EndianReader(hircHexProvider.Span.ToArray());
            var hircNode = WwiseBinaryScanner.MakeHIRCNode(SelectedHIRCItem.Index, reader, SelectedHIRCItem.Context.Version, SelectedHIRCItem.Context.UseFeedback);
            ChopTreeViewItemNames([hircNode]);
            WwiseBinaryScanner.AddNodeReferences();
            HIRCTreeViewItems.ClearEx();
            if (hircNode != null)
            {
                hircNode.IsSelected = true;
                hircNode.IsExpanded = true;
                HIRCTreeViewItems.Add(hircNode);
            }
        }

        private void ChopTreeViewItemNames(IEnumerable<BinInterpNode> nodes)
        {
            foreach (var node in nodes)
            {
                node.Header = "0x" + node.Header.Substring(6); // cut out so many digits from hex offset - this is dumb
                ChopTreeViewItemNames(node.Items.OfType<BinInterpNode>());
            }
        }
        
        private byte[] _originalHIRCHex;
        
        private bool _hircHexChanged;

        private bool HIRCHexChanged
        {
            get => _hircHexChanged;
            set => SetProperty(ref _hircHexChanged, value);
        }
        
        private void LoadCommands()
        {
            SaveHIRCHexCommand = new GenericCommand(SaveHIRCHex, CanSaveHIRCHex);
        }
        
        public ICommand SaveHIRCHexCommand { get; private set; }
        public ObservableCollectionExtended<ITreeItem> HIRCTreeViewItems { get; set; } = new();

        public void FixBuggyHexBox()
        {
            //This makes the hexbox widen by 1 and then shrink by 1
            //For some reason it won't calculate the scrollbar again unless you do this
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

        public void SelectHex(int index, int length)
        {
            if (SoundpanelHIRC_Hexbox != null && hircHexProvider != null && hircHexProvider.Span.Length > 0)
            {
                SoundpanelHIRC_Hexbox.SelectionStart = index;
                SoundpanelHIRC_Hexbox.SelectionLength = length;
                SoundpanelHIRC_Hexbox.ScrollByteIntoView(index);
            }
        }
        
        public long GetSelectedHex()
        {
            if (SoundpanelHIRC_Hexbox != null)
            {
                return SoundpanelHIRC_Hexbox.SelectionStart;
            }

            return -1;
        }
        
        private bool CanSaveHIRCHex() => HIRCHexChanged;

        private void SaveHIRCHex()
        {
            if(SelectedHIRCItem is null || hircHexProvider is null || hircHexProvider.Span.Length == 0)
            {
                return;
            }
            
            SelectedHIRCItem.CommitHex(hircHexProvider.Span.ToArray());
            RefreshTreeView();
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

        private void HIRCBinary_TreeView_SelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            SoundpanelHIRC_Hexbox.UnhighlightAll();
            if (HIRCBinary_TreeView.SelectedItem is BinInterpNode b)
            {
                SoundpanelHIRC_Hexbox.Highlight(b.Offset, b.Length);
                SoundpanelHIRC_Hexbox.SelectionStart = b.Offset;
                SoundpanelHIRC_Hexbox.SelectionLength = b.Length;
            }
        }
    }
}

