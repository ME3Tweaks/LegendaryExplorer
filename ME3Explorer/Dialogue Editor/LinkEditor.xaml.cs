using ME3Explorer.SharedUI;
using System;
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

        public ICommand FinishedCommand { get; set; }
        public ICommand AddCommand { get; set; }
        public ICommand DeleteCommand { get; set; }
        public ICommand UpCommand { get; set; }
        public ICommand DownCommand { get; set; }

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

            if (IsReply)
                this.Width = 700;

            Dnode = node;
            IsReply = Dnode.Node.IsReply;

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
            ParseLinks();
            GenerateTable();
        }

        private void LoadCommands()
        {
            FinishedCommand = new GenericCommand(Close_LinkEditor);
            AddCommand = new GenericCommand(CloneLink, HasActiveLink);
            DeleteCommand = new GenericCommand(DeleteLink, HasActiveLink);
            UpCommand = new RelayCommand(MoveLink);
            DownCommand = new RelayCommand(MoveLink);
        }

        private void ParseLinks()
        {
            int n = 0;
            foreach(var link in Dnode.Links)
            {
                link.Order = n;
                string a = "E";
                int tgtUID = link.Index;
                if (!IsReply)
                {
                    a = "R";
                    tgtUID += 1000;
                }
                link.NodeIDLink = $"{a}{link.Index}";

                var tgtObj = ParentWindow.CurrentObjects.FirstOrDefault(t => t.NodeUID == tgtUID);
                var tgtNode = tgtObj as DiagNode;

                link.TgtFireCnd = "Conditional";
                if(!tgtNode.Node.FiresConditional)
                    link.TgtFireCnd = "Bool";

                link.TgtCondition = tgtNode.Node.ConditionalOrBool;
                link.TgtLine = tgtNode.Node.Line;
                link.Ordinal = DialogueEditorWPF.AddOrdinal(link.Order + 1);
                linkTable.Add(link);
                n++;
            }
        }

        private void GenerateTable()
        {
            datagrid_Links.ItemsSource = linkTable;

            var clnO = new DataGridTextColumn();
            clnO.Header = "#";
            clnO.Binding = new Binding("Ordinal");
            clnO.Width = 30;
            datagrid_Links.Columns.Add(clnO);

            var clnA = new DataGridTextColumn();
            clnA.Header = "Link";
            clnA.Binding = new Binding("NodeIDLink");
            clnA.Width = 40;
            datagrid_Links.Columns.Add(clnA);

            if(!IsReply)
            {
                var clnB = new DataGridTextColumn();
                clnB.Header = "GUI Choice StrRef";
                clnB.Binding = new Binding("ReplyStrRef");
                clnB.Width = 70;
                datagrid_Links.Columns.Add(clnB);

                var clnC = new DataGridTextColumn();
                clnC.Header = "GUI Choice Line";
                clnC.Binding = new Binding("ReplyLine");
                clnC.IsReadOnly = true;
                clnC.Width = 120;
                datagrid_Links.Columns.Add(clnC);


                var clnD = new DataGridComboBoxColumn();
                clnD.Header = "GUI Category";
                clnD.ItemsSource = Enum.GetValues(typeof(EReplyCategory)).Cast<EReplyCategory>();
                clnD.SelectedItemBinding = new Binding("RCategory");
                clnD.Width = 150;
                datagrid_Links.Columns.Add(clnD);
            }

            var clnE = new DataGridTextColumn();
            clnE.Header = "Target Check";
            clnE.Binding = new Binding("TgtFireCnd");
            clnE.IsReadOnly = true;
            clnE.Width = 75;
            datagrid_Links.Columns.Add(clnE);

            var clnF = new DataGridTextColumn();
            clnF.Header = "Plot Check";
            clnF.Binding = new Binding("TgtCondition");
            clnF.IsReadOnly = true;
            clnF.Width = 65;
            datagrid_Links.Columns.Add(clnF);

            var clnG = new DataGridTextColumn();
            clnG.Header = "Target Line";
            clnG.Binding = new Binding("TgtLine");
            clnG.IsReadOnly = true;
            clnG.Width = 300;

            datagrid_Links.Columns.Add(clnG);
            
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
            Close();
        }

        private bool HasActiveLink()
        {
            return datagrid_Links != null && datagrid_Links.SelectedIndex >= 0;
        }

        private void DeleteLink()
        {
            ReplyChoiceNode result = datagrid_Links.SelectedItem as ReplyChoiceNode;
            linkTable.Remove(result);
            ReOrderTable();
        }

        private void CloneLink()
        {
            ReplyChoiceNode newlink = datagrid_Links.SelectedItem as ReplyChoiceNode;
            newlink.Order = linkTable.Count + 1;
            linkTable.Add(newlink);
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

            ReOrderTable();
        }
    }
}
