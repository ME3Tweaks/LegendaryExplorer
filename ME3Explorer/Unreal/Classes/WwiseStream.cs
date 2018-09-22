using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Media;
using ME3Explorer.Unreal;
using ME3Explorer.Packages;

namespace ME3Explorer.Unreal.Classes
{
    public class WwiseStream
    {
        public byte[] memory;
        public int memsize;
        int Index;
        public SoundPlayer sp;

        public int DataSize;
        public int DataOffset;
        public int ValueOffset;
        public int Id;
        public string FileName;

        public bool IsPCCStored { get { return FileName == null; } }

        public WwiseStream()
        {
        }

        public WwiseStream(IExportEntry export)
        {
            Index = export.Index;
            memory = export.Data;
            memsize = memory.Length;
            Deserialize(export.FileRef as ME3Package);
        }

        public WwiseStream(ME3Package pcc, int index)
        {
            Index = index;
            memory = pcc.Exports[Index].Data;
            memsize = memory.Length;
            Deserialize(pcc);
        }

        public void Deserialize(ME3Package pcc)
        {
            PropertyCollection properties = pcc.Exports[Index].GetProperties();
            int off = pcc.Exports[Index].propsEnd() + 8;
            var propold = PropertyReader.getPropList(pcc.Exports[Index]);
            int offold = propold[propold.Count - 1].offend + 8;
            ValueOffset = off;
            DataSize = BitConverter.ToInt32(memory, off);
            DataOffset = BitConverter.ToInt32(memory, off + 4);
            FileName = properties.GetProp<NameProperty>("Filename").Value;
            Id = properties.GetProp<IntProperty>("Id");
            /*for (int i = 0; i < props.Count; i++)
            {
                if (pcc.Names[props[i].Name] == "Filename")
                    FileName = pcc.Names[props[i].Value.IntValue];
                if (pcc.Names[props[i].Name] == "Id")
                    Id = props[i].Value.IntValue;
            }*/
        }

        public void ExtractToFile(string pathtoafc = "", string name = "", bool askSaveLoc = true)
        {
            if (FileName == "")
                return;
            if (FileName == null)
            {
                ExtractWav(pathtoafc, name, askSaveLoc);
            }
            else if (pathtoafc != "")
            {
                if (File.Exists(pathtoafc + FileName + ".afc"))
                    ExtractWav(pathtoafc + FileName + ".afc", name, askSaveLoc);
                else
                {
                    OpenFileDialog d = new OpenFileDialog();
                    d.Filter = FileName + ".afc|" + FileName + ".afc";
                    if (d.ShowDialog() == DialogResult.OK)
                        ExtractWav(d.FileName, name, askSaveLoc);
                }
            }
            else
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = FileName + ".afc|" + FileName + ".afc";
                if (d.ShowDialog() == DialogResult.OK)
                    ExtractWav(d.FileName, name, askSaveLoc);
            }
        }

        public void ImportFromFile(string path, string pathtoafc = "")
        {
            if (FileName == "")
                return;
            if (pathtoafc != "")
            {
                if (File.Exists(pathtoafc + FileName + ".afc"))
                    ImportWav(pathtoafc + FileName + ".afc", path, DataOffset);
                else
                {
                    OpenFileDialog d = new OpenFileDialog();
                    d.Filter = FileName + ".afc|" + FileName + ".afc";
                    if (d.ShowDialog() == DialogResult.OK)
                        ImportWav(d.FileName, path, DataOffset);
                }
            }
            else
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = FileName + ".afc|" + FileName + ".afc";
                if (d.ShowDialog() == DialogResult.OK)
                    ImportWav(d.FileName, path, DataOffset);
            }
        }

        public Stream GetPCMStream(string path)
        {
            if (!File.Exists(path))
                return null;
            //string loc = Path.GetDirectoryName(Application.ExecutablePath) + "\\exec";
            Stream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            if (path.EndsWith(".pcc"))
            {
                using (ME3Package package = MEPackageHandler.OpenME3Package(path))
                {
                    if (package.IsCompressed)
                    {
                        Stream result = CompressionHelper.DecompressME3(fs);
                        fs.Dispose();
                        fs = result;
                    }
                }
            }
            if (DataOffset + DataSize > fs.Length)
                return null;

            string basePath = System.IO.Path.GetTempPath() + "ME3EXP_SOUND_" + Guid.NewGuid().ToString(); //todo: clean these upon program exit or boot, not sure which

            ExtractRawFromStream(fs, basePath);
            fs.Dispose();

            ConvertRiffToWav(basePath);
            if (File.Exists(basePath + ".wav"))
            {
                byte[] pcmBytes = File.ReadAllBytes(basePath + ".wav");
                return new MemoryStream(pcmBytes);
            }
            return null;
        }

        public void Play(string afcPath = "")
        {
            if (FileName == "")
                return;
            if (FileName == null)
            {
                PlayWave(afcPath);
            }
            else if (afcPath != "")
            {
                if (File.Exists(afcPath + FileName + ".afc"))
                    PlayWave(afcPath + FileName + ".afc");
                else
                {
                    OpenFileDialog d = new OpenFileDialog();
                    d.Filter = FileName + ".afc|" + FileName + ".afc";
                    if (d.ShowDialog() == DialogResult.OK)
                        PlayWave(d.FileName);
                }
            }
            else
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = FileName + ".afc|" + FileName + ".afc";
                if (d.ShowDialog() == DialogResult.OK)
                    PlayWave(d.FileName);
            }
        }

        /// <summary>
        /// Creates wav file in temp directory
        /// </summary>
        /// <param name="afcPath"></param>
        /// <returns></returns>
        private string CreateWave(string afcPath)
        {
            if (!File.Exists(afcPath))
                return null;
            Stream fs = new FileStream(afcPath, FileMode.Open, FileAccess.Read);
            if (afcPath.EndsWith(".pcc"))
            {
                using (ME3Package package = MEPackageHandler.OpenME3Package(afcPath))
                {
                    if (package.IsCompressed)
                    {
                        Stream result = CompressionHelper.DecompressME3(fs);
                        fs.Dispose();
                        fs = result;
                    }
                }
            }
            if (DataOffset + DataSize > fs.Length)
                return null;

            string basePath = System.IO.Path.GetTempPath() + "ME3EXP_SOUND_" + Guid.NewGuid().ToString();

            ExtractRawFromStream(fs, basePath);
            fs.Dispose();

            ConvertRiffToWav(basePath);
            return basePath + ".wav";
        }

        private void PlayWave(string path)
        {
            string wavPath = CreateWave(path);
            if (wavPath != null && File.Exists(wavPath))
            {
                sp = new SoundPlayer(wavPath);
                sp.Play();
                while (!sp.IsLoadCompleted)
                    Application.DoEvents();
            }
        }

        private void ExtractWav(string path, string name = "", bool askSave = true)
        {
            string wavPath = CreateWave(path);
            if (wavPath != null && File.Exists(wavPath))
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "Wave Files(*.wav)|*.wav";
                d.FileName = name + ".wav";
                if (askSave)
                {
                    if (d.ShowDialog() == DialogResult.OK)
                        File.Copy(wavPath, d.FileName);
                }
                else
                {
                    File.Copy(wavPath, name, true);
                }
                if (askSave)
                    MessageBox.Show("Done.");
            }
        }

        private static void ConvertRiffToWav(string basePath)
        {
            //convert RIFF to OGG
            string loc = Path.GetDirectoryName(Application.ExecutablePath) + "\\exec";
            System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(loc + "\\ww2ogg.exe", basePath + ".dat");
            procStartInfo.WorkingDirectory = loc;
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.UseShellExecute = false;
            procStartInfo.CreateNoWindow = true;
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo = procStartInfo;
            proc.Start();
            proc.WaitForExit();
            proc.Close();

            //convert OGG to WAV
            procStartInfo = new System.Diagnostics.ProcessStartInfo(loc + "\\oggdec.exe", basePath + ".ogg");
            procStartInfo.WorkingDirectory = loc;
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.UseShellExecute = false;
            procStartInfo.CreateNoWindow = true;
            proc = new System.Diagnostics.Process();
            proc.StartInfo = procStartInfo;
            proc.Start();
            proc.WaitForExit();
            File.Delete(basePath + ".ogg");
            File.Delete(basePath + ".dat");
        }

        private void ExtractRawFromStream(Stream fs, string outputFile)
        {
            string outfile = outputFile + ".dat";
            if (File.Exists(outfile))
                File.Delete(outfile);
            FileStream fs2 = new FileStream(outfile, FileMode.Create, FileAccess.Write);
            fs.Seek(DataOffset, SeekOrigin.Begin);
            for (int i = 0; i < DataSize; i++)
                fs2.WriteByte((byte)fs.ReadByte());
            fs.Close();
            fs2.Close();
        }

        private MemoryStream ExtractRiffFromStream(Stream fs)
        {
            MemoryStream fs2 = new MemoryStream();
            fs.Seek(DataOffset, SeekOrigin.Begin);
            for (int i = 0; i < DataSize; i++)
                fs2.WriteByte((byte)fs.ReadByte());
            fs.Close();
            return fs2;
        }

        private void ImportWav(string pathafc, string pathwav, int off)
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
