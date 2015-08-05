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
using KFreonLib.Debugging;
using KFreonLib.MEDirectories;

namespace ME3Explorer.Propertydb
{
    public partial class PropertyDB : Form
    {
        public PropertyDB()
        {
            InitializeComponent();
        }

        public struct ClassDef
        {
            public string name;
            public List<PropDef> props;
        }

        public struct PropDef
        {
            public string name;
            public int type;
            public string ffpath;
            public int ffidx;
        }

        public List<ClassDef> Classes;

        public void RefreshLists()
        {
            listBox1.Items.Clear();
            listBox1.Visible = false;
            foreach (ClassDef c in Classes)
                listBox1.Items.Add(c.name);
            listBox1.Visible = true;
            UpdateStatus();
        }

        public void Sort()
        {
            bool run = true;
            while (run)
            {
                run = false;
                for (int i = 0; i < Classes.Count() - 1; i++)
                    if (Classes[i].name.CompareTo(Classes[i + 1].name) > 0) 
                    {
                        ClassDef tmp = Classes[i];
                        Classes[i] = Classes[i + 1];
                        Classes[i + 1] = tmp;
                        run = true;
                    }
            }
            foreach (ClassDef c in Classes)
            {
                run = true;
                while (run)
                {
                    run = false;
                    for (int i = 0; i < c.props.Count() - 1; i++)
                        if (c.props[i].type < c.props[i + 1].type)
                        {
                            PropDef tmp = c.props[i];
                            c.props[i] = c.props[i + 1];
                            c.props[i + 1] = tmp;
                            run = true;
                        }
                }
            }
        }

        public void UpdateStatus()
        {
            status.Text = "Classes : " + Classes.Count();
        }

        private void startScanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = ME3Directory.cookedPath;
            string[] files = Directory.GetFiles(path, "*.pcc");
            pb1.Maximum = files.Length;
            DebugOutput.Clear();
            Classes = new List<ClassDef>();
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                DebugOutput.PrintLn(i.ToString("d4")
                                    + "\\"
                                    + (files.Length - 1)
                                    + " : Loading file \""
                                    + file
                                    + "\"");
                PCCObject pcc = new PCCObject(file);
                pb2.Maximum = pcc.Exports.Count();
                {
                    pb1.Value = i;
                    RefreshLists();
                    Application.DoEvents();
                }
                for (int j = 0; j < pcc.Exports.Count(); j++)
                {
                    if (j % 100 == 0)//refresh
                    {
                        pb1.Value = i;
                        pb2.Value = j;
                        Application.DoEvents();
                    }
                    int f = -1;
                    for (int k = 0; k < Classes.Count(); k++)
                        if (Classes[k].name == pcc.Exports[j].ClassName)
                        {
                            f = k;
                            break;
                        }
                    if (f == -1)//New Class found, add
                    {
                        ClassDef tmp = new ClassDef();
                        tmp.name = pcc.Exports[j].ClassName;
                        tmp.props = new List<PropDef>();
                        Classes.Add(tmp);
                        f = Classes.Count() - 1;
                        UpdateStatus();
                    }
                    List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[j].Data);
                    ClassDef res = Classes[f];
                    foreach (PropertyReader.Property p in props)
                    {
                        int f2 = -1;
                        string name = pcc.getNameEntry(p.Name);
                        for (int k = 0; k < res.props.Count(); k++)
                            if (res.props[k].name == name)
                            {
                                f2 = k;
                                break;
                            }
                        if (f2 == -1) //found new prop
                        {
                            PropDef ptmp = new PropDef();
                            ptmp.name = name;
                            ptmp.type = (int)p.TypeVal;
                            ptmp.ffpath = Path.GetFileName(file);
                            ptmp.ffidx = j;
                            res.props.Add(ptmp);
                            //DebugOutput.PrintLn("\tin object #" 
                            //                    + j 
                            //                    + " class \"" 
                            //                    + pcc.Exports[j].ClassName 
                            //                    + "\" found property \"" 
                            //                    + name 
                            //                    + "\" type " 
                            //                    + PropertyReader.TypeToString(ptmp.type));
                        }
                    }
                }
            }            
            Sort();
            RefreshLists();
            UpdateStatus();
            MessageBox.Show("Done.");
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            ClassDef c = Classes[n];
            listBox2.Items.Clear();
            foreach (PropDef p in c.props)
                listBox2.Items.Add(p.name + " : " + PropertyReader.TypeToString(p.type) + " ff#" + p.ffidx + " ffFile: " + p.ffpath);
        }

        private void saveDBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.bin|*.bin";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                BitConverter.IsLittleEndian=true;
                WriteInt(Classes.Count(), fs);
                for (int i = 0; i < Classes.Count(); i++)
                {
                    WriteString(Classes[i].name, fs);
                    WriteInt(Classes[i].props.Count(), fs);
                    foreach (PropDef p in Classes[i].props)
                    {
                        WriteString(p.name, fs);
                        WriteInt(p.type, fs);
                        WriteString(p.ffpath, fs);
                        WriteInt(p.ffidx, fs);
                    }
                }
                fs.Close();
                MessageBox.Show("Done");
            }
        }

        public void WriteInt(int i, FileStream fs)
        {
            byte[] buff = BitConverter.GetBytes(i);
            fs.Write(buff, 0, 4);
        }
        
        public void WriteString(string s, FileStream fs)
        {
            byte[] buff = BitConverter.GetBytes((int)s.Length);
            fs.Write(buff, 0, 4);
            for (int i = 0; i < s.Length; i++)
                fs.WriteByte((byte)s[i]);
        }

        public int ReadInt(FileStream fs)
        {
            byte[] buff = new byte[4];
            for (int i = 0; i < 4; i++)
                buff[i] = (byte)fs.ReadByte();
            return BitConverter.ToInt32(buff, 0);
        }

        public string ReadString(FileStream fs)
        {
            byte[] buff = new byte[4];
            for (int i = 0; i < 4; i++)
                buff[i] = (byte)fs.ReadByte();
            int len = BitConverter.ToInt32(buff, 0);
            string s = "";
            for (int i = 0; i < len; i++)
                s += (char)fs.ReadByte();
            return s;
        }

        private void loadDBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.bin|*.bin";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Open, FileAccess.Read);
                BitConverter.IsLittleEndian = true;
                int count = ReadInt(fs);
                Classes = new List<ClassDef>();
                pb1.Maximum = count + 1;
                for (int i = 0; i < count; i++)
                {
                    if (i % 10 == 0)
                    {
                        pb1.Value = i;
                        Application.DoEvents();
                    }
                    ClassDef tmp = new ClassDef();
                    tmp.name = ReadString(fs);
                    int pcount = ReadInt(fs);
                    tmp.props = new List<PropDef>();
                    for (int j = 0; j < pcount; j++)
                    {
                        PropDef p = new PropDef();
                        p.name = ReadString(fs);
                        p.type = ReadInt(fs);
                        p.ffpath = ReadString(fs);
                        p.ffidx = ReadInt(fs);
                        tmp.props.Add(p);
                    }
                    Classes.Add(tmp);
                }
                fs.Close();
                Sort();
                RefreshLists();                
                MessageBox.Show("Done");
            }

        }

        private void statistiksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Classes == null)
                return;
            int countnodef = 0;
            int countndnotempt = 0;            
            foreach (ClassDef c in Classes)
            {
                if (!c.name.StartsWith("Default_"))
                {
                    countnodef++;
                    if (c.props.Count() > 1)//None doesnt count!
                        countndnotempt++;
                }
            }
            string s = "Classes found global: "
                     + Classes.Count
                     + "\nClasses found except \"Default_...\" : "
                     + countnodef
                     + "\nClasses found except \"Default_...\" with at least 1 property: "
                     + countndnotempt;
            MessageBox.Show(s);
        }

        private void warrantyVoiderMethodToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog d = new FolderBrowserDialog();
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string folder = d.SelectedPath + "\\";
                string loc = Path.GetDirectoryName(Application.ExecutablePath);
                string template = System.IO.File.ReadAllText(loc + "\\exec\\template.code");
                DebugOutput.Clear();
                DebugOutput.PrintLn("Generating SDK into folder: " + folder);
                foreach (ClassDef c in Classes)
                    if(c.props.Count > 1 && !c.name.StartsWith("Default_"))
                    {
                        string file = folder + c.name + ".cs";
                        DebugOutput.PrintLn(".\\" + c.name + ".cs generated...");
                        FileStream fs = new FileStream(file , FileMode.Create, FileAccess.Write);
                        string tmp = template;
                        tmp = tmp.Replace("**CLASSNAME**", c.name);
                        tmp = GenerateProps(tmp, c);
                        tmp = GenerateLoadSwitch(tmp, c);
                        tmp = GenerateTree(tmp, c);
                        for (int i = 0; i < tmp.Length; i++)
                            fs.WriteByte((byte)tmp[i]);
                        fs.Close();
                    }
                MessageBox.Show("Done.");
            }
        }

        public string GenerateProps(string template, ClassDef c)
        {
            string t = template;
            string rep = "";
            bool hadbool = false;
            bool hadint = false;
            bool hadobj = false;
            bool hadname = false;
            bool hadbyte = false;
            bool hadfloat = false;

            foreach (PropDef p in c.props)
            {
                string type = PropertyReader.TypeToString(p.type);
                switch (type)
                {
                    case "Bool Property":
                        if (!hadbool)
                        {
                            rep += "\t//Bool Properties\r\n\r\n";
                            hadbool = true;
                        }
                        rep += "\tpublic bool " + p.name + " = false;\r\n";
                        break;
                    case "Integer Property":
                        if (!hadint)
                        {
                            rep += "\t//Integer Properties\r\n\r\n";
                            hadint = true;
                        }
                        rep += "\tpublic int " + p.name + ";\r\n";
                        break;
                    case "Object Property":
                        if (!hadobj)
                        {
                            rep += "\t//Object Properties\r\n\r\n";
                            hadobj = true;
                        }
                        rep += "\tpublic int " + p.name + ";\r\n";
                        break;
                    case "Name Property":
                        if (!hadname)
                        {
                            rep += "\t//Name Properties\r\n\r\n";
                            hadname = true;
                        }
                        rep += "\tpublic int " + p.name + ";\r\n";
                        break;
                    case "Byte Property":
                        if (!hadbyte)
                        {
                            rep += "\t//Byte Properties\r\n\r\n";
                            hadbyte = true;
                        }
                        rep += "\tpublic int " + p.name + ";\r\n";
                        break;
                    case "Float Property":
                        if (!hadfloat)
                        {
                            rep += "\t//Float Properties\r\n\r\n";
                            hadfloat = true;
                        }
                        rep += "\tpublic float " + p.name + ";\r\n";
                        break;
                }
            }
            return t.Replace("**UNEALPROPS**", rep);
        }

        public string GenerateLoadSwitch(string template, ClassDef c)
        {
            string t = template;
            string rep = "";
            foreach (PropDef p in c.props)
            {               
                string type = PropertyReader.TypeToString(p.type);
                switch (type)
                {
                    case "Bool Property":
                        rep += "\t\t\t\t\tcase \"" + p.name + "\":\r\n";
                        rep += "\t\t\t\t\t\tif (p.raw[p.raw.Length - 1] == 1)\r\n";
                        rep += "\t\t\t\t\t\t" + p.name + " = true;\r\n";
                        rep += "\t\t\t\t\t\tbreak;\r\n";
                        break;
                    case "Integer Property":
                    case "Object Property":
                    case "Name Property":
                    case "Byte Property":
                        rep += "\t\t\t\t\tcase \"" + p.name + "\":\r\n";
                        rep += "\t\t\t\t\t\t" + p.name + " = p.Value.IntValue;\r\n";
                        rep += "\t\t\t\t\t\tbreak;\r\n";
                        break;
                    case "Float Property":
                        rep += "\t\t\t\t\tcase \"" + p.name + "\":\r\n";
                        rep += "\t\t\t\t\t\t" + p.name + " = BitConverter.ToSingle(p.raw, p.raw.Length - 4);\r\n";
                        rep += "\t\t\t\t\t\tbreak;\r\n";
                        break;
                }
            }
            return t.Replace("**LOADINGSWITCH**", rep);
        }

        public string GenerateTree(string template, ClassDef c)
        {
            string t = template;
            string rep = "";
            foreach (PropDef p in c.props)
            {
                string type = PropertyReader.TypeToString(p.type);
                switch (type)
                {
                    case "Object Property":
                    case "Integer Property":
                    case "Bool Property":
                    case "Float Property":
                        rep += "\t\t\tres.Nodes.Add(\"" + p.name + " : \" + " + p.name + ");\r\n";
                        break;
                    case "Name Property":
                    case "Byte Property":
                        rep += "\t\t\tres.Nodes.Add(\"" + p.name + " : \" + pcc.getNameEntry(" + p.name + "));\r\n";
                        break;
                }
            }
            return t.Replace("**TREEGEN**", rep);
        }
    }
}
