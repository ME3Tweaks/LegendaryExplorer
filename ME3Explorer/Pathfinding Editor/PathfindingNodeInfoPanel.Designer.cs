using ME3Explorer.Packages;
using ME3Explorer.Unreal;

namespace ME3Explorer.Pathfinding_Editor
{
    partial class PathfindingNodeInfoPanel
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.exportTitleLabel = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.reachableNodesList = new System.Windows.Forms.ListBox();
            this.label2 = new System.Windows.Forms.Label();
            this.sfxCombatZoneList = new System.Windows.Forms.ListBox();
            this.reachSpecSizeSelector = new System.Windows.Forms.ComboBox();
            this.reachSpecSizeLabel = new System.Windows.Forms.Label();
            this.connectionToLabel = new System.Windows.Forms.Label();
            this.reachSpecDestLabel = new System.Windows.Forms.Label();
            this.reachSpecDistanceHeaderLabel = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.pathNodeSizeComboBox = new System.Windows.Forms.ComboBox();
            this.zLabel = new System.Windows.Forms.Label();
            this.yLabel = new System.Windows.Forms.Label();
            this.xLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // exportTitleLabel
            // 
            this.exportTitleLabel.AutoSize = true;
            this.exportTitleLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.exportTitleLabel.Location = new System.Drawing.Point(3, 10);
            this.exportTitleLabel.Name = "exportTitleLabel";
            this.exportTitleLabel.Size = new System.Drawing.Size(120, 20);
            this.exportTitleLabel.TabIndex = 0;
            this.exportTitleLabel.Text = "Select a node";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(7, 66);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(215, 17);
            this.label1.TabIndex = 1;
            this.label1.Text = "Reachable Nodes (ReachSpecs)";
            // 
            // reachableNodesList
            // 
            this.reachableNodesList.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.reachableNodesList.FormattingEnabled = true;
            this.reachableNodesList.Location = new System.Drawing.Point(10, 86);
            this.reachableNodesList.Name = "reachableNodesList";
            this.reachableNodesList.Size = new System.Drawing.Size(453, 56);
            this.reachableNodesList.TabIndex = 2;
            this.reachableNodesList.SelectedIndexChanged += new System.EventHandler(this.reachSpecSelection_Changed);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(10, 230);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(122, 17);
            this.label2.TabIndex = 3;
            this.label2.Text = "SFXCombatZones";
            // 
            // sfxCombatZoneList
            // 
            this.sfxCombatZoneList.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.sfxCombatZoneList.FormattingEnabled = true;
            this.sfxCombatZoneList.Location = new System.Drawing.Point(13, 250);
            this.sfxCombatZoneList.Name = "sfxCombatZoneList";
            this.sfxCombatZoneList.Size = new System.Drawing.Size(450, 56);
            this.sfxCombatZoneList.TabIndex = 4;
            this.sfxCombatZoneList.SelectedIndexChanged += new System.EventHandler(this.sfxCombatZoneSelectionChanged);
            // 
            // reachSpecSizeSelector
            // 
            this.reachSpecSizeSelector.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.reachSpecSizeSelector.FormattingEnabled = true;
            this.reachSpecSizeSelector.Items.AddRange(new object[] {
            "Mooks (34x90)",
            "Minibosses (95x145)",
            "Bosses (140x195)"});
            this.reachSpecSizeSelector.Location = new System.Drawing.Point(245, 161);
            this.reachSpecSizeSelector.Name = "reachSpecSizeSelector";
            this.reachSpecSizeSelector.Size = new System.Drawing.Size(218, 21);
            this.reachSpecSizeSelector.TabIndex = 5;
            this.reachSpecSizeSelector.SelectedIndexChanged += new System.EventHandler(this.reachspecSizeBox_Changed);
            // 
            // reachSpecSizeLabel
            // 
            this.reachSpecSizeLabel.AutoSize = true;
            this.reachSpecSizeLabel.Location = new System.Drawing.Point(227, 145);
            this.reachSpecSizeLabel.Name = "reachSpecSizeLabel";
            this.reachSpecSizeLabel.Size = new System.Drawing.Size(87, 13);
            this.reachSpecSizeLabel.TabIndex = 6;
            this.reachSpecSizeLabel.Text = "ReachSpec Size";
            // 
            // connectionToLabel
            // 
            this.connectionToLabel.AutoSize = true;
            this.connectionToLabel.Location = new System.Drawing.Point(7, 145);
            this.connectionToLabel.Name = "connectionToLabel";
            this.connectionToLabel.Size = new System.Drawing.Size(73, 13);
            this.connectionToLabel.TabIndex = 7;
            this.connectionToLabel.Text = "Connection to";
            // 
            // reachSpecDestLabel
            // 
            this.reachSpecDestLabel.AutoSize = true;
            this.reachSpecDestLabel.Location = new System.Drawing.Point(22, 158);
            this.reachSpecDestLabel.Name = "reachSpecDestLabel";
            this.reachSpecDestLabel.Size = new System.Drawing.Size(124, 13);
            this.reachSpecDestLabel.TabIndex = 8;
            this.reachSpecDestLabel.Text = "No ReachSpec selected";
            // 
            // reachSpecDistanceHeaderLabel
            // 
            this.reachSpecDistanceHeaderLabel.AutoSize = true;
            this.reachSpecDistanceHeaderLabel.Location = new System.Drawing.Point(10, 187);
            this.reachSpecDistanceHeaderLabel.Name = "reachSpecDistanceHeaderLabel";
            this.reachSpecDistanceHeaderLabel.Size = new System.Drawing.Size(85, 13);
            this.reachSpecDistanceHeaderLabel.TabIndex = 9;
            this.reachSpecDistanceHeaderLabel.Text = "ReachSpec Info";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(22, 200);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(124, 13);
            this.label5.TabIndex = 10;
            this.label5.Text = "No ReachSpec selected";
            // 
            // pathNodeSizeComboBox
            // 
            this.pathNodeSizeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.pathNodeSizeComboBox.FormattingEnabled = true;
            this.pathNodeSizeComboBox.Items.AddRange(new object[] {
            "Mooks(34x90)",
            "Minibosses(105x145)",
            "Bosses(140x195)"});
            this.pathNodeSizeComboBox.Location = new System.Drawing.Point(7, 33);
            this.pathNodeSizeComboBox.Name = "pathNodeSizeComboBox";
            this.pathNodeSizeComboBox.Size = new System.Drawing.Size(215, 21);
            this.pathNodeSizeComboBox.TabIndex = 11;
            this.pathNodeSizeComboBox.SelectedIndexChanged += new System.EventHandler(this.pathNodeSize_DropdownChanged);
            // 
            // zLabel
            // 
            this.zLabel.AutoSize = true;
            this.zLabel.Location = new System.Drawing.Point(254, 58);
            this.zLabel.Name = "zLabel";
            this.zLabel.Size = new System.Drawing.Size(17, 13);
            this.zLabel.TabIndex = 13;
            this.zLabel.Text = "Z:";
            // 
            // yLabel
            // 
            this.yLabel.AutoSize = true;
            this.yLabel.Location = new System.Drawing.Point(254, 45);
            this.yLabel.Name = "yLabel";
            this.yLabel.Size = new System.Drawing.Size(17, 13);
            this.yLabel.TabIndex = 14;
            this.yLabel.Text = "Y:";
            // 
            // xLabel
            // 
            this.xLabel.AutoSize = true;
            this.xLabel.Location = new System.Drawing.Point(254, 32);
            this.xLabel.Name = "xLabel";
            this.xLabel.Size = new System.Drawing.Size(17, 13);
            this.xLabel.TabIndex = 15;
            this.xLabel.Text = "X:";
            // 
            // PathfindingNodeInfoPanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.xLabel);
            this.Controls.Add(this.yLabel);
            this.Controls.Add(this.zLabel);
            this.Controls.Add(this.pathNodeSizeComboBox);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.reachSpecDistanceHeaderLabel);
            this.Controls.Add(this.reachSpecDestLabel);
            this.Controls.Add(this.connectionToLabel);
            this.Controls.Add(this.reachSpecSizeLabel);
            this.Controls.Add(this.reachSpecSizeSelector);
            this.Controls.Add(this.sfxCombatZoneList);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.reachableNodesList);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.exportTitleLabel);
            this.Name = "PathfindingNodeInfoPanel";
            this.Size = new System.Drawing.Size(466, 563);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label exportTitleLabel;


        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ListBox reachableNodesList;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ListBox sfxCombatZoneList;
        private System.Windows.Forms.ComboBox reachSpecSizeSelector;
        private System.Windows.Forms.Label reachSpecSizeLabel;
        private System.Windows.Forms.Label connectionToLabel;
        private System.Windows.Forms.Label reachSpecDestLabel;
        private System.Windows.Forms.Label reachSpecDistanceHeaderLabel;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox pathNodeSizeComboBox;
        private System.Windows.Forms.Label zLabel;
        private System.Windows.Forms.Label yLabel;
        private System.Windows.Forms.Label xLabel;
    }
}
