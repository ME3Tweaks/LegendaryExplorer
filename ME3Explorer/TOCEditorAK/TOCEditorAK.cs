/*  Copyright (C) 2013 AmaroK86 (marcidm 'at' hotmail 'dot' com)
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.

 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.

 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Windows.Forms;
using Gibbed.IO;
using Gibbed.MassEffect3.FileFormats;
using AmaroK86.MassEffect3;
using System.Globalization;

namespace ME3Explorer.TOCEditorAK
{
    public partial class TOCEditorAK : Form
    {
        string tocbin;
        TOCHandler tocHnd;

        public TOCEditorAK()
        {
            InitializeComponent();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                tocbin = openFileDialog.FileName;
                openTOCFile(tocbin, AmaroK86.MassEffect3.ME3Paths.cookedPath);

                string originalTOC = tocbin + ".orig";
                if (File.Exists(originalTOC))
                    recoverFromBackupToolStripMenuItem.Visible = true;
            }
        }

        private bool isBlock(TreeNode treeNode)
        {
            return treeNode.Parent == null;
        }

        private bool isFile(TreeNode treeNode)
        {
            return (treeNode.Parent != null && treeNode.Parent.Parent == null);
        }

        private void openTOCFile(string tocbin, string gamePath)
        {
            tocHnd = new TOCHandler(tocbin, ME3Paths.gamePath);

            openTOC(tocHnd);
        }

        private void openTOC(TOCHandler tocHnd)
        {
            treeViewTOC.BeginUpdate();
            treeViewTOC.Nodes.Clear();

            foreach (TOCHandler.chunk chunk in tocHnd.chunkList)
            {
                blockToTreeView(chunk);
            }

            treeViewTOC.EndUpdate();
        }

        private void blockToTreeView(TOCHandler.chunk block, TreeNode selNode = null)
        {
            int blockIdx = tocHnd.chunkList.IndexOf(block);
            string blockText = "Block " + (blockIdx + 1) + ": " + block.countNextFiles + " files" +
                ((block.countNextFiles == 0)
                    ? ""
                    : ", relative offset 0x" + block.relPosition.ToString("X4") + ", block size: " + block.globalSize.ToString("#,##0.") + " bytes"
                );
            TreeNode root;
            if (selNode == null)
            {
                root = treeViewTOC.Nodes.Add("Block_" + blockIdx, blockText);
            }
            else
            {
                root = selNode;
                selNode.Nodes.Clear();
                selNode.Text = blockText;
            }
            root.ToolTipText = "Right click to edit block";

            if (block.countNextFiles == 0)
                return;

            int fileCount = 0;
            foreach (TOCHandler.fileStruct fileStruct in block.fileList)
            {
                TreeNode file = root.Nodes.Add(fileStruct.filePath, Path.GetFileName(fileStruct.filePath));
                file.ToolTipText = "Right click to edit file";
                fileCount++;
                if (!File.Exists(ME3Paths.gamePath + fileStruct.filePath))
                {
                    fileStruct.exist = false;
                    file.BackColor = Color.Red;
                }
                file.Nodes.Add("Flag: " + fileStruct.flag.ToString("X4"));
                file.Nodes.Add("Block size: " + fileStruct.blockSize + " bytes");
                file.Nodes.Add("Full path: " + fileStruct.filePath);
                file.Nodes.Add("File size: " + fileStruct.fileSize.ToString("#,##0.") + " bytes");
                file.Nodes.Add("SHA1 hash: 0x" + BitConverter.ToString(fileStruct.sha1).Replace("-", string.Empty));
            }
        }

        private void fixSizesAndHashesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (tocHnd != null)
                new WindowProgressForm(tocHnd.fixAll, true).ShowDialog();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (tocHnd == null)
                return;

            string originalTOC = tocHnd.tocFilePath + ".orig";
            if (!File.Exists(originalTOC))
                File.Move(tocHnd.tocFilePath, originalTOC);

            //try
            {
                tocHnd.saveToFile();
                openTOCFile(tocHnd.tocFilePath, ME3Paths.gamePath);

                MessageBox.Show("File saved correctly.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            /*catch (Exception exc)
            {
                MessageBox.Show("An error occurred while saving: " + exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }*/

            recoverFromBackupToolStripMenuItem.Visible = true;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void recoverFromBackupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (tocHnd == null)
                return;

            if (MessageBox.Show("Would you like to recover your original toc.bin file?", "Revert original toc.bin", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.No)
                return;

            File.Delete(tocHnd.tocFilePath);

            string originalTOC = tocHnd.tocFilePath + ".orig";
            File.Move(originalTOC, tocHnd.tocFilePath);

            recoverFromBackupToolStripMenuItem.Visible = false;

            openTOCFile(tocHnd.tocFilePath, ME3Paths.gamePath);
        }

        private void removeNotExistingFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (tocHnd == null)
                return;

            try
            {
                new WindowProgressForm(tocHnd.removeNotExistingFiles, null).ShowDialog();
                openTOC(tocHnd);

                MessageBox.Show("Not existing files have been removed correctly.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception exc)
            {
                MessageBox.Show("An error occurred while removing: " + exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if ((tocHnd != null && tocHnd.bChanged) && (MessageBox.Show("There are unsaved operations, would you like to exit anyway?", "Close", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No))
                e.Cancel = true;
        }

        private void treeViewTOC_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                treeViewTOC.SelectedNode = e.Node;
                TreeNode treeNode = treeViewTOC.SelectedNode;
                if (isBlock(treeNode))
                    contextMenuStripBlock.Show(MousePosition);
                else if (isFile(treeNode))
                    contextMenuStripFiles.Show(MousePosition);
            }
        }

        private void emptyBlockToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tocHnd.bChanged = true;

            TreeNode selNode = treeViewTOC.SelectedNode;
            string blockName = selNode.Name;
            int blockIndex = Convert.ToInt32(blockName.Remove(0, 6));
            tocHnd.chunkList[blockIndex].fileList = null;

            blockToTreeView(tocHnd.chunkList[blockIndex], selNode);
        }

        private void removeFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode selNode = treeViewTOC.SelectedNode;
            string blockName = selNode.Parent.Name;
            int blockIndex = Convert.ToInt32(blockName.Remove(0, 6));
            string fileToRemove = selNode.Name;

            try
            {
                tocHnd.removeFile(fileToRemove);
            }
            catch (Exception exc)
            {
                MessageBox.Show("An error occurred while removing \"" + fileToRemove + "\": " + exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            blockToTreeView(tocHnd.chunkList[blockIndex], selNode.Parent);
        }

        private void addFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string checkFolder = tocHnd.gamePath;
            OpenFileDialog addFileDialog = new OpenFileDialog();
            addFileDialog.InitialDirectory = checkFolder;

            if (addFileDialog.ShowDialog() != DialogResult.OK)
                return;

            string addFileName = addFileDialog.FileName;
            TreeNode selNode = treeViewTOC.SelectedNode;
            string blockName = selNode.Name;
            int blockIndex = Convert.ToInt32(blockName.Remove(0, 6));

            try
            {
                tocHnd.addFile(addFileName, blockIndex);
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message, "Can't add file", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            blockToTreeView(tocHnd.chunkList[blockIndex], selNode);
        }

        private void removeBlockToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tocHnd.bChanged = true;

            TreeNode selNode = treeViewTOC.SelectedNode;
            string blockName = selNode.Name;
            int blockIndex = Convert.ToInt32(blockName.Remove(0, 6));

            blockToTreeView(tocHnd.chunkList[blockIndex], selNode);
        }
    }
}
