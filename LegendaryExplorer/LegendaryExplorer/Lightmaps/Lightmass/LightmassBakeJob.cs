using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using LegendaryExplorerCore;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

namespace LegendaryExplorer.Lightmaps.Lightmass;

public sealed class LightmassBakeJobFile
{
    public string Path { get; set; }

    /// <summary>The file's lightmaps are baked and it's saved. Otherwise it only shadows and bounces light.</summary>
    public bool Editable { get; set; } = true;
}

/// <summary>
/// A lighting build: the level files that make up the scene, which of them get new lightmaps, and where the results go.
/// The Lightmass Baker tool edits and runs these, and <c>LegendaryExplorer.exe --bake-lighting job.json</c> runs one headlessly.
/// </summary>
public sealed class LightmassBakeJob
{
    public List<LightmassBakeJobFile> Files { get; set; } = [];

    [JsonConverter(typeof(StringEnumConverter))]
    public LightAssignmentPolicy AssignmentPolicy { get; set; } = LightAssignmentPolicy.RecomputeAssignments;

    [JsonConverter(typeof(StringEnumConverter))]
    public LightmassQuality Quality { get; set; } = LightmassQuality.Preview;

    // Only deserialization starts in compatibility mode. Explicit JSON values are applied afterwards.
    [OnDeserializing]
    private void ReadLegacyDefaults(StreamingContext context) => AssignmentPolicy = LightAssignmentPolicy.PreserveExistingAssignments;

    /// <summary>Uses the Lightmass settings BioWare changed for ME3 (<see cref="LightmassIni.ApplyBioWareOverrides"/>) instead of UDK's stock ones.</summary>
    public bool BioWareLightmassSettings { get; set; } = true;

    /// <summary>Name of the texture file cache the streamed lightmap textures go in. An existing cache of that name is added to.</summary>
    public string TfcName { get; set; } = "Textures_DLC_MOD_LEXLightmaps";

    /// <summary>Folder the rebaked files are saved to. When empty, they're saved over the originals.</summary>
    public string OutputDirectory { get; set; }

    /// <summary>UDK's Binaries folder. By default the one under the UDK path in LEX's settings.</summary>
    public string UdkBinaries { get; set; }

    /// <summary>Reads a job file. Relative paths in it are relative to the job file's folder, so a job can travel with its levels.</summary>
    public static LightmassBakeJob Load(string path)
    {
        // A file with no job in it is read like one with no settings: an old job, which preserves assignments (see ReadLegacyDefaults)
        var job = JsonConvert.DeserializeObject<LightmassBakeJob>(File.ReadAllText(path))
                  ?? new LightmassBakeJob { AssignmentPolicy = LightAssignmentPolicy.PreserveExistingAssignments };
        string folder = Path.GetDirectoryName(Path.GetFullPath(path));
        string Resolve(string p) => string.IsNullOrWhiteSpace(p) ? p : Path.GetFullPath(p, folder);
        job.Files ??= [];
        foreach (var file in job.Files) file.Path = Resolve(file.Path);
        job.OutputDirectory = Resolve(job.OutputDirectory);
        job.UdkBinaries = Resolve(job.UdkBinaries);
        return job;
    }

    public void Save(string path) => File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));

    /// <summary>
    /// The Binaries folder of the UDK in LEX's settings. Read from the settings rather than <see cref="UDKDirectory.DefaultGamePath"/>, which
    /// falls back to the first UDK in the registry (often not UDK-2015-02) if it's first used before the settings are loaded.
    /// </summary>
    public static string DefaultUdkBinaries =>
        (string.IsNullOrWhiteSpace(LegendaryExplorerCoreLibSettings.Instance?.UDKCustomDirectory) ? UDKDirectory.DefaultGamePath : LegendaryExplorerCoreLibSettings.Instance.UDKCustomDirectory)
        is { Length: > 0 } udk ? Path.Combine(udk, "Binaries") : null;

    public string ResolvedUdkBinaries => string.IsNullOrWhiteSpace(UdkBinaries) ? DefaultUdkBinaries : UdkBinaries;

    /// <summary>The cache always goes beside the saved files. In-place bakes require one shared folder.</summary>
    public string ResolvedTfcDirectory => !string.IsNullOrWhiteSpace(OutputDirectory) ? OutputDirectory
        : Files.FirstOrDefault(f => f.Editable) is { } first ? Path.GetDirectoryName(Path.GetFullPath(first.Path)) : null;

    public const string RequiredSwarmVersion = "1.7.6517";

    /// <summary>What stops this job from running, if anything.</summary>
    public List<string> Problems()
    {
        var problems = new List<string>();
        if (!Enum.IsDefined(AssignmentPolicy)) problems.Add("Unknown light assignment policy.");
        if (ResolvedUdkBinaries is not { } udk)
            problems.Add($"Choose UDK's Binaries folder: SwarmAgent.exe version {RequiredSwarmVersion} is required (February 2015 UDK).");
        else
        {
            if (!File.Exists(Path.Combine(udk, "Win64", "UnrealLightmass.exe")))
                problems.Add($"UnrealLightmass.exe wasn't found in {Path.Combine(udk, "Win64")}.");
            string swarmPath = Path.Combine(udk, "SwarmAgent.exe");
            if (!File.Exists(swarmPath))
                problems.Add($"SwarmAgent.exe wasn't found in {udk}. Version {RequiredSwarmVersion} is required (February 2015 UDK).");
            else
            {
                try
                {
                    var version = FileVersionInfo.GetVersionInfo(swarmPath);
                    if (version.FileMajorPart != 1 || version.FileMinorPart != 7 || version.FileBuildPart != 6517)
                        problems.Add($"SwarmAgent.exe version {version.FileVersion ?? "unknown"} is unsupported. Version {RequiredSwarmVersion} is required (February 2015 UDK).");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
                {
                    problems.Add($"Couldn't read the SwarmAgent.exe version: {ex.Message}");
                }
            }
        }
        if (Files.Count == 0)
            problems.Add("Add the level files that make up the scene.");
        else if (!Files.Any(f => f.Editable))
            problems.Add("Mark at least one file as baked: the others only cast shadows.");
        foreach (var f in Files.Where(f => !File.Exists(f.Path)))
            problems.Add($"{f.Path} doesn't exist.");
        if (string.IsNullOrWhiteSpace(OutputDirectory) && Files.Where(f => f.Editable && File.Exists(f.Path))
                .Select(f => Path.GetDirectoryName(Path.GetFullPath(f.Path))).Distinct(StringComparer.OrdinalIgnoreCase).Skip(1).Any())
            problems.Add("To save over the original files, all files marked Bake must be in the same folder so the texture cache can be saved beside them. Choose an output folder or adjust which files are marked Bake.");
        var duplicates = Files.GroupBy(f => System.IO.Path.GetFileName(f.Path), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            problems.Add($"More than one file is named {string.Join(", ", duplicates)}: the game loads only one of each.");
        if (string.IsNullOrWhiteSpace(TfcName) || TfcName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            problems.Add("Enter a valid texture cache name.");
        if (!string.IsNullOrWhiteSpace(OutputDirectory) && !Directory.Exists(OutputDirectory))
            problems.Add($"The output folder {OutputDirectory} doesn't exist.");
        if (ResolvedTfcDirectory is { } tfc && !Directory.Exists(tfc))
            problems.Add($"The texture cache folder {tfc} doesn't exist.");
        return problems;
    }

    /// <summary>Advice that doesn't stop the job.</summary>
    public List<string> Warnings()
    {
        var warnings = new List<string>();
        if (!string.IsNullOrWhiteSpace(TfcName) && !TfcName.StartsWith("Textures_DLC_MOD_", StringComparison.OrdinalIgnoreCase))
            warnings.Add("Texture caches for DLC mods are usually named Textures_DLC_MOD_<mod name>.");
        string[] cookedFolders = [LE1Directory.CookedPCPath, LE2Directory.CookedPCPath, LE3Directory.CookedPCPath];
        bool IsGameFolder(string dir) => dir != null && cookedFolders.Any(cooked => cooked != null
            && Path.GetFullPath(dir).TrimEnd('\\').Equals(Path.GetFullPath(cooked).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        if (IsGameFolder(ResolvedTfcDirectory))
            warnings.Add("The texture cache would be written into the game's own CookedPCConsole folder.");
        if (string.IsNullOrWhiteSpace(OutputDirectory) && Files.Any(f => f.Editable && IsGameFolder(Path.GetDirectoryName(f.Path))))
            warnings.Add("Baked files would be saved over the game's own files. Choose an output folder, or bake copies in your mod.");
        var inUse = Files.Where(f => f.Editable && string.IsNullOrWhiteSpace(OutputDirectory) && MEPackageHandler.IsPackageInUse(f.Path)).Select(f => Path.GetFileName(f.Path)).ToList();
        if (inUse.Count > 0)
            warnings.Add($"{string.Join(", ", inUse)} {(inUse.Count == 1 ? "is" : "are")} open in another tool, which won't see the new lightmaps (and could save over them). Unsaved changes there aren't baked.");
        return warnings;
    }

    /// <summary>
    /// The levels a persistent level streams in (its LevelStreaming objects' packages), which together make up the scene.
    /// Each is looked for next to the persistent level first, then among the files the game loads. Missing ones are skipped.
    /// </summary>
    public static List<string> StreamingLevels(string persistentPath)
    {
        using var package = MEPackageHandler.OpenMEPackage(persistentPath, forceLoadFromDisk: true);
        string folder = Path.GetDirectoryName(persistentPath);
        var loaded = MELoadedFiles.GetFilesLoadedInGame(package.Game);
        var result = new List<string>();
        foreach (var export in package.Exports.Where(e => e.ClassName.StartsWith("LevelStreaming")))
        {
            if (export.GetProperty<NameProperty>("PackageName")?.Value.Instanced is not { Length: > 0 } name) continue;
            string fileName = name + ".pcc";
            string local = Path.Combine(folder, fileName);
            string path = File.Exists(local) ? local : loaded.TryGetValue(fileName, out var p) ? p : null;
            if (path != null && !result.Contains(path, StringComparer.OrdinalIgnoreCase)) result.Add(path);
        }
        return result;
    }

    /// <summary>
    /// Opens the files (fresh from disk, apart from any other tool's copy), bakes, and saves the editable files that changed.
    /// </summary>
    /// <exception cref="InvalidOperationException">The job can't run (<see cref="Problems"/>), or the bake failed</exception>
    /// <exception cref="IOException">A file couldn't be saved, or another bake is writing the same texture cache</exception>
    public LightmassBakeResult Run(Action<string> log, CancellationToken cancel = default)
    {
        using var preview = Prepare(log, cancel);
        cancel.ThrowIfCancellationRequested();
        preview.Save(log);
        return preview.Result;
    }

    /// <summary>Bakes into private packages and a temporary texture cache. Dispose to discard, or call Save to publish the result.</summary>
    public LightmassBakePreview Prepare(Action<string> log, CancellationToken cancel = default)
    {
        log ??= _ => { };
        if (Problems() is { Count: > 0 } problems) throw new InvalidOperationException(string.Join("\n", problems));
        var preview = new LightmassBakePreview(this, LockTfc());

        log($"Lightmass and Swarm from {ResolvedUdkBinaries}");
        var ordered = Files.ToList();
        var packages = preview.Packages;
        try
        {
            foreach (var file in ordered)
            {
                cancel.ThrowIfCancellationRequested();
                log($"Opening {Path.GetFileName(file.Path)}...");
                var package = MEPackageHandler.OpenMEPackage(file.Path, forceLoadFromDisk: true);
                packages.Add(package);
                ValidateGame(package.Game, packages[0].Game);
            }
            var contextOnly = packages.Where((p, i) => !ordered[i].Editable).ToHashSet();
            // Follow actual streaming references: LE1's persistent worlds are not named BioP_.
            var world = FindWorldPackage(packages);
            packages.Remove(world);
            packages.Insert(0, world);
            log($"World settings: {world.FileNameNoExtension} ({world.Game})");
            var settings = new LightmassBakeSettings
            {
                UdkBinaries = ResolvedUdkBinaries,
                Quality = Quality,
                BioWareLightmassSettings = BioWareLightmassSettings,
                TfcName = TfcName,
                TfcDirectory = preview.TemporaryDirectory,
                CopiedCacheSource = preview.CopiedCacheSource,
                Scene = new LightmassSceneOptions { ContextOnly = contextOnly, AssignmentPolicy = AssignmentPolicy },
            };
            preview.Result = LightmassBake.Run(packages, settings, log, cancel);
            cancel.ThrowIfCancellationRequested();
            return preview;
        }
        catch
        {
            preview.Dispose();
            throw;
        }
    }

    public static void ValidateGame(MEGame game, MEGame sceneGame)
    {
        if (game is not (MEGame.LE1 or MEGame.LE2 or MEGame.LE3))
            throw new NotSupportedException($"{game} levels cannot be baked; choose LE1, LE2 or LE3 levels.");
        if (game != sceneGame) throw new NotSupportedException($"A bake cannot mix {sceneGame} and {game} packages.");
    }

    /// <summary>Prefer the world which streams the supplied levels; retain input order for standalone scenes.</summary>
    public static IMEPackage FindWorldPackage(IReadOnlyList<IMEPackage> packages)
    {
        var names = packages.Select(p => p.FileNameNoExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return packages.OrderByDescending(p => p.Exports.Where(e => e.ClassName.StartsWith("LevelStreaming", StringComparison.Ordinal))
            .Count(e => e.GetProperty<NameProperty>("PackageName") is { } name && names.Contains(name.Value.Instanced)))
            .ThenByDescending(p => p.Exports.Count(e => e.ClassName.StartsWith("LevelStreaming", StringComparison.Ordinal)))
            .First();
    }

    /// <summary>
    /// Held for the whole bake, so that a second bake writing the same texture cache (another window, or a headless bake) is refused at
    /// the start instead of failing once its own Lightmass run is over.
    /// </summary>
    FileStream LockTfc()
    {
        string lockPath = Path.Combine(ResolvedTfcDirectory, TfcName + ".tfc.lexbake-lock");
        try
        {
            return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (IOException e)
        {
            throw new IOException($"Another bake is writing {TfcName}.tfc in {ResolvedTfcDirectory}. Wait for it to finish, or use another texture cache name.", e);
        }
    }

    /// <summary>
    /// Saves through a temporary file that then replaces the target, so a failure throws (IMEPackage.Save only reports it to a callback)
    /// and leaves the existing file whole.
    /// </summary>
    internal static void Save(IMEPackage package, string path)
    {
        if (package is not MEPackage mePackage) throw new NotSupportedException($"{package.FileNameNoExtension} isn't a Mass Effect package");
        string temporary = path + ".lexbake-tmp";
        try
        {
            using (var saved = mePackage.SaveToStream(compress: true))
                saved.WriteToFile(temporary);
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"{path} couldn't be saved: {e.Message}", e);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
