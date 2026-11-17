using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// The packages the editor makes in memory only, which nothing but it uses: a copied actor's level (<see cref="ActorCapture"/>) and a chosen asset's
/// package (<see cref="AssetPackage"/>). Each is named LEXLevelEditorTEMP and a number, in the folder of the file it was made from, and its imports are
/// found as that file's are: from the files beside it, and by its tag data (whether it's loaded after a save, and its import hints)
/// </summary>
public static class TemporaryPackages
{
    private static int lastNumber;

    //each one's name in messages and its package, by its path: no file the user has is at it
    private static readonly ConcurrentDictionary<string, (string Name, IMEPackage Package)> byPath = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A new package in <paramref name="importsAs"/>'s file's folder, whose imports are found as its are (<see cref="EntryImporter.GetPossibleImportFiles"/>),
    /// and which imports what it can (<see cref="EntryImporter.IsSafeToImportFrom(string, IMEPackage, bool)"/>): what a file loaded after a save imports
    /// from (the game's files, and those the user named) are its import hints, since its own name isn't a level's. Messages call it <paramref name="name"/>
    /// </summary>
    public static IMEPackage Create(IMEPackage importsAs, string name)
    {
        string folder = Path.GetDirectoryName(importsAs.FilePath) ?? "";
        string path;
        do path = Path.Combine(folder, $"LEXLevelEditorTEMP_{Interlocked.Increment(ref lastNumber)}.pcc");
        while (File.Exists(path));
        var package = MEPackageHandler.CreateMemoryEmptyPackage(path, importsAs.Game);
        byPath[path] = (name, package);
        try
        {
            var tags = package.LECLTagData;
            tags.ImportHintFiles.AddRange(importsAs.LECLTagData.ImportHintFiles);
            tags.IsPostLoadFile = importsAs.LECLTagData.IsPostLoadFile ?? EntryImporter.IsPostLoadFile(importsAs.FilePath, importsAs.Game);
            if (tags.IsPostLoadFile == true)
            {
                tags.ImportHintFiles.AddRange(EntryImporter.FilesSafeToImportFromPostLoad(importsAs.Game));
                tags.ImportHintFiles.AddRange(EntryImporter.UserSpecifiedSafeToImportFromFiles(importsAs.Game));
            }
            const UnrealFlags.EPackageFlags requireLoaded = UnrealFlags.EPackageFlags.RequireImportsAlreadyLoaded;
            package.setFlags(package.Flags & ~requireLoaded | importsAs.Flags & requireLoaded);
        }
        catch
        {
            Release(package);
            throw;
        }
        return package;
    }

    /// <summary>What the file at <paramref name="path"/> is called in messages: its name, or for a temporary package, what it was made from</summary>
    public static string FileName(string path) => path is not null && byPath.TryGetValue(path, out var made) ? made.Name : Path.GetFileName(path);

    /// <summary>The temporary package at <paramref name="path"/>, or null</summary>
    public static IMEPackage Find(string path) => path is not null && byPath.TryGetValue(path, out var made) ? made.Package : null;

    /// <summary>Forgets <paramref name="package"/>, one of these, and disposes it</summary>
    public static void Release(IMEPackage package)
    {
        byPath.TryRemove(package.FilePath, out _);
        package.Dispose();
    }
}
