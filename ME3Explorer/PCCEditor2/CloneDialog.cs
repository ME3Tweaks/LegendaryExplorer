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

namespace ME3Explorer
{
    public partial class CloneDialog : Form
    {
        public PCCObject pcc;
        public PCCEditor2 refForm;
        public int ObjectIndex;
        public List<int> Classes;

        public CloneDialog()
        {
            InitializeComponent();
        }

        private void CloneDialog_Resize(object sender, EventArgs e)
        {
            RefreshButtons();
        }

        public void RefreshButtons()
        {
            button1.Left = group1.Width - button1.Width - 20;
            button1.Top = group1.Height - button1.Height - 10;
            button2.Left = group2.Width - button2.Width - 20;
            button2.Top = group2.Height - button2.Height - 10;
            button3.Top = button2.Top;
            button3.Left = button2.Left - button3.Width - 10;
            button5.Left = group3.Width - button5.Width - 20;
            button5.Top = group3.Height - button5.Height - 10;
            button6.Top = button5.Top;
            button6.Left = button5.Left - button6.Width - 10;
            button7.Top = splitContainer1.Panel1.Height - button7.Height - 20;
            button7.Left= splitContainer1.Panel1.Width - button7.Width - 10;
            button8.Top = button7.Top;
            button8.Left = button7.Left - button8.Width - 10;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            group1.Visible = false;
            group2.Visible = true;
        }

        private void CloneDialog_Load(object sender, EventArgs e)
        {
            InitStuff();
            RefreshButtons();
        }

        public void InitStuff()
        {
            textBox1.Text = pcc.Exports[ObjectIndex].ObjectName;
            comboBox1.Items.Clear();
            foreach (string name in pcc.Names)
                comboBox1.Items.Add(name);
            Classes = new List<int>();
            foreach (PCCObject.ExportEntry e in pcc.Exports)
            {
                bool found = false;
                foreach (int index in Classes)
                    if (e.idxClassName == index)
                        found = true;
                if (!found)
                    Classes.Add(e.idxClassName);
            }
            bool run = true;
            while (run)
            {
                run = false;
                for (int i = 0; i < Classes.Count - 1; i++)
                    if (pcc.getClassName(Classes[i]).CompareTo(pcc.getClassName(Classes[i + 1])) > 0)
                    {
                        int t = Classes[i];
                        Classes[i] = Classes[i + 1];
                        Classes[i + 1] = t;
                        run = true;
                    }
            }
            comboBox2.Items.Clear();
            foreach (int index in Classes)
                comboBox2.Items.Add(pcc.getClassName(index));
            treeView1.Nodes.Clear();
            TreeNode Root = new TreeNode("Root");
            Root.Name = "0";
            List<int> Links = new List<int>();
            for (int i = 0; i < pcc.Exports.Count; i++)
            {
                PCCObject.ExportEntry e = pcc.Exports[i];
                bool found = false;
                foreach (int index in Links)
                    if (index == e.idxLink)
                        found = true;
                if (!found)
                    Links.Add(e.idxLink);
            }
            Links.Sort();
            foreach (int index in Links)
                if (index != 0)
                {
                    if (index > 0)
                    {
                        PCCObject.ExportEntry e = pcc.Exports[index - 1];
                        string s = (index - 1).ToString() +  " : ";
                        if (e.PackageFullName != "Class" && e.PackageFullName != "Package")
                            s += e.PackageFullName + ".";
                        s += e.ObjectName;
                        TreeNode newlink = new TreeNode(s);
                        newlink.Name = index.ToString();
                        Root.Nodes.Add(newlink);
                    }
                    else
                    {
                        //PCCObject.ImportEntry e = pcc.Imports[index * -1 - 1];
                        //string s = "";
                        //if (e.PackageFullName != "Class" && e.PackageFullName != "Package")
                        //    s += e.PackageFullName + ".";
                        //s += e.ObjectName;
                        //TreeNode newlink = new TreeNode(s);
                        //newlink.Name = index.ToString();
                        //Root.Nodes.Add(newlink);
                    }

                }
            treeView1.Nodes.Add(Root);
            treeView1.ExpandAll();
        }

        private void CloneDialog_Paint(object sender, PaintEventArgs e)
        {
            RefreshButtons();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            group3.Visible = true;
            group2.Visible = false;
            RefreshButtons();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            group1.Visible = true;
            group2.Visible = false;
            RefreshButtons();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            group2.Visible = true;
            group3.Visible = false;
            RefreshButtons();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            group4.Visible = true;
            group3.Visible = false;
            RefreshButtons();
        }

        private void button7_Click(object sender, EventArgs e)
        {
            PCCObject.ExportEntry entry = new PCCObject.ExportEntry();
            PCCObject.ExportEntry source = pcc.Exports[ObjectIndex];
            byte[] Header = new byte[source.info.Length];                       //Header
            for (int i = 0; i < source.info.Length; i++)
                Header[i] = source.info[i];            
            entry.pccRef = pcc;
            entry.info = Header;
            if (radioButton2.Checked)                                           //Name from list
                if (comboBox1.SelectedIndex != -1)
                    entry.idxObjectName = comboBox1.SelectedIndex;
            if (radioButton3.Checked)                                           //Custom Name
                if(textBox1.Text != "")
                {
                    pcc.Names.Add(textBox1.Text);
                    entry.idxObjectName = pcc.Names.Count - 1;
                }
            if (radioButton4.Checked)                                           //Class
                if (comboBox2.SelectedIndex != -1)
                    entry.idxClassName = Classes[comboBox2.SelectedIndex];
            if (radioButton8.Checked)                                           //Link
                if (treeView1.SelectedNode != null)
                {
                    string link = treeView1.SelectedNode.Name;
                    entry.idxLink = Convert.ToInt32(link);
                }
            byte[] Data = new byte[0];
            if (radioButton6.Checked)                                           //Load data from file...
                if (File.Exists(textBox2.Text))
                {
                    FileStream fs = new FileStream(textBox2.Text, FileMode.Open, FileAccess.Read);
                    Data = new byte[fs.Length];                    
                    for (int i = 0; i < fs.Length; i++)
                        Data[i] = (byte)fs.ReadByte();
                    fs.Close();
                }
            if (radioButton7.Checked)                                           //...or keep old data
            {
                Data = new byte[source.Data.Length];
                for (int i = 0; i < source.Data.Length; i++)
                    Data[i] = source.Data[i];
            }
            entry.Data = Data;            
            int lastoffset = 0;
            foreach (PCCObject.ExportEntry ent in pcc.Exports)
                if (ent.DataOffset > lastoffset)
                    lastoffset = ent.DataOffset + ent.Data.Length;
            entry.DataOffset = lastoffset;
            //entry.DataSize = -1;//force update
            pcc.addExport(entry);
            foreach (PCCObject.ExportEntry ex in pcc.Exports) //silly update trick, but it works... wv
                ex.Data = ex.Data;
            refForm.RefreshView();
            this.Close();
        }

        private void button8_Click(object sender, EventArgs e)
        {
            group4.Visible = false;
            group3.Visible = true;
            RefreshButtons();
        }

        private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
        {
            RefreshButtons();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                textBox2.Text = d.FileName;
        }
    }
}
