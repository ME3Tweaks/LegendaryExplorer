namespace MassEffect3.AudioExtract
{
    partial class Extractor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Extractor));
            this.startButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.convertCheckBox = new System.Windows.Forms.CheckBox();
            this.validateCheckBox = new System.Windows.Forms.CheckBox();
            this.logTextBox = new System.Windows.Forms.RichTextBox();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.containerListBox = new System.Windows.Forms.ListBox();
            this.actorComboBox = new System.Windows.Forms.ComboBox();
            this.localeComboBox = new System.Windows.Forms.ComboBox();
            this.listButton = new System.Windows.Forms.Button();
            this.fileListView = new System.Windows.Forms.ListView();
            this.fileNameColumnHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.fileSizeColumnHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.duplicatesTextBox = new System.Windows.Forms.TextBox();
            this.selectNoneButton = new System.Windows.Forms.Button();
            this.selectAllButton = new System.Windows.Forms.Button();
            this.selectSearchButton = new System.Windows.Forms.Button();
            this.totalSizeLabel = new System.Windows.Forms.Label();
            this.saveFolderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            this.selectVisibleButton = new System.Windows.Forms.Button();
            this.openContainerFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.revorbCheckBox = new System.Windows.Forms.CheckBox();
            this.mainToolTip = new System.Windows.Forms.ToolTip(this.components);
            this.groupComboBox = new System.Windows.Forms.ComboBox();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // startButton
            // 
            this.startButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.startButton.Location = new System.Drawing.Point(553, 445);
            this.startButton.Name = "startButton";
            this.startButton.Size = new System.Drawing.Size(75, 23);
            this.startButton.TabIndex = 0;
            this.startButton.Text = "&Start";
            this.startButton.UseVisualStyleBackColor = true;
            this.startButton.Click += new System.EventHandler(this.OnStart);
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.Location = new System.Drawing.Point(472, 445);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 1;
            this.cancelButton.Text = "&Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += new System.EventHandler(this.OnCancel);
            // 
            // convertCheckBox
            // 
            this.convertCheckBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.convertCheckBox.AutoSize = true;
            this.convertCheckBox.Checked = true;
            this.convertCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.convertCheckBox.Location = new System.Drawing.Point(403, 449);
            this.convertCheckBox.Name = "convertCheckBox";
            this.convertCheckBox.Size = new System.Drawing.Size(63, 17);
            this.convertCheckBox.TabIndex = 2;
            this.convertCheckBox.Text = "C&onvert";
            this.mainToolTip.SetToolTip(this.convertCheckBox, "Convert the audio track using ww2ogg.");
            this.convertCheckBox.UseVisualStyleBackColor = true;
            // 
            // validateCheckBox
            // 
            this.validateCheckBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.validateCheckBox.AutoSize = true;
            this.validateCheckBox.Checked = true;
            this.validateCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.validateCheckBox.Location = new System.Drawing.Point(266, 449);
            this.validateCheckBox.Name = "validateCheckBox";
            this.validateCheckBox.Size = new System.Drawing.Size(64, 17);
            this.validateCheckBox.TabIndex = 3;
            this.validateCheckBox.Text = "&Validate";
            this.mainToolTip.SetToolTip(this.validateCheckBox, "Validate audio track data with known hash.");
            this.validateCheckBox.UseVisualStyleBackColor = true;
            // 
            // logTextBox
            // 
            this.logTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.logTextBox.BackColor = System.Drawing.SystemColors.Window;
            this.logTextBox.Location = new System.Drawing.Point(12, 343);
            this.logTextBox.Name = "logTextBox";
            this.logTextBox.ReadOnly = true;
            this.logTextBox.Size = new System.Drawing.Size(616, 96);
            this.logTextBox.TabIndex = 4;
            this.logTextBox.Text = "";
            // 
            // progressBar1
            // 
            this.progressBar1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar1.Location = new System.Drawing.Point(12, 314);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(616, 23);
            this.progressBar1.TabIndex = 5;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(12, 12);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.groupComboBox);
            this.splitContainer1.Panel1.Controls.Add(this.containerListBox);
            this.splitContainer1.Panel1.Controls.Add(this.actorComboBox);
            this.splitContainer1.Panel1.Controls.Add(this.localeComboBox);
            this.splitContainer1.Panel1.Controls.Add(this.listButton);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.fileListView);
            this.splitContainer1.Panel2.Controls.Add(this.duplicatesTextBox);
            this.splitContainer1.Size = new System.Drawing.Size(616, 267);
            this.splitContainer1.SplitterDistance = 205;
            this.splitContainer1.TabIndex = 6;
            // 
            // containerListBox
            // 
            this.containerListBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.containerListBox.FormattingEnabled = true;
            this.containerListBox.Location = new System.Drawing.Point(3, 3);
            this.containerListBox.Name = "containerListBox";
            this.containerListBox.Size = new System.Drawing.Size(199, 147);
            this.containerListBox.TabIndex = 3;
            // 
            // actorComboBox
            // 
            this.actorComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.actorComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.actorComboBox.FormattingEnabled = true;
            this.actorComboBox.Location = new System.Drawing.Point(3, 160);
            this.actorComboBox.Name = "actorComboBox";
            this.actorComboBox.Size = new System.Drawing.Size(199, 21);
            this.actorComboBox.TabIndex = 2;
            // 
            // localeComboBox
            // 
            this.localeComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.localeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.localeComboBox.FormattingEnabled = true;
            this.localeComboBox.Location = new System.Drawing.Point(3, 214);
            this.localeComboBox.Name = "localeComboBox";
            this.localeComboBox.Size = new System.Drawing.Size(199, 21);
            this.localeComboBox.TabIndex = 1;
            // 
            // listButton
            // 
            this.listButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.listButton.Location = new System.Drawing.Point(127, 241);
            this.listButton.Name = "listButton";
            this.listButton.Size = new System.Drawing.Size(75, 23);
            this.listButton.TabIndex = 0;
            this.listButton.Text = "List";
            this.listButton.UseVisualStyleBackColor = true;
            this.listButton.Click += new System.EventHandler(this.OnFilter);
            // 
            // fileListView
            // 
            this.fileListView.CheckBoxes = true;
            this.fileListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.fileNameColumnHeader,
            this.fileSizeColumnHeader});
            this.fileListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.fileListView.FullRowSelect = true;
            this.fileListView.Location = new System.Drawing.Point(0, 0);
            this.fileListView.Name = "fileListView";
            this.fileListView.Size = new System.Drawing.Size(407, 187);
            this.fileListView.TabIndex = 0;
            this.fileListView.UseCompatibleStateImageBehavior = false;
            this.fileListView.View = System.Windows.Forms.View.Details;
            this.fileListView.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.OnFileChecked);
            this.fileListView.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.OnFileSelectionChanged);
            // 
            // fileNameColumnHeader
            // 
            this.fileNameColumnHeader.Text = "Name";
            this.fileNameColumnHeader.Width = 280;
            // 
            // fileSizeColumnHeader
            // 
            this.fileSizeColumnHeader.Text = "Size";
            this.fileSizeColumnHeader.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.fileSizeColumnHeader.Width = 81;
            // 
            // duplicatesTextBox
            // 
            this.duplicatesTextBox.BackColor = System.Drawing.SystemColors.Window;
            this.duplicatesTextBox.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.duplicatesTextBox.Location = new System.Drawing.Point(0, 187);
            this.duplicatesTextBox.Multiline = true;
            this.duplicatesTextBox.Name = "duplicatesTextBox";
            this.duplicatesTextBox.ReadOnly = true;
            this.duplicatesTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.duplicatesTextBox.Size = new System.Drawing.Size(407, 80);
            this.duplicatesTextBox.TabIndex = 1;
            // 
            // selectNoneButton
            // 
            this.selectNoneButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.selectNoneButton.Location = new System.Drawing.Point(12, 285);
            this.selectNoneButton.Name = "selectNoneButton";
            this.selectNoneButton.Size = new System.Drawing.Size(75, 23);
            this.selectNoneButton.TabIndex = 9;
            this.selectNoneButton.Text = "None";
            this.selectNoneButton.UseVisualStyleBackColor = true;
            this.selectNoneButton.Click += new System.EventHandler(this.OnSelectNone);
            // 
            // selectAllButton
            // 
            this.selectAllButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.selectAllButton.Location = new System.Drawing.Point(93, 285);
            this.selectAllButton.Name = "selectAllButton";
            this.selectAllButton.Size = new System.Drawing.Size(75, 23);
            this.selectAllButton.TabIndex = 10;
            this.selectAllButton.Text = "All";
            this.selectAllButton.UseVisualStyleBackColor = true;
            this.selectAllButton.Click += new System.EventHandler(this.OnSelectAll);
            // 
            // selectSearchButton
            // 
            this.selectSearchButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.selectSearchButton.AutoSize = true;
            this.selectSearchButton.Location = new System.Drawing.Point(255, 285);
            this.selectSearchButton.Name = "selectSearchButton";
            this.selectSearchButton.Size = new System.Drawing.Size(28, 23);
            this.selectSearchButton.TabIndex = 11;
            this.selectSearchButton.Text = "@";
            this.selectSearchButton.UseVisualStyleBackColor = true;
            this.selectSearchButton.Click += new System.EventHandler(this.OnSelectSearch);
            // 
            // totalSizeLabel
            // 
            this.totalSizeLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.totalSizeLabel.Location = new System.Drawing.Point(289, 285);
            this.totalSizeLabel.Name = "totalSizeLabel";
            this.totalSizeLabel.Size = new System.Drawing.Size(339, 23);
            this.totalSizeLabel.TabIndex = 12;
            this.totalSizeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // saveFolderBrowserDialog
            // 
            this.saveFolderBrowserDialog.Description = "elect a directory for the files to be extracted to.";
            // 
            // selectVisibleButton
            // 
            this.selectVisibleButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.selectVisibleButton.Location = new System.Drawing.Point(174, 285);
            this.selectVisibleButton.Name = "selectVisibleButton";
            this.selectVisibleButton.Size = new System.Drawing.Size(75, 23);
            this.selectVisibleButton.TabIndex = 13;
            this.selectVisibleButton.Text = "&Visible";
            this.selectVisibleButton.UseVisualStyleBackColor = true;
            this.selectVisibleButton.Click += new System.EventHandler(this.OnSelectVisible);
            // 
            // openContainerFileDialog
            // 
            this.openContainerFileDialog.Filter = "WwiseStream Container (*.pcc, *.afc)|*.pcc;*.afc|All Files (*.*)|*.*";
            // 
            // revorbCheckBox
            // 
            this.revorbCheckBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.revorbCheckBox.AutoSize = true;
            this.revorbCheckBox.Checked = true;
            this.revorbCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.revorbCheckBox.Location = new System.Drawing.Point(336, 449);
            this.revorbCheckBox.Name = "revorbCheckBox";
            this.revorbCheckBox.Size = new System.Drawing.Size(61, 17);
            this.revorbCheckBox.TabIndex = 14;
            this.revorbCheckBox.Text = "Revorb";
            this.mainToolTip.SetToolTip(this.revorbCheckBox, "Recompute page granule positions in Ogg Vorbis file (it\'s a good thing).");
            this.revorbCheckBox.UseVisualStyleBackColor = true;
            // 
            // groupComboBox
            // 
            this.groupComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.groupComboBox.FormattingEnabled = true;
            this.groupComboBox.Location = new System.Drawing.Point(3, 187);
            this.groupComboBox.Name = "groupComboBox";
            this.groupComboBox.Size = new System.Drawing.Size(199, 21);
            this.groupComboBox.TabIndex = 4;
            // 
            // Extractor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(640, 480);
            this.Controls.Add(this.revorbCheckBox);
            this.Controls.Add(this.selectVisibleButton);
            this.Controls.Add(this.totalSizeLabel);
            this.Controls.Add(this.selectSearchButton);
            this.Controls.Add(this.selectAllButton);
            this.Controls.Add(this.selectNoneButton);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.progressBar1);
            this.Controls.Add(this.logTextBox);
            this.Controls.Add(this.validateCheckBox);
            this.Controls.Add(this.convertCheckBox);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.startButton);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Extractor";
            this.Text = "Gibbed\'s Mass Effect 3 Audio Extractor";
            this.Load += new System.EventHandler(this.OnLoad);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button startButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.CheckBox convertCheckBox;
        private System.Windows.Forms.CheckBox validateCheckBox;
        private System.Windows.Forms.RichTextBox logTextBox;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListView fileListView;
        private System.Windows.Forms.Button selectNoneButton;
        private System.Windows.Forms.Button selectAllButton;
        private System.Windows.Forms.Button selectSearchButton;
        private System.Windows.Forms.ColumnHeader fileNameColumnHeader;
        private System.Windows.Forms.ColumnHeader fileSizeColumnHeader;
        private System.Windows.Forms.ListBox containerListBox;
        private System.Windows.Forms.ComboBox actorComboBox;
        private System.Windows.Forms.ComboBox localeComboBox;
        private System.Windows.Forms.Button listButton;
        private System.Windows.Forms.Label totalSizeLabel;
        private System.Windows.Forms.TextBox duplicatesTextBox;
        private System.Windows.Forms.FolderBrowserDialog saveFolderBrowserDialog;
        private System.Windows.Forms.Button selectVisibleButton;
        private System.Windows.Forms.OpenFileDialog openContainerFileDialog;
        private System.Windows.Forms.CheckBox revorbCheckBox;
        private System.Windows.Forms.ToolTip mainToolTip;
        private System.Windows.Forms.ComboBox groupComboBox;
    }
}

