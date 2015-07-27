namespace ME3Explorer
{
    partial class XBoxConverter
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        public System.ComponentModel.IContainer components = null;

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
        public void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(XBoxConverter));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.xXXPCCToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pCCXXXToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rtb1 = new System.Windows.Forms.RichTextBox();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(292, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.xXXPCCToolStripMenuItem,
            this.pCCXXXToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // xXXPCCToolStripMenuItem
            // 
            this.xXXPCCToolStripMenuItem.Name = "xXXPCCToolStripMenuItem";
            this.xXXPCCToolStripMenuItem.Size = new System.Drawing.Size(137, 22);
            this.xXXPCCToolStripMenuItem.Text = "XXX -> PCC";
            this.xXXPCCToolStripMenuItem.Click += new System.EventHandler(this.xXXPCCToolStripMenuItem_Click);
            // 
            // pCCXXXToolStripMenuItem
            // 
            this.pCCXXXToolStripMenuItem.Name = "pCCXXXToolStripMenuItem";
            this.pCCXXXToolStripMenuItem.Size = new System.Drawing.Size(137, 22);
            this.pCCXXXToolStripMenuItem.Text = "PCC -> XXX";
            this.pCCXXXToolStripMenuItem.Click += new System.EventHandler(this.pCCXXXToolStripMenuItem_Click);
            // 
            // rtb1
            // 
            this.rtb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb1.Font = new System.Drawing.Font("Courier New", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtb1.Location = new System.Drawing.Point(0, 24);
            this.rtb1.Name = "rtb1";
            this.rtb1.Size = new System.Drawing.Size(292, 249);
            this.rtb1.TabIndex = 1;
            this.rtb1.Text = "";
            // 
            // XBoxConverter
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(292, 273);
            this.Controls.Add(this.rtb1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "XBoxConverter";
            this.Text = "XBoxConverter";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.XBoxConverter_FormClosing);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        public System.Windows.Forms.MenuStrip menuStrip1;
        public System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem xXXPCCToolStripMenuItem;
        public System.Windows.Forms.RichTextBox rtb1;
        public System.Windows.Forms.ToolStripMenuItem pCCXXXToolStripMenuItem;
    }
}