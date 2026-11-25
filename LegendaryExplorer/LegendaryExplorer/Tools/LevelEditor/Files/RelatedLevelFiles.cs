using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using System;
using System.IO;
using System.Linq;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// Finds the other files of a level: its persistent level, the levels it streams in, its Bio?_ siblings. Mods are often worked on in a folder
/// outside the game, so the folder of the file they belong with is looked in first, then the files the game loads
/// </summary>
public static class RelatedLevelFiles
{
    /// <summary>
    /// The path of <paramref name="fileName"/> in <paramref name="folder"/>, else the one the game loads. Null if neither has it.
    /// The path has the file's own casing, as package caches compare paths exactly
    /// </summary>
    public static string Find(string fileName, string folder, MEGame game)
    {
        if (folder is not null && File.Exists(Path.Combine(folder, fileName)))
        {
            return Path.GetFullPath(Directory.EnumerateFiles(folder, fileName).First(path => Path.GetFileName(path).Equals(fileName, StringComparison.OrdinalIgnoreCase)));
        }
        return MELoadedFiles.GetFilesLoadedInGame(game).TryGetValue(fileName, out string path) ? path : null;
    }

    /// <summary>The packages in <paramref name="folder"/> and the ones the game loads, by file name. The folder's copy of a file wins</summary>
    public static CaseInsensitiveDictionary<string> All(string folder, MEGame game)
    {
        var files = new CaseInsensitiveDictionary<string>();
        if (folder is not null && Directory.Exists(folder))
        {
            //(a three-letter pattern also matches longer extensions)
            foreach (string path in Directory.EnumerateFiles(folder, "*.pcc"))
            {
                if (path.EndsWith(".pcc", StringComparison.OrdinalIgnoreCase)) files[Path.GetFileName(path)] = Path.GetFullPath(path);
            }
        }
        foreach ((string fileName, string path) in MELoadedFiles.GetFilesLoadedInGame(game))
        {
            files.TryAdd(fileName, path);
        }
        return files;
    }
}
