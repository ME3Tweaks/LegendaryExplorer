namespace ME3Explorer.Material_Viewer
{
    partial class Shader_Code
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        public System.ComponentModel.IContainer components = null;

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
        public void InitializeComponent()
        {
            this.rtb1 = new System.Windows.Forms.RichTextBox();
            this.SuspendLayout();
            // 
            // rtb1
            // 
            this.rtb1.DetectUrls = false;
            this.rtb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtb1.Location = new System.Drawing.Point(0, 0);
            this.rtb1.Name = "rtb1";
            this.rtb1.ReadOnly = true;
            this.rtb1.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.ForcedBoth;
            this.rtb1.ShortcutsEnabled = false;
            this.rtb1.Size = new System.Drawing.Size(292, 273);
            this.rtb1.TabIndex = 0;
            this.rtb1.Text = "";
            this.rtb1.WordWrap = false;
            // 
            // Shader_Code
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(292, 273);
            this.Controls.Add(this.rtb1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Name = "Shader_Code";
            this.Text = "Shader Code";
            this.ResumeLayout(false);

        }

        #endregion

        public System.Windows.Forms.RichTextBox rtb1;
    }
}