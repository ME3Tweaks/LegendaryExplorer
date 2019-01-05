using ME3Explorer.Packages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for Bio2DAEditorWPF.xaml
    /// </summary>
    public partial class Bio2DAEditorWPF : ExportLoaderControl
    {
        static ME1Explorer.TalkFiles talkFiles = new ME1Explorer.TalkFiles();
        private Bio2DA _table2da;
        public Bio2DA Table2DA
        {
            get
            {
                return _table2da;
            }
            private set
            {
                SetProperty(ref _table2da, value);
            }
        }

        public Bio2DAEditorWPF()
        {
            InitializeComponent();
            talkFiles.LoadGlobalTlk();
        }

        private void StartBio2DAScan()
        {
            Table2DA = new Bio2DA(CurrentLoadedExport);
        }

        public override bool CanParse(IExportEntry exportEntry)
        {
            return !exportEntry.ObjectName.Contains("Default__") && exportEntry.ObjectName != "Default2DA" && (exportEntry.ClassName == "Bio2DA" || exportEntry.ClassName == "Bio2DANumberedRows");
        }

        public override void LoadExport(IExportEntry exportEntry)
        {
            CurrentLoadedExport = exportEntry;
            StartBio2DAScan();
        }

        public override void UnloadExport()
        {
            Table2DA = null;
            CurrentLoadedExport = null;
        }

        private void DataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            for (int counter = 0; counter < (Bio2DA_DataGrid.SelectedCells.Count); counter++)
            {
                int columnIndex = Bio2DA_DataGrid.SelectedCells[0].Column.DisplayIndex;
                int rowIndex = Bio2DA_DataGrid.Items.IndexOf(Bio2DA_DataGrid.SelectedCells[0].Item);
                var item = Table2DA[rowIndex, columnIndex];
                Bio2DAInfo_CellCoordinates_TextBlock.Text = "Selected cell coordinates: " + (rowIndex + 1) + "," + (columnIndex + 1);
                Bio2DAInfo_CellDataOffset_TextBlock.Text = "Selected cell data offset: ????";// + (rowIndex + 1) + "," + (columnIndex + 1);
                if (item != null)
                {
                    Bio2DAInfo_CellDataType_TextBlock.Text = "Selected cell data type: " + item.Type.ToString() + "   " + item.GetDisplayableValue();
                    Bio2DAInfo_CellDataOffset_TextBlock.Text = "Selected cell data offset: 0x" + item.Offset.ToString("X6");
                    if (item.Type == Bio2DACell.Bio2DADataType.TYPE_INT)
                    {
                        if (CurrentLoadedExport.FileRef.Game == MEGame.ME1)
                        {
                            Bio2DAInfo_CellDataAsStrRef_TextBlock.Text = talkFiles.findDataById(item.GetIntValue());
                        }
                    }
                    else
                    {
                        Bio2DAInfo_CellDataAsStrRef_TextBlock.Text = "Select cell of TYPE_INT to see as TLK Str";
                    }
                }
                else
                {
                    Bio2DAInfo_CellDataType_TextBlock.Text = "Selected cell data type: NULL";
                    Bio2DAInfo_CellDataOffset_TextBlock.Text = "Selected cell data offset: N/A";
                    Bio2DAInfo_CellDataAsStrRef_TextBlock.Text = "Select a cell to preview TLK value";
                }
            }
        }

        private void Save_Button_Click(object sender, RoutedEventArgs e)
        {
            Table2DA.Write2DAToExport();
            Table2DA.MarkAsUnmodified();
        }
    }

}
