using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Newtonsoft.Json;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.Packages
{
    public static class MEGameExtensions
    {
        /// <summary>
        /// If game is part of legendary edition (not including UDK)
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static bool IsLEGame(this MEGame game) => game is MEGame.LE1 or MEGame.LE2 or MEGame.LE3;

        /// <summary>
        /// Is game part of original trilogy (not including UDK)
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static bool IsOTGame(this MEGame game) => game is MEGame.ME1 or MEGame.ME2 or MEGame.ME3;

        /// <summary>
        /// Is game OT ME or LE ME
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static bool IsGame1(this MEGame game) => game is MEGame.ME1 or MEGame.LE1;

        /// <summary>
        /// Is game OT ME2 or LE ME2
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static bool IsGame2(this MEGame game) => game is MEGame.ME2 or MEGame.LE2;

        /// <summary>
        /// Is game OT ME3 or LE ME3
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static bool IsGame3(this MEGame game) => game is MEGame.ME3 or MEGame.LE3;

        public static MEGame ToOTVersion(this MEGame game)
        {
            if (game == MEGame.LE1) return MEGame.ME1;
            if (game == MEGame.LE2) return MEGame.ME2;
            if (game == MEGame.LE3) return MEGame.ME3;
            return game;
        }

        public static MEGame ToLEVersion(this MEGame game)
        {
            if (game == MEGame.ME1) return MEGame.LE1;
            if (game == MEGame.ME2) return MEGame.LE2;
            if (game == MEGame.ME3) return MEGame.LE3;
            return game;
        }

        public static string CookedDirName(this MEGame game) => game switch
        {
            MEGame.ME1 => "CookedPC",
            MEGame.ME2 => "CookedPC",
            MEGame.UDK => throw new Exception($"{game} does not support CookedDirName()"),
            MEGame.LELauncher => throw new Exception($"{game} does not support CookedDirName()"),
            _ => "CookedPCConsole"
        };
    }

    public enum MEGame
    {
        Unknown = 0,
        ME1 = 1,
        ME2 = 2,
        ME3 = 3,
        LE1 = 4,
        LE2 = 5,
        LE3 = 6,
        UDK = 7,

        // Not an actual game, but used for identifying game directories
        LELauncher = 100, // Do not change this number. It's so we can add before this without messing up any existing items
    }

    public static class MELocalizationExtensions
    {
        // This should only be used for "LOC_" filenames, there are more options for TLK localizations
        public static string ToLocaleString(this MELocalization localization, MEGame game)
        {
            if (game.IsGame1())
            {
                return localization switch
                {
                    MELocalization.DEU => "DE",
                    MELocalization.ESN => "ES",
                    MELocalization.FRA => "FR",
                    MELocalization.ITA => "IT",
                    MELocalization.POL => "PLPC", // This does not correctly account for PL
                    MELocalization.RUS => "RA",
                    MELocalization.JPN => "JA",
                    MELocalization.None => "",
                    _ => localization.ToString()
                };
            }
            return localization.ToString();
        }
    }

    // This does not work for ME1/LE1 as it uses two character non-int localizations
    public enum MELocalization
    {
        None = 0,
        INT,
        DEU,
        ESN,
        FRA,
        ITA,
        JPN,
        POL,
        RUS
    }

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
            unchecked
            {
                int hashCode = (int)Type;
                hashCode = (hashCode * 397) ^ (Reference != null ? Reference.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Transient.GetHashCode();
                return hashCode;
            }
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

        public OrderedMultiValueDictionary<NameReference, PropertyInfo> properties = new();
        public string baseClass;
        //Relative to BIOGame
        public string pccPath;

        public int exportIndex;
        public bool isAbstract;

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
        ObservableCollection<IPackageUser> Users { get; }
        List<IPackageUser> WeakUsers { get; }
        void RegisterTool(IPackageUser user);
        void Release(IPackageUser user = null);
        event UnrealPackageFile.MEPackageEventHandler noLongerOpenInTools;
        void RegisterUse();
        event UnrealPackageFile.MEPackageEventHandler noLongerUsed;
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
        /// Looks for an export with the same instanced name.
        /// </summary>
        /// <param name="instancedname"></param>
        /// <returns></returns>
        ExportEntry FindExport(string instancedname);
        /// <summary>
        /// Looks for an import with the same instanced name.
        /// </summary>
        /// <param name="instancedname"></param>
        /// <returns></returns>
        ImportEntry FindImport(string instancedname);
        /// <summary>
        /// Looks for an entry (imports first, then exports) with the same instanced name.
        /// </summary>
        /// <param name="instancedname"></param>
        /// <returns></returns>
        IEntry FindEntry(string instancedname);
        /// <summary>
        /// Invalidates the entry lookup table, causing it to be rebuilt next time FindEntry, FindExport, or FindImport is called.
        /// </summary>
        void InvalidateLookupTable();

        public EntryTree Tree { get; }
    }
}