namespace ME3Explorer
{
    partial class TexplorerFirstTimeSetup
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TexplorerFirstTimeSetup));
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.ContinueButton = new System.Windows.Forms.ToolStripButton();
            this.ExitButton = new System.Windows.Forms.ToolStripButton();
            this.StatusProgBar = new System.Windows.Forms.ToolStripProgressBar();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.StatusProgLabel = new System.Windows.Forms.ToolStripLabel();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.MainListView = new System.Windows.Forms.CheckedListBox();
            this.BackupPresentLabel = new System.Windows.Forms.Label();
            this.ExtractedListBox = new System.Windows.Forms.ListBox();
            this.ExtractedLabel = new System.Windows.Forms.Label();
            this.BackupCheckBox = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.DiskSpaceLabel = new System.Windows.Forms.Label();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ContinueButton,
            this.ExitButton,
            this.StatusProgBar,
            this.toolStripSeparator1,
            this.StatusProgLabel});
            this.toolStrip1.Location = new System.Drawing.Point(0, 361);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(716, 25);
            this.toolStrip1.TabIndex = 0;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // ContinueButton
            // 
            this.ContinueButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.ContinueButton.Image = ((System.Drawing.Image)(resources.GetObject("ContinueButton.Image")));
            this.ContinueButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.ContinueButton.Name = "ContinueButton";
            this.ContinueButton.Size = new System.Drawing.Size(60, 22);
            this.ContinueButton.Text = "Continue";
            this.ContinueButton.Click += new System.EventHandler(this.ContinueButton_Click);
            // 
            // ExitButton
            // 
            this.ExitButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.ExitButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.ExitButton.Image = ((System.Drawing.Image)(resources.GetObject("ExitButton.Image")));
            this.ExitButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.ExitButton.Name = "ExitButton";
            this.ExitButton.Size = new System.Drawing.Size(47, 22);
            this.ExitButton.Text = "Cancel";
            this.ExitButton.Click += new System.EventHandler(this.ExitButton_Click);
            // 
            // StatusProgBar
            // 
            this.StatusProgBar.Name = "StatusProgBar";
            this.StatusProgBar.Size = new System.Drawing.Size(100, 22);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 25);
            // 
            // StatusProgLabel
            // 
            this.StatusProgLabel.Name = "StatusProgLabel";
            this.StatusProgLabel.Size = new System.Drawing.Size(86, 22);
            this.StatusProgLabel.Text = "toolStripLabel1";
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.MainListView);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.DiskSpaceLabel);
            this.splitContainer1.Panel2.Controls.Add(this.BackupPresentLabel);
            this.splitContainer1.Panel2.Controls.Add(this.ExtractedListBox);
            this.splitContainer1.Panel2.Controls.Add(this.ExtractedLabel);
            this.splitContainer1.Panel2.Controls.Add(this.BackupCheckBox);
            this.splitContainer1.Panel2.Controls.Add(this.label1);
            this.splitContainer1.Size = new System.Drawing.Size(716, 361);
            this.splitContainer1.SplitterDistance = 302;
            this.splitContainer1.TabIndex = 1;
            // 
            // MainListView
            // 
            this.MainListView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.MainListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.MainListView.FormattingEnabled = true;
            this.MainListView.HorizontalScrollbar = true;
            this.MainListView.Location = new System.Drawing.Point(0, 0);
            this.MainListView.Name = "MainListView";
            this.MainListView.Size = new System.Drawing.Size(302, 361);
            this.MainListView.TabIndex = 0;
            this.MainListView.SelectedIndexChanged += new System.EventHandler(this.MainListView_SelectedIndexChanged);
            // 
            // BackupPresentLabel
            // 
            this.BackupPresentLabel.AutoSize = true;
            this.BackupPresentLabel.Location = new System.Drawing.Point(5, 65);
            this.BackupPresentLabel.Name = "BackupPresentLabel";
            this.BackupPresentLabel.Size = new System.Drawing.Size(92, 13);
            this.BackupPresentLabel.TabIndex = 5;
            this.BackupPresentLabel.Text = "Backup Exists at: ";
            // 
            // ExtractedListBox
            // 
            this.ExtractedListBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ExtractedListBox.FormattingEnabled = true;
            this.ExtractedListBox.HorizontalScrollbar = true;
            this.ExtractedListBox.Location = new System.Drawing.Point(3, 133);
            this.ExtractedListBox.Name = "ExtractedListBox";
            this.ExtractedListBox.Size = new System.Drawing.Size(404, 225);
            this.ExtractedListBox.TabIndex = 4;
            // 
            // ExtractedLabel
            // 
            this.ExtractedLabel.AutoSize = true;
            this.ExtractedLabel.Location = new System.Drawing.Point(2, 117);
            this.ExtractedLabel.Name = "ExtractedLabel";
            this.ExtractedLabel.Size = new System.Drawing.Size(93, 13);
            this.ExtractedLabel.TabIndex = 3;
            this.ExtractedLabel.Text = "Already Extracted!";
            // 
            // BackupCheckBox
            // 
            this.BackupCheckBox.AutoSize = true;
            this.BackupCheckBox.Location = new System.Drawing.Point(3, 41);
            this.BackupCheckBox.Name = "BackupCheckBox";
            this.BackupCheckBox.Size = new System.Drawing.Size(69, 17);
            this.BackupCheckBox.TabIndex = 2;
            this.BackupCheckBox.Text = "Backup?";
            this.BackupCheckBox.UseVisualStyleBackColor = true;
            this.BackupCheckBox.CheckedChanged += new System.EventHandler(this.BackupCheckBox_CheckedChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(82, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(51, 17);
            this.label1.TabIndex = 1;
            this.label1.Text = "Details";
            // 
            // DiskSpaceLabel
            // 
            this.DiskSpaceLabel.AutoSize = true;
            this.DiskSpaceLabel.Location = new System.Drawing.Point(282, 11);
            this.DiskSpaceLabel.Name = "DiskSpaceLabel";
            this.DiskSpaceLabel.Size = new System.Drawing.Size(35, 13);
            this.DiskSpaceLabel.TabIndex = 6;
            this.DiskSpaceLabel.Text = "label2";
            // 
            // TexplorerFirstTimeSetup
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(716, 386);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.toolStrip1);
            this.Name = "TexplorerFirstTimeSetup";
            this.Text = "TexplorerFirstTimeSetup";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormClosingEvent);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ToolStripButton ContinueButton;
        private System.Windows.Forms.ToolStripButton ExitButton;
        private System.Windows.Forms.ToolStripProgressBar StatusProgBar;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripLabel StatusProgLabel;
        private System.Windows.Forms.CheckedListBox MainListView;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox BackupCheckBox;
        private System.Windows.Forms.Label BackupPresentLabel;
        private System.Windows.Forms.ListBox ExtractedListBox;
        private System.Windows.Forms.Label ExtractedLabel;
        private System.Windows.Forms.Label DiskSpaceLabel;
    }
}