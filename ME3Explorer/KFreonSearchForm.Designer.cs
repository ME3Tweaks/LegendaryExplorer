namespace ME3Explorer
{
    partial class KFreonSearchForm
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
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.SearchCount = new System.Windows.Forms.ToolStripLabel();
            this.InputBox = new System.Windows.Forms.ToolStripTextBox();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.SearchCount,
            this.InputBox});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(609, 25);
            this.toolStrip1.TabIndex = 0;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // SearchCount
            // 
            this.SearchCount.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.SearchCount.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.SearchCount.Name = "SearchCount";
            this.SearchCount.Size = new System.Drawing.Size(0, 22);
            // 
            // InputBox
            // 
            this.InputBox.Name = "InputBox";
            this.InputBox.Size = new System.Drawing.Size(200, 25);
            this.InputBox.Enter += new System.EventHandler(this.InputBox_Enter);
            this.InputBox.Leave += new System.EventHandler(this.InputBox_Leave);
            this.InputBox.TextChanged += new System.EventHandler(this.InputBox_TextChanged);
            // 
            // listBox1
            // 
            this.listBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBox1.FormattingEnabled = true;
            this.listBox1.ItemHeight = 16;
            this.listBox1.Location = new System.Drawing.Point(0, 25);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(609, 397);
            this.listBox1.TabIndex = 1;
            this.listBox1.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged);
            // 
            // NewSearchForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(609, 422);
            this.Controls.Add(this.listBox1);
            this.Controls.Add(this.toolStrip1);
            this.Name = "NewSearchForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Search";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form_Closing);
            this.Load += new System.EventHandler(this.Search_Load);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripLabel SearchCount;
        private System.Windows.Forms.ToolStripTextBox InputBox;
        private System.Windows.Forms.ListBox listBox1;
    }
}