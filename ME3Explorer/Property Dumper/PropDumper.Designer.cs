namespace ME3Explorer.Property_Dumper
{
    partial class PropDumper
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PropDumper));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.makePropDumpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.makePropDumpForClassToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.makeDialogDumpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveDumpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pauseToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.pb1 = new System.Windows.Forms.ToolStripProgressBar();
            this.pb2 = new System.Windows.Forms.ToolStripProgressBar();
            this.Status = new System.Windows.Forms.ToolStripStatusLabel();
            this.rtb1 = new System.Windows.Forms.RichTextBox();
            this.menuStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.pauseToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(292, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            this.menuStrip1.Visible = false;
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.makePropDumpToolStripMenuItem,
            this.makePropDumpForClassToolStripMenuItem,
            this.makeDialogDumpToolStripMenuItem,
            this.saveDumpToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // makePropDumpToolStripMenuItem
            // 
            this.makePropDumpToolStripMenuItem.Name = "makePropDumpToolStripMenuItem";
            this.makePropDumpToolStripMenuItem.Size = new System.Drawing.Size(228, 22);
            this.makePropDumpToolStripMenuItem.Text = "Make Prop Dump";
            this.makePropDumpToolStripMenuItem.Click += new System.EventHandler(this.makePropDumpToolStripMenuItem_Click);
            // 
            // makePropDumpForClassToolStripMenuItem
            // 
            this.makePropDumpForClassToolStripMenuItem.Name = "makePropDumpForClassToolStripMenuItem";
            this.makePropDumpForClassToolStripMenuItem.Size = new System.Drawing.Size(228, 22);
            this.makePropDumpForClassToolStripMenuItem.Text = "Make Prop Dump for class";
            this.makePropDumpForClassToolStripMenuItem.Click += new System.EventHandler(this.makePropDumpForClassToolStripMenuItem_Click);
            // 
            // makeDialogDumpToolStripMenuItem
            // 
            this.makeDialogDumpToolStripMenuItem.Name = "makeDialogDumpToolStripMenuItem";
            this.makeDialogDumpToolStripMenuItem.Size = new System.Drawing.Size(228, 22);
            this.makeDialogDumpToolStripMenuItem.Text = "Make String Reference Dump";
            this.makeDialogDumpToolStripMenuItem.Click += new System.EventHandler(this.makeDialogDumpToolStripMenuItem_Click);
            // 
            // saveDumpToolStripMenuItem
            // 
            this.saveDumpToolStripMenuItem.Name = "saveDumpToolStripMenuItem";
            this.saveDumpToolStripMenuItem.Size = new System.Drawing.Size(228, 22);
            this.saveDumpToolStripMenuItem.Text = "Save dump";
            this.saveDumpToolStripMenuItem.Click += new System.EventHandler(this.saveDumpToolStripMenuItem_Click);
            // 
            // pauseToolStripMenuItem
            // 
            this.pauseToolStripMenuItem.Name = "pauseToolStripMenuItem";
            this.pauseToolStripMenuItem.Size = new System.Drawing.Size(50, 20);
            this.pauseToolStripMenuItem.Text = "Pause";
            this.pauseToolStripMenuItem.Visible = false;
            this.pauseToolStripMenuItem.Click += new System.EventHandler(this.pauseToolStripMenuItem_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.pb1,
            this.pb2});
            this.statusStrip1.Location = new System.Drawing.Point(0, 251);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(292, 22);
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
            // Status
            // 
            this.Status.Name = "Status";
            this.Status.Size = new System.Drawing.Size(61, 17);
            this.Status.Text = "State : Idle";
            // 
            // rtb1
            // 
            this.rtb1.DetectUrls = false;
            this.rtb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtb1.Location = new System.Drawing.Point(0, 24);
            this.rtb1.Name = "rtb1";
            this.rtb1.Size = new System.Drawing.Size(292, 227);
            this.rtb1.TabIndex = 2;
            this.rtb1.Text = "";
            this.rtb1.WordWrap = false;
            // 
            // PropDumper
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(292, 273);
            this.Controls.Add(this.rtb1);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "PropDumper";
            this.Text = "PropDumper";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.PropDumper_FormClosing);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem makePropDumpToolStripMenuItem;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.RichTextBox rtb1;
        private System.Windows.Forms.ToolStripStatusLabel Status;
        private System.Windows.Forms.ToolStripMenuItem makeDialogDumpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveDumpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pauseToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem makePropDumpForClassToolStripMenuItem;
        private System.Windows.Forms.ToolStripProgressBar pb1;
        private System.Windows.Forms.ToolStripProgressBar pb2;

    }
}