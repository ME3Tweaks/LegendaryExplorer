namespace ME3Explorer
{
    partial class TexplorerFirstTimeSetup
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TexplorerFirstTimeSetup));
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.ContinueButton = new System.Windows.Forms.ToolStripButton();
            this.ExitButton = new System.Windows.Forms.ToolStripButton();
            this.StatusProgBar = new System.Windows.Forms.ToolStripProgressBar();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.StatusProgLabel = new System.Windows.Forms.ToolStripLabel();
            this.MainListView = new System.Windows.Forms.CheckedListBox();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ContinueButton,
            this.ExitButton,
            this.StatusProgBar,
            this.toolStripSeparator1,
            this.StatusProgLabel});
            this.toolStrip1.Location = new System.Drawing.Point(0, 557);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Padding = new System.Windows.Forms.Padding(0, 0, 2, 0);
            this.toolStrip1.Size = new System.Drawing.Size(568, 37);
            this.toolStrip1.TabIndex = 0;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // ContinueButton
            // 
            this.ContinueButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.ContinueButton.Image = ((System.Drawing.Image)(resources.GetObject("ContinueButton.Image")));
            this.ContinueButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.ContinueButton.Name = "ContinueButton";
            this.ContinueButton.Size = new System.Drawing.Size(87, 34);
            this.ContinueButton.Text = "Continue";
            this.ContinueButton.Click += new System.EventHandler(this.ContinueButton_Click);
            // 
            // ExitButton
            // 
            this.ExitButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.ExitButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.ExitButton.Image = ((System.Drawing.Image)(resources.GetObject("ExitButton.Image")));
            this.ExitButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.ExitButton.Name = "ExitButton";
            this.ExitButton.Size = new System.Drawing.Size(67, 34);
            this.ExitButton.Text = "Cancel";
            this.ExitButton.Click += new System.EventHandler(this.ExitButton_Click);
            // 
            // StatusProgBar
            // 
            this.StatusProgBar.Name = "StatusProgBar";
            this.StatusProgBar.Size = new System.Drawing.Size(150, 34);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 37);
            // 
            // StatusProgLabel
            // 
            this.StatusProgLabel.Name = "StatusProgLabel";
            this.StatusProgLabel.Size = new System.Drawing.Size(131, 34);
            this.StatusProgLabel.Text = "toolStripLabel1";
            // 
            // MainListView
            // 
            this.MainListView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.MainListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.MainListView.FormattingEnabled = true;
            this.MainListView.HorizontalScrollbar = true;
            this.MainListView.Location = new System.Drawing.Point(0, 0);
            this.MainListView.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.MainListView.Name = "MainListView";
            this.MainListView.Size = new System.Drawing.Size(568, 557);
            this.MainListView.TabIndex = 0;
            this.MainListView.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.MainListView_ItemCheck);
            this.MainListView.SelectedIndexChanged += new System.EventHandler(this.MainListView_SelectedIndexChanged);
            // 
            // TexplorerFirstTimeSetup
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(144F, 144F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(568, 594);
            this.Controls.Add(this.MainListView);
            this.Controls.Add(this.toolStrip1);
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.Name = "TexplorerFirstTimeSetup";
            this.Text = "TexplorerFirstTimeSetup";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormClosingEvent);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton ContinueButton;
        private System.Windows.Forms.ToolStripButton ExitButton;
        private System.Windows.Forms.ToolStripProgressBar StatusProgBar;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripLabel StatusProgLabel;
        private System.Windows.Forms.CheckedListBox MainListView;
    }
}