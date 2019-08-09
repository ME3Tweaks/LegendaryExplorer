using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using ME3Explorer.Packages;

namespace ME3Explorer.DialogEditor
{
    public partial class AddReply : Form
    {
        public ME3BioConversation.EntryListReplyListStruct res;
        public IMEPackage pcc;
        public int state = 0;

        public AddReply()
        {
            InitializeComponent();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            state = -1;
        }

        private void button1_Click(object sender, EventArgs e)
        {            
            res = new ME3BioConversation.EntryListReplyListStruct();
            res.Paraphrase = textBox1.Text;
            res.refParaphrase = Int32.Parse(textBox2.Text);
            res.CategoryType = pcc.FindNameOrAdd("EReplyCategory");
            res.CategoryValue = pcc.FindNameOrAdd(comboBox1.Text);
            res.Index = Int32.Parse(textBox4.Text);
            state = 1;
        }
    }
}
