using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ME3Explorer.Unreal;

namespace ME3Explorer
{
    public partial class InterpreterHost : Form
    {
        public InterpreterHost(PCCObject pcc, int index)
        {
            InitializeComponent();
            string className = pcc.Exports[index].ClassName;
            interpreter1.Pcc = pcc;
            interpreter1.Index = index;
            interpreter1.InitInterpreter();
            toolStripStatusLabel1.Text = "Class: " + className + ", Export Index: " + index;
            toolStripStatusLabel2.Text = "@" + Path.GetFileName(pcc.pccFileName);
            interpreter1.hb1.ReadOnly = true;
            interpreter1.saveHexButton.Visible = false;
            interpreter1.exportButton.Visible = true;
        }

        public new void Show()
        {
            base.Show();
            interpreter1.treeView1.Nodes[0].Expand();
        }
    }
}
