namespace ME3Explorer
{
    partial class UDKImporter
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
            this.loadUPKButton = new System.Windows.Forms.Button();
            this.currentUPKFileLabel = new System.Windows.Forms.Label();
            this.upkImportableExportsListbox = new System.Windows.Forms.ListBox();
            this.SuspendLayout();
            // 
            // loadUPKButton
            // 
            this.loadUPKButton.Location = new System.Drawing.Point(12, 12);
            this.loadUPKButton.Name = "loadUPKButton";
            this.loadUPKButton.Size = new System.Drawing.Size(97, 23);
            this.loadUPKButton.TabIndex = 0;
            this.loadUPKButton.Text = "Load UPK File";
            this.loadUPKButton.UseVisualStyleBackColor = true;
            this.loadUPKButton.Click += new System.EventHandler(this.loadUPKButton_Click);
            // 
            // currentUPKFileLabel
            // 
            this.currentUPKFileLabel.AutoSize = true;
            this.currentUPKFileLabel.Location = new System.Drawing.Point(115, 17);
            this.currentUPKFileLabel.Name = "currentUPKFileLabel";
            this.currentUPKFileLabel.Size = new System.Drawing.Size(76, 13);
            this.currentUPKFileLabel.TabIndex = 1;
            this.currentUPKFileLabel.Text = "No file opened";
            // 
            // upkImportableExportsListview
            // 
            this.upkImportableExportsListbox.FormattingEnabled = true;
            this.upkImportableExportsListbox.Location = new System.Drawing.Point(12, 41);
            this.upkImportableExportsListbox.Name = "upkImportableExportsListview";
            this.upkImportableExportsListbox.Size = new System.Drawing.Size(300, 394);
            this.upkImportableExportsListbox.TabIndex = 3;
            // 
            // UDKImporter
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.upkImportableExportsListbox);
            this.Controls.Add(this.currentUPKFileLabel);
            this.Controls.Add(this.loadUPKButton);
            this.Name = "UDKImporter";
            this.Text = "UDKImporter";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button loadUPKButton;
        private System.Windows.Forms.Label currentUPKFileLabel;
        private System.Windows.Forms.ListBox upkImportableExportsListbox;
    }
}