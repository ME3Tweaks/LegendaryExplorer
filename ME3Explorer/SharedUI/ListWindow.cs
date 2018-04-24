using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ME3Explorer.SharedUI
{
    public partial class ListWindow : Form
    {
        public ListWindow(List<string> listItems, String title, String message, int width = 0, int height = 0)
        {
            InitializeComponent();
            Text = title;
            messageLabel.Text = message;
            if (width != 0)
            {
                Width = width;
            }
            if (height != 0)
            {
                Height = height;
            }
            foreach(string str in listItems)
            {
                listBox.Items.Add(str);
            }
            CenterToParent();
        }
    }
}
