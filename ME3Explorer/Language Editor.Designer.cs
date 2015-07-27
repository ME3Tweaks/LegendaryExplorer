namespace ME3Explorer
{
    partial class Language_Editor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Language_Editor));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyLanguagToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteLanguageToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.renameLanguageToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editEntryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.setLanguageAsDefaultToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.listBox2 = new System.Windows.Forms.ListBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.editToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(292, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.saveToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(71, 20);
            this.fileToolStripMenuItem.Text = "Languages";
            // 
            // saveToolStripMenuItem
            // 
            this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            this.saveToolStripMenuItem.Size = new System.Drawing.Size(98, 22);
            this.saveToolStripMenuItem.Text = "Save";
            this.saveToolStripMenuItem.Click += new System.EventHandler(this.saveToolStripMenuItem_Click);
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copyLanguagToolStripMenuItem,
            this.deleteLanguageToolStripMenuItem,
            this.renameLanguageToolStripMenuItem,
            this.editEntryToolStripMenuItem,
            this.setLanguageAsDefaultToolStripMenuItem});
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.editToolStripMenuItem.Text = "Edit";
            // 
            // copyLanguagToolStripMenuItem
            // 
            this.copyLanguagToolStripMenuItem.Name = "copyLanguagToolStripMenuItem";
            this.copyLanguagToolStripMenuItem.Size = new System.Drawing.Size(192, 22);
            this.copyLanguagToolStripMenuItem.Text = "Copy Language";
            this.copyLanguagToolStripMenuItem.Click += new System.EventHandler(this.copyLanguagToolStripMenuItem_Click);
            // 
            // deleteLanguageToolStripMenuItem
            // 
            this.deleteLanguageToolStripMenuItem.Name = "deleteLanguageToolStripMenuItem";
            this.deleteLanguageToolStripMenuItem.Size = new System.Drawing.Size(192, 22);
            this.deleteLanguageToolStripMenuItem.Text = "Delete Language";
            this.deleteLanguageToolStripMenuItem.Click += new System.EventHandler(this.deleteLanguageToolStripMenuItem_Click);
            // 
            // renameLanguageToolStripMenuItem
            // 
            this.renameLanguageToolStripMenuItem.Name = "renameLanguageToolStripMenuItem";
            this.renameLanguageToolStripMenuItem.Size = new System.Drawing.Size(192, 22);
            this.renameLanguageToolStripMenuItem.Text = "Rename Language";
            this.renameLanguageToolStripMenuItem.Click += new System.EventHandler(this.renameLanguageToolStripMenuItem_Click);
            // 
            // editEntryToolStripMenuItem
            // 
            this.editEntryToolStripMenuItem.Name = "editEntryToolStripMenuItem";
            this.editEntryToolStripMenuItem.Size = new System.Drawing.Size(192, 22);
            this.editEntryToolStripMenuItem.Text = "Edit Entry";
            this.editEntryToolStripMenuItem.Click += new System.EventHandler(this.editEntryToolStripMenuItem_Click);
            // 
            // setLanguageAsDefaultToolStripMenuItem
            // 
            this.setLanguageAsDefaultToolStripMenuItem.Name = "setLanguageAsDefaultToolStripMenuItem";
            this.setLanguageAsDefaultToolStripMenuItem.Size = new System.Drawing.Size(192, 22);
            this.setLanguageAsDefaultToolStripMenuItem.Text = "Set Language as Default";
            this.setLanguageAsDefaultToolStripMenuItem.Click += new System.EventHandler(this.setLanguageAsDefaultToolStripMenuItem_Click);
            // 
            // listBox2
            // 
            this.listBox2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox2.FormattingEnabled = true;
            this.listBox2.IntegralHeight = false;
            this.listBox2.Location = new System.Drawing.Point(0, 0);
            this.listBox2.Name = "listBox2";
            this.listBox2.Size = new System.Drawing.Size(191, 249);
            this.listBox2.TabIndex = 0;
            this.listBox2.DoubleClick += new System.EventHandler(this.listBox2_DoubleClick);
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
            this.splitContainer1.Panel2.Controls.Add(this.listBox2);
            this.splitContainer1.Size = new System.Drawing.Size(292, 249);
            this.splitContainer1.SplitterDistance = 97;
            this.splitContainer1.TabIndex = 2;
            // 
            // listBox1
            // 
            this.listBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox1.FormattingEnabled = true;
            this.listBox1.IntegralHeight = false;
            this.listBox1.Location = new System.Drawing.Point(0, 0);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(97, 249);
            this.listBox1.TabIndex = 0;
            this.listBox1.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged_1);
            this.listBox1.DoubleClick += new System.EventHandler(this.listBox1_DoubleClick);
            // 
            // Language_Editor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(292, 273);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Language_Editor";
            this.Text = "Language Editor";
            this.Load += new System.EventHandler(this.Language_Editor_Load);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        public System.Windows.Forms.MenuStrip menuStrip1;
        public System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem copyLanguagToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem deleteLanguageToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem editEntryToolStripMenuItem;
        public System.Windows.Forms.ListBox listBox2;
        public System.Windows.Forms.SplitContainer splitContainer1;
        public System.Windows.Forms.ListBox listBox1;
        public System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem renameLanguageToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem setLanguageAsDefaultToolStripMenuItem;


    }
}