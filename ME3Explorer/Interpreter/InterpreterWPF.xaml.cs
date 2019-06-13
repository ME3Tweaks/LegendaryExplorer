using ME1Explorer.Unreal;
using ME2Explorer.Unreal;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Media;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Be.Windows.Forms;
using System.Windows;
using System.ComponentModel;
using ME3Explorer.SharedUI;
using System.Windows.Input;
using static ME3Explorer.PackageEditorWPF;
using Gammtek.Conduit.Extensions.IO;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for InterpreterWPF.xaml
    /// </summary>
    public partial class InterpreterWPF : ExportLoaderControl
    {
        public ObservableCollectionExtended<UPropertyTreeViewEntry> PropertyNodes { get; set; } = new ObservableCollectionExtended<UPropertyTreeViewEntry>();
        //Values in this list will cause the ExportToString() method to be called on an objectproperty in InterpreterWPF.
        //This is useful for end user when they want to view things in a list for example, but all of the items are of the 
        //same type and are not distinguishable without changing to another export, wasting a lot of time.
        //values are the class of object value being parsed
        public static readonly string[] ExportToStringConverters = { "LevelStreamingKismet", "StaticMeshComponent", "ParticleSystemComponent", "DecalComponent", "LensFlareComponent" };
        public static readonly string[] IntToStringConverters = { "WwiseEvent" };
        public ObservableCollectionExtended<IndexedName> ParentNameList { get; private set; }

        private bool _hasUnsavedChanges;
        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set
            {
                _hasUnsavedChanges = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// The current list of loaded properties that back the property tree.
        /// This is used so we can do direct object reference comparisons for things like removal
        /// </summary>
        private PropertyCollection CurrentLoadedProperties;
        //Values in this list will cause custom code to be fired to modify what the displayed string is for IntProperties
        //when the class matches.


        int RescanSelectionOffset = 0;
        private readonly List<FrameworkElement> EditorSetElements = new List<FrameworkElement>();
        public struct PropHeader
        {
            public int name;
            public int type;
            public int size;
            public int index;
            public int offset;
        }

        //public string[] Types =
        //{
        //    "StructProperty", //0
        //    "IntProperty",
        //    "FloatProperty",
        //    "ObjectProperty",
        //    "NameProperty",
        //    "BoolProperty",  //5
        //    "ByteProperty",
        //    "ArrayProperty",
        //    "StrProperty",
        //    "StringRefProperty",
        //    "DelegateProperty",//10
        //    "None",
        //    "BioMask4Property",
        //};
        private HexBox Interpreter_Hexbox;
        private bool isLoadingNewData;
        private int ForcedRescanOffset;
        private bool ArrayElementJustAdded;

        public InterpreterWPF()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Interpreter WPF Export Loader", new WeakReference(this));
            LoadCommands();
            InitializeComponent();
            EditorSetElements.Add(Value_TextBox); //str, strref, int, float, obj
            EditorSetElements.Add(Value_ComboBox); //bool, name
            EditorSetElements.Add(NameIndexPrefix_TextBlock); //nameindex
            EditorSetElements.Add(NameIndex_TextBox); //nameindex
            EditorSetElements.Add(ParsedValue_TextBlock);
            EditorSetElements.Add(EditorSet_ArraySetSeparator);
            Set_Button.Visibility = Visibility.Collapsed;
            //EditorSet_Separator.Visibility = Visibility.Collapsed;
        }

        public UPropertyTreeViewEntry SelectedItem { get; set; }

        #region Commands
        public ICommand RemovePropertyCommand { get; set; }
        public ICommand AddPropertyCommand { get; set; }
        public ICommand CollapseChildrenCommand { get; set; }
        public ICommand ExpandChildrenCommand { get; set; }
        public ICommand SortChildrenCommand { get; set; }
        public ICommand SortParsedArrayAscendingCommand { get; set; } //obj, name only
        public ICommand SortParsedArrayDescendingCommand { get; set; } //obj, name only
        public ICommand SortValueArrayAscendingCommand { get; set; }
        public ICommand SortValueArrayDescendingCommand { get; set; }
        public ICommand PopoutInterpreterForObjectValueCommand { get; set; }
        public ICommand MoveArrayElementUpCommand { get; set; }
        public ICommand MoveArrayElementDownCommand { get; set; }
        public ICommand SaveHexChangesCommand { get; set; }
        public ICommand ToggleHexBoxWidthCommand { get; set; }
        public ICommand AddArrayElementCommand { get; set; }
        public ICommand RemoveArrayElementCommand { get; set; }
        public ICommand ClearArrayCommand { get; set; }
        private void LoadCommands()
        {
            RemovePropertyCommand = new GenericCommand(RemoveProperty, CanRemoveProperty);
            AddPropertyCommand = new GenericCommand(AddProperty, CanAddProperty);
            CollapseChildrenCommand = new GenericCommand(CollapseChildren, CanExpandOrCollapseChildren);
            ExpandChildrenCommand = new GenericCommand(ExpandChildren, CanExpandOrCollapseChildren);
            SortChildrenCommand = new GenericCommand(SortChildren, CanExpandOrCollapseChildren);

            SortParsedArrayAscendingCommand = new GenericCommand(SortParsedArrayAscending, CanSortArrayPropByParsedValue);
            SortParsedArrayDescendingCommand = new GenericCommand(SortParsedArrayDescending, CanSortArrayPropByParsedValue);
            SortValueArrayAscendingCommand = new GenericCommand(SortValueArrayAscending, CanSortArrayPropByValue);
            SortValueArrayDescendingCommand = new GenericCommand(SortValueArrayDescending, CanSortArrayPropByValue);
            ClearArrayCommand = new GenericCommand(ClearArray, ArrayIsSelected);
            PopoutInterpreterForObjectValueCommand = new GenericCommand(PopoutInterpreterForObj, ObjectPropertyExportIsSelected);

            SaveHexChangesCommand = new GenericCommand(Interpreter_SaveHexChanges, IsExportLoaded);
            ToggleHexBoxWidthCommand = new GenericCommand(Interpreter_ToggleHexboxWidth);
            AddArrayElementCommand = new GenericCommand(AddArrayElement, () => ArrayPropertyIsSelected() || ArrayElementIsSelected());
            RemoveArrayElementCommand = new GenericCommand(RemoveArrayElement, ArrayElementIsSelected);
            MoveArrayElementUpCommand = new GenericCommand(MoveArrayElementUp, CanMoveArrayElementUp);
            MoveArrayElementDownCommand = new GenericCommand(MoveArrayElementDown, CanMoveArrayElementDown);
        }

        private void ClearArray()
        {
            if (SelectedItem != null && SelectedItem.Property != null)
            {
                var araryProperty = (ArrayPropertyBase)SelectedItem.Property;
                araryProperty.Clear();
                CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);
            }
        }

        private bool ArrayIsSelected() => SelectedItem != null && SelectedItem.Property != null && SelectedItem.Property.GetType().IsOfGenericType(typeof(ArrayProperty<>));

        private bool ArrayPropertyIsSelected() => SelectedItem?.Property is ArrayPropertyBase;

        private bool IsExportLoaded() => CurrentLoadedExport != null;

        private bool ArrayElementIsSelected() => SelectedItem?.Parent?.Property is ArrayPropertyBase;

        private bool CanMoveArrayElementUp() => ArrayElementIsSelected() && SelectedItem.Parent.ChildrenProperties.IndexOf(SelectedItem) > 0;

        private bool CanMoveArrayElementDown()
        {
            var entries = SelectedItem?.Parent?.ChildrenProperties;
            return ArrayElementIsSelected() && entries.IndexOf(SelectedItem) < entries.Count - 1;
        }

        private void MoveArrayElementDown() => MoveArrayElement(false);

        private void MoveArrayElementUp() => MoveArrayElement(true);

        private void PopoutInterpreterForObj()
        {
            if (SelectedItem is UPropertyTreeViewEntry tvi && tvi.Property is ObjectProperty op && Pcc.isUExport(op.Value))
            {
                IExportEntry export = Pcc.getUExport(op.Value);
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new InterpreterWPF(), export)
                {
                    Title = $"Interpreter - {export.UIndex} {export.GetFullPath}_{export.indexValue} - {Pcc.FileName}"
                };
                elhw.Show();
            }
        }

        private bool ObjectPropertyExportIsSelected() => SelectedItem?.Property is ObjectProperty op && Pcc.isUExport(op.Value);

        private void SortParsedArrayAscending()
        {
            if (SelectedItem?.Property != null)
            {
                SortArrayPropertyParsed(SelectedItem.Property, true);
            }
        }

        private void SortValueArrayAscending()
        {
            if (SelectedItem?.Property != null)
            {
                SortArrayPropertyValue(SelectedItem.Property, true);
            }
        }

        private void SortValueArrayDescending()
        {
            if (SelectedItem?.Property != null)
            {
                SortArrayPropertyValue(SelectedItem.Property, false);
            }
        }

        private void SortParsedArrayDescending()
        {
            if (SelectedItem?.Property != null)
            {
                SortArrayPropertyParsed(SelectedItem.Property, false);
            }
        }

        private void SortArrayPropertyValue(UProperty property, bool ascending)
        {
            switch (property)
            {
                case ArrayProperty<ObjectProperty> aop:
                    aop.Values = ascending ? aop.OrderBy(x => x.Value).ToList() : aop.OrderByDescending(x => x.Value).ToList();
                    break;
                case ArrayProperty<IntProperty> aip:
                    aip.Values = ascending ? aip.OrderBy(x => x.Value).ToList() : aip.OrderByDescending(x => x.Value).ToList();
                    break;
                case ArrayProperty<FloatProperty> afp:
                    afp.Values = ascending ? afp.OrderBy(x => x.Value).ToList() : afp.OrderByDescending(x => x.Value).ToList();
                    break;
            }
            CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);
        }

        private void SortArrayPropertyParsed(UProperty property, bool ascending)
        {
            switch (property)
            {
                case ArrayProperty<ObjectProperty> aop:

                    int IndexKeySelector(ObjectProperty x) => Pcc.getEntry(x.Value)?.indexValue ?? 0;
                    string FullPathKeySelector(ObjectProperty x) => Pcc.getEntry(x.Value)?.GetFullPath ?? "";

                    aop.Values = ascending
                        ? aop.OrderBy(FullPathKeySelector).ThenBy(IndexKeySelector).ToList()
                        : aop.OrderByDescending(FullPathKeySelector).ThenByDescending(IndexKeySelector).ToList();
                    break;
                case ArrayProperty<NameProperty> anp:
                    anp.Values = (ascending ? anp.OrderBy(x => x.Value.InstancedString) : anp.OrderByDescending(x => x.Value.InstancedString)).ToList();
                    break;
            }
            CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);

        }

        internal void SetHexboxSelectedOffset(int v)
        {
            if (Interpreter_Hexbox != null)
            {
                Interpreter_Hexbox.SelectionStart = v;
                Interpreter_Hexbox.SelectionLength = 1;
            }
        }

        private bool CanSortArrayPropByParsedValue()
        {
            return SelectedItem?.Property is ArrayProperty<NameProperty> ||
                   SelectedItem?.Property is ArrayProperty<ObjectProperty>;
        }

        private bool CanSortArrayPropByValue()
        {
            return SelectedItem?.Property is ArrayProperty<NameProperty> ||
                   SelectedItem?.Property is ArrayProperty<ObjectProperty> ||
                   SelectedItem?.Property is ArrayProperty<IntProperty> ||
                   SelectedItem?.Property is ArrayProperty<FloatProperty>;
        }

        private void SortChildren() => SelectedItem?.ChildrenProperties.Sort(x => x.Property.Name.Name);

        private void ExpandChildren()
        {
            if (SelectedItem is UPropertyTreeViewEntry tvi)
            {
                SetChildrenExpandedStateRecursive(tvi, true);
            }
        }

        private static void SetChildrenExpandedStateRecursive(UPropertyTreeViewEntry root, bool IsExpanded)
        {
            root.IsExpanded = IsExpanded;
            foreach (UPropertyTreeViewEntry tvi in root.ChildrenProperties)
            {
                SetChildrenExpandedStateRecursive(tvi, IsExpanded);
            }
        }

        private bool CanExpandOrCollapseChildren() => SelectedItem is UPropertyTreeViewEntry tvi && tvi.ChildrenProperties.Count > 0;

        private void CollapseChildren()
        {
            if (SelectedItem != null)
            {
                SetChildrenExpandedStateRecursive(SelectedItem, false);
            }
        }

        private bool CanAddProperty()
        {
            if (CurrentLoadedExport == null)
            {
                return false;
            }
            if (Pcc.Game == MEGame.UDK)
            {
                //MessageBox.Show("Cannot add properties to UDK UPK files.", "Unsupported operation");
                return false;
            }
            if (CurrentLoadedExport.ClassName == "Class")
            {
                return false; //you can't add properties to class objects.
                              //we might want to see if the export has a NoneProperty - if it doesn't, adding properties won't work either.
                              //TODO
            }

            return true;
        }

        private void AddProperty()
        {
            if (Pcc.Game == MEGame.UDK)
            {
                MessageBox.Show("Cannot add properties to UDK UPK files.", "Unsupported operation");
                return;
            }

            var props = new List<string>();
            foreach (UProperty cProp in CurrentLoadedProperties)
            {
                //build a list we are going to the add dialog
                props.Add(cProp.Name);
            }

            (string, PropertyInfo)? prop = AddPropertyDialogWPF.GetProperty(CurrentLoadedExport, props, Pcc.Game, Window.GetWindow(this));

            if (prop.HasValue)
            {
                UProperty newProperty = null;
                (string propName, PropertyInfo propInfo) = prop.Value;
                //Todo: Maybe lookup the default value?
                switch (propInfo.type)
                {
                    case PropertyType.IntProperty:
                        newProperty = new IntProperty(0, propName);
                        break;
                    case PropertyType.BoolProperty:
                        newProperty = new BoolProperty(false, propName);
                        break;
                    case PropertyType.FloatProperty:
                        newProperty = new FloatProperty(0.0f, propName);
                        break;
                    case PropertyType.StringRefProperty:
                        newProperty = new StringRefProperty(propName);
                        break;
                    case PropertyType.StrProperty:
                        newProperty = new StrProperty("", propName);
                        break;
                    case PropertyType.ArrayProperty:
                        newProperty = new ArrayProperty<IntProperty>(ArrayType.Int, propName); //We can just set it to int as it will be reparsed and resolved.
                        break;
                    case PropertyType.NameProperty:
                        newProperty = new NameProperty(propName) { Value = "None" };
                        break;
                    case PropertyType.ByteProperty:
                        if (propInfo.IsEnumProp())
                        {
                            newProperty = new EnumProperty(propInfo.reference, Pcc.Game, propName);
                        }
                        else
                        {
                            newProperty = new ByteProperty(0, propName);
                        }
                        break;
                    case PropertyType.ObjectProperty:
                        newProperty = new ObjectProperty(0, propName);
                        break;
                    case PropertyType.StructProperty:

                        PropertyCollection structProps = UnrealObjectInfo.getDefaultStructValue(Pcc.Game, propInfo.reference, true);
                        newProperty = new StructProperty(propInfo.reference, structProps, propName, isImmutable: UnrealObjectInfo.isImmutable(propInfo.reference, Pcc.Game));
                        break;
                }

                //UProperty property = generateNewProperty(prop.Item1, currentInfo);
                if (newProperty != null)
                {
                    CurrentLoadedProperties.Insert(CurrentLoadedProperties.Count - 1, newProperty); //insert before noneproperty
                    ForcedRescanOffset = (int)CurrentLoadedProperties.Last().StartOffset;
                }
                //Todo: Create new node, prevent refresh of this instance.
                CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);
                //End Todo
            }
        }

        private void RemoveProperty()
        {
            if (Interpreter_TreeView.SelectedItem is UPropertyTreeViewEntry tvi && tvi.Property != null)
            {
                CurrentLoadedProperties.Remove(tvi.Property);
                CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);
                //StartScan();
            }
        }

        private bool CanRemoveProperty()
        {
            if (Interpreter_TreeView.SelectedItem is UPropertyTreeViewEntry tvi)
            {
                return tvi.Parent != null && tvi.Parent.Parent == null && !(tvi.Property is NoneProperty); //only items with a single parent (root nodes)
            }
            return false;
        }

        #endregion

        /// <summary>
        /// Unloads the loaded export, if any
        /// </summary>
        public override void UnloadExport()
        {
            CurrentLoadedExport = null;
            EditorSetElements.ForEach(x => x.Visibility = Visibility.Collapsed);
            Set_Button.Visibility = Visibility.Collapsed;
            //EditorSet_Separator.Visibility = Visibility.Collapsed;
            (Interpreter_Hexbox?.ByteProvider as DynamicByteProvider)?.Bytes.Clear();
            Interpreter_Hexbox?.Refresh();
            HasUnsavedChanges = false;
            PropertyNodes.Clear();
        }

        /// <summary>
        /// Load a new export for display and editing in this control
        /// </summary>
        /// <param name="export"></param>
        public override void LoadExport(IExportEntry export)
        {
            EditorSetElements.ForEach(x => x.Visibility = Visibility.Collapsed);
            Set_Button.Visibility = Visibility.Collapsed;
            //EditorSet_Separator.Visibility = Visibility.Collapsed;
            HasUnsavedChanges = false;
            Interpreter_Hexbox.UnhighlightAll();
            //set rescan offset
            //TODO: Make this more reliable because it is recycling virtualization
            if (CurrentLoadedExport != null && export.FileRef == Pcc && export.UIndex == CurrentLoadedExport.UIndex)
            {
                if (SelectedItem is UPropertyTreeViewEntry tvi && tvi.Property != null)
                {
                    RescanSelectionOffset = (int)tvi.Property.StartOffset;
                }
            }
            else
            {
                RescanSelectionOffset = 0;
            }
            if (ForcedRescanOffset != 0)
            {
                RescanSelectionOffset = ForcedRescanOffset;
                ForcedRescanOffset = 0;
            }
            //Debug.WriteLine("Selection offset: " + RescanSelectionOffset);
            CurrentLoadedExport = export;
            isLoadingNewData = true;
            (Interpreter_Hexbox.ByteProvider as DynamicByteProvider)?.ReplaceBytes(export.Data);
            hb1_SelectionChanged(null, null); //refresh bottom text
            Interpreter_Hexbox.Select(0, 1);
            Interpreter_Hexbox.ScrollByteIntoView();
            isLoadingNewData = false;
            StartScan();
        }

        /// <summary>
        /// Call this when reloading the entire tree.
        /// </summary>
        private void StartScan()
        {
            PropertyNodes.Clear();

            if (CurrentLoadedExport.ClassName == "Class")
            {
                UPropertyTreeViewEntry topLevelTree = new UPropertyTreeViewEntry
                {
                    DisplayName = $"Export {CurrentLoadedExport.UIndex }: { CurrentLoadedExport.ObjectName} ({CurrentLoadedExport.ClassName})",
                    IsExpanded = true
                };

                topLevelTree.ChildrenProperties.Add(new UPropertyTreeViewEntry
                {
                    DisplayName = "Class objects do not have properties",
                    Property = new UnknownProperty(),
                    Parent = topLevelTree
                });

                PropertyNodes.Add(topLevelTree);
            }
            else
            {

                UPropertyTreeViewEntry topLevelTree = new UPropertyTreeViewEntry
                {
                    DisplayName = $"Export {CurrentLoadedExport.UIndex }: { CurrentLoadedExport.ObjectName}_{CurrentLoadedExport.indexValue} ({CurrentLoadedExport.ClassName})",
                    IsExpanded = true
                };

                PropertyNodes.Add(topLevelTree);

                try
                {
                    CurrentLoadedProperties = CurrentLoadedExport.GetProperties(includeNoneProperties: true);
                    foreach (UProperty prop in CurrentLoadedProperties)
                    {
                        GenerateUPropertyTreeForProperty(prop, topLevelTree, CurrentLoadedExport, PropertyChangedHandler: OnUPropertyTreeViewEntry_PropertyChanged);
                    }
                }
                catch (Exception ex)
                {
                    UPropertyTreeViewEntry errorNode = new UPropertyTreeViewEntry
                    {
                        DisplayName = $"PARSE ERROR: {ex.Message}"
                    };
                    topLevelTree.ChildrenProperties.Add(errorNode);
                }

                if (RescanSelectionOffset != 0)
                {
                    UPropertyTreeViewEntry itemToSelect = topLevelTree.FlattenTree().LastOrDefault(x => x.Property != null && x.Property.StartOffset == RescanSelectionOffset);
                    if (itemToSelect != null)
                    {
                        UPropertyTreeViewEntry cachedSelectedItem = itemToSelect;
                        if (ArrayElementJustAdded)
                        {
                            //Ensure we are at array level so we can choose the last item
                            if (!(itemToSelect.Property is ArrayPropertyBase))
                            {
                                itemToSelect = itemToSelect.Parent;
                            }

                            RescanSelectionOffset = 0;
                            ArrayElementJustAdded = false;
                            if (itemToSelect.ChildrenProperties.LastOrDefault() is UPropertyTreeViewEntry u)
                            {
                                u.ExpandParents();
                                u.IsSelected = true;
                                return;
                            }
                        }
                        //todo: select node using parent-first selection (from packageeditorwpf)
                        //due to tree view virtualization

                        cachedSelectedItem.ExpandParents();
                        cachedSelectedItem.IsSelected = true;
                    }
                    RescanSelectionOffset = 0;
                }
            }
        }


        #region Static tree generating code (shared with BinaryInterpreterWPF)
        public static void GenerateUPropertyTreeForProperty(UProperty prop, UPropertyTreeViewEntry parent, IExportEntry export, string displayPrefix = "", PropertyChangedEventHandler PropertyChangedHandler = null)
        {
            var upropertyEntry = GenerateUPropertyTreeViewEntry(prop, parent, export, displayPrefix, PropertyChangedHandler);
            switch (prop)
            {
                case ArrayPropertyBase arrayProp:
                    {
                        int i = 0;
                        if (arrayProp.Count > 1000)
                        {
                            //Too big to load reliably, users won't edit huge things like this anyways.
                            UPropertyTreeViewEntry wontshowholder = new UPropertyTreeViewEntry()
                            {
                                DisplayName = "Too many children to display"
                            };
                            upropertyEntry.ChildrenProperties.Add(wontshowholder);
                        }
                        else
                        {
                            foreach (UProperty listProp in arrayProp.Properties)
                            {
                                GenerateUPropertyTreeForProperty(listProp, upropertyEntry, export, $" Item {i++}:", PropertyChangedHandler);
                            }
                        }
                        break;
                    }
                case StructProperty sProp:
                    {
                        foreach (var subProp in sProp.Properties)
                        {
                            GenerateUPropertyTreeForProperty(subProp, upropertyEntry, export, PropertyChangedHandler: PropertyChangedHandler);
                        }
                        break;
                    }
            }
        }

        internal void SetParentNameList(ObservableCollectionExtended<IndexedName> namesList)
        {
            ParentNameList = namesList;
        }

        public static UPropertyTreeViewEntry GenerateUPropertyTreeViewEntry(UProperty prop, UPropertyTreeViewEntry parent, IExportEntry parsingExport, string displayPrefix = "", PropertyChangedEventHandler PropertyChangedHandler = null)
        {
            string displayName = displayPrefix;
            if (!(parent.Property is ArrayPropertyBase))
            {
                displayName += $" {prop.Name}:";
            }
            string editableValue = ""; //editable value
            string parsedValue = ""; //human formatted item. Will most times be blank
            switch (prop)
            {
                case ObjectProperty op:
                    {
                        int index = op.Value;
                        var entry = parsingExport.FileRef.getEntry(index);
                        if (entry != null)
                        {
                            editableValue = index.ToString();
                            parsedValue = entry.GetFullPath;
                            if (entry is IExportEntry exp)
                            {
                                parsedValue += $"_{exp.indexValue}";
                            }
                            if (index > 0 && ExportToStringConverters.Contains(entry.ClassName))
                            {
                                editableValue += $" {ExportToString(parsingExport.FileRef.Exports[index - 1])}";
                            }
                        }
                        else if (index == 0)
                        {
                            editableValue = index.ToString();
                            parsedValue = "Null";

                        }
                        else
                        {
                            editableValue = index.ToString();
                            parsedValue = $"Index out of bounds of {(index < 0 ? "Import" : "Export")} list";
                        }
                    }
                    break;
                case IntProperty ip:
                    {
                        editableValue = ip.Value.ToString();
                        if (IntToStringConverters.Contains(parsingExport.ClassName))
                        {
                            parsedValue = IntToString(prop.Name, ip.Value, parsingExport);
                        }
                        if (ip.Name == "m_nStrRefID")
                        {
                            parsedValue = IntToString(prop.Name, ip.Value, parsingExport);
                        }

                        if (parent.Property is StructProperty property && property.StructType == "Rotator")
                        {
                            parsedValue = $"({ip.Value.ToDegrees():0.0######} degrees)";
                        }
                    }
                    break;
                case FloatProperty fp:
                    editableValue = fp.Value.ToString("0.########");
                    break;
                case BoolProperty bp:
                    editableValue = bp.Value.ToString(); //combobox
                    break;
                case ArrayPropertyBase ap:
                    {
                        ArrayType at = UnrealObjectInfo.GetArrayType(parsingExport.FileRef.Game, prop.Name.Name, parent.Property is StructProperty sp ? sp.StructType : parsingExport.ClassName, parsingExport);
                        editableValue = $"{at.ToString()} array";
                    }
                    break;
                case NameProperty np:
                    editableValue = $"{parsingExport.FileRef.findName(np.Value.Name)}_{np.Value.Number}";
                    parsedValue = np.Value.InstancedString;
                    break;
                case ByteProperty bp:
                    editableValue = parsedValue = bp.Value.ToString();
                    break;
                case EnumProperty ep:
                    editableValue = ep.Value.InstancedString;
                    break;
                case StringRefProperty strrefp:
                    editableValue = strrefp.Value.ToString();
                    switch (parsingExport.FileRef.Game)
                    {
                        case MEGame.ME3:
                            parsedValue = ME3TalkFiles.tlkList.Count == 0 ? "(.tlk not loaded)" : ME3TalkFiles.findDataById(strrefp.Value);
                            break;
                        case MEGame.ME2:
                            parsedValue = ME2Explorer.ME2TalkFiles.tlkList.Count == 0 ? "(.tlk not loaded)" : ME2Explorer.ME2TalkFiles.findDataById(strrefp.Value);
                            break;
                        case MEGame.ME1:
                            parsedValue = ME1Explorer.ME1TalkFiles.tlkList.Count == 0 ? "(no TLK loaded)" : ME1Explorer.ME1TalkFiles.findDataById(strrefp.Value);
                            break;
                    }
                    break;
                case StrProperty strp:
                    editableValue = strp.Value;
                    break;
                case StructProperty sp:

                    if (sp.StructType == "Vector" || sp.StructType == "Rotator")
                    {
                        string loc = string.Join(", ", sp.Properties.Select(p =>
                        {
                            switch (p)
                            {
                                case FloatProperty fp:
                                    return $"{p.Name}={fp.Value}";
                                case IntProperty ip:
                                    return $"{p.Name}={ip.Value}";
                                default:
                                    return "";
                            }
                        }));
                        parsedValue = $"({loc})";
                    }
                    else if (sp.StructType == "Guid")
                    {
                        MemoryStream ms = new MemoryStream();
                        foreach (IntProperty intProperty in sp.Properties)
                        {
                            ms.WriteInt32(intProperty);
                        }
                        Guid g = new Guid(ms.ToArray());
                        parsedValue = g.ToString();
                    }
                    else if (sp.StructType == "TimelineEffect")
                    {
                        EnumProperty typeProp = sp.Properties.GetProp<EnumProperty>("Type");
                        FloatProperty timeIndex = sp.Properties.GetProp<FloatProperty>("TimeIndex");
                        string timelineEffectType = typeProp != null
                            ? $"{typeProp.Value} @ {timeIndex.Value}s"
                            : "Unknown effect";
                        parsedValue = $"({timelineEffectType})";
                    }
                    else if (sp.StructType == "BioStreamingState")
                    {
                        editableValue = "";
                        NameProperty stateName = sp.Properties.GetProp<NameProperty>("StateName");
                        if (stateName != null && stateName.Value.Name != "None")
                        {
                            editableValue = $"{stateName.Value.Name}";
                        }

                        NameProperty inChunkName = sp.Properties.GetProp<NameProperty>("InChunkName");
                        if (inChunkName != null && inChunkName.Value.Name != "None")
                        {
                            editableValue += $" InChunkName: {inChunkName.Value.Name}";
                        }

                    }
                    else
                    {
                        parsedValue = sp.StructType;
                    }
                    break;
                case NoneProperty _:
                    parsedValue = "End of properties";
                    break;
            }
            UPropertyTreeViewEntry item = new UPropertyTreeViewEntry
            {
                Property = prop,
                EditableValue = editableValue,
                ParsedValue = parsedValue,
                DisplayName = displayName.Trim(),
                Parent = parent,
                AttachedExport = parsingExport
            };

            //Auto expand
            if (item.Property != null && item.Property.Name == "StreamingStates")
            {
                item.IsExpanded = true;
            }

            if (PropertyChangedHandler != null)
            {
                item.PropertyChanged += PropertyChangedHandler;
            }
            parent.ChildrenProperties.Add(item);
            return item;
        }

        private void OnUPropertyTreeViewEntry_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var uptvi = (UPropertyTreeViewEntry)sender;
            switch (e.PropertyName)
            {
                case "ColorStructCode" when uptvi.Property is StructProperty colorStruct:
                    uptvi.ChildrenProperties.ClearEx();
                    foreach (var subProp in colorStruct.Properties)
                    {
                        GenerateUPropertyTreeForProperty(subProp, uptvi, uptvi.AttachedExport, PropertyChangedHandler: OnUPropertyTreeViewEntry_PropertyChanged);
                    }
                    var a = colorStruct.GetProp<ByteProperty>("A");
                    var r = colorStruct.GetProp<ByteProperty>("R");
                    var g = colorStruct.GetProp<ByteProperty>("G");
                    var b = colorStruct.GetProp<ByteProperty>("B");

                    var byteProvider = (DynamicByteProvider)Interpreter_Hexbox.ByteProvider;
                    byteProvider.WriteByte(a.ValueOffset, a.Value);
                    byteProvider.WriteByte(r.ValueOffset, r.Value);
                    byteProvider.WriteByte(g.ValueOffset, g.Value);
                    byteProvider.WriteByte(b.ValueOffset, b.Value);
                    Interpreter_Hexbox.Refresh();
                    break;
            }
        }

        /// <summary>
        /// Converts a value of a property into a more human readable string.
        /// This is for IntProperty.
        /// </summary>
        /// <param name="name">Name of the property</param>
        /// <param name="value">Value of the property to transform</param>
        /// <param name="export">export the property belongs to</param>
        /// <returns></returns>
        private static string IntToString(NameReference name, int value, IExportEntry export)
        {
            switch (export.ClassName)
            {
                case "WwiseEvent":
                    switch (name)
                    {
                        case "Id":
                            return $" (0x{value:X8})";
                    }
                    break;
            }

            if (name == "m_nStrRefID")
            {
                switch (export.FileRef.Game)
                {
                    case MEGame.ME3:
                        return ME3TalkFiles.tlkList.Count == 0 ? "(.tlk not loaded)" : ME3TalkFiles.findDataById(value);
                    case MEGame.ME2:
                        return ME2Explorer.ME2TalkFiles.tlkList.Count == 0
                            ? "(.tlk not loaded)"
                            : ME2Explorer.ME2TalkFiles.findDataById(value);
                    case MEGame.ME1:
                        //Todo: Support local TLKs in this file.
                        return ME1Explorer.ME1TalkFiles.findDataById(value);
                }
            }
            return "";
        }

        private static string ExportToString(IExportEntry exportEntry)
        {
            switch (exportEntry.ObjectName)
            {
                case "LevelStreamingKismet":
                    NameProperty prop = exportEntry.GetProperty<NameProperty>("PackageName");
                    return $"({prop.Value.InstancedString})";
                case "StaticMeshComponent":
                    {
                        ObjectProperty smprop = exportEntry.GetProperty<ObjectProperty>("StaticMesh");
                        if (smprop != null)
                        {
                            IEntry smEntry = exportEntry.FileRef.getEntry(smprop.Value);
                            if (smEntry != null)
                            {
                                return $"({smEntry.ObjectName})";
                            }
                        }
                    }
                    break;
                case "LensFlareComponent":
                case "ParticleSystemComponent":
                    {
                        ObjectProperty smprop = exportEntry.GetProperty<ObjectProperty>("Template");
                        if (smprop != null)
                        {
                            IEntry smEntry = exportEntry.FileRef.getEntry(smprop.Value);
                            if (smEntry != null)
                            {
                                return $"({smEntry.ObjectName})";
                            }
                        }
                    }
                    break;
                case "DecalComponent":
                    {
                        ObjectProperty smprop = exportEntry.GetProperty<ObjectProperty>("DecalMaterial");
                        if (smprop != null)
                        {
                            IEntry smEntry = exportEntry.FileRef.getEntry(smprop.Value);
                            if (smEntry != null)
                            {
                                return $"({smEntry.ObjectName})";
                            }
                        }
                    }
                    break;
            }
            return "";
        }

        #endregion

        private void hb1_SelectionChanged(object sender, EventArgs e)
        {
            int start = (int)Interpreter_Hexbox.SelectionStart;
            int len = (int)Interpreter_Hexbox.SelectionLength;
            int size = (int)Interpreter_Hexbox.ByteProvider.Length;
            //TODO: Optimize this so this is only called when data has changed
            byte[] currentData = ((DynamicByteProvider)Interpreter_Hexbox.ByteProvider).Bytes.ToArray();
            try
            {
                if (start != -1 && start < size)
                {
                    string s = $"Byte: {currentData[start]}"; //if selection is same as size this will crash.
                    if (start <= currentData.Length - 4)
                    {
                        int val = BitConverter.ToInt32(currentData, start);
                        s += $", Int: {val}";
                        s += $", Float: {BitConverter.ToSingle(currentData, start)}";
                        if (Pcc.isName(val))
                        {
                            s += $", Name: {Pcc.getNameEntry(val)}";
                        }
                        if (Pcc.getEntry(val) is IExportEntry exp)
                        {
                            s += $", Export: {exp.ObjectName}";
                        }
                        else if (Pcc.getEntry(val) is ImportEntry imp)
                        {
                            s += $", Import: {imp.ObjectName}";
                        }
                    }
                    s += $" | Start=0x{start:X8} ";
                    if (len > 0)
                    {
                        s += $"Length=0x{len:X8} ";
                        s += $"End=0x{start + len - 1:X8}";
                    }
                    StatusBar_LeftMostText.Text = s;
                }
                else
                {
                    StatusBar_LeftMostText.Text = "Nothing Selected";
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void Interpreter_ToggleHexboxWidth()
        {
            GridLength len = HexboxColumnDefinition.Width;
            if (len.Value < HexboxColumnDefinition.MaxWidth)
            {
                HexboxColumnDefinition.Width = new GridLength(HexboxColumnDefinition.MaxWidth);
            }
            else
            {
                HexboxColumnDefinition.Width = new GridLength(HexboxColumnDefinition.MinWidth);
            }
        }

        private void Interpreter_TreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // This runs in a delegate due to how multithread bubble-up items work with treeview.
            // Without this delegate, the item selected will randomly be a parent item instead.
            // From https://www.codeproject.com/Tips/208896/WPF-TreeView-SelectedItemChanged-called-twice
            Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() => UpdateHexboxPosition(e.NewValue as UPropertyTreeViewEntry)));
            UPropertyTreeViewEntry newSelectedItem = (UPropertyTreeViewEntry)e.NewValue;
            //list of visible elements for editing
            var SupportedEditorSetElements = new List<FrameworkElement>();
            if (newSelectedItem?.Property != null)
            {
                switch (newSelectedItem.Property)
                {
                    case IntProperty ip:
                        Value_TextBox.Text = ip.Value.ToString();
                        SupportedEditorSetElements.Add(Value_TextBox);
                        if (newSelectedItem.Parent?.Property is StructProperty property && property.StructType == "Rotator")
                        {
                            //we support editing rotators as degrees. We will preview the raw value and enter data in degrees instead.
                            SupportedEditorSetElements.Add(ParsedValue_TextBlock);
                            Value_TextBox.Text = $"{ip.Value.ToDegrees():0.0######}";
                            ParsedValue_TextBlock.Text = $"{ip.Value} (raw value)"; //raw
                        }
                        break;
                    case FloatProperty fp:
                        Value_TextBox.Text = fp.Value.ToString();
                        SupportedEditorSetElements.Add(Value_TextBox);
                        break;
                    case StrProperty strp:
                        Value_TextBox.Text = strp.Value;
                        SupportedEditorSetElements.Add(Value_TextBox);
                        break;
                    case BoolProperty bp:
                        {
                            SupportedEditorSetElements.Add(Value_ComboBox);
                            var values = new List<string> { "True", "False" };
                            Value_ComboBox.IsEditable = false;
                            Value_ComboBox.ItemsSource = values;
                            Value_ComboBox.SelectedIndex = bp.Value ? 0 : 1; //true : false
                        }
                        break;
                    case ObjectProperty op:
                        Value_TextBox.Text = op.Value.ToString();
                        UpdateParsedEditorValue();
                        SupportedEditorSetElements.Add(Value_TextBox);
                        SupportedEditorSetElements.Add(ParsedValue_TextBlock);
                        break;
                    case NameProperty np:
                        TextSearch.SetTextPath(Value_ComboBox, "Name");
                        Value_ComboBox.IsEditable = true;

                        if (ParentNameList == null)
                        {
                            Value_ComboBox.ItemsSource = Pcc.Names.Select((nr, i) => new IndexedName(i, nr)).ToList();
                        }
                        else
                        {
                            Value_ComboBox.ItemsSource = ParentNameList;
                        }
                        Value_ComboBox.SelectedIndex = Pcc.findName(np.Value.Name);
                        NameIndex_TextBox.Text = np.Value.Number.ToString();

                        SupportedEditorSetElements.Add(Value_ComboBox);
                        SupportedEditorSetElements.Add(NameIndexPrefix_TextBlock);
                        SupportedEditorSetElements.Add(NameIndex_TextBox);
                        break;
                    case EnumProperty ep:
                        {
                            SupportedEditorSetElements.Add(Value_ComboBox);
                            List<NameReference> values = ep.EnumValues;
                            Value_ComboBox.ItemsSource = values;
                            int indexSelected = values.IndexOf(ep.Value);
                            Value_ComboBox.SelectedIndex = indexSelected;
                        }
                        break;
                    case ByteProperty bp:
                        Value_TextBox.Text = bp.Value.ToString();
                        SupportedEditorSetElements.Add(Value_TextBox);
                        break;
                    case StringRefProperty strrefp:
                        Value_TextBox.Text = strrefp.Value.ToString();
                        SupportedEditorSetElements.Add(Value_TextBox);
                        SupportedEditorSetElements.Add(ParsedValue_TextBlock);
                        UpdateParsedEditorValue();
                        break;
                }

                Set_Button.Visibility = SupportedEditorSetElements.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

                //Hide the non-used controls
                foreach (FrameworkElement fe in EditorSetElements)
                {
                    fe.Visibility = SupportedEditorSetElements.Contains(fe) ? Visibility.Visible : Visibility.Collapsed;
                }
                //EditorSet_Separator.Visibility = SupportedEditorSetElements.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            }
        }

        private void UpdateParsedEditorValue()
        {
            if (SelectedItem is UPropertyTreeViewEntry tvi && tvi.Property != null)
            {
                switch (tvi.Property)
                {
                    case IntProperty _:
                        if (tvi.Parent?.Property is StructProperty property && property.StructType == "Rotator")
                        {
                            //yes it is a float - we convert raw value to floating point degrees so we use float to raw int
                            if (float.TryParse(Value_TextBox.Text, out float degrees))
                            {
                                ParsedValue_TextBlock.Text = $"{degrees.ToUnrealRotationUnits()} (raw value)";
                            }
                            else
                            {
                                ParsedValue_TextBlock.Text = "Invalid value";
                            }
                        }
                        break;
                    case ObjectProperty _:
                        {
                            if (int.TryParse(Value_TextBox.Text, out int index))
                            {
                                if (index == 0)
                                {
                                    ParsedValue_TextBlock.Text = "Null";
                                }
                                else
                                {
                                    var entry = Pcc.getEntry(index);
                                    if (entry != null)
                                    {
                                        ParsedValue_TextBlock.Text = entry.GetFullPath;
                                    }
                                    else
                                    {
                                        ParsedValue_TextBlock.Text = "Index out of bounds of entry list";
                                    }
                                }
                            }
                            else
                            {
                                ParsedValue_TextBlock.Text = "Invalid value";
                            }
                        }
                        break;
                    case StringRefProperty _:
                        {
                            if (int.TryParse(Value_TextBox.Text, out int index))
                            {
                                string str;
                                switch (Pcc.Game)
                                {
                                    case MEGame.ME3:
                                        str = ME3TalkFiles.findDataById(index);
                                        str = str.Replace("\n", "[\\n]");
                                        if (str.Length > 82)
                                        {
                                            str = str.Substring(0, 80) + "...";
                                        }
                                        ParsedValue_TextBlock.Text = ME3TalkFiles.tlkList.Count == 0 ? "(.tlk not loaded)" : str.Replace(Environment.NewLine, "[\\n]");
                                        break;
                                    case MEGame.ME2:
                                        str = ME2Explorer.ME2TalkFiles.findDataById(index);
                                        str = str.Replace("\n", "[\\n]");
                                        if (str.Length > 82)
                                        {
                                            str = str.Substring(0, 80) + "...";
                                        }
                                        ParsedValue_TextBlock.Text = ME2Explorer.ME2TalkFiles.tlkList.Count == 0 ? "(.tlk not loaded)" : str.Replace(Environment.NewLine, "[\\n]");
                                        break;
                                    case MEGame.ME1:
                                        str = ME1Explorer.ME1TalkFiles.findDataById(index);
                                        str = str.Replace("\n", "[\\n]");
                                        if (str.Length > 82)
                                        {
                                            str = str.Substring(0, 80) + "...";
                                        }
                                        ParsedValue_TextBlock.Text = str.Replace(Environment.NewLine, "[\\n]");
                                        break;
                                }
                            }
                            else
                            {
                                ParsedValue_TextBlock.Text = "Invalid value";
                            }
                        }
                        break;
                    case NameProperty _:
                        {
                            if (int.TryParse(Value_TextBox.Text, out int index) && int.TryParse(NameIndex_TextBox.Text, out int number))
                            {
                                if (index >= 0 && index < Pcc.Names.Count)
                                {
                                    //ParsedValue_TextBlock.Text = Pcc.getNameEntry(index) + "_" + number;
                                }
                                else
                                {
                                    ParsedValue_TextBlock.Text = "Name index out of range";
                                }
                            }
                            else
                            {
                                ParsedValue_TextBlock.Text = "Invalid value(s)";
                            }
                        }
                        break;
                }
            }
        }

        private void UpdateHexboxPosition(UPropertyTreeViewEntry newSelectedItem)
        {
            if (newSelectedItem?.Property != null)
            {
                var hexPos = newSelectedItem.Property.ValueOffset;
                Interpreter_Hexbox.SelectionStart = hexPos;
                Interpreter_Hexbox.SelectionLength = 1; //maybe change

                Interpreter_Hexbox.UnhighlightAll();

                if (CurrentLoadedExport.ClassName != "Class")
                {
                    if (newSelectedItem.Property is StructProperty structProp)
                    {
                        //New selected property is struct property

                        switch (newSelectedItem.Parent.Property)
                        {
                            //If we are in an array
                            /* || newSelectedItem.Parent.Property is ArrayProperty<EnumProperty>*/
                            case ArrayProperty<StructProperty> _:
                                Interpreter_Hexbox.Highlight(structProp.StartOffset, structProp.GetLength(Pcc, true));
                                return;
                            case StructProperty structParentProp:
                                Interpreter_Hexbox.Highlight(structProp.StartOffset, structProp.GetLength(Pcc, structParentProp.IsImmutable));
                                return;
                            default:
                                Interpreter_Hexbox.Highlight(structProp.StartOffset, structProp.GetLength(Pcc));
                                break;
                        }
                    }
                    else if (newSelectedItem.Property is ArrayPropertyBase arrayProperty)
                    {
                        if (newSelectedItem.Parent.Property is StructProperty p && p.IsImmutable)
                        {
                            Interpreter_Hexbox.Highlight(arrayProperty.StartOffset, arrayProperty.GetLength(Pcc, true));
                        }
                        else
                        {
                            Interpreter_Hexbox.Highlight(arrayProperty.StartOffset, arrayProperty.GetLength(Pcc));
                        }
                        return;
                    }

                    switch (newSelectedItem.Property)
                    {
                        //case NoneProperty np:
                        //    Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset - 4, 8);
                        //    return;
                        //case StructProperty sp:
                        //    break;
                        case ObjectProperty _:
                        case FloatProperty _:
                        case IntProperty _:
                            {
                                switch (newSelectedItem.Parent.Property)
                                {
                                    case StructProperty p when p.IsImmutable:
                                        Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset, 4);
                                        return;
                                    case ArrayProperty<IntProperty> _:
                                    case ArrayProperty<FloatProperty> _:
                                    case ArrayProperty<ObjectProperty> _:
                                        Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset, 4);
                                        return;
                                    default:
                                        Interpreter_Hexbox.Highlight(newSelectedItem.Property.StartOffset, newSelectedItem.Property.ValueOffset + 4 - newSelectedItem.Property.StartOffset);
                                        break;
                                }
                                break;
                            }
                        case EnumProperty ep:
                            {
                                switch (newSelectedItem.Parent.Property)
                                {
                                    case StructProperty p when p.IsImmutable:
                                        Interpreter_Hexbox.Highlight(ep.ValueOffset, 8);
                                        return;
                                    case ArrayProperty<EnumProperty> _:
                                        Interpreter_Hexbox.Highlight(ep.ValueOffset, 8);
                                        return;
                                    default:
                                        Interpreter_Hexbox.Highlight(ep.StartOffset, ep.GetLength(Pcc));
                                        return;
                                }
                            }
                        case ByteProperty bp:
                            {
                                if ((newSelectedItem.Parent.Property is StructProperty p && p.IsImmutable)
                                    || newSelectedItem.Parent.Property is ArrayProperty<ByteProperty>)
                                {
                                    Interpreter_Hexbox.Highlight(bp.ValueOffset, 1);
                                    return;
                                }
                                break;
                            }
                        case StrProperty sp:
                            {
                                if (newSelectedItem.Parent.Property is StructProperty p && p.IsImmutable
                                 || newSelectedItem.Parent.Property is ArrayProperty<StrProperty>)
                                {
                                    Interpreter_Hexbox.Highlight(sp.StartOffset, sp.GetLength(Pcc, true));
                                    return;
                                }
                                Interpreter_Hexbox.Highlight(sp.StartOffset, sp.GetLength(Pcc));
                                break;
                            }
                        case BoolProperty boolp:
                            {
                                if (newSelectedItem.Parent.Property is StructProperty p && p.IsImmutable)
                                {
                                    Interpreter_Hexbox.Highlight(boolp.ValueOffset, 1);
                                    return;
                                }
                                Interpreter_Hexbox.Highlight(boolp.StartOffset, boolp.ValueOffset - boolp.StartOffset);
                                break;
                            }
                        case NameProperty np:
                            {
                                if (newSelectedItem.Parent.Property is StructProperty p && p.IsImmutable
                                 || newSelectedItem.Parent.Property is ArrayProperty<NameProperty>)
                                {
                                    Interpreter_Hexbox.Highlight(np.ValueOffset, 8);
                                    return;
                                }
                                Interpreter_Hexbox.Highlight(np.StartOffset, np.ValueOffset + 8 - np.StartOffset);
                                break;
                            }
                        case StringRefProperty srefp:
                            Interpreter_Hexbox.Highlight(srefp.StartOffset, srefp.GetLength(Pcc));
                            break;
                        case NoneProperty nonep:
                            Interpreter_Hexbox.Highlight(nonep.StartOffset, 8);
                            break;
                            //case EnumProperty ep:
                            //    Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset - 32, newSelectedItem.Property.GetLength(Pcc));
                            //    return;
                    }

                    //if (CurrentLoadedExport.ClassName != "Class")
                    //{
                    //    if (newSelectedItem.Property is StructProperty && newSelectedItem.Parent.Property is ArrayProperty<StructProperty> || newSelectedItem.Parent.Property is ArrayProperty<EnumProperty>)
                    //    {
                    //        Interpreter_Hexbox.Highlight(newSelectedItem.Property.StartOffset, newSelectedItem.Property.GetLength(Pcc, true));
                    //    }
                    //    else
                    //    {
                    //        Interpreter_Hexbox.Highlight(newSelectedItem.Property.StartOffset, newSelectedItem.Property.GetLength(Pcc));
                    //    }
                    //}
                    //else if (newSelectedItem.Parent.Property is ArrayProperty<IntProperty> || newSelectedItem.Parent.Property is ArrayProperty<FloatProperty> || newSelectedItem.Parent.Property is ArrayProperty<ObjectProperty>)
                    //{
                    //    Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset, 4);
                    //    return;
                    //}
                    //else if (newSelectedItem.Property is StructProperty)
                    //{
                    //    Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset - 40, newSelectedItem.Property.GetLength(Pcc));
                    //    return;
                    //}
                    //else
                    //{
                    //}
                    //array children
                    /*if (newSelectedItem.Parent.Property != null && newSelectedItem.Parent.Property.PropType == PropertyType.ArrayProperty)
                    {
                        if (newSelectedItem.Property is NameProperty np)
                        {
                            Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset, 8);
                        }
                        else
                        {
                            Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset, 4);
                        }
                        return;
                    }

                    else if (newSelectedItem.Parent.Property != null && newSelectedItem.Parent.Property.PropType == PropertyType.StructProperty)
                    {
                        //Determine if it is immutable or not, somehow.
                    }

                    else if (newSelectedItem.Property is StructProperty sp)
                    {
                        //struct size highlighting... not sure how to do this with this little info
                    }
                    else if (newSelectedItem.Property is NameProperty np)
                    {
                        Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset - 24, 32);
                    }
                    else if (newSelectedItem.Property is BoolProperty bp)
                    {
                        Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset - 24, 25);
                    }
                    else if (newSelectedItem.Parent.PropertyType != PropertyType.ArrayProperty.ToString())
                    {
                        Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset - 24, 28);
                    }*/
                }
                Interpreter_Hexbox_Host.UpdateLayout();
                Interpreter_Hexbox.Invalidate();
            }
        }

        private void Interpreter_Loaded(object sender, RoutedEventArgs e)
        {
            Interpreter_Hexbox = (HexBox)Interpreter_Hexbox_Host.Child;
            if (Interpreter_Hexbox.ByteProvider == null)
            {
                Interpreter_Hexbox.ByteProvider = new DynamicByteProvider();
            }
            //remove in the event this object is reloaded again
            Interpreter_Hexbox.ByteProvider.Changed -= Interpreter_Hexbox_BytesChanged;
            Interpreter_Hexbox.ByteProvider.Changed += Interpreter_Hexbox_BytesChanged;
        }

        private void Interpreter_Hexbox_BytesChanged(object sender, EventArgs e)
        {
            if (!isLoadingNewData)
            {
                HasUnsavedChanges = true;
            }
        }

        private void Interpreter_SaveHexChanges()
        {
            IByteProvider provider = Interpreter_Hexbox.ByteProvider;
            if (provider != null)
            {
                MemoryStream m = new MemoryStream();
                for (int i = 0; i < provider.Length; i++)
                    m.WriteByte(provider.ReadByte(i));
                CurrentLoadedExport.Data = m.ToArray();
            }
        }

        public override bool CanParse(IExportEntry exportEntry)
        {
            return true;
        }

        private void SetValue_Click(object sender, RoutedEventArgs args)
        {
            //todo: set value
            bool updated = false;
            if (SelectedItem is UPropertyTreeViewEntry tvi && tvi.Property != null)
            {
                UProperty property = tvi.Property;
                switch (property)
                {
                    case IntProperty ip:
                        {
                            //scoped for variable re-use

                            //ROTATORS
                            if (tvi.Parent?.Property is StructProperty structProperty && structProperty.StructType == "Rotator")
                            {
                                //yes it is a float - we convert raw value to floating point degrees so we use float to raw int
                                if (float.TryParse(Value_TextBox.Text, out float degrees))
                                {
                                    ip.Value = degrees.ToUnrealRotationUnits();
                                    updated = true;
                                }
                            }
                            else if (int.TryParse(Value_TextBox.Text, out int i) && i != ip.Value)
                            {
                                ip.Value = i;
                                updated = true;
                            }
                        }
                        break;
                    case FloatProperty fp:
                        {
                            if (float.TryParse(Value_TextBox.Text, out float f) && f != fp.Value)
                            {
                                fp.Value = f;
                                updated = true;
                            }
                        }
                        break;
                    case BoolProperty bp:
                        if (bp.Value != (Value_ComboBox.SelectedIndex == 0))
                        {
                            bp.Value = Value_ComboBox.SelectedIndex == 0; //0 = true
                            updated = true;
                        }
                        break;
                    case StrProperty sp:
                        if (sp.Value != Value_TextBox.Text)
                        {
                            sp.Value = Value_TextBox.Text;
                            updated = true;
                        }
                        break;
                    case StringRefProperty srp:
                        {
                            if (int.TryParse(Value_TextBox.Text, out int s) && s != srp.Value)
                            {
                                srp.Value = s;
                                updated = true;
                            }
                        }
                        break;
                    case ObjectProperty op:
                        {
                            if (int.TryParse(Value_TextBox.Text, out int o) && o != op.Value && (Pcc.isEntry(o) || o == 0))
                            {
                                op.Value = o;
                                updated = true;
                            }
                        }
                        break;
                    case EnumProperty ep:
                        if (ep.Value != (NameReference)Value_ComboBox.SelectedItem)
                        {
                            ep.Value = (NameReference)Value_ComboBox.SelectedItem; //0 = true
                            updated = true;
                        }
                        break;
                    case ByteProperty bytep:
                        if (byte.TryParse(Value_TextBox.Text, out byte b) && b != bytep.Value)
                        {
                            bytep.Value = b;
                            updated = true;
                        }
                        break;
                    case NameProperty namep:
                        //get string
                        string input = Value_ComboBox.Text;
                        var index = Pcc.findName(input);
                        if (index == -1)
                        {
                            //couldn't find name
                            if (MessageBoxResult.No == MessageBox.Show($"{Path.GetFileName(Pcc.FileName)} does not contain the Name: {input}\nWould you like to add it to the Name list?", "Name not found", MessageBoxButton.YesNo))
                            {
                                break;
                            }
                            else
                            {
                                index = Pcc.FindNameOrAdd(input);
                                //Wait for namelist to update. we may need to set a timer here.
                                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
                            }
                        }

                        bool nameindexok = int.TryParse(NameIndex_TextBox.Text, out int nameIndex);
                        nameindexok &= nameIndex >= 0;
                        if (index >= 0 && nameindexok)
                        {
                            namep.Value = new NameReference(input, nameIndex);
                            updated = true;
                        }
                        break;
                    default:
                        MessageBox.Show("This type cannot be set, please pester Mgamerz to fix this: " + property);
                        break;
                }
                //PropertyCollection props = CurrentLoadedExport.GetProperties();
                //props.Remove(tag);

                if (updated)
                {
                    //will cause a refresh from packageeditorwpf
                    CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);
                }
                //StartScan();
            }
        }

        private void ValueTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                SetValue_Click(null, null);
            }
        }

        private void Value_TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateParsedEditorValue();
        }

        private void AddArrayElement()
        {
            if (Interpreter_TreeView.SelectedItem is UPropertyTreeViewEntry tvi && tvi.Property != null)
            {
                string containingType = CurrentLoadedExport.ClassName;
                ArrayPropertyBase propertyToAddItemTo;
                int insertIndex;
                if (tvi.Property is ArrayPropertyBase arrayProperty)
                {
                    propertyToAddItemTo = arrayProperty;
                    insertIndex = arrayProperty.Count;
                    ArrayElementJustAdded = true;
                    if (tvi.Parent?.Property is StructProperty structProp)
                    {
                        containingType = structProp.StructType;
                    }
                }
                else if (tvi.Parent?.Property is ArrayPropertyBase parentArray)
                {
                    propertyToAddItemTo = parentArray;
                    insertIndex = tvi.Parent.ChildrenProperties.IndexOf(tvi);
                    ForcedRescanOffset = (int)tvi.Property.StartOffset;
                    if (tvi.Parent.Parent?.Property is StructProperty structProp)
                    {
                        containingType = structProp.StructType;
                    }
                }
                else
                {
                    return;
                }

                switch (propertyToAddItemTo)
                {
                    case ArrayProperty<NameProperty> anp:
                        NameProperty np = new NameProperty { Value = new NameReference(Pcc.getNameEntry(0)) };
                        anp.Insert(insertIndex, np);
                        break;
                    case ArrayProperty<ObjectProperty> aop:
                        aop.Insert(insertIndex, new ObjectProperty(0));
                        break;
                    case ArrayProperty<EnumProperty> aep:
                        {
                            PropertyInfo p = UnrealObjectInfo.GetPropertyInfo(Pcc.Game, aep.Name, containingType);
                            string typeName = p.reference;
                            EnumProperty ep = new EnumProperty(typeName, Pcc.Game);
                            aep.Insert(insertIndex, ep);
                        }
                        break;
                    case ArrayProperty<IntProperty> aip:
                        aip.Insert(insertIndex, new IntProperty(0));
                        break;
                    case ArrayProperty<FloatProperty> afp:
                        FloatProperty fp = new FloatProperty(0);
                        afp.Insert(insertIndex, fp);
                        break;
                    case ArrayProperty<StrProperty> asp:
                        asp.Insert(insertIndex, new StrProperty("Empty String"));
                        break;
                    case ArrayProperty<BoolProperty> abp:
                        abp.Insert(insertIndex, new BoolProperty(false));
                        break;
                    case ArrayProperty<StringRefProperty> astrf:
                        astrf.Insert(insertIndex, new StringRefProperty());
                        break;
                    case ArrayProperty<ByteProperty> abyte:
                        abyte.Insert(insertIndex, new ByteProperty(0));
                        break;
                    case ArrayProperty<StructProperty> astructp:
                        {
                            if (astructp.Count > 0)
                            {
                                astructp.Insert(insertIndex, astructp.Last()); //Bad form, but writing and reparse will correct it
                                break;
                            }

                            PropertyInfo p = UnrealObjectInfo.GetPropertyInfo(Pcc.Game, astructp.Name, containingType);
                            if (p == null)
                            {
                                //Attempt dynamic lookup
                                ClassInfo classInfo = null;
                                IExportEntry exportToBuildFor = CurrentLoadedExport;
                                if (CurrentLoadedExport.ClassName != "Class" && CurrentLoadedExport.idxClass > 0)
                                {
                                    exportToBuildFor = Pcc.getEntry(CurrentLoadedExport.idxClass) as IExportEntry;
                                }
                                switch (Pcc.Game)
                                {
                                    case MEGame.ME1:
                                        classInfo = ME1UnrealObjectInfo.generateClassInfo(exportToBuildFor);
                                        break;
                                    case MEGame.ME2:
                                        classInfo = ME2UnrealObjectInfo.generateClassInfo(exportToBuildFor);
                                        break;
                                    case MEGame.ME3:
                                        classInfo = ME3UnrealObjectInfo.generateClassInfo(exportToBuildFor);
                                        break;
                                }
                                p = UnrealObjectInfo.GetPropertyInfo(Pcc.Game, astructp.Name, containingType, classInfo);
                            }
                            if (p != null)
                            {
                                string typeName = p.reference;
                                PropertyCollection props = UnrealObjectInfo.getDefaultStructValue(Pcc.Game, typeName, true);
                                astructp.Insert(insertIndex, new StructProperty(typeName, props));
                            }
                        }
                        break;
                    default:
                        MessageBox.Show("Can't add this property type yet.\nPlease pester Mgamerz to get it implemented");
                        ArrayElementJustAdded = false;
                        return;
                }

                CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);
            }
        }

        private void RemoveArrayElement()
        {
            if (SelectedItem is UPropertyTreeViewEntry tvi
             && tvi.Parent?.Property is ArrayPropertyBase arrayProperty)
            {
                int index = tvi.Parent.ChildrenProperties.IndexOf(tvi);
                if (index >= 0)
                {
                    if (arrayProperty.Properties.HasExactly(1))
                    {
                        ForcedRescanOffset = (int)arrayProperty.StartOffset;
                    }
                    else
                    {
                        ForcedRescanOffset = (int)arrayProperty[index == arrayProperty.Count - 1 ? index - 1 : index].StartOffset;
                    }
                    arrayProperty.RemoveAt(index);

                    CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);
                }
                else
                {
                    Debug.WriteLine("DIDN'T REMOVE ANYTHING!");
                }
            }
        }

        public override void Dispose()
        {
            Interpreter_Hexbox = null;
            Interpreter_Hexbox_Host.Child.Dispose();
            Interpreter_Hexbox_Host.Dispose();
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new InterpreterWPF(), CurrentLoadedExport)
                {
                    Title = $"Interpreter - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.GetFullPath}_{CurrentLoadedExport.indexValue} - {Pcc.FileName}"
                };
                elhw.Show();
            }
        }

        private void MoveArrayElement(bool up)
        {
            if (SelectedItem is UPropertyTreeViewEntry tvi && tvi.Property != null && tvi.Parent?.Property is ArrayPropertyBase arrayProperty)
            {
                int index = tvi.Parent.ChildrenProperties.IndexOf(tvi);
                int swapIndex = up ? index - 1 : index + 1;
                //selection should be changed to the location of the element we are swapping with
                ForcedRescanOffset = (int)arrayProperty[swapIndex].StartOffset;
                arrayProperty.SwapElements(index, swapIndex);
                //Will force reload
                CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);
            }
        }
    }

    [DebuggerDisplay("UPropertyTreeViewEntry | {" + nameof(DisplayName) + "}")]
    public class UPropertyTreeViewEntry : NotifyPropertyChangedBase
    {
        static readonly string[] PropertyDumperSuppressedPropertyNames = { "CompressedTrackOffsets", "LookupTable" };

        private Brush _foregroundColor = Brushes.DarkSeaGreen;

        public IExportEntry AttachedExport;
        private string _colorStructCode;
        /// <summary>
        /// This property is used as a databinding workaround for when colorpicker is used as we can't convert back with a reference to the struct.
        /// </summary>
        public string ColorStructCode
        {
            get
            {
                if (Property is StructProperty colorStruct
                 && colorStruct.StructType == "Color")
                {
                    if (_colorStructCode != null) return _colorStructCode;

                    var a = colorStruct.GetProp<ByteProperty>("A").Value;
                    var r = colorStruct.GetProp<ByteProperty>("R").Value;
                    var g = colorStruct.GetProp<ByteProperty>("G").Value;
                    var b = colorStruct.GetProp<ByteProperty>("B").Value;

                    _colorStructCode = $"#{a:X2}{r:X2}{g:X2}{b:X2}";
                    return _colorStructCode;
                }
                return null;
            }
            set
            {
                if (_colorStructCode != value
                 && Property is StructProperty colorStruct
                 && colorStruct.StructType == "Color"
                 && ColorConverter.ConvertFromString(value) is Color newColor)
                {
                    var a = colorStruct.GetProp<ByteProperty>("A");
                    var r = colorStruct.GetProp<ByteProperty>("R");
                    var g = colorStruct.GetProp<ByteProperty>("G");
                    var b = colorStruct.GetProp<ByteProperty>("B");
                    a.Value = newColor.A;
                    r.Value = newColor.R;
                    g.Value = newColor.G;
                    b.Value = newColor.B;

                    _colorStructCode = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set => SetProperty(ref isSelected, value);
        }

        private bool isExpanded;
        public bool IsExpanded
        {
            get => isExpanded;
            set => SetProperty(ref isExpanded, value);
        }

        /// <summary>
        /// Used for inline editing. Return true to allow inline editing for property type
        /// </summary>
        public bool EditableType
        {
            get
            {
                if (Property == null) return false;

                switch (Property.PropType)
                {
                    //case Unreal.PropertyType.BoolProperty:
                    //case Unreal.PropertyType.ByteProperty:
                    case Unreal.PropertyType.FloatProperty:
                    case Unreal.PropertyType.IntProperty:
                    //case Unreal.PropertyType.NameProperty:
                    //case Unreal.PropertyType.StringRefProperty:
                    case Unreal.PropertyType.StrProperty:
                        //case Unreal.PropertyType.ObjectProperty:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public void ExpandParents()
        {
            if (Parent != null)
            {
                Parent.ExpandParents();
                Parent.IsExpanded = true;
            }
        }

        /// <summary>
        /// Flattens the tree into depth first order. Use this method for searching the list.
        /// </summary>
        /// <returns></returns>
        public List<UPropertyTreeViewEntry> FlattenTree()
        {
            var nodes = new List<UPropertyTreeViewEntry> { this };
            foreach (UPropertyTreeViewEntry tve in ChildrenProperties)
            {
                nodes.AddRange(tve.FlattenTree());
            }
            return nodes;
        }

        public event EventHandler PropertyUpdated;

        public UPropertyTreeViewEntry Parent { get; set; }

        /// <summary>
        /// The UProperty object from the export's properties that this node represents
        /// </summary>
        public UProperty Property { get; set; }
        /// <summary>
        /// List of children properties that link to this node.
        /// Only Struct and ArrayProperties will have this populated.
        /// </summary>
        public ObservableCollectionExtended<UPropertyTreeViewEntry> ChildrenProperties { get; set; }
        public UPropertyTreeViewEntry(UProperty property, string displayName = null)
        {
            Property = property;
            DisplayName = displayName;
            ChildrenProperties = new ObservableCollectionExtended<UPropertyTreeViewEntry>();
        }

        public UPropertyTreeViewEntry()
        {
            ChildrenProperties = new ObservableCollectionExtended<UPropertyTreeViewEntry>();
        }

        private string _editableValue;
        public string EditableValue
        {
            get => _editableValue ?? "";
            set
            {
                //Todo: Write property value here
                if (_editableValue != null && _editableValue != value)
                {
                    switch (Property)
                    {
                        case IntProperty intProp:
                            if (int.TryParse(value, out int parsedIntVal))
                            {
                                intProp.Value = parsedIntVal;
                                PropertyUpdated?.Invoke(this, EventArgs.Empty);
                                HasChanges = true;
                            }
                            else
                            {
                                return;
                            }
                            break;
                        case FloatProperty floatProp:
                            if (float.TryParse(value, out float parsedFloatVal))
                            {
                                floatProp.Value = parsedFloatVal;
                                PropertyUpdated?.Invoke(this, EventArgs.Empty);
                                HasChanges = true;
                            }
                            else
                            {
                                return;
                            }
                            break;
                    }
                }
                _editableValue = value;
            }
        }

        private bool _hasChanges;
        public bool HasChanges
        {
            get => _hasChanges;
            set => SetProperty(ref _hasChanges, value);
        }

        private string _displayName;
        public string DisplayName
        {
            get => _displayName ?? "DisplayName for this UPropertyTreeViewItem is null!";
            set => SetProperty(ref _displayName, value);
        }

        public bool IsUpdatable = false; //set to

        private string _parsedValue;
        public string ParsedValue
        {
            get => _parsedValue ?? "";
            set => SetProperty(ref _parsedValue, value);
        }

        public string RawPropertyType => Property != null ? Property.PropType.ToString() : "Currently loaded export";

        public string PropertyType
        {
            get
            {
                if (Property != null)
                {
                    switch (Property)
                    {
                        case ArrayPropertyBase arrayProp:
                            {
                                //we don't have reference to current pcc so we cannot look this up at this time.
                                //return $"ArrayProperty({(Property as ArrayProperty).arrayType})";
                                int count = arrayProp.Count;
                                return $"ArrayProperty - {count} item{(count != 1 ? "s" : "")}";
                            }
                        case StructProperty sp:
                            return $"{(sp.IsImmutable ? "Immutable " : "")}StructProperty({sp.StructType})";
                        case EnumProperty ep:
                            return $"ByteProperty(Enum): {ep.EnumType}";
                        default:
                            return Property.PropType.ToString();
                    }
                }

                return "Currently loaded export";
                //string type = UIndex < 0 ? "Imp" : "Exp";
                //return $"({type}) {UIndex} {Entry.ObjectName}({Entry.ClassName})"; */
            }
            //set { _displayName = value; }
        }

        public void PrintPretty(string indent, StreamWriter str, bool last, IExportEntry associatedExport)
        {
            bool supressNewLine = false;
            if (Property != null)
            {
                str.Write(indent);
                if (last)
                {
                    str.Write("└─");
                    indent += "  ";
                }
                else
                {
                    str.Write("├─");
                    indent += "| ";
                }
                //if (Parent != null && Parent == )
                str.Write(Property.Name + ": " + EditableValue);// + " "  " (" + PropertyType + ")");

                switch (Property)
                {
                    case ObjectProperty op:
                        {
                            //Resolve
                            string objectName = associatedExport.FileRef.GetEntryString(op.Value);
                            str.Write("  " + objectName);
                            break;
                        }
                    case NameProperty np:
                        //Resolve
                        str.Write($"  {np.Value}_{np.Value.Number}");
                        break;
                    case StringRefProperty srp:
                        {
                            if (associatedExport.FileRef.Game == MEGame.ME1)
                            {
                                //string objectName = associatedExport.FileRef.GetEntryString(srp.Value);
                                //if (InterpreterWPF.ME1TalkFiles == null)
                                //{
                                //    InterpreterWPF.ME1TalkFiles = new ME1Explorer.TalkFiles();
                                //}
                                //ParsedValue = InterpreterWPF.ME1TalkFiles.findDataById(srp.Value);
                                str.Write(" " + ParsedValue);
                            }

                            break;
                        }
                }

                bool isArrayPropertyTable = Property.GetType().IsOfGenericType(typeof(ArrayProperty<>));
                if (ChildrenProperties.Count > 1000 && isArrayPropertyTable)
                {
                    str.Write($" is very large array ({ChildrenProperties.Count} items) - skipping");
                    return;
                }

                if (PropertyDumperSuppressedPropertyNames.Any(x => x == Property.Name))
                {
                    str.Write(" - suppressed by data dumper.");
                    return;
                }
            }
            else
            {
                supressNewLine = true;
            }
            for (int i = 0; i < ChildrenProperties.Count; i++)
            {
                if (ChildrenProperties[i].Property is NoneProperty)
                {
                    continue;
                }
                if (!supressNewLine)
                {
                    str.Write("\n");
                }
                else
                {
                    supressNewLine = false;
                }
                ChildrenProperties[i].PrintPretty(indent, str, i == ChildrenProperties.Count - 1 || (i == ChildrenProperties.Count - 2 && ChildrenProperties[ChildrenProperties.Count - 1].Property is NoneProperty), associatedExport);
            }
        }

        public override string ToString()
        {
            return "UPropertyTreeViewEntry " + DisplayName;
        }
    }

}
