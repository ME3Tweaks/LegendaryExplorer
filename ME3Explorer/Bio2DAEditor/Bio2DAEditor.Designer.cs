using System;
using System.Windows.Forms;

namespace ME3Explorer
{
    partial class Bio2DAEditor
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Bio2DAEditor));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.exportButton = new System.Windows.Forms.ToolStripButton();
            this.saveHexButton = new System.Windows.Forms.ToolStripButton();
            this.toggleHexWidthButton = new System.Windows.Forms.ToolStripButton();
            this.setValueSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.nameEntry = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripTextBox1 = new System.Windows.Forms.ToolStripTextBox();
            this.proptext = new System.Windows.Forms.ToolStripTextBox();
            this.objectNameLabel = new System.Windows.Forms.ToolStripLabel();
            this.propDropdown = new System.Windows.Forms.ToolStripComboBox();
            this.setPropertyButton = new System.Windows.Forms.ToolStripButton();
            this.addArrayElementButton = new System.Windows.Forms.ToolStripButton();
            this.deleteArrayElementButton = new System.Windows.Forms.ToolStripButton();
            this.moveUpButton = new System.Windows.Forms.ToolStripButton();
            this.moveDownButton = new System.Windows.Forms.ToolStripButton();
            this.addPropButton = new System.Windows.Forms.ToolStripButton();
            this.findBox = new System.Windows.Forms.ToolStripTextBox();
            this.findButton = new System.Windows.Forms.ToolStripButton();
            this.viewModeDropDownList = new System.Windows.Forms.ToolStripComboBox();
            this.exportToExcelButton = new System.Windows.Forms.ToolStripButton();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.hb1 = new Be.Windows.Forms.HexBox();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.panel1 = new System.Windows.Forms.Panel();
            this.Label_CellCoordinate = new System.Windows.Forms.Label();
            this.Label_CellType = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.selectionStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.nodeContextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.expandAllChildrenToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.collapseAllChildrenToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.panel1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.nodeContextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportButton,
            this.saveHexButton,
            this.toggleHexWidthButton,
            this.setValueSeparator,
            this.nameEntry,
            this.toolStripTextBox1,
            this.proptext,
            this.objectNameLabel,
            this.propDropdown,
            this.setPropertyButton,
            this.addArrayElementButton,
            this.deleteArrayElementButton,
            this.moveUpButton,
            this.moveDownButton,
            this.addPropButton,
            this.findBox,
            this.findButton,
            this.viewModeDropDownList,
            this.exportToExcelButton});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(992, 27);
            this.toolStrip1.TabIndex = 1;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // exportButton
            // 
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(23, 22);
            // 
            // saveHexButton
            // 
            this.saveHexButton.AutoToolTip = false;
            this.saveHexButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.saveHexButton.Image = ((System.Drawing.Image)(resources.GetObject("saveHexButton.Image")));
            this.saveHexButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.saveHexButton.Name = "saveHexButton";
            this.saveHexButton.Size = new System.Drawing.Size(107, 24);
            this.saveHexButton.Text = "Save Hex Changes";
            this.saveHexButton.ToolTipText = "Saves hex changes in-memory (not to disk)";
            this.saveHexButton.Click += new System.EventHandler(this.saveHexButton_Click);
            // 
            // toggleHexWidthButton
            // 
            this.toggleHexWidthButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toggleHexWidthButton.Image = ((System.Drawing.Image)(resources.GetObject("toggleHexWidthButton.Image")));
            this.toggleHexWidthButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toggleHexWidthButton.Name = "toggleHexWidthButton";
            this.toggleHexWidthButton.Size = new System.Drawing.Size(105, 24);
            this.toggleHexWidthButton.Text = "Toggle Hex Width";
            this.toggleHexWidthButton.Click += new System.EventHandler(this.toggleHexWidthButton_Click);
            // 
            // setValueSeparator
            // 
            this.setValueSeparator.AutoSize = false;
            this.setValueSeparator.Name = "setValueSeparator";
            this.setValueSeparator.Size = new System.Drawing.Size(6, 25);
            // 
            // nameEntry
            // 
            this.nameEntry.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.nameEntry.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.CustomSource;
            this.nameEntry.Name = "nameEntry";
            this.nameEntry.Size = new System.Drawing.Size(200, 27);
            this.nameEntry.Visible = false;
            // 
            // toolStripTextBox1
            // 
            this.toolStripTextBox1.Name = "toolStripTextBox1";
            this.toolStripTextBox1.Size = new System.Drawing.Size(120, 27);
            this.toolStripTextBox1.Visible = false;
            // 
            // proptext
            // 
            this.proptext.Name = "proptext";
            this.proptext.Size = new System.Drawing.Size(120, 27);
            this.proptext.Visible = false;
            this.proptext.KeyUp += new System.Windows.Forms.KeyEventHandler(this.proptext_KeyUp);
            // 
            // objectNameLabel
            // 
            this.objectNameLabel.BackColor = System.Drawing.SystemColors.ControlDark;
            this.objectNameLabel.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.objectNameLabel.Name = "objectNameLabel";
            this.objectNameLabel.Size = new System.Drawing.Size(0, 22);
            this.objectNameLabel.Visible = false;
            // 
            // propDropdown
            // 
            this.propDropdown.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.propDropdown.DropDownWidth = 300;
            this.propDropdown.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.propDropdown.MaxDropDownItems = 20;
            this.propDropdown.Name = "propDropdown";
            this.propDropdown.Size = new System.Drawing.Size(200, 25);
            this.propDropdown.Visible = false;
            // 
            // setPropertyButton
            // 
            this.setPropertyButton.AutoSize = false;
            this.setPropertyButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.setPropertyButton.Image = ((System.Drawing.Image)(resources.GetObject("setPropertyButton.Image")));
            this.setPropertyButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.setPropertyButton.Name = "setPropertyButton";
            this.setPropertyButton.Size = new System.Drawing.Size(75, 22);
            this.setPropertyButton.Text = "Set Value";
            this.setPropertyButton.Visible = false;
            // 
            // addArrayElementButton
            // 
            this.addArrayElementButton.Name = "addArrayElementButton";
            this.addArrayElementButton.Size = new System.Drawing.Size(23, 4);

            // 
            // deleteArrayElementButton
            // 
            this.deleteArrayElementButton.Name = "deleteArrayElementButton";
            this.deleteArrayElementButton.Size = new System.Drawing.Size(23, 4);
            // 
            // moveUpButton
            // 
            this.moveUpButton.Name = "moveUpButton";
            this.moveUpButton.Size = new System.Drawing.Size(23, 22);
            // 
            // moveDownButton
            // 
            this.moveDownButton.Name = "moveDownButton";
            this.moveDownButton.Size = new System.Drawing.Size(23, 4);
            // 
            // addPropButton
            // 
            this.addPropButton.Name = "addPropButton";
            this.addPropButton.Size = new System.Drawing.Size(23, 4);
            // 
            // findBox
            // 
            this.findBox.Name = "findBox";
            this.findBox.Size = new System.Drawing.Size(100, 23);
            this.findBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.findButton_Pressed);
            // 
            // findButton
            // 
            this.findButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.findButton.Image = ((System.Drawing.Image)(resources.GetObject("findButton.Image")));
            this.findButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.findButton.Name = "findButton";
            this.findButton.Size = new System.Drawing.Size(34, 19);
            this.findButton.Text = "Find";
            this.findButton.Click += new System.EventHandler(this.FindButton_Click);
            // 
            // viewModeDropDownList
            // 
            this.viewModeDropDownList.Name = "viewModeDropDownList";
            this.viewModeDropDownList.Size = new System.Drawing.Size(121, 23);
            // 
            // exportToExcelButton
            // 
            this.exportToExcelButton.Image = ((System.Drawing.Image)(resources.GetObject("exportToExcelButton.Image")));
            this.exportToExcelButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.exportToExcelButton.Name = "exportToExcelButton";
            this.exportToExcelButton.Size = new System.Drawing.Size(143, 20);
            this.exportToExcelButton.Text = "Export table to xlsx file";
            this.exportToExcelButton.Click += new System.EventHandler(this.exportToExcel_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.Location = new System.Drawing.Point(0, 27);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.hb1);
            this.splitContainer1.Panel1MinSize = 205;
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.splitContainer2);
            this.splitContainer1.Size = new System.Drawing.Size(992, 351);

            this.splitContainer1.SplitterDistance = 205;
            this.splitContainer1.TabIndex = 3;
            this.splitContainer1.SplitterMoving += new System.Windows.Forms.SplitterCancelEventHandler(this.splitContainer1_SplitterMoving);
            // 
            // hb1
            // 
            this.hb1.BoldFont = null;
            this.hb1.BytesPerLine = 4;
            this.hb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.hb1.Font = new System.Drawing.Font("Courier New", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.hb1.LineInfoForeColor = System.Drawing.Color.Empty;
            this.hb1.LineInfoVisible = true;
            this.hb1.Location = new System.Drawing.Point(0, 0);
            this.hb1.MinBytesPerLine = 4;
            this.hb1.Name = "hb1";
            this.hb1.ShadowSelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(60)))), ((int)(((byte)(188)))), ((int)(((byte)(255)))));
            this.hb1.Size = new System.Drawing.Size(205, 349);
            this.hb1.StringViewVisible = true;
            this.hb1.TabIndex = 0;
            this.hb1.VScrollBarVisible = true;
            this.hb1.SelectionStartChanged += new System.EventHandler(this.hb1_SelectionChanged);
            this.hb1.SelectionLengthChanged += new System.EventHandler(this.hb1_SelectionChanged);

            // dataGridView1
            // 
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(0, 0);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.Size = new System.Drawing.Size(783, 349);
            this.dataGridView1.TabIndex = 0;

            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.dataGridView1);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.panel1);
            this.splitContainer2.Size = new System.Drawing.Size(783, 351);
            this.splitContainer2.SplitterDistance = 282;
            this.splitContainer2.TabIndex = 2;
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            this.dataGridView1.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(0, 0);
            this.dataGridView1.MultiSelect = false;
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dataGridView1.Size = new System.Drawing.Size(783, 282);
            this.dataGridView1.TabIndex = 0;
            this.dataGridView1.SelectionChanged += new System.EventHandler(this.Table_SelectionChanged);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.Label_CellCoordinate);
            this.panel1.Controls.Add(this.Label_CellType);
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(783, 65);
            this.panel1.TabIndex = 1;
            // 
            // Label_CellCoordinate
            // 
            this.Label_CellCoordinate.AutoSize = true;
            this.Label_CellCoordinate.Location = new System.Drawing.Point(14, 22);
            this.Label_CellCoordinate.Name = "Label_CellCoordinate";
            this.Label_CellCoordinate.Size = new System.Drawing.Size(65, 13);
            this.Label_CellCoordinate.TabIndex = 3;
            this.Label_CellCoordinate.Text = "Select a cell";
            // 
            // Label_CellType
            // 
            this.Label_CellType.AutoSize = true;
            this.Label_CellType.Location = new System.Drawing.Point(129, 22);
            this.Label_CellType.Name = "Label_CellType";
            this.Label_CellType.Size = new System.Drawing.Size(65, 13);
            this.Label_CellType.TabIndex = 2;
            this.Label_CellType.Text = "Select a cell";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(114, 4);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(64, 18);
            this.label2.TabIndex = 1;
            this.label2.Text = "Cell type";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(4, 4);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(33, 18);
            this.label1.TabIndex = 0;
            this.label1.Text = "Cell";
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.selectionStatus});
            this.statusStrip1.Location = new System.Drawing.Point(0, 376);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(992, 22);
            this.statusStrip1.TabIndex = 4;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // selectionStatus
            // 
            this.selectionStatus.Name = "selectionStatus";
            this.selectionStatus.Size = new System.Drawing.Size(98, 17);
            this.selectionStatus.Text = "Nothing Selected";
            // 
            // nodeContextMenuStrip1
            // 
            this.nodeContextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.expandAllChildrenToolStripMenuItem,
            this.collapseAllChildrenToolStripMenuItem});
            this.nodeContextMenuStrip1.Name = "nodeContextMenuStrip1";
            this.nodeContextMenuStrip1.Size = new System.Drawing.Size(68, 48);
            // 
            // expandAllChildrenToolStripMenuItem
            // 
            this.expandAllChildrenToolStripMenuItem.Name = "expandAllChildrenToolStripMenuItem";
            this.expandAllChildrenToolStripMenuItem.Size = new System.Drawing.Size(67, 22);
            // 
            // collapseAllChildrenToolStripMenuItem
            // 
            this.collapseAllChildrenToolStripMenuItem.Name = "collapseAllChildrenToolStripMenuItem";
            this.collapseAllChildrenToolStripMenuItem.Size = new System.Drawing.Size(67, 22);
            // 
            // Bio2DAEditor
            // 
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.statusStrip1);
            this.Name = "Bio2DAEditor";
            this.Size = new System.Drawing.Size(992, 398);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);

            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();

            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();

            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.nodeContextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private void proptext_KeyUp(object sender, KeyEventArgs e)
        {
            throw new NotImplementedException();
        }

        #endregion
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        public Be.Windows.Forms.HexBox hb1;
        private System.Windows.Forms.ToolStripTextBox proptext;
        private System.Windows.Forms.ToolStripButton setPropertyButton;
        private System.Windows.Forms.ToolStripSeparator setValueSeparator;
        private System.Windows.Forms.ToolStripButton deleteArrayElementButton;
        private System.Windows.Forms.ToolStripButton addArrayElementButton;
        private System.Windows.Forms.ToolStripComboBox propDropdown;
        private System.Windows.Forms.ToolStripTextBox nameEntry;
        private System.Windows.Forms.ToolStripLabel objectNameLabel;
        private System.Windows.Forms.ToolStripButton moveUpButton;
        private System.Windows.Forms.ToolStripButton moveDownButton;
        private System.Windows.Forms.ToolStripButton addPropButton;
        private System.Windows.Forms.ToolStripButton toggleHexWidthButton;
        public System.Windows.Forms.ToolStripButton saveHexButton;
        public System.Windows.Forms.ToolStripButton exportButton;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel selectionStatus;
        private System.Windows.Forms.ContextMenuStrip nodeContextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem expandAllChildrenToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem collapseAllChildrenToolStripMenuItem;
        private System.Windows.Forms.ToolStripTextBox toolStripTextBox1;
        private System.Windows.Forms.ToolStripTextBox findBox;
        private System.Windows.Forms.ToolStripButton findButton;
        private System.Windows.Forms.ToolStripComboBox viewModeDropDownList;
        private System.Windows.Forms.DataGridView dataGridView1;
        private ToolStripButton exportToExcelButton;
        private SplitContainer splitContainer2;
        private Panel panel1;
        private Label Label_CellCoordinate;
        private Label Label_CellType;
        private Label label2;
        private Label label1;
    }
}