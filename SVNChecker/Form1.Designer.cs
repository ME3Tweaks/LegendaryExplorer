namespace SVNChecker
{
    partial class Form1
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
            this.checkoutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fullToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.binaryOnlyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.Status = new System.Windows.Forms.ToolStripStatusLabel();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.rtb1 = new System.Windows.Forms.RichTextBox();
            this.rtb2 = new System.Windows.Forms.RichTextBox();
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
            this.checkoutToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(292, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            this.menuStrip1.Visible = false;
            // 
            // checkoutToolStripMenuItem
            // 
            this.checkoutToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fullToolStripMenuItem,
            this.binaryOnlyToolStripMenuItem});
            this.checkoutToolStripMenuItem.Name = "checkoutToolStripMenuItem";
            this.checkoutToolStripMenuItem.Size = new System.Drawing.Size(64, 20);
            this.checkoutToolStripMenuItem.Text = "Checkout";
            // 
            // fullToolStripMenuItem
            // 
            this.fullToolStripMenuItem.Name = "fullToolStripMenuItem";
            this.fullToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.fullToolStripMenuItem.Text = "Full";
            this.fullToolStripMenuItem.Click += new System.EventHandler(this.fullToolStripMenuItem_Click);
            // 
            // binaryOnlyToolStripMenuItem
            // 
            this.binaryOnlyToolStripMenuItem.Name = "binaryOnlyToolStripMenuItem";
            this.binaryOnlyToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.binaryOnlyToolStripMenuItem.Text = "Binary Only";
            this.binaryOnlyToolStripMenuItem.Click += new System.EventHandler(this.binaryOnlyToolStripMenuItem_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.Status});
            this.statusStrip1.Location = new System.Drawing.Point(0, 251);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(292, 22);
            this.statusStrip1.TabIndex = 1;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // Status
            // 
            this.Status.Name = "Status";
            this.Status.Size = new System.Drawing.Size(38, 17);
            this.Status.Text = "Ready";
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 24);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.rtb1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.rtb2);
            this.splitContainer1.Size = new System.Drawing.Size(292, 227);
            this.splitContainer1.SplitterDistance = 142;
            this.splitContainer1.TabIndex = 2;
            // 
            // rtb1
            // 
            this.rtb1.BackColor = System.Drawing.Color.Black;
            this.rtb1.DetectUrls = false;
            this.rtb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb1.Font = new System.Drawing.Font("Courier New", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtb1.ForeColor = System.Drawing.Color.Lime;
            this.rtb1.Location = new System.Drawing.Point(0, 0);
            this.rtb1.Name = "rtb1";
            this.rtb1.Size = new System.Drawing.Size(142, 227);
            this.rtb1.TabIndex = 2;
            this.rtb1.Text = "";
            this.rtb1.WordWrap = false;
            // 
            // rtb2
            // 
            this.rtb2.BackColor = System.Drawing.Color.Black;
            this.rtb2.DetectUrls = false;
            this.rtb2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb2.Font = new System.Drawing.Font("Courier New", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtb2.ForeColor = System.Drawing.Color.Lime;
            this.rtb2.Location = new System.Drawing.Point(0, 0);
            this.rtb2.Name = "rtb2";
            this.rtb2.Size = new System.Drawing.Size(146, 227);
            this.rtb2.TabIndex = 2;
            this.rtb2.Text = "";
            this.rtb2.WordWrap = false;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(292, 273);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Form1";
            this.Text = "SVN Checker by Warranty Voider";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
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
        private System.Windows.Forms.ToolStripMenuItem checkoutToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fullToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem binaryOnlyToolStripMenuItem;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel Status;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.RichTextBox rtb1;
        private System.Windows.Forms.RichTextBox rtb2;
    }
}

