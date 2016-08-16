using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ME2Explorer.Unreal;
using ME2Explorer.Unreal.Classes;
using KFreonLib.MEDirectories;
using ME3Explorer.Packages;

namespace ME2Explorer
{
    public partial class AddReply : Form
    {
        public ME2BioConversation.EntryListReplyListStruct res;
        public ME2Package pcc;
        public int state = 0;

        public AddReply()
        {
            InitializeComponent();
            comboBox1.Items.AddRange(new object[] {
            "REPLY_CATEGORY_DEFAULT",
            "REPLY_CATEGORY_AGREE",
            "REPLY_CATEGORY_DISAGREE",
            "REPLY_CATEGORY_FRIENDLY",
            "REPLY_CATEGORY_HOSTILE",
            "REPLY_CATEGORY_INVESTIGATE",
            "REPLY_CATEGORY_RENEGADE_INTERRUPT",
            "REPLY_CATEGORY_PARAGON_INTERRUPT"});
        }

        private void button2_Click(object sender, EventArgs e)
        {
            state = -1;
        }

        private void button1_Click(object sender, EventArgs e)
        {            
            res = new ME2BioConversation.EntryListReplyListStruct();
            res.Paraphrase = textBox1.Text;
            res.refParaphrase = Int32.Parse(textBox2.Text);
            res.CategoryValue = pcc.FindNameOrAdd(comboBox1.Text);
            res.Index = Int32.Parse(textBox4.Text);
            state = 1;
        }
    }
}
