namespace ME2Explorer
{
    partial class TlkManager
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TlkManager));
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.addTlkButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.removeTlkButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.tlkDownButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.tlkUpButton = new System.Windows.Forms.ToolStripButton();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // listBox1
            // 
            this.listBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox1.FormattingEnabled = true;
            this.listBox1.Location = new System.Drawing.Point(0, 25);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(688, 182);
            this.listBox1.TabIndex = 0;
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addTlkButton,
            this.toolStripSeparator1,
            this.removeTlkButton,
            this.toolStripSeparator2,
            this.tlkDownButton,
            this.toolStripSeparator3,
            this.tlkUpButton});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(688, 25);
            this.toolStrip1.TabIndex = 1;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // addTlkButton
            // 
            this.addTlkButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.addTlkButton.Image = ((System.Drawing.Image)(resources.GetObject("addTlkButton.Image")));
            this.addTlkButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.addTlkButton.Name = "addTlkButton";
            this.addTlkButton.Size = new System.Drawing.Size(49, 22);
            this.addTlkButton.Text = "Add tlk";
            this.addTlkButton.Click += new System.EventHandler(this.addTlkButton_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 25);
            // 
            // removeTlkButton
            // 
            this.removeTlkButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.removeTlkButton.Image = ((System.Drawing.Image)(resources.GetObject("removeTlkButton.Image")));
            this.removeTlkButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.removeTlkButton.Name = "removeTlkButton";
            this.removeTlkButton.Size = new System.Drawing.Size(70, 22);
            this.removeTlkButton.Text = "Remove tlk";
            this.removeTlkButton.Click += new System.EventHandler(this.removeTlkButton_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 25);
            // 
            // tlkDownButton
            // 
            this.tlkDownButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.tlkDownButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tlkDownButton.Image = ((System.Drawing.Image)(resources.GetObject("tlkDownButton.Image")));
            this.tlkDownButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tlkDownButton.Name = "tlkDownButton";
            this.tlkDownButton.Size = new System.Drawing.Size(42, 22);
            this.tlkDownButton.Text = "Down";
            this.tlkDownButton.Click += new System.EventHandler(this.tlkDownButton_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(6, 25);
            // 
            // tlkUpButton
            // 
            this.tlkUpButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.tlkUpButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tlkUpButton.Image = ((System.Drawing.Image)(resources.GetObject("tlkUpButton.Image")));
            this.tlkUpButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tlkUpButton.Name = "tlkUpButton";
            this.tlkUpButton.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.tlkUpButton.Size = new System.Drawing.Size(26, 22);
            this.tlkUpButton.Text = "Up";
            this.tlkUpButton.Click += new System.EventHandler(this.tlkUpButton_Click);
            // 
            // TlkManager
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(688, 207);
            this.Controls.Add(this.listBox1);
            this.Controls.Add(this.toolStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "TlkManager";
            this.Text = "TlkManager";
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton addTlkButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton removeTlkButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripButton tlkDownButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripButton tlkUpButton;
    }
}