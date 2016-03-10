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
            taskbar.Strip = toolStrip1;
        }
        
        private void openDebugWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            KFreonLib.Debugging.DebugOutput.StartDebugger("ME2Explorer Main Window");
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            foreach (taskbar.task_list l in taskbar.tools)
            {
                if (l.tool != null && l.tool.IsDisposed)
                {
                    taskbar.Strip.Items.Remove(l.icon);
                    taskbar.tools.Remove(l);
                    break;
                }
                else if (l.wpfWindow != null && System.Windows.PresentationSource.FromVisual(l.wpfWindow) == null)
                {
                    taskbar.Strip.Items.Remove(l.icon);
                    taskbar.tools.Remove(l);
                    break;
                }
            }
        }

        private void pCCEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PCCEditor ed = new PCCEditor();
            taskbar.AddTool(ed, Properties.Resources.package_editor_64x64);
        }

        private void dLCCrackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            taskbar.AddTool(new DLC_Crack.GiveEntitlements(), Properties.Resources.dlc_crackME2_64x64);
        }

        private void sequenceEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            taskbar.AddTool(new SequenceEditor(), Properties.Resources.sequence_editor_64x64);
        }
    }
}
