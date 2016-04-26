using System.Windows.Forms;

namespace ME3Explorer.PlotVarDB
{
    partial class PlotVarDB
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PlotVarDB));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.newDatabaseToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadDatabaseToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveDatabaseToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportToCSVToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importFromCSVToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripTextBox1 = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripButton1 = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.deleteRowButton = new System.Windows.Forms.ToolStripButton();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.status = new System.Windows.Forms.ToolStripStatusLabel();
            this.plotVarTable = new System.Windows.Forms.DataGridView();
            this.plotIDColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.varTypeColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.gameColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.categoryColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.category2column = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.stateColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.brokenColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.me1me2Column = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.me2me3Column = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.notesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.menuStrip1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.plotVarTable)).BeginInit();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1015, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            this.menuStrip1.Visible = false;
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newDatabaseToolStripMenuItem,
            this.loadDatabaseToolStripMenuItem,
            this.saveDatabaseToolStripMenuItem,
            this.exportToCSVToolStripMenuItem,
            this.importFromCSVToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // newDatabaseToolStripMenuItem
            // 
            this.newDatabaseToolStripMenuItem.Name = "newDatabaseToolStripMenuItem";
            this.newDatabaseToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.newDatabaseToolStripMenuItem.Text = "New Database";
            this.newDatabaseToolStripMenuItem.Click += new System.EventHandler(this.newDatabaseToolStripMenuItem_Click);
            // 
            // loadDatabaseToolStripMenuItem
            // 
            this.loadDatabaseToolStripMenuItem.Name = "loadDatabaseToolStripMenuItem";
            this.loadDatabaseToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.loadDatabaseToolStripMenuItem.Text = "Load Database";
            this.loadDatabaseToolStripMenuItem.Click += new System.EventHandler(this.loadDatabaseToolStripMenuItem_Click);
            // 
            // saveDatabaseToolStripMenuItem
            // 
            this.saveDatabaseToolStripMenuItem.Name = "saveDatabaseToolStripMenuItem";
            this.saveDatabaseToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.saveDatabaseToolStripMenuItem.Text = "Save Database";
            this.saveDatabaseToolStripMenuItem.Click += new System.EventHandler(this.saveDatabaseToolStripMenuItem_Click);
            // 
            // exportToCSVToolStripMenuItem
            // 
            this.exportToCSVToolStripMenuItem.Name = "exportToCSVToolStripMenuItem";
            this.exportToCSVToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.exportToCSVToolStripMenuItem.Text = "Export to CSV";
            this.exportToCSVToolStripMenuItem.Click += new System.EventHandler(this.exportToCSVToolStripMenuItem_Click);
            // 
            // importFromCSVToolStripMenuItem
            // 
            this.importFromCSVToolStripMenuItem.Name = "importFromCSVToolStripMenuItem";
            this.importFromCSVToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.importFromCSVToolStripMenuItem.Text = "Import from CSV ";
            this.importFromCSVToolStripMenuItem.Click += new System.EventHandler(this.importFromCSVToolStripMenuItem_Click);
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripTextBox1,
            this.toolStripButton1,
            this.toolStripSeparator2,
            this.deleteRowButton});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(1015, 25);
            this.toolStrip1.TabIndex = 1;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // toolStripTextBox1
            // 
            this.toolStripTextBox1.Name = "toolStripTextBox1";
            this.toolStripTextBox1.Size = new System.Drawing.Size(100, 25);
            this.toolStripTextBox1.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.toolStripTextBox1_KeyPress);
            // 
            // toolStripButton1
            // 
            this.toolStripButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButton1.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton1.Image")));
            this.toolStripButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButton1.Name = "toolStripButton1";
            this.toolStripButton1.Size = new System.Drawing.Size(46, 22);
            this.toolStripButton1.Text = "Search";
            this.toolStripButton1.Click += new System.EventHandler(this.toolStripButton1_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 25);
            // 
            // deleteRowButton
            // 
            this.deleteRowButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.deleteRowButton.Image = ((System.Drawing.Image)(resources.GetObject("deleteRowButton.Image")));
            this.deleteRowButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.deleteRowButton.Name = "deleteRowButton";
            this.deleteRowButton.Size = new System.Drawing.Size(70, 22);
            this.deleteRowButton.Text = "Delete Row";
            this.deleteRowButton.ToolTipText = "Delete the row of the selected cell or row. Alternatively, press Shift + Delete.";
            this.deleteRowButton.Click += new System.EventHandler(this.deleteRowButton_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.status});
            this.statusStrip1.Location = new System.Drawing.Point(0, 262);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(1015, 22);
            this.statusStrip1.TabIndex = 2;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // status
            // 
            this.status.Name = "status";
            this.status.Size = new System.Drawing.Size(0, 17);
            // 
            // plotVarTable
            // 
            this.plotVarTable.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.plotVarTable.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.plotIDColumn,
            this.varTypeColumn,
            this.gameColumn,
            this.categoryColumn,
            this.category2column,
            this.stateColumn,
            this.brokenColumn,
            this.me1me2Column,
            this.me2me3Column,
            this.notesColumn});
            this.plotVarTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.plotVarTable.Location = new System.Drawing.Point(0, 25);
            this.plotVarTable.MultiSelect = false;
            this.plotVarTable.Name = "plotVarTable";
            this.plotVarTable.Size = new System.Drawing.Size(1015, 237);
            this.plotVarTable.TabIndex = 4;
            this.plotVarTable.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.cellClicked);
            this.plotVarTable.CellValidating += new System.Windows.Forms.DataGridViewCellValidatingEventHandler(this.plotVarTable_CellValidating);
            this.plotVarTable.SortCompare += new System.Windows.Forms.DataGridViewSortCompareEventHandler(this.customSortCompare);
            this.plotVarTable.KeyDown += new System.Windows.Forms.KeyEventHandler(this.plotVarTable_KeyDown);
            // 
            // plotIDColumn
            // 
            dataGridViewCellStyle1.NullValue = "";
            this.plotIDColumn.DefaultCellStyle = dataGridViewCellStyle1;
            this.plotIDColumn.Frozen = true;
            this.plotIDColumn.HeaderText = "Plot ID";
            this.plotIDColumn.Name = "plotIDColumn";
            this.plotIDColumn.ToolTipText = "The ID that defines this plot element.";
            // 
            // varTypeColumn
            // 
            this.varTypeColumn.Frozen = true;
            this.varTypeColumn.HeaderText = "Variable Type";
            this.varTypeColumn.Items.AddRange(new object[] {
            "Boolean",
            "Float",
            "Integer"});
            this.varTypeColumn.Name = "varTypeColumn";
            this.varTypeColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.varTypeColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.varTypeColumn.ToolTipText = "Type of value this variable holds.";
            // 
            // gameColumn
            // 
            this.gameColumn.Frozen = true;
            this.gameColumn.HeaderText = "Game";
            this.gameColumn.Items.AddRange(new object[] {
            "Mass Effect",
            "Mass Effect 2",
            "Mass Effect 3"});
            this.gameColumn.Name = "gameColumn";
            this.gameColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.gameColumn.ToolTipText = "Which game this plot ID belongs to";
            // 
            // categoryColumn
            // 
            dataGridViewCellStyle2.NullValue = "";
            this.categoryColumn.DefaultCellStyle = dataGridViewCellStyle2;
            this.categoryColumn.Frozen = true;
            this.categoryColumn.HeaderText = "Category 1";
            this.categoryColumn.Name = "categoryColumn";
            this.categoryColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.categoryColumn.ToolTipText = "Category for the plot ID, such as LEVIATHAN or ROMANCE";
            // 
            // category2column
            // 
            dataGridViewCellStyle3.NullValue = "";
            this.category2column.DefaultCellStyle = dataGridViewCellStyle3;
            this.category2column.Frozen = true;
            this.category2column.HeaderText = "Category 2";
            this.category2column.Name = "category2column";
            this.category2column.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.category2column.ToolTipText = "Category (additional) for the plot ID, such as LEVIATHAN or ROMANCE";
            // 
            // stateColumn
            // 
            dataGridViewCellStyle4.NullValue = "";
            this.stateColumn.DefaultCellStyle = dataGridViewCellStyle4;
            this.stateColumn.Frozen = true;
            this.stateColumn.HeaderText = "State/Values";
            this.stateColumn.Name = "stateColumn";
            this.stateColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.stateColumn.ToolTipText = "Describes what this plot ID is used for";
            // 
            // brokenColumn
            // 
            this.brokenColumn.FalseValue = "false";
            this.brokenColumn.Frozen = true;
            this.brokenColumn.HeaderText = "Broken?";
            this.brokenColumn.IndeterminateValue = "false";
            this.brokenColumn.Name = "brokenColumn";
            this.brokenColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.brokenColumn.ToolTipText = "Indicator that this plot ID value is not properly set by the game";
            this.brokenColumn.TrueValue = "true";
            this.brokenColumn.Width = 50;
            // 
            // me1me2Column
            // 
            dataGridViewCellStyle5.NullValue = "";
            this.me1me2Column.DefaultCellStyle = dataGridViewCellStyle5;
            this.me1me2Column.Frozen = true;
            this.me1me2Column.HeaderText = "ME1->ME2 ID";
            this.me1me2Column.Name = "me1me2Column";
            this.me1me2Column.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.me1me2Column.ToolTipText = "The ID for the ME2 ID if this value carries from ME1 into ME2";
            // 
            // me2me3Column
            // 
            dataGridViewCellStyle6.NullValue = "";
            this.me2me3Column.DefaultCellStyle = dataGridViewCellStyle6;
            this.me2me3Column.Frozen = true;
            this.me2me3Column.HeaderText = "ME2->ME3 ID";
            this.me2me3Column.Name = "me2me3Column";
            this.me2me3Column.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.me2me3Column.ToolTipText = "The ID for the ME2 ID if this value carries from ME2 into ME3";
            // 
            // notesColumn
            // 
            this.notesColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            dataGridViewCellStyle7.NullValue = "";
            this.notesColumn.DefaultCellStyle = dataGridViewCellStyle7;
            this.notesColumn.HeaderText = "Notes";
            this.notesColumn.Name = "notesColumn";
            this.notesColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.notesColumn.ToolTipText = "Additional notes for this plot ID.";
            // 
            // PlotVarDB
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1015, 284);
            this.Controls.Add(this.plotVarTable);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "PlotVarDB";
            this.Text = "Plot Database";
            this.Load += new System.EventHandler(this.PlotVarDB_Load);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.plotVarTable)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripTextBox toolStripTextBox1;
        private System.Windows.Forms.ToolStripButton toolStripButton1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loadDatabaseToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveDatabaseToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem newDatabaseToolStripMenuItem;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel status;
        private System.Windows.Forms.DataGridView plotVarTable;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripButton deleteRowButton;
        private ToolStripMenuItem exportToCSVToolStripMenuItem;
        private ToolStripMenuItem importFromCSVToolStripMenuItem;
        private DataGridViewTextBoxColumn plotIDColumn;
        private DataGridViewComboBoxColumn varTypeColumn;
        private DataGridViewComboBoxColumn gameColumn;
        private DataGridViewTextBoxColumn categoryColumn;
        private DataGridViewTextBoxColumn category2column;
        private DataGridViewTextBoxColumn stateColumn;
        private DataGridViewCheckBoxColumn brokenColumn;
        private DataGridViewTextBoxColumn me1me2Column;
        private DataGridViewTextBoxColumn me2me3Column;
        private DataGridViewTextBoxColumn notesColumn;
    }
}