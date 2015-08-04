using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Net;
using Newtonsoft.Json;
using VersionSwitcher.jsonobjects;
using VersionSwitcher.cellrenderers;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Diagnostics;

namespace VersionSwitcher
{
    public partial class VersionSwitcher : Form
    {
        List<ReleaseRenderer> releaseData;
        string assetURL;
        private string tmpFile;
        private BackgroundWorker zipWorker;

        public VersionSwitcher()
        {
            InitializeComponent();

            //Download latest releases info
            WebClient c = new WebClient();
            c.DownloadStringCompleted += new DownloadStringCompletedEventHandler(infoDownloaded);
            c.Headers.Add("user-agent", "Microsoft .NET 4.5/ME3Explorer Project, by FemShep (Mgamerz)");
            c.DownloadStringAsync(new Uri("https://api.github.com/repos/me3explorer/me3explorer/releases"));
        }

        private void infoDownloaded(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                String json= e.Result;
                List<Release> releases = JsonConvert.DeserializeObject<List<Release>>(json);
                progressLabel.Text = "Select a release to switch to.";
                releaseData = new List<ReleaseRenderer>();
                foreach (Release r in releases)
                {
                    System.Console.WriteLine(r.name);
                    if (r.name.Equals("Stable: r653"))
                    {
                        continue;
                    }
                    if (r.assets.Count > 0) {
                        releaseData.Add(new ReleaseRenderer() { Name = r.name, Value = r.assets[0].browser_download_url });
                    }
                }
                this.releasesComboBox.DataSource = releaseData;
                this.releasesComboBox.DisplayMember = "Name";
                this.releasesComboBox.ValueMember = "Value";
                this.releasesComboBox.Enabled = true;
                if (releaseData.Count > 0)
                {
                    downloadButton.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex.ToString());
            }
        }

        private void downloadButton_Click(object sender, EventArgs e)
        {
            int index = this.releasesComboBox.SelectedIndex;
            ReleaseRenderer r = releaseData[index];
            assetURL = r.Value;
            System.Console.WriteLine(assetURL);
            WebClient client = new WebClient();
            client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(client_DownloadProgressChanged);
            client.DownloadFileCompleted += new AsyncCompletedEventHandler(client_ReleaseDownloadCompleted);
            downloadButton.Enabled = false;
            releasesComboBox.Enabled = false;
            progressLabel.Text = "Starting download";
            string path = Path.GetTempPath();
            string fileName = Path.GetFileName(assetURL);
            tmpFile = Path.Combine(path, fileName);
            client.DownloadFileAsync(new Uri(assetURL), tmpFile);
        }

        private void client_ReleaseDownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            //Extract to temp
            String path = Path.GetTempPath();
            if (Directory.Exists(path + "ME3Explorer"))
            {
                Directory.Delete(path + "ME3Explorer",true);
            }
            
            progressBar1.Style = ProgressBarStyle.Marquee;
            progressLabel.Text = "Extracting...";
            zipWorker = new BackgroundWorker();
            zipWorker.DoWork += unzipToDirectory;
            zipWorker.RunWorkerCompleted += unzipFinished;
            zipWorker.RunWorkerAsync(path);
            
        }

        private void  unzipToDirectory(object sender, DoWorkEventArgs e)
        {
            string path = (string) e.Argument;
            ZipFile.ExtractToDirectory(tmpFile, path);
            if (File.Exists(tmpFile))
            {
                File.Delete(tmpFile);
            }
            e.Result = path;
        }

        private void unzipFinished(object sender, RunWorkerCompletedEventArgs e)
        {
            progressLabel.Text = "Preparing to switch versions";
            string unzipPath = (string)e.Result;
            string newSwitcherPath = unzipPath + "\\ME3Explorer\\VersionSwitcher.exe";
            if (File.Exists(newSwitcherPath))
            {
                //check version
            }
            else
            {
                //copy self into folder
                File.Copy(System.Reflection.Assembly.GetEntryAssembly().Location, newSwitcherPath);
                if (!File.Exists(unzipPath + "\\ME3Explorer\\Newtonsoft.Json.dll"))
                {
                    File.Copy("Newtonsoft.Json.dll", unzipPath + "\\ME3Explorer\\Newtonsoft.Json.dll");
                }
            }

            //Build update.bat
            bool isInPlaceUpgrade = false;
            //use method that handles UNC \\
            string executingPath = System.IO.Path.GetDirectoryName(new System.Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath);

            if (File.Exists(executingPath+"\\ME3Explorer.exe"))
            {
                isInPlaceUpgrade = true;
            }
            string batchString = "::ME3Explorer Version Switching Script\r\n" +
                "@echo off\r\n" +
                "echo Ending ME3Explorer and VersionSwitcher\r\n" +
                "taskkill /f /im ME3Explorer.exe /T\r\n" +
                "taskkill /f /im VersionSwitcher.exe /T\r\n";

            if (isInPlaceUpgrade)
            {
                batchString += "echo Deleting existing ME3Explorer directory\r\n" +
                "rmdir /S /Q " + executingPath;
            }

            batchString += "echo Moving new version to old directory\r\n" +
                "xcopy /I /Y /S " + unzipPath + "ME3Explorer ";
            if (isInPlaceUpgrade)
            {
                //put in folder above ME3Explorer (in-place upgrade)
                batchString += executingPath;
                batchString += "\r\n";
                batchString += "start \"\" "+executingPath + "\\ME3Explorer.exe -version-switch-from ";
                //get me3explorer.exe version
                FileVersionInfo existingInfo = FileVersionInfo.GetVersionInfo(executingPath + "\\ME3Explorer.exe");
                batchString += existingInfo.ProductBuildPart;
                batchString += "\r\n"; //run me3exp
            }
            else
            {
                //put ME3Explorer directory in executing folder (standalone)
                batchString += executingPath + "\\ME3Explorer";
                batchString += "\r\n";
                batchString += "start \"\" " + executingPath + "\\ME3Explorer\\ME3Explorer.exe\r\n"; //run me3exp
            }
            batchString += "::Remove extracted copy\r\n";
            batchString += "rmdir /S /Q " + unzipPath + "ME3Explorer\r\n";
            batchString += "call :deleteSelf&exit /b\r\n";
            batchString += ":deleteSelf\r\n";
            //batchString += "start /b \"\" cmd /c del \"%~f0\"&exit /b\r\n";
            batchString += "pause";
            File.WriteAllText(Path.GetTempPath() + "me3explorer_version_switch.cmd", batchString);
            System.Diagnostics.Process.Start(Path.GetTempPath() + "me3explorer_version_switch.cmd");
            Environment.Exit(0);

            }

        void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            double bytesIn = double.Parse(e.BytesReceived.ToString());
            double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
            double percentage = bytesIn / totalBytes * 100;

            progressBar1.Value = int.Parse(Math.Truncate(percentage).ToString());
            progressLabel.Text = Math.Truncate(bytesIn / 1024) + "KB/" + Math.Truncate(totalBytes / 1024) + "KB downloaded";
        }

        private void VersionSwitcher_Load(object sender, EventArgs e)
        {

            Debug.WriteLine("VersionSwitcher Interface has loaded");
            // Set up the ToolTip text for the Butotn and Checkbox.
            string executingPath = System.IO.Path.GetDirectoryName(new System.Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath);
            if (File.Exists(executingPath + "\\ME3Explorer.exe"))
            {
                versionSwitcherToolTip.SetToolTip(this.downloadButton, "In-Place Switch");
            }
            else
            {
                versionSwitcherToolTip.SetToolTip(this.downloadButton, "New Download to ME3Explorer/");
            }
        }
    }
}
