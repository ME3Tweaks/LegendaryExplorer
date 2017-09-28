using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;

namespace ME3Explorer
{
    public partial class BinaryAddPropertyDialog : Form
    {
        public List<string> extantProps;

        Dictionary<string, ClassInfo> classList;

        public BinaryAddPropertyDialog()
        {
            InitializeComponent();
        }

        private void classListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            string className = classListBox.SelectedItem as string;
            List<string> props = classList[className].properties.Keys.Except(extantProps).ToList();
            props.Sort();
            propListBox.DataSource = props;
        }

        private void propListBox_DoubleClick(object sender, EventArgs e)
        {
            addButton.PerformClick();
        }

        public static string GetProperty(string className, List<string> _extantProps, MEGame game)
        {
            string temp = className;
            List<string> classes = new List<string>();
            Dictionary<string, ClassInfo> classList;
            switch (game)
            {
                case MEGame.ME1:
                    classList = ME1Explorer.Unreal.ME1UnrealObjectInfo.Classes;
                    break;
                case MEGame.ME2:
                    classList = ME2Explorer.Unreal.ME2UnrealObjectInfo.Classes;
                    break;
                case MEGame.ME3:
                default:
                    classList = ME3UnrealObjectInfo.Classes;
                    break;
            }
            while (classList.ContainsKey(temp) && temp != "Object")
            {
                classes.Add(temp);
                temp = classList[temp].baseClass;
            }
            classes.Reverse();
            BinaryAddPropertyDialog prompt = new BinaryAddPropertyDialog();
            prompt.classList = classList;
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
