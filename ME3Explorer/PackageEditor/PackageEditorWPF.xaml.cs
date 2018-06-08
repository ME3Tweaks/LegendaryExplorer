using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using Microsoft.Win32;
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
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for PackageEditorWPF.xaml
    /// </summary>
    public partial class PackageEditorWPF : WPFBase
    {
        enum View
        {
            Names,
            Imports,
            Exports,
            Tree
        }

        View CurrentView;
        public PropGrid pg;

        public static readonly string PackageEditorDataFolder = System.IO.Path.Combine(App.AppDataFolder, @"PackageEditor\");
        private readonly string RECENTFILES_FILE = "RECENTFILES";
        public List<string> RFiles;
        private string currentFile;
        private List<int> ClassNames;

        public PackageEditorWPF()
        {
            CurrentView = View.Tree;
            InitializeComponent();

            LoadRecentList();
            RefreshRecent(false);
        }

        private void OpenFile_Clicked(object sender, RoutedEventArgs e)
        {
            OpenFile();
        }

        private void OpenFile()
        {

        }

        private void LoadFile(string s)
        {
            try
            {
                currentFile = s;
                StatusBar_LeftMostText.Text = "Loading " + System.IO.Path.GetFileName(s);
                LoadMEPackage(s);

                /*interpreterControl.Pcc = pcc;
                binaryInterpreterControl.Pcc = pcc;
                bio2DAEditor1.Pcc = pcc;
                treeView1.Tag = pcc;*/
                RefreshView();
                InitStuff();
                StatusBar_LeftMostText.Text = System.IO.Path.GetFileName(s);
            }
            catch (Exception e)
            {
                StatusBar_LeftMostText.Text = "Failed to load " + System.IO.Path.GetFileName(s);
                MessageBox.Show("Error loading " + System.IO.Path.GetFileName(s) + ":\n" + e.Message);
            }
        }

        private void LoadRecentList()
        {
            Recents_MenuItem.IsEnabled = false;
            RFiles = new List<string>();
            RFiles.Clear();
            string path = PackageEditorDataFolder + RECENTFILES_FILE;
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
        }

        private void SaveRecentList()
        {
            if (!Directory.Exists(PackageEditorDataFolder))
            {
                Directory.CreateDirectory(PackageEditorDataFolder);
            }
            string path = PackageEditorDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
                File.Delete(path);
            File.WriteAllLines(path, RFiles);
        }

        private void RefreshRecent(bool propogate, List<string> recents = null)
        {
            if (propogate && recents != null)
            {
                //we are posting an update to other instances of packed

                //This code can be removed when non-WPF package editor is removed.
                var forms = System.Windows.Forms.Application.OpenForms;
                foreach (System.Windows.Forms.Form form in forms)
                {
                    if (form is PackageEditor) //it will never be "this"
                    {
                        ((PackageEditor)form).RefreshRecent(false, RFiles);
                    }
                }
                foreach (var form in App.Current.Windows)
                {
                    if (form is PackageEditorWPF && this != form)
                    {
                        ((PackageEditorWPF)form).RefreshRecent(false, RFiles);
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


            foreach (string filepath in RFiles)
            {
                MenuItem fr = new MenuItem()
                {
                    Header = filepath.Replace("_","__"),
                    Tag = filepath
                };
                fr.Click += RecentFile_click;
                Recents_MenuItem.Items.Add(fr);
            }
        }

        private void RecentFile_click(object sender, EventArgs e)
        {
            string s = ((MenuItem)sender).Tag.ToString();
            if (File.Exists(s))
            {
                LoadFile(s);
                AddRecent(s, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
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

        private void RefreshView()
        {
            if (pcc == null)
            {
                return;
            }
            //listBox1.BeginUpdate();
            //treeView1.BeginUpdate();
            ClearList(LeftSide_ListView);
            IReadOnlyList<ImportEntry> imports = pcc.Imports;
            IReadOnlyList<IExportEntry> Exports = pcc.Exports;
            if (CurrentView == View.Names)
            {
                LeftSide_ListView.ItemsSource = pcc.Names;
            }

            if (CurrentView == View.Imports)
            {
                List<string> importsList = new List<string>();
                int padding = imports.Count.ToString().Length;

                for (int i = 0; i < imports.Count; i++)
                {
                    //" (0x" + (pcc.ImportOffset + (i * ImportEntry.byteSize)).ToString("X4") + ")\
                    string importStr = $"{ i.ToString().PadLeft(padding, '0')}: {imports[i].GetFullPath}";
                    /*if (imports[i].PackageFullName != "Class" && imports[i].PackageFullName != "Package")
                    {
                        importStr += imports[i].PackageFullName + ".";
                    }
                    importStr += imports[i].ObjectName;*/
                    importsList.Add(importStr);
                }
                LeftSide_ListView.ItemsSource = importsList;
            }

            if (CurrentView == View.Exports)
            {
                List<string> exps = new List<string>(Exports.Count);
                int padding = Exports.Count.ToString().Length;
                for (int i = 0; i < Exports.Count; i++)
                {
                    string s = $"{i.ToString().PadLeft(padding, '0')}: ";
                    IExportEntry exp = pcc.getExport(i);
                    string PackageFullName = exp.PackageFullName;
                    if (PackageFullName != "Class" && PackageFullName != "Package")
                        s += PackageFullName + ".";
                    s += exp.ObjectName;
                    string ClassName = exp.ClassName;
                    if (ClassName == "ObjectProperty" || ClassName == "StructProperty")
                    {
                        //attempt to find type
                        byte[] data = exp.Data;
                        int importindex = BitConverter.ToInt32(data, data.Length - 4);
                        if (importindex < 0)
                        {
                            //import
                            importindex *= -1;
                            if (importindex > 0) importindex--;
                            if (importindex <= imports.Count)
                            {
                                s += " (" + imports[importindex].ObjectName + ")";
                            }
                        }
                        else
                        {
                            //export
                            if (importindex > 0) importindex--;
                            if (importindex <= Exports.Count)
                            {
                                s += " [" + Exports[importindex].ObjectName + "]";
                            }
                        }
                    }
                    exps.Add(s);
                }
                LeftSide_ListView.ItemsSource = exps;
            }
            if (CurrentView == View.Tree)
            {
                LeftSide_ListView.Visibility = Visibility.Collapsed;
                LeftSide_TreeView.Visibility = Visibility.Visible;
                ClearList(LeftSide_TreeView);

                int importsOffset = Exports.Count;
                int link;
                List<TreeViewItem> nodeList = new List<TreeViewItem>(Exports.Count + imports.Count + 1)
                {
                    new TreeViewItem() { Header = pcc.FileName, Tag = true }
                };


                for (int i = 0; i < Exports.Count; i++)
                {
                    nodeList.Add(new TreeViewItem()
                    {
                        Header = $"(Exp) {i} : {Exports[i].ObjectName}({Exports[i].ClassName})",
                        Name = $"_{i}", //must start letter or _
                        Foreground = Brushes.Black
                    });
                }

                for (int i = 0; i < imports.Count; i++)
                {
                    nodeList.Add(new TreeViewItem()
                    {
                        Header = $"(Imp) {i} : {imports[i].ObjectName}({imports[i].ClassName})",
                        Name = $"_n{i + 1}", //must start letter or _,
                        Foreground = Brushes.Gray
                    });
                }

                TreeViewItem node;
                int curIndex;
                for (int i = 1; i <= Exports.Count; i++)
                {
                    node = nodeList[i];
                    curIndex = i;
                    while (node.Tag as bool? != true)
                    {
                        node.Tag = true;
                        //Debug.WriteLine(curIndex);
                        curIndex = pcc.getEntry(curIndex).idxLink;
                        link = curIndex >= 0 ? curIndex : (-curIndex + importsOffset);
                        nodeList[link].Items.Add(node);
                        node = nodeList[link];
                    }
                }

                for (int i = 1; i <= imports.Count; i++)
                {
                    node = nodeList[i + importsOffset];
                    curIndex = -i;
                    while (node.Tag as bool? != true)
                    {
                        node.Tag = true;
                        curIndex = pcc.getEntry(curIndex).idxLink;
                        link = curIndex >= 0 ? curIndex : (-curIndex + importsOffset);
                        nodeList[link].Items.Add(node);
                        node = nodeList[link];
                    }
                }
                LeftSide_TreeView.Items.Add(nodeList[0]);
                nodeList[0].IsExpanded = true;
                /*LeftSide_TreeView.Items[0].Expand();
                */
            }
            else
            {
                LeftSide_ListView.Visibility = Visibility.Visible;
                LeftSide_TreeView.Visibility = Visibility.Collapsed;
            }

        }

        public void InitStuff()
        {
            if (pcc == null)
                return;
            ClassNames = new List<int>();
            IReadOnlyList<IExportEntry> Exports = pcc.Exports;
            for (int i = 0; i < Exports.Count; i++)
            {
                ClassNames.Add(Exports[i].idxClass); //This can probably be linq'd
            }
            List<string> names = ClassNames.Distinct().Select(pcc.getObjectName).ToList();
            names.Sort();
            ClearList(ClassDropdown_Combobox);
            ClassDropdown_Combobox.ItemsSource = names.ToArray();
            InfoTab_Objectname_ComboBox.ItemsSource = pcc.Names;
        }

        /// <summary>
        /// Clears the itemsource or items property of the passed in control.
        /// </summary>
        /// <param name="control">ItemsControl to clear all entries from.</param>
        private void ClearList(ItemsControl control)
        {
            if (control.ItemsSource != null)
            {
                control.ItemsSource = null;
            }
            else
            {
                control.Items.Clear();
            }
        }

        private void TreeView_Click(object sender, RoutedEventArgs e)
        {
            SetView(View.Tree);
            RefreshView();
        }
        private void NamesView_Click(object sender, RoutedEventArgs e)
        {
            SetView(View.Names);
            RefreshView();
        }
        private void ImportsView_Click(object sender, RoutedEventArgs e)
        {
            SetView(View.Imports);
            RefreshView();
        }
        private void ExportsView_Click(object sender, RoutedEventArgs e)
        {
            SetView(View.Exports);
            RefreshView();
        }

        void SetView(View n)
        {
            CurrentView = n;
            /*switch (n)
            {
                case View.Names:
                    Names_Button.Checked = true;
                    Button2.Checked = false;
                    Button3.Checked = false;
                    Button5.Checked = false;
                    break;
                case View.Imports:
                    Button1.Checked = false;
                    Button2.Checked = true;
                    Button3.Checked = false;
                    Button5.Checked = false;
                    break;
                case View.Tree:
                    Button1.Checked = false;
                    Button2.Checked = false;
                    Button3.Checked = false;
                    Button5.Checked = true;
                    break;
                case View.Exports:
                default:
                    Button1.Checked = false;
                    Button2.Checked = false;
                    Button3.Checked = true;
                    Button5.Checked = false;
                    break;
            }*/
        }

        public void PreviewInfo(int n)
        {
            if (n >= 0)
            {
                try
                {
                    /*infoHeaderBox.Text = "Export Header";
                    superclassTextBox.Visible = superclassLabel.Visible = true;
                    archetypeBox.Visible = label6.Visible = true;
                    indexBox.Visible = label5.Visible = true;
                    flagsBox.Visible = label11.Visible = false;
                    infoExportDataBox.Visible = true;*/
                    IExportEntry exportEntry = pcc.getExport(n);
                    //InfoTab_Objectname_TextBox.Text = exportEntry.ObjectName;
                    InfoTab_Objectname_ComboBox.SelectedItem = exportEntry.ObjectName;
                    //IEntry _class = pcc.getEntry(exportEntry.idxClass);
                    //
                    IReadOnlyList<ImportEntry> imports = pcc.Imports;

                    List<string> Classes = new List<string>();
                    for (int i = imports.Count - 1; i >= 0; i--)
                    {
                        Classes.Add($"{-i + 1}: {imports[i].GetFullPath}");
                    }
                    Classes.Add("0 : Class");
                    int count = 1;
                    IReadOnlyList<IExportEntry> Exports = pcc.Exports;
                    foreach (IExportEntry exp in Exports)
                    {

                        Classes.Add($"{count++}: {exp.GetFullPath}");
                    }

                    if (exportEntry.idxClass != 0)
                    {
                        //IEntry _class = pcc.getEntry(exportEntry.idxClass);
                        InfoTab_Class_ComboBox.ItemsSource = Classes;
                        InfoTab_Class_ComboBox.SelectedIndex = exportEntry.idxClass + imports.Count; //make positive
                    }
                    else
                    {
                        InfoTab_Class_ComboBox.SelectedIndex = imports.Count; //Class, 0
                    }
                    //classNameBox.Text = exportEntry.ClassName; //this seems to override the code directly above?
                    //InfoTab_Superclass_TextBox.Text = exportEntry.ClassParent;
                    //InfoTab_Packagename_TextBox.Text = exportEntry.PackageFullName;
                    InfoTab_Superclass_ComboBox.ItemsSource = Classes;
                    if (exportEntry.idxClassParent != 0)
                    {
                        //IEntry _class = pcc.getEntry(exportEntry.idxClass);
                        InfoTab_Superclass_ComboBox.SelectedIndex = exportEntry.idxClassParent + imports.Count; //make positive
                    }
                    else
                    {
                        InfoTab_Superclass_ComboBox.SelectedIndex = imports.Count; //Class, 0
                    }

                    InfoTab_PackageLink_ComboBox.ItemsSource = Classes;
                    if (exportEntry.idxLink != 0)
                    {
                        //IEntry _class = pcc.getEntry(exportEntry.idxClass);
                        InfoTab_PackageLink_ComboBox.SelectedIndex = exportEntry.idxLink + imports.Count; //make positive
                    }
                    else
                    {
                        InfoTab_PackageLink_ComboBox.SelectedIndex = imports.Count; //Class, 0
                    }

                    InfoTab_Headersize_TextBox.Text = exportEntry.header.Length + " bytes";
                    InfoTab_Index_TextBox.Text = exportEntry.indexValue.ToString();
                    //InfoTab_Archetypename_TextBox.Text = exportEntry.ArchtypeName;

                    //if (exportEntry.idxArchtype != 0)
                    //{
                    //    InfoTab_Archetypename_TextBox.Text = archetype.PackageFullName + "." + archetype.ObjectName;
                    //    InfoTab_Archetypename_TextBox.Text += " (" + (exportEntry.idxArchtype < 0 ? "imported" : "local") + " class) " + exportEntry.idxArchtype;
                    //}
                    InfoTab_Archetype_ComboBox.ItemsSource = Classes;
                    if (exportEntry.idxArchtype != 0)
                    {
                        InfoTab_Archetype_ComboBox.SelectedIndex = exportEntry.idxArchtype + imports.Count; //make positive
                    }
                    else
                    {
                        InfoTab_Archetype_ComboBox.SelectedIndex = imports.Count; //Class, 0
                    }
                    InfoTab_Flags_TextBox.Text = "0x" + exportEntry.ObjectFlags.ToString("X16");
                    InfoTab_ExportDataSize_TextBox.Text = exportEntry.DataSize + " bytes";
                    InfoTab_ExportOffsetHex_TextBox.Text = "0x" + exportEntry.DataOffset.ToString("X8");
                    InfoTab_ExportOffsetDec_TextBox.Text = exportEntry.DataOffset.ToString();
                }
                catch (Exception e)
                {
                    MessageBox.Show("An error occured while attempting to read the header for this export. This indicates there is likely something wrong with the header or its parent header.\n\n" + e.Message);
                }
            }
            else
            {
                /* n = -n - 1;
                 infoHeaderBox.Text = "Import Header";
                 superclassTextBox.Visible = superclassLabel.Visible = false;
                 archetypeBox.Visible = label6.Visible = false;
                 indexBox.Visible = label5.Visible = false;
                 flagsBox.Visible = label11.Visible = false;
                 infoExportDataBox.Visible = false;
                 ImportEntry importEntry = pcc.getImport(n);
                 objectNameBox.Text = importEntry.ObjectName;
                 classNameBox.Text = importEntry.ClassName;
                 packageNameBox.Text = importEntry.PackageFullName;
                 headerSizeBox.Text = ImportEntry.byteSize + " bytes";*/
            }
        }

        private bool GetSelected(out int n)
        {
            if (CurrentView == View.Tree && LeftSide_TreeView.SelectedItem != null && ((TreeViewItem)LeftSide_TreeView.SelectedItem).Name.StartsWith("_"))
            {
                string name = ((TreeViewItem)LeftSide_TreeView.SelectedItem).Name.Substring(1); //get rid of _
                if (name.StartsWith("n"))
                {
                    //its negative
                    name = $"-{name.Substring(1)}";
                }
                n = Convert.ToInt32(name);
                return true;
            }
            else if (CurrentView == View.Exports && LeftSide_ListView.SelectedItem != null)
            {
                n = LeftSide_ListView.SelectedIndex;
                return true;
            }
            else if (CurrentView == View.Imports && LeftSide_ListView.SelectedItem != null)
            {
                n = -LeftSide_ListView.SelectedIndex - 1;
                return true;
            }
            else
            {
                n = 0;
                return false;
            }
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            //throw new NotImplementedException();
        }


        private delegate void NoArgDelegate();
        /// <summary>
        /// Tree view selected item changed. This runs in a delegate due to how multithread bubble-up items work with treeview.
        /// Without this delegate, the item selected will randomly be a parent item instead.
        /// From https://www.codeproject.com/Tips/208896/WPF-TreeView-SelectedItemChanged-called-twice
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LeftSide_SelectedItemChanged(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background,
        (NoArgDelegate)delegate { Preview(); });
        }

        /// <summary>
        /// Listbox selected item changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LeftSide_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            Preview();
            e.Handled = true;
        }

        private void Preview(bool isRefresh = false)
        {
            if (!GetSelected(out int n))
            {
                return;
            }
            Debug.WriteLine("New selection: " + n);

            if (CurrentView == View.Imports || CurrentView == View.Exports || CurrentView == View.Tree)
            {
                //tabControl1_SelectedIndexChanged(null, null);
                PreviewInfo(n);
                Info_HeaderRaw_Hexbox.Stream = new System.IO.MemoryStream(pcc.getEntry(n.ToUnrealIdx()).header);
                //RefreshMetaData();
                //export
                if (n >= 0)
                {
                    /*PreviewProps(n);
                     if (!packageEditorTabPane.TabPages.ContainsKey(nameof(propertiesTab)))
                     {
                         packageEditorTabPane.TabPages.Insert(0, propertiesTab);
                     }
                     if (!packageEditorTabPane.TabPages.ContainsKey(nameof(interpreterTab)))
                     {
                         packageEditorTabPane.TabPages.Insert(1, interpreterTab);
                     }*/

                    IExportEntry exportEntry = pcc.getExport(n);
                    Script_Tab.Visibility = exportEntry.ClassName == "Function" ? Visibility.Visible : Visibility.Collapsed;
                    if (exportEntry.ClassName == "Function")
                    {
                        /*
                                                if (!Script_Tab.TabPages.ContainsKey(nameof(scriptTab)))
                                                {
                                                    packageEditorTabPane.TabPages.Add(scriptTab);
                                                }*/
                        if (pcc.Game == MEGame.ME3)
                        {
                            Function func = new Function(exportEntry.Data, pcc);
                            Script_TextBox.Text = func.ToRawText();
                        }
                        else if (pcc.Game == MEGame.ME1)
                        {
                            ME1Explorer.Unreal.Classes.Function func = new ME1Explorer.Unreal.Classes.Function(exportEntry.Data, pcc as ME1Package);
                            try
                            {
                                Script_TextBox.Text = func.ToRawText();
                            }
                            catch (Exception e)
                            {
                                Script_TextBox.Text = "Error parsing function: " + e.Message;
                            }
                        }
                        else
                        {
                            Script_TextBox.Text = "Parsing UnrealScript Functions for this game is not supported.";
                        }
                    }

                    /*
                    else if (packageEditorTabPane.TabPages.ContainsKey(nameof(scriptTab)))
                    {
                        packageEditorTabPane.TabPages.Remove(scriptTab);
                    }

                    if (BinaryInterpreter.ParsableBinaryClasses.Contains(exportEntry.ClassName))
                    {
                        if (!packageEditorTabPane.TabPages.ContainsKey(nameof(binaryEditorTab)))
                        {
                            packageEditorTabPane.TabPages.Add(binaryEditorTab);
                        }
                    }
                    else
                    {
                        removeBinaryTabPane();
                    }

                    if (Bio2DAEditor.ParsableBinaryClasses.Contains(exportEntry.ClassName) && !exportEntry.ObjectName.StartsWith("Default__"))
                    {
                        if (!packageEditorTabPane.TabPages.ContainsKey(nameof(bio2daEditorTab)))
                        {
                            packageEditorTabPane.TabPages.Add(bio2daEditorTab);
                        }
                    }
                    else
                    {
                        if (packageEditorTabPane.TabPages.ContainsKey(nameof(bio2daEditorTab)))
                        {
                            packageEditorTabPane.TabPages.Remove(bio2daEditorTab);
                        }
                    }

                    //headerRawHexBox.ByteProvider = new DynamicByteProvider(exportEntry.header);*/
                    if (!isRefresh)
                    {
                        InterpreterTab_Interpreter.loadNewExport(exportEntry);
                        //interpreterControl.export = exportEntry;
                        //interpreterControl.InitInterpreter();

                        if (BinaryInterpreter.ParsableBinaryClasses.Contains(exportEntry.ClassName))
                        {
                            if (exportEntry.ClassName == "Class" && exportEntry.ObjectName.StartsWith("Default__"))
                            {
                                //do nothing, this class is not actually a class.
                                BinaryInterpreter_Tab.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                //binaryInterpreterControl.export = exportEntry;
                                //binaryInterpreterControl.InitInterpreter();
                                BinaryInterpreter_Tab.Visibility = Visibility.Collapsed;

                            }
                        }
                        if (Bio2DAEditor.ParsableBinaryClasses.Contains(exportEntry.ClassName) && !exportEntry.ObjectName.StartsWith("Default__"))
                        {
                            Bio2DAViewer_Tab.Visibility = Visibility.Visible;
                            //bio2DAEditor1.export = exportEntry;
                            //bio2DAEditor1.InitInterpreter();
                        }
                        else
                        {
                            Bio2DAViewer_Tab.Visibility = Visibility.Collapsed;
                        }
                    }
                }
                //import
                else
                {
                    /*   n = -n - 1;
                       headerRawHexBox.ByteProvider = new DynamicByteProvider(pcc.getImport(n).header);
                       UpdateStatusIm(n);
                       if (packageEditorTabPane.TabPages.ContainsKey(nameof(interpreterTab)))
                       {
                           packageEditorTabPane.TabPages.Remove(interpreterTab);
                       }
                       if (packageEditorTabPane.TabPages.ContainsKey(nameof(propertiesTab)))
                       {
                           packageEditorTabPane.TabPages.Remove(propertiesTab);
                       }
                       if (packageEditorTabPane.TabPages.ContainsKey(nameof(scriptTab)))
                       {
                           packageEditorTabPane.TabPages.Remove(scriptTab);
                       }
                       if (packageEditorTabPane.TabPages.ContainsKey(nameof(binaryEditorTab)))
                       {
                           packageEditorTabPane.TabPages.Remove(binaryEditorTab);
                       }
                   }
                   */
                }
            }
        }

        private void OpenCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog { Filter = App.FileFilter };
            bool? result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
                try
                {
                    LoadFile(d.FileName);
                    AddRecent(d.FileName, false);
                    SaveRecentList();
                    RefreshRecent(true, RFiles);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open file:\n" + ex.Message);
                }
            }
        }

        private void SaveCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            //save();
        }

        private void SaveAsCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            //saveAs();
        }
    }
}