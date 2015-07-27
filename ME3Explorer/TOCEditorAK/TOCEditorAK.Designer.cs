namespace ME3Explorer.TOCEditorAK
{
    partial class TOCEditorAK
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
            this.treeViewTOC = new System.Windows.Forms.TreeView();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fixSizesAndHashesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeNotExistingFilesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.recoverFromBackupToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.contextMenuStripFiles = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.removeFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStripBlock = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.emptyBlockToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeBlockToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.trvToolTip = new System.Windows.Forms.ToolTip(this.components);
            this.menuStrip1.SuspendLayout();
            this.contextMenuStripFiles.SuspendLayout();
            this.contextMenuStripBlock.SuspendLayout();
            this.SuspendLayout();
            // 
            // treeViewTOC
            // 
            this.treeViewTOC.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.treeViewTOC.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.treeViewTOC.Location = new System.Drawing.Point(12, 35);
            this.treeViewTOC.Name = "treeViewTOC";
            this.treeViewTOC.ShowNodeToolTips = true;
            this.treeViewTOC.Size = new System.Drawing.Size(527, 319);
            this.treeViewTOC.TabIndex = 0;
            this.treeViewTOC.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.treeViewTOC_NodeMouseClick);
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.editToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(551, 24);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            this.menuStrip1.Visible = false;
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openToolStripMenuItem,
            this.saveToolStripMenuItem,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(35, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new System.Drawing.Size(100, 22);
            this.openToolStripMenuItem.Text = "Open";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
            // 
            // saveToolStripMenuItem
            // 
            this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            this.saveToolStripMenuItem.Size = new System.Drawing.Size(100, 22);
            this.saveToolStripMenuItem.Text = "Save";
            this.saveToolStripMenuItem.Click += new System.EventHandler(this.saveToolStripMenuItem_Click);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(100, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fixSizesAndHashesToolStripMenuItem,
            this.removeNotExistingFilesToolStripMenuItem,
            this.recoverFromBackupToolStripMenuItem});
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.editToolStripMenuItem.Text = "Edit";
            // 
            // fixSizesAndHashesToolStripMenuItem
            // 
            this.fixSizesAndHashesToolStripMenuItem.Name = "fixSizesAndHashesToolStripMenuItem";
            this.fixSizesAndHashesToolStripMenuItem.Size = new System.Drawing.Size(194, 22);
            this.fixSizesAndHashesToolStripMenuItem.Text = "Fix sizes and hashes";
            this.fixSizesAndHashesToolStripMenuItem.Click += new System.EventHandler(this.fixSizesAndHashesToolStripMenuItem_Click);
            // 
            // removeNotExistingFilesToolStripMenuItem
            // 
            this.removeNotExistingFilesToolStripMenuItem.Name = "removeNotExistingFilesToolStripMenuItem";
            this.removeNotExistingFilesToolStripMenuItem.Size = new System.Drawing.Size(194, 22);
            this.removeNotExistingFilesToolStripMenuItem.Text = "Remove not existing files";
            this.removeNotExistingFilesToolStripMenuItem.Click += new System.EventHandler(this.removeNotExistingFilesToolStripMenuItem_Click);
            // 
            // recoverFromBackupToolStripMenuItem
            // 
            this.recoverFromBackupToolStripMenuItem.Name = "recoverFromBackupToolStripMenuItem";
            this.recoverFromBackupToolStripMenuItem.Size = new System.Drawing.Size(194, 22);
            this.recoverFromBackupToolStripMenuItem.Text = "Recover original toc.bin";
            this.recoverFromBackupToolStripMenuItem.Visible = false;
            this.recoverFromBackupToolStripMenuItem.Click += new System.EventHandler(this.recoverFromBackupToolStripMenuItem_Click);
            // 
            // openFileDialog
            // 
            this.openFileDialog.Filter = "PCConsoleTOC.bin|PCConsoleTOC.bin|All files|*.*";
            // 
            // contextMenuStripFiles
            // 
            this.contextMenuStripFiles.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.removeFileToolStripMenuItem});
            this.contextMenuStripFiles.Name = "contextMenuStripFiles";
            this.contextMenuStripFiles.Size = new System.Drawing.Size(131, 26);
            // 
            // removeFileToolStripMenuItem
            // 
            this.removeFileToolStripMenuItem.Name = "removeFileToolStripMenuItem";
            this.removeFileToolStripMenuItem.Size = new System.Drawing.Size(130, 22);
            this.removeFileToolStripMenuItem.Text = "Remove file";
            this.removeFileToolStripMenuItem.Click += new System.EventHandler(this.removeFileToolStripMenuItem_Click);
            // 
            // contextMenuStripBlock
            // 
            this.contextMenuStripBlock.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addFileToolStripMenuItem,
            this.emptyBlockToolStripMenuItem,
            this.removeBlockToolStripMenuItem});
            this.contextMenuStripBlock.Name = "contextMenuStripBlock";
            this.contextMenuStripBlock.Size = new System.Drawing.Size(185, 70);
            // 
            // addFileToolStripMenuItem
            // 
            this.addFileToolStripMenuItem.Name = "addFileToolStripMenuItem";
            this.addFileToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.addFileToolStripMenuItem.Text = "Add file";
            this.addFileToolStripMenuItem.Click += new System.EventHandler(this.addFileToolStripMenuItem_Click);
            // 
            // emptyBlockToolStripMenuItem
            // 
            this.emptyBlockToolStripMenuItem.Name = "emptyBlockToolStripMenuItem";
            this.emptyBlockToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.emptyBlockToolStripMenuItem.Text = "Empty block";
            this.emptyBlockToolStripMenuItem.Click += new System.EventHandler(this.emptyBlockToolStripMenuItem_Click);
            // 
            // removeBlockToolStripMenuItem
            // 
            this.removeBlockToolStripMenuItem.Name = "removeBlockToolStripMenuItem";
            this.removeBlockToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.removeBlockToolStripMenuItem.Text = "Remove block (broken)";
            this.removeBlockToolStripMenuItem.Visible = false;
            this.removeBlockToolStripMenuItem.Click += new System.EventHandler(this.removeBlockToolStripMenuItem_Click);
            // 
            // TOCEditorAK
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(551, 366);
            this.Controls.Add(this.treeViewTOC);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "TOCEditorAK";
            this.Text = "PCConsoleTOC.bin editor by AmaroK86";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.contextMenuStripFiles.ResumeLayout(false);
            this.contextMenuStripBlock.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TreeView treeViewTOC;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.OpenFileDialog openFileDialog;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fixSizesAndHashesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem recoverFromBackupToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeNotExistingFilesToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuStripFiles;
        private System.Windows.Forms.ToolStripMenuItem removeFileToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuStripBlock;
        private System.Windows.Forms.ToolStripMenuItem emptyBlockToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeBlockToolStripMenuItem;
        private System.Windows.Forms.ToolTip trvToolTip;
    }
}