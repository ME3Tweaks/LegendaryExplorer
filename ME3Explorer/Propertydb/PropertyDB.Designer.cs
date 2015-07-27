namespace ME3Explorer.Propertydb
{
    partial class PropertyDB
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.startScanToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveDBToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadDBToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statistiksToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.pb1 = new System.Windows.Forms.ToolStripProgressBar();
            this.pb2 = new System.Windows.Forms.ToolStripProgressBar();
            this.status = new System.Windows.Forms.ToolStripStatusLabel();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.listBox2 = new System.Windows.Forms.ListBox();
            this.generateTemplatesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.warrantyVoiderMethodToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.amaroksMethodToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
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
            this.generateTemplatesToolStripMenuItem,
            this.statistiksToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(444, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.startScanToolStripMenuItem,
            this.saveDBToolStripMenuItem,
            this.loadDBToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(35, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // startScanToolStripMenuItem
            // 
            this.startScanToolStripMenuItem.Name = "startScanToolStripMenuItem";
            this.startScanToolStripMenuItem.Size = new System.Drawing.Size(135, 22);
            this.startScanToolStripMenuItem.Text = "Start Scan";
            this.startScanToolStripMenuItem.Click += new System.EventHandler(this.startScanToolStripMenuItem_Click);
            // 
            // saveDBToolStripMenuItem
            // 
            this.saveDBToolStripMenuItem.Name = "saveDBToolStripMenuItem";
            this.saveDBToolStripMenuItem.Size = new System.Drawing.Size(135, 22);
            this.saveDBToolStripMenuItem.Text = "Save DB";
            this.saveDBToolStripMenuItem.Click += new System.EventHandler(this.saveDBToolStripMenuItem_Click);
            // 
            // loadDBToolStripMenuItem
            // 
            this.loadDBToolStripMenuItem.Name = "loadDBToolStripMenuItem";
            this.loadDBToolStripMenuItem.Size = new System.Drawing.Size(135, 22);
            this.loadDBToolStripMenuItem.Text = "Load DB";
            this.loadDBToolStripMenuItem.Click += new System.EventHandler(this.loadDBToolStripMenuItem_Click);
            // 
            // statistiksToolStripMenuItem
            // 
            this.statistiksToolStripMenuItem.Name = "statistiksToolStripMenuItem";
            this.statistiksToolStripMenuItem.Size = new System.Drawing.Size(62, 20);
            this.statistiksToolStripMenuItem.Text = "Statistiks";
            this.statistiksToolStripMenuItem.Click += new System.EventHandler(this.statistiksToolStripMenuItem_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.pb1,
            this.pb2,
            this.status});
            this.statusStrip1.Location = new System.Drawing.Point(0, 244);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(444, 22);
            this.statusStrip1.TabIndex = 1;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // pb1
            // 
            this.pb1.Name = "pb1";
            this.pb1.Size = new System.Drawing.Size(100, 16);
            // 
            // pb2
            // 
            this.pb2.Name = "pb2";
            this.pb2.Size = new System.Drawing.Size(100, 16);
            // 
            // status
            // 
            this.status.Name = "status";
            this.status.Size = new System.Drawing.Size(59, 17);
            this.status.Text = "Classes : 0";
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
            this.splitContainer1.Size = new System.Drawing.Size(444, 220);
            this.splitContainer1.SplitterDistance = 147;
            this.splitContainer1.TabIndex = 2;
            // 
            // listBox1
            // 
            this.listBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBox1.FormattingEnabled = true;
            this.listBox1.ItemHeight = 16;
            this.listBox1.Location = new System.Drawing.Point(0, 0);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(147, 220);
            this.listBox1.TabIndex = 0;
            this.listBox1.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged);
            // 
            // listBox2
            // 
            this.listBox2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox2.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBox2.FormattingEnabled = true;
            this.listBox2.ItemHeight = 16;
            this.listBox2.Location = new System.Drawing.Point(0, 0);
            this.listBox2.Name = "listBox2";
            this.listBox2.Size = new System.Drawing.Size(293, 220);
            this.listBox2.TabIndex = 0;
            // 
            // generateTemplatesToolStripMenuItem
            // 
            this.generateTemplatesToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.warrantyVoiderMethodToolStripMenuItem,
            this.amaroksMethodToolStripMenuItem});
            this.generateTemplatesToolStripMenuItem.Name = "generateTemplatesToolStripMenuItem";
            this.generateTemplatesToolStripMenuItem.Size = new System.Drawing.Size(116, 20);
            this.generateTemplatesToolStripMenuItem.Text = "Generate Templates";
            // 
            // warrantyVoiderMethodToolStripMenuItem
            // 
            this.warrantyVoiderMethodToolStripMenuItem.Name = "warrantyVoiderMethodToolStripMenuItem";
            this.warrantyVoiderMethodToolStripMenuItem.Size = new System.Drawing.Size(208, 22);
            this.warrantyVoiderMethodToolStripMenuItem.Text = "Warranty Voiders Method";
            this.warrantyVoiderMethodToolStripMenuItem.Click += new System.EventHandler(this.warrantyVoiderMethodToolStripMenuItem_Click);
            // 
            // amaroksMethodToolStripMenuItem
            // 
            this.amaroksMethodToolStripMenuItem.Name = "amaroksMethodToolStripMenuItem";
            this.amaroksMethodToolStripMenuItem.Size = new System.Drawing.Size(208, 22);
            this.amaroksMethodToolStripMenuItem.Text = "Amaroks Method";
            // 
            // PropertyDB
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(444, 266);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "PropertyDB";
            this.Text = "PropertyDB";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem startScanToolStripMenuItem;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripProgressBar pb1;
        private System.Windows.Forms.ToolStripProgressBar pb2;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.ListBox listBox2;
        private System.Windows.Forms.ToolStripMenuItem saveDBToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loadDBToolStripMenuItem;
        private System.Windows.Forms.ToolStripStatusLabel status;
        private System.Windows.Forms.ToolStripMenuItem statistiksToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem generateTemplatesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem warrantyVoiderMethodToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem amaroksMethodToolStripMenuItem;
    }
}