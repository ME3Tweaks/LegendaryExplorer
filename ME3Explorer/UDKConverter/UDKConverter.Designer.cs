namespace ME3Explorer.UDKConverter
{
    partial class UDKConverter
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
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.rtb1 = new System.Windows.Forms.RichTextBox();
            this.convertToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pccToUpkToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.convertToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(292, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // rtb1
            // 
            this.rtb1.DetectUrls = false;
            this.rtb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtb1.Location = new System.Drawing.Point(0, 24);
            this.rtb1.Name = "rtb1";
            this.rtb1.Size = new System.Drawing.Size(292, 249);
            this.rtb1.TabIndex = 1;
            this.rtb1.Text = "THIS IS STILL UNDER DEVELOPMENT!";
            // 
            // convertToolStripMenuItem
            // 
            this.convertToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.pccToUpkToolStripMenuItem});
            this.convertToolStripMenuItem.Name = "convertToolStripMenuItem";
            this.convertToolStripMenuItem.Size = new System.Drawing.Size(58, 20);
            this.convertToolStripMenuItem.Text = "Convert";
            // 
            // pccToUpkToolStripMenuItem
            // 
            this.pccToUpkToolStripMenuItem.Name = "pccToUpkToolStripMenuItem";
            this.pccToUpkToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.pccToUpkToolStripMenuItem.Text = "pcc to upk";
            this.pccToUpkToolStripMenuItem.Click += new System.EventHandler(this.pccToUpkToolStripMenuItem_Click);
            // 
            // UDKConverter
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(292, 273);
            this.Controls.Add(this.rtb1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "UDKConverter";
            this.Text = "UDKConverter";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem convertToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pccToUpkToolStripMenuItem;
        private System.Windows.Forms.RichTextBox rtb1;
    }
}