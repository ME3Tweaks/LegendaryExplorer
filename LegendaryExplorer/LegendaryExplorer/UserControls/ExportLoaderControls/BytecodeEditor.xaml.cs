using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Be.Windows.Forms;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using static LegendaryExplorerCore.Unreal.UnrealFlags;
using Token = LegendaryExplorerCore.Unreal.Token;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for BytecodeEditor.xaml
    /// </summary>
    public partial class BytecodeEditor : ExportLoaderControl
    {
        private HexBox ScriptEditor_Hexbox;
        private bool ControlLoaded;

        public ObservableCollectionExtended<BytecodeSingularToken> TokenList { get; } = new();
        public ObservableCollectionExtended<object> DecompiledScriptBlocks { get; } = new();
        public ObservableCollectionExtended<ScriptHeaderItem> ScriptHeaderBlocks { get; } = new();
        public ObservableCollectionExtended<ScriptHeaderItem> ScriptFooterBlocks { get; } = new();

        public bool SubstituteImageForHexBox
        {
            get => (bool)GetValue(SubstituteImageForHexBoxProperty);
            set => SetValue(SubstituteImageForHexBoxProperty, value);
        }
        public static readonly DependencyProperty SubstituteImageForHexBoxProperty = DependencyProperty.Register(
            nameof(SubstituteImageForHexBox), typeof(bool), typeof(BytecodeEditor), new PropertyMetadata(false, SubstituteImageForHexBoxChangedCallback));

        private static void SubstituteImageForHexBoxChangedCallback(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            BytecodeEditor i = (BytecodeEditor)obj;
            if (e.NewValue is true && i.ScriptEditor_Hexbox_Host.Child.Height > 0 && i.ScriptEditor_Hexbox_Host.Child.Width > 0)
            {
                i.hexboxImageSub.Source = i.ScriptEditor_Hexbox_Host.Child.DrawToBitmapSource();
                i.hexboxImageSub.Width = i.ScriptEditor_Hexbox_Host.ActualWidth;
                i.hexboxImageSub.Height = i.ScriptEditor_Hexbox_Host.ActualHeight;
                i.hexboxImageSub.Visibility = Visibility.Visible;
                i.ScriptEditor_Hexbox_Host.Visibility = Visibility.Collapsed;
            }
            else
            {
                i.ScriptEditor_Hexbox_Host.Visibility = Visibility.Visible;
                i.hexboxImageSub.Visibility = Visibility.Collapsed;
            }
        }

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
            LoadCommands();
            InitializeComponent();
        }

        public RelayCommand NopOutStatementCommand { get; set; }
        private void LoadCommands()
        {
            NopOutStatementCommand = new RelayCommand(NopOutStatement, CanNopOutStatement);
        }

        private void NopOutStatement(object obj)
        {
            if (obj is Statement st)
            {
                var objBin = (UStruct)ObjectBinary.From(CurrentLoadedExport);
                for (int i = st.StartOffset; i < st.EndOffset; i++)
                {
                    objBin.ScriptBytes[i] = 0x0B; // OP_Nothing
                }
                CurrentLoadedExport.WriteBinary(objBin);
            }
        }

        private bool CanNopOutStatement(object obj)
        {
            return obj is Statement && CurrentLoadedExport is {Game: < MEGame.ME3}; // We only support nop on ME1/ME2 cause they don't use memory jumps. memory jumps complicate things
        }

        public override bool CanParse(ExportEntry exportEntry)
        {
            return exportEntry.ClassName is "Function" or "State" || exportEntry.IsClass && exportEntry.GetBinaryData<UClass>().ScriptStorageSize > 0;
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            BytecodeStart = 0;
            CurrentLoadedExport = exportEntry;
            ScriptEditor_Hexbox.ByteProvider = new ReadOptimizedByteProvider(CurrentLoadedExport.Data);
            ScriptEditor_Hexbox.ByteProvider.Changed += ByteProviderBytesChanged;
            try
            {
                StartFunctionScan(CurrentLoadedExport.Data);
            }
            catch (Exception e)
            {
                DecompiledScriptBlocks.ClearEx();
                DecompiledScriptBlocks.Add("Decompilation Error!");
                DecompiledScriptBlocks.Add(e.FlattenException());
            }
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                var elhw = new ExportLoaderHostedWindow(new BytecodeEditor(), CurrentLoadedExport)
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
            MEGame game = Pcc.Game;
            if (game is MEGame.ME3 or MEGame.LE1 or  MEGame.LE2 or MEGame.LE3 or MEGame.UDK || Pcc.Platform == MEPackage.GamePlatform.PS3)
            {
                var func = new Function(data, CurrentLoadedExport);
                func.ParseFunction();
                DecompiledScriptBlocks.Add(func.GetSignature());
                DecompiledScriptBlocks.AddRange(func.ScriptBlocks);
                TokenList.AddRange(func.SingularTokenList);

                int diskSize;
                int pos = CurrentLoadedExport.IsClass ? 4 : 0xC;
                if (game is MEGame.UDK)
                {

                    var nextItemCompilingChain = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                    ScriptHeaderBlocks.Add(new ScriptHeaderItem("Next item in loading chain", nextItemCompilingChain, pos, nextItemCompilingChain > 0 ? CurrentLoadedExport : null));

                    pos += 4;
                    var functionSuperclass = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                    ScriptHeaderBlocks.Add(new ScriptHeaderItem($"{CurrentLoadedExport.ClassName} superclass", functionSuperclass, pos, functionSuperclass != 0 ? CurrentLoadedExport.FileRef.GetEntry(functionSuperclass) : null));
                    pos += 4;//skip script text
                    pos += 4;
                    nextItemCompilingChain = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                    ScriptHeaderBlocks.Add(new ScriptHeaderItem("Children Probe Start", nextItemCompilingChain, pos, nextItemCompilingChain > 0 ? CurrentLoadedExport : null));
                    pos += 12; //skip c++ text, line number and position
                    pos += 4;
                    ScriptHeaderBlocks.Add(new ScriptHeaderItem("Size in Memory", EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian), pos));

                    pos += 4; 
                    diskSize = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                    ScriptHeaderBlocks.Add(new ScriptHeaderItem("Size on disk", diskSize, pos));
                }
                else
                {

                    var functionSuperclass = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                    ScriptHeaderBlocks.Add(new ScriptHeaderItem($"{CurrentLoadedExport.ClassName} superclass", functionSuperclass, pos, functionSuperclass != 0 ? CurrentLoadedExport.FileRef.GetEntry(functionSuperclass) : null));

                    pos += 4;
                    var nextItemCompilingChain = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                    ScriptHeaderBlocks.Add(new ScriptHeaderItem("Next item in loading chain", nextItemCompilingChain, pos, nextItemCompilingChain > 0 ? CurrentLoadedExport : null));

                    pos += 4;
                    nextItemCompilingChain = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                    ScriptHeaderBlocks.Add(new ScriptHeaderItem("Children Probe Start", nextItemCompilingChain, pos, nextItemCompilingChain > 0 ? CurrentLoadedExport : null));

                    pos += 4;
                    ScriptHeaderBlocks.Add(new ScriptHeaderItem("Size in Memory", EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian), pos));

                    pos += 4;
                    diskSize = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                    ScriptHeaderBlocks.Add(new ScriptHeaderItem("Size on disk", diskSize, pos));
                }



                List<int> objRefPositions = func.ScriptBlocks.SelectMany(tok => tok.inPackageReferences)
                                                .Where(tup => tup.type == Token.INPACKAGEREFTYPE_ENTRY)
                                                .Select(tup => tup.position).ToList();
                int calculatedLength = diskSize + 4 * objRefPositions.Count;
                DiskToMemPosMap = func.DiskToMemPosMap;


                DecompiledScriptBoxTitle = $"Decompiled Script (calculated memory size: {calculatedLength} 0x{calculatedLength:X})";


                if (CurrentLoadedExport.ClassName == "Function")
                {
                    pos += 4 + diskSize;
                    ScriptFooterBlocks.Add(new ScriptHeaderItem("Native Index", EndianReader.ToInt16(data, pos, CurrentLoadedExport.FileRef.Endian), pos) { length = 2 });
                    pos += 2;

                    if (CurrentLoadedExport.Game is MEGame.LE1 or MEGame.LE2 or MEGame.UDK)
                    {
                        ScriptFooterBlocks.Add(new ScriptHeaderItem("Operator Precedence", data[pos], pos));
                        pos++;
                    }

                    ScriptFooterBlocks.Add(new ScriptHeaderItem("Flags", $"0x{EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian):X8} {func.GetFlags()[6..]}", pos));

                    if (CurrentLoadedExport.Game is MEGame.LE1 or MEGame.LE2 or MEGame.UDK)
                    {
                        pos += 4;
                        ScriptFooterBlocks.Add(new ScriptHeaderItem("FriendlyName", CurrentLoadedExport.FileRef.GetNameEntry(EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian)), pos));
                    }
                }
                else
                {
                    //State
                    //parse remaining
                    var footerstartpos = pos + diskSize + 4;
                    var footerdata = CurrentLoadedExport.DataReadOnly.Slice(footerstartpos, CurrentLoadedExport.DataSize - footerstartpos);
                    var fpos = 0;
                    int probeMaskLength = game is MEGame.UDK ? 4 : 8;
                    ScriptFooterBlocks.Add(new ScriptHeaderItem("ProbeMask", "??", fpos + footerstartpos) { length = probeMaskLength });
                    fpos += probeMaskLength;

                    if (game is not MEGame.UDK)
                    {
                        ScriptFooterBlocks.Add(new ScriptHeaderItem("IgnoreMask", "??", fpos + footerstartpos) { length = 8 });
                        fpos += 0x8;
                    }

                    ScriptFooterBlocks.Add(new ScriptHeaderItem("Label Table Offset", EndianReader.ToInt16(footerdata, fpos, Pcc.Endian), fpos + footerstartpos) { length = 2 });
                    fpos += 0x2;


                    var stateFlagsBytes = footerdata.Slice(fpos, 0x4);
                    var stateFlags = (EStateFlags)EndianReader.ToInt32(stateFlagsBytes, 0, CurrentLoadedExport.FileRef.Endian);
                    var names = stateFlags.ToString().Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    ScriptFooterBlocks.Add(new ScriptHeaderItem("State flags", string.Join(" ", names), fpos + footerstartpos));
                    fpos += 0x4;



                    //if ((stateFlags & EStateFlags.Simulated) != 0)
                    //{
                    //    //Replication offset? Like in Function?
                    //    ScriptFooterBlocks.Add(new ScriptHeaderItem("RepOffset? ", EndianReader.ToInt16(footerdata, fpos, CurrentLoadedExport.FileRef.Endian), fpos));
                    //    fpos += 0x2;
                    //}

                    var numMappedFunctions = EndianReader.ToInt32(footerdata, fpos, CurrentLoadedExport.FileRef.Endian);
                    ScriptFooterBlocks.Add(new ScriptHeaderItem("Num of mapped functions", numMappedFunctions.ToString(), fpos + footerstartpos));
                    fpos += 4;
                    for (int i = 0; i < numMappedFunctions; i++)
                    {
                        var name = EndianReader.ToInt32(footerdata, fpos, CurrentLoadedExport.FileRef.Endian);
                        var uindex = EndianReader.ToInt32(footerdata, fpos + 8, CurrentLoadedExport.FileRef.Endian);
                        var funcMap = new ScriptHeaderItem($"FunctionMap[{i}]:", 
                                                           $"{CurrentLoadedExport.FileRef.GetNameEntry(name)} => {CurrentLoadedExport.FileRef.GetEntry(uindex)?.FullPath}()",
                                                           fpos + footerstartpos);
                        ScriptFooterBlocks.Add(funcMap);
                        fpos += 12;
                    }
                }
            }
            else if (game is MEGame.ME1 or MEGame.ME2)
            {
                //Header
                int pos = CurrentLoadedExport.IsClass ? 4 : 12;
                var functionSuperclass = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                ScriptHeaderBlocks.Add(new ScriptHeaderItem($"{CurrentLoadedExport.ClassName} superclass", functionSuperclass, pos, functionSuperclass != 0 ? CurrentLoadedExport.FileRef.GetEntry(functionSuperclass) : null));
                pos += 4;

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
                var func = CurrentLoadedExport.ClassName == "Function" ? UE3FunctionReader.ReadFunction(CurrentLoadedExport, data) : UE3FunctionReader.ReadState(CurrentLoadedExport, data);
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
                    pos = data.Length - (func.HasFlag("Net") && game.IsOTGame() ? 17 : 15);
                    string flagStr = func.GetFlags();
                    ScriptFooterBlocks.Add(new ScriptHeaderItem("Native Index",
                        EndianReader.ToInt16(data, pos, CurrentLoadedExport.FileRef.Endian), pos));
                    pos += 2;

                    ScriptFooterBlocks.Add(new ScriptHeaderItem("Operator Precedence", data[pos], pos));
                    pos++;

                    int functionFlags = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                    ScriptFooterBlocks.Add(new ScriptHeaderItem("Flags", $"0x{functionFlags:X8} {flagStr}", pos));
                    pos += 4;

                    if (game.IsOTGame() && func.HasFlag("Net"))
                    {
                        ScriptFooterBlocks.Add(new ScriptHeaderItem("ReplicationOffset", EndianReader.ToInt16(data, pos, CurrentLoadedExport.FileRef.Endian), pos));
                        pos += 2;
                    }

                    int friendlyNameIndex = EndianReader.ToInt32(data, pos, CurrentLoadedExport.FileRef.Endian);
                    ScriptFooterBlocks.Add(
                        new ScriptHeaderItem("Friendly Name", Pcc.GetNameEntry(friendlyNameIndex), pos) { length = 8 });
                    pos += 8;
                }
                else if (CurrentLoadedExport.ClassName == "State")
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
                    var currentData = ((ReadOptimizedByteProvider)ScriptEditor_Hexbox.ByteProvider).Span;
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
                        if (Pcc.Game >= MEGame.ME3 && diskPos >= 0 && diskPos < DiskToMemPosMap.Length)
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
                int bytecodeStart = Pcc.Game is MEGame.ME1 or MEGame.ME2 ? 0x2C : 0x20;
                if (CurrentLoadedExport.IsClass)
                {
                    bytecodeStart -= 8;
                }
                if (start >= bytecodeStart && start < CurrentLoadedExport.DataSize - 6)
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
                else if (start >= 0x0C && start < bytecodeStart)
                {
                    //header
                    int index = (start - 0xC) / 4;
                    Function_Header.SelectedIndex = index;
                    selectedBox = Function_Header;

                }
                else if (start > CurrentLoadedExport.DataSize - 6)
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
            byte[] newBytes = ((ReadOptimizedByteProvider)ScriptEditor_Hexbox.ByteProvider).Span.ToArray();
            if (CurrentLoadedExport.Game >= MEGame.ME3)
            {
                int sizeDiff = newBytes.Length - CurrentLoadedExport.DataSize;
                int offset = 0x1C;
                if (CurrentLoadedExport.IsClass)
                {
                    offset -= 8;
                }
                int diskSize = EndianReader.ToInt32(CurrentLoadedExport.DataReadOnly, offset, CurrentLoadedExport.FileRef.Endian);
                diskSize += sizeDiff;
                newBytes.OverwriteRange(offset, EndianBitConverter.GetBytes(diskSize, Pcc.Endian));
            }
            StartFunctionScan(newBytes);
        }

        private void ScriptEditor_SaveHexChanges_Click(object sender, RoutedEventArgs e)
        {
            ((ReadOptimizedByteProvider)ScriptEditor_Hexbox.ByteProvider).ApplyChanges();
            byte[] newBytes = ((ReadOptimizedByteProvider)ScriptEditor_Hexbox.ByteProvider).Span.ToArray();
            if (CurrentLoadedExport.Game >= MEGame.ME3)
            {
                int sizeDiff = newBytes.Length - CurrentLoadedExport.DataSize;
                int offset = 0x1C;
                if (CurrentLoadedExport.IsClass)
                {
                    offset -= 8;
                }
                int diskSize = EndianReader.ToInt32(CurrentLoadedExport.DataReadOnly, offset, CurrentLoadedExport.FileRef.Endian);
                diskSize += sizeDiff;
                newBytes.OverwriteRange(offset, EndianBitConverter.GetBytes(diskSize, Pcc.Endian));
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
