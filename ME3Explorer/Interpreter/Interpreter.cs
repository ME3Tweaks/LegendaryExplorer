using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Be.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using ME3Explorer.Packages;
using KFreonLib.MEDirectories;
using System.Diagnostics;
using ME1Explorer.Unreal;
using ME2Explorer.Unreal;
using ME1Explorer.Unreal.Classes;

namespace ME3Explorer
{
    public partial class Interpreter : UserControl
    {
        public IMEPackage Pcc { get { return pcc; } set { pcc = value; defaultStructValues.Clear(); } }
        public IExportEntry export;
        public string className;
        public byte[] memory;
        public int memsize;
        public int readerpos;

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

        internal void hideHexBox()
        {
            hb1.Visible = false;
            splitContainer1.Panel1Collapsed = true;
            splitContainer1.Panel1.Hide();
        }

        private BioTlkFileSet tlkset;
        private int lastSetOffset = -1; //offset set by program, used for checking if user changed since set 
        private nodeType LAST_SELECTED_PROP_TYPE = nodeType.Unknown; //last property type user selected. Will use to check the current offset for type
        private TreeNode LAST_SELECTED_NODE = null; //last selected tree node
        private const int HEXBOX_MAX_WIDTH = 650;

        private IMEPackage pcc;
        private Dictionary<string, List<PropertyReader.Property>> defaultStructValues;

        int? selectedNodePos = null;

        public Interpreter()
        {
            InitializeComponent();
            SetTopLevel(false);
            defaultStructValues = new Dictionary<string, List<PropertyReader.Property>>();
        }

        /// <summary>
        /// Used for relinking object arrays when dragging trees between files in PackageEditor.
        /// </summary>
        /// <param name="export">Export to scan.</param>
        public Interpreter(IMEPackage importingPCC, IExportEntry importingExport, IMEPackage destPCC, IExportEntry destExport, SortedDictionary<int, int> crossPCCReferences)
        {
            //This will make it fairly slow, but will make it so I don't have to change everything.
            InitializeComponent();
            SetTopLevel(false);
            defaultStructValues = new Dictionary<string, List<PropertyReader.Property>>();
            this.pcc = importingPCC;
            this.export = importingExport;
            memory = export.Data;
            memsize = memory.Length;
            className = export.ClassName;
            StartScan();
            RelinkObjectProperties(crossPCCReferences, treeView1.SelectedNode, destExport);
        }

        private void RelinkObjectProperties(SortedDictionary<int, int> crossPCCReferences, TreeNode rootNode, IExportEntry destinationExport)
        {
            if (rootNode != null)
            {
                if (rootNode.Nodes.Count > 0)
                {
                    //container.
                    foreach (TreeNode node in rootNode.Nodes)
                    {
                        RelinkObjectProperties(crossPCCReferences, node, destinationExport);
                    }
                }
                else
                {
                    //leaf
                    if (rootNode.Tag != null)
                    {
                        if ((nodeType)rootNode.Tag == nodeType.ObjectProperty || (nodeType)rootNode.Tag == nodeType.StructLeafObject || (nodeType)rootNode.Tag == nodeType.ArrayLeafObject)
                        {

                            int valueoffset = 0;
                            if ((nodeType)rootNode.Tag == nodeType.ObjectProperty)
                            {
                                valueoffset = 24;
                            }

                            int off = getPosFromNode(rootNode) + valueoffset;
                            int n = BitConverter.ToInt32(memory, off);
                            if (n > 0)
                            {
                                n--;
                            }
                            //if (n < -1)
                            //{
                            //    n++;
                            //}
                            //Debug.WriteLine(rootNode.Tag + " " + n + " " + rootNode.Text);
                            if (n != 0)
                            {
                                int key;
                                if (crossPCCReferences.TryGetValue(n, out key))
                                {
                                    byte[] data = destinationExport.Data;
                                    //we can remap this
                                    if (key > 0)
                                    {
                                        key++; //+1 indexing
                                    }
                                    byte[] buff2 = BitConverter.GetBytes(key);
                                    for (int o = 0; o < 4; o++)
                                    {
                                        //Write object property value
                                        //byte preval = exportdata[o + o];
                                        data[off + o] = buff2[o];
                                        //byte postval = exportdata[destprop.offsetval + o];

                                        //Debug.WriteLine("Updating Byte at 0x" + (destprop.offsetval + o).ToString("X4") + " from " + preval + " to " + postval + ". It should have been set to " + buff2[o]);
                                    }
                                    destinationExport.Data = data;
                                }
                                else
                                {
                                    Debug.WriteLine("Relink miss, attempting JIT relink on " + n + " " + rootNode.Text);
                                    if (n < 0 && Math.Abs(n) - 1 < pcc.ImportCount)
                                    {
                                        //Lets add this as an import. Or at least find one
                                        ImportEntry origImport = pcc.getImport(Math.Abs(n) - 1);
                                        string origImportFullName = origImport.GetFullPath;
                                        Debug.WriteLine("We should import " + origImport.GetFullPath);

                                        int newFileObjectValue = n;
                                        foreach(ImportEntry imp in destinationExport.FileRef.Imports)
                                        {
                                            if (imp.GetFullPath == origImportFullName)
                                            {
                                                Debug.WriteLine("RELINK " + n + " to " + imp.UIndex);
                                                newFileObjectValue = imp.UIndex;
                                                break;
                                            }
                                        }
                                        if (newFileObjectValue == n)
                                        {
                                            //it doesn't exist locally so we need to find or add the upstream imports
                                            ImportEntry newImport = getOrAddCrossImport(origImportFullName, destinationExport.FileRef);
                                            if (newImport != null)
                                            {
                                                newFileObjectValue = newImport.UIndex;
                                            }
                                        }

                                        if (newFileObjectValue != n)
                                        {
                                            //Write new value
                                            byte[] data = destinationExport.Data;
                                            byte[] buff2 = BitConverter.GetBytes(newFileObjectValue);
                                            for (int o = 0; o < 4; o++)
                                            {
                                                data[off + o] = buff2[o];
                                                //Debug.WriteLine("Updating Byte at 0x" + (destprop.offsetval + o).ToString("X4") + " from " + preval + " to " + postval + ". It should have been set to " + buff2[o]);
                                            }
                                            destinationExport.Data = data;
                                        }
                                    }
                                }
                            }



                            //if (n > 0)
                            //{
                            //    //update export
                            //    Debug.WriteLine("EX Object Data: " + n + " " + pcc.Exports[n - 1].ObjectName);
                            //}
                            //else if (n < 0)
                            //{
                            //    //update import ref
                            //    Debug.WriteLine("IM Object Data: " + n + " " + pcc.Imports[-n - 1].ObjectName);

                            //}
                        }
                    }
                }
            }
        }

        private ImportEntry getOrAddCrossImport(string importFullName, IMEPackage destinationPCC)
        {
            //see if this import exists locally
            foreach (ImportEntry imp in destinationPCC.Imports)
            {
                if (imp.GetFullPath == importFullName)
                {
                    return imp;
                }
            }

            //Import doesn't exist, so we're gonna need to add it
            //But first we need to figure out what needs to be added upstream as links
            //Search upstream until we find something, or we can't g
            string[] importParts = importFullName.Split('.');
            List<int> upstreamLinks = new List<int>(); //0 = top level, 1 = next level... n = what we wanted to import
            int upstreamCount = 1;

            ImportEntry upstreamImport = null;
            while (upstreamCount < importParts.Count())
            {
                string upstream = String.Join(".", importParts, 0, importParts.Count() - upstreamCount);
                foreach (ImportEntry imp in destinationPCC.Imports)
                {
                    if (imp.GetFullPath == upstream)
                    {
                        upstreamImport = imp;
                        break;
                    }
                }

                if (upstreamImport != null)
                {
                    break;
                }
                upstreamCount++;
            }

            ImportEntry mostdownstreamimport = null;
            if (upstreamImport == null)
            {
                string fullobjectname = importParts[0];

                ImportEntry donorTopLevelImport = null;
                foreach (ImportEntry imp in pcc.Imports) //importing side info we will move to our dest pcc
                {
                    if (imp.GetFullPath == fullobjectname)
                    {
                        donorTopLevelImport = imp;
                        break;
                    }
                }

                //Create new toplevel import and set that as the most downstream one.
                int downstreamPackageName = destinationPCC.FindNameOrAdd(donorTopLevelImport.PackageFile);
                int downstreamClassName = destinationPCC.FindNameOrAdd(donorTopLevelImport.ClassName);
                int downstreamName = destinationPCC.FindNameOrAdd(fullobjectname);

                mostdownstreamimport = new ImportEntry(destinationPCC);
               // mostdownstreamimport.idxLink = downstreamLinkIdx; ??
                mostdownstreamimport.idxClassName = downstreamClassName;
                mostdownstreamimport.idxObjectName = downstreamName;
                mostdownstreamimport.idxPackageName = downstreamPackageName;
                destinationPCC.addImport(mostdownstreamimport);
                upstreamImport = mostdownstreamimport;
                upstreamCount--;
                //return null;
            }

            //Have an upstream import, now we need to add downstream imports.

            while (upstreamCount > 0)
            {
                upstreamCount--;
                string fullobjectname = String.Join(".", importParts, 0, importParts.Count() - upstreamCount);
                ImportEntry donorUpstreamImport = null;
                foreach (ImportEntry imp in pcc.Imports) //importing side info we will move to our dest pcc
                {
                    if (imp.GetFullPath == fullobjectname)
                    {
                        donorUpstreamImport = imp;
                        break;
                    }
                }


                int downstreamName = destinationPCC.FindNameOrAdd(importParts[importParts.Count() - upstreamCount - 1]);
                Debug.WriteLine(destinationPCC.Names[downstreamName]);
                int downstreamLinkIdx = upstreamImport.UIndex;
                Debug.WriteLine(upstreamImport.GetFullPath);

                int downstreamPackageName = destinationPCC.FindNameOrAdd(Path.GetFileNameWithoutExtension(donorUpstreamImport.PackageFile));
                int downstreamClassName = destinationPCC.FindNameOrAdd(donorUpstreamImport.ClassName);

                //ImportEntry classImport = getOrAddImport();
                //int downstreamClass = 0;
                //if (classImport != null) {
                //    downstreamClass = classImport.UIndex; //no recursion pls
                //} else
                //{
                //    throw new Exception("No class was found for importing");
                //}

                mostdownstreamimport = new ImportEntry(destinationPCC);
                mostdownstreamimport.idxLink = downstreamLinkIdx;
                mostdownstreamimport.idxClassName = downstreamClassName;
                mostdownstreamimport.idxObjectName = downstreamName;
                mostdownstreamimport.idxPackageName = downstreamPackageName;
                destinationPCC.addImport(mostdownstreamimport);
                upstreamImport = mostdownstreamimport;
            }
            return mostdownstreamimport;
        }

        public void InitInterpreter(BioTlkFileSet editorTlkSet = null)
        {
            memory = export.Data;
            memsize = memory.Length;
            DynamicByteProvider db = new DynamicByteProvider(export.Data);
            hb1.ByteProvider = db;
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

        public new void Show()
        {
            base.Show();
        }

        private void StartScan(IEnumerable<string> expandedNodes = null, string topNodeName = null, string selectedNodeName = null)
        {
            resetPropEditingControls();
            treeView1.BeginUpdate();
            treeView1.Nodes.Clear();
            readerpos = export.GetPropertyStart();

            TreeNode topLevelTree = new TreeNode("0000 : " + export.ObjectName);
            topLevelTree.Tag = nodeType.Root;
            topLevelTree.Name = "0";
            try
            {
                List<PropHeader> topLevelHeaders = ReadHeadersTillNone();
                GenerateTree(topLevelTree, topLevelHeaders);
            }
            catch (Exception ex)
            {
                topLevelTree.Nodes.Add("PARSE ERROR " + ex.Message);
                addPropButton.Visible = false;
            }
            treeView1.Nodes.Add(topLevelTree);
            treeView1.CollapseAll();
            treeView1.Nodes[0].Expand();
            TreeNode[] nodes;
            if (expandedNodes != null)
            {
                int memDiff = memory.Length - memsize;
                int selectedPos = getPosFromNode(selectedNodeName);
                int curPos = 0;
                foreach (string item in expandedNodes)
                {
                    curPos = getPosFromNode(item);
                    if (curPos > selectedPos)
                    {
                        curPos += memDiff;
                    }
                    nodes = treeView1.Nodes.Find((item[0] == '-' ? -curPos : curPos).ToString(), true);
                    if (nodes.Length > 0)
                    {
                        foreach (var node in nodes)
                        {
                            node.Expand();
                        }
                    }
                }
            }
            nodes = treeView1.Nodes.Find(topNodeName, true);
            if (nodes.Length > 0)
            {
                treeView1.TopNode = nodes[0];
            }
            nodes = treeView1.Nodes.Find(selectedNodeName, true);
            if (nodes.Length > 0)
            {
                treeView1.SelectedNode = nodes[0];
            }
            else
            {
                treeView1.SelectedNode = treeView1.Nodes[0];
            }
            treeView1.EndUpdate();
            memsize = memory.Length;
        }

        public void GenerateTree(TreeNode localRoot, List<PropHeader> headersList)
        {
            foreach (PropHeader header in headersList)
            {
                if (readerpos > memory.Length)
                {
                    throw new IndexOutOfRangeException(": tried to read past bounds of Export Data");
                }
                nodeType type = getType(pcc.getNameEntry(header.type));
                if (type != nodeType.ArrayProperty && type != nodeType.StructProperty)
                    localRoot.Nodes.Add(GenerateNode(header));
                else
                {
                    if (type == nodeType.ArrayProperty)
                    {
                        TreeNode t = GenerateNode(header);
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
                            t.Text = t.Text.Insert(t.Text.IndexOf("Size: ") - 2, $"({info.reference})");
                            for (int i = 0; i < arrayLength; i++)
                            {
                                readerpos = tmp;
                                int pos = tmp;
                                List<PropHeader> arrayListPropHeaders = ReadHeadersTillNone();
                                tmp = readerpos;
                                TreeNode n = new TreeNode(i.ToString());
                                n.Tag = nodeType.ArrayLeafStruct;
                                n.Name = (-pos).ToString();
                                t.Nodes.Add(n);
                                n = t.LastNode;
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
                                t.LastNode.Remove();
                                t.Nodes.Add(n);
                            }
                            localRoot.Nodes.Add(t);
                        }
                        else
                        {
                            t.Text = t.Text.Insert(t.Text.IndexOf("Size: ") - 2, $"({arrayType.ToString()})");
                            int count = 0;
                            int pos;
                            if (header.size > 1000 && arrayType == ArrayType.Byte)
                            {
                                TreeNode node = new TreeNode();
                                node.Name = (header.offset + 28).ToString();
                                node.Tag = nodeType.Unknown;
                                node.Text = "Large binary data array. Skipping Parsing";
                                t.Nodes.Add(node);
                                localRoot.Nodes.Add(t);
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
                                TreeNode node = new TreeNode();
                                node.Name = pos.ToString();
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
                                node.Text = s;
                                t.Nodes.Add(node);
                            }
                            localRoot.Nodes.Add(t);
                        }
                    }
                    if (type == nodeType.StructProperty)
                    {
                        TreeNode t = GenerateNode(header);
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
                        localRoot.Nodes.Add(t);
                    }

                }
            }
        }
        //structs that are serialized down to just their values.
        private void GenerateSpecialStruct(TreeNode t, string structType, int size)
        {
            TreeNode node;
            //have to handle this specially to get the degrees conversion
            if (structType == "Rotator")
            {
                string[] labels = { "Pitch", "Yaw", "Roll" };
                int val;
                for (int i = 0; i < 3; i++)
                {
                    val = BitConverter.ToInt32(memory, readerpos);
                    node = new TreeNode(readerpos.ToString("X4") + ": " + labels[i] + " : " + val + " (" + (val * 360f / 65536f).ToString("0.0######") + " degrees)");
                    node.Name = readerpos.ToString();
                    node.Tag = nodeType.StructLeafDeg;
                    t.Nodes.Add(node);
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
                            node = new TreeNode(readerpos.ToString("X4") + ": " + memory.Skip(readerpos).Take(size).Aggregate("", (b, s) => b + " " + s.ToString("X2")));
                            node.Tag = nodeType.Unknown;
                            t.Nodes.Add(node);
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
                        node = new TreeNode(pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
                        node.Name = pos.ToString();
                        node.Tag = nodeType.StructLeafFloat;
                        t.Nodes.Add(node);
                        pos += 4;
                    }
                }
                else if (structType == "Vector" || structType == "RwVector3")
                {
                    string[] labels = { "X", "Y", "Z" };
                    for (int i = 0; i < 3; i++)
                    {
                        node = new TreeNode(pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
                        node.Name = pos.ToString();
                        node.Tag = nodeType.StructLeafFloat;
                        t.Nodes.Add(node);
                        pos += 4;
                    }
                }
                else if (structType == "Color")
                {
                    string[] labels = { "B", "G", "R", "A" };
                    for (int i = 0; i < 4; i++)
                    {
                        node = new TreeNode(pos.ToString("X4") + ": " + labels[i] + " : " + memory[pos]);
                        node.Name = pos.ToString();
                        node.Tag = nodeType.StructLeafByte;
                        t.Nodes.Add(node);
                        pos += 1;
                    }
                }
                else if (structType == "LinearColor")
                {
                    string[] labels = { "R", "G", "B", "A" };
                    for (int i = 0; i < 4; i++)
                    {
                        node = new TreeNode(pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
                        node.Name = pos.ToString();
                        node.Tag = nodeType.StructLeafFloat;
                        t.Nodes.Add(node);
                        pos += 4;
                    }
                }
                //uses EndsWith to support RwQuat, RwVector4, and RwPlane
                else if (structType.EndsWith("Quat") || structType.EndsWith("Vector4") || structType.EndsWith("Plane"))
                {
                    string[] labels = { "X", "Y", "Z", "W" };
                    for (int i = 0; i < 4; i++)
                    {
                        node = new TreeNode(pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
                        node.Name = pos.ToString();
                        node.Tag = nodeType.StructLeafFloat;
                        t.Nodes.Add(node);
                        pos += 4;
                    }
                }
                else if (structType == "TwoVectors")
                {
                    string[] labels = { "X", "Y", "Z", "X", "Y", "Z" };
                    for (int i = 0; i < 6; i++)
                    {
                        node = new TreeNode(pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
                        node.Name = pos.ToString();
                        node.Tag = nodeType.StructLeafFloat;
                        t.Nodes.Add(node);
                        pos += 4;
                    }
                }
                else if (structType == "Matrix" || structType == "RwMatrix44")
                {
                    string[] labels = { "X Plane", "Y Plane", "Z Plane", "W Plane" };
                    string[] labels2 = { "X", "Y", "Z", "W" };
                    TreeNode node2;
                    for (int i = 0; i < 3; i++)
                    {
                        node2 = new TreeNode(labels[i]);
                        node2.Name = pos.ToString();
                        for (int j = 0; j < 4; j++)
                        {
                            node = new TreeNode(pos.ToString("X4") + ": " + labels2[j] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
                            node.Name = pos.ToString();
                            node.Tag = nodeType.StructLeafFloat;
                            node2.Nodes.Add(node);
                            pos += 4;
                        }
                        t.Nodes.Add(node2);
                    }
                }
                else if (structType == "Guid")
                {
                    string[] labels = { "A", "B", "C", "D" };
                    for (int i = 0; i < 4; i++)
                    {
                        node = new TreeNode(pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToInt32(memory, pos));
                        node.Name = pos.ToString();
                        node.Tag = nodeType.StructLeafInt;
                        t.Nodes.Add(node);
                        pos += 4;
                    }
                }
                else if (structType == "IntPoint")
                {
                    string[] labels = { "X", "Y" };
                    for (int i = 0; i < 2; i++)
                    {
                        node = new TreeNode(pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToInt32(memory, pos));
                        node.Name = pos.ToString();
                        node.Tag = nodeType.StructLeafInt;
                        t.Nodes.Add(node);
                        pos += 4;
                    }
                }
                else if (structType == "Box" || structType == "BioRwBox")
                {
                    string[] labels = { "Min", "Max" };
                    string[] labels2 = { "X", "Y", "Z" };
                    TreeNode node2;
                    for (int i = 0; i < 2; i++)
                    {
                        node2 = new TreeNode(labels[i]);
                        node2.Name = pos.ToString();
                        for (int j = 0; j < 3; j++)
                        {
                            node = new TreeNode(pos.ToString("X4") + ": " + labels2[j] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
                            node.Name = pos.ToString();
                            node.Tag = nodeType.StructLeafFloat;
                            node2.Nodes.Add(node);
                            pos += 4;
                        }
                        t.Nodes.Add(node2);
                    }
                    node = new TreeNode(pos.ToString("X4") + ": IsValid : " + memory[pos]);
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafByte;
                    t.Nodes.Add(node);
                    pos += 1;
                }
                else
                {
                    //just prints the raw hex since there's no telling what it actually is
                    node = new TreeNode(pos.ToString("X4") + ": " + memory.Skip(pos).Take(size).Aggregate("", (b, s) => b + " " + s.ToString("X2")));
                    node.Tag = nodeType.Unknown;
                    t.Nodes.Add(node);
                    pos += size;
                }
                readerpos = pos;
            }
        }

        private int GenerateSpecialStructProp(TreeNode t, string s, int pos, PropertyReader.Property prop)
        {
            if (pos > memory.Length)
            {
                throw new Exception(": tried to read past bounds of Export Data");
            }
            int n;
            TreeNode node;
            PropertyInfo propInfo;
            switch (prop.TypeVal)
            {
                case PropertyType.FloatProperty:
                    s += BitConverter.ToSingle(memory, pos).ToString("0.0######");
                    node = new TreeNode(s);
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafFloat;
                    t.Nodes.Add(node);
                    pos += 4;
                    break;
                case PropertyType.IntProperty:
                    s += BitConverter.ToInt32(memory, pos).ToString();
                    node = new TreeNode(s);
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafInt;
                    t.Nodes.Add(node);
                    pos += 4;
                    break;
                case PropertyType.ObjectProperty:
                    n = BitConverter.ToInt32(memory, pos);
                    s += n + " (" + pcc.getObjectName(n) + ")";
                    node = new TreeNode(s);
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafObject;
                    t.Nodes.Add(node);
                    pos += 4;
                    break;
                case PropertyType.StringRefProperty:
                    n = BitConverter.ToInt32(memory, pos);
                    s += "#" + n + ": ";
                    s += ME3TalkFiles.tlkList.Count == 0 ? "(.tlk not loaded)" : ME3TalkFiles.findDataById(n);
                    node = new TreeNode(s);
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafInt;
                    t.Nodes.Add(node);
                    pos += 4;
                    break;
                case PropertyType.NameProperty:
                    n = BitConverter.ToInt32(memory, pos);
                    pos += 4;
                    s += "\"" + pcc.getNameEntry(n) + "\"_" + BitConverter.ToInt32(memory, pos);
                    node = new TreeNode(s);
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafName;
                    t.Nodes.Add(node);
                    pos += 4;
                    break;
                case PropertyType.BoolProperty:
                    s += (memory[pos] > 0).ToString();
                    node = new TreeNode(s);
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafBool;
                    t.Nodes.Add(node);
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
                        node = new TreeNode(s);
                        node.Name = pos.ToString();
                        node.Tag = nodeType.StructLeafEnum;
                        t.Nodes.Add(node);
                        pos += 8;
                    }
                    else
                    {
                        s += "(byte)" + memory[pos];
                        node = new TreeNode(s);
                        node.Name = pos.ToString();
                        node.Tag = nodeType.StructLeafByte;
                        t.Nodes.Add(node);
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
                    node = new TreeNode(s);
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafStr;
                    t.Nodes.Add(node);
                    pos += n * 2;
                    break;
                case PropertyType.ArrayProperty:
                    n = BitConverter.ToInt32(memory, pos);
                    s += n + " elements";
                    node = new TreeNode(s);
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafArray;
                    pos += 4;
                    propInfo = GetPropertyInfo(prop.Name);
                    ArrayType arrayType = GetArrayType(propInfo);
                    TreeNode node2;
                    string s2;
                    for (int i = 0; i < n; i++)
                    {
                        if (arrayType == ArrayType.Struct)
                        {
                            readerpos = pos;
                            node2 = new TreeNode(i + ": (" + propInfo.reference + ")");
                            node2.Name = (-pos).ToString();
                            node2.Tag = nodeType.StructLeafStruct;
                            GenerateSpecialStruct(node2, propInfo.reference, 0);
                            node.Nodes.Add(node2);
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
                    t.Nodes.Add(node);
                    break;
                case PropertyType.StructProperty:
                    propInfo = GetPropertyInfo(prop.Name);
                    s += propInfo.reference;
                    node = new TreeNode(s);
                    node.Name = (-pos).ToString();
                    node.Tag = nodeType.StructLeafStruct;
                    readerpos = pos;
                    GenerateSpecialStruct(node, propInfo.reference, 0);
                    pos = readerpos;
                    t.Nodes.Add(node);
                    break;
                case PropertyType.DelegateProperty:
                    throw new NotImplementedException($"at position {pos.ToString("X4")}: cannot read Delegate property of Immutable struct");
                case PropertyType.Unknown:
                    throw new NotImplementedException($"at position {pos.ToString("X4")}: cannot read Unknown property of Immutable struct");
                case PropertyType.None:
                default:
                    break;
            }

            return pos;
        }

        public TreeNode GenerateNode(PropHeader p)
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
                    if (pcc.Game == MEGame.ME3)
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
            TreeNode ret = new TreeNode(s);
            ret.Tag = propertyType;
            ret.Name = p.offset.ToString();
            return ret;
        }

        public nodeType getType(string s)
        {
            int ret = -1;
            for (int i = 0; i < Types.Length; i++)
                if (s == Types[i])
                    ret = i;
            return (nodeType)ret;
        }

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
                    if (pcc.getNameEntry(p.name) != "None")
                    {
                        p.type = BitConverter.ToInt32(memory, readerpos + 8);
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
                            if (pcc.Game == MEGame.ME3)
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

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.txt|*.txt";
            d.FileName = export.ObjectName + ".txt";
            if (d.ShowDialog() == DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                PrintNodes(treeView1.Nodes, fs, 0);
                fs.Close();
                MessageBox.Show("Done.");
            }
        }

        public void PrintNodes(TreeNodeCollection t, FileStream fs, int depth)
        {
            string tab = "";
            for (int i = 0; i < depth; i++)
                tab += ' ';
            foreach (TreeNode t1 in t)
            {
                string s = tab + t1.Text;
                WriteString(fs, s);
                fs.WriteByte(0xD);
                fs.WriteByte(0xA);
                if (t1.Nodes.Count != 0)
                    PrintNodes(t1.Nodes, fs, depth + 4);
            }
        }

        public void WriteString(FileStream fs, string s)
        {
            for (int i = 0; i < s.Length; i++)
                fs.WriteByte((byte)s[i]);
        }

        private string getEnclosingType(TreeNode node)
        {
            Stack<TreeNode> nodeStack = new Stack<TreeNode>();
            string typeName = className;
            string propname;
            PropertyInfo p;
            while (node != null && !node.Tag.Equals(nodeType.Root))
            {
                nodeStack.Push(node);
                node = node.Parent;
            }
            bool isStruct = false;
            while (nodeStack.Count > 0)
            {
                node = nodeStack.Pop();
                if ((nodeType)node.Tag == nodeType.ArrayLeafStruct)
                {
                    continue;
                }
                propname = pcc.getNameEntry(BitConverter.ToInt32(memory, getPosFromNode(node.Name)));
                p = GetPropertyInfo(propname, typeName, isStruct);
                typeName = p.reference;
                isStruct = true;
            }
            return typeName;
        }
        private bool isArrayLeaf(nodeType type)
        {
            return (type == nodeType.ArrayLeafBool || type == nodeType.ArrayLeafEnum || type == nodeType.ArrayLeafFloat ||
                type == nodeType.ArrayLeafInt || type == nodeType.ArrayLeafName || type == nodeType.ArrayLeafObject ||
                type == nodeType.ArrayLeafString || type == nodeType.ArrayLeafStruct || type == nodeType.ArrayLeafByte);
        }

        private bool isStructLeaf(nodeType type)
        {
            return (type == nodeType.StructLeafByte || type == nodeType.StructLeafDeg || type == nodeType.StructLeafFloat ||
                type == nodeType.StructLeafBool || type == nodeType.StructLeafInt || type == nodeType.StructLeafName ||
                type == nodeType.StructLeafStr || type == nodeType.StructLeafEnum || type == nodeType.StructLeafArray ||
                type == nodeType.StructLeafStruct || type == nodeType.StructLeafObject);
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            LAST_SELECTED_NODE = e.Node;
            resetPropEditingControls();
            if (e.Node.Name == "")
            {
                Debug.WriteLine("This node is not parsable.");
                //can't attempt to parse this.
                LAST_SELECTED_PROP_TYPE = nodeType.Unknown;
                return;
            }
            try
            {
                int off = getPosFromNode(e.Node.Name);
                hb1.SelectionStart = off;
                lastSetOffset = off;
                hb1.SelectionLength = 1;
                if (e.Node.Tag == null)
                {
                    LAST_SELECTED_PROP_TYPE = nodeType.Unknown;
                    return;
                }
                LAST_SELECTED_PROP_TYPE = (nodeType)e.Node.Tag;
                if (isArrayLeaf(LAST_SELECTED_PROP_TYPE) || isStructLeaf(LAST_SELECTED_PROP_TYPE))
                {
                    TryParseStructPropertyOrArrayLeaf(e.Node);
                }
                else if (LAST_SELECTED_PROP_TYPE == nodeType.ArrayProperty)
                {
                    addArrayElementButton.Visible = true;
                    proptext.Clear();
                    ArrayType arrayType = GetArrayType(BitConverter.ToInt32(memory, off), getEnclosingType(e.Node.Parent));
                    switch (arrayType)
                    {
                        case ArrayType.Byte:
                        case ArrayType.String:
                            proptext.Visible = true;
                            break;
                        case ArrayType.Object:
                            objectNameLabel.Text = "()";
                            proptext.Visible = objectNameLabel.Visible = true;
                            break;
                        case ArrayType.Int:
                            proptext.Text = "0";
                            proptext.Visible = true;
                            break;
                        case ArrayType.Float:
                            proptext.Text = "0.0";
                            proptext.Visible = true;
                            break;
                        case ArrayType.Name:
                            proptext.Text = "0";
                            nameEntry.AutoCompleteCustomSource.AddRange(pcc.Names.ToArray());
                            proptext.Visible = nameEntry.Visible = true;
                            break;
                        case ArrayType.Bool:
                            propDropdown.Items.Clear();
                            propDropdown.Items.Add("False");
                            propDropdown.Items.Add("True");
                            propDropdown.Visible = true;
                            break;
                        case ArrayType.Enum:
                            string enumName = getEnclosingType(e.Node);
                            List<string> values = GetEnumValues(enumName, BitConverter.ToInt32(memory, getPosFromNode(e.Node.Parent.Name)));
                            if (values == null)
                            {
                                addArrayElementButton.Visible = false;
                                return;
                            }
                            propDropdown.Items.Clear();
                            propDropdown.Items.AddRange(values.ToArray());
                            propDropdown.Visible = true;
                            break;
                        case ArrayType.Struct:
                        default:
                            break;
                    }
                }
                else if (LAST_SELECTED_PROP_TYPE == nodeType.Root)
                {
                    addPropButton.Visible = true;
                }
                else if (LAST_SELECTED_PROP_TYPE == nodeType.None && e.Node.Parent.Tag != null && e.Node.Parent.Tag.Equals(nodeType.Root))
                {
                    addPropButton.Visible = true;
                }
                else
                {
                    TryParseProperty();
                }
            }
            catch (Exception)
            {
                Debug.WriteLine("Node name is not in correct format.");
                //name is wrong, don't attempt to continue parsing.
                LAST_SELECTED_PROP_TYPE = nodeType.Unknown;
                return;
            }
        }
        private void resetPropEditingControls()
        {
            objectNameLabel.Visible = nameEntry.Visible = proptext.Visible = setPropertyButton.Visible = propDropdown.Visible =
                addArrayElementButton.Visible = deleteArrayElementButton.Visible = moveDownButton.Visible =
                moveUpButton.Visible = addPropButton.Visible = false;
            nameEntry.AutoCompleteCustomSource.Clear();
            nameEntry.Clear();
            proptext.Clear();
        }

        private void TryParseProperty()
        {
            try
            {
                int pos = (int)hb1.SelectionStart;
                if (memory.Length - pos < 16)
                    return;
                int type = BitConverter.ToInt32(memory, pos + 8);
                int test = BitConverter.ToInt32(memory, pos + 12);
                if (test != 0 || !pcc.isName(type))
                    return;
                switch (pcc.getNameEntry(type))
                {
                    case "IntProperty":
                    case "StringRefProperty":
                        proptext.Text = BitConverter.ToInt32(memory, pos + 24).ToString();
                        proptext.Visible = true;
                        break;
                    case "ObjectProperty":
                        int n = BitConverter.ToInt32(memory, pos + 24);
                        objectNameLabel.Text = $"({pcc.getObjectName(n)})";
                        proptext.Text = n.ToString();
                        objectNameLabel.Visible = proptext.Visible = true;
                        break;
                    case "FloatProperty":
                        proptext.Text = BitConverter.ToSingle(memory, pos + 24).ToString();
                        proptext.Visible = true;
                        break;
                    case "BoolProperty":
                        propDropdown.Items.Clear();
                        propDropdown.Items.Add("False");
                        propDropdown.Items.Add("True");
                        propDropdown.SelectedIndex = memory[pos + 24];
                        propDropdown.Visible = true;
                        break;
                    case "NameProperty":
                        proptext.Text = BitConverter.ToInt32(memory, pos + 28).ToString();
                        nameEntry.Text = pcc.getNameEntry(BitConverter.ToInt32(memory, pos + 24));
                        nameEntry.AutoCompleteCustomSource.AddRange(pcc.Names.ToArray());
                        nameEntry.Visible = true;
                        proptext.Visible = true;
                        break;
                    case "StrProperty":
                        string s = "";
                        int count = BitConverter.ToInt32(memory, pos + 24);
                        pos += 28;
                        if (count < 0)
                        {
                            for (int i = 0; i < -count; i++)
                            {
                                s += (char)memory[pos + i * 2];
                            }
                        }
                        else
                        {
                            for (int i = 0; i < count; i++)
                            {
                                s += (char)memory[pos + i];
                            }
                        }
                        proptext.Text = s;
                        proptext.Visible = true;
                        break;
                    case "ByteProperty":
                        int size = BitConverter.ToInt32(memory, pos + 16);
                        string enumName = pcc.getNameEntry(BitConverter.ToInt32(memory, pos + 24));
                        int valOffset;
                        if (pcc.Game == MEGame.ME3)
                        {
                            valOffset = 32;
                        }
                        else
                        {
                            valOffset = 24;
                        }
                        if (size > 1)
                        {
                            try
                            {
                                List<string> values = GetEnumValues(enumName, BitConverter.ToInt32(memory, pos));
                                if (values != null)
                                {
                                    propDropdown.Items.Clear();
                                    propDropdown.Items.AddRange(values.ToArray());
                                    propDropdown.SelectedItem = pcc.getNameEntry(BitConverter.ToInt32(memory, pos + valOffset));
                                    propDropdown.Visible = true;
                                }
                            }
                            catch (Exception)
                            {
                            }
                        }
                        else
                        {
                            proptext.Text = memory[pos + valOffset].ToString();
                            proptext.Visible = true;
                        }
                        break;
                    default:
                        return;
                }
                setPropertyButton.Visible = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void TryParseStructPropertyOrArrayLeaf(TreeNode node)
        {
            try
            {
                nodeType type = (nodeType)node.Tag;
                int pos = (int)hb1.SelectionStart;
                if (memory.Length - pos < 8)
                    return;
                switch (type)
                {
                    case nodeType.ArrayLeafInt:
                    case nodeType.StructLeafInt:
                        proptext.Text = BitConverter.ToInt32(memory, pos).ToString();
                        proptext.Visible = true;
                        break;
                    case nodeType.ArrayLeafObject:
                    case nodeType.StructLeafObject:
                        int n = BitConverter.ToInt32(memory, pos);
                        objectNameLabel.Text = $"({pcc.getObjectName(n)})";
                        proptext.Text = n.ToString();
                        proptext.Visible = objectNameLabel.Visible = true;
                        break;
                    case nodeType.ArrayLeafFloat:
                    case nodeType.StructLeafFloat:
                        proptext.Text = BitConverter.ToSingle(memory, pos).ToString();
                        proptext.Visible = true;
                        break;
                    case nodeType.ArrayLeafBool:
                    case nodeType.StructLeafBool:
                        propDropdown.Items.Clear();
                        propDropdown.Items.Add("False");
                        propDropdown.Items.Add("True");
                        propDropdown.SelectedIndex = memory[pos];
                        propDropdown.Visible = true;
                        break;
                    case nodeType.ArrayLeafByte:
                    case nodeType.StructLeafByte:
                        proptext.Text = memory[pos].ToString();
                        proptext.Visible = true;
                        break;
                    case nodeType.ArrayLeafName:
                    case nodeType.StructLeafName:
                        proptext.Text = BitConverter.ToInt32(memory, pos + 4).ToString();
                        nameEntry.Text = pcc.getNameEntry(BitConverter.ToInt32(memory, pos));
                        nameEntry.AutoCompleteCustomSource.AddRange(pcc.Names.ToArray());
                        nameEntry.Visible = proptext.Visible = true;
                        break;
                    case nodeType.ArrayLeafString:
                    case nodeType.StructLeafStr:
                        string s = "";
                        int count = -BitConverter.ToInt32(memory, pos);
                        for (int i = 0; i < count - 1; i++)
                        {
                            s += (char)memory[pos + 4 + i * 2];
                        }
                        proptext.Text = s;
                        proptext.Visible = true;
                        break;
                    case nodeType.ArrayLeafEnum:
                    case nodeType.StructLeafEnum:
                        string enumName;
                        if (type == nodeType.StructLeafEnum)
                        {
                            int begin = node.Text.LastIndexOf(':') + 3;
                            enumName = node.Text.Substring(begin, node.Text.IndexOf(',') - 1 - begin);
                        }
                        else
                        {
                            enumName = getEnclosingType(node.Parent);
                        }
                        List<string> values = GetEnumValues(enumName, BitConverter.ToInt32(memory, getPosFromNode(node.Parent)));
                        if (values == null)
                        {
                            return;
                        }
                        propDropdown.Items.Clear();
                        propDropdown.Items.AddRange(values.ToArray());
                        setPropertyButton.Visible = propDropdown.Visible = true;
                        propDropdown.SelectedItem = pcc.getNameEntry(BitConverter.ToInt32(memory, pos));
                        break;
                    case nodeType.StructLeafDeg:
                        proptext.Text = (BitConverter.ToInt32(memory, pos) * 360f / 65536f).ToString();
                        proptext.Visible = true;
                        break;
                    case nodeType.ArrayLeafStruct:
                        break;
                    default:
                        return;
                }
                setPropertyButton.Visible = true;
                if (isArrayLeaf(type))
                {
                    deleteArrayElementButton.Visible = addArrayElementButton.Visible = true;
                    if (type == nodeType.ArrayLeafStruct)
                    {
                        setPropertyButton.Visible = false;
                    }
                    if (node.NextNode != null)
                    {
                        moveDownButton.Visible = true;
                    }
                    if (node.PrevNode != null)
                    {
                        moveUpButton.Visible = true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void setProperty_Click(object sender, EventArgs e)
        {
            if (hb1.SelectionStart != lastSetOffset)
            {
                return; //user manually moved cursor
            }
            if (isArrayLeaf(LAST_SELECTED_PROP_TYPE) || isStructLeaf(LAST_SELECTED_PROP_TYPE))
            {
                setStructOrArrayProperty();
            }
            else
            {
                setNonArrayProperty();
            }
        }

        private void setStructOrArrayProperty()
        {
            try
            {
                int pos = lastSetOffset;
                if (memory.Length - pos < 8)
                    return;
                byte b = 0;
                float f = 0;
                int i = 0;
                switch (LAST_SELECTED_PROP_TYPE)
                {
                    case nodeType.ArrayLeafByte:
                    case nodeType.StructLeafByte:
                        if (byte.TryParse(proptext.Text, out b))
                        {
                            memory[pos] = b;
                            UpdateMem(pos);
                        }
                        break;
                    case nodeType.ArrayLeafBool:
                    case nodeType.StructLeafBool:
                        memory[pos] = (byte)propDropdown.SelectedIndex;
                        UpdateMem(pos);
                        break;
                    case nodeType.ArrayLeafFloat:
                    case nodeType.StructLeafFloat:
                        proptext.Text = CheckSeperator(proptext.Text);
                        if (float.TryParse(proptext.Text, out f))
                        {
                            WriteMem(pos, BitConverter.GetBytes(f));
                            UpdateMem(pos);
                        }
                        break;
                    case nodeType.StructLeafDeg:
                        if (float.TryParse(proptext.Text, out f))
                        {
                            WriteMem(pos, BitConverter.GetBytes(Convert.ToInt32(f * 65536f / 360f)));
                            UpdateMem(pos);
                        }
                        break;
                    case nodeType.ArrayLeafInt:
                    case nodeType.ArrayLeafObject:
                    case nodeType.StructLeafObject:
                    case nodeType.StructLeafInt:
                        proptext.Text = CheckSeperator(proptext.Text);
                        if (int.TryParse(proptext.Text, out i))
                        {
                            WriteMem(pos, BitConverter.GetBytes(i));
                            UpdateMem(pos);
                        }
                        break;
                    case nodeType.ArrayLeafEnum:
                    case nodeType.StructLeafEnum:
                        i = pcc.FindNameOrAdd(propDropdown.SelectedItem as string);
                        WriteMem(pos, BitConverter.GetBytes(i));
                        UpdateMem(pos);
                        break;
                    case nodeType.ArrayLeafName:
                    case nodeType.StructLeafName:
                        if (int.TryParse(proptext.Text, out i))
                        {
                            if (!pcc.Names.Contains(nameEntry.Text) &&
                                DialogResult.No == MessageBox.Show($"{Path.GetFileName(pcc.FileName)} does not contain the Name: {nameEntry.Text}\nWould you like to add it to the Name list?", "", MessageBoxButtons.YesNo))
                            {
                                break;
                            }
                            WriteMem(pos, BitConverter.GetBytes(pcc.FindNameOrAdd(nameEntry.Text)));
                            WriteMem(pos + 4, BitConverter.GetBytes(i));
                            UpdateMem(pos);
                        }
                        break;
                    case nodeType.ArrayLeafString:
                    case nodeType.StructLeafStr:
                        string s = proptext.Text;
                        int offset = pos;
                        int stringMultiplier = 1;
                        int oldLength = BitConverter.ToInt32(memory, offset);
                        if (oldLength < 0)
                        {
                            stringMultiplier = 2;
                            oldLength *= -2;
                        }
                        int oldSize = 4 + oldLength;
                        List<byte> stringBuff = new List<byte>(s.Length * stringMultiplier);
                        if (stringMultiplier == 2)
                        {
                            for (int j = 0; j < s.Length; j++)
                            {
                                stringBuff.AddRange(BitConverter.GetBytes(s[j]));
                            }
                            stringBuff.Add(0);
                        }
                        else
                        {
                            for (int j = 0; j < s.Length; j++)
                            {
                                stringBuff.Add(BitConverter.GetBytes(s[j])[0]);
                            }
                        }
                        stringBuff.Add(0);
                        byte[] buff = BitConverter.GetBytes((s.Count() + 1) * stringMultiplier + 4);
                        for (int j = 0; j < 4; j++)
                            memory[offset - 8 + j] = buff[j];
                        buff = BitConverter.GetBytes((s.Count() + 1) * (stringMultiplier == 1 ? 1 : -1));
                        for (int j = 0; j < 4; j++)
                            memory[offset + j] = buff[j];
                        buff = new byte[memory.Length - oldLength + stringBuff.Count];
                        int startLength = offset + 4;
                        int startLength2 = startLength + oldLength;
                        for (int j = 0; j < startLength; j++)
                        {
                            buff[j] = memory[j];
                        }
                        for (int j = 0; j < stringBuff.Count; j++)
                        {
                            buff[j + startLength] = stringBuff[j];
                        }
                        startLength += stringBuff.Count;
                        for (int j = 0; j < memory.Length - startLength2; j++)
                        {
                            buff[j + startLength] = memory[j + startLength2];
                        }
                        memory = buff;

                        //bubble up size
                        TreeNode parent = LAST_SELECTED_NODE.Parent;
                        while (parent != null && (parent.Tag.Equals(nodeType.StructProperty) || parent.Tag.Equals(nodeType.ArrayProperty) ||
                            parent.Tag.Equals(nodeType.ArrayLeafStruct) || isStructLeaf((nodeType)parent.Tag)))
                        {
                            if ((nodeType)parent.Tag == nodeType.ArrayLeafStruct || isStructLeaf((nodeType)parent.Tag))
                            {
                                parent = parent.Parent;
                                continue;
                            }
                            updateArrayLength(getPosFromNode(parent.Name), 0, (stringBuff.Count + 4) - oldSize);
                            parent = parent.Parent;
                        }
                        UpdateMem(pos);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void setNonArrayProperty()
        {
            try
            {
                int pos = (int)hb1.SelectionStart;
                if (memory.Length - pos < 16)
                    return;
                int type = BitConverter.ToInt32(memory, pos + 8);
                int test = BitConverter.ToInt32(memory, pos + 12);
                if (test != 0 || !pcc.isName(type))
                    return;
                int i = 0;
                float f = 0;
                byte b = 0;
                switch (pcc.getNameEntry(type))
                {
                    case "IntProperty":
                    case "ObjectProperty":
                    case "StringRefProperty":
                        if (int.TryParse(proptext.Text, out i))
                        {
                            WriteMem(pos + 24, BitConverter.GetBytes(i));
                            UpdateMem(pos);
                        }
                        break;
                    case "NameProperty":
                        if (int.TryParse(proptext.Text, out i))
                        {
                            if (!pcc.Names.Contains(nameEntry.Text) &&
                                DialogResult.No == MessageBox.Show($"{Path.GetFileName(pcc.FileName)} does not contain the Name: {nameEntry.Text}\nWould you like to add it to the Name list?", "", MessageBoxButtons.YesNo))
                            {
                                break;
                            }
                            WriteMem(pos + 24, BitConverter.GetBytes(pcc.FindNameOrAdd(nameEntry.Text)));
                            WriteMem(pos + 28, BitConverter.GetBytes(i));
                            UpdateMem(pos);
                        }
                        break;
                    case "FloatProperty":
                        proptext.Text = CheckSeperator(proptext.Text);
                        if (float.TryParse(proptext.Text, out f))
                        {
                            WriteMem(pos + 24, BitConverter.GetBytes(f));
                            UpdateMem(pos);
                        }
                        break;
                    case "BoolProperty":
                        memory[pos + 24] = (byte)propDropdown.SelectedIndex;
                        UpdateMem(pos);
                        break;
                    case "ByteProperty":
                        int valOffset;
                        if (pcc.Game == MEGame.ME3)
                        {
                            valOffset = 32;
                        }
                        else
                        {
                            valOffset = 24;
                        }
                        if (propDropdown.Visible)
                        {
                            i = pcc.FindNameOrAdd(propDropdown.SelectedItem as string);
                            WriteMem(pos + valOffset, BitConverter.GetBytes(i));
                            UpdateMem(pos);
                        }
                        else if (byte.TryParse(proptext.Text, out b))
                        {
                            memory[pos + valOffset] = b;
                            UpdateMem(pos);
                        }
                        break;
                    case "StrProperty":
                        string s = proptext.Text;
                        int offset = pos + 24;
                        int stringMultiplier = 1;
                        int oldSize = BitConverter.ToInt32(memory, pos + 16);
                        int oldLength = BitConverter.ToInt32(memory, offset);
                        if (oldLength < 0)
                        {
                            stringMultiplier = 2;
                            oldLength *= -2;
                        }
                        List<byte> stringBuff = new List<byte>(s.Length * stringMultiplier);
                        if (stringMultiplier == 2)
                        {
                            for (int j = 0; j < s.Length; j++)
                            {
                                stringBuff.AddRange(BitConverter.GetBytes(s[j]));
                            }
                            stringBuff.Add(0);
                        }
                        else
                        {
                            for (int j = 0; j < s.Length; j++)
                            {
                                stringBuff.Add(BitConverter.GetBytes(s[j])[0]);
                            }
                        }
                        stringBuff.Add(0);
                        byte[] buff = BitConverter.GetBytes((s.Count() + 1) * stringMultiplier + 4);
                        for (int j = 0; j < 4; j++)
                            memory[offset - 8 + j] = buff[j];
                        buff = BitConverter.GetBytes((s.Count() + 1) * (stringMultiplier == 1 ? 1 : -1));
                        for (int j = 0; j < 4; j++)
                            memory[offset + j] = buff[j];
                        buff = new byte[memory.Length - oldLength + stringBuff.Count];
                        int startLength = offset + 4;
                        int startLength2 = startLength + oldLength;
                        for (int j = 0; j < startLength; j++)
                        {
                            buff[j] = memory[j];
                        }
                        for (int j = 0; j < stringBuff.Count; j++)
                        {
                            buff[j + startLength] = stringBuff[j];
                        }
                        startLength += stringBuff.Count;
                        for (int j = 0; j < memory.Length - startLength2; j++)
                        {
                            buff[j + startLength] = memory[j + startLength2];
                        }
                        memory = buff;

                        //bubble up size
                        TreeNode parent = LAST_SELECTED_NODE.Parent;
                        while (parent != null && (parent.Tag.Equals(nodeType.StructProperty) || parent.Tag.Equals(nodeType.ArrayProperty) || parent.Tag.Equals(nodeType.ArrayLeafStruct)))
                        {
                            if ((nodeType)parent.Tag == nodeType.ArrayLeafStruct)
                            {
                                parent = parent.Parent;
                                continue;
                            }
                            updateArrayLength(getPosFromNode(parent.Name), 0, (stringBuff.Count + 4) - oldSize);
                            parent = parent.Parent;
                        }
                        UpdateMem(pos);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private byte[] deleteArrayLeaf()
        {
            try
            {
                int pos = (int)hb1.SelectionStart;
                if (hb1.SelectionStart != lastSetOffset)
                {
                    return new byte[0]; //user manually moved cursor
                }

                if (memory.Length - pos < 8) //not long enough to deal with
                    return new byte[0];

                byte[] removedBytes;
                TreeNode parent = LAST_SELECTED_NODE.Parent;
                int leafOffset = getPosFromNode(LAST_SELECTED_NODE.Name);
                int parentOffset = getPosFromNode(parent.Name);

                int size;
                switch (LAST_SELECTED_PROP_TYPE)
                {
                    case nodeType.ArrayLeafInt:
                    case nodeType.ArrayLeafFloat:
                    case nodeType.ArrayLeafObject:
                        size = 4;
                        break;
                    case nodeType.ArrayLeafName:
                    case nodeType.ArrayLeafEnum:
                        size = 8;
                        break;
                    case nodeType.ArrayLeafBool:
                    case nodeType.ArrayLeafByte:
                        size = 1;
                        break;
                    case nodeType.ArrayLeafString:
                        size = BitConverter.ToInt32(memory, leafOffset);
                        if (size < 0)
                        {
                            size *= -2;
                        }
                        size += 4;
                        break;
                    case nodeType.ArrayLeafStruct:
                        int tmp = readerpos = leafOffset;
                        ReadHeadersTillNone();
                        size = readerpos - tmp;
                        break;
                    default:
                        return new byte[0];
                }
                removedBytes = memory.Skip(leafOffset).Take(size).ToArray();
                //bubble up size
                bool firstbubble = true;
                while (parent != null && (parent.Tag.Equals(nodeType.StructProperty) || parent.Tag.Equals(nodeType.ArrayProperty) || parent.Tag.Equals(nodeType.ArrayLeafStruct)))
                {
                    if ((nodeType)parent.Tag == nodeType.ArrayLeafStruct)
                    {
                        parent = parent.Parent;
                        continue;
                    }
                    parentOffset = getPosFromNode(parent.Name);
                    if (firstbubble)
                    {
                        memory = RemoveIndices(memory, leafOffset, size);
                        firstbubble = false;
                        updateArrayLength(parentOffset, -1, -size);
                    }
                    else
                    {
                        updateArrayLength(parentOffset, 0, -size);
                    }
                    parent = parent.Parent;
                }
                if (LAST_SELECTED_PROP_TYPE == nodeType.ArrayLeafStruct)
                {
                    UpdateMem(-pos);
                }
                else
                {
                    UpdateMem(pos);
                }
                return removedBytes;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return new byte[0];
            }
        }

        private void addArrayLeaf()
        {
            try
            {
                int pos = (int)hb1.SelectionStart;
                if (hb1.SelectionStart != lastSetOffset)
                {
                    return; //user manually moved cursor
                }
                bool isLeaf = false;
                int leafOffset = 0;
                //is a leaf
                if (deleteArrayElementButton.Visible == true)
                {
                    isLeaf = true;
                    leafOffset = pos;
                    pos = getPosFromNode(LAST_SELECTED_NODE.Parent.Name);
                    LAST_SELECTED_NODE = LAST_SELECTED_NODE.Parent;
                }
                int size = BitConverter.ToInt32(memory, pos + 16);
                int count = BitConverter.ToInt32(memory, pos + 24);
                int leafSize = 0;
                ArrayType arrayType = GetArrayType(BitConverter.ToInt32(memory, pos), getEnclosingType(LAST_SELECTED_NODE.Parent));
                List<byte> memList = memory.ToList();
                int i;
                float f;
                byte b = 0;
                int offset;
                if (isLeaf)
                {
                    offset = leafOffset;
                }
                else
                {
                    offset = pos + 24 + size;
                }
                switch (arrayType)
                {
                    case ArrayType.Int:
                    case ArrayType.Object:
                        leafSize = 4;
                        if (!int.TryParse(proptext.Text, out i))
                        {
                            return; //not valid element
                        }
                        memList.InsertRange(offset, BitConverter.GetBytes(i));
                        break;
                    case ArrayType.Float:
                        leafSize = 4;
                        if (!float.TryParse(proptext.Text, out f))
                        {
                            return; //not valid element
                        }
                        memList.InsertRange(offset, BitConverter.GetBytes(f));
                        break;
                    case ArrayType.Byte:
                        leafSize = 1;
                        if (!byte.TryParse(proptext.Text, out b))
                        {
                            return; //not valid
                        }
                        memList.Insert(offset, b);
                        break;
                    case ArrayType.Bool:
                        leafSize = 1;
                        memList.Insert(offset, (byte)propDropdown.SelectedIndex);
                        break;
                    case ArrayType.Name:
                        leafSize = 8;
                        if (!int.TryParse(proptext.Text, out i))
                        {
                            return; //not valid
                        }
                        if (!pcc.Names.Contains(nameEntry.Text) &&
                            DialogResult.No == MessageBox.Show($"{Path.GetFileName(pcc.FileName)} does not contain the Name: {nameEntry.Text}\nWould you like to add it to the Name list?", "", MessageBoxButtons.YesNo))
                        {
                            return;
                        }
                        memList.InsertRange(offset, BitConverter.GetBytes(pcc.FindNameOrAdd(nameEntry.Text)));
                        memList.InsertRange(offset + 4, BitConverter.GetBytes(i));
                        break;
                    case ArrayType.Enum:
                        leafSize = 8;
                        string selectedItem = propDropdown.SelectedItem as string;
                        if (selectedItem == null)
                        {
                            return;
                        }
                        i = pcc.FindNameOrAdd(selectedItem);
                        memList.InsertRange(offset, BitConverter.GetBytes(i));
                        memList.InsertRange(offset + 4, new byte[4]);
                        break;
                    case ArrayType.String:
                        List<byte> stringBuff = new List<byte>();

                        if (pcc.Game == MEGame.ME3)
                        {
                            memList.InsertRange(offset, BitConverter.GetBytes(-(proptext.Text.Length + 1)));
                            for (int j = 0; j < proptext.Text.Length; j++)
                            {
                                stringBuff.AddRange(BitConverter.GetBytes(proptext.Text[j]));
                            }
                            stringBuff.Add(0);
                        }
                        else
                        {
                            memList.InsertRange(offset, BitConverter.GetBytes(proptext.Text.Length + 1));
                            for (int j = 0; j < proptext.Text.Length; j++)
                            {
                                stringBuff.Add(BitConverter.GetBytes(proptext.Text[j])[0]);
                            }
                        }
                        stringBuff.Add(0);
                        memList.InsertRange(offset + 4, stringBuff);
                        leafSize = 4 + stringBuff.Count;
                        break;
                    case ArrayType.Struct:
                        byte[] buff;
                        if (LAST_SELECTED_NODE.Nodes.Count == 0)
                        {
                            if (pcc.Game == MEGame.ME3)
                            {
                                buff = ME3UnrealObjectInfo.getDefaultClassValue(pcc as ME3Package, getEnclosingType(LAST_SELECTED_NODE));
                                if (buff == null)
                                {
                                    return;
                                }
                            }
                            else
                            {
                                MessageBox.Show("Cannot add new struct values to an array that does not already have them when editing ME1 or ME2 files.", "Sorry :(");
                                return;
                            }
                        }
                        //clone struct if existing
                        else
                        {
                            int startOff = getPosFromNode(LAST_SELECTED_NODE.LastNode);
                            int length = getPosFromNode(LAST_SELECTED_NODE.NextNode) - startOff;
                            buff = memory.Skip(startOff).Take(length).ToArray();
                        }
                        memList.InsertRange(offset, buff);
                        leafSize = buff.Length;
                        break;
                    default:
                        return;
                }
                memory = memList.ToArray();
                updateArrayLength(pos, 1, leafSize);

                //bubble up size
                TreeNode parent = LAST_SELECTED_NODE.Parent;
                while (parent != null && (parent.Tag.Equals(nodeType.StructProperty) || parent.Tag.Equals(nodeType.ArrayProperty) || parent.Tag.Equals(nodeType.ArrayLeafStruct)))
                {
                    if ((nodeType)parent.Tag == nodeType.ArrayLeafStruct)
                    {
                        parent = parent.Parent;
                        continue;
                    }
                    updateArrayLength(getPosFromNode(parent.Name), 0, leafSize);
                    parent = parent.Parent;
                }
                UpdateMem(arrayType == ArrayType.Struct ? -offset : offset);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private T[] RemoveIndices<T>(T[] IndicesArray, int RemoveAt, int NumElementsToRemove)
        {
            if (RemoveAt < 0 || RemoveAt > IndicesArray.Length - 1 || NumElementsToRemove < 0 || NumElementsToRemove + RemoveAt > IndicesArray.Length - 1)
            {
                return IndicesArray;
            }
            T[] newIndicesArray = new T[IndicesArray.Length - NumElementsToRemove];

            int i = 0;
            int j = 0;
            while (i < IndicesArray.Length)
            {
                if (i < RemoveAt || i >= RemoveAt + NumElementsToRemove)
                {
                    newIndicesArray[j] = IndicesArray[i];
                    j++;
                }
                else
                {
                    //Debug.WriteLine("Skipping byte: " + i.ToString("X4"));
                }

                i++;
            }

            return newIndicesArray;
        }

        private void WriteMem(int pos, byte[] buff)
        {
            for (int i = 0; i < buff.Length; i++)
                memory[pos + i] = buff[i];
        }

        /// <summary>
        /// Updates an array properties length and size in bytes. Does not refresh the memory view
        /// </summary>
        /// <param name="startpos">Starting index of the array property</param>
        /// <param name="countDelta">Delta in terms of how many items the array has</param>
        /// <param name="byteDelta">Delta in terms of how many bytes the array data is</param>
        private void updateArrayLength(int startpos, int countDelta, int byteDelta)
        {
            int sizeOffset = 16;
            int countOffset = 24;
            int oldSize = BitConverter.ToInt32(memory, sizeOffset + startpos);
            int oldCount = BitConverter.ToInt32(memory, countOffset + startpos);

            int newSize = oldSize + byteDelta;
            int newCount = oldCount + countDelta;

            WriteMem(startpos + sizeOffset, BitConverter.GetBytes(newSize));
            WriteMem(startpos + countOffset, BitConverter.GetBytes(newCount));
        }


        private void UpdateMem(int? _selectedNodePos = null)
        {
            export.Data = memory.TypedClone();
            selectedNodePos = _selectedNodePos;
        }

        public void RefreshMem()
        {
            hb1.ByteProvider = new DynamicByteProvider(memory);
            //adds rootnode to list
            List<TreeNode> allNodes = treeView1.Nodes.Cast<TreeNode>().ToList();
            //flatten tree of nodes into list.
            for (int i = 0; i < allNodes.Count(); i++)
            {
                allNodes.AddRange(allNodes[i].Nodes.Cast<TreeNode>());
            }

            var expandedNodes = allNodes.Where(x => x.IsExpanded).Select(x => x.Name);
            StartScan(expandedNodes, treeView1.TopNode?.Name, selectedNodePos?.ToString());


        }

        private string CheckSeperator(string s)
        {
            string seperator = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            string wrongsep;
            if (seperator == ".")
                wrongsep = ",";
            else
                wrongsep = ".";
            return s.Replace(wrongsep, seperator);
        }

        private void expandAllButton_Click(object sender, EventArgs e)
        {
            if (treeView1 != null)
            {
                treeView1.ExpandAll();
            }
        }

        private void collapseAllButton_Click(object sender, EventArgs e)
        {
            if (treeView1 != null)

            {
                treeView1.CollapseAll();
                treeView1.Nodes[0].Expand();
            }
        }

        private void deleteArrayElement_Click(object sender, EventArgs e)
        {
            deleteArrayLeaf();
        }

        private void addArrayElementButton_Click(object sender, EventArgs e)
        {
            addArrayLeaf();
        }

        private void treeView1_AfterExpand(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Tag != null && e.Node.Tag.Equals(nodeType.ArrayProperty) && e.Node.Nodes.Count == 1)
            {
                e.Node.Nodes[0].Expand();
            }
        }

        private void proptext_KeyUp(object sender, KeyEventArgs e)
        {
            if (objectNameLabel.Visible)
            {
                int i;
                if (int.TryParse(proptext.Text, out i))
                {
                    objectNameLabel.Text = $"({pcc.getObjectName(i)})";
                }
                else
                {
                    objectNameLabel.Text = "()";
                }
            }
        }

        private void moveUpButton_Click(object sender, EventArgs e)
        {
            moveElement(true);
        }

        private void moveDownButton_Click(object sender, EventArgs e)
        {
            moveElement(false);
        }

        private void moveElement(bool up)
        {
            if (hb1.SelectionStart != lastSetOffset)
            {
                return;//user manually moved cursor
            }
            int pos;
            TreeNode node;
            TreeNode parent = LAST_SELECTED_NODE.Parent;
            if (up)
            {
                node = LAST_SELECTED_NODE.PrevNode;
                pos = getPosFromNode(node.Name);
            }
            else
            {
                node = LAST_SELECTED_NODE.NextNode;
                pos = getPosFromNode(node.Name);
                //account for structs not neccesarily being the same size
                if (node.Nodes.Count > 0)
                {
                    //position of element being moved down + size of struct below it
                    pos = lastSetOffset + (getPosFromNode(node.LastNode.Name) + 8 - pos);
                }
            }
            byte[] element = deleteArrayLeaf();
            List<byte> memList = memory.ToList();
            memList.InsertRange(pos, element);
            memory = memList.ToArray();
            //bubble up size
            bool firstbubble = true;
            int parentOffset;
            while (parent != null && (parent.Tag.Equals(nodeType.StructProperty) || parent.Tag.Equals(nodeType.ArrayProperty) || parent.Tag.Equals(nodeType.ArrayLeafStruct)))
            {
                if ((nodeType)parent.Tag == nodeType.ArrayLeafStruct)
                {
                    parent = parent.Parent;
                    continue;
                }
                parentOffset = getPosFromNode(parent.Name);
                if (firstbubble)
                {
                    firstbubble = false;
                    updateArrayLength(parentOffset, 1, element.Length);
                }
                else
                {
                    updateArrayLength(parentOffset, 0, element.Length);
                }
                parent = parent.Parent;
            }
            if (node.Nodes.Count > 0)
            {
                UpdateMem(-pos);
            }
            else
            {
                UpdateMem(pos);
            }
        }

        private void addPropButton_Click(object sender, EventArgs e)
        {
            List<string> props = PropertyReader.getPropList(export).Select(x => pcc.getNameEntry(x.Name)).ToList();
            string prop = AddPropertyDialog.GetProperty(className, props, pcc.Game);
            AddProperty(prop);
        }

        public void AddProperty(string prop)
        {
            if (prop != null)
            {
                PropertyInfo info = GetPropertyInfo(prop, className);
                if (info.type == PropertyType.StructProperty && pcc.Game != MEGame.ME3)
                {
                    MessageBox.Show("Cannot add StructProperties when editing ME1 or ME2 files.", "Sorry :(");
                    return;
                }
                List<byte> buff = new List<byte>();
                //name
                buff.AddRange(BitConverter.GetBytes(pcc.FindNameOrAdd(prop)));
                buff.AddRange(new byte[4]);
                //type
                buff.AddRange(BitConverter.GetBytes(pcc.FindNameOrAdd(info.type.ToString())));
                buff.AddRange(new byte[4]);

                switch (info.type)
                {
                    case PropertyType.IntProperty:
                    case PropertyType.StringRefProperty:
                    case PropertyType.FloatProperty:
                    case PropertyType.ObjectProperty:
                    case PropertyType.ArrayProperty:
                        //size
                        buff.AddRange(BitConverter.GetBytes(4));
                        buff.AddRange(new byte[4]);
                        //value
                        buff.AddRange(BitConverter.GetBytes(0));
                        break;
                    case PropertyType.NameProperty:
                        //size
                        buff.AddRange(BitConverter.GetBytes(8));
                        buff.AddRange(new byte[4]);
                        //value
                        buff.AddRange(BitConverter.GetBytes(pcc.FindNameOrAdd("None")));
                        buff.AddRange(BitConverter.GetBytes(0));
                        break;
                    case PropertyType.BoolProperty:
                        //size
                        buff.AddRange(BitConverter.GetBytes(0));
                        buff.AddRange(new byte[4]);
                        //value
                        if (pcc.Game == MEGame.ME3)
                        {
                            buff.Add(0);
                        }
                        else
                        {
                            buff.AddRange(new byte[4]);
                        }
                        break;
                    case PropertyType.StrProperty:
                        //size
                        buff.AddRange(BitConverter.GetBytes(6));
                        buff.AddRange(new byte[4]);
                        //value
                        if (pcc.Game == MEGame.ME3)
                        {
                            buff.AddRange(BitConverter.GetBytes(-1));
                            buff.Add(0);
                        }
                        else
                        {
                            buff.AddRange(BitConverter.GetBytes(1));
                        }
                        buff.Add(0);
                        break;
                    case PropertyType.DelegateProperty:
                        //size
                        buff.AddRange(BitConverter.GetBytes(12));
                        buff.AddRange(new byte[4]);
                        //value
                        buff.AddRange(BitConverter.GetBytes(0));
                        buff.AddRange(BitConverter.GetBytes(0));
                        buff.AddRange(BitConverter.GetBytes(0));
                        break;
                    case PropertyType.ByteProperty:
                        if (info.reference == null)
                        {
                            //size
                            buff.AddRange(BitConverter.GetBytes(1));
                            buff.AddRange(new byte[4]);
                            if (pcc.Game == MEGame.ME3)
                            {
                                //enum Type
                                buff.AddRange(BitConverter.GetBytes(pcc.FindNameOrAdd("None")));
                                buff.AddRange(new byte[4]);
                            }
                            //value
                            buff.Add(0);
                        }
                        else
                        {
                            //size
                            buff.AddRange(BitConverter.GetBytes(8));
                            buff.AddRange(new byte[4]);
                            if (pcc.Game == MEGame.ME3)
                            {
                                //enum Type
                                buff.AddRange(BitConverter.GetBytes(pcc.FindNameOrAdd(info.reference)));
                                buff.AddRange(new byte[4]);
                            }
                            //value
                            buff.AddRange(BitConverter.GetBytes(pcc.FindNameOrAdd("None")));
                            buff.AddRange(new byte[4]);
                        }
                        break;
                    case PropertyType.StructProperty:
                        byte[] structBuff = ME3UnrealObjectInfo.getDefaultClassValue(pcc as ME3Package, info.reference);
                        if (structBuff == null)
                        {
                            return;
                        }
                        //size
                        buff.AddRange(BitConverter.GetBytes(structBuff.Length));
                        buff.AddRange(new byte[4]);
                        //struct Type
                        buff.AddRange(BitConverter.GetBytes(pcc.FindNameOrAdd(info.reference)));
                        buff.AddRange(new byte[4]);
                        //value
                        buff.AddRange(structBuff);
                        break;
                    default:
                        return;
                }
                int pos = getPosFromNode(treeView1.Nodes[0].LastNode.Name);
                List<byte> memlist = memory.ToList();
                memlist.InsertRange(pos, buff);
                memory = memlist.ToArray();
                UpdateMem(pos);
            }
        }

        private void splitContainer1_SplitterMoving(object sender, SplitterCancelEventArgs e)
        {
            //a hack to set max width for SplitContainer1
            splitContainer1.Panel2MinSize = splitContainer1.Width - HEXBOX_MAX_WIDTH;
        }

        private void toggleHexWidthButton_Click(object sender, EventArgs e)
        {
            if (splitContainer1.SplitterDistance > splitContainer1.Panel1MinSize)
            {
                splitContainer1.SplitterDistance = splitContainer1.Panel1MinSize;
            }
            else
            {
                splitContainer1.SplitterDistance = HEXBOX_MAX_WIDTH;
            }
        }

        private void hb1_SelectionChanged(object sender, EventArgs e)
        {
            int start = (int)hb1.SelectionStart;
            int len = (int)hb1.SelectionLength;
            int size = (int)hb1.ByteProvider.Length;
            try
            {
                if (memory != null && start != -1 && start + len < size)
                {
                    string s = $"Byte: {memory[start]}"; //if selection is same as size this will crash.
                    if (start <= memory.Length - 4)
                    {
                        s += $", Int: {BitConverter.ToInt32(memory, start)}";
                    }
                    s += $" | Start=0x{start.ToString("X8")} ";
                    if (len > 0)
                    {
                        s += $"Length=0x{len.ToString("X8")} ";
                        s += $"End=0x{(start + len - 1).ToString("X8")}";
                    }
                    selectionStatus.Text = s;
                }
                else
                {
                    selectionStatus.Text = "Nothing Selected";
                }
            }
            catch (Exception)
            {
            }
        }

        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                treeView1.SelectedNode = e.Node;
                if (e.Node.Nodes.Count != 0)
                {
                    nodeContextMenuStrip1.Show(MousePosition);
                }
            }
        }

        private void expandAllChildrenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            treeView1.SelectedNode.ExpandAll();
        }

        private void collapseAllChildrenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            treeView1.SelectedNode.Collapse(false);
        }

        private int getPosFromNode(TreeNode t)
        {
            return getPosFromNode(t.Name);
        }

        private int getPosFromNode(string s)
        {
            return Math.Abs(Convert.ToInt32(s));
        }

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
                    return ME3UnrealObjectInfo.getPropertyInfo(className, pcc.getNameEntry(propName));
            }
            return null;
        }

        private PropertyInfo GetPropertyInfo(string propname, string typeName, bool inStruct = false)
        {
            switch (pcc.Game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getPropertyInfo(typeName, propname, inStruct);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getPropertyInfo(typeName, propname, inStruct);
                case MEGame.ME3:
                    return ME3UnrealObjectInfo.getPropertyInfo(typeName, propname, inStruct);
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
                    return ME1UnrealObjectInfo.getArrayType(typeName, pcc.getNameEntry(propName));
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getArrayType(typeName, pcc.getNameEntry(propName));
                case MEGame.ME3:
                    return ME3UnrealObjectInfo.getArrayType(typeName, pcc.getNameEntry(propName));
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
                    return ME3UnrealObjectInfo.getEnumValues(enumName, true);
            }
            return null;
        }
        #endregion

        private void reorderArrayToolStripMenuItem_Click(object sender, EventArgs e)
        {

            int off = getPosFromNode(treeView1.SelectedNode);
            int n = BitConverter.ToInt32(memory, off);
            string name = pcc.getNameEntry(n);

            ArrayProperty<ObjectProperty> prop = export.GetProperty<ArrayProperty<ObjectProperty>>(name);
            if (prop != null)
            {
                List<string> itemsToSort = new List<string>();
                foreach (ObjectProperty op in prop)
                {
                    int value = op.Value;
                    IEntry entry = pcc.getEntry(value);
                    itemsToSort.Add(entry.PackageFullName + "." + entry.ObjectName);
                }
                itemsToSort.Sort();
                string str = "";
                foreach (string item in itemsToSort)
                {
                    str += item;
                    str += "\n";
                }


                Clipboard.SetText(str);
            }
        }

        private void setValueKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)

            {
                // Then Do your Thang
                setPropertyButton.PerformClick();
                RefreshMem();
            }
        }
    }
}
