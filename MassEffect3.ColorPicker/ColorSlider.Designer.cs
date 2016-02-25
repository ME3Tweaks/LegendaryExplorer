namespace MassEffect3.ColorPicker
{
    partial class ColorSlider
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label = new System.Windows.Forms.Label();
            this.colorGradient = new ColorPicker.ColorGradient();
            this.numericUpDown = new System.Windows.Forms.NumericUpDown();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown)).BeginInit();
            this.SuspendLayout();
            // 
            // label
            // 
            this.label.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.label.Location = new System.Drawing.Point(0, 4);
            this.label.Name = "label";
            this.label.Size = new System.Drawing.Size(20, 13);
            this.label.TabIndex = 0;
            this.label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // colorGradient
            // 
            this.colorGradient.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.colorGradient.Count = 1;
            this.colorGradient.CustomGradient = null;
            this.colorGradient.DrawFarNub = true;
            this.colorGradient.DrawNearNub = false;
            this.colorGradient.Location = new System.Drawing.Point(21, 1);
            this.colorGradient.MaxColor = System.Drawing.Color.White;
            this.colorGradient.MinColor = System.Drawing.Color.Black;
            this.colorGradient.Name = "colorGradient";
            this.colorGradient.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.colorGradient.Size = new System.Drawing.Size(73, 19);
            this.colorGradient.TabIndex = 1;
            this.colorGradient.Value = 0;
            this.colorGradient.ValueChanged += new System.EventHandler(this.OnValueChanged);
            // 
            // numericUpDown
            // 
            this.numericUpDown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.numericUpDown.Location = new System.Drawing.Point(98, 0);
            this.numericUpDown.Name = "numericUpDown";
            this.numericUpDown.Size = new System.Drawing.Size(56, 20);
            this.numericUpDown.TabIndex = 2;
            this.numericUpDown.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.numericUpDown.ValueChanged += new System.EventHandler(this.OnValueChanged);
            this.numericUpDown.Leave += new System.EventHandler(this.OnUpDownLeave);
            this.numericUpDown.KeyUp += new System.Windows.Forms.KeyEventHandler(this.OnUpDownKeyUp);
            this.numericUpDown.Enter += new System.EventHandler(this.OnUpDownEnter);
            // 
            // ColorSlider
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.Controls.Add(this.numericUpDown);
            this.Controls.Add(this.colorGradient);
            this.Controls.Add(this.label);
            this.Name = "ColorSlider";
            this.Size = new System.Drawing.Size(154, 20);
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label;
        private ColorGradient colorGradient;
        private System.Windows.Forms.NumericUpDown numericUpDown;
    }
}
