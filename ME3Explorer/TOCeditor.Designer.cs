namespace ME3Explorer
{
    partial class TOCeditor
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        public System.ComponentModel.IContainer components = null;

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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TOCeditor));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.searchToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.findStringToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.searchAgainToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editFilesizeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.searchToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(292, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            this.menuStrip1.Visible = false;
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openToolStripMenuItem,
            this.saveToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(35, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.openToolStripMenuItem.Text = "Open";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
            // 
            // saveToolStripMenuItem
            // 
            this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            this.saveToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.saveToolStripMenuItem.Text = "Save";
            this.saveToolStripMenuItem.Click += new System.EventHandler(this.saveToolStripMenuItem_Click);
            // 
            // searchToolStripMenuItem
            // 
            this.searchToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.findStringToolStripMenuItem,
            this.searchAgainToolStripMenuItem,
            this.editFilesizeToolStripMenuItem});
            this.searchToolStripMenuItem.Name = "searchToolStripMenuItem";
            this.searchToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.searchToolStripMenuItem.Text = "Edit";
            // 
            // findStringToolStripMenuItem
            // 
            this.findStringToolStripMenuItem.Name = "findStringToolStripMenuItem";
            this.findStringToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
            this.findStringToolStripMenuItem.Text = "Find String";
            this.findStringToolStripMenuItem.Click += new System.EventHandler(this.findStringToolStripMenuItem_Click);
            // 
            // searchAgainToolStripMenuItem
            // 
            this.searchAgainToolStripMenuItem.Name = "searchAgainToolStripMenuItem";
            this.searchAgainToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F3;
            this.searchAgainToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
            this.searchAgainToolStripMenuItem.Text = "Search again";
            this.searchAgainToolStripMenuItem.Click += new System.EventHandler(this.searchAgainToolStripMenuItem_Click);
            // 
            // editFilesizeToolStripMenuItem
            // 
            this.editFilesizeToolStripMenuItem.Name = "editFilesizeToolStripMenuItem";
            this.editFilesizeToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
            this.editFilesizeToolStripMenuItem.Text = "Edit Filesize";
            this.editFilesizeToolStripMenuItem.Click += new System.EventHandler(this.editFilesizeToolStripMenuItem_Click);
            // 
            // listBox1
            // 
            this.listBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBox1.FormattingEnabled = true;
            this.listBox1.IntegralHeight = false;
            this.listBox1.ItemHeight = 16;
            this.listBox1.Location = new System.Drawing.Point(0, 24);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(292, 249);
            this.listBox1.TabIndex = 1;
            // 
            // TOCeditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(292, 273);
            this.Controls.Add(this.listBox1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "TOCeditor";
            this.Text = "TOCeditor";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.TOCeditor_FormClosing);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        public System.Windows.Forms.MenuStrip menuStrip1;
        public System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        public System.Windows.Forms.ListBox listBox1;
        public System.Windows.Forms.ToolStripMenuItem searchToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem findStringToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem editFilesizeToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem searchAgainToolStripMenuItem;
    }
}