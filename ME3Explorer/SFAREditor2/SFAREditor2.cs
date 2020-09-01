using System;
using System.IO;
using System.Windows.Forms;
using ME3Explorer.Debugging;
using ME3Explorer.SharedUI;
using ME3ExplorerCore.Unreal;

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

                //var outf = @"X:\quickbms";
                //foreach (var f in DLC.Files.Where(x => x.isActualFile))
                //{
                //    var outpath = Path.Combine(outf, f.FileName.Replace("/", "\\").TrimStart('\\'));
                //    Directory.CreateDirectory(Path.GetDirectoryName(outpath));
                //    var decomp = DLC.DecompressEntry(f);
                //    decomp.WriteToFile(outpath);
                //}
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
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
            if (treeView1.Nodes.Count > 0)
            {
                string result = PromptDialog.Prompt(null, "Please enter string to search", "ME3 Explorer", previousTerm, true);
                previousTerm = result;
                selectSearchedElement(result);
            }
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

        private void unpackAllDLCsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new DLCUnpacker.DLCUnpacker().Show();
        }

        private void updateTOCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (DLC != null)
            {
                DLC.UpdateTOCbin();
                DebugOutput.PrintLn("Done.");
                MessageBox.Show("Done.");
            }
        }
    }
}
