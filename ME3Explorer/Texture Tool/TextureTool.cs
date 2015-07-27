using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using ME2Explorer;
using System.IO;
using AmaroK86.ImageFormat;
using SaltTexture2D = KFreonLib.Textures.ME3SaltTexture2D;
using KFreonLib.MEDirectories;

namespace ME3Explorer.Texture_Tool
{
    public partial class TextureTool : Form
    {
        List<SaltTexture2D> textures;
        SaltTexture2D tex2D;
        string pathCooked;
        KFreonLib.PCCObjects.ME3PCCObject pcc;
        int numFiles;
        string exec;

        public TextureTool()
        {
            InitializeComponent();
            exec = Path.GetDirectoryName(Application.ExecutablePath) + "\\exec\\";
            LoadMe();
        }

        private void LoadMe()
        {
            pathCooked = ME3Directory.cookedPath;
            textures = new List<SaltTexture2D>();
            tex2D = new SaltTexture2D();
            tex2D.allPccs.Add(pathCooked + "BioD_Nor_100Cabin.pcc");
            tex2D.pccExpIdx = 2763;
            textures.Add(tex2D);
        }

        private void PropertiesPopup()
        {
            AmaroK86.ImageFormat.ImageSize imgSize = tex2D.imgList.Max(image => image.imgSize);

            string LOD;
            if (String.IsNullOrEmpty(tex2D.LODGroup))
            {
                LOD = "No LODGroup (Uses World)";
            }
            else
                LOD = tex2D.LODGroup;
            string arc;
            if (String.IsNullOrEmpty(tex2D.arcName))
            {
                arc = "\nPCC Stored";
            }
            else
                arc = "\nTexture Cache File: " + tex2D.arcName + ".tfc";

            string mesg = "Information about: " + tex2D.texName;
            mesg += "\nFormat: " + tex2D.texFormat;
            mesg += "\nWidth: " + imgSize.width + ", Height: " + imgSize.height;
            mesg += "\nLODGroup: " + LOD;
            mesg += arc;
            mesg += "\nOriginal Location: " + tex2D.allPccs[0];
            for (int i = 1; i < tex2D.allPccs.Count; i++)
                mesg += "\nAlso found in: " + tex2D.allPccs[i];

            MessageBox.Show(mesg);
        }

        private void SetStatus(string val)
        {
            toolStripStatusLabel1.Text = val;
            Application.DoEvents();
        }

        private void PreviewPopup()
        {
            Bitmap bmp = tex2D.GetImage(332);
            if (pictureBox1.Image != null)
                pictureBox1.Image.Dispose();

            pictureBox1.Image = bmp;



            /*ImageSize imgSize;
            string imgName = (tex2D.getFileFormat() == ".tga") ? "exec\\" + "preview00" : "exec\\" + "preview";
            imgName += tex2D.getFileFormat();

            if (File.Exists("exec\\preview00.tga"))
                File.Delete("exec\\preview00.tga");
            if (tex2D.imgList.Count != 1)
                imgSize = tex2D.imgList.Where(img => (img.imgSize.width <= 512 || img.imgSize.height <= 512) && img.offset != -1).Max(image => image.imgSize);
            else
                imgSize = tex2D.imgList.First().imgSize;
            tex2D.extractImage(imgSize.ToString(), pathCooked, imgName);

            if (tex2D.getFileFormat() == ".dds")
            {
                ExecuteCommandSync(@"exec\readdxt.exe " + imgName);
                File.Delete(imgName);
            }

            imgName = "exec\\preview00.tga";*/
            /*if (File.Exists(Path.GetFullPath(imgName)))
            {
                if (previewer == null || previewer.IsDisposed)
                    previewer = new TexPreview();
                if (previewer.Visible == true)
                    previewer.Visible = false;
                previewer.Width = (int)imgSize.width;
                previewer.Height = (int)imgSize.height;

                TargaImage ti = new TargaImage(imgName);
                previewer.pictureBox1.Image = ti.Image;
                previewer.pictureBox1.Refresh();
                previewer.Show();
            }
            else
                MessageBox.Show("Texture extraction error. No preview available", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);*/
        }

        public void ExecuteCommandSync(object command)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo procStartInfo =
                    new System.Diagnostics.ProcessStartInfo("cmd", "/c " + command);
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.UseShellExecute = false;
                procStartInfo.CreateNoWindow = true;
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.StartInfo = procStartInfo;
                proc.Start();
                string result = proc.StandardOutput.ReadToEnd();
                Console.WriteLine(result);
            }
            catch
            {
                // Log the exception
            }
        }

        private void loadTex(int texIndex)
        {
            pcc = new KFreonLib.PCCObjects.ME3PCCObject(textures[texIndex].allPccs[0]);
            tex2D = new SaltTexture2D(pcc, textures[texIndex].pccExpIdx, Path.GetDirectoryName(pathCooked));
            tex2D.pccExpIdx = textures[texIndex].pccExpIdx;
            tex2D.allPccs = textures[texIndex].allPccs;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            loadTex(0);
            OpenFileDialog open = new OpenFileDialog();
            open.Filter = "DDS Files|*.dds";
            open.Title = "Please select the file to mod with";
            if (open.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;
            try
            {
                ImageFile im = KFreonLib.Textures.Creation.LoadAKImageFile(null, open.FileName);
                tex2D.OneImageToRuleThemAll(im, pathCooked, im.imgData);

                SaveChanges();
            }
            catch (Exception exc)
            {
                MessageBox.Show("An error occurred: \n" + exc);
            }


        }

        private void button2_Click(object sender, EventArgs e)
        {
            loadTex(0);
            PreviewPopup();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            loadTex(0);
            PropertiesPopup();
        }

        private void SaveChanges()
        {
            numFiles = tex2D.allPccs.Count + 1;
            bgw1.RunWorkerAsync();
            pb1.Maximum = numFiles + 1;
        }

        private void bgw1_DoWork(object sender, DoWorkEventArgs e)
        {
            bgw1.ReportProgress(0);

            pcc = new KFreonLib.PCCObjects.ME3PCCObject(tex2D.allPccs[0]);
            KFreonLib.PCCObjects.ME3ExportEntry expEntry = pcc.Exports[tex2D.pccExpIdx];
            expEntry.SetData(tex2D.ToArray(expEntry.DataOffset, pcc));
            pcc.Exports[tex2D.pccExpIdx] = expEntry;
            pcc.saveToFile(tex2D.allPccs[0]);

            #region Old code
            /*
            File.Copy(pcc.pccFileName, exec + "temp.pcc", true);

            long test = (new FileInfo(exec + "temp.pcc")).Length;
            if (pcc.bCompressed)
            {
                Form2 decompress = new Form2();
                decompress.Decompress(exec + "temp.pcc");
                decompress.Close();
                if (test == (new FileInfo(exec + "temp.pcc")).Length)
                    pcc.saveToFile(exec + "temp.pcc");
            }
            pcc = new Texplorer.PCCObject(exec + "temp.pcc");
            pcc.Names.Add("TEXTUREGROUP_Shadowmap");
            Unreal.PCCObject.ExportEntry exp = pcc.Exports[tex2D.pccExpIdx];
            exp.Data = tex2D.ToArray(exp.DataOffset, pcc);
            pcc.Exports[tex2D.pccExpIdx] = exp;
            pcc.altSaveToFile(exec + "temp2.pcc", true);
            try
            {
                Unreal.PCCObject temp = new Unreal.PCCObject(exec + "temp2.pcc");
            }
            catch
            {
                pcc.saveToFile(exec + "temp2.pcc");
            }
            File.Copy(exec + "temp2.pcc", tex2D.allPccs[0], true);
            bgw1.ReportProgress(1);

            for (int i = 0; i < tex2D.allPccs.Count; i++)
            {
                throw new NotImplementedException("Not done yet");
            }
            */
            #endregion
            tocUpdate();
        }

        private void bgw1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            pb1.Value = e.ProgressPercentage;
            if (e.ProgressPercentage < numFiles)
                SetStatus(e.ProgressPercentage + " / " + numFiles + " saved...");
            else
                SetStatus("Updating PCConsoleTOC.bin...");
        }

        private void bgw1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            pb1.Value = pb1.Maximum;
            SetStatus("Finished");
        }

        private void tocUpdate()
        {
            TOCUpdater.TOCUpdater toc = new TOCUpdater.TOCUpdater();
            toc.EasyUpdate();
        }
    }
}
