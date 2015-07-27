namespace ME3Explorer.AnimationExplorer
{
    partial class AnimationExplorer
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
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openPccToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.startScanToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadDBToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveDBToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.transferToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportToPSAToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.treeView1 = new System.Windows.Forms.TreeView();
            this.importFromPSAToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.transferToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(643, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openPccToolStripMenuItem,
            this.startScanToolStripMenuItem,
            this.loadDBToolStripMenuItem,
            this.saveDBToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(35, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // openPccToolStripMenuItem
            // 
            this.openPccToolStripMenuItem.Name = "openPccToolStripMenuItem";
            this.openPccToolStripMenuItem.Size = new System.Drawing.Size(149, 22);
            this.openPccToolStripMenuItem.Text = "Open single pcc";
            this.openPccToolStripMenuItem.Click += new System.EventHandler(this.openPccToolStripMenuItem_Click);
            // 
            // startScanToolStripMenuItem
            // 
            this.startScanToolStripMenuItem.Name = "startScanToolStripMenuItem";
            this.startScanToolStripMenuItem.Size = new System.Drawing.Size(149, 22);
            this.startScanToolStripMenuItem.Text = "Start Scan";
            this.startScanToolStripMenuItem.Click += new System.EventHandler(this.startScanToolStripMenuItem_Click);
            // 
            // loadDBToolStripMenuItem
            // 
            this.loadDBToolStripMenuItem.Name = "loadDBToolStripMenuItem";
            this.loadDBToolStripMenuItem.Size = new System.Drawing.Size(149, 22);
            this.loadDBToolStripMenuItem.Text = "Load DB";
            this.loadDBToolStripMenuItem.Click += new System.EventHandler(this.loadDBToolStripMenuItem_Click);
            // 
            // saveDBToolStripMenuItem
            // 
            this.saveDBToolStripMenuItem.Name = "saveDBToolStripMenuItem";
            this.saveDBToolStripMenuItem.Size = new System.Drawing.Size(149, 22);
            this.saveDBToolStripMenuItem.Text = "Save DB";
            this.saveDBToolStripMenuItem.Click += new System.EventHandler(this.saveDBToolStripMenuItem_Click);
            // 
            // transferToolStripMenuItem
            // 
            this.transferToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportToPSAToolStripMenuItem,
            this.importFromPSAToolStripMenuItem});
            this.transferToolStripMenuItem.Name = "transferToolStripMenuItem";
            this.transferToolStripMenuItem.Size = new System.Drawing.Size(60, 20);
            this.transferToolStripMenuItem.Text = "Transfer";
            // 
            // exportToPSAToolStripMenuItem
            // 
            this.exportToPSAToolStripMenuItem.Name = "exportToPSAToolStripMenuItem";
            this.exportToPSAToolStripMenuItem.Size = new System.Drawing.Size(153, 22);
            this.exportToPSAToolStripMenuItem.Text = "Export to PSA";
            this.exportToPSAToolStripMenuItem.Click += new System.EventHandler(this.exportToPSAToolStripMenuItem_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 24);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.listBox1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.treeView1);
            this.splitContainer1.Size = new System.Drawing.Size(643, 358);
            this.splitContainer1.SplitterDistance = 214;
            this.splitContainer1.TabIndex = 1;
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
            this.listBox1.Size = new System.Drawing.Size(214, 358);
            this.listBox1.TabIndex = 0;
            this.listBox1.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.listBox1_MouseDoubleClick);
            // 
            // treeView1
            // 
            this.treeView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.treeView1.Location = new System.Drawing.Point(0, 0);
            this.treeView1.Name = "treeView1";
            this.treeView1.Size = new System.Drawing.Size(425, 358);
            this.treeView1.TabIndex = 2;
            // 
            // importFromPSAToolStripMenuItem
            // 
            this.importFromPSAToolStripMenuItem.Name = "importFromPSAToolStripMenuItem";
            this.importFromPSAToolStripMenuItem.Size = new System.Drawing.Size(153, 22);
            this.importFromPSAToolStripMenuItem.Text = "Import from PSA";
            this.importFromPSAToolStripMenuItem.Click += new System.EventHandler(this.importFromPSAToolStripMenuItem_Click);
            // 
            // AnimationExplorer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(643, 382);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "AnimationExplorer";
            this.Text = "Animation Explorer";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openPccToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem startScanToolStripMenuItem;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.TreeView treeView1;
        private System.Windows.Forms.ToolStripMenuItem saveDBToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loadDBToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem transferToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportToPSAToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importFromPSAToolStripMenuItem;
    }
}