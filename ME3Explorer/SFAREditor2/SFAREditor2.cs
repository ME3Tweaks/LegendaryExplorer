using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Be;
using Be.Windows.Forms;
using ME3Explorer.Unreal;
using KFreonLib.Debugging;
using KFreonLib.MEDirectories;
using UsefulThings;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace ME3Explorer
{
    public partial class SFAREditor2 : Form
    {
        string previousTerm = "";
        DLCPackage DLC;

        public SFAREditor2()
        {
            InitializeComponent();
            DebugOutput.StartDebugger("SFAR Editor 2");
        }

        private void openSFARToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.sfar|*.sfar";
            if (d.ShowDialog() == DialogResult.OK)
            {
                openSFAR(d.FileName);
            }
        }

        private void openSFAR(string filename)
        {
            try
            {
                
                DLC = new DLCPackage(filename);
                treeView1.Nodes.Clear();
                treeView1.Nodes.Add(DLC.ToTree());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private DLCPackage openSFAR2(string filename)
        {
            try
            {
                
                DLC = new DLCPackage(filename);
                treeView1.Nodes.Clear();
                treeView1.Nodes.Add(DLC.ToTree());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            return DLC;
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (t == null || t.Parent == null || t.Parent.Text != "FileEntries")
                return;
            int n = t.Index;
        }

        private void searchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter string to search", "ME3 Explorer", previousTerm, 0, 0);
            previousTerm = result;
            selectSearchedElement(result);
        }

        private void selectSearchedElement(string query)
        {
            if (query == "")
                return;
            TreeNode SelectedNode = SearchNode(query, treeView1.Nodes[0]);
            if (SelectedNode != null)
            {
                this.treeView1.SelectedNode = SelectedNode;
                this.treeView1.SelectedNode.Expand();
                this.treeView1.Select();
            }
        }

        private TreeNode SearchNode(string SearchText, TreeNode StartNode)
        {
            TreeNode node = null;
            while (StartNode != null)
            {
                if (StartNode.Text.ToLower().Contains(SearchText.ToLower()))
                {
                    node = StartNode;
                    break;
                }
                if (StartNode.Nodes.Count != 0)
                {
                    node = SearchNode(SearchText, StartNode.Nodes[0]);//Recursive Search
                    if (node != null)
                    {
                        break;
                    }
                }
                StartNode = StartNode.NextNode;
            }
            return node;
        }

        private void addFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (DLC == null)
                return;
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.*|*.*";
            string p = Path.GetDirectoryName(DLC.Files[0].FileName) + "\\";
            if (d.ShowDialog() == DialogResult.OK)
            {
                string path = p + Path.GetFileName(d.FileName);
                path = path.Replace('\\', '/');
                string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter the path with name that the file will have inside the DLC", "ME3 Explorer", path, 0, 0);
                if (result == "")
                    return;
                path = result;
                System.Diagnostics.Debug.WriteLine("Adding file quick: " + d.FileName + " " + path);

                DLC.AddFileQuick(d.FileName, path);
                DLC = new DLCPackage(DLC.FileName);
                treeView1.Nodes.Clear();
                treeView1.Nodes.Add(DLC.ToTree());
                SearchNode(result, treeView1.Nodes[0]);
                MessageBox.Show("File added.");
            }
        }

        private void rebuildSFARToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (DLC == null)
                return;
            DebugOutput.StartDebugger("DLCEditor2");
            DLC.ReBuild();
            DLC = new DLCPackage(DLC.FileName);
            treeView1.Nodes.Clear();
            treeView1.Nodes.Add(DLC.ToTree());
            MessageBox.Show("SFAR Rebuilt.");
        }

        private void extractSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (DLC == null || t == null || t.Parent == null || t.Parent.Text != "FileEntries")
                return;
            int n = t.Index;
            SaveFileDialog d = new SaveFileDialog();
            string filename = DLC.Files[n].FileName;
            d.Filter = Path.GetFileName(filename) + " | " + Path.GetFileName(filename);
            d.FileName = Path.GetFileName(filename);
            if (d.ShowDialog() == DialogResult.OK)
            {
                extractFile(n, d.FileName);
                MessageBox.Show("File extracted.");
            }
        }

        private void extractFile(int n, string exportLocation)
        {
            MemoryStream m = DLC.DecompressEntry(n);
            FileStream fs = new FileStream(exportLocation, FileMode.Create, FileAccess.Write);
            fs.Write(m.ToArray(), 0, (int)m.Length);
            fs.Close();
        }

        private void replaceSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (DLC == null || t == null || t.Parent == null || t.Parent.Text != "FileEntries")
                return;
            int n = t.Index;

            OpenFileDialog d = new OpenFileDialog();
            string filename = DLC.Files[n].FileName;
            d.Filter = Path.GetFileName(filename) + " | " + Path.GetFileName(filename);
            d.FileName = Path.GetFileName(filename);
            if (d.ShowDialog() == DialogResult.OK)
            {
                replaceFile(d.FileName, n);
                MessageBox.Show("File Replaced.");
            }
        }

        private void replaceFile(string filename, int n)
        {
            DLC.ReplaceEntry(filename, n);

            DLC = new DLCPackage(DLC.FileName);
            treeView1.Nodes.Clear();
            treeView1.Nodes.Add(DLC.ToTree());
            SearchNode(filename, treeView1.Nodes[0]);
        }

        private void createReplaceModJobToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (DLC == null || t == null || t.Parent == null || t.Parent.Text != "FileEntries")
                return;
            int n = t.Index;
            OpenFileDialog d = new OpenFileDialog();
            string filename = DLC.Files[n].FileName;
            d.Filter = Path.GetFileName(filename) + " | " + Path.GetFileName(filename);
            d.FileName = Path.GetFileName(filename);
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                KFreonLib.Scripting.ModMaker.ModJob mj = new KFreonLib.Scripting.ModMaker.ModJob();
                string file = DLC.FileName;               //full path
                string t1 = Path.GetDirectoryName(file);    //cooked
                string t2 = Path.GetDirectoryName(t1);      //DLC_Name
                string t3 = Path.GetDirectoryName(t2);      //DLC
                t3 += "\\";
                file = file.Substring(t3.Length);
                file = file.Replace("\\", "\\\\");
                mj.data = File.ReadAllBytes(d.FileName);
                mj.Name = "DLC File Replacement in DLC \"" + file + "\" with File #" + n + " with " + mj.data.Length + " bytes of data";
                string loc = Path.GetDirectoryName(Application.ExecutablePath);
                string template = System.IO.File.ReadAllText(loc + "\\exec\\JobTemplate_DLCReplace.txt");
                template = template.Replace("**m1**", n.ToString());
                template = template.Replace("**m2**", file);
                mj.Script = template;
                KFreonLib.Scripting.ModMaker.JobList.Add(mj);
                MessageBox.Show("Done.");
            }
        }

        private void deleteSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (DLC == null || t == null || t.Parent == null || t.Parent.Text != "FileEntries")
                return;
            if (MessageBox.Show("Are you sure to delete this File?", "DLCEditor2", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                int n = t.Index;
                DLC.DeleteEntry(n);
                DLC = new DLCPackage(DLC.FileName);
                treeView1.Nodes.Clear();
                treeView1.Nodes.Add(DLC.ToTree());
                MessageBox.Show("File Deleted.");
            }
        }

        private void unpackSFARToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (DLC == null || DLC.Files == null)
                return;
            string result = "pcc; tfc; afc; cnd; tlk; bin; bik; dlc";
            string unpackFolder;
            result = Microsoft.VisualBasic.Interaction.InputBox("Please enter pattern for unpacking, keep default to unpack everything.", "ME3 Explorer", "pcc; tfc; afc; cnd; tlk; bin; bik; dlc", 0, 0);

            if (result == "")
                return;
            DebugOutput.PrintLn("result : " + result);
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            dialog.EnsurePathExists = true;
            dialog.Title = "Choose a folder to unpack to. Select ME3 directory to unpack to proper folder";
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                unpackFolder = dialog.FileName;
            else
                return;
            if (!unpackFolder.EndsWith("\\"))
                unpackFolder = unpackFolder + "\\";
            DebugOutput.PrintLn("Extracting DLC to : " + unpackFolder);

            result = result.Trim();
            if (result.EndsWith(";"))
                result = result.Substring(0, result.Length - 1);
            string[] patt = result.Split(';');
            string file = DLC.FileName;                   //full path
            string t1 = Path.GetDirectoryName(file);        //cooked
            string t2 = Path.GetDirectoryName(t1);          //DLC_Name
            string t3 = Path.GetDirectoryName(t2);          //DLC
            string t4 = Path.GetDirectoryName(t3);          //BioGame
            DebugOutput.PrintLn("DLC name : " + t2);
            if (DLC.Files.Length > 1)
            {
                List<int> Indexes = new List<int>();
                for (int i = 0; i < DLC.Files.Length; i++)
                {
                    string DLCpath = DLC.Files[i].FileName;
                    for (int j = 0; j < patt.Length; j++)
                        if (DLCpath.ToLower().EndsWith(patt[j].Trim().ToLower()) && patt[j].Trim().ToLower() != "")
                        {
                            string relPath = GetRelativePath(DLCpath);
                            string outpath = unpackFolder + relPath;
                            DebugOutput.PrintLn("Extracting file #" + i.ToString("d4") + ": " + outpath);
                            if (!Directory.Exists(Path.GetDirectoryName(outpath)))
                                Directory.CreateDirectory(Path.GetDirectoryName(outpath));
                            using (FileStream fs = new FileStream(outpath, FileMode.Create))
                                DLC.DecompressEntry(i).WriteTo(fs);
                            Indexes.Add(i);
                            Application.DoEvents();
                            break;
                        }
                }
                DLC.DeleteEntries(Indexes);
            }

            // AutoTOC
            AutoTOC.prepareToCreateTOC(t2 + "\\PCConsoleTOC.bin");
            MessageBox.Show("SFAR Unpacked.");
        }

        public static void unpackSFAR(DLCPackage dlc)
        {
            if (dlc == null || dlc.Files == null)
                return;
            string[] patt = { "pcc", "bik", "tfc", "afc", "cnd", "tlk", "bin", "dlc" };
            string file = dlc.FileName;                   //full path
            string t1 = Path.GetDirectoryName(file);        //cooked
            string t2 = Path.GetDirectoryName(t1);          //DLC_Name
            string t3 = Path.GetDirectoryName(t2);          //DLC
            string t4 = Path.GetDirectoryName(t3);          //BioGame
            string gamebase = Path.GetDirectoryName(t4);    //Mass Effect3
            DebugOutput.PrintLn("Extracting DLC with gamebase : " + gamebase);
            DebugOutput.PrintLn("DLC name : " + t2);
            if (dlc.Files.Length > 1)
            {
                List<int> Indexes = new List<int>();
                for (int i = 0; i < dlc.Files.Length; i++)
                {
                    string DLCpath = dlc.Files[i].FileName;
                    for (int j = 0; j < patt.Length; j++)
                        if (DLCpath.ToLower().EndsWith(patt[j].Trim().ToLower()) && patt[j].Trim().ToLower() != "")
                        {
                            string relPath = GetRelativePath(DLCpath);
                            string outpath = gamebase + relPath;
                            DebugOutput.PrintLn("Extracting file #" + i.ToString("d4") + ": " + outpath);
                            if (!Directory.Exists(Path.GetDirectoryName(outpath)))
                                Directory.CreateDirectory(Path.GetDirectoryName(outpath));

                            if (!File.Exists(outpath))
                                using (FileStream fs = new FileStream(outpath, FileMode.Create))
                                    dlc.DecompressEntryAsync(i, fs).Wait();
                            Indexes.Add(i);
                            Application.DoEvents();
                            break;
                        }
                }
                dlc.DeleteEntries(Indexes);
            }

            // AutoTOC
            AutoTOC.prepareToCreateTOC(t2 + "\\PCConsoleTOC.bin");
            DebugOutput.PrintLn("DLC Done.");
        }

        private void unpackAllDLCsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExtractAllDLC();
        }

        public bool ExtractAllDLC()
        {
            bool retval = true;
            string DLCBasePath = ME3Directory.DLCPath;
            DebugOutput.PrintLn("DLC Path: " + DLCBasePath);
            List<string> files = new List<string>(Directory.EnumerateFiles(DLCBasePath, "Default.sfar", SearchOption.AllDirectories));
            foreach (string file in files)
            {
                string[] parts = file.Split('\\');
                if (parts[parts.Length-2].ToLower() != "cookedpcconsole")
                {
                    DebugOutput.PrintLn(file + "  doesn't look correct. SFAR in the wrong place?");
                    retval = false;
                    continue;
                }


                if (file != "")
                {
                    unpackSFAR(openSFAR2(file));

                }
            }
            DebugOutput.PrintLn("All DLCs Done.");
            return retval;
        }

        static string GetRelativePath(string DLCpath)
        {
            return DLCpath.Replace("/", "\\");
        }

        private void updateTOCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DLC.UpdateTOCbin();
            DebugOutput.PrintLn("Done.");
            MessageBox.Show("Done.");
        }

        private void updateTOCAndRebuildToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DLC.UpdateTOCbin(true);
            DebugOutput.PrintLn("Done.");
            MessageBox.Show("Done.");
        }
    }
}
