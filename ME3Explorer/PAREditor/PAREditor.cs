using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Be.Windows.Forms;

namespace ME3Explorer.PAREditor
{
    public partial class PAREditor : Form
    {
        public byte[] Memory;
        public string Data;

        public PAREditor()
        {
            InitializeComponent();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.par|*.par";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Memory = File.ReadAllBytes(d.FileName);
                hb1.ByteProvider = new DynamicByteProvider(Memory);
                DecodeBinary();
            }
        }

        public void DecodeBinary()
        {
            byte[] buff = PARFileDecXOR(Memory);
            string s = "";
            foreach (byte b in buff)
                s += (char)b;
            rtb1.Text = s;
        }

        private string PARFileKey = "q@pO3o#5jNA6$sjP3qwe1";

        public byte[] PARFileDecXOR(byte[] PARFileContents)
        {
            int PARFileKeyPosition = 0;
            byte[] PARFileContentToProcess;
            char[] PARFileXORedContent = new char[PARFileContents.Length];
            byte[] PARFileKeyByteArray = Encoding.UTF8.GetBytes(PARFileKey);
            if (PARFileContents[0] != 0x2A ||
                PARFileContents[1] != 0x02 ||
                PARFileContents[2] != 0x11 ||
                PARFileContents[3] != 0x3C)
                PARFileContentToProcess = PARFileContents.Skip(4).ToArray();
            else
                PARFileContentToProcess = PARFileContents;
            for (int i = 0; i < PARFileContentToProcess.Length; i++)
            {
                PARFileXORedContent[i] = (char)(PARFileContentToProcess[i] ^ PARFileKeyByteArray[PARFileKeyPosition]);
                PARFileKeyPosition = ((PARFileKeyPosition + 1) % PARFileKeyByteArray.Length);
            }

            return Encoding.UTF8.GetBytes(PARFileXORedContent);
        }
        public byte[] PARFileEncXOR(byte[] PARFileContents)
        {
            int PARFileKeyPosition = 0;
            byte[] PARFileXORedContent = new byte[PARFileContents.Length];
            byte[] PARFileKeyByteArray = Encoding.UTF8.GetBytes(PARFileKey);
            for (int i = 0; i < PARFileContents.Length; i++)
            {
                PARFileXORedContent[i] = (byte)(PARFileKeyByteArray[PARFileKeyPosition] ^ PARFileContents[i]);
                PARFileKeyPosition = ((PARFileKeyPosition + 1) % PARFileKeyByteArray.Length);
            }
            return PARFileXORedContent;
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.par|*.par";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                byte[] buff = Encoding.UTF8.GetBytes(rtb1.Text);
                byte[] res = PARFileEncXOR(buff);
                MemoryStream m = new MemoryStream();
                m.Write(res, 0, res.Length);
                Memory = m.ToArray();
                hb1.ByteProvider = new DynamicByteProvider(Memory);
                DecodeBinary();
                File.WriteAllBytes(d.FileName, Memory);
                MessageBox.Show("Done.");
            }
        }
    }
}
