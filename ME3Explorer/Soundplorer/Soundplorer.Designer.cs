namespace ME3Explorer
{
    partial class Soundplorer
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Soundplorer));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openPccToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openDLCPccToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.transferToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportToWAVToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importFromWAVToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.advancedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.directAFCReplaceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.playToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.stopToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.Status = new System.Windows.Forms.ToolStripStatusLabel();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.hb1 = new Be.Windows.Forms.HexBox();
            this.rtb1 = new System.Windows.Forms.RichTextBox();
            this.menuStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
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
            this.transferToolStripMenuItem,
            this.advancedToolStripMenuItem,
            this.playToolStripMenuItem,
            this.stopToolStripMenuItem});
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
            this.openPccToolStripMenuItem,
            this.openDLCPccToolStripMenuItem,
            this.saveToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(35, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // openPccToolStripMenuItem
            // 
            this.openPccToolStripMenuItem.Name = "openPccToolStripMenuItem";
            this.openPccToolStripMenuItem.Size = new System.Drawing.Size(141, 22);
            this.openPccToolStripMenuItem.Text = "Open pcc";
            this.openPccToolStripMenuItem.Click += new System.EventHandler(this.openPccToolStripMenuItem_Click);
            // 
            // openDLCPccToolStripMenuItem
            // 
            this.openDLCPccToolStripMenuItem.Name = "openDLCPccToolStripMenuItem";
            this.openDLCPccToolStripMenuItem.Size = new System.Drawing.Size(141, 22);
            this.openDLCPccToolStripMenuItem.Text = "Open DLC pcc";
            this.openDLCPccToolStripMenuItem.Click += new System.EventHandler(this.openDLCPccToolStripMenuItem_Click);
            // 
            // saveToolStripMenuItem
            // 
            this.saveToolStripMenuItem.Enabled = false;
            this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            this.saveToolStripMenuItem.Size = new System.Drawing.Size(141, 22);
            this.saveToolStripMenuItem.Text = "Save";
            this.saveToolStripMenuItem.Click += new System.EventHandler(this.saveToolStripMenuItem_Click);
            // 
            // transferToolStripMenuItem
            // 
            this.transferToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportToWAVToolStripMenuItem,
            this.importFromWAVToolStripMenuItem});
            this.transferToolStripMenuItem.Name = "transferToolStripMenuItem";
            this.transferToolStripMenuItem.Size = new System.Drawing.Size(60, 20);
            this.transferToolStripMenuItem.Text = "Transfer";
            // 
            // exportToWAVToolStripMenuItem
            // 
            this.exportToWAVToolStripMenuItem.Name = "exportToWAVToolStripMenuItem";
            this.exportToWAVToolStripMenuItem.Size = new System.Drawing.Size(157, 22);
            this.exportToWAVToolStripMenuItem.Text = "Export to WAV";
            this.exportToWAVToolStripMenuItem.Click += new System.EventHandler(this.exportToWAVToolStripMenuItem_Click);
            // 
            // importFromWAVToolStripMenuItem
            // 
            this.importFromWAVToolStripMenuItem.Name = "importFromWAVToolStripMenuItem";
            this.importFromWAVToolStripMenuItem.Size = new System.Drawing.Size(157, 22);
            this.importFromWAVToolStripMenuItem.Text = "Import from WAV";
            this.importFromWAVToolStripMenuItem.Click += new System.EventHandler(this.importFromWAVToolStripMenuItem_Click);
            // 
            // advancedToolStripMenuItem
            // 
            this.advancedToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.directAFCReplaceToolStripMenuItem});
            this.advancedToolStripMenuItem.Name = "advancedToolStripMenuItem";
            this.advancedToolStripMenuItem.Size = new System.Drawing.Size(67, 20);
            this.advancedToolStripMenuItem.Text = "Advanced";
            // 
            // directAFCReplaceToolStripMenuItem
            // 
            this.directAFCReplaceToolStripMenuItem.Name = "directAFCReplaceToolStripMenuItem";
            this.directAFCReplaceToolStripMenuItem.Size = new System.Drawing.Size(163, 22);
            this.directAFCReplaceToolStripMenuItem.Text = "Direct AFC replace";
            this.directAFCReplaceToolStripMenuItem.Click += new System.EventHandler(this.directAFCReplaceToolStripMenuItem_Click);
            // 
            // playToolStripMenuItem
            // 
            this.playToolStripMenuItem.Name = "playToolStripMenuItem";
            this.playToolStripMenuItem.Size = new System.Drawing.Size(39, 20);
            this.playToolStripMenuItem.Text = "Play";
            this.playToolStripMenuItem.Click += new System.EventHandler(this.playToolStripMenuItem_Click);
            // 
            // stopToolStripMenuItem
            // 
            this.stopToolStripMenuItem.Name = "stopToolStripMenuItem";
            this.stopToolStripMenuItem.Size = new System.Drawing.Size(41, 20);
            this.stopToolStripMenuItem.Text = "Stop";
            this.stopToolStripMenuItem.Click += new System.EventHandler(this.stopToolStripMenuItem_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.Status});
            this.statusStrip1.Location = new System.Drawing.Point(0, 244);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(292, 22);
            this.statusStrip1.TabIndex = 1;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // Status
            // 
            this.Status.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Status.Name = "Status";
            this.Status.Size = new System.Drawing.Size(48, 17);
            this.Status.Text = "Ready";
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.listBox1);
            this.splitContainer1.Panel1.RightToLeft = System.Windows.Forms.RightToLeft.No;
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.hb1);
            this.splitContainer1.Panel2.Controls.Add(this.rtb1);
            this.splitContainer1.Panel2.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.splitContainer1.Size = new System.Drawing.Size(292, 244);
            this.splitContainer1.SplitterDistance = 140;
            this.splitContainer1.TabIndex = 2;
            // 
            // listBox1
            // 
            this.listBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox1.FormattingEnabled = true;
            this.listBox1.IntegralHeight = false;
            this.listBox1.Location = new System.Drawing.Point(0, 0);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(140, 244);
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
            this.hb1.Size = new System.Drawing.Size(148, 244);
            this.hb1.StringViewVisible = true;
            this.hb1.TabIndex = 1;
            this.hb1.UseFixedBytesPerLine = true;
            this.hb1.Visible = false;
            this.hb1.VScrollBarVisible = true;
            // 
            // rtb1
            // 
            this.rtb1.DetectUrls = false;
            this.rtb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtb1.Location = new System.Drawing.Point(0, 0);
            this.rtb1.Name = "rtb1";
            this.rtb1.Size = new System.Drawing.Size(148, 244);
            this.rtb1.TabIndex = 0;
            this.rtb1.Text = "";
            this.rtb1.WordWrap = false;
            // 
            // Soundplorer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(292, 266);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Soundplorer";
            this.Text = "Soundplorer";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
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
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.RichTextBox rtb1;
        private System.Windows.Forms.ToolStripMenuItem playToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem stopToolStripMenuItem;
        private System.Windows.Forms.ToolStripStatusLabel Status;
        private System.Windows.Forms.ToolStripMenuItem transferToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportToWAVToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importFromWAVToolStripMenuItem;
        private Be.Windows.Forms.HexBox hb1;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openDLCPccToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem advancedToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem directAFCReplaceToolStripMenuItem;
    }
}