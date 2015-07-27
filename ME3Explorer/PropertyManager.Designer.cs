namespace ME3Explorer
{
    partial class PropertyManager
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PropertyManager));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.newToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadDefinitonsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveDefinitionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadDefaultToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveAsDefaultToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importFromFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.actionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addClassToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteClassToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deletePropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyClassToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.listBox2 = new System.Windows.Forms.ListBox();
            this.rtb1 = new System.Windows.Forms.RichTextBox();
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
            this.toolStripMenuItem1,
            this.fileToolStripMenuItem,
            this.actionToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(292, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(12, 20);
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newToolStripMenuItem,
            this.loadDefinitonsToolStripMenuItem,
            this.saveDefinitionsToolStripMenuItem,
            this.loadDefaultToolStripMenuItem,
            this.saveAsDefaultToolStripMenuItem,
            this.importFromFileToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // newToolStripMenuItem
            // 
            this.newToolStripMenuItem.Name = "newToolStripMenuItem";
            this.newToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.newToolStripMenuItem.Text = "New";
            this.newToolStripMenuItem.Click += new System.EventHandler(this.newToolStripMenuItem_Click);
            // 
            // loadDefinitonsToolStripMenuItem
            // 
            this.loadDefinitonsToolStripMenuItem.Name = "loadDefinitonsToolStripMenuItem";
            this.loadDefinitonsToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.loadDefinitonsToolStripMenuItem.Text = "Open";
            this.loadDefinitonsToolStripMenuItem.Click += new System.EventHandler(this.loadDefinitonsToolStripMenuItem_Click);
            // 
            // saveDefinitionsToolStripMenuItem
            // 
            this.saveDefinitionsToolStripMenuItem.Name = "saveDefinitionsToolStripMenuItem";
            this.saveDefinitionsToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.saveDefinitionsToolStripMenuItem.Text = "Save as";
            this.saveDefinitionsToolStripMenuItem.Click += new System.EventHandler(this.saveDefinitionsToolStripMenuItem_Click);
            // 
            // loadDefaultToolStripMenuItem
            // 
            this.loadDefaultToolStripMenuItem.Name = "loadDefaultToolStripMenuItem";
            this.loadDefaultToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.loadDefaultToolStripMenuItem.Text = "Load Default";
            this.loadDefaultToolStripMenuItem.Click += new System.EventHandler(this.loadDefaultToolStripMenuItem_Click);
            // 
            // saveAsDefaultToolStripMenuItem
            // 
            this.saveAsDefaultToolStripMenuItem.Name = "saveAsDefaultToolStripMenuItem";
            this.saveAsDefaultToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.saveAsDefaultToolStripMenuItem.Text = "Save as Default";
            this.saveAsDefaultToolStripMenuItem.Click += new System.EventHandler(this.saveAsDefaultToolStripMenuItem_Click);
            // 
            // importFromFileToolStripMenuItem
            // 
            this.importFromFileToolStripMenuItem.Name = "importFromFileToolStripMenuItem";
            this.importFromFileToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.importFromFileToolStripMenuItem.Text = "Import from File";
            this.importFromFileToolStripMenuItem.Click += new System.EventHandler(this.importFromFileToolStripMenuItem_Click);
            // 
            // actionToolStripMenuItem
            // 
            this.actionToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addClassToolStripMenuItem,
            this.addPropertyToolStripMenuItem,
            this.deleteClassToolStripMenuItem,
            this.deletePropertyToolStripMenuItem,
            this.copyClassToolStripMenuItem});
            this.actionToolStripMenuItem.Name = "actionToolStripMenuItem";
            this.actionToolStripMenuItem.Size = new System.Drawing.Size(54, 20);
            this.actionToolStripMenuItem.Text = "Action";
            // 
            // addClassToolStripMenuItem
            // 
            this.addClassToolStripMenuItem.Name = "addClassToolStripMenuItem";
            this.addClassToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
            this.addClassToolStripMenuItem.Text = "Add Class";
            this.addClassToolStripMenuItem.Click += new System.EventHandler(this.addClassToolStripMenuItem_Click);
            // 
            // addPropertyToolStripMenuItem
            // 
            this.addPropertyToolStripMenuItem.Name = "addPropertyToolStripMenuItem";
            this.addPropertyToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
            this.addPropertyToolStripMenuItem.Text = "Add Property";
            this.addPropertyToolStripMenuItem.Click += new System.EventHandler(this.addPropertyToolStripMenuItem_Click);
            // 
            // deleteClassToolStripMenuItem
            // 
            this.deleteClassToolStripMenuItem.Name = "deleteClassToolStripMenuItem";
            this.deleteClassToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
            this.deleteClassToolStripMenuItem.Text = "Delete Class";
            this.deleteClassToolStripMenuItem.Click += new System.EventHandler(this.deleteClassToolStripMenuItem_Click);
            // 
            // deletePropertyToolStripMenuItem
            // 
            this.deletePropertyToolStripMenuItem.Name = "deletePropertyToolStripMenuItem";
            this.deletePropertyToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
            this.deletePropertyToolStripMenuItem.Text = "Delete Property";
            this.deletePropertyToolStripMenuItem.Click += new System.EventHandler(this.deletePropertyToolStripMenuItem_Click);
            // 
            // copyClassToolStripMenuItem
            // 
            this.copyClassToolStripMenuItem.Name = "copyClassToolStripMenuItem";
            this.copyClassToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
            this.copyClassToolStripMenuItem.Text = "Copy Class";
            this.copyClassToolStripMenuItem.Click += new System.EventHandler(this.copyClassToolStripMenuItem_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 24);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.splitContainer2);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.rtb1);
            this.splitContainer1.Size = new System.Drawing.Size(292, 249);
            this.splitContainer1.SplitterDistance = 172;
            this.splitContainer1.TabIndex = 1;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.listBox1);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.listBox2);
            this.splitContainer2.Size = new System.Drawing.Size(292, 172);
            this.splitContainer2.SplitterDistance = 130;
            this.splitContainer2.TabIndex = 0;
            // 
            // listBox1
            // 
            this.listBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox1.FormattingEnabled = true;
            this.listBox1.IntegralHeight = false;
            this.listBox1.Location = new System.Drawing.Point(0, 0);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(130, 172);
            this.listBox1.TabIndex = 0;
            this.listBox1.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged);
            // 
            // listBox2
            // 
            this.listBox2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox2.FormattingEnabled = true;
            this.listBox2.IntegralHeight = false;
            this.listBox2.Location = new System.Drawing.Point(0, 0);
            this.listBox2.Name = "listBox2";
            this.listBox2.Size = new System.Drawing.Size(158, 172);
            this.listBox2.TabIndex = 0;
            this.listBox2.SelectedIndexChanged += new System.EventHandler(this.listBox2_SelectedIndexChanged);
            // 
            // rtb1
            // 
            this.rtb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb1.Location = new System.Drawing.Point(0, 0);
            this.rtb1.Name = "rtb1";
            this.rtb1.Size = new System.Drawing.Size(292, 73);
            this.rtb1.TabIndex = 0;
            this.rtb1.Text = "";
            // 
            // PropertyManager
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(292, 273);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "PropertyManager";
            this.Text = "Property Manager";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.PropertyManager_FormClosing);
            this.Load += new System.EventHandler(this.PropertyManager_Load);
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

        public System.Windows.Forms.MenuStrip menuStrip1;
        public System.Windows.Forms.ToolStripMenuItem toolStripMenuItem1;
        public System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem loadDefinitonsToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem saveDefinitionsToolStripMenuItem;
        public System.Windows.Forms.SplitContainer splitContainer1;
        public System.Windows.Forms.SplitContainer splitContainer2;
        public System.Windows.Forms.ListBox listBox1;
        public System.Windows.Forms.ListBox listBox2;
        public System.Windows.Forms.RichTextBox rtb1;
        public System.Windows.Forms.ToolStripMenuItem actionToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem addClassToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem addPropertyToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem deleteClassToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem deletePropertyToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem loadDefaultToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem saveAsDefaultToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem newToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem importFromFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyClassToolStripMenuItem;
    }
}