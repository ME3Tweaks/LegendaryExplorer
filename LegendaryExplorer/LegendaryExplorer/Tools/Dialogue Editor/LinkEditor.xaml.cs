using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.SharedUI;
using LegendaryExplorerCore.Dialogue;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using static LegendaryExplorer.Tools.TlkManagerNS.TLKManagerWPF;

namespace LegendaryExplorer.DialogueEditor
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class LinkEditor : Window
    {
        private readonly DialogueEditorWindow ParentWindow;
        private readonly DiagNode Dnode;
        private readonly bool IsReply;
        public ObservableCollectionExtended<ReplyChoiceNode> linkTable { get; } = new();
        private bool NeedsSave;
        public bool NeedsPush;
        public ICommand FinishedCommand { get; set; }
        public ICommand AddCommand { get; set; }
        public ICommand DeleteCommand { get; set; }
        public ICommand UpCommand { get; set; }
        public ICommand DownCommand { get; set; }
        public ICommand EditCommand { get; set; }

        public LinkEditor(DialogueEditorWindow owner, DiagNode node)
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

            var tgtObj = ParentWindow.CurrentObjects.First(t => t.NodeUID == tgtUID);
            var tgtNode = (DiagNode)tgtObj;

            link.TgtFireCnd = "Conditional";
            if (!tgtNode.Node.FiresConditional)
                link.TgtFireCnd = "Bool";

            link.TgtCondition = tgtNode.Node.ConditionalOrBool;
            link.TgtLine = tgtNode.Node.Line;
            link.Ordinal = DialogueEditorWindow.AddOrdinal(link.Order + 1);
            link.TgtSpeaker = tgtNode.Node.SpeakerTag.SpeakerName;
        }

        private void GenerateTable()
        {

            datagrid_Links.ItemsSource = linkTable;

            var clnO = new DataGridTextColumn
            {
                Header = "#",
                Binding = new Binding(nameof(ReplyChoiceNode.Ordinal)),
                Width = 30,
                IsReadOnly = true,
                Foreground = Brushes.DarkSlateGray
            };
            datagrid_Links.Columns.Add(clnO);

            var clnA = new DataGridTextColumn
            {
                Header = "Link",
                Binding = new Binding(nameof(ReplyChoiceNode.NodeIDLink)),
                IsReadOnly = true,
                Width = 40,
                FontWeight = FontWeights.Heavy
            };
            datagrid_Links.Columns.Add(clnA);

            if (!IsReply)
            {
                var clnB = new DataGridTextColumn
                {
                    Header = "GUI StrRef",
                    Binding = new Binding(nameof(ReplyChoiceNode.ReplyStrRef)),
                    IsReadOnly = false,
                    Width = 70,
                    FontWeight = FontWeights.Bold
                };
                datagrid_Links.Columns.Add(clnB);

                var clnC = new DataGridTextColumn
                {
                    Header = "GUI Choice Line",
                    Binding = new Binding(nameof(ReplyChoiceNode.ReplyLine)),
                    IsReadOnly = true,
                    Width = 120,
                    Foreground = Brushes.DarkSlateGray
                };
                datagrid_Links.Columns.Add(clnC);


                var clnD = new DataGridComboBoxColumn
                {
                    Header = "GUI Category",
                    ItemsSource = GetReplyCategoryValues(),
                    SelectedItemBinding = new Binding(nameof(ReplyChoiceNode.RCategory)),
                    IsReadOnly = false,
                    Width = 150
                };
                clnB.FontWeight = FontWeights.Bold;
                datagrid_Links.Columns.Add(clnD);
            }

            var clnE = new DataGridTextColumn
            {
                Header = "Target Check",
                Binding = new Binding(nameof(ReplyChoiceNode.TgtFireCnd)),
                IsReadOnly = true,
                Width = 80,
                Foreground = Brushes.DarkSlateGray
            };
            datagrid_Links.Columns.Add(clnE);

            var clnF = new DataGridTextColumn
            {
                Header = "Plot Check",
                Binding = new Binding(nameof(ReplyChoiceNode.TgtCondition)),
                IsReadOnly = true,
                Width = 65,
                Foreground = Brushes.DarkSlateGray
            };
            datagrid_Links.Columns.Add(clnF);

            var clnH = new DataGridTextColumn
            {
                Header = "Speaker",
                Binding = new Binding(nameof(ReplyChoiceNode.TgtSpeaker)),
                IsReadOnly = true,
                Width = 100,
                Foreground = Brushes.DarkSlateGray
            };
            datagrid_Links.Columns.Add(clnH);

            var clnG = new DataGridTextColumn
            {
                Header = "Target Line",
                Binding = new Binding(nameof(ReplyChoiceNode.TgtLine)),
                IsReadOnly = true,
                Width = 355,
                Foreground = Brushes.DarkSlateGray
            };
            datagrid_Links.Columns.Add(clnG);

            if (!IsReply)
            {
                clnH.Width = 60;
                clnG.Width = 200;
            }


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
            string ldlg = InputComboBoxDialog.GetValue(this, "Pick the next dialogue node to link to", "Dialogue Editor", links, links[l]);

            if (string.IsNullOrEmpty(ldlg))
                return;
            editLink.Index = links.FindIndex(ldlg.Equals);

            if (!IsReply)
            {
                //Set StrRef
                int strRef = 0;
                bool isNumber = false;
                while (!isNumber)
                {
                    var response = PromptDialog.Prompt(this, "Enter the TLK String Reference for the dialogue wheel:", "Link Editor", editLink.ReplyStrRef.ToString());
                    if (response == null || response == "0")
                        return;
                    isNumber = int.TryParse(response, out strRef);
                    if (!isNumber || strRef <= 0)
                    {
                        var wdlg = MessageBox.Show("The string reference must be a positive whole number.", "Dialogue Editor", MessageBoxButton.OKCancel);
                        if (wdlg == MessageBoxResult.Cancel)
                            return;
                    }
                }
                editLink.ReplyStrRef = strRef;

                ////Set GUI Reply style
                EReplyCategory rc = editLink.RCategory; //Get current link

                string rdlg = InputComboBoxDialog.GetValue(this, "Pick the wheel position or interrupt:", "Dialogue Editor", GetReplyCategoryValues().Select(v => v.ToString()), rc.ToString());

                if (string.IsNullOrEmpty(rdlg))
                    return;
                rc = Enums.Parse<EReplyCategory>(rdlg);
                editLink.RCategory = rc;
            }
            ParseLink(editLink);
            ReOrderTable();
            NeedsSave = true;
        }

        private void ReOrderTable()
        {
            linkTable.Sort(l => l.Order);

            int n = 0;
            foreach (ReplyChoiceNode link in linkTable)
            {
                link.Order = n;
                link.Ordinal = DialogueEditorWindow.AddOrdinal(link.Order + 1);
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
                Dnode.NodeProp.Properties.AddOrReplaceProp(new ArrayProperty<IntProperty>(linkTable.Select(link => new IntProperty(link.Index)), "EntryList"));
            }
            else
            {
                Dnode.NodeProp.Properties.AddOrReplaceProp(new ArrayProperty<StructProperty>(linkTable.Select(link =>
                    new StructProperty("BioDialogReplyListDetails", new PropertyCollection
                    {
                        new IntProperty(link.Index, "nIndex"),
                        new StringRefProperty(link.ReplyStrRef, "srParaphrase"),
                        new StrProperty("", "sParaphrase"),
                        new EnumProperty(link.RCategory.ToString(), "EReplyCategory", ParentWindow.Pcc.Game, "Category"),
                        new NoneProperty()
                    })
                ), "ReplyListNew"));
            }
            NeedsPush = true;
            NeedsSave = false;
        }

        private bool HasActiveLink()
        {
            return datagrid_Links is { SelectedIndex: >= 0 };
        }

        private void DeleteLink()
        {
            var result = datagrid_Links.SelectedItem as ReplyChoiceNode;
            linkTable.Remove(result);
            NeedsSave = true;
            ReOrderTable();
        }

        private void CloneLink()
        {
            ReplyChoiceNode donor = linkTable[datagrid_Links.SelectedIndex];
            linkTable.Add(new ReplyChoiceNode(donor) { Order = linkTable.Count + 1 });
            NeedsSave = true;
            ReOrderTable();
        }

        private void MoveLink(object obj)
        {
            string command = obj as string;

            int n = 1;
            if (command == "Up") //"Up" is down in index
            {
                n = -1;
            }
            int movelinkID = datagrid_Links.SelectedIndex;
            int swapNodeID = movelinkID + n;

            if ((movelinkID == 0 && command == "Up") || (movelinkID >= linkTable.Count - 1 && command == "Down"))
                return;

            ReplyChoiceNode moveNode = linkTable[movelinkID];
            ReplyChoiceNode swapNode = linkTable[swapNodeID];
            (moveNode.Order, swapNode.Order) = (swapNode.Order, moveNode.Order);
            NeedsSave = true;
            ReOrderTable();
        }

        private void LinkEd_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (NeedsSave)
            {
                switch (MessageBox.Show("There are unsaved changes. Do you wish to save now?", "Link Editor", MessageBoxButton.YesNoCancel))
                {
                    case MessageBoxResult.Yes:
                        SaveToProperties();
                        break;
                    case MessageBoxResult.Cancel:
                        e.Cancel = true;
                        break;
                }
            }
        }

        private EReplyCategory[] GetReplyCategoryValues()
        {
            if (ParentWindow.Pcc.Game.IsGame1())
            {
                return new[]
                {
                    EReplyCategory.REPLY_CATEGORY_DEFAULT,
                    EReplyCategory.REPLY_CATEGORY_AGREE,
                    EReplyCategory.REPLY_CATEGORY_DISAGREE,
                    EReplyCategory.REPLY_CATEGORY_FRIENDLY,
                    EReplyCategory.REPLY_CATEGORY_HOSTILE,
                    EReplyCategory.REPLY_CATEGORY_INVESTIGATE,
                };
            }
            return Enums.GetValues<EReplyCategory>();
        }
    }
}
