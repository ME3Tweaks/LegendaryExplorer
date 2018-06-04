namespace ME3Explorer
{
    partial class SequenceEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SequenceEditor));
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.graphEditor = new UMD.HCIL.GraphEditor.GraphEditor();
            this.splitContainer3 = new System.Windows.Forms.SplitContainer();
            this.pg1 = new System.Windows.Forms.PropertyGrid();
            this.treeView1 = new System.Windows.Forms.TreeView();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.savePccToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.savePCCAsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveViewToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.saveViewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.loadAlternateTLKToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.recentToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addObjectsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showOutputNumbersToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.autoSaveViewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.useGlobalSequenceRefSavesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.interpretToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripTextBox1 = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripButton1 = new System.Windows.Forms.ToolStripButton();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.openInPackageEditorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openInInterpEditorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cloneToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.breakLinksToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel2 = new System.Windows.Forms.ToolStripStatusLabel();
            this.contextMenuStrip2 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addObjectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).BeginInit();
            this.splitContainer3.Panel1.SuspendLayout();
            this.splitContainer3.Panel2.SuspendLayout();
            this.splitContainer3.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.contextMenuStrip2.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 24);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.splitContainer2);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.listBox1);
            this.splitContainer1.Size = new System.Drawing.Size(1024, 552);
            this.splitContainer1.SplitterDistance = 698;
            this.splitContainer1.TabIndex = 0;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.graphEditor);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.splitContainer3);
            this.splitContainer2.Size = new System.Drawing.Size(698, 552);
            this.splitContainer2.SplitterDistance = 403;
            this.splitContainer2.TabIndex = 0;
            // 
            // graphEditor
            // 
            this.graphEditor.AllowDrop = true;
            this.graphEditor.BackColor = System.Drawing.Color.White;
            this.graphEditor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.graphEditor.GridFitText = false;
            this.graphEditor.Location = new System.Drawing.Point(0, 0);
            this.graphEditor.Name = "graphEditor";
            this.graphEditor.RegionManagement = true;
            this.graphEditor.Size = new System.Drawing.Size(698, 403);
            this.graphEditor.TabIndex = 1;
            this.graphEditor.Text = "graphEditor1";
            this.graphEditor.Click += new System.EventHandler(this.graphEditor_Click);
            this.graphEditor.DragDrop += new System.Windows.Forms.DragEventHandler(this.SequenceEditor_DragDrop);
            this.graphEditor.DragEnter += new System.Windows.Forms.DragEventHandler(this.SequenceEditor_DragEnter);
            // 
            // splitContainer3
            // 
            this.splitContainer3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer3.Location = new System.Drawing.Point(0, 0);
            this.splitContainer3.Name = "splitContainer3";
            // 
            // splitContainer3.Panel1
            // 
            this.splitContainer3.Panel1.Controls.Add(this.pg1);
            // 
            // splitContainer3.Panel2
            // 
            this.splitContainer3.Panel2.Controls.Add(this.treeView1);
            this.splitContainer3.Size = new System.Drawing.Size(698, 145);
            this.splitContainer3.SplitterDistance = 346;
            this.splitContainer3.TabIndex = 0;
            // 
            // pg1
            // 
            this.pg1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pg1.CategoryForeColor = System.Drawing.SystemColors.InactiveCaptionText;
            this.pg1.HelpVisible = false;
            this.pg1.Location = new System.Drawing.Point(0, 0);
            this.pg1.Name = "pg1";
            this.pg1.Size = new System.Drawing.Size(344, 120);
            this.pg1.TabIndex = 0;
            this.pg1.PropertyValueChanged += new System.Windows.Forms.PropertyValueChangedEventHandler(this.pg1_PropertyValueChanged);
            // 
            // treeView1
            // 
            this.treeView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.treeView1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.treeView1.HideSelection = false;
            this.treeView1.Location = new System.Drawing.Point(0, 0);
            this.treeView1.Name = "treeView1";
            this.treeView1.Size = new System.Drawing.Size(348, 120);
            this.treeView1.TabIndex = 0;
            this.treeView1.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeView1_AfterSelect);
            // 
            // listBox1
            // 
            this.listBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBox1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBox1.FormattingEnabled = true;
            this.listBox1.IntegralHeight = false;
            this.listBox1.ItemHeight = 16;
            this.listBox1.Location = new System.Drawing.Point(2, 0);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(320, 527);
            this.listBox1.TabIndex = 0;
            this.listBox1.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged);
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.editToolStripMenuItem,
            this.optionsToolStripMenuItem,
            this.interpretToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1024, 24);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.loadToolStripMenuItem,
            this.savePccToolStripMenuItem,
            this.savePCCAsMenuItem,
            this.saveViewToolStripMenuItem1,
            this.saveViewToolStripMenuItem,
            this.toolStripSeparator1,
            this.loadAlternateTLKToolStripMenuItem,
            this.recentToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // loadToolStripMenuItem
            // 
            this.loadToolStripMenuItem.Name = "loadToolStripMenuItem";
            this.loadToolStripMenuItem.Size = new System.Drawing.Size(187, 22);
            this.loadToolStripMenuItem.Text = "Load";
            this.loadToolStripMenuItem.Click += new System.EventHandler(this.loadToolStripMenuItem_Click);
            // 
            // savePccToolStripMenuItem
            // 
            this.savePccToolStripMenuItem.Name = "savePccToolStripMenuItem";
            this.savePccToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.savePccToolStripMenuItem.Size = new System.Drawing.Size(187, 22);
            this.savePccToolStripMenuItem.Text = "Save pcc";
            this.savePccToolStripMenuItem.Click += new System.EventHandler(this.savePccToolStripMenuItem_Click_1);
            // 
            // savePCCAsMenuItem
            // 
            this.savePCCAsMenuItem.Name = "savePCCAsMenuItem";
            this.savePCCAsMenuItem.Size = new System.Drawing.Size(187, 22);
            this.savePCCAsMenuItem.Text = "Save pcc As";
            this.savePCCAsMenuItem.Click += new System.EventHandler(this.savePCCAsMenuItem_Click);
            // 
            // saveViewToolStripMenuItem1
            // 
            this.saveViewToolStripMenuItem1.Name = "saveViewToolStripMenuItem1";
            this.saveViewToolStripMenuItem1.Size = new System.Drawing.Size(187, 22);
            this.saveViewToolStripMenuItem1.Text = "Save View";
            this.saveViewToolStripMenuItem1.Click += new System.EventHandler(this.saveViewToolStripMenuItem1_Click);
            // 
            // saveViewToolStripMenuItem
            // 
            this.saveViewToolStripMenuItem.Name = "saveViewToolStripMenuItem";
            this.saveViewToolStripMenuItem.Size = new System.Drawing.Size(187, 22);
            this.saveViewToolStripMenuItem.Text = "Save Image";
            this.saveViewToolStripMenuItem.Click += new System.EventHandler(this.saveImageToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(184, 6);
            // 
            // loadAlternateTLKToolStripMenuItem
            // 
            this.loadAlternateTLKToolStripMenuItem.Name = "loadAlternateTLKToolStripMenuItem";
            this.loadAlternateTLKToolStripMenuItem.Size = new System.Drawing.Size(187, 22);
            this.loadAlternateTLKToolStripMenuItem.Text = "Manage Loaded TLKs";
            this.loadAlternateTLKToolStripMenuItem.Click += new System.EventHandler(this.loadAlternateTLKToolStripMenuItem_Click);
            // 
            // recentToolStripMenuItem
            // 
            this.recentToolStripMenuItem.Name = "recentToolStripMenuItem";
            this.recentToolStripMenuItem.Size = new System.Drawing.Size(187, 22);
            this.recentToolStripMenuItem.Text = "Recent";
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addObjectsToolStripMenuItem});
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.Size = new System.Drawing.Size(39, 20);
            this.editToolStripMenuItem.Text = "Edit";
            // 
            // addObjectsToolStripMenuItem
            // 
            this.addObjectsToolStripMenuItem.Name = "addObjectsToolStripMenuItem";
            this.addObjectsToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.addObjectsToolStripMenuItem.Text = "Add Object";
            this.addObjectsToolStripMenuItem.Click += new System.EventHandler(this.addObjectsToolStripMenuItem_Click);
            // 
            // optionsToolStripMenuItem
            // 
            this.optionsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.showOutputNumbersToolStripMenuItem,
            this.autoSaveViewToolStripMenuItem,
            this.useGlobalSequenceRefSavesToolStripMenuItem});
            this.optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            this.optionsToolStripMenuItem.Size = new System.Drawing.Size(61, 20);
            this.optionsToolStripMenuItem.Text = "Options";
            // 
            // showOutputNumbersToolStripMenuItem
            // 
            this.showOutputNumbersToolStripMenuItem.CheckOnClick = true;
            this.showOutputNumbersToolStripMenuItem.Name = "showOutputNumbersToolStripMenuItem";
            this.showOutputNumbersToolStripMenuItem.Size = new System.Drawing.Size(264, 22);
            this.showOutputNumbersToolStripMenuItem.Text = "Show Output Numbers";
            this.showOutputNumbersToolStripMenuItem.Click += new System.EventHandler(this.showOutputNumbersToolStripMenuItem_Click);
            // 
            // autoSaveViewToolStripMenuItem
            // 
            this.autoSaveViewToolStripMenuItem.CheckOnClick = true;
            this.autoSaveViewToolStripMenuItem.Name = "autoSaveViewToolStripMenuItem";
            this.autoSaveViewToolStripMenuItem.Size = new System.Drawing.Size(264, 22);
            this.autoSaveViewToolStripMenuItem.Text = "Auto-Save View";
            // 
            // useGlobalSequenceRefSavesToolStripMenuItem
            // 
            this.useGlobalSequenceRefSavesToolStripMenuItem.CheckOnClick = true;
            this.useGlobalSequenceRefSavesToolStripMenuItem.Name = "useGlobalSequenceRefSavesToolStripMenuItem";
            this.useGlobalSequenceRefSavesToolStripMenuItem.Size = new System.Drawing.Size(264, 22);
            this.useGlobalSequenceRefSavesToolStripMenuItem.Text = "Use Global Sequence Ref View Saves";
            this.useGlobalSequenceRefSavesToolStripMenuItem.Click += new System.EventHandler(this.useGlobalSequenceRefSavesToolStripMenuItem_Click);
            // 
            // interpretToolStripMenuItem
            // 
            this.interpretToolStripMenuItem.Name = "interpretToolStripMenuItem";
            this.interpretToolStripMenuItem.Size = new System.Drawing.Size(64, 20);
            this.interpretToolStripMenuItem.Text = "Interpret";
            this.interpretToolStripMenuItem.Click += new System.EventHandler(this.interpretToolStripMenuItem_Click);
            // 
            // toolStrip1
            // 
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripTextBox1,
            this.toolStripButton1});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(652, 25);
            this.toolStrip1.TabIndex = 2;
            this.toolStrip1.Text = "toolStrip1";
            this.toolStrip1.Visible = false;
            // 
            // toolStripTextBox1
            // 
            this.toolStripTextBox1.Name = "toolStripTextBox1";
            this.toolStripTextBox1.Size = new System.Drawing.Size(100, 25);
            // 
            // toolStripButton1
            // 
            this.toolStripButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButton1.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton1.Image")));
            this.toolStripButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButton1.Name = "toolStripButton1";
            this.toolStripButton1.Size = new System.Drawing.Size(38, 22);
            this.toolStripButton1.Text = "Scale";
            this.toolStripButton1.Click += new System.EventHandler(this.toolStripButton1_Click);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openInPackageEditorToolStripMenuItem,
            this.openInInterpEditorToolStripMenuItem,
            this.cloneToolStripMenuItem,
            this.breakLinksToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(198, 92);
            // 
            // openInPackageEditorToolStripMenuItem
            // 
            this.openInPackageEditorToolStripMenuItem.Name = "openInPackageEditorToolStripMenuItem";
            this.openInPackageEditorToolStripMenuItem.Size = new System.Drawing.Size(197, 22);
            this.openInPackageEditorToolStripMenuItem.Text = "Open in Package Editor";
            this.openInPackageEditorToolStripMenuItem.Click += new System.EventHandler(this.openInPackageEditorToolStripMenuItem_Click);
            // 
            // openInInterpEditorToolStripMenuItem
            // 
            this.openInInterpEditorToolStripMenuItem.Name = "openInInterpEditorToolStripMenuItem";
            this.openInInterpEditorToolStripMenuItem.Size = new System.Drawing.Size(197, 22);
            this.openInInterpEditorToolStripMenuItem.Text = "Open in InterpViewer";
            this.openInInterpEditorToolStripMenuItem.Visible = false;
            this.openInInterpEditorToolStripMenuItem.Click += new System.EventHandler(this.openInInterpEditorToolStripMenuItem_Click);
            // 
            // cloneToolStripMenuItem
            // 
            this.cloneToolStripMenuItem.Name = "cloneToolStripMenuItem";
            this.cloneToolStripMenuItem.Size = new System.Drawing.Size(197, 22);
            this.cloneToolStripMenuItem.Text = "Clone";
            this.cloneToolStripMenuItem.Click += new System.EventHandler(this.cloneToolStripMenuItem_Click);
            // 
            // breakLinksToolStripMenuItem
            // 
            this.breakLinksToolStripMenuItem.Name = "breakLinksToolStripMenuItem";
            this.breakLinksToolStripMenuItem.Size = new System.Drawing.Size(197, 22);
            this.breakLinksToolStripMenuItem.Text = "Break Links";
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1,
            this.toolStripStatusLabel2});
            this.statusStrip1.Location = new System.Drawing.Point(0, 554);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(1024, 22);
            this.statusStrip1.TabIndex = 3;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            this.toolStripStatusLabel1.ForeColor = System.Drawing.Color.MediumBlue;
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(10, 17);
            this.toolStripStatusLabel1.Text = " ";
            // 
            // toolStripStatusLabel2
            // 
            this.toolStripStatusLabel2.ForeColor = System.Drawing.SystemColors.ControlText;
            this.toolStripStatusLabel2.Name = "toolStripStatusLabel2";
            this.toolStripStatusLabel2.Size = new System.Drawing.Size(10, 17);
            this.toolStripStatusLabel2.Text = " ";
            // 
            // contextMenuStrip2
            // 
            this.contextMenuStrip2.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addObjectToolStripMenuItem});
            this.contextMenuStrip2.Name = "contextMenuStrip2";
            this.contextMenuStrip2.Size = new System.Drawing.Size(135, 26);
            // 
            // addObjectToolStripMenuItem
            // 
            this.addObjectToolStripMenuItem.Name = "addObjectToolStripMenuItem";
            this.addObjectToolStripMenuItem.Size = new System.Drawing.Size(134, 22);
            this.addObjectToolStripMenuItem.Text = "Add Object";
            this.addObjectToolStripMenuItem.Click += new System.EventHandler(this.addObjectsToolStripMenuItem_Click);
            // 
            // SequenceEditor
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1024, 576);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "SequenceEditor";
            this.Text = "SequenceEditor";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.SequenceEditor_FormClosing);
            this.Load += new System.EventHandler(this.SequenceEditor_Load);
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.SequenceEditor_DragDrop);
            this.DragEnter += new System.Windows.Forms.DragEventHandler(this.SequenceEditor_DragEnter);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.splitContainer3.Panel1.ResumeLayout(false);
            this.splitContainer3.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).EndInit();
            this.splitContainer3.ResumeLayout(false);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.contextMenuStrip1.ResumeLayout(false);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.contextMenuStrip2.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private UMD.HCIL.GraphEditor.GraphEditor graphEditor;
        private System.Windows.Forms.SplitContainer splitContainer3;
        private System.Windows.Forms.PropertyGrid pg1;
        private System.Windows.Forms.TreeView treeView1;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loadToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveViewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem savePccToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem interpretToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveViewToolStripMenuItem1;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripTextBox toolStripTextBox1;
        private System.Windows.Forms.ToolStripButton toolStripButton1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem openInPackageEditorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem breakLinksToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addObjectsToolStripMenuItem;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel2;
        private System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showOutputNumbersToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem autoSaveViewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openInInterpEditorToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem useGlobalSequenceRefSavesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loadAlternateTLKToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem savePCCAsMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip2;
        private System.Windows.Forms.ToolStripMenuItem addObjectToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem cloneToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem recentToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
    }
}