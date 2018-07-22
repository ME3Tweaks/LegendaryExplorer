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

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for InterpreterWPF.xaml
    /// </summary>
    public partial class InterpreterWPF : UserControl
    {
        private IMEPackage pcc;
        public IMEPackage Pcc { get { return pcc; } set { pcc = value; defaultStructValues.Clear(); } }
        private Dictionary<string, List<PropertyReader.Property>> defaultStructValues;
        private byte[] memory;
        private int memsize;
        private string className;
        private BioTlkFileSet tlkset;
        private BioTlkFileSet editorTlkSet;
        int readerpos;
        public IExportEntry CurrentLoadedExport;

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
            InitializeComponent();
            defaultStructValues = new Dictionary<string, List<PropertyReader.Property>>();
        }

        public void loadNewExport(IExportEntry export)
        {
            pcc = export.FileRef;
            CurrentLoadedExport = export;
            memory = export.Data;
            memsize = memory.Length;
            //List<byte> bytes = export.Data.ToList();
            //MemoryStream ms = new MemoryStream(); //initializing memorystream directly with byte[] does not allow it to expand.
            //ms.Write(export.Data, 0, export.Data.Length); //write the data into the memorystream.
            Interpreter_Hexbox.ByteProvider = new DynamicByteProvider(export.Data);
            className = export.ClassName;

            if (pcc.Game == MEGame.ME1)
            {
                // attempt to find a TlkFileSet associated with the object, else just pick the first one and hope it's correct
                if (editorTlkSet == null)
                {
                    PropertyReader.Property tlkSetRef = PropertyReader.getPropList(export).FirstOrDefault(x => pcc.getNameEntry(x.Name) == "m_oTlkFileSet");
                    if (tlkSetRef != null)
                    {
                        tlkset = new BioTlkFileSet(pcc as ME1Package, tlkSetRef.Value.IntValue - 1);
                    }
                    else
                    {
                        tlkset = new BioTlkFileSet(pcc as ME1Package);
                    }
                }
                else
                {
                    tlkset = editorTlkSet;
                }
            }
            StartScan();
        }

        private void StartScan(IEnumerable<string> expandedItems = null, string topNodeName = null, string selectedNodeName = null)
        {
            //resetPropEditingControls();
            //Interpreter_TreeView.BeginUpdate();
            Interpreter_TreeView.Items.Clear();
            readerpos = CurrentLoadedExport.GetPropertyStart();

            TreeViewItem topLevelTree = new TreeViewItem()
            {
                Header = "0000 : " + CurrentLoadedExport.ObjectName,
                IsExpanded = true
            };
            topLevelTree.Tag = nodeType.Root;
            topLevelTree.Name = "_" + "0";

            try
            {
                PropertyCollection props = CurrentLoadedExport.GetProperties(includeNoneProperties: true);
                foreach (UProperty prop in props)
                {
                    GenerateTreeForProperty(prop, topLevelTree);
                }
                //GenerateTreeFromProperties(topLevelTree, props);
            }
            catch (Exception ex)
            {
                TreeViewItem errorNode = new TreeViewItem()
                {
                    Header = $"PARSE ERROR {ex.Message}"
                };
                topLevelTree.Items.Add(errorNode);
                //addPropButton.Visible = false;
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
            Interpreter_TreeView.Items.Add(topLevelTree);
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
            memsize = memory.Length;
        }

        private void GenerateTreeForProperty(UProperty prop, TreeViewItem parent)
        {
            string s = prop.Offset.ToString("X4") + ": ";
            s += "\"" + prop.Name + "\" ";
            s += "Type: " + prop.PropType;
            //s += "Size: " + prop.
            var nodeColor = Brushes.Black;

            if (prop.PropType != PropertyType.None)
            {
                switch (prop.PropType)
                {
                    case PropertyType.ObjectProperty:
                        {
                            s += " Value: ";
                            int index = (prop as ObjectProperty).Value;
                            var entry = pcc.getEntry(index);
                            if (entry != null)
                            {
                                s += index + " " + entry.GetFullPath;
                            }
                            else
                            {
                                s += index + " Index out of bounds of " + (index < 0 ? "Import" : "Export") + " list";
                            }
                            nodeColor = Brushes.Blue;
                        }
                        break;
                    case PropertyType.IntProperty:
                        {
                            s += " Value: ";
                            s += (prop as IntProperty).Value;
                            nodeColor = Brushes.Green;
                        }
                        break;
                    case PropertyType.FloatProperty:
                        {
                            s += " Value: ";
                            s += (prop as FloatProperty).Value;
                            nodeColor = Brushes.Red;
                        }
                        break;
                    case PropertyType.BoolProperty:
                        {
                            s += " Value: ";
                            s += (prop as BoolProperty).Value;
                            nodeColor = Brushes.Orange;
                        }
                        break;
                    case PropertyType.ArrayProperty:
                        {
                            s += ", Array Size: " + (prop as ArrayPropertyBase).ValuesAsProperties.Count();
                        }
                        break;
                    case PropertyType.NameProperty:
                        s += " Value: ";
                        s += (prop as NameProperty).NameTableIndex + " " + (prop as NameProperty).Value;
                        break;
                    case PropertyType.StructProperty:
                        s += ", ";
                        s += (prop as StructProperty).StructType;
                        break;
                }
            }
            Debug.WriteLine(s);
            TreeViewItem item = new TreeViewItem()
            {
                Header = s,
                Name = "_" + prop.Offset.ToString(),
                Foreground = nodeColor,
                Tag = prop
            };
            parent.Items.Add(item);
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

        private void GenerateTreeForArrayProperty(UProperty prop, TreeViewItem parent, int index)
        {
            string s = prop.Offset.ToString("X4") + " Item " + index + "";

            switch (prop.PropType)
            {
                case PropertyType.ObjectProperty:
                    int oIndex = (prop as ObjectProperty).Value;
                    s += ": " + ((oIndex > 0) ? pcc.getEntry(oIndex).GetFullPath : "[0] Null");
                    break;
                case PropertyType.StructProperty:

                    break;
                case PropertyType.BoolProperty:
                    s += ": " + (prop as BoolProperty).Value;
                    break;
                case PropertyType.NameProperty:
                    s += ": " + (prop as NameProperty).NameTableIndex + " " + (prop as NameProperty).Value;

                    break;
            }


            TreeViewItem item = new TreeViewItem()
            {
                Header = s,
                Name = "_" + prop.Offset,
                Tag = prop
            };
            parent.Items.Add(item);
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

        /*
        public List<PropHeader> ReadHeadersTillNone()
        {
            List<PropHeader> ret = new List<PropHeader>();
            bool run = true;
            while (run)
            {
                PropHeader p = new PropHeader();
                if (readerpos > memory.Length || readerpos < 0)
                {
                    //nothing else to interpret.
                    run = false;
                    continue;
                }
                p.name = BitConverter.ToInt32(memory, readerpos);

                if (readerpos == 4 && pcc.isName(p.name) && pcc.getNameEntry(p.name) == export.ObjectName)
                {
                    //It's a primitive component header
                    //Debug.WriteLine("Primitive Header " + pcc.Names[p.name]);
                    readerpos += 12;
                    continue;
                }

                if (!pcc.isName(p.name))
                    run = false;
                else
                {
                    string name = pcc.getNameEntry(p.name);
                    if (pcc.getNameEntry(p.name) != "None")
                    {
                        p.type = BitConverter.ToInt32(memory, readerpos + 8);
                        if (p.name == 0 && p.type == 0 && pcc.getNameEntry(0) == "ArrayProperty")
                        {
                            //This could be a struct that just happens to have arrayproperty at name 0... this might fubar some stuff
                            return ret;
                        }
                        if (!pcc.isName(p.type) || getType(pcc.getNameEntry(p.type)) == nodeType.Unknown)
                            run = false;
                        else
                        {
                            p.size = BitConverter.ToInt32(memory, readerpos + 16);
                            p.index = BitConverter.ToInt32(memory, readerpos + 20);
                            p.offset = readerpos;
                            ret.Add(p);
                            readerpos += p.size + 24;

                            if (getType(pcc.getNameEntry(p.type)) == nodeType.StructProperty) //StructName
                                readerpos += 8;
                            if (pcc.Game == MEGame.ME3 || pcc.Game == MEGame.UDK)
                            {
                                if (getType(pcc.getNameEntry(p.type)) == nodeType.BoolProperty)//Boolbyte
                                    readerpos++;
                                if (getType(pcc.getNameEntry(p.type)) == nodeType.ByteProperty)//byteprop
                                    readerpos += 8;
                            }
                            else
                            {
                                if (getType(pcc.getNameEntry(p.type)) == nodeType.BoolProperty)
                                    readerpos += 4;
                            }
                        }
                    }
                    else
                    {
                        p.type = p.name;
                        p.size = 0;
                        p.index = 0;
                        p.offset = readerpos;
                        ret.Add(p);
                        readerpos += 8;
                        run = false;
                    }
                }
            }
            return ret;
        }

        public void GenerateTree(TreeViewItem localRoot, List<PropHeader> headersList)
        {
            foreach (PropHeader header in headersList)
            {
                if (readerpos > memory.Length)
                {
                    throw new IndexOutOfRangeException(": tried to read past bounds of Export Data");
                }
                nodeType type = getType(pcc.getNameEntry(header.type));
                //Debug.WriteLine("Generating tree item for " + pcc.getNameEntry(header.name) + " at 0x" + header.offset.ToString("X6"));

                if (type != nodeType.ArrayProperty && type != nodeType.StructProperty)
                {

                    localRoot.Items.Add(GenerateNode(header));
                }
                else
                {
                    if (type == nodeType.ArrayProperty)
                    {
                        TreeViewItem t = GenerateNode(header);
                        int arrayLength = BitConverter.ToInt32(memory, header.offset + 24);
                        readerpos = header.offset + 28;
                        int tmp = readerpos;
                        ArrayType arrayType;
                        try
                        {
                            arrayType = GetArrayType(header.name);
                        }
                        catch (Exception)
                        {
                            arrayType = ArrayType.Int;
                        }
                        if (arrayType == ArrayType.Struct)
                        {
                            PropertyInfo info = GetPropertyInfo(header.name);
                            t.Header = t.Header.ToString().Insert(t.Header.ToString().IndexOf("Size: ") - 2, $"({info.reference})");
                            for (int i = 0; i < arrayLength; i++)
                            {
                                readerpos = tmp;
                                int pos = tmp;
                                List<PropHeader> arrayListPropHeaders = ReadHeadersTillNone();
                                tmp = readerpos;
                                TreeViewItem n = new TreeViewItem() { Header = i.ToString() };
                                n.Tag = nodeType.ArrayLeafStruct;
                                n.Name = "_" + (-pos).ToString();
                                t.Items.Add(n);
                                n = (TreeViewItem)t.Items[t.Items.Count - 1]; //final node
                                if (info != null && (ME3UnrealObjectInfo.isImmutable(info.reference) || arrayListPropHeaders.Count == 0))
                                {
                                    readerpos = pos;
                                    GenerateSpecialStruct(n, info.reference, header.size / arrayLength);
                                    tmp = readerpos;
                                }
                                else if (arrayListPropHeaders.Count > 0)
                                {
                                    GenerateTree(n, arrayListPropHeaders);
                                }
                                else
                                {
                                    throw new Exception($"at position {readerpos.ToString("X4")}. Could not read element {i} of ArrayProperty {pcc.getNameEntry(header.name)}");
                                }
                                t.Items.Remove(t.Items[t.Items.Count - 1]);
                                t.Items.Add(n);
                            }
                            localRoot.Items.Add(t);
                        }
                        else
                        {
                            t.Header = t.Header.ToString().Insert(t.Header.ToString().IndexOf("Size: ") - 2, $"({arrayType.ToString()})");
                            int count = 0;
                            int pos;
                            if (header.size > 1000 && arrayType == ArrayType.Byte)
                            {
                                TreeViewItem node = new TreeViewItem();
                                node.Name = "_" + (header.offset + 28).ToString();
                                node.Tag = nodeType.Unknown;
                                node.Header = "Large binary data array. Skipping Parsing";
                                t.Items.Add(node);
                                localRoot.Items.Add(t);
                                continue;
                            }
                            for (int i = 0; i < (header.size - 4); count++)
                            {
                                pos = header.offset + 28 + i;
                                if (pos > memory.Length)
                                {
                                    throw new Exception(": tried to read past bounds of Export Data");
                                }
                                int val = BitConverter.ToInt32(memory, pos);
                                string s = pos.ToString("X4") + "|" + count + ": ";
                                TreeViewItem node = new TreeViewItem();
                                node.Name = "_" + pos.ToString();
                                if (arrayType == ArrayType.Object)
                                {
                                    node.Tag = nodeType.ArrayLeafObject;
                                    int value = val;
                                    if (value == 0)
                                    {
                                        //invalid
                                        s += "Null [" + value + "] ";
                                    }
                                    else
                                    {

                                        bool isImport = value < 0;
                                        if (isImport)
                                        {
                                            value = -value;
                                        }
                                        value--; //0-indexed
                                        if (isImport)
                                        {
                                            if (pcc.ImportCount > value)
                                            {
                                                if (pcc.getNameEntry(header.name) == "m_AutoPersistentObjects")
                                                {
                                                    s += pcc.getImport(value).PackageFullName + ".";
                                                }

                                                s += pcc.getImport(value).ObjectName + " [IMPORT " + value + "]";
                                            }
                                            else
                                            {
                                                s += "Index not in import list [" + value + "]";
                                            }
                                        }
                                        else
                                        {
                                            if (pcc.ExportCount > value)
                                            {
                                                if (pcc.getNameEntry(header.name) == "m_AutoPersistentObjects")
                                                {
                                                    s += pcc.getExport(value).PackageFullName + ".";
                                                }
                                                if (pcc.getNameEntry(header.name) == "StreamingLevels")
                                                {
                                                    IExportEntry streamingLevel = pcc.getExport(value);
                                                    NameProperty prop = streamingLevel.GetProperty<NameProperty>("PackageName");

                                                    s += prop.Value.Name + "_" + prop.Value.Number + " in ";
                                                }
                                                s += pcc.getExport(value).ObjectName + " [EXPORT " + value + "]";
                                            }
                                            else
                                            {
                                                s += "Index not in export list [" + value + "]";
                                            }
                                        }
                                    }
                                    i += 4;
                                }
                                else if (arrayType == ArrayType.Name || arrayType == ArrayType.Enum)
                                {

                                    node.Tag = arrayType == ArrayType.Name ? nodeType.ArrayLeafName : nodeType.ArrayLeafEnum;
                                    int value = val;
                                    if (value < 0)
                                    {
                                        s += "Invalid Name Index [" + value + "]";
                                    }
                                    else
                                    {
                                        if (pcc.Names.Count > value)
                                        {
                                            s += $"\"{pcc.Names[value]}\"_{BitConverter.ToInt32(memory, pos + 4)}[NAMEINDEX {value}]";
                                        }
                                        else
                                        {
                                            s += "Index not in name list [" + value + "]";
                                        }
                                    }
                                    i += 8;
                                }
                                else if (arrayType == ArrayType.Float)
                                {
                                    node.Tag = nodeType.ArrayLeafFloat;
                                    s += BitConverter.ToSingle(memory, pos).ToString("0.0######");
                                    i += 4;
                                }
                                else if (arrayType == ArrayType.Byte)
                                {
                                    node.Tag = nodeType.ArrayLeafByte;
                                    s += "(byte)" + memory[pos];
                                    i += 1;
                                }
                                else if (arrayType == ArrayType.Bool)
                                {
                                    node.Tag = nodeType.ArrayLeafBool;
                                    s += BitConverter.ToBoolean(memory, pos);
                                    i += 1;
                                }
                                else if (arrayType == ArrayType.String)
                                {
                                    node.Tag = nodeType.ArrayLeafString;
                                    int sPos = pos + 4;
                                    s += "\"";
                                    if (val < 0)
                                    {
                                        int len = -val;
                                        for (int j = 1; j < len; j++)
                                        {
                                            s += BitConverter.ToChar(memory, sPos);
                                            sPos += 2;
                                        }
                                        i += (len * 2) + 4;
                                    }
                                    else
                                    {
                                        for (int j = 1; j < val; j++)
                                        {
                                            s += (char)memory[sPos];
                                            sPos++;
                                        }
                                        i += val + 4;
                                    }
                                    s += "\"";
                                }
                                else
                                {
                                    node.Tag = nodeType.ArrayLeafInt;
                                    s += val.ToString();
                                    i += 4;
                                }
                                node.Header = s;
                                t.Items.Add(node);
                            }
                            localRoot.Items.Add(t);
                        }
                    }
                    if (type == nodeType.StructProperty)
                    {
                        if (pcc.getNameEntry(header.name) == "ArriveTangent")
                        {
                            Debug.WriteLine("test");
                        }
                        TreeViewItem t = GenerateNode(header);
                        readerpos = header.offset + 32;
                        List<PropHeader> ll = ReadHeadersTillNone();
                        if (ll.Count != 0)
                        {
                            GenerateTree(t, ll);
                        }
                        else
                        {
                            string structType = pcc.getNameEntry(BitConverter.ToInt32(memory, header.offset + 24));
                            GenerateSpecialStruct(t, structType, header.size);
                        }
                        localRoot.Items.Add(t);
                    }

                }
            }
        }
        //structs that are serialized down to just their values.
        private void GenerateSpecialStruct(TreeViewItem t, string structType, int size)
        {
            TreeViewItem node;
            //have to handle this specially to get the degrees conversion
            if (structType == "Rotator")
            {
                string[] labels = { "Pitch", "Yaw", "Roll" };
                int val;
                for (int i = 0; i < 3; i++)
                {
                    val = BitConverter.ToInt32(memory, readerpos);
                    node = new TreeViewItem() { Header = readerpos.ToString("X4") + ": " + labels[i] + " : " + val + " (" + (val * 360f / 65536f).ToString("0.0######") + " degrees)" };
                    node.Name = "_" + readerpos.ToString();
                    node.Tag = nodeType.StructLeafDeg;
                    t.Items.Add(node);
                    readerpos += 4;
                }
            }
            else if (pcc.Game == MEGame.ME3)
            {
                if (ME3UnrealObjectInfo.Structs.ContainsKey(structType))
                {
                    List<PropertyReader.Property> props;
                    //memoize
                    if (defaultStructValues.ContainsKey(structType))
                    {
                        props = defaultStructValues[structType];
                    }
                    else
                    {
                        byte[] defaultValue = ME3UnrealObjectInfo.getDefaultClassValue(pcc as ME3Package, structType, true);
                        if (defaultValue == null)
                        {
                            //just prints the raw hex since there's no telling what it actually is
                            node = new TreeViewItem() { Header = readerpos.ToString("X4") + ": " + memory.Skip(readerpos).Take(size).Aggregate("", (b, s) => b + " " + s.ToString("X2")) };
                            node.Tag = nodeType.Unknown;
                            t.Items.Add(node);
                            readerpos += size;
                            return;
                        }
                        props = PropertyReader.ReadProp(pcc, defaultValue, 0);
                        defaultStructValues.Add(structType, props);
                    }
                    for (int i = 0; i < props.Count; i++)
                    {
                        string s = readerpos.ToString("X4") + ": " + pcc.getNameEntry(props[i].Name) + " : ";
                        readerpos = GenerateSpecialStructProp(t, s, readerpos, props[i]);
                    }
                }
            }
            else
            {
                //TODO: implement getDefaultClassValue() for ME1 and ME2 so this isn't needed
                int pos = readerpos;
                if (structType == "Vector2d" || structType == "RwVector2")
                {
                    string[] labels = { "X", "Y" };
                    for (int i = 0; i < 2; i++)
                    {
                        node = new TreeViewItem() { Header = pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######") };
                        node.Name = "_" + pos.ToString();
                        node.Tag = nodeType.StructLeafFloat;
                        t.Items.Add(node);
                        pos += 4;
                    }
                }
                else if (structType == "Vector" || structType == "RwVector3")
                {
                    string[] labels = { "X", "Y", "Z" };
                    for (int i = 0; i < 3; i++)
                    {
                        node = new TreeViewItem() { Header = pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######") };
                        node.Name = "_" + pos.ToString();
                        node.Tag = nodeType.StructLeafFloat;
                        t.Items.Add(node);
                        pos += 4;
                    }
                }
                else if (structType == "Color")
                {
                    string[] labels = { "B", "G", "R", "A" };
                    for (int i = 0; i < 4; i++)
                    {
                        node = new TreeViewItem()
                        {
                            Header = pos.ToString("X4") + ": " + labels[i] + " : " + memory[pos]
                        };
                        node.Name = "_" + pos.ToString();
                        node.Tag = nodeType.StructLeafByte;
                        t.Items.Add(node);
                        pos += 1;
                    }
                }
                else if (structType == "LinearColor")
                {
                    string[] labels = { "R", "G", "B", "A" };
                    for (int i = 0; i < 4; i++)
                    {
                        node = new TreeViewItem()
                        {
                            Header = pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######")
                        };
                        node.Name = "_" + pos.ToString();
                        node.Tag = nodeType.StructLeafFloat;
                        t.Items.Add(node);
                        pos += 4;
                    }
                }
                //uses EndsWith to support RwQuat, RwVector4, and RwPlane
                else if (structType.EndsWith("Quat") || structType.EndsWith("Vector4") || structType.EndsWith("Plane"))
                {
                    string[] labels = { "X", "Y", "Z", "W" };
                    for (int i = 0; i < 4; i++)
                    {
                        node = new TreeViewItem()
                        {
                            Header = pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######")
                        };
                        node.Name = "_" + pos.ToString();
                        node.Tag = nodeType.StructLeafFloat;
                        t.Items.Add(node);
                        pos += 4;
                    }
                }
                else if (structType == "TwoVectors")
                {
                    string[] labels = { "X", "Y", "Z", "X", "Y", "Z" };
                    for (int i = 0; i < 6; i++)
                    {
                        node = new TreeViewItem() { Header = pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######") };

                        node.Name = "_" + pos.ToString();
                        node.Tag = nodeType.StructLeafFloat;
                        t.Items.Add(node);
                        pos += 4;
                    }
                }
                else if (structType == "Matrix" || structType == "RwMatrix44")
                {
                    string[] labels = { "X Plane", "Y Plane", "Z Plane", "W Plane" };
                    string[] labels2 = { "X", "Y", "Z", "W" };
                    TreeViewItem node2;
                    for (int i = 0; i < 3; i++)
                    {
                        node2 = new TreeViewItem()
                        {
                            Header = labels[i]
                        };
                        node2.Name = "_" + pos.ToString();
                        for (int j = 0; j < 4; j++)
                        {
                            node = new TreeViewItem() { Header = pos.ToString("X4") + ": " + labels2[j] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######") };
                            node.Name = "_" + pos.ToString();
                            node.Tag = nodeType.StructLeafFloat;
                            node2.Items.Add(node);
                            pos += 4;
                        }
                        t.Items.Add(node2);
                    }
                }
                else if (structType == "Guid")
                {
                    string[] labels = { "A", "B", "C", "D" };
                    for (int i = 0; i < 4; i++)
                    {
                        node = new TreeViewItem()
                        {
                            Header = pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToInt32(memory, pos)
                        };
                        node.Name = "_" + pos.ToString();
                        node.Tag = nodeType.StructLeafInt;
                        t.Items.Add(node);
                        pos += 4;
                    }
                }
                else if (structType == "IntPoint")
                {
                    string[] labels = { "X", "Y" };
                    for (int i = 0; i < 2; i++)
                    {
                        node = new TreeViewItem()
                        {
                            Header = pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToInt32(memory, pos)
                        };
                        node.Name = "_" + pos.ToString();
                        node.Tag = nodeType.StructLeafInt;
                        t.Items.Add(node);
                        pos += 4;
                    }
                }
                else if (structType == "Box" || structType == "BioRwBox")
                {
                    string[] labels = { "Min", "Max" };
                    string[] labels2 = { "X", "Y", "Z" };
                    TreeViewItem node2;
                    for (int i = 0; i < 2; i++)
                    {
                        node2 = new TreeViewItem() { Header = labels[i] };
                        node2.Name = "_" + pos.ToString();
                        for (int j = 0; j < 3; j++)
                        {
                            node = new TreeViewItem() { Header = pos.ToString("X4") + ": " + labels2[j] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######") };
                            node.Name = "_" + pos.ToString();
                            node.Tag = nodeType.StructLeafFloat;
                            node2.Items.Add(node);
                            pos += 4;
                        }
                        t.Items.Add(node2);
                    }
                    node = new TreeViewItem() { Header = pos.ToString("X4") + ": IsValid : " + memory[pos] };
                    node.Name = "_" + pos.ToString();
                    node.Tag = nodeType.StructLeafByte;
                    t.Items.Add(node);
                    pos += 1;
                }
                else
                {
                    //just prints the raw hex since there's no telling what it actually is
                    node = new TreeViewItem() { Header = pos.ToString("X4") + ": " + memory.Skip(pos).Take(size).Aggregate("", (b, s) => b + " " + s.ToString("X2")) };
                    node.Tag = nodeType.Unknown;
                    t.Items.Add(node);
                    pos += size;
                }
                readerpos = pos;
            }
        }

        private int GenerateSpecialStructProp(TreeViewItem t, string s, int pos, PropertyReader.Property prop)
        {
            if (pos > memory.Length)
            {
                throw new Exception(": tried to read past bounds of Export Data");
            }
            int n;
            TreeViewItem node;
            PropertyInfo propInfo;
            switch (prop.TypeVal)
            {
                case PropertyType.FloatProperty:
                    s += BitConverter.ToSingle(memory, pos).ToString("0.0######");
                    node = new TreeViewItem() { Header = s };
                    node.Name = "_" + pos.ToString();
                    node.Tag = nodeType.StructLeafFloat;
                    t.Items.Add(node);
                    pos += 4;
                    break;
                case PropertyType.IntProperty:
                    s += BitConverter.ToInt32(memory, pos).ToString();
                    node = new TreeViewItem() { Header = s };
                    node.Name = "_" + pos.ToString();
                    node.Tag = nodeType.StructLeafInt;
                    t.Items.Add(node);
                    pos += 4;
                    break;
                case PropertyType.ObjectProperty:
                    n = BitConverter.ToInt32(memory, pos);
                    s += n + " (" + pcc.getObjectName(n) + ")";
                    node = new TreeViewItem() { Header = s };
                    node.Name = "_" + pos.ToString();
                    node.Tag = nodeType.StructLeafObject;
                    t.Items.Add(node);
                    pos += 4;
                    break;
                case PropertyType.StringRefProperty:
                    n = BitConverter.ToInt32(memory, pos);
                    s += "#" + n + ": ";
                    s += ME3TalkFiles.tlkList.Count == 0 ? "(.tlk not loaded)" : ME3TalkFiles.findDataById(n);
                    node = new TreeViewItem() { Header = s };
                    node.Name = "_" + pos.ToString();
                    node.Tag = nodeType.StructLeafInt;
                    t.Items.Add(node);
                    pos += 4;
                    break;
                case PropertyType.NameProperty:
                    n = BitConverter.ToInt32(memory, pos);
                    pos += 4;
                    s += "\"" + pcc.getNameEntry(n) + "\"_" + BitConverter.ToInt32(memory, pos);
                    node = new TreeViewItem() { Header = s };
                    node.Name = "_" + pos.ToString();
                    node.Tag = nodeType.StructLeafName;
                    t.Items.Add(node);
                    pos += 4;
                    break;
                case PropertyType.BoolProperty:
                    s += (memory[pos] > 0).ToString();
                    node = new TreeViewItem() { Header = s };
                    node.Name = "_" + pos.ToString();
                    node.Tag = nodeType.StructLeafBool;
                    t.Items.Add(node);
                    pos += 1;
                    break;
                case PropertyType.ByteProperty:
                    if (prop.Size != 1)
                    {
                        string enumName = GetPropertyInfo(prop.Name)?.reference;
                        if (enumName != null)
                        {
                            s += "\"" + enumName + "\", ";
                        }
                        s += "\"" + pcc.getNameEntry(BitConverter.ToInt32(memory, pos)) + "\"";
                        node = new TreeViewItem() { Header = s };
                        node.Name = "_" + pos.ToString();
                        node.Tag = nodeType.StructLeafEnum;
                        t.Items.Add(node);
                        pos += 8;
                    }
                    else
                    {
                        s += "(byte)" + memory[pos];
                        node = new TreeViewItem() { Header = s };
                        node.Name = "_" + pos.ToString();
                        node.Tag = nodeType.StructLeafByte;
                        t.Items.Add(node);
                        pos += 1;
                    }
                    break;
                case PropertyType.StrProperty:
                    n = BitConverter.ToInt32(memory, pos);
                    pos += 4;
                    s += "\"";
                    for (int i = 0; i < n - 1; i++)
                        s += (char)memory[pos + i * 2];
                    s += "\"";
                    node = new TreeViewItem() { Header = s };
                    node.Name = "_" + pos.ToString();
                    node.Tag = nodeType.StructLeafStr;
                    t.Items.Add(node);
                    pos += n * 2;
                    break;
                case PropertyType.ArrayProperty:
                    n = BitConverter.ToInt32(memory, pos);
                    s += n + " elements";
                    node = new TreeViewItem() { Header = s };
                    node.Name = "_" + pos.ToString();
                    node.Tag = nodeType.StructLeafArray;
                    pos += 4;
                    propInfo = GetPropertyInfo(prop.Name);
                    ArrayType arrayType = GetArrayType(propInfo);
                    TreeViewItem node2;
                    string s2;
                    for (int i = 0; i < n; i++)
                    {
                        if (arrayType == ArrayType.Struct)
                        {
                            readerpos = pos;
                            node2 = new TreeViewItem()
                            {
                                Header = i + ": (" + propInfo.reference + ")"
                            };
                            node2.Name = "_" + (-pos).ToString();
                            node2.Tag = nodeType.StructLeafStruct;
                            GenerateSpecialStruct(node2, propInfo.reference, 0);
                            node.Items.Add(node2);
                            pos = readerpos;
                        }
                        else
                        {
                            s2 = "";
                            PropertyType type = PropertyType.None;
                            int size = 0;
                            switch (arrayType)
                            {
                                case ArrayType.Object:
                                    type = PropertyType.ObjectProperty;
                                    break;
                                case ArrayType.Name:
                                    type = PropertyType.NameProperty;
                                    break;
                                case ArrayType.Byte:
                                    type = PropertyType.ByteProperty;
                                    size = 1;
                                    break;
                                case ArrayType.Enum:
                                    type = PropertyType.ByteProperty;
                                    break;
                                case ArrayType.Bool:
                                    type = PropertyType.BoolProperty;
                                    break;
                                case ArrayType.String:
                                    type = PropertyType.StrProperty;
                                    break;
                                case ArrayType.Float:
                                    type = PropertyType.FloatProperty;
                                    break;
                                case ArrayType.Int:
                                    type = PropertyType.IntProperty;
                                    break;
                            }
                            pos = GenerateSpecialStructProp(node, s2, pos, new PropertyReader.Property { TypeVal = type, Size = size });
                        }
                    }
                    t.Items.Add(node);
                    break;
                case PropertyType.StructProperty:
                    propInfo = GetPropertyInfo(prop.Name);
                    s += propInfo.reference;
                    node = new TreeViewItem()
                    {
                        Header = s
                    };
                    node.Name = "_" + (-pos).ToString();
                    node.Tag = nodeType.StructLeafStruct;
                    readerpos = pos;
                    GenerateSpecialStruct(node, propInfo.reference, 0);
                    pos = readerpos;
                    t.Items.Add(node);
                    break;
                case PropertyType.DelegateProperty:
                    throw new NotImplementedException($"at position {pos.ToString("X4")}: cannot read Delegate property of Immutable struct");
                case PropertyType.Unknown:
                    throw new NotImplementedException($"at position {pos.ToString("X4")}: cannot read Unknown property of Immutable struct");
                case PropertyType.None:
                    node = new TreeViewItem() { Header = s };
                    node.Name = "_" + pos.ToString();
                    node.Tag = nodeType.StructLeafObject;
                    t.Items.Add(node);
                    pos += 8;
                    break;
                default:
                    break;
            }

            return pos;
        }

        public TreeViewItem GenerateNode(PropHeader p)
        {
            string s = p.offset.ToString("X4") + ": ";
            s += "Name: \"" + pcc.getNameEntry(p.name) + "\" ";
            s += "Type: \"" + pcc.getNameEntry(p.type) + "\" ";
            s += "Size: " + p.size + " Value: ";
            nodeType propertyType = getType(pcc.getNameEntry(p.type));
            int idx;
            byte val;
            switch (propertyType)
            {
                case nodeType.IntProperty:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    if (pcc.getNameEntry(p.name) == "m_nStrRefID")
                    {
                        s += "#" + idx + ": ";
                        if (pcc.Game == MEGame.ME3)
                        {
                            s += ME3TalkFiles.tlkList.Count == 0 ? "(.tlk not loaded)" : ME3TalkFiles.findDataById(idx);
                        }
                        else if (pcc.Game == MEGame.ME2)
                        {
                            s += ME2Explorer.ME2TalkFiles.tlkList.Count == 0 ? "(.tlk not loaded)" : ME2Explorer.ME2TalkFiles.findDataById(idx);
                        }
                        else if (pcc.Game == MEGame.ME1)
                        {
                            s += tlkset == null ? "(.tlk not loaded)" : tlkset.findDataById(idx);
                        }
                    }
                    else
                    {
                        s += idx.ToString();
                    }
                    break;
                case nodeType.ObjectProperty:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += idx + " (" + pcc.getObjectName(idx) + ")";
                    break;
                case nodeType.StrProperty:
                    int count = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "\"";
                    if (count < 0)
                    {
                        for (int i = 0; i < count * -1 - 1; i++)
                            s += (char)memory[p.offset + 28 + i * 2];
                    }
                    else
                    {
                        for (int i = 0; i < count - 1; i++)
                            s += (char)memory[p.offset + 28 + i];
                    }
                    s += "\"";
                    break;
                case nodeType.BoolProperty:
                    val = memory[p.offset + 24];
                    s += (val == 1).ToString();
                    break;
                case nodeType.FloatProperty:
                    float f = BitConverter.ToSingle(memory, p.offset + 24);
                    s += f.ToString("0.0######");
                    break;
                case nodeType.NameProperty:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "\"" + pcc.getNameEntry(idx) + "\"_" + BitConverter.ToInt32(memory, p.offset + 28);
                    break;
                case nodeType.StructProperty:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "\"" + pcc.getNameEntry(idx) + "\"";
                    break;
                case nodeType.ByteProperty:
                    if (pcc.Game == MEGame.ME3 || pcc.Game == MEGame.UDK)
                    {
                        if (p.size == 1)
                        {
                            val = memory[p.offset + 32];
                            s += val.ToString();
                        }
                        else
                        {
                            idx = BitConverter.ToInt32(memory, p.offset + 24);
                            int idx2 = BitConverter.ToInt32(memory, p.offset + 32);
                            s += "\"" + pcc.getNameEntry(idx) + "\",\"" + pcc.getNameEntry(idx2) + "\"";
                        }
                    }
                    else
                    {
                        if (p.size == 1)
                        {
                            val = memory[p.offset + 24];
                            s += val.ToString();
                        }
                        else
                        {
                            idx = BitConverter.ToInt32(memory, p.offset + 24);
                            s += "\"" + pcc.getNameEntry(idx) + "\"";
                        }
                    }
                    break;
                case nodeType.ArrayProperty:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += idx + "(count)";
                    break;
                case nodeType.StringRefProperty:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "#" + idx + ": ";
                    if (pcc.Game == MEGame.ME3)
                    {
                        s += ME3TalkFiles.tlkList.Count == 0 ? "(.tlk not loaded)" : ME3TalkFiles.findDataById(idx);
                    }
                    else if (pcc.Game == MEGame.ME2)
                    {
                        s += ME2Explorer.ME2TalkFiles.tlkList.Count == 0 ? "(.tlk not loaded)" : ME2Explorer.ME2TalkFiles.findDataById(idx);
                    }
                    else if (pcc.Game == MEGame.ME1)
                    {
                        s += tlkset == null ? "(.tlk not loaded)" : tlkset.findDataById(idx);
                    }
                    break;
            }
            TreeViewItem ret = new TreeViewItem()
            {
                Header = s
            };
            ret.Tag = propertyType;
            ret.Name = "_" + p.offset.ToString();
            return ret;
        }

        public nodeType getType(string s)
        {
            for (int i = 0; i < Types.Length; i++)
                if (s == Types[i])
                {
                    return (nodeType)i;
                }
            return (nodeType)(-1);
        }*/

        #region UnrealObjectInfo
        private PropertyInfo GetPropertyInfo(int propName)
        {
            switch (pcc.Game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getPropertyInfo(className, pcc.getNameEntry(propName));
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getPropertyInfo(className, pcc.getNameEntry(propName));
                case MEGame.ME3:
                case MEGame.UDK:
                    return ME3UnrealObjectInfo.getPropertyInfo(className, pcc.getNameEntry(propName));
            }
            return null;
        }

        private PropertyInfo GetPropertyInfo(string propname, string typeName, bool inStruct = false, ClassInfo nonVanillaClassInfo = null)
        {
            switch (pcc.Game)
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

        private ArrayType GetArrayType(PropertyInfo propInfo)
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

        private ArrayType GetArrayType(int propName, string typeName = null)
        {
            if (typeName == null)
            {
                typeName = className;
            }
            switch (pcc.Game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getArrayType(typeName, pcc.getNameEntry(propName), export: CurrentLoadedExport);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getArrayType(typeName, pcc.getNameEntry(propName), export: CurrentLoadedExport);
                case MEGame.ME3:
                case MEGame.UDK:
                    return ME3UnrealObjectInfo.getArrayType(typeName, pcc.getNameEntry(propName), export: CurrentLoadedExport);
            }
            return ArrayType.Int;
        }

        private List<string> GetEnumValues(string enumName, int propName)
        {
            switch (pcc.Game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getEnumfromProp(className, pcc.getNameEntry(propName));
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getEnumfromProp(className, pcc.getNameEntry(propName));
                case MEGame.ME3:
                case MEGame.UDK:
                    return ME3UnrealObjectInfo.getEnumValues(enumName, true);
            }
            return null;
        }
        #endregion

        private void Interpreter_ToggleHexboxWidth_Click(object sender, System.Windows.RoutedEventArgs e)
        {

        }

        private void Interpreter_TreeViewSelectedItemChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background,
(NoArgDelegate)delegate { UpdateHexboxPosition(e.NewValue); });
        }

        private delegate void NoArgDelegate();
        /// <summary>
        /// Tree view selected item changed. This runs in a delegate due to how multithread bubble-up items work with treeview.
        /// Without this delegate, the item selected will randomly be a parent item instead.
        /// From https://www.codeproject.com/Tips/208896/WPF-TreeView-SelectedItemChanged-called-twice
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UpdateHexboxPosition(object tvi)
        {
            TreeViewItem newSelectedItem = (TreeViewItem)tvi;
            if (newSelectedItem != null)
            {
                var hexPosStr = newSelectedItem.Name.Substring(1); //remove _
                int hexPos = Convert.ToInt32(hexPosStr);
                Interpreter_Hexbox.SelectionStart = hexPos;
                Interpreter_Hexbox.SelectionLength = 1;
                //Interpreter_HexBox.SetPosition(hexPos);
                //                Debug.WriteLine(newSelectedItem.Name);
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
                    } else
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
    }
}
