using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Unreal.Classes
{
    public class Bio2DA
    {
        public bool IsIndexed = false;

        public Bio2DACell[,] Cells { get; set; }
        /// <summary>
        /// List of raw rownames
        /// </summary>
        public List<string> RowNames { get; set; }
        /// <summary>
        /// Replaces _ with __ to avoid AccessKeys when rendering 
        /// </summary>
        public List<string> RowNamesUI { get; private set; }
        /// <summary>
        /// List of column names
        /// </summary>
        public List<string> ColumnNames { get; set; }

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

        /// <summary>
        /// Marks all cells as not modified
        /// </summary>
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

        public Bio2DACell this[int rowindex, int colindex]
        {
            get => Cells[rowindex, colindex];
            set
            {
                // set the item for this index. value will be of type Bio2DACell.
                Cells[rowindex, colindex] = value;
            }
        }
    }
}
