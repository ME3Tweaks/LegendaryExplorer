using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.PlotDatabase;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorer.Misc;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for Bio2DAEditorWPF.xaml
    /// </summary>
    public partial class Bio2DAEditorWPF : ExportLoaderControl
    {
        public ObservableCollectionExtended<IndexedName> ParentNameList { get; private set; }

        private Bio2DA _table2da;
        public Bio2DA Table2DA
        {
            get => _table2da;
            private set => SetProperty(ref _table2da, value);
        }

        public ICommand CommitCommand { get; set; }

        public RelayCommand SetCellTypeCommand { get; private set; }

        public Bio2DAEditorWPF() : base("Bio2DA Editor")
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        public override bool CanParse(ExportEntry exportEntry)
        {
            return !exportEntry.IsDefaultObject && exportEntry.ObjectName != "Default2DA" && (exportEntry.ClassName == "Bio2DA" || exportEntry.ClassName == "Bio2DANumberedRows");
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            CurrentLoadedExport = exportEntry;
            Table2DA = new Bio2DA(CurrentLoadedExport);
        }

        public override void UnloadExport()
        {
            Table2DA = null;
            CurrentLoadedExport = null;
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new Bio2DAEditorWPF(), CurrentLoadedExport)
                {
                    Title = $"Bio2DA Editor - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
                elhw.Show();
            }
        }
        private void DataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            for (int counter = 0; counter < Bio2DA_DataGrid.SelectedCells.Count; counter++)
            {
                int columnIndex = Bio2DA_DataGrid.SelectedCells[0].Column.DisplayIndex;
                int rowIndex = Bio2DA_DataGrid.Items.IndexOf(Bio2DA_DataGrid.SelectedCells[0].Item);
                var item = Table2DA[rowIndex, columnIndex];
                Bio2DAInfo_CellCoordinates_TextBlock.Text = $"Selected cell coordinates: {rowIndex + 1},{columnIndex + 1}";
                if (item != null)
                {
                    Bio2DAInfo_CellDataType_TextBlock.Text = $"Selected cell data type: {item.Type}";
                    Bio2DAInfo_CellData_TextBlock.Text = $"Selected cell data: {item.DisplayableValue}";
                    Bio2DAInfo_CellDataOffset_TextBlock.Text = $"Selected cell data offset: 0x{item.Offset:X6}";
                    if (item.Type == Bio2DACell.Bio2DADataType.TYPE_INT)
                    {
                        var columnName = Table2DA.ColumnNames[columnIndex];
                        switch (columnName)
                        {
                            case "PlotID":
                                Bio2DAInfo_CellDataAsStrType_TextBlock.Text = "Value as Plot Int Path:";
                                Bio2DAInfo_CellDataAsStrRef_TextBlock.Text = PlotDatabases
                                    .FindPlotIntByID(item.IntValue, CurrentLoadedExport.FileRef.Game)?.Path;
                                break;
                            case "c_transition":
                                Bio2DAInfo_CellDataAsStrType_TextBlock.Text = "Value as Transition Path:";
                                Bio2DAInfo_CellDataAsStrRef_TextBlock.Text = PlotDatabases
                                    .FindPlotTransitionByID(item.IntValue, CurrentLoadedExport.FileRef.Game)?.Path;
                                break;
                            case "VisibleFunction":
                            case "UsableFunction":
                            case "UsablePlanetFunction":
                            case "c_shopcondition":
                                Bio2DAInfo_CellDataAsStrType_TextBlock.Text = "Value as Conditional Path:";
                                Bio2DAInfo_CellDataAsStrRef_TextBlock.Text = PlotDatabases
                                    .FindPlotConditionalByID(item.IntValue, CurrentLoadedExport.FileRef.Game)?.Path;
                                break;
                            default:
                                Bio2DAInfo_CellDataAsStrType_TextBlock.Text = "Value as TLK Reference:";
                                Bio2DAInfo_CellDataAsStrRef_TextBlock.Text = TLKManagerWPF.GlobalFindStrRefbyID(item.IntValue, CurrentLoadedExport.FileRef.Game, CurrentLoadedExport.FileRef);
                                break;
                        }
                    }
                    else
                    {
                        Bio2DAInfo_CellDataAsStrType_TextBlock.Text = "Value as TLK Reference:";
                        Bio2DAInfo_CellDataAsStrRef_TextBlock.Text = "Select cell of TYPE_INT to see as TLK Str";
                    }
                }
                else
                {
                    Bio2DAInfo_CellDataType_TextBlock.Text = "Selected cell data type: NULL";
                    Bio2DAInfo_CellData_TextBlock.Text = "Selected cell data:";
                    Bio2DAInfo_CellDataOffset_TextBlock.Text = "Selected cell data offset: N/A";
                    Bio2DAInfo_CellDataAsStrType_TextBlock.Text = "Value as TLK Reference:";
                    Bio2DAInfo_CellDataAsStrRef_TextBlock.Text = "Select a cell to preview TLK value";
                }
            }
        }

        private void LoadCommands()
        {
            CommitCommand = new GenericCommand(Commit2DA, CanCommit2DA);
            SetCellTypeCommand = new RelayCommand(SetCellType, SingleCellSelected);
        }

        private void SetCellType(object obj)
        {
            if (obj is string newCellType)
            {
                int columnIndex = Bio2DA_DataGrid.SelectedCells[0].Column.DisplayIndex;
                int rowIndex = Bio2DA_DataGrid.Items.IndexOf(Bio2DA_DataGrid.SelectedCells[0].Item);
                var item = Table2DA[rowIndex, columnIndex];
                switch (newCellType)
                {
                    case "INT":
                        item.Type = Bio2DACell.Bio2DADataType.TYPE_INT;
                        break;
                    case "NAME":
                        // Type will change on assignment
                        item.NameValue = new NameReference(CurrentLoadedExport.FileRef.Names[0], 0);
                        break;
                    case "FLOAT":
                        item.Type = Bio2DACell.Bio2DADataType.TYPE_FLOAT;
                        break;
                    case "NULL":
                        item.Type = Bio2DACell.Bio2DADataType.TYPE_NULL;
                        break;
                }
            }
        }

        private bool SingleCellSelected(object obj)
        {
            return Bio2DA_DataGrid != null && Bio2DA_DataGrid.SelectedCells.Count == 1;
        }

        private void Commit2DA()
        {
            Table2DA.Write2DAToExport();
            Table2DA.MarkAsUnmodified();
        }

        private bool CanCommit2DA() => Table2DA?.IsModified ?? false;

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog
            {
                Filter = "Excel spreadsheet|*.xlsx",
                FileName = CurrentLoadedExport.ObjectName
            };
            if (d.ShowDialog() == true)
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
            MessageBox.Show("Excel sheet must be formatted so: \r\nFIRST ROW must have the same column headings as current sheet. \r\nFIRST COLUMN has row numbers. \r\nIf using a multisheet excel file, the sheet tab must be named 'Import'.", "IMPORTANT INFORMATION:");
            OpenFileDialog oDlg = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                Title = "Import Excel table",
                CustomPlaces = AppDirectories.GameCustomPlaces
            };

            if (oDlg.ShowDialog() == true)
            {
                if (MessageBox.Show("This will overwrite the existing 2DA table.", "WARNING", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    Bio2DA resulting2DA = Bio2DAExtended.ReadExcelTo2DA(CurrentLoadedExport, oDlg.FileName);
                    if (resulting2DA != null)
                    {
                        if (resulting2DA.IsIndexed != Table2DA.IsIndexed)
                        {
                            MessageBox.Show(resulting2DA.IsIndexed
                                                ? "Warning: Imported sheet contains blank cells. Underlying sheet does not."
                                                : "Warning: Underlying sheet contains blank cells. Imported sheet does not.");
                        }
                        resulting2DA.Write2DAToExport();
                    }
                }
            }
        }
    }
}
