namespace ME1Explorer
{
    partial class Selector
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
            this.SelectionBox = new System.Windows.Forms.CheckedListBox();
            this.OKButton = new System.Windows.Forms.Button();
            this.CancelButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // SelectionBox
            // 
            this.SelectionBox.CheckOnClick = true;
            this.SelectionBox.FormattingEnabled = true;
            this.SelectionBox.Location = new System.Drawing.Point(-1, -2);
            this.SelectionBox.Name = "SelectionBox";
            this.SelectionBox.Size = new System.Drawing.Size(459, 394);
            this.SelectionBox.TabIndex = 0;
            this.SelectionBox.SelectedIndexChanged += new System.EventHandler(this.SelectionBox_SelectedIndexChanged);
            // 
            // OKButton
            // 
            this.OKButton.Location = new System.Drawing.Point(12, 398);
            this.OKButton.Name = "OKButton";
            this.OKButton.Size = new System.Drawing.Size(75, 23);
            this.OKButton.TabIndex = 1;
            this.OKButton.Text = "OK";
            this.OKButton.UseVisualStyleBackColor = true;
            this.OKButton.Click += new System.EventHandler(this.OKButton_Click);
            // 
            // CancelButton
            // 
            this.CancelButton.Location = new System.Drawing.Point(370, 398);
            this.CancelButton.Name = "CancelButton";
            this.CancelButton.Size = new System.Drawing.Size(75, 23);
            this.CancelButton.TabIndex = 2;
            this.CancelButton.Text = "Cancel";
            this.CancelButton.UseVisualStyleBackColor = true;
            this.CancelButton.Click += new System.EventHandler(this.CancelButton_Click);
            // 
            // Selector
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(457, 428);
            this.Controls.Add(this.CancelButton);
            this.Controls.Add(this.OKButton);
            this.Controls.Add(this.SelectionBox);
            this.Name = "Selector";
            this.Text = "Selector";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.CheckedListBox SelectionBox;
        private System.Windows.Forms.Button OKButton;
        private System.Windows.Forms.Button CancelButton;
    }
}