using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.UnrealHelper;

namespace ME3Explorer
{
    public partial class Language_Editor : Form
    {
        public Languages lang;
        public Language_Editor()
        {
            InitializeComponent();
        }

        private void Language_Editor_Load(object sender, EventArgs e)
        {
            if (lang == null) return;
            if (lang.GlobalLang == null) lang.CreateDefaultLang();
            RefreshList();   
        }

        public void RefreshList()
        {
            listBox1.Items.Clear();
            for (int i = 0; i < lang.GlobalLang.Count; i++)
                if(i == lang.CurrentLanguage)
                    listBox1.Items.Add("*" + lang.GlobalLang[i].name);
                else
                    listBox1.Items.Add(lang.GlobalLang[i].name);
        }
        public void RefreshList2()
        {
            int n = listBox1.SelectedIndex;
            int m = listBox2.SelectedIndex;
            if (n == -1) return;
            listBox2.Items.Clear();
            for (int i = 0; i < lang.GlobalLang[n].Entries.Count; i++)
                listBox2.Items.Add(lang.GlobalLang[n].Entries[i]);
            listBox2.SelectedIndex = m;
        }
        
        private void copyLanguagToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1) return;
            Languages.Language l = new Languages.Language();
            l.name = lang.GlobalLang[n].name;
            l.Entries = new List<string>();
            for (int i = 0; i < lang.GlobalLang[n].Entries.Count; i++)
                l.Entries.Add(lang.GlobalLang[n].Entries[i]);
                lang.GlobalLang.Add(l);
            RefreshList();
        }

        private void deleteLanguageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1) return;
            lang.GlobalLang.Remove(lang.GlobalLang[n]);
            RefreshList();
        }

        private void editEntryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1) return;
            int m = listBox2.SelectedIndex;
            if (m == -1) return;
            string name = lang.GlobalLang[n].Entries[m];
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new name", "ME3 Explorer", name, 0, 0);
            if (result != "")
            {
                lang.GlobalLang[n].Entries[m] = result;
                RefreshList2();
            }
        }

        private void listBox1_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1) return;
            RefreshList2();
        }

        private void renameLanguageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1) return;
            string name = lang.GlobalLang[n].name;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new name", "ME3 Explorer", name, 0, 0);
            if (result != "")
            {
                Languages.Language l = lang.GlobalLang[n];
                l.name = result;
                lang.GlobalLang[n] = l;
                RefreshList();
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveLanguages();
            MessageBox.Show("Done.");    
        }

        private void SaveLanguages()
        {
            string loc = Path.GetDirectoryName(Application.ExecutablePath) + "\\exec\\languages.xml";
            TreeNode r = lang.ToTree();
            TreeViewSerializer ts = new TreeViewSerializer();
            ts.SerializeTreeView(r, loc);
        }

        private void listBox2_DoubleClick(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1) return;
            int m = listBox2.SelectedIndex;
            if (m == -1) return;
            string name = lang.GlobalLang[n].Entries[m];
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new name", "ME3 Explorer", name, 0, 0);
            if (result != "")
            {
                lang.GlobalLang[n].Entries[m] = result;
                RefreshList2();
            }
        }

        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1) return;
            string name = lang.GlobalLang[n].name;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new name", "ME3 Explorer", name, 0, 0);
            if (result != "")
            {
                Languages.Language l = lang.GlobalLang[n];
                l.name = result;
                lang.GlobalLang[n] = l;
                RefreshList();
            }
        }

        private void setLanguageAsDefaultToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1) return;
            lang.CurrentLanguage = n;
            RefreshList();
            listBox1.SelectedIndex = n;
            SaveLanguages();
            MessageBox.Show("Done. Please restart the program");
        }

    }
}
