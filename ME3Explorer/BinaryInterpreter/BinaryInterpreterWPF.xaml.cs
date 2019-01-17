using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Be.Windows.Forms;
using Gibbed.IO;
using ME3Explorer;
using ME3Explorer.Packages;
using ME3Explorer.Pathfinding_Editor;
using ME3Explorer.SharedUI;
using ME3Explorer.Soundplorer;
using ME3Explorer.Unreal;
using StreamHelpers;
using static ME3Explorer.BinaryInterpreter;
using static ME3Explorer.EnumExtensions;
using static ME3Explorer.PackageEditorWPF;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for BinaryInterpreterWPF.xaml
    /// </summary>
    public partial class BinaryInterpreterWPF : ExportLoaderControl
    {
        private HexBox BinaryInterpreter_Hexbox;
        BackgroundWorker ScanWorker;

        private string _selectedFileOffset;
        public string SelectedFileOffset
        {
            get { return _selectedFileOffset; }
            set
            {
                if (_selectedFileOffset != value)
                {
                    _selectedFileOffset = value;
                    OnPropertyChanged();
                }
            }
        }

        private int? _byteShiftUpDownValue;
        public int? ByteShiftUpDownValue
        {
            get { return _byteShiftUpDownValue; }
            set
            {
                if (_byteShiftUpDownValue != value)
                {
                    _byteShiftUpDownValue = value;
                    OnPropertyChanged();
                }
            }
        }

        private Visibility _genericEditorSetVisibility;
        public Visibility GenericEditorSetVisibility
        {
            get { return _genericEditorSetVisibility; }
            set
            {
                if (_genericEditorSetVisibility != value)
                {
                    _genericEditorSetVisibility = value;
                    OnPropertyChanged();
                }
            }
        }
        private List<FrameworkElement> EditorSetElements = new List<FrameworkElement>();
        public enum InterpreterMode
        {
            Objects,
            Names,
            Integers,
            Floats
        }

        private InterpreterMode interpreterMode = InterpreterMode.Objects;
        private bool LoadingNewData;

        public BinaryInterpreterWPF()
        {
            ByteShiftUpDownValue = 0;
            InitializeComponent();
            LoadCommands();
            EditorSetElements.Add(Value_TextBox); //str, strref, int, float, obj
            EditorSetElements.Add(Value_ComboBox); //bool, name
            EditorSetElements.Add(NameIndexPrefix_TextBlock); //nameindex
            EditorSetElements.Add(NameIndex_TextBox); //nameindex
            EditorSetElements.Add(ParsedValue_TextBlock);
            EditorSetElements.Add(AddArrayElement_Button);
            EditorSetElements.Add(RemoveArrayElement_Button);

            //EditorSetElements.Add(EditorSet_ArraySetSeparator);
            Set_Button.Visibility = Visibility.Collapsed;
            EditorSet_Separator.Visibility = Visibility.Collapsed;
            GenericEditorSetVisibility = Visibility.Collapsed;
            GenericParsing_ComboBox.ItemsSource = Enum.GetValues(typeof(InterpreterMode)).Cast<InterpreterMode>();
            GenericParsing_ComboBox.SelectedItem = InterpreterMode.Objects;
        }

        #region Commands
        public ICommand CopyOffsetCommand { get; set; }
        public ObservableCollectionExtended<IndexedName> ParentNameList { get; private set; }

        private void LoadCommands()
        {
            CopyOffsetCommand = new RelayCommand(CopyFileOffsetToClipboard, OffsetIsSelected);
        }

        private void CopyFileOffsetToClipboard(object obj)
        {
            Clipboard.SetText(SelectedFileOffset);
        }

        private bool OffsetIsSelected(object obj)
        {
            return BinaryInterpreter_Hexbox.SelectionStart >= 0;
        }
        #endregion

        static readonly string[] ParsableBinaryClasses = { "Level", "StaticMeshCollectionActor", "StaticLightCollectionActor", "ShaderCache", "Class", "BioStage", "ObjectProperty", "Const",
            "Enum", "ArrayProperty","FloatProperty", "StructProperty", "ComponentProperty", "IntProperty", "NameProperty", "BoolProperty", "ClassProperty", "ByteProperty","Enum","ObjectRedirector", "WwiseEvent", "Material", "StaticMesh", "MaterialInstanceConstant",
            "BioDynamicAnimSet", "StaticMeshComponent", "SkeletalMeshComponent", "SkeletalMesh", "PrefabInstance", "MetaData",
            "WwiseStream", "WwiseBank", "TextureMovie", "GuidCache", "World", "Texture2D", "State", "BioGestureRuntimeData", "BioTlkFileSet", "ScriptStruct", "SoundCue", "SoundNodeWave","BioSoundNodeWaveStreamingData"};

        public override bool CanParse(IExportEntry exportEntry)
        {
            return ParsableBinaryClasses.Contains(exportEntry.ClassName) && !exportEntry.ObjectName.StartsWith("Default__");
        }

        public override void LoadExport(IExportEntry exportEntry)
        {
            LoadingNewData = true;
            ByteShift_UpDown.Value = 0;
            CurrentLoadedExport = exportEntry;

            OnDemand_Panel.Visibility = Visibility.Visible;
            LoadedContent_Panel.Visibility = Visibility.Collapsed;
            if (CurrentLoadedExport.Data.Length < 20480 || Properties.Settings.Default.BinaryInterpreterWPFAutoScanAlways)
            {
                StartBinaryScan();
            }
            else
            {
                ParseBinary_Button.Visibility = Visibility.Visible;
                ParseBinary_Spinner.Visibility = Visibility.Collapsed;
                OnDemand_Title_TextBlock.Text = "This export is larger than 20KB";
                OnDemand_Subtext_TextBlock.Text = "Large exports are not automatically parsed to improve performance";
            }
            LoadingNewData = false; //technically not true, since it's background thread. However, for the purposes of resetting controls, it is done loading.
        }

        #region static stuff
        public enum NodeType
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

        #endregion

        private void StartBinaryScan()
        {
            if (CurrentLoadedExport is null)
            {
                return;
            }
            OnDemand_Title_TextBlock.Text = "Parsing binary";
            OnDemand_Subtext_TextBlock.Text = "Please wait";
            ParseBinary_Button.Visibility = Visibility.Collapsed;
            ParseBinary_Spinner.Visibility = Visibility.Visible;
            DynamicByteProvider db = new DynamicByteProvider(CurrentLoadedExport.Data);
            BinaryInterpreter_Hexbox.ByteProvider = db;
            byte[] data = CurrentLoadedExport.Data;
            int binarystart = 0;
            if (CurrentLoadedExport.ClassName != "Class")
            {
                binarystart = CurrentLoadedExport.propsEnd();
            }

            //top node will always be of this element type.
            BinaryInterpreterWPFTreeViewItem topLevelTree = new BinaryInterpreterWPFTreeViewItem
            {
                Header = $"{binarystart:X4} : {CurrentLoadedExport.ObjectName} - Binary start",
                Tag = NodeType.Root,
                Name = "_" + binarystart,
                IsExpanded = true
            };
            //BinaryInterpreter_TreeView.Items.Add(topLevelTree);

            ScanWorker = new BackgroundWorker();
            ScanWorker.WorkerSupportsCancellation = true;
            ScanWorker.DoWork += PerformScanBackground;
            ScanWorker.WorkerReportsProgress = true;
            ScanWorker.RunWorkerCompleted += PerformScan_Completed;
            //We will not modify topleveltree in background thread, however we will pass it through to the completed method.
            ScanWorker.RunWorkerAsync(new Tuple<BinaryInterpreterWPFTreeViewItem, byte[], int>(topLevelTree, data, binarystart));
        }

        private void PerformScan_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            var result = (BinaryInterpreterWPFTreeViewItem)e.Result;
            OnDemand_Panel.Visibility = Visibility.Collapsed;
            LoadedContent_Panel.Visibility = Visibility.Visible;
            BinaryInterpreter_TreeView.ItemsSource = new List<object>(new BinaryInterpreterWPFTreeViewItem[] { result });
        }

        private void PerformScanBackground(object sender, DoWorkEventArgs e)
        {
            List<object> subNodes = null;
            Tuple<BinaryInterpreterWPFTreeViewItem, byte[], int> arguments = (Tuple<BinaryInterpreterWPFTreeViewItem, byte[], int>)e.Argument;
            byte[] data = arguments.Item2;
            int binarystart = arguments.Item3;
            bool isGenericScan = false;
            bool appendGenericScan = false;
            switch (CurrentLoadedExport.ClassName)
            {
                case "IntProperty":
                case "BoolProperty":
                case "ArrayProperty":
                case "FloatProperty":
                case "ClassProperty":
                case "ByteProperty":
                case "NameProperty":
                case "StructProperty":
                case "ComponentProperty":
                case "ObjectProperty":
                    subNodes = StartObjectScan(data);
                    break;
                case "BioDynamicAnimSet":
                    subNodes = StartBioDynamicAnimSetScan(data, ref binarystart);
                    break;
                case "ObjectRedirector":
                    subNodes = StartObjectRedirectorScan(data, ref binarystart);
                    break;
                case "MetaData":
                    subNodes = StartMetaDataScan(data, ref binarystart);
                    break;
                case "WwiseStream":
                case "WwiseBank":
                    subNodes = Scan_WwiseStreamBank(data);
                    break;
                case "WwiseEvent":
                    subNodes = Scan_WwiseEvent(data, ref binarystart);
                    break;
                case "BioStage":
                    subNodes = StartBioStageScan(data, ref binarystart);
                    break;
                case "BioTlkFileSet":
                    subNodes = StartBioTlkFileSetScan(data, ref binarystart);
                    break;
                case "Class":
                    subNodes = StartClassScan(data);
                    break;
                case "Enum":
                case "Const":
                    subNodes = StartEnumScan(data);
                    break;
                case "GuidCache":
                    subNodes = StartGuidCacheScan(data, ref binarystart);
                    break;
                case "Level":
                    subNodes = StartLevelScan(data, ref binarystart);
                    appendGenericScan = true;
                    break;
                case "Material":
                case "MaterialInstanceConstant":
                    subNodes = StartMaterialScan(data, ref binarystart);
                    break;
                case "PrefabInstance":
                    subNodes = StartPrefabInstanceScan(data, ref binarystart);
                    break;
                case "SkeletalMesh":
                    subNodes = StartSkeletalMeshScan(data, ref binarystart);
                    break;
                case "StaticMeshCollectionActor":
                    subNodes = StartStaticMeshCollectionActorScan(data, ref binarystart);
                    break;
                case "StaticMesh":
                    subNodes = StartStaticMeshScan(data, ref binarystart);
                    break;
                case "Texture2D":
                    subNodes = StartTextureBinaryScan(data);
                    break;
                case "State":
                    subNodes = StartStateScan(data, ref binarystart);
                    break;
                case "TextureMovie":
                    subNodes = StartTextureMovieScan(data, ref binarystart);
                    break;
                case "BioGestureRuntimeData":
                    subNodes = StartBioGestureRuntimeDataScan(data, ref binarystart);
                    break;
                case "ScriptStruct":
                    subNodes = StartScriptStructScan(data, ref binarystart);
                    break;
                case "SoundCue":
                    subNodes = StartSoundCueScan(data, ref binarystart);
                    break;
                case "BioSoundNodeWaveStreamingData":
                    subNodes = StartBioSoundNodeWaveStreamingDataScan(data, ref binarystart);
                    break;
                case "SoundNodeWave":
                    subNodes = StartSoundNodeWaveScan(data, ref binarystart);
                    break;
                default:
                    isGenericScan = true;
                    subNodes = StartGenericScan(data, ref binarystart);
                    break;
            }
            if (appendGenericScan)
            {
                BinaryInterpreterWPFTreeViewItem genericContainer = new BinaryInterpreterWPFTreeViewItem() { Header = $"Generic scan data", IsExpanded = true };
                subNodes.Add(genericContainer);
                genericContainer.Items.AddRange(StartGenericScan(data, ref binarystart));
            }
            GenericEditorSetVisibility = (appendGenericScan || isGenericScan) ? Visibility.Visible : Visibility.Collapsed;
            arguments.Item1.Items = subNodes;
            e.Result = arguments.Item1; //return topLevelTree
        }

        private List<object> StartMetaDataScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<object>();
            try
            {
                int offset = binarystart;

                int count = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Metadata String Count: {count}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                MemoryStream ms = new MemoryStream(data);
                ms.Position = offset;
                for (int i = 0; i < count; i++)
                {
                    offset = (int)ms.Position;

                    string label = null;
                    if (i % 2 == 1)
                    {
                        var postint = ms.ReadInt32();
                        var nameIdx = ms.ReadInt32();
                        label = CurrentLoadedExport.FileRef.getNameEntry(nameIdx);
                        ms.ReadInt32();
                    }

                    var strLen = ms.ReadUInt32();
                    var line = ms.ReadString(strLen, true, Encoding.ASCII);
                    if (label != null)
                    {
                        subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                        {
                            Header = $"0x{offset:X6}    {label}:\n{line}\n",
                            Name = "_" + offset,
                            Tag = NodeType.None
                        });
                    }
                    else
                    {
                        subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                        {
                            Header = $"0x{offset:X6} {line}",
                            Name = "_" + offset,
                            Tag = NodeType.None
                        });
                    }
                }
                /*
                offset = binarystart + 0x18;

                MemoryStream ms = new MemoryStream(data);
                ms.Position = offset;
                var scriptStructProperties = PropertyCollection.ReadProps(CurrentLoadedExport.FileRef, ms, "ScriptStruct", includeNoneProperty: true);

                UPropertyTreeViewEntry topLevelTree = new UPropertyTreeViewEntry(); //not used, just for holding and building data.
                foreach (UProperty prop in scriptStructProperties)
                {
                    InterpreterWPF.GenerateUPropertyTreeForProperty(prop, topLevelTree, CurrentLoadedExport);
                }*/
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<object> StartBioTlkFileSetScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<object>();
            try
            {
                int offset = binarystart;
                if (data.Length > binarystart)
                {
                    int count = BitConverter.ToInt32(data, offset);
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{offset:X4} Count: {count}",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    //offset += 4;
                    //offset += 8; //skip 8

                    for (int i = 0; i < count; i++)
                    {
                        int langRef = BitConverter.ToInt32(data, offset);
                        int langTlkCount = BitConverter.ToInt32(data, offset + 8);
                        var languageNode = new BinaryInterpreterWPFTreeViewItem
                        {
                            Header = $"0x{offset:X4} {CurrentLoadedExport.FileRef.getNameEntry(langRef)} - {langTlkCount} entries",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafName,
                            IsExpanded = true
                        };
                        subnodes.Add(languageNode);
                        offset += 12;

                        for (int k = 0; k < langTlkCount; k++)
                        {
                            int tlkIndex = BitConverter.ToInt32(data, offset); //-1 in reader
                            languageNode.Items.Add(new BinaryInterpreterWPFTreeViewItem
                            {
                                Header = $"0x{offset:X4} TLK #{k} export: {tlkIndex} {CurrentLoadedExport.FileRef.GetEntryString(tlkIndex)}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafObject
                            });
                            offset += 4;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<object> StartSoundNodeWaveScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<object>();
            try
            {
                int offset = binarystart;


                int classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X8} Item1: {classObjTree} (0x{classObjTree:X8})",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X8} Data length: {classObjTree} (0x{classObjTree:X8})",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X8} Data length: {classObjTree} (0x{classObjTree:X8})",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X8} Item4: {classObjTree} (0x{classObjTree:X8})",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;
                /*
                offset = binarystart + 0x18;

                MemoryStream ms = new MemoryStream(data);
                ms.Position = offset;
                var scriptStructProperties = PropertyCollection.ReadProps(CurrentLoadedExport.FileRef, ms, "ScriptStruct", includeNoneProperty: true);

                UPropertyTreeViewEntry topLevelTree = new UPropertyTreeViewEntry(); //not used, just for holding and building data.
                foreach (UProperty prop in scriptStructProperties)
                {
                    InterpreterWPF.GenerateUPropertyTreeForProperty(prop, topLevelTree, CurrentLoadedExport);
                }*/
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<object> StartBioSoundNodeWaveStreamingDataScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<object>();
            try
            {
                int offset = binarystart;


                int numBytesOfStreamingData = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Streaming Data Size: {numBytesOfStreamingData}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                var nextFileOffset = BitConverter.ToInt32(data, offset);
                var node = new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Next file offset: {nextFileOffset}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                };

                var clickToGotoOffset = new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Click to go to referenced offset 0x{nextFileOffset:X5}",
                    Name = "_" + nextFileOffset
                };
                node.Items.Add(clickToGotoOffset);

                subnodes.Add(node);
                offset += 4;

                MemoryStream asStream = new MemoryStream(data);
                asStream.Position = offset;

                while (asStream.Position < asStream.Length)
                {
                    Debug.WriteLine("Reading at " + asStream.Position);
                    ISACT_Parser.ReadStream(asStream);
                }
                /*
                offset = binarystart + 0x18;

                MemoryStream ms = new MemoryStream(data);
                ms.Position = offset;
                var scriptStructProperties = PropertyCollection.ReadProps(CurrentLoadedExport.FileRef, ms, "ScriptStruct", includeNoneProperty: true);

                UPropertyTreeViewEntry topLevelTree = new UPropertyTreeViewEntry(); //not used, just for holding and building data.
                foreach (UProperty prop in scriptStructProperties)
                {
                    InterpreterWPF.GenerateUPropertyTreeForProperty(prop, topLevelTree, CurrentLoadedExport);
                }*/
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<object> StartSoundCueScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<object>();
            try
            {
                int offset = binarystart;


                int classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} NextItemCompilingChain: {classObjTree} {CurrentLoadedExport.FileRef.GetEntryString(classObjTree)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;
                /*
                offset = binarystart + 0x18;

                MemoryStream ms = new MemoryStream(data);
                ms.Position = offset;
                var scriptStructProperties = PropertyCollection.ReadProps(CurrentLoadedExport.FileRef, ms, "ScriptStruct", includeNoneProperty: true);

                UPropertyTreeViewEntry topLevelTree = new UPropertyTreeViewEntry(); //not used, just for holding and building data.
                foreach (UProperty prop in scriptStructProperties)
                {
                    InterpreterWPF.GenerateUPropertyTreeForProperty(prop, topLevelTree, CurrentLoadedExport);
                }*/
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        internal void SetHexboxSelectedOffset(int v)
        {
            if (BinaryInterpreter_Hexbox != null)
            {
                BinaryInterpreter_Hexbox.SelectionStart = v;
                BinaryInterpreter_Hexbox.SelectionLength = 1;
            }
        }
        #region scans
        private List<object> StartScriptStructScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<object>();
            try
            {
                int offset = binarystart + 0x4;

                int classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} NextItemCompilingChain: {classObjTree} {CurrentLoadedExport.FileRef.GetEntryString(classObjTree)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                int childObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} ChildCompilingChain: {childObjTree} {CurrentLoadedExport.FileRef.GetEntryString(childObjTree)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                offset = binarystart + (CurrentLoadedExport.FileRef.Game == MEGame.ME3 ? 0x18 : 0x24);

                MemoryStream ms = new MemoryStream(data);
                ms.Position = offset;
                var scriptStructProperties = PropertyCollection.ReadProps(CurrentLoadedExport.FileRef, ms, "ScriptStruct", includeNoneProperty: true);

                UPropertyTreeViewEntry topLevelTree = new UPropertyTreeViewEntry(); //not used, just for holding and building data.
                foreach (UProperty prop in scriptStructProperties)
                {
                    InterpreterWPF.GenerateUPropertyTreeForProperty(prop, topLevelTree, CurrentLoadedExport);
                }
                subnodes.AddRange(topLevelTree.ChildrenProperties.Cast<object>().ToList());

                //subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                //{
                //    Header = $"{offset:X4} Name: {CurrentLoadedExport.FileRef.getNameEntry(entryUIndex)}",
                //    Name = "_" + offset.ToString()
                //});
                //offset += 12;


                /*
                for (int i = 0; i < count; i++)
                {
                    int name1 = BitConverter.ToInt32(data, offset);
                    int name2 = BitConverter.ToInt32(data, offset + 8);
                    string text = $"{offset:X4} Item {i}: {CurrentLoadedExport.FileRef.getNameEntry(name1)} => {CurrentLoadedExport.FileRef.getNameEntry(name2)}";
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = text,
                        Name = "_" + offset.ToString()
                    });
                    offset += 16;
                }*/
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<object> StartBioGestureRuntimeDataScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<object>();
            try
            {
                int offset = binarystart;
                int count = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{offset:X4} Count: {count}",
                    Name = "_" + offset.ToString()
                });
                offset += 4;
                for (int i = 0; i < count; i++)
                {
                    int name1 = BitConverter.ToInt32(data, offset);
                    int name2 = BitConverter.ToInt32(data, offset + 8);
                    string text = $"{offset:X4} Item {i}: {CurrentLoadedExport.FileRef.getNameEntry(name1)} => {CurrentLoadedExport.FileRef.getNameEntry(name2)}";
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = text,
                        Name = "_" + offset.ToString()
                    });
                    offset += 16;
                }

                int idx = 0;
                MemoryStream dataAsStream = new MemoryStream(data);
                while (offset < data.Length)
                {
                    var node = new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"{offset:X4} Item {idx}",
                        Name = "_" + offset.ToString()
                    };
                    subnodes.Add(node);

                    int unk1 = BitConverter.ToInt32(data, offset);
                    node.Items.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"{offset:X4} Unk1: {unk1}",
                        Name = "_" + offset.ToString()
                    });
                    offset += 4;
                    int unk2 = BitConverter.ToInt32(data, offset);
                    node.Items.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"{offset:X4} Name Unk2: {unk2} {CurrentLoadedExport.FileRef.getNameEntry(unk2)}",
                        Name = "_" + offset.ToString()
                    });
                    offset += 8;
                    int unk3 = BitConverter.ToInt32(data, offset);
                    node.Items.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"{offset:X4} Name Unk3: {unk3} {CurrentLoadedExport.FileRef.getNameEntry(unk3)}",
                        Name = "_" + offset.ToString()
                    });
                    offset += 8;

                    dataAsStream.Position = offset;
                    int strLength = dataAsStream.ReadValueS32();
                    string str = dataAsStream.ReadString(strLength * -2, true, Encoding.Unicode);
                    node.Items.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"{offset:X4}: {str}",
                        Name = "_" + offset.ToString()
                    });
                    offset = (int)dataAsStream.Position;
                    int unk4 = BitConverter.ToInt32(data, offset);
                    node.Items.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"{offset:X4} Name Unk4: {unk4} {CurrentLoadedExport.FileRef.getNameEntry(unk4)}",
                        Name = "_" + offset.ToString()
                    });
                    offset += 8;
                    idx++;
                    break;
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }
        private List<object> StartObjectRedirectorScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<object>();

            int redirnum = BitConverter.ToInt32(data, binarystart);
            subnodes.Add(new BinaryInterpreterWPFTreeViewItem
            {
                Header = $"{binarystart:X4} Redirect references to this export to: {redirnum} {CurrentLoadedExport.FileRef.getEntry(redirnum).GetFullPath}",
                Name = "_" + binarystart.ToString()
            });
            return subnodes;
        }

        private List<object> StartObjectScan(byte[] data)
        {
            var subnodes = new List<object>();
            try
            {
                int offset = 0; //this property starts at 0 for parsing
                int unrealExportIndex = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Unreal Unique Index: {unrealExportIndex}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                int noneUnrealProperty = BitConverter.ToInt32(data, offset);
                //int noneUnrealPropertyIndex = BitConverter.ToInt32(data, offset + 4);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Unreal property None Name: {CurrentLoadedExport.FileRef.getNameEntry(noneUnrealProperty)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafName
                });
                offset += 8;

                int superclassIndex = BitConverter.ToInt32(data, offset);
                string superclassStr = CurrentLoadedExport.FileRef.GetEntryString(superclassIndex);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Superclass: {superclassIndex}({superclassStr})",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                int classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Next item in compiling chain UIndex: {classObjTree} {CurrentLoadedExport.FileRef.GetEntryString(classObjTree)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                int unk1 = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Unknown 1: {unk1}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                UnrealFlags.EPropertyFlags ObjectFlagsMask = (UnrealFlags.EPropertyFlags)BitConverter.ToUInt64(data, offset);
                BinaryInterpreterWPFTreeViewItem objectFlagsNode = new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} ObjectFlags: 0x{(ulong)ObjectFlagsMask:X16}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                };

                subnodes.Add(objectFlagsNode);

                //Create objectflags tree
                foreach (UnrealFlags.EPropertyFlags flag in GetValues<UnrealFlags.EPropertyFlags>())
                {
                    if ((ObjectFlagsMask & flag) != UnrealFlags.EPropertyFlags.None)
                    {
                        string reason = UnrealFlags.propertyflagsdesc[flag];
                        objectFlagsNode.Items.Add(new BinaryInterpreterWPFTreeViewItem
                        {
                            Header = $"{(ulong)flag:X16} {flag} {reason}",
                            Name = "_" + offset
                        });
                    }
                }
                offset += 8;



                //has listed outerclass
                int none = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} None: {CurrentLoadedExport.FileRef.getNameEntry(none)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 8;

                int unk2 = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Unknown2: {unk2}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4; //

                switch (CurrentLoadedExport.ClassName)
                {
                    case "ByteProperty":
                    case "StructProperty":
                    case "ObjectProperty":
                    case "ComponentProperty":
                        {
                            if ((ObjectFlagsMask & UnrealFlags.EPropertyFlags.RepRetry) != 0)
                            {
                                offset += 2;
                            }
                            //has listed outerclass
                            int outer = BitConverter.ToInt32(data, offset);
                            subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                            {
                                Header = $"0x{offset:X5} OuterClass: {outer} {CurrentLoadedExport.FileRef.GetEntryString(outer)}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;
                        }
                        break;
                    case "ArrayProperty":
                        {
                            int outer = BitConverter.ToInt32(data, offset);
                            subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                            {
                                Header = $"0x{offset:X5} Array can hold objects of type: {outer} {CurrentLoadedExport.FileRef.GetEntryString(outer)}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;
                        }
                        break;
                    case "ClassProperty":
                        {

                            //has listed outerclass
                            int outer = BitConverter.ToInt32(data, offset);
                            subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                            {
                                Header = $"0x{offset:X5} Outer class: {outer} {CurrentLoadedExport.FileRef.GetEntryString(outer)}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            //type of class
                            int classtype = BitConverter.ToInt32(data, offset);
                            subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                            {
                                Header = $"0x{offset:X5} Class type: {classtype} {CurrentLoadedExport.FileRef.GetEntryString(classtype)}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafObject
                            });
                            offset += 4;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<object> Scan_WwiseStreamBank(byte[] data)
        {
            /*
             * int32 0?
             * stream length in AFC +4 | (bank size)
             * stream length in AFC +4 | (repeat) (bank size)
             * stream offset in AFC +4 | (bank offset in file)
             */
            var subnodes = new List<object>();
            try
            {
                int pos = 0;
                switch (CurrentLoadedExport.FileRef.Game)
                {
                    case MEGame.ME3:
                        pos = CurrentLoadedExport.propsEnd();
                        break;
                    case MEGame.ME2:
                        pos = CurrentLoadedExport.propsEnd() + 0x20;
                        break;
                }

                int unk1 = BitConverter.ToInt32(data, pos);
                int DataSize = BitConverter.ToInt32(data, pos + 4);
                int DataSize2 = BitConverter.ToInt32(data, pos + 8);
                int DataOffset = BitConverter.ToInt32(data, pos + 0xC);

                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{pos:X4} Unknown: {unk1}",
                    Name = "_" + pos,
                });
                pos += 4;
                string dataset1type = CurrentLoadedExport.ClassName == "WwiseStream" ? "Stream length" : "Bank size";
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{DataSize:X4} : {dataset1type} {DataSize} (0x{DataSize:X})",
                    Name = "_" + pos,
                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{ pos:X4} {dataset1type}: {DataSize2} (0x{ DataSize2:X})",
                    Name = "_" + pos,
                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                string dataset2type = CurrentLoadedExport.ClassName == "WwiseStream" ? "Stream offset" : "Bank offset";
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{pos:X4} {dataset2type} in file: {DataOffset} (0x{DataOffset:X})",
                    Name = "_" + pos,
                    Tag = NodeType.StructLeafInt
                });

                if (CurrentLoadedExport.ClassName == "WwiseBank")
                {
                    //if (CurrentLoadedExport.DataOffset < DataOffset && (CurrentLoadedExport.DataOffset + CurrentLoadedExport.DataSize) < DataOffset)
                    //{
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = "Click here to jump to the calculated end offset of wwisebank in this export",
                        Name = "_" + (DataSize2 + CurrentLoadedExport.propsEnd() + 16),
                        Tag = NodeType.Unknown
                    });
                    //}
                }

                pos += 4;
                switch (CurrentLoadedExport.ClassName)
                {
                    case "WwiseStream" when pos < data.Length && CurrentLoadedExport.GetProperty<NameProperty>("Filename") == null:
                        {
                            subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                            {
                                Header = $"{pos:X4} Embedded sound data. Use Soundplorer to modify this data.",
                                Name = "_" + pos,
                                Tag = NodeType.Unknown
                            });
                            subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                            {
                                Header = "The stream offset to this data will be automatically updated when this file is saved.",
                                Tag = NodeType.Unknown
                            });
                            break;
                        }
                    case "WwiseBank":
                        subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                        {
                            Header = $"{pos:X4} Embedded soundbank. Use Soundplorer WPF to view data.",
                            Name = "_" + pos,
                            Tag = NodeType.Unknown
                        });
                        subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                        {
                            Header = "The bank offset to this data will be automatically updated when this file is saved.",
                            Tag = NodeType.Unknown
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<object> Scan_WwiseEvent(byte[] data, ref int binarystart)
        {
            var subnodes = new List<object>();
            if (CurrentLoadedExport.FileRef.Game != MEGame.ME3)
            {
                subnodes.Add("Only ME3 is supported for this scan.");
                return subnodes;
            }
            try
            {
                int binarypos = binarystart;
                int count = BitConverter.ToInt32(data, binarypos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem { Header = $"0x{binarypos:X4} Count: {count.ToString()}", Name = "_" + binarypos });
                binarypos += 4; //+ int
                for (int i = 0; i < count; i++)
                {
                    string nodeText = $"0x{binarypos:X4} ";
                    int val = BitConverter.ToInt32(data, binarypos);
                    string name = val.ToString();
                    if (val > 0 && val <= CurrentLoadedExport.FileRef.Exports.Count)
                    {
                        IExportEntry exp = CurrentLoadedExport.FileRef.Exports[val - 1];
                        nodeText += $"{i}: {name} {exp.PackageFullName}.{exp.ObjectName} ({exp.ClassName})";
                    }
                    else if (val < 0 && val != int.MinValue && Math.Abs(val) <= CurrentLoadedExport.FileRef.Imports.Count)
                    {
                        int csImportVal = Math.Abs(val) - 1;
                        ImportEntry imp = CurrentLoadedExport.FileRef.Imports[csImportVal];
                        nodeText += $"{i}: {name} {imp.PackageFullName}.{imp.ObjectName} ({imp.ClassName})";
                    }

                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = nodeText,
                        Tag = NodeType.StructLeafObject,
                        Name = "_" + binarypos
                    });
                    binarypos += 4;
                    /*
                    int objectindex = BitConverter.ToInt32(data, binarypos);
                    IEntry obj = pcc.getEntry(objectindex);
                    string nodeValue = obj.GetFullPath;
                    node.Tag = nodeType.StructLeafObject;
                    */
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<object> StartBioDynamicAnimSetScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<object>();

            try
            {
                int binarypos = binarystart;
                int count = BitConverter.ToInt32(data, binarypos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{binarypos:X4} Count: {count.ToString()}"
                });
                binarypos += 4; //+ int
                for (int i = 0; i < count; i++)
                {
                    int nameIndex = BitConverter.ToInt32(data, binarypos);
                    int nameIndexNum = BitConverter.ToInt32(data, binarypos + 4);
                    int shouldBe1 = BitConverter.ToInt32(data, binarypos + 8);

                    //TODO: Relink this property on package porting!
                    var name = CurrentLoadedExport.FileRef.getNameEntry(nameIndex);
                    string nodeValue = $"{(name == "INVALID NAME VALUE " + nameIndex ? "" : name)}_{nameIndexNum}";
                    if (shouldBe1 != 1)
                    {
                        //ERROR
                        nodeValue += " - Not followed by 1 (integer)!";
                    }

                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{binarypos:X4} Name: {nodeValue}",
                        Tag = NodeType.StructLeafName,
                        Name = $"_{binarypos.ToString()}"
                    });
                    binarypos += 12;
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        //TODO: unfinished. currently does not display the properties for the list of BioStageCamera objects at the end
        private List<object> StartBioStageScan(byte[] data, ref int binarystart)
        {
            /*
             * Length (int)
                Name: m_aCameraList
                int unknown 0
                Count + int unknown
                [Camera name
                    unreal property data]*/
            var subnodes = new List<object>();
            //if ((CurrentLoadedExport.Header[0x1f] & 0x2) != 0)
            {

                int pos = binarystart;
                int length = BitConverter.ToInt32(data, binarystart);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{binarystart:X4} Length: {length}",
                    Name = $"_{pos.ToString()}"
                });
                pos += 4;
                if (length != 0)
                {
                    int nameindex = BitConverter.ToInt32(data, pos);
                    int nameindexunreal = BitConverter.ToInt32(data, pos + 4);

                    string name = CurrentLoadedExport.FileRef.getNameEntry(nameindex);
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"{pos:X4} Camera: {name}_{nameindexunreal}",
                        Name = $"_{pos.ToString()}",
                        Tag = NodeType.StructLeafName
                    });

                    pos += 8;
                    int shouldbezero = BitConverter.ToInt32(data, pos);
                    if (shouldbezero != 0)
                    {
                        Debug.WriteLine($"NOT ZERO FOUND: {pos}");
                    }
                    pos += 4;

                    int count = BitConverter.ToInt32(data, pos);
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"{pos:X4} Count: {count}",
                        Name = $"_{pos.ToString()}"
                    });
                    pos += 4;

                    shouldbezero = BitConverter.ToInt32(data, pos);
                    if (shouldbezero != 0)
                    {
                        Debug.WriteLine($"NOT ZERO FOUND: {pos}");
                    }
                    pos += 4;
                    try
                    {
                        var stream = new MemoryStream(data);
                        for (int i = 0; i < count; i++)
                        {
                            nameindex = BitConverter.ToInt32(data, pos);
                            nameindexunreal = BitConverter.ToInt32(data, pos + 4);
                            BinaryInterpreterWPFTreeViewItem parentnode = new BinaryInterpreterWPFTreeViewItem
                            {
                                Header = $"{pos:X4} Camera {i + 1}: {CurrentLoadedExport.FileRef.getNameEntry(nameindex)}_{nameindexunreal}",
                                Tag = NodeType.StructLeafName,
                                Name = $"_{pos.ToString()}"
                            };
                            subnodes.Add(parentnode);
                            pos += 8;
                            stream.Seek(pos, SeekOrigin.Begin);
                            var props = PropertyCollection.ReadProps(CurrentLoadedExport.FileRef, stream, "BioStageCamera", includeNoneProperty: true);

                            UPropertyTreeViewEntry topLevelTree = new UPropertyTreeViewEntry(); //not used, just for holding and building data.
                            foreach (UProperty prop in props)
                            {
                                InterpreterWPF.GenerateUPropertyTreeForProperty(prop, topLevelTree, CurrentLoadedExport);
                            }
                            subnodes.AddRange(topLevelTree.ChildrenProperties.Cast<object>().ToList());

                            //finish writing function here
                            pos = props.endOffset;

                        }
                    }
                    catch (Exception ex)
                    {
                        subnodes.Add(new BinaryInterpreterWPFTreeViewItem { Header = $"Error reading binary data: {ex}" });
                    }
                }
            }
            return subnodes;
        }

        private List<object> StartClassScan(byte[] data)
        {
            //const int nonTableEntryCount = 2; //how many items we parse that are not part of the functions table. e.g. the count, the defaults pointer
            var subnodes = new List<object>();
            try
            {
                int offset = 0;

                int unrealExportIndex = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Unreal Unique Index: {unrealExportIndex}",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;


                int superclassIndex = BitConverter.ToInt32(data, offset);
                string superclassStr = CurrentLoadedExport.FileRef.GetEntryString(superclassIndex);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Superclass Index: {superclassIndex}({superclassStr})",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                int unknown1 = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Unknown 1: {unknown1}",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                int classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} ProbeMask/Class Object Tree Final Pointer Index: {classObjTree}",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;


                //I am not sure what these mean. However if Pt1&2 are 33/25, the following bytes that follow are extended.
                //int headerUnknown1 = BitConverter.ToInt32(data, offset);
                Int64 ignoreMask = BitConverter.ToInt64(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} IgnoreMask: 0x{ignoreMask:X16}",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 8;

                Int16 labelOffset = BitConverter.ToInt16(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} LabelOffset: 0x{labelOffset:X4}",
                    Name = "_" + offset

                });
                offset += 2;

                int skipAmount = 0x6;
                //Find end of script block. Seems to be 10 FF's.
                while (offset + skipAmount + 10 < data.Length)
                {
                    //Debug.WriteLine($"Checking at 0x{offset + skipAmount + 10:X4}");
                    bool isEnd = true;
                    for (int i = 0; i < 10; i++)
                    {
                        byte b = data[offset + skipAmount + i];
                        if (b != 0xFF)
                        {
                            isEnd = false;
                            break;
                        }
                    }
                    if (isEnd)
                    {
                        break;
                    }
                    skipAmount++;
                }
                //if (headerUnknown1 == 33 && headerUnknown2 == 25)
                //{
                //    skipAmount = 0x2F;
                //}
                //else if (headerUnknown1 == 34 && headerUnknown2 == 26)
                //{
                //    skipAmount = 0x30;
                //}
                //else if (headerUnknown1 == 728 && headerUnknown2 == 532)
                //{
                //    skipAmount = 0x22A;
                //}
                int offsetEnd = offset + skipAmount + 10;
                var scriptBlock = new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} State/Script Block: 0x{offset:X4} - 0x{offsetEnd:X4}",
                    Name = "_" + offset
                };
                subnodes.Add(scriptBlock);

                if (CurrentLoadedExport.FileRef.Game == MEGame.ME3 && skipAmount > 6)
                {
                    byte[] scriptmemory = data.Skip(offset).Take(skipAmount).ToArray();
                    var tokens = Bytecode.ParseBytecode(scriptmemory, CurrentLoadedExport.FileRef, offset);
                    string scriptText = "";
                    foreach (Token t in tokens.Item1)
                    {
                        scriptText += "0x" + t.pos.ToString("X4") + " " + t.text + "\n";
                    }
                    scriptBlock.Items.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = scriptText,
                        Name = "_" + offset
                    });
                }


                offset += skipAmount + 10; //heuristic to find end of script
                                           //for (int i = 0; i < 5; i++)
                                           //{
                uint stateMask = BitConverter.ToUInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Statemask: {stateMask} [{getStateFlagsStr(stateMask)}]",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;
                //}
                //offset += 2; //oher unknown
                int localFunctionsTableCount = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Local Functions Count: {localFunctionsTableCount}",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;
                for (int i = 0; i < localFunctionsTableCount; i++)
                {
                    int nameTableIndex = BitConverter.ToInt32(data, offset);
                    //int nameIndex = BitConverter.ToInt32(data, offset + 4);
                    offset += 8;
                    int functionObjectIndex = BitConverter.ToInt32(data, offset);
                    offset += 4;
                    (subnodes.Last() as BinaryInterpreterWPFTreeViewItem).Items.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{offset - 12:X5}  {CurrentLoadedExport.FileRef.getNameEntry(nameTableIndex)}() = {functionObjectIndex} ({CurrentLoadedExport.FileRef.GetEntryString(functionObjectIndex)})",
                        Name = "_" + (offset - 12),
                        Tag = NodeType.StructLeafName //might need to add a subnode for the 3rd int
                    });
                }

                UnrealFlags.EClassFlags ClassFlags = (UnrealFlags.EClassFlags)BitConverter.ToUInt32(data, offset);

                var classFlagsNode = new BinaryInterpreterWPFTreeViewItem()
                {
                    Header = $"0x{offset:X5} Class Mask: 0x{ClassFlags.ToString():X8}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                };
                subnodes.Add(classFlagsNode);

                //Create claskmask tree
                foreach (UnrealFlags.EClassFlags flag in GetValues<UnrealFlags.EClassFlags>())
                {
                    if ((ClassFlags & flag) != UnrealFlags.EClassFlags.None)
                    {
                        string reason = UnrealFlags.classflagdesc[flag];
                        classFlagsNode.Items.Add(new BinaryInterpreterWPFTreeViewItem
                        {
                            Header = $"{(ulong)flag:X16} {flag} {reason}",
                            Name = "_" + offset
                        });
                    }
                }
                offset += 4;

                if (CurrentLoadedExport.FileRef.Game != MEGame.ME3)
                {
                    offset += 1; //seems to be a blank byte here
                }

                int coreReference = BitConverter.ToInt32(data, offset);
                string coreRefFullPath = CurrentLoadedExport.FileRef.GetEntryString(coreReference);

                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Outer Class: {coreReference} ({coreRefFullPath})",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafObject
                });
                offset += 4;


                if (CurrentLoadedExport.FileRef.Game == MEGame.ME3)
                {
                    offset = ClassParser_ReadComponentsTable(subnodes, data, offset);
                    offset = ClassParser_ReadImplementsTable(subnodes, data, offset);
                    int postComponentsNoneNameIndex = BitConverter.ToInt32(data, offset);
                    //int postComponentNoneIndex = BitConverter.ToInt32(data, offset + 4);
                    string postCompName = CurrentLoadedExport.FileRef.getNameEntry(postComponentsNoneNameIndex); //This appears to be unused in ME#, it is always None it seems.
                                                                                                                 /*if (postCompName != "None")
                                                                                                                 {
                                                                                                                     Debugger.Break();
                                                                                                                 }*/
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{offset:X5} Post-Components Blank ({postCompName})",
                        Name = "_" + offset,

                        Tag = NodeType.StructLeafName
                    });
                    offset += 8;

                    int unknown4 = BitConverter.ToInt32(data, offset);
                    /*if (unknown4 != 0)
                    {
                        Debug.WriteLine("Unknown 4 is not 0: {unknown4);
                       // Debugger.Break();
                    }*/
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{offset:X5} Unknown 4: {unknown4}",
                        Name = "_" + offset,

                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;
                }
                else
                {
                    offset = ClassParser_ReadImplementsTable(subnodes, data, offset);
                    offset = ClassParser_ReadComponentsTable(subnodes, data, offset);

                    /*int unknown4 = BitConverter.ToInt32(data, offset);
                    node = new BinaryInterpreterWPFTreeViewItem($"0x{offset:X5} Unknown 4: {unknown4);
                    node.Name = offset.ToString();
                    node.Tag = nodeType.StructLeafInt;
                    subnodes.Add(node);
                    offset += 4;*/

                    int me12unknownend1 = BitConverter.ToInt32(data, offset);
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{offset:X5} ME1/ME2 Unknown1: {me12unknownend1}",
                        Name = "_" + offset,

                        Tag = NodeType.StructLeafName
                    });
                    offset += 4;

                    int me12unknownend2 = BitConverter.ToInt32(data, offset);
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{offset:X5} ME1/ME2 Unknown2: {me12unknownend2}",
                        Name = "_" + offset,

                        Tag = NodeType.StructLeafName
                    });
                    offset += 4;
                }

                int defaultsClassLink = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Class Defaults: {defaultsClassLink} ({CurrentLoadedExport.FileRef.GetEntryString(defaultsClassLink)}))",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                if (CurrentLoadedExport.FileRef.Game == MEGame.ME3)
                {
                    int functionsTableCount = BitConverter.ToInt32(data, offset);
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{offset:X5} Full Functions Table Count: {functionsTableCount}",
                        Name = "_" + offset,

                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    for (int i = 0; i < functionsTableCount; i++)
                    {
                        int functionsTableIndex = BitConverter.ToInt32(data, offset);
                        string impexpName = CurrentLoadedExport.FileRef.GetEntryString(functionsTableIndex);
                        (subnodes.Last() as BinaryInterpreterWPFTreeViewItem).Items.Add(new BinaryInterpreterWPFTreeViewItem
                        {
                            Header = $"0x{offset:X5} {impexpName}",
                            Tag = NodeType.StructLeafObject,
                            Name = "_" + offset

                        });
                        offset += 4;
                    }
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private int ClassParser_ReadComponentsTable(List<object> subnodes, byte[] data, int offset)
        {
            if (CurrentLoadedExport.FileRef.Game == MEGame.ME3)
            {
                int componentTableNameIndex = BitConverter.ToInt32(data, offset);
                //int componentTableIndex = BitConverter.ToInt32(data, offset + 4);
                offset += 8;

                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset - 8:X5} Components Table ({CurrentLoadedExport.FileRef.getNameEntry(componentTableNameIndex)})",
                    Name = "_" + (offset - 8),

                    Tag = NodeType.StructLeafName
                });
                int componentTableCount = BitConverter.ToInt32(data, offset);
                offset += 4;

                for (int i = 0; i < componentTableCount; i++)
                {
                    int nameTableIndex = BitConverter.ToInt32(data, offset);
                    //int nameIndex = BitConverter.ToInt32(data, offset + 4);
                    offset += 8;
                    int componentObjectIndex = BitConverter.ToInt32(data, offset);
                    offset += 4;
                    string objectName = CurrentLoadedExport.FileRef.GetEntryString(componentObjectIndex);
                    (subnodes.Last() as BinaryInterpreterWPFTreeViewItem).Items.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{offset - 12:X5}  {CurrentLoadedExport.FileRef.getNameEntry(nameTableIndex)}({objectName})",
                        Name = "_" + (offset - 12),

                        Tag = NodeType.StructLeafName
                    });
                }
            }
            else
            {
                int componentTableCount = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Components Table Count: {componentTableCount}",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                for (int i = 0; i < componentTableCount; i++)
                {
                    int nameTableIndex = BitConverter.ToInt32(data, offset);
                    //int nameIndex = BitConverter.ToInt32(data, offset + 4);
                    offset += 8;
                    int componentObjectIndex = BitConverter.ToInt32(data, offset);

                    string objName = "Null";
                    if (componentObjectIndex != 0)
                    {
                        objName = CurrentLoadedExport.FileRef.GetEntryString(componentObjectIndex);
                    }
                    (subnodes.Last() as BinaryInterpreterWPFTreeViewItem).Items.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{offset - 8:X5}  {CurrentLoadedExport.FileRef.getNameEntry(nameTableIndex)}({objName})",
                        Name = "_" + (offset - 8),

                        Tag = NodeType.StructLeafName
                    });
                    offset += 4;

                }
            }
            return offset;
        }

        private int ClassParser_ReadImplementsTable(List<object> subnodes, byte[] data, int offset)
        {
            if (CurrentLoadedExport.FileRef.Game == MEGame.ME3)
            {
                int interfaceCount = BitConverter.ToInt32(data, offset);

                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Implemented Interfaces Table Count: {interfaceCount}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;
                for (int i = 0; i < interfaceCount; i++)
                {
                    int interfaceIndex = BitConverter.ToInt32(data, offset);
                    offset += 4;

                    string objectName = CurrentLoadedExport.FileRef.GetEntryString(interfaceIndex);
                    BinaryInterpreterWPFTreeViewItem subnode = new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{offset - 12:X5}  {interfaceIndex} {objectName}",
                        Name = "_" + (offset - 4),
                        Tag = NodeType.StructLeafName
                    };
                    (subnodes.Last() as BinaryInterpreterWPFTreeViewItem).Items.Add(subnode);

                    //propertypointer
                    interfaceIndex = BitConverter.ToInt32(data, offset);
                    offset += 4;

                    objectName = CurrentLoadedExport.FileRef.GetEntryString(interfaceIndex);
                    subnode.Items.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{offset - 12:X5}  Interface Property Link: {interfaceIndex} {objectName}",
                        Name = "_" + (offset - 4),

                        Tag = NodeType.StructLeafObject
                    });
                }
            }
            else
            {
                int interfaceTableName = BitConverter.ToInt32(data, offset); //????
                offset += 8;

                int interfaceCount = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset - 8:X5} Implemented Interfaces Table Count: {interfaceCount} ({CurrentLoadedExport.FileRef.getNameEntry(interfaceTableName)})",
                    Name = "_" + (offset - 8),

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;
                for (int i = 0; i < interfaceCount; i++)
                {
                    int interfaceNameIndex = BitConverter.ToInt32(data, offset);
                    offset += 8;

                    BinaryInterpreterWPFTreeViewItem subnode = new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{offset - 8:X5}  {CurrentLoadedExport.FileRef.getNameEntry(interfaceNameIndex)}",
                        Name = "_" + (offset - 8),

                        Tag = NodeType.StructLeafName
                    };
                    (subnodes.Last() as BinaryInterpreterWPFTreeViewItem).Items.Add(subnode);

                    //propertypointer
                    /* interfaceIndex = BitConverter.ToInt32(data, offset);
                     offset += 4;

                     objectName = CurrentLoadedExport.FileRef.GetEntryString(interfaceIndex);
                     TreeNode subsubnode = new TreeNode($"0x{offset - 12:X5}  Interface Property Link: {interfaceIndex} {objectName}");
                     subsubnode.Name = (offset - 4).ToString();
                     subsubnode.Tag = nodeType.StructLeafObject;
                     subnode.Nodes.Add(subsubnode);
                     */
                }
            }
            return offset;
        }

        private List<object> StartEnumScan(byte[] data)
        {
            var subnodes = new List<object>();

            try
            {
                int offset = 0;
                int unrealExportIndex = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Unreal Unique Index: {unrealExportIndex}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                int noneUnrealProperty = BitConverter.ToInt32(data, offset);
                //int noneUnrealPropertyIndex = BitConverter.ToInt32(data, offset + 4);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Unreal property None Name: {CurrentLoadedExport.FileRef.getNameEntry(noneUnrealProperty)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafName
                });
                offset += 8;

                int superclassIndex = BitConverter.ToInt32(data, offset);
                string superclassStr = CurrentLoadedExport.FileRef.GetEntryString(superclassIndex);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} Superclass: {superclassIndex}({superclassStr})",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                int classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{offset:X5} NextItemCompilingChain: {classObjTree} {CurrentLoadedExport.FileRef.GetEntryString(classObjTree)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                if (CurrentLoadedExport.ClassName == "Enum")
                {

                    int enumSize = BitConverter.ToInt32(data, offset);
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{offset:X5} Enum Size: {enumSize}",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    for (int i = 0; i < enumSize; i++)
                    {
                        int enumName = BitConverter.ToInt32(data, offset);
                        //int enumNameIndex = BitConverter.ToInt32(data, offset + 4);
                        subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                        {
                            Header = $"0x{offset:X5} EnumName[{i}]: {CurrentLoadedExport.FileRef.getNameEntry(enumName)}",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafName
                        });
                        offset += 8;
                    }
                }

                if (CurrentLoadedExport.ClassName == "Const")
                {
                    int literalStringLength = BitConverter.ToInt32(data, offset);
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{offset:X5} Const Literal Length: {literalStringLength}",
                        Name = "_" + offset,
                        Tag = NodeType.IntProperty
                    });
                    offset += 4;

                    //value is stored as a literal string in binary.
                    MemoryStream stream = new MemoryStream(data) { Position = offset };
                    if (literalStringLength < 0)
                    {
                        string str = stream.ReadString((literalStringLength * -2), true, Encoding.Unicode);
                        subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                        {
                            Header = $"0x{offset:X5} Const Literal Value: {str}",
                            Name = "_" + offset,
                            Tag = NodeType.StrProperty
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<object> StartGuidCacheScan(byte[] data, ref int binarystart)
        {
            /*
             *  
             *  count +4
             *      nameentry +8
             *      guid +16
             *      
             */
            var subnodes = new List<object>();

            try
            {
                int pos = binarystart;
                int count = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{pos:X4} count: {count}",
                    Name = "_" + pos,

                });
                pos += 4;
                for (int i = 0; i < count && pos < data.Length; i++)
                {
                    int nameRef = BitConverter.ToInt32(data, pos);
                    int nameIdx = BitConverter.ToInt32(data, pos + 4);
                    Guid guid = new Guid(data.Skip(pos + 8).Take(16).ToArray());
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"{pos:X4} {CurrentLoadedExport.FileRef.getNameEntry(nameRef)}_{nameIdx}: {{{guid}}}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafName
                    });
                    //Debug.WriteLine($"{pos:X4} {CurrentLoadedExport.FileRef.getNameEntry(nameRef)}_{nameIdx}: {{{guid}}}");
                    pos += 24;
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<object> StartLevelScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<object>();
            try
            {
                int start = binarystart;
                //uint exportid = BitConverter.ToUInt32(data, start);
                start += 4;
                uint numberofitems = BitConverter.ToUInt32(data, start);
                //int countoffset = start;
                BinaryInterpreterWPFTreeViewItem countnode = new BinaryInterpreterWPFTreeViewItem
                {
                    Tag = NodeType.StructLeafInt, //change to listlength or something.
                    Header = $"{start:X4} Level Items List Length: {numberofitems}",
                    Name = "_" + start
                };
                subnodes.Add(countnode);

                start += 4;
                //uint bioworldinfoexportid = BitConverter.ToUInt32(data, start);
                //BinaryInterpreterWPFTreeViewItem bionode = new BinaryInterpreterWPFTreeViewItem
                //{
                //    Tag = NodeType.StructLeafObject,
                //    Header = $"{start:X4} BioWorldInfo Export: {bioworldinfoexportid}",
                //    Name = "_" + start

                //};
                //if (bioworldinfoexportid < CurrentLoadedExport.FileRef.ExportCount && bioworldinfoexportid > 0)
                //{
                //    int me3expindex = (int)bioworldinfoexportid;
                //    IEntry exp = CurrentLoadedExport.FileRef.getEntry(me3expindex);
                //    bionode.Header += $" ({exp.PackageFullName}.{exp.ObjectName})";
                //}
                //subnodes.Add(bionode);

                //IExportEntry bioworldinfo = CurrentLoadedExport.FileRef.Exports[(int)bioworldinfoexportid - 1];
                //if (bioworldinfo.ObjectName != "BioWorldInfo")
                //{
                //    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                //    {
                //        Tag = NodeType.StructLeafObject,
                //        Header = $"{start:X4} Export reference to bioworldinfo resolves to wrong export. Resolved to {bioworldinfo.ObjectName} as export {bioworldinfoexportid}",
                //        Name = "_" + start

                //    });
                //    return subnodes;
                //}

                //start += 4;
                //uint shouldbezero = BitConverter.ToUInt32(data, start);
                //if (shouldbezero != 0)
                //{
                //    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                //    {
                //        Tag = NodeType.Unknown,
                //        Header = $"{start:X4} Export may have extra parameters not accounted for yet (did not find 0 at 0x{start:X5} )",
                //        Name = "_" + start

                //    });
                //    return subnodes;
                //}
                //start += 4;
                int itemcount = 0;

                while (itemcount < numberofitems)
                {
                    //get header.
                    uint itemexportid = BitConverter.ToUInt32(data, start);
                    if (itemexportid == 0)
                    {
                        subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                        {
                            Tag = NodeType.ArrayLeafObject,
                            Header = $"{start:X4}|{itemcount}: {0.ToString().PadRight(8, ' ')} Null/Class",
                            Name = "_" + start
                        });
                        start += 4;
                        itemcount++;
                    }
                    else if (itemexportid - 1 < CurrentLoadedExport.FileRef.Exports.Count)
                    {
                        IExportEntry locexp = CurrentLoadedExport.FileRef.Exports[(int)itemexportid - 1];
                        //Console.WriteLine($"0x{start:X5} \t0x{itemexportid:X5} \t{locexp.PackageFullName}.{locexp.ObjectName}_{locexp.indexValue} [{itemexportid - 1}]");
                        subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                        {
                            Tag = NodeType.ArrayLeafObject,
                            Header = $"{start:X4}|{itemcount}: {locexp.UIndex.ToString().PadRight(8, ' ')} {CurrentLoadedExport.FileRef.GetEntryString(locexp.UIndex)}_{locexp.indexValue}",
                            Name = "_" + start

                        });
                        start += 4;
                        itemcount++;
                    }
                    else
                    {
                        //Console.WriteLine($"0x{start:X5} \t0x{itemexportid:X5} \tInvalid item. Ensure the list is the correct length. (Export {itemexportid})");
                        subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                        {
                            Tag = NodeType.ArrayLeafObject,
                            Header = $"{start:X4} Invalid item. Ensure the list is the correct length. (Export {itemexportid})",
                            Name = "_" + start
                        });
                        start += 4;
                        itemcount++;
                    }
                }
                int unrealNameLen = BitConverter.ToInt32(data, start);
                Encoding encodingToUse = Encoding.ASCII;
                if (unrealNameLen < 0)
                {
                    unrealNameLen *= -2;
                    encodingToUse = Encoding.Unicode;
                }
                int strStart = start;
                start += 4;
                MemoryStream ms = new MemoryStream(data);
                ms.Position = start;
                string unrealStr = ms.ReadString(unrealNameLen, true, encodingToUse);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Tag = NodeType.Unknown,
                    Header = $"{strStart:X4}| Unreal header string: " + unrealStr,
                    Name = "_" + strStart
                });
                start += unrealNameLen;

                start += 4; //blank 0

                int persistentLevelPackageLen = BitConverter.ToInt32(data, start);
                start += 4;
                encodingToUse = Encoding.ASCII;
                if (persistentLevelPackageLen < 0)
                {
                    persistentLevelPackageLen *= -2;
                    encodingToUse = Encoding.Unicode;
                }
                ms.Position = start;
                string persistentLevelPackageStr = ms.ReadString(persistentLevelPackageLen, true, encodingToUse);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Tag = NodeType.Unknown,
                    Header = $"{start:X4}| Persistent Level Package: " + persistentLevelPackageStr,
                    Name = "_" + start
                });
                start += persistentLevelPackageLen;

                binarystart = start;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<object> StartMaterialScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<object>();

            if (binarystart >= data.Length)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = "No Binary Data" });
                return subnodes;
            }
            try
            {
                int binarypos = binarystart + 0x8;
                if (CurrentLoadedExport.FileRef.Game == MEGame.ME2)
                {
                    binarypos -= 4;
                }
                int guidcount = BitConverter.ToInt32(data, binarypos);



                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{binarypos:X4} GUID count: {guidcount}",
                    Name = "_" + binarypos
                });
                binarypos += 4;
                for (int i = 0; i < guidcount; i++)
                {
                    byte[] guidData = data.Skip(binarypos).Take(16).ToArray();
                    Guid guid = new Guid(guidData);
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{binarypos:X4} GUID: {guid}",
                        Name = "_" + binarypos
                    });
                    binarypos += 16;
                }
                int unkcount = BitConverter.ToInt32(data, binarypos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{binarypos:X4} ??? (Count?): {unkcount}",
                    Name = "_" + binarypos
                });
                binarypos += 4;
                int count = BitConverter.ToInt32(data, binarypos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{binarypos:X4} Count: {count}",
                    Name = "_" + binarypos
                });
                binarypos += 4;

                while (binarypos <= data.Length - 4 && count > 0)
                {
                    int val = BitConverter.ToInt32(data, binarypos);
                    string name = val.ToString();

                    if (val > 0 && val <= CurrentLoadedExport.FileRef.Exports.Count)
                    {
                        IExportEntry exp = CurrentLoadedExport.FileRef.Exports[val - 1];
                        name += $" {exp.PackageFullName}.{exp.ObjectName} ({exp.ClassName})";
                    }
                    else if (val < 0 && Math.Abs(val) <= CurrentLoadedExport.FileRef.Imports.Count)
                    {
                        int csImportVal = Math.Abs(val) - 1;
                        ImportEntry imp = CurrentLoadedExport.FileRef.Imports[csImportVal];
                        name += $" {imp.PackageFullName}.{imp.ObjectName} ({imp.ClassName})";

                    }
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{binarypos:X4} {name}",
                        Tag = NodeType.StructLeafObject,
                        Name = "_" + binarypos

                    });
                    binarypos += 4;
                    count--;
                }

                subnodes.Add(new BinaryInterpreterWPFTreeViewItem { Header = "There's a bunch more binary in this object, guids and name refs and object refs." });
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem { Header = "Unfortunately this tool is not smart enough to understand them, but you might be able to." });
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem { Header = "This is your chance to prove that humans are still better than machines." });
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<object> StartPrefabInstanceScan(byte[] data, ref int binarystart)
        {
            /*
             *  count: 4 bytes 
             *      Prefab ref : 4 bytes
             *      Level Object : 4 bytes
             *  0: 4 bytes
             *  
             */
            var subnodes = new List<object>();
            if ((CurrentLoadedExport.Header[0x1f] & 0x2) == 0)
            {
                return subnodes;
            }

            try
            {
                int pos = binarystart;
                int count = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{pos:X4} Count: {count}",
                    Name = "_" + pos

                });
                pos += 4;
                while (pos + 8 <= data.Length && count > 0)
                {
                    var exportRef = BitConverter.ToInt32(data, pos);
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"{pos:X4}: {exportRef} Prefab: {CurrentLoadedExport.FileRef.getEntry(exportRef).GetFullPath}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafObject
                    });
                    pos += 4;
                    exportRef = BitConverter.ToInt32(data, pos);
                    if (exportRef == 0)
                    {
                        (subnodes.Last() as BinaryInterpreterWPFTreeViewItem).Items.Add(new BinaryInterpreterWPFTreeViewItem
                        {
                            Header = $"{pos:X4}: {exportRef} Level Object: Null",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafObject
                        });
                    }
                    else
                    {
                        (subnodes.Last() as BinaryInterpreterWPFTreeViewItem).Items.Add(new BinaryInterpreterWPFTreeViewItem
                        {
                            Header = $"{pos:X4}: {exportRef} Level Object: {CurrentLoadedExport.FileRef.getEntry(exportRef).GetFullPath}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafObject
                        });
                    }

                    pos += 4;
                    count--;
                }

            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<object> StartSkeletalMeshScan(byte[] data, ref int binarystart)
        {
            /*
             *  
             *  Bounding +28
             *  count +4
             *      materials
             *  
             */
            var subnodes = new List<object>();

            try
            {
                int pos = binarystart;
                pos += 28; //bounding
                int count = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{pos:X4} Material Count: {count}",
                    Name = "_" + pos,

                });
                pos += 4;
                for (int i = 0; i < count; i++)
                {
                    int material = BitConverter.ToInt32(data, pos);
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"{pos:X4} Material: ({material}) {CurrentLoadedExport.FileRef.getEntry(material)?.GetFullPath ?? ""}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafObject
                    });
                    pos += 4;
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<object> StartStaticMeshCollectionActorScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<object>();
            try
            {
                //get a list of staticmesh stuff from the props.
                var smacitems = new List<IExportEntry>();
                var props = CurrentLoadedExport.GetProperty<ArrayProperty<ObjectProperty>>("StaticMeshComponents");

                foreach (var prop in props)
                {
                    if (prop.Value > 0)
                    {
                        smacitems.Add(CurrentLoadedExport.FileRef.getEntry(prop.Value) as IExportEntry);
                    }
                    else
                    {
                        smacitems.Add(null);
                    }
                }

                //find start of class binary (end of props)
                int start = binarystart;

                //Lets make sure this binary is divisible by 64.
                if ((data.Length - start) % 64 != 0)
                {
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Tag = NodeType.Unknown,
                        Header = $"{start:X4} Binary data is not divisible by 64 ({data.Length - start})! SMCA binary data should be a length divisible by 64.",
                        Name = "_" + start

                    });
                    return subnodes;
                }

                int smcaindex = 0;
                while (start < data.Length && smcaindex < smacitems.Count)
                {
                    BinaryInterpreterWPFTreeViewItem smcanode = new BinaryInterpreterWPFTreeViewItem
                    {
                        Tag = NodeType.Unknown
                    };
                    IExportEntry assossiateddata = smacitems[smcaindex];
                    string staticmesh = "";
                    string objtext = "Null - unused data";
                    if (assossiateddata != null)
                    {
                        objtext = $"[Export {assossiateddata.UIndex}] {assossiateddata.ObjectName}_{assossiateddata.indexValue}";

                        //find associated static mesh value for display.
                        byte[] smc_data = assossiateddata.Data;
                        int staticmeshstart = 0x4;
                        bool found = false;
                        while (staticmeshstart < smc_data.Length && smc_data.Length - 8 >= staticmeshstart)
                        {
                            ulong nameindex = BitConverter.ToUInt64(smc_data, staticmeshstart);
                            if (nameindex < (ulong)CurrentLoadedExport.FileRef.Names.Count && CurrentLoadedExport.FileRef.Names[(int)nameindex] == "StaticMesh")
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
                            if (staticmeshexp > 0 && staticmeshexp < CurrentLoadedExport.FileRef.ExportCount)
                            {
                                staticmesh = CurrentLoadedExport.FileRef.getEntry(staticmeshexp).ObjectName;
                            }
                        }
                    }

                    smcanode.Header = $"{start:X4} [{smcaindex}] {objtext} {staticmesh}";
                    smcanode.Name = "_" + start;
                    subnodes.Add(smcanode);

                    //Read nodes
                    for (int i = 0; i < 16; i++)
                    {
                        float smcadata = BitConverter.ToSingle(data, start);
                        BinaryInterpreterWPFTreeViewItem node = new BinaryInterpreterWPFTreeViewItem
                        {
                            Tag = NodeType.StructLeafFloat,
                            Header = start.ToString("X4")
                        };

                        //TODO: Figure out what the rest of these mean
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

                        node.Header += $" {label} {smcadata}";

                        //TODO: Lookup staticmeshcomponent so we can see what this actually is without changing to the export

                        node.Name = "_" + start;
                        smcanode.Items.Add(node);
                        start += 4;
                    }

                    smcaindex++;
                }
                //topLevelTree.ItemsSource = subnodes;

            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;

        }

        private List<object> StartStaticMeshScan(byte[] data, ref int binarystart)
        {
            /*
             *  
             *  Bounding +28
             *  RB_BodySetup <----------------------------
             *  more bounding +28 
             *  size +4 bytes
             *  count +4 bytes
             *  kDOPTree +(size*count)
             *  size +4 bytes
             *  count +4 bytes
             *  RawTris +(size*count)
             *  meshversion +4
             *  lodcount +4
             *      guid +16
             *      sectioncount +4
             *          MATERIAL <------------------------
             *          +36
             *          unk5
             *          +13
             *      section[0].unk5 == 1 ? +12 : +4
             */
            var subnodes = new List<object>();
            try
            {
                int pos = binarystart;
                pos += 28;
                int rbRef = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{pos:X4} RB_BodySetup: ({rbRef}) {CurrentLoadedExport.FileRef.getEntry(rbRef)?.GetFullPath ?? ""}",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafObject

                });
                pos += 28; //bounding
                int size = BitConverter.ToInt32(data, pos);
                int count = BitConverter.ToInt32(data, pos + 4);
                pos += 8 + (size * count); //kDOPTree
                size = BitConverter.ToInt32(data, pos);
                count = BitConverter.ToInt32(data, pos + 4);
                pos += 8 + (size * count); //RawTris
                pos += 4; //meshversion
                int lodCount = BitConverter.ToInt32(data, pos);
                pos += 4;
                int unk5 = 0;
                for (int i = 0; i < lodCount; i++)
                {
                    pos += 16; //guid
                    int sectionCount = BitConverter.ToInt32(data, pos);
                    pos += 4;
                    for (int j = 0; j < sectionCount; j++)
                    {
                        int material = BitConverter.ToInt32(data, pos);
                        subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                        {
                            Header = $"{pos:X4} Material: ({material}) {CurrentLoadedExport.FileRef.getEntry(material)?.GetFullPath ?? ""}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafObject
                        });
                        pos += 36;
                        if (j == 0)
                        {
                            unk5 = BitConverter.ToInt32(data, pos);
                        }
                        pos += 13;
                    }
                    pos += unk5 == 1 ? 12 : 4;
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<object> StartTextureBinaryScan(byte[] data)
        {
            var subnodes = new List<object>();

            try
            {
                var textureData = new MemoryStream(data);

                int unrealExportIndex = ReadInt32(textureData);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"0x{textureData.Position - 4:X5} Unreal Unique Index: {unrealExportIndex}",
                    Name = "_" + (textureData.Position - 4),

                    Tag = NodeType.StructLeafInt
                });

                PropertyCollection properties = CurrentLoadedExport.GetProperties();
                if (textureData.Length == properties.endOffset)
                    return subnodes; // no binary data


                textureData.Position = properties.endOffset;
                if (CurrentLoadedExport.FileRef.Game != MEGame.ME3)
                {
                    textureData.Seek(12, SeekOrigin.Current); // 12 zeros
                    textureData.Seek(4, SeekOrigin.Current); // position in the package
                }

                int numMipMaps = ReadInt32(textureData);
                for (int l = 0; l < numMipMaps; l++)
                {
                    var mipMapNode = new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{textureData.Position} MipMap #{l}",
                        Name = "_" + (textureData.Position)

                    };
                    subnodes.Add(mipMapNode);

                    StorageTypes storageType = (StorageTypes)ReadInt32(textureData);
                    mipMapNode.Items.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{textureData.Position - 4} Storage Type: {storageType}",
                        Name = "_" + (textureData.Position - 4)

                    });

                    var uncompressedSize = ReadInt32(textureData);
                    mipMapNode.Items.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{textureData.Position - 4} Uncompressed Size: {uncompressedSize}",
                        Name = "_" + (textureData.Position - 4)

                    });

                    var compressedSize = ReadInt32(textureData);
                    mipMapNode.Items.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{textureData.Position - 4} Compressed Size: {compressedSize}",
                        Name = "_" + (textureData.Position - 4)

                    });

                    var dataOffset = ReadInt32(textureData);
                    mipMapNode.Items.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{textureData.Position - 4} Data Offset: 0x{dataOffset:X8}",
                        Name = "_" + (textureData.Position - 4)

                    });

                    switch (storageType)
                    {
                        case StorageTypes.pccUnc:
                            textureData.Seek(uncompressedSize, SeekOrigin.Current);
                            break;
                        case StorageTypes.pccLZO:
                        case StorageTypes.pccZlib:
                            textureData.Seek(compressedSize, SeekOrigin.Current);
                            break;
                    }

                    var mipWidth = ReadInt32(textureData);
                    mipMapNode.Items.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{textureData.Position - 4} Mip Width: {mipWidth}",
                        Name = "_" + (textureData.Position - 4),
                        Tag = NodeType.StructLeafInt
                    });

                    var mipHeight = ReadInt32(textureData);
                    mipMapNode.Items.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{textureData.Position - 4} Mip Height: {mipHeight}",
                        Name = "_" + (textureData.Position - 4),
                        Tag = NodeType.StructLeafInt
                    });
                }
                ReadInt32(textureData); //skip
                if (CurrentLoadedExport.FileRef.Game != MEGame.ME1)
                {
                    byte[] textureGuid = textureData.ReadBytes(16);
                    var textureGuidNode = new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"0x{textureData.Position} Texture GUID: {new Guid(textureGuid)}",
                        Name = "_" + (textureData.Position)

                    };
                    subnodes.Add(textureGuidNode);
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<object> StartTextureMovieScan(byte[] data, ref int binarystart)
        {
            /*
             *  
             *  count +4
             *      stream length in TFC +4
             *      stream length in TFC +4 (repeat)
             *      stream offset in TFC +4
             *  
             */
            var subnodes = new List<object>();
            try
            {
                int pos = binarystart;
                int unk1 = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{pos:X4} Unknown: {unk1}",
                    Name = "_" + pos,

                });
                pos += 4;
                int length = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{pos:X4} bik length: {length} (0x{length:X})",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                length = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{pos:X4} bik length: {length} (0x{length:X})",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                int offset = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{pos:X4} bik offset in file: {offset} (0x{offset:X})",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                if (pos < data.Length && CurrentLoadedExport.GetProperty<NameProperty>("Filename") == null)
                {
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"{pos:X4} The rest of the binary is the bik.",
                        Name = "_" + pos,

                        Tag = NodeType.Unknown
                    });
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = "The stream offset to this data will be automatically updated when this file is saved.",
                        Tag = NodeType.Unknown
                    });
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<object> StartStateScan(byte[] data, ref int binarystart)
        {
            /*
             * Has UnrealScript Functions contained within, however 
             * the exact format of the data has yet to be determined.
             * Probably better in Script Editor
             */
            var subnodes = new List<object>();

            try
            {
                int pos = binarystart;
                int stateEntryIndex = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{pos:X4} State Entry: {stateEntryIndex} {CurrentLoadedExport.FileRef.GetEntryString(stateEntryIndex)}",
                    Name = "_" + pos,
                    Tag = NodeType.StructLeafObject
                });
                pos += 4;

                /*int length = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{pos:X4} bik length: {length} (0x{length:X})",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                length = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{pos:X4} bik length: {length} (0x{length:X})",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                int offset = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{pos:X4} bik offset in file: {offset} (0x{offset:X})",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                if (pos < data.Length && CurrentLoadedExport.GetProperty<NameProperty>("Filename") == null)
                {
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"{pos:X4} The rest of the binary is the bik.",
                        Name = "_" + pos,

                        Tag = NodeType.Unknown
                    });
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = "The stream offset to this data will be automatically updated when this file is saved.",
                        Tag = NodeType.Unknown
                    });
                }
                */
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<object> StartGenericScan(byte[] data, ref int binarystart)
        {
            binarystart = ByteShiftUpDownValue.Value + binarystart;
            var subnodes = new List<object>();

            if (binarystart >= data.Length)
            {
                return subnodes;
            }
            try
            {
                int binarypos = binarystart;

                //binarypos += 0x1C; //Skip ??? and GUID
                //int guid = BitConverter.ToInt32(data, binarypos);
                /*int num1 = BitConverter.ToInt32(data, binarypos);
                TreeNode node = new TreeNode($"0x{binarypos:X4} ???: {num1.ToString());
                subnodes.Add(node);
                binarypos += 4;
                int num2 = BitConverter.ToInt32(data, binarypos);
                node = new TreeNode($"0x{binarypos:X4} Count: {num2.ToString());
                subnodes.Add(node);
                binarypos += 4;
                */
                int datasize = 4;
                if (interpreterMode == InterpreterMode.Names)
                {
                    datasize = 8;
                }

                while (binarypos <= data.Length - datasize)
                {

                    string nodeText = $"0x{binarypos:X4} : ";
                    var node = new BinaryInterpreterWPFTreeViewItem();

                    switch (interpreterMode)
                    {
                        case InterpreterMode.Objects:
                            {
                                int val = BitConverter.ToInt32(data, binarypos);
                                string name = val.ToString();
                                if (val > 0 && val <= CurrentLoadedExport.FileRef.ExportCount)
                                {
                                    IExportEntry exp = CurrentLoadedExport.FileRef.Exports[val - 1];
                                    nodeText += $"{name} {exp.PackageFullName}.{exp.ObjectName} ({exp.ClassName})";
                                }
                                else if (val < 0 && val != int.MinValue && Math.Abs(val) <= CurrentLoadedExport.FileRef.ImportCount)
                                {
                                    int csImportVal = Math.Abs(val) - 1;
                                    ImportEntry imp = CurrentLoadedExport.FileRef.Imports[csImportVal];
                                    nodeText += $"{name} {imp.PackageFullName}.{imp.ObjectName} ({imp.ClassName})";
                                }
                                node.Tag = NodeType.StructLeafObject;
                                break;
                            }
                        case InterpreterMode.Names:
                            {
                                int val = BitConverter.ToInt32(data, binarypos);
                                if (val > 0 && val <= CurrentLoadedExport.FileRef.NameCount)
                                {
                                    nodeText += $"{val.ToString().PadRight(14, ' ')}{CurrentLoadedExport.FileRef.getNameEntry(val)}";
                                }
                                else
                                {
                                    nodeText += $"              {val}"; //14 spaces
                                }
                                node.Tag = NodeType.StructLeafName;
                                break;
                            }
                        case InterpreterMode.Floats:
                            {
                                float val = BitConverter.ToSingle(data, binarypos);
                                nodeText += val.ToString();
                                node.Tag = NodeType.StructLeafFloat;
                                break;
                            }
                        case InterpreterMode.Integers:
                            {
                                int val = BitConverter.ToInt32(data, binarypos);
                                nodeText += val.ToString();
                                node.Tag = NodeType.StructLeafInt;
                                break;
                            }
                    }
                    node.Header = nodeText;
                    node.Name = "_" + binarypos;
                    subnodes.Add(node);
                    binarypos += 4;
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }
        #endregion

        public override void UnloadExport()
        {
            BinaryInterpreter_Hexbox.ByteProvider = new DynamicByteProvider(new byte[] { });
            BinaryInterpreter_TreeView.ItemsSource = null;
            if (CurrentLoadedExport != null && CurrentLoadedExport.Data.Length > 20480)
            {
                //There was likely a large amount of nodes placed onto the UI
                //Lets free that memory once this export unloads
                //but we wait a few seconds to let the UI release everything
                //(Waiting for a render does not seem to release references)

                Action detach = null;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };

                var handler = new EventHandler((s, args) =>
                {
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect();
            // Note: When stop is called this DispatcherTimer handler will be GC'd (eventually). There is no need to unregister the event.
            timer.Stop();
                    detach?.Invoke();
                });

                detach = new Action(() => timer.Tick -= handler); // No need for deregistering but just for safety let's do it.
                timer.Tick += handler;
                timer.Start();
            }
            CurrentLoadedExport = null;
        }

        private void BinaryInterpreter_SaveHexChanges_Click(object sender, RoutedEventArgs e)
        {
            IByteProvider provider = BinaryInterpreter_Hexbox.ByteProvider;
            if (provider != null)
            {
                MemoryStream m = new MemoryStream();
                for (int i = 0; i < provider.Length; i++)
                    m.WriteByte(provider.ReadByte(i));
                CurrentLoadedExport.Data = m.ToArray();
            }
        }

        internal void SetParentNameList(ObservableCollectionExtended<IndexedName> namesList)
        {
            ParentNameList = namesList;
        }

        private void BinaryInterpreter_TreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            BinaryInterpreter_Hexbox.UnhighlightAll();
            List<FrameworkElement> SupportedEditorSetElements = new List<FrameworkElement>();

            switch (BinaryInterpreter_TreeView.SelectedItem)
            {
                case BinaryInterpreterWPFTreeViewItem bitve:
                    int dataOffset = 0;
                    if (bitve.Name is string offsetStr && offsetStr.StartsWith("_"))
                    {
                        offsetStr = offsetStr.Substring(1); //remove _
                        if (int.TryParse(offsetStr, out dataOffset))
                        {
                            BinaryInterpreter_Hexbox.SelectionStart = dataOffset;
                            BinaryInterpreter_Hexbox.SelectionLength = 1;
                        }
                    }
                    switch (bitve.Tag)
                    {
                        case NodeType.ArrayLeafObject:
                        case NodeType.StructLeafObject:
                            if (dataOffset != 0)
                            {
                                Value_TextBox.Text = BitConverter.ToInt32(CurrentLoadedExport.Data, dataOffset).ToString();
                                SupportedEditorSetElements.Add(Value_TextBox);
                                SupportedEditorSetElements.Add(ParsedValue_TextBlock);
                            }
                            break;
                        case NodeType.StructLeafName:
                            TextSearch.SetTextPath(Value_ComboBox, "Name");
                            Value_ComboBox.IsEditable = true;

                            if (ParentNameList == null)
                            {
                                var indexedList = new List<object>();
                                for (int i = 0; i < CurrentLoadedExport.FileRef.Names.Count; i++)
                                {
                                    NameReference nr = CurrentLoadedExport.FileRef.Names[i];
                                    indexedList.Add(new IndexedName(i, nr));
                                }
                                Value_ComboBox.ItemsSource = indexedList;
                            }
                            else
                            {
                                Value_ComboBox.ItemsSource = ParentNameList;
                            }
                            int nameIdx = BitConverter.ToInt32(CurrentLoadedExport.Data, dataOffset);
                            int nameValueIndex = BitConverter.ToInt32(CurrentLoadedExport.Data, dataOffset + 4);
                            string nameStr = CurrentLoadedExport.FileRef.getNameEntry(nameIdx);
                            if (nameStr != "")
                            {
                                Value_ComboBox.SelectedIndex = nameIdx;
                                NameIndex_TextBox.Text = nameValueIndex.ToString();
                            }
                            else
                            {
                                Value_ComboBox.SelectedIndex = -1;
                                NameIndex_TextBox.Text = "";
                            }
                            SupportedEditorSetElements.Add(Value_ComboBox);
                            SupportedEditorSetElements.Add(NameIndexPrefix_TextBlock);
                            SupportedEditorSetElements.Add(NameIndex_TextBox);
                            break;
                        case NodeType.StructLeafInt:
                            //Todo: We can add different nodeTypes to trigger different ParsedValue parsers, 
                            //such as IntOffset. Enter in int, parse as hex
                            Value_TextBox.Text = BitConverter.ToInt32(CurrentLoadedExport.Data, dataOffset).ToString();
                            SupportedEditorSetElements.Add(Value_TextBox);
                            break;
                        case NodeType.StructLeafFloat:
                            Value_TextBox.Text = BitConverter.ToSingle(CurrentLoadedExport.Data, dataOffset).ToString();
                            SupportedEditorSetElements.Add(Value_TextBox);
                            break;
                    }
                    break;
                case UPropertyTreeViewEntry uptve:
                    if (uptve.Property != null)
                    {
                        var hexPos = uptve.Property.ValueOffset;
                        BinaryInterpreter_Hexbox.SelectionStart = hexPos;
                        BinaryInterpreter_Hexbox.SelectionLength = 1; //maybe change
                        switch (uptve.Property)
                        {
                            //case NoneProperty np:
                            //    Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset - 4, 8);
                            //    return;
                            //case StructProperty sp:
                            //    break;
                            case ObjectProperty op:
                            case FloatProperty fp:
                            case IntProperty ip:
                                {
                                    if (uptve.Parent.Property is StructProperty p && p.IsImmutable)
                                    {
                                        BinaryInterpreter_Hexbox.Highlight(uptve.Property.ValueOffset, 4);
                                        return;
                                    }
                                    else if (uptve.Parent.Property is ArrayProperty<IntProperty> || uptve.Parent.Property is ArrayProperty<FloatProperty> || uptve.Parent.Property is ArrayProperty<ObjectProperty>)
                                    {
                                        BinaryInterpreter_Hexbox.Highlight(uptve.Property.ValueOffset, 4);
                                        return;
                                    }
                                }
                                //otherwise use the default
                                break;
                            case NameProperty np:
                                {
                                    if (uptve.Parent.Property is StructProperty p && p.IsImmutable)
                                    {
                                        BinaryInterpreter_Hexbox.Highlight(uptve.Property.ValueOffset, 8);
                                        return;
                                    }
                                    else if (uptve.Parent.Property is ArrayProperty<NameProperty>)
                                    {
                                        BinaryInterpreter_Hexbox.Highlight(uptve.Property.ValueOffset, 8);
                                        return;
                                    }
                                }
                                break;
                                //case EnumProperty ep:
                                //    BinaryInterpreter_Hexbox.Highlight(uptve.Property.ValueOffset - 32, uptve.Property.GetLength(CurrentLoadedExport.FileRef));
                                //    return;
                        }

                        BinaryInterpreter_Hexbox.Highlight(uptve.Property.StartOffset, uptve.Property.GetLength(CurrentLoadedExport.FileRef));
                    }
                    break;
            }

            //Hide the non-used controls
            foreach (FrameworkElement fe in EditorSetElements)
            {
                fe.Visibility = SupportedEditorSetElements.Contains(fe) ? Visibility.Visible : Visibility.Collapsed;
            }
            EditorSet_Separator.Visibility = SupportedEditorSetElements.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            Set_Button.Visibility = SupportedEditorSetElements.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BinaryInterpreter_TreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {

        }

        private void BinaryInterpreter_ToggleHexboxWidth_Click(object sender, RoutedEventArgs e)
        {
            GridLength len = HexboxColumnDefinition.Width;
            if (len.Value < HexboxColumnDefinition.MaxWidth)
            {
                HexboxColumnDefinition.Width = new GridLength(HexboxColumnDefinition.MaxWidth);
            }
            else
            {
                HexboxColumnDefinition.Width = new GridLength(HexboxColumnDefinition.MinWidth);
            }
        }

        private void BinaryInterpreter_Loaded(object sender, RoutedEventArgs e)
        {
            BinaryInterpreter_Hexbox = (HexBox)BinaryInterpreter_Hexbox_Host.Child;
        }

        private void hb1_SelectionChanged(object sender, EventArgs e)
        {

            int start = (int)BinaryInterpreter_Hexbox.SelectionStart;
            int len = (int)BinaryInterpreter_Hexbox.SelectionLength;
            int size = (int)BinaryInterpreter_Hexbox.ByteProvider.Length;
            byte[] currentData = (BinaryInterpreter_Hexbox.ByteProvider as DynamicByteProvider)?.Bytes.ToArray();
            try
            {
                if (currentData != null && start != -1 && start < size)
                {
                    string s = $"Byte: {currentData[start]}"; //if selection is same as size this will crash.
                    if (start <= currentData.Length - 4)
                    {
                        int val = BitConverter.ToInt32(currentData, start);
                        s += $", Int: {val}";
                        if (CurrentLoadedExport.FileRef.isName(val))
                        {
                            s += $", Name: {CurrentLoadedExport.FileRef.getNameEntry(val)}";
                        }
                        if (CurrentLoadedExport.FileRef.getEntry(val) is IExportEntry exp)
                        {
                            s += $", Export: {exp.ObjectName}";
                        }
                        else if (CurrentLoadedExport.FileRef.getEntry(val) is ImportEntry imp)
                        {
                            s += $", Import: {imp.ObjectName}";
                        }
                    }
                    s += $" | Start=0x{start:X8} ";
                    if (len > 0)
                    {
                        s += $"Length=0x{len:X8} ";
                        s += $"End=0x{(start + len - 1):X8}";
                    }
                    StatusBar_LeftMostText.Text = s;
                    SelectedFileOffset = $"{(CurrentLoadedExport.DataOffset + start):X8}";
                }
                else
                {
                    StatusBar_LeftMostText.Text = "Nothing Selected";
                }
            }
            catch
            {
                // ignored
            }
        }

        private void viewModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            interpreterMode = (InterpreterMode)GenericParsing_ComboBox.SelectedValue;
            StartBinaryScan();
        }

        private void ParseBinary_Button_Click(object sender, RoutedEventArgs e)
        {
            StartBinaryScan();
        }

        private void FileOffsetStatusbar_RightMouseUp(object sender, MouseButtonEventArgs e)
        {

        }

        private void ByteShift_UpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (!LoadingNewData)
            {
                StartBinaryScan();
            }
        }

        private void SetValue_Click(object sender, RoutedEventArgs e)
        {
            switch (BinaryInterpreter_TreeView.SelectedItem)
            {
                case BinaryInterpreterWPFTreeViewItem bitve:
                    int dataOffset = 0;
                    if (bitve.Name is string offsetStr && offsetStr.StartsWith("_"))
                    {
                        offsetStr = offsetStr.Substring(1); //remove _
                        if (int.TryParse(offsetStr, out dataOffset))
                        {
                            BinaryInterpreter_Hexbox.SelectionStart = dataOffset;
                            BinaryInterpreter_Hexbox.SelectionLength = 1;
                        }
                    }
                    bool parsedValueSucceeded = int.TryParse(Value_TextBox.Text, out int parsedValue);
                    bool parsedFloatSucceeded = float.TryParse(Value_TextBox.Text, out float parsedFloatValue);

                    switch (bitve.Tag)
                    {
                        case NodeType.ArrayLeafObject:
                        case NodeType.StructLeafInt:
                        case NodeType.StructLeafObject:
                            if (dataOffset != 0 && parsedValueSucceeded)
                            {
                                byte[] data = CurrentLoadedExport.Data;
                                SharedPathfinding.WriteMem(data, dataOffset, BitConverter.GetBytes(parsedValue));
                                CurrentLoadedExport.Data = data;
                            }
                            break;
                        case NodeType.StructLeafFloat:
                            if (dataOffset != 0 && parsedFloatSucceeded)
                            {
                                byte[] data = CurrentLoadedExport.Data;
                                SharedPathfinding.WriteMem(data, dataOffset, BitConverter.GetBytes(parsedFloatValue));
                                CurrentLoadedExport.Data = data;
                            }
                            break;
                            /*case NodeType.StructLeafName:
                                TextSearch.SetTextPath(Value_ComboBox, "Name");
                                Value_ComboBox.IsEditable = true;

                                if (ParentNameList == null)
                                {
                                    var indexedList = new List<object>();
                                    for (int i = 0; i < CurrentLoadedExport.FileRef.Names.Count; i++)
                                    {
                                        NameReference nr = CurrentLoadedExport.FileRef.Names[i];
                                        indexedList.Add(new IndexedName(i, nr));
                                    }
                                    Value_ComboBox.ItemsSource = indexedList;
                                }
                                else
                                {
                                    Value_ComboBox.ItemsSource = ParentNameList;
                                }
                                int nameIdx = BitConverter.ToInt32(CurrentLoadedExport.Data, dataOffset);
                                int nameValueIndex = BitConverter.ToInt32(CurrentLoadedExport.Data, dataOffset + 4);
                                string nameStr = CurrentLoadedExport.FileRef.getNameEntry(nameIdx);
                                if (nameStr != "")
                                {
                                    Value_ComboBox.SelectedIndex = nameIdx;
                                    NameIndex_TextBox.Text = nameValueIndex.ToString();
                                }
                                else
                                {
                                    Value_ComboBox.SelectedIndex = -1;
                                    NameIndex_TextBox.Text = "";
                                }
                                SupportedEditorSetElements.Add(Value_ComboBox);
                                SupportedEditorSetElements.Add(NameIndexPrefix_TextBlock);
                                SupportedEditorSetElements.Add(NameIndex_TextBox);
                                break;
                                //Todo: We can add different nodeTypes to trigger different ParsedValue parsers, 
                                //such as IntOffset. Enter in int, parse as hex
                                Value_TextBox.Text = BitConverter.ToInt32(CurrentLoadedExport.Data, dataOffset).ToString();
                                SupportedEditorSetElements.Add(Value_TextBox);
                                break;*/
                    }
                    break;
                case UPropertyTreeViewEntry uptve:
                    if (uptve.Property != null)
                    {
                        var hexPos = uptve.Property.ValueOffset;

                        /*
                        BinaryInterpreter_Hexbox.SelectionStart = hexPos;
                        BinaryInterpreter_Hexbox.SelectionLength = 1; //maybe change
                        switch (uptve.Property)
                        {
                            //case NoneProperty np:
                            //    Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset - 4, 8);
                            //    return;
                            //case StructProperty sp:
                            //    break;
                            case ObjectProperty op:
                            case FloatProperty fp:
                            case IntProperty ip:
                                {
                                    if (uptve.Parent.Property is StructProperty p && p.IsImmutable)
                                    {
                                        BinaryInterpreter_Hexbox.Highlight(uptve.Property.ValueOffset, 4);
                                        return;
                                    }
                                    else if (uptve.Parent.Property is ArrayProperty<IntProperty> || uptve.Parent.Property is ArrayProperty<FloatProperty> || uptve.Parent.Property is ArrayProperty<ObjectProperty>)
                                    {
                                        BinaryInterpreter_Hexbox.Highlight(uptve.Property.ValueOffset, 4);
                                        return;
                                    }
                                }
                                //otherwise use the default
                                break;
                            case NameProperty np:
                                {
                                    if (uptve.Parent.Property is StructProperty p && p.IsImmutable)
                                    {
                                        BinaryInterpreter_Hexbox.Highlight(uptve.Property.ValueOffset, 8);
                                        return;
                                    }
                                    else if (uptve.Parent.Property is ArrayProperty<NameProperty>)
                                    {
                                        BinaryInterpreter_Hexbox.Highlight(uptve.Property.ValueOffset, 8);
                                        return;
                                    }
                                }
                                break;
                                //case EnumProperty ep:
                                //    BinaryInterpreter_Hexbox.Highlight(uptve.Property.ValueOffset - 32, uptve.Property.GetLength(CurrentLoadedExport.FileRef));
                                //    return;
                        }

                        BinaryInterpreter_Hexbox.Highlight(uptve.Property.StartOffset, uptve.Property.GetLength(CurrentLoadedExport.FileRef));
                        */
                    }
                    break;
            }
        }

        private void RegenerateNode(object selectedItem)
        {
            throw new NotImplementedException();
        }

        private void RemoveArrayElement_Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void AddArrayElement_Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Value_TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void ValueTextBox_KeyDown(object sender, KeyEventArgs e)
        {

        }
    }

    class BinaryInterpreterWPFTreeViewItem
    {
        public string Header { get; set; }
        public string Name { get; set; }
        public object Tag { get; set; }
        public bool IsExpanded { get; set; }

        /// <summary>
        /// Children nodes of this item. They can be of different types (like UPropertyTreeViewEntry).
        /// </summary>
        public List<object> Items { get; set; }
        public BinaryInterpreterWPFTreeViewItem()
        {
            Items = new List<object>();
        }

        public BinaryInterpreterWPFTreeViewItem(string header)
        {
            Items = new List<object>();
            Header = header;
        }
    }
}
