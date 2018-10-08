using ME1Explorer.Unreal;
using ME1Explorer.Unreal.Classes;
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
using System.Runtime.CompilerServices;
using ME3Explorer.SharedUI;
using System.Windows.Input;

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
        public readonly string[] ExportToStringConverters = { "LevelStreamingKismet" };
        /// <summary>
        /// The current list of loaded properties that back the property tree.
        /// This is used so we can do direct object reference comparisons for things like removal
        /// </summary>
        private PropertyCollection CurrentLoadedProperties;
        //Values in this list will cause custom code to be fired to modify what the displayed string is for IntProperties
        //when the class matches.
        public readonly string[] IntToStringConverters = { "WwiseEvent" };

        private Dictionary<string, List<PropertyReader.Property>> defaultStructValues;
        //private byte[] memory;
        //private int memsize;
        private string className;
        private BioTlkFileSet tlkset;
        private BioTlkFileSet editorTlkSet;
        int readerpos;
        int RescanSelectionOffset = 0;
        private List<FrameworkElement> EditorSetElements = new List<FrameworkElement>();
        public struct PropHeader
        {
            public int name;
            public int type;
            public int size;
            public int index;
            public int offset;
        }

        public string[] Types =
        {
            "StructProperty", //0
            "IntProperty",
            "FloatProperty",
            "ObjectProperty",
            "NameProperty",
            "BoolProperty",  //5
            "ByteProperty",
            "ArrayProperty",
            "StrProperty",
            "StringRefProperty",
            "DelegateProperty",//10
            "None",
            "BioMask4Property",
        };
        private HexBox Interpreter_Hexbox;

        public enum nodeType
        {
            Unknown = -1,
            StructProperty = 0,
            IntProperty = 1,
            FloatProperty = 2,
            ObjectProperty = 3,
            NameProperty = 4,
            BoolProperty = 5,
            ByteProperty = 6,
            ArrayProperty = 7,
            StrProperty = 8,
            StringRefProperty = 9,
            DelegateProperty = 10,
            None,
            BioMask4Property,

            ArrayLeafObject,
            ArrayLeafName,
            ArrayLeafEnum,
            ArrayLeafStruct,
            ArrayLeafBool,
            ArrayLeafString,
            ArrayLeafFloat,
            ArrayLeafInt,
            ArrayLeafByte,

            StructLeafByte,
            StructLeafFloat,
            StructLeafDeg, //indicates this is a StructProperty leaf that is in degrees (actually unreal rotation units)
            StructLeafInt,
            StructLeafObject,
            StructLeafName,
            StructLeafBool,
            StructLeafStr,
            StructLeafArray,
            StructLeafEnum,
            StructLeafStruct,

            Root,
        }

        public InterpreterWPF()
        {
            LoadCommands();
            InitializeComponent();
            EditorSetElements.Add(Value_TextBox); //str, strref, int, float, obj
            EditorSetElements.Add(Value_ComboBox); //bool, name
            EditorSetElements.Add(NameIndexPrefix_TextBlock); //nameindex
            EditorSetElements.Add(NameIndex_TextBox); //nameindex
            EditorSetElements.Add(ParsedValue_TextBlock);

            defaultStructValues = new Dictionary<string, List<PropertyReader.Property>>();
        }

        #region Commands
        public ICommand RemovePropertyCommand { get; set; }
        private void LoadCommands()
        {
            // Player commands
            RemovePropertyCommand = new RelayCommand(RemoveProperty, CanRemoveProperty);
        }

        private void RemoveProperty(object obj)
        {
            UPropertyTreeViewEntry tvi = (UPropertyTreeViewEntry)Interpreter_TreeView.SelectedItem;
            if (tvi != null && tvi.Property != null)
            {
                UProperty tag = tvi.Property;
                PropertyCollection props = CurrentLoadedExport.GetProperties();
                props.Remove(tag);
                CurrentLoadedExport.WriteProperties(props);
                StartScan();
            }
        }

        private bool CanRemoveProperty(object obj)
        {
            UPropertyTreeViewEntry tvi = (UPropertyTreeViewEntry)Interpreter_TreeView.SelectedItem;
            if (tvi != null)
            {
                return tvi.Parent != null && tvi.Parent.Parent == null; //only items with a single parent (root nodes)
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
            Interpreter_Hexbox.ByteProvider = new DynamicByteProvider(new byte[] { });
            PropertyNodes.Clear();
        }

        /// <summary>
        /// Load a new export for display and editing in this control
        /// </summary>
        /// <param name="export"></param>
        public override void LoadExport(IExportEntry export)
        {
            EditorSetElements.ForEach(x => x.Visibility = Visibility.Collapsed);

            //check rescan
            //TODO: Make this more reliable because it is recycling virtualization
            if (CurrentLoadedExport != null && export.FileRef == CurrentLoadedExport.FileRef && export.UIndex == CurrentLoadedExport.UIndex)
            {
                UPropertyTreeViewEntry tvi = (UPropertyTreeViewEntry)Interpreter_TreeView.SelectedItem;
                if (tvi != null && tvi.Property != null)
                {
                    RescanSelectionOffset = (int)tvi.Property.Offset;
                }
            }
            else
            {
                RescanSelectionOffset = 0;
            }
            CurrentLoadedExport = export;
            //List<byte> bytes = export.Data.ToList();
            //MemoryStream ms = new MemoryStream(); //initializing memorystream directly with byte[] does not allow it to expand.
            //ms.Write(export.Data, 0, export.Data.Length); //write the data into the memorystream.
            Interpreter_Hexbox.ByteProvider = new DynamicByteProvider(export.Data);
            className = export.ClassName;

            if (CurrentLoadedExport.FileRef.Game == MEGame.ME1)
            {
                // attempt to find a TlkFileSet associated with the object, else just pick the first one and hope it's correct
                if (editorTlkSet == null)
                {
                    try
                    {
                        IntProperty tlkSetRef = export.GetProperty<IntProperty>("m_oTlkFileSet");
                        if (tlkSetRef != null)
                        {
                            tlkset = new BioTlkFileSet(CurrentLoadedExport.FileRef as ME1Package, tlkSetRef.Value - 1);
                        }
                        else
                        {
                            tlkset = new BioTlkFileSet(CurrentLoadedExport.FileRef as ME1Package);
                        }
                    }
                    catch (Exception e)
                    {
                        tlkset = new BioTlkFileSet(CurrentLoadedExport.FileRef as ME1Package);
                    }
                }
                else
                {
                    tlkset = editorTlkSet;
                }
            }
            StartScan();
        }

        /// <summary>
        /// Call this when reloading the entire tree.
        /// </summary>
        /// <param name="expandedItems"></param>
        /// <param name="topNodeName"></param>
        /// <param name="selectedNodeName"></param>
        private void StartScan(IEnumerable<string> expandedItems = null, string topNodeName = null, string selectedNodeName = null)
        {
            PropertyNodes.Clear();
            readerpos = CurrentLoadedExport.GetPropertyStart();

            UPropertyTreeViewEntry topLevelTree = new UPropertyTreeViewEntry()
            {
                DisplayName = $"Export {CurrentLoadedExport.UIndex }: { CurrentLoadedExport.ObjectName} ({CurrentLoadedExport.ClassName})",
                IsExpanded = true
            };

            PropertyNodes.Add(topLevelTree);

            try
            {
                CurrentLoadedProperties = CurrentLoadedExport.GetProperties(includeNoneProperties: true);
                foreach (UProperty prop in CurrentLoadedProperties)
                {
                    GenerateTreeForProperty(prop, topLevelTree);
                }
            }
            catch (Exception ex)
            {
                UPropertyTreeViewEntry errorNode = new UPropertyTreeViewEntry()
                {
                    DisplayName = $"PARSE ERROR {ex.Message}"
                };
                topLevelTree.ChildrenProperties.Add(errorNode);
            }

            if (RescanSelectionOffset != 0)
            {
                var flattenedTree = topLevelTree.FlattenTree();
                var itemToSelect = flattenedTree.FirstOrDefault(x => x.Property != null && x.Property.Offset == RescanSelectionOffset);
                if (itemToSelect != null)
                {
                    itemToSelect.ExpandParents();
                    itemToSelect.IsSelected = true;
                }
            }
            /*try
            {
                List<PropHeader> topLevelHeaders = ReadHeadersTillNone();
                GenerateTree(topLevelTree, topLevelHeaders);
            }
            catch (Exception ex)
            {
                TreeViewItem errorNode = new TreeViewItem()
                {
                    Header = $"PARSE ERROR {ex.Message}"
                };
                topLevelTree.Items.Add(errorNode);
                //addPropButton.Visible = false;
                //removePropertyButton.Visible = false;
            }*/
            //Interpreter_TreeView.CollapseAll();
            //Interpreter_TreeView.Items[0].Expand();

            /*TreeViewItem[] Items;
            if (expandedItems != null)
            {
                int memDiff = memory.Length - memsize;
                int selectedPos = getPosFromNode(selectedNodeName);
                int curPos = 0;
                foreach (string item in expandedItems)
                {
                    curPos = getPosFromNode(item);
                    if (curPos > selectedPos)
                    {
                        curPos += memDiff;
                    }
                    Items = Interpreter_TreeView.Items.Find((item[0] == '-' ? -curPos : curPos).ToString(), true);
                    if (Items.Length > 0)
                    {
                        foreach (var node in Items)
                        {
                            node.Expand();
                        }
                    }
                }
            }
            Items = Interpreter_TreeView.Items.Find(topNodeName, true);
            if (Items.Length > 0)
            {
                Interpreter_TreeView.TopNode = Items[0];
            }
            Items = Interpreter_TreeView.Items.Find(selectedNodeName, true);
            if (Items.Length > 0)
            {
                Interpreter_TreeView.SelectedNode = Items[0];
            }
            else
            {
                Interpreter_TreeView.S = Interpreter_TreeView.Items[0];
            }*/
        }



        private void GenerateTreeForProperty(UProperty prop, UPropertyTreeViewEntry parent, string displayPrefix = "")
        {
            var upropertyEntry = GenerateUPropertyTreeViewEntry(prop, parent);
            if (prop.PropType == PropertyType.ArrayProperty)
            {
                int i = 0;
                foreach (UProperty listProp in (prop as ArrayPropertyBase).ValuesAsProperties)
                {
                    GenerateTreeForProperty(listProp, upropertyEntry, " Item " + (i++));
                }
            }
            if (prop.PropType == PropertyType.StructProperty)
            {
                var sProp = prop as StructProperty;
                foreach (var subProp in sProp.Properties)
                {
                    GenerateTreeForProperty(subProp, upropertyEntry);
                }
            }
        }

        private UPropertyTreeViewEntry GenerateUPropertyTreeViewEntry(UProperty prop, UPropertyTreeViewEntry parent, string displayPrefix = "")
        {
            string displayName = $"{prop.Offset.ToString("X4")}{displayPrefix}: {prop.Name}:";
            string editableValue = ""; //editable value
            string parsedValue = ""; //human formatted item. Will most times be blank
            switch (prop)
            {
                case ObjectProperty op:
                    {
                        int index = op.Value;
                        var entry = CurrentLoadedExport.FileRef.getEntry(index);
                        if (entry != null)
                        {
                            editableValue = index.ToString();
                            parsedValue = entry.GetFullPath;
                            if (index > 0 && ExportToStringConverters.Contains(entry.ClassName))
                            {
                                editableValue += " " + ExportToString(CurrentLoadedExport.FileRef.Exports[index - 1]);
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
                            parsedValue = "Index out of bounds of " + (index < 0 ? "Import" : "Export") + " list";
                        }
                    }
                    break;
                case IntProperty ip:
                    {
                        editableValue = ip.Value.ToString();
                        if (IntToStringConverters.Contains(CurrentLoadedExport.ClassName))
                        {
                            parsedValue = IntToString(prop.Name, ip.Value);
                        }
                    }
                    break;
                case FloatProperty fp:
                    {
                        editableValue = fp.Value.ToString();
                    }
                    break;
                case BoolProperty bp:
                    {
                        editableValue = bp.Value.ToString(); //combobox
                    }
                    break;
                case ArrayPropertyBase ap:
                    {
                        //todo - assign bottom text to show array type.
                        ArrayType at = GetArrayType(prop.Name.Name);
                        parsedValue = $"{at.ToString()} array, {ap.ValuesAsProperties.Count()} items";
                    }
                    break;
                case NameProperty np:
                    editableValue = np.NameTableIndex.ToString() + "_" + np.Name.Number.ToString();
                    parsedValue = np.Value + "_" + np.Name.Number.ToString(); //will require special 2-box setup
                    break;
                case ByteProperty bp:
                    editableValue = (prop as ByteProperty).Value.ToString();
                    parsedValue = (prop as ByteProperty).Value.ToString();

                    break;
                case EnumProperty ep:
                    //editableValue = (prop as EnumProperty).Value.ToString();
                    parsedValue = (prop as EnumProperty).Value;
                    break;
                case StringRefProperty strrefp:
                    editableValue = strrefp.Value.ToString();
                    if (CurrentLoadedExport.FileRef.Game == MEGame.ME3)
                    {
                        parsedValue = ME3TalkFiles.tlkList.Count == 0 ? "(.tlk not loaded)" : ME3TalkFiles.findDataById(strrefp.Value);
                    }
                    break;
                case StrProperty strp:
                    editableValue = strp.Value;
                    break;
                case StructProperty sp:
                    parsedValue = sp.StructType;
                    break;
                case NoneProperty np:
                    parsedValue = "End of properties";
                    break;
            }
            UPropertyTreeViewEntry item = new UPropertyTreeViewEntry()
            {
                Property = prop,
                EditableValue = editableValue,
                ParsedValue = parsedValue,
                DisplayName = displayName,
                Parent = parent,
            };
            item.PropertyUpdated += OnPropertyUpdated;
            parent.ChildrenProperties.Add(item);
            return item;
        }

        /// <summary>
        /// This handler is assigned to UPropertyTreeViewEntry and is called
        /// when the value stored by the entry is updated.
        /// </summary>
        /// <param name="sender">Calling UPropertyTreeViewEntry object</param>
        /// <param name="e"></param>
        private void OnPropertyUpdated(object sender, EventArgs e)
        {
            Debug.WriteLine("prop updated");
            UPropertyTreeViewEntry Sender = sender as UPropertyTreeViewEntry;
            if (Sender != null && Sender.Property != null)
            {
                int offset = (int)Sender.Property.Offset;

                switch (Sender.Property.PropType)
                {
                    case PropertyType.IntProperty:
                        Interpreter_Hexbox.ByteProvider.WriteBytes(offset, BitConverter.GetBytes((Sender.Property as IntProperty).Value));
                        break;
                    case PropertyType.FloatProperty:
                        Interpreter_Hexbox.ByteProvider.WriteBytes(offset, BitConverter.GetBytes((Sender.Property as FloatProperty).Value));
                        break;
                }
                Interpreter_Hexbox.Refresh();
            }
        }

        /// <summary>
        /// Converts a value of a property into a more human readable string.
        /// This is for IntProperty.
        /// </summary>
        /// <param name="name">Name of the property</param>
        /// <param name="value">Value of the property to transform</param>
        /// <returns></returns>
        private string IntToString(NameReference name, int value)
        {
            switch (CurrentLoadedExport.ClassName)
            {
                case "WwiseEvent":
                    switch (name)
                    {
                        case "Id":
                            return " (0x" + value.ToString("X8") + ")";
                    }
                    break;
            }
            return "";
        }

        private void GenerateTreeForArrayProperty(UProperty prop, UPropertyTreeViewEntry parent, int index)
        {
            string displayPrefix = prop.Offset.ToString("X4") + " Item " + index + ": ";
            string displayValue = "";
            switch (prop.PropType)
            {
                case PropertyType.ObjectProperty:
                    int oIndex = (prop as ObjectProperty).Value;
                    displayValue = ": [" + oIndex + "] ";
                    if (oIndex > 0 || oIndex < 0)
                    {
                        if (oIndex <= CurrentLoadedExport.FileRef.ExportCount && oIndex > CurrentLoadedExport.FileRef.ImportCount * -1)
                        {
                            displayValue = CurrentLoadedExport.FileRef.getEntry(oIndex).GetFullPath;
                            if (oIndex > 0 && ExportToStringConverters.Contains(CurrentLoadedExport.FileRef.Exports[oIndex - 1].ClassName))
                            {
                                displayValue += " " + ExportToString(CurrentLoadedExport.FileRef.Exports[oIndex - 1]);
                            }
                        }
                        else
                        {
                            displayValue = "Object index out of bounds of PCC imports/exports";
                        }
                    }
                    else
                    {
                        displayValue = "Null";
                    }
                    break;
                case PropertyType.StructProperty:
                    //TODO
                    break;
                case PropertyType.BoolProperty:
                    displayValue = (prop as BoolProperty).Value.ToString();
                    break;
                case PropertyType.IntProperty:
                    displayValue = (prop as IntProperty).Value.ToString();
                    break;
                case PropertyType.NameProperty:
                    displayValue = (prop as NameProperty).NameTableIndex + " " + (prop as NameProperty).Value;

                    break;
            }


            UPropertyTreeViewEntry item = new UPropertyTreeViewEntry()
            {
                DisplayName = displayPrefix,
                EditableValue = displayValue,
                Property = prop,
                Parent = parent
            };

            parent.ChildrenProperties.Add(item);
            if (prop.PropType == PropertyType.ArrayProperty)
            {
                int i = 0;
                foreach (UProperty listProp in (prop as ArrayPropertyBase).ValuesAsProperties)
                {
                    GenerateTreeForArrayProperty(listProp, item, i++);
                }
            }
            if (prop.PropType == PropertyType.StructProperty)
            {
                var sProp = prop as StructProperty;
                foreach (var subProp in sProp.Properties)
                {
                    GenerateTreeForProperty(subProp, item);
                }
            }
        }

        private string ExportToString(IExportEntry exportEntry)
        {
            switch (exportEntry.ObjectName)
            {
                case "LevelStreamingKismet":
                    NameProperty prop = exportEntry.GetProperty<NameProperty>("PackageName");
                    return "(" + prop.Value.Name + "_" + prop.Value.Number + ")";
            }
            return "";
        }

        private void hb1_SelectionChanged(object sender, EventArgs e)
        {
            int start = (int)Interpreter_Hexbox.SelectionStart;
            int len = (int)Interpreter_Hexbox.SelectionLength;
            int size = (int)Interpreter_Hexbox.ByteProvider.Length;
            //TODO: Optimize this so this is only called when data has changed
            byte[] currentData = (Interpreter_Hexbox.ByteProvider as DynamicByteProvider).Bytes.ToArray();
            try
            {
                if (currentData != null && start != -1 && start < size)
                {
                    string s = $"Byte: {currentData[start]}"; //if selection is same as size this will crash.
                    if (start <= currentData.Length - 4)
                    {
                        int val = BitConverter.ToInt32(currentData, start);
                        s += $", Int: {val}";
                        if (CurrentLoadedExport.FileRef.isName(val))
                        {
                            s += $", Name: {CurrentLoadedExport.FileRef.getNameEntry(val)}";
                        }
                        if (CurrentLoadedExport.FileRef.getEntry(val) is IExportEntry exp)
                        {
                            s += $", Export: {exp.ObjectName}";
                        }
                        else if (CurrentLoadedExport.FileRef.getEntry(val) is ImportEntry imp)
                        {
                            s += $", Import: {imp.ObjectName}";
                        }
                    }
                    s += $" | Start=0x{start.ToString("X8")} ";
                    if (len > 0)
                    {
                        s += $"Length=0x{len.ToString("X8")} ";
                        s += $"End=0x{(start + len - 1).ToString("X8")}";
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
            }
        }

        #region UnrealObjectInfo
        private PropertyInfo GetPropertyInfo(int propName)
        {
            switch (CurrentLoadedExport.FileRef.Game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getPropertyInfo(className, CurrentLoadedExport.FileRef.getNameEntry(propName));
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getPropertyInfo(className, CurrentLoadedExport.FileRef.getNameEntry(propName));
                case MEGame.ME3:
                case MEGame.UDK:
                    return ME3UnrealObjectInfo.getPropertyInfo(className, CurrentLoadedExport.FileRef.getNameEntry(propName));
            }
            return null;
        }

        private PropertyInfo GetPropertyInfo(string propname, string typeName, bool inStruct = false, ClassInfo nonVanillaClassInfo = null)
        {
            switch (CurrentLoadedExport.FileRef.Game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getPropertyInfo(typeName, propname, inStruct, nonVanillaClassInfo);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getPropertyInfo(typeName, propname, inStruct, nonVanillaClassInfo);
                case MEGame.ME3:
                case MEGame.UDK:
                    return ME3UnrealObjectInfo.getPropertyInfo(typeName, propname, inStruct, nonVanillaClassInfo);
            }
            return null;
        }

        private static ArrayType GetArrayType(PropertyInfo propInfo, IMEPackage pcc)
        {
            switch (pcc.Game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getArrayType(propInfo);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getArrayType(propInfo);
                case MEGame.ME3:
                case MEGame.UDK:
                    return ME3UnrealObjectInfo.getArrayType(propInfo);
            }
            return ArrayType.Int;
        }

        private ArrayType GetArrayType(string propName, string typeName = null)
        {
            if (typeName == null)
            {
                typeName = className;
            }
            switch (CurrentLoadedExport.FileRef.Game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getArrayType(typeName, propName, export: CurrentLoadedExport);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getArrayType(typeName, propName, export: CurrentLoadedExport);
                case MEGame.ME3:
                case MEGame.UDK:
                    return ME3UnrealObjectInfo.getArrayType(typeName, propName, export: CurrentLoadedExport);
            }
            return ArrayType.Int;
        }

        private ArrayType GetArrayType(int propName, string typeName = null)
        {
            if (typeName == null)
            {
                typeName = className;
            }
            switch (CurrentLoadedExport.FileRef.Game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getArrayType(typeName, CurrentLoadedExport.FileRef.getNameEntry(propName), export: CurrentLoadedExport);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getArrayType(typeName, CurrentLoadedExport.FileRef.getNameEntry(propName), export: CurrentLoadedExport);
                case MEGame.ME3:
                case MEGame.UDK:
                    return ME3UnrealObjectInfo.getArrayType(typeName, CurrentLoadedExport.FileRef.getNameEntry(propName), export: CurrentLoadedExport);
            }
            return ArrayType.Int;
        }

        //private List<string> GetEnumValues(string enumName, int propName)
        private List<string> GetEnumValues(string enumName, string propName)
        {
            switch (CurrentLoadedExport.FileRef.Game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getEnumfromProp(className, propName);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getEnumfromProp(className, propName);
                case MEGame.ME3:
                case MEGame.UDK:
                    return ME3UnrealObjectInfo.getEnumValues(enumName, true);
            }
            return null;
        }
        #endregion

        private void Interpreter_ToggleHexboxWidth_Click(object sender, System.Windows.RoutedEventArgs e)
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

        private void Interpreter_TreeViewSelectedItemChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, (NoArgDelegate)delegate { UpdateHexboxPosition(e.NewValue as UPropertyTreeViewEntry); });
            UPropertyTreeViewEntry newSelectedItem = (UPropertyTreeViewEntry)e.NewValue;
            //list of visible elements for editing
            List<FrameworkElement> SupportedEditorSetElements = new List<FrameworkElement>();
            if (newSelectedItem != null && newSelectedItem.Property != null)
            {
                switch (newSelectedItem.Property)
                {
                    case IntProperty ip:
                        Value_TextBox.Text = ip.Value.ToString();
                        SupportedEditorSetElements.Add(Value_TextBox);
                        break;

                    case FloatProperty fp:
                        Value_TextBox.Text = fp.Value.ToString();
                        SupportedEditorSetElements.Add(Value_TextBox);
                        break;
                    case BoolProperty bp:
                        {
                            SupportedEditorSetElements.Add(Value_ComboBox);
                            List<string> values = new List<string>(new string[] { "True", "False" });
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
                    case EnumProperty ep:
                        {
                            SupportedEditorSetElements.Add(Value_ComboBox);
                            List<string> values = ep.EnumValues;
                            Value_ComboBox.ItemsSource = values;
                            int indexSelected = values.IndexOf(ep.Value.Name);
                            Value_ComboBox.SelectedIndex = indexSelected;
                        }
                        break;
                    case StringRefProperty strrefp:
                        {
                            Value_TextBox.Text = strrefp.Value.ToString();
                            SupportedEditorSetElements.Add(Value_TextBox);
                            SupportedEditorSetElements.Add(ParsedValue_TextBlock);
                            UpdateParsedEditorValue();
                        }
                        break;
                }

                //Hide the non-used controls
                foreach (FrameworkElement fe in EditorSetElements)
                {
                    fe.Visibility = SupportedEditorSetElements.Contains(fe) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private void UpdateParsedEditorValue()
        {
            UPropertyTreeViewEntry tvi = (UPropertyTreeViewEntry)Interpreter_TreeView.SelectedItem;
            if (tvi != null && tvi.Property != null)

                switch (tvi.Property)
                {
                    case ObjectProperty op:
                        {
                            if (int.TryParse(Value_TextBox.Text, out int index))
                            {
                                if (index == 0)
                                {
                                    ParsedValue_TextBlock.Text = "Null";
                                }
                                else
                                {
                                    var entry = CurrentLoadedExport.FileRef.getEntry(index);
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
                    case StringRefProperty sp:
                        {
                            if (int.TryParse(Value_TextBox.Text, out int index))
                            {
                                if (CurrentLoadedExport.FileRef.Game == MEGame.ME3)
                                {
                                    string str = ME3TalkFiles.findDataById(index);
                                    str = str.Replace("\n", "[\\n]");
                                    if (str.Length > 80)
                                    {
                                        str = str.Substring(0, 80) + "...";
                                    }
                                    ParsedValue_TextBlock.Text = ME3TalkFiles.tlkList.Count == 0 ? "(.tlk not loaded)" : str.Replace(System.Environment.NewLine, "[\\n]");
                                }
                            }
                            else
                            {
                                ParsedValue_TextBlock.Text = "Invalid value";
                            }
                        }
                        break;
                }
        }
        /// <summary>
        /// Tree view selected item changed. This runs in a delegate due to how multithread bubble-up items work with treeview.
        /// Without this delegate, the item selected will randomly be a parent item instead.
        /// From https://www.codeproject.com/Tips/208896/WPF-TreeView-SelectedItemChanged-called-twice
        /// </summary>
        private delegate void NoArgDelegate();



        private void UpdateHexboxPosition(UPropertyTreeViewEntry newSelectedItem)
        {
            if (newSelectedItem != null && newSelectedItem.Property != null)
            {
                var hexPos = newSelectedItem.Property.Offset;
                Interpreter_Hexbox.SelectionStart = hexPos;
                Interpreter_Hexbox.SelectionLength = 1; //maybe change
            }
        }

        private void Interpreter_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Interpreter_Hexbox = (HexBox)Interpreter_Hexbox_Host.Child;
        }

        private void Interpreter_SaveHexChanged_Click(object sender, System.Windows.RoutedEventArgs e)
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

        private void Interpreter_TreeView_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TreeViewItem treeViewItem = VisualUpwardSearch(e.OriginalSource as DependencyObject);

            if (treeViewItem != null)
            {
                treeViewItem.Focus();
                e.Handled = true;

                if (treeViewItem.Tag != null)
                {
                    if (treeViewItem.Tag is ArrayPropertyBase)
                    {
                        Interpreter_TreeView.ContextMenu = Interpreter_TreeView.Resources["ArrayPropertyContext"] as System.Windows.Controls.ContextMenu;
                    }
                    else
                    {
                        Interpreter_TreeView.ContextMenu = Interpreter_TreeView.Resources["FolderContext"] as System.Windows.Controls.ContextMenu;
                    }
                }
            }
        }

        static TreeViewItem VisualUpwardSearch(DependencyObject source)
        {
            while (source != null && !(source is TreeViewItem))
                source = VisualTreeHelper.GetParent(source);

            return source as TreeViewItem;
        }

        private void RemovePropertyCommand_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            TreeViewItem tvi = (TreeViewItem)Interpreter_TreeView.SelectedItem;
            if (tvi != null)
            {
                UProperty tag = (UProperty)tvi.Tag;
                CurrentLoadedProperties.Remove(tag);
                CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);
                StartScan();
            }
        }

        private void ArrayOrderByValueCommand_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            TreeViewItem tvi = (TreeViewItem)Interpreter_TreeView.SelectedItem;
            if (tvi != null)
            {
                UProperty tag = (UProperty)tvi.Tag;
                ArrayType at = GetArrayType(tag.Name); //we may need to account for substructs
                bool sorted = false;
                switch (at)
                {
                    case ArrayType.Object:
                        {
                            ArrayProperty<ObjectProperty> list = (ArrayProperty<ObjectProperty>)tag;
                            List<ObjectProperty> sortedList = list.ToList();
                            sortedList.Sort();
                            list.Values.Clear();
                            list.Values.AddRange(sortedList);
                            sorted = true;
                        }
                        break;
                    case ArrayType.Int:
                        {
                            ArrayProperty<IntProperty> list = (ArrayProperty<IntProperty>)tag;
                            List<IntProperty> sortedList = list.ToList();
                            sortedList.Sort();
                            list.Values.Clear();
                            list.Values.AddRange(sortedList);
                            sorted = true;
                        }
                        break;
                    case ArrayType.Float:
                        {
                            ArrayProperty<FloatProperty> list = (ArrayProperty<FloatProperty>)tag;
                            List<FloatProperty> sortedList = list.ToList();
                            sortedList.Sort();
                            list.Values.Clear();
                            list.Values.AddRange(sortedList);
                            sorted = true;
                        }
                        break;
                }
                if (sorted)
                {
                    ItemsControl i = GetSelectedTreeViewItemParent(tvi);
                    //write at root node level
                    if (i.Tag is nodeType && (nodeType)i.Tag == nodeType.Root)
                    {
                        CurrentLoadedExport.WriteProperty(tag);
                        StartScan();
                    }

                    //have to figure out how to deal with structproperties or array of array propertie
                    /*if (tvi.Tag is StructProperty)
                    {
                        StructProperty sp = tvi.Tag as StructProperty;
                        sp.Properties.
                        CurrentLoadedExport.WriteProperty(tag);
                        StartScan();
                    }*/
                }
                //PropertyCollection props = CurrentLoadedExport.GetProperties();
                //props.Remove(tag);
                //CurrentLoadedExport.WriteProperties(props);
                //
            }
        }

        public ItemsControl GetSelectedTreeViewItemParent(TreeViewItem item)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(item);
            while (!(parent is TreeViewItem || parent is TreeView))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            return parent as ItemsControl;
        }

        private void Interpreter_AddProperty_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentLoadedExport.FileRef.Game == MEGame.UDK)
            {
                MessageBox.Show("Cannot add properties to UDK UPK files.", "Unsupported operation");
                return;
            }

            List<string> props = new List<string>();
            foreach (UProperty cProp in CurrentLoadedProperties)
            {
                //build a list we are going to the add dialog
                props.Add(cProp.Name);
            }

            Tuple<string, PropertyInfo> prop = AddPropertyDialogWPF.GetProperty(CurrentLoadedExport, props, CurrentLoadedExport.FileRef.Game);

            if (prop != null)
            {
                /*string origname = CurrentLoadedExport.ClassName;
                string temp = CurrentLoadedExport.ClassName;
                List<string> classes = new List<string>();
                Dictionary<string, ClassInfo> classList;
                switch (CurrentLoadedExport.FileRef.Game)
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
                ClassInfo currentInfo = null;
                if (!classList.ContainsKey(temp))
                {
                    IExportEntry exportTemp = CurrentLoadedExport.FileRef.Exports[CurrentLoadedExport.idxClass - 1];
                    //current object is not in classes db, temporarily add it to the list
                    switch (CurrentLoadedExport.FileRef.Game)
                    {
                        case MEGame.ME1:
                            currentInfo = ME1Explorer.Unreal.ME1UnrealObjectInfo.generateClassInfo(exportTemp);
                            break;
                        case MEGame.ME2:
                            currentInfo = ME2Explorer.Unreal.ME2UnrealObjectInfo.generateClassInfo(exportTemp);
                            break;
                        case MEGame.ME3:
                        default:
                            currentInfo = ME3UnrealObjectInfo.generateClassInfo(exportTemp);
                            break;
                    }
                    currentInfo.baseClass = exportTemp.ClassParent;
                }*/

                UProperty newProperty = null;
                //Todo: Maybe lookup the default value?
                switch (prop.Item2.type)
                {
                    case PropertyType.IntProperty:
                        newProperty = new IntProperty(0, prop.Item1);
                        break;
                    case PropertyType.BoolProperty:
                        newProperty = new BoolProperty(false, prop.Item1);
                        break;
                    case PropertyType.FloatProperty:
                        newProperty = new FloatProperty(0.0f, prop.Item1);
                        break;
                    case PropertyType.StringRefProperty:
                        newProperty = new StringRefProperty(prop.Item1);
                        break;
                    case PropertyType.StrProperty:
                        newProperty = new StrProperty("", prop.Item1);
                        break;
                    case PropertyType.ArrayProperty:
                        newProperty = new ArrayProperty<IntProperty>(ArrayType.Int, prop.Item1); //We can just set it to int as it will be reparsed and resolved.
                        break;
                }

                //UProperty property = generateNewProperty(prop.Item1, currentInfo);
                if (newProperty != null)
                {
                    CurrentLoadedProperties.Add(newProperty);
                }
                //Todo: Create new node, prevent refresh of this instance.
                CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);
                //End Todo
            }
        }

        private UProperty generateNewProperty(string prop, ClassInfo nonVanillaClassInfo)
        {
            if (prop != null)
            {
                PropertyInfo info = GetPropertyInfo(prop, className, nonVanillaClassInfo: nonVanillaClassInfo);
                if (info == null)
                {
                    MessageBox.Show("Error reading property.", "Error");
                    return null;
                }
                //TODO: Implement code to generate a new StructProperty UProperty
                if (info.type == PropertyType.StructProperty /* &&CurrentLoadedExport.FileRef.Game != MEGame.ME3*/)
                {
                    MessageBox.Show("Cannot add StructProperties when editing ME1 or ME2 files (or ME3 currently).", "Sorry :(");
                    return null;
                }
            }
            return null;
        }

        public override bool CanParse(IExportEntry exportEntry)
        {
            return true;
        }

        private void SetValue_Click(object sender, RoutedEventArgs e)
        {
            //todo: set value
            bool updated = false;
            UPropertyTreeViewEntry tvi = (UPropertyTreeViewEntry)Interpreter_TreeView.SelectedItem;
            if (tvi != null && tvi.Property != null)
            {
                UProperty tag = tvi.Property;
                switch (tag)
                {
                    case IntProperty ip:
                        {
                            //scoped for variable re-use
                            if (int.TryParse(Value_TextBox.Text, out int i) && i != ip.Value)
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
                            if (int.TryParse(Value_TextBox.Text, out int o) && o != op.Value)
                            {
                                op.Value = o;
                                updated = true;
                            }
                        }
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
    }

    [DebuggerDisplay("UPropertyTreeViewEntry | {DisplayName}")]
    public class UPropertyTreeViewEntry : INotifyPropertyChanged
    {
        protected void OnPropertyChanged([CallerMemberName] string propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private Brush _foregroundColor = Brushes.DarkSeaGreen;
        private bool isSelected;
        public bool IsSelected
        {
            get { return this.isSelected; }
            set
            {
                if (value != this.isSelected)
                {
                    isSelected = value;
                    OnPropertyChanged("IsSelected");
                }
            }
        }

        private bool isExpanded;
        public bool IsExpanded
        {
            get { return this.isExpanded; }
            set
            {
                if (value != this.isExpanded)
                {
                    this.isExpanded = value;
                    OnPropertyChanged("IsExpanded");
                }
            }
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
                }
                return false;
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
            List<UPropertyTreeViewEntry> nodes = new List<UPropertyTreeViewEntry>();
            nodes.Add(this);
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
            get
            {
                if (_editableValue != null) return _editableValue;
                return "";
            }
            set
            {
                //Todo: Write property value here
                if (_editableValue != null && _editableValue != value)
                {
                    switch (Property.PropType)
                    {
                        case Unreal.PropertyType.IntProperty:
                            if (int.TryParse(value, out int parsedIntVal))
                            {
                                (Property as IntProperty).Value = parsedIntVal;
                                PropertyUpdated?.Invoke(this, EventArgs.Empty);
                                HasChanges = true;
                            }
                            else
                            {
                                return;
                            }
                            break;
                        case Unreal.PropertyType.FloatProperty:
                            if (float.TryParse(value, out float parsedFloatVal))
                            {
                                (Property as FloatProperty).Value = parsedFloatVal;
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
            get { return _hasChanges; }
            set
            {
                _hasChanges = value;
                OnPropertyChanged();
            }
        }

        private string _displayName;
        public string DisplayName
        {
            get
            {
                if (_displayName != null) return _displayName;
                return "DisplayName for this UPropertyTreeViewItem is null!";
                //string type = UIndex < 0 ? "Imp" : "Exp";
                //return $"({type}) {UIndex} {Entry.ObjectName}({Entry.ClassName})"; */
            }
            set { _displayName = value; }
        }

        public bool IsUpdatable = false; //set to

        private string _parsedValue;
        public string ParsedValue
        {
            get
            {
                if (_parsedValue != null) return _parsedValue;
                return "";
            }
            set
            {
                _parsedValue = value;

            }
        }

        public string PropertyType
        {
            get
            {
                if (Property != null)
                {
                    if (Property.PropType == Unreal.PropertyType.ArrayProperty)
                    {
                        //we don't have reference to current pcc so we cannot look this up at this time.
                        //return $"ArrayProperty({(Property as ArrayProperty).arrayType})";
                        var props = (Property as ArrayPropertyBase).ValuesAsProperties;
                        return $"ArrayProperty | {props.Count()} item{(props.Count() != 1 ? "s" : "")}";

                    }
                    else if (Property.PropType == Unreal.PropertyType.StructProperty)
                    {
                        return $"StructProperty({(Property as StructProperty).StructType})";
                    }
                    else if (Property.PropType == Unreal.PropertyType.ByteProperty && Property is EnumProperty)
                    {
                        return "ByteProperty(Enum)"; //proptype and type don't seem to always match for some reason
                    }
                    else
                    {
                        return Property.PropType.ToString();
                    }
                }
                return "Currently loaded export";
                //string type = UIndex < 0 ? "Imp" : "Exp";
                //return $"({type}) {UIndex} {Entry.ObjectName}({Entry.ClassName})"; */
            }
            //set { _displayName = value; }
        }

        public override string ToString()
        {
            return "UPropertyTreeViewEntry " + DisplayName;
        }
    }

    public class PropertyEditorSetSelector : DataTemplateSelector
    {
        public DataTemplate SingleTextboxEditorSet { get; set; }
        public DataTemplate DualTextboxEditorSet { get; set; }
        public DataTemplate ComboBoxEditorSet { get; set; }

        bool someKindOfCondition = true;
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {

            if (someKindOfCondition)
            {
                return SingleTextboxEditorSet;
            }
            else
            {
                return DualTextboxEditorSet;
            }
        }
    }
}
