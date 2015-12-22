using System;

namespace ME3Explorer.Interpreter2
{
    partial class Interpreter2
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Interpreter2));
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripButton1 = new System.Windows.Forms.ToolStripButton();
            this.toolStripButton2 = new System.Windows.Forms.ToolStripButton();
            this.expandSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.expandAllButton = new System.Windows.Forms.ToolStripButton();
            this.collapseAllButton = new System.Windows.Forms.ToolStripButton();
            this.setValueSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.proptext = new System.Windows.Forms.ToolStripTextBox();
            this.setPropertyButton = new System.Windows.Forms.ToolStripButton();
            this.arraySeparator = new System.Windows.Forms.ToolStripSeparator();
            this.arrayViewerDropdown = new System.Windows.Forms.ToolStripComboBox();
            this.arrayPropertyDropdown = new System.Windows.Forms.ToolStripDropDownButton();
            this.arrayAdd4Bytes = new System.Windows.Forms.ToolStripMenuItem();
            this.arrayAdd8Bytes = new System.Windows.Forms.ToolStripMenuItem();
            this.arrayRemove4Bytes = new System.Windows.Forms.ToolStripMenuItem();
            this.arrayRemove8Bytes = new System.Windows.Forms.ToolStripMenuItem();
            this.doNotUseThisMenuToModifyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.arraysOfBYTEPropertiesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.arraysOfBYTEPropertiesToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.arraysOfSTRUCTPropertiesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.arraysOfDELEGATEPropertiesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.arraysOfBOOLPropertiesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
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
            // statusStrip1
            // 
            this.statusStrip1.Location = new System.Drawing.Point(0, 414);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(1008, 22);
            this.statusStrip1.TabIndex = 0;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripButton1,
            this.toolStripButton2,
            this.expandSeparator,
            this.expandAllButton,
            this.collapseAllButton,
            this.setValueSeparator,
            this.proptext,
            this.setPropertyButton,
            this.arraySeparator,
            this.arrayViewerDropdown,
            this.arrayPropertyDropdown});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(1008, 25);
            this.toolStrip1.TabIndex = 1;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // toolStripButton1
            // 
            this.toolStripButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButton1.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton1.Image")));
            this.toolStripButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButton1.Name = "toolStripButton1";
            this.toolStripButton1.Size = new System.Drawing.Size(63, 22);
            this.toolStripButton1.Text = "Start Scan";
            this.toolStripButton1.Click += new System.EventHandler(this.toolStripButton1_Click);
            // 
            // toolStripButton2
            // 
            this.toolStripButton2.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButton2.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton2.Image")));
            this.toolStripButton2.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButton2.Name = "toolStripButton2";
            this.toolStripButton2.Size = new System.Drawing.Size(69, 22);
            this.toolStripButton2.Text = "Export Tree";
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
            this.setValueSeparator.Visible = false;
            // 
            // proptext
            // 
            this.proptext.Name = "proptext";
            this.proptext.Size = new System.Drawing.Size(100, 25);
            this.proptext.Visible = false;
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
            // arraySeparator
            // 
            this.arraySeparator.Name = "arraySeparator";
            this.arraySeparator.Size = new System.Drawing.Size(6, 25);
            // 
            // arrayViewerDropdown
            // 
            this.arrayViewerDropdown.BackColor = System.Drawing.SystemColors.Control;
            this.arrayViewerDropdown.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.arrayViewerDropdown.Items.AddRange(new object[] {
            "Arrays: Raw",
            "Arrays: Import/Export",
            "Arrays: Name Table"});
            this.arrayViewerDropdown.Name = "arrayViewerDropdown";
            this.arrayViewerDropdown.Size = new System.Drawing.Size(140, 25);
            this.arrayViewerDropdown.SelectedIndexChanged += new System.EventHandler(this.arrayViewerDropdown_selectionChanged);
            // 
            // arrayPropertyDropdown
            // 
            this.arrayPropertyDropdown.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.arrayPropertyDropdown.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.arrayAdd4Bytes,
            this.arrayAdd8Bytes,
            this.arrayRemove4Bytes,
            this.arrayRemove8Bytes,
            this.doNotUseThisMenuToModifyToolStripMenuItem});
            this.arrayPropertyDropdown.Enabled = false;
            this.arrayPropertyDropdown.Image = ((System.Drawing.Image)(resources.GetObject("arrayPropertyDropdown.Image")));
            this.arrayPropertyDropdown.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.arrayPropertyDropdown.Name = "arrayPropertyDropdown";
            this.arrayPropertyDropdown.Size = new System.Drawing.Size(139, 22);
            this.arrayPropertyDropdown.Text = "Array Property Actions";
            this.arrayPropertyDropdown.Visible = false;
            // 
            // arrayAdd4Bytes
            // 
            this.arrayAdd4Bytes.Name = "arrayAdd4Bytes";
            this.arrayAdd4Bytes.Size = new System.Drawing.Size(245, 22);
            this.arrayAdd4Bytes.Text = "Add Value (4-bytes) ";
            this.arrayAdd4Bytes.ToolTipText = "Adds a 4 byte value to this array.";
            // 
            // arrayAdd8Bytes
            // 
            this.arrayAdd8Bytes.Name = "arrayAdd8Bytes";
            this.arrayAdd8Bytes.Size = new System.Drawing.Size(245, 22);
            this.arrayAdd8Bytes.Text = "Add Value (8-bytes)";
            this.arrayAdd8Bytes.ToolTipText = "Adds an 8-byte value to this array. If this array uses 4-byte values, this is eff" +
    "ectively 2 values.";
            // 
            // arrayRemove4Bytes
            // 
            this.arrayRemove4Bytes.Name = "arrayRemove4Bytes";
            this.arrayRemove4Bytes.Size = new System.Drawing.Size(245, 22);
            this.arrayRemove4Bytes.Text = "Remove Value (4-bytes)";
            this.arrayRemove4Bytes.ToolTipText = "Removes 4 bytes starting at the selected index.";
            this.arrayRemove4Bytes.Click += new System.EventHandler(this.arrayRemove4Bytes_Click);
            // 
            // arrayRemove8Bytes
            // 
            this.arrayRemove8Bytes.Name = "arrayRemove8Bytes";
            this.arrayRemove8Bytes.Size = new System.Drawing.Size(245, 22);
            this.arrayRemove8Bytes.Text = "Remove Value (8-bytes)";
            this.arrayRemove8Bytes.ToolTipText = "Removes 8 bytes from the selected index. If this array uses 4-byte values, this w" +
    "ill remove 2 values.";
            // 
            // doNotUseThisMenuToModifyToolStripMenuItem
            // 
            this.doNotUseThisMenuToModifyToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.arraysOfBYTEPropertiesToolStripMenuItem,
            this.arraysOfBYTEPropertiesToolStripMenuItem1,
            this.arraysOfSTRUCTPropertiesToolStripMenuItem,
            this.arraysOfDELEGATEPropertiesToolStripMenuItem,
            this.arraysOfBOOLPropertiesToolStripMenuItem});
            this.doNotUseThisMenuToModifyToolStripMenuItem.Name = "doNotUseThisMenuToModifyToolStripMenuItem";
            this.doNotUseThisMenuToModifyToolStripMenuItem.Size = new System.Drawing.Size(245, 22);
            this.doNotUseThisMenuToModifyToolStripMenuItem.Text = "Do not use this menu to modify:";
            // 
            // arraysOfBYTEPropertiesToolStripMenuItem
            // 
            this.arraysOfBYTEPropertiesToolStripMenuItem.Enabled = false;
            this.arraysOfBYTEPropertiesToolStripMenuItem.Name = "arraysOfBYTEPropertiesToolStripMenuItem";
            this.arraysOfBYTEPropertiesToolStripMenuItem.Size = new System.Drawing.Size(234, 22);
            this.arraysOfBYTEPropertiesToolStripMenuItem.Text = "Arrays of BYTE properties";
            // 
            // arraysOfBYTEPropertiesToolStripMenuItem1
            // 
            this.arraysOfBYTEPropertiesToolStripMenuItem1.Enabled = false;
            this.arraysOfBYTEPropertiesToolStripMenuItem1.Name = "arraysOfBYTEPropertiesToolStripMenuItem1";
            this.arraysOfBYTEPropertiesToolStripMenuItem1.Size = new System.Drawing.Size(234, 22);
            this.arraysOfBYTEPropertiesToolStripMenuItem1.Text = "Arrays of STRING properties";
            // 
            // arraysOfSTRUCTPropertiesToolStripMenuItem
            // 
            this.arraysOfSTRUCTPropertiesToolStripMenuItem.Enabled = false;
            this.arraysOfSTRUCTPropertiesToolStripMenuItem.Name = "arraysOfSTRUCTPropertiesToolStripMenuItem";
            this.arraysOfSTRUCTPropertiesToolStripMenuItem.Size = new System.Drawing.Size(234, 22);
            this.arraysOfSTRUCTPropertiesToolStripMenuItem.Text = "Arrays of STRUCT properties";
            // 
            // arraysOfDELEGATEPropertiesToolStripMenuItem
            // 
            this.arraysOfDELEGATEPropertiesToolStripMenuItem.Enabled = false;
            this.arraysOfDELEGATEPropertiesToolStripMenuItem.Name = "arraysOfDELEGATEPropertiesToolStripMenuItem";
            this.arraysOfDELEGATEPropertiesToolStripMenuItem.Size = new System.Drawing.Size(234, 22);
            this.arraysOfDELEGATEPropertiesToolStripMenuItem.Text = "Arrays of DELEGATE properties";
            // 
            // arraysOfBOOLPropertiesToolStripMenuItem
            // 
            this.arraysOfBOOLPropertiesToolStripMenuItem.Enabled = false;
            this.arraysOfBOOLPropertiesToolStripMenuItem.Name = "arraysOfBOOLPropertiesToolStripMenuItem";
            this.arraysOfBOOLPropertiesToolStripMenuItem.Size = new System.Drawing.Size(234, 22);
            this.arraysOfBOOLPropertiesToolStripMenuItem.Text = "Arrays of BOOL properties";
            // 
            // menuStrip1
            // 
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(292, 24);
            this.menuStrip1.TabIndex = 2;
            this.menuStrip1.Text = "menuStrip1";
            this.menuStrip1.Visible = false;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 25);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.hb1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.treeView1);
            this.splitContainer1.Size = new System.Drawing.Size(1008, 389);
            this.splitContainer1.SplitterDistance = 332;
            this.splitContainer1.TabIndex = 3;
            // 
            // hb1
            // 
            this.hb1.BoldFont = null;
            this.hb1.BytesPerLine = 4;
            this.hb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.hb1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.hb1.LineInfoForeColor = System.Drawing.Color.Empty;
            this.hb1.LineInfoVisible = true;
            this.hb1.Location = new System.Drawing.Point(0, 0);
            this.hb1.Name = "hb1";
            this.hb1.ReadOnly = true;
            this.hb1.ShadowSelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(60)))), ((int)(((byte)(188)))), ((int)(((byte)(255)))));
            this.hb1.Size = new System.Drawing.Size(332, 389);
            this.hb1.StringViewVisible = true;
            this.hb1.TabIndex = 0;
            this.hb1.UseFixedBytesPerLine = true;
            this.hb1.VScrollBarVisible = true;
            // 
            // treeView1
            // 
            this.treeView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.treeView1.HideSelection = false;
            this.treeView1.Location = new System.Drawing.Point(0, 0);
            this.treeView1.Name = "treeView1";
            this.treeView1.Size = new System.Drawing.Size(672, 389);
            this.treeView1.TabIndex = 0;
            this.treeView1.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeView1_AfterSelect);
            // 
            // Interpreter2
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1008, 436);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Interpreter2";
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

        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private Be.Windows.Forms.HexBox hb1;
        private System.Windows.Forms.ToolStripButton toolStripButton1;
        private System.Windows.Forms.TreeView treeView1;
        private System.Windows.Forms.ToolStripButton toolStripButton2;
        private System.Windows.Forms.ToolStripSeparator expandSeparator;
        private System.Windows.Forms.ToolStripTextBox proptext;
        private System.Windows.Forms.ToolStripButton setPropertyButton;
        private System.Windows.Forms.ToolStripSeparator setValueSeparator;
        private System.Windows.Forms.ToolStripButton expandAllButton;
        private System.Windows.Forms.ToolStripButton collapseAllButton;
        private System.Windows.Forms.ToolStripComboBox arrayViewerDropdown;
        private System.Windows.Forms.ToolStripSeparator arraySeparator;
        private System.Windows.Forms.ToolStripDropDownButton arrayPropertyDropdown;
        private System.Windows.Forms.ToolStripMenuItem arrayAdd4Bytes;
        private System.Windows.Forms.ToolStripMenuItem arrayAdd8Bytes;
        private System.Windows.Forms.ToolStripMenuItem arrayRemove4Bytes;
        private System.Windows.Forms.ToolStripMenuItem arrayRemove8Bytes;
        private System.Windows.Forms.ToolStripMenuItem doNotUseThisMenuToModifyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem arraysOfBYTEPropertiesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem arraysOfBYTEPropertiesToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem arraysOfSTRUCTPropertiesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem arraysOfDELEGATEPropertiesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem arraysOfBOOLPropertiesToolStripMenuItem;
    }
}