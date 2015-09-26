using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Media;
using ME3Explorer.Unreal;

namespace ME3Explorer.Unreal.Classes
{
    public class WwiseStream
    {
        public byte[] memory;
        public int memsize;
        public PCCObject pcc;
        public List<PropertyReader.Property> props;
        public SoundPlayer sp;

        public int DataSize;
        public int DataOffset;
        public int ValueOffset;
        public int Id;
        public string FileName;

        public WwiseStream()
        {
        }
        
        public WwiseStream(PCCObject Pcc, byte[] Raw)
        {
            pcc = Pcc;
            memory = Raw;
            memsize = memory.Length;
            Deserialize();
        }

        public void Deserialize()
        {
            props = PropertyReader.getPropList(pcc, memory);
            int off = props[props.Count - 1].offend + 8;
            ValueOffset = off;
            DataSize = BitConverter.ToInt32(memory, off);
            DataOffset = BitConverter.ToInt32(memory, off + 4);
            for (int i = 0; i < props.Count; i++)
            {
                if (pcc.Names[props[i].Name] == "Filename")
                    FileName = pcc.Names[props[i].Value.IntValue];
                if (pcc.Names[props[i].Name] == "Id")
                    Id = props[i].Value.IntValue;
            }
        }

        public void ExtractToFile(string pathtoafc = "",string name = "",bool askSaveLoc = true)
        {
            if (FileName == "")
                return;
            if (pathtoafc != "")
            {
                if (File.Exists(pathtoafc + FileName + ".afc"))
                    ExtractWav(pathtoafc + FileName + ".afc", DataOffset, DataSize,name,askSaveLoc);
                else
                {
                    OpenFileDialog d = new OpenFileDialog();
                    d.Filter = FileName + ".afc|" + FileName + ".afc";
                    if (d.ShowDialog() == DialogResult.OK)
                        ExtractWav(d.FileName, DataOffset, DataSize, name, askSaveLoc);
                }
            }
            else
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = FileName + ".afc|" + FileName + ".afc";
                if (d.ShowDialog() == DialogResult.OK)
                    ExtractWav(d.FileName, DataOffset, DataSize, name, askSaveLoc);
            }
        }

        public void ImportFromFile(string path, string pathBIO, string pathtoafc = "",bool updateTOC=true)
        {
            if (FileName == "")
                return;
            if (pathtoafc != "")
            {
                if (File.Exists(pathtoafc + FileName + ".afc"))
                    ImportWav(pathtoafc + FileName + ".afc", path, DataOffset, pathBIO, updateTOC);
                else
                {
                    OpenFileDialog d = new OpenFileDialog();
                    d.Filter = FileName + ".afc|" + FileName + ".afc";
                    if (d.ShowDialog() == DialogResult.OK)
                        ImportWav(d.FileName, path, DataOffset, pathBIO, updateTOC);
                }
            }
            else
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = FileName + ".afc|" + FileName + ".afc";
                if (d.ShowDialog() == DialogResult.OK)
                    ImportWav(d.FileName, path, DataOffset, pathBIO, updateTOC);
            }
        }

        public void Play(string pathtoafc = "")
        {
            if (FileName == "")
                return;
            if (pathtoafc != "")
            {
                if (File.Exists(pathtoafc + FileName + ".afc"))
                    PlayWave(pathtoafc + FileName + ".afc", DataOffset, DataSize);
                else
                {
                    OpenFileDialog d = new OpenFileDialog();
                    d.Filter = FileName + ".afc|" + FileName + ".afc";
                    if (d.ShowDialog() == DialogResult.OK)
                        PlayWave(d.FileName, DataOffset, DataSize);
                }
            }
            else
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = FileName + ".afc|" + FileName + ".afc";
                if (d.ShowDialog() == DialogResult.OK)
                    PlayWave(d.FileName, DataOffset, DataSize);
            }
        }

        private void PlayWave(string path, int off, int size)
        {
            if (!File.Exists(path))
                return;
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            if (File.Exists(loc + "\\exec\\out.dat"))
                File.Delete(loc + "\\exec\\out.dat");
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            if (off + size > fs.Length)
                return;
            FileStream fs2 = new FileStream(loc + "\\exec\\out.dat", FileMode.Create, FileAccess.Write);
            fs.Seek(off, SeekOrigin.Begin);
            for (int i = 0; i < size; i++)
                fs2.WriteByte((byte)fs.ReadByte());
            fs.Close();
            fs2.Close();
            System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(loc + "\\exec\\ww2ogg.exe", "out.dat");
            procStartInfo.WorkingDirectory = loc + "\\exec";
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.UseShellExecute = false;
            procStartInfo.CreateNoWindow = true;
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo = procStartInfo;
            proc.Start();
            proc.WaitForExit();
            proc.Close();
            procStartInfo = new System.Diagnostics.ProcessStartInfo(loc + "\\exec\\oggdec.exe", "out.ogg");
            procStartInfo.WorkingDirectory = loc + "\\exec";
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.UseShellExecute = false;
            procStartInfo.CreateNoWindow = true;
            proc = new System.Diagnostics.Process();
            proc.StartInfo = procStartInfo;
            proc.Start();
            proc.WaitForExit();
            if (File.Exists(loc + "\\exec\\out.wav"))
            {
                sp = new SoundPlayer(loc + "\\exec\\out.wav");
                sp.Play();
                while (!sp.IsLoadCompleted)
                    Application.DoEvents();
            }
            File.Delete(loc + "\\exec\\out.ogg");
            File.Delete(loc + "\\exec\\out.dat");
        }

        private void ExtractWav(string path, int off, int size, string name = "",bool askSave = true)
        {
            if (!File.Exists(path))
                return;
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            if (File.Exists(loc + "\\exec\\out.dat"))
                File.Delete(loc + "\\exec\\out.dat");
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            if (off + size > fs.Length)
                return;
            FileStream fs2 = new FileStream(loc + "\\exec\\out.dat", FileMode.Create, FileAccess.Write);
            fs.Seek(off, SeekOrigin.Begin);
            for (int i = 0; i < size; i++)
                fs2.WriteByte((byte)fs.ReadByte());
            fs.Close();
            fs2.Close();
            System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(loc + "\\exec\\ww2ogg.exe", "out.dat");
            procStartInfo.WorkingDirectory = loc + "\\exec";
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.UseShellExecute = false;
            procStartInfo.CreateNoWindow = true;
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo = procStartInfo;
            proc.Start();
            proc.WaitForExit();
            proc.Close();
            procStartInfo = new System.Diagnostics.ProcessStartInfo(loc + "\\exec\\oggdec.exe", "out.ogg");
            procStartInfo.WorkingDirectory = loc + "\\exec";
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.UseShellExecute = false;
            procStartInfo.CreateNoWindow = true;
            proc = new System.Diagnostics.Process();
            proc.StartInfo = procStartInfo;
            proc.Start();
            proc.WaitForExit();
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "Wave Files(*.wav)|*.wav";
            d.FileName = name + ".wav";
            if (askSave)
            {
                if (d.ShowDialog() == DialogResult.OK)
                    File.Copy(loc + "\\exec\\out.wav", d.FileName);
            }
            else
            {                
                File.Copy(loc + "\\exec\\out.wav", name,true);
            }
            File.Delete(loc + "\\exec\\out.ogg");
            File.Delete(loc + "\\exec\\out.dat");
            if(askSave)
                MessageBox.Show("Done.");
        }

        private void ImportWav(string pathafc, string pathwav, int off, string pathBIO, bool updateTOC = true)
        {
            if (!File.Exists(pathafc) || !File.Exists(pathwav))
                return;
            FileStream fs = new FileStream(pathafc, FileMode.Open, FileAccess.Read);
            byte[] Header = new byte[94];
            fs.Seek(DataOffset, SeekOrigin.Begin);
            for (int i = 0; i < 94; i++)
                Header[i] = (byte)fs.ReadByte();
            fs.Close();
            fs = new FileStream(pathwav, FileMode.Open, FileAccess.Read);
            byte[] newfile = new byte[fs.Length];
            for (int i = 0; i < fs.Length; i++)
                newfile[i] = (byte)fs.ReadByte();
            fs.Close();
            newfile = ModifyHeader(newfile, Header);
            fs = new FileStream(pathafc, FileMode.Append, FileAccess.Write, FileShare.Write);
            int newoff = (int)fs.Length;
            int newsize = newfile.Length;
            for (int i = 0; i < newsize; i++)
                fs.WriteByte(newfile[i]);
            uint newafcsize = (uint)fs.Length;
            fs.Close();
            byte[] buff = BitConverter.GetBytes(newsize);
            for (int i = 0; i < 4; i++)
                memory[ValueOffset + i - 4] = buff[i];
            for (int i = 0; i < 4; i++)
                memory[ValueOffset + i] = buff[i];
            buff = BitConverter.GetBytes(newoff);
            for (int i = 0; i < 4; i++)
                memory[ValueOffset + i + 4] = buff[i];
            DataSize = newsize;
            DataOffset = newoff;
            TOCeditor tc = new TOCeditor();
            string s = Path.GetFileName(pathafc);
            if (updateTOC)
                if (!tc.UpdateFile("\\" + s, newafcsize, pathBIO + "PCConsoleTOC.bin"))
                    MessageBox.Show("Didn't found Entry!");
        }

        private byte[] ModifyHeader(byte[] nw, byte[] old)
        {
            MemoryStream m = new MemoryStream();
            m.Write(nw, 0, 8);
            m.Write(old, 8, 14);
            m.Write(nw, 22, 10);
            m.Write(old, 32, 8);
            m.Write(nw, 40, 4);
            int len = nw.Length - 52;
            m.Write(nw, 52, len);
            return m.ToArray();
        }
    }
}
