using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
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
        private string currentFile;
        private List<int> ClassNames;

        public PackageEditorWPF()
        {
            CurrentView = View.Exports;
            InitializeComponent();
        }

        private void OpenFile_Clicked(object sender, RoutedEventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog { Filter = App.FileFilter };
            bool? result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
                try
                {
                    LoadFile(d.FileName);
                    //AddRecent(d.FileName, false);
                    //SaveRecentList();
                    //RefreshRecent(true, RFiles);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open file:\n" + ex.Message);
                }
            }
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

        private void RefreshView()
        {
            if (pcc == null)
            {
                return;
            }
            //listBox1.BeginUpdate();
            //treeView1.BeginUpdate();
            LeftSide_ListView.ItemsSource = null;
            LeftSide_ListView.Items.Clear();
            IReadOnlyList<ImportEntry> imports = pcc.Imports;
            IReadOnlyList<IExportEntry> Exports = pcc.Exports;
            if (CurrentView == View.Names)
            {
                LeftSide_ListView.ItemsSource = pcc.Names;
            }

            if (CurrentView == View.Imports)
            {
                List<string> importsList = new List<string>();
                for (int i = 0; i < imports.Count; i++)
                {
                    string importStr = i + " (0x" + (pcc.ImportOffset + (i * ImportEntry.byteSize)).ToString("X4") + "): (" + imports[i].PackageFile + ") ";
                    if (imports[i].PackageFullName != "Class" && imports[i].PackageFullName != "Package")
                    {
                        importStr += imports[i].PackageFullName + ".";
                    }
                    importStr += imports[i].ObjectName;
                    importsList.Add(importStr);
                }
                LeftSide_ListView.Items.Add(importsList);
            }

            if (CurrentView == View.Exports)
            {
                List<string> exps = new List<string>(Exports.Count);
                for (int i = 0; i < Exports.Count; i++)
                {
                    string s = $"{i}:";
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
                LeftSide_ListView.ItemsSource = exps.ToArray();
            }
            if (CurrentView == View.Tree)
            {
                /*listBox1.Visible = false;
                treeView1.Visible = true;
                treeView1.Nodes.Clear();
                int importsOffset = Exports.Count;
                int link;
                List<TreeNode> nodeList = new List<TreeNode>(Exports.Count + imports.Count + 1)
                {
                    new TreeNode(pcc.FileName) { Tag = true }
                };
                for (int i = 0; i < Exports.Count; i++)
                {
                    nodeList.Add(new TreeNode($"(Exp){i} : {Exports[i].ObjectName}({Exports[i].ClassName})")
                    {
                        Name = i.ToString()
                    });
                }
                for (int i = 0; i < imports.Count; i++)
                {
                    nodeList.Add(new TreeNode($"(Imp){i} : {imports[i].ObjectName}({imports[i].ClassName})")
                    {
                        Name = (-i - 1).ToString()
                    });
                }
                TreeNode node;
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
                        nodeList[link].Nodes.Add(node);
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
                        nodeList[link].Nodes.Add(node);
                        node = nodeList[link];
                    }
                }
                treeView1.Nodes.Add(nodeList[0]);
                treeView1.Nodes[0].Expand();
            }
            else
            {
                treeView1.Visible = false;
                listBox1.Visible = true;
            }
            treeView1.EndUpdate();
            listBox1.EndUpdate();*/
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
                ClassNames.Add(Exports[i].idxClass);
            }
            List<string> names = ClassNames.Distinct().Select(pcc.getObjectName).ToList();
            names.Sort();
            ClassDropdown_Combobox.Items.Clear();
            ClassDropdown_Combobox.ItemsSource = names.ToArray();
        }


        private void TreeView_Click(object sender, RoutedEventArgs e)
        {
            SetView(View.Tree);
            RefreshView();
        }
        private void NamesView_Click(object sender, RoutedEventArgs e)
        {
            SetView(View.Tree);
            RefreshView();
        }
        private void ImportsView_Click(object sender, RoutedEventArgs e)
        {
            SetView(View.Tree);
            RefreshView();
        }
        private void ExportsView_Click(object sender, RoutedEventArgs e)
        {
            SetView(View.Tree);
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

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            //throw new NotImplementedException();
        }
    }
}