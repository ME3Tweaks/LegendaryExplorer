namespace ME3Explorer
{
    partial class Texplorer2
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Texplorer2));
            this.StatusStrip = new System.Windows.Forms.ToolStrip();
            this.MainProgressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.StatusLabel = new System.Windows.Forms.ToolStripLabel();
            this.CancelButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripButton1 = new System.Windows.Forms.ToolStripLabel();
            this.toolStripLabel1 = new System.Windows.Forms.ToolStripSeparator();
            this.VersionLabel = new System.Windows.Forms.ToolStripLabel();
            this.MainMenuStrip = new System.Windows.Forms.MenuStrip();
            this.saveChangesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.instructionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.treeIOToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.changePathsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.startTPFModeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rebuildDatabaseToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.updateTOCsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.regenerateThumbnailsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addDLCToTreeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.MainTreeView = new System.Windows.Forms.TreeView();
            this.TreeImageList = new System.Windows.Forms.ImageList(this.components);
            this.splitContainer3 = new System.Windows.Forms.SplitContainer();
            this.ContextPanel = new System.Windows.Forms.Panel();
            this.LowResButton = new System.Windows.Forms.Button();
            this.NoRenderButton = new System.Windows.Forms.Button();
            this.RegenerateButton = new System.Windows.Forms.Button();
            this.UpscaleButton = new System.Windows.Forms.Button();
            this.ExtractButton = new System.Windows.Forms.Button();
            this.ReplaceButton = new System.Windows.Forms.Button();
            this.AddBiggerButton = new System.Windows.Forms.Button();
            this.MainListView = new System.Windows.Forms.ListView();
            this.ListViewImageList = new System.Windows.Forms.ImageList(this.components);
            this.PicturePanel = new System.Windows.Forms.Panel();
            this.MainPictureBox = new System.Windows.Forms.PictureBox();
            this.splitContainer4 = new System.Windows.Forms.SplitContainer();
            this.SearchBox = new System.Windows.Forms.TextBox();
            this.DetailsHideButton = new System.Windows.Forms.Button();
            this.SearchCountLabel = new System.Windows.Forms.Label();
            this.TabSearchSplitter = new System.Windows.Forms.SplitContainer();
            this.SearchListBox = new System.Windows.Forms.ListBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.PropertiesTab = new System.Windows.Forms.TabPage();
            this.PropertiesRTB = new System.Windows.Forms.RichTextBox();
            this.PCCsTab = new System.Windows.Forms.TabPage();
            this.PCCsCheckedListBox = new System.Windows.Forms.CheckedListBox();
            this.PCCBoxContext = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.OutputBox = new System.Windows.Forms.RichTextBox();
            this.ChangeButton = new System.Windows.Forms.Button();
            this.PrimaryToolTip = new System.Windows.Forms.ToolTip(this.components);
            this.Game3Label = new System.Windows.Forms.Label();
            this.Game2Label = new System.Windows.Forms.Label();
            this.Game1Label = new System.Windows.Forms.Label();
            this.GamesLabel = new System.Windows.Forms.Label();
            this.Tree3Label = new System.Windows.Forms.Label();
            this.Tree2Label = new System.Windows.Forms.Label();
            this.Tree1Label = new System.Windows.Forms.Label();
            this.TreeLabel = new System.Windows.Forms.Label();
            this.StatusStrip.SuspendLayout();
            this.MainMenuStrip.SuspendLayout();
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
            this.ContextPanel.SuspendLayout();
            this.PicturePanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MainPictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer4)).BeginInit();
            this.splitContainer4.Panel1.SuspendLayout();
            this.splitContainer4.Panel2.SuspendLayout();
            this.splitContainer4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.TabSearchSplitter)).BeginInit();
            this.TabSearchSplitter.Panel1.SuspendLayout();
            this.TabSearchSplitter.Panel2.SuspendLayout();
            this.TabSearchSplitter.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.PropertiesTab.SuspendLayout();
            this.PCCsTab.SuspendLayout();
            this.PCCBoxContext.SuspendLayout();
            this.SuspendLayout();
            // 
            // StatusStrip
            // 
            this.StatusStrip.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.StatusStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.StatusStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.StatusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.MainProgressBar,
            this.StatusLabel,
            this.CancelButton,
            this.toolStripButton1,
            this.toolStripLabel1,
            this.VersionLabel});
            this.StatusStrip.Location = new System.Drawing.Point(0, 439);
            this.StatusStrip.Name = "StatusStrip";
            this.StatusStrip.Padding = new System.Windows.Forms.Padding(0, 0, 2, 0);
            this.StatusStrip.Size = new System.Drawing.Size(1424, 37);
            this.StatusStrip.TabIndex = 0;
            this.StatusStrip.Text = "toolStrip1";
            // 
            // MainProgressBar
            // 
            this.MainProgressBar.Name = "MainProgressBar";
            this.MainProgressBar.Size = new System.Drawing.Size(150, 34);
            this.MainProgressBar.Step = 1;
            // 
            // StatusLabel
            // 
            this.StatusLabel.Name = "StatusLabel";
            this.StatusLabel.Size = new System.Drawing.Size(60, 34);
            this.StatusLabel.Text = "Ready";
            // 
            // CancelButton
            // 
            this.CancelButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.CancelButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.CancelButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.CancelButton.Name = "CancelButton";
            this.CancelButton.Size = new System.Drawing.Size(67, 34);
            this.CancelButton.Text = "Cancel";
            this.CancelButton.Visible = false;
            this.CancelButton.Click += new System.EventHandler(this.CancelButton_Click);
            // 
            // toolStripButton1
            // 
            this.toolStripButton1.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolStripButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButton1.Enabled = false;
            this.toolStripButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButton1.Name = "toolStripButton1";
            this.toolStripButton1.Size = new System.Drawing.Size(315, 34);
            this.toolStripButton1.Text = "WV, AK, Salt, Gibbed = The Real Heros";
            // 
            // toolStripLabel1
            // 
            this.toolStripLabel1.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolStripLabel1.Name = "toolStripLabel1";
            this.toolStripLabel1.Size = new System.Drawing.Size(6, 37);
            // 
            // VersionLabel
            // 
            this.VersionLabel.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.VersionLabel.Enabled = false;
            this.VersionLabel.Name = "VersionLabel";
            this.VersionLabel.Size = new System.Drawing.Size(74, 34);
            this.VersionLabel.Text = "Versiojn";
            // 
            // MainMenuStrip
            // 
            this.MainMenuStrip.BackColor = System.Drawing.Color.Transparent;
            this.MainMenuStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.MainMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.saveChangesToolStripMenuItem,
            this.instructionsToolStripMenuItem,
            this.treeIOToolStripMenuItem,
            this.changePathsToolStripMenuItem,
            this.startTPFModeToolStripMenuItem,
            this.rebuildDatabaseToolStripMenuItem,
            this.updateTOCsToolStripMenuItem,
            this.regenerateThumbnailsToolStripMenuItem,
            this.addDLCToTreeToolStripMenuItem});
            this.MainMenuStrip.Location = new System.Drawing.Point(0, 0);
            this.MainMenuStrip.Name = "MainMenuStrip";
            this.MainMenuStrip.Padding = new System.Windows.Forms.Padding(9, 3, 0, 3);
            this.MainMenuStrip.Size = new System.Drawing.Size(1424, 35);
            this.MainMenuStrip.TabIndex = 1;
            this.MainMenuStrip.Text = "menuStrip1";
            // 
            // saveChangesToolStripMenuItem
            // 
            this.saveChangesToolStripMenuItem.Name = "saveChangesToolStripMenuItem";
            this.saveChangesToolStripMenuItem.Size = new System.Drawing.Size(134, 29);
            this.saveChangesToolStripMenuItem.Text = "Save Changes";
            this.saveChangesToolStripMenuItem.Click += new System.EventHandler(this.saveChangesToolStripMenuItem_Click);
            // 
            // instructionsToolStripMenuItem
            // 
            this.instructionsToolStripMenuItem.Name = "instructionsToolStripMenuItem";
            this.instructionsToolStripMenuItem.Size = new System.Drawing.Size(116, 29);
            this.instructionsToolStripMenuItem.Text = "Instructions";
            this.instructionsToolStripMenuItem.Click += new System.EventHandler(this.instructionsToolStripMenuItem_Click);
            // 
            // treeIOToolStripMenuItem
            // 
            this.treeIOToolStripMenuItem.Name = "treeIOToolStripMenuItem";
            this.treeIOToolStripMenuItem.Size = new System.Drawing.Size(79, 29);
            this.treeIOToolStripMenuItem.Text = "Tree IO";
            this.treeIOToolStripMenuItem.Click += new System.EventHandler(this.treeIOToolStripMenuItem_Click);
            // 
            // changePathsToolStripMenuItem
            // 
            this.changePathsToolStripMenuItem.Name = "changePathsToolStripMenuItem";
            this.changePathsToolStripMenuItem.Size = new System.Drawing.Size(131, 29);
            this.changePathsToolStripMenuItem.Text = "Change Paths";
            this.changePathsToolStripMenuItem.Click += new System.EventHandler(this.changePathsToolStripMenuItem_Click);
            // 
            // startTPFModeToolStripMenuItem
            // 
            this.startTPFModeToolStripMenuItem.Name = "startTPFModeToolStripMenuItem";
            this.startTPFModeToolStripMenuItem.Size = new System.Drawing.Size(145, 29);
            this.startTPFModeToolStripMenuItem.Text = "Start TPF Mode";
            this.startTPFModeToolStripMenuItem.Click += new System.EventHandler(this.startTPFModeToolStripMenuItem_Click);
            // 
            // rebuildDatabaseToolStripMenuItem
            // 
            this.rebuildDatabaseToolStripMenuItem.Name = "rebuildDatabaseToolStripMenuItem";
            this.rebuildDatabaseToolStripMenuItem.Size = new System.Drawing.Size(162, 29);
            this.rebuildDatabaseToolStripMenuItem.Text = "Rebuild Database";
            this.rebuildDatabaseToolStripMenuItem.Click += new System.EventHandler(this.rebuildDatabaseToolStripMenuItem_Click);
            // 
            // updateTOCsToolStripMenuItem
            // 
            this.updateTOCsToolStripMenuItem.Name = "updateTOCsToolStripMenuItem";
            this.updateTOCsToolStripMenuItem.Size = new System.Drawing.Size(132, 29);
            this.updateTOCsToolStripMenuItem.Text = "Update TOC\'s";
            this.updateTOCsToolStripMenuItem.Click += new System.EventHandler(this.updateTOCsToolStripMenuItem_Click);
            // 
            // regenerateThumbnailsToolStripMenuItem
            // 
            this.regenerateThumbnailsToolStripMenuItem.Name = "regenerateThumbnailsToolStripMenuItem";
            this.regenerateThumbnailsToolStripMenuItem.Size = new System.Drawing.Size(208, 29);
            this.regenerateThumbnailsToolStripMenuItem.Text = "Regenerate Thumbnails";
            this.regenerateThumbnailsToolStripMenuItem.Click += new System.EventHandler(this.regenerateThumbnailsToolStripMenuItem_Click);
            // 
            // addDLCToTreeToolStripMenuItem
            // 
            this.addDLCToTreeToolStripMenuItem.Name = "addDLCToTreeToolStripMenuItem";
            this.addDLCToTreeToolStripMenuItem.Size = new System.Drawing.Size(151, 29);
            this.addDLCToTreeToolStripMenuItem.Text = "Add DLC to tree";
            this.addDLCToTreeToolStripMenuItem.Click += new System.EventHandler(this.addDLCToTreeToolStripMenuItem_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 35);
            this.splitContainer1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.splitContainer2);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.OutputBox);
            this.splitContainer1.Size = new System.Drawing.Size(1424, 404);
            this.splitContainer1.SplitterDistance = 308;
            this.splitContainer1.SplitterWidth = 2;
            this.splitContainer1.TabIndex = 2;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.MainTreeView);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.splitContainer3);
            this.splitContainer2.Size = new System.Drawing.Size(1424, 308);
            this.splitContainer2.SplitterDistance = 395;
            this.splitContainer2.SplitterWidth = 2;
            this.splitContainer2.TabIndex = 0;
            // 
            // MainTreeView
            // 
            this.MainTreeView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.MainTreeView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.MainTreeView.ImageIndex = 0;
            this.MainTreeView.ImageList = this.TreeImageList;
            this.MainTreeView.Location = new System.Drawing.Point(0, 0);
            this.MainTreeView.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.MainTreeView.Name = "MainTreeView";
            this.MainTreeView.SelectedImageIndex = 1;
            this.MainTreeView.Size = new System.Drawing.Size(395, 308);
            this.MainTreeView.TabIndex = 0;
            this.PrimaryToolTip.SetToolTip(this.MainTreeView, "This area shows the textures in the selected game. \r\nFolders are often package na" +
        "mes, but only devs need that.");
            this.MainTreeView.AfterCollapse += new System.Windows.Forms.TreeViewEventHandler(this.MainTreeView_AfterCollapse);
            this.MainTreeView.AfterExpand += new System.Windows.Forms.TreeViewEventHandler(this.MainTreeView_AfterExpand);
            this.MainTreeView.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.MainTreeView_AfterSelect);
            // 
            // TreeImageList
            // 
            this.TreeImageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("TreeImageList.ImageStream")));
            this.TreeImageList.TransparentColor = System.Drawing.Color.Transparent;
            this.TreeImageList.Images.SetKeyName(0, "Folder.ico");
            this.TreeImageList.Images.SetKeyName(1, "Folder-Open.ico");
            // 
            // splitContainer3
            // 
            this.splitContainer3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer3.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainer3.Location = new System.Drawing.Point(0, 0);
            this.splitContainer3.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.splitContainer3.Name = "splitContainer3";
            // 
            // splitContainer3.Panel1
            // 
            this.splitContainer3.Panel1.AutoScroll = true;
            this.splitContainer3.Panel1.AutoScrollMinSize = new System.Drawing.Size(10, 10);
            this.splitContainer3.Panel1.Controls.Add(this.ContextPanel);
            this.splitContainer3.Panel1.Controls.Add(this.MainListView);
            this.splitContainer3.Panel1.Controls.Add(this.PicturePanel);
            // 
            // splitContainer3.Panel2
            // 
            this.splitContainer3.Panel2.Controls.Add(this.splitContainer4);
            this.splitContainer3.Size = new System.Drawing.Size(1027, 308);
            this.splitContainer3.SplitterDistance = 726;
            this.splitContainer3.SplitterWidth = 2;
            this.splitContainer3.TabIndex = 0;
            // 
            // ContextPanel
            // 
            this.ContextPanel.Controls.Add(this.LowResButton);
            this.ContextPanel.Controls.Add(this.NoRenderButton);
            this.ContextPanel.Controls.Add(this.RegenerateButton);
            this.ContextPanel.Controls.Add(this.UpscaleButton);
            this.ContextPanel.Controls.Add(this.ExtractButton);
            this.ContextPanel.Controls.Add(this.ReplaceButton);
            this.ContextPanel.Controls.Add(this.AddBiggerButton);
            this.ContextPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.ContextPanel.Location = new System.Drawing.Point(0, 318);
            this.ContextPanel.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.ContextPanel.Name = "ContextPanel";
            this.ContextPanel.Size = new System.Drawing.Size(700, 46);
            this.ContextPanel.TabIndex = 2;
            this.PrimaryToolTip.SetToolTip(this.ContextPanel, "This context menu appears when a texture is selected,\r\nand provides texture based" +
        " operations. \r\nClick this bubble to remove all instructions.");
            // 
            // LowResButton
            // 
            this.LowResButton.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.LowResButton.Location = new System.Drawing.Point(858, 5);
            this.LowResButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.LowResButton.Name = "LowResButton";
            this.LowResButton.Size = new System.Drawing.Size(112, 35);
            this.LowResButton.TabIndex = 6;
            this.LowResButton.Text = "Low Res";
            this.PrimaryToolTip.SetToolTip(this.LowResButton, "Not currently implemented");
            this.LowResButton.UseVisualStyleBackColor = true;
            this.LowResButton.Click += new System.EventHandler(this.LowResButton_Click);
            // 
            // NoRenderButton
            // 
            this.NoRenderButton.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.NoRenderButton.Location = new System.Drawing.Point(736, 5);
            this.NoRenderButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.NoRenderButton.Name = "NoRenderButton";
            this.NoRenderButton.Size = new System.Drawing.Size(112, 35);
            this.NoRenderButton.TabIndex = 5;
            this.NoRenderButton.Text = "No Render";
            this.PrimaryToolTip.SetToolTip(this.NoRenderButton, "Not currently implemented");
            this.NoRenderButton.UseVisualStyleBackColor = true;
            this.NoRenderButton.Click += new System.EventHandler(this.NoRenderButton_Click);
            // 
            // RegenerateButton
            // 
            this.RegenerateButton.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.RegenerateButton.Location = new System.Drawing.Point(538, 5);
            this.RegenerateButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.RegenerateButton.Name = "RegenerateButton";
            this.RegenerateButton.Size = new System.Drawing.Size(189, 35);
            this.RegenerateButton.TabIndex = 4;
            this.RegenerateButton.Text = "Regenerate Thumbnail";
            this.PrimaryToolTip.SetToolTip(this.RegenerateButton, "Recreates a thumbnail image from the game files");
            this.RegenerateButton.UseVisualStyleBackColor = true;
            this.RegenerateButton.Click += new System.EventHandler(this.RegenerateButton_Click);
            // 
            // UpscaleButton
            // 
            this.UpscaleButton.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.UpscaleButton.Location = new System.Drawing.Point(417, 5);
            this.UpscaleButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.UpscaleButton.Name = "UpscaleButton";
            this.UpscaleButton.Size = new System.Drawing.Size(112, 35);
            this.UpscaleButton.TabIndex = 3;
            this.UpscaleButton.Text = "Upscale";
            this.PrimaryToolTip.SetToolTip(this.UpscaleButton, "Currently unused");
            this.UpscaleButton.UseVisualStyleBackColor = true;
            this.UpscaleButton.Click += new System.EventHandler(this.UpscaleButton_Click);
            // 
            // ExtractButton
            // 
            this.ExtractButton.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.ExtractButton.Location = new System.Drawing.Point(296, 5);
            this.ExtractButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.ExtractButton.Name = "ExtractButton";
            this.ExtractButton.Size = new System.Drawing.Size(112, 35);
            this.ExtractButton.TabIndex = 2;
            this.ExtractButton.Text = "Extract";
            this.PrimaryToolTip.SetToolTip(this.ExtractButton, "Extract mip level from selected image");
            this.ExtractButton.UseVisualStyleBackColor = true;
            this.ExtractButton.Click += new System.EventHandler(this.ExtractButton_Click);
            // 
            // ReplaceButton
            // 
            this.ReplaceButton.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.ReplaceButton.Location = new System.Drawing.Point(174, 5);
            this.ReplaceButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.ReplaceButton.Name = "ReplaceButton";
            this.ReplaceButton.Size = new System.Drawing.Size(112, 35);
            this.ReplaceButton.TabIndex = 1;
            this.ReplaceButton.Text = "Replace";
            this.PrimaryToolTip.SetToolTip(this.ReplaceButton, "Replace a mip level with another image");
            this.ReplaceButton.UseVisualStyleBackColor = true;
            this.ReplaceButton.Click += new System.EventHandler(this.ReplaceButton_Click);
            // 
            // AddBiggerButton
            // 
            this.AddBiggerButton.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.AddBiggerButton.Location = new System.Drawing.Point(9, 5);
            this.AddBiggerButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.AddBiggerButton.Name = "AddBiggerButton";
            this.AddBiggerButton.Size = new System.Drawing.Size(156, 35);
            this.AddBiggerButton.TabIndex = 0;
            this.AddBiggerButton.Text = "Add Bigger Image";
            this.PrimaryToolTip.SetToolTip(this.AddBiggerButton, "Increases selected texture resolution with a provided image");
            this.AddBiggerButton.UseVisualStyleBackColor = true;
            this.AddBiggerButton.Click += new System.EventHandler(this.AddBiggerButton_Click);
            // 
            // MainListView
            // 
            this.MainListView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.MainListView.GridLines = true;
            this.MainListView.HideSelection = false;
            this.MainListView.LargeImageList = this.ListViewImageList;
            this.MainListView.Location = new System.Drawing.Point(4, 0);
            this.MainListView.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.MainListView.MultiSelect = false;
            this.MainListView.Name = "MainListView";
            this.MainListView.Size = new System.Drawing.Size(681, 318);
            this.MainListView.TabIndex = 0;
            this.MainListView.TileSize = new System.Drawing.Size(128, 192);
            this.PrimaryToolTip.SetToolTip(this.MainListView, "This area shows the textures within the selected folder.");
            this.MainListView.UseCompatibleStateImageBehavior = false;
            this.MainListView.SelectedIndexChanged += new System.EventHandler(this.MainListView_SelectedIndexChanged);
            this.MainListView.DoubleClick += new System.EventHandler(this.MainListView_DoubleClick);
            this.MainListView.Leave += new System.EventHandler(this.MainListView_FocusLeave);
            this.MainListView.MouseDown += new System.Windows.Forms.MouseEventHandler(this.MainListView_MouseDown);
            // 
            // ListViewImageList
            // 
            this.ListViewImageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("ListViewImageList.ImageStream")));
            this.ListViewImageList.TransparentColor = System.Drawing.Color.Transparent;
            this.ListViewImageList.Images.SetKeyName(0, "Placeholder.ico");
            // 
            // PicturePanel
            // 
            this.PicturePanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.PicturePanel.AutoScroll = true;
            this.PicturePanel.AutoScrollMinSize = new System.Drawing.Size(10, 10);
            this.PicturePanel.Controls.Add(this.MainPictureBox);
            this.PicturePanel.Location = new System.Drawing.Point(9, 8);
            this.PicturePanel.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.PicturePanel.Name = "PicturePanel";
            this.PicturePanel.Size = new System.Drawing.Size(567, 318);
            this.PicturePanel.TabIndex = 3;
            this.PrimaryToolTip.SetToolTip(this.PicturePanel, "This area shows the textures within the selected folder.\r\nIt shows a thumbnail of" +
        " each image and clicking on an\r\nimage will show details on the right.");
            this.PicturePanel.MouseClick += new System.Windows.Forms.MouseEventHandler(this.PicturePanel_Click);
            // 
            // MainPictureBox
            // 
            this.MainPictureBox.Location = new System.Drawing.Point(0, 0);
            this.MainPictureBox.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.MainPictureBox.Name = "MainPictureBox";
            this.MainPictureBox.Size = new System.Drawing.Size(149, 226);
            this.MainPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.MainPictureBox.TabIndex = 1;
            this.MainPictureBox.TabStop = false;
            this.PrimaryToolTip.SetToolTip(this.MainPictureBox, "This area shows the textures within the selected folder.");
            this.MainPictureBox.Click += new System.EventHandler(this.MainPictureBox_Click);
            // 
            // splitContainer4
            // 
            this.splitContainer4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer4.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer4.Location = new System.Drawing.Point(0, 0);
            this.splitContainer4.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.splitContainer4.Name = "splitContainer4";
            this.splitContainer4.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer4.Panel1
            // 
            this.splitContainer4.Panel1.Controls.Add(this.SearchBox);
            this.splitContainer4.Panel1.Controls.Add(this.DetailsHideButton);
            this.splitContainer4.Panel1.Controls.Add(this.SearchCountLabel);
            // 
            // splitContainer4.Panel2
            // 
            this.splitContainer4.Panel2.Controls.Add(this.TabSearchSplitter);
            this.splitContainer4.Size = new System.Drawing.Size(299, 308);
            this.splitContainer4.SplitterDistance = 25;
            this.splitContainer4.SplitterWidth = 2;
            this.splitContainer4.TabIndex = 0;
            // 
            // SearchBox
            // 
            this.SearchBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.SearchBox.Location = new System.Drawing.Point(51, -4);
            this.SearchBox.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.SearchBox.Name = "SearchBox";
            this.SearchBox.Size = new System.Drawing.Size(241, 26);
            this.SearchBox.TabIndex = 2;
            this.PrimaryToolTip.SetToolTip(this.SearchBox, "Search filters:\r\nname = Texture search\r\n@expID = Export ID search\r\n\\pcc = Game fi" +
        "lename search\r\n\\pcc @ expID = Combined search\r\n-name = Thumbnail search");
            this.SearchBox.TextChanged += new System.EventHandler(this.SearchBox_TextChanged);
            this.SearchBox.Enter += new System.EventHandler(this.SearchBox_Enter);
            this.SearchBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Search_KeyDown);
            this.SearchBox.Leave += new System.EventHandler(this.SearchBox_Leave);
            // 
            // DetailsHideButton
            // 
            this.DetailsHideButton.FlatAppearance.BorderSize = 0;
            this.DetailsHideButton.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.DetailsHideButton.Location = new System.Drawing.Point(4, 0);
            this.DetailsHideButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.DetailsHideButton.Name = "DetailsHideButton";
            this.DetailsHideButton.Size = new System.Drawing.Size(44, 35);
            this.DetailsHideButton.TabIndex = 1;
            this.DetailsHideButton.Text = ">>";
            this.DetailsHideButton.UseVisualStyleBackColor = true;
            this.DetailsHideButton.Click += new System.EventHandler(this.DetailsHideButton_Click);
            // 
            // SearchCountLabel
            // 
            this.SearchCountLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.SearchCountLabel.AutoSize = true;
            this.SearchCountLabel.Location = new System.Drawing.Point(275, 8);
            this.SearchCountLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.SearchCountLabel.Name = "SearchCountLabel";
            this.SearchCountLabel.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.SearchCountLabel.Size = new System.Drawing.Size(18, 20);
            this.SearchCountLabel.TabIndex = 3;
            this.SearchCountLabel.Text = "0";
            this.SearchCountLabel.TextAlign = System.Drawing.ContentAlignment.TopRight;
            this.SearchCountLabel.SizeChanged += new System.EventHandler(this.Change_Changed);
            // 
            // TabSearchSplitter
            // 
            this.TabSearchSplitter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.TabSearchSplitter.Location = new System.Drawing.Point(0, 0);
            this.TabSearchSplitter.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.TabSearchSplitter.Name = "TabSearchSplitter";
            this.TabSearchSplitter.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // TabSearchSplitter.Panel1
            // 
            this.TabSearchSplitter.Panel1.Controls.Add(this.SearchListBox);
            this.TabSearchSplitter.Panel1MinSize = 0;
            // 
            // TabSearchSplitter.Panel2
            // 
            this.TabSearchSplitter.Panel2.Controls.Add(this.tabControl1);
            this.TabSearchSplitter.Panel2MinSize = 0;
            this.TabSearchSplitter.Size = new System.Drawing.Size(299, 281);
            this.TabSearchSplitter.SplitterDistance = 23;
            this.TabSearchSplitter.SplitterWidth = 2;
            this.TabSearchSplitter.TabIndex = 2;
            // 
            // SearchListBox
            // 
            this.SearchListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.SearchListBox.FormattingEnabled = true;
            this.SearchListBox.ItemHeight = 20;
            this.SearchListBox.Location = new System.Drawing.Point(0, 0);
            this.SearchListBox.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.SearchListBox.Name = "SearchListBox";
            this.SearchListBox.Size = new System.Drawing.Size(299, 23);
            this.SearchListBox.TabIndex = 1;
            this.SearchListBox.SelectedIndexChanged += new System.EventHandler(this.SearchListBox_SelectedIndexChanged);
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.PropertiesTab);
            this.tabControl1.Controls.Add(this.PCCsTab);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(299, 256);
            this.tabControl1.TabIndex = 0;
            this.PrimaryToolTip.SetToolTip(this.tabControl1, "Displays properties and list of pcc\'s containing the selected texture");
            // 
            // PropertiesTab
            // 
            this.PropertiesTab.Controls.Add(this.PropertiesRTB);
            this.PropertiesTab.Location = new System.Drawing.Point(4, 29);
            this.PropertiesTab.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.PropertiesTab.Name = "PropertiesTab";
            this.PropertiesTab.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.PropertiesTab.Size = new System.Drawing.Size(291, 223);
            this.PropertiesTab.TabIndex = 0;
            this.PropertiesTab.Text = "Properties";
            this.PropertiesTab.UseVisualStyleBackColor = true;
            // 
            // PropertiesRTB
            // 
            this.PropertiesRTB.BackColor = System.Drawing.Color.White;
            this.PropertiesRTB.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.PropertiesRTB.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PropertiesRTB.Location = new System.Drawing.Point(4, 5);
            this.PropertiesRTB.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.PropertiesRTB.Name = "PropertiesRTB";
            this.PropertiesRTB.ReadOnly = true;
            this.PropertiesRTB.Size = new System.Drawing.Size(283, 213);
            this.PropertiesRTB.TabIndex = 0;
            this.PropertiesRTB.Text = "";
            // 
            // PCCsTab
            // 
            this.PCCsTab.Controls.Add(this.PCCsCheckedListBox);
            this.PCCsTab.Location = new System.Drawing.Point(4, 29);
            this.PCCsTab.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.PCCsTab.Name = "PCCsTab";
            this.PCCsTab.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.PCCsTab.Size = new System.Drawing.Size(440, 409);
            this.PCCsTab.TabIndex = 1;
            this.PCCsTab.Text = "PCC\'s";
            this.PCCsTab.UseVisualStyleBackColor = true;
            // 
            // PCCsCheckedListBox
            // 
            this.PCCsCheckedListBox.CheckOnClick = true;
            this.PCCsCheckedListBox.ContextMenuStrip = this.PCCBoxContext;
            this.PCCsCheckedListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PCCsCheckedListBox.FormattingEnabled = true;
            this.PCCsCheckedListBox.HorizontalScrollbar = true;
            this.PCCsCheckedListBox.Location = new System.Drawing.Point(4, 5);
            this.PCCsCheckedListBox.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.PCCsCheckedListBox.Name = "PCCsCheckedListBox";
            this.PCCsCheckedListBox.Size = new System.Drawing.Size(432, 399);
            this.PCCsCheckedListBox.TabIndex = 0;
            this.PCCsCheckedListBox.SelectedIndexChanged += new System.EventHandler(this.PCCsCheckedListBox_SelectedIndexChanged);
            // 
            // PCCBoxContext
            // 
            this.PCCBoxContext.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.PCCBoxContext.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItem1});
            this.PCCBoxContext.Name = "contextMenuStrip1";
            this.PCCBoxContext.Size = new System.Drawing.Size(180, 34);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(179, 30);
            this.toolStripMenuItem1.Text = "Export List";
            this.toolStripMenuItem1.Click += new System.EventHandler(this.PCCBoxContext_Click);
            // 
            // OutputBox
            // 
            this.OutputBox.BackColor = System.Drawing.Color.White;
            this.OutputBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.OutputBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.OutputBox.Location = new System.Drawing.Point(0, 0);
            this.OutputBox.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.OutputBox.Name = "OutputBox";
            this.OutputBox.ReadOnly = true;
            this.OutputBox.Size = new System.Drawing.Size(1424, 94);
            this.OutputBox.TabIndex = 0;
            this.OutputBox.Text = "";
            this.PrimaryToolTip.SetToolTip(this.OutputBox, "This area displays some useful information.\r\nBasically a filtered version of the " +
        "Debug Window.");
            // 
            // ChangeButton
            // 
            this.ChangeButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.ChangeButton.Enabled = false;
            this.ChangeButton.FlatAppearance.BorderSize = 0;
            this.ChangeButton.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.ChangeButton.Location = new System.Drawing.Point(1263, 2);
            this.ChangeButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.ChangeButton.Name = "ChangeButton";
            this.ChangeButton.Size = new System.Drawing.Size(154, 35);
            this.ChangeButton.TabIndex = 3;
            this.ChangeButton.Text = "Modding ME3";
            this.PrimaryToolTip.SetToolTip(this.ChangeButton, "Click to change which game is loaded");
            this.ChangeButton.UseVisualStyleBackColor = true;
            this.ChangeButton.Click += new System.EventHandler(this.ChangeButton_Click);
            // 
            // PrimaryToolTip
            // 
            this.PrimaryToolTip.AutomaticDelay = 1000;
            // 
            // Game3Label
            // 
            this.Game3Label.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.Game3Label.AutoSize = true;
            this.Game3Label.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Game3Label.ForeColor = System.Drawing.Color.Red;
            this.Game3Label.Location = new System.Drawing.Point(1231, 6);
            this.Game3Label.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Game3Label.Name = "Game3Label";
            this.Game3Label.Size = new System.Drawing.Size(21, 24);
            this.Game3Label.TabIndex = 4;
            this.Game3Label.Text = "3";
            // 
            // Game2Label
            // 
            this.Game2Label.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.Game2Label.AutoSize = true;
            this.Game2Label.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Game2Label.ForeColor = System.Drawing.Color.Red;
            this.Game2Label.Location = new System.Drawing.Point(1197, 6);
            this.Game2Label.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Game2Label.Name = "Game2Label";
            this.Game2Label.Size = new System.Drawing.Size(21, 24);
            this.Game2Label.TabIndex = 5;
            this.Game2Label.Text = "2";
            // 
            // Game1Label
            // 
            this.Game1Label.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.Game1Label.AutoSize = true;
            this.Game1Label.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Game1Label.ForeColor = System.Drawing.Color.Red;
            this.Game1Label.Location = new System.Drawing.Point(1165, 6);
            this.Game1Label.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Game1Label.Name = "Game1Label";
            this.Game1Label.Size = new System.Drawing.Size(21, 24);
            this.Game1Label.TabIndex = 6;
            this.Game1Label.Text = "1";
            // 
            // GamesLabel
            // 
            this.GamesLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.GamesLabel.AutoSize = true;
            this.GamesLabel.Location = new System.Drawing.Point(1091, 9);
            this.GamesLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.GamesLabel.Name = "GamesLabel";
            this.GamesLabel.Size = new System.Drawing.Size(65, 20);
            this.GamesLabel.TabIndex = 7;
            this.GamesLabel.Text = "Games:";
            // 
            // Tree3Label
            // 
            this.Tree3Label.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.Tree3Label.AutoSize = true;
            this.Tree3Label.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Tree3Label.ForeColor = System.Drawing.Color.Red;
            this.Tree3Label.Location = new System.Drawing.Point(1058, 6);
            this.Tree3Label.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Tree3Label.Name = "Tree3Label";
            this.Tree3Label.Size = new System.Drawing.Size(21, 24);
            this.Tree3Label.TabIndex = 8;
            this.Tree3Label.Text = "3";
            // 
            // Tree2Label
            // 
            this.Tree2Label.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.Tree2Label.AutoSize = true;
            this.Tree2Label.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Tree2Label.ForeColor = System.Drawing.Color.Red;
            this.Tree2Label.Location = new System.Drawing.Point(1025, 6);
            this.Tree2Label.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Tree2Label.Name = "Tree2Label";
            this.Tree2Label.Size = new System.Drawing.Size(21, 24);
            this.Tree2Label.TabIndex = 9;
            this.Tree2Label.Text = "2";
            // 
            // Tree1Label
            // 
            this.Tree1Label.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.Tree1Label.AutoSize = true;
            this.Tree1Label.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Tree1Label.ForeColor = System.Drawing.Color.Red;
            this.Tree1Label.Location = new System.Drawing.Point(992, 6);
            this.Tree1Label.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Tree1Label.Name = "Tree1Label";
            this.Tree1Label.Size = new System.Drawing.Size(21, 24);
            this.Tree1Label.TabIndex = 10;
            this.Tree1Label.Text = "1";
            // 
            // TreeLabel
            // 
            this.TreeLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.TreeLabel.AutoSize = true;
            this.TreeLabel.Location = new System.Drawing.Point(935, 9);
            this.TreeLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.TreeLabel.Name = "TreeLabel";
            this.TreeLabel.Size = new System.Drawing.Size(53, 20);
            this.TreeLabel.TabIndex = 11;
            this.TreeLabel.Text = "Trees:";
            // 
            // Texplorer2
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(1424, 476);
            this.Controls.Add(this.TreeLabel);
            this.Controls.Add(this.Tree1Label);
            this.Controls.Add(this.Tree2Label);
            this.Controls.Add(this.Tree3Label);
            this.Controls.Add(this.GamesLabel);
            this.Controls.Add(this.Game1Label);
            this.Controls.Add(this.Game2Label);
            this.Controls.Add(this.Game3Label);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.ChangeButton);
            this.Controls.Add(this.StatusStrip);
            this.Controls.Add(this.MainMenuStrip);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.Name = "Texplorer2";
            this.Text = "Texplorer 2.0";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form_Closing);
            this.StatusStrip.ResumeLayout(false);
            this.StatusStrip.PerformLayout();
            this.MainMenuStrip.ResumeLayout(false);
            this.MainMenuStrip.PerformLayout();
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
            this.ContextPanel.ResumeLayout(false);
            this.PicturePanel.ResumeLayout(false);
            this.PicturePanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MainPictureBox)).EndInit();
            this.splitContainer4.Panel1.ResumeLayout(false);
            this.splitContainer4.Panel1.PerformLayout();
            this.splitContainer4.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer4)).EndInit();
            this.splitContainer4.ResumeLayout(false);
            this.TabSearchSplitter.Panel1.ResumeLayout(false);
            this.TabSearchSplitter.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.TabSearchSplitter)).EndInit();
            this.TabSearchSplitter.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.PropertiesTab.ResumeLayout(false);
            this.PCCsTab.ResumeLayout(false);
            this.PCCBoxContext.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStrip StatusStrip;
        private System.Windows.Forms.ToolStripProgressBar MainProgressBar;
        private System.Windows.Forms.ToolStripLabel StatusLabel;
        private System.Windows.Forms.MenuStrip MainMenuStrip;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.TreeView MainTreeView;
        private System.Windows.Forms.RichTextBox OutputBox;
        private System.Windows.Forms.ToolStripMenuItem instructionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem rebuildDatabaseToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem updateTOCsToolStripMenuItem;
        private System.Windows.Forms.ImageList TreeImageList;
        private System.Windows.Forms.ImageList ListViewImageList;
        private System.Windows.Forms.SplitContainer splitContainer3;
        private System.Windows.Forms.ListView MainListView;
        private System.Windows.Forms.SplitContainer splitContainer4;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage PropertiesTab;
        private System.Windows.Forms.TabPage PCCsTab;
        private System.Windows.Forms.RichTextBox PropertiesRTB;
        private System.Windows.Forms.CheckedListBox PCCsCheckedListBox;
        private System.Windows.Forms.Button DetailsHideButton;
        private System.Windows.Forms.TextBox SearchBox;
        private System.Windows.Forms.ListBox SearchListBox;
        private System.Windows.Forms.SplitContainer TabSearchSplitter;
        private System.Windows.Forms.Button ChangeButton;
        private System.Windows.Forms.Label SearchCountLabel;
        private System.Windows.Forms.ToolStripButton CancelButton;
        private System.Windows.Forms.Panel ContextPanel;
        private System.Windows.Forms.Button ExtractButton;
        private System.Windows.Forms.Button ReplaceButton;
        private System.Windows.Forms.Button AddBiggerButton;
        private System.Windows.Forms.Button UpscaleButton;
        private System.Windows.Forms.Panel PicturePanel;
        public System.Windows.Forms.PictureBox MainPictureBox;
        private System.Windows.Forms.ToolStripMenuItem regenerateThumbnailsToolStripMenuItem;
        private System.Windows.Forms.Button RegenerateButton;
        private System.Windows.Forms.Button LowResButton;
        private System.Windows.Forms.Button NoRenderButton;
        private System.Windows.Forms.ToolStripLabel toolStripButton1;
        private System.Windows.Forms.ToolStripMenuItem startTPFModeToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip PCCBoxContext;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem1;
        private System.Windows.Forms.ToolTip PrimaryToolTip;
        private System.Windows.Forms.Label Game3Label;
        private System.Windows.Forms.Label Game2Label;
        private System.Windows.Forms.Label Game1Label;
        private System.Windows.Forms.Label GamesLabel;
        private System.Windows.Forms.Label Tree3Label;
        private System.Windows.Forms.Label Tree2Label;
        private System.Windows.Forms.Label Tree1Label;
        private System.Windows.Forms.Label TreeLabel;
        private System.Windows.Forms.ToolStripMenuItem saveChangesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem treeIOToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem changePathsToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripLabel1;
        private System.Windows.Forms.ToolStripLabel VersionLabel;
        private System.Windows.Forms.ToolStripMenuItem addDLCToTreeToolStripMenuItem;
    }
}