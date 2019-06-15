using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using static ME3Explorer.TlkManagerNS.TLKManagerWPF;
using static ME3Explorer.Dialogue_Editor.BioConversationExtended;

namespace ME3Explorer.Dialogue_Editor
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class LinkEditor : Window
    {
        public Enum ERCategory;
        private DialogueEditorWPF ParentWindow;
        private DiagNode Dnode;
        private bool IsReply;
        public ObservableCollectionExtended<ReplyChoiceNode> linkTable { get; } = new ObservableCollectionExtended<ReplyChoiceNode>();
        private bool NeedsSave;
        public bool NeedsPush;
        public ICommand FinishedCommand { get; set; }
        public ICommand AddCommand { get; set; }
        public ICommand DeleteCommand { get; set; }
        public ICommand UpCommand { get; set; }
        public ICommand DownCommand { get; set; }
        public ICommand EditCommand { get; set; }

        public LinkEditor(DialogueEditorWPF owner, DiagNode node)
        {
            ParentWindow = owner;
            if (ParentWindow.SelectedDialogueNode == null)
            {
                Close();
                throw new Exception("ListEd couldn't find node.");
            }
            LoadCommands();
            InitializeComponent();

            Dnode = node;
            IsReply = Dnode.Node.IsReply;
            if (IsReply)
                this.Width = 700;

            string s = "E";
            int id = Dnode.NodeID;
            if (IsReply)
            {
                //outType_TB.Text = "E";
                s = "R";
                id -= 1000;
            }

            Title = $"Link Editor - {s}{id} : {Dnode.Node.LineStrRef}";
            LineString_TextBlock.Text = Dnode.Node.Line;

            int n = 0;
            foreach (var link in Dnode.Links)
            {
                link.Order = n;
                ParseLink(link);
                linkTable.Add(link);
                n++;
            }
            GenerateTable();
        }

        private void LoadCommands()
        {
            FinishedCommand = new GenericCommand(Close_LinkEditor);
            AddCommand = new GenericCommand(CloneLink, HasActiveLink);
            EditCommand = new GenericCommand(EditItem, HasActiveLink);
            DeleteCommand = new GenericCommand(DeleteLink, HasActiveLink);
            UpCommand = new RelayCommand(MoveLink);
            DownCommand = new RelayCommand(MoveLink);
        }

        private void ParseLink(ReplyChoiceNode link)
        {
            string a = "E";
            int tgtUID = link.Index;
            if (!IsReply)
            {
                a = "R";
                tgtUID += 1000;
            }
            link.NodeIDLink = $"{a}{link.Index}";

            link.ReplyLine = GlobalFindStrRefbyID(link.ReplyStrRef, ParentWindow.Pcc);

            var tgtObj = ParentWindow.CurrentObjects.FirstOrDefault(t => t.NodeUID == tgtUID);
            var tgtNode = tgtObj as DiagNode;

            link.TgtFireCnd = "Conditional";
            if (!tgtNode.Node.FiresConditional)
                link.TgtFireCnd = "Bool";

            link.TgtCondition = tgtNode.Node.ConditionalOrBool;
            link.TgtLine = tgtNode.Node.Line;
            link.Ordinal = DialogueEditorWPF.AddOrdinal(link.Order + 1);
            link.TgtSpeaker = tgtNode.Node.SpeakerTag.SpeakerName;
        }

        private void GenerateTable()
        {
            
            datagrid_Links.ItemsSource = linkTable;

            var clnO = new DataGridTextColumn();
            clnO.Header = "#";
            clnO.Binding = new Binding("Ordinal");
            clnO.Width = 30;
            clnO.IsReadOnly = true;
            clnO.Foreground = Brushes.DarkSlateGray;
            datagrid_Links.Columns.Add(clnO);

            var clnA = new DataGridTextColumn();
            clnA.Header = "Link";
            clnA.Binding = new Binding("NodeIDLink");
            clnA.IsReadOnly = true;
            clnA.Width = 40;
            clnA.FontWeight = FontWeights.Heavy;
            datagrid_Links.Columns.Add(clnA);

            if(!IsReply)
            {
                var clnB = new DataGridTextColumn();
                clnB.Header = "GUI StrRef";
                clnB.Binding = new Binding("ReplyStrRef");
                clnB.IsReadOnly = false;
                clnB.Width = 70;
                clnB.FontWeight = FontWeights.Bold;
                datagrid_Links.Columns.Add(clnB);

                var clnC = new DataGridTextColumn();
                clnC.Header = "GUI Choice Line";
                clnC.Binding = new Binding("ReplyLine");
                clnC.IsReadOnly = true;
                clnC.Width = 120;
                clnC.Foreground = Brushes.DarkSlateGray;
                datagrid_Links.Columns.Add(clnC);


                var clnD = new DataGridComboBoxColumn();
                clnD.Header = "GUI Category";
                clnD.ItemsSource = Enum.GetValues(typeof(EReplyCategory)).Cast<EReplyCategory>();
                clnD.SelectedItemBinding = new Binding("RCategory");
                clnD.IsReadOnly = false;
                clnD.Width = 150;
                clnB.FontWeight = FontWeights.Bold;
                datagrid_Links.Columns.Add(clnD);
            }

            var clnE = new DataGridTextColumn();
            clnE.Header = "Target Check";
            clnE.Binding = new Binding("TgtFireCnd");
            clnE.IsReadOnly = true;
            clnE.Width = 80;
            clnE.Foreground = Brushes.DarkSlateGray;
            datagrid_Links.Columns.Add(clnE);

            var clnF = new DataGridTextColumn();
            clnF.Header = "Plot Check";
            clnF.Binding = new Binding("TgtCondition");
            clnF.IsReadOnly = true;
            clnF.Width = 65;
            clnF.Foreground = Brushes.DarkSlateGray;
            datagrid_Links.Columns.Add(clnF);

            var clnH = new DataGridTextColumn();
            clnH.Header = "Speaker";
            clnH.Binding = new Binding("TgtSpeaker");
            clnH.IsReadOnly = true;
            clnH.Width = 60;
            clnH.Foreground = Brushes.DarkSlateGray;
            datagrid_Links.Columns.Add(clnH);

            var clnG = new DataGridTextColumn();
            clnG.Header = "Target Line";
            clnG.Binding = new Binding("TgtLine");
            clnG.IsReadOnly = true;
            clnG.Width = 355;
            clnG.Foreground = Brushes.DarkSlateGray;
            if (!IsReply)
            {
                clnG.Width = 200;
            }
            datagrid_Links.Columns.Add(clnG);

            datagrid_Links.MouseDoubleClick += Datagrid_Table_MouseDoubleClick;
        }

        private void Datagrid_Table_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EditItem();
        }

        private void EditItem()
        {
            if (!HasActiveLink())
                return;

            var editLink = linkTable[datagrid_Links.SelectedIndex];

            //Set new Link
            var links = new List<string>();
            int l = editLink.Index; //Get current link
            if (IsReply)
            {
                foreach (var entry in ParentWindow.SelectedConv.EntryList)
                {
                    links.Add($"{entry.NodeCount}: {entry.LineStrRef} {entry.Line}");
                }
            }
            else
            {
                foreach (var entry in ParentWindow.SelectedConv.ReplyList)
                {
                    links.Add($"{entry.NodeCount}: {entry.LineStrRef} {entry.Line}");
                }
            }
            var ldlg = InputComboBox.GetValue("Pick the next dialogue node to link to", links, links[l], false);

            if (ldlg == "")
                return;
            editLink.Index = links.FindIndex(ldlg.Equals);

            if(!IsReply)
            {
                //Set StrRef
                int strRef = 0;
                bool isNumber = false;
                while (!isNumber)
                {
                    var sdlg = new PromptDialog("Enter the TLK String Reference for the dialogue wheel:", "Link Editor", editLink.ReplyStrRef.ToString());
                    sdlg.Owner = this;
                    sdlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    sdlg.ShowDialog();
                    if (sdlg.ResponseText == null || sdlg.ResponseText == "0")
                        return;
                    isNumber = int.TryParse(sdlg.ResponseText, out strRef);
                    if (!isNumber || strRef <= 0)
                    {
                        var wdlg = MessageBox.Show("The string reference must be a positive whole number.", "Dialogue Editor", MessageBoxButton.OKCancel);
                        if (wdlg == MessageBoxResult.Cancel)
                            return;
                    }
                }
                editLink.ReplyStrRef = strRef;

                ////Set GUI Reply style
                //var eRCats = Enum.GetValues(typeof(EReplyCategory)).Cast<EReplyCategory>();
                //var rc = editLink.RCategory; //Get current link
            
                //var rdlg = InputComboBox.GetValue("Pick the wheel position or interrupt:", eRCats, rc.ToString(), false);

                //if (rdlg == "")
                //    return;
                //Enum.TryParse(rdlg.ToString(), out rc);
                //editLink.RCategory = rc;
            }
            ParseLink(editLink);
            ReOrderTable();
            NeedsSave = true;
        }

        private void ReOrderTable()
        {
            linkTable.Sort(l => l.Order);

            int n = 0;
            foreach(var link in linkTable )
            {
                link.Order = n;
                link.Ordinal = DialogueEditorWPF.AddOrdinal(link.Order + 1);
                n++;
            }
        }

        private void Close_LinkEditor()
        {
            SaveToProperties();
            Close();
        }

        private void SaveToProperties()
        {
            if (IsReply)
            {
                var newEntriesProp = new ArrayProperty<IntProperty>(ArrayType.Int, "EntryList");
                foreach (var link in linkTable)
                {
                    newEntriesProp.Add(new IntProperty(link.Index));
                }
                Dnode.NodeProp.Properties.AddOrReplaceProp(newEntriesProp);
            }
            else
            {
                var newReplyListProp = new ArrayProperty<StructProperty>(ArrayType.Struct, "ReplyListNew");

                foreach (var link in linkTable)
                {
                    var newProps = new PropertyCollection();
                    newProps.Add(new IntProperty(link.Index, "nIndex"));
                    newProps.Add(new StringRefProperty(link.ReplyStrRef, "srParaphrase"));
                    newProps.Add(new StrProperty("", "sParaphrase"));
                    newProps.Add(new EnumProperty(link.RCategory.ToString(), "EReplyCategory", ParentWindow.Pcc.Game, "Category"));
                    newProps.Add(new NoneProperty());
                    var newstruct = new StructProperty("BioDialogReplyListDetails", newProps);
                    newReplyListProp.Add(newstruct);
                }
                Dnode.NodeProp.Properties.AddOrReplaceProp(newReplyListProp);
            }
            NeedsPush = true;
            NeedsSave = false;
        }

        private bool HasActiveLink()
        {
            return datagrid_Links != null && datagrid_Links.SelectedIndex >= 0;
        }

        private void DeleteLink()
        {
            ReplyChoiceNode result = datagrid_Links.SelectedItem as ReplyChoiceNode;
            linkTable.Remove(result);
            NeedsSave = true;
            ReOrderTable();
        }

        private void CloneLink()
        {
            var donor = linkTable[datagrid_Links.SelectedIndex];
            ReplyChoiceNode newlink = new ReplyChoiceNode(donor.Order, donor.Index, donor.Paraphrase, donor.ReplyStrRef, donor.RCategory, donor.ReplyLine, donor.NodeIDLink, donor.Ordinal, donor.TgtCondition, donor.TgtFireCnd, donor.TgtLine, donor.TgtSpeaker);
            newlink.Order = linkTable.Count + 1;
            linkTable.Add(newlink);
            NeedsSave = true;
            ReOrderTable();
        }

        private void MoveLink(object obj)
        {
            string command = obj as string;

            int n = 1;
            if(command == "Up") //"Up" is down in index
            {
                n = -1;
            }
            int movelinkID = datagrid_Links.SelectedIndex;
            int swapNodeID = movelinkID + n;

            if ((movelinkID == 0 && command == "Up") || (movelinkID >= linkTable.Count - 1 && command == "Down"))
                return;

            ReplyChoiceNode moveNode = linkTable[movelinkID];
            ReplyChoiceNode swapNode = linkTable[swapNodeID];
            int swapto = moveNode.Order;
            moveNode.Order = swapNode.Order;
            swapNode.Order = swapto;
            NeedsSave = true;
            ReOrderTable();
        }

        private void LinkEd_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(NeedsSave)
            {
                var confirm = MessageBox.Show("There are unsaved changes. Do you wish to save now?", "Link Editor", MessageBoxButton.YesNoCancel);

                if (confirm == MessageBoxResult.Yes)
                {
                    SaveToProperties();
                }
                else if (confirm == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
        }
    }
}
