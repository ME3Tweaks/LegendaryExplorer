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
    public partial class Conditionals : Form
    {
        public struct Entries
        {
            public int id;
            public int off;
            public int size;
            public int listoff;
            public byte[] data;
            public List<int> refbool;
            public List<int> refint;
        }

        public struct OptFlag
        {
            public byte flagbyte;
            public byte optbyte;
        }

        public List<Entries> Entry = new List<Entries>();
        public byte[] memory;
        public int memsize;
        public List<int> currefint = new List<int>();
        public List<int> currefbool = new List<int>();
        public Conditionals()
        {
            InitializeComponent();
        }
        public int UnknownInt16;

        int ReadInt32(FileStream fs)
        {
            byte[] buff = new byte[4];
            fs.Read(buff, 0, 4);
            return BitConverter.ToInt32(buff, 0);
        }
        int ReadInt16(FileStream fs)
        {
            byte[] buff = new byte[2];
            fs.Read(buff, 0, 2);
            return BitConverter.ToInt16(buff, 0);
        }

        byte[] ReadArray(FileStream fs, uint pos, uint len)
        {
            byte[] res = new byte[len];
            int cnt;
            int sum = 0;
            fs.Position = pos;
            while ((cnt = fs.Read(res, sum, (int)len - sum)) > 0) sum += cnt;
            return res;
        }

        public void SortEntries()
        {
            bool done = false;
            while (!done)
            {
                done = true;
                for (int i = 0; i < Entry.Count() - 1; i++)
                {
                    if (Entry[i].off > Entry[i + 1].off)
                    {
                        done = false;
                        Entries temp = Entry[i];
                        Entry[i] = Entry[i + 1];
                        Entry[i + 1] = temp;
                    }
                }
            }
        }
        public void SortEntries2()
        {
            bool done = false;
            while (!done)
            {
                done = true;
                for (int i = 0; i < Entry.Count() - 1; i++)
                {
                    if (Entry[i].id > Entry[i + 1].id)
                    {
                        done = false;
                        Entries temp = Entry[i];
                        Entry[i] = Entry[i + 1];
                        Entry[i + 1] = temp;
                    }
                }
            }
        }

        public void CalcSize(long filesize)
        {
            int a = 0;
            int b = 0;
            int size = 0;
            Entries temp;
            for (int i = 0; i < Entry.Count(); i++)
            {
                a = Entry[i].off;
                if (i == Entry.Count - 1)
                {
                    b = (int)filesize;
                }
                else for (int j = i + 1; j < Entry.Count(); j++)
                    {
                        if (Entry[j].off > a)
                        {
                            b = Entry[j].off;
                            break;
                        }
                    }
                size = b - a;
                temp = Entry[i];
                temp.size = size;
                Entry[i] = temp;
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BitConverter.IsLittleEndian = true;
            string path = string.Empty;
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Filter = "*.cnd|*.cnd";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                path = openFileDialog1.FileName;
                LoadFile(path);
            }
        }

        public void LoadFile(string path)
        {
            FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            memsize = (int)fileStream.Length;
            memory = new byte[memsize];
            int cnt;
            int sum = 0;
            while ((cnt = fileStream.Read(memory, sum, memsize - sum)) > 0) sum += cnt;
            fileStream.Close();
            fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            BitConverter.IsLittleEndian = true;
            int magic = ReadInt32(fileStream);
            if (magic != 0x434F4E44)
            {
                fileStream.Close();
                return;
            }
            int version = ReadInt32(fileStream);
            if (version != 1)
            {
                fileStream.Close();
                return;
            }
            UnknownInt16 = ReadInt16(fileStream);
            int count = ReadInt16(fileStream);
            Entry = new List<Entries>();
            listBox1.Items.Clear();
            Entries temp = new Entries();
            for (int i = 0; i < count; i++)
            {
                temp.id = ReadInt32(fileStream);
                temp.off = ReadInt32(fileStream);
                temp.size = -1;
                temp.listoff = i * 8 + 12;
                Entry.Add(temp);
            }
            SortEntries();
            CalcSize(fileStream.Length);
            for (int i = 0; i < count; i++)
            {
                temp = Entry[i];
                temp.data = ReadArray(fileStream, (uint)temp.off, (uint)temp.size);
                Entry[i] = temp;
            }
            SortEntries2();
            for (int i = 0; i < count; i++)
            {
                temp = Entry[i];
                listBox1.Items.Add(temp.id.ToString() + " : " + temp.off.ToString("X") + " = " + temp.size.ToString("X"));
            }
            fileStream.Close();
        }

        public void RefreshList()
        {
            listBox1.Items.Clear();
            for (int i = 0; i < Entry.Count; i++)
            {
                Entries temp = Entry[i];
                listBox1.Items.Add(temp.id.ToString() + " : " + temp.off.ToString("X") + " = " + temp.size.ToString("X"));
            }
        }

        public void ReplaceData(byte[] buff, int entry)
        {
            Entries entr = Entry[entry];
            entr.data = buff;
            entr.size = (int)buff.Length;
            Entry[entry] = entr;
            RefreshList();
        }

        public OptFlag getOptFlag(byte b)
        {
            OptFlag temp = new OptFlag();
            temp.flagbyte = (byte)((b & 0x0F) >> 0);
            temp.optbyte = (byte)((b & 0xF0) >> 4);
            return temp;
        }

        public string CondFloatExp(byte[] buffer)
        {
            string s = "";
            var op = buffer[1];
            switch (op)
            {
                case 0:
                    {
                        var count = BitConverter.ToUInt16(buffer, 2);
                        for (int i = 0, j = 0; i < count; i++, j += 2)
                        {
                            if (i > 0)
                                s += " + ";
                            int n = BitConverter.ToUInt16(buffer, j + 4) + 4;
                            byte[] sub = new byte[buffer.Length - n];
                            for (int k = n; k < buffer.Length; k++)
                                sub[k - n] = buffer[k];
                            s += CondFloat(sub);
                        }
                        break;
                    }

                case 2:
                    {
                        var count = BitConverter.ToUInt16(buffer, 2);
                        for (int i = 0, j = 0; i < count; i++, j += 2)
                        {
                            if (i > 0)
                                s += " * ";
                            int n = BitConverter.ToUInt16(buffer, j + 4) + 4;
                            byte[] sub = new byte[buffer.Length - n];
                            for (int k = n; k < buffer.Length; k++)
                                sub[k - n] = buffer[k];
                            s += CondFloat(sub);
                        }
                        break;
                    }

                default:
                    {
                        break;
                    }
            }
            return s;
        }

        public string CondFloat(byte[] buffer)
        {
            string s = "";
            OptFlag Flags = getOptFlag(buffer[0]);
            switch (Flags.flagbyte)
            {
                case 1://int
                    {
                        s = CondInt(buffer);
                        break;
                    }

                case 2://float
                    {
                        switch (Flags.optbyte)
                        {
                            case 2://float
                                {
                                    s = "f" + BitConverter.ToSingle(buffer, 1).ToString();
                                    break;
                                }

                            case 5://expression
                                {
                                    return "(" + CondFloatExp(buffer) + ")";
                                }

                            case 6://table
                                {
                                    s = "plot.floats[" + BitConverter.ToInt32(buffer, 1).ToString() + "]";
                                    break;
                                }

                            default:
                                {
                                    break;
                                }
                        }
                        break;
                    }

                default:
                    {
                        break;
                    }
            }
            return s;
        }

        public string CondIntExp(byte[] buffer)
        {
            string s = "";
            var op = buffer[1];
            switch (op)
            {
                case 0:
                    {
                        var count = BitConverter.ToUInt16(buffer, 2);
                        for (int i = 0, j = 0; i < count; i++, j += 2)
                        {
                            if (i > 0)
                                s += " + ";
                            int n = BitConverter.ToUInt16(buffer, j + 4) + 4;
                            byte[] sub = new byte[buffer.Length - n];
                            for (int k = n; k < buffer.Length; k++)
                                sub[k - n] = buffer[k];
                            s += CondInt(sub);
                        }
                        break;
                    }

                default:
                    {
                        break;
                    }
            }
            return s;
        }

        public string CondInt(byte[] buffer)
        {
            string s = "";
            OptFlag Flags = getOptFlag(buffer[0]);
            switch (Flags.flagbyte)
            {
                case 1://int
                    {
                        switch (Flags.optbyte)
                        {
                            case 1://int
                                {
                                    s = "i" + BitConverter.ToInt32(buffer, 1).ToString();
                                    break;
                                }
                            case 3://argument
                                {
                                    var value = BitConverter.ToInt32(buffer, 1);
                                    s = "a" + value.ToString();
                                    break;
                                }

                            case 5://expression
                                {
                                    return "(" + CondIntExp(buffer) + ")";
                                }

                            case 6://table
                                {
                                    currefint.Add(BitConverter.ToInt32(buffer, 1));
                                    s = "plot.ints[" + BitConverter.ToInt32(buffer, 1).ToString() + "]";
                                    break;
                                }

                            default:
                                {
                                    break;
                                }
                        }
                        break;
                    }

                case 2://float
                    {
                        s = CondFloat(buffer);
                        break;
                    }

                default:
                    {
                        break;
                    }
            }
            return s;
        }

        public string CondGen(byte[] buffer)
        {
            OptFlag Flags = getOptFlag(buffer[0]);
            switch (Flags.flagbyte)
            {
                case 0://bool
                    {
                        return CondBool(buffer);
                    }

                case 1://int
                    {
                        return CondInt(buffer);
                    }

                default:
                    {
                        return CondFloat(buffer);
                    }
            }

        }

        public string CondBoolExp(byte[] buffer)
        {
            string s = "";
            var op = buffer[1];
            switch (op)
            {
                case 4:
                    {
                        var count = BitConverter.ToUInt16(buffer, 2);
                        for (int i = 0, j = 0; i < count; i++, j += 2)
                        {
                            if (i > 0)
                                s += " && ";
                            int n = BitConverter.ToUInt16(buffer, j + 4) + 4;
                            byte[] sub = new byte[buffer.Length - n];
                            for (int k = n; k < buffer.Length; k++)
                                sub[k - n] = buffer[k];
                            s += CondBool(sub);
                        }
                        break;
                    }

                case 5:
                    {
                        var count = BitConverter.ToUInt16(buffer, 2);
                        for (int i = 0, j = 0; i < count; i++, j += 2)
                        {
                            if (i > 0)
                                s += " || ";
                            int n = BitConverter.ToUInt16(buffer, j + 4) + 4;
                            byte[] sub = new byte[buffer.Length - n];
                            for (int k = n; k < buffer.Length; k++)
                                sub[k - n] = buffer[k];
                            s += CondBool(sub);
                        }
                        break;
                    }

                case 6:
                    {
                        int n = BitConverter.ToUInt16(buffer, 4) + 4;
                        byte[] sub = new byte[buffer.Length - n];
                        for (int k = n; k < buffer.Length; k++)
                            sub[k - n] = buffer[k];
                        s = CondBool(sub) + " == false";
                        break;
                    }

                case 7:
                case 8:
                case 9:
                case 10:
                case 11:
                case 12:
                    {
                        int n = BitConverter.ToUInt16(buffer, 4) + 4;
                        byte[] sub = new byte[buffer.Length - n];
                        for (int k = n; k < buffer.Length; k++)
                            sub[k - n] = buffer[k];
                        n = BitConverter.ToUInt16(buffer, 6) + 4;
                        byte[] sub2 = new byte[buffer.Length - n];
                        for (int k = n; k < buffer.Length; k++)
                            sub2[k - n] = buffer[k];

                        var left = CondGen(sub);
                        var right = CondGen(sub2);

                        var comparisonType = buffer[1];
                        switch (comparisonType)
                        {
                            case 7:
                                {
                                    s = left + " == " + right;
                                    break;
                                }

                            case 8:
                                {
                                    s = left + " != " + right;
                                    break;
                                }

                            case 9:
                                {
                                    s = left + " < " + right;
                                    break;
                                }

                            case 10:
                                {
                                    s = left + " <= " + right;
                                    break;
                                }

                            case 11:
                                {
                                    s = left + " > " + right;
                                    break;
                                }

                            case 12:
                                {
                                    s = left + " >= " + right;
                                    break;
                                }

                            default:
                                {
                                    break;
                                }
                        }
                        break;
                    }

                default:
                    {
                        break;
                    }
            }

            return s;
        }

        public string CondBool(byte[] buffer)
        {
            string s = "";
            OptFlag Flags = getOptFlag(buffer[0]);
            switch (Flags.flagbyte)
            {
                case 0: //bool
                    {
                        switch (Flags.optbyte)
                        {
                            case 0: //bool
                                {
                                    s = (buffer[1] == 1 ? "Bool true" : "Bool false");
                                    break;
                                }
                            case 3://Argument
                                {
                                    var value = BitConverter.ToInt32(buffer, 1);
                                    if (value == -1)
                                    {
                                        s = "(arg == -1)";
                                        break;
                                    }

                                    var functionLength = BitConverter.ToInt16(buffer, 5);
                                    var tagLength = BitConverter.ToInt16(buffer, 7);
                                    string function = "";
                                    for (int i = 0; i < functionLength; i++)
                                        function += (char)buffer[9 + i];
                                    s = "Function :" + function + " Value:" + value.ToString();
                                    if (tagLength > 0)
                                    {
                                        string tag = "";
                                        for (int i = 0; i < tagLength; i++)
                                            function += (char)buffer[9 + functionLength + i];
                                        s += " Tag:" + tag;
                                    }
                                    break;
                                }
                            case 5://expression
                                {
                                    return "(" + CondBoolExp(buffer) + ")";
                                }

                            case 6://table
                                {
                                    currefbool.Add(BitConverter.ToInt32(buffer, 1));
                                    return "plot.bools[" + BitConverter.ToInt32(buffer, 1) + "]";
                                }

                            default:
                                {
                                    break;
                                }

                        }
                        break;
                    }
                case 1://int
                    {
                        s = CondInt(buffer) + " != 0";
                        break;
                    }

                case 2://float
                    {
                        s = CondFloat(buffer) + " != 0";
                        break;
                    }

                default:
                    {
                        break;
                    }

            }
            return s;
        }

        public void DetectCondition(byte[] buffer, bool textout)
        {
            currefbool = new List<int>();
            currefint = new List<int>();
            if (textout)
                rtb1.Text += "\n\n" + CondBool(buffer);
            else
                CondBool(buffer);
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = string.Empty;
            SaveFileDialog FileDialog1 = new SaveFileDialog();
            FileDialog1.Filter = "*.cnd|*.cnd";
            BitConverter.IsLittleEndian = true;
            if (FileDialog1.ShowDialog() == DialogResult.OK)
            {
                path = FileDialog1.FileName;
                FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
                MemoryStream m = new MemoryStream();
                m.Write(BitConverter.GetBytes((uint)0x434F4E44), 0, 4);
                m.Write(BitConverter.GetBytes((int)1), 0, 4);
                m.Write(BitConverter.GetBytes((Int16)UnknownInt16), 0, 2);
                m.Write(BitConverter.GetBytes((Int16)Entry.Count), 0, 2);
                for (int i = 0; i < Entry.Count; i++)
                {
                    Entries en = Entry[i];
                    m.Write(BitConverter.GetBytes(en.id), 0, 4);
                    m.Write(BitConverter.GetBytes(en.off), 0, 4);
                }
                for (int i = 0; i < Entry.Count; i++)
                {
                    Entries en = Entry[i];
                    en.off = (int)m.Position;
                    Entry[i] = en;
                    m.Write(en.data, 0, en.size);
                }
                int pos = 16;
                for (int i = 0; i < Entry.Count; i++)
                {
                    Entries en = Entry[i];
                    m.Seek(pos, SeekOrigin.Begin);
                    m.Write(BitConverter.GetBytes((uint)en.off), 0, 4);
                    pos += 8;
                }
                fileStream.Write(m.ToArray(), 0, (int)m.Length);
                fileStream.Close();

                TOCeditor tc = new TOCeditor();
                tc.MdiParent = this.ParentForm;
                tc.Show();
                string fname = Path.GetFileName(path);
                if (!tc.UpdateFile(fname, (uint)memsize))
                    MessageBox.Show("Didn't found entry!");
                tc.Close();
                LoadFile(path);
            }
        }
        string CreateRect(int x, int y, int w, int h)
        {
            return "<rect x=\"" + x.ToString() + "\" y=\"" + y.ToString() + "\" width=\"" + w.ToString() + "\" height=\"" + h.ToString() + "\" style=\"fill:red;\"/>";
        }
        string CreateText(int x, int y, string s)
        {
            return "<text x=\"" + x.ToString() + "\" y=\"" + y.ToString() + "\">" + s + "</text>";
        }

        string CreateLine(int x1, int y1, int x2, int y2)
        {
            return "<line x1=\"" + x1.ToString() + "\" y1=\"" + y1.ToString() + "\" x2=\"" + x2.ToString() + "\" y2=\"" + y2.ToString() + "\" style=\"stroke:black; stroke-width:2px;\" />";
        }

        private void mapToSVGToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = string.Empty;
            SaveFileDialog FileDialog1 = new SaveFileDialog();
            FileDialog1.Filter = "SVG File(*.svg)|*.svg";
            if (FileDialog1.ShowDialog() == DialogResult.OK)
            {
                path = FileDialog1.FileName;
                FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
                List<int> allrefbool = new List<int>();
                List<int> allrefint = new List<int>();
                pb1.Maximum = Entry.Count;
                for (int i = 0; i < Entry.Count; i++)
                {
                    pb1.Value = i;
                    Application.DoEvents();
                    Entries temp = Entry[i];
                    byte[] buffer = new byte[temp.size];
                    for (int j = 0; j < temp.size; j++)
                        buffer[j] = memory[temp.off + j];
                    DetectCondition(buffer, false);
                    temp.refbool = currefbool;
                    temp.refint = currefint;
                    Entry[i] = temp;
                    for (int j = 0; j < currefbool.Count; j++)
                    {
                        bool gotbool = false;
                        for (int k = 0; k < allrefbool.Count; k++)
                            if (allrefbool[k] == currefbool[j])
                                gotbool = true;
                        if (!gotbool)
                            allrefbool.Add(currefbool[j]);
                    }
                    for (int j = 0; j < currefint.Count; j++)
                    {
                        bool gotint = false;
                        for (int k = 0; k < allrefint.Count; k++)
                            if (allrefint[k] == currefint[j])
                                gotint = true;
                        if (!gotint)
                            allrefint.Add(currefint[j]);
                    }

                    temp.refbool = currefbool;
                    temp.refint = currefint;
                }
                int h = allrefbool.Count * 40 + 40;
                int w = 100000;
                if (allrefint.Count * 40 + 40 > h) h = allrefint.Count * 40 + 40;
                if (Entry.Count * 40 + 40 > h) h = Entry.Count * 40 + 40;
                string Header = "<?xml version=\"1.0\" encoding=\"ISO-8859-1\" standalone=\"no\" ?><!DOCTYPE svg PUBLIC \"-//W3C//DTD SVG 20010904//EN\" \"http://www.w3.org/TR/2001/REC-SVG-20010904/DTD/svg10.dtd\"><svg width=\"" + w.ToString() + "\" height=\"" + h.ToString() + "\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\">";
                for (int i = 0; i < Header.Length; i++)
                    fileStream.WriteByte((byte)Header[i]);
                for (int i = 0; i < allrefbool.Count; i++)
                {
                    string s = CreateRect(10, i * 40, 100, 30);
                    string s2 = CreateText(15, i * 40 + 20, "Bool #" + allrefbool[i].ToString());
                    for (int j = 0; j < s.Length; j++)
                        fileStream.WriteByte((byte)s[j]);
                    for (int j = 0; j < s2.Length; j++)
                        fileStream.WriteByte((byte)s2[j]);
                }
                for (int i = 0; i < allrefint.Count; i++)
                {
                    string s = CreateRect(99900, i * 40, 100, 30);
                    string s2 = CreateText(99915, i * 40 + 20, "Int #" + allrefint[i].ToString());
                    for (int j = 0; j < s.Length; j++)
                        fileStream.WriteByte((byte)s[j]);
                    for (int j = 0; j < s2.Length; j++)
                        fileStream.WriteByte((byte)s2[j]);
                }
                for (int i = 0; i < Entry.Count; i++)
                {
                    Entries temp = Entry[i];
                    string s = CreateRect(50000, i * 40, 100, 30);
                    string s2 = CreateText(50005, i * 40 + 20, "Rule #" + Entry[i].id);
                    for (int j = 0; j < s.Length; j++)
                        fileStream.WriteByte((byte)s[j]);
                    for (int j = 0; j < s2.Length; j++)
                        fileStream.WriteByte((byte)s2[j]);
                    if (temp.refbool != null)
                        for (int j = 0; j < temp.refbool.Count; j++)
                        {
                            for (int k = 0; k < allrefbool.Count; k++)
                                if (allrefbool[k] == temp.refbool[j])
                                {
                                    string s3 = CreateLine(50000, i * 40 + 20, 110, k * 40 + 20);
                                    for (int l = 0; l < s3.Length; l++)
                                        fileStream.WriteByte((byte)s3[l]);
                                }
                        }
                    if (temp.refint != null)
                        for (int j = 0; j < temp.refint.Count; j++)
                        {
                            for (int k = 0; k < allrefint.Count; k++)
                                if (allrefint[k] == temp.refint[j])
                                {
                                    string s3 = CreateLine(50100, i * 40 + 20, 99900, k * 40 + 20);
                                    for (int l = 0; l < s3.Length; l++)
                                        fileStream.WriteByte((byte)s3[l]);
                                }
                        }
                }

                string Footer = "</svg>";
                for (int i = 0; i < Footer.Length; i++)
                    fileStream.WriteByte((byte)Footer[i]);
                fileStream.Close();
                MessageBox.Show("Done");
            }

        }

        private void Conditionals_FormClosing(object sender, FormClosingEventArgs e)
        {
            taskbar.RemoveTool(this);
        }

        private void editExpressionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1) return;
            Entries temp = this.Entry[n];
            CondEditor con = new CondEditor();
            con.MdiParent = this.ParentForm;
            con.cond = this;
            con.currentry = temp;
            con.currnr = n;
            con.setRtb1(CondBool(temp.data));
            con.Show();
        }

        private void cloneToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1) return;
            Entries temp = Entry[n];
            temp.id = GetBiggestID() + 1;
            Entry.Add(temp);
            RefreshList();
        }

        public int GetBiggestID()
        {
            int r = 0;
            foreach (Entries e in Entry)
                if (e.id > r)
                    r = e.id;
            return r;
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1) return;
            Entries temp = Entry[n];
            Entry.Remove(temp);
            RefreshList();
        }

        private void listBox1_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1) return;
            Entries temp = Entry[n];
            string ascii = "";
            string outs = "0\t";
            int limit = 1024;
            int x = 0;
            for (int j = 0; j < temp.size && j < limit; j++)
            {
                x++;
                if (x == 17)
                {
                    x = 1;
                    outs += ascii + "\n" + j.ToString("X") + "\t";
                    ascii = "";
                }
                n = temp.data[j];
                if (n > 15)
                {
                    outs += n.ToString("X") + " ";
                }
                else
                {
                    outs += "0" + n.ToString("X") + " ";
                }
                if (n > 31)
                {
                    ascii += (char)n;
                }
                else
                {
                    ascii += ".";
                }
            }
            if (x == 16)
            {
                outs += ascii;
            }
            else
            {
                for (int j = 0; j < 16 - x; j++)
                    outs += "   ";
                outs += ascii;
            }
            if (temp.size >= limit) outs += "\n...";
            rtb1.Text = outs;
            DetectCondition(temp.data, true);
        }

        private void editIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1) return;
            Entries temp = Entry[n];
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new ID", "ME3 Explorer", temp.id.ToString(), 0, 0);
            int x;
            if (!int.TryParse(result, out x))
            {
                MessageBox.Show("Invalid Entry");
                return;
            }
            temp.id = x;
            Entry[n] = temp;
            SortEntries2();
            RefreshList();
        }

        public string lastsearch = "";

        private void forIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter string to search for", "ME3 Explorer", lastsearch, 0, 0);
            if (result == "")
                return;
            lastsearch = result.ToLower();
            for (int i = n + 1; i < listBox1.Items.Count; i++)
            {
                Entries temp = Entry[i];
                string s = CondBool(temp.data);
                if (s.ToLower().Contains(lastsearch))
                {
                    listBox1.SelectedIndex = i;
                    return;
                }
            }
            MessageBox.Show("Not found!");
        }

        private void searchAgainToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (lastsearch == "")
                return;
            for (int i = n + 1; i < listBox1.Items.Count; i++)
            {
                Entries temp = Entry[i];
                string s = CondBool(temp.data);
                if (s.ToLower().Contains(lastsearch))
                {
                    listBox1.SelectedIndex = i;
                    return;
                }
            }
            MessageBox.Show("Not found!");
        }
    }
}
