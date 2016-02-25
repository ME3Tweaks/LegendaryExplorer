namespace MassEffect3.SaveEdit
{
    partial class SavePicker
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SavePicker));
			this.mainSplitContainer = new System.Windows.Forms.SplitContainer();
			this.iconImageList = new System.Windows.Forms.ImageList(this.components);
			this.careerToolStrip = new System.Windows.Forms.ToolStrip();
			this.deleteCareerButton = new System.Windows.Forms.ToolStripButton();
			this.refreshButton = new System.Windows.Forms.ToolStripButton();
			this.saveToolStrip = new System.Windows.Forms.ToolStrip();
			this.deleteSaveButton = new System.Windows.Forms.ToolStripButton();
			this.loadFileButton = new System.Windows.Forms.ToolStripButton();
			this.saveFileButton = new System.Windows.Forms.ToolStripButton();
			this.cancelButton = new System.Windows.Forms.ToolStripButton();
			this.careerListView = new MassEffect3.SaveEdit.DoubleBufferedListView();
			this.careerHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.saveListView = new MassEffect3.SaveEdit.DoubleBufferedListView();
			this.saveHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			((System.ComponentModel.ISupportInitialize)(this.mainSplitContainer)).BeginInit();
			this.mainSplitContainer.Panel1.SuspendLayout();
			this.mainSplitContainer.Panel2.SuspendLayout();
			this.mainSplitContainer.SuspendLayout();
			this.careerToolStrip.SuspendLayout();
			this.saveToolStrip.SuspendLayout();
			this.SuspendLayout();
			// 
			// mainSplitContainer
			// 
			resources.ApplyResources(this.mainSplitContainer, "mainSplitContainer");
			this.mainSplitContainer.Name = "mainSplitContainer";
			// 
			// mainSplitContainer.Panel1
			// 
			this.mainSplitContainer.Panel1.Controls.Add(this.careerListView);
			this.mainSplitContainer.Panel1.Controls.Add(this.careerToolStrip);
			// 
			// mainSplitContainer.Panel2
			// 
			this.mainSplitContainer.Panel2.Controls.Add(this.saveListView);
			this.mainSplitContainer.Panel2.Controls.Add(this.saveToolStrip);
			// 
			// iconImageList
			// 
			this.iconImageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("iconImageList.ImageStream")));
			this.iconImageList.TransparentColor = System.Drawing.Color.Transparent;
			this.iconImageList.Images.SetKeyName(0, "New");
			this.iconImageList.Images.SetKeyName(1, "Class_Unknown");
			this.iconImageList.Images.SetKeyName(2, "Class_Adept");
			this.iconImageList.Images.SetKeyName(3, "Class_Sentinel");
			this.iconImageList.Images.SetKeyName(4, "Class_Infiltrator");
			this.iconImageList.Images.SetKeyName(5, "Class_Soldier");
			this.iconImageList.Images.SetKeyName(6, "Class_Vanguard");
			this.iconImageList.Images.SetKeyName(7, "Class_Engineer");
			// 
			// careerToolStrip
			// 
			resources.ApplyResources(this.careerToolStrip, "careerToolStrip");
			this.careerToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.deleteCareerButton,
            this.refreshButton});
			this.careerToolStrip.Name = "careerToolStrip";
			// 
			// deleteCareerButton
			// 
			this.deleteCareerButton.Image = global::MassEffect3.SaveEdit.Properties.Resources.SavePIcker_Career_Delete;
			resources.ApplyResources(this.deleteCareerButton, "deleteCareerButton");
			this.deleteCareerButton.Name = "deleteCareerButton";
			this.deleteCareerButton.Click += new System.EventHandler(this.OnDeleteCareer);
			// 
			// refreshButton
			// 
			this.refreshButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
			this.refreshButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.refreshButton.Image = global::MassEffect3.SaveEdit.Properties.Resources.SavePicker_Career_Refresh;
			resources.ApplyResources(this.refreshButton, "refreshButton");
			this.refreshButton.Name = "refreshButton";
			this.refreshButton.Click += new System.EventHandler(this.OnRefresh);
			// 
			// saveToolStrip
			// 
			resources.ApplyResources(this.saveToolStrip, "saveToolStrip");
			this.saveToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.deleteSaveButton,
            this.loadFileButton,
            this.saveFileButton,
            this.cancelButton});
			this.saveToolStrip.Name = "saveToolStrip";
			// 
			// deleteSaveButton
			// 
			this.deleteSaveButton.Image = global::MassEffect3.SaveEdit.Properties.Resources.SavePicker_Save_Delete;
			resources.ApplyResources(this.deleteSaveButton, "deleteSaveButton");
			this.deleteSaveButton.Name = "deleteSaveButton";
			this.deleteSaveButton.Click += new System.EventHandler(this.OnDeleteSave);
			// 
			// loadFileButton
			// 
			this.loadFileButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
			this.loadFileButton.Image = global::MassEffect3.SaveEdit.Properties.Resources.SavePicker_Save_File_Load;
			resources.ApplyResources(this.loadFileButton, "loadFileButton");
			this.loadFileButton.Name = "loadFileButton";
			this.loadFileButton.Click += new System.EventHandler(this.OnChooseSave);
			// 
			// saveFileButton
			// 
			this.saveFileButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
			this.saveFileButton.Image = global::MassEffect3.SaveEdit.Properties.Resources.SavePicker_Save_File_Save;
			resources.ApplyResources(this.saveFileButton, "saveFileButton");
			this.saveFileButton.Name = "saveFileButton";
			this.saveFileButton.Click += new System.EventHandler(this.OnChooseSave);
			// 
			// cancelButton
			// 
			this.cancelButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
			this.cancelButton.Image = global::MassEffect3.SaveEdit.Properties.Resources.SavePicker_Cancel;
			resources.ApplyResources(this.cancelButton, "cancelButton");
			this.cancelButton.Name = "cancelButton";
			this.cancelButton.Click += new System.EventHandler(this.OnCancel);
			// 
			// careerListView
			// 
			this.careerListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.careerHeader});
			resources.ApplyResources(this.careerListView, "careerListView");
			this.careerListView.FullRowSelect = true;
			this.careerListView.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
			this.careerListView.HideSelection = false;
			this.careerListView.Items.AddRange(new System.Windows.Forms.ListViewItem[] {
            ((System.Windows.Forms.ListViewItem)(resources.GetObject("careerListView.Items")))});
			this.careerListView.LargeImageList = this.iconImageList;
			this.careerListView.Name = "careerListView";
			this.careerListView.ShowItemToolTips = true;
			this.careerListView.SmallImageList = this.iconImageList;
			this.careerListView.UseCompatibleStateImageBehavior = false;
			this.careerListView.View = System.Windows.Forms.View.Details;
			this.careerListView.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.OnSelectCareer);
			// 
			// careerHeader
			// 
			resources.ApplyResources(this.careerHeader, "careerHeader");
			// 
			// saveListView
			// 
			this.saveListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.saveHeader});
			resources.ApplyResources(this.saveListView, "saveListView");
			this.saveListView.FullRowSelect = true;
			this.saveListView.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
			this.saveListView.HideSelection = false;
			this.saveListView.Items.AddRange(new System.Windows.Forms.ListViewItem[] {
            ((System.Windows.Forms.ListViewItem)(resources.GetObject("saveListView.Items")))});
			this.saveListView.LargeImageList = this.iconImageList;
			this.saveListView.Name = "saveListView";
			this.saveListView.SmallImageList = this.iconImageList;
			this.saveListView.UseCompatibleStateImageBehavior = false;
			this.saveListView.View = System.Windows.Forms.View.Details;
			this.saveListView.ItemActivate += new System.EventHandler(this.OnSaveActivate);
			this.saveListView.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.OnSelectSave);
			// 
			// saveHeader
			// 
			resources.ApplyResources(this.saveHeader, "saveHeader");
			// 
			// SavePicker
			// 
			resources.ApplyResources(this, "$this");
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this.mainSplitContainer);
			this.DoubleBuffered = true;
			this.Name = "SavePicker";
			this.ShowIcon = false;
			this.ShowInTaskbar = false;
			this.Shown += new System.EventHandler(this.OnShown);
			this.mainSplitContainer.Panel1.ResumeLayout(false);
			this.mainSplitContainer.Panel1.PerformLayout();
			this.mainSplitContainer.Panel2.ResumeLayout(false);
			this.mainSplitContainer.Panel2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.mainSplitContainer)).EndInit();
			this.mainSplitContainer.ResumeLayout(false);
			this.careerToolStrip.ResumeLayout(false);
			this.careerToolStrip.PerformLayout();
			this.saveToolStrip.ResumeLayout(false);
			this.saveToolStrip.PerformLayout();
			this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer mainSplitContainer;
        private DoubleBufferedListView careerListView;
        private System.Windows.Forms.ColumnHeader careerHeader;
        private System.Windows.Forms.ToolStrip careerToolStrip;
        private System.Windows.Forms.ToolStripButton deleteCareerButton;
        private DoubleBufferedListView saveListView;
        private System.Windows.Forms.ToolStrip saveToolStrip;
        private System.Windows.Forms.ToolStripButton deleteSaveButton;
        private System.Windows.Forms.ToolStripButton loadFileButton;
        private System.Windows.Forms.ImageList iconImageList;
        private System.Windows.Forms.ColumnHeader saveHeader;
        private System.Windows.Forms.ToolStripButton cancelButton;
        private System.Windows.Forms.ToolStripButton saveFileButton;
        private System.Windows.Forms.ToolStripButton refreshButton;
    }
}