using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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
    public partial class AddPropertyDialogWPF : NotifyPropertyChangedWindowBase
    {
        public List<string> existingProperties;
        Dictionary<string, ClassInfo> classList;
        
        private string _selectedClassName;
        public string SelectedClassName
        {
            get => _selectedClassName;
            set => SetProperty(ref _selectedClassName, value);
        }

        public AddPropertyDialogWPF()
        {
            LoadCommands();
            InitializeComponent();
        }

        public ICommand AddPropertyCommand { get; set; }

        private void LoadCommands()
        {
            AddPropertyCommand = new GenericCommand(AddProperty, CanAddProperty);
        }

        private void AddProperty()
        {
            DialogResult = true;
            Close();
        }

        private bool CanAddProperty()
        {
            return PropertiesListView.SelectedItem != null;
        }

        public static (string, PropertyInfo)? GetProperty(ExportEntry export, List<string> _existingProperties, MEGame game, Window callingWindow = null)
        {
            string origname = export.ClassName;
            string temp = export.ClassName;
            var classes = new List<string>();
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
            //For debugging ME1 Objectinfo when we get around to it
            //foreach (KeyValuePair<string, ClassInfo> entry in classList)
            //{
            //    // do something with entry.Value or entry.Key
            //    if (entry.Key.StartsWith("LightMap"))
            //    {
            //        Debug.WriteLine(entry.Key);
            //    }
            //}
            if (!classList.ContainsKey(temp) && export.Class is ImportEntry)
            {
                //lookup import parent info
                temp = export.SuperClassName;
            }
            else if (!classList.ContainsKey(temp) && export.Class is ExportEntry classExport)
            {
                export = classExport;
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
                currentInfo.baseClass = export.SuperClassName;
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
            if (callingWindow != null)
            {
                prompt.Owner = callingWindow;
            }
            prompt.classList = classList;
            prompt.existingProperties = _existingProperties;
            prompt.ClassesListView.ItemsSource = classes;
            prompt.ClassesListView.SelectedItem = origname;
            prompt.ShowDialog();
            if (prompt.DialogResult.HasValue
             && prompt.DialogResult.Value
             && prompt.PropertiesListView.SelectedIndex != -1
             && prompt.PropertiesListView.SelectedItem is KeyValuePair<string, PropertyInfo> kvp)
            {
                return (kvp.Key, kvp.Value);
            }
            return null;
        }

        private void ClassesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string className = (string)ClassesListView.SelectedItem;
            SelectedClassName = className;
            var props = classList[className].properties.Where(x => !x.Value.Transient && !existingProperties.Contains(x.Key)).OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
            //props.Sort();
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
