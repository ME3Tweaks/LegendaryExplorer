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
    public partial class AddPropertyDialog : Form
    {
        public List<string> extantProps;

        Dictionary<string, ClassInfo> classList;

        public AddPropertyDialog()
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

        public static string GetProperty(IExportEntry export, List<string> _extantProps, MEGame game)
        {
            string origname = export.ClassName;
            string temp = export.ClassName;
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
            if (!classList.ContainsKey(temp))
            {
                export = export.FileRef.Exports[export.idxClass - 1];
                //current object is not in classes db, temporarily add it to the list
                ClassInfo currentInfo;
                switch (game)
                {
                    case MEGame.ME1:
                        currentInfo = ME1Explorer.Unreal.ME1UnrealObjectInfo.generateClassInfo(export);
                        break;
                    case MEGame.ME2:
                        currentInfo = ME2Explorer.Unreal.ME2UnrealObjectInfo.generateClassInfo(export);
                        break;
                    case MEGame.ME3:
                    default:
                        currentInfo = ME3UnrealObjectInfo.generateClassInfo(export);
                        break;
                }
                currentInfo.baseClass = export.ClassParent;
                classList = classList.ToDictionary(entry => entry.Key, entry => entry.Value);
                classList[temp] = currentInfo;
            }
            while (classList.ContainsKey(temp) && temp != "Object")
            {
                classes.Add(temp);
                temp = classList[temp].baseClass;
            }
            classes.Reverse();
            AddPropertyDialog prompt = new AddPropertyDialog();
            prompt.classList = classList;
            prompt.extantProps = _extantProps;
            prompt.classListBox.DataSource = classes;
            prompt.classListBox.SelectedItem = origname;
            if (prompt.ShowDialog() == DialogResult.OK && prompt.propListBox.SelectedIndex != -1)
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
