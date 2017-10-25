using System;

namespace ME3Explorer
{
    partial class BinaryInterpreter
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BinaryInterpreter));
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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.hb1 = new Be.Windows.Forms.HexBox();
            this.treeView1 = new System.Windows.Forms.TreeView();
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
            this.viewModeDropDownList});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(992, 25);
            this.toolStrip1.TabIndex = 1;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // exportButton
            // 
            this.exportButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.exportButton.Image = ((System.Drawing.Image)(resources.GetObject("exportButton.Image")));
            this.exportButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(44, 24);
            this.exportButton.Text = "Export";
            this.exportButton.Visible = false;
            this.exportButton.Click += new System.EventHandler(this.toolStripButton2_Click);
            // 
            // saveHexButton
            // 
            this.saveHexButton.AutoToolTip = false;
            this.saveHexButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.saveHexButton.Image = ((System.Drawing.Image)(resources.GetObject("saveHexButton.Image")));
            this.saveHexButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.saveHexButton.Name = "saveHexButton";
            this.saveHexButton.Size = new System.Drawing.Size(107, 22);
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
            this.toggleHexWidthButton.Size = new System.Drawing.Size(105, 22);
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
            this.objectNameLabel.Size = new System.Drawing.Size(0, 24);
            this.objectNameLabel.Visible = false;
            // 
            // propDropdown
            // 
            this.propDropdown.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.propDropdown.DropDownWidth = 300;
            this.propDropdown.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.propDropdown.MaxDropDownItems = 20;
            this.propDropdown.Name = "propDropdown";
            this.propDropdown.Size = new System.Drawing.Size(200, 27);
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
            this.setPropertyButton.Click += new System.EventHandler(this.setProperty_Click);
            // 
            // addArrayElementButton
            // 
            this.addArrayElementButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.addArrayElementButton.Image = ((System.Drawing.Image)(resources.GetObject("addArrayElementButton.Image")));
            this.addArrayElementButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.addArrayElementButton.Name = "addArrayElementButton";
            this.addArrayElementButton.Size = new System.Drawing.Size(110, 22);
            this.addArrayElementButton.Text = "Add Array Element";
            this.addArrayElementButton.Visible = false;
            this.addArrayElementButton.Click += new System.EventHandler(this.addArrayElementButton_Click);
            // 
            // deleteArrayElementButton
            // 
            this.deleteArrayElementButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.deleteArrayElementButton.Image = ((System.Drawing.Image)(resources.GetObject("deleteArrayElementButton.Image")));
            this.deleteArrayElementButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.deleteArrayElementButton.Name = "deleteArrayElementButton";
            this.deleteArrayElementButton.Size = new System.Drawing.Size(121, 22);
            this.deleteArrayElementButton.Text = "Delete Array Element";
            this.deleteArrayElementButton.Visible = false;
            this.deleteArrayElementButton.Click += new System.EventHandler(this.deleteElement_Click);
            // 
            // moveUpButton
            // 
            this.moveUpButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.moveUpButton.Image = ((System.Drawing.Image)(resources.GetObject("moveUpButton.Image")));
            this.moveUpButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.moveUpButton.Name = "moveUpButton";
            this.moveUpButton.Size = new System.Drawing.Size(23, 22);
            this.moveUpButton.Text = "▲";
            this.moveUpButton.ToolTipText = "Move element up";
            this.moveUpButton.Visible = false;
            this.moveUpButton.Click += new System.EventHandler(this.moveUpButton_Click);
            // 
            // moveDownButton
            // 
            this.moveDownButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.moveDownButton.Image = ((System.Drawing.Image)(resources.GetObject("moveDownButton.Image")));
            this.moveDownButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.moveDownButton.Name = "moveDownButton";
            this.moveDownButton.Size = new System.Drawing.Size(23, 22);
            this.moveDownButton.Text = "▼";
            this.moveDownButton.ToolTipText = "Move element down";
            this.moveDownButton.Visible = false;
            this.moveDownButton.Click += new System.EventHandler(this.moveDownButton_Click);
            // 
            // addPropButton
            // 
            this.addPropButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.addPropButton.Image = ((System.Drawing.Image)(resources.GetObject("addPropButton.Image")));
            this.addPropButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.addPropButton.Name = "addPropButton";
            this.addPropButton.Size = new System.Drawing.Size(81, 22);
            this.addPropButton.Text = "Add Property";
            this.addPropButton.Click += new System.EventHandler(this.addPropButton_Click);
            // 
            // findBox
            // 
            this.findBox.Name = "findBox";
            this.findBox.Size = new System.Drawing.Size(100, 25);
            this.findBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.findButton_Pressed);
            // 
            // findButton
            // 
            this.findButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.findButton.Image = ((System.Drawing.Image)(resources.GetObject("findButton.Image")));
            this.findButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.findButton.Name = "findButton";
            this.findButton.Size = new System.Drawing.Size(34, 22);
            this.findButton.Text = "Find";
            this.findButton.Click += new System.EventHandler(this.FindButton_Click);
            // 
            // viewModeDropDownList
            // 
            this.viewModeDropDownList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.viewModeDropDownList.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.viewModeDropDownList.Items.AddRange(new object[] {
            "Objects",
            "Names",
            "Integers",
            "Floats"});
            this.viewModeDropDownList.Name = "viewModeDropDownList";
            this.viewModeDropDownList.Size = new System.Drawing.Size(121, 25);
            this.viewModeDropDownList.SelectedIndexChanged += new System.EventHandler(this.viewModeChanged);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.Location = new System.Drawing.Point(0, 25);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.hb1);
            this.splitContainer1.Panel1MinSize = 205;
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.treeView1);
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
            this.hb1.Size = new System.Drawing.Size(205, 351);
            this.hb1.StringViewVisible = true;
            this.hb1.TabIndex = 0;
            this.hb1.VScrollBarVisible = true;
            this.hb1.SelectionStartChanged += new System.EventHandler(this.hb1_SelectionChanged);
            this.hb1.SelectionLengthChanged += new System.EventHandler(this.hb1_SelectionChanged);
            // 
            // treeView1
            // 
            this.treeView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.treeView1.HideSelection = false;
            this.treeView1.Location = new System.Drawing.Point(0, 0);
            this.treeView1.Name = "treeView1";
            this.treeView1.Size = new System.Drawing.Size(783, 351);
            this.treeView1.TabIndex = 0;
            this.treeView1.AfterExpand += new System.Windows.Forms.TreeViewEventHandler(this.treeView1_AfterExpand);
            this.treeView1.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeView1_AfterSelect);
            this.treeView1.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.treeView1_NodeMouseClick);
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
            this.nodeContextMenuStrip1.Size = new System.Drawing.Size(185, 48);
            // 
            // expandAllChildrenToolStripMenuItem
            // 
            this.expandAllChildrenToolStripMenuItem.Name = "expandAllChildrenToolStripMenuItem";
            this.expandAllChildrenToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.expandAllChildrenToolStripMenuItem.Text = "Expand All Children";
            this.expandAllChildrenToolStripMenuItem.Click += new System.EventHandler(this.expandAllChildrenToolStripMenuItem_Click);
            // 
            // collapseAllChildrenToolStripMenuItem
            // 
            this.collapseAllChildrenToolStripMenuItem.Name = "collapseAllChildrenToolStripMenuItem";
            this.collapseAllChildrenToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.collapseAllChildrenToolStripMenuItem.Text = "Collapse All Children";
            this.collapseAllChildrenToolStripMenuItem.Click += new System.EventHandler(this.collapseAllChildrenToolStripMenuItem_Click);
            // 
            // BinaryInterpreter
            // 
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.statusStrip1);
            this.Name = "BinaryInterpreter";
            this.Size = new System.Drawing.Size(992, 398);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.nodeContextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        
        #endregion
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        public Be.Windows.Forms.HexBox hb1;
        public System.Windows.Forms.TreeView treeView1;
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
    }
}