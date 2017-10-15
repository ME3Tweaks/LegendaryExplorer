namespace ME3Explorer
{
    partial class PathfindingEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PathfindingEditor));
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.graphEditor = new UMD.HCIL.PathingGraphEditor.PathingGraphEditor();
            this.splitContainer3 = new System.Windows.Forms.SplitContainer();
            this.pg1 = new System.Windows.Forms.PropertyGrid();
            this.interpreter1 = new ME3Explorer.Interpreter();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.savePccToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.savePCCAsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveViewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.togglePathfindingNodes = new System.Windows.Forms.ToolStripMenuItem();
            this.toggleActorNodes = new System.Windows.Forms.ToolStripMenuItem();
            this.staticMeshCollectionActorsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripTextBox1 = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripButton1 = new System.Windows.Forms.ToolStripButton();
            this.rightMouseButtonMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.openInPackageEditorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cloneToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.breakLinksToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.changeNodeTypeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toSFXEnemySpawnPointToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toPathNodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toSFXNavTurretPointToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toSFXNavBoostNodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sFXNavBoostNodeTopToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sFXNavBoostNodeBottomToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createReachSpecToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.setGraphPositionAsNodeLocationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.generateNewRandomGUIDToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.filenameLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel2 = new System.Windows.Forms.ToolStripStatusLabel();
            this.contextMenuStrip2 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addObjectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).BeginInit();
            this.splitContainer3.Panel1.SuspendLayout();
            this.splitContainer3.Panel2.SuspendLayout();
            this.splitContainer3.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.rightMouseButtonMenu.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.contextMenuStrip2.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 24);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.splitContainer2);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.listBox1);
            this.splitContainer1.Size = new System.Drawing.Size(1024, 552);
            this.splitContainer1.SplitterDistance = 698;
            this.splitContainer1.TabIndex = 0;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.graphEditor);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.splitContainer3);
            this.splitContainer2.Size = new System.Drawing.Size(698, 552);
            this.splitContainer2.SplitterDistance = 403;
            this.splitContainer2.TabIndex = 0;
            // 
            // graphEditor
            // 
            this.graphEditor.AllowDrop = true;
            this.graphEditor.BackColor = System.Drawing.Color.White;
            this.graphEditor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.graphEditor.GridFitText = false;
            this.graphEditor.Location = new System.Drawing.Point(0, 0);
            this.graphEditor.Name = "graphEditor";
            this.graphEditor.RegionManagement = true;
            this.graphEditor.Size = new System.Drawing.Size(698, 403);
            this.graphEditor.TabIndex = 1;
            this.graphEditor.Text = "graphEditor1";
            this.graphEditor.Click += new System.EventHandler(this.graphEditor_Click);
            this.graphEditor.DragDrop += new System.Windows.Forms.DragEventHandler(this.PathfindingEditor_DragDrop);
            this.graphEditor.DragEnter += new System.Windows.Forms.DragEventHandler(this.PathfindingEditor_DragEnter);
            // 
            // splitContainer3
            // 
            this.splitContainer3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer3.Location = new System.Drawing.Point(0, 0);
            this.splitContainer3.Name = "splitContainer3";
            // 
            // splitContainer3.Panel1
            // 
            this.splitContainer3.Panel1.Controls.Add(this.pg1);
            // 
            // splitContainer3.Panel2
            // 
            this.splitContainer3.Panel2.Controls.Add(this.interpreter1);
            this.splitContainer3.Size = new System.Drawing.Size(698, 145);
            this.splitContainer3.SplitterDistance = 346;
            this.splitContainer3.TabIndex = 0;
            // 
            // pg1
            // 
            this.pg1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pg1.CategoryForeColor = System.Drawing.SystemColors.InactiveCaptionText;
            this.pg1.HelpVisible = false;
            this.pg1.LineColor = System.Drawing.SystemColors.ControlDark;
            this.pg1.Location = new System.Drawing.Point(0, 0);
            this.pg1.Name = "pg1";
            this.pg1.Size = new System.Drawing.Size(343, 120);
            this.pg1.TabIndex = 0;
            this.pg1.PropertyValueChanged += new System.Windows.Forms.PropertyValueChangedEventHandler(this.pg1_PropertyValueChanged);
            // 
            // interpreter1
            // 
            this.interpreter1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.interpreter1.Location = new System.Drawing.Point(0, 0);
            this.interpreter1.Name = "interpreter1";
            this.interpreter1.Pcc = null;
            this.interpreter1.Size = new System.Drawing.Size(348, 145);
            this.interpreter1.TabIndex = 0;
            // 
            // listBox1
            // 
            this.listBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBox1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBox1.FormattingEnabled = true;
            this.listBox1.IntegralHeight = false;
            this.listBox1.ItemHeight = 16;
            this.listBox1.Location = new System.Drawing.Point(2, 0);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(320, 527);
            this.listBox1.TabIndex = 0;
            this.listBox1.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged);
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.optionsToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1024, 24);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.loadToolStripMenuItem,
            this.savePccToolStripMenuItem,
            this.savePCCAsMenuItem,
            this.saveViewToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // loadToolStripMenuItem
            // 
            this.loadToolStripMenuItem.Name = "loadToolStripMenuItem";
            this.loadToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.loadToolStripMenuItem.Text = "Load";
            this.loadToolStripMenuItem.Click += new System.EventHandler(this.loadToolStripMenuItem_Click);
            // 
            // savePccToolStripMenuItem
            // 
            this.savePccToolStripMenuItem.Name = "savePccToolStripMenuItem";
            this.savePccToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.savePccToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.savePccToolStripMenuItem.Text = "Save pcc";
            this.savePccToolStripMenuItem.Click += new System.EventHandler(this.savePccToolStripMenuItem_Click_1);
            // 
            // savePCCAsMenuItem
            // 
            this.savePCCAsMenuItem.Name = "savePCCAsMenuItem";
            this.savePCCAsMenuItem.Size = new System.Drawing.Size(160, 22);
            this.savePCCAsMenuItem.Text = "Save pcc As";
            this.savePCCAsMenuItem.Click += new System.EventHandler(this.savePCCAsMenuItem_Click);
            // 
            // saveViewToolStripMenuItem
            // 
            this.saveViewToolStripMenuItem.Name = "saveViewToolStripMenuItem";
            this.saveViewToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.saveViewToolStripMenuItem.Text = "Save Image";
            this.saveViewToolStripMenuItem.Click += new System.EventHandler(this.saveImageToolStripMenuItem_Click);
            // 
            // optionsToolStripMenuItem
            // 
            this.optionsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.togglePathfindingNodes,
            this.toggleActorNodes,
            this.staticMeshCollectionActorsToolStripMenuItem});
            this.optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            this.optionsToolStripMenuItem.Size = new System.Drawing.Size(95, 20);
            this.optionsToolStripMenuItem.Text = "Viewing Mode";
            // 
            // togglePathfindingNodes
            // 
            this.togglePathfindingNodes.Checked = true;
            this.togglePathfindingNodes.CheckOnClick = true;
            this.togglePathfindingNodes.CheckState = System.Windows.Forms.CheckState.Checked;
            this.togglePathfindingNodes.Name = "togglePathfindingNodes";
            this.togglePathfindingNodes.Size = new System.Drawing.Size(220, 22);
            this.togglePathfindingNodes.Text = "Pathfinding Nodes";
            this.togglePathfindingNodes.ToolTipText = "Toggle pathfinding nodes and reachspecs";
            this.togglePathfindingNodes.Click += new System.EventHandler(this.togglePathfindingNodes_Click);
            // 
            // toggleActorNodes
            // 
            this.toggleActorNodes.CheckOnClick = true;
            this.toggleActorNodes.Name = "toggleActorNodes";
            this.toggleActorNodes.Size = new System.Drawing.Size(220, 22);
            this.toggleActorNodes.Text = "Actor Nodes";
            this.toggleActorNodes.ToolTipText = "Toggle showing actors such as static meshes and blocking volumes.";
            this.toggleActorNodes.Click += new System.EventHandler(this.toggleActorNodes_Click);
            // 
            // staticMeshCollectionActorsToolStripMenuItem
            // 
            this.staticMeshCollectionActorsToolStripMenuItem.Name = "staticMeshCollectionActorsToolStripMenuItem";
            this.staticMeshCollectionActorsToolStripMenuItem.Size = new System.Drawing.Size(220, 22);
            this.staticMeshCollectionActorsToolStripMenuItem.Text = "StaticMeshCollectionActors";
            this.staticMeshCollectionActorsToolStripMenuItem.ToolTipText = "Locations of items in StaticMeshActorCollections. Enabling these options can lead to a significant de" +
    "crease in editor performance.";
            // 
            // toolStrip1
            // 
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripTextBox1,
            this.toolStripButton1});
            this.toolStrip1.Location = new System.Drawing.Point(0, 24);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(1024, 25);
            this.toolStrip1.TabIndex = 2;
            this.toolStrip1.Text = "toolStrip1";
            this.toolStrip1.Visible = false;
            // 
            // toolStripTextBox1
            // 
            this.toolStripTextBox1.Name = "toolStripTextBox1";
            this.toolStripTextBox1.Size = new System.Drawing.Size(100, 25);
            // 
            // toolStripButton1
            // 
            this.toolStripButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButton1.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton1.Image")));
            this.toolStripButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButton1.Name = "toolStripButton1";
            this.toolStripButton1.Size = new System.Drawing.Size(38, 22);
            this.toolStripButton1.Text = "Scale";
            this.toolStripButton1.Click += new System.EventHandler(this.toolStripButton1_Click);
            // 
            // rightMouseButtonMenu
            // 
            this.rightMouseButtonMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openInPackageEditorToolStripMenuItem,
            this.cloneToolStripMenuItem,
            this.breakLinksToolStripMenuItem,
            this.changeNodeTypeToolStripMenuItem,
            this.createReachSpecToolStripMenuItem,
            this.setGraphPositionAsNodeLocationToolStripMenuItem,
            this.generateNewRandomGUIDToolStripMenuItem});
            this.rightMouseButtonMenu.Name = "contextMenuStrip1";
            this.rightMouseButtonMenu.Size = new System.Drawing.Size(295, 158);
            // 
            // openInPackageEditorToolStripMenuItem
            // 
            this.openInPackageEditorToolStripMenuItem.Name = "openInPackageEditorToolStripMenuItem";
            this.openInPackageEditorToolStripMenuItem.Size = new System.Drawing.Size(294, 22);
            this.openInPackageEditorToolStripMenuItem.Text = "Open in Package Editor";
            this.openInPackageEditorToolStripMenuItem.Click += new System.EventHandler(this.openInPackageEditorToolStripMenuItem_Click);
            // 
            // cloneToolStripMenuItem
            // 
            this.cloneToolStripMenuItem.Name = "cloneToolStripMenuItem";
            this.cloneToolStripMenuItem.Size = new System.Drawing.Size(294, 22);
            this.cloneToolStripMenuItem.Text = "Clone Node";
            this.cloneToolStripMenuItem.Click += new System.EventHandler(this.cloneToolStripMenuItem_Click);
            // 
            // breakLinksToolStripMenuItem
            // 
            this.breakLinksToolStripMenuItem.Name = "breakLinksToolStripMenuItem";
            this.breakLinksToolStripMenuItem.Size = new System.Drawing.Size(294, 22);
            this.breakLinksToolStripMenuItem.Text = "Remove ReachSpecs";
            // 
            // changeNodeTypeToolStripMenuItem
            // 
            this.changeNodeTypeToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toSFXEnemySpawnPointToolStripMenuItem,
            this.toPathNodeToolStripMenuItem,
            this.toSFXNavTurretPointToolStripMenuItem,
            this.toSFXNavBoostNodeToolStripMenuItem});
            this.changeNodeTypeToolStripMenuItem.Name = "changeNodeTypeToolStripMenuItem";
            this.changeNodeTypeToolStripMenuItem.Size = new System.Drawing.Size(294, 22);
            this.changeNodeTypeToolStripMenuItem.Text = "Change Node Type";
            // 
            // toSFXEnemySpawnPointToolStripMenuItem
            // 
            this.toSFXEnemySpawnPointToolStripMenuItem.Name = "toSFXEnemySpawnPointToolStripMenuItem";
            this.toSFXEnemySpawnPointToolStripMenuItem.Size = new System.Drawing.Size(208, 22);
            this.toSFXEnemySpawnPointToolStripMenuItem.Text = "To SFXEnemySpawnPoint";
            this.toSFXEnemySpawnPointToolStripMenuItem.Click += new System.EventHandler(this.toSFXEnemySpawnPointToolStripMenuItem_Click);
            // 
            // toPathNodeToolStripMenuItem
            // 
            this.toPathNodeToolStripMenuItem.Name = "toPathNodeToolStripMenuItem";
            this.toPathNodeToolStripMenuItem.Size = new System.Drawing.Size(208, 22);
            this.toPathNodeToolStripMenuItem.Text = "To PathNode";
            this.toPathNodeToolStripMenuItem.Click += new System.EventHandler(this.toPathNodeToolStripMenuItem_Click);
            // 
            // toSFXNavTurretPointToolStripMenuItem
            // 
            this.toSFXNavTurretPointToolStripMenuItem.Name = "toSFXNavTurretPointToolStripMenuItem";
            this.toSFXNavTurretPointToolStripMenuItem.Size = new System.Drawing.Size(208, 22);
            this.toSFXNavTurretPointToolStripMenuItem.Text = "To SFXNav_TurretPoint";
            this.toSFXNavTurretPointToolStripMenuItem.Click += new System.EventHandler(this.toSFXNavTurretPointToolStripMenuItem_Click);
            // 
            // toSFXNavBoostNodeToolStripMenuItem
            // 
            this.toSFXNavBoostNodeToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.sFXNavBoostNodeTopToolStripMenuItem,
            this.sFXNavBoostNodeBottomToolStripMenuItem});
            this.toSFXNavBoostNodeToolStripMenuItem.Name = "toSFXNavBoostNodeToolStripMenuItem";
            this.toSFXNavBoostNodeToolStripMenuItem.Size = new System.Drawing.Size(208, 22);
            this.toSFXNavBoostNodeToolStripMenuItem.Text = "To SFXNav_BoostNode";
            // 
            // sFXNavBoostNodeTopToolStripMenuItem
            // 
            this.sFXNavBoostNodeTopToolStripMenuItem.Name = "sFXNavBoostNodeTopToolStripMenuItem";
            this.sFXNavBoostNodeTopToolStripMenuItem.Size = new System.Drawing.Size(229, 22);
            this.sFXNavBoostNodeTopToolStripMenuItem.Text = "SFXNav_BoostNode (Top)";
            this.sFXNavBoostNodeTopToolStripMenuItem.Click += new System.EventHandler(this.sFXNavBoostNodeTopToolStripMenuItem_Click);
            // 
            // sFXNavBoostNodeBottomToolStripMenuItem
            // 
            this.sFXNavBoostNodeBottomToolStripMenuItem.Name = "sFXNavBoostNodeBottomToolStripMenuItem";
            this.sFXNavBoostNodeBottomToolStripMenuItem.Size = new System.Drawing.Size(229, 22);
            this.sFXNavBoostNodeBottomToolStripMenuItem.Text = "SFXNav_BoostNode (Bottom)";
            this.sFXNavBoostNodeBottomToolStripMenuItem.Click += new System.EventHandler(this.sFXNavBoostNodeBottomToolStripMenuItem_Click);
            // 
            // createReachSpecToolStripMenuItem
            // 
            this.createReachSpecToolStripMenuItem.Name = "createReachSpecToolStripMenuItem";
            this.createReachSpecToolStripMenuItem.Size = new System.Drawing.Size(294, 22);
            this.createReachSpecToolStripMenuItem.Text = "Create ReachSpec";
            this.createReachSpecToolStripMenuItem.Click += new System.EventHandler(this.createReachSpecToolStripMenuItem_Click);
            // 
            // setGraphPositionAsNodeLocationToolStripMenuItem
            // 
            this.setGraphPositionAsNodeLocationToolStripMenuItem.Name = "setGraphPositionAsNodeLocationToolStripMenuItem";
            this.setGraphPositionAsNodeLocationToolStripMenuItem.Size = new System.Drawing.Size(294, 22);
            this.setGraphPositionAsNodeLocationToolStripMenuItem.Text = "Set Graph Position as Node Location (X,Y)";
            this.setGraphPositionAsNodeLocationToolStripMenuItem.Click += new System.EventHandler(this.setGraphPositionAsNodeLocationToolStripMenuItem_Click);
            // 
            // generateNewRandomGUIDToolStripMenuItem
            // 
            this.generateNewRandomGUIDToolStripMenuItem.Name = "generateNewRandomGUIDToolStripMenuItem";
            this.generateNewRandomGUIDToolStripMenuItem.Size = new System.Drawing.Size(294, 22);
            this.generateNewRandomGUIDToolStripMenuItem.Text = "Generate new random NavGUID";
            this.generateNewRandomGUIDToolStripMenuItem.Click += new System.EventHandler(this.generateNewRandomGUIDToolStripMenuItem_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.filenameLabel,
            this.toolStripStatusLabel2});
            this.statusStrip1.Location = new System.Drawing.Point(0, 554);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(1024, 22);
            this.statusStrip1.TabIndex = 3;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // filenameLabel
            // 
            this.filenameLabel.ForeColor = System.Drawing.Color.MediumBlue;
            this.filenameLabel.Name = "filenameLabel";
            this.filenameLabel.Size = new System.Drawing.Size(10, 17);
            this.filenameLabel.Text = " ";
            // 
            // toolStripStatusLabel2
            // 
            this.toolStripStatusLabel2.ForeColor = System.Drawing.SystemColors.ControlText;
            this.toolStripStatusLabel2.Name = "toolStripStatusLabel2";
            this.toolStripStatusLabel2.Size = new System.Drawing.Size(10, 17);
            this.toolStripStatusLabel2.Text = " ";
            // 
            // contextMenuStrip2
            // 
            this.contextMenuStrip2.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addObjectToolStripMenuItem});
            this.contextMenuStrip2.Name = "contextMenuStrip2";
            this.contextMenuStrip2.Size = new System.Drawing.Size(135, 26);
            // 
            // addObjectToolStripMenuItem
            // 
            this.addObjectToolStripMenuItem.Name = "addObjectToolStripMenuItem";
            this.addObjectToolStripMenuItem.Size = new System.Drawing.Size(134, 22);
            this.addObjectToolStripMenuItem.Text = "Add Object";
            // 
            // PathfindingEditor
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1024, 576);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "PathfindingEditor";
            this.Text = "Pathfinding Editor";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.PathfindingEditor_FormClosing);
            this.Load += new System.EventHandler(this.PathfindingEditor_Load);
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.PathfindingEditor_DragDrop);
            this.DragEnter += new System.Windows.Forms.DragEventHandler(this.PathfindingEditor_DragEnter);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.splitContainer3.Panel1.ResumeLayout(false);
            this.splitContainer3.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).EndInit();
            this.splitContainer3.ResumeLayout(false);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.rightMouseButtonMenu.ResumeLayout(false);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.contextMenuStrip2.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private UMD.HCIL.PathingGraphEditor.PathingGraphEditor graphEditor;
        private System.Windows.Forms.SplitContainer splitContainer3;
        private System.Windows.Forms.PropertyGrid pg1;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loadToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveViewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem savePccToolStripMenuItem;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripTextBox toolStripTextBox1;
        private System.Windows.Forms.ToolStripButton toolStripButton1;
        private System.Windows.Forms.ContextMenuStrip rightMouseButtonMenu;
        private System.Windows.Forms.ToolStripMenuItem openInPackageEditorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem breakLinksToolStripMenuItem;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel filenameLabel;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel2;
        private System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem togglePathfindingNodes;
        private System.Windows.Forms.ToolStripMenuItem toggleActorNodes;
        private System.Windows.Forms.ToolStripMenuItem savePCCAsMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip2;
        private System.Windows.Forms.ToolStripMenuItem addObjectToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem cloneToolStripMenuItem;
        private Interpreter interpreter1;
        private System.Windows.Forms.ToolStripMenuItem changeNodeTypeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toSFXEnemySpawnPointToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toPathNodeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toSFXNavTurretPointToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createReachSpecToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem setGraphPositionAsNodeLocationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toSFXNavBoostNodeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sFXNavBoostNodeTopToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sFXNavBoostNodeBottomToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem generateNewRandomGUIDToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem staticMeshCollectionActorsToolStripMenuItem;
    }
}