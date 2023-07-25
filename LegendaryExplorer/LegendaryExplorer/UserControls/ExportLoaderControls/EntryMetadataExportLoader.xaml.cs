using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Be.Windows.Forms;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using Xceed.Wpf.Toolkit.Primitives;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for MetadataEditorWPF.xaml
    /// </summary>
    public partial class EntryMetadataExportLoader : ExportLoaderControl
    {
        //This is a ExportLoaderControl as it can technically function as one. It can also function as an ImportLoader. Given that there is really no other
        //use for loading imports into an editor I am going to essentially just add the required load methods in this loader.

        private const int HEADER_OFFSET_EXP_IDXCLASS = 0x0;
        private const int HEADER_OFFSET_EXP_IDXSUPERCLASS = 0x4;
        private const int HEADER_OFFSET_EXP_IDXLINK = 0x8;
        private const int HEADER_OFFSET_EXP_IDXOBJECTNAME = 0xC;
        private const int HEADER_OFFSET_EXP_INDEXVALUE = 0x10;
        private const int HEADER_OFFSET_EXP_IDXARCHETYPE = 0x14;
        private const int HEADER_OFFSET_EXP_OBJECTFLAGS = 0x18;

        private const int HEADER_OFFSET_EXP_UNKNOWN1 = 0x1C;


        private const int HEADER_OFFSET_IMP_IDXCLASSNAME = 0x8;
        private const int HEADER_OFFSET_IMP_IDXLINK = 0x10;
        private const int HEADER_OFFSET_IMP_IDXOBJECTNAME = 0x14;
        private const int HEADER_OFFSET_IMP_IDXPACKAGEFILE = 0x0;
        private IEntry _currentLoadedEntry;
        public IEntry CurrentLoadedEntry { get => _currentLoadedEntry; private set => SetProperty(ref _currentLoadedEntry, value); }
        private byte[] OriginalHeader;

        public ObservableCollectionExtended<object> AllEntriesList { get; } = new();
        public ObservableCollectionExtended<object> AllClassesList { get; } = new();
        /// <summary>
        /// Functions can list other functions as superclass so you cannot only display a list of classes
        /// </summary>
        public ObservableCollectionExtended<object> AllSuperClassesList { get; } = new();
        public int CurrentObjectNameIndex { get; private set; }

        private HexBox Header_Hexbox;
        private ReadOptimizedByteProvider headerByteProvider;
        private bool loadingNewData;

        public bool SubstituteImageForHexBox
        {
            get => (bool)GetValue(SubstituteImageForHexBoxProperty);
            set => SetValue(SubstituteImageForHexBoxProperty, value);
        }
        public static readonly DependencyProperty SubstituteImageForHexBoxProperty = DependencyProperty.Register(
            nameof(SubstituteImageForHexBox), typeof(bool), typeof(EntryMetadataExportLoader), new PropertyMetadata(false, SubstituteImageForHexBoxChangedCallback));

        private static void SubstituteImageForHexBoxChangedCallback(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            EntryMetadataExportLoader i = (EntryMetadataExportLoader)obj;
            if (e.NewValue is true && i.Header_Hexbox_Host.Child.Height > 0 && i.Header_Hexbox_Host.Child.Width > 0)
            {
                i.hexboxImageSub.Source = i.Header_Hexbox_Host.Child.DrawToBitmapSource();
                i.hexboxImageSub.Width = i.Header_Hexbox_Host.ActualWidth;
                i.hexboxImageSub.Height = i.Header_Hexbox_Host.ActualHeight;
                i.hexboxImageSub.Visibility = Visibility.Visible;
                i.Header_Hexbox_Host.Visibility = Visibility.Collapsed;
            }
            else
            {
                i.Header_Hexbox_Host.Visibility = Visibility.Visible;
                i.hexboxImageSub.Visibility = Visibility.Collapsed;
            }
        }

        public string ObjectIndexOffsetText => CurrentLoadedEntry is ImportEntry ? "0x18 Object index:" : "0x10 Object index:";

        public EntryMetadataExportLoader() : base("Metadata Editor")
        {
            LoadCommands();
            InitializeComponent();
        }

        private bool ControlLoaded;

        private bool _hexChanged;
        private int _exportFlagsOffset;

        public bool HexChanged
        {
            get => _hexChanged && CurrentLoadedEntry != null;
            private set
            {
                if (SetProperty(ref _hexChanged, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ICommand SaveHexChangesCommand { get; private set; }

        private void LoadCommands()
        {
            SaveHexChangesCommand = new GenericCommand(SaveHexChanges, CanSaveHexChanges);
        }

        private bool CanSaveHexChanges() => CurrentLoadedEntry != null && HexChanged;

        private void SaveHexChanges()
        {
            var bytes = GetHeaderBytes();
            CurrentLoadedEntry.SetHeaderValuesFromByteArray(bytes.ToArray());
            switch (CurrentLoadedEntry)
            {
                case ExportEntry exportEntry:
                    LoadExport(exportEntry);
                    break;
                case ImportEntry importEntry:
                    LoadImport(importEntry);
                    break;
            }
        }

        public override bool CanParse(ExportEntry exportEntry) => true;

        public void RefreshAllEntriesList(IMEPackage pcc)
        {
            if (pcc is null)
            {
                AllEntriesList.ClearEx();
                AllClassesList.ClearEx();
                return;
            }
            var allEntriesNew = new List<object>();
            var allClassesNew = new List<object>();
            for (int i = pcc.Imports.Count - 1; i >= 0; i--)
            {
                allEntriesNew.Add(pcc.Imports[i]);
                if (pcc.Imports[i].IsClass)
                {
                    allClassesNew.Add(pcc.Imports[i]);
                }
            }
            allEntriesNew.Add(ZeroUIndexClassEntry.Instance);
            allClassesNew.Add(ZeroUIndexClassEntry.Instance);
            foreach (ExportEntry exp in pcc.Exports)
            {
                allEntriesNew.Add(exp);
                if (exp.IsClass)
                {
                    allClassesNew.Add(exp);
                }
            }
            AllEntriesList.ReplaceAll(allEntriesNew);
            AllClassesList.ReplaceAll(allClassesNew);
        }

        private void RefreshSuperclassOptions(ExportEntry export)
        {
            var allSuperclassesNew = new List<object>();
            if (export != null && export.ClassName is "Class" or "State" or "Function") // Others cannot use SuperClass
            {
                var isClass = export.ClassName == "Class";
                var isState = export.ClassName == "State";
                var isFunc = export.ClassName == "Function";
                
                var pcc = export.FileRef;
                for (int i = pcc.Imports.Count - 1; i >= 0; i--)
                {
                    if (pcc.Imports[i].IsClass && isClass)
                    {
                        allSuperclassesNew.Add(pcc.Imports[i]);
                    }
                    if (pcc.Imports[i].ClassName == "State" && isState)
                    {
                        allSuperclassesNew.Add(pcc.Imports[i]);
                    }
                    if (pcc.Imports[i].ClassName == "Function" && isFunc)
                    {
                        allSuperclassesNew.Add(pcc.Imports[i]);
                    }
                }

                allSuperclassesNew.Add(ZeroUIndexClassEntry.Instance);
                foreach (ExportEntry exp in pcc.Exports)
                {
                    if (exp.IsClass && isClass)
                    {
                        allSuperclassesNew.Add(exp);
                    }

                    if (exp.ClassName is "State" && isState)
                    {
                        allSuperclassesNew.Add(exp);
                    }

                    if (exp.ClassName is "Function" && isFunc)
                    {
                        allSuperclassesNew.Add(exp);
                    }
                }
            }
            else
            {
                allSuperclassesNew.Add(ZeroUIndexClassEntry.Instance);
            }

            AllSuperClassesList.ReplaceAll(allSuperclassesNew);
        }

        public override void PopOut()
        {
            if (CurrentLoadedEntry is ExportEntry export)
            {
                var mde = new EntryMetadataExportLoader();
                var elhw = new ExportLoaderHostedWindow(mde, export)
                {
                    Height = 620,
                    Width = 780,
                    Title = $"Metadata Editor - {export.UIndex} {export.InstancedFullPath} - {export.FileRef.FilePath}"
                };
                mde.RefreshAllEntriesList(CurrentLoadedEntry.FileRef);
                elhw.Show();
            }
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            loadingNewData = true;
            byte[] header = exportEntry.GenerateHeader();
            try
            {
                Row_Archetype.Height = new GridLength(24);
                Row_ExpClass.Height = new GridLength(24);
                Row_Superclass.Height = new GridLength(24);
                Row_ImpClass.Height = new GridLength(0);
                Row_ExpClass.Height = new GridLength(24);
                Row_Packagefile.Height = new GridLength(0);
                Row_ObjectFlags.Height = new GridLength(24);
                Row_ExportDataSize.Height = new GridLength(24);
                Row_ExportDataOffsetDec.Height = new GridLength(24);
                Row_ExportDataOffsetHex.Height = new GridLength(24);
                Row_ExportExportFlags.Height = new GridLength(24);
                Row_ExportPackageFlags.Height = new GridLength(24);
                Row_ExportGenerationNetObjectCount.Height = new GridLength(24);
                Row_ExportGUID.Height = new GridLength(24);
                InfoTab_Link_TextBlock.Text = "0x08 Link:";
                InfoTab_ObjectName_TextBlock.Text = "0x0C Object name:";

                InfoTab_Objectname_ComboBox.SelectedIndex = exportEntry.FileRef.findName(exportEntry.ObjectName.Name);

                LoadAllEntriesBindedItems(exportEntry);

                InfoTab_Headersize_TextBox.Text = $"{header.Length} bytes";
                InfoTab_ObjectnameIndex_TextBox.Text = exportEntry.indexValue.ToString();

                var flagsList = Enums.GetValues<EObjectFlags>().Distinct().ToList();
                //Don't even get me started on how dumb it is that SelectedItems is read only...
                string selectedFlags = flagsList.Where(flag => exportEntry.ObjectFlags.Has(flag)).StringJoin(" ");

                InfoTab_Flags_ComboBox.ItemsSource = flagsList;
                InfoTab_Flags_ComboBox.SelectedValue = selectedFlags;

                InfoTab_ExportDataSize_TextBox.Text =
                    $"{exportEntry.DataSize} bytes ({FileSize.FormatSize(exportEntry.DataSize)})";
                InfoTab_ExportOffsetHex_TextBox.Text = $"0x{exportEntry.DataOffset:X8}";
                InfoTab_ExportOffsetDec_TextBox.Text = exportEntry.DataOffset.ToString();

                if (exportEntry.HasComponentMap)
                {
                    var componentMap = exportEntry.ComponentMap;
                    string components = $"ComponentMap: 0x{40:X2} {componentMap.Count} items\n";
                    int pairOffset = 44;
                    foreach ((NameReference name, int uIndex) in componentMap)
                    {
                        components += $"0x{pairOffset:X2} {name.Instanced} => {uIndex} {exportEntry.FileRef.GetEntryString(uIndex + 1)}\n"; // +1 because it appears to be 0 based?
                        pairOffset += 12;
                    }

                    Header_Hexbox_ComponentsLabel.Text = components;
                }
                else
                {
                    Header_Hexbox_ComponentsLabel.Text = "";
                }
                _exportFlagsOffset = exportEntry.HasComponentMap ? 44 + EndianReader.ToInt32(header, 40, exportEntry.FileRef.Endian) * 12 : 40;
                InfoTab_ExportFlags_TextBlock.Text = $"0x{_exportFlagsOffset:X2} ExportFlags:";
                List<EExportFlags> exportFlagsList = Enums.GetValues<EExportFlags>().Distinct().ToList();
                string selectedExportFlags = exportFlagsList.Where(flag => exportEntry.ExportFlags.Has(flag)).StringJoin(" ");
                InfoTab_ExportFlags_ComboBox.ItemsSource = exportFlagsList;
                InfoTab_ExportFlags_ComboBox.SelectedValue = selectedExportFlags;

                InfoTab_GenerationNetObjectCount_TextBlock.Text =
                    $"0x{_exportFlagsOffset + 4:X2} GenerationNetObjs:";
                int[] generationNetObjectCount = exportEntry.GenerationNetObjectCount;
                InfoTab_GenerationNetObjectCount_TextBox.Text =
                    $"{generationNetObjectCount.Length} counts: {string.Join(", ", generationNetObjectCount)}";

                int packageGuidOffset = _exportFlagsOffset + 8 + generationNetObjectCount.Length * 4;
                InfoTab_GUID_TextBlock.Text = $"0x{packageGuidOffset:X2} GUID:";
                InfoTab_ExportGUID_TextBox.Text = exportEntry.PackageGUID.ToString();
                if (exportEntry.FileRef.Platform is MEPackage.GamePlatform.Xenon && exportEntry.FileRef.Game is MEGame.ME1)
                {
                    InfoTab_PackageFlags_TextBlock.Text = "";
                    InfoTab_PackageFlags_TextBox.Text = "";
                }
                else
                {
                    InfoTab_PackageFlags_TextBlock.Text = $"0x{packageGuidOffset + 16:X2} PackageFlags:";
                    InfoTab_PackageFlags_TextBox.Text = Enums.GetValues<EPackageFlags>().Distinct().ToList()
                        .Where(flag => exportEntry.PackageFlags.HasFlag(flag)).StringJoin(" ");
                }
            }
            catch (Exception e)
            {
                //MessageBox.Show("An error occurRed while attempting to read the header for this export. This indicates there is likely something wrong with the header or its parent header.\n\n" + e.Message);
            }

            CurrentLoadedEntry = exportEntry;
            OriginalHeader = header;
            headerByteProvider.ReplaceBytes(header);
            HexChanged = false;
            Header_Hexbox?.Refresh();
            OnPropertyChanged(nameof(ObjectIndexOffsetText));
            loadingNewData = false;
        }

        /// <summary>
        /// Sets the dropdowns for the items binded to the AllEntries list. HandleUpdate() may fire in the parent control, refreshing the list of values, so we will refire this when that occurs.
        /// </summary>
        /// <param name="entry"></param>
        private void LoadAllEntriesBindedItems(IEntry entry)
        {
            if (entry is ExportEntry exportEntry)
            {
                if (exportEntry.IsClass)
                {
                    InfoTab_Class_ComboBox.SelectedItem = ZeroUIndexClassEntry.Instance; //Class, 0
                }
                else
                {
                    InfoTab_Class_ComboBox.SelectedItem = exportEntry.Class; //make positive
                }

                RefreshSuperclassOptions(exportEntry);
                if (exportEntry.HasSuperClass)
                {
                    InfoTab_Superclass_ComboBox.SelectedItem = exportEntry.SuperClass;
                }
                else
                {
                    InfoTab_Superclass_ComboBox.SelectedItem = ZeroUIndexClassEntry.Instance; //Class, 0
                }

                if (exportEntry.HasParent)
                {
                    InfoTab_PackageLink_ComboBox.SelectedItem = exportEntry.Parent;
                }
                else
                {
                    InfoTab_PackageLink_ComboBox.SelectedItem = ZeroUIndexClassEntry.Instance; //Class, 0
                }

                if (exportEntry.HasArchetype)
                {
                    InfoTab_Archetype_ComboBox.SelectedItem = exportEntry.Archetype;
                }
                else
                {
                    InfoTab_Archetype_ComboBox.SelectedItem = ZeroUIndexClassEntry.Instance; //Class, 0
                }
            }
            else if (entry is ImportEntry importEntry)
            {
                if (importEntry.HasParent)
                {
                    InfoTab_PackageLink_ComboBox.SelectedItem = importEntry.Parent;
                }
                else
                {
                    InfoTab_PackageLink_ComboBox.SelectedItem = ZeroUIndexClassEntry.Instance; //Class, 0
                }
            }
        }

        public void LoadImport(ImportEntry importEntry)
        {
            loadingNewData = true;
            InfoTab_Headersize_TextBox.Text = $"{ImportEntry.HeaderLength} bytes";
            Row_Archetype.Height = new GridLength(0);
            Row_ExpClass.Height = new GridLength(0);
            Row_ImpClass.Height = new GridLength(24);
            Row_ExportDataSize.Height = new GridLength(0);
            Row_ExportDataOffsetDec.Height = new GridLength(0);
            Row_ExportDataOffsetHex.Height = new GridLength(0);
            Row_ExportExportFlags.Height = new GridLength(0);
            Row_ExportPackageFlags.Height = new GridLength(0);
            Row_ExportGenerationNetObjectCount.Height = new GridLength(0);
            Row_ExportGUID.Height = new GridLength(0);
            Row_Superclass.Height = new GridLength(0);
            Row_ObjectFlags.Height = new GridLength(0);
            Row_Packagefile.Height = new GridLength(24);
            InfoTab_Link_TextBlock.Text = "0x10 Link:";
            InfoTab_ObjectName_TextBlock.Text = "0x14 Object name:";
            Header_Hexbox_ComponentsLabel.Text = "";

            InfoTab_Objectname_ComboBox.SelectedIndex = importEntry.FileRef.findName(importEntry.ObjectName.Name);
            InfoTab_ImpClass_ComboBox.SelectedIndex = importEntry.FileRef.findName(importEntry.ClassName);
            LoadAllEntriesBindedItems(importEntry);

            InfoTab_PackageFile_ComboBox.SelectedIndex = importEntry.FileRef.findName(importEntry.PackageFile);
            InfoTab_ObjectnameIndex_TextBox.Text = importEntry.indexValue.ToString();
            CurrentLoadedEntry = importEntry;
            OriginalHeader = CurrentLoadedEntry.GenerateHeader();
            headerByteProvider.ReplaceBytes(OriginalHeader);
            Header_Hexbox?.Refresh();
            HexChanged = false;
            OnPropertyChanged(nameof(ObjectIndexOffsetText));
            loadingNewData = false;
        }

        internal void SetHexboxSelectedOffset(long v)
        {
            if (Header_Hexbox != null)
            {
                Header_Hexbox.SelectionStart = v;
                Header_Hexbox.SelectionLength = 1;
            }
        }

        private void hb1_SelectionChanged(object sender, EventArgs e)
        {
            int start = (int)Header_Hexbox.SelectionStart;
            int len = (int)Header_Hexbox.SelectionLength;
            int size = (int)headerByteProvider.Length;

            var currentData = headerByteProvider.Span;
            try
            {
                if (start != -1 && start < size)
                {
                    string s = $"Byte: {currentData[start]}"; //if selection is same as size this will crash.
                    if (start <= currentData.Length - 4)
                    {
                        int val = EndianReader.ToInt32(currentData, start, CurrentLoadedEntry.FileRef.Endian);
                        s += $", Int: {val}";
                        if (CurrentLoadedEntry.FileRef.IsName(val))
                        {
                            s += $", Name: {CurrentLoadedEntry.FileRef.GetNameEntry(val)}";
                        }
                        if (CurrentLoadedEntry.FileRef.GetEntry(val) is ExportEntry exp)
                        {
                            s += $", Export: {exp.ObjectName.Instanced}";
                        }
                        else if (CurrentLoadedEntry.FileRef.GetEntry(val) is ImportEntry imp)
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
                    Header_Hexbox_SelectedBytesLabel.Text = s;
                }
                else
                {
                    Header_Hexbox_SelectedBytesLabel.Text = "Nothing Selected";
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        internal void ClearMetadataPane()
        {
            loadingNewData = true;
            InfoTab_Objectname_ComboBox.SelectedItem = null;
            InfoTab_Class_ComboBox.SelectedItem = null;
            InfoTab_Superclass_ComboBox.SelectedItem = null;
            InfoTab_PackageLink_ComboBox.SelectedItem = null;
            InfoTab_Headersize_TextBox.Text = null;
            InfoTab_ObjectnameIndex_TextBox.Text = null;
            //InfoTab_Archetype_ComboBox.ItemsSource = null;
            //InfoTab_Archetype_ComboBox.Items.Clear();
            InfoTab_Archetype_ComboBox.SelectedItem = null;
            InfoTab_Flags_ComboBox.ItemsSource = null;
            InfoTab_Flags_ComboBox.SelectedItem = null;
            InfoTab_ExportFlags_ComboBox.ItemsSource = null;
            InfoTab_ExportFlags_ComboBox.SelectedItem = null;
            InfoTab_ExportDataSize_TextBox.Text = null;
            InfoTab_ExportOffsetHex_TextBox.Text = null;
            InfoTab_ExportOffsetDec_TextBox.Text = null;
            headerByteProvider.Clear();
            loadingNewData = false;
        }


        public override void UnloadExport()
        {
            UnloadEntry();
        }

        private void UnloadEntry()
        {
            CurrentLoadedEntry = null;
            ClearMetadataPane();
            Header_Hexbox?.Refresh();
        }

        internal void LoadPccData(IMEPackage pcc)
        {
            RefreshAllEntriesList(pcc);
        }

        //Exports
        private void Info_ClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData && InfoTab_Class_ComboBox.SelectedIndex >= 0)
            {
                var selectedClassIndex = InfoTab_Class_ComboBox.SelectedIndex;
                var unrealIndex = (AllClassesList[selectedClassIndex] as IEntry)?.UIndex ?? 0;
                if (unrealIndex == CurrentLoadedEntry?.UIndex)
                {
                    var exp = (ExportEntry)CurrentLoadedEntry;
                    var expClassUIndex = exp.Class?.UIndex ?? 0;
                    loadingNewData = true;
                    InfoTab_Class_ComboBox.SelectedIndex = expClassUIndex != 0
                        ? AllClassesList.FindIndex(x => x is IEntry ie && ie.UIndex == expClassUIndex)
                        : AllClassesList.IndexOf(ZeroUIndexClassEntry.Instance); // Set to 0
                    loadingNewData = false;
                    MessageBox.Show("Cannot set class to self, this will cause infinite recursion in game.");
                    return;
                }

                headerByteProvider.WriteBytes(HEADER_OFFSET_EXP_IDXCLASS, BitConverter.GetBytes(unrealIndex));
                Header_Hexbox?.Refresh();
            }
        }

        private void InfoTab_Header_ByteProvider_InternalChanged(object sender, EventArgs e)
        {
            if (OriginalHeader != null)
            {
                HexChanged = !headerByteProvider.Span.SequenceEqual(OriginalHeader);
            }
        }

        private void Info_PackageLinkClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData && InfoTab_PackageLink_ComboBox.SelectedIndex >= 0)
            {

                var selectedImpExp = InfoTab_PackageLink_ComboBox.SelectedIndex;
                var unrealIndex = selectedImpExp - CurrentLoadedEntry.FileRef.ImportCount; //get the actual UIndex
                if (unrealIndex == CurrentLoadedEntry?.UIndex)
                {
                    MessageBox.Show("Cannot link to self, this will cause infinite recursion.");
                    InfoTab_PackageLink_ComboBox.SelectedIndex = CurrentLoadedEntry.idxLink + CurrentLoadedEntry.FileRef.ImportCount;
                    return;
                }
                headerByteProvider.WriteBytes(CurrentLoadedEntry is ExportEntry ? HEADER_OFFSET_EXP_IDXLINK : HEADER_OFFSET_IMP_IDXLINK, BitConverter.GetBytes(unrealIndex));
                Header_Hexbox?.Refresh();
            }
        }

        private void Info_SuperClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData && InfoTab_Superclass_ComboBox.SelectedIndex >= 0)
            {
                var selectedClassIndex = InfoTab_Superclass_ComboBox.SelectedIndex;
                var unrealIndex = (AllClassesList[selectedClassIndex] as IEntry)?.UIndex ?? 0;
                if (unrealIndex == CurrentLoadedEntry?.UIndex)
                {
                    MessageBox.Show("Cannot set superclass to self, this will cause infinite recursion in game.");
                    var exp = (ExportEntry)CurrentLoadedEntry;

                    if (exp.HasSuperClass)
                    {
                        var superclass = exp.SuperClass;
                        InfoTab_Superclass_ComboBox.SelectedIndex = AllClassesList.FindIndex(x => x is IEntry ie && ie == superclass);
                    }
                    else
                    {
                        InfoTab_Superclass_ComboBox.SelectedIndex = AllClassesList.IndexOf(ZeroUIndexClassEntry.Instance);
                    }
                    return;
                }

                headerByteProvider.WriteBytes(HEADER_OFFSET_EXP_IDXSUPERCLASS, BitConverter.GetBytes(unrealIndex));
                Header_Hexbox?.Refresh();
            }
        }

        private void Info_ObjectNameComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData && InfoTab_Objectname_ComboBox.SelectedIndex >= 0 && CurrentLoadedEntry != null)
            {
                var selectedNameIndex = InfoTab_Objectname_ComboBox.SelectedIndex;
                if (selectedNameIndex >= 0)
                {
                    headerByteProvider.WriteBytes(CurrentLoadedEntry is ExportEntry ? HEADER_OFFSET_EXP_IDXOBJECTNAME : HEADER_OFFSET_IMP_IDXOBJECTNAME, BitConverter.GetBytes(selectedNameIndex));
                    Header_Hexbox?.Refresh();
                }
            }
        }

        private void Info_IndexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!loadingNewData)
            {
                if (int.TryParse(InfoTab_ObjectnameIndex_TextBox.Text, out int x))
                {
                    headerByteProvider.WriteBytes(CurrentLoadedEntry is ExportEntry ? HEADER_OFFSET_EXP_INDEXVALUE : HEADER_OFFSET_IMP_IDXOBJECTNAME + 4, BitConverter.GetBytes(x));
                    Header_Hexbox?.Refresh();
                }
            }
        }

        private void Info_ArchetypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData && InfoTab_Archetype_ComboBox.SelectedIndex >= 0)
            {
                var selectedArchetTypeIndex = InfoTab_Archetype_ComboBox.SelectedIndex;
                var unrealIndex = selectedArchetTypeIndex - CurrentLoadedEntry.FileRef.ImportCount;
                if (unrealIndex == CurrentLoadedEntry?.UIndex)
                {
                    MessageBox.Show("Cannot set archetype to self, this will cause infinite recursion in game.");
                    var exp = (ExportEntry)CurrentLoadedEntry;

                    if (exp.HasArchetype)
                    {
                        InfoTab_Archetype_ComboBox.SelectedIndex = exp.Archetype.UIndex + CurrentLoadedEntry.FileRef.ImportCount;
                    }
                    else
                    {
                        InfoTab_Archetype_ComboBox.SelectedIndex = CurrentLoadedEntry.FileRef.ImportCount; //0
                    }
                    return;
                }

                headerByteProvider.WriteBytes(HEADER_OFFSET_EXP_IDXARCHETYPE, BitConverter.GetBytes(unrealIndex));
                Header_Hexbox?.Refresh();
            }
        }

        private void Info_PackageFileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData && InfoTab_PackageFile_ComboBox.SelectedIndex >= 0)
            {
                int selectedNameIndex = InfoTab_PackageFile_ComboBox.SelectedIndex;
                if (selectedNameIndex == CurrentLoadedEntry.FileRef.findName("None"))
                {
                    MessageBox.Show("Cannot set Package File to 'None', this is not allowed by the game engine.");
                    InfoTab_PackageFile_ComboBox.SelectedIndex = CurrentLoadedEntry.FileRef.findName((CurrentLoadedEntry as ImportEntry).PackageFile);
                    return;
                }

                headerByteProvider.WriteBytes(HEADER_OFFSET_IMP_IDXPACKAGEFILE, BitConverter.GetBytes(selectedNameIndex));
                Header_Hexbox?.Refresh();
            }
        }

        private void Info_ImpClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData && InfoTab_ImpClass_ComboBox.SelectedIndex >= 0)
            {
                var selectedNameIndex = InfoTab_ImpClass_ComboBox.SelectedIndex;
                headerByteProvider.WriteBytes(HEADER_OFFSET_IMP_IDXCLASSNAME, BitConverter.GetBytes(selectedNameIndex));
                Header_Hexbox?.Refresh();
            }
        }

        private void InfoTab_Objectname_ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Header_Hexbox.SelectionStart = CurrentLoadedEntry is ExportEntry ? HEADER_OFFSET_EXP_IDXOBJECTNAME : HEADER_OFFSET_IMP_IDXOBJECTNAME;
            Header_Hexbox.SelectionLength = 4;
        }

        private void InfoTab_Class_ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Header_Hexbox.SelectionStart = HEADER_OFFSET_EXP_IDXCLASS;
            Header_Hexbox.SelectionLength = 4;
        }

        private void InfoTab_ImpClass_ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Header_Hexbox.SelectionStart = HEADER_OFFSET_IMP_IDXCLASSNAME;
            Header_Hexbox.SelectionLength = 4;
        }

        private void InfoTab_Superclass_ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Header_Hexbox.SelectionStart = HEADER_OFFSET_EXP_IDXSUPERCLASS;
            Header_Hexbox.SelectionLength = 4;
        }

        private void InfoTab_PackageLink_ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Header_Hexbox.SelectionStart = CurrentLoadedEntry is ExportEntry ? HEADER_OFFSET_EXP_IDXLINK : HEADER_OFFSET_IMP_IDXLINK;
            Header_Hexbox.SelectionLength = 4;
        }

        private void InfoTab_PackageFile_ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Header_Hexbox.SelectionStart = HEADER_OFFSET_IMP_IDXPACKAGEFILE;
            Header_Hexbox.SelectionLength = 4;
        }

        private void InfoTab_Archetype_ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Header_Hexbox.SelectionStart = HEADER_OFFSET_EXP_IDXARCHETYPE;
            Header_Hexbox.SelectionLength = 4;
        }

        private void InfoTab_Flags_ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Header_Hexbox.SelectionStart = HEADER_OFFSET_EXP_OBJECTFLAGS;
            Header_Hexbox.SelectionLength = 8;
        }

        private void InfoTab_ExportFlags_ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Header_Hexbox.SelectionStart = _exportFlagsOffset;
            Header_Hexbox.SelectionLength = 4;
        }

        private void InfoTab_ObjectNameIndex_ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Header_Hexbox.SelectionStart = CurrentLoadedEntry is ExportEntry ? HEADER_OFFSET_EXP_IDXOBJECTNAME + 4 : HEADER_OFFSET_IMP_IDXOBJECTNAME + 4;
            Header_Hexbox.SelectionLength = 4;
        }

        /// <summary>
        /// Handler for when the flags combobox item changes value
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InfoTab_Flags_ComboBox_ItemSelectionChanged(object sender, ItemSelectionChangedEventArgs e)
        {
            if (!loadingNewData)
            {
                EObjectFlags newFlags = 0U;
                foreach (object flag in InfoTab_Flags_ComboBox.Items)
                {
                    if (InfoTab_Flags_ComboBox.ItemContainerGenerator.ContainerFromItem(flag) is SelectorItem { IsSelected: true })
                    {
                        newFlags |= (EObjectFlags)flag;
                    }
                }
                //Debug.WriteLine(newFlags);
                headerByteProvider.WriteBytes(HEADER_OFFSET_EXP_OBJECTFLAGS, BitConverter.GetBytes((ulong)newFlags));
                Header_Hexbox?.Refresh();
            }
        }

        /// <summary>
        /// Handler for when the exportflags combobox item changes value
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InfoTab_ExportFlags_ComboBox_ItemSelectionChanged(object sender, ItemSelectionChangedEventArgs e)
        {
            if (!loadingNewData)
            {
                EExportFlags newFlags = 0U;
                foreach (object flag in InfoTab_ExportFlags_ComboBox.Items)
                {
                    if (InfoTab_ExportFlags_ComboBox.ItemContainerGenerator.ContainerFromItem(flag) is SelectorItem { IsSelected: true })
                    {
                        newFlags |= (EExportFlags)flag;
                    }
                }
                //Debug.WriteLine(newFlags);
                headerByteProvider.WriteBytes(_exportFlagsOffset, BitConverter.GetBytes((uint)newFlags));
                Header_Hexbox?.Refresh();
            }
        }

        private void MetadataEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (!ControlLoaded)
            {
                Header_Hexbox = (HexBox)Header_Hexbox_Host.Child;
                headerByteProvider = new ReadOptimizedByteProvider();
                Header_Hexbox.ByteProvider = headerByteProvider;
                if (CurrentLoadedEntry != null) headerByteProvider.ReplaceBytes(CurrentLoadedEntry.GenerateHeader());
                headerByteProvider.Changed += InfoTab_Header_ByteProvider_InternalChanged;
                ControlLoaded = true;

                Header_Hexbox.SelectionStartChanged -= hb1_SelectionChanged;
                Header_Hexbox.SelectionLengthChanged -= hb1_SelectionChanged;

                Header_Hexbox.SelectionStartChanged += hb1_SelectionChanged;
                Header_Hexbox.SelectionLengthChanged += hb1_SelectionChanged;
            }
        }

        /// <summary>
        /// Handles pressing the enter key when the class dropdown is active. Automatically will attempt to find the next object by class.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InfoTab_Objectname_ComboBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                //Check name
                var text = InfoTab_Objectname_ComboBox.Text;
                int index = CurrentLoadedEntry.FileRef.findName(text);
                if (index < 0 && !string.IsNullOrEmpty(text))
                {
                    Keyboard.ClearFocus();
                    string input = $"The name \"{text}\" does not exist in the current loaded package.\nIf you'd like to add this name, press enter below, or change the name to what you would like it to be.";
                    string result = PromptDialog.Prompt(this, input, "Enter new name", text);
                    if (!string.IsNullOrEmpty(result))
                    {
                        int idx = CurrentLoadedEntry.FileRef.FindNameOrAdd(result);
                        if (idx != CurrentLoadedEntry.FileRef.Names.Count - 1)
                        {
                            //not the last
                            MessageBox.Show($"{result} already exists in this package file.\nName index: {idx} (0x{idx:X8})", "Name already exists");
                        }
                        else
                        {
                            CurrentObjectNameIndex = idx;
                        }
                        //refresh should be triggered by hosting window
                    }
                }
                else
                {
                    e.Handled = true;
                }
            }
        }

        public override void SignalNamelistAboutToUpdate()
        {
            CurrentObjectNameIndex = CurrentObjectNameIndex >= 0 ? CurrentObjectNameIndex : InfoTab_Objectname_ComboBox.SelectedIndex;
        }

        public override void SignalNamelistChanged()
        {
            InfoTab_Objectname_ComboBox.SelectedIndex = CurrentObjectNameIndex;
            CurrentObjectNameIndex = -1;
        }

        public override void Dispose()
        {
            if (Header_Hexbox != null)
            {
                Header_Hexbox.SelectionStartChanged -= hb1_SelectionChanged;
                Header_Hexbox.SelectionLengthChanged -= hb1_SelectionChanged;
            }

            Header_Hexbox = null;
            Header_Hexbox_Host?.Child.Dispose();
            Header_Hexbox_Host?.Dispose();
            Header_Hexbox_Host = null;
            AllEntriesList.Clear();
            _currentLoadedEntry = null;
        }



        /// <summary>
        /// This class is used when stuffing into the list. It makes "0" searchable by having the UIndex property.
        /// </summary>
        internal class ZeroUIndexClassEntry
        {
            public static readonly ZeroUIndexClassEntry Instance = new();

            private ZeroUIndexClassEntry() { }

            public override string ToString() => "0: Class";

            public int UIndex => 0;
        }

        private ReadOnlySpan<byte> GetHeaderBytes() => headerByteProvider.Span;

        private void GoToExportClass_Clicked(object sender, MouseButtonEventArgs e)
        {
            var header = GetHeaderBytes();
            if (header.Length >= HEADER_OFFSET_EXP_IDXCLASS + 4)
            {
                var uindex = EndianReader.ToInt32(header, HEADER_OFFSET_EXP_IDXCLASS, CurrentLoadedEntry.FileRef.Endian);
                GoToEntryUIndex(uindex);
            }
        }

        private void GoToSuperclass_Clicked(object sender, MouseButtonEventArgs e)
        {
            var header = GetHeaderBytes();
            if (header.Length >= HEADER_OFFSET_EXP_IDXCLASS + 4)
            {
                int uindex = EndianReader.ToInt32(header, HEADER_OFFSET_EXP_IDXSUPERCLASS, CurrentLoadedEntry.FileRef.Endian);
                GoToEntryUIndex(uindex);
            }
        }

        private void GoToArchetype_Clicked(object sender, MouseButtonEventArgs e)
        {
            var header = GetHeaderBytes();
            if (header.Length >= HEADER_OFFSET_EXP_IDXCLASS + 4)
            {
                int uindex = EndianReader.ToInt32(header, HEADER_OFFSET_EXP_IDXARCHETYPE, CurrentLoadedEntry.FileRef.Endian);
                GoToEntryUIndex(uindex);
            }
        }

        private void GoToEntryUIndex(int uIndex)
        {
            if (CurrentLoadedEntry.FileRef.TryGetEntry(uIndex, out IEntry entry))
            {
                if (entry is ExportEntry exp)
                {
                    if (Window.GetWindow(this) is PackageEditorWindow pe)
                    {
                        pe.GoToNumber(exp.UIndex);
                    }
                }
                else if (entry is ImportEntry imp)
                {
                    if (EntryImporter.ResolveImport(imp) is ExportEntry resolved)
                    {
                        var p = new PackageEditorWindow();
                        p.Show();
                        p.LoadFile(resolved.FileRef.FilePath, resolved.UIndex);
                        p.Activate(); //bring to front
                    }
                }
            }
        }
    }
}
