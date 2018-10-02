using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public ObservableCollectionExtended<Token> DecompiledScriptBlocks { get; private set; } = new ObservableCollectionExtended<Token>();

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
            return exportEntry.ClassName == "Function" && exportEntry.FileRef.Game == MEGame.ME3;
        }

        public override void LoadExport(IExportEntry exportEntry)
        {
            CurrentLoadedExport = exportEntry;
            ScriptEditor_Hexbox.ByteProvider = new DynamicByteProvider(CurrentLoadedExport.Data);
            ScriptEditor_Hexbox.ByteProvider.Changed += ByteProviderBytesChanged;
            StartFunctionScan(CurrentLoadedExport.Data);
        }

        private void StartFunctionScan(byte[] data)
        {
            if (CurrentLoadedExport.FileRef.Game == MEGame.ME3)
            {
                var func = new ME3Explorer.Unreal.Classes.Function(data, CurrentLoadedExport.FileRef);
                func.ParseFunction();
                DecompiledScriptBlocks.Clear();
                DecompiledScriptBlocks.AddRange(func.ScriptBlocks);

                TokenList.Clear();
                TokenList.AddRange(func.SingularTokenList);

                Function_Header.Text = func.HeaderText;
            }
            else if (CurrentLoadedExport.FileRef.Game == MEGame.ME1)
            {
                ME1Explorer.Unreal.Classes.Function func = new ME1Explorer.Unreal.Classes.Function(data, CurrentLoadedExport.FileRef as ME1Package);
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
            if (start >= 0x20)
            {

                //int index = -1;
                //foreach (Token x in DecompiledScriptBlocks)
                //{
                //    index++;
                //    if (start >= x.pos && start < (x.pos + x.raw.Length))
                //    {
                //        break;
                //    }
                //}
                //Tokens_ListBox.SelectedIndex = index;
                
                Token token = DecompiledScriptBlocks.FirstOrDefault(x => start >= x.pos && start < (x.pos + x.raw.Length));
                if (token != null)
                {
                    Function_ListBox.SelectedItem = token;
                }

                BytecodeSingularToken bst = TokenList.FirstOrDefault(x => x.startPos == start);
                if (bst != null)
                {
                    Tokens_ListBox.SelectedItem = bst;
                }
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
                ScriptEditor_Hexbox.SelectionStart = selectedToken.startPos;
                ScriptEditor_Hexbox.SelectionLength = 1;
            }
        }
    }
}
