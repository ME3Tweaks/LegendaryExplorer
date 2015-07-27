using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer
{
    public partial class DirectReplace : Form
    {
        public DirectReplace()
        {
            InitializeComponent();
        }

        private void DirectReplace_Resize(object sender, EventArgs e)
        {
            int h = statusStrip1.Height;
            button3.Top = this.Height - button3.Height - h - 40;
            button3.Left = this.Width - button3.Width - 10;
            button4.Top = button3.Top;
            button4.Left = button3.Left - button4.Width - 10;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                textBox1.Text = d.FileName;

        }

        private void button2_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.afc|*.afc";
            d.FileName = textBox2.Text;
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                textBox2.Text = d.FileName;
        }

        private void RunShell(string cmd, string args)
        {
            System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(cmd, args);
            procStartInfo.WorkingDirectory = Path.GetDirectoryName(cmd);
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.UseShellExecute = false;
            procStartInfo.CreateNoWindow = true;
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo = procStartInfo;
            proc.Start();
            proc.WaitForExit();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            status.Text = "Running...";            
            Application.DoEvents();
            //old version was with CL tool
            //string loc = Path.GetDirectoryName(Application.ExecutablePath);
            //RunShell(loc + "\\exec\\afc_fileswitch.exe", "\"" + textBox1.Text + "\" \"" + textBox2.Text + "\" " + textBox3.Text);
            if (!File.Exists(textBox1.Text) || !File.Exists(textBox2.Text))
            {
                status.Text = "Err: File not found.";
                return;
            }
            int offset = Convert.ToInt32(textBox3.Text);
            FileStream target = new FileStream(textBox2.Text, FileMode.Open, FileAccess.Write);
            FileStream input = new FileStream(textBox1.Text, FileMode.Open, FileAccess.Read);
            int len = (int)input.Length;
            int pbsplitter = len / 100;
            int count = 0;
            pb1.Minimum = 0;
            pb1.Maximum = len;
            if (offset > target.Length || offset < 0)
            {
                status.Text = "Err: Offset outside file";
                input.Close();
                target.Close();
                return;
            }
            target.Seek(offset, SeekOrigin.Begin);
            for (int i = 0; i < len; i++)
            {
                target.WriteByte((byte)input.ReadByte());
                count++;
                if (count >= pbsplitter)
                {
                    count = 0;
                    pb1.Value = i;
                    Application.DoEvents();
                }
            }
            pb1.Value = 0;
            input.Close();
            target.Close();
            status.Text = "Done.";

        }
    }
}
