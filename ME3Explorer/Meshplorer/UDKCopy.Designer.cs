namespace ME3Explorer.Meshplorer
{
    partial class UDKCopy
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
            this.openUDKPackageToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importLODToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importMeshToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.MeshListBox = new System.Windows.Forms.ListBox();
            this.LODListBox = new System.Windows.Forms.ListBox();
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
            this.importLODToolStripMenuItem,
            this.importMeshToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(292, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openUDKPackageToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // openUDKPackageToolStripMenuItem
            // 
            this.openUDKPackageToolStripMenuItem.Name = "openUDKPackageToolStripMenuItem";
            this.openUDKPackageToolStripMenuItem.Size = new System.Drawing.Size(176, 22);
            this.openUDKPackageToolStripMenuItem.Text = "Open UDK Package";
            this.openUDKPackageToolStripMenuItem.Click += new System.EventHandler(this.openUDKPackageToolStripMenuItem_Click);
            // 
            // importLODToolStripMenuItem
            // 
            this.importLODToolStripMenuItem.Name = "importLODToolStripMenuItem";
            this.importLODToolStripMenuItem.Size = new System.Drawing.Size(81, 20);
            this.importLODToolStripMenuItem.Text = "Import LOD";
            this.importLODToolStripMenuItem.Click += new System.EventHandler(this.importLODToolStripMenuItem_Click);
            //
            // importMeshToolStripItem
            //
            this.importMeshToolStripMenuItem.Name = "importMeshToolStripItem";
            this.importMeshToolStripMenuItem.Text = "Import Complete Mesh";
            this.importMeshToolStripMenuItem.Click += new System.EventHandler(this.ImportMeshToolStripMenuItem_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 24);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.MeshListBox);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.LODListBox);
            this.splitContainer1.Size = new System.Drawing.Size(292, 249);
            this.splitContainer1.SplitterDistance = 140;
            this.splitContainer1.TabIndex = 1;
            // 
            // listBox1
            // 
            this.MeshListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.MeshListBox.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MeshListBox.FormattingEnabled = true;
            this.MeshListBox.IntegralHeight = false;
            this.MeshListBox.ItemHeight = 16;
            this.MeshListBox.Location = new System.Drawing.Point(0, 0);
            this.MeshListBox.Name = "listBox1";
            this.MeshListBox.ScrollAlwaysVisible = true;
            this.MeshListBox.Size = new System.Drawing.Size(140, 249);
            this.MeshListBox.TabIndex = 0;
            this.MeshListBox.SelectedIndexChanged += new System.EventHandler(this.MeshListBox_SelectedIndexChanged);
            // 
            // listBox2
            // 
            this.LODListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.LODListBox.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LODListBox.FormattingEnabled = true;
            this.LODListBox.IntegralHeight = false;
            this.LODListBox.ItemHeight = 16;
            this.LODListBox.Location = new System.Drawing.Point(0, 0);
            this.LODListBox.Name = "listBox2";
            this.LODListBox.ScrollAlwaysVisible = true;
            this.LODListBox.Size = new System.Drawing.Size(148, 249);
            this.LODListBox.TabIndex = 0;
            // 
            // UDKCopy
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(292, 273);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "UDKCopy";
            this.Text = "UDKCopy";
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

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openUDKPackageToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importLODToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importMeshToolStripMenuItem;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListBox MeshListBox;
        private System.Windows.Forms.ListBox LODListBox;
    }
}