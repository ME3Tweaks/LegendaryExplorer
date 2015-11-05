namespace ME3Explorer.LevelExplorer
{
    partial class ME3LevelExplorer
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ME3LevelExplorer));
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.toolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.levelDatabaseToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.levelEditorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.debugToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openDebugWindowToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolsToolStripMenuItem,
            this.debugToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(632, 24);
            this.menuStrip1.TabIndex = 4;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // toolsToolStripMenuItem
            // 
            this.toolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.levelDatabaseToolStripMenuItem,
            this.levelEditorToolStripMenuItem});
            this.toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            this.toolsToolStripMenuItem.Size = new System.Drawing.Size(92, 20);
            this.toolsToolStripMenuItem.Text = "Open Subtool";
            // 
            // levelDatabaseToolStripMenuItem
            // 
            this.levelDatabaseToolStripMenuItem.Image = global::ME3Explorer.Properties.Resources.level_database_64x64;
            this.levelDatabaseToolStripMenuItem.Name = "levelDatabaseToolStripMenuItem";
            this.levelDatabaseToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.levelDatabaseToolStripMenuItem.Text = "Level Database";
            this.levelDatabaseToolStripMenuItem.Click += new System.EventHandler(this.levelDatabaseToolStripMenuItem_Click);
            // 
            // levelEditorToolStripMenuItem
            // 
            this.levelEditorToolStripMenuItem.Image = global::ME3Explorer.Properties.Resources.level_editor_64x64;
            this.levelEditorToolStripMenuItem.Name = "levelEditorToolStripMenuItem";
            this.levelEditorToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.levelEditorToolStripMenuItem.Text = "Level Editor";
            this.levelEditorToolStripMenuItem.Click += new System.EventHandler(this.levelEditorToolStripMenuItem_Click);
            // 
            // debugToolStripMenuItem
            // 
            this.debugToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openDebugWindowToolStripMenuItem});
            this.debugToolStripMenuItem.Name = "debugToolStripMenuItem";
            this.debugToolStripMenuItem.Size = new System.Drawing.Size(54, 20);
            this.debugToolStripMenuItem.Text = "Debug";
            // 
            // openDebugWindowToolStripMenuItem
            // 
            this.openDebugWindowToolStripMenuItem.Name = "openDebugWindowToolStripMenuItem";
            this.openDebugWindowToolStripMenuItem.Size = new System.Drawing.Size(188, 22);
            this.openDebugWindowToolStripMenuItem.Text = "Open Debug Window";
            this.openDebugWindowToolStripMenuItem.Click += new System.EventHandler(this.openDebugWindowToolStripMenuItem_Click);
            // 
            // ME3LevelExplorer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(632, 453);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.IsMdiContainer = true;
            this.Name = "ME3LevelExplorer";
            this.Text = "ME3 Level Explorer";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        #endregion

        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem levelDatabaseToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem levelEditorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem debugToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openDebugWindowToolStripMenuItem;
    }
}



