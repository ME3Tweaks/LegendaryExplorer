using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ME3ExplorerCore.Gammtek.Extensions;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Interaction logic for AddPropertyDialogWPF.xaml
    /// </summary>
    public partial class AddPropertyDialogWPF : TrackingNotifyPropertyChangedWindowBase
    {
        public class AddPropertyItem
        {
            public AddPropertyItem() { }

            public AddPropertyItem(string propname, PropertyInfo propInfo)
            {
                this.PropertyName = propname;
                this.PropInfo = propInfo;
            }

            public string PropertyName { get; }
            public PropertyInfo PropInfo { get; }
        }

        public AddPropertyDialogWPF(List<ClassInfo> classList, List<string> _existingProperties) : base ("Add Property Dialog", false)
        {
            _classHierarchy.ReplaceAll(classList.Select(x => x.ClassName));
            existingProperties = _existingProperties;
            classToClassPropertyMap = classList.ToDictionary(x => x.ClassName,
                x => x.properties.Select(y => new AddPropertyItem(y.Key, y.Value)).ToList());
            LoadCommands();
            InitializeComponent();

            ClassesView.Filter = FilterClass;
            PropertiesView.Filter = FilterProperty;
            SelectedClassName = _classHierarchy.Reverse().FirstOrDefault(x => classToClassPropertyMap[x].Any(FilterProperty));
        }

        private bool FilterClass(object obj)
        {
            if (obj is string className)
            {
                if (string.IsNullOrWhiteSpace(FilterText)) return true; //no filter
                var props = classToClassPropertyMap[className];
                return props.Any(FilterProperty);
            }

            return false;
        }

        private bool FilterProperty(object obj)
        {
            if (obj is AddPropertyItem api)
            {
                if (api.PropInfo.Transient) return false; //Don't show transient props
                if (existingProperties.Contains(api.PropertyName, StringComparer.InvariantCultureIgnoreCase)) return false; //Don't show existing properties
                if (string.IsNullOrWhiteSpace(FilterText)) return true; //no filter
                if (api.PropertyName.Contains(FilterText, StringComparison.InvariantCultureIgnoreCase)) 
                    return true;
                if (api.PropInfo.Type.ToString().Contains(FilterText, StringComparison.InvariantCultureIgnoreCase)) 
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Properties already attached to our export (that should not be shown)
        /// </summary>
        private List<string> existingProperties;
        /// <summary>
        /// Mapping of class names to the class properties
        /// </summary>

        private Dictionary<string, List<AddPropertyItem>> classToClassPropertyMap;


        #region Binding properties
        // The selected class
        private string _selectedClassName;
        public string SelectedClassName
        {
            get => _selectedClassName;
            set
            {
                if (SetProperty(ref _selectedClassName, value) && value != null)
                {
                    updateShownProperties();
                }
            }
        }

        // The selected property object
        private AddPropertyItem _selectedProperty;
        public AddPropertyItem SelectedProperty
        {
            get => _selectedProperty;
            set => SetProperty(ref _selectedProperty, value);
        }

        // The current filter text
        private string _filterText;
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (SetProperty(ref _filterText, value))
                {
                    // May need to reselect?
                    var selectedClassName = SelectedClassName;
                    ClassesView.Refresh();
                    PropertiesView.Refresh();
                    if (!ClassesView.Contains(selectedClassName))
                    {
                        // Done this way since there's no index accessors
                        foreach (var v in ClassesView)
                        {
                            if (v is string str)
                            {
                                SelectedClassName = str;
                                break;
                            }
                        }
                    }
                }
            }
        }

        private ObservableCollectionExtended<string> _classHierarchy { get; } = new ObservableCollectionExtended<string>();
        public ICollectionView ClassesView => CollectionViewSource.GetDefaultView(_classHierarchy);
        private ObservableCollectionExtended<AddPropertyItem> _availableProperties { get; } = new ObservableCollectionExtended<AddPropertyItem>();
        public ICollectionView PropertiesView => CollectionViewSource.GetDefaultView(_availableProperties);

        #endregion

        private void updateShownProperties()
        {
            // Replaces the list of available properties with ones for the specified selected class
            // that are not transient and are not already part of the export
            using (PropertiesView.DeferRefresh())
            {
                if (SelectedClassName != null)
                {
                    _availableProperties.ReplaceAll(classToClassPropertyMap[SelectedClassName]
                        .Where(x => !x.PropInfo.Transient && !existingProperties.Contains(x.PropertyName))
                        .OrderBy(x => x.PropertyName));
                }
                else
                {
                    _availableProperties.ClearEx();
                }
            }
        }

        #region Commands
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
            return SelectedProperty != null;
        }

        #endregion



        public static (string, PropertyInfo)? GetProperty(ExportEntry export, List<string> _existingProperties, MEGame game, Window callingWindow = null)
        {
            string temp = export.ClassName;
            var classes = new List<ClassInfo>();
            Dictionary<string, ClassInfo> classList;
            switch (game)
            {
                case MEGame.ME1:
                    classList = ME1UnrealObjectInfo.Classes;
                    break;
                case MEGame.ME2:
                    classList = ME2UnrealObjectInfo.Classes;
                    break;
                case MEGame.ME3:
                default:
                    classList = ME3UnrealObjectInfo.Classes;
                    break;
            }

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
                        currentInfo = ME1UnrealObjectInfo.generateClassInfo(export);
                        break;
                    case MEGame.ME2:
                        currentInfo = ME2UnrealObjectInfo.generateClassInfo(export);
                        break;
                    case MEGame.ME3:
                    default:
                        currentInfo = ME3UnrealObjectInfo.generateClassInfo(export);
                        break;
                }
                currentInfo.baseClass = export.SuperClassName;
                classList = classList.ToDictionary(entry => entry.Key, entry => entry.Value);
                classList[temp] = currentInfo;
                classExport = classExport.SuperClass as ExportEntry;
                while (!classList.ContainsKey(currentInfo.baseClass) && classExport != null)
                {
                    currentInfo = UnrealObjectInfo.generateClassInfo(classExport);
                    if (currentInfo == null)
                    {
                        break;
                    }
                    classList[classExport.ObjectName] = currentInfo;
                    classExport = classExport.SuperClass as ExportEntry;
                }
            }
            while (classList.ContainsKey(temp) && temp != "Object")
            {
                classes.Add(classList[temp]);
                temp = classList[temp].baseClass;
            }
            classes.Reverse();
            AddPropertyDialogWPF prompt = new AddPropertyDialogWPF(classes, _existingProperties)
            {
                Owner = callingWindow
            };
            //prompt.ClassesListView.ItemsSource = classes;
            //prompt.ClassesListView.SelectedItem = origname;
            prompt.ShowDialog();
            if (prompt.DialogResult.HasValue
             && prompt.DialogResult.Value
             && prompt.SelectedProperty != null)
            {
                return (prompt.SelectedProperty.PropertyName, prompt.SelectedProperty.PropInfo);
            }
            return null;
        }

        private void PropertiesListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedProperty != null)
            {
                DialogResult = true;
                Close();
            }
        }
    }
}
