using MassEffect3.SaveFormats;

namespace MassEffect3.SaveEdit
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
			this._rawSplitContainer = new System.Windows.Forms.SplitContainer();
			this._rawParentPropertyGrid = new System.Windows.Forms.PropertyGrid();
			this._rawChildPropertyGrid = new System.Windows.Forms.PropertyGrid();
			this._rootToolStrip = new System.Windows.Forms.ToolStrip();
			this._rootNewSplitButton = new System.Windows.Forms.ToolStripDropDownButton();
			this._rootNewMaleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this._rootNewFemaleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this._rootOpenFromGenericButton = new System.Windows.Forms.ToolStripSplitButton();
			this._rootOpenFromCareerMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this._rootOpenFromFileMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this._rootSaveToGenericButton = new System.Windows.Forms.ToolStripSplitButton();
			this._rootSaveToCareerMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this._rootSaveToFileMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this._rootSettingsButton = new System.Windows.Forms.ToolStripDropDownButton();
			this._useCareerPickerMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this._backupAutoSavesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.compareToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this._rootAboutButton = new System.Windows.Forms.ToolStripButton();
			this._rootTabControl = new System.Windows.Forms.TabControl();
			this._playerRootTabPage = new System.Windows.Forms.TabPage();
			this._playerRootTabControl = new System.Windows.Forms.TabControl();
			this._playerBasicTabPage = new System.Windows.Forms.TabPage();
			this._playerBasicPanel = new System.Windows.Forms.FlowLayoutPanel();
			this._playerBasicGenderWarningLabel = new System.Windows.Forms.Label();
			this._playerAppearanceRootTabPage = new System.Windows.Forms.TabPage();
			this._playerAppearanceRootTabControl = new System.Windows.Forms.TabControl();
			this._playerAppearanceColorTabPage = new System.Windows.Forms.TabPage();
			this._playerAppearanceColorListBox = new System.Windows.Forms.ListBox();
			this._rootVectorParametersBindingSource = new System.Windows.Forms.BindingSource(this.components);
			this._playerAppearanceColorToolStrip = new System.Windows.Forms.ToolStrip();
			this._playerAppearanceColorAddColorButton = new System.Windows.Forms.ToolStripButton();
			this._playerAppearanceColorRemoveColorButton = new System.Windows.Forms.ToolStripButton();
			this._toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
			this._playerAppearanceColorChangeColorButton = new System.Windows.Forms.ToolStripButton();
			this._rootIconImageList = new System.Windows.Forms.ImageList(this.components);
			this._playerAppearanceRootToolStrip = new System.Windows.Forms.ToolStrip();
			this._playerAppearanceMorphHeadDropDownButton = new System.Windows.Forms.ToolStripDropDownButton();
			this._playerAppearanceMorphHeadImportButton = new System.Windows.Forms.ToolStripMenuItem();
			this._playerAppearanceMorphHeadExportButton = new System.Windows.Forms.ToolStripMenuItem();
			this._playerAppearancePresetDropDownButton = new System.Windows.Forms.ToolStripDropDownButton();
			this._playerAppearancePresetOpenFromFileButton = new System.Windows.Forms.ToolStripMenuItem();
			this._playerAppearancePresetSaveToFileButton = new System.Windows.Forms.ToolStripMenuItem();
			this._squadRootTabPage = new System.Windows.Forms.TabPage();
			this._squadRootTabControl = new System.Windows.Forms.TabControl();
			this._squadBasicTabPage = new System.Windows.Forms.TabPage();
			this._squadBasicPanel = new System.Windows.Forms.FlowLayoutPanel();
			this._plotRootTabPage = new System.Windows.Forms.TabPage();
			this._plotRootTabControl = new System.Windows.Forms.TabControl();
			this._plotManualTabPage = new System.Windows.Forms.TabPage();
			this._plotManualToolStrip = new System.Windows.Forms.ToolStrip();
			this._plotManualClearLogButton = new System.Windows.Forms.ToolStripButton();
			this._plotManualLogTextBox = new System.Windows.Forms.TextBox();
			this._plotManualFloatGroupBox = new System.Windows.Forms.GroupBox();
			this._plotManualFloatIdTextBox = new System.Windows.Forms.TextBox();
			this._plotManualFloatGetButton = new System.Windows.Forms.Button();
			this._plotManualFloatSetButton = new System.Windows.Forms.Button();
			this._plotManualFloatValueTextBox = new System.Windows.Forms.TextBox();
			this._plotManualIntGroupBox = new System.Windows.Forms.GroupBox();
			this._plotManualIntIdTextBox = new System.Windows.Forms.TextBox();
			this._plotManualIntGetButton = new System.Windows.Forms.Button();
			this._plotManualIntSetButton = new System.Windows.Forms.Button();
			this._plotManualIntValueTextBox = new System.Windows.Forms.TextBox();
			this._plotManualBoolGroupBox = new System.Windows.Forms.GroupBox();
			this._plotManualBoolIdTextBox = new System.Windows.Forms.TextBox();
			this._plotManualBoolSetButton = new System.Windows.Forms.Button();
			this._plotManualBoolGetButton = new System.Windows.Forms.Button();
			this._plotManualBoolValueCheckBox = new System.Windows.Forms.CheckBox();
			this._plotManualPlayerVarTabPage = new System.Windows.Forms.TabPage();
			this._plotManualPlayerVarToolStrip = new System.Windows.Forms.ToolStrip();
			this._plotManualPlayerVarClearLogButton = new System.Windows.Forms.ToolStripButton();
			this._plotManualPlayerVarLogTextBox = new System.Windows.Forms.TextBox();
			this._plotManualPlayerVarGroupBox = new System.Windows.Forms.GroupBox();
			this._plotManualPlayerVarIdTextBox = new System.Windows.Forms.TextBox();
			this._plotManualPlayerVarGetButton = new System.Windows.Forms.Button();
			this._plotManualPlayerVarSetButton = new System.Windows.Forms.Button();
			this._plotManualPlayerVarValueTextBox = new System.Windows.Forms.TextBox();
			this._rawTabPage = new System.Windows.Forms.TabPage();
			this._rootSaveFileBindingSource = new System.Windows.Forms.BindingSource(this.components);
			this._rootSaveGameOpenFileDialog = new System.Windows.Forms.OpenFileDialog();
			this._rootSaveGameSaveFileDialog = new System.Windows.Forms.SaveFileDialog();
			this._rootMorphHeadOpenFileDialog = new System.Windows.Forms.OpenFileDialog();
			this._rootMorphHeadSaveFileDialog = new System.Windows.Forms.SaveFileDialog();
			this._rootAppearancePresetOpenFileDialog = new System.Windows.Forms.OpenFileDialog();
			this._rootAppearancePresetSaveFileDialog = new System.Windows.Forms.SaveFileDialog();
			((System.ComponentModel.ISupportInitialize)(this._rawSplitContainer)).BeginInit();
			this._rawSplitContainer.Panel1.SuspendLayout();
			this._rawSplitContainer.Panel2.SuspendLayout();
			this._rawSplitContainer.SuspendLayout();
			this._rootToolStrip.SuspendLayout();
			this._rootTabControl.SuspendLayout();
			this._playerRootTabPage.SuspendLayout();
			this._playerRootTabControl.SuspendLayout();
			this._playerBasicTabPage.SuspendLayout();
			this._playerAppearanceRootTabPage.SuspendLayout();
			this._playerAppearanceRootTabControl.SuspendLayout();
			this._playerAppearanceColorTabPage.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this._rootVectorParametersBindingSource)).BeginInit();
			this._playerAppearanceColorToolStrip.SuspendLayout();
			this._playerAppearanceRootToolStrip.SuspendLayout();
			this._squadRootTabPage.SuspendLayout();
			this._squadRootTabControl.SuspendLayout();
			this._squadBasicTabPage.SuspendLayout();
			this._plotRootTabPage.SuspendLayout();
			this._plotRootTabControl.SuspendLayout();
			this._plotManualTabPage.SuspendLayout();
			this._plotManualToolStrip.SuspendLayout();
			this._plotManualFloatGroupBox.SuspendLayout();
			this._plotManualIntGroupBox.SuspendLayout();
			this._plotManualBoolGroupBox.SuspendLayout();
			this._plotManualPlayerVarTabPage.SuspendLayout();
			this._plotManualPlayerVarToolStrip.SuspendLayout();
			this._plotManualPlayerVarGroupBox.SuspendLayout();
			this._rawTabPage.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this._rootSaveFileBindingSource)).BeginInit();
			this.SuspendLayout();
			// 
			// _rawSplitContainer
			// 
			resources.ApplyResources(this._rawSplitContainer, "_rawSplitContainer");
			this._rawSplitContainer.Name = "_rawSplitContainer";
			// 
			// _rawSplitContainer.Panel1
			// 
			this._rawSplitContainer.Panel1.Controls.Add(this._rawParentPropertyGrid);
			// 
			// _rawSplitContainer.Panel2
			// 
			this._rawSplitContainer.Panel2.Controls.Add(this._rawChildPropertyGrid);
			// 
			// _rawParentPropertyGrid
			// 
			this._rawParentPropertyGrid.CategoryForeColor = System.Drawing.SystemColors.InactiveCaptionText;
			resources.ApplyResources(this._rawParentPropertyGrid, "_rawParentPropertyGrid");
			this._rawParentPropertyGrid.Name = "_rawParentPropertyGrid";
			this._rawParentPropertyGrid.PropertySort = System.Windows.Forms.PropertySort.Categorized;
			this._rawParentPropertyGrid.SelectedGridItemChanged += new System.Windows.Forms.SelectedGridItemChangedEventHandler(this.OnSelectedGridItemChanged);
			// 
			// _rawChildPropertyGrid
			// 
			this._rawChildPropertyGrid.CategoryForeColor = System.Drawing.SystemColors.InactiveCaptionText;
			resources.ApplyResources(this._rawChildPropertyGrid, "_rawChildPropertyGrid");
			this._rawChildPropertyGrid.Name = "_rawChildPropertyGrid";
			this._rawChildPropertyGrid.PropertySort = System.Windows.Forms.PropertySort.Categorized;
			// 
			// _rootToolStrip
			// 
			this._rootToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._rootNewSplitButton,
            this._rootOpenFromGenericButton,
            this._rootSaveToGenericButton,
            this._rootSettingsButton,
            this._rootAboutButton});
			resources.ApplyResources(this._rootToolStrip, "_rootToolStrip");
			this._rootToolStrip.Name = "_rootToolStrip";
			// 
			// _rootNewSplitButton
			// 
			this._rootNewSplitButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this._rootNewSplitButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._rootNewMaleToolStripMenuItem,
            this._rootNewFemaleToolStripMenuItem});
			this._rootNewSplitButton.Image = global::MassEffect3.SaveEdit.Properties.Resources.Editor_SaveFile_New_Generic;
			resources.ApplyResources(this._rootNewSplitButton, "_rootNewSplitButton");
			this._rootNewSplitButton.Name = "_rootNewSplitButton";
			// 
			// _rootNewMaleToolStripMenuItem
			// 
			this._rootNewMaleToolStripMenuItem.Image = global::MassEffect3.SaveEdit.Properties.Resources.Editor_SaveFile_New_Male;
			this._rootNewMaleToolStripMenuItem.Name = "_rootNewMaleToolStripMenuItem";
			resources.ApplyResources(this._rootNewMaleToolStripMenuItem, "_rootNewMaleToolStripMenuItem");
			this._rootNewMaleToolStripMenuItem.Click += new System.EventHandler(this.OnSaveNewMale);
			// 
			// _rootNewFemaleToolStripMenuItem
			// 
			this._rootNewFemaleToolStripMenuItem.Image = global::MassEffect3.SaveEdit.Properties.Resources.Editor_SaveFile_New_Female;
			this._rootNewFemaleToolStripMenuItem.Name = "_rootNewFemaleToolStripMenuItem";
			resources.ApplyResources(this._rootNewFemaleToolStripMenuItem, "_rootNewFemaleToolStripMenuItem");
			this._rootNewFemaleToolStripMenuItem.Click += new System.EventHandler(this.OnSaveNewFemale);
			// 
			// _rootOpenFromGenericButton
			// 
			this._rootOpenFromGenericButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._rootOpenFromCareerMenuItem,
            this._rootOpenFromFileMenuItem});
			this._rootOpenFromGenericButton.Image = global::MassEffect3.SaveEdit.Properties.Resources.Editor_SaveFile_Open_Generic;
			resources.ApplyResources(this._rootOpenFromGenericButton, "_rootOpenFromGenericButton");
			this._rootOpenFromGenericButton.Name = "_rootOpenFromGenericButton";
			this._rootOpenFromGenericButton.ButtonClick += new System.EventHandler(this.OnSaveOpenFromGeneric);
			// 
			// _rootOpenFromCareerMenuItem
			// 
			this._rootOpenFromCareerMenuItem.Name = "_rootOpenFromCareerMenuItem";
			resources.ApplyResources(this._rootOpenFromCareerMenuItem, "_rootOpenFromCareerMenuItem");
			this._rootOpenFromCareerMenuItem.Click += new System.EventHandler(this.OnSaveOpenFromCareer);
			// 
			// _rootOpenFromFileMenuItem
			// 
			this._rootOpenFromFileMenuItem.Name = "_rootOpenFromFileMenuItem";
			resources.ApplyResources(this._rootOpenFromFileMenuItem, "_rootOpenFromFileMenuItem");
			this._rootOpenFromFileMenuItem.Click += new System.EventHandler(this.OnSaveOpenFromFile);
			// 
			// _rootSaveToGenericButton
			// 
			this._rootSaveToGenericButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._rootSaveToCareerMenuItem,
            this._rootSaveToFileMenuItem});
			this._rootSaveToGenericButton.Image = global::MassEffect3.SaveEdit.Properties.Resources.Editor_SaveFile_Save_Generic;
			resources.ApplyResources(this._rootSaveToGenericButton, "_rootSaveToGenericButton");
			this._rootSaveToGenericButton.Name = "_rootSaveToGenericButton";
			this._rootSaveToGenericButton.ButtonClick += new System.EventHandler(this.OnSaveSaveToGeneric);
			// 
			// _rootSaveToCareerMenuItem
			// 
			this._rootSaveToCareerMenuItem.Name = "_rootSaveToCareerMenuItem";
			resources.ApplyResources(this._rootSaveToCareerMenuItem, "_rootSaveToCareerMenuItem");
			this._rootSaveToCareerMenuItem.Click += new System.EventHandler(this.OnSaveSaveToCareer);
			// 
			// _rootSaveToFileMenuItem
			// 
			this._rootSaveToFileMenuItem.Name = "_rootSaveToFileMenuItem";
			resources.ApplyResources(this._rootSaveToFileMenuItem, "_rootSaveToFileMenuItem");
			this._rootSaveToFileMenuItem.Click += new System.EventHandler(this.OnSaveSaveToFile);
			// 
			// _rootSettingsButton
			// 
			this._rootSettingsButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this._rootSettingsButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._useCareerPickerMenuItem,
            this._backupAutoSavesMenuItem,
            this.compareToolStripMenuItem});
			this._rootSettingsButton.Image = global::MassEffect3.SaveEdit.Properties.Resources.Editor_Settings;
			resources.ApplyResources(this._rootSettingsButton, "_rootSettingsButton");
			this._rootSettingsButton.Name = "_rootSettingsButton";
			// 
			// _useCareerPickerMenuItem
			// 
			this._useCareerPickerMenuItem.Checked = true;
			this._useCareerPickerMenuItem.CheckOnClick = true;
			this._useCareerPickerMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
			this._useCareerPickerMenuItem.Name = "_useCareerPickerMenuItem";
			resources.ApplyResources(this._useCareerPickerMenuItem, "_useCareerPickerMenuItem");
			this._useCareerPickerMenuItem.CheckedChanged += new System.EventHandler(this.UseCareerPickerMenuItem_OnCheckedChanged);
			// 
			// _backupAutoSavesMenuItem
			// 
			this._backupAutoSavesMenuItem.Checked = true;
			this._backupAutoSavesMenuItem.CheckOnClick = true;
			this._backupAutoSavesMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
			this._backupAutoSavesMenuItem.Name = "_backupAutoSavesMenuItem";
			resources.ApplyResources(this._backupAutoSavesMenuItem, "_backupAutoSavesMenuItem");
			this._backupAutoSavesMenuItem.CheckedChanged += new System.EventHandler(this.BackupAutoSavesMenuItem_OnCheckedChanged);
			// 
			// compareToolStripMenuItem
			// 
			this.compareToolStripMenuItem.Name = "compareToolStripMenuItem";
			resources.ApplyResources(this.compareToolStripMenuItem, "compareToolStripMenuItem");
			this.compareToolStripMenuItem.Click += new System.EventHandler(this.compareToolStripMenuItem_Click);
			// 
			// _rootAboutButton
			// 
			this._rootAboutButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
			this._rootAboutButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			resources.ApplyResources(this._rootAboutButton, "_rootAboutButton");
			this._rootAboutButton.Image = global::MassEffect3.SaveEdit.Properties.Resources.Editor_About;
			this._rootAboutButton.Name = "_rootAboutButton";
			// 
			// _rootTabControl
			// 
			resources.ApplyResources(this._rootTabControl, "_rootTabControl");
			this._rootTabControl.Controls.Add(this._playerRootTabPage);
			this._rootTabControl.Controls.Add(this._squadRootTabPage);
			this._rootTabControl.Controls.Add(this._plotRootTabPage);
			this._rootTabControl.Controls.Add(this._rawTabPage);
			this._rootTabControl.ImageList = this._rootIconImageList;
			this._rootTabControl.Name = "_rootTabControl";
			this._rootTabControl.SelectedIndex = 0;
			this._rootTabControl.TabIndexChanged += new System.EventHandler(this.OnRootTabIndexChanged);
			// 
			// _playerRootTabPage
			// 
			this._playerRootTabPage.Controls.Add(this._playerRootTabControl);
			resources.ApplyResources(this._playerRootTabPage, "_playerRootTabPage");
			this._playerRootTabPage.Name = "_playerRootTabPage";
			this._playerRootTabPage.UseVisualStyleBackColor = true;
			// 
			// _playerRootTabControl
			// 
			resources.ApplyResources(this._playerRootTabControl, "_playerRootTabControl");
			this._playerRootTabControl.Controls.Add(this._playerBasicTabPage);
			this._playerRootTabControl.Controls.Add(this._playerAppearanceRootTabPage);
			this._playerRootTabControl.ImageList = this._rootIconImageList;
			this._playerRootTabControl.Name = "_playerRootTabControl";
			this._playerRootTabControl.SelectedIndex = 0;
			// 
			// _playerBasicTabPage
			// 
			this._playerBasicTabPage.Controls.Add(this._playerBasicPanel);
			this._playerBasicTabPage.Controls.Add(this._playerBasicGenderWarningLabel);
			resources.ApplyResources(this._playerBasicTabPage, "_playerBasicTabPage");
			this._playerBasicTabPage.Name = "_playerBasicTabPage";
			this._playerBasicTabPage.UseVisualStyleBackColor = true;
			// 
			// _playerBasicPanel
			// 
			resources.ApplyResources(this._playerBasicPanel, "_playerBasicPanel");
			this._playerBasicPanel.Name = "_playerBasicPanel";
			// 
			// _playerBasicGenderWarningLabel
			// 
			resources.ApplyResources(this._playerBasicGenderWarningLabel, "_playerBasicGenderWarningLabel");
			this._playerBasicGenderWarningLabel.Name = "_playerBasicGenderWarningLabel";
			// 
			// _playerAppearanceRootTabPage
			// 
			this._playerAppearanceRootTabPage.Controls.Add(this._playerAppearanceRootTabControl);
			this._playerAppearanceRootTabPage.Controls.Add(this._playerAppearanceRootToolStrip);
			resources.ApplyResources(this._playerAppearanceRootTabPage, "_playerAppearanceRootTabPage");
			this._playerAppearanceRootTabPage.Name = "_playerAppearanceRootTabPage";
			this._playerAppearanceRootTabPage.UseVisualStyleBackColor = true;
			// 
			// _playerAppearanceRootTabControl
			// 
			resources.ApplyResources(this._playerAppearanceRootTabControl, "_playerAppearanceRootTabControl");
			this._playerAppearanceRootTabControl.Controls.Add(this._playerAppearanceColorTabPage);
			this._playerAppearanceRootTabControl.ImageList = this._rootIconImageList;
			this._playerAppearanceRootTabControl.Name = "_playerAppearanceRootTabControl";
			this._playerAppearanceRootTabControl.SelectedIndex = 0;
			// 
			// _playerAppearanceColorTabPage
			// 
			this._playerAppearanceColorTabPage.Controls.Add(this._playerAppearanceColorListBox);
			this._playerAppearanceColorTabPage.Controls.Add(this._playerAppearanceColorToolStrip);
			resources.ApplyResources(this._playerAppearanceColorTabPage, "_playerAppearanceColorTabPage");
			this._playerAppearanceColorTabPage.Name = "_playerAppearanceColorTabPage";
			this._playerAppearanceColorTabPage.UseVisualStyleBackColor = true;
			// 
			// _playerAppearanceColorListBox
			// 
			this._playerAppearanceColorListBox.DataSource = this._rootVectorParametersBindingSource;
			this._playerAppearanceColorListBox.DisplayMember = "Name";
			resources.ApplyResources(this._playerAppearanceColorListBox, "_playerAppearanceColorListBox");
			this._playerAppearanceColorListBox.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
			this._playerAppearanceColorListBox.FormattingEnabled = true;
			this._playerAppearanceColorListBox.Name = "_playerAppearanceColorListBox";
			this._playerAppearanceColorListBox.ValueMember = "Name";
			this._playerAppearanceColorListBox.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.OnDrawColorListBoxItem);
			this._playerAppearanceColorListBox.DoubleClick += new System.EventHandler(this.OnPlayerAppearanceColorChange);
			// 
			// _rootVectorParametersBindingSource
			// 
			this._rootVectorParametersBindingSource.DataSource = typeof(MassEffect3.SaveFormats.MorphHead.VectorParameter);
			// 
			// _playerAppearanceColorToolStrip
			// 
			this._playerAppearanceColorToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._playerAppearanceColorAddColorButton,
            this._playerAppearanceColorRemoveColorButton,
            this._toolStripSeparator1,
            this._playerAppearanceColorChangeColorButton});
			resources.ApplyResources(this._playerAppearanceColorToolStrip, "_playerAppearanceColorToolStrip");
			this._playerAppearanceColorToolStrip.Name = "_playerAppearanceColorToolStrip";
			// 
			// _playerAppearanceColorAddColorButton
			// 
			this._playerAppearanceColorAddColorButton.Image = global::MassEffect3.SaveEdit.Properties.Resources.Editor_Player_Appearance_Color_Add;
			resources.ApplyResources(this._playerAppearanceColorAddColorButton, "_playerAppearanceColorAddColorButton");
			this._playerAppearanceColorAddColorButton.Name = "_playerAppearanceColorAddColorButton";
			this._playerAppearanceColorAddColorButton.Click += new System.EventHandler(this.OnPlayerAppearanceColorAdd);
			// 
			// _playerAppearanceColorRemoveColorButton
			// 
			this._playerAppearanceColorRemoveColorButton.Image = global::MassEffect3.SaveEdit.Properties.Resources.Editor_Player_Appearance_Color_Remove;
			resources.ApplyResources(this._playerAppearanceColorRemoveColorButton, "_playerAppearanceColorRemoveColorButton");
			this._playerAppearanceColorRemoveColorButton.Name = "_playerAppearanceColorRemoveColorButton";
			this._playerAppearanceColorRemoveColorButton.Click += new System.EventHandler(this.OnPlayerAppearanceColorRemove);
			// 
			// _toolStripSeparator1
			// 
			this._toolStripSeparator1.Name = "_toolStripSeparator1";
			resources.ApplyResources(this._toolStripSeparator1, "_toolStripSeparator1");
			// 
			// _playerAppearanceColorChangeColorButton
			// 
			this._playerAppearanceColorChangeColorButton.Image = global::MassEffect3.SaveEdit.Properties.Resources.Editor_Player_Appearance_Color_Change;
			resources.ApplyResources(this._playerAppearanceColorChangeColorButton, "_playerAppearanceColorChangeColorButton");
			this._playerAppearanceColorChangeColorButton.Name = "_playerAppearanceColorChangeColorButton";
			this._playerAppearanceColorChangeColorButton.Click += new System.EventHandler(this.OnPlayerAppearanceColorChange);
			// 
			// _rootIconImageList
			// 
			this._rootIconImageList.ColorDepth = System.Windows.Forms.ColorDepth.Depth8Bit;
			resources.ApplyResources(this._rootIconImageList, "_rootIconImageList");
			this._rootIconImageList.TransparentColor = System.Drawing.Color.Transparent;
			// 
			// _playerAppearanceRootToolStrip
			// 
			this._playerAppearanceRootToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._playerAppearanceMorphHeadDropDownButton,
            this._playerAppearancePresetDropDownButton});
			resources.ApplyResources(this._playerAppearanceRootToolStrip, "_playerAppearanceRootToolStrip");
			this._playerAppearanceRootToolStrip.Name = "_playerAppearanceRootToolStrip";
			// 
			// _playerAppearanceMorphHeadDropDownButton
			// 
			this._playerAppearanceMorphHeadDropDownButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._playerAppearanceMorphHeadImportButton,
            this._playerAppearanceMorphHeadExportButton});
			this._playerAppearanceMorphHeadDropDownButton.Image = global::MassEffect3.SaveEdit.Properties.Resources.Editor_Player_Appearance_MorphHead_Generic;
			resources.ApplyResources(this._playerAppearanceMorphHeadDropDownButton, "_playerAppearanceMorphHeadDropDownButton");
			this._playerAppearanceMorphHeadDropDownButton.Name = "_playerAppearanceMorphHeadDropDownButton";
			// 
			// _playerAppearanceMorphHeadImportButton
			// 
			this._playerAppearanceMorphHeadImportButton.Image = global::MassEffect3.SaveEdit.Properties.Resources.Editor_Player_Appearance_MorphHead_Import;
			this._playerAppearanceMorphHeadImportButton.Name = "_playerAppearanceMorphHeadImportButton";
			resources.ApplyResources(this._playerAppearanceMorphHeadImportButton, "_playerAppearanceMorphHeadImportButton");
			this._playerAppearanceMorphHeadImportButton.Click += new System.EventHandler(this.OnImportHeadMorph);
			// 
			// _playerAppearanceMorphHeadExportButton
			// 
			this._playerAppearanceMorphHeadExportButton.Image = global::MassEffect3.SaveEdit.Properties.Resources.Editor_Player_Appearance_MorphHead_Export;
			this._playerAppearanceMorphHeadExportButton.Name = "_playerAppearanceMorphHeadExportButton";
			resources.ApplyResources(this._playerAppearanceMorphHeadExportButton, "_playerAppearanceMorphHeadExportButton");
			this._playerAppearanceMorphHeadExportButton.Click += new System.EventHandler(this.OnExportHeadMorph);
			// 
			// _playerAppearancePresetDropDownButton
			// 
			this._playerAppearancePresetDropDownButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._playerAppearancePresetOpenFromFileButton,
            this._playerAppearancePresetSaveToFileButton});
			this._playerAppearancePresetDropDownButton.Image = global::MassEffect3.SaveEdit.Properties.Resources.Editor_Player_Appearance_Preset_Generic;
			resources.ApplyResources(this._playerAppearancePresetDropDownButton, "_playerAppearancePresetDropDownButton");
			this._playerAppearancePresetDropDownButton.Name = "_playerAppearancePresetDropDownButton";
			// 
			// _playerAppearancePresetOpenFromFileButton
			// 
			this._playerAppearancePresetOpenFromFileButton.Name = "_playerAppearancePresetOpenFromFileButton";
			resources.ApplyResources(this._playerAppearancePresetOpenFromFileButton, "_playerAppearancePresetOpenFromFileButton");
			this._playerAppearancePresetOpenFromFileButton.Click += new System.EventHandler(this.OnLoadAppearancePresetFromFile);
			// 
			// _playerAppearancePresetSaveToFileButton
			// 
			this._playerAppearancePresetSaveToFileButton.Name = "_playerAppearancePresetSaveToFileButton";
			resources.ApplyResources(this._playerAppearancePresetSaveToFileButton, "_playerAppearancePresetSaveToFileButton");
			this._playerAppearancePresetSaveToFileButton.Click += new System.EventHandler(this.OnSaveAppearancePresetToFile);
			// 
			// _squadRootTabPage
			// 
			this._squadRootTabPage.Controls.Add(this._squadRootTabControl);
			resources.ApplyResources(this._squadRootTabPage, "_squadRootTabPage");
			this._squadRootTabPage.Name = "_squadRootTabPage";
			this._squadRootTabPage.UseVisualStyleBackColor = true;
			// 
			// _squadRootTabControl
			// 
			resources.ApplyResources(this._squadRootTabControl, "_squadRootTabControl");
			this._squadRootTabControl.Controls.Add(this._squadBasicTabPage);
			this._squadRootTabControl.ImageList = this._rootIconImageList;
			this._squadRootTabControl.Name = "_squadRootTabControl";
			this._squadRootTabControl.SelectedIndex = 0;
			// 
			// _squadBasicTabPage
			// 
			this._squadBasicTabPage.Controls.Add(this._squadBasicPanel);
			resources.ApplyResources(this._squadBasicTabPage, "_squadBasicTabPage");
			this._squadBasicTabPage.Name = "_squadBasicTabPage";
			this._squadBasicTabPage.UseVisualStyleBackColor = true;
			// 
			// _squadBasicPanel
			// 
			resources.ApplyResources(this._squadBasicPanel, "_squadBasicPanel");
			this._squadBasicPanel.Name = "_squadBasicPanel";
			// 
			// _plotRootTabPage
			// 
			this._plotRootTabPage.Controls.Add(this._plotRootTabControl);
			resources.ApplyResources(this._plotRootTabPage, "_plotRootTabPage");
			this._plotRootTabPage.Name = "_plotRootTabPage";
			this._plotRootTabPage.UseVisualStyleBackColor = true;
			// 
			// _plotRootTabControl
			// 
			resources.ApplyResources(this._plotRootTabControl, "_plotRootTabControl");
			this._plotRootTabControl.Controls.Add(this._plotManualTabPage);
			this._plotRootTabControl.Controls.Add(this._plotManualPlayerVarTabPage);
			this._plotRootTabControl.ImageList = this._rootIconImageList;
			this._plotRootTabControl.Name = "_plotRootTabControl";
			this._plotRootTabControl.SelectedIndex = 0;
			// 
			// _plotManualTabPage
			// 
			this._plotManualTabPage.Controls.Add(this._plotManualToolStrip);
			this._plotManualTabPage.Controls.Add(this._plotManualLogTextBox);
			this._plotManualTabPage.Controls.Add(this._plotManualFloatGroupBox);
			this._plotManualTabPage.Controls.Add(this._plotManualIntGroupBox);
			this._plotManualTabPage.Controls.Add(this._plotManualBoolGroupBox);
			resources.ApplyResources(this._plotManualTabPage, "_plotManualTabPage");
			this._plotManualTabPage.Name = "_plotManualTabPage";
			this._plotManualTabPage.UseVisualStyleBackColor = true;
			// 
			// _plotManualToolStrip
			// 
			resources.ApplyResources(this._plotManualToolStrip, "_plotManualToolStrip");
			this._plotManualToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._plotManualClearLogButton});
			this._plotManualToolStrip.Name = "_plotManualToolStrip";
			// 
			// _plotManualClearLogButton
			// 
			this._plotManualClearLogButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
			this._plotManualClearLogButton.Image = global::MassEffect3.SaveEdit.Properties.Resources.Editor_Plot_Manual_ClearLog;
			resources.ApplyResources(this._plotManualClearLogButton, "_plotManualClearLogButton");
			this._plotManualClearLogButton.Name = "_plotManualClearLogButton";
			this._plotManualClearLogButton.Click += new System.EventHandler(this.OnPlotManualClearLog);
			// 
			// _plotManualLogTextBox
			// 
			resources.ApplyResources(this._plotManualLogTextBox, "_plotManualLogTextBox");
			this._plotManualLogTextBox.BackColor = System.Drawing.SystemColors.Window;
			this._plotManualLogTextBox.Name = "_plotManualLogTextBox";
			this._plotManualLogTextBox.ReadOnly = true;
			// 
			// _plotManualFloatGroupBox
			// 
			this._plotManualFloatGroupBox.Controls.Add(this._plotManualFloatIdTextBox);
			this._plotManualFloatGroupBox.Controls.Add(this._plotManualFloatGetButton);
			this._plotManualFloatGroupBox.Controls.Add(this._plotManualFloatSetButton);
			this._plotManualFloatGroupBox.Controls.Add(this._plotManualFloatValueTextBox);
			resources.ApplyResources(this._plotManualFloatGroupBox, "_plotManualFloatGroupBox");
			this._plotManualFloatGroupBox.Name = "_plotManualFloatGroupBox";
			this._plotManualFloatGroupBox.TabStop = false;
			// 
			// _plotManualFloatIdTextBox
			// 
			resources.ApplyResources(this._plotManualFloatIdTextBox, "_plotManualFloatIdTextBox");
			this._plotManualFloatIdTextBox.Name = "_plotManualFloatIdTextBox";
			// 
			// _plotManualFloatGetButton
			// 
			resources.ApplyResources(this._plotManualFloatGetButton, "_plotManualFloatGetButton");
			this._plotManualFloatGetButton.Name = "_plotManualFloatGetButton";
			this._plotManualFloatGetButton.UseVisualStyleBackColor = true;
			this._plotManualFloatGetButton.Click += new System.EventHandler(this.OnPlotManualGetFloat);
			// 
			// _plotManualFloatSetButton
			// 
			resources.ApplyResources(this._plotManualFloatSetButton, "_plotManualFloatSetButton");
			this._plotManualFloatSetButton.Name = "_plotManualFloatSetButton";
			this._plotManualFloatSetButton.UseVisualStyleBackColor = true;
			this._plotManualFloatSetButton.Click += new System.EventHandler(this.OnPlotManualSetFloat);
			// 
			// _plotManualFloatValueTextBox
			// 
			resources.ApplyResources(this._plotManualFloatValueTextBox, "_plotManualFloatValueTextBox");
			this._plotManualFloatValueTextBox.Name = "_plotManualFloatValueTextBox";
			// 
			// _plotManualIntGroupBox
			// 
			this._plotManualIntGroupBox.Controls.Add(this._plotManualIntIdTextBox);
			this._plotManualIntGroupBox.Controls.Add(this._plotManualIntGetButton);
			this._plotManualIntGroupBox.Controls.Add(this._plotManualIntSetButton);
			this._plotManualIntGroupBox.Controls.Add(this._plotManualIntValueTextBox);
			resources.ApplyResources(this._plotManualIntGroupBox, "_plotManualIntGroupBox");
			this._plotManualIntGroupBox.Name = "_plotManualIntGroupBox";
			this._plotManualIntGroupBox.TabStop = false;
			// 
			// _plotManualIntIdTextBox
			// 
			resources.ApplyResources(this._plotManualIntIdTextBox, "_plotManualIntIdTextBox");
			this._plotManualIntIdTextBox.Name = "_plotManualIntIdTextBox";
			// 
			// _plotManualIntGetButton
			// 
			resources.ApplyResources(this._plotManualIntGetButton, "_plotManualIntGetButton");
			this._plotManualIntGetButton.Name = "_plotManualIntGetButton";
			this._plotManualIntGetButton.UseVisualStyleBackColor = true;
			this._plotManualIntGetButton.Click += new System.EventHandler(this.OnPlotManualGetInt);
			// 
			// _plotManualIntSetButton
			// 
			resources.ApplyResources(this._plotManualIntSetButton, "_plotManualIntSetButton");
			this._plotManualIntSetButton.Name = "_plotManualIntSetButton";
			this._plotManualIntSetButton.UseVisualStyleBackColor = true;
			this._plotManualIntSetButton.Click += new System.EventHandler(this.OnPlotManualSetInt);
			// 
			// _plotManualIntValueTextBox
			// 
			resources.ApplyResources(this._plotManualIntValueTextBox, "_plotManualIntValueTextBox");
			this._plotManualIntValueTextBox.Name = "_plotManualIntValueTextBox";
			// 
			// _plotManualBoolGroupBox
			// 
			this._plotManualBoolGroupBox.Controls.Add(this._plotManualBoolIdTextBox);
			this._plotManualBoolGroupBox.Controls.Add(this._plotManualBoolSetButton);
			this._plotManualBoolGroupBox.Controls.Add(this._plotManualBoolGetButton);
			this._plotManualBoolGroupBox.Controls.Add(this._plotManualBoolValueCheckBox);
			resources.ApplyResources(this._plotManualBoolGroupBox, "_plotManualBoolGroupBox");
			this._plotManualBoolGroupBox.Name = "_plotManualBoolGroupBox";
			this._plotManualBoolGroupBox.TabStop = false;
			// 
			// _plotManualBoolIdTextBox
			// 
			resources.ApplyResources(this._plotManualBoolIdTextBox, "_plotManualBoolIdTextBox");
			this._plotManualBoolIdTextBox.Name = "_plotManualBoolIdTextBox";
			// 
			// _plotManualBoolSetButton
			// 
			resources.ApplyResources(this._plotManualBoolSetButton, "_plotManualBoolSetButton");
			this._plotManualBoolSetButton.Name = "_plotManualBoolSetButton";
			this._plotManualBoolSetButton.UseVisualStyleBackColor = true;
			this._plotManualBoolSetButton.Click += new System.EventHandler(this.OnPlotManualSetBool);
			// 
			// _plotManualBoolGetButton
			// 
			resources.ApplyResources(this._plotManualBoolGetButton, "_plotManualBoolGetButton");
			this._plotManualBoolGetButton.Name = "_plotManualBoolGetButton";
			this._plotManualBoolGetButton.UseVisualStyleBackColor = true;
			this._plotManualBoolGetButton.Click += new System.EventHandler(this.OnPlotManualGetBool);
			// 
			// _plotManualBoolValueCheckBox
			// 
			resources.ApplyResources(this._plotManualBoolValueCheckBox, "_plotManualBoolValueCheckBox");
			this._plotManualBoolValueCheckBox.Name = "_plotManualBoolValueCheckBox";
			this._plotManualBoolValueCheckBox.UseVisualStyleBackColor = true;
			// 
			// _plotManualPlayerVarTabPage
			// 
			this._plotManualPlayerVarTabPage.Controls.Add(this._plotManualPlayerVarToolStrip);
			this._plotManualPlayerVarTabPage.Controls.Add(this._plotManualPlayerVarLogTextBox);
			this._plotManualPlayerVarTabPage.Controls.Add(this._plotManualPlayerVarGroupBox);
			resources.ApplyResources(this._plotManualPlayerVarTabPage, "_plotManualPlayerVarTabPage");
			this._plotManualPlayerVarTabPage.Name = "_plotManualPlayerVarTabPage";
			this._plotManualPlayerVarTabPage.UseVisualStyleBackColor = true;
			// 
			// _plotManualPlayerVarToolStrip
			// 
			resources.ApplyResources(this._plotManualPlayerVarToolStrip, "_plotManualPlayerVarToolStrip");
			this._plotManualPlayerVarToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._plotManualPlayerVarClearLogButton});
			this._plotManualPlayerVarToolStrip.Name = "_plotManualPlayerVarToolStrip";
			// 
			// _plotManualPlayerVarClearLogButton
			// 
			this._plotManualPlayerVarClearLogButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
			this._plotManualPlayerVarClearLogButton.Image = global::MassEffect3.SaveEdit.Properties.Resources.Editor_Plot_Manual_ClearLog;
			resources.ApplyResources(this._plotManualPlayerVarClearLogButton, "_plotManualPlayerVarClearLogButton");
			this._plotManualPlayerVarClearLogButton.Name = "_plotManualPlayerVarClearLogButton";
			this._plotManualPlayerVarClearLogButton.Click += new System.EventHandler(this.OnPlotManualPlayerVarClearLog);
			// 
			// _plotManualPlayerVarLogTextBox
			// 
			resources.ApplyResources(this._plotManualPlayerVarLogTextBox, "_plotManualPlayerVarLogTextBox");
			this._plotManualPlayerVarLogTextBox.BackColor = System.Drawing.SystemColors.Window;
			this._plotManualPlayerVarLogTextBox.Name = "_plotManualPlayerVarLogTextBox";
			this._plotManualPlayerVarLogTextBox.ReadOnly = true;
			// 
			// _plotManualPlayerVarGroupBox
			// 
			this._plotManualPlayerVarGroupBox.Controls.Add(this._plotManualPlayerVarIdTextBox);
			this._plotManualPlayerVarGroupBox.Controls.Add(this._plotManualPlayerVarGetButton);
			this._plotManualPlayerVarGroupBox.Controls.Add(this._plotManualPlayerVarSetButton);
			this._plotManualPlayerVarGroupBox.Controls.Add(this._plotManualPlayerVarValueTextBox);
			resources.ApplyResources(this._plotManualPlayerVarGroupBox, "_plotManualPlayerVarGroupBox");
			this._plotManualPlayerVarGroupBox.Name = "_plotManualPlayerVarGroupBox";
			this._plotManualPlayerVarGroupBox.TabStop = false;
			// 
			// _plotManualPlayerVarIdTextBox
			// 
			resources.ApplyResources(this._plotManualPlayerVarIdTextBox, "_plotManualPlayerVarIdTextBox");
			this._plotManualPlayerVarIdTextBox.Name = "_plotManualPlayerVarIdTextBox";
			// 
			// _plotManualPlayerVarGetButton
			// 
			resources.ApplyResources(this._plotManualPlayerVarGetButton, "_plotManualPlayerVarGetButton");
			this._plotManualPlayerVarGetButton.Name = "_plotManualPlayerVarGetButton";
			this._plotManualPlayerVarGetButton.UseVisualStyleBackColor = true;
			this._plotManualPlayerVarGetButton.Click += new System.EventHandler(this.OnPlotManualGetPlayerVar);
			// 
			// _plotManualPlayerVarSetButton
			// 
			resources.ApplyResources(this._plotManualPlayerVarSetButton, "_plotManualPlayerVarSetButton");
			this._plotManualPlayerVarSetButton.Name = "_plotManualPlayerVarSetButton";
			this._plotManualPlayerVarSetButton.UseVisualStyleBackColor = true;
			this._plotManualPlayerVarSetButton.Click += new System.EventHandler(this.OnPlotManualSetPlayerVar);
			// 
			// _plotManualPlayerVarValueTextBox
			// 
			resources.ApplyResources(this._plotManualPlayerVarValueTextBox, "_plotManualPlayerVarValueTextBox");
			this._plotManualPlayerVarValueTextBox.Name = "_plotManualPlayerVarValueTextBox";
			// 
			// _rawTabPage
			// 
			this._rawTabPage.Controls.Add(this._rawSplitContainer);
			resources.ApplyResources(this._rawTabPage, "_rawTabPage");
			this._rawTabPage.Name = "_rawTabPage";
			this._rawTabPage.UseVisualStyleBackColor = true;
			// 
			// _rootSaveFileBindingSource
			// 
			this._rootSaveFileBindingSource.DataSource = typeof(MassEffect3.SaveFormats.SFXSaveGameFile);
			// 
			// _rootSaveGameOpenFileDialog
			// 
			this._rootSaveGameOpenFileDialog.DefaultExt = "pcsav";
			resources.ApplyResources(this._rootSaveGameOpenFileDialog, "_rootSaveGameOpenFileDialog");
			this._rootSaveGameOpenFileDialog.RestoreDirectory = true;
			// 
			// _rootSaveGameSaveFileDialog
			// 
			resources.ApplyResources(this._rootSaveGameSaveFileDialog, "_rootSaveGameSaveFileDialog");
			this._rootSaveGameSaveFileDialog.RestoreDirectory = true;
			// 
			// _rootMorphHeadOpenFileDialog
			// 
			resources.ApplyResources(this._rootMorphHeadOpenFileDialog, "_rootMorphHeadOpenFileDialog");
			this._rootMorphHeadOpenFileDialog.RestoreDirectory = true;
			// 
			// _rootMorphHeadSaveFileDialog
			// 
			resources.ApplyResources(this._rootMorphHeadSaveFileDialog, "_rootMorphHeadSaveFileDialog");
			this._rootMorphHeadSaveFileDialog.RestoreDirectory = true;
			// 
			// _rootAppearancePresetOpenFileDialog
			// 
			resources.ApplyResources(this._rootAppearancePresetOpenFileDialog, "_rootAppearancePresetOpenFileDialog");
			this._rootAppearancePresetOpenFileDialog.RestoreDirectory = true;
			// 
			// _rootAppearancePresetSaveFileDialog
			// 
			resources.ApplyResources(this._rootAppearancePresetSaveFileDialog, "_rootAppearancePresetSaveFileDialog");
			this._rootAppearancePresetSaveFileDialog.RestoreDirectory = true;
			// 
			// Editor
			// 
			resources.ApplyResources(this, "$this");
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this._rootTabControl);
			this.Controls.Add(this._rootToolStrip);
			this.Name = "Editor";
			this._rawSplitContainer.Panel1.ResumeLayout(false);
			this._rawSplitContainer.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this._rawSplitContainer)).EndInit();
			this._rawSplitContainer.ResumeLayout(false);
			this._rootToolStrip.ResumeLayout(false);
			this._rootToolStrip.PerformLayout();
			this._rootTabControl.ResumeLayout(false);
			this._playerRootTabPage.ResumeLayout(false);
			this._playerRootTabControl.ResumeLayout(false);
			this._playerBasicTabPage.ResumeLayout(false);
			this._playerBasicTabPage.PerformLayout();
			this._playerAppearanceRootTabPage.ResumeLayout(false);
			this._playerAppearanceRootTabPage.PerformLayout();
			this._playerAppearanceRootTabControl.ResumeLayout(false);
			this._playerAppearanceColorTabPage.ResumeLayout(false);
			this._playerAppearanceColorTabPage.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this._rootVectorParametersBindingSource)).EndInit();
			this._playerAppearanceColorToolStrip.ResumeLayout(false);
			this._playerAppearanceColorToolStrip.PerformLayout();
			this._playerAppearanceRootToolStrip.ResumeLayout(false);
			this._playerAppearanceRootToolStrip.PerformLayout();
			this._squadRootTabPage.ResumeLayout(false);
			this._squadRootTabControl.ResumeLayout(false);
			this._squadBasicTabPage.ResumeLayout(false);
			this._plotRootTabPage.ResumeLayout(false);
			this._plotRootTabControl.ResumeLayout(false);
			this._plotManualTabPage.ResumeLayout(false);
			this._plotManualTabPage.PerformLayout();
			this._plotManualToolStrip.ResumeLayout(false);
			this._plotManualToolStrip.PerformLayout();
			this._plotManualFloatGroupBox.ResumeLayout(false);
			this._plotManualFloatGroupBox.PerformLayout();
			this._plotManualIntGroupBox.ResumeLayout(false);
			this._plotManualIntGroupBox.PerformLayout();
			this._plotManualBoolGroupBox.ResumeLayout(false);
			this._plotManualBoolGroupBox.PerformLayout();
			this._plotManualPlayerVarTabPage.ResumeLayout(false);
			this._plotManualPlayerVarTabPage.PerformLayout();
			this._plotManualPlayerVarToolStrip.ResumeLayout(false);
			this._plotManualPlayerVarToolStrip.PerformLayout();
			this._plotManualPlayerVarGroupBox.ResumeLayout(false);
			this._plotManualPlayerVarGroupBox.PerformLayout();
			this._rawTabPage.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this._rootSaveFileBindingSource)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStrip _rootToolStrip;
        private System.Windows.Forms.TabControl _rootTabControl;
        private System.Windows.Forms.TabPage _playerRootTabPage;
        private System.Windows.Forms.TabPage _rawTabPage;
        private System.Windows.Forms.ToolStripSplitButton _rootOpenFromGenericButton;
        private System.Windows.Forms.OpenFileDialog _rootSaveGameOpenFileDialog;
        private System.Windows.Forms.SplitContainer _rawSplitContainer;
        private System.Windows.Forms.PropertyGrid _rawParentPropertyGrid;
        private System.Windows.Forms.PropertyGrid _rawChildPropertyGrid;
        private System.Windows.Forms.ImageList _rootIconImageList;
        private System.Windows.Forms.ToolStripSplitButton _rootSaveToGenericButton;
        private System.Windows.Forms.ToolStripDropDownButton _rootNewSplitButton;
        private System.Windows.Forms.TabControl _playerRootTabControl;
        private System.Windows.Forms.TabPage _playerBasicTabPage;
        private System.Windows.Forms.TabPage _playerAppearanceRootTabPage;
        private System.Windows.Forms.ToolStripMenuItem _rootNewMaleToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem _rootNewFemaleToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem _rootOpenFromCareerMenuItem;
        private System.Windows.Forms.ToolStripMenuItem _rootOpenFromFileMenuItem;
        private System.Windows.Forms.ToolStripMenuItem _rootSaveToCareerMenuItem;
        private System.Windows.Forms.ToolStripMenuItem _rootSaveToFileMenuItem;
        private System.Windows.Forms.SaveFileDialog _rootSaveGameSaveFileDialog;
        private System.Windows.Forms.OpenFileDialog _rootMorphHeadOpenFileDialog;
        private System.Windows.Forms.SaveFileDialog _rootMorphHeadSaveFileDialog;
        private System.Windows.Forms.ToolStrip _playerAppearanceRootToolStrip;
        private System.Windows.Forms.ToolStripDropDownButton _rootSettingsButton;
        private System.Windows.Forms.ToolStripMenuItem _useCareerPickerMenuItem;
        private System.Windows.Forms.TabPage _plotRootTabPage;
        private System.Windows.Forms.TabControl _plotRootTabControl;
        private System.Windows.Forms.TabPage _plotManualTabPage;
        private System.Windows.Forms.TextBox _plotManualLogTextBox;
        private System.Windows.Forms.GroupBox _plotManualFloatGroupBox;
        private System.Windows.Forms.Button _plotManualFloatGetButton;
        private System.Windows.Forms.Button _plotManualFloatSetButton;
        private System.Windows.Forms.TextBox _plotManualFloatValueTextBox;
        private System.Windows.Forms.GroupBox _plotManualIntGroupBox;
        private System.Windows.Forms.Button _plotManualIntGetButton;
        private System.Windows.Forms.Button _plotManualIntSetButton;
        private System.Windows.Forms.TextBox _plotManualIntValueTextBox;
        private System.Windows.Forms.GroupBox _plotManualBoolGroupBox;
        private System.Windows.Forms.Button _plotManualBoolSetButton;
        private System.Windows.Forms.Button _plotManualBoolGetButton;
		private System.Windows.Forms.CheckBox _plotManualBoolValueCheckBox;
        private System.Windows.Forms.TextBox _plotManualFloatIdTextBox;
        private System.Windows.Forms.TextBox _plotManualIntIdTextBox;
		private System.Windows.Forms.TextBox _plotManualBoolIdTextBox;
        private System.Windows.Forms.ToolStripDropDownButton _playerAppearancePresetDropDownButton;
        private System.Windows.Forms.ToolStripMenuItem _playerAppearancePresetOpenFromFileButton;
        private System.Windows.Forms.OpenFileDialog _rootAppearancePresetOpenFileDialog;
        private System.Windows.Forms.ToolStripMenuItem _playerAppearancePresetSaveToFileButton;
        private System.Windows.Forms.SaveFileDialog _rootAppearancePresetSaveFileDialog;
        private System.Windows.Forms.ToolStripDropDownButton _playerAppearanceMorphHeadDropDownButton;
        private System.Windows.Forms.ToolStripMenuItem _playerAppearanceMorphHeadImportButton;
        private System.Windows.Forms.ToolStripMenuItem _playerAppearanceMorphHeadExportButton;
        private System.Windows.Forms.TabControl _playerAppearanceRootTabControl;
        private System.Windows.Forms.TabPage _playerAppearanceColorTabPage;
        private System.Windows.Forms.ListBox _playerAppearanceColorListBox;
        private System.Windows.Forms.ToolStrip _playerAppearanceColorToolStrip;
        private System.Windows.Forms.ToolStripButton _playerAppearanceColorAddColorButton;
        private System.Windows.Forms.ToolStripButton _playerAppearanceColorRemoveColorButton;
        private System.Windows.Forms.ToolStripSeparator _toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton _playerAppearanceColorChangeColorButton;
        private System.Windows.Forms.FlowLayoutPanel _playerBasicPanel;
        internal System.Windows.Forms.BindingSource _rootSaveFileBindingSource;
        internal System.Windows.Forms.BindingSource _rootVectorParametersBindingSource;
        private System.Windows.Forms.Label _playerBasicGenderWarningLabel;
		private System.Windows.Forms.ToolStripButton _rootAboutButton;
		private System.Windows.Forms.ToolStrip _plotManualToolStrip;
		private System.Windows.Forms.ToolStripButton _plotManualClearLogButton;
		private System.Windows.Forms.TabPage _plotManualPlayerVarTabPage;
		private System.Windows.Forms.ToolStrip _plotManualPlayerVarToolStrip;
		private System.Windows.Forms.ToolStripButton _plotManualPlayerVarClearLogButton;
		private System.Windows.Forms.TextBox _plotManualPlayerVarLogTextBox;
		private System.Windows.Forms.GroupBox _plotManualPlayerVarGroupBox;
		private System.Windows.Forms.TextBox _plotManualPlayerVarIdTextBox;
		private System.Windows.Forms.Button _plotManualPlayerVarGetButton;
		private System.Windows.Forms.Button _plotManualPlayerVarSetButton;
		private System.Windows.Forms.TextBox _plotManualPlayerVarValueTextBox;
		private System.Windows.Forms.ToolStripMenuItem _backupAutoSavesMenuItem;
		private System.Windows.Forms.TabPage _squadRootTabPage;
		private System.Windows.Forms.TabControl _squadRootTabControl;
		private System.Windows.Forms.TabPage _squadBasicTabPage;
		private System.Windows.Forms.FlowLayoutPanel _squadBasicPanel;
		private System.Windows.Forms.ToolStripMenuItem compareToolStripMenuItem;
	}
}

