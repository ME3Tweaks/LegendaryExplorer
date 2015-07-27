namespace ME3Explorer
{
    partial class Conditionals
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        public System.ComponentModel.IContainer components = null;

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
        public void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Conditionals));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mapToSVGToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editExpressionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editIDToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cloneToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.pb1 = new System.Windows.Forms.ToolStripProgressBar();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.rtb1 = new System.Windows.Forms.RichTextBox();
            this.searchToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.forIDToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.searchAgainToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.editToolStripMenuItem,
            this.searchToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(701, 24);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            this.menuStrip1.Visible = false;
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openToolStripMenuItem,
            this.saveToolStripMenuItem,
            this.mapToSVGToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(35, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new System.Drawing.Size(129, 22);
            this.openToolStripMenuItem.Text = "Open";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
            // 
            // saveToolStripMenuItem
            // 
            this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            this.saveToolStripMenuItem.Size = new System.Drawing.Size(129, 22);
            this.saveToolStripMenuItem.Text = "Save";
            this.saveToolStripMenuItem.Click += new System.EventHandler(this.saveToolStripMenuItem_Click);
            // 
            // mapToSVGToolStripMenuItem
            // 
            this.mapToSVGToolStripMenuItem.Name = "mapToSVGToolStripMenuItem";
            this.mapToSVGToolStripMenuItem.Size = new System.Drawing.Size(129, 22);
            this.mapToSVGToolStripMenuItem.Text = "Map to SVG";
            this.mapToSVGToolStripMenuItem.Click += new System.EventHandler(this.mapToSVGToolStripMenuItem_Click);
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.editExpressionToolStripMenuItem,
            this.editIDToolStripMenuItem,
            this.cloneToolStripMenuItem1,
            this.deleteToolStripMenuItem});
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.editToolStripMenuItem.Text = "Edit";
            // 
            // editExpressionToolStripMenuItem
            // 
            this.editExpressionToolStripMenuItem.Name = "editExpressionToolStripMenuItem";
            this.editExpressionToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            this.editExpressionToolStripMenuItem.Text = "Edit Expression";
            this.editExpressionToolStripMenuItem.Click += new System.EventHandler(this.editExpressionToolStripMenuItem_Click);
            // 
            // editIDToolStripMenuItem
            // 
            this.editIDToolStripMenuItem.Name = "editIDToolStripMenuItem";
            this.editIDToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            this.editIDToolStripMenuItem.Text = "Edit ID";
            this.editIDToolStripMenuItem.Click += new System.EventHandler(this.editIDToolStripMenuItem_Click);
            // 
            // cloneToolStripMenuItem1
            // 
            this.cloneToolStripMenuItem1.Name = "cloneToolStripMenuItem1";
            this.cloneToolStripMenuItem1.Size = new System.Drawing.Size(147, 22);
            this.cloneToolStripMenuItem1.Text = "Clone";
            this.cloneToolStripMenuItem1.Click += new System.EventHandler(this.cloneToolStripMenuItem1_Click);
            // 
            // deleteToolStripMenuItem
            // 
            this.deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
            this.deleteToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            this.deleteToolStripMenuItem.Text = "Delete";
            this.deleteToolStripMenuItem.Click += new System.EventHandler(this.deleteToolStripMenuItem_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.pb1});
            this.statusStrip1.Location = new System.Drawing.Point(0, 340);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(701, 22);
            this.statusStrip1.TabIndex = 3;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // pb1
            // 
            this.pb1.Name = "pb1";
            this.pb1.Size = new System.Drawing.Size(100, 16);
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
            this.splitContainer1.Panel2.Controls.Add(this.rtb1);
            this.splitContainer1.Size = new System.Drawing.Size(701, 316);
            this.splitContainer1.SplitterDistance = 233;
            this.splitContainer1.TabIndex = 4;
            // 
            // listBox1
            // 
            this.listBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBox1.FormattingEnabled = true;
            this.listBox1.IntegralHeight = false;
            this.listBox1.ItemHeight = 16;
            this.listBox1.Location = new System.Drawing.Point(0, 0);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(233, 316);
            this.listBox1.TabIndex = 1;
            this.listBox1.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged_1);
            // 
            // rtb1
            // 
            this.rtb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtb1.Location = new System.Drawing.Point(0, 0);
            this.rtb1.Name = "rtb1";
            this.rtb1.Size = new System.Drawing.Size(464, 316);
            this.rtb1.TabIndex = 0;
            this.rtb1.Text = "";
            // 
            // searchToolStripMenuItem
            // 
            this.searchToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.forIDToolStripMenuItem,
            this.searchAgainToolStripMenuItem});
            this.searchToolStripMenuItem.Name = "searchToolStripMenuItem";
            this.searchToolStripMenuItem.Size = new System.Drawing.Size(52, 20);
            this.searchToolStripMenuItem.Text = "Search";
            // 
            // forIDToolStripMenuItem
            // 
            this.forIDToolStripMenuItem.Name = "forIDToolStripMenuItem";
            this.forIDToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.F)));
            this.forIDToolStripMenuItem.Size = new System.Drawing.Size(159, 22);
            this.forIDToolStripMenuItem.Text = "For String";
            this.forIDToolStripMenuItem.Click += new System.EventHandler(this.forIDToolStripMenuItem_Click);
            // 
            // searchAgainToolStripMenuItem
            // 
            this.searchAgainToolStripMenuItem.Name = "searchAgainToolStripMenuItem";
            this.searchAgainToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F3;
            this.searchAgainToolStripMenuItem.Size = new System.Drawing.Size(159, 22);
            this.searchAgainToolStripMenuItem.Text = "Search Again";
            this.searchAgainToolStripMenuItem.Click += new System.EventHandler(this.searchAgainToolStripMenuItem_Click);
            // 
            // Conditionals
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(701, 362);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Conditionals";
            this.Text = "Conditionals";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Conditionals_FormClosing);
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

        public System.Windows.Forms.MenuStrip menuStrip1;
        public System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem mapToSVGToolStripMenuItem;
        public System.Windows.Forms.StatusStrip statusStrip1;
        public System.Windows.Forms.ToolStripProgressBar pb1;
        private System.Windows.Forms.ToolStripMenuItem editExpressionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem cloneToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem deleteToolStripMenuItem;
        public System.Windows.Forms.SplitContainer splitContainer1;
        public System.Windows.Forms.ListBox listBox1;
        public System.Windows.Forms.RichTextBox rtb1;
        private System.Windows.Forms.ToolStripMenuItem editIDToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem searchToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem forIDToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem searchAgainToolStripMenuItem;
    }
}