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
using Gammtek.Conduit.IO;
using ME3Explorer.ME1.Unreal.UnhoodBytecode;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using static ME3Explorer.ME1.Unreal.UnhoodBytecode.BytecodeReader;
using static ME3Explorer.Unreal.Bytecode;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for UnrealScriptWPF.xaml
    /// </summary>
    public partial class UnrealScriptWPF : ExportLoaderControl
    {
        private HexBox ScriptEditor_Hexbox;
        private bool ControlLoaded;

        public ObservableCollectionExtended<BytecodeSingularToken> TokenList { get; private set; } = new ObservableCollectionExtended<BytecodeSingularToken>();
        public ObservableCollectionExtended<object> DecompiledScriptBlocks { get; private set; } = new ObservableCollectionExtended<object>();
        public ObservableCollectionExtended<ScriptHeaderItem> ScriptHeaderBlocks { get; private set; } = new ObservableCollectionExtended<ScriptHeaderItem>();
        public ObservableCollectionExtended<ScriptHeaderItem> ScriptFooterBlocks { get; private set; } = new ObservableCollectionExtended<ScriptHeaderItem>();

        private bool TokenChanging = false;
        private int BytecodeStart;

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

        public bool HexBoxSelectionChanging { get; private set; }

        public UnrealScriptWPF()
        {
            InitializeComponent();
        }

        public override bool CanParse(ExportEntry exportEntry)
        {
            return ((exportEntry.ClassName == "Function" || exportEntry.ClassName == "State") && exportEntry.FileRef.Game != MEGame.UDK);
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            BytecodeStart = 0;
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

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new UnrealScriptWPF(), CurrentLoadedExport);
                elhw.Title = $"UnrealScript Editor - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}";
                elhw.Show();
            }
        }

        private void StartFunctionScan(byte[] data)
        {
            DecompiledScriptBlocks.ClearEx();
            TokenList.ClearEx();
            ScriptHeaderBlocks.ClearEx();
            ScriptFooterBlocks.ClearEx();
            if (CurrentLoadedExport.FileRef.Game == MEGame.ME3)
            {
                var func = new ME3Explorer.Unreal.Classes.Function(data, CurrentLoadedExport, CurrentLoadedExport.ClassName == "State" ? Convert.ToInt32(StartOffset_Changer.Text) : 32);


                func.ParseFunction();
                DecompiledScriptBlocks.Add(func.GetSignature());
                DecompiledScriptBlocks.AddRange(func.ScriptBlocks);
                TokenList.AddRange(func.SingularTokenList);

                int pos = 12;

                var functionSuperclass = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Function superclass", functionSuperclass, pos, functionSuperclass != 0 ? CurrentLoadedExport.FileRef.GetEntry(functionSuperclass) : null));

                pos += 4;
                var nextItemCompilingChain = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Next item in loading chain", nextItemCompilingChain, pos, nextItemCompilingChain > 0 ? CurrentLoadedExport : null));

                pos += 4;
                nextItemCompilingChain = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Children Probe Start", nextItemCompilingChain, pos, nextItemCompilingChain > 0 ? CurrentLoadedExport : null));

                pos += 4;
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Unknown 2 (Memory size?)", EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian), pos));

                pos += 4;
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Size", EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian), pos));

                pos = data.Length - 6;
                string flagStr = func.GetFlags();
                ScriptFooterBlocks.Add(new ScriptHeaderItem("Native Index", EndianReader.ToInt16(data, pos, CurrentLoadedExport.FileRef.Endian), pos));
                pos += 2;
                ScriptFooterBlocks.Add(new ScriptHeaderItem("Flags", $"0x{EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian).ToString("X8")} {func.GetFlags().Substring(6)}", pos));
            }
            else if (CurrentLoadedExport.FileRef.Game == MEGame.ME1 || CurrentLoadedExport.Game == MEGame.ME2)
            {
                //Header
                int pos = 16;

                var nextItemCompilingChain = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Next item in loading chain", nextItemCompilingChain, pos, nextItemCompilingChain > 0 ? CurrentLoadedExport : null));

                pos += 8;
                nextItemCompilingChain = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Children Probe Start", nextItemCompilingChain, pos, nextItemCompilingChain > 0 ? CurrentLoadedExport : null));

                pos += 4;
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Unknown 1 (??)", EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian), pos));

                pos += 4;
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Unknown 2 (Line??)", EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian), pos));

                pos += 4;
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Unknown 3 (TextPos??)", EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian), pos));

                pos += 4;
                int scriptSize = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Script Size", scriptSize, pos));
                pos += 4;
                BytecodeStart = pos;
                var func = UE3FunctionReader.ReadFunction(CurrentLoadedExport, data);
                func.Decompile(new TextBuilder(), false); //parse bytecode

                bool defined = func.HasFlag("Defined");
                if (defined)
                {
                    DecompiledScriptBlocks.Add(func.FunctionSignature + " {");
                }
                else
                {
                    DecompiledScriptBlocks.Add(func.FunctionSignature);
                }
                for (int i = 0; i < func.Statements.statements.Count; i++)
                {
                    Statement s = func.Statements.statements[i];
                    s.SetPaddingForScriptSize(scriptSize);
                    DecompiledScriptBlocks.Add(s);
                    if (s.Reader != null && i == 0)
                    {
                        //Add tokens read from statement. All of them point to the same reader, so just do only the first one.
                        TokenList.AddRange(s.Reader.ReadTokens.Select(x => x.ToBytecodeSingularToken(pos)).OrderBy(x => x.StartPos));
                    }
                }

                if (defined)
                {
                    DecompiledScriptBlocks.Add("}");
                }

                //Footer
                pos = data.Length - (func.HasFlag("Net") ? 17 : 15);
                string flagStr = func.GetFlags();
                ScriptFooterBlocks.Add(new ScriptHeaderItem("Native Index", EndianReader.ToInt16(data, pos, CurrentLoadedExport.FileRef.Endian), pos));
                pos += 2;

                ScriptFooterBlocks.Add(new ScriptHeaderItem("Operator Precedence", data[pos], pos));
                pos++;

                int functionFlags = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                ScriptFooterBlocks.Add(new ScriptHeaderItem("Flags", $"0x{functionFlags.ToString("X8")} {flagStr}", pos));
                pos += 4;

                //if ((functionFlags & func._flagSet.GetMask("Net")) != 0)
                //{
                //ScriptFooterBlocks.Add(new ScriptHeaderItem("Unknown 1 (RepOffset?)", EndianReader.ToInt16(data, pos, CurrentLoadedExport.FileRef.Endian), pos));
                //pos += 2;
                //}

                int friendlyNameIndex = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                ScriptFooterBlocks.Add(new ScriptHeaderItem("Friendly Name Table Index", $"0x{friendlyNameIndex.ToString("X8")} {CurrentLoadedExport.FileRef.GetNameEntry(friendlyNameIndex)}", pos));
                pos += 4;

                ScriptFooterBlocks.Add(new ScriptHeaderItem("Unknown 2", $"0x{EndianReader.ToInt16(data, pos, CurrentLoadedExport.FileRef.Endian).ToString("X4")}", pos));
                pos += 4;

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

        private static string IndentString(string stringToIndent)
        {
            string[] lines = stringToIndent.Split(
                new[] { Environment.NewLine },
                StringSplitOptions.None
            );
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                lines[i] = "    " + line; //textbuilder use 4 spaces
            }
            return string.Join("\n", lines);
        }

        public override void UnloadExport()
        {
            CurrentLoadedExport = null;
        }

        private void hb1_SelectionChanged(object sender, EventArgs e)
        {
            if (!TokenChanging)
            {
                HexBoxSelectionChanging = true;
                int start = (int)ScriptEditor_Hexbox.SelectionStart;
                int len = (int)ScriptEditor_Hexbox.SelectionLength;
                int size = (int)ScriptEditor_Hexbox.ByteProvider.Length;
                try
                {
                    //TODO: Optimize this so this is only called when data has changed
                    byte[] currentData = ((DynamicByteProvider)ScriptEditor_Hexbox.ByteProvider).Bytes.ToArray();
                    if (start != -1 && start < size)
                    {
                        string s = $"Byte: {currentData[start]}"; //if selection is same as size this will crash.
                        if (start <= currentData.Length - 4)
                        {
                            int val = EndianReader.ToInt32(currentData, start, CurrentLoadedExport.FileRef.Endian);
                            s += $", Int: {val}";
                            s += $", Float: {EndianReader.ToSingle(currentData, start, CurrentLoadedExport.FileRef.Endian)}";
                            if (CurrentLoadedExport.FileRef.IsName(val))
                            {
                                s += $", Name: {CurrentLoadedExport.FileRef.GetNameEntry(val)}";
                            }
                            if (CurrentLoadedExport.FileRef.Game == MEGame.ME1)
                            {
                                ME1OpCodes m = (ME1OpCodes)currentData[start];
                                s += $", OpCode: {m}";
                            }

                            if (CurrentLoadedExport.FileRef.GetEntry(val) is IEntry ent)
                            {
                                string type = ent is ExportEntry ? "Export" : "Import";
                                if (ent.ObjectName == CurrentLoadedExport.ObjectName)
                                {
                                    s += $", {type}: {ent.InstancedFullPath}";
                                }
                                else
                                {
                                    s += $", {type}: {ent.ObjectName.Instanced}";
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
                        Function_ListBox.ScrollIntoView(token);
                    }

                    BytecodeSingularToken bst = TokenList.FirstOrDefault(x => x.StartPos == start);
                    if (bst != null)
                    {
                        Tokens_ListBox.SelectedItem = bst;
                        Tokens_ListBox.ScrollIntoView(bst);
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
                HexBoxSelectionChanging = false;
            }
        }

        private void UnrealScriptWPF_Loaded(object sender, RoutedEventArgs e)
        {
            ScriptEditor_Hexbox = (HexBox)ScriptEditor_Hexbox_Host.Child;
            ControlLoaded = true;
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
            if (selectedToken != null && !HexBoxSelectionChanging)
            {
                TokenChanging = true;
                ScriptEditor_Hexbox.SelectionStart = selectedToken.StartPos;
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
            public ScriptHeaderItem(string id, int value, int offset, IEntry callingEntry = null)
            {
                this.id = id;
                this.value = $"{value}";
                if (callingEntry != null)
                {
                    this.value += $" ({ callingEntry.FileRef.GetEntry(value).FullPath})";
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

            public ScriptHeaderItem(string id, byte value, int offset)
            {
                this.id = id;
                this.value = value.ToString();
                this.offset = offset;
                length = 1;
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
            Statement me1statement = Function_ListBox.SelectedItem as Statement;
            ScriptEditor_Hexbox.UnhighlightAll();
            if (token != null)
            {
                ScriptEditor_Hexbox.Highlight(token.pos, token.raw.Length);
            }

            if (me1statement != null)
            {
                //todo: figure out how length could be calculated
                ScriptEditor_Hexbox.Highlight(me1statement.StartOffset + BytecodeStart, me1statement.EndOffset - me1statement.StartOffset);
            }
        }

        private void FunctionHeader_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ScriptHeaderItem token = Function_Header.SelectedItem as ScriptHeaderItem;
            ScriptEditor_Hexbox.UnhighlightAll();
            if (token != null && !TokenChanging && !HexBoxSelectionChanging)
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
            if (token != null && !TokenChanging && !HexBoxSelectionChanging)
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

        private void StartOffset_Changed(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (ControlLoaded)
            {
                ScriptEditor_PreviewScript_Click(null, null);
            }
        }
    }
}
