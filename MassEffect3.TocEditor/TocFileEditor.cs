using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MassEffect3.TocEditor
{
	public partial class TocFileEditor : Form
	{
		private TocHandler _tocHnd;
		private string _tocbin;

		private string _gamePath;
		private string _cookedPath;

		public TocFileEditor()
		{
			InitializeComponent();
		}

		private string GetFileItemPath(TocHandler.FileStruct fileStruct)
		{
			var tocPath = Path.GetDirectoryName(_tocbin);
			var path = fileStruct.FilePath;

			if (!string.IsNullOrEmpty(path))
			{
				if (path.StartsWith("BioGame\\", StringComparison.OrdinalIgnoreCase))
				{
					path = path.Remove(0, "BioGame\\".Length);
				}
			}

			if (tocPath != null)
			{
				return Path.Combine(tocPath, path);
			}

			return null;
		}

		private void openToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (openFileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			_tocbin = openFileDialog.FileName;
			OpenTocFile(_tocbin, _cookedPath);

			var originalToc = _tocbin + ".orig";

			if (File.Exists(originalToc))
			{
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

		private void OpenTocFile(string tocbin, string gamePath = "")
		{
			if (string.IsNullOrEmpty(gamePath))
			{
				gamePath = Path.GetDirectoryName(tocbin);
			}

			_tocHnd = new TocHandler(tocbin, gamePath);

			OpenToc(_tocHnd);
		}

		private void OpenToc(TocHandler tocHnd)
		{
			treeViewTOC.BeginUpdate();
			treeViewTOC.Nodes.Clear();

			foreach (var chunk in tocHnd.ChunkList)
			{
				BlockToTreeView(chunk);
			}

			treeViewTOC.EndUpdate();
		}

		private void BlockToTreeView(TocHandler.Chunk block, TreeNode selNode = null)
		{
			var blockIdx = _tocHnd.ChunkList.IndexOf(block);
			var blockText = "Block " + (blockIdx + 1) + ": " + block.CountNextFiles + " files" +
							((block.CountNextFiles == 0)
								? ""
								: ", relative offset 0x" + block.RelPosition.ToString("X4") + ", block size: " +
								block.GlobalSize.ToString("#,##0.") + " bytes"
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

			if (block.CountNextFiles == 0)
			{
				return;
			}

			//var fileCount = 0;

			foreach (var fileStruct in block.FileList)
			{
				var file = root.Nodes.Add(fileStruct.FilePath, Path.GetFileName(fileStruct.FilePath));
				file.ToolTipText = "Right click to edit file";
				//fileCount++;

				//if (!File.Exists(_gamePath + fileStruct.FilePath))
				if (!File.Exists(GetFileItemPath(fileStruct)))
				{
					fileStruct.Exist = false;
					file.BackColor = Color.Red;
				}

				file.Nodes.Add("Block size: " + fileStruct.BlockSize + " bytes");
				file.Nodes.Add("Full path: " + fileStruct.FilePath);
				file.Nodes.Add("File size: " + fileStruct.FileSize.ToString("#,##0.") + " bytes");
				file.Nodes.Add("SHA1 hash: 0x" + BitConverter.ToString(fileStruct.Sha1).Replace("-", string.Empty));
			}
		}

		private void fixSizesAndHashesToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (_tocHnd != null)
			{
				new WindowProgressForm(_tocHnd.FixAll, true).ShowDialog();
			}
		}

		private void saveToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (_tocHnd == null)
			{
				return;
			}

			var originalToc = _tocHnd.TocFilePath + ".orig";

			if (!File.Exists(originalToc))
			{
				File.Move(_tocHnd.TocFilePath, originalToc);
			}

			//try
			{
				_tocHnd.SaveToFile();
				OpenTocFile(_tocHnd.TocFilePath, _gamePath);

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
			Close();
		}

		private void recoverFromBackupToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (_tocHnd == null)
			{
				return;
			}

			if (
				MessageBox.Show("Would you like to recover your original toc.bin file?", "Revert original toc.bin",
					MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.No)
			{
				return;
			}

			File.Delete(_tocHnd.TocFilePath);

			var originalToc = _tocHnd.TocFilePath + ".orig";
			File.Move(originalToc, _tocHnd.TocFilePath);

			recoverFromBackupToolStripMenuItem.Visible = false;

			OpenTocFile(_tocHnd.TocFilePath, _gamePath);
		}

		private void removeNotExistingFilesToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (_tocHnd == null)
			{
				return;
			}

			try
			{
				new WindowProgressForm(_tocHnd.RemoveNotExistingFiles, null).ShowDialog();
				OpenToc(_tocHnd);

				MessageBox.Show("Not existing files have been removed correctly.", "Done", MessageBoxButtons.OK,
					MessageBoxIcon.Information);
			}
			catch (Exception exc)
			{
				MessageBox.Show("An error occurred while removing: " + exc.Message, "Error", MessageBoxButtons.OK,
					MessageBoxIcon.Error);
			}
		}

		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			if ((_tocHnd != null && _tocHnd.Changed) &&
				(MessageBox.Show("There are unsaved operations, would you like to exit anyway?", "Close", MessageBoxButtons.YesNo,
					MessageBoxIcon.Warning) == DialogResult.No))
			{
				e.Cancel = true;
			}
		}

		private void treeViewTOC_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
		{
			if (e.Button == MouseButtons.Right)
			{
				treeViewTOC.SelectedNode = e.Node;
				var treeNode = treeViewTOC.SelectedNode;
				if (isBlock(treeNode))
				{
					contextMenuStripBlock.Show(MousePosition);
				}
				else if (isFile(treeNode))
				{
					contextMenuStripFiles.Show(MousePosition);
				}
			}
		}

		private void emptyBlockToolStripMenuItem_Click(object sender, EventArgs e)
		{
			_tocHnd.Changed = true;

			var selNode = treeViewTOC.SelectedNode;
			var blockName = selNode.Name;
			var blockIndex = Convert.ToInt32(blockName.Remove(0, 6));
			_tocHnd.ChunkList[blockIndex].FileList = null;

			BlockToTreeView(_tocHnd.ChunkList[blockIndex], selNode);
		}

		private void removeFileToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var selNode = treeViewTOC.SelectedNode;
			var blockName = selNode.Parent.Name;
			var blockIndex = Convert.ToInt32(blockName.Remove(0, 6));
			var fileToRemove = selNode.Name;

			try
			{
				_tocHnd.RemoveFile(fileToRemove);
			}
			catch (Exception exc)
			{
				MessageBox.Show("An error occurred while removing \"" + fileToRemove + "\": " + exc.Message, "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
			}

			BlockToTreeView(_tocHnd.ChunkList[blockIndex], selNode.Parent);
		}

		private void addFileToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var checkFolder = _tocHnd.GamePath;
			var addFileDialog = new OpenFileDialog
			{
				InitialDirectory = checkFolder
			};

			if (addFileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			var addFileName = addFileDialog.FileName;
			var selNode = treeViewTOC.SelectedNode;
			var blockName = selNode.Name;
			var blockIndex = Convert.ToInt32(blockName.Remove(0, 6));

			try
			{
				_tocHnd.AddFile(addFileName, blockIndex);
			}
			catch (Exception exc)
			{
				MessageBox.Show(exc.Message, "Can't add file", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}

			BlockToTreeView(_tocHnd.ChunkList[blockIndex], selNode);
		}

		private void removeBlockToolStripMenuItem_Click(object sender, EventArgs e)
		{
			_tocHnd.Changed = true;

			var selNode = treeViewTOC.SelectedNode;
			var blockName = selNode.Name;
			var blockIndex = Convert.ToInt32(blockName.Remove(0, 6));

			BlockToTreeView(_tocHnd.ChunkList[blockIndex], selNode);
		}
	}
}