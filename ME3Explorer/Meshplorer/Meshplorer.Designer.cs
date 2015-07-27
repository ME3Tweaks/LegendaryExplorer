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
            this.loadFromDatabaseToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.selectMatForSectionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lODToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lOD1ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lOD2ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lOD3ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lOD4ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.transferToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportToPSKToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportTo3DSToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.serializeToFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importFromPSKToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importFromUDKToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.importOptionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rotatingToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.debugToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportTreeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.dumpBinaryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripComboBox1 = new System.Windows.Forms.ToolStripComboBox();
            this.toolStripButton1 = new System.Windows.Forms.ToolStripButton();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.splitContainer3 = new System.Windows.Forms.SplitContainer();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.treeView1 = new System.Windows.Forms.TreeView();
            this.rtb1 = new System.Windows.Forms.RichTextBox();
            this.pb1 = new System.Windows.Forms.PictureBox();
            this.hb1 = new Be.Windows.Forms.HexBox();
            this.menuStrip1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
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
            ((System.ComponentModel.ISupportInitialize)(this.pb1)).BeginInit();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.editToolStripMenuItem,
            this.lODToolStripMenuItem,
            this.transferToolStripMenuItem,
            this.viewToolStripMenuItem,
            this.debugToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(639, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            this.menuStrip1.Visible = false;
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.loadPCCToolStripMenuItem,
            this.loadFromDatabaseToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(35, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // loadPCCToolStripMenuItem
            // 
            this.loadPCCToolStripMenuItem.Name = "loadPCCToolStripMenuItem";
            this.loadPCCToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            this.loadPCCToolStripMenuItem.Text = "Load PCC";
            this.loadPCCToolStripMenuItem.Click += new System.EventHandler(this.loadPCCToolStripMenuItem_Click);
            // 
            // loadFromDatabaseToolStripMenuItem
            // 
            this.loadFromDatabaseToolStripMenuItem.Name = "loadFromDatabaseToolStripMenuItem";
            this.loadFromDatabaseToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            this.loadFromDatabaseToolStripMenuItem.Text = "Load from Database";
            this.loadFromDatabaseToolStripMenuItem.Click += new System.EventHandler(this.loadFromDatabaseToolStripMenuItem_Click);
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.selectMatForSectionToolStripMenuItem});
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.editToolStripMenuItem.Text = "Edit";
            // 
            // selectMatForSectionToolStripMenuItem
            // 
            this.selectMatForSectionToolStripMenuItem.Name = "selectMatForSectionToolStripMenuItem";
            this.selectMatForSectionToolStripMenuItem.Size = new System.Drawing.Size(179, 22);
            this.selectMatForSectionToolStripMenuItem.Text = "Select Mat for Section";
            this.selectMatForSectionToolStripMenuItem.Click += new System.EventHandler(this.selectMatForSectionToolStripMenuItem_Click);
            // 
            // lODToolStripMenuItem
            // 
            this.lODToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lOD1ToolStripMenuItem,
            this.lOD2ToolStripMenuItem,
            this.lOD3ToolStripMenuItem,
            this.lOD4ToolStripMenuItem});
            this.lODToolStripMenuItem.Name = "lODToolStripMenuItem";
            this.lODToolStripMenuItem.Size = new System.Drawing.Size(39, 20);
            this.lODToolStripMenuItem.Text = "LOD";
            this.lODToolStripMenuItem.Visible = false;
            // 
            // lOD1ToolStripMenuItem
            // 
            this.lOD1ToolStripMenuItem.Name = "lOD1ToolStripMenuItem";
            this.lOD1ToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F1;
            this.lOD1ToolStripMenuItem.Size = new System.Drawing.Size(122, 22);
            this.lOD1ToolStripMenuItem.Text = "LOD 1";
            this.lOD1ToolStripMenuItem.Click += new System.EventHandler(this.lOD1ToolStripMenuItem_Click);
            // 
            // lOD2ToolStripMenuItem
            // 
            this.lOD2ToolStripMenuItem.Name = "lOD2ToolStripMenuItem";
            this.lOD2ToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F2;
            this.lOD2ToolStripMenuItem.Size = new System.Drawing.Size(122, 22);
            this.lOD2ToolStripMenuItem.Text = "LOD 2";
            this.lOD2ToolStripMenuItem.Click += new System.EventHandler(this.lOD2ToolStripMenuItem_Click);
            // 
            // lOD3ToolStripMenuItem
            // 
            this.lOD3ToolStripMenuItem.Name = "lOD3ToolStripMenuItem";
            this.lOD3ToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F3;
            this.lOD3ToolStripMenuItem.Size = new System.Drawing.Size(122, 22);
            this.lOD3ToolStripMenuItem.Text = "LOD 3";
            this.lOD3ToolStripMenuItem.Click += new System.EventHandler(this.lOD3ToolStripMenuItem_Click);
            // 
            // lOD4ToolStripMenuItem
            // 
            this.lOD4ToolStripMenuItem.Name = "lOD4ToolStripMenuItem";
            this.lOD4ToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F4;
            this.lOD4ToolStripMenuItem.Size = new System.Drawing.Size(122, 22);
            this.lOD4ToolStripMenuItem.Text = "LOD 4";
            this.lOD4ToolStripMenuItem.Click += new System.EventHandler(this.lOD4ToolStripMenuItem_Click);
            // 
            // transferToolStripMenuItem
            // 
            this.transferToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportToPSKToolStripMenuItem,
            this.exportTo3DSToolStripMenuItem,
            this.serializeToFileToolStripMenuItem,
            this.importFromPSKToolStripMenuItem,
            this.importFromUDKToolStripMenuItem,
            this.toolStripMenuItem1,
            this.importOptionsToolStripMenuItem});
            this.transferToolStripMenuItem.Name = "transferToolStripMenuItem";
            this.transferToolStripMenuItem.Size = new System.Drawing.Size(60, 20);
            this.transferToolStripMenuItem.Text = "Transfer";
            // 
            // exportToPSKToolStripMenuItem
            // 
            this.exportToPSKToolStripMenuItem.Name = "exportToPSKToolStripMenuItem";
            this.exportToPSKToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.exportToPSKToolStripMenuItem.Text = "Export to PSK";
            this.exportToPSKToolStripMenuItem.Click += new System.EventHandler(this.exportToPSKToolStripMenuItem_Click);
            // 
            // exportTo3DSToolStripMenuItem
            // 
            this.exportTo3DSToolStripMenuItem.Name = "exportTo3DSToolStripMenuItem";
            this.exportTo3DSToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.exportTo3DSToolStripMenuItem.Text = "Export to 3DS";
            this.exportTo3DSToolStripMenuItem.Click += new System.EventHandler(this.exportTo3DSToolStripMenuItem_Click);
            // 
            // serializeToFileToolStripMenuItem
            // 
            this.serializeToFileToolStripMenuItem.Name = "serializeToFileToolStripMenuItem";
            this.serializeToFileToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.serializeToFileToolStripMenuItem.Text = "Serialize to file";
            this.serializeToFileToolStripMenuItem.Click += new System.EventHandler(this.serializeToFileToolStripMenuItem_Click);
            // 
            // importFromPSKToolStripMenuItem
            // 
            this.importFromPSKToolStripMenuItem.Name = "importFromPSKToolStripMenuItem";
            this.importFromPSKToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.importFromPSKToolStripMenuItem.Text = "Import from PSK";
            this.importFromPSKToolStripMenuItem.Click += new System.EventHandler(this.importFromPSKToolStripMenuItem_Click);
            // 
            // importFromUDKToolStripMenuItem
            // 
            this.importFromUDKToolStripMenuItem.Name = "importFromUDKToolStripMenuItem";
            this.importFromUDKToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.importFromUDKToolStripMenuItem.Text = "Import from UDK...";
            this.importFromUDKToolStripMenuItem.Click += new System.EventHandler(this.importFromUDKToolStripMenuItem_Click);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(163, 6);
            // 
            // importOptionsToolStripMenuItem
            // 
            this.importOptionsToolStripMenuItem.Name = "importOptionsToolStripMenuItem";
            this.importOptionsToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.importOptionsToolStripMenuItem.Text = "Import Options";
            this.importOptionsToolStripMenuItem.Click += new System.EventHandler(this.importOptionsToolStripMenuItem_Click);
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.rotatingToolStripMenuItem});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(41, 20);
            this.viewToolStripMenuItem.Text = "View";
            // 
            // rotatingToolStripMenuItem
            // 
            this.rotatingToolStripMenuItem.Checked = true;
            this.rotatingToolStripMenuItem.CheckOnClick = true;
            this.rotatingToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.rotatingToolStripMenuItem.Name = "rotatingToolStripMenuItem";
            this.rotatingToolStripMenuItem.Size = new System.Drawing.Size(115, 22);
            this.rotatingToolStripMenuItem.Text = "Rotating";
            this.rotatingToolStripMenuItem.Click += new System.EventHandler(this.rotatingToolStripMenuItem_Click);
            // 
            // debugToolStripMenuItem
            // 
            this.debugToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportTreeToolStripMenuItem,
            this.dumpBinaryToolStripMenuItem});
            this.debugToolStripMenuItem.Name = "debugToolStripMenuItem";
            this.debugToolStripMenuItem.Size = new System.Drawing.Size(50, 20);
            this.debugToolStripMenuItem.Text = "Debug";
            // 
            // exportTreeToolStripMenuItem
            // 
            this.exportTreeToolStripMenuItem.Name = "exportTreeToolStripMenuItem";
            this.exportTreeToolStripMenuItem.Size = new System.Drawing.Size(134, 22);
            this.exportTreeToolStripMenuItem.Text = "Export Tree";
            this.exportTreeToolStripMenuItem.Click += new System.EventHandler(this.exportTreeToolStripMenuItem_Click);
            // 
            // dumpBinaryToolStripMenuItem
            // 
            this.dumpBinaryToolStripMenuItem.Name = "dumpBinaryToolStripMenuItem";
            this.dumpBinaryToolStripMenuItem.Size = new System.Drawing.Size(134, 22);
            this.dumpBinaryToolStripMenuItem.Text = "Dump Binary";
            this.dumpBinaryToolStripMenuItem.Click += new System.EventHandler(this.dumpBinaryToolStripMenuItem_Click_1);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Location = new System.Drawing.Point(0, 478);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(639, 22);
            this.statusStrip1.TabIndex = 1;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripComboBox1,
            this.toolStripButton1});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(639, 25);
            this.toolStrip1.TabIndex = 2;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // toolStripComboBox1
            // 
            this.toolStripComboBox1.AutoSize = false;
            this.toolStripComboBox1.Name = "toolStripComboBox1";
            this.toolStripComboBox1.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            this.toolStripComboBox1.Size = new System.Drawing.Size(500, 21);
            // 
            // toolStripButton1
            // 
            this.toolStripButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButton1.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton1.Image")));
            this.toolStripButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButton1.Name = "toolStripButton1";
            this.toolStripButton1.Size = new System.Drawing.Size(82, 22);
            this.toolStripButton1.Text = "Set as material";
            this.toolStripButton1.Click += new System.EventHandler(this.toolStripButton1_Click);
            // 
            // timer1
            // 
            this.timer1.Interval = 16;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 25);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.splitContainer2);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.hb1);
            this.splitContainer1.Size = new System.Drawing.Size(639, 453);
            this.splitContainer1.SplitterDistance = 340;
            this.splitContainer1.TabIndex = 3;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.splitContainer3);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.rtb1);
            this.splitContainer2.Panel2.Controls.Add(this.pb1);
            this.splitContainer2.Size = new System.Drawing.Size(639, 340);
            this.splitContainer2.SplitterDistance = 438;
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
            this.splitContainer3.Size = new System.Drawing.Size(438, 340);
            this.splitContainer3.SplitterDistance = 100;
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
            this.listBox1.Size = new System.Drawing.Size(100, 340);
            this.listBox1.TabIndex = 0;
            this.listBox1.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged_1);
            // 
            // treeView1
            // 
            this.treeView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.treeView1.Location = new System.Drawing.Point(0, 0);
            this.treeView1.Name = "treeView1";
            this.treeView1.Size = new System.Drawing.Size(334, 340);
            this.treeView1.TabIndex = 0;
            this.treeView1.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeView1_AfterSelect);
            // 
            // rtb1
            // 
            this.rtb1.DetectUrls = false;
            this.rtb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb1.Font = new System.Drawing.Font("Courier New", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtb1.Location = new System.Drawing.Point(0, 0);
            this.rtb1.Name = "rtb1";
            this.rtb1.Size = new System.Drawing.Size(197, 340);
            this.rtb1.TabIndex = 1;
            this.rtb1.Text = "";
            this.rtb1.Visible = false;
            this.rtb1.WordWrap = false;
            // 
            // pb1
            // 
            this.pb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pb1.Location = new System.Drawing.Point(0, 0);
            this.pb1.Name = "pb1";
            this.pb1.Size = new System.Drawing.Size(197, 340);
            this.pb1.TabIndex = 0;
            this.pb1.TabStop = false;
            // 
            // hb1
            // 
            this.hb1.BoldFont = null;
            this.hb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.hb1.Font = new System.Drawing.Font("Courier New", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.hb1.LineInfoForeColor = System.Drawing.Color.Empty;
            this.hb1.LineInfoVisible = true;
            this.hb1.Location = new System.Drawing.Point(0, 0);
            this.hb1.Name = "hb1";
            this.hb1.ReadOnly = true;
            this.hb1.ShadowSelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(60)))), ((int)(((byte)(188)))), ((int)(((byte)(255)))));
            this.hb1.Size = new System.Drawing.Size(639, 109);
            this.hb1.StringViewVisible = true;
            this.hb1.TabIndex = 0;
            this.hb1.UseFixedBytesPerLine = true;
            this.hb1.VScrollBarVisible = true;
            // 
            // Meshplorer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(639, 500);
            this.Controls.Add(this.splitContainer1);
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
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Meshplorer_KeyDown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.Meshplorer_KeyUp);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
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
            ((System.ComponentModel.ISupportInitialize)(this.pb1)).EndInit();
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
        public System.Windows.Forms.ToolStripMenuItem exportToPSKToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem serializeToFileToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem importFromPSKToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem lODToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem lOD1ToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem lOD2ToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem lOD3ToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem lOD4ToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem rotatingToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem debugToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem exportTreeToolStripMenuItem;
        public System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        public System.Windows.Forms.ToolStripMenuItem importOptionsToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem loadFromDatabaseToolStripMenuItem;
        private System.ComponentModel.IContainer components;
        private System.Windows.Forms.ToolStripMenuItem exportTo3DSToolStripMenuItem;
        private System.Windows.Forms.SplitContainer splitContainer1;
        public System.Windows.Forms.SplitContainer splitContainer2;
        public System.Windows.Forms.SplitContainer splitContainer3;
        public System.Windows.Forms.ListBox listBox1;
        public System.Windows.Forms.TreeView treeView1;
        public System.Windows.Forms.RichTextBox rtb1;
        public System.Windows.Forms.PictureBox pb1;
        private Be.Windows.Forms.HexBox hb1;
        public System.Windows.Forms.ToolStripMenuItem dumpBinaryToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importFromUDKToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem selectMatForSectionToolStripMenuItem;
        private System.Windows.Forms.ToolStripComboBox toolStripComboBox1;
        private System.Windows.Forms.ToolStripButton toolStripButton1;
    }
}