using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using KFreonLib.MEDirectories;
using System.Threading.Tasks;

namespace ME3Explorer
{
    public partial class AutoTOC : Form
    {
        public AutoTOC()
        {
            InitializeComponent();
        }

        public static void prepareToCreateTOC(string consoletocFile, RichTextBox rtb = null)
        {
            if (!consoletocFile.EndsWith("\\"))
            {
                consoletocFile = consoletocFile + "\\";
            }
            List<string> files = GetFiles(consoletocFile);
            if (files.Count != 0)
            {
                rtb?.AppendText($"Creating TOC in {consoletocFile}\n");
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
                CreateTOC(pathbase, consoletocFile + "PCConsoleTOC.bin", files.ToArray());
                rtb?.AppendText("Done.\n");
            }
        }

        static void CreateTOC(string basepath, string tocFile, string[] files)
        {
            BitConverter.IsLittleEndian = true;
            byte[] SHA1 = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            using (FileStream fs = new FileStream(tocFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                fs.Write(BitConverter.GetBytes(0x3AB70C13), 0, 4);
                fs.Write(BitConverter.GetBytes(0x0), 0, 4);
                fs.Write(BitConverter.GetBytes(0x1), 0, 4);
                fs.Write(BitConverter.GetBytes(0x8), 0, 4);
                fs.Write(BitConverter.GetBytes(files.Length), 0, 4);
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
                        fs.Write(BitConverter.GetBytes((int)(new FileInfo(basepath + file)).Length), 0, 4);//Filesize
                    }
                    else
                    {
                        fs.Write(BitConverter.GetBytes(0), 0, 4);//Filesize
                    }
                    fs.Write(SHA1, 0, 20);
                    foreach (char c in file)
                        fs.WriteByte((byte)c);
                    fs.WriteByte(0);
                } 
            }
        }

        static List<string> GetFiles(string basefolder)
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

        static string[] DirFiles(string path)
        {
            string[] Pattern = { "*.pcc", "*.afc", "*.bik", "*.bin", "*.tlk", "*.txt", "*.cnd", "*.upk", "*.tfc" };
            List<string> res = new List<string>();
            foreach (string s in Pattern)
                res.AddRange(Directory.GetFiles(path, s));
            return res.ToArray();
        }

        private void generateAllTOCsButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(ME3Directory.cookedPath))
            {
                MessageBox.Show("This functionality requires ME3 to be installed. Set its path at:\n Options > Set Custom Path > Mass Effect 3");
                return;
            }
            rtb1.Clear();
            GenerateAllTOCs(rtb1);
            rtb1.AppendText("***********************\n* All TOCs Generated! *\n***********************\n");
        }

        public static void GenerateAllTOCs(RichTextBox rtb = null)
        {
            List<DirectoryInfo> folders = (new DirectoryInfo(ME3Directory.DLCPath)).GetDirectories().ToList();
            folders.Add(new DirectoryInfo(ME3Directory.gamePath + @"BIOGame\"));
            //only use parallel execution if no ui interaction will be performed.
            if (rtb == null)
            {
                Parallel.ForEach(folders, d =>
                {
                    prepareToCreateTOC(d.FullName);
                }); 
            }
            else
            {
                foreach (DirectoryInfo d in folders)
                {
                    prepareToCreateTOC(d.FullName, rtb);
                }
            }
        }

        private void createTOCButton_Click(object sender, EventArgs e)
        {
            rtb1.Clear();
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "PCConsoleTOC.bin|PCConsoleTOC.bin";
            d.FileName = "PCConsoleTOC.bin";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = Path.GetDirectoryName(d.FileName) + "\\";
                prepareToCreateTOC(path, rtb1);
            }
        }
    }
}