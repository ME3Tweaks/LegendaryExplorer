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

        private void MainWindow_Load(object sender, EventArgs e)
        {
            taskbar.Strip = toolStrip1;
            Unreal.UnrealObjectInfo.loadfromJSON();
        }

        private void Form_Closing(object sender, FormClosingEventArgs e)
        {
            Application.Exit();
        }

        private void openDebugWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            KFreonLib.Debugging.DebugOutput.StartDebugger("ME1Explorer Main Window");
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

        private void pccEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PCCEditor p = new PCCEditor();
            taskbar.AddTool(p, Properties.Resources.package_editor_64x64);
            //taskbar.AddTool doesn't call the override in PCCEditor.
            p.Show();
        }

        private void saveGameEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            taskbar.AddTool(new SaveGameEditor.SaveEditor(), Properties.Resources.save_gameeditor_64x64);
        }

        private void saveGameOperatorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            taskbar.AddTool(new SaveGameOperator.SaveGameOperator(), Properties.Resources.save_gameoperator_64x64);
        }
        
        private void sequenceEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            taskbar.AddTool(new SequenceEditor(), Properties.Resources.sequence_editor_64x64);
        }

        private void dialogEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            taskbar.AddTool(new DialogEditor(), Properties.Resources.dialogue_editor_64x64);
        }

        private void tLKEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            taskbar.AddTool(new TlkManager(true), Properties.Resources.TLK_editor_64x64);
        }
    }
}
