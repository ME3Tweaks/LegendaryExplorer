using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LegendaryExplorerCore.Gammtek.Collections.Specialized;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Newtonsoft.Json;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.Packages
{
    public enum ArrayType
    {
        Object,
        Name,
        Enum,
        Struct,
        Bool,
        String,
        Float,
        Int,
        Byte,
        StringRef
    }

    [DebuggerDisplay("PropertyInfo | {Type} , reference: {Reference}, transient: {Transient}")]
    public class PropertyInfo : IEquatable<PropertyInfo>
    {
        //DO NOT CHANGE THE NAME OF ANY OF THESE fields/properties. THIS WILL BREAK JSON PARSING!
        public PropertyType Type { get; }
        public string Reference { get; }
        public bool Transient { get; }
        public int StaticArrayLength { get; }

        public PropertyInfo(PropertyType type, string reference = null, bool transient = false, int staticArrayLength = 1)
        {
            Type = type;
            Reference = reference;
            Transient = transient;
            StaticArrayLength = staticArrayLength;
        }

        public bool IsEnumProp() => Type == PropertyType.ByteProperty && Reference != null && Reference != "Class" && Reference != "Object";

        public bool IsStaticArray() => StaticArrayLength > 1;

        #region IEquatable

        public bool Equals(PropertyInfo other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Type == other.Type && string.Equals(Reference, other.Reference) && Transient == other.Transient;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == this.GetType() && Equals((PropertyInfo)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, Reference, Transient);
        }

        public static bool operator ==(PropertyInfo left, PropertyInfo right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(PropertyInfo left, PropertyInfo right)
        {
            return !Equals(left, right);
        }

        #endregion
    }

    public class ClassInfo
    {
        //DO NOT CHANGE THE NAME OF ANY OF THESE fields/properties. THIS WILL BREAK JSON PARSING!
        [JsonIgnore]
        public string ClassName { get; set; }

#pragma warning disable CS0618 // Type or member is obsolete
        public OrderedMultiValueDictionary<NameReference, PropertyInfo> properties = [];
#pragma warning restore CS0618 // Type or member is obsolete
        public string baseClass;
        //Relative to BIOGame
        public string pccPath;

        public int exportIndex;
        public bool isAbstract;

        /// <summary>
        /// If the export is forcedexport, which changes how we have to reference it for an import
        /// </summary>
        public bool forcedExport;

        /// <summary>
        /// The instanced full path of the object. This is not serialized; only populated when dynamically loading
        /// </summary>
        [JsonIgnore]
        public string instancedFullPath;

        public bool TryGetPropInfo(NameReference name, MEGame game, out PropertyInfo propInfo) =>
            properties.TryGetValue(name, out propInfo) || (GlobalUnrealObjectInfo.GetClassOrStructInfo(game, baseClass)?.TryGetPropInfo(name, game, out propInfo) ?? false);
    }

    public interface IMEPackage : IDisposable
    {
        EPackageFlags Flags { get; }
        bool IsCompressed { get; }
        int NameCount { get; }
        int ExportCount { get; }
        int ImportCount { get; }
        int ImportOffset { get; }
        int ExportOffset { get; }
        int NameOffset { get; }
        /// <summary>
        /// The number of compressed chunks in the chunk table there were found during package loading.
        /// </summary>
        int NumCompressedChunksAtLoad { get; }
        Guid PackageGuid { get; set; }
        IReadOnlyList<ExportEntry> Exports { get; }
        IReadOnlyList<ImportEntry> Imports { get; }
        IReadOnlyList<string> Names { get; }
        MEGame Game { get; }
        MEPackage.GamePlatform Platform { get; }
        Endian Endian { get; }
        MELocalization Localization { get; }
        string FilePath { get; }
        public string FileNameNoExtension { get; }
        DateTime LastSaved { get; }
        long FileSize { get; }

        /// <summary>
        /// Custom user-defined metadata to associate with this package object. This data has no effect on saving or loading, it is only for library user convenience. This is not serialized!
        /// </summary>
        public Dictionary<string, object> CustomMetadata { get; set; }

        /// <summary>
        /// Data read from the LECL tag (LE only)
        /// </summary>
        public LECLData LECLTagData { get; }

        //reading
        bool IsUExport(int index);
        bool IsName(int index);
        /// <summary>
        /// Checks if the specified UIndex is an import
        /// </summary>
        /// <param name="uindex"></param>
        /// <returns></returns>
        bool IsImport(int uindex);
        bool IsEntry(int uindex);
        /// <summary>
        ///     gets Export or Import entry, from unreal index. Returns null for invalid UIndexes
        /// </summary>
        /// <param name="index">unreal index</param>
        IEntry GetEntry(int index);

        /// <summary>
        /// Gets an export based on it's unreal based index in the export list.
        /// </summary>
        /// <param name="uIndex">unreal-based index in the export list</param>
        ExportEntry GetUExport(int uIndex);

        /// <summary>
        /// Gets an import based on it's unreal based index.
        /// </summary>
        /// <param name="uIndex">unreal-based index</param>
        ImportEntry GetImport(int uIndex);
        /// <summary>
        /// Try to get an ExportEntry by UIndex.
        /// </summary>
        /// <param name="uIndex"></param>
        /// <param name="export"></param>
        /// <returns></returns>
        bool TryGetUExport(int uIndex, out ExportEntry export);
        /// <summary>
        /// Try to get an ImportEntry by UIndex.
        /// </summary>
        /// <param name="uIndex"></param>
        /// <param name="import"></param>
        /// <returns></returns>
        bool TryGetImport(int uIndex, out ImportEntry import);
        /// <summary>
        /// Try to get an IEntry by UIndex.
        /// </summary>
        /// <param name="uIndex"></param>
        /// <param name="entry"></param>
        /// <returns></returns>
        bool TryGetEntry(int uIndex, out IEntry entry);

        int findName(string nameToFind);
        /// <summary>
        ///     gets Export or Import name, from unreal index
        /// </summary>
        /// <param name="index">unreal index</param>
        string getObjectName(int index);
        string GetNameEntry(int index);
        /// <summary>
        /// Gets the next available index for a name, checking for other objects incrementally with a same instanced full name until a free one is found that is higher than the original one
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        int GetNextIndexForInstancedName(IEntry entry);

        /// <summary>
        /// Gets an indexed name that is unique in the file
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        NameReference GetNextIndexedName(string name);
        //editing
        int FindNameOrAdd(string name);
        void replaceName(int index, string newName);
        void AddExport(ExportEntry exportEntry);
        void AddImport(ImportEntry importEntry);
        /// <summary>
        ///     exposed so that the property import function can restore the namelist after a failure.
        ///     please don't use it anywhere else.
        /// </summary>
        void restoreNames(List<string> list);

        /// <summary>
        /// Removes trashed imports and exports if they are at the end of their respective lists
        /// can only remove from the end because doing otherwise would mess up the indexing
        /// </summary>
        void RemoveTrailingTrash();

        byte[] getHeader();

        /// <summary>
        /// Collection of <see cref="IPackageUser"/>s that are using this <see cref="IMEPackage"/>. Use <see cref="RegisterTool"/> and <see cref="Release"/> to modify this collection.
        /// </summary>
        IReadOnlyCollection<IPackageUser> Users { get; }
        /// <summary>
        /// Collection of <see cref="IWeakPackageUser"/>s. This is for users that want to subscribe to package change notifications as long as the package is open,
        /// but don't want to cause it to stay open if there are no other users. Unlike <see cref="Users"/>, this collection should be modified directly,
        /// and since it only keeps weak references, users don't need to remove themselves before they fall out of scope.
        /// </summary>
        WeakCollection<IWeakPackageUser> WeakUsers { get; }
        void RegisterTool(IPackageUser user);
        void Release(IPackageUser user = null);
        /// <summary>
        /// Invoked when <see cref="Users"/> becomes empty.
        /// </summary>
        event UnrealPackageFile.MEPackageEventHandler NoLongerOpenInTools;
        void RegisterUse();
        /// <summary>
        /// Invoked when usages drop tp 0. Every call to <see cref="RegisterTool"/> or <see cref="RegisterUse"/> adds a usage,
        /// and every call to <see cref="Release"/> or <see cref="IDisposable.Dispose"/> subtracts a usage.
        /// </summary>
        event UnrealPackageFile.MEPackageEventHandler NoLongerUsed;
        MemoryStream SaveToStream(bool compress, bool includeAdditionalPackagesToCook = true, bool includeDependencyTable = true);
        List<ME1TalkFile> LocalTalkFiles { get; }
        public bool IsModified { get; internal set; }

        /// <summary>
        /// Compares this package against the one located on disk at the specified path
        /// </summary>
        /// <param name="packagePath"></param>
        /// <returns></returns>
        List<EntryStringPair> CompareToPackage(string packagePath);
        /// <summary>
        /// Compares this package against the specified other one
        /// </summary>
        /// <param name="compareFile"></param>
        /// <returns></returns>
        List<EntryStringPair> CompareToPackage(IMEPackage compareFile);
        /// <summary>
        /// Compares this package against the one in the specified stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        List<EntryStringPair> CompareToPackage(Stream stream);
        /// <summary>
        /// Looks for an export with the same instanced name
        /// </summary>
        /// <param name="instancedname"></param>
        /// <returns></returns>
        ExportEntry FindExport(string instancedname);
        /// <summary>
        /// Looks for an export with the same instanced name and classname
        /// </summary>
        /// <param name="instancedname"></param>
        /// <returns></returns>
        ExportEntry FindExport(string instancedname, string className);
        /// <summary>
        /// Looks for an import with the same instanced name.
        /// </summary>
        /// <param name="instancedname"></param>
        /// <returns></returns>
        ImportEntry FindImport(string instancedname);
        /// <summary>
        /// Looks for an entry with the same instanced path.
        /// </summary>
        /// <param name="instancedPath"></param>
        /// <returns></returns>
        /// <remarks>Can return the "wrong" one if multiple entries have the same path.
        /// Use <see cref="FindEntry(string, string)"/> to distinguish by class</remarks>
        IEntry FindEntry(string instancedPath);

        /// <summary>
        /// Looks for an entry with the same instanced path and class.
        /// </summary>
        /// <param name="instancedPath"></param>
        /// <param name="className"></param>
        /// <returns></returns>
        /// <remarks>Falls back to a linear search if it finds a match on path but not class</remarks>
        IEntry FindEntry(string instancedPath, string className);
        /// <summary>
        /// Invalidates the entry lookup table, causing it to be rebuilt next time FindEntry, FindExport, or FindImport is called.
        /// </summary>
        void InvalidateLookupTable();

        public EntryTree Tree { get; }

        /// <summary>
        /// If this package was opened from a non-disk source and doesn't have a true filepath (e.g. from SFAR - won't have single file it resided in on disk)
        /// </summary>
        bool IsMemoryPackage { get; set; }
    }
}