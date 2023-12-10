using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LegendaryExplorerCore.Gammtek.Collections.Specialized;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Packages
{
    [Flags]
    public enum PackageChange
    {
        Export = 0x1,
        Import = 0x2,
        Name = 0x4,
        Add = 0x8,
        Remove = 0x10,
        Data = 0x20,
        Header = 0x40,
        Entry = 0x80,
        EntryAdd = Entry | Add,
        EntryRemove = Entry | Remove,
        EntryHeader = Entry | Header,
        ExportData = Export | Data | Entry,
        ExportHeader = Export | EntryHeader,
        ImportHeader = Import | EntryHeader,
        ExportAdd = Export | EntryAdd,
        ImportAdd = Import | EntryAdd,
        ExportRemove = Export | EntryRemove,
        ImportRemove = Import | EntryRemove,
        NameAdd = Name | Add,
        NameRemove = Name | Remove,
        NameEdit = Name | Data
    }

    [DebuggerDisplay("PackageUpdate | {Change} on index {Index}")]
    public readonly struct PackageUpdate
    {
        /// <summary>
        /// Details on what piece of data has changed
        /// </summary>
        public readonly PackageChange Change;
        /// <summary>
        /// index of what item has changed. Meaning depends on value of Change
        /// </summary>
        public readonly int Index;

        public PackageUpdate(PackageChange change, int index)
        {
            this.Change = change;
            this.Index = index;
        }
    }

    public abstract partial class UnrealPackageFile : INotifyPropertyChanged
    {
        public const uint packageTagLittleEndian = 0x9E2A83C1; //Default, PC
        public const uint packageTagBigEndian = 0xC1832A9E;
        public string FilePath { get; }
        public string FileNameNoExtension { get; }
        public bool IsModified { get; protected set; }
        public int FullHeaderSize { get; protected set; }
        public UnrealFlags.EPackageFlags Flags { get; protected set; }
        public int NameCount { get; protected set; }
        public int NameOffset { get; protected set; }
        public int ExportCount { get; protected set; }
        public int ExportOffset { get; protected set; }
        public int ImportCount { get; protected set; }
        public int ImportOffset { get; protected set; }
        public int NumCompressedChunksAtLoad { get; protected set; }
        public int DependencyTableOffset { get; protected set; }
        public Guid PackageGuid { get; set; }

        /// <summary>
        /// For concurrency when rebuilding the lookup table
        /// </summary>
        private object _packageSyncObj = new object();

        /// <summary>
        /// For concurrency when accessing FindExport/Import/Entry, and the table needs regenerated. This prevents multi-threading use from searching a currently rebuilding lookup table
        /// </summary>
        private object _findEntrySyncObj = new object();

        public bool IsCompressed => Flags.Has(UnrealFlags.EPackageFlags.Compressed);

        /// <summary>
        /// A lookup table that maps the full instanced path of an entry to that entry, which makes looking up entries by name quick.
        /// ONLY WORKS properly if there are NO duplicate indexes (besides trash) in the package.
        /// </summary>
        protected CaseInsensitiveDictionary<IEntry> EntryLookupTable;
        private EntryTree _tree;
        private bool lookupTableNeedsToBeRegenerated = true;
        public void InvalidateLookupTable() => lookupTableNeedsToBeRegenerated = true;

        public EntryTree Tree
        {
            get
            {
                if (lookupTableNeedsToBeRegenerated)
                {
                    RebuildLookupTable();
                }
                return _tree;
            }
        }

        public enum CompressionType
        {
            None = 0,
            Zlib = 0x1, // PC ME3
            LZO = 0x2, //ME1 and ME2 PC
            LZX = 0x4, //Xbox
            LZMA = 0x8, //WiiU, PS3 
            OodleLeviathan = 0x400 // LE1?
        }

        private List<ME1TalkFile> localTlks;
        public List<ME1TalkFile> LocalTalkFiles => localTlks ??= ReadLocalTLKs();

        /// <summary>
        /// Sets the list of LocalTLKs - use for changing the language of locally loaded TLKs
        /// </summary>
        /// <param name="tlks">List of TLKs to use locally from this package</param>
        public void SetLocalTLKs(IEnumerable<ME1TalkFile> tlks)
        {
            if (localTlks == null)
                localTlks = new List<ME1TalkFile>();
            else
                localTlks.Clear();
            localTlks.AddRange(tlks);
        }

        public static ushort UnrealVersion(MEGame game) => game switch
        {
            MEGame.ME1 => MEPackage.ME1UnrealVersion,
            MEGame.ME2 => MEPackage.ME2UnrealVersion,
            MEGame.ME3 => MEPackage.ME3UnrealVersion,
            MEGame.LE1 => MEPackage.LE1UnrealVersion,
            MEGame.LE2 => MEPackage.LE2UnrealVersion,
            MEGame.LE3 => MEPackage.LE3UnrealVersion,
            MEGame.UDK => UDKPackage.UDKUnrealVersion2015, // This is technically not correct since UDK has many versions we support
            _ => throw new ArgumentOutOfRangeException(nameof(game), game, null)
        };

        public static ushort LicenseeVersion(MEGame game) => game switch
        {
            MEGame.ME1 => MEPackage.ME1LicenseeVersion,
            MEGame.ME2 => MEPackage.ME2LicenseeVersion,
            MEGame.ME3 => MEPackage.ME3LicenseeVersion,
            MEGame.LE1 => MEPackage.LE1LicenseeVersion,
            MEGame.LE2 => MEPackage.LE2LicenseeVersion,
            MEGame.LE3 => MEPackage.LE3LicenseeVersion,
            MEGame.UDK => UDKPackage.UDKLicenseeVersion,
            _ => throw new ArgumentOutOfRangeException(nameof(game), game, null)
        };

        #region Names
        protected uint namesAdded;


        // Used to make name lookups quick when doing a contains operation as this method is called
        // quite often
        protected readonly CaseInsensitiveDictionary<int> nameLookupTable = new();

        protected List<string> names;
        public IReadOnlyList<string> Names => names;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsName(int index) => index >= 0 && index < names.Count;

        public string GetNameEntry(int index) => IsName(index) ? names[index] : "";


        public int FindNameOrAdd(string name)
        {
            if (nameLookupTable.TryGetValue(name, out var index))
            {
                return index;
            }

            addName(name, true); //Don't bother doing a lookup as we just did one. 
            // If this was an issue it'd be a multithreading issue that still could occur and is an
            // issue in the user code
            return names.Count - 1;
        }

        protected void addName(string name, bool skipLookup = false)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new Exception("Cannot add a null/empty name to the list of names for a package file.\nThis is a bug in LegendaryExplorerCore.");
            }

            if (skipLookup || !nameLookupTable.TryGetValue(name, out var index))
            {
                names.Add(name);
                namesAdded++;
                nameLookupTable[name] = names.Count - 1;
                NameCount = names.Count;

                UpdateTools(PackageChange.NameAdd, NameCount - 1);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameCount)));
                IsModified = true;
            }
        }

        public void replaceName(int idx, string newName)
        {
            if (newName == null)
            {
                // Cannot add a null name!
                throw new ArgumentException(nameof(newName), new Exception("Cannot replace a name with a null value!"));
            }
            if (IsName(idx) && !names[idx].Equals(newName, StringComparison.InvariantCultureIgnoreCase))
            {
                nameLookupTable.Remove(names[idx]);
                names[idx] = newName;
                nameLookupTable[newName] = idx;
                IsModified = true; // Package has become modified
                UpdateTools(PackageChange.NameEdit, idx);
                InvalidateLookupTable(); //If name of object was changed this could change all instanced paths
            }
        }

        /// <summary>
        /// Checks whether a name exists in the PCC and returns its index
        /// If it doesn't exist returns -1
        /// </summary>
        /// <param name="nameToFind">The name of the string to find</param>
        /// <returns></returns>
        public int findName(string nameToFind)
        {
            if (nameLookupTable.TryGetValue(nameToFind, out var index))
            {
                return index;
            }
            return -1;
        }

        public void restoreNames(List<string> list)
        {
            names = list;
            mapNames();
            NameCount = names.Count;
        }

        private void mapNames()
        {
            nameLookupTable.Clear();
            int i = 0;
            foreach (var name in names)
            {
                nameLookupTable[name] = i;
                i++;
            }
        }

        /// <summary>
        /// Gets the next available index for a name, checking for other objects incrementally with a same instanced full name until a free one is found that is higher than the original one
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        public int GetNextIndexForInstancedName(IEntry entry)
        {
            var parentName = entry.ParentInstancedFullPath;
            var baseName = string.IsNullOrWhiteSpace(parentName) ? entry.ObjectName.Name : $"{parentName}.{entry.ObjectName.Name}";

            var index = entry.ObjectName.Number;
            while (true)
            {
                index++;
                if (FindEntry($"{baseName}_{index - 1}") == null)
                    return index;
            }
        }

        public NameReference GetNextIndexedName(string name)
        {
            name = name.Trim().Replace(' ', '_'); //no spaces 

            int index = 0;
            foreach (ExportEntry ent in exports)
            {
                if (name == ent.ObjectName && ent.ObjectName.Number > index)
                {
                    index = ent.ObjectName.Number;
                }
            }

            return new NameReference(name, index + 1);
        }

        #endregion

        #region Exports
        protected List<ExportEntry> exports;
        public IReadOnlyList<ExportEntry> Exports => exports;

        public bool IsUExport(int uindex) => uindex > 0 && uindex <= exports.Count;

        public void AddExport(ExportEntry exportEntry)
        {
            // Uncomment this to debug when an export is being added
            //if (exportEntry.ObjectName == @"SFXPower_Pull_Heavy_Hench")
            //    Debugger.Break();

            if (exportEntry.FileRef != this)
                throw new Exception("Cannot add an export entry from another package file");

            exportEntry.DataChanged = true;
            exportEntry.HeaderOffset = 1; //This will make it so when setting idxLink it knows the export has been attached to the tree, even though this doesn't do anything. Find by offset may be confused by this. Updates on save
            exportEntry.Index = exports.Count;
            exportEntry.PropertyChanged += exportChanged;
            exports.Add(exportEntry);

            ExportCount = exports.Count;

            if (!lookupTableNeedsToBeRegenerated)
            {
                // CROSSGEN-V: CHECK BEFORE ADDING TO MAKE SURE WE DON'T GOOF IT UP
#if DEBUG
                if (EntryLookupTable.TryGetValue(exportEntry.InstancedFullPath, out _))
                {
                    // Debug.WriteLine($"ENTRY LOOKUP TABLE ALREADY HAS ITEM BEING ADDED!!! ITEM: {exportEntry.InstancedFullPath}");
                    //Debugger.Break(); // This already exists!
                }
#endif
                // END CROSSGEN-V
                EntryLookupTable[exportEntry.InstancedFullPath] = exportEntry;
                _tree.Add(exportEntry);
            }

            //Debug.WriteLine($@" >> Added export {exportEntry.InstancedFullPath}");


            UpdateTools(PackageChange.ExportAdd, exportEntry.UIndex);
            //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs((nameof(ExportCount));
        }

        public IEntry FindEntry(string instancedname)
        {
            IEntry matchingEntry;
            // START CRITICAL SECTION ---------------------------------
            lock (_findEntrySyncObj)
            {
                if (lookupTableNeedsToBeRegenerated)
                {
                    RebuildLookupTable();
                }
                EntryLookupTable.TryGetValue(instancedname, out matchingEntry);
            }
            // END CRITICAL SECTION ------------------------------------
            return matchingEntry;
        }

        public ImportEntry FindImport(string instancedname)
        {
            IEntry matchingEntry;
            // START CRITICAL SECTION ---------------------------------
            lock (_findEntrySyncObj)
            {
                if (lookupTableNeedsToBeRegenerated)
                {
                    RebuildLookupTable();
                }
                EntryLookupTable.TryGetValue(instancedname, out matchingEntry);
            }
            // END CRITICAL SECTION ------------------------------------

            if (matchingEntry is ExportEntry)
            {
                // We want import version
                // Some files like LE2 Engine.pcc have imports and exports for same named thing
                // for some reason
                // Look manually for object
                return Imports.FirstOrDefault(x => x.InstancedFullPath == instancedname);
            }
            return matchingEntry as ImportEntry;
        }

        public ExportEntry FindExport(string instancedname)
        {
            IEntry matchingEntry;
            // START CRITICAL SECTION ---------------------------------
            lock (_findEntrySyncObj)
            {
                if (lookupTableNeedsToBeRegenerated)
                {
                    RebuildLookupTable();
                }
                EntryLookupTable.TryGetValue(instancedname, out matchingEntry);
            }
            // END CRITICAL SECTION ------------------------------------

            if (matchingEntry is ImportEntry)
            {
                // We want export version
                // Some files like LE2 Engine.pcc have imports and exports for same named thing
                // for some reason
                // Look manually for object
                return Exports.FirstOrDefault(x => x.InstancedFullPath == instancedname);
            }

            return matchingEntry as ExportEntry;
        }

        public ExportEntry GetUExport(int uindex) => exports[uindex - 1];

        public bool TryGetUExport(int uIndex, out ExportEntry export)
        {
            if (IsUExport(uIndex))
            {
                export = GetUExport(uIndex);
                return true;
            }

            export = null;
            return false;
        }
        #endregion

        #region Imports
        protected List<ImportEntry> imports;
        public IReadOnlyList<ImportEntry> Imports => imports;

        /// <summary>
        /// Determines if this is an Import based on it's UIndex
        /// </summary>
        /// <param name="uindex"></param>
        /// <returns></returns>
        public bool IsImport(int uindex) => uindex < 0 && uindex >= -imports.Count;

        /// <summary>
        /// Adds an import to the tree. This method is used to add new imports.
        /// </summary>
        /// <param name="importEntry"></param>
        public void AddImport(ImportEntry importEntry)
        {
            if (importEntry.FileRef != this)
                throw new Exception("you cannot add a new import entry from another package file, it has invalid references!");

            // If you need to catch a certain import being added
            // uncomment the following
            //if (importEntry.InstancedFullPath == "BIOC_Materials")
            //    Debugger.Break();

            importEntry.Index = imports.Count;
            importEntry.PropertyChanged += importChanged;
            importEntry.HeaderOffset = 1; //This will make it so when setting idxLink it knows the import has been attached to the tree, even though this doesn't do anything. Find by offset may be confused by this. Updates on save
            imports.Add(importEntry);

            if (!lookupTableNeedsToBeRegenerated)
            {
                if (EntryLookupTable.TryGetValue(importEntry.InstancedFullPath, out _))
                {
                    // Debug.WriteLine($"ENTRY LOOKUP TABLE ALREADY HAS ITEM BEING ADDED!!! ITEM: {importEntry.InstancedFullPath}");
                    //Debugger.Break(); // This already exists!
                }
                EntryLookupTable[importEntry.InstancedFullPath] = importEntry;
                _tree.Add(importEntry);
            }

            importEntry.EntryHasPendingChanges = true;
            ImportCount = imports.Count;

            UpdateTools(PackageChange.ImportAdd, importEntry.UIndex);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImportCount)));
        }

        /// <summary>
        /// Rebuilds the lookup table for this package. Call when there are name changes or the name of an entry is changed. May
        /// need to be optimized in a way so this is not called during things like porting so the list is not constantly rebuilt.
        /// </summary>
        public void RebuildLookupTable()
        {
            // This needs locked or multithreaded use might corrupt the lookup table
            // We don't want it to be the concurrent version since we don't want the lookup table being modified
            // in multiple locations at the same time
            lock (_packageSyncObj)
            {
                EntryLookupTable.Clear();
                //pre-order traversal of entry tree
                //this is superior to just looping through the export and import arrays and calculating the InstancedFullPath anew for each one
                //as InstancedFullPath has to recurse up from leaf to root, performing multiple string concats per node.
                var tree = new EntryTree((IMEPackage)this);
                var stack = new Stack<(TreeNode<IEntry, int>, string, int)>(8); //max tree depth will rarely be more than 8
                foreach (TreeNode<IEntry, int> root in tree.Roots)
                {
                    stack.Clear();
                    string objFullPath = root.Data.ObjectName.Instanced;
                    EntryLookupTable[objFullPath] = root.Data;
                    if (root.Children.Count is 0)
                    {
                        continue;
                    }

                    stack.Push((root, objFullPath, 0));
                    while (true)
                    {
                        if (stack.Count is 0)
                        {
                            break;
                        }

                        (TreeNode<IEntry, int> node, objFullPath, int i) = stack.Pop();
                        if (i + 1 < node.Children.Count)
                        {
                            stack.Push((node, objFullPath, i + 1));
                        }

                        node = tree[node.Children[i]];
                        objFullPath = node.Data.ObjectName.AddToPath(objFullPath);
                        EntryLookupTable[objFullPath] = node.Data;
                        if (node.Children.Count > 0)
                        {
                            stack.Push((node, objFullPath, 0));
                        }
                    }
                }

                this._tree = tree;
                lookupTableNeedsToBeRegenerated = false;
            }
        }

        public ImportEntry GetImport(int uIndex) => imports[Math.Abs(uIndex) - 1];
        public bool TryGetImport(int uIndex, out ImportEntry import)
        {
            if (IsImport(uIndex))
            {
                import = GetImport(uIndex);
                return true;
            }

            import = null;
            return false;
        }

        #endregion

        #region IEntry
        /// <summary>
        ///     gets Export or Import name
        /// </summary>
        /// <param name="uIndex">unreal index</param>
        public string getObjectName(int uIndex)
        {
            if (IsEntry(uIndex))
                return GetEntry(uIndex).ObjectName;
            if (uIndex == 0)
                return "Class";
            return "";
        }

        /// <summary>
        ///     gets Export or Import entry
        /// </summary>
        /// <param name="uindex">unreal index</param>
        public IEntry GetEntry(int uindex)
        {
            if (IsUExport(uindex))
                return exports[uindex - 1];
            if (IsImport(uindex))
                return imports[-uindex - 1];
            return null;
        }
        public bool IsEntry(int uindex) => IsUExport(uindex) || IsImport(uindex);
        public bool TryGetEntry(int uIndex, out IEntry entry)
        {
            if (IsEntry(uIndex))
            {
                entry = GetEntry(uIndex);
                return true;
            }

            entry = null;
            return false;
        }

        public void RemoveTrailingTrash()
        {
            ExportEntry trashPackage = FindExport(TrashPackageName);
            if (trashPackage == null)
            {
                return;
            }
            int trashPackageUIndex = trashPackage.UIndex;
            //make sure the first trashed export is the trashpackage
            foreach (ExportEntry exp in exports)
            {
                if (exp == trashPackage)
                {
                    //trashpackage is the first trashed export, so we're good
                    break;
                }
                if (exp.idxLink == trashPackageUIndex)
                {
                    //turn this into trashpackage, turn old trashpackage into regular Trash, and point all trash entries to the new trashpackage
                    exp.ObjectName = TrashPackageName;
                    exp.idxLink = 0;
                    exp.PackageGUID = TrashPackageGuid;

                    trashPackage.ObjectName = "Trash";
                    trashPackage.idxLink = exp.UIndex;
                    trashPackage.PackageGUID = Guid.Empty;

                    foreach (IEntry entry in trashPackage.GetChildren())
                    {
                        entry.idxLink = exp.UIndex;
                    }

                    trashPackage = exp;
                    trashPackageUIndex = trashPackage.UIndex;
                    break;
                }
            }


            //remove imports
            for (int i = ImportCount - 1; i >= 0; i--)
            {
                ImportEntry lastImport = imports[i];
                if (lastImport.idxLink != trashPackageUIndex)
                {
                    //non-trash import, so stop removing
                    break;
                }

                lastImport.PropertyChanged -= importChanged;
                imports.RemoveAt(i);
                UpdateTools(PackageChange.ImportRemove, lastImport.UIndex);
                IsModified = true;
            }
            if (ImportCount != imports.Count)
            {
                ImportCount = imports.Count;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImportCount)));
            }

            //remove exports
            for (int i = ExportCount - 1; i >= 0; i--)
            {
                ExportEntry lastExport = exports[i];
                if (lastExport.idxLink != trashPackageUIndex)
                {
                    //non-trash export, so stop removing
                    break;
                }

                lastExport.PropertyChanged -= exportChanged;
                exports.RemoveAt(i);
                UpdateTools(PackageChange.ExportRemove, lastExport.UIndex);
                IsModified = true;
            }
            if (ExportCount != exports.Count)
            {
                ExportCount = exports.Count;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExportCount)));
            }
            //if there are no more trashed imports or exports, and if the TrashPackage is the last export, remove it
            if (exports.LastOrDefault() is ExportEntry finalExport && finalExport == trashPackage)
            {
                if (trashPackage.GetChildren().IsEmpty())
                {
                    trashPackage.PropertyChanged -= exportChanged;
                    exports.Remove(trashPackage);
                    UpdateTools(PackageChange.ExportRemove, trashPackage.UIndex);
                    IsModified = true;
                }
            }

            if (ExportCount != exports.Count)
            {
                ExportCount = exports.Count;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExportCount)));
            }
            InvalidateLookupTable();
        }

        #endregion

        private DateTime? lastSaved;
        public DateTime LastSaved
        {
            get
            {
                if (lastSaved.HasValue)
                {
                    return lastSaved.Value;
                }

                if (File.Exists(FilePath))
                {
                    return (new FileInfo(FilePath)).LastWriteTime;
                }

                return DateTime.MinValue;
            }
        }

        public long FileSize => File.Exists(FilePath) ? (new FileInfo(FilePath)).Length : 0;

        protected virtual void AfterSave()
        {
            //We do if checks here to prevent firing tons of extra events as we can't prevent firing change notifications if 
            //it's not really a change due to the side effects of suppressing that.
            foreach (var export in exports)
            {
                if (export.DataChanged)
                {
                    export.DataChanged = false;
                }
                if (export.HeaderChanged)
                {
                    export.HeaderChanged = false;
                }
                if (export.EntryHasPendingChanges)
                {
                    export.EntryHasPendingChanges = false;
                }
            }
            foreach (var import in imports)
            {
                if (import.HeaderChanged)
                {
                    import.HeaderChanged = false;
                }
                if (import.EntryHasPendingChanges)
                {
                    import.EntryHasPendingChanges = false;
                }
            }
            namesAdded = 0;

            lastSaved = DateTime.Now;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastSaved)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileSize)));
            IsModified = false;
        }

        /// <summary>
        /// Reads local TLK exports. Only use in Game 1 packages.
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public List<ME1TalkFile> ReadLocalTLKs(string language = null, bool getAllGenders = false)
        {
            var tlks = new List<ME1TalkFile>();
            var langToMatch = language ?? LegendaryExplorerCoreLibSettings.Instance.TLKDefaultLanguage;
            if (this is MEPackage mePackage && mePackage.Game.IsGame1() && mePackage.Platform == MEPackage.GamePlatform.PC)
            {
                var exportsToLoad = new List<ExportEntry>();
                var processedExports = new List<int>();
                foreach (var tlkFileSet in Exports.Where(x => x.ClassName == "BioTlkFileSet" && !x.IsDefaultObject).Select(exp => exp.GetBinaryData<BioTlkFileSet>()))
                {
                    bool addedLoad = false;
                    foreach ((NameReference lang, BioTlkFileSet.BioTlkSet bioTlkSet) in tlkFileSet.TlkSets)
                    {
                        if (!addedLoad && langToMatch.Equals(lang, StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (getAllGenders)
                            {
                                exportsToLoad.Add(GetUExport(bioTlkSet.Male));
                                exportsToLoad.Add(GetUExport(bioTlkSet.Female));
                            }
                            else
                            {
                                exportsToLoad.Add(GetUExport(LegendaryExplorerCoreLibSettings.Instance.TLKGenderIsMale ? bioTlkSet.Male : bioTlkSet.Female));
                            }
                            addedLoad = true;
                        }
                        processedExports.Add(bioTlkSet.Male);
                        processedExports.Add(bioTlkSet.Female);
                    }
                }

                // Global TLK
                foreach (var tlk in Exports.Where(x => x.ClassName == "BioTlkFile" && !x.IsDefaultObject && !processedExports.Contains(x.UIndex)))
                {
                    exportsToLoad.Add(tlk);
                    processedExports.Add(tlk.UIndex); // This is technically not necessary in code path but might be useful if this code changes in future.
                }
                foreach (var exp in exportsToLoad)
                {
                    //Debug.WriteLine("Loading local TLK: " + exp.GetIndexedFullPath);
                    tlks.Add(new ME1TalkFile(exp));
                }
            }
            return tlks;
        }

        #region packageHandler stuff

        private readonly List<IPackageUser> _users = new();
        public IReadOnlyCollection<IPackageUser> Users => _users;
        public WeakCollection<IWeakPackageUser> WeakUsers { get; } = new();

        public void RegisterTool(IPackageUser user)
        {
            // DEBUGGING MEMORY LEAK CODE
            //Debug.WriteLine($"{FilePath} RefCount incrementing from {RefCount} to {RefCount + 1} due to RegisterTool()");
            RefCount++;
            _users.Add(user);
            user.RegisterClosed(() =>
            {
                ReleaseUser(user);
                Dispose();
            });
        }

        public void Release(IPackageUser user = null)
        {
            if (user != null)
            {
                user = _users.FirstOrDefault(x => x == user);
                if (user != null)
                {
                    ReleaseUser(user);
                }
                else
                {
                    Debug.WriteLine("Releasing package that isn't in use by any user");
                }
            }
            Dispose();
        }

        private void ReleaseUser(IPackageUser user)
        {
            _users.Remove(user);
            if (_users.Count == 0)
            {
                NoLongerOpenInTools?.Invoke(this);
            }
            user.ReleaseUse();
        }

        public delegate void MEPackageEventHandler(UnrealPackageFile sender);
        public event MEPackageEventHandler NoLongerOpenInTools;

        protected void exportChanged(object sender, PropertyChangedEventArgs e)
        {
            // If we are never using the global cache there is no point
            // to notifying other things because nothing will share the 
            // package file
            if (sender is ExportEntry exp)
            {
                switch (e.PropertyName)
                {
                    case nameof(ExportEntry.DataChanged):
                        UpdateTools(PackageChange.ExportData, exp.UIndex);
                        break;
                    case nameof(ExportEntry.HeaderChanged):
                        UpdateTools(PackageChange.ExportHeader, exp.UIndex);
                        break;
                }
            }
        }

        protected void importChanged(object sender, PropertyChangedEventArgs e)
        {
            if (MEPackageHandler.GlobalSharedCacheEnabled && sender is ImportEntry imp && e.PropertyName == nameof(ImportEntry.HeaderChanged))
            {
                UpdateTools(PackageChange.ImportHeader, imp.UIndex);
            }
        }

        private readonly object _updatelock = new();
        private readonly HashSet<PackageUpdate> pendingUpdates = new();

        //Once this many milliseconds have gone by without a new change being queued, all the pending updates will be broadcast to the Users and WeakUsers
        private const int QUEUING_DELAY = 50;
        private Timer updateTimer;

        private void UpdateToolsCallback(object _)
        {
            lock (_updatelock)
            {
                updateTimer.Dispose();
                updateTimer = null;
            }

            //Runs update handling on the UI thread (or whatever thread the sync context is associated with if this is used in a non-GUI app)
            new TaskFactory(LegendaryExplorerCoreLib.SYNCHRONIZATION_CONTEXT).StartNew(() =>
            {
                List<PackageUpdate> updates;
                lock (_updatelock)
                {
                    updates = pendingUpdates.ToList();
                    pendingUpdates.Clear();
                }
                var removedImports = updates.Where(u => u.Change == PackageChange.ImportRemove).Select(u => u.Index).ToList();
                var removedExports = updates.Where(u => u.Change == PackageChange.ExportRemove).Select(u => u.Index).ToList();
                var pendingUpdatesList = new List<PackageUpdate>();
                //remove add/change updates for entries that have been removed
                foreach (PackageUpdate upd in updates)
                {
                    switch (upd.Change)
                    {
                        case PackageChange.ExportAdd:
                        case PackageChange.ExportData:
                        case PackageChange.ExportHeader:
                            {
                                if (!removedExports.Contains(upd.Index))
                                {
                                    pendingUpdatesList.Add(upd);
                                }
                                break;
                            }
                        case PackageChange.ImportAdd:
                        case PackageChange.ImportHeader:
                            {
                                if (!removedImports.Contains(upd.Index))
                                {
                                    pendingUpdatesList.Add(upd);
                                }
                                break;
                            }
                        default:
                            pendingUpdatesList.Add(upd);
                            break;
                    }
                }
                //WeakUsers needs to come before Users so that FileLib will invalidate BEFORE ScriptEditor refreshes.
                //This is hacky, and some sort of User priority system should be implemented in the future
                foreach (var item in WeakUsers.Concat(_users))
                {
                    item.HandleUpdate(pendingUpdatesList);
                }
            });
        }

        private void UpdateTools(PackageChange change, int index)
        {
            if (_users.Count == 0 && WeakUsers.Count == 0)
            {
                return;
            }
            var update = new PackageUpdate(change, index);
            lock (_updatelock)
            {
                pendingUpdates.Add(update);
                updateTimer ??= new Timer(UpdateToolsCallback);
                updateTimer.Change(QUEUING_DELAY, Timeout.Infinite);
            }
        }

        public event MEPackageEventHandler NoLongerUsed;
        /// <summary>
        /// Amount of known tracked references to this object that were acquired through OpenMEPackage(). Manual references are not tracked
        /// </summary>
        public int RefCount { get; private set; }

        public void RegisterUse()
        {
            // DEBUGGING MEMORY LEAK CODE
            //Debug.WriteLine($"{FilePath} RefCount incrementing from {RefCount} to {RefCount + 1}");
            RefCount++;
        }

        /// <summary>
        /// Doesn't neccesarily dispose the object.
        /// Will only do so once this has been called by every place that uses it.
        /// HIGHLY Recommend using the using block instead of calling this directly.
        /// </summary>
        public void Dispose()
        {
            // DEBUGGING MEMORY LEAK CODE
            //Debug.WriteLine($"{FilePath} RefCount decrementing from {RefCount} to {RefCount - 1}");

            RefCount--;

            if (RefCount == 0)
            {
                NoLongerUsed?.Invoke(this);
            }
        }
        #endregion

        public const string TrashPackageName = "ME3ExplorerTrashPackage";
        public static readonly Guid TrashPackageGuid = "ME3ExpTrashPackage".ToGuid(); //DO NOT EDIT!!

        protected UnrealPackageFile(string filePath)
        {
            FilePath = filePath;
            if (filePath is not null)
            {
                FileNameNoExtension = Path.GetFileNameWithoutExtension(filePath);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
