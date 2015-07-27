using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;

namespace ME1Explorer
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

        public void StartDebug()
        {
            KFreonLib.Debugging.DebugOutput.StartDebugger("ME1Explorer Main Window");
        }

        private void openDebugWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StartDebug();
        }

        private void saveGameEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFormMaximized(new SaveGameEditor.SaveEditor());
        }

        public void OpenFormMaximized(Form f)
        {
            f.MdiParent = this;
            f.Show();
            f.WindowState = FormWindowState.Maximized;
        }

        private void saveGameOperatorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFormMaximized(new SaveGameOperator.SaveGameOperator());
        }

        private void Form_Closing(object sender, FormClosingEventArgs e)
        {
            Application.Exit();
        }
    }
}
