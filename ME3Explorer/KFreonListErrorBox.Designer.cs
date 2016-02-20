namespace ME3Explorer
{
    partial class KFreonListErrorBox
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
            this.MainMessageTextBox = new System.Windows.Forms.RichTextBox();
            this.ItemsBox = new System.Windows.Forms.ListBox();
            this.OKButton = new System.Windows.Forms.Button();
            this.SaveButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // MainMessageTextBox
            // 
            this.MainMessageTextBox.BackColor = System.Drawing.SystemColors.Control;
            this.MainMessageTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.MainMessageTextBox.Dock = System.Windows.Forms.DockStyle.Top;
            this.MainMessageTextBox.Location = new System.Drawing.Point(0, 0);
            this.MainMessageTextBox.Name = "MainMessageTextBox";
            this.MainMessageTextBox.Size = new System.Drawing.Size(559, 114);
            this.MainMessageTextBox.TabIndex = 0;
            this.MainMessageTextBox.Text = "";
            // 
            // ItemsBox
            // 
            this.ItemsBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ItemsBox.FormattingEnabled = true;
            this.ItemsBox.Location = new System.Drawing.Point(0, 119);
            this.ItemsBox.Name = "ItemsBox";
            this.ItemsBox.Size = new System.Drawing.Size(559, 95);
            this.ItemsBox.TabIndex = 1;
            // 
            // OKButton
            // 
            this.OKButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.OKButton.Location = new System.Drawing.Point(12, 234);
            this.OKButton.Name = "OKButton";
            this.OKButton.Size = new System.Drawing.Size(75, 23);
            this.OKButton.TabIndex = 2;
            this.OKButton.Text = "OK";
            this.OKButton.UseVisualStyleBackColor = true;
            this.OKButton.Click += new System.EventHandler(this.OKButton_Click);
            // 
            // SaveButton
            // 
            this.SaveButton.Location = new System.Drawing.Point(239, 234);
            this.SaveButton.Name = "SaveButton";
            this.SaveButton.Size = new System.Drawing.Size(75, 23);
            this.SaveButton.TabIndex = 3;
            this.SaveButton.Text = "Save Items";
            this.SaveButton.UseVisualStyleBackColor = true;
            this.SaveButton.Click += new System.EventHandler(this.SaveButton_Click);
            // 
            // KFreonListErrorBox
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(559, 269);
            this.Controls.Add(this.SaveButton);
            this.Controls.Add(this.OKButton);
            this.Controls.Add(this.ItemsBox);
            this.Controls.Add(this.MainMessageTextBox);
            this.Name = "KFreonListErrorBox";
            this.Text = "Error";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.RichTextBox MainMessageTextBox;
        private System.Windows.Forms.ListBox ItemsBox;
        private System.Windows.Forms.Button OKButton;
        private System.Windows.Forms.Button SaveButton;
    }
}