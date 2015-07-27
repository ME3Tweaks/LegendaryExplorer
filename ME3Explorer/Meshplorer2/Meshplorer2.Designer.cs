namespace ME3Explorer.Meshplorer2
{
    partial class Meshplorer2
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Meshplorer2));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.globalTreeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.scanToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.previewWithTreeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rotateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importBonesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.transferToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importFromUDKToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openUPKToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importLODToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripTextBox1 = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripButton1 = new System.Windows.Forms.ToolStripButton();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.treeView1 = new System.Windows.Forms.TreeView();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.pb1 = new System.Windows.Forms.PictureBox();
            this.treeView2 = new System.Windows.Forms.TreeView();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.pbar1 = new System.Windows.Forms.ToolStripProgressBar();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.splitContainer3 = new System.Windows.Forms.SplitContainer();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.listBox2 = new System.Windows.Forms.ListBox();
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
            ((System.ComponentModel.ISupportInitialize)(this.pb1)).BeginInit();
            this.statusStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).BeginInit();
            this.splitContainer3.Panel1.SuspendLayout();
            this.splitContainer3.Panel2.SuspendLayout();
            this.splitContainer3.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.globalTreeToolStripMenuItem,
            this.optionsToolStripMenuItem,
            this.transferToolStripMenuItem,
            this.fileToolStripMenuItem,
            this.importLODToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(653, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            this.menuStrip1.Visible = false;
            // 
            // globalTreeToolStripMenuItem
            // 
            this.globalTreeToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.loadToolStripMenuItem,
            this.scanToolStripMenuItem,
            this.saveToolStripMenuItem});
            this.globalTreeToolStripMenuItem.Name = "globalTreeToolStripMenuItem";
            this.globalTreeToolStripMenuItem.Size = new System.Drawing.Size(73, 20);
            this.globalTreeToolStripMenuItem.Text = "Global Tree";
            // 
            // loadToolStripMenuItem
            // 
            this.loadToolStripMenuItem.Name = "loadToolStripMenuItem";
            this.loadToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.loadToolStripMenuItem.Text = "Load";
            this.loadToolStripMenuItem.Click += new System.EventHandler(this.loadToolStripMenuItem_Click);
            // 
            // scanToolStripMenuItem
            // 
            this.scanToolStripMenuItem.Name = "scanToolStripMenuItem";
            this.scanToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.scanToolStripMenuItem.Text = "Scan";
            this.scanToolStripMenuItem.Click += new System.EventHandler(this.scanToolStripMenuItem_Click);
            // 
            // saveToolStripMenuItem
            // 
            this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            this.saveToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.saveToolStripMenuItem.Text = "Save";
            this.saveToolStripMenuItem.Click += new System.EventHandler(this.saveToolStripMenuItem_Click);
            // 
            // optionsToolStripMenuItem
            // 
            this.optionsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.previewWithTreeToolStripMenuItem,
            this.rotateToolStripMenuItem,
            this.importBonesToolStripMenuItem});
            this.optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            this.optionsToolStripMenuItem.Size = new System.Drawing.Size(56, 20);
            this.optionsToolStripMenuItem.Text = "Options";
            // 
            // previewWithTreeToolStripMenuItem
            // 
            this.previewWithTreeToolStripMenuItem.Checked = true;
            this.previewWithTreeToolStripMenuItem.CheckOnClick = true;
            this.previewWithTreeToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.previewWithTreeToolStripMenuItem.Name = "previewWithTreeToolStripMenuItem";
            this.previewWithTreeToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.previewWithTreeToolStripMenuItem.Text = "Preview with Tree";
            // 
            // rotateToolStripMenuItem
            // 
            this.rotateToolStripMenuItem.Checked = true;
            this.rotateToolStripMenuItem.CheckOnClick = true;
            this.rotateToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.rotateToolStripMenuItem.Name = "rotateToolStripMenuItem";
            this.rotateToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.rotateToolStripMenuItem.Text = "Rotate";
            this.rotateToolStripMenuItem.Click += new System.EventHandler(this.rotateToolStripMenuItem_Click);
            // 
            // importBonesToolStripMenuItem
            // 
            this.importBonesToolStripMenuItem.Name = "importBonesToolStripMenuItem";
            this.importBonesToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.importBonesToolStripMenuItem.Text = "Import Bones";
            this.importBonesToolStripMenuItem.Click += new System.EventHandler(this.importBonesToolStripMenuItem_Click);
            // 
            // transferToolStripMenuItem
            // 
            this.transferToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.importFromUDKToolStripMenuItem});
            this.transferToolStripMenuItem.Name = "transferToolStripMenuItem";
            this.transferToolStripMenuItem.Size = new System.Drawing.Size(60, 20);
            this.transferToolStripMenuItem.Text = "Transfer";
            // 
            // importFromUDKToolStripMenuItem
            // 
            this.importFromUDKToolStripMenuItem.Name = "importFromUDKToolStripMenuItem";
            this.importFromUDKToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.importFromUDKToolStripMenuItem.Text = "Import from UDK...";
            this.importFromUDKToolStripMenuItem.Click += new System.EventHandler(this.importFromUDKToolStripMenuItem_Click);
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openUPKToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(35, 20);
            this.fileToolStripMenuItem.Text = "File";
            this.fileToolStripMenuItem.Visible = false;
            // 
            // openUPKToolStripMenuItem
            // 
            this.openUPKToolStripMenuItem.Name = "openUPKToolStripMenuItem";
            this.openUPKToolStripMenuItem.Size = new System.Drawing.Size(134, 22);
            this.openUPKToolStripMenuItem.Text = "Open UPK...";
            this.openUPKToolStripMenuItem.Click += new System.EventHandler(this.openUPKToolStripMenuItem_Click);
            // 
            // importLODToolStripMenuItem
            // 
            this.importLODToolStripMenuItem.Name = "importLODToolStripMenuItem";
            this.importLODToolStripMenuItem.Size = new System.Drawing.Size(86, 20);
            this.importLODToolStripMenuItem.Text = "Import LOD...";
            this.importLODToolStripMenuItem.Visible = false;
            this.importLODToolStripMenuItem.Click += new System.EventHandler(this.importLODToolStripMenuItem_Click);
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripTextBox1,
            this.toolStripButton1});
            this.toolStrip1.Location = new System.Drawing.Point(0, 24);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(653, 25);
            this.toolStrip1.TabIndex = 1;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // toolStripTextBox1
            // 
            this.toolStripTextBox1.Name = "toolStripTextBox1";
            this.toolStripTextBox1.Size = new System.Drawing.Size(100, 25);
            this.toolStripTextBox1.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.toolStripTextBox1_KeyPress_1);
            // 
            // toolStripButton1
            // 
            this.toolStripButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButton1.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton1.Image")));
            this.toolStripButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButton1.Name = "toolStripButton1";
            this.toolStripButton1.Size = new System.Drawing.Size(31, 22);
            this.toolStripButton1.Text = "Find";
            this.toolStripButton1.Click += new System.EventHandler(this.toolStripButton1_Click_1);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 49);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.treeView1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.splitContainer2);
            this.splitContainer1.Size = new System.Drawing.Size(653, 344);
            this.splitContainer1.SplitterDistance = 217;
            this.splitContainer1.TabIndex = 2;
            // 
            // treeView1
            // 
            this.treeView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.treeView1.HideSelection = false;
            this.treeView1.Location = new System.Drawing.Point(0, 0);
            this.treeView1.Name = "treeView1";
            this.treeView1.Size = new System.Drawing.Size(217, 344);
            this.treeView1.TabIndex = 0;
            this.treeView1.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeView1_AfterSelect);
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
            this.splitContainer2.Panel1.Controls.Add(this.pb1);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.treeView2);
            this.splitContainer2.Size = new System.Drawing.Size(432, 344);
            this.splitContainer2.SplitterDistance = 200;
            this.splitContainer2.TabIndex = 0;
            // 
            // pb1
            // 
            this.pb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pb1.Location = new System.Drawing.Point(0, 0);
            this.pb1.Name = "pb1";
            this.pb1.Size = new System.Drawing.Size(432, 200);
            this.pb1.TabIndex = 0;
            this.pb1.TabStop = false;
            this.pb1.MouseHover += new System.EventHandler(this.pb1_MouseHover);
            // 
            // treeView2
            // 
            this.treeView2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView2.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.treeView2.Location = new System.Drawing.Point(0, 0);
            this.treeView2.Name = "treeView2";
            this.treeView2.Size = new System.Drawing.Size(432, 140);
            this.treeView2.TabIndex = 0;
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.pbar1});
            this.statusStrip1.Location = new System.Drawing.Point(0, 393);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(653, 22);
            this.statusStrip1.TabIndex = 3;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // pbar1
            // 
            this.pbar1.Name = "pbar1";
            this.pbar1.Size = new System.Drawing.Size(100, 16);
            // 
            // timer1
            // 
            this.timer1.Interval = 10;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // splitContainer3
            // 
            this.splitContainer3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer3.Location = new System.Drawing.Point(0, 49);
            this.splitContainer3.Name = "splitContainer3";
            // 
            // splitContainer3.Panel1
            // 
            this.splitContainer3.Panel1.Controls.Add(this.listBox1);
            // 
            // splitContainer3.Panel2
            // 
            this.splitContainer3.Panel2.Controls.Add(this.listBox2);
            this.splitContainer3.Size = new System.Drawing.Size(653, 344);
            this.splitContainer3.SplitterDistance = 217;
            this.splitContainer3.TabIndex = 4;
            this.splitContainer3.Visible = false;
            // 
            // listBox1
            // 
            this.listBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBox1.FormattingEnabled = true;
            this.listBox1.IntegralHeight = false;
            this.listBox1.ItemHeight = 16;
            this.listBox1.Location = new System.Drawing.Point(0, 0);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(217, 344);
            this.listBox1.TabIndex = 0;
            this.listBox1.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged);
            // 
            // listBox2
            // 
            this.listBox2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox2.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBox2.FormattingEnabled = true;
            this.listBox2.IntegralHeight = false;
            this.listBox2.ItemHeight = 16;
            this.listBox2.Location = new System.Drawing.Point(0, 0);
            this.listBox2.Name = "listBox2";
            this.listBox2.Size = new System.Drawing.Size(432, 344);
            this.listBox2.TabIndex = 0;
            // 
            // Meshplorer2
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(653, 415);
            this.Controls.Add(this.splitContainer3);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.menuStrip1);
            this.Controls.Add(this.statusStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Meshplorer2";
            this.Text = "Meshplorer2";
            this.Load += new System.EventHandler(this.Meshplorer2_Load);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.Meshplorer2_Paint);
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
            ((System.ComponentModel.ISupportInitialize)(this.pb1)).EndInit();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.splitContainer3.Panel1.ResumeLayout(false);
            this.splitContainer3.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).EndInit();
            this.splitContainer3.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem globalTreeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem scanToolStripMenuItem;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TreeView treeView1;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.TreeView treeView2;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripProgressBar pbar1;
        private System.Windows.Forms.PictureBox pb1;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loadToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem previewWithTreeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem rotateToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem transferToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importFromUDKToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openUPKToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importLODToolStripMenuItem;
        private System.Windows.Forms.SplitContainer splitContainer3;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.ListBox listBox2;
        private System.Windows.Forms.ToolStripMenuItem importBonesToolStripMenuItem;
        private System.Windows.Forms.ToolStripTextBox toolStripTextBox1;
        private System.Windows.Forms.ToolStripButton toolStripButton1;
    }
}