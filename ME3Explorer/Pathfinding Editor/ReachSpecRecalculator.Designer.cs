namespace ME3Explorer.Pathfinding_Editor
{
    partial class ReachSpecRecalculator
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ReachSpecRecalculator));
            this.recalculateButton = new System.Windows.Forms.Button();
            this.reachSpecProgressBar = new System.Windows.Forms.ProgressBar();
            this.label1 = new System.Windows.Forms.Label();
            this.progressLabel = new System.Windows.Forms.Label();
            this.readOnlyCheckbox = new System.Windows.Forms.CheckBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            // 
            // recalculateButton
            // 
            this.recalculateButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.recalculateButton.Location = new System.Drawing.Point(257, 151);
            this.recalculateButton.Name = "recalculateButton";
            this.recalculateButton.Size = new System.Drawing.Size(156, 23);
            this.recalculateButton.TabIndex = 0;
            this.recalculateButton.Text = "Recalculate ReachSpecs";
            this.recalculateButton.UseVisualStyleBackColor = true;
            this.recalculateButton.Click += new System.EventHandler(this.recalculateButton_Click);
            // 
            // reachSpecProgressBar
            // 
            this.reachSpecProgressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.reachSpecProgressBar.Location = new System.Drawing.Point(12, 151);
            this.reachSpecProgressBar.Name = "reachSpecProgressBar";
            this.reachSpecProgressBar.Size = new System.Drawing.Size(239, 23);
            this.reachSpecProgressBar.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(401, 93);
            this.label1.TabIndex = 2;
            this.label1.Text = resources.GetString("label1.Text");
            // 
            // progressLabel
            // 
            this.progressLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.progressLabel.AutoSize = true;
            this.progressLabel.Location = new System.Drawing.Point(12, 135);
            this.progressLabel.Name = "progressLabel";
            this.progressLabel.Size = new System.Drawing.Size(72, 13);
            this.progressLabel.TabIndex = 3;
            this.progressLabel.Text = "Progress Text";
            this.progressLabel.Visible = false;
            // 
            // readOnlyCheckbox
            // 
            this.readOnlyCheckbox.AutoSize = true;
            this.readOnlyCheckbox.Location = new System.Drawing.Point(339, 128);
            this.readOnlyCheckbox.Name = "readOnlyCheckbox";
            this.readOnlyCheckbox.Size = new System.Drawing.Size(74, 17);
            this.readOnlyCheckbox.TabIndex = 4;
            this.readOnlyCheckbox.Text = "Read-only";
            this.toolTip1.SetToolTip(this.readOnlyCheckbox, "Prevents modification of reachspecs, only showing how many need to be updated.");
            this.readOnlyCheckbox.UseVisualStyleBackColor = true;
            // 
            // ReachSpecRecalculator
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(425, 186);
            this.Controls.Add(this.readOnlyCheckbox);
            this.Controls.Add(this.progressLabel);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.reachSpecProgressBar);
            this.Controls.Add(this.recalculateButton);
            this.Name = "ReachSpecRecalculator";
            this.Text = "ReachSpec Recalculator";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button recalculateButton;
        private System.Windows.Forms.ProgressBar reachSpecProgressBar;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label progressLabel;
        private System.Windows.Forms.CheckBox readOnlyCheckbox;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}