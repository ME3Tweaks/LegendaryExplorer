using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer.UnrealHelper
{
    public class AFCFile
    {
        public AFCFile()
        {
        }

        public void ExtractWav(string path,int off, int size)
        {
            if (!File.Exists(path))
                return;
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            if (File.Exists(loc + "\\exec\\out.dat"))
                File.Delete(loc + "\\exec\\out.dat");
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            if(off+size >= fs.Length)
                return;
            FileStream fs2 = new FileStream(loc + "\\exec\\out.dat", FileMode.Create, FileAccess.Write);
            fs.Seek(off,SeekOrigin.Begin);
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
            if (d.ShowDialog() == DialogResult.OK)
                File.Copy(loc + "\\exec\\out.wav", d.FileName);
            File.Delete(loc + "\\exec\\out.ogg");
            File.Delete(loc + "\\exec\\out.dat");
            MessageBox.Show("Done.");
        }
    }
}
