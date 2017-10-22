using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using ME3Explorer.Packages;
using ME3Explorer.PathfindingNodes;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Nodes;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.PathingGraphEditor;

using Newtonsoft.Json;
using KFreonLib.MEDirectories;
using Gibbed.IO;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using ME3Explorer.Pathfinding_Editor;
using ME3Explorer.ActorNodes;
using static ME3Explorer.Pathfinding_Editor.PathfindingNodeMaster;
using static ME3Explorer.BinaryInterpreter;

namespace ME3Explorer
{


    public partial class PathfindingEditor : WinFormsBase
    {
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

        private const int NODETYPE_SFXENEMYSPAWNPOINT = 0;
        private const int NODETYPE_PATHNODE = 1;
        private const int NODETYPE_SFXNAV_TURRETPOINT = 2;
        private const int NODETYPE_SFXNAV_JUMPNODE = 3;
        private const int NODETYPE_SFXNAV_BOOSTNODE_TOP = 4;
        private const int NODETYPE_SFXNAV_BOOSTNODE_BOTTOM = 5;
        private static string classDatabasePath = "";

        public static Dictionary<string, Dictionary<string, string>> importclassdb = new Dictionary<string, Dictionary<string, string>>(); //SFXGame.Default__SFXEnemySpawnPoint -> class, packagefile (can infer link and name)
        public static Dictionary<string, Dictionary<string, string>> exportclassdb = new Dictionary<string, Dictionary<string, string>>(); //SFXEnemy SpawnPoint -> class, name, ...etc

        public string[] pathfindingNodeClasses = { "PathNode", "SFXEnemySpawnPoint", "BioPathPoint", "SFXNav_TurretPoint", "CoverLink", "SFXNav_SpawnEntrance", "SFXNav_LadderNode", "SFXDoorMarker", "SFXNav_JumpNode", "SFXNav_JumpDownNode", "NavigationPoint", "CoverSlotMarker", "SFXOperation_ObjectiveSpawnPoint", "SFXNav_BoostNode", "SFXNav_LargeClimbNode", "SFXNav_LargeMantleNode", "SFXNav_ClimbWallNode", "WwiseAmbientSound" };
        public string[] actorNodeClasses = { "BlockingVolume", "StaticMeshActor", "InterpActor", "SFXDoor", "BioTriggerVolume" };

        public string[] ignoredobjectnames = { "PREFAB_Ladders_3M_Arc0", "PREFAB_Ladders_3M_Arc1" };
        public bool ActorNodesActive = false;
        public bool PathfindingNodesActive = true;
        public bool StaticMeshCollectionActorNodesActive = false;

        public PathfindingEditor()
        {
            AllowRefresh = true;
            classDatabasePath = Application.StartupPath + "//exec//pathfindingclassdb.json";
            InitializeComponent();

            graphEditor.BackColor = Color.FromArgb(167, 167, 167);
            graphEditor.Camera.MouseDown += backMouseDown_Handler;
            graphEditor.AddInputEventListener(new PathfindingMouseListener(this));
            zoomController = new PathingZoomController(graphEditor);

            SText.LoadFont();


            if (importclassdb.Count() == 0 || exportclassdb.Count() == 0)
            {

                if (File.Exists(classDatabasePath))
                {

                    string raw = File.ReadAllText(classDatabasePath);
                    JObject o = JObject.Parse(raw);
                    JToken exportjson = o.SelectToken("exporttypes");
                    JToken importjson = o.SelectToken("importtypes");
                    exportclassdb = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(exportjson.ToString());
                    importclassdb = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(importjson.ToString());
                }

            }
        }

        private void pathfindingEditor_MouseMoveHandler(object sender, MouseEventArgs e)
        {
            PointF mousePoint = e.Location;
            Debug.WriteLine(mousePoint);
        }

        private void PathfindingEditor_Load(object sender, EventArgs e)
        {
            interpreter1.hideHexBox();
        }

        private struct SaveData
        {
            public bool absoluteIndex;
            public int index;
            public float X;
            public float Y;

            public SaveData(int i) : this()
            {
                index = i;
            }
        }

        //public static readonly string OptionsPath = Path.Combine(PathfindingEditorDataFolder, "PathfindingEditorOptions.JSON");

        private ME1Explorer.TalkFiles talkFiles;
        private bool selectedByNode;
        private int selectedIndex;
        private PathingZoomController zoomController;
        public TreeNode SeqTree;
        public PropGrid pg;
        /// <summary>
        /// List of export-indices of items loaded into the pathfinding viewer
        /// </summary>
        public List<int> CurrentObjects = new List<int>();
        /// <summary>
        /// List of nodes in the graph
        /// </summary>
        public List<PathfindingNodeMaster> Objects;
        private List<SaveData> SavedPositions;
        public bool RefOrRefChild;

        public string CurrentFile;
        public string JSONpath;


        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = App.FileFilter;
            if (d.ShowDialog() == DialogResult.OK)
            {
                LoadFile(d.FileName);
            }
        }

        public void LoadFile(string fileName)
        {
            //try
            //{
            smacCoordinates = new Dictionary<int, PointF>();
            bool isFirstLoad = CurrentFile == null;
            VisibleActorCollections = new List<int>();
            LoadMEPackage(fileName);
            CurrentFile = fileName;
            filenameLabel.Text = Path.GetFileName(fileName);
            //LoadSequences();
            graphEditor.nodeLayer.RemoveAllChildren();
            graphEditor.edgeLayer.RemoveAllChildren();
            CurrentObjects.Clear();
            if (LoadPathingNodesFromLevel())
            {
                PointF graphcenter = GenerateGraph();
                if (isFirstLoad && listBox1.Items.Count > 0)
                {
                    listBox1.SelectedIndex = 0;
                }
            }
            else
            {
                CurrentFile = null;
            }
            //}
            //catch (Exception ex)
            //{
            //  MessageBox.Show("Error:\n" + ex.Message);
            //}
        }

        /* private void LoadSequences()
         {
             treeView1.Nodes.Clear();
             Dictionary<string, TreeNode> prefabs = new Dictionary<string, TreeNode>();
             for (int i = 0; i < pcc.ExportCount; i++)
             {
                 IExportEntry exportEntry = pcc.getExport(i);
                 if (exportEntry.ClassName == "Sequence" && !pcc.getObjectClass(exportEntry.idxLink).Contains("Sequence"))
                 {
                     treeView1.Nodes.Add(FindSequences(pcc, i, !(exportEntry.ObjectName == "Main_Sequence")));
                 }
                 if (exportEntry.ClassName == "Prefab")
                 {
                     prefabs.Add(exportEntry.ObjectName, new TreeNode(exportEntry.GetFullPath));
                 }
             }
             if (prefabs.Count > 0)
             {
                 for (int i = 0; i < pcc.ExportCount; i++)
                 {
                     IExportEntry exportEntry = pcc.getExport(i);
                     if (exportEntry.ClassName == "PrefabSequence" && pcc.getObjectClass(exportEntry.idxLink) == "Prefab")
                     {
                         string parentName = pcc.getObjectName(exportEntry.idxLink);
                         if (prefabs.ContainsKey(parentName))
                         {
                             prefabs[parentName].Nodes.Add(FindSequences(pcc, i, false));
                         }
                     }
                 }
                 foreach (var item in prefabs.Values)
                 {
                     if (item.Nodes.Count > 0)
                     {
                         treeView1.Nodes.Add(item);
                     }
                 }
             }
             if (treeView1.Nodes.Count == 0)
             {
                 MessageBox.Show("No Sequences found!");
                 return;
             }

             treeView1.ExpandAll();
             if (treeView1.Nodes.Count > 0)
             {
                 treeView1.TopNode = treeView1.Nodes[0];
             }
         }*/

        private bool LoadPathingNodesFromLevel()
        {
            if (pcc == null)
            {
                return false;
            }

            staticMeshCollectionActorsToolStripMenuItem.DropDownItems.Clear();
            staticMeshCollectionActorsToolStripMenuItem.Enabled = false;

            CurrentObjects = new List<int>();
            listBox1.Items.Clear();


            /*var seqObjs = export.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
            if (seqObjs != null)
            {
                var objIndices = seqObjs.Select(x => x.Value - 1).ToList();
                objIndices.Sort();
                foreach (int seqObj in objIndices)
                {
                    CurrentObjects.Add(seqObj);
                    IExportEntry exportEntry = pcc.getExport(seqObj);
                    listBox1.Items.Add("#" + seqObj + " :" + exportEntry.ObjectName + " class: " + exportEntry.ClassName);
                }
            }*/

            foreach (IExportEntry exp in pcc.Exports)
            {
                if (exp.ClassName == "Level" && exp.ObjectName == "PersistentLevel")
                {
                    //Read persistent level binary
                    byte[] data = exp.Data;

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
                    /*TreeNode countnode = new TreeNode();
                    countnode.Tag = nodeType.Unknown;
                    countnode.Text = start.ToString("X4") + " Level Items List Length: " + numberofitems;
                    countnode.Name = start.ToString();
                    topLevelTree.Nodes.Add(countnode);*/


                    start += 4;
                    uint bioworldinfoexportid = BitConverter.ToUInt32(data, start);
                    /*TreeNode bionode = new TreeNode();
                    bionode.Tag = nodeType.StructLeafObject;
                    bionode.Text = start.ToString("X4") + " BioWorldInfo Export: " + bioworldinfoexportid;
                    if (bioworldinfoexportid < pcc.ExportCount && bioworldinfoexportid > 0)
                    {
                        int me3expindex = (int)bioworldinfoexportid;
                        IEntry exp = pcc.getEntry(me3expindex);
                        bionode.Text += " (" + exp.PackageFullName + "." + exp.ObjectName + ")";
                    }


                    bionode.Name = start.ToString();
                    topLevelTree.Nodes.Add(bionode);*/

                    IExportEntry bioworldinfo = pcc.Exports[(int)bioworldinfoexportid - 1];
                    if (bioworldinfo.ObjectName != "BioWorldInfo")
                    {
                        //INVALID!!
                        /*
                        TreeNode node = new TreeNode();
                        node.Tag = nodeType.Unknown;
                        node.Text = start.ToString("X4") + " Export pointer to bioworldinfo resolves to wrong export Resolved to " + bioworldinfo.ObjectName + " as export " + bioworldinfoexportid;
                        node.Name = start.ToString();
                        topLevelTree.Nodes.Add(node);
                        treeView1.Nodes.Add(topLevelTree);*/
                        return false;
                    }

                    start += 4;
                    uint shouldbezero = BitConverter.ToUInt32(data, start);
                    if (shouldbezero != 0)
                    {
                        //INVALID!!!
                        //TreeNode node = new TreeNode();
                        //node.Tag = nodeType.Unknown;
                        //node.Text = start.ToString("X4") + " Export may have extra parameters not accounted for yet (did not find 0 at 0x" + start.ToString("X8") + ")";
                        //node.Name = start.ToString();
                        //topLevelTree.Nodes.Add(node);
                        //treeView1.Nodes.Add(topLevelTree);
                        return false;
                    }
                    start += 4;
                    int itemcount = 2; //Skip bioworldinfo and Class

                    while (itemcount < numberofitems)
                    {
                        //get header.
                        uint itemexportid = BitConverter.ToUInt32(data, start);
                        if (itemexportid - 1 < pcc.Exports.Count)
                        {
                            IExportEntry exportEntry = pcc.Exports[(int)itemexportid - 1];
                            if (ignoredobjectnames.Contains(exportEntry.ObjectName))
                            {
                                start += 4;
                                itemcount++;
                                continue;
                            }

                            if (PathfindingNodesActive)
                            {
                                if (pathfindingNodeClasses.Contains(exportEntry.ClassName))
                                {
                                    CurrentObjects.Add(exportEntry.Index);
                                    listBox1.Items.Add("#" + (exportEntry.Index) + " " + exportEntry.ObjectName + " - Class: " + exportEntry.ClassName);
                                }
                            }

                            if (ActorNodesActive)
                            {
                                if (actorNodeClasses.Contains(exportEntry.ClassName))
                                {
                                    CurrentObjects.Add(exportEntry.Index);
                                    listBox1.Items.Add("#" + (exportEntry.Index) + " " + exportEntry.ObjectName + " - Class: " + exportEntry.ClassName);
                                }
                            }

                            //if (VisibleActorCollections.Contains(exportEntry.Index))
                            //{
                            if (exportEntry.ObjectName == "StaticMeshCollectionActor")
                            {
                                ToolStripMenuItem testItem = new ToolStripMenuItem(exportEntry.Index + " " + exportEntry.ObjectName + "_" + exportEntry.indexValue);
                                testItem.Click += (object o, EventArgs args) =>
                                {
                                    staticMeshCollectionActor_ToggleVisibility(testItem, exportEntry, testItem.Checked);
                                };
                                if (VisibleActorCollections.Contains(exportEntry.Index))
                                {
                                    byte[] smacData = exportEntry.Data;
                                    testItem.Checked = true;
                                    //Make new nodes for each item...
                                    ArrayProperty<ObjectProperty> smacItems = exportEntry.GetProperty<ArrayProperty<ObjectProperty>>("StaticMeshComponents");
                                    if (smacItems != null)
                                    {
                                        int binarypos = findEndOfProps(exportEntry);

                                        //Read exports...
                                        foreach (ObjectProperty obj in smacItems)
                                        {
                                            CurrentObjects.Add(obj.Value - 1);
                                            listBox1.Items.Add("#" + (exportEntry.Index) + " " + exportEntry.ObjectName + " - Class: " + exportEntry.ClassName);

                                            //Read location and put in position map
                                            int offset = binarypos + 12 * 4;
                                            float x = BitConverter.ToSingle(smacData, offset);
                                            float y = BitConverter.ToSingle(smacData, offset + 4);
                                            Debug.WriteLine(offset.ToString("X4")+" "+x + "," + y);
                                            smacCoordinates[obj.Value - 1] = new PointF(x, y);
                                            binarypos += 64;
                                        }
                                    }
                                }
                                staticMeshCollectionActorsToolStripMenuItem.DropDown.Items.Add(testItem);
                                staticMeshCollectionActorsToolStripMenuItem.Enabled = true;
                            }
                            //}
                            start += 4;
                            itemcount++;
                        }
                        else
                        {
                            //INVALID ITEM ENCOUNTERED!
                            /*
                            Console.WriteLine("0x" + start.ToString("X8") + "\t0x" + itemexportid.ToString("X8") + "\tInvalid item. Ensure the list is the correct length. (Export " + itemexportid + ")");
                            TreeNode node = new TreeNode();
                            node.Tag = nodeType.ArrayLeafObject;
                            node.Text = start.ToString("X4") + " Invalid item.Ensure the list is the correct length. (Export " + itemexportid + ")";
                            node.Name = start.ToString();
                            topLevelTree.Nodes.Add(node);*/
                            start += 4;
                            itemcount++;
                        }
                    }



                    for (int i = 0; i < pcc.ExportCount; i++)
                    {
                        IExportEntry exportEntry = pcc.getExport(i);

                    }

                    bool oneViewActive = PathfindingNodesActive || ActorNodesActive;
                    if (oneViewActive && listBox1.Items.Count == 0)
                    {
                        MessageBox.Show("No nodes visible with current view options.\nChange view options to see if there are any viewable nodes.");
                        graphEditor.Enabled = true;
                        graphEditor.UseWaitCursor = false;
                        return true; //file still loaded.
                    }
                    selectedIndex = -1;
                    graphEditor.Enabled = true;
                    graphEditor.UseWaitCursor = false;
                    return true;

                }
            }
            //No level was found.
            return false;
        }

        private void staticMeshCollectionActor_ToggleVisibility(ToolStripMenuItem item, IExportEntry exportEntry, bool @checked)
        {
            item.Checked = !item.Checked;
            if (item.Checked)
            {
                VisibleActorCollections.Add(exportEntry.Index);
            }
            else
            {
                VisibleActorCollections.Remove(exportEntry.Index);
            }
            RefreshView();
        }

        public void RefreshView()
        {
            if (AllowRefresh)
            {
                //int selectednode = listBox1.SelectedIndex;
                graphEditor.nodeLayer.RemoveAllChildren();
                graphEditor.edgeLayer.RemoveAllChildren();
                CurrentObjects.Clear();
                LoadPathingNodesFromLevel();
                GenerateGraph();
                //listBox1.SelectedIndex = selectednode;
            }
        }

        public PointF GenerateGraph()
        {
            graphEditor.nodeLayer.RemoveAllChildren();
            graphEditor.edgeLayer.RemoveAllChildren();
            StartPosEvents = 0;
            StartPosActions = 0;
            StartPosVars = 0;
            PointF centerpoint = new PointF(0, 0);
            Objects = new List<PathfindingNodeMaster>();
            if (CurrentObjects != null)
            {
                //  double fullx = 0;
                //  double fully = 0;
                int currentcount = CurrentObjects.Count(); //Some objects load additional objects. We will process them after.
                for (int i = 0; i < currentcount; i++)
                {
                    PointF pos = LoadObject(CurrentObjects[i]);
                    //fullx += pos.X;
                    //fully += pos.Y;
                }
                CreateConnections();
            }
            foreach (PNode o in Objects)
            {
                o.MouseDown += node_MouseDown;
            }
            return centerpoint;
            /*if (SavedPositions.Count == 0 && CurrentFile.Contains("_LOC_INT") && pcc.Game != MEGame.ME1)
            {
                LoadDialogueObjects();
            }*/
        }

        public float StartPosEvents;
        public float StartPosActions;
        public float StartPosVars;
        private Dictionary<int, PointF> smacCoordinates;

        public PointF LoadObject(int index)
        {
            PointF smacPos;
            bool found = smacCoordinates.TryGetValue(index, out smacPos);
            if (found)
            {
                SMAC_ActorNode smac = new SMAC_ActorNode(index, smacPos.X, smacPos.Y, pcc, graphEditor);
                Objects.Add(smac);
                return smacPos;
            }
            else
            {
                string s = pcc.getExport(index).ObjectName;
                int x = 0, y = 0;
                //SaveData savedInfo = new SaveData(-1);
                IExportEntry exporttoLoad = pcc.getExport(index);
                StructProperty prop = exporttoLoad.GetProperty<StructProperty>("location");
                if (prop != null)
                {
                    PropertyCollection nodelocprops = (prop as StructProperty).Properties;
                    //X offset is 0x20
                    //Y offset is 0x24
                    //Z offset is 0x28 (unused as this is a 2D graph)
                    foreach (var locprop in nodelocprops)
                    {
                        switch (locprop.Name)
                        {
                            case "X":
                                x = Convert.ToInt32((locprop as FloatProperty).Value);
                                break;
                            case "Y":
                                y = Convert.ToInt32((locprop as FloatProperty).Value);
                                break;
                        }
                    }

                    IExportEntry export = pcc.getExport(index);
                    if (pathfindingNodeClasses.Contains(export.ClassName))
                    {
                        PathfindingNode pathNode = null;
                        switch (export.ClassName)
                        {
                            case "PathNode":
                                pathNode = new PathfindingNodes.PathNode(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXEnemySpawnPoint":
                                pathNode = new PathfindingNodes.SFXEnemySpawnPoint(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXNav_JumpNode":
                                pathNode = new PathfindingNodes.SFXNav_JumpNode(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXDoorMarker":
                                pathNode = new PathfindingNodes.SFXDoorMarker(index, x, y, pcc, graphEditor);
                                break;
                            
                            case "BioPathPoint":
                                pathNode = new PathfindingNodes.BioPathPoint(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXNav_TurretPoint":
                                pathNode = new PathfindingNodes.SFXNav_TurretPoint(index, x, y, pcc, graphEditor);
                                break;
                            case "WwiseAmbientSound":
                                pathNode = new PathfindingNodes.WwiseAmbientSound(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXOperation_ObjectiveSpawnPoint":
                                pathNode = new PathfindingNodes.SFXObjectiveSpawnPoint(index, x, y, pcc, graphEditor);

                                //Create annex node if required
                                var annexZoneLocProp = export.GetProperty<ObjectProperty>("AnnexZoneLocation");
                                if (annexZoneLocProp != null)
                                {
                                    IExportEntry annexzonelocexp = pcc.Exports[annexZoneLocProp.Value - 1];

                                    PropertyCollection annexzoneprops = annexzonelocexp.GetProperties();
                                    foreach (var annexprop in annexzoneprops)
                                    {
                                        if (annexprop.Name == "location")
                                        {
                                            PropertyCollection sublocprops = (annexprop as StructProperty).Properties;
                                            int locx = 0;
                                            int locy = 0;
                                            foreach (var locprop in sublocprops)
                                            {
                                                switch (locprop.Name)
                                                {
                                                    case "X":
                                                        locx = Convert.ToInt32((locprop as FloatProperty).Value);
                                                        break;
                                                    case "Y":
                                                        locy = Convert.ToInt32((locprop as FloatProperty).Value);
                                                        break;
                                                }
                                            }

                                            AnnexNode annexNode = new PathfindingNodes.AnnexNode(annexzonelocexp.Index, locx, locy, pcc, graphEditor);
                                            Objects.Add(annexNode); //this might cause concurrentmodificationexception...
                                            listBox1.Items.Add("#" + (annexzonelocexp.Index) + " " + annexzonelocexp.ObjectName + " class: " + annexzonelocexp.ClassName);
                                            //annexNode.MouseDown += node_MouseDown;
                                            CurrentObjects.Add(annexzonelocexp.Index); //this might cause concurrentmodificationexception...
                                            break;
                                        }
                                    }
                                }

                                break;
                            case "SFXNav_BoostNode":
                                pathNode = new PathfindingNodes.SFXNav_BoostNode(index, x, y, pcc, graphEditor);
                                break;
                            default:
                                pathNode = new PathfindingNodes.PendingNode(index, x, y, pcc, graphEditor);
                                break;
                        }
                        Objects.Add(pathNode);
                        return new PointF(x, y);
                    } //End if Pathnode Class

                    if (actorNodeClasses.Contains(export.ClassName))
                    {
                        ActorNode actorNode = null;
                        switch (export.ClassName)
                        {
                            case "BlockingVolume":
                                actorNode = new BlockingVolumeNode(index, x, y, pcc, graphEditor);
                                break;
                            case "InterpActor":
                                actorNode = new InterpActorNode(index, x, y, pcc, graphEditor);
                                break;
                            case "BioTriggerVolume":
                                actorNode = new ActorNodes.BioTriggerVolume(index, x, y, pcc, graphEditor);
                                break;
                            default:
                                actorNode = new PendingActorNode(index, x, y, pcc, graphEditor);
                                break;
                        }
                        Objects.Add(actorNode);
                        return new PointF(x, y);
                    }
                }
            }
            return new PointF(0, 0); //hopefully won't see you
        }

        public void CreateConnections()
        {
            if (Objects != null && Objects.Count != 0)
            {
                for (int i = 0; i < Objects.Count; i++)
                {
                    graphEditor.addNode(Objects[i]);
                }
                foreach (PathfindingNodeMaster o in graphEditor.nodeLayer)
                {
                    o.CreateConnections(ref Objects);
                }

                foreach (PPath edge in graphEditor.edgeLayer)
                {
                    PathingGraphEditor.UpdateEdgeStraight(edge);
                }
            }
        }

        public void GetProperties(IExportEntry export)
        {
            List<PropertyReader.Property> p;
            switch (export.ClassName)
            {
                default:
                    p = PropertyReader.getPropList(export);
                    break;
            }
            pg = new PropGrid();
            pg1.SelectedObject = pg;
            for (int l = 0; l < p.Count; l++)
                pg.Add(PropertyReader.PropertyToGrid(p[l], pcc));
            pg1.Refresh();
            if (interpreter1.Pcc != export.FileRef)
            {
                interpreter1.Pcc = export.FileRef; //allows cross file references to work.
            }
            interpreter1.export = export;
            interpreter1.InitInterpreter();
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1 || n < 0 || n >= CurrentObjects.Count())
                return;
            PathfindingNodeMaster s = Objects.FirstOrDefault(o => o.Index == CurrentObjects[n]);
            if (s != null)
            {
                if (selectedIndex != -1)
                {
                    PathfindingNodeMaster d = Objects.FirstOrDefault(o => o.Index == CurrentObjects[selectedIndex]);
                    if (d != null)
                        d.Deselect();
                }
                s.Select();
                if (!selectedByNode)
                    graphEditor.Camera.AnimateViewToPanToBounds(s.GlobalFullBounds, 0);
            }
            GetProperties(pcc.getExport(CurrentObjects[n]));
            selectedIndex = n;
            selectedByNode = false;
            graphEditor.Refresh();
        }

        private void PathfindingEditor_FormClosing(object sender, FormClosingEventArgs e)
        {

        }

        private void saveImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CurrentObjects.Count == 0)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "Bmp Files (*.bmp)|*.bmp";
            if (d.ShowDialog() == DialogResult.OK)
            {
                PNode r = graphEditor.Root;
                RectangleF rr = r.GlobalFullBounds;
                PNode p = PPath.CreateRectangle(rr.X, rr.Y, rr.Width, rr.Height);
                p.Brush = Brushes.White;
                graphEditor.addBack(p);
                graphEditor.Camera.Visible = false;
                Image image = graphEditor.Root.ToImage();
                graphEditor.Camera.Visible = true;
                image.Save(d.FileName, ImageFormat.Bmp);
                graphEditor.backLayer.RemoveAllChildren();
                MessageBox.Show("Done.");
            }
        }

        protected void node_MouseDown(object sender, PInputEventArgs e)
        {
            int n = ((PathfindingNodeMaster)sender).Index;
            int selected = CurrentObjects.IndexOf(n);
            if (selected == -1)
                return;
            selectedByNode = true;
            listBox1.SelectedIndex = selected;
            if (e.Button == MouseButtons.Right)
            {
                breakLinksToolStripMenuItem.DropDownItems.Clear();



                PathfindingNodeMaster node = (PathfindingNodeMaster)sender;

                if (node is PathfindingNode)
                {
                    rightMouseButtonMenu.Show(MousePosition);
                    //open in InterpEditor
                    string className = pcc.getExport(((PathfindingNodes.PathfindingNode)sender).Index).ClassName;
                    //break links
                    breakLinksToolStripMenuItem.Enabled = false;
                    breakLinksToolStripMenuItem.DropDown = null;
                    ToolStripMenuItem breaklLinkItem;
                    ToolStripDropDown submenu = new ToolStripDropDown();
                    IExportEntry nodeExport = pcc.Exports[n];//
                    ArrayProperty<ObjectProperty> PathList = nodeExport.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                    if (PathList != null)
                    {
                        foreach (ObjectProperty prop in PathList)
                        {
                            IExportEntry outgoingSpec = pcc.Exports[prop.Value - 1];
                            StructProperty outgoingEndStructProp = outgoingSpec.GetProperty<StructProperty>("End"); //Embeds END
                            ObjectProperty outgoingSpecEndProp = outgoingEndStructProp.Properties.GetProp<ObjectProperty>("Actor"); //END                    


                            breaklLinkItem = new ToolStripMenuItem("Break reachspec to " + (outgoingSpecEndProp.Value - 1));
                            breaklLinkItem.Click += (object o, EventArgs args) =>
                            {
                                removeReachSpec(nodeExport, outgoingSpec);
                            };
                            submenu.Items.Add(breaklLinkItem);
                        }

                        breakLinksToolStripMenuItem.Enabled = true;
                        breakLinksToolStripMenuItem.DropDown = submenu;
                    }
                }

                //    for (int i = 0; i < sBox.Outlinks.Count; i++)
                //    {
                //        for (int j = 0; j < sBox.Outlinks[i].Links.Count; j++)
                //        {
                //            if (sBox.Outlinks[i].Links[j] != -1)
                //            {
                //                temp = new ToolStripMenuItem("Break link from " + sBox.Outlinks[i].Desc + " to " + sBox.Outlinks[i].Links[j] + " :" + sBox.Outlinks[i].InputIndices[j]);
                //                int linkConnection = i;
                //                int linkIndex = j;
                //                temp.Click += (object o, EventArgs args) =>
                //                {
                //                    sBox.RemoveOutlink(linkConnection, linkIndex);
                //                };
                //                outLinkMenu.Items.Add(temp);
                //            }
                //        }
                //    }
                //    if(varLinkMenu.Items.Count > 0)
                //    {
                //        temp = new ToolStripMenuItem("Variable Links");
                //        temp.DropDown = varLinkMenu;
                //        submenu.Items.Add(temp);
                //    }
                //    if (outLinkMenu.Items.Count > 0)
                //    {
                //        temp = new ToolStripMenuItem("Output Links");
                //        temp.DropDown = outLinkMenu;
                //        submenu.Items.Add(temp);
                //    }
                //    if (submenu.Items.Count > 0)
                //    {
                //        temp = new ToolStripMenuItem("Break all Links");
                //        temp.Click += removeAllLinks_handler;
                //        temp.Tag = sender;
                //        submenu.Items.Add(temp);
                //        breakLinksToolStripMenuItem.Enabled = true;
                //        breakLinksToolStripMenuItem.DropDown = submenu;
                //    }
                //}
            }
        }

        private IExportEntry removeReachSpec(IExportEntry nodeExport, IExportEntry outgoingSpec)
        {
            ArrayProperty<ObjectProperty> PathList = nodeExport.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
            for (int i = 0; i < PathList.Count; i++)
            {
                ObjectProperty prop = PathList[i];
                if (prop.Value == outgoingSpec.UIndex)
                {
                    PathList.RemoveAt(i);
                    nodeExport.WriteProperty(PathList);
                    break;
                }
            }

            return nodeExport;
        }

        public bool AllowRefresh { get; private set; }
        public List<int> VisibleActorCollections { get; private set; }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            graphEditor.ScaleViewTo((float)Convert.ToDecimal(toolStripTextBox1.Text));
        }

        private void openInPackageEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int l = CurrentObjects[listBox1.SelectedIndex];
            if (l == -1)
                return;
            PackageEditor p = new PackageEditor();
            p.Show();
            p.LoadFile(CurrentFile);
            p.goToNumber(l);
        }

        private void pg1_PropertyValueChanged(object o, PropertyValueChangedEventArgs e)
        {

            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            PropGrid.propGridPropertyValueChanged(e, CurrentObjects[n], pcc);
        }

        private void togglePathfindingNodes_Click(object sender, EventArgs e)
        {
            PathfindingNodesActive = togglePathfindingNodes.Checked;
            RefreshView();

        }

        private void useGlobalSequenceRefSavesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CurrentObjects.Count == 0)
                return;
        }

        private void graphEditor_Click(object sender, EventArgs e)
        {
            graphEditor.Focus();
        }

        private void PathfindingEditor_DragDrop(object sender, DragEventArgs e)
        {
            List<string> DroppedFiles = ((string[])e.Data.GetData(DataFormats.FileDrop)).ToList();
            if (DroppedFiles.Count > 0)
            {
                LoadFile(DroppedFiles[0]);
            }
        }

        private void PathfindingEditor_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.All;
            else
                e.Effect = DragDropEffects.None;
        }

        private void savePccToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            pcc.save();
            MessageBox.Show("Done");
        }

        private void savePCCAsMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            string extension = Path.GetExtension(pcc.FileName);
            d.Filter = $"*{extension}|*{extension}";
            if (d.ShowDialog() == DialogResult.OK)
            {
                pcc.save(d.FileName);
                MessageBox.Show("Done");
            }
        }

        private void backMouseDown_Handler(object sender, PInputEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                contextMenuStrip2.Show(MousePosition);
            }
        }

        void generateNewRandomGUID(IExportEntry export)
        {
            StructProperty guidProp = export.GetProperty<StructProperty>("NavGuid");
            if (guidProp != null)
            {
                Random rnd = new Random();
                IntProperty A = guidProp.GetProp<IntProperty>("A");
                IntProperty B = guidProp.GetProp<IntProperty>("B");
                IntProperty C = guidProp.GetProp<IntProperty>("C");
                IntProperty D = guidProp.GetProp<IntProperty>("D");
                A.Value = rnd.Next();
                B.Value = rnd.Next();
                C.Value = rnd.Next();
                D.Value = rnd.Next();
                export.WriteProperty(guidProp);
            }
        }

        void cloneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = CurrentObjects[listBox1.SelectedIndex];
            if (n == -1)
                return;

            AllowRefresh = false;
            IExportEntry nodeEntry = pcc.Exports[n];
            ObjectProperty collisionComponentProperty = nodeEntry.GetProperty<ObjectProperty>("CollisionComponent");
            IExportEntry collisionEntry = pcc.Exports[collisionComponentProperty.Value];

            int newNodeIndex = pcc.Exports.Count;
            int newCollisionIndex = newNodeIndex + 1;

            pcc.addExport(nodeEntry.Clone());
            pcc.addExport(collisionEntry.Clone());

            IExportEntry newNodeEntry = pcc.Exports[newNodeIndex];
            IExportEntry newCollisionEntry = pcc.Exports[newCollisionIndex];

            //empty the pathlist
            ArrayProperty<ObjectProperty> PathList = newNodeEntry.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
            PathList.Clear();
            newNodeEntry.WriteProperty(PathList);

            //reuse
            collisionComponentProperty = newNodeEntry.GetProperty<ObjectProperty>("CollisionComponent");
            collisionComponentProperty.Value = newCollisionEntry.UIndex;
            newNodeEntry.WriteProperty(collisionComponentProperty);

            collisionComponentProperty = newNodeEntry.GetProperty<ObjectProperty>("CylinderComponent");
            collisionComponentProperty.Value = newCollisionEntry.UIndex;
            newNodeEntry.WriteProperty(collisionComponentProperty);
            newCollisionEntry.idxLink = newNodeEntry.UIndex;

            //Add cloned node to persistentlevel
            IExportEntry persistentlevel = null;
            foreach (IExportEntry exp in pcc.Exports)
            {
                if (exp.ClassName == "Level" && exp.ObjectName == "PersistentLevel")
                {
                    persistentlevel = exp;
                    break;
                }
            }
            //Read persistent level binary
            byte[] data = persistentlevel.Data;

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

            uint exportid = BitConverter.ToUInt32(data, start);
            start += 4;
            uint numberofitems = BitConverter.ToUInt32(data, start);
            numberofitems++;
            WriteMem(start, data, BitConverter.GetBytes(numberofitems));
            int insertoffset = (int)(numberofitems * 4) + start;
            List<byte> memList = data.ToList();
            memList.InsertRange(insertoffset, BitConverter.GetBytes(newNodeEntry.UIndex));
            data = memList.ToArray();
            persistentlevel.Data = data;

            reindexObjectsWithName(newNodeEntry.ObjectName);
            reindexObjectsWithName(newCollisionEntry.ObjectName);
            AllowRefresh = true;

            //RefreshView();
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            IEnumerable<PackageUpdate> relevantUpdates = updates.Where(x => x.change != PackageChange.Import &&
                                                                            x.change != PackageChange.ImportAdd &&
                                                                            x.change != PackageChange.Names);
            List<int> updatedExports = relevantUpdates.Select(x => x.index).ToList();
            //if (updatedExports.Contains(Sequence.Index))
            //{
            //    //loaded sequence is no longer a sequence
            //    //if (!Sequence.ClassName.Contains("Sequence"))
            //    //{
            //    //    graphEditor.nodeLayer.RemoveAllChildren();
            //    //    graphEditor.edgeLayer.RemoveAllChildren();
            //    //    CurrentObjects.Clear();
            //    //    listBox1.Items.Clear();
            //    //}
            //    RefreshView();
            //    LoadPathingNodesFromLevel();
            //    return;
            //}

            if (updatedExports.Intersect(CurrentObjects).Count() > 0)
            {
                RefreshView();
            }
            foreach (var i in updatedExports)
            {
                if (pathfindingNodeClasses.Contains(pcc.getExport(i).ClassName))
                {
                    LoadPathingNodesFromLevel();
                    break;
                }
            }
        }

        private void WriteMem(int pos, byte[] memory, byte[] buff)
        {
            for (int i = 0; i < buff.Length; i++)
                memory[pos + i] = buff[i];
        }

        /// <summary>
        /// This method changes a node's type. It does many steps:
        /// It checks that the node type imports exists as well as it's collision cylinder and reach spec imports.
        /// It will scan all nodes for incoming reachspecs to this node and change them to the appropriate class. 
        /// It changes the collision cylinder archetype
        /// It changes the node class and object name
        /// </summary>
        /// <param name="nodeEntry"></param>
        /// <param name="newType"></param>
        private void changeNodeType(IExportEntry nodeEntry, int newType)
        {
            List<UProperty> propertiesToAdd = new List<UProperty>();
            List<string> propertiesToRemoveIfPresent = new List<string>();

            string exportclassdbkey = "";
            switch (newType)
            {
                case NODETYPE_SFXENEMYSPAWNPOINT:
                    exportclassdbkey = "SFXEnemySpawnPoint";
                    break;
                case NODETYPE_PATHNODE:
                    exportclassdbkey = "PathNode";
                    break;
                case NODETYPE_SFXNAV_TURRETPOINT:
                    exportclassdbkey = "SFXNav_TurretPoint";
                    break;
                case NODETYPE_SFXNAV_BOOSTNODE_TOP:
                    exportclassdbkey = "SFXNav_BoostNode";
                    BoolProperty bTopNode = new BoolProperty(true, "bTopNode");
                    propertiesToAdd.Add(bTopNode);
                    break;
                case NODETYPE_SFXNAV_BOOSTNODE_BOTTOM:
                    exportclassdbkey = "SFXNav_BoostNode";
                    propertiesToRemoveIfPresent.Add("bTopNode");
                    break;
                default:
                    return;
            }

            //lookup requirements in DB
            Dictionary<string, string> exportclassinfo = exportclassdb[exportclassdbkey];
            string newclass = exportclassinfo["class"];
            string newname = exportclassinfo["name"];
            string newcylindercomponentarchetype = exportclassinfo["cylindercomponentarchetype"];

            //Get current cylinder component export.
            PropertyCollection props = nodeEntry.GetProperties();
            ObjectProperty cylindercomponent = props.GetProp<ObjectProperty>("CollisionComponent");
            IExportEntry cylindercomponentexp = pcc.getExport(cylindercomponent.Value - 1);

            //Ensure all classes are imported.
            ImportEntry newnodeclassimp = getOrAddImport(newclass);
            ImportEntry newcylindercomponentimp = getOrAddImport(newcylindercomponentarchetype);

            if (newnodeclassimp != null)
            {
                nodeEntry.idxClass = newnodeclassimp.UIndex;
                nodeEntry.idxObjectName = pcc.FindNameOrAdd(newname);
                cylindercomponentexp.idxArchtype = newcylindercomponentimp.UIndex;
            }

            //Write new properties
            foreach (UProperty prop in propertiesToAdd)
            {
                nodeEntry.WriteProperty(prop);
            }

            //Remove specific properties
            if (propertiesToRemoveIfPresent.Count > 0)
            {
                PropertyCollection properties = nodeEntry.GetProperties();
                List<UProperty> propertiesToRemove = new List<UProperty>();
                foreach (UProperty prop in properties)
                {
                    if (propertiesToRemoveIfPresent.Contains(prop.Name))
                    {
                        propertiesToRemove.Add(prop);
                    }
                }

                foreach (UProperty prop in propertiesToRemove)
                {
                    properties.Remove(prop);
                }
                nodeEntry.WriteProperties(properties);
            }



            string objectname = nodeEntry.ObjectName;

            // Get list of all exports with that object name.
            //List<IExportEntry> exports = new List<IExportEntry>();
            //Could use LINQ... meh.

            reindexObjectsWithName(objectname);
        }

        /// <summary>
        /// Reindexes all objects in this pcc that have the same objectname.
        /// USE WITH CAUTION!
        /// </summary>
        /// <param name="objectname">Objectname to reindex</param>
        private void reindexObjectsWithName(string objectname)
        {
            int index = 1; //we'll start at 1.
            foreach (IExportEntry export in pcc.Exports)
            {
                if (objectname == export.ObjectName)
                {
                    export.indexValue = index;
                    index++;
                }
            }
        }

        private void toSFXEnemySpawnPointToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex >= 0)
            {
                int n = CurrentObjects[listBox1.SelectedIndex];
                if (n == -1)
                    return;

                if (pcc.getEntry(n).ClassName != "SFXEnemySpawnPoint")
                {
                    changeNodeType(pcc.Exports[n], NODETYPE_SFXENEMYSPAWNPOINT);
                    RefreshView();
                }
            }
        }

        private void toPathNodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex >= 0)
            {
                int n = CurrentObjects[listBox1.SelectedIndex];
                if (n == -1)
                    return;
                IExportEntry selectednodeexp = pcc.Exports[n];
                if (selectednodeexp.ClassName != "PathNode")
                {
                    changeNodeType(selectednodeexp, NODETYPE_PATHNODE);
                    RefreshView();
                }
            }
        }


        private void toSFXNavTurretPointToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex >= 0)
            {
                int n = CurrentObjects[listBox1.SelectedIndex];
                if (n == -1)
                    return;
                IExportEntry selectednodeexp = pcc.Exports[n];
                if (selectednodeexp.ClassName != "SFXNav_TurretPoint")
                {
                    changeNodeType(selectednodeexp, NODETYPE_SFXNAV_TURRETPOINT);
                    RefreshView();
                }
            }
        }

        private ImportEntry getOrAddImport(string importFullName)
        {
            foreach (ImportEntry imp in pcc.Imports)
            {
                if (imp.GetFullPath == importFullName)
                {
                    return imp;
                }
            }

            //Import doesn't exist, so we're gonna need to add it
            //But first we need to figure out what needs to be added.
            string[] importParts = importFullName.Split('.');
            List<int> upstreamLinks = new List<int>(); //0 = top level, 1 = next level... n = what we wanted to import
            int upstreamCount = 1;

            ImportEntry upstreamImport = null;
            while (upstreamCount < importParts.Count())
            {
                string upstream = String.Join(".", importParts, 0, importParts.Count() - upstreamCount);
                foreach (ImportEntry imp in pcc.Imports)
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

            if (upstreamImport == null)
            {
                //There is no top level import, which is very unlikely (engine, sfxgame)
                return null;
            }

            //Have an upstream import, now we need to add downstream imports.
            ImportEntry mostdownstreamimport = null;

            while (upstreamCount > 0)
            {
                upstreamCount--;
                string fullobjectname = String.Join(".", importParts, 0, importParts.Count() - upstreamCount);
                Dictionary<string, string> importdbinfo = importclassdb[fullobjectname];

                int downstreamName = pcc.FindNameOrAdd(importParts[importParts.Count() - upstreamCount - 1]);
                Debug.WriteLine(pcc.Names[downstreamName]);
                int downstreamLinkIdx = upstreamImport.UIndex;
                Debug.WriteLine(upstreamImport.GetFullPath);

                int downstreamPackageName = pcc.FindNameOrAdd(importdbinfo["packagefile"]);
                int downstreamClassName = pcc.FindNameOrAdd(importdbinfo["class"]);

                //ImportEntry classImport = getOrAddImport();
                //int downstreamClass = 0;
                //if (classImport != null) {
                //    downstreamClass = classImport.UIndex; //no recursion pls
                //} else
                //{
                //    throw new Exception("No class was found for importing");
                //}

                mostdownstreamimport = new ImportEntry(pcc);
                mostdownstreamimport.idxLink = downstreamLinkIdx;
                mostdownstreamimport.idxClassName = downstreamClassName;
                mostdownstreamimport.idxObjectName = downstreamName;
                mostdownstreamimport.idxPackageName = downstreamPackageName;
                pcc.addImport(mostdownstreamimport);
                upstreamImport = mostdownstreamimport;
            }
            return mostdownstreamimport;
        }

        private void createReachSpecToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int sourceExportIndex = CurrentObjects[listBox1.SelectedIndex];
            if (sourceExportIndex == -1)
                return;



            string reachSpecClass = "";
            int destinationIndex = -1;
            bool createTwoWay = true;
            using (ReachSpecCreatorForm form = new ReachSpecCreatorForm(pcc, sourceExportIndex))
            {
                DialogResult dr = form.ShowDialog();
                if (dr != DialogResult.Yes)
                {
                    return; //user cancel
                }

                createTwoWay = form.CreateTwoWaySpec;
                destinationIndex = form.DestinationNode;
                reachSpecClass = form.SpecClass;
            }

            IExportEntry startNode = pcc.Exports[sourceExportIndex];
            Debug.WriteLine("Source Node: " + startNode.Index);
            //Find reachspec to clone
            IExportEntry reachSpectoClone = null;
            foreach (IExportEntry exp in pcc.Exports)
            {
                if (exp.ClassName == reachSpecClass)
                {
                    reachSpectoClone = exp;
                    break;
                }
            }

            Debug.WriteLine("Num Exports: " + pcc.Exports.Count);
            int outgoingSpec = pcc.ExportCount;
            int incomingSpec = pcc.ExportCount + 1;


            if (reachSpectoClone != null)
            {
                IExportEntry destNode = pcc.Exports[destinationIndex];
                Debug.WriteLine("Destination Node: " + destNode.Index);

                //time to clone.
                pcc.addExport(reachSpectoClone.Clone()); //outgoing

                //Have to do this manually because tools firing the clone seem to bust it.

                Debug.WriteLine("Clone 1 Num Exports: " + pcc.Exports.Count);
                Debug.WriteLine("Clone 1 UIndex: " + pcc.Exports[outgoingSpec].UIndex);
                if (createTwoWay)
                {
                    pcc.addExport(reachSpectoClone.Clone()); //incoming
                    Debug.WriteLine("Clone 2 Num Exports: " + pcc.Exports.Count);
                    Debug.WriteLine("Clone 2 UIndex: " + pcc.Exports[incomingSpec].UIndex);

                }

                IExportEntry outgoingSpecExp = pcc.Exports[outgoingSpec];
                Debug.WriteLine("Outgoing UIndex: " + outgoingSpecExp.UIndex);

                ObjectProperty outgoingSpecStartProp = outgoingSpecExp.GetProperty<ObjectProperty>("Start"); //START
                StructProperty outgoingEndStructProp = outgoingSpecExp.GetProperty<StructProperty>("End"); //Embeds END
                ObjectProperty outgoingSpecEndProp = outgoingEndStructProp.Properties.GetProp<ObjectProperty>("Actor"); //END
                outgoingSpecStartProp.Value = startNode.UIndex;
                outgoingSpecEndProp.Value = destNode.UIndex;


                //Add to source node prop
                ArrayProperty<ObjectProperty> PathList = startNode.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                byte[] memory = startNode.Data;
                memory = addObjectArrayLeaf(memory, (int)PathList.Offset, outgoingSpecExp.UIndex);
                startNode.Data = memory;
                outgoingSpecExp.WriteProperty(outgoingSpecStartProp);
                outgoingSpecExp.WriteProperty(outgoingEndStructProp);

                if (createTwoWay)
                {
                    IExportEntry incomingSpecExp = pcc.Exports[incomingSpec];
                    ObjectProperty incomingSpecStartProp = incomingSpecExp.GetProperty<ObjectProperty>("Start"); //START
                    StructProperty incomingEndStructProp = incomingSpecExp.GetProperty<StructProperty>("End"); //Embeds END
                    ObjectProperty incomingSpecEndProp = incomingEndStructProp.Properties.GetProp<ObjectProperty>("Actor"); //END

                    incomingSpecStartProp.Value = destNode.UIndex;//Uindex
                    incomingSpecEndProp.Value = startNode.UIndex;


                    //Add to source node prop
                    ArrayProperty<ObjectProperty> DestPathList = pcc.Exports[destinationIndex].GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                    memory = destNode.Data;
                    memory = addObjectArrayLeaf(memory, (int)DestPathList.Offset, incomingSpecExp.UIndex);
                    destNode.Data = memory;
                    //destNode.WriteProperty(DestPathList);
                    incomingSpecExp.WriteProperty(incomingSpecStartProp);
                    incomingSpecExp.WriteProperty(incomingEndStructProp);

                    //verify
                    destNode.GetProperties();
                }
                //incomingSpecStartProp.Value =

                //Reindex reachspecs.
                reindexObjectsWithName(reachSpecClass);
            }
        }

        /// <summary>
        /// Adds an item to a property array
        /// </summary>
        /// <param name="memory">Export memory</param>
        /// <param name="arrayStartOffset">Offset to start of the array property</param>
        /// <param name="objectUIndex">UIndex of object/int to add.</param>
        /// <returns>Updated memory</returns>
        private byte[] addObjectArrayLeaf(byte[] memory, int arrayStartOffset, int objectUIndex)
        {
            try
            {
                int pos = arrayStartOffset;
                bool isLeaf = false;
                int leafOffset = 0;

                int size = BitConverter.ToInt32(memory, pos + 16);
                int count = BitConverter.ToInt32(memory, pos + 24);
                int leafSize = 0;
                ArrayType arrayType = ArrayType.Object;
                List<byte> memList = memory.ToList();
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
                        memList.InsertRange(offset, BitConverter.GetBytes(objectUIndex));
                        break;
                    /*case ArrayType.Float:
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
                        break;*/
                    default:
                        return null;
                }
                memory = memList.ToArray();
                memory = updateArrayLength(memory, pos, 1, leafSize);

                //bubble up size - NOT IMPLEMENTED
                /*
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
                }*/
                return memory.TypedClone();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Updates an array properties length and size in bytes. Does not refresh the memory view
        /// </summary>
        /// <param name="startpos">Starting index of the array property</param>
        /// <param name="countDelta">Delta in terms of how many items the array has</param>
        /// <param name="byteDelta">Delta in terms of how many bytes the array data is</param>
        private byte[] updateArrayLength(byte[] memory, int startpos, int countDelta, int byteDelta)
        {
            int sizeOffset = 16;
            int countOffset = 24;
            int oldSize = BitConverter.ToInt32(memory, sizeOffset + startpos);
            int oldCount = BitConverter.ToInt32(memory, countOffset + startpos);

            int newSize = oldSize + byteDelta;
            int newCount = oldCount + countDelta;

            memory = WriteMem(memory, startpos + sizeOffset, BitConverter.GetBytes(newSize));
            memory = WriteMem(memory, startpos + countOffset, BitConverter.GetBytes(newCount));

            return memory;
        }

        /// <summary>
        /// Writes the buffer to the memory array starting at position pos
        /// </summary>
        /// <param name="memory">Memory array to overwrite onto</param>
        /// <param name="pos">Position to start writing at</param>
        /// <param name="buff">byte array to write, in order</param>
        /// <returns>Modified memory</returns>
        private byte[] WriteMem(byte[] memory, int pos, byte[] buff)
        {
            for (int i = 0; i < buff.Length; i++)
                memory[pos + i] = buff[i];

            return memory;
        }

        /// <summary>
        /// Sets the location X and Y value of the node to the same as the value shown in the graph.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void setGraphPositionAsNodeLocationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Find node
            int sourceExportIndex = CurrentObjects[listBox1.SelectedIndex];
            if (sourceExportIndex == -1)
                return;

            PathfindingNode node = null;
            foreach (PathfindingNodeMaster n in Objects)
            {
                if (n.Index == sourceExportIndex && n is PathfindingNode)
                {
                    node = (PathfindingNode) n;
                    break;
                }
            }
            if (node != null)
            {
                float locX = node.GlobalBounds.X;
                float locY = node.GlobalBounds.Y;

                IExportEntry export = node.export;
                StructProperty locationProp = export.GetProperty<StructProperty>("location");
                FloatProperty xProp = locationProp.Properties.GetProp<FloatProperty>("X");
                FloatProperty yProp = locationProp.Properties.GetProp<FloatProperty>("Y");

                xProp.Value = locX;
                yProp.Value = locY;

                export.WriteProperty(locationProp);
                MessageBox.Show("Location set to " + locX + "," + locY);

                //Need to update
            }
        }

        private void sFXNavBoostNodeTopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex >= 0)
            {
                int n = CurrentObjects[listBox1.SelectedIndex];
                if (n == -1)
                    return;
                IExportEntry selectednodeexp = pcc.Exports[n];
                if (selectednodeexp.ClassName != "SFXNav_BoostNode")
                {
                    changeNodeType(selectednodeexp, NODETYPE_SFXNAV_BOOSTNODE_TOP);
                    RefreshView();
                }
            }
        }

        private void sFXNavBoostNodeBottomToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex >= 0)
            {
                int n = CurrentObjects[listBox1.SelectedIndex];
                if (n == -1)
                    return;
                IExportEntry selectednodeexp = pcc.Exports[n];
                if (selectednodeexp.ClassName != "SFXNav_BoostNode")
                {
                    changeNodeType(selectednodeexp, NODETYPE_SFXNAV_BOOSTNODE_BOTTOM);
                    RefreshView();
                }
            }
        }

        private void generateNewRandomGUIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = CurrentObjects[listBox1.SelectedIndex];
            if (n == -1)
                return;

            AllowRefresh = false;
            IExportEntry nodeEntry = pcc.Exports[n];
            generateNewRandomGUID(nodeEntry);
            AllowRefresh = true;


        }

        class PathfindingMouseListener : PBasicInputEventHandler
        {
            private PathfindingEditor pathfinder;

            public PathfindingMouseListener(PathfindingEditor pathfinder)
            {
                this.pathfinder = pathfinder;
            }

            public override void OnMouseMove(object sender, PInputEventArgs e)
            {
                if (pathfinder != null)
                {
                    PointF pos = e.Position;
                    string fname = Path.GetFileName(pathfinder.CurrentFile);
                    int X = Convert.ToInt32(pos.X);
                    int Y = Convert.ToInt32(pos.Y);
                    pathfinder.filenameLabel.Text = fname + " [" + X + "," + Y + "]";
                }
            }
        }

        private void toggleActorNodes_Click(object sender, EventArgs e)
        {
            ActorNodesActive = toggleActorNodes.Checked;
            RefreshView();
        }

        public nodeType getType(string s)
        {
            int ret = -1;
            for (int i = 0; i < Types.Length; i++)
                if (s == Types[i])
                    ret = i;
            return (nodeType)ret;
        }

        private int findEndOfProps(IExportEntry export)
        {
            byte[] memory = export.Data;
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
    }



    public class PathingZoomController
    {
        public static float MIN_SCALE = .005f;
        public static float MAX_SCALE = 15;
        PCamera camera;

        public PathingZoomController(PathingGraphEditor graphEditor)
        {
            this.camera = graphEditor.Camera;
            camera.ViewScale = 0.5f;
            camera.Canvas.ZoomEventHandler = null;
            camera.MouseWheel += OnMouseWheel;
            graphEditor.KeyDown += OnKeyDown;
        }

        public void OnKeyDown(object o, KeyEventArgs e)
        {
            if (e.Control)
            {
                if (e.KeyCode == Keys.OemMinus)
                {
                    scaleView(0.8f, new PointF(camera.ViewBounds.X + (camera.ViewBounds.Height / 2), camera.ViewBounds.Y + (camera.ViewBounds.Width / 2)));
                }
                else if (e.KeyCode == Keys.Oemplus)
                {
                    scaleView(1.2f, new PointF(camera.ViewBounds.X + (camera.ViewBounds.Height / 2), camera.ViewBounds.Y + (camera.ViewBounds.Width / 2)));
                }
            }
        }

        public void OnMouseWheel(object o, PInputEventArgs ea)
        {
            scaleView(1.0f + (0.001f * ea.WheelDelta), ea.Position);
        }

        public void scaleView(float scaleDelta, PointF p)
        {
            float currentScale = camera.ViewScale;
            float newScale = currentScale * scaleDelta;
            if (newScale < MIN_SCALE)
            {
                camera.ViewScale = MIN_SCALE;
                return;
            }
            if ((MAX_SCALE > 0) && (newScale > MAX_SCALE))
            {
                camera.ViewScale = MAX_SCALE;
                return;
            }
            camera.ScaleViewBy(scaleDelta, p.X, p.Y);
        }
    }

    public static class ExportInputPrompt
    {
        public static IExportEntry ShowDialog(string text, string caption, IMEPackage pcc)
        {
            Form prompt = new Form()
            {
                Width = 500,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterScreen
            };
            Label textLabel = new Label() { Left = 50, Top = 20, Text = text };
            //TextBox textBox = new TextBox() { Left = 50, Top = 50, Width = 400 };
            ComboBox items = new ComboBox() { Left = 50, Top = 50, Width = 400 };
            items.DropDownStyle = ComboBoxStyle.DropDownList;
            List<IExportEntry> exports = new List<IExportEntry>();
            foreach (IExportEntry exp in pcc.Exports)
            {
                if (exp.ObjectName == "StaticMeshCollectionActor")
                {
                    items.Items.Add(exp.Index + " " + exp.ObjectName + "_" + exp.indexValue);
                    exports.Add(exp);
                }
            }

            Button confirmation = new Button() { Text = "Ok", Left = 350, Width = 100, Top = 70, DialogResult = DialogResult.OK };
            confirmation.Click += (sender, e) => { prompt.Close(); };
            prompt.Controls.Add(items);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;

            return prompt.ShowDialog() == DialogResult.OK ? exports[items.SelectedIndex] : null;
        }
    }
}