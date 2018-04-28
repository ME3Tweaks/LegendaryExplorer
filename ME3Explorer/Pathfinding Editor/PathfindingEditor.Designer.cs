using ME3Explorer.Pathfinding_Editor;

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
            this.activeExportsListbox = new System.Windows.Forms.ListBox();
            this.interpreter1 = new ME3Explorer.Interpreter();
            this.pathfindingNodeInfoPanel = new ME3Explorer.Pathfinding_Editor.PathfindingNodeInfoPanel();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.savePccToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.savePCCAsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveViewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.recentToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.refreshToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.filterByZToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.nodesPropertiesPanelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.togglePathfindingNodes = new System.Windows.Forms.ToolStripMenuItem();
            this.toggleActorNodes = new System.Windows.Forms.ToolStripMenuItem();
            this.splinesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.staticMeshCollectionActorsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sFXCombatZonesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.recalculateReachspecsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fixStackHeadersToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.validateReachToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.relinkingPathfindingChainButton = new System.Windows.Forms.ToolStripMenuItem();
            this.flipLevelUpsidedownEXPERIMENTALToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.gotoNode_TextBox = new System.Windows.Forms.ToolStripTextBox();
            this.gotoNodeButton = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripTextBox1 = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripButton1 = new System.Windows.Forms.ToolStripButton();
            this.rightMouseButtonMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.openInPackageEditorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openInCurveEditorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cloneToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.breakLinksToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.changeNodeTypeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toBioPathPointToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toSFXNavTurretPointToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toSFXEnemySpawnPointToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toSFXDynamicCoverLinkToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toSFXDynamicCoverSlotMarkerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toSFXNavBoostNodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sFXNavBoostNodeTopToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sFXNavBoostNodeBottomToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toSFXNavClimbWallNodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toPathNodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toSFXNavLargeBoostNodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toSFXNavLargeMantleNodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createReachSpecToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.setGraphPositionAsNodeLocationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.setGraphPositionAsSplineLocationXYToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.generateNewRandomGUIDToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addToSFXCombatZoneToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.splitContainer1.Location = new System.Drawing.Point(0, 27);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.splitContainer2);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.pathfindingNodeInfoPanel);
            this.splitContainer1.Size = new System.Drawing.Size(1211, 521);
            this.splitContainer1.SplitterDistance = 825;
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
            this.splitContainer2.Panel2Collapsed = true;
            this.splitContainer2.Size = new System.Drawing.Size(825, 521);
            this.splitContainer2.SplitterDistance = 379;
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
            this.graphEditor.Size = new System.Drawing.Size(825, 521);
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
            this.splitContainer3.Panel1.Controls.Add(this.activeExportsListbox);
            // 
            // splitContainer3.Panel2
            // 
            this.splitContainer3.Panel2.Controls.Add(this.interpreter1);
            this.splitContainer3.Size = new System.Drawing.Size(150, 46);
            this.splitContainer3.SplitterDistance = 74;
            this.splitContainer3.TabIndex = 0;
            // 
            // activeExportsListbox
            // 
            this.activeExportsListbox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.activeExportsListbox.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.activeExportsListbox.FormattingEnabled = true;
            this.activeExportsListbox.IntegralHeight = false;
            this.activeExportsListbox.ItemHeight = 16;
            this.activeExportsListbox.Location = new System.Drawing.Point(0, 0);
            this.activeExportsListbox.Margin = new System.Windows.Forms.Padding(3, 3, 3, 1000);
            this.activeExportsListbox.Name = "activeExportsListbox";
            this.activeExportsListbox.Size = new System.Drawing.Size(74, 22);
            this.activeExportsListbox.TabIndex = 0;
            this.activeExportsListbox.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged);
            // 
            // interpreter1
            // 
            this.interpreter1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.interpreter1.Location = new System.Drawing.Point(0, 0);
            this.interpreter1.Name = "interpreter1";
            this.interpreter1.Pcc = null;
            this.interpreter1.Size = new System.Drawing.Size(68, 22);
            this.interpreter1.TabIndex = 0;
            // 
            // pathfindingNodeInfoPanel
            // 
            this.pathfindingNodeInfoPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pathfindingNodeInfoPanel.Location = new System.Drawing.Point(0, 0);
            this.pathfindingNodeInfoPanel.Margin = new System.Windows.Forms.Padding(6);
            this.pathfindingNodeInfoPanel.Name = "pathfindingNodeInfoPanel";
            this.pathfindingNodeInfoPanel.Size = new System.Drawing.Size(382, 521);
            this.pathfindingNodeInfoPanel.TabIndex = 0;
            // 
            // menuStrip1
            // 
            this.menuStrip1.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.optionsToolStripMenuItem,
            this.toolsToolStripMenuItem,
            this.gotoNode_TextBox,
            this.gotoNodeButton});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1211, 27);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.loadToolStripMenuItem,
            this.savePccToolStripMenuItem,
            this.savePCCAsMenuItem,
            this.saveViewToolStripMenuItem,
            this.toolStripSeparator2,
            this.recentToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 23);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // loadToolStripMenuItem
            // 
            this.loadToolStripMenuItem.Name = "loadToolStripMenuItem";
            this.loadToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this.loadToolStripMenuItem.Size = new System.Drawing.Size(173, 22);
            this.loadToolStripMenuItem.Text = "Load";
            this.loadToolStripMenuItem.Click += new System.EventHandler(this.loadToolStripMenuItem_Click);
            // 
            // savePccToolStripMenuItem
            // 
            this.savePccToolStripMenuItem.Name = "savePccToolStripMenuItem";
            this.savePccToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.savePccToolStripMenuItem.Size = new System.Drawing.Size(173, 22);
            this.savePccToolStripMenuItem.Text = "Save pcc";
            this.savePccToolStripMenuItem.Click += new System.EventHandler(this.savePccToolStripMenuItem_Click_1);
            // 
            // savePCCAsMenuItem
            // 
            this.savePCCAsMenuItem.Name = "savePCCAsMenuItem";
            this.savePCCAsMenuItem.Size = new System.Drawing.Size(173, 22);
            this.savePCCAsMenuItem.Text = "Save pcc As";
            this.savePCCAsMenuItem.Click += new System.EventHandler(this.savePCCAsMenuItem_Click);
            // 
            // saveViewToolStripMenuItem
            // 
            this.saveViewToolStripMenuItem.Name = "saveViewToolStripMenuItem";
            this.saveViewToolStripMenuItem.Size = new System.Drawing.Size(173, 22);
            this.saveViewToolStripMenuItem.Text = "Save graph as PNG";
            this.saveViewToolStripMenuItem.Click += new System.EventHandler(this.saveImageToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(170, 6);
            // 
            // recentToolStripMenuItem
            // 
            this.recentToolStripMenuItem.Name = "recentToolStripMenuItem";
            this.recentToolStripMenuItem.Size = new System.Drawing.Size(173, 22);
            this.recentToolStripMenuItem.Text = "Recent";
            // 
            // optionsToolStripMenuItem
            // 
            this.optionsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.refreshToolStripMenuItem,
            this.filterByZToolStripMenuItem,
            this.nodesPropertiesPanelToolStripMenuItem,
            this.toolStripSeparator1,
            this.togglePathfindingNodes,
            this.toggleActorNodes,
            this.splinesToolStripMenuItem,
            this.staticMeshCollectionActorsToolStripMenuItem,
            this.sFXCombatZonesToolStripMenuItem});
            this.optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            this.optionsToolStripMenuItem.Size = new System.Drawing.Size(95, 23);
            this.optionsToolStripMenuItem.Text = "Viewing Mode";
            // 
            // refreshToolStripMenuItem
            // 
            this.refreshToolStripMenuItem.Name = "refreshToolStripMenuItem";
            this.refreshToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F5;
            this.refreshToolStripMenuItem.Size = new System.Drawing.Size(279, 22);
            this.refreshToolStripMenuItem.Text = "Refresh";
            this.refreshToolStripMenuItem.ToolTipText = "Reloads the visible layers and resets objects to their listed position.";
            this.refreshToolStripMenuItem.Click += new System.EventHandler(this.refreshToolStripMenuItem_Click);
            // 
            // filterByZToolStripMenuItem
            // 
            this.filterByZToolStripMenuItem.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.filterByZToolStripMenuItem.Name = "filterByZToolStripMenuItem";
            this.filterByZToolStripMenuItem.Size = new System.Drawing.Size(279, 22);
            this.filterByZToolStripMenuItem.Text = "Filter by Z";
            this.filterByZToolStripMenuItem.ToolTipText = "Filters visible nodes above or below a Z value.";
            this.filterByZToolStripMenuItem.Click += new System.EventHandler(this.filterByZToolStripMenuItem_Click);
            // 
            // nodesPropertiesPanelToolStripMenuItem
            // 
            this.nodesPropertiesPanelToolStripMenuItem.Name = "nodesPropertiesPanelToolStripMenuItem";
            this.nodesPropertiesPanelToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.P)));
            this.nodesPropertiesPanelToolStripMenuItem.Size = new System.Drawing.Size(279, 22);
            this.nodesPropertiesPanelToolStripMenuItem.Text = "Toggle Nodes/Properties Panel";
            this.nodesPropertiesPanelToolStripMenuItem.Click += new System.EventHandler(this.nodesPropertiesPanelToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(276, 6);
            // 
            // togglePathfindingNodes
            // 
            this.togglePathfindingNodes.Checked = true;
            this.togglePathfindingNodes.CheckOnClick = true;
            this.togglePathfindingNodes.CheckState = System.Windows.Forms.CheckState.Checked;
            this.togglePathfindingNodes.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.togglePathfindingNodes.Name = "togglePathfindingNodes";
            this.togglePathfindingNodes.ShortcutKeys = System.Windows.Forms.Keys.F1;
            this.togglePathfindingNodes.Size = new System.Drawing.Size(279, 22);
            this.togglePathfindingNodes.Text = "Pathfinding Nodes";
            this.togglePathfindingNodes.ToolTipText = "Toggle pathfinding nodes and reachspecs";
            this.togglePathfindingNodes.Click += new System.EventHandler(this.togglePathfindingNodes_Click);
            // 
            // toggleActorNodes
            // 
            this.toggleActorNodes.CheckOnClick = true;
            this.toggleActorNodes.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.toggleActorNodes.Name = "toggleActorNodes";
            this.toggleActorNodes.ShortcutKeys = System.Windows.Forms.Keys.F2;
            this.toggleActorNodes.Size = new System.Drawing.Size(279, 22);
            this.toggleActorNodes.Text = "Actor Nodes";
            this.toggleActorNodes.ToolTipText = "Toggle showing actors such as static meshes and blocking volumes.";
            this.toggleActorNodes.Click += new System.EventHandler(this.toggleActorNodes_Click);
            // 
            // splinesToolStripMenuItem
            // 
            this.splinesToolStripMenuItem.CheckOnClick = true;
            this.splinesToolStripMenuItem.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.splinesToolStripMenuItem.Name = "splinesToolStripMenuItem";
            this.splinesToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F3;
            this.splinesToolStripMenuItem.Size = new System.Drawing.Size(279, 22);
            this.splinesToolStripMenuItem.Text = "Splines";
            this.splinesToolStripMenuItem.ToolTipText = "Toggle showing spline actors and their splinecomponents";
            this.splinesToolStripMenuItem.Click += new System.EventHandler(this.splinesToolStripMenuItem_Click);
            // 
            // staticMeshCollectionActorsToolStripMenuItem
            // 
            this.staticMeshCollectionActorsToolStripMenuItem.Enabled = false;
            this.staticMeshCollectionActorsToolStripMenuItem.Name = "staticMeshCollectionActorsToolStripMenuItem";
            this.staticMeshCollectionActorsToolStripMenuItem.Size = new System.Drawing.Size(279, 22);
            this.staticMeshCollectionActorsToolStripMenuItem.Text = "StaticMeshCollectionActors";
            this.staticMeshCollectionActorsToolStripMenuItem.ToolTipText = "Locations of items in StaticMeshActorCollections. Enabling these options can lead" +
    " to a significant decrease in editor performance.";
            // 
            // sFXCombatZonesToolStripMenuItem
            // 
            this.sFXCombatZonesToolStripMenuItem.Enabled = false;
            this.sFXCombatZonesToolStripMenuItem.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.sFXCombatZonesToolStripMenuItem.Name = "sFXCombatZonesToolStripMenuItem";
            this.sFXCombatZonesToolStripMenuItem.Size = new System.Drawing.Size(279, 22);
            this.sFXCombatZonesToolStripMenuItem.Text = "SFXCombatZones";
            // 
            // toolsToolStripMenuItem
            // 
            this.toolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.recalculateReachspecsToolStripMenuItem,
            this.fixStackHeadersToolStripMenuItem,
            this.validateReachToolStripMenuItem,
            this.relinkingPathfindingChainButton,
            this.flipLevelUpsidedownEXPERIMENTALToolStripMenuItem});
            this.toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            this.toolsToolStripMenuItem.Size = new System.Drawing.Size(48, 23);
            this.toolsToolStripMenuItem.Text = "Tools";
            // 
            // recalculateReachspecsToolStripMenuItem
            // 
            this.recalculateReachspecsToolStripMenuItem.Name = "recalculateReachspecsToolStripMenuItem";
            this.recalculateReachspecsToolStripMenuItem.Size = new System.Drawing.Size(286, 22);
            this.recalculateReachspecsToolStripMenuItem.Text = "Recalculate Reachspecs";
            this.recalculateReachspecsToolStripMenuItem.ToolTipText = "Recalculates the distances and directions of reachspecs so AI can properly naviga" +
    "te the pathing network";
            this.recalculateReachspecsToolStripMenuItem.Click += new System.EventHandler(this.recalculateReachspecsToolStripMenuItem_Click);
            // 
            // fixStackHeadersToolStripMenuItem
            // 
            this.fixStackHeadersToolStripMenuItem.Name = "fixStackHeadersToolStripMenuItem";
            this.fixStackHeadersToolStripMenuItem.Size = new System.Drawing.Size(286, 22);
            this.fixStackHeadersToolStripMenuItem.Text = "Fix Stack Headers";
            this.fixStackHeadersToolStripMenuItem.ToolTipText = "Exports that exist in the level and have a stack should have their first 8 bytes " +
    "point to their class (2x). This will check all level items for this and set them" +
    " appropriately.";
            this.fixStackHeadersToolStripMenuItem.Click += new System.EventHandler(this.fixStackHeadersToolStripMenuItem_Click);
            // 
            // validateReachToolStripMenuItem
            // 
            this.validateReachToolStripMenuItem.Name = "validateReachToolStripMenuItem";
            this.validateReachToolStripMenuItem.Size = new System.Drawing.Size(286, 22);
            this.validateReachToolStripMenuItem.Text = "Fix Duplicate Nav GUIDs";
            this.validateReachToolStripMenuItem.ToolTipText = "Finds navigation points with duplicate nav GUIDs and will offer to fix them by ge" +
    "nerating new ones.";
            this.validateReachToolStripMenuItem.Click += new System.EventHandler(this.validateReachToolStripMenuItem_Click);
            // 
            // relinkingPathfindingChainButton
            // 
            this.relinkingPathfindingChainButton.Name = "relinkingPathfindingChainButton";
            this.relinkingPathfindingChainButton.Size = new System.Drawing.Size(286, 22);
            this.relinkingPathfindingChainButton.Text = "Relink Pathfinding Chain";
            this.relinkingPathfindingChainButton.ToolTipText = "Generates a new pathfinding chain by making each node reference at least one othe" +
    "r so all nodes are referenced.";
            this.relinkingPathfindingChainButton.Click += new System.EventHandler(this.relinkPathfinding_ButtonClicked);
            // 
            // flipLevelUpsidedownEXPERIMENTALToolStripMenuItem
            // 
            this.flipLevelUpsidedownEXPERIMENTALToolStripMenuItem.Name = "flipLevelUpsidedownEXPERIMENTALToolStripMenuItem";
            this.flipLevelUpsidedownEXPERIMENTALToolStripMenuItem.Size = new System.Drawing.Size(286, 22);
            this.flipLevelUpsidedownEXPERIMENTALToolStripMenuItem.Text = "Flip Level Upsidedown (EXPERIMENTAL)";
            this.flipLevelUpsidedownEXPERIMENTALToolStripMenuItem.ToolTipText = "Was an experiment that inverts all items around the origin point and inverts thei" +
    "r size. Flips level upside down, but the textures are not displayed properly as " +
    "they are 1 sided.";
            this.flipLevelUpsidedownEXPERIMENTALToolStripMenuItem.Click += new System.EventHandler(this.flipLevelUpsidedownEXPERIMENTALToolStripMenuItem_Click);
            // 
            // gotoNode_TextBox
            // 
            this.gotoNode_TextBox.Name = "gotoNode_TextBox";
            this.gotoNode_TextBox.Size = new System.Drawing.Size(100, 23);
            this.gotoNode_TextBox.ToolTipText = "Enter an export # here and press enter to highlight it";
            this.gotoNode_TextBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.gotoField_KeyPressed);
            // 
            // gotoNodeButton
            // 
            this.gotoNodeButton.Name = "gotoNodeButton";
            this.gotoNodeButton.Size = new System.Drawing.Size(77, 23);
            this.gotoNodeButton.Text = "Goto Node";
            this.gotoNodeButton.Click += new System.EventHandler(this.gotoButton_Clicked);
            // 
            // toolStrip1
            // 
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(32, 32);
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
            this.rightMouseButtonMenu.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.rightMouseButtonMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openInPackageEditorToolStripMenuItem,
            this.openInCurveEditorToolStripMenuItem,
            this.cloneToolStripMenuItem,
            this.breakLinksToolStripMenuItem,
            this.changeNodeTypeToolStripMenuItem,
            this.createReachSpecToolStripMenuItem,
            this.setGraphPositionAsNodeLocationToolStripMenuItem,
            this.setGraphPositionAsSplineLocationXYToolStripMenuItem,
            this.generateNewRandomGUIDToolStripMenuItem,
            this.addToSFXCombatZoneToolStripMenuItem});
            this.rightMouseButtonMenu.Name = "contextMenuStrip1";
            this.rightMouseButtonMenu.Size = new System.Drawing.Size(298, 224);
            // 
            // openInPackageEditorToolStripMenuItem
            // 
            this.openInPackageEditorToolStripMenuItem.Name = "openInPackageEditorToolStripMenuItem";
            this.openInPackageEditorToolStripMenuItem.Size = new System.Drawing.Size(297, 22);
            this.openInPackageEditorToolStripMenuItem.Text = "Open in Package Editor";
            this.openInPackageEditorToolStripMenuItem.Click += new System.EventHandler(this.openInPackageEditorToolStripMenuItem_Click);
            // 
            // openInCurveEditorToolStripMenuItem
            // 
            this.openInCurveEditorToolStripMenuItem.Name = "openInCurveEditorToolStripMenuItem";
            this.openInCurveEditorToolStripMenuItem.Size = new System.Drawing.Size(297, 22);
            this.openInCurveEditorToolStripMenuItem.Text = "Open in Curve Editor";
            this.openInCurveEditorToolStripMenuItem.Click += new System.EventHandler(this.openInCurveEditorToolStripMenuItem_Click);
            // 
            // cloneToolStripMenuItem
            // 
            this.cloneToolStripMenuItem.Name = "cloneToolStripMenuItem";
            this.cloneToolStripMenuItem.Size = new System.Drawing.Size(297, 22);
            this.cloneToolStripMenuItem.Text = "Clone Node";
            this.cloneToolStripMenuItem.Click += new System.EventHandler(this.cloneToolStripMenuItem_Click);
            // 
            // breakLinksToolStripMenuItem
            // 
            this.breakLinksToolStripMenuItem.Name = "breakLinksToolStripMenuItem";
            this.breakLinksToolStripMenuItem.Size = new System.Drawing.Size(297, 22);
            this.breakLinksToolStripMenuItem.Text = "Remove ReachSpecs";
            // 
            // changeNodeTypeToolStripMenuItem
            // 
            this.changeNodeTypeToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toBioPathPointToolStripMenuItem,
            this.toSFXNavTurretPointToolStripMenuItem,
            this.toSFXEnemySpawnPointToolStripMenuItem,
            this.toSFXDynamicCoverLinkToolStripMenuItem,
            this.toSFXDynamicCoverSlotMarkerToolStripMenuItem,
            this.toSFXNavBoostNodeToolStripMenuItem,
            this.toSFXNavClimbWallNodeToolStripMenuItem,
            this.toPathNodeToolStripMenuItem,
            this.toSFXNavLargeBoostNodeToolStripMenuItem,
            this.toSFXNavLargeMantleNodeToolStripMenuItem});
            this.changeNodeTypeToolStripMenuItem.Name = "changeNodeTypeToolStripMenuItem";
            this.changeNodeTypeToolStripMenuItem.Size = new System.Drawing.Size(297, 22);
            this.changeNodeTypeToolStripMenuItem.Text = "Change Node Type";
            // 
            // toBioPathPointToolStripMenuItem
            // 
            this.toBioPathPointToolStripMenuItem.Name = "toBioPathPointToolStripMenuItem";
            this.toBioPathPointToolStripMenuItem.Size = new System.Drawing.Size(245, 22);
            this.toBioPathPointToolStripMenuItem.Text = "To BioPathPoint";
            this.toBioPathPointToolStripMenuItem.Click += new System.EventHandler(this.toBioPathPointToolStripMenuItem_Click);
            // 
            // toSFXNavTurretPointToolStripMenuItem
            // 
            this.toSFXNavTurretPointToolStripMenuItem.Name = "toSFXNavTurretPointToolStripMenuItem";
            this.toSFXNavTurretPointToolStripMenuItem.Size = new System.Drawing.Size(245, 22);
            this.toSFXNavTurretPointToolStripMenuItem.Text = "To PathNode";
            this.toSFXNavTurretPointToolStripMenuItem.ToolTipText = "Use by Engineers, Geth Primes, and Collector Troopers to put down their placeable" +
    "/pawns.";
            this.toSFXNavTurretPointToolStripMenuItem.Click += new System.EventHandler(this.toSFXNavTurretPointToolStripMenuItem_Click);
            // 
            // toSFXEnemySpawnPointToolStripMenuItem
            // 
            this.toSFXEnemySpawnPointToolStripMenuItem.Name = "toSFXEnemySpawnPointToolStripMenuItem";
            this.toSFXEnemySpawnPointToolStripMenuItem.Size = new System.Drawing.Size(245, 22);
            this.toSFXEnemySpawnPointToolStripMenuItem.Text = "To SFXEnemySpawnPoint";
            this.toSFXEnemySpawnPointToolStripMenuItem.ToolTipText = "Spawnpoint for an enemy group in MP. Ensure it has enough radial space for a grou" +
    "p. Use SupportedReachSpec prop to limit what can spawn here.";
            this.toSFXEnemySpawnPointToolStripMenuItem.Click += new System.EventHandler(this.toSFXEnemySpawnPointToolStripMenuItem_Click);
            // 
            // toSFXDynamicCoverLinkToolStripMenuItem
            // 
            this.toSFXDynamicCoverLinkToolStripMenuItem.Name = "toSFXDynamicCoverLinkToolStripMenuItem";
            this.toSFXDynamicCoverLinkToolStripMenuItem.Size = new System.Drawing.Size(245, 22);
            this.toSFXDynamicCoverLinkToolStripMenuItem.Text = "To SFXDynamicCoverLink";
            this.toSFXDynamicCoverLinkToolStripMenuItem.Click += new System.EventHandler(this.toSFXDynamicCoverLinkToolStripMenuItem_Click);
            // 
            // toSFXDynamicCoverSlotMarkerToolStripMenuItem
            // 
            this.toSFXDynamicCoverSlotMarkerToolStripMenuItem.Name = "toSFXDynamicCoverSlotMarkerToolStripMenuItem";
            this.toSFXDynamicCoverSlotMarkerToolStripMenuItem.Size = new System.Drawing.Size(245, 22);
            this.toSFXDynamicCoverSlotMarkerToolStripMenuItem.Text = "To SFXDynamicCoverSlotMarker";
            this.toSFXDynamicCoverSlotMarkerToolStripMenuItem.Click += new System.EventHandler(this.toSFXDynamicCoverSlotMarkerToolStripMenuItem_Click);
            // 
            // toSFXNavBoostNodeToolStripMenuItem
            // 
            this.toSFXNavBoostNodeToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.sFXNavBoostNodeTopToolStripMenuItem,
            this.sFXNavBoostNodeBottomToolStripMenuItem});
            this.toSFXNavBoostNodeToolStripMenuItem.Name = "toSFXNavBoostNodeToolStripMenuItem";
            this.toSFXNavBoostNodeToolStripMenuItem.Size = new System.Drawing.Size(245, 22);
            this.toSFXNavBoostNodeToolStripMenuItem.Text = "To SFXNav_BoostNode";
            this.toSFXNavBoostNodeToolStripMenuItem.ToolTipText = "Used by mooks to boost up and down vertically.";
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
            // toSFXNavClimbWallNodeToolStripMenuItem
            // 
            this.toSFXNavClimbWallNodeToolStripMenuItem.Name = "toSFXNavClimbWallNodeToolStripMenuItem";
            this.toSFXNavClimbWallNodeToolStripMenuItem.Size = new System.Drawing.Size(245, 22);
            this.toSFXNavClimbWallNodeToolStripMenuItem.Text = "To SFXNav_ClimbWallNode";
            this.toSFXNavClimbWallNodeToolStripMenuItem.Click += new System.EventHandler(this.toSFXNavClimbWallNodeToolStripMenuItem_Click);
            // 
            // toPathNodeToolStripMenuItem
            // 
            this.toPathNodeToolStripMenuItem.Name = "toPathNodeToolStripMenuItem";
            this.toPathNodeToolStripMenuItem.Size = new System.Drawing.Size(245, 22);
            this.toPathNodeToolStripMenuItem.Text = "To PathNode";
            this.toPathNodeToolStripMenuItem.ToolTipText = "Basic pathfinding node.";
            this.toPathNodeToolStripMenuItem.Click += new System.EventHandler(this.toPathNodeToolStripMenuItem_Click);
            // 
            // toSFXNavLargeBoostNodeToolStripMenuItem
            // 
            this.toSFXNavLargeBoostNodeToolStripMenuItem.Name = "toSFXNavLargeBoostNodeToolStripMenuItem";
            this.toSFXNavLargeBoostNodeToolStripMenuItem.Size = new System.Drawing.Size(245, 22);
            this.toSFXNavLargeBoostNodeToolStripMenuItem.Text = "To SFXNav_LargeBoostNode";
            this.toSFXNavLargeBoostNodeToolStripMenuItem.ToolTipText = "Used by Banshees to ascend and descend.";
            this.toSFXNavLargeBoostNodeToolStripMenuItem.Click += new System.EventHandler(this.toSFXNavLargeBoostNodeToolStripMenuItem_Click);
            // 
            // toSFXNavLargeMantleNodeToolStripMenuItem
            // 
            this.toSFXNavLargeMantleNodeToolStripMenuItem.Name = "toSFXNavLargeMantleNodeToolStripMenuItem";
            this.toSFXNavLargeMantleNodeToolStripMenuItem.Size = new System.Drawing.Size(245, 22);
            this.toSFXNavLargeMantleNodeToolStripMenuItem.Text = "To SFXNav_LargeMantleNode";
            this.toSFXNavLargeMantleNodeToolStripMenuItem.Click += new System.EventHandler(this.toSFXNavLargeMantleNodeToolStripMenuItem_Click);
            // 
            // createReachSpecToolStripMenuItem
            // 
            this.createReachSpecToolStripMenuItem.Name = "createReachSpecToolStripMenuItem";
            this.createReachSpecToolStripMenuItem.Size = new System.Drawing.Size(297, 22);
            this.createReachSpecToolStripMenuItem.Text = "Create ReachSpec";
            this.createReachSpecToolStripMenuItem.Click += new System.EventHandler(this.createReachSpecToolStripMenuItem_Click);
            // 
            // setGraphPositionAsNodeLocationToolStripMenuItem
            // 
            this.setGraphPositionAsNodeLocationToolStripMenuItem.Name = "setGraphPositionAsNodeLocationToolStripMenuItem";
            this.setGraphPositionAsNodeLocationToolStripMenuItem.Size = new System.Drawing.Size(297, 22);
            this.setGraphPositionAsNodeLocationToolStripMenuItem.Text = "Set Graph Position as Node Location (X,Y)";
            this.setGraphPositionAsNodeLocationToolStripMenuItem.Click += new System.EventHandler(this.setGraphPositionAsNodeLocationToolStripMenuItem_Click);
            // 
            // setGraphPositionAsSplineLocationXYToolStripMenuItem
            // 
            this.setGraphPositionAsSplineLocationXYToolStripMenuItem.Name = "setGraphPositionAsSplineLocationXYToolStripMenuItem";
            this.setGraphPositionAsSplineLocationXYToolStripMenuItem.Size = new System.Drawing.Size(297, 22);
            this.setGraphPositionAsSplineLocationXYToolStripMenuItem.Text = "Set Graph Position as Spline Location (X,Y)";
            this.setGraphPositionAsSplineLocationXYToolStripMenuItem.Click += new System.EventHandler(this.setGraphPositionAsSplineLocationXYToolStripMenuItem_Click);
            // 
            // generateNewRandomGUIDToolStripMenuItem
            // 
            this.generateNewRandomGUIDToolStripMenuItem.Name = "generateNewRandomGUIDToolStripMenuItem";
            this.generateNewRandomGUIDToolStripMenuItem.Size = new System.Drawing.Size(297, 22);
            this.generateNewRandomGUIDToolStripMenuItem.Text = "Generate new random NavGUID";
            this.generateNewRandomGUIDToolStripMenuItem.Click += new System.EventHandler(this.generateNewRandomGUIDToolStripMenuItem_Click);
            // 
            // addToSFXCombatZoneToolStripMenuItem
            // 
            this.addToSFXCombatZoneToolStripMenuItem.Name = "addToSFXCombatZoneToolStripMenuItem";
            this.addToSFXCombatZoneToolStripMenuItem.Size = new System.Drawing.Size(297, 22);
            this.addToSFXCombatZoneToolStripMenuItem.Text = "Add to SFXCombatZone";
            // 
            // statusStrip1
            // 
            this.statusStrip1.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.filenameLabel,
            this.toolStripStatusLabel2});
            this.statusStrip1.Location = new System.Drawing.Point(0, 526);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(1211, 22);
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
            this.contextMenuStrip2.ImageScalingSize = new System.Drawing.Size(32, 32);
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
            this.ClientSize = new System.Drawing.Size(1211, 548);
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
        private System.Windows.Forms.ListBox activeExportsListbox;
        private System.Windows.Forms.SplitContainer splitContainer2;
        public UMD.HCIL.PathingGraphEditor.PathingGraphEditor graphEditor;
        private System.Windows.Forms.SplitContainer splitContainer3;
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
        private PathfindingNodeInfoPanel pathfindingNodeInfoPanel;
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
        private System.Windows.Forms.ToolStripMenuItem sFXCombatZonesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addToSFXCombatZoneToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toSFXNavLargeBoostNodeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem filterByZToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem refreshToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toSFXNavLargeMantleNodeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toSFXNavClimbWallNodeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem recalculateReachspecsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fixStackHeadersToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem validateReachToolStripMenuItem;
        private System.Windows.Forms.ToolStripTextBox gotoNode_TextBox;
        private System.Windows.Forms.ToolStripMenuItem gotoNodeButton;
        private System.Windows.Forms.ToolStripMenuItem relinkingPathfindingChainButton;
        private System.Windows.Forms.ToolStripMenuItem toBioPathPointToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toSFXDynamicCoverLinkToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toSFXDynamicCoverSlotMarkerToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem flipLevelUpsidedownEXPERIMENTALToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem splinesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem setGraphPositionAsSplineLocationXYToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem recentToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openInCurveEditorToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem nodesPropertiesPanelToolStripMenuItem;
    }
}