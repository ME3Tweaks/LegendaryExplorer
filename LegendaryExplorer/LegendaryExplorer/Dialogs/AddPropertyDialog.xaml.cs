using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Dialogs
{
    /// <summary>
    /// Interaction logic for AddPropertyDialogWPF.xaml
    /// </summary>
    public partial class AddPropertyDialog : TrackingNotifyPropertyChangedWindowBase
    {
        public class AddPropertyItem
        {
            public AddPropertyItem() { }

            public AddPropertyItem(NameReference propname, int staticArrayIndex, PropertyInfo propInfo)
            {
                PropertyName = propname;
                PropInfo = propInfo;
                StaticArrayIndex = staticArrayIndex;
            }

            public string DisplayName => PropInfo.IsStaticArray() ? $"{PropertyName.Instanced}[{StaticArrayIndex}]" : PropertyName.Instanced; 
            public NameReference PropertyName { get; }
            public int StaticArrayIndex { get; }
            public PropertyInfo PropInfo { get; }
        }

        public AddPropertyDialog(List<ClassInfo> classList, List<PropNameStaticArrayIdxPair> _existingProperties) : base ("Add Property Dialog", false)
        {
            _classHierarchy.ReplaceAll(classList.Select(x => x.ClassName));
            existingProperties = _existingProperties;
            classToClassPropertyMap = new Dictionary<string, List<AddPropertyItem>>(classList.Count);
            foreach (ClassInfo classInfo in classList)
            {
                var addPropertyItems = new List<AddPropertyItem>();
                foreach ((NameReference propName, PropertyInfo propInfo) in classInfo.properties)
                {
                    if (propInfo.IsStaticArray())
                    {
                        for (int i = 0; i < propInfo.StaticArrayLength; i++)
                        {
                            addPropertyItems.Add(new AddPropertyItem(propName, i, propInfo));
                        }
                    }
                    else
                    {
                        addPropertyItems.Add(new AddPropertyItem(propName, 0, propInfo));
                    }
                }
                classToClassPropertyMap.Add(classInfo.ClassName, addPropertyItems);
            }

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
                if (api.PropInfo.Transient && !ShowTransients) return false; //Don't show transient props
                if (existingProperties.Contains(new PropNameStaticArrayIdxPair(api.PropertyName, api.StaticArrayIndex))) return false; //Don't show existing properties
                if (string.IsNullOrWhiteSpace(FilterText)) return true; //no filter
                if (api.PropertyName.Instanced.Contains(FilterText, StringComparison.InvariantCultureIgnoreCase)) 
                    return true;
                if (api.PropInfo.Type.ToString().Contains(FilterText, StringComparison.InvariantCultureIgnoreCase)) 
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Properties already attached to our export (that should not be shown)
        /// </summary>
        private readonly List<PropNameStaticArrayIdxPair> existingProperties;
        /// <summary>
        /// Mapping of class names to the class properties
        /// </summary>
        private readonly Dictionary<string, List<AddPropertyItem>> classToClassPropertyMap;

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
                    UpdateShownProperties();
                }
            }
        }
        private bool _showTransients;
        public bool ShowTransients
        {
            get => _showTransients;
            set
            {
                if (SetProperty(ref _showTransients, value))
                {
                    UpdateShownProperties();
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

        private ObservableCollectionExtended<string> _classHierarchy { get; } = new();
        public ICollectionView ClassesView => CollectionViewSource.GetDefaultView(_classHierarchy);
        private ObservableCollectionExtended<AddPropertyItem> _availableProperties { get; } = new();
        public ICollectionView PropertiesView => CollectionViewSource.GetDefaultView(_availableProperties);

        #endregion

        private void UpdateShownProperties()
        {
            // Replaces the list of available properties with ones for the specified selected class
            // that are not transient and are not already part of the export
            using (PropertiesView.DeferRefresh())
            {
                if (SelectedClassName != null)
                {
                    _availableProperties.ReplaceAll(classToClassPropertyMap[SelectedClassName]
                        .Where(x => (!x.PropInfo.Transient || ShowTransients) && !existingProperties.Contains(new PropNameStaticArrayIdxPair(x.PropertyName, x.StaticArrayIndex)))
                        .OrderBy(x => new PropNameStaticArrayIdxPair(x.PropertyName, x.StaticArrayIndex)));
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

        public static (NameReference, int, PropertyInfo)? GetProperty(ExportEntry export, List<PropNameStaticArrayIdxPair> _existingProperties, MEGame game, Window callingWindow = null)
        {
            string temp = export.ClassName;
            var classes = new List<ClassInfo>();
            Dictionary<string, ClassInfo> classList = GlobalUnrealObjectInfo.GetClasses(game);

            if (!classList.ContainsKey(temp) && export.Class is ImportEntry)
            {
                if (GlobalUnrealObjectInfo.generateClassInfo(export) is ClassInfo info)
                {
                    classes.Add(info);
                    temp = info.baseClass;
                }
                else
                {
                    //lookup import parent info
                    temp = export.SuperClassName;
                }
            }
            else if (!classList.ContainsKey(temp) && export.Class is ExportEntry classExport)
            {
                export = classExport;
                //current object is not in classes db, temporarily add it to the list
                using var cache = new PackageCache();
                ClassInfo currentInfo = GlobalUnrealObjectInfo.generateClassInfo(export, packageCache: cache);
                classList = classList.ToDictionary(entry => entry.Key, entry => entry.Value);
                classList[temp] = currentInfo;
                classExport = classExport.SuperClass as ExportEntry;
                while (!classList.ContainsKey(currentInfo.baseClass) && classExport != null)
                {
                    currentInfo = GlobalUnrealObjectInfo.generateClassInfo(classExport, packageCache: cache);
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
            var prompt = new AddPropertyDialog(classes, _existingProperties)
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
                return (prompt.SelectedProperty.PropertyName, prompt.SelectedProperty.StaticArrayIndex,  prompt.SelectedProperty.PropInfo);
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

    public readonly struct PropNameStaticArrayIdxPair : IEquatable<PropNameStaticArrayIdxPair>, IComparable<PropNameStaticArrayIdxPair>, IComparable
    {
        public readonly NameReference Name;
        public readonly int StaticArrayIdx;

        public PropNameStaticArrayIdxPair(NameReference name, int staticArrayIdx)
        {
            Name = name;
            StaticArrayIdx = staticArrayIdx;
        }

        #region IEquatable

        public bool Equals(PropNameStaticArrayIdxPair other)
        {
            return Name.Equals(other.Name) && StaticArrayIdx == other.StaticArrayIdx;
        }

        public override bool Equals(object obj)
        {
            return obj is PropNameStaticArrayIdxPair other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, StaticArrayIdx);
        }

        public static bool operator ==(PropNameStaticArrayIdxPair left, PropNameStaticArrayIdxPair right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PropNameStaticArrayIdxPair left, PropNameStaticArrayIdxPair right)
        {
            return !left.Equals(right);
        }

        #endregion

        #region IComparable

        public int CompareTo(PropNameStaticArrayIdxPair other)
        {
            int nameComparison = Name.CompareTo(other.Name);
            if (nameComparison != 0) return nameComparison;
            return StaticArrayIdx.CompareTo(other.StaticArrayIdx);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            return obj is PropNameStaticArrayIdxPair other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(PropNameStaticArrayIdxPair)}");
        }

        public static bool operator <(PropNameStaticArrayIdxPair left, PropNameStaticArrayIdxPair right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(PropNameStaticArrayIdxPair left, PropNameStaticArrayIdxPair right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(PropNameStaticArrayIdxPair left, PropNameStaticArrayIdxPair right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(PropNameStaticArrayIdxPair left, PropNameStaticArrayIdxPair right)
        {
            return left.CompareTo(right) >= 0;
        }

        #endregion
    }
}
