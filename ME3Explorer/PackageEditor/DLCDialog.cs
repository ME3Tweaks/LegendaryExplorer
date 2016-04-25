using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;

namespace ME3Explorer
{
    public partial class DLCDialog : Form
    {
        public object Result = null;
        public string MyPath;
        public DLCPackage dlc;
        public List<int> Objects;
        public DLCDialog()
        {
            InitializeComponent();
        }

        public void Init(string DLCpath)
        {
            MyPath = DLCpath;
            dlc = new DLCPackage(DLCpath);
            int count = 0;
            Objects = new List<int>();
            listBox1.Items.Clear();
            foreach (DLCPackage.FileEntryStruct f in dlc.Files)
            {
                if (f.FileName.Trim().ToLower().EndsWith(".pcc"))
                {
                    listBox1.Items.Add(f.FileName);
                    Objects.Add(count);
                }
                count++;
            }
            bool run = true;
            while (run)
            {
                run = false;
                for (int i = 0; i < Objects.Count - 1; i++)
                    if (listBox1.Items[i].ToString().CompareTo(listBox1.Items[i + 1]) > 0)
                    {
                        string s = listBox1.Items[i].ToString();
                        listBox1.Items[i] = listBox1.Items[i + 1].ToString();
                        listBox1.Items[i + 1] = s;
                        int x = Objects[i];
                        Objects[i] = Objects[i + 1];
                        Objects[i + 1] = x;
                        run = true;
                    }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Result = listBox1.SelectedIndex;
        }
    }
}
