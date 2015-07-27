using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;

namespace KFreonLib.Debugging
{
    public partial class DebugWindow : Form
    {
        public DebugWindow()
        {
            InitializeComponent();
        }

        private void DebugWindow_Load(object sender, EventArgs e)
        {
            DebugOutput.SetBox(rtb1);
        }

        private void saveLogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.txt|*.txt";
            string text = rtb1.Text;

            Thread thrd = new Thread(new ThreadStart(() =>
            {
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        
                        File.WriteAllText(d.FileName, text);
                        MessageBox.Show("Saved contents to " + d.FileName);
                    }
                    catch (Exception exp)
                    {
                        MessageBox.Show("Error occured while saving: " + exp.Message);
                    }
                }
            }));
            thrd.SetApartmentState(ApartmentState.STA);
            thrd.Start();
        }

        private void Form_Closing(object sender, FormClosingEventArgs e)
        {
            DebugOutput.NullifyRTB();
        }
    }
}
