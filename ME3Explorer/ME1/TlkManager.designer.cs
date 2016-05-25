namespace ME1Explorer
{
    partial class TlkManager
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TlkManager));
            this.fileBox = new System.Windows.Forms.ListBox();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.bioTlkSetBox = new System.Windows.Forms.ListBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.tlkFileBox = new System.Windows.Forms.ListBox();
            this.tlkFileContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.splitContainer3 = new System.Windows.Forms.SplitContainer();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.selectedTlkFilesBox = new System.Windows.Forms.ListBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.toolStrip2 = new System.Windows.Forms.ToolStrip();
            this.addToSelectedTlkFilesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveToFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addFileButton = new System.Windows.Forms.ToolStripButton();
            this.replaceWithFileButton = new System.Windows.Forms.ToolStripButton();
            this.saveToFileButton = new System.Windows.Forms.ToolStripButton();
            this.addFromTlkFilesButton = new System.Windows.Forms.ToolStripButton();
            this.removeButton = new System.Windows.Forms.ToolStripButton();
            this.downButton = new System.Windows.Forms.ToolStripButton();
            this.upButton = new System.Windows.Forms.ToolStripButton();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.tlkFileContextMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).BeginInit();
            this.splitContainer3.Panel1.SuspendLayout();
            this.splitContainer3.Panel2.SuspendLayout();
            this.splitContainer3.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.groupBox4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.toolStrip2.SuspendLayout();
            this.SuspendLayout();
            // 
            // fileBox
            // 
            this.fileBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.fileBox.FormattingEnabled = true;
            this.fileBox.Location = new System.Drawing.Point(3, 16);
            this.fileBox.Name = "fileBox";
            this.fileBox.Size = new System.Drawing.Size(261, 254);
            this.fileBox.TabIndex = 0;
            this.fileBox.SelectedIndexChanged += new System.EventHandler(this.fileBox_SelectedIndexChanged);
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.groupBox2);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.groupBox3);
            this.splitContainer2.Size = new System.Drawing.Size(528, 273);
            this.splitContainer2.SplitterDistance = 251;
            this.splitContainer2.TabIndex = 1;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.bioTlkSetBox);
            this.groupBox2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox2.Location = new System.Drawing.Point(0, 0);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(251, 273);
            this.groupBox2.TabIndex = 2;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "BioTlkSets";
            // 
            // bioTlkSetBox
            // 
            this.bioTlkSetBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.bioTlkSetBox.FormattingEnabled = true;
            this.bioTlkSetBox.Location = new System.Drawing.Point(3, 16);
            this.bioTlkSetBox.Name = "bioTlkSetBox";
            this.bioTlkSetBox.Size = new System.Drawing.Size(245, 254);
            this.bioTlkSetBox.TabIndex = 1;
            this.bioTlkSetBox.SelectedIndexChanged += new System.EventHandler(this.bioTlkSetBox_SelectedIndexChanged);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.tlkFileBox);
            this.groupBox3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox3.Location = new System.Drawing.Point(0, 0);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(273, 273);
            this.groupBox3.TabIndex = 1;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "TlkFiles";
            // 
            // tlkFileBox
            // 
            this.tlkFileBox.ContextMenuStrip = this.tlkFileContextMenu;
            this.tlkFileBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlkFileBox.FormattingEnabled = true;
            this.tlkFileBox.Location = new System.Drawing.Point(3, 16);
            this.tlkFileBox.Name = "tlkFileBox";
            this.tlkFileBox.Size = new System.Drawing.Size(267, 254);
            this.tlkFileBox.TabIndex = 0;
            this.tlkFileBox.DoubleClick += new System.EventHandler(this.tlkFileBox_DoubleClick);
            // 
            // tlkFileContextMenu
            // 
            this.tlkFileContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addToSelectedTlkFilesToolStripMenuItem,
            this.saveToFileToolStripMenuItem});
            this.tlkFileContextMenu.Name = "tlkFileContextMenu";
            this.tlkFileContextMenu.Size = new System.Drawing.Size(200, 70);
            // 
            // splitContainer3
            // 
            this.splitContainer3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer3.Location = new System.Drawing.Point(0, 25);
            this.splitContainer3.Name = "splitContainer3";
            // 
            // splitContainer3.Panel1
            // 
            this.splitContainer3.Panel1.Controls.Add(this.groupBox1);
            // 
            // splitContainer3.Panel2
            // 
            this.splitContainer3.Panel2.Controls.Add(this.splitContainer2);
            this.splitContainer3.Size = new System.Drawing.Size(799, 273);
            this.splitContainer3.SplitterDistance = 267;
            this.splitContainer3.TabIndex = 1;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.fileBox);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox1.Location = new System.Drawing.Point(0, 0);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(267, 273);
            this.groupBox1.TabIndex = 2;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Files";
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addFileButton,
            this.replaceWithFileButton,
            this.saveToFileButton});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(799, 25);
            this.toolStrip1.TabIndex = 2;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.selectedTlkFilesBox);
            this.groupBox4.Controls.Add(this.toolStrip2);
            this.groupBox4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox4.Location = new System.Drawing.Point(0, 0);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(799, 197);
            this.groupBox4.TabIndex = 1;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Selected TlkFiles";
            // 
            // selectedTlkFilesBox
            // 
            this.selectedTlkFilesBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.selectedTlkFilesBox.FormattingEnabled = true;
            this.selectedTlkFilesBox.Location = new System.Drawing.Point(3, 41);
            this.selectedTlkFilesBox.Name = "selectedTlkFilesBox";
            this.selectedTlkFilesBox.Size = new System.Drawing.Size(793, 153);
            this.selectedTlkFilesBox.TabIndex = 0;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.splitContainer3);
            this.splitContainer1.Panel1.Controls.Add(this.toolStrip1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.groupBox4);
            this.splitContainer1.Size = new System.Drawing.Size(799, 499);
            this.splitContainer1.SplitterDistance = 298;
            this.splitContainer1.TabIndex = 2;
            // 
            // toolStrip2
            // 
            this.toolStrip2.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addFromTlkFilesButton,
            this.removeButton,
            this.downButton,
            this.upButton});
            this.toolStrip2.Location = new System.Drawing.Point(3, 16);
            this.toolStrip2.Name = "toolStrip2";
            this.toolStrip2.Size = new System.Drawing.Size(793, 25);
            this.toolStrip2.TabIndex = 1;
            this.toolStrip2.Text = "toolStrip2";
            // 
            // addToSelectedTlkFilesToolStripMenuItem
            // 
            this.addToSelectedTlkFilesToolStripMenuItem.Name = "addToSelectedTlkFilesToolStripMenuItem";
            this.addToSelectedTlkFilesToolStripMenuItem.Size = new System.Drawing.Size(199, 22);
            this.addToSelectedTlkFilesToolStripMenuItem.Text = "Add to Selected TlkFiles";
            this.addToSelectedTlkFilesToolStripMenuItem.Click += new System.EventHandler(this.addToSelectedTlkFilesToolStripMenuItem_Click);
            // 
            // saveToFileToolStripMenuItem
            // 
            this.saveToFileToolStripMenuItem.Name = "saveToFileToolStripMenuItem";
            this.saveToFileToolStripMenuItem.Size = new System.Drawing.Size(199, 22);
            this.saveToFileToolStripMenuItem.Text = "Save To File";
            this.saveToFileToolStripMenuItem.Visible = false;
            this.saveToFileToolStripMenuItem.Click += new System.EventHandler(this.saveToFileToolStripMenuItem_Click);
            // 
            // addFileButton
            // 
            this.addFileButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.addFileButton.Image = ((System.Drawing.Image)(resources.GetObject("addFileButton.Image")));
            this.addFileButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.addFileButton.Name = "addFileButton";
            this.addFileButton.Size = new System.Drawing.Size(61, 22);
            this.addFileButton.Text = "Open File";
            this.addFileButton.Click += new System.EventHandler(this.addFileButton_Click);
            // 
            // replaceWithFileButton
            // 
            this.replaceWithFileButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.replaceWithFileButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.replaceWithFileButton.Image = ((System.Drawing.Image)(resources.GetObject("replaceWithFileButton.Image")));
            this.replaceWithFileButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.replaceWithFileButton.Name = "replaceWithFileButton";
            this.replaceWithFileButton.Size = new System.Drawing.Size(101, 22);
            this.replaceWithFileButton.Text = "Replace With File";
            this.replaceWithFileButton.Visible = false;
            this.replaceWithFileButton.Click += new System.EventHandler(this.replaceWithFileButton_Click);
            // 
            // saveToFileButton
            // 
            this.saveToFileButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.saveToFileButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.saveToFileButton.Image = ((System.Drawing.Image)(resources.GetObject("saveToFileButton.Image")));
            this.saveToFileButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.saveToFileButton.Name = "saveToFileButton";
            this.saveToFileButton.Size = new System.Drawing.Size(73, 22);
            this.saveToFileButton.Text = "Save To File";
            this.saveToFileButton.Visible = false;
            this.saveToFileButton.Click += new System.EventHandler(this.saveToFileButton_Click);
            // 
            // addFromTlkFilesButton
            // 
            this.addFromTlkFilesButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.addFromTlkFilesButton.Image = ((System.Drawing.Image)(resources.GetObject("addFromTlkFilesButton.Image")));
            this.addFromTlkFilesButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.addFromTlkFilesButton.Name = "addFromTlkFilesButton";
            this.addFromTlkFilesButton.Size = new System.Drawing.Size(104, 22);
            this.addFromTlkFilesButton.Text = "Add from TlkFiles";
            this.addFromTlkFilesButton.Click += new System.EventHandler(this.addFromTlkFilesButton_Click);
            // 
            // removeButton
            // 
            this.removeButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.removeButton.Image = ((System.Drawing.Image)(resources.GetObject("removeButton.Image")));
            this.removeButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.removeButton.Name = "removeButton";
            this.removeButton.Size = new System.Drawing.Size(54, 22);
            this.removeButton.Text = "Remove";
            this.removeButton.Click += new System.EventHandler(this.removeButton_Click);
            // 
            // downButton
            // 
            this.downButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.downButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.downButton.Image = ((System.Drawing.Image)(resources.GetObject("downButton.Image")));
            this.downButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.downButton.Name = "downButton";
            this.downButton.Size = new System.Drawing.Size(42, 22);
            this.downButton.Text = "Down";
            this.downButton.Click += new System.EventHandler(this.downButton_Click);
            // 
            // upButton
            // 
            this.upButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.upButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.upButton.Image = ((System.Drawing.Image)(resources.GetObject("upButton.Image")));
            this.upButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.upButton.Name = "upButton";
            this.upButton.Size = new System.Drawing.Size(26, 22);
            this.upButton.Text = "Up";
            this.upButton.Click += new System.EventHandler(this.upButton_Click);
            // 
            // TlkManager
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(799, 499);
            this.Controls.Add(this.splitContainer1);
            this.Name = "TlkManager";
            this.Text = "TlkManager";
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.tlkFileContextMenu.ResumeLayout(false);
            this.splitContainer3.Panel1.ResumeLayout(false);
            this.splitContainer3.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).EndInit();
            this.splitContainer3.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.toolStrip2.ResumeLayout(false);
            this.toolStrip2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox fileBox;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.SplitContainer splitContainer3;
        private System.Windows.Forms.ListBox bioTlkSetBox;
        private System.Windows.Forms.ListBox tlkFileBox;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.ListBox selectedTlkFilesBox;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton addFileButton;
        private System.Windows.Forms.ContextMenuStrip tlkFileContextMenu;
        private System.Windows.Forms.ToolStrip toolStrip2;
        private System.Windows.Forms.ToolStripButton addFromTlkFilesButton;
        private System.Windows.Forms.ToolStripButton removeButton;
        private System.Windows.Forms.ToolStripButton downButton;
        private System.Windows.Forms.ToolStripButton upButton;
        private System.Windows.Forms.ToolStripMenuItem addToSelectedTlkFilesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveToFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripButton replaceWithFileButton;
        private System.Windows.Forms.ToolStripButton saveToFileButton;
    }
}