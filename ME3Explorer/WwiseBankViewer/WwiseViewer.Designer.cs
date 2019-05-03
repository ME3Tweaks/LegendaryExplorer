namespace ME3Explorer.WwiseBankEditor
{
    partial class WwiseEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(WwiseEditor));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openPccToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.savePccToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cloneHIRCObjectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.transferToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportAllWEMFilesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.recreateBankToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.toolStrip2 = new System.Windows.Forms.ToolStrip();
            this.searchHexTextBox = new System.Windows.Forms.ToolStripTextBox();
            this.searchHexButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.searchHexStatus = new System.Windows.Forms.ToolStripLabel();
            this.statusStrip2 = new System.Windows.Forms.StatusStrip();
            this.hircHexboxStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.hircHexBox = new Be.Windows.Forms.HexBox();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.saveHexChangesButton = new System.Windows.Forms.ToolStripButton();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.openFileLabel = new System.Windows.Forms.ToolStripStatusLabel();
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
            this.toolStrip2.SuspendLayout();
            this.statusStrip2.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.editToolStripMenuItem,
            this.transferToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Padding = new System.Windows.Forms.Padding(12, 4, 0, 4);
            this.menuStrip1.Size = new System.Drawing.Size(1726, 44);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openPccToolStripMenuItem,
            this.savePccToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(64, 38);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // openPccToolStripMenuItem
            // 
            this.openPccToolStripMenuItem.Name = "openPccToolStripMenuItem";
            this.openPccToolStripMenuItem.Size = new System.Drawing.Size(214, 38);
            this.openPccToolStripMenuItem.Text = "Open Pcc";
            this.openPccToolStripMenuItem.Click += new System.EventHandler(this.openPccToolStripMenuItem_Click);
            // 
            // savePccToolStripMenuItem
            // 
            this.savePccToolStripMenuItem.Name = "savePccToolStripMenuItem";
            this.savePccToolStripMenuItem.Size = new System.Drawing.Size(214, 38);
            this.savePccToolStripMenuItem.Text = "Save Pcc";
            this.savePccToolStripMenuItem.Click += new System.EventHandler(this.savePccToolStripMenuItem_Click);
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.cloneHIRCObjectToolStripMenuItem,
            this.editToolStripMenuItem1});
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.Size = new System.Drawing.Size(67, 38);
            this.editToolStripMenuItem.Text = "Edit";
            // 
            // cloneHIRCObjectToolStripMenuItem
            // 
            this.cloneHIRCObjectToolStripMenuItem.Name = "cloneHIRCObjectToolStripMenuItem";
            this.cloneHIRCObjectToolStripMenuItem.Size = new System.Drawing.Size(426, 38);
            this.cloneHIRCObjectToolStripMenuItem.Text = "Clone HIRC object";
            this.cloneHIRCObjectToolStripMenuItem.Click += new System.EventHandler(this.cloneObjectToolStripMenuItem_Click);
            // 
            // editToolStripMenuItem1
            // 
            this.editToolStripMenuItem1.Name = "editToolStripMenuItem1";
            this.editToolStripMenuItem1.Size = new System.Drawing.Size(426, 38);
            this.editToolStripMenuItem1.Text = "Edit Type02 Sound SFX/Voice";
            this.editToolStripMenuItem1.Click += new System.EventHandler(this.editSoundSFXVoiceToolStripMenuItem1_Click);
            // 
            // transferToolStripMenuItem
            // 
            this.transferToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportAllWEMFilesToolStripMenuItem,
            this.recreateBankToolStripMenuItem});
            this.transferToolStripMenuItem.Name = "transferToolStripMenuItem";
            this.transferToolStripMenuItem.Size = new System.Drawing.Size(111, 38);
            this.transferToolStripMenuItem.Text = "Transfer";
            // 
            // exportAllWEMFilesToolStripMenuItem
            // 
            this.exportAllWEMFilesToolStripMenuItem.Name = "exportAllWEMFilesToolStripMenuItem";
            this.exportAllWEMFilesToolStripMenuItem.Size = new System.Drawing.Size(340, 38);
            this.exportAllWEMFilesToolStripMenuItem.Text = "Export all WEM files";
            this.exportAllWEMFilesToolStripMenuItem.Click += new System.EventHandler(this.exportAllWEMFilesToolStripMenuItem_Click);
            // 
            // recreateBankToolStripMenuItem
            // 
            this.recreateBankToolStripMenuItem.Name = "recreateBankToolStripMenuItem";
            this.recreateBankToolStripMenuItem.Size = new System.Drawing.Size(340, 38);
            this.recreateBankToolStripMenuItem.Text = "Recreate Bank To File";
            this.recreateBankToolStripMenuItem.Click += new System.EventHandler(this.recreateBankToolStripMenuItem_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 44);
            this.splitContainer1.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.listBox1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.tabControl1);
            this.splitContainer1.Size = new System.Drawing.Size(1726, 951);
            this.splitContainer1.SplitterDistance = 726;
            this.splitContainer1.SplitterWidth = 8;
            this.splitContainer1.TabIndex = 1;
            // 
            // listBox1
            // 
            this.listBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBox1.FormattingEnabled = true;
            this.listBox1.IntegralHeight = false;
            this.listBox1.ItemHeight = 29;
            this.listBox1.Location = new System.Drawing.Point(0, 0);
            this.listBox1.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(726, 951);
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
            this.tabControl1.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(992, 951);
            this.tabControl1.TabIndex = 0;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.hb1);
            this.tabPage1.Location = new System.Drawing.Point(8, 39);
            this.tabPage1.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.tabPage1.Size = new System.Drawing.Size(976, 904);
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
            this.hb1.Location = new System.Drawing.Point(6, 6);
            this.hb1.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.hb1.MaxBytesPerLine = 100;
            this.hb1.MinBytesPerLine = 0;
            this.hb1.Name = "hb1";
            this.hb1.ShadowSelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(60)))), ((int)(((byte)(188)))), ((int)(((byte)(255)))));
            this.hb1.Size = new System.Drawing.Size(964, 892);
            this.hb1.StringViewVisible = true;
            this.hb1.TabIndex = 0;
            this.hb1.UseFixedBytesPerLine = true;
            this.hb1.VScrollBarVisible = true;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.rtb1);
            this.tabPage2.Location = new System.Drawing.Point(8, 39);
            this.tabPage2.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.tabPage2.Size = new System.Drawing.Size(976, 882);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "First Scan";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // rtb1
            // 
            this.rtb1.DetectUrls = false;
            this.rtb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtb1.Location = new System.Drawing.Point(6, 6);
            this.rtb1.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.rtb1.Name = "rtb1";
            this.rtb1.Size = new System.Drawing.Size(964, 870);
            this.rtb1.TabIndex = 0;
            this.rtb1.Text = "";
            this.rtb1.WordWrap = false;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.splitContainer2);
            this.tabPage3.Location = new System.Drawing.Point(8, 39);
            this.tabPage3.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.tabPage3.Size = new System.Drawing.Size(976, 904);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "HIRC objects";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(6, 6);
            this.splitContainer2.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.splitContainer2.Name = "splitContainer2";
            this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.listBox2);
            this.splitContainer2.Panel1.Controls.Add(this.toolStrip2);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.statusStrip2);
            this.splitContainer2.Panel2.Controls.Add(this.hircHexBox);
            this.splitContainer2.Panel2.Controls.Add(this.toolStrip1);
            this.splitContainer2.Size = new System.Drawing.Size(964, 892);
            this.splitContainer2.SplitterDistance = 322;
            this.splitContainer2.SplitterWidth = 8;
            this.splitContainer2.TabIndex = 0;
            // 
            // listBox2
            // 
            this.listBox2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox2.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBox2.FormattingEnabled = true;
            this.listBox2.IntegralHeight = false;
            this.listBox2.ItemHeight = 29;
            this.listBox2.Location = new System.Drawing.Point(0, 39);
            this.listBox2.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.listBox2.Name = "listBox2";
            this.listBox2.Size = new System.Drawing.Size(964, 283);
            this.listBox2.TabIndex = 0;
            this.listBox2.SelectedIndexChanged += new System.EventHandler(this.listBox2_SelectedIndexChanged);
            // 
            // toolStrip2
            // 
            this.toolStrip2.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.toolStrip2.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.searchHexTextBox,
            this.searchHexButton,
            this.toolStripSeparator1,
            this.searchHexStatus});
            this.toolStrip2.Location = new System.Drawing.Point(0, 0);
            this.toolStrip2.Name = "toolStrip2";
            this.toolStrip2.Padding = new System.Windows.Forms.Padding(0, 0, 2, 0);
            this.toolStrip2.Size = new System.Drawing.Size(964, 39);
            this.toolStrip2.TabIndex = 1;
            this.toolStrip2.Text = "toolStrip2";
            // 
            // searchHexTextBox
            // 
            this.searchHexTextBox.Name = "searchHexTextBox";
            this.searchHexTextBox.Size = new System.Drawing.Size(196, 39);
            // 
            // searchHexButton
            // 
            this.searchHexButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.searchHexButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.searchHexButton.Name = "searchHexButton";
            this.searchHexButton.Size = new System.Drawing.Size(138, 36);
            this.searchHexButton.Text = "Search Hex";
            this.searchHexButton.Click += new System.EventHandler(this.searchHexButton_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 39);
            // 
            // searchHexStatus
            // 
            this.searchHexStatus.Name = "searchHexStatus";
            this.searchHexStatus.Size = new System.Drawing.Size(0, 36);
            // 
            // statusStrip2
            // 
            this.statusStrip2.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.statusStrip2.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.hircHexboxStatusLabel});
            this.statusStrip2.Location = new System.Drawing.Point(0, 525);
            this.statusStrip2.Name = "statusStrip2";
            this.statusStrip2.Padding = new System.Windows.Forms.Padding(2, 0, 28, 0);
            this.statusStrip2.Size = new System.Drawing.Size(964, 37);
            this.statusStrip2.TabIndex = 2;
            this.statusStrip2.Text = "statusStrip2";
            // 
            // hircHexboxStatusLabel
            // 
            this.hircHexboxStatusLabel.Name = "hircHexboxStatusLabel";
            this.hircHexboxStatusLabel.Size = new System.Drawing.Size(198, 32);
            this.hircHexboxStatusLabel.Text = "Nothing selected";
            // 
            // hircHexBox
            // 
            this.hircHexBox.BoldFont = null;
            this.hircHexBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.hircHexBox.Font = new System.Drawing.Font("Courier New", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.hircHexBox.LineInfoForeColor = System.Drawing.Color.Empty;
            this.hircHexBox.LineInfoVisible = true;
            this.hircHexBox.Location = new System.Drawing.Point(0, 39);
            this.hircHexBox.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.hircHexBox.MaxBytesPerLine = 100;
            this.hircHexBox.MinBytesPerLine = 0;
            this.hircHexBox.Name = "hircHexBox";
            this.hircHexBox.ShadowSelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(60)))), ((int)(((byte)(188)))), ((int)(((byte)(255)))));
            this.hircHexBox.Size = new System.Drawing.Size(964, 523);
            this.hircHexBox.StringViewVisible = true;
            this.hircHexBox.TabIndex = 0;
            this.hircHexBox.UseFixedBytesPerLine = true;
            this.hircHexBox.VScrollBarVisible = true;
            this.hircHexBox.SelectionStartChanged += new System.EventHandler(this.hircHexbox_SelectionChanged);
            this.hircHexBox.SelectionLengthChanged += new System.EventHandler(this.hircHexbox_SelectionChanged);
            // 
            // toolStrip1
            // 
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.saveHexChangesButton});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Padding = new System.Windows.Forms.Padding(0, 0, 2, 0);
            this.toolStrip1.Size = new System.Drawing.Size(964, 39);
            this.toolStrip1.TabIndex = 1;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // saveHexChangesButton
            // 
            this.saveHexChangesButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.saveHexChangesButton.Image = ((System.Drawing.Image)(resources.GetObject("saveHexChangesButton.Image")));
            this.saveHexChangesButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.saveHexChangesButton.Name = "saveHexChangesButton";
            this.saveHexChangesButton.Size = new System.Drawing.Size(216, 36);
            this.saveHexChangesButton.Text = "Save Hex Changes";
            this.saveHexChangesButton.Click += new System.EventHandler(this.saveHexChangesButton_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openFileLabel});
            this.statusStrip1.Location = new System.Drawing.Point(0, 995);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Padding = new System.Windows.Forms.Padding(2, 0, 28, 0);
            this.statusStrip1.Size = new System.Drawing.Size(1726, 22);
            this.statusStrip1.TabIndex = 2;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // openFileLabel
            // 
            this.openFileLabel.Name = "openFileLabel";
            this.openFileLabel.Size = new System.Drawing.Size(0, 17);
            // 
            // WwiseEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1726, 1017);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.menuStrip1);
            this.Controls.Add(this.statusStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.Name = "WwiseEditor";
            this.Text = "WwiseBank Editor";
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
            this.splitContainer2.Panel1.PerformLayout();
            this.splitContainer2.Panel2.ResumeLayout(false);
            this.splitContainer2.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.toolStrip2.ResumeLayout(false);
            this.toolStrip2.PerformLayout();
            this.statusStrip2.ResumeLayout(false);
            this.statusStrip2.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
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
        private Be.Windows.Forms.HexBox hircHexBox;
        private System.Windows.Forms.ToolStripMenuItem recreateBankToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem savePccToolStripMenuItem;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton saveHexChangesButton;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel openFileLabel;
        private System.Windows.Forms.ToolStrip toolStrip2;
        private System.Windows.Forms.ToolStripTextBox searchHexTextBox;
        private System.Windows.Forms.ToolStripButton searchHexButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripLabel searchHexStatus;
        private System.Windows.Forms.ToolStripMenuItem cloneHIRCObjectToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem1;
        private System.Windows.Forms.StatusStrip statusStrip2;
        private System.Windows.Forms.ToolStripStatusLabel hircHexboxStatusLabel;
    }
}