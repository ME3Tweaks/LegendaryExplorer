using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME1Explorer.Unreal;

namespace ME1Explorer.SaveGameOperator
{
    public partial class SaveGameOperator : Form
    {
        public SaveGameOperator()
        {
            InitializeComponent();
        }

        SaveGame Save;

        private void loadSaveGameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.MassEffectSave|*.MassEffectSave";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BitConverter.IsLittleEndian = true;
                Save = new SaveGame(d.FileName);
                Save.Loaded = true;
                LoadData();
            }
        }

        public void LoadData()
        {
            Save.ExtractME1Package(0, "temp.upk");
            PCCObject pcc = new PCCObject("temp.upk");
            File.Delete("temp.upk");
            byte[] buff = pcc.Exports[1].Data;
            List<SaltPropertyReader.Property> props = SaltPropertyReader.getPropList(pcc, buff);
            foreach (SaltPropertyReader.Property p in props)
            {
                string name = p.Name;
                switch (name)
                {
                    case "m_nResourceCredits":
                        textBox1.Text = BitConverter.ToInt32(buff, p.offend - 4).ToString();
                        break;
                    case "m_nResourceGrenades":
                        textBox2.Text = BitConverter.ToInt32(buff, p.offend - 4).ToString();
                        break;
                    case "m_fResourceMedigel":
                        textBox3.Text = BitConverter.ToSingle(buff, p.offend - 4).ToString();
                        break;
                    case "m_fResourceSalvage":
                        textBox4.Text = BitConverter.ToSingle(buff, p.offend - 4).ToString();
                        break;
                }                
            }
            textBox5.Text = BitConverter.ToInt32(Save.Files[1].memory.ToArray(), 0x4A7).ToString();//Paragon
            textBox6.Text = BitConverter.ToInt32(Save.Files[1].memory.ToArray(), 0x4A3).ToString();//Renegade
        }

        public void SaveData()
        {
            Save.ExtractME1Package(0, "temp.upk");
            PCCObject pcc = new PCCObject("temp.upk");
            File.Delete("temp.upk");
            byte[] buff = pcc.Exports[1].Data;
            List<SaltPropertyReader.Property> props = SaltPropertyReader.getPropList(pcc, buff);
            int v;
            float f;
            foreach (SaltPropertyReader.Property p in props)
            {
                string name = p.Name;
                switch (name)
                {
                    case "m_nResourceCredits":
                        v = Convert.ToInt32(textBox1.Text);
                        buff = WriteInt(buff, p.offend - 4, v);
                        break;
                    case "m_nResourceGrenades":
                        v = Convert.ToInt32(textBox2.Text);
                        buff = WriteInt(buff, p.offend - 4, v);
                        break;
                    case "m_fResourceMedigel":
                        f = Convert.ToSingle(textBox3.Text);
                        buff = WriteFloat(buff, p.offend - 4, f);
                        break;
                    case "m_fResourceSalvage":
                        f = Convert.ToSingle(textBox4.Text);
                        buff = WriteFloat(buff, p.offend - 4, f);
                        break;
                }
            }
            pcc.Exports[1].Data = buff;
            pcc.SaveToFile("temp.upk");
            Save.ImportME1Package(0, "temp.upk");
            File.Delete("temp.upk");
            v = Convert.ToInt32(textBox5.Text);
            buff = Save.Files[1].memory.ToArray();
            buff = WriteInt(buff, 0x4A7, v);//Paragon
            v = Convert.ToInt32(textBox6.Text);
            buff = WriteInt(buff, 0x4A3, v);//Renegade
            SaveGame.SaveFileEntry e = Save.Files[1];
            e.memory = new MemoryStream(buff);
            Save.Files[1] = e;
        }

        public byte[] WriteInt(byte[] buff, int pos, int v)
        {
            byte[] temp = BitConverter.GetBytes(v);
            for (int i = 0; i < 4; i++)
                buff[pos + i] = temp[i];
            return buff;
        }

        public byte[] WriteFloat(byte[] buff, int pos, float f)
        {
            byte[] temp = BitConverter.GetBytes(f);
            for (int i = 0; i < 4; i++)
                buff[pos + i] = temp[i];
            return buff;
        }

        private void storeSaveGameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Save == null || !Save.Loaded)
                return;
            SaveData();
            Save.Save();
            MessageBox.Show("Done.");
        }

        private void SaveGameOperator_Load(object sender, EventArgs e)
        {

        }

        private void makeBackupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Save.Loaded)
            {
                if (MessageBox.Show("This will save the original loaded file without modifications currently in the value boxes\n\nIf the file was already Saved in the meantime, last Saved file will be backed up. \n\nProceed?", "Yo", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    saveFileDialog1.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    saveFileDialog1.Filter = "*.MassEffectSave|*.MassEffectSave";
                    if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                    {
                        Save.Save(saveFileDialog1.FileName);
                    }
                }

            }
            else
                MessageBox.Show("Please load a save file first!");

        }
    }
}
