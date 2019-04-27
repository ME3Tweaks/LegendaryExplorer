using ClosedXML.Excel;
using Gammtek.Conduit.Extensions;
using Gibbed.IO;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ME3Explorer
{
    public class Bio2DA
    {
        public bool IsIndexed = false;
        /// <summary>
        /// Number of cells that the Bio2DA says exist in the table
        /// </summary>
        int PopulatedCellCount = 0;
        static string[] stringRefColumns = { "StringRef", "SaveGameStringRef", "Title", "LabelRef", "Name", "ActiveWorld", "Description", "ButtonLabel" };
        public Bio2DACell[,] Cells { get; set; }
        public List<string> RowNames { get; set; }
        public List<string> ColumnNames { get; set; }

        public int RowCount
        {
            get
            {
                return RowNames == null ? 0 : RowNames.Count;
            }
        }

        public int ColumnCount
        {
            get
            {
                return ColumnNames == null ? 0 : ColumnNames.Count;
            }
        }

        IExportEntry export;
        public Bio2DA(IExportEntry export)
        {
            //Console.WriteLine("Loading " + export.ObjectName);
            this.export = export;
            IMEPackage pcc = export.FileRef;
            byte[] data = export.Data;

            RowNames = new List<string>();
            if (export.ClassName == "Bio2DA")
            {
                string rowLabelsVar = "m_sRowLabel";
                var properties = export.GetProperties();
                var props = export.GetProperty<ArrayProperty<NameProperty>>(rowLabelsVar);
                if (props != null)
                {
                    foreach (NameProperty n in props)
                    {
                        RowNames.Add(n.ToString());
                    }
                }
                else
                {
                    Console.WriteLine("Unable to find row names property!");
                    Debugger.Break();
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
                    }
                }
                else
                {
                    Debug.WriteLine("Unable to find row names property (m_lstRowNumbers)!");
                    Debugger.Break();
                    return;
                }
            }

            //Get Columns
            ColumnNames = new List<string>();
            int colcount = BitConverter.ToInt32(data, data.Length - 4);
            int currentcoloffset = 0;
            while (colcount >= 0)
            {
                currentcoloffset += 4;
                int colindex = BitConverter.ToInt32(data, data.Length - currentcoloffset);
                currentcoloffset += 8; //names in this case don't use nameindex values.
                int nameindex = BitConverter.ToInt32(data, data.Length - currentcoloffset);
                string name = pcc.getNameEntry(nameindex);
                ColumnNames.Insert(0, name);
                colcount--;
            }
            Cells = new Bio2DACell[RowCount, ColumnCount];

            currentcoloffset += 4;  //column count.
            int infilecolcount = BitConverter.ToInt32(data, data.Length - currentcoloffset);

            //start of binary data
            int binstartoffset = export.propsEnd(); //arrayheader + nonenamesize + number of items in this list
            int curroffset = binstartoffset;

            int cellcount = BitConverter.ToInt32(data, curroffset);
            if ( cellcount > 0 ) 
            {
                curroffset += 4;
                for (int rowindex = 0; rowindex < RowCount; rowindex++)
                {
                    for (int colindex = 0; colindex < ColumnCount && curroffset < data.Length - currentcoloffset; colindex++)
                    {
                        byte dataType = data[curroffset];
                        curroffset++;
                        int dataSize = dataType == (byte)Bio2DACell.Bio2DADataType.TYPE_NAME ? 8 : 4;
                        byte[] celldata = new byte[dataSize];
                        Buffer.BlockCopy(data, curroffset, celldata, 0, dataSize);
                        Bio2DACell cell = new Bio2DACell(pcc, curroffset, dataType, celldata);
                        Cells[rowindex, colindex] = cell;
                        curroffset += dataSize;
                    }
                }
                PopulatedCellCount = RowCount * ColumnCount;  //Required for edits to write correct count if SaveToExport
            }
            else
            {
                IsIndexed = true;
                curroffset += 4; //theres a 0 here for some reason
                cellcount = BitConverter.ToInt32(data, curroffset);
                curroffset += 4;

                //curroffset += 4;
                while (PopulatedCellCount < cellcount)
                {
                    int index = BitConverter.ToInt32(data, curroffset);
                    int row = index / ColumnCount;
                    int col = index % ColumnCount;
                    curroffset += 4;
                    byte dataType = data[curroffset];
                    int dataSize = dataType == (byte)Bio2DACell.Bio2DADataType.TYPE_NAME ? 8 : 4;
                    curroffset++;
                    byte[] celldata = new byte[dataSize];
                    Buffer.BlockCopy(data, curroffset, celldata, 0, dataSize);
                    Bio2DACell cell = new Bio2DACell(pcc, curroffset, dataType, celldata);
                    this[row, col] = cell;
                    curroffset += dataSize;
                }
            }
            //Console.WriteLine("Finished loading " + export.ObjectName);
        }

        /// <summary>
        /// Initializes a blank Bio2DA. Cells is not initialized, the caller must set up this Bio2DA.
        /// </summary>
        public Bio2DA()
        {
            ColumnNames = new List<string>();
            RowNames = new List<string>();
        }

        internal string GetColumnNameByIndex(int columnIndex)
        {
            if (columnIndex < ColumnNames.Count && columnIndex >= 0)
            {
                return ColumnNames[columnIndex];
            }
            return null;
        }

        public Bio2DACell GetColumnItem(int row, string columnName)
        {
            int colIndex = ColumnNames.IndexOf(columnName);
            if (colIndex >= 0)
            {
                return Cells[row, colIndex];
            }
            return null;
        }

        public int GetColumnIndexByName(string columnName)
        {
            return ColumnNames.IndexOf(columnName);
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
            var worksheet = workbook.Worksheets.Add(export.ObjectName.Truncate(30));

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
                        worksheet.Cell(rowindex + 2, colindex + 2).Value = cell.GetDisplayableValue();
                        if (cell.Type == Bio2DACell.Bio2DADataType.TYPE_INT && cell.GetIntValue() > 0)
                        {
                            int stringId = cell.GetIntValue();
                            string tlkLookup = ME1Explorer.ME1TalkFiles.findDataById(stringId);
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
            using (var stream = new MemoryStream())
            {
                //Cell count
                if (IsIndexed)
                {
                    //Indexed ones seem to have 0 at start
                    stream.WriteBytes(BitConverter.GetBytes(0));
                }
                stream.WriteBytes(BitConverter.GetBytes(PopulatedCellCount));

                //Write cell data
                for (int rowindex = 0; rowindex < RowCount; rowindex++)
                {
                    for (int colindex = 0; colindex < ColumnCount; colindex++)
                    {
                        Bio2DACell cell = Cells[rowindex, colindex];
                        if (cell != null)
                        {
                            if (IsIndexed)
                            {
                                //write index
                                int index = (rowindex * ColumnCount) + colindex; //+1 because they are not zero based indexes since they are numerals
                                stream.WriteBytes(BitConverter.GetBytes(index));
                            }
                            stream.WriteByte((byte)cell.Type);
                            stream.WriteBytes(cell.Data);
                        }
                        else
                        {
                            if (IsIndexed)
                            {
                                //this is a blank cell. It is not present in the table.
                                continue;
                            }
                            else
                            {
                                Debug.WriteLine("THIS SHOULDN'T OCCUR!");
                                Debugger.Break();
                                throw new Exception("A non-indexed Bio2DA cannot have null cells.");
                            }
                        }
                    }
                }

                //Write Columns
                if (!IsIndexed)
                {
                    stream.WriteBytes(BitConverter.GetBytes(0)); //seems to be a 0 before column definitions
                }
                //Console.WriteLine("Columns defs start at " + stream.Position.ToString("X6"));
                stream.WriteBytes(BitConverter.GetBytes(ColumnCount));
                for (int colindex = 0; colindex < ColumnCount; colindex++)
                {
                    //Console.WriteLine("Writing column definition " + columnNames[colindex]);
                    int nameIndexForCol = export.FileRef.FindNameOrAdd(ColumnNames[colindex]);
                    stream.WriteBytes(BitConverter.GetBytes(nameIndexForCol));
                    stream.WriteBytes(BitConverter.GetBytes(0)); //second half of name reference in 2da is always zero since they're always indexed at 0
                    stream.WriteBytes(BitConverter.GetBytes(colindex));
                }

                int propsEnd = export.propsEnd();
                byte[] binarydata = stream.ToArray();

                //Todo: Rewrite properties here
                PropertyCollection props = new PropertyCollection();
                if (export.ClassName == "Bio2DA")
                {
                    ArrayProperty<NameProperty> indicies = new ArrayProperty<NameProperty>(ArrayType.Name, "m_sRowLabel");
                    foreach (var rowname in RowNames)
                    {
                        indicies.Add(new NameProperty(rowname));
                    }
                    props.Add(indicies);
                }
                else
                {
                    ArrayProperty<IntProperty> indices = new ArrayProperty<IntProperty>(ArrayType.Int, "m_lstRowNumbers");
                    foreach (var rowname in RowNames)
                    {
                        indices.Add(new IntProperty(int.Parse(rowname)));
                    }
                    props.Add(indices);
                }

                MemoryStream propsStream = new MemoryStream();
                props.WriteTo(propsStream, export.FileRef);
                MemoryStream currentDataStream = new MemoryStream(export.Data);
                byte[] propertydata = propsStream.ToArray();
                int propertyStartOffset = export.GetPropertyStart();
                var newExportData = new byte[propertyStartOffset + propertydata.Length + binarydata.Length];
                Buffer.BlockCopy(export.Data, 0, newExportData, 0, propertyStartOffset);
                propertydata.CopyTo(newExportData, propertyStartOffset);
                binarydata.CopyTo(newExportData, propertyStartOffset + propertydata.Length);
                //Console.WriteLine("Old data size: " + export.Data.Length);
                //Console.WriteLine("NEw data size: " + newExportData.Length);

                //This assumes the input and output data sizes are the same. We should not assume this with new functionality
                //if (export.Data.Length != newExportData.Length)
                //{
                //    Debug.WriteLine("FILES ARE WRONG SIZE");
                //    Debugger.Break();
                //}
                export.Data = newExportData;
            }
        }

        public static Bio2DA ReadExcelTo2DA(IExportEntry export, string Filename)
        {
            var Workbook = new XLWorkbook(Filename);
            IXLWorksheet iWorksheet = null;
            if ( Workbook.Worksheets.Count() > 1)
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

            //Do we want to limit user to importing same column structure as existing?  Who would be stupid enough to do something else??? ME.
            // - Kinkojiro, 2019

            //STEP 1 Clear existing data
            Bio2DA bio2da = new Bio2DA();
            bio2da.export = export;

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
                        Bio2DACell newCell = new Bio2DACell();
                        if (int.TryParse(xlCellContents, out int intVal))
                        {
                            newCell.Type = Bio2DACell.Bio2DADataType.TYPE_INT;
                            newCell.Data = BitConverter.GetBytes(intVal);
                        }
                        else if (float.TryParse(xlCellContents, out float floatVal))
                        {
                            newCell.Type = Bio2DACell.Bio2DADataType.TYPE_INT;
                            newCell.Data = BitConverter.GetBytes(intVal);
                        }
                        else
                        {
                            newCell.Type = Bio2DACell.Bio2DADataType.TYPE_NAME;
                            newCell.Pcc = export.FileRef; //for displaying, if this displays before the export is reloaded and 2da is refreshed
                            newCell.Data = BitConverter.GetBytes((long)export.FileRef.FindNameOrAdd(xlCellContents)); //long because names are 8 bytes not 4
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
            get
            {
                // get the item for that index.
                return Cells[rowindex, colindex];
            }
            set
            {
                // set the item for this index. value will be of type Bio2DACell.
                if (Cells[rowindex, colindex] == null && value != null)
                {
                    PopulatedCellCount++;
                }
                if (Cells[rowindex, colindex] != null && value == null)
                {
                    PopulatedCellCount--;
                }
                Cells[rowindex, colindex] = value;
            }
        }
    }
}
