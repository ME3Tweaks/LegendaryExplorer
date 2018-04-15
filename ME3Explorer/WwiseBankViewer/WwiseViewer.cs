using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using ME3Explorer.Packages;
using ME3Explorer;
using Be.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace ME3Explorer.WwiseBankEditor
{
    public partial class WwiseEditor : WinFormsBase
    {
        public List<int> objects;
        public WwiseBank bank;

        public WwiseEditor()
        {
            InitializeComponent();
        }

        private void openPccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == DialogResult.OK)
            {
                LoadFile(d.FileName);
            }
        }

        public void LoadFile(string fileName)
        {
            try
            {
                LoadME3Package(fileName);
                ListRefresh();
                openFileLabel.Text = Path.GetFileName(fileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message);
            }
        }

        public void ListRefresh()
        {
            objects = new List<int>();
            IReadOnlyList<IExportEntry> Exports = pcc.Exports;
            for (int i = 0; i < Exports.Count; i++)
                if (Exports[i].ClassName == "WwiseBank")
                    objects.Add(i);

            listBox1.Items.Clear();
            listBox2.Items.Clear();
            for (int i = 0; i < objects.Count; i++)
                listBox1.Items.Add(objects[i] + " : " + pcc.Exports[objects[i]].ObjectName);            
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshSelected();
        }

        public void RefreshSelected()
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            int index = objects[n];
            bank = new WwiseBank(pcc as ME3Package, index);
            hb1.ByteProvider = new DynamicByteProvider(bank.getBinary());
            rtb1.Text = bank.GetQuickScan();
            ListRefresh2();
        }

        public void ListRefresh2()
        {
            listBox2.Items.Clear();
            for (int i = 0; i < bank.HIRCObjects.Count; i++)
                listBox2.Items.Add(i.ToString("D4") + " : " + bank.GetHircDesc(bank.HIRCObjects[i]));
        }

        private void exportAllWEMFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (bank == null || bank.didx_data == null || bank.didx_data.Length == 0)
                return;
            CommonOpenFileDialog m = new CommonOpenFileDialog();
            m.IsFolderPicker = true;
            m.EnsurePathExists = true;
            m.Title = "Select Folder to Output to";
            if (m.ShowDialog() == CommonFileDialogResult.Ok)
            {
                if (bank.ExportAllWEMFiles(m.FileName))
                    MessageBox.Show("Done.");
                else
                    MessageBox.Show("Error occured!");
            }
        }

        private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox2.SelectedIndex;
            if (n == -1)
                return;
            hb2.ByteProvider = new DynamicByteProvider(bank.HIRCObjects[n]);
        }

        private void recreateBankToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (bank == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.bin|*.bin";
            if (d.ShowDialog() == DialogResult.OK)
            {
                File.WriteAllBytes(d.FileName, bank.RecreateBinary());
                MessageBox.Show("Done.");
            }
        }

        private void cloneObjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (bank == null)
                return;
            int m = listBox2.SelectedIndex;
            if (m == -1)
                return;
            bank.CloneHIRCObject(m);
            ListRefresh2();
            listBox2.SelectedIndex = listBox2.Items.Count - 1;
        }

        private void saveHexEditsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveHIRCHexEdits();
        }

        private void saveHIRCHexEdits()
        {
            if (bank == null)
                return;
            int m = listBox2.SelectedIndex;
            if (m == -1)
                return;
            byte[] tmp = new byte[hb2.ByteProvider.Length];
            for (int i = 0; i < hb2.ByteProvider.Length; i++)
                tmp[i] = hb2.ByteProvider.ReadByte(i);

            //write size of this HIRC
            int insideLen = (int) hb2.ByteProvider.Length - 5;
            byte[] b = BitConverter.GetBytes(insideLen);
            b.CopyTo(tmp, 1);

            bank.HIRCObjects[m] = tmp;
            Console.WriteLine("HIRC hex size: " + bank.HIRCObjects[m].Count().ToString("X4"));
            ListRefresh2();
            listBox2.SelectedIndex = m;
        }

        private void editToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (bank == null)
                return;
            int m = listBox2.SelectedIndex;
            if (m == -1)
                return;
            byte[] buff = bank.HIRCObjects[m];
            if (buff[0] != 0x02)
            {
                MessageBox.Show("Not a Sound SFX/Voice object");
                return;
            }
            else
            {
                
                int ID1 = BitConverter.ToInt32(buff, 5);
                int opt = BitConverter.ToInt32(buff, 13);
                int ID2 = BitConverter.ToInt32(buff, 17);
                int ID3 = BitConverter.ToInt32(buff, 21);
                int tp = buff[25];
                string s = ID1 + ", " + opt + ", " + ID2 + ", " + ID3 + ", " + tp;
                string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new values[Object ID, Stream Option, ID audio, ID source, Sound Type", "ME3Explorer", s, 0, 0);
                string[] sres = result.Split(',');
                if (sres.Length != 5)
                    return;
                if (!int.TryParse(sres[0].Trim(), out ID1))
                    return;
                if (!int.TryParse(sres[1].Trim(), out opt))
                    return;
                if (!int.TryParse(sres[2].Trim(), out ID2))
                    return;
                if (!int.TryParse(sres[3].Trim(), out ID3))
                    return;
                if (!int.TryParse(sres[4].Trim(), out tp))
                    return;
                MemoryStream res = new MemoryStream(buff);
                res.Seek(5, 0);
                res.Write(BitConverter.GetBytes(ID1), 0, 4);
                res.Seek(13, 0);
                res.Write(BitConverter.GetBytes(opt), 0, 4);
                res.Write(BitConverter.GetBytes(ID2), 0, 4);
                res.Write(BitConverter.GetBytes(ID3), 0, 4);
                res.WriteByte((byte)tp);
                bank.HIRCObjects[m] = res.ToArray();
                ListRefresh2();
                listBox2.SelectedIndex = m;
            }
        }

        private void saveBankToPccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (bank == null)
                return;
            int n = bank.MyIndex;
            byte[] tmp = bank.RecreateBinary();
            pcc.Exports[n].Data = tmp;
            MessageBox.Show("Done.");
        }

        private void savePccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == DialogResult.OK)
            {
                pcc.save(d.FileName);
                MessageBox.Show("Done.");
            }
        }

        private void saveHexChangesButton_Click(object sender, EventArgs e)
        {
            saveHIRCHexEdits();
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            IEnumerable<PackageUpdate> relevantUpdates = updates.Where(x => x.change != PackageChange.Import &&
                                                                            x.change != PackageChange.ImportAdd &&
                                                                            x.change != PackageChange.Names);
            List<int> updatedExports = relevantUpdates.Select(x => x.index).ToList();
            if (updatedExports.Contains(bank.MyIndex))
            {
                int index = bank.MyIndex;
                //loaded sequence is no longer a sequence
                if (pcc.getExport(index).ClassName != "WwiseBank")
                {
                    bank = null;
                    listBox2.Items.Clear();
                    rtb1.Text = "";
                    hb1.ByteProvider = new DynamicByteProvider(new List<byte>());
                    hb2.ByteProvider = new DynamicByteProvider(new List<byte>());
                }
                RefreshSelected();
                updatedExports.Remove(index);
            }
            if (updatedExports.Intersect(objects).Count() > 0)
            {
                ListRefresh();
            }
            foreach (var i in updatedExports)
            {
                if (pcc.getExport(i).ClassName.Contains("WwiseBank"))
                {
                    ListRefresh();
                    break;
                }
            }
        }
        private void searchHexButton_Click(object sender, EventArgs e)
        {
            if (bank == null)
                return;
            int m = listBox2.SelectedIndex;
            if (m == -1)
                m = 0;
            string hexString = searchHexTextBox.Text.Replace(" ", string.Empty);
            if (hexString.Length == 0)
                return;
            if (!HexConverter.Hexconverter.isHexString(hexString))
            {
                searchHexStatus.Text = "Illegal characters in Hex String";
                return;
            }
            if (hexString.Length % 2 != 0)
            {
                searchHexStatus.Text = "Odd number of characters in Hex String";
                return;
            }
            byte[] buff = new byte[hexString.Length / 2];
            for (int i = 0; i < hexString.Length / 2; i++)
            {
                buff[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }
            byte[] hirc;
            int count = bank.HIRCObjects.Count;
            int hexboxIndex = (int)hb2.SelectionStart + 1;
            for (int i = 0; i < count; i++)
            {
                hirc = bank.HIRCObjects[(i + m) % count]; //search from selected index, and loop back around
                int indexIn = hirc.IndexOfArray(buff, hexboxIndex);
                if (indexIn > -1)
                {
                    listBox2.SelectedIndex = (i + m) % count;
                    hb2.Select(indexIn, buff.Length);
                    searchHexStatus.Text = "";
                    return;
                }
                hexboxIndex = 0;
            }
            searchHexStatus.Text = "Hex not found";
        }
    }
}