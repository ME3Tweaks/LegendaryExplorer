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
    public partial class BinaryInterpreter : UserControl
    {
        public IMEPackage Pcc { get { return pcc; } set { pcc = value; defaultStructValues.Clear(); } }

        public int InterpreterMode { get; private set; }
        private const int INTERPRETERMODE_OBJECTS = 0;
        private const int INTERPRETERMODE_NAMES = 1;
        private const int INTERPRETERMODE_INTEGERS = 2;
        private const int INTERPRETERMODE_FLOATS = 3;
        /*
         * Objects
Names
Integers
Floats*/
        public IExportEntry export;
        public string className;
        public byte[] memory;
        public int memsize;
        //public int readerpos;

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


        private int lastSetOffset = -1; //offset set by program, used for checking if user changed since set 
        private nodeType LAST_SELECTED_PROP_TYPE = nodeType.Unknown; //last property type user selected. Will use to check the current offset for type
        private TreeNode LAST_SELECTED_NODE = null; //last selected tree node
        private const int HEXBOX_MAX_WIDTH = 650;

        private IMEPackage pcc;
        private Dictionary<string, List<PropertyReader.Property>> defaultStructValues;

        int? selectedNodePos = null;

        public static readonly string[] ParsableBinaryClasses = { "Level", "StaticMeshCollectionActor", "Class", "Material", "StaticMesh", "MaterialInstanceConstant", "StaticMeshComponent", "SkeletalMeshComponent", "SkeletalMesh", "Model", "Polys" }; //classes that have binary parse code


        public BinaryInterpreter()
        {
            InitializeComponent();
            SetTopLevel(false);
            defaultStructValues = new Dictionary<string, List<PropertyReader.Property>>();
        }

        /// <summary>
        /// DON'T USE THIS FOR NOW! Used for relinking.
        /// </summary>
        /// <param name="export">Export to scan.</param>
        public BinaryInterpreter(IMEPackage importingPCC, IExportEntry importingExport, IMEPackage destPCC, IExportEntry destExport, SortedDictionary<int, int> crossPCCReferences)
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
            //StartScan();
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
                                    Debug.WriteLine("Relink miss: " + n + " " + rootNode.Text);
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

        public void InitInterpreter()
        {
            memory = export.Data;
            memsize = memory.Length;
            DynamicByteProvider db = new DynamicByteProvider(export.Data);
            hb1.ByteProvider = db;
            className = export.ClassName;
            StartScan();
        }

        private void StartScan(string topNodeName = null, string selectedNodeName = null)
        {
            switch (className)
            {
                case "Class":
                    StartClassScan();
                    break;
                case "Level":
                    StartLevelScan();
                    break;
                case "StaticMeshCollectionActor":
                    StartStaticMeshCollectionActorScan();
                    break;
                case "Material":
                    StartMaterialScan();
                    break;
                default:
                    StartGenericScan();
                    break;
            }

            var nodes = treeView1.Nodes.Find(topNodeName, true);
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
        }

        private void StartGenericScan(string nodeNameToSelect = null)
        {
            resetPropEditingControls();
            treeView1.BeginUpdate();
            treeView1.Nodes.Clear();
            addPropButton.Visible = false;

            byte[] data = export.Data;

            int binarystart = findEndOfProps();
            //find start of class binary (end of props). This should 



            TreeNode topLevelTree = new TreeNode("0000 : " + export.ObjectName + " (Generic Scan)");
            topLevelTree.Tag = nodeType.Root;
            topLevelTree.Name = "0";
            try
            {
                int binarypos = binarystart;
                List<TreeNode> subnodes = new List<TreeNode>();

                //binarypos += 0x1C; //Skip ??? and GUID
                //int guid = BitConverter.ToInt32(data, binarypos);
                /*int num1 = BitConverter.ToInt32(data, binarypos);
                TreeNode node = new TreeNode("0x" + binarypos.ToString("X4") + " ???: " + num1.ToString());
                subnodes.Add(node);
                binarypos += 4;
                int num2 = BitConverter.ToInt32(data, binarypos);
                node = new TreeNode("0x" + binarypos.ToString("X4") + " Count: " + num2.ToString());
                subnodes.Add(node);
                binarypos += 4;
                */
                int datasize = 4;
                if (InterpreterMode == INTERPRETERMODE_NAMES)
                {
                    datasize = 8;
                }

                while (binarypos <= data.Length - datasize)
                {

                    string nodeText = "0x" + binarypos.ToString("X4") + " ";
                    TreeNode node = new TreeNode();

                    switch (InterpreterMode)
                    {
                        case INTERPRETERMODE_OBJECTS:
                            {
                                int val = BitConverter.ToInt32(data, binarypos);
                                string name = val.ToString();
                                if (val > 0 && val <= pcc.Exports.Count)
                                {
                                    IExportEntry exp = pcc.Exports[val - 1];
                                    nodeText += name + " " + exp.PackageFullName + "." + exp.ObjectName + " (" + exp.ClassName + ")";
                                }
                                else if (val < 0 && val != int.MinValue && Math.Abs(val) <= pcc.Imports.Count)
                                {
                                    int csImportVal = Math.Abs(val) - 1;
                                    ImportEntry imp = pcc.Imports[csImportVal];
                                    nodeText += name + " " + imp.PackageFullName + "." + imp.ObjectName + " (" + imp.ClassName + ")";
                                }
                                node.Tag = nodeType.StructLeafObject;
                                break;
                            }
                        case INTERPRETERMODE_NAMES:
                            {
                                int val = BitConverter.ToInt32(data, binarypos);
                                if (val > 0 && val <= pcc.Names.Count)
                                {
                                    IExportEntry exp = pcc.Exports[val - 1];
                                    nodeText += val + " \t" + pcc.getNameEntry(val);
                                }
                                else
                                {
                                    nodeText += "\t" + val;
                                }
                                node.Tag = nodeType.StructLeafName;
                                break;
                            }
                        case INTERPRETERMODE_FLOATS:
                            {
                                float val = BitConverter.ToSingle(data, binarypos);
                                nodeText += val.ToString();
                                node.Tag = nodeType.StructLeafFloat;
                                break;
                            }
                        case INTERPRETERMODE_INTEGERS:
                            {
                                int val = BitConverter.ToInt32(data, binarypos);
                                nodeText += val.ToString();
                                node.Tag = nodeType.StructLeafInt;
                                break;
                            }
                    }
                    node.Text = nodeText;
                    node.Name = binarypos.ToString();
                    subnodes.Add(node);
                    binarypos += 4;
                }
                topLevelTree.Nodes.AddRange(subnodes.ToArray());
            }
            catch (Exception ex)
            {
                topLevelTree.Nodes.Add("An error occured parsing the staticmesh: " + ex.Message);
            }
            treeView1.Nodes.Add(topLevelTree);
            treeView1.CollapseAll();
            treeView1.Nodes[0].Expand();
            TreeNode[] nodes;
            if (nodeNameToSelect != null)
            {
                nodes = treeView1.Nodes.Find(nodeNameToSelect, true);
                if (nodes.Length > 0)
                {
                    treeView1.SelectedNode = nodes[0];
                }
                else
                {
                    treeView1.SelectedNode = treeView1.Nodes[0];
                }
            }

            treeView1.EndUpdate();
            memsize = memory.Length;
        }

        private void StartStaticMeshCollectionActorScan(string nodeNameToSelect = null)
        {
            resetPropEditingControls();
            treeView1.BeginUpdate();
            treeView1.Nodes.Clear();
            addPropButton.Visible = false;
            addArrayElementButton.Visible = false;
            moveUpButton.Visible = false;
            moveDownButton.Visible = false;

            TreeNode topLevelTree = new TreeNode("0000 : " + export.ObjectName + " Binary");
            topLevelTree.Tag = nodeType.Root;
            topLevelTree.Name = "0";

            //try
            {
                byte[] data = export.Data;
                //get a list of staticmesh stuff from the props.
                int propstart = 0x4; //we're assuming as any collection build by the engine should have started with this and i doubt any users will be making their own SMAC
                int listsize = System.BitConverter.ToInt32(data, 28);

                List<IExportEntry> smacitems = new List<IExportEntry>();

                for (int i = 0; i < listsize; i++)
                {
                    int offset = (32 + i * 4);
                    //fetch exports
                    int entryval = BitConverter.ToInt32(data, offset);
                    if (entryval > 0 && entryval < pcc.ExportCount)
                    {
                        IExportEntry export = (IExportEntry)pcc.getEntry(entryval);
                        smacitems.Add(export);
                    }
                    else if (entryval == 0)
                    {
                        smacitems.Add(null);
                    }
                }

                //find start of class binary (end of props)
                int start = 0x4;
                while (start < data.Length && data.Length - 8 >= start)
                {
                    ulong nameindex = BitConverter.ToUInt64(data, start);
                    if (nameindex < (ulong)pcc.Names.Count && pcc.Names[(int)nameindex] == "None")
                    {
                        //found it
                        start += 8;
                        break;
                    }
                    else
                    {
                        start += 1;
                    }
                }

                if (data.Length - start < 4)
                {
                    TreeNode node = new TreeNode();
                    node.Tag = nodeType.Unknown;
                    node.Text = start.ToString("X4") + " Could not find end of properties (looking for none)";
                    node.Name = start.ToString();
                    topLevelTree.Nodes.Add(node);
                    treeView1.Nodes.Add(topLevelTree);
                    return;
                }

                //Lets make sure this binary is divisible by 64.
                if ((data.Length - start) % 64 != 0)
                {
                    TreeNode node = new TreeNode();
                    node.Tag = nodeType.Unknown;
                    node.Text = start.ToString("X4") + " Binary data is not divisible by 64 (" + (data.Length - start) + ")! SMCA binary data should be a length divisible by 64.";
                    node.Name = start.ToString();
                    topLevelTree.Nodes.Add(node);
                    treeView1.Nodes.Add(topLevelTree);
                    return;
                }

                int smcaindex = 0;
                while (start < data.Length && smcaindex < smacitems.Count - 1)
                {
                    TreeNode smcanode = new TreeNode();
                    smcanode.Tag = nodeType.Unknown;
                    IExportEntry assossiateddata = smacitems[smcaindex];
                    string staticmesh = "";
                    string objtext = "Null - unused data";
                    if (assossiateddata != null)
                    {
                        objtext = "[Export " + assossiateddata.Index + "] " + assossiateddata.ObjectName + "_" + assossiateddata.indexValue;

                        //find associated static mesh value for display.
                        byte[] smc_data = assossiateddata.Data;
                        int staticmeshstart = 0x4;
                        bool found = false;
                        while (staticmeshstart < smc_data.Length && smc_data.Length - 8 >= staticmeshstart)
                        {
                            ulong nameindex = BitConverter.ToUInt64(smc_data, staticmeshstart);
                            if (nameindex < (ulong)pcc.Names.Count && pcc.Names[(int)nameindex] == "StaticMesh")
                            {
                                //found it
                                found = true;
                                break;
                            }
                            else
                            {
                                staticmeshstart += 1;
                            }
                        }

                        if (found)
                        {
                            int staticmeshexp = BitConverter.ToInt32(smc_data, staticmeshstart + 0x18);
                            if (staticmeshexp > 0 && staticmeshexp < pcc.ExportCount)
                            {
                                staticmesh = pcc.getEntry(staticmeshexp).ObjectName;
                            }
                        }
                    }

                    smcanode.Text = start.ToString("X4") + " [" + smcaindex + "] " + objtext + " " + staticmesh;
                    smcanode.Name = start.ToString();
                    topLevelTree.Nodes.Add(smcanode);

                    //Read nodes
                    for (int i = 0; i < 16; i++)
                    {
                        float smcadata = BitConverter.ToSingle(data, start);
                        TreeNode node = new TreeNode();
                        node.Tag = nodeType.StructLeafFloat;
                        node.Text = start.ToString("X4");

                        string label = i.ToString();
                        switch (i)
                        {
                            case 1:
                                label = "ScalingXorY1:";
                                break;
                            case 12:
                                label = "LocX:";
                                break;
                            case 13:
                                label = "LocY:";
                                break;
                            case 14:
                                label = "LocZ:";
                                break;
                            case 15:
                                label = "CameraLayerDistance?:";
                                break;
                        }

                        node.Text += " " + label + " " + smcadata;

                        //Lookup staticmeshcomponent so we can see what this actually is without flipping
                        // export

                        node.Name = start.ToString();
                        smcanode.Nodes.Add(node);
                        start += 4;
                    }

                    smcaindex++;
                }
                treeView1.Nodes.Add(topLevelTree);
                treeView1.CollapseAll();
                topLevelTree.Expand();
                treeView1.EndUpdate();
            }
        }

        private void StartLevelScan(string nodeNameToSelect = null)
        {
            resetPropEditingControls();
            treeView1.BeginUpdate();
            treeView1.Nodes.Clear();
            addPropButton.Visible = false;

            TreeNode topLevelTree = new TreeNode("0000 : " + export.ObjectName + " Binary");
            topLevelTree.Tag = nodeType.Root;
            topLevelTree.Name = "0";
            //try
            {
                byte[] data = export.Data;

                //find start of class binary (end of props)
                int start = 0x4;
                while (start < data.Length)
                {
                    uint nameindex = BitConverter.ToUInt32(data, start);
                    if (nameindex < pcc.Names.Count && pcc.Names[(int)nameindex] == "None")
                    {
                        //found it
                        start += 8;
                        break;
                    }
                    else
                    {
                        start += 4;
                    }
                }

                //Console.WriteLine("Found start of binary at " + start.ToString("X8"));

                uint exportid = BitConverter.ToUInt32(data, start);
                start += 4;
                uint numberofitems = BitConverter.ToUInt32(data, start);
                int countoffset = start;
                TreeNode countnode = new TreeNode();
                countnode.Tag = nodeType.Unknown;
                countnode.Text = start.ToString("X4") + " Level Items List Length: " + numberofitems;
                countnode.Name = start.ToString();
                topLevelTree.Nodes.Add(countnode);


                start += 4;
                uint bioworldinfoexportid = BitConverter.ToUInt32(data, start);
                TreeNode bionode = new TreeNode();
                bionode.Tag = nodeType.StructLeafObject;
                bionode.Text = start.ToString("X4") + " BioWorldInfo Export: " + bioworldinfoexportid;
                if (bioworldinfoexportid < pcc.ExportCount && bioworldinfoexportid > 0)
                {
                    int me3expindex = (int)bioworldinfoexportid;
                    IEntry exp = pcc.getEntry(me3expindex);
                    bionode.Text += " (" + exp.PackageFullName + "." + exp.ObjectName + ")";
                }


                bionode.Name = start.ToString();
                topLevelTree.Nodes.Add(bionode);

                IExportEntry bioworldinfo = pcc.Exports[(int)bioworldinfoexportid - 1];
                if (bioworldinfo.ObjectName != "BioWorldInfo")
                {
                    TreeNode node = new TreeNode();
                    node.Tag = nodeType.Unknown;
                    node.Text = start.ToString("X4") + " Export pointer to bioworldinfo resolves to wrong export. Resolved to " + bioworldinfo.ObjectName + " as export " + bioworldinfoexportid;
                    node.Name = start.ToString();
                    topLevelTree.Nodes.Add(node);
                    treeView1.Nodes.Add(topLevelTree);
                    return;
                }

                start += 4;
                uint shouldbezero = BitConverter.ToUInt32(data, start);
                if (shouldbezero != 0)
                {
                    TreeNode node = new TreeNode();
                    node.Tag = nodeType.Unknown;
                    node.Text = start.ToString("X4") + " Export may have extra parameters not accounted for yet (did not find 0 at 0x" + start.ToString("X8") + ")";
                    node.Name = start.ToString();
                    topLevelTree.Nodes.Add(node);
                    treeView1.Nodes.Add(topLevelTree);
                    return;
                }
                start += 4;
                int itemcount = 2; //Skip bioworldinfo and Class

                while (itemcount < numberofitems)
                {
                    //get header.
                    uint itemexportid = BitConverter.ToUInt32(data, start);
                    if (itemexportid - 1 < pcc.Exports.Count)
                    {
                        IExportEntry locexp = pcc.Exports[(int)itemexportid - 1];
                        //Console.WriteLine("0x" + start.ToString("X8") + "\t0x" + itemexportid.ToString("X8") + "\t" + locexp.PackageFullName + "." + locexp.ObjectName + "_" + locexp.indexValue + " [" + (itemexportid - 1) + "]");
                        TreeNode node = new TreeNode();
                        node.Tag = nodeType.ArrayLeafObject;
                        node.Text = start.ToString("X4") + "|" + itemcount + ": " + locexp.PackageFullName + "." + locexp.ObjectName + "_" + locexp.indexValue + " [" + (itemexportid - 1) + "]";
                        node.Name = start.ToString();
                        topLevelTree.Nodes.Add(node);
                        start += 4;
                        itemcount++;
                    }
                    else
                    {
                        Console.WriteLine("0x" + start.ToString("X8") + "\t0x" + itemexportid.ToString("X8") + "\tInvalid item. Ensure the list is the correct length. (Export " + itemexportid + ")");
                        TreeNode node = new TreeNode();
                        node.Tag = nodeType.ArrayLeafObject;
                        node.Text = start.ToString("X4") + " Invalid item.Ensure the list is the correct length. (Export " + itemexportid + ")";
                        node.Name = start.ToString();
                        topLevelTree.Nodes.Add(node);
                        start += 4;
                        itemcount++;
                    }
                }

                treeView1.Nodes.Add(topLevelTree);
                treeView1.CollapseAll();
                treeView1.Nodes[0].Expand();
                TreeNode[] nodes;
                if (nodeNameToSelect != null)
                {
                    //Needs fixed up
                    nodes = topLevelTree.Nodes.Find(nodeNameToSelect, true);
                    if (nodes.Length > 0)
                    {
                        treeView1.SelectedNode = nodes[0];
                    }
                    else
                    {
                        treeView1.SelectedNode = treeView1.Nodes[0];
                    }
                }

                treeView1.EndUpdate();
                topLevelTree.Expand();
                memsize = memory.Length;

            }
            //catch (Exception e)
            //{
            //  topLevelTree.Nodes.Add("Error parsing level: " + e.Message);
            //}
        }



        private void StartClassScan(string nodeNameToSelect = null)
        {
            const int nonTableEntryCount = 2; //how many items we parse that are not part of the functions table. e.g. the count, the defaults pointer
            resetPropEditingControls();
            treeView1.BeginUpdate();
            treeView1.Nodes.Clear();
            addPropButton.Visible = false;

            TreeNode topLevelTree = new TreeNode("0000 : " + export.ObjectName);
            topLevelTree.Tag = nodeType.Root;
            topLevelTree.Name = "0";
            try
            {
                List<TreeNode> subnodes = ReadTableBackwards(export);
                subnodes.Reverse();
                for (int i = nonTableEntryCount; i < subnodes.Count; i++)
                {
                    string text = subnodes[i].Text;
                    text = (i - nonTableEntryCount) + " | " + text;
                    subnodes[i].Text = text;
                }
                topLevelTree.Nodes.AddRange(subnodes.ToArray());
            }
            catch (Exception ex)
            {
                topLevelTree.Nodes.Add("An error occured parsing the class: " + ex.Message);
            }
            treeView1.Nodes.Add(topLevelTree);
            treeView1.CollapseAll();
            treeView1.Nodes[0].Expand();
            TreeNode[] nodes;
            //if (expandedNodes != null)
            //{
            //    int memDiff = memory.Length - memsize;
            //    int selectedPos = getPosFromNode(selectedNodeName);
            //    int curPos = 0;
            //    foreach (string item in expandedNodes)
            //    {
            //        curPos = getPosFromNode(item);
            //        if (curPos > selectedPos)
            //        {
            //            curPos += memDiff;
            //        }
            //        nodes = treeView1.Nodes.Find((item[0] == '-' ? -curPos : curPos).ToString(), true);
            //        if (nodes.Length > 0)
            //        {
            //            foreach (var node in nodes)
            //            {
            //                node.Expand();
            //            }
            //        }
            //    }
            //}
            if (nodeNameToSelect != null)
            {
                nodes = treeView1.Nodes.Find(nodeNameToSelect, true);
                if (nodes.Length > 0)
                {
                    treeView1.SelectedNode = nodes[0];
                }
                else
                {
                    treeView1.SelectedNode = treeView1.Nodes[0];
                }
            }

            treeView1.EndUpdate();
            memsize = memory.Length;
        }


        private void StartMaterialScan(string nodeNameToSelect = null)
        {
            const int nonTableEntryCount = 2; //how many items we parse that are not part of the functions table. e.g. the count, the defaults pointer
            resetPropEditingControls();
            treeView1.BeginUpdate();
            treeView1.Nodes.Clear();
            addPropButton.Visible = false;

            byte[] data = export.Data;

            int binarystart = findEndOfProps();
            //find start of class binary (end of props). This should 



            TreeNode topLevelTree = new TreeNode("0000 : " + export.ObjectName);
            topLevelTree.Tag = nodeType.Root;
            topLevelTree.Name = "0";
            try
            {
                int binarypos = binarystart;
                List<TreeNode> subnodes = new List<TreeNode>();

                binarypos += 0x1C; //Skip ??? and GUID
                                   //int guid = BitConverter.ToInt32(data, binarypos);
                int num1 = BitConverter.ToInt32(data, binarypos);
                TreeNode node = new TreeNode("0x" + binarypos.ToString("X4") + " ???: " + num1.ToString());
                subnodes.Add(node);
                binarypos += 4;
                int num2 = BitConverter.ToInt32(data, binarypos);
                node = new TreeNode("0x" + binarypos.ToString("X4") + " Count: " + num2.ToString());
                subnodes.Add(node);
                binarypos += 4;

                while (binarypos <= data.Length - 4)
                {
                    int val = BitConverter.ToInt32(data, binarypos);
                    string name = val.ToString();
                    if (val > 0 && val <= pcc.Exports.Count)
                    {
                        IExportEntry exp = pcc.Exports[val - 1];
                        name += " " + exp.PackageFullName + "." + exp.ObjectName + " (" + exp.ClassName + ")";
                    }
                    else if (val < 0 && Math.Abs(val) <= pcc.Imports.Count)
                    {
                        int csImportVal = Math.Abs(val) - 1;
                        ImportEntry imp = pcc.Imports[csImportVal];
                        name += " " + imp.PackageFullName + "." + imp.ObjectName + " (" + imp.ClassName + ")";

                    }

                    node = new TreeNode("0x" + binarypos.ToString("X4") + " " + name);
                    node.Tag = nodeType.StructLeafObject;
                    node.Name = binarypos.ToString();
                    subnodes.Add(node);
                    binarypos += 4;
                }
                topLevelTree.Nodes.AddRange(subnodes.ToArray());
            }
            catch (Exception ex)
            {
                topLevelTree.Nodes.Add("An error occured parsing the material: " + ex.Message);
            }
            treeView1.Nodes.Add(topLevelTree);
            treeView1.CollapseAll();
            treeView1.Nodes[0].Expand();
            TreeNode[] nodes;
            if (nodeNameToSelect != null)
            {
                nodes = treeView1.Nodes.Find(nodeNameToSelect, true);
                if (nodes.Length > 0)
                {
                    treeView1.SelectedNode = nodes[0];
                }
                else
                {
                    treeView1.SelectedNode = treeView1.Nodes[0];
                }
            }

            treeView1.EndUpdate();
            memsize = memory.Length;
        }

        private int findEndOfProps()
        {
            int readerpos = export.GetPropertyStart();
            bool run = true;
            while (run)
            {
                PropHeader p = new PropHeader();
                if (readerpos > memory.Length || readerpos < 0)
                {
                    //nothing else to interpret.
                    run = false;
                    readerpos = -1;
                    continue;
                }
                p.name = BitConverter.ToInt32(memory, readerpos);
                if (!pcc.isName(p.name))
                    run = false;
                else
                {
                    if (pcc.getNameEntry(p.name) != "None")
                    {
                        p.type = BitConverter.ToInt32(memory, readerpos + 8);
                        nodeType type = getType(pcc.getNameEntry(p.type));
                        bool isUnknownType = type == nodeType.Unknown;
                        if (!pcc.isName(p.type) || isUnknownType)
                            run = false;
                        else
                        {
                            p.size = BitConverter.ToInt32(memory, readerpos + 16);
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
                        readerpos += 8;
                        run = false;
                    }
                }
            }
            return readerpos;
        }

        private List<TreeNode> ReadTableBackwards(IExportEntry export)
        {
            List<TreeNode> tableItems = new List<TreeNode>();

            byte[] data = export.Data;
            int endOffset = data.Length;
            int count = 0;
            endOffset -= 4; //int
            while (endOffset > 0)
            {
                int index = BitConverter.ToInt32(data, endOffset);
                if (index < 0 && -index - 1 < pcc.Imports.Count)
                {
                    //import
                    int localindex = Math.Abs(index) - 1;
                    TreeNode node = new TreeNode();
                    node.Tag = nodeType.ArrayLeafObject;
                    node.Text = "0x" + endOffset.ToString("X4") + " [I] " + pcc.Imports[localindex].PackageFullName + "." + pcc.Imports[localindex].ObjectName;
                    node.Name = endOffset.ToString();
                    tableItems.Add(node);
                }
                else if (index > 0 && index != count)
                {
                    int localindex = index - 1;
                    TreeNode node = new TreeNode();
                    node.Tag = nodeType.ArrayLeafObject;
                    node.Name = endOffset.ToString();
                    node.Text = "0x" + endOffset.ToString("X4") + " [E] " + pcc.Exports[localindex].PackageFullName + "." + pcc.Exports[localindex].ObjectName + "_" + pcc.Exports[localindex].indexValue;
                    tableItems.Add(node);
                }
                else
                {
                    //Console.WriteLine("UNPARSED INDEX: " + index);
                }
                //Console.WriteLine(index);
                if (index == count)
                {
                    {
                        TreeNode node = new TreeNode();
                        node.Tag = nodeType.StructLeafInt;
                        node.Name = endOffset.ToString();
                        node.Text = endOffset.ToString("X4") + " Class Functions Table Count";
                        tableItems.Add(node);
                    }
                    endOffset -= 4;
                    if (endOffset > 0)
                    {
                        TreeNode node = new TreeNode();
                        node.Tag = nodeType.StructLeafObject;
                        node.Name = endOffset.ToString();
                        string defaults = "";
                        int defaultsindex = BitConverter.ToInt32(data, endOffset);
                        if (defaultsindex < 0 && -index - 1 < pcc.Imports.Count)
                        {
                            defaultsindex = Math.Abs(defaultsindex) - 1;
                            defaults = pcc.Imports[defaultsindex].PackageFullName + "." + pcc.Imports[defaultsindex].ObjectName;
                        }
                        else if (defaultsindex > 0 && defaultsindex - 1 < pcc.Exports.Count)
                        {
                            defaults = pcc.Exports[defaultsindex - 1].PackageFullName + "." + pcc.Exports[defaultsindex - 1].ObjectName;
                        }

                        node.Text = endOffset.ToString("X4") + " Class Defaults | " + defaults;
                        tableItems.Add(node);
                    }
                    //Console.WriteLine("FOUND START OF LIST AT 0x" + endOffset.ToString("X8") + ", items: " + index);
                    break;
                }
                endOffset -= 4;
                count++;
            }

            //Console.WriteLine("Number of items processed: " + count);
            return tableItems;
        }

        public new void Show()
        {
            base.Show();
            //StartScan();
        }

        public nodeType getType(string s)
        {
            int ret = -1;
            for (int i = 0; i < Types.Length; i++)
                if (s == Types[i])
                    ret = i;
            return (nodeType)ret;
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
            catch (Exception ep)
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
            //UpdateMem();
            RefreshMem();
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

        private void deleteElement()
        {
            try
            {
                int pos = (int)hb1.SelectionStart;
                if (hb1.SelectionStart != lastSetOffset)
                {
                    return; //user manually moved cursor
                }


                int size; //num bytes to delete at pos
                switch (className)
                {
                    case "Level":
                        size = 4;
                        int offset = getPosFromNode(LAST_SELECTED_NODE.Name);
                        int start = 0x4;
                        while (start < export.Data.Length)
                        {
                            uint nameindex = BitConverter.ToUInt32(export.Data, start);
                            if (nameindex < pcc.Names.Count && pcc.Names[(int)nameindex] == "None")
                            {
                                //found it
                                start += 8;
                                break;
                            }
                            else
                            {
                                start += 4;
                            }
                        }

                        //Console.WriteLine("Found start of binary at " + start.ToString("X8"));

                        uint exportid = BitConverter.ToUInt32(export.Data, start);
                        start += 4;
                        uint numberofitems = BitConverter.ToUInt32(export.Data, start);
                        numberofitems--;
                        WriteMem(start, BitConverter.GetBytes(numberofitems));
                        //Debug.WriteLine("Size before: " + memory.Length);
                        memory = RemoveIndices(memory, offset, size);
                        //Debug.WriteLine("Size after: " + memory.Length);

                        UpdateMem();
                        RefreshMem();
                        break;
                    case "Class":
                        size = 4;
                        break;


                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
        }

        private void addArrayLeaf()
        {

            int pos = (int)hb1.SelectionStart;
            if (hb1.SelectionStart != lastSetOffset)
            {
                return; //user manually moved cursor
            }

            try
            {
                //int size; //num bytes to delete at pos
                switch (className)
                {
                    case "Level":
                        int i = -1;
                        if (!int.TryParse(proptext.Text, out i))
                        {
                            return; //not valid element
                        }
                        int start = 0x4;
                        while (start < export.Data.Length)
                        {
                            uint nameindex = BitConverter.ToUInt32(export.Data, start);
                            if (nameindex < pcc.Names.Count && pcc.Names[(int)nameindex] == "None")
                            {
                                //found it
                                start += 8;
                                break;
                            }
                            else
                            {
                                start += 4;
                            }
                        }

                        //Console.WriteLine("Found start of binary at " + start.ToString("X8"));

                        uint exportid = BitConverter.ToUInt32(export.Data, start);
                        start += 4;
                        uint numberofitems = BitConverter.ToUInt32(export.Data, start);
                        numberofitems++;
                        WriteMem(start, BitConverter.GetBytes(numberofitems));
                        //Debug.WriteLine("Size before: " + memory.Length);
                        //memory = RemoveIndices(memory, offset, size);
                        int offset = (int)(start + numberofitems * 4); //will be at the very end of the list as it is now +1
                        List<byte> memList = memory.ToList();
                        memList.InsertRange(offset, BitConverter.GetBytes(i));
                        memory = memList.ToArray();
                        //export.Data = memory.TypedClone();
                        UpdateMem(offset);
                        break;
                    case "Class":
                        //size = 4;
                        break;
                }
                RefreshMem();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
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
            StartScan(treeView1.TopNode?.Name, selectedNodePos?.ToString());

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

        private void deleteElement_Click(object sender, EventArgs e)
        {
            deleteElement();
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
            byte[] element = new byte[0]; //PLACEHOLDER!
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
                if (memory != null && start != -1 && start + len <= size)
                {
                    string s = $"Byte: {memory[start]}";
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

        private void FindButton_Click(object sender, EventArgs e)
        {
            TreeNodeCollection collect = treeView1.Nodes;
            if (collect.Count > 0)
            {
                collect = collect[0].Nodes;
            }
            string searchtext = findBox.Text;

            foreach (TreeNode node in collect)
            {
                if (node.Text.Contains(searchtext))
                {
                    treeView1.SelectedNode = node;
                    break;
                }
            }
        }

        private void findButton_Pressed(object sender, KeyPressEventArgs e)
        {
            if (this.findBox.Focused && e.KeyChar == '\r')
            {
                // click the Go button
                this.findButton.PerformClick();
                // don't allow the Enter key to pass to textbox
                e.Handled = true;
            }

        }

        private void viewModeChanged(object sender, EventArgs e)
        {
            InterpreterMode = ((ToolStripComboBox)sender).SelectedIndex;
            if (memory != null)
            {
                RefreshMem();
            }
        }

        private void saveHexButton_Click(object sender, EventArgs e)
        {

        }
    }
}
