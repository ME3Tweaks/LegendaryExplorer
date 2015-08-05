using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3VanillaMaker
{
    public partial class Form1 : Form
    {
        public string GamePath, BackupPath;
        public long GameSize, BackupSize, GlobalFileSize, GameFileCount, BackupFileCount, GlobalFileCount;
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DialogResult result = FolderDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                GamePath = FolderDialog.SelectedPath;
                DisplayRefresh(true, false);
            }
        }

        public void DisplayRefresh(bool GameOnly = false, bool BackupOnly = false)
        {
            if (Directory.Exists(GamePath) && !BackupOnly)
            {
                treeView1.Nodes.Clear();                
                GlobalFileSize = 0;
                GlobalFileCount = 0;
                treeView1.Nodes.Add(GenerateTreeForFolder(GamePath));
                textBox1.Text = GamePath;
                GameSize = GlobalFileSize;
                GameFileCount = GlobalFileCount;
                label1.Text = "Game (" + GameSize + " bytes)(" + GameFileCount + " files)";
            }
            if (Directory.Exists(BackupPath) && !GameOnly)
            {
                treeView2.Nodes.Clear();
                GlobalFileSize = 0;
                GlobalFileCount = 0;
                treeView2.Nodes.Add(GenerateTreeForFolder(BackupPath));
                textBox2.Text = BackupPath;
                BackupSize = GlobalFileSize;
                BackupFileCount = GlobalFileCount;
                label2.Text = "Backup (" + BackupSize + " bytes)(" + BackupFileCount + " files)";
            }
        }

        public TreeNode GenerateTreeForFolder(string path)
        {
            string p = path;
            if (p.EndsWith("\\"))
                p = p.Substring(0, p.Length - 1);
            string pp = Path.GetDirectoryName(p);
            if (pp.Length == 3) //root
                pp = pp.Substring(0, 2);
            TreeNode res = new TreeNode(p.Substring(pp.Length + 1));
            string[] dirs = Directory.GetDirectories(p);
            string[] files = Directory.GetFiles(p);
            foreach (string dir in dirs)
                res.Nodes.Add(GenerateTreeForFolder(dir));
            long FileSize;
            foreach (string file in files)
            {
                GlobalFileSize += FileSize = new FileInfo(file).Length;
                GlobalFileCount++;
                res.Nodes.Add(Path.GetFileName(file) + "(" + FileSize + " bytes)");
            }
            return res;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            DialogResult result = FolderDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                BackupPath = FolderDialog.SelectedPath;
                DisplayRefresh(false, true);
            }
        }

        public CopyForm CForm;

        public void CopyAllFiles(string pathIn, string pathOut)
        {
            string[] dirs = Directory.GetDirectories(pathIn);
            string[] files = Directory.GetFiles(pathIn);
            foreach (string file in files)
            {
                if (CForm.Exit)
                    return;
                CForm.pb1.Value = (int)(GlobalFileCount & 0x7FFFFFFF);
                CForm.label1.Text = "From : " + file;
                string fileout =pathOut+ '\\' + Path.GetFileName(file);
                CForm.label2.Text = "To : " + fileout;
                if (File.Exists(fileout))
                    CForm.label2.ForeColor = Color.Red;
                else
                    CForm.label2.ForeColor = Color.Black;
                Application.DoEvents();
                File.Copy(file, fileout, true);
                GlobalFileCount++;
                while(CForm.Pause && !CForm.Exit)
                    Application.DoEvents();                
            }
            foreach (string dir in dirs)
            {
                string dirname = Path.GetFileName(dir);
                string dirnameout = pathOut + '\\' + dirname;
                if (!Directory.Exists(dirnameout))
                    Directory.CreateDirectory(dirnameout);
                CopyAllFiles(dir, dirnameout);
            }
        }

        private void createFullBackupToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            if (GamePath == null || GamePath.Length == 0 || !Directory.Exists(GamePath) ||
                BackupPath == null || BackupPath.Length == 0 || !Directory.Exists(BackupPath))
            {
                MessageBox.Show("Please select a Game and Backup location first!");
                return;
            }
            CForm = new CopyForm();
            GlobalFileCount = 0;
            CForm.pb1.Maximum = (int)(GameFileCount & 0x7FFFFFFF);
            CForm.Copy = new CopyForm.CopyMethod(Dummy1);
            CForm.Text = "Backing up Game...";
            CForm.ShowDialog();
            DisplayRefresh();
        }

        public void Dummy1()
        {
            CopyAllFiles(GamePath, BackupPath);
            CForm.Close();
        }

        public void Dummy2()
        {
            CopyAllFiles(BackupPath, GamePath);
            CForm.Close();
        }

        private void restoreAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (GamePath == null || GamePath.Length == 0 || !Directory.Exists(GamePath) ||
                BackupPath == null || BackupPath.Length == 0 || !Directory.Exists(BackupPath))
            {
                MessageBox.Show("Please select a Game and Backup location first!");
                return;
            }
            CForm = new CopyForm();
            GlobalFileCount = 0;
            CForm.pb1.Maximum = (int)(BackupFileCount & 0x7FFFFFFF);
            CForm.Copy = new CopyForm.CopyMethod(Dummy2);
            CForm.Text = "Backing up Game...";
            CForm.ShowDialog();
            DisplayRefresh();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            textBox1.Text = GamePath = AmaroK86.MassEffect3.ME3Paths.gamePath;
            DisplayRefresh(true, false);
        }
    }
}
