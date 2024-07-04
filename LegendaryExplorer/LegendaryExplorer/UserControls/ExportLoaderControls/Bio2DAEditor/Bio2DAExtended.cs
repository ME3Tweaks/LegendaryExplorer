using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using ClosedXML.Excel;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    public static class Bio2DAExtended
    {
        public static void Write2DAToExcel(this Bio2DA twoDA, string path)
        {
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(twoDA.Export.ObjectName.Name.Truncate(30));

            //write labels
            for (int rowindex = 0; rowindex < twoDA.RowCount; rowindex++)
            {
                worksheet.Cell(rowindex + 2, 1).Value = twoDA.RowNames[rowindex];
            }

            for (int colindex = 0; colindex < twoDA.ColumnCount; colindex++)
            {
                worksheet.Cell(1, colindex + 2).Value = twoDA.ColumnNames[colindex];
            }

            //write data
            for (int rowindex = 0; rowindex < twoDA.RowCount; rowindex++)
            {
                for (int colindex = 0; colindex < twoDA.ColumnCount; colindex++)
                {
                    if (twoDA.Cells[rowindex, colindex] != null)
                    {
                        var cell = twoDA.Cells[rowindex, colindex];
                        worksheet.Cell(rowindex + 2, colindex + 2).Value = cell.DisplayableValue;
                        if (cell.Type == Bio2DACell.Bio2DADataType.TYPE_INT && cell.IntValue > 0)
                        {
                            int stringId = cell.IntValue;
                            //Unsure if we will have reference to filerefs here depending on which constructor was used. Hopefully we will.
                            string tlkLookup = TLKManagerWPF.GlobalFindStrRefbyID(stringId, twoDA.Export.FileRef.Game, twoDA.Export.FileRef);
                            if (tlkLookup != "No Data" && tlkLookup != "")
                            {
                                worksheet.Cell(rowindex + 2, colindex + 2).GetComment().AddText(tlkLookup);
                            }
                        }
                    }
                }
            }

            worksheet.SheetView.FreezeRows(1);
            worksheet.SheetView.FreezeColumns(1);
            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(path);
        }

        public static Bio2DA ReadExcelTo2DA(ExportEntry export, string Filename)
        {
            var Workbook = new XLWorkbook(Filename);
            IXLWorksheet iWorksheet;
            if (Workbook.Worksheets.Count() > 1)
            {
                try
                {
                    iWorksheet = Workbook.Worksheet("Import");
                }
                catch
                {
                    MessageBox.Show("Import Sheet not found");
                    return null;
                }
            }
            else
            {
                iWorksheet = Workbook.Worksheet(1);
            }

            //STEP 1 Clear existing data
            Bio2DA bio2da = new Bio2DA
            {
                Export = export
            };

            //STEP 2 Read columns and row names

            //Column names
            IXLRow hRow = iWorksheet.Row(1);
            var colNames = new List<string>();
            var rowNames = new List<string>();
            foreach (IXLCell cell in hRow.Cells(hRow.FirstCellUsed().Address.ColumnNumber, hRow.LastCellUsed().Address.ColumnNumber))
            {
                if (cell.Address.ColumnNumber > 1) //ignore excel column 1
                {
                    colNames.Add(cell.Value.ToString());
                }
            }

            //Row names 
            IXLColumn column = iWorksheet.Column(1);
            foreach (IXLCell cell in column.Cells())
            {
                if (cell.Address.RowNumber > 1) //ignore excel row 1
                {
                    rowNames.Add(cell.Value.ToString());
                }
            }

            //Populate the Bio2DA now that we know the size
            bio2da.Cells = new Bio2DACell[rowNames.Count, colNames.Count];
            
            // Fill with null blanks initially so we have full coverage.
            for (int i = 0; i < rowNames.Count; i++)
            {
                for (int j = 0; j < colNames.Count; j++)
                {
                    bio2da.Cells[i, j] = new Bio2DACell() { package = export.FileRef}; 
                }
            }

            // Add the columns and names to the 2DA.
            foreach (var col in colNames)
                bio2da.AddColumn(col);
            foreach (var row in rowNames)
                bio2da.AddRow(row);

            //Step 3 Populate the table.
            //indices here are excel based. Subtract two to get Bio2DA based.
            for (int rowIndex = 2; rowIndex < (bio2da.RowCount + 2); rowIndex++)
            {
                for (int columnIndex = 2; columnIndex < bio2da.ColumnCount + 2; columnIndex++)
                {
                    IXLCell xlCell = iWorksheet.Cell(rowIndex, columnIndex);
                    string xlCellContents = xlCell.Value.ToString();
                    if (!string.IsNullOrEmpty(xlCellContents))
                    {
                        Bio2DACell newCell;
                        if (int.TryParse(xlCellContents, out int intVal))
                        {
                            newCell = new Bio2DACell(intVal) { package = export.FileRef };
                        }
                        else if (float.TryParse(xlCellContents, out float floatVal))
                        {
                            newCell = new Bio2DACell(floatVal) { package = export.FileRef };
                        }
                        else
                        {
                            newCell = new Bio2DACell(xlCellContents, export.FileRef) { package = export.FileRef };
                        }
                        bio2da[rowIndex - 2, columnIndex - 2] = newCell;
                    }
                    else
                    {
                        bio2da.IsIndexed = true;  //Null cells = indexing
                    }
                }
            }
            return bio2da;
        }
    }
}
