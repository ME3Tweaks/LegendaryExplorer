using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using Be.Windows.Forms;
using ME3Explorer.ME1.Unreal.UnhoodBytecode;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using static ME3Explorer.Unreal.Bytecode;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for UnrealScriptWPF.xaml
    /// </summary>
    public partial class UnrealScriptWPF : ExportLoaderControl
    {
        private HexBox ScriptEditor_Hexbox;
        public ObservableCollectionExtended<BytecodeSingularToken> TokenList { get; private set; } = new ObservableCollectionExtended<BytecodeSingularToken>();
        public ObservableCollectionExtended<object> DecompiledScriptBlocks { get; private set; } = new ObservableCollectionExtended<object>();
        public ObservableCollectionExtended<ScriptHeaderItem> ScriptHeaderBlocks { get; private set; } = new ObservableCollectionExtended<ScriptHeaderItem>();
        public ObservableCollectionExtended<ScriptHeaderItem> ScriptFooterBlocks { get; private set; } = new ObservableCollectionExtended<ScriptHeaderItem>();

        private bool TokenChanging = false;

        private bool _bytesHaveChanged { get; set; }
        public bool BytesHaveChanged
        {
            get { return _bytesHaveChanged; }
            set
            {
                if (_bytesHaveChanged != value)
                {
                    _bytesHaveChanged = value;
                    OnPropertyChanged();
                }
            }
        }
        public UnrealScriptWPF()
        {
            InitializeComponent();
        }

        public override bool CanParse(IExportEntry exportEntry)
        {
            return (exportEntry.ClassName == "Function" || exportEntry.ClassName == "State") && (exportEntry.FileRef.Game == MEGame.ME3 || exportEntry.FileRef.Game == MEGame.ME1);
        }

        public override void LoadExport(IExportEntry exportEntry)
        {
            CurrentLoadedExport = exportEntry;
            ScriptEditor_Hexbox.ByteProvider = new DynamicByteProvider(CurrentLoadedExport.Data);
            ScriptEditor_Hexbox.ByteProvider.Changed += ByteProviderBytesChanged;

            if (exportEntry.ClassName == "Function")
            {
                StartOffset_Changer.Value = 32;
                StartOffset_Changer.Visibility = Visibility.Collapsed;
            }
            else
            {
                StartOffset_Changer.Visibility = Visibility.Visible;
            }

            StartFunctionScan(CurrentLoadedExport.Data);
        }

        private void StartFunctionScan(byte[] data)
        {
            if (CurrentLoadedExport.FileRef.Game == MEGame.ME3)
            {
                var func = new ME3Explorer.Unreal.Classes.Function(data, CurrentLoadedExport.FileRef, CurrentLoadedExport.ClassName == "State" ? Convert.ToInt32(StartOffset_Changer.Text) : 32);
                DecompiledScriptBlocks.Clear();
                TokenList.Clear();

                func.ParseFunction();
                DecompiledScriptBlocks.AddRange(func.ScriptBlocks);
                TokenList.AddRange(func.SingularTokenList);

                ScriptHeaderBlocks.Clear();
                int pos = 16;
                var nextItemCompilingChain = BitConverter.ToInt32(CurrentLoadedExport.Data, pos);
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Next item in loading chain", nextItemCompilingChain, pos, nextItemCompilingChain > 0 ? CurrentLoadedExport : null));

                pos += 4;
                nextItemCompilingChain = BitConverter.ToInt32(CurrentLoadedExport.Data, pos);
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Children Probe Start", nextItemCompilingChain, pos, nextItemCompilingChain > 0 ? CurrentLoadedExport : null));

                pos += 4;
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Unknown 2 (Memory size?)", BitConverter.ToInt32(CurrentLoadedExport.Data, pos), pos));

                pos += 4;
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Size", BitConverter.ToInt32(CurrentLoadedExport.Data, pos), pos));

                ScriptFooterBlocks.Clear();
                pos = CurrentLoadedExport.Data.Length - 6;
                string flagStr = func.GetFlags();
                ScriptFooterBlocks.Add(new ScriptHeaderItem("Native Index", BitConverter.ToInt16(CurrentLoadedExport.Data, pos), pos));
                pos += 2;
                ScriptFooterBlocks.Add(new ScriptHeaderItem("Flags", $"0x{BitConverter.ToInt32(CurrentLoadedExport.Data, pos).ToString("X8")} {func.GetFlags().Substring(6)}", pos));
            }
            else if (CurrentLoadedExport.FileRef.Game == MEGame.ME1)
            {
                DecompiledScriptBlocks.Clear();
                var funcoutput = UE3FunctionReader.ReadFunction(CurrentLoadedExport);
                var result = funcoutput.Split(new[] { '\r', '\n' }).Where(x=>x != "").ToList();
                DecompiledScriptBlocks.AddRange(result);
                //ME1Explorer.Unreal.Classes.Function func = new ME1Explorer.Unreal.Classes.Function(data, CurrentLoadedExport.FileRef as ME1Package);
                //try
                //{
                //    Function_TextBox.Text = func.ToRawText();
                //}
                //catch (Exception e)
                //{
                //    Function_TextBox.Text = "Error parsing function: " + e.Message;
                //}
            }
            else
            {
                //Function_TextBox.Text = "Parsing UnrealScript Functions for this game is not supported.";
            }
        }

        public override void UnloadExport()
        {
            CurrentLoadedExport = null;
        }

        private void hb1_SelectionChanged(object sender, EventArgs e)
        {
            if (!TokenChanging)
            {
                int start = (int)ScriptEditor_Hexbox.SelectionStart;
                int len = (int)ScriptEditor_Hexbox.SelectionLength;
                int size = (int)ScriptEditor_Hexbox.ByteProvider.Length;
                //TODO: Optimize this so this is only called when data has changed
                byte[] currentData = (ScriptEditor_Hexbox.ByteProvider as DynamicByteProvider).Bytes.ToArray();
                try
                {
                    if (currentData != null && start != -1 && start < size)
                    {
                        string s = $"Byte: {currentData[start]}"; //if selection is same as size this will crash.
                        if (start <= currentData.Length - 4)
                        {
                            int val = BitConverter.ToInt32(currentData, start);
                            s += $", Int: {val}";
                            if (CurrentLoadedExport.FileRef.isName(val))
                            {
                                s += $", Name: {CurrentLoadedExport.FileRef.getNameEntry(val)}";
                            }
                            if (CurrentLoadedExport.FileRef.getEntry(val) is IExportEntry exp)
                            {
                                s += $", Export: {exp.ObjectName}";
                            }
                            else if (CurrentLoadedExport.FileRef.getEntry(val) is ImportEntry imp)
                            {
                                s += $", Import: {imp.ObjectName}";
                            }
                        }
                        s += $" | Start=0x{start.ToString("X8")} ";
                        if (len > 0)
                        {
                            s += $"Length=0x{len.ToString("X8")} ";
                            s += $"End=0x{(start + len - 1).ToString("X8")}";
                        }
                        StatusBar_LeftMostText.Text = s;
                    }
                    else
                    {
                        StatusBar_LeftMostText.Text = "Nothing Selected";
                    }
                }
                catch (Exception)
                {
                }

                //Find which decompiled script block the cursor belongs to
                ListBox selectedBox = null;
                List<ListBox> allBoxesToUpdate = new List<ListBox>(new ListBox[] { Function_ListBox, Function_Header, Function_Footer });
                if (start >= 0x20 && start < CurrentLoadedExport.DataSize - 6)
                {
                    Token token = null;
                    foreach (object o in DecompiledScriptBlocks)
                    {
                        if (o is Token x && start >= x.pos && start < (x.pos + x.raw.Length))
                        {
                            token = x;
                            break;
                        }
                    }
                    if (token != null)
                    {
                        Function_ListBox.SelectedItem = token;
                    }

                    BytecodeSingularToken bst = TokenList.FirstOrDefault(x => x.startPos == start);
                    if (bst != null)
                    {
                        Tokens_ListBox.SelectedItem = bst;
                    }
                    selectedBox = Function_ListBox;
                }
                if (start >= 0x0C && start < 0x20)
                {
                    //header
                    int index = (start - 0xC) / 4;
                    Function_Header.SelectedIndex = index;
                    selectedBox = Function_Header;

                }
                if (start > CurrentLoadedExport.DataSize - 6)
                {
                    //footer
                    //yeah yeah I know this is very specific code.
                    if (start == CurrentLoadedExport.DataSize - 6 || start == CurrentLoadedExport.DataSize - 5)
                    {
                        Function_Footer.SelectedIndex = 0;
                    }
                    else
                    {
                        Function_Footer.SelectedIndex = 1;
                    }
                    selectedBox = Function_Footer;
                }

                //deselect the other boxes
                if (selectedBox != null)
                {
                    allBoxesToUpdate.Remove(selectedBox);
                }
                allBoxesToUpdate.ForEach(x => x.SelectedIndex = -1);
            }
        }

        private void UnrealScriptWPF_Loaded(object sender, RoutedEventArgs e)
        {
            ScriptEditor_Hexbox = (HexBox)ScriptEditor_Hexbox_Host.Child;
        }

        private void ByteProviderBytesChanged(object sender, EventArgs e)
        {
            BytesHaveChanged = true;
        }

        private void ScriptEditor_PreviewScript_Click(object sender, RoutedEventArgs e)
        {
            byte[] data = (ScriptEditor_Hexbox.ByteProvider as DynamicByteProvider).Bytes.ToArray();
            StartFunctionScan(data);
        }

        private void ScriptEditor_SaveHexChanges_Click(object sender, RoutedEventArgs e)
        {
            (ScriptEditor_Hexbox.ByteProvider as DynamicByteProvider).ApplyChanges();
            CurrentLoadedExport.Data = (ScriptEditor_Hexbox.ByteProvider as DynamicByteProvider).Bytes.ToArray();
        }

        private void Tokens_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BytecodeSingularToken selectedToken = Tokens_ListBox.SelectedItem as BytecodeSingularToken;
            if (selectedToken != null)
            {
                TokenChanging = true;
                ScriptEditor_Hexbox.SelectionStart = selectedToken.startPos;
                ScriptEditor_Hexbox.SelectionLength = 1;
                TokenChanging = false;
            }
        }


        public class ScriptHeaderItem
        {
            public string id { get; set; }
            public string value { get; set; }
            public int offset { get; set; }
            public int length { get; set; }
            public ScriptHeaderItem(string id, int value, int offset, IExportEntry callingEntry = null)
            {
                this.id = id;
                this.value = $"{value}";
                if (callingEntry != null)
                {
                    this.value += $" ({ callingEntry.FileRef.getEntry(value).GetFullPath})";
                }
                else
                {
                    this.value += $" ({ value:X8})";
                }
                this.offset = offset;
                length = 4;
            }
            public ScriptHeaderItem(string id, string value, int offset)
            {
                this.id = id;
                this.value = value;
                this.offset = offset;
                length = 4;
            }
            public ScriptHeaderItem(string id, short value, int offset)
            {
                this.id = id;
                this.value = $"{value} ({value.ToString("X4")})";
                this.offset = offset;
                length = 2;
            }
        }

        private void Function_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Token token = Function_ListBox.SelectedItem as Token;
            ScriptEditor_Hexbox.UnhighlightAll();
            if (token != null)
            {
                ScriptEditor_Hexbox.Highlight(token.pos, token.raw.Length);
            }
        }

        private void FunctionHeader_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ScriptHeaderItem token = Function_Header.SelectedItem as ScriptHeaderItem;
            ScriptEditor_Hexbox.UnhighlightAll();
            if (token != null && !TokenChanging)
            {
                TokenChanging = true;
                ScriptEditor_Hexbox.SelectionStart = token.offset;
                ScriptEditor_Hexbox.SelectionLength = token.length;
                TokenChanging = false;
            }
        }

        private void FunctionFooter_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ScriptHeaderItem token = Function_Footer.SelectedItem as ScriptHeaderItem;
            ScriptEditor_Hexbox.UnhighlightAll();
            if (token != null && !TokenChanging)
            {
                TokenChanging = true;
                ScriptEditor_Hexbox.SelectionStart = token.offset;
                ScriptEditor_Hexbox.SelectionLength = token.length;
                TokenChanging = false;
            }
        }

        public override void Dispose()
        {
            ScriptEditor_Hexbox = null;
            ScriptEditor_Hexbox_Host.Child.Dispose();
            ScriptEditor_Hexbox_Host.Dispose();
        }
    }
}
