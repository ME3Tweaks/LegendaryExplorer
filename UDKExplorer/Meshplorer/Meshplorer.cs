using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using UDKExplorer.UDK;
using UDKExplorer.UDK.Classes;
using Be;
using Be.Windows;
using Be.Windows.Forms;

namespace UDKExplorer.Meshplorer
{
    public partial class Meshplorer : Form
    {
        public Meshplorer()
        {
            InitializeComponent();
        }

        public UDKObject udk;
        public SkeletalMesh SKM;
        public StaticMesh STM;
        public List<int> objects;

        private void openPackageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.u;*.upk;*.udk|*.u;*.upk;*.udk";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                udk = new UDKObject(d.FileName);
                RefreshList();
            }
        }

        public void RefreshList()
        {
            listBox1.Items.Clear();
            objects = new List<int>();
            for (int i = 0; i < udk.ExportCount; i++)
            {
                if (udk.GetClass(udk.Exports[i].clas) == "SkeletalMesh")
                {
                    objects.Add(i);
                    listBox1.Items.Add("SkeletalMesh #" + i.ToString("d4") + " : " + udk.GetName(udk.Exports[i].name));
                }
                if (udk.GetClass(udk.Exports[i].clas) == "StaticMesh")
                {
                    objects.Add(i);
                    listBox1.Items.Add("StaticMesh #" + i.ToString("d4") + " : " + udk.GetName(udk.Exports[i].name));
                }

            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            Preview();
        }

        public void Preview()
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            int idx = objects[n];
            hb1.ByteProvider = new DynamicByteProvider(udk.Exports[idx].data);
            treeView1.Nodes.Clear();
            if (udk.GetClass(udk.Exports[idx].clas) == "SkeletalMesh")
            {
                STM = null;
                SKM = new SkeletalMesh(udk, idx);
                treeView1.Nodes.Add(SKM.ToTree());
            }
            if (udk.GetClass(udk.Exports[idx].clas) == "StaticMesh")
            {
                SKM = null;
                STM = new StaticMesh(udk, idx);
                treeView1.Nodes.Add(STM.ToTree());
            }
        }
        

        private void hb1_SelectionLengthChanged(object sender, EventArgs e)
        {
            toolStripStatusLabel1.Text = "Selection : 0x" + hb1.SelectionStart.ToString("X") + " - 0x" + (hb1.SelectionStart + hb1.SelectionLength).ToString("X") + " Length : " + hb1.SelectionLength.ToString("X");
            if (hb1.SelectionStart < hb1.ByteProvider.Length - 4)
            {
                byte[] buff = new byte[4];
                for (int i = 0; i < 4; i++)
                    buff[i] = hb1.ByteProvider.ReadByte(hb1.SelectionStart + i);
                BitConverter.IsLittleEndian = true;
                toolStripStatusLabel1.Text += " Values at Selectionstart : " + BitConverter.ToInt32(buff, 0) + "(INT) " + BitConverter.ToSingle(buff, 0) + "f(FLOAT)";
            }
        }
    }
}
