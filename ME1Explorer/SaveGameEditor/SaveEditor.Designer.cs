namespace ME1Explorer.SaveGameEditor
{
    partial class SaveEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SaveEditor));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openSaveGameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveSaveGameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.transferToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rawDumpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.extractAsME1PackageToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importFromME1PackageToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripButton1 = new System.Windows.Forms.ToolStripButton();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.hb1 = new Be.Windows.Forms.HexBox();
            this.menuStrip1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
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
            this.menuStrip1.Size = new System.Drawing.Size(292, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            this.menuStrip1.Visible = false;
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openSaveGameToolStripMenuItem,
            this.saveSaveGameToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(35, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // openSaveGameToolStripMenuItem
            // 
            this.openSaveGameToolStripMenuItem.Name = "openSaveGameToolStripMenuItem";
            this.openSaveGameToolStripMenuItem.Size = new System.Drawing.Size(157, 22);
            this.openSaveGameToolStripMenuItem.Text = "Open Save Game";
            this.openSaveGameToolStripMenuItem.Click += new System.EventHandler(this.openSaveGameToolStripMenuItem_Click);
            // 
            // saveSaveGameToolStripMenuItem
            // 
            this.saveSaveGameToolStripMenuItem.Name = "saveSaveGameToolStripMenuItem";
            this.saveSaveGameToolStripMenuItem.Size = new System.Drawing.Size(157, 22);
            this.saveSaveGameToolStripMenuItem.Text = "Store Save Game";
            this.saveSaveGameToolStripMenuItem.Click += new System.EventHandler(this.saveSaveGameToolStripMenuItem_Click);
            // 
            // transferToolStripMenuItem
            // 
            this.transferToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.rawDumpToolStripMenuItem,
            this.extractAsME1PackageToolStripMenuItem,
            this.importFromME1PackageToolStripMenuItem});
            this.transferToolStripMenuItem.Name = "transferToolStripMenuItem";
            this.transferToolStripMenuItem.Size = new System.Drawing.Size(60, 20);
            this.transferToolStripMenuItem.Text = "Transfer";
            // 
            // rawDumpToolStripMenuItem
            // 
            this.rawDumpToolStripMenuItem.Name = "rawDumpToolStripMenuItem";
            this.rawDumpToolStripMenuItem.Size = new System.Drawing.Size(197, 22);
            this.rawDumpToolStripMenuItem.Text = "Raw Dump";
            this.rawDumpToolStripMenuItem.Click += new System.EventHandler(this.rawDumpToolStripMenuItem_Click);
            // 
            // extractAsME1PackageToolStripMenuItem
            // 
            this.extractAsME1PackageToolStripMenuItem.Name = "extractAsME1PackageToolStripMenuItem";
            this.extractAsME1PackageToolStripMenuItem.Size = new System.Drawing.Size(197, 22);
            this.extractAsME1PackageToolStripMenuItem.Text = "Extract as ME1 Package";
            this.extractAsME1PackageToolStripMenuItem.Click += new System.EventHandler(this.extractAsME1PackageToolStripMenuItem_Click);
            // 
            // importFromME1PackageToolStripMenuItem
            // 
            this.importFromME1PackageToolStripMenuItem.Name = "importFromME1PackageToolStripMenuItem";
            this.importFromME1PackageToolStripMenuItem.Size = new System.Drawing.Size(197, 22);
            this.importFromME1PackageToolStripMenuItem.Text = "Import from ME1 Package";
            this.importFromME1PackageToolStripMenuItem.Click += new System.EventHandler(this.importFromME1PackageToolStripMenuItem_Click);
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripButton1});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(292, 25);
            this.toolStrip1.TabIndex = 1;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // toolStripButton1
            // 
            this.toolStripButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButton1.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton1.Image")));
            this.toolStripButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButton1.Name = "toolStripButton1";
            this.toolStripButton1.Size = new System.Drawing.Size(102, 22);
            this.toolStripButton1.Text = "Save Hex Changes";
            this.toolStripButton1.Click += new System.EventHandler(this.toolStripButton1_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 25);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.listBox1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.hb1);
            this.splitContainer1.Size = new System.Drawing.Size(292, 248);
            this.splitContainer1.SplitterDistance = 97;
            this.splitContainer1.TabIndex = 2;
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
            this.listBox1.Size = new System.Drawing.Size(97, 248);
            this.listBox1.TabIndex = 0;
            this.listBox1.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged);
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
            this.hb1.ShadowSelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(60)))), ((int)(((byte)(188)))), ((int)(((byte)(255)))));
            this.hb1.Size = new System.Drawing.Size(191, 248);
            this.hb1.StringViewVisible = true;
            this.hb1.TabIndex = 0;
            this.hb1.UseFixedBytesPerLine = true;
            this.hb1.VScrollBarVisible = true;
            // 
            // SaveEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(292, 273);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "SaveEditor";
            this.Text = "WV\'s Save Game Editor";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
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

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openSaveGameToolStripMenuItem;
        private Be.Windows.Forms.HexBox hb1;
        private System.Windows.Forms.ToolStripButton toolStripButton1;
        private System.Windows.Forms.ToolStripMenuItem saveSaveGameToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem transferToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem rawDumpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem extractAsME1PackageToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importFromME1PackageToolStripMenuItem;
    }
}