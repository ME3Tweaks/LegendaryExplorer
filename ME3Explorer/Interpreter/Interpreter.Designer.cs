using System;

namespace ME3Explorer
{
    partial class Interpreter
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Interpreter));
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripButton2 = new System.Windows.Forms.ToolStripButton();
            this.expandSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.expandAllButton = new System.Windows.Forms.ToolStripButton();
            this.collapseAllButton = new System.Windows.Forms.ToolStripButton();
            this.setValueSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.nameEntry = new System.Windows.Forms.ToolStripTextBox();
            this.proptext = new System.Windows.Forms.ToolStripTextBox();
            this.objectNameLabel = new System.Windows.Forms.ToolStripLabel();
            this.propDropdown = new System.Windows.Forms.ToolStripComboBox();
            this.setPropertyButton = new System.Windows.Forms.ToolStripButton();
            this.addArrayElementButton = new System.Windows.Forms.ToolStripButton();
            this.deleteArrayElementButton = new System.Windows.Forms.ToolStripButton();
            this.moveUpButton = new System.Windows.Forms.ToolStripButton();
            this.moveDownButton = new System.Windows.Forms.ToolStripButton();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.hb1 = new Be.Windows.Forms.HexBox();
            this.treeView1 = new System.Windows.Forms.TreeView();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripButton2,
            this.expandSeparator,
            this.expandAllButton,
            this.collapseAllButton,
            this.setValueSeparator,
            this.nameEntry,
            this.proptext,
            this.objectNameLabel,
            this.propDropdown,
            this.setPropertyButton,
            this.addArrayElementButton,
            this.deleteArrayElementButton,
            this.moveUpButton,
            this.moveDownButton});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(992, 25);
            this.toolStrip1.TabIndex = 1;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // toolStripButton2
            // 
            this.toolStripButton2.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButton2.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton2.Image")));
            this.toolStripButton2.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButton2.Name = "toolStripButton2";
            this.toolStripButton2.Size = new System.Drawing.Size(44, 22);
            this.toolStripButton2.Text = "Export";
            this.toolStripButton2.Click += new System.EventHandler(this.toolStripButton2_Click);
            // 
            // expandSeparator
            // 
            this.expandSeparator.Name = "expandSeparator";
            this.expandSeparator.Size = new System.Drawing.Size(6, 25);
            // 
            // expandAllButton
            // 
            this.expandAllButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.expandAllButton.Image = ((System.Drawing.Image)(resources.GetObject("expandAllButton.Image")));
            this.expandAllButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.expandAllButton.Name = "expandAllButton";
            this.expandAllButton.Size = new System.Drawing.Size(66, 22);
            this.expandAllButton.Text = "Expand All";
            this.expandAllButton.Click += new System.EventHandler(this.expandAllButton_Click);
            // 
            // collapseAllButton
            // 
            this.collapseAllButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.collapseAllButton.Image = ((System.Drawing.Image)(resources.GetObject("collapseAllButton.Image")));
            this.collapseAllButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.collapseAllButton.Name = "collapseAllButton";
            this.collapseAllButton.Size = new System.Drawing.Size(73, 22);
            this.collapseAllButton.Text = "Collapse All";
            this.collapseAllButton.Click += new System.EventHandler(this.collapseAllButton_Click);
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
            this.nameEntry.Size = new System.Drawing.Size(200, 25);
            this.nameEntry.Visible = false;
            // 
            // proptext
            // 
            this.proptext.Name = "proptext";
            this.proptext.Size = new System.Drawing.Size(120, 25);
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
            this.deleteArrayElementButton.Size = new System.Drawing.Size(121, 19);
            this.deleteArrayElementButton.Text = "Delete Array Element";
            this.deleteArrayElementButton.Visible = false;
            this.deleteArrayElementButton.Click += new System.EventHandler(this.deleteArrayElement_Click);
            // 
            // moveUpButton
            // 
            this.moveUpButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.moveUpButton.Image = ((System.Drawing.Image)(resources.GetObject("moveUpButton.Image")));
            this.moveUpButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.moveUpButton.Name = "moveUpButton";
            this.moveUpButton.Size = new System.Drawing.Size(23, 19);
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
            this.moveDownButton.Size = new System.Drawing.Size(23, 19);
            this.moveDownButton.Text = "▼";
            this.moveDownButton.ToolTipText = "Move element down";
            this.moveDownButton.Visible = false;
            this.moveDownButton.Click += new System.EventHandler(this.moveDownButton_Click);
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
            this.splitContainer1.Size = new System.Drawing.Size(992, 373);
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
            this.hb1.Size = new System.Drawing.Size(205, 373);
            this.hb1.StringViewVisible = true;
            this.hb1.TabIndex = 0;
            this.hb1.VScrollBarVisible = true;
            // 
            // treeView1
            // 
            this.treeView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.treeView1.HideSelection = false;
            this.treeView1.Location = new System.Drawing.Point(0, 0);
            this.treeView1.Name = "treeView1";
            this.treeView1.Size = new System.Drawing.Size(783, 373);
            this.treeView1.TabIndex = 0;
            this.treeView1.AfterExpand += new System.Windows.Forms.TreeViewEventHandler(this.treeView1_AfterExpand);
            this.treeView1.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeView1_AfterSelect);
            // 
            // Interpreter
            // 
            this.ClientSize = new System.Drawing.Size(992, 398);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.toolStrip1);
            this.Name = "Interpreter";
            this.Text = "Interpreter";
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        
        #endregion
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        public Be.Windows.Forms.HexBox hb1;
        public System.Windows.Forms.TreeView treeView1;
        private System.Windows.Forms.ToolStripButton toolStripButton2;
        private System.Windows.Forms.ToolStripTextBox proptext;
        private System.Windows.Forms.ToolStripButton setPropertyButton;
        private System.Windows.Forms.ToolStripSeparator setValueSeparator;
        private System.Windows.Forms.ToolStripButton expandAllButton;
        private System.Windows.Forms.ToolStripButton collapseAllButton;
        private System.Windows.Forms.ToolStripButton deleteArrayElementButton;
        private System.Windows.Forms.ToolStripButton addArrayElementButton;
        private System.Windows.Forms.ToolStripComboBox propDropdown;
        private System.Windows.Forms.ToolStripTextBox nameEntry;
        private System.Windows.Forms.ToolStripLabel objectNameLabel;
        private System.Windows.Forms.ToolStripSeparator expandSeparator;
        private System.Windows.Forms.ToolStripButton moveUpButton;
        private System.Windows.Forms.ToolStripButton moveDownButton;
    }
}