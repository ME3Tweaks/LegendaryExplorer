using KFreonLib.Textures;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ME3Explorer
{
    public partial class KFreonSearchForm : Form
    {
        List<TreeTexInfo> texes;
        private Texplorer2 myTexplorer = null;

        public KFreonSearchForm(Form callingForm)
        {
            myTexplorer = callingForm as Texplorer2;
            InitializeComponent();
            this.HandleCreated += new EventHandler(Search_HandleCreated);
            SearchCount.Text = listBox1.Items.Count.ToString();
        }

        public void LoadSearch()
        {
            texes = myTexplorer.SearchLoad();
            foreach (TreeTexInfo tex in texes)
                listBox1.Items.Add(tex.TexName + " (" + tex.ParentNode.Text.ToLower() + ")");
        }

        private void Search_HandleCreated(object sender, EventArgs e)
        {
            this.BeginInvoke(new MethodInvoker(FocusFirst));
        }

        private void FocusFirst()
        {
            this.InputBox.Focus();
        }

        private void Search_Load(object sender, EventArgs e)
        {
            if (texes.Count == 0)
                LoadSearch();
        }

        public void Reset()
        {
            texes.Clear();
        }

        public void SearchAllv5(string searchString, ListBox box, string searchType)
        {
            box.Invoke(new Action(() => box.ClearSelected()));
            string pattern = Regex.Escape(searchString).ToLower();

            if (searchString != string.Empty)
            {
                List<string> list = new List<string>();

                // KFreon: Hash search
                if (searchString.Length > 2 && searchString.Substring(0, 2) == "0x")
                {
                    for (int i = 0; i < texes.Count; i++)
                    {
                        TreeTexInfo tex = texes[i];
                        string thing = searchString.Substring(2).ToLowerInvariant();
                        string fromGame = KFreonLib.Textures.Methods.FormatTexmodHashAsString(tex.Hash).Substring(2).ToLowerInvariant();
                        if (fromGame.Contains(thing))
                            list.Add(tex.TexName + " (" + tex.ParentNode.Text.ToLower() + ")");
                    }
                }
                else if (searchString[0] == '@')   // KFreon: Export ID search
                {
                    int expID = 0;
                    if (!int.TryParse(searchString.Substring(1), out expID))
                        return;

                    for (int i = 0; i < texes.Count; i++)
                    {
                        TreeTexInfo tex = texes[i];
                        if (tex.ExpIDs.Contains(expID))
                            list.Add(tex.TexName + " (" + tex.ParentNode.Text.ToLower() + ")");
                    }
                }
                else if (searchString[0] == '\\')  // KFreon: Filename search
                {
                    int exppos = searchString.IndexOf('@');
                    int length = searchString.Length - (searchString.Length - exppos) - 2;
                    string name = (exppos == -1) ? searchString.Substring(1).ToLowerInvariant() : searchString.Substring(1, length).ToLowerInvariant();


                    if (exppos != -1)  // KFreon: Filename + ExpID search
                    {
                        int expID = 0;
                        if (!int.TryParse(searchString.Substring(exppos + 1), out expID))
                            return;

                        for (int i = 0; i < texes.Count; i++)
                        {
                            TreeTexInfo tex = texes[i];
                            for (int j = 0; j < tex.Files.Count; j++)
                                if (tex.Files[j].Split('\\').Last().ToLowerInvariant().Contains(name) && tex.ExpIDs[j] == expID)
                                    list.Add(tex.TexName + " (" + tex.ParentNode.Text.ToLower() + ")");
                        }
                    }
                    else  // KFreon: Normal filename search
                        for (int i = 0; i < texes.Count; i++)
                        {
                            TreeTexInfo tex = texes[i];
                            foreach (string filename in tex.Files)
                                if (filename.Split('\\').Last().ToLowerInvariant().Contains(name))
                                    list.Add(tex.TexName + " (" + tex.ParentNode.Text.ToLower() + ")");
                        }
                }
                else if (searchString[0] == '-')  // KFreon: Thumbnail search
                {
                    string searchstr = searchString.Substring(1).ToLowerInvariant();
                    foreach (TreeTexInfo tex in texes)
                    {
                        string name = Path.GetFileNameWithoutExtension(tex.ThumbnailPath).ToLowerInvariant();
                        if (name.Contains(searchstr))
                            list.Add(tex.TexName + " (" + tex.ParentNode.Text.ToLower() + ")");
                    }
                }
                else  // KFreon: Normal search
                {
                    for (int i = 0; i < texes.Count; i++)
                    {
                        TreeTexInfo tex = texes[i];
                        string name = tex.TexName + " (" + tex.ParentNode.Text.ToLower() + ")";
                        string s = name.ToLower();
                        Match match = Regex.Match(s, pattern, RegexOptions.IgnoreCase);
                        if (match.Success)
                            list.Add(s);
                    }
                }
                box.Invoke(new Action(() =>
                {
                    box.Items.Clear();
                    box.Items.AddRange(list.ToArray());
                }));
            }
        }

        private void InputBox_Enter(object sender, EventArgs e)
        {
            InputBox.ForeColor = Color.Black;
            InputBox.Clear();
        }

        private void InputBox_Leave(object sender, EventArgs e)
        {

        }

        private void InputBox_TextChanged(object sender, EventArgs e)
        {
            if (InputBox.Text != "" && InputBox.ForeColor != Color.Gray)
                if (InputBox.Text[0] != '\\')
                    SearchAllv5(InputBox.Text, listBox1, "new");

            SearchCount.Text = listBox1.Items.Count.ToString();
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            myTexplorer.SearchList_Click(listBox1);
        }

        private void Form_Closing(object sender, FormClosingEventArgs e)
        {
            myTexplorer.ResetSearchAfterClose();
        }
    }
}
