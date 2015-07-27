namespace ME3Explorer
{
    partial class DLCExplorer
    {

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
        public void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DLCExplorer));
            this.treeViewSfar = new System.Windows.Forms.TreeView();
            this.imageListIcons = new System.Windows.Forms.ImageList(this.components);
            this.contextMenuStripEditor = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.extractToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replaceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.undoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.propertiesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.DialogOpenSfarFile = new System.Windows.Forms.OpenFileDialog();
            this.extractFileDialog = new System.Windows.Forms.SaveFileDialog();
            this.groupBoxFile = new System.Windows.Forms.GroupBox();
            this.labelDataOffset = new System.Windows.Forms.Label();
            this.textBoxDataOffset = new System.Windows.Forms.TextBox();
            this.labelFullName = new System.Windows.Forms.Label();
            this.labelComprSizeBytes = new System.Windows.Forms.Label();
            this.labelHash = new System.Windows.Forms.Label();
            this.textBoxHash = new System.Windows.Forms.TextBox();
            this.textBoxBlockIndex = new System.Windows.Forms.TextBox();
            this.textBoxComprSize = new System.Windows.Forms.TextBox();
            this.textBoxFullName = new System.Windows.Forms.TextBox();
            this.labelBlockIndex = new System.Windows.Forms.Label();
            this.labelComprSize = new System.Windows.Forms.Label();
            this.textBoxEntry = new System.Windows.Forms.TextBox();
            this.labelEntry = new System.Windows.Forms.Label();
            this.textBoxUncSize = new System.Windows.Forms.TextBox();
            this.labelUncSizeBytes = new System.Windows.Forms.Label();
            this.labelFileSize = new System.Windows.Forms.Label();
            this.groupBoxSfar = new System.Windows.Forms.GroupBox();
            this.textBoxCRatio = new System.Windows.Forms.TextBox();
            this.labelTotalComprBytes = new System.Windows.Forms.Label();
            this.labelTotalUncBytes = new System.Windows.Forms.Label();
            this.textBoxNumOfFiles = new System.Windows.Forms.TextBox();
            this.labelNumOfFiles = new System.Windows.Forms.Label();
            this.labelFirstBlockOffset = new System.Windows.Forms.Label();
            this.textBoxFirstBlockOffset = new System.Windows.Forms.TextBox();
            this.textBoxFirstEntryOffset = new System.Windows.Forms.TextBox();
            this.labelFirstEntryOffset = new System.Windows.Forms.Label();
            this.labelComprRatio = new System.Windows.Forms.Label();
            this.labelTotalUncSize = new System.Windows.Forms.Label();
            this.textBoxTotalUncSize = new System.Windows.Forms.TextBox();
            this.textBoxTotalComprSize = new System.Windows.Forms.TextBox();
            this.labelTotalComprSize = new System.Windows.Forms.Label();
            this.textBoxFirstDataOffset = new System.Windows.Forms.TextBox();
            this.labelFirstDataOffset = new System.Windows.Forms.Label();
            this.DialogSelectFileToReplace = new System.Windows.Forms.OpenFileDialog();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.toolStripProgressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.toolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.extractFolderDialog = new System.Windows.Forms.FolderBrowserDialog();
            this.backgroundWorkerExtractFile = new System.ComponentModel.BackgroundWorker();
            this.backgroundWorkerEditFile = new System.ComponentModel.BackgroundWorker();
            this.toolStripMenu = new System.Windows.Forms.ToolStrip();
            this.toolStripOpenFile = new System.Windows.Forms.ToolStripButton();
            this.toolStripSaveFile = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripInfo = new System.Windows.Forms.ToolStripButton();
            this.toolStripAbout = new System.Windows.Forms.ToolStripButton();
            this.contextMenuStripEditor.SuspendLayout();
            this.groupBoxFile.SuspendLayout();
            this.groupBoxSfar.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.toolStripMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // treeViewSfar
            // 
            this.treeViewSfar.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.treeViewSfar.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.treeViewSfar.ImageIndex = 0;
            this.treeViewSfar.ImageList = this.imageListIcons;
            this.treeViewSfar.Indent = 25;
            this.treeViewSfar.Location = new System.Drawing.Point(0, 34);
            this.treeViewSfar.Name = "treeViewSfar";
            this.treeViewSfar.PathSeparator = "/";
            this.treeViewSfar.SelectedImageIndex = 0;
            this.treeViewSfar.ShowLines = false;
            this.treeViewSfar.Size = new System.Drawing.Size(579, 445);
            this.treeViewSfar.TabIndex = 4;
            this.treeViewSfar.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeViewSfar_AfterSelect);
            this.treeViewSfar.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.treeViewSfar_NodeMouseClick);
            // 
            // imageListIcons
            // 
            this.imageListIcons.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageListIcons.ImageStream")));
            this.imageListIcons.TransparentColor = System.Drawing.Color.Transparent;
            this.imageListIcons.Images.SetKeyName(0, "folder.ico");
            this.imageListIcons.Images.SetKeyName(1, "sheet.ico");
            this.imageListIcons.Images.SetKeyName(2, "image.ico");
            this.imageListIcons.Images.SetKeyName(3, "music.ico");
            this.imageListIcons.Images.SetKeyName(4, "video.ico");
            // 
            // contextMenuStripEditor
            // 
            this.contextMenuStripEditor.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addFileToolStripMenuItem,
            this.extractToolStripMenuItem,
            this.replaceToolStripMenuItem,
            this.deleteToolStripMenuItem,
            this.undoToolStripMenuItem,
            this.propertiesToolStripMenuItem});
            this.contextMenuStripEditor.Name = "contextMenuStripEditor";
            this.contextMenuStripEditor.Size = new System.Drawing.Size(128, 136);
            // 
            // addFileToolStripMenuItem
            // 
            this.addFileToolStripMenuItem.Name = "addFileToolStripMenuItem";
            this.addFileToolStripMenuItem.Size = new System.Drawing.Size(127, 22);
            this.addFileToolStripMenuItem.Text = "Add File";
            this.addFileToolStripMenuItem.Click += new System.EventHandler(this.addFileToolStripMenuItem_Click);
            // 
            // extractToolStripMenuItem
            // 
            this.extractToolStripMenuItem.Name = "extractToolStripMenuItem";
            this.extractToolStripMenuItem.Size = new System.Drawing.Size(127, 22);
            this.extractToolStripMenuItem.Text = "Extract";
            this.extractToolStripMenuItem.Click += new System.EventHandler(this.extractToolStripMenuItem_Click);
            // 
            // replaceToolStripMenuItem
            // 
            this.replaceToolStripMenuItem.Name = "replaceToolStripMenuItem";
            this.replaceToolStripMenuItem.Size = new System.Drawing.Size(127, 22);
            this.replaceToolStripMenuItem.Text = "Replace";
            this.replaceToolStripMenuItem.Click += new System.EventHandler(this.replaceToolStripMenuItem_Click);
            // 
            // deleteToolStripMenuItem
            // 
            this.deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
            this.deleteToolStripMenuItem.Size = new System.Drawing.Size(127, 22);
            this.deleteToolStripMenuItem.Text = "Delete";
            this.deleteToolStripMenuItem.Click += new System.EventHandler(this.deleteToolStripMenuItem_Click);
            // 
            // undoToolStripMenuItem
            // 
            this.undoToolStripMenuItem.Name = "undoToolStripMenuItem";
            this.undoToolStripMenuItem.Size = new System.Drawing.Size(127, 22);
            this.undoToolStripMenuItem.Text = "Undo";
            this.undoToolStripMenuItem.Click += new System.EventHandler(this.undoToolStripMenuItem_Click);
            // 
            // propertiesToolStripMenuItem
            // 
            this.propertiesToolStripMenuItem.Name = "propertiesToolStripMenuItem";
            this.propertiesToolStripMenuItem.Size = new System.Drawing.Size(127, 22);
            this.propertiesToolStripMenuItem.Text = "Properties";
            this.propertiesToolStripMenuItem.Click += new System.EventHandler(this.propertiesToolStripMenuItem_Click);
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(32, 19);
            // 
            // DialogOpenSfarFile
            // 
            this.DialogOpenSfarFile.Filter = "ME3 DLC file|*.sfar|All files|*.*";
            this.DialogOpenSfarFile.Title = "Select a DLC sfar file";
            // 
            // extractFileDialog
            // 
            this.extractFileDialog.Filter = "All files|*.*";
            this.extractFileDialog.Title = "Select the place to extract your file";
            // 
            // groupBoxFile
            // 
            this.groupBoxFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxFile.Controls.Add(this.labelDataOffset);
            this.groupBoxFile.Controls.Add(this.textBoxDataOffset);
            this.groupBoxFile.Controls.Add(this.labelFullName);
            this.groupBoxFile.Controls.Add(this.labelComprSizeBytes);
            this.groupBoxFile.Controls.Add(this.labelHash);
            this.groupBoxFile.Controls.Add(this.textBoxHash);
            this.groupBoxFile.Controls.Add(this.textBoxBlockIndex);
            this.groupBoxFile.Controls.Add(this.textBoxComprSize);
            this.groupBoxFile.Controls.Add(this.textBoxFullName);
            this.groupBoxFile.Controls.Add(this.labelBlockIndex);
            this.groupBoxFile.Controls.Add(this.labelComprSize);
            this.groupBoxFile.Controls.Add(this.textBoxEntry);
            this.groupBoxFile.Controls.Add(this.labelEntry);
            this.groupBoxFile.Controls.Add(this.textBoxUncSize);
            this.groupBoxFile.Controls.Add(this.labelUncSizeBytes);
            this.groupBoxFile.Controls.Add(this.labelFileSize);
            this.groupBoxFile.Location = new System.Drawing.Point(585, 261);
            this.groupBoxFile.MaximumSize = new System.Drawing.Size(344, 0);
            this.groupBoxFile.MinimumSize = new System.Drawing.Size(344, 218);
            this.groupBoxFile.Name = "groupBoxFile";
            this.groupBoxFile.Size = new System.Drawing.Size(344, 218);
            this.groupBoxFile.TabIndex = 20;
            this.groupBoxFile.TabStop = false;
            this.groupBoxFile.Text = "File Properties";
            // 
            // labelDataOffset
            // 
            this.labelDataOffset.AutoSize = true;
            this.labelDataOffset.Location = new System.Drawing.Point(71, 186);
            this.labelDataOffset.Name = "labelDataOffset";
            this.labelDataOffset.Size = new System.Drawing.Size(64, 13);
            this.labelDataOffset.TabIndex = 21;
            this.labelDataOffset.Text = "Data Offset:";
            // 
            // textBoxDataOffset
            // 
            this.textBoxDataOffset.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxDataOffset.Location = new System.Drawing.Point(141, 180);
            this.textBoxDataOffset.Margin = new System.Windows.Forms.Padding(1);
            this.textBoxDataOffset.Name = "textBoxDataOffset";
            this.textBoxDataOffset.ReadOnly = true;
            this.textBoxDataOffset.Size = new System.Drawing.Size(88, 25);
            this.textBoxDataOffset.TabIndex = 20;
            // 
            // labelFullName
            // 
            this.labelFullName.AutoSize = true;
            this.labelFullName.Location = new System.Drawing.Point(15, 22);
            this.labelFullName.Name = "labelFullName";
            this.labelFullName.Size = new System.Drawing.Size(57, 13);
            this.labelFullName.TabIndex = 12;
            this.labelFullName.Text = "Full Name:";
            // 
            // labelComprSizeBytes
            // 
            this.labelComprSizeBytes.AutoSize = true;
            this.labelComprSizeBytes.Location = new System.Drawing.Point(205, 105);
            this.labelComprSizeBytes.Name = "labelComprSizeBytes";
            this.labelComprSizeBytes.Size = new System.Drawing.Size(33, 13);
            this.labelComprSizeBytes.TabIndex = 19;
            this.labelComprSizeBytes.Text = "Bytes";
            // 
            // labelHash
            // 
            this.labelHash.AutoSize = true;
            this.labelHash.Location = new System.Drawing.Point(6, 51);
            this.labelHash.Name = "labelHash";
            this.labelHash.Size = new System.Drawing.Size(66, 13);
            this.labelHash.TabIndex = 8;
            this.labelHash.Text = "Hash Name:";
            // 
            // textBoxHash
            // 
            this.textBoxHash.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxHash.Location = new System.Drawing.Point(76, 45);
            this.textBoxHash.Margin = new System.Windows.Forms.Padding(1);
            this.textBoxHash.Name = "textBoxHash";
            this.textBoxHash.ReadOnly = true;
            this.textBoxHash.Size = new System.Drawing.Size(264, 25);
            this.textBoxHash.TabIndex = 9;
            // 
            // textBoxBlockIndex
            // 
            this.textBoxBlockIndex.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxBlockIndex.Location = new System.Drawing.Point(141, 153);
            this.textBoxBlockIndex.Margin = new System.Windows.Forms.Padding(1);
            this.textBoxBlockIndex.Name = "textBoxBlockIndex";
            this.textBoxBlockIndex.ReadOnly = true;
            this.textBoxBlockIndex.Size = new System.Drawing.Size(88, 25);
            this.textBoxBlockIndex.TabIndex = 11;
            // 
            // textBoxComprSize
            // 
            this.textBoxComprSize.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxComprSize.Location = new System.Drawing.Point(141, 99);
            this.textBoxComprSize.Margin = new System.Windows.Forms.Padding(1);
            this.textBoxComprSize.Name = "textBoxComprSize";
            this.textBoxComprSize.ReadOnly = true;
            this.textBoxComprSize.Size = new System.Drawing.Size(60, 25);
            this.textBoxComprSize.TabIndex = 18;
            this.textBoxComprSize.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // textBoxFullName
            // 
            this.textBoxFullName.Location = new System.Drawing.Point(76, 19);
            this.textBoxFullName.Margin = new System.Windows.Forms.Padding(1);
            this.textBoxFullName.Name = "textBoxFullName";
            this.textBoxFullName.ReadOnly = true;
            this.textBoxFullName.Size = new System.Drawing.Size(264, 20);
            this.textBoxFullName.TabIndex = 13;
            // 
            // labelBlockIndex
            // 
            this.labelBlockIndex.AutoSize = true;
            this.labelBlockIndex.Location = new System.Drawing.Point(69, 159);
            this.labelBlockIndex.Name = "labelBlockIndex";
            this.labelBlockIndex.Size = new System.Drawing.Size(66, 13);
            this.labelBlockIndex.TabIndex = 10;
            this.labelBlockIndex.Text = "Block Index:";
            // 
            // labelComprSize
            // 
            this.labelComprSize.AutoSize = true;
            this.labelComprSize.Location = new System.Drawing.Point(44, 105);
            this.labelComprSize.Name = "labelComprSize";
            this.labelComprSize.Size = new System.Drawing.Size(91, 13);
            this.labelComprSize.TabIndex = 17;
            this.labelComprSize.Text = "Compressed Size:";
            // 
            // textBoxEntry
            // 
            this.textBoxEntry.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxEntry.Location = new System.Drawing.Point(141, 126);
            this.textBoxEntry.Margin = new System.Windows.Forms.Padding(1);
            this.textBoxEntry.Name = "textBoxEntry";
            this.textBoxEntry.ReadOnly = true;
            this.textBoxEntry.Size = new System.Drawing.Size(88, 25);
            this.textBoxEntry.TabIndex = 7;
            // 
            // labelEntry
            // 
            this.labelEntry.AutoSize = true;
            this.labelEntry.Location = new System.Drawing.Point(70, 132);
            this.labelEntry.Name = "labelEntry";
            this.labelEntry.Size = new System.Drawing.Size(65, 13);
            this.labelEntry.TabIndex = 6;
            this.labelEntry.Text = "Entry Offset:";
            // 
            // textBoxUncSize
            // 
            this.textBoxUncSize.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxUncSize.Location = new System.Drawing.Point(141, 72);
            this.textBoxUncSize.Margin = new System.Windows.Forms.Padding(1);
            this.textBoxUncSize.Name = "textBoxUncSize";
            this.textBoxUncSize.ReadOnly = true;
            this.textBoxUncSize.Size = new System.Drawing.Size(60, 25);
            this.textBoxUncSize.TabIndex = 14;
            this.textBoxUncSize.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // labelUncSizeBytes
            // 
            this.labelUncSizeBytes.AutoSize = true;
            this.labelUncSizeBytes.Location = new System.Drawing.Point(205, 78);
            this.labelUncSizeBytes.Name = "labelUncSizeBytes";
            this.labelUncSizeBytes.Size = new System.Drawing.Size(33, 13);
            this.labelUncSizeBytes.TabIndex = 16;
            this.labelUncSizeBytes.Text = "Bytes";
            // 
            // labelFileSize
            // 
            this.labelFileSize.AutoSize = true;
            this.labelFileSize.Location = new System.Drawing.Point(86, 78);
            this.labelFileSize.Name = "labelFileSize";
            this.labelFileSize.Size = new System.Drawing.Size(49, 13);
            this.labelFileSize.TabIndex = 15;
            this.labelFileSize.Text = "File Size:";
            // 
            // groupBoxSfar
            // 
            this.groupBoxSfar.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxSfar.AutoSize = true;
            this.groupBoxSfar.Controls.Add(this.textBoxCRatio);
            this.groupBoxSfar.Controls.Add(this.labelTotalComprBytes);
            this.groupBoxSfar.Controls.Add(this.labelTotalUncBytes);
            this.groupBoxSfar.Controls.Add(this.textBoxNumOfFiles);
            this.groupBoxSfar.Controls.Add(this.labelNumOfFiles);
            this.groupBoxSfar.Controls.Add(this.labelFirstBlockOffset);
            this.groupBoxSfar.Controls.Add(this.textBoxFirstBlockOffset);
            this.groupBoxSfar.Controls.Add(this.textBoxFirstEntryOffset);
            this.groupBoxSfar.Controls.Add(this.labelFirstEntryOffset);
            this.groupBoxSfar.Controls.Add(this.labelComprRatio);
            this.groupBoxSfar.Controls.Add(this.labelTotalUncSize);
            this.groupBoxSfar.Controls.Add(this.textBoxTotalUncSize);
            this.groupBoxSfar.Controls.Add(this.textBoxTotalComprSize);
            this.groupBoxSfar.Controls.Add(this.labelTotalComprSize);
            this.groupBoxSfar.Controls.Add(this.textBoxFirstDataOffset);
            this.groupBoxSfar.Controls.Add(this.labelFirstDataOffset);
            this.groupBoxSfar.Location = new System.Drawing.Point(585, 34);
            this.groupBoxSfar.MaximumSize = new System.Drawing.Size(344, 0);
            this.groupBoxSfar.MinimumSize = new System.Drawing.Size(344, 0);
            this.groupBoxSfar.Name = "groupBoxSfar";
            this.groupBoxSfar.Size = new System.Drawing.Size(344, 221);
            this.groupBoxSfar.TabIndex = 21;
            this.groupBoxSfar.TabStop = false;
            this.groupBoxSfar.Text = "sfar Properties";
            // 
            // textBoxCRatio
            // 
            this.textBoxCRatio.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxCRatio.Location = new System.Drawing.Point(140, 98);
            this.textBoxCRatio.Margin = new System.Windows.Forms.Padding(1);
            this.textBoxCRatio.Name = "textBoxCRatio";
            this.textBoxCRatio.ReadOnly = true;
            this.textBoxCRatio.Size = new System.Drawing.Size(75, 25);
            this.textBoxCRatio.TabIndex = 16;
            // 
            // labelTotalComprBytes
            // 
            this.labelTotalComprBytes.AutoSize = true;
            this.labelTotalComprBytes.Location = new System.Drawing.Point(217, 77);
            this.labelTotalComprBytes.Name = "labelTotalComprBytes";
            this.labelTotalComprBytes.Size = new System.Drawing.Size(33, 13);
            this.labelTotalComprBytes.TabIndex = 15;
            this.labelTotalComprBytes.Text = "Bytes";
            // 
            // labelTotalUncBytes
            // 
            this.labelTotalUncBytes.AutoSize = true;
            this.labelTotalUncBytes.Location = new System.Drawing.Point(217, 50);
            this.labelTotalUncBytes.Name = "labelTotalUncBytes";
            this.labelTotalUncBytes.Size = new System.Drawing.Size(33, 13);
            this.labelTotalUncBytes.TabIndex = 14;
            this.labelTotalUncBytes.Text = "Bytes";
            // 
            // textBoxNumOfFiles
            // 
            this.textBoxNumOfFiles.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxNumOfFiles.Location = new System.Drawing.Point(140, 17);
            this.textBoxNumOfFiles.Margin = new System.Windows.Forms.Padding(1);
            this.textBoxNumOfFiles.Name = "textBoxNumOfFiles";
            this.textBoxNumOfFiles.ReadOnly = true;
            this.textBoxNumOfFiles.Size = new System.Drawing.Size(75, 25);
            this.textBoxNumOfFiles.TabIndex = 13;
            // 
            // labelNumOfFiles
            // 
            this.labelNumOfFiles.AutoSize = true;
            this.labelNumOfFiles.Location = new System.Drawing.Point(69, 23);
            this.labelNumOfFiles.Name = "labelNumOfFiles";
            this.labelNumOfFiles.Size = new System.Drawing.Size(65, 13);
            this.labelNumOfFiles.TabIndex = 12;
            this.labelNumOfFiles.Text = "Num of files:";
            // 
            // labelFirstBlockOffset
            // 
            this.labelFirstBlockOffset.AutoSize = true;
            this.labelFirstBlockOffset.Location = new System.Drawing.Point(61, 158);
            this.labelFirstBlockOffset.Name = "labelFirstBlockOffset";
            this.labelFirstBlockOffset.Size = new System.Drawing.Size(73, 13);
            this.labelFirstBlockOffset.TabIndex = 11;
            this.labelFirstBlockOffset.Text = "Blocks Offset:";
            // 
            // textBoxFirstBlockOffset
            // 
            this.textBoxFirstBlockOffset.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxFirstBlockOffset.Location = new System.Drawing.Point(140, 152);
            this.textBoxFirstBlockOffset.Margin = new System.Windows.Forms.Padding(1);
            this.textBoxFirstBlockOffset.Name = "textBoxFirstBlockOffset";
            this.textBoxFirstBlockOffset.ReadOnly = true;
            this.textBoxFirstBlockOffset.Size = new System.Drawing.Size(88, 25);
            this.textBoxFirstBlockOffset.TabIndex = 10;
            // 
            // textBoxFirstEntryOffset
            // 
            this.textBoxFirstEntryOffset.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxFirstEntryOffset.Location = new System.Drawing.Point(140, 125);
            this.textBoxFirstEntryOffset.Margin = new System.Windows.Forms.Padding(1);
            this.textBoxFirstEntryOffset.Name = "textBoxFirstEntryOffset";
            this.textBoxFirstEntryOffset.ReadOnly = true;
            this.textBoxFirstEntryOffset.Size = new System.Drawing.Size(88, 25);
            this.textBoxFirstEntryOffset.TabIndex = 9;
            // 
            // labelFirstEntryOffset
            // 
            this.labelFirstEntryOffset.AutoSize = true;
            this.labelFirstEntryOffset.Location = new System.Drawing.Point(69, 131);
            this.labelFirstEntryOffset.Name = "labelFirstEntryOffset";
            this.labelFirstEntryOffset.Size = new System.Drawing.Size(65, 13);
            this.labelFirstEntryOffset.TabIndex = 8;
            this.labelFirstEntryOffset.Text = "Entry Offset:";
            // 
            // labelComprRatio
            // 
            this.labelComprRatio.AutoSize = true;
            this.labelComprRatio.Location = new System.Drawing.Point(36, 104);
            this.labelComprRatio.Name = "labelComprRatio";
            this.labelComprRatio.Size = new System.Drawing.Size(98, 13);
            this.labelComprRatio.TabIndex = 6;
            this.labelComprRatio.Text = "Compression Ratio:";
            // 
            // labelTotalUncSize
            // 
            this.labelTotalUncSize.AutoSize = true;
            this.labelTotalUncSize.Location = new System.Drawing.Point(11, 50);
            this.labelTotalUncSize.Name = "labelTotalUncSize";
            this.labelTotalUncSize.Size = new System.Drawing.Size(123, 13);
            this.labelTotalUncSize.TabIndex = 5;
            this.labelTotalUncSize.Text = "Uncompressed files size:";
            // 
            // textBoxTotalUncSize
            // 
            this.textBoxTotalUncSize.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxTotalUncSize.Location = new System.Drawing.Point(140, 44);
            this.textBoxTotalUncSize.Margin = new System.Windows.Forms.Padding(1);
            this.textBoxTotalUncSize.Name = "textBoxTotalUncSize";
            this.textBoxTotalUncSize.ReadOnly = true;
            this.textBoxTotalUncSize.Size = new System.Drawing.Size(75, 25);
            this.textBoxTotalUncSize.TabIndex = 4;
            this.textBoxTotalUncSize.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // textBoxTotalComprSize
            // 
            this.textBoxTotalComprSize.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxTotalComprSize.Location = new System.Drawing.Point(140, 71);
            this.textBoxTotalComprSize.Margin = new System.Windows.Forms.Padding(1);
            this.textBoxTotalComprSize.Name = "textBoxTotalComprSize";
            this.textBoxTotalComprSize.ReadOnly = true;
            this.textBoxTotalComprSize.Size = new System.Drawing.Size(75, 25);
            this.textBoxTotalComprSize.TabIndex = 3;
            this.textBoxTotalComprSize.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // labelTotalComprSize
            // 
            this.labelTotalComprSize.AutoSize = true;
            this.labelTotalComprSize.Location = new System.Drawing.Point(24, 77);
            this.labelTotalComprSize.Name = "labelTotalComprSize";
            this.labelTotalComprSize.Size = new System.Drawing.Size(110, 13);
            this.labelTotalComprSize.TabIndex = 2;
            this.labelTotalComprSize.Text = "Compressed files size:";
            // 
            // textBoxFirstDataOffset
            // 
            this.textBoxFirstDataOffset.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxFirstDataOffset.Location = new System.Drawing.Point(140, 179);
            this.textBoxFirstDataOffset.Margin = new System.Windows.Forms.Padding(1);
            this.textBoxFirstDataOffset.Name = "textBoxFirstDataOffset";
            this.textBoxFirstDataOffset.ReadOnly = true;
            this.textBoxFirstDataOffset.Size = new System.Drawing.Size(88, 25);
            this.textBoxFirstDataOffset.TabIndex = 1;
            // 
            // labelFirstDataOffset
            // 
            this.labelFirstDataOffset.AutoSize = true;
            this.labelFirstDataOffset.Location = new System.Drawing.Point(70, 185);
            this.labelFirstDataOffset.Name = "labelFirstDataOffset";
            this.labelFirstDataOffset.Size = new System.Drawing.Size(64, 13);
            this.labelFirstDataOffset.TabIndex = 0;
            this.labelFirstDataOffset.Text = "Data Offset:";
            // 
            // DialogSelectFileToReplace
            // 
            this.DialogSelectFileToReplace.Title = "Select the file to replace";
            // 
            // statusStrip
            // 
            this.statusStrip.GripMargin = new System.Windows.Forms.Padding(2, 3, 2, 2);
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripProgressBar,
            this.toolStripStatusLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 493);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(935, 22);
            this.statusStrip.TabIndex = 22;
            this.statusStrip.Text = "statusStrip";
            // 
            // toolStripProgressBar
            // 
            this.toolStripProgressBar.Name = "toolStripProgressBar";
            this.toolStripProgressBar.Size = new System.Drawing.Size(100, 16);
            this.toolStripProgressBar.Visible = false;
            // 
            // toolStripStatusLabel
            // 
            this.toolStripStatusLabel.Name = "toolStripStatusLabel";
            this.toolStripStatusLabel.Size = new System.Drawing.Size(112, 17);
            this.toolStripStatusLabel.Text = "toolStripStatusLabel";
            this.toolStripStatusLabel.Visible = false;
            // 
            // extractFolderDialog
            // 
            this.extractFolderDialog.Description = "Select the destination folder";
            // 
            // backgroundWorkerExtractFile
            // 
            this.backgroundWorkerExtractFile.WorkerReportsProgress = true;
            this.backgroundWorkerExtractFile.WorkerSupportsCancellation = true;
            this.backgroundWorkerExtractFile.DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorkerExtractFile_DoWork);
            // 
            // backgroundWorkerEditFile
            // 
            this.backgroundWorkerEditFile.WorkerReportsProgress = true;
            this.backgroundWorkerEditFile.WorkerSupportsCancellation = true;
            this.backgroundWorkerEditFile.DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorkerReplaceFile_DoWork);
            this.backgroundWorkerEditFile.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.backgroundWorkerReplaceFile_ProgressChanged);
            // 
            // toolStripMenu
            // 
            this.toolStripMenu.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.toolStripMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripOpenFile,
            this.toolStripSaveFile,
            this.toolStripSeparator,
            this.toolStripInfo,
            this.toolStripAbout});
            this.toolStripMenu.Location = new System.Drawing.Point(0, 0);
            this.toolStripMenu.Name = "toolStripMenu";
            this.toolStripMenu.Size = new System.Drawing.Size(935, 31);
            this.toolStripMenu.TabIndex = 23;
            this.toolStripMenu.Text = "Open File";
            // 
            // toolStripOpenFile
            // 
            this.toolStripOpenFile.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripOpenFile.Image = global::ME3Explorer.Properties.Resources.folder;
            this.toolStripOpenFile.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripOpenFile.Name = "toolStripOpenFile";
            this.toolStripOpenFile.Size = new System.Drawing.Size(28, 28);
            this.toolStripOpenFile.Text = "Open .sfar file";
            this.toolStripOpenFile.Click += new System.EventHandler(this.toolStripOpenFile_Click);
            // 
            // toolStripSaveFile
            // 
            this.toolStripSaveFile.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripSaveFile.Enabled = false;
            this.toolStripSaveFile.Image = global::ME3Explorer.Properties.Resources.document_save;
            this.toolStripSaveFile.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripSaveFile.Name = "toolStripSaveFile";
            this.toolStripSaveFile.Size = new System.Drawing.Size(28, 28);
            this.toolStripSaveFile.Text = "Save modified file";
            this.toolStripSaveFile.Click += new System.EventHandler(this.toolStripSaveFile_Click);
            // 
            // toolStripSeparator
            // 
            this.toolStripSeparator.Name = "toolStripSeparator";
            this.toolStripSeparator.Size = new System.Drawing.Size(6, 31);
            // 
            // toolStripInfo
            // 
            this.toolStripInfo.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripInfo.Image = global::ME3Explorer.Properties.Resources.info;
            this.toolStripInfo.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripInfo.Name = "toolStripInfo";
            this.toolStripInfo.Size = new System.Drawing.Size(28, 28);
            this.toolStripInfo.Text = "General Info";
            this.toolStripInfo.Click += new System.EventHandler(this.toolStripInfo_Click);
            // 
            // toolStripAbout
            // 
            this.toolStripAbout.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripAbout.Image = global::ME3Explorer.Properties.Resources.help_browser;
            this.toolStripAbout.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripAbout.Name = "toolStripAbout";
            this.toolStripAbout.Size = new System.Drawing.Size(28, 28);
            this.toolStripAbout.Text = "About";
            this.toolStripAbout.Click += new System.EventHandler(this.toolStripAbout_Click);
            // 
            // DLCExplorer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(935, 515);
            this.Controls.Add(this.toolStripMenu);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.groupBoxSfar);
            this.Controls.Add(this.treeViewSfar);
            this.Controls.Add(this.groupBoxFile);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(800, 542);
            this.Name = "DLCExplorer";
            this.Text = "Mass Effect 3 DLC Explorer";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.contextMenuStripEditor.ResumeLayout(false);
            this.groupBoxFile.ResumeLayout(false);
            this.groupBoxFile.PerformLayout();
            this.groupBoxSfar.ResumeLayout(false);
            this.groupBoxSfar.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.toolStripMenu.ResumeLayout(false);
            this.toolStripMenu.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        public System.Windows.Forms.TreeView treeViewSfar;
        public System.Windows.Forms.OpenFileDialog DialogOpenSfarFile;
        public System.Windows.Forms.ContextMenuStrip contextMenuStripEditor;
        public System.Windows.Forms.ToolStripMenuItem extractToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem replaceToolStripMenuItem;
        public System.Windows.Forms.ImageList imageListIcons;
        public System.Windows.Forms.SaveFileDialog extractFileDialog;
        public System.Windows.Forms.GroupBox groupBoxFile;
        public System.Windows.Forms.Label labelDataOffset;
        public System.Windows.Forms.TextBox textBoxDataOffset;
        public System.Windows.Forms.Label labelFullName;
        public System.Windows.Forms.Label labelComprSizeBytes;
        public System.Windows.Forms.Label labelHash;
        public System.Windows.Forms.TextBox textBoxHash;
        public System.Windows.Forms.TextBox textBoxBlockIndex;
        public System.Windows.Forms.TextBox textBoxComprSize;
        public System.Windows.Forms.TextBox textBoxFullName;
        public System.Windows.Forms.Label labelBlockIndex;
        public System.Windows.Forms.Label labelComprSize;
        public System.Windows.Forms.TextBox textBoxEntry;
        public System.Windows.Forms.Label labelEntry;
        public System.Windows.Forms.TextBox textBoxUncSize;
        public System.Windows.Forms.Label labelUncSizeBytes;
        public System.Windows.Forms.Label labelFileSize;
        public System.Windows.Forms.GroupBox groupBoxSfar;
        public System.Windows.Forms.TextBox textBoxNumOfFiles;
        public System.Windows.Forms.Label labelNumOfFiles;
        public System.Windows.Forms.Label labelFirstBlockOffset;
        public System.Windows.Forms.TextBox textBoxFirstBlockOffset;
        public System.Windows.Forms.TextBox textBoxFirstEntryOffset;
        public System.Windows.Forms.Label labelFirstEntryOffset;
        public System.Windows.Forms.Label labelComprRatio;
        public System.Windows.Forms.Label labelTotalUncSize;
        public System.Windows.Forms.TextBox textBoxTotalUncSize;
        public System.Windows.Forms.TextBox textBoxTotalComprSize;
        public System.Windows.Forms.Label labelTotalComprSize;
        public System.Windows.Forms.TextBox textBoxFirstDataOffset;
        public System.Windows.Forms.Label labelFirstDataOffset;
        public System.Windows.Forms.Label labelTotalComprBytes;
        public System.Windows.Forms.Label labelTotalUncBytes;
        public System.Windows.Forms.OpenFileDialog DialogSelectFileToReplace;
        public System.Windows.Forms.StatusStrip statusStrip;
        public System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        public System.Windows.Forms.TextBox textBoxCRatio;
        public System.Windows.Forms.FolderBrowserDialog extractFolderDialog;
        public System.ComponentModel.BackgroundWorker backgroundWorkerExtractFile;
        public System.ComponentModel.BackgroundWorker backgroundWorkerEditFile;
        public System.Windows.Forms.ToolStripMenuItem deleteToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem propertiesToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem addFileToolStripMenuItem;
        public System.Windows.Forms.ToolStrip toolStripMenu;
        public System.Windows.Forms.ToolStripButton toolStripOpenFile;
        public System.Windows.Forms.ToolStripButton toolStripSaveFile;
        public System.Windows.Forms.ToolStripSeparator toolStripSeparator;
        public System.Windows.Forms.ToolStripButton toolStripInfo;
        public System.Windows.Forms.ToolStripButton toolStripAbout;
        public System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel;
        public System.Windows.Forms.ToolStripProgressBar toolStripProgressBar;
        public System.Windows.Forms.ToolStripMenuItem undoToolStripMenuItem;
        private System.ComponentModel.IContainer components;
    }
}

