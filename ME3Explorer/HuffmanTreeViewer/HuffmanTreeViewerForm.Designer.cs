using System.Drawing;

namespace ME3Explorer.HuffmanTreeViewer
{
    partial class HuffmanTreeViewerForm
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
            this.graphEditor1 = new HuffmanGraph();
            this.SuspendLayout();
            // 
            // graphEditor1
            // 
            this.graphEditor1.AllowDrop = true;
            this.graphEditor1.BackColor = System.Drawing.Color.SlateGray;
            this.graphEditor1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.graphEditor1.GridFitText = false;
            this.graphEditor1.Location = new System.Drawing.Point(0, 0);
            this.graphEditor1.Name = "graphEditor1";
            this.graphEditor1.RegionManagement = true;
            this.graphEditor1.Size = new System.Drawing.Size(1244, 844);
            this.graphEditor1.TabIndex = 0;
            this.graphEditor1.Text = "graphEditor1";
            // 
            // HuffmanTreeViewerForm
            // 
            this.ClientSize = new System.Drawing.Size(1244, 844);
            this.Controls.Add(this.graphEditor1);
            this.Name = "HuffmanTreeViewerForm";
            this.Text = "HuffmanTreeViewerForm";
            this.ResumeLayout(false);

        }

        #endregion

        private HuffmanGraph graphEditor1;
    }
}