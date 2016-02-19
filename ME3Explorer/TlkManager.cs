using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ME3Explorer
{
    public partial class TlkManager : Form
    {
        public TalkFiles tlkFiles;

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
            foreach (TalkFile tlk in tlkFiles.tlkList)
            {
                listBox1.Items.Add(tlk.path);
            }
        }

        private void addTlkButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.tlk|*.tlk";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                tlkFiles.LoadTlkData(d.FileName);
                refresh();
                listBox1.SelectedIndex = listBox1.Items.Count - 1;
            }
        }

        private void removeTlkButton_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n != -1)
            {
                tlkFiles.tlkList.RemoveAt(n);
                refresh();
            }
        }

        private void tlkUpButton_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n > 0)
            {
                TalkFile tlk = tlkFiles.tlkList[n];
                tlkFiles.tlkList.RemoveAt(n);
                tlkFiles.tlkList.Insert(n - 1, tlk);
                refresh();
                listBox1.SelectedIndex = n - 1;
            }
        }

        private void tlkDownButton_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n != -1 && n < listBox1.Items.Count - 1)
            {
                TalkFile tlk = tlkFiles.tlkList[n];
                tlkFiles.tlkList.RemoveAt(n);
                tlkFiles.tlkList.Insert(n + 1, tlk);
                refresh();
                listBox1.SelectedIndex = n + 1;
            }

        }
    }
}
