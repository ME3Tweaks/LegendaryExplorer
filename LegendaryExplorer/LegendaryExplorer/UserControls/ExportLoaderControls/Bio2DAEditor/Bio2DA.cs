using System;
using System.Collections.Generic;
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

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    public class Bio2DA
    {
        public bool IsIndexed = false;

        //static string[] stringRefColumns = { "StringRef", "SaveGameStringRef", "Title", "LabelRef", "Name", "ActiveWorld", "Description", "ButtonLabel" };
        public Bio2DACell[,] Cells { get; set; }
        public List<string> RowNames { get; set; }
        /// <summary>
        /// Replaces _ with __ to avoid AccessKeys when rendering 
        /// </summary>
        public List<string> RowNamesUI { get; private set; }
        public List<string> ColumnNames { get; set; }

        public int RowCount => RowNames?.Count ?? 0;

        public int ColumnCount => ColumnNames?.Count ?? 0;

        public bool IsModified
        {
            get
            {
                if (Cells == null) return false;
                for (int i = 0; i < RowCount; i++)
                {
                    for (int j = 0; j < ColumnCount; j++)
                    {
                        Bio2DACell c = Cells[i, j];
                        if (c == null) continue;
                        if (c.IsModified) return true;
                    }
                }
                return false;
            }
        }

        ExportEntry Export;
        public Bio2DA(ExportEntry export)
        {
            //Console.WriteLine("Loading " + export.ObjectName);
            Export = export;

            RowNames = new List<string>();
            RowNamesUI = new List<string>();
            if (export.ClassName == "Bio2DA")
            {
                const string rowLabelsVar = "m_sRowLabel";
                var props = export.GetProperty<ArrayProperty<NameProperty>>(rowLabelsVar);
                if (props != null)
                {
                    foreach (NameProperty n in props)
                    {
                        RowNames.Add(n.Value.Instanced);
                        RowNamesUI.Add(n.Value.Instanced.Replace("_", "__"));
                    }
                }
                else
                {
                    Console.WriteLine("Unable to find row names property!");
                    return;
                }
            }
            else
            {
                var props = export.GetProperty<ArrayProperty<IntProperty>>("m_lstRowNumbers");//Bio2DANumberedRows
                if (props != null)
                {
                    foreach (IntProperty n in props)
                    {
                        RowNames.Add(n.Value.ToString());
                        RowNamesUI.Add(n.Value.ToString());
                    }
                }
                else
                {
                    Debug.WriteLine("Unable to find row names property (m_lstRowNumbers)!");
                    return;
                }
            }

            var binary = export.GetBinaryData<Bio2DABinary>();

            ColumnNames = new List<string>(binary.ColumnNames.Select(n => n.Name));
            Cells = new Bio2DACell[RowCount, ColumnCount];

            foreach ((int index, Bio2DABinary.Cell cell) in binary.Cells)
            {
                int row = index / ColumnCount;
                int col = index % ColumnCount;
                this[row, col] = cell.Type switch
                {
                    Bio2DABinary.DataType.INT => new Bio2DACell(cell.IntValue),
                    Bio2DABinary.DataType.NAME => new Bio2DACell(cell.NameValue, export.FileRef),
                    Bio2DABinary.DataType.FLOAT => new Bio2DACell(cell.FloatValue),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            IsIndexed = binary.IsIndexed;
        }

        /// <summary>
        /// Initializes a blank Bio2DA. Cells is not initialized, the caller must set up this Bio2DA.
        /// </summary>
        public Bio2DA()
        {
            ColumnNames = new List<string>();
            RowNames = new List<string>();
        }

        internal void MarkAsUnmodified()
        {
            for (int rowindex = 0; rowindex < RowCount; rowindex++)
            {
                for (int colindex = 0; colindex < ColumnCount; colindex++)
                {
                    Bio2DACell cell = Cells[rowindex, colindex];
                    if (cell != null)
                    {
                        cell.IsModified = false;
                    }
                }
            }
        }

        public void Write2DAToExcel(string path)
        {
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(Export.ObjectName.Name.Truncate(30));

            //write labels
            for (int rowindex = 0; rowindex < RowCount; rowindex++)
            {
                worksheet.Cell(rowindex + 2, 1).Value = RowNames[rowindex];
            }

            for (int colindex = 0; colindex < ColumnCount; colindex++)
            {
                worksheet.Cell(1, colindex + 2).Value = ColumnNames[colindex];
            }

            //write data
            for (int rowindex = 0; rowindex < RowCount; rowindex++)
            {
                for (int colindex = 0; colindex < ColumnCount; colindex++)
                {
                    if (Cells[rowindex, colindex] != null)
                    {
                        var cell = Cells[rowindex, colindex];
                        worksheet.Cell(rowindex + 2, colindex + 2).Value = cell.DisplayableValue;
                        if (cell.Type == Bio2DACell.Bio2DADataType.TYPE_INT && cell.IntValue > 0)
                        {
                            int stringId = cell.IntValue;
                            //Unsure if we will have reference to filerefs here depending on which constructor was used. Hopefully we will.
                            string tlkLookup = TLKManagerWPF.GlobalFindStrRefbyID(stringId, Export.FileRef.Game, Export.FileRef);
                            if (tlkLookup != "No Data" && tlkLookup != "")
                            {
                                worksheet.Cell(rowindex + 2, colindex + 2).Comment.AddText(tlkLookup);
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

        public void Write2DAToExport()
        {
            var binary = new Bio2DABinary
            {
                ColumnNames = ColumnNames.Select(s => new NameReference(s)).ToList(),
                Cells = new OrderedMultiValueDictionary<int, Bio2DABinary.Cell>(),
                Export = Export,
                IsIndexed = IsIndexed
            };

            for (int rowindex = 0; rowindex < RowCount; rowindex++)
            {
                for (int colindex = 0; colindex < ColumnCount; colindex++)
                {
                    Bio2DACell cell = Cells[rowindex, colindex];
                    if (cell != null)
                    {
                        int index = (rowindex * ColumnCount) + colindex;
                        binary.Cells.Add(index, cell.Type switch
                        {
                            Bio2DACell.Bio2DADataType.TYPE_INT => new Bio2DABinary.Cell { IntValue = cell.IntValue, Type = Bio2DABinary.DataType.INT },
                            Bio2DACell.Bio2DADataType.TYPE_NAME => new Bio2DABinary.Cell { NameValue = cell.NameValue, Type = Bio2DABinary.DataType.NAME },
                            Bio2DACell.Bio2DADataType.TYPE_FLOAT => new Bio2DABinary.Cell { FloatValue = cell.FloatValue, Type = Bio2DABinary.DataType.FLOAT },
                            _ => throw new ArgumentOutOfRangeException()
                        });
                    }
                }
            }

            Property rowsProp = Export.ClassName switch
            {
                "Bio2DA" => new ArrayProperty<NameProperty>(RowNames.Select(n => new NameProperty(n)), "m_sRowLabel"),
                "Bio2DANumberedRows" => new ArrayProperty<IntProperty>(RowNames.Select(n => new IntProperty(int.Parse(n))), "m_lstRowNumbers"),
                _ => throw new ArgumentOutOfRangeException()
            };

            Export.WritePropertyAndBinary(rowsProp, binary);
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
            foreach (IXLCell cell in hRow.Cells(hRow.FirstCellUsed().Address.ColumnNumber, hRow.LastCellUsed().Address.ColumnNumber))
            {
                if (cell.Address.ColumnNumber > 1) //ignore excel column 1
                {
                    bio2da.ColumnNames.Add(cell.Value.ToString());
                }
            }

            //Row names 
            IXLColumn column = iWorksheet.Column(1);
            foreach (IXLCell cell in column.Cells())
            {
                if (cell.Address.RowNumber > 1) //ignore excel row 1
                {
                    bio2da.RowNames.Add(cell.Value.ToString());
                }
            }

            //Populate the Bio2DA now that we know the size
            bio2da.Cells = new Bio2DACell[bio2da.RowCount, bio2da.ColumnCount];


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
                            newCell = new Bio2DACell(intVal);
                        }
                        else if (float.TryParse(xlCellContents, out float floatVal))
                        {
                            newCell = new Bio2DACell(floatVal);
                        }
                        else
                        {
                            newCell = new Bio2DACell(xlCellContents, export.FileRef);
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

        public Bio2DACell this[int rowindex, int colindex]
        {
            get => Cells[rowindex, colindex];
            set
            {
                // set the item for this index. value will be of type Bio2DACell.
                if (Cells[rowindex, colindex] == null && value != null)
                {
                }
                if (Cells[rowindex, colindex] != null && value == null)
                {
                }
                Cells[rowindex, colindex] = value;
            }
        }
    }
}
