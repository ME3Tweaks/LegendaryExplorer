using ME3Explorer.Packages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for Bio2DAEditorWPF.xaml
    /// </summary>
    public partial class Bio2DAEditorWPF : ExportLoaderControl
    {
        private Bio2DA _table2da;

        public Bio2DA Table2DA
        {
            get
            {
                return _table2da;
            }
            private set
            {
                Debug.WriteLine("Setting 2da");
                SetProperty(ref _table2da, value);
            }
        }

        public IExportEntry CurrentlyLoadedExport { get; private set; }

        public Bio2DAEditorWPF()
        {
            InitializeComponent();
        }

        private void StartBio2DAScan()
        {
            //dataGridView1.Rows.Clear();
            //dataGridView1.Columns.Clear();
            Table2DA = new Bio2DA(CurrentlyLoadedExport);
            //Add columns
            /*for (int j = 0; j < table2da.columnNames.Count(); j++)
            {
                dataGridView1.Columns.Add(table2da.columnNames[j], table2da.columnNames[j]);
            }


            //Add rows
            for (int i = 0; i < table2da.rowNames.Count(); i++)
            {
                //defines row data. If you add columns, you need to add them here in order
                List<Object> rowData = new List<object>();
                for (int j = 0; j < table2da.columnNames.Count(); j++)
                {
                    Bio2DACell cell = table2da[i, j];
                    if (cell != null)
                    {
                        rowData.Add(cell.GetDisplayableValue());
                    }
                    else
                    {
                        rowData.Add(null);
                    }
                    //rowData.Add(table2da[i, j]);
                }
                dataGridView1.Rows.Add(rowData.ToArray());
                dataGridView1.Rows[dataGridView1.Rows.Count - 1].HeaderCell.Value = table2da.rowNames[i];
            }

            //Add row headers
            for (int i = 0; i < table2da.rowNames.Count(); i++)
            {
                dataGridView1.Rows[i].HeaderCell.Value = table2da.rowNames[i];
            }*/
        }

        public override bool CanParse(IExportEntry exportEntry)
        {
            return exportEntry.ClassName == "Bio2DA" || exportEntry.ClassName == "Bio2DANumberedRows";
        }

        public override void LoadExport(IExportEntry exportEntry)
        {
            CurrentlyLoadedExport = exportEntry;
            StartBio2DAScan();
        }

        public override void UnloadExport()
        {
            Table2DA = null;
            CurrentlyLoadedExport = null;
        }
    }

}
