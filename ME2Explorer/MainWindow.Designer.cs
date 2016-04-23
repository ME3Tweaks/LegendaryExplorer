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
            this.developerToolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pCCEditorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sequenceEditorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.optionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openDebugWindowToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.dialogueEditorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.developerToolsToolStripMenuItem,
            this.optionToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(986, 24);
            this.menuStrip1.TabIndex = 4;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // developerToolsToolStripMenuItem
            // 
            this.developerToolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.pCCEditorToolStripMenuItem,
            this.sequenceEditorToolStripMenuItem,
            this.dialogueEditorToolStripMenuItem});
            this.developerToolsToolStripMenuItem.Name = "developerToolsToolStripMenuItem";
            this.developerToolsToolStripMenuItem.Size = new System.Drawing.Size(104, 20);
            this.developerToolsToolStripMenuItem.Text = "Developer Tools";
            // 
            // pCCEditorToolStripMenuItem
            // 
            this.pCCEditorToolStripMenuItem.Image = global::ME2Explorer.Properties.Resources.package_editor_64x64;
            this.pCCEditorToolStripMenuItem.Name = "pCCEditorToolStripMenuItem";
            this.pCCEditorToolStripMenuItem.Size = new System.Drawing.Size(159, 22);
            this.pCCEditorToolStripMenuItem.Text = "PCC Editor";
            this.pCCEditorToolStripMenuItem.Click += new System.EventHandler(this.pCCEditorToolStripMenuItem_Click);
            // 
            // sequenceEditorToolStripMenuItem
            // 
            this.sequenceEditorToolStripMenuItem.Image = global::ME2Explorer.Properties.Resources.sequence_editor_64x64;
            this.sequenceEditorToolStripMenuItem.Name = "sequenceEditorToolStripMenuItem";
            this.sequenceEditorToolStripMenuItem.Size = new System.Drawing.Size(159, 22);
            this.sequenceEditorToolStripMenuItem.Text = "Sequence Editor";
            this.sequenceEditorToolStripMenuItem.Click += new System.EventHandler(this.sequenceEditorToolStripMenuItem_Click);
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
            // toolStrip1
            // 
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(60, 50);
            this.toolStrip1.Location = new System.Drawing.Point(0, 518);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(986, 25);
            this.toolStrip1.TabIndex = 9;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Interval = 1000;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // dialogueEditorToolStripMenuItem
            // 
            this.dialogueEditorToolStripMenuItem.Image = global::ME2Explorer.Properties.Resources.dialogue_editor_64x64;
            this.dialogueEditorToolStripMenuItem.Name = "dialogueEditorToolStripMenuItem";
            this.dialogueEditorToolStripMenuItem.Size = new System.Drawing.Size(159, 22);
            this.dialogueEditorToolStripMenuItem.Text = "Dialogue Editor";
            this.dialogueEditorToolStripMenuItem.Click += new System.EventHandler(this.dialogueEditorToolStripMenuItem_Click);
            // 
            // MainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(986, 543);
            this.Controls.Add(this.toolStrip1);
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
        private System.Windows.Forms.ToolStripMenuItem optionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openDebugWindowToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem developerToolsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sequenceEditorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pCCEditorToolStripMenuItem;
        public System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.ToolStripMenuItem dialogueEditorToolStripMenuItem;
    }
}



