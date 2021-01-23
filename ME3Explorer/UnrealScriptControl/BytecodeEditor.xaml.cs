using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Be.Windows.Forms;
using ME3Explorer.SharedUI;
using ME3ExplorerCore.Gammtek.IO;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.ME1.Unreal.UnhoodBytecode;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;
using ME3ExplorerCore.Unreal.Classes;
using Token = ME3ExplorerCore.Unreal.Token;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for BytecodeEditor.xaml
    /// </summary>
    public partial class BytecodeEditor : ExportLoaderControl
    {
        private HexBox ScriptEditor_Hexbox;
        private bool ControlLoaded;

        public ObservableCollectionExtended<BytecodeSingularToken> TokenList { get; private set; } = new ObservableCollectionExtended<BytecodeSingularToken>();
        public ObservableCollectionExtended<object> DecompiledScriptBlocks { get; private set; } = new ObservableCollectionExtended<object>();
        public ObservableCollectionExtended<ScriptHeaderItem> ScriptHeaderBlocks { get; private set; } = new ObservableCollectionExtended<ScriptHeaderItem>();
        public ObservableCollectionExtended<ScriptHeaderItem> ScriptFooterBlocks { get; private set; } = new ObservableCollectionExtended<ScriptHeaderItem>();

        private bool TokenChanging = false;
        private int BytecodeStart;

        private bool _bytesHaveChanged;
        public bool BytesHaveChanged
        {
            get => _bytesHaveChanged;
            set => SetProperty(ref _bytesHaveChanged, value);
        }

        private string _decompiledScriptBoxTitle = "Tokens";

        public string DecompiledScriptBoxTitle
        {
            get => _decompiledScriptBoxTitle;
            set => SetProperty(ref _decompiledScriptBoxTitle, value);
        }

        private int[] DiskToMemPosMap = new int[0];

        public bool HexBoxSelectionChanging { get; private set; }

        public BytecodeEditor() : base("BytecodeEditor")
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
            StartFunctionScan(CurrentLoadedExport.Data);
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new BytecodeEditor(), CurrentLoadedExport)
                {
                    Title = $"Bytecode Editor - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
                elhw.Show();
            }
        }

        private void StartFunctionScan(byte[] data)
        {
            DecompiledScriptBlocks.ClearEx();
            TokenList.ClearEx();
            ScriptHeaderBlocks.ClearEx();
            ScriptFooterBlocks.ClearEx();
            DecompiledScriptBoxTitle = "Decompiled Script";
            if (Pcc.Game == MEGame.ME3 || Pcc.Platform == MEPackage.GamePlatform.PS3)
            {
                var func = new Function(data, CurrentLoadedExport, 32);


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
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Size in Memory", EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian), pos));

                pos += 4;
                int diskSize = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Size on disk", diskSize, pos));



                List<int> objRefPositions = func.ScriptBlocks.SelectMany(tok => tok.inPackageReferences)
                                                .Where(tup => tup.type == Token.INPACKAGEREFTYPE_ENTRY)
                                                .Select(tup => tup.position).ToList();
                int calculatedLength = diskSize + 4 * objRefPositions.Count;
                DiskToMemPosMap = func.DiskToMemPosMap;
                //DiskToMemPosMap = new int[diskSize];
                //int iDisk = 0;
                //int iMem = 0;
                //foreach (int objRefPosition in objRefPositions)
                //{
                //    while (iDisk < objRefPosition + 4)
                //    {
                //        DiskToMemPosMap[iDisk] = iMem;
                //        iDisk++;
                //        iMem++;
                //    }
                //    iMem += 4;
                //}
                //while (iDisk < diskSize)
                //{
                //    DiskToMemPosMap[iDisk] = iMem;
                //    iDisk++;
                //    iMem++;
                //}

                //foreach (Token t in DecompiledScriptBlocks.OfType<Token>())
                //{
                //    var diskPos = t.pos - 32;
                //    if (diskPos >= 0 && diskPos < DiskToMemPosMap.Length)
                //    {
                //        t.memPos = DiskToMemPosMap[diskPos];
                //    }
                //}


                DecompiledScriptBoxTitle = $"Decompiled Script (calculated memory size: {calculatedLength} 0x{calculatedLength:X})";


                if (CurrentLoadedExport.ClassName == "Function")
                {
                    var nativeBackOffset = CurrentLoadedExport.FileRef.Game < MEGame.ME3 ? 7 : 6;
                    pos = data.Length - nativeBackOffset;
                    string flagStr = func.GetFlags();
                    ScriptFooterBlocks.Add(new ScriptHeaderItem("Native Index", EndianReader.ToInt16(data, pos, CurrentLoadedExport.FileRef.Endian), pos) { length = 2 });
                    pos += 2;
                    ScriptFooterBlocks.Add(new ScriptHeaderItem("Flags", $"0x{EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian):X8} {func.GetFlags().Substring(6)}", pos));
                }
                else
                {
                    //State
                    //parse remaining
                    var footerstartpos = 0x20 + diskSize;
                    var footerdata = CurrentLoadedExport.Data.Slice(0x20 + diskSize, (int)CurrentLoadedExport.Data.Length - (0x20 + diskSize));
                    var fpos = 0;
                    ScriptFooterBlocks.Add(new ScriptHeaderItem("Probemask?", "??", fpos + footerstartpos) { length = 8 });
                    fpos += 0x8;

                    ScriptFooterBlocks.Add(new ScriptHeaderItem("Unknown 8 FF's", "??", fpos + footerstartpos) { length = 8 });
                    fpos += 0x8;

                    ScriptFooterBlocks.Add(new ScriptHeaderItem("Label Table Offset", "??", fpos + footerstartpos) { length = 2 });
                    fpos += 0x2;


                    var stateFlagsBytes = footerdata.Slice(fpos, 0x4);
                    var stateFlags = (StateFlags)EndianReader.ToInt32(stateFlagsBytes, 0, CurrentLoadedExport.FileRef.Endian);
                    var names = stateFlags.ToString().Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    ScriptFooterBlocks.Add(new ScriptHeaderItem("State flags", string.Join(" ", names), fpos + footerstartpos));
                    fpos += 0x4;



                    //if ((stateFlags & StateFlags.Simulated) != 0)
                    //{
                    //    //Replication offset? Like in Function?
                    //    ScriptFooterBlocks.Add(new ScriptHeaderItem("RepOffset? ", EndianReader.ToInt16(footerdata, fpos, CurrentLoadedExport.FileRef.Endian), fpos));
                    //    fpos += 0x2;
                    //}

                    var numMappedFunctions = EndianReader.ToInt32(footerdata, fpos, CurrentLoadedExport.FileRef.Endian);
                    ScriptFooterBlocks.Add(new ScriptHeaderItem("Num of mapped functions", numMappedFunctions.ToString(), fpos));
                    fpos += 4;
                    for (int i = 0; i < numMappedFunctions; i++)
                    {
                        var name = EndianReader.ToInt32(footerdata, fpos, CurrentLoadedExport.FileRef.Endian);
                        var uindex = EndianReader.ToInt32(footerdata, fpos + 8, CurrentLoadedExport.FileRef.Endian);
                        var funcMap = new ScriptHeaderItem($"FunctionMap[{i}]:", $"{CurrentLoadedExport.FileRef.GetNameEntry(name)} => {CurrentLoadedExport.FileRef.GetEntry(uindex)?.FullPath}()", fpos);
                        ScriptFooterBlocks.Add(funcMap);
                        fpos += 12;
                    }
                }
            }
            else if (Pcc.Game == MEGame.ME1 || Pcc.Game == MEGame.ME2)
            {
                //Header
                int pos = 16;

                var nextItemCompilingChain = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Next item in loading chain", nextItemCompilingChain, pos, nextItemCompilingChain > 0 ? CurrentLoadedExport : null));

                pos += 8;
                nextItemCompilingChain = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Children Probe Start", nextItemCompilingChain, pos, nextItemCompilingChain > 0 ? CurrentLoadedExport : null));

                pos += 8;
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Line", EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian), pos));

                pos += 4;
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("TextPos", EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian), pos));

                pos += 4;
                int scriptSize = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                ScriptHeaderBlocks.Add(new ScriptHeaderItem("Script Size", scriptSize, pos));
                pos += 4;
                BytecodeStart = pos;
                var func = CurrentLoadedExport.ClassName == "State" ? UE3FunctionReader.ReadState(CurrentLoadedExport, data) : UE3FunctionReader.ReadFunction(CurrentLoadedExport, data);
                func.Decompile(new TextBuilder(), false, true); //parse bytecode

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
                if (CurrentLoadedExport.ClassName == "Function")
                {
                    pos = data.Length - (func.HasFlag("Net") ? 17 : 15);
                    string flagStr = func.GetFlags();
                    ScriptFooterBlocks.Add(new ScriptHeaderItem("Native Index",
                        EndianReader.ToInt16(data, pos, CurrentLoadedExport.FileRef.Endian), pos));
                    pos += 2;

                    ScriptFooterBlocks.Add(new ScriptHeaderItem("Operator Precedence", data[pos], pos));
                    pos++;

                    int functionFlags = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                    ScriptFooterBlocks.Add(new ScriptHeaderItem("Flags", $"0x{functionFlags:X8} {flagStr}", pos));
                    pos += 4;

                    //if ((functionFlags & func._flagSet.GetMask("Net")) != 0)
                    //{
                    //ScriptFooterBlocks.Add(new ScriptHeaderItem("Unknown 1 (RepOffset?)", EndianReader.ToInt16(data, pos, CurrentLoadedExport.FileRef.Endian), pos));
                    //pos += 2;
                    //}

                    int friendlyNameIndex = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                    ScriptFooterBlocks.Add(
                        new ScriptHeaderItem("Friendly Name", Pcc.GetNameEntry(friendlyNameIndex), pos) { length = 8 });
                    pos += 8;
                } else if (CurrentLoadedExport.ClassName == "State")
                {
                    // There's labeltable offset but not very useful otherwise. Would probably use objectbinary to just get it.
                    // Maybe this will be overhauled someday
                    // Probably not
                }
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
                            if (Pcc.Platform != MEPackage.GamePlatform.PS3 && Pcc.Game <= MEGame.ME2)
                            {
                                BytecodeReader.ME1OpCodes m = (BytecodeReader.ME1OpCodes)currentData[start];
                                s += $", OpCode: {m}";
                            }
                            else if (Pcc.Platform == MEPackage.GamePlatform.PS3 || Pcc.Game == MEGame.ME2)
                            {
                                Bytecode.byteOpnameMap.TryGetValue(currentData[start], out var opcodeName);
                                s += $", OpCode: {opcodeName ?? currentData[start].ToString()}";
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

                        int diskPos = start - 32;
                        if (Pcc.Game == MEGame.ME3 && diskPos >= 0 && diskPos < DiskToMemPosMap.Length)
                        {
                            s += $" | MemoryPos=0x{DiskToMemPosMap[diskPos]:X4}";
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
                var allBoxesToUpdate = new List<ListBox>(new[] { Function_ListBox, Function_Header, Function_Footer });
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
            byte[] newBytes = (ScriptEditor_Hexbox.ByteProvider as DynamicByteProvider).Bytes.ToArray();
            if (CurrentLoadedExport.Game == MEGame.ME3)
            {
                int sizeDiff = newBytes.Length - CurrentLoadedExport.DataSize;
                int diskSize = BitConverter.ToInt32(CurrentLoadedExport.Data, 0x1C);
                diskSize += sizeDiff;
                newBytes.OverwriteRange(0x1C, BitConverter.GetBytes(diskSize));
            }
            StartFunctionScan(newBytes);
        }

        private void ScriptEditor_SaveHexChanges_Click(object sender, RoutedEventArgs e)
        {
            (ScriptEditor_Hexbox.ByteProvider as DynamicByteProvider).ApplyChanges();
            byte[] newBytes = (ScriptEditor_Hexbox.ByteProvider as DynamicByteProvider).Bytes.ToArray();
            if (CurrentLoadedExport.Game == MEGame.ME3)
            {
                int sizeDiff = newBytes.Length - CurrentLoadedExport.DataSize;
                int diskSize = BitConverter.ToInt32(CurrentLoadedExport.Data, 0x1C);
                diskSize += sizeDiff;
                newBytes.OverwriteRange(0x1C, BitConverter.GetBytes(diskSize));
            }
            CurrentLoadedExport.Data = newBytes;
        }

        private void Tokens_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Tokens_ListBox.SelectedItem is BytecodeSingularToken selectedToken && !HexBoxSelectionChanging)
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
                    this.value += $" (0x{value:X4})";
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
                this.value = $"{value} ({value:X4})";
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
