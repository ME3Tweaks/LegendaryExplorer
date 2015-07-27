using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using AmaroK86.ImageFormat;

namespace SaltTPF
{
    public partial class TPFView : Form
    {
        private TPFExtract _tpf;
        private bool _autoPreview = true;
        private bool _resize = true;

        public TPFView(TPFExtract tpf)
        {
            InitializeComponent();

            _tpf = tpf;
            for (int i = 0; i < _tpf.GetNumEntries(); i++)
            {
                listBox1.Items.Add(_tpf.GetEntry(i).Filename);
            }

            uint unc = 0;
            uint cpr = 0;
            for (int i = 0; i < _tpf.GetNumEntries(); i++)
            {
                unc += _tpf.GetEntry(i).UncomprSize;
                cpr += _tpf.GetEntry(i).ComprSize;
            }
            rtb4.Text = "TPF File Info\n\nFilename: " + _tpf.GetFilename() + "\nStored Files: " + _tpf.GetNumEntries() + "\nAverage Compression Ratio: " +
                ((float)cpr / unc) + "\nComment:\n" + _tpf.GetComment();
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void autoPreviewsEnabledToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _autoPreview = !_autoPreview;
            if (_autoPreview)
                autoPreviewsEnabledToolStripMenuItem.Text = "AutoPreviews Enabled";
            else
                autoPreviewsEnabledToolStripMenuItem.Text = "AutoPreviews Disabled";
        }

        private void resizeEnabledToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _resize = !_resize;
            if (_resize)
                resizeEnabledToolStripMenuItem.Text = "Resize Enabled";
            else
                resizeEnabledToolStripMenuItem.Text = "Resize Disabled";
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex < 0)
                return;

            ZipReader.ZipEntryFull ent = _tpf.GetEntry(listBox1.SelectedIndex);

            rtb2.Text = ent.Filename + "\n\nCompressed Length: " + ent.ComprSize + "\nUncompressed Length: " + ent.UncomprSize + "\nCompression Ratio: " +
                ((float)ent.ComprSize / ent.UncomprSize) + "\nCRC-32: " + BitConverter.ToString(BitConverter.GetBytes(ent.CRC)) + "\nComment:\n";
            if (String.IsNullOrEmpty(ent.Comment))
                rtb2.Text += "None";
            else
                rtb2.Text += ent.Comment;

            if (!_autoPreview)
                return;

            String extension = Path.GetExtension(ent.Filename);
            if (String.Compare(extension, ".def", true) != 0)
                SearchHash(listBox1.SelectedIndex);
            if (String.Compare(extension, ".dds", true) == 0)
                PreviewTex(ent, true);
            else if (String.Compare(extension, ".tga", true) == 0)
                PreviewTex(ent, false);
            else if (String.Compare(extension, ".def", true) == 0)
                PreviewDef(ent);
            else if (String.Compare(extension, ".jpg", true) == 0 || String.Compare(extension, ".gif", true) == 0 || String.Compare(extension, ".png", true) == 0)
                PreviewImg(ent);
        }

        private void PreviewTex(ZipReader.ZipEntryFull ent, bool dds)
        {
            try
            {
                Bitmap img;
                if (dds)
                {
                    byte[] data = ent.Extract(true);
                    if (data == null)
                        throw new NullReferenceException("Data returned was null");

                    DDSPreview ddsimg = new DDSPreview(data);
                    //img = DDSImage.ToBitmap(ddsimg.GetMipData(), ddsimg.Format, (int)ddsimg.Width, (int)ddsimg.Height);
                    img = ddsimg.ToBitmap();
                }
                else
                {
                    ent.Extract(false, "preview.tga");
                    img = new TargaImage("preview.tga").Image;
                    File.Delete("preview.tga");
                }
                if (pictureBox1.Image != null)
                    pictureBox1.Image.Dispose();

                if (_resize)
                    pictureBox1.Image = resizeImage(img, new System.Drawing.Size(512, 512));
                else
                    pictureBox1.Image = img;
                pictureBox1.Visible = true;
                pictureBox1.Refresh();
            }
            catch (Exception exc)
            {
                MessageBox.Show("An error occurred: " + exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PreviewDef(ZipReader.ZipEntryFull ent)
        {
            try
            {
                byte[] data = ent.Extract(true);
                if (data == null)
                    throw new NullReferenceException("Data returned was null");

                pictureBox1.Visible = false;
                char[] chars = new char[data.Length];
                for (int i = 0; i < data.Length; i++)
                    chars[i] = (char)data[i];
                rtb1.Text = "Texmod.def contents:\n\n" + new string(chars);
            }
            catch (Exception exc)
            {
                MessageBox.Show("An error occurred: " + exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PreviewImg(ZipReader.ZipEntryFull ent)
        {

        }

        private Image resizeImage(Image imgToResize, Size size)
        {
            int sourceWidth = imgToResize.Width;
            int sourceHeight = imgToResize.Height;

            float nPercent = 0;
            float nPercentW = 0;
            float nPercentH = 0;

            nPercentW = ((float)size.Width / (float)sourceWidth);
            nPercentH = ((float)size.Height / (float)sourceHeight);

            if (nPercentH < nPercentW)
                nPercent = nPercentH;
            else
                nPercent = nPercentW;

            int destWidth = (int)(sourceWidth * nPercent);
            int destHeight = (int)(sourceHeight * nPercent);

            Bitmap b = new Bitmap(destWidth, destHeight);
            Graphics g = Graphics.FromImage((Image)b);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            g.DrawImage(imgToResize, 0, 0, destWidth, destHeight);
            g.Dispose();

            return (Image)b;
        }

        private void SearchHash(int n)
        {
            String text;

            try
            {
                byte[] data = _tpf.GetTexmodDef().Extract(true);
                if (data == null)
                    throw new NullReferenceException("Data returned was null");

                char[] chars = new char[data.Length];
                for (int i = 0; i < data.Length; i++)
                    chars[i] = (char)data[i];

                text = new string(chars);
            }
            catch (Exception exc)
            {
                MessageBox.Show("An error occurred: " + exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            text = text.Split('\n')[n];
            uint hash = uint.Parse(text.Split('|')[0].Substring(2), System.Globalization.NumberStyles.AllowHexSpecifier);
            text = text.Split('|')[1];

            rtb3.Text = "Hash matches:\n\n";

            for (int i = 0; i < 3; i++)
            {
                List<String> vals = _tpf.SearchHash(i, hash);
                if (vals != null)
                {
                    rtb3.Text += "ME" + (i + 1) + " results:\n";
                    foreach (String v in vals)
                    {
                        rtb3.Text += v.Substring(1) + "\n";
                    }
                    rtb3.Text += "\n";
                }
                else
                    rtb3.Text += "No matches found in ME" + (i + 1) + "\n\n";
            }
        }

        private void extractSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex < 0)
                return;

            ZipReader.ZipEntryFull ent = _tpf.GetEntry(listBox1.SelectedIndex);

            SaveFileDialog saver = new SaveFileDialog();
            saver.Title = "Enter the filename to save the extracted file to";
            saver.FileName = ent.Filename;
            if (saver.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;
            ent.Extract(false, saver.FileName);
        }

        private void extractAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fold = new FolderBrowserDialog();
            fold.Description = "Select the directory to save the files to";
            if (fold.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;
            for (int i = 0; i < _tpf.GetNumEntries(); i++)
            {
                ZipReader.ZipEntryFull ent = _tpf.GetEntry(i);
                ent.Extract(false, Path.Combine(fold.SelectedPath, ent.Filename));
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog open = new OpenFileDialog();
            open.Title = "Select the TPF file to open";
            open.Filter = "TPF File|*.tpf";
            if (open.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            TPFExtract newtpf = new TPFExtract(open.FileName, Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "exec"), true);
            this.Close();
        }

        private void extractToFileStructureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fold = new FolderBrowserDialog();
            fold.Description = "Select the root folder to write to";
            if (fold.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            String alltext;

            try
            {
                byte[] data = _tpf.GetTexmodDef().Extract(true);
                if (data == null)
                    throw new NullReferenceException("Data returned was null");

                char[] chars = new char[data.Length];
                for (int i = 0; i < data.Length; i++)
                    chars[i] = (char)data[i];

                alltext = new string(chars);
            }
            catch (Exception exc)
            {
                MessageBox.Show("An error occurred: " + exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Directory.CreateDirectory(Path.Combine(fold.SelectedPath, "ME1"));
            Directory.CreateDirectory(Path.Combine(fold.SelectedPath, "ME2"));
            Directory.CreateDirectory(Path.Combine(fold.SelectedPath, "ME3"));
            Directory.CreateDirectory(Path.Combine(fold.SelectedPath, "Unknown"));

            for (int i = 0; i < _tpf.GetNumEntries() - 1; i++)
            {
                String txt = alltext.Split('\n')[i];
                uint hash = uint.Parse(txt.Split('|')[0].Substring(2), System.Globalization.NumberStyles.AllowHexSpecifier);
                txt = txt.Split('|')[1];
                bool found = false;

                for (int j = 0; j < 3; j++)
                {
                    List<String> vals = _tpf.SearchHash(j, hash);
                    if (vals != null)
                    {
                        found = true;
                        foreach (String s in vals)
                        {
                            String tmp = s.Substring(1);
                            String[] paths = tmp.Split('.');
                            String path = Path.Combine(fold.SelectedPath, "ME" + (j + 1));
                            for (int k = 0; k < paths.Length - 1; k++)
                            {
                                path = Path.Combine(path, paths[k]);
                                Directory.CreateDirectory(path);
                            }
                            _tpf.ExtractN(Path.ChangeExtension(Path.Combine(path, paths[paths.Length - 1]), Path.GetExtension(_tpf.GetEntry(i).Filename)), i);
                        }
                    }
                }
                if (!found)
                {
                    _tpf.ExtractN(Path.Combine(fold.SelectedPath, "Unknown", _tpf.GetEntry(i).Filename), i);
                }
            }

            MessageBox.Show("finished");
        }
    }
}
