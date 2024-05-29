using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.PlotDatabase;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.SharedUI
{
    [DebuggerDisplay("TreeViewEntry {" + nameof(DisplayName) + "}")]
    public sealed class TreeViewEntry : NotifyPropertyChangedBase, IDisposable
    {
        private static readonly PackageCache DefaultsLookupCache = new() { CacheMaxSize = 3 }; // Don't let cache get big.

        public bool IsProgramaticallySelecting;

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set => SetProperty(ref isSelected, value);
        }

        /// <summary>
        /// Returns the game that this entry node is tied to
        /// </summary>
        public MEGame Game => PackageRef?.Game ?? Entry.Game;

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
        /// Only used on the root node - used to tell what package this entry represent the root for
        /// </summary>
        public IMEPackage PackageRef { get; set; }

        /// <summary>
        /// List of entries that link to this node
        /// </summary>
        public ObservableCollectionExtended<TreeViewEntry> Sublinks { get; set; }
        public TreeViewEntry(IEntry entry, string displayName = null)
        {
            Entry = entry;
            DisplayName = displayName;
            Sublinks = new ObservableCollectionExtended<TreeViewEntry>();

            // Events don't work in interface without method to raise changes
            // so we just attach to each
            if (Entry is ImportEntry imp)
            {
                imp.PropertyChanged += TVEntryPropertyChanged;
            }
            else if (Entry is ExportEntry exp)
            {
                exp.PropertyChanged += TVEntryPropertyChanged;
            }
        }

        private void TVEntryPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Settings.PackageEditor_ShowTreeEntrySubText)
            {
                RefreshSubText();
            }
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

        private readonly string _displayName;
        public string DisplayName
        {
            get
            {
                try
                {
                    if (_displayName != null) return _displayName;
                    string returnvalue = $"{UIndex} {Entry.ObjectName.Instanced}";
                    if (Settings.PackageEditor_ShowImpExpPrefix)
                    {
                        string type = UIndex < 0 ? "(Imp) " : "(Exp) ";
                        returnvalue = type + returnvalue;
                    }

                    string className = Entry.ClassName;
                    if (Entry is ExportEntry exp && BinaryInterpreterWPF.IsNativePropertyType(Entry.ClassName))
                    {
                        // We can't do this in the subtext setter since:
                        // 1. Users might have that off
                        // 2. The display property is Init-Only
                        var bin = ObjectBinary.From<UBoolProperty>(exp);
                        if (bin.ArraySize > 1)
                            className += $"[{bin.ArraySize}]";
                    }
                    returnvalue += $"({className})";
                    return returnvalue;
                }
                catch (Exception)
                {
                    return "ERROR GETTING DISPLAY NAME!";
                }
            }
            init { _displayName = value; OnPropertyChanged(); }
        }

        private bool loadedSubtext = false;
        private string _subtext;
        public string SubText
        {
            get
            {
                if (!Settings.PackageEditor_ShowTreeEntrySubText) return null;
                try
                {
                    if (loadedSubtext) return _subtext;
                    if (Entry == null) return null;
                    if (Entry is ExportEntry ee)
                    {
                        if (ee.IsDefaultObject) return null; // We don't have subtext on defaults.
                        //Parse as export
                        switch (ee.ClassName)
                        {
                            case "Function":
                                {
                                    //check if exec
                                    var data = ee.DataReadOnly;
                                    if (Entry.FileRef.Game == MEGame.ME3 || Entry.FileRef.Platform == MEPackage.GamePlatform.PS3 || Entry.Game.IsLEGame())
                                    {
                                        var flagOffset = Entry.Game.IsGame3() || Entry.FileRef.Platform == MEPackage.GamePlatform.PS3 ? 4 : 12;
                                        var flags = EndianReader.ToInt32(data, data.Length - flagOffset, ee.FileRef.Endian);
                                        var fs = new FlagValues(flags, UE3FunctionReader._flagSet);
                                        _subtext = "";
                                        if (fs.HasFlag("Static"))
                                        {
                                            if (_subtext != "") _subtext += " ";
                                            _subtext = "Static";
                                        }
                                        if (fs.HasFlag("Native"))
                                        {
                                            if (_subtext != "") _subtext += " ";
                                            _subtext += "Native";
                                            var nativeBackOffset = !Entry.FileRef.Game.IsGame3() ? 3 : 2; // can be ps3 me1/me2
                                            var nativeIndex = EndianReader.ToInt16(data, data.Length - nativeBackOffset - flagOffset, ee.FileRef.Endian);
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
                                    else if (Entry.Game.IsOTGame() || Entry.Game is MEGame.UDK) // ME1 / ME2
                                    {
                                        //This could be -14 if it's defined as Net... we would have to decompile the whole function to know though...
                                        var flags = EndianReader.ToInt32(data, data.Length - 12, ee.FileRef.Endian);
                                        var fs = new FlagValues(flags, UE3FunctionReader._flagSet);
                                        if (fs.HasFlag("Exec"))
                                        {
                                            _subtext = "Exec - console command";
                                        }
                                        else if (fs.HasFlag("Native"))
                                        {
                                            var nativeBackOffset = ee.FileRef.Game == MEGame.ME3 ? 6 : 7;
                                            if (ee.Game is MEGame.UDK || ee.Game < MEGame.ME3 && ee.FileRef.Platform != MEPackage.GamePlatform.PS3)
                                            {
                                                nativeBackOffset = 0xF;
                                            }
                                            var nativeIndex = EndianReader.ToInt16(data, data.Length - nativeBackOffset,
                                                ee.FileRef.Endian);
                                            if (nativeIndex > 0)
                                            {
                                                _subtext = "Native, index " + nativeIndex;
                                            }
                                            else
                                            {
                                                _subtext = "Native";
                                            }
                                        }
                                    }

                                    if (Entry.ObjectName.Name.StartsWith("F") &&
                                        Entry.ParentName.Equals("BioAutoConditionals", StringComparison.OrdinalIgnoreCase) &&
                                        int.TryParse(Entry.ObjectName.Name.Substring(1), out int id))
                                    {
                                        _subtext = PlotDatabases.FindPlotConditionalByID(id, Entry.Game)?.Path;
                                    }

                                    break;
                                }
                            case "Const":
                                {
                                    var data = ee.DataReadOnly;
                                    //This is kind of a hack. 
                                    var value = EndianReader.ReadUnrealString(data, Entry.Game is MEGame.UDK ? 0x10 : 0x14, ee.FileRef.Endian);
                                    _subtext = "Value: " + value;
                                    break;
                                }
                            case "ByteProperty":
                            case "StructProperty":
                            case "ObjectProperty":
                            case "ComponentProperty":
                                {
                                    // Objects of this type
                                    var typeRef = EndianReader.ToInt32(ee.DataReadOnly, Entry.FileRef.Platform == MEPackage.GamePlatform.PC ? Entry.Game is MEGame.UDK ? 0x28 : 0x2C : 0x20, ee.FileRef.Endian);
                                    if (ee.FileRef.TryGetEntry(typeRef, out var type))
                                    {
                                        _subtext = type.ObjectName;
                                    }
                                    break;
                                }
                            case "ClassProperty":
                                {
                                    var data = ee.DataReadOnly;
                                    var typeRef = EndianReader.ToInt32(data, data.Length - 4, ee.FileRef.Endian);
                                    if (ee.FileRef.TryGetEntry(typeRef, out var type))
                                    {
                                        _subtext = $"Class: {type.ObjectName}";
                                    }
                                    break;
                                }
                            case "Texture2D":
                                {
                                    var properties = ee.GetProperties();
                                    var sizeX = properties.GetProp<IntProperty>("SizeX");
                                    var sizeY = properties.GetProp<IntProperty>("SizeY");
                                    _subtext = $"{sizeX?.Value}x{sizeY?.Value}";
                                    break;
                                }
                            case "ParticleSpriteEmitter":
                                {
                                    var emitterName = ee.GetProperty<NameProperty>("EmitterName");
                                    if (emitterName != null)
                                    {
                                        _subtext = emitterName.Value.Instanced;
                                    }
                                }
                                break;
                            case "SFXGalaxy":
                            case "SFXSystem":
                            case "SFXCluster":
                                {
                                    var objName = ee.GetProperty<StringRefProperty>("DisplayName");
                                    if (objName != null)
                                    {
                                        var dispName = TLKManagerWPF.GlobalFindStrRefbyID(objName.Value, ee.Game);
                                        if (dispName != @"No Data")
                                        {
                                            _subtext = dispName;
                                        }
                                    }
                                }
                                break;
                        }

                        // Short circuit
                        

                        if (!AddPropertyFlags(ee))
                        {
                            var tag = ee.GetProperty<NameProperty>("Tag", DefaultsLookupCache); // Todo: Pass a package cache through here so hits to Engine.pcc aren't as costly. We will need a global shared package cache (maybe just for this treeview), but one that is not
                                                                                                // using the LEX cache as we don't want the package actually open.
                            if (tag != null && tag.Value.Name != Entry.ObjectName)
                            {
                                _subtext = tag.Value.Instanced;
                            }
                        }
                    }

                    if (_subtext == null)
                    {
                        // Parse if export or import
                        switch (Entry.ClassName)
                        {
                            case "WwiseEvent":
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

                                    break;
                                }
                            case "WwiseStream":
                                {
                                    //parse out tlk id?
                                    var splits = Entry.ObjectName.Name.Split('_', ',');
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

                                    break;
                                }
                            case "SoundNodeWave":
                            case "SoundCue":
                                {
                                    //parse out tlk id?
                                    var splits = Entry.ObjectName.Name.Split('_', ',');
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

                                    break;
                                }
                        }
                    }

                    loadedSubtext = true;
                    return _subtext;
                }
                catch (Exception)
                {
                    loadedSubtext = true;
                    _subtext = "ERROR GETTING SUBTEXT!";
                    return "ERROR GETTING SUBTEXT!";
                }
            }
            set { _subtext = value; OnPropertyChanged(); }
        }

        private bool AddPropertyFlags(ExportEntry ee)
        {
            if (BinaryInterpreterWPF.IsNativePropertyType(Entry.ClassName))
            {
                var objectFlags = ee.GetPropertyFlags();
                if (objectFlags != null)
                {
                    if (objectFlags.Value.HasFlag(UnrealFlags.EPropertyFlags.Config))
                    {
                        if (_subtext != null)
                        {
                            _subtext = " Config, " + _subtext;
                        }
                        else
                        {
                            _subtext = "Config";
                        }
                    }
                }
                else
                {
                    // Bool is the most common subset so we parse the export as this to access the actual data.
                    // Lots of common properties won't have a stack
                    var bin = ObjectBinary.From<UBoolProperty>(ee);
                    if (bin.PropertyFlags.HasFlag(UnrealFlags.EPropertyFlags.Config))
                    {
                        if (_subtext != null)
                        {
                            _subtext = " Config, " + _subtext;
                        }
                        else
                        {
                            _subtext = "Config";
                        }
                    }
                }

                return true;
            }

            return false;
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

        public void Dispose()
        {
            if (Entry is not null)
            {
                Entry.PropertyChanged -= TVEntryPropertyChanged;
                Entry = null;
            }
        }
    }
}