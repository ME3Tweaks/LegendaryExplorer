using System;
using System.Collections.Generic;
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

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for UnrealScriptWPF.xaml
    /// </summary>
    public partial class UnrealScriptWPF : ExportLoaderControl
    {
        private HexBox ScriptEditor_Hexbox;

        public UnrealScriptWPF()
        {
            InitializeComponent();
        }

        public override bool CanParse(IExportEntry exportEntry)
        {
            return exportEntry.ClassName == "Function";
        }

        public override void LoadExport(IExportEntry exportEntry)
        {
            CurrentLoadedExport = exportEntry;
            ScriptEditor_Hexbox.ByteProvider = new DynamicByteProvider(CurrentLoadedExport.Data);
            StartFunctionScan(CurrentLoadedExport.Data);
        }

        private void StartFunctionScan(byte[] data)
        {
            if (CurrentLoadedExport.FileRef.Game == MEGame.ME3)
            {
                var func = new ME3Explorer.Unreal.Classes.Function(data, CurrentLoadedExport.FileRef);
                func.ParseFunction();
                Function_TextBox.Text = func.ScriptText;
                Tokens_ListBox.ItemsSource = func.TokenList;
                

            }
            else if (CurrentLoadedExport.FileRef.Game == MEGame.ME1)
            {
                ME1Explorer.Unreal.Classes.Function func = new ME1Explorer.Unreal.Classes.Function(data, CurrentLoadedExport.FileRef as ME1Package);
                try
                {
                    Function_TextBox.Text = func.ToRawText();
                }
                catch (Exception e)
                {
                    Function_TextBox.Text = "Error parsing function: " + e.Message;
                }
            }
            else
            {
                Function_TextBox.Text = "Parsing UnrealScript Functions for this game is not supported.";
            }
        }

        public override void UnloadExport()
        {
            CurrentLoadedExport = null;
        }

        private void hb1_SelectionChanged(object sender, EventArgs e)
        {

        }

        private void UnrealScriptWPF_Loaded(object sender, RoutedEventArgs e)
        {
            ScriptEditor_Hexbox = (HexBox)Interpreter_Hexbox_Host.Child;
        }

        private void ScriptEditor_PreviewScript_Click(object sender, RoutedEventArgs e)
        {
            byte[] data = (ScriptEditor_Hexbox.ByteProvider as DynamicByteProvider).Bytes.ToArray();
            StartFunctionScan(data);
        }

        private void ScriptEditor_SaveHexChanges_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
