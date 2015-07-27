using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using AmaroK86.ImageFormat;
using KFreonLib.Textures;
using KFreonLib.MEDirectories;
using KFreonLib.PCCObjects;


namespace ME3Explorer
{
    public partial class AssetExplorer : Form
    {
        public string pathME3exe;
        public string pathCooked;
        public string pathBIOGame;
        public string currentPCC;
        PCCObject pcc = null;
        AmaroK86.MassEffect3.DLCBase currentDLC = null;
        Texture2D tex2D;
        WwiseStream w;
        public Clipboard clip;
        string mainPCCFolder = "Default PCC Files";

        public class ClipboardDependency
        {
            public string Name;
            public PCCObject.ImportEntry classimp;
            public ClipboardDependency child;
        }

        public struct Clipboard
        {
            public PCCObject.ExportEntry entry;
            public ClipboardDependency dep; //for class
            public string Name;
            public bool isFilled;
        }

        public AssetExplorer()
        {
            InitializeComponent();
        }

        public void LoadMe()
        {
            clip = new Clipboard();
            clip.isFilled = false;
            if (ME3Directory.gamePath != null)
            {
                pathME3exe  = ME3Directory.gamePath + @"Binaries\Win32\MassEffect3.exe";
                pathBIOGame = ME3Directory.gamePath + @"BIOGame\";
                pathCooked  = ME3Directory.cookedPath;
                Println("Found MassEffect3.exe : " + pathME3exe);
                Println("Cooked Folder : " + pathCooked);
                Println("Loading files...");
                LoadFileNames();
            }
            else
            {
                this.Close();
            }
        }

        public void LoadFileNames()
        {
            TV1.BeginUpdate();
            TV1.Nodes.Clear();
            TV1.Sort();

            // add pcc list of main directory
            currentPCC = "";
            string[] files = Directory.GetFiles(pathCooked, "*.pcc");
            TreeNode t = new TreeNode(mainPCCFolder);
            t.ImageIndex = 1;
            for (int i = 0; i < files.Length; i++)
            {
                TreeNode t2 = new TreeNode(Path.GetFileName(files[i]));
                t2.ImageIndex = 1;
                t.Nodes.Add(t2);
            }
            TV1.Nodes.Add(t);

            // add pcc list of every dlc found
            /*string[] dlcs = Directory.GetDirectories(ME3Directory.DLCPath, "DLC_*");
            foreach (string dlcPath in dlcs)
            {
                // build path of current dlc
                string dlcName = Path.GetFileName(dlcPath);
                string dlcFullPath = ME3Directory.DLCFilePath(dlcName);

                // build root node of current dlc
                TreeNode dlcNode = new TreeNode(dlcName);
                dlcNode.ImageIndex = 1;
                TV1.Nodes.Add(dlcNode);

                // load list of pcc stored in current dlc and add nodes to root node
                AmaroK86.MassEffect3.DLCBase dlcBase = new AmaroK86.MassEffect3.DLCBase(dlcFullPath);
                List<string> dlcPccList = dlcBase.fileNameList.Where(fileName => Path.GetExtension(fileName) == ".pcc").ToList();
                foreach (string path in dlcPccList)
                {
                    // pcc are selectable inside treeView throught their full name (ex. "/BIOGame/DLC/DLC_HEN_PR/CookedPCConsole/BioA_Cat001.pcc")
                    TreeNode dlcPccNode = dlcNode.Nodes.Add(path, Path.GetFileName(path));
                    dlcPccNode.ImageIndex = 1;
                }
            }*/

            //TV1.Nodes[0].Expand();
            TV1.EndUpdate();

            listView1.Clear();
        }

        public void LoadFile(string s)
        {
            if (!File.Exists(s))
                return;
            currentPCC = s;
            pcc = new PCCObject(s);
            GeneratePccTree();
        }

        public void GeneratePccTree()
        {
            TV1.BeginUpdate();
            TV1.Nodes.Clear();

            TreeNode root = TV1.Nodes.Add(pcc.pccFileName, pcc.pccFileName);
            root.ImageIndex = 1;

            foreach (PCCObject.ExportEntry exportEntry in pcc.Exports)
            {
                string[] pathChunks = (exportEntry.PackageFullName).Split('.');

                string recombinedPath = "";
                foreach (string chunk in pathChunks)
                {
                    if (recombinedPath == "")
                        recombinedPath += chunk;
                    else
                        recombinedPath += "." + chunk;

                    if (root.Nodes.ContainsKey(recombinedPath))
                        root = root.Nodes[recombinedPath];
                    else
                        root = root.Nodes.Add(recombinedPath, chunk);

                    root.ImageIndex = 1;
                }

                root = TV1.Nodes[pcc.pccFileName];
            }
            root.Expand();

            TV1.Sort();
            TV1.EndUpdate();
        }

        public TreeNode[] FindNode(TreeNode t, string s)
        {
            List<TreeNode> lres = new List<TreeNode>();
            for (int i = 0; i < t.Nodes.Count; i++)
            {
                TreeNode t2 = t.Nodes[i];
                if (t2.Name == s)
                    lres.Add(t2);
                if (t2.Nodes.Count != 0)
                    lres.AddRange(FindNode(t2, s));
            }
            return lres.ToArray();
        }

        public void Print(string s)
        {
            string sout = rtb1.Text;
            sout += s;
            rtb1.Text = sout;
        }

        public void Print(int i)
        {
            string sout = rtb1.Text;
            sout += i.ToString();
            rtb1.Text = sout;
        }

        public void Println(string s)
        {
            string sout = rtb1.Text;
            sout += s + "\n";
            rtb1.Text = sout;
        }

        public void Println(int i)
        {
            string sout = rtb1.Text;
            sout += i.ToString() + "\n";
            rtb1.Text = sout;
        }

        private void TV1_DoubleClick(object sender, EventArgs e)
        {
            TreeNode t = TV1.SelectedNode;
            if (t == null || t.Parent == null)
                return;
            Println("Loading " + t.Text + " ...");

            string DLCName = t.Parent.Text;
            if (DLCName == mainPCCFolder)
            {
                currentPCC = ME3Directory.cookedPath + t.Text;
                pcc = new PCCObject(currentPCC);
            }
            else
            {
                currentPCC = t.Name;
                string tempPCCPath = Path.GetFileName(currentPCC);
                currentDLC = new AmaroK86.MassEffect3.DLCBase(ME3Directory.DLCFilePath(DLCName));
                currentDLC.extractFile(currentPCC, tempPCCPath);
                pcc = new PCCObject(tempPCCPath);
                pcc.bDLCStored = true;
            }

            GeneratePccTree();
        }

        private void TV1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            listView1.Visible = true;
            panelImage.Visible = false;

            if (currentPCC == "")
            {
            }
            else
            {
                TreeNode t = TV1.SelectedNode;
                if (t == null)
                    return;
                //int l = Convert.ToInt32(t.Name);
                listView1.BeginUpdate();
                listView1.Items.Clear();
                for (int i = 0; i < pcc.Exports.Count; i++)
                    if (pcc.Exports[i].ClassName != "Class")
                        switch (pcc.Exports[i].ClassName)
                        {
                            case "Package":
                                break;
                            case "Texture2D":
                                if (pcc.Exports[i].PackageFullName == t.Name)
                                {
                                    ListViewItem it = new ListViewItem("", 1);
                                    it.Text = pcc.Exports[i].ObjectName;
                                    it.Name = i.ToString();
                                    listView1.Items.Add(it);
                                }
                                break;
                            case "Sequence":
                                //if (pcc.Exports[i].Link == l + 1)
                                if (pcc.Exports[i].PackageFullName == t.Name)
                                {
                                    ListViewItem it = new ListViewItem("", 2);
                                    it.Text = pcc.Exports[i].ObjectName;
                                    it.Name = i.ToString();
                                    listView1.Items.Add(it);
                                }
                                break;
                            case "Level":
                                //if (pcc.Exports[i].Link == l + 1)
                                if (pcc.Exports[i].PackageFullName == t.Name)
                                {
                                    ListViewItem it = new ListViewItem("", 3);
                                    it.Text = pcc.Exports[i].ObjectName;
                                    it.Name = i.ToString();
                                    listView1.Items.Add(it);
                                }
                                break;
                            case "WwiseStream":
                                //if (pcc.Exports[i].Link == l + 1)
                                if (pcc.Exports[i].PackageFullName == t.Name)
                                {
                                    ListViewItem it = new ListViewItem("", 4);
                                    it.Text = pcc.Exports[i].ObjectName;
                                    it.Name = i.ToString();
                                    listView1.Items.Add(it);
                                }
                                break;
                            default:
                                //if (pcc.Exports[i].Link == l + 1)
                                if (pcc.Exports[i].PackageFullName == t.Name)
                                {
                                    ListViewItem it = new ListViewItem("", 0);
                                    it.Text = pcc.Exports[i].ObjectName;
                                    it.Name = i.ToString();
                                    listView1.Items.Add(it);
                                }
                                break;
                        }
                listView1.EndUpdate();
                listView1.Refresh();
            }
        }

        private void backToOvervieToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadFileNames();
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0)
                return;
            ListViewItem item = listView1.SelectedItems[0];
            TreeNode t = TV1.SelectedNode;
            if (t == null)
                return;
            int l = Convert.ToInt32(item.Name);
            string name = item.Text;
            //for (int i = 0; i < pcc.ExportCount; i++)
                if (pcc.Exports[l].ObjectName == name)
                {
                    string s = "SIZE: " + pcc.Exports[l].DataSize.ToString();
                    s += " bytes  OFFSET: " + pcc.Exports[l].DataOffset.ToString();
                    s += "  CLASS: " + pcc.Exports[l].ClassName;
                    s += "  NAME: " + pcc.Exports[l].ObjectName;
                    s += "  INDEX: " + l;
                    SetStatus(s);
                }
        }

        public void SetStatus(string s)
        {
            StatusLabel.Text = s;
        }

        private void listView1_MouseClick(object sender, MouseEventArgs e)
        {
            ListView.SelectedIndexCollection n = listView1.SelectedIndices;
            if (n.Count != 1 || pcc == null)
                return;
            int index = Convert.ToInt32(listView1.Items[n[0]].Name);

            if (pcc.Exports[index].ClassName == Texture2D.className)
                tex2D = new Texture2D(pcc, index);
            else
                tex2D = null;

            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                switch (pcc.Exports[index].ClassName)
                {
                    case "Sequence":
                        editToolStripMenuItem1.Visible = true;
                        inSequenceEditorToolStripMenuItem.Visible = true;
                        textureToolStripMenuItem.Visible = false;
                        soundsToolStripMenuItem.Visible = false;
                        contextMenuStrip1.Show(MousePosition);
                        break;
                    case "Texture2D":
                        soundsToolStripMenuItem.Visible = false;
                        editToolStripMenuItem1.Visible = false;
                        textureToolStripMenuItem.Visible = true;
                        contextMenuStrip1.Show(MousePosition);
                        break;
                    //case "Level":
                    //    soundsToolStripMenuItem.Visible = false;
                    //    editToolStripMenuItem1.Visible = true;
                    //    inSequenceEditorToolStripMenuItem.Visible = false;
                    //    textureToolStripMenuItem.Visible = false;
                    //    contextMenuStrip1.Show(MousePosition);
                    //    break;
                    case "WwiseStream":
                        editToolStripMenuItem1.Visible = false;
                        textureToolStripMenuItem.Visible = false;
                        soundsToolStripMenuItem.Visible = true;
                        contextMenuStrip1.Show(MousePosition);
                        break;
                    default:
                        soundsToolStripMenuItem.Visible = false;
                        editToolStripMenuItem1.Visible = false;
                        textureToolStripMenuItem.Visible = false;
                        contextMenuStrip1.Show(MousePosition);
                        break;
                }
            }
        }

        private void copyObjectToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            CopyObject();
        }

        public void CopyObject()
        {
            ListView.SelectedIndexCollection n = listView1.SelectedIndices;
            if (n.Count != 1 || pcc == null)
                return;
            int index = Convert.ToInt32(listView1.Items[n[0]].Name);
            int link = pcc.Exports[index].idxClassName;
            Clipboard temp = new Clipboard();
            temp.entry = pcc.Exports[index];
            temp.Name = pcc.Exports[index].ObjectName;
            temp.dep = FindLink(link);
            temp.isFilled = true;
            clip = temp;
            Println(temp.Name + " copied!");
        }

        public PCCObject.ExportEntry CopyExport(PCCObject.ExportEntry exp)
        {
            PCCObject.ExportEntry ret = new PCCObject.ExportEntry(pcc, exp.Data, exp.offset);
            //ret.childs = exp.childs;
            //ret.ClassName = exp.ClassName;
            //ret.Data = CopyArray(exp.Data);
            //ret.DataSize = exp.DataSize;
            //ret.Link = exp.Link;
            //ret.ObjectName = exp.ObjectName;
            //ret.off = exp.off;
            //ret.raw = CopyArray(exp.raw);
            return ret;
        }

        public byte[] CopyArray(byte[] arr)
        {
            byte[] ret = new byte[arr.Length];
            for (int i = 0; i < arr.Length; i++)
                ret[i] = arr[i];
            return ret;
        }

        public ClipboardDependency FindLink(int link)
        {
            ClipboardDependency dep = new ClipboardDependency();
            dep.classimp = pcc.Imports[link * -1 - 1];
            dep.Name = pcc.Imports[link * -1 - 1].ObjectName;
            Println("Class dep: " + dep.Name);
            if (pcc.Imports[link * -1 - 1].Link < 0)
                dep.child = FindLink(pcc.Imports[link * -1 - 1].Link);
            return dep;
        }

        public void PasteObject()
        {
            TreeNode t = TV1.SelectedNode;
            if (t == null || pcc == null || !clip.isFilled)
                return;
            int nlink = Convert.ToInt32(t.Name);
            PCCObject.ExportEntry ent = CopyExport(clip.entry);
            ent.Link = nlink + 1;
            int found = -1;
            for (int i = 0; i < pcc.Names.Count; i++)
                if (pcc.Names[i] == clip.Name)
                    found = i;
            if (found != -1)
                ent.idxObjectName = found;
            else
            {
                pcc.Names.Add(clip.Name);
                ent.idxObjectName = pcc.Names.Count - 1;
            }
            found = -1;
            for (int i = 0; i < pcc.Imports.Count; i++)
                if (pcc.Imports[i].ObjectName == clip.dep.Name)
                    found = i;
            if (found != -1)
            {
                ent.idxClassName = found * -1 - 1;
                pcc.Exports.Add(ent);
                Println("Done! All found");
            }
            else
            {
                //the tricky part comes here! importing the imports!
            }
        }

        private void copyObjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyObject();
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PasteObject();
        }

        private void saveChangesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null || currentPCC == "")
                return;
            pcc.altSaveToFile(currentPCC, true);
            FileStream fs = new FileStream(currentPCC, FileMode.Open, FileAccess.Read);
            uint size = (uint)fs.Length;
            string name = Path.GetFileName(currentPCC);
            fs.Close();
            TOCeditor tc = new TOCeditor();
            tc.MdiParent = this.ParentForm;
            string tocpath = ME3Directory.tocFile;
            if (File.Exists(tocpath))
            {
                if (!tc.UpdateFile(name, size, tocpath))
                    MessageBox.Show("Didn't found Entry");
            }
            else
            {
                if (!tc.UpdateFile(name, size))
                    MessageBox.Show("Didn't found Entry");
            }
            tc.Close();
            MessageBox.Show("File " + Path.GetFileName(currentPCC) + " saved.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void extractToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string currentDir = ME3Directory.cookedPath;
            string imgFileName = null;
            string imgFileFolder = null;

            // setting TexSelection Form to export images
            TexSelection texSelection = new TexSelection(tex2D);
            texSelection.FormTitle = "Select images to extract";
            texSelection.btnSelectionText = "Extract";
            if (tex2D.imgList.Count > 1)
            {
                texSelection.ShowDialog();
                texSelection.Dispose();
            }
            else
                texSelection.bOk = true;

            if (texSelection.bOk)
            {
                try
                {
                    // check that the image list has at least one image stored inside an external archive, if not it's useless to search the archive location
                    if (tex2D.imgList.Any(images => images.storageType == Texture2D.storage.arcCpr || images.storageType == Texture2D.storage.arcUnc))
                    {
                        // check if archive file is present in the same pcc directory
                        string archivePath = currentDir + tex2D.arcName + ".tfc";
                        if (!File.Exists(archivePath))
                        {
                            OpenFileDialog openArchive = openFileDialog;
                            openArchive.Title = "Select the .tfc archive needed to extract images";
                            openArchive.Filter = tex2D.arcName + ".tfc|" + tex2D.arcName + ".tfc|All files|*.*";

                            // try to open the last opened directory
                            archivePath = openArchive.InitialDirectory + "\\" + tex2D.arcName + ".tfc";
                            if (File.Exists(archivePath)) // if the archive exists then use it directly
                            {
                                currentDir = openArchive.InitialDirectory;
                            }
                            else // if not then the user searches for it
                            {
                                DialogResult resultSelection;
                                do
                                {
                                    resultSelection = openArchive.ShowDialog();
                                } while (!File.Exists(openArchive.FileName) && resultSelection != DialogResult.Cancel);

                                if (resultSelection == DialogResult.Cancel)
                                    return; // exit if user press cancel (or undo)
                                else
                                {
                                    openArchive.InitialDirectory = Path.GetDirectoryName(openArchive.FileName);
                                    currentDir = Path.GetDirectoryName(openArchive.FileName);
                                }
                            }
                        }
                    }

                    if (texSelection.imageListBox.CheckedItems.Count == 1) // if user selected only one image to extract
                    {
                        string imgSize = texSelection.imageListBox.CheckedItems[0].ToString().Split(' ')[1]; // take imagesize (ex. 512x512) from checklist text: "Image 512x512 stored..."
                        SaveFileDialog saveFile = new SaveFileDialog();
                        saveFile.Title = "Select the location to save the image";
                        saveFile.FileName = tex2D.texName + "_" + imgSize + tex2D.getFileFormat();
                        saveFile.Filter = "Image file|*" + tex2D.getFileFormat() + "|All files|*.*";
                        //saveFile.RestoreDirectory = true;
                        DialogResult resultSelection = saveFile.ShowDialog();
                        if (resultSelection == DialogResult.OK)
                        {
                            imgFileName = Path.GetFileName(saveFile.FileName);
                            imgFileFolder = Path.GetDirectoryName(saveFile.FileName);
                        }
                        else
                            return;
                    }
                    else // if user selected multiple images to extract
                    {
                        FolderBrowserDialog saveFolder = new FolderBrowserDialog();
                        saveFolder.Description = "Select the folder location to save all the images";
                        DialogResult resultSelection = saveFolder.ShowDialog();
                        if (resultSelection == DialogResult.OK)
                        {
                            imgFileFolder = saveFolder.SelectedPath;
                        }
                        else
                            return;
                    }

                    // main extraction loop
                    foreach (Object entry in texSelection.imageListBox.CheckedItems)
                    {
                        string imgSize = entry.ToString().Split(' ')[1]; // take imagesize (ex. 512x512) from checklist text: "Image 512x512 stored..."
                        string imgFinalFileName;
                        if (imgFileName == null)
                            imgFinalFileName = tex2D.texName + "_" + imgSize + tex2D.getFileFormat();
                        else
                            imgFinalFileName = imgFileName;
                        string fullpath = imgFileFolder + "\\" + imgFinalFileName;

                        // extraction function
                        tex2D.extractImage(imgSize, currentDir, fullpath);
                    }

                    // update the pcc with the new replaced infos
                    ListViewItem item = listView1.SelectedItems[0];
                    int index = Convert.ToInt32(item.Name);
                    PCCObject.ExportEntry expEntry = pcc.Exports[index];
                    expEntry.Data = tex2D.ToArray(expEntry.DataOffset);
                    //pcc.ChangeExportEntry(index, expEntry);
                    //pcc.UpdateAllOffsets();

                    MessageBox.Show("All images are extracted correctly.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception exc)
                {
                    MessageBox.Show("An error occurred while extracting images: " + exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void replaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string currentDir = ME3Directory.cookedPath;

            // setting TexSelection Form to export images
            TexSelection texSelection = new TexSelection(tex2D, true);
            texSelection.FormTitle = "Select images to replace";
            texSelection.btnSelectionText = "Replace";
            if (tex2D.imgList.Count > 1)
            {
                texSelection.ShowDialog();
                texSelection.Dispose();
            }
            else
                texSelection.bOk = true;

            if (texSelection.bOk)
            {
                try
                {
                    // main replace loop
                    foreach (Object entry in texSelection.imageListBox.CheckedItems)
                    {
                        string imgSize = entry.ToString().Split(' ')[1]; // take imagesize (ex. 512x512) from checklist text: "Image 512x512 stored..."

                        OpenFileDialog openImage = openFileDialog;
                        openImage.Title = "Select the image to replace with size " + imgSize;
                        openImage.Filter = "Image file|*" + tex2D.getFileFormat() + "|All files|*.*";

                        if (openImage.ShowDialog() != DialogResult.OK)
                            return;

                        // replace function
                        tex2D.replaceImage(imgSize, openImage.FileName, currentDir);
                    }

                    //--------- update the pcc with the new replaced infos ----------
                    // select export index
                    ListViewItem item = listView1.SelectedItems[0];
                    int index = Convert.ToInt32(item.Name);
                    // copy export data
                    PCCObject.ExportEntry expEntry = pcc.Exports[index];
                    // change data with new tex data
                    expEntry.Data = tex2D.ToArray(expEntry.DataOffset);
                    // updating pcc file
                    //pcc.ChangeExportEntry(index, expEntry);
                    //pcc.UpdateAllOffsets();

                    // changing tfc size
                    // check that the image list has at least one image stored inside an external archive, if not it's useless to search the archive location
                    if (tex2D.imgList.Any(images => images.storageType == Texture2D.storage.arcCpr || images.storageType == Texture2D.storage.arcUnc))
                    {
                        TOCeditor tc = new TOCeditor();
                        uint arcSize;
                        string arcPath = currentDir + tex2D.arcName + ".tfc";
                        using (FileStream archiveStream = File.OpenRead(arcPath))
                            arcSize = (uint)archiveStream.Length;
                        string tocpath = pathBIOGame + "PCConsoleTOC.bin";
                        if (File.Exists(tocpath))
                            while (!tc.UpdateFile("\\" + tex2D.arcName + ".tfc", arcSize, tocpath))
                            {
                                OpenFileDialog openImage = openFileDialog;
                                openImage.Title = "Select the archive path";
                                openImage.Filter = tex2D.arcName + ".tfc|" + tex2D.arcName + ".tfc|All files|*.*";
                                openImage.ShowDialog();
                                arcPath = openImage.FileName;
                                using (FileStream archiveStream = File.OpenRead(arcPath))
                                    arcSize = (uint)archiveStream.Length;
                            }
                        else
                            while (!tc.UpdateFile("\\" + tex2D.arcName + ".tfc", arcSize))
                            {
                                OpenFileDialog openImage = openFileDialog;
                                openImage.Title = "Select the archive path";
                                openImage.Filter = tex2D.arcName + ".tfc|" + tex2D.arcName + ".tfc|All files|*.*";
                                openImage.ShowDialog();
                                arcPath = openImage.FileName;
                                using (FileStream archiveStream = File.OpenRead(arcPath))
                                    arcSize = (uint)archiveStream.Length;
                            }
                    }

                    //---------------- end of replace -------------------------------
                    MessageBox.Show("All images are replaced correctly.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception exc)
                {
                    MessageBox.Show("An error occurred while replacing images: " + exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void upscaleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (tex2D.imgList.Count <= 1)
                {
                    MessageBox.Show("You cannot upscale a texture that has only one image!", "Invalid operation", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string currentDir = pathCooked;
                OpenFileDialog openImage = openFileDialog;
                openImage.Title = "Select the image to add";
                openImage.Filter = "Image file|*" + tex2D.getFileFormat() + "|All files|*.*";

                if (openImage.ShowDialog() != DialogResult.OK)
                    return;

                // add function
                tex2D.addBiggerImage(openImage.FileName, currentDir);

                //--------- update the pcc with the new replaced infos ----------
                // select export index
                ListViewItem item = listView1.SelectedItems[0];
                int index = Convert.ToInt32(item.Name);
                // copy export data
                PCCObject.ExportEntry expEntry = pcc.Exports[index];
                // change data with new tex data
                expEntry.Data = tex2D.ToArray(expEntry.DataOffset);
                // updating pcc file
                //pcc.ChangeExportEntry(index, expEntry);
                //pcc.UpdateAllOffsets();

                // changing tfc size
                // check that the image list has at least one image stored inside an external archive, if not it's useless to search the archive location
                if (tex2D.imgList.Any(images => images.storageType == Texture2D.storage.arcCpr || images.storageType == Texture2D.storage.arcUnc))
                {
                    TOCeditor tc = new TOCeditor();
                    uint arcSize;
                    string arcPath = currentDir + tex2D.arcName + ".tfc";
                    using (FileStream archiveStream = File.OpenRead(arcPath))
                        arcSize = (uint)archiveStream.Length;
                    string tocpath = pathBIOGame + "PCConsoleTOC.bin";
                    if (File.Exists(tocpath))
                        while (!tc.UpdateFile("\\" + tex2D.arcName + ".tfc", arcSize, tocpath))
                        {
                            openImage = openFileDialog;
                            openImage.Title = "Select the archive path";
                            openImage.Filter = tex2D.arcName + ".tfc|" + tex2D.arcName + ".tfc|All files|*.*";
                            openImage.ShowDialog();
                            arcPath = openImage.FileName;
                            using (FileStream archiveStream = File.OpenRead(arcPath))
                                arcSize = (uint)archiveStream.Length;
                        }
                    else
                        while (!tc.UpdateFile("\\" + tex2D.arcName + ".tfc", arcSize))
                        {
                            openImage = openFileDialog;
                            openImage.Title = "Select the archive path";
                            openImage.Filter = tex2D.arcName + ".tfc|" + tex2D.arcName + ".tfc|All files|*.*";
                            openImage.ShowDialog();
                            arcPath = openImage.FileName;
                            using (FileStream archiveStream = File.OpenRead(arcPath))
                                arcSize = (uint)archiveStream.Length;
                        }
                }
                //---------------- end of replace -------------------------------

                /*
                // code test to prove that tex2d works, just decomment it and comment precedent code
                byte[] pccFile = PCCHandler.Decompress(pcc.pccFileName);
                using (FileStream debugStr = File.OpenWrite(pcc.pccFileName))
                {
                    debugStr.Write(pccFile, 0, pccFile.Length);
                    debugStr.Seek(expEntry.DataOffset, SeekOrigin.Begin);
                    byte[] buffer = tex2D.ToArray(expEntry.DataOffset);
                    debugStr.Write(buffer, 0, buffer.Length);
                }*/
                MessageBox.Show("Image was added correctly.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception exc)
            {
                MessageBox.Show("An error occurred while adding image: " + exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void addBiggestImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //try
            {
                if (tex2D.imgList.Count <= 1)
                {
                    MessageBox.Show("You cannot upscale a texture that has only one image!", "Invalid operation", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string currentDir = pathCooked;
                string texGroupToAdd;
                OpenFileDialog openImage = openFileDialog;
                openImage.Title = "Select the image to add";
                openImage.Filter = "Image file|*" + tex2D.getFileFormat() + "|All files|*.*";

                if (openImage.ShowDialog() != DialogResult.OK)
                    return;

                // add function
                int numTexBegin = tex2D.imgList.Count;
                tex2D.OneImageToRuleThemAll(openImage.FileName, currentDir, out texGroupToAdd);
                int numTexEnd = tex2D.imgList.Count;

                //--------- update the pcc with the new replaced infos ----------
                // select export index
                ListViewItem item = listView1.SelectedItems[0];
                int index = Convert.ToInt32(item.Name);
                // copy export data
                PCCObject.ExportEntry expEntry = pcc.Exports[index];
                // change data with new tex data
                expEntry.Data = tex2D.ToArray(expEntry.DataOffset);
                // updating pcc file
                //pcc.ChangeExportEntry(index, expEntry);
                //pcc.UpdateAllOffsets();

                // changing tfc size
                // check that the image list has at least one image stored inside an external archive, if not it's useless to search the archive location
                if (tex2D.imgList.Any(images => images.storageType == Texture2D.storage.arcCpr || images.storageType == Texture2D.storage.arcUnc))
                {
                    TOCeditor tc = new TOCeditor();
                    uint arcSize;
                    string arcPath = currentDir + tex2D.arcName + ".tfc";
                    using (FileStream archiveStream = File.OpenRead(arcPath))
                        arcSize = (uint)archiveStream.Length;
                    string tocpath = pathBIOGame + "PCConsoleTOC.bin";
                    if (File.Exists(tocpath))
                        while (!tc.UpdateFile("\\" + tex2D.arcName + ".tfc", arcSize, tocpath))
                        {
                            openImage = openFileDialog;
                            openImage.Title = "Select the archive path";
                            openImage.Filter = tex2D.arcName + ".tfc|" + tex2D.arcName + ".tfc|All files|*.*";
                            openImage.ShowDialog();
                            arcPath = openImage.FileName;
                            using (FileStream archiveStream = File.OpenRead(arcPath))
                                arcSize = (uint)archiveStream.Length;
                        }
                    else
                        while (!tc.UpdateFile("\\" + tex2D.arcName + ".tfc", arcSize))
                        {
                            openImage = openFileDialog;
                            openImage.Title = "Select the archive path";
                            openImage.Filter = tex2D.arcName + ".tfc|" + tex2D.arcName + ".tfc|All files|*.*";
                            openImage.ShowDialog();
                            arcPath = openImage.FileName;
                            using (FileStream archiveStream = File.OpenRead(arcPath))
                                arcSize = (uint)archiveStream.Length;
                        }
                }

                //---------------- end of replace -------------------------------
                if(texGroupToAdd != null)
                    MessageBox.Show("Texture replaced correctly. Make sure to add " + texGroupToAdd + " inside coalesced.bin", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show("Texture replaced correctly.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            /*catch (Exception exc)
            {
                MessageBox.Show("An error occurred while replacing texture: " + exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }*/
        }

        private void inSequenceEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
        public void ExtractSound()
        {
            if (listView1.SelectedItems.Count != 1 || pcc == null)
                return;
            ListViewItem item = listView1.SelectedItems[0];
            int index = Convert.ToInt32(item.Name);
            if (pcc.Exports[index].ClassName == "WwiseStream")
            {
                WwiseStream w = new WwiseStream(pcc, pcc.Exports[index].Data);
                w.ExtractToFile(pathCooked,pcc.Exports[index].ObjectName);
            }
        }

        public void PlaySound()
        {
            if (listView1.SelectedItems.Count != 1 || pcc == null)
                return;
            ListViewItem item = listView1.SelectedItems[0];
            int index = Convert.ToInt32(item.Name);
            if (pcc.Exports[index].ClassName == "WwiseStream")
            {
                w = new WwiseStream(pcc, pcc.Exports[index].Data);
                w.Play(pathCooked);
            }
        }

        public void ImportSound()
        {
            if (listView1.SelectedItems.Count != 1 || pcc == null)
                return;
            ListViewItem item = listView1.SelectedItems[0];
            int index = Convert.ToInt32(item.Name);
            if (pcc.Exports[index].ClassName == "WwiseStream")
            {
                OpenFileDialog o = new OpenFileDialog();
                o.Filter = "Wwise wav (*.wav)|*.wav";
                if (o.ShowDialog() == DialogResult.OK)
                {
                    w = new WwiseStream(pcc, pcc.Exports[index].Data);
                    w.ImportFromFile(o.FileName, pathBIOGame, pathCooked);
                    byte[] buff = new byte[w.memsize];
                    for (int i = 0; i < w.memsize; i++)
                        buff[i] = w.memory[i];
                    PCCObject.ExportEntry ent = pcc.Exports[index];
                    ent.Data = buff;
                    //pcc.ChangeExportEntry(index, CopyExport(ent));
                    MessageBox.Show("Done.");
                }
            }
        }

        private void extractToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ExtractSound();
        }

        private void playToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PlaySound();
        }

        private void stopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (w != null)
                if (w.sp != null)
                    w.sp.Stop();
        }

        private void importToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ImportSound();
        }

        private void openExternalPccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openPcc = openFileDialog;
            openPcc.Title = "Select the image to add";
            openPcc.Filter = "Pcc file|*.pcc|All files|*.*";

            if (openPcc.ShowDialog() != DialogResult.OK)
                return;

            Println("Loading " + Path.GetFileName(openPcc.FileName) + " ...");
            LoadFile(openPcc.FileName);
        }

        private void previewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ImageSize imgSize;
            //string imgName = (tex2D.getFileFormat() == ".tga") ? "exec\\" + "preview00" : "exec\\" + "preview";
            string imgName = "exec\\preview" + tex2D.getFileFormat();

            if (File.Exists("exec\\preview.tga"))
                File.Delete("exec\\preview.tga");
            if (File.Exists("exec\\preview.dds"))
                File.Delete("exec\\preview.dds");

            if (tex2D.imgList.Count != 1)
                imgSize = tex2D.imgList.Where(img => (img.imgSize.width <= 512 || img.imgSize.height <= 512) && img.offset != -1).Max(image => image.imgSize);
            else
                imgSize = tex2D.imgList.First().imgSize;

            tex2D.extractImage(imgSize.ToString(), ME3Directory.cookedPath, imgName);

            if (File.Exists(Path.GetFullPath(imgName)))
            {
                if (pictureBox.Image != null)
                    pictureBox.Image.Dispose();
                pictureBox.SizeMode = PictureBoxSizeMode.AutoSize;
                if (tex2D.getFileFormat() == ".dds")
                {
                    DDSImage ddsImage = new DDSImage(imgName);
                    pictureBox.Image = ddsImage.ToPictureBox(pictureBox.Width, pictureBox.Height);
                }
                else // .tga
                {
                    TargaImage ti = new TargaImage(imgName);
                    pictureBox.Image = ti.Image;
                }
                pictureBox.Refresh();
                listView1.Visible = false;
                panelImage.Visible = true;

                if (File.Exists(imgName))
                    File.Delete(imgName);
            }
        }


        private void pictureBox_Click(object sender, EventArgs e)
        {
            panelImage.Visible = false;
            listView1.Visible = true;
        }

        private void panelImage_Click(object sender, EventArgs e)
        {
            panelImage.Visible = false;
            listView1.Visible = true;
        }

        public void ExecuteCommandSync(object command)
        {
            try
            {
                // create the ProcessStartInfo using "cmd" as the program to be run,
                // and "/c " as the parameters.
                // Incidentally, /c tells cmd that we want it to execute the command that follows,
                // and then exit.
                System.Diagnostics.ProcessStartInfo procStartInfo =
                    new System.Diagnostics.ProcessStartInfo("cmd", "/c " + command);

                // The following commands are needed to redirect the standard output.
                // This means that it will be redirected to the Process.StandardOutput StreamReader.
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.UseShellExecute = false;
                // Do not create the black window.
                procStartInfo.CreateNoWindow = true;
                // Now we create a process, assign its ProcessStartInfo and start it
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.StartInfo = procStartInfo;
                proc.Start();
                // Get the output into a string
                string result = proc.StandardOutput.ReadToEnd();
                // Display the command output.
                Console.WriteLine(result);
            }
            catch
            {
                // Log the exception
            }
        }

        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            ListView.SelectedIndexCollection n = listView1.SelectedIndices;
            if (n.Count != 1 || pcc == null)
                return;
            int index = Convert.ToInt32(listView1.Items[n[0]].Name);

            switch (pcc.Exports[index].ClassName)
            {
                case Texture2D.className: previewToolStripMenuItem_Click(sender, e); break;
            }
        }

        private void makeModToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ListView.SelectedIndexCollection n = listView1.SelectedIndices;
            if (n.Count != 1 || pcc == null)
                return;
            int index = Convert.ToInt32(listView1.Items[n[0]].Name);
            string name = pcc.Exports[index].ObjectName;
            string pccname = Path.GetFileName(currentPCC);
            OpenFileDialog d = new OpenFileDialog();
            d.Title = "Select the image to add";
            d.Filter = "Image file|*" + tex2D.getFileFormat() + "|All files|*.*";
            if (d.ShowDialog() == DialogResult.OK)
            {
                KFreonLib.Scripting.ModMaker.CreateTextureJob((ITexture2D)tex2D, d.FileName, 3, pathBIOGame);
                MessageBox.Show("Done.");
            }
        }

        private void panelImage_Paint(object sender, PaintEventArgs e)
        {

        }

        private void openInPCCEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0)
                return;
            ListViewItem item = listView1.SelectedItems[0];
            TreeNode t = TV1.SelectedNode;
            if (t == null)
                return;
            int l = Convert.ToInt32(item.Name);
            PCCEditor2 p = new PCCEditor2();
            p.MdiParent = this.MdiParent;
            p.WindowState = FormWindowState.Maximized;
            p.Show();
            p.pcc = new PCCObject(currentPCC);
            p.SetView(2);
            p.RefreshView();
            p.InitStuff();
            p.listBox1.SelectedIndex = l;
        }

        private void AssetExplorer_FormClosing(object sender, FormClosingEventArgs e)
        {
            taskbar.RemoveTool(this);
        }

        private void removeTopImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0)
                return;
            if (tex2D.imgList.Count == 1)
            {
                MessageBox.Show("Only 1 image present. You can't remove that", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            try
            {
                tex2D.removeImage();
                ListViewItem item = listView1.SelectedItems[0];
                int index = Convert.ToInt32(item.Name);
                // copy export data
                PCCObject.ExportEntry expEntry = pcc.Exports[index];
                // change data with new tex data
                expEntry.Data = tex2D.ToArray(expEntry.DataOffset);
                MessageBox.Show("Texture successfuly removed");
            }
            catch (Exception exc)
            {
                MessageBox.Show("An error occurred while removing texture: " + exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }
}
