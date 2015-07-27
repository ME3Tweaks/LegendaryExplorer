using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace ME3Explorer.DLLInjector
{
    public partial class DLLInjector : Form
    {
        #region include
        [DllImport("kernel32")]
        public static extern IntPtr CreateRemoteThread(
          IntPtr hProcess,
          IntPtr lpThreadAttributes,
          uint dwStackSize,
          UIntPtr lpStartAddress, // raw Pointer into remote process
          IntPtr lpParameter,
          uint dwCreationFlags,
          out IntPtr lpThreadId
        );

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(
            UInt32 dwDesiredAccess,
            Int32 bInheritHandle,
            Int32 dwProcessId
            );

        [DllImport("kernel32.dll")]
        public static extern Int32 CloseHandle(
        IntPtr hObject
        );

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool VirtualFreeEx(
            IntPtr hProcess,
            IntPtr lpAddress,
            UIntPtr dwSize,
            uint dwFreeType
            );

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        public static extern UIntPtr GetProcAddress(
            IntPtr hModule,
            string procName
            );

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern IntPtr VirtualAllocEx(
            IntPtr hProcess,
            IntPtr lpAddress,
            uint dwSize,
            uint flAllocationType,
            uint flProtect
            );

        [DllImport("kernel32.dll")]
        static extern bool WriteProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            string lpBuffer,
            UIntPtr nSize,
            out IntPtr lpNumberOfBytesWritten
        );

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(
            string lpModuleName
            );

        [DllImport("kernel32", SetLastError = true, ExactSpelling = true)]
        internal static extern Int32 WaitForSingleObject(
            IntPtr handle,
            Int32 milliseconds
            );
        #endregion

        public struct OffsetEntry
        {
            public string md5;
            public uint offset;
        }

        public List<OffsetEntry> Offsets;

        public List<int> IDs;
        public int SelectedID;
        public TcpClient client;
        public string ProcessPath;
        public string CurrentMD5;
        NetworkStream MyStream;
        BackgroundWorker bw;
             
        public DLLInjector()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            RefreshList();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.dll|*.dll";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                textBox1.Text = d.FileName;
        }

        private void DLLInjector_Resize(object sender, EventArgs e)
        {
            try
            {
                comboBox1.Width = this.Width - comboBox1.Left - 20;
                button4.Left = this.Width - button4.Width - 20;
                textBox1.Width = button4.Left - textBox1.Left - 10;
                rtb1.Width = this.Width - rtb1.Left - 20;
                rtb1.Height = this.Height - rtb1.Top - 60;
                button3.Top = rtb1.Top + rtb1.Height + 5;
                button3.Left = this.Width - button3.Width - 20;
                textBox2.Width = button3.Left - textBox2.Left - 10;
                textBox2.Top = button3.Top;
            }
            catch
            {
            }
        }

        private void DLLInjector_Load(object sender, EventArgs e)
        {
            ReadOffsets();
        }

        public void ReadOffsets()
        {
            try
            {
                string path = Path.GetDirectoryName(Application.ExecutablePath) + "\\exec\\offsets.txt";
                if (!File.Exists(path))
                {
                    Println("No offsets.txt found!");
                    return;
                }
                string s = System.IO.File.ReadAllText(path);
                string[] entries = s.Split(';');
                Offsets = new List<OffsetEntry>();
                foreach (string e in entries)
                {
                    string[] parms = e.Split(',');
                    if (parms.Length == 2)
                    {
                        parms[0] = parms[0].Trim();
                        parms[1] = parms[1].Trim();
                        Println("Found MD5 : " + parms[0] + " @ 0x" + parms[1]);
                        OffsetEntry oe = new OffsetEntry();
                        oe.md5 = parms[0];
                        oe.offset = (uint)Int32.Parse(parms[1], System.Globalization.NumberStyles.HexNumber);
                        Offsets.Add(oe);
                    }
                }
            }
            catch (Exception ex)
            {
                Println(ex.Message);
            }
        }

        public void InjectDLL(IntPtr hProcess, String strDLLName)
        {
            IntPtr bytesout;
            Int32 LenWrite = strDLLName.Length + 1;
            IntPtr AllocMem = (IntPtr)VirtualAllocEx(hProcess, (IntPtr)null, (uint)LenWrite, 0x1000, 0x40); //allocation pour WriteProcessMemory
            WriteProcessMemory(hProcess, AllocMem, strDLLName, (UIntPtr)LenWrite, out bytesout);
            UIntPtr Injector = (UIntPtr)GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
            if (Injector == null)
            {
                MessageBox.Show(" Injector Error! \n ");
                return;
            }
            IntPtr hThread = (IntPtr)CreateRemoteThread(hProcess, (IntPtr)null, 0, Injector, AllocMem, 0, out bytesout);
            if (hThread == null)
            {
                MessageBox.Show(" hThread [ 1 ] Error! \n ");
                return;
            }
            uint Result = (uint)WaitForSingleObject(hThread, 10 * 1000);
            if (Result == 0x00000080L || Result == 0x00000102L || Result == 0xFFFFFFFF)
            {
                MessageBox.Show(" hThread [ 2 ] Error! \n ");
                if (hThread != null)
                    CloseHandle(hThread);
                return;
            }
            Thread.Sleep(1000);
            VirtualFreeEx(hProcess, AllocMem, (UIntPtr)0, 0x8000);
            if (hThread != null)
                CloseHandle(hThread);
            CreateClient();
            return;
        }

        public void RefreshList()
        {
            comboBox1.Items.Clear();
            IDs = new List<int>();
            Process[] ProcList;
            ProcList = Process.GetProcesses();
            int f = -1;
            int count = 0;
            foreach (Process p in ProcList)
            {
                comboBox1.Items.Add(p.Id + " - " + p.ProcessName);
                IDs.Add(p.Id);
                if (p.ProcessName == "MassEffect3")// || p.ProcessName == "notepad")
                    f = count;
                count++;
            }
            if (f != -1)
                comboBox1.SelectedIndex = f;
        }

        public string GetMD5(string fileName)
        {
            FileStream file = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] retVal = md5.ComputeHash(file);
            file.Close();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < retVal.Length; i++)
                sb.Append(retVal[i].ToString("X2"));
            return sb.ToString();
        }

        public bool CreateConf()
        {
            uint off = 0;
            foreach (OffsetEntry o in Offsets)
                if (o.md5 == CurrentMD5)
                    off = o.offset;
            if (off == 0)
            {
                Println("Offset not found in database, please visit forum.");
                return false;
            }
            FileStream fs = new FileStream(ProcessPath + "conf.bin", FileMode.Create, FileAccess.Write);
            fs.WriteByte((byte)'W');
            fs.WriteByte((byte)'V');
            BitConverter.IsLittleEndian = true;
            fs.Write(BitConverter.GetBytes(off), 0, 4);
            fs.Close();
            return true;
        }

        public void CreateClient()
        {
            Println("Start client...");
            client = new TcpClient("127.0.0.1", 28999);
            Println("Connection : " + client.Connected);
            MyStream = client.GetStream();
            bw = new BackgroundWorker();
            bw.WorkerSupportsCancellation = true;
            bw.WorkerReportsProgress = true;
            bw.DoWork += new DoWorkEventHandler(Listener);
            bw.RunWorkerAsync();
        }

        private void Listener(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            byte[] buff = new byte[70000];
            int count;
            while (true)
                try
                {
                    while ((count = MyStream.Read(buff, 0, buff.Length)) != 0)
                    {
                        Println(Encoding.ASCII.GetString(buff));
                        MyStream.WriteByte(32);
                    }
                }
                catch (Exception ex)
                {
                }
        }

        public void Println(string s)
        {
            if (rtb1 != null)
            {
                Action action = () => rtb1.Text += s + "\n";
                rtb1.Invoke(action);
                action = () => rtb1.SelectionStart = rtb1.TextLength;
                rtb1.Invoke(action);
                action = () => rtb1.ScrollToCaret();
                rtb1.Invoke(action);
            }
        }

        public void SendCommand()
        {
            byte[] buff = Encoding.ASCII.GetBytes(textBox2.Text);
            MyStream.Write(buff, 0, buff.Length);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Int32 ProcID = IDs[SelectedID];
            if (ProcID >= 0)
            {
                IntPtr hProcess = (IntPtr)OpenProcess(0x1F0FFF, 1, ProcID);
                Process proc = Process.GetProcessById(ProcID);
                ProcessPath = Path.GetDirectoryName(proc.Modules[0].FileName) + "\\";
                if (hProcess == null)
                {
                    Println("OpenProcess() Failed!");
                    return;
                }
                else
                {
                    if (CreateConf())
                    {
                        InjectDLL(hProcess, textBox1.Text);
                        Println("Attaching Succeeded!");
                    }
                }
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                int n = comboBox1.SelectedIndex;
                if (n == -1)
                    return;
                Int32 ProcID = IDs[n];
                if (ProcID >= 0)
                {
                    IntPtr hProcess = (IntPtr)OpenProcess(0x1F0FFF, 1, ProcID);
                    Process proc = Process.GetProcessById(ProcID);
                    if (hProcess == null)
                    {
                        Println("OpenProcess() Failed!");
                        return;
                    }
                    CurrentMD5 = GetMD5(proc.Modules[0].FileName);
                    Println("MD5 is : " + CurrentMD5);
                }
                SelectedID = n;
            }
            catch (Exception ex)
            {
                Println(ex.Message);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            SendCommand();
        }

        private void textBox2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
                SendCommand();
        }

    }
}
