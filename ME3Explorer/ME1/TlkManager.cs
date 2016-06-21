using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;

using ME1Explorer.Unreal.Classes;
using ME3Explorer.Packages;

namespace ME1Explorer
{
    public partial class TlkManager : Form
    {
        public List<ME1Package> packages;
        public List<BioTlkFileSet> tlkFileSets;
        public TalkFiles selectedTlks;

        private bool editor;

        public TlkManager(bool _editor = false)
        {
            InitializeComponent();
            editor = _editor;
            if (editor)
            {
                this.Text = "Tlk Editor";
                splitContainer1.Panel2Collapsed = true;
                addToSelectedTlkFilesToolStripMenuItem.Visible = false;
                saveToFileButton.Visible = true;
                saveToFileToolStripMenuItem.Visible = true;
                replaceWithFileButton.Visible = true;
                packages = new List<ME1Package>();
                tlkFileSets = new List<BioTlkFileSet>();
            }
        }

        public void InitTlkManager(TalkFiles tlks)
        {
            packages = new List<ME1Package>();
            tlkFileSets = new List<BioTlkFileSet>();
            selectedTlks = tlks;
            
            foreach (TalkFile tlkFile in selectedTlks.tlkList)
            {
                selectedTlkFilesBox.Items.Add(Path.GetFileName(tlkFile.pcc.fileName) + " -> " + tlkFile.BioTlkSetName + tlkFile.Name);
            }
        }

        public void InitTlkManager(ME1Package pcc, BioTlkFileSet tlkSet, TalkFiles tlks = null)
        {
            packages = new List<ME1Package>();
            tlkFileSets = new List<BioTlkFileSet>();
            selectedTlks = tlks ?? new TalkFiles();

            packages.Add(pcc);
            refreshFileBox();
            fileBox.SelectedIndex = 0;
            Application.DoEvents();
            for (int i = 0; i < tlkFileSets.Count; i++)
            {
                if (tlkFileSets[i].index == tlkSet.index)
                {
                    bioTlkSetBox.SelectedIndex = i;
                    Application.DoEvents();
                    tlkFileBox.SelectedIndex = tlkSet.selectedTLK;
                    break;
                }
            }
            TalkFile tlk = tlkSet.talkFiles[tlkSet.selectedTLK];
            if (!selectedTlks.tlkList.Contains(tlk))
            {
                selectedTlks.tlkList.Add(tlk);
            }
            foreach (TalkFile tlkFile in selectedTlks.tlkList)
            {
                selectedTlkFilesBox.Items.Add(Path.GetFileName(pcc.fileName) + " -> " + tlkFile.BioTlkSetName + tlkFile.Name); 
            }
        }

        private void refreshFileBox()
        {
            int selectedFile = fileBox.SelectedIndex;
            int selectedTlkSet = bioTlkSetBox.SelectedIndex;
            int selectedTlkFile = tlkFileBox.SelectedIndex;
            fileBox.Items.Clear();
            foreach (ME1Package pcc in packages)
            {
                fileBox.Items.Add(Path.GetFileName(pcc.fileName));
            }
            fileBox.SelectedIndex = selectedFile;
            Application.DoEvents();
            bioTlkSetBox.SelectedIndex = selectedTlkSet;
            Application.DoEvents();
            tlkFileBox.SelectedIndex = selectedTlkFile;
        }

        private void addFileButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.u;*.upk;*sfm|*.u;*.upk;*sfm";
            if (d.ShowDialog() == DialogResult.OK)
            {
                packages.Add(new ME1Package(d.FileName));
                refreshFileBox();
            }
        }

        private void fileBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            bioTlkSetBox.Items.Clear();
            tlkFileBox.Items.Clear();
            int n = fileBox.SelectedIndex;
            if (n == -1)
            {
                return;
            }
            ME1Package pcc = packages[n];
            tlkFileSets.Clear();
            for (int i = 0; i < pcc.Exports.Count; i++)
            {
                if(pcc.Exports[i].ClassName == "BioTlkFileSet")
                {
                    BioTlkFileSet b = new BioTlkFileSet(pcc, i);
                    tlkFileSets.Add(b);
                    bioTlkSetBox.Items.Add(b.Name);
                }
            }
            //No BioTlkSets, look for loose BioTlkFiles
            if(tlkFileSets.Count == 0)
            {
                BioTlkFileSet tlkSet = new BioTlkFileSet(pcc);
                for (int i = 0; i < pcc.Exports.Count; i++)
                {
                    if (pcc.Exports[i].ClassName == "BioTlkFile")
                    {
                        TalkFile tlk = new TalkFile(pcc, i);
                        tlkSet.talkFiles.Add(tlk);
                    }
                }
                if (tlkSet.talkFiles.Count != 0)
                {
                    tlkFileSets.Add(tlkSet);
                    bioTlkSetBox.Items.Add("Misc TlkFiles");
                }
            }
        }

        private void bioTlkSetBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = bioTlkSetBox.SelectedIndex;
            if (n == -1)
            {
                return;
            }
            tlkFileBox.BeginUpdate();
            tlkFileBox.Items.Clear();
            BioTlkFileSet tlkSet = tlkFileSets[n];
            foreach (TalkFile tlk in tlkSet.talkFiles)
            {
                tlkFileBox.Items.Add(tlk.Name);
            }
            tlkFileBox.EndUpdate();
        }

        private void tlkFileBox_DoubleClick(object sender, EventArgs e)
        {
            addToSelectedTlks();
        }

        private void addToSelectedTlks()
        {
            int n = tlkFileBox.SelectedIndex;
            if (n == -1 || editor)
            {
                return;
            }
            TalkFile tlk = tlkFileSets[bioTlkSetBox.SelectedIndex].talkFiles[n];
            if (!selectedTlks.tlkList.Contains(tlk))
            {
                selectedTlks.tlkList.Add(tlk);
                selectedTlkFilesBox.Items.Add(Path.GetFileName(tlk.pcc.fileName) + " -> " + tlkFileSets[bioTlkSetBox.SelectedIndex].Name + tlk.Name);
            }
        }

        private void addToSelectedTlkFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            addToSelectedTlks();
        }

        private void addFromTlkFilesButton_Click(object sender, EventArgs e)
        {
            addToSelectedTlks();
        }

        private void removeButton_Click(object sender, EventArgs e)
        {
            int n = selectedTlkFilesBox.SelectedIndex;
            if (n == -1)
            {
                return;
            }
            selectedTlks.tlkList.RemoveAt(n);
            selectedTlkFilesBox.Items.RemoveAt(n);
        }

        private void upButton_Click(object sender, EventArgs e)
        {
            int n = selectedTlkFilesBox.SelectedIndex;
            if (n <= 0)
            {
                return;
            }
            TalkFile tlk = selectedTlks.tlkList[n];
            string tlkPath = selectedTlkFilesBox.Items[n] as string;
            selectedTlks.tlkList.RemoveAt(n);
            selectedTlks.tlkList.Insert(n - 1, tlk);

            selectedTlkFilesBox.Items.RemoveAt(n);
            selectedTlkFilesBox.Items.Insert(n - 1, tlkPath);
            selectedTlkFilesBox.SelectedIndex = n - 1;
        }

        private void downButton_Click(object sender, EventArgs e)
        {
            int n = selectedTlkFilesBox.SelectedIndex;
            if (n == -1 || n >= selectedTlkFilesBox.Items.Count - 1)
            {
                return;
            }
            TalkFile tlk = selectedTlks.tlkList[n];
            string tlkPath = selectedTlkFilesBox.Items[n] as string;
            selectedTlks.tlkList.RemoveAt(n);
            selectedTlks.tlkList.Insert(n + 1, tlk);

            selectedTlkFilesBox.Items.RemoveAt(n);
            selectedTlkFilesBox.Items.Insert(n + 1, tlkPath);
            selectedTlkFilesBox.SelectedIndex = n + 1;
        }

        private void saveToFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveToFile();
        }

        private void saveToFileButton_Click(object sender, EventArgs e)
        {
            saveToFile();
        }

        private void saveToFile()
        {
            int n = tlkFileBox.SelectedIndex;
            if (n == -1)
            {
                return;
            }
            TalkFile tlk = tlkFileSets[bioTlkSetBox.SelectedIndex].talkFiles[n];

            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.xml|*.xml";
            if (d.ShowDialog() == DialogResult.OK)
            {
                tlk.saveToFile(d.FileName);
                MessageBox.Show("Done");
            }
        }

        private void replaceWithFileButton_Click(object sender, EventArgs e)
        {
            int n = tlkFileBox.SelectedIndex;
            if (n == -1)
            {
                return;
            }
            TalkFile tlk = tlkFileSets[bioTlkSetBox.SelectedIndex].talkFiles[n];
            HuffmanCompression compressor = new HuffmanCompression();

            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.xml|*.xml";
            if (d.ShowDialog() == DialogResult.OK)
            {
                compressor.LoadInputData(d.FileName);
                compressor.replaceTlkwithFile(tlk.pcc, tlk.index);
                MessageBox.Show("Done");
            }
            n = packages.FindIndex(x => tlk.pcc.fileName == x.fileName);
            packages.RemoveAt(n);
            packages.Insert(n, new ME1Package(tlk.pcc.fileName));
            refreshFileBox();
        }
    }
}
