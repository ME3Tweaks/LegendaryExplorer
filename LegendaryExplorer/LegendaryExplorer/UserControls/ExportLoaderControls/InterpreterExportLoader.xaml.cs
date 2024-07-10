using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Be.Windows.Forms;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.PlotDatabase;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.UnrealScript;
using static LegendaryExplorer.UserControls.ExportLoaderControls.EntryMetadataExportLoader;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for InterpreterExportLoader.xaml
    /// </summary>
    public partial class InterpreterExportLoader : ExportLoaderControl
    {
        public ObservableCollectionExtended<UPropertyTreeViewEntry> PropertyNodes { get; } = [];
        //Values in this list will cause the ExportToString() method to be called on an objectproperty in InterpreterExportLoader.
        //This is useful for end user when they want to view things in a list for example, but all of the items are of the 
        //same type and are not distinguishable without changing to another export, wasting a lot of time.
        //values are the class of object value being parsed
        public static readonly string[] ExportToStringConverters = ["LevelStreamingKismet", "StaticMeshComponent", "ParticleSystemComponent", "DecalComponent", "LensFlareComponent", "AnimNodeSequence", "BioAnimNodeSequence"];
        public static readonly string[] IntToStringConverters = [ "WwiseEvent", "WwiseBank", "WwiseStream", "BioSeqAct_PMExecuteTransition", "BioSeqAct_PMExecuteConsequence", "BioSeqAct_PMCheckState", "BioSeqAct_PMCheckConditional", "BioSeqVar_StoryManagerInt",
                                                                "BioSeqVar_StoryManagerFloat", "BioSeqVar_StoryManagerBool", "BioSeqVar_StoryManagerStateId", "SFXSceneShopNodePlotCheck", "BioWorldInfo", "CoverLink" ];
        public ObservableCollectionExtended<IndexedName> ParentNameList { get; private set; }

        public bool SubstituteImageForHexBox
        {
            get => (bool)GetValue(SubstituteImageForHexBoxProperty);
            set => SetValue(SubstituteImageForHexBoxProperty, value);
        }
        public static readonly DependencyProperty SubstituteImageForHexBoxProperty = DependencyProperty.Register(
            nameof(SubstituteImageForHexBox), typeof(bool), typeof(InterpreterExportLoader), new PropertyMetadata(false, SubstituteImageForHexBoxChangedCallback));

        /// <summary>
        /// Use only for binding to prevent null bindings
        /// </summary>
        public GenericCommand NavigateToEntryCommandInternal { get; set; }

        public GenericCommand CopyPropertyCommand { get; set; }

        public GenericCommand PastePropertyCommand { get; set; }

        public RelayCommand NavigateToEntryCommand
        {
            get => (RelayCommand)GetValue(NavigateToEntryCallbackProperty);
            set => SetValue(NavigateToEntryCallbackProperty, value);
        }

        public static readonly DependencyProperty NavigateToEntryCallbackProperty = DependencyProperty.Register(
            nameof(NavigateToEntryCommand), typeof(RelayCommand), typeof(InterpreterExportLoader), new PropertyMetadata(null));

        public bool HideHexBox
        {
            get => (bool)GetValue(HideHexBoxProperty);
            set => SetValue(HideHexBoxProperty, value);
        }
        public static readonly DependencyProperty HideHexBoxProperty = DependencyProperty.Register(
            nameof(HideHexBox), typeof(bool), typeof(InterpreterExportLoader), new PropertyMetadata(false, DisableHexBoxChangedCallback));

        public bool ForceSimpleMode
        {
            get => (bool)GetValue(ForceSimpleModeProperty);
            set => SetValue(ForceSimpleModeProperty, value);
        }
        public static readonly DependencyProperty ForceSimpleModeProperty = DependencyProperty.Register(
            nameof(ForceSimpleMode), typeof(bool), typeof(InterpreterExportLoader), new PropertyMetadata(false, ForceSimpleModeChangedCallback));

        public int HexBoxMinWidth
        {
            get => (int)GetValue(HexBoxMinWidthProperty);
            set => SetValue(HexBoxMinWidthProperty, value);
        }
        public static readonly DependencyProperty HexBoxMinWidthProperty = DependencyProperty.Register(
            nameof(HexBoxMinWidth), typeof(int), typeof(InterpreterExportLoader), new PropertyMetadata(default(int)));

        public int HexBoxMaxWidth
        {
            get => (int)GetValue(HexBoxMaxWidthProperty);
            set => SetValue(HexBoxMaxWidthProperty, value);
        }
        public static readonly DependencyProperty HexBoxMaxWidthProperty = DependencyProperty.Register(
            nameof(HexBoxMaxWidth), typeof(int), typeof(InterpreterExportLoader), new PropertyMetadata(default(int)));

        public bool AdvancedView => !ForceSimpleMode && Settings.Interpreter_AdvancedDisplay;

        public bool ShowPropOffsets => !HideHexBox && AdvancedView;

        private bool _hasUnsavedChanges;
        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set
            {
                _hasUnsavedChanges = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// The current list of loaded properties that back the property tree.
        /// This is used so we can do direct object reference comparisons for things like removal
        /// </summary>
        private PropertyCollection CurrentLoadedProperties;
        //Values in this list will cause custom code to be fired to modify what the displayed string is for IntProperties
        //when the class matches.

        int RescanSelectionOffset;
        private readonly List<FrameworkElement> EditorSetElements = [];

        private HexBox Interpreter_Hexbox;
        private bool isLoadingNewData;
        private int ForcedRescanOffset;
        private bool ArrayElementJustAdded;

        /// <summary>
        /// Reference to the package that the property we copied from is from
        /// </summary>
        private WeakReference<IMEPackage> CopiedPropertyPackage { get; } = new(null); // Default to null but ensure weak reference is generated.

        /// <summary>
        /// The currently copied property.
        /// </summary>
        private static Property CopiedProperty { get; set; }

        public InterpreterExportLoader() : base("Properties")
        {
            LoadCommands();
            InitializeComponent();
            Settings.StaticPropertyChanged += SettingChanged;
            EditorSetElements.Add(Value_TextBox); //str, strref, int, float, obj
            EditorSetElements.Add(Value_ObjectComboBox); // Object selector
            EditorSetElements.Add(Value_ComboBox); //bool, name
            EditorSetElements.Add(NameIndexPrefix_TextBlock); //nameindex
            EditorSetElements.Add(NameIndex_TextBox); //nameindex
            EditorSetElements.Add(ParsedValue_TextBlock);
            Set_Button.Visibility = Visibility.Collapsed;

            //EditorSet_Separator.Visibility = Visibility.Collapsed;
        }

        void SettingChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.Interpreter_AdvancedDisplay))
            {
                OnPropertyChanged(nameof(AdvancedView));
                OnPropertyChanged(nameof(ShowPropOffsets));
            }
        }

        private static void ForceSimpleModeChangedCallback(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            var i = (InterpreterExportLoader)obj;
            i.OnPropertyChanged(nameof(AdvancedView));
            i.OnPropertyChanged(nameof(ShowPropOffsets));
        }

        private static void DisableHexBoxChangedCallback(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            var i = (InterpreterExportLoader)obj;
            if ((bool)e.NewValue)
            {
                i.hexBoxContainer.Visibility = i.HexProps_GridSplitter.Visibility = i.ToggleHexbox_Button.Visibility = i.SaveHexChange_Button.Visibility = i.HexInfoStatusBar.Visibility = Visibility.Collapsed;
                i.HexboxColumn_GridSplitter_ColumnDefinition.Width = new GridLength(0);
                i.HexboxColumnDefinition.MinWidth = 0;
                i.HexboxColumnDefinition.MaxWidth = 0;
                i.HexboxColumnDefinition.Width = new GridLength(0);
            }
            else
            {
                i.hexBoxContainer.Visibility = i.HexProps_GridSplitter.Visibility = i.ToggleHexbox_Button.Visibility = i.SaveHexChange_Button.Visibility = i.HexInfoStatusBar.Visibility = Visibility.Visible;
                i.HexboxColumnDefinition.Width = new GridLength(i.HexBoxMinWidth);
                i.HexboxColumn_GridSplitter_ColumnDefinition.Width = new GridLength(1);
                i.HexboxColumnDefinition.bind(ColumnDefinition.MinWidthProperty, i, nameof(HexBoxMinWidth));
                i.HexboxColumnDefinition.bind(ColumnDefinition.MaxWidthProperty, i, nameof(HexBoxMaxWidth));
            }
            i.OnPropertyChanged(nameof(ShowPropOffsets));
        }

        private static void SubstituteImageForHexBoxChangedCallback(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            InterpreterExportLoader i = (InterpreterExportLoader)obj;
            if (e.NewValue is true && i.Interpreter_Hexbox_Host.Child.Height > 0 && i.Interpreter_Hexbox_Host.Child.Width > 0)
            {
                i.hexboxImageSub.Source = i.Interpreter_Hexbox_Host.Child.DrawToBitmapSource();
                i.hexboxImageSub.Width = i.Interpreter_Hexbox_Host.ActualWidth;
                i.hexboxImageSub.Height = i.Interpreter_Hexbox_Host.ActualHeight;
                i.hexboxImageSub.Visibility = Visibility.Visible;
                i.Interpreter_Hexbox_Host.Visibility = Visibility.Collapsed;
            }
            else
            {
                i.Interpreter_Hexbox_Host.Visibility = Visibility.Visible;
                i.hexboxImageSub.Visibility = Visibility.Collapsed;
            }
        }

        public UPropertyTreeViewEntry SelectedItem { get; set; }

        #region Commands
        public ICommand RemovePropertyCommand { get; set; }
        public ICommand AddPropertyCommand { get; set; }
        public ICommand AddPropertiesToStructCommand { get; set; }
        public ICommand CollapseChildrenCommand { get; set; }
        public ICommand ExpandChildrenCommand { get; set; }
        public ICommand SortChildrenCommand { get; set; }
        public ICommand SortParsedArrayAscendingCommand { get; set; } //obj, name only
        public ICommand SortParsedArrayDescendingCommand { get; set; } //obj, name only
        public ICommand SortValueArrayAscendingCommand { get; set; }
        public ICommand SortValueArrayDescendingCommand { get; set; }
        public ICommand PopoutInterpreterForObjectValueCommand { get; set; }
        public ICommand MoveArrayElementUpCommand { get; set; }
        public ICommand MoveArrayElementDownCommand { get; set; }
        public ICommand SaveHexChangesCommand { get; set; }
        public ICommand ToggleHexBoxCommand { get; set; }
        public ICommand AddArrayElementCommand { get; set; }
        public ICommand RemoveArrayElementCommand { get; set; }
        public ICommand ClearArrayCommand { get; set; }
        public ICommand CopyValueCommand { get; set; }
        public ICommand CopyPropNameCommand { get; set; }
        public ICommand CopyUnrealScriptPropValueCommand { get; set; }
        public ICommand GenerateGUIDCommand { get; set; }
        public ICommand OpenInPackageEditorCommand { get; set; }
        public ICommand OpenInMeshplorerCommand { get; set; }
        public ICommand AttemptOpenImportDefinitionCommand { get; set; }
        private void LoadCommands()
        {
            AddPropertiesToStructCommand = new GenericCommand(AddPropertiesToStruct, CanAddPropertiesToStruct);
            RemovePropertyCommand = new GenericCommand(RemoveProperty, CanRemoveProperty);
            AddPropertyCommand = new GenericCommand(AddProperty, CanAddProperty);
            CollapseChildrenCommand = new GenericCommand(CollapseChildren, CanExpandOrCollapseChildren);
            ExpandChildrenCommand = new GenericCommand(ExpandChildren, CanExpandOrCollapseChildren);
            SortChildrenCommand = new GenericCommand(SortChildren, CanSortChildren);

            SortParsedArrayAscendingCommand = new GenericCommand(SortParsedArrayAscending, CanSortArrayPropByParsedValue);
            SortParsedArrayDescendingCommand = new GenericCommand(SortParsedArrayDescending, CanSortArrayPropByParsedValue);
            SortValueArrayAscendingCommand = new GenericCommand(SortValueArrayAscending, CanSortArrayPropByValue);
            SortValueArrayDescendingCommand = new GenericCommand(SortValueArrayDescending, CanSortArrayPropByValue);
            ClearArrayCommand = new GenericCommand(ClearArray, CanClearArray);
            PopoutInterpreterForObjectValueCommand = new GenericCommand(PopoutInterpreterForObj, ObjectPropertyExportIsSelected);
            OpenInMeshplorerCommand = new GenericCommand(OpenReferenceInMeshplorer, CanOpenInMeshplorer);

            SaveHexChangesCommand = new GenericCommand(Interpreter_SaveHexChanges, IsExportLoaded);
            ToggleHexBoxCommand = new GenericCommand(ToggleHexbox);
            AddArrayElementCommand = new GenericCommand(AddArrayElement, CanAddArrayElement);
            RemoveArrayElementCommand = new GenericCommand(RemoveArrayElement, ArrayElementIsSelected);
            MoveArrayElementUpCommand = new GenericCommand(MoveArrayElementUp, CanMoveArrayElementUp);
            MoveArrayElementDownCommand = new GenericCommand(MoveArrayElementDown, CanMoveArrayElementDown);
            GenerateGUIDCommand = new GenericCommand(GenerateNewGUID, IsItemGUIDImmutable);
            NavigateToEntryCommandInternal = new GenericCommand(FireNavigateCallback, CanFireNavigateCallback);
            OpenInPackageEditorCommand = new GenericCommand(OpenInPackageEditor, ObjectPropertyExportIsSelected);
            AttemptOpenImportDefinitionCommand = new GenericCommand(AttemptOpenImport, ObjectPropertyImportIsSelected);

            CopyValueCommand = new GenericCommand(CopyPropertyValue, CanCopyPropertyValue);
            CopyPropNameCommand = new GenericCommand(CopyPropertyName, CanCopyPropertyName);
            CopyUnrealScriptPropValueCommand = new GenericCommand(CopyUnrealScriptPropValue, CanCopyUnrealScriptPropValue);

            CopyPropertyCommand = new GenericCommand(CopyProperty, CanCopyProperty);
            PastePropertyCommand = new GenericCommand(PasteProperty, CanPasteProperty);
        }

        private void CopyProperty()
        {
            if (Interpreter_TreeView?.SelectedItem is UPropertyTreeViewEntry tvi && tvi.Parent != null && tvi.Parent.Property == null && tvi.Property is not NoneProperty)
            {
                CopiedProperty = tvi.Property;
                CopiedPropertyPackage.SetTarget(CurrentLoadedExport.FileRef);
            }
        }

        private void PasteProperty()
        {
            if (!CopiedPropertyPackage.TryGetTarget(out var package)) return;
            if (CopiedProperty == null) return;
            if (CurrentLoadedExport == null) return;
            if (package != CurrentLoadedExport.FileRef) return;

            // Check existing prop name
            var existingProp = CurrentLoadedExport.GetProperties().FirstOrDefault(x => x.Name.Instanced == CopiedProperty.Name.Instanced);

            if (existingProp != null)
            {
                var overwrite = MessageBox.Show(Window.GetWindow(this), $"This export already has a property named {existingProp.Name}. Do you want to overwrite it?", "Ovewrite warning", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
                if (!overwrite) return; // Abort
            }

            // Write the property.
            CurrentLoadedExport.WriteProperty(CopiedProperty);
        }

        private bool CanPasteProperty()
        {
            return CurrentLoadedExport != null && CopiedPropertyPackage != null
                                               && CopiedPropertyPackage.TryGetTarget(out var package)
                                               && package == CurrentLoadedExport.FileRef
                                               && CurrentLoadedExport.ClassName != @"Function"
                                               && CurrentLoadedExport.ClassName != @"Class"; // We should probably make it so you can only do it if we know it supports that property, but this is a POC
        }

        private bool CanCopyProperty()
        {
            if (CurrentLoadedExport != null && Interpreter_TreeView?.SelectedItem is UPropertyTreeViewEntry tvi)
            {
                if (tvi.Property is NoneProperty) return false; // You cannot copy NoneProperties
                return tvi.Parent != null && tvi.Parent.Property == null; // Parent has no property, which means this is a root node
            }
            return false;
        }

        private void CopyUnrealScriptPropValue()
        {
            try
            {
                if (Interpreter_TreeView?.SelectedItem is UPropertyTreeViewEntry { Property: ArrayPropertyBase prop })
                {
                    var lib = new FileLib(Pcc);
                    lib.Initialize();//not going to check for failure since we're just decompiling
                    string value = UnrealScriptCompiler.GetPropertyLiteralValue(prop, CurrentLoadedExport, lib);
                    Clipboard.SetText(value);
                }
                //clear that chonky FileLib out of memory
                MemoryAnalyzer.ForceFullGC(true);
            }
            catch
            {
                // sometimes errors occur on copy when clipboard is locked. Dont do anything
            }
        }

        private bool CanCopyUnrealScriptPropValue() => Interpreter_TreeView?.SelectedItem is UPropertyTreeViewEntry { Property: ArrayPropertyBase };

        private void CopyPropertyValue()
        {
            try
            {
                if (Interpreter_TreeView?.SelectedItem is UPropertyTreeViewEntry tvi && !string.IsNullOrWhiteSpace(tvi.ParsedValue))
                {
                    Clipboard.SetText(tvi.ParsedValue);
                }
            }
            catch
            {
                // sometimes errors occur on copy when clipboard is locked. Dont do anything
            }
        }

        private bool CanCopyPropertyValue()
        {
            if (Interpreter_TreeView?.SelectedItem is UPropertyTreeViewEntry tvi && !string.IsNullOrWhiteSpace(tvi.ParsedValue))
            {
                return true;
            }

            return false;
        }

        private void CopyPropertyName()
        {
            try
            {
                if (Interpreter_TreeView?.SelectedItem is UPropertyTreeViewEntry tvi)
                {
                    Clipboard.SetText(tvi.Property.Name.Instanced);
                }
            }
            catch
            {
                // sometimes errors occur on copy when clipboard is locked. Dont do anything
            }
        }

        private bool CanCopyPropertyName() => Interpreter_TreeView?.SelectedItem is UPropertyTreeViewEntry;

        private void AttemptOpenImport()
        {
            if (CurrentLoadedExport != null && SelectedItem?.Property is ObjectProperty op && CurrentLoadedExport.FileRef.IsImport(op.Value))
            {
                var export = EntryImporter.ResolveImport(CurrentLoadedExport.FileRef.GetImport(op.Value));
                if (export != null)
                {
                    var p = new PackageEditorWindow();
                    p.Show();
                    p.LoadEntry(export);
                    p.Activate(); //bring to front        
                }
                else
                {
                    MessageBox.Show("Could not find source of this import. Make sure files are properly named.");
                }
            }
        }

        private void OpenReferenceInMeshplorer()
        {
            if (SelectedItem.Property is ObjectProperty op)
            {
                var p = new Tools.Meshplorer.MeshplorerWindow(CurrentLoadedExport.FileRef.GetUExport(op.Value));
                p.Show();
                p.Activate(); //bring to front
            }
        }

        private bool CanOpenInMeshplorer()
        {
            if (CurrentLoadedExport != null && SelectedItem?.Property is ObjectProperty op && CurrentLoadedExport.FileRef.IsUExport(op.Value))
            {
                var entry = CurrentLoadedExport.FileRef.GetUExport(op.Value);
                return MeshRenderer.CanParseStatic(entry);
            }

            return false;
        }

        private void OpenInPackageEditor()
        {
            if (SelectedItem.Property is ObjectProperty op)
            {
                var p = new PackageEditorWindow();
                p.Show();
                p.LoadFile(CurrentLoadedExport.FileRef.FilePath, op.Value);
                p.Activate(); //bring to front
            }
        }

        private void FireNavigateCallback()
        {
            var objProp = (ObjectProperty)SelectedItem.Property;
            var entry = CurrentLoadedExport.FileRef.GetEntry(objProp.Value);
            NavigateToEntryCommand?.Execute(entry);
        }

        private bool CanFireNavigateCallback()
        {
            if (CurrentLoadedExport != null && NavigateToEntryCommand != null && SelectedItem is { Property: ObjectProperty op })
            {
                var entry = CurrentLoadedExport.FileRef.GetEntry(op.Value);
                return NavigateToEntryCommand.CanExecute(entry);
            }

            return false;
        }

        private bool CanAddArrayElement()
        {
            return SelectedItem != null && !SelectedItem.HasTooManyChildrenToDisplay
                && (ArrayPropertyIsSelected() || ArrayElementIsSelected());
        }

        private void ClearArray()
        {
            if (SelectedItem?.Property != null && !SelectedItem.HasTooManyChildrenToDisplay)
            {
                var araryProperty = (ArrayPropertyBase)SelectedItem.Property;
                araryProperty.Clear();
                CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);
            }
        }

        private bool CanClearArray() => SelectedItem?.Property is ArrayPropertyBase && !SelectedItem.HasTooManyChildrenToDisplay;

        private bool ArrayPropertyIsSelected() => SelectedItem?.Property is ArrayPropertyBase;

        private bool IsExportLoaded() => CurrentLoadedExport != null;
        private bool IsItemGUIDImmutable() => SelectedItem?.Property is StructProperty { IsImmutable: true, StructType: "Guid" };
        private void GenerateNewGUID()
        {
            CurrentLoadedExport.WriteProperty(CommonStructs.GuidProp(Guid.NewGuid(), SelectedItem.Property.Name));
        }

        private bool ArrayElementIsSelected() => SelectedItem?.Parent?.Property is ArrayPropertyBase;

        private bool CanMoveArrayElementUp() => ArrayElementIsSelected() && SelectedItem.Parent.ChildrenProperties.IndexOf(SelectedItem) > 0;

        private bool CanMoveArrayElementDown()
        {
            var entries = SelectedItem?.Parent?.ChildrenProperties;
            return entries != null && ArrayElementIsSelected() && entries.IndexOf(SelectedItem) < entries.Count - 1;
        }

        private void MoveArrayElementDown() => MoveArrayElement(false);

        private void MoveArrayElementUp() => MoveArrayElement(true);

        private void PopoutInterpreterForObj()
        {
            if (SelectedItem is UPropertyTreeViewEntry { Property: ObjectProperty op } && Pcc.IsUExport(op.Value))
            {
                ExportEntry export = Pcc.GetUExport(op.Value);
                var elhw = new ExportLoaderHostedWindow(new InterpreterExportLoader(), export)
                {
                    Title = $"Properties - {export.UIndex} {export.InstancedFullPath} - {Pcc.FilePath}"
                };
                elhw.Show();
            }
        }

        private bool ObjectPropertyExportIsSelected() => Pcc is not null && SelectedItem?.Property is ObjectProperty op && Pcc.IsUExport(op.Value);
        private bool ObjectPropertyImportIsSelected() => Pcc is not null && SelectedItem?.Property is ObjectProperty op && Pcc.IsImport(op.Value);

        private void SortParsedArrayAscending()
        {
            if (SelectedItem?.Property != null)
            {
                SortArrayPropertyParsed(SelectedItem.Property, true);
            }
        }

        private void SortValueArrayAscending()
        {
            if (SelectedItem?.Property != null)
            {
                SortArrayPropertyValue(SelectedItem.Property, true);
            }
        }

        private void SortValueArrayDescending()
        {
            if (SelectedItem?.Property != null)
            {
                SortArrayPropertyValue(SelectedItem.Property, false);
            }
        }

        private void SortParsedArrayDescending()
        {
            if (SelectedItem?.Property != null)
            {
                SortArrayPropertyParsed(SelectedItem.Property, false);
            }
        }

        private void SortArrayPropertyValue(Property property, bool ascending)
        {
            switch (property)
            {
                case ArrayProperty<ObjectProperty> aop:
                    aop.Values = ascending ? aop.OrderBy(x => x.Value).ToList() : aop.OrderByDescending(x => x.Value).ToList();
                    break;
                case ArrayProperty<IntProperty> aip:
                    aip.Values = ascending ? aip.OrderBy(x => x.Value).ToList() : aip.OrderByDescending(x => x.Value).ToList();
                    break;
                case ArrayProperty<FloatProperty> afp:
                    afp.Values = ascending ? afp.OrderBy(x => x.Value).ToList() : afp.OrderByDescending(x => x.Value).ToList();
                    break;
            }
            CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);
        }

        private void SortArrayPropertyParsed(Property property, bool ascending)
        {
            switch (property)
            {
                case ArrayProperty<ObjectProperty> aop:

                    string FullPathKeySelector(ObjectProperty x) => Pcc.GetEntry(x.Value)?.InstancedFullPath ?? "";

                    aop.Values = ascending
                        ? aop.OrderBy(FullPathKeySelector).ToList()
                        : aop.OrderByDescending(FullPathKeySelector).ToList();
                    break;
                case ArrayProperty<NameProperty> anp:
                    anp.Values = (ascending ? anp.OrderBy(x => x.Value.Instanced) : anp.OrderByDescending(x => x.Value.Instanced)).ToList();
                    break;
            }
            CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);
        }

        internal void SetHexboxSelectedOffset(int v)
        {
            if (Interpreter_Hexbox != null)
            {
                Interpreter_Hexbox.SelectionStart = v;
                Interpreter_Hexbox.SelectionLength = 1;
            }
        }

        private bool CanSortArrayPropByParsedValue()
        {
            return SelectedItem != null && !SelectedItem.HasTooManyChildrenToDisplay && (SelectedItem.Property is ArrayProperty<NameProperty> ||
                   SelectedItem.Property is ArrayProperty<ObjectProperty>);
        }

        private bool CanSortArrayPropByValue()
        {
            return SelectedItem != null && !SelectedItem.HasTooManyChildrenToDisplay &&
                   (SelectedItem.Property is ArrayProperty<NameProperty> ||
                   SelectedItem.Property is ArrayProperty<ObjectProperty> ||
                   SelectedItem.Property is ArrayProperty<IntProperty> ||
                   SelectedItem.Property is ArrayProperty<FloatProperty>);
        }

        private void SortChildren() => SelectedItem?.ChildrenProperties.Sort(x => x.Property.Name.Name);

        private void ExpandChildren()
        {
            if (SelectedItem is UPropertyTreeViewEntry tvi)
            {
                SetChildrenExpandedStateRecursive(tvi, true);
            }
        }

        private static void SetChildrenExpandedStateRecursive(UPropertyTreeViewEntry root, bool IsExpanded)
        {
            root.IsExpanded = IsExpanded;
            foreach (UPropertyTreeViewEntry tvi in root.ChildrenProperties)
            {
                SetChildrenExpandedStateRecursive(tvi, IsExpanded);
            }
        }

        private bool CanExpandOrCollapseChildren() => SelectedItem is UPropertyTreeViewEntry tvi && tvi.ChildrenProperties.Count > 0;
        private bool CanSortChildren() => SelectedItem is UPropertyTreeViewEntry { HasTooManyChildrenToDisplay: false } tvi && tvi.ChildrenProperties.Count > 0;

        private void CollapseChildren()
        {
            if (SelectedItem != null)
            {
                SetChildrenExpandedStateRecursive(SelectedItem, false);
            }
        }

        private bool CanAddProperty()
        {
            if (CurrentLoadedExport == null)
            {
                return false;
            }
            if (CurrentLoadedExport.ClassName == "Class")
            {
                return false; //you can't add properties to class objects.
                              //we might want to see if the export has a NoneProperty - if it doesn't, adding properties won't work either.
                              //TODO
            }

            return true;
        }

        private void AddProperty()
        {
            var props = new List<PropNameStaticArrayIdxPair>();
            foreach (Property cProp in CurrentLoadedProperties)
            {
                //build a list we are going to the add dialog
                props.Add(new(cProp.Name, cProp.StaticArrayIndex));
            }

            if (AddPropertyDialog.GetProperty(CurrentLoadedExport, props, Pcc.Game, Window.GetWindow(this)) is (NameReference propName, int staticArrayIndex, PropertyInfo propInfo))
            {
                Property newProperty = null;
                //Todo: Maybe lookup the default value?
                switch (propInfo.Type)
                {
                    case PropertyType.IntProperty:
                        newProperty = new IntProperty(0, propName);
                        break;
                    case PropertyType.BoolProperty:
                        newProperty = new BoolProperty(false, propName);
                        break;
                    case PropertyType.FloatProperty:
                        newProperty = new FloatProperty(0.0f, propName);
                        break;
                    case PropertyType.StringRefProperty:
                        newProperty = new StringRefProperty(propName);
                        break;
                    case PropertyType.StrProperty:
                        newProperty = new StrProperty("", propName);
                        break;
                    case PropertyType.ArrayProperty:
                        newProperty = new ArrayProperty<IntProperty>(propName); //We can just set it to int as it will be reparsed and resolved.
                        break;
                    case PropertyType.NameProperty:
                        newProperty = new NameProperty("None", propName);
                        break;
                    case PropertyType.ByteProperty:
                        if (propInfo.IsEnumProp())
                        {
                            newProperty = new EnumProperty(propInfo.Reference, Pcc.Game, propName);
                        }
                        else
                        {
                            newProperty = new ByteProperty(0, propName);
                        }
                        break;
                    case PropertyType.BioMask4Property:
                        newProperty = new BioMask4Property(0, propName);
                        break;
                    case PropertyType.ObjectProperty:
                        newProperty = new ObjectProperty(0, propName);
                        break;
                    case PropertyType.DelegateProperty:
                        newProperty = new DelegateProperty("None", 0, propName);
                        break;
                    case PropertyType.StructProperty:
                        PropertyCollection structProps = GlobalUnrealObjectInfo.getDefaultStructValue(Pcc.Game, propInfo.Reference, true);
                        newProperty = new StructProperty(propInfo.Reference, structProps, propName, isImmutable: GlobalUnrealObjectInfo.IsImmutable(propInfo.Reference, Pcc.Game));
                        break;
                }

                //UProperty property = generateNewProperty(prop.Item1, currentInfo);
                if (newProperty != null)
                {
                    newProperty.StaticArrayIndex = staticArrayIndex;
                    CurrentLoadedProperties.Insert(CurrentLoadedProperties.Count - 1, newProperty); //insert before noneproperty
                    ForcedRescanOffset = CurrentLoadedProperties.Last().StartOffset;
                }
                //Todo: Create new node, prevent refresh of this instance.
                CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);
                //End Todo
            }
        }

        private void RemoveProperty()
        {
            if (Interpreter_TreeView.SelectedItem is UPropertyTreeViewEntry tvi && tvi.Parent != null && tvi.Property != null)
            {
                if (tvi.Parent.Parent == null)
                {
                    CurrentLoadedProperties.Remove(tvi.Property);
                }
                else if (tvi.Parent.Property is StructProperty sp) //inside struct
                {
                    sp.Properties.Remove(tvi.Property);
                }
                CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);
            }
        }

        private bool CanRemoveProperty()
        {
            if (Interpreter_TreeView?.SelectedItem is UPropertyTreeViewEntry tvi)
            {
                return tvi.Parent != null && tvi.Property is not NoneProperty &&
                       (tvi.Parent.Parent == null //items with a single parent (root nodes)
                     || tvi.Parent.Property is StructProperty { IsImmutable: false }); //properties that are part of a non-immutable StructProperty
            }
            return false;
        }

        private void AddPropertiesToStruct()
        {
            if (Interpreter_TreeView.SelectedItem is UPropertyTreeViewEntry { Property: StructProperty sp })
            {
                PropertyCollection defaultProps = GlobalUnrealObjectInfo.getDefaultStructValue(Pcc.Game, sp.StructType, true);
                foreach (Property prop in sp.Properties)
                {
                    defaultProps.AddOrReplaceProp(prop);
                }

                sp.Properties = defaultProps;
                CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);
            }
        }

        private bool CanAddPropertiesToStruct()
        {
            if (Interpreter_TreeView?.SelectedItem is UPropertyTreeViewEntry { Property: StructProperty { IsImmutable: false } sp } tvi)
            {
                ClassInfo structInfo = GlobalUnrealObjectInfo.GetClassOrStructInfo(Pcc.Game, sp.StructType);
                var allProps = new List<PropNameStaticArrayIdxPair>();
                while (structInfo != null)
                {
                    foreach ((NameReference propName, PropertyInfo propInfo) in structInfo.properties)
                    {
                        if (propInfo.IsStaticArray())
                        {
                            for (int i = 0; i < propInfo.StaticArrayLength; i++)
                            {
                                allProps.Add(new PropNameStaticArrayIdxPair(propName, i));
                            }
                        }
                        else
                        {
                            allProps.Add(new PropNameStaticArrayIdxPair(propName, 0));
                        }
                    }
                    structInfo = GlobalUnrealObjectInfo.GetClassOrStructInfo(Pcc.Game, structInfo.baseClass);
                }
                HashSet<PropNameStaticArrayIdxPair> existingPropNames = tvi.ChildrenProperties.Select(t => new PropNameStaticArrayIdxPair(t.Property.Name, t.Property.StaticArrayIndex)).ToHashSet();
                if (!allProps.All(existingPropNames.Contains))
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        /// <summary>
        /// Unloads the loaded export, if any
        /// </summary>
        public override void UnloadExport()
        {
            CurrentLoadedExport = null;
            EditorSetElements.ForEach(x => x.Visibility = Visibility.Collapsed);
            Set_Button.Visibility = Visibility.Collapsed;
            //EditorSet_Separator.Visibility = Visibility.Collapsed;
            (Interpreter_Hexbox?.ByteProvider as ReadOptimizedByteProvider)?.Clear();
            Interpreter_Hexbox?.Refresh();
            HasUnsavedChanges = false;
            PropertyNodes.Clear();
        }

        /// <summary>
        /// Load a new export for display and editing in this control
        /// </summary>
        /// <param name="export"></param>
        public override void LoadExport(ExportEntry export)
        {
            EditorSetElements.ForEach(x => x.Visibility = Visibility.Collapsed);
            Set_Button.Visibility = Visibility.Collapsed;
            //EditorSet_Separator.Visibility = Visibility.Collapsed;
            HasUnsavedChanges = false;
            Interpreter_Hexbox.UnhighlightAll();
            //set rescan offset
            //TODO: Make this more reliable because it is recycling virtualization
            if (CurrentLoadedExport != null && export.FileRef == Pcc && export.UIndex == CurrentLoadedExport.UIndex)
            {
                if (SelectedItem is UPropertyTreeViewEntry { Property: not null } tvi)
                {
                    RescanSelectionOffset = tvi.Property.StartOffset;
                }
            }
            else
            {
                RescanSelectionOffset = 0;
            }
            if (ForcedRescanOffset != 0)
            {
                RescanSelectionOffset = ForcedRescanOffset;
                ForcedRescanOffset = 0;
            }
            //Debug.WriteLine("Selection offset: " + RescanSelectionOffset);
            CurrentLoadedExport = export;
            isLoadingNewData = true;
            (Interpreter_Hexbox.ByteProvider as ReadOptimizedByteProvider)?.ReplaceBytes(export.Data);
            hb1_SelectionChanged(null, null); //refresh bottom text
            Interpreter_Hexbox.Select(0, 1);
            Interpreter_Hexbox.ScrollByteIntoView();
            isLoadingNewData = false;
            StartScan();
        }

        /// <summary>
        /// Call this when reloading the entire tree.
        /// </summary>
        private void StartScan()
        {
            PropertyNodes.Clear();

            if (CurrentLoadedExport.ClassName == "Class")
            {
                var topLevelTree = new UPropertyTreeViewEntry
                {
                    DisplayName = $"Export {CurrentLoadedExport.UIndex}: {CurrentLoadedExport.ObjectName.Instanced} ({CurrentLoadedExport.ClassName})",
                    IsExpanded = true
                };

                topLevelTree.ChildrenProperties.Add(new UPropertyTreeViewEntry
                {
                    DisplayName = $"Class objects do not have properties.\nDefault properties for this class are located in the Default__{CurrentLoadedExport.ObjectName} object.",
                    Parent = topLevelTree,
                    AdvancedModeText = "" //blank the bottom line
                });

                PropertyNodes.Add(topLevelTree);
            }
            else
            {
                var topLevelTree = new UPropertyTreeViewEntry
                {
                    DisplayName = $"Export {CurrentLoadedExport.UIndex}: {CurrentLoadedExport.InstancedFullPath} ({CurrentLoadedExport.ClassName})",
                    IsExpanded = true
                };

                PropertyNodes.Add(topLevelTree);

                try
                {
                    CurrentLoadedProperties = CurrentLoadedExport.GetProperties(includeNoneProperties: true);
                    foreach (Property prop in CurrentLoadedProperties)
                    {
                        GenerateUPropertyTreeForProperty(prop, topLevelTree, CurrentLoadedExport, PropertyChangedHandler: OnUPropertyTreeViewEntry_PropertyChanged);
                    }
                }
                catch (Exception ex)
                {
                    var errorNode = new UPropertyTreeViewEntry
                    {
                        DisplayName = $"PARSE ERROR: {ex.Message}"
                    };
                    topLevelTree.ChildrenProperties.Add(errorNode);
                }

                if (RescanSelectionOffset != 0)
                {
                    UPropertyTreeViewEntry itemToSelect = topLevelTree.FlattenTree().LastOrDefault(x => x.Property != null && x.Property.StartOffset == RescanSelectionOffset);
                    if (itemToSelect != null)
                    {
                        UPropertyTreeViewEntry cachedSelectedItem = itemToSelect;
                        if (ArrayElementJustAdded)
                        {
                            //Ensure we are at array level so we can choose the last item
                            if (!(itemToSelect.Property is ArrayPropertyBase))
                            {
                                itemToSelect = itemToSelect.Parent;
                            }

                            RescanSelectionOffset = 0;
                            ArrayElementJustAdded = false;
                            if (itemToSelect.ChildrenProperties.LastOrDefault() is UPropertyTreeViewEntry u)
                            {
                                u.ExpandParents();
                                u.IsSelected = true;
                                return;
                            }
                        }
                        //todo: select node using parent-first selection (from packageeditor)
                        //due to tree view virtualization

                        cachedSelectedItem.ExpandParents();
                        cachedSelectedItem.IsSelected = true;
                    }
                    RescanSelectionOffset = 0;
                }
            }
        }

        #region Static tree generating code (shared with BinaryInterpreterExportLoader)
        public static void GenerateUPropertyTreeForProperty(Property prop, UPropertyTreeViewEntry parent, ExportEntry export, string displayPrefix = "", PropertyChangedEventHandler PropertyChangedHandler = null)
        {
            var upropertyEntry = GenerateUPropertyTreeViewEntry(prop, parent, export, displayPrefix, PropertyChangedHandler);
            switch (prop)
            {
                case ArrayPropertyBase arrayProp:
                    {
                        int i = 0;
                        if (arrayProp.Count > 1000 && Settings.Interpreter_LimitArrayPropertySize)
                        {
                            //Too big to load reliably, users won't edit huge things like this anyways.
                            var wontshowholder = new UPropertyTreeViewEntry
                            {
                                DisplayName = "Too many children to display",
                                HasTooManyChildrenToDisplay = true,
                                AdvancedModeText = "Disable this optimization in Package Editor Options menu"
                            };
                            upropertyEntry.HasTooManyChildrenToDisplay = true;
                            upropertyEntry.ChildrenProperties.Add(wontshowholder);
                        }
                        else
                        {
                            foreach (Property listProp in arrayProp.Properties)
                            {
                                GenerateUPropertyTreeForProperty(listProp, upropertyEntry, export, $" Item {i++}:", PropertyChangedHandler);
                            }
                        }
                        break;
                    }
                case StructProperty sProp:
                    {
                        foreach (var subProp in sProp.Properties)
                        {
                            GenerateUPropertyTreeForProperty(subProp, upropertyEntry, export, PropertyChangedHandler: PropertyChangedHandler);
                        }
                        break;
                    }
            }
        }

        internal void SetParentNameList(ObservableCollectionExtended<IndexedName> namesList)
        {
            ParentNameList = namesList;
        }

        public static UPropertyTreeViewEntry GenerateUPropertyTreeViewEntry(Property prop, UPropertyTreeViewEntry parent, ExportEntry parsingExport, string displayPrefix = "", PropertyChangedEventHandler PropertyChangedHandler = null)
        {
            string displayName;

            if (parent.Property is ArrayPropertyBase)
            {
                displayName = displayPrefix;
            }
            else
            {
                string propName = prop.Name.Instanced;
                int strLength = propName.Length + 1;
                if (displayPrefix.Length > 0)
                {
                    strLength += displayPrefix.Length + 1;
                }

                var propInfo = GlobalUnrealObjectInfo.GetPropertyInfo(parsingExport.FileRef.Game, prop.Name,
                    parent.Property is StructProperty sp ? sp.StructType : parsingExport.ClassName, containingExport: parsingExport);
                bool isStaticArrayProp = false;
                if (propInfo?.StaticArrayLength > 1 || prop.StaticArrayIndex > 0)
                {
                    isStaticArrayProp = true;
                    strLength += prop.StaticArrayIndex.NumDigits() + 2;
                }

                displayName = string.Create(strLength, (displayPrefix, propName, isStaticArrayProp ? prop.StaticArrayIndex : -1), (span, tuple) =>
                {
                    int pos = 0;
                    if (tuple.displayPrefix.Length > 0)
                    {
                        tuple.displayPrefix.AsSpan().CopyTo(span);
                        pos = tuple.displayPrefix.Length;
                        span[pos] = ' ';
                        ++pos;
                    }
                    tuple.propName.AsSpan().CopyTo(span.Slice(pos));
                    pos += tuple.propName.Length;
                    if (tuple.Item3 >= 0)
                    {
                        span[pos] = '[';
                        ++pos;
                        int numDigits = tuple.Item3.NumDigits();
                        ((uint)tuple.Item3).ToStrInPlace(span.Slice(pos, numDigits));
                        pos += numDigits;
                        span[pos] = ']';
                        ++pos;
                    }

                    span[pos] = ':';
                });
            }

            bool isExpanded = false;
            string editableValue = ""; //editable value
            string parsedValue = ""; //human formatted item. Will most times be blank
            switch (prop)
            {
                case ObjectProperty op:
                    {
                        int index = op.Value;
                        var entry = parsingExport.FileRef.GetEntry(index);
                        editableValue = index.ToString();
                        if (entry != null)
                        {
                            parsedValue = entry.InstancedFullPath;
                            if (index > 0 && ExportToStringConverters.Contains(entry.ClassName))
                            {
                                editableValue = $"{index} {ExportToString(parsingExport.FileRef.GetUExport(index))}";
                            }
                        }
                        else if (index == 0)
                        {
                            parsedValue = "Null";
                        }
                        else
                        {
                            parsedValue = $"Index out of bounds of {(index < 0 ? "Import" : "Export")} list";
                        }
                    }
                    break;
                case DelegateProperty dp:
                    {
                        int index = dp.Value.ContainingObjectUIndex;
                        var entry = parsingExport.FileRef.GetEntry(index);
                        editableValue = index.ToString();
                        if (entry != null)
                        {
                            parsedValue = $"{entry.InstancedFullPath}.{dp.Value.FunctionName.Instanced}";
                        }
                        else if (index == 0)
                        {
                            parsedValue = dp.Value.FunctionName.Instanced;
                        }
                        else
                        {
                            parsedValue = $"Index out of bounds of {(index < 0 ? "Import" : "Export")} list";
                        }
                    }
                    break;
                case IntProperty ip:
                    {
                        editableValue = ip.Value.ToString();
                        if (IntToStringConverters.Contains(parsingExport.ClassName))
                        {
                            parsedValue = IntToString(prop.Name.Name ?? parent?.Property.Name.Name, ip.Value, parsingExport);
                        }
                        if (ip.Name == "m_nStrRefID" || ip.Name == "nLineStrRef" || ip.Name == "nStrRefID" || ip.Name == "m_iStringRef" || ip.Name == "m_iDescriptionStringRef" || ip.Name == "m_srStringID")
                        {
                            parsedValue = IntToString(prop.Name, ip.Value, parsingExport);
                        }

                        if (ip.Name == "VisibleConditional" || ip.Name == "UsableConditional" || ip.Name == "ReaperControlCondition" || ip.Name == "PlanetLandCondition" ||
                             ip.Name == "PlanetPlotLabelCondition" || ip.Name == "DisplayGAWCondition" || ip.Name == "InvasionCondition" || ip.Name == "DestructionCondition")
                        {
                            parsedValue = PlotDatabases.FindPlotConditionalByID(ip.Value, parsingExport.Game)?.Path;
                        }

                        if (parent.Property is StructProperty { StructType: "Rotator" })
                        {
                            parsedValue = $"({ip.Value.UnrealRotationUnitsToDegrees():0.0######} degrees)";
                        }
                    }
                    break;
                case FloatProperty fp:
                    editableValue = fp.Value.ToString("0.########");
                    break;
                case BoolProperty bp:
                    editableValue = bp.Value.ToString(); //combobox
                    break;
                case ArrayPropertyBase ap:
                    {
                        ArrayType at = GlobalUnrealObjectInfo.GetArrayType(parsingExport.FileRef.Game, prop.Name, parent.Property is StructProperty sp ? sp.StructType : parsingExport.ClassName, parsingExport);

                        if (at is ArrayType.Struct or ArrayType.Enum or ArrayType.Object)
                        {
                            // Try to get the type of struct array
                            // This code doesn't work for nested structs as the containing class is different
                            var containingType = parent.Property is StructProperty pStructProp ? pStructProp.StructType : parsingExport.ClassName;
                            PropertyInfo p = GlobalUnrealObjectInfo.GetPropertyInfo(parsingExport.FileRef.Game, prop.Name, containingType);
                            if (p != null)
                            {
                                editableValue = $"{p.Reference} {at} array";

                                // Expanded array types
                                if (parent.Property == null && p.Reference == "TimelineEffect")
                                {
                                    isExpanded = true;
                                }
                                break;
                            }
                        }

                        if (at is ArrayType.Byte)
                        {
                            // Special converters
                            if (parsingExport.ClassName == "CoverLink" && prop.Name.Name == "Interactions" && prop is ImmutableByteArrayProperty ibap)
                            {
                                string actions = "";
                                foreach (var b in ibap.Bytes)
                                {
                                    actions += "[";
                                    actions += (b & (1 << 4)) != 0 ? "CT_MidLevel " : "CT_Standing "; // DestType
                                    actions += (b & (1 << 5)) != 0 ? "CA_LeanLeft" :
                                        (b & (1 << 6)) != 0 ? "CA_LeanRight" :
                                        (b & (1 << 7)) != 0 ? "CA_PopUp" :
                                        "CA_Default";
                                    actions += "] ";
                                }

                                editableValue = $"{at} array - {actions}";
                                break;
                            }
                        }
                        editableValue = $"{at} array";
                    }
                    break;
                case NameProperty np:
                    editableValue = $"{parsingExport.FileRef.findName(np.Value.Name)}_{np.Value.Number}";
                    parsedValue = np.Value.Instanced;
                    break;
                case ByteProperty bp:
                    editableValue = parsedValue = bp.Value.ToString();
                    break;
                case BioMask4Property b4p:
                    editableValue = parsedValue = b4p.Value.ToString();
                    break;
                case EnumProperty ep:
                    editableValue = ep.Value.Instanced;
                    break;
                case StringRefProperty strrefp:
                    editableValue = strrefp.Value.ToString();
                    parsedValue = TLKManagerWPF.GlobalFindStrRefbyID(strrefp.Value, parsingExport.FileRef.Game, parsingExport.FileRef);
                    break;
                case StrProperty strp:
                    editableValue = strp.Value;
                    break;
                case StructProperty sp:
                    // CUSTOM UI TEMPLATES GO HERE
                    if (sp.StructType is "Vector" or "Rotator" or "Cylinder" or "PlotStreamingElement" or "RwVector3" or "Plane")
                    {
                        string parsedText = string.Join(", ", sp.Properties.Where(x => !(x is NoneProperty)).Select(p =>
                         {
                             switch (p)
                             {
                                 case FloatProperty fp:
                                     return $"{p.Name}={fp.Value}";
                                 case IntProperty ip:
                                     return $"{p.Name}={ip.Value}";
                                 case NameProperty np:
                                     return $"{p.Name}={np.Value}";
                                 case BoolProperty bp:
                                     return $"{p.Name}={bp.Value}";
                                 default:
                                     return "";
                             }
                         }));
                        parsedValue = $"({parsedText})";
                    }
                    else if (sp.StructType == "Guid")
                    {
                        //may seem a strange place to put this kind of optimization, but the old way of using a MemoryStream
                        //generated a surprisingly large amount of allocations during package dumping
                        Span<byte> guidBytes = stackalloc byte[16];
                        for (int i = 0; i < 4; i++)
                        {
                            IntProperty intProperty = (IntProperty)sp.Properties[i];
                            int value = intProperty.Value;
                            MemoryMarshal.Write(guidBytes.Slice(i * 4), in value);
                        }

                        parsedValue = new Guid(guidBytes).ToString();
                    }
                    else if (sp.StructType == "PlotStreamingSet")
                    {
                        parsedValue = $"({sp.Properties.GetProp<NameProperty>("VirtualChunkName")?.Value})";
                    }
                    else if (sp.StructType == "SFXVocalizationRole")
                    {
                        parsedValue = $"({sp.Properties.GetProp<ArrayProperty<StructProperty>>("Roles")?.Count ?? 0} roles)";
                    }
                    else if (sp.StructType == "TimelineEffect")
                    {
                        EnumProperty typeProp = sp.Properties.GetProp<EnumProperty>("Type");
                        FloatProperty timeIndex = sp.Properties.GetProp<FloatProperty>("TimeIndex");
                        string timelineEffectType = typeProp != null
                            ? $"{typeProp.Value} @ {timeIndex.Value}s"
                            : "Unknown effect";
                        switch (typeProp.Value)
                        {
                            case "TLT_InputOn":
                                timelineEffectType += $" {sp.Properties.GetProp<StrProperty>("InputAlias")?.Value} {sp.Properties.GetProp<DelegateProperty>("InputHandle")?.Value.FunctionName.Instanced}";
                                break;
                            case "TLT_Function":
                                timelineEffectType += $" {sp.Properties.GetProp<NameProperty>("Func")?.Value}()";
                                break;
                            case "TLT_ClientEffect":
                                timelineEffectType += $" {sp.Properties.GetProp<ObjectProperty>("RVR_CrustTemplate")?.ResolveToEntry(parsingExport.FileRef)?.InstancedFullPath}";
                                break;
                            case "TLT_GameEffect":
                                timelineEffectType += $" {sp.Properties.GetProp<ObjectProperty>("GameEffectClass")?.ResolveToEntry(parsingExport.FileRef)?.InstancedFullPath}";
                                break;
                            case "TLT_Sound":
                                timelineEffectType += $" {sp.Properties.GetProp<ObjectProperty>("Sound")?.ResolveToEntry(parsingExport.FileRef)?.InstancedFullPath}";
                                break;
                            case "TLT_ScreenShake":
                                timelineEffectType += $" {sp.Properties.GetProp<ObjectProperty>("ScreenShakeClass")?.ResolveToEntry(parsingExport.FileRef)?.InstancedFullPath}";
                                break;
                            case "TLT_Rumble":
                                timelineEffectType += $" {sp.Properties.GetProp<ObjectProperty>("RumbleClass")?.ResolveToEntry(parsingExport.FileRef)?.InstancedFullPath}";
                                break;
                            case "TLT_Damage":
                                timelineEffectType += $" {sp.Properties.GetProp<FloatProperty>("Damage")?.Value}";
                                break;
                            case "TLT_AOEVisiblePawns":
                                timelineEffectType += $" {sp.Properties.GetProp<ObjectProperty>("AOEImpactTimeline")?.ResolveToEntry(parsingExport.FileRef)?.InstancedFullPath}";
                                break;

                        }
                        parsedValue = $"({timelineEffectType})";
                    }
                    else if (sp.StructType == "BioStreamingState")
                    {
                        editableValue = "";
                        NameProperty stateName = sp.Properties.GetProp<NameProperty>("StateName");
                        if (stateName != null && stateName.Value.Name != "None")
                        {
                            editableValue = $"{stateName.Value.Name}";
                        }

                        NameProperty inChunkName = sp.Properties.GetProp<NameProperty>("InChunkName");
                        if (inChunkName != null && inChunkName.Value.Name != "None")
                        {
                            editableValue += $" InChunkName: {inChunkName.Value.Name}";
                        }
                    }
                    else if (sp.StructType == "SeqVarLink")
                    {
                        var linkName = sp.Properties.GetProp<StrProperty>("LinkDesc");
                        if (linkName != null)
                        {
                            editableValue = $"SeqVarLink {linkName.Value}";
                        }
                    }
                    else if (sp.StructType == "BaseSliders")
                    {
                        var linkName = sp.Properties.GetProp<StrProperty>("m_sSliderName");
                        if (linkName != null)
                        {
                            editableValue = $"BaseSliders - {linkName.Value}";
                        }
                    }
                    else if (sp.StructType == "Category")
                    {
                        var linkName = sp.Properties.GetProp<StrProperty>("m_sCatName");
                        if (linkName != null)
                        {
                            editableValue = $"Category - {linkName.Value}";
                        }
                    }
                    else if (sp.StructType == "Slider")
                    {
                        var linkName = sp.Properties.GetProp<StrProperty>("m_sName");
                        if (linkName != null)
                        {
                            editableValue = $"Slider - {linkName.Value}";
                        }
                    }
                    else if (sp.StructType == "TextureParameterValue")
                    {
                        var parmName = sp.GetProp<NameProperty>("ParameterName");
                        var parmValue = sp.GetProp<ObjectProperty>("ParameterValue");

                        if (parmName != null && parmValue != null && parmValue.Value != 0 && parsingExport.FileRef.TryGetEntry(parmValue.Value, out var entry))
                        {
                            parsedValue = $" {parmName}: {entry.ObjectName.Instanced}";
                        }
                    }
                    else if (sp.StructType == "TextureParameter")
                    {
                        var parmName = sp.GetProp<NameProperty>("nName");
                        var parmValue = sp.GetProp<ObjectProperty>("m_pTexture");

                        if (parmName != null && parmValue != null && parmValue.Value != 0)
                        {
                            parsedValue = $" {parmName}: {parsingExport.FileRef.GetEntry(parmValue.Value).ObjectName.Instanced}";
                        }
                    }
                    else if (sp.StructType == "ScalarParameterValue")
                    {
                        var parmValue = sp.GetProp<FloatProperty>("ParameterValue");
                        var parmName = sp.GetProp<NameProperty>("ParameterName");
                        if (parmName != null && parmValue != null)
                        {
                            parsedValue = $" {parmName}: {parmValue.Value}";
                        }
                    }
                    else if (sp.StructType == "ColorParameter")
                    {
                        //var parmValue = sp.GetProp<StructProperty>("cValue");
                        var parmName = sp.GetProp<NameProperty>("nName");
                        if (parmName != null)
                        {
                            parsedValue = $" {parmName}";
                        }
                    }
                    else if (sp.StructType == "ScalarParameter")
                    {
                        var parmValue = sp.GetProp<FloatProperty>("sValue");
                        var parmName = sp.GetProp<NameProperty>("nName");
                        if (parmName != null && parmValue != null)
                        {
                            parsedValue = $" {parmName}: {parmValue.Value}";
                        }
                    }
                    else if (sp.StructType == "VectorParameterValue")
                    {
                        var parmValue = sp.GetProp<StructProperty>("ParameterValue");
                        var parmName = sp.GetProp<NameProperty>("ParameterName");
                        if (parmName != null && parmValue != null)
                        {
                            string structParam = string.Join(", ", parmValue.Properties.Where(x => !(x is NoneProperty)).Select(p =>
                            {
                                switch (p)
                                {
                                    case FloatProperty fp:
                                        return $"{p.Name}={fp.Value}";
                                    case IntProperty ip:
                                        return $"{p.Name}={ip.Value}";
                                    default:
                                        return "";
                                }
                            }));
                            parsedValue = $" {parmName}: {structParam}";
                        }
                    }
                    else if (sp.StructType == "PowerLevelUp")
                    {
                        var powerClass = sp.GetProp<ObjectProperty>("PowerClass");
                        var rank = sp.GetProp<FloatProperty>("Rank");
                        var evolvedPowerClass = sp.GetProp<ObjectProperty>("EvolvedPowerClass");
                        parsedValue = $" {powerClass.ResolveToEntry(parsingExport.FileRef).ObjectName} Rank {rank.Value}";
                        if (evolvedPowerClass.Value != 0)
                        {
                            parsedValue += $" => {evolvedPowerClass.ResolveToEntry(parsingExport.FileRef).ObjectName}";
                        }
                    }
                    else if (sp.StructType == "BioStageCamera")
                    {
                        var cameraTagProp = parsingExport.Game.IsGame3() ? "nmCameraTag" : "sCameraTag";
                        var cameraTag = sp.GetProp<NameProperty>(cameraTagProp);
                        parsedValue = cameraTag?.Value.Instanced ?? "";
                    }
                    else if (sp.StructType == "BioStageCameraCustom")
                    {
                        var cameraTag = sp.GetProp<StrProperty>("m_sCameraName");
                        parsedValue = cameraTag?.Value ?? "";
                    }
                    else if (sp.StructType == "FireLink" && parsingExport.Game.IsLEGame())
                    {
                        var destActor = sp.GetProp<StructProperty>("TargetActor")?.GetProp<ObjectProperty>("Actor");
                        if (destActor != null)
                        {
                            var destSlot = sp.GetProp<StructProperty>("TargetActor")?.GetProp<IntProperty>("SlotIdx");
                            if (destSlot != null && destActor.Value != 0 && parsingExport.FileRef.TryGetEntry(destActor.Value, out var entry))
                            {
                                parsedValue = $"-> {entry.ObjectName.Instanced} Slot {destSlot.Value}";
                            }
                        }
                    }
                    else if (sp.StructType == "FireLinkItem" && parsingExport.Game.IsLEGame())
                    {
                        parsedValue = $"{sp.GetProp<EnumProperty>("SrcType").Value} {sp.GetProp<EnumProperty>("SrcAction").Value} -> {sp.GetProp<EnumProperty>("DestType").Value} {sp.GetProp<EnumProperty>("DestAction").Value}";
                    }
                    else if (sp.StructType == "TerrainLayer")
                    {
                        parsedValue = $"{sp.GetProp<StrProperty>("Name").Value}";
                    }
                    else if (sp.StructType == "FilterLimit")
                    {
                        parsedValue = $"Enabled={sp.GetProp<BoolProperty>("Enabled").Value}, Base={sp.GetProp<FloatProperty>("Base").Value}";
                    }
                    else if (sp.StructType == "ExpressionInput")
                    {
                        isExpanded = true;
                        parsedValue = sp.StructType;
                    }
                    else if (sp.StructType == "RvrMultiplexorEntry")
                    {
                        parsedValue = $"{sp.GetProp<NameProperty>("m_nmTag").Value}";
                    }
                    else if (sp.StructType == "BioPropertyInfo")
                    {
                        var propertyName = sp.GetProp<NameProperty>("PropertyName")?.Value ?? "";
                        var actualPropertyName = sp.GetProp<NameProperty>("ActualPropertyName")?.Value ?? "";
                        parsedValue = $"{propertyName} ({actualPropertyName})";
                    }
                    else if (sp.StructType == "PropertyInfo")
                    {
                        parsedValue = $"{sp.GetProp<NameProperty>("PropertyName").Value}";
                    }
                    else if (sp.StructType is "LightingChannelContainer" or "RBCollisionChannelContainer")
                    {
                        var channels = sp.Properties.Where(p => p is BoolProperty { Value: true });
                        parsedValue = string.Join(", ", channels.Select(p => p.Name.Instanced));
                    }
                    else if (sp.StructType is "MorphFeature")
                    {
                        parsedValue = $"{sp.GetProp<NameProperty>("sFeatureName")?.Value}";
                    }
                    else if (sp.StructType is "OffsetBonePos")
                    {
                        parsedValue = $"{sp.GetProp<NameProperty>("nName")?.Value}";
                    }
                    else if (sp.StructType is "RvrCEParameterDistribution")
                    {
                        parsedValue = $"Variable: {sp.GetProp<StructProperty>("Parameter").GetProp<NameProperty>("Variable")}";
                    }
                    else if (sp.StructType is "SettingsPropertyPropertyMetaData")
                    {
                        parsedValue =
                            $"ID: {sp.GetProp<IntProperty>("Id").Value}, Name: {sp.GetProp<NameProperty>("Name").Value.Instanced} | {sp.GetProp<EnumProperty>(@"MappingType").Value.Instanced}";
                    }
                    else
                    {
                        parsedValue = sp.StructType;
                    }
                    break;
                case NoneProperty _:
                    parsedValue = "End of properties";
                    break;
            }
            var item = new UPropertyTreeViewEntry
            {
                Property = prop,
                EditableValue = editableValue,
                ParsedValue = parsedValue,
                DisplayName = displayName,
                Parent = parent,
                AttachedExport = parsingExport,
                IsExpanded = isExpanded
            };

            //Auto expand items
            if (item.Property != null && item.Property.Name == "StreamingStates")
            {
                item.IsExpanded = true;
            }

            if (PropertyChangedHandler != null)
            {
                item.PropertyChanged += PropertyChangedHandler;
            }
            parent.ChildrenProperties.Add(item);
            return item;
        }

        private void OnUPropertyTreeViewEntry_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var uptvi = (UPropertyTreeViewEntry)sender;
            switch (e.PropertyName)
            {
                case "ColorStructCode" when uptvi.Property is StructProperty { StructType: "Color" } colorStruct:
                    {
                        uptvi.ChildrenProperties.ClearEx();
                        foreach (var subProp in colorStruct.Properties)
                        {
                            GenerateUPropertyTreeForProperty(subProp, uptvi, uptvi.AttachedExport, PropertyChangedHandler: OnUPropertyTreeViewEntry_PropertyChanged);
                        }
                        var a = colorStruct.GetProp<ByteProperty>("A");
                        var r = colorStruct.GetProp<ByteProperty>("R");
                        var g = colorStruct.GetProp<ByteProperty>("G");
                        var b = colorStruct.GetProp<ByteProperty>("B");

                        var byteProvider = (ReadOptimizedByteProvider)Interpreter_Hexbox.ByteProvider;
                        byteProvider.WriteByte(a.ValueOffset, a.Value);
                        byteProvider.WriteByte(r.ValueOffset, r.Value);
                        byteProvider.WriteByte(g.ValueOffset, g.Value);
                        byteProvider.WriteByte(b.ValueOffset, b.Value);
                        Interpreter_Hexbox.Refresh();
                        break;
                    }
                case "ColorStructCode" when uptvi.Property is StructProperty { StructType: "LinearColor" } linColStruct:
                    {
                        uptvi.ChildrenProperties.ClearEx();
                        foreach (var subProp in linColStruct.Properties)
                        {
                            GenerateUPropertyTreeForProperty(subProp, uptvi, uptvi.AttachedExport, PropertyChangedHandler: OnUPropertyTreeViewEntry_PropertyChanged);
                        }
                        var a = linColStruct.GetProp<FloatProperty>("A");
                        var r = linColStruct.GetProp<FloatProperty>("R");
                        var g = linColStruct.GetProp<FloatProperty>("G");
                        var b = linColStruct.GetProp<FloatProperty>("B");

                        var byteProvider = (ReadOptimizedByteProvider)Interpreter_Hexbox.ByteProvider;
                        byteProvider.WriteBytes(a.ValueOffset, BitConverter.GetBytes(a.Value));
                        byteProvider.WriteBytes(r.ValueOffset, BitConverter.GetBytes(r.Value));
                        byteProvider.WriteBytes(g.ValueOffset, BitConverter.GetBytes(g.Value));
                        byteProvider.WriteBytes(b.ValueOffset, BitConverter.GetBytes(b.Value));
                        Interpreter_Hexbox.Refresh();
                        break;
                    }
            }
        }

        /// <summary>
        /// Converts a value of a property into a more human readable string.
        /// This is for IntProperty.
        /// </summary>
        /// <param name="name">Name of the property</param>
        /// <param name="value">Value of the property to transform</param>
        /// <param name="export">export the property belongs to</param>
        /// <returns></returns>
        private static string IntToString(NameReference name, int value, ExportEntry export)
        {
            switch (export.ClassName)
            {
                case "CoverLink":
                    switch (name)
                    {
                        case "PackedProperties_CoverPairRefAndDynamicInfo": // FireLink in ME3/LE3
                            {
                                var dynamicLinkInfoIndex = (value & 0xFFFF0000) >> 16;
                                var covRefIdx = value & 0x0000FFFF;

                                var level = ObjectBinary.From<Level>(export.FileRef.FindExport("TheWorld.PersistentLevel"));
                                if (level.CoverIndexPairs.Count >= covRefIdx)
                                {
                                    var cover = level.CoverIndexPairs[covRefIdx];
                                    var coverRef = level.CoverLinkRefs[(int)cover.CoverIndexIdx];
                                    if (coverRef > 0)
                                    {
                                        return $"Cover Reference: {export.FileRef.GetUExport(coverRef).ObjectName.Instanced} slot {cover.SlotIdx}, DynamicLinkInfoIndex: {dynamicLinkInfoIndex}";
                                    }
                                    else
                                    {
                                        return $"Cover Reference: <Null>, slot {cover.SlotIdx}, DynamicLinkInfoIndex: {dynamicLinkInfoIndex}";
                                    }
                                }

                                return $"INVALID COVREF {covRefIdx}";
                            }
                        case "ExposedCoverPackedProperties":
                            {
                                var exposedScale = (value & 0xFFFF0000) >> 16;
                                var covRefIdx = value & 0x0000FFFF;

                                var level = ObjectBinary.From<Level>(export.FileRef.FindExport("TheWorld.PersistentLevel"));
                                if (level.CoverIndexPairs.Count >= covRefIdx)
                                {
                                    var cover = level.CoverIndexPairs[covRefIdx];
                                    var coverLinkRef = level.CoverLinkRefs[(int)cover.CoverIndexIdx];
                                    if (coverLinkRef > 0)
                                    {
                                        return $"Cover Reference: {export.FileRef.GetUExport(coverLinkRef).ObjectName.Instanced} slot {cover.SlotIdx}, ExposureScale: {exposedScale}";
                                    }
                                    else
                                    {
                                        // Not entirely sure what this means. Value is 0, this is not import or export.
                                        return $"Cover Reference: <Null>, slot {cover.SlotIdx}, ExposureScale: {exposedScale}";
                                    }
                                }

                                return $"INVALID COVREF {covRefIdx}, Exposure level: {exposedScale}";
                            }
                        case "DangerCoverPackedProperties":
                            {
                                var dangerCost = (value & 0xFFFF0000) >> 16;
                                var navRefIdx = value & 0x0000FFFF;

                                var level = ObjectBinary.From<Level>(export.FileRef.FindExport("TheWorld.PersistentLevel"));
                                if (level.NavRefs.Count >= navRefIdx)
                                {
                                    var navRef = level.NavRefs[navRefIdx];
                                    if (navRef > 0)
                                    {
                                        return $"Nav Reference: {export.FileRef.GetUExport(navRef).ObjectName.Instanced}, Danger cost: {dangerCost}";
                                    }
                                    else
                                    {
                                        return $"Nav Reference: <Null>, Danger cost: {dangerCost}";
                                    }
                                }

                                return $"INVALID Nav {navRefIdx}, Danger cost: {dangerCost}";
                            }
                    }

                    break;
                case "WwiseEvent":
                case "WwiseBank":
                case "WwiseStream":
                    switch (name)
                    {
                        case "Id":
                            return $"(0x{value:X8})";
                    }

                    break;
                case "BioSeqVar_StoryManagerStateId":
                case "BioSeqVar_StoryManagerBool":
                case "BioSeqAct_PMCheckState":
                    if (name == "m_nIndex") return PlotDatabases.FindPlotBoolByID(value, export.Game)?.Path;
                    break;
                case "BioSeqVar_StoryManagerInt":
                    if (name == "m_nIndex") return PlotDatabases.FindPlotIntByID(value, export.Game)?.Path;
                    break;
                case "BioSeqVar_StoryManagerFloat":
                    if (name == "m_nIndex") return PlotDatabases.FindPlotFloatByID(value, export.Game)?.Path;
                    break;
                case "BioSeqAct_PMCheckConditional":
                    if (name == "m_nIndex") return PlotDatabases.FindPlotConditionalByID(value, export.Game)?.Path;
                    break;
                case "BioSeqAct_PMExecuteConsequence":
                case "BioSeqAct_PMExecuteTransition":
                    if (name == "m_nIndex") return PlotDatabases.FindPlotTransitionByID(value, export.Game)?.Path;
                    break;
                case "BioWorldInfo":
                    if (name == "Conditional") return PlotDatabases.FindPlotConditionalByID(value, export.Game)?.Path;
                    break;
                case "SFXSceneShopNodePlotCheck":
                    if (name == "m_nIndex" && !export.IsDefaultObject)
                    {
                        Enum.TryParse(export.GetProperty<EnumProperty>("VarType").Value.Name, out ESFXSSPlotVarType type);
                        switch (type)
                        {
                            case ESFXSSPlotVarType.PlotVar_Float:
                                {
                                    return PlotDatabases.FindPlotFloatByID(value, export.Game)?.Path;
                                }
                            case ESFXSSPlotVarType.PlotVar_State:
                                {
                                    return PlotDatabases.FindPlotBoolByID(value, export.Game)?.Path;
                                }
                            case ESFXSSPlotVarType.PlotVar_Int:
                                {
                                    return PlotDatabases.FindPlotIntByID(value, export.Game)?.Path;
                                }
                            default: return "";
                        }
                    }

                    break;

            }

            if (name == "m_nStrRefID" || name == "nLineStrRef" || name == "nStrRefID" || name == "m_iStringRef" || name == "m_iDescriptionStringRef" || name == "m_srStringID")
            {
                return TLKManagerWPF.GlobalFindStrRefbyID(value, export.FileRef.Game, export.FileRef);
            }
            return "";
        }

        private static string ExportToString(ExportEntry exportEntry)
        {
            switch (exportEntry.ClassName)
            {
                case "LevelStreamingKismet":
                    {
                        NameProperty prop = exportEntry.GetProperty<NameProperty>("PackageName");
                        return $"({prop.Value.Instanced})";
                    }
                case "StaticMeshComponent":
                    {
                        ObjectProperty smprop = exportEntry.GetProperty<ObjectProperty>("StaticMesh");
                        if (smprop != null)
                        {
                            IEntry smEntry = exportEntry.FileRef.GetEntry(smprop.Value);
                            if (smEntry != null)
                            {
                                return $"({smEntry.ObjectName.Instanced})";
                            }
                        }
                    }
                    break;
                case "LensFlareComponent":
                case "ParticleSystemComponent":
                    {
                        ObjectProperty smprop = exportEntry.GetProperty<ObjectProperty>("Template");
                        if (smprop != null)
                        {
                            IEntry smEntry = exportEntry.FileRef.GetEntry(smprop.Value);
                            if (smEntry != null)
                            {
                                return $"({smEntry.ObjectName.Instanced})";
                            }
                        }
                    }
                    break;
                case "DecalComponent":
                    {
                        ObjectProperty smprop = exportEntry.GetProperty<ObjectProperty>("DecalMaterial");
                        if (smprop != null)
                        {
                            IEntry smEntry = exportEntry.FileRef.GetEntry(smprop.Value);
                            if (smEntry != null)
                            {
                                return $"({smEntry.ObjectName.Instanced})";
                            }
                        }
                    }
                    break;
                case "AnimNodeSequence":
                case "BioAnimNodeSequence":
                    {
                        NameProperty prop = exportEntry.GetProperty<NameProperty>("AnimSeqName");
                        return $"({prop?.Value.Instanced ?? "No Name"})";
                    }
            }
            return "";
        }

        #endregion

        private void hb1_SelectionChanged(object sender, EventArgs e)
        {
            int start = (int)Interpreter_Hexbox.SelectionStart;
            int len = (int)Interpreter_Hexbox.SelectionLength;
            int size = (int)Interpreter_Hexbox.ByteProvider.Length;

            var currentData = ((ReadOptimizedByteProvider)Interpreter_Hexbox.ByteProvider).Span;
            try
            {
                if (start != -1 && start < size)
                {
                    string s = $"Byte: {currentData[start]}"; //if selection is same as size this will crash.
                    if (start <= currentData.Length - 4)
                    {
                        int val = EndianReader.ToInt32(currentData, start, Pcc.Endian);
                        s += $", Int: {val}";
                        s += $", Float: {EndianReader.ToSingle(currentData, start, Pcc.Endian)}";
                        if (Pcc.IsName(val))
                        {
                            s += $", Name: {Pcc.GetNameEntry(val)}";
                        }
                        if (Pcc.GetEntry(val) is ExportEntry exp)
                        {
                            s += $", Export: {exp.ObjectName.Instanced}";
                        }
                        else if (Pcc.GetEntry(val) is ImportEntry imp)
                        {
                            s += $", Import: {imp.ObjectName.Instanced}";
                        }
                    }
                    s += $" | Start=0x{start:X8} ";
                    if (len > 0)
                    {
                        s += $"Length=0x{len:X8} ";
                        s += $"End=0x{start + len - 1:X8}";
                    }
                    StatusBar_LeftMostText.Text = s;
                }
                else
                {
                    StatusBar_LeftMostText.Text = "Nothing Selected";
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public void ToggleHexbox()
        {
            if (HideHexBox)
            {
                Settings.PackageEditor_HideInterpreterHexBox = HideHexBox = false;
            }
            else
            {
                Settings.PackageEditor_HideInterpreterHexBox = HideHexBox = true;
                ToggleHexbox_Button.Visibility = Visibility.Visible;
            }
        }

        private void Interpreter_TreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // This runs in a delegate due to how multithread bubble-up items work with treeview.
            // Without this delegate, the item selected will randomly be a parent item instead.
            // From https://www.codeproject.com/Tips/208896/WPF-TreeView-SelectedItemChanged-called-twice
            Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() => UpdateHexboxPosition(e.NewValue as UPropertyTreeViewEntry)));
            UPropertyTreeViewEntry newSelectedItem = (UPropertyTreeViewEntry)e.NewValue;
            //list of visible elements for editing
            var SupportedEditorSetElements = new List<FrameworkElement>();
            if (newSelectedItem?.Property != null)
            {
                switch (newSelectedItem.Property)
                {
                    case IntProperty ip:
                        Value_TextBox.Text = ip.Value.ToString();
                        SupportedEditorSetElements.Add(Value_TextBox);
                        if (newSelectedItem.Parent?.Property is StructProperty { StructType: "Rotator" })
                        {
                            //we support editing rotators as degrees. We will preview the raw value and enter data in degrees instead.
                            SupportedEditorSetElements.Add(ParsedValue_TextBlock);
                            Value_TextBox.Text = $"{ip.Value.UnrealRotationUnitsToDegrees():0.0######}";
                            ParsedValue_TextBlock.Text = $"{ip.Value} (raw value)"; //raw
                        }
                        break;
                    case FloatProperty fp:
                        Value_TextBox.Text = fp.Value.ToString();
                        SupportedEditorSetElements.Add(Value_TextBox);
                        break;
                    case StrProperty strp:
                        Value_TextBox.Text = strp.Value;
                        SupportedEditorSetElements.Add(Value_TextBox);
                        break;
                    case BoolProperty bp:
                        {
                            SupportedEditorSetElements.Add(Value_ComboBox);
                            var values = new List<string> { "True", "False" };
                            Value_ComboBox.IsEditable = false;
                            Value_ComboBox.ItemsSource = values;
                            Value_ComboBox.SelectedIndex = bp.Value ? 0 : 1; //true : false
                        }
                        break;
                    case ObjectProperty op:
                        UpdateObjectComboBoxOptions(op, newSelectedItem);
                        Value_ObjectComboBox.SelectedItem = op.Value == 0 ? ZeroUIndexClassEntry.Instance : op.ResolveToEntry(CurrentLoadedExport.FileRef);
                        SupportedEditorSetElements.Add(Value_ObjectComboBox);

                        // This is old implementation: Switched over in nightly 07/23/2023
                        /*
                        Value_TextBox.Text = op.Value.ToString();
                        UpdateParsedEditorValue(newSelectedItem);
                        SupportedEditorSetElements.Add(Value_TextBox);
                        SupportedEditorSetElements.Add(ParsedValue_TextBlock);
                        */
                        break;
                    case DelegateProperty dp:
                        TextSearch.SetTextPath(Value_ComboBox, "Name");
                        Value_ComboBox.IsEditable = true;
                        if (ParentNameList == null)
                        {
                            Value_ComboBox.ItemsSource = Pcc.Names.Select((nr, i) => new IndexedName(i, nr)).ToList();
                        }
                        else
                        {
                            Value_ComboBox.ItemsSource = ParentNameList;
                        }
                        Value_ComboBox.SelectedIndex = Pcc.findName(dp.Value.FunctionName.Name);
                        NameIndex_TextBox.Text = dp.Value.FunctionName.Number.ToString();

                        Value_TextBox.Text = dp.Value.ContainingObjectUIndex.ToString();
                        UpdateParsedEditorValue(newSelectedItem);
                        SupportedEditorSetElements.Add(Value_TextBox);
                        SupportedEditorSetElements.Add(ParsedValue_TextBlock);
                        SupportedEditorSetElements.Add(Value_ComboBox);
                        SupportedEditorSetElements.Add(NameIndexPrefix_TextBlock);
                        SupportedEditorSetElements.Add(NameIndex_TextBox);
                        break;
                    case NameProperty np:
                        TextSearch.SetTextPath(Value_ComboBox, "Name");
                        Value_ComboBox.IsEditable = true;

                        if (ParentNameList == null)
                        {
                            Value_ComboBox.ItemsSource = Pcc.Names.Select((nr, i) => new IndexedName(i, nr)).ToList();
                        }
                        else
                        {
                            Value_ComboBox.ItemsSource = ParentNameList;
                        }
                        Value_ComboBox.SelectedIndex = Pcc.findName(np.Value.Name);
                        NameIndex_TextBox.Text = np.Value.Number.ToString();

                        SupportedEditorSetElements.Add(Value_ComboBox);
                        SupportedEditorSetElements.Add(NameIndexPrefix_TextBlock);
                        SupportedEditorSetElements.Add(NameIndex_TextBox);
                        break;
                    case EnumProperty ep:
                        {
                            SupportedEditorSetElements.Add(Value_ComboBox);
                            List<NameReference> values = GlobalUnrealObjectInfo.GetEnumValues(Pcc.Game, ep.EnumType, true);
                            Value_ComboBox.ItemsSource = values;
                            int indexSelected = values.IndexOf(ep.Value);
                            Value_ComboBox.SelectedIndex = indexSelected;
                        }
                        break;
                    case ByteProperty bp:
                        Value_TextBox.Text = bp.Value.ToString();
                        SupportedEditorSetElements.Add(Value_TextBox);
                        break;
                    case BioMask4Property b4p:
                        Value_TextBox.Text = b4p.Value.ToString();
                        SupportedEditorSetElements.Add(Value_TextBox);
                        break;
                    case StringRefProperty strrefp:
                        Value_TextBox.Text = strrefp.Value.ToString();
                        SupportedEditorSetElements.Add(Value_TextBox);
                        SupportedEditorSetElements.Add(ParsedValue_TextBlock);
                        UpdateParsedEditorValue(newSelectedItem);
                        break;
                }
            }

            Set_Button.Visibility = SupportedEditorSetElements.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            //Hide the non-used controls
            foreach (FrameworkElement fe in EditorSetElements)
            {
                fe.Visibility = SupportedEditorSetElements.Contains(fe) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Updates the object combobox based on the property
        /// </summary>
        /// <param name="op"></param>
        /// <param name="uPropertyTreeViewEntry"></param>
        private void UpdateObjectComboBoxOptions(ObjectProperty op, UPropertyTreeViewEntry uPropertyTreeViewEntry)
        {
            // Find if this is contained in a struct; we use that for property type parsing, 
            // otherwise just use the class itself
            string containingClassOrStructName = CurrentLoadedExport.ClassName; // Default
            var parentProperty = uPropertyTreeViewEntry.Parent;
            while (parentProperty != null && parentProperty.Property != null)
            {
                if (parentProperty.Property is StructProperty sp)
                {
                    containingClassOrStructName = sp.StructType;
                    break;
                }
                else if (parentProperty.Property is ArrayPropertyBase apb)
                {
                    containingClassOrStructName = apb.Reference;
                }

                parentProperty = parentProperty.Parent;
            }

            var expectedType = GlobalUnrealObjectInfo.GetExpectedClassTypeForObjectProperty(CurrentLoadedExport, op, containingClassOrStructName, uPropertyTreeViewEntry.Parent?.Property);
            Value_ObjectComboBox.ItemsSource = MakeAllEntriesList(expectedType);
        }

        private List<object> MakeAllEntriesList(string onlyOfType = null)
        {
            var allEntriesNew = new List<object>();
            ImportEntry imp = null;

            #region Imports

            for (int i = CurrentLoadedExport.FileRef.Imports.Count - 1; i >= 0; i--)
            {
                imp = CurrentLoadedExport.FileRef.Imports[i];
                if (onlyOfType != null)
                {
                    if (imp.IsClass)
                    {
                        if ((onlyOfType == @"Class" && imp.ClassName == @"Class") || imp.ClassName == onlyOfType || imp.InheritsFrom(onlyOfType))
                        {
                            allEntriesNew.Add(imp);
                            continue;
                        }
                    }
                    else if (imp.IsA(onlyOfType))
                    {
                        allEntriesNew.Add(imp);
                        continue;
                    }
                }
                else
                {
                    allEntriesNew.Add(imp);
                }
            }

            #endregion
            allEntriesNew.Add(ZeroUIndexClassEntry.Instance);

            #region Exports
            foreach (ExportEntry exp in CurrentLoadedExport.FileRef.Exports)
            {
                if (onlyOfType != null)
                {
                    if (exp.IsClass)
                    {
                        if (onlyOfType == @"Class" || exp.InheritsFrom(onlyOfType))
                        {
                            allEntriesNew.Add(exp);
                            continue;
                        }
                    }
                    else if (exp.IsA(onlyOfType))
                    {
                        allEntriesNew.Add(exp);
                        continue;
                    }
                }
                else
                {
                    allEntriesNew.Add(exp);
                }
            }
            #endregion
            return allEntriesNew;
        }

        private void UpdateParsedEditorValue(UPropertyTreeViewEntry treeViewEntry = null)
        {
            UPropertyTreeViewEntry tvi = treeViewEntry ?? SelectedItem;
            if (tvi?.Property != null)
            {
                switch (tvi.Property)
                {
                    case IntProperty _:
                        if (tvi.Parent?.Property is StructProperty property && property.StructType == "Rotator")
                        {
                            //yes it is a float - we convert raw value to floating point degrees so we use float to raw int
                            if (float.TryParse(Value_TextBox.Text, out float degrees))
                            {
                                ParsedValue_TextBlock.Text = $"{degrees.DegreesToUnrealRotationUnits()} (raw value)";
                            }
                            else
                            {
                                ParsedValue_TextBlock.Text = "Invalid value";
                            }
                        }
                        break;
                    case DelegateProperty _:
                    case ObjectProperty _:
                        {
                            if (int.TryParse(Value_TextBox.Text, out int index))
                            {
                                if (index == 0)
                                {
                                    ParsedValue_TextBlock.Text = "Null";
                                }
                                else
                                {
                                    var entry = Pcc.GetEntry(index);
                                    if (entry != null)
                                    {
                                        ParsedValue_TextBlock.Text = entry.InstancedFullPath;
                                    }
                                    else
                                    {
                                        ParsedValue_TextBlock.Text = "Index out of bounds of entry list";
                                    }
                                }
                            }
                            else
                            {
                                ParsedValue_TextBlock.Text = "Invalid value";
                            }
                        }
                        break;
                    case StringRefProperty _:
                        {
                            if (int.TryParse(Value_TextBox.Text, out int index))
                            {
                                string str = TLKManagerWPF.GlobalFindStrRefbyID(index, CurrentLoadedExport.FileRef.Game, CurrentLoadedExport.FileRef);
                                str = str?.Replace("\n", "[\\n]");
                                if (str?.Length > 82)
                                {
                                    str = str.Substring(0, 80) + "...";
                                }
                                ParsedValue_TextBlock.Text = str?.Replace(Environment.NewLine, "[\\n]");
                            }
                            else
                            {
                                ParsedValue_TextBlock.Text = "Invalid value";
                            }
                        }
                        break;
                    case NameProperty _:
                        {
                            if (int.TryParse(Value_TextBox.Text, out int index) && int.TryParse(NameIndex_TextBox.Text, out int number))
                            {
                                if (index >= 0 && index < Pcc.Names.Count)
                                {
                                    //ParsedValue_TextBlock.Text = Pcc.getNameEntry(index) + "_" + number;
                                }
                                else
                                {
                                    ParsedValue_TextBlock.Text = "Name index out of range";
                                }
                            }
                            else
                            {
                                ParsedValue_TextBlock.Text = "Invalid value(s)";
                            }
                        }
                        break;
                }
            }
        }

        private void UpdateHexboxPosition(UPropertyTreeViewEntry newSelectedItem)
        {
            if (newSelectedItem?.Property != null && Interpreter_Hexbox != null)
            {
                var hexPos = newSelectedItem.Property.ValueOffset;
                Interpreter_Hexbox.SelectionStart = hexPos;
                Interpreter_Hexbox.SelectionLength = 1; //maybe change

                Interpreter_Hexbox.UnhighlightAll();

                if (CurrentLoadedExport?.ClassName != "Class")
                {
                    if (newSelectedItem.Property is StructProperty structProp)
                    {
                        //New selected property is struct property

                        switch (newSelectedItem.Parent.Property)
                        {
                            //If we are in an array
                            /* || newSelectedItem.Parent.Property is ArrayProperty<EnumProperty>*/
                            case ArrayProperty<StructProperty> _:
                                Interpreter_Hexbox.Highlight(structProp.StartOffset, structProp.GetLength(Pcc, true));
                                return;
                            case StructProperty structParentProp:
                                Interpreter_Hexbox.Highlight(structProp.StartOffset, structProp.GetLength(Pcc, structParentProp.IsImmutable));
                                return;
                            default:
                                Interpreter_Hexbox.Highlight(structProp.StartOffset, structProp.GetLength(Pcc));
                                break;
                        }
                    }
                    else if (newSelectedItem.Property is ArrayPropertyBase arrayProperty)
                    {
                        if (newSelectedItem.Parent.Property is StructProperty { IsImmutable: true })
                        {
                            Interpreter_Hexbox.Highlight(arrayProperty.StartOffset, arrayProperty.GetLength(Pcc, true));
                        }
                        else
                        {
                            Interpreter_Hexbox.Highlight(arrayProperty.StartOffset, arrayProperty.GetLength(Pcc));
                        }
                        return;
                    }

                    switch (newSelectedItem.Property)
                    {
                        //case NoneProperty np:
                        //    Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset - 4, 8);
                        //    return;
                        //case StructProperty sp:
                        //    break;
                        case ObjectProperty:
                        case FloatProperty:
                        case IntProperty:
                            {
                                switch (newSelectedItem.Parent.Property)
                                {
                                    case StructProperty { IsImmutable: true }:
                                    case ArrayProperty<IntProperty>:
                                    case ArrayProperty<FloatProperty>:
                                    case ArrayProperty<ObjectProperty>:
                                        Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset, 4);
                                        return;
                                    default:
                                        Interpreter_Hexbox.Highlight(newSelectedItem.Property.StartOffset, newSelectedItem.Property.ValueOffset + 4 - newSelectedItem.Property.StartOffset);
                                        break;
                                }
                                break;
                            }
                        case EnumProperty ep:
                            {
                                switch (newSelectedItem.Parent.Property)
                                {
                                    case StructProperty { IsImmutable: true }:
                                        Interpreter_Hexbox.Highlight(ep.ValueOffset, 8);
                                        return;
                                    case ArrayProperty<EnumProperty>:
                                        Interpreter_Hexbox.Highlight(ep.ValueOffset, 8);
                                        return;
                                    default:
                                        Interpreter_Hexbox.Highlight(ep.StartOffset, ep.GetLength(Pcc));
                                        return;
                                }
                            }
                        case ByteProperty bp:
                            {
                                if (newSelectedItem.Parent.Property is StructProperty { IsImmutable: true } or ArrayProperty<ByteProperty>)
                                {
                                    Interpreter_Hexbox.Highlight(bp.ValueOffset, 1);
                                    return;
                                }
                                break;
                            }
                        case BioMask4Property b4p:
                            {
                                if (newSelectedItem.Parent.Property is StructProperty { IsImmutable: true } or ArrayProperty<BioMask4Property>)
                                {
                                    Interpreter_Hexbox.Highlight(b4p.ValueOffset, 1);
                                    return;
                                }
                                break;
                            }
                        case StrProperty sp:
                            {
                                if (newSelectedItem.Parent.Property is StructProperty { IsImmutable: true } or ArrayProperty<StrProperty>)
                                {
                                    Interpreter_Hexbox.Highlight(sp.StartOffset, sp.GetLength(Pcc, true));
                                    return;
                                }
                                Interpreter_Hexbox.Highlight(sp.StartOffset, sp.GetLength(Pcc));
                                break;
                            }
                        case BoolProperty boolp:
                            {
                                if (newSelectedItem.Parent.Property is StructProperty { IsImmutable: true })
                                {
                                    Interpreter_Hexbox.Highlight(boolp.ValueOffset, 1);
                                    return;
                                }
                                Interpreter_Hexbox.Highlight(boolp.StartOffset, boolp.ValueOffset - boolp.StartOffset);
                                break;
                            }
                        case NameProperty np:
                            {
                                if (newSelectedItem.Parent.Property is StructProperty { IsImmutable: true } or ArrayProperty<NameProperty>)
                                {
                                    Interpreter_Hexbox.Highlight(np.ValueOffset, 8);
                                    return;
                                }
                                Interpreter_Hexbox.Highlight(np.StartOffset, np.ValueOffset + 8 - np.StartOffset);
                                break;
                            }
                        case DelegateProperty dp:
                            {
                                if (newSelectedItem.Parent.Property is StructProperty { IsImmutable: true } or ArrayProperty<DelegateProperty>)
                                {
                                    Interpreter_Hexbox.Highlight(dp.ValueOffset, 12);
                                    return;
                                }
                                Interpreter_Hexbox.Highlight(dp.StartOffset, dp.ValueOffset + 12 - dp.StartOffset);
                                break;
                            }
                        case StringRefProperty srefp:
                            {
                                if (newSelectedItem.Parent.Property is StructProperty { IsImmutable: true } or ArrayProperty<StringRefProperty>)
                                {
                                    Interpreter_Hexbox.Highlight(srefp.ValueOffset, 4);
                                    return;
                                }
                                Interpreter_Hexbox.Highlight(srefp.StartOffset, srefp.GetLength(Pcc));
                            }
                            break;
                        case NoneProperty nonep:
                            Interpreter_Hexbox.Highlight(nonep.StartOffset, 8);
                            break;
                            //case EnumProperty ep:
                            //    Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset - 32, newSelectedItem.Property.GetLength(Pcc));
                            //    return;
                    }

                    //if (CurrentLoadedExport.ClassName != "Class")
                    //{
                    //    if (newSelectedItem.Property is StructProperty && newSelectedItem.Parent.Property is ArrayProperty<StructProperty> || newSelectedItem.Parent.Property is ArrayProperty<EnumProperty>)
                    //    {
                    //        Interpreter_Hexbox.Highlight(newSelectedItem.Property.StartOffset, newSelectedItem.Property.GetLength(Pcc, true));
                    //    }
                    //    else
                    //    {
                    //        Interpreter_Hexbox.Highlight(newSelectedItem.Property.StartOffset, newSelectedItem.Property.GetLength(Pcc));
                    //    }
                    //}
                    //else if (newSelectedItem.Parent.Property is ArrayProperty<IntProperty> || newSelectedItem.Parent.Property is ArrayProperty<FloatProperty> || newSelectedItem.Parent.Property is ArrayProperty<ObjectProperty>)
                    //{
                    //    Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset, 4);
                    //    return;
                    //}
                    //else if (newSelectedItem.Property is StructProperty)
                    //{
                    //    Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset - 40, newSelectedItem.Property.GetLength(Pcc));
                    //    return;
                    //}
                    //else
                    //{
                    //}
                    //array children
                    /*if (newSelectedItem.Parent.Property != null && newSelectedItem.Parent.Property.PropType == PropertyType.ArrayProperty)
                    {
                        if (newSelectedItem.Property is NameProperty np)
                        {
                            Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset, 8);
                        }
                        else
                        {
                            Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset, 4);
                        }
                        return;
                    }

                    else if (newSelectedItem.Parent.Property != null && newSelectedItem.Parent.Property.PropType == PropertyType.StructProperty)
                    {
                        //Determine if it is immutable or not, somehow.
                    }

                    else if (newSelectedItem.Property is StructProperty sp)
                    {
                        //struct size highlighting... not sure how to do this with this little info
                    }
                    else if (newSelectedItem.Property is NameProperty np)
                    {
                        Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset - 24, 32);
                    }
                    else if (newSelectedItem.Property is BoolProperty bp)
                    {
                        Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset - 24, 25);
                    }
                    else if (newSelectedItem.Parent.PropertyType != PropertyType.ArrayProperty.ToString())
                    {
                        Interpreter_Hexbox.Highlight(newSelectedItem.Property.ValueOffset - 24, 28);
                    }*/
                }
                Interpreter_Hexbox_Host.UpdateLayout();
                Interpreter_Hexbox.Invalidate();
            }
        }

        private void Interpreter_Loaded(object sender, RoutedEventArgs e)
        {
            Interpreter_Hexbox = (HexBox)Interpreter_Hexbox_Host.Child;
            Interpreter_Hexbox.ByteProvider ??= new ReadOptimizedByteProvider();
            //remove in the event this object is reloaded again
            Interpreter_Hexbox.ByteProvider.Changed -= Interpreter_Hexbox_BytesChanged;
            Interpreter_Hexbox.ByteProvider.Changed += Interpreter_Hexbox_BytesChanged;

            Interpreter_Hexbox.SelectionStartChanged -= hb1_SelectionChanged;
            Interpreter_Hexbox.SelectionLengthChanged -= hb1_SelectionChanged;

            Interpreter_Hexbox.SelectionStartChanged += hb1_SelectionChanged;
            Interpreter_Hexbox.SelectionLengthChanged += hb1_SelectionChanged;

            // ??
            this.bind(HexBoxMinWidthProperty, Interpreter_Hexbox, nameof(Interpreter_Hexbox.MinWidth));
            this.bind(HexBoxMaxWidthProperty, Interpreter_Hexbox, nameof(Interpreter_Hexbox.MaxWidth));
        }

        private void Interpreter_Hexbox_BytesChanged(object sender, EventArgs e)
        {
            if (!isLoadingNewData)
            {
                HasUnsavedChanges = true;
            }
        }

        private void Interpreter_SaveHexChanges()
        {
            if (Interpreter_Hexbox.ByteProvider is ReadOptimizedByteProvider provider)
            {
                CurrentLoadedExport.Data = provider.Span.ToArray();
            }
        }

        public override bool CanParse(ExportEntry exportEntry)
        {
            return true;
        }

        private void SetValue_Click(object sender, RoutedEventArgs args)
        {
            //todo: set value
            bool updated = false;
            if (SelectedItem is UPropertyTreeViewEntry tvi && tvi.Property != null)
            {
                Property property = tvi.Property;
                switch (property)
                {
                    case IntProperty ip:
                        {
                            //scoped for variable re-use

                            //ROTATORS
                            if (tvi.Parent?.Property is StructProperty structProperty && structProperty.StructType == "Rotator")
                            {
                                //yes it is a float - we convert raw value to floating point degrees so we use float to raw int
                                if (float.TryParse(Value_TextBox.Text, out float degrees))
                                {
                                    ip.Value = degrees.DegreesToUnrealRotationUnits();
                                    updated = true;
                                }
                            }
                            // WwiseEvents are unsigned integers, this allows entering the value
                            else if (CurrentLoadedExport != null && CurrentLoadedExport.ClassName is "WwiseEvent" or "WwiseBank"
                                                                   && uint.TryParse(Value_TextBox.Text, out uint ui)
                                                                   && ui != ip.Value)
                            {
                                ip.Value = (int)ui;
                                updated = true;
                            }
                            else if (int.TryParse(Value_TextBox.Text, out int i) && i != ip.Value)
                            {
                                ip.Value = i;
                                updated = true;
                            }
                        }
                        break;
                    case FloatProperty fp:
                        {
                            if (float.TryParse(Value_TextBox.Text, out float f) && f != fp.Value)
                            {
                                fp.Value = f;
                                updated = true;
                            }
                        }
                        break;
                    case BoolProperty bp:
                        if (bp.Value != (Value_ComboBox.SelectedIndex == 0))
                        {
                            bp.Value = Value_ComboBox.SelectedIndex == 0; //0 = true
                            updated = true;
                        }
                        break;
                    case StrProperty sp:
                        if (sp.Value != Value_TextBox.Text)
                        {
                            sp.Value = Value_TextBox.Text;
                            updated = true;
                        }
                        break;
                    case StringRefProperty srp:
                        {
                            if (int.TryParse(Value_TextBox.Text, out int s) && s != srp.Value)
                            {
                                srp.Value = s;
                                updated = true;
                            }
                        }
                        break;
                    case ObjectProperty op:
                        {
                            if (Value_ObjectComboBox.SelectedItem is IEntry ie)
                            {
                                op.Value = ie.UIndex;
                                updated = true;
                            }
                            else if (Value_ObjectComboBox.SelectedItem is ZeroUIndexClassEntry)
                            {
                                op.Value = 0;
                                updated = true;
                            }
                            // This is old implementation; switched over 07/23/2023
                            /*
                                                        if (int.TryParse(Value_TextBox.Text, out int o) && o != op.Value && (Pcc.IsEntry(o) || o == 0))
                                                        {
                                                            op.Value = o;
                                                            updated = true;
                                                        }
                             */

                        }
                        break;
                    case EnumProperty ep:
                        if (ep.Value != (NameReference)Value_ComboBox.SelectedItem)
                        {
                            ep.Value = (NameReference)Value_ComboBox.SelectedItem; //0 = true
                            updated = true;
                        }
                        break;
                    case ByteProperty bytep:
                        if (byte.TryParse(Value_TextBox.Text, out byte b) && b != bytep.Value)
                        {
                            bytep.Value = b;
                            updated = true;
                        }
                        break;
                    case BioMask4Property b4p:
                        if (byte.TryParse(Value_TextBox.Text, out byte b4) && b4 != b4p.Value)
                        {
                            b4p.Value = b4;
                            updated = true;
                        }
                        break;
                    case NameProperty namep:
                        {
                            //get string
                            string input = Value_ComboBox.Text;
                            var index = Pcc.findName(input);
                            if (index == -1)
                            {
                                //couldn't find name
                                if (MessageBoxResult.No == MessageBox.Show($"{Path.GetFileName(Pcc.FilePath)} does not contain the Name: {input}\nWould you like to add it to the Name list?", "Name not found", MessageBoxButton.YesNo))
                                {
                                    break;
                                }

                                index = Pcc.FindNameOrAdd(input);
                                //Wait for namelist to update. we may need to set a timer here.
                                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
                            }

                            bool nameindexok = int.TryParse(NameIndex_TextBox.Text, out int nameIndex);
                            nameindexok &= nameIndex >= 0;
                            if (index >= 0 && nameindexok)
                            {
                                namep.Value = new NameReference(input, nameIndex);
                                updated = true;
                            }
                            break;
                        }
                    case DelegateProperty delp:
                        if (int.TryParse(Value_TextBox.Text, out int _o) && (Pcc.IsEntry(_o) || _o == 0))
                        {
                            //get string
                            string input = Value_ComboBox.Text;
                            var index = Pcc.findName(input);
                            if (index == -1)
                            {
                                //couldn't find name
                                if (MessageBoxResult.No == MessageBox.Show($"{Path.GetFileName(Pcc.FilePath)} does not contain the Name: {input}\nWould you like to add it to the Name list?", "Name not found", MessageBoxButton.YesNo))
                                {
                                    break;
                                }

                                index = Pcc.FindNameOrAdd(input);
                                //Wait for namelist to update. we may need to set a timer here.
                                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
                            }

                            bool nameindexok = int.TryParse(NameIndex_TextBox.Text, out int nameIndex);
                            nameindexok &= nameIndex >= 0;
                            if (index >= 0 && nameindexok)
                            {
                                delp.Value = new ScriptDelegate(_o, new NameReference(input, nameIndex));
                                updated = true;
                            }
                        }
                        break;
                    default:
                        MessageBox.Show("This type cannot be set, please pester Mgamerz to fix this: " + property);
                        break;
                }
                //PropertyCollection props = CurrentLoadedExport.GetProperties();
                //props.Remove(tag);

                if (updated)
                {
                    //will cause a refresh from packageeditor
                    CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);
                }
                //StartScan();
            }
        }

        private void ValueTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                SetValue_Click(null, null);
            }
        }

        private void Value_TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateParsedEditorValue();
        }

        private void AddArrayElement()
        {
            if (Interpreter_TreeView.SelectedItem is UPropertyTreeViewEntry tvi && tvi.Property != null)
            {
                string containingType = CurrentLoadedExport.ClassName;
                ArrayPropertyBase propertyToAddItemTo;
                int insertIndex;
                if (tvi.Property is ArrayPropertyBase arrayProperty)
                {
                    propertyToAddItemTo = arrayProperty;
                    insertIndex = arrayProperty.Count;
                    ArrayElementJustAdded = true;
                    if (tvi.Parent?.Property is StructProperty structProp)
                    {
                        containingType = structProp.StructType;
                    }
                }
                else if (tvi.Parent?.Property is ArrayPropertyBase parentArray)
                {
                    propertyToAddItemTo = parentArray;
                    insertIndex = tvi.Parent.ChildrenProperties.IndexOf(tvi);
                    ForcedRescanOffset = (int)tvi.Property.StartOffset;
                    if (tvi.Parent.Parent?.Property is StructProperty structProp)
                    {
                        containingType = structProp.StructType;
                    }
                }
                else
                {
                    return;
                }

                switch (propertyToAddItemTo)
                {
                    case ArrayProperty<NameProperty> anp:
                        NameProperty np = new NameProperty("None");
                        anp.Insert(insertIndex, np);
                        break;
                    case ArrayProperty<ObjectProperty> aop:
                        aop.Insert(insertIndex, new ObjectProperty(0));
                        break;
                    case ArrayProperty<DelegateProperty> aop:
                        aop.Insert(insertIndex, new DelegateProperty("None", 0));
                        break;
                    case ArrayProperty<EnumProperty> aep:
                        {
                            PropertyInfo p = GlobalUnrealObjectInfo.GetPropertyInfo(Pcc.Game, aep.Name, containingType);
                            string typeName = p.Reference;
                            EnumProperty ep = new EnumProperty(typeName, Pcc.Game);
                            aep.Insert(insertIndex, ep);
                        }
                        break;
                    case ArrayProperty<IntProperty> aip:
                        aip.Insert(insertIndex, new IntProperty(0));
                        break;
                    case ArrayProperty<FloatProperty> afp:
                        FloatProperty fp = new FloatProperty(0);
                        afp.Insert(insertIndex, fp);
                        break;
                    case ArrayProperty<StrProperty> asp:
                        asp.Insert(insertIndex, new StrProperty("Empty String"));
                        break;
                    case ArrayProperty<BoolProperty> abp:
                        abp.Insert(insertIndex, new BoolProperty(false));
                        break;
                    case ArrayProperty<StringRefProperty> astrf:
                        astrf.Insert(insertIndex, new StringRefProperty());
                        break;
                    case ArrayProperty<ByteProperty> abyte:
                        abyte.Insert(insertIndex, new ByteProperty(0));
                        break;
                    case ArrayProperty<BioMask4Property> ab4:
                        ab4.Insert(insertIndex, new BioMask4Property(0));
                        break;
                    case ArrayProperty<StructProperty> astructp:
                        {
                            if (astructp.Count > 0)
                            {
                                astructp.Insert(insertIndex, astructp.Last()); //Bad form, but writing and reparse will correct it
                                break;
                            }

                            PropertyInfo p = GlobalUnrealObjectInfo.GetPropertyInfo(Pcc.Game, astructp.Name, containingType);
                            if (p == null)
                            {
                                //Attempt dynamic lookup
                                if (!(CurrentLoadedExport.Class is ExportEntry exportToBuildFor))
                                {
                                    exportToBuildFor = CurrentLoadedExport;
                                }
                                ClassInfo classInfo = GlobalUnrealObjectInfo.generateClassInfo(exportToBuildFor);
                                p = GlobalUnrealObjectInfo.GetPropertyInfo(Pcc.Game, astructp.Name, containingType, classInfo);
                            }
                            if (p != null)
                            {
                                string typeName = p.Reference;
                                PropertyCollection props = GlobalUnrealObjectInfo.getDefaultStructValue(Pcc.Game, typeName, true);
                                var isInImmutable = IsInImmutable(tvi);
                                astructp.Insert(insertIndex, new StructProperty(typeName, props, isImmutable: isInImmutable || GlobalUnrealObjectInfo.IsImmutable(typeName, Pcc.Game)));
                            }
                        }
                        break;
                    default:
                        MessageBox.Show("Can't add this property type yet.\nPlease pester Mgamerz to get it implemented");
                        ArrayElementJustAdded = false;
                        return;
                }

                CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);
            }
        }

        private bool IsInImmutable(UPropertyTreeViewEntry tvi)
        {
            if (tvi?.Property == null)
                return false; // The root

            if (tvi.Property is StructProperty sp && sp.IsImmutable)
                return true;

            return IsInImmutable(tvi.Parent);
        }

        private void RemoveArrayElement()
        {
            if (SelectedItem is UPropertyTreeViewEntry tvi
             && tvi.Parent?.Property is ArrayPropertyBase arrayProperty)
            {
                int index = tvi.Parent.ChildrenProperties.IndexOf(tvi);
                if (index >= 0)
                {
                    if (arrayProperty.Properties.HasExactly(1))
                    {
                        ForcedRescanOffset = (int)arrayProperty.StartOffset;
                    }
                    else
                    {
                        ForcedRescanOffset = (int)arrayProperty[index == arrayProperty.Count - 1 ? index - 1 : index].StartOffset;
                    }
                    arrayProperty.RemoveAt(index);

                    CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);
                }
                else
                {
                    Debug.WriteLine("DIDN'T REMOVE ANYTHING!");
                }
            }
        }

        public override void Dispose()
        {
            Settings.StaticPropertyChanged -= SettingChanged;

            if (Interpreter_Hexbox != null)
            {
                if (Interpreter_Hexbox.ByteProvider != null)
                {
                    Interpreter_Hexbox.ByteProvider.Changed -= Interpreter_Hexbox_BytesChanged;
                }
                Interpreter_Hexbox.SelectionLengthChanged -= hb1_SelectionChanged;
            }

            Interpreter_Hexbox = null;
            Interpreter_Hexbox_Host?.Child.Dispose();
            Interpreter_Hexbox_Host?.Dispose();
            Interpreter_Hexbox_Host = null;
            CurrentLoadedExport = null;
            //needed because wpf controls can take a loong time to get garbage collected,
            //so we need to sever all links to the IMEPackage immediately if we want it to be cleaned up in a timely fashion
            ClearTree(PropertyNodes);
            Interpreter_TreeView = null;

            static void ClearTree(ObservableCollectionExtended<UPropertyTreeViewEntry> treeViewEntries)
            {
                foreach (UPropertyTreeViewEntry tve in treeViewEntries)
                {
                    tve.AttachedExport = null;
                    if (tve.ChildrenProperties.Count > 0)
                    {
                        ClearTree(tve.ChildrenProperties);
                    }
                }
            }
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                var elhw = new ExportLoaderHostedWindow(new InterpreterExportLoader(), CurrentLoadedExport)
                {
                    Title = $"Properties - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {Pcc.FilePath}"
                };
                elhw.Show();
            }
        }

        private void MoveArrayElement(bool up)
        {
            if (SelectedItem is UPropertyTreeViewEntry tvi && tvi.Property != null && tvi.Parent?.Property is ArrayPropertyBase arrayProperty)
            {
                int index = tvi.Parent.ChildrenProperties.IndexOf(tvi);
                int swapIndex = up ? index - 1 : index + 1;
                //selection should be changed to the location of the element we are swapping with
                ForcedRescanOffset = (int)arrayProperty[swapIndex].StartOffset;
                arrayProperty.SwapElements(index, swapIndex);
                //Will force reload
                CurrentLoadedExport.WriteProperties(CurrentLoadedProperties);
            }
        }

        private void ColorPicker_Closed(object sender, RoutedEventArgs e)
        {
            Interpreter_SaveHexChanges();
        }
    }

    [DebuggerDisplay("UPropertyTreeViewEntry | {" + nameof(DisplayName) + "}")]
    public class UPropertyTreeViewEntry : NotifyPropertyChangedBase, ITreeItem
    {
        public bool HasTooManyChildrenToDisplay { get; set; }
        public ExportEntry AttachedExport;
        private string _colorStructCode;
        /// <summary>
        /// This property is used as a databinding workaround for when colorpicker is used as we can't convert back with a reference to the struct.
        /// </summary>
        public string ColorStructCode
        {
            get
            {
                if (Property is StructProperty colorStruct)
                {
                    if (colorStruct.StructType == "Color")
                    {
                        if (_colorStructCode != null) return _colorStructCode;

                        var a = colorStruct.GetProp<ByteProperty>("A").Value;
                        var r = colorStruct.GetProp<ByteProperty>("R").Value;
                        var g = colorStruct.GetProp<ByteProperty>("G").Value;
                        var b = colorStruct.GetProp<ByteProperty>("B").Value;

                        return _colorStructCode = $"#{a:X2}{r:X2}{g:X2}{b:X2}";
                    }
                    if (colorStruct.StructType == "LinearColor")
                    {
                        if (_colorStructCode != null) return _colorStructCode;

                        var a = colorStruct.GetProp<FloatProperty>("A").Value;
                        var r = colorStruct.GetProp<FloatProperty>("R").Value;
                        var g = colorStruct.GetProp<FloatProperty>("G").Value;
                        var b = colorStruct.GetProp<FloatProperty>("B").Value;

                        static int ToByteColor(float f) => ((int)(f * 255)).Clamp(0, 255);

                        return _colorStructCode = $"#{ToByteColor(a):X2}{ToByteColor(r):X2}{ToByteColor(g):X2}{ToByteColor(b):X2}";
                    }
                }

                return null;
            }
            set
            {
                if (_colorStructCode != value && ColorConverter.ConvertFromString(value) is Color newColor)
                {
                    if (Property is StructProperty { StructType: "Color" } colorStruct)
                    {
                        var a = colorStruct.GetProp<ByteProperty>("A");
                        var r = colorStruct.GetProp<ByteProperty>("R");
                        var g = colorStruct.GetProp<ByteProperty>("G");
                        var b = colorStruct.GetProp<ByteProperty>("B");
                        a.Value = newColor.A;
                        r.Value = newColor.R;
                        g.Value = newColor.G;
                        b.Value = newColor.B;

                        _colorStructCode = value;
                        OnPropertyChanged();
                    }
                    else if (Property is StructProperty { StructType: "LinearColor" } linColStruct)
                    {
                        var a = linColStruct.GetProp<FloatProperty>("A");
                        var r = linColStruct.GetProp<FloatProperty>("R");
                        var g = linColStruct.GetProp<FloatProperty>("G");
                        var b = linColStruct.GetProp<FloatProperty>("B");
                        a.Value = newColor.A / 255f;
                        r.Value = newColor.R / 255f;
                        g.Value = newColor.G / 255f;
                        b.Value = newColor.B / 255f;

                        _colorStructCode = value;
                        OnPropertyChanged();
                    }
                }
            }
        }

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set => SetProperty(ref isSelected, value);
        }

        private bool isExpanded;
        public bool IsExpanded
        {
            get => isExpanded;
            set => SetProperty(ref isExpanded, value);
        }

        /// <summary>
        /// Used for inline editing. Return true to allow inline editing for property type
        /// </summary>
        public bool EditableType
        {
            get
            {
                if (Property == null) return false;

                switch (Property)
                {
                    //case BoolProperty _:
                    //case ByteProperty _:
                    case FloatProperty _:
                    case IntProperty _:
                    //case NameProperty _:
                    //case StringRefProperty _:
                    case StrProperty _:
                        //case ObjectProperty _:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public void ExpandParents()
        {
            if (Parent != null)
            {
                Parent.ExpandParents();
                Parent.IsExpanded = true;
            }
        }

        /// <summary>
        /// Flattens the tree into depth first order. Use this method for searching the list.
        /// </summary>
        /// <returns></returns>
        public List<UPropertyTreeViewEntry> FlattenTree()
        {
            var nodes = new List<UPropertyTreeViewEntry> { this };
            foreach (UPropertyTreeViewEntry tve in ChildrenProperties)
            {
                nodes.AddRange(tve.FlattenTree());
            }
            return nodes;
        }

        public event EventHandler PropertyUpdated;

        public UPropertyTreeViewEntry Parent { get; set; }

        /// <summary>
        /// The UProperty object from the export's properties that this node represents
        /// </summary>
        public Property Property { get; set; }

        /// <summary>
        /// List of children properties that link to this node.
        /// Only Struct and ArrayProperties will have this populated.
        /// </summary>
        public ObservableCollectionExtended<UPropertyTreeViewEntry> ChildrenProperties { get; } = [];
        public UPropertyTreeViewEntry(Property property, string displayName = null)
        {
            Property = property;
            DisplayName = displayName;
        }

        public UPropertyTreeViewEntry()
        {
        }

        private string _editableValue;
        public string EditableValue
        {
            get => _editableValue ?? "";
            set
            {
                //Todo: Write property value here
                if (_editableValue != null && _editableValue != value)
                {
                    switch (Property)
                    {
                        case IntProperty intProp:
                            if (int.TryParse(value, out int parsedIntVal))
                            {
                                intProp.Value = parsedIntVal;
                                PropertyUpdated?.Invoke(this, EventArgs.Empty);
                                HasChanges = true;
                            }
                            else
                            {
                                return;
                            }
                            break;
                        case FloatProperty floatProp:
                            if (float.TryParse(value, out float parsedFloatVal))
                            {
                                floatProp.Value = parsedFloatVal;
                                PropertyUpdated?.Invoke(this, EventArgs.Empty);
                                HasChanges = true;
                            }
                            else
                            {
                                return;
                            }
                            break;
                    }
                }
                _editableValue = value;
            }
        }

        private bool _hasChanges;
        public bool HasChanges
        {
            get => _hasChanges;
            set => SetProperty(ref _hasChanges, value);
        }

        private string _displayName;
        public string DisplayName
        {
            get => _displayName ?? "DisplayName for this UPropertyTreeViewItem is null!";
            set => SetProperty(ref _displayName, value);
        }

        public bool IsUpdatable = false; //set to

        private string _parsedValue;
        public string ParsedValue
        {
            get => _parsedValue ?? "";
            set => SetProperty(ref _parsedValue, value);
        }

        /// <summary>
        /// For UI binding only (as it can return EnumProperty)
        /// </summary>
        public string RawPropertyType
        {
            get
            {
                if (Property is EnumProperty ep)
                {
                    return @"EnumProperty";
                }
                if (Property != null)
                {
                    return Property.PropType.ToString();
                }
                return "Currently loaded export";
            }
        }

        public string PropertyType
        {
            get
            {
                if (Property != null)
                {
                    switch (Property)
                    {
                        case ArrayPropertyBase arrayProp:
                            {
                                //we don't have reference to current pcc so we cannot look this up at this time.
                                //return $"ArrayProperty({(Property as ArrayProperty).arrayType})";
                                int count = arrayProp.Count;
                                return $"ArrayProperty - {count} item{(count != 1 ? "s" : "")}";
                            }
                        case StructProperty sp:
                            return $"{(sp.IsImmutable ? "Immutable " : "")}StructProperty({sp.StructType})";
                        case ObjectProperty op:
                            string propType = op.InternalPropType.ToString();
                            if (op.Name.Name != null)
                            {
                                string container = AttachedExport.ClassName;
                                if (Parent?.Property is StructProperty psp)
                                {
                                    container = psp.StructType;
                                }

                                var type = GlobalUnrealObjectInfo.GetPropertyInfo(AttachedExport.Game, op.Name, container, containingExport: AttachedExport);
                                if (type != null)
                                {
                                    return $"{propType} ({type.Reference})";
                                }
                                else
                                {
                                    return $"{propType} (???)";
                                }
                            }
                            else
                            {
                                return propType;
                            }

                        case EnumProperty ep:
                            return $"EnumProperty ({ep.EnumType})";
                        default:
                            return Property.PropType.ToString();
                    }
                }
                if (AdvancedModeText != null)
                {
                    return AdvancedModeText;
                }
                return "Currently loaded export";
                //string type = UIndex < 0 ? "Imp" : "Exp";
                //return $"({type}) {UIndex} {Entry.ObjectName}({Entry.ClassName})"; */
            }
            //set { _displayName = value; }
        }

        private string _advancedModeText;
        public string AdvancedModeText
        {
            get => _advancedModeText;
            internal set
            {
                SetProperty(ref _advancedModeText, value);
                OnPropertyChanged(nameof(AdvancedModeText));
            }
        }
        public void PrintPretty(string indent, TextWriter str, bool last, ExportEntry associatedExport)
        {
            bool supressNewLine = false;
            if (Property != null)
            {
                str.Write(indent);
                if (last)
                {
                    str.Write("└─");
                    indent += "  ";
                }
                else
                {
                    str.Write("├─");
                    indent += "| ";
                }
                //if (Parent != null && Parent == )
                str.Write(Property.Name + ": " + EditableValue);// + " "  " (" + PropertyType + ")");

                switch (Property)
                {
                    case DelegateProperty dp:
                        str.Write($" {ParsedValue}");
                        break;
                    case ObjectProperty op:
                        {
                            //Resolve
                            string objectName = associatedExport.FileRef.GetEntryString(op.Value);
                            str.Write("  " + objectName);
                            break;
                        }
                    case NameProperty np:
                        //Resolve
                        str.Write($"  {np.Value}_{np.Value.Number}");
                        break;
                    case StringRefProperty srp:
                        {
                            if (associatedExport.FileRef.Game == MEGame.ME1)
                            {
                                //string objectName = associatedExport.FileRef.GetEntryString(srp.Value);
                                //if (InterpreterExportLoader.ME1TalkFiles == null)
                                //{
                                //    InterpreterExportLoader.ME1TalkFiles = new ME1Explorer.TalkFiles();
                                //}
                                //ParsedValue = InterpreterExportLoader.ME1TalkFiles.findDataById(srp.Value);
                                str.Write(" " + ParsedValue);
                            }

                            break;
                        }
                }

                bool isArrayPropertyTable = Property is ArrayPropertyBase;
                if (ChildrenProperties.Count > 1000 && isArrayPropertyTable)
                {
                    str.Write($" is very large array ({ChildrenProperties.Count} items) - skipping");
                    return;
                }

                if (Property.Name.Name is "CompressedTrackOffsets" or "LookupTable")
                {
                    str.Write(" - suppressed by data dumper.");
                    return;
                }
            }
            else
            {
                supressNewLine = true;
            }
            for (int i = 0; i < ChildrenProperties.Count; i++)
            {
                if (ChildrenProperties[i].Property is NoneProperty)
                {
                    continue;
                }
                if (!supressNewLine)
                {
                    str.Write("\n");
                }
                else
                {
                    supressNewLine = false;
                }
                ChildrenProperties[i].PrintPretty(indent, str, i == ChildrenProperties.Count - 1 || (i == ChildrenProperties.Count - 2 && ChildrenProperties[^1].Property is NoneProperty), associatedExport);
            }
        }

        public override string ToString()
        {
            return "UPropertyTreeViewEntry " + DisplayName;
        }
    }
}
