namespace ME3VanillaMaker
{
    partial class Form1
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
            this.components = new System.ComponentModel.Container();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.treeView1 = new System.Windows.Forms.TreeView();
            this.context = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.createFullBackupToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.label1 = new System.Windows.Forms.Label();
            this.treeView2 = new System.Windows.Forms.TreeView();
            this.context2 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.restoreAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.label2 = new System.Windows.Forms.Label();
            this.FolderDialog = new System.Windows.Forms.FolderBrowserDialog();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.context.SuspendLayout();
            this.context2.SuspendLayout();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Dock = System.Windows.Forms.DockStyle.Top;
            this.button1.Location = new System.Drawing.Point(0, 20);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(704, 23);
            this.button1.TabIndex = 0;
            this.button1.Text = "Game Path...";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // button2
            // 
            this.button2.Dock = System.Windows.Forms.DockStyle.Top;
            this.button2.Location = new System.Drawing.Point(0, 63);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(704, 23);
            this.button2.TabIndex = 1;
            this.button2.Text = "Backup Path";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // textBox1
            // 
            this.textBox1.Dock = System.Windows.Forms.DockStyle.Top;
            this.textBox1.Location = new System.Drawing.Point(0, 0);
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            this.textBox1.Size = new System.Drawing.Size(704, 20);
            this.textBox1.TabIndex = 2;
            // 
            // textBox2
            // 
            this.textBox2.Dock = System.Windows.Forms.DockStyle.Top;
            this.textBox2.Location = new System.Drawing.Point(0, 43);
            this.textBox2.Name = "textBox2";
            this.textBox2.ReadOnly = true;
            this.textBox2.Size = new System.Drawing.Size(704, 20);
            this.textBox2.TabIndex = 3;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 86);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.treeView1);
            this.splitContainer1.Panel1.Controls.Add(this.label1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.treeView2);
            this.splitContainer1.Panel2.Controls.Add(this.label2);
            this.splitContainer1.Size = new System.Drawing.Size(704, 486);
            this.splitContainer1.SplitterDistance = 349;
            this.splitContainer1.TabIndex = 4;
            // 
            // treeView1
            // 
            this.treeView1.ContextMenuStrip = this.context;
            this.treeView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.treeView1.Location = new System.Drawing.Point(0, 13);
            this.treeView1.Name = "treeView1";
            this.treeView1.Size = new System.Drawing.Size(349, 473);
            this.treeView1.TabIndex = 0;
            // 
            // context
            // 
            this.context.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.createFullBackupToolStripMenuItem});
            this.context.Name = "context";
            this.context.Size = new System.Drawing.Size(164, 26);
            // 
            // createFullBackupToolStripMenuItem
            // 
            this.createFullBackupToolStripMenuItem.Name = "createFullBackupToolStripMenuItem";
            this.createFullBackupToolStripMenuItem.Size = new System.Drawing.Size(163, 22);
            this.createFullBackupToolStripMenuItem.Text = "Create Full Backup";
            this.createFullBackupToolStripMenuItem.Click += new System.EventHandler(this.createFullBackupToolStripMenuItem_Click_1);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Top;
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(35, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Game";
            // 
            // treeView2
            // 
            this.treeView2.ContextMenuStrip = this.context2;
            this.treeView2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView2.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.treeView2.Location = new System.Drawing.Point(0, 13);
            this.treeView2.Name = "treeView2";
            this.treeView2.Size = new System.Drawing.Size(351, 473);
            this.treeView2.TabIndex = 0;
            // 
            // context2
            // 
            this.context2.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.restoreAllToolStripMenuItem});
            this.context2.Name = "context2";
            this.context2.Size = new System.Drawing.Size(127, 26);
            // 
            // restoreAllToolStripMenuItem
            // 
            this.restoreAllToolStripMenuItem.Name = "restoreAllToolStripMenuItem";
            this.restoreAllToolStripMenuItem.Size = new System.Drawing.Size(126, 22);
            this.restoreAllToolStripMenuItem.Text = "Restore All";
            this.restoreAllToolStripMenuItem.Click += new System.EventHandler(this.restoreAllToolStripMenuItem_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Dock = System.Windows.Forms.DockStyle.Top;
            this.label2.Location = new System.Drawing.Point(0, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(44, 13);
            this.label2.TabIndex = 1;
            this.label2.Text = "Backup";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(704, 572);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.textBox2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.textBox1);
            this.Name = "Form1";
            this.Text = "ME3 Vanilla Maker by Warranty Voider";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.context.ResumeLayout(false);
            this.context2.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TreeView treeView1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TreeView treeView2;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.FolderBrowserDialog FolderDialog;
        private System.Windows.Forms.ContextMenuStrip context;
        private System.Windows.Forms.ToolStripMenuItem createFullBackupToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip context2;
        private System.Windows.Forms.ToolStripMenuItem restoreAllToolStripMenuItem;
    }
}

