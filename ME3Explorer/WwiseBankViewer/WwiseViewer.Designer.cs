namespace ME3Explorer.WwiseBankViewer
{
    partial class WwiseViewer
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
            this.transferToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportAllWEMFilesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.hb1 = new Be.Windows.Forms.HexBox();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.rtb1 = new System.Windows.Forms.RichTextBox();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.listBox2 = new System.Windows.Forms.ListBox();
            this.hb2 = new Be.Windows.Forms.HexBox();
            this.recreateBankToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.hIRCToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cloneObjectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveHexEditsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.saveBankToPccToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.savePccToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.editToolStripMenuItem,
            this.transferToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(863, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            this.menuStrip1.Visible = false;
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openPccToolStripMenuItem,
            this.savePccToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(35, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // openPccToolStripMenuItem
            // 
            this.openPccToolStripMenuItem.Name = "openPccToolStripMenuItem";
            this.openPccToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.openPccToolStripMenuItem.Text = "Open Pcc";
            this.openPccToolStripMenuItem.Click += new System.EventHandler(this.openPccToolStripMenuItem_Click);
            // 
            // transferToolStripMenuItem
            // 
            this.transferToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportAllWEMFilesToolStripMenuItem,
            this.recreateBankToolStripMenuItem});
            this.transferToolStripMenuItem.Name = "transferToolStripMenuItem";
            this.transferToolStripMenuItem.Size = new System.Drawing.Size(60, 20);
            this.transferToolStripMenuItem.Text = "Transfer";
            // 
            // exportAllWEMFilesToolStripMenuItem
            // 
            this.exportAllWEMFilesToolStripMenuItem.Name = "exportAllWEMFilesToolStripMenuItem";
            this.exportAllWEMFilesToolStripMenuItem.Size = new System.Drawing.Size(168, 22);
            this.exportAllWEMFilesToolStripMenuItem.Text = "Export all WEM files";
            this.exportAllWEMFilesToolStripMenuItem.Click += new System.EventHandler(this.exportAllWEMFilesToolStripMenuItem_Click);
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
            this.splitContainer1.Panel2.Controls.Add(this.tabControl1);
            this.splitContainer1.Size = new System.Drawing.Size(863, 505);
            this.splitContainer1.SplitterDistance = 363;
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
            this.listBox1.Size = new System.Drawing.Size(363, 505);
            this.listBox1.TabIndex = 0;
            this.listBox1.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged);
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(496, 505);
            this.tabControl1.TabIndex = 0;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.hb1);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(488, 503);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "RAW";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // hb1
            // 
            this.hb1.BoldFont = null;
            this.hb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.hb1.Font = new System.Drawing.Font("Courier New", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.hb1.LineInfoForeColor = System.Drawing.Color.Empty;
            this.hb1.LineInfoVisible = true;
            this.hb1.Location = new System.Drawing.Point(3, 3);
            this.hb1.Name = "hb1";
            this.hb1.ShadowSelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(60)))), ((int)(((byte)(188)))), ((int)(((byte)(255)))));
            this.hb1.Size = new System.Drawing.Size(482, 497);
            this.hb1.StringViewVisible = true;
            this.hb1.TabIndex = 0;
            this.hb1.UseFixedBytesPerLine = true;
            this.hb1.VScrollBarVisible = true;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.rtb1);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(488, 503);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "First Scan";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // rtb1
            // 
            this.rtb1.DetectUrls = false;
            this.rtb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtb1.Location = new System.Drawing.Point(3, 3);
            this.rtb1.Name = "rtb1";
            this.rtb1.Size = new System.Drawing.Size(482, 497);
            this.rtb1.TabIndex = 0;
            this.rtb1.Text = "";
            this.rtb1.WordWrap = false;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.splitContainer2);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage3.Size = new System.Drawing.Size(488, 479);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "HIRC objects";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(3, 3);
            this.splitContainer2.Name = "splitContainer2";
            this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.listBox2);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.hb2);
            this.splitContainer2.Size = new System.Drawing.Size(482, 473);
            this.splitContainer2.SplitterDistance = 172;
            this.splitContainer2.TabIndex = 0;
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
            this.listBox2.Size = new System.Drawing.Size(482, 172);
            this.listBox2.TabIndex = 0;
            this.listBox2.SelectedIndexChanged += new System.EventHandler(this.listBox2_SelectedIndexChanged);
            // 
            // hb2
            // 
            this.hb2.BoldFont = null;
            this.hb2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.hb2.Font = new System.Drawing.Font("Courier New", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.hb2.LineInfoForeColor = System.Drawing.Color.Empty;
            this.hb2.LineInfoVisible = true;
            this.hb2.Location = new System.Drawing.Point(0, 0);
            this.hb2.Name = "hb2";
            this.hb2.ShadowSelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(60)))), ((int)(((byte)(188)))), ((int)(((byte)(255)))));
            this.hb2.Size = new System.Drawing.Size(482, 297);
            this.hb2.StringViewVisible = true;
            this.hb2.TabIndex = 0;
            this.hb2.UseFixedBytesPerLine = true;
            this.hb2.VScrollBarVisible = true;
            // 
            // recreateBankToolStripMenuItem
            // 
            this.recreateBankToolStripMenuItem.Name = "recreateBankToolStripMenuItem";
            this.recreateBankToolStripMenuItem.Size = new System.Drawing.Size(178, 22);
            this.recreateBankToolStripMenuItem.Text = "Recreate Bank To File";
            this.recreateBankToolStripMenuItem.Click += new System.EventHandler(this.recreateBankToolStripMenuItem_Click);
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.hIRCToolStripMenuItem,
            this.saveBankToPccToolStripMenuItem});
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.editToolStripMenuItem.Text = "Edit";
            // 
            // hIRCToolStripMenuItem
            // 
            this.hIRCToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.cloneObjectToolStripMenuItem,
            this.saveHexEditsToolStripMenuItem,
            this.editToolStripMenuItem1});
            this.hIRCToolStripMenuItem.Name = "hIRCToolStripMenuItem";
            this.hIRCToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.hIRCToolStripMenuItem.Text = "HIRC";
            // 
            // cloneObjectToolStripMenuItem
            // 
            this.cloneObjectToolStripMenuItem.Name = "cloneObjectToolStripMenuItem";
            this.cloneObjectToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.cloneObjectToolStripMenuItem.Text = "Clone Object";
            this.cloneObjectToolStripMenuItem.Click += new System.EventHandler(this.cloneObjectToolStripMenuItem_Click);
            // 
            // saveHexEditsToolStripMenuItem
            // 
            this.saveHexEditsToolStripMenuItem.Name = "saveHexEditsToolStripMenuItem";
            this.saveHexEditsToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.saveHexEditsToolStripMenuItem.Text = "Save Hex Edits";
            this.saveHexEditsToolStripMenuItem.Click += new System.EventHandler(this.saveHexEditsToolStripMenuItem_Click);
            // 
            // editToolStripMenuItem1
            // 
            this.editToolStripMenuItem1.Name = "editToolStripMenuItem1";
            this.editToolStripMenuItem1.Size = new System.Drawing.Size(214, 22);
            this.editToolStripMenuItem1.Text = "Edit Type02 Sound SFX/Voice";
            this.editToolStripMenuItem1.Click += new System.EventHandler(this.editToolStripMenuItem1_Click);
            // 
            // saveBankToPccToolStripMenuItem
            // 
            this.saveBankToPccToolStripMenuItem.Name = "saveBankToPccToolStripMenuItem";
            this.saveBankToPccToolStripMenuItem.Size = new System.Drawing.Size(156, 22);
            this.saveBankToPccToolStripMenuItem.Text = "Save Bank to Pcc";
            this.saveBankToPccToolStripMenuItem.Click += new System.EventHandler(this.saveBankToPccToolStripMenuItem_Click);
            // 
            // savePccToolStripMenuItem
            // 
            this.savePccToolStripMenuItem.Name = "savePccToolStripMenuItem";
            this.savePccToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.savePccToolStripMenuItem.Text = "Save Pcc";
            this.savePccToolStripMenuItem.Click += new System.EventHandler(this.savePccToolStripMenuItem_Click);
            // 
            // WwiseViewer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(863, 529);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "WwiseViewer";
            this.Text = "Wwise Viewer";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.tabPage3.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openPccToolStripMenuItem;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private Be.Windows.Forms.HexBox hb1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.RichTextBox rtb1;
        private System.Windows.Forms.ToolStripMenuItem transferToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportAllWEMFilesToolStripMenuItem;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.ListBox listBox2;
        private Be.Windows.Forms.HexBox hb2;
        private System.Windows.Forms.ToolStripMenuItem recreateBankToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem hIRCToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem cloneObjectToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveHexEditsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem saveBankToPccToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem savePccToolStripMenuItem;
    }
}