using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;
using ME3Explorer.Coalesced_Editor;
using Gibbed.MassEffect3.FileFormats.Coalesced;

namespace ME3Explorer.Coalesced_Operator
{
    public partial class Keybinder : Form
    {
        public FileWrapper JSONfile;
        public string coalescedPATH, tempPath = System.IO.Path.GetTempPath() + "CoalTemp\\";
        char[] forSplitting = { '"' };
        public static Entry [] allBinds = { };
        public string[] bindKeyDefault;
        public string[] bindCommandDefault;
        public string[] bindKeyBase;
        public string[] bindCommandBase;


        public Keybinder()
        {
            InitializeComponent();
            fetchData();

        }
        public void fetchData()
        {
            JSONfile = opClass.LoadJSON(tempPath + "\\06_bioinput.json");
            allBinds = opClass.ReadAllEntries(JSONfile, "sfxgame.sfxgamemodedefault", "bindings");
            Application.DoEvents();

            TreeNode t1 = new TreeNode("Binded game keys");
            TreeNode defaultSection = new TreeNode("sfxgame.sfxgamemodedefault");
            int counter = 0;

            string[] temp1 = new string[allBinds.Count()];
            string[] temp2 = new string[allBinds.Count()];

            foreach (Entry key in allBinds)
            {
                string[] temp = key.Value.Split(forSplitting);
                temp1[counter] = temp[1];
                temp2[counter] = temp[3];
                defaultSection.Nodes.Add(counter.ToString(), temp1[counter] + " =  "+ temp2[counter]);
                counter++;
            }

            bindKeyDefault = temp1;
            bindCommandDefault = temp2;
            t1.Nodes.Add(defaultSection);
            

            TreeNode baseSection = new TreeNode("sfxgame.sfxgamemodebase");
            counter = 0;
            allBinds = opClass.ReadAllEntries(JSONfile, "sfxgame.sfxgamemodebase", "bindings");

            temp1 = new string[allBinds.Count()];
            temp2 = new string[allBinds.Count()];

            foreach (Entry key in allBinds)
            {
                string[] temp = key.Value.Split(forSplitting);
                temp1[counter] = temp[1];
                temp2[counter] = temp[3];
                baseSection.Nodes.Add(counter.ToString(), temp1[counter] + " =  " + temp2[counter]);
                counter++;
            }
            t1.Nodes.Add(baseSection);
            treeView1.Nodes.Add(t1);
            bindKeyBase = temp1;
            bindCommandBase = temp2;



        }

        private void label2_Click(object sender, EventArgs e)
        {
            
        }

        private void splitContainer1_Panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            textBox2.Text = treeView1.SelectedNode.Text;
            if (treeView1.SelectedNode.Text != "Binded game keys")
            {
                if (treeView1.SelectedNode.Parent.Text == "sfxgame.sfxgamemodedefault")
                    textBox1.Text = bindKeyDefault[treeView1.SelectedNode.Index];
                else if (treeView1.SelectedNode.Parent.Text == "sfxgame.sfxgamemodebase")
                    textBox1.Text = bindKeyBase[treeView1.SelectedNode.Index];
                else textBox1.Text = "";
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            opClass.WriteEntry(JSONfile, "sfxgame.sfxgamemodedefault", "bindings", treeView1.SelectedNode.Index, "( Name=\""+textBox1.Text+"\", Command=\""+textBox2.Text+"\" )");
            opClass.SaveJSON(JSONfile, tempPath + "\\06_bioinput.json");
            
        }

        private void Keybinder_KeyPress(object sender, KeyPressEventArgs e)
        {
           
          

        }
    }
}
