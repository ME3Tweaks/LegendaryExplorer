namespace ME3Explorer.Meshplorer
{
    partial class Meshplorer
    {

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
        public void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Meshplorer));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadPCCToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.savePCCToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.recentToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lODToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lOD0ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lOD1ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lOD2ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lOD3ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.transferToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportToOBJToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importFromOBJToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rotatingToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.wireframeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.solidToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.firstPersonToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.debugToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportTreeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.dumpBinaryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.MaterialBox = new System.Windows.Forms.ToolStripComboBox();
            this.MaterialIndexBox = new System.Windows.Forms.ToolStripComboBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.splitContainer3 = new System.Windows.Forms.SplitContainer();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.treeView1 = new System.Windows.Forms.TreeView();
            this.view = new ME3Explorer.Scene3D.SceneRenderControl();
            this.rtb1 = new System.Windows.Forms.RichTextBox();
            this.menuStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).BeginInit();
            this.splitContainer3.Panel1.SuspendLayout();
            this.splitContainer3.Panel2.SuspendLayout();
            this.splitContainer3.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.lODToolStripMenuItem,
            this.transferToolStripMenuItem,
            this.viewToolStripMenuItem,
            this.debugToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1184, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.loadPCCToolStripMenuItem,
            this.savePCCToolStripMenuItem,
            this.toolStripSeparator1,
            this.recentToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // loadPCCToolStripMenuItem
            // 
            this.loadPCCToolStripMenuItem.Name = "loadPCCToolStripMenuItem";
            this.loadPCCToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.loadPCCToolStripMenuItem.Text = "Load PCC";
            this.loadPCCToolStripMenuItem.Click += new System.EventHandler(this.loadPCCToolStripMenuItem_Click);
            // 
            // savePCCToolStripMenuItem
            // 
            this.savePCCToolStripMenuItem.Name = "savePCCToolStripMenuItem";
            this.savePCCToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.savePCCToolStripMenuItem.Text = "Save PCC";
            this.savePCCToolStripMenuItem.Click += new System.EventHandler(this.savePCCToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(177, 6);
            // 
            // recentToolStripMenuItem
            // 
            this.recentToolStripMenuItem.Name = "recentToolStripMenuItem";
            this.recentToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.recentToolStripMenuItem.Text = "Recent";
            // 
            // lODToolStripMenuItem
            // 
            this.lODToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lOD0ToolStripMenuItem,
            this.lOD1ToolStripMenuItem,
            this.lOD2ToolStripMenuItem,
            this.lOD3ToolStripMenuItem});
            this.lODToolStripMenuItem.Name = "lODToolStripMenuItem";
            this.lODToolStripMenuItem.Size = new System.Drawing.Size(42, 20);
            this.lODToolStripMenuItem.Text = "LOD";
            this.lODToolStripMenuItem.Visible = false;
            // 
            // lOD0ToolStripMenuItem
            // 
            this.lOD0ToolStripMenuItem.Name = "lOD0ToolStripMenuItem";
            this.lOD0ToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F1;
            this.lOD0ToolStripMenuItem.Size = new System.Drawing.Size(125, 22);
            this.lOD0ToolStripMenuItem.Text = "LOD 0";
            this.lOD0ToolStripMenuItem.Click += new System.EventHandler(this.lOD0ToolStripMenuItem_Click);
            // 
            // lOD1ToolStripMenuItem
            // 
            this.lOD1ToolStripMenuItem.Name = "lOD1ToolStripMenuItem";
            this.lOD1ToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F2;
            this.lOD1ToolStripMenuItem.Size = new System.Drawing.Size(125, 22);
            this.lOD1ToolStripMenuItem.Text = "LOD 1";
            this.lOD1ToolStripMenuItem.Click += new System.EventHandler(this.lOD1ToolStripMenuItem_Click);
            // 
            // lOD2ToolStripMenuItem
            // 
            this.lOD2ToolStripMenuItem.Name = "lOD2ToolStripMenuItem";
            this.lOD2ToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F3;
            this.lOD2ToolStripMenuItem.Size = new System.Drawing.Size(125, 22);
            this.lOD2ToolStripMenuItem.Text = "LOD 2";
            this.lOD2ToolStripMenuItem.Click += new System.EventHandler(this.lOD2ToolStripMenuItem_Click);
            // 
            // lOD3ToolStripMenuItem
            // 
            this.lOD3ToolStripMenuItem.Name = "lOD3ToolStripMenuItem";
            this.lOD3ToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F4;
            this.lOD3ToolStripMenuItem.Size = new System.Drawing.Size(125, 22);
            this.lOD3ToolStripMenuItem.Text = "LOD 3";
            this.lOD3ToolStripMenuItem.Click += new System.EventHandler(this.lOD3ToolStripMenuItem_Click);
            // 
            // transferToolStripMenuItem
            // 
            this.transferToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportToOBJToolStripMenuItem,
            this.importFromOBJToolStripMenuItem,
            this.toolStripMenuItem1,});
            this.transferToolStripMenuItem.Name = "transferToolStripMenuItem";
            this.transferToolStripMenuItem.Size = new System.Drawing.Size(61, 20);
            this.transferToolStripMenuItem.Text = "Transfer";
            // 
            // exportToOBJToolStripMenuItem
            // 
            this.exportToOBJToolStripMenuItem.Name = "exportToOBJToolStripMenuItem";
            this.exportToOBJToolStripMenuItem.Size = new System.Drawing.Size(174, 22);
            this.exportToOBJToolStripMenuItem.Text = "Export to OBJ";
            this.exportToOBJToolStripMenuItem.Click += new System.EventHandler(this.exportToOBJToolStripMenuItem_Click);
            // 
            // importFromOBJToolStripMenuItem
            // 
            this.importFromOBJToolStripMenuItem.Name = "importFromOBJToolStripMenuItem";
            this.importFromOBJToolStripMenuItem.Size = new System.Drawing.Size(174, 22);
            this.importFromOBJToolStripMenuItem.Text = "Import from OBJ";
            this.importFromOBJToolStripMenuItem.Click += new System.EventHandler(this.importFromOBJToolStripMenuItem_Click);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(171, 6);
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.rotatingToolStripMenuItem,
            this.wireframeToolStripMenuItem,
            this.solidToolStripMenuItem,
            this.firstPersonToolStripMenuItem});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.viewToolStripMenuItem.Text = "View";
            // 
            // rotatingToolStripMenuItem
            // 
            this.rotatingToolStripMenuItem.CheckOnClick = true;
            this.rotatingToolStripMenuItem.Name = "rotatingToolStripMenuItem";
            this.rotatingToolStripMenuItem.Size = new System.Drawing.Size(135, 22);
            this.rotatingToolStripMenuItem.Text = "Rotating";
            // 
            // wireframeToolStripMenuItem
            // 
            this.wireframeToolStripMenuItem.CheckOnClick = true;
            this.wireframeToolStripMenuItem.Name = "wireframeToolStripMenuItem";
            this.wireframeToolStripMenuItem.Size = new System.Drawing.Size(135, 22);
            this.wireframeToolStripMenuItem.Text = "Wireframe";
            // 
            // solidToolStripMenuItem
            // 
            this.solidToolStripMenuItem.Checked = true;
            this.solidToolStripMenuItem.CheckOnClick = true;
            this.solidToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.solidToolStripMenuItem.Name = "solidToolStripMenuItem";
            this.solidToolStripMenuItem.Size = new System.Drawing.Size(135, 22);
            this.solidToolStripMenuItem.Text = "Solid";
            // 
            // firstPersonToolStripMenuItem
            // 
            this.firstPersonToolStripMenuItem.CheckOnClick = true;
            this.firstPersonToolStripMenuItem.Name = "firstPersonToolStripMenuItem";
            this.firstPersonToolStripMenuItem.Size = new System.Drawing.Size(135, 22);
            this.firstPersonToolStripMenuItem.Text = "First Person";
            this.firstPersonToolStripMenuItem.Click += new System.EventHandler(this.firstPersonToolStripMenuItem_Click);
            // 
            // debugToolStripMenuItem
            // 
            this.debugToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportTreeToolStripMenuItem,
            this.dumpBinaryToolStripMenuItem});
            this.debugToolStripMenuItem.Name = "debugToolStripMenuItem";
            this.debugToolStripMenuItem.Size = new System.Drawing.Size(54, 20);
            this.debugToolStripMenuItem.Text = "Debug";
            // 
            // exportTreeToolStripMenuItem
            // 
            this.exportTreeToolStripMenuItem.Name = "exportTreeToolStripMenuItem";
            this.exportTreeToolStripMenuItem.Size = new System.Drawing.Size(143, 22);
            this.exportTreeToolStripMenuItem.Text = "Export Tree";
            this.exportTreeToolStripMenuItem.Click += new System.EventHandler(this.exportTreeToolStripMenuItem_Click);
            // 
            // dumpBinaryToolStripMenuItem
            // 
            this.dumpBinaryToolStripMenuItem.Name = "dumpBinaryToolStripMenuItem";
            this.dumpBinaryToolStripMenuItem.Size = new System.Drawing.Size(143, 22);
            this.dumpBinaryToolStripMenuItem.Text = "Dump Binary";
            this.dumpBinaryToolStripMenuItem.Click += new System.EventHandler(this.dumpBinaryToolStripMenuItem_Click_1);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblStatus});
            this.statusStrip1.Location = new System.Drawing.Point(0, 739);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(1184, 22);
            this.statusStrip1.TabIndex = 1;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // lblStatus
            // 
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(230, 17);
            this.lblStatus.Text = "Select an ME3 package file to view meshes";
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.MaterialBox,
            this.MaterialIndexBox});
            this.toolStrip1.Location = new System.Drawing.Point(0, 24);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(1184, 25);
            this.toolStrip1.TabIndex = 2;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // MaterialBox
            // 
            this.MaterialBox.AutoSize = false;
            this.MaterialBox.Name = "MaterialBox";
            this.MaterialBox.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            this.MaterialBox.Size = new System.Drawing.Size(500, 23);
            this.MaterialBox.Visible = false;
            // 
            // MaterialIndexBox
            // 
            this.MaterialIndexBox.AutoSize = false;
            this.MaterialIndexBox.DropDownWidth = 400;
            this.MaterialIndexBox.Name = "MaterialIndexBox";
            this.MaterialIndexBox.Size = new System.Drawing.Size(400, 23);
            this.MaterialIndexBox.Visible = false;
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Interval = 10;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 49);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.splitContainer3);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.view);
            this.splitContainer2.Panel2.Controls.Add(this.rtb1);
            this.splitContainer2.Size = new System.Drawing.Size(1184, 690);
            this.splitContainer2.SplitterDistance = 618;
            this.splitContainer2.TabIndex = 4;
            // 
            // splitContainer3
            // 
            this.splitContainer3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer3.Location = new System.Drawing.Point(0, 0);
            this.splitContainer3.Name = "splitContainer3";
            // 
            // splitContainer3.Panel1
            // 
            this.splitContainer3.Panel1.Controls.Add(this.listBox1);
            // 
            // splitContainer3.Panel2
            // 
            this.splitContainer3.Panel2.Controls.Add(this.treeView1);
            this.splitContainer3.Size = new System.Drawing.Size(618, 690);
            this.splitContainer3.SplitterDistance = 317;
            this.splitContainer3.TabIndex = 0;
            // 
            // listBox1
            // 
            this.listBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBox1.FormattingEnabled = true;
            this.listBox1.HorizontalScrollbar = true;
            this.listBox1.IntegralHeight = false;
            this.listBox1.ItemHeight = 16;
            this.listBox1.Location = new System.Drawing.Point(0, 0);
            this.listBox1.Name = "listBox1";
            this.listBox1.ScrollAlwaysVisible = true;
            this.listBox1.Size = new System.Drawing.Size(317, 690);
            this.listBox1.TabIndex = 0;
            this.listBox1.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged_1);
            // 
            // treeView1
            // 
            this.treeView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.treeView1.HideSelection = false;
            this.treeView1.Location = new System.Drawing.Point(0, 0);
            this.treeView1.Name = "treeView1";
            this.treeView1.Size = new System.Drawing.Size(297, 690);
            this.treeView1.TabIndex = 0;
            this.treeView1.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeView1_AfterSelect);
            // 
            // view
            // 
            this.view.Dock = System.Windows.Forms.DockStyle.Fill;
            this.view.Location = new System.Drawing.Point(0, 0);
            this.view.Name = "view";
            this.view.Size = new System.Drawing.Size(562, 690);
            this.view.TabIndex = 1;
            this.view.TabStop = false;
            this.view.Context.Wireframe = false;
            this.view.Update += new System.EventHandler<float>(this.view_Update);
            this.view.Render += new System.EventHandler(this.view_Render);
            // 
            // rtb1
            // 
            this.rtb1.DetectUrls = false;
            this.rtb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb1.Font = new System.Drawing.Font("Courier New", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtb1.Location = new System.Drawing.Point(0, 0);
            this.rtb1.Name = "rtb1";
            this.rtb1.Size = new System.Drawing.Size(562, 690);
            this.rtb1.TabIndex = 1;
            this.rtb1.Text = "";
            this.rtb1.Visible = false;
            this.rtb1.WordWrap = false;
            // 
            // Meshplorer
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1184, 761);
            this.Controls.Add(this.splitContainer2);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Meshplorer";
            this.Text = "Meshplorer";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Meshplorer_FormClosing);
            this.Load += new System.EventHandler(this.Meshplorer_Load);
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.meshplorer_DragDrop);
            this.DragEnter += new System.Windows.Forms.DragEventHandler(this.meshplorer_DragEnter);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Meshplorer_KeyDown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.Meshplorer_KeyUp);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.splitContainer3.Panel1.ResumeLayout(false);
            this.splitContainer3.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).EndInit();
            this.splitContainer3.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        public System.Windows.Forms.MenuStrip menuStrip1;
        public System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem loadPCCToolStripMenuItem;
        public System.Windows.Forms.StatusStrip statusStrip1;
        public System.Windows.Forms.ToolStrip toolStrip1;
        public System.Windows.Forms.Timer timer1;
        public System.Windows.Forms.ToolStripMenuItem transferToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem lODToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem lOD0ToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem lOD1ToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem lOD2ToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem lOD3ToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem rotatingToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem debugToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem exportTreeToolStripMenuItem;
        public System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.ComponentModel.IContainer components;
        public System.Windows.Forms.SplitContainer splitContainer2;
        public System.Windows.Forms.SplitContainer splitContainer3;
        public System.Windows.Forms.ListBox listBox1;
        public System.Windows.Forms.TreeView treeView1;
        public System.Windows.Forms.RichTextBox rtb1;
        public System.Windows.Forms.ToolStripMenuItem dumpBinaryToolStripMenuItem;
        private System.Windows.Forms.ToolStripComboBox MaterialBox;
        private System.Windows.Forms.ToolStripMenuItem savePCCToolStripMenuItem;
        private Scene3D.SceneRenderControl view;
        private System.Windows.Forms.ToolStripMenuItem wireframeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem solidToolStripMenuItem;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
        private System.Windows.Forms.ToolStripMenuItem firstPersonToolStripMenuItem;
        private System.Windows.Forms.ToolStripComboBox MaterialIndexBox;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem recentToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportToOBJToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importFromOBJToolStripMenuItem;
    }
}