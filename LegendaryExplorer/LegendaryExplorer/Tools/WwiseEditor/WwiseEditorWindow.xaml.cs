using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.ExperimentsTools;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.ToolsetDev.MemoryAnalyzer;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.UserControls.SharedToolControls;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.AppCenter.Analytics;
using Microsoft.Win32;
using Newtonsoft.Json;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using Piccolo;
using Piccolo.Event;
using Piccolo.Nodes;
using Brushes = System.Drawing.Brushes;
using Color = System.Drawing.Color;
using Image = System.Drawing.Image;
using Path = System.IO.Path;

namespace LegendaryExplorer.Tools.WwiseEditor
{
    /// <summary>
    /// Interaction logic for WwiseEditorWPF.xaml
    /// </summary>
    public partial class WwiseEditorWindow : WPFBase, IRecents
    {
        private struct SaveData
        {
            public uint ID;
            public float X;
            public float Y;
        }
        private readonly WwiseGraphEditor graphEditor;
        public WwiseEditorWindow() : base("Wwise Editor")
        {
            DataContext = this;
            StatusText = "Select package file to load";
            LoadCommands();
            InitializeComponent();

            RecentsController.InitRecentControl(Toolname, Recents_MenuItem, fileName=>LoadFile(fileName));

            graphEditor = (WwiseGraphEditor)GraphHost.Child;
            graphEditor.BackColor = GraphEditorBackColor;

            AutoSaveView_MenuItem.IsChecked = Misc.AppSettings.Settings.WwiseGraphEditor_AutoSaveView;

            soundPanel.SoundPanel_TabsControl.SelectedIndex = 1;
            soundPanel.HIRCObjectSelected += SoundPanel_HIRCObjectSelected;
        }

        public WwiseEditorWindow(ExportEntry exportToLoad) : this()
        {
            FileQueuedForLoad = exportToLoad.FileRef.FilePath;
            ExportQueuedForFocusing = exportToLoad;
        }

        public WwiseEditorWindow(string filePath, int uIndex = 0) : this()
        {
            FileQueuedForLoad = filePath;
            ExportQueuedForFocusing = null;
            UIndexQueuedForFocusing = uIndex;
        }

        public ObservableCollectionExtended<ExportEntry> WwiseBankExports { get; } = new();
        public ObservableCollectionExtended<WwiseHircObjNode> CurrentObjects { get; } = new();
        public ObservableCollectionExtended<WwiseBusTreeEntry> BusHierarchy { get; } = new();
        public ObservableCollectionExtended<WwiseEventLookup> KnownEvents { get; } = new();

        private List<SaveData> SavedPositions;

        private string FileQueuedForLoad;
        private ExportEntry ExportQueuedForFocusing;
        private readonly int UIndexQueuedForFocusing;
        private static readonly Color GraphEditorBackColor = Color.FromArgb(167, 167, 167);
        public string CurrentFile;
        public string JSONpath;

        private ExportEntry _currentExport;
        public ExportEntry CurrentExport
        {
            get => _currentExport;
            set
            {
                if (AutoSaveView_MenuItem.IsChecked)
                {
                    SaveView();
                }
                if (SetProperty(ref _currentExport, value))
                {
                    LoadBank(value, true);
                }
            }
        }

        private WwiseHircObjNode _selectedNode;
        public WwiseHircObjNode SelectedNode
        {
            get => _selectedNode;
            private set
            {
                if (value != _selectedNode && _selectedNode != null)
                {
                    _selectedNode.IsSelected = false;
                }
                if (SetProperty(ref _selectedNode, value) && value != null)
                {
                    value.IsSelected = true;
                    if (panToSelection)
                    {
                        graphEditor.Camera.AnimateViewToCenterBounds(value.GlobalFullBounds, false, 100);
                    }

                    if (!(value is WExport))
                    {
                        soundPanel.HIRC_ListBox.SelectedIndex = CurrentObjects.IndexOf(value);
                        binaryInterpreter.TrySelectHIRCItem(GetHIRCItemIndex(value.ID));
                    }
                }
            }
        }

        /// <summary>
        /// Returns the position of the HIRC object with the given ID within the currently loaded bank's HIRC
        /// array (file order), or -1 if not found. This is the same order the "Binary Interpreter" tab's HIRC
        /// items tree is built in, so it's used to jump straight to a given object's full field breakdown there.
        /// </summary>
        private int GetHIRCItemIndex(uint id)
        {
            int index = 0;
            foreach (uint hircId in CurrentWwiseBank.HIRCObjects.Keys)
            {
                if (hircId == id)
                {
                    return index;
                }
                index++;
            }

            return -1;
        }

        private WwiseBankParsed CurrentWwiseBank;

        /// <summary>
        /// Whether to draw extra edges in the graph showing the actor-mixer hierarchy (which node routes its
        /// audio through which child), in addition to the normal Event/Action/Sound flow edges.
        /// </summary>
        private bool ShowBusRoutingHierarchy;

        private void ShowBusRoutingHierarchy_Click(object sender, RoutedEventArgs e)
        {
            ShowBusRoutingHierarchy = ShowBusRoutingHierarchy_MenuItem.IsChecked;
            if (CurrentWwiseBank != null)
            {
                RefreshView();
            }
        }

        private void LoadAdditionalWwiseNames_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult source = MessageBox.Show(this,
                "Select Yes to scan a package file, or No to load a text file containing one Wwise name per line.",
                "Load Additional Wwise Names", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (source == MessageBoxResult.Cancel)
            {
                return;
            }

            OpenFileDialog dialog = new()
            {
                Filter = source == MessageBoxResult.Yes
                    ? GameFileFilters.ME3ME2SaveFileFilter
                    : "Text files|*.txt|All files|*.*",
                CustomPlaces = AppDirectories.GameCustomPlaces,
                Title = source == MessageBoxResult.Yes
                    ? "Select package to scan for Wwise names"
                    : "Select text file containing Wwise names"
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                List<string> names = source == MessageBoxResult.Yes
                    ? ReadPackageNames(dialog.FileName)
                    : File.ReadLines(dialog.FileName).ToList();
                int added = graphEditor.AddSupplementalNames(names);

                // Rebuild the visible bank so newly resolved names are reflected in node comments, event-action
                // titles, and output labels. The resolver itself remains alive across bank/package switches.
                if (CurrentExport != null)
                {
                    RefreshView();
                }

                MessageBox.Show(this,
                    $"Loaded {added:N0} new Wwise name{(added == 1 ? "" : "s")} into the current tool session.",
                    "Wwise Names Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Unable to load Wwise names:\n{ex.Message}",
                    "Load Additional Wwise Names", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static List<string> ReadPackageNames(string filePath)
        {
            using IMEPackage package = MEPackageHandler.OpenMEPackage(filePath);
            return GetPackageNames(package).ToList();
        }

        private static IEnumerable<string> GetPackageNames(IMEPackage package)
        {
            return package.Names
                .Concat(package.Exports.Select(entry => entry.ObjectName.Instanced))
                .Concat(package.Imports.Select(entry => entry.ObjectName.Instanced));
        }

        private void RefreshKnownEvents()
        {
            var events = new List<WwiseEventLookup>();
            foreach (WEvent eventNode in CurrentObjects.OfType<WEvent>())
            {
                if (graphEditor.NameResolver.TryResolve(eventNode.ID, out string resolvedName))
                {
                    events.Add(new WwiseEventLookup(resolvedName, new[] { eventNode.ID }));
                }
            }

            KnownEvents.ReplaceAll(events
                .GroupBy(eventLookup => eventLookup.Name, System.StringComparer.OrdinalIgnoreCase)
                .Select(group => new WwiseEventLookup(group.Key, group.SelectMany(eventLookup => eventLookup.IDs)))
                .OrderBy(eventLookup => eventLookup.Name, System.StringComparer.OrdinalIgnoreCase));
            KnownEvents_ComboBox.SelectedIndex = -1;
        }

        private void KnownEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0 || e.AddedItems[0] is not WwiseEventLookup selectedEvent)
            {
                return;
            }

            WwiseHircObjNode node = CurrentObjects.OfType<WEvent>()
                .FirstOrDefault(eventNode => selectedEvent.Matches(eventNode.ID));
            node ??= CurrentObjects.OfType<WExport>()
                .FirstOrDefault(eventExport => eventExport.Export.ClassName == "WwiseEvent" &&
                    (selectedEvent.Matches(eventExport.ID) ||
                     selectedEvent.Matches(SharedMethods.CalculateFNV132Hash(eventExport.Export.ObjectName.Instanced))));
            if (node == null)
            {
                return;
            }

            // Select first so the normal graph selection state and the sound panel stay in sync. Schedule the
            // camera move after the selection handlers have run; those handlers can invalidate the camera's
            // current view before the graph node is ready to be focused.
            panToSelection = false;
            SelectedNode = node;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                if (!CurrentObjects.Contains(node))
                {
                    return;
                }

                RectangleF bounds = node.GlobalFullBounds;
                bounds.Inflate(Math.Max(bounds.Width, 120), Math.Max(bounds.Height, 90));
                graphEditor.Camera.AnimateViewToCenterBounds(bounds, true, 300);
            }));
        }

        public ICommand OpenCommand { get; set; }
        public ICommand SaveCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }
        public ICommand SaveImageCommand { get; set; }
        public ICommand SaveViewCommand { get; set; }

        private void LoadCommands()
        {
            OpenCommand = new GenericCommand(OpenFile);
            SaveCommand = new GenericCommand(SavePackage, IsPackageLoaded);
            SaveAsCommand = new GenericCommand(SavePackageAs, IsPackageLoaded);
            SaveImageCommand = new GenericCommand(SaveImage, () => CurrentObjects.Any);
            SaveViewCommand = new GenericCommand(() => SaveView(), () => CurrentObjects.Any);
        }

        private bool IsPackageLoaded() => Pcc != null;

        private async void SavePackageAs()
        {
            string extension = Path.GetExtension(Pcc.FilePath);
            SaveFileDialog d = new () { Filter = $"*{extension}|*{extension}" };
            if (d.ShowDialog() == true)
            {
                await Pcc.SaveAsync(d.FileName);
                MessageBox.Show(this, "Done.");
            }
        }

        private async void SavePackage()
        {
            await Pcc.SaveAsync();
        }

        private void OpenFile()
        {
            OpenFileDialog d = new ()
            {
                Filter = GameFileFilters.ME3ME2SaveFileFilter,
                CustomPlaces = AppDirectories.GameCustomPlaces
            };
            if (d.ShowDialog() == true)
            {
#if !DEBUG
                try
                {
#endif

                LoadFile(d.FileName);
#if !DEBUG
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open file:\n" + ex.Message);
                }
#endif
            }
        }

        public void LoadFile(string s, int goToIndex = 0)
        {
            try
            {
                Properties_InterpreterWPF.UnloadExport();
                binaryInterpreter.UnloadExport();
                soundPanel.FreeAudioResources();
                SelectedNode = null;

                StatusBar_LeftMostText.Text =
                    $"Loading {Path.GetFileName(s)} ({FileSize.FormatSize(new FileInfo(s).Length)})";
                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
                LoadMEPackage(s);
                CurrentFile = Path.GetFileName(s);
                graphEditor.BusNames = WwiseBusNames.ForGame(Pcc.Game);
                graphEditor.SetPackageNames(GetPackageNames(Pcc));

                graphEditor.nodeLayer.RemoveAllChildren();
                graphEditor.edgeLayer.RemoveAllChildren();
                BusHierarchy.ClearEx();

                WwiseBankExports.ReplaceAll(Pcc.Exports.Where(exp => exp.ClassName == "WwiseBank"));

                if (WwiseBankExports.IsEmpty())
                {
                    UnLoadMEPackage();
                    MessageBox.Show(this, "This file does not contain any WwiseBanks!");
                    StatusText = "Select a package file to load";
                    Title = "Wwise Editor";
                    CurrentFile = null;
                    soundPanelColumn.Width = GridLength.Auto;
                    return;
                }

                StatusBar_LeftMostText.Text = Path.GetFileName(s);
                Title = $"Wwise Editor - {s}";

                RecentsController.AddRecent(s, false, Pcc?.Game);
                RecentsController.SaveRecentList(true);
                if (goToIndex != 0)
                {
                    CurrentExport = WwiseBankExports.FirstOrDefault(x => x.UIndex == goToIndex);
                    ExportQueuedForFocusing = CurrentExport;
                }
                else
                {
                    CurrentExport = null;
                }

                soundPanelColumn.Width = new GridLength(425);
            }
            catch (Exception e)
            {
                StatusBar_LeftMostText.Text = "Failed to load " + Path.GetFileName(s);
                MessageBox.Show($"Error loading {Path.GetFileName(s)}:\n{e.Message}");
                UnLoadMEPackage();
                Title = "Wwise Editor";
                CurrentFile = null;
                soundPanelColumn.Width = GridLength.Auto;
            }
        }

        public void LoadBank(ExportEntry export, bool fromFile = false)
        {
            if (export == null)
            {
                return;
            }
            graphEditor.Enabled = false;
            graphEditor.UseWaitCursor = true;

            CurrentWwiseBank = export.GetBinaryData<WwiseBankParsed>();
            graphEditor.BusRouting = WwiseBusRouting.Parse(CurrentWwiseBank.BnkFile);
            SetupJSON(export);
            Properties_InterpreterWPF.LoadExport(export);
            binaryInterpreter.LoadExport(export);
            soundPanel.LoadExport(export);

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
            }
            try
            {
                GenerateGraph();
            }
            catch (Exception e) when (!App.IsDebug)
            {
                MessageBox.Show(this, $"Error loading WwiseBank:\n{e.Message}");
            }
            graphEditor.Enabled = true;
            graphEditor.UseWaitCursor = false;
        }

        private void GenerateGraph()
        {
            graphEditor.nodeLayer.RemoveAllChildren();
            graphEditor.edgeLayer.RemoveAllChildren();
            GetObjects(CurrentWwiseBank);
            Layout();
            foreach (var o in CurrentObjects)
            {
                o.MouseDown += Node_MouseDown;
            }

            if (SavedPositions.IsEmpty())
            {
                AutoLayout();
            }

            BuildBusHierarchy();
            RefreshKnownEvents();
        }

        /// <summary>
        /// Builds the bus parent/child tree from the parsed <see cref="WwiseBankParsed.Bus"/> objects across every
        /// WwiseBank export in the loaded package. Most buses are only defined once (often in a shared/init bank)
        /// and merely referenced elsewhere, so a single bank's HIRC objects are rarely the full picture.
        /// Names come from a loaded bus ID map (see <see cref="LoadBusNameMap(string)"/>); IDs without a known name
        /// are labelled by their hex ID, since Wwise bus names aren't stored in cooked banks.
        /// </summary>
        private void BuildBusHierarchy()
        {
            var buses = new Dictionary<uint, WwiseBankParsed.Bus>();
            foreach (ExportEntry bankExport in Pcc.Exports.Where(exp => exp.ClassName == "WwiseBank"))
            {
                WwiseBankParsed bank = bankExport == CurrentExport ? CurrentWwiseBank : bankExport.GetBinaryData<WwiseBankParsed>();
                foreach (WwiseBankParsed.Bus bus in bank.HIRCObjects.Values.OfType<WwiseBankParsed.Bus>())
                {
                    buses.TryAdd(bus.ID, bus);
                }
            }

            Dictionary<uint, WwiseBusTreeEntry> entriesByID = buses.Values.ToDictionary(bus => bus.ID,
                bus => new WwiseBusTreeEntry(bus, graphEditor.BusNames.GetValueOrDefault(bus.ID)));

            var roots = new List<WwiseBusTreeEntry>();
            foreach (WwiseBankParsed.Bus bus in buses.Values)
            {
                WwiseBusTreeEntry entry = entriesByID[bus.ID];
                if (bus.ParentID != 0 && entriesByID.TryGetValue(bus.ParentID, out WwiseBusTreeEntry parentEntry))
                {
                    parentEntry.Children.Add(entry);
                }
                else
                {
                    roots.Add(entry);
                }
            }

            BusHierarchy.ReplaceAll(roots.OrderBy(r => r.DisplayName));
        }

        private void BusHierarchy_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is WwiseBusTreeEntry entry &&
                CurrentObjects.FirstOrDefault(node => !(node is WExport) && node.ID == entry.ID) is { } nodeToSelect)
            {
                panToSelection = true;
                SelectedNode = nodeToSelect;
            }
        }

        private void GetObjects(WwiseBankParsed bank)
        {
            var newObjs = new List<WwiseHircObjNode>();
            foreach ((uint id, WwiseBankParsed.HIRCObject hircObject) in CurrentWwiseBank.HIRCObjects)
            {
                newObjs.Add(hircObject switch
                {
                    WwiseBankParsed.Event evt => new WEvent(evt, 0, 0, graphEditor),
                    WwiseBankParsed.EventAction evtAct => new WEventAction(evtAct, 0, 0, graphEditor),
                    WwiseBankParsed.SoundSFXVoice sfxvoice => new WSoundSFXVoice(sfxvoice, 0, 0, graphEditor),
                    _ => new WGeneric(hircObject, 0, 0, graphEditor)
                });
            }

            CurrentObjects.ReplaceAll(newObjs);
        }

        public void Layout()
        {
            if (CurrentObjects != null && CurrentObjects.Any())
            {
                var wwiseEvents = new Dictionary<uint, List<ExportEntry>>();
                var wwiseStreams = new Dictionary<uint, ExportEntry>();
                foreach (ExportEntry exportEntry in Pcc.Exports)
                {
                    switch (exportEntry.ClassName)
                    {
                        case "WwiseEvent":
                        {
                            uint id = (exportEntry.GetProperty<IntProperty>("Id")?.Value ?? 0).ReinterpretAsUint();
                            wwiseEvents.AddToListAt(id, exportEntry);
                            // Wwise IDs are an FNV-1 hash of the lowercased object name. Also index by that hash so
                            // references still resolve to a name when the export's "Id" property is missing or stale.
                            uint nameHash = SharedMethods.CalculateFNV132Hash(exportEntry.ObjectName.Instanced);
                            if (nameHash != id)
                            {
                                wwiseEvents.AddToListAt(nameHash, exportEntry);
                            }
                            break;
                        }
                        case "WwiseStream":
                        {
                            uint id = (exportEntry.GetProperty<IntProperty>("Id")?.Value ?? 0).ReinterpretAsUint();
                            wwiseStreams.TryAdd(id, exportEntry);
                            wwiseStreams.TryAdd(SharedMethods.CalculateFNV132Hash(exportEntry.ObjectName.Instanced), exportEntry);
                            break;
                        }
                    }
                }
                var referencedExports = new Dictionary<uint, List<WExport>>();
                foreach (var obj in CurrentObjects)
                {
                    graphEditor.AddNode(obj);
                    switch (obj)
                    {
                        case WEvent wEvent:
                        {
                            if (!referencedExports.TryGetValue(wEvent.ID, out List<WExport> wExports))
                            {
                                if (!wwiseEvents.TryGetValue(wEvent.ID, out List<ExportEntry> wwiseEventExports))
                                {
                                    continue;
                                }

                                wExports = new List<WExport>();
                                foreach (var wwiseEventExp in wwiseEventExports)
                                {
                                    var wExp = new WExport(wwiseEventExp, 0, 0, graphEditor);
                                    wExports.Add(wExp);
                                    referencedExports.AddToListAt(wEvent.ID, wExp);
                                    graphEditor.AddNode(wExp);
                                }
                            }
                            obj.Varlinks[0].Links.AddRange(wExports.Select(x => (uint)x.Export.UIndex));
                            break;
                        }
                        case WSoundSFXVoice wSound:
                        {
                            if (!referencedExports.TryGetValue(wSound.SoundSFXVoice.AudioID, out List<WExport> wExports))
                            {
                                if (!wwiseStreams.TryGetValue(wSound.SoundSFXVoice.AudioID, out ExportEntry wwiseSoundExport))
                                {
                                    continue;
                                }

                                wExports = new List<WExport>();
                                var wExp = new WExport(wwiseSoundExport, 0, 0, graphEditor);
                                wExports.Add(wExp);
                                referencedExports.AddToListAt(wSound.SoundSFXVoice.AudioID, wExp);
                                graphEditor.AddNode(wExp);
                            }
                            obj.Varlinks[0].Links.Clear();
                            obj.Varlinks[0].Links.AddRange(wExports.Select(x => (uint)x.Export.UIndex));
                            break;
                        }
                    }
                }
                CurrentObjects.AddRange(referencedExports.Values.SelectMany(vals => vals));

                if (ShowBusRoutingHierarchy)
                {
                    foreach (var obj in CurrentObjects)
                    {
                        if (obj is WExport || !graphEditor.BusRouting.Nodes.TryGetValue(obj.ID, out var nodeInfo) || nodeInfo.Children.Count == 0)
                        {
                            continue;
                        }

                        PPath linkBox = obj.CreateActionLinkBox();
                        linkBox.Brush = new SolidBrush(Color.Black);
                        linkBox.Pickable = false;
                        obj.Outlinks.Add(new WwiseHircObjNode.OutputLink
                        {
                            Desc = "Bus Routing Children",
                            Links = nodeInfo.Children.ToList(),
                            Edges = new List<ActionEdge>(),
                            node = linkBox
                        });
                    }
                }

                foreach (var obj in CurrentObjects)
                {
                    obj.CreateConnections(CurrentObjects);
                }

                foreach (WwiseHircObjNode obj in CurrentObjects)
                {
                    SaveData savedInfo = default;
                    uint id = obj is WExport wExp ? wExp.Export.UIndex.ReinterpretAsUint() : obj.ID;
                    if (SavedPositions.Any())
                    {
                        savedInfo = SavedPositions.FirstOrDefault(p => id == p.ID);
                    }

                    bool hasSavedPosition = savedInfo.ID == id;
                    if (hasSavedPosition)
                    {
                        obj.Layout(savedInfo.X, savedInfo.Y);
                    }
                    else
                    {
                        obj.Layout();
                    }
                }

                foreach (WwiseEdEdge edge in graphEditor.edgeLayer)
                {
                    WwiseGraphEditor.UpdateEdge(edge);
                }
            }
        }

        private void AutoLayout()
        {
            foreach (WwiseHircObjNode obj in CurrentObjects)
            {
                obj.SetOffset(0, 0); //remove existing positioning
            }

            const float HORIZONTAL_SPACING = 40;
            const float VERTICAL_SPACING = 20;
            const float VAR_SPACING = 10;
            var visitedNodes = new HashSet<uint>();
            var eventNodes = CurrentObjects.OfType<WEvent>().ToList();
            WwiseHircObjNode firstNode = eventNodes.FirstOrDefault();
            var varNodeLookup = CurrentObjects.OfType<WExport>().ToDictionary(obj => obj.Export.UIndex);
            var opNodeLookup = CurrentObjects.OfType<WGeneric>().ToDictionary(obj => obj.ID);
            var rootTree = new List<WwiseHircObjNode>();
            //WEvents are natural root nodes. ALmost everything will proceed from one of these
            foreach (WEvent eventNode in eventNodes)
            {
                LayoutTree(eventNode, 5 * VERTICAL_SPACING);
            }

            //Find WGenerics with no inputs. These will not have been reached from an WEvent
            var orphanRoots = CurrentObjects.OfType<WGeneric>().Where(node => node.InputEdges.IsEmpty());
            foreach (WGeneric orphan in orphanRoots)
            {
                if (!visitedNodes.Contains(orphan.ID))
                {
                    LayoutTree(orphan, VERTICAL_SPACING);
                }
            }

            //It's possible that there are groups of otherwise unconnected WGenerics that form cycles.
            //Might be possible to make a better heuristic for choosing a root than sequence order, but this situation is so rare it's not worth the effort
            var cycleNodes = CurrentObjects.OfType<WGeneric>().Where(node => !visitedNodes.Contains(node.ID));
            foreach (WGeneric cycleNode in cycleNodes)
            {
                LayoutTree(cycleNode, VERTICAL_SPACING);
            }

            if (firstNode != null) CurrentObjects.OffsetBy(0, -firstNode.OffsetY);

            foreach (WwiseEdEdge edge in graphEditor.edgeLayer)
                WwiseGraphEditor.UpdateEdge(edge);

            void LayoutTree(WwiseHircObjNode WGeneric, float verticalSpacing)
            {
                if (firstNode == null) firstNode = WGeneric;
                visitedNodes.Add(WGeneric.ID);
                var subTree = LayoutSubTree(WGeneric);
                float width = subTree.BoundingRect().Width + HORIZONTAL_SPACING;
                //ignore nodes that are further to the right than this subtree is wide. This allows tighter spacing
                float dy = rootTree.Where(node => node.GlobalFullBounds.Left < width).BoundingRect().Bottom;
                if (dy > 0) dy += verticalSpacing;
                subTree.OffsetBy(0, dy);
                rootTree.AddRange(subTree);
            }

            List<WwiseHircObjNode> LayoutSubTree(WwiseHircObjNode root)
            {
                var tree = new List<WwiseHircObjNode>();
                var vars = new List<WwiseHircObjNode>();
                foreach (var varLink in root.Varlinks)
                {
                    float dx = varLink.node.GlobalFullBounds.X - WExport.RADIUS;
                    float dy = root.GlobalFullHeight + VAR_SPACING;
                    foreach (uint id in varLink.Links.Where(id => !visitedNodes.Contains(id)))
                    {
                        visitedNodes.Add(id);
                        if (varNodeLookup.TryGetValue((int)id, out WExport WExport))
                        {
                            WExport.OffsetBy(dx, dy);
                            dy += WExport.GlobalFullHeight + VAR_SPACING;
                            vars.Add(WExport);
                        }
                        else if (opNodeLookup.TryGetValue(id, out WGeneric node))
                        {
                            node.OffsetBy(dx, dy);
                            dy += node.GlobalFullHeight + VAR_SPACING;
                            vars.Add(node);
                        }
                    }
                }

                var childTrees = new List<List<WwiseHircObjNode>>();
                var children = root.Outlinks.SelectMany(link => link.Links).Where(id => !visitedNodes.Contains(id));
                foreach (uint id in children)
                {
                    visitedNodes.Add(id);
                    if (opNodeLookup.TryGetValue(id, out WGeneric node))
                    {
                        List<WwiseHircObjNode> subTree = LayoutSubTree(node);
                        childTrees.Add(subTree);
                    }
                }

                if (childTrees.Any())
                {
                    float dx = root.GlobalFullWidth + (HORIZONTAL_SPACING * (1 + childTrees.Count * 0.4f));
                    foreach (List<WwiseHircObjNode> subTree in childTrees)
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
                    float centerOffset = tree.OfType<WGeneric>().BoundingRect().Height / 2 - root.GlobalFullHeight / 2;
                    root.OffsetBy(0, centerOffset);
                    vars.OffsetBy(0, centerOffset);
                }

                tree.AddRange(vars);
                tree.Add(root);
                return tree;
            }
        }

        private void SoundPanel_HIRCObjectSelected(uint id)
        {
            if (CurrentObjects.Where(node => !(node is WExport)).FirstOrDefault(node => node.ID == id) is {} nodeToSelect)
            {
                panToSelection = true;
                SelectedNode = nodeToSelect;
            }
        }

        private bool panToSelection = true;
        protected void Node_MouseDown(object sender, PInputEventArgs e)
        {
            if (sender is WwiseHircObjNode obj)
            {
                obj.posAtDragStart = obj.GlobalFullBounds;
                if (e.Button == System.Windows.Forms.MouseButtons.Right)
                {
                    panToSelection = false;

                    SelectedNode = obj;
                    OpenNodeContextMenu(obj);
                }
                else if (!obj.IsSelected)
                {
                    panToSelection = false;
                    SelectedNode = obj;
                }
            }
        }

        private bool AllowWindowRefocus = true;
        public void OpenNodeContextMenu(WwiseHircObjNode obj)
        {
            if (FindResource("nodeContextMenu") is ContextMenu contextMenu)
            {
                bool showContextMenu = false;
                if (contextMenu.GetChild("openInPackEdMenuItem") is MenuItem openInPackEdMenuItem)
                {
                    if (obj is WExport)
                    {
                        openInPackEdMenuItem.Visibility = Visibility.Visible;
                        showContextMenu = true;
                    }
                    else
                    {
                        openInPackEdMenuItem.Visibility = Visibility.Collapsed;
                    }
                }

                if (showContextMenu)
                {
                    contextMenu.IsOpen = true;
                    graphEditor.DisableDragging();
                }
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

        private void OpenInPackageEditor_Clicked(object sender, RoutedEventArgs e)
        {
            if (SelectedNode is WExport wExport)
            {
                AllowWindowRefocus = false; //prevents flicker effect when windows try to focus and then package editor activates
                var p = new PackageEditorWindow();
                p.Show();
                p.LoadFile(wExport.Export.FileRef.FilePath, wExport.Export.UIndex);
                p.Activate(); //bring to front
            }
        }

        public void RefreshView()
        {
            //saveView(false);
            if (Pcc != null)
            {
                graphEditor.SetPackageNames(GetPackageNames(Pcc));
            }
            LoadBank(CurrentExport, false);
            RefreshKnownEvents();
        }

        public override void HandleUpdate(List<PackageUpdate> updates)
        {
            if (Pcc == null)
            {
                return;
            }

            IEnumerable<PackageUpdate> relevantUpdates = updates.Where(update => update.Change.HasFlag(PackageChange.Export));
            List<int> updatedExports = relevantUpdates.Select(x => x.Index).ToList();
            if (CurrentExport != null && updatedExports.Contains(CurrentExport.UIndex))
            {
                if (CurrentExport.ClassName != "WwiseBank")
                {
                    CurrentExport = null;
                    graphEditor.nodeLayer.RemoveAllChildren();
                    graphEditor.edgeLayer.RemoveAllChildren();
                    CurrentObjects.ClearEx();
                    Properties_InterpreterWPF.UnloadExport();
                }

                RefreshView();
                WwiseBankExports.ReplaceAll(Pcc.Exports.Where(exp => exp.ClassName == "WwiseBank"));
                return;
            }

            bool refreshedBanks = false, refreshedView = false;
            foreach (var uIndex in updatedExports)
            {
                if (Pcc.IsUExport(uIndex))
                {
                    string className = Pcc.GetUExport(uIndex).ClassName;

                    if (!refreshedBanks && className == "WwiseBank")
                    {
                        WwiseBankExports.ReplaceAll(Pcc.Exports.Where(exp => exp.ClassName == "WwiseBank"));
                        refreshedBanks = true;
                    }

                    if (!refreshedView && (className == "WwiseStream" || className == "WwiseEvent"))
                    {
                        RefreshView();
                        refreshedView = true;
                    }

                    if (refreshedView && refreshedBanks)
                    {
                        break;
                    }
                }
            }
        }

        private void WwiseEditorWPF_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(FileQueuedForLoad))
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    //Wait for all children to finish loading
                    LoadFile(FileQueuedForLoad);
                    FileQueuedForLoad = null;

                    if (ExportQueuedForFocusing is null && Pcc.IsUExport(UIndexQueuedForFocusing))
                    {
                        ExportQueuedForFocusing = Pcc.GetUExport(UIndexQueuedForFocusing);
                    }

                    if (WwiseBankExports.Contains(ExportQueuedForFocusing))
                    {
                        CurrentExport = ExportQueuedForFocusing;
                    }
                    ExportQueuedForFocusing = null;

                    Activate();
                }));
            }
        }

        public static readonly string WwiseEditorDataFolder = Path.Combine(AppDirectories.AppDataFolder, "WwiseEditor");
        public static readonly string OptionsPath = Path.Combine(WwiseEditorDataFolder, "WwiseEditorOptions.JSON");
        public static readonly string ME3ViewsPath = Path.Combine(WwiseEditorDataFolder, "ME3Views");
        public static readonly string ME2ViewsPath = Path.Combine(WwiseEditorDataFolder, "ME2Views");
        public static readonly string LE3ViewsPath = Path.Combine(WwiseEditorDataFolder, "LE3Views");
        public static readonly string LE2ViewsPath = Path.Combine(WwiseEditorDataFolder, "LE2Views");

        private void SetupJSON(ExportEntry export)
        {
            string objectName = System.Text.RegularExpressions.Regex.Replace(export.ObjectName.Name, @"[<>:""/\\|?*]", "");

            var bankID = BitConverter.ToUInt32(BitConverter.GetBytes(export.GetProperty<IntProperty>("Id")), 0);
            string viewsPath = export.Game switch
            {
                MEGame.LE2 => LE2ViewsPath,
                MEGame.LE3 => LE3ViewsPath,
                MEGame.ME2 => ME2ViewsPath,
                _ => ME3ViewsPath
            };

            JSONpath = Path.Combine(viewsPath, $"{CurrentFile}.#{export.UIndex}.{bankID:X8}.{objectName}.JSON");
        }

        private void SaveView(bool toFile = true)
        {
            if (CurrentObjects.Count == 0)
                return;
            SavedPositions = new List<SaveData>();
            foreach (WwiseHircObjNode obj in CurrentObjects)
            {
                if (obj.Pickable)
                {
                    SavedPositions.Add(new SaveData
                    {
                        ID = obj is WExport wExp ? wExp.Export.UIndex.ReinterpretAsUint() : obj.ID,
                        X = obj.X + obj.Offset.X,
                        Y = obj.Y + obj.Offset.Y
                    });
                }
            }

            if (toFile)
            {
                string outputFile = JsonConvert.SerializeObject(SavedPositions);
                if (!Directory.Exists(Path.GetDirectoryName(JSONpath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(JSONpath));
                File.WriteAllText(JSONpath, outputFile);
                SavedPositions.Clear();
            }
        }

        private void SaveImage()
        {
            if (CurrentObjects.Count == 0)
                return;
            string objectName = System.Text.RegularExpressions.Regex.Replace(CurrentExport.ObjectName.Instanced, @"[<>:""/\\|?*]", "");
            SaveFileDialog d = new ()
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
                graphEditor.AddBack(p);
                graphEditor.Camera.Visible = false;
                Image image = graphEditor.Root.ToImage();
                graphEditor.Camera.Visible = true;
                image.Save(d.FileName, ImageFormat.Png);
                graphEditor.backLayer.RemoveAllChildren();
                MessageBox.Show(this, "Done.");
            }
        }

        #region Busy

        public override void SetBusy(string text = null)
        {
            Image graphImage = graphEditor.Camera.ToImage((int)graphEditor.Camera.GlobalFullWidth, (int)graphEditor.Camera.GlobalFullHeight, new SolidBrush(GraphEditorBackColor));
            graphImageSub.Source = graphImage.ToBitmapImage();
            graphImageSub.Width = graphGrid.ActualWidth;
            graphImageSub.Height = graphGrid.ActualHeight;
            graphImageSub.Visibility = Visibility.Visible;
            GraphHost.Visibility = Visibility.Collapsed;
            BusyText = text;
            IsBusy = true;
        }

        public override void EndBusy()
        {
            IsBusy = false;
            GraphHost.Visibility = Visibility.Visible;
            graphImageSub.Visibility = Visibility.Collapsed;
        }

        #endregion

        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, $"{CurrentFile} {value}");
        }

        private void WwiseEditorWPF_OnClosing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
            {
                return;
            }
            if (AutoSaveView_MenuItem.IsChecked)
                SaveView();

            Misc.AppSettings.Settings.WwiseGraphEditor_AutoSaveView = AutoSaveView_MenuItem.IsChecked;
            soundPanel.HIRCObjectSelected -= SoundPanel_HIRCObjectSelected;
            soundPanel.Dispose();
            
            foreach (var x in CurrentObjects)
            {
                x.MouseDown -= Node_MouseDown;
                x.Dispose();
            }

            CurrentObjects.Clear();
            graphEditor.Dispose();

            RecentsController?.Dispose();
        }

        public void PropogateRecentsChange(string propogationSource, IEnumerable<RecentsControl.RecentItem> newRecents)
        {
            RecentsController.PropogateRecentsChange(false, newRecents);
        }

        public string Toolname => "WwiseEditor";
    }
}
