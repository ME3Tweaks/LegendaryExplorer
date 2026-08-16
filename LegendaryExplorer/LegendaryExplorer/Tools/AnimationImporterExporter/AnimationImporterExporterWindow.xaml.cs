using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.UserControls.SharedToolControls;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.AppCenter.Analytics;
using Microsoft.Win32;
using Path = System.IO.Path;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.AnimationImporterExporter
{
    /// <summary>
    /// Interaction logic for AnimationImporterExporterWindow.xaml
    /// </summary>
    public partial class AnimationImporterExporterWindow : WPFBase, IRecents
    {
        public const string PSAFilter = "*.psa|*.psa";
        public const string BVHFilter = "*.bvh|*.bvh";
        public const string GLTFFilter = "glTF binary|*.glb|glTF|*.gltf";

        public AnimationImporterExporterWindow() : base("Animation Importer/Exporter")
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();

            RecentsController.InitRecentControl(Toolname, Recents_MenuItem, fileName => LoadFile(fileName));
        }

        public AnimationImporterExporterWindow(ExportEntry exportToLoad) : this()
        {
            FileQueuedForLoad = exportToLoad.FileRef.FilePath;
            ExportQueuedForFocusing = exportToLoad;
        }

        public AnimationImporterExporterWindow(string filePath, int uIndex = 0) : this()
        {
            FileQueuedForLoad = filePath;
            ExportQueuedForFocusing = null;
            UIndexQueuedForFocusing = uIndex;
        }

        #region Properties

        private ExportEntry _currentExport;
        public ExportEntry CurrentExport
        {
            get => _currentExport;
            set
            {
                SetProperty(ref _currentExport, value);
                if (value == null)
                {
                    BinaryInterpreterTab_BinaryInterpreter.UnloadExport();
                    InterpreterTab_Interpreter.UnloadExport();
                }
                else
                {
                    BinaryInterpreterTab_BinaryInterpreter.LoadExport(CurrentExport);
                    InterpreterTab_Interpreter.LoadExport(CurrentExport);

                    // If it's an AnimSequence, load it into the preview
                    if (CurrentExport.ClassName == "AnimSequence")
                    {
                        AnimPreview.LoadAnimSequence(CurrentExport);
                    }
                }
            }
        }

        private string FileQueuedForLoad;
        private ExportEntry ExportQueuedForFocusing;
        private readonly int UIndexQueuedForFocusing;

        public ObservableCollectionExtended<ExportEntry> AnimSequenceExports { get; } = new();
        public ObservableCollectionExtended<ExportEntry> SkeletalMeshExports { get; } = new();

        private ExportEntry _selectedSkeletalMesh;
        public ExportEntry SelectedSkeletalMesh
        {
            get => _selectedSkeletalMesh;
            set
            {
                if (SetProperty(ref _selectedSkeletalMesh, value) && value != null)
                {
                    AnimPreview.LoadSkeletalMesh(value);
                }
            }
        }

        #endregion

        #region Commands

        public ICommand OpenFileCommand { get; set; }
        public ICommand SaveFileCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }
        public ICommand ImportFromUDKCommand { get; set; }
        public ICommand ReplaceFromUDKCommand { get; set; }
        public ICommand ImportFromPSACommand { get; set; }
        public ICommand ReplaceFromPSACommand { get; set; }
        public ICommand ExportAnimSeqToPSACommand { get; set; }
        public ICommand ExportAnimSetToPSACommand { get; set; }
        public ICommand ExportAnimSeqToBVHCommand { get; set; }
        public ICommand ImportFromBVHCommand { get; set; }
        public ICommand ReplaceFromBVHCommand { get; set; }
        public ICommand ExportAnimSeqToGLTFCommand { get; set; }

        private void LoadCommands()
        {
            OpenFileCommand = new GenericCommand(OpenFile);
            SaveFileCommand = new GenericCommand(SaveFile, IsPackageLoaded);
            SaveAsCommand = new GenericCommand(SaveFileAs, IsPackageLoaded);

            ImportFromUDKCommand = new GenericCommand(ImportFromUDK, IsPackageLoaded);
            ReplaceFromUDKCommand = new GenericCommand(ReplaceFromUDK, IsAnimSequenceSelected);
            ImportFromPSACommand = new GenericCommand(ImportFromPSA, IsPackageLoaded);
            ReplaceFromPSACommand = new GenericCommand(ReplaceFromPSA, IsAnimSequenceSelected);
            ExportAnimSeqToPSACommand = new GenericCommand(ExportAnimSeqToPSA, IsAnimSequenceSelected);
            ExportAnimSetToPSACommand = new GenericCommand(ExportAnimSetToPSA, IsBioAnimDataSelected);
            ExportAnimSeqToBVHCommand = new GenericCommand(ExportAnimSeqToBVH, IsAnimSequenceSelected);
            ImportFromBVHCommand = new GenericCommand(ImportFromBVH, IsPackageLoaded);
            ReplaceFromBVHCommand = new GenericCommand(ReplaceFromBVH, IsAnimSequenceSelected);
            ExportAnimSeqToGLTFCommand = new GenericCommand(ExportAnimSeqToGLTF, IsAnimSequenceSelected);
        }

        #endregion

        #region Import/Export functionality

        private void ExportAnimSetToPSA()
        {
            throw new NotImplementedException();
        }

        private void ExportAnimSeqToPSA()
        {
            if (ObjectBinary.From(CurrentExport) is AnimSequence animSequence)
            {
                string sequenceName = CurrentExport.GetProperty<NameProperty>("SequenceName")?.Value.Instanced ?? CurrentExport.ObjectName.Instanced;
                var dlg = new SaveFileDialog
                {
                    Filter = PSAFilter,
                    FileName = $"{sequenceName}.psa",
                    AddExtension = true,
                };
                if (dlg.ShowDialog(this) == true)
                {
                    PSA.CreateFrom(animSequence).ToFile(dlg.FileName);
                    MessageBox.Show("Done!", "PSA Export", MessageBoxButton.OK);
                }
            }
        }

        private void ImportFromBVH()
        {
            var dlg = new OpenFileDialog
            {
                Filter = BVHFilter,
                CheckFileExists = true,
                Title = "Select BVH file",
                Multiselect = false,
            };
            if (dlg.ShowDialog(this) != true) return;

            string chosenCoord = InputComboBoxDialog.GetValue(this,
                "Select the coordinate system the BVH was exported with.",
                "Coordinate System",
                CoordSystemOptions.Select(o => o.Label),
                CoordSystemOptions[0].Label);
            if (chosenCoord.IsEmpty()) return;

            BVHCoordinateSystem cs = CoordSystemOptions.First(o => o.Label == chosenCoord).Value;

            AnimationCompressionFormat rotationCompressionFormat = AnimationCompressionFormat.ACF_Float96NoW;
            if (!TryGetRotationCompressionFormat(ref rotationCompressionFormat)) return;

            AnimSequence animSeq = BVH.ImportFromBVH(dlg.FileName, cs);
            string defaultName = Path.GetFileNameWithoutExtension(dlg.FileName);
            animSeq.Name = NameReference.FromInstancedString(defaultName);

            var pkg = ExportCreator.CreatePackageExport(Pcc, NameReference.FromInstancedString(defaultName));
            var bioAnimSetData = ExportCreator.CreateExport(Pcc, "BioAnimSetData", "BioAnimSetData", pkg);
            bioAnimSetData.WriteProperty(new ArrayProperty<NameProperty>(
                animSeq.Bones.Select(b => new NameProperty(NameReference.FromInstancedString(b.Trim().Replace(' ', '-')))),
                "TrackBoneNames"));

            var seqExp = ExportCreator.CreateExport(Pcc, NameReference.FromInstancedString(defaultName), "AnimSequence", pkg);
            var props = seqExp.GetProperties();
            animSeq.UpdateProps(props, Pcc.Game, rotationCompressionFormat, forceUpdate: true);
            props.AddOrReplaceProp(new ObjectProperty(bioAnimSetData, "m_pBioAnimSetData"));
            seqExp.WriteProperties(props);
            seqExp.WriteBinary(animSeq);

            MessageBox.Show("Done!", "BVH Import", MessageBoxButton.OK);
        }

        private void ReplaceFromBVH()
        {
            var dlg = new OpenFileDialog
            {
                Filter = BVHFilter,
                CheckFileExists = true,
                Title = "Select BVH file",
                Multiselect = false,
            };
            if (dlg.ShowDialog(this) != true) return;

            string chosenCoord = InputComboBoxDialog.GetValue(this,
                "Select the coordinate system the BVH was exported with.",
                "Coordinate System",
                CoordSystemOptions.Select(o => o.Label),
                CoordSystemOptions[0].Label);
            if (chosenCoord.IsEmpty()) return;

            BVHCoordinateSystem cs = CoordSystemOptions.First(o => o.Label == chosenCoord).Value;

            AnimSequence bvhSeq = BVH.ImportFromBVH(dlg.FileName, cs);

            var props = CurrentExport.GetProperties();

            if (props.GetProp<ObjectProperty>("m_pBioAnimSetData") is { Value: > 0 } bioAnimSetProp
                && Pcc.TryGetUExport(bioAnimSetProp.Value, out ExportEntry bioAnimSet)
                && bioAnimSet.GetProperty<ArrayProperty<NameProperty>>("TrackBoneNames") is { } trackNames)
            {
                List<string> existingBones = trackNames.Select(np => np.Value.Instanced).ToList();

                if (existingBones.Except(bvhSeq.Bones, StringComparer.OrdinalIgnoreCase).ToList() is { Count: > 0 } missingBones)
                {
                    MessageBox.Show($"This BVH is missing these bones:\n{string.Join(", ", missingBones)}",
                        "", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var bvhNameToTrack = new Dictionary<string, AnimTrack>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < bvhSeq.Bones.Count; i++)
                    bvhNameToTrack[bvhSeq.Bones[i]] = bvhSeq.RawAnimationData[i];

                bvhSeq.RawAnimationData = existingBones.Select(b => bvhNameToTrack[b]).ToList();
                bvhSeq.Bones = existingBones.Clone();
            }

            if (props.GetProp<EnumProperty>("RotationCompressionFormat") is not { } compressionEnum
                || !Enum.TryParse(compressionEnum.Value, out AnimationCompressionFormat rotationCompressionFormat))
            {
                rotationCompressionFormat = AnimationCompressionFormat.ACF_Float96NoW;
            }

            if (!TryGetRotationCompressionFormat(ref rotationCompressionFormat)) return;

            var originalSeqName = props.GetProp<NameProperty>("SequenceName");
            bvhSeq.UpdateProps(props, CurrentExport.Game, rotationCompressionFormat, forceUpdate: true);
            if (originalSeqName != null)
                props.AddOrReplaceProp(originalSeqName);
            CurrentExport.WriteProperties(props);
            CurrentExport.WriteBinary(bvhSeq);
            MessageBox.Show("Done!", "Replace from BVH", MessageBoxButton.OK);
        }

        private static readonly (string Label, BVHCoordinateSystem Value)[] CoordSystemOptions =
        [
            ("Y-up, Right-handed (Blender default)", BVHCoordinateSystem.YUpRightHanded),
            ("Y-up, Left-handed",                    BVHCoordinateSystem.YUpLeftHanded),
            ("Z-up, Right-handed",                   BVHCoordinateSystem.ZUpRightHanded),
            ("Z-up, Left-handed (UE3 native)",        BVHCoordinateSystem.ZUpLeftHanded),
        ];

        private async void ExportAnimSeqToBVH()
        {
            if (ObjectBinary.From(CurrentExport) is not AnimSequence animSequence)
                return;

            MeshBone[] refSkeleton = await TryGetRefSkeletonFromDB(Pcc.Game);
            if (refSkeleton is null)
                return;

            string chosenCoord = InputComboBoxDialog.GetValue(this,
                "Select the coordinate system for the BVH export.",
                "Coordinate System",
                CoordSystemOptions.Select(o => o.Label),
                CoordSystemOptions[0].Label);
            if (chosenCoord.IsEmpty())
                return;

            BVHCoordinateSystem coordinateSystem = CoordSystemOptions.First(o => o.Label == chosenCoord).Value;

            string sequenceName = CurrentExport.GetProperty<NameProperty>("SequenceName")?.Value.Instanced ?? CurrentExport.ObjectName.Instanced;
            var dlg = new SaveFileDialog
            {
                Filter = BVHFilter,
                FileName = $"{sequenceName}.bvh",
                AddExtension = true,
            };
            if (dlg.ShowDialog(this) == true)
            {
                BVH.ExportToBVH(animSequence, dlg.FileName, refSkeleton, coordinateSystem);
                MessageBox.Show("Done!", "BVH Export", MessageBoxButton.OK);
            }
        }

        private async void ExportAnimSeqToGLTF()
        {
            if (ObjectBinary.From(CurrentExport) is not AnimSequence animSequence)
                return;

            MeshBone[] refSkeleton = await TryGetRefSkeletonFromDB(Pcc.Game);
            if (refSkeleton is null)
                return;

            string sequenceName = CurrentExport.GetProperty<NameProperty>("SequenceName")?.Value.Instanced ?? CurrentExport.ObjectName.Instanced;
            var dlg = new SaveFileDialog
            {
                Filter = GLTFFilter,
                FileName = $"{sequenceName}.glb",
                AddExtension = true,
            };
            if (dlg.ShowDialog(this) != true)
                return;

            IsBusy = true;
            BusyText = "Exporting to glTF…";
            string filePath = dlg.FileName;
            string version = $"Legendary Explorer {AppVersion.DisplayedVersion}";

            try
            {
                await Task.Run(() => GLTF.ExportAnimSequenceToGltf(animSequence, refSkeleton, filePath, version));
            }
            finally
            {
                IsBusy = false;
            }

            MessageBox.Show("Done!", "glTF Export", MessageBoxButton.OK);
        }

        private async Task<MeshBone[]> TryGetRefSkeletonFromDB(MEGame game)
        {
            var (pkg, export) = await TryGetSkeletalMeshExportFromDB(game);
            using (pkg)
            {
                if (export is not null && ObjectBinary.From(export) is SkeletalMesh skMesh)
                    return skMesh.RefSkeleton;
            }
            return null;
        }

        /// <summary>
        /// Asks the user to pick a SkeletalMesh from the asset database and returns the owning package + export.
        /// Caller is responsible for disposing the returned IMEPackage.
        /// </summary>
        private async Task<(IMEPackage pkg, ExportEntry export)> TryGetSkeletalMeshExportFromDB(MEGame game)
        {
            string dbPath = AssetDatabaseWindow.GetDBPath(game);
            if (!File.Exists(dbPath))
            {
                MessageBox.Show(
                    "No asset database found for this game.\n\nSelect a SkeletalMesh from the current package, or generate the asset database using the Asset Database tool.",
                    "No Asset Database", MessageBoxButton.OK, MessageBoxImage.Information);
                return (null, null);
            }

            IsBusy = true;
            BusyText = "Loading asset database…";
            var db = new AssetDB();
            try
            {
                await AssetDatabaseWindow.LoadDatabase(dbPath, game, db, CancellationToken.None);
            }
            finally
            {
                IsBusy = false;
            }

            var skeletalMeshes = db.Meshes.Where(m => m.IsSkeleton).OrderBy(m => m.MeshName).ToList();
            if (skeletalMeshes.Count == 0)
            {
                MessageBox.Show("No SkeletalMeshes found in the asset database.", "Select SkeletalMesh", MessageBoxButton.OK, MessageBoxImage.Warning);
                return (null, null);
            }

            string chosen = InputComboBoxDialog.GetValue(this,
                "Select a SkeletalMesh to use as the skeleton.\nThe animation must be compatible with the chosen mesh.",
                "Select SkeletalMesh",
                skeletalMeshes.Select(m => m.DisplayString));
            if (chosen.IsEmpty())
                return (null, null);

            MeshRecord selectedRecord = skeletalMeshes.First(m => m.DisplayString == chosen);

            string gamePath = MEDirectories.GetDefaultGamePath(game);
            if (gamePath is null || !Directory.Exists(gamePath))
            {
                MessageBox.Show($"Game path for {game} is not configured. Check your settings.", "Select SkeletalMesh", MessageBoxButton.OK, MessageBoxImage.Error);
                return (null, null);
            }

            foreach (var (fileKey, uIndex, _) in selectedRecord.Usages)
            {
                FileNameDirKeyPair filePair = db.FileList[fileKey];
                string contentDir = db.ContentDir[filePair.DirectoryKey];

                string filePath = Directory.EnumerateFiles(gamePath, $"{filePair.FileName}.*", SearchOption.AllDirectories)
                                           .FirstOrDefault(f => f.Contains(contentDir));

                if (filePath is null && game == MEGame.ME3)
                {
                    string sfarPath = Path.Combine(MEDirectories.GetDLCPath(MEGame.ME3), contentDir, "CookedPCConsole", "Default.sfar");
                    if (File.Exists(sfarPath))
                    {
                        var dlp = new DLCPackage(sfarPath);
                        if (dlp.FindFileEntry(filePair.FileName) >= 0)
                            filePath = sfarPath;
                    }
                }

                if (filePath is null)
                    continue;

                IMEPackage pkg = MEPackageHandler.OpenMEPackage(filePath);
                if (pkg.IsUExport(uIndex) && pkg.GetUExport(uIndex) is ExportEntry export && export.IsA("SkeletalMesh"))
                {
                    return (pkg, export);
                }
                pkg.Dispose();
            }

            MessageBox.Show($"Could not locate any package file for '{selectedRecord.MeshName}'.", "Select SkeletalMesh", MessageBoxButton.OK, MessageBoxImage.Error);
            return (null, null);
        }

        private void ReplaceFromPSA()
        {
            if (CurrentExport.ClassName == "AnimSequence")
            {
                var dlg = new OpenFileDialog
                {
                    Filter = PSAFilter,
                    CheckFileExists = true,
                    Title = "Select PSA",
                    Multiselect = false,
                    CustomPlaces = AppDirectories.GameCustomPlaces
                };
                if (dlg.ShowDialog(this) == true)
                {
                    var props = CurrentExport.GetProperties();

                    var psa = PSA.FromFile(dlg.FileName);
                    var psaSeqs = psa.GetAnimSequences();
                    if (psaSeqs.IsEmpty())
                    {
                        MessageBox.Show("This PSA is empty!", "", MessageBoxButton.OK, MessageBoxImage.Error); //can this happen?
                        return;
                    }

                    if (props.GetProp<ObjectProperty>("m_pBioAnimSetData") is { Value: > 0 } bioAnimSetProp
                        && Pcc.TryGetUExport(bioAnimSetProp.Value, out ExportEntry bioAnimSet)
                        && bioAnimSet.GetProperty<ArrayProperty<NameProperty>>("TrackBoneNames") is {} trackNames)
                    {
                        List<string> existingBones = trackNames.Select(nameProp => nameProp.Value.Instanced).ToList();
                        if (!existingBones.SequenceEqual(psaSeqs[0].Bones))
                        {
                            if (existingBones.Except(psaSeqs[0].Bones).ToList() is { Count: > 0 } missingBones)
                            {
                                MessageBox.Show($"This PSA is missing these bones:\n{string.Join(',', missingBones)}", "", MessageBoxButton.OK, MessageBoxImage.Error); //can this happen?
                                return;
                            }
                            foreach (AnimSequence psaSeq in psaSeqs)
                            {
                                var fixedAnimTracks = new List<AnimTrack>();
                                foreach (string bone in existingBones)
                                {
                                    fixedAnimTracks.Add(psaSeq.RawAnimationData[psaSeq.Bones.IndexOf(bone)]);
                                }
                                psaSeq.RawAnimationData = fixedAnimTracks;
                                psaSeq.Bones = existingBones.Clone();
                            }
                        }
                    }
                    else
                    {
                        //No m_pBioAnimSetData specified. Import anyway I suppose, since we can't do any validation
                    }

                    AnimSequence selectedAnimSequence = psaSeqs[0];
                    if (psaSeqs.Count > 1)
                    {
                        var seqName = InputComboBoxDialog.GetValue(this, "Select animation from PSA", "Animation Selector", psaSeqs.Select(s => s.Name.Instanced));
                        if (seqName.IsEmpty())
                        {
                            return;
                        }

                        selectedAnimSequence = psaSeqs.First(s => s.Name.Instanced == seqName);
                    }

                    if (props.GetProp<EnumProperty>("RotationCompressionFormat") is not EnumProperty compressionFormatEnum ||
                        !Enum.TryParse(compressionFormatEnum.Value, out AnimationCompressionFormat rotationCompressionFormat))
                    {
                        rotationCompressionFormat = AnimationCompressionFormat.ACF_Float96NoW;
                    }

                    if (!TryGetRotationCompressionFormat(ref rotationCompressionFormat))
                    {
                        return;
                    }

                    var originalSeqName = props.GetProp<NameProperty>("SequenceName");
                    selectedAnimSequence.UpdateProps(props, CurrentExport.Game, rotationCompressionFormat, forceUpdate: true);
                    if (originalSeqName != null)
                    {
                        props.AddOrReplaceProp(originalSeqName);
                    }
                    CurrentExport.WriteProperties(props);
                    CurrentExport.WriteBinary(selectedAnimSequence);
                    MessageBox.Show("Done!", "Replace From PSA", MessageBoxButton.OK);
                }
            }
        }

        private bool TryGetRotationCompressionFormat(ref AnimationCompressionFormat rotationCompressionFormat)
        {
            string compressionFormatString = InputComboBoxDialog.GetValue(this, "Select desired rotation compression format", "Rotation Compression Format Selector",
                AnimSequence.ValidRotationCompressionFormats.Select(x => x.ToString()), rotationCompressionFormat.ToString());

            return Enum.TryParse(compressionFormatString, out rotationCompressionFormat);
        }

        private void ImportFromPSA()
        {
            var dlg = new OpenFileDialog
            {
                Filter = PSAFilter,
                CheckFileExists = true,
                Title = "Select PSA",
                Multiselect = false,
                CustomPlaces = AppDirectories.GameCustomPlaces
            };
            if (dlg.ShowDialog(this) == true)
            {
                PSA psa = PSA.FromFile(dlg.FileName);
                List<AnimSequence> psaSeqs = psa.GetAnimSequences();
                if (psaSeqs.IsEmpty())
                {
                    MessageBox.Show("This PSA is empty!", "", MessageBoxButton.OK, MessageBoxImage.Error); //can this happen?
                    return;
                }

                AnimationCompressionFormat rotationCompressionFormat = AnimationCompressionFormat.ACF_Float96NoW;
                if (!TryGetRotationCompressionFormat(ref rotationCompressionFormat))
                {
                    return;
                }

                List<AnimSequence> seqsToImport;
                if (psaSeqs.Count > 1)
                {
                    const string allOption = "(Import all)";
                    var choices = psaSeqs.Select(s => s.Name.Instanced).Prepend(allOption);
                    string chosen = InputComboBoxDialog.GetValue(this, "Select which animation(s) to import", "Animation Selector", choices, allOption);
                    if (chosen.IsEmpty())
                    {
                        return;
                    }
                    seqsToImport = chosen == allOption ? psaSeqs : [psaSeqs.First(s => s.Name.Instanced == chosen)];
                }
                else
                {
                    seqsToImport = psaSeqs;
                }

                var pkg = ExportCreator.CreatePackageExport(Pcc, NameReference.FromInstancedString(Path.GetFileNameWithoutExtension(dlg.FileName)));

                var bioAnimSetData = ExportCreator.CreateExport(Pcc, "BioAnimSetData", "BioAnimSetData", pkg);
                bioAnimSetData.WriteProperty(new ArrayProperty<NameProperty>(seqsToImport[0].Bones.Select(b => new NameProperty(NameReference.FromInstancedString(b.Trim().Replace(' ', '-')))), "TrackBoneNames"));

                foreach (AnimSequence seq in seqsToImport)
                {
                    var seqExp = ExportCreator.CreateExport(Pcc, NameReference.FromInstancedString(seq.Name), "AnimSequence", pkg);
                    var props = seqExp.GetProperties();
                    seq.UpdateProps(props, Pcc.Game, rotationCompressionFormat, forceUpdate: true);
                    props.AddOrReplaceProp(new ObjectProperty(bioAnimSetData, "m_pBioAnimSetData"));
                    seqExp.WriteProperties(props);
                    seqExp.WriteBinary(seq);
                }
                MessageBox.Show("Done!", "Import From PSA", MessageBoxButton.OK);
            }
        }

        private void ReplaceFromUDK()
        {
            if (CurrentExport.ClassName == "AnimSequence")
            {
                var dlg = new OpenFileDialog
                {
                    Filter = GameFileFilters.UDKFileFilter,
                    CheckFileExists = true,
                    Title = "Select UDK file",
                    Multiselect = false,
                    CustomPlaces = AppDirectories.GameCustomPlaces
                };
                if (dlg.ShowDialog(this) == true)
                {
                    using var upk = MEPackageHandler.OpenUDKPackage(dlg.FileName);
                    var animSets = upk.Exports.Where(exp => exp.ClassName == "AnimSet").ToList();
                    if (animSets.IsEmpty())
                    {
                        MessageBox.Show("This file contains no AnimSets!", "", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var curSeq = CurrentExport.GetBinaryData<AnimSequence>();
                    animSets = animSets.Where(set => set.GetProperty<ArrayProperty<NameProperty>>("TrackBoneNames").Select(np => np.Value.Instanced).SequenceEqual(curSeq.Bones)).ToList();
                    if (animSets.IsEmpty())
                    {
                        MessageBox.Show("This file contains no compatible Animations! TrackBoneNames must be identical to replace this animation.",
                                        "", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var selectedExport = EntrySelector.GetEntry<ExportEntry>(this, upk, "Select an AnimSequence", entry => entry.ClassName == "AnimSequence" && animSets.Contains(entry.Parent));
                    if (selectedExport is null)
                    {
                        return;
                    }
                    var props = CurrentExport.GetProperties();
                    if (props.GetProp<EnumProperty>("RotationCompressionFormat") is not EnumProperty compressionFormatEnum ||
                        !Enum.TryParse(compressionFormatEnum.Value, out AnimationCompressionFormat rotationCompressionFormat))
                    {
                        rotationCompressionFormat = AnimationCompressionFormat.ACF_Float96NoW;
                    }

                    if (!TryGetRotationCompressionFormat(ref rotationCompressionFormat))
                    {
                        return;
                    }

                    var selectedAnimSequence = selectedExport.GetBinaryData<AnimSequence>();

                    var originalSeqName = props.GetProp<NameProperty>("SequenceName");
                    selectedAnimSequence.UpdateProps(props, CurrentExport.Game, rotationCompressionFormat);
                    if (originalSeqName != null)
                    {
                        props.AddOrReplaceProp(originalSeqName);
                    }
                    CurrentExport.WriteProperties(props);
                    CurrentExport.WriteBinary(selectedAnimSequence);
                    MessageBox.Show("Done!", "Replace From UDK", MessageBoxButton.OK);
                }
            }
        }

        private void ImportFromUDK()
        {
            var dlg = new OpenFileDialog
            {
                Filter = GameFileFilters.UDKFileFilter,
                CheckFileExists = true,
                Title = "Select UDK file",
                Multiselect = false,
                CustomPlaces = AppDirectories.GameCustomPlaces
            };
            if (dlg.ShowDialog(this) == true)
            {
                using var upk = MEPackageHandler.OpenUDKPackage(dlg.FileName);
                var animSets = upk.Exports.Where(exp => exp.ClassName == "AnimSet").ToList();
                if (animSets.IsEmpty())
                {
                    MessageBox.Show("This file contains no AnimSets!", "", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var selectedExport = EntrySelector.GetEntry<ExportEntry>(this, upk, "Select an AnimSequence, or an Animset",
                                                                                 entry => animSets.Contains(entry) || entry.ClassName == "AnimSequence" && animSets.Contains(entry.Parent));

                var selectedAnimSequences = new List<AnimSequence>();
                ExportEntry animSet;
                switch (selectedExport?.ClassName)
                {
                    case "AnimSequence":
                        selectedAnimSequences.Add(selectedExport.GetBinaryData<AnimSequence>());
                        animSet = (ExportEntry)selectedExport.Parent;
                        break;
                    case "AnimSet":
                    {
                        animSet = selectedExport;
                        var sequences = animSet.GetProperty<ArrayProperty<ObjectProperty>>("Sequences");
                        if (sequences is null || sequences.IsEmpty())
                        {
                            MessageBox.Show("This AnimSets has no AnimSeqeunces!", "", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        selectedAnimSequences.AddRange(sequences.Select(op => upk.GetUExport(op.Value).GetBinaryData<AnimSequence>()));
                        break;
                    }
                    default:
                        return;
                }

                var rotationCompressionFormat = AnimationCompressionFormat.ACF_Float96NoW;
                if (!TryGetRotationCompressionFormat(ref rotationCompressionFormat))
                {
                    return;
                }

                var pkg = ExportCreator.CreatePackageExport(Pcc, animSet.ObjectName);

                var bioAnimSetData = ExportCreator.CreateExport(Pcc, animSet.ObjectName, "BioAnimSetData", pkg);
                bioAnimSetData.WriteProperty(animSet.GetProperty<ArrayProperty<NameProperty>>("TrackBoneNames"));

                foreach (AnimSequence seq in selectedAnimSequences)
                {
                    var seqExp = ExportCreator.CreateExport(Pcc, NameReference.FromInstancedString(seq.Name), "AnimSequence", pkg);
                    var props = seqExp.GetProperties();
                    seq.UpdateProps(props, Pcc.Game, rotationCompressionFormat);
                    props.AddOrReplaceProp(new ObjectProperty(bioAnimSetData, "m_pBioAnimSetData"));
                    seqExp.WriteProperties(props);
                    seqExp.WriteBinary(seq);
                }
                MessageBox.Show("Done!", "Import From UDK", MessageBoxButton.OK);
            }
        }

        #endregion

        #region Helpers

        private bool IsBioAnimDataSelected() => CurrentExport?.ClassName == "BioAnimSetData";

        private bool IsAnimSequenceSelected() => CurrentExport?.ClassName == "AnimSequence";

        private bool IsPackageLoaded() => Pcc != null;

        #endregion

        #region File operations

        private async void SaveFile()
        {
            await Pcc.SaveAsync();
        }

        private async void SaveFileAs()
        {
            string fileFilter;
            switch (Pcc.Game)
            {
                case MEGame.ME1:
                    fileFilter = GameFileFilters.ME1SaveFileFilter;
                    break;
                case MEGame.ME2:
                case MEGame.ME3:
                    fileFilter = GameFileFilters.ME3ME2SaveFileFilter;
                    break;
                case MEGame.LE1:
                case MEGame.LE2:
                case MEGame.LE3:
                    fileFilter = GameFileFilters.LESaveFileFilter;
                    break;
                default:
                    string extension = Path.GetExtension(Pcc.FilePath);
                    fileFilter = $"*{extension}|*{extension}";
                    break;
            }
            var d = new SaveFileDialog { Filter = fileFilter };
            if (d.ShowDialog() == true)
            {
                await Pcc.SaveAsync(d.FileName);
                MessageBox.Show("Done");
            }
        }

        private void OpenFile()
        {
            var d = AppDirectories.GetOpenPackageDialog();
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
                StatusBar_LeftMostText.Text = $"Loading {Path.GetFileName(s)} ({FileSize.FormatSize(new FileInfo(s).Length)})";
                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
                LoadMEPackage(s);

                AnimSequenceExports.ReplaceAll(Pcc.Exports.Where(exp => exp.ClassName == "AnimSequence"));
                SkeletalMeshExports.ReplaceAll(Pcc.Exports.Where(exp => exp.ClassName == "SkeletalMesh"));

                StatusBar_LeftMostText.Text = Path.GetFileName(s);
                Title = $"Animation Importer/Exporter - {s}";

                RecentsController.AddRecent(s, false, Pcc?.Game);
                RecentsController.SaveRecentList(true);
                if (goToIndex != 0)
                {
                    CurrentExport = AnimSequenceExports.FirstOrDefault(x => x.UIndex == goToIndex);
                    ExportQueuedForFocusing = CurrentExport;
                }
            }
            catch (Exception e)
            {
                StatusBar_LeftMostText.Text = "Failed to load " + Path.GetFileName(s);
                MessageBox.Show($"Error loading {Path.GetFileName(s)}:\n{e.Message}");
            }
        }

        #endregion

        public override void HandleUpdate(List<PackageUpdate> updates)
        {
            if (CurrentExport != null && updates.Any(update => update.Change == PackageChange.ExportData && update.Index == CurrentExport.UIndex) && CurrentExport.ClassName == "AnimSequence")
            {
                CurrentExport = CurrentExport;//trigger propertyset stuff
            }

            List<PackageUpdate> exportUpdates = updates.Where(upd => upd.Change.HasFlag(PackageChange.Export)).ToList();
            bool shouldUpdateList = false;
            foreach (ExportEntry animSequenceExport in AnimSequenceExports)
            {
                if (exportUpdates.Any(upd => upd.Index == animSequenceExport.UIndex))
                {
                    shouldUpdateList = true;
                    break;
                }
            }

            if (!shouldUpdateList)
            {
                foreach (PackageUpdate update in exportUpdates)
                {
                    if (Pcc.GetEntry(update.Index) is IEntry {ClassName: "AnimSequence"})
                    {
                        shouldUpdateList = true;
                        break;
                    }
                }
            }

            if (shouldUpdateList)
            {
                AnimSequenceExports.ReplaceAll(Pcc.Exports.Where(exp => exp.ClassName == "AnimSequence"));
            }
        }

        private void AnimationImporter_OnLoaded(object sender, RoutedEventArgs e)
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

                    if (AnimSequenceExports.Contains(ExportQueuedForFocusing))
                    {
                        CurrentExport = ExportQueuedForFocusing;
                    }
                    ExportQueuedForFocusing = null;

                    Activate();
                }));
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string ext = Path.GetExtension(files[0]).ToLower();
                if (ext != ".upk" && ext != ".pcc" && ext != ".sfm")
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string ext = Path.GetExtension(files[0]).ToLower();
                if (ext == ".upk" || ext == ".pcc" || ext == ".sfm")
                {
                    LoadFile(files[0]);
                }
            }
        }
        private void OpenInPackageEditor_Clicked(object sender, RoutedEventArgs e)
        {
            if (AnimExportsListBox.SelectedItem is ExportEntry export)
            {
                var p = new PackageEditor.PackageEditorWindow();
                p.Show();
                p.LoadFile(export.FileRef.FilePath, export.UIndex);
                p.Activate(); //bring to front
            }
        }

        public void PropogateRecentsChange(string propogationSource, IEnumerable<RecentsControl.RecentItem> newRecents)
        {
            RecentsController.PropogateRecentsChange(false, newRecents);
        }

        public string Toolname => "AnimationImporterExporter";

        private void AnimationImporter_OnClosing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
                return;

            AnimPreview?.Dispose();
            InterpreterTab_Interpreter?.Dispose();
            BinaryInterpreterTab_BinaryInterpreter?.Dispose();
            RecentsController?.Dispose();
        }
    }
}
