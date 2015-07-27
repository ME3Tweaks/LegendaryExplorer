using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer
{
    public partial class XBoxConverter : Form
    {
        public struct ChunkBlock
        {
            public int sizeC;
            public int sizeUC;
        }

        public XBoxConverter()
        {
            InitializeComponent();
        }

        private void xXXPCCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "ME3 XBox File(*.xxx)|*.xxx";
            if (d.ShowDialog() == DialogResult.OK)
            {
                string loc = Path.GetDirectoryName(Application.ExecutablePath);
                File.Copy(d.FileName, loc + "\\exec\\temp.dat",true);
                System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(loc + "\\exec\\quickbms.exe", "-o XBoxLZX.bms temp.dat");
                procStartInfo.WorkingDirectory = Path.GetDirectoryName(loc + "\\exec\\");
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.UseShellExecute = false;
                procStartInfo.CreateNoWindow = true;
                rtb1.Text = "Loading file: " + d.FileName + "\n";
                using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(procStartInfo))
                {
                    using (StreamReader reader = process.StandardOutput)
                    {
                        string result = reader.ReadToEnd();
                        rtb1.Text += result;
                        rtb1.SelectionStart = rtb1.Text.Length;
                        rtb1.ScrollToCaret();
                        Application.DoEvents();
                    }
                }
                if (File.Exists(loc + "\\exec\\out.bin"))
                {
                    string n = Path.GetDirectoryName(d.FileName) + "\\" + Path.GetFileNameWithoutExtension(d.FileName) +  ".pcc";
                    File.Copy(loc + "\\exec\\out.bin", n, true);
                    rtb1.Text += "\nFile \"" + n + "\" written to disk.";
                    rtb1.SelectionStart = rtb1.Text.Length;
                    rtb1.ScrollToCaret();
                    File.Delete(loc + "\\exec\\out.bin");
                    File.Delete(loc + "\\exec\\temp.dat");

                }
            }
        }

        private byte[] StrToBuf(string s)
        {
            byte[] t = new byte[s.Length];
            for (int i = 0; i < s.Length; i++)
                t[i] = (byte)s[i];
            return t;
        }

        private void pCCXXXToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "ME3 XBox File(*.pcc)|*.pcc";
            List<ChunkBlock> cb = new List<ChunkBlock>();
            if (d.ShowDialog() == DialogResult.OK)
            {
                string loc = Path.GetDirectoryName(Application.ExecutablePath);
                File.Copy(d.FileName, loc + "\\exec\\temp.dat", true);                
                FileStream fs = new FileStream(loc + "\\exec\\temp.dat", FileMode.Open, FileAccess.Read);
                int block = 0x20000;
                int count = (int)fs.Length / block;
                rtb1.Text = "Loading file: " + d.FileName + "\n";
                if (fs.Length % block != 0)
                    count++;
                int pos = 0;
                int size = block;
                byte[] buff;
                if (File.Exists(loc + "\\exec\\tmpout.dat"))
                    File.Delete(loc + "\\exec\\tmpout.dat");
                FileStream fout = new FileStream(loc + "\\exec\\tmpout.dat", FileMode.Create, FileAccess.Write);
                for (int i = 0; i < count; i++)
                {
                    if (pos + size > fs.Length)
                        size = (int)fs.Length - pos;                    
                    FileStream fs2 = new FileStream(loc + "\\exec\\tmp.dat", FileMode.Create, FileAccess.Write);
                    for (int j = 0; j < size; j++)
                        fs2.WriteByte((byte)fs.ReadByte());
                    fs2.Close();
                    fs2 = new FileStream(loc + "\\exec\\Convert.bms", FileMode.Create, FileAccess.Write);
                    buff = StrToBuf("log \"out.bin\" 0 0" +(char)0xD + (char) 0xA);
                    fs2.Write(buff, 0, buff.Length);
                    buff = StrToBuf("ComType xmemlzx_compress" + (char)0xD + (char)0xA);
                    fs2.Write(buff, 0, buff.Length);
                    buff = StrToBuf("Clog \"out.bin\" 0 " + size.ToString() + " 10000000" + (char)0xD + (char)0xA);
                    fs2.Write(buff, 0, buff.Length);
                    buff = StrToBuf("Print \"Compressing Block #" + i.ToString() + "/" + count.ToString() + "\"...");
                    fs2.Write(buff, 0, buff.Length);
                    fs2.Close();
                    System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(loc + "\\exec\\quickbms.exe", "-o Convert.bms tmp.dat");
                    procStartInfo.WorkingDirectory = Path.GetDirectoryName(loc + "\\exec\\");
                    procStartInfo.RedirectStandardOutput = true;
                    procStartInfo.UseShellExecute = false;
                    procStartInfo.CreateNoWindow = true;
                    using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(procStartInfo))
                    {
                        using (StreamReader reader = process.StandardOutput)
                        {
                            string result = reader.ReadToEnd();
                            rtb1.Text += result;
                            rtb1.SelectionStart = rtb1.Text.Length;
                            rtb1.ScrollToCaret();
                            Application.DoEvents();
                        }
                    }
                    fs2 = new FileStream(loc + "\\exec\\out.bin", FileMode.Open, FileAccess.Read);
                    for (int j = 0; j < fs2.Length; j++)
                        fout.WriteByte((byte)fs2.ReadByte());
                    buff = new byte[4];
                    fout.Write(buff, 0, 4);
                    ChunkBlock t = new ChunkBlock();
                    t.sizeUC = size;
                    t.sizeC = (int)fs2.Length + 4;
                    cb.Add(t);
                    fs2.Close();
                    pos += size;                    
                }
                int fullsize = (int)fout.Length;                
                fout.Close();
                rtb1.Text += "\nCombining Chunks to xxx file...";
                rtb1.SelectionStart = rtb1.Text.Length;
                rtb1.ScrollToCaret();
                bool isLittle = BitConverter.IsLittleEndian;
                BitConverter.IsLittleEndian = true;
                fout = new FileStream(Path.GetDirectoryName(d.FileName) + "\\" + Path.GetFileNameWithoutExtension(d.FileName) + ".xxx", FileMode.Create, FileAccess.Write);
                buff = BitConverter.GetBytes(0x9E2A83C1);
                fout.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(0x00020000);
                fout.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(fullsize);
                fout.Write(buff, 0, 4); 
                buff = BitConverter.GetBytes(fs.Length);
                fout.Write(buff, 0, 4);
                fs.Close();
                for (int i = 0; i < cb.Count; i++)
                {
                    buff = BitConverter.GetBytes(cb[i].sizeC);
                    fout.Write(buff, 0, 4);
                    buff = BitConverter.GetBytes(cb[i].sizeUC);
                    fout.Write(buff, 0, 4);
                }
                fs = new FileStream(loc + "\\exec\\tmpout.dat", FileMode.Open, FileAccess.Read);
                for(int i=0;i<fs.Length ;i++)
                    fout.WriteByte((byte)fs.ReadByte());
                fout.Close();
                fs.Close();
                if (File.Exists(loc + "\\exec\\tmp.dat"))
                    File.Delete(loc + "\\exec\\tmp.dat");
                if (File.Exists(loc + "\\exec\\tmpout.dat"))
                    File.Delete(loc + "\\exec\\tmpout.dat");
                if (File.Exists(loc + "\\exec\\temp.dat"))
                    File.Delete(loc + "\\exec\\temp.dat");
                if (File.Exists(loc + "\\exec\\Convert.bms"))
                    File.Delete(loc + "\\exec\\Convert.bms");
                if (File.Exists(loc + "\\exec\\out.bin"))
                    File.Delete(loc + "\\exec\\out.bin");
                rtb1.Text += "\nFile: " + Path.GetDirectoryName(d.FileName) + "\\" + Path.GetFileNameWithoutExtension(d.FileName) + ".xxx written to disk.";
                rtb1.SelectionStart = rtb1.Text.Length;
                rtb1.ScrollToCaret();
                BitConverter.IsLittleEndian = isLittle;
            }
        }

        private void XBoxConverter_FormClosing(object sender, FormClosingEventArgs e)
        {
            taskbar.RemoveTool(this);
        }
    }
}
