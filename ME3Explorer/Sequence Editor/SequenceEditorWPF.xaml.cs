using Gammtek.Conduit.Extensions.Collections.Generic;
using ME3Explorer.Packages;
using ME3Explorer.SequenceObjects;
using ME3Explorer.SharedUI;
using ME3Explorer.SharedUI.Interfaces;
using ME3Explorer.SharedUI.PeregrineTreeView;
using ME3Explorer.Unreal;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition.Primitives;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Gammtek.Conduit.Extensions;
using UMD.HCIL.GraphEditor;
using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Nodes;
using Color = System.Drawing.Color;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using System.Windows.Threading;
using Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap;
using GongSolutions.Wpf.DragDrop;
using MassEffect.NativesEditor.Views;
using ME3Explorer.Matinee;
using ME3Explorer.Unreal.BinaryConverters;

namespace ME3Explorer.Sequence_Editor
{
    /// <summary>
    /// Interaction logic for SequenceEditorWPF.xaml
    /// </summary>
    public partial class SequenceEditorWPF : WPFBase
    {
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

        private const float CLONED_SEQREF_MAGIC = 2.237777E-35f;

        private readonly GraphEditor graphEditor;
        public ObservableCollectionExtended<SObj> CurrentObjects { get; } = new ObservableCollectionExtended<SObj>();
        public ObservableCollectionExtended<SObj> SelectedObjects { get; } = new ObservableCollectionExtended<SObj>();
        public ObservableCollectionExtended<ExportEntry> SequenceExports { get; } = new ObservableCollectionExtended<ExportEntry>();
        public ObservableCollectionExtended<TreeViewEntry> TreeViewRootNodes { get; set; } = new ObservableCollectionExtended<TreeViewEntry>();
        public string CurrentFile;
        public string JSONpath;

        private ExportEntry _selectedSequence;
        public ExportEntry SelectedSequence
        {
            get => _selectedSequence;
            set => SetProperty(ref _selectedSequence, value);
        }

        private List<SaveData> SavedPositions;
        public bool RefOrRefChild;

        public static readonly string SequenceEditorDataFolder = Path.Combine(App.AppDataFolder, @"SequenceEditor\");
        public static readonly string OptionsPath = Path.Combine(SequenceEditorDataFolder, "SequenceEditorOptions.JSON");
        public static readonly string ME3ViewsPath = Path.Combine(SequenceEditorDataFolder, @"ME3SequenceViews\");
        public static readonly string ME2ViewsPath = Path.Combine(SequenceEditorDataFolder, @"ME2SequenceViews\");
        public static readonly string ME1ViewsPath = Path.Combine(SequenceEditorDataFolder, @"ME1SequenceViews\");

        public SequenceEditorWPF()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Sequence Editor WPF", new WeakReference(this));
            LoadCommands();
            DataContext = this;
            StatusText = "Select package file to load";
            InitializeComponent();

            LoadRecentList();

            graphEditor = (GraphEditor)GraphHost.Child;
            graphEditor.BackColor = Color.FromArgb(167, 167, 167);
            graphEditor.Camera.MouseDown += backMouseDown_Handler;
            graphEditor.Camera.MouseUp += back_MouseUp;

            this.graphEditor.Click += graphEditor_Click;
            this.graphEditor.DragDrop += SequenceEditor_DragDrop;
            this.graphEditor.DragEnter += SequenceEditor_DragEnter;

            eventsToolBox.DoubleClickCallback = CreateNewObject;
            actionsToolBox.DoubleClickCallback = CreateNewObject;
            conditionsToolBox.DoubleClickCallback = CreateNewObject;
            variablesToolBox.DoubleClickCallback = CreateNewObject;

            if (File.Exists(OptionsPath))
            {
                var options = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(OptionsPath));
                if (options.ContainsKey("AutoSave"))
                    AutoSaveView_MenuItem.IsChecked = (bool)options["AutoSave"];
                if (options.ContainsKey("OutputNumbers"))
                    ShowOutputNumbers_MenuItem.IsChecked = (bool)options["OutputNumbers"];
                if (options.ContainsKey("GlobalSeqRefView"))
                    GlobalSeqRefViewSavesMenuItem.IsChecked = (bool)options["GlobalSeqRefView"];
                SObj.OutputNumbers = ShowOutputNumbers_MenuItem.IsChecked;
            }
        }

        public SequenceEditorWPF(ExportEntry export) : this()
        {
            FileQueuedForLoad = export.FileRef.FilePath;
            ExportQueuedForFocusing = export;
        }

        public ICommand OpenCommand { get; set; }
        public ICommand SaveCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }
        public ICommand SaveImageCommand { get; set; }
        public ICommand SaveViewCommand { get; set; }
        public ICommand AutoLayoutCommand { get; set; }
        public ICommand GotoCommand { get; set; }
        public ICommand KismetLogCommand { get; set; }

        private void LoadCommands()
        {
            OpenCommand = new GenericCommand(OpenPackage);
            SaveCommand = new GenericCommand(SavePackage, PackageIsLoaded);
            SaveAsCommand = new GenericCommand(SavePackageAs, PackageIsLoaded);
            SaveImageCommand = new GenericCommand(SaveImage, CurrentObjects.Any);
            SaveViewCommand = new GenericCommand(() => saveView(), CurrentObjects.Any);
            AutoLayoutCommand = new GenericCommand(AutoLayout, CurrentObjects.Any);
            GotoCommand = new GenericCommand(GoTo, PackageIsLoaded);
            KismetLogCommand = new RelayCommand(OpenKismetLogParser, CanOpenKismetLog);
        }

        private void CreateNewObject(ClassInfo info)
        {
            if (SelectedSequence == null)
            {
                return;
            }
            IEntry classEntry = EntryImporter.EnsureClassIsInFile(Pcc, info.ClassName);
            if (classEntry is null)
            {
                MessageBox.Show(this, $"Could not import {info.ClassName}'s class definition! It may defined in a DLC you don't have.");
                return;
            }

            ExportEntry newSeqObj = new ExportEntry(Pcc, properties: SequenceObjectCreator.GetSequenceObjectDefaults(Pcc, info))
            {
                Class = classEntry,
                ObjectName = info.ClassName
            };
            Pcc.AddExport(newSeqObj);
            addObject(newSeqObj);
        }

        private bool CanOpenKismetLog(object o)
        {
            switch (o)
            {
                case true:
                    return Pcc != null && File.Exists(KismetLogParser.KismetLogPath(Pcc.Game));
                case MEGame game:
                    return File.Exists(KismetLogParser.KismetLogPath(game));
                default:
                    return false;
            }
        }

        private void OpenKismetLogParser(object obj)
        {
            if (CanOpenKismetLog(obj))
            {
                switch (obj)
                {
                    case true:
                        kismetLogParser.LoadLog(Pcc.Game, Pcc);
                        break;
                    case MEGame game:
                        kismetLogParser.LoadLog(game);
                        break;
                    default:
                        return;
                }
                kismetLogParser.Visibility = Visibility.Visible;
                kismetLogParserRow.Height = new GridLength(150);
                kismetLogParser.ExportFound = (filePath, uIndex) =>
                {
                    if (Pcc == null || Pcc.FilePath != filePath) LoadFile(filePath);
                    GoToExport(Pcc.GetUExport(uIndex), false);
                };
            }
            else
            {
                MessageBox.Show(this, "No Kismet Log!");
            }
        }

        private void GoTo()
        {
            if (EntrySelector.GetEntry<ExportEntry>(this, Pcc) is ExportEntry export)
            {
                GoToExport(export);
            }
        }

        private string _statusText;

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, $"{CurrentFile} {value}");
        }

        private TreeViewEntry _selectedItem;

        public TreeViewEntry SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (AutoSaveView_MenuItem.IsChecked)
                {
                    saveView();
                }

                if (SetProperty(ref _selectedItem, value) && value != null)
                {
                    value.IsSelected = true;
                    LoadSequence((ExportEntry)value.Entry);
                }
            }
        }

        private void SavePackageAs()
        {
            string extension = Path.GetExtension(Pcc.FilePath);
            SaveFileDialog d = new SaveFileDialog { Filter = $"*{extension}|*{extension}" };
            if (d.ShowDialog() == true)
            {
                Pcc.Save(d.FileName);
                MessageBox.Show(this, "Done.");
            }
        }

        private void SavePackage()
        {
            Pcc.Save();
        }

        private void OpenPackage()
        {
            OpenFileDialog d = new OpenFileDialog { Filter = App.FileFilter };
            if (d.ShowDialog() == true)
            {
                try
                {
                    LoadFile(d.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Unable to open file:\n" + ex.Message);
                }
            }
        }

        private bool PackageIsLoaded()
        {
            System.Diagnostics.Debug.WriteLine("Package Is Loaded.");
            return Pcc != null;
        }

        public void LoadFile(string fileName)
        {
            try
            {
                SelectedSequence = null;
                CurrentObjects.ClearEx();
                SequenceExports.ClearEx();
                SelectedObjects.ClearEx();
                LoadMEPackage(fileName);
                CurrentFile = System.IO.Path.GetFileName(fileName);
                LoadSequences();
                if (TreeViewRootNodes.IsEmpty())
                {
                    UnLoadMEPackage();
                    MessageBox.Show(this, "This file does not contain any Sequences!");
                    StatusText = "Select a package file to load";
                    return;
                }

                graphEditor.nodeLayer.RemoveAllChildren();
                graphEditor.edgeLayer.RemoveAllChildren();

                AddRecent(fileName, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);

                Title = $"Sequence Editor - {fileName}";
                StatusText = null; //no status

                eventsToolBox.Classes = SequenceObjectCreator.GetSequenceEvents(Pcc.Game);
                actionsToolBox.Classes = SequenceObjectCreator.GetSequenceActions(Pcc.Game);
                conditionsToolBox.Classes = SequenceObjectCreator.GetSequenceConditions(Pcc.Game);
                variablesToolBox.Classes = SequenceObjectCreator.GetSequenceVariables(Pcc.Game);

            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error:\n" + ex.Message);
                Title = "Sequence Editor";
                CurrentFile = null;
                UnLoadMEPackage();
            }
        }

        private void LoadSequences()
        {
            TreeViewRootNodes.ClearEx();
            var prefabs = new Dictionary<string, TreeViewEntry>();
            foreach (var export in Pcc.Exports)
            {
                switch (export.ClassName)
                {
                    case "Sequence" when !(export.HasParent && export.Parent.IsSequence()):
                        TreeViewRootNodes.Add(FindSequences(export, export.ObjectName != "Main_Sequence"));
                        SequenceExports.Add(export);
                        break;
                    case "Prefab":
                        try
                        {
                            prefabs.Add(export.ObjectName.Name, new TreeViewEntry(export, export.InstancedFullPath));
                        }
                        catch
                        {
                            // ignored
                        }

                        break;
                }
            }

            if (prefabs.Count > 0)
            {
                foreach (var export in Pcc.Exports)
                {
                    if (export.ClassName == "PrefabSequence" && export.Parent?.ClassName == "Prefab")
                    {
                        string parentName = Pcc.getObjectName(export.idxLink);
                        if (prefabs.ContainsKey(parentName))
                        {
                            prefabs[parentName].Sublinks.Add(FindSequences(export));
                        }
                    }
                }

                foreach (var item in prefabs.Values)
                {
                    if (item.Sublinks.Any())
                    {
                        TreeViewRootNodes.Add(item);
                    }
                }
            }
        }

        private TreeViewEntry FindSequences(ExportEntry rootSeq, bool wantFullName = false)
        {
            string seqName = wantFullName ? rootSeq.InstancedFullPath : "";
            if (rootSeq.GetProperty<StrProperty>("ObjName") is StrProperty objName)
            {
                seqName += objName;
            }
            else
            {
                seqName += rootSeq.ObjectName.Instanced;
            }
            var root = new TreeViewEntry(rootSeq, $"#{rootSeq.UIndex}: {seqName}")
            {
                IsExpanded = true
            };
            var pcc = rootSeq.FileRef;
            var seqObjs = rootSeq.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
            if (seqObjs != null)
            {
                foreach (ObjectProperty seqObj in seqObjs)
                {
                    if (!pcc.IsUExport(seqObj.Value)) continue;
                    ExportEntry exportEntry = pcc.GetUExport(seqObj.Value);
                    if (exportEntry.ClassName == "Sequence" || exportEntry.ClassName.StartsWith("PrefabSequence"))
                    {
                        TreeViewEntry t = FindSequences(exportEntry);
                        SequenceExports.Add(exportEntry);
                        root.Sublinks.Add(t);
                    }
                    else if (exportEntry.ClassName == "SequenceReference")
                    {
                        var propSequenceReference = exportEntry.GetProperty<ObjectProperty>("oSequenceReference");
                        if (propSequenceReference != null)
                        {
                            TreeViewEntry t = FindSequences(pcc.GetUExport(propSequenceReference.Value));
                            SequenceExports.Add(exportEntry);
                            root.Sublinks.Add(t);
                        }
                    }
                }
            }

            return root;
        }

        private void LoadSequence(ExportEntry seqExport, bool fromFile = true)
        {
            if (seqExport == null)
            {
                return;
            }

            graphEditor.Enabled = false;
            graphEditor.UseWaitCursor = true;
            SelectedSequence = seqExport;
            SetupJSON(SelectedSequence);
            var selectedExports = SelectedObjects.Select(o => o.Export).ToList();
            Properties_InterpreterWPF.LoadExport(seqExport);
            if (fromFile)
            {
                if (File.Exists(JSONpath))
                {
                    SavedPositions = JsonConvert.DeserializeObject<List<SaveData>>(File.ReadAllText(JSONpath));
                }
                else
                {
                    SavedPositions = new List<SaveData>();
                }

                customSaveData.Clear();
                selectedExports.Clear();
            }
            try
            {
                GenerateGraph();
                if (selectedExports.Count == 1 && CurrentObjects.FirstOrDefault(obj => obj.Export == selectedExports[0]) is SObj selectedObj)
                {
                    panToSelection = false;
                    CurrentObjects_ListBox.SelectedItem = selectedObj;
                }
            }
            catch (Exception e) when (!App.IsDebug)
            {
                MessageBox.Show(this, $"Error loading sequences from file:\n{e.Message}");
            }
            graphEditor.Enabled = true;
            graphEditor.UseWaitCursor = false;
        }

        private void SetupJSON(ExportEntry export)
        {
            string objectName = System.Text.RegularExpressions.Regex.Replace(export.ObjectName.Name, @"[<>:""/\\|?*]", "");
            bool isClonedSeqRef = false;
            var defaultViewZoomProp = export.GetProperty<FloatProperty>("DefaultViewZoom");
            if (defaultViewZoomProp != null && Math.Abs(defaultViewZoomProp.Value - CLONED_SEQREF_MAGIC) < 1.0E-30f)
            {
                isClonedSeqRef = true;
            }

            string parentFullPath = export.ParentFullPath;
            if (GlobalSeqRefViewSavesMenuItem.IsChecked && parentFullPath.Contains("SequenceReference") && !isClonedSeqRef)
            {
                string packageName = parentFullPath.Substring(parentFullPath.LastIndexOf("SequenceReference"));
                if (Pcc.Game == MEGame.ME3)
                {
                    JSONpath = $"{ME3ViewsPath}{packageName}.{objectName}.JSON";
                }
                else
                {
                    packageName = packageName.Replace("SequenceReference", "");
                    int idx = export.UIndex;
                    string ObjName = "";
                    while (idx > 0)
                    {
                        if (Pcc.GetUExport(Pcc.GetUExport(idx).idxLink).ClassName == "SequenceReference")
                        {
                            var objNameProp = Pcc.GetUExport(idx).GetProperty<StrProperty>("ObjName");
                            if (objNameProp != null)
                            {
                                ObjName = objNameProp.Value;
                                break;
                            }
                        }

                        idx = Pcc.GetUExport(idx).idxLink;
                    }

                    if (objectName == "Sequence")
                    {
                        objectName = ObjName;
                        packageName = "." + packageName;
                    }
                    else
                        packageName = packageName.Replace("Sequence", ObjName) + ".";

                    if (Pcc.Game == MEGame.ME2)
                    {
                        JSONpath = $"{ME2ViewsPath}SequenceReference{packageName}{objectName}.JSON";
                    }
                    else
                    {
                        JSONpath = $"{ME1ViewsPath}SequenceReference{packageName}{objectName}.JSON";
                    }
                }

                RefOrRefChild = true;
            }
            else
            {
                string viewsPath = ME3ViewsPath;
                switch (Pcc.Game)
                {
                    case MEGame.ME2:
                        viewsPath = ME2ViewsPath;
                        break;
                    case MEGame.ME1:
                        viewsPath = ME1ViewsPath;
                        break;
                }

                JSONpath = $"{viewsPath}{CurrentFile}.#{export.Index}{objectName}.JSON";
                RefOrRefChild = false;
            }
        }

        public void GetObjects(ExportEntry export)
        {
            CurrentObjects.ClearEx();
            var seqObjs = export.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
            if (seqObjs != null)
            {
                CurrentObjects.AddRange(seqObjs.OrderBy(prop => prop.Value)
                                               .Where(prop => Pcc.IsUExport(prop.Value))
                                               .Select(prop => LoadObject(Pcc.GetUExport(prop.Value))));
                if (CurrentObjects.Count != seqObjs.Count)
                {
                    MessageBox.Show(this, "Sequence contains invalid exports! Correct this by editing the SequenceObject array in the Interpreter");
                }
            }
        }

        public void GenerateGraph()
        {
            graphEditor.nodeLayer.RemoveAllChildren();
            graphEditor.edgeLayer.RemoveAllChildren();
            StartPosEvents = 0;
            StartPosActions = 0;
            StartPosVars = 0;
            GetObjects(SelectedSequence);
            Layout();
            foreach (SObj o in CurrentObjects)
            {
                o.MouseDown += node_MouseDown;
                o.Click += node_Click;
            }

            if (SavedPositions.IsEmpty() && (Pcc.Game != MEGame.ME1 && Pcc.Game != MEGame.UDK))
            {
                AutoLayout();
            }
        }

        public float StartPosEvents;
        public float StartPosActions;
        public float StartPosVars;

        public SObj LoadObject(ExportEntry export)
        {
            int x = 0, y = 0;
            foreach (var prop in export.GetProperties())
            {
                switch (prop)
                {
                    case IntProperty intProp when intProp.Name == "ObjPosX":
                        x = intProp.Value;
                        break;
                    case IntProperty intProp when intProp.Name == "ObjPosY":
                        y = intProp.Value;
                        break;
                }
            }

            if (export.IsOrInheritsFrom("SequenceEvent"))
            {
                return new SEvent(export, x, y, graphEditor);
            }
            else if (export.IsOrInheritsFrom("SequenceVariable"))
            {
                return new SVar(export, x, y, graphEditor);
            }
            else if (export.ClassName == "SequenceFrame" && (Pcc.Game == MEGame.ME1 || Pcc.Game == MEGame.UDK))
            {
                return new SFrame(export, x, y, graphEditor);
            }
            else //if (s.StartsWith("BioSeqAct_") || s.StartsWith("SeqAct_") || s.StartsWith("SFXSeqAct_") || s.StartsWith("SeqCond_") || pcc.getExport(index).ClassName == "Sequence" || pcc.getExport(index).ClassName == "SequenceReference")
            {
                return new SAction(export, x, y, graphEditor);
            }
        }

        public void Layout()
        {
            if (CurrentObjects != null && CurrentObjects.Any())
            {
                foreach (SObj obj in CurrentObjects)
                {
                    graphEditor.addNode(obj);
                }

                foreach (SObj obj in CurrentObjects)
                {
                    obj.CreateConnections(CurrentObjects);
                }

                for (int i = 0; i < CurrentObjects.Count; i++)
                {
                    SObj obj = CurrentObjects[i];
                    SaveData savedInfo = new SaveData(-1);
                    if (SavedPositions.Any())
                    {
                        if (RefOrRefChild)
                            savedInfo = SavedPositions.FirstOrDefault(p => i == p.index);
                        else
                            savedInfo = SavedPositions.FirstOrDefault(p => obj.Index == p.index);
                    }

                    bool hasSavedPosition =
                        savedInfo.index == (RefOrRefChild ? i : obj.Index);
                    if (hasSavedPosition)
                    {
                        obj.Layout(savedInfo.X, savedInfo.Y);
                    }
                    else if (Pcc.Game == MEGame.ME1 || Pcc.Game == MEGame.UDK)
                    {
                        obj.Layout();
                    }
                    else
                    {
                        switch (obj)
                        {
                            case SEvent _:
                                obj.Layout(StartPosEvents, 0);
                                StartPosEvents += obj.Width + 20;
                                break;
                            case SAction _:
                                obj.Layout(StartPosActions, 250);
                                StartPosActions += obj.Width + 20;
                                break;
                            case SVar _:
                                obj.Layout(StartPosVars, 500);
                                StartPosVars += obj.Width + 20;
                                break;
                        }
                    }
                }

                foreach (SeqEdEdge edge in graphEditor.edgeLayer)
                {
                    GraphEditor.UpdateEdge(edge);
                }
            }
        }

        private void AutoLayout()
        {
            foreach (SObj obj in CurrentObjects)
            {
                obj.SetOffset(0, 0); //remove existing positioning
            }

            const float HORIZONTAL_SPACING = 40;
            const float VERTICAL_SPACING = 20;
            const float VAR_SPACING = 10;
            var visitedNodes = new HashSet<int>();
            var eventNodes = CurrentObjects.OfType<SEvent>().ToList();
            SObj firstNode = eventNodes.FirstOrDefault();
            var varNodeLookup = CurrentObjects.OfType<SVar>().ToDictionary(obj => obj.UIndex);
            var opNodeLookup = CurrentObjects.OfType<SBox>().ToDictionary(obj => obj.UIndex);
            var rootTree = new List<SObj>();
            //SEvents are natural root nodes. ALmost everything will proceed from one of these
            foreach (SEvent eventNode in eventNodes)
            {
                LayoutTree(eventNode, 5 * VERTICAL_SPACING);
            }

            //Find SActions with no inputs. These will not have been reached from an SEvent
            var orphanRoots = CurrentObjects.OfType<SAction>().Where(node => node.InputEdges.IsEmpty());
            foreach (SAction orphan in orphanRoots)
            {
                LayoutTree(orphan, VERTICAL_SPACING);
            }

            //It's possible that there are groups of otherwise unconnected SActions that form cycles.
            //Might be possible to make a better heuristic for choosing a root than sequence order, but this situation is so rare it's not worth the effort
            var cycleNodes = CurrentObjects.OfType<SAction>().Where(node => !visitedNodes.Contains(node.UIndex));
            foreach (SAction cycleNode in cycleNodes)
            {
                LayoutTree(cycleNode, VERTICAL_SPACING);
            }

            //Lonely unconnected variables. Put them in a row below everything else
            var unusedVars = CurrentObjects.OfType<SVar>().Where(obj => !visitedNodes.Contains(obj.UIndex));
            float varOffset = 0;
            float vertOffset = rootTree.BoundingRect().Bottom + VERTICAL_SPACING;
            foreach (SVar unusedVar in unusedVars)
            {
                unusedVar.OffsetBy(varOffset, vertOffset);
                varOffset += unusedVar.GlobalFullWidth + HORIZONTAL_SPACING;
            }

            if (firstNode != null) CurrentObjects.OffsetBy(0, -firstNode.OffsetY);

            foreach (SeqEdEdge edge in graphEditor.edgeLayer)
                GraphEditor.UpdateEdge(edge);


            void LayoutTree(SBox sAction, float verticalSpacing)
            {
                if (firstNode == null) firstNode = sAction;
                visitedNodes.Add(sAction.UIndex);
                var subTree = LayoutSubTree(sAction);
                float width = subTree.BoundingRect().Width + HORIZONTAL_SPACING;
                //ignore nodes that are further to the right than this subtree is wide. This allows tighter spacing
                float dy = rootTree.Where(node => node.GlobalFullBounds.Left < width).BoundingRect().Bottom;
                if (dy > 0) dy += verticalSpacing;
                subTree.OffsetBy(0, dy);
                rootTree.AddRange(subTree);
            }

            List<SObj> LayoutSubTree(SBox root)
            {
                //Task.WaitAll(Task.Delay(1500));
                var tree = new List<SObj>();
                var vars = new List<SVar>();
                foreach (var varLink in root.Varlinks)
                {
                    float dx = varLink.node.GlobalFullBounds.X - SVar.RADIUS;
                    float dy = root.GlobalFullHeight + VAR_SPACING;
                    foreach (int uIndex in varLink.Links.Where(uIndex => !visitedNodes.Contains(uIndex)))
                    {
                        visitedNodes.Add(uIndex);
                        if (varNodeLookup.TryGetValue(uIndex, out SVar sVar))
                        {
                            sVar.OffsetBy(dx, dy);
                            dy += sVar.GlobalFullHeight + VAR_SPACING;
                            vars.Add(sVar);
                        }
                    }
                }

                var childTrees = new List<List<SObj>>();
                var children = root.Outlinks.SelectMany(link => link.Links).Where(uIndex => !visitedNodes.Contains(uIndex));
                foreach (int uIndex in children)
                {
                    visitedNodes.Add(uIndex);
                    if (opNodeLookup.TryGetValue(uIndex, out SBox node))
                    {
                        List<SObj> subTree = LayoutSubTree(node);
                        childTrees.Add(subTree);
                    }
                }

                if (childTrees.Any())
                {
                    float dx = root.GlobalFullWidth + (HORIZONTAL_SPACING * (1 + childTrees.Count * 0.4f));
                    foreach (List<SObj> subTree in childTrees)
                    {
                        float subTreeWidth = subTree.BoundingRect().Width + HORIZONTAL_SPACING + dx;
                        //ignore nodes that are further to the right than this subtree is wide. This allows tighter spacing
                        float dy = tree.Where(node => node.GlobalFullBounds.Left < subTreeWidth).BoundingRect().Bottom;
                        if (dy > 0) dy += VERTICAL_SPACING;
                        subTree.OffsetBy(dx, dy);
                        //TODO: fix this so it doesn't screw up some sequences. eg: BioD_ProEar_310BigFall.pcc
                        /*float treeWidth = tree.BoundingRect().Width + HORIZONTAL_SPACING;
                        //tighten spacing when this subtree is wider than existing tree. 
                        dy -= subTree.Where(node => node.GlobalFullBounds.Left < treeWidth).BoundingRect().Top;
                        if (dy < 0) dy += VERTICAL_SPACING;
                        subTree.OffsetBy(0, dy);*/

                        tree.AddRange(subTree);
                    }

                    //center the root on its children
                    float centerOffset = tree.OfType<SBox>().BoundingRect().Height / 2 - root.GlobalFullHeight / 2;
                    root.OffsetBy(0, centerOffset);
                    vars.OffsetBy(0, centerOffset);
                }

                tree.AddRange(vars);
                tree.Add(root);
                return tree;
            }
        }

        public void RefreshView()
        {
            saveView(false);
            LoadSequence(SelectedSequence, false);
        }

        #region Recents

        private readonly List<Button> RecentButtons = new List<Button>();
        public List<string> RFiles;
        private readonly string RECENTFILES_FILE = "RECENTFILES";

        private void LoadRecentList()
        {
            RecentButtons.AddRange(new[] { RecentButton1, RecentButton2, RecentButton3, RecentButton4, RecentButton5, RecentButton6, RecentButton7, RecentButton8, RecentButton9, RecentButton10 });
            Recents_MenuItem.IsEnabled = false;
            RFiles = new List<string>();
            RFiles.Clear();
            string path = SequenceEditorDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
            {
                string[] recents = File.ReadAllLines(path);
                foreach (string recent in recents)
                {
                    if (File.Exists(recent))
                    {
                        AddRecent(recent, true);
                    }
                }
            }

            RefreshRecent(false);
        }

        private void SaveRecentList()
        {
            if (!Directory.Exists(SequenceEditorDataFolder))
            {
                Directory.CreateDirectory(SequenceEditorDataFolder);
            }

            string path = SequenceEditorDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
                File.Delete(path);
            File.WriteAllLines(path, RFiles);
        }

        public void RefreshRecent(bool propogate, List<string> recents = null)
        {
            if (propogate && recents != null)
            {
                //we are posting an update to other instances of SeqEd
                foreach (var window in Application.Current.Windows)
                {
                    if (window is SequenceEditorWPF wpf && this != wpf)
                    {
                        wpf.RefreshRecent(false, RFiles);
                    }
                }
            }
            else if (recents != null)
            {
                //we are receiving an update
                RFiles = new List<string>(recents);
            }

            Recents_MenuItem.Items.Clear();
            if (RFiles.Count <= 0)
            {
                Recents_MenuItem.IsEnabled = false;
                return;
            }

            Recents_MenuItem.IsEnabled = true;

            int i = 0;
            foreach (string filepath in RFiles)
            {
                MenuItem fr = new MenuItem()
                {
                    Header = filepath.Replace("_", "__"),
                    Tag = filepath
                };
                RecentButtons[i].Visibility = Visibility.Visible;
                RecentButtons[i].Content = Path.GetFileName(filepath.Replace("_", "__"));
                RecentButtons[i].Click -= RecentFile_click;
                RecentButtons[i].Click += RecentFile_click;
                RecentButtons[i].Tag = filepath;
                RecentButtons[i].ToolTip = filepath;
                fr.Click += RecentFile_click;
                Recents_MenuItem.Items.Add(fr);
                i++;
            }

            while (i < 10)
            {
                RecentButtons[i].Visibility = Visibility.Collapsed;
                i++;
            }
        }

        private void RecentFile_click(object sender, EventArgs e)
        {
            string s = ((FrameworkElement)sender).Tag.ToString();
            if (File.Exists(s))
            {
                LoadFile(s);
            }
            else
            {
                MessageBox.Show(this, "File does not exist: " + s);
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

            Recents_MenuItem.IsEnabled = true;
        }

        #endregion Recents

        private void backMouseDown_Handler(object sender, PInputEventArgs e)
        {
            if (!(e.PickedNode is PCamera) || SelectedSequence == null) return;

            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                if (FindResource("backContextMenu") is ContextMenu contextMenu)
                {
                    contextMenu.IsOpen = true;
                }
            }
            else if (e.Shift)
            {
                //graphEditor.StartBoxSelection(e);
                //e.Handled = true;
            }
            else
            {
                CurrentObjects_ListBox.SelectedItems.Clear();
            }
        }

        private void back_MouseUp(object sender, PInputEventArgs e)
        {
            //var nodesToSelect = graphEditor.EndBoxSelection().OfType<SObj>();
            //foreach (SObj sObj in nodesToSelect)
            //{
            //    panToSelection = false;
            //    CurrentObjects_ListBox.SelectedItems.Add(sObj);
            //}
        }

        private void graphEditor_Click(object sender, EventArgs e)
        {
            graphEditor.Focus();
        }

        private void SequenceEditor_DragEnter(object sender, System.Windows.Forms.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.Forms.DataFormats.FileDrop))
                e.Effect = System.Windows.Forms.DragDropEffects.All;
            else
                e.Effect = System.Windows.Forms.DragDropEffects.None;
        }

        private void SequenceEditor_DragDrop(object sender, System.Windows.Forms.DragEventArgs e)
        {
            if (e.Data.GetData(System.Windows.Forms.DataFormats.FileDrop) is string[] DroppedFiles)
            {
                if (DroppedFiles.Any())
                {
                    LoadFile(DroppedFiles[0]);
                }
            }
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            if (Pcc == null)
            {
                return; //nothing is loaded
            }

            IEnumerable<PackageUpdate> relevantUpdates = updates.Where(x => x.change != PackageChange.Import &&
                                                                            x.change != PackageChange.ImportAdd &&
                                                                            x.change != PackageChange.Names);
            List<int> updatedExports = relevantUpdates.Select(x => x.index).ToList();
            if (SelectedSequence != null && updatedExports.Contains(SelectedSequence.Index))
            {
                //loaded sequence is no longer a sequence
                if (!SelectedSequence.IsSequence())
                {
                    SelectedSequence = null;
                    graphEditor.nodeLayer.RemoveAllChildren();
                    graphEditor.edgeLayer.RemoveAllChildren();
                    CurrentObjects.ClearEx();
                    SequenceExports.ClearEx();
                    SelectedObjects.ClearEx();
                    Properties_InterpreterWPF.UnloadExport();
                }

                RefreshView();
                LoadSequences();
                return;
            }

            if (updatedExports.Intersect(CurrentObjects.Select(obj => obj.Index)).Any())
            {
                RefreshView();
            }

            foreach (var i in updatedExports)
            {
                if (Pcc.IsUExport(i + 1) && Pcc.getExport(i).IsSequence())
                {
                    LoadSequences();
                    break;
                }
            }
        }

        private readonly List<SaveData> customSaveData = new List<SaveData>();
        private bool panToSelection = true;
        private string FileQueuedForLoad;
        private ExportEntry ExportQueuedForFocusing;
        private bool AllowWindowRefocus = true;

        private void saveView(bool toFile = true)
        {
            if (CurrentObjects.Count == 0)
                return;
            SavedPositions = new List<SaveData>();
            for (int i = 0; i < CurrentObjects.Count; i++)
            {
                SObj obj = CurrentObjects[i];
                if (obj.Pickable)
                {
                    SavedPositions.Add(new SaveData
                    {
                        absoluteIndex = RefOrRefChild,
                        index = RefOrRefChild ? i : obj.Index,
                        X = obj.X + obj.Offset.X,
                        Y = obj.Y + obj.Offset.Y
                    });
                }
            }
            SavedPositions.AddRange(customSaveData);
            customSaveData.Clear();

            if (toFile)
            {
                string outputFile = JsonConvert.SerializeObject(SavedPositions);
                if (!Directory.Exists(Path.GetDirectoryName(JSONpath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(JSONpath));
                File.WriteAllText(JSONpath, outputFile);
                SavedPositions.Clear();
            }
        }

        public void OpenNodeContextMenu(SObj obj)
        {
            if (FindResource("nodeContextMenu") is ContextMenu contextMenu)
            {
                if (obj is SBox sBox && (sBox.Varlinks.Any() || sBox.Outlinks.Any() || sBox.EventLinks.Any())
                 && contextMenu.GetChild("breakLinksMenuItem") is MenuItem breakLinksMenuItem)
                {
                    bool hasLinks = false;
                    if (breakLinksMenuItem.GetChild("outputLinksMenuItem") is MenuItem outputLinksMenuItem)
                    {
                        outputLinksMenuItem.Visibility = Visibility.Collapsed;
                        outputLinksMenuItem.Items.Clear();
                        for (int i = 0; i < sBox.Outlinks.Count; i++)
                        {
                            for (int j = 0; j < sBox.Outlinks[i].Links.Count; j++)
                            {
                                outputLinksMenuItem.Visibility = Visibility.Visible;
                                hasLinks = true;
                                var temp = new MenuItem
                                {
                                    Header = $"Break link from {sBox.Outlinks[i].Desc} to {sBox.Outlinks[i].Links[j]}"
                                };
                                int linkConnection = i;
                                int linkIndex = j;
                                temp.Click += (o, args) => { sBox.RemoveOutlink(linkConnection, linkIndex); };
                                outputLinksMenuItem.Items.Add(temp);
                            }
                        }

                        if (outputLinksMenuItem.Items.Count > 0)
                        {
                            var temp = new MenuItem { Header = "Break All", Tag = obj.Export };
                            temp.Click += removeAllOutputLinks;
                            outputLinksMenuItem.Items.Add(temp);
                        }
                    }

                    if (breakLinksMenuItem.GetChild("varLinksMenuItem") is MenuItem varLinksMenuItem)
                    {
                        varLinksMenuItem.Visibility = Visibility.Collapsed;
                        varLinksMenuItem.Items.Clear();
                        for (int i = 0; i < sBox.Varlinks.Count; i++)
                        {
                            for (int j = 0; j < sBox.Varlinks[i].Links.Count; j++)
                            {
                                varLinksMenuItem.Visibility = Visibility.Visible;
                                hasLinks = true;
                                var temp = new MenuItem
                                {
                                    Header = $"Break link from {sBox.Varlinks[i].Desc} to {sBox.Varlinks[i].Links[j]}"
                                };
                                int linkConnection = i;
                                int linkIndex = j;
                                temp.Click += (o, args) => { sBox.RemoveVarlink(linkConnection, linkIndex); };
                                varLinksMenuItem.Items.Add(temp);
                            }
                        }

                        if (varLinksMenuItem.Items.Count > 0)
                        {
                            var temp = new MenuItem { Header = "Break All", Tag = obj.Export };
                            temp.Click += removeAllVarLinks;
                            varLinksMenuItem.Items.Add(temp);
                        }
                    }
                    if (breakLinksMenuItem.GetChild("eventLinksMenuItem") is MenuItem eventLinksMenuItem)
                    {
                        eventLinksMenuItem.Visibility = Visibility.Collapsed;
                        eventLinksMenuItem.Items.Clear();
                        for (int i = 0; i < sBox.EventLinks.Count; i++)
                        {
                            for (int j = 0; j < sBox.EventLinks[i].Links.Count; j++)
                            {
                                eventLinksMenuItem.Visibility = Visibility.Visible;
                                hasLinks = true;
                                var temp = new MenuItem
                                {
                                    Header = $"Break link from {sBox.EventLinks[i].Desc} to {sBox.EventLinks[i].Links[j]}"
                                };
                                int linkConnection = i;
                                int linkIndex = j;
                                temp.Click += (o, args) =>
                                {
                                    sBox.RemoveEventlink(linkConnection, linkIndex);
                                };
                                eventLinksMenuItem.Items.Add(temp);
                            }
                        }

                        if (eventLinksMenuItem.Items.Count > 0)
                        {
                            var temp = new MenuItem { Header = "Break All", Tag = obj.Export };
                            temp.Click += removeAllEventLinks;
                            eventLinksMenuItem.Items.Add(temp);
                        }
                    }
                    if (breakLinksMenuItem.GetChild("breakAllLinksMenuItem") is MenuItem breakAllLinksMenuItem)
                    {
                        if (hasLinks)
                        {
                            breakLinksMenuItem.Visibility = Visibility.Visible;
                            breakAllLinksMenuItem.Visibility = Visibility.Visible;
                            breakAllLinksMenuItem.Tag = obj.Export;
                        }
                        else
                        {
                            breakLinksMenuItem.Visibility = Visibility.Collapsed;
                            breakAllLinksMenuItem.Visibility = Visibility.Collapsed;
                        }
                    }
                }

                if (contextMenu.GetChild("interpViewerMenuItem") is MenuItem interpViewerMenuItem)
                {
                    string className = obj.Export.ClassName;
                    if (className == "InterpData"
                        || (className == "SeqAct_Interp" && obj is SAction action && action.Varlinks.Any() && action.Varlinks[0].Links.Any()
                            && Pcc.IsUExport(action.Varlinks[0].Links[0]) && Pcc.GetUExport(action.Varlinks[0].Links[0]).ClassName == "InterpData"))
                    {
                        interpViewerMenuItem.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        interpViewerMenuItem.Visibility = Visibility.Collapsed;
                    }
                }

                if (contextMenu.GetChild("cloneInterpDataMenuItem") is MenuItem cloneInterpDataMenuItem)
                {
                    string className = obj.Export.ClassName;
                    if (className == "InterpData")
                    {
                        cloneInterpDataMenuItem.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        cloneInterpDataMenuItem.Visibility = Visibility.Collapsed;
                    }
                }

                if (contextMenu.GetChild("plotEditorMenuItem") is MenuItem plotEditorMenuItem)
                {

                    if (Pcc.Game == MEGame.ME3 && obj is SAction sAction &&
                        sAction.Export.ClassName == "BioSeqAct_PMExecuteTransition" &&
                        sAction.Export.GetProperty<IntProperty>("m_nIndex") != null)
                    {
                        plotEditorMenuItem.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        plotEditorMenuItem.Visibility = Visibility.Collapsed;
                    }
                }

                if (contextMenu.GetChild("dialogueEditorMenuItem") is MenuItem dialogueEditorMenuItem)
                {

                    if (obj is SAction sAction &&
                        (sAction.Export.ClassName.EndsWith("SeqAct_StartConversation") || sAction.Export.ClassName.EndsWith("StartAmbientConv")) &&
                        sAction.Export.GetProperty<ObjectProperty>("Conv") != null)
                    {
                        dialogueEditorMenuItem.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        dialogueEditorMenuItem.Visibility = Visibility.Collapsed;
                    }
                }

                if (contextMenu.GetChild("openRefInPackEdMenuItem") is MenuItem openRefInPackEdMenuItem)
                {

                    if (Pcc.Game == MEGame.ME3 && obj is SVar sVar &&
                        Pcc.IsEntry(sVar.Export.GetProperty<ObjectProperty>("ObjValue")?.Value ?? 0))
                    {
                        openRefInPackEdMenuItem.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        openRefInPackEdMenuItem.Visibility = Visibility.Collapsed;
                    }
                }

                contextMenu.IsOpen = true;
                graphEditor.DisableDragging();
            }
        }

        private void removeAllLinks(object sender, RoutedEventArgs args)
        {
            ExportEntry export = (ExportEntry)((MenuItem)sender).Tag;
            removeAllLinks(export);
        }

        private void removeAllOutputLinks(object sender, RoutedEventArgs args)
        {
            ExportEntry export = (ExportEntry)((MenuItem)sender).Tag;
            var outLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            if (outLinksProp != null)
            {
                foreach (var prop in outLinksProp)
                {
                    prop.GetProp<ArrayProperty<StructProperty>>("Links").Clear();
                }
            }
            export.WriteProperty(outLinksProp);
        }

        private void removeAllVarLinks(object sender, RoutedEventArgs args)
        {
            ExportEntry export = (ExportEntry)((MenuItem)sender).Tag;
            var varLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
            if (varLinksProp != null)
            {
                foreach (var prop in varLinksProp)
                {
                    prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables").Clear();
                }
            }
            export.WriteProperty(varLinksProp);
        }

        private void removeAllEventLinks(object sender, RoutedEventArgs args)
        {
            ExportEntry export = (ExportEntry)((MenuItem)sender).Tag;
            var eventLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("EventLinks");
            if (eventLinksProp != null)
            {
                foreach (var prop in eventLinksProp)
                {
                    prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedEvents").Clear();
                }
            }
            export.WriteProperty(eventLinksProp);
        }

        private static void removeAllLinks(ExportEntry export)
        {
            var props = export.GetProperties();
            var outLinksProp = props.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
            if (outLinksProp != null)
            {
                foreach (var prop in outLinksProp)
                {
                    prop.GetProp<ArrayProperty<StructProperty>>("Links").Clear();
                }
            }

            var varLinksProp = props.GetProp<ArrayProperty<StructProperty>>("VariableLinks");
            if (varLinksProp != null)
            {
                foreach (var prop in varLinksProp)
                {
                    prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables").Clear();
                }
            }

            var eventLinksProp = props.GetProp<ArrayProperty<StructProperty>>("EventLinks");
            if (eventLinksProp != null)
            {
                foreach (var prop in eventLinksProp)
                {
                    prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedEvents").Clear();
                }
            }

            export.WriteProperties(props);
        }

        private void TrashAndRemoveFromSequence_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects_ListBox.SelectedItem is SObj sObj)
            {
                //remove incoming connections
                switch (sObj)
                {
                    case SVar sVar:
                        foreach (VarEdge edge in sVar.connections)
                        {
                            edge.originator.RemoveVarlink(edge);
                        }
                        break;
                    case SAction sAction:
                        foreach (SBox.InputLink inLink in sAction.InLinks)
                        {
                            foreach (ActionEdge edge in inLink.Edges)
                            {
                                edge.originator.RemoveOutlink(edge);
                            }
                        }
                        break;
                    case SEvent sEvent:
                        foreach (EventEdge edge in sEvent.connections)
                        {
                            edge.originator.RemoveEventlink(edge);
                        }
                        break;
                }

                //remove outgoing links
                removeAllLinks(sObj.Export);

                //remove from sequence
                var seqObjs = SelectedSequence.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
                var arrayObj = seqObjs?.FirstOrDefault(x => x.Value == sObj.UIndex);
                if (arrayObj != null)
                {
                    seqObjs.Remove(arrayObj);
                    SelectedSequence.WriteProperty(seqObjs);
                }

                //Trash
                EntryPruner.TrashEntryAndDescendants(sObj.Export);

            }
        }

        protected void node_MouseDown(object sender, PInputEventArgs e)
        {
            if (sender is SObj obj)
            {
                obj.posAtDragStart = obj.GlobalFullBounds;
                if (e.Button == System.Windows.Forms.MouseButtons.Right)
                {
                    panToSelection = false;
                    if (SelectedObjects.Count > 1)
                    {
                        CurrentObjects_ListBox.SelectedItems.Clear();
                        panToSelection = false;
                    }

                    CurrentObjects_ListBox.SelectedItem = obj;
                    OpenNodeContextMenu(obj);
                }
                else if (e.Shift || e.Control)
                {
                    panToSelection = false;
                    if (obj.IsSelected)
                    {
                        CurrentObjects_ListBox.SelectedItems.Remove(obj);
                    }
                    else
                    {
                        CurrentObjects_ListBox.SelectedItems.Add(obj);
                    }
                }
                else if (!obj.IsSelected)
                {
                    panToSelection = false;
                    CurrentObjects_ListBox.SelectedItem = obj;
                }
            }
        }

        private void node_Click(object sender, PInputEventArgs e)
        {
            if (sender is SObj obj)
            {
                if (e.Button != System.Windows.Forms.MouseButtons.Left && obj.GlobalFullBounds == obj.posAtDragStart)
                {
                    if (!e.Shift && !e.Control)
                    {
                        if (SelectedObjects.Count == 1 && obj.IsSelected) return;
                        panToSelection = false;
                        if (SelectedObjects.Count > 1)
                        {
                            CurrentObjects_ListBox.SelectedItems.Clear();
                            panToSelection = false;
                        }

                        CurrentObjects_ListBox.SelectedItem = obj;
                    }
                }
            }
        }

        private void SequenceEditorWPF_Closing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
                return;

            if (AutoSaveView_MenuItem.IsChecked)
                saveView();

            var options = new Dictionary<string, object>
            {
                {"OutputNumbers", SObj.OutputNumbers},
                {"AutoSave", AutoSaveView_MenuItem.IsChecked},
                {"GlobalSeqRefView", GlobalSeqRefViewSavesMenuItem.IsChecked}
            };
            string outputFile = JsonConvert.SerializeObject(options);
            if (!Directory.Exists(SequenceEditorDataFolder))
                Directory.CreateDirectory(SequenceEditorDataFolder);
            File.WriteAllText(OptionsPath, outputFile);

            //Code here remove these objects from leaking the window memory
            graphEditor.Camera.MouseDown -= backMouseDown_Handler;
            graphEditor.Camera.MouseUp -= back_MouseUp;
            graphEditor.Click -= graphEditor_Click;
            graphEditor.DragDrop -= SequenceEditor_DragDrop;
            graphEditor.DragEnter -= SequenceEditor_DragEnter;
            CurrentObjects.ForEach(x =>
            {
                x.MouseDown -= node_MouseDown;
                x.Click -= node_Click;
                x.Dispose();
            });
            CurrentObjects.Clear();
            graphEditor.Dispose();
            Properties_InterpreterWPF.Dispose();
            GraphHost.Child = null; //This seems to be required to clear OnChildGotFocus handler from WinFormsHost
            GraphHost.Dispose();
            DataContext = null;
            DispatcherHelper.EmptyQueue();
        }

        private void OpenInPackageEditor_Clicked(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects_ListBox.SelectedItem is SObj obj)
            {
                AllowWindowRefocus = false; //prevents flicker effect when windows try to focus and then package editor activates
                PackageEditorWPF p = new PackageEditorWPF();
                p.Show();
                p.LoadFile(obj.Export.FileRef.FilePath, obj.UIndex);
                p.Activate(); //bring to front
            }
        }

        private void OpenReferencedObjectInPackageEditor_Clicked(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects_ListBox.SelectedItem is SVar sVar && sVar.Export.GetProperty<ObjectProperty>("ObjValue") is ObjectProperty objProp)
            {
                AllowWindowRefocus = false; //prevents flicker effect when windows try to focus and then package editor activates
                PackageEditorWPF p = new PackageEditorWPF();
                p.Show();
                p.LoadFile(sVar.Export.FileRef.FilePath, objProp.Value);
                p.Activate(); //bring to front
            }
        }

        private void CloneInterpData_Clicked(object sender, RoutedEventArgs e)
        {
            if (SelectedObjects.HasExactly(1) && SelectedObjects[0] is SVar sVar && sVar.Export.ClassName == "InterpData")
            {
                addObject((ExportEntry)EntryCloner.cloneTree(sVar.Export));
            }
        }

        private void CloneObject_Clicked(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects_ListBox.SelectedItem is SObj obj)
            {
                ExportEntry clonedExport = cloneObject(obj.Export, SelectedSequence);
                customSaveData.Add(new SaveData
                {
                    index = clonedExport.Index,
                    X = graphEditor.Camera.ViewCenterX,
                    Y = graphEditor.Camera.ViewCenterY
                });
            }
        }

        static ExportEntry cloneObject(ExportEntry old, ExportEntry sequence, bool topLevel = true)
        {
            IMEPackage pcc = sequence.FileRef;
            ExportEntry exp = old.Clone();
            //needs to have the same index to work properly
            if (exp.ClassName == "SeqVar_External")
            {
                exp.indexValue = old.indexValue;
            }

            pcc.AddExport(exp);
            addObjectToSequence(exp, topLevel, sequence);
            cloneSequence(exp, sequence);
            return exp;
        }

        static void addObjectToSequence(ExportEntry newObject, bool removeLinks, ExportEntry sequenceExport)
        {
            var seqObjs = sequenceExport.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
            if (seqObjs != null)
            {
                seqObjs.Add(new ObjectProperty(newObject));
                sequenceExport.WriteProperty(seqObjs);

                PropertyCollection newObjectProps = newObject.GetProperties();
                newObjectProps.AddOrReplaceProp(new ObjectProperty(sequenceExport, "ParentSequence"));
                if (removeLinks)
                {
                    var outLinksProp = newObjectProps.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
                    if (outLinksProp != null)
                    {
                        foreach (var prop in outLinksProp)
                        {
                            prop.GetProp<ArrayProperty<StructProperty>>("Links").Clear();
                        }
                    }

                    var varLinksProp = newObjectProps.GetProp<ArrayProperty<StructProperty>>("VariableLinks");
                    if (varLinksProp != null)
                    {
                        foreach (var prop in varLinksProp)
                        {
                            prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables").Clear();
                        }
                    }
                }

                newObject.WriteProperties(newObjectProps);
                newObject.Parent = sequenceExport;
            }
        }

        static void cloneSequence(ExportEntry exp, ExportEntry parentSequence)
        {
            IMEPackage pcc = exp.FileRef;
            if (exp.ClassName == "Sequence")
            {
                var seqObjs = exp.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
                if (seqObjs == null || seqObjs.Count == 0)
                {
                    return;
                }

                //store original list of sequence objects;
                List<int> oldObjects = seqObjs.Select(x => x.Value).ToList();

                //clear original sequence objects
                seqObjs.Clear();
                exp.WriteProperty(seqObjs);

                //clone all children
                foreach (var obj in oldObjects)
                {
                    cloneObject(pcc.GetUExport(obj), exp, false);
                }

                //re-point children's links to new objects
                seqObjs = exp.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
                foreach (var seqObj in seqObjs)
                {
                    ExportEntry obj = pcc.GetUExport(seqObj.Value);
                    var props = obj.GetProperties();
                    var outLinksProp = props.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
                    if (outLinksProp != null)
                    {
                        foreach (var outLinkStruct in outLinksProp)
                        {
                            var links = outLinkStruct.GetProp<ArrayProperty<StructProperty>>("Links");
                            foreach (var link in links)
                            {
                                var linkedOp = link.GetProp<ObjectProperty>("LinkedOp");
                                linkedOp.Value = seqObjs[oldObjects.IndexOf(linkedOp.Value)].Value;
                            }
                        }
                    }

                    var varLinksProp = props.GetProp<ArrayProperty<StructProperty>>("VariableLinks");
                    if (varLinksProp != null)
                    {
                        foreach (var varLinkStruct in varLinksProp)
                        {
                            var links = varLinkStruct.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables");
                            foreach (var link in links)
                            {
                                link.Value = seqObjs[oldObjects.IndexOf(link.Value)].Value;
                            }
                        }
                    }

                    var eventLinksProp = props.GetProp<ArrayProperty<StructProperty>>("EventLinks");
                    if (eventLinksProp != null)
                    {
                        foreach (var eventLinkStruct in eventLinksProp)
                        {
                            var links = eventLinkStruct.GetProp<ArrayProperty<ObjectProperty>>("LinkedEvents");
                            foreach (var link in links)
                            {
                                link.Value = seqObjs[oldObjects.IndexOf(link.Value)].Value;
                            }
                        }
                    }

                    obj.WriteProperties(props);
                }

                //re-point sequence links to new objects
                int oldObj;
                int newObj;
                var propCollection = exp.GetProperties();
                var inputLinksProp = propCollection.GetProp<ArrayProperty<StructProperty>>("InputLinks");
                if (inputLinksProp != null)
                {
                    foreach (var inLinkStruct in inputLinksProp)
                    {
                        var linkedOp = inLinkStruct.GetProp<ObjectProperty>("LinkedOp");
                        oldObj = linkedOp.Value;
                        if (oldObj != 0)
                        {
                            newObj = seqObjs[oldObjects.IndexOf(oldObj)].Value;
                            linkedOp.Value = newObj;

                            NameProperty linkAction = inLinkStruct.GetProp<NameProperty>("LinkAction");
                            linkAction.Value = new NameReference(linkAction.Value.Name, pcc.GetUExport(newObj).indexValue);
                        }
                    }
                }

                var outputLinksProp = propCollection.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
                if (outputLinksProp != null)
                {
                    foreach (var outLinkStruct in outputLinksProp)
                    {
                        var linkedOp = outLinkStruct.GetProp<ObjectProperty>("LinkedOp");
                        oldObj = linkedOp.Value;
                        if (oldObj != 0)
                        {
                            newObj = seqObjs[oldObjects.IndexOf(oldObj)].Value;
                            linkedOp.Value = newObj;

                            NameProperty linkAction = outLinkStruct.GetProp<NameProperty>("LinkAction");
                            linkAction.Value = new NameReference(linkAction.Value.Name, pcc.GetUExport(newObj).indexValue);
                        }
                    }
                }

                exp.WriteProperties(propCollection);
            }
            else if (exp.ClassName == "SequenceReference")
            {
                //set OSequenceReference to new sequence
                var oSeqRefProp = exp.GetProperty<ObjectProperty>("oSequenceReference");
                if (oSeqRefProp == null || oSeqRefProp.Value == 0)
                {
                    return;
                }

                int oldSeqIndex = oSeqRefProp.Value;
                oSeqRefProp.Value = exp.UIndex + 1;
                exp.WriteProperty(oSeqRefProp);

                //clone sequence
                cloneObject(pcc.GetUExport(oldSeqIndex), parentSequence, false);

                //remove cloned sequence from SeqRef's parent's sequenceobjects
                var seqObjs = parentSequence.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
                seqObjs.RemoveAt(seqObjs.Count - 1);
                parentSequence.WriteProperty(seqObjs);

                //set SequenceReference's linked name indices
                var inputIndices = new List<int>();
                var outputIndices = new List<int>();

                ExportEntry newSequence = pcc.GetUExport(exp.UIndex + 1);
                var props = newSequence.GetProperties();
                var inLinksProp = props.GetProp<ArrayProperty<StructProperty>>("InputLinks");
                if (inLinksProp != null)
                {
                    foreach (var inLink in inLinksProp)
                    {
                        inputIndices.Add(inLink.GetProp<NameProperty>("LinkAction").Value.Number);
                    }
                }

                var outLinksProp = props.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
                if (outLinksProp != null)
                {
                    foreach (var outLinks in outLinksProp)
                    {
                        outputIndices.Add(outLinks.GetProp<NameProperty>("LinkAction").Value.Number);
                    }
                }

                props = exp.GetProperties();
                inLinksProp = props.GetProp<ArrayProperty<StructProperty>>("InputLinks");
                if (inLinksProp != null)
                {
                    for (int i = 0; i < inLinksProp.Count; i++)
                    {
                        NameProperty linkAction = inLinksProp[i].GetProp<NameProperty>("LinkAction");
                        linkAction.Value = new NameReference(linkAction.Value.Name, inputIndices[i]);
                    }
                }

                outLinksProp = props.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
                if (outLinksProp != null)
                {
                    for (int i = 0; i < outLinksProp.Count; i++)
                    {
                        NameProperty linkAction = outLinksProp[i].GetProp<NameProperty>("LinkAction");
                        linkAction.Value = new NameReference(linkAction.Value.Name, outputIndices[i]);
                    }
                }

                exp.WriteProperties(props);

                //set new Sequence's link and ParentSequence prop to SeqRef
                newSequence.WriteProperty(new ObjectProperty(exp.UIndex, "ParentSequence"));
                newSequence.idxLink = exp.UIndex;

                //set DefaultViewZoom to magic number to flag that this is a cloned Sequence Reference and global saves cannot be used with it
                //ugly, but it should work
                newSequence.WriteProperty(new FloatProperty(CLONED_SEQREF_MAGIC, "DefaultViewZoom"));
            }
        }

        private void ContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            graphEditor.AllowDragging();
            if (AllowWindowRefocus)
            {
                Focus(); //this will make window bindings work, as context menu is not part of the visual tree, and focus will be on there if the user clicked it.
            }

            AllowWindowRefocus = true;
        }

        private void CurrentObjectsList_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems?.Cast<SObj>().ToList() is List<SObj> deselectedEntries)
            {
                SelectedObjects.RemoveRange(deselectedEntries);
                foreach (SObj obj in deselectedEntries)
                {
                    obj.IsSelected = false;
                }
            }

            if (e.AddedItems?.Cast<SObj>().ToList() is IList<SObj> selectedEntries)
            {
                SelectedObjects.AddRange(selectedEntries);
                foreach (SObj obj in selectedEntries)
                {
                    obj.IsSelected = true;
                }
            }

            if (SelectedObjects.Count == 1)
            {
                Properties_InterpreterWPF.LoadExport(SelectedObjects[0].Export);
            }
            else if (!(Properties_InterpreterWPF.CurrentLoadedExport?.IsSequence() ?? false))
            {
                Properties_InterpreterWPF.UnloadExport();
            }

            if (SelectedObjects.Any())
            {
                if (panToSelection)
                {
                    if (SelectedObjects.Count == 1)
                    {
                        graphEditor.Camera.AnimateViewToCenterBounds(SelectedObjects[0].GlobalFullBounds, false, 100);
                    }
                    else
                    {
                        RectangleF boundingBox = SelectedObjects.Select(obj => obj.GlobalFullBounds).BoundingRect();
                        graphEditor.Camera.AnimateViewToCenterBounds(boundingBox, true, 200);
                    }
                }
            }

            panToSelection = true;
            graphEditor.Refresh();
        }

        private void SaveImage()
        {
            if (CurrentObjects.Count == 0)
                return;
            string objectName = System.Text.RegularExpressions.Regex.Replace(SelectedSequence.ObjectName.Name, @"[<>:""/\\|?*]", "");
            SaveFileDialog d = new SaveFileDialog
            {
                Filter = "PNG Files (*.png)|*.png",
                FileName = $"{CurrentFile}.{objectName}"
            };
            if (d.ShowDialog() == true)
            {
                PNode r = graphEditor.Root;
                RectangleF rr = r.GlobalFullBounds;
                PNode p = PPath.CreateRectangle(rr.X, rr.Y, rr.Width, rr.Height);
                p.Brush = Brushes.White;
                graphEditor.addBack(p);
                graphEditor.Camera.Visible = false;
                System.Drawing.Image image = graphEditor.Root.ToImage();
                graphEditor.Camera.Visible = true;
                image.Save(d.FileName, ImageFormat.Png);
                graphEditor.backLayer.RemoveAllChildren();
                MessageBox.Show(this, "Done.");
            }
        }

        private void addObject(ExportEntry exportToAdd, bool removeLinks = true)
        {
            customSaveData.Add(new SaveData
            {
                index = exportToAdd.Index,
                X = graphEditor.Camera.ViewCenterX,
                Y = graphEditor.Camera.ViewCenterY
            });
            addObjectToSequence(exportToAdd, removeLinks, SelectedSequence);
        }

        private void AddObject_Clicked(object sender, RoutedEventArgs e)
        {
            if (EntrySelector.GetEntry<ExportEntry>(this, Pcc) is ExportEntry exportToAdd)
            {
                if (!exportToAdd.IsOrInheritsFrom("SequenceObject"))
                {
                    MessageBox.Show(this, $"#{exportToAdd.UIndex}: {exportToAdd.ObjectName.Instanced} is not a sequence object.");
                    return;
                }

                if (CurrentObjects.Any(obj => obj.Export == exportToAdd))
                {
                    MessageBox.Show(this, $"#{exportToAdd.UIndex}: {exportToAdd.ObjectName.Instanced} is already in the sequence.");
                    return;
                }

                addObject(exportToAdd);
            }
        }

        private void showOutputNumbers_Click(object sender, EventArgs e)
        {
            SObj.OutputNumbers = ShowOutputNumbers_MenuItem.IsChecked;
            if (CurrentObjects.Any())
            {
                RefreshView();
            }

        }

        private void OpenInInterpViewer_Clicked(object sender, RoutedEventArgs e)
        {
            if (Pcc.Game != MEGame.ME3)
            {
                MessageBox.Show(this, "InterpViewer does not support ME1 or ME2 yet.", "Sorry!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (CurrentObjects_ListBox.SelectedItem is SObj obj)
            {
                int uIndex;
                ExportEntry exportEntry = obj.Export;
                if (exportEntry.IsOrInheritsFrom("InterpData"))
                {
                    uIndex = exportEntry.UIndex;
                }
                else if (obj is SAction sAction && sAction.Varlinks.Any() && sAction.Varlinks[0].Links.Any())
                {
                    uIndex = sAction.Varlinks[0].Links[0];
                }
                else
                {
                    MessageBox.Show(this, "No InterpData to open!", "Sorry!", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                AllowWindowRefocus = false; //prevents flicker effect when windows try to focus and then package editor activates
                var p = new InterpEditor();
                p.Show();
                p.LoadFile(Pcc.FilePath);
                p.SelectedInterpData = Pcc.GetUExport(uIndex);
            }
        }

        private void OpenInDialogueEditor_Clicked(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects_ListBox.SelectedItem is SObj obj &&
                (obj.Export.ClassName.EndsWith("SeqAct_StartConversation") || obj.Export.ClassName.EndsWith("StartAmbientConv")) &&
                obj.Export.GetProperty<ObjectProperty>("Conv") is ObjectProperty conv)
            {
                if (Pcc.IsUExport(conv.Value))
                {
                    AllowWindowRefocus = false; //prevents flicker effect when windows try to focus and then package editor activates
                    new Dialogue_Editor.DialogueEditorWPF(Pcc.GetUExport(conv.Value)).Show();
                    return;
                }

                if (Pcc.IsImport(conv.Value))
                {
                    ImportEntry convImport = Pcc.GetImport(conv.Value);
                    string extension = Path.GetExtension(Pcc.FilePath);
                    string noExtensionPath = Path.ChangeExtension(Pcc.FilePath, null);
                    string loc_int = Pcc.Game == MEGame.ME1 ? "_LOC_int" : "_LOC_INT";
                    string convFilePath = noExtensionPath + loc_int + extension;
                    if (File.Exists(convFilePath))
                    {
                        using (var convFile = MEPackageHandler.OpenMEPackage(convFilePath))
                        {
                            var convExport = convFile.Exports.FirstOrDefault(x => x.ObjectName == convImport.ObjectName);
                            if (convExport != null)
                            {
                                AllowWindowRefocus = false; //prevents flicker effect when windows try to focus and then package editor activates
                                new Dialogue_Editor.DialogueEditorWPF(convExport).Show();
                                return;
                            }
                        }
                    }
                }
            }
            MessageBox.Show(this, "Cannot find Conversation!", "Sorry!", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void GlobalSeqRefViewSavesMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects.Any())
            {
                SetupJSON(SelectedSequence);
            }
        }

        private void SequenceEditorWPF_Loaded(object sender, RoutedEventArgs e)
        {
            if (FileQueuedForLoad != null)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    //Wait for all children to finish loading
                    LoadFile(FileQueuedForLoad);
                    FileQueuedForLoad = null;

                    if (ExportQueuedForFocusing != null)
                    {
                        GoToExport(ExportQueuedForFocusing);
                        ExportQueuedForFocusing = null;
                    }

                    Activate();
                }));
            }
        }

        private void GoToExport(ExportEntry export, bool selectSequences = true)
        {
            foreach (ExportEntry exp in SequenceExports)
            {
                if (selectSequences && export == exp)
                {
                    if (export.ClassName == "SequenceReference")
                    {
                        var sequenceprop = exp.GetProperty<ObjectProperty>("oSequenceReference");
                        if (sequenceprop != null)
                        {
                            export = Pcc.GetUExport(sequenceprop.Value);
                        }
                        else
                        {
                            return;
                        }
                    }

                    SelectedItem = TreeViewRootNodes.SelectMany(node => node.FlattenTree()).First(node => node.UIndex == export.UIndex);
                    break;
                }

                ExportEntry sequence = exp;
                if (sequence.ClassName == "SequenceReference")
                {
                    var sequenceprop = sequence.GetProperty<ObjectProperty>("oSequenceReference");
                    if (sequenceprop != null)
                    {
                        sequence = Pcc.GetUExport(sequenceprop.Value);
                    }
                    else
                    {
                        return;
                    }
                }

                var seqObjs = sequence.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
                if (seqObjs != null && seqObjs.Any(objProp => objProp.Value == export.UIndex))
                {
                    //This is our sequence
                    SelectedItem = TreeViewRootNodes.SelectMany(node => node.FlattenTree()).First(node => node.UIndex == sequence.UIndex);
                    CurrentObjects_ListBox.SelectedItem = CurrentObjects.FirstOrDefault(x => x.Export == export);
                    break;
                }
            }
        }
        //TODO: Make this work for ME2 and ME1
        private void PlotEditorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Pcc.Game == MEGame.ME3 &&
                CurrentObjects_ListBox.SelectedItem is SAction sAction &&
                sAction.Export.ClassName == "BioSeqAct_PMExecuteTransition" &&
                sAction.Export.GetProperty<IntProperty>("m_nIndex")?.Value is int m_nIndex)
            {
                var plotFiles = MELoadedFiles.GetEnabledDLCFiles(MEGame.ME3).OrderByDescending(dir => MELoadedFiles.GetMountPriority(dir, MEGame.ME3))
                                              .Select(dir => Path.Combine(dir, "CookedPCConsole", $"Startup_{MELoadedFiles.GetDLCNameFromDir(dir)}_INT.pcc"))
                                              .Append(Path.Combine(ME3Directory.cookedPath, "SFXGameInfoSP_SF.pcc"))
                                              .Where(File.Exists);
                string filePath = null;
                foreach (string plotFile in plotFiles)
                {
                    using (IMEPackage pcc = MEPackageHandler.OpenMEPackage(plotFile))
                    {
                        if (StateEventMapView.TryFindStateEventMap(pcc, out ExportEntry export))
                        {
                            var stateEventMap = BinaryBioStateEventMap.Load(export);
                            if (stateEventMap.StateEvents.ContainsKey(m_nIndex))
                            {
                                filePath = plotFile;
                            }
                        }
                    }
                }

                if (filePath != null)
                {

                    var plotEd = new PlotEditor();
                    plotEd.Show();
                    plotEd.LoadFile(filePath);
                    plotEd.GoToStateEvent(m_nIndex);
                }
                else
                {
                    MessageBox.Show(this, $"Could not find State Event {m_nIndex}");
                }
            }
        }

        private void RepointIncomingReferences_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects_ListBox.SelectedItem is SVar sVar)
            {
                if (EntrySelector.GetEntry<ExportEntry>(this, Pcc) is ExportEntry export)
                {
                    if (CurrentObjects.All(x => x.Export != export))
                    {
                        MessageBox.Show($"#{export.UIndex} {export.ObjectName.Instanced}  is not part of this sequence, and can't be repointed to.");
                        return;
                    }
                    var sequence = sVar.Export.FileRef.GetUExport(sVar.Export.GetProperty<ObjectProperty>("ParentSequence").Value);
                    var sequenceObjects = sequence.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
                    foreach (var seqObjRef in sequenceObjects)
                    {
                        var saveProps = false;
                        var seqObj = sVar.Export.FileRef.GetUExport(seqObjRef.Value);
                        var props = seqObj.GetProperties();
                        var variableLinks = props.GetProp<ArrayProperty<StructProperty>>("VariableLinks");
                        if (variableLinks != null)
                        {
                            foreach (var variableLink in variableLinks)
                            {
                                var linkedVars = variableLink.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables");
                                if (linkedVars != null)
                                {
                                    foreach (var linkedVar in linkedVars)
                                    {
                                        if (linkedVar.Value == sVar.Export.UIndex)
                                        {
                                            linkedVar.Value = export.UIndex; //repoint
                                            saveProps = true;
                                        }
                                    }
                                }
                            }
                        }

                        if (saveProps)
                        {
                            seqObj.WriteProperties(props);
                        }
                    }
                    RefreshView();
                }
            }

        }

        private void ShowAdditionalInfoInCommentTextMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.SequenceEditor_ShowParsedInfo = !Properties.Settings.Default.SequenceEditor_ShowParsedInfo;
            Properties.Settings.Default.Save();
        }

        private void IntegerUpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (CurrentObjects.Any())
            {
                RefreshView();
            }
        }
    }
    static class SequenceEditorExtensions
    {
        public static bool IsSequence(this IEntry entry) => entry.IsOrInheritsFrom("Sequence");
    }
}