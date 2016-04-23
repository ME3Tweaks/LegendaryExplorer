namespace ME3Explorer
{
    partial class CondEditor
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
        public void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CondEditor));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadCodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveCodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveBinaryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.compileAndReplaceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.compileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replaceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.rtb1 = new System.Windows.Forms.RichTextBox();
            this.rtb2 = new System.Windows.Forms.RichTextBox();
            this.treeView1 = new System.Windows.Forms.TreeView();
            this.menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
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
            this.editorToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(487, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            this.menuStrip1.Visible = false;
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.loadCodeToolStripMenuItem,
            this.saveCodeToolStripMenuItem,
            this.saveBinaryToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // loadCodeToolStripMenuItem
            // 
            this.loadCodeToolStripMenuItem.Name = "loadCodeToolStripMenuItem";
            this.loadCodeToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.loadCodeToolStripMenuItem.Text = "Load Code";
            this.loadCodeToolStripMenuItem.Click += new System.EventHandler(this.loadCodeToolStripMenuItem_Click);
            // 
            // saveCodeToolStripMenuItem
            // 
            this.saveCodeToolStripMenuItem.Name = "saveCodeToolStripMenuItem";
            this.saveCodeToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.saveCodeToolStripMenuItem.Text = "Save Code";
            this.saveCodeToolStripMenuItem.Click += new System.EventHandler(this.saveCodeToolStripMenuItem_Click);
            // 
            // saveBinaryToolStripMenuItem
            // 
            this.saveBinaryToolStripMenuItem.Name = "saveBinaryToolStripMenuItem";
            this.saveBinaryToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.saveBinaryToolStripMenuItem.Text = "Save Binary";
            this.saveBinaryToolStripMenuItem.Click += new System.EventHandler(this.saveBinaryToolStripMenuItem_Click);
            // 
            // editorToolStripMenuItem
            // 
            this.editorToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.compileAndReplaceToolStripMenuItem,
            this.compileToolStripMenuItem,
            this.replaceToolStripMenuItem});
            this.editorToolStripMenuItem.Name = "editorToolStripMenuItem";
            this.editorToolStripMenuItem.Size = new System.Drawing.Size(50, 20);
            this.editorToolStripMenuItem.Text = "Editor";
            // 
            // compileAndReplaceToolStripMenuItem
            // 
            this.compileAndReplaceToolStripMenuItem.Name = "compileAndReplaceToolStripMenuItem";
            this.compileAndReplaceToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F6;
            this.compileAndReplaceToolStripMenuItem.Size = new System.Drawing.Size(205, 22);
            this.compileAndReplaceToolStripMenuItem.Text = "Compile and Replace";
            this.compileAndReplaceToolStripMenuItem.Click += new System.EventHandler(this.compileAndReplaceToolStripMenuItem_Click);
            // 
            // compileToolStripMenuItem
            // 
            this.compileToolStripMenuItem.Name = "compileToolStripMenuItem";
            this.compileToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F5;
            this.compileToolStripMenuItem.Size = new System.Drawing.Size(205, 22);
            this.compileToolStripMenuItem.Text = "Compile";
            this.compileToolStripMenuItem.Click += new System.EventHandler(this.compileToolStripMenuItem_Click);
            // 
            // replaceToolStripMenuItem
            // 
            this.replaceToolStripMenuItem.Name = "replaceToolStripMenuItem";
            this.replaceToolStripMenuItem.Size = new System.Drawing.Size(205, 22);
            this.replaceToolStripMenuItem.Text = "Replace";
            this.replaceToolStripMenuItem.Click += new System.EventHandler(this.replaceToolStripMenuItem_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 24);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.splitContainer2);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.treeView1);
            this.splitContainer1.Size = new System.Drawing.Size(487, 289);
            this.splitContainer1.SplitterDistance = 327;
            this.splitContainer1.TabIndex = 1;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.rtb1);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.rtb2);
            this.splitContainer2.Size = new System.Drawing.Size(327, 289);
            this.splitContainer2.SplitterDistance = 207;
            this.splitContainer2.TabIndex = 0;
            // 
            // rtb1
            // 
            this.rtb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtb1.Location = new System.Drawing.Point(0, 0);
            this.rtb1.Name = "rtb1";
            this.rtb1.Size = new System.Drawing.Size(327, 207);
            this.rtb1.TabIndex = 0;
            this.rtb1.Text = "";
            // 
            // rtb2
            // 
            this.rtb2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb2.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtb2.Location = new System.Drawing.Point(0, 0);
            this.rtb2.Name = "rtb2";
            this.rtb2.Size = new System.Drawing.Size(327, 78);
            this.rtb2.TabIndex = 0;
            this.rtb2.Text = "";
            this.rtb2.WordWrap = false;
            // 
            // treeView1
            // 
            this.treeView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.treeView1.Location = new System.Drawing.Point(0, 0);
            this.treeView1.Name = "treeView1";
            this.treeView1.Size = new System.Drawing.Size(156, 289);
            this.treeView1.TabIndex = 0;
            // 
            // CondEditor
            // 
            this.ClientSize = new System.Drawing.Size(487, 313);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "CondEditor";
            this.Text = "Condition Editor";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.CondEditor_FormClosing);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem editorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem compileToolStripMenuItem;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.SplitContainer splitContainer2;
        public System.Windows.Forms.RichTextBox rtb1;
        public System.Windows.Forms.RichTextBox rtb2;
        private System.Windows.Forms.TreeView treeView1;
        public System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem loadCodeToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem saveCodeToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem saveBinaryToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem replaceToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem compileAndReplaceToolStripMenuItem;



    }
}