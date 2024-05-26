using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using PropertyChanged;

namespace LegendaryExplorerCore.Unreal.Classes
{
    public enum Bio2DAMergeResult
    {
        /// <summary>
        /// The result is unknown
        /// </summary>
        Unknown,
        /// <summary>
        /// 2DA attempted to merge into itself
        /// </summary>
        ERROR_MergeIntoSelf,
        /// <summary>
        /// The destination table had columns already and the incoming table's columnset was different
        /// </summary>
        ERROR_DifferingColumnCount,
        /// <summary>
        /// Merge was successful
        /// </summary>
        OK
    }
    public class Bio2DA : INotifyPropertyChanged
    {
        public bool IsIndexed;
        private readonly List<string> _rowNames;
        private readonly List<string> _columnNames;

        /// <summary>
        /// The cell data for this 2DA. Indexing with integers will access this like an array, accessing with strings will access using the row and column names. Cells are mapped via [Row, Column].
        /// </summary>
        public Bio2DACell[,] Cells { get; set; }

        /// <summary>
        /// List of raw rownames
        /// </summary>
        public IReadOnlyList<string> RowNames => _rowNames;

        /// <summary>
        /// List of column names
        /// </summary>
        public IReadOnlyList<string> ColumnNames => _columnNames;

        /// <summary>
        /// Replaces _ with __ to avoid AccessKeys when rendering. This list is not updated when a row name changes in RowNames or a row is added.
        /// </summary>
        public List<string> RowNamesUI { get; }

        public int RowCount => RowNames?.Count ?? 0;

        public int ColumnCount => ColumnNames?.Count ?? 0;

        /// <summary>
        /// If this 2DA instance has been modified since it was loaded
        /// </summary>
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

        /// <summary>
        /// Export that was used to load this Bio2DA. Is null if an export was not used to load this 2DA
        /// </summary>
        public ExportEntry Export;

        /// <summary>
        /// Merges this 2DA table's data into the specified one, overwriting any same-name/indexed rows in the destination with data from ours. Returns a list of row indexes from THIS 2DA that were merged into the destination 2DA.
        /// </summary>
        /// <param name="destination2DA"></param>
        /// <exception cref="Exception">Any errors that occur </exception>
        public List<int> MergeInto(Bio2DA destination2DA, out Bio2DAMergeResult result, bool addMissingRows = true)
        {
            if (ReferenceEquals(this, destination2DA))
            {
                LECLog.Error("Cannot merge 2DA into itself!");
                result = Bio2DAMergeResult.ERROR_MergeIntoSelf;
                return null;
            }

            if (RowCount == 0)
            {
                result = Bio2DAMergeResult.OK;
                return new List<int>(0); // Nothing to merge
            }

            if (ColumnCount != destination2DA.ColumnCount)
            {
                if (destination2DA.RowCount > 0 || destination2DA.ColumnCount > 0)
                {
                    // If destination is not empty, do not use it
                    LECLog.Error("Cannot merge 2DAs: Column counts are not the same");
                    result = Bio2DAMergeResult.ERROR_DifferingColumnCount;
                    return null;
                }

                // Initializing from empty - merging existing 2DA into empty 2DA

                // Populate columns
                foreach (var v in ColumnNames)
                {
                    destination2DA.AddColumn(v);
                }
            }

            // Merge rows
            List<int> mergedRows = new List<int>();
            for (int localRowIdx = 0; localRowIdx < RowCount; localRowIdx++)
            {
                var rowName = RowNames[localRowIdx];
                int destRowIdx;
                if (!addMissingRows)
                {
                    if (!destination2DA.TryGetRowIndexByName(rowName, out destRowIdx))
                    {
                        continue;
                    }
                }
                else
                {
                    destRowIdx = destination2DA.AddRow(rowName);
                }

                mergedRows.Add(localRowIdx); // Mark this row as being merged
                // Debug.WriteLine($"Writing {destRowIdx}----------------------------");
                foreach (var colName in ColumnNames)
                {
                    // Debug.WriteLine($"Writing {rowIdx},{colName}");

                    var localCell = this[localRowIdx, colName];
                    switch (localCell.Type)
                    {
                        case Bio2DACell.Bio2DADataType.TYPE_FLOAT:
                            destination2DA[destRowIdx, colName].FloatValue = localCell.FloatValue;
                            break;
                        case Bio2DACell.Bio2DADataType.TYPE_INT:
                            destination2DA[destRowIdx, colName].IntValue = localCell.IntValue;
                            break;
                        case Bio2DACell.Bio2DADataType.TYPE_NAME:
                            destination2DA[destRowIdx, colName].NameValue = localCell.NameValue;
                            break;
                        case Bio2DACell.Bio2DADataType.TYPE_NULL:
                            destination2DA[destRowIdx, colName].Type = Bio2DACell.Bio2DADataType.TYPE_NULL;
                            break;
                        default:
                            Debugger.Break();
                            break;
                    }
                }
            }

            result = Bio2DAMergeResult.OK;
            return mergedRows;
        }

        /// <summary>
        /// Constructs a Bio2DA object from the specified export
        /// </summary>
        /// <param name="export"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public Bio2DA(ExportEntry export) : this()
        {
            //Console.WriteLine("Loading " + export.ObjectName);
            Export = export;

            // GET ROW NAMES
            if (export.ClassName == "Bio2DA")
            {
                const string rowLabelsVar = "m_sRowLabel";
                var props = export.GetProperty<ArrayProperty<NameProperty>>(rowLabelsVar);
                if (props != null)
                {
                    foreach (NameProperty n in props)
                    {
                        _rowNames.Add(n.Value.Instanced);
                        RowNamesUI.Add(n.Value.Instanced.Replace("_", "__"));
                    }
                }
                else
                {
                    Debug.WriteLine("Unable to find row names property!");
                }
            }
            else
            {
                var props = export.GetProperty<ArrayProperty<IntProperty>>("m_lstRowNumbers");//Bio2DANumberedRows
                if (props != null)
                {
                    foreach (IntProperty n in props)
                    {
                        _rowNames.Add(n.Value.ToString());
                        RowNamesUI.Add(n.Value.ToString());
                    }
                }
                else
                {
                    Debug.WriteLine("Unable to find row names property (m_lstRowNumbers)!");
                }
            }

            var binary = export.GetBinaryData<Bio2DABinary>();

            for (int i = 0; i < binary.ColumnNames.Count; i++)
            {
                _columnNames.Add(binary.ColumnNames[i]);
                mappedColumnNames[binary.ColumnNames[i]] = i;
            }

            for (int i = 0; i < RowCount; i++)
            {
                mappedRowNames[RowNames[i]] = i;
            }

            Cells = new Bio2DACell[RowCount, ColumnCount];
            if (ColumnCount > 0) // Prevents division by zero
            {
                foreach ((int index, Bio2DACell cell) in binary.Cells)
                {
                    int row = index / ColumnCount;
                    int col = index % ColumnCount;
                    this[row, col] = cell.Type switch
                    {
                        Bio2DACell.Bio2DADataType.TYPE_INT => new Bio2DACell(cell.IntValue) { package = export.FileRef },
                        Bio2DACell.Bio2DADataType.TYPE_NAME => new Bio2DACell(cell.NameValue, export.FileRef) { package = export.FileRef },
                        Bio2DACell.Bio2DADataType.TYPE_FLOAT => new Bio2DACell(cell.FloatValue) { package = export.FileRef },
                        // Null populated later
                        _ => throw new ArgumentOutOfRangeException()
                    };
                }
            }

            // Populate null cells with TYPE_NULL cells
            for (int row = 0; row < RowCount; row++)
            {
                for (int col = 0; col < ColumnCount; col++)
                {
                    this[row, col] ??= new Bio2DACell { package = export.FileRef };
                }
            }

            IsIndexed = binary.IsIndexed;
        }

        /// <summary>
        /// Initializes a blank Bio2DA. Cells is not initialized, the caller must set up this Bio2DA.
        /// </summary>
        public Bio2DA()
        {
            _columnNames = new List<string>();
            _rowNames = new List<string>();
            mappedRowNames = new CaseInsensitiveDictionary<int>();
            mappedColumnNames = new CaseInsensitiveDictionary<int>();
            RowNamesUI = new List<string>();
            Cells = new Bio2DACell[0, 0]; // Changed to initialize variable 11/12/2023 for LE1R merge code
        }

        /// <summary>
        /// Marks all cells as not modified
        /// </summary>
        public void MarkAsUnmodified()
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

        public void Write2DAToExport(ExportEntry exportToWriteTo = null)
        {
            var binary = new Bio2DABinary
            {
                ColumnNames = ColumnNames.Select(s => new NameReference(s)).ToList(),
                Cells = new(),
                Export = Export,
                IsIndexed = IsIndexed
            };

            for (int rowindex = 0; rowindex < RowCount; rowindex++)
            {
                for (int colindex = 0; colindex < ColumnCount; colindex++)
                {
                    Bio2DACell cell = Cells[rowindex, colindex];
                    //if (cell == null || cell.Type == Bio2DACell.Bio2DADataType.TYPE_NULL)
                    //    Debugger.Break();
                    if (cell != null && cell.Type != Bio2DACell.Bio2DADataType.TYPE_NULL)
                    {
                        int index = (rowindex * ColumnCount) + colindex;
                        binary.Cells.Add(index, cell.Type switch
                        {
                            Bio2DACell.Bio2DADataType.TYPE_INT => new Bio2DACell { IntValue = cell.IntValue, Type = Bio2DACell.Bio2DADataType.TYPE_INT },
                            Bio2DACell.Bio2DADataType.TYPE_NAME => new Bio2DACell { NameValue = cell.NameValue, Type = Bio2DACell.Bio2DADataType.TYPE_NAME },
                            Bio2DACell.Bio2DADataType.TYPE_FLOAT => new Bio2DACell { FloatValue = cell.FloatValue, Type = Bio2DACell.Bio2DADataType.TYPE_FLOAT },
                            // NULL IS NOT ADDED
                            _ => throw new ArgumentOutOfRangeException()
                        });
                    }
                }
            }

            // This is so newly minted 2DA can be installed into an export.
            exportToWriteTo ??= Export; // 11/12/2023 fix backwards assignment - LE1R

            if (RowNames.Count > 0)
            {
                Property rowsProp = exportToWriteTo.ClassName switch // 11/12/2023 fix writing to wrong export (used this object's Export not passed in) - LE1R
                {
                    "Bio2DA" => new ArrayProperty<NameProperty>(RowNames.Select(n => new NameProperty(n)), "m_sRowLabel"),
                    "Bio2DANumberedRows" => new ArrayProperty<IntProperty>(RowNames.Select(n => new IntProperty(int.Parse(n))), "m_lstRowNumbers"),
                    _ => throw new ArgumentOutOfRangeException()
                };
                exportToWriteTo.WritePropertyAndBinary(rowsProp, binary);
            }
            else
            {
                exportToWriteTo.RemoveProperty("m_sRowLabel"); // No rows.
                exportToWriteTo.RemoveProperty("m_lstRowNumbers"); // No rows.
                exportToWriteTo.WriteBinary(binary);
            }
        }

        internal string GetColumnNameByIndex(int columnIndex)
        {
            if (columnIndex < ColumnNames.Count && columnIndex >= 0)
            {
                return ColumnNames[columnIndex];
            }
            return null;
        }

        public int GetColumnIndexByName(string columnName)
        {
            return mappedColumnNames[columnName];
        }

        public int GetRowIndexByName(string rowname)
        {
            return mappedRowNames[rowname];
        }

        public bool TryGetRowIndexByName(string rowname, out int rowIndex)
        {
            return mappedRowNames.TryGetValue(rowname, out rowIndex);
        }

        #region Setters / Accessors

        /// <summary>
        /// Maps column names to their indices for faster lookups
        /// </summary>
        private readonly CaseInsensitiveDictionary<int> mappedColumnNames;

        /// <summary>
        /// Maps row names to their indices for faster lookups. For Bio2DA, lookups directly use the string value. For Bio2DANumberedRows, a .ToString() is run first on the lookup value so we don't have to maintain a different dictionary.
        /// </summary>
        private readonly CaseInsensitiveDictionary<int> mappedRowNames;

        /// <summary>
        /// Adds a new row of the specified name to the table. If using Bio2DANumberedRows, pass a string version of an int. If a row already exists with this name, the index for that row is returned instead. Upon adding a new row, new TYPE_NULL cells are added
        /// </summary>
        /// <param name="rowName"></param>
        /// <returns>The row index created, or found if existing</returns>
        public int AddRow(string rowName)
        {
            if (mappedRowNames.TryGetValue(rowName, out int existing))
            {
                return existing;
            }
            if (RowCount == Cells.GetLength(0))
            {
                Expand2DA(true);
            }
            mappedRowNames[rowName] = _rowNames.Count; // 0 based
            _rowNames.Add(rowName);

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowNames)));

            return _rowNames.Count - 1;
        }

        /// <summary>
        /// Adds a new column of the specified name to the table. If a column already exists with this name, the index for that column is returned instead. Upon adding a new column, new TYPE_NULL cells are added.
        /// </summary>
        /// <param name="columnName"></param>
        /// <returns></returns>
        public int AddColumn(string columnName)
        {
            if (mappedColumnNames.TryGetValue(columnName, out int existing))
            {
                return existing;
            }
            if (ColumnCount == Cells.GetLength(1))
            {
                Expand2DA(false);
            }
            mappedColumnNames[columnName] = _columnNames.Count; // 0 based
            _columnNames.Add(columnName);

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColumnNames)));

            return _columnNames.Count - 1;
        }

        /// <summary>
        /// Expands the 2DA by copying the Bio2DA cells into a new table. 
        /// </summary>
        /// <param name="expandVertically"></param>
        private void Expand2DA(bool expandVertically)
        {
            // Called before column or row was added.

            int newRowCount = RowCount;
            int newColCount = ColumnCount;
            if (expandVertically)
                newRowCount++;
            else
                newColCount++;

            var oldCells = Cells;
            Cells = new Bio2DACell[newRowCount, newColCount];
            for (int i = 0; i < RowCount; i++)
            {
                for (int j = 0; j < ColumnCount; j++)
                {
                    Cells[i, j] = oldCells[i, j];
                }
            }

            if (expandVertically)
            {
                // New row. Populate all columns in this row
                for (int i = 0; i < ColumnCount; i++)
                {
                    Cells[newRowCount - 1, i] = new Bio2DACell { package = Export?.FileRef }; // -1 as it's 0 indexed
                }
            }
            else
            {
                // New Column. Populate all rows for this column
                for (int i = 0; i < RowCount; i++)
                {
                    Cells[i, newColCount - 1] = new Bio2DACell { package = Export?.FileRef }; ; // -1 as it's 0 indexed
                }
            }
        }

        /// <summary>
        /// Removes all rows from this 2DA table
        /// </summary>
        public void ClearRows()
        {
            _rowNames.Clear();
        }

        /// <summary>
        /// Accesses a 2DA by cell coordinates starting from the top left of 0,0.
        /// </summary>
        /// <param name="rowindex"></param>
        /// <param name="colindex"></param>
        /// <returns></returns>
        [SuppressPropertyChangedWarnings]
        public Bio2DACell this[int rowindex, int colindex]
        {
            get => Cells[rowindex, colindex];
            // set the item for this index. value will be of type Bio2DACell.
            set => Cells[rowindex, colindex] = value;
        }

        /// <summary>
        /// Accesses a 2DA by row name and the column index, starting from the left of 0. For Bio2DANumberedRows, you must pass the row name as a string version of the row number value.
        /// </summary>
        /// <param name="rowname">Row name (case insensitive)</param>
        /// <param name="colindex"></param>
        /// <returns></returns>
        [SuppressPropertyChangedWarnings]
        public Bio2DACell this[string rowname, int colindex]
        {
            get
            {
                if (mappedRowNames.TryGetValue(rowname, out int rowIndex))
                {
                    return Cells[rowIndex, colindex];
                }

                throw new Exception($"A row named '{rowname}' was not found in the 2DA");
            }
            set
            {
                // set the item for this index. value will be of type Bio2DACell.
                if (mappedRowNames.TryGetValue(rowname, out int rowIndex))
                {
                    Cells[rowIndex, colindex] = value;
                }
                else
                {
                    throw new Exception($"A row named '{rowname}' was not found in the 2DA");
                }
            }
        }

        /// <summary>
        /// Accesses a 2DA by row index and a column name, starting from the top of 0.
        /// </summary>
        /// <param name="rowindex"></param>
        /// <param name="columnname">Column name (case insenitive)</param>
        /// <returns></returns>
        [SuppressPropertyChangedWarnings]
        public Bio2DACell this[int rowindex, string columnname]
        {
            get
            {
                if (mappedColumnNames.TryGetValue(columnname, out int colindex))
                {
                    return Cells[rowindex, colindex];
                }

                throw new Exception($"A column named '{columnname}' was not found in the 2DA");
            }
            set
            {
                // set the item for this index. value will be of type Bio2DACell.
                if (mappedColumnNames.TryGetValue(columnname, out int colindex))
                {
                    Cells[rowindex, colindex] = value;
                }
                else
                {
                    throw new Exception($"A column named '{columnname}' was not found in the 2DA");
                }
            }
        }

        /// <summary>
        /// Accesses a 2DA by row name and a column name. For Bio2DANumberedRows, you must pass the row name as a string version of the row number value.
        /// </summary>
        /// <param name="rowname">Row name (case insensitive). For Bio2DANumberedRows, pass the value of the row name as a string.</param>
        /// <param name="columnname">Column name (case insenitive)</param>
        /// <returns></returns>
        [SuppressPropertyChangedWarnings]
        public Bio2DACell this[string rowname, string columnname]
        {
            get
            {
                if (mappedColumnNames.TryGetValue(columnname, out int colindex))
                {
                    if (mappedRowNames.TryGetValue(rowname, out int rowindex))
                    {
                        return Cells[rowindex, colindex];
                    }
                    throw new Exception($"A row named '{rowname}' was not found in the 2DA");
                }
                throw new Exception($"A column named '{columnname}' was not found in the 2DA");
            }
            set
            {
                // set the item for this index. value will be of type Bio2DACell.
                if (mappedColumnNames.TryGetValue(columnname, out int colindex))
                {
                    if (mappedRowNames.TryGetValue(rowname, out int rowindex))
                    {
                        Cells[rowindex, colindex] = value;
                    }
                    else
                    {
                        throw new Exception($"A row named '{rowname}' was not found in the 2DA");
                    }
                }
                else
                {
                    throw new Exception($"A column named '{columnname}' was not found in the 2DA");
                }
            }
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        public bool TryGetColumnIndexByName(string colname, out int colIndex)
        {
            return mappedColumnNames.TryGetValue(colname, out colIndex);
        }
    }
}
