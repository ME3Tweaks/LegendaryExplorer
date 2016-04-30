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

namespace ME3Explorer
{
    public partial class AddPropertyDialog : Form
    {
        public List<string> extantProps;

        public AddPropertyDialog()
        {
            InitializeComponent();
        }

        private void classListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            string className = classListBox.SelectedItem as string;
            List<string> props = UnrealObjectInfo.Classes[className].properties.Keys.Except(extantProps).ToList();
            props.Sort();
            propListBox.DataSource = props;
        }

        private void propListBox_DoubleClick(object sender, EventArgs e)
        {
            addButton.PerformClick();
        }

        public static string GetProperty(string className, List<string> _extantProps)
        {
            string temp = className;
            List<string> classes = new List<string>();
            while (UnrealObjectInfo.Classes.ContainsKey(temp) && temp != "Object")
            {
                classes.Add(temp);
                temp = UnrealObjectInfo.Classes[temp].baseClass;
            }
            classes.Reverse();
            AddPropertyDialog prompt = new AddPropertyDialog();
            prompt.extantProps = _extantProps;
            prompt.classListBox.DataSource = classes;
            prompt.classListBox.SelectedItem = className;
            if(prompt.ShowDialog() == DialogResult.OK && prompt.propListBox.SelectedIndex != -1)
            {
                return prompt.propListBox.SelectedItem as string;
            }
            else
            {
                return null;
            }
        }
    }
}
