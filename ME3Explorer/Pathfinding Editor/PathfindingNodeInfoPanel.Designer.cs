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
            this.SuspendLayout();
            // 
            // exportTitleLabel
            // 
            this.exportTitleLabel.AutoSize = true;
            this.exportTitleLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.exportTitleLabel.Location = new System.Drawing.Point(3, 10);
            this.exportTitleLabel.Name = "exportTitleLabel";
            this.exportTitleLabel.Size = new System.Drawing.Size(199, 20);
            this.exportTitleLabel.TabIndex = 0;
            this.exportTitleLabel.Text = "Placeholder Export Title";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(7, 34);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(93, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Reachable Nodes";
            // 
            // reachableNodesList
            // 
            this.reachableNodesList.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.reachableNodesList.FormattingEnabled = true;
            this.reachableNodesList.Location = new System.Drawing.Point(10, 50);
            this.reachableNodesList.Name = "reachableNodesList";
            this.reachableNodesList.Size = new System.Drawing.Size(366, 56);
            this.reachableNodesList.TabIndex = 2;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(7, 109);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(93, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "SFXCombatZones";
            // 
            // sfxCombatZoneList
            // 
            this.sfxCombatZoneList.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.sfxCombatZoneList.FormattingEnabled = true;
            this.sfxCombatZoneList.Location = new System.Drawing.Point(10, 125);
            this.sfxCombatZoneList.Name = "sfxCombatZoneList";
            this.sfxCombatZoneList.Size = new System.Drawing.Size(366, 56);
            this.sfxCombatZoneList.TabIndex = 4;
            this.sfxCombatZoneList.SelectedIndexChanged += new System.EventHandler(this.sfxCombatZoneSelectionChanged);
            // 
            // PathfindingNodeInfoPanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.sfxCombatZoneList);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.reachableNodesList);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.exportTitleLabel);
            this.Name = "PathfindingNodeInfoPanel";
            this.Size = new System.Drawing.Size(379, 563);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label exportTitleLabel;


        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ListBox reachableNodesList;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ListBox sfxCombatZoneList;
    }
}
