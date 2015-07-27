using KFreonLib.Debugging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer.LevelExplorer
{
    public partial class ME3LevelExplorer : Form
    {
        public ME3LevelExplorer()
        {
            InitializeComponent();
        }

        private void levelDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Levelbase b = new Levelbase();
            b.MdiParent = this;
            b.Show();
            b.WindowState = FormWindowState.Maximized;
        }

        private void levelEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LevelEditor.Leveleditor ed = new LevelEditor.Leveleditor();
            ed.MdiParent = this;
            ed.Show();
            ed.WindowState = FormWindowState.Maximized;
        }

        private void openDebugWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DebugOutput.StartDebugger("ME3 Level Explorer");
        }
    }
}
