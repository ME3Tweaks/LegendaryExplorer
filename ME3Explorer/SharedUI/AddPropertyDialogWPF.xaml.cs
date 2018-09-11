using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Interaction logic for AddPropertyDialogWPF.xaml
    /// </summary>
    public partial class AddPropertyDialogWPF : Window
    {
        public List<string> extantProps;
        Dictionary<string, ClassInfo> classList;

        public AddPropertyDialogWPF()
        {
            InitializeComponent();
        }

        private void propListBox_DoubleClick(object sender, EventArgs e)
        {
            DialogResult = true;
            Close();
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
            foreach (KeyValuePair<string, ClassInfo> entry in classList)
            {
                // do something with entry.Value or entry.Key
                if (entry.Key.StartsWith("LightMap"))
                {
                    Debug.WriteLine(entry.Key);
                }
            }
            if (!classList.ContainsKey(temp) && export.idxClass < 0)
            {
                //lookup import parent info
                temp = export.ClassParent;
            }
            else if (!classList.ContainsKey(temp) && export.idxClass > 0)
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
            AddPropertyDialogWPF prompt = new AddPropertyDialogWPF();
            prompt.classList = classList;
            prompt.extantProps = _extantProps;
            prompt.ClassesListView.ItemsSource = classes;
            prompt.ClassesListView.SelectedItem = origname;
            prompt.ShowDialog();
            if (prompt.DialogResult.HasValue && prompt.DialogResult.Value && prompt.PropertiesListView.SelectedIndex != -1)
            {
                return prompt.PropertiesListView.SelectedItem as string;
            }
            else
            {
                return null;
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void ClassesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string className = ClassesListView.SelectedItem as string;
            List<string> props = classList[className].properties.Keys.Except(extantProps).ToList();
            props.Sort();
            PropertiesListView.ItemsSource = props;
        }

        private void PropertiesListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PropertiesListView.SelectedIndex >= 0)
            {
                DialogResult = true;
                Close();
            }
        }
    }
}
