namespace SaltTPF
{
    partial class TPFView
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.extractSelectedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.extractAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.closeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.previewsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.autoPreviewsEnabledToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.resizeEnabledToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.rtb1 = new System.Windows.Forms.RichTextBox();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.rtb2 = new System.Windows.Forms.RichTextBox();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.rtb3 = new System.Windows.Forms.RichTextBox();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.rtb4 = new System.Windows.Forms.RichTextBox();
            this.extractToFileStructureToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.tabPage2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.tabPage4.SuspendLayout();
            this.SuspendLayout();
            // 
            // statusStrip1
            // 
            this.statusStrip1.Location = new System.Drawing.Point(0, 557);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(846, 22);
            this.statusStrip1.TabIndex = 0;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.previewsToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(846, 24);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openToolStripMenuItem,
            this.extractSelectedToolStripMenuItem,
            this.extractAllToolStripMenuItem,
            this.extractToFileStructureToolStripMenuItem,
            this.closeToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.openToolStripMenuItem.Text = "Open";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
            // 
            // extractSelectedToolStripMenuItem
            // 
            this.extractSelectedToolStripMenuItem.Name = "extractSelectedToolStripMenuItem";
            this.extractSelectedToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.extractSelectedToolStripMenuItem.Text = "Extract Selected";
            this.extractSelectedToolStripMenuItem.Click += new System.EventHandler(this.extractSelectedToolStripMenuItem_Click);
            // 
            // extractAllToolStripMenuItem
            // 
            this.extractAllToolStripMenuItem.Name = "extractAllToolStripMenuItem";
            this.extractAllToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.extractAllToolStripMenuItem.Text = "Extract All";
            this.extractAllToolStripMenuItem.Click += new System.EventHandler(this.extractAllToolStripMenuItem_Click);
            // 
            // closeToolStripMenuItem
            // 
            this.closeToolStripMenuItem.Name = "closeToolStripMenuItem";
            this.closeToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.closeToolStripMenuItem.Text = "Close";
            this.closeToolStripMenuItem.Click += new System.EventHandler(this.closeToolStripMenuItem_Click);
            // 
            // previewsToolStripMenuItem
            // 
            this.previewsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.autoPreviewsEnabledToolStripMenuItem,
            this.resizeEnabledToolStripMenuItem});
            this.previewsToolStripMenuItem.Name = "previewsToolStripMenuItem";
            this.previewsToolStripMenuItem.Size = new System.Drawing.Size(65, 20);
            this.previewsToolStripMenuItem.Text = "Previews";
            // 
            // autoPreviewsEnabledToolStripMenuItem
            // 
            this.autoPreviewsEnabledToolStripMenuItem.Name = "autoPreviewsEnabledToolStripMenuItem";
            this.autoPreviewsEnabledToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.autoPreviewsEnabledToolStripMenuItem.Text = "AutoPreviews Enabled";
            this.autoPreviewsEnabledToolStripMenuItem.Click += new System.EventHandler(this.autoPreviewsEnabledToolStripMenuItem_Click);
            // 
            // resizeEnabledToolStripMenuItem
            // 
            this.resizeEnabledToolStripMenuItem.Name = "resizeEnabledToolStripMenuItem";
            this.resizeEnabledToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.resizeEnabledToolStripMenuItem.Text = "Resize Enabled";
            this.resizeEnabledToolStripMenuItem.Click += new System.EventHandler(this.resizeEnabledToolStripMenuItem_Click);
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
            this.splitContainer1.Size = new System.Drawing.Size(846, 533);
            this.splitContainer1.SplitterDistance = 303;
            this.splitContainer1.TabIndex = 2;
            // 
            // listBox1
            // 
            this.listBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox1.FormattingEnabled = true;
            this.listBox1.Location = new System.Drawing.Point(0, 0);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(303, 533);
            this.listBox1.TabIndex = 0;
            this.listBox1.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged);
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Controls.Add(this.tabPage4);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(539, 533);
            this.tabControl1.TabIndex = 0;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.pictureBox1);
            this.tabPage1.Controls.Add(this.rtb1);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(531, 507);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Preview";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // pictureBox1
            // 
            this.pictureBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBox1.Location = new System.Drawing.Point(3, 3);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(525, 501);
            this.pictureBox1.TabIndex = 1;
            this.pictureBox1.TabStop = false;
            // 
            // rtb1
            // 
            this.rtb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb1.Location = new System.Drawing.Point(3, 3);
            this.rtb1.Name = "rtb1";
            this.rtb1.ReadOnly = true;
            this.rtb1.Size = new System.Drawing.Size(525, 501);
            this.rtb1.TabIndex = 0;
            this.rtb1.Text = "";
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.rtb2);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(531, 507);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "File Info";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // rtb2
            // 
            this.rtb2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb2.Location = new System.Drawing.Point(3, 3);
            this.rtb2.Name = "rtb2";
            this.rtb2.ReadOnly = true;
            this.rtb2.Size = new System.Drawing.Size(525, 501);
            this.rtb2.TabIndex = 0;
            this.rtb2.Text = "";
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.rtb3);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Size = new System.Drawing.Size(531, 507);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Hash Info";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // rtb3
            // 
            this.rtb3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb3.Location = new System.Drawing.Point(0, 0);
            this.rtb3.Name = "rtb3";
            this.rtb3.ReadOnly = true;
            this.rtb3.Size = new System.Drawing.Size(531, 507);
            this.rtb3.TabIndex = 0;
            this.rtb3.Text = "";
            // 
            // tabPage4
            // 
            this.tabPage4.Controls.Add(this.rtb4);
            this.tabPage4.Location = new System.Drawing.Point(4, 22);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Size = new System.Drawing.Size(531, 507);
            this.tabPage4.TabIndex = 3;
            this.tabPage4.Text = "TPF Info";
            this.tabPage4.UseVisualStyleBackColor = true;
            // 
            // rtb4
            // 
            this.rtb4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb4.Location = new System.Drawing.Point(0, 0);
            this.rtb4.Name = "rtb4";
            this.rtb4.ReadOnly = true;
            this.rtb4.Size = new System.Drawing.Size(531, 507);
            this.rtb4.TabIndex = 0;
            this.rtb4.Text = "";
            // 
            // extractToFileStructureToolStripMenuItem
            // 
            this.extractToFileStructureToolStripMenuItem.Name = "extractToFileStructureToolStripMenuItem";
            this.extractToFileStructureToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.extractToFileStructureToolStripMenuItem.Text = "Extract to File Structure";
            this.extractToFileStructureToolStripMenuItem.Click += new System.EventHandler(this.extractToFileStructureToolStripMenuItem_Click);
            // 
            // TPFView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(846, 579);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "TPFView";
            this.Text = "TPFView";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.tabPage2.ResumeLayout(false);
            this.tabPage3.ResumeLayout(false);
            this.tabPage4.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem extractSelectedToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem extractAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem closeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem previewsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem autoPreviewsEnabledToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem resizeEnabledToolStripMenuItem;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.RichTextBox rtb1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.TabPage tabPage4;
        private System.Windows.Forms.RichTextBox rtb2;
        private System.Windows.Forms.RichTextBox rtb3;
        private System.Windows.Forms.RichTextBox rtb4;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem extractToFileStructureToolStripMenuItem;
    }
}