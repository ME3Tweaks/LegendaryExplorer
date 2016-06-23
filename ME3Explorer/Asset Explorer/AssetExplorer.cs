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
using ME3Explorer.Packages;


namespace ME3Explorer
{
    public partial class AssetExplorer : Form
    {
        public string pathME3exe;
        public string pathCooked;
        public string pathBIOGame;
        public string currentPCC;
        ME3PCCObject pcc = null;
        AmaroK86.MassEffect3.DLCBase currentDLC = null;
        ME3SaltTexture2D tex2D;
        WwiseStream w;
        public Clipboard clip;
        string mainPCCFolder = "Default PCC Files";

        public class ClipboardDependency
        {
            public string Name;
            public Packages.ME3ImportEntry classimp;
            public ClipboardDependency child;
        }

        public struct Clipboard
        {
            public Packages.ME3ExportEntry entry;
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
                MessageBox.Show("This tool requires ME3 to be installed. Set its path at:\n Options > Set Custom Path > Mass Effect 3");
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
            try
            {
                if (!File.Exists(s))
                    return;
                pcc = new ME3PCCObject(s);
                currentPCC = s;
                GeneratePccTree();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message);
            }
        }

        public void GeneratePccTree()
        {
            TV1.BeginUpdate();
            TV1.Nodes.Clear();

            TreeNode root = TV1.Nodes.Add(pcc.pccFileName, pcc.pccFileName);
            root.ImageIndex = 1;

            foreach (KFreonLib.PCCObjects.ME3ExportEntry exportEntry in pcc.Exports)
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
            try
            {
                if (DLCName == mainPCCFolder)
                {
                    currentPCC = ME3Directory.cookedPath + t.Text;
                    pcc = new ME3PCCObject(currentPCC);
                }
                else
                {
                    return;
                }

                GeneratePccTree();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message);
            }
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

            if (pcc.Exports[index].ClassName == ME3SaltTexture2D.className)
                tex2D = new ME3SaltTexture2D(pcc, index, ME3Directory.BIOGamePath);
            else
                tex2D = null;

            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                switch (pcc.Exports[index].ClassName)
                {
                    case "Sequence":
                        textureToolStripMenuItem.Visible = false;
                        soundsToolStripMenuItem.Visible = false;
                        contextMenuStrip1.Show(MousePosition);
                        break;
                    case "Texture2D":
                        soundsToolStripMenuItem.Visible = false;
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
                        textureToolStripMenuItem.Visible = false;
                        soundsToolStripMenuItem.Visible = true;
                        contextMenuStrip1.Show(MousePosition);
                        break;
                    default:
                        soundsToolStripMenuItem.Visible = false;
                        textureToolStripMenuItem.Visible = false;
                        contextMenuStrip1.Show(MousePosition);
                        break;
                }
            }
        }

        public byte[] CopyArray(byte[] arr)
        {
            byte[] ret = new byte[arr.Length];
            for (int i = 0; i < arr.Length; i++)
                ret[i] = arr[i];
            return ret;
        }

        private void extractToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ImageSize imgSize;
            string format = tex2D.getFileFormat();
            string imgName = tex2D.texName + format;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = $"*{format}|*{format}";
            d.FileName = imgName;
            if (d.ShowDialog() == DialogResult.OK)
            {
                imgName = d.FileName;
                if (tex2D.imgList.Count != 1)
                    imgSize = tex2D.imgList.Where(img => (img.imgSize.width <= 512 || img.imgSize.height <= 512) && img.offset != -1).Max(image => image.imgSize);
                else
                    imgSize = tex2D.imgList.First().imgSize;

                tex2D.extractImage(imgSize.ToString(), false, ME3Directory.cookedPath, imgName);
                MessageBox.Show("Done");
            }
        }

        public void ExtractSound()
        {
            if (listView1.SelectedItems.Count != 1 || pcc == null)
                return;
            ListViewItem item = listView1.SelectedItems[0];
            int index = Convert.ToInt32(item.Name);
            if (pcc.Exports[index].ClassName == "WwiseStream")
            {
                WwiseStream w = new WwiseStream(new ME3Package(pcc.pccFileName), index);
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
                w = new WwiseStream(new ME3Package(pcc.pccFileName), index);
                w.Play(pathCooked);
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

            tex2D.extractImage(imgSize.ToString(), false, ME3Directory.cookedPath, imgName);

            if (File.Exists(Path.GetFullPath(imgName)))
            {
                if (pictureBox.Image != null)
                    pictureBox.Image.Dispose();
                pictureBox.SizeMode = PictureBoxSizeMode.AutoSize;
                DDSImage ddsImage = new DDSImage(imgName);
                pictureBox.Image = ddsImage.ToPictureBox(pictureBox.Width, pictureBox.Height);
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
                case ME3SaltTexture2D.className: previewToolStripMenuItem_Click(sender, e); break;
            }
        }

        private void makeModToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void panelImage_Paint(object sender, PaintEventArgs e)
        {

        }

        private void openInPackageEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0)
                return;
            ListViewItem item = listView1.SelectedItems[0];
            TreeNode t = TV1.SelectedNode;
            if (t == null)
                return;
            int l = Convert.ToInt32(item.Name);
            PackageEditor p = new PackageEditor();
            p.MdiParent = this.MdiParent;
            p.WindowState = FormWindowState.Maximized;
            p.Show();
            p.LoadFile(currentPCC);
            p.goToNumber(l);
        }

        private void removeTopImageToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

    }
}
