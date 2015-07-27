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
using UDKExplorer;
using UDKExplorer.UDK;

namespace ME3Explorer.UDKConverter
{
    public partial class UDKConverter : Form
    {
        public UDKConverter()
        {
            InitializeComponent();
        }

        private void pccToUpkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                ConvertPccToUpk(d.FileName);
        }

        public void ConvertPccToUpk(string filename)
        {
            string waitline = "\\-/|";
            int pos = 0;
            int count = 0;
            BitConverter.IsLittleEndian = true;
            PCCObject pcc = new PCCObject(filename);
            string newfilename = Path.GetDirectoryName(filename) + "\\" + Path.GetFileNameWithoutExtension(filename) + ".upk";
            UDKObject udk = new UDKObject();
            udk.Names = new List<UDKObject.NameEntry>();
            udk.Imports = new List<UDKObject.ImportEntry>();
            udk.Exports = new List<UDKObject.ExportEntry>();
            udk.ExportCount = pcc.Exports.Count;
            udk.ImportCount = pcc.Imports.Count;
            udk.NameCount = pcc.Names.Count;
            rtb1.Text = "Convert Names...\n";
            RtbUpd();
            foreach (string s in pcc.Names)
            {
                UDKObject.NameEntry e = new UDKObject.NameEntry();
                e.name = s;
                e.flags = 0x70010;
                udk.Names.Add(e);
                if ((count++) % 100 == 0)
                {
                    this.Text = "UDK Converter " + waitline[pos++];
                    if (pos == waitline.Length)
                        pos = 0;
                }
            }
            rtb1.Text += "Convert Imports...\n";
            RtbUpd();
            foreach (PCCObject.ImportEntry i in pcc.Imports)
            {
                UDKObject.ImportEntry e = new UDKObject.ImportEntry();
                e.raw = i.data;
                udk.Imports.Add(e);
                if ((count++) % 100 == 0)
                {
                    this.Text = "UDK Converter " + waitline[pos++];
                    if (pos == waitline.Length)
                        pos = 0;
                }
            }
            rtb1.Text += "Convert Exports...\n";
            RtbUpd();
            foreach (PCCObject.ExportEntry ex in pcc.Exports)
            {
                UDKObject.ExportEntry e = new UDKObject.ExportEntry();
                e.raw = ex.info;
                e.data = ex.Data;
                udk.Exports.Add(e);
                if ((count++) % 100 == 0)
                {
                    this.Text = "UDK Converter " + waitline[pos++];
                    if (pos == waitline.Length)
                        pos = 0;
                }
            }
            rtb1.Text += "Saving to file...\n";
            this.Text = "UDK Converter";
            udk._HeaderOff = 0x19;
            udk.Header = CreateUPKHeader(pcc);
            udk.fz.raw = new byte[0];
            udk.SaveToFile(newfilename);
            MessageBox.Show("Done.");
        }

        public void RtbUpd()
        {
            rtb1.SelectionStart = rtb1.TextLength;
            rtb1.ScrollToCaret();
            rtb1.Refresh();
        }

        public byte[] CreateUPKHeader(PCCObject pcc)
        {
            byte[] res = new byte[0x81];
            CopyArray(pcc.header, res, 0, 4, 0);        //magic
            res[0x04] = 0x2D;                           //filevers
            res[0x05] = 0x03;
            res[0x0C] = 0x05;                           //"None".Length
            CopyStrToArray("None", res, 0x10);
            CopyArray(pcc.header, res, 0x1A, 4, 0x15);  //Flags
            CopyArray(pcc.header, res, 0x4E, 16, 0x45); //GUID
            res[0x55] = 0x01;                           //Generation Count
            res[0x65] = 0x94;                           //Engine Vers
            res[0x66] = 0x2A;
            return res;
        }

        public void CopyArray(byte[] src, byte[] dst, int SrcBeg, int Count, int DstOff)
        {
            for (int i = SrcBeg; i < SrcBeg + Count; i++)
                dst[DstOff + i - SrcBeg] = src[i];
        }
        public void CopyStrToArray(string src, byte[] dst, int DstOff)
        {
            for (int i = 0; i < src.Length; i++)
                dst[DstOff + i] = (byte)src[i];
        }
    }
}
