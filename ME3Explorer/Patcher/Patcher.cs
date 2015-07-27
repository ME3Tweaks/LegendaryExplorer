using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Tools;

namespace ME3Explorer.Patcher
{
    public partial class Patcher : Form
    {
        public Patcher()
        {
            InitializeComponent();
        }

        private void createPatchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            rtb1.Text += "\nCreating patch\n" +  "==============\n\nOriginal File:";
            string fileorg = "";
            string filemod = "";
            string filepatch = "";
            OpenFileDialog d = new OpenFileDialog();
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                fileorg = d.FileName;
            if (fileorg == "")
            {
                rtb1.Text += "\nCanceled by user";
                return;
            }
            rtb1.Text += fileorg + "\nModefied File:";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                filemod = d.FileName;
            if (filemod == "")
            {
                rtb1.Text += "\nCanceled by user";
                return;
            }
            rtb1.Text += filemod + "\nPatch File:";
            SaveFileDialog d2 = new SaveFileDialog();
            d2.Filter = "*.patch|*.patch";
            if (d2.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                filepatch = d2.FileName;
            if (filepatch == "")
            {
                rtb1.Text += "\nCanceled by user";
                return;
            }
            rtb1.Text += filepatch + "\n\nCreating MD5...\n";
            string hashorg = MD5Hash.FromFile(fileorg);
            rtb1.Text += "Org File: " + hashorg + "\n";
            string hashmod = MD5Hash.FromFile(filemod);
            rtb1.Text += "Mod File: " + hashmod + "\n";
            rtb1.Text += "\nCreating Patch...\n";
            rtb1.Refresh();
            Application.DoEvents();
            if (File.Exists(loc + "\\exec\\out.patch"))
                File.Delete(loc + "\\exec\\out.patch");

           // RunShell(loc + "\\exec\\bsdiff.exe", "\"" + fileorg + "\" \"" + filemod + "\" out.patch");
            PatcherTool.CreatePatch(fileorg, filemod, "out.patch");


            if (File.Exists(loc + "\\exec\\out.patch"))
            {
                rtb1.Text += "\nCombining Patch with md5...\n";
                FileStream fs = new FileStream(filepatch, FileMode.Create, FileAccess.Write);
                for (int i = 0; i < 32; i++)
                    fs.WriteByte((byte)hashorg[i]);
                for (int i = 0; i < 32; i++)
                    fs.WriteByte((byte)hashmod[i]);
                FileStream fs2 = new FileStream(loc + "\\exec\\out.patch", FileMode.Open, FileAccess.Read);
                for (int i = 0; i < fs2.Length; i++)
                    fs.WriteByte((byte)fs2.ReadByte());
                fs2.Close();
                fs.Close();
                rtb1.Text += "\nDone.\n";                
                File.Delete(loc + "\\exec\\out.patch");
            }
            else
            {
                rtb1.Text += "\nCanceled by user";
                return;
            }
            
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

        private void applyPatchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            rtb1.Text += "\nApplying patch\n" + "==============\n\nOriginal File:";
            string fileorg = "";
            string filemod = "";
            string filepatch = "";
            OpenFileDialog d = new OpenFileDialog();
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                fileorg = d.FileName;
            if (fileorg == "")
            {
                rtb1.Text += "\nCanceled by user";
                return;
            }
            rtb1.Text += fileorg + "\nOutput File:";
            SaveFileDialog d2 = new SaveFileDialog();
            if (d2.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                filemod = d2.FileName;
            if (filemod == "")
            {
                rtb1.Text += "\nCanceled by user";
                return;
            }
            rtb1.Text += filemod + "\nPatch File:";
            d.Filter = "*.patch|*.patch";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                filepatch = d.FileName;
            if (filepatch == "")
            {
                rtb1.Text += "\nCanceled by user";
                return;
            }
            rtb1.Text += filepatch + "\n\nReading MD5...\n";
            if (File.Exists(filepatch))
            {
                FileStream fs = new FileStream(filepatch, FileMode.Open, FileAccess.Read);
                string hashorg = "";
                string hashmod = "";
                for (int i = 0; i < 32; i++)
                    hashorg += (char)fs.ReadByte();
                for (int i = 0; i < 32; i++)
                    hashmod += (char)fs.ReadByte();
                rtb1.Text += "Checksum Org File: " + hashorg + "\n";
                rtb1.Text += "Checksum Mod File: " + hashmod + "\n";
                string s = MD5Hash.FromFile(fileorg);
                rtb1.Text += "Actual Org File: " + s;
                if (s == hashorg)
                    rtb1.Text += "...OK\n";
                else
                {
                    rtb1.Text += "...FAIL\n Exiting.";
                    return;
                }
                rtb1.Text += "\nLoading Patch...";
                FileStream fs2 = new FileStream(loc + "\\exec\\out.patch", FileMode.Create, FileAccess.Write);
                for (int i = 64; i < fs.Length; i++)
                    fs2.WriteByte((byte)fs.ReadByte());
                fs2.Close();
                fs.Close();
                rtb1.Text += "\nApplying Path...\n";
                rtb1.Refresh();
                Application.DoEvents();
                //RunShell(loc + "\\exec\\bspatch", "\"" + fileorg + "\" \"" + filemod + "\" out.patch");
                PatcherTool.ApplyPatch(loc + "\\exec\\",fileorg, filemod, "out.patch");
                s = MD5Hash.FromFile(filemod);
                rtb1.Text += "\nFinished. Checking...\nActual Mod File: " + s;
                if (s == hashmod)
                    rtb1.Text += "...OK\n";
                else
                {
                    rtb1.Text += "...FAIL\n Exiting.";
                    return;
                }
                rtb1.Text += "\nDone.\n";
                File.Delete(loc + "\\exec\\out.patch");
            }
            else
            {
                rtb1.Text += "\nCanceled by user";
                return;
            }
        }
    }
}
