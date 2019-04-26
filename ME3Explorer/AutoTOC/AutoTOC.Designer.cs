namespace ME3Explorer.T
{
    partial class AutoTOC
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AutoTOC));
            this.rtb1 = new System.Windows.Forms.RichTextBox();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.generateAllTOCsButton = new System.Windows.Forms.ToolStripButton();
            this.createTOCButton = new System.Windows.Forms.ToolStripButton();
            this.RunAutoFileListBtn = new System.Windows.Forms.ToolStripButton();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // rtb1
            // 
            this.rtb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtb1.Location = new System.Drawing.Point(0, 25);
            this.rtb1.Name = "rtb1";
            this.rtb1.Size = new System.Drawing.Size(481, 329);
            this.rtb1.TabIndex = 1;
            this.rtb1.Text = "";
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.generateAllTOCsButton,
            this.createTOCButton,
            this.RunAutoFileListBtn });
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(481, 25);
            this.toolStrip1.TabIndex = 2;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // generateAllTOCsButton
            // 
            this.generateAllTOCsButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.generateAllTOCsButton.Image = ((System.Drawing.Image)(resources.GetObject("generateAllTOCsButton.Image")));
            this.generateAllTOCsButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.generateAllTOCsButton.Name = "generateAllTOCsButton";
            this.generateAllTOCsButton.Size = new System.Drawing.Size(106, 22);
            this.generateAllTOCsButton.Text = "Generate All TOCs";
            this.generateAllTOCsButton.Click += new System.EventHandler(this.generateAllTOCsButton_Click);
            // 
            // createTOCButton
            // 
            this.createTOCButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.createTOCButton.Image = ((System.Drawing.Image)(resources.GetObject("createTOCButton.Image")));
            this.createTOCButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.createTOCButton.Name = "createTOCButton";
            this.createTOCButton.Size = new System.Drawing.Size(71, 22);
            this.createTOCButton.Text = "Create TOC";
            this.createTOCButton.Click += new System.EventHandler(this.createTOCButton_Click);
            // 
            // RunAutoFileListBtn
            // 
            this.RunAutoFileListBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.RunAutoFileListBtn.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton1.Image")));
            this.RunAutoFileListBtn.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.RunAutoFileListBtn.Name = "RunAutoFileListBtn";
            this.RunAutoFileListBtn.Size = new System.Drawing.Size(71, 22);
            this.RunAutoFileListBtn.Text = "Create ME1 FileList";
            this.RunAutoFileListBtn.Click += new System.EventHandler(this.Evt_RunAutoFileBtn_Click);
            // 
            // AutoTOC
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(481, 354);
            this.Controls.Add(this.rtb1);
            this.Controls.Add(this.toolStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "AutoTOC";
            this.Text = "Automatic TOC Generator";
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.RichTextBox rtb1;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton generateAllTOCsButton;
        private System.Windows.Forms.ToolStripButton createTOCButton;
        private System.Windows.Forms.ToolStripButton RunAutoFileListBtn;
    }
}