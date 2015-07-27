using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using ME3Explorer.Unreal;
using KFreonLib.Debugging;

namespace ME3Explorer.DLCTOCbinUpdater
{
    public partial class DLCTOCbinUpdater : Form
    {
        public class TransparentRichTextBox : RichTextBox
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
            static extern IntPtr LoadLibrary(string lpFileName);

            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams prams = base.CreateParams;
                    if (LoadLibrary("msftedit.dll") != IntPtr.Zero)
                    {
                        prams.ExStyle |= 0x020; // transparent 
                        prams.ClassName = "RICHEDIT50W";
                    }
                    return prams;
                }
            }
        }

        TransparentRichTextBox alpha;
        DLCPackage DLC;

        public DLCTOCbinUpdater()
        {
            InitializeComponent();
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        }

        private void DLCTOCbinUpdater_Activated(object sender, EventArgs e)
        {
            alpha = new TransparentRichTextBox();
            alpha.Dock = DockStyle.Fill;
            alpha.Font = new Font("Courir New", 12.0f, FontStyle.Bold);
            alpha.ForeColor = Color.FromArgb(64, 224, 0);
            alpha.ReadOnly = true;
            alpha.WordWrap = false;
            this.Controls.Add(alpha);
            DebugOutput.SetBox(alpha);
            DebugOutput.PrintLn("Ready.");
        }

        private void checkSFARToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.sfar|*.sfar";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BitConverter.IsLittleEndian = true;
                DebugOutput.Clear();
                DLC = new DLCPackage(d.FileName);
                DLC.UpdateTOCbin();
                DebugOutput.PrintLn("Done.");
                MessageBox.Show("Done.");
            }
        }

        private void checkAndRebuildSFARToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.sfar|*.sfar";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BitConverter.IsLittleEndian = true;
                DebugOutput.Clear();
                DLC = new DLCPackage(d.FileName);
                DLC.UpdateTOCbin(true);
                DebugOutput.PrintLn("Done.");
                MessageBox.Show("Done.");
            }
        }
    }
}
