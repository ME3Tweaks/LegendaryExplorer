namespace ME3Creator
{
    partial class Objectselect
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.r3 = new System.Windows.Forms.RadioButton();
            this.r2 = new System.Windows.Forms.RadioButton();
            this.button2 = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.r1 = new System.Windows.Forms.RadioButton();
            this.comboBox2 = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // comboBox1
            // 
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Location = new System.Drawing.Point(167, 45);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(330, 21);
            this.comboBox1.TabIndex = 10;
            // 
            // r3
            // 
            this.r3.AutoSize = true;
            this.r3.Location = new System.Drawing.Point(21, 85);
            this.r3.Name = "r3";
            this.r3.Size = new System.Drawing.Size(87, 17);
            this.r3.TabIndex = 9;
            this.r3.Text = "Select Import";
            this.r3.UseVisualStyleBackColor = true;
            // 
            // r2
            // 
            this.r2.AutoSize = true;
            this.r2.Location = new System.Drawing.Point(21, 49);
            this.r2.Name = "r2";
            this.r2.Size = new System.Drawing.Size(88, 17);
            this.r2.TabIndex = 8;
            this.r2.Text = "Select Export";
            this.r2.UseVisualStyleBackColor = true;
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(329, 111);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(81, 26);
            this.button2.TabIndex = 7;
            this.button2.Text = "Abort";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(416, 111);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(81, 26);
            this.button1.TabIndex = 6;
            this.button1.Text = "OK";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // r1
            // 
            this.r1.AutoSize = true;
            this.r1.Checked = true;
            this.r1.Location = new System.Drawing.Point(21, 12);
            this.r1.Name = "r1";
            this.r1.Size = new System.Drawing.Size(108, 17);
            this.r1.TabIndex = 12;
            this.r1.TabStop = true;
            this.r1.Text = "Select \"Class\" (0)";
            this.r1.UseVisualStyleBackColor = true;
            // 
            // comboBox2
            // 
            this.comboBox2.FormattingEnabled = true;
            this.comboBox2.Location = new System.Drawing.Point(167, 81);
            this.comboBox2.Name = "comboBox2";
            this.comboBox2.Size = new System.Drawing.Size(330, 21);
            this.comboBox2.TabIndex = 13;
            // 
            // Objectselect
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(503, 146);
            this.Controls.Add(this.comboBox2);
            this.Controls.Add(this.r1);
            this.Controls.Add(this.comboBox1);
            this.Controls.Add(this.r3);
            this.Controls.Add(this.r2);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "Objectselect";
            this.Text = "Object Selection";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Objectselect_FormClosing);
            this.Shown += new System.EventHandler(this.Objectselect_Shown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.RadioButton r3;
        private System.Windows.Forms.RadioButton r2;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.RadioButton r1;
        private System.Windows.Forms.ComboBox comboBox2;
    }
}