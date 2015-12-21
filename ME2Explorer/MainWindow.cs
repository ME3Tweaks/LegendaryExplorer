using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME2Explorer
{
    public partial class MainWindow : Form
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void pCCEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PCCEditor ed = new PCCEditor();
            ed.MdiParent = this;
            ed.Show();
            ed.WindowState = FormWindowState.Maximized;
        }

        private void dLCCrackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DLC_Crack.GiveEntitlements ent = new DLC_Crack.GiveEntitlements();
            ent.MdiParent = this;
            ent.WindowState = FormWindowState.Maximized;
            ent.Show();
        }

        public void StartDebug()
        {
            KFreonLib.Debugging.DebugOutput.StartDebugger("ME2Explorer Main Window");
        }

        private void openDebugWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StartDebug();
        }

        public void OpenFormMaximized(Form f)
        {
            f.MdiParent = this;
            f.Show();
            f.WindowState = FormWindowState.Maximized;
        }

        private void sequenceEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SequenceEditor sqed = new SequenceEditor();
            sqed.MdiParent = this;
            sqed.Show();
            sqed.WindowState = FormWindowState.Maximized;
        }
    }
}
