using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using BitConverter = KFreonLib.Misc.BitConverter;
using KFreonLib.PCCObjects;
using KFreonLib.Debugging;
using KFreonLib.Misc;

namespace KFreonLib.Scripting
{
    public class BasicFunction
    {
        public void DebugPrintln(string s)
        {
            DebugOutput.PrintLn(s);
        }

        public void DebugPrint(string s)
        {
            DebugOutput.Print(s);
        }

        public void DebugClear()
        {
            DebugOutput.Clear();
        }

        public void ShowMessage(string msg)
        {
            MessageBox.Show(msg);
        }

        public string GetStringInput(string message, string preset)
        {
            return Microsoft.VisualBasic.Interaction.InputBox(message, "MEExplorer", preset, 0, 0);
        }

        public void ShowError(string msg)
        {
            MessageBox.Show(msg, "MEExplorer Exception Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public bool AskYN(string question, string title)
        {
            DialogResult r = MessageBox.Show(question, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r == DialogResult.Yes)
                return true;
            else
                return false;
        }

        public string OpenFile(string name = "", string path = "", string filter = "all|*.*")
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = filter;
            if (path != "")
                d.FileName = path + name;
            else
                d.FileName = name;
            d.ShowDialog();
            return d.FileName;
        }

        public string SaveFile(string name = "", string path = "", string filter = "all|*.*")
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = filter;
            if (path != "")
                d.FileName = path + name;
            else
                d.FileName = name;
            d.ShowDialog();
            return d.FileName;
        }

        public int FileSize(string path)
        {
            if (!File.Exists(path))
                return -1;
            FileStream f = new FileStream(path, FileMode.Open, FileAccess.Read);
            int len = (int)f.Length;
            f.Close();
            return len;
        }

        public void DecompressPCC(string path)
        {
            if (!File.Exists(path))
                return;
            PCCObjects.Misc.PCCDecompress(path);
            string filen = Path.GetFileName(path);
            FileStream fs2 = new FileStream(path, FileMode.Open, FileAccess.Read);
            uint fsize = (uint)fs2.Length;
            fs2.Close();
            TOCeditor tc = new TOCeditor();
            if (!tc.UpdateFile(filen, fsize))
                MessageBox.Show("Didn't found Entry");
            tc.Close();
        }

        public string getExecPath()
        {
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            return loc + "\\exec\\";
        }

        public void LoadPcc(IPCCObject pcc, string path)
        {
            switch (pcc.GameVersion)
            {
                case 1:
                    pcc = new ME1PCCObject(path);
                    break;
                case 2:
                    pcc = new ME2PCCObject(path);
                    break;
                case 3:
                    pcc = new ME3PCCObject(path);
                    break;
            }
        }

        public string DetectTexType(string path)
        {
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            BitConverter.IsLittleEndian = true;
            byte[] buff = new byte[4];
            for (int i = 0; i < 4; i++)
                buff[i] = (byte)fs.ReadByte();
            int type = BitConverter.ToInt32(buff, 0);
            string newfile = Path.GetDirectoryName(path) + "\\";
            if (type == 0)
            {
                newfile += "data.dds";
            }
            else
            {
                newfile += "data.tga";
            }
            if (File.Exists(newfile))
                File.Delete(newfile);
            FileStream fs2 = new FileStream(newfile, FileMode.Create, FileAccess.Write);
            for (int i = 0; i < fs.Length - 4; i++)
                fs2.WriteByte((byte)fs.ReadByte());
            fs.Close();
            fs2.Close();
            return Path.GetFileName(newfile);
        }
    }
}
