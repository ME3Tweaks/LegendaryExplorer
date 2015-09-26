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
using KFreonLib.MEDirectories;

namespace ME3Explorer.GUIDCacheEditor
{
    public partial class GUIDCacheEditor : Form
    {
        public struct GuidEntry
        {
            public int NameIdx;
            public string Name;
            public byte[] GUID;
        }

        public List<PropertyReader.Property> props;
        public List<GuidEntry> GUIDs;
        public PCCObject pcc;
        
        public GUIDCacheEditor()
        {
            InitializeComponent();
        }

        private void openGUIDCacheToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BitConverter.IsLittleEndian = true;
            pcc = new PCCObject(ME3Directory.cookedPath + "GuidCache.pcc");
            ReadGUIDs(pcc.Exports[0].Data);
            RefreshLists();
        }

        public void ReadGUIDs(byte[] buff)
        {
            props = PropertyReader.getPropList(pcc, buff);
            int pos = props[props.Count - 1].offend;
            int count = BitConverter.ToInt32(buff, pos);
            pos += 4;
            GUIDs = new List<GuidEntry>();
            for (int i = 0; i < count; i++)
            {
                GuidEntry g = new GuidEntry();
                g.NameIdx = BitConverter.ToInt32(buff, pos);
                g.Name = pcc.getNameEntry(g.NameIdx);
                g.GUID = new byte[16];
                for (int j = 0; j < 16; j++)
                    g.GUID[j] = buff[pos + j + 8];
                GUIDs.Add(g);
                pos += 24;
            }            
        }

        public void RefreshLists()
        {
            listBox1.Items.Clear();
            int count = 0;
            foreach (GuidEntry g in GUIDs)
            {
                string s = (count++).ToString("d5") + " : GUID[";
                foreach (byte b in g.GUID)
                    s += b.ToString("X2");
                s += "] \"" + g.Name + "\"";
                listBox1.Items.Add(s);
            }
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (GUIDs == null)
                return;
            if (n == -1)
                n = 0;
            else
                n++;
            for(int i=n;i<GUIDs.Count;i++)
                if (GUIDs[i].Name.ToLower().Contains(toolStripTextBox1.Text))
                {
                    listBox1.SelectedIndex = i;
                    return;
                }
        }

        private void listBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            GuidEntry g = GUIDs[n];
            string s = g.NameIdx + ",";
            foreach (byte b in g.GUID)
                s += b.ToString("X2");
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new values", "ME3Explorer", s, 0, 0);
            if (result == "")
                return;
            string[] tmp = result.Split(',');
            if (tmp.Length != 2 || tmp[1].Trim().Length != 32)
                return;
            int i = -1;
            if (Int32.TryParse(tmp[0],out i) && pcc.isName(i))
            {
                g.NameIdx = i;
                g.Name = pcc.getNameEntry(i);
            }
            else
                return;
            g.GUID = StringToByteArray(tmp[1]);
            if (g.GUID[0] == 0x00)
                return;
            GUIDs[n] = g;
            RefreshLists();
            listBox1.SelectedIndex = n;
        }

        public static byte[] StringToByteArray(string hex)
        {
            if (hex.Length % 2 == 1)
                return new byte[16];
            byte[] arr = new byte[hex.Length >> 1];
            for (int i = 0; i < hex.Length >> 1; ++i)
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            return arr;
        }

        public static int GetHexVal(char hex)
        {
            int val = (int)hex;
            return val - (val < 58 ? 48 : 55);
        }

        private void saveGUIDCacheToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null || GUIDs == null)
                return;
            MemoryStream m = new MemoryStream();
            byte[] buff = pcc.Exports[0].Data;
            props = PropertyReader.getPropList(pcc, buff);
            int pos = props[props.Count - 1].offend;
            m.Write(buff, 0, pos);
            m.Write(BitConverter.GetBytes(GUIDs.Count), 0, 4);
            foreach (GuidEntry g in GUIDs)
            {
                m.Write(BitConverter.GetBytes(g.NameIdx), 0, 4);
                m.Write(BitConverter.GetBytes((int)0), 0, 4);
                foreach (byte b in g.GUID)
                    m.WriteByte(b);
            }
            pcc.Exports[0].Data = m.ToArray();
            pcc.Exports[0].hasChanged = true;
            pcc.altSaveToFile(pcc.pccFileName, true, 30); //weird header!
            MessageBox.Show("Done.");
            RefreshLists();
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            GUIDs.RemoveAt(n);
            RefreshLists();
        }

        private void cloneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            GuidEntry g = GUIDs[n];
            GuidEntry g2 = new GuidEntry();
            g2.NameIdx = g.NameIdx;
            g2.Name = g.Name;
            g2.GUID = new byte[16];
            for (int i = 0; i < 16; i++)
                g2.GUID[i] = g.GUID[i];
            GUIDs.Add(g2);
            RefreshLists();
        }
    }
}
