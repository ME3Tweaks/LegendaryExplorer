using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.SharedUI.PeregrineTreeView;

namespace ME3Explorer
{
    [DebuggerDisplay("TreeViewEntry {" + nameof(DisplayName) + "}")]
    public class TreeViewEntry : NotifyPropertyChangedBase
    {
        private System.Windows.Media.Brush _foregroundColor = System.Windows.Media.Brushes.DarkSeaGreen;

        public bool IsProgramaticallySelecting;

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set => SetProperty(ref isSelected, value);
        }
        /*   {
              /* if (!IsProgramaticallySelecting && isSelected != value)
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
                   var ancestorsToExpand = new Stack<TreeViewEntry>();

                   var parent = Parent;
                   while (parent != null)
                   {
                       if (!parent.IsExpanded)
                           ancestorsToExpand.Push(parent);

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
       }*/

        private bool isExpanded;
        public bool IsExpanded
        {
            get => this.isExpanded;
            set => SetProperty(ref isExpanded, value);
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
        public List<TreeViewEntry> FlattenTree()
        {
            var nodes = new List<TreeViewEntry> { this };
            foreach (TreeViewEntry tve in Sublinks)
            {
                nodes.AddRange(tve.FlattenTree());
            }
            return nodes;
        }

        public TreeViewEntry Parent { get; set; }

        /// <summary>
        /// The entry object from the file that this node represents
        /// </summary>
        public IEntry Entry { get; set; }
        /// <summary>
        /// List of entries that link to this node
        /// </summary>
        public ObservableCollectionExtended<TreeViewEntry> Sublinks { get; set; }
        public TreeViewEntry(IEntry entry, string displayName = null)
        {
            Entry = entry;
            DisplayName = displayName;
            Sublinks = new ObservableCollectionExtended<TreeViewEntry>();
        }

        public void RefreshDisplayName()
        {
            OnPropertyChanged(nameof(DisplayName));
        }

        private string _displayName;
        public string DisplayName
        {
            get
            {
                try
                {
                    if (_displayName != null) return _displayName;
                    string type = UIndex < 0 ? "(Imp) " : "(Exp) ";
                    string returnvalue = $"{UIndex} {Entry.ObjectName}";
                    if (Properties.Settings.Default.PackageEditorWPF_ShowImpExpPrefix)
                    {
                        returnvalue = type + returnvalue;
                    }
                    if (Properties.Settings.Default.PackageEditorWPF_TreeViewShowEntryIndex && Entry.indexValue != 0)
                    {
                        returnvalue += $"_{Entry.indexValue}";
                    }
                    returnvalue += $"({Entry.ClassName})";
                    return returnvalue;
                }
                catch (Exception)
                {
                    return "ERROR GETTING DISPLAY NAME!";
                }
            }
            set { _displayName = value; OnPropertyChanged(); }
        }

        public int UIndex => Entry?.UIndex ?? 0;

        public System.Windows.Media.Brush ForegroundColor
        {
            get => Entry == null ? System.Windows.Media.Brushes.Black : UIndex > 0 ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.Gray;
            set
            {
                _foregroundColor = value;
                OnPropertyChanged();
            }
        }

        public override string ToString()
        {
            return "TreeViewEntry " + DisplayName;
        }

        /// <summary>
        /// Sorts this node's children in ascending positives first, then descending negatives
        /// </summary>
        internal void SortChildren()
        {
            var exportNodes = Sublinks.Where(x => x.Entry.UIndex > 0).OrderBy(x => x.UIndex).ToList();
            var importNodes = Sublinks.Where(x => x.Entry.UIndex < 0).OrderByDescending(x => x.UIndex).ToList();

            exportNodes.AddRange(importNodes);
            Sublinks.ClearEx();
            Sublinks.AddRange(exportNodes);
        }
    }
}