using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer.AutoTOC
{
    public partial class AutoTOC : Form
    {
        public AutoTOC()
        {
            InitializeComponent();
            //FemShep's Mod Manager 4.1 automator for AutoTOC.
            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Length == 3)
            {
                //try
                //{
                string cmdCommand = arguments[1];
                if (cmdCommand.Equals("-autotoc", StringComparison.Ordinal))
                {

                    String tocfile = arguments[2];
                    prepareToCreateTOC(tocfile);
                    Environment.Exit(0);
                    Application.Exit();
                }
            }
        }

        private void createTOCForBasefolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "PCConsoleTOC.bin|PCConsoleTOC.bin";
            d.FileName = "PCConsoleTOC.bin";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = Path.GetDirectoryName(d.FileName) + "\\";
                prepareToCreateTOC(path);
            }
        }

        private void prepareToCreateTOC(string consoletocFile)
        {
            if (!consoletocFile.EndsWith("\\"))
            {
                consoletocFile = consoletocFile + "\\";
            }
            List<string> files = GetFiles(consoletocFile);
            if (files.Count != 0)
            {
                string t = files[0];
                int n = t.IndexOf("DLC_");
                if (n > 0)
                {
                    for (int i = 0; i < files.Count; i++)
                        files[i] = files[i].Substring(n);
                    string t2 = files[0];
                    n = t2.IndexOf("\\");
                    for (int i = 0; i < files.Count; i++)
                        files[i] = files[i].Substring(n + 1);
                }
                else
                {
                    n = t.IndexOf("BIOGame");
                    if (n > 0)
                    {
                        for (int i = 0; i < files.Count; i++)
                            files[i] = files[i].Substring(n);
                    }
                }
            }
            rtb1.Text = "Getting file list...\n";
            rtb1.Visible = false;
            foreach (string s in files)
                rtb1.AppendText(s + "\n");
            rtb1.Visible = true;
            rtb1.AppendText("Creating TOC...\n");
            string pathbase;
            string t3 = files[0];
            int n2 = t3.IndexOf("BIOGame");
            if (n2 >= 0)
            {
                pathbase = Path.GetDirectoryName(Path.GetDirectoryName(consoletocFile)) + "\\";
            }
            else
            {
                pathbase = consoletocFile;
            }
            CreateTOC(pathbase, consoletocFile + "PCConsoleTOC.bin",files.ToArray());
            rtb1.AppendText("Done.\n");
        }

        public void CreateTOC(string basepath, string tocFile, string[] files)
        {
            BitConverter.IsLittleEndian = true;
            FileStream fs = new FileStream(tocFile, FileMode.Create, FileAccess.Write);
            fs.Write(BitConverter.GetBytes((int)0x3AB70C13), 0, 4);
            fs.Write(BitConverter.GetBytes((int)0x0), 0, 4);
            fs.Write(BitConverter.GetBytes((int)0x1), 0, 4);
            fs.Write(BitConverter.GetBytes((int)0x8), 0, 4);
            fs.Write(BitConverter.GetBytes((int)files.Length), 0, 4);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (i == files.Length - 1)//Entry Size
                    fs.Write(new byte[2], 0, 2);
                else
                    fs.Write(BitConverter.GetBytes((ushort)(0x1D + file.Length)), 0, 2);
                fs.Write(BitConverter.GetBytes((ushort)0), 0, 2);//Flags
                if (Path.GetFileName(file).ToLower() != "pcconsoletoc.bin")
                {
                    FileStream fs2 = new FileStream(basepath + file, FileMode.Open, FileAccess.Read);
                    fs.Write(BitConverter.GetBytes((int)fs2.Length), 0, 4);//Filesize
                    fs2.Close();
                }
                else
                {
                    fs.Write(BitConverter.GetBytes((int)0), 0, 4);//Filesize
                }
                fs.Write(BitConverter.GetBytes((int)0x0), 0, 4);//SHA1
                fs.Write(BitConverter.GetBytes((int)0x0), 0, 4);
                fs.Write(BitConverter.GetBytes((int)0x0), 0, 4);
                fs.Write(BitConverter.GetBytes((int)0x0), 0, 4);
                fs.Write(BitConverter.GetBytes((int)0x0), 0, 4);
                foreach (char c in file)
                    fs.WriteByte((byte)c);
                fs.WriteByte(0);
            }
            fs.Close();
        }

        public List<string> GetFiles(string basefolder)
        {
            List<string> res = new List<string>();
            string test = Path.GetFileName(Path.GetDirectoryName(basefolder));
            string[] files = DirFiles(basefolder);
            res.AddRange(files);
            DirectoryInfo folder = new DirectoryInfo(basefolder);
            DirectoryInfo[] folders = folder.GetDirectories();
            if (folders.Length != 0)
                if (test != "BIOGame")
                    foreach (DirectoryInfo f in folders)
                        res.AddRange(GetFiles(basefolder + f.Name + "\\"));
                else
                    foreach (DirectoryInfo f in folders)
                        if (f.Name == "CookedPCConsole" || /*f.Name == "DLC" ||*/ f.Name == "Movies" || f.Name == "Splash")
                            res.AddRange(GetFiles(basefolder + f.Name + "\\"));
            return res;
        }

        public string[] Pattern = { "*.pcc", "*.afc", "*.bik", "*.bin", "*.tlk", "*.txt", "*.cnd", "*.upk", "*.tfc" };

        public string[] DirFiles(string path)
        {
            List<string> res = new List<string>();
            foreach (string s in Pattern)
                res.AddRange(Directory.GetFiles(path, s));
            return res.ToArray();
        }
    }
}