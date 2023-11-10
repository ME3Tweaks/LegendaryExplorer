using Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.Packages;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.SharedUI.PeregrineTreeView;
using LegendaryExplorer.Tools.PathfindingEditor;
using LegendaryExplorer.Tools.PlotEditor;
using LegendaryExplorer.Tools.Sequence_Editor.Experiments;
using LegendaryExplorer.Tools.SequenceObjects;
using LegendaryExplorer.UserControls.SharedToolControls;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using Piccolo;
using Piccolo.Event;
using Piccolo.Nodes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LegendaryExplorer.Tools.CustomFilesManager;
using Color = System.Drawing.Color;
using Image = System.Drawing.Image;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using LegendaryExplorer.Tools.PackageEditor;

namespace LegendaryExplorer.Tools.Sequence_Editor
{
    /// <summary>
    /// Interaction logic for SequenceEditorWPF.xaml
    /// </summary>
    public partial class SequenceEditorWPF : WPFBase, IRecents
    {
        private readonly SequenceGraphEditor graphEditor;
        public ObservableCollectionExtended<SObj> CurrentObjects { get; } = new();
        public ObservableCollectionExtended<SObj> SelectedObjects { get; } = new();
        public ObservableCollectionExtended<ExportEntry> SequenceExports { get; } = new();
        public ObservableCollectionExtended<TreeViewEntry> TreeViewRootNodes { get; } = new();
        public string CurrentFile;
        public string JSONpath;

        private bool _useSavedViews = true; // Should probably be a global setting

        public bool UseSavedViews
        {
            get => _useSavedViews;
            set
            {
                if (SetProperty(ref _useSavedViews, value) && SelectedSequence != null)
                {
                    LoadSequence(SelectedSequence);
                }
            }
        }

        private ExportEntry _selectedSequence;

        public ExportEntry SelectedSequence
        {
            get => _selectedSequence;
            set => SetProperty(ref _selectedSequence, value);
        }

        public record SavedViewData(Dictionary<int, PointF> Positions, RectangleF ViewBounds);

        private SavedViewData SavedView;

        public static readonly string SequenceEditorDataFolder =
            Path.Combine(AppDirectories.AppDataFolder, @"SequenceEditor\");

        public static readonly string
            OptionsPath = Path.Combine(SequenceEditorDataFolder, "SequenceEditorOptions.JSON");

        public static readonly string ME3ViewsPath = Path.Combine(SequenceEditorDataFolder, @"ME3SequenceViews\");
        public static readonly string ME2ViewsPath = Path.Combine(SequenceEditorDataFolder, @"ME2SequenceViews\");
        public static readonly string ME1ViewsPath = Path.Combine(SequenceEditorDataFolder, @"ME1SequenceViews\");
        public static readonly string LE3ViewsPath = Path.Combine(SequenceEditorDataFolder, @"LE3SequenceViews\");
        public static readonly string LE2ViewsPath = Path.Combine(SequenceEditorDataFolder, @"LE2SequenceViews\");
        public static readonly string LE1ViewsPath = Path.Combine(SequenceEditorDataFolder, @"LE1SequenceViews\");

        public SequenceEditorWPF() : base("Sequence Editor")
        {
            LoadCommands();
            DataContext = this;
            StatusText = "Select package file to load";
            InitializeComponent();

            RecentsController.InitRecentControl(Toolname, Recents_MenuItem, x => LoadFile(x));

            graphEditor = (SequenceGraphEditor)GraphHost.Child;
            graphEditor.BackColor = GraphEditorBackColor;
            graphEditor.Camera.MouseDown += backMouseDown_Handler;
            graphEditor.Camera.MouseUp += back_MouseUp;

            graphEditor.Click += graphEditor_Click;
            graphEditor.DragDrop += SequenceEditor_DragDrop;
            graphEditor.DragEnter += SequenceEditor_DragEnter;

            favoritesToolBox.DoubleClickCallback = CreateNewObject;
            eventsToolBox.DoubleClickCallback = CreateNewObject;
            actionsToolBox.DoubleClickCallback = CreateNewObject;
            conditionsToolBox.DoubleClickCallback = CreateNewObject;
            variablesToolBox.DoubleClickCallback = CreateNewObject;
            customSequencesToolBox.DoubleClickCallback = CreateCustomSequence;

            favoritesToolBox.ShiftClickCallback = RemoveFavorite;
            eventsToolBox.ShiftClickCallback = SetFavorite;
            actionsToolBox.ShiftClickCallback = SetFavorite;
            conditionsToolBox.ShiftClickCallback = SetFavorite;
            variablesToolBox.ShiftClickCallback = SetFavorite;
            // Custom sequences are not ClassInfo so they cannot be set as a favorite

            AutoSaveView_MenuItem.IsChecked = Settings.SequenceEditor_AutoSaveViewV2;
            ShowOutputNumbers_MenuItem.IsChecked = Settings.SequenceEditor_ShowOutputNumbers;
            SObj.OutputNumbers = ShowOutputNumbers_MenuItem.IsChecked;
        }

        private void CreateCustomSequence(object obj)
        {
            var customInfo = customSequencesToolBox.SelectedItem as CustomAsset;
            if (customInfo == null || !File.Exists(customInfo.PackageFilePath) || SelectedSequence == null)
                return;

            using var p = MEPackageHandler.OpenMEPackage(customInfo.PackageFilePath);
            var sourceExp = p.FindExport(customInfo.InstancedFullPath);
            if (sourceExp == null)
            {
                MessageBox.Show(
                    $"Cannot find export '{customInfo.InstancedFullPath}' in package file '{customInfo.PackageFilePath}'.");
                return;
            }

            SequenceEditorExperimentsM.InstallSequencePrefab(sourceExp, SelectedSequence);
        }

        public SequenceEditorWPF(ExportEntry export) : this()
        {
            FileQueuedForLoad = export.FileRef.FilePath;
            ExportQueuedForFocusing = export;
        }

        public SequenceEditorWPF(IMEPackage package) : this()
        {
            PackageQueuedForLoad = package;
        }

        public ICommand OpenCommand { get; set; }
        public ICommand SaveCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }
        public ICommand SaveImageCommand { get; set; }
        public ICommand SaveViewCommand { get; set; }
        public ICommand AutoLayoutCommand { get; set; }
        public ICommand UseSavedViewsCommand { get; set; }
        public ICommand ScanFolderForLoopsCommand { get; set; }
        public ICommand CheckSequenceSetsCommand { get; set; }
        public ICommand ConvertSeqActLogCommentCommand { get; set; }
        public ICommand GotoCommand { get; set; }
        public ICommand KismetLogCommand { get; set; }
        public ICommand KismetLogCurrentSequenceCommand { get; set; }
        public ICommand SearchCommand { get; set; }
        public ICommand ForceReloadPackageCommand { get; set; }
        public ICommand ResetFavoritesCommand { get; set; }
        public ICommand OpenOtherVersionCommand { get; set; }
        public ICommand ComparePackagesCommand { get; set; }
        public ICommand CompareToUnmoddedCommand { get; set; }
        public ICommand DesignerCreateInputCommand { get; set; }
        public ICommand DesignerCreateOutputCommand { get; set; }
        public ICommand DesignerCreateExternCommand { get; set; }
        public ICommand OpenHighestMountedCommand { get; set; }

        private void LoadCommands()
        {
            ForceReloadPackageCommand = new GenericCommand(ForceReloadPackageWithoutSharing, CanForceReload);
            OpenCommand = new GenericCommand(OpenPackage);
            SaveCommand = new GenericCommand(SavePackage, PackageIsLoaded);
            SaveAsCommand = new GenericCommand(SavePackageAs, PackageIsLoaded);
            SaveImageCommand = new GenericCommand(SaveImage, () => CurrentObjects.Any);
            SaveViewCommand = new GenericCommand(() => saveView(), () => CurrentObjects.Any);
            AutoLayoutCommand = new GenericCommand(() => AutoLayout(), () => CurrentObjects.Any);
            GotoCommand = new GenericCommand(GoTo, PackageIsLoaded);
            KismetLogCommand = new RelayCommand(OpenKismetLogParser, CanOpenKismetLog);
            ScanFolderForLoopsCommand = new GenericCommand(ScanFolderPackagesForTightLoops);
            CheckSequenceSetsCommand = new GenericCommand(() => SequenceEditorExperimentsM.CheckSequenceSets(this),
                () => CurrentObjects.Any);
            ConvertSeqActLogCommentCommand = new GenericCommand(
                () => SequenceEditorExperimentsM.ConvertSeqAct_Log_objComments(Pcc), () => SequenceExports.Any);
            SearchCommand = new GenericCommand(SearchDialogue, () => CurrentObjects.Any);
            UseSavedViewsCommand = new GenericCommand(ToggleSavedViews,
                () => Pcc != null && (Pcc is { Game: MEGame.ME1 } || Pcc.Game.IsLEGame()));
            ResetFavoritesCommand = new GenericCommand(ResetFavorites, () => Pcc != null);
            OpenOtherVersionCommand = new GenericCommand(OpenOtherVersion, () => Pcc != null && Pcc.Game.IsMEGame());
            CompareToUnmoddedCommand =
                new GenericCommand(() => SharedPackageTools.ComparePackageToUnmodded(this, entryDoubleClick),
                    () => SharedPackageTools.CanCompareToUnmodded(this));
            ComparePackagesCommand =
                new GenericCommand(() => SharedPackageTools.ComparePackageToAnother(this, entryDoubleClick),
                    PackageIsLoaded);
            OpenHighestMountedCommand = new GenericCommand(OpenHighestMountedVersion, IsLoadedPackageME);

            DesignerCreateExternCommand = new GenericCommand(CreateExtern, () => SelectedSequence != null);
            DesignerCreateInputCommand = new GenericCommand(CreateInput, () => SelectedSequence != null);
            DesignerCreateOutputCommand = new GenericCommand(CreateOutput, () => SelectedSequence != null);
        }

        private void CreateOutput()
        {
            var outputLabel = PromptDialog.Prompt(this, "Enter an output label for this sequence.", "Enter label",
                "Out", true);
            if (string.IsNullOrWhiteSpace(outputLabel))
                return;

            // Create an add activation to sequence
            var finished = SequenceObjectCreator.CreateSequenceObject(Pcc, "SeqAct_FinishSequence");
            finished.WriteProperty(new StrProperty(outputLabel, "OutputLabel"));
            finished.idxLink = SelectedSequence.UIndex;
            // Reindex if necessary
            var expCount = Pcc.Exports.Count(x => x.InstancedFullPath == finished.InstancedFullPath);
            if (expCount > 1)
            {
                // update the index
                finished.ObjectName = Pcc.GetNextIndexedName(finished.ObjectName.Name);
            }

            KismetHelper.AddObjectToSequence(finished, SelectedSequence);

            // Add output link to sequence
            var outputLinks = SelectedSequence.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            if (outputLinks == null)
            {
                outputLinks = new ArrayProperty<StructProperty>("OutputLinks");
            }

            // Add struct
            PropertyInfo p = GlobalUnrealObjectInfo.GetPropertyInfo(Pcc.Game, "OutputLinks", "Sequence");
            if (p == null)
            {
                Debugger.Break();
            }

            if (p != null)
            {
                string typeName = p.Reference;
                PropertyCollection props = GlobalUnrealObjectInfo.getDefaultStructValue(Pcc.Game, typeName, true);
                props.AddOrReplaceProp(new NameProperty(finished.ObjectName, "LinkAction"));
                props.AddOrReplaceProp(new StrProperty(outputLabel, "LinkDesc"));
                props.AddOrReplaceProp(new ObjectProperty(finished, "LinkedOp"));
                outputLinks.Add(new StructProperty(typeName, props, isImmutable: false));
            }

            SelectedSequence.WriteProperty(outputLinks);
        }

        private void CreateInput()
        {
            var inputLabel = PromptDialog.Prompt(this, "Enter an input label for this activation.", "Enter label", "In",
                true);
            if (string.IsNullOrWhiteSpace(inputLabel))
                return;

            // Create an add activation to sequence
            var activation = SequenceObjectCreator.CreateSequenceObject(Pcc, "SeqEvent_SequenceActivated");
            activation.idxLink = SelectedSequence.UIndex;
            // Reindex if necessary
            var expCount = Pcc.Exports.Count(x => x.InstancedFullPath == activation.InstancedFullPath);
            if (expCount > 1)
            {
                // update the index
                activation.ObjectName = Pcc.GetNextIndexedName(activation.ObjectName.Name);
            }

            KismetHelper.AddObjectToSequence(activation, SelectedSequence);

            // Add input link to sequence
            var inputLinks = SelectedSequence.GetProperty<ArrayProperty<StructProperty>>("InputLinks");
            if (inputLinks == null)
            {
                inputLinks = new ArrayProperty<StructProperty>("InputLinks");
            }

            // Add struct
            PropertyInfo p = GlobalUnrealObjectInfo.GetPropertyInfo(Pcc.Game, "InputLinks", "Sequence");
            if (p == null)
            {
                Debugger.Break();
            }

            if (p != null)
            {
                string typeName = p.Reference;
                PropertyCollection props = GlobalUnrealObjectInfo.getDefaultStructValue(Pcc.Game, typeName, true);
                props.AddOrReplaceProp(new NameProperty(activation.ObjectName, "LinkAction"));
                props.AddOrReplaceProp(new StrProperty(inputLabel, "LinkDesc"));
                props.AddOrReplaceProp(new ObjectProperty(activation, "LinkedOp"));
                inputLinks.Add(new StructProperty(typeName, props, isImmutable: false));
            }

            SelectedSequence.WriteProperty(inputLinks);
        }

        private void CreateExtern()
        {
            var externName = PromptDialog.Prompt(this, "Enter an variable label for this external variable.",
                "Enter label", "", true);
            if (string.IsNullOrWhiteSpace(externName))
                return;

            var classOptions = GlobalUnrealObjectInfo.GetClasses(Pcc.Game).Values
                .Where(x => x.IsA("SequenceVariable", Pcc.Game)).Select(x => x.ClassName).OrderBy(x => x).ToList();
            var externDataType = InputComboBoxDialog.GetValue(this, "Select datatype for this external variable.",
                "Select datatype",
                classOptions);

            if (string.IsNullOrWhiteSpace(externDataType))
            {
                return;
            }


            // Create a new extern
            var externalVar = SequenceObjectCreator.CreateSequenceObject(Pcc, "SeqVar_External");
            externalVar.idxLink = SelectedSequence.UIndex;

            var expectedDataTypeClass =
                EntryImporter.EnsureClassIsInFile(Pcc, externDataType, new RelinkerOptionsPackage());
            externalVar.WriteProperty(new StrProperty(externName, "VariableLabel"));
            externalVar.WriteProperty(new ObjectProperty(expectedDataTypeClass, "ExpectedType"));
            // Reindex if necessary
            var expCount = Pcc.Exports.Count(x => x.InstancedFullPath == externalVar.InstancedFullPath);
            if (expCount > 1)
            {
                // update the index
                externalVar.ObjectName = Pcc.GetNextIndexedName(externalVar.ObjectName.Name);
            }

            KismetHelper.AddObjectToSequence(externalVar, SelectedSequence);

            // Add input link to sequence
            var variableLinks = SelectedSequence.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
            if (variableLinks == null)
            {
                variableLinks = new ArrayProperty<StructProperty>("VariableLinks");
            }

            // Add struct to VariableLinks
            PropertyInfo p = GlobalUnrealObjectInfo.GetPropertyInfo(Pcc.Game, "VariableLinks", "Sequence");
            if (p == null)
            {
                Debugger.Break();
            }

            if (p != null)
            {
                string typeName = p.Reference;
                PropertyCollection props = GlobalUnrealObjectInfo.getDefaultStructValue(Pcc.Game, typeName, true);
                props.AddOrReplaceProp(new NameProperty(externalVar.ObjectName, "LinkVar"));
                props.AddOrReplaceProp(new StrProperty(externName, "LinkDesc"));
                props.AddOrReplaceProp(new ObjectProperty(expectedDataTypeClass, "ExpectedType"));
                variableLinks.Add(new StructProperty(typeName, props, isImmutable: false));
            }

            SelectedSequence.WriteProperty(variableLinks);
        }

        private void entryDoubleClick(EntryStringPair clickedItem)
        {
            if (clickedItem?.Entry != null && clickedItem.Entry.UIndex != 0)
            {
                GoToExport(clickedItem.Entry.UIndex);
            }
        }

        private void ToggleSavedViews()
        {
            UseSavedViews = !UseSavedViews;
        }

        private bool CanForceReload() => App.IsDebug && PackageIsLoaded();

        private string searchtext = "";

        private void SearchDialogue()
        {
            const string input = "Enter text to search comments for";
            searchtext = PromptDialog.Prompt(this, input, "Search Comments", searchtext, true);

            if (!string.IsNullOrEmpty(searchtext))
            {
                SObj selectedObj = SelectedObjects.FirstOrDefault();
                var tgt = CurrentObjects.AfterThenBefore(selectedObj).FirstOrDefault(d =>
                    d.Comment.Contains(searchtext, StringComparison.InvariantCultureIgnoreCase));
                if (tgt != null)
                {
                    GoToExport(tgt.Export);
                }
                else
                {
                    MessageBox.Show($"No comment with \"{searchtext}\" found");
                }
            }
        }

        private void ScanFolderPackagesForTightLoops()
        {
            //This method ignores gates because they always link to themselves. Well, mostly.
            var dlg = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = "Select folder containing package files"
            };
            //SirC is going to love this level of indention
            //lol just kidding
            //sorry in advance
            //-Mgamerz
            if (dlg.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                var packageFolderPath = dlg.FileName;
                var packageFiles =
                    Directory.EnumerateFiles(packageFolderPath, "*.pcc",
                        SearchOption.TopDirectoryOnly); //pcc only for now. not sure upk/u/sfm is worth it, maybe.
                List<string> tightLoops = new List<string>();
                foreach (var file in packageFiles)
                {
                    Debug.WriteLine("Opening package " + file);
                    var p = MEPackageHandler.OpenMEPackage(file);
                    //find sequence objects
                    var sequences = p.Exports.Where(x => !x.IsDefaultObject && x.ClassName == "Sequence");
                    foreach (var sequence in sequences)
                    {
                        //get list of items in the sequence
                        var seqObjectsList = sequence.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
                        if (seqObjectsList != null)
                        {
                            foreach (var seqObjectRef in seqObjectsList)
                            {
                                var seqObj = p.GetUExport(seqObjectRef.Value);
                                if (seqObj.ClassName is "SeqAct_Gate") continue;
                                ; //skip gates
                                var outputLinks = seqObj.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
                                if (outputLinks != null)
                                {
                                    foreach (var outlink in outputLinks)
                                    {
                                        var links = outlink.GetProp<ArrayProperty<StructProperty>>("Links");
                                        if (links != null)
                                        {
                                            foreach (var link in links)
                                            {
                                                var linkedOp = link.GetProp<ObjectProperty>("LinkedOp");
                                                if (linkedOp != null)
                                                {
                                                    //this is what we are looking for. See if reference to self
                                                    if (linkedOp.Value == seqObj.UIndex)
                                                    {
                                                        //!! Self reference
                                                        tightLoops.Add(
                                                            $"Tight loop in {Path.GetFileName(file)}, export {seqObjectRef.Value} {seqObj.InstancedFullPath}");
                                                    }
                                                }

                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (tightLoops.Any())
                {
                    var ld = new ListDialog(tightLoops, "Tight sequence loops found",
                        "The following sequence objects link to themselves on an output and may cause significant harm to game performance.",
                        this);
                    ld.Show();
                }
                else
                {
                    MessageBox.Show("No tight loops found");
                }
            }
        }

        private async void CreateNewObject(ClassInfo info)
        {
            if (SelectedSequence == null)
            {
                return;
            }

            IEntry classEntry;
            if (Pcc.Exports.Any(exp => exp.ObjectName == info.ClassName) ||
                Pcc.Imports.Any(imp => imp.ObjectName == info.ClassName) ||
                GlobalUnrealObjectInfo.GetClassOrStructInfo(Pcc.Game, info.ClassName) is { } classInfo &&
                EntryImporter.IsSafeToImportFrom(classInfo.pccPath, Pcc.Game, Pcc.FilePath))
            {
                var rop = new RelinkerOptionsPackage();
                classEntry = EntryImporter.EnsureClassIsInFile(Pcc, info.ClassName, rop);
                EntryImporterExtended.ShowRelinkResultsIfAny(rop);
            }
            else
            {
                SetBusy($"Adding {info.ClassName}");
                classEntry = await Task.Run(() =>
                {
                    var rop = new RelinkerOptionsPackage();
                    var result = EntryImporter.EnsureClassIsInFile(Pcc, info.ClassName, rop);
                    EntryImporterExtended.ShowRelinkResultsIfAny(rop);
                    return result;
                }).ConfigureAwait(true);
            }

            if (classEntry is null)
            {
                EndBusy();
                MessageBox.Show(this,
                    $"Could not import {info.ClassName}'s class definition! It may be defined in a DLC you don't have.");
                return;
            }

            var packageCache = new PackageCache { AlwaysOpenFromDisk = false };
            packageCache.InsertIntoCache(Pcc);
            var newSeqObj = new ExportEntry(Pcc, SelectedSequence, Pcc.GetNextIndexedName(info.ClassName),
                properties: SequenceObjectCreator.GetSequenceObjectDefaults(Pcc, info, packageCache))
            {
                Class = classEntry,
            };
            newSeqObj.ObjectFlags |= UnrealFlags.EObjectFlags.Transactional;
            Pcc.AddExport(newSeqObj);
            addObject(newSeqObj);
            EndBusy();
        }

        private bool CanOpenKismetLog(object o)
        {
            switch (o)
            {
                case true:
                    return Pcc != null && File.Exists(KismetLogParser.KismetLogPath(Pcc.Game));
                case MEGame game:
                    return File.Exists(KismetLogParser.KismetLogPath(game));
                case "CurrentSequence":
                    return Pcc != null && File.Exists(KismetLogParser.KismetLogPath(Pcc.Game)) &&
                           SelectedSequence != null;
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
                    case "CurrentSequence":
                        kismetLogParser.LoadLog(Pcc.Game, Pcc, SelectedSequence);
                        break;
                    default:
                        return;
                }

                kismetLogParser.Visibility = Visibility.Visible;
                kismetLogParserRow.Height = new GridLength(150);
                kismetLogParser.ExportFound = (filePath, uIndex) =>
                {
                    if (Pcc == null || Pcc.FilePath != filePath) LoadFile(filePath);
                    GoToExport(Pcc.GetUExport(uIndex), goIntoSequences: false);
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

        #region Busy

        public override void SetBusy(string text = null)
        {
            Image graphImage = graphEditor.Camera.ToImage((int)graphEditor.Camera.GlobalFullWidth,
                (int)graphEditor.Camera.GlobalFullHeight, new SolidBrush(GraphEditorBackColor));
            graphImageSub.Source = graphImage.ToBitmapImage();
            graphImageSub.Width = graphGrid.ActualWidth;
            graphImageSub.Height = graphGrid.ActualHeight;
            if (toolBoxExpander.ActualHeight > 0 && toolBoxExpander.ActualWidth > 0)
            {
                // Do not draw if area == 0
                expanderImageSub.Source = toolBoxExpander.DrawToBitmapSource();
            }

            expanderImageSub.Width = toolBoxExpander.ActualWidth;
            expanderImageSub.Height = toolBoxExpander.ActualHeight;
            expanderImageSub.Visibility = Visibility.Visible;
            graphImageSub.Visibility = Visibility.Visible;
            BusyText = text;
            IsBusy = true;
        }

        public override void EndBusy()
        {
            IsBusy = false;
            graphImageSub.Visibility = expanderImageSub.Visibility = Visibility.Collapsed;
        }

        #endregion

        private string _statusText;

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
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
                    if (value.Entry is ExportEntry exportEntry)
                    {
                        value.IsSelected = true;
                        LoadSequence(exportEntry);
                    }
                    else
                    {
                        MessageBox.Show(this, "Can't select an imported sequence");
                    }
                }
            }
        }

        private async void SavePackageAs()
        {
            string extension = Path.GetExtension(Pcc.FilePath);
            var d = new SaveFileDialog { Filter = $"*{extension}|*{extension}" };
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

        private void OpenPackage()
        {
            var d = AppDirectories.GetOpenPackageDialog();
            if (d.ShowDialog() == true)
            {
                try
                {
                    LoadFile(d.FileName);
                }
                catch (Exception ex) when (!App.IsDebug)
                {
                    MessageBox.Show(this, "Unable to open file:\n" + ex.Message);
                }
            }
        }

        private bool PackageIsLoaded()
        {
            return Pcc != null;
        }

        private void preloadPackage(string filePath, long packageSize)
        {
            try
            {
                SelectedSequence = null;
                CurrentObjects.ClearEx();
                SequenceExports.ClearEx();
                SelectedObjects.ClearEx();
            }
            catch (Exception ex) when (!App.IsDebug)
            {
                MessageBox.Show(this, "Package Pre-Load Error:\n" + ex.Message);
                Title = "Sequence Editor";
                CurrentFile = null;
                UnLoadMEPackage();
            }
        }

        public void postloadPackage(string filePath)
        {
            try
            {
                LoadSequences();
                if (TreeViewRootNodes.IsEmpty())
                {
                    UnLoadMEPackage();
                    MessageBox.Show(this, "This file does not contain any sequences!");
                    StatusText = "Select package file to load";
                    return;
                }

                graphEditor.nodeLayer.RemoveAllChildren();
                graphEditor.edgeLayer.RemoveAllChildren();

                Title = $"Sequence Editor - {filePath}";
                StatusText = GetStatusBarText();

                RefreshToolboxItems(true);
            }
            catch (Exception ex) when (!App.IsDebug)
            {
                MessageBox.Show(this, "Package Post-Load Error:\n" + ex.Message);
                Title = "Sequence Editor";
                CurrentFile = null;
                UnLoadMEPackage();
            }
        }

        private void OpenHighestMountedVersion()
        {
            if (MEDirectories.GetBioGamePath(Pcc.Game) is null)
            {
                MessageBox.Show($"No {Pcc.Game} installation detected!");
                return;
            }

            string fileName = Path.GetFileName(Pcc.FilePath);
            if (!MELoadedFiles.TryGetHighestMountedFile(Pcc.Game, fileName, out string filePath))
            {
                MessageBox.Show($"No file named '{fileName}' was found in the {Pcc.Game} installation.");
            }
            else if (Path.GetFullPath(filePath) == Path.GetFullPath(Pcc.FilePath))
            {
                MessageBox.Show($"This is the highest mounted version of {fileName} in your {Pcc.Game} installation.");
            }
            else
            {
                var entry = SelectedItem?.Entry ?? SelectedSequence;
                var pe = new SequenceEditorWPF();
                pe.LoadFileAndGoTo(filePath, goToEntry: entry?.InstancedFullPath);
                pe.Show();
            }
        }

        /// <summary>
        /// Reloads the toolbox data
        /// </summary>
        public void RefreshToolboxItems(bool includeCustomSequences = false)
        {
            if (Pcc != null)
            {
                favoritesToolBox.Classes.ClearEx();
                favoritesToolBox.Classes.AddRange(GetSavedFavorites());
                eventsToolBox.Classes.ClearEx();
                eventsToolBox.Classes.AddRange(SequenceObjectCreator.GetSequenceEvents(Pcc.Game)
                    .OrderBy(info => info.ClassName));
                actionsToolBox.Classes.ClearEx();
                actionsToolBox.Classes.AddRange(SequenceObjectCreator.GetSequenceActions(Pcc.Game)
                    .OrderBy(info => info.ClassName));
                conditionsToolBox.Classes.ClearEx();
                conditionsToolBox.Classes.AddRange(SequenceObjectCreator.GetSequenceConditions(Pcc.Game)
                    .OrderBy(info => info.ClassName));
                variablesToolBox.Classes.ClearEx();
                variablesToolBox.Classes.AddRange(SequenceObjectCreator.GetSequenceVariables(Pcc.Game)
                    .OrderBy(info => info.ClassName));

                if (includeCustomSequences)
                {
                    customSequencesToolBox.Items.ClearEx();
                    customSequencesToolBox.Items.AddRange(CustomAssets.CustomSequences[Pcc.Game]);
                }
            }
        }

        private IEnumerable<ClassInfo> GetSavedFavorites()
        {
            if (Pcc != null)
            {
                var setting = Settings.Get_SequenceEditor_Favorites(Pcc.Game);
                var classes = setting.Split(";");
                return classes.Select(className => GlobalUnrealObjectInfo.GetClassOrStructInfo(Pcc.Game, className))
                    .NonNull().OrderBy(info => info.ClassName);
            }

            return Array.Empty<ClassInfo>();
        }

        private void SaveFavorites()
        {
            if (Pcc != null)
            {
                var classes = favoritesToolBox.Classes.Select(cl => cl.ClassName);
                var favorites = new StringBuilder();
                foreach (var cl in classes)
                {
                    favorites.Append(cl + ";");
                }

                if (favorites.Length > 0) favorites.Remove(favorites.Length - 1, 1);
                Settings.Set_SequenceEditor_Favorites(Pcc.Game, favorites.ToString());
            }
        }

        private void SetFavorite(ClassInfo classInfo)
        {
            if (!favoritesToolBox.Classes.Contains(classInfo))
            {
                favoritesToolBox.Classes.Add(classInfo);
                favoritesToolBox.Classes.Sort(cl => cl.ClassName);
                SaveFavorites();
            }
        }

        private void RemoveFavorite(ClassInfo classInfo)
        {
            favoritesToolBox.Classes.Remove(classInfo);
            SaveFavorites();
        }

        private void ResetFavorites()
        {
            favoritesToolBox.Classes.Clear();
            favoritesToolBox.Classes.AddRange(SequenceObjectCreator.GetCommonObjects(Pcc.Game)
                .OrderBy(info => info.ClassName));
            SaveFavorites();
        }

        public void LoadFileFromStream(Stream stream, string associatedFilePath, int goToIndex = 0)
        {
            try
            {
                var currentFile = Path.GetFileName(associatedFilePath);
                preloadPackage(currentFile, stream.Length);
                LoadMEPackage(stream, associatedFilePath);
                CurrentFile = currentFile;
                postloadPackage(associatedFilePath);
                if (goToIndex != 0 && Pcc.TryGetUExport(goToIndex, out var exp))
                {
                    GoToExport(exp);
                }

            }
            catch (Exception ex) when (!App.IsDebug)
            {
                MessageBox.Show(this, "Package Stream-Load Error:\n" + ex.Message);
                Title = "Sequence Editor";
                CurrentFile = null;
                UnLoadMEPackage();
            }
        }

        public void LoadFileAndGoTo(string fileName, int uIndex = 0, string goToEntry = null,
            Action loadPackageDelegate = null)
        {
            LoadFile(fileName, loadPackageDelegate);
            if (uIndex > 0)
            {
                GoToExport(uIndex);
            }
            else if (goToEntry != null)
            {
                var exp = Pcc.FindExport(goToEntry);
                if (exp != null)
                {
                    GoToExport(exp);
                }
            }
        }

        /// <summary>
        /// Loads a package file into the editor for use
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="loadPackageDelegate">Delegate that can be used to set the Pcc object on this object instead of the default from-disk loader</param>
        public void LoadFile(string fileName, Action loadPackageDelegate = null)
        {
            try
            {
                preloadPackage(fileName, 0); // We don't show the size so don't bother
                if (loadPackageDelegate != null)
                {
                    // Used for loading packages from memory from another tool
                    // This is useful for dev where you have a window open for a package that no longer exists
                    // e.g. when building mods via c# and the folder is constantly being deleted
                    loadPackageDelegate.Invoke();
                }
                else
                {
                    // Used for loading package from disk (even in shared interop already).
                    LoadMEPackage(fileName);
                }

                CurrentFile = Path.GetFileName(fileName);

                // Streams don't work for recents
                RecentsController.AddRecent(fileName, false, Pcc?.Game);
                RecentsController.SaveRecentList(true);

                postloadPackage(fileName);

            }
            catch (Exception ex) when (!App.IsDebug)
            {
                MessageBox.Show(this, "Error:\n" + ex.Message);
                Title = "Sequence Editor";
                CurrentFile = null;
                UnLoadMEPackage();
            }
        }

        private void LoadSequences()
        {
            ResetTreeView();
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

        private void ResetTreeView()
        {
            foreach (TreeViewEntry tvi in TreeViewRootNodes.SelectMany(node => node.FlattenTree()))
            {
                tvi.Dispose();
            }

            TreeViewRootNodes.ClearEx();
        }

        private TreeViewEntry FindSequences(ExportEntry rootSeq, bool wantFullName = false)
        {
            string seqName = (wantFullName && !string.IsNullOrWhiteSpace(rootSeq.ParentFullPath))
                ? $"{rootSeq.ParentInstancedFullPath}."
                : "";
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
                            TreeViewEntry treeViewEntry = null;

                            if (pcc.TryGetUExport(propSequenceReference.Value, out var exportRef))
                            {
                                treeViewEntry = FindSequences(exportRef);
                                SequenceExports.Add(exportEntry);
                            }
                            else if (pcc.TryGetImport(propSequenceReference.Value, out var importRef))
                            {
                                treeViewEntry = new TreeViewEntry(importRef,
                                    $"#{importRef.UIndex}: {importRef.InstancedFullPath}");
                            }

                            if (treeViewEntry != null)
                            {
                                root.Sublinks.Add(treeViewEntry);
                            }
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
            if (fromFile)
            {
                Properties_InterpreterWPF.LoadExport(seqExport);
                if (UseSavedViews && File.Exists(JSONpath))
                {
                    SavedView = JsonConvert.DeserializeObject<SavedViewData>(File.ReadAllText(JSONpath));
                }
                else
                {
                    SavedView = new(new(), RectangleF.Empty);
                }

                customSaveData.Clear();
                selectedExports.Clear();
            }

            try
            {
                GenerateGraph();
                if (selectedExports.Count == 1 &&
                    CurrentObjects.FirstOrDefault(obj => obj.Export == selectedExports[0]) is SObj selectedObj)
                {
                    panToSelection = false;
                    CurrentObjects_ListBox.SelectedItem = selectedObj;
                }

                if (fromFile)
                {
                    if (SavedView.ViewBounds != RectangleF.Empty)
                    {
                        graphEditor.Camera.ViewBounds = SavedView.ViewBounds;
                    }
                    else
                    {
                        RectangleF viewBounds =
                            (CurrentObjects.FirstOrDefault(obj => obj is SEvent) ?? CurrentObjects.FirstOrDefault())
                            ?.GlobalFullBounds ?? new RectangleF();
                        graphEditor.Camera.AnimateViewToCenterBounds(viewBounds, false, 0);
                    }
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
            string objectName =
                System.Text.RegularExpressions.Regex.Replace(export.ObjectName.Name, @"[<>:""/\\|?*]", "");
            string viewsPath = Pcc.Game switch
            {
                MEGame.LE1 => LE1ViewsPath,
                MEGame.LE2 => LE2ViewsPath,
                MEGame.LE3 => LE3ViewsPath,
                MEGame.ME1 => ME1ViewsPath,
                MEGame.ME2 => ME2ViewsPath,
                _ => ME3ViewsPath
            };

            JSONpath = Path.Combine(viewsPath, $"{CurrentFile}.v2#{export.UIndex - 1}{objectName}.JSON");
        }

        public void GetObjects(ExportEntry export)
        {
            CurrentObjects.ClearEx();
            var seqObjs = export.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
            if (seqObjs != null)
            {
                // Resolve imports
                //var convertedImports = new List<ExportEntry>();
                //var imports = seqObjs.Where(x => x.Value < 0).Select(x => x.ResolveToEntry(export.FileRef) as ImportEntry);

                //foreach (var import in imports)
                //{
                //    var resolved = EntryImporter.ResolveImport(import);
                //    if (resolved != null)
                //    {
                //        convertedImports.Add(resolved);
                //    }
                //}

                var nullCount = seqObjs.Count(x => x.Value == 0);

                CurrentObjects.AddRange(seqObjs.OrderBy(prop => prop.Value)
                    .Where(prop => Pcc.IsUExport(prop.Value))
                    .Select(prop => Pcc.GetUExport(prop.Value))
                    .ToHashSet() //remove duplicate exports
                    .Select(LoadObject));
                //CurrentObjects.AddRange(convertedImports.Select(LoadObject));

                // Subtrack imports. But they should be shown still
                if (CurrentObjects.Count != (seqObjs.Count - nullCount))
                {
                    MessageBox.Show(this,
                        "Sequence contains invalid or duplicate exports! Correct this by editing the SequenceObject array in the Properties editor");
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

            if (SavedView.Positions.IsEmpty() && (Pcc.Game is MEGame.ME2 or MEGame.ME3))
            {
                AutoLayout();
            }
        }

        public float StartPosEvents;
        public float StartPosActions;
        public float StartPosVars;

        public SObj LoadObject(ExportEntry export)
        {
            float x = float.NaN, y = float.NaN;
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

            if (export.IsA("SequenceEvent"))
            {
                return new SEvent(export, graphEditor);
            }
            else if (export.IsA("SequenceVariable"))
            {
                return new SVar(export, graphEditor);
            }
            else if (export.ClassName == "SequenceFrame" &&
                     (Pcc.Game == MEGame.ME1 || Pcc.Game == MEGame.UDK || Pcc.Game.IsLEGame()))
            {
                return new SFrame(export, graphEditor);
            }
            else //if (s.StartsWith("BioSeqAct_") || s.StartsWith("SeqAct_") || s.StartsWith("SFXSeqAct_") || s.StartsWith("SeqCond_") || pcc.getExport(index).ClassName == "Sequence" || pcc.getExport(index).ClassName == "SequenceReference")
            {
                return new SAction(export, graphEditor);
            }
        }

        private static bool warnedOfReload = false;

        /// <summary>
        /// Forcibly reloads the package from disk. The package loaded in this instance will no longer be shared.
        /// </summary>
        private void ForceReloadPackageWithoutSharing()
        {
            var fileOnDisk = Pcc.FilePath;
            if (fileOnDisk != null && File.Exists(fileOnDisk))
            {
                if (Pcc.IsModified)
                {
                    var warningResult = MessageBox.Show(this,
                        "The current package is modified. Reloading the package will cause you to lose all changes to this package.\n\nReload anyways?",
                        "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (warningResult != MessageBoxResult.Yes)
                        return; // Do not continue!
                }

                if (!warnedOfReload)
                {
                    var warningResult = MessageBox.Show(this,
                        "Forcibly reloading a package will drop it out of tool sharing - making changes to this package in other will not be reflected in this window, and changes to this window will not be reflected in other windows. THIS MEANS SAVING WILL OVERWRITE CHANGES FROM OTHER WINDOWS. Only continue if you know what you are doing.\n\nReload anyways?",
                        "Warning", MessageBoxButton.YesNo, MessageBoxImage.Error);
                    if (warningResult != MessageBoxResult.Yes)
                        return; // Do not continue!
                    warnedOfReload = true;
                }

                var selectedIndex = (CurrentObjects_ListBox.SelectedItem as SObj)?.Export.UIndex ?? 0;
                using var fStream = File.OpenRead(fileOnDisk);
                LoadFileFromStream(fStream, fileOnDisk, selectedIndex);
                Title += " (NOT SHARED WITH OTHER WINDOWS)";
            }
        }

        public void Layout()
        {
            var objsInNeedOfLayout = new HashSet<SObj>();
            if (CurrentObjects != null && CurrentObjects.Any())
            {
                foreach (SObj obj in CurrentObjects)
                {
                    graphEditor.addNode(obj);
                }

                List<SAction> actions = CurrentObjects.OfType<SAction>().ToList();
                List<SVar> vars = CurrentObjects.OfType<SVar>().ToList();
                List<SEvent> events = CurrentObjects.OfType<SEvent>().ToList();

                foreach (SObj obj in CurrentObjects)
                {
                    obj.CreateConnections(actions, vars, events);
                }

                foreach (SObj obj in CurrentObjects)
                {
                    if (SavedView.Positions.TryGetValue(obj.UIndex, out PointF savedInfo))
                    {
                        obj.Layout(savedInfo.X, savedInfo.Y);
                        continue;
                    }

                    if (Pcc.Game is MEGame.ME1 or MEGame.UDK || Pcc.Game.IsLEGame())
                    {
                        var props = obj.Export.GetProperties();
                        IntProperty xPos = props.GetProp<IntProperty>("ObjPosX");
                        IntProperty yPos = props.GetProp<IntProperty>("ObjPosY");
                        if (xPos is not null || yPos is not null)
                        {
                            obj.Layout(xPos?.Value ?? 0, yPos?.Value ?? 0);
                            continue;
                        }
                    }

                    objsInNeedOfLayout.Add(obj);
                    obj.Layout(0, 0);
                    //switch (obj)
                    //{
                    //    case SEvent:
                    //        obj.Layout(StartPosEvents, 0);
                    //        StartPosEvents += obj.Width + 20;
                    //        break;
                    //    case SAction:
                    //        obj.Layout(StartPosActions, 250);
                    //        StartPosActions += obj.Width + 20;
                    //        break;
                    //    case SVar:
                    //        obj.Layout(StartPosVars, 500);
                    //        StartPosVars += obj.Width + 20;
                    //        break;
                    //}
                }

                if (objsInNeedOfLayout.Any())
                {
                    AutoLayout(objsInNeedOfLayout);
                }
                else
                {
                    foreach (SeqEdEdge edge in graphEditor.edgeLayer)
                    {
                        SequenceGraphEditor.UpdateEdge(edge);
                    }
                }
            }
        }

        private void AutoLayout(ICollection<SObj> objsToLayout = null)
        {
            var visitedNodes = new HashSet<int>();

            if (objsToLayout is null)
            {
                objsToLayout = CurrentObjects;
            }
            else
            {
                visitedNodes.AddRange(CurrentObjects.Except(objsToLayout).Select(obj => obj.UIndex));
            }

            foreach (SObj obj in objsToLayout)
            {
                obj.SetOffset(0, 0); //remove existing positioning
            }

            const float HORIZONTAL_SPACING = 40;
            const float VERTICAL_SPACING = 20;
            const float VAR_SPACING = 10;
            var eventNodes = objsToLayout.OfType<SEvent>().ToList();
            SObj firstNode = eventNodes.FirstOrDefault();
            var varNodeLookup = objsToLayout.OfType<SVar>().ToDictionary(obj => obj.UIndex);
            var opNodeLookup = objsToLayout.OfType<SBox>().ToDictionary(obj => obj.UIndex);
            var rootTree = new List<SObj>();
            //SEvents are natural root nodes. ALmost everything will proceed from one of these
            foreach (SEvent eventNode in eventNodes)
            {
                LayoutTree(eventNode, 5 * VERTICAL_SPACING);
            }

            //Find SActions with no inputs. These will not have been reached from an SEvent
            var orphanRoots = objsToLayout.OfType<SAction>().Where(node => node.InputEdges.IsEmpty());
            foreach (SAction orphan in orphanRoots)
            {
                LayoutTree(orphan, VERTICAL_SPACING);
            }

            //It's possible that there are groups of otherwise unconnected SActions that form cycles.
            //Might be possible to make a better heuristic for choosing a root than sequence order, but this situation is so rare it's not worth the effort
            var cycleNodes = objsToLayout.OfType<SAction>().Where(node => !visitedNodes.Contains(node.UIndex));
            foreach (SAction cycleNode in cycleNodes)
            {
                LayoutTree(cycleNode, VERTICAL_SPACING);
            }

            //Lonely unconnected variables. Put them in a row below everything else
            var unusedVars = objsToLayout.OfType<SVar>().Where(obj => !visitedNodes.Contains(obj.UIndex));
            float varOffset = 0;
            float vertOffset = rootTree.BoundingRect().Bottom + VERTICAL_SPACING;
            foreach (SVar unusedVar in unusedVars)
            {
                unusedVar.OffsetBy(varOffset, vertOffset);
                varOffset += unusedVar.GlobalFullWidth + HORIZONTAL_SPACING;
            }

            if (firstNode != null) objsToLayout.OffsetBy(0, -firstNode.OffsetY);

            foreach (SeqEdEdge edge in graphEditor.edgeLayer)
                SequenceGraphEditor.UpdateEdge(edge);


            void LayoutTree(SBox sAction, float verticalSpacing)
            {
                firstNode ??= sAction;
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
                    float dx = varLink.Node.GlobalFullBounds.X - SVar.RADIUS;
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
                var children = root.Outlinks.SelectMany(link => link.Links)
                    .Where(uIndex => !visitedNodes.Contains(uIndex));
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

        public override void HandleUpdate(List<PackageUpdate> updates)
        {
            if (Pcc == null)
            {
                return; //nothing is loaded
            }

            IEnumerable<PackageUpdate> relevantUpdates = updates.Where(x => x.Change.Has(PackageChange.Export));
            List<int> updatedExports = relevantUpdates.Select(x => x.Index).ToList();
            if (SelectedSequence != null && updatedExports.Contains(SelectedSequence.UIndex))
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
            }
            else
            {
                if (updatedExports.Intersect(CurrentObjects.Select(obj => obj.UIndex)).Any())
                {
                    RefreshView();
                }

                foreach (var updatedExportUIndex in updatedExports)
                {
                    if (Pcc.TryGetUExport(updatedExportUIndex, out ExportEntry updatedExport) &&
                        updatedExport.IsSequence() && updatedExport != SelectedSequence)
                    {
                        LoadSequences();
                        break;
                    }
                }
            }


            if (updatedExports.Any(uIdx => Pcc.GetEntry(uIdx) is ExportEntry { IsClass: true }))
            {
                RefreshToolboxItems();
            }
        }

        private readonly Dictionary<int, PointF> customSaveData = new();
        private bool panToSelection = true;
        private IMEPackage PackageQueuedForLoad;
        private string FileQueuedForLoad;
        private ExportEntry ExportQueuedForFocusing;
        private bool AllowWindowRefocus = true;
        private static readonly Color GraphEditorBackColor = Color.FromArgb(167, 167, 167);

        private void saveView(bool toFile = true)
        {
            if (CurrentObjects.Count == 0)
                return;
            SavedView = new(new(), graphEditor.Camera.ViewBounds);
            foreach (SObj obj in CurrentObjects)
            {
                if (obj.Pickable)
                {
                    SavedView.Positions[obj.UIndex] = new PointF(obj.X + obj.Offset.X, obj.Y + obj.Offset.Y);
                }
            }

            foreach ((int key, PointF value) in customSaveData)
            {
                SavedView.Positions[key] = value;
            }

            customSaveData.Clear();

            if (toFile)
            {
                string outputFile = JsonConvert.SerializeObject(SavedView);
                if (!Directory.Exists(Path.GetDirectoryName(JSONpath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(JSONpath));
                File.WriteAllText(JSONpath, outputFile);
                SavedView.Positions.Clear();
            }
        }

        public void OpenNodeContextMenu(SObj obj)
        {
            if (FindResource("nodeContextMenu") is ContextMenu contextMenu)
            {
                // BREAK LINKS CODE
                if (contextMenu.GetChild("breakLinksMenuItem") is MenuItem breakLinksMenuItem)
                {
                    if (obj is SBox sBox && (sBox.Varlinks.Any() || sBox.Outlinks.Any() || sBox.EventLinks.Any()))
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
                                    string targetStr = null;
                                    if (Pcc.TryGetEntry(sBox.Outlinks[i].Links[j], out var target))
                                    {
                                        targetStr = target.ObjectName.Instanced;
                                    }

                                    var temp = new MenuItem
                                    {
                                        Header =
                                            $"Break link from {sBox.Outlinks[i].Desc} to {sBox.Outlinks[i].Links[j]} {targetStr}"
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

                                    string targetStr = null;
                                    if (Pcc.TryGetEntry(sBox.Varlinks[i].Links[j], out var target))
                                    {
                                        targetStr = target.ObjectName.Instanced;
                                    }

                                    var temp = new MenuItem
                                    {
                                        Header =
                                            $"Break link from {sBox.Varlinks[i].Desc} to {sBox.Varlinks[i].Links[j]} {targetStr}"
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
                                        Header =
                                            $"Break link from {sBox.EventLinks[i].Desc} to {sBox.EventLinks[i].Links[j]}"
                                    };
                                    int linkConnection = i;
                                    int linkIndex = j;
                                    temp.Click += (o, args) => { sBox.RemoveEventlink(linkConnection, linkIndex); };
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
                    else
                    {
                        breakLinksMenuItem.Visibility = Visibility.Collapsed;
                    }
                }

                // SKIP SEQ OBJECT CODE
                if (contextMenu.GetChild("skipObjMenuItem") is MenuItem skipObjMenuItem)
                {
                    if (obj is SBox sBox && sBox.Outlinks.Any())
                    {
                        // TODO: LIMIT TO SINGLE INPUT CAUSE IT DOESN'T REALLY WORK
                        // WITH MULTIPLE 
                        bool hasLinks = false;
                        skipObjMenuItem.Visibility = Visibility.Collapsed;
                        skipObjMenuItem.Items.Clear();
                        for (int i = 0; i < sBox.Outlinks.Count; i++)
                        {
                            skipObjMenuItem.Visibility = Visibility.Visible;
                            hasLinks = true;
                            var temp = new MenuItem
                            {
                                Header = $"Use {sBox.Outlinks[i].Desc} as skipped path"
                            };
                            int linkConnection = i;
                            temp.Click += (o, args) =>
                            {
                                KismetHelper.SkipSequenceElement(obj.Export, outboundLinkIdx: linkConnection);
                            };
                            skipObjMenuItem.Items.Add(temp);
                        }
                    }
                    else
                    {
                        skipObjMenuItem.Visibility = Visibility.Collapsed;
                    }
                }

                if (contextMenu.GetChild("interpViewerMenuItem") is MenuItem interpViewerMenuItem)
                {
                    string className = obj.Export.ClassName;
                    if (className == "InterpData"
                        || (className == "SeqAct_Interp" && obj is SAction action && action.Varlinks.Any() &&
                            action.Varlinks[0].Links.Any()
                            && Pcc.IsUExport(action.Varlinks[0].Links[0]) &&
                            Pcc.GetUExport(action.Varlinks[0].Links[0]).ClassName == "InterpData"))
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
                    if (obj is SAction sAction &&
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
                        (sAction.Export.ClassName.EndsWith("SeqAct_StartConversation") ||
                         sAction.Export.ClassName.EndsWith("StartAmbientConv")) &&
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

                    if (Pcc.Game.IsGame3() && obj is SVar sVar &&
                        Pcc.IsEntry(sVar.Export.GetProperty<ObjectProperty>("ObjValue")?.Value ?? 0))
                    {
                        openRefInPackEdMenuItem.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        openRefInPackEdMenuItem.Visibility = Visibility.Collapsed;
                    }
                }

                if (contextMenu.GetChild("repointIncomingReferences") is MenuItem repointIncomingReferences)
                {

                    if (obj is SVar sVar)
                    {
                        repointIncomingReferences.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        repointIncomingReferences.Visibility = Visibility.Collapsed;
                    }
                }

                if (contextMenu.GetChild("sequenceRefGotoMenuItem") is MenuItem sequenceRefGotoMenuItem)
                {

                    if (obj is SAction sAction && sAction.Export != null &&
                        (sAction.Export.ClassName is "SequenceReference" or "Sequence"))
                    {
                        sequenceRefGotoMenuItem.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        sequenceRefGotoMenuItem.Visibility = Visibility.Collapsed;
                    }
                }

                if (contextMenu.GetChild("extractSequenceMenuItem") is MenuItem extractSequenceMenuItem)
                {
#if DEBUG
                    if (obj is SAction sAction && sAction.Export != null &&
                        (sAction.Export.ClassName is "SequenceReference" or "Sequence"))
                    {
                        extractSequenceMenuItem.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        extractSequenceMenuItem.Visibility = Visibility.Collapsed;
                    }
#endif
                }

                if (contextMenu.GetChild("trimSequenceVariablesMenuItem") is MenuItem trimVariableLinksMenuItem)
                {
#if DEBUG
                    if (obj.Export != null && (obj is SAction sAction || obj is SEvent))
                    {
                        trimVariableLinksMenuItem.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        trimVariableLinksMenuItem.Visibility = Visibility.Collapsed;
                    }
#endif
                }

                if (contextMenu.GetChild("seqLogAddItemMenuItem") is MenuItem seqLogAddItemMenuItem)
                {

                    if (obj is SAction sAction && sAction.Export != null && sAction.Export.ClassName == "SeqAct_Log")
                    {
                        seqLogAddItemMenuItem.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        seqLogAddItemMenuItem.Visibility = Visibility.Collapsed;
                    }
                }

                if (contextMenu.GetChild("seqLogLogObjectMenuItem") is MenuItem seqLogLogObjectMenuItem)
                {

                    if (obj is SVar sVar && sVar.Export != null)
                    {
                        seqLogLogObjectMenuItem.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        seqLogLogObjectMenuItem.Visibility = Visibility.Collapsed;
                    }
                }

                if (contextMenu.GetChild("seqLogLogOutlinkFiringMenuItem") is MenuItem seqLogLogOutlinkFiringMenuItem)
                {
                    if (obj is SBox sAction && sAction.Export != null && sAction.Outlinks.Any())
                    {
                        seqLogLogOutlinkFiringMenuItem.Visibility = Visibility.Visible;

                        seqLogLogOutlinkFiringMenuItem.Items.Clear();
                        for (int i = 0; i < sAction.Outlinks.Count; i++)
                        {
                            int tempIdx = i; // Captured
                            var temp = new MenuItem
                            {
                                Header = $"Log when {sAction.Outlinks[i].Desc} fires"
                            };
                            temp.Click += (o, args) => { SeqLogLogOutlink(sAction, sAction.Outlinks[tempIdx].Desc); };
                            seqLogLogOutlinkFiringMenuItem.Items.Add(temp);
                        }
                    }
                    else
                    {
                        seqLogLogOutlinkFiringMenuItem.Visibility = Visibility.Collapsed;
                    }
                }

                if (contextMenu.GetChild("addSwitchOutlinksMenuItem") is MenuItem addSwitchOutlinksMenuItem)
                {
                    if (obj is SAction sAction && sAction.Export != null &&
                        sAction.Export.Class.InheritsFrom("SeqAct_Switch"))
                    {
                        addSwitchOutlinksMenuItem.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        addSwitchOutlinksMenuItem.Visibility = Visibility.Collapsed;
                    }
                }

                contextMenu.IsOpen = true;
                graphEditor.DisableDragging();
            }
        }

        private void removeAllLinks(object sender, RoutedEventArgs args)
        {
            ExportEntry export = (ExportEntry)((MenuItem)sender).Tag;
            KismetHelper.RemoveAllLinks(export);
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

        private void RemoveFromSequence_Click(object sender, RoutedEventArgs e)
        {
            RemoveFromSequence(false);
        }

        private void TrashAndRemoveFromSequence_Click(object sender, RoutedEventArgs e)
        {
            RemoveFromSequence(true);
        }

        /// <summary>
        /// Removes an object from a sequence.
        /// </summary>
        /// <param name="trash">If the object should be trashed. Most times this is desirable, however if an object is being moved to another sequence, this is not desirable.</param>
        private void RemoveFromSequence(bool trash)
        {
            if (CurrentObjects_ListBox.SelectedItem is SObj sObj)
            {
                //remove incoming connections
                switch (sObj)
                {
                    case SVar sVar:
                        foreach (VarEdge edge in sVar.Connections)
                        {
                            edge.Originator.RemoveVarlink(edge);
                        }

                        break;
                    case SAction sAction:
                        foreach (SBox.InputLink inLink in sAction.InLinks)
                        {
                            foreach (ActionEdge edge in inLink.Edges)
                            {
                                edge.Originator.RemoveOutlink(edge);
                            }
                        }

                        break;
                    case SEvent sEvent:
                        foreach (EventEdge edge in sEvent.Connections)
                        {
                            edge.Originator.RemoveEventlink(edge);
                        }

                        break;
                }

                //remove outgoing links
                KismetHelper.RemoveAllLinks(sObj.Export);

                //remove from sequence
                var seqObjs = SelectedSequence.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
                var arrayObj = seqObjs?.FirstOrDefault(x => x.Value == sObj.UIndex);
                if (arrayObj != null)
                {
                    seqObjs.Remove(arrayObj);
                    SelectedSequence.WriteProperty(seqObjs);
                }

                if (trash)
                {
                    //Trash
                    EntryPruner.TrashEntryAndDescendants(sObj.Export);
                }
            }
        }

        protected void node_MouseDown(object sender, PInputEventArgs e)
        {
            if (sender is SObj obj)
            {
                obj.PosAtDragStart = obj.GlobalFullBounds;
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
                if (e.Button != System.Windows.Forms.MouseButtons.Left && obj.GlobalFullBounds == obj.PosAtDragStart)
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

            Settings.SequenceEditor_AutoSaveViewV2 = AutoSaveView_MenuItem.IsChecked;
            Settings.SequenceEditor_ShowOutputNumbers = SObj.OutputNumbers;

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
            ResetTreeView();
            graphEditor.Dispose();
            Properties_InterpreterWPF.Dispose();
            GraphHost.Child = null; //This seems to be required to clear OnChildGotFocus handler from WinFormsHost
            GraphHost.Dispose();
            DataContext = null;
            DispatcherHelper.EmptyQueue();
            RecentsController?.Dispose();
        }

        private void OpenInPackageEditor_Clicked(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects_ListBox.SelectedItem is SObj obj)
            {
                AllowWindowRefocus =
                    false; //prevents flicker effect when windows try to focus and then package editor activates
                var p = new PackageEditor.PackageEditorWindow();
                p.Show();
                p.LoadFile(obj.Export.FileRef.FilePath, obj.UIndex);
                p.Activate(); //bring to front
            }
        }

        private void OpenReferencedObjectInPackageEditor_Clicked(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects_ListBox.SelectedItem is SVar sVar &&
                sVar.Export.GetProperty<ObjectProperty>("ObjValue") is ObjectProperty objProp)
            {
                AllowWindowRefocus =
                    false; //prevents flicker effect when windows try to focus and then package editor activates
                var p = new PackageEditor.PackageEditorWindow();
                p.Show();
                p.LoadFile(sVar.Export.FileRef.FilePath, objProp.Value);
                p.Activate(); //bring to front
            }
        }

        private void CloneInterpData_Clicked(object sender, RoutedEventArgs e)
        {
            if (SelectedObjects.HasExactly(1) && SelectedObjects[0] is SVar sVar &&
                sVar.Export.ClassName == "InterpData")
            {
                addObject(EntryCloner.CloneTree(sVar.Export));
            }
        }

        private void CloneObject_Clicked(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects_ListBox.SelectedItem is SObj obj)
            {
                ExportEntry clonedExport = KismetHelper.CloneObject(obj.Export, SelectedSequence);
                customSaveData[clonedExport.UIndex] =
                    new PointF(graphEditor.Camera.ViewCenterX, graphEditor.Camera.ViewCenterY);
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
            string objectName =
                System.Text.RegularExpressions.Regex.Replace(SelectedSequence.ObjectName.Name, @"[<>:""/\\|?*]", "");
            var d = new SaveFileDialog
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
                Image image = graphEditor.Root.ToImage();
                graphEditor.Camera.Visible = true;
                image.Save(d.FileName, ImageFormat.Png);
                graphEditor.backLayer.RemoveAllChildren();
                MessageBox.Show(this, "Done.");
            }
        }

        private void addObject(ExportEntry exportToAdd, bool removeLinks = true)
        {
            customSaveData[exportToAdd.UIndex] =
                new PointF(graphEditor.Camera.ViewCenterX, graphEditor.Camera.ViewCenterY);
            KismetHelper.AddObjectToSequence(exportToAdd, SelectedSequence, removeLinks);
        }

        private void AddObject_Clicked(object sender, RoutedEventArgs e)
        {
            if (EntrySelector.GetEntry<ExportEntry>(this, Pcc) is ExportEntry exportToAdd)
            {
                if (!exportToAdd.IsA("SequenceObject"))
                {
                    MessageBox.Show(this,
                        $"#{exportToAdd.UIndex}: {exportToAdd.ObjectName.Instanced} is not a sequence object.");
                    return;
                }

                if (CurrentObjects.Any(obj => obj.Export == exportToAdd))
                {
                    MessageBox.Show(this,
                        $"#{exportToAdd.UIndex}: {exportToAdd.ObjectName.Instanced} is already in the sequence.");
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

            if (CurrentObjects_ListBox.SelectedItem is SObj obj)
            {
                int uIndex;
                ExportEntry exportEntry = obj.Export;
                if (exportEntry.IsA("InterpData"))
                {
                    uIndex = exportEntry.UIndex;
                }
                else if (obj is SAction sAction && sAction.Varlinks.Any() && sAction.Varlinks[0].Links.Any())
                {
                    uIndex = sAction.Varlinks[0].Links[0];
                }
                else
                {
                    MessageBox.Show(this, "No InterpData to open!", "Sorry!", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                AllowWindowRefocus =
                    false; //prevents flicker effect when windows try to focus and then package editor activates

                var p = new InterpEditor.InterpEditorWindow();
                p.Show();
                p.LoadFile(Pcc.FilePath);
                p.SelectedInterpData = Pcc.GetUExport(uIndex);
            }
        }

        private void OpenInDialogueEditor_Clicked(object sender, RoutedEventArgs e)
        {

            if (CurrentObjects_ListBox.SelectedItem is SObj obj &&
                (obj.Export.ClassName.EndsWith("SeqAct_StartConversation") ||
                 obj.Export.ClassName.EndsWith("StartAmbientConv")) &&
                obj.Export.GetProperty<ObjectProperty>("Conv") is ObjectProperty conv)
            {
                if (Pcc.IsUExport(conv.Value))
                {
                    AllowWindowRefocus =
                        false; //prevents flicker effect when windows try to focus and then package editor activates
                    new DialogueEditor.DialogueEditorWindow(Pcc.GetUExport(conv.Value)).Show();
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
                        using var convFile = MEPackageHandler.OpenMEPackage(convFilePath);
                        var convExport = convFile.Exports.FirstOrDefault(x => x.ObjectName == convImport.ObjectName);
                        if (convExport != null)
                        {
                            AllowWindowRefocus =
                                false; //prevents flicker effect when windows try to focus and then package editor activates
                            new DialogueEditor.DialogueEditorWindow(convExport).Show();
                            return;
                        }
                    }
                    else if (EntryImporter.ResolveImport(convImport) is ExportEntry fauxExport)
                    {
                        using var convFile = MEPackageHandler.OpenMEPackage(fauxExport.FileRef.FilePath);
                        var convExport = convFile.GetUExport(fauxExport.UIndex);
                        if (convExport != null)
                        {
                            AllowWindowRefocus =
                                false; //prevents flicker effect when windows try to focus and then package editor activates
                            new DialogueEditor.DialogueEditorWindow(convExport).Show();
                            return;
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
            if (FileQueuedForLoad != null || PackageQueuedForLoad != null || ExportQueuedForFocusing != null)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    //Wait for all children to finish loading
                    if (FileQueuedForLoad != null)
                    {
                        LoadFile(FileQueuedForLoad);
                        FileQueuedForLoad = null;
                    }
                    else if (PackageQueuedForLoad != null)
                    {
                        LoadFile(PackageQueuedForLoad.FilePath, () => RegisterPackage(PackageQueuedForLoad));
                        PackageQueuedForLoad = null;
                    }

                    if (ExportQueuedForFocusing != null)
                    {
                        GoToExport(ExportQueuedForFocusing);
                        ExportQueuedForFocusing = null;
                    }

                    Activate();
                }));
            }
        }

        private void GoToExport(int UIndex)
        {
            if (Pcc != null)
            {
                ExportEntry exp = Pcc.GetUExport(UIndex);
                if (exp != null)
                {
                    if (!IsLoaded)
                    {
                        ExportQueuedForFocusing = exp;
                    }
                    else
                    {
                        GoToExport(exp);
                    }
                }
            }
        }

        private void GoToExport(ExportEntry expToNavigateTo, bool goIntoSequences = true)
        {
            if (!IsLoaded)
            {
                // Do not try to navigate if UI has not finished loading
                ExportQueuedForFocusing = expToNavigateTo;
                return;
            }

            if (goIntoSequences && expToNavigateTo.ClassName is "SequenceReference" or "Sequence")
            {
                if (expToNavigateTo.ClassName == "SequenceReference")
                {
                    var sequenceprop = expToNavigateTo.GetProperty<ObjectProperty>("oSequenceReference");
                    if (sequenceprop != null)
                    {
                        expToNavigateTo = Pcc?.GetUExport(sequenceprop.Value);
                    }
                    else
                    {
                        return;
                    }
                }

                SelectedItem = TreeViewRootNodes.SelectMany(node => node.FlattenTree())
                    .FirstOrDefault(node => node.UIndex == expToNavigateTo.UIndex);
                return;
            }
            else
            {
                // Find which sequence contains this object
                foreach (ExportEntry exp in SequenceExports)
                {


                    // Get the export for the sequence we will look for objects in
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

                    // Enumerate the objects in the sequence to see if what we are looking for is in this sequence
                    var seqObjs = sequence.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
                    if (seqObjs != null && seqObjs.Any(objProp => objProp.Value == expToNavigateTo.UIndex))
                    {
                        //This is our sequence
                        var nodes = TreeViewRootNodes.SelectMany(node => node.FlattenTree())
                            .ToList(); // This is to debug selection failures
                        SelectedItem = nodes.First(node => node.UIndex == sequence.UIndex);
                        CurrentObjects_ListBox.SelectedItem =
                            CurrentObjects.FirstOrDefault(x => x.Export == expToNavigateTo);
                        break;
                    }
                }
            }
        }

        private void PlotEditorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects_ListBox.SelectedItem is SAction sAction &&
                sAction.Export.ClassName == "BioSeqAct_PMExecuteTransition" &&
                sAction.Export.GetProperty<IntProperty>("m_nIndex")?.Value is int m_nIndex)
            {
                IEnumerable<string> plotFiles = new List<string>();
                int stateEventKey = m_nIndex;

                if (Pcc.Game is MEGame.ME3 or MEGame.LE3)
                {
                    plotFiles = MELoadedDLC.GetEnabledDLCFolders(Pcc.Game)
                        .OrderByDescending(dir => MELoadedDLC.GetMountPriority(dir, Pcc.Game))
                        .Select(dir => Path.Combine(dir, Pcc.Game.CookedDirName(),
                            $"Startup_{MELoadedDLC.GetDLCNameFromDir(dir)}_INT.pcc"))
                        .Append(Path.Combine(MEDirectories.GetCookedPath(Pcc.Game), "SFXGameInfoSP_SF.pcc"))
                        .Where(File.Exists);
                }

                if (Pcc.Game is MEGame.ME2 or MEGame.LE2)
                {
                    plotFiles = MELoadedDLC.GetEnabledDLCFolders(Pcc.Game)
                        .OrderByDescending(dir => MELoadedDLC.GetMountPriority(dir, Pcc.Game))
                        .Select(dir => Path.Combine(dir, Pcc.Game.CookedDirName(),
                            $"Startup_{MELoadedDLC.GetDLCNameFromDir(dir)}_INT.pcc"))
                        .Append(Path.Combine(MEDirectories.GetCookedPath(Pcc.Game), "Startup_INT.pcc"))
                        .Where(File.Exists);
                }

                if (Pcc.Game is MEGame.LE1)
                {
                    plotFiles = MELoadedDLC.GetEnabledDLCFolders(Pcc.Game)
                        .OrderByDescending(dir => MELoadedDLC.GetMountPriority(dir, Pcc.Game))
                        //.Select(dir => Path.Combine(dir, "CookedPCConsole", $"Startup_{MELoadedDLC.GetDLCNameFromDir(dir)}_INT.pcc")) // TODO: implement once ME1 DLC folders work
                        .Append(Path.Combine(MEDirectories.GetCookedPath(Pcc.Game), "BIOC_Materials.pcc"))
                        .Where(File.Exists);
                }

                if (Pcc.Game is MEGame.ME1)
                {
                    plotFiles = MELoadedDLC.GetEnabledDLCFolders(Pcc.Game)
                        .OrderByDescending(dir => MELoadedDLC.GetMountPriority(dir, Pcc.Game))
                        .Select(dir => Path.Combine(dir, Pcc.Game.CookedDirName(),
                            $@"Packages\PlotManagerAuto{MELoadedDLC.GetDLCNameFromDir(dir)}.upk"))
                        .Append(Path.Combine(MEDirectories.GetCookedPath(Pcc.Game), @"Packages\PlotManagerAuto.upk"))
                        .Where(File.Exists);
                }

                if (stateEventKey != 0 && plotFiles.Any())
                {
                    string filePath = null;
                    foreach (var plotFile in plotFiles)
                    {
                        using IMEPackage pcc = MEPackageHandler.OpenMEPackage(plotFile);
                        if (StateEventMapView.TryFindStateEventMap(pcc, out ExportEntry export))
                        {
                            var stateEventMap = BinaryBioStateEventMap.Load(export);
                            if (stateEventMap.StateEvents.ContainsKey(stateEventKey))
                            {
                                filePath = plotFile;
                            }
                        }
                    }

                    if (filePath != null)
                    {

                        var plotEd = new PlotEditorWindow();
                        plotEd.Show();
                        plotEd.LoadFile(filePath);
                        plotEd.GoToStateEvent(stateEventKey);
                    }
                    else
                    {
                        MessageBox.Show(this, $"Could not find State Event {stateEventKey}");
                    }
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
                        MessageBox.Show(
                            $"#{export.UIndex} {export.ObjectName.Instanced}  is not part of this sequence, and can't be repointed to.");
                        return;
                    }

                    var sequence =
                        sVar.Export.FileRef.GetUExport(sVar.Export.GetProperty<ObjectProperty>("ParentSequence").Value);
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
            Settings.Save();
        }

        private void IntegerUpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (CurrentObjects.Any())
            {
                RefreshView();
            }
        }

        private void EditComment_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects_ListBox.SelectedItem is SObj sObj)
            {
                var comments = sObj.Export.GetProperty<ArrayProperty<StrProperty>>("m_aObjComment") ??
                               new ArrayProperty<StrProperty>("m_aObjComment");

                string commentText = string.Join("\n", comments.Select(prop => prop.Value));

                string resultText = PromptDialog.Prompt(this, "", "Edit Comment", commentText, true,
                    inputType: PromptDialog.InputType.Multiline);

                if (resultText == null)
                {
                    return;
                }

                comments = new ArrayProperty<StrProperty>(
                    resultText.SplitLines(StringSplitOptions.RemoveEmptyEntries).Select(s => new StrProperty(s)),
                    "m_aObjComment");

                sObj.Export.WriteProperty(comments);
            }
        }

        public void PropogateRecentsChange(string propogationSource, IEnumerable<RecentsControl.RecentItem> newRecents)
        {
            RecentsController.PropogateRecentsChange(false, newRecents);
        }

        private void GotoSequenceReference_Clicked(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects_ListBox.SelectedItem is SAction sAction &&
                (sAction.Export.ClassName is "SequenceReference" or "Sequence"))
            {
                GoToExport(sAction.Export);
            }
        }

        private void AddToLogString_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects_ListBox.SelectedItem is SAction sAction &&
                sAction.Export.ClassName == "SeqAct_Log")
            {
                var result = PromptDialog.Prompt(this, "Enter the string to log", "Enter string");
                if (!string.IsNullOrWhiteSpace(result))
                {
                    var newSeqObj = LEXSequenceObjectCreator.CreateSequenceObject(Pcc, "SeqVar_String");
                    newSeqObj.WriteProperty(new StrProperty(result, "StrValue"));
                    KismetHelper.AddObjectToSequence(newSeqObj, SelectedSequence);
                    var varLinks = KismetHelper.GetVariableLinksOfNode(sAction.Export);
                    var stringVarLink = varLinks.First(x => x.LinkDesc == "String");
                    stringVarLink.LinkedNodes.Add(newSeqObj);
                    KismetHelper.WriteVariableLinksToNode(sAction.Export, varLinks);
                }
            }
        }

        private void CreateSeqLogForObject_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects_ListBox.SelectedItem is SVar sVar)
            {
                var result = PromptDialog.Prompt(this, "Enter the string to log alongside this", "Enter string");
                if (!string.IsNullOrWhiteSpace(result))
                {
                    // Create the log object and add it to the sequence
                    var seqLogObj = LEXSequenceObjectCreator.CreateSequenceObject(Pcc, "SeqAct_Log");
                    KismetHelper.AddObjectToSequence(seqLogObj, SelectedSequence);

                    // Create user string SeqVar
                    var newSeqObj = LEXSequenceObjectCreator.CreateSequenceObject(Pcc, "SeqVar_String");
                    newSeqObj.WriteProperty(new StrProperty(result, "StrValue"));
                    KismetHelper.AddObjectToSequence(newSeqObj, SelectedSequence);

                    // Attach the user string SeqVar and the selected item to the log.

                    // String
                    var varLinks = KismetHelper.GetVariableLinksOfNode(seqLogObj);
                    var stringVarLink = varLinks.First(x => x.LinkDesc == "String");
                    stringVarLink.LinkedNodes.Add(newSeqObj);


                    VarLinkInfo linkToAttachTo = null;
                    if (sVar.Export.IsA("SeqVar_String"))
                    {
                        linkToAttachTo = varLinks.First(x => x.LinkDesc == "String");
                    }
                    else if (sVar.Export.IsA("SeqVar_Float"))
                    {
                        linkToAttachTo = varLinks.First(x => x.LinkDesc == "Float");
                    }
                    else if (sVar.Export.IsA("SeqVar_Bool"))
                    {
                        linkToAttachTo = varLinks.First(x => x.LinkDesc == "Bool");
                    }
                    else if (sVar.Export.IsA("SeqVar_Object"))
                    {
                        linkToAttachTo = varLinks.First(x => x.LinkDesc == "Object");
                    }
                    else if (sVar.Export.IsA("SeqVar_Int"))
                    {
                        linkToAttachTo = varLinks.First(x => x.LinkDesc == "Int");
                    }
                    else if (sVar.Export.IsA("SeqVar_Name"))
                    {
                        linkToAttachTo = varLinks.First(x => x.LinkDesc == "Name");
                    }
                    else if (sVar.Export.IsA("SeqVar_Vector"))
                    {
                        linkToAttachTo = varLinks.First(x => x.LinkDesc == "Vector");
                    }
                    else if (sVar.Export.IsA("SeqVar_ObjectList"))
                    {
                        linkToAttachTo = varLinks.First(x => x.LinkDesc == "Obj List");
                    }
                    else if (sVar.Export.IsA("SeqVar_External"))
                    {
                        // Just use Object
                        linkToAttachTo = varLinks.First(x => x.LinkDesc == "Object");
                    }


                    if (linkToAttachTo == null)
                    {
                        Debugger.Break();
                    }
                    else
                    {
                        linkToAttachTo.LinkedNodes.Add(sVar.Export);
                    }

                    // Write the links
                    KismetHelper.WriteVariableLinksToNode(seqLogObj, varLinks);
                }
            }
        }

        private void SeqLogLogOutlink(SBox sourceAction, string outLinkName)
        {
            var result = PromptDialog.Prompt(this,
                $"Enter the string to log when the outlink '{outLinkName}' is fired.", "Enter string",
                $"Outlink {outLinkName} fired from {sourceAction.Export.UIndex} {sourceAction.Export.ObjectName.Instanced}",
                true);
            if (!string.IsNullOrWhiteSpace(result))
            {
                // Create the log object and add it to the sequence
                var seqLogObj = LEXSequenceObjectCreator.CreateSequenceObject(Pcc, "SeqAct_Log");
                KismetHelper.AddObjectToSequence(seqLogObj, SelectedSequence);

                // Create user string SeqVar
                var newSeqObj = LEXSequenceObjectCreator.CreateSequenceObject(Pcc, "SeqVar_String");
                newSeqObj.WriteProperty(new StrProperty(result, "StrValue"));
                KismetHelper.AddObjectToSequence(newSeqObj, SelectedSequence);

                // Attach the user string SeqVar and the selected item to the log.
                KismetHelper.CreateVariableLink(seqLogObj, "String", newSeqObj);

                // Add an outlink to the new object
                KismetHelper.CreateOutputLink(sourceAction.Export, outLinkName, seqLogObj);
            }
        }

        private void OpenClassDefinitionInPackageEditor_Clicked(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects_ListBox.SelectedItem is SObj obj && obj.Export != null)
            {
                // Get class of the object
                var objClass = obj.Export.Class;
                string className = objClass.ClassName;
                if (objClass is ImportEntry imp)
                {
                    objClass = EntryImporter.ResolveImport(imp);
                }

                if (objClass != null)
                {
                    AllowWindowRefocus =
                        false; //prevents flicker effect when windows try to focus and then package editor activates
                    var p = new PackageEditor.PackageEditorWindow();
                    p.Show();
                    p.LoadFile(objClass.FileRef.FilePath, objClass.UIndex);
                    p.Activate(); //bring to front
                }
                else
                {
                    MessageBox.Show($"Could not determine where class '{className}' is defined.",
                        "Cannot locate class");
                }
            }
        }

        private void OpenOtherVersion()
        {
            var result = CrossGenHelpers.FetchOppositeGenPackage(Pcc, out var otherGen);
            if (result != null)
            {
                MessageBox.Show(result);
            }
            else
            {
                var nodeEntry = SelectedObjects.FirstOrDefault();
                SequenceEditorWPF seqEd = new SequenceEditorWPF(otherGen);
                if (nodeEntry != null && nodeEntry.Export != null)
                {
                    seqEd.ExportQueuedForFocusing = otherGen.FindExport(nodeEntry.Export.InstancedFullPath);
                }

                seqEd.Show();
            }
        }


        private void LoadCustomClasses_Clicked(object sender, RoutedEventArgs e)
        {
            SequenceEditorExperimentsM.LoadCustomClassesFromFile(this);
        }

        private void LoadCustomClassesFromCurentPackage_Clicked(object sender, RoutedEventArgs e)
        {
            SequenceEditorExperimentsM.LoadCustomClassesFromCurrentPackage(this);
        }

        private void CommitObjectPositions_Clicked(object sender, RoutedEventArgs e)
        {
            SequenceEditorExperimentsM.CommitSequenceObjectPositions(this);
        }

        private void UpdateSelVarLinks_Clicked(object sender, RoutedEventArgs e)
        {
            SequenceEditorExperimentsE.UpdateSequenceVarLinks(GetSEWindow(), true);
        }

        private void UpdateSequenceVarLinks_Clicked(object sender, RoutedEventArgs e)
        {
            SequenceEditorExperimentsE.UpdateSequenceVarLinks(GetSEWindow());
        }

        private void AddDialogueWheelCam_Clicked(object sender, RoutedEventArgs e)
        {
            SequenceEditorExperimentsE.AddDialogueWheelTemplate(GetSEWindow());
        }

        private void AddDialogueWheelDir_Clicked(object sender, RoutedEventArgs e)
        {
            SequenceEditorExperimentsE.AddDialogueWheelTemplate(GetSEWindow(), true);
        }

        private void AddAnchorToInterps_Clicked(object sender, RoutedEventArgs e)
        {
            SequenceEditorExperimentsK.UpdateAllInterpAnchorsVarLinks(GetSEWindow());
        }

        public SequenceEditorWPF GetSEWindow()
        {
            if (GetWindow(this) is SequenceEditorWPF sew)
            {
                return sew;
            }

            return null;
        }

        private void ImportSequenceFromAnotherPackage_Clicked(object sender, RoutedEventArgs e)
        {
            SequenceEditorExperimentsM.InstallSequencePrefab(GetSEWindow());
        }

        private void CopyInstancedFullPath_Clicked(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects_ListBox.SelectedItem is SObj obj)
            {
                Clipboard.SetText(obj.Export.InstancedFullPath);
            }
        }

        private void ExtractSequence_Clicked(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects_ListBox.SelectedItem is SAction sAction &&
                (sAction.Export.ClassName is "SequenceReference" or "Sequence"))
            {
                var seqExp = sAction.Export;

                // We're going to have to modify the package to get this to work, unfortunately...

                // Remove object reference
                var props = seqExp.GetProperties();
                seqExp.RemoveProperty("ParentSequence");
                KismetHelper.RemoveAllLinks(seqExp);
                var originalIdxLink = seqExp.idxLink;

                // Set to root
                seqExp.idxLink = 0;

                SharedPackageTools.ExtractEntryToNewPackage(seqExp, x =>
                {
                    if (x)
                    {
                        SetBusy();
                    }
                    else
                    {
                        // Restore
                        seqExp.WriteProperties(props);
                        seqExp.idxLink = originalIdxLink;
                        EndBusy();
                    }
                }, x => BusyText = x, entryDoubleClick, this);

            }
        }

        private void TrimVariableLinks_Clicked(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects_ListBox.SelectedItem is SObj sAction && sAction.Export != null)
            {
                KismetHelper.TrimVariableLinks(sAction.Export);
            }
        }

        public string Toolname => "SequenceEditor";

        private void AddSwitchOutlinksMenuItem_Clicked(object sender, RoutedEventArgs e)
        {
            if (CurrentObjects_ListBox.SelectedItem is SObj sAction && sAction.Export != null)
            {
                var result = PromptDialog.Prompt(this, "How many outlinks would you like to add?",
                    "Add switch outlinks", "1", true);
                if (int.TryParse(result, out var howManyToAdd) && howManyToAdd > 0)
                {


                    var sw = sAction.Export;
                    var currentIdx = KismetHelper.GetOutputLinksOfNode(sw).Count;
                    for (int i = 0; i < howManyToAdd; i++)
                    {
                        KismetHelper.CreateNewOutputLink(sw, $"Link {++currentIdx}", null);
                    }

                    sw.WriteProperty(new IntProperty(currentIdx, "LinkCount"));
                }
            }
        }
    }

    static class SequenceEditorExtensions
        {
            public static bool IsSequence(this IEntry entry) => entry.IsA("Sequence");
        }
    }