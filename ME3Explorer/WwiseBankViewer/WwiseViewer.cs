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
using Be.Windows.Forms;

namespace ME3Explorer.WwiseBankViewer
{
    public partial class WwiseViewer : Form
    {
        public PCCObject pcc;
        public List<int> objects;
        public WwiseBank bank;

        public WwiseViewer()
        {
            InitializeComponent();
        }

        private void openPccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                objects = new List<int>();
                pcc = new PCCObject(d.FileName);
                for (int i = 0; i < pcc.Exports.Count; i++)
                    if (pcc.Exports[i].ClassName == "WwiseBank")
                        objects.Add(i);
                ListRefresh();
            }
        }

        public void ListRefresh()
        {
            listBox1.Items.Clear();
            listBox2.Items.Clear();
            for (int i = 0; i < objects.Count; i++)
                listBox1.Items.Add(objects[i] + " : " + pcc.Exports[objects[i]].ObjectName);            
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshSelected();
        }

        public void RefreshSelected()
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            int index = objects[n];
            bank = new WwiseBank(pcc, index);
            hb1.ByteProvider = new DynamicByteProvider(bank.getBinary());
            rtb1.Text = bank.GetQuickScan();
            ListRefresh2();
        }

        public void ListRefresh2()
        {
            listBox2.Items.Clear();
            for (int i = 0; i < bank.HIRCObjects.Count; i++)
                listBox2.Items.Add(i.ToString("D4") + " : " + bank.GetHircDesc(bank.HIRCObjects[i]));
        }

        private void exportAllWEMFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (bank == null || bank.didx_data == null || bank.didx_data.Length == 0)
                return;
             System.Windows.Forms.FolderBrowserDialog m = new System.Windows.Forms.FolderBrowserDialog();
            m.ShowDialog();
            if (m.SelectedPath != "")
            {
                string dir = m.SelectedPath + "\\";
                if (bank.ExportAllWEMFiles(dir))
                    MessageBox.Show("Done.");
                else
                    MessageBox.Show("Error occured!");
            }
        }

        private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox2.SelectedIndex;
            if (n == -1)
                return;
            hb2.ByteProvider = new DynamicByteProvider(bank.HIRCObjects[n]);
        }

        private void recreateBankToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (bank == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.bin|*.bin";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                File.WriteAllBytes(d.FileName, bank.RecreateBinary());
                MessageBox.Show("Done.");
            }
        }

        private void cloneObjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            int m = listBox2.SelectedIndex;
            if (m == -1)
                return;
            bank.CloneHIRCObject(m);
            ListRefresh2();
            listBox2.SelectedIndex = listBox2.Items.Count - 1;
        }

        private void saveHexEditsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            int m = listBox2.SelectedIndex;
            if (m == -1)
                return;
            byte[] tmp = new byte[hb2.ByteProvider.Length];
            for (int i = 0; i < hb2.ByteProvider.Length; i++)
                tmp[i] = hb2.ByteProvider.ReadByte(i);
            bank.HIRCObjects[m] = tmp;
            ListRefresh2();
            listBox2.SelectedIndex = m;
        }

        private void editToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            int m = listBox2.SelectedIndex;
            if (m == -1)
                return;
            byte[] buff = bank.HIRCObjects[m];
            if (buff[0] != 0x02)
            {
                MessageBox.Show("Not a Sound SFX/Voice object");
                return;
            }
            else
            {
                BitConverter.IsLittleEndian = true;
                int ID1 = BitConverter.ToInt32(buff, 5);
                int opt = BitConverter.ToInt32(buff, 13);
                int ID2 = BitConverter.ToInt32(buff, 17);
                int ID3 = BitConverter.ToInt32(buff, 21);
                int tp = buff[25];
                string s = ID1 + ", " + opt + ", " + ID2 + ", " + ID3 + ", " + tp;
                string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new values[Object ID, Stream Option, ID audio, ID source, Sound Type", "ME3Explorer", s, 0, 0);
                string[] sres = result.Split(',');
                if (sres.Length != 5)
                    return;
                if (!int.TryParse(sres[0].Trim(), out ID1))
                    return;
                if (!int.TryParse(sres[1].Trim(), out opt))
                    return;
                if (!int.TryParse(sres[2].Trim(), out ID2))
                    return;
                if (!int.TryParse(sres[3].Trim(), out ID3))
                    return;
                if (!int.TryParse(sres[4].Trim(), out tp))
                    return;
                MemoryStream res = new MemoryStream(buff);
                res.Seek(5, 0);
                res.Write(BitConverter.GetBytes(ID1), 0, 4);
                res.Seek(13, 0);
                res.Write(BitConverter.GetBytes(opt), 0, 4);
                res.Write(BitConverter.GetBytes(ID2), 0, 4);
                res.Write(BitConverter.GetBytes(ID3), 0, 4);
                res.WriteByte((byte)tp);
                bank.HIRCObjects[m] = res.ToArray();
                ListRefresh2();
                listBox2.SelectedIndex = m;
            }
        }

        private void saveBankToPccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1 || bank == null)
                return;
            byte[] tmp = bank.RecreateBinary();
            pcc.Exports[objects[n]].Data = tmp;
            MessageBox.Show("Done.");
        }

        private void savePccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                pcc.altSaveToFile(d.FileName, true);
                MessageBox.Show("Done.");
            }
        }
    }
}
