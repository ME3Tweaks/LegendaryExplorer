using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Packages.CloningImportingAndRelinking
{
    /// <summary>
    /// Database class for mapping objects from an export in one game to the same one in another game
    /// </summary>
    public class ObjectInstanceDB
    {
        /// <summary>
        /// Game this DB is for
        /// </summary>
        private readonly MEGame Game;

        /// <summary>
        /// List of FilePaths
        /// </summary>
        private readonly List<string> FilePaths;

        /// <summary>
        /// The version of this ObjectInstanceDB
        /// </summary>
        public int Version { get; init; }

        /// <summary>
        /// List of distinct class names. Objects reference these by index rather than storing the
        /// name, since a handful of class names cover hundreds of thousands of objects.
        /// </summary>
        private readonly List<string> ClassNames;

        /// <summary>
        /// Reverse of <see cref="ClassNames"/>, used only while building.
        /// </summary>
        private readonly CaseInsensitiveDictionary<int> ClassNameIndexes = new();

        /// <summary>
        /// Map of Instanced Full Paths to the files that contain them (indexes into FilePaths)
        /// </summary>
        private readonly CaseInsensitiveDictionary<ObjectInstanceInfo> ExportMap;

        private ObjectInstanceDB(MEGame game, List<string> filePaths, CaseInsensitiveDictionary<ObjectInstanceInfo> exportMap, List<string> classNames = null)
        {
            Game = game;
            FilePaths = filePaths;
            ExportMap = exportMap;
            ClassNames = classNames ?? new List<string>();
            for (int i = 0; i < ClassNames.Count; i++)
            {
                ClassNameIndexes[ClassNames[i]] = i;
            }
        }

        /// <summary>
        /// Index of a class name, adding it to the table if new.
        /// </summary>
        private int IndexForClass(string className)
        {
            if (string.IsNullOrEmpty(className)) return -1;
            if (ClassNameIndexes.TryGetValue(className, out int existing)) return existing;
            int index = ClassNames.Count;
            ClassNames.Add(className);
            ClassNameIndexes[className] = index;
            return index;
        }

        /// <summary>
        /// Class name of the given object, or null when this database predates class indexing
        /// (version 2 and below) or does not contain the object. Callers must treat null as
        /// "unknown" rather than "no match".
        /// </summary>
        public string GetClassName(string ifp)
        {
            if (!ExportMap.TryGetValue(ifp, out ObjectInstanceInfo info)) return null;
            if (info.ClassIndex < 0 || info.ClassIndex >= ClassNames.Count) return null;
            return ClassNames[info.ClassIndex];
        }

        /// <summary>
        /// True when this database carries class information; false for databases serialized before
        /// version 3, which need regenerating to gain it.
        /// </summary>
        public bool HasClassNames => ClassNames.Count > 0;

        private const uint MAGIC = 0x1552D027;

        // V2 05/14/2024 - Add NetIndex
        // V3 08/23/2026 - Add class name table; objects reference it by index
        private const uint CURRENT_VERSION = 3;

        public void Serialize(Stream outStream)
        {
            outStream.WriteUInt32(MAGIC);
            outStream.WriteUInt32(CURRENT_VERSION);

            outStream.WriteInt32(FilePaths.Count);
            foreach (string filePath in FilePaths)
            {
                outStream.WriteStringUtf8WithLength(filePath);
            }

            outStream.WriteInt32(ClassNames.Count); // V3
            foreach (string className in ClassNames)
            {
                outStream.WriteStringUtf8WithLength(className);
            }

            outStream.WriteInt32(ExportMap.Count);
            foreach ((string ifp, ObjectInstanceInfo oii) in ExportMap)
            {
                outStream.WriteStringUtf8WithLength(ifp);
                outStream.WriteInt32(oii.NetIndex); // V2
                outStream.WriteInt32(oii.GenerationNetObjCount); // V2
                outStream.WriteInt32(oii.ClassIndex); // V3
                outStream.WriteInt32(oii.Files.Count);
                outStream.Write(oii.Files.AsSpan().AsBytes());
            }
        }

        public static ObjectInstanceDB Deserialize(MEGame game, Stream inStream)
        {
            if (inStream.ReadUInt32() != MAGIC)
            {
                throw new Exception($"This is not a serialized {nameof(ObjectInstanceDB)}!");
            }
            switch (inStream.ReadUInt32())
            {
                case 1:
                    {
                        int filePathsCount = inStream.ReadInt32();
                        var filePaths = new List<string>(filePathsCount);
                        for (int i = 0; i < filePathsCount; i++)
                        {
                            filePaths.Add(inStream.ReadStringUtf8WithLength());
                        }

                        int exportMapCount = inStream.ReadInt32();
                        var exportMap = new CaseInsensitiveDictionary<ObjectInstanceInfo>(exportMapCount);
                        for (int i = 0; i < exportMapCount; i++)
                        {
                            string key = inStream.ReadStringUtf8WithLength();
                            int filesCount = inStream.ReadInt32();
                            var files = new List<int>(filesCount);
                            CollectionsMarshal.SetCount(files, filesCount);
                            inStream.ReadToSpan(files.AsSpan().AsBytes());
                            exportMap.Add(key, new ObjectInstanceInfo(files)); // V1 uses NetIndex 0 for everything.
                        }
                        return new ObjectInstanceDB(game, filePaths, exportMap) { Version = 1 };
                    }
                case 2:
                    {
                        int filePathsCount = inStream.ReadInt32();
                        var filePaths = new List<string>(filePathsCount);
                        for (int i = 0; i < filePathsCount; i++)
                        {
                            filePaths.Add(inStream.ReadStringUtf8WithLength());
                        }

                        int exportMapCount = inStream.ReadInt32();
                        var exportMap = new CaseInsensitiveDictionary<ObjectInstanceInfo>(exportMapCount);
                        for (int i = 0; i < exportMapCount; i++)
                        {
                            string key = inStream.ReadStringUtf8WithLength();
                            int netIndex = inStream.ReadInt32();
                            int generationNetObjsCount = inStream.ReadInt32();

                            int filesCount = inStream.ReadInt32();
                            var files = new List<int>(filesCount);
                            CollectionsMarshal.SetCount(files, filesCount);
                            inStream.ReadToSpan(files.AsSpan().AsBytes());
                            exportMap.Add(key, new ObjectInstanceInfo(files)
                            {
                                NetIndex = netIndex,
                                GenerationNetObjCount = generationNetObjsCount
                            });
                        }
                        return new ObjectInstanceDB(game, filePaths, exportMap) { Version = 2 };
                    }
                case 3:
                    {
                        int filePathsCount = inStream.ReadInt32();
                        var filePaths = new List<string>(filePathsCount);
                        for (int i = 0; i < filePathsCount; i++)
                        {
                            filePaths.Add(inStream.ReadStringUtf8WithLength());
                        }

                        int classNamesCount = inStream.ReadInt32();
                        var classNames = new List<string>(classNamesCount);
                        for (int i = 0; i < classNamesCount; i++)
                        {
                            classNames.Add(inStream.ReadStringUtf8WithLength());
                        }

                        int exportMapCount = inStream.ReadInt32();
                        var exportMap = new CaseInsensitiveDictionary<ObjectInstanceInfo>(exportMapCount);
                        for (int i = 0; i < exportMapCount; i++)
                        {
                            string key = inStream.ReadStringUtf8WithLength();
                            int netIndex = inStream.ReadInt32();
                            int generationNetObjsCount = inStream.ReadInt32();
                            int classIndex = inStream.ReadInt32();

                            int filesCount = inStream.ReadInt32();
                            var files = new List<int>(filesCount);
                            CollectionsMarshal.SetCount(files, filesCount);
                            inStream.ReadToSpan(files.AsSpan().AsBytes());
                            exportMap.Add(key, new ObjectInstanceInfo(files)
                            {
                                NetIndex = netIndex,
                                GenerationNetObjCount = generationNetObjsCount,
                                ClassIndex = classIndex
                            });
                        }
                        return new ObjectInstanceDB(game, filePaths, exportMap, classNames) { Version = 3 };
                    }
                case var unsupportedVersion:
                    throw new Exception($"{unsupportedVersion} is not a supported {nameof(ObjectInstanceDB)} version!");
            }
        }

        public ObjectInstanceInfo GetInfo(string ifp)
        {
            if (ExportMap.TryGetValue(ifp, out ObjectInstanceInfo info))
            {
                return info;
            }

            return null;
        }

        /// <summary>
        /// Returns a list of relative package file paths (to the game root) that contain the specified ExportEntry with the same InstancedFullPath. Returns null if the specified IFP is not in the database, indicating an object of that name does not exist in the game.
        /// </summary>
        /// <param name="ifp">Instanced full path of the object to find</param>
        /// <param name="localization">The localization for the InstancedFullPath object. This will force results to have a specific localization. If none is provided, all results are considered valid.</param>
        /// <returns></returns>
        public List<string> GetFilesContainingObject(string ifp, MELocalization localization = MELocalization.None)
        {
            if (ExportMap.TryGetValue(ifp, out ObjectInstanceInfo info))
            {
                return info.Files.Select(x => FilePaths[x]).Where(x => localization == MELocalization.None || x.GetUnrealLocalization() == localization).ToList();
            }

            return null;
        }

        /// <summary>
        /// Creates an empty database to be filled with <see cref="AddFileToDB(IMEPackage,string,bool,Func{ExportEntry,bool},string)"/>.
        /// For callers that do their own file enumeration -- e.g. one that inventories a package for
        /// several databases at once and does not want <see cref="Create"/> to open every file again.
        /// </summary>
        public static ObjectInstanceDB CreateEmpty(MEGame game)
        {
            return new ObjectInstanceDB(game, [], new CaseInsensitiveDictionary<ObjectInstanceInfo>());
        }

        public static ObjectInstanceDB Create(MEGame game, List<string> files, Action<int> numDoneReporter = null, Action<int> addExtraFiles = null, string gamePathOverride = null)
        {
            var objectDB = new ObjectInstanceDB(game, new List<string>(files.Count), new CaseInsensitiveDictionary<ObjectInstanceInfo>());
            int numDone = 0;
            for (int i = 0; i < files.Count; i++)
            {
                numDone++;
                if (game == MEGame.ME3 && Path.GetFileName(files[i]) == "Default.sfar")
                {
                    // It's an SFAR package
                    DLCPackage dlc = new DLCPackage(files[i]);
                    addExtraFiles?.Invoke(dlc.Files.Count(x => x.isActualFile && x.FileName.RepresentsPackageFilePath()));
                    foreach (var f in dlc.Files)
                    {
                        if (f.isActualFile && f.FileName.RepresentsPackageFilePath())
                        {
                            var decompress = dlc.DecompressEntry(f);
                            using var dlcP = MEPackageHandler.OpenMEPackageFromStream(decompress, f.FileName);
                            objectDB.AddFileToDB(dlcP, f.FileName, true, null, gamePathOverride);
                            numDone++;
                            numDoneReporter?.Invoke(numDone);
                        }
                    }
                }
                else if (files[i].RepresentsPackageFilePath())
                {
                    objectDB.AddFileToDB(files[i], false, gamePathOverride);
                }
                else
                {
                    // This should not happen.
                    Debugger.Break();
                }

                numDoneReporter?.Invoke(numDone);
            }
            return objectDB;
        }

        /// <summary>
        /// Adds objects to the db from the given package.
        /// </summary>
        /// <param name="package">Package object to inventory</param>
        /// <param name="filePath">Path to package file on disk</param>
        /// <param name="insertAtStart">If true, add new instances of objects to the start of a list, so they will be preferred.</param>
        /// <param name="objectsToAddPredicate">Predicate to determine which objects to add to the database. If null, all objects will be added.</param>
        public void AddFileToDB(IMEPackage package, string filePath, bool insertAtStart = true, Func<ExportEntry, bool> objectsToAddPredicate = null, string gamePathOverride = null)
        {
            int filePathIndex = FilePaths.Count;
            if (package.Game != Game)
            {
                throw new InvalidOperationException($"Cannot add a {package.Game} file to a {Game} database");
            }

            // Relativize the path the CALLER gave, not the one the package happens to carry. A
            // package opened from an SFAR stream has no path of its own beyond the archive entry
            // name, and a caller doing its own enumeration may already know the game-relative path it
            // wants stored. Every in-tree caller passes package.FilePath here, so this is unchanged
            // for them.
            string pathToStore = filePath ?? package.FilePath;

            // An explicit root allows indexing a game that is only present as a backup; without
            // one GetDefaultGamePath returns null for an uninstalled game and this threw.
            string defaultGamePath = gamePathOverride ?? MEDirectories.GetDefaultGamePath(Game);
            if (!string.IsNullOrEmpty(defaultGamePath) && pathToStore != null
                && pathToStore.StartsWith(defaultGamePath, StringComparison.OrdinalIgnoreCase))
            {
                // Store relative path
                FilePaths.Add(pathToStore.Substring(defaultGamePath.Trim('\\', '/').Length + 1));
            }
            else
            {
                // Store full path, or whatever relative path the caller supplied
                FilePaths.Add(pathToStore);
            }

            // Index objects
            foreach (ExportEntry exp in package.Exports.Where(objectsToAddPredicate ?? AlwaysAdd))
            {
                string ifp = exp.InstancedFullPath;

                // Things to ignore
                if (ifp.StartsWith(@"TheWorld"))
                    continue;
                if (ifp.StartsWith(@"ObjectReferencer"))
                    continue;

                // Index it
                if (!ExportMap.TryGetValue(ifp, out ObjectInstanceInfo records))
                {
                    records = new ObjectInstanceInfo(new List<int>(), exp) { ClassIndex = IndexForClass(exp.ClassName) };
                    ExportMap.Add(ifp, records);
                }

                if (insertAtStart)
                {
                    records.Files.Insert(0, filePathIndex);
                }
                else
                {
                    records.Files.Add(filePathIndex);
                }
            }
        }

        private bool AlwaysAdd(ExportEntry entry)
        {
            return true;
        }

        /// <summary>
        /// Removes all entries associated with a specific package
        /// </summary>
        /// <param name="package"></param>
        public void RemoveFileFromDB(IMEPackage package)
        {
            var fileIndex = FilePaths.IndexOf(package.FilePath);
            if (fileIndex != -1)
            {
                foreach (ExportEntry exp in package.Exports)
                {
                    if (ExportMap.TryGetValue(exp.InstancedFullPath, out ObjectInstanceInfo records))
                    {
                        var fIndex = records.Files.IndexOf(fileIndex);
                        if (fIndex != -1)
                        {
                            records.Files.RemoveAt(fIndex);
                            if (records.Files.Count == 0)
                            {
                                // Removed from DB entirely
                                ExportMap.Remove(exp.InstancedFullPath);
                            }
                        }
                    }
                }
                // Removing it would offset indices for later added files. We have to store null instead.
                FilePaths[fileIndex] = null;
            }
        }

        /// <summary>
        /// Adds objects in file to db 
        /// </summary>
        /// <param name="filePath">Path to package file on disk</param>
        /// <param name="insertAtStart">If true, add new instances of objects to the start of a list, so they will be preferred.</param>
        //keep public; is used by external tools
        public void AddFileToDB(string filePath, bool insertAtStart = true, string gamePathOverride = null)
        {
            using IMEPackage package = MEPackageHandler.OpenMEPackage(filePath);
            AddFileToDB(package, filePath, insertAtStart, null, gamePathOverride);
        }

        /// <summary>
        /// Gets a list of all unique object paths in this database. This returns an enumerator.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetAllObjectPaths(bool alphabetical)
        {
            if (alphabetical)
            {
                return ExportMap.Keys.OrderBy(x => x);
            }
            return ExportMap.Keys;
        }

        /// <summary>
        /// Adds a single export object to the database
        /// </summary>
        /// <param name="export"></param>
        /// <param name="insertAtStart"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void AddObjectToDB(ExportEntry export, bool insertAtStart = false)
        {
            string ifp = export.InstancedFullPath;

            // These items will never be indexed
            if (ifp.StartsWith(@"ObjectReferencer"))
                return;

            int filePathIndex = FilePaths.IndexOf(export.FileRef.FilePath);
            if (export.Game != Game)
            {
                throw new InvalidOperationException($"Cannot add a {export.Game} object to a {Game} database");
            }

            string defaultGamePath = MEDirectories.GetDefaultGamePath(Game);
            if (!string.IsNullOrEmpty(defaultGamePath) && export.FileRef.FilePath != null
                && export.FileRef.FilePath.StartsWith(defaultGamePath, StringComparison.OrdinalIgnoreCase))
            {
                // Store relative path
                FilePaths.Add(export.FileRef.FilePath.Substring(defaultGamePath.Trim('\\', '/').Length + 1));
            }
            else
            {
                // Store full path
                FilePaths.Add(export.FileRef.FilePath);
            }

            // Index it
            if (!ExportMap.TryGetValue(ifp, out ObjectInstanceInfo records))
            {
                records = new ObjectInstanceInfo(new List<int>(), export) { ClassIndex = IndexForClass(export.ClassName) };
                ExportMap.Add(ifp, records);
            }

            if (insertAtStart)
            {
                records.Files.Insert(0, filePathIndex);
            }
            else
            {
                records.Files.Add(filePathIndex);
            }
        }
    }

    public class ObjectInstanceInfo
    {
        public List<int> Files;
        public int NetIndex;
        public int GenerationNetObjCount; // Set on Package only

        /// <summary>
        /// Index into the database's class name table, or -1 when unknown (databases serialized
        /// before version 3).
        /// </summary>
        public int ClassIndex = -1;

        /// <summary>
        /// For serialization
        /// </summary>
        /// <param name="files">List of file indices</param>
        /// <param name="export">Export to parse</param>
        public ObjectInstanceInfo(List<int> files, ExportEntry export)
        {
            Files = files;
            NetIndex = export.NetIndex;
            GenerationNetObjCount = export.GenerationNetObjectCount.Length == 1 ? export.GenerationNetObjectCount[0] : 0;
        }

        /// <summary>
        /// For deserialization
        /// </summary>
        /// <param name="files">List of file indices</param>
        public ObjectInstanceInfo(List<int> files)
        {
            Files = files;
        }
    }
}
