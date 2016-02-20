using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ME3Explorer
{
    public partial class KFreonListErrorBox : Form
    {
        public List<string> Items { get; set; }


        public KFreonListErrorBox()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message to display</param>
        /// <param name="title">Title of window.</param>
        /// <param name="items">List of items to display.</param>
        /// <param name="icon">Icon of window. Use SystemIcons.</param>
        public KFreonListErrorBox(string message, string title, List<string> items, Icon icon)
        {
            InitializeComponent();
            Items = items;

            // KFreon: Set properties
            this.Icon = icon;
            this.Text = title;
            this.MainMessageTextBox.Text = message;
            this.ItemsBox.Items.AddRange(items.ToArray());
        }

        private void OKButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select destination";
                if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllLines(ofd.FileName, Items.ToArray());
                        MessageBox.Show("Saved list at: " + ofd.FileName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
            }
        }
    }
}
