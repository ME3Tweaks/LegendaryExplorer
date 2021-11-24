using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using PropertyChanged;

namespace LegendaryExplorerCore.Unreal.Classes
{
    [AddINotifyPropertyChangedInterface]
    public class Bio2DA
    {
        public bool IsIndexed = false;
        private List<string> _rowNames;
        private List<string> _columnNames;

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
        public List<string> RowNamesUI { get; private set; }


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

        public Bio2DA(ExportEntry export)
        {
            //Console.WriteLine("Loading " + export.ObjectName);
            Export = export;

            _rowNames = new List<string>();
            RowNamesUI = new List<string>();
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
                        _rowNames.Add(n.Value.ToString());
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

            _columnNames = new List<string>(binary.ColumnNames.Select(n => n.Name));
            mappedColumnNames = new CaseInsensitiveDictionary<int>(ColumnNames.Count);
            for (int i = 0; i < ColumnCount; i++)
            {
                mappedColumnNames[ColumnNames[i]] = i;
            }

            mappedRowNames = new CaseInsensitiveDictionary<int>(RowCount);
            for (int i = 0; i < RowCount; i++)
            {
                mappedRowNames[RowNames[i]] = i;
            }

            Cells = new Bio2DACell[RowCount, ColumnCount];
            foreach ((int index, Bio2DACell cell) in binary.Cells)
            {
                int row = index / ColumnCount;
                int col = index % ColumnCount;
                this[row, col] = cell.Type switch
                {
                    Bio2DACell.Bio2DADataType.TYPE_INT => new Bio2DACell(cell.IntValue),
                    Bio2DACell.Bio2DADataType.TYPE_NAME => new Bio2DACell(cell.NameValue, export.FileRef),
                    Bio2DACell.Bio2DADataType.TYPE_FLOAT => new Bio2DACell(cell.FloatValue),
                    // Null populated later
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            // Populate null cells with TYPE_NULL cells
            for (int row = 0; row < RowCount; row++)
            {
                for (int col = 0; col < ColumnCount; col++)
                {
                    if (this[row, col] == null)
                    {
                        this[row, col] = new Bio2DACell();
                    }
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

        public void Write2DAToExport(ExportEntry export = null)
        {
            var binary = new Bio2DABinary
            {
                ColumnNames = ColumnNames.Select(s => new NameReference(s)).ToList(),
                Cells = new OrderedMultiValueDictionary<int, Bio2DACell>(),
                Export = Export,
                IsIndexed = IsIndexed
            };

            for (int rowindex = 0; rowindex < RowCount; rowindex++)
            {
                for (int colindex = 0; colindex < ColumnCount; colindex++)
                {
                    Bio2DACell cell = Cells[rowindex, colindex];
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

            Property rowsProp = Export.ClassName switch
            {
                "Bio2DA" => new ArrayProperty<NameProperty>(RowNames.Select(n => new NameProperty(n)), "m_sRowLabel"),
                "Bio2DANumberedRows" => new ArrayProperty<IntProperty>(RowNames.Select(n => new IntProperty(int.Parse(n))), "m_lstRowNumbers"),
                _ => throw new ArgumentOutOfRangeException()
            };

            // This is so newly minted 2DA can be installed into an export.
            export ??= Export;
            export.WritePropertyAndBinary(rowsProp, binary);
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

        #region Setters / Accessors

        /// <summary>
        /// Maps column names to their indices for faster lookups
        /// </summary>
        private CaseInsensitiveDictionary<int> mappedColumnNames;

        /// <summary>
        /// Maps row names to their indices for faster lookups. For Bio2DA, lookups directly use the string value. For Bio2DANumberedRows, a .ToString() is run first on the lookup value so we don't have to maintain a different dictionary.
        /// </summary>
        private CaseInsensitiveDictionary<int> mappedRowNames;

        /// <summary>
        /// Adds a new row of the specified name to the table. If using Bio2DANumberedRows, pass a string version of an int. If a row already exists with this name, the index for that row is returned instead. Upon adding a new row, new TYPE_NULL cells are added
        /// </summary>
        /// <param name="rowName"></param>
        /// <returns></returns>
        public int AddRow(string rowName)
        {
            if (mappedRowNames.TryGetValue(rowName, out var existing))
            {
                return existing;
            }
            if (RowCount < Cells.GetLength(0))
            {
                Expand2DA(true);
            }
            mappedRowNames[rowName] = _rowNames.Count; // 0 based
            _rowNames.Add(rowName);
            return _rowNames.Count - 1;
        }

        /// <summary>
        /// Adds a new column of the specified name to the table. If a column already exists with this name, the index for that column is returned instead. Upon adding a new column, new TYPE_NULL cells are added.
        /// </summary>
        /// <param name="rowName"></param>
        /// <returns></returns>
        public int AddColumn(string columnName)
        {
            if (mappedColumnNames.TryGetValue(columnName, out var existing))
            {
                return existing;
            }
            if (ColumnCount < Cells.GetLength(1))
            {
                Expand2DA(false);
            }
            mappedColumnNames[columnName] = _columnNames.Count; // 0 based
            _columnNames.Add(columnName);
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
                    Cells[newRowCount - 1, i] = new Bio2DACell(); // -1 as it's 0 indexed
                }
            }
            else
            {
                // New Column. Populate all rows for this column
                for (int i = 0; i < RowCount; i++)
                {
                    Cells[i, newColCount - 1] = new Bio2DACell(); // -1 as it's 0 indexed
                }
            }
        }

        /// <summary>
        /// Accesses a 2DA by cell coordinates starting from the top left of 0,0.
        /// </summary>
        /// <param name="rowindex"></param>
        /// <param name="colindex"></param>
        /// <returns></returns>
        public Bio2DACell this[int rowindex, int colindex]
        {
            get => Cells[rowindex, colindex];
            set
            {
                // set the item for this index. value will be of type Bio2DACell.
                Cells[rowindex, colindex] = value;
            }
        }

        /// <summary>
        /// Accesses a 2DA by row name and the column index, starting from the left of 0. For Bio2DANumberedRows, you must pass the row name as a string version of the row number value.
        /// </summary>
        /// <param name="rowname">Row name (case insensitive)</param>
        /// <param name="colindex"></param>
        /// <returns></returns>
        public Bio2DACell this[string rowname, int colindex]
        {
            get
            {
                if (mappedRowNames.TryGetValue(rowname, out var rowIndex))
                {
                    return Cells[rowIndex, colindex];
                }

                throw new Exception($"A row named '{rowname}' was not found in the 2DA");
            }
            set
            {
                // set the item for this index. value will be of type Bio2DACell.
                if (mappedRowNames.TryGetValue(rowname, out var rowIndex))
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
        public Bio2DACell this[int rowindex, string columnname]
        {
            get
            {
                if (mappedColumnNames.TryGetValue(columnname, out var colindex))
                {
                    return Cells[rowindex, colindex];
                }

                throw new Exception($"A column named '{columnname}' was not found in the 2DA");
            }
            set
            {
                // set the item for this index. value will be of type Bio2DACell.
                if (mappedColumnNames.TryGetValue(columnname, out var colindex))
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
        public Bio2DACell this[string rowname, string columnname]
        {
            get
            {
                if (mappedColumnNames.TryGetValue(columnname, out var colindex))
                {
                    if (mappedRowNames.TryGetValue(rowname, out var rowindex))
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
                if (mappedColumnNames.TryGetValue(columnname, out var colindex))
                {
                    if (mappedRowNames.TryGetValue(rowname, out var rowindex))
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
    }
}
