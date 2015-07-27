namespace TestSample
{
    partial class KittenPuppyControl
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
            this.ctrlPuppy = new System.Windows.Forms.PictureBox();
            this.ctrlKitten = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.ctrlPuppy)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ctrlKitten)).BeginInit();
            this.SuspendLayout();
            // 
            // ctrlPuppy
            // 
            this.ctrlPuppy.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.ctrlPuppy.Image = global::TestSample.Properties.Resources.puppy;
            this.ctrlPuppy.Location = new System.Drawing.Point(0, 0);
            this.ctrlPuppy.Name = "ctrlPuppy";
            this.ctrlPuppy.Size = new System.Drawing.Size(150, 150);
            this.ctrlPuppy.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.ctrlPuppy.TabIndex = 1;
            this.ctrlPuppy.TabStop = false;
            // 
            // ctrlKitten
            // 
            this.ctrlKitten.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.ctrlKitten.Image = global::TestSample.Properties.Resources.kitten;
            this.ctrlKitten.Location = new System.Drawing.Point(0, 0);
            this.ctrlKitten.Name = "ctrlKitten";
            this.ctrlKitten.Size = new System.Drawing.Size(150, 150);
            this.ctrlKitten.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.ctrlKitten.TabIndex = 0;
            this.ctrlKitten.TabStop = false;
            // 
            // KittenPuppyControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.ctrlPuppy);
            this.Controls.Add(this.ctrlKitten);
            this.Name = "KittenPuppyControl";
            ((System.ComponentModel.ISupportInitialize)(this.ctrlPuppy)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ctrlKitten)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox ctrlKitten;
        private System.Windows.Forms.PictureBox ctrlPuppy;
    }
}
