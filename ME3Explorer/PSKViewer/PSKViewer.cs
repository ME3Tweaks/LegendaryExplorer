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

namespace ME3Explorer.PSKViewer
{
    public partial class PSKViewer : Form
    {
        public PSKFile psk;
        public string CurrFile;

        public PSKViewer()
        {
            InitializeComponent();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.psk|*.psk;*.pskx";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = d.FileName;
                psk = new PSKFile();
                psk.ImportPSK(path);
                CurrFile = path;
                RefreshTree();
            }
        }

        public void RefreshTree()
        {
            treeView1.Nodes.Clear();
            TreeNode t = new TreeNode(Path.GetFileNameWithoutExtension(CurrFile));
            t.Nodes.Add("ACTRHEAD");
            t = ReadPoints(t);
            t = ReadEdges(t);
            t = ReadFaces(t);
            t = ReadMaterials(t);
            t = ReadBones(t);
            t = ReadWeights(t);
            treeView1.Nodes.Add(t);
            treeView1.Nodes[0].Expand();                        
        }

        public TreeNode ReadWeights(TreeNode Tin)
        {
            TreeNode t = new TreeNode("RAWWEIGHTS");
            t.Nodes.Add("Size : 12");
            t.Nodes.Add("Count : " + psk.psk.weights.Count());
            TreeNode t2 = new TreeNode("Data");
            int count = 0;
            foreach (PSKFile.PSKWeight w in psk.psk.weights)
            {

                string s = count.ToString("d4") + " (";
                s += w.weight + " ; ";
                s += w.point + " ; ";
                s += w.bone  + " )";
                t2.Nodes.Add(s);
                count++;
            }
            t.Nodes.Add(t2);
            Tin.Nodes.Add(t);
            return Tin;
        }

        public TreeNode ReadBones(TreeNode Tin)
        {
            TreeNode t = new TreeNode("REFSKEL");
            t.Nodes.Add("Size : 120");
            t.Nodes.Add("Count : " + psk.psk.bones.Count());
            TreeNode t2 = new TreeNode("Data");
            int count = 0;
            foreach (PSKFile.PSKBone b in psk.psk.bones)
            {

                string s = count.ToString("d4") + " \"" + b.name + "\" Position (";
                s += b.location.x + " ; ";
                s += b.location.y + " ; ";
                s += b.location.z + " ) Orientation (";
                s += b.rotation.x + " ; ";
                s += b.rotation.y + " ; ";
                s += b.rotation.z + " ; ";
                s += b.rotation.w + ") Parent (";
                s += b.parent + ") Childs (" + b.childs + ")";
                t2.Nodes.Add(s);
                count++;
            }
            t.Nodes.Add(t2);
            Tin.Nodes.Add(t);
            return Tin;
        }
        
        public TreeNode ReadPoints(TreeNode Tin)
        {
            TreeNode t = new TreeNode("PNTS0000");
            t.Nodes.Add("Size : 12");
            t.Nodes.Add("Count : " + psk.psk.points.Count());
            TreeNode t2 = new TreeNode("Data");
            int count=0;
            foreach (PSKFile.PSKPoint p in psk.psk.points)
            {

                string s = count.ToString("d4") + " (";
                s += p.x + " ; ";
                s += p.y + " ; ";
                s += p.z + " )";
                t2.Nodes.Add(s);
                count ++;
            }
            t.Nodes.Add(t2);
            Tin.Nodes.Add(t);
            return Tin;
        }

        public TreeNode ReadEdges(TreeNode Tin)
        {
            TreeNode t = new TreeNode("VTXW0000");
            t.Nodes.Add("Size : 16");
            t.Nodes.Add("Count : " + psk.psk.edges.Count());
            TreeNode t2 = new TreeNode("Data");
            int count = 0;
            foreach (PSKFile.PSKEdge e in psk.psk.edges)
            {

                string s = count.ToString("d4") + " (Index: ";
                s += e.index + " UV(";
                s += e.U + " ; " + e.V + ") Material: ";
                s += e.material + " SmoothGroup: " + e.padding2;
                s += ")";
                t2.Nodes.Add(s);
                count++;
            }
            t.Nodes.Add(t2);
            Tin.Nodes.Add(t);
            return Tin;
        }

        public TreeNode ReadFaces(TreeNode Tin)
        {
            TreeNode t = new TreeNode("FACE0000");
            t.Nodes.Add("Size : 12");
            t.Nodes.Add("Count : " + psk.psk.faces.Count());
            TreeNode t2 = new TreeNode("Data");
            int count = 0;
            foreach (PSKFile.PSKFace f in psk.psk.faces)
            {

                string s = count.ToString("d4") + " (v0: ";
                s += f.v0 + " ; v1: ";
                s += f.v1 + " ; v2: ";
                s += f.v2 + " ; Material: ";
                s += f.material + " )";
                t2.Nodes.Add(s);
                count++;
            }
            t.Nodes.Add(t2);
            Tin.Nodes.Add(t);
            return Tin;
        }

        public TreeNode ReadMaterials(TreeNode Tin)
        {
            TreeNode t = new TreeNode("MATT0000");
            t.Nodes.Add("Size : 88");
            t.Nodes.Add("Count : " + psk.psk.materials.Count());
            TreeNode t2 = new TreeNode("Data");
            int count = 0;
            foreach (PSKFile.PSKMaterial m in psk.psk.materials)
            {

                string s = count.ToString("d4") + " (Name: ";
                s += m.name + " Texture: ";
                s += m.texture + " PolyFlags: ";
                s += m.polyflags + " AuxMat: ";
                s += m.auxmaterial + " AuxFlags: ";
                s += m.auxflags + " LOD Bias: ";
                s += m.LODbias  + " LOD Style: ";
                s += m.LODstyle;
                s += ")";
                t2.Nodes.Add(s);
                count++;
            }
            t.Nodes.Add(t2);
            Tin.Nodes.Add(t);
            return Tin;
        }

        private void checkForDuplicatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (psk != null)
            {
                List<int> HasDup = new List<int>();
                int count = 0;
                for (int i = 0; i < psk.psk.points.Count; i++)
                {
                    int dup = -1;
                    for (int j = 0; j < psk.psk.points.Count; j++)
                        if (j != i && psk.psk.points[i] == psk.psk.points[j])
                            dup = j;
                    if (dup!=-1) count++;
                    HasDup.Add(dup);
                }
                string s = "Found " + count + " duplicate points\n";
                count = 0;
                for (int i = 0; i < psk.psk.points.Count; i++)
                    if (HasDup[i] != -1 && count ++ < 30)
                        s += "#" + i + " = #" + HasDup[i]+ "\n" ;
                MessageBox.Show(s);
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (psk == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.psk|*.psk;*.pskx";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = d.FileName;                
                psk.Export(path);
                MessageBox.Show("Done.");
            }
        }

        private void trimBoneNamesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (psk == null)
                return;
            int count = 0;
            for (int i = 0; i < psk.psk.bones.Count;i++ )
            {
                PSKFile.PSKBone b = psk.psk.bones[i];
                int before = b.name.Length;
                b.name = b.name.Trim();
                if (b.name.Length != before)
                {
                    psk.psk.bones[i] = b;
                    count++;
                }
            }
            RefreshTree();
            MessageBox.Show("Done. Trimmed " + count + " names.");
        }
    }
}
