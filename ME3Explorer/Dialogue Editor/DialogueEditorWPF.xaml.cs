using Gammtek.Conduit.Extensions.Collections.Generic;
using ME3Explorer;
using ME3Explorer.Packages;
using ME3Explorer.Sequence_Editor;
using ME3Explorer.SharedUI;
using ME3Explorer.SharedUI.PeregrineTreeView;
using ME3Explorer.Unreal;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Nodes;
using ME3Explorer.Dialogue_Editor.BioConversationExtended;
using static ME3Explorer.TlkManagerNS.TLKManagerWPF;
using InterpEditor = ME3Explorer.Matinee.InterpEditor;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ME3Explorer.Dialogue_Editor
{
    /// <summary>
    /// Interaction logic for DialogueEditorWPF.xaml
    /// </summary>
    public partial class DialogueEditorWPF : WPFBase
    {
        #region Declarations
        private struct SaveData
        {
            public int index;
            public float X;
            public float Y;

            public SaveData(int i) : this()
            {
                index = i;
            }
        }

        private readonly ConvGraphEditor graphEditor;
        public ObservableCollectionExtended<IEntry> FFXAnimsets { get; } = new ObservableCollectionExtended<IEntry>();
        public ObservableCollectionExtended<ConversationExtended> Conversations { get; } = new ObservableCollectionExtended<ConversationExtended>();
        public ExportEntry CurrentLoadedExport;
        public ObservableCollectionExtended<SpeakerExtended> SelectedSpeakerList { get; } = new ObservableCollectionExtended<SpeakerExtended>();
        public ObservableCollectionExtended<SpeakerExtended> ListenersList { get; } = new ObservableCollectionExtended<SpeakerExtended>();
        private DialogueNodeExtended _SelectedDialogueNode;
        public DialogueNodeExtended SelectedDialogueNode
        {
            get => _SelectedDialogueNode;
            set => SetProperty(ref _SelectedDialogueNode, value);
        }
        private DialogueNodeExtended MirrorDialogueNode;
        private bool IsLocalUpdate; //Used to prevent uneccessary UI updates.
        //SPEAKERS
        private SpeakerExtended _SelectedSpeaker;
        public SpeakerExtended SelectedSpeaker
        {
            get => _SelectedSpeaker;
            set => SetProperty(ref _SelectedSpeaker, value);
        }
        private Dictionary<string, int> _SelectedStarts = new Dictionary<string, int>();
        public Dictionary<string, int> SelectedStarts
        {
            get => _SelectedStarts;
            set => SetProperty(ref _SelectedStarts, value);
        }
        private int forcedSelectStart = -1;
        private string _SelectedScript = "None";
        public string SelectedScript
        {
            get => _SelectedScript;
            set => SetProperty(ref _SelectedScript, value);
        }
        #region ConvoBox //Conversation Box Links
        private ConversationExtended _SelectedConv;
        public ConversationExtended SelectedConv
        {
            get => _SelectedConv;
            set => SetProperty(ref _SelectedConv, value);
        }
        private string _level;
        public string Level
        {
            get => _level;
            set => SetProperty(ref _level, value);
        }
        private int CurrentUIMode = -1; //Sets which panel is up.
        #endregion ConvoBox//Conversation Box Links

        private BlockingCollection<ConversationExtended> BackQueue = new BlockingCollection<ConversationExtended>();
        private BackgroundWorker BackParser = new BackgroundWorker();
        private bool NoUIRefresh; //stops graph refresh on update.
        // FOR GRAPHING
        public ObservableCollectionExtended<DObj> CurrentObjects { get; } = new ObservableCollectionExtended<DObj>();
        public ObservableCollectionExtended<DObj> SelectedObjects { get; } = new ObservableCollectionExtended<DObj>();
        private readonly List<SaveData> extraSaveData = new List<SaveData>();
        private bool panToSelection = true;
        private string FileQueuedForLoad;
        private ExportEntry ExportQueuedForFocusing;
        public string CurrentFile;
        public string JSONpath;
        private List<SaveData> SavedPositions;

        public static readonly string DialogueEditorDataFolder = Path.Combine(App.AppDataFolder, @"DialogueEditor\");
        public static readonly string OptionsPath = Path.Combine(DialogueEditorDataFolder, "DialogueEditorOptions.JSON");
        public static readonly string ME3ViewsPath = Path.Combine(DialogueEditorDataFolder, @"ME3DialogueViews\");
        public static readonly string ME2ViewsPath = Path.Combine(DialogueEditorDataFolder, @"ME2DialogueViews\");
        public static readonly string ME1ViewsPath = Path.Combine(DialogueEditorDataFolder, @"ME1DialogueViews\");
        internal static string ActorDatabasePath = Path.Combine(App.ExecFolder, "ActorTagdb.json");
        private static bool TagDBLoaded;
        private static Dictionary<string, int> ActorStrRefs;

        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, $"{CurrentFile} {value}");
        }
        private int LayoutMode; //0 = column, 1 = waterfall.
        private int SaveViewMode; //0 = auto save, 1 = manual save, 2 = autogenerate.
        public float StartPoDStarts;
        public float StartPoDiagNodes;
        public float StartPoDReplyNodes;

        public ICommand OpenCommand { get; set; }
        public ICommand SaveCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }
        public ICommand SaveImageCommand { get; set; }
        public ICommand SaveViewCommand { get; set; }
        public ICommand GoToCommand { get; set; }
        public ICommand AutoLayoutCommand { get; set; }
        public ICommand LoadTLKManagerCommand { get; set; }
        public ICommand OpenInCommand { get; set; }
        public ICommand OpenInCommand_Wwbank { get; set; }
        public ICommand OpenInCommand_FFXNS { get; set; }
        public ICommand OpenInCommand_Line { get; set; }
        public ICommand SpeakerMoveUpCommand { get; set; }
        public ICommand SpeakerMoveDownCommand { get; set; }
        public ICommand AddSpeakerCommand { get; set; }
        public ICommand DeleteSpeakerCommand { get; set; }
        public ICommand ChangeNameCommand { get; set; }
        public ICommand ChangeLineSizeCommand { get; set; }
        public ICommand StartUpCommand { get; set; }
        public ICommand StartDownCommand { get; set; }
        public ICommand StartAddCommand { get; set; }
        public ICommand StartDeleteCommand { get; set; }
        public ICommand StartEditCommand { get; set; }
        public ICommand ScriptAddCommand { get; set; }
        public ICommand ScriptDeleteCommand { get; set; }
        public ICommand NodeEditCommand { get; set; }
        public ICommand NodeAddCommand { get; set; }
        public ICommand NodeRemoveCommand { get; set; }
        public ICommand NodeDeleteAllLinksCommand { get; set; }
        public ICommand TestPathsCommand { get; set; }
        public ICommand DefaultColorsCommand { get; set; }
        public ICommand StageDirectionsModCommand { get; set; }
        public ICommand RecenterCommand { get; set; }
        public ICommand UpdateLayoutDefaultsCommand { get; set; }

        private bool HasWwbank(object param)
        {
            return SelectedConv?.WwiseBank != null;
        }
        private bool HasFFXNS(object param)
        {
            return SelectedConv?.NonSpkrFFX != null;
        }
        private bool SpkrCanMoveUp(object param)
        {
            return SelectedSpeaker != null && SelectedSpeaker.SpeakerID > 0;
        }
        private bool SpkrCanMoveDown(object param)
        {
            return SelectedSpeaker != null && SelectedSpeaker.SpeakerID >= 0 && SelectedSpeaker.SpeakerID + 3 < SelectedSpeakerList.Count;
        }
        private bool HasActiveSpkr()
        {
            return Speakers_ListBox.SelectedIndex >= 2;
        }
        private bool LineHasInterpdata(object param)
        {
            return SelectedDialogueNode?.Interpdata != null;
        }
        private bool StartCanMoveUp(object param)
        {
            return SelectedConv != null && Start_ListBox.SelectedIndex > 0;
        }
        private bool StartCanMoveDown(object param)
        {
            return SelectedConv != null && Start_ListBox.SelectedIndex >= 0 && Start_ListBox.SelectedIndex < Start_ListBox.Items.Count - 1;
        }
        private bool StartCanDelete()
        {
            return SelectedConv != null && Start_ListBox.SelectedIndex >= 0 && Start_ListBox.Items.Count > 1;
        }
        private bool ScriptCanDelete()
        {
            return SelectedConv != null && Script_ListBox.SelectedIndex > 0;
        }
        #endregion Declarations

        #region Startup/Exit
        public DialogueEditorWPF()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Dialogue Editor WPF", new WeakReference(this));
            LoadCommands();
            StatusText = "Select package file to load";
            SelectedSpeaker = new SpeakerExtended(-3, "None");


            InitializeComponent();

            LoadRecentList();

            graphEditor = (ConvGraphEditor)GraphHost.Child;
            graphEditor.BackColor = Color.FromArgb(130, 130, 130);
            graphEditor.Camera.MouseDown += backMouseDown_Handler;
            graphEditor.Camera.MouseUp += back_MouseUp;

            this.graphEditor.Click += graphEditor_Click;
            this.graphEditor.DragDrop += DialogueEditor_DragDrop;
            this.graphEditor.DragEnter += DialogueEditor_DragEnter;

            Node_Combo_GUIStyle.ItemsSource = Enums.GetValues<EConvGUIStyles>();
            Node_Combo_ReplyType.ItemsSource = Enums.GetValues<EReplyTypes>();

            if (File.Exists(OptionsPath)) //Handle options
            {
                var options = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(OptionsPath));
                if (options.ContainsKey("LineTextSize"))
                {
                    ChangeLineSize(null);
                }
                if (options.ContainsKey("LineTextColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["LineTextColor"]);
                    DBox.lineColor = c;
                    ClrPcker_Line.SelectedColor = c.ToWPFColor();
                }
                if (options.ContainsKey("ParaIntRColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["ParaIntRColor"]);
                    DObj.paraintColor = c;
                    ClrPcker_ParaInt.SelectedColor = c.ToWPFColor();
                }
                if (options.ContainsKey("RenIntRColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["RenIntRColor"]);
                    DObj.renintColor = c;
                    ClrPcker_RenInt.SelectedColor = c.ToWPFColor();
                }
                if (options.ContainsKey("AgreeRColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["AgreeRColor"]);
                    DObj.agreeColor = c;
                    ClrPcker_Agree.SelectedColor = c.ToWPFColor();
                }
                if (options.ContainsKey("DisagreeRColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["DisagreeRColor"]);
                    DObj.disagreeColor = c;
                    ClrPcker_Disagree.SelectedColor = c.ToWPFColor();
                }
                if (options.ContainsKey("FriendlyRColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["FriendlyRColor"]);
                    DObj.friendlyColor = c;
                    ClrPcker_Friendly.SelectedColor = c.ToWPFColor();
                }
                if (options.ContainsKey("HostileRColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["HostileRColor"]);
                    DObj.hostileColor = c;
                    ClrPcker_Hostile.SelectedColor = c.ToWPFColor();
                }
                if (options.ContainsKey("EntryPenColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["EntryPenColor"]);
                    DObj.entryPenColor = c;
                    ClrPcker_EntryPen.SelectedColor = c.ToWPFColor();
                }
                if (options.ContainsKey("EntryColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["EntryColor"]);
                    DObj.entryColor = c;
                    ClrPcker_Entry.SelectedColor = c.ToWPFColor();
                }
                if (options.ContainsKey("ReplyColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["ReplyColor"]);
                    DObj.replyColor = c;
                    ClrPcker_Reply.SelectedColor = c.ToWPFColor();
                }
                if (options.ContainsKey("ReplyPenColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["ReplyPenColor"]);
                    DObj.replyPenColor = c;
                    ClrPcker_ReplyPen.SelectedColor = c.ToWPFColor();
                }
                if (options.ContainsKey("AutoSaveMode"))
                {
                    int.TryParse(options["AutoSaveMode"].ToString(), out int a);
                    SaveViewMode = a;
                }
                if (options.ContainsKey("LayoutMode"))
                {
                    int.TryParse(options["LayoutMode"].ToString(), out int l);
                    LayoutMode = l;
                }
                if (options.ContainsKey("LinesAtTop"))
                    HideEntryOutput_MenuItem.IsChecked = (bool)options["LinesAtTop"];
                if (options.ContainsKey("OutputNumbers"))
                    ShowLinesOnTop_MenuItem.IsChecked = (bool)options["OutputNumbers"];
            }
            else
            {
                Menu_LineSize_10.IsChecked = true;
                ClrPcker_Line.SelectedColor = DBox.lineColor.ToWPFColor();
                ClrPcker_ParaInt.SelectedColor = DObj.paraintColor.ToWPFColor();
                ClrPcker_RenInt.SelectedColor = DObj.renintColor.ToWPFColor();
                ClrPcker_Agree.SelectedColor = DObj.agreeColor.ToWPFColor();
                ClrPcker_Disagree.SelectedColor = DObj.disagreeColor.ToWPFColor();
                ClrPcker_Friendly.SelectedColor = DObj.friendlyColor.ToWPFColor();
                ClrPcker_Hostile.SelectedColor = DObj.hostileColor.ToWPFColor();
                ClrPcker_Entry.SelectedColor = DObj.entryColor.ToWPFColor();
                ClrPcker_EntryPen.SelectedColor = DObj.entryPenColor.ToWPFColor();
                ClrPcker_Reply.SelectedColor = DObj.replyColor.ToWPFColor();
                ClrPcker_ReplyPen.SelectedColor = DObj.replyPenColor.ToWPFColor();
            }
            UpdateLayoutDefaults("startup");
        }
        public DialogueEditorWPF(ExportEntry export) : this()
        {
            FileQueuedForLoad = export.FileRef.FilePath;
            ExportQueuedForFocusing = export;
        }
        private void LoadCommands()
        {
            OpenCommand = new GenericCommand(OpenPackage);
            SaveCommand = new GenericCommand(SavePackage, PackageIsLoaded);
            SaveAsCommand = new GenericCommand(SavePackageAs, PackageIsLoaded);
            SaveViewCommand = new GenericCommand(() => saveView(), CurrentObjects.Any);
            SaveImageCommand = new GenericCommand(SaveImage, CurrentObjects.Any);
            AutoLayoutCommand = new GenericCommand(AutoLayout, CurrentObjects.Any);
            GoToCommand = new GenericCommand(GoToBoxOpen);
            LoadTLKManagerCommand = new GenericCommand(LoadTLKManager);
            OpenInCommand = new RelayCommand(OpenInAction);
            OpenInCommand_FFXNS = new RelayCommand(OpenInAction, HasFFXNS);
            OpenInCommand_Wwbank = new RelayCommand(OpenInAction, HasWwbank);
            OpenInCommand_Line = new RelayCommand(OpenInAction, LineHasInterpdata);
            SpeakerMoveUpCommand = new RelayCommand(SpeakerMoveAction, SpkrCanMoveUp);
            SpeakerMoveDownCommand = new RelayCommand(SpeakerMoveAction, SpkrCanMoveDown);
            AddSpeakerCommand = new GenericCommand(SpeakerAdd);
            DeleteSpeakerCommand = new GenericCommand(SpeakerDelete, HasActiveSpkr);
            ChangeNameCommand = new GenericCommand(SpeakerGoToName, HasActiveSpkr);
            ChangeLineSizeCommand = new RelayCommand(ChangeLineSize);
            StartUpCommand = new RelayCommand(StartMoveAction, StartCanMoveUp);
            StartDownCommand = new RelayCommand(StartMoveAction, StartCanMoveDown);
            StartAddCommand = new RelayCommand(StartAddEdit);
            StartDeleteCommand = new GenericCommand(StartDelete, StartCanDelete);
            StartEditCommand = new RelayCommand(StartAddEdit);
            ScriptAddCommand = new GenericCommand(Script_Add);
            ScriptDeleteCommand = new GenericCommand(Script_Delete, ScriptCanDelete);
            NodeEditCommand = new RelayCommand(DialogueNode_OpenLinkEditor);
            NodeAddCommand = new RelayCommand(DialogueNode_Add);
            NodeRemoveCommand = new RelayCommand(DialogueNode_Delete);
            NodeDeleteAllLinksCommand = new RelayCommand(DialogueNode_DeleteLinks);
            StageDirectionsModCommand = new RelayCommand(StageDirections_Modify);
            TestPathsCommand = new GenericCommand(TestPaths);
            DefaultColorsCommand = new GenericCommand(ResetColorsToDefault);
            RecenterCommand = new GenericCommand(graphEditor_PanTo);
            UpdateLayoutDefaultsCommand = new RelayCommand(UpdateLayoutDefaults);
        }
        private void DialogueEditorWPF_Loaded(object sender, RoutedEventArgs e)
        {
            if (FileQueuedForLoad != null)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    //Wait for all children to finish loading
                    LoadFile(FileQueuedForLoad);
                    FileQueuedForLoad = null;

                    if (ExportQueuedForFocusing != null && ExportQueuedForFocusing.ClassName == "BioConversation")
                    {
                        Conversations_ListBox.SelectedItem = Conversations.FirstOrDefault(x => x.Export.UIndex == ExportQueuedForFocusing.UIndex);
                        SetUIMode(0, true);
                        ExportQueuedForFocusing = null;
                    }

                    Activate();
                }));
            }
        }
        private void SavePackageAs()
        {
            string extension = Path.GetExtension(Pcc.FilePath);
            SaveFileDialog d = new SaveFileDialog { Filter = $"*{extension}|*{extension}" };
            if (d.ShowDialog() == true)
            {
                Pcc.save(d.FileName);
                MessageBox.Show("Done.");
            }
        }
        private void SavePackage()
        {
            Pcc.save();
        }
        private void DialogueEditorWPF_Closing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
                return;

            if (AutoSaveView_MenuItem.IsChecked)
                saveView();

            var options = new Dictionary<string, object>
            {
                {"LineTextSize", DBox.LineScaleOption},
                {"LineTextColor", ColorTranslator.ToHtml(DBox.lineColor)},
                {"ParaIntRColor", ColorTranslator.ToHtml(DObj.paraintColor)},
                {"RenIntRColor", ColorTranslator.ToHtml(DObj.renintColor)},
                {"AgreeRColor", ColorTranslator.ToHtml(DObj.agreeColor)},
                {"DisagreeRColor", ColorTranslator.ToHtml(DObj.disagreeColor)},
                {"FriendlyRColor", ColorTranslator.ToHtml(DObj.friendlyColor)},
                {"HostileRColor", ColorTranslator.ToHtml(DObj.hostileColor)},
                {"EntryColor", ColorTranslator.ToHtml(DObj.entryColor)},
                {"ReplyColor", ColorTranslator.ToHtml(DObj.replyColor)},
                {"EntryPenColor", ColorTranslator.ToHtml(DObj.entryPenColor)},
                {"ReplyPenColor", ColorTranslator.ToHtml(DObj.replyPenColor)},
                {"LinesAtTop", DBox.LinesAtTop},
                {"OutputNumbers", DObj.OutputNumbers},
                {"AutoSaveMode", SaveViewMode},
                {"LayoutMode", LayoutMode},
            };
            string outputFile = JsonConvert.SerializeObject(options);
            if (!Directory.Exists(DialogueEditorDataFolder))
                Directory.CreateDirectory(DialogueEditorDataFolder);
            File.WriteAllText(OptionsPath, outputFile);

            //Code here remove these objects from leaking the window memory
            graphEditor.Camera.MouseDown -= backMouseDown_Handler;
            graphEditor.Camera.MouseUp -= back_MouseUp;
            graphEditor.Click -= graphEditor_Click;
            graphEditor.DragDrop -= DialogueEditor_DragDrop;
            graphEditor.DragEnter -= DialogueEditor_DragEnter;
            CurrentObjects.ForEach(x =>
            {
                x.MouseDown -= node_MouseDown;
                x.Click -= node_Click;
                x.Dispose();
            });
            CurrentObjects.Clear();
            graphEditor.Dispose();
            Properties_InterpreterWPF.Dispose();
            SoundpanelWPF_F.Dispose();
            SoundpanelWPF_M.Dispose();
            FaceFXAnimSetEditorControl_F.Dispose();
            FaceFXAnimSetEditorControl_M.Dispose();
            GraphHost.Child = null; //This seems to be required to clear OnChildGotFocus handler from WinFormsHost
            GraphHost.Dispose();
            DataContext = null;
            DispatcherHelper.EmptyQueue();
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
                    MessageBox.Show("Unable to open file:\n" + ex.Message);
                }
            }
        }
        private bool PackageIsLoaded()
        {
            //System.Diagnostics.Debug.WriteLine("Package Is Loaded.");
            return Pcc != null;
        }
        public void LoadFile(string fileName)
        {
            try
            {
                Conversations.ClearEx();
                SelectedSpeakerList.ClearEx();
                SelectedObjects.ClearEx();
                SelectedDialogueNode = null;
                SelectedConv = null;

                LoadMEPackage(fileName);
                CurrentFile = Path.GetFileName(fileName);
                LoadConversations();
                if (Conversations.IsEmpty())
                {
                    UnloadFile();
                    MessageBox.Show("This file does not contain any Conversations!");
                    return;
                }
                FirstParse();
                RightBarColumn.Width = new GridLength(260);
                graphEditor.nodeLayer.RemoveAllChildren();
                graphEditor.edgeLayer.RemoveAllChildren();

                AddRecent(fileName, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);

                Title = $"Dialogue Editor WPF - {fileName}";
                StatusText = null;

                Level = Path.GetFileName(Pcc.FilePath);
                if (Pcc.Game == MEGame.ME1)
                {
                    Level = $"{Level.Remove(Level.Length - 4)}_LOC_INT{Path.GetExtension(Pcc.FilePath)}";
                }
                else
                {
                    Level = $"{Level.Remove(Level.Length - 12)}.pcc";
                }

                //Build Animset list
                FFXAnimsets.ClearEx();
                foreach (var exp in Pcc.Exports.Where(exp => exp.ClassName == "FaceFXAnimSet"))
                {
                    FFXAnimsets.Add(exp);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message);
                Title = "Dialogue Editor WPF";
                UnloadFile();
            }
        }
        private void UnloadFile()
        {

            RightBarColumn.Width = new GridLength(0);
            SelectedConv = null;
            CurrentLoadedExport = null;
            Conversations.ClearEx();
            SelectedSpeakerList.ClearEx();
            Properties_InterpreterWPF.UnloadExport();
            SoundpanelWPF_F.UnloadExport();
            SoundpanelWPF_M.UnloadExport();
            FaceFXAnimSetEditorControl_F.UnloadExport();
            FaceFXAnimSetEditorControl_M.UnloadExport();
            CurrentObjects.Clear();
            graphEditor.nodeLayer.RemoveAllChildren();
            graphEditor.edgeLayer.RemoveAllChildren();
            CurrentFile = null;
            UnLoadMEPackage();
            StatusText = "Select a package file to load";
        }
        #endregion Startup/Exit

        #region Parsing
        private void LoadConversations()
        {
            Conversations.ClearEx();
            foreach (var exp in Pcc.Exports.Where(exp => exp.ClassName.Equals("BioConversation")))
            {
                Conversations.Add(new ConversationExtended(exp.UIndex, exp.ObjectName, exp.GetProperties(), exp, new ObservableCollectionExtended<SpeakerExtended>(), new ObservableCollectionExtended<DialogueNodeExtended>(), new ObservableCollectionExtended<DialogueNodeExtended>(), new ObservableCollectionExtended<StageDirection>()));
            }
        }
        private async void FirstParse()
        {
            BackQueue = new BlockingCollection<ConversationExtended>();
            BackParser = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            BackParser.DoWork += BackParse;
            BackParser.RunWorkerCompleted += BackParser_RunWorkerCompleted;
            BackParser.RunWorkerAsync();

            if(!App.TlkFirstLoadDone)
            {
                bool waitingfortlks = true;
                while(waitingfortlks)
                {
                    waitingfortlks = await CheckProcess(100, App.TlkFirstLoadDone, true);
                }
            }

            if (SelectedConv != null && SelectedConv.IsFirstParsed == false) //Get Active setup pronto.
            {
                ParseStartingList(SelectedConv);
                ParseSpeakers(SelectedConv);
                GenerateSpeakerList();
                ParseEntryList(SelectedConv);
                ParseReplyList(SelectedConv);
                ParseScripts(SelectedConv);
                ParseNSFFX(SelectedConv);
                ParseSequence(SelectedConv);
                ParseWwiseBank(SelectedConv);
                ParseStageDirections(SelectedConv);

                SelectedConv.IsFirstParsed = true;
                DetailParse(SelectedConv);
            }

            foreach (var conv in Conversations.Where(conv => conv.IsFirstParsed == false)) //Get Speakers entry and replies plus convo data first
            {
                ParseStartingList(conv);
                ParseSpeakers(conv);
                ParseEntryList(conv);
                ParseReplyList(conv);
                ParseScripts(conv);
                ParseNSFFX(conv);
                ParseSequence(conv);
                ParseWwiseBank(conv);
                ParseStageDirections(conv);
                conv.IsFirstParsed = true;

                if(!conv.IsParsed)
                    BackQueue.Add(conv);
            }
#if DEBUG
            Debug.WriteLine("FirstParse Done");
#endif
            BackQueue.CompleteAdding();
        }
        private void BackParse(object sender, DoWorkEventArgs e)
        {
#if DEBUG
            Debug.WriteLine("BackParse Starting");
#endif
            //Do minor stuff
            foreach (var conv in BackQueue.GetConsumingEnumerable(CancellationToken.None))
            {
                DetailParse(conv);
            }

        }
        private void BackParser_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {

            BackParser.CancelAsync();
#if DEBUG
            Debug.WriteLine("BackParse Done");
#endif
        }
        private void DetailParse(ConversationExtended conv)
        {
            
            foreach (var spkr in conv.Speakers)
            {
                spkr.FaceFX_Male = GetFaceFX(conv, spkr.SpeakerID, true);
                spkr.FaceFX_Female = GetFaceFX(conv, spkr.SpeakerID, false);
            }
            GenerateSpeakerTags(conv);
            ParseLinesInterpData(conv);
            ParseLinesFaceFX(conv);
            ParseLinesAudioStreams(conv);
            ParseLinesScripts(conv);

            conv.IsParsed = true;
        }
        private void ParseSpeakers(ConversationExtended conv)
        {
            conv.Speakers = new ObservableCollectionExtended<SpeakerExtended>
            {
                new SpeakerExtended(-2, "player", null, null, 125303, "\"Shepard\""),
                new SpeakerExtended(-1, "owner", null, null, 0, "No data")
            };
            try
            {
                if (Pcc.Game != MEGame.ME3)
                {
                    var s_speakers = conv.BioConvo.GetProp<ArrayProperty<StructProperty>>("m_SpeakerList");
                    if (s_speakers != null)
                    {
                        for (int id = 0; id < s_speakers.Count; id++)
                        {
                            var spkr = new SpeakerExtended(id, s_speakers[id].GetProp<NameProperty>("sSpeakerTag").ToString());
                            conv.Speakers.Add(spkr);
                        }
                    }
                }
                else
                {
                    var a_speakers = conv.BioConvo.GetProp<ArrayProperty<NameProperty>>("m_aSpeakerList");
                    if (a_speakers != null)
                    {
                        int id = 0;
                        foreach (NameProperty n in a_speakers)
                        {
                            var spkr = new SpeakerExtended(id, n.ToString());
                            conv.Speakers.Add(spkr);
                            id++;
                        }
                    }
                }
            }
            catch (Exception e)
            {
#if DEBUG
                throw new Exception("Starting List Parse failed", e);
#endif
            }
        }
        private void ParseEntryList(ConversationExtended conv)
        {
            conv.EntryList = new ObservableCollectionExtended<DialogueNodeExtended>();
            var entryprop = conv.BioConvo.GetProp<ArrayProperty<StructProperty>>("m_EntryList");
            int cnt = 0;

            foreach (StructProperty Node in entryprop)
            {
                int speakerindex = -1;
                int linestrref = 0;
                int cond = -1;
                string line = "Unknown Reference";
                int stevent = -1;
                try
                {

                    speakerindex = Node.GetProp<IntProperty>("nSpeakerIndex");
                    var linestrrefprop = Node.GetProp<StringRefProperty>("srText");
                    if(linestrrefprop != null)
                    {
                        linestrref = linestrrefprop.Value;
                        line = GlobalFindStrRefbyID(linestrref, Pcc);
                    }
                    
                    var condprop = Node.GetProp<IntProperty>("nConditionalFunc");
                    if (condprop != null)
                    {
                        cond = condprop.Value;
                    }
                    
                    var steventprop = Node.GetProp<IntProperty>("nStateTransition");
                    if (steventprop != null)
                    {
                        stevent = steventprop.Value;
                    }
                    bool bcond = Node.GetProp<BoolProperty>("bFireConditional");
                    conv.EntryList.Add(new DialogueNodeExtended(Node, false, cnt, speakerindex, linestrref, line, bcond, cond, stevent, EReplyTypes.REPLY_STANDARD));
                    cnt++;

                }
                catch (Exception e)
                {
#if DEBUG
                    throw new Exception($"Entry List Parse failed {conv.ConvName}:E{cnt} {speakerindex}, {linestrref}, {line}, {cond}, {stevent}", e);
#endif
                }
            }
        }
        private void ParseReplyList(ConversationExtended conv)
        {
            conv.ReplyList = new ObservableCollectionExtended<DialogueNodeExtended>();
            var replyprop = conv.BioConvo.GetProp<ArrayProperty<StructProperty>>("m_ReplyList"); //ME3
            if (replyprop != null)
            {
                int cnt = 0;
                foreach (StructProperty Node in replyprop)
                {
                    int linestrref = 0;
                    int cond = -1;
                    string line = "Unknown Reference";
                    int stevent = -1;
                    bool bcond = false;
                    EReplyTypes eReply = EReplyTypes.REPLY_STANDARD;
                    try
                    {
                        linestrref = Node.GetProp<StringRefProperty>("srText").Value;
                        line = GlobalFindStrRefbyID(linestrref, Pcc);
                        cond = Node.GetProp<IntProperty>("nConditionalFunc").Value;
                        stevent = Node.GetProp<IntProperty>("nStateTransition").Value;
                        bcond = Node.GetProp<BoolProperty>("bFireConditional");
                        Enum.TryParse(Node.GetProp<EnumProperty>("ReplyType").Value.Name, out eReply);
                        conv.ReplyList.Add(new DialogueNodeExtended(Node, true, cnt, -2, linestrref, line, bcond, cond, stevent, eReply));
                        cnt++;
                    }
                    catch (Exception e)
                    {
#if DEBUG
                        throw new Exception($"Reply List Parse failed {conv.ConvName}:R{cnt} Player, {linestrref}, {line}, {cond}, {stevent}, {bcond.ToString()}, {eReply.ToString()}", e);  //Note some convos don't have replies.
#endif
                    }
                }
            }
        }
        private void ParseScripts(ConversationExtended conv)
        {
            conv.ScriptList.Add("None");
            if (Pcc.Game == MEGame.ME3)
            {
                var a_scripts = conv.BioConvo.GetProp<ArrayProperty<NameProperty>>("m_aScriptList");
                if (a_scripts != null)
                {
                    foreach (var scriptprop in a_scripts)
                    {
                        var scriptname = scriptprop.Value;
                        conv.ScriptList.Add(scriptname);
                    }
                }
            }
            else
            {
                var a_sscripts = conv.BioConvo.GetProp<ArrayProperty<StructProperty>>("m_ScriptList");
                if (a_sscripts != null)
                {
                    foreach (var scriptprop in a_sscripts)
                    {
                        var s = scriptprop.GetProp<NameProperty>("sScriptTag");
                        conv.ScriptList.Add(s.Value);
                    }
                }
            }
        }
        private void ParseStageDirections(ConversationExtended conv)
        {
            if (Pcc.Game == MEGame.ME3)
            {
                var dprop = conv.BioConvo.GetProp<ArrayProperty<StructProperty>>("m_aStageDirections"); //ME3 Only not in ME1/2
                if (dprop != null)
                {
                    foreach (var direction in dprop)
                    {
                        int strref = 0;
                        string line = "No data";
                        string action = "None";
                        try
                        {
                            var strrefprop = direction.GetProp<StringRefProperty>("srStrRef");
                            if (strrefprop != null)
                            {
                                strref = strrefprop.Value;
                                line = GlobalFindStrRefbyID(strref, Pcc);
                            }
                            var actionprop = direction.GetProp<StrProperty>("sText");
                            if (actionprop != null)
                            {
                                action = actionprop.Value;
                            }
                            conv.StageDirections.Add(new StageDirection(strref, line, action));
                        }
                        catch (Exception e)
                        {
#if DEBUG
                            throw new Exception($"stage directions parse failed {conv.ConvName}", e);
#endif
                        }
                    }
                }
            }
        }
        private void GenerateSpeakerList()
        {
            SelectedSpeakerList.ClearEx();

            foreach (var spkr in SelectedConv.Speakers)
            {
                SelectedSpeakerList.Add(spkr);
            }
        }
        private static void GenerateSpeakerTags(ConversationExtended conv)
        {
            foreach (var e in conv.EntryList)
            {
                int spkridx = e.SpeakerIndex;
                var spkrtag = conv.Speakers.FirstOrDefault(s => s.SpeakerID == spkridx);
                if (spkrtag != null)
                    e.SpeakerTag = spkrtag;
            }

            foreach (var r in conv.ReplyList)
            {
                int spkridx = r.SpeakerIndex;
                var spkrtag = conv.Speakers.FirstOrDefault(s => s.SpeakerID == spkridx);
                if (spkrtag != null)
                    r.SpeakerTag = spkrtag;
            }
        }
        /// <summary>
        /// Gets the interpdata for each node in conversation
        /// </summary>
        /// <param name="conv"></param>
        private void ParseLinesInterpData(ConversationExtended conv)
        {
            if (conv.Sequence == null || conv.Sequence.UIndex < 1)
                return;
            //Get sequence from convo
            //Get list of BioConvoStarts
            //Match to export id => SeqAct_Interp => Interpdata
            if (conv.Sequence is ExportEntry sequence)
            {
                var seqobjs = sequence.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");

                var convStarts = new Dictionary<int, ExportEntry>();
                foreach (var prop in seqobjs)
                {
                    var seqobj = Pcc.getUExport(prop.Value);
                    if (seqobj.ClassName == "BioSeqEvt_ConvNode")
                    {
                        int key = seqobj.GetProperty<IntProperty>("m_nNodeID"); //ME3
                        if (!convStarts.ContainsKey(key))
                        {
                            convStarts.Add(key, seqobj);
                        }
                    }
                }

                foreach (var entry in conv.EntryList)
                {
                    try
                    {
                        entry.ExportID = entry.NodeProp.GetProp<IntProperty>("nExportID");
                        if (entry.ExportID != 0)
                        {
                            var convstart = convStarts.Where(s => s.Key == entry.ExportID).FirstOrDefault().Value;
                            if (convstart != null)
                            {
                                var outLinksProp = convstart.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
                                if (outLinksProp != null && outLinksProp.Count > 0)
                                {
                                    var linksProp = outLinksProp[0].GetProp<ArrayProperty<StructProperty>>("Links");
                                    if (linksProp != null)
                                    {
                                        var link = linksProp[0].GetProp<ObjectProperty>("LinkedOp").Value;
                                        var interpseqact = Pcc.getUExport(link);
                                        if (interpseqact.ClassName != "SeqAct_Interp") //Double check egm facefx not in the loop. Go two nodes deeper. "past conditional / BioSeqAct_SetFaceFX"
                                        {
                                            var outLinksProp2 = interpseqact.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
                                            if (outLinksProp2 != null && outLinksProp2.Count > 0)
                                            {
                                                var linksProp2 = outLinksProp2[0].GetProp<ArrayProperty<StructProperty>>("Links");
                                                if (linksProp2 != null && linksProp2.Count > 0)
                                                {
                                                    var link2 = linksProp2[0].GetProp<ObjectProperty>("LinkedOp").Value;
                                                    interpseqact = Pcc.getUExport(link2);
                                                    if (interpseqact.ClassName != "SeqAct_Interp") //Double check egm facefx not in the loop. Go two nodes deeper. "past conditional / BioSeqAct_SetFaceFX"
                                                    {
                                                        var outLinksProp3 = interpseqact.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
                                                        if (outLinksProp3 != null && outLinksProp3.Count > 0)
                                                        {
                                                            var linksProp3 = outLinksProp[0].GetProp<ArrayProperty<StructProperty>>("Links");
                                                            if (linksProp3 != null)
                                                            {
                                                                var link3 = linksProp3[0].GetProp<ObjectProperty>("LinkedOp").Value;
                                                                interpseqact = Pcc.getUExport(link3);
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        var varLinksProp = interpseqact.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
                                        if (varLinksProp != null)
                                        {
                                            foreach (var prop in varLinksProp)
                                            {
                                                var desc = prop.GetProp<StrProperty>("LinkDesc").Value; //ME3/ME2/ME1
                                                if (desc == "Data") //ME3/ME1
                                                {
                                                    var linkedVars = prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables");
                                                    if (linkedVars != null && linkedVars.Count > 0)
                                                    {
                                                        var datalink = linkedVars[0].Value;
                                                        entry.Interpdata = Pcc.getUExport(datalink);

                                                    }

                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
#if DEBUG
                        throw new Exception($"EntryList parse interpdata failed: {entry.NodeCount}", e);
#endif
                    }
                }

                foreach (var reply in conv.ReplyList)
                {
                    try
                    {
                        reply.ExportID = reply.NodeProp.GetProp<IntProperty>("nExportID");
                        if (reply.ExportID != 0)
                        {
                            var convstart = convStarts.Where(s => s.Key == reply.ExportID).FirstOrDefault().Value;
                            if (convstart != null)
                            {
                                var outLinksProp = convstart.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
                                if (outLinksProp != null && outLinksProp.Count > 0)
                                {
                                    var linksProp = outLinksProp[0].GetProp<ArrayProperty<StructProperty>>("Links");
                                    if (linksProp != null && linksProp.Count > 0)
                                    {
                                        var link = linksProp[0].GetProp<ObjectProperty>("LinkedOp").Value;
                                        var interpseqact = Pcc.getUExport(link);
                                        if (interpseqact.ClassName != "SeqAct_Interp") //Double check egm facefx not in the loop. Go two nodes deeper. "past conditional / SFXSeqAct_SetFaceFX"
                                        {
                                            var outLinksProp2 = interpseqact.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
                                            if (outLinksProp2 != null && outLinksProp2.Count > 0)
                                            {
                                                var linksProp2 = outLinksProp2[0].GetProp<ArrayProperty<StructProperty>>("Links");
                                                if (linksProp2 != null && linksProp2.Count > 0)
                                                {
                                                    var link2 = linksProp2[0].GetProp<ObjectProperty>("LinkedOp").Value;
                                                    interpseqact = Pcc.getUExport(link2);
                                                    if (interpseqact.ClassName != "SeqAct_Interp") //Double check egm facefx not in the loop. Go two nodes deeper. "past conditional / SFXSeqAct_SetFaceFX"
                                                    {
                                                        var outLinksProp3 = interpseqact.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
                                                        if (outLinksProp3 != null && outLinksProp3.Count > 0)
                                                        {
                                                            var linksProp3 = outLinksProp3[0].GetProp<ArrayProperty<StructProperty>>("Links");
                                                            if (linksProp3 != null && linksProp3.Count > 0)
                                                            {
                                                                var link3 = linksProp3[0].GetProp<ObjectProperty>("LinkedOp").Value;
                                                                interpseqact = Pcc.getUExport(link3);
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        var varLinksProp = interpseqact.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
                                        if (varLinksProp != null)
                                        {
                                            foreach (var prop in varLinksProp)
                                            {
                                                var desc = prop.GetProp<StrProperty>("LinkDesc").Value;
                                                if (desc == "Data")
                                                {
                                                    var linkedVars = prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables");
                                                    if (linkedVars != null && linkedVars.Count > 0)
                                                    {
                                                        var datalink = linkedVars[0].Value;
                                                        reply.Interpdata = Pcc.getUExport(datalink);
                                                    }

                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"ReplyList parse interpdata failed: {reply.NodeCount}", e);
                    }
                }
            }
        }
        /// <summary>
        /// Parses for male and female wwisestream IEntry for every line in the conversation.
        /// </summary>
        private void ParseLinesAudioStreams(ConversationExtended conv)
        {
            try
            {

                if (Pcc.Game != MEGame.ME1)
                {
                    Dictionary<string, ExportEntry> streams = Pcc.Exports.Where(x => x.ClassName == "WwiseStream").ToDictionary(x => $"{x.ObjectName.ToLower()}_{x.UIndex}");

                    foreach (var node in conv.EntryList)
                    {
                        string srchFem = $"{node.LineStrRef}_f";
                        string srchM = $"{node.LineStrRef}_m";
                        node.WwiseStream_Female = streams.FirstOrDefault(s => s.Key.Contains(srchFem)).Value;
                        node.WwiseStream_Male = streams.FirstOrDefault(s => s.Key.Contains(srchM)).Value;
                    }

                    foreach (var node in conv.ReplyList)
                    {
                        string srchFem = $"{node.LineStrRef}_f";
                        string srchM = $"{node.LineStrRef}_m";
                        node.WwiseStream_Female = streams.FirstOrDefault(s => s.Key.Contains(srchFem)).Value;
                        node.WwiseStream_Male = streams.FirstOrDefault(s => s.Key.Contains(srchM)).Value;
                    }
                }
            }
            catch (Exception e)
            {
#if DEBUG
                throw new Exception("Failure to parse wwisestreams for lines", e);
#endif
            }
        }
        private static void ParseLinesScripts(ConversationExtended conv)
        {
            if (conv.IsFirstParsed)
            {
                try
                {
                    foreach (var entry in conv.EntryList)
                    {
                        var scriptidx = entry.NodeProp.GetProp<IntProperty>("nScriptIndex");
                        entry.Script = conv.ScriptList[scriptidx + 1];
                    }
                    foreach (var reply in conv.ReplyList)
                    {
                        var scriptidx = reply.NodeProp.GetProp<IntProperty>("nScriptIndex");
                        reply.Script = conv.ScriptList[scriptidx + 1];
                    }
                }
                catch (Exception e)
                {
#if DEBUG
                    throw new Exception("Parse failure on script list", e);
#endif
                }
            }
        }
        private void ParseNodeData(DialogueNodeExtended node)
        {
            try
            {
                var nodeprop = node.NodeProp;
                node.Listener = nodeprop.GetProp<IntProperty>("nListenerIndex");  //ME3//ME2//ME1
                node.IsDefaultAction = false;
                node.IsMajorDecision = false;
                if (node.IsReply)
                {
                    node.IsSkippable = false; //ME3/
                    node.IsUnskippable = nodeprop.GetProp<BoolProperty>("bUnskippable");

                }
                else
                {
                    node.IsSkippable = nodeprop.GetProp<BoolProperty>("bSkippable"); //ME3/
                    node.IsUnskippable = false;

                }
                node.ConditionalParam = nodeprop.GetProp<IntProperty>("nConditionalParam");
                node.TransitionParam = nodeprop.GetProp<IntProperty>("nStateTransitionParam");
                node.CameraIntimacy = nodeprop.GetProp<IntProperty>("nCameraIntimacy");
                node.IsAmbient = nodeprop.GetProp<BoolProperty>("bAmbient");
                node.IsNonTextLine = nodeprop.GetProp<BoolProperty>("bNonTextLine");
                node.IgnoreBodyGesture = nodeprop.GetProp<BoolProperty>("bIgnoreBodyGestures");
                node.GUIStyle = Enums.Parse<EConvGUIStyles>(nodeprop.GetProp<EnumProperty>("eGUIStyle").Value.Name);
                if (Pcc.Game == MEGame.ME3)
                {
                    node.HideSubtitle = nodeprop.GetProp<BoolProperty>("bAlwaysHideSubtitle");
                    if (node.IsReply)
                    {
                        node.IsDefaultAction = nodeprop.GetProp<BoolProperty>("bIsDefaultAction");
                        node.IsMajorDecision = nodeprop.GetProp<BoolProperty>("bIsMajorDecision");
                    }
                }

                var lengthprop = node.Interpdata?.GetProperty<FloatProperty>("InterpLength");
                if (lengthprop != null)
                {
                    node.InterpLength = lengthprop.Value;
                }
            }
            catch (Exception e)
            {
#if DEBUG
                throw new Exception("DiagNodeParse Failed.", e);
#endif
            }
        }
        private static void ParseLinesFaceFX(ConversationExtended conv)
        {
            foreach (var entry in conv.EntryList)
            {
                if (entry.Line != "No data" && !string.IsNullOrWhiteSpace(entry.Line))
                {
                    entry.FaceFX_Female = $"FXA_{entry.LineStrRef}_F";
                    entry.FaceFX_Male = $"FXA_{entry.LineStrRef}_M";
                }
                else
                {
                    entry.FaceFX_Female = "None";
                    entry.FaceFX_Male = "None";
                }
            }

            foreach (var reply in conv.ReplyList)
            {
                if (reply.Line != "No data" && !string.IsNullOrWhiteSpace(reply.Line))
                {
                    reply.FaceFX_Female = $"FXA_{reply.LineStrRef}_F";
                    reply.FaceFX_Male = $"FXA_{reply.LineStrRef}_M";
                }
                else
                {
                    reply.FaceFX_Female = "None";
                    reply.FaceFX_Male = "None";
                }
            }
        }

        /// <summary>
        /// Returns the IEntry of FaceFXAnimSet
        /// </summary>
        /// <param name="conv"></param>
        /// <param name="speakerID">SpeakerID: -1 = Owner, -2 = Player</param>
        /// <param name="isMale">will pull female by default</param>
        public IEntry GetFaceFX(ConversationExtended conv, int speakerID, bool isMale = false)
        {
            string ffxPropName = "m_aFemaleFaceSets"; //ME2/M£3
            if (isMale)
            {
                ffxPropName = "m_aMaleFaceSets";
            }
            var ffxList = conv.BioConvo.GetProp<ArrayProperty<ObjectProperty>>(ffxPropName);
            if (ffxList != null && ffxList.Count > speakerID + 2)
            {
                return Pcc.getEntry(ffxList[speakerID + 2].Value);
            }

            return null;
        }
        /// <summary>
        /// Sets the IEntry of appropriate sequence
        /// </summary>
        public void ParseSequence(ConversationExtended conv)
        {
            string propname = "MatineeSequence";
            if (Pcc.Game == MEGame.ME1)
            {
                propname = "m_pEvtSystemSeq";
            }

            var seq = conv.BioConvo.GetProp<ObjectProperty>(propname);
            if (seq != null)
            {
                conv.Sequence = Pcc.getEntry(seq.Value);
            }
            else
            {
                conv.Sequence = null;
            }
        }
        /// <summary>
        /// Sets the IEntry of NonSpeaker FaceFX
        /// </summary>
        public void ParseNSFFX(ConversationExtended conv)
        {
            string propname = "m_pNonSpeakerFaceFXSet";
            if (Pcc.Game == MEGame.ME1)
            {
                propname = "m_pConvFaceFXSet";
            }

            var seq = conv.BioConvo.GetProp<ObjectProperty>(propname);
            if (seq != null)
            {
                conv.NonSpkrFFX = Pcc.getEntry(seq.Value);
            }
            else
            {
                conv.NonSpkrFFX = null;
            }
        }
        /// <summary>
        /// Sets the Uindex of WwiseBank
        /// </summary>
        public void ParseWwiseBank(ConversationExtended conv)
        {
            conv.WwiseBank = null;
            if(Pcc.Game != MEGame.ME1)
            {
                try
                {
                    ArrayProperty<ObjectProperty> wwevents;
                    IEntry ffxo = GetFaceFX(conv, -1, true); //find owner animset

                    if (ffxo == null) //if no facefx then maybe soundobject conversation
                    {
                        wwevents = conv.Export.GetProperty<ArrayProperty<ObjectProperty>>("m_aMaleSoundObjects");

                    }
                    else
                    {
                        ExportEntry ffxoExport = (ExportEntry)ffxo;

                        wwevents = ffxoExport.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedSoundCues"); //pull an owner wwiseevent array
                        if (wwevents == null || wwevents.Count == 0 || wwevents[0].Value == 0)
                        {
                            IEntry ffxp = GetFaceFX(conv, -2, true); //find player as alternative
                            if (!Pcc.isUExport(ffxp.UIndex))
                                return;
                            ExportEntry ffxpExport = (ExportEntry)ffxp;
                            wwevents = ffxpExport.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedSoundCues");
                        }
                        if (wwevents == null || wwevents.Count == 0 || wwevents[0].Value == 0)
                        {
                            IEntry ffxS = GetFaceFX(conv, 0, true); //find speaker 1 as alternative
                            if (ffxS == null || !Pcc.isUExport(ffxS.UIndex))
                                return;
                            ExportEntry ffxSExport = (ExportEntry)ffxS;
                            wwevents = ffxSExport.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedSoundCues");
                        }
                    }

                    if (wwevents == null || wwevents.Count == 0 || wwevents[0].Value == 0)
                    {
                        conv.WwiseBank = null;
                        return;
                    }

                    if (Pcc.Game == MEGame.ME3)
                    {
                        StructProperty r = Pcc.getUExport(wwevents[0].Value).GetProperty<StructProperty>("Relationships"); //lookup bank
                        var bank = r.GetProp<ObjectProperty>("Bank");
                        conv.WwiseBank = Pcc.getUExport(bank.Value);
                    }
                    else if (Pcc.Game == MEGame.ME2) //Game is ME2.  Wwisebank ref in Binary.
                    {
                        byte[] data = Pcc.getUExport(wwevents[0].Value).getBinaryData();
                        int binarypos = 4;
                        int count = BitConverter.ToInt32(data, binarypos);
                        if (count > 0)
                        {
                            binarypos += 4;
                            int bnkcount = BitConverter.ToInt32(data, binarypos);
                            if (bnkcount > 0)
                            {
                                binarypos += 4;
                                int bank = BitConverter.ToInt32(data, binarypos);
                                conv.WwiseBank = Pcc.getUExport(bank);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
#if DEBUG
                    throw new Exception($"WwiseBank Parse Failed. {conv.ConvName}", e);
#endif
                }
            }
        }
        public int ParseActorsNames(ConversationExtended conv, string tag)
        {
            if (Pcc.Game == MEGame.ME1)
            {
                try
                {
                    var actors = Pcc.Exports.Where(xp => xp.ClassName == "BioPawn");
                    ExportEntry actor = actors.First(a => a.GetProperty<NameProperty>("Tag").ToString() == tag);
                    var behav = actor.GetProperty<ObjectProperty>("m_oBehavior");
                    var set = Pcc.getUExport(behav.Value).GetProperty<ObjectProperty>("m_oActorType");
                    var strrefprop = Pcc.getUExport(set.Value).GetProperty<StringRefProperty>("ActorGameNameStrRef");
                    if (strrefprop != null)
                    {
                        return strrefprop.Value;
                    }
                }
                catch
                {
                    return -2;
                }
            }

            // ME2/ME3 need to load non-LOC file.  Or parse a JSON.

            return 0;
        }
        /// <summary>
        /// Gets dictionary of starting list and position
        /// </summary>
        /// <returns>Key = position on list, Value = Outlink</returns>
        public void ParseStartingList(ConversationExtended conv)
        {
            conv.StartingList = new SortedDictionary<int, int>();
            var prop = conv.Export.GetProperty<ArrayProperty<IntProperty>>("m_StartingList"); //ME1/ME2/ME3
            if (prop != null)
            {
                int pos = 0;
                foreach (var sl in prop)
                {
                    conv.StartingList.Add(pos, sl.Value);
                    pos++;
                }
            }
        }
        #endregion Parsing

        #region RecreateToFile
        public static void PushConvoToFile(ConversationExtended convo)
        {

            convo.Export.WriteProperties(convo.BioConvo);

        }
        public void PushLocalGraphChanges(DiagNode obj)
        {
            IsLocalUpdate = true;
            RecreateNodesToProperties(SelectedConv);

            float newX = obj.X + obj.OffsetX;
            float newY = obj.Y + obj.OffsetY;
            obj.RemoveAllChildren();
            obj.RemoveConnections();
            obj.GetOutputLinks(obj.Node);
            obj.Layout(newX, newY);
            obj.RecreateConnections(CurrentObjects);

            foreach (DiagEdEdge edge in graphEditor.edgeLayer)
            {
                ConvGraphEditor.UpdateEdge(edge);
            }

        }
        private bool AutoGenerateSpeakerArrays(ConversationExtended conv)
        {
            bool hasLoopingPaths = false;

            var blankaSpkr = new ArrayProperty<IntProperty>("aSpeakerList");
            foreach (var dnode in SelectedConv.EntryList)
            {
                dnode.NodeProp.Properties.AddOrReplaceProp(blankaSpkr);
            }

            foreach ((var _, int entryIndex) in conv.StartingList)
            {
                var aSpkrs = new SortedSet<int>();
                var startNode = conv.EntryList[entryIndex];
                var visitedNodes = new HashSet<DialogueNodeExtended>();
                var newNodes = new Queue<DialogueNodeExtended>();
                aSpkrs.Add(startNode.SpeakerIndex);
                var startprop = startNode.NodeProp.GetProp<ArrayProperty<StructProperty>>("ReplyListNew");
                foreach (var e in startprop)
                {
                    var lprop = e.GetProp<IntProperty>("nIndex");
                    newNodes.Enqueue(conv.ReplyList[lprop.Value]);

                }
                visitedNodes.Add(startNode);
                while (newNodes.Any())
                {
                    var thisnode = newNodes.Dequeue();
                    if (!visitedNodes.Contains(thisnode))
                    {
                        if (thisnode.IsReply)
                        {
                            var thisprop = thisnode.NodeProp.GetProp<ArrayProperty<IntProperty>>("EntryList");
                            if (thisprop != null)
                            {
                                foreach (var r in thisprop)
                                {
                                    newNodes.Enqueue(conv.EntryList[r.Value]);
                                }
                            }
                        }
                        else
                        {
                            aSpkrs.Add(thisnode.SpeakerIndex);
                            var thisprop = thisnode.NodeProp.GetProp<ArrayProperty<StructProperty>>("ReplyListNew");
                            foreach (var e in thisprop)
                            {
                                var eprop = e.GetProp<IntProperty>("nIndex");
                                newNodes.Enqueue(conv.ReplyList[eprop.Value]);

                            }
                        }
                        visitedNodes.Add(thisnode);
                    }
                    else { hasLoopingPaths = true; }
                }
                var newaSpkr = new ArrayProperty<IntProperty>("aSpeakerList");
                foreach (var a in aSpkrs)
                {
                    newaSpkr.Add(a);
                }
                startNode.NodeProp.Properties.AddOrReplaceProp(newaSpkr);
            }
            return hasLoopingPaths;
        }
        private void SaveSpeakersToProperties(IEnumerable<SpeakerExtended> speakerCollection)
        {
            try
            {

                var m_aSpeakerList = new ArrayProperty<NameProperty>("m_aSpeakerList");
                var m_SpeakerList = new ArrayProperty<StructProperty>("m_SpeakerList");
                var m_aMaleFaceSets = new ArrayProperty<ObjectProperty>("m_aMaleFaceSets");
                var m_aFemaleFaceSets = new ArrayProperty<ObjectProperty>("m_aFemaleFaceSets");

                foreach (SpeakerExtended spkr in speakerCollection)
                {
                    if (spkr.SpeakerID >= 0)
                    {
                        if (Pcc.Game == MEGame.ME3)
                        {
                            m_aSpeakerList.Add(new NameProperty(spkr.SpeakerName, "m_aSpeakerList"));
                        }
                        else
                        {
                            m_SpeakerList.Add(new StructProperty("BioDialogSpeaker", new PropertyCollection
                            {
                                new NameProperty(spkr.SpeakerName, "sSpeakerTag"),
                                new NoneProperty()
                            }));
                        }
                    }

                    if (spkr.FaceFX_Male == null)
                    {
                        m_aMaleFaceSets.Add(new ObjectProperty(0));
                    }
                    else
                    {
                        m_aMaleFaceSets.Add(new ObjectProperty(spkr.FaceFX_Male));
                    }
                    if (spkr.FaceFX_Female == null)
                    {
                        m_aFemaleFaceSets.Add(new ObjectProperty(0));
                    }
                    else
                    {
                        m_aFemaleFaceSets.Add(new ObjectProperty(spkr.FaceFX_Female));
                    }
                }

                if (m_aSpeakerList.Count > 0 && Pcc.Game == MEGame.ME3)
                {
                    SelectedConv.BioConvo.AddOrReplaceProp(m_aSpeakerList);
                }
                else if (m_SpeakerList.Count > 0)
                {
                    SelectedConv.BioConvo.AddOrReplaceProp(m_SpeakerList);
                }
                if (m_aMaleFaceSets.Count > 0)
                {
                    SelectedConv.BioConvo.AddOrReplaceProp(m_aMaleFaceSets);
                }
                if (m_aFemaleFaceSets.Count > 0)
                {
                    SelectedConv.BioConvo.AddOrReplaceProp(m_aFemaleFaceSets);
                }
                PushConvoToFile(SelectedConv);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Speaksave FAILED. {e}");

            }
        }
        public void RecreateNodesToProperties(ConversationExtended conv, bool pushtofile = true)
        {
            AutoGenerateSpeakerArrays(conv);
            var newstartlist = new ArrayProperty<IntProperty>("m_StartingList");
            foreach ((var _, int value) in conv.StartingList)
            {
                newstartlist.Add(value);
            }

            var newentryList = new ArrayProperty<StructProperty>("m_EntryList");
            foreach (var entry in conv.EntryList.OrderBy(entry => entry.NodeCount))
            {
                newentryList.Add(entry.NodeProp);
            }
            var newreplyList = new ArrayProperty<StructProperty>("m_ReplyList");
            foreach (var reply in conv.ReplyList.OrderBy(reply => reply.NodeCount))
            {
                newreplyList.Add(reply.NodeProp);
            }

            if (newstartlist.Count > 0)
            {
                conv.BioConvo.AddOrReplaceProp(newstartlist);
            }

            if (newentryList.Count > 0)
            {
                conv.BioConvo.AddOrReplaceProp(newentryList);
            }

            if (newreplyList.Count >= 0)
            {
                conv.BioConvo.AddOrReplaceProp(newreplyList);
            }

            if (pushtofile)
            {
                PushConvoToFile(conv);
            }
        }
        private void SaveScriptsToProperties(ConversationExtended conv, bool pushtofile = true)
        {
            if (Pcc.Game == MEGame.ME3)
            {
                var newscriptList = new ArrayProperty<NameProperty>("m_aScriptList");
                foreach (var script in conv.ScriptList)
                {
                    if (script.Name != "None")
                    {
                        newscriptList.Add(new NameProperty(script, "m_aScriptList"));
                    }
                }
                if (newscriptList.Count > 0)
                {
                    conv.BioConvo.AddOrReplaceProp(newscriptList);
                }
                else
                {
                    conv.BioConvo.TryReplaceProp(newscriptList);
                }
            }
            else
            {
                var newscriptList = new ArrayProperty<StructProperty>("m_ScriptList");
                foreach (var script in conv.ScriptList)
                {
                    if (script.Name != "None")
                    {
                        newscriptList.Add(new StructProperty("BioDialogScript", new PropertyCollection
                        {
                            new NameProperty(script, "sScriptTag"),
                            new NoneProperty()
                        }));
                    }
                }
                if (newscriptList.Count > 0)
                {
                    conv.BioConvo.AddOrReplaceProp(newscriptList);
                }
                else
                {
                    conv.BioConvo.TryReplaceProp(newscriptList);
                }
            }
            if (pushtofile)
            {
                PushConvoToFile(conv);
            }
        }
        private static void SaveStageDirectionsToProperties(ConversationExtended conv)
        {
            var aStageDirs = new ArrayProperty<StructProperty>("m_aStageDirections");
            foreach (var stageD in conv.StageDirections)
            {
                var p = new PropertyCollection();
                p.AddOrReplaceProp(new StrProperty(stageD.Direction, "sText"));
                p.AddOrReplaceProp(new StringRefProperty(stageD.StageStrRef, "srStrRef"));
                p.AddOrReplaceProp(new NoneProperty());
                aStageDirs.Add(new StructProperty("BioStageDirection", p));
            }
            conv.BioConvo.AddOrReplaceProp(aStageDirs);
            PushConvoToFile(conv);
        }
        #endregion RecreateToFile

        #region Handling-updates
        public override void handleUpdate(List<PackageUpdate> updates)
        {
            if (Pcc == null || IsLocalUpdate)
            {
                if (IsLocalUpdate) //If local load just refresh interpreter
                {
                    Properties_InterpreterWPF.LoadExport(SelectedConv.Export);
                    IsLocalUpdate = false;
                }
                return; //nothing is loaded
            }
            List<PackageUpdate> relevantUpdates = updates.Where(x => x.change != PackageChange.Import &&
                                                                            x.change != PackageChange.ImportAdd &&
                                                                            x.change != PackageChange.Names).ToList();

            if (SelectedConv != null && CurrentLoadedExport.ClassName != "BioConversation")
            {
                //loaded convo is no longer a convo
                SelectedConv = null;
                SelectedSpeakerList.ClearEx();
                Conversations_ListBox.SelectedIndex = -1;
                graphEditor.nodeLayer.RemoveAllChildren();
                graphEditor.edgeLayer.RemoveAllChildren();
                Properties_InterpreterWPF.UnloadExport();
                SoundpanelWPF_F.UnloadExport();
                SoundpanelWPF_M.UnloadExport();
                FaceFXAnimSetEditorControl_F.UnloadExport();
                FaceFXAnimSetEditorControl_M.UnloadExport();
                LoadConversations();
                return;
            }

            List<int> updatedConvos = relevantUpdates.Select(x => x.index).Where(update => Pcc.getExport(update).ClassName == "BioConversation").ToList();

            if (relevantUpdates.Select(x => x.index).Any(update => Pcc.getExport(update).ClassName == "FaceFXAnimSet"))
            {
                FFXAnimsets.Clear(); //REBUILD ANIMSET LIST IF NEW ONES and Rerun parsing of speakers.
                foreach (var exp in Pcc.Exports.Where(exp => exp.ClassName == "FaceFXAnimSet"))
                {
                    FFXAnimsets.Add(exp);
                }

                if (SelectedConv != null) updatedConvos.Add(SelectedConv.Export.Index);
            }

            if (SelectedDialogueNode != null) //Update any changes to live dialogue node
            {
                if (relevantUpdates.Select(x => x.index).Any(update => Pcc.getExport(update) == SelectedDialogueNode.Interpdata))
                {
                    if (SelectedDialogueNode.Interpdata.ClassName == "Interpdata") //If changed??
                    {
                        var lengthprop = SelectedDialogueNode.Interpdata.GetProperty<FloatProperty>("InterpLength");
                        if (lengthprop != null)
                        {
                            SelectedDialogueNode.InterpLength = lengthprop.Value;
                        }
                    }
                }
            }




            if (updatedConvos.IsEmpty())
                return;

            int cSelectedIdx = Conversations_ListBox.SelectedIndex;
            int sSelectedIdx = Speakers_ListBox.SelectedIndex;
            foreach (var uxp in updatedConvos)
            {
                var exp = Pcc.getExport(uxp);
                int index = Conversations.FindIndex(i => i.ExportUID == exp.UIndex);
                Conversations.RemoveAt(index);
                Conversations.Insert(index, new ConversationExtended(exp.UIndex, exp.ObjectName, exp.GetProperties(), exp, new ObservableCollectionExtended<SpeakerExtended>(), new ObservableCollectionExtended<DialogueNodeExtended>(), new ObservableCollectionExtended<DialogueNodeExtended>(), new ObservableCollectionExtended<StageDirection>()));
            }

            FirstParse();
            Conversations_ListBox.SelectedIndex = cSelectedIdx;
            Speakers_ListBox.SelectedIndex = sSelectedIdx;
            if (!NoUIRefresh)
            {
                RefreshView();
                SetUIMode(CurrentUIMode, true);
                DialogueNode_SelectByIndex(-1);
            }
            NoUIRefresh = false;

        }

        public void NodePropertyChanged(object sender, PropertyChangedEventArgs e) //update handler for selecteddiagnode.
        {
            if (sender == null || SelectedConv == null || SelectedDialogueNode == null)
                return;

            var diagnode = (DialogueNodeExtended)sender;  //THIS IS A GATE TO CHECK IF VALUES HAVE CHANGED
            var newvalue = diagnode.GetType().GetProperty(e.PropertyName).GetValue(diagnode, null);
            var oldvalue = MirrorDialogueNode.GetType().GetProperty(e.PropertyName).GetValue(MirrorDialogueNode, null);
            if (newvalue.ToString() == oldvalue.ToString())
            {
                return;
            }
            MirrorDialogueNode.GetType().GetProperty(e.PropertyName).SetValue(MirrorDialogueNode, newvalue);
            //IF PASS THEN RECREATE NODE
            var node = SelectedDialogueNode;
            var prop = node.NodeProp;
            IsLocalUpdate = true;  //Full reparse of changed convo not needed.



            if (e.PropertyName == "LineStrRef" || e.PropertyName == "SpeakerIndex")
            {
                IsLocalUpdate = false;  //StrRef/Speaker change requires full reparse due to FaceFX/Rechart.
            }
            var needsRefresh = false; //Controls if refresh chart (auto happens on full parse)

            switch (e.PropertyName)         // Props in both replies and entries. All Games.
            {
                case "Listener":
                    var nListenerIndex = new IntProperty(node.Listener, "nListenerIndex");
                    prop.Properties.AddOrReplaceProp(nListenerIndex);
                    break;
                case "LineStrRef":
                    var srText = new StringRefProperty(node.LineStrRef, "srText");
                    prop.Properties.AddOrReplaceProp(srText);
                    break;
                case "ConditionalOrBool":
                    var nConditionalFunc = new IntProperty(node.ConditionalOrBool, "nConditionalFunc");
                    prop.Properties.AddOrReplaceProp(nConditionalFunc);
                    needsRefresh = true;
                    break;
                case "ConditionalParam":
                    var nConditionalParam = new IntProperty(node.ConditionalParam, "nConditionalParam");
                    prop.Properties.AddOrReplaceProp(nConditionalParam);
                    break;
                case "Transition":
                    var nStateTransition = new IntProperty(node.Transition, "nStateTransition");
                    prop.Properties.AddOrReplaceProp(nStateTransition);
                    needsRefresh = true;
                    break;
                case "TransitionParam":
                    var nStateTransitionParam = new IntProperty(node.TransitionParam, "nStateTransitionParam");
                    prop.Properties.AddOrReplaceProp(nStateTransitionParam);
                    break;
                case "ExportID":
                    var nExportID = new IntProperty(node.ExportID, "nExportID");
                    prop.Properties.AddOrReplaceProp(nExportID);
                    break;
                case "CameraIntimacy":
                    var CameraIntimacy = new IntProperty(node.CameraIntimacy, "nCameraIntimacy");
                    prop.Properties.AddOrReplaceProp(CameraIntimacy);
                    break;
                case "FiresConditional":
                    var bFireConditional = new BoolProperty(node.FiresConditional, "bFireConditional");
                    prop.Properties.AddOrReplaceProp(bFireConditional);
                    needsRefresh = true;
                    break;
                case "IsAmbient":
                    var bAmbient = new BoolProperty(node.IsAmbient, "bAmbient");
                    prop.Properties.AddOrReplaceProp(bAmbient);
                    break;
                case "IsNonTextLine":
                    var bNonTextLine = new BoolProperty(node.IsNonTextLine, "bNonTextLine");
                    prop.Properties.AddOrReplaceProp(bNonTextLine);
                    break;
                case "IgnoreBodyGesture":
                    var bIgnoreBodyGestures = new BoolProperty(node.IgnoreBodyGesture, "bIgnoreBodyGestures");
                    prop.Properties.AddOrReplaceProp(bIgnoreBodyGestures);
                    break;
                case "Script":
                    var scriptidx = SelectedConv.ScriptList.FindIndex(s => s == node.Script) - 1;
                    var nScriptIndex = new IntProperty(scriptidx, "nScriptIndex");
                    prop.Properties.AddOrReplaceProp(nScriptIndex);
                    break;
                case "GUIStyle":
                    var EConvGUIStyles = new EnumProperty(node.GUIStyle.ToString(), "EConvGUIStyles", Pcc.Game, "eGUIStyle");
                    prop.Properties.AddOrReplaceProp(EConvGUIStyles);
                    break;
                default:
                    break;
            }
            //Skip SText
            if (Pcc.Game == MEGame.ME3 && e.PropertyName == "HideSubtitle")
            {
                var bAlwaysHideSubtitle = new BoolProperty(node.HideSubtitle, "bAlwaysHideSubtitle");
                prop.Properties.AddOrReplaceProp(bAlwaysHideSubtitle);
            }


            if (!SelectedDialogueNode.IsReply)
            {
                //Ignore replylist for now
                //Ignore aSpeakerList  <-- autorecreated
                var nSpeakerIndex = new IntProperty(node.SpeakerIndex, "nSpeakerIndex");
                prop.Properties.AddOrReplaceProp(nSpeakerIndex);
                var bSkippable = new BoolProperty(node.IsSkippable, "bSkippable");
                prop.Properties.AddOrReplaceProp(bSkippable);

            }
            else
            {
                //Ignore Entry List
                var bUnskippable = new BoolProperty(node.IsUnskippable, "bUnskippable");
                prop.Properties.AddOrReplaceProp(bUnskippable);
                if (e.PropertyName == "ReplyType")
                {
                    var ReplyType = new EnumProperty(node.ReplyType.ToString(), "EReplyTypes", Pcc.Game, "ReplyType");
                    prop.Properties.AddOrReplaceProp(ReplyType);
                    needsRefresh = true;
                }


                if (Pcc.Game == MEGame.ME3 && (e.PropertyName == "bIsDefaultAction" || e.PropertyName == "bIsMajorDecision"))
                {
                    var bIsDefaultAction = new BoolProperty(node.IsDefaultAction, "bIsDefaultAction");
                    prop.Properties.AddOrReplaceProp(bIsDefaultAction);
                    var bIsMajorDecision = new BoolProperty(node.IsMajorDecision, "bIsMajorDecision");
                    prop.Properties.AddOrReplaceProp(bIsMajorDecision);
                }
            }

            RecreateNodesToProperties(SelectedConv);

            if (needsRefresh)
                RefreshView();

        }

        #endregion Handling-updates

        #region Recents

        private readonly List<Button> RecentButtons = new List<Button>();
        public List<string> RFiles;
        private const string RECENTFILES_FILE = "RECENTFILES";

        private void LoadRecentList()
        {
            RecentButtons.AddRange(new[] { RecentButton1, RecentButton2, RecentButton3, RecentButton4, RecentButton5, RecentButton6, RecentButton7, RecentButton8, RecentButton9, RecentButton10 });
            Recents_MenuItem.IsEnabled = false;
            RFiles = new List<string>();
            RFiles.Clear();
            string path = Path.Combine(DialogueEditorDataFolder, RECENTFILES_FILE);
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
            if (!Directory.Exists(DialogueEditorDataFolder))
            {
                Directory.CreateDirectory(DialogueEditorDataFolder);
            }

            string path = Path.Combine(DialogueEditorDataFolder, RECENTFILES_FILE);
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
                    if (window is DialogueEditorWPF wpf && this != wpf)
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
                MenuItem fr = new MenuItem
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
                MessageBox.Show("File does not exist: " + s);
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

        #region CreateGraph

        public void GenerateGraph()
        {

            if (File.Exists(JSONpath) && LayoutMode != 2)
            {
                SavedPositions = JsonConvert.DeserializeObject<List<SaveData>>(File.ReadAllText(JSONpath));
            }
            else
            {
                SavedPositions = new List<SaveData>();
            }
            extraSaveData.Clear();

            CurrentObjects.ClearEx();
            graphEditor.nodeLayer.RemoveAllChildren();
            graphEditor.edgeLayer.RemoveAllChildren();
            StartPoDStarts = 0;
            StartPoDiagNodes = 0;
            StartPoDReplyNodes = 20;
            if (SelectedConv == null)
                return;


            LoadDialogueObjects();
            Layout();
            graphEditor.Enabled = true;
            graphEditor.UseWaitCursor = false;
            foreach (DObj o in CurrentObjects)
            {
                o.MouseDown += node_MouseDown;
                o.Click += node_Click;
            }

            graphEditor.Camera.X = 0;
            graphEditor.Camera.Y = 0;
            if (SavedPositions.IsEmpty() || SaveViewMode == 2)
            {
                AutoLayout();
            }
        }
        public bool LoadDialogueObjects()
        {
            float x = 0;
            float y = 0;
            int ecnt = SelectedConv.EntryList.Count;
            int rcnt = SelectedConv.ReplyList.Count;
            int max = Math.Max(ecnt, rcnt);
            var startlist = new Dictionary<int, int>(SelectedConv.StartingList); //Dictionary (Key = position on list, value = outlink)
            for (int n = 0; n < max; n++)
            {
                bool isInList = startlist.Values.IndexOf(n) != -1;
                if (isInList)
                {
                    var startOrder = startlist.FirstOrDefault(k => k.Value == n).Key;
                    var newstart = new DStart(this, startOrder, n, x, y, graphEditor);
                    CurrentObjects.Add(newstart);
                }
                if (n < ecnt)
                {
                    CurrentObjects.Add(new DiagNodeEntry(this, SelectedConv.EntryList[n], x, y, graphEditor));
                }

                if (n < rcnt)
                {
                    CurrentObjects.Add(new DiagNodeReply(this, SelectedConv.ReplyList[n], x, y, graphEditor));
                }
            }

            return true;
        }
        public void Layout()
        {
            if (CurrentObjects != null && CurrentObjects.Any())
            {
                foreach (DObj obj in CurrentObjects)
                {
                    graphEditor.addNode(obj);
                }

                foreach (DObj obj in CurrentObjects)
                {
                    obj.CreateConnections(CurrentObjects);
                }

                foreach (DObj obj in CurrentObjects)
                {
                    //SAVED DATA
                    SaveData savedInfo = new SaveData(-1);
                    if (SavedPositions.Any() && LayoutMode != 2)
                    {
                        DObj obj1 = obj;
                        savedInfo = SavedPositions.FirstOrDefault(p => obj1.NodeUID == p.index);
                    }

                    bool hasSavedPosition = savedInfo.index == obj.NodeUID;
                    if (hasSavedPosition)
                    {
                        obj.Layout(savedInfo.X, savedInfo.Y);
                    }
                    else
                    {
                        switch (obj)
                        {
                            case DStart dStart:
                                float ystart = dStart.StartNumber * 127;
                                obj.Layout(0, ystart);
                                //StartPoDStarts += obj.Height + 20;
                                break;
                            case DiagNodeReply _:
                                obj.Layout(500, StartPoDReplyNodes);
                                StartPoDReplyNodes += obj.Height + 25;
                                break;
                            case DiagNode _:
                                obj.Layout(250, StartPoDiagNodes);
                                StartPoDiagNodes += obj.Height + 25;
                                break;

                        }
                    }
                }

                foreach (DiagEdEdge edge in graphEditor.edgeLayer)
                {
                    ConvGraphEditor.UpdateEdge(edge);
                }
            }
        }
        private void AutoLayout()
        {
            switch (LayoutMode)
            {
                case 1:
                    AutoLayout_Waterfall();
                    break;
                case 2:
                    AutoLayout_AdvancedColumn();
                    break;
                default:
                    AutoLayout_SimpleColumn();
                    break;
            }
        }
        private void AutoLayout_SimpleColumn()
        {
            if (CurrentObjects != null && CurrentObjects.Any())
            {
                foreach (DObj obj in CurrentObjects)
                {
                    obj.SetOffset(0, 0); //remove existing positioning
                }

                const float HORIZONTAL_SPACING = 400;
                float VERTICAL_SPACING = 30;
                if (ShowLinesOnTop_MenuItem.IsChecked)
                    VERTICAL_SPACING = 45;

                var layoutEntries = new Queue<DiagNodeEntry>();
                var layoutReplies = new Queue<DiagNodeReply>();
                var layoutStarts = new ObservableCollectionExtended<DStart>();
                foreach (var obj in CurrentObjects)
                {
                    switch (obj)
                    {
                        case DStart dStart:
                            layoutStarts.Add(dStart);
                            break;
                        case DiagNodeReply diagNodeReply:
                            layoutReplies.Enqueue(diagNodeReply);
                            break;
                        case DiagNodeEntry diagNodeEntry:
                            layoutEntries.Enqueue(diagNodeEntry);
                            break;
                    }
                }

                StartPoDStarts = 0;
                float addheight = 0;
                int currentrow = 0;
                while (layoutEntries.Count > 0 || layoutReplies.Count > 0 || layoutStarts.Count > 0)
                {
                    DStart start = layoutStarts.FirstOrDefault(n => n.StartNumber == currentrow);
                    if (start != null)
                    {
                        start.SetOffset(0, StartPoDStarts);
                        if (start.Height > addheight)
                        {
                            addheight = start.Height;
                        }
                        layoutStarts.Remove(start);
                    }

                    
                    if (layoutEntries.Count > 0)
                    {
                        DiagNodeEntry entry = layoutEntries.Dequeue();
                        entry.SetOffset(HORIZONTAL_SPACING, StartPoDStarts);
                        if (entry.Height > addheight)
                        {
                            addheight = entry.Height;
                        }
                    }


                    if (layoutReplies.Count > 0)
                    {
                        DiagNodeReply reply = layoutReplies.Dequeue();
                        reply.SetOffset(HORIZONTAL_SPACING * 2, StartPoDStarts + 30);
                        if (reply.Height > addheight)
                        {
                            addheight = reply.Height;
                        }
                    }

                    //Adjust height of next start
                    StartPoDStarts += addheight + VERTICAL_SPACING;
                    addheight = 0;
                    currentrow++;
                }

                foreach (DiagEdEdge edge in graphEditor.edgeLayer)
                {
                    ConvGraphEditor.UpdateEdge(edge);
                }
            }
        }
        private void AutoLayout_AdvancedColumn()
        {
            foreach (DObj obj in CurrentObjects)
            {
                obj.SetOffset(0, 0); //remove existing positioning
            }
            int rowAt = 0;
            int maxEntryRow = -1;
            int maxReplyRow = -1;
            int maxStartrow = -1;
            float maxobjHeight = 0;
            float rowShift = 0;
            const float COLUMN_SPACING = 350;
            const float WATERFALL_SPACING = 40;
            const float ROW_SPACING = 200;
            var visitedNodes = new HashSet<int>();
            List<DStart> startNodes = CurrentObjects.OfType<DStart>().ToList();
            List<DiagNode> allNodes = CurrentObjects.OfType<DiagNode>().OrderBy(n => n.NodeUID).ToList();
            var BranchQueue = new Queue<DiagNode>();

            //TAKE First Start - use to get first stack of columns
            //add to first layer until end. any other branches add to branch stack LIFO to create second etc layer.
            // If no branches in stack then go back to new start.
            // Every start

            while (allNodes.Count > 0)
            {
                DStart firstNode = startNodes.FirstOrDefault();
                if (firstNode != null)
                {
                    if (maxEntryRow <= maxReplyRow) // means finished on reply
                    {
                        maxStartrow = maxReplyRow + 1; //start next row.
                    }
                    else
                    {
                        maxStartrow = maxEntryRow + 1;
                    }

                    firstNode.SetOffset(0, maxStartrow * ROW_SPACING + rowShift);
                    startNodes.Remove(firstNode);
                    visitedNodes.Add(firstNode.NodeUID);
                    DiagNode nextNode = allNodes.FirstOrDefault(x => x.NodeUID == firstNode.StartNumber);
                    if (nextNode != null && !visitedNodes.Contains(nextNode.NodeUID))
                    {
                        while (!(nextNode == null && BranchQueue.IsEmpty()))
                        {
                            var thisNode = nextNode;
                            nextNode = null;
                            if (thisNode != null && !visitedNodes.Contains(thisNode.NodeUID))
                            {

                                int r = 0;
                                if (!thisNode.Node.IsReply) 
                                {
                                    if (maxobjHeight > ROW_SPACING) //On entry set spacing for this row
                                    {
                                        rowShift += maxobjHeight + 30 - ROW_SPACING;
                                    }



                                    if (maxEntryRow >= rowAt)
                                        rowAt = maxEntryRow + 1;

                                    r = 1000; //Conversion factor from nIndex to NodeUID to link to reply
                                    thisNode.SetOffset(COLUMN_SPACING, rowAt * ROW_SPACING + rowShift);
                                    maxEntryRow = rowAt;
                                    maxobjHeight = thisNode.Height;

                                }
                                else
                                {
                                    if (maxReplyRow >= rowAt)
                                        rowAt = maxReplyRow + 1;

                                    thisNode.SetOffset(2 * COLUMN_SPACING, rowAt * ROW_SPACING + rowShift + WATERFALL_SPACING);
                                    maxReplyRow = rowAt;
                                    rowAt++;  //After reply go to next row.
                                    if(thisNode.Height > maxobjHeight)
                                    {
                                        maxobjHeight = thisNode.Height;
                                    }
                                }
                                visitedNodes.Add(thisNode.NodeUID);
                                allNodes.Remove(thisNode);
                                if (thisNode.Links.Count != 0)
                                {
                                    for (int i = 0; i < thisNode.Links.Count; i++) //DO IN REVERSE SO STACK IS CORRECTLY DONE
                                    {
                                        if (i == 0)
                                        {
                                            nextNode = allNodes.FirstOrDefault(x => x.NodeUID == (thisNode.Links[0].Index + r));
                                        }
                                        else
                                        {
                                            var pushqueue = allNodes.FirstOrDefault(x => x.NodeUID == thisNode.Links[i].Index + r);
                                            if (pushqueue != null) //means link to visited node.  Don't add to Branchstack.
                                            {
                                                BranchQueue.Enqueue(pushqueue);
                                            }
                                        }
                                    }
                                }
                            }
                            else if (!BranchQueue.IsEmpty())//REACHED END OF BRANCH PULL nextNode from STACK
                            {
                                nextNode = BranchQueue.Dequeue();
                                if (visitedNodes.Contains(nextNode.NodeUID)) //if nextnode is already up, make sure stack is pulled again without moving down.
                                {
                                    nextNode = null;
                                }
                            }
                            else
                            {
                                rowAt++;
                            }
                        }
                    }
                }
                else //everything else is orphan.
                {
                    int orphanrowEntry = maxStartrow;
                    int orphanrowReply = maxStartrow;
                    foreach (var obj in allNodes)
                    {
                        if (obj.Node.IsReply)
                        {
                            obj.SetOffset(2 * COLUMN_SPACING, orphanrowReply * ROW_SPACING + WATERFALL_SPACING);
                            orphanrowReply++;
                        }
                        else
                        {
                            obj.SetOffset(1 * COLUMN_SPACING, orphanrowEntry * ROW_SPACING);
                            orphanrowEntry++;
                        }
                    }
                    break;
                }
            }

            foreach (DiagEdEdge edge in graphEditor.edgeLayer)
            {
                ConvGraphEditor.UpdateEdge(edge);
            }
        }
        private void AutoLayout_Waterfall()
        {

            foreach (DObj obj in CurrentObjects)
            {
                obj.SetOffset(0, 0); //remove existing positioning
            }
            int rowAt = 0;
            int columnAt = 0;
            int maxrow = 0;
            const float COLUMN_SPACING = 220;
            const float WATERFALL_SPACING = 40;
            const float ROW_SPACING = 200;
            var visitedNodes = new HashSet<int>();
            List<DStart> startNodes = CurrentObjects.OfType<DStart>().ToList();
            List<DiagNode> allNodes = CurrentObjects.OfType<DiagNode>().OrderBy(n => n.NodeUID).ToList();
            var BranchStack = new Stack<(DiagNode node, int column)>();

            //TAKE First Start - use to get first layer of waterfall.
            //add to first layer until end. any other branches add to branch stack LIFO to create second etc layer.
            // If no branches in stack then go back to new start.

            while (allNodes.Count > 0)
            {
                DStart firstNode = startNodes.FirstOrDefault();
                if(firstNode != null)
                {
                    if (rowAt > maxrow)
                    {
                        maxrow = rowAt;
                    }
                    else
                    {
                        rowAt = maxrow;
                    }

                    columnAt = 0;
                    
                    firstNode.SetOffset(columnAt, maxrow * ROW_SPACING);
                    maxrow++;
                    startNodes.Remove(firstNode);
                    visitedNodes.Add(firstNode.NodeUID);
                    DiagNode nextNode = allNodes.FirstOrDefault(x => x.NodeUID == firstNode.StartNumber);
                    if (nextNode != null && !visitedNodes.Contains(nextNode.NodeUID))
                    {
                        while (!(nextNode == null && BranchStack.IsEmpty()))
                        {
                            var thisNode = nextNode;
                            nextNode = null;
                            if (thisNode != null && !visitedNodes.Contains(thisNode.NodeUID))
                            {
                                columnAt++;
                                thisNode.SetOffset(columnAt * COLUMN_SPACING, rowAt * ROW_SPACING + columnAt * WATERFALL_SPACING);
                                visitedNodes.Add(thisNode.NodeUID);
                                allNodes.Remove(thisNode);
                                if (thisNode.Links.Count != 0)
                                {
                                    int r = 0;
                                    if(!thisNode.Node.IsReply) //Conversion factor from nIndex to NodeUID
                                    {
                                        r = 1000;
                                    }
                                    for (int i = thisNode.Links.Count - 1; i >= 0; i--) //DO IN REVERSE SO STACK IS CORRECTLY DONE
                                    {
                                        if (i == 0)
                                        {
                                            nextNode = allNodes.FirstOrDefault(x => x.NodeUID == (thisNode.Links[0].Index + r));
                                        }
                                        else
                                        {
                                            var pushstack = allNodes.FirstOrDefault(x => x.NodeUID == thisNode.Links[i].Index + r);
                                            if(pushstack != null) //means link to visited node.  Don't add to Branchstack.
                                            {
                                                BranchStack.Push((pushstack, columnAt));
                                            }
                                        }
                                    }
                                }
                            }
                            else if(!BranchStack.IsEmpty())//REACHED END OF BRANCH PULL nextNode from STACK
                            {
                                (nextNode, columnAt) = BranchStack.Pop();

                                if(visitedNodes.Contains(nextNode.NodeUID)) //if nextnode is already up, make sure stack is pulled again without moving down.
                                {
                                    nextNode = null;
                                }
                                else
                                {
                                    rowAt++;
                                    maxrow++;
                                }
                            }
                        }
                    }
                }
                else //everything else is orphan.
                {
                    int orphanrowEntry = maxrow;
                    int orphanrowReply = maxrow;
                    foreach (var obj in allNodes)
                    {
                        if (obj.Node.IsReply)
                        {
                            obj.SetOffset(2 * COLUMN_SPACING, orphanrowReply * ROW_SPACING + WATERFALL_SPACING);
                            orphanrowReply++;
                        }
                        else
                        {
                            obj.SetOffset(1 * COLUMN_SPACING, orphanrowEntry * ROW_SPACING);
                            orphanrowEntry++;
                        }
                    }
                    break;
                }
            }

            foreach (DiagEdEdge edge in graphEditor.edgeLayer)
            {
                ConvGraphEditor.UpdateEdge(edge);
            }
        }

        public void RefreshView()
        {
            if (SelectedConv != null)
            {
                Properties_InterpreterWPF.LoadExport(CurrentLoadedExport);
                if (SelectedDialogueNode != null)
                {
                    RefreshExportLoaders();
                }

                GenerateGraph();
                if (LayoutMode != 2)
                {
                    saveView(false);
                }
            }
        }

        private void RefreshExportLoaders()
        {
            if(SelectedDialogueNode.WwiseStream_Female == null)
            {
                SoundpanelWPF_F.UnloadExport();
            }
            else
            {
                SoundpanelWPF_F.LoadExport(SelectedDialogueNode.WwiseStream_Female);
            }
                    
            if(SelectedDialogueNode.WwiseStream_Male == null)
            {
                SoundpanelWPF_M.UnloadExport();
            }
            else
            {
                SoundpanelWPF_M.LoadExport(SelectedDialogueNode.WwiseStream_Male);
            }

            soundPanelTabControl.SelectedIndex = faceFXEditorTabControl.SelectedIndex = SelectedDialogueNode.WwiseStream_Female == null ? 1 : 0;
            if (SelectedDialogueNode.SpeakerTag.FaceFX_Female is ExportEntry faceFX_f)
            {
                FaceFXAnimSetEditorControl_F.LoadExport(faceFX_f);
                FaceFXAnimSetEditorControl_F.SelectLineByName(SelectedDialogueNode.FaceFX_Female);
            }
            else
            {
                FaceFXAnimSetEditorControl_F.UnloadExport();
            }

            if (SelectedDialogueNode.SpeakerTag.FaceFX_Male is ExportEntry faceFX_m)
            {
                FaceFXAnimSetEditorControl_M.LoadExport(faceFX_m);
                FaceFXAnimSetEditorControl_M.SelectLineByName(SelectedDialogueNode.FaceFX_Male);
            }
            else
            {
                FaceFXAnimSetEditorControl_M.UnloadExport();
            }
        }

        #endregion CreateGraph  

        #region UIHandling-items
        /// <summary>
        /// Sets UI to 0 = Convo (default), 1=Speakers, 2=Node.
        /// </summary>
        private void SetUIMode(int mode, bool force = false)
        {
            if (mode == CurrentUIMode && !force)
            {
                return;
            }
            CurrentUIMode = mode;

            Speaker_Panel.Visibility = Visibility.Collapsed;
            Convo_Panel.Visibility = Visibility.Collapsed;
            Node_Panel.Visibility = Visibility.Collapsed;
            Start_Panel.Visibility = Visibility.Collapsed;
            switch (CurrentUIMode)
            {
                case 1:
                    Speaker_Panel.Visibility = Visibility.Visible;
                    break;
                case 2:
                    Node_Panel.Visibility = Visibility.Visible;
                    break;
                case 3:
                    Start_Panel.Visibility = Visibility.Visible;
                    break;
                default:
                    Convo_Panel.Visibility = Visibility.Visible;
                    break;

            }

            StageDirections_Expander.Visibility = Pcc.Game == MEGame.ME3 ? Visibility.Visible : Visibility.Collapsed;
        }
        private void ListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox box)
            {
                switch (box.Name)
                {
                    case "Speakers_ListBox":
                        SetUIMode(1, true);
                        break;
                    default:
                        SetUIMode(0, true);
                        break;
                }
            }

        }

        private void ConversationList_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AutoSaveView_MenuItem.IsChecked)
            {
                saveView();
            }

            if (Conversations_ListBox.SelectedIndex < 0)
            {
                SelectedConv = null;
                SelectedDialogueNode = null;
                SelectedSpeakerList.ClearEx();
                Properties_InterpreterWPF.UnloadExport();
                SoundpanelWPF_F.UnloadExport();
                SoundpanelWPF_M.UnloadExport();
                FaceFXAnimSetEditorControl_F.UnloadExport();
                FaceFXAnimSetEditorControl_M.UnloadExport();
                Convo_Panel.Visibility = Visibility.Collapsed;
            }
            else
            {
                SelectedDialogueNode = null; //Before convos change make sure no properties fire.
                graphEditor.Enabled = false;
                graphEditor.UseWaitCursor = true;
                var nconv = Conversations[Conversations_ListBox.SelectedIndex];
                SelectedConv = new ConversationExtended(nconv);
                CurrentLoadedExport = SelectedConv.Export;
                SetupConvJSON(CurrentLoadedExport);
                if (Pcc.Game == MEGame.ME1)
                {
                    LevelHeader.Text = "Audio/Matinee File:";
                    LevelHeader.ToolTip = "File that contains the audio and cutscene data for the conversation";
                    Level_Textbox.ToolTip = "File that contains the audio and cutscene data for the conversation";
                    OpenLevelPackEd_Button.ToolTip = "Open Audio/Matinee File in Package Editor";
                    OpenLevelSeqEd_Button.ToolTip = "Open Audio/Matinee File in Sequence Editor";
                }
                else
                {
                    LevelHeader.Text = "Level:";
                    LevelHeader.ToolTip = "File with the level and sequence that uses the conversation.";
                    Level_Textbox.ToolTip = "File with the level and sequence that uses the conversation.";
                    OpenLevelPackEd_Button.ToolTip = "Open level in Package Editor";
                    OpenLevelSeqEd_Button.ToolTip = "Open level in Sequence Editor";
                }

                GenerateSpeakerList();
                RefreshView();
                Start_ListBoxUpdate();

                ListenersList.ClearEx();
                ListenersList.Add(new SpeakerExtended(-3, "none"));
                foreach (var spkr in SelectedSpeakerList)
                {
                    ListenersList.Add(spkr);
                }

            }
            graphEditor_PanTo();

        }
        private void Convo_NSFFX_DropDownClosed(object sender, EventArgs e)
        {
            if (FFXAnimsets.Count < 1 || Conversations[Conversations_ListBox.SelectedIndex].NonSpkrFFX == null)
                return;

            if (Conversations[Conversations_ListBox.SelectedIndex].NonSpkrFFX.UIndex != FFXAnimsets[ComboBox_Conv_NSFFX.SelectedIndex].UIndex)
            {
                SelectedConv.BioConvo.AddOrReplaceProp(new ObjectProperty(SelectedConv.NonSpkrFFX, "m_pNonSpeakerFaceFXSet"));
                PushConvoToFile(SelectedConv);
            }
        }
        private void SetupConvJSON(ExportEntry export)
        {
            string objectName = Regex.Replace(export.ObjectName, @"[<>:""/\\|?*]", "");
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

            JSONpath = Path.Combine(viewsPath, $"{CurrentFile}.#{export.Index}{objectName}.JSON");
        }

        private void Speakers_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!SelectedSpeakerList.IsEmpty())
            {
                if (Speakers_ListBox.SelectedIndex >= 0)
                {
                    if (SelectedSpeaker.StrRefID <= 0)
                    {
                        SelectedSpeaker.StrRefID = LookupTagRef(SelectedSpeaker.SpeakerName);
                        SelectedSpeaker.FriendlyName = GlobalFindStrRefbyID(SelectedSpeaker.StrRefID, Pcc);
                    }

                    TextBox_Speaker_Name.IsEnabled = SelectedSpeaker.SpeakerID >= 0;
                }
                else
                {
                    SelectedSpeaker = SelectedSpeakerList[0];
                }
            }
        }
        private void SpeakerMoveAction(object obj)
        {
            SpkrUpButton.IsEnabled = false;
            SpkrDownButton.IsEnabled = false;
            string direction = obj as string;
            int n = 1; //Movement default is down the list (higher n)
            if (direction == "Up")
            {
                n = -1;
            }

            int selectedIndex = Speakers_ListBox.SelectedIndex;
            Speakers_ListBox.SelectedIndex = -1;

            var OldSpkrList = new ObservableCollectionExtended<SpeakerExtended>(SelectedSpeakerList);
            SelectedSpeakerList.ClearEx();

            var itemToMove = OldSpkrList[selectedIndex];
            itemToMove.SpeakerID = selectedIndex + n - 2;
            OldSpkrList[selectedIndex + n].SpeakerID = selectedIndex - 2;
            OldSpkrList.RemoveAt(selectedIndex);
            OldSpkrList.Insert(selectedIndex + n, itemToMove);

            foreach (var spkr in OldSpkrList)
            {
                SelectedSpeakerList.Add(spkr);
            }
            SelectedConv.Speakers = new ObservableCollectionExtended<SpeakerExtended>(SelectedSpeakerList);
            Speakers_ListBox.SelectedIndex = selectedIndex + n;
            SpkrUpButton.IsEnabled = true;
            SpkrDownButton.IsEnabled = true;
            SaveSpeakersToProperties(SelectedSpeakerList);
        }
        private void ComboBox_Speaker_FFX_DropDownClosed(object sender, EventArgs e)
        {
            var ffxMaleNew = SelectedSpeaker.FaceFX_Male;
            var ffxMaleOld = SelectedSpeakerList[SelectedSpeaker.SpeakerID + 2].FaceFX_Male;
            var ffxFemaleNew = SelectedSpeaker.FaceFX_Female;
            var ffxFemaleOld = SelectedSpeakerList[SelectedSpeaker.SpeakerID + 2].FaceFX_Female;
            if (ffxMaleNew == ffxMaleOld && ffxFemaleNew == ffxFemaleOld)
                return;

            System.Media.SystemSounds.Question.Play();
            var dlg = MessageBox.Show("Are you sure you want to change this speaker's facial animation set? (Not recommended)", "WARNING", MessageBoxButton.OKCancel);
            if (dlg == MessageBoxResult.Cancel)
            {
                SelectedSpeaker.FaceFX_Male = ffxMaleOld;
                SelectedSpeaker.FaceFX_Female = ffxFemaleOld;
                return;
            }



            SelectedSpeakerList[SelectedSpeaker.SpeakerID + 2].FaceFX_Male = ffxMaleNew;
            SelectedSpeakerList[SelectedSpeaker.SpeakerID + 2].FaceFX_Female = ffxFemaleNew;

            SaveSpeakersToProperties(SelectedSpeakerList);

        }
        private void EnterName_Speaker_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var dlg = MessageBox.Show("Do you want to change this actor's tag?", "Confirm", MessageBoxButton.YesNo);
                if (dlg != MessageBoxResult.No)
                {
                    Keyboard.ClearFocus();
                    SelectedSpeakerList[Speakers_ListBox.SelectedIndex].SpeakerName = SelectedSpeaker.SpeakerName;
                    SelectedSpeaker.StrRefID = LookupTagRef(SelectedSpeaker.SpeakerName);
                    SelectedSpeaker.FriendlyName = GlobalFindStrRefbyID(SelectedSpeakerList[Speakers_ListBox.SelectedIndex].StrRefID, Pcc);

                    SaveSpeakersToProperties(SelectedSpeakerList);
                }
            }
        }
        private void SpeakerAdd()
        {
            int maxID = SelectedSpeakerList.Max(x => x.SpeakerID);
            var ndlg = new PromptDialog("Enter the new actors tag", "Add a speaker", "Actor_Tag");
            ndlg.ShowDialog();
            if (ndlg.ResponseText == null || ndlg.ResponseText == "Actor_Tag")
                return;
            Pcc.FindNameOrAdd(ndlg.ResponseText);
            SelectedSpeakerList.Add(new SpeakerExtended(maxID + 1, ndlg.ResponseText, null, null, 0, "No Data"));
            SaveSpeakersToProperties(SelectedSpeakerList);
        }
        private void SpeakerDelete()
        {
            var deleteTarget = Speakers_ListBox.SelectedIndex;
            if (deleteTarget < 2)
            {
                MessageBox.Show("Owner and Player speakers cannot be deleted.", "Dialogue Editor");
                return;
            }

            string delName = SelectedSpeakerList[deleteTarget].SpeakerName;
            int delID = SelectedSpeakerList[deleteTarget].SpeakerID;
            var dlg = MessageBox.Show($"Are you sure you want to delete {delID} : {delName}? ", "Warning: Speaker Deletion", MessageBoxButton.OKCancel);

            if (dlg == MessageBoxResult.Cancel)
                return;

            foreach (var node in SelectedConv.EntryList)
            {
                if (node.SpeakerIndex == delID)
                {
                    MessageBox.Show("Deletion Aborted.\r\nSpeakers with active dialogue nodes cannot be deleted.", "Dialogue Editor", MessageBoxButton.OK);
                    return;
                }
            }


            SelectedConv.Speakers.RemoveAt(deleteTarget);
            SelectedSpeakerList.RemoveAt(deleteTarget);
            SaveSpeakersToProperties(SelectedSpeakerList);
        }
        private void SpeakerGoToName()
        {
            TextBox_Speaker_Name.Focus();
            TextBox_Speaker_Name.CaretIndex = TextBox_Speaker_Name.Text.Length;
        }
        private static int LookupTagRef(string actortag)
        {
            if (!TagDBLoaded)
            {
                if (File.Exists(ActorDatabasePath))
                {
                    ActorStrRefs = JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(ActorDatabasePath));
                    TagDBLoaded = true;
                }
            }
            var strref = ActorStrRefs.FirstOrDefault(a => string.Equals(a.Key, actortag, StringComparison.CurrentCultureIgnoreCase));
            if (strref.Key != null)
            {
                return strref.Value;
            }

            return 0;
        }

        private void EditBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            var editbox = (TextBox)sender;
            editbox.BorderThickness = new Thickness(2, 2, 2, 2);
            editbox.Background = System.Windows.Media.Brushes.GhostWhite;
        }
        private void EditBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            var editbox = (TextBox)sender;
            editbox.BorderThickness = new Thickness(0, 0, 0, 0);
            editbox.Background = System.Windows.Media.Brushes.White;
        }
        private void EditBox_Node_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                var tbox = (TextBox)sender;
                Keyboard.ClearFocus();
                var be = tbox.GetBindingExpression(TextBox.TextProperty);
                switch (e.Key)
                {
                    case Key.Enter:
                        be?.UpdateSource();
                        break;
                    case Key.Escape:
                        be?.UpdateTarget();
                        break;
                }
            }
        }
        private void NumberValidationEditBox(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex("[^-]+[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void Start_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var start = CurrentObjects.OfType<DStart>().FirstOrDefault(s => s.Order == Start_ListBox.SelectedIndex);
            if (start == null)
                return;

            foreach (var oldselection in SelectedObjects)
            {
                oldselection.IsSelected = false;
            }
            SelectedObjects.ClearEx();
            start.IsSelected = true;
            SelectedObjects.Add(start);
            panToSelection = false;
        }
        private void StartAddEdit(object param)
        {
            var p = param as string;
            int newKey = SelectedConv.StartingList.Count;
            int f = 0;
            if (p == "Edit")
            {
                newKey = Start_ListBox.SelectedIndex;
                f = SelectedConv.StartingList[newKey];
            }

            var links = new List<string>();
            foreach (var entry in SelectedConv.EntryList)
            {
                links.Add($"{entry.NodeCount}: {entry.LineStrRef} {entry.Line}");
            }
            var sdlg = InputComboBoxWPF.GetValue(this, "Pick an entry node to link to", links, links[f], false);

            if (sdlg == "")
                return;

            var newVal = links.FindIndex(sdlg.Equals);

            if (p == "Edit")
            {
                SelectedConv.StartingList[newKey] = newVal;
            }
            else
            {
                SelectedConv.StartingList.Add(newKey, newVal);
            }

            RecreateNodesToProperties(SelectedConv);
            forcedSelectStart = newKey;
        }
        private void StartDelete()
        {
            SelectedConv.StartingList.Remove(Start_ListBox.SelectedIndex);
            RecreateNodesToProperties(SelectedConv);
        }
        private void StartMoveAction(object obj)
        {
            StartUpButton.IsEnabled = false;
            StartDownButton.IsEnabled = false;
            string direction = obj as string;
            int n = 1; //Movement default is down the list (higher n)
            if (direction == "Up")
            {
                n = -1;
            }

            int selectedIndex = Start_ListBox.SelectedIndex;
            Start_ListBox.SelectedIndex = -1;

            var selectedval = SelectedConv.StartingList[selectedIndex];
            var shiftval = SelectedConv.StartingList[selectedIndex + n];

            SelectedConv.StartingList.Remove(selectedIndex);
            SelectedConv.StartingList.Remove(selectedIndex + n);
            SelectedConv.StartingList.Add(selectedIndex + n, selectedval);
            SelectedConv.StartingList.Add(selectedIndex, shiftval);

            RecreateNodesToProperties(SelectedConv);
            forcedSelectStart = selectedIndex + n;
            StartUpButton.IsEnabled = true;
            StartDownButton.IsEnabled = true;
        }
        private void Start_ListBoxUpdate()
        {
            var i = Start_ListBox.SelectedIndex;
            Start_ListBox.SelectedIndex = -1;
            Start_ListBox.ItemsSource = null;
            SelectedStarts.Clear();
            foreach (var s in SelectedConv.StartingList)
            {
                SelectedStarts.Add(AddOrdinal(s.Key + 1), s.Value);
            }
            Start_ListBox.ItemsSource = SelectedStarts;
            if (forcedSelectStart > -1)
            {
                Start_ListBox.SelectedIndex = forcedSelectStart;
                forcedSelectStart = -1;
                Start_ListBox.Focus();
            }
            else
            {
                Start_ListBox.SelectedIndex = i;
            }
            panToSelection = false;
        }

        private void Script_Add()
        {
            if (NamePromptDialog.Prompt(this, "Enter the new script name", "Add a script", Pcc, out NameReference result))
            {
                Pcc.FindNameOrAdd(result);
                SelectedConv.ScriptList.Add(result);
                SaveScriptsToProperties(SelectedConv);
            }
        }
        private void Script_Delete()
        {
            var cdlg = MessageBox.Show("Are you sure you want to delete this script reference?", "Confirm", MessageBoxButton.OKCancel);
            if (cdlg == MessageBoxResult.Cancel)
                return;
            var script2remove = (NameReference)Script_ListBox.SelectedItem;
            //CHECK IF ANY LINES REFERENCE THIS SCRIPT.
            bool hasreferences = SelectedConv.EntryList.Any(e => e.Script == script2remove);
            if (!hasreferences)
            {
                hasreferences = SelectedConv.ReplyList.Any(r => r.Script == script2remove);
            }

            if (hasreferences)
            {
                MessageBox.Show("There are lines that reference this script.\r\nPlease remove all references before deleting", "Warning", MessageBoxButton.OK);
                return;
            }

            SelectedConv.ScriptList.Remove(script2remove);

            SaveScriptsToProperties(SelectedConv, false);
            RecreateNodesToProperties(SelectedConv);
        }

        private DiagNode DialogueNode_SelectByIndex(int index, bool isreply = false)
        {
            if (SelectedObjects.Count > 0 && index == -1) //In this case pull up first selected object on list.
            {
                if (SelectedObjects[0] is DiagNode d)
                {
                    //Get redrawn node to keep in focus
                    var dnode = CurrentObjects.OfType<DiagNode>().FirstOrDefault(o => o.Node.NodeCount == d.Node.NodeCount && o.Node.IsReply == d.Node.IsReply);

                    if (dnode != null)
                    {
                        DialogueNode_Selected(dnode);
                        return dnode;
                    }
                }
            }
            else if (index >= 0)
            {
                var dnode = CurrentObjects.OfType<DiagNode>().FirstOrDefault(o => o.Node.NodeCount == index && o.Node.IsReply == isreply);

                if (dnode != null)
                {
                    DialogueNode_Selected(dnode);
                    return dnode;
                }
            }

            return null;
        }
        private void DialogueNode_Selected(DiagNode obj)
        {
            foreach (var oldselection in SelectedObjects)
            {
                oldselection.IsSelected = false;
            }
            SelectedObjects.ClearEx();
            obj.IsSelected = true;
            SelectedObjects.Add(obj);

            ParseNodeData(obj.Node);
            SelectedDialogueNode = obj.Node;
            SelectedDialogueNode.PropertyChanged += NodePropertyChanged;
            MirrorDialogueNode = new DialogueNodeExtended(SelectedDialogueNode);  //Setup gate

            Node_Combo_Spkr.SelectedIndex = SelectedDialogueNode.SpeakerIndex + 2;
            Node_Combo_Lstnr.SelectedIndex = SelectedDialogueNode.Listener + 3;

            Node_Combo_Spkr.IsEnabled = true; //Enable/disable boxes

            Node_CB_HideSubs.IsEnabled = false;
            Node_CB_ESkippable.IsEnabled = false;
            Node_CB_RMajor.IsEnabled = false;
            Node_CB_RDefault.IsEnabled = false;
            Node_CB_RUnskippable.IsEnabled = false;
            Node_Combo_ReplyType.IsEnabled = false;

            if (Pcc.Game == MEGame.ME3)
            {
                Node_CB_HideSubs.IsEnabled = true;
            }

            if (SelectedDialogueNode.IsReply)
            {
                Node_Text_Type.Text = "Reply Node";
                Node_Combo_Spkr.IsEnabled = false;
                Node_CB_RUnskippable.IsEnabled = true;
                Node_Combo_ReplyType.IsEnabled = true;
                if (Pcc.Game == MEGame.ME3)
                {
                    Node_CB_RMajor.IsEnabled = true;
                    Node_CB_RDefault.IsEnabled = true;
                }
            }
            else
            {
                Node_Text_Type.Text = "Entry Node";
                Node_CB_ESkippable.IsEnabled = true;
            }

            RefreshExportLoaders();

            if (SelectedDialogueNode.FiresConditional)
                Node_Text_Cnd.Text = "Conditional: ";
            else
                Node_Text_Cnd.Text = "Bool: ";


        }
        private void DialogueNode_OpenLinkEditor(object obj)
        {
            var linkEdDlg = new LinkEditor(this, SelectedObjects[0] as DiagNode)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            linkEdDlg.ShowDialog();

            if (linkEdDlg.NeedsPush)
            {
                RecreateNodesToProperties(SelectedConv);
            }

        }
        private async void DialogueNode_Add(object obj)
        {
            string command = obj as string;

            if (command == "AddReply")
            {
                PropertyCollection newprop = UnrealObjectInfo.getDefaultStructValue(Pcc.Game, "BioDialogReplyNode", true);
                var props = SelectedConv.BioConvo.GetProp<ArrayProperty<StructProperty>>("m_ReplyList") ??
                            new ArrayProperty<StructProperty>("m_ReplyList");
                //Set to needed defaults.
                newprop.AddOrReplaceProp(new EnumProperty("GUI_STYLE_NONE", "EConvGUIStyles", Pcc.Game, "eGUIStyle"));
                newprop.AddOrReplaceProp(new EnumProperty("REPLY_STANDARD", "EReplyTypes", Pcc.Game, "ReplyType"));
                newprop.GetProp<IntProperty>("nScriptIndex").Value = -1;
                newprop.GetProp<BoolProperty>("bFireConditional").Value = true;
                newprop.GetProp<IntProperty>("nConditionalFunc").Value = -1;
                newprop.GetProp<IntProperty>("nConditionalParam").Value = -1;
                newprop.GetProp<IntProperty>("nStateTransition").Value = -1;
                newprop.GetProp<IntProperty>("nStateTransitionParam").Value = -1;
                newprop.GetProp<IntProperty>("nCameraIntimacy").Value = 1;
                props.Add(new StructProperty("BioDialogReplyNode", newprop));
                SelectedConv.BioConvo.AddOrReplaceProp(props);
                PushConvoToFile(SelectedConv);
                return;
            }

            if (command == "AddEntry")
            {
                PropertyCollection newprop = UnrealObjectInfo.getDefaultStructValue(Pcc.Game, "BioDialogEntryNode", true);
                var props = SelectedConv.BioConvo.GetProp<ArrayProperty<StructProperty>>("m_EntryList") ??
                            new ArrayProperty<StructProperty>("m_EntryList");
                var EConvGUIStyles = new EnumProperty("GUI_STYLE_NONE", "EConvGUIStyles", Pcc.Game, "eGUIStyle");
                newprop.AddOrReplaceProp(EConvGUIStyles);
                newprop.GetProp<IntProperty>("nSpeakerIndex").Value = -1;
                newprop.GetProp<IntProperty>("nScriptIndex").Value = -1;
                newprop.GetProp<BoolProperty>("bFireConditional").Value = true;
                newprop.GetProp<IntProperty>("nConditionalFunc").Value = -1;
                newprop.GetProp<IntProperty>("nConditionalParam").Value = -1;
                newprop.GetProp<IntProperty>("nStateTransition").Value = -1;
                newprop.GetProp<IntProperty>("nStateTransitionParam").Value = -1;
                newprop.GetProp<IntProperty>("nCameraIntimacy").Value = 1;
                props.Add(new StructProperty("BioDialogEntryNode", newprop));
                SelectedConv.BioConvo.AddOrReplaceProp(props);
                PushConvoToFile(SelectedConv);
                return;
            }

            if (command == "CloneReply")
            {
                int newIndex = SelectedConv.ReplyList.Count;
                SelectedConv.ReplyList.Add(new DialogueNodeExtended(SelectedDialogueNode) { NodeCount = newIndex });
                NoUIRefresh = true;
                RecreateNodesToProperties(SelectedConv);
                bool p = true;
                while (p)
                {
                    p = await CheckProcess(100, NoUIRefresh, false);
                }
                panToSelection = false;
                graphEditor.Enabled = false;
                graphEditor.UseWaitCursor = true;
                GenerateGraph();
                DiagNode node = DialogueNode_SelectByIndex(newIndex, true);
                DialogueNode_DeleteLinks(node);
                graphEditor.Enabled = true;
                graphEditor.UseWaitCursor = false;
                graphEditor.Camera.AnimateViewToCenterBounds(node.GlobalFullBounds, false, 1000);
                graphEditor.Refresh();
                return;
            }

            if (command == "CloneEntry")
            {
                int newIndex = SelectedConv.EntryList.Count;
                SelectedConv.EntryList.Add(new DialogueNodeExtended(SelectedDialogueNode) { NodeCount = newIndex });
                NoUIRefresh = true;
                RecreateNodesToProperties(SelectedConv);
                bool p = true;
                while (p)
                {
                    p = await CheckProcess(100, NoUIRefresh, false);
                }
                panToSelection = false;
                graphEditor.Enabled = false;
                graphEditor.UseWaitCursor = true;
                GenerateGraph();
                DiagNode node = DialogueNode_SelectByIndex(newIndex, false);
                DialogueNode_DeleteLinks(node);
                graphEditor.Enabled = true;
                graphEditor.UseWaitCursor = false;
                graphEditor.Camera.AnimateViewToCenterBounds(node.GlobalFullBounds, false, 1000);
                graphEditor.Refresh();
            }
        }
        private void DialogueNode_DeleteLinks(object obj)
        {
            if (SelectedDialogueNode.IsReply)
            {
                var entrylinklist = SelectedDialogueNode.NodeProp.GetProp<ArrayProperty<IntProperty>>("EntryList");
                if (entrylinklist != null)
                {
                    entrylinklist.Clear();
                    RecreateNodesToProperties(SelectedConv);
                }
            }
            else
            {
                var replylinklist = SelectedDialogueNode.NodeProp.GetProp<ArrayProperty<StructProperty>>("ReplyListNew");
                if (replylinklist != null)
                {
                    replylinklist.Clear();
                    RecreateNodesToProperties(SelectedConv);
                }
            }
        }
        private void DialogueNode_Delete(object obj)
        {
            //Warn
            var wdlg = MessageBox.Show("Do you want to remove this dialogue node?", "Warning", MessageBoxButton.OKCancel);
            if (wdlg == MessageBoxResult.Cancel)
                return;

            if (SelectedDialogueNode.IsReply == false && SelectedConv.EntryList.Count <= 1)
            {
                MessageBox.Show("Each conversation must have a minimum of one entry node.", "Warning", MessageBoxButton.OK);
                return;
            }

            var deleteNode = SelectedDialogueNode;
            SelectedDialogueNode = null;
            SelectedObjects.ClearEx();
            int deleteID = deleteNode.NodeCount;
            if (deleteNode.IsReply)
            {
                foreach (var entry in SelectedConv.EntryList)
                {
                    var newReplyLinksProp = new ArrayProperty<StructProperty>("ReplyListNew");
                    var oldReplyLinksProp = entry.NodeProp.GetProp<ArrayProperty<StructProperty>>("ReplyListNew");
                    if (oldReplyLinksProp != null)
                    {
                        foreach (var link in oldReplyLinksProp)
                        {
                            var linkval = link.GetProp<IntProperty>("nIndex").Value;
                            if (linkval != deleteID)
                            {
                                if (linkval > deleteID)
                                {
                                    linkval -= 1;

                                }
                                var newip = new IntProperty(linkval, "nIndex");
                                link.Properties.AddOrReplaceProp(newip);
                                newReplyLinksProp.Add(link);
                            }
                        }
                    }

                    entry.NodeProp.Properties.AddOrReplaceProp(newReplyLinksProp);
                }
                SelectedConv.ReplyList.RemoveAt(deleteID);
            }
            else
            {

                foreach (var reply in SelectedConv.ReplyList)
                {
                    var oldEntryLinksProp = reply.NodeProp.GetProp<ArrayProperty<IntProperty>>("EntryList");
                    var newEntryLinksProp = new ArrayProperty<IntProperty>("EntryList");
                    if (oldEntryLinksProp != null)
                    {
                        foreach (var r in oldEntryLinksProp)
                        {
                            if (r.Value != deleteID)
                            {
                                if (r.Value > deleteID)
                                {
                                    r.Value -= 1;
                                }
                                newEntryLinksProp.Add(r);
                            }
                        }
                    }
                    reply.NodeProp.Properties.AddOrReplaceProp(newEntryLinksProp);
                }

                var newStartList = new SortedDictionary<int, int>();
                foreach ((int key, int val) in SelectedConv.StartingList)
                {
                    if (val > deleteID)
                    {
                        newStartList.Add(key, val - 1);
                    }
                    else if (val < deleteID)
                    {
                        newStartList.Add(key, val);
                    }
                }
                SelectedConv.StartingList.Clear();
                foreach (var ns in newStartList)
                {
                    SelectedConv.StartingList.Add(ns.Key, ns.Value);
                }

                SelectedConv.EntryList.RemoveAt(deleteID);
            }
            RecreateNodesToProperties(SelectedConv);
        }

        private void StageDirections_TextChanged(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                var tbox = (TextBox)sender;
                Keyboard.ClearFocus();
                var be = tbox.GetBindingExpression(TextBox.TextProperty);
                switch (e.Key)
                {
                    case Key.Enter:
                        be?.UpdateSource();
                        SaveStageDirectionsToProperties(SelectedConv);
                        break;
                    case Key.Escape:
                        be?.UpdateTarget();
                        break;
                }
            }
        }
        private void StageDirections_Modify(object obj)
        {
            string command = obj as string;
            if (command == "Add")
            {
                int strRef = 0;
                bool isNumber = false;
                while (!isNumber)
                {
                    var sdlg = new PromptDialog("Enter the TLK String Reference for the direction:", "Add a Stage Direction", "0");
                    sdlg.ShowDialog();
                    if (sdlg.ResponseText == null || sdlg.ResponseText == "0")
                        return;
                    isNumber = int.TryParse(sdlg.ResponseText, out strRef);
                    if (!isNumber || strRef <= 0)
                    {
                        var wdlg = MessageBox.Show("The string reference must be a positive whole number.", "Dialogue Editor", MessageBoxButton.OKCancel);
                        if (wdlg == MessageBoxResult.Cancel)
                            return;
                    }
                }

                SelectedConv.StageDirections.Add(new StageDirection(strRef, GlobalFindStrRefbyID(strRef, Pcc), "Add direction"));
                SaveStageDirectionsToProperties(SelectedConv);

            }
            else if (command == "Delete" && StageDirs_ListBox.SelectedIndex >= 0)
            {
                SelectedConv.StageDirections.RemoveAt(StageDirs_ListBox.SelectedIndex);
                SaveStageDirectionsToProperties(SelectedConv);
            }

        }
        #endregion

        #region UIHandling-graph

        private void node_Click(object sender, PInputEventArgs e)
        {
            if (e.Shift && e.PickedNode is DObj dObj)
            {
                dObj.IsSelected = true;
                SelectedObjects.Add(dObj);
            }
            else if (sender is DiagNode obj)
            {
                SetUIMode(2, false);
                if (e.Button != System.Windows.Forms.MouseButtons.Left && obj.GlobalFullBounds == obj.posAtDragStart)
                {
                    if (!e.Shift && !e.Control)
                    {
                        if (SelectedObjects.Count == 1 && obj.IsSelected) return;
                        panToSelection = false;
                        if (SelectedObjects.Count > 1)
                        {
                            panToSelection = false;
                        }
                    }
                }
                else
                {
                    DialogueNode_Selected(obj);
                }
            }
            else if (sender is DStart start)
            {
                foreach (var oldselection in SelectedObjects)
                {
                    oldselection.IsSelected = false;
                }
                SelectedObjects.ClearEx();
                start.IsSelected = true;
                SelectedObjects.Add(start);

                Start_ListBox.SelectedIndex = start.Order;
                SetUIMode(3, false);
            }

        }
        private void graphEditor_Click(object sender, EventArgs e)
        {
            graphEditor.Focus();
        }
        private void graphEditor_PanTo()
        {
            var PanObjects = new ObservableCollectionExtended<DObj>();
            PanObjects.AddRange(CurrentObjects.Take(5));

            if (PanObjects.Any())
            {
                if (panToSelection)
                {
                    if (PanObjects.Count == 1)
                    {
                        graphEditor.Camera.AnimateViewToCenterBounds(PanObjects[0].GlobalFullBounds, false, 100);
                    }
                    else
                    {
                        RectangleF boundingBox = PanObjects.Select(obj => obj.GlobalFullBounds).BoundingRect();
                        graphEditor.Camera.AnimateViewToCenterBounds(boundingBox, true, 200);
                    }
                }
            }

            panToSelection = true;
            graphEditor.Refresh();
        }
        private void DialogueEditor_DragEnter(object sender, System.Windows.Forms.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.Forms.DataFormats.FileDrop))
                e.Effect = System.Windows.Forms.DragDropEffects.All;
            else
                e.Effect = System.Windows.Forms.DragDropEffects.None;
        }
        private void DialogueEditor_DragDrop(object sender, System.Windows.Forms.DragEventArgs e)
        {
            if (e.Data.GetData(System.Windows.Forms.DataFormats.FileDrop) is string[] DroppedFiles)
            {
                if (DroppedFiles.Any())
                {
                    LoadFile(DroppedFiles[0]);
                }
            }
        }
        private void saveView(bool toFile = true)
        {
            if (CurrentObjects.Count == 0)
                return;
            SavedPositions = new List<SaveData>();
            foreach (DObj obj in CurrentObjects)
            {
                if (obj.Pickable)
                {
                    SavedPositions.Add(new SaveData
                    {
                        index = obj.NodeUID,
                        X = obj.X + obj.Offset.X,
                        Y = obj.Y + obj.Offset.Y
                    });
                }
            }

            SavedPositions.AddRange(extraSaveData);
            extraSaveData.Clear();

            if (toFile)
            {
                string outputFile = JsonConvert.SerializeObject(SavedPositions);
                if (!Directory.Exists(Path.GetDirectoryName(JSONpath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(JSONpath));
                File.WriteAllText(JSONpath, outputFile);
                SavedPositions.Clear();
            }
        }
        protected void node_MouseDown(object sender, PInputEventArgs e)
        {
            if (sender is DObj obj)
            {
                obj.posAtDragStart = obj.GlobalFullBounds;
                if (e.Button == System.Windows.Forms.MouseButtons.Right)
                {
                    panToSelection = false;
                    OpenNodeContextMenu(obj);
                }
                else if (e.Shift || e.Control)
                {
                    panToSelection = false;

                }
                else if (!obj.IsSelected)
                {
                    foreach (var oldselection in SelectedObjects)
                    {
                        oldselection.IsSelected = false;
                    }
                    SelectedObjects.ClearEx();
                    panToSelection = false;
                }
            }
        }


        private void backMouseDown_Handler(object sender, PInputEventArgs e)
        {
            if (!(e.PickedNode is PCamera) || SelectedConv == null) return;

            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {

            }

        }
        private void back_MouseUp(object sender, PInputEventArgs e)
        {
            //var nodesToSelect = graphEditor.OfType<DObj>();
            //foreach (DObj DObj in nodesToSelect)
            //{
            //    panToSelection = false;
            //    .SelectedItems.Add(DObj);
            //}
        }

        #endregion UIHandling-graph

        #region UIHandling-menus
        public void OpenNodeContextMenu(DObj obj)
        {
            if (obj is DStart dStart)
            {
                if (FindResource("startnodeContextMenu") is ContextMenu contextMenu)
                {
                    foreach (var oldselection in SelectedObjects)
                    {
                        oldselection.IsSelected = false;
                    }
                    SelectedObjects.ClearEx();
                    dStart.IsSelected = true;
                    SelectedObjects.Add(dStart);

                    Start_ListBox.SelectedIndex = dStart.Order;
                    SetUIMode(3, false);
                    contextMenu.DataContext = this;
                    contextMenu.IsOpen = true;
                    graphEditor.DisableDragging();

                }

            }
            else if (obj is DiagNodeReply dreply)
            {
                if (FindResource("replynodeContextMenu") is ContextMenu contextMenu)
                {
                    if (contextMenu.GetChild("replyLinkEditContextMenu") is MenuItem editHeader)
                    {
                        editHeader.Background = new System.Windows.Media.SolidColorBrush(DObj.replyColor.ToWPFColor());
                    }
                    if (dreply.Outlinks.Any()
                     && contextMenu.GetChild("breakLinksMenuItem") is MenuItem breakLinksMenuItem)
                    {
                        bool hasLinks = false;
                        if (breakLinksMenuItem.GetChild("outputLinksMenuItem") is MenuItem outputLinksMenuItem)
                        {
                            outputLinksMenuItem.Visibility = Visibility.Collapsed;
                            outputLinksMenuItem.Items.Clear();
                            for (int i = 0; i < dreply.Outlinks.Count; i++)
                            {
                                for (int j = 0; j < dreply.Outlinks[i].Links.Count; j++)
                                {
                                    outputLinksMenuItem.Visibility = Visibility.Visible;
                                    hasLinks = true;
                                    var temp = new MenuItem
                                    {
                                        Header = $"Break link from R{dreply.NodeID - 1000} to E{dreply.Outlinks[i].Links[j]}"
                                    };
                                    int linkConnection = i;
                                    int linkIndex = j;
                                    temp.Click += (o, args) => { dreply.RemoveOutlink(linkConnection, linkIndex); };
                                    outputLinksMenuItem.Items.Add(temp);
                                }
                            }
                        }

                        if (breakLinksMenuItem.GetChild("breakAllLinksMenuItem") is MenuItem breakAllLinksMenuItem)
                        {
                            if (hasLinks)
                            {
                                breakLinksMenuItem.Visibility = Visibility.Visible;
                                breakAllLinksMenuItem.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                breakLinksMenuItem.Visibility = Visibility.Collapsed;
                                breakAllLinksMenuItem.Visibility = Visibility.Collapsed;
                            }
                        }
                    }
                    DialogueNode_Selected(dreply);
                    SetUIMode(2, false);
                    contextMenu.DataContext = this;
                    contextMenu.IsOpen = true;
                    graphEditor.DisableDragging();
                }
            }
            else if (obj is DiagNodeEntry dentry)
            {
                if (FindResource("entrynodeContextMenu") is ContextMenu contextMenu)
                {
                    if (contextMenu.GetChild("entryLinkEditContextMenu") is MenuItem editHeader)
                    {
                        editHeader.Background = new System.Windows.Media.SolidColorBrush(DObj.entryColor.ToWPFColor());
                    }

                    if (dentry.Outlinks.Any()
                     && contextMenu.GetChild("ebreakLinksMenuItem") is MenuItem breakLinksMenuItem)
                    {
                        bool hasLinks = false;
                        if (breakLinksMenuItem.GetChild("eoutputLinksMenuItem") is MenuItem outputLinksMenuItem)
                        {
                            outputLinksMenuItem.Visibility = Visibility.Collapsed;
                            outputLinksMenuItem.Items.Clear();
                            for (int i = 0; i < dentry.Outlinks.Count; i++)
                            {
                                for (int j = 0; j < dentry.Outlinks[i].Links.Count; j++)
                                {
                                    outputLinksMenuItem.Visibility = Visibility.Visible;
                                    hasLinks = true;
                                    var temp = new MenuItem
                                    {
                                        Header = $"Break link from E{dentry.NodeID} to R{dentry.Outlinks[i].Links[j] - 1000}"
                                    };
                                    int linkConnection = i;
                                    int linkIndex = j;
                                    temp.Click += (o, args) => { dentry.RemoveOutlink(linkConnection, linkIndex); };
                                    outputLinksMenuItem.Items.Add(temp);
                                }
                            }
                        }

                        if (breakLinksMenuItem.GetChild("ebreakAllLinksMenuItem") is MenuItem breakAllLinksMenuItem)
                        {
                            if (hasLinks)
                            {
                                breakLinksMenuItem.Visibility = Visibility.Visible;
                                breakAllLinksMenuItem.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                breakLinksMenuItem.Visibility = Visibility.Collapsed;
                                breakAllLinksMenuItem.Visibility = Visibility.Collapsed;
                            }
                        }
                    }
                    DialogueNode_Selected(dentry);
                    SetUIMode(2, false);
                    contextMenu.DataContext = this;
                    contextMenu.IsOpen = true;
                    graphEditor.DisableDragging();
                }
            }
        }
        private void ContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            graphEditor.AllowDragging();
            Focus(); //this will make window bindings work, as context menu is not part of the visual tree, and focus will be on there if the user clicked it.
        }
        private void GotoBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems != e.RemovedItems)
            {
                if (GotoBox.SelectedItem is DiagNode dnode)
                {
                    DialogueNode_Selected(dnode);

                }
                if (GotoBox.SelectedItem is DObj o)
                {
                    graphEditor.Camera.AnimateViewToCenterBounds(o.GlobalFullBounds, false, 100);
                    graphEditor.Refresh();
                }
            }
        }
        private void UpdateLayoutDefaults(object obj)
        {
            string command = obj as string;
            bool needsRegen = false;
            bool forceRegen = false;
            switch (command)
            {
                case "Lay_Manual":
                    SaveViewMode = 1;
                    break;
                case "Lay_AutoSave":
                    SaveViewMode = 0;
                    if (CurrentObjects.Any())
                    {
                        SetupConvJSON(SelectedConv.Export);
                    }
                    break;
                case "Lay_AutoGen":
                    SaveViewMode = 2;
                    break;
                case "Auto_Column":
                    LayoutMode = 0;
                    needsRegen = true;
                    break;
                case "Auto_Waterfall":
                    LayoutMode = 1;
                    needsRegen = true;
                    break;
                case "Auto_AdvColumn":
                    LayoutMode = 2;
                    needsRegen = true;
                    break;
                case "Toggle_Output":
                    DObj.OutputNumbers = HideEntryOutput_MenuItem.IsChecked;
                    forceRegen = true;
                    break;
                case "Toggle_LineAtTop":
                    DBox.LinesAtTop = ShowLinesOnTop_MenuItem.IsChecked;
                    forceRegen = true;
                    break;
                default:
                    break;
            }
            ManualSaveView_MenuItem.IsChecked = false;
            AutoGenView_MenuItem.IsChecked = false;
            AutoSaveView_MenuItem.IsChecked = false;
            Waterfall_MenuItem.IsChecked = false;
            Column_MenuItem.IsChecked = false;
            AdvColumn_MenuItem.IsChecked = false;
            switch (SaveViewMode)
            {
                case 1:
                    ManualSaveView_MenuItem.IsChecked = true;
                    break;
                case 2:
                    AutoGenView_MenuItem.IsChecked = true;
                    break;
                default: //in case non valid reset
                    AutoSaveView_MenuItem.IsChecked = true;
                    SaveViewMode = 0;
                    break;
            }
            switch (LayoutMode)
            {
                case 1:
                    Waterfall_MenuItem.IsChecked = true;
                    break;
                case 2:
                    AdvColumn_MenuItem.IsChecked = true;
                    break;
                default: //in case non valid reset
                    Column_MenuItem.IsChecked = true;
                    LayoutMode = 0;
                    break;
            }
            DBox.LinesAtTop = ShowLinesOnTop_MenuItem.IsChecked; 
            DObj.OutputNumbers = HideEntryOutput_MenuItem.IsChecked; 

            if (CurrentObjects.Any() && ((needsRegen && SaveViewMode == 2) || forceRegen))
            {
                RefreshView();
            }
        }
        private void GenderTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(e.Source is TabControl tabctrl)
            {
                soundPanelTabControl.SelectedIndex = tabctrl.SelectedIndex;
                faceFXEditorTabControl.SelectedIndex = tabctrl.SelectedIndex;
            }
        }
        private void TestPaths()
        {
            if (AutoGenerateSpeakerArrays(SelectedConv))
            {
                MessageBox.Show("There are possible looping pathways to this conversation.\r\nThis can be a problem unless the player has control of the loop via choices.", "Dialogue Editor");
            }
            else
            {
                MessageBox.Show("No looping paths in the conversation.", "Dialogue Editor");
            }
        }

        //TEMPORARY UNTIL NEW BUILD
        private void OpenInInterpViewer_Clicked(ExportEntry exportEntry)
        {

            var p = new InterpEditor();
            p.Show();
            p.LoadPCC(Pcc.FilePath);
            if (exportEntry.ObjectName == "InterpData")
            {
                p.toolStripComboBox1.SelectedIndex = p.objects.IndexOf(exportEntry.Index);
                p.loadInterpData(exportEntry.Index);
            }
            else
            {
                ////int i = ((DiagNode)obj).Varlinks[0].Links[0] - 1; //0-based index because Interp Viewer is old
                //p.toolStripComboBox1.SelectedIndex = p.objects.IndexOf(i);
                //p.loadInterpData(i);
            }

        }


        private void OpenInAction(object obj)
        {

            string tool = obj as string;
            switch (tool)
            {
                case "PackEdLvl":
                    OpenInToolkit("PackageEditor", 0, Level);
                    break;
                case "PackEdNode":
                    OpenInToolkit("PackageEditor", SelectedConv.ExportUID);
                    break;
                case "PackEdLine":
                    OpenInToolkit("PackageEditor", SelectedDialogueNode.Interpdata.UIndex);
                    break;
                case "SeqEdLvl":
                    OpenInToolkit("SequenceEditor", 0, Level);
                    break;
                case "SeqEdNode":
                    if (SelectedConv.Sequence.UIndex < 0)
                    {
                        OpenInToolkit("SequenceEditor", 0, Level);
                    }
                    else
                    {
                        OpenInToolkit("SequenceEditor", SelectedConv.Sequence.UIndex);
                    }
                    break;
                case "SeqEdLine":
                    OpenInToolkit("SequenceEditor", SelectedDialogueNode.Interpdata.UIndex);
                    break;
                case "FaceFXNS":
                    OpenInToolkit("FaceFXEditor", SelectedConv.NonSpkrFFX.UIndex);
                    break;
                case "FaceFXSpkrM":
                    if (Pcc.isUImport(SelectedSpeaker.FaceFX_Male.UIndex))
                    {
                        OpenInToolkit("FaceFXEditor", 0, Level); //CAN SEND TO THE CORRECT EXPORT IN THE NEW FILE LOAD?
                    }
                    else
                    {
                        OpenInToolkit("FaceFXEditor", SelectedSpeaker.FaceFX_Male.UIndex);
                    }
                    break;
                case "FaceFXSpkrF":
                    if (Pcc.isUImport(SelectedSpeaker.FaceFX_Female.UIndex))
                    {
                        OpenInToolkit("FaceFXEditor", 0, Level);
                    }
                    else
                    {
                        OpenInToolkit("FaceFXEditor", SelectedSpeaker.FaceFX_Female.UIndex);
                    }
                    break;
                case "FaceFXLineM":
                    OpenInToolkit("FaceFXEditor", SelectedDialogueNode.SpeakerTag.FaceFX_Male.UIndex, null, SelectedDialogueNode.FaceFX_Male);
                    break;
                case "FaceFXLineF":
                    OpenInToolkit("FaceFXEditor", SelectedDialogueNode.SpeakerTag.FaceFX_Female.UIndex, null, SelectedDialogueNode.FaceFX_Female);
                    break;
                case "SoundP_Bank":
                    if (SelectedConv.WwiseBank != null)
                    {
                        OpenInToolkit("SoundplorerWPF", SelectedConv.WwiseBank.UIndex);
                    }
                    break;
                case "SoundP_StreamM":
                    if (SelectedDialogueNode.WwiseStream_Male != null)
                    {
                        OpenInToolkit("SoundplorerWPF", SelectedDialogueNode.WwiseStream_Male.UIndex);
                    }
                    break;
                case "SoundP_StreamF":
                    if (SelectedDialogueNode.WwiseStream_Female != null)
                    {
                        OpenInToolkit("SoundplorerWPF", SelectedDialogueNode.WwiseStream_Female.UIndex);
                    }
                    break;
                case "InterpEdLine":
                    if (SelectedDialogueNode.Interpdata != null)
                    {
                        OpenInInterpViewer_Clicked(SelectedDialogueNode.Interpdata);
                    }
                    break;
                default:
                    OpenInToolkit(tool);
                    break;
            }
        }
        private void OpenInToolkit(string tool, int export = 0, string filename = null, string param = null)
        {
            string filePath = null;
            if (filename != null)  //If file is a new loaded file need to find path.
            {
                filePath = Path.Combine(Path.GetDirectoryName(Pcc.FilePath), filename);

                if (!File.Exists(filePath))
                {
                    string rootPath = null;
                    switch (Pcc.Game)
                    {
                        case MEGame.ME1:
                            rootPath = ME1Directory.gamePath;
                            break;
                        case MEGame.ME2:
                            rootPath = ME2Directory.gamePath;
                            break;
                        case MEGame.ME3:
                            rootPath = ME3Directory.gamePath;
                            break;
                    }
                    filePath = Directory.GetFiles(rootPath, Level, SearchOption.AllDirectories).FirstOrDefault();
                    if (filePath == null)
                    {
                        MessageBox.Show($"File {filename} not found.");
                        return;
                    }
                    var dlg = MessageBox.Show($"Opening level at {filePath}", "Dialogue Editor", MessageBoxButton.OKCancel);
                    if (dlg == MessageBoxResult.Cancel)
                    {
                        return;
                    }
                }
            }

            if (filePath == null)
            {
                filePath = Pcc.FilePath;
            }

            switch (tool)
            {

                case "FaceFXEditor":
                    if (Pcc.isUExport(export) && param != null)
                    {
                        new FaceFX.FaceFXEditor(Pcc.getUExport(export), param).Show();
                    }
                    else if (Pcc.isUExport(export))
                    {
                        new FaceFX.FaceFXEditor(Pcc.getUExport(export)).Show();
                    }
                    else
                    {
                        var facefxEditor = new FaceFX.FaceFXEditor();
                        facefxEditor.LoadFile(filePath);
                        facefxEditor.Show();
                    }
                    break;
                case "PackageEditor":
                    var packEditor = new PackageEditorWPF();
                    packEditor.Show();
                    if (Pcc.isUExport(export))
                    {
                        packEditor.LoadFile(Pcc.FilePath, export);
                    }
                    else
                    {
                        packEditor.LoadFile(filePath);
                    }
                    break;
                case "SoundplorerWPF":
                    var soundplorerWPF = new Soundplorer.SoundplorerWPF();
                    soundplorerWPF.LoadFile(Pcc.FilePath);
                    soundplorerWPF.Show();
                    if (Pcc.isUExport(export))
                    {
                        soundplorerWPF.soundPanel.LoadExport(Pcc.getUExport(export));
                    }
                    break;
                case "SequenceEditor":
                    if (Pcc.isUExport(export))
                    {
                        new SequenceEditorWPF(Pcc.getUExport(export)).Show();
                    }
                    else
                    {
                        var seqEditor = new SequenceEditorWPF();
                        seqEditor.LoadFile(filePath);
                        seqEditor.Show();
                    }
                    break;
            }
        }

        private void GoToBoxOpen()
        {
            if (!GotoBox.IsDropDownOpen)
            {
                GotoBox.IsDropDownOpen = true;
                Keyboard.Focus(GotoBox);
            }
            else
            {
                GotoBox.IsDropDownOpen = false;
            }

        }
        private static void LoadTLKManager()
        {
            if (!Application.Current.Windows.OfType<TlkManagerNS.TLKManagerWPF>().Any())
            {
                TlkManagerNS.TLKManagerWPF m = new TlkManagerNS.TLKManagerWPF();
                m.Show();
            }
            else
            {
                Application.Current.Windows.OfType<TlkManagerNS.TLKManagerWPF>().First().Focus();
            }
        }
        private void SaveImage()
        {
            if (CurrentObjects.Count == 0)
                return;
            string objectName = Regex.Replace(CurrentLoadedExport.ObjectName, @"[<>:""/\\|?*]", "");
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
                MessageBox.Show("Done.");
            }
        }
        private void ChangeLineSize(object obj)
        {
            if (!(obj is string cmd))
            {
                var options = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(OptionsPath));
                cmd = ((double)options["LineTextSize"] * 10).ToString();
            }
            Menu_LineSize_00.IsChecked = false;
            Menu_LineSize_15.IsChecked = false;
            Menu_LineSize_20.IsChecked = false;
            Menu_LineSize_10.IsChecked = false;
            switch (cmd)
            {
                case "00":
                    Menu_LineSize_00.IsChecked = true;
                    DBox.LineScaleOption = 0f;
                    break;
                case "15":
                    Menu_LineSize_15.IsChecked = true;
                    DBox.LineScaleOption = 1.5f;
                    break;
                case "20":
                    Menu_LineSize_20.IsChecked = true;
                    DBox.LineScaleOption = 2.0f;
                    break;
                default:
                    DBox.LineScaleOption = 1.0f;
                    Menu_LineSize_10.IsChecked = true;
                    break;
            }
            RefreshView();
        }
        private void ChangeLineColor(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            var source = (Xceed.Wpf.Toolkit.ColorPicker)sender;
            var newcolor = e.NewValue.Value;
            switch (source.Name)
            {
                case "ClrPcker_Line":
                    DBox.lineColor = newcolor.ToWinformsColor();
                    break;
                case "ClrPcker_ParaInt":
                    DObj.paraintColor = newcolor.ToWinformsColor();;
                    break;
                case "ClrPcker_RenInt":
                    DObj.renintColor = newcolor.ToWinformsColor();;
                    break;
                case "ClrPcker_Agree":
                    DObj.agreeColor = newcolor.ToWinformsColor();;
                    break;
                case "ClrPcker_Disagree":
                    DObj.disagreeColor = newcolor.ToWinformsColor();;
                    break;
                case "ClrPcker_Friendly":
                    DObj.friendlyColor = newcolor.ToWinformsColor();;
                    break;
                case "ClrPcker_Hostile":
                    DObj.hostileColor = newcolor.ToWinformsColor();;
                    break;
                case "ClrPcker_EntryPen":
                    DObj.entryPenColor = newcolor.ToWinformsColor();;
                    break;
                case "ClrPcker_ReplyPen":
                    DObj.replyPenColor = newcolor.ToWinformsColor();;
                    break;
                case "ClrPcker_Entry":
                    DObj.entryColor = newcolor.ToWinformsColor();;
                    break;
                case "ClrPcker_Reply":
                    DObj.replyColor = newcolor.ToWinformsColor();;
                    break;
            }

            RefreshView();
        }
        private void ResetColorsToDefault()
        {
            var cdlg = MessageBox.Show("Do you wish to reset the color scheme?", "Dialogue Editor", MessageBoxButton.OKCancel);
            if (cdlg == MessageBoxResult.Cancel)
                return;

            DBox.lineColor = Color.FromArgb(74, 63, 190);
            DObj.paraintColor = Color.Blue; ;//Strong Blue
            DObj.renintColor = Color.Red;//Strong Red
            DObj.agreeColor = Color.DodgerBlue;
            DObj.disagreeColor = Color.Tomato;
            DObj.friendlyColor = Color.FromArgb(3, 3, 116);//dark blue
            DObj.hostileColor = Color.FromArgb(116, 3, 3);//dark red
            DObj.entryColor = Color.DarkGoldenrod;
            DObj.entryPenColor = Color.Black;
            DObj.replyColor = Color.CadetBlue;
            DObj.replyPenColor = Color.Black;
            ClrPcker_Line.SelectedColor = DBox.lineColor.ToWPFColor();
            ClrPcker_ParaInt.SelectedColor = DObj.paraintColor.ToWPFColor();
            ClrPcker_RenInt.SelectedColor = DObj.renintColor.ToWPFColor();
            ClrPcker_Agree.SelectedColor = DObj.agreeColor.ToWPFColor();
            ClrPcker_Disagree.SelectedColor = DObj.disagreeColor.ToWPFColor();
            ClrPcker_Friendly.SelectedColor = DObj.friendlyColor.ToWPFColor();
            ClrPcker_Hostile.SelectedColor = DObj.hostileColor.ToWPFColor();
            ClrPcker_Entry.SelectedColor = DObj.entryColor.ToWPFColor();
            ClrPcker_EntryPen.SelectedColor = DObj.entryPenColor.ToWPFColor();
            ClrPcker_Reply.SelectedColor = DObj.replyColor.ToWPFColor();
            ClrPcker_ReplyPen.SelectedColor = DObj.replyPenColor.ToWPFColor();
        }

        #endregion

        #region Helpers
        public static string AddOrdinal(int num)
        {
            if (num <= 0) return num.ToString();
            switch (num % 100)
            {
                case 11:
                case 12:
                case 13:
                    return num + "th";
            }
            switch (num % 10)
            {
                case 1:
                    return num + "st";
                case 2:
                    return num + "nd";
                case 3:
                    return num + "rd";
                default:
                    return num + "th";
            }
        }
        /// <summary>
        /// Wait for bool condition to switch to false. Used for async delay. Await until awaitforfalse and awaitfortrue are synchronised or straight delay.
        /// </summary>
        /// <param name="waitforfalse">condition</param>
        /// <param name="waitfortrue">condition</param>
        /// <param name="delay">Delay in milliseconds.</param>
        /// <returns></returns>
        public async Task<bool> CheckProcess(int delay, bool waitforfalse = false, bool waitfortrue = true)
        {
            if (waitforfalse == waitfortrue)
            {
                return false;
            }

            await Task.Delay(new TimeSpan(0, 0, 0, 0, delay));
            return true;
        }




        #endregion Helpers

    }
}