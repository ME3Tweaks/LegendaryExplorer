using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Security.Cryptography;
using Microsoft.Win32;
using System.IO;
using ME2Explorer.Helper;
using Gibbed.IO;
using KFreonLib.MEDirectories;

namespace ME2Explorer.DLC_Crack
{
    public partial class GiveEntitlements : Form
    {
        public GiveEntitlements()
        {
            InitializeComponent();
        }

        private void WriteLine(string ln)
        {
            rtb1.Text += ln + "\n";
        }

        private void WriteLine()
        {
            WriteLine("");
        }

        private void WriteLine(string ln, string rplce1)
        {
            WriteLine(ln.Replace("{0}", rplce1));
        }

        private void WriteLine(string ln, string rplce1, string rplce2)
        {
            WriteLine(ln.Replace("{1}", rplce1).Replace("{0}", rplce2));
        }

        public void runCrackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WriteLine("Giving ME2 DLC Entitlements");
            WriteLine("Based upon the cracks by Illiria and Loghain");
            WriteLine();
            BitConverter.IsLittleEndian = true;

            if (String.IsNullOrEmpty(ME2Directory.DLCPath) || !Directory.Exists(ME2Directory.DLCPath))
            {
                MessageBox.Show("Could not find Mass Effect 2 DLC directory.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            WriteLine("Looking for DLC...");
            StringWriter writer = new StringWriter();
            writer.WriteLine("[Hash]");
            foreach (string dlc in Directory.GetDirectories(ME2Directory.DLCPath))
            {
                string mountFile = Path.Combine(Path.Combine(dlc, "CookedPC"), "Mount.dlc");
                if (File.Exists(mountFile))
                {
                    string dlcName = null;
                    uint dlcNum;
                    using (FileStream fs = new FileStream(mountFile, FileMode.Open, FileAccess.Read))
                    {
                        fs.Seek(0xC, SeekOrigin.Begin);
                        byte[] buff = fs.ReadBytes(4);
                        dlcNum = BitConverter.ToUInt32(buff, 0);
                        fs.Seek(0x2C, SeekOrigin.Begin);
                        buff = fs.ReadBytes(4);
                        int strlen = BitConverter.ToInt32(buff, 0);
                        if (strlen > 0 && strlen < 0x100)
                        {
                            buff = fs.ReadBytes(strlen - 1);
                            dlcName = Encoding.ASCII.GetString(buff);
                        }
                    }
                    if (!String.IsNullOrEmpty(dlcName))
                        WriteLine("Found dlc: " + dlcName + " ( " + Path.GetFileName(dlc) + " ) ");
                    else
                        WriteLine("Found dlc: " + Path.GetFileName(dlc));

                    writer.WriteLine("GiveMeDLC.Mount{0}={1}", dlcNum, ComputeDLCHash(Path.Combine(dlc, "CookedPC")));
                }
                Application.DoEvents();
            }
            WriteLine();
            writer.WriteLine();
            writer.WriteLine("[Global]");
            writer.WriteLine("LastNucleusID=GiveMeDLC");
            writer.WriteLine();
            writer.WriteLine("[KeyValuePair]");
            writer.WriteLine("GiveMeDLC.Entitlement.ME2PCOffers.ONLINE_ACCESS=TRUE");
            writer.WriteLine("GiveMeDLC.Entitlement.ME2PCOffers.PC_CERBERUS_NETWORK=TRUE");
            writer.WriteLine("GiveMeDLC.Numeric.DaysSinceReg=0");
            writer.WriteLine();
            WriteLine("Saving BioPersistentEntitlementCache.ini...");
            byte[] bytes = Encoding.Unicode.GetBytes(writer.GetStringBuilder().ToString());
            byte[] magic = IPEncryptor();
            if (magic == null)
            {
                MessageBox.Show("Could not get adapter entropy.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            }
            else
            {
                bytes = DataProtection.Encrypt(bytes, magic);
                if (bytes == null)
                {
                    MessageBox.Show("Could not encrypt entitlement cache.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                }
                else
                {
                    string str8 = Path.Combine(Path.Combine(Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "BioWare"), "Mass Effect 2"), "BIOGame"), "Config");
                    if (!Directory.Exists(str8))
                    {
                        Directory.CreateDirectory(str8);
                    }
                    string str9 = Path.Combine(str8, "BioPersistentEntitlementCache.ini");
                    if (!File.Exists(str9) || (MessageBox.Show(string.Format("Overwrite \"{0}\"?", str9), "Question", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes))
                    {
                        Stream stream2 = File.Open(str9, FileMode.Create, FileAccess.Write);
                        stream2.Write(bytes, 0, bytes.Length);
                        stream2.Close();
                        WriteLine();
                        WriteLine("DONE!");
                    }
                    else
                        WriteLine("Not saved");
                }
            }
        }

        private byte[] IPEncryptor()
        {
            byte[] destinationArray = new byte[8];
            List<IpHelp.Native.IP_ADAPTER_INFO> adaptersInfo = IpHelp.GetAdaptersInfo();
            if (adaptersInfo == null)
            {
                return null;
            }
            foreach (IpHelp.Native.IP_ADAPTER_INFO ip_adapter_info in adaptersInfo)
            {
                if (ip_adapter_info.AddressLength <= destinationArray.Length)
                {
                    Array.Copy(ip_adapter_info.Address, 0, destinationArray, 0, (long)ip_adapter_info.AddressLength);
                }
            }
            destinationArray[0] = (byte)(destinationArray[0] ^ 0x65);
            destinationArray[1] = (byte)(destinationArray[1] ^ 0x6f);
            destinationArray[2] = (byte)(destinationArray[2] ^ 0x4a);
            destinationArray[4] = (byte)(destinationArray[4] ^ 0x66);
            destinationArray[5] = (byte)(destinationArray[5] ^ 0x61);
            destinationArray[6] = (byte)(destinationArray[6] ^ 0x72);
            destinationArray[7] = (byte)(destinationArray[7] ^ 0x47);
            return destinationArray;
        }

        private string ComputeDLCHash(string basePath)
        {

            using (SHA1 sha = SHA1.Create())
            {
                sha.Initialize();
                byte[] buff = new byte[0x1000];
                string[] files = Directory.GetFiles(basePath);

                IEnumerable<string> filesToHash = from file in Directory.GetFiles(basePath) where ShouldHashPath(file) orderby file.ToUpperInvariant() select file;
                foreach (string file in filesToHash)
                {
                    using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                    {
                        int inputCount = fs.Read(buff, 0, buff.Length);
                        sha.TransformBlock(buff, 0, inputCount, null, 0);
                    }
                }
                sha.TransformFinalBlock(buff, 0, 0);
                return BitConverter.ToString(sha.Hash).Replace("-", "").ToLower();
            }
        }

        private bool ShouldHashPath(string filePath)
        {
            if (filePath.EndsWith(".pcc") || filePath.EndsWith(".ini"))
            {
                if (filePath.Length < 8)
                {
                    return true;
                }
                char ch = filePath[filePath.Length - 8];
                if (ch != '_')
                {
                    return true;
                }
                string str = filePath.Substring(filePath.Length - 7, 3).ToUpperInvariant();
                for (int i = 0; i < str.Length; i++)
                {
                    if ((str[i] < 'A') || (str[i] > 'Z'))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void richTextBox1_KeyDown(object sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
        }

        private void backupFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WriteLine("Attempting to backup existing \"BioPersistentEntitlementCache.ini\" file...\n");
            string path = Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "BioWare"), "Mass Effect 2"), "BIOGame"), "Config"), "BioPersistentEntitlementCache.ini");
            if (!File.Exists(path))
            {
                WriteLine("File does not exist! Exiting...");
                MessageBox.Show("Cache file doesn't exist. No extra file created", "Invalid operation", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            WriteLine("File found! Copying existing file to \"BioPersistentEntitlementCacheBackup.ini\"...");
            File.Copy(path, Path.Combine(Path.GetDirectoryName(path), "BioPersistentEntitlementCacheBackup.ini"), true);
            WriteLine("Operation successful!");
        }
    }
}
