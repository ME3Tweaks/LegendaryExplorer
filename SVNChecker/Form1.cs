using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SharpSvn;

namespace SVNChecker
{
    public partial class Form1 : Form
    {
        SvnClient client = new SvnClient();
        public List<string> FilesToLoad;
        public static readonly object _sync = new object();
        public Thread Downloader, thread;
        public WebClient webClient = new WebClient();
        public string URL;
        public int FC, FCd;
        public bool exit = false;
        public string OutFolder;// = Path.GetDirectoryName(Application.ExecutablePath) + "\\Download\\";
        public Form1()
        {
            InitializeComponent();
        }

        public void Work()
        {
            FilesToLoad = new List<string>();
            Downloader = new Thread(new ThreadStart(DownloadFiles));
            Downloader.Start();
            Print("Checking Revision...");
            SvnInfoEventArgs info;
            Uri repo = new Uri(URL);
            client.GetInfo(repo, out info);
            Print(string.Format("The last revision of {0} is {1}\nGetting Files...", repo, info.Revision));
            Collection<SvnListEventArgs> list = new Collection<SvnListEventArgs>();
            client.GetList(repo.AbsoluteUri, out list);
            FC = 0;
            FCd = 0;
            int count = 0;
            foreach (SvnListEventArgs entry in list)
            {
                if (count++ != 0)  //ignore .. basefolder
                    PrintAllFile(entry, repo.AbsoluteUri);
                if (exit)
                    return;
            }
            while (true)
            {
                lock (_sync)
                {
                    if (FilesToLoad.Count == 0)
                        break;
                    if (exit)
                        return;
                }
                Application.DoEvents();
            }
            
            Print("\nDone.");
            Print2("\n\nDone.");
            Status.Text = "Ready";
        }

        public void DownloadFiles()
        {
            string file,fileout;
            while (true)
            {
                if (!thread.IsAlive) //doublecheck to avoid race condition
                    return;
                Print2("Waiting for files...\n");
                while ((file = FetchFileToDownload()) == "" && thread.IsAlive) Application.DoEvents();
                if (!thread.IsAlive)
                    return;
                Print2("Downloading File " + file + " ... ");
                fileout = OutFolder;
                fileout += file.Substring(URL.Length + 1).Replace("/","\\");
                if (!Directory.Exists(Path.GetDirectoryName(fileout)))
                    Directory.CreateDirectory(Path.GetDirectoryName(fileout));
                file = file.Replace("#", "%23").Replace(" ", "%20");
                webClient.DownloadFile(file, fileout);
                Print2("Done.\n");
                FCd++;
                UpdateStatus();
                lock (_sync)
                {
                    FilesToLoad.RemoveAt(0);
                    if (exit)
                        return;
                }
                Application.DoEvents();
            }
        }

        public void UpdateStatus()
        {
            lock (_sync)
            {
                Status.Text = "Files/Downloaded yet : " + FC + " / " + FCd;
            }
        }

        public void Print2(string s)
        {
            try
            {
                rtb2.Invoke(new Action(() => { rtb2.AppendText(s); rtb2.SelectionStart = rtb2.Text.Length; rtb2.ScrollToCaret(); }));
            }
            catch(Exception)
            {
            }
        }

        public void AddFileToDownload(string path)
        {
            lock (_sync)
            {
                FilesToLoad.Add(path);
            }
        }

        public string FetchFileToDownload()
        {
            string res = "";
            lock (_sync)
            {
                if (FilesToLoad.Count != 0)
                    res = FilesToLoad[0];                    
            }
            return res;
        }

        private void PrintAllFile(SvnListEventArgs e, string basepath)
        {
            if (e.Entry.NodeKind == SvnNodeKind.File)
            {
                string path = (basepath + "/" + e.Path).Replace("%23", "#").Replace("%20", " ");
                Print(string.Format("{0} : {1}", e.Entry.Time, path));
                AddFileToDownload(path);
                FC++;
                UpdateStatus();
                lock (_sync)
                {
                    if (exit)
                        return;
                }
            }
            if (e.Entry.NodeKind == SvnNodeKind.Directory && e.Name != "")
            {
                Collection<SvnListEventArgs> list = new Collection<SvnListEventArgs>();
                string path = basepath + "/" + e.Name.Replace("#", "%23").Replace(" ", "%20");
                client.GetList(path, out list);
                int count = 0;
                foreach (SvnListEventArgs entry in list)
                {
                    if (count++ != 0) //ignore .. basefolder
                        PrintAllFile(entry, path);
                    lock (_sync)
                    {
                        if (exit)
                            return;
                    }
                }
            }
        }

        private void Print(string s)
        {
            try
            {
                rtb1.Invoke(new Action(() =>
                {
                    rtb1.AppendText(s + "\n");
                    rtb1.SelectionStart = rtb1.Text.Length;
                    rtb1.ScrollToCaret();
                }));
                Application.DoEvents();
            }
            catch(Exception) { };
        }

        private bool SetOutputPath()
        {
            FolderBrowserDialog d = new FolderBrowserDialog();
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                OutFolder = d.SelectedPath + "\\";
                return true;
            }
            return false;
        }

        private void fullToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!SetOutputPath())
                return;            
            URL = @"http://svn.code.sf.net/p/me3explorer/code";
            thread = new Thread(new ThreadStart(Work));
            thread.Start();
        }

        private void binaryOnlyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!SetOutputPath())
                return;
            URL = @"http://svn.code.sf.net/p/me3explorer/code/ME3Explorer/bin/Debug";
            thread = new Thread(new ThreadStart(Work));
            thread.Start();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            lock (_sync)
            {
                exit = true;
            }
        }
    }
}
