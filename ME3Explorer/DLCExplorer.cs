using System;
using System.IO;
using System.Globalization;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
//using System.Text;
using System.Windows.Forms;
using AmaroK86.MassEffect3;
using Gibbed.MassEffect3.FileFormats;

namespace ME3Explorer
{
    public partial class DLCExplorer : Form
    {
        DLCBase dlcBase = null;
        DLCEditor dlcEditor = null;

        public DLCExplorer()
        {
            InitializeComponent();
            //sort tree nodes by name
            treeViewSfar.Sort();

            //let open sfar files using windows "open with" option and
            //then open sfar files with double clicking on them
            string[] arguments = Environment.GetCommandLineArgs();

            //We were just passed the DLC SFAR name
            if (arguments.Length == 2)
            {
                try
                {
                    string dlcFileName = arguments[1];
                    if (File.Exists(dlcFileName))
                        openSfarFile(dlcFileName);
                    else
                        throw new FileNotFoundException("File not found.");
                }
                catch (Exception exc)
                {
                    MessageBox.Show("An error occurred while opening " + Path.GetFileName(arguments[1]) + ":\n" + exc.Message, "ME3 DLC Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(0);
                    Application.Exit();
                }
            }
        }

        //------------------------- Auxiliary Functions --------------------------

        private void setSize(long byteVal, out float outVal, ref string byteSize)
        {
            int count = 0;
            outVal = (float)byteVal;
            while (outVal > 1024)
            {
                outVal /= 1024;
                count++;
            }
            switch (count)
            {
                case 0: byteSize = "Bytes"; break;
                case 1: byteSize = "KBytes"; break;
                case 2: byteSize = "MBytes"; break;
                case 3: byteSize = "GBytes"; break;
            }
        }

        string getFolderPath(TreeNode node)
        {
            if (!isRoot(node))
                return getFolderPath(node.Parent) + "/" + node.Text;
            else
                return "";
        }

        private bool isFile(TreeNode node)
        {
            return (node.Nodes.Count == 0);
        }

        private bool isFolder(TreeNode node)
        {
            return !isFile(node);
        }

        private bool isRoot(TreeNode node)
        {
            return node.Parent == null;
        }

        //------------------------- GUI Menu Functions --------------------------

        private void openSfarFile(string fileName)
        {
            dlcBase = new DLCBase(fileName);
            dlcEditor = new DLCEditor(dlcBase);

            


            TreeNode root = new TreeNode(fileName);
            root.Name = fileName;
            TreeNode node = root;
            treeViewSfar.Nodes.Clear();
            treeViewSfar.Nodes.Add(root);

            foreach (sfarFile entry in dlcBase.fileList)
            {
                if (entry.fileName != null)
                {
                    string filePath = entry.fileName;
                    node = root;
                    System.Diagnostics.Debug.WriteLine("Parsing: " + entry.fileName);
                    string[] pathBits = filePath.Substring(1).Split('/');
                    for (int i = 0; i < pathBits.Length - 1; i++)
                    {
                        System.Diagnostics.Debug.WriteLine("Parsing pathbit: " + pathBits[i]);
                        node = AddNode(node, pathBits[i]);
                    }
                    //Add the 'file' object to the list
                    TreeNode last = node.Nodes.Add(pathBits[pathBits.Length - 1], pathBits[pathBits.Length - 1]);
                    last.Name = filePath;

                    switch (Path.GetExtension(filePath))
                    {
                        case ".afc": last.ImageIndex = 3; break;
                        case ".bik": last.ImageIndex = 4; break;
                        case ".tfc": last.ImageIndex = 2; break;
                        default: last.ImageIndex = 1; break;
                    }
                }
            }
            float fileSize;
            string strFileSize = "";

            textBoxNumOfFiles.Text = dlcBase.numOfFiles.ToString();

            //show the total uncompressed size
            setSize(dlcBase.totalUncSize, out fileSize, ref strFileSize);
            textBoxTotalUncSize.Text = fileSize.ToString("0.0", CultureInfo.InvariantCulture);
            labelTotalUncBytes.Text = strFileSize;

            //show the total compressed size
            setSize(dlcBase.totalComprSize, out fileSize, ref strFileSize);
            textBoxTotalComprSize.Text = fileSize.ToString("0.0", CultureInfo.InvariantCulture);
            labelTotalComprBytes.Text = strFileSize;

            textBoxCRatio.Text = ((float)dlcBase.totalComprSize / (float)dlcBase.totalUncSize * (float)100).ToString("0.#") + "%";
            textBoxFirstEntryOffset.Text = "0x" + dlcBase.entryOffset.ToString("X8");
            textBoxFirstBlockOffset.Text = "0x" + dlcBase.blockTableOffset.ToString("X8");
            textBoxFirstDataOffset.Text = "0x" + dlcBase.dataOffset.ToString("X8");

            //enable the right-click menu for nodes selection
            treeViewSfar.ContextMenuStrip = contextMenuStripEditor;

            //clear the previous values;
            textBoxFullName.Text = "";
            textBoxHash.Text = "";
            textBoxUncSize.Text = "";
            textBoxComprSize.Text = "";
            textBoxEntry.Text = "";
            textBoxBlockIndex.Text = "";
            textBoxDataOffset.Text = "";

            toolStripSaveFile.Enabled = false;
        }

        private TreeNode AddNode(TreeNode node, string key)
        {
            if (node.Nodes.ContainsKey(key))
            {
                return node.Nodes[key];
            }
            else
            {
                return node.Nodes.Add(key, key, 0);
            }
        }

        //------------------------- GUI Treeview Functions --------------------------

        private void treeViewSfar_AfterSelect(object sender, TreeViewEventArgs e)
        {
            TreeNode node = treeViewSfar.SelectedNode;
            if (node == null || isFolder(node))
                return;
            //prevents the image change
            node.SelectedImageIndex = node.ImageIndex;

            // if the program is extracting or replacing a file, it doesn't update the bottom status label
            if (!backgroundWorkerExtractFile.IsBusy && !backgroundWorkerEditFile.IsBusy)
            {
                toolStripStatusLabel.Text = node.Name;
                toolStripStatusLabel.Visible = true;
            }

            if (!dlcBase.fileList.Contains(FileNameHash.Compute(node.Name)))
                return;

            //the hard part begins here!
            sfarFile entry = dlcBase.fileList[FileNameHash.Compute(node.Name)];
            int fileBlockIndex;
            int fileComprSize = 0;
            float fileSize;
            string strFileSize = "";

            fileBlockIndex = entry.blockSizeIndex;
            textBoxFullName.Text = node.Name;
            textBoxHash.Text = entry.nameHash.ToString();

            setSize((long)entry.uncompressedSize, out fileSize, ref strFileSize);
            textBoxUncSize.Text = fileSize.ToString("0.0", CultureInfo.InvariantCulture);
            labelUncSizeBytes.Text = strFileSize;

            if (fileBlockIndex != -1)
            {
                for (int i = 0; i < entry.blockSizeArray.Length; i++)
                    fileComprSize += entry.blockSizeArray[i];

                setSize((long)fileComprSize, out fileSize, ref strFileSize);
                textBoxComprSize.Text = fileSize.ToString("0.0", CultureInfo.InvariantCulture);
                labelComprSizeBytes.Text = strFileSize;
            }
            else
            {
                setSize((long)entry.uncompressedSize, out fileSize, ref strFileSize);
                textBoxComprSize.Text = fileSize.ToString("0.0", CultureInfo.InvariantCulture);
                labelComprSizeBytes.Text = strFileSize;
            }
            textBoxEntry.Text = "0x" + entry.entryOffset.ToString("X8");
            textBoxBlockIndex.Text = fileBlockIndex.ToString();
            textBoxDataOffset.Text = "0x" + entry.dataOffset[0].ToString("X8");

        }

        //this function set what happens when click above a treenode
        private void treeViewSfar_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button.Equals(MouseButtons.Right))
            {
                if (e.Node != null)
                {
                    treeViewSfar.SelectedNode = e.Node;
                    if (isRoot(e.Node) || isFolder(e.Node))
                    {
                        if(isRoot(e.Node))
                            extractToolStripMenuItem.Visible = false;
                        else
                            extractToolStripMenuItem.Visible = true;

                        addFileToolStripMenuItem.Visible = true;
                        deleteToolStripMenuItem.Visible = false;
                        replaceToolStripMenuItem.Visible = false;
                        propertiesToolStripMenuItem.Visible = false;
                        undoToolStripMenuItem.Visible = false;
                    }
                    else //file operations
                    {
                        addFileToolStripMenuItem.Visible = false;
                        deleteToolStripMenuItem.Visible = false;
                        extractToolStripMenuItem.Visible = false;
                        replaceToolStripMenuItem.Visible = false;
                        propertiesToolStripMenuItem.Visible = false;

                        undoToolStripMenuItem.Visible = true;
                        switch (dlcEditor.listComplete[FileNameHash.Compute(e.Node.Name)])
                        {
                            case DLCEditor.action.add:
                                undoToolStripMenuItem.Text = "Undo add";
                                break;
                            case DLCEditor.action.delete:
                                undoToolStripMenuItem.Text = "Undo delete";
                                break;
                            case DLCEditor.action.replace:
                                undoToolStripMenuItem.Text = "Undo replace";
                                break;
                            case DLCEditor.action.copy:
                                deleteToolStripMenuItem.Visible = true;
                                extractToolStripMenuItem.Visible = true;
                                replaceToolStripMenuItem.Visible = true;
                                propertiesToolStripMenuItem.Visible = true;
                                undoToolStripMenuItem.Visible = false;
                                break;
                        }
                    }

                    //disable all actions when background operations are active
                    if (backgroundWorkerExtractFile.IsBusy || backgroundWorkerEditFile.IsBusy)
                    {
                        addFileToolStripMenuItem.Visible = false;
                        deleteToolStripMenuItem.Visible = false;
                        extractToolStripMenuItem.Visible = false;
                        replaceToolStripMenuItem.Visible = false;
                    }

                    contextMenuStripEditor.Show(MousePosition);
                }
            }
        }

        //------------------------- GUI Context Menu Functions --------------------------

        private void addFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode node = treeViewSfar.SelectedNode;
            if (isFolder(node) && DialogSelectFileToReplace.ShowDialog() == DialogResult.OK)
            {
                string selectedFile = Path.GetFileName(DialogSelectFileToReplace.FileName);
                string dlcNewFile = getFolderPath(node) + "/" + selectedFile;

                //check if the added file already exists in the dlc archive
                if (dlcBase.fileList.Contains(FileNameHash.Compute(dlcNewFile)))
                {
                    DialogResult replaceQuestion = MessageBox.Show("Warning! " + dlcNewFile + " already exist in the archive, would you like to replace it?", "Warning, adding existing file", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
                    if (replaceQuestion == DialogResult.No)
                        return;
                    else
                    {
                        TreeNode replaceNode = node.Nodes[node.Nodes.IndexOfKey(dlcNewFile)];
                        dlcEditor.setReplaceFile(dlcNewFile, DialogSelectFileToReplace.FileName);
                        replaceNode.BackColor = Color.Yellow;
                        toolStripSaveFile.Enabled = true;
                        return;
                    }
                }

                TreeNode last = node.Nodes.Add(selectedFile, selectedFile);
                last.Name = dlcNewFile;
                last.BackColor = Color.LightGreen;
                switch (Path.GetExtension(selectedFile))
                {
                    case ".afc": last.ImageIndex = 3; break;
                    case ".bik": last.ImageIndex = 4; break;
                    case ".tfc": last.ImageIndex = 2; break;
                    default: last.ImageIndex = 1; break;
                }

                dlcEditor.setAddFile(dlcNewFile, DialogSelectFileToReplace.FileName);
                toolStripSaveFile.Enabled = true;
                treeViewSfar.SelectedNode = null;
            }
        }

        private void replaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode node = treeViewSfar.SelectedNode;
            if (isFile(node) && DialogSelectFileToReplace.ShowDialog() == DialogResult.OK)
            {
                string selectedFile = Path.GetFileName(DialogSelectFileToReplace.FileName);
                if (String.Compare(selectedFile, node.Text) != 0)
                {
                    DialogResult replaceQuestion = MessageBox.Show("Warning: " + selectedFile + " has a different name from the original " + node.Text + ", would you like to continue?", "Warning, different file names", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
                    if (replaceQuestion == DialogResult.No)
                        return;
                }

                if (dlcEditor.isFileSetForDelete(node.Name))
                    dlcEditor.undoDeleteFile(node.Name);

                dlcEditor.setReplaceFile(node.Name, DialogSelectFileToReplace.FileName);
                node.BackColor = Color.Yellow;
                toolStripSaveFile.Enabled = true;
                treeViewSfar.SelectedNode = null;
            }
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode node = treeViewSfar.SelectedNode;
            DialogResult replaceQuestion = MessageBox.Show("Are you sure you want to select " + node.Text + " for deletion?", "Delete file selection", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
            if (replaceQuestion == DialogResult.Yes)
            {
                if (dlcEditor.isFileSetForReplacement(node.Name))
                    dlcEditor.undoReplaceFile(node.Name);

                dlcEditor.setDeleteFile(node.Name);
                node.BackColor = Color.Red;
                toolStripSaveFile.Enabled = true;
                treeViewSfar.SelectedNode = null; // disable node selection to show the color
            }
        }

        private void extractToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode node = treeViewSfar.SelectedNode;
            extractFileDialog.FileName = node.Text;
            List<sfarFile> listFiles = new List<sfarFile>();

            //add selected file(s) to the extraction list
            if (isFile(node) && extractFileDialog.ShowDialog() == DialogResult.OK)
            {
                sfarFile entry = dlcBase.fileList[FileNameHash.Compute(node.Name)];
                listFiles.Add(entry);
            }
            else if (isFolder(node) && extractFolderDialog.ShowDialog() == DialogResult.OK)
            {
                foreach (sfarFile entry in dlcBase.fileList)
                {
                    string fileName = entry.fileName;
                    int indexStr;
                    if (fileName == null)
                        continue;
                    indexStr = fileName.IndexOf(node.Text);
                    if (indexStr != -1)
                    {
                        listFiles.Add(entry);
                    }
                }
            }
            else // safety else, hopefully never enters
            {
                return;
            }

            toolStripProgressBar.Visible = true;
            toolStripStatusLabel.Visible = true;

            try
            {
                //main extraction
                backgroundWorkerExtractFile.RunWorkerAsync(new object[2] { listFiles, node });
                while (backgroundWorkerExtractFile.IsBusy)
                {
                    // Keep UI messages moving, so the form remains 
                    // responsive during the asynchronous operation.
                    if (backgroundWorkerExtractFile.CancellationPending)
                        return;
                    else
                        Application.DoEvents();
                }

                toolStripStatusLabel.Text = "Done.";

                if (isFile(node))
                    MessageBox.Show("File " + node.Text + " has been successfully extracted.", "Extraction success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show("All files from folder " + node.Text + " have been successfully extracted.", "Extraction success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                toolStripProgressBar.Visible = false;
            }
            catch (Exception exc)
            {
                MessageBox.Show("An error occurred while extracting " + node.Text + ":\n" + exc.Message, "Exception Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #region backgroundworkers

        private void backgroundWorkerExtractFile_DoWork(object sender, DoWorkEventArgs e)
        {
            // Get the BackgroundWorker that raised this event.
            BackgroundWorker worker = sender as BackgroundWorker;

            //extracting arguments
            object[] args = e.Argument as object[];
            List<sfarFile> listFiles = (List<sfarFile>)args[0];
            TreeNode node = (TreeNode)args[1];
            string finalPath;

            int count = 1;

            //extract files from the extraction list
            foreach (sfarFile entry in listFiles)
            {
                string fileName = entry.fileName;
                if (isFile(node))
                {
                    finalPath = extractFileDialog.FileName;
                }
                else
                {
                    string fullPath;
                    int indexStr = fileName.IndexOf(node.Text);
                    string chunkPart = fileName.Substring(indexStr);
                    chunkPart = chunkPart.Replace("/", "\\");
                    fullPath = extractFolderDialog.SelectedPath + "\\" + chunkPart;
                    //creating full folder structure
                    if (!Directory.Exists(Path.GetDirectoryName(fullPath)))
                        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    finalPath = fullPath;
                }

                worker.ReportProgress(0,count++ + "/" + listFiles.Count + ": Extracting " + Path.GetFileName(fileName));

                using (Stream input = File.OpenRead(dlcBase.fileName), output = File.Create(finalPath))
                {
                    DLCUnpack.DecompressEntry(entry, input, output, dlcBase.CompressionScheme, worker);
                }
                e.Result = true;

                worker.ReportProgress(100);
                //toolStripProgressBar.Value = count++;
            }//end foreach

        }

        private void backgroundWorkerExtractFile_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if(e.UserState != null)
                toolStripStatusLabel.Text = e.UserState as String;
            try
            {
                this.toolStripProgressBar.Value = e.ProgressPercentage;
            }
            catch { }
        }

        private void backgroundWorkerReplaceFile_DoWork(object sender, DoWorkEventArgs e)
        {
            // Get the BackgroundWorker that raised this event.
            BackgroundWorker worker = sender as BackgroundWorker;

            //extracting arguments
            object[] args = e.Argument as object[];

            string newSfar = (string)args[0];
            dlcEditor.Execute(newSfar, worker);
        }

        private void backgroundWorkerReplaceFile_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState != null)
                toolStripStatusLabel.Text = e.UserState as String;
            try
            {
                this.toolStripProgressBar.Value = e.ProgressPercentage;
            }
            catch { }
        }

        #endregion

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            taskbar.RemoveTool(this);
            //exit immediately if no files were loaded
            if (dlcBase == null)
                return;

            if (backgroundWorkerEditFile.IsBusy || backgroundWorkerExtractFile.IsBusy)
            {
                DialogResult closeQuestion = MessageBox.Show("There are background operations executing, would you like to close the program anyway?", "Force program closing", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (closeQuestion == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
                //when closing the program, if there are background operations, these operations terminate them
                if (backgroundWorkerEditFile.IsBusy)
                    backgroundWorkerEditFile.CancelAsync();
                if (backgroundWorkerExtractFile.IsBusy)
                    backgroundWorkerExtractFile.CancelAsync();
            }
            else
            {
                if (dlcEditor.checkForExec())
                {
                    DialogResult closeQuestion = MessageBox.Show("There are unsaved operations, would you like to exit anyway?", "Pending modified operations", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (closeQuestion == DialogResult.No)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }
         
        }

        private void toolStripOpenFile_Click(object sender, EventArgs e)
        {
            if (DialogOpenSfarFile.ShowDialog() == DialogResult.OK)
            {
                openSfarFile(DialogOpenSfarFile.FileName);
            }
        }

        private void propertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode node = treeViewSfar.SelectedNode;
            string message = "";
            float fileSize;
            float comprRatio;
            string strFileSize = "";

            if (isFile(node))
            {
                FileNameHash fileHash = FileNameHash.Compute(node.Name);
                sfarFile entry = dlcBase.fileList[fileHash];
                int compressedSize = 0;
                if (entry.blockSizeIndex != -1)
                    foreach (int i in entry.blockSizeArray)
                        compressedSize += i;
                else
                    compressedSize = (int)entry.uncompressedSize;

                message += "Full Path:      " + entry.fileName +
                           "\n\nHash file name: " + fileHash;

                setSize(entry.uncompressedSize, out fileSize, ref strFileSize);
                message += "\n\nFile size: " + fileSize.ToString("0.0", CultureInfo.InvariantCulture) + " " + strFileSize;
                message += " (" + entry.uncompressedSize.ToString("0,0", CultureInfo.InvariantCulture) + " Bytes)";

                setSize(compressedSize, out fileSize, ref strFileSize);
                message += "\n\nCompressed size: " + fileSize.ToString("0.0", CultureInfo.InvariantCulture) + " " + strFileSize;
                message += " (" + compressedSize.ToString("0,0", CultureInfo.InvariantCulture) + " Bytes)";

                comprRatio = (float)compressedSize / (float)entry.uncompressedSize * (float)100;
                message += "\n\nCompression Ratio: " + comprRatio.ToString("0.#") + "%";

                MessageBox.Show(message, "Properties - " + node.Text, MessageBoxButtons.OK, MessageBoxIcon.None);
            }
        }

        private void toolStripInfo_Click(object sender, EventArgs e)
        {
            string message = "";
            float fileSize;
            float comprRatio;
            string strFileSize = "";

            if (dlcBase != null && dlcBase.fileName != "")
            {
                message += "Full Path: " + Path.GetFullPath(dlcBase.fileName);
                message += "\n\nNum. of files: " + dlcBase.numOfFiles;

                //show the total uncompressed size
                setSize(dlcBase.totalUncSize, out fileSize, ref strFileSize);
                message += "\n\nUncompressed files size: " + fileSize.ToString("0.0", CultureInfo.InvariantCulture) + " " + strFileSize;
                message += " (" + dlcBase.totalUncSize.ToString("0,0", CultureInfo.InvariantCulture) + " Bytes)";

                //show the total compressed size
                setSize(dlcBase.totalComprSize, out fileSize, ref strFileSize);
                message += "\n\nCompressed files size: " + fileSize.ToString("0.0", CultureInfo.InvariantCulture) + " " + strFileSize;
                message += " (" + dlcBase.totalComprSize.ToString("0,0", CultureInfo.InvariantCulture) + " Bytes)";

                comprRatio = (float)dlcBase.totalComprSize / (float)dlcBase.totalUncSize * (float)100;
                message += "\n\nCompression Ratio: " + comprRatio.ToString("0.#") + "%";

                message += "\n\nOffsets:";
                message += "\nEntries:        0x" + dlcBase.entryOffset.ToString("X8");
                message += "\nBlocks Size: 0x" + dlcBase.blockTableOffset.ToString("X8");
                message += "\nData:            0x" + dlcBase.dataOffset.ToString("X8");

                MessageBox.Show(message, "Properties - DLC Archive", MessageBoxButtons.OK, MessageBoxIcon.None);
            }
        }

        private void toolStripSaveFile_Click(object sender, EventArgs e)
        {
            try
            {
                DialogResult backupQuestion = DialogResult.No;
                backupQuestion = MessageBox.Show("Would you like to create a backup file?", "Create backup file", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                string oldSfar = dlcBase.fileName;
                string fileToReplace = DialogSelectFileToReplace.FileName;
                string newSfar = oldSfar + ".tmp";

                toolStripSaveFile.Enabled = false;
                toolStripStatusLabel.Visible = true;
                //toolStripStatusLabel.Text = "Editing " + Path.GetFileName(oldSfar);
                //this.Refresh();
                toolStripProgressBar.Value = 0;
                toolStripProgressBar.Maximum = 100;
                toolStripProgressBar.Visible = true;

                //backgroundWorkerEditFile.RunWorkerAsync(new object[4] { oldSfar, node.Name, fileToReplace, newSfar });
                backgroundWorkerEditFile.RunWorkerAsync(new object[1] { newSfar });
                while (backgroundWorkerEditFile.IsBusy)
                {
                    // Keep UI messages moving, so the form remains 
                    // responsive during the asynchronous operation.
                    if (backgroundWorkerEditFile.CancellationPending)
                        return;
                    else
                        Application.DoEvents();
                }

                if (backupQuestion == DialogResult.Yes)
                    File.Replace(newSfar, oldSfar, oldSfar + ".bak");
                else
                {
                    if (File.Exists(oldSfar))
                        File.Delete(oldSfar);
                    File.Move(newSfar, oldSfar);
                }

                toolStripStatusLabel.Text = "Done.";
                this.Refresh();
                MessageBox.Show("File " + dlcBase.fileName + " has been successfully edited.", "Replacement success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                toolStripProgressBar.Visible = false;

                openSfarFile(oldSfar);
            }
            catch (Exception exc)
            {
                MessageBox.Show("An error occurred while editing " + dlcBase.fileName + ":\n" + exc.ToString(), "Exception Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                toolStripSaveFile.Enabled = true; 
                toolStripProgressBar.Visible = false;
            }
        }

        private void toolStripAbout_Click(object sender, EventArgs e)
        {
            System.Reflection.Assembly objAssembl = System.Reflection.Assembly.GetExecutingAssembly();
            MessageBox.Show("Program: AmaroK86.MassEffect3.DLCExplorer" +
                            "\n\nAuthor: AmaroK86" +
                            "\n\nE-Mail: marcidm@hotmail.com" +
                            "\n\nVersion: 1.1.0.4", "About", MessageBoxButtons.OK, MessageBoxIcon.None);
        }

        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode node = treeViewSfar.SelectedNode;
            DLCEditor.action undoAction = dlcEditor.listComplete[FileNameHash.Compute(node.Name)];
            switch (undoAction)
            {
                case DLCEditor.action.add:
                    treeViewSfar.SelectedNode.Remove();
                    dlcEditor.undoAddFile(node.Name);
                    break;
                case DLCEditor.action.delete:
                    node.BackColor = Color.Empty;
                    dlcEditor.undoDeleteFile(node.Name);
                    break;
                case DLCEditor.action.replace:
                   node.BackColor = Color.Empty;
                    dlcEditor.undoReplaceFile(node.Name);
                    break;
                case DLCEditor.action.copy:
                    break;
            }
            toolStripSaveFile.Enabled = dlcEditor.checkForExec();
        }
    }
}

