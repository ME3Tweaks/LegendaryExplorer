using MassEffect2.SaveFormats;

namespace MassEffect2.SaveEdit
{
    partial class Editor
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Editor));
			this._RawSplitContainer = new System.Windows.Forms.SplitContainer();
			this._RawParentPropertyGrid = new System.Windows.Forms.PropertyGrid();
			this._RawChildPropertyGrid = new System.Windows.Forms.PropertyGrid();
			this._RootToolStrip = new System.Windows.Forms.ToolStrip();
			this._RootNewSplitButton = new System.Windows.Forms.ToolStripDropDownButton();
			this._RootNewMaleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this._RootNewFemaleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this._RootOpenFromGenericButton = new System.Windows.Forms.ToolStripSplitButton();
			this._RootOpenFromCareerMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this._RootOpenFromFileMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this._RootSaveToGenericButton = new System.Windows.Forms.ToolStripSplitButton();
			this._RootSaveToCareerMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this._RootSaveToFileMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this._RootSettingsButton = new System.Windows.Forms.ToolStripDropDownButton();
			this._RootDontUseCareerPickerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this._RootAboutButton = new System.Windows.Forms.ToolStripButton();
			this._RootTabControl = new System.Windows.Forms.TabControl();
			this._PlayerRootTabPage = new System.Windows.Forms.TabPage();
			this._PlayerRootTabControl = new System.Windows.Forms.TabControl();
			this._PlayerBasicTabPage = new System.Windows.Forms.TabPage();
			this._PlayerBasicPanel = new System.Windows.Forms.FlowLayoutPanel();
			this._PlayerBasicGenderWarningLabel = new System.Windows.Forms.Label();
			this._PlayerAppearanceRootTabPage = new System.Windows.Forms.TabPage();
			this._PlayerAppearanceRootTabControl = new System.Windows.Forms.TabControl();
			this._PlayerAppearanceColorTabPage = new System.Windows.Forms.TabPage();
			this._PlayerAppearanceColorListBox = new System.Windows.Forms.ListBox();
			this._RootVectorParametersBindingSource = new System.Windows.Forms.BindingSource(this.components);
			this._PlayerAppearanceColorToolStrip = new System.Windows.Forms.ToolStrip();
			this._PlayerAppearanceColorAddColorButton = new System.Windows.Forms.ToolStripButton();
			this._PlayerAppearanceColorRemoveColorButton = new System.Windows.Forms.ToolStripButton();
			this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
			this._PlayerAppearanceColorChangeColorButton = new System.Windows.Forms.ToolStripButton();
			this._RootIconImageList = new System.Windows.Forms.ImageList(this.components);
			this._PlayerAppearanceRootToolStrip = new System.Windows.Forms.ToolStrip();
			this._PlayerAppearanceMorphHeadDropDownButton = new System.Windows.Forms.ToolStripDropDownButton();
			this._PlayerAppearanceMorphHeadImportButton = new System.Windows.Forms.ToolStripMenuItem();
			this._PlayerAppearanceMorphHeadExportButton = new System.Windows.Forms.ToolStripMenuItem();
			this._PlayerAppearancePresetDropDownButton = new System.Windows.Forms.ToolStripDropDownButton();
			this._PlayerAppearancePresetOpenFromFileButton = new System.Windows.Forms.ToolStripMenuItem();
			this._PlayerAppearancePresetSaveToFileButton = new System.Windows.Forms.ToolStripMenuItem();
			this._PlotRootTabPage = new System.Windows.Forms.TabPage();
			this._PlotRootTabControl = new System.Windows.Forms.TabControl();
			this._PlotManualTabPage = new System.Windows.Forms.TabPage();
			this._PlotManualToolStrip = new System.Windows.Forms.ToolStrip();
			this._PlotManualClearLogButton = new System.Windows.Forms.ToolStripButton();
			this._PlotManualLogTextBox = new System.Windows.Forms.TextBox();
			this._PlotManualFloatGroupBox = new System.Windows.Forms.GroupBox();
			this._PlotManualFloatIdTextBox = new System.Windows.Forms.TextBox();
			this._PlotManualFloatGetButton = new System.Windows.Forms.Button();
			this._PlotManualFloatSetButton = new System.Windows.Forms.Button();
			this._PlotManualFloatValueTextBox = new System.Windows.Forms.TextBox();
			this._PlotManualIntGroupBox = new System.Windows.Forms.GroupBox();
			this._PlotManualIntIdTextBox = new System.Windows.Forms.TextBox();
			this._PlotManualIntGetButton = new System.Windows.Forms.Button();
			this._PlotManualIntSetButton = new System.Windows.Forms.Button();
			this._PlotManualIntValueTextBox = new System.Windows.Forms.TextBox();
			this._PlotManualBoolGroupBox = new System.Windows.Forms.GroupBox();
			this._PlotManualBoolIdTextBox = new System.Windows.Forms.TextBox();
			this._PlotManualBoolSetButton = new System.Windows.Forms.Button();
			this._PlotManualBoolGetButton = new System.Windows.Forms.Button();
			this._PlotManualBoolValueCheckBox = new System.Windows.Forms.CheckBox();
			this._RawTabPage = new System.Windows.Forms.TabPage();
			this._RootSaveFileBindingSource = new System.Windows.Forms.BindingSource(this.components);
			this._RootSaveGameOpenFileDialog = new System.Windows.Forms.OpenFileDialog();
			this._RootSaveGameSaveFileDialog = new System.Windows.Forms.SaveFileDialog();
			this._RootMorphHeadOpenFileDialog = new System.Windows.Forms.OpenFileDialog();
			this._RootMorphHeadSaveFileDialog = new System.Windows.Forms.SaveFileDialog();
			this._RootAppearancePresetOpenFileDialog = new System.Windows.Forms.OpenFileDialog();
			this._RootAppearancePresetSaveFileDialog = new System.Windows.Forms.SaveFileDialog();
			((System.ComponentModel.ISupportInitialize)(this._RawSplitContainer)).BeginInit();
			this._RawSplitContainer.Panel1.SuspendLayout();
			this._RawSplitContainer.Panel2.SuspendLayout();
			this._RawSplitContainer.SuspendLayout();
			this._RootToolStrip.SuspendLayout();
			this._RootTabControl.SuspendLayout();
			this._PlayerRootTabPage.SuspendLayout();
			this._PlayerRootTabControl.SuspendLayout();
			this._PlayerBasicTabPage.SuspendLayout();
			this._PlayerAppearanceRootTabPage.SuspendLayout();
			this._PlayerAppearanceRootTabControl.SuspendLayout();
			this._PlayerAppearanceColorTabPage.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this._RootVectorParametersBindingSource)).BeginInit();
			this._PlayerAppearanceColorToolStrip.SuspendLayout();
			this._PlayerAppearanceRootToolStrip.SuspendLayout();
			this._PlotRootTabPage.SuspendLayout();
			this._PlotRootTabControl.SuspendLayout();
			this._PlotManualTabPage.SuspendLayout();
			this._PlotManualToolStrip.SuspendLayout();
			this._PlotManualFloatGroupBox.SuspendLayout();
			this._PlotManualIntGroupBox.SuspendLayout();
			this._PlotManualBoolGroupBox.SuspendLayout();
			this._RawTabPage.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this._RootSaveFileBindingSource)).BeginInit();
			this.SuspendLayout();
			// 
			// _RawSplitContainer
			// 
			resources.ApplyResources(this._RawSplitContainer, "_RawSplitContainer");
			this._RawSplitContainer.Name = "_RawSplitContainer";
			// 
			// _RawSplitContainer.Panel1
			// 
			this._RawSplitContainer.Panel1.Controls.Add(this._RawParentPropertyGrid);
			// 
			// _RawSplitContainer.Panel2
			// 
			this._RawSplitContainer.Panel2.Controls.Add(this._RawChildPropertyGrid);
			// 
			// _RawParentPropertyGrid
			// 
			resources.ApplyResources(this._RawParentPropertyGrid, "_RawParentPropertyGrid");
			this._RawParentPropertyGrid.Name = "_RawParentPropertyGrid";
			this._RawParentPropertyGrid.PropertySort = System.Windows.Forms.PropertySort.Categorized;
			this._RawParentPropertyGrid.SelectedGridItemChanged += new System.Windows.Forms.SelectedGridItemChangedEventHandler(this.OnSelectedGridItemChanged);
			// 
			// _RawChildPropertyGrid
			// 
			resources.ApplyResources(this._RawChildPropertyGrid, "_RawChildPropertyGrid");
			this._RawChildPropertyGrid.Name = "_RawChildPropertyGrid";
			this._RawChildPropertyGrid.PropertySort = System.Windows.Forms.PropertySort.Categorized;
			// 
			// _RootToolStrip
			// 
			this._RootToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._RootNewSplitButton,
            this._RootOpenFromGenericButton,
            this._RootSaveToGenericButton,
            this._RootSettingsButton,
            this._RootAboutButton});
			resources.ApplyResources(this._RootToolStrip, "_RootToolStrip");
			this._RootToolStrip.Name = "_RootToolStrip";
			// 
			// _RootNewSplitButton
			// 
			this._RootNewSplitButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this._RootNewSplitButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._RootNewMaleToolStripMenuItem,
            this._RootNewFemaleToolStripMenuItem});
			this._RootNewSplitButton.Image = global::MassEffect2.SaveEdit.Properties.Resources.Editor_SaveFile_New_Generic;
			resources.ApplyResources(this._RootNewSplitButton, "_RootNewSplitButton");
			this._RootNewSplitButton.Name = "_RootNewSplitButton";
			// 
			// _RootNewMaleToolStripMenuItem
			// 
			this._RootNewMaleToolStripMenuItem.Image = global::MassEffect2.SaveEdit.Properties.Resources.Editor_SaveFile_New_Male;
			this._RootNewMaleToolStripMenuItem.Name = "_RootNewMaleToolStripMenuItem";
			resources.ApplyResources(this._RootNewMaleToolStripMenuItem, "_RootNewMaleToolStripMenuItem");
			this._RootNewMaleToolStripMenuItem.Click += new System.EventHandler(this.OnSaveNewMale);
			// 
			// _RootNewFemaleToolStripMenuItem
			// 
			this._RootNewFemaleToolStripMenuItem.Image = global::MassEffect2.SaveEdit.Properties.Resources.Editor_SaveFile_New_Female;
			this._RootNewFemaleToolStripMenuItem.Name = "_RootNewFemaleToolStripMenuItem";
			resources.ApplyResources(this._RootNewFemaleToolStripMenuItem, "_RootNewFemaleToolStripMenuItem");
			this._RootNewFemaleToolStripMenuItem.Click += new System.EventHandler(this.OnSaveNewFemale);
			// 
			// _RootOpenFromGenericButton
			// 
			this._RootOpenFromGenericButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._RootOpenFromCareerMenuItem,
            this._RootOpenFromFileMenuItem});
			this._RootOpenFromGenericButton.Image = global::MassEffect2.SaveEdit.Properties.Resources.Editor_SaveFile_Open_Generic;
			resources.ApplyResources(this._RootOpenFromGenericButton, "_RootOpenFromGenericButton");
			this._RootOpenFromGenericButton.Name = "_RootOpenFromGenericButton";
			this._RootOpenFromGenericButton.ButtonClick += new System.EventHandler(this.OnSaveOpenFromGeneric);
			// 
			// _RootOpenFromCareerMenuItem
			// 
			this._RootOpenFromCareerMenuItem.Name = "_RootOpenFromCareerMenuItem";
			resources.ApplyResources(this._RootOpenFromCareerMenuItem, "_RootOpenFromCareerMenuItem");
			this._RootOpenFromCareerMenuItem.Click += new System.EventHandler(this.OnSaveOpenFromCareer);
			// 
			// _RootOpenFromFileMenuItem
			// 
			this._RootOpenFromFileMenuItem.Name = "_RootOpenFromFileMenuItem";
			resources.ApplyResources(this._RootOpenFromFileMenuItem, "_RootOpenFromFileMenuItem");
			this._RootOpenFromFileMenuItem.Click += new System.EventHandler(this.OnSaveOpenFromFile);
			// 
			// _RootSaveToGenericButton
			// 
			this._RootSaveToGenericButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._RootSaveToCareerMenuItem,
            this._RootSaveToFileMenuItem});
			this._RootSaveToGenericButton.Image = global::MassEffect2.SaveEdit.Properties.Resources.Editor_SaveFile_Save_Generic;
			resources.ApplyResources(this._RootSaveToGenericButton, "_RootSaveToGenericButton");
			this._RootSaveToGenericButton.Name = "_RootSaveToGenericButton";
			this._RootSaveToGenericButton.ButtonClick += new System.EventHandler(this.OnSaveSaveToGeneric);
			// 
			// _RootSaveToCareerMenuItem
			// 
			this._RootSaveToCareerMenuItem.Name = "_RootSaveToCareerMenuItem";
			resources.ApplyResources(this._RootSaveToCareerMenuItem, "_RootSaveToCareerMenuItem");
			this._RootSaveToCareerMenuItem.Click += new System.EventHandler(this.OnSaveSaveToCareer);
			// 
			// _RootSaveToFileMenuItem
			// 
			this._RootSaveToFileMenuItem.Name = "_RootSaveToFileMenuItem";
			resources.ApplyResources(this._RootSaveToFileMenuItem, "_RootSaveToFileMenuItem");
			this._RootSaveToFileMenuItem.Click += new System.EventHandler(this.OnSaveSaveToFile);
			// 
			// _RootSettingsButton
			// 
			this._RootSettingsButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this._RootSettingsButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._RootDontUseCareerPickerToolStripMenuItem});
			this._RootSettingsButton.Image = global::MassEffect2.SaveEdit.Properties.Resources.Editor_Settings;
			resources.ApplyResources(this._RootSettingsButton, "_RootSettingsButton");
			this._RootSettingsButton.Name = "_RootSettingsButton";
			// 
			// _RootDontUseCareerPickerToolStripMenuItem
			// 
			this._RootDontUseCareerPickerToolStripMenuItem.CheckOnClick = true;
			this._RootDontUseCareerPickerToolStripMenuItem.Name = "_RootDontUseCareerPickerToolStripMenuItem";
			resources.ApplyResources(this._RootDontUseCareerPickerToolStripMenuItem, "_RootDontUseCareerPickerToolStripMenuItem");
			// 
			// _RootAboutButton
			// 
			this._RootAboutButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
			this._RootAboutButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			resources.ApplyResources(this._RootAboutButton, "_RootAboutButton");
			this._RootAboutButton.Image = global::MassEffect2.SaveEdit.Properties.Resources.Editor_About;
			this._RootAboutButton.Name = "_RootAboutButton";
			// 
			// _RootTabControl
			// 
			resources.ApplyResources(this._RootTabControl, "_RootTabControl");
			this._RootTabControl.Controls.Add(this._PlayerRootTabPage);
			this._RootTabControl.Controls.Add(this._PlotRootTabPage);
			this._RootTabControl.Controls.Add(this._RawTabPage);
			this._RootTabControl.ImageList = this._RootIconImageList;
			this._RootTabControl.Name = "_RootTabControl";
			this._RootTabControl.SelectedIndex = 0;
			this._RootTabControl.TabIndexChanged += new System.EventHandler(this.OnRootTabIndexChanged);
			// 
			// _PlayerRootTabPage
			// 
			this._PlayerRootTabPage.Controls.Add(this._PlayerRootTabControl);
			resources.ApplyResources(this._PlayerRootTabPage, "_PlayerRootTabPage");
			this._PlayerRootTabPage.Name = "_PlayerRootTabPage";
			this._PlayerRootTabPage.UseVisualStyleBackColor = true;
			// 
			// _PlayerRootTabControl
			// 
			resources.ApplyResources(this._PlayerRootTabControl, "_PlayerRootTabControl");
			this._PlayerRootTabControl.Controls.Add(this._PlayerBasicTabPage);
			this._PlayerRootTabControl.Controls.Add(this._PlayerAppearanceRootTabPage);
			this._PlayerRootTabControl.ImageList = this._RootIconImageList;
			this._PlayerRootTabControl.Name = "_PlayerRootTabControl";
			this._PlayerRootTabControl.SelectedIndex = 0;
			// 
			// _PlayerBasicTabPage
			// 
			this._PlayerBasicTabPage.Controls.Add(this._PlayerBasicPanel);
			this._PlayerBasicTabPage.Controls.Add(this._PlayerBasicGenderWarningLabel);
			resources.ApplyResources(this._PlayerBasicTabPage, "_PlayerBasicTabPage");
			this._PlayerBasicTabPage.Name = "_PlayerBasicTabPage";
			this._PlayerBasicTabPage.UseVisualStyleBackColor = true;
			// 
			// _PlayerBasicPanel
			// 
			resources.ApplyResources(this._PlayerBasicPanel, "_PlayerBasicPanel");
			this._PlayerBasicPanel.Name = "_PlayerBasicPanel";
			// 
			// _PlayerBasicGenderWarningLabel
			// 
			resources.ApplyResources(this._PlayerBasicGenderWarningLabel, "_PlayerBasicGenderWarningLabel");
			this._PlayerBasicGenderWarningLabel.Name = "_PlayerBasicGenderWarningLabel";
			// 
			// _PlayerAppearanceRootTabPage
			// 
			this._PlayerAppearanceRootTabPage.Controls.Add(this._PlayerAppearanceRootTabControl);
			this._PlayerAppearanceRootTabPage.Controls.Add(this._PlayerAppearanceRootToolStrip);
			resources.ApplyResources(this._PlayerAppearanceRootTabPage, "_PlayerAppearanceRootTabPage");
			this._PlayerAppearanceRootTabPage.Name = "_PlayerAppearanceRootTabPage";
			this._PlayerAppearanceRootTabPage.UseVisualStyleBackColor = true;
			// 
			// _PlayerAppearanceRootTabControl
			// 
			resources.ApplyResources(this._PlayerAppearanceRootTabControl, "_PlayerAppearanceRootTabControl");
			this._PlayerAppearanceRootTabControl.Controls.Add(this._PlayerAppearanceColorTabPage);
			this._PlayerAppearanceRootTabControl.ImageList = this._RootIconImageList;
			this._PlayerAppearanceRootTabControl.Name = "_PlayerAppearanceRootTabControl";
			this._PlayerAppearanceRootTabControl.SelectedIndex = 0;
			// 
			// _PlayerAppearanceColorTabPage
			// 
			this._PlayerAppearanceColorTabPage.Controls.Add(this._PlayerAppearanceColorListBox);
			this._PlayerAppearanceColorTabPage.Controls.Add(this._PlayerAppearanceColorToolStrip);
			resources.ApplyResources(this._PlayerAppearanceColorTabPage, "_PlayerAppearanceColorTabPage");
			this._PlayerAppearanceColorTabPage.Name = "_PlayerAppearanceColorTabPage";
			this._PlayerAppearanceColorTabPage.UseVisualStyleBackColor = true;
			// 
			// _PlayerAppearanceColorListBox
			// 
			this._PlayerAppearanceColorListBox.DataSource = this._RootVectorParametersBindingSource;
			this._PlayerAppearanceColorListBox.DisplayMember = "Name";
			resources.ApplyResources(this._PlayerAppearanceColorListBox, "_PlayerAppearanceColorListBox");
			this._PlayerAppearanceColorListBox.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
			this._PlayerAppearanceColorListBox.FormattingEnabled = true;
			this._PlayerAppearanceColorListBox.Name = "_PlayerAppearanceColorListBox";
			this._PlayerAppearanceColorListBox.ValueMember = "Name";
			this._PlayerAppearanceColorListBox.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.OnDrawColorListBoxItem);
			this._PlayerAppearanceColorListBox.DoubleClick += new System.EventHandler(this.OnPlayerAppearanceColorChange);
			// 
			// _RootVectorParametersBindingSource
			// 
			this._RootVectorParametersBindingSource.DataSource = typeof(MassEffect2.SaveFormats.MorphHead.VectorParameter);
			// 
			// _PlayerAppearanceColorToolStrip
			// 
			this._PlayerAppearanceColorToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._PlayerAppearanceColorAddColorButton,
            this._PlayerAppearanceColorRemoveColorButton,
            this.toolStripSeparator1,
            this._PlayerAppearanceColorChangeColorButton});
			resources.ApplyResources(this._PlayerAppearanceColorToolStrip, "_PlayerAppearanceColorToolStrip");
			this._PlayerAppearanceColorToolStrip.Name = "_PlayerAppearanceColorToolStrip";
			// 
			// _PlayerAppearanceColorAddColorButton
			// 
			this._PlayerAppearanceColorAddColorButton.Image = global::MassEffect2.SaveEdit.Properties.Resources.Editor_Player_Appearance_Color_Add;
			resources.ApplyResources(this._PlayerAppearanceColorAddColorButton, "_PlayerAppearanceColorAddColorButton");
			this._PlayerAppearanceColorAddColorButton.Name = "_PlayerAppearanceColorAddColorButton";
			this._PlayerAppearanceColorAddColorButton.Click += new System.EventHandler(this.OnPlayerAppearanceColorAdd);
			// 
			// _PlayerAppearanceColorRemoveColorButton
			// 
			this._PlayerAppearanceColorRemoveColorButton.Image = global::MassEffect2.SaveEdit.Properties.Resources.Editor_Player_Appearance_Color_Remove;
			resources.ApplyResources(this._PlayerAppearanceColorRemoveColorButton, "_PlayerAppearanceColorRemoveColorButton");
			this._PlayerAppearanceColorRemoveColorButton.Name = "_PlayerAppearanceColorRemoveColorButton";
			this._PlayerAppearanceColorRemoveColorButton.Click += new System.EventHandler(this.OnPlayerAppearanceColorRemove);
			// 
			// toolStripSeparator1
			// 
			this.toolStripSeparator1.Name = "toolStripSeparator1";
			resources.ApplyResources(this.toolStripSeparator1, "toolStripSeparator1");
			// 
			// _PlayerAppearanceColorChangeColorButton
			// 
			this._PlayerAppearanceColorChangeColorButton.Image = global::MassEffect2.SaveEdit.Properties.Resources.Editor_Player_Appearance_Color_Change;
			resources.ApplyResources(this._PlayerAppearanceColorChangeColorButton, "_PlayerAppearanceColorChangeColorButton");
			this._PlayerAppearanceColorChangeColorButton.Name = "_PlayerAppearanceColorChangeColorButton";
			this._PlayerAppearanceColorChangeColorButton.Click += new System.EventHandler(this.OnPlayerAppearanceColorChange);
			// 
			// _RootIconImageList
			// 
			this._RootIconImageList.ColorDepth = System.Windows.Forms.ColorDepth.Depth8Bit;
			resources.ApplyResources(this._RootIconImageList, "_RootIconImageList");
			this._RootIconImageList.TransparentColor = System.Drawing.Color.Transparent;
			// 
			// _PlayerAppearanceRootToolStrip
			// 
			this._PlayerAppearanceRootToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._PlayerAppearanceMorphHeadDropDownButton,
            this._PlayerAppearancePresetDropDownButton});
			resources.ApplyResources(this._PlayerAppearanceRootToolStrip, "_PlayerAppearanceRootToolStrip");
			this._PlayerAppearanceRootToolStrip.Name = "_PlayerAppearanceRootToolStrip";
			// 
			// _PlayerAppearanceMorphHeadDropDownButton
			// 
			this._PlayerAppearanceMorphHeadDropDownButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._PlayerAppearanceMorphHeadImportButton,
            this._PlayerAppearanceMorphHeadExportButton});
			this._PlayerAppearanceMorphHeadDropDownButton.Image = global::MassEffect2.SaveEdit.Properties.Resources.Editor_Player_Appearance_MorphHead_Generic;
			resources.ApplyResources(this._PlayerAppearanceMorphHeadDropDownButton, "_PlayerAppearanceMorphHeadDropDownButton");
			this._PlayerAppearanceMorphHeadDropDownButton.Name = "_PlayerAppearanceMorphHeadDropDownButton";
			// 
			// _PlayerAppearanceMorphHeadImportButton
			// 
			this._PlayerAppearanceMorphHeadImportButton.Image = global::MassEffect2.SaveEdit.Properties.Resources.Editor_Player_Appearance_MorphHead_Import;
			this._PlayerAppearanceMorphHeadImportButton.Name = "_PlayerAppearanceMorphHeadImportButton";
			resources.ApplyResources(this._PlayerAppearanceMorphHeadImportButton, "_PlayerAppearanceMorphHeadImportButton");
			this._PlayerAppearanceMorphHeadImportButton.Click += new System.EventHandler(this.OnImportHeadMorph);
			// 
			// _PlayerAppearanceMorphHeadExportButton
			// 
			this._PlayerAppearanceMorphHeadExportButton.Image = global::MassEffect2.SaveEdit.Properties.Resources.Editor_Player_Appearance_MorphHead_Export;
			this._PlayerAppearanceMorphHeadExportButton.Name = "_PlayerAppearanceMorphHeadExportButton";
			resources.ApplyResources(this._PlayerAppearanceMorphHeadExportButton, "_PlayerAppearanceMorphHeadExportButton");
			this._PlayerAppearanceMorphHeadExportButton.Click += new System.EventHandler(this.OnExportHeadMorph);
			// 
			// _PlayerAppearancePresetDropDownButton
			// 
			this._PlayerAppearancePresetDropDownButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._PlayerAppearancePresetOpenFromFileButton,
            this._PlayerAppearancePresetSaveToFileButton});
			this._PlayerAppearancePresetDropDownButton.Image = global::MassEffect2.SaveEdit.Properties.Resources.Editor_Player_Appearance_Preset_Generic;
			resources.ApplyResources(this._PlayerAppearancePresetDropDownButton, "_PlayerAppearancePresetDropDownButton");
			this._PlayerAppearancePresetDropDownButton.Name = "_PlayerAppearancePresetDropDownButton";
			// 
			// _PlayerAppearancePresetOpenFromFileButton
			// 
			this._PlayerAppearancePresetOpenFromFileButton.Name = "_PlayerAppearancePresetOpenFromFileButton";
			resources.ApplyResources(this._PlayerAppearancePresetOpenFromFileButton, "_PlayerAppearancePresetOpenFromFileButton");
			this._PlayerAppearancePresetOpenFromFileButton.Click += new System.EventHandler(this.OnLoadAppearancePresetFromFile);
			// 
			// _PlayerAppearancePresetSaveToFileButton
			// 
			this._PlayerAppearancePresetSaveToFileButton.Name = "_PlayerAppearancePresetSaveToFileButton";
			resources.ApplyResources(this._PlayerAppearancePresetSaveToFileButton, "_PlayerAppearancePresetSaveToFileButton");
			this._PlayerAppearancePresetSaveToFileButton.Click += new System.EventHandler(this.OnSaveAppearancePresetToFile);
			// 
			// _PlotRootTabPage
			// 
			this._PlotRootTabPage.Controls.Add(this._PlotRootTabControl);
			resources.ApplyResources(this._PlotRootTabPage, "_PlotRootTabPage");
			this._PlotRootTabPage.Name = "_PlotRootTabPage";
			this._PlotRootTabPage.UseVisualStyleBackColor = true;
			// 
			// _PlotRootTabControl
			// 
			resources.ApplyResources(this._PlotRootTabControl, "_PlotRootTabControl");
			this._PlotRootTabControl.Controls.Add(this._PlotManualTabPage);
			this._PlotRootTabControl.ImageList = this._RootIconImageList;
			this._PlotRootTabControl.Name = "_PlotRootTabControl";
			this._PlotRootTabControl.SelectedIndex = 0;
			// 
			// _PlotManualTabPage
			// 
			this._PlotManualTabPage.Controls.Add(this._PlotManualToolStrip);
			this._PlotManualTabPage.Controls.Add(this._PlotManualLogTextBox);
			this._PlotManualTabPage.Controls.Add(this._PlotManualFloatGroupBox);
			this._PlotManualTabPage.Controls.Add(this._PlotManualIntGroupBox);
			this._PlotManualTabPage.Controls.Add(this._PlotManualBoolGroupBox);
			resources.ApplyResources(this._PlotManualTabPage, "_PlotManualTabPage");
			this._PlotManualTabPage.Name = "_PlotManualTabPage";
			this._PlotManualTabPage.UseVisualStyleBackColor = true;
			// 
			// _PlotManualToolStrip
			// 
			resources.ApplyResources(this._PlotManualToolStrip, "_PlotManualToolStrip");
			this._PlotManualToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._PlotManualClearLogButton});
			this._PlotManualToolStrip.Name = "_PlotManualToolStrip";
			// 
			// _PlotManualClearLogButton
			// 
			this._PlotManualClearLogButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
			this._PlotManualClearLogButton.Image = global::MassEffect2.SaveEdit.Properties.Resources.Editor_Plot_Manual_ClearLog;
			resources.ApplyResources(this._PlotManualClearLogButton, "_PlotManualClearLogButton");
			this._PlotManualClearLogButton.Name = "_PlotManualClearLogButton";
			this._PlotManualClearLogButton.Click += new System.EventHandler(this.OnPlotManualClearLog);
			// 
			// _PlotManualLogTextBox
			// 
			resources.ApplyResources(this._PlotManualLogTextBox, "_PlotManualLogTextBox");
			this._PlotManualLogTextBox.BackColor = System.Drawing.SystemColors.Window;
			this._PlotManualLogTextBox.Name = "_PlotManualLogTextBox";
			this._PlotManualLogTextBox.ReadOnly = true;
			// 
			// _PlotManualFloatGroupBox
			// 
			this._PlotManualFloatGroupBox.Controls.Add(this._PlotManualFloatIdTextBox);
			this._PlotManualFloatGroupBox.Controls.Add(this._PlotManualFloatGetButton);
			this._PlotManualFloatGroupBox.Controls.Add(this._PlotManualFloatSetButton);
			this._PlotManualFloatGroupBox.Controls.Add(this._PlotManualFloatValueTextBox);
			resources.ApplyResources(this._PlotManualFloatGroupBox, "_PlotManualFloatGroupBox");
			this._PlotManualFloatGroupBox.Name = "_PlotManualFloatGroupBox";
			this._PlotManualFloatGroupBox.TabStop = false;
			// 
			// _PlotManualFloatIdTextBox
			// 
			resources.ApplyResources(this._PlotManualFloatIdTextBox, "_PlotManualFloatIdTextBox");
			this._PlotManualFloatIdTextBox.Name = "_PlotManualFloatIdTextBox";
			// 
			// _PlotManualFloatGetButton
			// 
			resources.ApplyResources(this._PlotManualFloatGetButton, "_PlotManualFloatGetButton");
			this._PlotManualFloatGetButton.Name = "_PlotManualFloatGetButton";
			this._PlotManualFloatGetButton.UseVisualStyleBackColor = true;
			this._PlotManualFloatGetButton.Click += new System.EventHandler(this.OnPlotManualGetFloat);
			// 
			// _PlotManualFloatSetButton
			// 
			resources.ApplyResources(this._PlotManualFloatSetButton, "_PlotManualFloatSetButton");
			this._PlotManualFloatSetButton.Name = "_PlotManualFloatSetButton";
			this._PlotManualFloatSetButton.UseVisualStyleBackColor = true;
			this._PlotManualFloatSetButton.Click += new System.EventHandler(this.OnPlotManualSetFloat);
			// 
			// _PlotManualFloatValueTextBox
			// 
			resources.ApplyResources(this._PlotManualFloatValueTextBox, "_PlotManualFloatValueTextBox");
			this._PlotManualFloatValueTextBox.Name = "_PlotManualFloatValueTextBox";
			// 
			// _PlotManualIntGroupBox
			// 
			this._PlotManualIntGroupBox.Controls.Add(this._PlotManualIntIdTextBox);
			this._PlotManualIntGroupBox.Controls.Add(this._PlotManualIntGetButton);
			this._PlotManualIntGroupBox.Controls.Add(this._PlotManualIntSetButton);
			this._PlotManualIntGroupBox.Controls.Add(this._PlotManualIntValueTextBox);
			resources.ApplyResources(this._PlotManualIntGroupBox, "_PlotManualIntGroupBox");
			this._PlotManualIntGroupBox.Name = "_PlotManualIntGroupBox";
			this._PlotManualIntGroupBox.TabStop = false;
			// 
			// _PlotManualIntIdTextBox
			// 
			resources.ApplyResources(this._PlotManualIntIdTextBox, "_PlotManualIntIdTextBox");
			this._PlotManualIntIdTextBox.Name = "_PlotManualIntIdTextBox";
			// 
			// _PlotManualIntGetButton
			// 
			resources.ApplyResources(this._PlotManualIntGetButton, "_PlotManualIntGetButton");
			this._PlotManualIntGetButton.Name = "_PlotManualIntGetButton";
			this._PlotManualIntGetButton.UseVisualStyleBackColor = true;
			this._PlotManualIntGetButton.Click += new System.EventHandler(this.OnPlotManualGetInt);
			// 
			// _PlotManualIntSetButton
			// 
			resources.ApplyResources(this._PlotManualIntSetButton, "_PlotManualIntSetButton");
			this._PlotManualIntSetButton.Name = "_PlotManualIntSetButton";
			this._PlotManualIntSetButton.UseVisualStyleBackColor = true;
			this._PlotManualIntSetButton.Click += new System.EventHandler(this.OnPlotManualSetInt);
			// 
			// _PlotManualIntValueTextBox
			// 
			resources.ApplyResources(this._PlotManualIntValueTextBox, "_PlotManualIntValueTextBox");
			this._PlotManualIntValueTextBox.Name = "_PlotManualIntValueTextBox";
			// 
			// _PlotManualBoolGroupBox
			// 
			this._PlotManualBoolGroupBox.Controls.Add(this._PlotManualBoolIdTextBox);
			this._PlotManualBoolGroupBox.Controls.Add(this._PlotManualBoolSetButton);
			this._PlotManualBoolGroupBox.Controls.Add(this._PlotManualBoolGetButton);
			this._PlotManualBoolGroupBox.Controls.Add(this._PlotManualBoolValueCheckBox);
			resources.ApplyResources(this._PlotManualBoolGroupBox, "_PlotManualBoolGroupBox");
			this._PlotManualBoolGroupBox.Name = "_PlotManualBoolGroupBox";
			this._PlotManualBoolGroupBox.TabStop = false;
			// 
			// _PlotManualBoolIdTextBox
			// 
			resources.ApplyResources(this._PlotManualBoolIdTextBox, "_PlotManualBoolIdTextBox");
			this._PlotManualBoolIdTextBox.Name = "_PlotManualBoolIdTextBox";
			// 
			// _PlotManualBoolSetButton
			// 
			resources.ApplyResources(this._PlotManualBoolSetButton, "_PlotManualBoolSetButton");
			this._PlotManualBoolSetButton.Name = "_PlotManualBoolSetButton";
			this._PlotManualBoolSetButton.UseVisualStyleBackColor = true;
			this._PlotManualBoolSetButton.Click += new System.EventHandler(this.OnPlotManualSetBool);
			// 
			// _PlotManualBoolGetButton
			// 
			resources.ApplyResources(this._PlotManualBoolGetButton, "_PlotManualBoolGetButton");
			this._PlotManualBoolGetButton.Name = "_PlotManualBoolGetButton";
			this._PlotManualBoolGetButton.UseVisualStyleBackColor = true;
			this._PlotManualBoolGetButton.Click += new System.EventHandler(this.OnPlotManualGetBool);
			// 
			// _PlotManualBoolValueCheckBox
			// 
			resources.ApplyResources(this._PlotManualBoolValueCheckBox, "_PlotManualBoolValueCheckBox");
			this._PlotManualBoolValueCheckBox.Name = "_PlotManualBoolValueCheckBox";
			this._PlotManualBoolValueCheckBox.UseVisualStyleBackColor = true;
			// 
			// _RawTabPage
			// 
			this._RawTabPage.Controls.Add(this._RawSplitContainer);
			resources.ApplyResources(this._RawTabPage, "_RawTabPage");
			this._RawTabPage.Name = "_RawTabPage";
			this._RawTabPage.UseVisualStyleBackColor = true;
			// 
			// _RootSaveFileBindingSource
			// 
			this._RootSaveFileBindingSource.DataSource = typeof(MassEffect2.SaveFormats.SFXSaveGameFile);
			// 
			// _RootSaveGameOpenFileDialog
			// 
			this._RootSaveGameOpenFileDialog.DefaultExt = "pcsav";
			resources.ApplyResources(this._RootSaveGameOpenFileDialog, "_RootSaveGameOpenFileDialog");
			this._RootSaveGameOpenFileDialog.RestoreDirectory = true;
			// 
			// _RootSaveGameSaveFileDialog
			// 
			resources.ApplyResources(this._RootSaveGameSaveFileDialog, "_RootSaveGameSaveFileDialog");
			this._RootSaveGameSaveFileDialog.RestoreDirectory = true;
			// 
			// _RootMorphHeadOpenFileDialog
			// 
			resources.ApplyResources(this._RootMorphHeadOpenFileDialog, "_RootMorphHeadOpenFileDialog");
			this._RootMorphHeadOpenFileDialog.RestoreDirectory = true;
			// 
			// _RootMorphHeadSaveFileDialog
			// 
			resources.ApplyResources(this._RootMorphHeadSaveFileDialog, "_RootMorphHeadSaveFileDialog");
			this._RootMorphHeadSaveFileDialog.RestoreDirectory = true;
			// 
			// _RootAppearancePresetOpenFileDialog
			// 
			resources.ApplyResources(this._RootAppearancePresetOpenFileDialog, "_RootAppearancePresetOpenFileDialog");
			this._RootAppearancePresetOpenFileDialog.RestoreDirectory = true;
			// 
			// _RootAppearancePresetSaveFileDialog
			// 
			resources.ApplyResources(this._RootAppearancePresetSaveFileDialog, "_RootAppearancePresetSaveFileDialog");
			this._RootAppearancePresetSaveFileDialog.RestoreDirectory = true;
			// 
			// Editor
			// 
			resources.ApplyResources(this, "$this");
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this._RootTabControl);
			this.Controls.Add(this._RootToolStrip);
			this.Name = "Editor";
			this._RawSplitContainer.Panel1.ResumeLayout(false);
			this._RawSplitContainer.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this._RawSplitContainer)).EndInit();
			this._RawSplitContainer.ResumeLayout(false);
			this._RootToolStrip.ResumeLayout(false);
			this._RootToolStrip.PerformLayout();
			this._RootTabControl.ResumeLayout(false);
			this._PlayerRootTabPage.ResumeLayout(false);
			this._PlayerRootTabControl.ResumeLayout(false);
			this._PlayerBasicTabPage.ResumeLayout(false);
			this._PlayerBasicTabPage.PerformLayout();
			this._PlayerAppearanceRootTabPage.ResumeLayout(false);
			this._PlayerAppearanceRootTabPage.PerformLayout();
			this._PlayerAppearanceRootTabControl.ResumeLayout(false);
			this._PlayerAppearanceColorTabPage.ResumeLayout(false);
			this._PlayerAppearanceColorTabPage.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this._RootVectorParametersBindingSource)).EndInit();
			this._PlayerAppearanceColorToolStrip.ResumeLayout(false);
			this._PlayerAppearanceColorToolStrip.PerformLayout();
			this._PlayerAppearanceRootToolStrip.ResumeLayout(false);
			this._PlayerAppearanceRootToolStrip.PerformLayout();
			this._PlotRootTabPage.ResumeLayout(false);
			this._PlotRootTabControl.ResumeLayout(false);
			this._PlotManualTabPage.ResumeLayout(false);
			this._PlotManualTabPage.PerformLayout();
			this._PlotManualToolStrip.ResumeLayout(false);
			this._PlotManualToolStrip.PerformLayout();
			this._PlotManualFloatGroupBox.ResumeLayout(false);
			this._PlotManualFloatGroupBox.PerformLayout();
			this._PlotManualIntGroupBox.ResumeLayout(false);
			this._PlotManualIntGroupBox.PerformLayout();
			this._PlotManualBoolGroupBox.ResumeLayout(false);
			this._PlotManualBoolGroupBox.PerformLayout();
			this._RawTabPage.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this._RootSaveFileBindingSource)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStrip _RootToolStrip;
        private System.Windows.Forms.TabControl _RootTabControl;
        private System.Windows.Forms.TabPage _PlayerRootTabPage;
        private System.Windows.Forms.TabPage _RawTabPage;
        private System.Windows.Forms.ToolStripSplitButton _RootOpenFromGenericButton;
        private System.Windows.Forms.OpenFileDialog _RootSaveGameOpenFileDialog;
        private System.Windows.Forms.SplitContainer _RawSplitContainer;
        private System.Windows.Forms.PropertyGrid _RawParentPropertyGrid;
        private System.Windows.Forms.PropertyGrid _RawChildPropertyGrid;
        private System.Windows.Forms.ImageList _RootIconImageList;
        private System.Windows.Forms.ToolStripSplitButton _RootSaveToGenericButton;
        private System.Windows.Forms.ToolStripDropDownButton _RootNewSplitButton;
        private System.Windows.Forms.TabControl _PlayerRootTabControl;
        private System.Windows.Forms.TabPage _PlayerBasicTabPage;
        private System.Windows.Forms.TabPage _PlayerAppearanceRootTabPage;
        private System.Windows.Forms.ToolStripMenuItem _RootNewMaleToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem _RootNewFemaleToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem _RootOpenFromCareerMenuItem;
        private System.Windows.Forms.ToolStripMenuItem _RootOpenFromFileMenuItem;
        private System.Windows.Forms.ToolStripMenuItem _RootSaveToCareerMenuItem;
        private System.Windows.Forms.ToolStripMenuItem _RootSaveToFileMenuItem;
        private System.Windows.Forms.SaveFileDialog _RootSaveGameSaveFileDialog;
        private System.Windows.Forms.OpenFileDialog _RootMorphHeadOpenFileDialog;
        private System.Windows.Forms.SaveFileDialog _RootMorphHeadSaveFileDialog;
        private System.Windows.Forms.ToolStrip _PlayerAppearanceRootToolStrip;
        private System.Windows.Forms.ToolStripDropDownButton _RootSettingsButton;
        private System.Windows.Forms.ToolStripMenuItem _RootDontUseCareerPickerToolStripMenuItem;
		private System.Windows.Forms.TabPage _PlotRootTabPage;
        private System.Windows.Forms.ToolStripDropDownButton _PlayerAppearancePresetDropDownButton;
        private System.Windows.Forms.ToolStripMenuItem _PlayerAppearancePresetOpenFromFileButton;
        private System.Windows.Forms.OpenFileDialog _RootAppearancePresetOpenFileDialog;
        private System.Windows.Forms.ToolStripMenuItem _PlayerAppearancePresetSaveToFileButton;
        private System.Windows.Forms.SaveFileDialog _RootAppearancePresetSaveFileDialog;
        private System.Windows.Forms.ToolStripDropDownButton _PlayerAppearanceMorphHeadDropDownButton;
        private System.Windows.Forms.ToolStripMenuItem _PlayerAppearanceMorphHeadImportButton;
        private System.Windows.Forms.ToolStripMenuItem _PlayerAppearanceMorphHeadExportButton;
        private System.Windows.Forms.TabControl _PlayerAppearanceRootTabControl;
        private System.Windows.Forms.TabPage _PlayerAppearanceColorTabPage;
        private System.Windows.Forms.ListBox _PlayerAppearanceColorListBox;
        private System.Windows.Forms.ToolStrip _PlayerAppearanceColorToolStrip;
        private System.Windows.Forms.ToolStripButton _PlayerAppearanceColorAddColorButton;
        private System.Windows.Forms.ToolStripButton _PlayerAppearanceColorRemoveColorButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton _PlayerAppearanceColorChangeColorButton;
        private System.Windows.Forms.FlowLayoutPanel _PlayerBasicPanel;
        internal System.Windows.Forms.BindingSource _RootSaveFileBindingSource;
        internal System.Windows.Forms.BindingSource _RootVectorParametersBindingSource;
        private System.Windows.Forms.Label _PlayerBasicGenderWarningLabel;
		private System.Windows.Forms.ToolStripButton _RootAboutButton;
		private System.Windows.Forms.TabControl _PlotRootTabControl;
		private System.Windows.Forms.TabPage _PlotManualTabPage;
		private System.Windows.Forms.ToolStrip _PlotManualToolStrip;
		private System.Windows.Forms.ToolStripButton _PlotManualClearLogButton;
		private System.Windows.Forms.TextBox _PlotManualLogTextBox;
		private System.Windows.Forms.GroupBox _PlotManualFloatGroupBox;
		private System.Windows.Forms.TextBox _PlotManualFloatIdTextBox;
		private System.Windows.Forms.Button _PlotManualFloatGetButton;
		private System.Windows.Forms.Button _PlotManualFloatSetButton;
		private System.Windows.Forms.TextBox _PlotManualFloatValueTextBox;
		private System.Windows.Forms.GroupBox _PlotManualIntGroupBox;
		private System.Windows.Forms.TextBox _PlotManualIntIdTextBox;
		private System.Windows.Forms.Button _PlotManualIntGetButton;
		private System.Windows.Forms.Button _PlotManualIntSetButton;
		private System.Windows.Forms.TextBox _PlotManualIntValueTextBox;
		private System.Windows.Forms.GroupBox _PlotManualBoolGroupBox;
		private System.Windows.Forms.TextBox _PlotManualBoolIdTextBox;
		private System.Windows.Forms.Button _PlotManualBoolSetButton;
		private System.Windows.Forms.Button _PlotManualBoolGetButton;
		private System.Windows.Forms.CheckBox _PlotManualBoolValueCheckBox;
    }
}

