using System;
using System.IO;
using System.Windows.Forms;
using ME3Explorer.ME3ExpMemoryAnalyzer;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Unreal;

namespace ME3Explorer
{
    public partial class PSAViewer : Form
    {
        public PSA psa;
        public string CurrFile;

        public PSAViewer()
        {
            MemoryAnalyzer.AddTrackedMemoryItem(new MemoryAnalyzerObjectExtended("PSA Viewer", new WeakReference(this)));
            InitializeComponent();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog
            {
                Filter = AnimationImporter.PSAFilter,
                Multiselect = false,
                CheckFileExists = true
            };
            if (d.ShowDialog() == DialogResult.OK)
            {
                string path = d.FileName;
                psa = PSA.FromFile(path);
                CurrFile = path;
                RefreshTree();
            }
        }

        public void RefreshTree()
        {
            treeView1.Nodes.Clear();
            TreeNode t = new TreeNode(Path.GetFileNameWithoutExtension(CurrFile));
            t.Nodes.Add("ANIMHEAD");
            t = ReadBones(t);
            t = ReadAnimInfo(t);
            t = ReadAnimKeys(t);
            treeView1.Nodes.Add(t);
            treeView1.Nodes[0].Expand();                        
        }
        
        public TreeNode ReadBones(TreeNode Tin)
        {
            TreeNode t = new TreeNode("BONENAMES");
            t.Nodes.Add("Size : 120");
            t.Nodes.Add("Count : " + psa.Bones.Count);
            TreeNode t2 = new TreeNode("Data");
            int count = 0;
            foreach (var b in psa.Bones)
            {

                string s = count.ToString("d4") + " \"" + b.Name + "\" Position (";
                s += b.Position.X + " ; ";
                s += b.Position.Y + " ; ";
                s += b.Position.Z + " ) Orientation (";
                s += b.Rotation.X + " ; ";
                s += b.Rotation.Y + " ; ";
                s += b.Rotation.Z + " ; ";
                s += b.Rotation.W + ") Parent (";
                s += b.ParentIndex + ") Childs (" + b.NumChildren + ")";
                t2.Nodes.Add(s);
                count++;
            }
            t.Nodes.Add(t2);
            Tin.Nodes.Add(t);
            return Tin;
        }

        public TreeNode ReadAnimInfo(TreeNode Tin)
        {
            TreeNode t = new TreeNode("ANIMINFO");
            t.Nodes.Add("Size : 168");
            t.Nodes.Add("Count : " + psa.Infos.Count);
            TreeNode t2 = new TreeNode("Data");
            int count = 0;
            foreach (var info in psa.Infos)
            {
                TreeNode t3 = new TreeNode(count.ToString("d4") 
                    + " :  \"" 
                    + info.Name 
                    + "\" (Group : \"" 
                    + info.Group 
                    + "\")"
                    );
                t3.Nodes.Add("Total Bones : " + info.TotalBones);
                t3.Nodes.Add("Root Include : " + info.RootInclude);
                t3.Nodes.Add("Key Compression Style : " + info.KeyCompressionStyle);
                t3.Nodes.Add("Key Quotum : " + info.KeyQuotum);
                t3.Nodes.Add("Key Reduction : " + info.KeyReduction);
                t3.Nodes.Add("Track Time : " + info.TrackTime);
                t3.Nodes.Add("Anim Rate : " + info.AnimRate);
                t3.Nodes.Add("Start Bone : " + info.StartBone);
                t3.Nodes.Add("First Raw Frame : " + info.FirstRawFrame);
                t3.Nodes.Add("Num Raw Frames : " + info.NumRawFrames);
                t2.Nodes.Add(t3);
                count++;
            }
            t.Nodes.Add(t2);
            Tin.Nodes.Add(t);
            return Tin;
        }

        public TreeNode ReadAnimKeys(TreeNode Tin)
        {
            TreeNode t = new TreeNode("ANIMKEYS");
            t.Nodes.Add("Size : 32");
            t.Nodes.Add("Count : " + psa.Keys.Count);
            TreeNode t2 = new TreeNode("Data");
            int count = 0;
            foreach (var key in psa.Keys)
            {

                string s = count.ToString("d4") + " : ";
                //for (int i = 0; i < key.raw.Length; i++)
                //    s += key.raw[i].ToString("X2") + " ";
                s += "Position : (" + key.Position.X + " ; " + key.Position.Y + " ; " + key.Position.Z + " ) ";
                s += "Rotation : (" + key.Rotation.X + " ; " + key.Rotation.Y + " ; " + key.Rotation.Z + " ; " + key.Rotation.W + " ) ";
                s += "Time : " + key.Time;
                t2.Nodes.Add(s);
                count++;
            }
            t.Nodes.Add(t2);
            Tin.Nodes.Add(t);
            return Tin;
        }

    }
}
