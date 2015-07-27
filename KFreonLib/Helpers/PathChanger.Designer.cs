namespace KFreonLib.Helpers
{
    partial class PathChanger
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
            this.ME1Path = new System.Windows.Forms.TextBox();
            this.ME1Label = new System.Windows.Forms.Label();
            this.ME2Label = new System.Windows.Forms.Label();
            this.ME2Path = new System.Windows.Forms.TextBox();
            this.ME3Label = new System.Windows.Forms.Label();
            this.ME3Path = new System.Windows.Forms.TextBox();
            this.OKButton = new System.Windows.Forms.Button();
            this.CancelPushButton = new System.Windows.Forms.Button();
            this.BrowseME1Button = new System.Windows.Forms.Button();
            this.BrowseME2Button = new System.Windows.Forms.Button();
            this.BrowseME3Button = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(148, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(283, 17);
            this.label1.TabIndex = 0;
            this.label1.Text = "This tool changes path for speficied game/s";
            // 
            // ME1Path
            // 
            this.ME1Path.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ME1Path.Location = new System.Drawing.Point(50, 48);
            this.ME1Path.Name = "ME1Path";
            this.ME1Path.Size = new System.Drawing.Size(483, 20);
            this.ME1Path.TabIndex = 1;
            this.ME1Path.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ME1Box_KeyDown);
            // 
            // ME1Label
            // 
            this.ME1Label.AutoSize = true;
            this.ME1Label.Location = new System.Drawing.Point(12, 51);
            this.ME1Label.Name = "ME1Label";
            this.ME1Label.Size = new System.Drawing.Size(32, 13);
            this.ME1Label.TabIndex = 2;
            this.ME1Label.Text = "ME1:";
            // 
            // ME2Label
            // 
            this.ME2Label.AutoSize = true;
            this.ME2Label.Location = new System.Drawing.Point(12, 89);
            this.ME2Label.Name = "ME2Label";
            this.ME2Label.Size = new System.Drawing.Size(32, 13);
            this.ME2Label.TabIndex = 3;
            this.ME2Label.Text = "ME2:";
            // 
            // ME2Path
            // 
            this.ME2Path.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ME2Path.Location = new System.Drawing.Point(50, 86);
            this.ME2Path.Name = "ME2Path";
            this.ME2Path.Size = new System.Drawing.Size(483, 20);
            this.ME2Path.TabIndex = 4;
            this.ME2Path.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ME2Box_KeyDown);
            // 
            // ME3Label
            // 
            this.ME3Label.AutoSize = true;
            this.ME3Label.Location = new System.Drawing.Point(12, 125);
            this.ME3Label.Name = "ME3Label";
            this.ME3Label.Size = new System.Drawing.Size(32, 13);
            this.ME3Label.TabIndex = 5;
            this.ME3Label.Text = "ME3:";
            // 
            // ME3Path
            // 
            this.ME3Path.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ME3Path.Location = new System.Drawing.Point(50, 122);
            this.ME3Path.Name = "ME3Path";
            this.ME3Path.Size = new System.Drawing.Size(483, 20);
            this.ME3Path.TabIndex = 6;
            this.ME3Path.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ME3Box_KeyDown);
            // 
            // OKButton
            // 
            this.OKButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.OKButton.Location = new System.Drawing.Point(50, 172);
            this.OKButton.Name = "OKButton";
            this.OKButton.Size = new System.Drawing.Size(75, 23);
            this.OKButton.TabIndex = 7;
            this.OKButton.Text = "OK";
            this.OKButton.UseVisualStyleBackColor = true;
            this.OKButton.Click += new System.EventHandler(this.OKButton_Click);
            // 
            // CancelPushButton
            // 
            this.CancelPushButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.CancelPushButton.Location = new System.Drawing.Point(485, 172);
            this.CancelPushButton.Name = "CancelPushButton";
            this.CancelPushButton.Size = new System.Drawing.Size(75, 23);
            this.CancelPushButton.TabIndex = 8;
            this.CancelPushButton.Text = "Cancel";
            this.CancelPushButton.UseVisualStyleBackColor = true;
            this.CancelPushButton.Click += new System.EventHandler(this.CancelPushButton_Click);
            // 
            // BrowseME1Button
            // 
            this.BrowseME1Button.Location = new System.Drawing.Point(539, 46);
            this.BrowseME1Button.Name = "BrowseME1Button";
            this.BrowseME1Button.Size = new System.Drawing.Size(75, 23);
            this.BrowseME1Button.TabIndex = 9;
            this.BrowseME1Button.Text = "Browse";
            this.BrowseME1Button.UseVisualStyleBackColor = true;
            this.BrowseME1Button.Click += new System.EventHandler(this.BrowseME1Button_Click);
            // 
            // BrowseME2Button
            // 
            this.BrowseME2Button.Location = new System.Drawing.Point(539, 84);
            this.BrowseME2Button.Name = "BrowseME2Button";
            this.BrowseME2Button.Size = new System.Drawing.Size(75, 23);
            this.BrowseME2Button.TabIndex = 10;
            this.BrowseME2Button.Text = "Browse";
            this.BrowseME2Button.UseVisualStyleBackColor = true;
            this.BrowseME2Button.Click += new System.EventHandler(this.BrowseME2Button_Click);
            // 
            // BrowseME3Button
            // 
            this.BrowseME3Button.Location = new System.Drawing.Point(539, 120);
            this.BrowseME3Button.Name = "BrowseME3Button";
            this.BrowseME3Button.Size = new System.Drawing.Size(75, 23);
            this.BrowseME3Button.TabIndex = 11;
            this.BrowseME3Button.Text = "Browse";
            this.BrowseME3Button.UseVisualStyleBackColor = true;
            this.BrowseME3Button.Click += new System.EventHandler(this.BrowseME3Button_Click);
            // 
            // PathChanger
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(620, 207);
            this.Controls.Add(this.BrowseME3Button);
            this.Controls.Add(this.BrowseME2Button);
            this.Controls.Add(this.BrowseME1Button);
            this.Controls.Add(this.CancelPushButton);
            this.Controls.Add(this.OKButton);
            this.Controls.Add(this.ME3Path);
            this.Controls.Add(this.ME3Label);
            this.Controls.Add(this.ME2Path);
            this.Controls.Add(this.ME2Label);
            this.Controls.Add(this.ME1Label);
            this.Controls.Add(this.ME1Path);
            this.Controls.Add(this.label1);
            this.Name = "PathChanger";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Game Path Changer";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox ME1Path;
        private System.Windows.Forms.Label ME1Label;
        private System.Windows.Forms.Label ME2Label;
        private System.Windows.Forms.TextBox ME2Path;
        private System.Windows.Forms.Label ME3Label;
        private System.Windows.Forms.TextBox ME3Path;
        private System.Windows.Forms.Button OKButton;
        private System.Windows.Forms.Button CancelPushButton;
        private System.Windows.Forms.Button BrowseME1Button;
        private System.Windows.Forms.Button BrowseME2Button;
        private System.Windows.Forms.Button BrowseME3Button;
    }
}