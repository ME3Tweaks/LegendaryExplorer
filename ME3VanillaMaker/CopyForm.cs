using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3VanillaMaker
{
    public partial class CopyForm : Form
    {
        public delegate void CopyMethod();

        public CopyMethod Copy;
        public bool Init = false;
        public bool Pause = false;
        public bool Exit = false;

        public CopyForm()
        {
            InitializeComponent();
        }

        private void CopyForm_Activated(object sender, EventArgs e)
        {
            if (!Init)
            {
                Init = true;
                Copy();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Pause = !Pause;
            if(Pause)
                button1.Text = "Unpause";
            else
                button1.Text = "Pause";
        }

        private void CopyForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Exit = true;
        }

        private void CopyForm_Resize(object sender, EventArgs e)
        {
            button1.Left = this.Width - button1.Width - 15;
            button1.Top = 10;
        }
    }
}
