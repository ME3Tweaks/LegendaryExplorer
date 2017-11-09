namespace ME3Explorer.Pathfinding_Editor
{
    partial class ReachSpecCreatorForm
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
            this.label1 = new System.Windows.Forms.Label();
            this.createReturningSpecCheckbox = new System.Windows.Forms.CheckBox();
            this.reachSpecTypeComboBox = new System.Windows.Forms.ComboBox();
            this.createSpecButton = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.specSizeCombobox = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.sourceNodeLabel = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.destinationNodeTextBox = new System.Windows.Forms.TextBox();
            this.destinationLabel = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.distanceLabel = new System.Windows.Forms.Label();
            this.directionLabel = new System.Windows.Forms.Label();
            this.directionX = new System.Windows.Forms.Label();
            this.directionY = new System.Windows.Forms.Label();
            this.directionZ = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(9, 126);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(105, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "ReachSpec Type";
            // 
            // createReturningSpecCheckbox
            // 
            this.createReturningSpecCheckbox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.createReturningSpecCheckbox.AutoSize = true;
            this.createReturningSpecCheckbox.Checked = true;
            this.createReturningSpecCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.createReturningSpecCheckbox.Location = new System.Drawing.Point(12, 169);
            this.createReturningSpecCheckbox.Name = "createReturningSpecCheckbox";
            this.createReturningSpecCheckbox.Size = new System.Drawing.Size(216, 17);
            this.createReturningSpecCheckbox.TabIndex = 1;
            this.createReturningSpecCheckbox.Text = "Additionally create returning ReachSpec";
            this.toolTip1.SetToolTip(this.createReturningSpecCheckbox, "Create a 2-way ReachSpec so that AI can traverse in both directions between these" +
        " nodes.");
            this.createReturningSpecCheckbox.UseVisualStyleBackColor = true;
            // 
            // reachSpecTypeComboBox
            // 
            this.reachSpecTypeComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.reachSpecTypeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.reachSpecTypeComboBox.FormattingEnabled = true;
            this.reachSpecTypeComboBox.Items.AddRange(new object[] {
            "ReachSpec",
            "SFXBoostReachSpec",
            "SFXLargeBoostReachSpec",
            "SFXLargeMantleReachSpec"});
            this.reachSpecTypeComboBox.Location = new System.Drawing.Point(21, 142);
            this.reachSpecTypeComboBox.Name = "reachSpecTypeComboBox";
            this.reachSpecTypeComboBox.Size = new System.Drawing.Size(185, 21);
            this.reachSpecTypeComboBox.TabIndex = 2;
            this.toolTip1.SetToolTip(this.reachSpecTypeComboBox, "Type of ReachSpec. Not all pawns can traverse all ReachSpecs (e.g. climbwall is o" +
        "nly usable by Husks and Abominations).");
            // 
            // createSpecButton
            // 
            this.createSpecButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.createSpecButton.Enabled = false;
            this.createSpecButton.Location = new System.Drawing.Point(360, 163);
            this.createSpecButton.Name = "createSpecButton";
            this.createSpecButton.Size = new System.Drawing.Size(127, 23);
            this.createSpecButton.TabIndex = 3;
            this.createSpecButton.Text = "Create ReachSpec";
            this.createSpecButton.UseVisualStyleBackColor = true;
            this.createSpecButton.Click += new System.EventHandler(this.createSpecButton_Click);
            // 
            // specSizeCombobox
            // 
            this.specSizeCombobox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.specSizeCombobox.FormattingEnabled = true;
            this.specSizeCombobox.Items.AddRange(new object[] {
            "Mooks(34x90)",
            "Minibosses(105x145)",
            "Bosses(140x195)"});
            this.specSizeCombobox.Location = new System.Drawing.Point(21, 90);
            this.specSizeCombobox.Name = "specSizeCombobox";
            this.specSizeCombobox.Size = new System.Drawing.Size(185, 21);
            this.specSizeCombobox.TabIndex = 16;
            this.toolTip1.SetToolTip(this.specSizeCombobox, "Type of ReachSpec. Not all pawns can traverse all ReachSpecs (e.g. climbwall is o" +
        "nly usable by Husks and Abominations).");
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(12, 9);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(85, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "Starting Node";
            // 
            // sourceNodeLabel
            // 
            this.sourceNodeLabel.AutoSize = true;
            this.sourceNodeLabel.Location = new System.Drawing.Point(22, 22);
            this.sourceNodeLabel.Name = "sourceNodeLabel";
            this.sourceNodeLabel.Size = new System.Drawing.Size(114, 13);
            this.sourceNodeLabel.TabIndex = 5;
            this.sourceNodeLabel.Text = "Export 488 | PathNode";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(12, 35);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(105, 13);
            this.label4.TabIndex = 6;
            this.label4.Text = "Destination Node";
            // 
            // destinationNodeTextBox
            // 
            this.destinationNodeTextBox.Location = new System.Drawing.Point(21, 51);
            this.destinationNodeTextBox.Name = "destinationNodeTextBox";
            this.destinationNodeTextBox.Size = new System.Drawing.Size(100, 20);
            this.destinationNodeTextBox.TabIndex = 0;
            this.destinationNodeTextBox.TextChanged += new System.EventHandler(this.destinationNodeTextBox_TextChanged);
            this.destinationNodeTextBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.destinationNodeTextBox_KeyPressed);
            // 
            // destinationLabel
            // 
            this.destinationLabel.AutoSize = true;
            this.destinationLabel.Location = new System.Drawing.Point(126, 54);
            this.destinationLabel.Name = "destinationLabel";
            this.destinationLabel.Size = new System.Drawing.Size(80, 13);
            this.destinationLabel.TabIndex = 8;
            this.destinationLabel.Text = "| Enter Export #";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(315, 9);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(99, 13);
            this.label6.TabIndex = 9;
            this.label6.Text = "ReachSpec Info";
            // 
            // distanceLabel
            // 
            this.distanceLabel.AutoSize = true;
            this.distanceLabel.Location = new System.Drawing.Point(318, 26);
            this.distanceLabel.Name = "distanceLabel";
            this.distanceLabel.Size = new System.Drawing.Size(52, 13);
            this.distanceLabel.TabIndex = 10;
            this.distanceLabel.Text = "Distance:";
            // 
            // directionLabel
            // 
            this.directionLabel.AutoSize = true;
            this.directionLabel.Location = new System.Drawing.Point(318, 39);
            this.directionLabel.Name = "directionLabel";
            this.directionLabel.Size = new System.Drawing.Size(83, 13);
            this.directionLabel.TabIndex = 11;
            this.directionLabel.Text = "Direction Vector";
            // 
            // directionX
            // 
            this.directionX.AutoSize = true;
            this.directionX.Location = new System.Drawing.Point(332, 51);
            this.directionX.Name = "directionX";
            this.directionX.Size = new System.Drawing.Size(17, 13);
            this.directionX.TabIndex = 12;
            this.directionX.Text = "X:";
            // 
            // directionY
            // 
            this.directionY.AutoSize = true;
            this.directionY.Location = new System.Drawing.Point(332, 64);
            this.directionY.Name = "directionY";
            this.directionY.Size = new System.Drawing.Size(17, 13);
            this.directionY.TabIndex = 13;
            this.directionY.Text = "Y:";
            // 
            // directionZ
            // 
            this.directionZ.AutoSize = true;
            this.directionZ.Location = new System.Drawing.Point(332, 77);
            this.directionZ.Name = "directionZ";
            this.directionZ.Size = new System.Drawing.Size(17, 13);
            this.directionZ.TabIndex = 14;
            this.directionZ.Text = "Z:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(12, 74);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(163, 13);
            this.label3.TabIndex = 15;
            this.label3.Text = "ReachSpec Size Allowance";
            // 
            // ReachSpecCreatorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(499, 198);
            this.Controls.Add(this.specSizeCombobox);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.directionZ);
            this.Controls.Add(this.directionY);
            this.Controls.Add(this.directionX);
            this.Controls.Add(this.directionLabel);
            this.Controls.Add(this.distanceLabel);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.destinationLabel);
            this.Controls.Add(this.destinationNodeTextBox);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.sourceNodeLabel);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.createSpecButton);
            this.Controls.Add(this.reachSpecTypeComboBox);
            this.Controls.Add(this.createReturningSpecCheckbox);
            this.Controls.Add(this.label1);
            this.Name = "ReachSpecCreatorForm";
            this.Text = "ReachSpec Generator";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox createReturningSpecCheckbox;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.ComboBox reachSpecTypeComboBox;
        private System.Windows.Forms.Button createSpecButton;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label sourceNodeLabel;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox destinationNodeTextBox;
        private System.Windows.Forms.Label destinationLabel;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label distanceLabel;
        private System.Windows.Forms.Label directionLabel;
        private System.Windows.Forms.Label directionX;
        private System.Windows.Forms.Label directionY;
        private System.Windows.Forms.Label directionZ;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox specSizeCombobox;
    }
}