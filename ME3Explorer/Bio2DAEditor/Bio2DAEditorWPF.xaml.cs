using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using Microsoft.Win32;
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
using static ME3Explorer.PackageEditorWPF;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for Bio2DAEditorWPF.xaml
    /// </summary>
    public partial class Bio2DAEditorWPF : ExportLoaderControl
    {
        public ObservableCollectionExtended<IndexedName> ParentNameList { get; private set; }

        //private Bio2DA CachedME12DA_TalentsGUI;
        private Bio2DA CachedME12DA_ClassTalents_Talents;
        private Bio2DA CachedME12DA_TalentEffectLevels;

        private Bio2DA _table2da;
        public Bio2DA Table2DA
        {
            get => _table2da;
            private set => SetProperty(ref _table2da, value);
        }

        public Bio2DAEditorWPF()
        {
            InitializeComponent();
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
            if (CachedME12DA_TalentEffectLevels == null && CurrentLoadedExport.FileRef.FileName.Contains("Engine.u"))
            {
                IExportEntry TalentEffectLevels = CurrentLoadedExport.FileRef.Exports.FirstOrDefault(x => x.ObjectName == "Talent_TalentEffectLevels" && x.ClassName == "Bio2DANumberedRows");
                if (TalentEffectLevels != null)
                {
                    CachedME12DA_TalentEffectLevels = new Bio2DA(TalentEffectLevels);
                }
            }

            for (int counter = 0; counter < (Bio2DA_DataGrid.SelectedCells.Count); counter++)
            {
                int columnIndex = Bio2DA_DataGrid.SelectedCells[0].Column.DisplayIndex;
                string columnName = Table2DA.GetColumnNameByIndex(columnIndex);
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
                            if (columnName == "TalentID" && CachedME12DA_TalentEffectLevels != null)
                            {
                                //Get Talent ID name
                                for (int i = 0; i < CachedME12DA_TalentEffectLevels.RowNames.Count; i++)
                                {
                                    if (CachedME12DA_TalentEffectLevels[i, 0].GetIntValue() == item.GetIntValue())
                                    {
                                        int labelColumn = CachedME12DA_TalentEffectLevels.GetColumnIndexByName("Talent_Label");
                                        string label = CachedME12DA_TalentEffectLevels[i, labelColumn].GetDisplayableValue();
                                        Bio2DAInfo_CellDataAsStrRef_TextBlock.Text = label;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                Bio2DAInfo_CellDataAsStrRef_TextBlock.Text = ME1Explorer.ME1TalkFiles.findDataById(item.GetIntValue());
                            }
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

        /// <summary>
        /// Removes the access key where you can do _ for quick key press to go to a column. will make headers and stuff look proper
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            string header = e.Column.Header.ToString();

            // Replace all underscores with two underscores, to prevent AccessKey handling
            e.Column.Header = header.Replace("_", "__");
        }

        private void Save_Button_Click(object sender, RoutedEventArgs e)
        {
            Table2DA.Write2DAToExport();
            Table2DA.MarkAsUnmodified();
        }

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog
            {
                Filter = "Excel spreadsheet|*.xlsx",
                FileName = CurrentLoadedExport.ObjectName
            };
            var result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
                Table2DA.Write2DAToExcel(d.FileName);
                MessageBox.Show("Done");
            }
        }

        internal void SetParentNameList(ObservableCollectionExtended<IndexedName> namesList)
        {
            ParentNameList = namesList;
        }

        public override void Dispose()
        {
            //Nothing to dispose in this control
        }

        private void ImportToExcel_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Excel sheet must be formatted so: \r\nFIRST ROW must have the column data types.  \r\nSECOND ROW must have the same column headings as current sheet. \r\nThe sheet tab must be named 'Import'.", "IMPORTANT INFORMATION:" );
            OpenFileDialog oDlg = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx"
            };
            oDlg.Title = "Import Excel table";
            var result = oDlg.ShowDialog();
            if (result.HasValue && result.Value)
            {
                if (MessageBox.Show("This will overwrite the existing 2DA table.", "WARNING", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    if (Table2DA.WriteExcelTo2DA(oDlg.FileName)) { MessageBox.Show("Done"); };
                }
            }
        }
    }
}
