namespace ME1Explorer
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
            this.saveGameEditorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveGameOperatorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.developerToolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pCCEditorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.dialogEditorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sequenceViewerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tLKEditorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.optionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openDebugWindowToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
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
            this.menuStrip1.Size = new System.Drawing.Size(883, 24);
            this.menuStrip1.TabIndex = 4;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // toolsToolStripMenuItem
            // 
            this.toolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.saveGameEditorToolStripMenuItem,
            this.saveGameOperatorToolStripMenuItem});
            this.toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            this.toolsToolStripMenuItem.Size = new System.Drawing.Size(74, 20);
            this.toolsToolStripMenuItem.Text = "User Tools";
            // 
            // saveGameEditorToolStripMenuItem
            // 
            this.saveGameEditorToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("saveGameEditorToolStripMenuItem.Image")));
            this.saveGameEditorToolStripMenuItem.Name = "saveGameEditorToolStripMenuItem";
            this.saveGameEditorToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.saveGameEditorToolStripMenuItem.Text = "Save Game Editor";
            this.saveGameEditorToolStripMenuItem.Click += new System.EventHandler(this.saveGameEditorToolStripMenuItem_Click);
            // 
            // saveGameOperatorToolStripMenuItem
            // 
            this.saveGameOperatorToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("saveGameOperatorToolStripMenuItem.Image")));
            this.saveGameOperatorToolStripMenuItem.Name = "saveGameOperatorToolStripMenuItem";
            this.saveGameOperatorToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.saveGameOperatorToolStripMenuItem.Text = "Save Game Operator";
            this.saveGameOperatorToolStripMenuItem.Click += new System.EventHandler(this.saveGameOperatorToolStripMenuItem_Click);
            // 
            // developerToolsToolStripMenuItem
            // 
            this.developerToolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.pCCEditorToolStripMenuItem,
            this.dialogEditorToolStripMenuItem,
            this.sequenceViewerToolStripMenuItem,
            this.tLKEditorToolStripMenuItem});
            this.developerToolsToolStripMenuItem.Name = "developerToolsToolStripMenuItem";
            this.developerToolsToolStripMenuItem.Size = new System.Drawing.Size(104, 20);
            this.developerToolsToolStripMenuItem.Text = "Developer Tools";
            // 
            // pCCEditorToolStripMenuItem
            // 
            this.pCCEditorToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("pCCEditorToolStripMenuItem.Image")));
            this.pCCEditorToolStripMenuItem.Name = "pCCEditorToolStripMenuItem";
            this.pCCEditorToolStripMenuItem.Size = new System.Drawing.Size(159, 22);
            this.pCCEditorToolStripMenuItem.Text = "Package Editor";
            this.pCCEditorToolStripMenuItem.Click += new System.EventHandler(this.pccEditorToolStripMenuItem_Click);
            // 
            // dialogEditorToolStripMenuItem
            // 
            this.dialogEditorToolStripMenuItem.Image = global::ME1Explorer.Properties.Resources.dialogue_editor_64x64;
            this.dialogEditorToolStripMenuItem.Name = "dialogEditorToolStripMenuItem";
            this.dialogEditorToolStripMenuItem.Size = new System.Drawing.Size(159, 22);
            this.dialogEditorToolStripMenuItem.Text = "Dialog Editor";
            this.dialogEditorToolStripMenuItem.Click += new System.EventHandler(this.dialogEditorToolStripMenuItem_Click);
            // 
            // sequenceViewerToolStripMenuItem
            // 
            this.sequenceViewerToolStripMenuItem.Image = global::ME1Explorer.Properties.Resources.sequence_editor_64x64;
            this.sequenceViewerToolStripMenuItem.Name = "sequenceViewerToolStripMenuItem";
            this.sequenceViewerToolStripMenuItem.Size = new System.Drawing.Size(159, 22);
            this.sequenceViewerToolStripMenuItem.Text = "Sequence Editor";
            this.sequenceViewerToolStripMenuItem.Click += new System.EventHandler(this.sequenceEditorToolStripMenuItem_Click);
            // 
            // tLKEditorToolStripMenuItem
            // 
            this.tLKEditorToolStripMenuItem.Image = global::ME1Explorer.Properties.Resources.TLK_editor_64x64;
            this.tLKEditorToolStripMenuItem.Name = "tLKEditorToolStripMenuItem";
            this.tLKEditorToolStripMenuItem.Size = new System.Drawing.Size(159, 22);
            this.tLKEditorToolStripMenuItem.Text = "TLK Editor";
            this.tLKEditorToolStripMenuItem.Click += new System.EventHandler(this.tLKEditorToolStripMenuItem_Click);
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
            this.toolStrip1.Location = new System.Drawing.Point(0, 428);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(883, 25);
            this.toolStrip1.TabIndex = 8;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Interval = 1000;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // MainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(883, 453);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.IsMdiContainer = true;
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "MainWindow";
            this.Text = "ME1 Explorer by Warranty Voider";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form_Closing);
            this.Load += new System.EventHandler(this.MainWindow_Load);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        #endregion

        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveGameEditorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem optionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openDebugWindowToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveGameOperatorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem developerToolsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pCCEditorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem dialogEditorToolStripMenuItem;
        public System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.ToolStripMenuItem sequenceViewerToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem tLKEditorToolStripMenuItem;
    }
}



