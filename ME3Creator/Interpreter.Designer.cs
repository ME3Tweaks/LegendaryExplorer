using System;

namespace ME3Creator
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Interpreter));
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.nameEntry = new System.Windows.Forms.ToolStripTextBox();
            this.proptext = new System.Windows.Forms.ToolStripTextBox();
            this.objectNameLabel = new System.Windows.Forms.ToolStripLabel();
            this.propDropdown = new System.Windows.Forms.ToolStripComboBox();
            this.setPropertyButton = new System.Windows.Forms.ToolStripButton();
            this.addArrayElementButton = new System.Windows.Forms.ToolStripButton();
            this.deleteArrayElementButton = new System.Windows.Forms.ToolStripButton();
            this.moveUpButton = new System.Windows.Forms.ToolStripButton();
            this.moveDownButton = new System.Windows.Forms.ToolStripButton();
            this.treeView1 = new System.Windows.Forms.TreeView();
            this.nodeContextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.expandAllChildrenToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.collapseAllChildrenToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip1.SuspendLayout();
            this.nodeContextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
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
            this.deleteArrayElementButton.Size = new System.Drawing.Size(121, 22);
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
            // treeView1
            // 
            this.treeView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.treeView1.HideSelection = false;
            this.treeView1.Location = new System.Drawing.Point(0, 25);
            this.treeView1.Name = "treeView1";
            this.treeView1.Size = new System.Drawing.Size(992, 373);
            this.treeView1.TabIndex = 0;
            this.treeView1.AfterExpand += new System.Windows.Forms.TreeViewEventHandler(this.treeView1_AfterExpand);
            this.treeView1.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeView1_AfterSelect);
            this.treeView1.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.treeView1_NodeMouseClick);
            // 
            // nodeContextMenuStrip1
            // 
            this.nodeContextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.expandAllChildrenToolStripMenuItem,
            this.collapseAllChildrenToolStripMenuItem});
            this.nodeContextMenuStrip1.Name = "nodeContextMenuStrip1";
            this.nodeContextMenuStrip1.Size = new System.Drawing.Size(185, 70);
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
            // Interpreter
            // 
            this.Controls.Add(this.treeView1);
            this.Controls.Add(this.toolStrip1);
            this.Name = "Interpreter";
            this.Size = new System.Drawing.Size(992, 398);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.nodeContextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        
        #endregion
        private System.Windows.Forms.ToolStrip toolStrip1;
        public System.Windows.Forms.TreeView treeView1;
        private System.Windows.Forms.ToolStripTextBox proptext;
        private System.Windows.Forms.ToolStripButton setPropertyButton;
        private System.Windows.Forms.ToolStripButton deleteArrayElementButton;
        private System.Windows.Forms.ToolStripButton addArrayElementButton;
        private System.Windows.Forms.ToolStripComboBox propDropdown;
        private System.Windows.Forms.ToolStripTextBox nameEntry;
        private System.Windows.Forms.ToolStripLabel objectNameLabel;
        private System.Windows.Forms.ToolStripButton moveUpButton;
        private System.Windows.Forms.ToolStripButton moveDownButton;
        private System.Windows.Forms.ContextMenuStrip nodeContextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem expandAllChildrenToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem collapseAllChildrenToolStripMenuItem;
    }
}