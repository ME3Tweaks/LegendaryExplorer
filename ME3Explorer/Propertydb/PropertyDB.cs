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
using ME3Explorer.Packages;
using ME3Explorer.Debugging;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace ME3Explorer.Propertydb
{
    public partial class PropertyDB : Form
    {
        public PropertyDB()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Property Database", new WeakReference(this));
            InitializeComponent();
            if (File.Exists(Properties.Settings.Default.PropertyDBPath))
            {
                loadDB(Properties.Settings.Default.PropertyDBPath);
            }
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
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            windowOpen = false;
        }

        public List<ClassDef> Classes;
        private bool windowOpen = true;

        public void RefreshLists()
        {
            listBox1.Items.Clear();
            listBox1.Visible = false;
            foreach (ClassDef c in Classes)
                listBox1.Items.Add(c.name);
            listBox1.Visible = true;
            UpdateStatus();
        }

        private void loadDB(string fileName)
        {
            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);

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
                ClassDef tmp = new ClassDef
                {
                    name = ReadString(fs),
                    props = new List<PropDef>()
                };
                int pcount = ReadInt(fs);
                for (int j = 0; j < pcount; j++)
                {
                    tmp.props.Add(new PropDef
                    {
                        name = ReadString(fs),
                        type = ReadInt(fs),
                        ffpath = ReadString(fs),
                        ffidx = ReadInt(fs)
                    });
                }
                Classes.Add(tmp);
            }
            fs.Close();
            Sort();
            RefreshLists();
            Properties.Settings.Default.PropertyDBPath = fileName;
        }       

        public void Sort()
        {
            bool run = true;
            while (run)
            {
                run = false;
                for (int i = 0; i < Classes.Count - 1; i++)
                {
                    if (Classes[i].name.CompareTo(Classes[i + 1].name) > 0) 
                    {
                        ClassDef tmp = Classes[i];
                        Classes[i] = Classes[i + 1];
                        Classes[i + 1] = tmp;
                        run = true;
                    }
                }
            }
            foreach (ClassDef c in Classes)
            {
                run = true;
                while (run)
                {
                    run = false;
                    for (int i = 0; i < c.props.Count - 1; i++)
                    {
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
        }

        public void UpdateStatus()
        {
            status.Text = "Classes : " + Classes.Count;
        }

        private void startScanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(ME3Directory.cookedPath))
            {
                MessageBox.Show("This functionality requires ME3 to be installed. Set its path at:\n Options > Set Custom Path > Mass Effect 3");
                return;
            }

            string path = ME3Directory.cookedPath;
            string[] files = Directory.GetFiles(path, "*.pcc");
            pb1.Maximum = files.Length;
            DebugOutput.Clear();
            Classes = new List<ClassDef>();
            for (int i = 0; i < files.Length && windowOpen; i++)
            {
                string file = files[i];
                DebugOutput.PrintLn(i.ToString("d4")
                                    + "\\"
                                    + (files.Length - 1)
                                    + " : Loading file \""
                                    + file
                                    + "\"");
                try
                {
                    using (ME3Package pcc = MEPackageHandler.OpenME3Package(file))
                    {
                        IReadOnlyList<IExportEntry> Exports = pcc.Exports;
                        pb2.Maximum = Exports.Count;
                        {
                            pb1.Value = i;
                            RefreshLists();
                            Application.DoEvents();
                        }
                        for (int j = 0; j < Exports.Count && windowOpen; j++)
                        {
                            if (j % 100 == 0) //refresh
                            {
                                pb1.Value = i;
                                pb2.Value = j;
                                Application.DoEvents();
                            }

                            int f = -1;
                            for (int k = 0; k < Classes.Count; k++)
                                if (Classes[k].name == Exports[j].ClassName)
                                {
                                    f = k;
                                    break;
                                }

                            if (f == -1) //New Class found, add
                            {
                                ClassDef tmp = new ClassDef
                                {
                                    name = Exports[j].ClassName,
                                    props = new List<PropDef>()
                                };
                                Classes.Add(tmp);
                                f = Classes.Count - 1;
                                UpdateStatus();
                            }

                            List<PropertyReader.Property> props = PropertyReader.getPropList(Exports[j]);
                            ClassDef res = Classes[f];
                            foreach (PropertyReader.Property p in props)
                            {
                                int f2 = -1;
                                string name = pcc.getNameEntry(p.Name);
                                for (int k = 0; k < res.props.Count; k++)
                                    if (res.props[k].name == name)
                                    {
                                        f2 = k;
                                        break;
                                    }

                                if (f2 == -1) //found new prop
                                {
                                    res.props.Add(new PropDef
                                    {
                                        name = name,
                                        type = (int)p.TypeVal,
                                        ffpath = Path.GetFileName(file),
                                        ffidx = j
                                    });
                                    //DebugOutput.PrintLn("\tin object #" 
                                    //                    + j 
                                    //                    + " class \"" 
                                    //                    + pcc.Exports[j].ClassName 
                                    //                    + "\" found property \"" 
                                    //                    + name 
                                    //                    + "\" type " 
                                    //                    + PropertyTypeToString(ptmp.type));
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error:\n" + ex.Message);
                    DebugOutput.PrintLn("Could not open file: " + file);
                    break;
                }
            }

            if (windowOpen)
            {
                Sort();
                RefreshLists();
                UpdateStatus();
                MessageBox.Show("Done.");
            }
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
            SaveFileDialog d = new SaveFileDialog
            {
                Filter = "*.bin|*.bin"
            };
            if (d.ShowDialog() == DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);

                WriteInt(Classes.Count, fs);
                for (int i = 0; i < Classes.Count; i++)
                {
                    WriteString(Classes[i].name, fs);
                    WriteInt(Classes[i].props.Count, fs);
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
                Properties.Settings.Default.PropertyDBPath = d.FileName;
            }
        }

        public void WriteInt(int i, FileStream fs)
        {
            byte[] buff = BitConverter.GetBytes(i);
            fs.Write(buff, 0, 4);
        }
        
        public void WriteString(string s, FileStream fs)
        {
            byte[] buff = BitConverter.GetBytes(s.Length);
            fs.Write(buff, 0, 4);
            foreach (char c in s)
                fs.WriteByte((byte)c);
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
            OpenFileDialog d = new OpenFileDialog
            {
                Filter = "*.bin|*.bin"
            };
            if (d.ShowDialog() == DialogResult.OK)
            {
                loadDB(d.FileName);
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
                    if (c.props.Count > 1)//None doesnt count!
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
    }
}
