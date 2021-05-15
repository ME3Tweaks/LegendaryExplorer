//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Windows;
//using System.Windows.Media;
//using ME3Explorer.TlkManagerNS;
//using ME3ExplorerCore.Gammtek.IO;
//using ME3ExplorerCore.ME1.Unreal.UnhoodBytecode;
//using ME3ExplorerCore.Misc;
//using ME3ExplorerCore.Packages;
//using ME3ExplorerCore.Unreal;

//namespace ME3Explorer.TextureStudio
//{
//    [DebuggerDisplay("TextureTreeViewEntry {}")]
//    public class TextureTreeViewEntry : TextureMapMemoryEntry
//    {
//        public bool IsProgramaticallySelecting;

//        private bool isSelected;
//        public bool IsSelected
//        {
//            get => isSelected;
//            set => SetProperty(ref isSelected, value);
//        }

//        private bool isExpanded;
//        public bool IsExpanded
//        {
//            get => this.isExpanded;
//            set => SetProperty(ref isExpanded, value);
//        }

//        public void ExpandParents()
//        {
//            if (EntryParent != null)
//            {
//                Parent.ExpandParents();
//                Parent.IsExpanded = true;
//            }
//        }

//        private TextureMapMemoryEntry Entry { get; set; }

//        /// <summary>
//        /// Flattens the tree into depth first order. Use this method for searching the list.
//        /// </summary>
//        /// <returns></returns>
//        public List<TextureTreeViewEntry> FlattenTree()
//        {
//            var nodes = new List<TextureTreeViewEntry> { this };
//            foreach (TextureTreeViewEntry tve in Sublinks)
//            {
//                nodes.AddRange(tve.FlattenTree());
//            }
//            return nodes;
//        }

//        /// <summary>
//        /// List of entries that link to this node
//        /// </summary>
//        public ObservableCollectionExtended<TextureTreeViewEntry> Sublinks { get; set; }
//        public TextureTreeViewEntry(IEntry entry, string displayName = null)
//        {
//            Entry = entry;
//            DisplayName = displayName;
//            Sublinks = new ObservableCollectionExtended<TextureTreeViewEntry>();
//        }

//        //public void RefreshDisplayName()
//        //{
//        //    OnPropertyChanged(nameof(DisplayName));
//        //}

//        //public void RefreshSubText()
//        //{
//        //    loadedSubtext = false;
//        //    SubText = null;
//        //}

//        //private string _displayName;
//        //public string DisplayName
//        //{
//        //    get
//        //    {
//        //        try
//        //        {
//        //            if (_displayName != null) return _displayName;
//        //            string type = UIndex < 0 ? "(Imp) " : "(Exp) ";
//        //            string returnvalue = $"{UIndex} {Entry.ObjectName.Instanced}";
//        //            if (Properties.Settings.Default.PackageEditorWPF_ShowImpExpPrefix)
//        //            {
//        //                returnvalue = type + returnvalue;
//        //            }
//        //            returnvalue += $"({Entry.ClassName})";
//        //            return returnvalue;
//        //        }
//        //        catch (Exception)
//        //        {
//        //            return "ERROR GETTING DISPLAY NAME!";
//        //        }
//        //    }
//        //    set { _displayName = value; OnPropertyChanged(); }
//        //}

        
//        /*
//        private bool loadedSubtext = false;
//        private string _subtext;
//        public string SubText
//        {
//            get
//            {
//                if (!Properties.Settings.Default.PackageEditorWPF_ShowSubText) return null;
//                try
//                {
//                    if (loadedSubtext) return _subtext;
//                    if (Entry == null) return null;
//                    var ee = Entry as ExportEntry;

//                    if (ee != null)
//                    {
//                        //Parse as export
//                        switch (ee.ClassName)
//                        {
//                            case "Function":
//                                {
//                                    //check if exec
//                                    var data = ee.Data;
//                                    if (Entry.FileRef.Game == MEGame.ME3 ||
//                                        Entry.FileRef.Platform == MEPackage.GamePlatform.PS3)
//                                    {
//                                        var flags = EndianReader.ToInt32(data, data.Length - 4, ee.FileRef.Endian);
//                                        FlagValues fs = new FlagValues(flags, UE3FunctionReader._flagSet);
//                                        _subtext = "";
//                                        if (fs.HasFlag("Static"))
//                                        {
//                                            if (_subtext != "") _subtext += " ";
//                                            _subtext = "Static";
//                                        }
//                                        if (fs.HasFlag("Native"))
//                                        {
//                                            if (_subtext != "") _subtext += " ";
//                                            _subtext += "Native";
//                                            var nativeBackOffset = Entry.FileRef.Game < MEGame.ME3 ? 7 : 6;
//                                            var nativeIndex = EndianReader.ToInt16(data, data.Length - nativeBackOffset, ee.FileRef.Endian);
//                                            if (nativeIndex > 0)
//                                            {
//                                                _subtext += ", index " + nativeIndex;
//                                            }
//                                        }

//                                        if (fs.HasFlag("Exec"))
//                                        {
//                                            if (_subtext != "") _subtext += " ";
//                                            _subtext += "Exec - console command";
//                                        }

//                                        if (_subtext == "") _subtext = null;
//                                    }
//                                    else
//                                    {
//                                        //This could be -14 if it's defined as Net... we would have to decompile the whole function to know though...
//                                        var flags = EndianReader.ToInt32(data, data.Length - 12, ee.FileRef.Endian);
//                                        FlagValues fs = new FlagValues(flags, UE3FunctionReader._flagSet);
//                                        if (fs.HasFlag("Exec"))
//                                        {
//                                            _subtext = "Exec - console command";
//                                        }
//                                        else if (fs.HasFlag("Native"))
//                                        {
//                                            var nativeBackOffset = ee.FileRef.Game == MEGame.ME3 ? 6 : 7;
//                                            if (ee.Game < MEGame.ME3 &&
//                                                ee.FileRef.Platform != MEPackage.GamePlatform.PS3)
//                                                nativeBackOffset = 0xF;
//                                            var nativeIndex = EndianReader.ToInt16(data, data.Length - nativeBackOffset,
//                                                ee.FileRef.Endian);
//                                            if (nativeIndex > 0)
//                                            {
//                                                _subtext = "Native, index " + nativeIndex;
//                                            }
//                                            else
//                                            {
//                                                _subtext = "Native";
//                                            }
//                                        }
//                                    }

//                                    break;
//                                }
//                            case "Const":
//                                {
//                                    var data = ee.Data;
//                                    //This is kind of a hack. 
//                                    var value = EndianReader.ReadUnrealString(data, 0x14, ee.FileRef.Endian);
//                                    _subtext = "Value: " + value;
//                                    break;
//                                }
//                            case "ByteProperty":
//                            case "StructProperty":
//                            case "ObjectProperty":
//                            case "ComponentProperty":
//                                {
//                                    // Objects of this type
//                                    var typeRef = EndianReader.ToInt32(ee.Data, Entry.FileRef.Platform == MEPackage.GamePlatform.PC ? 0x2C : 0x20, ee.FileRef.Endian);
//                                    if (ee.FileRef.TryGetEntry(typeRef, out var type))
//                                    {
//                                        _subtext = type.ObjectName;
//                                    }

//                                    break;
//                                }
//                            case "ClassProperty":
//                                {
//                                    var data = ee.Data;
//                                    var typeRef = EndianReader.ToInt32(data, data.Length - 4, ee.FileRef.Endian);
//                                    if (ee.FileRef.TryGetEntry(typeRef, out var type))
//                                    {
//                                        _subtext = $"Class: {type.ObjectName}";
//                                    }
//                                    break;
//                                }
//                            case "Texture2D":
//                                {
//                                    var properties = ee.GetProperties();
//                                    var sizeX = properties.GetProp<IntProperty>("SizeX");
//                                    var sizeY = properties.GetProp<IntProperty>("SizeY");
//                                    _subtext = $"{sizeX?.Value}x{sizeY?.Value}";
//                                    break;
//                                }
//                        }

//                        if (BinaryInterpreterWPF.IsNativePropertyType(Entry.ClassName))
//                        {
//                            var data = ee.Data;
//                            //This is kind of a hack. 
//                            UnrealFlags.EPropertyFlags objectFlags =
//                                (UnrealFlags.EPropertyFlags)EndianReader.ToUInt64(data, 0x18, ee.FileRef.Endian);
//                            if ((objectFlags & UnrealFlags.EPropertyFlags.Config) != 0)
//                            {
//                                if (_subtext != null)
//                                {
//                                    _subtext = "Config, " + _subtext;
//                                }
//                                else
//                                {
//                                    _subtext = "Config";
//                                }
//                            }
//                        }
//                        else
//                        {
//                            var tag = ee.GetProperty<NameProperty>("Tag");
//                            if (tag != null && tag.Value.Name != Entry.ObjectName)
//                            {
//                                _subtext = tag.Value.Name;
//                            }
//                        }
//                    }

//                    if (_subtext == null)
//                    {

//                        // Parse if export or import
//                        switch (Entry.ClassName)
//                        {
//                            case "WwiseEvent":
//                                {
//                                    //parse out tlk id?
//                                    if (Entry.ObjectName.Name.StartsWith("VO_"))
//                                    {
//                                        var parsing = Entry.ObjectName.Name.Substring(3);
//                                        var nextUnderScore = parsing.IndexOf("_");
//                                        if (nextUnderScore > 0)
//                                        {
//                                            parsing = parsing.Substring(0, nextUnderScore);
//                                            if (int.TryParse(parsing, out var parsedInt))
//                                            {
//                                                //Lookup TLK
//                                                var data = TLKManagerWPF.GlobalFindStrRefbyID(parsedInt, Entry.FileRef);
//                                                if (data != "No Data")
//                                                {
//                                                    _subtext = data;
//                                                }
//                                            }
//                                        }
//                                    }

//                                    break;
//                                }
//                            case "WwiseStream":
//                                {
//                                    //parse out tlk id?
//                                    var splits = Entry.ObjectName.Name.Split('_', ',');
//                                    for (int i = splits.Length - 1; i > 0; i--)
//                                    {
//                                        //backwards is faster
//                                        if (int.TryParse(splits[i], out var parsed))
//                                        {
//                                            //Lookup TLK
//                                            var data = TLKManagerWPF.GlobalFindStrRefbyID(parsed, Entry.FileRef);
//                                            if (data != "No Data")
//                                            {
//                                                _subtext = data;
//                                            }
//                                        }
//                                    }

//                                    break;
//                                }
//                        }
//                    }

//                    loadedSubtext = true;
//                    return _subtext;
//                }
//                catch (Exception)
//                {
//                    loadedSubtext = true;
//                    _subtext = "ERROR GETTING SUBTEXT!";
//                    return "ERROR GETTING SUBTEXT!";
//                }
//            }
//            set { _subtext = value; OnPropertyChanged(); }
//        }*/

//        /*
//        public int UIndex => Entry?.UIndex ?? 0;

//        private System.Windows.Media.Brush _foregroundColor;
//        public System.Windows.Media.Brush ForegroundColor
//        {
//            get => Entry is ImportEntry ? ImportEntryBrush : ExportEntryBrush;
//            set
//            {
//                _foregroundColor = value;
//                OnPropertyChanged();
//            }
//        }

//        private static SolidColorBrush ImportEntryBrush => SystemColors.GrayTextBrush;
//        private static SolidColorBrush ExportEntryBrush => SystemColors.ControlTextBrush;
//        */
//        public override string ToString()
//        {
//            return "TextureTreeViewEntry " + DisplayName;
//        }

//        /*
//        /// <summary>
//        /// Sorts this node's children in ascending positives first, then descending negatives
//        /// </summary>
//        internal void SortChildren()
//        {
//            var exportNodes = Sublinks.Where(x => x.Entry.UIndex > 0).OrderBy(x => x.UIndex).ToList();
//            var importNodes = Sublinks.Where(x => x.Entry.UIndex < 0).OrderByDescending(x => x.UIndex).ToList();

//            exportNodes.AddRange(importNodes);
//            Sublinks.ClearEx();
//            Sublinks.AddRange(exportNodes);
//        }*/
//    }
//}