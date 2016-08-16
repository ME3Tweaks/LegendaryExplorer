using System;
using System.Windows.Forms;

namespace ME3Explorer
{
    public partial class TlkManager : Form
    {

        public TlkManager()
        {
            InitializeComponent();
        }

        public void InitTlkManager()
        {
            refresh();
        }

        private void refresh()
        {
            listBox1.Items.Clear();
            foreach (TalkFile tlk in ME3TalkFiles.tlkList)
            {
                listBox1.Items.Add(tlk.path);
            }
        }

        private void addTlkButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.tlk|*.tlk";
            if (d.ShowDialog() == DialogResult.OK)
            {
                ME3TalkFiles.addTLK(d.FileName);
                refresh();
                listBox1.SelectedIndex = listBox1.Items.Count - 1;
            }
        }

        private void removeTlkButton_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n != -1)
            {
                ME3TalkFiles.removeTLK(n);
                refresh();
            }
        }

        private void tlkUpButton_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n > 0)
            {
                ME3TalkFiles.moveTLKUp(n);
                refresh();
                listBox1.SelectedIndex = n - 1;
            }
        }

        private void tlkDownButton_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n != -1 && n < listBox1.Items.Count - 1)
            {
                ME3TalkFiles.moveTLKDown(n);
                refresh();
                listBox1.SelectedIndex = n + 1;
            }

        }
    }
}
