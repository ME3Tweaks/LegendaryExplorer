using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;

namespace ME3Explorer.ClassViewer
{
    public partial class ClassViewer : Form
    {
        public PCCObject pcc;
        public List<int> Objects;
        public string output;

        public ClassViewer()
        {
            InitializeComponent();
        }

        private void openPccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                pcc = new PCCObject(d.FileName);
                Objects = new List<int>();
                for (int i = 0; i < pcc.Exports.Count; i++)
                    if (pcc.Exports[i].ClassName == "Class")
                        Objects.Add(i);
                RefreshLists();
            }
        }

        public void RefreshLists()
        {
            listBox1.Items.Clear();
            foreach (int i in Objects)
                listBox1.Items.Add("#" + i + " : " + pcc.Exports[i].ObjectName);
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1 || pcc == null)
                return;
            AnalyzeClass(Objects[n]);
        }

        public void AnalyzeClass(int index)
        {
            BitConverter.IsLittleEndian = true;
            output = "";
            Printf("Start Analyzing...");
            byte[] buff = pcc.Exports[index].Data;
            int pos = 0;
            for (int i = 0; i < 5; i++)
            {
                int v = GetInt(buff, pos);
                if (i == 1 ||  i == 3)
                    Printf("Header Value : 0x" + v.ToString("X8") + " " + GetFullName(v));
                else
                    Printf("Header Value : 0x" + v.ToString("X8"));
                pos += 4;
            }
            int sec1size = GetInt(buff, pos);
            pos += 4;
            Printf("Section 1 Size : 0x" + sec1size.ToString("X8"));
            if (sec1size < 0)
            {
                Printf("#Err: Expected positive value!");
                return;
            }
            if (sec1size + pos > buff.Length)
            {
                Printf("#Err: Unexpected size!");
                return;
            }
            if (sec1size != 0)
            {
                string s = "\t Data:";
                for (int i = 0; i < sec1size; i++)
                    s += buff[pos + i].ToString("X2") + " ";
                Printf(s);
                pos += sec1size;
            }
            for (int i = 0; i < 4; i++)
            {
                Printf("Header Value : 0x" + GetInt(buff, pos).ToString("X8"));
                pos += 4;
            }
            Printf("Header Value : 0x" + GetInt16(buff, pos).ToString("X4"));
            pos += 2;
            Printf("Header Value : 0x" + GetInt(buff, pos).ToString("X8"));
            pos += 4;
            int sec2count = GetInt(buff, pos);
            pos += 4;
            Printf("Section 2 Count : " + sec2count);
            if (sec2count < 0)
            {
                Printf("#Err: Expected positive value!");
                return;
            }
            if (sec2count * 0xC + pos > buff.Length)
            {
                Printf("#Err: Unexpected size!");
                return;
            }
            if (sec2count != 0)
                for (int i = 0; i < sec2count; i++)
                {
                    int v = GetInt(buff, pos);
                    string s = "\t#" + i + " : 0x" + v.ToString("X8");
                    s += " 0x" + GetInt(buff, pos + 4).ToString("X8");
                    int v2 = GetInt(buff, pos + 8);
                    s += " 0x" + v2.ToString("X8");
                    s += " \"" + pcc.getNameEntry(v) +"\" " + GetFullName(v2);
                    Printf(s);
                    pos += 0xC;
                }
            for (int i = 0; i < 4; i++)
            {
                int v = GetInt(buff, pos);
                if (i == 1)
                    Printf("Header Value : 0x" + v.ToString("X8") + " " + GetFullName(v));
                else if (i == 2)
                    Printf("Header Value : 0x" + v.ToString("X8") + " " + pcc.getNameEntry(v));
                else
                    Printf("Header Value : 0x" + v.ToString("X8"));
                pos += 4;
            }
            int sec3count = GetInt(buff, pos);
            pos += 4;
            Printf("Section 3 Count : " + sec3count);
            if (sec3count < 0)
            {
                Printf("#Err: Expected positive value!");
                return;
            }
            if (sec3count * 0xC + pos > buff.Length)
            {
                Printf("#Err: Unexpected size!");
                return;
            }
            if (sec3count != 0)
                for (int i = 0; i < sec3count; i++)
                {
                    int v = GetInt(buff, pos);
                    string s = "\t#" + i + " : 0x" + v.ToString("X8");
                    s += " 0x" + GetInt(buff, pos + 4).ToString("X8");
                    int v2 = GetInt(buff, pos + 8);
                    s += " 0x" + v2.ToString("X8");
                    s += " \"" + pcc.getNameEntry(v) + "\" " + GetFullName(v2);
                    Printf(s);
                    pos += 0xC;
                }
            int sec4count = GetInt(buff, pos);
            pos += 4;
            Printf("Section 4 Count : " + sec4count);
            if (sec4count < 0)
            {
                Printf("#Err: Expected positive value!");
                return;
            }
            if (sec4count * 8 + pos > buff.Length)
            {
                Printf("#Err: Unexpected size!");
                return;
            }
            if (sec4count != 0)
                for (int i = 0; i < sec4count; i++)
                {
                    int v1 = GetInt(buff, pos);
                    string s = "\t#" + i + " : 0x" + v1.ToString("X8");
                    int v2 = GetInt(buff, pos + 4);
                    s += " 0x" + GetInt(buff, pos + 4).ToString("X8") + " " + GetFullName(v1) + " " + GetFullName(v2);
                    Printf(s);
                    pos += 8;
                }
            for (int i = 0; i < 4; i++)
            {
                int v = GetInt(buff, pos);
                if (i == 0)
                    Printf("Header Value : 0x" + v.ToString("X8") + " " + pcc.getNameEntry(v));
                else if (i == 3)
                    Printf("Header Value : 0x" + v.ToString("X8") + " " + GetFullName(v));
                else
                    Printf("Header Value : 0x" + v.ToString("X8"));
                pos += 4;
            }
            int sec5count = GetInt(buff, pos);
            pos += 4;
            Printf("Section 5 Count : " + sec5count);
            if (sec5count < 0)
            {
                Printf("#Err: Expected positive value!");
                return;
            }
            if (sec5count * 4 + pos > buff.Length)
            {
                Printf("#Err: Unexpected size!");
                return;
            }
            if (sec5count != 0)
                for (int i = 0; i < sec5count; i++)
                {
                    int v = GetInt(buff, pos);
                    Printf("\t#" + i + " : 0x" + v.ToString("X8") + " " + GetFullName(v));
                    pos += 4;
                }
            if (pos == buff.Length)
                output = "FINISHED!\n" + output;
            else
                output = "NOT FINISHED!\n" + output;
            rtb1.Text = output;
        }

        public string GetFullName(int index)
        {
            string s = "";
            int i = index;
            if (i < 0)
            {
                i = -i - 1;
                if (pcc.Imports[i].PackageFullName != "Class" && pcc.Imports[i].PackageFullName != "Package")
                    s += pcc.Imports[i].PackageFullName + ".";
                s += pcc.Imports[i].ObjectName;
            }
            else if (i > 0) 
            {
                i--;
                if (pcc.Exports[i].PackageFullName != "Class" && pcc.Exports[i].PackageFullName != "Package")
                    s += pcc.Exports[i].PackageFullName + ".";
                s += pcc.Exports[i].ObjectName;
            }
            return s;
        }

        public int GetInt(byte[] buff, int pos)
        {
            if (pos < 0 || pos > buff.Length - 4)
                return 0;
            return BitConverter.ToInt32(buff, pos);
        }

        public ushort GetInt16(byte[] buff, int pos)
        {
            if (pos < 0 || pos > buff.Length - 2)
                return 0;
            return BitConverter.ToUInt16(buff, pos);
        }


        public void Printf(string s)
        {
            output += s + "\n";
        }
    }
}
