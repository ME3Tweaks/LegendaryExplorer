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
            Console.WriteLine("Loading " + export.ObjectName);
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
                string rowLabelsVar = "m_lstRowNumbers"; //Bio2DANumberedRows
                var props = export.GetProperty<ArrayProperty<IntProperty>>(rowLabelsVar);
                if (props != null)
                {
                    foreach (IntProperty n in props)
                    {
                        RowNames.Add(n.Value.ToString());
                    }
                }
                else
                {
                    Console.WriteLine("Unable to find row names property!");
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
                        int dataSize = dataType == Bio2DACell.TYPE_NAME ? 8 : 4;
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
                    int dataSize = dataType == Bio2DACell.TYPE_NAME ? 8 : 4;
                    curroffset++;
                    byte[] celldata = new byte[dataSize];
                    Buffer.BlockCopy(data, curroffset, celldata, 0, dataSize);
                    Bio2DACell cell = new Bio2DACell(pcc, curroffset, dataType, celldata);
                    Cells[row, col] = cell;
                    CellCount++;
                    curroffset += dataSize;
                }
            }
            Console.WriteLine("Finished loading " + export.ObjectName);
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
                            string tlkLookup = ME1Explorer.TlkManager.GetStringById(stringId);
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
                                Console.WriteLine("THIS SHOULDN'T OCCUR!");
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
                byte[] propertydata = export.Data.Take(propsEnd).ToArray();
                var newExportData = new byte[propertydata.Length + binarydata.Length];
                propertydata.CopyTo(newExportData, 0);
                binarydata.CopyTo(newExportData, propertydata.Length);
                //Console.WriteLine("Old data size: " + export.Data.Length);
                //Console.WriteLine("NEw data size: " + newExportData.Length);
                if (export.Data.Length != newExportData.Length)
                {
                    Console.WriteLine("FILES ARE WRONG SIZE");
                    Debugger.Break();
                }
                export.Data = newExportData;
            }
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
