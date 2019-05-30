using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using Xceed.Wpf.Toolkit.Primitives;
using static ME3Explorer.Unreal.UnrealFlags;

namespace ME3Explorer.MetadataEditor
{
    /// <summary>
    /// Interaction logic for MetadataEditorWPF.xaml
    /// </summary>
    public partial class MetadataEditorWPF : ExportLoaderControl
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
        private IEntry CurrentLoadedEntry;
        private byte[] OriginalHeader;

        public ObservableCollectionExtended<object> AllEntriesList { get; } = new ObservableCollectionExtended<object>();
        public int CurrentObjectNameIndex { get; private set; }

        private HexBox Header_Hexbox;
        private DynamicByteProvider headerByteProvider;
        private bool loadingNewData;

        public string ObjectIndexOffsetText => CurrentLoadedEntry is ImportEntry ? "0x18 Object index:" : "0x10 Object index:";

        public MetadataEditorWPF()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        private bool ControlLoaded;

        private bool _hexChanged;

        public bool HexChanged
        {
            get => _hexChanged;
            private set => SetProperty(ref _hexChanged, value);
        }

        public ICommand SaveHexChangesCommand { get; private set; }

        private void LoadCommands()
        {
            SaveHexChangesCommand = new GenericCommand(SaveHexChanges, CanSaveHexChanges);
        }

        private bool CanSaveHexChanges() => HexChanged;

        private void SaveHexChanges()
        {
            MemoryStream m = new MemoryStream();
            for (int i = 0; i < headerByteProvider.Length; i++)
                m.WriteByte(headerByteProvider.ReadByte(i));
            CurrentLoadedEntry.Header = m.ToArray();
            switch (CurrentLoadedEntry)
            {
                case IExportEntry exportEntry:
                    LoadExport(exportEntry);
                    break;
                case ImportEntry importEntry:
                    LoadImport(importEntry);
                    break;
            }
        }

        public override bool CanParse(IExportEntry exportEntry) => true;

        public void RefreshAllEntriesList(IMEPackage pcc)
        {
            var allEntriesNew = new List<object>();
            for (int i = pcc.Imports.Count - 1; i >= 0; i--)
            {
                allEntriesNew.Add(pcc.Imports[i]);
            }
            allEntriesNew.Add(ZeroUIndexClassEntry.instance);
            foreach (IExportEntry exp in pcc.Exports)
            {
                allEntriesNew.Add(exp);
            }
            AllEntriesList.ReplaceAll(allEntriesNew);
        }

        public override void PopOut()
        {
            if (CurrentLoadedEntry is IExportEntry export)
            {
                var mde = new MetadataEditorWPF();
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(mde, export)
                {
                    Height = 620,
                    Width = 780,
                    Title = $"Metadata Editor - {export.UIndex} {export.GetFullPath}_{export.indexValue} - {export.FileRef.FileName}"
                };
                mde.RefreshAllEntriesList(CurrentLoadedEntry.FileRef);
                elhw.Show();
            }
        }

        public override void LoadExport(IExportEntry exportEntry)
        {
            loadingNewData = true;
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
                Row_ExportUnknown1.Height = new GridLength(24);
                Row_ExportUnknown2.Height = new GridLength(24);
                Row_ExportPreGUIDCount.Height = new GridLength(24);
                Row_ExportGUID.Height = new GridLength(24);
                InfoTab_Link_TextBlock.Text = "0x08 Link:";
                InfoTab_ObjectName_TextBlock.Text = "0x0C Object name:";

                InfoTab_Objectname_ComboBox.SelectedIndex = exportEntry.FileRef.findName(exportEntry.ObjectName);

                LoadAllEntriesBindedItems(exportEntry);

                InfoTab_Headersize_TextBox.Text = exportEntry.Header.Length + " bytes";
                InfoTab_ObjectnameIndex_TextBox.Text = BitConverter.ToInt32(exportEntry.Header, HEADER_OFFSET_EXP_IDXOBJECTNAME + 4).ToString();

                var flagsList = Enums.GetValues<EObjectFlags>().Distinct().ToList();
                //Don't even get me started on how dumb it is that SelectedItems is read only...
                string selectedFlags = "";
                foreach (EObjectFlags flag in flagsList)
                {
                    bool selected = (exportEntry.ObjectFlags & (ulong)flag) != 0;
                    if (selected)
                    {
                        if (selectedFlags != "")
                        {
                            selectedFlags += " ";
                        }
                        selectedFlags += flag;
                    }
                }

                InfoTab_Flags_ComboBox.ItemsSource = flagsList;
                InfoTab_Flags_ComboBox.SelectedValue = selectedFlags;

                InfoTab_ExportDataSize_TextBox.Text = $"{exportEntry.DataSize} bytes";
                InfoTab_ExportOffsetHex_TextBox.Text = $"0x{exportEntry.DataOffset:X8}";
                InfoTab_ExportOffsetDec_TextBox.Text = exportEntry.DataOffset.ToString();

                //not parsed by package handling, must do it manually here
                byte[] header = exportEntry.Header;

                InfoTab_ExportUnknown1_TextBox.Text = BitConverter.ToInt32(header, 0x28).ToString();

                int preguidcountoffset = exportEntry.FileRef.Game == MEGame.ME3 ? 0x2C : 0x30;
                InfoTab_PreGUID_TextBlock.Text = $"0x{preguidcountoffset:X2} Pre GUID count:";
                int preguidcount = BitConverter.ToInt32(header, preguidcountoffset);
                InfoTab_ExportPreGuidCount_TextBox.Text = preguidcount.ToString();

                int guidOffset = (preguidcountoffset + 4) + (preguidcount * 4);
                InfoTab_GUID_TextBlock.Text = $"0x{guidOffset:X2} GUID:";
                byte[] guidbytes = header.Skip(guidOffset).Take(16).ToArray();
                Guid guid = new Guid(guidbytes);
                InfoTab_ExportGUID_TextBox.Text = guid.ToString();
                InfoTab_Unknown2_TextBlock.Text = $"0x{(guidOffset + 16):X2} Unknown 2:";
                if (guidOffset + 16 <= header.Length - 4)
                {
                    InfoTab_ExportUnknown2_TextBox.Text = BitConverter.ToInt32(header, guidOffset + 16).ToString();
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occured while attempting to read the header for this export. This indicates there is likely something wrong with the header or its parent header.\n\n" + e.Message);
            }

            CurrentLoadedEntry = exportEntry;
            OriginalHeader = CurrentLoadedEntry.Header;
            headerByteProvider.ReplaceBytes(CurrentLoadedEntry.Header);
            HexChanged = false;
            Header_Hexbox.Refresh();
            OnPropertyChanged(nameof(ObjectIndexOffsetText));
            loadingNewData = false;
        }

        /// <summary>
        /// Sets the dropdowns for the items binded to the AllEntries list. HandleUpdate() may fire in the parent control, refreshing the list of values, so we will refire this when that occurs.
        /// </summary>
        /// <param name="entry"></param>
        private void LoadAllEntriesBindedItems(IEntry entry)
        {
            if (entry is IExportEntry exportEntry)
            {
                if (exportEntry.idxClass != 0)
                {
                    //IEntry _class = pcc.getEntry(exportEntry.idxClass);
                    //InfoTab_Class_ComboBox.ItemsSource = AllEntriesList;
                    InfoTab_Class_ComboBox.SelectedIndex = exportEntry.idxClass + exportEntry.FileRef.Imports.Count; //make positive
                }
                else
                {
                    InfoTab_Class_ComboBox.SelectedIndex = exportEntry.FileRef.Imports.Count; //Class, 0
                }

                //InfoTab_Superclass_ComboBox.ItemsSource = AllEntriesList;
                if (exportEntry.idxClassParent != 0)
                {
                    InfoTab_Superclass_ComboBox.SelectedIndex = exportEntry.idxClassParent + exportEntry.FileRef.Imports.Count; //make positive
                }
                else
                {
                    InfoTab_Superclass_ComboBox.SelectedIndex = exportEntry.FileRef.Imports.Count; //Class, 0
                }

                if (exportEntry.idxLink != 0)
                {
                    InfoTab_PackageLink_ComboBox.SelectedIndex = exportEntry.idxLink + exportEntry.FileRef.Imports.Count; //make positive
                }
                else
                {
                    InfoTab_PackageLink_ComboBox.SelectedIndex = exportEntry.FileRef.Imports.Count; //Class, 0
                }

                if (exportEntry.idxArchtype != 0)
                {
                    InfoTab_Archetype_ComboBox.SelectedIndex = exportEntry.idxArchtype + exportEntry.FileRef.Imports.Count; //make positive
                }
                else
                {
                    InfoTab_Archetype_ComboBox.SelectedIndex = exportEntry.FileRef.Imports.Count; //Class, 0
                }
            }
            else if (entry is ImportEntry importEntry)
            {
                if (importEntry.idxLink != 0)
                {
                    InfoTab_PackageLink_ComboBox.SelectedIndex = importEntry.idxLink + importEntry.FileRef.Imports.Count; //make positive
                }
                else
                {
                    InfoTab_PackageLink_ComboBox.SelectedIndex = importEntry.FileRef.Imports.Count; //Class, 0
                }
            }
        }

        public void LoadImport(ImportEntry importEntry)
        {
            loadingNewData = true;
            InfoTab_Headersize_TextBox.Text = $"{importEntry.Header.Length} bytes";
            Row_Archetype.Height = new GridLength(0);
            Row_ExpClass.Height = new GridLength(0);
            Row_ImpClass.Height = new GridLength(24);
            Row_ExportDataSize.Height = new GridLength(0);
            Row_ExportDataOffsetDec.Height = new GridLength(0);
            Row_ExportDataOffsetHex.Height = new GridLength(0);
            Row_ExportUnknown1.Height = new GridLength(0);
            Row_ExportUnknown2.Height = new GridLength(0);
            Row_ExportPreGUIDCount.Height = new GridLength(0);
            Row_ExportGUID.Height = new GridLength(0);
            Row_Superclass.Height = new GridLength(0);
            Row_ObjectFlags.Height = new GridLength(0);
            Row_Packagefile.Height = new GridLength(24);
            InfoTab_Link_TextBlock.Text = "0x10 Link:";
            InfoTab_ObjectName_TextBlock.Text = "0x14 Object name:";

            InfoTab_Objectname_ComboBox.SelectedIndex = importEntry.FileRef.findName(importEntry.ObjectName);
            InfoTab_ImpClass_ComboBox.SelectedIndex = importEntry.FileRef.findName(importEntry.ClassName);
            LoadAllEntriesBindedItems(importEntry);

            InfoTab_PackageFile_ComboBox.SelectedIndex = importEntry.FileRef.findName(System.IO.Path.GetFileNameWithoutExtension(importEntry.PackageFile));
            InfoTab_ObjectnameIndex_TextBox.Text = BitConverter.ToInt32(importEntry.Header, HEADER_OFFSET_IMP_IDXOBJECTNAME + 4).ToString();
            CurrentLoadedEntry = importEntry;
            OriginalHeader = CurrentLoadedEntry.Header;
            headerByteProvider.ReplaceBytes(CurrentLoadedEntry.Header);
            Header_Hexbox.Refresh();
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
            //TODO: Optimize this so this is only called when data has changed
            byte[] currentData = headerByteProvider.Bytes.ToArray();
            try
            {
                if (start != -1 && start < size)
                {
                    string s = $"Byte: {currentData[start]}"; //if selection is same as size this will crash.
                    if (start <= currentData.Length - 4)
                    {
                        int val = BitConverter.ToInt32(currentData, start);
                        s += $", Int: {val}";
                        if (CurrentLoadedEntry.FileRef.isName(val))
                        {
                            s += $", Name: {CurrentLoadedEntry.FileRef.getNameEntry(val)}";
                        }
                        if (CurrentLoadedEntry.FileRef.getEntry(val) is IExportEntry exp)
                        {
                            s += $", Export: {exp.ObjectName}";
                        }
                        else if (CurrentLoadedEntry.FileRef.getEntry(val) is ImportEntry imp)
                        {
                            s += $", Import: {imp.ObjectName}";
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
            InfoTab_ExportDataSize_TextBox.Text = null;
            InfoTab_ExportOffsetHex_TextBox.Text = null;
            InfoTab_ExportOffsetDec_TextBox.Text = null;
            headerByteProvider.ClearBytes();
            loadingNewData = false;
        }


        public override void UnloadExport()
        {
            UnloadEntry();
        }

        private void UnloadEntry()
        {
            CurrentLoadedEntry = null;
        }

        internal void LoadPccData(IMEPackage pcc)
        {
            RefreshAllEntriesList(pcc);
        }

        private void Info_ClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData && InfoTab_Class_ComboBox.SelectedIndex >= 0)
            {
                var selectedClassIndex = InfoTab_Class_ComboBox.SelectedIndex;
                var unrealIndex = selectedClassIndex - CurrentLoadedEntry.FileRef.ImportCount;
                headerByteProvider.WriteBytes(HEADER_OFFSET_EXP_IDXCLASS, BitConverter.GetBytes(unrealIndex));
                Header_Hexbox.Refresh();
            }
        }

        private void InfoTab_Header_ByteProvider_InternalChanged(object sender, EventArgs e)
        {
            if (OriginalHeader != null)
            {
                HexChanged = !headerByteProvider.Bytes.SequenceEqual(OriginalHeader);
            }
        }

        private void Info_PackageLinkClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData && InfoTab_PackageLink_ComboBox.SelectedIndex >= 0)
            {
                var selectedImpExp = InfoTab_PackageLink_ComboBox.SelectedIndex;
                var unrealIndex = selectedImpExp - CurrentLoadedEntry.FileRef.ImportCount;
                headerByteProvider.WriteBytes(CurrentLoadedEntry is ExportEntry ? HEADER_OFFSET_EXP_IDXLINK : HEADER_OFFSET_IMP_IDXLINK, BitConverter.GetBytes(unrealIndex));
                Header_Hexbox.Refresh();
            }
        }

        private void Info_SuperClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData && InfoTab_Superclass_ComboBox.SelectedIndex >= 0)
            {
                var selectedClassIndex = InfoTab_Superclass_ComboBox.SelectedIndex;
                var unrealIndex = selectedClassIndex - CurrentLoadedEntry.FileRef.ImportCount;
                headerByteProvider.WriteBytes(HEADER_OFFSET_EXP_IDXSUPERCLASS, BitConverter.GetBytes(unrealIndex));
                Header_Hexbox.Refresh();
            }
        }

        private void Info_ObjectNameComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData && InfoTab_Objectname_ComboBox.SelectedIndex >= 0)
            {
                var selectedNameIndex = InfoTab_Objectname_ComboBox.SelectedIndex;
                if (selectedNameIndex >= 0)
                {
                    headerByteProvider.WriteBytes(CurrentLoadedEntry is ExportEntry ? HEADER_OFFSET_EXP_IDXOBJECTNAME : HEADER_OFFSET_IMP_IDXOBJECTNAME, BitConverter.GetBytes(selectedNameIndex));
                    Header_Hexbox.Refresh();
                }
            }
        }

        private void Info_IndexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!loadingNewData)
            {
                if (int.TryParse(InfoTab_ObjectnameIndex_TextBox.Text, out int x))
                {
                    headerByteProvider.WriteBytes(CurrentLoadedEntry is ExportEntry ? HEADER_OFFSET_EXP_IDXOBJECTNAME + 4 : HEADER_OFFSET_IMP_IDXOBJECTNAME + 4, BitConverter.GetBytes(x));
                    Header_Hexbox.Refresh();
                }
            }
        }

        private void Info_ArchetypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData && InfoTab_Archetype_ComboBox.SelectedIndex >= 0)
            {
                var selectedArchetTypeIndex = InfoTab_Archetype_ComboBox.SelectedIndex;
                var unrealIndex = selectedArchetTypeIndex - CurrentLoadedEntry.FileRef.ImportCount;
                headerByteProvider.WriteBytes(HEADER_OFFSET_EXP_IDXARCHETYPE, BitConverter.GetBytes(unrealIndex));
                Header_Hexbox.Refresh();
            }
        }

        private void Info_PackageFileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData && InfoTab_PackageFile_ComboBox.SelectedIndex >= 0)
            {
                var selectedNameIndex = InfoTab_PackageFile_ComboBox.SelectedIndex;
                headerByteProvider.WriteBytes(HEADER_OFFSET_IMP_IDXPACKAGEFILE, BitConverter.GetBytes(selectedNameIndex));
                Header_Hexbox.Refresh();
            }
        }

        private void Info_ImpClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData && InfoTab_ImpClass_ComboBox.SelectedIndex >= 0)
            {
                var selectedNameIndex = InfoTab_ImpClass_ComboBox.SelectedIndex;
                headerByteProvider.WriteBytes(HEADER_OFFSET_IMP_IDXCLASSNAME, BitConverter.GetBytes(selectedNameIndex));
                Header_Hexbox.Refresh();
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
                EPropertyFlags newFlags = 0U;
                foreach (var flag in InfoTab_Flags_ComboBox.Items)
                {
                    if (InfoTab_Flags_ComboBox.ItemContainerGenerator.ContainerFromItem(flag) is SelectorItem selectorItem && selectorItem.IsSelected != true)
                    {
                        newFlags |= (EPropertyFlags)flag;
                    }
                }
                //Debug.WriteLine(newFlags);
                headerByteProvider.WriteBytes(HEADER_OFFSET_EXP_OBJECTFLAGS, BitConverter.GetBytes((ulong)newFlags));
                Header_Hexbox.Refresh();
            }
        }

        private void MetadataEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (!ControlLoaded)
            {
                Debug.WriteLine("MDE HB LOADED");
                Header_Hexbox = (HexBox)Header_Hexbox_Host.Child;
                headerByteProvider = new DynamicByteProvider();
                Header_Hexbox.ByteProvider = headerByteProvider;
                if (CurrentLoadedEntry != null) headerByteProvider.ReplaceBytes(CurrentLoadedEntry.Header);
                headerByteProvider.Changed += InfoTab_Header_ByteProvider_InternalChanged;
                ControlLoaded = true;
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
                    string result = PromptDialog.Prompt(Window.GetWindow(this), input, "Enter new name", text);
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
            Header_Hexbox = null;
            Header_Hexbox_Host.Child.Dispose();
            Header_Hexbox_Host.Dispose();
        }

        /// <summary>
        /// This class is used when stuffing into the list. It makes "0" searchable by having the UIndex property.
        /// </summary>
        private class ZeroUIndexClassEntry
        {
            public static readonly ZeroUIndexClassEntry instance = new ZeroUIndexClassEntry();

            private ZeroUIndexClassEntry() { }

            public override string ToString() => "0: Class";

            public int UIndex => 0;
        }
    }
}
