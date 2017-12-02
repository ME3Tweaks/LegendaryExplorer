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
        public static string[] Types =
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
        private const int NODETYPE_SFXNAV_LAREGEBOOSTNODE = 6;
        private const int NODETYPE_SFXNAV_LARGEMANTLENODE = 7;
        private const int NODETYPE_SFXNAV_CLIMBWALLNODE = 8;
        private const int NODETYPE_BIOPATHPOINT = 9;
        private const int NODETYPE_SFXDYNAMICCOVERLINK = 10;
        private const int NODETYPE_SFXDYNAMICCOVERSLOTMARKER = 11;

        private static string classDatabasePath = "";

        public static Dictionary<string, Dictionary<string, string>> importclassdb = new Dictionary<string, Dictionary<string, string>>(); //SFXGame.Default__SFXEnemySpawnPoint -> class, packagefile (can infer link and name)
        public static Dictionary<string, Dictionary<string, string>> exportclassdb = new Dictionary<string, Dictionary<string, string>>(); //SFXEnemy SpawnPoint -> class, name, ...etc

        public string[] pathfindingNodeClasses = { "PathNode", "SFXEnemySpawnPoint", "MantleMarker", "SFXNav_InteractionHenchOmniToolCrouch", "BioPathPoint", "SFXNav_LargeBoostNode", "SFXNav_LargeMantleNode", "SFXNav_InteractionStandGuard", "SFXNav_TurretPoint", "CoverLink", "SFXDynamicCoverLink", "SFXDynamicCoverSlotMarker", "SFXNav_SpawnEntrance", "SFXNav_LadderNode", "SFXDoorMarker", "SFXNav_JumpNode", "SFXNav_JumpDownNode", "NavigationPoint", "CoverSlotMarker", "SFXOperation_ObjectiveSpawnPoint", "SFXNav_BoostNode", "SFXNav_LargeClimbNode", "SFXNav_LargeMantleNode", "SFXNav_ClimbWallNode", "WwiseAmbientSound" };
        public string[] actorNodeClasses = { "BlockingVolume", "StaticMeshActor", "InterpActor", "SFXDoor", "BioTriggerVolume", "SFXPlaceable_Generator", "SFXPlaceable_ShieldGenerator", "SFXBlockingVolume_Ledge", "SFXAmmoContainer", "SFXGrenadeContainer", "SFXCombatZone", "BioStartLocation", "BioStartLocationMP", "SFXStuntActor", "SkeletalMeshActor" };
        public string[] ignoredobjectnames = { "PREFAB_Ladders_3M_Arc0", "PREFAB_Ladders_3M_Arc1" }; //These come up as parsed classes but aren't actually part of the level, only prefabs. They should be ignored
        public bool ActorNodesActive = false;
        public bool PathfindingNodesActive = true;
        public bool StaticMeshCollectionActorNodesActive = false;

        public PathfindingEditor()
        {
            AllowRefresh = true;
            classDatabasePath = Application.StartupPath + "//exec//pathfindingclassdb.json";
            InitializeComponent();
            pathfindingNodeInfoPanel.PassPathfindingNodeEditorIn(this);
            graphEditor.BackColor = Color.FromArgb(167, 167, 167);
            graphEditor.AddInputEventListener(new PathfindingMouseListener(this));
            zoomController = new PathingZoomController(graphEditor);
            CurrentFilterType = HeightFilterForm.FILTER_Z_NONE;
            CurrentZFilterValue = 0;
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
            //PointF mousePoint = e.Location;
            //Debug.WriteLine(mousePoint);
        }

        private void PathfindingEditor_Load(object sender, EventArgs e)
        {
            interpreter1.hideHexBox();
        }

        //public static readonly string OptionsPath = Path.Combine(PathfindingEditorDataFolder, "PathfindingEditorOptions.JSON");

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
        public bool RefOrRefChild;

        public string CurrentFile;
        public string JSONpath;
        public List<int> sfxCombatZones = new List<int>();

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
            ActiveCombatZoneExportIndex = -1;
            sfxCombatZones = new List<int>();
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
            sFXCombatZonesToolStripMenuItem.DropDownItems.Clear();
            sFXCombatZonesToolStripMenuItem.Enabled = false;
            sfxCombatZones = new List<int>();
            CurrentObjects = new List<int>();
            listBox1.Items.Clear();

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

                            //SFXCombatZone 
                            if (exportEntry.ClassName == "SFXCombatZone")
                            {
                                sfxCombatZones.Add(exportEntry.Index);
                                ToolStripMenuItem testItem = new ToolStripMenuItem(exportEntry.Index + " " + exportEntry.ObjectName + "_" + exportEntry.indexValue);
                                if (exportEntry.Index == ActiveCombatZoneExportIndex)
                                {
                                    testItem.Checked = true;
                                }
                                testItem.Click += (object o, EventArgs args) =>
                                {
                                    setSFXCombatZoneBGActive(testItem, exportEntry, testItem.Checked);
                                };
                                sFXCombatZonesToolStripMenuItem.DropDown.Items.Add(testItem);
                                sFXCombatZonesToolStripMenuItem.Enabled = true;
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
                                            if (obj.Value > 0)
                                            {
                                                CurrentObjects.Add(obj.Value - 1);
                                                listBox1.Items.Add("#" + (exportEntry.Index) + " " + exportEntry.ObjectName + " - Class: " + exportEntry.ClassName);

                                                //Read location and put in position map
                                                int offset = binarypos + 12 * 4;
                                                float x = BitConverter.ToSingle(smacData, offset);
                                                float y = BitConverter.ToSingle(smacData, offset + 4);
                                                //Debug.WriteLine(offset.ToString("X4") + " " + x + "," + y);
                                                smacCoordinates[obj.Value - 1] = new PointF(x, y);
                                            }
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

        private void setSFXCombatZoneBGActive(ToolStripMenuItem testItem, IExportEntry exportEntry, bool @checked)
        {
            testItem.Checked = !testItem.Checked;
            if (testItem.Checked)
            {
                ActiveCombatZoneExportIndex = exportEntry.Index;
            }
            else
            {
                ActiveCombatZoneExportIndex = -1;
            }
            foreach (ToolStripMenuItem tsmi in sFXCombatZonesToolStripMenuItem.DropDownItems)
            {
                if (tsmi.Checked && tsmi != testItem)
                {
                    tsmi.Checked = false; //uncheck other combat zones
                }
            }
            RefreshView();
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
                int selectednodeindex = listBox1.SelectedIndex;
                PathfindingNodeMaster nodeMaster = null;
                if (selectednodeindex >= 0 && selectednodeindex < CurrentObjects.Count())
                {
                    nodeMaster = Objects.FirstOrDefault(o => o.Index == CurrentObjects[selectednodeindex]);
                }


                graphEditor.nodeLayer.RemoveAllChildren();
                graphEditor.edgeLayer.RemoveAllChildren();
                CurrentObjects.Clear();
                LoadPathingNodesFromLevel();
                GenerateGraph();
                if (nodeMaster != null)
                {
                    int n = nodeMaster.Index;
                    int selected = CurrentObjects.IndexOf(n);
                    if (selected == -1)
                        return;
                    IsReloadSelecting = true;
                    listBox1.SelectedIndex = selected;
                    IsReloadSelecting = false;

                }
                //
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
                    LoadObject(CurrentObjects[i]);
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
        private bool IsReloadSelecting = false;

        public void LoadObject(int index)
        {
            PointF smacPos;
            bool found = smacCoordinates.TryGetValue(index, out smacPos);
            if (found)
            {
                SMAC_ActorNode smac = new SMAC_ActorNode(index, smacPos.X, smacPos.Y, pcc, graphEditor);
                Objects.Add(smac);
                return;
            }
            else
            {
                string s = pcc.getExport(index).ObjectName;
                int x = 0, y = 0, z = int.MinValue;
                //SaveData savedInfo = new SaveData(-1);
                IExportEntry exporttoLoad = pcc.getExport(index);
                //                var props = 
                var props = exporttoLoad.GetProperties();
                StructProperty prop = props.GetProp<StructProperty>("location");
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
                            case "Z":
                                z = Convert.ToInt32((locprop as FloatProperty).Value);
                                break;
                        }
                    }

                    if (CurrentFilterType != HeightFilterForm.FILTER_Z_NONE)
                    {
                        if (CurrentFilterType == HeightFilterForm.FILTER_Z_BELOW && z < CurrentZFilterValue)
                        {
                            return;
                        }
                        else if (CurrentFilterType == HeightFilterForm.FILTER_Z_ABOVE && z > CurrentZFilterValue)
                        {
                            return;
                        }
                    }

                    //IExportEntry export = pcc.getExport(index);
                    if (pathfindingNodeClasses.Contains(exporttoLoad.ClassName))
                    {
                        PathfindingNode pathNode = null;
                        switch (exporttoLoad.ClassName)
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
                            case "SFXNav_LargeMantleNode":
                                pathNode = new PathfindingNodes.SFXNav_LargeMantleNode(index, x, y, pcc, graphEditor);
                                break;
                            case "BioPathPoint":
                                pathNode = new PathfindingNodes.BioPathPoint(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXNav_LargeBoostNode":
                                pathNode = new PathfindingNodes.SFXNav_LargeBoostNode(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXNav_TurretPoint":
                                pathNode = new PathfindingNodes.SFXNav_TurretPoint(index, x, y, pcc, graphEditor);
                                break;
                            case "CoverLink":
                                pathNode = new PathfindingNodes.CoverLink(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXNav_JumpDownNode":
                                pathNode = new PathfindingNodes.SFXNav_JumpDownNode(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXNav_LadderNode":
                                pathNode = new PathfindingNodes.SFXNav_LadderNode(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXDynamicCoverLink":
                                pathNode = new PathfindingNodes.SFXDynamicCoverLink(index, x, y, pcc, graphEditor);
                                break;
                            case "WwiseAmbientSound":
                                pathNode = new PathfindingNodes.WwiseAmbientSound(index, x, y, pcc, graphEditor);
                                break;
                            case "CoverSlotMarker":
                                pathNode = new PathfindingNodes.CoverSlotMarker(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXDynamicCoverSlotMarker":
                                pathNode = new PathfindingNodes.SFXDynamicCoverSlotMarker(index, x, y, pcc, graphEditor);
                                break;
                            case "MantleMarker":
                                pathNode = new PathfindingNodes.MantleMarker(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXOperation_ObjectiveSpawnPoint":
                                pathNode = new PathfindingNodes.SFXObjectiveSpawnPoint(index, x, y, pcc, graphEditor);

                                //Create annex node if required
                                var annexZoneLocProp = props.GetProp<ObjectProperty>("AnnexZoneLocation");
                                if (annexZoneLocProp != null)
                                {
                                    int ind = annexZoneLocProp.Value - 1;
                                    if (ind >= 0 && ind < pcc.Exports.Count)
                                    {
                                        IExportEntry annexzonelocexp = pcc.Exports[ind];

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
                                    else
                                    {
                                        pathNode.comment.Text += "\nBAD ANNEXZONELOC!";
                                        pathNode.comment.TextBrush = new SolidBrush(Color.Red);
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
                        if (ActiveCombatZoneExportIndex >= 0)
                        {
                            ArrayProperty<StructProperty> volumes = props.GetProp<ArrayProperty<StructProperty>>("Volumes");
                            if (volumes != null)
                            {
                                foreach (StructProperty volume in volumes)
                                {
                                    ObjectProperty actorRef = volume.GetProp<ObjectProperty>("Actor");
                                    if (actorRef != null)
                                    {
                                        if (actorRef.Value == ActiveCombatZoneExportIndex + 1)
                                        {
                                            Debug.WriteLine("FOUND ACTIVE COMBAT NODE!");
                                            pathNode.shape.Brush = PathfindingNodeMaster.sfxCombatZoneBrush;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        Objects.Add(pathNode);
                        return;
                    } //End if Pathnode Class

                    if (actorNodeClasses.Contains(exporttoLoad.ClassName))
                    {
                        ActorNode actorNode = null;
                        switch (exporttoLoad.ClassName)
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
                            case "SFXGrenadeContainer":
                                actorNode = new ActorNodes.SFXGrenadeContainer(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXAmmoContainer":
                                actorNode = new ActorNodes.SFXAmmoContainer(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXBlockingVolume_Ledge":
                                actorNode = new ActorNodes.SFXBlockingVolume_Ledge(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXCombatZone":
                                actorNode = new ActorNodes.SFXCombatZone(index, x, y, pcc, graphEditor);
                                break;
                            case "BioStartLocation":
                            case "BioStartLocationMP":
                                actorNode = new ActorNodes.BioStartLocation(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXStuntActor":
                                actorNode = new ActorNodes.SFXStuntActor(index, x, y, pcc, graphEditor);
                                break;
                            case "SkeletalMeshActor":
                                actorNode = new ActorNodes.SkeletalMeshActor(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXPlaceable_Generator":
                            case "SFXPlaceable_ShieldGenerator":
                                actorNode = new ActorNodes.SFXPlaceable(index, x, y, pcc, graphEditor);
                                break;
                            default:
                                actorNode = new PendingActorNode(index, x, y, pcc, graphEditor);
                                break;
                        }
                        if (ActiveCombatZoneExportIndex >= 0)
                        {
                            ArrayProperty<StructProperty> volumes = props.GetProp<ArrayProperty<StructProperty>>("Volumes");
                            if (volumes != null)
                            {
                                foreach (StructProperty volume in volumes)
                                {
                                    ObjectProperty actorRef = volume.GetProp<ObjectProperty>("Actor");
                                    if (actorRef != null && actorRef.Value == ActiveCombatZoneExportIndex - 1)
                                    {
                                        //Debug.WriteLine("FOUND ACTIVE COMBAT NODE!");
                                        actorNode.shape.Brush = PathfindingNodeMaster.sfxCombatZoneBrush;
                                        break;
                                    }
                                }
                            }
                        }
                        Objects.Add(actorNode);
                        return;
                    }
                }
            }
            return; //hopefully won't see you
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
            pathfindingNodeInfoPanel.LoadExport(export);
            if (!IsReloadSelecting)
            {
                if (interpreter1.Pcc != export.FileRef)
                {
                    interpreter1.Pcc = export.FileRef; //allows cross file references to work.
                }
                interpreter1.export = export;
                interpreter1.InitInterpreter();
            }
            ObjectProperty combatZone = export.GetProperty<ObjectProperty>("CombatZone");
            if (combatZone != null && ActiveCombatZoneExportIndex != combatZone.Value - 1)
            {
                ActiveCombatZoneExportIndex = combatZone.Value - 1;
                RefreshView();
                graphEditor.Invalidate();
            }
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
            d.Filter = "Png Files (*.png)|*.png";
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
                image.Save(d.FileName, ImageFormat.Png);
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
            addToSFXCombatZoneToolStripMenuItem.Enabled = false;
            if (e.Button == MouseButtons.Right)
            {
                addToSFXCombatZoneToolStripMenuItem.DropDownItems.Clear();
                breakLinksToolStripMenuItem.DropDownItems.Clear();
                PathfindingNodeMaster node = (PathfindingNodeMaster)sender;
                IExportEntry nodeExp = pcc.Exports[n];
                var properties = nodeExp.GetProperties();
                if (node is PathfindingNode)
                {
                    changeNodeTypeToolStripMenuItem.Enabled = true;
                    createReachSpecToolStripMenuItem.Enabled = true;
                    generateNewRandomGUIDToolStripMenuItem.Enabled = true;
                    if (node.export.ClassName == "CoverSlotMarker")
                    {
                        ToolStripDropDown combatZonesDropdown = new ToolStripDropDown();
                        ToolStripMenuItem combatZoneItem;

                        foreach (int expid in sfxCombatZones)
                        {
                            IExportEntry combatZoneExp = pcc.Exports[expid];
                            combatZoneItem = new ToolStripMenuItem(combatZoneExp.Index + " " + combatZoneExp.ObjectName + "_" + combatZoneExp.indexValue);
                            combatZoneItem.Click += (object o, EventArgs args) =>
                            {
                                //removeReachSpec(nodeExp, outgoingSpec);
                                addCombatZoneRef(nodeExp, combatZoneExp);
                            };
                            combatZonesDropdown.Items.Add(combatZoneItem);
                        }
                        addToSFXCombatZoneToolStripMenuItem.Enabled = true;
                        addToSFXCombatZoneToolStripMenuItem.DropDown = combatZonesDropdown;
                    }

                    rightMouseButtonMenu.Show(MousePosition);
                    //open in InterpEditor
                    string className = pcc.getExport(((PathfindingNodes.PathfindingNode)sender).Index).ClassName;
                    //break links
                    breakLinksToolStripMenuItem.Enabled = false;
                    breakLinksToolStripMenuItem.DropDown = null;
                    ToolStripMenuItem breaklLinkItem;
                    ToolStripDropDown submenu = new ToolStripDropDown();
                    ArrayProperty<ObjectProperty> PathList = properties.GetProp<ArrayProperty<ObjectProperty>>("PathList");
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
                                removeReachSpec(nodeExp, outgoingSpec);
                            };
                            submenu.Items.Add(breaklLinkItem);
                        }

                        breakLinksToolStripMenuItem.Enabled = true;
                        breakLinksToolStripMenuItem.DropDown = submenu;
                    }
                }
                else if (node is ActorNode)
                {
                    changeNodeTypeToolStripMenuItem.Enabled = false;
                    generateNewRandomGUIDToolStripMenuItem.Enabled = false;
                    createReachSpecToolStripMenuItem.Enabled = false;
                    addToSFXCombatZoneToolStripMenuItem.Enabled = false;
                    addToSFXCombatZoneToolStripMenuItem.DropDown = null;
                    breakLinksToolStripMenuItem.Enabled = false;
                    breakLinksToolStripMenuItem.DropDown = null;

                    rightMouseButtonMenu.Show(MousePosition);

                }
            }
        }

        private void addCombatZoneRef(IExportEntry nodeExp, IExportEntry combatZoneExp)
        {
            //Adds a combat zone to the list of Volumes. Creates Volumes if it doesnt exist yet.
            ArrayProperty<StructProperty> volumes = nodeExp.GetProperty<ArrayProperty<StructProperty>>("Volumes");
            if (volumes == null)
            {
                //we need to add it as a property
                interpreter1.AddProperty("Volumes"); //Assuming interpreter shows current item.
                volumes = nodeExp.GetProperty<ArrayProperty<StructProperty>>("Volumes");
            }
            byte[] actorRef = new byte[20]; //5 ints
            //GUID of combat zone
            StructProperty guid = combatZoneExp.GetProperty<StructProperty>("CombatZoneGUID");
            int a = guid.GetProp<IntProperty>("A");
            int b = guid.GetProp<IntProperty>("D");
            int c = guid.GetProp<IntProperty>("C");
            int d = guid.GetProp<IntProperty>("D");

            actorRef = SharedPathfinding.WriteMem(actorRef, 0, BitConverter.GetBytes(a));
            actorRef = SharedPathfinding.WriteMem(actorRef, 4, BitConverter.GetBytes(b));
            actorRef = SharedPathfinding.WriteMem(actorRef, 8, BitConverter.GetBytes(c));
            actorRef = SharedPathfinding.WriteMem(actorRef, 12, BitConverter.GetBytes(d));

            //Combat Zone Ref
            actorRef = SharedPathfinding.WriteMem(actorRef, 16, BitConverter.GetBytes(combatZoneExp.UIndex));

        }

        /*private void addArrayLeaf()
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
        }*/

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
        public int ActiveCombatZoneExportIndex { get; set; }
        public int CurrentFilterType { get; private set; }
        public int CurrentZFilterValue { get; private set; }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            graphEditor.ScaleViewTo((float)Convert.ToDecimal(toolStripTextBox1.Text));
        }

        private void openInPackageEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex < 0)
            {
                return;
            }
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

        void cloneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = CurrentObjects[listBox1.SelectedIndex];
            if (n == -1)
                return;

            AllowRefresh = false;
            IExportEntry nodeEntry = pcc.Exports[n];
            ObjectProperty collisionComponentProperty = nodeEntry.GetProperty<ObjectProperty>("CollisionComponent");
            IExportEntry collisionEntry = pcc.Exports[collisionComponentProperty.Value - 1];

            int newNodeIndex = pcc.Exports.Count;
            int newCollisionIndex = newNodeIndex + 1;

            pcc.addExport(nodeEntry.Clone());
            pcc.addExport(collisionEntry.Clone());

            IExportEntry newNodeEntry = pcc.Exports[newNodeIndex];
            IExportEntry newCollisionEntry = pcc.Exports[newCollisionIndex];

            //empty the pathlist
            ArrayProperty<ObjectProperty> PathList = newNodeEntry.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
            if (PathList != null)
            {
                PathList.Clear();
                newNodeEntry.WriteProperty(PathList);
            }
            //reuse
            collisionComponentProperty = newNodeEntry.GetProperty<ObjectProperty>("CollisionComponent");
            if (collisionComponentProperty != null)
            {
                collisionComponentProperty.Value = newCollisionEntry.UIndex;
                newCollisionEntry.idxLink = newNodeEntry.UIndex;
                newNodeEntry.WriteProperty(collisionComponentProperty);

                collisionComponentProperty = newNodeEntry.GetProperty<ObjectProperty>("CylinderComponent");
                if (collisionComponentProperty != null)
                {
                    collisionComponentProperty.Value = newCollisionEntry.UIndex;
                    newNodeEntry.WriteProperty(collisionComponentProperty);

                }
            }

            SharedPathfinding.GenerateNewRandomGUID(newNodeEntry);
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
            SharedPathfinding.WriteMem(data, start, BitConverter.GetBytes(numberofitems));
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
                case NODETYPE_SFXNAV_LAREGEBOOSTNODE:
                    exportclassdbkey = "SFXNav_LargeBoostNode";
                    propertiesToRemoveIfPresent.Add("bTopNode"); //if coming from boost node
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
                case NODETYPE_SFXNAV_LARGEMANTLENODE:
                    exportclassdbkey = "SFXNav_LargeMantleNode";
                    ObjectProperty mantleDest = new ObjectProperty(0, "MantleDest");
                    propertiesToAdd.Add(mantleDest);
                    break;
                case NODETYPE_SFXNAV_CLIMBWALLNODE:
                    exportclassdbkey = "SFXNav_ClimbWallNode";
                    //propertiesToRemoveIfPresent.Add("ClimbDest");
                    break;
                case NODETYPE_BIOPATHPOINT:
                    {
                        exportclassdbkey = "BioPathPoint";
                        BoolProperty bEnabled = new BoolProperty(true, "bEnabled");
                        propertiesToAdd.Add(bEnabled);
                        break;
                    }
                case NODETYPE_SFXDYNAMICCOVERLINK:
                    {
                        exportclassdbkey = "SFXDynamicCoverLink";
                        //BoolProperty bEnabled = new BoolProperty(true, "bEnabled");
                        //propertiesToAdd.Add(bEnabled);
                        break;
                    }
                case NODETYPE_SFXDYNAMICCOVERSLOTMARKER:
                    {
                        exportclassdbkey = "SFXDynamicCoverSlotMarker";
                        //coverslot property blows up adding properties
                        BoolProperty bEnabled = new BoolProperty(true, "bEnabled");
                        propertiesToAdd.Add(bEnabled);
                        break;
                    }
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
            if (propertiesToAdd.Count() > 0 || propertiesToRemoveIfPresent.Count() > 0)
            {
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
            }

            //perform special tasks here.
            switch (newType)
            {
                case NODETYPE_SFXNAV_LAREGEBOOSTNODE:
                    {
                        //Maximize MaxPathSize
                        StructProperty maxpathsize = nodeEntry.GetProperty<StructProperty>("MaxPathSize");
                        if (maxpathsize != null)
                        {
                            FloatProperty radius = maxpathsize.GetProp<FloatProperty>("Radius");
                            FloatProperty height = maxpathsize.GetProp<FloatProperty>("Height");

                            if (radius != null)
                            {
                                radius.Value = 140;
                            }
                            if (height != null)
                            {
                                height.Value = 195;
                            }
                            nodeEntry.WriteProperty(maxpathsize);
                        }


                        //If items on the other end of a reachspec are also SFXNav_LargeBoostNode,
                        //Ensure the reachspec is SFXLargeBoostReachSpec.
                        //Ensure maxpath sizes are set to max size.
                        ArrayProperty<ObjectProperty> pathList = nodeEntry.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                        if (pathList != null)
                        {
                            for (int i = 0; i < pathList.Count; i++)
                            {
                                IExportEntry spec = pcc.Exports[pathList[i].Value - 1];
                                //Get ending
                                int othernodeidx = 0;
                                PropertyCollection specprops = spec.GetProperties();
                                foreach (var prop in specprops)
                                {
                                    if (prop.Name == "End")
                                    {
                                        PropertyCollection reachspecprops = (prop as StructProperty).Properties;
                                        foreach (var rprop in reachspecprops)
                                        {
                                            if (rprop.Name == "Actor")
                                            {
                                                othernodeidx = (rprop as ObjectProperty).Value;
                                                break;
                                            }
                                        }
                                    }
                                    if (othernodeidx != 0)
                                    {
                                        break;
                                    }
                                }

                                if (othernodeidx != 0)
                                {
                                    bool keepParsing = true;
                                    IExportEntry specDest = pcc.Exports[othernodeidx - 1];
                                    if (specDest.ClassName == "SFXNav_LargeBoostNode" && spec.ClassName != "SFXLargeBoostReachSpec")
                                    {
                                        //Change the reachspec info outgoing to this node...
                                        ImportEntry newReachSpecClass = getOrAddImport("SFXGame.SFXLargeBoostReachSpec");

                                        if (newReachSpecClass != null)
                                        {
                                            spec.idxClass = newReachSpecClass.UIndex;
                                            spec.idxObjectName = pcc.FindNameOrAdd("SFXLargeBoostReachSpec");
                                            //set spec to banshee sized
                                            setReachSpecSize(spec, PathfindingNodeInfoPanel.BANSHEE_RADIUS, PathfindingNodeInfoPanel.BANSHEE_HEIGHT);
                                        }

                                        //Change the reachspec incoming to this node...
                                        ArrayProperty<ObjectProperty> otherNodePathlist = specDest.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                                        if (otherNodePathlist != null)
                                        {
                                            for (int on = 0; on < otherNodePathlist.Count; on++)
                                            {
                                                IExportEntry inboundSpec = pcc.Exports[otherNodePathlist[on].Value - 1];
                                                //Get ending
                                                //PropertyCollection inboundProps = inboundSpec.GetProperties();
                                                var prop = inboundSpec.GetProperty<StructProperty>("End");
                                                if (prop != null)
                                                {
                                                    PropertyCollection reachspecprops = (prop as StructProperty).Properties;
                                                    foreach (var rprop in reachspecprops)
                                                    {
                                                        if (rprop.Name == "Actor")
                                                        {
                                                            int inboundSpecDest = (rprop as ObjectProperty).Value;
                                                            if (inboundSpecDest == nodeEntry.UIndex)
                                                            {
                                                                //The node is inbound to me.
                                                                inboundSpec.idxClass = newReachSpecClass.UIndex;
                                                                inboundSpec.idxObjectName = pcc.FindNameOrAdd("SFXLargeBoostReachSpec");
                                                                //widen spec
                                                                setReachSpecSize(inboundSpec, PathfindingNodeInfoPanel.BANSHEE_RADIUS, PathfindingNodeInfoPanel.BANSHEE_HEIGHT);
                                                                keepParsing = false; //stop the outer loop
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                                if (!keepParsing)
                                                {
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }


                        //var outLinksProp = nodeEntry.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                        //if (outLinksProp != null)
                        //{
                        //    foreach (var prop in outLinksProp)
                        //    {
                        //        int reachspecexport = prop.Value;
                        //        ReachSpecs.Add(pcc.Exports[reachspecexport - 1]);
                        //    }

                        //    foreach (IExportEntry spec in ReachSpecs)
                        //    {

                        //    }
                        //}
                    }

                    break;

            }



            string objectname = nodeEntry.ObjectName;

            // Get list of all exports with that object name.
            //List<IExportEntry> exports = new List<IExportEntry>();
            //Could use LINQ... meh.

            reindexObjectsWithName(objectname);
        }

        private void setReachSpecSize(IExportEntry spec, int radius, int height)
        {
            PropertyCollection specProperties = spec.GetProperties();
            IntProperty radiusProp = specProperties.GetProp<IntProperty>("CollisionRadius");
            IntProperty heightProp = specProperties.GetProp<IntProperty>("CollisionHeight");
            if (radiusProp != null)
            {
                radiusProp.Value = radius;
            }
            if (heightProp != null)
            {
                heightProp.Value = height;
            }
            spec.WriteProperties(specProperties); //write it back.
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

                if (pcc.Exports[n].ClassName != "SFXEnemySpawnPoint")
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
            if (listBox1.SelectedIndex < 0)
            {
                return;
            }
            int sourceExportIndex = CurrentObjects[listBox1.SelectedIndex];
            if (sourceExportIndex == -1)
                return;



            string reachSpecClass = "";
            int destinationIndex = -1;
            bool createTwoWay = true;
            int size = 1; //Minibosses by default
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
                size = form.SpecSize;
            }

            IExportEntry startNode = pcc.Exports[sourceExportIndex];
            //Debug.WriteLine("Source Node: " + startNode.Index);
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

            //Debug.WriteLine("Num Exports: " + pcc.Exports.Count);
            int outgoingSpec = pcc.ExportCount;
            int incomingSpec = pcc.ExportCount + 1;


            if (reachSpectoClone != null)
            {
                IExportEntry destNode = pcc.Exports[destinationIndex];
                //Debug.WriteLine("Destination Node: " + destNode.Index);

                //time to clone.
                pcc.addExport(reachSpectoClone.Clone()); //outgoing

                //Have to do this manually because tools firing the clone seem to bust it.

                //Debug.WriteLine("Clone 1 Num Exports: " + pcc.Exports.Count);
                //Debug.WriteLine("Clone 1 UIndex: " + pcc.Exports[outgoingSpec].UIndex);
                if (createTwoWay)
                {
                    pcc.addExport(reachSpectoClone.Clone()); //incoming
                    //Debug.WriteLine("Clone 2 Num Exports: " + pcc.Exports.Count);
                    //Debug.WriteLine("Clone 2 UIndex: " + pcc.Exports[incomingSpec].UIndex);

                }

                IExportEntry outgoingSpecExp = pcc.Exports[outgoingSpec];
                //Debug.WriteLine("Outgoing UIndex: " + outgoingSpecExp.UIndex);

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

                //Write Spec Size
                int radVal = -1;
                int heightVal = -1;

                Point sizePair = PathfindingNodeInfoPanel.getDropdownSizePair(size);
                radVal = sizePair.X;
                heightVal = sizePair.Y;
                setReachSpecSize(outgoingSpecExp, radVal, heightVal);


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
                    setReachSpecSize(incomingSpecExp, radVal, heightVal);


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

            memory = SharedPathfinding.WriteMem(memory, startpos + sizeOffset, BitConverter.GetBytes(newSize));
            memory = SharedPathfinding.WriteMem(memory, startpos + countOffset, BitConverter.GetBytes(newCount));

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

            PathfindingNodeMaster node = null;
            foreach (PathfindingNodeMaster n in Objects)
            {
                if (n.Index == sourceExportIndex)
                {
                    node = n;
                    break;
                }
            }
            if (node != null)
            {
                float locX = node.GlobalBounds.X;
                float locY = node.GlobalBounds.Y;

                IExportEntry export = node.export;
                StructProperty locationProp = export.GetProperty<StructProperty>("location");
                if (locationProp != null)
                {
                    FloatProperty xProp = locationProp.Properties.GetProp<FloatProperty>("X");
                    FloatProperty yProp = locationProp.Properties.GetProp<FloatProperty>("Y");

                    xProp.Value = locX;
                    yProp.Value = locY;

                    export.WriteProperty(locationProp);
                    MessageBox.Show("Location set to " + locX + "," + locY);

                }
                else
                {
                    MessageBox.Show("No location property on this export.");

                }
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
            SharedPathfinding.GenerateNewRandomGUID(nodeEntry);
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
                    if (pathfinder.CurrentFilterType != HeightFilterForm.FILTER_Z_NONE)
                    {
                        fname += " | Hiding nodes " + (pathfinder.CurrentFilterType == HeightFilterForm.FILTER_Z_ABOVE ? "above" : "below") + " Z = " + pathfinder.CurrentZFilterValue + " | ";
                    }

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

        public static nodeType getType(string s)
        {
            int ret = -1;
            for (int i = 0; i < Types.Length; i++)
                if (s == Types[i])
                    ret = i;
            return (nodeType)ret;
        }

        public static int findEndOfProps(IExportEntry export)
        {
            IMEPackage pcc = export.FileRef;
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

        private void toSFXNavLargeBoostNodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex >= 0)
            {
                int n = CurrentObjects[listBox1.SelectedIndex];
                if (n == -1)
                    return;
                IExportEntry selectednodeexp = pcc.Exports[n];
                if (selectednodeexp.ClassName != "SFXNav_LargeBoostNode")
                {
                    changeNodeType(selectednodeexp, NODETYPE_SFXNAV_LAREGEBOOSTNODE);
                    RefreshView();
                }
            }
        }

        private void filterByZToolStripMenuItem_Click(object sender, EventArgs e)
        {

            using (HeightFilterForm hff = new HeightFilterForm(CurrentFilterType, CurrentZFilterValue))
            {
                DialogResult dr = hff.ShowDialog();
                if (dr != DialogResult.Yes)
                {
                    return; //user cancel
                }

                CurrentFilterType = hff.NewFilterType;
                CurrentZFilterValue = hff.NewFilterZ;
                if (CurrentFilterType != HeightFilterForm.FILTER_Z_NONE)
                {
                    filenameLabel.Text = Path.GetFileName(CurrentFile) + " | Hiding nodes " + (CurrentFilterType == HeightFilterForm.FILTER_Z_ABOVE ? "above" : "below") + " Z = " + CurrentZFilterValue;
                }
                else
                {
                    filenameLabel.Text = Path.GetFileName(CurrentFile);
                }
                RefreshView();
            }
        }

        private void refreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RefreshView();
        }

        private void recalculateReachspecsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ReachSpecRecalculator rsr = new ReachSpecRecalculator(this);
            rsr.ShowDialog(this);
        }

        private void toSFXNavLargeMantleNodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex >= 0)
            {
                int n = CurrentObjects[listBox1.SelectedIndex];
                if (n == -1)
                    return;
                IExportEntry selectednodeexp = pcc.Exports[n];
                if (selectednodeexp.ClassName != "SFXNav_LargeMantleNode")
                {
                    changeNodeType(selectednodeexp, NODETYPE_SFXNAV_LARGEMANTLENODE);
                    RefreshView();
                }
            }
        }

        private void toSFXNavClimbWallNodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex >= 0)
            {
                int n = CurrentObjects[listBox1.SelectedIndex];
                if (n == -1)
                    return;
                IExportEntry selectednodeexp = pcc.Exports[n];
                if (selectednodeexp.ClassName != "SFXNav_ClimbWallNode")
                {
                    changeNodeType(selectednodeexp, NODETYPE_SFXNAV_CLIMBWALLNODE);
                    RefreshView();
                }
            }
        }

        private void fixStackHeadersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
            {
                return;
            }
            //int start = 0x4;
            //if (level != null)
            //{
            //    start = findEndOfProps(level);
            //}
            //Read persistent level binary
            int itemcount = 2;
            int numUpdated = 0;

            Dictionary<int, List<int>> mpIDs = new Dictionary<int, List<int>>();
            Debug.WriteLine("Start of header fix scan===================");

            //start full scan.
            itemcount = 2;

            foreach (IExportEntry exportEntry in pcc.Exports)
            {
                string path = exportEntry.GetFullPath;
                string[] pieces = path.Split('.');

                if (pieces.Length < 3 || pieces[0] != "TheWorld" || pieces[1] != "PersistentLevel")
                {
                    continue;
                }
                //}

                //while (itemcount < numberofitems)
                //{
                //    //get header.
                //    uint itemexportid = BitConverter.ToUInt32(data, start);
                //    if (itemexportid - 1 < pcc.Exports.Count && itemexportid > 0)
                //    {
                //        IExportEntry exportEntry = pcc.Exports[(int)itemexportid - 1];

                int idOffset = 0;
                if ((exportEntry.ObjectFlags & (ulong)UnrealFlags.EObjectFlags.HasStack) != 0)
                {
                    byte[] exportData = exportEntry.Data;
                    int classId1 = BitConverter.ToInt32(exportData, 0);
                    int classId2 = BitConverter.ToInt32(exportData, 4);
                    //Debug.WriteLine(maybe_MPID);
                    bool updated = false;

                    int metadataClass = exportEntry.idxClass;
                    if ((classId1 != metadataClass) || (classId2 != metadataClass))
                    {
                        Debug.WriteLine("Updating unreal class data header in export " + exportEntry.Index + " " + exportEntry.ClassName);
                        //Update unreal header
                        SharedPathfinding.WriteMem(exportData, 0, BitConverter.GetBytes(metadataClass));
                        SharedPathfinding.WriteMem(exportData, 4, BitConverter.GetBytes(metadataClass));
                        numUpdated++;
                        updated = true;
                    }
                    if (updated)
                    {
                        exportEntry.Data = exportData;
                    }
                    idOffset = 0x1A;
                }


                int maybe_MPID = BitConverter.ToInt32(exportEntry.Data, idOffset);
                List<int> idList;
                if (mpIDs.TryGetValue(maybe_MPID, out idList))
                {
                    Debug.WriteLine(itemcount);
                    idList.Add(exportEntry.Index);
                }
                else
                {
                    mpIDs[maybe_MPID] = new List<int>();
                    mpIDs[maybe_MPID].Add(exportEntry.Index);
                }
                itemcount++;
            }

            //Update IDs
            for (int index = 0; index < mpIDs.Count; index++)
            {
                var item = mpIDs.ElementAt(index);
                List<int> valueList = item.Value;
                if (valueList.Count > 1 && item.Key != 0 && item.Key != -1)
                {
                    string itemlist = pcc.Exports[valueList[0]].ObjectName;
                    for (int i = 1; i < valueList.Count; i++)
                    // for (int i = valueList.Count - 1; i > 1; i--)
                    {
                        int max = mpIDs.Keys.Max();
                        //Debug.WriteLine("New max key size: " + max);
                        IExportEntry export = pcc.Exports[valueList[i]];
                        itemlist += " " + export.ObjectName;
                        string exportname = export.ObjectName;
                        if (exportname.Contains("SFXOperation") || exportname.Contains("ReachSpec"))
                        {

                            int idOffset = 0;
                            if ((export.ObjectFlags & (ulong)UnrealFlags.EObjectFlags.HasStack) != 0)
                            {
                                idOffset = 0x1A;
                            }
                            byte[] exportData = export.Data;

                            int nameId = BitConverter.ToInt32(exportData, 4);

                            if (pcc.isName(nameId) && pcc.getNameEntry(nameId) == export.ObjectName)
                            {
                                //It's a primitive component header
                                idOffset += 8;
                                continue;
                            }
                            int maybe_MPID = BitConverter.ToInt32(exportData, idOffset);

                            max++;
                            int origId = maybe_MPID;
                            SharedPathfinding.WriteMem(exportData, idOffset, BitConverter.GetBytes(max));
                            numUpdated++;


                            maybe_MPID = BitConverter.ToInt32(exportData, idOffset); //read new fixed id
                            Debug.WriteLine("Updated MPID " + origId + " -> " + maybe_MPID + " " + export.ObjectName + " in exp " + export.Index);
                            export.Data = exportData;

                            //add to new list to prevent rewrite of dupes.
                            mpIDs[maybe_MPID] = new List<int>();
                            mpIDs[maybe_MPID].Add(export.Index);
                        }

                    }
                    Debug.WriteLine(itemlist);
                }
            }



            MessageBox.Show(this, numUpdated + " export" + (numUpdated != 1 ? "s" : "") + " in PersistentLevel had data headers updated.", "Header Check Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void validateReachToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
            {
                return;
            }

            new DuplicateGUIDWindow(pcc).Show(this);
        }

        private void gotoButton_Clicked(object sender, EventArgs e)
        {
            if (pcc == null)
            {
                return;
            }
            string searchText = gotoNode_TextBox.Text;
            int exportNum;
            if (int.TryParse(searchText, out exportNum))
            {
                if (exportNum < pcc.ExportCount && exportNum >= 0)
                {
                    int index = 0;
                    foreach (int item in CurrentObjects)
                    {
                        if (item == exportNum)
                        {
                            //it exists

                            listBox1.SelectedIndex = index;
                            break;
                        }
                        index++;
                    }
                }
            }
        }

        private void gotoField_KeyPressed(object sender, KeyPressEventArgs e)
        {

            if ((e.KeyChar == Convert.ToChar(Keys.Enter)))
            {
                e.Handled = true;
                gotoNodeButton.PerformClick();
                return;
            }
            e.Handled = !char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar); //prevent non digit entry
        }

        private void relinkPathfinding_ButtonClicked(object sender, EventArgs e)
        {
            List<IExportEntry> pathfindingChain = new List<IExportEntry>();

            //Get list of all pathfinding nodes
            foreach (IExportEntry exp in pcc.Exports)
            {
                if (exp.ClassName == "Level" && exp.ObjectName == "PersistentLevel")
                {
                    int start = findEndOfProps(exp) + 4; //itemcount
                    //Read persistent level binary
                    byte[] data = exp.Data;
                    uint numberofitems = BitConverter.ToUInt32(data, start);
                    int countoffset = start;

                    start += 8;
                    int itemcount = 2; //Skip bioworldinfo and Class

                    //Get all nav items.
                    while (itemcount <= numberofitems)
                    {
                        //get header.
                        uint itemexportid = BitConverter.ToUInt32(data, start);
                        if (itemexportid - 1 < pcc.Exports.Count)
                        {
                            IExportEntry exportEntry = pcc.Exports[(int)itemexportid - 1];
                            StructProperty navGuid = exportEntry.GetProperty<StructProperty>("NavGuid");
                            if (navGuid != null)
                            {
                                pathfindingChain.Add(exportEntry);
                            }

                            start += 4;
                            itemcount++;
                        }
                        else
                        {
                            start += 4;
                            itemcount++;
                        }
                    }

                    //Filter so it only has nextNavigationPoint. This will drop the end node
                    List<IExportEntry> nextNavigationPointChain = new List<IExportEntry>();
                    foreach (IExportEntry exportEntry in pathfindingChain)
                    {
                        ObjectProperty nextNavigationPointProp = exportEntry.GetProperty<ObjectProperty>("nextNavigationPoint");

                        if (nextNavigationPointProp == null)
                        {
                            //don't add this as its not part of this chain
                            continue;
                        }
                        nextNavigationPointChain.Add(exportEntry);
                    }

                    //Follow chain to end to find end node
                    IExportEntry nodeEntry = nextNavigationPointChain[0];
                    ObjectProperty nextNavPoint = nodeEntry.GetProperty<ObjectProperty>("nextNavigationPoint");

                    while (nextNavPoint != null)
                    {
                        nodeEntry = pcc.Exports[nextNavPoint.Value - 1];
                        nextNavPoint = nodeEntry.GetProperty<ObjectProperty>("nextNavigationPoint");
                    }

                    //rebuild chain
                    for (int i = 0; i < nextNavigationPointChain.Count; i++)
                    {
                        IExportEntry chainItem = nextNavigationPointChain[i];
                        IExportEntry nextchainItem;
                        if (i < nextNavigationPointChain.Count - 1)
                        {
                            nextchainItem = nextNavigationPointChain[i + 1];
                        }
                        else
                        {
                            nextchainItem = nodeEntry;
                        }

                        ObjectProperty nextNav = chainItem.GetProperty<ObjectProperty>("nextNavigationPoint");

                        byte[] expData = chainItem.Data;
                        SharedPathfinding.WriteMem(expData, (int)nextNav.Offset, BitConverter.GetBytes(nextchainItem.UIndex));
                        chainItem.Data = expData;
                        Debug.WriteLine(chainItem.Index + " Chain link -> " + nextchainItem.UIndex);
                    }

                    MessageBox.Show("NavigationPoint chain has been updated.");
                }
            }
        }

        private void toBioPathPointToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex >= 0)
            {
                int n = CurrentObjects[listBox1.SelectedIndex];
                if (n == -1)
                    return;

                if (pcc.Exports[n].ClassName != "BioPathPoint")
                {
                    changeNodeType(pcc.Exports[n], NODETYPE_BIOPATHPOINT);
                    RefreshView();
                }
            }
        }

        private void toSFXDynamicCoverLinkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex >= 0)
            {
                int n = CurrentObjects[listBox1.SelectedIndex];
                if (n == -1)
                    return;

                if (pcc.Exports[n].ClassName != "SFXDynamicCoverLink")
                {
                    changeNodeType(pcc.Exports[n], NODETYPE_SFXDYNAMICCOVERLINK);
                    RefreshView();
                }
            }
        }

        private void toSFXDynamicCoverSlotMarkerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex >= 0)
            {
                int n = CurrentObjects[listBox1.SelectedIndex];
                if (n == -1)
                    return;

                if (pcc.Exports[n].ClassName != "SFXDynamicCovertSlotMarker")
                {
                    changeNodeType(pcc.Exports[n], NODETYPE_SFXDYNAMICCOVERSLOTMARKER);
                    RefreshView();
                }
            }
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