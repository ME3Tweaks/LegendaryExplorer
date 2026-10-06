using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Tests;

/// <summary>
/// Opt-in offline parity harness: compares the LE3 offline material shader compiler with the shipping
/// RefShaderCache-PC-D3D-SM5.upk material shader maps. Skipped unless LE3_PARITY_DECOOKED is set.
/// Read-only: opens decooked packages and the shipping cache for reading, never saves or launches anything.
/// Env vars: LE3_PARITY_DECOOKED (folder of decooked LE3 .pcc), LE3_PARITY_MAX_PACKAGES, LE3_PARITY_MAX_MATERIALS,
/// LE3_PARITY_REPORT (report path), LE3_PARITY_GAME (…\Game\ME3 root), LE3_PARITY_THREADS (jobs per material),
/// LE3_PARITY_MAX_MINUTES (time budget, default 20), LE3_PARITY_DUMP_DIR (optional: dump a few differing pairs),
/// LE3_PARITY_MATERIALS (optional ';'-separated full object paths), LE3_PARITY_INSPECT_DIR (optional diagnostics),
/// LE3_PARITY_SKIP_PACKAGES (skip the first N packages).
/// </summary>
[TestClass]
public class LE3ShippingParityHarness
{
    private const string DefaultGameRoot = @"S:\SteamLibrary\steamapps\common\Mass Effect Legendary Edition\Game\ME3";
    private static readonly string InspectionDirectory = Environment.GetEnvironmentVariable("LE3_PARITY_INSPECT_DIR");

    private enum JobKind
    {
        CompileFailed,
        NotInShippingMap,
        ShippingBytecodeMissing,
        IdenticalBytes,
        IdenticalDisassembly,
        Different,
        DisassemblyError
    }

    private sealed class JobOutcome
    {
        public JobKind Kind;
        public string ShaderType, VertexFactory;
        public string Detail;           // normalized compile diagnostic, or first-diff pattern
        public int OursInstr, TheirsInstr;
        public bool SameOpcodeSequence;
        public string FirstDiffExample; // raw first differing line pair
        public string OursDisasm, TheirsDisasm;
    }

    private sealed class Group
    {
        public int Count;
        public string Example;
    }

    private sealed class Stats
    {
        public int Packages, PackagesFailedToOpen, Materials;
        public int NoShippingMap, NoExactKeyButBaseIdPresent, WithShippingMap;
        public int TranslateFailed, PlanFailed, CompilePartial, FullyCompiled;
        public int MembershipMatch, MembershipMismatch, PlanSubsetOfShipping, UniformMatch, UniformMismatch, UniformNotComparable;
        public int MaterialsAllJobsMatchBytes, MaterialsAllJobsMatchDisasm, MaterialsWithAnyDifferent;
        public int ResourceBinaryErrors, Exceptions;
        public long MissingPermutations, UnexpectedPermutations;
        public int JobsTotal;
        public readonly Dictionary<JobKind, int> JobKinds = new();
        public int InstrEqual, InstrOursLess, InstrOursMore, DiffSameOpcodes;
        public readonly Dictionary<string, Group> Failures = new(); // "phase: message"
        public readonly Dictionary<string, Group> DiffPatterns = new();
        public readonly Dictionary<string, Group> UniformDiffPatterns = new();
        public readonly Dictionary<string, int> MissingByType = new();
        public readonly Dictionary<string, int> UnexpectedByType = new();
        public readonly Dictionary<string, int> FailedCompileByType = new();
        public readonly List<string> MembershipExamples = new();
        public readonly List<string> UniformExamples = new();
        public readonly List<string> SkippedReasons = new();
        public int EmptyResourceId;
        // lookup counters (used on the lookup Stats instance)
        public int ExactHit, NotInDb, CookedExportMissing, CookedIdEmpty, CookedKeyNoMap, CookedOpenErrors;
        public int NameUnique, NameAmbiguousShipping, NameAmbiguousDecooked, NameNotInShipping;
        public readonly Dictionary<string, List<string>> Samples = new();
        public void Sample(string kind, string text)
        {
            if (!Samples.TryGetValue(kind, out var l)) Samples[kind] = l = new List<string>();
            if (l.Count < 2) l.Add(text);
        }
        public readonly List<string> NoMapSamples = new();
        public readonly Dictionary<string, long> PhaseMs = new();
        public readonly List<string> FullMatchExamples = new();
        public readonly List<string> DifferentMaterialInventory = new();
        public readonly List<string> UniformDifferenceInventory = new();

        public void Merge(Stats o)
        {
            Packages += o.Packages; PackagesFailedToOpen += o.PackagesFailedToOpen; Materials += o.Materials;
            NoShippingMap += o.NoShippingMap; NoExactKeyButBaseIdPresent += o.NoExactKeyButBaseIdPresent; WithShippingMap += o.WithShippingMap;
            TranslateFailed += o.TranslateFailed; PlanFailed += o.PlanFailed; CompilePartial += o.CompilePartial; FullyCompiled += o.FullyCompiled;
            MembershipMatch += o.MembershipMatch; MembershipMismatch += o.MembershipMismatch; PlanSubsetOfShipping += o.PlanSubsetOfShipping;
            UniformMatch += o.UniformMatch; UniformMismatch += o.UniformMismatch; UniformNotComparable += o.UniformNotComparable;
            MaterialsAllJobsMatchBytes += o.MaterialsAllJobsMatchBytes; MaterialsAllJobsMatchDisasm += o.MaterialsAllJobsMatchDisasm;
            MaterialsWithAnyDifferent += o.MaterialsWithAnyDifferent; ResourceBinaryErrors += o.ResourceBinaryErrors; Exceptions += o.Exceptions;
            MissingPermutations += o.MissingPermutations; UnexpectedPermutations += o.UnexpectedPermutations; JobsTotal += o.JobsTotal;
            InstrEqual += o.InstrEqual; InstrOursLess += o.InstrOursLess; InstrOursMore += o.InstrOursMore; DiffSameOpcodes += o.DiffSameOpcodes;
            EmptyResourceId += o.EmptyResourceId;
            ExactHit += o.ExactHit; NotInDb += o.NotInDb; CookedExportMissing += o.CookedExportMissing; CookedIdEmpty += o.CookedIdEmpty;
            CookedKeyNoMap += o.CookedKeyNoMap; CookedOpenErrors += o.CookedOpenErrors; NameUnique += o.NameUnique;
            NameAmbiguousShipping += o.NameAmbiguousShipping; NameAmbiguousDecooked += o.NameAmbiguousDecooked; NameNotInShipping += o.NameNotInShipping;
            foreach (var kv in o.Samples) foreach (var t in kv.Value) Sample(kv.Key, t);
            foreach (var p in o.JobKinds) JobKinds[p.Key] = JobKinds.GetValueOrDefault(p.Key) + p.Value;
            MergeGroups(Failures, o.Failures); MergeGroups(DiffPatterns, o.DiffPatterns); MergeGroups(UniformDiffPatterns, o.UniformDiffPatterns);
            MergeInts(MissingByType, o.MissingByType); MergeInts(UnexpectedByType, o.UnexpectedByType); MergeInts(FailedCompileByType, o.FailedCompileByType);
            foreach (var p in o.PhaseMs) PhaseMs[p.Key] = PhaseMs.GetValueOrDefault(p.Key) + p.Value;
            AddUpTo(MembershipExamples, o.MembershipExamples, 3); AddUpTo(UniformExamples, o.UniformExamples, 3);
            AddUpTo(NoMapSamples, o.NoMapSamples, 4); AddUpTo(FullMatchExamples, o.FullMatchExamples, 5);
            DifferentMaterialInventory.AddRange(o.DifferentMaterialInventory);
            UniformDifferenceInventory.AddRange(o.UniformDifferenceInventory);
            AddUpTo(SkippedReasons, o.SkippedReasons, 20);
        }

        private static void MergeGroups(Dictionary<string, Group> to, Dictionary<string, Group> from)
        {
            foreach (var p in from)
            {
                if (to.TryGetValue(p.Key, out var g)) g.Count += p.Value.Count;
                else to[p.Key] = new Group { Count = p.Value.Count, Example = p.Value.Example };
            }
        }
        private static void MergeInts(Dictionary<string, int> to, Dictionary<string, int> from)
        {
            foreach (var p in from) to[p.Key] = to.GetValueOrDefault(p.Key) + p.Value;
        }
        private static void AddUpTo(List<string> to, List<string> from, int max)
        {
            foreach (var x in from) if (to.Count < max) to.Add(x);
        }
    }

    [TestMethod]
    [TestCategory("ShippingParity")]
    public void CompareOfflineCompilerWithShippingShaderMaps()
    {
        string decooked = Environment.GetEnvironmentVariable("LE3_PARITY_DECOOKED");
        if (string.IsNullOrWhiteSpace(decooked))
            Assert.Inconclusive("Set LE3_PARITY_DECOOKED to a folder of decooked LE3 packages to run the shipping parity harness.");
        Assert.IsTrue(Directory.Exists(decooked), "LE3_PARITY_DECOOKED folder does not exist: " + decooked);

        int maxPackages = EnvInt("LE3_PARITY_MAX_PACKAGES", int.MaxValue);
        int maxMaterials = EnvInt("LE3_PARITY_MAX_MATERIALS", int.MaxValue);
        int skipPackages = EnvInt("LE3_PARITY_SKIP_PACKAGES", 0);
        int threads = EnvInt("LE3_PARITY_THREADS", 4); // parallel shader compiles within one material
        int packageThreads = EnvInt("LE3_PARITY_PACKAGE_THREADS", Math.Max(1, Environment.ProcessorCount / 2)); // packages processed concurrently
        double maxMinutes = EnvInt("LE3_PARITY_MAX_MINUTES", 20);
        string gameRoot = Environment.GetEnvironmentVariable("LE3_PARITY_GAME");
        if (string.IsNullOrWhiteSpace(gameRoot)) gameRoot = DefaultGameRoot;
        string reportPath = Environment.GetEnvironmentVariable("LE3_PARITY_REPORT");
        if (string.IsNullOrWhiteSpace(reportPath)) reportPath = Path.Combine(Path.GetTempPath(), "le3_parity_report.txt");
        string dumpDir = Environment.GetEnvironmentVariable("LE3_PARITY_DUMP_DIR");

        GlobalTest.Init();
        // The reader resolves the shader cache from the LE3 default game path; tests normally point that at the
        // mini test game, so aim it at the real install for this opt-in run only.
        LE3Directory.DefaultGamePath = gameRoot;
        string cachePath = RefShaderCacheReader.ShaderFilePath(MEGame.LE3);
        Assert.IsTrue(cachePath != null && File.Exists(cachePath), "Shipping shader cache not found: " + cachePath);
        string shaderDir = Path.Combine(gameRoot, "Engine", "Shaders");
        Assert.IsTrue(Directory.Exists(shaderDir), "Shader sources not found: " + shaderDir);

        var total = Stopwatch.StartNew();
        var stats = new Stats();
        var snapshot = LE3ShaderSourceSnapshot.FromDirectory(shaderDir, 1252);

        // One pass over the shipping cache to learn which material shader maps exist.
        var sw = Stopwatch.StartNew();
        RefShaderCacheReader.PopulateOffsets(MEGame.LE3);
        var index = ShippingIndex.Build();
        long setupMs = sw.ElapsedMilliseconds;

        // Census of the whole decooked set (object names + per-package material counts), used for unique-name fallback
        // and to report how many materials the skipped EffectsMaterials packages hold.
        var allPackagePaths = Directory.EnumerateFiles(decooked, "*.pcc", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        sw.Restart();
        var census = DecookedCensus.Build(allPackagePaths);
        long censusMs = sw.ElapsedMilliseconds;
        using var cooked = new CookedResolver(gameRoot);

        bool includeEffects = Environment.GetEnvironmentVariable("LE3_PARITY_INCLUDE_EFFECTS") == "1";
        // LE3_PARITY_INSTANCES=1: compare MaterialInstance static permutations (exact cooked key only) instead of base materials.
        bool instanceMode = Environment.GetEnvironmentVariable("LE3_PARITY_INSTANCES") == "1";
        bool onlyEffects = Environment.GetEnvironmentVariable("LE3_PARITY_ONLY_EFFECTS") == "1";
        static bool IsEffects(string path) => Path.GetFileName(path).StartsWith("EffectsMaterials", StringComparison.OrdinalIgnoreCase);
        var skipped = new List<(string Name, int Materials)>();
        IEnumerable<string> selected = allPackagePaths;
        if (onlyEffects) selected = selected.Where(IsEffects);
        else if (!includeEffects)
        {
            skipped = allPackagePaths.Where(IsEffects).Select(x => (Path.GetFileName(x), census.MaterialsPerPackage.GetValueOrDefault(Path.GetFileName(x)))).ToList();
            selected = selected.Where(x => !IsEffects(x));
        }
        // LE3_PARITY_PACKAGES: optional ';'-separated package file names (without folder) to run exclusively.
        var onlyPackages = Environment.GetEnvironmentVariable("LE3_PARITY_PACKAGES")?
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var onlyMaterials = Environment.GetEnvironmentVariable("LE3_PARITY_MATERIALS")?
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (onlyPackages is { Length: > 0 })
            selected = allPackagePaths.Where(x => onlyPackages.Contains(Path.GetFileName(x), StringComparer.OrdinalIgnoreCase));
        var packagePaths = selected.Skip(skipPackages).Take(maxPackages).ToList();
        string mode = (onlyEffects ? "ONLY EffectsMaterials*" : includeEffects ? "all packages (EffectsMaterials* included)" : "EffectsMaterials* skipped") +
            (instanceMode ? "; INSTANCE PERMUTATIONS (exact cooked static key; group (a) = instances)" : "");

        var firstShippingInfo = new FirstShaderInfo();
        using var resolver = new TextureResolver(decooked, cooked.FindTextureDonorFiles);
        string progressPath = reportPath + ".progress";
        bool budgetHit = false;
        int materialsStarted = 0, packagesDone = 0;
        var dump = new DumpCounter();
        var gate = new object();
        var lastReport = Stopwatch.StartNew();
        var look = new Stats(); var statsA = new Stats(); var statsB = new Stats();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        string Report(bool budget, bool partial) => BuildReport(look, statsA, statsB, firstShippingInfo, resolver, cooked, census, skipped, mode, packagePaths.Count,
            total.Elapsed, setupMs + censusMs, index, budget, threads, decooked, gameRoot, partial);

        // Packages are independent (separate IMEPackage instances), so process several at once. Each worker keeps
        // local stats that are merged under a lock; a fresh compiler is created per material.
        Parallel.ForEach(packagePaths, new ParallelOptions { MaxDegreeOfParallelism = packageThreads }, (packagePath, loopState) =>
        {
            if (Volatile.Read(ref materialsStarted) >= maxMaterials) { loopState.Stop(); return; }
            if (total.Elapsed.TotalMinutes >= maxMinutes) { budgetHit = true; loopState.Stop(); return; }
            var l = new Stats(); var a = new Stats(); var b = new Stats();
            var psw = Stopwatch.StartNew();
            IMEPackage package = null;
            var opened = new Dictionary<string, IMEPackage>(StringComparer.OrdinalIgnoreCase);
            try
            {
                package = MEPackageHandler.OpenMEPackage(packagePath);
                if (package.Game != MEGame.LE3)
                {
                    l.SkippedReasons.Add("not LE3: " + Path.GetFileName(packagePath));
                }
                else
                {
                    AddMs(l, "open", psw);
                    l.Packages++;
                    var materials = package.Exports
                        .Where(e => !e.IsDefaultObject && (instanceMode
                                ? e.IsA("MaterialInstance") && e.GetProperties().GetProp<BoolProperty>("bHasStaticPermutationResource")?.Value == true
                                : e.ClassName == "Material" || e.ClassName == "DecalMaterial") &&
                            (onlyMaterials is not { Length: > 0 } || onlyMaterials.Contains(e.InstancedFullPath, StringComparer.OrdinalIgnoreCase))).ToList();
                    foreach (var material in materials)
                    {
                        if (Interlocked.Increment(ref materialsStarted) > maxMaterials) break;
                        if (total.Elapsed.TotalMinutes >= maxMinutes) { budgetHit = true; break; }
                        if (instanceMode)
                        {
                            ProcessInstance(material, decooked, opened, index, cooked, snapshot, resolver, l, a, firstShippingInfo, threads, dumpDir, dump);
                            continue;
                        }
                        try
                        {
                            var (grp, map) = ResolveMaterial(material, index, cooked, census, l);
                            if (grp == LookupGroup.Excluded) continue;
                            var target = grp == LookupGroup.ExactCooked ? a : b;
                            target.Materials++;
                            try
                            {
                                ProcessMaterial(material, snapshot, map, resolver, target, firstShippingInfo, threads, dumpDir, dump);
                            }
                            catch (Exception e)
                            {
                                target.Exceptions++;
                                AddGroup(target.Failures, "harness-exception: " + Normalize(e.GetType().Name + " " + FirstLine(e.Message)), material.InstancedFullPath + " @ " + Path.GetFileName(packagePath));
                            }
                        }
                        catch (Exception e)
                        {
                            l.Exceptions++;
                            AddGroup(l.Failures, "lookup-exception: " + Normalize(e.GetType().Name + " " + FirstLine(e.Message)), material.InstancedFullPath + " @ " + Path.GetFileName(packagePath));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                l.PackagesFailedToOpen++;
                AddGroup(l.Failures, "open-package: " + Normalize(e.GetType().Name + " " + FirstLine(e.Message)), packagePath);
            }
            finally
            {
                package?.Dispose();
                foreach (var p in opened.Values) p?.Dispose();
            }
            lock (gate)
            {
                look.Merge(l); statsA.Merge(a); statsB.Merge(b);
                packagesDone++;
                try
                {
                    File.WriteAllText(progressPath, $"{DateTime.Now:T} packages {packagesDone}/{packagePaths.Count} materials {look.Materials} exact {statsA.Materials} unique-name {statsB.Materials} jobs {statsA.JobsTotal + statsB.JobsTotal} elapsed {total.Elapsed.TotalSeconds:F0}s");
                    if (lastReport.Elapsed.TotalSeconds > 90)
                    {
                        lastReport.Restart();
                        File.WriteAllText(reportPath, Report(true, true));
                    }
                }
                catch { }
            }
        });

        string report = Report(budgetHit, false);
        File.WriteAllText(reportPath, report);
        Console.WriteLine(report.Length > 6000 ? report[..6000] : report);
        Console.WriteLine("Report: " + reportPath);
    }

    private sealed class DumpCounter { public int N; }

    /// <summary>Instance mode: the decooked base graph compiled with the cooked instance's exact static key.</summary>
    private sealed record InstanceTarget(ExportEntry BaseMaterial, StaticParameterSet Key, MaterialResource Resource);

    /// <summary>Walks the decooked parent chain to its base Material. Imported parents are opened from the decooked
    /// folder by package name (read-only, cached per worker); returns null with a reason when the chain is broken.</summary>
    private static ExportEntry FindDecookedBase(ExportEntry instance, string decookedFolder, Dictionary<string, IMEPackage> opened, out string reason)
    {
        reason = null;
        var current = instance;
        for (int depth = 0; depth < 32; depth++)
        {
            if (current.IsA("Material")) return current;
            if (!current.IsA("MaterialInstance")) { reason = $"chain reaches {current.ClassName}"; return null; }
            int parentIndex = current.GetProperties().GetProp<ObjectProperty>("Parent")?.Value ?? 0;
            if (parentIndex == 0 || !current.FileRef.TryGetEntry(parentIndex, out var parent) || parent == null) { reason = "no Parent"; return null; }
            if (parent is ExportEntry local) { current = local; continue; }
            string packageName = parent.InstancedFullPath.Split('.')[0];
            string path = Path.Combine(decookedFolder, packageName + ".pcc");
            if (!opened.TryGetValue(path, out var pkg))
            {
                pkg = File.Exists(path) ? MEPackageHandler.OpenMEPackage(path) : null; // read-only: never saved
                opened[path] = pkg;
            }
            current = pkg?.FindExport(parent.InstancedFullPath);
            if (current == null) { reason = "imported parent not in decooked folder"; return null; }
        }
        reason = "parent chain too deep";
        return null;
    }

    // ------------------------------------------------------------------------------------------------

    private enum LookupGroup { ExactCooked, UniqueName, Excluded }

    private static void ProcessInstance(ExportEntry instance, string decookedFolder, Dictionary<string, IMEPackage> opened, ShippingIndex index,
        CookedResolver cooked, LE3ShaderSourceSnapshot snapshot, TextureResolver resolver, Stats look, Stats target, FirstShaderInfo first,
        int threads, string dumpDir, DumpCounter dump)
    {
        string where = instance.InstancedFullPath + " @ " + Path.GetFileName(instance.FileRef.FilePath);
        look.Materials++;
        try
        {
            var baseMaterial = FindDecookedBase(instance, decookedFolder, opened, out string reason);
            if (baseMaterial == null) { look.NameNotInShipping++; look.Sample("instance: decooked base unresolved (" + reason + ")", where); return; }
            var sw = Stopwatch.StartNew();
            var (outcome, map, key, resource, note) = cooked.ResolveInstance(instance, baseMaterial, index);
            AddMs(look, "lookup", sw);
            switch (outcome)
            {
                case CookedOutcome.Hit: look.ExactHit++; look.Sample("exact", where + note); break;
                case CookedOutcome.NoMap: look.CookedKeyNoMap++; look.Sample("cooked key has no shipping map", where + note); return;
                case CookedOutcome.IdEmpty: look.CookedIdEmpty++; look.Sample("cooked copy has no static permutation", where + note); return;
                case CookedOutcome.OpenError: look.CookedOpenErrors++; look.Sample("cooked open/read error", where + note); return;
                case CookedOutcome.NotInDb: look.NotInDb++; look.Sample("instance not in object DB", where); return;
                default: look.CookedExportMissing++; return;
            }
            target.Materials++;
            ProcessMaterial(instance, snapshot, map, resolver, target, first, threads, dumpDir, dump, new InstanceTarget(baseMaterial, key, resource));
        }
        catch (Exception e)
        {
            target.Exceptions++;
            AddGroup(target.Failures, "harness-exception: " + Normalize(e.GetType().Name + " " + FirstLine(e.Message)), where);
        }
    }

    private static (LookupGroup Group, MaterialShaderMap Map) ResolveMaterial(ExportEntry export, ShippingIndex index, CookedResolver cooked,
        DecookedCensus census, Stats look)
    {
        string where = export.InstancedFullPath + " @ " + Path.GetFileName(export.FileRef.FilePath);
        look.Materials++;
        var sw = Stopwatch.StartNew();
        var (outcome, map, note) = cooked.Resolve(export, index);
        AddMs(look, "lookup", sw);
        switch (outcome)
        {
            case CookedOutcome.Hit:
                look.ExactHit++; look.Sample("exact", where + note);
                return (LookupGroup.ExactCooked, map);
            case CookedOutcome.NoMap:
                look.CookedKeyNoMap++; look.Sample("cooked key has no shipping map", where + note);
                return (LookupGroup.Excluded, null);
            case CookedOutcome.IdEmpty:
                look.CookedIdEmpty++; look.Sample("cooked ID empty", where);
                return (LookupGroup.Excluded, null);
            case CookedOutcome.OpenError:
                look.CookedOpenErrors++; look.Sample("cooked open/read error", where + note);
                return (LookupGroup.Excluded, null);
            case CookedOutcome.NotInDb: look.NotInDb++; break;
            case CookedOutcome.ExportMissing: look.CookedExportMissing++; break;
        }
        // No cooked copy: unique-name fallback (the FriendlyName is only an object name, so it must be unique on both sides).
        string name = export.ObjectName.Instanced;
        int shipping = index.NameCount(name), decooked = census.NameCount(name);
        if (shipping == 0) { look.NameNotInShipping++; look.Sample("no cooked copy, name not in shipping", where); return (LookupGroup.Excluded, null); }
        if (shipping > 1) { look.NameAmbiguousShipping++; look.Sample("no cooked copy, name ambiguous in shipping", where + $" ({shipping} maps)"); return (LookupGroup.Excluded, null); }
        if (decooked > 1) { look.NameAmbiguousDecooked++; look.Sample("no cooked copy, name ambiguous in decooked", where + $" ({decooked} materials)"); return (LookupGroup.Excluded, null); }
        var byName = index.BaseMapByName(name);
        if (byName == null) { look.NameNotInShipping++; look.Sample("no cooked copy, only shipping map is a static-param permutation", where); return (LookupGroup.Excluded, null); }
        look.NameUnique++; look.Sample("unique-name", where);
        return (LookupGroup.UniqueName, byName);
    }

    // LE3_PARITY_MEMBERSHIP_LOG: one line per membership mismatch with the material's selection-relevant properties.
    private static readonly string MembershipLog = Environment.GetEnvironmentVariable("LE3_PARITY_MEMBERSHIP_LOG");
    private static readonly object MembershipLogLock = new();
    private static void LogMembership(ExportEntry export, string where, LE3MaterialShaderCompiler.ShaderPermutationComparison membership)
    {
        if (string.IsNullOrEmpty(MembershipLog)) return;
        var props = export.GetPropertiesIncludingInherited();
        string flags = string.Join(",", props.OfType<BoolProperty>().Where(b => b.Value).Select(b => b.Name.Name));
        string enums = string.Join(",", props.OfType<EnumProperty>().Select(e => $"{e.Name.Name}={e.Value.Name}"));
        static string Group(IEnumerable<LE3MaterialShaderCompiler.ShaderPermutationIdentity> ids) => string.Join("; ", ids
            .GroupBy(i => i.VertexFactoryTypeName ?? "non-mesh").Select(g => $"{g.Key}: {string.Join(" ", g.Select(i => i.ShaderTypeName))}"));
        string line = $"{where}" + Environment.NewLine + $"  flags: {flags}" + Environment.NewLine + $"  enums: {enums}" + Environment.NewLine +
            $"  parent: {export.Parent?.ClassName}" + Environment.NewLine + $"  missing: {Group(membership.MissingPermutations)}" + Environment.NewLine +
            $"  planOnly: {Group(membership.UnexpectedPermutations)}" + Environment.NewLine;
        lock (MembershipLogLock) File.AppendAllText(MembershipLog, line);
    }

    private static void ProcessMaterial(ExportEntry export, LE3ShaderSourceSnapshot snapshot, MaterialShaderMap map,
        TextureResolver resolver, Stats stats, FirstShaderInfo first, int threads, string dumpDir, DumpCounter dump,
        InstanceTarget instance = null)
    {
        string where = export.InstancedFullPath + " @ " + Path.GetFileName(export.FileRef.FilePath);
        var sw = Stopwatch.StartNew();

        MaterialResource resource;
        try
        {
            resource = instance?.Resource ?? ObjectBinary.From<Material>(export).SM3MaterialResource;
        }
        catch (Exception e)
        {
            stats.ResourceBinaryErrors++;
            AddGroup(stats.Failures, "material-binary: " + Normalize(e.GetType().Name + " " + FirstLine(e.Message)), where);
            return;
        }
        stats.WithShippingMap++;

        // ---- translate -------------------------------------------------------------------------
        sw.Restart();
        var compiler = new LE3MaterialShaderCompiler(instance == null ? new ResolvedMaterialCompilationInput(export)
            : new ResolvedMaterialCompilationInput(instance.BaseMaterial, instance.Key, requestedMaterialInstance: export), null, resolver.Get, null);
        TranslationResult translation;
        try
        {
            translation = compiler.TranslateMaterialResourceHlsl(snapshot);
        }
        catch (Exception e)
        {
            stats.TranslateFailed++;
            AddGroup(stats.Failures, "translate(exception): " + Normalize(e.GetType().Name + " " + FirstLine(e.Message)), where);
            return;
        }
        AddMs(stats, "translate", sw);
        if (!translation.Success)
        {
            stats.TranslateFailed++;
            foreach (var key2 in (translation.Errors.Count == 0 ? new[] { "no HLSL produced" } : translation.Errors.ToArray()).Select(m => "translate: " + Normalize(m)).Distinct())
                AddGroup(stats.Failures, key2, where);
            return;
        }

        LE3ParityInspection.Write(InspectionDirectory, "decooked", export, resource, translation, map);

        // ---- uniform arrays (independent of the plan) ------------------------------------------
        sw.Restart();
        try
        {
            var uniform = CompareUniformExpressionsWithShaderMap(translation, map);
            if (uniform.MatchesExpectedUniformExpressions) stats.UniformMatch++;
            else
            {
                stats.UniformMismatch++;
                foreach (string difference in uniform.Differences)
                    stats.UniformDifferenceInventory.Add(where + ": " + difference);
                foreach (string error in uniform.Errors)
                    stats.UniformDifferenceInventory.Add(where + ": error: " + error);
                string firstDiff = uniform.Differences.Count > 0 ? uniform.Differences[0] : (uniform.Errors.Count > 0 ? "error: " + uniform.Errors[0] : "unknown");
                AddGroup(stats.UniformDiffPatterns, Normalize(firstDiff), where);
                if (stats.UniformExamples.Count < 3)
                    stats.UniformExamples.Add($"{where}: expected {uniform.ExpectedExpressionCount} translated {uniform.TranslatedExpressionCount}; {Trunc(firstDiff, 220)}");
            }
        }
        catch (Exception e)
        {
            stats.UniformNotComparable++;
            AddGroup(stats.Failures, "uniform-compare(exception): " + Normalize(e.GetType().Name + " " + FirstLine(e.Message)), where);
        }
        AddMs(stats, "uniform-compare", sw);

        // ---- plan ------------------------------------------------------------------------------
        sw.Restart();
        ShaderCompilePlan plan;
        try
        {
            plan = compiler.BuildKnownSubsetCompilePlan(translation, resource);
        }
        catch (Exception e)
        {
            stats.PlanFailed++;
            AddGroup(stats.Failures, "plan(exception): " + Normalize(e.GetType().Name + " " + FirstLine(e.Message)), where);
            return;
        }
        AddMs(stats, "plan", sw);
        if (!plan.Success)
        {
            stats.PlanFailed++;
            foreach (var key2 in (plan.Errors.Count == 0 ? new[] { "plan failed without message" } : plan.Errors.ToArray()).Select(m => "plan: " + Normalize(StripIdentity(m))).Distinct())
                AddGroup(stats.Failures, key2, where);
            return;
        }

        // ---- membership ------------------------------------------------------------------------
        var membership = ComparePlannedPermutationsWithShaderMap(plan, map);
        // Native IsComplete requires only the required set; shader maps keep stale extras across recompiles.
        if (membership.PlanSucceeded && membership.Errors.Count == 0 && membership.UnexpectedPermutations.Count == 0) stats.PlanSubsetOfShipping++;
        if (membership.MatchesExpectedPermutations) stats.MembershipMatch++;
        else
        {
            stats.MembershipMismatch++;
            LogMembership(export, where, membership);
            stats.MissingPermutations += membership.MissingPermutations.Count;
            stats.UnexpectedPermutations += membership.UnexpectedPermutations.Count;
            foreach (var p in membership.MissingPermutations) Bump(stats.MissingByType, p.ShaderTypeName + (p.VertexFactoryTypeName == null ? "" : "/" + p.VertexFactoryTypeName));
            foreach (var p in membership.UnexpectedPermutations) Bump(stats.UnexpectedByType, p.ShaderTypeName + (p.VertexFactoryTypeName == null ? "" : "/" + p.VertexFactoryTypeName));
            if (stats.MembershipExamples.Count < 3)
                stats.MembershipExamples.Add($"{where}: planned {membership.PlannedPermutationCount} vs shipping {membership.ExpectedPermutationCount}; missing(from plan) {membership.MissingPermutations.Count} e.g. [{string.Join(", ", membership.MissingPermutations.Take(3).Select(Describe))}]; unexpected(plan-only) {membership.UnexpectedPermutations.Count} e.g. [{string.Join(", ", membership.UnexpectedPermutations.Take(3).Select(Describe))}]{(membership.Errors.Count > 0 ? "; errors: " + Trunc(membership.Errors[0], 160) : "")}");
        }

        // ---- compile + per-job compare ---------------------------------------------------------
        sw.Restart();
        var lookup = BuildShippingLookup(map);
        var outcomes = new JobOutcome[plan.Jobs.Count];
        Parallel.For(0, plan.Jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = threads }, i =>
        {
            outcomes[i] = ProcessJob(compiler, plan.Jobs[i], snapshot, lookup, first);
        });
        AddMs(stats, "compile+compare", sw);

        stats.JobsTotal += outcomes.Length;
        int compileFailures = 0, matchBytes = 0, matchDisasm = 0;
        foreach (var o in outcomes)
        {
            stats.JobKinds[o.Kind] = stats.JobKinds.GetValueOrDefault(o.Kind) + 1;
            switch (o.Kind)
            {
                case JobKind.CompileFailed:
                    compileFailures++;
                    Bump(stats.FailedCompileByType, o.ShaderType + (o.VertexFactory == null ? "" : "/" + o.VertexFactory));
                    AddGroup(stats.Failures, "compile: " + o.Detail, where + $" [{o.ShaderType}{(o.VertexFactory == null ? "" : "/" + o.VertexFactory)}]");
                    continue;
                case JobKind.IdenticalBytes: matchBytes++; matchDisasm++; break;
                case JobKind.IdenticalDisassembly: matchDisasm++; break;
                case JobKind.Different:
                    AddGroup(stats.DiffPatterns, o.Detail, $"{where} [{o.ShaderType}{(o.VertexFactory == null ? "" : "/" + o.VertexFactory)}]: {o.FirstDiffExample}");
                    if (o.SameOpcodeSequence) stats.DiffSameOpcodes++;
                    int dumpSlot = -1;
                    if (dumpDir != null && (dumpSlot = Interlocked.Increment(ref dump.N) - 1) < 8)
                    {
                        Directory.CreateDirectory(dumpDir);
                        string stem = Path.Combine(dumpDir, $"diff_{dumpSlot}_{Sanitize(export.ObjectName.Instanced)}_{Sanitize(o.ShaderType)}_{Sanitize(o.VertexFactory ?? "nonmesh")}");
                        File.WriteAllText(stem + "_ours.txt", o.OursDisasm ?? "");
                        File.WriteAllText(stem + "_shipping.txt", o.TheirsDisasm ?? "");
                    }
                    break;
                case JobKind.DisassemblyError:
                    AddGroup(stats.Failures, "disassembly: " + o.Detail, where);
                    break;
            }
            if (o.Kind is JobKind.IdenticalBytes or JobKind.IdenticalDisassembly or JobKind.Different)
            {
                if (o.OursInstr == o.TheirsInstr) stats.InstrEqual++;
                else if (o.OursInstr < o.TheirsInstr) stats.InstrOursLess++;
                else stats.InstrOursMore++;
            }
        }
        if (compileFailures == 0) stats.FullyCompiled++; else stats.CompilePartial++;
        if (outcomes.Any(o => o.Kind == JobKind.Different))
        {
            stats.MaterialsWithAnyDifferent++;
            stats.DifferentMaterialInventory.Add(where + $": {outcomes.Count(o => o.Kind == JobKind.Different)}/{outcomes.Length} jobs differ; " +
                outcomes.First(o => o.Kind == JobKind.Different).FirstDiffExample);
        }
        if (outcomes.Length > 0 && matchBytes == outcomes.Length) stats.MaterialsAllJobsMatchBytes++;
        if (outcomes.Length > 0 && matchDisasm == outcomes.Length)
        {
            stats.MaterialsAllJobsMatchDisasm++;
            if (stats.FullMatchExamples.Count < 5) stats.FullMatchExamples.Add(where + $" ({outcomes.Length} jobs)");
        }
    }

    private static JobOutcome ProcessJob(LE3MaterialShaderCompiler compiler, ShaderCompileJob job, LE3ShaderSourceSnapshot snapshot,
        Dictionary<string, ShippingShaderEntry> lookup, FirstShaderInfo first)
    {
        string type = job.Target.ShaderTypeName, vf = job.Target.VertexFactoryTypeName;
        var outcome = new JobOutcome { ShaderType = type, VertexFactory = vf };
        ShaderCompilationResult result;
        try
        {
            result = compiler.CompileShader(job, snapshot);
        }
        catch (Exception e)
        {
            outcome.Kind = JobKind.CompileFailed;
            outcome.Detail = "exception " + Normalize(e.GetType().Name + " " + FirstLine(e.Message));
            return outcome;
        }
        if (!result.Success)
        {
            outcome.Kind = JobKind.CompileFailed;
            outcome.Detail = Normalize(FirstErrorLine(result.Diagnostics));
            return outcome;
        }
        outcome.OursInstr = result.InstructionCount;

        string lookupKey = vf == null ? type : vf + "|" + type;
        if (!lookup.TryGetValue(lookupKey, out var shipping))
        {
            outcome.Kind = JobKind.NotInShippingMap;
            return outcome;
        }
        byte[] theirs = RefShaderCacheReader.GetShaderBytecode(MEGame.LE3, shipping.Guid);
        if (theirs == null || theirs.Length == 0)
        {
            outcome.Kind = JobKind.ShippingBytecodeMissing;
            return outcome;
        }
        first.OfferPair(result.Bytecode, theirs, type, vf);
        outcome.TheirsInstr = ReadStatInstructionCount(theirs) ?? -1;

        if (result.Bytecode.AsSpan().SequenceEqual(theirs))
        {
            outcome.Kind = JobKind.IdenticalBytes;
            return outcome;
        }
        List<string> a, b;
        try
        {
            outcome.OursDisasm = Disassemble(result.Bytecode);
            outcome.TheirsDisasm = Disassemble(theirs);
            a = NormalizeDisassembly(outcome.OursDisasm);
            b = NormalizeDisassembly(outcome.TheirsDisasm);
        }
        catch (Exception e)
        {
            outcome.Kind = JobKind.DisassemblyError;
            outcome.Detail = Normalize(e.GetType().Name + " " + FirstLine(e.Message));
            return outcome;
        }
        if (a.SequenceEqual(b))
        {
            outcome.Kind = JobKind.IdenticalDisassembly;
            outcome.OursDisasm = outcome.TheirsDisasm = null;
            return outcome;
        }
        outcome.Kind = JobKind.Different;
        outcome.SameOpcodeSequence = a.Select(Opcode).SequenceEqual(b.Select(Opcode));
        int n = Math.Min(a.Count, b.Count), idx = 0;
        while (idx < n && a[idx] == b[idx]) idx++;
        string la = idx < a.Count ? a[idx] : "<end of ours>", lb = idx < b.Count ? b[idx] : "<end of shipping>";
        outcome.FirstDiffExample = $"line {idx}: ours `{Trunc(la, 90)}` vs shipping `{Trunc(lb, 90)}` (ours {a.Count} lines, shipping {b.Count})";
        outcome.Detail = (outcome.SameOpcodeSequence ? "[same-opcodes] " : "[diff-opcodes] ") + "ours `" + PatternOf(la) + "` | shipping `" + PatternOf(lb) + "`";
        return outcome;
    }

    // ---- shipping map index ----------------------------------------------------------------------

    private sealed class ShippingIndex
    {
        public int TotalMaps, BaseMaps, DuplicateIds;
        public readonly Dictionary<Guid, MaterialShaderMap> ById = new();
        public readonly Dictionary<StaticParameterSet, MaterialShaderMap> ByStaticKey = new();
        private readonly Dictionary<string, int> nameCounts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MaterialShaderMap> baseByName = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> SampleNames = new();

        public int NameCount(string name) => nameCounts.GetValueOrDefault(name);
        public MaterialShaderMap BaseMapByName(string name) => baseByName.GetValueOrDefault(name);

        public static ShippingIndex Build()
        {
            var index = new ShippingIndex();
            string path = RefShaderCacheReader.ShaderFilePath(MEGame.LE3);
            var readerType = typeof(RefShaderCacheReader);
            const System.Reflection.BindingFlags priv = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            int mapsOffset = (int)readerType.GetField("LE3MaterialShaderMapsOffset", priv).GetValue(null);
            using var fs = File.OpenRead(path);
            using var package = MEPackageHandler.OpenMEPackageFromStream(fs, quickLoad: true);
            readerType.GetMethod("ReadNames", priv).Invoke(null, [fs, package]);
            var sc = new SerializingContainer(fs, package, true);
            sc.ms.JumpTo(mapsOffset);
            int count = fs.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                StaticParameterSet sps = null;
                sc.Serialize(ref sps);
                long start = sc.ms.Position;
                sc.ms.Skip(8);
                int end = sc.ms.ReadInt32();
                sc.ms.JumpTo(start);
                MaterialShaderMap map = null;
                sc.Serialize(ref map);
                sc.ms.JumpTo(end);
                index.TotalMaps++;
                bool isBase = (sps.StaticSwitchParameters?.Length ?? 0) == 0 && (sps.StaticComponentMaskParameters?.Length ?? 0) == 0 &&
                              (sps.NormalParameters?.Length ?? 0) == 0 && (sps.TerrainWeightParameters?.Length ?? 0) == 0;
                string name = map.FriendlyName ?? "";
                index.nameCounts[name] = index.nameCounts.GetValueOrDefault(name) + 1;
                if (index.SampleNames.Count < 6) index.SampleNames.Add($"{name} (base={isBase})");
                if (!isBase)
                {
                    // Instance permutations: the exact key is the whole StaticParameterSet (bOverride ignored by equality).
                    if (!index.ByStaticKey.TryAdd(sps, map)) index.DuplicateIds++;
                    continue;
                }
                index.BaseMaps++;
                // Exact key: StaticParameterSet with BaseMaterialId = id and empty parameter lists, same equality as the reader's lookup.
                if (!index.ById.TryAdd(sps.BaseMaterialId, map)) index.DuplicateIds++;
                index.baseByName[name] = map;
            }
            return index;
        }
    }

    /// <summary>Object-name census of every decooked base material, plus per-package material counts.</summary>
    private sealed class DecookedCensus
    {
        public int TotalMaterials;
        public readonly Dictionary<string, int> Names = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> MaterialsPerPackage = new(StringComparer.OrdinalIgnoreCase);
        public int NameCount(string name) => Names.GetValueOrDefault(name);
        public int UniqueNames => Names.Count(p => p.Value == 1);

        public static DecookedCensus Build(IReadOnlyList<string> paths)
        {
            var census = new DecookedCensus();
            Parallel.ForEach(paths, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) }, path =>
            {
                try
                {
                    using var package = MEPackageHandler.OpenMEPackage(path);
                    if (package.Game != MEGame.LE3) return;
                    var names = package.Exports
                        .Where(e => !e.IsDefaultObject && (e.ClassName == "Material" || e.ClassName == "DecalMaterial"))
                        .Select(e => e.ObjectName.Instanced).ToList();
                    lock (census)
                    {
                        census.MaterialsPerPackage[Path.GetFileName(path)] = names.Count;
                        census.TotalMaterials += names.Count;
                        foreach (var n in names) census.Names[n] = census.Names.GetValueOrDefault(n) + 1;
                    }
                }
                catch { }
            });
            return census;
        }
    }

    private enum CookedOutcome { NotInDb = 0, ExportMissing = 1, OpenError = 2, IdEmpty = 3, NoMap = 4, Hit = 5 }

    /// <summary>
    /// Finds the shipping copy of a decooked material through the prebuilt LE3 object DB and reads its real
    /// MaterialResource.ID. Opened cooked packages are cached (small LRU) and disposed with the resolver.
    /// </summary>
    private sealed class CookedResolver : IDisposable
    {
        private readonly ObjectInstanceDB db;
        private readonly string gameRoot;
        private readonly Dictionary<string, IMEPackage> cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<string> order = new();
        public string SampleRelativePath;
        public int CandidateFilesTotal, MaterialsWithMultipleFiles, DistinctOpened;
        // Usage flags overlaid from the cooked copy onto the in-memory decooked material (never saved).
        public int FlagOverlayMaterials, FlagOverlayCount;
        public readonly ConcurrentDictionary<string, int> FlagOverlayNames = new();
        private static readonly bool OverlayCookedFlags = Environment.GetEnvironmentVariable("LE3_PARITY_COOKED_FLAGS") != "0";

        public CookedResolver(string gameRoot)
        {
            this.gameRoot = gameRoot;
            string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LegendaryExplorer", "ObjectDatabases", "LE3.bin");
            Assert.IsTrue(File.Exists(dbPath), "Object database not found: " + dbPath);
            // Read fully into memory: Deserialize issues single Stream.Read calls that only MemoryStream satisfies reliably.
            using var stream = new MemoryStream(File.ReadAllBytes(dbPath));
            db = ObjectInstanceDB.Deserialize(MEGame.LE3, stream);
        }

        public IEnumerable<string> FindTextureDonorFiles(string objectPath)
        {
            lock (this)
                return db.GetFilesContainingObject(objectPath)?
                    .Select(path => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(gameRoot, path)))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();
        }

        public (CookedOutcome Outcome, MaterialShaderMap Map, string Note) Resolve(ExportEntry export, ShippingIndex index)
        {
            lock (this)
            {
                string ifp = export.InstancedFullPath;
                var files = db.GetFilesContainingObject(ifp);
                if (files == null || files.Count == 0) return (CookedOutcome.NotInDb, null, "");
                SampleRelativePath ??= files[0];
                CandidateFilesTotal += files.Count;
                if (files.Count > 1) MaterialsWithMultipleFiles++;
                string decookedFile = Path.GetFileName(export.FileRef.FilePath ?? "");
                var ordered = files.OrderByDescending(f => string.Equals(Path.GetFileName(f), decookedFile, StringComparison.OrdinalIgnoreCase)).Take(4).ToList();
                var best = CookedOutcome.ExportMissing;
                string note = "";
                foreach (string rel in ordered)
                {
                    string full = Path.IsPathRooted(rel) ? rel : Path.Combine(gameRoot, rel);
                    IMEPackage package;
                    try { package = Open(full); }
                    catch (Exception e)
                    {
                        best = (CookedOutcome)Math.Max((int)best, (int)CookedOutcome.OpenError);
                        note = $" [{Path.GetFileName(rel)}: {e.GetType().Name}]";
                        continue;
                    }
                    var cookedExport = package.FindExport(ifp);
                    if (cookedExport == null) continue;
                    Guid id;
                    try { id = ObjectBinary.From<Material>(cookedExport).SM3MaterialResource.ID; }
                    catch (Exception e)
                    {
                        best = (CookedOutcome)Math.Max((int)best, (int)CookedOutcome.OpenError);
                        note = $" [{Path.GetFileName(rel)} binary: {e.GetType().Name}]";
                        continue;
                    }
                    if (id == Guid.Empty) { best = (CookedOutcome)Math.Max((int)best, (int)CookedOutcome.IdEmpty); continue; }
                    if (index.ById.TryGetValue(id, out var map))
                    {
                        LE3ParityInspection.Write(InspectionDirectory, "cooked", cookedExport,
                            ObjectBinary.From<Material>(cookedExport).SM3MaterialResource);
                        if (OverlayCookedFlags) OverlayUsageFlags(cookedExport, export);
                        return (CookedOutcome.Hit, map, $" [cooked {Path.GetFileName(rel)}, id {id}]");
                    }
                    best = (CookedOutcome)Math.Max((int)best, (int)CookedOutcome.NoMap);
                    note = $" [cooked {Path.GetFileName(rel)}, id {id}]";
                }
                return (best, null, note);
            }
        }

        /// <summary>
        /// Instance mode: the cooked copy's serialized static parameter set is the exact shipping key (the game never
        /// recomputes it). Also overlays the base material's cooked usage flags, since selection reads the base.
        /// </summary>
        public (CookedOutcome Outcome, MaterialShaderMap Map, StaticParameterSet Key, MaterialResource Resource, string Note)
            ResolveInstance(ExportEntry instance, ExportEntry decookedBase, ShippingIndex index)
        {
            lock (this)
            {
                string ifp = instance.InstancedFullPath;
                var files = db.GetFilesContainingObject(ifp);
                if (files == null || files.Count == 0) return (CookedOutcome.NotInDb, null, null, null, "");
                SampleRelativePath ??= files[0];
                var best = CookedOutcome.ExportMissing;
                string note = "";
                foreach (string rel in files.Take(4))
                {
                    string full = Path.IsPathRooted(rel) ? rel : Path.Combine(gameRoot, rel);
                    ExportEntry cookedExport;
                    MaterialInstance binary;
                    try
                    {
                        cookedExport = Open(full).FindExport(ifp);
                        if (cookedExport == null) continue;
                        binary = ObjectBinary.From<MaterialInstance>(cookedExport);
                    }
                    catch (Exception e)
                    {
                        best = (CookedOutcome)Math.Max((int)best, (int)CookedOutcome.OpenError);
                        note = $" [{Path.GetFileName(rel)}: {e.GetType().Name}]";
                        continue;
                    }
                    var key = binary?.SM3StaticParameterSet;
                    if (key == null || binary.SM3StaticPermutationResource == null)
                    {
                        best = (CookedOutcome)Math.Max((int)best, (int)CookedOutcome.IdEmpty);
                        note = $" [cooked {Path.GetFileName(rel)} has no static permutation]";
                        continue;
                    }
                    if (index.ByStaticKey.TryGetValue(key, out var map))
                    {
                        if (OverlayCookedFlags && decookedBase != null)
                        {
                            var cookedBase = db.GetFilesContainingObject(decookedBase.InstancedFullPath)?.Take(2)
                                .Select(r => { try { return Open(Path.IsPathRooted(r) ? r : Path.Combine(gameRoot, r)).FindExport(decookedBase.InstancedFullPath); } catch { return null; } })
                                .FirstOrDefault(e => e != null);
                            if (cookedBase != null) OverlayUsageFlags(cookedBase, decookedBase);
                        }
                        return (CookedOutcome.Hit, map, key, binary.SM3StaticPermutationResource, $" [cooked {Path.GetFileName(rel)}]");
                    }
                    best = (CookedOutcome)Math.Max((int)best, (int)CookedOutcome.NoMap);
                    note = $" [cooked {Path.GetFileName(rel)}, base id {key.BaseMaterialId}]";
                }
                return (best, null, null, null, note);
            }
        }

        // The editor sets bUsedWith* automatically when a material is used, so decooked copies can lag the
        // shipped state. Shader selection must use the shipped flags to compare like with like.
        private void OverlayUsageFlags(ExportEntry cookedExport, ExportEntry decooked)
        {
            static bool IsUsageFlag(string name) => name.StartsWith("bUsedWith", StringComparison.Ordinal) || name == "bUsedAsSpecialEngineMaterial";
            var cookedProps = cookedExport.GetProperties();
            var decookedProps = decooked.GetProperties();
            var names = cookedProps.OfType<BoolProperty>().Select(p => p.Name.Name)
                .Concat(decookedProps.OfType<BoolProperty>().Select(p => p.Name.Name)).Where(IsUsageFlag).Distinct().ToList();
            int changed = 0;
            foreach (string name in names)
            {
                bool cookedValue = cookedProps.GetProp<BoolProperty>(name)?.Value == true;
                bool decookedValue = decookedProps.GetProp<BoolProperty>(name)?.Value == true;
                if (cookedValue == decookedValue) continue;
                decookedProps.AddOrReplaceProp(new BoolProperty(cookedValue, name));
                FlagOverlayNames.AddOrUpdate($"{name}={cookedValue}", 1, (_, n) => n + 1);
                changed++;
            }
            if (changed == 0) return;
            decooked.WriteProperties(decookedProps);
            FlagOverlayMaterials++;
            FlagOverlayCount += changed;
        }

        private IMEPackage Open(string path)
        {
            if (cache.TryGetValue(path, out var p)) return p;
            p = MEPackageHandler.OpenMEPackage(path); // read-only: never saved
            DistinctOpened++;
            cache[path] = p;
            order.Enqueue(path);
            while (order.Count > 12)
            {
                string old = order.Dequeue();
                if (cache.Remove(old, out var oldPackage)) oldPackage.Dispose();
            }
            return p;
        }

        public void Dispose()
        {
            foreach (var p in cache.Values) p.Dispose();
            cache.Clear();
        }
    }

    // ---- shipping map lookup ---------------------------------------------------------------------

    private readonly record struct ShippingShaderEntry(Guid Guid);

    private static Dictionary<string, ShippingShaderEntry> BuildShippingLookup(MaterialShaderMap map)
    {
        var d = new Dictionary<string, ShippingShaderEntry>(StringComparer.Ordinal);
        foreach (var pair in map.Shaders)
            if (pair.Value != null) d.TryAdd(pair.Key.Instanced, new ShippingShaderEntry(pair.Value.Id));
        if (map.MeshShaderMaps != null)
            foreach (var mesh in map.MeshShaderMaps)
                foreach (var pair in mesh.Shaders)
                    if (pair.Value != null) d.TryAdd(mesh.VertexFactoryType.Instanced + "|" + pair.Key.Instanced, new ShippingShaderEntry(pair.Value.Id));
        return d;
    }

    // ---- DXBC helpers ---------------------------------------------------------------------------

    private static string Disassemble(byte[] bytecode)
    {
        using var bc = new global::SharpDX.D3DCompiler.ShaderBytecode(bytecode);
        return bc.Disassemble();
    }

    private static List<string> NormalizeDisassembly(string text) =>
        text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("//", StringComparison.Ordinal)).ToList();

    private static string Opcode(string line)
    {
        int i = 0;
        while (i < line.Length && !char.IsWhiteSpace(line[i])) i++;
        return line[..i];
    }

    private static readonly Regex NumberRx = new(@"-?\d+(\.\d+)?([eE][+-]?\d+)?", RegexOptions.Compiled);
    private static string PatternOf(string line) => Trunc(NumberRx.Replace(line, "#"), 90);

    private static List<(string Tag, int Offset, int Size)> ReadChunks(byte[] dxbc)
    {
        var list = new List<(string, int, int)>();
        if (dxbc.Length < 32 || dxbc[0] != 'D' || dxbc[1] != 'X' || dxbc[2] != 'B' || dxbc[3] != 'C') return list;
        int count = BitConverter.ToInt32(dxbc, 28);
        for (int i = 0; i < count && 32 + i * 4 + 4 <= dxbc.Length; i++)
        {
            int off = BitConverter.ToInt32(dxbc, 32 + i * 4);
            if (off < 0 || off + 8 > dxbc.Length) break;
            list.Add((Encoding.ASCII.GetString(dxbc, off, 4), off + 8, BitConverter.ToInt32(dxbc, off + 4)));
        }
        return list;
    }

    private static int? ReadStatInstructionCount(byte[] dxbc)
    {
        foreach (var (tag, off, size) in ReadChunks(dxbc))
            if (tag == "STAT" && size >= 4 && off + 4 <= dxbc.Length) return BitConverter.ToInt32(dxbc, off);
        return null;
    }

    private sealed class FirstShaderInfo
    {
        private readonly object gate = new();
        public byte[] Shipping, Ours;
        public string ShippingName;
        public void OfferPair(byte[] ours, byte[] shipping, string type, string vf)
        {
            lock (gate) { if (Shipping == null) { Shipping = shipping; Ours = ours; ShippingName = type + (vf == null ? "" : "/" + vf); } }
        }
    }

    private static void DescribeChunks(StringBuilder sb, string label, byte[] dxbc)
    {
        var chunks = ReadChunks(dxbc);
        sb.AppendLine($"{label}: {dxbc.Length} bytes, chunks: " + string.Join(", ", chunks.Select(c => $"{c.Tag}({c.Size})")));
        var debug = chunks.Where(c => c.Tag is "SDBG" or "SPDB" or "ILDB" or "ILDN" or "PDBG" or "SRCS" or "SRCI").ToList();
        if (debug.Count == 0)
        {
            sb.AppendLine($"  {label}: NO debug/source chunks (no SDBG/SPDB/ILDB/ILDN/SRCS): original source text is NOT recoverable from this bytecode.");
            return;
        }
        foreach (var (tag, off, size) in debug)
        {
            int end = Math.Min(dxbc.Length, off + size);
            // longest printable-ASCII run and count of runs >= 20 chars: crude detector for embedded source text
            int best = 0, bestStart = off, run = 0, runStart = off, runs20 = 0;
            for (int i = off; i < end; i++)
            {
                byte c = dxbc[i];
                bool printable = c is >= 0x20 and < 0x7f || c is 9 or 10 or 13;
                if (printable) { if (run == 0) runStart = i; run++; }
                else
                {
                    if (run >= 20) runs20++;
                    if (run > best) { best = run; bestStart = runStart; }
                    run = 0;
                }
            }
            if (run >= 20) runs20++;
            if (run > best) { best = run; bestStart = runStart; }
            string snippet = best > 0 ? Encoding.ASCII.GetString(dxbc, bestStart, Math.Min(best, 160)).Replace("\r", " ").Replace("\n", " | ") : "";
            string text = Encoding.ASCII.GetString(dxbc, off, Math.Max(0, end - off));
            bool hasMaterialText = text.Contains("MaterialTemplate", StringComparison.Ordinal) || text.Contains("CalcMaterialParameters", StringComparison.Ordinal)
                || text.Contains("GetMaterialEmissive", StringComparison.Ordinal) || text.Contains("#include", StringComparison.Ordinal);
            sb.AppendLine($"  {label} {tag} chunk {size} bytes: longest printable run {best} chars, {runs20} runs >= 20 chars, contains Material/#include text: {hasMaterialText}");
            sb.AppendLine($"    longest run starts: \"{snippet}\"");
            sb.AppendLine(hasMaterialText && best >= 200
                ? "    => embedded source text appears RECOVERABLE (printable runs of source size present)."
                : "    => no evidence of embedded source text (only short strings such as names/paths).");
        }
    }

    // ---- texture resolution ---------------------------------------------------------------------

    internal sealed class TextureResolver : IDisposable
    {
        private readonly string folder;
        private readonly Func<string, IEnumerable<string>> findDonorFiles;
        private readonly Func<string, bool> fileExists;
        private readonly Func<string, LE3TextureMetadataReader> openReader;
        private readonly Dictionary<string, LE3TextureMetadataReader> readers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<string> order = new();
        private readonly HashSet<string> missingPackages = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> Outcomes = new();
        public readonly List<string> MetadataErrors = new();
        public readonly SortedSet<string> MissingMetadataRequests = new(StringComparer.OrdinalIgnoreCase);

        public TextureResolver(string folder, Func<string, IEnumerable<string>> findDonorFiles = null,
            Func<string, bool> fileExists = null, Func<string, LE3TextureMetadataReader> openReader = null)
        {
            this.folder = Path.GetFullPath(folder);
            this.findDonorFiles = findDonorFiles;
            this.fileExists = fileExists ?? File.Exists;
            this.openReader = openReader ?? LE3TextureMetadataReader.Open;
        }

        private void Count(string what) => Outcomes[what] = Outcomes.GetValueOrDefault(what) + 1;

        // Guess-and-verify experiments only: LE3_PARITY_TEXTURE_OVERRIDES="Full.Texture.Path=TC_Compression;..."
        // replaces the resolved CompressionSettings. A candidate is accepted only if shipping bytecode then matches.
        private static readonly Dictionary<string, string> CompressionOverrides =
            (Environment.GetEnvironmentVariable("LE3_PARITY_TEXTURE_OVERRIDES") ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(pair => pair.Split('=', 2, StringSplitOptions.TrimEntries)).Where(kv => kv.Length == 2)
            .ToDictionary(kv => kv[0], kv => kv[1], StringComparer.OrdinalIgnoreCase);

        public TextureCompilationMetadata Get(IEntry texture)
        {
            TextureCompilationMetadata meta;
            lock (this) meta = GetCore(texture);
            if (meta == null || !CompressionOverrides.TryGetValue(texture.InstancedFullPath, out string compression)) return meta;
            lock (this) Count("compression override (guess) applied");
            return new TextureCompilationMetadata(meta.Dimension, compression, meta.UnpackMin, meta.UnpackMax, meta.FlipBookScale);
        }

        private TextureCompilationMetadata GetCore(IEntry texture)
        {
            try
            {
                bool cube = texture.IsA("TextureCube");
                var defaultMeta = new TextureCompilationMetadata(cube ? TextureDimension.TextureCube : TextureDimension.Texture2D);
                if (texture is ExportEntry local)
                {
                    if (local.IsA("Texture2D") || cube)
                    {
                        Count("local export read");
                        return WithFlipBookScale(TextureCompilationMetadata.FromExport(local), local);
                    }
                    Count("local non-Texture2D (default metadata): " + local.ClassName);
                    return defaultMeta;
                }
                var import = (ImportEntry)texture;
                IEntry top = import;
                while (top.Parent != null) top = top.Parent;
                string package = top.ObjectName.Instanced;
                string full = import.InstancedFullPath;
                string inner = full.StartsWith(package + ".", StringComparison.OrdinalIgnoreCase) ? full[(package.Length + 1)..] : full;
                string primaryPath = Path.Combine(folder, package + ".pcc");
                var primary = ReadImportCandidate(primaryPath, full, inner, cube, primary: true);
                if (primary != null) { Count("import read ok"); return primary; }
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { primaryPath };
                foreach (string donorPath in findDonorFiles?.Invoke(full) ?? Array.Empty<string>())
                {
                    string candidatePath = Path.GetFullPath(donorPath);
                    if (!seen.Add(candidatePath)) continue;
                    var metadata = ReadImportCandidate(candidatePath, full, inner, cube, primary: false);
                    if (metadata == null) continue;
                    Count("import read ok (object DB donor)");
                    return metadata;
                }
                Count("import: no authoritative metadata donor");
                MissingMetadataRequests.Add($"{full} ({import.ClassName}) requested by {Path.GetFileName(import.FileRef.FilePath)}; candidate packages: " +
                    string.Join(", ", seen.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)));
                return null;
            }
            catch (Exception e)
            {
                Count("error " + e.GetType().Name);
                if (MetadataErrors.Count < 3) MetadataErrors.Add(texture?.InstancedFullPath + ": " + e);
                return null;
            }
        }

        private TextureCompilationMetadata ReadImportCandidate(string path, string full, string inner, bool cube, bool primary)
        {
            var reader = GetReader(path);
            if (reader == null) { Count(primary ? "import: package not in decooked folder" : "donor: unavailable package"); return null; }
            // A cooked container may hold thousands of unrelated objects. Only the original
            // full path is authoritative there; stripped paths are for decooked packages only.
            var export = reader.Package.FindExport(full) ?? (primary ? reader.Package.FindExport(inner) : null);
            if (export == null) { Count("import: export not found in package"); return null; }
            if (cube ? !export.IsA("TextureCube") : !export.IsA("Texture2D") || export.IsA("TextureCube"))
            { Count("import: incompatible texture class " + export.ClassName); return null; }
            try
            {
                return WithFlipBookScale(reader.Read(export), export, reader);
            }
            catch (Exception exception)
            {
                Count("donor metadata error " + exception.GetType().Name);
                if (MetadataErrors.Count < 3) MetadataErrors.Add(full + " @ " + path + ": " + exception);
                return null;
            }
        }

        // Effective flipbook scale is transient (PostLoad: 1/HorizontalImages, 1/VerticalImages, no clamping).
        // The compiler requires the caller to supply it, so the harness derives it from the stored counts.
        private TextureCompilationMetadata WithFlipBookScale(TextureCompilationMetadata meta, ExportEntry texture,
            LE3TextureMetadataReader reader = null)
        {
            if (meta == null || meta.FlipBookScale != null || !texture.IsA("TextureFlipBook")) return meta;
            PropertyCollection props;
            if (reader == null) props = texture.GetProperties();
            else
            {
                // Header-only donors have no export.Data buffer. Read the stored image counts
                // from a property stream, without loading the flipbook's texture payload.
                using var data = reader.Package.OpenExportDataStream(texture);
                data.Position = texture.GetPropertyStart();
                props = PropertyCollection.ReadProps(texture, data, texture.ClassName, includeNoneProperty: true,
                    entry: texture.IsDefaultObject ? texture.Class : texture);
            }
            static float ImageCount(Property p) => p switch { IntProperty i => i.Value, ByteProperty b => b.Value, FloatProperty f => f.Value, _ => 1 };
            float h = ImageCount(props.GetProp<Property>("HorizontalImages")), v = ImageCount(props.GetProp<Property>("VerticalImages"));
            Count("flipbook scale derived");
            return new TextureCompilationMetadata(meta.Dimension, meta.CompressionSettings, meta.UnpackMin, meta.UnpackMax,
                new System.Numerics.Vector2(1f / h, 1f / v));
        }

        private LE3TextureMetadataReader GetReader(string path)
        {
            if (readers.TryGetValue(path, out var r)) return r;
            if (missingPackages.Contains(path)) return null;
            if (!fileExists(path)) { missingPackages.Add(path); return null; }
            try
            {
                r = openReader(path);
            }
            catch
            {
                missingPackages.Add(path);
                return null;
            }
            readers[path] = r;
            order.Enqueue(path);
            while (order.Count > 24)
            {
                string old = order.Dequeue();
                if (readers.Remove(old, out var oldReader)) oldReader.Dispose();
            }
            return r;
        }

        public void Dispose()
        {
            foreach (var r in readers.Values) r.Dispose();
            readers.Clear();
        }
    }

    // ---- reporting ------------------------------------------------------------------------------

    private static string BuildReport(Stats look, Stats a, Stats b, FirstShaderInfo first, TextureResolver resolver, CookedResolver cooked,
        DecookedCensus census, List<(string Name, int Materials)> skipped, string mode, int packagesPlanned, TimeSpan elapsed, long setupMs,
        ShippingIndex index, bool budgetHit, int threads, string decooked, string gameRoot, bool partial = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("LE3 OFFLINE COMPILER vs SHIPPING SHADER MAPS - PARITY REPORT" + (partial ? "  [PARTIAL SNAPSHOT - run still in progress]" : ""));
        sb.AppendLine($"Decooked: {decooked}  Game: {gameRoot}  compile threads/material: {threads}  scope: {mode}");
        sb.AppendLine($"Elapsed {elapsed.TotalMinutes:F1} min (setup+census {setupMs} ms){(budgetHit ? "  [STOPPED: time budget reached before all packages]" : "")}");
        sb.AppendLine($"Shipping maps: {index.TotalMaps} ({index.BaseMaps} base/empty static params; duplicate base IDs {index.DuplicateIds}). Decooked base materials: {census.TotalMaterials} ({census.UniqueNames} unique object names).");
        if (skipped.Count > 0)
            sb.AppendLine($"SKIPPED packages ({skipped.Sum(x => x.Materials)} materials; opt in with LE3_PARITY_INCLUDE_EFFECTS=1 or LE3_PARITY_ONLY_EFFECTS=1): " +
                          string.Join(", ", skipped.Select(x => $"{x.Name} ({x.Materials})")));
        var phases = new Dictionary<string, long>();
        foreach (var st in new[] { look, a, b }) foreach (var p in st.PhaseMs) phases[p.Key] = phases.GetValueOrDefault(p.Key) + p.Value;
        sb.AppendLine($"Packages processed {look.Packages}/{packagesPlanned} (open failures {look.PackagesFailedToOpen}); phase ms (summed over threads): " +
                      string.Join(", ", phases.OrderByDescending(p => p.Value).Select(p => $"{p.Key} {p.Value}")));
        sb.AppendLine();

        sb.AppendLine("== MATERIAL LOOKUP (how each decooked base material was matched to a shipping map) ==");
        sb.AppendLine($"Materials examined: {look.Materials}   (a) exact key via cooked copy: {a.Materials}   (b) unique-name match: {b.Materials}   (c) excluded: {look.Materials - a.Materials - b.Materials}");
        sb.AppendLine($"  cooked-copy path: exact-key hit {look.ExactHit}; object not in DB {look.NotInDb}; cooked export missing {look.CookedExportMissing}; cooked ID empty {look.CookedIdEmpty}; cooked key with no shipping map {look.CookedKeyNoMap}; cooked open/read errors {look.CookedOpenErrors}");
        sb.AppendLine($"  no-cooked-copy fallback ({look.NotInDb + look.CookedExportMissing}): unique-name match {look.NameUnique}; excluded: name ambiguous in shipping {look.NameAmbiguousShipping}, ambiguous in decooked {look.NameAmbiguousDecooked}, no/only-permutation shipping map {look.NameNotInShipping}");
        sb.AppendLine($"  cooked usage-flag overlay: {cooked.FlagOverlayMaterials} materials, {cooked.FlagOverlayCount} flags; top: " +
            string.Join(", ", cooked.FlagOverlayNames.OrderByDescending(x => x.Value).Take(8).Select(x => $"{x.Key} x{x.Value}")));
        sb.AppendLine($"  object DB: sample relative path \"{cooked.SampleRelativePath}\"; materials in >1 cooked file {cooked.MaterialsWithMultipleFiles}; distinct cooked packages opened {cooked.DistinctOpened}");
        foreach (var kv in look.Samples.OrderBy(k => k.Key)) foreach (var t in kv.Value.Take(1)) sb.AppendLine($"    sample [{kv.Key}]: {Trunc(t, 170)}");
        foreach (var g in look.Failures) sb.AppendLine($"  lookup/harness failure x{g.Value.Count}: {g.Key} e.g. {Trunc(g.Value.Example, 120)}");
        sb.AppendLine("  Texture metadata resolution: " + string.Join("; ", resolver.Outcomes.OrderByDescending(p => p.Value).Select(p => $"{p.Key} {p.Value}")));
        foreach (string error in resolver.MetadataErrors) sb.AppendLine("    metadata error: " + error);
        foreach (string request in resolver.MissingMetadataRequests) sb.AppendLine("    unresolved texture: " + request);
        sb.AppendLine();

        AppendGroup(sb, "(a) EXACT KEY VIA COOKED COPY", a, 10, 5);
        AppendGroup(sb, "(b) UNIQUE-NAME MATCH, UNSHIPPED MATERIALS (less trusted)", b, 5, 3);

        sb.AppendLine("== COMPLETE DIFFERING-MATERIAL INVENTORY (not truncated examples) ==");
        foreach (var row in a.DifferentMaterialInventory.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) sb.AppendLine("  exact: " + row);
        foreach (var row in b.DifferentMaterialInventory.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) sb.AppendLine("  unique-name: " + row);
        sb.AppendLine("== COMPLETE ORDERED-UNIFORM DIFFERENCE INVENTORY (one reason per mismatching root) ==");
        foreach (var row in a.UniformDifferenceInventory.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) sb.AppendLine("  exact: " + row);
        foreach (var row in b.UniformDifferenceInventory.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) sb.AppendLine("  unique-name: " + row);

        sb.AppendLine("== SHIPPING SHADER DXBC CHUNKS (first shipping shader compared) ==");
        if (first.Shipping != null)
        {
            sb.AppendLine("Shader: " + first.ShippingName);
            DescribeChunks(sb, "shipping", first.Shipping);
        }
        else sb.AppendLine("No shipping shader was fetched in this run.");
        if (first.Ours != null) DescribeChunks(sb, "ours", first.Ours);
        return sb.ToString();
    }

    private static void AppendGroup(StringBuilder sb, string title, Stats s, int topFailures, int topDiffs)
    {
        sb.AppendLine($"=========== {title} ===========");
        sb.AppendLine($"Materials: {s.Materials}; harness exceptions {s.Exceptions}; material-binary errors {s.ResourceBinaryErrors}");
        sb.AppendLine($"  translate failed: {s.TranslateFailed}; plan failed: {s.PlanFailed}; planned & compiled: {s.FullyCompiled + s.CompilePartial} (all jobs compiled {s.FullyCompiled}, some job failed {s.CompilePartial})");
        sb.AppendLine($"  uniform arrays: match {s.UniformMatch}, mismatch {s.UniformMismatch}, not comparable {s.UniformNotComparable}");
        sb.AppendLine($"  required set present (plan subset of shipping): {s.PlanSubsetOfShipping}; plan-only permutations {s.UnexpectedPermutations}");
        sb.AppendLine($"  membership: match {s.MembershipMatch}, mismatch {s.MembershipMismatch} (shipping-only permutations {s.MissingPermutations}; plan-only {s.UnexpectedPermutations})");
        sb.AppendLine($"  materials with every job byte-identical {s.MaterialsAllJobsMatchBytes}; every job identical incl. disassembly {s.MaterialsAllJobsMatchDisasm}; with >=1 different job {s.MaterialsWithAnyDifferent}");
        sb.AppendLine($"  per-job bytecode ({s.JobsTotal} jobs): " + string.Join(", ", Enum.GetValues<JobKind>().Select(k => $"{k} {s.JobKinds.GetValueOrDefault(k)}")));
        sb.AppendLine($"  instruction counts over compared pairs: equal {s.InstrEqual}, ours fewer {s.InstrOursLess}, ours more {s.InstrOursMore}; different pairs with same opcode sequence {s.DiffSameOpcodes}");
        sb.AppendLine("  Top failure groups (materials; compile = jobs):");
        WriteGroups(sb, s.Failures, topFailures);
        sb.AppendLine("  Membership mismatch examples:");
        foreach (var e in s.MembershipExamples) sb.AppendLine("    " + Trunc(e, 330));
        sb.AppendLine("    top shipping-only: " + string.Join("; ", s.MissingByType.OrderByDescending(p => p.Value).Take(4).Select(p => $"{p.Key} x{p.Value}")));
        sb.AppendLine("    top plan-only: " + string.Join("; ", s.UnexpectedByType.OrderByDescending(p => p.Value).Take(4).Select(p => $"{p.Key} x{p.Value}")));
        sb.AppendLine("  Uniform mismatch patterns:");
        foreach (var e in s.UniformExamples.Take(2)) sb.AppendLine("    " + Trunc(e, 260));
        WriteGroups(sb, s.UniformDiffPatterns, 5);
        sb.AppendLine("  'Different' bytecode - most common first-differing-line patterns:");
        WriteGroups(sb, s.DiffPatterns, topDiffs, 3);
        sb.AppendLine();
    }

    private static void WriteGroups(StringBuilder sb, Dictionary<string, Group> groups, int top, int maxExampleLen = 1)
    {
        if (groups.Count == 0) { sb.AppendLine("  (none)"); return; }
        foreach (var g in groups.OrderByDescending(p => p.Value.Count).Take(top))
        {
            sb.AppendLine($"  {g.Value.Count,6}x {Trunc(g.Key, 230)}");
            sb.AppendLine($"          e.g. {Trunc(g.Value.Example, 260)}");
        }
        if (groups.Count > top) sb.AppendLine($"  ... and {groups.Count - top} more groups");
    }

    // ---- misc helpers -----------------------------------------------------------------------------

    /// <summary>Plan errors look like "ShaderType / VertexFactory: reason"; group by reason only.</summary>
    private static string StripIdentity(string message)
    {
        int i = message.IndexOf(": ", StringComparison.Ordinal);
        return i > 0 && message[..i].Contains(" / ", StringComparison.Ordinal) ? message[(i + 2)..] : message;
    }

    private static string Describe(ShaderPermutationIdentity p) => p.ShaderTypeName + (p.VertexFactoryTypeName == null ? "" : "/" + p.VertexFactoryTypeName);

    private static void AddMs(Stats s, string phase, Stopwatch sw) => s.PhaseMs[phase] = s.PhaseMs.GetValueOrDefault(phase) + sw.ElapsedMilliseconds;
    private static void Bump(Dictionary<string, int> d, string k) => d[k] = d.GetValueOrDefault(k) + 1;

    private static void AddGroup(Dictionary<string, Group> d, string key, string example)
    {
        if (!d.TryGetValue(key, out var g)) d[key] = g = new Group { Example = example };
        g.Count++;
    }

    private static int EnvInt(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out int v) && v > 0 ? v : fallback;

    private static string Trunc(string s, int n) => s == null ? "" : s.Length <= n ? s : s[..n] + "...";
    private static string FirstLine(string s) => (s ?? "").Split('\n')[0].Trim();
    private static string Sanitize(string s) => Regex.Replace(s ?? "", @"[^\w\-]", "_");

    private static string FirstErrorLine(string diagnostics)
    {
        if (string.IsNullOrWhiteSpace(diagnostics)) return "compile failed without diagnostics";
        var lines = diagnostics.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        return lines.FirstOrDefault(l => l.Contains("error", StringComparison.OrdinalIgnoreCase)) ?? lines[0];
    }

    private static readonly Regex WinPathRx = new(@"[A-Za-z]:[\\/][^\s'""]*", RegexOptions.Compiled);
    private static readonly Regex QuotedRx = new(@"'[^']*'|""[^""]*""|`[^`]*`", RegexOptions.Compiled);
    private static readonly Regex DottedRx = new(@"\b[\w$]+(?:\.[\w$]+)+\b", RegexOptions.Compiled);
    private static readonly Regex FileLineRx = new(@"\b[\w.\-]+\.(usf|hlsl|fx)\(\d+(,\d+(-\d+)?)?\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex InstancedRx = new(@"\b\w+_\d+\b", RegexOptions.Compiled);
    private static readonly Regex NumRx = new(@"\b\d+\b", RegexOptions.Compiled);

    /// <summary>Strips paths, numbers and names so equivalent causes group together.</summary>
    private static string Normalize(string message)
    {
        if (string.IsNullOrEmpty(message)) return "(empty)";
        string m = FirstLine(message);
        m = FileLineRx.Replace(m, "<file>(#)");
        m = WinPathRx.Replace(m, "<path>");
        m = QuotedRx.Replace(m, "'<n>'");
        m = DottedRx.Replace(m, "<path>");
        m = InstancedRx.Replace(m, "<name>");
        m = NumRx.Replace(m, "#");
        return Trunc(m, 200);
    }
}
