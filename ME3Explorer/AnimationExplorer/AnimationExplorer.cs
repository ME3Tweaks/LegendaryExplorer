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
using ME3Explorer.Packages;
using StreamHelpers;

namespace ME3Explorer.AnimationExplorer
{
    public partial class AnimationExplorer : WinFormsBase
    {
        public List<AnimTree> AT;
        public List<AnimSet> AS;
        public List<string> filenames = new List<string>();

        public AnimationExplorer()
        {
            InitializeComponent();
        }

        private void openPccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog {Filter = "*.pcc|*.pcc"};
            if (d.ShowDialog() == DialogResult.OK)
                LoadPcc(d.FileName);
        }

        public void LoadPcc(string s)
        {
            //try
            //{
            LoadME3Package(s);
            reScan();
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show("Error:\n" + ex.Message);
            //}
        }

        private void reScan()
        {
            AT = new List<AnimTree>();
            AS = new List<AnimSet>();
            foreach (ExportEntry exportEntry in Pcc.Exports)
            {
                switch (exportEntry.ClassName)
                {
                    case "AnimTree":
                        AT.Add(new AnimTree(exportEntry));
                        break;
                    case "AnimSet":
                        AS.Add(new AnimSet(exportEntry));
                        break;
                }
            }
            treeView1.Nodes.Clear();
            foreach (AnimTree at in AT)
                treeView1.Nodes.Add(at.ToTree());
            foreach (AnimSet ans in AS)
                treeView1.Nodes.Add(ans.ToTree());
        }

        private void startScanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = ME3Directory.cookedPath;
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("This functionality requires ME3 to be installed. Set its path at:\n Options > Set Custom Path > Mass Effect 3");
                return;
            }
            string[] files = Directory.GetFiles(path, "*.pcc");
            filenames = new List<string>();
            foreach (string file in files)
            {
                try
                {
                    using (IMEPackage _pcc = MEPackageHandler.OpenME3Package(file))
                    {
                        bool found = _pcc.Exports.Any(ex => ex.ClassName == "AnimTree" || ex.ClassName == "AnimSet");

                        if (found)
                            filenames.Add(file); 
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: Could not open {Path.GetFileName(file)}\n{ex.Message}");
                }
            }
            RefreshLists();
        }

        public void RefreshLists()
        {
            listBox1.Items.Clear();
            foreach (string s in filenames)
                listBox1.Items.Add(Path.GetFileName(s));
        }

        private void listBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            LoadPcc(filenames[n]);
        }

        private void saveDBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog {Filter = "*.dbs|*.dbs"};
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                
                fs.WriteInt32(filenames.Count);
                foreach (string s in filenames)
                {
                    fs.WriteInt32(s.Length);
                    fs.WriteStringASCII(s);
                }
                fs.Close();
            }
        }

        private void loadDBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog {Filter = "*.dbs|*.dbs"};
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                filenames = new List<string>();
                FileStream fs = new FileStream(d.FileName, FileMode.Open, FileAccess.Read);
                
                int count = fs.ReadInt32();
                for (int i = 0; i < count; i++)
                    filenames.Add(fs.ReadStringASCII(fs.ReadInt32()));
                fs.Close();
                RefreshLists();
            }
        }
        
        private void exportToPSAToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode n = treeView1.SelectedNode;
            if (n == null)
                return;
            int idx = GetRootIndex(n) - AT.Count;
            if (idx >= 0 && idx < AS.Count)
            {
                AnimSet ans = AS[idx];
                SaveFileDialog d = new SaveFileDialog {Filter = "*.psa|*.psa"};
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    ans.ExportToPSA(d.FileName);
                    MessageBox.Show("Done.");
                }
            }
        }

        public int GetRootIndex(TreeNode t)
        {
            while (true)
            {
                if (t.Parent == null) return t.Index;
                t = t.Parent;
            }
        }

        private void importFromPSAToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode n = treeView1.SelectedNode;
            if (n == null)
                return;
            int idx = GetRootIndex(n) - AT.Count;
            if (idx >= 0 && idx < AS.Count)
            {
                AnimSet ans = AS[idx];
                OpenFileDialog d = new OpenFileDialog {Filter = "*.psa|*.psa"};
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    if(ans.ImportFromPSA(d.FileName))
                        MessageBox.Show("Done.");
                }
            }
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            IEnumerable<PackageUpdate> relevantUpdates = updates.Where(x => x.change != PackageChange.Import &&
                                                                            x.change != PackageChange.ImportAdd &&
                                                                            x.change != PackageChange.Names);
            if (relevantUpdates.Any())
            {
                reScan();
            }
        }
    }
}
