using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer.HeaderEditor
{
    public partial class HeaderEditor : Form
    {
        public byte[] Memory;
        public string MyFileName;

        public HeaderEditor()
        {
            InitializeComponent();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Memory = File.ReadAllBytes(d.FileName);
                MyFileName = d.FileName;
                ReadHeader();
            }
        }

        public void ReadHeader()
        {
            BitConverter.IsLittleEndian = true;
            textBox1.Text = BitConverter.ToInt32(Memory, 0x1A).ToString("X8");
            int test = BitConverter.ToInt32(Memory, 0x1E);
            int pos = test == 0 ? 0x22 : 0x1E;
            textBox2.Text = BitConverter.ToInt32(Memory, pos).ToString();
            textBox3.Text = BitConverter.ToInt32(Memory, pos + 4).ToString("X8");
            textBox4.Text = BitConverter.ToInt32(Memory, pos + 8).ToString();
            textBox5.Text = BitConverter.ToInt32(Memory, pos + 12).ToString("X8");
            textBox6.Text = BitConverter.ToInt32(Memory, pos + 16).ToString();
            textBox7.Text = BitConverter.ToInt32(Memory, pos + 20).ToString("X8");
        }

        public void WriteHeader()
        {
            if (MyFileName == "")
                return;
            MemoryStream m = new MemoryStream(Memory);
            BitConverter.IsLittleEndian = true;
            m.Seek(0x1A, 0);
            m.Write(BitConverter.GetBytes(Int32.Parse(textBox1.Text, System.Globalization.NumberStyles.HexNumber)), 0, 4);
            int test = BitConverter.ToInt32(Memory, 0x1E);
            int pos = test == 0 ? 0x22 : 0x1E;
            m.Seek(pos, 0);
            m.Write(BitConverter.GetBytes(Int32.Parse(textBox2.Text, System.Globalization.NumberStyles.Integer)), 0, 4);
            m.Write(BitConverter.GetBytes(Int32.Parse(textBox3.Text, System.Globalization.NumberStyles.HexNumber)), 0, 4);
            m.Write(BitConverter.GetBytes(Int32.Parse(textBox4.Text, System.Globalization.NumberStyles.Integer)), 0, 4);
            m.Write(BitConverter.GetBytes(Int32.Parse(textBox5.Text, System.Globalization.NumberStyles.HexNumber)), 0, 4);
            m.Write(BitConverter.GetBytes(Int32.Parse(textBox6.Text, System.Globalization.NumberStyles.Integer)), 0, 4);
            m.Write(BitConverter.GetBytes(Int32.Parse(textBox7.Text, System.Globalization.NumberStyles.HexNumber)), 0, 4);
            File.WriteAllBytes(MyFileName, m.ToArray());
            MessageBox.Show("Done.");
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WriteHeader();
        }
    }
}
