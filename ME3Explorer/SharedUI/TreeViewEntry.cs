using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DocumentFormat.OpenXml.Wordprocessing;
using Gammtek.Conduit.IO;
using ME2Explorer;
using ME3Explorer.ME1.Unreal.UnhoodBytecode;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.SharedUI.PeregrineTreeView;
using ME3Explorer.TlkManagerNS;
using ME3Explorer.Unreal;

namespace ME3Explorer
{
    [DebuggerDisplay("TreeViewEntry {" + nameof(DisplayName) + "}")]
    public class TreeViewEntry : NotifyPropertyChangedBase
    {
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

        public void RefreshSubText()
        {
            loadedSubtext = false;
            SubText = null;
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
                    string returnvalue = $"{UIndex} {Entry.ObjectName.Instanced}";
                    if (Properties.Settings.Default.PackageEditorWPF_ShowImpExpPrefix)
                    {
                        returnvalue = type + returnvalue;
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

        private bool loadedSubtext = false;
        private string _subtext;
        public string SubText
        {
            get
            {
                if (!Properties.Settings.Default.PackageEditorWPF_ShowSubText) return null;
                try
                {
                    if (loadedSubtext) return _subtext;
                    if (Entry == null) return null;
                    var ee = Entry as ExportEntry;
                    if (Entry.ClassName == "WwiseEvent")
                    {
                        //parse out tlk id?
                        if (Entry.ObjectName.Name.StartsWith("VO_"))
                        {
                            var parsing = Entry.ObjectName.Name.Substring(3);
                            var nextUnderScore = parsing.IndexOf("_");
                            if (nextUnderScore > 0)
                            {
                                parsing = parsing.Substring(0, nextUnderScore);
                                if (int.TryParse(parsing, out var parsedInt))
                                {
                                    //Lookup TLK
                                    var data = TLKManagerWPF.GlobalFindStrRefbyID(parsedInt, Entry.FileRef);
                                    if (data != "No Data")
                                    {
                                        _subtext = data;
                                    }
                                }
                            }
                        }
                    }
                    else if (Entry.ClassName == "WwiseStream")
                    {
                        //parse out tlk id?
                        var splits = Entry.ObjectName.Name.Split('_');
                        for (int i = splits.Length - 1; i > 0; i--)
                        {
                            //backwards is faster
                            if (int.TryParse(splits[i], out var parsed))
                            {
                                //Lookup TLK
                                var data = TLKManagerWPF.GlobalFindStrRefbyID(parsed, Entry.FileRef);
                                if (data != "No Data")
                                {
                                    _subtext = data;
                                }
                            }
                        }
                    }
                    else if (ee != null)
                    {
                        if (Entry.ClassName == "Function" && ee != null)
                        {
                            //check if exec
                            var data = ee.Data;
                            if (Entry.FileRef.Game == MEGame.ME3 || Entry.FileRef.Platform == MEPackage.GamePlatform.PS3)
                            {
                                var flags = EndianReader.ToInt32(data, data.Length - 4, ee.FileRef.Endian);
                                FlagValues fs = new FlagValues(flags, UE3FunctionReader._flagSet);
                                _subtext = "";
                                if (fs.HasFlag("Native"))
                                {
                                    _subtext = "Native";
                                    var nativeIndex = EndianReader.ToInt16(data, data.Length - 6, ee.FileRef.Endian);
                                    if (nativeIndex > 0)
                                    {
                                        _subtext += ", index " + nativeIndex;
                                    }
                                }
                                if (fs.HasFlag("Exec"))
                                {
                                    if (_subtext != "") _subtext += " ";
                                    _subtext += "Exec - console command";
                                }

                                if (_subtext == "") _subtext = null;
                            }
                            else
                            {
                                //This could be -14 if it's defined as Net... we would have to decompile the whole function to know though...
                                var flags = EndianReader.ToInt32(data, data.Length - 12, ee.FileRef.Endian);
                                FlagValues fs = new FlagValues(flags, UE3FunctionReader._flagSet);
                                if (fs.HasFlag("Exec"))
                                {
                                    _subtext = "Exec - console command";
                                }
                                else if (fs.HasFlag("Native"))
                                {
                                    var nativeIndex = EndianReader.ToInt16(data, data.Length - 6, ee.FileRef.Endian);
                                    _subtext = "Native, index "+nativeIndex;
                                }
                            }

                        }
                        else if (Entry.ClassName == "Const")
                        {
                            var data = ee.Data;
                            //This is kind of a hack. 
                            var value = EndianReader.ReadUnrealString(data, 0x14, ee.FileRef.Endian);
                            _subtext = "Value: " + value;
                        }
                        else if (BinaryInterpreterWPF.IsNativePropertyType(Entry.ClassName))
                        {
                            var data = ee.Data;
                            //This is kind of a hack. 
                            UnrealFlags.EPropertyFlags objectFlags = (UnrealFlags.EPropertyFlags)EndianReader.ToUInt64(data, 0x18, ee.FileRef.Endian);
                            if ((objectFlags & UnrealFlags.EPropertyFlags.Config) != 0)
                            {
                                _subtext = "Config";
                            }
                        }
                        else
                        {
                            var tag = ee.GetProperty<NameProperty>("Tag");
                            if (tag != null && tag.Value.Name != Entry.ObjectName)
                            {
                                _subtext = tag.Value.Name;
                            }
                        }
                    }

                    loadedSubtext = true;
                    return _subtext;
                }
                catch (Exception)
                {
                    return "ERROR GETTING SUBTEXT!";
                }
            }
            set { _subtext = value; OnPropertyChanged(); }
        }

        public int UIndex => Entry?.UIndex ?? 0;

        private System.Windows.Media.Brush _foregroundColor;
        public System.Windows.Media.Brush ForegroundColor
        {
            get => Entry is ImportEntry ? ImportEntryBrush : ExportEntryBrush;
            set
            {
                _foregroundColor = value;
                OnPropertyChanged();
            }
        }

        private static SolidColorBrush ImportEntryBrush => SystemColors.GrayTextBrush;
        private static SolidColorBrush ExportEntryBrush => SystemColors.ControlTextBrush;

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