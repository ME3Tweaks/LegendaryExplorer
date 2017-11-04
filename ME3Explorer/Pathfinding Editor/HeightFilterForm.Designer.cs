namespace ME3Explorer.Pathfinding_Editor
{
    partial class HeightFilterForm
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
            this.label1 = new System.Windows.Forms.Label();
            this.radioButton_FilterAbove = new System.Windows.Forms.RadioButton();
            this.radioButton_NoFilter = new System.Windows.Forms.RadioButton();
            this.radioButton_FilterBelow = new System.Windows.Forms.RadioButton();
            this.FilterZValueBox = new System.Windows.Forms.TextBox();
            this.applyFilterButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(468, 26);
            this.label1.TabIndex = 0;
            this.label1.Text = "You can filter out visible nodes by a Z value to make it easier to view nodes lay" +
    "ered on top of one \r\nanother at different heights, such as in a 2-story area.";
            // 
            // radioButton_FilterAbove
            // 
            this.radioButton_FilterAbove.AutoSize = true;
            this.radioButton_FilterAbove.Location = new System.Drawing.Point(16, 51);
            this.radioButton_FilterAbove.Name = "radioButton_FilterAbove";
            this.radioButton_FilterAbove.Size = new System.Drawing.Size(108, 17);
            this.radioButton_FilterAbove.TabIndex = 1;
            this.radioButton_FilterAbove.TabStop = true;
            this.radioButton_FilterAbove.Text = "Filter out above Z";
            this.radioButton_FilterAbove.UseVisualStyleBackColor = true;
            // 
            // radioButton_NoFilter
            // 
            this.radioButton_NoFilter.AutoSize = true;
            this.radioButton_NoFilter.Location = new System.Drawing.Point(371, 74);
            this.radioButton_NoFilter.Name = "radioButton_NoFilter";
            this.radioButton_NoFilter.Size = new System.Drawing.Size(84, 17);
            this.radioButton_NoFilter.TabIndex = 2;
            this.radioButton_NoFilter.TabStop = true;
            this.radioButton_NoFilter.Text = "Turn off filter";
            this.radioButton_NoFilter.UseVisualStyleBackColor = true;
            this.radioButton_NoFilter.CheckedChanged += new System.EventHandler(this.filterChecked_Changed);
            // 
            // radioButton_FilterBelow
            // 
            this.radioButton_FilterBelow.AutoSize = true;
            this.radioButton_FilterBelow.Location = new System.Drawing.Point(18, 100);
            this.radioButton_FilterBelow.Name = "radioButton_FilterBelow";
            this.radioButton_FilterBelow.Size = new System.Drawing.Size(106, 17);
            this.radioButton_FilterBelow.TabIndex = 3;
            this.radioButton_FilterBelow.TabStop = true;
            this.radioButton_FilterBelow.Text = "Filter out below Z";
            this.radioButton_FilterBelow.UseVisualStyleBackColor = true;
            // 
            // FilterZValueBox
            // 
            this.FilterZValueBox.Location = new System.Drawing.Point(16, 74);
            this.FilterZValueBox.Name = "FilterZValueBox";
            this.FilterZValueBox.Size = new System.Drawing.Size(100, 20);
            this.FilterZValueBox.TabIndex = 4;
            this.FilterZValueBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.FilterZValueBox_KeyPress);
            // 
            // applyFilterButton
            // 
            this.applyFilterButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.applyFilterButton.Location = new System.Drawing.Point(371, 100);
            this.applyFilterButton.Name = "applyFilterButton";
            this.applyFilterButton.Size = new System.Drawing.Size(92, 23);
            this.applyFilterButton.TabIndex = 5;
            this.applyFilterButton.Text = "Apply Filtering";
            this.applyFilterButton.UseVisualStyleBackColor = true;
            this.applyFilterButton.Click += new System.EventHandler(this.applyFilterButton_Click);
            // 
            // HeightFilterForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(478, 129);
            this.Controls.Add(this.applyFilterButton);
            this.Controls.Add(this.FilterZValueBox);
            this.Controls.Add(this.radioButton_FilterBelow);
            this.Controls.Add(this.radioButton_NoFilter);
            this.Controls.Add(this.radioButton_FilterAbove);
            this.Controls.Add(this.label1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "HeightFilterForm";
            this.Text = "Filter by Z";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.RadioButton radioButton_FilterAbove;
        private System.Windows.Forms.RadioButton radioButton_NoFilter;
        private System.Windows.Forms.RadioButton radioButton_FilterBelow;
        private System.Windows.Forms.TextBox FilterZValueBox;
        private System.Windows.Forms.Button applyFilterButton;
    }
}