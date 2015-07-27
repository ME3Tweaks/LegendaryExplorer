namespace KFreonLib
{
    partial class TOCComparison
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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.FirstPathLabel = new System.Windows.Forms.Label();
            this.FirstBrowseButton = new System.Windows.Forms.Button();
            this.SecondPathLabel = new System.Windows.Forms.Label();
            this.SecondBrowseButton = new System.Windows.Forms.Button();
            this.CompareButton = new System.Windows.Forms.Button();
            this.FirstTreeView = new System.Windows.Forms.TreeView();
            this.SecondTreeView = new System.Windows.Forms.TreeView();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(0, 1);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.FirstTreeView);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.SecondTreeView);
            this.splitContainer1.Size = new System.Drawing.Size(1112, 520);
            this.splitContainer1.SplitterDistance = 540;
            this.splitContainer1.TabIndex = 0;
            // 
            // FirstPathLabel
            // 
            this.FirstPathLabel.AutoSize = true;
            this.FirstPathLabel.Location = new System.Drawing.Point(12, 532);
            this.FirstPathLabel.Name = "FirstPathLabel";
            this.FirstPathLabel.Size = new System.Drawing.Size(35, 13);
            this.FirstPathLabel.TabIndex = 1;
            this.FirstPathLabel.Text = "label1";
            // 
            // FirstBrowseButton
            // 
            this.FirstBrowseButton.Location = new System.Drawing.Point(430, 527);
            this.FirstBrowseButton.Name = "FirstBrowseButton";
            this.FirstBrowseButton.Size = new System.Drawing.Size(75, 23);
            this.FirstBrowseButton.TabIndex = 2;
            this.FirstBrowseButton.Text = "Browse";
            this.FirstBrowseButton.UseVisualStyleBackColor = true;
            this.FirstBrowseButton.Click += new System.EventHandler(this.FirstBrowseButton_Click);
            // 
            // SecondPathLabel
            // 
            this.SecondPathLabel.AutoSize = true;
            this.SecondPathLabel.Location = new System.Drawing.Point(592, 532);
            this.SecondPathLabel.Name = "SecondPathLabel";
            this.SecondPathLabel.Size = new System.Drawing.Size(35, 13);
            this.SecondPathLabel.TabIndex = 3;
            this.SecondPathLabel.Text = "label2";
            // 
            // SecondBrowseButton
            // 
            this.SecondBrowseButton.Location = new System.Drawing.Point(1025, 527);
            this.SecondBrowseButton.Name = "SecondBrowseButton";
            this.SecondBrowseButton.Size = new System.Drawing.Size(75, 23);
            this.SecondBrowseButton.TabIndex = 4;
            this.SecondBrowseButton.Text = "Browse";
            this.SecondBrowseButton.UseVisualStyleBackColor = true;
            this.SecondBrowseButton.Click += new System.EventHandler(this.SecondBrowseButton_Click);
            // 
            // CompareButton
            // 
            this.CompareButton.Location = new System.Drawing.Point(511, 527);
            this.CompareButton.Name = "CompareButton";
            this.CompareButton.Size = new System.Drawing.Size(75, 23);
            this.CompareButton.TabIndex = 5;
            this.CompareButton.Text = "Compare!";
            this.CompareButton.UseVisualStyleBackColor = true;
            this.CompareButton.Click += new System.EventHandler(this.CompareButton_Click);
            // 
            // FirstTreeView
            // 
            this.FirstTreeView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.FirstTreeView.Location = new System.Drawing.Point(0, 0);
            this.FirstTreeView.Name = "FirstTreeView";
            this.FirstTreeView.Size = new System.Drawing.Size(540, 520);
            this.FirstTreeView.TabIndex = 0;
            // 
            // SecondTreeView
            // 
            this.SecondTreeView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.SecondTreeView.Location = new System.Drawing.Point(0, 0);
            this.SecondTreeView.Name = "SecondTreeView";
            this.SecondTreeView.Size = new System.Drawing.Size(568, 520);
            this.SecondTreeView.TabIndex = 0;
            // 
            // TOCComparison
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1112, 554);
            this.Controls.Add(this.CompareButton);
            this.Controls.Add(this.SecondBrowseButton);
            this.Controls.Add(this.FirstPathLabel);
            this.Controls.Add(this.SecondPathLabel);
            this.Controls.Add(this.FirstBrowseButton);
            this.Controls.Add(this.splitContainer1);
            this.Name = "TOCComparison";
            this.Text = "TOCComparison";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Label FirstPathLabel;
        private System.Windows.Forms.Button FirstBrowseButton;
        private System.Windows.Forms.Label SecondPathLabel;
        private System.Windows.Forms.Button SecondBrowseButton;
        private System.Windows.Forms.Button CompareButton;
        private System.Windows.Forms.TreeView FirstTreeView;
        private System.Windows.Forms.TreeView SecondTreeView;
    }
}