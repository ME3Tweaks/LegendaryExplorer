using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using CsvHelper;
using System.Text.RegularExpressions;

namespace ME3Explorer.PlotVarDB
{
    public partial class PlotVarDB : Form
    {
        public const int V2PLUS_MAGIC = 0x504C4F54; //files that start with this are V2 and above. That is the only purpose of this value. Also easter egg ;)
        public const int VERSION_2 = 2;
        public const int CURRENT_VERSION = VERSION_2;

        //Constants for the column indexes - allows easy adding and accessing of columns by name
        public const int COL_PLOTID = 0;
        public const int COL_VARTYPE = 1;
        public const int COL_GAME = 2;
        public const int COL_CATEGORY1 = 3;
        public const int COL_CATEGORY2 = 4;
        public const int COL_STATE = 5;
        public const int COL_BROKEN = 6;
        public const int COL_ME2SPEC = 7;
        public const int COL_ME3SPEC = 8;
        public const int COL_NOTES = 9;

        //.db vartype value mapping
        public const int VARTYPE_BOOL = 0;
        public const int VARTYPE_FLOAT = 2;
        public const int VARTYPE_INTEGER = 1;

        //Game value mapping
        public const int GAME_ME1 = 1;
        public const int GAME_ME2 = 2;
        public const int GAME_ME3 = 3;

        //List of loaded plotvarentries. Commit the table to sync this with the displayed data.
        public List<PlotVarEntry> entries;

        //Turns on/off cell validation. When refreshing the table it should be off because while the data is loading
        //values may be invalid until fully loaded. Turn on once loaded.
        private bool validating = true;

        public class PlotVarEntry
        {
            public int id { get; set; }
            public int type { get; set; } // 0 = bool, 1 = int, 2 = float
            public int game { get; set; } //0 = me1, 2 = me2, 3 = me3. Use consts
            public string category1 { get; set; }
            public string category2 { get; set; }
            public string state { get; set; }
            public bool broken { get; set; }
            public int me2id { get; set; }
            public int me3id { get; set; }
            public string notes { get; set; }
        }

        public PlotVarDB()
        {
            InitializeComponent();
            this.plotVarTable.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        }

        private void PlotVarDB_Load(object sender, EventArgs e)
        {
            this.plotVarTable.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            entries = new List<PlotVarEntry>();
            string appPath = Application.StartupPath;
            if (File.Exists(appPath + @"\plot.db"))
            {
                loadDatabase(appPath + @"\plot.db");
            }
            else
            {
                status.Text = "Open DB from the file menu or start entering data to start a new one";
            }
        }

        public string TypeToString(int type)
        {
            string res = "";
            switch (type)
            {
                case 0:
                    res = "Boolean";
                    break;
                case 1:
                    res = "Integer";
                    break;
                case 2:
                    res = "Float";
                    break;
            }
            return res;
        }

        public void RefreshTable()
        {
            validating = false;
            plotVarTable.ClearSelection();
            plotVarTable.Rows[0].Selected = true;
            while (plotVarTable.Rows[0] != null && !plotVarTable.Rows[0].IsNewRow)
            {
                plotVarTable.Rows.Remove(plotVarTable.Rows[0]);
            } //avoids a bug with clear()
            validating = true;

            foreach (PlotVarEntry p in entries)
            {
                //defines row data. If you add columns, you need to add them here in order
                object[] row = new object[] { p.id.ToString(), TypeToString(p.type), GameToString(p.game), p.category1, p.category2, p.state, p.broken, p.me2id > 0 ? p.me2id.ToString() : "", p.me3id > 0 ? p.me3id.ToString() : "", p.notes };
                plotVarTable.Rows.Add(row);
            }
            status.Text = "Number of entries: " + (plotVarTable.Rows.Count - 1);
        }

        private object GameToString(int game)
        {
            switch (game)
            {
                case GAME_ME1:
                    return "Mass Effect";
                case GAME_ME2:
                    return "Mass Effect 2";
                case GAME_ME3:
                    return "Mass Effect 3";
                default:
                    return "";
            }
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            Search();
        }

        public void Search()
        {
            string s = toolStripTextBox1.Text;
            if (s.Length == 0)
                return;
            s = s.ToLower();
            int n = plotVarTable.CurrentRow.Index;
            if (n >= plotVarTable.NewRowIndex)
            {
                //wrap search
                n = 0;
            }

            List<int> searchColumns = new List<int>();
            //add search columns to this.
            searchColumns.Add(COL_PLOTID);
            searchColumns.Add(COL_CATEGORY1);
            searchColumns.Add(COL_CATEGORY2);
            searchColumns.Add(COL_STATE);
            searchColumns.Add(COL_ME2SPEC);
            searchColumns.Add(COL_ME2SPEC);
            searchColumns.Add(COL_NOTES);

            for (int i = n + 1; i < plotVarTable.Rows.Count; i++)
            {
                DataGridViewCellCollection row = plotVarTable.Rows[i].Cells;
                foreach (int col in searchColumns)
                {
                    object cellValue = row[col].Value;
                    if (cellValue != null && row[col].Value.ToString().ToLower().Contains(s))
                    {
                        plotVarTable.ClearSelection();
                        //plotVarTable.Rows[i].Selected = true;
                        plotVarTable.CurrentCell = plotVarTable[col, i];
                        status.Text = "";
                        return;
                    }
                }
            }
            status.Text = "No matches from current row to end of database.";
        }

        private void toolStripTextBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
                Search();
        }

        private void newDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            entries = new List<PlotVarEntry>();
            RefreshTable();
        }

        private void saveDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            commitTable();
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.db|*.db";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                BitConverter.IsLittleEndian = true;

                //Header
                fs.Write(BitConverter.GetBytes(V2PLUS_MAGIC), 0, 4);
                fs.Write(BitConverter.GetBytes(CURRENT_VERSION), 0, 4);

                //Start of data
                fs.Write(BitConverter.GetBytes(entries.Count), 0, 4);
                foreach (PlotVarEntry p in entries)
                {
                    fs.Write(BitConverter.GetBytes(p.id), 0, 4);
                    fs.Write(BitConverter.GetBytes(p.type), 0, 4);
                    fs.Write(BitConverter.GetBytes(p.game), 0, 4);
                    WriteString(fs, p.category1 ?? "");
                    WriteString(fs, p.state ?? "");
                    fs.WriteByte(BitConverter.GetBytes(p.broken)[0]); //gets 1 byte true/false
                    fs.Write(BitConverter.GetBytes(p.me2id), 0, 4);
                    fs.Write(BitConverter.GetBytes(p.me3id), 0, 4);
                    WriteString(fs, p.notes ?? "");
                }
                fs.Close();
                status.Text = "Saved DB to " + d.FileName;
            }
        }

        private void commitTable()
        {
            //Commit the current edit to the table
            if (plotVarTable.IsCurrentCellDirty)
            {
                plotVarTable.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }

            List<PlotVarEntry> commitingEntries = new List<PlotVarEntry>();
            foreach (DataGridViewRow row in plotVarTable.Rows)
            {
                if ((string)row.Cells[COL_PLOTID].Value != null && ((string)row.Cells[COL_PLOTID].Value).Trim() != "")
                {
                    PlotVarEntry pve = new PlotVarEntry();
                    pve.id = Convert.ToInt32((string)row.Cells[COL_PLOTID].Value);
                    pve.type = StringToType((string)row.Cells[COL_VARTYPE].Value);
                    pve.game = StringToGame((string)row.Cells[COL_GAME].Value);
                    pve.category1 = row.Cells[COL_CATEGORY1].Value != null ? row.Cells[COL_CATEGORY1].Value.ToString() : "";
                    pve.state = row.Cells[COL_STATE].Value != null ? row.Cells[COL_STATE].Value.ToString() : "";
                    object broken = row.Cells[COL_BROKEN].Value;
                    pve.broken = Convert.ToBoolean(broken);
                    pve.me2id = row.Cells[COL_ME2SPEC].Value != null && !row.Cells[COL_ME2SPEC].Value.Equals("") ? Convert.ToInt32(row.Cells[COL_ME2SPEC].Value.ToString()) : 0;
                    pve.me3id = row.Cells[COL_ME3SPEC].Value != null && !row.Cells[COL_ME3SPEC].Value.Equals("") ? Convert.ToInt32(row.Cells[COL_ME3SPEC].Value.ToString()) : 0;
                    pve.notes = row.Cells[COL_NOTES].Value != null ? row.Cells[COL_NOTES].Value.ToString() : "";
                    commitingEntries.Add(pve);
                }
            }

            entries = commitingEntries;
        }

        private int StringToGame(string value)
        {
            switch (value)
            {
                case "Mass Effect":
                    return GAME_ME1;
                case "Mass Effect 2":
                    return GAME_ME2;
                case "Mass Effect 3":
                    return GAME_ME3;
                default:
                    return 3; //unknown.
            }
        }

        private void plotVarTable_CellValidating(object sender,
                                           DataGridViewCellValidatingEventArgs e)
        {
            if (!validating)
            {
                return;
            }
            if ((e.ColumnIndex == COL_PLOTID || e.ColumnIndex == COL_ME2SPEC || e.ColumnIndex == COL_ME3SPEC) && e.RowIndex < plotVarTable.NewRowIndex && e.FormattedValue != null) // Plot ID
            {
                if (e.ColumnIndex == COL_PLOTID && e.FormattedValue.Equals("") && e.RowIndex != plotVarTable.NewRowIndex)
                {
                    e.Cancel = true;
                    status.Text = "Invalid value. Value cannot be empty.";
                }

                if (e.ColumnIndex == COL_ME2SPEC || e.ColumnIndex == COL_ME3SPEC && e.FormattedValue.Equals(""))
                {
                    status.Text = "";
                    return;
                }
                int i;
                if (!int.TryParse(Convert.ToString(e.FormattedValue), out i))
                {
                    e.Cancel = true;
                    status.Text = "Invalid value. Must be an integer.";
                }
                else
                {
                    // the input is numeric 
                    //we're OK
                    status.Text = "";

                }
            }
        }

        private int StringToType(string value)
        {
            switch (value)
            {
                case "Boolean":
                    return VARTYPE_BOOL;
                case "Integer":
                    return VARTYPE_INTEGER;
                case "Float":
                    return VARTYPE_FLOAT;
                default:
                    return 3;
            }
        }

        private string TypeToCSVType(int value)
        {
            switch (value)
            {
                case 0:
                    return "B";
                case 1:
                    return "I";
                case 2:
                    return "F";
                default:
                    return "";
            }
        }

        public void WriteString(FileStream fs, string s)
        {
            fs.Write(BitConverter.GetBytes((int)s.Length), 0, 4);
            fs.Write(GetBytes(s), 0, s.Length);
        }

        public string ReadString(FileStream fs)
        {
            string s = "";
            byte[] buff = new byte[4];
            for (int i = 0; i < 4; i++)
                buff[i] = (byte)fs.ReadByte();
            int count = BitConverter.ToInt32(buff, 0);
            buff = new byte[count];
            for (int i = 0; i < count; i++)
                buff[i] = (byte)fs.ReadByte();
            s = GetString(buff);
            return s;
        }

        public byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length];
            for (int i = 0; i < str.Length; i++)
                bytes[i] = (byte)str[i];
            return bytes;
        }

        public string GetString(byte[] bytes)
        {
            string s = "";
            for (int i = 0; i < bytes.Length; i++)
                s += (char)bytes[i];
            return s;
        }

        public int ReadInt(FileStream fs)
        {
            int res = 0;
            byte[] buff = new byte[4];
            fs.Read(buff, 0, 4);
            res = BitConverter.ToInt32(buff, 0);
            return res;
        }

        public bool ReadBool(FileStream fs)
        {
            byte[] buff = new byte[1];
            fs.Read(buff, 0, 1);
            return buff[0] != 0;
        }

        private void loadDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.db|*.db";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                loadDatabase(d.FileName);
            }
        }

        private void loadDatabase(string fileName)
        {
            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            BitConverter.IsLittleEndian = true;
            int magic = ReadInt(fs);
            if (magic != V2PLUS_MAGIC)
            {
                //Pre v748
                fs.Seek(0, SeekOrigin.Begin);
                entries = readVersion1DB(fs);
            }
            else
            {
                //748+
                int version = ReadInt(fs);
                switch (version)
                {
                    case VERSION_2:
                        entries = readVersion2DB(fs);
                        break;
                        //add a case here if you are adding/updating the format of the DB.
                }
            }
            fs.Close();
            RefreshTable();
        }

        /// <summary>
        /// Reads a v2 type DB (r748), by FemShep. Returns list of read entries.
        /// Contains the following columns:
        /// Plot ID
        /// Type (as int)
        /// Game (as int)
        /// Category1 (as string)
        /// Category2 (as string)
        /// State/Value (as string)
        /// Broken (as bool (stored as byte))
        /// ME2 ID (as int)
        /// ME3 ID (as int)
        /// Notes (as string)
        /// </summary>
        /// <param name="fs">Filestream advanced past the version indicator</param>
        private List<PlotVarEntry> readVersion2DB(FileStream fs)
        {
            PlotVarEntry p;
            entries = new List<PlotVarEntry>();

            int count = ReadInt(fs);
            for (int i = 0; i < count; i++)
            {
                p = new PlotVarEntry();
                p.id = ReadInt(fs);
                p.type = ReadInt(fs);
                p.game = ReadInt(fs);
                p.category1 = ReadString(fs);
                p.state = ReadString(fs);
                p.broken = ReadBool(fs);
                p.me2id = ReadInt(fs);
                p.me3id = ReadInt(fs);
                p.notes = ReadString(fs);
                entries.Add(p);
            }

            return entries;
        }

        /// <summary>
        /// Reads the old style DB files from Pre-r748.
        /// Contains the following columns:
        /// ID (as int)
        /// Type (as int)
        /// Desc (as string)
        /// This file format has a different layout than v2 and up.
        /// </summary>
        /// <param name="fs">Filestream at the start of a v1 file</param>
        /// <returns>List of imported entries. Desc is mapped to the state column.</returns>
        private List<PlotVarEntry> readVersion1DB(FileStream fs)
        {
            PlotVarEntry p;
            entries = new List<PlotVarEntry>();
            int count = ReadInt(fs);
            for (int i = 0; i < count; i++)
            {
                p = new PlotVarEntry();
                p.id = ReadInt(fs);
                p.type = ReadInt(fs);
                string desc = ReadString(fs);
                p.category1 = Regex.Match(desc, @"\[(.*?)\]").Groups[1].Value;
                p.state = desc.Substring(p.category1.Length > 0 ? p.category1.Length + 2 : 0).Trim();
                p.game = GAME_ME1;
                p.broken = false;
                entries.Add(p);
            }
            count = ReadInt(fs);
            for (int i = 0; i < count; i++)
            {
                p = new PlotVarEntry();
                p.id = ReadInt(fs);
                p.type = ReadInt(fs);
                string desc = ReadString(fs);
                p.category1 = Regex.Match(desc, @"\[(.*?)\]").Groups[1].Value;
                p.state = desc.Substring(p.category1.Length > 0 ? p.category1.Length + 2 : 0).Trim();
                p.game = GAME_ME2;
                p.broken = false;
                entries.Add(p);
            }
            count = ReadInt(fs);
            for (int i = 0; i < count; i++)
            {
                p = new PlotVarEntry();
                p.id = ReadInt(fs);
                p.type = ReadInt(fs);
                string desc = ReadString(fs);
                p.category1 = Regex.Match(desc, @"\[(.*?)\]").Groups[1].Value;
                p.state = desc.Substring(p.category1.Length > 0 ? p.category1.Length + 2 : 0).Trim();
                p.game = GAME_ME3;
                p.broken = false;
                entries.Add(p);
            }
            return entries;
        }

        /// <summary>
        /// Handles the delete key press on a row
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void plotVarTable_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && e.Modifiers == Keys.Shift)
            {
                deleteCurrentRow();
            }
            else if (e.KeyCode == Keys.Delete)
            {
                deleteCurrentCell();
            } else if (e.Control && e.KeyCode == Keys.C)
            {
                DataObject d = plotVarTable.GetClipboardContent();
                Clipboard.SetDataObject(d);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.V)
            {
                string s = Clipboard.GetText();
                string[] lines = s.Split('\n');
                int row = plotVarTable.CurrentCell.RowIndex;
                int col = plotVarTable.CurrentCell.ColumnIndex;
                string[] cells = lines[0].Split('\t');
                int cellsSelected = cells.Length;
                for (int i = 0; i < cellsSelected; i++)
                {
                    plotVarTable[col, row].Value = cells[i];
                    col++;
                }
            }
        }

        private void deleteCurrentCell()
        {
            int colIndex = plotVarTable.CurrentCell.ColumnIndex;
            if (colIndex != COL_VARTYPE && colIndex != COL_GAME && colIndex != COL_BROKEN)
            {
                plotVarTable.CurrentCell.Value = "";
            }
        }

        /// <summary>
        /// Deletes the currently selected row
        /// </summary>
        private void deleteCurrentRow()
        {
            if (plotVarTable.CurrentRow != null && !plotVarTable.CurrentRow.IsNewRow)
            {
                plotVarTable.Rows.RemoveAt(plotVarTable.CurrentRow.Index);
            }
        }

        private void deleteRowButton_Click(object sender, EventArgs e)
        {
            deleteCurrentRow();
        }

        private void exportToCSVToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.csv|*.csv";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                System.IO.StreamWriter stringWriter = new System.IO.StreamWriter(d.FileName);
                var csv = new CsvWriter(stringWriter);
                //Header record
                csv.WriteField("id");
                csv.WriteField("type");
                csv.WriteField("game");
                csv.WriteField("category1");
                csv.WriteField("category2");
                csv.WriteField("state");
                csv.WriteField("broken");
                csv.WriteField("me2id");
                csv.WriteField("me3id");
                csv.WriteField("notes");
                csv.NextRecord();

                //Write records
                foreach (var item in entries)
                {
                    csv.WriteField(item.id);
                    csv.WriteField(TypeToCSVType(item.type));
                    csv.WriteField(item.game);
                    csv.WriteField(item.category1);
                    csv.WriteField(item.category2);
                    csv.WriteField(item.state);
                    csv.WriteField(item.broken);
                    csv.WriteField(item.me2id);
                    csv.WriteField(item.me3id);
                    csv.WriteField(item.notes);
                    csv.NextRecord();
                }

                stringWriter.Close();
                status.Text = "Exported DB to CSV: " + d.FileName;
            }
        }

        private void customSortCompare(object sender, DataGridViewSortCompareEventArgs e)
        {
            if (e.Column.Index == COL_PLOTID || e.Column.Index == COL_ME2SPEC || e.Column.Index == COL_ME3SPEC)
            {
                int a = 0;
                int b = 0;
                bool aresult = Int32.TryParse(e.CellValue1.ToString(), out a);
                bool bresult = Int32.TryParse(e.CellValue2.ToString(), out b);
                if (!aresult && !bresult)
                {
                    //two empty values being compared
                    e.SortResult = 0;
                    e.Handled = true;
                    return;
                }

                e.SortResult = a.CompareTo(b);

                e.Handled = true;
            }
        }

        private void importFromCSVToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.csv|*.csv";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                commitTable();
                System.IO.StreamReader stringreader = new System.IO.StreamReader(d.FileName);
                var csv = new CsvReader(stringreader);
                var item = csv.GetRecords<PlotVarEntry>();
                List<PlotVarEntry> importingEntries = new List<PlotVarEntry>();

                while (csv.Read())
                {
                    PlotVarEntry p = new PlotVarEntry();
                    p.id = csv.GetField<int>(COL_PLOTID);
                    p.type = CSVStringToType(csv.GetField<string>(COL_VARTYPE));
                    p.game = csv.GetField<int>(COL_GAME);
                    p.category1 = csv.GetField<string>(COL_CATEGORY1);
                    p.category2 = csv.GetField<string>(COL_CATEGORY2);
                    p.state = csv.GetField<string>(COL_STATE);
                    p.broken = csv.GetField<bool>(COL_BROKEN);
                    p.me2id = csv.GetField<int>(COL_ME2SPEC);
                    p.me3id = csv.GetField<int>(COL_ME3SPEC);
                    p.notes = csv.GetField<string>(COL_NOTES);
                    importingEntries.Add(p);
                }


                //csv.GetRecords<PlotVarEntry>().ToList();
                stringreader.Close();

                //import
                int recordsImported = 0, recordsUpdated = 0;
                foreach (PlotVarEntry pve in importingEntries)
                {
                    bool import = true;
                    foreach (PlotVarEntry ent in entries)
                    {
                        if (ent.id == pve.id && ent.game == pve.game)
                        {
                            import = false;
                            bool recordUpdated = false;
                            //same entry, merge empty values
                            //vartype
                            if (ent.type != pve.type)
                            {
                                ent.type = pve.type;
                                recordUpdated = true;
                            }

                            //broken
                            if (ent.broken != pve.broken)
                            {
                                ent.broken = pve.broken;
                                recordUpdated = true;
                            }

                            //me2
                            if (ent.me2id != pve.me2id)
                            {
                                ent.me2id = pve.me2id;
                                recordUpdated = true;
                            }

                            //me3id
                            if (ent.me3id != pve.me3id)
                            {
                                ent.me3id = pve.me3id;
                                recordUpdated = true;
                            }

                            //category
                            if (ent.category1 == null || ent.category1.Equals(""))
                            {
                                ent.category1 = pve.category1;
                                recordUpdated = true;
                            }

                            //state
                            if (ent.state == null || ent.state.Equals(""))
                            {
                                ent.state = pve.state;
                                recordUpdated = true;
                            }

                            //notes
                            if (ent.notes == null || ent.notes.Equals(""))
                            {
                                ent.notes = pve.notes;
                                recordUpdated = true;
                            }

                            if (recordUpdated)
                            {
                                recordsUpdated++;
                            }
                        }
                    }
                    if (import)
                    {
                        recordsImported++;
                        entries.Add(pve);
                    }
                }
                RefreshTable();
                status.Text = "Imported from CSV into DB: " + d.FileName + " | " + recordsImported + " records imported | " + recordsUpdated + " records upated";
            }
        }
        private int CSVStringToType(string v)
        {
            switch (v)
            {
                case "B":
                    return VARTYPE_BOOL;
                case "I":
                    return VARTYPE_INTEGER;
                case "F":
                    return VARTYPE_FLOAT;
                default:
                    return 3;
            }
        }

        private void cellClicked(object sender, DataGridViewCellEventArgs e)
        {
            bool validClick = (e.RowIndex != -1 && e.ColumnIndex != -1); //Make sure the clicked row/column is valid.
            var datagridview = sender as DataGridView;

            // Check to make sure the cell clicked is the cell containing the combobox 
            if (datagridview.Columns[e.ColumnIndex] is DataGridViewComboBoxColumn && validClick)
            {
                datagridview.BeginEdit(true);
                ((ComboBox)datagridview.EditingControl).DroppedDown = true;
            }
        }
    }
}