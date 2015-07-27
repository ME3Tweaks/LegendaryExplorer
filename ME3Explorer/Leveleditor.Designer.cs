namespace ME3Explorer
{
    partial class Leveleditor
    {

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
        public void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Leveleditor));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveCamPosToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadCamPosToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveChangesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.objectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.transformToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cloneToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.optionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.texturedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.solidToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.wireframeLitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.wireframeUnlitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editorSettingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.moveWASDIn3DScreenToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.drawBoundingSphereToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.treeView1 = new System.Windows.Forms.TreeView();
            this.pic1 = new System.Windows.Forms.PictureBox();
            this.rtb1 = new System.Windows.Forms.RichTextBox();
            this.menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pic1)).BeginInit();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.objectToolStripMenuItem,
            this.optionToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(453, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.saveCamPosToolStripMenuItem,
            this.loadCamPosToolStripMenuItem,
            this.saveChangesToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(35, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // saveCamPosToolStripMenuItem
            // 
            this.saveCamPosToolStripMenuItem.Name = "saveCamPosToolStripMenuItem";
            this.saveCamPosToolStripMenuItem.Size = new System.Drawing.Size(143, 22);
            this.saveCamPosToolStripMenuItem.Text = "Save Cam Pos";
            this.saveCamPosToolStripMenuItem.Click += new System.EventHandler(this.saveCamPosToolStripMenuItem_Click);
            // 
            // loadCamPosToolStripMenuItem
            // 
            this.loadCamPosToolStripMenuItem.Name = "loadCamPosToolStripMenuItem";
            this.loadCamPosToolStripMenuItem.Size = new System.Drawing.Size(143, 22);
            this.loadCamPosToolStripMenuItem.Text = "Load Cam Pos";
            this.loadCamPosToolStripMenuItem.Click += new System.EventHandler(this.loadCamPosToolStripMenuItem_Click);
            // 
            // saveChangesToolStripMenuItem
            // 
            this.saveChangesToolStripMenuItem.Name = "saveChangesToolStripMenuItem";
            this.saveChangesToolStripMenuItem.Size = new System.Drawing.Size(143, 22);
            this.saveChangesToolStripMenuItem.Text = "Save Changes";
            this.saveChangesToolStripMenuItem.Click += new System.EventHandler(this.saveChangesToolStripMenuItem_Click);
            // 
            // objectToolStripMenuItem
            // 
            this.objectToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.transformToolStripMenuItem,
            this.cloneToolStripMenuItem});
            this.objectToolStripMenuItem.Name = "objectToolStripMenuItem";
            this.objectToolStripMenuItem.Size = new System.Drawing.Size(51, 20);
            this.objectToolStripMenuItem.Text = "Object";
            // 
            // transformToolStripMenuItem
            // 
            this.transformToolStripMenuItem.Name = "transformToolStripMenuItem";
            this.transformToolStripMenuItem.Size = new System.Drawing.Size(123, 22);
            this.transformToolStripMenuItem.Text = "Transform";
            this.transformToolStripMenuItem.Click += new System.EventHandler(this.transformToolStripMenuItem_Click);
            // 
            // cloneToolStripMenuItem
            // 
            this.cloneToolStripMenuItem.Name = "cloneToolStripMenuItem";
            this.cloneToolStripMenuItem.Size = new System.Drawing.Size(123, 22);
            this.cloneToolStripMenuItem.Text = "Clone";
            this.cloneToolStripMenuItem.Visible = false;
            this.cloneToolStripMenuItem.Click += new System.EventHandler(this.cloneToolStripMenuItem_Click);
            // 
            // optionToolStripMenuItem
            // 
            this.optionToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.viewToolStripMenuItem,
            this.editorSettingsToolStripMenuItem});
            this.optionToolStripMenuItem.Name = "optionToolStripMenuItem";
            this.optionToolStripMenuItem.Size = new System.Drawing.Size(51, 20);
            this.optionToolStripMenuItem.Text = "Option";
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.texturedToolStripMenuItem,
            this.solidToolStripMenuItem,
            this.wireframeLitToolStripMenuItem,
            this.wireframeUnlitToolStripMenuItem});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            this.viewToolStripMenuItem.Text = "View";
            // 
            // texturedToolStripMenuItem
            // 
            this.texturedToolStripMenuItem.Name = "texturedToolStripMenuItem";
            this.texturedToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.T)));
            this.texturedToolStripMenuItem.Size = new System.Drawing.Size(193, 22);
            this.texturedToolStripMenuItem.Text = "Textured";
            this.texturedToolStripMenuItem.Click += new System.EventHandler(this.texturedToolStripMenuItem_Click);
            // 
            // solidToolStripMenuItem
            // 
            this.solidToolStripMenuItem.Name = "solidToolStripMenuItem";
            this.solidToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.solidToolStripMenuItem.Size = new System.Drawing.Size(193, 22);
            this.solidToolStripMenuItem.Text = "Solid";
            this.solidToolStripMenuItem.Click += new System.EventHandler(this.solidToolStripMenuItem_Click);
            // 
            // wireframeLitToolStripMenuItem
            // 
            this.wireframeLitToolStripMenuItem.Name = "wireframeLitToolStripMenuItem";
            this.wireframeLitToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.L)));
            this.wireframeLitToolStripMenuItem.Size = new System.Drawing.Size(193, 22);
            this.wireframeLitToolStripMenuItem.Text = "Wireframe Lit";
            this.wireframeLitToolStripMenuItem.Click += new System.EventHandler(this.wireframeLitToolStripMenuItem_Click);
            // 
            // wireframeUnlitToolStripMenuItem
            // 
            this.wireframeUnlitToolStripMenuItem.Name = "wireframeUnlitToolStripMenuItem";
            this.wireframeUnlitToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.W)));
            this.wireframeUnlitToolStripMenuItem.Size = new System.Drawing.Size(193, 22);
            this.wireframeUnlitToolStripMenuItem.Text = "Wireframe Unlit";
            this.wireframeUnlitToolStripMenuItem.Click += new System.EventHandler(this.wireframeUnlitToolStripMenuItem_Click);
            // 
            // editorSettingsToolStripMenuItem
            // 
            this.editorSettingsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.moveWASDIn3DScreenToolStripMenuItem,
            this.drawBoundingSphereToolStripMenuItem});
            this.editorSettingsToolStripMenuItem.Name = "editorSettingsToolStripMenuItem";
            this.editorSettingsToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            this.editorSettingsToolStripMenuItem.Text = "Editor Settings";
            // 
            // moveWASDIn3DScreenToolStripMenuItem
            // 
            this.moveWASDIn3DScreenToolStripMenuItem.Checked = true;
            this.moveWASDIn3DScreenToolStripMenuItem.CheckOnClick = true;
            this.moveWASDIn3DScreenToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.moveWASDIn3DScreenToolStripMenuItem.Name = "moveWASDIn3DScreenToolStripMenuItem";
            this.moveWASDIn3DScreenToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.M)));
            this.moveWASDIn3DScreenToolStripMenuItem.Size = new System.Drawing.Size(239, 22);
            this.moveWASDIn3DScreenToolStripMenuItem.Text = "Move WASD in 3D Screen";
            this.moveWASDIn3DScreenToolStripMenuItem.Click += new System.EventHandler(this.moveWASDIn3DScreenToolStripMenuItem_Click);
            // 
            // drawBoundingSphereToolStripMenuItem
            // 
            this.drawBoundingSphereToolStripMenuItem.Checked = true;
            this.drawBoundingSphereToolStripMenuItem.CheckOnClick = true;
            this.drawBoundingSphereToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.drawBoundingSphereToolStripMenuItem.Name = "drawBoundingSphereToolStripMenuItem";
            this.drawBoundingSphereToolStripMenuItem.Size = new System.Drawing.Size(239, 22);
            this.drawBoundingSphereToolStripMenuItem.Text = "Draw Bounding Sphere";
            this.drawBoundingSphereToolStripMenuItem.Click += new System.EventHandler(this.drawBoundingSphereToolStripMenuItem_Click);
            // 
            // timer1
            // 
            this.timer1.Interval = 10;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick_1);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 24);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.splitContainer2);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.rtb1);
            this.splitContainer1.Size = new System.Drawing.Size(453, 403);
            this.splitContainer1.SplitterDistance = 292;
            this.splitContainer1.TabIndex = 1;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.treeView1);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.pic1);
            this.splitContainer2.Size = new System.Drawing.Size(453, 292);
            this.splitContainer2.SplitterDistance = 151;
            this.splitContainer2.TabIndex = 0;
            // 
            // treeView1
            // 
            this.treeView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView1.Location = new System.Drawing.Point(0, 0);
            this.treeView1.Name = "treeView1";
            this.treeView1.Size = new System.Drawing.Size(151, 292);
            this.treeView1.TabIndex = 0;
            this.treeView1.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeView1_AfterSelect);
            this.treeView1.DoubleClick += new System.EventHandler(this.treeView1_DoubleClick);
            // 
            // pic1
            // 
            this.pic1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pic1.Location = new System.Drawing.Point(0, 0);
            this.pic1.Name = "pic1";
            this.pic1.Size = new System.Drawing.Size(298, 292);
            this.pic1.TabIndex = 1;
            this.pic1.TabStop = false;
            this.pic1.Click += new System.EventHandler(this.pic1_Click);
            this.pic1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.pic1_MouseClick_1);
            this.pic1.MouseMove += new System.Windows.Forms.MouseEventHandler(this.pic1_MouseMove);
            // 
            // rtb1
            // 
            this.rtb1.DetectUrls = false;
            this.rtb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtb1.HideSelection = false;
            this.rtb1.Location = new System.Drawing.Point(0, 0);
            this.rtb1.Name = "rtb1";
            this.rtb1.ReadOnly = true;
            this.rtb1.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.ForcedVertical;
            this.rtb1.Size = new System.Drawing.Size(453, 107);
            this.rtb1.TabIndex = 0;
            this.rtb1.Text = "";
            // 
            // Leveleditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(453, 427);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.Name = "Leveleditor";
            this.Text = "Leveleditor";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Leveleditor_KeyDown);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pic1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        public System.Windows.Forms.MenuStrip menuStrip1;
        public System.Windows.Forms.ToolStripMenuItem optionToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem objectToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem saveCamPosToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem loadCamPosToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem transformToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem solidToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem wireframeLitToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem wireframeUnlitToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem editorSettingsToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem moveWASDIn3DScreenToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem drawBoundingSphereToolStripMenuItem;
        public System.Windows.Forms.Timer timer1;
        public System.Windows.Forms.SplitContainer splitContainer1;
        public System.Windows.Forms.SplitContainer splitContainer2;
        public System.Windows.Forms.TreeView treeView1;
        public System.Windows.Forms.PictureBox pic1;
        public System.Windows.Forms.RichTextBox rtb1;
        private System.ComponentModel.IContainer components;
        private System.Windows.Forms.ToolStripMenuItem texturedToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem cloneToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveChangesToolStripMenuItem;

    }
}