using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal.Classes;

namespace ME3Explorer
{
    public partial class TexSelection : Form
    {
        bool bSelected = false;
        public bool bOk { get { return bSelected; } set { bSelected = value; } }
        public CheckedListBox imageListBox { get { return checkedListBoxTex; } }
        public string btnSelectionText { set { btnEdit.Text = value; } }
        public string FormTitle { set { this.Text = value; } }

        public TexSelection(Texture2D texture, bool allowVoidImages = false)
        {
            InitializeComponent();

            foreach (Texture2D.ImageInfo imgInfo in texture.imgList)
            {
                if (!allowVoidImages && imgInfo.offset == -1)
                    continue;
                else
                {
                    string storage;
                    int offset;
                    bool bChecked = (texture.imgList.Count == 1) ? true : false;
                    if (imgInfo.storageType == Texture2D.storage.pccSto)
                    {
                        storage = "pcc file";
                        offset = imgInfo.offset + (int)texture.pccOffset;
                    }
                    else
                    {
                        storage = "archive file";
                        offset = imgInfo.offset;
                    }
                    checkedListBoxTex.Items.Add("Image " + imgInfo.imgSize + " stored inside " + storage + " at offset " + offset, bChecked);
                }

                if (imgInfo.imgSize.width <= 4 || imgInfo.imgSize.height <= 4) // avoids selection of smaller images
                    break;
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            bSelected = true;
            this.Close();
        }

        private void checkedListBoxTex_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}
