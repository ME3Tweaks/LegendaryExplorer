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
    public partial class HeightFilterForm : Form
    {

        public int NewFilterZ = 0;
        public int NewFilterType = -1;

        public const int FILTER_Z_NONE = 0;
        public const int FILTER_Z_ABOVE = 1;
        public const int FILTER_Z_BELOW = 2;

        public HeightFilterForm(int currentFilter, int currentFilterValue)
        {

            InitializeComponent();
            //Parent = form;
            StartPosition = FormStartPosition.CenterParent;
            if (currentFilter > 0)
            {
                if (currentFilter == 1)
                {
                    radioButton_FilterAbove.Checked = true;
                }
                if (currentFilter == 2)
                {
                    radioButton_FilterBelow.Checked = true;
                }
                FilterZValueBox.Text = currentFilterValue.ToString();
            }
            else
            {
                if (currentFilter == 0)
                {
                    radioButton_NoFilter.Checked = true;
                }
            }
        }

        private void applyFilterButton_Click(object sender, EventArgs e)
        {

            if (radioButton_NoFilter.Checked)
            {
                NewFilterType = FILTER_Z_NONE;
                this.DialogResult = System.Windows.Forms.DialogResult.Yes;
                this.Close();
                return;
            }

            int filterValue = Int32.MinValue;
            if (radioButton_FilterAbove.Checked || radioButton_FilterBelow.Checked)
            {
                bool valueValid = Int32.TryParse(FilterZValueBox.Text, out filterValue);
                if (valueValid)
                {
                    NewFilterZ = filterValue;
                    NewFilterType = radioButton_FilterBelow.Checked ? FILTER_Z_BELOW : FILTER_Z_ABOVE;
                    this.DialogResult = System.Windows.Forms.DialogResult.Yes;
                    this.Close();
                }
            }
        }

        private void filterChecked_Changed(object sender, EventArgs e)
        {
            //if (sender is RadioButton)
            //{
            //RadioButton button = (RadioButton)sender;

            if (radioButton_FilterAbove.Checked || radioButton_FilterBelow.Checked)
            {
                FilterZValueBox.Enabled = true;
            }
            else
            {
                FilterZValueBox.Enabled = false;
            }

            //}
        }

        private void FilterZValueBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar == Convert.ToChar(Keys.Enter)) && FilterZValueBox.Enabled)
            {
                e.Handled = true;
                applyFilterButton.PerformClick();
                return;
            }
            e.Handled = !char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar) && (e.KeyChar != '-'); //prevent non digit entry
        }
    }
}