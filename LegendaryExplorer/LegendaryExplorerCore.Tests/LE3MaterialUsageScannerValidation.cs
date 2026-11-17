using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

/// <summary>
/// Opt-in (LE3_USAGE_SCAN=1): runs <see cref="LE3MaterialUsageScanner"/> over the shipping LE3 packages and checks
/// every use it derives against the shipping base material's flags. A shipped material is always flagged for what
/// it is drawn with, so a use whose flag is clear points at a scanner rule that is too broad. Read-only.
/// LE3_USAGE_SCAN_GAME overrides the game root; LE3_USAGE_SCAN_FILTER limits packages by file-name substring;
/// LE3_USAGE_SCAN_REPORT sets the report path.
/// </summary>
[TestClass]
public class LE3MaterialUsageScannerValidation
{
    private const string DefaultGameRoot = @"S:\SteamLibrary\steamapps\common\Mass Effect Legendary Edition\Game\ME3";

    [TestMethod]
    public void ScannerUsesAreFlaggedOnShippingMaterials()
    {
        if (Environment.GetEnvironmentVariable("LE3_USAGE_SCAN") != "1") Assert.Inconclusive("Set LE3_USAGE_SCAN=1 to run.");
        string gameRoot = Environment.GetEnvironmentVariable("LE3_USAGE_SCAN_GAME") is { Length: > 0 } g ? g : DefaultGameRoot;
        string filter = Environment.GetEnvironmentVariable("LE3_USAGE_SCAN_FILTER");
        string reportPath = Environment.GetEnvironmentVariable("LE3_USAGE_SCAN_REPORT") is { Length: > 0 } r ? r
            : Path.Combine(Path.GetTempPath(), "le3_usage_scan.txt");

        GlobalTest.Init();
        LE3Directory.DefaultGamePath = gameRoot;
        var files = Directory.EnumerateFiles(Path.Combine(gameRoot, "BioGame"), "*.pcc", SearchOption.AllDirectories)
            .Where(f => string.IsNullOrEmpty(filter) || Path.GetFileName(f).Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();

        var total = Stopwatch.StartNew();
        // (usage, source class) -> [uses, flag set, flag missing, vetoed, unresolved]
        var counts = new ConcurrentDictionary<(LE3MaterialUsage, string), int[]>();
        var examples = new ConcurrentDictionary<(LE3MaterialUsage, string), string>();
        var failures = new ConcurrentBag<string>();
        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) },
            () => new PackageCache { CacheMaxSize = 40 },
            (file, _, cache) =>
            {
                try
                {
                    using var package = MEPackageHandler.UnsafePartialLoad(file, LE3MaterialUsageScanner.NeedsExport);
                    var uses = LE3MaterialUsageScanner.Collect(package, import =>
                    {
                        try { return EntryImporter.ResolveImport(import, cache); }
                        catch { return null; }
                    });
                    foreach (var use in uses)
                    {
                        string sourceClass = package.FindExport(use.Source)?.ClassName ?? "?";
                        if (use.BaseMaterial != null && use.Usage is LE3MaterialUsage.StaticLighting or LE3MaterialUsage.VectorLightMaps
                                or LE3MaterialUsage.SimpleLightMaps or LE3MaterialUsage.LightEnvironments)
                        {
                            var props = use.BaseMaterial.GetProperties();
                            sourceClass += $" [{use.Detail ?? "-"} {props.GetProp<EnumProperty>("LightingModel")?.Value.Name ?? "MLM_Phong"} " +
                                $"{props.GetProp<EnumProperty>("BlendMode")?.Value.Name ?? "BLEND_Opaque"}]";
                        }
                        var key = (use.Usage, sourceClass);
                        var c = counts.GetOrAdd(key, _ => new int[5]);
                        int slot;
                        if (use.BaseMaterial == null) slot = 4;
                        else
                        {
                            var report = LE3MaterialUsageAdvisor.Check(use.BaseMaterial.GetProperties(), [use.Usage], use.BaseMaterial.ClassName);
                            slot = report.Vetoed.Count > 0 ? 3 : report.MissingFlags.Count > 0 ? 2 : 1;
                            if (slot >= 2) examples.TryAdd(key, $"{use.ReferencePath} via {use.Source} @ {Path.GetFileName(file)}");
                        }
                        lock (c) { c[0]++; c[slot]++; }
                    }
                }
                catch (Exception e)
                {
                    failures.Add($"{Path.GetFileName(file)}: {e.GetType().Name} {e.Message.Split('\n')[0]}");
                }
                return cache;
            },
            cache => cache.Dispose());

        var sb = new StringBuilder();
        sb.AppendLine($"LE3 material usage scanner vs shipping flags. {files.Count} packages, {total.Elapsed.TotalMinutes:F1} min, {failures.Count} failures.");
        sb.AppendLine("usage / source class: uses | flag set | flag missing | vetoed | unresolved");
        foreach (var ((usage, sourceClass), c) in counts.OrderBy(k => k.Key.Item1).ThenByDescending(k => k.Value[0]))
        {
            sb.AppendLine($"  {usage} / {sourceClass}: {c[0]} | {c[1]} | {c[2]} | {c[3]} | {c[4]}");
            if (examples.TryGetValue((usage, sourceClass), out var example)) sb.AppendLine($"      e.g. {example}");
        }
        foreach (var failure in failures.Take(30)) sb.AppendLine("  failure: " + failure);
        File.WriteAllText(reportPath, sb.ToString());
        Console.WriteLine($"Report: {reportPath}");
        Assert.AreEqual(0, failures.Count, $"{failures.Count} packages failed; see {reportPath}");
    }
}
