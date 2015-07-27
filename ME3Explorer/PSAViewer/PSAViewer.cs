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

namespace ME3Explorer
{
    public partial class PSAViewer : Form
    {
        public PSAFile psa;
        public string CurrFile;

        public PSAViewer()
        {
            InitializeComponent();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.psa|*.psa";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = d.FileName;
                psa = new PSAFile();
                psa.ImportPSA(path);
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
            t.Nodes.Add("Count : " + psa.data.Bones.Count());
            TreeNode t2 = new TreeNode("Data");
            int count = 0;
            foreach (PSAFile.PSABone b in psa.data.Bones)
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

        public TreeNode ReadAnimInfo(TreeNode Tin)
        {
            TreeNode t = new TreeNode("ANIMINFO");
            t.Nodes.Add("Size : 168");
            t.Nodes.Add("Count : " + psa.data.Infos.Count());
            TreeNode t2 = new TreeNode("Data");
            int count = 0;
            foreach (PSAFile.PSAAnimInfo info in psa.data.Infos)
            {
                TreeNode t3 = new TreeNode(count.ToString("d4") 
                    + " :  \"" 
                    + info.name 
                    + "\" (Group : \"" 
                    + info.group 
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
            t.Nodes.Add("Count : " + psa.data.Keys.Count());
            TreeNode t2 = new TreeNode("Data");
            int count = 0;
            foreach (PSAFile.PSAAnimKeys key in psa.data.Keys)
            {

                string s = count.ToString("d4") + " : ";
                //for (int i = 0; i < key.raw.Length; i++)
                //    s += key.raw[i].ToString("X2") + " ";
                s += "Location : (" + key.location.x + " ; " + key.location.y + " ; " + key.location.z + " ) ";
                s += "Rotation : (" + key.rotation.x + " ; " + key.rotation.y + " ; " + key.rotation.z + " ; " + key.rotation.w + " ) ";
                s += "Time : " + key.time;
                t2.Nodes.Add(s);
                count++;
            }
            t.Nodes.Add(t2);
            Tin.Nodes.Add(t);
            return Tin;
        }

    }
}
