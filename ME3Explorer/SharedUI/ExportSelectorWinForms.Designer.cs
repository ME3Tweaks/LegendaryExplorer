namespace ME3Explorer.SharedUI
{
    partial class ExportSelectorWinForms
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ExportSelectorWinForms));
            this.instructionsLabel = new System.Windows.Forms.Label();
            this.indexingLabel = new System.Windows.Forms.Label();
            this.indexField = new System.Windows.Forms.TextBox();
            this.selectedItemLabel = new System.Windows.Forms.Label();
            this.acceptButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // instructionsLabel
            // 
            this.instructionsLabel.AutoSize = true;
            this.instructionsLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.instructionsLabel.Location = new System.Drawing.Point(13, 13);
            this.instructionsLabel.Name = "instructionsLabel";
            this.instructionsLabel.Size = new System.Drawing.Size(206, 17);
            this.instructionsLabel.TabIndex = 0;
            this.instructionsLabel.Text = "Enter an export or import index.";
            // 
            // indexingLabel
            // 
            this.indexingLabel.AutoSize = true;
            this.indexingLabel.Location = new System.Drawing.Point(13, 30);
            this.indexingLabel.Name = "indexingLabel";
            this.indexingLabel.Size = new System.Drawing.Size(247, 13);
            this.indexingLabel.TabIndex = 1;
            this.indexingLabel.Text = "Data entered is 1 based indexing (Unreal indexing).";
            // 
            // indexField
            // 
            this.indexField.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.indexField.Location = new System.Drawing.Point(318, 53);
            this.indexField.Name = "indexField";
            this.indexField.Size = new System.Drawing.Size(75, 20);
            this.indexField.TabIndex = 2;
            this.indexField.TextChanged += new System.EventHandler(this.IndexBox_TextChanged);
            this.indexField.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.InputField_KeyPressed);
            // 
            // selectedItemLabel
            // 
            this.selectedItemLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.selectedItemLabel.Location = new System.Drawing.Point(12, 53);
            this.selectedItemLabel.Name = "selectedItemLabel";
            this.selectedItemLabel.Size = new System.Drawing.Size(300, 52);
            this.selectedItemLabel.TabIndex = 3;
            this.selectedItemLabel.Text = "Enter item index";
            // 
            // acceptButton
            // 
            this.acceptButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.acceptButton.Enabled = false;
            this.acceptButton.Location = new System.Drawing.Point(318, 79);
            this.acceptButton.Name = "acceptButton";
            this.acceptButton.Size = new System.Drawing.Size(75, 23);
            this.acceptButton.TabIndex = 4;
            this.acceptButton.Text = "OK";
            this.acceptButton.UseVisualStyleBackColor = true;
            this.acceptButton.Click += new System.EventHandler(this.acceptButton_Click);
            // 
            // ExportSelectorWinForms
            // 
            this.AcceptButton = this.acceptButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(405, 114);
            this.Controls.Add(this.acceptButton);
            this.Controls.Add(this.selectedItemLabel);
            this.Controls.Add(this.indexField);
            this.Controls.Add(this.indexingLabel);
            this.Controls.Add(this.instructionsLabel);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "ExportSelectorWinForms";
            this.Text = "Import/Export Selector";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label instructionsLabel;
        private System.Windows.Forms.Label indexingLabel;
        private System.Windows.Forms.TextBox indexField;
        private System.Windows.Forms.Label selectedItemLabel;
        private System.Windows.Forms.Button acceptButton;
    }
}