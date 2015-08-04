namespace VersionSwitcher
{
    partial class VersionSwitcher
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VersionSwitcher));
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.instructionLabel = new System.Windows.Forms.Label();
            this.downloadButton = new System.Windows.Forms.Button();
            this.progressLabel = new System.Windows.Forms.Label();
            this.releasesComboBox = new System.Windows.Forms.ComboBox();
            this.versionSwitcherToolTip = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            // 
            // progressBar1
            // 
            this.progressBar1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar1.Location = new System.Drawing.Point(12, 70);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(391, 23);
            this.progressBar1.TabIndex = 0;
            // 
            // instructionLabel
            // 
            this.instructionLabel.AutoSize = true;
            this.instructionLabel.Location = new System.Drawing.Point(12, 9);
            this.instructionLabel.MaximumSize = new System.Drawing.Size(400, 0);
            this.instructionLabel.Name = "instructionLabel";
            this.instructionLabel.Size = new System.Drawing.Size(399, 26);
            this.instructionLabel.TabIndex = 1;
            this.instructionLabel.Text = "Select a released version of ME3Explorer to switch to. This program will download" +
    " it and restart the program for you.";
            // 
            // downloadButton
            // 
            this.downloadButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.downloadButton.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.downloadButton.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.downloadButton.Enabled = false;
            this.downloadButton.Location = new System.Drawing.Point(312, 41);
            this.downloadButton.Name = "downloadButton";
            this.downloadButton.Size = new System.Drawing.Size(91, 23);
            this.downloadButton.TabIndex = 2;
            this.downloadButton.Text = "Download";
            this.downloadButton.UseVisualStyleBackColor = true;
            this.downloadButton.Click += new System.EventHandler(this.downloadButton_Click);
            // 
            // progressLabel
            // 
            this.progressLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.progressLabel.AutoSize = true;
            this.progressLabel.Location = new System.Drawing.Point(9, 96);
            this.progressLabel.Name = "progressLabel";
            this.progressLabel.Size = new System.Drawing.Size(98, 13);
            this.progressLabel.TabIndex = 5;
            this.progressLabel.Text = "Getting releases list";
            // 
            // releasesComboBox
            // 
            this.releasesComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.releasesComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.releasesComboBox.Enabled = false;
            this.releasesComboBox.FormattingEnabled = true;
            this.releasesComboBox.Location = new System.Drawing.Point(12, 43);
            this.releasesComboBox.Name = "releasesComboBox";
            this.releasesComboBox.Size = new System.Drawing.Size(121, 21);
            this.releasesComboBox.TabIndex = 7;
            // 
            // VersionSwitcher
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(415, 118);
            this.Controls.Add(this.releasesComboBox);
            this.Controls.Add(this.progressLabel);
            this.Controls.Add(this.downloadButton);
            this.Controls.Add(this.instructionLabel);
            this.Controls.Add(this.progressBar1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "VersionSwitcher";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "ME3Explorer Version Switcher";
            this.Load += new System.EventHandler(this.VersionSwitcher_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.Label instructionLabel;
        private System.Windows.Forms.Button downloadButton;
        private System.Windows.Forms.Label progressLabel;
        private System.Windows.Forms.ComboBox releasesComboBox;
        private System.Windows.Forms.ToolTip versionSwitcherToolTip;
    }
}

