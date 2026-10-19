using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using LegendaryExplorer.Lightmaps.Codec;
using LegendaryExplorer.Lightmaps.Game;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.Lightmaps.Lightmass;

public sealed class LightmassBakeSettings
{
    /// <summary>UDK's Binaries folder (SwarmAgent.exe, Win64\UnrealLightmass.exe, Win64\AgentInterface.dll).</summary>
    public string UdkBinaries;
    public LightmassQuality Quality = LightmassQuality.Preview;
    /// <summary>Applies <see cref="LightmassIni.ApplyBioWareOverrides"/> over UDK's BaseLightmass.ini.</summary>
    public bool BioWareLightmassSettings = true;
    public LightmassSceneOptions Scene = new();
    /// <summary>Where the mod TFC for streamed lightmap textures is written, and its name (Textures_DLC_MOD_..., for the game to find it).</summary>
    public string TfcDirectory;
    /// <summary>Original path of the cache copied into TfcDirectory for a preview.</summary>
    internal string CopiedCacheSource;
    public string TfcName = "Textures_DLC_MOD_LEXLightmaps";
    /// <summary>
    /// Atlases get new allocations (AtlasPacker) instead of keeping the original layout. Not offered in the Lightmass Baker: it trashes and
    /// replaces the atlases, which is for testing the rewriter's write path.
    /// </summary>
    public bool Repack;
}

public sealed class LightmassBakeResult
{
    public LightmassScene Scene;
    public Dictionary<Guid, LightmassMappingResult> Results = new();
    public List<string> Alerts = [];
    /// <summary>Mappings Lightmass failed, and mappings it delivered no results for. They keep their original lighting.</summary>
    public int FailedTasks, MissingMappings;
    /// <summary>Whether every mapping was baked.</summary>
    public bool Complete => FailedTasks == 0 && MissingMappings == 0;
    public TimeSpan ExportTime, LightmassTime, ApplyTime;
    public Dictionary<IMEPackage, LightmapRewriter.Summary> Rewrites = new();
    public Dictionary<IMEPackage, LightmassShadowMapWriter.Summary> ShadowMaps = new();
    public Dictionary<string, int> KeptOriginal = new();
    public string TfcPath;
    /// <summary>Where the rebaked files were saved (<see cref="LightmassBakeJob"/>).</summary>
    public List<string> SavedFiles = [];
}

/// <summary>
/// Bakes static lighting for a set of LE3 levels with UDK's Lightmass: builds the scene, writes the channels, runs a Swarm job with a
/// task per mapping (as FLightmassProcessor::Run does, Lightmass.cpp:3141), reads the results and rewrites the levels' lightmaps.
/// The packages are changed in memory; saving them (and shipping the TFC) is the caller's.
/// </summary>
public static class LightmassBake
{
    /// <summary>BioWare's LMT_3/4 colour against the directional look (LE3_Lightmaps.md §16.1, provisional).</summary>
    const float VectorBrightness = 1.16f;

    /// <summary>What the Swarm message handler tells the wait loop (written on the bridge's reader thread).</summary>
    sealed class JobState
    {
        public volatile bool Done, Failed;
        public volatile string FailureMessage, AgentError;
    }

    public static LightmassBakeResult Run(IReadOnlyList<IMEPackage> levels, LightmassBakeSettings settings, Action<string> log, CancellationToken cancel = default)
    {
        log ??= _ => { };
        var result = new LightmassBakeResult();
        string win64 = Path.Combine(settings.UdkBinaries, "Win64");
        string lightmassExe = Path.Combine(win64, "UnrealLightmass.exe");
        if (!File.Exists(lightmassExe)) throw new FileNotFoundException("UnrealLightmass.exe wasn't found in the UDK's Binaries\\Win64 folder", lightmassExe);
        var ini = DuplicatingIni.LoadIni(Path.Combine(settings.UdkBinaries, "..", "Engine", "Config", "BaseLightmass.ini"));
        if (settings.BioWareLightmassSettings) ini.ApplyBioWareOverrides();

        var timer = Stopwatch.StartNew();
        log("Gathering the scene...");
        const string materialSection = "DevOptions.StaticLightingMaterial";
        settings.Scene.EmissiveSampleSize = (int)ini.Float(materialSection, "EmissiveSampleSize");
        settings.Scene.DiffuseSampleSize = (int)ini.Float(materialSection, "DiffuseSampleSize");
        settings.Scene.TransmissionSampleSize = (int)ini.Float(materialSection, "TransmissionSampleSize");
        LightmassMaterialCapture capture = null;
        try
        {
            capture = new LightmassMaterialCapture();
        }
        catch (Exception e)
        {
            // No Direct3D 11 device: still a usable bake, only flatter
            log($"Materials can't be rendered ({e.Message}), so they all get a constant albedo.");
        }
        settings.Scene.MaterialCapture = capture;
        using var builder = new LightmassSceneBuilder(settings.Scene);
        LightmassScene scene;
        try
        {
            scene = result.Scene = builder.Build(levels, log);
        }
        finally
        {
            settings.Scene.MaterialCapture = null;
            capture?.Dispose();
        }
        foreach (var (what, n) in scene.Report.Where(r => r.Key.StartsWith("material:")))
            log($"{n} {(n == 1 ? "material" : "materials")} {what["material: ".Length..]}");
        cancel.ThrowIfCancellationRequested();
        var mappings = scene.Mappings.ToDictionary(i => i.Guid);
        if (mappings.Count == 0)
        {
            log("Nothing to bake: no lightmaps in these levels.");
            return result;
        }

        var sceneGuid = Guid.NewGuid();
        using var swarm = new SwarmClient(settings.UdkBinaries, cancel: cancel);
        var completed = new BlockingCollection<(Guid Task, bool Success)>();
        var job = new JobState();
        swarm.Message += m =>
        {
            switch (m.Type)
            {
                case "TASK_STATE" when m.Fields[1] is "TASK_STATE_COMPLETE_SUCCESS" or "TASK_STATE_COMPLETE_FAILURE" or "TASK_STATE_KILLED":
                    if (LightmassFormat.ParseUe3String(m.Fields[0]) is { } done) completed.Add((done, m.Fields[1] == "TASK_STATE_COMPLETE_SUCCESS"));
                    break;
                case "TASK_STATE" when m.Fields[1] is "TASK_STATE_REJECTED" or "TASK_STATE_INVALID":
                    if (LightmassFormat.ParseUe3String(m.Fields[0]) is { } rejected) completed.Add((rejected, false));
                    job.FailureMessage ??= $"a task was {m.Fields[1]["TASK_STATE_".Length..].ToLowerInvariant()}";
                    job.Failed = true;
                    break;
                case "JOB_STATE":
                    if (m.Fields[0] is "STATE_COMPLETE_FAILURE" or "STATE_KILLED" or "STATE_INVALID")
                    {
                        job.FailureMessage ??= string.IsNullOrEmpty(m.Fields.ElementAtOrDefault(1)) ? m.Fields[0] : m.Fields[1];
                        job.Failed = true;
                    }
                    if (m.Fields[0] is "STATE_COMPLETE_SUCCESS" or "STATE_COMPLETE_FAILURE" or "STATE_KILLED") job.Done = true;
                    if (!string.IsNullOrEmpty(m.Fields.ElementAtOrDefault(1))) log($"Swarm: job {m.Fields[0]}: {m.Fields[1]}");
                    break;
                case "ALERT":
                    lock (result.Alerts) result.Alerts.Add($"{m.Fields[0]}: {m.Fields.ElementAtOrDefault(3)}");
                    break;
                case "QUIT":
                    job.AgentError ??= "the agent quit";
                    break;
                case "ERROR":
                    // The bridge lost the agent (closed from its tray, or crashed): no more messages will come.
                    job.AgentError ??= string.Join(" ", m.Fields);
                    break;
            }
        };

        swarm.OpenJob(sceneGuid);
        log($"Exporting {scene.Instances.Count} mesh instances and {scene.Lights.Count} lights...");
        var exporter = new LightmassExporter(scene, ini, settings.Quality, swarm);
        exporter.Write(sceneGuid);
        log($"Exported ({exporter.MeshesWritten} new meshes, {exporter.MaterialsWritten} new materials and {exporter.TerrainsWritten} new terrains in Swarm's cache).");
        result.ExportTime = timer.Elapsed;
        cancel.ThrowIfCancellationRequested();

        // One task per mapping, biggest first (the agent hands them out in order).
        swarm.BeginJobSpecification(lightmassExe, $"{LightmassFormat.Ue3String(sceneGuid)} -trisperleaf 4", 0,
            [Path.Combine(win64, "AgentInterface.dll")], levels[0].FileNameNoExtension);
        foreach (var m in mappings.Values.OrderByDescending(m => m.Cost))
            swarm.AddTask(m.Guid, m.TaskType, m.Cost);
        swarm.EndJobSpecification();
        log($"Lightmass is baking {mappings.Count} mappings ({settings.Quality} quality)...");

        timer.Restart();
        var pending = mappings.Keys.ToHashSet();
        var failed = new HashSet<Guid>();
        var lastReport = Stopwatch.StartNew();
        try
        {
            while (pending.Count > 0)
            {
                cancel.ThrowIfCancellationRequested();
                if (!completed.TryTake(out var task, 250))
                {
                    // Only once the queue is drained, so every task the agent reported is counted
                    if (swarm.Failure is { } bridgeFailure) throw new InvalidOperationException(bridgeFailure);
                    if (job.AgentError is { } agentError) throw new InvalidOperationException($"Lost the Swarm agent ({agentError}), so the bake can't finish. Nothing was written.");
                    if (job.Done || job.Failed) break;
                    continue;
                }
                if (!pending.Remove(task.Task)) continue;
                if (!task.Success) { failed.Add(task.Task); continue; }
                ImportChannel(swarm, task.Task, mappings, result);
                if (lastReport.Elapsed.TotalSeconds >= 2 || pending.Count == 0)
                {
                    log($"Lightmass: {mappings.Count - pending.Count}/{mappings.Count} mappings done");
                    lastReport.Restart();
                }
            }
            // Mappings delivered in another task's channel are already in; any still pending are read now if their channel exists.
            if (!job.Failed)
                foreach (var guid in pending.ToList()) ImportChannel(swarm, guid, mappings, result);
        }
        finally
        {
            try { if (swarm.Failure == null) swarm.CloseJob(); } catch (InvalidOperationException e) { log($"Swarm: CloseJob failed: {e.Message}"); }
            DeleteJobFolder(swarm.JobFolder, log);
        }
        result.LightmassTime = timer.Elapsed;
        if (job.Failed) throw new InvalidOperationException($"Lightmass's job failed ({job.FailureMessage}). Nothing was written; see Lightmass's log in UDK's Logs folder.");
        // (A failed task's mapping can still arrive in another task's channel.)
        result.FailedTasks = failed.Count(g => !result.Results.ContainsKey(g));
        result.MissingMappings = mappings.Keys.Count(g => !result.Results.ContainsKey(g) && !failed.Contains(g));
        log($"Lightmass finished in {result.LightmassTime:hh\\:mm\\:ss}: {result.Results.Count} mappings baked" +
            $"{(result.FailedTasks > 0 ? $", {result.FailedTasks} failed" : "")}{(result.MissingMappings > 0 ? $", {result.MissingMappings} missing" : "")}.");
        if (result.Results.Count == 0) return result;
        cancel.ThrowIfCancellationRequested();

        timer.Restart();
        log("Writing lightmaps...");
        Apply(levels, scene, result, settings, log, cancel);
        result.ApplyTime = timer.Elapsed;
        log($"Done in {result.ApplyTime:mm\\:ss}.");
        return result;
    }

    /// <summary>
    /// The job's folder in Swarm's cache (its scene channel and results, often hundreds of MB), once the job is closed and the results read.
    /// The agent's own pruning (MaximumJobsToKeep) leaves these behind.
    /// </summary>
    static void DeleteJobFolder(string folder, Action<string> log)
    {
        if (folder == null || !Directory.Exists(folder)) return;
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log($"Swarm's job folder {folder} couldn't be deleted ({e.Message}).");
        }
    }

    /// <summary>A mapping's results channel. Lightmass can merge several mappings into one channel, named after one of them.</summary>
    static void ImportChannel(SwarmClient swarm, Guid task, Dictionary<Guid, LightmassMapping> mappings, LightmassBakeResult result)
    {
        if (result.Results.ContainsKey(task) || !mappings.TryGetValue(task, out var mapping)) return;
        string channel = mapping.IsTextureMapping
            ? LightmassFormat.ChannelName(task, LightmassFormat.TextureMappingVersion, LightmassFormat.TextureMappingExtension)
            : LightmassFormat.ChannelName(task, LightmassFormat.VertexMappingVersion, LightmassFormat.VertexMappingExtension);
        if (swarm.JobChannelPath(channel) is not { } path) return;
        List<LightmassMappingResult> read;
        try
        {
            read = LightmassResults.Read(path, mapping.IsTextureMapping);
        }
        catch (Exception e) when (e is IOException or InvalidDataException)
        {
            throw new InvalidDataException($"Lightmass's results in {path} couldn't be read: {e.Message}", e);
        }
        foreach (var r in read) result.Results[r.Guid] = r;
    }

    static void Apply(IReadOnlyList<IMEPackage> levels, LightmassScene scene, LightmassBakeResult result, LightmassBakeSettings settings, Action<string> log,
        CancellationToken cancel)
    {
        string tfcDirectory = settings.TfcDirectory ?? Path.GetDirectoryName(levels[0].FilePath);
        using var source = new LightmassBakeSource(scene, result.Results);
        var packages = scene.Mappings.Where(m => result.Results.ContainsKey(m.Guid)).SelectMany(m => m.Targets).Select(t => t.Owner.FileRef).Distinct().ToList();
        var tfc = new TfcWriter(tfcDirectory, settings.TfcName, append: true, sourcePackages: packages,
            copiedCacheSource: settings.CopiedCacheSource);
        result.TfcPath = tfc.FilePath;
        foreach (var package in packages)
        {
            cancel.ThrowIfCancellationRequested();
            var summary = new LightmapRewriter(package, tfc, ByteEncoding.Gamma22, source, VectorBrightness).RewriteAll(settings.Repack);
            result.Rewrites[package] = summary;
            log($"{package.FileNameNoExtension}: {LightmassBakeReport.LightmapLine(summary)}");
            var shadows = result.ShadowMaps[package] = new LightmassShadowMapWriter(package, tfc, scene, result.Results).Write(summary.AppliedLightmaps);
            log($"{package.FileNameNoExtension}: {LightmassBakeReport.ShadowLine(shadows)}");
        }
        tfc.PreserveUnwrittenTextures(packages);
        foreach (var (why, n) in source.Report) result.KeptOriginal[why] = n;
    }
}

/// <summary>A human-readable account of a bake, for the Lightmass Baker's log and the headless bake.</summary>
public static class LightmassBakeReport
{
    public static string ShadowLine(LightmassShadowMapWriter.Summary s) =>
        $"{s.TextureShadowMaps} texture shadow maps in {s.Atlases} atlases, {s.VertexShadowMaps} vertex shadow maps (replacing {s.Removed})";

    public static string LightmapLine(LightmapRewriter.Summary s) =>
        $"{s.Allocations} texture lightmaps in {s.TextureSets} atlases, {s.VertexLightmaps} vertex lightmaps" +
        (s.UnbakedSets > 0 ? $" ({s.UnbakedSets} atlases with nothing baked left as they were)" : "");

    public static string Summary(LightmassBakeResult result, bool preview = false)
    {
        int mappings = result.Scene?.Mappings.Count() ?? 0;
        if (mappings == 0) return "There was nothing to bake: none of the baked files have lightmaps that can be rebaked.";
        var lines = new List<string>
        {
            $"Lightmass baked {result.Results.Count} of {mappings} lightmaps in {result.LightmassTime:hh\\:mm\\:ss}" +
            (result.Complete ? "." : $" ({result.FailedTasks} failed, {result.MissingMappings} missing: they keep their original lighting).")
        };
        foreach (var (package, summary) in result.Rewrites)
        {
            lines.Add($"{package.FileNameNoExtension}: {LightmapLine(summary)}");
            if (result.ShadowMaps.TryGetValue(package, out var shadows)) lines.Add($"    {ShadowLine(shadows)}");
        }
        if (result.KeptOriginal.Count > 0)
            lines.Add($"Kept their original lighting: {string.Join(", ", result.KeptOriginal.Select(k => $"{k.Value} {k.Key}"))}.");
        if (result.Alerts.Count > 0)
        {
            lines.Add($"Lightmass reported {result.Alerts.Count} warnings:");
            foreach (var group in result.Alerts.GroupBy(a => a).OrderByDescending(g => g.Count()).Take(10))
                lines.Add($"  {group.Count()} x {group.Key}");
        }
        if (result.TfcPath != null) lines.Add(preview ? "Texture cache prepared for preview; it will be published when you save."
            : $"Streamed lightmap textures: {result.TfcPath} (ship it with the mod).");
        foreach (string saved in result.SavedFiles) lines.Add($"Saved {saved}");
        return string.Join("\n", lines);
    }
}
