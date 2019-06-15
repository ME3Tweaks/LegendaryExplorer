using Gammtek.Conduit.Extensions.Collections.Generic;
using ME3Explorer;
using ME3Explorer.Packages;
using ME3Explorer.Sequence_Editor;
using ME3Explorer.SharedUI;
using ME3Explorer.SharedUI.PeregrineTreeView;
using ME3Explorer.Unreal;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Nodes;
using static ME3Explorer.Dialogue_Editor.BioConversationExtended;
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

        private readonly ConvGraphEditor graphEditor;
        public ObservableCollectionExtended<IEntry> FFXAnimsets { get; } = new ObservableCollectionExtended<IEntry>();
        public ObservableCollectionExtended<ConversationExtended> Conversations { get; } = new ObservableCollectionExtended<ConversationExtended>();
        public PropertyCollection CurrentConvoProperties;
        public IExportEntry CurrentLoadedExport;
        public IMEPackage CurrentConvoPackage;
        public ObservableCollectionExtended<SpeakerExtended> SelectedSpeakerList { get; } = new ObservableCollectionExtended<SpeakerExtended>();
        public ObservableCollectionExtended<SpeakerExtended> ListenersList { get; } = new ObservableCollectionExtended<SpeakerExtended>();
        private DialogueNodeExtended _SelectedDialogueNode;
        public DialogueNodeExtended SelectedDialogueNode
        {
            get => _SelectedDialogueNode;
            set
            {
                if (value != _SelectedDialogueNode)
                    SetProperty(ref _SelectedDialogueNode, value);
            }
        }
        private DialogueNodeExtended MirrorDialogueNode;
        private Boolean IsLocalUpdate = false; //Used to prevent uneccessary UI updates.
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
            set
            {
                if (value != _SelectedStarts)
                    SetProperty(ref _SelectedStarts, value);
            }
        }
        private int forcedSelectStart = -1;
        private string _SelectedScript = "None";
        public string SelectedScript
        {
            get => _SelectedScript;
            set
            {
                if (value != _SelectedScript)
                    SetProperty(ref _SelectedScript, value);
            }
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
        private static BackgroundWorker BackParser = new BackgroundWorker();
        private bool NoUIRefresh; //stops graph refresh on update.
        // FOR GRAPHING
        public ObservableCollectionExtended<DObj> CurrentObjects { get; } = new ObservableCollectionExtended<DObj>();
        public ObservableCollectionExtended<DObj> SelectedObjects { get; } = new ObservableCollectionExtended<DObj>();
        private readonly List<SaveData> extraSaveData = new List<SaveData>();
        private bool panToSelection = true;
        private string FileQueuedForLoad;
        private IExportEntry ExportQueuedForFocusing;
        public string CurrentFile;
        public string JSONpath;
        private List<SaveData> SavedPositions;
        public bool RefOrRefChild;

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
        private bool HasWwbank(object param)
        {
            return SelectedConv != null && SelectedConv.WwiseBank != null;
        }
        private bool HasFFXNS(object param)
        {
            return SelectedConv != null && SelectedConv.NonSpkrFFX != null;
        }
        private bool SpkrCanMoveUp(object param)
        {
            return SelectedSpeaker != null && SelectedSpeaker.SpeakerID > 0;
        }
        private bool SpkrCanMoveDown(object param)
        {
            return SelectedSpeaker != null && SelectedSpeaker.SpeakerID >= 0 && (SelectedSpeaker.SpeakerID + 3) < SelectedSpeakerList.Count;
        }
        private bool HasActiveSpkr()
        {
            return Speakers_ListBox.SelectedIndex >= 2;
        }
        private bool LineHasInterpdata(object param)
        {
            return SelectedDialogueNode != null && SelectedDialogueNode.Interpdata != null;
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

            Node_Combo_GUIStyle.ItemsSource = Enum.GetValues(typeof(EConvGUIStyles)).Cast<EConvGUIStyles>();
            Node_Combo_ReplyType.ItemsSource = Enum.GetValues(typeof(EReplyTypes)).Cast<EReplyTypes>();

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
                    ClrPcker_Line.SelectedColor = System.Windows.Media.Color.FromArgb(c.A,c.R,c.G,c.B);
                }
                if (options.ContainsKey("ParaIntRColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["ParaIntRColor"]);
                    DObj.paraintColor = c;
                    ClrPcker_ParaInt.SelectedColor = System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
                }
                if (options.ContainsKey("RenIntRColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["RenIntRColor"]);
                    DObj.renintColor = c;
                    ClrPcker_RenInt.SelectedColor = System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
                }
                if (options.ContainsKey("AgreeRColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["AgreeRColor"]);
                    DObj.agreeColor = c;
                    ClrPcker_Agree.SelectedColor = System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
                }
                if (options.ContainsKey("DisagreeRColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["DisagreeRColor"]);
                    DObj.disagreeColor = c;
                    ClrPcker_Disagree.SelectedColor = System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
                }
                if (options.ContainsKey("FriendlyRColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["FriendlyRColor"]);
                    DObj.friendlyColor = c;
                    ClrPcker_Friendly.SelectedColor = System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
                }
                if (options.ContainsKey("HostileRColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["HostileRColor"]);
                    DObj.hostileColor = c;
                    ClrPcker_Hostile.SelectedColor = System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
                }
                if (options.ContainsKey("EntryPenColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["EntryPenColor"]);
                    DObj.entryPenColor = c;
                    ClrPcker_EntryPen.SelectedColor = System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
                }
                if (options.ContainsKey("EntryColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["EntryColor"]);
                    DObj.entryColor = c;
                    ClrPcker_Entry.SelectedColor = System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
                }
                if (options.ContainsKey("ReplyColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["ReplyColor"]);
                    DObj.replyColor = c;
                    ClrPcker_Reply.SelectedColor = System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
                }
                if (options.ContainsKey("ReplyPenColor"))
                {
                    var c = ColorTranslator.FromHtml((string)options["ReplyPenColor"]);
                    DObj.replyPenColor = c;
                    ClrPcker_ReplyPen.SelectedColor = System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
                }
                if (options.ContainsKey("AutoSave"))
                    AutoSaveView_MenuItem.IsChecked = (bool)options["AutoSave"];
                if (options.ContainsKey("OutputNumbers"))
                    ShowOutputNumbers_MenuItem.IsChecked = (bool)options["OutputNumbers"];
                if (options.ContainsKey("GlobalSeqRefView"))
                    GlobalSeqRefViewSavesMenuItem.IsChecked = (bool)options["GlobalSeqRefView"];
            }
            else
            {
                Menu_LineSize_10.IsChecked = true;
                ClrPcker_Line.SelectedColor = System.Windows.Media.Color.FromArgb(DBox.lineColor.A, DBox.lineColor.R, DBox.lineColor.G, DBox.lineColor.B);
                ClrPcker_ParaInt.SelectedColor = System.Windows.Media.Color.FromArgb(DObj.paraintColor.A, DObj.paraintColor.R, DObj.paraintColor.G, DObj.paraintColor.B);
                ClrPcker_RenInt.SelectedColor = System.Windows.Media.Color.FromArgb(DObj.renintColor.A, DObj.renintColor.R, DObj.renintColor.G, DObj.renintColor.B);
                ClrPcker_Agree.SelectedColor = System.Windows.Media.Color.FromArgb(DObj.agreeColor.A, DObj.agreeColor.R, DObj.agreeColor.G, DObj.agreeColor.B);
                ClrPcker_Disagree.SelectedColor = System.Windows.Media.Color.FromArgb(DObj.disagreeColor.A, DObj.disagreeColor.R, DObj.disagreeColor.G, DObj.disagreeColor.B);
                ClrPcker_Friendly.SelectedColor = System.Windows.Media.Color.FromArgb(DObj.friendlyColor.A, DObj.friendlyColor.R, DObj.friendlyColor.G, DObj.friendlyColor.B);
                ClrPcker_Hostile.SelectedColor = System.Windows.Media.Color.FromArgb(DObj.hostileColor.A, DObj.hostileColor.R, DObj.hostileColor.G, DObj.hostileColor.B);
                ClrPcker_Entry.SelectedColor = System.Windows.Media.Color.FromArgb(DObj.entryColor.A, DObj.entryColor.R, DObj.entryColor.G, DObj.entryColor.B);
                ClrPcker_EntryPen.SelectedColor = System.Windows.Media.Color.FromArgb(DObj.entryPenColor.A, DObj.entryPenColor.R, DObj.entryPenColor.G, DObj.entryPenColor.B);
                ClrPcker_Reply.SelectedColor = System.Windows.Media.Color.FromArgb(DObj.replyColor.A, DObj.replyColor.R, DObj.replyColor.G, DObj.replyColor.B);
                ClrPcker_ReplyPen.SelectedColor = System.Windows.Media.Color.FromArgb(DObj.replyPenColor.A, DObj.replyPenColor.R, DObj.replyPenColor.G, DObj.replyPenColor.B);
            }
        }

        public DialogueEditorWPF(IExportEntry export) : this()
        {
            FileQueuedForLoad = export.FileRef.FileName;
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

                    if (ExportQueuedForFocusing != null)
                    {
                        //GoToExport(ExportQueuedForFocusing);
                        ExportQueuedForFocusing = null;
                    }

                    Activate();
                }));
            }
        }

        private void SavePackageAs()
        {
            string extension = Path.GetExtension(Pcc.FileName);
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

                {"OutputNumbers", DObj.OutputNumbers},
                {"AutoSave", AutoSaveView_MenuItem.IsChecked},
                {"GlobalSeqRefView", GlobalSeqRefViewSavesMenuItem.IsChecked}

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
            System.Diagnostics.Debug.WriteLine("Package Is Loaded.");
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
                
                CurrentConvoPackage = Pcc;
                FirstParse();
                RightBarColumn.Width = new GridLength(260);
                graphEditor.nodeLayer.RemoveAllChildren();
                graphEditor.edgeLayer.RemoveAllChildren();

                AddRecent(fileName, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);

                Title = $"Dialogue Editor WPF - {fileName}";
                StatusText = null;

                Level = Path.GetFileName(Pcc.FileName);
                if (Pcc.Game != MEGame.ME1)
                {
                    Level = $"{Level.Remove(Level.Length - 12)}.pcc";
                }
                else
                {
                    Level = $"{Level.Remove(Level.Length - 4)}_LOC_INT{Path.GetExtension(Pcc.FileName)}";
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
            CurrentConvoPackage = null;
            Conversations.ClearEx();
            SelectedSpeakerList.ClearEx();
            Properties_InterpreterWPF.UnloadExport();
            SoundpanelWPF_F.UnloadExport();
            SoundpanelWPF_M.UnloadExport();
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
        private void FirstParse()
        {
            Conversations_ListBox.IsEnabled = false;
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
            }
            Debug.WriteLine("FirstParse Done");
            BackParser = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            BackParser.DoWork += BackParse;
            BackParser.RunWorkerCompleted += BackParser_RunWorkerCompleted;
            BackParser.RunWorkerAsync();

            Conversations_ListBox.IsEnabled = true;
        }
        private void BackParse(object sender, DoWorkEventArgs e)
        {
            Debug.WriteLine( "BackParse Starting");
            //TOO MANY PROBLEMS ON BACK THREAD. OPTIMISE LATER.
            if (SelectedConv != null && SelectedConv.IsParsed == false) //Get Active setup pronto.
            {
                foreach (var spkr in SelectedConv.Speakers)
                {
                    spkr.FaceFX_Male = GetFaceFX(SelectedConv, spkr.SpeakerID, true);
                    spkr.FaceFX_Female = GetFaceFX(SelectedConv, spkr.SpeakerID, false);
                }
                GenerateSpeakerTags(SelectedConv);
                ParseLinesInterpData(SelectedConv);
                ParseLinesFaceFX(SelectedConv);
                ParseLinesAudioStreams(SelectedConv);
                ParseLinesScripts(SelectedConv);

                SelectedConv.IsParsed = true;
            }

            //Do minor stuff
            foreach (var conv in Conversations.Where(conv => conv.IsParsed == false))
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

        }
        private void BackParser_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            BackParser.CancelAsync();
            Debug.WriteLine("BackParse Done");
        }

        private void ParseSpeakers(ConversationExtended conv)
        {
            conv.Speakers = new ObservableCollectionExtended<SpeakerExtended>();
            conv.Speakers.Add(new SpeakerExtended(-2, "player", null, null, 125303, "\"Shepard\""));
            conv.Speakers.Add(new SpeakerExtended(-1, "owner", null, null, 0, "No data"));
            if (CurrentConvoPackage.Game != MEGame.ME3)
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
        private void ParseEntryList(ConversationExtended conv)
        {
            conv.EntryList = new ObservableCollectionExtended<DialogueNodeExtended>();
            try
            {
                var entryprop = conv.BioConvo.GetProp<ArrayProperty<StructProperty>>("m_EntryList"); //ME3/ME1
                int cnt = 0;
                foreach (StructProperty Node in entryprop)
                {
                    int speakerindex = Node.GetProp<IntProperty>("nSpeakerIndex");
                    int linestrref = Node.GetProp<StringRefProperty>("srText").Value;
                    string line = GlobalFindStrRefbyID(linestrref, CurrentConvoPackage);
                    int cond = Node.GetProp<IntProperty>("nConditionalFunc").Value;
                    int stevent = Node.GetProp<IntProperty>("nStateTransition").Value;
                    bool bcond = Node.GetProp<BoolProperty>("bFireConditional");
                    conv.EntryList.Add(new DialogueNodeExtended(Node, false, cnt, speakerindex, linestrref, line, bcond, cond, stevent, EReplyTypes.REPLY_STANDARD));
                    cnt++;
                }
            }
            catch (Exception e)
            {
                //throw new Exception("Entry List Parse failed", e);
            }
        }
        private void ParseReplyList(ConversationExtended conv)
        {
            conv.ReplyList = new ObservableCollectionExtended<DialogueNodeExtended>();
            try
            {
                var replyprop = conv.BioConvo.GetProp<ArrayProperty<StructProperty>>("m_ReplyList"); //ME3
                int cnt = 0;
                foreach (StructProperty Node in replyprop)
                {
                    int speakerindex = -2;
                    int linestrref = Node.GetProp<StringRefProperty>("srText").Value;
                    string line = GlobalFindStrRefbyID(linestrref, CurrentConvoPackage);
                    int cond = Node.GetProp<IntProperty>("nConditionalFunc").Value;
                    int stevent = Node.GetProp<IntProperty>("nStateTransition").Value;
                    bool bcond = Node.GetProp<BoolProperty>("bFireConditional");
                    Enum.TryParse(Node.GetProp<EnumProperty>("ReplyType").Value.Name, out EReplyTypes eReply);
                    conv.ReplyList.Add(new DialogueNodeExtended(Node, true, cnt, speakerindex, linestrref, line, bcond, cond, stevent, eReply));
                    cnt++;
                }
            }
            catch (Exception e)
            {
                //throw new Exception("Reply List Parse failed", e);  //Note some convos don't have replies.
            }
        }
        private void ParseScripts(ConversationExtended conv)
        {
            conv.ScriptList = new List<String>();
            conv.ScriptList.Add("None");
            if (CurrentConvoPackage.Game == MEGame.ME3)
            {
                var a_scripts = conv.BioConvo.GetProp<ArrayProperty<NameProperty>>("m_aScriptList");
                if (a_scripts != null)
                {
                    foreach (var scriptprop in a_scripts)
                    {
                        var scriptname = scriptprop.ToString();
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
                        conv.ScriptList.Add(s.ToString());
                    }
                }
            }
        }
        private void ParseStageDirections(ConversationExtended conv)
        {
            conv.StageDirections = new ObservableCollectionExtended<StageDirection>();
            if(Pcc.Game == MEGame.ME3)
            {
                var dprop = conv.BioConvo.GetProp<ArrayProperty<StructProperty>>("m_aStageDirections"); //ME3 Only not in ME1/2
                if (dprop != null)
                {
                    foreach (var direction in dprop)
                    {
                        int strref = 0;
                        string line = "No data";
                        string action = "None";
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
        private void GenerateSpeakerTags(ConversationExtended conv)
        {
            var spkrlist = conv.Speakers;
            foreach (var e in conv.EntryList)
            {
                int spkridx = e.SpeakerIndex;
                var spkrtag = conv.Speakers.Where(s => s.SpeakerID == spkridx).FirstOrDefault();
                if (spkrtag != null)
                    e.SpeakerTag = spkrtag;
            }

            foreach (var r in conv.ReplyList)
            {
                int spkridx = r.SpeakerIndex;
                var spkrtag = conv.Speakers.Where(s => s.SpeakerID == spkridx).FirstOrDefault();
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
            var sequence = conv.Sequence as IExportEntry;
            var seqobjs = sequence.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");

            var convStarts = new Dictionary<int, IExportEntry>();
            foreach (var prop in seqobjs)
            {
                var seqobj = Pcc.getUExport(prop.Value);
                if (seqobj.ClassName == "BioSeqEvt_ConvNode")
                {
                    int key = seqobj.GetProperty<IntProperty>("m_nNodeID"); //ME3
                    convStarts.Add(key, seqobj);
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
                            if (outLinksProp != null)
                            {
                                var linksProp = outLinksProp[0].GetProp<ArrayProperty<StructProperty>>("Links");
                                if (linksProp != null)
                                {
                                    var link = linksProp[0].GetProp<ObjectProperty>("LinkedOp").Value;
                                    var interpseqact = Pcc.getUExport(link);
                                    if (interpseqact.ClassName != "SeqAct_Interp") //Double check egm facefx not in the loop. Go two nodes deeper. "past conditional / BioSeqAct_SetFaceFX"
                                    {
                                        var outLinksProp2 = interpseqact.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
                                        if (outLinksProp2 != null)
                                        {
                                            var linksProp2 = outLinksProp2[0].GetProp<ArrayProperty<StructProperty>>("Links");
                                            if (linksProp2 != null)
                                            {
                                                var link2 = linksProp2[0].GetProp<ObjectProperty>("LinkedOp").Value;
                                                interpseqact = Pcc.getUExport(link2);
                                                if (interpseqact.ClassName != "SeqAct_Interp") //Double check egm facefx not in the loop. Go two nodes deeper. "past conditional / BioSeqAct_SetFaceFX"
                                                {
                                                    var outLinksProp3 = interpseqact.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
                                                    if (outLinksProp3 != null)
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
                                                if (linkedVars != null)
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
                    throw new Exception($"EntryList parse interpdata failed: {entry.NodeCount}", e);
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
                            if (outLinksProp != null)
                            {
                                var linksProp = outLinksProp[0].GetProp<ArrayProperty<StructProperty>>("Links");
                                if (linksProp != null)
                                {
                                    var link = linksProp[0].GetProp<ObjectProperty>("LinkedOp").Value;
                                    var interpseqact = Pcc.getUExport(link);
                                    if (interpseqact.ClassName != "SeqAct_Interp") //Double check egm facefx not in the loop. Go two nodes deeper. "past conditional / BioSeqAct_SetFaceFX"
                                    {
                                        var outLinksProp2 = interpseqact.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
                                        if (outLinksProp2 != null)
                                        {
                                            var linksProp2 = outLinksProp2[0].GetProp<ArrayProperty<StructProperty>>("Links");
                                            if (linksProp2 != null)
                                            {
                                                var link2 = linksProp2[0].GetProp<ObjectProperty>("LinkedOp").Value;
                                                interpseqact = Pcc.getUExport(link2);
                                                if (interpseqact.ClassName != "SeqAct_Interp") //Double check egm facefx not in the loop. Go two nodes deeper. "past conditional / BioSeqAct_SetFaceFX"
                                                {
                                                    var outLinksProp3 = interpseqact.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
                                                    if (outLinksProp3 != null)
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
                                                if (linkedVars != null)
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
        /// <summary>
        /// Parses for male and female wwisestream IEntry for every line in the conversation.
        /// </summary>
        /// <param name="diag"></param>
        private void ParseLinesAudioStreams(ConversationExtended conv)
        {
            try
            {
                //Pull Male/Female animsets from Speaker
                //Get reference line how??
                //
                if (Pcc.Game != MEGame.ME1)
                {
                    Dictionary<string, IExportEntry> streams = Pcc.Exports.Where(x => x.ClassName == "WwiseStream").ToDictionary(x => x.ObjectName.ToLower(), x => x);

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
            catch
            {
                //ignore
            }
        }
        private void ParseLinesScripts(ConversationExtended conv)
        {
            if(conv.IsFirstParsed)
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
                    MessageBox.Show($"Parse failure on script list{e}");//ignore
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
                Enum.TryParse(nodeprop.GetProp<EnumProperty>("eGUIStyle").Value.Name, out EConvGUIStyles gstyle);
                node.GUIStyle = gstyle;
                if (Pcc.Game == MEGame.ME3)
                {
                    node.HideSubtitle = nodeprop.GetProp<BoolProperty>("bAlwaysHideSubtitle");
                    if (node.IsReply)
                    {
                        node.IsDefaultAction = nodeprop.GetProp<BoolProperty>("bIsDefaultAction");
                        node.IsMajorDecision = nodeprop.GetProp<BoolProperty>("bIsMajorDecision");
                    }
                }

                if (node.Interpdata != null)
                {
                    var lengthprop = node.Interpdata.GetProperty<FloatProperty>("InterpLength");
                    if (lengthprop != null)
                    {
                        node.InterpLength = lengthprop.Value;
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show($"DiagNodeParse Failed. {e}");
            }
        }
        private void ParseLinesFaceFX(ConversationExtended conv)
        {
            foreach (var entry in conv.EntryList)
            {
                if (entry.Line != "No data" && entry.Line != " " && entry.Line != "" && entry.Line != "  ")
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
                if (reply.Line != "No data" && reply.Line != " " && reply.Line != "" && reply.Line != "  ")
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
        /// <param name="speakerID">SpeakerID -1 = Owner, -2 = Player</param>
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
            else
            {
                return null;
            }
        }
        /// <summary>
        /// Sets the IEntry of appropriate sequence
        /// </summary>
        public void ParseSequence(ConversationExtended conv)
        {
            string propname = "MatineeSequence";
            if (CurrentConvoPackage.Game == MEGame.ME1)
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
            if (CurrentConvoPackage.Game == MEGame.ME1)
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
            try
            {
                IEntry ffxo = GetFaceFX(conv, -1, true); //find owner animset
                if (!Pcc.isUExport(ffxo.UIndex))
                    return;
                IExportEntry ffxoExport = ffxo as IExportEntry;

                var wwevents = ffxoExport.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedSoundCues"); //pull a wwiseevent array
                if (wwevents == null)
                {
                    IEntry ffxp = GetFaceFX(conv, -2, true); //find player as alternative
                    if (!Pcc.isUExport(ffxo.UIndex))
                        return;
                    IExportEntry ffxpExport = ffxp as IExportEntry;
                    wwevents = ffxpExport.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedSoundCues");
                }

                if (Pcc.Game == MEGame.ME3)
                {
                    StructProperty r = CurrentConvoPackage.getUExport(wwevents[0].Value).GetProperty<StructProperty>("Relationships"); //lookup bank
                    var bank = r.GetProp<ObjectProperty>("Bank");
                    conv.WwiseBank = Pcc.getUExport(bank.Value);
                }
                else if (Pcc.Game == MEGame.ME2) //Game is ME2.  Wwisebank ref in Binary.
                {
                    var data = Pcc.getUExport(wwevents[0].Value).getBinaryData();
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
            catch
            {
                //ignore
            }
        }
        public int ParseActorsNames(ConversationExtended conv, string tag)
        {
            if (CurrentConvoPackage.Game == MEGame.ME1)
            {
                try
                {
                    var actors = CurrentConvoPackage.Exports.Where(xp => xp.ClassName == "BioPawn");
                    IExportEntry actor = actors.FirstOrDefault(a => a.GetProperty<NameProperty>("Tag").ToString() == tag);
                    var behav = actor.GetProperty<ObjectProperty>("m_oBehavior");
                    var set = CurrentConvoPackage.getUExport(behav.Value).GetProperty<ObjectProperty>("m_oActorType");
                    var strrefprop = CurrentConvoPackage.getUExport(set.Value).GetProperty<StringRefProperty>("ActorGameNameStrRef");
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

        private bool AutoGenerateSpeakerArrays(ConversationExtended conv)
        {
            bool hasLoopingPaths = false;

            var blankaSpkr = new ArrayProperty<IntProperty>(ArrayType.Int, "aSpeakerList");
            foreach (var dnode in SelectedConv.EntryList)
            {
                dnode.NodeProp.Properties.AddOrReplaceProp(blankaSpkr);
            }

            foreach (var s in conv.StartingList)
            {
                var aSpkrs = new SortedSet<int>();
                var startNode = conv.EntryList[s.Value];
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
                    if(!visitedNodes.Contains(thisnode))
                    {
                        aSpkrs.Add(thisnode.SpeakerIndex);
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
                var newaSpkr = new ArrayProperty<IntProperty>(ArrayType.Int,"aSpeakerList");
                foreach(var a in aSpkrs)
                {
                    newaSpkr.Add(a);
                }
                startNode.NodeProp.Properties.AddOrReplaceProp(newaSpkr);
            }
            return hasLoopingPaths;
        }

        private void SaveSpeakersToProperties(ObservableCollectionExtended<SpeakerExtended> speakerCollection)
        {
            try
            {

                var m_aSpeakerList = new ArrayProperty<NameProperty>(ArrayType.Name, "m_aSpeakerList");
                var m_SpeakerList = new ArrayProperty<StructProperty>(ArrayType.Struct, "m_SpeakerList");
                var m_aMaleFaceSets = new ArrayProperty<ObjectProperty>(ArrayType.Object, "m_aMaleFaceSets");
                var m_aFemaleFaceSets = new ArrayProperty<ObjectProperty>(ArrayType.Object, "m_aFemaleFaceSets");

                foreach (SpeakerExtended spkr in speakerCollection)
                {
                    if (spkr.SpeakerID >= 0)
                    {
                        if (Pcc.Game == MEGame.ME3)
                        {
                            m_aSpeakerList.Add(new NameProperty("m_aSpeakerList") { Value = spkr.SpeakerName });
                        }
                        else
                        {
                            var spkrProp = new PropertyCollection();
                            spkrProp.Add(new NameProperty("sSpeakerTag") { Value = spkr.SpeakerName });
                            spkrProp.Add(new NoneProperty());
                            m_SpeakerList.Add(new StructProperty("BioDialogSpeaker", spkrProp));
                        }
                    }

                    if (spkr.FaceFX_Male == null)
                    {
                        m_aMaleFaceSets.Add(new ObjectProperty(0));
                    }
                    else
                    {
                        m_aMaleFaceSets.Add(new ObjectProperty(spkr.FaceFX_Male, "m_aMaleFaceSets"));
                    }
                    if (spkr.FaceFX_Female == null)
                    {
                        m_aFemaleFaceSets.Add(new ObjectProperty(0));
                    }
                    else
                    {
                        m_aFemaleFaceSets.Add(new ObjectProperty(spkr.FaceFX_Female, "m_aFemaleFaceSets"));
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
            var newstartlist = new ArrayProperty<IntProperty>(ArrayType.Int, "m_StartingList");
            foreach (var start in conv.StartingList)
            {
                newstartlist.Add(start.Value);
            }

            var newentryList = new ArrayProperty<StructProperty>(ArrayType.Struct,"m_EntryList");
            foreach (var entry in conv.EntryList.OrderBy(entry => entry.NodeCount))
            {
                newentryList.Add(entry.NodeProp);
            }
            var newreplyList = new ArrayProperty<StructProperty>(ArrayType.Struct, "m_ReplyList");
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
            if(Pcc.Game == MEGame.ME3)
            {
                var newscriptList = new ArrayProperty<NameProperty>(ArrayType.Name, "m_aScriptList");
                foreach (var script in conv.ScriptList)
                {
                    if(script != "None")
                    {
                        newscriptList.Add(new NameProperty("m_aScriptList") { Value = script });
                    }
                }
                if (newscriptList.Count > 0)
                {
                    conv.BioConvo.AddOrReplaceProp(newscriptList);
                }
            }
            else
            {
                var newscriptList = new ArrayProperty<StructProperty>(ArrayType.Name, "m_ScriptList");
                foreach (var script in conv.ScriptList)
                {
                    if (script != "None")
                    {
                        var s = new PropertyCollection();
                        s.AddOrReplaceProp(new NameProperty("sScriptTag", script));
                        s.AddOrReplaceProp(new NoneProperty());
                        newscriptList.Add(new StructProperty("BioDialogScript", s));
                    }
                }
                if (newscriptList.Count > 0)
                {
                    conv.BioConvo.AddOrReplaceProp(newscriptList);
                }
            }
            if(pushtofile)
            {
                PushConvoToFile(conv);
            }
        }

        private void SaveStageDirectionsToProperties(ConversationExtended conv)
        {
            var aStageDirs = new ArrayProperty<StructProperty>(ArrayType.Struct, "m_aStageDirections");
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
            IEnumerable<PackageUpdate> relevantUpdates = updates.Where(x => x.change != PackageChange.Import &&
                                                                            x.change != PackageChange.ImportAdd &&
                                                                            x.change != PackageChange.Names);

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
                LoadConversations();
                return;
            }

            if (relevantUpdates.Select(x => x.index).Where(update => Pcc.getExport(update).ClassName == "FaceFXAnimSet").Any())
            {
                FFXAnimsets.ClearEx(); //REBUILD ANIMSET LIST IF NEW ONES.
                foreach (var exp in Pcc.Exports.Where(exp => exp.ClassName == "FaceFXAnimSet"))
                {
                    FFXAnimsets.Add(exp);
                }
            }

            if (SelectedDialogueNode != null) //Update any changes to live dialogue node
            {
                if (relevantUpdates.Select(x => x.index).Where(update => Pcc.getExport(update) == SelectedDialogueNode.Interpdata).Any())
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



            List<int> updatedConvos = relevantUpdates.Select(x => x.index).Where(update => Pcc.getExport(update).ClassName == "BioConversation").ToList();
            if (updatedConvos.IsEmpty())
                return;

            int cSelectedIdx = Conversations_ListBox.SelectedIndex;
            int sSelectedIdx = Speakers_ListBox.SelectedIndex;
            foreach (var uxp in updatedConvos)
            {
                var exp = Pcc.getExport(uxp);
                int index = Conversations.FindIndex(i => i.ExportUID == exp.UIndex);  //Can remove this nested loop?  How?
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

            var diagnode = sender as DialogueNodeExtended;  //THIS IS A GATE TO CHECK IF VALUES HAVE CHANGED
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
                    var nStateTransitionParam = new IntProperty(node.TransitionParam,"nStateTransitionParam");
                    prop.Properties.AddOrReplaceProp(nStateTransitionParam);
                    break;
                case "ExportID":
                    var nExportID = new IntProperty(node.ExportID,"nExportID");
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
        private readonly string RECENTFILES_FILE = "RECENTFILES";

        private void LoadRecentList()
        {
            RecentButtons.AddRange(new[] { RecentButton1, RecentButton2, RecentButton3, RecentButton4, RecentButton5, RecentButton6, RecentButton7, RecentButton8, RecentButton9, RecentButton10 });
            Recents_MenuItem.IsEnabled = false;
            RFiles = new List<string>();
            RFiles.Clear();
            string path = DialogueEditorDataFolder + RECENTFILES_FILE;
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

            string path = DialogueEditorDataFolder + RECENTFILES_FILE;
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

            if (File.Exists(JSONpath))
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
            //if (SavedPositions.IsEmpty())
            //{
            //    {
            //        AutoLayout();
            //    }
            //}
        }
        public bool LoadDialogueObjects()
        {
            float x = 0;
            float y = 0;
            int ecnt = SelectedConv.EntryList.Count;
            int rcnt = SelectedConv.ReplyList.Count;
            int[] m = { ecnt, rcnt };
            int max = m.Max();
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
                    CurrentObjects.Add(new DiagNodeEntry(this, SelectedConv.EntryList[n], Pcc.Game, x, y, graphEditor));
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

                bool lastwasreply = true;
                float f = 3;
                for (int i = 0; i < CurrentObjects.Count; i++)
                {
                    DObj obj = CurrentObjects[i];

                    //SAVED DATA
                    SaveData savedInfo = new SaveData(-1);
                    if (SavedPositions.Any())
                    {
                        if (RefOrRefChild)
                            savedInfo = SavedPositions.FirstOrDefault(p => i == p.index);
                        else
                            savedInfo = SavedPositions.FirstOrDefault(p => obj.NodeUID == p.index);
                    }

                    bool hasSavedPosition =
                        savedInfo.index == (RefOrRefChild ? i : obj.NodeUID);
                    if (hasSavedPosition)
                    {
                        obj.Layout(savedInfo.X, savedInfo.Y);
                    }
                    else
                    {
                        //SIMPLE LAYOUT THIS ONLY WORKS WHEN THERE ARE ENTRY NODES AND REPLY NODES MATCHED.
                        switch (obj)
                        {
                            case DStart _:
                                DStart dstart = obj as DStart;
                                obj.Layout(0, StartPoDiagNodes);
                                break;
                            case DiagNodeEntry _:
                                obj.Layout(250, StartPoDiagNodes);
                                if (lastwasreply) { f = 3; }
                                StartPoDiagNodes += obj.Height / f + 25;
                                f = 1;
                                lastwasreply = false;
                                break;
                            case DiagNodeReply _:
                                obj.Layout(700, StartPoDiagNodes);
                                StartPoDiagNodes += obj.Height + 25;
                                lastwasreply = true;
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

            //SIMPLE LAYOUT
            //switch (obj)
            //{
            //    case DStart _:
            //        DStart dstart = obj as DStart;
            //        float ystart = (dstart.StartNumber * 127);
            //        obj.Layout(0, ystart);
            //        //StartPoDStarts += obj.Height + 20;
            //        break;
            //    case DiagNodeReply _:
            //        obj.Layout(500, StartPoDReplyNodes);
            //        StartPoDReplyNodes += obj.Height + 25;
            //        break;
            //    case DiagNode _:
            //        obj.Layout(250, StartPoDiagNodes);
            //        StartPoDiagNodes += obj.Height + 25;
            //        break;

            //}
            //    }

            //foreach (DObj obj in CurrentObjects)
            //{
            //    obj.SetOffset(0, 0); //remove existing positioning
            //}

            //const float HORIZONTAL_SPACING = 40;
            //const float VERTICAL_SPACING = 20;
            //var visitedNodes = new HashSet<int>();
            //var eventNodes = CurrentObjects.OfType<DStart>().ToList();
            //DObj firstNode = eventNodes.FirstOrDefault();

            //var rootTree = new List<DObj>();
            ////DStarts are natural root nodes. ALmost everything will proceed from one of these
            //foreach (DStart eventNode in eventNodes)
            //{
            //    LayoutTree(eventNode, 5 * VERTICAL_SPACING);
            //}

            ////Find DiagNodes with no inputs. These will not have been reached from an DStart
            //var orphanRoots = CurrentObjects.OfType<DiagNode>().Where(node => node.InputEdges.IsEmpty());
            //foreach (DiagNode orphan in orphanRoots)
            //{
            //    LayoutTree(orphan, VERTICAL_SPACING);
            //}

            ////It's possible that there are groups of otherwise unconnected DiagNodes that form cycles.
            ////Might be possible to make a better heuristic for choosing a root than sequence order, but this situation is so rare it's not worth the effort
            //var cycleNodes = CurrentObjects.OfType<DiagNode>().Where(node => !visitedNodes.Contains(node.UIndex));
            //foreach (DiagNode cycleNode in cycleNodes)
            //{
            //    LayoutTree(cycleNode, VERTICAL_SPACING);
            //}

            ////Lonely unconnected variables. Put them in a row below everything else
            //var unusedVars = CurrentObjects.OfType<SVar>().Where(obj => !visitedNodes.Contains(obj.UIndex));
            //float varOffset = 0;
            //float vertOffset = rootTree.BoundingRect().Bottom + VERTICAL_SPACING;
            //foreach (SVar unusedVar in unusedVars)
            //{
            //    unusedVar.OffsetBy(varOffset, vertOffset);
            //    varOffset += unusedVar.GlobalFullWidth + HORIZONTAL_SPACING;
            //}

            //if (firstNode != null) CurrentObjects.OffsetBy(0, -firstNode.OffsetY);

            //foreach (DiagEdEdge edge in graphEditor.edgeLayer)
            //    ConvGraphEditor.UpdateEdge(edge);


            //void LayoutTree(DBox DiagNode, float verticalSpacing)
            //{
            //    if (firstNode == null) firstNode = DiagNode;
            //    visitedNodes.Add(DiagNode.UIndex);
            //    var subTree = LayoutSubTree(DiagNode);
            //    float width = subTree.BoundingRect().Width + HORIZONTAL_SPACING;
            //    //ignore nodes that are further to the right than this subtree is wide. This allows tighter spacing
            //    float dy = rootTree.Where(node => node.GlobalFullBounds.Left < width).BoundingRect().Bottom;
            //    if (dy > 0) dy += verticalSpacing;
            //    subTree.OffsetBy(0, dy);
            //    rootTree.AddRange(subTree);
            //}

            //List<DObj> LayoutSubTree(DBox root)
            //{
            //    //Task.WaitAll(Task.Delay(1500));
            //    var tree = new List<DObj>();
            //    var childTrees = new List<List<DObj>>();
            //    var children = root.Outlinks.SelectMany(link => link.Links).Where(uIndex => !visitedNodes.Contains(uIndex));
            //    foreach (int uIndex in children)
            //    {
            //        visitedNodes.Add(uIndex);
            //        if (opNodeLookup.TryGetValue(uIndex, out DBox node))
            //        {
            //            List<DObj> subTree = LayoutSubTree(node);
            //            childTrees.Add(subTree);
            //        }
            //    }

            //    if (childTrees.Any())
            //    {
            //        float dx = root.GlobalFullWidth + (HORIZONTAL_SPACING * (1 + childTrees.Count * 0.4f));
            //        foreach (List<DObj> subTree in childTrees)
            //        {
            //            float subTreeWidth = subTree.BoundingRect().Width + HORIZONTAL_SPACING + dx;
            //            //ignore nodes that are further to the right than this subtree is wide. This allows tighter spacing
            //            float dy = tree.Where(node => node.GlobalFullBounds.Left < subTreeWidth).BoundingRect().Bottom;
            //            if (dy > 0) dy += VERTICAL_SPACING;
            //            subTree.OffsetBy(dx, dy);
            //            //TODO: fix this so it doesn't screw up some sequences. eg: BioD_ProEar_310BigFall.pcc
            //            /*float treeWidth = tree.BoundingRect().Width + HORIZONTAL_SPACING;
            //            //tighten spacing when this subtree is wider than existing tree. 
            //            dy -= subTree.Where(node => node.GlobalFullBounds.Left < treeWidth).BoundingRect().Top;
            //            if (dy < 0) dy += VERTICAL_SPACING;
            //            subTree.OffsetBy(0, dy);*/

            //            tree.AddRange(subTree);
            //        }

            //        //center the root on its children
            //        float centerOffset = tree.OfType<DBox>().BoundingRect().Height / 2 - root.GlobalFullHeight / 2;
            //        root.OffsetBy(0, centerOffset);
            //    }


            //    tree.Add(root);
            //    return tree;
            //}
        }

        public void RefreshView()
        {
            if(SelectedConv != null)
            {
                Properties_InterpreterWPF.LoadExport(CurrentLoadedExport);
                if (SelectedDialogueNode != null)
                {
                    SoundpanelWPF_F.LoadExport(SelectedDialogueNode.WwiseStream_Female);
                    SoundpanelWPF_M.LoadExport(SelectedDialogueNode.WwiseStream_Male);
                }

                GenerateGraph();
                saveView(false);
            }
        }
        #endregion CreateGraph  

        #region UIHandling-items
        /// <summary>
        /// Sets UI to 0 = Convo (default), 1=Speakers, 2=Node.
        /// </summary>
        private int SetUIMode(int mode, Boolean force = false)
        {
            if (mode == CurrentUIMode && !force)
            {
                return CurrentUIMode;
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

            if (Pcc.Game == MEGame.ME3)
            {
                StageDirections_Expander.Visibility = Visibility.Visible;
            }
            else
            {
                StageDirections_Expander.Visibility = Visibility.Collapsed;
            }
            return CurrentUIMode;
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
                Convo_Panel.Visibility = Visibility.Collapsed;
            }
            else
            {
                SelectedDialogueNode = null; //Before convos change make sure no properties fire.
                graphEditor.Enabled = false;
                graphEditor.UseWaitCursor = true;
                var nconv = Conversations[Conversations_ListBox.SelectedIndex];
                SelectedConv = new ConversationExtended(nconv.ExportUID, nconv.ConvName, nconv.BioConvo, nconv.Export, nconv.IsParsed, nconv.IsFirstParsed, nconv.StartingList, nconv.Speakers, nconv.EntryList, nconv.ReplyList, nconv.StageDirections, nconv.WwiseBank, nconv.Sequence, nconv.NonSpkrFFX, nconv.ScriptList); ;
                CurrentLoadedExport = CurrentConvoPackage.getUExport(SelectedConv.ExportUID);
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
        private void SetupConvJSON(IExportEntry export)
        {
            string objectName = Regex.Replace(export.ObjectName, @"[<>:""/\\|?*]", "");
            bool isClonedSeqRef = false;
            var defaultViewZoomProp = export.GetProperty<FloatProperty>("DefaultViewZoom");
            if (defaultViewZoomProp != null && Math.Abs(defaultViewZoomProp.Value - CLONED_SEQREF_MAGIC) < 1.0E-30f)
            {
                isClonedSeqRef = true;
            }

            string packageFullName = export.PackageFullName;
            if (GlobalSeqRefViewSavesMenuItem.IsChecked && packageFullName.Contains("SequenceReference") && !isClonedSeqRef)
            {
                string packageName = packageFullName.Substring(packageFullName.LastIndexOf("SequenceReference"));
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
                        if (Pcc.getUExport(Pcc.getUExport(idx).idxLink).ClassName == "SequenceReference")
                        {
                            var objNameProp = Pcc.getUExport(idx).GetProperty<StrProperty>("ObjName");
                            if (objNameProp != null)
                            {
                                ObjName = objNameProp.Value;
                                break;
                            }
                        }

                        idx = Pcc.getUExport(idx).idxLink;
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

        private void Speakers_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!SelectedSpeakerList.IsEmpty())
            {
                if (Speakers_ListBox.SelectedIndex >= 0)
                {
                    if(SelectedSpeaker.StrRefID <= 0)
                    {
                        SelectedSpeaker.StrRefID = LookupTagRef(SelectedSpeaker.SpeakerName);
                        SelectedSpeaker.FriendlyName = GlobalFindStrRefbyID(SelectedSpeaker.StrRefID, Pcc);
                    }

                    if (SelectedSpeaker.SpeakerID < 0)
                    {
                        TextBox_Speaker_Name.IsEnabled = false;
                    }
                    else
                    {
                        TextBox_Speaker_Name.IsEnabled = true;
                    }

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
                if (dlg == MessageBoxResult.No)
                {
                    return;
                }
                else
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
        private int LookupTagRef(string actortag)
        {
            if (!TagDBLoaded)
            {
                if (File.Exists(ActorDatabasePath))
                {
                    ActorStrRefs = JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(ActorDatabasePath));
                    TagDBLoaded = true;
                }
            }
            var strref = ActorStrRefs.FirstOrDefault(a => a.Key.ToLower() == actortag.ToLower());
            if (strref.Key != null)
            {
                return strref.Value;
            }

            return 0;
        }

        private void EditBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            var editbox = sender as TextBox;
            editbox.BorderThickness = new Thickness(2, 2, 2, 2);
            editbox.Background = System.Windows.Media.Brushes.GhostWhite;
        }
        private void EditBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            var editbox = sender as TextBox;
            editbox.BorderThickness = new Thickness(0, 0, 0, 0);
            editbox.Background = System.Windows.Media.Brushes.White;
        }
        private void EditBox_Node_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                var tbox = sender as TextBox;
                Keyboard.ClearFocus();
                var be = tbox.GetBindingExpression(TextBox.TextProperty);
                switch (e.Key)
                {
                    case Key.Enter:
                        if (be != null) be.UpdateSource();
                        break;
                    case Key.Escape:
                        if (be != null) be.UpdateTarget();
                        break;
                }
            }
        }
        private void NumberValidationEditBox(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void Start_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var startNodes = CurrentObjects.OfType<DStart>().ToList();
            var start = startNodes.FirstOrDefault(s => s.Order == Start_ListBox.SelectedIndex );
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
            foreach(var entry in SelectedConv.EntryList)
            {
                links.Add($"{entry.NodeCount}: {entry.LineStrRef} {entry.Line}");
            }
            var sdlg = InputComboBox.GetValue("Pick an entry node to link to", links, links[f], false);
            
            if (sdlg == "")
                return;

            var newVal = links.FindIndex(sdlg.Equals);

            if(p == "Edit")
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
            if(forcedSelectStart > -1)
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
            var sdlg = new PromptDialog("Enter the new script name", "Add a script", "script name");
            sdlg.ShowDialog();
            if (sdlg.ResponseText == null || sdlg.ResponseText == "script name")
                return;
            Pcc.FindNameOrAdd(sdlg.ResponseText);
            SelectedConv.ScriptList.Add(sdlg.ResponseText);
            SaveScriptsToProperties(SelectedConv);
        }
        private void Script_Delete()
        {
            var cdlg = MessageBox.Show("Are you sure you want to delete this script reference", "Confirm", MessageBoxButton.OKCancel);
            if (cdlg == MessageBoxResult.Cancel)
                return;
            var script2remove = Script_ListBox.SelectedItem.ToString();
            //CHECK IF ANY LINES REFERENCE THIS SCRIPT.
            bool hasreferences = false;
            hasreferences = SelectedConv.EntryList.Any(e => e.Script == script2remove);
            if (!hasreferences)
            {
                hasreferences = SelectedConv.ReplyList.Any(r => r.Script == script2remove);
            }

            if(hasreferences)
            {
                MessageBox.Show("There are lines that reference this script.\r\nPlease remove all references before deleting", "Warning", MessageBoxButton.OK);
                return;
            }

            SelectedConv.ScriptList.Remove(script2remove);
            foreach(var e in SelectedConv.EntryList)
            {
                var scriptidx = SelectedConv.ScriptList.FindIndex(s => s == e.Script) - 1;
                var nScriptIndex = new IntProperty(scriptidx, "nScriptIndex");
                e.NodeProp.Properties.AddOrReplaceProp(nScriptIndex);
            }
            foreach (var r in SelectedConv.ReplyList)
            {
                var scriptidx = SelectedConv.ScriptList.FindIndex(s => s == r.Script) - 1;
                var nScriptIndex = new IntProperty(scriptidx,"nScriptIndex");
                r.NodeProp.Properties.AddOrReplaceProp(nScriptIndex);
            }

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
            else if (index >= 0 )
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

            SoundpanelWPF_F.LoadExport(SelectedDialogueNode.WwiseStream_Female);
            SoundpanelWPF_M.LoadExport(SelectedDialogueNode.WwiseStream_Male);

            if (SelectedDialogueNode.FiresConditional)
                Node_Text_Cnd.Text = "Conditional: ";
            else
                Node_Text_Cnd.Text = "Bool: ";


        }
        private void DialogueNode_OpenLinkEditor(object obj)
        {
            var linkEdDlg = new LinkEditor(this, SelectedObjects[0] as DiagNode);
            linkEdDlg.Owner = this;
            linkEdDlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            linkEdDlg.ShowDialog();

            if(linkEdDlg.NeedsPush)
            {
                RecreateNodesToProperties(SelectedConv);
            }
            
        }
        private async void DialogueNode_Add(object obj)
        {
            string command = obj as string;

            if(command == "AddReply")
            {
                PropertyCollection newprop = UnrealObjectInfo.getDefaultStructValue(Pcc.Game, "BioDialogReplyNode", true);
                var props = SelectedConv.BioConvo.GetProp<ArrayProperty<StructProperty>>("m_ReplyList");
                if (props == null)
                {
                    props = new ArrayProperty<StructProperty>(ArrayType.Struct, "m_ReplyList");
                }
                //Set to needed defaults.
                var EConvGUIStyles = new EnumProperty("GUI_STYLE_NONE","EConvGUIStyles", Pcc.Game, "eGUIStyle");
                newprop.AddOrReplaceProp(EConvGUIStyles);
                var rtype = new EnumProperty("REPLY_STANDARD", "EReplyTypes", Pcc.Game, "ReplyType");
                newprop.AddOrReplaceProp(rtype);
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
                var props = SelectedConv.BioConvo.GetProp<ArrayProperty<StructProperty>>("m_EntryList");
                if (props == null)
                {
                    props = new ArrayProperty<StructProperty>(ArrayType.Struct, "m_EntryList");
                }
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
                var newReply = new DialogueNodeExtended(SelectedDialogueNode);
                newReply.NodeCount = newIndex;
                SelectedConv.ReplyList.Add(newReply);
                NoUIRefresh = true;
                RecreateNodesToProperties(SelectedConv);
                bool p = true;
                while(p)
                {
                    p = await CheckProcess(NoUIRefresh, 100);
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
                var newEntry = new DialogueNodeExtended(SelectedDialogueNode);
                newEntry.NodeCount = SelectedConv.ReplyList.Count;
                SelectedConv.EntryList.Add(newEntry);
                NoUIRefresh = true;
                RecreateNodesToProperties(SelectedConv);
                bool p = true;
                while (p)
                {
                    p = await CheckProcess(NoUIRefresh, 100);
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
                return;
            }
        }
        private void DialogueNode_DeleteLinks(object obj)
        {
            if(SelectedDialogueNode.IsReply)
            {
                var entrylinklist = SelectedDialogueNode.NodeProp.GetProp<ArrayProperty<IntProperty>>("EntryList");
                if(entrylinklist != null)
                {
                    entrylinklist.Clear();
                    RecreateNodesToProperties(SelectedConv);
                }
            }
            else
            {
                var replylinklist = SelectedDialogueNode.NodeProp.GetProp<ArrayProperty<StructProperty>>("ReplyListNew");
                if(replylinklist != null)
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
            if(deleteNode.IsReply)
            {
                foreach (var entry in SelectedConv.EntryList)
                {
                    var newReplyLinksProp = new ArrayProperty<StructProperty>(ArrayType.Struct, "ReplyListNew");
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
                    var newEntryLinksProp = new ArrayProperty<IntProperty>(ArrayType.Int, "EntryList");
                    if (oldEntryLinksProp != null)
                    {
                        foreach (var r in oldEntryLinksProp)
                        {
                            if(r.Value != deleteID)
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
                foreach (KeyValuePair<int,int> s in SelectedConv.StartingList)
                {
                    int val = s.Value;
                    int key = s.Key;
                    if (val > deleteID)
                    {
                        newStartList.Add(key, val - 1);
                    }
                    else if(val < deleteID)
                    {
                        newStartList.Add(key, val);
                    }
                }
                SelectedConv.StartingList.Clear();
                foreach(var ns in newStartList)
                {
                    SelectedConv.StartingList.Add(ns.Key,ns.Value);
                }

                SelectedConv.EntryList.RemoveAt(deleteID);
            }
            RecreateNodesToProperties(SelectedConv);
        }

        private void StageDirections_TextChanged(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                var tbox = sender as TextBox;
                Keyboard.ClearFocus();
                var be = tbox.GetBindingExpression(TextBox.TextProperty);
                switch (e.Key)
                {
                    case Key.Enter:
                        if (be != null) be.UpdateSource();
                        SaveStageDirectionsToProperties(SelectedConv);
                        break;
                    case Key.Escape:
                        if (be != null) be.UpdateTarget();
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
                    if(!isNumber || strRef <= 0)
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
            if (sender is DiagNode obj)
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
            else if(sender is DStart start)
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
            for (int i = 0; i < CurrentObjects.Count; i++)
            {
                DObj obj = CurrentObjects[i];
                if (obj.Pickable)
                {
                    SavedPositions.Add(new SaveData
                    {
                        absoluteIndex = RefOrRefChild,
                        index = RefOrRefChild ? i : obj.NodeUID,
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



        private void backMouseDown_Handler(object sender, PInputEventArgs e)
        {
            if (!(e.PickedNode is PCamera) || SelectedConv == null) return;

            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {

            }
            else if (e.Shift)
            {
                //graphEditor.StartBoxSelection(e);
                //e.Handled = true;
            }
            else
            {
                //Conversations_ListBox.SelectedItems.Clear();
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

        private void showOutputNumbers_Click(object sender, EventArgs e)
        {
            DObj.OutputNumbers = ShowOutputNumbers_MenuItem.IsChecked;
            if (CurrentObjects.Any())
            {
                RefreshView();
            }
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
                if(GotoBox.SelectedItem is DiagNode dnode)
                {
                    DialogueNode_Selected(dnode);

                }
                if(GotoBox.SelectedItem is DObj o)
                {
                    graphEditor.Camera.AnimateViewToCenterBounds(o.GlobalFullBounds, false, 100);
                    graphEditor.Refresh();
                }
            }
        }
        private void GlobalSeqRefViewSavesMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects.Any())
            {
                SetupConvJSON(SelectedConv.Export);
            }
        }

        private void TestPaths()
        {
            if(AutoGenerateSpeakerArrays(SelectedConv))
            {
                MessageBox.Show("There are possible looping pathways to this conversation.\r\nThis can be a problem unless the player has control of the loop via choices.", "Dialogue Editor");
            }
            else
            {
                MessageBox.Show("No looping paths in the conversation.", "Dialogue Editor");
            }
        }
        //TEMPORARY UNTIL NEW BUILD
        private void OpenInInterpViewer_Clicked(IExportEntry exportEntry)
        {

            var p = new InterpEditor();
            p.Show();
            p.LoadPCC(Pcc.FileName);
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
                filePath = Path.Combine(Path.GetDirectoryName(Pcc.FileName), filename);

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
                filePath = Pcc.FileName;
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
                        packEditor.LoadFile(Pcc.FileName, export);
                    }
                    else
                    {
                        packEditor.LoadFile(filePath);
                    }
                    break;
                case "SoundplorerWPF":
                    var soundplorerWPF = new Soundplorer.SoundplorerWPF();
                    soundplorerWPF.LoadFile(Pcc.FileName);
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
            if(!GotoBox.IsDropDownOpen)
            {
                GotoBox.IsDropDownOpen = true;
                Keyboard.Focus(GotoBox);
            }
            else
            {
                GotoBox.IsDropDownOpen = false;
            }
            
        }
        private void LoadTLKManager()
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
            string cmd = obj as string;
            if (cmd == null)
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
            var source = sender as Xceed.Wpf.Toolkit.ColorPicker;
            var newcolor = e.NewValue.Value;
            switch(source.Name)
            {
                case "ClrPcker_Line":
                    DBox.lineColor = Color.FromArgb(newcolor.A, newcolor.R, newcolor.G, newcolor.B);
                    break;
                case "ClrPcker_ParaInt":
                    DObj.paraintColor = Color.FromArgb(newcolor.A, newcolor.R, newcolor.G, newcolor.B);
                    break;
                case "ClrPcker_RenInt":
                    DObj.renintColor = Color.FromArgb(newcolor.A, newcolor.R, newcolor.G, newcolor.B);
                    break;
                case "ClrPcker_Agree":
                    DObj.agreeColor = Color.FromArgb(newcolor.A, newcolor.R, newcolor.G, newcolor.B);
                    break;
                case "ClrPcker_Disagree":
                    DObj.disagreeColor = Color.FromArgb(newcolor.A, newcolor.R, newcolor.G, newcolor.B);
                    break;
                case "ClrPcker_Friendly":
                    DObj.friendlyColor = Color.FromArgb(newcolor.A, newcolor.R, newcolor.G, newcolor.B);
                    break;
                case "ClrPcker_Hostile":
                    DObj.hostileColor = Color.FromArgb(newcolor.A, newcolor.R, newcolor.G, newcolor.B);
                    break;
                case "ClrPcker_EntryPen":
                    DObj.entryPenColor = Color.FromArgb(newcolor.A, newcolor.R, newcolor.G, newcolor.B);
                    break;
                case "ClrPcker_ReplyPen":
                    DObj.replyPenColor = Color.FromArgb(newcolor.A, newcolor.R, newcolor.G, newcolor.B);
                    break;
                case "ClrPcker_Entry":
                    DObj.entryColor = Color.FromArgb(newcolor.A, newcolor.R, newcolor.G, newcolor.B);
                    break;
                case "ClrPcker_Reply":
                    DObj.replyColor = Color.FromArgb(newcolor.A, newcolor.R, newcolor.G, newcolor.B);
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
            ClrPcker_Line.SelectedColor = System.Windows.Media.Color.FromArgb(DBox.lineColor.A, DBox.lineColor.R, DBox.lineColor.G, DBox.lineColor.B);
            ClrPcker_ParaInt.SelectedColor = System.Windows.Media.Color.FromArgb(DObj.paraintColor.A, DObj.paraintColor.R, DObj.paraintColor.G, DObj.paraintColor.B);
            ClrPcker_RenInt.SelectedColor = System.Windows.Media.Color.FromArgb(DObj.renintColor.A, DObj.renintColor.R, DObj.renintColor.G, DObj.renintColor.B);
            ClrPcker_Agree.SelectedColor = System.Windows.Media.Color.FromArgb(DObj.agreeColor.A, DObj.agreeColor.R, DObj.agreeColor.G, DObj.agreeColor.B);
            ClrPcker_Disagree.SelectedColor = System.Windows.Media.Color.FromArgb(DObj.disagreeColor.A, DObj.disagreeColor.R, DObj.disagreeColor.G, DObj.disagreeColor.B);
            ClrPcker_Friendly.SelectedColor = System.Windows.Media.Color.FromArgb(DObj.friendlyColor.A, DObj.friendlyColor.R, DObj.friendlyColor.G, DObj.friendlyColor.B);
            ClrPcker_Hostile.SelectedColor = System.Windows.Media.Color.FromArgb(DObj.hostileColor.A, DObj.hostileColor.R, DObj.hostileColor.G, DObj.hostileColor.B);
            ClrPcker_Entry.SelectedColor = System.Windows.Media.Color.FromArgb(DObj.entryColor.A, DObj.entryColor.R, DObj.entryColor.G, DObj.entryColor.B);
            ClrPcker_EntryPen.SelectedColor = System.Windows.Media.Color.FromArgb(DObj.entryPenColor.A, DObj.entryPenColor.R, DObj.entryPenColor.G, DObj.entryPenColor.B);
            ClrPcker_Reply.SelectedColor = System.Windows.Media.Color.FromArgb(DObj.replyColor.A, DObj.replyColor.R, DObj.replyColor.G, DObj.replyColor.B);
            ClrPcker_ReplyPen.SelectedColor = System.Windows.Media.Color.FromArgb(DObj.replyPenColor.A, DObj.replyPenColor.R, DObj.replyPenColor.G, DObj.replyPenColor.B);
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

        public async Task<bool> CheckProcess(bool waitforfalse, int delay)
        {
            if (!waitforfalse)
            {
                return false;
            }

            await Task.Delay(new TimeSpan( 0, 0, 0, 0, delay));
            return true;
        }


        #endregion Helpers


    }
}