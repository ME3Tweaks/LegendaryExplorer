using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ME3Explorer.Pathfinding_Editor
{
    public partial class ListWindow : Form
    {
        public ListWindow(List<string> listItems, String title)
        {
            InitializeComponent();
            Text = title;
            foreach(string str in listItems)
            {
                listBox.Items.Add(str);
            }
            CenterToParent();
        }
    }
}
