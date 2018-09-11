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
    public partial class Bio2DAEditorWPF : UserControl, INotifyPropertyChanged
    {
        private Bio2DA _table2da;
        public static readonly string[] ParsableBinaryClasses = { "Bio2DA", "Bio2DANumberedRows" };

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

        public void LoadExport(IExportEntry export)
        {
            CurrentlyLoadedExport = export;
            StartBio2DAScan();
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

        #region Property Changed Notification
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Notifies listeners when given property is updated.
        /// </summary>
        /// <param name="propertyname">Name of property to give notification for. If called in property, argument can be ignored as it will be default.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        internal void UnloadExport()
        {
            CurrentlyLoadedExport = null;
        }
        #endregion
    }

}
