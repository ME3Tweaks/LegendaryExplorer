using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Classes;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    public static class Bio2DAExtended
    {
        public static void Write2DAToCSV(this Bio2DA twoDA, string path)
        {
            using StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(true));

            writer.WriteLine(string.Join(",", new[] { "" }.Concat(twoDA.ColumnNames).Select(EscapeCsvField)));

            // Write row labels in the first column, followed by the 2DA cell values.
            for (int rowindex = 0; rowindex < twoDA.RowCount; rowindex++)
            {
                var fields = new List<string>(twoDA.ColumnCount + 1)
                {
                    twoDA.RowNames[rowindex]
                };

                for (int colindex = 0; colindex < twoDA.ColumnCount; colindex++)
                {
                    fields.Add(twoDA.Cells[rowindex, colindex]?.DisplayableValue ?? "");
                }

                writer.WriteLine(string.Join(",", fields.Select(EscapeCsvField)));
            }
        }

        public static Bio2DA ReadCSVTo2DA(ExportEntry export, string filename)
        {
            var csvRows = ReadCsvRows(filename);
            if (csvRows.Count == 0)
            {
                MessageBox.Show("CSV file is empty");
                return null;
            }

            //STEP 1 Clear existing data
            Bio2DA bio2da = new Bio2DA
            {
                Export = export
            };

            //STEP 2 Read columns and row names

            //Column names
            var colNames = new List<string>();
            var rowNames = new List<string>();
            for (int columnIndex = 1; columnIndex < csvRows[0].Count; columnIndex++)
            {
                colNames.Add(csvRows[0][columnIndex]);
            }

            //Row names 
            for (int rowIndex = 1; rowIndex < csvRows.Count; rowIndex++)
            {
                if (csvRows[rowIndex].Count > 0)
                {
                    rowNames.Add(csvRows[rowIndex][0]);
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
            //indices here are CSV based. Subtract one to get Bio2DA based.
            for (int rowIndex = 1; rowIndex < bio2da.RowCount + 1; rowIndex++)
            {
                List<string> csvRow = csvRows[rowIndex];
                for (int columnIndex = 1; columnIndex < bio2da.ColumnCount + 1; columnIndex++)
                {
                    string csvCellContents = columnIndex < csvRow.Count ? csvRow[columnIndex] : "";
                    if (!string.IsNullOrEmpty(csvCellContents))
                    {
                        Bio2DACell newCell;
                        if (int.TryParse(csvCellContents, out int intVal))
                        {
                            newCell = new Bio2DACell(intVal) { package = export.FileRef };
                        }
                        else if (float.TryParse(csvCellContents, out float floatVal))
                        {
                            newCell = new Bio2DACell(floatVal) { package = export.FileRef };
                        }
                        else
                        {
                            newCell = new Bio2DACell(csvCellContents, export.FileRef) { package = export.FileRef };
                        }
                        bio2da[rowIndex - 1, columnIndex - 1] = newCell;
                    }
                    else
                    {
                        bio2da.IsIndexed = true;  //Null cells = indexing
                    }
                }
            }
            return bio2da;
        }

        private static string EscapeCsvField(string field)
        {
            field ??= "";
            return field.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
                       ? $"\"{field.Replace("\"", "\"\"")}\""
                       : field;
        }

        private static List<List<string>> ReadCsvRows(string filename)
        {
            string csv = File.ReadAllText(filename);
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                else
                {
                    switch (c)
                    {
                        case '"':
                            inQuotes = true;
                            break;
                        case ',':
                            row.Add(field.ToString());
                            field.Clear();
                            break;
                        case '\r':
                            if (i + 1 < csv.Length && csv[i + 1] == '\n')
                            {
                                i++;
                            }
                            row.Add(field.ToString());
                            field.Clear();
                            rows.Add(row);
                            row = new List<string>();
                            break;
                        case '\n':
                            row.Add(field.ToString());
                            field.Clear();
                            rows.Add(row);
                            row = new List<string>();
                            break;
                        default:
                            field.Append(c);
                            break;
                    }
                }
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }

            return rows;
        }
    }
}
