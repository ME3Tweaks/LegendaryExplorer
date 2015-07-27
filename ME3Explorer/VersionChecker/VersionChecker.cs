using KFreonLib.MEDirectories;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer.VersionChecker
{
    public partial class VersionChecker : Form
    {
        public struct KeyDescPair
        {
            public string Key;
            public string Desc;
        }

        public List<KeyDescPair> Versions;

        public VersionChecker()
        {
            InitializeComponent();
        }

        private void VersionChecker_Load(object sender, EventArgs e)
        {
            Versions = new List<KeyDescPair>();
            Versions.Add(newKey("ef390c9ce1c007feddc7a2194623452a", "Mass Effect original 1.0.5427.1"));
            Versions.Add(newKey("00a118c6af390f86836ddcde93e26cb6", "Mass Effect original 1.0.5427.1 DLC one"));
            Versions.Add(newKey("7205949a1728cb11a68fb26e6cd6770a", "Mass Effect original 1.2.5427.16 2nd Patch"));
            Versions.Add(newKey("d5ed593c05d477d7422d2ae85a003b5d", "Mass Effect original 1.3.5427.46 3rd Patch"));
            Versions.Add(newKey("3bd1a01979478587f28949b7e78194c2", "Mass Effect, crack no Origin"));
            Versions.Add(newKey("4160720f4ede5231b98bab686edeb411", "Mass Effect, crack no DLC check"));
            RefreshLists();
        }

        public void RefreshLists()
        {
            listBox1.Items.Clear();
            listBox2.Items.Clear();
            for (int i = 0; i < Versions.Count(); i++)
            {
                string s = Versions[i].Key + " : " + Versions[i].Desc;
                listBox1.Items.Add(s);
                listBox2.Items.Add(s);
            }
        }

        public KeyDescPair newKey(string md5, string desc)
        {
            KeyDescPair k = new KeyDescPair();
            k.Key = md5;
            k.Desc = desc;
            return k;
        }

        private void checkVersionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = ME3Directory.gamePath;
            if (path != "")
            {
                path += "Binaries\\Win32\\MassEffect3.exe";
                string hash = MD5Hash.FromFile(path);
                bool found = false;
                for (int i = 0; i < Versions.Count(); i++)
                    if (hash == Versions[i].Key)
                    {
                        RefreshLists();
                        listBox1.SelectedIndex = i;
                        found = true;
                        MessageBox.Show("Your version is: " + Versions[i].Desc);
                    }
                if (!found)
                {
                    RefreshLists();
                    listBox1.Items.Add(hash + " : Unknown Version, please report md5");
                    listBox1.SelectedIndex = Versions.Count();
                    System.Windows.Forms.Clipboard.SetText(hash);
                    MessageBox.Show("Your version is unknown, please report the md5 string, its in the clipboard now");
                }
            }
        }
    }
}
