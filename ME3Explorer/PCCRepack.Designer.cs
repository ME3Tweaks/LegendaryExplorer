namespace ME3Explorer
{
    partial class PCCRepack
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PCCRepack));
            this.buttonCompressPCC = new System.Windows.Forms.Button();
            this.openPccDialog = new System.Windows.Forms.OpenFileDialog();
            this.buttonDecompressPCC = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // buttonCompressPCC
            // 
            this.buttonCompressPCC.Location = new System.Drawing.Point(12, 12);
            this.buttonCompressPCC.Name = "buttonCompressPCC";
            this.buttonCompressPCC.Size = new System.Drawing.Size(140, 35);
            this.buttonCompressPCC.TabIndex = 0;
            this.buttonCompressPCC.Text = "Select pcc file to compress";
            this.buttonCompressPCC.UseVisualStyleBackColor = true;
            this.buttonCompressPCC.Click += new System.EventHandler(this.buttonCompressPCC_Click);
            // 
            // openPccDialog
            // 
            this.openPccDialog.Filter = "Pcc files (*.pcc)|*.pcc|All files (*.*)|*.*";
            // 
            // buttonDecompressPCC
            // 
            this.buttonDecompressPCC.Location = new System.Drawing.Point(12, 53);
            this.buttonDecompressPCC.Name = "buttonDecompressPCC";
            this.buttonDecompressPCC.Size = new System.Drawing.Size(140, 36);
            this.buttonDecompressPCC.TabIndex = 1;
            this.buttonDecompressPCC.Text = "Select pcc file to decompress";
            this.buttonDecompressPCC.UseVisualStyleBackColor = true;
            this.buttonDecompressPCC.Click += new System.EventHandler(this.buttonDecompressPCC_Click);
            // 
            // PCCRepack
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(367, 130);
            this.Controls.Add(this.buttonDecompressPCC);
            this.Controls.Add(this.buttonCompressPCC);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "PCCRepack";
            this.Text = "PCCRepack";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.PCCRepack_FormClosing);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button buttonCompressPCC;
        private System.Windows.Forms.OpenFileDialog openPccDialog;
        private System.Windows.Forms.Button buttonDecompressPCC;
    }
}