using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BinaryPack.Attributes;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;

namespace LegendaryExplorer.Tools.ScriptDebugger
{
    public class ScriptDatabase
    {
        private readonly MEGame Game;
        private readonly Dictionary<string, List<(string filePath, int uIndex, bool forcedExport)>> FuncPathToExportDict = new();
        private readonly Dictionary<(string filePath, int uIndex), List<(string text, int position)>> ExportIdToTokensDict = new();

        private static readonly string LE3ScriptDatabasePath = Path.Combine(ScriptDebuggerWindow.ScriptDebuggerDataFolder, @"LE3ScriptDatabase.bin");
        private static readonly string LE2ScriptDatabasePath = Path.Combine(ScriptDebuggerWindow.ScriptDebuggerDataFolder, @"LE2ScriptDatabase.bin");
        private static readonly string LE1ScriptDatabasePath = Path.Combine(ScriptDebuggerWindow.ScriptDebuggerDataFolder, @"LE1ScriptDatabase.bin");
        private string DBPath => Game switch
        {
            MEGame.LE1 => LE1ScriptDatabasePath,
            MEGame.LE2 => LE2ScriptDatabasePath,
            MEGame.LE3 => LE3ScriptDatabasePath,
            _ => throw new ArgumentOutOfRangeException(nameof(Game))
        };

        //NEVER CHANGE!
        private const uint MAGIC = 0xDB651366;
        //Increment if changes are made to the DB format
        private const uint VERSION = 1;

        public ScriptDatabase(MEGame game)
        {
            Game = game;

            var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(Game, true).Values.ToHashSet(StringComparer.OrdinalIgnoreCase);

            TryLoadDatabase(loadedFiles, out Dictionary<string, DateTime> fileDates);

            //either the database couldn't be loaded, or some updates need to be made to it
            if (loadedFiles.Count > 0)
            {
                fileDates ??= new Dictionary<string, DateTime>();
                foreach (string filePath in loadedFiles.Where(path => path.EndsWith(".pcc", StringComparison.OrdinalIgnoreCase)))
                {
                    fileDates.Add(filePath, File.GetLastWriteTimeUtc(filePath));
                    //we don't need the export data, so to speed up scanning we won't read it.
                    using IMEPackage pcc = MEPackageHandler.UnsafePartialLoad(filePath, _ => false);

                    //this is faster than pcc.FindEntry("Core.Function"), since it avoids building the lookup table
                    IEntry functionClass = pcc.Imports.FirstOrDefault(imp => imp.ObjectNameString == "Function" && imp.ParentName == "Core");
                    if (functionClass is null)
                    {
                        continue;
                    }
                    int funcClassUindex = functionClass.UIndex;
                    foreach (ExportEntry exportEntry in pcc.Exports)
                    {
                        if (exportEntry.idxClass == funcClassUindex)
                        {
                            string funcPath = exportEntry.InstancedFullPath;
                            IEntry parent = exportEntry;
                            while (parent.Parent is not null)
                            {
                                parent = parent.Parent;
                            }
                            FuncPathToExportDict.AddToListAt(funcPath, (filePath, exportEntry.UIndex, parent is ExportEntry { IsClass: false }));
                        }
                    }
                }

                SaveDatabase(fileDates);
            }
        }

        private void TryLoadDatabase(HashSet<string> existingFiles, out Dictionary<string, DateTime> fileDates)
        {
            fileDates = null;
            if (!File.Exists(DBPath))
            {
                return;
            }

            List<ScriptDatabaseEntry> entryList;

            try
            {
                using var fs = new FileStream(DBPath, FileMode.Open);
                if (fs.ReadUInt32() != MAGIC
                    || fs.ReadUInt32() != VERSION)
                {
                    return;
                }
                var savedDB = BinaryPack.BinaryConverter.Deserialize<SavedScriptDatabase>(fs);
                entryList = savedDB.Entries;
                fileDates = savedDB.FileDates;
            }
            catch
            {
                //file is corrupted, delete it
                File.Delete(DBPath);
                return;
            }

            var filesToRemove = new HashSet<string>();
            foreach ((string filePath, DateTime lastModified) in fileDates)
            {
                if (existingFiles.Remove(filePath))
                {
                    if (File.GetLastWriteTimeUtc(filePath) != lastModified)
                    {
                        existingFiles.Add(filePath);
                        filesToRemove.Add(filePath);
                    }
                }
                else
                {
                    filesToRemove.Add(filePath);
                }
            }
            foreach (string filePath in filesToRemove)
            {
                fileDates.Remove(filePath);
            }

            foreach ((string functionPath, string filePath, int uIndex, bool isForcedExport) in entryList)
            {
                if (!filesToRemove.Contains(filePath))
                {
                    FuncPathToExportDict.AddToListAt(functionPath, (filePath, uIndex, isForcedExport));
                }
            }
        }

        private void SaveDatabase(Dictionary<string, DateTime> fileDates)
        {
            List<ScriptDatabaseEntry> entryList = FuncPathToExportDict.SelectMany(kvp => kvp.Value.Select(tup => new ScriptDatabaseEntry(kvp.Key, tup.filePath, tup.uIndex, tup.forcedExport))).ToList();
            Directory.CreateDirectory(Path.GetDirectoryName(DBPath)!);
            using var fs = new FileStream(DBPath, FileMode.Create);
            fs.WriteUInt32(MAGIC);
            fs.WriteUInt32(VERSION);

            var savedDB = new SavedScriptDatabase { Entries = entryList, FileDates = fileDates };
            BinaryPack.BinaryConverter.Serialize(savedDB, fs);
        }

        public IEnumerable<ScriptDatabaseEntry> GetEntries()
        {
            foreach ((string funcPath, var instances) in FuncPathToExportDict)
            {
                foreach ((string filePath, int uIndex, bool forcedExport) in instances)
                {
                    yield return new ScriptDatabaseEntry(funcPath, filePath, uIndex, forcedExport);
                }
            }
        }

        public (int uIndex, bool forcedExport) GetFunctionLocationFromPath(string funcPath, string filePath)
        {
            if (FuncPathToExportDict.TryGetValue(funcPath, out var possibleLocations))
            {
                foreach ((string possibleFilePath, int uIndex, bool forcedExport) in possibleLocations)
                {
                    if (possibleFilePath == filePath)
                    {
                        return (uIndex, forcedExport);
                    }
                }
            }
            return (0, false);
        }

        public List<ScriptStatement> GetStatements(string funcPath, string filePath)
        {
            (int uIndex, bool _) = GetFunctionLocationFromPath(funcPath, filePath);
            return uIndex == 0 ? null : GetStatements(filePath, uIndex);
        }
        
        public List<ScriptStatement> GetStatements(string filePath, int uIndex)
        {
            if (filePath is null)
            {
                return null;
            }
            if (ExportIdToTokensDict.TryGetValue((filePath, uIndex), out var tokens))
            {
                return tokens.Select(tuple => new ScriptStatement(tuple.text, tuple.position)).ToList();
            }
            using IMEPackage pcc = MEPackageHandler.UnsafePartialLoad(filePath, export => export.ClassName == "Function");
            IEntry functionClass = pcc.Imports.First(imp => imp.ObjectNameString == "Function" && imp.ParentName == "Core");
            int funcClassUindex = functionClass.UIndex;
            foreach (ExportEntry exportEntry in pcc.Exports)
            {
                if (exportEntry.idxClass == funcClassUindex)
                {
                    var function = new Function(exportEntry.Data, exportEntry);
                    function.ParseFunction();
                    ExportIdToTokensDict[(filePath, exportEntry.UIndex)] = function.ScriptBlocks.Select(token => (token.text, token.memPos)).ToList();
                }
            }
            if (ExportIdToTokensDict.TryGetValue((filePath, uIndex), out tokens))
            {
                return tokens.Select(tuple => new ScriptStatement(tuple.text, tuple.position)).ToList();
            }
            return null;
        }
    }

    public record ScriptDatabaseEntry(string FunctionPath, string FilePath, int UIndex, bool IsForcedExport)
    {
        //DO NOT USE THIS! It exists solely for the BinaryPack serializer
        public ScriptDatabaseEntry() : this(null, null, 0, false){}

        [IgnoredMember]
        public string FileName => Path.GetFileNameWithoutExtension(FilePath);
        [IgnoredMember]
        public string FullFunctionPath => IsForcedExport ? FunctionPath : $"{FileName}.{FunctionPath}";
    }

    public class SavedScriptDatabase
    {
        public SavedScriptDatabase() {}

        public Dictionary<string, DateTime> FileDates { get; set; } = new();

        public List<ScriptDatabaseEntry> Entries { get; set; } = new();
    }
}
