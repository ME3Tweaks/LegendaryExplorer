namespace ME3Explorer.Pathfinding_Editor
{
    partial class DuplicateGUIDWindow
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
            this.duplicatesListBox = new System.Windows.Forms.ListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.generateButton = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.aLabel = new System.Windows.Forms.Label();
            this.bLabel = new System.Windows.Forms.Label();
            this.cLabel = new System.Windows.Forms.Label();
            this.dLabel = new System.Windows.Forms.Label();
            this.crossLevelPathsLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // duplicatesListBox
            // 
            this.duplicatesListBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.duplicatesListBox.FormattingEnabled = true;
            this.duplicatesListBox.Location = new System.Drawing.Point(12, 25);
            this.duplicatesListBox.Name = "duplicatesListBox";
            this.duplicatesListBox.Size = new System.Drawing.Size(356, 225);
            this.duplicatesListBox.TabIndex = 0;
            this.duplicatesListBox.SelectedIndexChanged += new System.EventHandler(this.duplicateGuidList_SelectionChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(661, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Ensure there are no duplicate navigation GUIDs in this file to ensure proper path" +
    "finding. Regenerate ones that don\'t have cross level paths.";
            // 
            // generateButton
            // 
            this.generateButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.generateButton.Enabled = false;
            this.generateButton.Location = new System.Drawing.Point(613, 237);
            this.generateButton.Name = "generateButton";
            this.generateButton.Size = new System.Drawing.Size(149, 23);
            this.generateButton.TabIndex = 2;
            this.generateButton.Text = "Generate new GUID";
            this.generateButton.UseVisualStyleBackColor = true;
            this.generateButton.Click += new System.EventHandler(this.generateButton_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(377, 25);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(51, 20);
            this.label2.TabIndex = 3;
            this.label2.Text = "GUID";
            // 
            // aLabel
            // 
            this.aLabel.AutoSize = true;
            this.aLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.aLabel.Location = new System.Drawing.Point(394, 45);
            this.aLabel.Name = "aLabel";
            this.aLabel.Size = new System.Drawing.Size(21, 17);
            this.aLabel.TabIndex = 4;
            this.aLabel.Text = "A:";
            // 
            // bLabel
            // 
            this.bLabel.AutoSize = true;
            this.bLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.bLabel.Location = new System.Drawing.Point(394, 62);
            this.bLabel.Name = "bLabel";
            this.bLabel.Size = new System.Drawing.Size(21, 17);
            this.bLabel.TabIndex = 5;
            this.bLabel.Text = "B:";
            // 
            // cLabel
            // 
            this.cLabel.AutoSize = true;
            this.cLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cLabel.Location = new System.Drawing.Point(394, 79);
            this.cLabel.Name = "cLabel";
            this.cLabel.Size = new System.Drawing.Size(21, 17);
            this.cLabel.TabIndex = 6;
            this.cLabel.Text = "C:";
            // 
            // dLabel
            // 
            this.dLabel.AutoSize = true;
            this.dLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dLabel.Location = new System.Drawing.Point(394, 96);
            this.dLabel.Name = "dLabel";
            this.dLabel.Size = new System.Drawing.Size(22, 17);
            this.dLabel.TabIndex = 7;
            this.dLabel.Text = "D:";
            // 
            // crossLevelPathsLabel
            // 
            this.crossLevelPathsLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.crossLevelPathsLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.crossLevelPathsLabel.Location = new System.Drawing.Point(377, 133);
            this.crossLevelPathsLabel.Name = "crossLevelPathsLabel";
            this.crossLevelPathsLabel.Size = new System.Drawing.Size(385, 101);
            this.crossLevelPathsLabel.TabIndex = 8;
            this.crossLevelPathsLabel.Text = "This navigation node has cross level paths - you shouldn\'t generate a new GUID fo" +
    "r this!";
            // 
            // DuplicateGUIDWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(774, 272);
            this.Controls.Add(this.crossLevelPathsLabel);
            this.Controls.Add(this.dLabel);
            this.Controls.Add(this.cLabel);
            this.Controls.Add(this.bLabel);
            this.Controls.Add(this.aLabel);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.generateButton);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.duplicatesListBox);
            this.Name = "DuplicateGUIDWindow";
            this.Text = "DuplicateGUIDWindow";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox duplicatesListBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button generateButton;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label aLabel;
        private System.Windows.Forms.Label bLabel;
        private System.Windows.Forms.Label cLabel;
        private System.Windows.Forms.Label dLabel;
        private System.Windows.Forms.Label crossLevelPathsLabel;
    }
}