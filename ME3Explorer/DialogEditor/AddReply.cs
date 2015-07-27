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
using KFreonLib.MEDirectories;

namespace ME3Explorer.DialogEditor
{
    public partial class AddReply : Form
    {
        public BioConversation.EntryListReplyListStruct res;
        public PCCObject pcc;
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
            res = new BioConversation.EntryListReplyListStruct();
            int n = pcc.findName("EReplyCategory");
            if (n == -1)
            {
                MessageBox.Show("Please add Name \"EReplyCategory\"");
                state = -1;
                return;
            }
            res.Paraphrase = textBox1.Text;
            res.refParaphrase = Int32.Parse(textBox2.Text);
            res.CategoryType = n;
            res.CategoryValue = Int32.Parse(textBox3.Text);
            res.Index = Int32.Parse(textBox4.Text);
            state = 1;
        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {
            int i=0;
            if (Int32.TryParse(textBox3.Text, out i))
            {
                label5.Text = "Preview: " + pcc.getNameEntry(i);
            }
            else
            {
                label5.Text = "Preview: fail";
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter name to search\nExamples:\nREPLY_AUTOCONTINUE\nREPLY_CATEGORY_DEFAULT\nREPLY_STANDARD", "ME3Explorer", "REPLY_CATEGORY_DEFAULT", 0, 0);
            if (result == "") return;
            int n = pcc.findName(result);
            if (n != -1)
                textBox3.Text = n.ToString();
        }
    }
}
