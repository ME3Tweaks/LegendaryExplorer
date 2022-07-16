using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using Microsoft.Toolkit.HighPerformance;

namespace LegendaryExplorerCore.Packages.CloningImportingAndRelinking
{
    /// <summary>
    /// Database class for mapping objects from an export in one game to the same one in another game
    /// </summary>
    public class ObjectInstanceDB
    {
        private readonly MEGame Game;

        private readonly List<string> FilePaths;

        private readonly CaseInsensitiveDictionary<List<int>> ExportMap;

        private ObjectInstanceDB(MEGame game, List<string> filePaths, CaseInsensitiveDictionary<List<int>> exportMap)
        {
            Game = game;
            FilePaths = filePaths;
            ExportMap = exportMap;
        }
        
        private const uint MAGIC = 0x1552D027;
        private const uint CURRENT_VERSION = 1;

        public void Serialize(Stream outStream)
        {
            outStream.WriteUInt32(MAGIC);
            outStream.WriteUInt32(CURRENT_VERSION);
            
            outStream.WriteInt32(FilePaths.Count);
            foreach (string filePath in FilePaths)
            {
                outStream.WriteStringUtf8WithLength(filePath);
            }

            outStream.WriteInt32(ExportMap.Count);
            foreach ((string ifp, List<int> files) in ExportMap)
            {
                outStream.WriteStringUtf8WithLength(ifp);
                outStream.WriteInt32(files.Count);
                outStream.Write(files.AsSpan().AsBytes());
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
                    var exportMap = new CaseInsensitiveDictionary<List<int>>(exportMapCount);
                    for (int i = 0; i < exportMapCount; i++)
                    {
                        string key = inStream.ReadStringUtf8WithLength();
                        int filesCount = inStream.ReadInt32();
                        int[] files = new int[filesCount];
                        inStream.ReadToSpan(files.AsSpan().AsBytes());
                        exportMap.Add(key, new List<int>(files));
                    }
                    return new ObjectInstanceDB(game, filePaths, exportMap);
                }
                case var unsupportedVersion:
                    throw new Exception($"{unsupportedVersion} is not a supported {nameof(ObjectInstanceDB)} version!");
            }
        }


        /// <summary>
        /// Returns a list of relative package file paths (to the game root) that contain the specified ExportEntry with the same InstancedFullPath. Returns null if the specified IFP is not in the database, indicating an object of that name does not exist in the game.
        /// </summary>
        /// <param name="ifp">Instanced full path of the object to find</param>
        /// <param name="localization">The localization for the InstancedFullPath object. This will force results to have a specific localization. If none is provided, all results are considered valid.</param>
        /// <returns></returns>
        public List<string> GetFilesContainingObject(string ifp, MELocalization localization = MELocalization.None)
        {
            if (ExportMap.TryGetValue(ifp, out List<int> files))
            {
                return files.Select(x => FilePaths[x]).Where(x => localization == MELocalization.None || x.GetUnrealLocalization() == localization).ToList();
            }

            return null;
        }

        public static ObjectInstanceDB Create(MEGame game, List<string> files, Action<int> progressReporter = null)
        {
            var objectDB = new ObjectInstanceDB(game, new List<string>(files.Count), new CaseInsensitiveDictionary<List<int>>());
            for (int i = 0; i < files.Count; i++)
            {
                objectDB.AddFileToDB(files[i], false);

                progressReporter?.Invoke(i);
            }
            return objectDB;
        }

        /// <summary>
        /// Adds objects in file to db 
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="insertAtStart">If true, add new instances of objects to the start of a list, so they will be preferred.</param>
        //keep public; is used by external tools
        public void AddFileToDB(string filePath, bool insertAtStart = true)
        {
            int filePathIndex = FilePaths.Count;

            // Load tables only to increase performance.
            using IMEPackage package = MEPackageHandler.UnsafePartialLoad(filePath, _ => false);

            if (package.Game != Game)
            {
                throw new InvalidOperationException($"Cannot add a {package.Game} file to a {Game} database");
            }

            string defaultGamePath = MEDirectories.GetDefaultGamePath(Game);
            if (package.FilePath.StartsWith(defaultGamePath))
            {
                // Store relative path
                FilePaths.Add(package.FilePath.Substring(defaultGamePath.Length + 1));
            }
            else
            {
                // Store full path
                FilePaths.Add(package.FilePath);
            }

            // Index objects
            foreach (ExportEntry exp in package.Exports)
            {
                string ifp = exp.InstancedFullPath;

                // Things to ignore
                if (ifp.StartsWith(@"TheWorld"))
                    continue;
                if (ifp.StartsWith(@"ObjectReferencer"))
                    continue;

                // Index it
                if (!ExportMap.TryGetValue(ifp, out List<int> records))
                {
                    records = new List<int>();
                    ExportMap.Add(ifp, records);
                }

                if (insertAtStart)
                {
                    records.Insert(0, filePathIndex);
                }
                else
                {
                    records.Add(filePathIndex);
                }
            }
        }
    }
}
