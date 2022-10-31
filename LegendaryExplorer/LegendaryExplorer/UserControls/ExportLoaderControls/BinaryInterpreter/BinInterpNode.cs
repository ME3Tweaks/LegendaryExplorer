using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI.PeregrineTreeView;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    public interface ITreeItem
    {
        bool IsSelected { get; set; }
        bool IsExpanded { get; set; }
        void PrintPretty(string indent, TextWriter str, bool last, ExportEntry associatedExport);
    }

    public class BinInterpNode : NotifyPropertyChangedBase, ITreeItem
    {
        public enum ArrayPropertyChildAddAlgorithm
        {
            None,
            FourBytes
        }

        /// <summary>
        /// Used to cache the UIndex of object refs
        /// </summary>
        public int UIndexValue { get; set; }

        public string Header { get; set; }
        public string Name { get; set; }
        public object Tag { get; set; }
        public BinInterpNode Parent;
        public ArrayPropertyChildAddAlgorithm ArrayAddAlgoritm;

        public bool IsExpanded { get; set; }

        /// <summary>
        /// Children nodes of this item. They can be of different types (like UPropertyTreeViewEntry).
        /// </summary>
        public List<ITreeItem> Items { get; set; }
        public BinInterpNode()
        {
            Items = new List<ITreeItem>();
        }

        public BinInterpNode(string header) : this()
        {
            Header = header;
        }

        /// <summary>
        /// Gets the data offset of this node.
        /// </summary>
        /// <returns></returns>
        public int GetOffset()
        {
            if (Name != null && Name.StartsWith("_"))
            {
                if (int.TryParse(Name.Substring(1), out var dataOffset)) // remove _
                {
                    return dataOffset;
                }
            }

            return 0;
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
                Name = $"_{pos}";
            }
            Tag = nodeType;
        }

        public long GetPos()
        {
            if (!string.IsNullOrEmpty(Name) && long.TryParse(Name.Substring(1), out var pos)) return pos;
            return 0;
        }

        public int GetObjectRefValue(ExportEntry export)
        {
            if (UIndexValue != 0) return UIndexValue; //cached
            if (Tag is BinaryInterpreterWPF.NodeType type && (type == BinaryInterpreterWPF.NodeType.ArrayLeafObject || type == BinaryInterpreterWPF.NodeType.ObjectProperty || type == BinaryInterpreterWPF.NodeType.StructLeafObject))
            {
                UIndexValue = EndianReader.ToInt32(export.DataReadOnly, (int)GetPos(), export.FileRef.Endian);
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
                if (Name != null)
                {
                    str.Write(Name.TrimStart('_') + ": " + Header);// + " "  " (" + PropertyType + ")");
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
                    var ancestorsToExpand = new Stack<BinInterpNode>();

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

        public int Length { get; set; }
    }
}