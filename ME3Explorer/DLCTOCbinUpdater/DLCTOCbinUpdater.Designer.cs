namespace ME3Explorer.DLCTOCbinUpdater
{
    partial class DLCTOCbinUpdater
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
            this.checkSFARToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.checkAndRebuildSFARToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(595, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            this.menuStrip1.Visible = false;
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.checkSFARToolStripMenuItem,
            this.checkAndRebuildSFARToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(35, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // checkSFARToolStripMenuItem
            // 
            this.checkSFARToolStripMenuItem.Name = "checkSFARToolStripMenuItem";
            this.checkSFARToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.checkSFARToolStripMenuItem.Text = "Check SFAR";
            this.checkSFARToolStripMenuItem.Click += new System.EventHandler(this.checkSFARToolStripMenuItem_Click);
            // 
            // checkAndRebuildSFARToolStripMenuItem
            // 
            this.checkAndRebuildSFARToolStripMenuItem.Name = "checkAndRebuildSFARToolStripMenuItem";
            this.checkAndRebuildSFARToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.checkAndRebuildSFARToolStripMenuItem.Text = "Check and Rebuild SFAR";
            this.checkAndRebuildSFARToolStripMenuItem.Click += new System.EventHandler(this.checkAndRebuildSFARToolStripMenuItem_Click);
            // 
            // DLCTOCbinUpdater
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackgroundImage = global::ME3Explorer.Properties.Resources.back2;
            this.ClientSize = new System.Drawing.Size(595, 341);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "DLCTOCbinUpdater";
            this.Text = "DLCTOCbinUpdater";
            this.Activated += new System.EventHandler(this.DLCTOCbinUpdater_Activated);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem checkSFARToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem checkAndRebuildSFARToolStripMenuItem;
    }
}