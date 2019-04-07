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
using ME3Explorer.SplineNodes;
using SharpDX;
using ME3Explorer.SharedUI;
using System.Windows.Threading;

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
        private const int NODETYPE_SFXNAV_JUMPDOWNNODE_TOP = 12;
        private const int NODETYPE_SFXNAV_JUMPDOWNNODE_BOTTOM = 13;

        public List<string> RFiles;
        public static readonly string PathfindingEditorDataFolder = Path.Combine(App.AppDataFolder, @"PathfindingEditor\");
        private readonly string RECENTFILES_FILE = "RECENTFILES";
        private static string classDatabasePath = "";

        public static Dictionary<string, Dictionary<string, string>> importclassdb = new Dictionary<string, Dictionary<string, string>>(); //SFXGame.Default__SFXEnemySpawnPoint -> class, packagefile (can infer link and name)
        public static Dictionary<string, Dictionary<string, string>> exportclassdb = new Dictionary<string, Dictionary<string, string>>(); //SFXEnemy SpawnPoint -> class, name, ...etc

        public string[] pathfindingNodeClasses = { "PathNode", "SFXEnemySpawnPoint", "PathNode_Dynamic", "SFXNav_HarvesterMoveNode", "SFXNav_LeapNodeHumanoid", "MantleMarker", "TargetPoint", "BioPathPoint", "SFXNav_LargeBoostNode", "SFXNav_LargeMantleNode", "SFXNav_InteractionStandGuard", "SFXNav_TurretPoint", "CoverLink", "SFXDynamicCoverLink", "SFXDynamicCoverSlotMarker", "SFXNav_SpawnEntrance", "SFXNav_LadderNode", "SFXDoorMarker", "SFXNav_JumpNode", "SFXNav_JumpDownNode", "NavigationPoint", "CoverSlotMarker", "SFXOperation_ObjectiveSpawnPoint", "SFXNav_BoostNode", "SFXNav_LargeClimbNode", "SFXNav_LargeMantleNode", "SFXNav_ClimbWallNode",
                "SFXNav_InteractionHenchOmniTool", "SFXNav_InteractionHenchOmniToolCrouch", "SFXNav_InteractionHenchBeckonFront", "SFXNav_InteractionHenchBeckonRear", "SFXNav_InteractionHenchCustom", "SFXNav_InteractionHenchCover", "SFXNav_InteractionHenchCrouch", "SFXNav_InteractionHenchInteractLow", "SFXNav_InteractionHenchManual", "SFXNav_InteractionHenchStandIdle", "SFXNav_InteractionHenchStandTyping", "SFXNav_InteractionUseConsole", "SFXNav_InteractionStandGuard", "SFXNav_InteractionHenchOmniToolCrouch", "SFXNav_InteractionInspectWeapon", "SFXNav_InteractionOmniToolScan" };
        public string[] actorNodeClasses = { "BlockingVolume", "DynamicBlockingVolume", "StaticMeshActor", "SFXMedStation", "InterpActor", "SFXDoor", "BioTriggerVolume", "SFXArmorNode", "BioTriggerStream", "SFXTreasureNode", "SFXPointOfInterest", "SFXPlaceable_Generator", "SFXPlaceable_ShieldGenerator", "SFXBlockingVolume_Ledge", "SFXAmmoContainer_Simulator", "SFXAmmoContainer", "SFXGrenadeContainer", "SFXCombatZone", "BioStartLocation", "BioStartLocationMP", "SFXStuntActor", "SkeletalMeshActor", "WwiseAmbientSound", "WwiseAudioVolume" };
        public string[] splineNodeClasses = { "SplineActor" };
        public string[] ignoredobjectnames = { "PREFAB_Ladders_3M_Arc0", "PREFAB_Ladders_3M_Arc1" }; //These come up as parsed classes but aren't actually part of the level, only prefabs. They should be ignored
        public bool ActorNodesActive = false;
        public bool PathfindingNodesActive = true;
        public bool StaticMeshCollectionActorNodesActive = false;
        private List<IExportEntry> AllLevelObjects = new List<IExportEntry>();
        public PathfindingEditor()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Pathfinding Editor", new WeakReference(this));

            AllowRefresh = true;
            classDatabasePath = Path.Combine(Application.StartupPath, "exec", "pathfindingclassdb.json");
            InitializeComponent();

            pathfindingMouseListener = new PathfindingMouseListener(this); //Must be member so we can release reference

            //Stuff that can't be done in designer view easily
            showVolumesInsteadOfNodesToolStripMenuItem.DropDown.Closing += new ToolStripDropDownClosingEventHandler(DropDown_Closing);
            ViewingModesMenuItem.DropDown.Closing += new ToolStripDropDownClosingEventHandler(DropDown_Closing);
            sFXCombatZonesToolStripMenuItem.DropDown.Closing += new ToolStripDropDownClosingEventHandler(DropDown_Closing);
            staticMeshCollectionActorsToolStripMenuItem.DropDown.Closing += new ToolStripDropDownClosingEventHandler(DropDown_Closing);

            //

            LoadRecentList();
            RefreshRecent(false);
            pathfindingNodeInfoPanel.PassPathfindingNodeEditorIn(this);
            graphEditor.BackColor = System.Drawing.Color.FromArgb(130, 130, 130);
            graphEditor.AddInputEventListener(pathfindingMouseListener);

            graphEditor.Click += graphEditor_Click;
            graphEditor.DragDrop += PathfindingEditor_DragDrop;
            graphEditor.DragEnter += PathfindingEditor_DragEnter;

            zoomController = new PathingZoomController(graphEditor);
            CurrentFilterType = HeightFilterForm.FILTER_Z_NONE;
            CurrentZFilterValue = 0;


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

        /// <summary>
        /// This method prevents checking a box in menu system from closing the menu, which can be annoying and tedious
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void DropDown_Closing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
            {
                e.Cancel = true;
            }
        }

        private void RefreshRecent(bool propogate, List<string> recents = null)
        {
            if (propogate && recents != null)
            {
                //we are posting an update to other instances of packed
                var forms = Application.OpenForms;
                foreach (Form form in forms)
                {
                    if (form is PathfindingEditor && this != form)
                    {
                        ((PathfindingEditor)form).RefreshRecent(false, RFiles);
                    }
                }
            }
            else if (recents != null)
            {
                //we are receiving an update
                RFiles = new List<string>(recents);
            }
            recentToolStripMenuItem.DropDownItems.Clear();
            if (RFiles.Count <= 0)
            {
                recentToolStripMenuItem.Enabled = false;
                return;
            }
            recentToolStripMenuItem.Enabled = true;

            foreach (string filepath in RFiles)
            {
                ToolStripMenuItem fr = new ToolStripMenuItem(filepath, null, RecentFile_click);
                recentToolStripMenuItem.DropDownItems.Add(fr);
            }
        }

        private void RecentFile_click(object sender, EventArgs e)
        {
            string s = sender.ToString();
            if (File.Exists(s))
            {
                LoadFile(s);
                AddRecent(s, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
            }
            else
            {
                MessageBox.Show("File does not exist: " + s);
            }
        }

        private void SaveRecentList()
        {
            if (!Directory.Exists(PathfindingEditorDataFolder))
            {
                Directory.CreateDirectory(PathfindingEditorDataFolder);
            }
            string path = PathfindingEditorDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
                File.Delete(path);
            File.WriteAllLines(path, RFiles);
        }

        private void PathfindingEditor_Load(object sender, EventArgs e)
        {
            interpreterWPF.hideHexBox();
        }

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
                AddRecent(d.FileName, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
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
                if (isFirstLoad && activeExportsListbox.Items.Count > 0)
                {
                    activeExportsListbox.SelectedIndex = 0;
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

        private bool LoadPathingNodesFromLevel()
        {
            if (pcc == null)
            {
                return false;
            }

            staticMeshCollectionActorsToolStripMenuItem.DropDownItems.Clear();
            staticMeshCollectionActorsToolStripMenuItem.Enabled = false;
            staticMeshCollectionActorsToolStripMenuItem.ToolTipText = "No StaticMeshCollectionActors found in this file";
            sFXCombatZonesToolStripMenuItem.DropDownItems.Clear();
            sFXCombatZonesToolStripMenuItem.Enabled = false;
            sFXCombatZonesToolStripMenuItem.ToolTipText = "No SFXCombatZones found in this file";
            sfxCombatZones = new List<int>();
            CurrentObjects = new List<int>();
            activeExportsListbox.Items.Clear();
            AllLevelObjects.Clear();
            foreach (IExportEntry exp in pcc.Exports)
            {
                if (exp.ClassName == "Level" && exp.ObjectName == "PersistentLevel")
                {
                    //Read persistent level binary
                    byte[] data = exp.Data;

                    //find start of class binary (end of props)
                    int start = exp.propsEnd();

                    //Console.WriteLine("Found start of binary at " + start.ToString("X8"));

                    uint exportid = BitConverter.ToUInt32(data, start);
                    start += 4;
                    uint numberofitems = BitConverter.ToUInt32(data, start);
                    int countoffset = start;

                    start += 4;
                    uint bioworldinfoexportid = BitConverter.ToUInt32(data, start);

                    IExportEntry bioworldinfo = pcc.Exports[(int)bioworldinfoexportid - 1];
                    if (bioworldinfo.ObjectName != "BioWorldInfo")
                    {
                        //INVALID!!
                        return false;
                    }
                    AllLevelObjects.Add(bioworldinfo);

                    start += 4;
                    uint shouldbezero = BitConverter.ToUInt32(data, start);
                    if (shouldbezero != 0 && pcc.Game != MEGame.ME1)
                    {
                        //INVALID!!!
                        return false;
                    }
                    int itemcount = 1; //Skip bioworldinfo and Class
                    if (pcc.Game != MEGame.ME1)
                    {
                        start += 4;
                        itemcount = 2;
                    }

                    while (itemcount < numberofitems)
                    {
                        //get header.
                        uint itemexportid = BitConverter.ToUInt32(data, start);
                        if (itemexportid - 1 < pcc.Exports.Count)
                        {
                            IExportEntry exportEntry = pcc.Exports[(int)itemexportid - 1];
                            AllLevelObjects.Add(exportEntry);

                            if (ignoredobjectnames.Contains(exportEntry.ObjectName))
                            {
                                start += 4;
                                itemcount++;
                                continue;
                            }

                            bool isParsedByExistingLayer = false;

                            if (pathfindingNodeClasses.Contains(exportEntry.ClassName))
                            {
                                isParsedByExistingLayer = true;
                                if (PathfindingNodesActive)
                                {
                                    CurrentObjects.Add(exportEntry.Index);
                                    activeExportsListbox.Items.Add("#" + (exportEntry.Index) + " " + exportEntry.ObjectName + "_" + exportEntry.indexValue + " - Class: " + exportEntry.ClassName);
                                }
                            }

                            if (actorNodeClasses.Contains(exportEntry.ClassName))
                            {
                                isParsedByExistingLayer = true;
                                if (ActorNodesActive)
                                {
                                    CurrentObjects.Add(exportEntry.Index);
                                    activeExportsListbox.Items.Add("#" + (exportEntry.Index) + " " + exportEntry.ObjectName + " - Class: " + exportEntry.ClassName);
                                }
                            }

                            if (splineNodeClasses.Contains(exportEntry.ClassName))
                            {
                                isParsedByExistingLayer = true;

                                if (SplineNodesActive)
                                {
                                    CurrentObjects.Add(exportEntry.Index);
                                    activeExportsListbox.Items.Add("#" + (exportEntry.Index) + " " + exportEntry.ObjectName + " - Class: " + exportEntry.ClassName);
                                    ArrayProperty<StructProperty> connectionsProp = exportEntry.GetProperty<ArrayProperty<StructProperty>>("Connections");
                                    if (connectionsProp != null)
                                    {
                                        foreach (StructProperty connectionProp in connectionsProp)
                                        {
                                            ObjectProperty splinecomponentprop = connectionProp.GetProp<ObjectProperty>("SplineComponent");
                                            IExportEntry splineComponentExport = pcc.getExport(splinecomponentprop.Value - 1);
                                            CurrentObjects.Add(splinecomponentprop.Value - 1);
                                            activeExportsListbox.Items.Add("#" + (splineComponentExport.Index) + " " + splineComponentExport.ObjectName + " - Class: " + splineComponentExport.ClassName);
                                        }
                                    }
                                }
                            }

                            //SFXCombatZone 
                            if (exportEntry.ClassName == "SFXCombatZone")
                            {
                                isParsedByExistingLayer = true;
                                sfxCombatZones.Add(exportEntry.Index);
                                ToolStripMenuItem combatZoneItem = new ToolStripMenuItem(exportEntry.Index + " " + exportEntry.ObjectName + "_" + exportEntry.indexValue);
                                combatZoneItem.ImageScaling = ToolStripItemImageScaling.None;
                                if (exportEntry.Index == ActiveCombatZoneExportIndex)
                                {
                                    combatZoneItem.Checked = true;
                                }
                                combatZoneItem.Click += (object o, EventArgs args) =>
                                {
                                    setSFXCombatZoneBGActive(combatZoneItem, exportEntry, combatZoneItem.Checked);
                                };
                                sFXCombatZonesToolStripMenuItem.DropDown.Items.Add(combatZoneItem);
                                sFXCombatZonesToolStripMenuItem.Enabled = true;
                                sFXCombatZonesToolStripMenuItem.ToolTipText = "Select a SFXCombatZone to highlight coverslots that are part of it";
                            }




                            //if (VisibleActorCollections.Contains(exportEntry.Index))
                            //{
                            if (exportEntry.ObjectName == "StaticMeshCollectionActor")
                            {
                                isParsedByExistingLayer = true;
                                ToolStripMenuItem collectionItem = new ToolStripMenuItem(exportEntry.Index + " " + exportEntry.ObjectName + "_" + exportEntry.indexValue);
                                collectionItem.ImageScaling = ToolStripItemImageScaling.None;
                                collectionItem.Click += (object o, EventArgs args) =>
                                {
                                    staticMeshCollectionActor_ToggleVisibility(collectionItem, exportEntry, collectionItem.Checked);
                                };
                                if (VisibleActorCollections.Contains(exportEntry.Index))
                                {
                                    byte[] smacData = exportEntry.Data;
                                    collectionItem.Checked = true;
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
                                                activeExportsListbox.Items.Add("#" + (exportEntry.Index) + " " + exportEntry.ObjectName + " - Class: " + exportEntry.ClassName);

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
                                staticMeshCollectionActorsToolStripMenuItem.DropDown.Items.Add(collectionItem);
                                staticMeshCollectionActorsToolStripMenuItem.Enabled = true;
                                staticMeshCollectionActorsToolStripMenuItem.ToolTipText = "Select a StaticMeshCollectionActor to add it to the editor";

                            }

                            if (EverythingElseActive && !isParsedByExistingLayer)
                            {
                                CurrentObjects.Add(exportEntry.Index);
                                activeExportsListbox.Items.Add("#" + (exportEntry.Index) + " " + exportEntry.ObjectName + " - Class: " + exportEntry.ClassName);
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

                    bool oneViewActive = PathfindingNodesActive || ActorNodesActive || EverythingElseActive;
                    if (oneViewActive && activeExportsListbox.Items.Count == 0)
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

        private void setSFXCombatZoneBGActive(ToolStripMenuItem combatZoneMenuItem, IExportEntry exportEntry, bool @checked)
        {
            combatZoneMenuItem.Checked = !combatZoneMenuItem.Checked;
            if (combatZoneMenuItem.Checked)
            {
                ActiveCombatZoneExportIndex = exportEntry.Index;
            }
            else
            {
                ActiveCombatZoneExportIndex = -1;
            }
            foreach (ToolStripMenuItem tsmi in sFXCombatZonesToolStripMenuItem.DropDownItems)
            {
                if (tsmi.Checked && tsmi != combatZoneMenuItem)
                {
                    tsmi.Checked = false; //uncheck other combat zones
                }
            }
            RefreshView();
            graphEditor.Invalidate();
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
            graphEditor.Invalidate();
        }

        public void RefreshView()
        {
            if (AllowRefresh)
            {
                int selectednodeindex = activeExportsListbox.SelectedIndex;
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
                    activeExportsListbox.SelectedIndex = selected;
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
                if (HighlightSequenceReferences)
                {

                    var referencemap = new Dictionary<int, List<int>>(); //node index mapped to list of things referencing it
                    foreach (IExportEntry export in pcc.Exports)
                    {
                        if (export.ClassName == "SFXSeqEvt_Touch" || export.ClassName.StartsWith("SeqVar") || export.ClassName.StartsWith("SFXSeq"))
                        {
                            var props = export.GetProperties();
                            var originator = props.GetProp<ObjectProperty>("Originator");
                            var objvalue = props.GetProp<ObjectProperty>("ObjValue");

                            if (originator != null)
                            {
                                var index = originator.Value - 1; //0-based indexing is used here
                                List<int> list;
                                if (!referencemap.TryGetValue(index, out list))
                                {
                                    list = new List<int>();
                                    referencemap[index] = list;
                                }
                                list.Add(export.UIndex);
                            }
                            if (objvalue != null)
                            {
                                var index = objvalue.Value - 1; //0-based indexing is used here
                                List<int> list;
                                if (!referencemap.TryGetValue(index, out list))
                                {
                                    list = new List<int>();
                                    referencemap[index] = list;
                                }
                                list.Add(export.UIndex);
                            }
                        }
                    }

                    //Generate refereneced count
                    for (int i = 0; i < CurrentObjects.Count; i++)
                    {
                        List<int> list;
                        if (referencemap.TryGetValue(CurrentObjects[i], out list))
                        {
                            //node is referenced
                            PathfindingNodeMaster nodeMaster = Objects.FirstOrDefault(o => o.Index == CurrentObjects[i]);
                            nodeMaster.comment.Text += "\nReferenced in " + list.Count() + " sequence object" + (list.Count() != 1 ? "s" : "");
                            foreach (int x in list)
                            {
                                nodeMaster.comment.Text += "\n" + x;
                            }
                        }
                    }
                    /*if (export.ClassName == "SFXSeqAct_AIFactory2")
                        {
                            PropertyCollection props = export.GetProperties();
                            var objCommentProp = props.GetProp<ArrayProperty<StrProperty>>("m_aObjComment");
                            string comment = "";
                            if (objCommentProp != null) {
                                foreach (string commentitem in objCommentProp)
                                {
                                    comment += commentitem + "\n";
                                }
                            }


                            List<string> currentReferences;
                        }*/
                }

                NodeTagListLoading = true;
                allTagsCombobox.Items.Clear();
                List<string> tags = new List<string>();
                foreach (PathfindingNodeMaster n in Objects)
                {
                    if (n.NodeTag != null && n.NodeTag != "")
                    {
                        tags.Add(n.NodeTag);
                    }
                }
                tags = tags.Distinct().ToList();
                tags.Sort();
                tags.Insert(0, "Node tags list");
                allTagsCombobox.Items.AddRange(tags.ToArray());
                allTagsCombobox.SelectedIndex = 0;
                NodeTagListLoading = false;
                /*foreach (IExportEntry export in pcc.Exports)
            {

            }
            if (sequenceObjectsReferencingThisItem.Count > 0)
            {
            ToolStripDropDown submenu = new ToolStripDropDown();
            foreach (IExportEntry referencing in sequenceObjectsReferencingThisItem)
            {

                ToolStripMenuItem breaklLinkItem = new ToolStripMenuItem(referencing.UIndex + " " + referencing.GetFullPath);
                breaklLinkItem.Click += (object o, EventArgs args) =>
                {
                    //sequence editor load
                    var editor = new SequenceEditor(referencing);
                    editor.BringToFront();
                    editor.Show();
                };
                submenu.Items.Add(breaklLinkItem);
            }

            exportsReferencingThisNodeToolStripMenuItem.Visible = true;
            exportsReferencingThisNodeToolStripMenuItem.DropDown = submenu;
            }*/

            }
            foreach (PNode o in Objects)
            {
                o.MouseDown += node_MouseDown;
            }
            return centerpoint;
        }

        public float StartPosEvents;
        public float StartPosActions;
        public float StartPosVars;
        private Dictionary<int, PointF> smacCoordinates;
        private bool IsReloadSelecting = false;
        private bool SplineNodesActive;
        private PathfindingNodeMaster CurrentlySelectedSplinePoint;
        private List<int> CurrentlyHighlightedCoverlinkNodes = new List<int>();
        private bool EverythingElseActive;
        private bool HighlightSequenceReferences;
        private bool NodeTagListLoading;
        private PathfindingMouseListener pathfindingMouseListener;

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
                            case "SFXNav_LeapNodeHumanoid":
                                pathNode = new PathfindingNodes.SFXNav_LeapNodeHumanoid(index, x, y, pcc, graphEditor);
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
                            case "PathNode_Dynamic":
                                pathNode = new PathfindingNodes.PathNode_Dynamic(index, x, y, pcc, graphEditor);
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

                            case "CoverSlotMarker":
                                pathNode = new PathfindingNodes.CoverSlotMarker(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXDynamicCoverSlotMarker":
                                pathNode = new PathfindingNodes.SFXDynamicCoverSlotMarker(index, x, y, pcc, graphEditor);
                                break;
                            case "MantleMarker":
                                pathNode = new PathfindingNodes.MantleMarker(index, x, y, pcc, graphEditor);
                                break;
                            case "TargetPoint":
                                pathNode = new PathfindingNodes.TargetPoint(index, x, y, pcc, graphEditor);
                                break;

                            case "SFXNav_HarvesterMoveNode":
                                pathNode = new PathfindingNodes.SFXNav_HarvesterMoveNode(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXOperation_ObjectiveSpawnPoint":
                                pathNode = new PathfindingNodes.SFXObjectiveSpawnPoint(index, x, y, pcc, graphEditor);

                                //Create annex node if required
                                var annexZoneLocProp = props.GetProp<ObjectProperty>("AnnexZoneLocation");
                                if (annexZoneLocProp != null)
                                {
                                    int ind = annexZoneLocProp.Value - 1;
                                    if (ind < 0 || ind > pcc.Exports.Count)
                                    {
                                        pathNode.comment.Text += "\nBAD ANNEXZONELOC!";
                                        pathNode.comment.TextBrush = new SolidBrush(System.Drawing.Color.Red);
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
                        if (ActiveCombatZoneExportIndex >= 0 && exporttoLoad.ClassName == "CoverSlotMarker")
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

                    else if (actorNodeClasses.Contains(exporttoLoad.ClassName))
                    {
                        ActorNode actorNode = null;
                        switch (exporttoLoad.ClassName)
                        {
                            case "BlockingVolume":
                                actorNode = new BlockingVolumeNode(index, x, y, pcc, graphEditor);
                                break;
                            case "DynamicBlockingVolume":
                                actorNode = new DynamicBlockingVolume(index, x, y, pcc, graphEditor);
                                break;
                            case "InterpActor":
                                actorNode = new InterpActorNode(index, x, y, pcc, graphEditor);
                                break;
                            case "BioTriggerVolume":
                                actorNode = new ActorNodes.BioTriggerVolume(index, x, y, pcc, graphEditor);
                                break;
                            case "BioTriggerStream":
                                actorNode = new ActorNodes.BioTriggerStream(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXGrenadeContainer":
                                actorNode = new ActorNodes.SFXGrenadeContainer(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXAmmoContainer":
                                actorNode = new ActorNodes.SFXAmmoContainer(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXAmmoContainer_Simulator":
                                actorNode = new ActorNodes.SFXAmmoContainer_Simulator(index, x, y, pcc, graphEditor);
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
                            case "StaticMeshActor":
                                actorNode = new ActorNodes.StaticMeshActorNode(index, x, y, pcc, graphEditor);
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
                            case "WwiseAmbientSound":
                                actorNode = new ActorNodes.WwiseAmbientSound(index, x, y, pcc, graphEditor);
                                break;
                            case "WwiseAudioVolume":
                                actorNode = new ActorNodes.WwiseAudioVolume(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXArmorNode":
                            case "SFXTreasureNode":
                                actorNode = new ActorNodes.SFXTreasureNode(index, x, y, pcc, graphEditor);
                                break;
                            case "SFXMedStation":
                                actorNode = new ActorNodes.SFXMedStation(index, x, y, pcc, graphEditor);
                                break;
                            default:
                                actorNode = new PendingActorNode(index, x, y, pcc, graphEditor);
                                break;
                        }



                        /* if (ActiveCombatZoneExportIndex >= 0)
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
                         }*/
                        Objects.Add(actorNode);
                        return;
                    }

                    else if (splineNodeClasses.Contains(exporttoLoad.ClassName))
                    {
                        SplineNode splineNode = null;
                        switch (exporttoLoad.ClassName)
                        {
                            case "SplineActor":
                                splineNode = new SplineActorNode(index, x, y, pcc, graphEditor);

                                ArrayProperty<StructProperty> connectionsProp = exporttoLoad.GetProperty<ArrayProperty<StructProperty>>("Connections");
                                if (connectionsProp != null)
                                {
                                    foreach (StructProperty connectionProp in connectionsProp)
                                    {
                                        ObjectProperty splinecomponentprop = connectionProp.GetProp<ObjectProperty>("SplineComponent");
                                        IExportEntry splineComponentExport = pcc.getExport(splinecomponentprop.Value - 1);
                                        //Debug.WriteLine(splineComponentExport.GetFullPath + " " + splinecomponentprop.Value);
                                        StructProperty splineInfo = splineComponentExport.GetProperty<StructProperty>("SplineInfo");
                                        if (splineInfo != null)
                                        {
                                            ArrayProperty<StructProperty> pointsProp = splineInfo.GetProp<ArrayProperty<StructProperty>>("Points");
                                            StructProperty point1 = pointsProp[0].GetProp<StructProperty>("OutVal");
                                            double xf = point1.GetProp<FloatProperty>("X");
                                            double yf = point1.GetProp<FloatProperty>("Y");
                                            //double zf = point1.GetProp<FloatProperty>("Z");
                                            //Point3D point1_3d = new Point3D(xf, yf, zf);
                                            SplinePoint0Node point0node = new SplinePoint0Node(splinecomponentprop.Value - 1, Convert.ToInt32(xf), Convert.ToInt32(yf), pcc, graphEditor);
                                            StructProperty point2 = pointsProp[1].GetProp<StructProperty>("OutVal");
                                            xf = point2.GetProp<FloatProperty>("X");
                                            yf = point2.GetProp<FloatProperty>("Y");
                                            //zf = point2.GetProp<FloatProperty>("Z");
                                            //Point3D point2_3d = new Point3D(xf, yf, zf);
                                            SplinePoint1Node point1node = new SplinePoint1Node(splinecomponentprop.Value - 1, Convert.ToInt32(xf), Convert.ToInt32(yf), pcc, graphEditor);
                                            point0node.SetDestinationPoint(point1node);

                                            Objects.Add(point0node);
                                            Objects.Add(point1node);

                                            StructProperty reparamProp = splineComponentExport.GetProperty<StructProperty>("SplineReparamTable");
                                            ArrayProperty<StructProperty> reparamPoints = reparamProp.GetProp<ArrayProperty<StructProperty>>("Points");
                                        }
                                    }
                                }
                                break;
                            default:
                                splineNode = new PendingSplineNode(index, x, y, pcc, graphEditor);
                                break;
                        }
                        Objects.Add(splineNode);
                        return;
                    }

                    else
                    {
                        //everything else
                        Objects.Add(new EverythingElseNode(index, x, y, pcc, graphEditor));
                        return;
                    }
                }
            }
            return; //hopefully won't see you
        }

        private void HighlightCoverlinkSlots(IExportEntry coverlink)
        {

            ArrayProperty<StructProperty> props = coverlink.GetProperty<ArrayProperty<StructProperty>>("Slots");
            if (props != null)
            {
                CurrentlyHighlightedCoverlinkNodes = new List<int>();
                CurrentlyHighlightedCoverlinkNodes.Add(coverlink.Index);

                foreach (StructProperty slot in props)
                {
                    ObjectProperty coverslot = slot.GetProp<ObjectProperty>("SlotMarker");
                    if (coverslot != null)
                    {
                        CurrentlyHighlightedCoverlinkNodes.Add(coverslot.Value - 1);
                    }
                }
                foreach (PathfindingNodeMaster pnm in Objects)
                {
                    if (pnm.export == coverlink)
                    {
                        pnm.shape.Brush = PathfindingNodeMaster.sfxCombatZoneBrush;
                        continue;
                    }
                    if (CurrentlyHighlightedCoverlinkNodes.Contains(pnm.export.Index))
                    {
                        pnm.shape.Brush = PathfindingNodeMaster.highlightedCoverSlotBrush;
                    }
                }
            }
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
                    if (edge.BezierPoints != null)
                    {
                        //Currently not implemented, will hopefully come in future update
                        PathingGraphEditor.UpdateEdgeBezier(edge);
                    }
                    else
                    {
                        PathingGraphEditor.UpdateEdgeStraight(edge);
                    }
                }
            }
        }

        public void GetProperties(IExportEntry export)
        {
            pathfindingNodeInfoPanel.LoadExport(export);
            interpreterWPF.LoadExport(export);
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
            int n = activeExportsListbox.SelectedIndex;
            if (n == -1 || n < 0 || n >= CurrentObjects.Count())
                return;

            //Clear coverlinknode highlighting.
            foreach (PathfindingNodeMaster pnm in Objects)
            {
                if (CurrentlyHighlightedCoverlinkNodes.Contains(pnm.export.Index))
                {
                    pnm.shape.Brush = pathfindingNodeBrush;
                }
            }
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

                switch (s.export.ClassName)
                {
                    case "CoverLink":
                        HighlightCoverlinkSlots(s.export);
                        break;
                    case "CoverSlotMarker":
                        StructProperty sp = s.export.GetProperty<StructProperty>("OwningSlot");
                        if (sp != null)
                        {
                            ObjectProperty op = sp.GetProp<ObjectProperty>("Link");
                            if (op != null && op.Value - 1 < pcc.ExportCount)
                            {
                                HighlightCoverlinkSlots(pcc.Exports[op.Value - 1]);
                            }
                        }
                        break;
                }
            }

            GetProperties(pcc.getExport(CurrentObjects[n]));
            selectedIndex = n;
            selectedByNode = false;
            graphEditor.Refresh();
            graphPropertiesSplitPanel.Panel2Collapsed = false;
        }

        private void PathfindingEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            //Nullify things to prevent memory leaks
            interpreterWPF.Dispose();
            interpreterWPFElementHost.Child = null;
            interpreterWPFElementHost.Dispose();
            rightMouseButtonMenu = null;
            contextMenuStrip2 = null;
            showVolumesInsteadOfNodesToolStripMenuItem = null;
            ViewingModesMenuItem = null;
            sFXCombatZonesToolStripMenuItem = null;
            staticMeshCollectionActorsToolStripMenuItem = null;
            graphEditor.RemoveInputEventListener(pathfindingMouseListener);
            graphEditor.Click -= graphEditor_Click;
            graphEditor.DragDrop -= PathfindingEditor_DragDrop;
            graphEditor.DragEnter -= PathfindingEditor_DragEnter;
            zoomController.Dispose();
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
                System.Drawing.RectangleF rr = r.GlobalFullBounds;
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

        private void LoadRecentList()
        {
            RFiles = new List<string>();
            RFiles.Clear();
            string path = PathfindingEditorDataFolder + RECENTFILES_FILE;
            recentToolStripMenuItem.Enabled = false;
            if (File.Exists(path))
            {
                string[] recents = File.ReadAllLines(path);
                foreach (string recent in recents)
                {
                    if (File.Exists(recent))
                    {
                        recentToolStripMenuItem.Enabled = true;
                        AddRecent(recent, true);
                    }
                }
            }
        }

        public void AddRecent(string s, bool loadingList)
        {
            RFiles = RFiles.Where(x => !x.Equals(s, StringComparison.InvariantCultureIgnoreCase)).ToList();
            if (loadingList)
            {
                RFiles.Add(s); //in order
            }
            else
            {
                RFiles.Insert(0, s); //put at front
            }
            if (RFiles.Count > 10)
            {
                RFiles.RemoveRange(10, RFiles.Count - 10);
            }
            recentToolStripMenuItem.Enabled = true;
        }

        protected void node_MouseDown(object sender, PInputEventArgs e)
        {
            PathfindingNodeMaster node = (PathfindingNodeMaster)sender;
            int n = node.Index;
            foreach (PathfindingNodeMaster pfm in Objects)
            {
                pfm.Deselect();
            }
            int selected = CurrentObjects.IndexOf(n);
            if (selected == -1)
                return;
            activeExportsListbox.SelectedIndex = selected;
            if ((node is SplinePoint0Node) || (node is SplinePoint1Node))
            {
                node.Select();
            }
            CurrentlySelectedSplinePoint = null;
            selectedByNode = true;
            if (e.Button == MouseButtons.Right)
            {
                node.Select();
                addToSFXCombatZoneToolStripMenuItem.DropDownItems.Clear();
                breakLinksToolStripMenuItem.DropDownItems.Clear();
                breakLinksToolStripMenuItem.Visible = false;
                setGraphPositionAsNodeLocationToolStripMenuItem.Visible = true;
                setGraphPositionAsSplineLocationXYToolStripMenuItem.Visible = false;
                openInCurveEditorToolStripMenuItem.Visible = false;
                cloneToolStripMenuItem.Visible = false;
                changeNodeTypeToolStripMenuItem.Visible = false;
                createReachSpecToolStripMenuItem.Visible = false;
                generateNewRandomGUIDToolStripMenuItem.Visible = false;
                addToSFXCombatZoneToolStripMenuItem.Visible = false;
                exportsReferencingThisNodeToolStripMenuItem.DropDownItems.Clear();
                exportsReferencingThisNodeToolStripMenuItem.Visible = false;
                IExportEntry nodeExp = pcc.Exports[n];
                var properties = nodeExp.GetProperties();
                List<IExportEntry> sequenceObjectsReferencingThisItem = new List<IExportEntry>();

                #region SequenceEditorReferences
                foreach (IExportEntry export in pcc.Exports)
                {
                    if (export.ClassName == "SFXSeqEvt_Touch" || export.ClassName.StartsWith("SeqVar") || export.ClassName.StartsWith("SFXSeq"))
                    {
                        var props = export.GetProperties();
                        var originator = props.GetProp<ObjectProperty>("Originator");
                        var objvalue = props.GetProp<ObjectProperty>("ObjValue");

                        if (originator != null && originator.Value == node.export.UIndex)
                        {
                            sequenceObjectsReferencingThisItem.Add(export);
                        }
                        if (objvalue != null && objvalue.Value == node.export.UIndex)
                        {
                            sequenceObjectsReferencingThisItem.Add(export);
                        }
                    }
                }
                if (sequenceObjectsReferencingThisItem.Count > 0)
                {
                    ToolStripDropDown submenu = new ToolStripDropDown();
                    foreach (IExportEntry referencing in sequenceObjectsReferencingThisItem)
                    {

                        ToolStripMenuItem breaklLinkItem = new ToolStripMenuItem(referencing.UIndex + " " + referencing.GetFullPath);
                        breaklLinkItem.Click += (object o, EventArgs args) =>
                        {
                            //sequence editor load
                            var editor = new SequenceEditor(referencing);
                            editor.BringToFront();
                            editor.Show();
                        };
                        submenu.Items.Add(breaklLinkItem);
                    }

                    exportsReferencingThisNodeToolStripMenuItem.Visible = true;
                    exportsReferencingThisNodeToolStripMenuItem.DropDown = submenu;
                }
                #endregion

                if (node is PathfindingNode)
                {
                    breakLinksToolStripMenuItem.Visible = true;

                    cloneToolStripMenuItem.Visible = true;
                    changeNodeTypeToolStripMenuItem.Visible = true;
                    createReachSpecToolStripMenuItem.Visible = true;
                    generateNewRandomGUIDToolStripMenuItem.Visible = true;
                    if (node.export.ClassName == "CoverSlotMarker")
                    {
                        addToSFXCombatZoneToolStripMenuItem.Enabled = sfxCombatZones.Count > 0;
                        ToolStripDropDown combatZonesDropdown = new ToolStripDropDown();
                        ToolStripMenuItem combatZoneItem;
                        ArrayProperty<StructProperty> volumes = node.export.GetProperty<ArrayProperty<StructProperty>>("Volumes");

                        foreach (int expid in sfxCombatZones)
                        {
                            bool isAlreadyPartOfCombatZone = false;
                            if (volumes != null)
                            {
                                foreach (StructProperty actorRef in volumes)
                                {
                                    var actor = actorRef.GetProp<ObjectProperty>("Actor");
                                    if (actor.Value == expid + 1)
                                    {
                                        Debug.WriteLine("part of combat zone already: " + actor.Value);
                                        isAlreadyPartOfCombatZone = true;
                                        break;
                                    }
                                }
                            }
                            if (isAlreadyPartOfCombatZone)
                            {
                                continue; //go to next entry
                            }
                            Debug.WriteLine("not part of combat zone already: " + expid);

                            IExportEntry combatZoneExp = pcc.Exports[expid];
                            combatZoneItem = new ToolStripMenuItem(combatZoneExp.Index + " " + combatZoneExp.ObjectName + "_" + combatZoneExp.indexValue);
                            combatZoneItem.Click += (object o, EventArgs args) =>
                            {
                                //removeReachSpec(nodeExp, outgoingSpec);
                                addCombatZoneRef(nodeExp, combatZoneExp);
                            };
                            combatZonesDropdown.Items.Add(combatZoneItem);
                        }
                        addToSFXCombatZoneToolStripMenuItem.Visible = true;
                        addToSFXCombatZoneToolStripMenuItem.DropDown = combatZonesDropdown;
                    }

                    rightMouseButtonMenu.Show(MousePosition);
                    //open in InterpEditor
                    string className = pcc.getExport(((PathfindingNodes.PathfindingNode)sender).Index).ClassName;
                    //break links
                    breakLinksToolStripMenuItem.Visible = false;
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
                            ObjectProperty outgoingSpecEndProp = outgoingEndStructProp.Properties.GetProp<ObjectProperty>(SharedPathfinding.GetReachSpecEndName(outgoingSpec)); //END                    

                            IEntry dest = pcc.getEntry(outgoingSpecEndProp.Value);
                            breaklLinkItem = new ToolStripMenuItem("Break " + outgoingSpec.ObjectName + " to " + (outgoingSpecEndProp.Value - 1) + " " + dest.ObjectName);
                            breaklLinkItem.Click += (object o, EventArgs args) =>
                            {
                                removeReachSpec(nodeExp, outgoingSpec);
                            };
                            submenu.Items.Add(breaklLinkItem);
                        }

                        breakLinksToolStripMenuItem.Visible = true;
                        breakLinksToolStripMenuItem.DropDown = submenu;
                    }
                }
                else if (node is ActorNode)
                {
                    cloneToolStripMenuItem.Visible = true;
                    addToSFXCombatZoneToolStripMenuItem.DropDown = null;
                    breakLinksToolStripMenuItem.DropDown = null;
                    rightMouseButtonMenu.Show(MousePosition);

                    //if (node is ActorNodes.BioTriggerVolume)
                    //{



                    //}

                    if (node is SplinePoint0Node || node is SplinePoint1Node)
                    {
                        setGraphPositionAsNodeLocationToolStripMenuItem.Visible = false;
                        setGraphPositionAsSplineLocationXYToolStripMenuItem.Visible = true;
                        openInCurveEditorToolStripMenuItem.Visible = true;
                        CurrentlySelectedSplinePoint = node;
                        addToSFXCombatZoneToolStripMenuItem.DropDown = null;
                        breakLinksToolStripMenuItem.DropDown = null;
                        rightMouseButtonMenu.Show(MousePosition);
                    }
                    else if (node is SplineNode)
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
        }

        private void addCombatZoneRef(IExportEntry nodeExp, IExportEntry combatZoneExp)
        {
            //Adds a combat zone to the list of Volumes. Creates Volumes if it doesnt exist yet. Currently does not check if the item already exists as part of that combat zone.
            PropertyCollection props = nodeExp.GetProperties();
            ArrayProperty<StructProperty> volumes = props.GetProp<ArrayProperty<StructProperty>>("Volumes");
            if (volumes == null)
            {
                //we need to add it as a property
                volumes = new ArrayProperty<StructProperty>(ArrayType.Struct, "Volumes");
                props.Add(volumes);
            }

            StructProperty newVolumeProperty = new StructProperty("ActorReference", true);
            StructProperty newGUIDProperty = new StructProperty("ActorReference", true);
            newGUIDProperty.Name = "Guid";

            StructProperty guid = combatZoneExp.GetProperty<StructProperty>("CombatZoneGuid");
            newGUIDProperty.Properties.Add(new IntProperty(guid.GetProp<IntProperty>("A"), "A"));
            newGUIDProperty.Properties.Add(new IntProperty(guid.GetProp<IntProperty>("B"), "B"));
            newGUIDProperty.Properties.Add(new IntProperty(guid.GetProp<IntProperty>("C"), "C"));
            newGUIDProperty.Properties.Add(new IntProperty(guid.GetProp<IntProperty>("D"), "D"));
            newVolumeProperty.Properties.Add(newGUIDProperty);
            var newActorProperty = new ObjectProperty(combatZoneExp.UIndex, "Actor");
            newVolumeProperty.Properties.Add(newActorProperty);
            volumes.Add(newVolumeProperty);
            nodeExp.WriteProperty(volumes);
            return;
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
        public int ActiveCombatZoneExportIndex { get; set; }
        public int CurrentFilterType { get; private set; }
        public int CurrentZFilterValue { get; private set; }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            graphEditor.ScaleViewTo((float)Convert.ToDecimal(toolStripTextBox1.Text));
        }

        private void openInPackageEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (activeExportsListbox.SelectedIndex < 0)
            {
                return;
            }
            int l = CurrentObjects[activeExportsListbox.SelectedIndex];
            if (l == -1)
                return;
            PackageEditor p = new PackageEditor();
            p.Show();
            p.LoadFile(CurrentFile);
            p.goToNumber(l);
        }

        private void pg1_PropertyValueChanged(object o, PropertyValueChangedEventArgs e)
        {

            int n = activeExportsListbox.SelectedIndex;
            if (n == -1)
                return;
            PropGrid.propGridPropertyValueChanged(e, CurrentObjects[n], pcc);
        }

        private void togglePathfindingNodes_Click(object sender, EventArgs e)
        {
            int preCount = CurrentObjects.Count;
            PathfindingNodesActive = togglePathfindingNodes.Checked;
            RefreshView();
            int postCount = CurrentObjects.Count;
            if (preCount == 0 && postCount != 0)
            {
                graphPropertiesSplitPanel.Panel2Collapsed = false;
            }
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
                AddRecent(DroppedFiles[0], false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
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
            d.FileName = Path.GetFileName(pcc.FileName);
            if (d.ShowDialog() == DialogResult.OK)
            {
                pcc.save(d.FileName);
                MessageBox.Show("Done");
            }
        }

        void cloneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = CurrentObjects[activeExportsListbox.SelectedIndex];
            if (n == -1)
                return;

            AllowRefresh = false;
            IExportEntry nodeEntry = pcc.Exports[n];
            cloneNode(nodeEntry);
            AllowRefresh = true;

            //RefreshView();
        }

        private IExportEntry cloneNode(IExportEntry nodeEntry)
        {
            ObjectProperty collisionComponentProperty = nodeEntry.GetProperty<ObjectProperty>("CollisionComponent");
            IExportEntry collisionEntry = nodeEntry.FileRef.Exports[collisionComponentProperty.Value - 1];

            int newNodeIndex = nodeEntry.FileRef.Exports.Count;
            int newCollisionIndex = newNodeIndex + 1;

            nodeEntry.FileRef.addExport(nodeEntry.Clone());
            nodeEntry.FileRef.addExport(collisionEntry.Clone());

            IExportEntry newNodeEntry = nodeEntry.FileRef.Exports[newNodeIndex];
            IExportEntry newCollisionEntry = nodeEntry.FileRef.Exports[newCollisionIndex];
            newCollisionEntry.idxLink = newNodeEntry.UIndex;

            //empty the pathlist
            ArrayProperty<ObjectProperty> PathList = newNodeEntry.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
            if (PathList != null)
            {
                PathList.Clear();
                newNodeEntry.WriteProperty(PathList);
            }

            //reuse
            PropertyCollection newExportProps = newNodeEntry.GetProperties();
            bool changed = false;
            foreach (UProperty prop in newExportProps)
            {
                if (prop is ObjectProperty)
                {
                    var objProp = prop as ObjectProperty;
                    if (objProp.Value == collisionEntry.UIndex)
                    {
                        objProp.Value = newCollisionEntry.UIndex;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                newNodeEntry.WriteProperties(newExportProps);
            }
            /*
            collisionComponentProperty = newNodeEntry.GetProperty<ObjectProperty>("CollisionComponent");
            if (collisionComponentProperty != null)
            {
                collisionComponentProperty.Value = newCollisionEntry.UIndex;
                newNodeEntry.WriteProperty(collisionComponentProperty);
            }

            collisionComponentProperty = newNodeEntry.GetProperty<ObjectProperty>("CylinderComponent");
            if (collisionComponentProperty != null)
            {
                collisionComponentProperty.Value = newCollisionEntry.UIndex;
                newNodeEntry.WriteProperty(collisionComponentProperty);

            }*/

            SharedPathfinding.GenerateNewRandomGUID(newNodeEntry);
            //Add cloned node to persistentlevel
            IExportEntry persistentlevel = null;
            foreach (IExportEntry exp in nodeEntry.FileRef.Exports)
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
            int start = persistentlevel.propsEnd();

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
            return newNodeEntry;
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
                    {
                        exportclassdbkey = "SFXNav_BoostNode";
                        BoolProperty bTopNode = new BoolProperty(true, "bTopNode");
                        propertiesToAdd.Add(bTopNode);
                        propertiesToRemoveIfPresent.Add("JumpDownDest");
                    }
                    break;
                case NODETYPE_SFXNAV_BOOSTNODE_BOTTOM:
                    exportclassdbkey = "SFXNav_BoostNode";
                    propertiesToRemoveIfPresent.Add("bTopNode");
                    propertiesToRemoveIfPresent.Add("JumpDownDest");
                    break;
                case NODETYPE_SFXNAV_JUMPDOWNNODE_TOP:
                    {
                        exportclassdbkey = "SFXNav_JumpDownNode";
                        BoolProperty bTopNode = new BoolProperty(true, "bTopNode");
                        propertiesToAdd.Add(bTopNode);
                        propertiesToRemoveIfPresent.Add("BoostDest");
                    }
                    break;
                case NODETYPE_SFXNAV_JUMPDOWNNODE_BOTTOM:
                    exportclassdbkey = "SFXNav_JumpDownNode";
                    propertiesToRemoveIfPresent.Add("bTopNode");
                    propertiesToRemoveIfPresent.Add("BoostDest");
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
                                            if (rprop.Name == SharedPathfinding.GetReachSpecEndName(spec))
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
                                                        if (rprop.Name == SharedPathfinding.GetReachSpecEndName(inboundSpec))
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
                if (objectname == export.ObjectName && export.ClassName != "Class")
                {
                    export.indexValue = index;
                    index++;
                }
            }
        }

        private void toSFXEnemySpawnPointToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (activeExportsListbox.SelectedIndex >= 0)
            {
                int n = CurrentObjects[activeExportsListbox.SelectedIndex];
                if (n == -1)
                    return;

                if (pcc.Exports[n].ClassName != "SFXEnemySpawnPoint")
                {
                    changeNodeType(pcc.Exports[n], NODETYPE_SFXENEMYSPAWNPOINT);
                    RefreshView();
                }
            }
        }

        private void toSFXNavTurretPointToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (activeExportsListbox.SelectedIndex >= 0)
            {
                int n = CurrentObjects[activeExportsListbox.SelectedIndex];
                if (n == -1)
                    return;

                if (pcc.Exports[n].ClassName != "SFXNav_TurretPoint")
                {
                    changeNodeType(pcc.Exports[n], NODETYPE_SFXNAV_TURRETPOINT);
                    RefreshView();
                }
            }
        }

        private void toPathNodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (activeExportsListbox.SelectedIndex >= 0)
            {
                int n = CurrentObjects[activeExportsListbox.SelectedIndex];
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
                mostdownstreamimport.idxPackageFile = downstreamPackageName;
                pcc.addImport(mostdownstreamimport);
                upstreamImport = mostdownstreamimport;
            }
            return mostdownstreamimport;
        }

        private void createReachSpecToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (activeExportsListbox.SelectedIndex < 0)
            {
                return;
            }
            int sourceExportIndex = CurrentObjects[activeExportsListbox.SelectedIndex];
            if (sourceExportIndex == -1)
                return;



            string reachSpecClass = "";
            int destinationIndex = -1;
            bool createTwoWay = true;
            int size = 1; //Minibosses by default
            int destinationType = 0; //local by default
            using (ReachSpecCreatorForm form = new ReachSpecCreatorForm(pcc, sourceExportIndex))
            {
                DialogResult dr = form.ShowDialog(this);
                if (dr != DialogResult.Yes)
                {
                    return; //user cancel
                }

                createTwoWay = form.CreateTwoWaySpec;
                destinationIndex = form.DestinationNode;
                reachSpecClass = form.SpecClass;
                size = form.SpecSize;
                destinationType = form.DestinationType;
            }
            IExportEntry startNode = pcc.Exports[sourceExportIndex];
            createReachSpec(startNode, createTwoWay, destinationIndex, reachSpecClass, size, destinationType);
        }

        private void createReachSpec(IExportEntry startNode, bool createTwoWay, int destinationIndex, string reachSpecClass, int size, int destinationType)
        {
            //func
            IExportEntry reachSpectoClone = null;
            foreach (IExportEntry exp in pcc.Exports)
            {
                if (exp.ClassName == "ReachSpec") //clone basic reachspec, set class later
                {
                    reachSpectoClone = exp;
                    break;
                }
            }
            if (destinationType == 1) //EXTERNAL
            {
                //external node

                //Debug.WriteLine("Num Exports: " + pcc.Exports.Count);
                int outgoingSpec = pcc.ExportCount;
                int incomingSpec = pcc.ExportCount + 1;


                if (reachSpectoClone != null)
                {
                    pcc.addExport(reachSpectoClone.Clone()); //outgoing

                    IExportEntry outgoingSpecExp = pcc.Exports[outgoingSpec]; //cloned outgoing
                    ImportEntry reachSpecClassImp = getOrAddImport(reachSpecClass); //new class type.

                    outgoingSpecExp.idxClass = reachSpecClassImp.UIndex;
                    outgoingSpecExp.idxObjectName = reachSpecClassImp.idxObjectName;

                    ObjectProperty outgoingSpecStartProp = outgoingSpecExp.GetProperty<ObjectProperty>("Start"); //START
                    StructProperty outgoingEndStructProp = outgoingSpecExp.GetProperty<StructProperty>("End"); //Embeds END
                    ObjectProperty outgoingSpecEndProp = outgoingEndStructProp.Properties.GetProp<ObjectProperty>(SharedPathfinding.GetReachSpecEndName(outgoingSpecExp)); //END
                    outgoingSpecStartProp.Value = startNode.UIndex;
                    outgoingSpecEndProp.Value = 0; //we will have to set the GUID - maybe through form or something


                    //Add to source node prop
                    ArrayProperty<ObjectProperty> PathList = startNode.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                    byte[] memory = startNode.Data;
                    memory = addObjectArrayLeaf(memory, (int)PathList.ValueOffset, outgoingSpecExp.UIndex);
                    startNode.Data = memory;
                    outgoingSpecExp.WriteProperty(outgoingSpecStartProp);
                    outgoingSpecExp.WriteProperty(outgoingEndStructProp);

                    //Write Spec Size
                    int radVal = -1;
                    int heightVal = -1;

                    System.Drawing.Point sizePair = PathfindingNodeInfoPanel.getDropdownSizePair(size);
                    radVal = sizePair.X;
                    heightVal = sizePair.Y;
                    setReachSpecSize(outgoingSpecExp, radVal, heightVal);

                    //Reindex reachspecs.
                    reindexObjectsWithName(reachSpecClass);
                }
            }
            else
            {
                //Debug.WriteLine("Source Node: " + startNode.Index);

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

                    IExportEntry outgoingSpecExp = pcc.Exports[outgoingSpec]; //cloned outgoing
                    ImportEntry reachSpecClassImp = getOrAddImport(reachSpecClass); //new class type.

                    outgoingSpecExp.idxClass = reachSpecClassImp.UIndex;
                    outgoingSpecExp.idxObjectName = reachSpecClassImp.idxObjectName;

                    if (reachSpecClass == "Engine.SlotToSlotReachSpec")
                    {
                        var props = outgoingSpecExp.GetProperties();
                        props.Add(new ByteProperty(1, "SpecDirection"));
                        outgoingSpecExp.WriteProperties(props);
                    }

                    //Debug.WriteLine("Outgoing UIndex: " + outgoingSpecExp.UIndex);

                    ObjectProperty outgoingSpecStartProp = outgoingSpecExp.GetProperty<ObjectProperty>("Start"); //START
                    StructProperty outgoingEndStructProp = outgoingSpecExp.GetProperty<StructProperty>("End"); //Embeds END
                    ObjectProperty outgoingSpecEndProp = outgoingEndStructProp.Properties.GetProp<ObjectProperty>(SharedPathfinding.GetReachSpecEndName(outgoingSpecExp)); //END
                    outgoingSpecStartProp.Value = startNode.UIndex;
                    outgoingSpecEndProp.Value = destNode.UIndex;


                    //Add to source node prop
                    ArrayProperty<ObjectProperty> PathList = startNode.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                    byte[] memory = startNode.Data;
                    memory = addObjectArrayLeaf(memory, (int)PathList.ValueOffset, outgoingSpecExp.UIndex);
                    startNode.Data = memory;
                    outgoingSpecExp.WriteProperty(outgoingSpecStartProp);
                    outgoingSpecExp.WriteProperty(outgoingEndStructProp);

                    //Write Spec Size
                    int radVal = -1;
                    int heightVal = -1;

                    System.Drawing.Point sizePair = PathfindingNodeInfoPanel.getDropdownSizePair(size);
                    radVal = sizePair.X;
                    heightVal = sizePair.Y;
                    setReachSpecSize(outgoingSpecExp, radVal, heightVal);


                    if (createTwoWay)
                    {
                        IExportEntry incomingSpecExp = pcc.Exports[incomingSpec];
                        incomingSpecExp.idxClass = reachSpecClassImp.UIndex;
                        incomingSpecExp.idxObjectName = reachSpecClassImp.idxObjectName;

                        if (reachSpecClass == "Engine.SlotToSlotReachSpec")
                        {
                            var props = incomingSpecExp.GetProperties();
                            props.Add(new ByteProperty(2, "SpecDirection"));
                            incomingSpecExp.WriteProperties(props);
                        }

                        ObjectProperty incomingSpecStartProp = incomingSpecExp.GetProperty<ObjectProperty>("Start"); //START
                        StructProperty incomingEndStructProp = incomingSpecExp.GetProperty<StructProperty>("End"); //Embeds END
                        ObjectProperty incomingSpecEndProp = incomingEndStructProp.Properties.GetProp<ObjectProperty>(SharedPathfinding.GetReachSpecEndName(incomingSpecExp)); //END

                        incomingSpecStartProp.Value = destNode.UIndex;//Uindex
                        incomingSpecEndProp.Value = startNode.UIndex;


                        //Add to source node prop
                        ArrayProperty<ObjectProperty> DestPathList = pcc.Exports[destinationIndex].GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                        memory = destNode.Data;
                        memory = addObjectArrayLeaf(memory, (int)DestPathList.ValueOffset, incomingSpecExp.UIndex);
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
            int sourceExportIndex = CurrentObjects[activeExportsListbox.SelectedIndex];
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
            if (activeExportsListbox.SelectedIndex >= 0)
            {
                int n = CurrentObjects[activeExportsListbox.SelectedIndex];
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
            if (activeExportsListbox.SelectedIndex >= 0)
            {
                int n = CurrentObjects[activeExportsListbox.SelectedIndex];
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
            int n = CurrentObjects[activeExportsListbox.SelectedIndex];
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
            int preCount = CurrentObjects.Count;
            ActorNodesActive = toggleActorNodes.Checked;
            RefreshView();
            int postCount = CurrentObjects.Count;
            if (preCount == 0 && postCount != 0)
            {
                graphPropertiesSplitPanel.Panel2Collapsed = false;
            }
        }

        public static NodeType getType(string s)
        {
            int ret = -1;
            for (int i = 0; i < Types.Length; i++)
                if (s == Types[i])
                    ret = i;
            return (NodeType)ret;
        }

        public static int findEndOfProps(IExportEntry export)
        {
            return export.propsEnd();
            //IMEPackage pcc = export.FileRef;
            //byte[] memory = export.Data;
            //int readerpos = export.GetPropertyStart();
            //bool run = true;
            //while (run)
            //{
            //    PropHeader p = new PropHeader();
            //    if (readerpos > memory.Length || readerpos < 0)
            //    {
            //        //nothing else to interpret.
            //        run = false;
            //        readerpos = -1;
            //        continue;
            //    }
            //    p.name = BitConverter.ToInt32(memory, readerpos);
            //    if (!pcc.isName(p.name))
            //        run = false;
            //    else
            //    {
            //        if (pcc.getNameEntry(p.name) != "None")
            //        {
            //            p.type = BitConverter.ToInt32(memory, readerpos + 8);
            //            nodeType type = getType(pcc.getNameEntry(p.type));
            //            bool isUnknownType = type == nodeType.Unknown;
            //            if (!pcc.isName(p.type) || isUnknownType)
            //                run = false;
            //            else
            //            {
            //                p.size = BitConverter.ToInt32(memory, readerpos + 16);
            //                readerpos += p.size + 24;

            //                if (getType(pcc.getNameEntry(p.type)) == nodeType.StructProperty) //StructName
            //                    readerpos += 8;
            //                if (pcc.Game == MEGame.ME3)
            //                {
            //                    if (getType(pcc.getNameEntry(p.type)) == nodeType.BoolProperty)//Boolbyte
            //                        readerpos++;
            //                    if (getType(pcc.getNameEntry(p.type)) == nodeType.ByteProperty)//byteprop
            //                        readerpos += 8;
            //                }
            //                else
            //                {
            //                    if (getType(pcc.getNameEntry(p.type)) == nodeType.BoolProperty)
            //                        readerpos += 4;
            //                }
            //            }
            //        }
            //        else
            //        {
            //            readerpos += 8;
            //            run = false;
            //        }
            //    }
            //}
            //return readerpos;
        }

        private void toSFXNavLargeBoostNodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (activeExportsListbox.SelectedIndex >= 0)
            {
                int n = CurrentObjects[activeExportsListbox.SelectedIndex];
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
                    filterByZToolStripMenuItem.Checked = true;
                    filenameLabel.Text = Path.GetFileName(CurrentFile) + " | Hiding nodes " + (CurrentFilterType == HeightFilterForm.FILTER_Z_ABOVE ? "above" : "below") + " Z = " + CurrentZFilterValue;
                }
                else
                {
                    filterByZToolStripMenuItem.Checked = false;
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
            if (activeExportsListbox.SelectedIndex >= 0)
            {
                int n = CurrentObjects[activeExportsListbox.SelectedIndex];
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
            if (activeExportsListbox.SelectedIndex >= 0)
            {
                int n = CurrentObjects[activeExportsListbox.SelectedIndex];
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
            fixStackHeaders(true);
        }

        private void fixStackHeaders(bool showUI)
        {
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
            if (showUI)
            {
                MessageBox.Show(this, numUpdated + " export" + (numUpdated != 1 ? "s" : "") + " in PersistentLevel had data headers updated.", "Header Check Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
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

                            activeExportsListbox.SelectedIndex = index;
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
            relinkingPathfindingChain();
        }

        private void relinkingPathfindingChain()
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
                        SharedPathfinding.WriteMem(expData, (int)nextNav.ValueOffset, BitConverter.GetBytes(nextchainItem.UIndex));
                        chainItem.Data = expData;
                        Debug.WriteLine(chainItem.Index + " Chain link -> " + nextchainItem.UIndex);
                    }

                    MessageBox.Show("NavigationPoint chain has been updated.");
                }
            }
        }

        private void toBioPathPointToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (activeExportsListbox.SelectedIndex >= 0)
            {
                int n = CurrentObjects[activeExportsListbox.SelectedIndex];
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
            if (activeExportsListbox.SelectedIndex >= 0)
            {
                int n = CurrentObjects[activeExportsListbox.SelectedIndex];
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
            if (activeExportsListbox.SelectedIndex >= 0)
            {
                int n = CurrentObjects[activeExportsListbox.SelectedIndex];
                if (n == -1)
                    return;

                if (pcc.Exports[n].ClassName != "SFXDynamicCovertSlotMarker")
                {
                    changeNodeType(pcc.Exports[n], NODETYPE_SFXDYNAMICCOVERSLOTMARKER);
                    RefreshView();
                }
            }
        }

        private void flipLevelUpsidedownEXPERIMENTALToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (IExportEntry exp in pcc.Exports)
            {
                switch (exp.ObjectName)
                {
                    case "StaticMeshCollectionActor":
                        {
                            //This is going to get ugly.

                            byte[] data = exp.Data;
                            //get a list of staticmesh stuff from the props.
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
                            int start = findEndOfProps(exp);

                            if (data.Length - start < 4)
                            {
                                return;
                            }

                            //Lets make sure this binary is divisible by 64.
                            if ((data.Length - start) % 64 != 0)
                            {
                                return;
                            }

                            int smcaindex = 0;
                            while (start < data.Length && smcaindex < listsize - 1)
                            {
                                float x = BitConverter.ToSingle(data, start + smcaindex * 64 + (12 * 4));
                                float y = BitConverter.ToSingle(data, start + smcaindex * 64 + (13 * 4));
                                float z = BitConverter.ToSingle(data, start + smcaindex * 64 + (14 * 4));
                                data = SharedPathfinding.WriteMem(data, start + smcaindex * 64 + (12 * 4), BitConverter.GetBytes(x * -1));
                                data = SharedPathfinding.WriteMem(data, start + smcaindex * 64 + (13 * 4), BitConverter.GetBytes(y * -1));
                                data = SharedPathfinding.WriteMem(data, start + smcaindex * 64 + (14 * 4), BitConverter.GetBytes(z * -1));

                                InvertScalingOnExport(smacitems[smcaindex], "Scale3D");
                                smcaindex++;
                                Debug.WriteLine(exp.Index + " " + smcaindex + " SMAC Flipping " + x + "," + y + "," + z);
                            }
                            exp.Data = data;
                        }
                        break;
                    default:
                        {
                            var props = exp.GetProperties();
                            StructProperty locationProp = props.GetProp<StructProperty>("location");
                            if (locationProp != null)
                            {
                                FloatProperty xProp = locationProp.Properties.GetProp<FloatProperty>("X");
                                FloatProperty yProp = locationProp.Properties.GetProp<FloatProperty>("Y");
                                FloatProperty zProp = locationProp.Properties.GetProp<FloatProperty>("Z");
                                Debug.WriteLine(exp.Index + " " + exp.ObjectName + "Flipping " + xProp.Value + "," + yProp.Value + "," + zProp.Value);

                                xProp.Value = xProp.Value * -1;
                                yProp.Value = yProp.Value * -1;
                                zProp.Value = zProp.Value * -1;

                                exp.WriteProperty(locationProp);
                                InvertScalingOnExport(exp, "DrawScale3D");
                            }
                            break;
                        }
                }
            }
            MessageBox.Show("Items flipped.", "Flipping complete");
        }

        private void InvertScalingOnExport(IExportEntry exp, string propname)
        {
            var drawScale3D = exp.GetProperty<StructProperty>(propname);
            bool hasDrawScale = drawScale3D != null;
            if (drawScale3D == null)
            {
                Interpreter addPropInterp = new Interpreter();
                addPropInterp.Pcc = exp.FileRef;
                addPropInterp.export = exp;
                addPropInterp.InitInterpreter();
                addPropInterp.AddProperty(propname); //Assuming interpreter shows current item.
                addPropInterp.Dispose();
                drawScale3D = exp.GetProperty<StructProperty>(propname);
            }
            var drawScaleX = drawScale3D.GetProp<FloatProperty>("X");
            var drawScaleY = drawScale3D.GetProp<FloatProperty>("Y");
            var drawScaleZ = drawScale3D.GetProp<FloatProperty>("Z");
            if (!hasDrawScale)
            {
                drawScaleX.Value = -1;
                drawScaleY.Value = -1;
                drawScaleZ.Value = -1;
            }
            else
            {
                drawScaleX.Value = -drawScaleX.Value;
                drawScaleY.Value = -drawScaleY.Value;
                drawScaleZ.Value = -drawScaleZ.Value;
            }
            exp.WriteProperty(drawScale3D);
        }

        private void splinesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SplineNodesActive = splinesToolStripMenuItem.Checked;
            RefreshView();
        }

        private void setGraphPositionAsSplineLocationXYToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Find node
            PathfindingNodeMaster splinePoint = CurrentlySelectedSplinePoint;
            if (splinePoint == null)
                return;
            float locX = splinePoint.GlobalBounds.X;
            float locY = splinePoint.GlobalBounds.Y;

            IExportEntry export = splinePoint.export;

            StructProperty splineInfo = export.GetProperty<StructProperty>("SplineInfo");
            if (splineInfo != null)
            {
                ArrayProperty<StructProperty> pointsProp = splineInfo.GetProp<ArrayProperty<StructProperty>>("Points");
                StructProperty point0 = pointsProp[0];
                StructProperty point1 = pointsProp[1];

                StructProperty splinePointToUpdateLoc = splinePoint is SplinePoint0Node ? point0 : point1;
                StructProperty point = splinePointToUpdateLoc.GetProp<StructProperty>("OutVal");
                point.GetProp<FloatProperty>("X").Value = locX;
                point.GetProp<FloatProperty>("Y").Value = locY;
                export.WriteProperty(splineInfo);

                //Recalculate the param table.
                Vector3 a = GetVector3(point0.GetProp<StructProperty>("OutVal"));
                Vector3 t1 = GetVector3(point0.GetProp<StructProperty>("LeaveTangent"));
                Vector3 t2 = GetVector3(point1.GetProp<StructProperty>("ArriveTangent"));
                Vector3 d = GetVector3(point1.GetProp<StructProperty>("OutVal"));
                StructProperty reparam = export.GetProperty<StructProperty>("SplineReparamTable");
                ArrayProperty<StructProperty> points = reparam.GetProp<ArrayProperty<StructProperty>>("Points");
                float[] outvals = new float[10];
                for (int i = 0; i < 10; i++)
                {
                    outvals[i] = points[i].GetProp<FloatProperty>("OutVal").Value;
                }
                float[] reparamInPoints = getReparamPoints(outvals, a, t1, t2, d);
                //todo: scale values based on original distances of reparam table.
                for (int i = 0; i < 9; i++)
                {
                    int index = i + 1; //we don't change anything on node 0.
                    points[index].GetProp<FloatProperty>("InVal").Value = reparamInPoints[i];
                }
                export.WriteProperty(reparam);
                MessageBox.Show("Location set to " + locX + "," + locY + ".\nThe reparam table has been updated for this spline.");
            }
            else
            {
                MessageBox.Show("No location property on this spline node.");
            }
            //Need to update
        }

        /// <summary>
        /// Converts struct property to SharpDX Vector 3
        /// </summary>
        /// <param name="vectorStruct">Vector Struct to convert</param>
        /// <returns></returns>
        public static Vector3 GetVector3(StructProperty vectorStruct)
        {
            Vector3 v = new Vector3();
            v.X = vectorStruct.GetProp<FloatProperty>("X");
            v.Y = vectorStruct.GetProp<FloatProperty>("Y");
            v.Z = vectorStruct.GetProp<FloatProperty>("Z");
            return v;
        }

        /// <summary>
        /// Converts struct property to SharpDX Vector 2
        /// </summary>
        /// <param name="vectorStruct">Vector Struct to convert</param>
        /// <returns></returns>
        public static Vector2 GetVector2(StructProperty vectorStruct)
        {
            Vector2 v = new Vector2();
            v.X = vectorStruct.GetProp<FloatProperty>("X");
            v.Y = vectorStruct.GetProp<FloatProperty>("Y");
            return v;
        }

        #region Benji's Magic Spline Code
        public static float evaluateBezier(float a, float b, float c, float d, float t)
        {
            return (float)(a * Math.Pow(1.0f - t, 3.0f) + 3.0f * b * Math.Pow(1.0f - t, 2.0) * t + 3.0f * c * (1.0f - t) * Math.Pow(t, 2.0f) + d * Math.Pow(t, 3.0f));
        }

        public static Vector3 evaluateBezier3D(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            return new Vector3(evaluateBezier(a.X, b.X, c.X, d.X, t), evaluateBezier(a.Y, b.Y, c.Y, d.Y, t), evaluateBezier(a.Z, b.Z, c.Z, d.Z, t));
        }

        // table is a float array of 9 elements
        //outvals, a, t1, t2, d
        private float[] getReparamPoints(float[] outvals, Vector3 startPoint, Vector3 outgoingTangent, Vector3 incomingTangent, Vector3 endPoint)
        {
            //outvals is the timing on the spline, since it is not always uniform.
            float[] table = new float[9];
            // Calculate bezier params
            Vector3 b = startPoint + outgoingTangent / 3.0f; // Operator overloading is lovely. Java can go die in a hole.
            Vector3 c = endPoint - incomingTangent / 3.0f;

            // Accumulate length and record
            float length = 0;
            for (int i = 0; i < 9; i++)
            {
                // Calculate value at this point and the next, and then compute the change
                // - USED FOR UNIFORM DISTRIBUTION
                //Vector3 startValue = evaluateBezier3D(startPoint, b, c, endPoint, i / 9.0f);
                //Vector3 endValue = evaluateBezier3D(startPoint, b, c, endPoint, (i + 1) / 9.0f);

                // Calculate value at this point and the next, and then compute the change
                // - USED FOR SAME AS SOURCE DISTRIBUTION
                Vector3 startValue = evaluateBezier3D(startPoint, b, c, endPoint, outvals[i]);
                Vector3 endValue = evaluateBezier3D(startPoint, b, c, endPoint, outvals[i + 1]);
                Vector3 dValue = endValue - startValue; // Change in value over 1/9th units, more operator overloading! Woo!

                // Calculate, accumulate, and record the distance
                float distance = dValue.Length(); // Pythagorean theorem, hopefully you use a math library with float Vector3.Length.
                length += distance;
                table[i] = length;
            }
            return table;
        }
        #endregion

        private void openInCurveEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (activeExportsListbox.SelectedIndex < 0)
            {
                return;
            }
            int l = CurrentObjects[activeExportsListbox.SelectedIndex];
            if (l == -1)
                return;
            CurveEd.CurveEditorHost c = new CurveEd.CurveEditorHost(pcc.getExport(l));
            c.Show();
        }

        private void nodesPropertiesPanelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            graphPropertiesSplitPanel.Panel2Collapsed = !graphPropertiesSplitPanel.Panel2Collapsed;
        }

        private void removeFromLevelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
            {
                return;
            }

            int n = CurrentObjects[activeExportsListbox.SelectedIndex];
            if (n == -1)
                return;

            AllowRefresh = false;
            IExportEntry nodeEntry = pcc.Exports[n];

            //This can probably be optimized if we make class that reads this and stores data for us rather than having
            //to reread everything.
            foreach (IExportEntry exp in pcc.Exports)
            {
                if (exp.ClassName == "Level" && exp.ObjectName == "PersistentLevel")
                {
                    //Read persistent level binary
                    byte[] data = exp.Data;

                    //find start of class binary (end of props)
                    int start = exp.propsEnd();

                    //Console.WriteLine("Found start of binary at " + start.ToString("X8"));

                    //uint exportid = BitConverter.ToUInt32(data, start);
                    start += 4; //skip export id
                    uint numberofitems = BitConverter.ToUInt32(data, start);
                    int countoffset = start;

                    start += 12; //skip bioworldinfo, 0;

                    int itemcount = 2; //Skip bioworldinfo and class (0) objects

                    while (itemcount < numberofitems)
                    {
                        //get header.
                        uint itemexportid = BitConverter.ToUInt32(data, start);
                        if (itemexportid == nodeEntry.UIndex)
                        {
                            SharedPathfinding.WriteMem(data, countoffset, BitConverter.GetBytes(numberofitems - 1));
                            byte[] destarray = new byte[data.Length - 4];
                            Buffer.BlockCopy(data, 0, destarray, 0, start);
                            Buffer.BlockCopy(data, start + 4, destarray, start, data.Length - (start + 4));
                            Debug.WriteLine(data.Length);
                            Debug.WriteLine("DA " + destarray.Length);
                            exp.Data = destarray;
                            AllowRefresh = true;
                            RefreshView();
                            Application.DoEvents();
                            MessageBox.Show("Removed item from level.");
                            return;
                        }
                        itemcount++;
                        start += 4;
                    }
                    MessageBox.Show("Could not find item in level.");
                }
            }
            //No level was found.
        }

        public class PathingZoomController : IDisposable
        {
            public static float MIN_SCALE = .005f;
            public static float MAX_SCALE = 15;
            PathingGraphEditor graphEditor;
            PCamera camera;

            public PathingZoomController(PathingGraphEditor graphEditor)
            {
                this.graphEditor = graphEditor;
                this.camera = graphEditor.Camera;
                camera.ViewScale = 0.5f;
                camera.MouseWheel += OnMouseWheel;
                graphEditor.KeyDown += OnKeyDown;
            }

            public void Dispose()
            {
                //Remove event handlers for memory cleanup
                camera.Canvas.ZoomEventHandler = null;
                camera.MouseWheel -= OnMouseWheel;
                graphEditor.KeyDown -= OnKeyDown;
                camera = null;
                graphEditor = null;
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

        private void bioTriggerVolumeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            graphEditor.showVolume_BioTriggerVolume = !graphEditor.showVolume_BioTriggerVolume;
            bioTriggerVolumeToolStripMenuItem.Checked = graphEditor.showVolume_BioTriggerVolume;
            RefreshView();
            graphEditor.Invalidate();
        }

        private void bioTriggerStreamToolStripMenuItem_Click(object sender, EventArgs e)
        {
            graphEditor.showVolume_BioTriggerStream = !graphEditor.showVolume_BioTriggerStream;
            bioTriggerStreamToolStripMenuItem.Checked = graphEditor.showVolume_BioTriggerStream;
            RefreshView();
            graphEditor.Invalidate();
        }

        private void blockingVolumeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            graphEditor.showVolume_BlockingVolume = !graphEditor.showVolume_BlockingVolume;
            blockingVolumeToolStripMenuItem.Checked = graphEditor.showVolume_BlockingVolume;
            RefreshView();
            graphEditor.Invalidate();
        }

        private void dynamicBlockingVolumeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            graphEditor.showVolume_DynamicBlockingVolume = !graphEditor.showVolume_DynamicBlockingVolume;
            dynamicBlockingVolumeToolStripMenuItem.Checked = graphEditor.showVolume_DynamicBlockingVolume;
            RefreshView();
            graphEditor.Invalidate();
        }

        private void wwiseAudioVolumeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            graphEditor.showVolume_WwiseAudioVolume = !graphEditor.showVolume_WwiseAudioVolume;
            wwiseAudioVolumeToolStripMenuItem.Checked = graphEditor.showVolume_WwiseAudioVolume;
            RefreshView();
        }

        private void enableDisableVolumesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            graphEditor.showVolumeBrushes = !graphEditor.showVolumeBrushes;
            enableDisableVolumesToolStripMenuItem.Checked = graphEditor.showVolumeBrushes;
            RefreshView();
            graphEditor.Invalidate();
        }

        private void everythingElseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int preCount = CurrentObjects.Count;
            EverythingElseActive = everythingElseToolStripMenuItem.Checked;
            RefreshView();
            int postCount = CurrentObjects.Count;
            if (preCount == 0 && postCount != 0)
            {
                graphPropertiesSplitPanel.Panel2Collapsed = false;
            }
        }

        private void sFXCombatZoneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            graphEditor.showVolume_SFXCombatZones = !graphEditor.showVolume_SFXCombatZones;
            sFXCombatZoneToolStripMenuItem.Checked = graphEditor.showVolume_SFXCombatZones;
            RefreshView();
        }

        private void sFXBlockingVolumeLedgeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            graphEditor.showVolume_SFXBlockingVolume_Ledge = !graphEditor.showVolume_SFXBlockingVolume_Ledge;
            sFXBlockingVolumeLedgeToolStripMenuItem.Checked = graphEditor.showVolume_SFXBlockingVolume_Ledge;
            RefreshView();
        }

        private void highlightReferencedNodeFromSequencesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            HighlightSequenceReferences = !HighlightSequenceReferences;
            highlightReferencedNodeFromSequencesToolStripMenuItem.Checked = HighlightSequenceReferences;
            RefreshView();
            graphEditor.Invalidate();
        }

        private void findNextNodeWTagToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int currentIndex = activeExportsListbox.SelectedIndex;
            if (currentIndex < 0) currentIndex = -1;
            currentIndex++;

            int nodeTagToFindIndex = allTagsCombobox.SelectedIndex;
            if (nodeTagToFindIndex == 0) return; //empty
            string nodeTagToFind = (string)allTagsCombobox.Items[nodeTagToFindIndex];

            for (int i = 0; i < Objects.Count(); i++)
            {

                PathfindingNodeMaster ci = Objects[(i + currentIndex) % Objects.Count()];
                if (ci.NodeTag == nodeTagToFind)
                {
                    int n = ci.Index;
                    activeExportsListbox.SelectedIndex = CurrentObjects.IndexOf(n);
                    break;
                }
            }
        }

        private void toSFXNavJumpDownNode_Top_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (activeExportsListbox.SelectedIndex >= 0)
            {
                int n = CurrentObjects[activeExportsListbox.SelectedIndex];
                if (n == -1)
                    return;

                if (pcc.Exports[n].ClassName != "SFXNav_JumpDownNode")
                {
                    changeNodeType(pcc.Exports[n], NODETYPE_SFXNAV_JUMPDOWNNODE_TOP);
                    RefreshView();
                }
            }
        }

        private void toSFXNavJumpDownNodeBottomToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (activeExportsListbox.SelectedIndex >= 0)
            {
                int n = CurrentObjects[activeExportsListbox.SelectedIndex];
                if (n == -1)
                    return;

                if (pcc.Exports[n].ClassName != "SFXNav_JumpDownNode")
                {
                    changeNodeType(pcc.Exports[n], NODETYPE_SFXNAV_JUMPDOWNNODE_BOTTOM);
                    RefreshView();
                }
            }
        }

        private void addExportToLevelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
            {
                return;
            }

            IExportEntry levelExport = null;
            foreach (IExportEntry exp in pcc.Exports)
            {
                if (exp.ClassName == "Level" && exp.ObjectName == "PersistentLevel")
                {
                    levelExport = exp;
                    break;
                }
            }



            if (levelExport != null)
            {
                using (ExportSelectorWinForms form = new ExportSelectorWinForms(pcc, ExportSelectorWinForms.SUPPORTS_EXPORTS_ONLY))
                {
                    DialogResult dr = form.ShowDialog(this);
                    if (dr != DialogResult.Yes)
                    {
                        return; //user cancel
                    }

                    int i = form.SelectedItemIndex;
                    IExportEntry addingExport = pcc.getExport(i);
                    if (!AllLevelObjects.Contains(addingExport))
                    {
                        byte[] leveldata = levelExport.Data;
                        int start = levelExport.propsEnd();
                        //Console.WriteLine("Found start of binary at {start.ToString("X8"));

                        uint exportid = BitConverter.ToUInt32(levelExport.Data, start);
                        start += 4;
                        uint numberofitems = BitConverter.ToUInt32(levelExport.Data, start);
                        numberofitems++;
                        SharedPathfinding.WriteMem(leveldata, start, BitConverter.GetBytes(numberofitems));

                        //Debug.WriteLine("Size before: {memory.Length);
                        //memory = RemoveIndices(memory, offset, size);
                        int offset = (int)(start + numberofitems * 4); //will be at the very end of the list as it is now +1
                        List<byte> memList = leveldata.ToList();
                        memList.InsertRange(offset, BitConverter.GetBytes(addingExport.UIndex));
                        leveldata = memList.ToArray();
                        levelExport.Data = leveldata;
                        RefreshView();
                        graphEditor.Invalidate();
                    }
                    else
                    {
                        MessageBox.Show(i + " is already in the level.");
                    }
                }
            }
            else
            {
                MessageBox.Show("PersistentLevel export not found.");
            }
        }

        private void buildLinearPathfindnigChainFromLogfileEXPERIMENTALToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
            {
                return;
            }

            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "Point Logger ASI file output (txt)|*txt";
            string pathfindingChainFile = null;
            if (d.ShowDialog() == DialogResult.OK)
            {
                pathfindingChainFile = d.FileName;


                var pointsStrs = File.ReadAllLines(pathfindingChainFile);
                var points = new List<Point3D>();
                int lineIndex = 0;
                foreach (var point in pointsStrs)
                {
                    lineIndex++;
                    if (lineIndex <= 4)
                    {
                        continue; //skip header of file
                    }
                    string[] coords = point.Split(',');
                    points.Add(new Point3D(float.Parse(coords[0]), float.Parse(coords[1]), float.Parse(coords[2])));
                }
                var basePathNode = pcc.Exports.First(x => x.ObjectName == "PathNode" && x.ClassName == "PathNode");
                IExportEntry firstNode = null;
                IExportEntry previousNode = null;


                foreach (var point in points)
                {
                    IExportEntry newNode = cloneNode(basePathNode);
                    StructProperty prop = newNode.GetProperty<StructProperty>("location");
                    if (prop != null)
                    {
                        PropertyCollection nodelocprops = (prop as StructProperty).Properties;
                        foreach (var locprop in nodelocprops)
                        {
                            switch (locprop.Name)
                            {
                                case "X":
                                    (locprop as FloatProperty).Value = (float)point.X;
                                    break;
                                case "Y":
                                    (locprop as FloatProperty).Value = (float)point.Y;
                                    break;
                                case "Z":
                                    (locprop as FloatProperty).Value = (float)point.Z;
                                    break;
                            }
                        }
                        newNode.WriteProperty(prop);

                        if (previousNode != null)
                        {
                            createReachSpec(previousNode, true, newNode.Index, "Engine.ReachSpec", 1, 0);
                        }
                        if (firstNode == null)
                        {
                            firstNode = newNode;
                        }
                        previousNode = newNode;
                    }
                }
                //createReachSpec(previousNode, true, firstNode.Index, "Engine.ReachSpec", 1, 0);

                fixStackHeaders(false);
                relinkingPathfindingChain();
                ReachSpecRecalculator rsr = new ReachSpecRecalculator(this);
                rsr.ShowDialog(this);
                Debug.WriteLine("Done");
            }
        }
    }
}