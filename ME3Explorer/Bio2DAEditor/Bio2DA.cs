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
        bool IsIndexed = false;
        int CellCount = 0;
        static string[] stringRefColumns = { "StringRef", "SaveGameStringRef", "Title", "LabelRef", "Name", "ActiveWorld", "Description", "ButtonLabel" };
        public Bio2DACell[,] Cells { get; set; }
        public List<string> RowNames { get; set; }
        public List<string> ColumnNames { get; set; }
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
            Cells = new Bio2DACell[RowNames.Count(), ColumnNames.Count()];

            currentcoloffset += 4;  //column count.
            int infilecolcount = BitConverter.ToInt32(data, data.Length - currentcoloffset);

            //start of binary data
            int binstartoffset = export.propsEnd(); //arrayheader + nonenamesize + number of items in this list
            int curroffset = binstartoffset;

            int cellcount = BitConverter.ToInt32(data, curroffset);
            if (cellcount > 0)
            {
                curroffset += 4;
                for (int rowindex = 0; rowindex < RowNames.Count(); rowindex++)
                {
                    for (int colindex = 0; colindex < ColumnNames.Count() && curroffset < data.Length - currentcoloffset; colindex++)
                    {
                        byte dataType = 255;
                        dataType = data[curroffset];
                        curroffset++;
                        int dataSize = dataType == (byte)Bio2DACell.Bio2DADataType.TYPE_NAME ? 8 : 4;
                        byte[] celldata = new byte[dataSize];
                        Buffer.BlockCopy(data, curroffset, celldata, 0, dataSize);
                        Bio2DACell cell = new Bio2DACell(pcc, curroffset, dataType, celldata);
                        Cells[rowindex, colindex] = cell;
                        curroffset += dataSize;
                    }
                }
                CellCount = RowNames.Count() * ColumnNames.Count();
            }
            else
            {
                IsIndexed = true;
                curroffset += 4; //theres a 0 here for some reason
                cellcount = BitConverter.ToInt32(data, curroffset);
                curroffset += 4;

                //curroffset += 4;
                while (CellCount < cellcount)
                {
                    int index = BitConverter.ToInt32(data, curroffset);
                    int row = index / ColumnNames.Count();
                    int col = index % ColumnNames.Count();
                    curroffset += 4;
                    byte dataType = data[curroffset];
                    int dataSize = dataType == (byte)Bio2DACell.Bio2DADataType.TYPE_NAME ? 8 : 4;
                    curroffset++;
                    byte[] celldata = new byte[dataSize];
                    Buffer.BlockCopy(data, curroffset, celldata, 0, dataSize);
                    Bio2DACell cell = new Bio2DACell(pcc, curroffset, dataType, celldata);
                    Cells[row, col] = cell;
                    CellCount++;
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
            for (int rowindex = 0; rowindex < RowNames.Count(); rowindex++)
            {
                for (int colindex = 0; colindex < ColumnNames.Count(); colindex++)
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
            for (int rowindex = 0; rowindex < RowNames.Count(); rowindex++)
            {
                worksheet.Cell(rowindex + 2, 1).Value = RowNames[rowindex];
            }

            for (int colindex = 0; colindex < ColumnNames.Count(); colindex++)
            {
                worksheet.Cell(1, colindex + 2).Value = ColumnNames[colindex];
            }

            //write data
            for (int rowindex = 0; rowindex < RowNames.Count(); rowindex++)
            {
                for (int colindex = 0; colindex < ColumnNames.Count(); colindex++)
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
                stream.WriteBytes(BitConverter.GetBytes(CellCount));

                //Write cell data
                for (int rowindex = 0; rowindex < RowNames.Count(); rowindex++)
                {
                    for (int colindex = 0; colindex < ColumnNames.Count(); colindex++)
                    {
                        Bio2DACell cell = Cells[rowindex, colindex];
                        if (cell != null)
                        {
                            if (IsIndexed)
                            {
                                //write index
                                int index = (rowindex * ColumnNames.Count()) + colindex; //+1 because they are not zero based indexes since they are numerals
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
                stream.WriteBytes(BitConverter.GetBytes(ColumnNames.Count()));
                for (int colindex = 0; colindex < ColumnNames.Count(); colindex++)
                {
                    //Console.WriteLine("Writing column definition " + columnNames[colindex]);
                    int nameIndexForCol = export.FileRef.findName(ColumnNames[colindex]);
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
                    foreach(var rowname in RowNames)
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
            try
            {
                //You could just have it check only the first sheet. This way you would not need to rename exported sheets  //A: This way I can have a single excel file with all the references, notes etc and just autocreate a page to import.
                //If you wanted to be really crazy you could do a sheet selector. [By naming you already have that] I don't know how useful that is though since we only export single sheet [I use large spreadsheets for tracking all my modding stuff.]
                iWorksheet = Workbook.Worksheet("Import");
            }
            catch
            {
                MessageBox.Show("Import Sheet not found");
                return null; 
            }

            //Do we want to limit user to importing same column structure as existing?  Who would be stupid enough to do something else??? ME.
            // - Kinkojiro, 2019

            //STEP 0 Clear existing data
            Bio2DA bio2da = new Bio2DA();
            bio2da.export = export;

            int i = 0; //debug

            //STEP 1 Read columns and row names

            //Column names
            IXLRow hRow = iWorksheet.Row(1);
            foreach (IXLCell cell in hRow.Cells(hRow.FirstCellUsed().Address.ColumnNumber, hRow.LastCellUsed().Address.ColumnNumber))
            {
                if (cell.Address.ColumnNumber > 1) //ignore excel column 1
                {
                    bio2da.ColumnNames.Add(cell.Value.ToString());
                    //i++; //debug column count
                }
            }


            //Row names 
            IXLColumn column = iWorksheet.Column(1);
            foreach (IXLCell cell in column.Cells())
            {
                if (cell.Address.RowNumber > 1) //ignore excel row 1
                {
                    bio2da.RowNames.Add(cell.Value.ToString());
                    //i++; //debug row count
                }
            }

            //Populate the Bio2DA now that we know the size
            bio2da.Cells = new Bio2DACell[bio2da.RowNames.Count(), bio2da.ColumnNames.Count()];
            

            //Step 3 Populate the table.
            /*
             * if string.isemptyornull(READVALUEFROMEXCEL) continue reading next cell; (skip)
             * if (int.TryParse(READVALUEFROMEXCEL, out int value) {
             *  cell.datatype = int
             *  cell.data = BitConverter.GetBytes(value)
             * } else if float.try...
             * 
             * } else {
             *  its a string. treat it as a name
             * }
             */

            int rowindex = 0;
            int columnindex = 0;

            foreach (IXLRow row in iWorksheet.Rows())
            {
                foreach (IXLCell cell in row.Cells(row.FirstCellUsed().Address.ColumnNumber, row.LastCellUsed().Address.ColumnNumber))
                {
                    columnindex = cell.Address.ColumnNumber - 2;
                    rowindex = cell.Address.RowNumber - 2;
                    if ((columnindex >= 0) & (rowindex >= 0)) //Make sure start from excel column 2, row 2
                    {
                        string debugCell = cell.Value.ToString(); //debug
                        if (!String.IsNullOrEmpty(cell.Value.ToString()))
                        {
                            var nCellType = Bio2DACell.Bio2DADataType.TYPE_INT;
                            byte[] nCellData;

                            if (int.TryParse(cell.Value.ToString(), out int intData)) //Test int
                            {
                                nCellData = BitConverter.GetBytes(intData);
                            }
                            else if (float.TryParse(cell.Value.ToString(), out float fltData)) //Test float
                            {
                                nCellType = Bio2DACell.Bio2DADataType.TYPE_FLOAT;
                                nCellData = BitConverter.GetBytes(fltData);
                            }
                            else  //Is name
                            {
                                int nameData = export.FileRef.FindNameOrAdd(cell.Value.ToString());
                                nCellType = Bio2DACell.Bio2DADataType.TYPE_NAME;
                                nCellData = BitConverter.GetBytes(nameData);
                            }
                            byte wtfByte = 0; //HOW DO YOU GET nCellType 0,1,2 to a Byte boolean?>?????
                            Bio2DACell nCell = new Bio2DACell(export.FileRef, wtfByte, nCellData);
                            bio2da.Cells[rowindex, columnindex] = nCell;

                            nCell.DisplayableValue = cell.Value.ToString(); //Debug write again to be sure.
                        }
                        else //TEMPORARY DEBUG GET AROUND EMPTY CELLS
                        {
                            //byte wtfByte = 0;
                            //Bio2DACell nCell = new Bio2DACell(export.FileRef, wtfByte, BitConverter.GetBytes(0));
                            //bio2da.Cells[columnindex, rowindex] = nCell;
                        }
                    }
                }
                //CURRENT ISSUES:
                // 1. DONE
                // 2. TABLE IS ONLY DOING INTS - HOW TO CONVERT to Byte the DataType.  Also dealing with nulls.
                // 3. DONE
                // 4. DONE

            }
            //Step 4 return table
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
                // set the item for this index. value will be of type Thing.
                Cells[rowindex, colindex] = value;
            }
        }
    }
}
