using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer.UECodeEditor
{
    public partial class OPCodeTable : Form
    {
        public OPCodeTable()
        {
            InitializeComponent();
        }

        private void OPCodeTable_Paint(object sender, PaintEventArgs e)
        {
            
        }

        public void RefreshLists()
        {
            listBox1.Items.Clear();
            if (OPCodes.Table == null)
                OPCodes.Table = new List<OPCodes.OPCEntry>();
            bool run = true;
            while (run)
            {
                run = false;
                for (int i = 0; i < OPCodes.Table.Count - 1; i++)
                    if (OPCodes.Table[i].OPCode > OPCodes.Table[i + 1].OPCode)
                    {
                        OPCodes.OPCEntry e = OPCodes.Table[i];
                        OPCodes.Table[i] = OPCodes.Table[i + 1];
                        OPCodes.Table[i + 1] = e;
                        run = true;
                    }
            }
            foreach (OPCodes.OPCEntry e in OPCodes.Table)
            {
                string s = "OPCode : 0x" + e.OPCode.ToString("X2") + " Pattern: " + e.Pattern;
                listBox1.Items.Add(s);
            }
        }

        private void OPCodeTable_Activated(object sender, EventArgs e)
        {
            RefreshLists();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            OPCodes.OPCEntry en = new OPCodes.OPCEntry();
            try
            {
                string text = textBox1.Text.Trim();
                int i = Int32.Parse(text, System.Globalization.NumberStyles.HexNumber);
                en.OPCode = i;
                en.Pattern = textBox2.Text;
                OPCodes.Table.Add(en);
                RefreshLists();
            }
            catch (Exception ex)
            {
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            OPCodes.Table.Remove(OPCodes.Table[n]);
            RefreshLists();
        }

        private void newTableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OPCodes.Table = new List<OPCodes.OPCEntry>();
        }

        private void saveTableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.opc|*.opc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                BitConverter.IsLittleEndian = true;
                byte[] buff = BitConverter.GetBytes(OPCodes.Table.Count);
                fs.Write(buff, 0, 4);
                foreach (OPCodes.OPCEntry en in OPCodes.Table)
                {
                    fs.Write(BitConverter.GetBytes(en.OPCode), 0, 4);
                    WriteString(fs, en.Pattern);
                }
                fs.Close();
                MessageBox.Show("Done.");
            }
        }

        public void WriteString(FileStream fs, string s)
        {
            fs.Write(BitConverter.GetBytes((int)s.Length), 0, 4);
            fs.Write(GetBytes(s), 0, s.Length);
        }

        public string ReadString(FileStream fs)
        {
            string s = "";
            byte[] buff = new byte[4];
            for (int i = 0; i < 4; i++)
                buff[i] = (byte)fs.ReadByte();
            int count = BitConverter.ToInt32(buff, 0);
            buff = new byte[count];
            for (int i = 0; i < count; i++)
                buff[i] = (byte)fs.ReadByte();
            s = GetString(buff);
            return s;
        }

        public byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length];
            for (int i = 0; i < str.Length; i++)
                bytes[i] = (byte)str[i];
            return bytes;
        }

        public string GetString(byte[] bytes)
        {
            string s = "";
            for (int i = 0; i < bytes.Length; i++)
                s += (char)bytes[i];
            return s;
        }

        private void loadTableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.opc|*.opc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Open, FileAccess.Read);
                BitConverter.IsLittleEndian = true;
                byte[] buff = new byte[4];
                fs.Read(buff, 0, 4);
                int count = BitConverter.ToInt32(buff, 0);
                OPCodes.Table = new List<OPCodes.OPCEntry>();
                for (int i = 0; i < count; i++) 
                {
                    OPCodes.OPCEntry en = new OPCodes.OPCEntry();
                    fs.Read(buff, 0, 4);
                    en.OPCode = BitConverter.ToInt32(buff, 0);
                    en.Pattern = ReadString(fs);
                    OPCodes.Table.Add(en);
                }
                fs.Close();
                RefreshLists();
            }
        }
    }
}
