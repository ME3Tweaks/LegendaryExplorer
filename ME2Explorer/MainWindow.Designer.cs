namespace ME2Explorer
{
    partial class MainWindow
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.toolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.dLCCrackToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.optionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openDebugWindowToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.developerToolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sequenceEditorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pCCEditorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolsToolStripMenuItem,
            this.developerToolsToolStripMenuItem,
            this.optionToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(986, 24);
            this.menuStrip1.TabIndex = 4;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // toolsToolStripMenuItem
            // 
            this.toolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.dLCCrackToolStripMenuItem});
            this.toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            this.toolsToolStripMenuItem.Size = new System.Drawing.Size(73, 20);
            this.toolsToolStripMenuItem.Text = "User Tools";
            // 
            // dLCCrackToolStripMenuItem
            // 
            this.dLCCrackToolStripMenuItem.Image = global::ME2Explorer.Properties.Resources.dlc_crackME2_64x64;
            this.dLCCrackToolStripMenuItem.Name = "dLCCrackToolStripMenuItem";
            this.dLCCrackToolStripMenuItem.Size = new System.Drawing.Size(129, 22);
            this.dLCCrackToolStripMenuItem.Text = "DLC Crack";
            this.dLCCrackToolStripMenuItem.Click += new System.EventHandler(this.dLCCrackToolStripMenuItem_Click);
            // 
            // optionToolStripMenuItem
            // 
            this.optionToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openDebugWindowToolStripMenuItem});
            this.optionToolStripMenuItem.Name = "optionToolStripMenuItem";
            this.optionToolStripMenuItem.Size = new System.Drawing.Size(61, 20);
            this.optionToolStripMenuItem.Text = "Options";
            // 
            // openDebugWindowToolStripMenuItem
            // 
            this.openDebugWindowToolStripMenuItem.Name = "openDebugWindowToolStripMenuItem";
            this.openDebugWindowToolStripMenuItem.Size = new System.Drawing.Size(188, 22);
            this.openDebugWindowToolStripMenuItem.Text = "Open Debug Window";
            this.openDebugWindowToolStripMenuItem.Click += new System.EventHandler(this.openDebugWindowToolStripMenuItem_Click);
            // 
            // developerToolsToolStripMenuItem
            // 
            this.developerToolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.sequenceEditorToolStripMenuItem,
            this.pCCEditorToolStripMenuItem});
            this.developerToolsToolStripMenuItem.Name = "developerToolsToolStripMenuItem";
            this.developerToolsToolStripMenuItem.Size = new System.Drawing.Size(103, 20);
            this.developerToolsToolStripMenuItem.Text = "Developer Tools";
            // 
            // sequenceEditorToolStripMenuItem
            // 
            this.sequenceEditorToolStripMenuItem.Image = global::ME2Explorer.Properties.Resources.sequence_editor_64x64;
            this.sequenceEditorToolStripMenuItem.Name = "sequenceEditorToolStripMenuItem";
            this.sequenceEditorToolStripMenuItem.Size = new System.Drawing.Size(159, 22);
            this.sequenceEditorToolStripMenuItem.Text = "Sequence Editor";
            this.sequenceEditorToolStripMenuItem.Click += new System.EventHandler(this.sequenceEditorToolStripMenuItem_Click);
            // 
            // pCCEditorToolStripMenuItem
            // 
            this.pCCEditorToolStripMenuItem.Image = global::ME2Explorer.Properties.Resources.package_editor_64x64;
            this.pCCEditorToolStripMenuItem.Name = "pCCEditorToolStripMenuItem";
            this.pCCEditorToolStripMenuItem.Size = new System.Drawing.Size(159, 22);
            this.pCCEditorToolStripMenuItem.Text = "Package Editor";
            this.pCCEditorToolStripMenuItem.Click += new System.EventHandler(this.pCCEditorToolStripMenuItem_Click);
            // 
            // MainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(986, 543);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.IsMdiContainer = true;
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "MainWindow";
            this.Text = "ME2 Explorer by Warranty Voider";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        #endregion

        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem dLCCrackToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem optionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openDebugWindowToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem developerToolsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sequenceEditorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pCCEditorToolStripMenuItem;
    }
}



