using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.SharedUI.PeregrineTreeView;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{

    [DebuggerDisplay("BIN {Header}")]
    public class BinInterpNode() : NotifyPropertyChangedBase, ITreeItem
    {
        public enum ArrayPropertyChildAddAlgorithm : byte
        {
            None,
            FourBytes
        }

        public string Header { get; set; }
        public ITreeItem Parent { get; set; }

        private List<ITreeItem> _items = [];
        /// <summary>
        /// Children nodes of this item. They can be of different types (like UPropertyTreeViewEntry).
        /// </summary>
        public List<ITreeItem> Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        /// <summary>
        /// Used to cache the UIndex of object refs
        /// </summary>
        public int UIndexValue { get; set; }
        public int Offset { get; set; } = -1;

        public int Length { get; set; }
        public BinaryInterpreterWPF.NodeType Tag { get; set; }
        public ArrayPropertyChildAddAlgorithm ArrayAddAlgorithm;

        protected bool _isExpanded;
        public virtual bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public BinInterpNode(string header) : this()
        {
            Header = header;
        }

        /// <summary>
        /// Recursively removes null nodes, which may happen due to conditional adds
        /// </summary>
        public void RemoveNullNodes()
        {
            Items.RemoveAll(x => x == null);
            foreach (var item in Items.OfType<BinInterpNode>())
            {
                item.RemoveNullNodes();
            }
        }

        public BinInterpNode(long pos, string text, BinaryInterpreterWPF.NodeType nodeType = BinaryInterpreterWPF.NodeType.Unknown) : this()
        {
            Header = pos >= 0 ? $"0x{pos:X8}: {text}" : text;
            if (pos >= 0)
            {
                Offset = (int)pos;
            }
            Tag = nodeType;
        }

        public int GetPos()
        {
            if (Offset >= 0) return Offset;
            return 0;
        }

        public int GetObjectRefValue(ExportEntry export)
        {
            if (UIndexValue != 0) return UIndexValue; //cached
            if (Tag is BinaryInterpreterWPF.NodeType.ArrayLeafObject or BinaryInterpreterWPF.NodeType.ObjectProperty or BinaryInterpreterWPF.NodeType.StructLeafObject)
            {
                UIndexValue = EndianReader.ToInt32(export.DataReadOnly, GetPos(), export.FileRef.Endian);
            }
            return UIndexValue;
        }

        public void PrintPretty(string indent, TextWriter str, bool last, ExportEntry associatedExport)
        {
            bool supressNewLine = false;
            if (Header != null)
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
                if (Offset != null)
                {
                    str.Write(Offset + ": " + Header);// + " "  " (" + PropertyType + ")");
                }
                else
                {
                    str.Write(Header);// + " "  " (" + PropertyType + ")");
                }
            }
            else
            {
                supressNewLine = true;
            }

            for (int i = 0; i < Items.Count; i++)
            {
                if (!supressNewLine)
                {
                    str.Write("\n");
                }
                else
                {
                    supressNewLine = false;
                }
                (Items[i] as BinInterpNode)?.PrintPretty(indent, str, i == Items.Count - 1, associatedExport);
            }
        }

        public bool IsProgramaticallySelecting;
        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (!IsProgramaticallySelecting && isSelected != value)
                {
                    //user is selecting
                    isSelected = value;
                    OnPropertyChanged();
                    return;
                }
                // build a priority queue of dispatcher operations

                // All operations relating to tree item expansion are added with priority = DispatcherPriority.ContextIdle, so that they are
                // sorted before any operations relating to selection (which have priority = DispatcherPriority.ApplicationIdle).
                // This ensures that the visual container for all items are created before any selection operation is carried out.
                // First expand all ancestors of the selected item - those closest to the root first
                // Expanding a node will scroll as many of its children as possible into view - see perTreeViewItemHelper, but these scrolling
                // operations will be added to the queue after all of the parent expansions.
                if (value)
                {
                    var ancestorsToExpand = new Stack<ITreeItem>();

                    var parent = Parent;
                    while (parent != null)
                    {
                        if (!parent.IsExpanded)
                            ancestorsToExpand.Push(Parent);

                        parent = parent.Parent;
                    }

                    while (ancestorsToExpand.Any())
                    {
                        var parentToExpand = ancestorsToExpand.Pop();
                        DispatcherHelper.AddToQueue(() => parentToExpand.IsExpanded = true, DispatcherPriority.ContextIdle);
                    }
                }

                //cancel if we're currently selected.
                if (isSelected == value)
                    return;

                // Set the item's selected state - use DispatcherPriority.ApplicationIdle so this operation is executed after all
                // expansion operations, no matter when they were added to the queue.
                // Selecting a node will also scroll it into view - see perTreeViewItemHelper
                DispatcherHelper.AddToQueue(() =>
                {
                    if (value != isSelected)
                    {
                        this.isSelected = value;
                        OnPropertyChanged(nameof(IsSelected));
                        IsProgramaticallySelecting = false;
                    }
                }, DispatcherPriority.ApplicationIdle);

                // note that by rule, a TreeView can only have one selected item, but this is handled automatically by 
                // the control - we aren't required to manually unselect the previously selected item.

                // execute all of the queued operations in descending DipatecherPriority order (expansion before selection)
                var unused = DispatcherHelper.ProcessQueueAsync();
            }
        }
    }

    public class BinInterpNodeLazy : BinInterpNode
    {
        private Func<int, List<ITreeItem>> _getChildrenCallback;

        public BinInterpNodeLazy(long pos, string text, Func<int, List<ITreeItem>> getChildrenCallback) : base(pos, text)
        {
            _getChildrenCallback = getChildrenCallback;
            Items.Add(new BinInterpNode());
        }

        public override bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value) && value)
                {
                    if (_getChildrenCallback is not null)
                    {
                        Items = _getChildrenCallback(Offset);
                        _getChildrenCallback = null;
                    }
                }
            }
        }
    }
}