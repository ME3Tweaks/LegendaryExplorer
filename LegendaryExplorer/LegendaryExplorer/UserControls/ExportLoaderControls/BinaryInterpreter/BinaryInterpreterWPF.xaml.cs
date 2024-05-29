using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Be.Windows.Forms;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.SharedUI.PeregrineTreeView;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for BinaryInterpreterWPF.xaml
    /// </summary>
    public partial class BinaryInterpreterWPF : ExportLoaderControl
    {
        public bool SubstituteImageForHexBox
        {
            get => (bool)GetValue(SubstituteImageForHexBoxProperty);
            set => SetValue(SubstituteImageForHexBoxProperty, value);
        }
        public static readonly DependencyProperty SubstituteImageForHexBoxProperty = DependencyProperty.Register(
            nameof(SubstituteImageForHexBox), typeof(bool), typeof(BinaryInterpreterWPF), new PropertyMetadata(false, SubstituteImageForHexBoxChangedCallback));

        public bool HideHexBox
        {
            get => (bool)GetValue(HideHexBoxProperty);
            set => SetValue(HideHexBoxProperty, value);
        }
        public static readonly DependencyProperty HideHexBoxProperty = DependencyProperty.Register(
            nameof(HideHexBox), typeof(bool), typeof(BinaryInterpreterWPF), new PropertyMetadata(false, HideHexBoxChangedCallback));

        public bool AlwaysLoadRegardlessOfSize
        {
            get => (bool)GetValue(AlwaysLoadRegardlessOfSizeProperty);
            set => SetValue(AlwaysLoadRegardlessOfSizeProperty, value);
        }
        public static readonly DependencyProperty AlwaysLoadRegardlessOfSizeProperty = DependencyProperty.Register(
            nameof(AlwaysLoadRegardlessOfSize), typeof(bool), typeof(BinaryInterpreterWPF), new PropertyMetadata(false));

        /// <summary>
        /// Use only for binding to prevent null bindings
        /// </summary>
        public GenericCommand NavigateToEntryCommandInternal { get; set; }

        public RelayCommand NavigateToEntryCommand
        {
            get => (RelayCommand)GetValue(NavigateToEntryCallbackProperty);
            set => SetValue(NavigateToEntryCallbackProperty, value);
        }

        public static readonly DependencyProperty NavigateToEntryCallbackProperty = DependencyProperty.Register(
            nameof(NavigateToEntryCommand), typeof(RelayCommand), typeof(BinaryInterpreterWPF), new PropertyMetadata(null));

        public int HexBoxMinWidth
        {
            get => (int)GetValue(HexBoxMinWidthProperty);
            set => SetValue(HexBoxMinWidthProperty, value);
        }
        public static readonly DependencyProperty HexBoxMinWidthProperty = DependencyProperty.Register(
            nameof(HexBoxMinWidth), typeof(int), typeof(BinaryInterpreterWPF), new PropertyMetadata(default(int)));

        public int HexBoxMaxWidth
        {
            get => (int)GetValue(HexBoxMaxWidthProperty);
            set => SetValue(HexBoxMaxWidthProperty, value);
        }
        public static readonly DependencyProperty HexBoxMaxWidthProperty = DependencyProperty.Register(
            nameof(HexBoxMaxWidth), typeof(int), typeof(BinaryInterpreterWPF), new PropertyMetadata(default(int)));

        private HexBox BinaryInterpreter_Hexbox;

        /// <summary>
        /// The UI host that is hosting this instance of Binary Interpreter. This can be set as busy when doing things like resolving imports
        /// </summary>
        public IBusyUIHost HostingControl
        {
            get => (IBusyUIHost)GetValue(HostingControlProperty);
            set => SetValue(HostingControlProperty, value);
        }

        public static readonly DependencyProperty HostingControlProperty = DependencyProperty.Register(
            nameof(HostingControl), typeof(IBusyUIHost), typeof(BinaryInterpreterWPF));

        private string _selectedFileOffset;
        public string SelectedFileOffset
        {
            get => _selectedFileOffset;
            set => SetProperty(ref _selectedFileOffset, value);
        }

        private int? _byteShiftUpDownValue;
        public int? ByteShiftUpDownValue
        {
            get => _byteShiftUpDownValue;
            set => SetProperty(ref _byteShiftUpDownValue, value);
        }

        private Visibility _genericEditorSetVisibility;
        public Visibility GenericEditorSetVisibility
        {
            get => _genericEditorSetVisibility;
            set => SetProperty(ref _genericEditorSetVisibility, value);
        }
        private readonly List<FrameworkElement> EditorSetElements = new();
        public ObservableCollectionExtended<BinInterpNode> TreeViewItems { get; } = new();
        public ObservableCollectionExtended<IndexedName> ParentNameList { get; private set; }
        public enum InterpreterMode
        {
            Objects,
            Names,
            Integers,
            Floats
        }

        private InterpreterMode interpreterMode = InterpreterMode.Objects;
        private bool LoadingNewData;

        public BinaryInterpreterWPF() : base("Binary Interpreter")
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
            EditorSetElements.Add(EditorSet_Separator_LeftsideArray);

            //EditorSetElements.Add(EditorSet_ArraySetSeparator);
            Set_Button.Visibility = Visibility.Collapsed;
            EditorSet_Separator.Visibility = Visibility.Collapsed;
            GenericEditorSetVisibility = Visibility.Collapsed;
            GenericParsing_ComboBox.ItemsSource = Enum.GetValues(typeof(InterpreterMode)).Cast<InterpreterMode>();
            GenericParsing_ComboBox.SelectedItem = InterpreterMode.Objects;
        }

        #region Commands
        public ICommand CopyOffsetCommand { get; set; }
        public ICommand OpenInPackageEditorCommand { get; set; }
        public ICommand FindDefinitionOfImportCommand { get; set; }
        public ICommand CopyGuidCommand { get; set; }

        private void LoadCommands()
        {
            CopyOffsetCommand = new RelayCommand(CopyFileOffsetToClipboard, OffsetIsSelected);
            NavigateToEntryCommandInternal = new GenericCommand(FireNavigateCallback, CanFireNavigateCallback);
            OpenInPackageEditorCommand = new GenericCommand(OpenInPackageEditor, IsSelectedItemAnObjectRef);
            FindDefinitionOfImportCommand = new GenericCommand(FindDefinitionOfImport, IsSelectedItemAnImportObjectRef);
            CopyGuidCommand = new GenericCommand(CopyGuid, IsSelectedItemAGuid);
        }

        private void CopyGuid()
        {
            if (BinaryInterpreter_TreeView.SelectedItem is BinInterpNode b)
            {
                // How to use DataReadOnly here?
                var stream = new EndianReader(new MemoryStream(CurrentLoadedExport.Data));
                stream.Seek(b.GetPos(), SeekOrigin.Begin);
                var g = stream.ReadGuid();
                Clipboard.SetText(g.ToString().Replace(" ",""));
            }
        }

        private bool IsSelectedItemAnObjectRef()
        {
            return BinaryInterpreter_TreeView.SelectedItem is BinInterpNode b && IsObjectNodeType(b);
        }

        private bool IsSelectedItemAnImportObjectRef()
        {
            return BinaryInterpreter_TreeView.SelectedItem is BinInterpNode b && IsImportObjectNodeType(b);
        }

        private bool IsSelectedItemAGuid()
        {
            return BinaryInterpreter_TreeView.SelectedItem is BinInterpNode b && b.Tag is NodeType nt && nt == NodeType.Guid;
        }

        private void FireNavigateCallback()
        {
            if (CurrentLoadedExport != null && NavigateToEntryCommand != null && BinaryInterpreter_TreeView.SelectedItem is BinInterpNode b && IsObjectNodeType(b))
            {
                var pos = b.GetPos();
                var value = EndianReader.ToInt32(CurrentLoadedExport.DataReadOnly, (int)pos, CurrentLoadedExport.FileRef.Endian);
                if (CurrentLoadedExport.FileRef.IsEntry(value))
                {
                    NavigateToEntryCommand?.Execute(CurrentLoadedExport.FileRef.GetEntry(value));
                }
            }
        }

        private bool CanFireNavigateCallback()
        {
            if (NavigateToEntryCommand != null && BinaryInterpreter_TreeView.SelectedItem is BinInterpNode b &&
                IsObjectNodeType(b))
            {
                var pos = b.GetPos();
                var value = EndianReader.ToInt32(CurrentLoadedExport.DataReadOnly, (int)pos, CurrentLoadedExport.FileRef.Endian);
                return CurrentLoadedExport.FileRef.IsEntry(value);
            }

            return false;
        }

        private void OpenInPackageEditor()
        {
            if (BinaryInterpreter_TreeView.SelectedItem is BinInterpNode b && IsObjectNodeType(b))
            {
                int index = b.UIndexValue == 0 ? b.GetObjectRefValue(CurrentLoadedExport) : b.UIndexValue;
                if (CurrentLoadedExport.FileRef.IsEntry(index))
                {
                    var p = new PackageEditorWindow();
                    p.Show();
                    p.LoadFile(CurrentLoadedExport.FileRef.FilePath, index);
                    p.Activate(); //bring to front  
                }
            }
        }

        private void FindDefinitionOfImport()
        {
            if (BinaryInterpreter_TreeView.SelectedItem is BinInterpNode b && IsImportObjectNodeType(b))
            {
                if (CurrentLoadedExport.FileRef.IsEntry(b.UIndexValue))
                {
                    int index = b.UIndexValue == 0 ? b.GetObjectRefValue(CurrentLoadedExport) : b.UIndexValue;
                    var import = CurrentLoadedExport.FileRef.GetImport(index);
                    if (HostingControl is not null)
                    {
                        HostingControl.IsBusy = true;
                        HostingControl.BusyText = "Attempting to find source of import...";
                    }
                    Task.Run(() => EntryImporter.ResolveImport(import)).ContinueWithOnUIThread(prevTask =>
                    {
                        if (HostingControl is not null) HostingControl.IsBusy = false;
                        if (prevTask.Result is ExportEntry res)
                        {
                            var pwpf = new PackageEditorWindow();
                            pwpf.Show();
                            pwpf.LoadEntry(res);
                            pwpf.RestoreAndBringToFront();
                        }
                        else
                        {
                            MessageBox.Show(
                                "Could not find the export that this import references.\nHas the link or name (including parents) of this import been changed?\nDo the filenames match the BioWare naming scheme if it's a BioX file?");
                        }
                    });
                }
            }
        }

        private bool IsImportObjectNodeType(object nodeobj)
        {
            if (nodeobj is BinInterpNode b && IsObjectNodeType(nodeobj))
            {
                int index = b.UIndexValue == 0 ? b.GetObjectRefValue(CurrentLoadedExport) : b.UIndexValue;
                return CurrentLoadedExport.FileRef.IsImport(index);
            }
            return false;
        }

        private static bool IsObjectNodeType(object nodeobj)
        {
            if (nodeobj is BinInterpNode { Tag: NodeType type })
            {
                if (type == NodeType.ArrayLeafObject) return true;
                if (type == NodeType.ObjectProperty) return true;
                if (type == NodeType.StructLeafObject) return true;
            }

            return false;
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

        public static readonly HashSet<string> ParsableBinaryClasses = new()
        {
            "AnimSequence",
            "ArrayProperty",
            "BioCodexMap",
            "BioConsequenceMap",
            "BioCreatureSoundSet",
            "BioDynamicAnimSet",
            "BioGestureRuntimeData",
            "BioMorphFace",
            "BioOutcomeMap",
            "BioPawn",
            "BioQuestMap",
            "BioSocketSupermodel",
            "BioSoundNodeWaveStreamingData",
            "BioStage",
            "BioStateEventMap",
            "BioTlkFileSet",
            "BioMask4Property",
            "BoolProperty",
            "BrushComponent",
            "ByteProperty",
            "Class",
            "ClassProperty",
            "ComponentProperty",
            "Const",
            "CookedBulkDataInfoContainer",
            "CoverMeshComponent",
            "DecalComponent",
            "DecalMaterial",
            "DelegateProperty",
            "DirectionalLightComponent",
            "DominantDirectionalLightComponent",
            "DominantPointLightComponent",
            "DominantSpotLightComponent",
            "Enum",
            "FaceFXAnimSet",
            "FaceFXAsset",
            "FloatProperty",
            "FluidSurfaceComponent",
            "ForceFeedbackWaveform",
            "FracturedStaticMesh",
            "FracturedStaticMeshComponent",
            "Function",
            "GuidCache",
            "InteractiveFoliageComponent",
            "InterfaceProperty",
            "IntProperty",
            "Level",
            "LightMapTexture2D",
            "MapProperty",
            "Material",
            "MaterialInstanceConstant",
            "MaterialInstanceConstants",
            "MaterialInstanceTimeVarying",
            "MetaData",
            "Model",
            "ModelComponent",
            "MorphTarget",
            "NameProperty",
            "ObjectProperty",
            "ObjectRedirector",
            "PhysicsAssetInstance",
            "PointLightComponent",
            "Polys",
            "PrefabInstance",
            "RB_BodySetup",
            "SFXNav_LargeMantleNode",
            "SFXMorphFaceFrontEndDataSource",
            "ScriptStruct",
            "ShaderCache",
            "ShaderCachePayload",
            "ShadowMap1D",
            "ShadowMapTexture2D",
            "SkeletalMesh",
            "SkyLightComponent",
            "SoundCue",
            "SoundNodeWave",
            "SphericalHarmonicLightComponent",
            "SplineMeshComponent",
            "SpotLightComponent",
            "State",
            "StaticLightCollectionActor",
            "StaticMesh",
            "StaticMeshCollectionActor",
            "StaticMeshComponent",
            "StrProperty",
            "StringRefProperty",
            "StructProperty",
            "Terrain",
            "TerrainComponent",
            "TerrainWeightMapTexture",
            "TextBuffer",
            "Texture2D",
            "TextureFlipBook",
            "TextureMovie",
            "World",
            "WwiseBank",
            "WwiseEvent",
            "WwiseStream",
            "Bio2DANumberedRows",
            "Bio2DA",
        };

        public override bool CanParse(ExportEntry exportEntry)
        {
            //return exportEntry.HasStack || ((ParsableBinaryClasses.Contains(exportEntry.ClassName) || exportEntry.IsA("BioPawn")) && !exportEntry.IsDefaultObject)
            //    || exportEntry.TemplateOwnerClassIdx >= 0;

            // crossgen 9/24/2021
            return !exportEntry.IsDefaultObject && (exportEntry.HasStack || exportEntry.TemplateOwnerClassIdx >= 0 || exportEntry.propsEnd() < exportEntry.DataSize);
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new BinaryInterpreterWPF() { AlwaysLoadRegardlessOfSize = true }, CurrentLoadedExport)
                {
                    Title = $"Binary Interpreter - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
                elhw.Show();
            }
        }

        private int PreviousLoadedUIndex = -1;
        private string PreviousSelectedTreeName = "";

        public override void LoadExport(ExportEntry exportEntry)
        {
            LoadingNewData = true;
            ByteShift_UpDown.Value = 0;
            if (CurrentLoadedExport != null)
            {
                PreviousLoadedUIndex = CurrentLoadedExport.UIndex;
                if (BinaryInterpreter_TreeView.SelectedItem is BinInterpNode b)
                {
                    PreviousSelectedTreeName = b.Name;
                }
            }
            CurrentLoadedExport = exportEntry;

            OnDemand_Panel.Visibility = Visibility.Visible;
            LoadedContent_Panel.Visibility = Visibility.Collapsed;
            if (CurrentLoadedExport.DataSize < 20480 || Settings.BinaryInterpreter_SkipAutoParseSizeCheck || AlwaysLoadRegardlessOfSize)
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

            // For right clicking things.
            Guid,

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
            byte[] data = CurrentLoadedExport.Data;
            var db = new ReadOptimizedByteProvider(data);
            BinaryInterpreter_Hexbox.ByteProvider = db;
            hb1_SelectionChanged(BinaryInterpreter_Hexbox, EventArgs.Empty);//reassigning the ByteProvider won't trigger this, leaving old info in statusbar
            int binarystart = 0;
            if (CurrentLoadedExport.ClassName != "Class")
            {
                binarystart = CurrentLoadedExport.propsEnd();
            }

            //top node will always be of this element type.
            var topLevelTree = new BinInterpNode
            {
                Header = $"{binarystart:X4} : {CurrentLoadedExport.InstancedFullPath} - Binary start",
                Tag = NodeType.Root,
                Name = "_" + binarystart,
                IsExpanded = true
            };
            //BinaryInterpreter_TreeView.Items.Add(topLevelTree);

            Task.Run(() => PerformScanBackground(topLevelTree, data, binarystart))
                .ContinueWithOnUIThread(prevTask =>
                {
                    var result = prevTask.Result;
                    OnDemand_Panel.Visibility = Visibility.Collapsed;
                    LoadedContent_Panel.Visibility = Visibility.Visible;
                    TreeViewItems.Replace(result);
                });
        }

        public static bool IsNativePropertyType(string classname)
        {
            switch (classname)
            {
                case "IntProperty":
                case "BoolProperty":
                case "ArrayProperty":
                case "FloatProperty":
                case "ClassProperty":
                case "ByteProperty":
                case "StrProperty":
                case "NameProperty":
                case "StringRefProperty":
                case "StructProperty":
                case "ComponentProperty":
                case "ObjectProperty":
                case "DelegateProperty":
                case "InterfaceProperty":
                    return true;
                default:
                    return false;
            }
        }

        private BinInterpNode PerformScanBackground(BinInterpNode topLevelTree, byte[] data, int binarystart)
        {
            if (CurrentLoadedExport == null) return topLevelTree; //Could happen due to multithread
            try
            {
                var subNodes = new List<ITreeItem>();
                bool isGenericScan = false;
                bool appendGenericScan = false;

                if (CurrentLoadedExport.HasStack)
                {
                    subNodes.AddRange(StartStackScan(data));
                }

                //pre-property binary
                switch (CurrentLoadedExport.ClassName)
                {
                    case "DominantSpotLightComponent":
                    case "DominantDirectionalLightComponent":
                        subNodes.AddRange(StartDominantLightScan(data));
                        break;
                }

                if (CurrentLoadedExport.TemplateOwnerClassIdx is var toci and >= 0)
                {
                    int n = EndianReader.ToInt32(data, toci, CurrentLoadedExport.FileRef.Endian);
                    subNodes.Add(new BinInterpNode(toci, $"TemplateOwnerClass: #{n} {CurrentLoadedExport.FileRef.GetEntryString(n)}", NodeType.StructLeafObject) { Length = 4 });
                }

                string className = CurrentLoadedExport.ClassName;
                if (CurrentLoadedExport.IsA("BioPawn"))
                {
                    className = "BioPawn";
                }
                switch (className)
                {
                    case "IntProperty":
                    case "BoolProperty":
                    case "ArrayProperty":
                    case "FloatProperty":
                    case "ClassProperty":
                    case "BioMask4Property":
                    case "ByteProperty":
                    case "StrProperty":
                    case "NameProperty":
                    case "StringRefProperty":
                    case "StructProperty":
                    case "ComponentProperty":
                    case "ObjectProperty":
                    case "DelegateProperty":
                    case "MapProperty":
                    case "InterfaceProperty":
                        subNodes.AddRange(StartPropertyScan(data, ref binarystart));
                        break;
                    case "BioDynamicAnimSet":
                        subNodes.AddRange(StartBioDynamicAnimSetScan(data, ref binarystart));
                        break;
                    case "BioSquadCombat":
                        if (CurrentLoadedExport != null && CurrentLoadedExport.Game.IsGame1()) // Only game 1 has binary data
                        {
                            subNodes.AddRange(StartBioSquadCombatScan(data, ref binarystart));
                        }
                        break;
                    case "ObjectRedirector":
                        subNodes.AddRange(StartObjectRedirectorScan(data, ref binarystart));
                        break;
                    case "MetaData":
                        subNodes.AddRange(StartMetaDataScan(data, ref binarystart));
                        break;
                    case "TextBuffer":
                        subNodes.AddRange(StartTextBufferScan(data, binarystart));
                        break;
                    case "WwiseStream":
                        subNodes.AddRange(Scan_WwiseStream(data));
                        break;
                    case "WwiseBank":
                        subNodes.AddRange(Scan_WwiseBank(data));
                        break;
                    case "WwiseEvent":
                        subNodes.AddRange(Scan_WwiseEvent(data, ref binarystart));
                        break;
                    case "Bio2DA":
                    case "Bio2DANumberedRows":
                        subNodes.AddRange(Scan_Bio2DA(data));
                        break;
                    case "BioStage":
                        subNodes.AddRange(StartBioStageScan(data, ref binarystart));
                        break;
                    case "BioTlkFileSet":
                        subNodes.AddRange(StartBioTlkFileSetScan(data, ref binarystart));
                        break;
                    case "Class":
                        subNodes.AddRange(StartClassScan(data));
                        break;
                    case "Enum":
                        subNodes.AddRange(StartEnumScan(data, ref binarystart));
                        break;
                    case "Const":
                        subNodes.AddRange(StartConstScan(data, ref binarystart));
                        break;
                    case "Function":
                        subNodes.AddRange(StartFunctionScan(data, ref binarystart));
                        break;
                    case "GuidCache":
                        subNodes.AddRange(StartGuidCacheScan(data, ref binarystart));
                        break;
                    case "World":
                        subNodes.AddRange(StartWorldScan(data, ref binarystart));
                        break;
                    case "ShaderCache":
                        subNodes.AddRange(StartShaderCacheScanStream(data, ref binarystart));
                        break;
                    case "ShaderCachePayload": //Consoles
                        subNodes.AddRange(StartShaderCachePayloadScanStream(data, ref binarystart));
                        break;
                    case "Model":
                        subNodes.AddRange(StartModelScan(data, ref binarystart));
                        break;
                    case "Polys":
                        subNodes.AddRange(StartPolysScan(data, ref binarystart));
                        break;
                    case "Level":
                        subNodes.AddRange(StartLevelScan(data, ref binarystart));
                        break;
                    case "DecalMaterial":
                    case "Material":
                        subNodes.AddRange(StartMaterialScan(data, ref binarystart));
                        break;
                    case "MaterialInstanceConstant":
                    case "MaterialInstanceTimeVarying":
                        subNodes.AddRange(StartMaterialInstanceScan(data, ref binarystart));
                        break;
                    case "PrefabInstance":
                        subNodes.AddRange(StartPrefabInstanceScan(data, ref binarystart));
                        break;
                    case "SkeletalMesh":
                    case "BioSocketSupermodel":
                        subNodes.AddRange(StartSkeletalMeshScan(data, ref binarystart));
                        break;
                    case "StaticMeshCollectionActor":
                        subNodes.AddRange(StartStaticMeshCollectionActorScan(data, ref binarystart));
                        break;
                    case "FracturedStaticMesh":
                        subNodes.AddRange(StartFracturedStaticMeshScan(data, ref binarystart));
                        break;
                    case "StaticMesh":
                        subNodes.AddRange(StartStaticMeshScan(data, ref binarystart));
                        break;
                    case "CoverMeshComponent":
                    case "InteractiveFoliageComponent":
                    case "SplineMeshComponent":
                    case "FracturedStaticMeshComponent":
                    case "StaticMeshComponent":
                        subNodes.AddRange(StartStaticMeshComponentScan(data, ref binarystart));
                        break;
                    case "StaticLightCollectionActor":
                        subNodes.AddRange(StartStaticLightCollectionActorScan(data, ref binarystart));
                        break;
                    case "Texture2D":
                    case "LightMapTexture2D":
                    case "ShadowMapTexture2D":
                    case "TextureFlipBook":
                    case "TerrainWeightMapTexture":
                        subNodes.AddRange(StartTextureBinaryScan(data, binarystart));
                        break;
                    case "ShadowMap1D":
                        subNodes.AddRange(StartShadowMap1DScan(data, binarystart));
                        break;
                    case "State":
                        subNodes.AddRange(StartStateScan(data, ref binarystart));
                        break;
                    case "TextureMovie":
                        subNodes.AddRange(StartTextureMovieScan(data, ref binarystart));
                        break;
                    case "BioGestureRuntimeData":
                        subNodes.AddRange(StartBioGestureRuntimeDataScan(data, ref binarystart));
                        break;
                    case "ScriptStruct":
                        subNodes.AddRange(StartScriptStructScan(data, ref binarystart));
                        break;
                    case "SoundCue":
                        subNodes.AddRange(StartSoundCueScan(data, ref binarystart));
                        break;
                    case "BioSoundNodeWaveStreamingData":
                        subNodes.AddRange(StartBioSoundNodeWaveStreamingDataScan(data, ref binarystart));
                        break;
                    case "SoundNodeWave":
                        subNodes.AddRange(StartSoundNodeWaveScan(data, ref binarystart));
                        break;
                    case "BioStateEventMap":
                        subNodes.AddRange(StartBioStateEventMapScan(data, ref binarystart));
                        break;
                    case "BioCodexMap":
                        subNodes.AddRange(StartBioCodexMapScan(data, ref binarystart));
                        break;
                    case "BioQuestMap":
                        subNodes.AddRange(StartBioQuestMapScan(data, ref binarystart));
                        break;
                    case "BioConsequenceMap":
                        subNodes.AddRange(StartBioStateEventMapScan(data, ref binarystart));
                        break;
                    case "FaceFXAnimSet":
                        subNodes.AddRange(StartFaceFXAnimSetScan(data, ref binarystart));
                        break;
                    case "FaceFXAsset":
                        subNodes.AddRange(StartFaceFXAssetScan(data, ref binarystart));
                        break;
                    case "AnimSequence":
                        subNodes.AddRange(StartAnimSequenceScan(data, ref binarystart));
                        break;
                    case "DirectionalLightComponent":
                    case "PointLightComponent":
                    case "SkyLightComponent":
                    case "SphericalHarmonicLightComponent":
                    case "SpotLightComponent":
                    case "DominantSpotLightComponent":
                    case "DominantPointLightComponent":
                    case "DominantDirectionalLightComponent":
                        subNodes.AddRange(StartLightComponentScan(data, binarystart));
                        break;
                    case "RB_BodySetup":
                        subNodes.AddRange(StartRB_BodySetupScan(data, ref binarystart));
                        break;
                    case "BrushComponent":
                        subNodes.AddRange(StartBrushComponentScan(data, ref binarystart));
                        break;
                    case "ModelComponent":
                        subNodes.AddRange(StartModelComponentScan(data, ref binarystart));
                        break;
                    case "BioPawn":
                        subNodes.AddRange(StartBioPawnScan(data, ref binarystart));
                        break;
                    case "PhysicsAssetInstance":
                        subNodes.AddRange(StartPhysicsAssetInstanceScan(data, ref binarystart));
                        break;
                    case "CookedBulkDataInfoContainer":
                        subNodes.AddRange(StartCookedBulkDataInfoContainerScan(data, ref binarystart));
                        break;
                    case "DecalComponent":
                        subNodes.AddRange(StartDecalComponentScan(data, ref binarystart));
                        break;
                    case "Terrain":
                        subNodes.AddRange(StartTerrainScan(data, ref binarystart));
                        break;
                    case "TerrainComponent":
                        subNodes.AddRange(StartTerrainComponentScan(data, ref binarystart));
                        break;
                    case "FluidSurfaceComponent":
                        subNodes.AddRange(StartFluidSurfaceComponentScan(data, ref binarystart));
                        break;
                    case "ForceFeedbackWaveform":
                        subNodes.AddRange(StartForceFeedbackWaveformScan(data, ref binarystart));
                        break;
                    case "MorphTarget":
                        subNodes.AddRange(StartMorphTargetScan(data, ref binarystart));
                        break;
                    case "BioMorphFace":
                        subNodes.AddRange(StartBioMorphFaceScan(data, ref binarystart));
                        break;
                    case "SFXMorphFaceFrontEndDataSource":
                        subNodes.AddRange(StartSFXMorphFaceFrontEndDataSourceScan(data, ref binarystart));
                        break;
                    case "BioCreatureSoundSet":
                        subNodes.AddRange(StartBioCreatureSoundSetScan(data, ref binarystart));
                        break;
                    case "BioGestureRulesData":
                        subNodes.AddRange(StartBioGestureRulesDataScan(data, ref binarystart));
                        break;
                    default:
                        if (!CurrentLoadedExport.HasStack)
                        {
                            isGenericScan = true;
                            subNodes.AddRange(StartGenericScan(data, ref binarystart));
                        }
                        break;
                }
                if (appendGenericScan)
                {
                    BinInterpNode genericContainer = new BinInterpNode { Header = "Generic scan data", IsExpanded = true };
                    subNodes.Add(genericContainer);

                    var genericItems = StartGenericScan(data, ref binarystart);
                    foreach (ITreeItem o in genericItems)
                    {
                        if (o is BinInterpNode b)
                        {
                            b.Parent = genericContainer;
                        }
                    }
                    genericContainer.Items.AddRange(genericItems);
                }
                if (PreviousLoadedUIndex == CurrentLoadedExport?.UIndex && PreviousSelectedTreeName != "")
                {
                    var reSelected = AttemptSelectPreviousEntry(subNodes);
                    Debug.WriteLine("Reselected previous entry");
                }

                GenericEditorSetVisibility = (appendGenericScan || isGenericScan) ? Visibility.Visible : Visibility.Collapsed;
                topLevelTree.Items = subNodes;
                foreach (ITreeItem o in subNodes)
                {
                    if (o is BinInterpNode b)
                    {
                        b.RemoveNullNodes();
                        b.Parent = topLevelTree;
                    }
                }
            }
            catch (Exception ex)
            {
                topLevelTree.Items.Add(new BinInterpNode(ex.FlattenException()));
            }
            return topLevelTree;
        }

        private bool AttemptSelectPreviousEntry(IEnumerable<ITreeItem> subNodes)
        {
            foreach (ITreeItem o in subNodes)
            {
                if (o is BinInterpNode b)
                {
                    if (b.Name == PreviousSelectedTreeName)
                    {
                        b.IsProgramaticallySelecting = true;
                        b.IsSelected = true;
                        return true;
                    }
                    if (b.Items != null)
                    {
                        if (AttemptSelectPreviousEntry(b.Items))
                        {
                            o.IsExpanded = true;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        internal void SetHexboxSelectedOffset(int v)
        {
            if (BinaryInterpreter_Hexbox != null)
            {
                BinaryInterpreter_Hexbox.SelectionStart = v;
                BinaryInterpreter_Hexbox.SelectionLength = 1;
            }
        }

        public override void UnloadExport()
        {
            //Todo: convert to this single byteprovider and clear bytes rather than instantiating new ones.
            BinaryInterpreter_Hexbox.ByteProvider = new ReadOptimizedByteProvider();
            TreeViewItems.ClearEx();
            if (CurrentLoadedExport != null && CurrentLoadedExport.DataSize > 20480)
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

                detach = () => timer.Tick -= handler; // No need for deregistering but just for safety let's do it.
                timer.Tick += handler;
                timer.Start();
            }
            CurrentLoadedExport = null;
        }

        private void BinaryInterpreter_SaveHexChanges_Click(object sender, RoutedEventArgs e)
        {
            if (BinaryInterpreter_Hexbox.ByteProvider is ReadOptimizedByteProvider provider)
            {
                CurrentLoadedExport.Data = provider.Span.ToArray();
            }
        }

        internal void SetParentNameList(ObservableCollectionExtended<IndexedName> namesList)
        {
            ParentNameList = namesList;
        }

        private void BinaryInterpreter_TreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            BinaryInterpreter_Hexbox.UnhighlightAll();
            var SupportedEditorSetElements = new List<FrameworkElement>();
            switch (BinaryInterpreter_TreeView.SelectedItem)
            {
                case BinInterpNode bitve:
                    int dataOffset = bitve.GetOffset();
                    if (dataOffset > 0)
                    {
                        BinaryInterpreter_Hexbox.SelectionStart = dataOffset;
                        BinaryInterpreter_Hexbox.SelectionLength = 1;
                        if (bitve.Length > 0)
                        {
                            BinaryInterpreter_Hexbox.Highlight(dataOffset, bitve.Length);
                        }
                    }
                    switch (bitve.Tag)
                    {
                        case NodeType.ArrayLeafObject:
                        case NodeType.StructLeafObject:
                            if (dataOffset != 0)
                            {
                                Value_TextBox.Text = EndianReader.ToInt32(CurrentLoadedExport.DataReadOnly, dataOffset, CurrentLoadedExport.FileRef.Endian).ToString();
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
                                    string nr = CurrentLoadedExport.FileRef.Names[i];
                                    indexedList.Add(new IndexedName(i, nr));
                                }
                                Value_ComboBox.ItemsSource = indexedList;
                            }
                            else
                            {
                                Value_ComboBox.ItemsSource = ParentNameList;
                            }
                            int nameIdx = EndianReader.ToInt32(CurrentLoadedExport.DataReadOnly, dataOffset, CurrentLoadedExport.FileRef.Endian);
                            int nameValueIndex = EndianReader.ToInt32(CurrentLoadedExport.DataReadOnly, dataOffset + 4, CurrentLoadedExport.FileRef.Endian);
                            string nameStr = CurrentLoadedExport.FileRef.GetNameEntry(nameIdx);
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
                            Value_TextBox.Text = EndianReader.ToInt32(CurrentLoadedExport.DataReadOnly, dataOffset, CurrentLoadedExport.FileRef.Endian).ToString();
                            SupportedEditorSetElements.Add(Value_TextBox);
                            break;
                        case NodeType.StructLeafFloat:
                            Value_TextBox.Text = EndianReader.ToSingle(CurrentLoadedExport.DataReadOnly, dataOffset, CurrentLoadedExport.FileRef.Endian).ToString();
                            SupportedEditorSetElements.Add(Value_TextBox);
                            break;
                    }
                    if (bitve.ArrayAddAlgoritm != BinInterpNode.ArrayPropertyChildAddAlgorithm.None)
                    {
                        SupportedEditorSetElements.Add(AddArrayElement_Button);
                        SupportedEditorSetElements.Add(EditorSet_Separator_LeftsideArray);
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

            this.bind(HexBoxMinWidthProperty, BinaryInterpreter_Hexbox, nameof(BinaryInterpreter_Hexbox.MinWidth));
            this.bind(HexBoxMaxWidthProperty, BinaryInterpreter_Hexbox, nameof(BinaryInterpreter_Hexbox.MaxWidth));
        }

        private void hb1_SelectionChanged(object sender, EventArgs e)
        {
            int start = (int)BinaryInterpreter_Hexbox.SelectionStart;
            int len = (int)BinaryInterpreter_Hexbox.SelectionLength;
            int size = (int)BinaryInterpreter_Hexbox.ByteProvider.Length;
            try
            {
                var currentData = ((ReadOptimizedByteProvider)BinaryInterpreter_Hexbox.ByteProvider).Span;
                if (currentData != null && start != -1 && start < size)
                {
                    string s = $"Byte: {currentData[start]}"; //if selection is same as size this will crash.
                    if (start <= currentData.Length - 2)
                    {
                        ushort val = EndianReader.ToUInt16(currentData, start, CurrentLoadedExport.FileRef.Endian);
                        s += $", UShort: {val}";
                    }
                    if (start <= currentData.Length - 4)
                    {
                        int val = EndianReader.ToInt32(currentData, start, CurrentLoadedExport.FileRef.Endian);
                        s += $", Int: {val}";
                        float fval = EndianReader.ToSingle(currentData, start, CurrentLoadedExport.FileRef.Endian);
                        s += $", Float: {fval}";
                        if (CurrentLoadedExport.FileRef.IsName(val))
                        {
                            s += $", Name: {CurrentLoadedExport.FileRef.GetNameEntry(val)}";
                        }
                        if (CurrentLoadedExport.FileRef.GetEntry(val) is ExportEntry exp)
                        {
                            s += $", Export: {exp.ObjectName.Instanced}";
                        }
                        else if (CurrentLoadedExport.FileRef.GetEntry(val) is ImportEntry imp)
                        {
                            s += $", Import: {imp.ObjectName.Instanced}";
                        }
                    }
                    s += $" | Start=0x{start:X8} ";
                    if (len > 0)
                    {
                        s += $"Length=0x{len:X8} ";
                        s += $"End=0x{(start + len - 1):X8}";
                    }
                    StatusBar_LeftMostText.Text = s;
                    SelectedFileOffset = $"{CurrentLoadedExport.DataOffset + start:X8}";
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
                case BinInterpNode bitve:
                    var dataOffset = (int)bitve.GetPos();
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
                                data.OverwriteRange(dataOffset, BitConverter.GetBytes(parsedValue));
                                CurrentLoadedExport.Data = data;
                            }
                            break;
                        case NodeType.StructLeafFloat:
                            if (dataOffset != 0 && parsedFloatSucceeded)
                            {
                                byte[] data = CurrentLoadedExport.Data;
                                data.OverwriteRange(dataOffset, BitConverter.GetBytes(parsedFloatValue));
                                CurrentLoadedExport.Data = data;
                            }
                            break;
                        case NodeType.StructLeafName:
                            var item = Value_ComboBox.SelectedItem as IndexedName;
                            if (item == null)
                            {
                                var text = Value_ComboBox.Text;
                                if (!string.IsNullOrEmpty(text))
                                {
                                    int index = CurrentLoadedExport.FileRef.findName(text);
                                    if (index < 0)
                                    {
                                        string input = $"The name \"{text}\" does not exist in the current loaded package.\nIf you'd like to add this name, press enter below, or change the name to what you would like it to be.";
                                        string result = PromptDialog.Prompt(Window.GetWindow(this), input, "Enter new name", text);
                                        if (!string.IsNullOrEmpty(result))
                                        {
                                            int idx = CurrentLoadedExport.FileRef.FindNameOrAdd(result);
                                            if (idx != CurrentLoadedExport.FileRef.Names.Count - 1)
                                            {
                                                //not the last
                                                MessageBox.Show($"{result} already exists in this package file.\nName index: {idx} (0x{idx:X8})", "Name already exists");
                                            }
                                            else
                                            {
                                                item = new IndexedName(idx, result);
                                            }
                                        }
                                    }
                                }
                            }
                            bool nameindexok = int.TryParse(NameIndex_TextBox.Text, out int nameIndex);
                            nameindexok &= nameIndex >= 0;
                            if (item != null && dataOffset != 0 && nameindexok)
                            {
                                byte[] data = CurrentLoadedExport.Data;
                                data.OverwriteRange(dataOffset, BitConverter.GetBytes(CurrentLoadedExport.FileRef.findName(item.Name)));
                                data.OverwriteRange(dataOffset + 4, BitConverter.GetBytes(nameIndex));
                                CurrentLoadedExport.Data = data;
                                Debug.WriteLine("Set data");
                            }
                            break;
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

        private void RemoveArrayElement_Button_Click(object sender, RoutedEventArgs e)
        {
        }

        private void AddArrayElement_Button_Click(object sender, RoutedEventArgs e)
        {
            if (BinaryInterpreter_TreeView.SelectedItem is BinInterpNode bitvi)
            {
                switch (bitvi.ArrayAddAlgoritm)
                {
                    case BinInterpNode.ArrayPropertyChildAddAlgorithm.FourBytes:
                        BinInterpNode container = bitvi;
                        if ((NodeType)container.Tag == NodeType.ArrayLeafObject)
                        {
                            container = bitvi.Parent; //container
                        }
                        var dataCopy = CurrentLoadedExport.Data;
                        int countOffset = int.Parse(container.Name.Substring(1)); //chop off _
                        int count = BitConverter.ToInt32(dataCopy, countOffset);

                        //Incrememnt Count
                        dataCopy.OverwriteRange(countOffset, BitConverter.GetBytes(count + 1));

                        //Insert new entry
                        List<byte> memList = dataCopy.ToList();
                        int offset = countOffset + ((count + 1) * 4); //will be at the very end of the list as it is now +1

                        memList.InsertRange(offset, BitConverter.GetBytes(0));
                        CurrentLoadedExport.Data = memList.ToArray();
                        break;
                }
            }
        }

        private void Value_TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
        }

        private void ValueTextBox_KeyDown(object sender, KeyEventArgs e)
        {
        }

        public override void Dispose()
        {
            TreeViewItems.Clear();
            DispatcherHelper.EmptyQueue(); //this should force out references to us hopefully
            BinaryInterpreter_Hexbox = null;
            BinaryInterpreter_Hexbox_Host.Child.Dispose();
            BinaryInterpreter_Hexbox_Host.Dispose();
        }

        private static void HideHexBoxChangedCallback(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            BinaryInterpreterWPF i = (BinaryInterpreterWPF)obj;
            if ((bool)e.NewValue)
            {
                i.hexBoxContainer.Visibility = i.HexProps_GridSplitter.Visibility = i.ToggleHexboxWidth_Button.Visibility = i.SaveHexChange_Button.Visibility = Visibility.Collapsed;
                i.HexboxColumn_GridSplitter_ColumnDefinition.Width = new GridLength(0);
                i.HexboxColumnDefinition.MinWidth = 0;
                i.HexboxColumnDefinition.MaxWidth = 0;
                i.HexboxColumnDefinition.Width = new GridLength(0);
            }
            else
            {
                i.hexBoxContainer.Visibility = i.HexProps_GridSplitter.Visibility = i.ToggleHexboxWidth_Button.Visibility = i.SaveHexChange_Button.Visibility = Visibility.Visible;
                i.HexboxColumnDefinition.Width = new GridLength(i.HexBoxMinWidth);
                i.HexboxColumn_GridSplitter_ColumnDefinition.Width = new GridLength(1);
                i.HexboxColumnDefinition.bind(ColumnDefinition.MinWidthProperty, i, nameof(HexBoxMinWidth));
                i.HexboxColumnDefinition.bind(ColumnDefinition.MaxWidthProperty, i, nameof(HexBoxMaxWidth));
            }
        }

        private static void SubstituteImageForHexBoxChangedCallback(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            BinaryInterpreterWPF i = (BinaryInterpreterWPF)obj;
            if (e.NewValue is true && i.BinaryInterpreter_Hexbox_Host.Child.Height > 0 && i.BinaryInterpreter_Hexbox_Host.Child.Width > 0)
            {
                i.hexboxImageSub.Source = i.BinaryInterpreter_Hexbox_Host.Child.DrawToBitmapSource();
                i.hexboxImageSub.Width = i.BinaryInterpreter_Hexbox_Host.ActualWidth;
                i.hexboxImageSub.Height = i.BinaryInterpreter_Hexbox_Host.ActualHeight;
                i.hexboxImageSub.Visibility = Visibility.Visible;
                i.BinaryInterpreter_Hexbox_Host.Visibility = Visibility.Collapsed;
            }
            else
            {
                i.BinaryInterpreter_Hexbox_Host.Visibility = Visibility.Visible;
                i.hexboxImageSub.Visibility = Visibility.Collapsed;
            }
        }

        private void CopyTree_Button_OnClick(object sender, RoutedEventArgs e)
        {
            if (TreeViewItems.Count > 0)
            {
                try
                {
                    using (StringWriter stringoutput = new StringWriter())
                    {
                        TreeViewItems[0].PrintPretty("", stringoutput, true, CurrentLoadedExport);
#if DEBUG
                        //Uncomment this to write it out to disk. sometimes pasting big text busts things
                        //File.WriteAllText(@"C:\users\public\bincopy.txt", stringoutput.ToString());
#endif
                        Clipboard.SetText(stringoutput.ToString());
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to copy to clipboard: " + ex.Message);
                }
            }
        }
    }
}