using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KFreonLib.Helpers
{
    /// <summary>
    /// Provides an interface to select some items from a list of items. 
    /// Provides a method to have an action for changing selected index.
    /// </summary>
    public partial class SelectionForm : Form
    {
        public List<string> SelectedItems = new List<string>();
        public List<int> SelectedInds = new List<int>();
        private Action SelectedIndexChangedAction = null;


        /// <summary>
        /// Contstructor.
        /// </summary>
        /// <param name="names">Items to display.</param>
        /// <param name="Description">Window message to display above selection list.</param>
        /// <param name="Title">Window title.</param>
        /// <param name="SelectAll">If true, all items are selected.</param>
        /// <param name="SelectedIndChanged">OPTIONAL: Action to perform on selection changed event.</param>
        public SelectionForm(List<string> names, string Description, string Title, bool SelectAll, Action SelectedIndChanged = null)
        {
            InitializeComponent();

            // KFreon: Must have something to select
            if (names == null || names.Count == 0)
                return;

            // KFreon: Set window title and description label
            this.Text = Title;
            DescriptionLabel.Text = Description;

            // KFreon: Center descriptionlabel
            int midpoint = (int)(this.Width / 2.0);
            int labelwidth = DescriptionLabel.Width;
            DescriptionLabel.Left = midpoint - (int)(labelwidth / 2.0);

            SelectAllCheckBox.Checked = SelectAll;

            // KFreon: Setup listview
            for (int i = 0; i < names.Count; i++)
            {
                listView1.Items.Add(names[i]);
                listView1.Items[i].Checked = SelectAll;
            }

            // KFreon: Set action if existing
            if (SelectedIndChanged != null)
                SelectedIndexChangedAction = SelectedIndChanged;
        }


        // OK button
        private void button1_Click(object sender, EventArgs e)
        {
            if (listView1.CheckedItems.Count == 0)
                return;

            SelectedItems = new List<string>();
            SelectedInds = new List<int>();
            for (int i = 0; i < listView1.CheckedItems.Count; i++)
            {
                SelectedInds.Add(i);
                SelectedItems.Add(listView1.CheckedItems[i].Text);
            }
            this.Close();
        }


        // Cancel button
        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void SelectAllCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            for (int i = 0; i < listView1.Items.Count; i++)
                listView1.Items[i].Checked = SelectAllCheckBox.Checked;
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (SelectedIndexChangedAction != null)
                SelectedIndexChangedAction();
        }
    }
}
