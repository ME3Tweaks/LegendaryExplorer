using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LegendaryExplorer.Tools.MaterialShaderCompilation;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Compiler = LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorer.Tests.Tools.MaterialShaderCompilation;

[TestClass]
public class MaterialShaderCompilationServiceTests
{
    #region Test infrastructure

    private static readonly object InitLock = new();
    private static bool initialized;

    private static void InitLib()
    {
        lock (InitLock)
        {
            if (initialized) return;
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            LegendaryExplorerCoreLib.InitLib(TaskScheduler.FromCurrentSynchronizationContext(), _ => { });
            initialized = true;
        }
    }

    [TestInitialize]
    public void Setup() => InitLib();

    private static ExportEntry CreateMaterial(MEGame game = MEGame.LE3, string className = "Material", IMEPackage package = null)
    {
        package ??= MEPackageHandler.CreateMemoryEmptyPackage("MaterialShaderCompilationServiceTests.pcc", game);
        return package.CreateExport("TestMaterial", className, indexed: false);
    }

    private static LE3ShaderSourceSnapshot Snapshot(string text = "x") =>
        new(new Dictionary<string, string> { ["MaterialTemplate.usf"] = text });

    /// <summary>Synchronous progress sink (Progress&lt;T&gt; posts asynchronously and would race assertions).</summary>
    private sealed class SyncProgress : IProgress<MaterialShaderCompileProgress>
    {
        public readonly List<MaterialShaderCompileProgress> Reports = [];
        public void Report(MaterialShaderCompileProgress value) => Reports.Add(value);
    }

    private sealed class FakeSession : IMaterialShaderCompilerSession
    {
        public readonly List<string> Calls = [];
        public IReadOnlyList<string> Targets = ["A", "B", "C"];
        public Func<MaterialShaderStepResult> OnTranslate = () => Ok();
        public Func<MaterialShaderStepResult> OnPlan = () => Ok();
        public Func<int, MaterialShaderStepResult> OnCompileJob = _ => Ok();
        public Func<MaterialShaderStepResult> OnAssemble = () => Ok();
        public bool CompleteShaderSet;
        private bool planned;

        public MaterialShaderStepResult Translate() { Calls.Add("Translate"); return OnTranslate(); }

        public MaterialShaderStepResult Plan()
        {
            Calls.Add("Plan");
            var result = OnPlan();
            planned = result.Success;
            return result;
        }

        // Mirrors the production session: no planned targets exist until a plan succeeds.
        public IReadOnlyList<string> PlannedTargets => planned ? Targets : null;

        public MaterialShaderStepResult CompileJob(int jobIndex) { Calls.Add($"CompileJob({jobIndex})"); return OnCompileJob(jobIndex); }
        public MaterialShaderStepResult Assemble() { Calls.Add("Assemble"); return OnAssemble(); }
        public bool IsCompleteShaderSet => CompleteShaderSet;
        public Compiler.TranslationResult Translation => null;
        public Compiler.ShaderCompilePlan CompilePlan => null;
        public IReadOnlyList<Compiler.ShaderCompilationResult> CompiledShaders => null;
        public Compiler.ShaderCacheFragmentResult Fragment => null;
    }

    private sealed class FakeBackend : IMaterialShaderCompilerBackend
    {
        public readonly FakeSession Session = new();
        public int CreateSessionCalls;
        public MaterialShaderCompileRequest LastRequest;
        public Compiler.ResolvedMaterialCompilationInput LastInput;
        public LE3ShaderSourceSnapshot LastSources;
        public Func<IMaterialShaderCompilerSession> OnCreate;

        public IMaterialShaderCompilerSession CreateSession(MaterialShaderCompileRequest request,
            Compiler.ResolvedMaterialCompilationInput input, LE3ShaderSourceSnapshot sources)
        {
            CreateSessionCalls++;
            LastRequest = request; LastInput = input; LastSources = sources;
            return OnCreate != null ? OnCreate() : Session;
        }
    }

    private static MaterialShaderStepResult Ok(string diagnostics = null) => new(true, [], diagnostics);
    private static MaterialShaderStepResult Fail(params string[] errors) => new(false, errors);

    /// <summary>Counts callbacks so tests can prove nothing was touched.</summary>
    private sealed class Probe
    {
        public int SourceProviderCalls, MetadataCalls;
        public LE3ShaderSourceSnapshot Snapshot = MaterialShaderCompilationServiceTests.Snapshot();
    }

    private static MaterialShaderCompileRequest Request(ExportEntry material, Probe probe, bool resource = true,
        Guid? resourceId = null, string friendlyName = "TestMaterial", bool sourceProvider = true, bool metadata = true,
        IEntry engineDefaultTexture = null) => new()
    {
        Material = material,
        Resource = resource ? MaterialResource.Create() : null,
        ResourceId = resourceId ?? Guid.NewGuid(),
        FriendlyName = friendlyName,
        EngineDefaultTexture = engineDefaultTexture,
        SourceProvider = sourceProvider ? () => { probe.SourceProviderCalls++; return probe.Snapshot; } : null,
        GetTextureMetadata = metadata ? _ => { probe.MetadataCalls++; return null; } : null,
    };

    private static MaterialShaderCompilationService Enabled(FakeBackend backend) =>
        new(new MaterialShaderCompilationOptions { Enabled = true }, backend);

    #endregion

    #region Gate

    [TestMethod]
    public async Task DefaultConstructedService_IsDisabled_AndTouchesNothing()
    {
        var probe = new Probe();
        var service = new MaterialShaderCompilationService();
        var request = Request(CreateMaterial(), probe);

        var result = await service.CompileMaterialAsync(request);

        Assert.AreEqual(MaterialShaderCompileOutcome.Disabled, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.Gate, result.Phase);
        Assert.AreEqual(0, probe.SourceProviderCalls);
        Assert.AreEqual(0, probe.MetadataCalls);
        Assert.IsFalse(result.IsZeroJobPlan);
        Assert.IsFalse(result.IsCompleteShaderSet);
        Assert.AreEqual(0, result.Jobs.Count);
        Assert.IsNull(result.Exception);
        Assert.IsFalse(result.IsBackendUnavailable);
        Assert.AreEqual(1, result.Errors.Count);
    }

    [TestMethod]
    public void OptionsEnabled_DefaultsToFalse() => Assert.IsFalse(new MaterialShaderCompilationOptions().Enabled);

    [TestMethod]
    public async Task ExplicitDisabled_DoesNotCallBackendSourceProviderOrMetadata()
    {
        var probe = new Probe();
        var backend = new FakeBackend();
        var service = new MaterialShaderCompilationService(new MaterialShaderCompilationOptions { Enabled = false }, backend);

        var result = await service.CompileMaterialAsync(Request(CreateMaterial(), probe));

        Assert.AreEqual(MaterialShaderCompileOutcome.Disabled, result.Outcome);
        Assert.AreEqual(0, backend.CreateSessionCalls);
        Assert.AreEqual(0, backend.Session.Calls.Count);
        Assert.AreEqual(0, probe.SourceProviderCalls);
        Assert.AreEqual(0, probe.MetadataCalls);
    }

    [TestMethod]
    public async Task Disabled_WithNullRequest_IsStillDisabledNotFailed()
    {
        var result = await new MaterialShaderCompilationService(new MaterialShaderCompilationOptions { Enabled = false }, new FakeBackend())
            .CompileMaterialAsync(null);
        Assert.AreEqual(MaterialShaderCompileOutcome.Disabled, result.Outcome);
    }

    [TestMethod]
    public async Task Disabled_WithCancelledToken_IsStillDisabled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await new MaterialShaderCompilationService().CompileMaterialAsync(Request(CreateMaterial(), new Probe()), null, cts.Token);
        Assert.AreEqual(MaterialShaderCompileOutcome.Disabled, result.Outcome);
    }

    #endregion

    #region Validation

    [TestMethod]
    public async Task NullRequest_FailsInValidation()
    {
        var backend = new FakeBackend();
        var result = await Enabled(backend).CompileMaterialAsync(null);
        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.Validation, result.Phase);
        Assert.AreEqual(0, backend.CreateSessionCalls);
        Assert.AreEqual(1, result.Errors.Count);
    }

    [TestMethod]
    public async Task NullMaterial_FailsInValidation()
    {
        var probe = new Probe();
        var backend = new FakeBackend();
        var result = await Enabled(backend).CompileMaterialAsync(Request(null, probe));
        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.Validation, result.Phase);
        Assert.AreEqual(0, probe.SourceProviderCalls);
        Assert.AreEqual(0, backend.CreateSessionCalls);
    }

    [TestMethod]
    [DataRow(MEGame.LE2)]
    [DataRow(MEGame.ME3)]
    [DataRow(MEGame.LE1)]
    public async Task NonLE3Material_IsUnsupported(MEGame game)
    {
        var probe = new Probe();
        var backend = new FakeBackend();
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(game), probe));
        Assert.AreEqual(MaterialShaderCompileOutcome.Unsupported, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.Validation, result.Phase);
        Assert.AreEqual(0, probe.SourceProviderCalls);
        Assert.AreEqual(0, backend.CreateSessionCalls);
    }

    [TestMethod]
    public async Task MaterialInstanceConstant_IsUnsupported_WithoutBackendCall()
    {
        var probe = new Probe();
        var backend = new FakeBackend();
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(className: "MaterialInstanceConstant"), probe));
        Assert.AreEqual(MaterialShaderCompileOutcome.Unsupported, result.Outcome);
        Assert.AreEqual(0, backend.CreateSessionCalls);
        Assert.AreEqual(0, probe.SourceProviderCalls);
        Assert.AreEqual("MaterialInstance compilation requires an instance resolver, and none is configured.", result.Errors.Single());
        Assert.IsNull(result.InstanceResolution);
    }

    [TestMethod]
    public async Task NonMaterialExport_IsUnsupported()
    {
        var probe = new Probe();
        var backend = new FakeBackend();
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(className: "Texture2D"), probe));
        Assert.AreEqual(MaterialShaderCompileOutcome.Unsupported, result.Outcome);
        Assert.AreEqual(0, backend.CreateSessionCalls);
        Assert.AreEqual(0, probe.SourceProviderCalls);
    }

    [TestMethod]
    public async Task MissingResource_FailsInValidation()
    {
        var probe = new Probe();
        var backend = new FakeBackend();
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), probe, resource: false));
        AssertValidationFailure(result, probe, backend);
    }

    [TestMethod]
    public async Task EmptyResourceId_FailsInValidation()
    {
        var probe = new Probe();
        var backend = new FakeBackend();
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), probe, resourceId: Guid.Empty));
        AssertValidationFailure(result, probe, backend);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public async Task EmptyFriendlyName_FailsInValidation(string name)
    {
        var probe = new Probe();
        var backend = new FakeBackend();
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), probe, friendlyName: name));
        AssertValidationFailure(result, probe, backend);
    }

    [TestMethod]
    public async Task NullSourceProvider_FailsInValidation()
    {
        var probe = new Probe();
        var backend = new FakeBackend();
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), probe, sourceProvider: false));
        AssertValidationFailure(result, probe, backend);
    }

    [TestMethod]
    public async Task NullTextureMetadataProvider_FailsInValidation()
    {
        var probe = new Probe();
        var backend = new FakeBackend();
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), probe, metadata: false));
        AssertValidationFailure(result, probe, backend);
    }

    private static void AssertValidationFailure(MaterialShaderCompileResult result, Probe probe, FakeBackend backend)
    {
        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.Validation, result.Phase);
        Assert.AreEqual(1, result.Errors.Count);
        Assert.IsNull(result.Exception);
        Assert.AreEqual(0, probe.SourceProviderCalls);
        Assert.AreEqual(0, backend.CreateSessionCalls);
    }

    [TestMethod]
    public async Task DecalMaterialBase_IsAccepted()
    {
        var probe = new Probe();
        var backend = new FakeBackend();
        var material = CreateMaterial(className: "DecalMaterial");
        var result = await Enabled(backend).CompileMaterialAsync(Request(material, probe));
        Assert.AreEqual(MaterialShaderCompileOutcome.SucceededFragment, result.Outcome);
        Assert.AreSame(material, backend.LastInput.BaseMaterial);
    }

    [TestMethod]
    public async Task EngineDefaultTextureFromAnotherPackage_FailsInValidation_WithException()
    {
        var probe = new Probe();
        var backend = new FakeBackend();
        using var other = MEPackageHandler.CreateMemoryEmptyPackage("Other.pcc", MEGame.LE3);
        var foreignTexture = other.CreateExport("Tex", "Texture2D", indexed: false);

        var result = await Enabled(backend).CompileMaterialAsync(
            Request(CreateMaterial(), probe, engineDefaultTexture: foreignTexture));

        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.Validation, result.Phase);
        Assert.IsInstanceOfType<ArgumentException>(result.Exception);
        Assert.IsFalse(result.IsBackendUnavailable);
        Assert.AreEqual(0, probe.SourceProviderCalls);
        Assert.AreEqual(0, backend.CreateSessionCalls);
    }

    [TestMethod]
    public async Task EngineDefaultTextureInSamePackage_IsAccepted()
    {
        var probe = new Probe();
        var backend = new FakeBackend();
        var material = CreateMaterial();
        var texture = material.FileRef.CreateExport("DefaultTex", "Texture2D", indexed: false);
        var result = await Enabled(backend).CompileMaterialAsync(Request(material, probe, engineDefaultTexture: texture));
        Assert.AreEqual(MaterialShaderCompileOutcome.SucceededFragment, result.Outcome);
        Assert.AreSame(texture, backend.LastInput.EngineDefaultTexture);
    }

    #endregion

    #region Source capture

    [TestMethod]
    public async Task SourceProvider_InvokedOnce_AndSameSnapshotPassedToSession()
    {
        var probe = new Probe();
        var backend = new FakeBackend();
        var request = Request(CreateMaterial(), probe);

        var result = await Enabled(backend).CompileMaterialAsync(request);

        Assert.AreEqual(MaterialShaderCompileOutcome.SucceededFragment, result.Outcome);
        Assert.AreEqual(1, probe.SourceProviderCalls);
        Assert.AreEqual(1, backend.CreateSessionCalls);
        Assert.AreSame(probe.Snapshot, backend.LastSources);
        Assert.AreSame(request, backend.LastRequest);
        Assert.AreSame(request.Material, backend.LastInput.BaseMaterial);
        Assert.AreEqual(0, probe.MetadataCalls, "The service itself must not call the texture metadata provider.");
    }

    [TestMethod]
    public async Task NullSnapshot_FailsInSourceCapture_WithoutSession()
    {
        var probe = new Probe { Snapshot = null };
        var backend = new FakeBackend();
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), probe));
        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.SourceCapture, result.Phase);
        Assert.AreEqual(1, probe.SourceProviderCalls);
        Assert.AreEqual(0, backend.CreateSessionCalls);
        Assert.AreEqual(1, result.Errors.Count);
    }

    [TestMethod]
    public async Task SourceProviderThrows_FailsInSourceCapture_WithException()
    {
        var backend = new FakeBackend();
        var request = Request(CreateMaterial(), new Probe());
        var boom = new InvalidOperationException("no sources");
        var failing = new MaterialShaderCompileRequest
        {
            Material = request.Material, Resource = request.Resource, ResourceId = request.ResourceId,
            FriendlyName = request.FriendlyName, GetTextureMetadata = request.GetTextureMetadata,
            SourceProvider = () => throw boom
        };
        var result = await Enabled(backend).CompileMaterialAsync(failing);
        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.SourceCapture, result.Phase);
        Assert.AreSame(boom, result.Exception);
        Assert.AreEqual(0, backend.CreateSessionCalls);
    }

    [TestMethod]
    public async Task CreateSessionThrows_FailsInSourceCapture_WithException()
    {
        var backend = new FakeBackend { OnCreate = () => throw new InvalidOperationException("session") };
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()));
        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.SourceCapture, result.Phase);
        Assert.IsInstanceOfType<InvalidOperationException>(result.Exception);
    }

    #endregion

    #region Phase errors (returned, not thrown)

    [TestMethod]
    public async Task TranslationFailure_ReturnsErrors_AndSkipsPlan()
    {
        var backend = new FakeBackend();
        backend.Session.OnTranslate = () => Fail("bad expression", "bad input");
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()));

        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.Translation, result.Phase);
        CollectionAssert.AreEqual(new[] { "bad expression", "bad input" }, result.Errors.ToArray());
        CollectionAssert.AreEqual(new[] { "Translate" }, backend.Session.Calls);
        Assert.IsNull(result.Exception);
        Assert.IsFalse(result.IsZeroJobPlan);
        Assert.AreEqual(0, result.SelectedJobCount);
    }

    [TestMethod]
    public async Task PlanFailure_ReturnsErrors_AndCompilesNoJobs()
    {
        var backend = new FakeBackend();
        backend.Session.OnPlan = () => Fail("unresolved ShouldCache");
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()));

        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.Planning, result.Phase);
        CollectionAssert.AreEqual(new[] { "unresolved ShouldCache" }, result.Errors.ToArray());
        CollectionAssert.AreEqual(new[] { "Translate", "Plan" }, backend.Session.Calls);
        Assert.AreEqual(0, result.Jobs.Count);
        Assert.IsFalse(result.IsZeroJobPlan, "A failed plan is not a zero-job plan.");
        Assert.AreEqual(0, result.SelectedJobCount);
    }

    [TestMethod]
    public async Task FailedJob_StopsLaterJobs_RetainsEarlierReports_AndSetsFailedTarget()
    {
        var backend = new FakeBackend();
        backend.Session.OnCompileJob = i => i == 1 ? Fail("hlsl error") : Ok("warn0");
        var progress = new SyncProgress();
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()), progress);

        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.Compilation, result.Phase);
        Assert.AreEqual("B", result.FailedTarget);
        CollectionAssert.AreEqual(new[] { "hlsl error" }, result.Errors.ToArray());
        CollectionAssert.AreEqual(new[] { "Translate", "Plan", "CompileJob(0)", "CompileJob(1)" }, backend.Session.Calls);
        Assert.AreEqual(2, result.Jobs.Count);
        Assert.AreEqual(new MaterialShaderJobReport(0, "A", true, "warn0"), result.Jobs[0]);
        Assert.IsFalse(result.Jobs[1].Success);
        Assert.AreEqual("B", result.Jobs[1].Target);
        CollectionAssert.AreEqual(new[] { "A", "B", "C" }, result.PlannedTargets.ToArray());
        // (0,N) and (1,N) only: no completion report for the failed job.
        Assert.AreEqual(2, progress.Reports.Count);
    }

    [TestMethod]
    public async Task FailedJobWithoutErrors_GetsDefaultMessageNamingTarget()
    {
        var backend = new FakeBackend();
        backend.Session.OnCompileJob = i => i == 0 ? new MaterialShaderStepResult(false, [], null) : Ok();
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()));
        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.AreEqual("A", result.FailedTarget);
        StringAssert.Contains(result.Errors.Single(), "A");
    }

    [TestMethod]
    public async Task FailedFirstJob_FailedTargetIsFirstTarget_AndOnlyFailedReportRetained()
    {
        var backend = new FakeBackend();
        backend.Session.OnCompileJob = _ => Fail("nope");
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()));
        Assert.AreEqual("A", result.FailedTarget);
        Assert.AreEqual(1, result.Jobs.Count);
        Assert.IsFalse(result.Jobs[0].Success);
    }

    [TestMethod]
    public async Task AssemblyFailure_ReturnsErrors_AtAssemblyPhase_WithAllJobsRetained()
    {
        var backend = new FakeBackend();
        backend.Session.OnAssemble = () => Fail("cache build failed");
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()));

        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.Assembly, result.Phase);
        CollectionAssert.AreEqual(new[] { "cache build failed" }, result.Errors.ToArray());
        Assert.IsNull(result.FailedTarget);
        Assert.AreEqual(3, result.Jobs.Count);
        Assert.IsNull(result.Exception);
    }

    #endregion

    #region Exceptions versus returned errors

    [TestMethod]
    public async Task ExceptionInTranslate_RecordsPhaseAndException()
    {
        var boom = new InvalidOperationException("translate boom");
        var backend = new FakeBackend();
        backend.Session.OnTranslate = () => throw boom;
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()));
        AssertException(result, MaterialShaderCompilePhase.Translation, boom);
    }

    [TestMethod]
    public async Task ExceptionInPlan_RecordsPhaseAndException()
    {
        var boom = new InvalidOperationException("plan boom");
        var backend = new FakeBackend();
        backend.Session.OnPlan = () => throw boom;
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()));
        AssertException(result, MaterialShaderCompilePhase.Planning, boom);
    }

    [TestMethod]
    public async Task ExceptionInCompileJob_RecordsPhaseTargetAndEarlierReports()
    {
        var boom = new InvalidOperationException("job boom");
        var backend = new FakeBackend();
        backend.Session.OnCompileJob = i => i == 1 ? throw boom : Ok();
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()));
        AssertException(result, MaterialShaderCompilePhase.Compilation, boom);
        Assert.AreEqual("B", result.FailedTarget);
        Assert.AreEqual(1, result.Jobs.Count);
        Assert.AreEqual(0, result.Jobs[0].JobIndex);
    }

    [TestMethod]
    public async Task ExceptionInAssemble_RecordsPhaseAndException()
    {
        var boom = new InvalidOperationException("assemble boom");
        var backend = new FakeBackend();
        backend.Session.OnAssemble = () => throw boom;
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()));
        AssertException(result, MaterialShaderCompilePhase.Assembly, boom);
        Assert.AreEqual(3, result.Jobs.Count);
    }

    private static void AssertException(MaterialShaderCompileResult result, MaterialShaderCompilePhase phase, Exception expected)
    {
        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.AreEqual(phase, result.Phase);
        Assert.AreSame(expected, result.Exception);
        Assert.IsFalse(result.IsBackendUnavailable);
        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], expected.Message);
        StringAssert.Contains(result.Errors[0], phase.ToString());
    }

    public static IEnumerable<object[]> BackendLoadFailures()
    {
        yield return [new DllNotFoundException("d3dcompiler")];
        yield return [new BadImageFormatException("wrong arch")];
        yield return [new EntryPointNotFoundException("D3DCompile")];
        yield return [new TypeInitializationException("Native", new DllNotFoundException("d3dcompiler"))];
        yield return [new TypeInitializationException("Outer", new TypeInitializationException("Inner", new DllNotFoundException("d3dcompiler")))];
    }

    [TestMethod]
    [DynamicData(nameof(BackendLoadFailures))]
    public async Task BackendLoadFailure_FlagsBackendUnavailable(Exception exception)
    {
        var backend = new FakeBackend();
        backend.Session.OnCompileJob = _ => throw exception;
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()));
        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.IsTrue(result.IsBackendUnavailable);
        Assert.AreSame(exception, result.Exception);
        Assert.AreEqual(MaterialShaderCompilePhase.Compilation, result.Phase);
    }

    [TestMethod]
    public async Task BackendLoadFailure_DuringSessionCreation_FlagsBackendUnavailable()
    {
        var backend = new FakeBackend { OnCreate = () => throw new DllNotFoundException("d3dcompiler") };
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()));
        Assert.IsTrue(result.IsBackendUnavailable);
        Assert.AreEqual(MaterialShaderCompilePhase.SourceCapture, result.Phase);
    }

    [TestMethod]
    public async Task TypeInitializationException_WithUnrelatedInner_IsNotBackendUnavailable()
    {
        var backend = new FakeBackend();
        backend.Session.OnTranslate = () => throw new TypeInitializationException("X", new InvalidOperationException("other"));
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()));
        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.IsFalse(result.IsBackendUnavailable);
        Assert.IsNotNull(result.Exception);
    }

    [TestMethod]
    public async Task ReturnedErrors_HaveNoExceptionAndAreNotBackendUnavailable()
    {
        var backend = new FakeBackend();
        backend.Session.OnTranslate = () => Fail("material problem");
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()));
        Assert.IsNull(result.Exception);
        Assert.IsFalse(result.IsBackendUnavailable);
    }

    [TestMethod]
    public async Task OperationCanceledFromSession_WithTokenNotCancelled_IsFailureNotCancellation()
    {
        var backend = new FakeBackend();
        backend.Session.OnTranslate = () => throw new OperationCanceledException();
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()));
        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.IsInstanceOfType<OperationCanceledException>(result.Exception);
    }

    [TestMethod]
    public async Task OperationCanceledFromSession_WithTokenCancelled_IsCancelled()
    {
        using var cts = new CancellationTokenSource();
        var backend = new FakeBackend();
        backend.Session.OnTranslate = () => { cts.Cancel(); throw new OperationCanceledException(cts.Token); };
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()), null, cts.Token);
        Assert.AreEqual(MaterialShaderCompileOutcome.Cancelled, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.Translation, result.Phase);
    }

    #endregion

    #region Zero-job plan, success, warnings, progress

    [TestMethod]
    public async Task ZeroJobPlan_SucceedsAsFragment_FlaggedZeroJobAndIncomplete()
    {
        var backend = new FakeBackend();
        backend.Session.Targets = [];
        var progress = new SyncProgress();
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()), progress);

        Assert.AreEqual(MaterialShaderCompileOutcome.SucceededFragment, result.Outcome);
        Assert.AreNotEqual(MaterialShaderCompileOutcome.Disabled, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.Completed, result.Phase);
        Assert.IsTrue(result.IsZeroJobPlan);
        Assert.IsFalse(result.IsCompleteShaderSet);
        Assert.AreEqual(0, result.SelectedJobCount);
        Assert.AreEqual(0, result.Jobs.Count);
        CollectionAssert.AreEqual(new[] { "Translate", "Plan", "Assemble" }, backend.Session.Calls);
        CollectionAssert.AreEqual(new[] { new MaterialShaderCompileProgress(0, 0, null) }, progress.Reports);
    }

    [TestMethod]
    public async Task Success_ReportsFragmentOutcome_CompletedPhase_AndPlannedTargets()
    {
        var backend = new FakeBackend();
        backend.Session.Targets = ["VS / FactoryX", "PS / FactoryX"];
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()));

        Assert.AreEqual(MaterialShaderCompileOutcome.SucceededFragment, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.Completed, result.Phase);
        Assert.IsFalse(result.IsCompleteShaderSet);
        Assert.IsFalse(result.IsZeroJobPlan);
        Assert.IsNull(result.FailedTarget);
        Assert.IsNull(result.Exception);
        Assert.AreEqual(0, result.Errors.Count);
        Assert.AreEqual(2, result.SelectedJobCount);
        CollectionAssert.AreEqual(new[] { "VS / FactoryX", "PS / FactoryX" }, result.PlannedTargets.ToArray());
        Assert.AreEqual(2, result.Jobs.Count);
        Assert.IsTrue(result.Jobs.All(j => j.Success));
        CollectionAssert.AreEqual(new[] { "Translate", "Plan", "CompileJob(0)", "CompileJob(1)", "Assemble" }, backend.Session.Calls);
        Assert.IsNotNull(result.CompiledShaders);
    }

    [TestMethod]
    public async Task CompletenessIsReportedFromSession_NotInferredFromSuccess()
    {
        var backend = new FakeBackend();
        backend.Session.CompleteShaderSet = true;
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()));
        Assert.AreEqual(MaterialShaderCompileOutcome.SucceededFragment, result.Outcome);
        Assert.IsTrue(result.IsCompleteShaderSet);
    }

    [TestMethod]
    public async Task SuccessfulJobWarnings_AreRetainedInJobs()
    {
        var backend = new FakeBackend();
        backend.Session.OnCompileJob = i => i == 1 ? Ok("warning X1234: implicit truncation") : Ok();
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()));

        Assert.AreEqual(MaterialShaderCompileOutcome.SucceededFragment, result.Outcome);
        Assert.AreEqual(3, result.Jobs.Count);
        Assert.IsNull(result.Jobs[0].Diagnostics);
        Assert.AreEqual("warning X1234: implicit truncation", result.Jobs[1].Diagnostics);
        Assert.IsTrue(result.Jobs[1].Success);
        Assert.AreEqual("B", result.Jobs[1].Target);
        Assert.AreEqual(1, result.Jobs[1].JobIndex);
    }

    [TestMethod]
    public async Task Progress_ReportsZeroThenEachCompletedJob()
    {
        var backend = new FakeBackend();
        var progress = new SyncProgress();
        await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()), progress);

        CollectionAssert.AreEqual(new[]
        {
            new MaterialShaderCompileProgress(0, 3, null),
            new MaterialShaderCompileProgress(1, 3, "A"),
            new MaterialShaderCompileProgress(2, 3, "B"),
            new MaterialShaderCompileProgress(3, 3, "C"),
        }, progress.Reports);
    }

    [TestMethod]
    public async Task Progress_IsNotReportedWhenValidationFails()
    {
        var progress = new SyncProgress();
        await Enabled(new FakeBackend()).CompileMaterialAsync(null, progress);
        Assert.AreEqual(0, progress.Reports.Count);
    }

    [TestMethod]
    public async Task NullProgress_IsAccepted()
    {
        var result = await Enabled(new FakeBackend()).CompileMaterialAsync(Request(CreateMaterial(), new Probe()), null);
        Assert.AreEqual(MaterialShaderCompileOutcome.SucceededFragment, result.Outcome);
    }

    #endregion

    #region Cancellation

    [TestMethod]
    public async Task CancelledBeforeStart_IsCancelled_WithNoWork()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var probe = new Probe();
        var backend = new FakeBackend();
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), probe), null, cts.Token);

        Assert.AreEqual(MaterialShaderCompileOutcome.Cancelled, result.Outcome);
        Assert.AreEqual(0, probe.SourceProviderCalls);
        Assert.AreEqual(0, backend.CreateSessionCalls);
        Assert.AreEqual(0, result.Jobs.Count);
    }

    [TestMethod]
    public async Task CancelledDuringFirstJob_RetainsJobZeroReport_AndNeverCompilesJobOne()
    {
        using var cts = new CancellationTokenSource();
        var backend = new FakeBackend();
        backend.Session.OnCompileJob = i => { if (i == 0) cts.Cancel(); return Ok("w0"); };
        var progress = new SyncProgress();

        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()), progress, cts.Token);

        Assert.AreEqual(MaterialShaderCompileOutcome.Cancelled, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.Compilation, result.Phase);
        Assert.AreEqual(1, result.Jobs.Count);
        Assert.AreEqual(new MaterialShaderJobReport(0, "A", true, "w0"), result.Jobs[0]);
        CollectionAssert.AreEqual(new[] { "Translate", "Plan", "CompileJob(0)" }, backend.Session.Calls);
        Assert.IsNull(result.Exception);
        Assert.AreEqual(3, result.SelectedJobCount);
    }

    [TestMethod]
    public async Task CancelledDuringLastJob_IsCancelledAtAssembly_WithoutAssembling()
    {
        using var cts = new CancellationTokenSource();
        var backend = new FakeBackend();
        backend.Session.OnCompileJob = i => { if (i == 2) cts.Cancel(); return Ok(); };

        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()), null, cts.Token);

        Assert.AreEqual(MaterialShaderCompileOutcome.Cancelled, result.Outcome);
        Assert.AreEqual(3, result.Jobs.Count);
        CollectionAssert.DoesNotContain(backend.Session.Calls, "Assemble");
    }

    [TestMethod]
    public async Task CancelledDuringTranslate_StopsBeforePlan()
    {
        using var cts = new CancellationTokenSource();
        var backend = new FakeBackend();
        backend.Session.OnTranslate = () => { cts.Cancel(); return Ok(); };
        var result = await Enabled(backend).CompileMaterialAsync(Request(CreateMaterial(), new Probe()), null, cts.Token);
        Assert.AreEqual(MaterialShaderCompileOutcome.Cancelled, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.Planning, result.Phase);
        CollectionAssert.AreEqual(new[] { "Translate" }, backend.Session.Calls);
    }

    #endregion

    #region MaterialInstance resolution

    private sealed class FakeResolver : IMaterialShaderInstanceResolver
    {
        public int Calls;
        public ExportEntry LastRequested;
        public Func<ExportEntry, MaterialInstanceResolution> OnResolve;

        public MaterialInstanceResolution Resolve(ExportEntry materialInstance)
        {
            Calls++;
            LastRequested = materialInstance;
            return OnResolve(materialInstance);
        }
    }

    private static MaterialShaderCompilationService WithResolver(FakeBackend backend, IMaterialShaderInstanceResolver resolver) =>
        new(new MaterialShaderCompilationOptions { Enabled = true, InstanceResolver = resolver }, backend);

    private static MaterialShaderCompileRequest InstanceRequest(ExportEntry instance, Probe probe, bool resource = false) =>
        Request(instance, probe, resource: resource);

    /// <summary>A material, a plain parent chain mic -> material, and (optionally) an owner with a serialized permutation.</summary>
    private sealed class InstanceFixture : IDisposable
    {
        public readonly IMEPackage Package = LE3InstanceFixtures.NewPackage("InstanceService.pcc");
        public readonly Guid BaseId = Guid.NewGuid();
        public readonly Guid PermutationId = Guid.NewGuid();
        public readonly ExportEntry Material;
        public readonly ExportEntry Owner;
        public readonly ExportEntry Child;
        public readonly StaticParameterSet OwnerSet;

        public InstanceFixture()
        {
            Material = LE3InstanceFixtures.BaseMaterial(Package, resourceId: BaseId);
            OwnerSet = LE3InstanceFixtures.StaticSet(BaseId, "ServiceSwitch", true);
            Owner = LE3InstanceFixtures.Instance(Package, "Owner", Material, true, PermutationId, OwnerSet);
            Child = LE3InstanceFixtures.Instance(Package, "Child", Owner);
        }

        public void Dispose() => Package.Dispose();
    }

    [TestMethod]
    public async Task InstanceWithoutResolver_IsUnsupported_WithNoBackendOrSourceCall()
    {
        using var fixture = new InstanceFixture();
        var probe = new Probe();
        var backend = new FakeBackend();

        var result = await Enabled(backend).CompileMaterialAsync(InstanceRequest(fixture.Child, probe));

        Assert.AreEqual(MaterialShaderCompileOutcome.Unsupported, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.Validation, result.Phase);
        Assert.AreEqual("MaterialInstance compilation requires an instance resolver, and none is configured.", result.Errors.Single());
        Assert.AreEqual(0, backend.CreateSessionCalls);
        Assert.AreEqual(0, probe.SourceProviderCalls);
    }

    [TestMethod]
    public async Task InstanceWithoutResolver_EvenWhenOtherFieldsAreInvalid_ReportsUnsupportedFirst()
    {
        using var fixture = new InstanceFixture();
        var result = await Enabled(new FakeBackend()).CompileMaterialAsync(
            Request(fixture.Child, new Probe(), resourceId: Guid.Empty));
        Assert.AreEqual(MaterialShaderCompileOutcome.Unsupported, result.Outcome);
    }

    [TestMethod]
    [DataRow(true, MaterialShaderCompileOutcome.Unsupported)]
    [DataRow(false, MaterialShaderCompileOutcome.Failed)]
    public async Task ResolverFailure_MapsByIsUnsupported_InInstanceResolutionPhase(bool unsupported, MaterialShaderCompileOutcome expected)
    {
        using var fixture = new InstanceFixture();
        var failure = new MaterialInstanceResolution
        {
            RequestedInstance = fixture.Child, IsUnsupported = unsupported, Errors = ["resolver says no"]
        };
        var resolver = new FakeResolver { OnResolve = _ => failure };
        var probe = new Probe();
        var backend = new FakeBackend();

        var result = await WithResolver(backend, resolver).CompileMaterialAsync(InstanceRequest(fixture.Child, probe));

        Assert.AreEqual(expected, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.InstanceResolution, result.Phase);
        CollectionAssert.AreEqual(new[] { "resolver says no" }, result.Errors.ToArray());
        Assert.AreSame(failure, result.InstanceResolution);
        Assert.AreEqual(1, resolver.Calls);
        Assert.AreSame(fixture.Child, resolver.LastRequested);
        Assert.AreEqual(0, backend.CreateSessionCalls);
        Assert.AreEqual(0, probe.SourceProviderCalls);
        Assert.IsNull(result.Exception);
    }

    [TestMethod]
    public async Task ResolverReturnsNull_FailsInInstanceResolutionPhase()
    {
        using var fixture = new InstanceFixture();
        var backend = new FakeBackend();
        var result = await WithResolver(backend, new FakeResolver { OnResolve = _ => null })
            .CompileMaterialAsync(InstanceRequest(fixture.Child, new Probe()));
        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.InstanceResolution, result.Phase);
        Assert.AreEqual(0, backend.CreateSessionCalls);
    }

    [TestMethod]
    public async Task ResolverThrows_FailsInInstanceResolutionPhase_WithException()
    {
        using var fixture = new InstanceFixture();
        var boom = new InvalidOperationException("resolver boom");
        var result = await WithResolver(new FakeBackend(), new FakeResolver { OnResolve = _ => throw boom })
            .CompileMaterialAsync(InstanceRequest(fixture.Child, new Probe()));
        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.InstanceResolution, result.Phase);
        Assert.AreSame(boom, result.Exception);
    }

    [TestMethod]
    public async Task InstanceWithResolvedStaticParameters_FailsInValidation_WithoutCallingResolver()
    {
        using var fixture = new InstanceFixture();
        var resolver = new FakeResolver { OnResolve = _ => throw new AssertFailedException("resolver must not run") };
        var probe = new Probe();
        var backend = new FakeBackend();
        var request = InstanceRequest(fixture.Child, probe) with { ResolvedStaticParameters = LE3InstanceFixtures.StaticSet(fixture.BaseId) };

        var result = await WithResolver(backend, resolver).CompileMaterialAsync(request);

        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.Validation, result.Phase);
        Assert.AreEqual(1, result.Errors.Count);
        Assert.AreEqual(0, resolver.Calls);
        Assert.AreEqual(0, backend.CreateSessionCalls);
        Assert.AreEqual(0, probe.SourceProviderCalls);
        Assert.IsNull(result.Exception);
    }

    [TestMethod]
    public async Task InstanceRequest_StillValidatesCommonFields()
    {
        using var fixture = new InstanceFixture();
        var resolver = new FakeResolver { OnResolve = _ => throw new AssertFailedException("resolver must not run") };
        foreach (var request in new[]
        {
            Request(fixture.Child, new Probe(), resource: false, resourceId: Guid.Empty),
            Request(fixture.Child, new Probe(), resource: false, friendlyName: ""),
            Request(fixture.Child, new Probe(), resource: false, sourceProvider: false),
            Request(fixture.Child, new Probe(), resource: false, metadata: false),
        })
        {
            var result = await WithResolver(new FakeBackend(), resolver).CompileMaterialAsync(request);
            Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
            Assert.AreEqual(MaterialShaderCompilePhase.Validation, result.Phase);
        }
        Assert.AreEqual(0, resolver.Calls);
    }

    [TestMethod]
    public async Task InstanceWithoutResource_DefaultsToResolvedPermutationResource()
    {
        using var fixture = new InstanceFixture();
        var permutation = MaterialResource.Create();
        var resolution = new MaterialInstanceResolution
        {
            RequestedInstance = fixture.Child, Chain = [fixture.Child, fixture.Owner, fixture.Material], BaseMaterial = fixture.Material,
            PermutationOwner = fixture.Owner, EffectiveStaticParameters = fixture.OwnerSet, PermutationResource = permutation
        };
        var backend = new FakeBackend();

        var result = await WithResolver(backend, new FakeResolver { OnResolve = _ => resolution })
            .CompileMaterialAsync(InstanceRequest(fixture.Child, new Probe()));

        Assert.AreEqual(MaterialShaderCompileOutcome.SucceededFragment, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.Completed, result.Phase);
        Assert.AreSame(permutation, backend.LastRequest.Resource);
        Assert.AreSame(resolution, result.InstanceResolution);
        Assert.IsTrue(result.InstanceResolution.SharesAncestorPermutation);
    }

    [TestMethod]
    public async Task InstanceWithSuppliedResource_KeepsIt()
    {
        using var fixture = new InstanceFixture();
        var supplied = MaterialResource.Create();
        var resolution = new MaterialInstanceResolution
        {
            RequestedInstance = fixture.Child, Chain = [fixture.Child, fixture.Material], BaseMaterial = fixture.Material,
            PermutationResource = MaterialResource.Create()
        };
        var backend = new FakeBackend();
        var request = InstanceRequest(fixture.Child, new Probe()) with { Resource = supplied };

        var result = await WithResolver(backend, new FakeResolver { OnResolve = _ => resolution }).CompileMaterialAsync(request);

        Assert.AreEqual(MaterialShaderCompileOutcome.SucceededFragment, result.Outcome);
        Assert.AreSame(supplied, backend.LastRequest.Resource);
        Assert.AreNotSame(resolution.PermutationResource, backend.LastRequest.Resource);
    }

    [TestMethod]
    public async Task InstanceWithoutResource_AndNoResolvedResource_Fails()
    {
        using var fixture = new InstanceFixture();
        var resolution = new MaterialInstanceResolution
        {
            RequestedInstance = fixture.Child, Chain = [fixture.Child, fixture.Material], BaseMaterial = fixture.Material
        };
        var backend = new FakeBackend();

        var result = await WithResolver(backend, new FakeResolver { OnResolve = _ => resolution })
            .CompileMaterialAsync(InstanceRequest(fixture.Child, new Probe()));

        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.InstanceResolution, result.Phase);
        Assert.AreEqual(1, result.Errors.Count);
        Assert.AreEqual(0, backend.CreateSessionCalls);
    }

    [TestMethod]
    public async Task InstanceInput_CarriesBaseMaterial_RequestedInstance_AndOwnerStaticParameters()
    {
        using var fixture = new InstanceFixture();
        var backend = new FakeBackend();
        var probe = new Probe();

        var result = await WithResolver(backend, new LE3MaterialInstanceResolver())
            .CompileMaterialAsync(InstanceRequest(fixture.Child, probe));

        Assert.AreEqual(MaterialShaderCompileOutcome.SucceededFragment, result.Outcome, string.Join("; ", result.Errors));
        Assert.AreEqual(1, backend.CreateSessionCalls);
        Assert.AreEqual(1, probe.SourceProviderCalls);
        Assert.AreSame(fixture.Material, backend.LastInput.BaseMaterial);
        Assert.AreSame(fixture.Child, backend.LastInput.RequestedMaterialInstance);
        Assert.AreSame(fixture.Child, backend.LastInput.RequestedMaterial);
        var snapshot = backend.LastInput.GetStaticParametersSnapshot();
        Assert.IsNotNull(snapshot);
        Assert.IsTrue(fixture.OwnerSet.Equals(snapshot));
        Assert.AreEqual(1, snapshot.StaticSwitchParameters.Length);
        Assert.AreEqual("ServiceSwitch", snapshot.StaticSwitchParameters[0].ParameterName.Instanced);
        // Resource defaulted to the owner's serialized permutation resource.
        Assert.AreEqual(fixture.PermutationId, backend.LastRequest.Resource.ID);
        Assert.AreSame(probe.Snapshot, backend.LastSources);
        Assert.AreSame(fixture.Child, result.InstanceResolution.RequestedInstance);
        Assert.AreSame(fixture.Owner, result.InstanceResolution.PermutationOwner);
        Assert.IsTrue(result.InstanceResolution.SharesAncestorPermutation);
    }

    [TestMethod]
    public async Task InstanceWithoutOwner_UsesNullStaticParameters_AndBaseResource()
    {
        using var package = LE3InstanceFixtures.NewPackage();
        var baseId = Guid.NewGuid();
        var material = LE3InstanceFixtures.BaseMaterial(package, resourceId: baseId);
        var mic = LE3InstanceFixtures.Instance(package, "Mic", material);
        var backend = new FakeBackend();

        var result = await WithResolver(backend, new LE3MaterialInstanceResolver())
            .CompileMaterialAsync(InstanceRequest(mic, new Probe()));

        Assert.AreEqual(MaterialShaderCompileOutcome.SucceededFragment, result.Outcome, string.Join("; ", result.Errors));
        Assert.AreSame(material, backend.LastInput.BaseMaterial);
        Assert.AreSame(mic, backend.LastInput.RequestedMaterialInstance);
        Assert.IsNull(backend.LastInput.GetStaticParametersSnapshot());
        Assert.AreEqual(baseId, backend.LastRequest.Resource.ID);
    }

    [TestMethod]
    public async Task InstanceRequestWhoseOwnerIsRequested_IsNotSharedPermutation()
    {
        using var fixture = new InstanceFixture();
        var backend = new FakeBackend();

        var result = await WithResolver(backend, new LE3MaterialInstanceResolver())
            .CompileMaterialAsync(InstanceRequest(fixture.Owner, new Probe()));

        Assert.AreEqual(MaterialShaderCompileOutcome.SucceededFragment, result.Outcome, string.Join("; ", result.Errors));
        Assert.IsFalse(result.InstanceResolution.SharesAncestorPermutation);
        Assert.AreSame(fixture.Owner, backend.LastInput.RequestedMaterialInstance);
    }

    [TestMethod]
    public async Task RealResolverUnsupportedInstance_MapsToUnsupported_WithResolutionAttached()
    {
        using var package = LE3InstanceFixtures.NewPackage();
        var mic = LE3InstanceFixtures.Instance(package, "Mic", null); // no Parent
        var backend = new FakeBackend();

        var result = await WithResolver(backend, new LE3MaterialInstanceResolver())
            .CompileMaterialAsync(InstanceRequest(mic, new Probe()));

        Assert.AreEqual(MaterialShaderCompileOutcome.Unsupported, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.InstanceResolution, result.Phase);
        Assert.IsNotNull(result.InstanceResolution);
        Assert.IsTrue(result.InstanceResolution.IsUnsupported);
        Assert.AreEqual(0, backend.CreateSessionCalls);
    }

    [TestMethod]
    public async Task RealResolverCycle_MapsToFailed()
    {
        using var package = LE3InstanceFixtures.NewPackage();
        var a = LE3InstanceFixtures.Instance(package, "A", null);
        var b = LE3InstanceFixtures.Instance(package, "B", a);
        LE3InstanceFixtures.SetParent(a, b);

        var result = await WithResolver(new FakeBackend(), new LE3MaterialInstanceResolver())
            .CompileMaterialAsync(InstanceRequest(a, new Probe()));

        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.InstanceResolution, result.Phase);
        Assert.IsFalse(result.InstanceResolution.IsUnsupported);
    }

    [TestMethod]
    public async Task ResolutionWarnings_AreAvailableOnSuccessfulResult()
    {
        using var package = LE3InstanceFixtures.NewPackage();
        var material = LE3InstanceFixtures.BaseMaterial(package, resourceId: Guid.NewGuid());
        var mic = LE3InstanceFixtures.Instance(package, "Mic", material, true, Guid.NewGuid(), LE3InstanceFixtures.StaticSet(Guid.NewGuid()));

        var result = await WithResolver(new FakeBackend(), new LE3MaterialInstanceResolver())
            .CompileMaterialAsync(InstanceRequest(mic, new Probe()));

        Assert.AreEqual(MaterialShaderCompileOutcome.SucceededFragment, result.Outcome, string.Join("; ", result.Errors));
        Assert.AreEqual(1, result.InstanceResolution.Warnings.Count);
    }

    [TestMethod]
    public async Task BaseMaterialRequest_WithResolverConfigured_IsUnchanged_AndNeverResolves()
    {
        using var fixture = new InstanceFixture();
        var resolver = new FakeResolver { OnResolve = _ => throw new AssertFailedException("resolver must not run") };
        var backend = new FakeBackend();
        var probe = new Probe();
        var request = Request(fixture.Material, probe);

        var result = await WithResolver(backend, resolver).CompileMaterialAsync(request);

        Assert.AreEqual(MaterialShaderCompileOutcome.SucceededFragment, result.Outcome);
        Assert.AreEqual(0, resolver.Calls);
        Assert.IsNull(result.InstanceResolution);
        Assert.AreSame(request, backend.LastRequest);
        Assert.AreSame(fixture.Material, backend.LastInput.BaseMaterial);
        Assert.IsNull(backend.LastInput.RequestedMaterialInstance);
        Assert.IsNull(backend.LastInput.GetStaticParametersSnapshot());
    }

    [TestMethod]
    public async Task BaseMaterialRequest_StillRequiresResource_WhenResolverConfigured()
    {
        using var fixture = new InstanceFixture();
        var result = await WithResolver(new FakeBackend(), new LE3MaterialInstanceResolver())
            .CompileMaterialAsync(Request(fixture.Material, new Probe(), resource: false));
        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.Validation, result.Phase);
    }

    [TestMethod]
    public async Task BaseMaterialRequest_WithResolvedStaticParameters_IsStillAccepted()
    {
        using var fixture = new InstanceFixture();
        var backend = new FakeBackend();
        var request = Request(fixture.Material, new Probe()) with { ResolvedStaticParameters = fixture.OwnerSet };

        var result = await WithResolver(backend, new LE3MaterialInstanceResolver()).CompileMaterialAsync(request);

        Assert.AreEqual(MaterialShaderCompileOutcome.SucceededFragment, result.Outcome);
        Assert.IsTrue(fixture.OwnerSet.Equals(backend.LastInput.GetStaticParametersSnapshot()));
    }

    [TestMethod]
    public async Task InstanceRequest_DisabledGate_StillDoesNotResolve()
    {
        using var fixture = new InstanceFixture();
        var resolver = new FakeResolver { OnResolve = _ => throw new AssertFailedException("resolver must not run") };
        var service = new MaterialShaderCompilationService(
            new MaterialShaderCompilationOptions { Enabled = false, InstanceResolver = resolver }, new FakeBackend());

        var result = await service.CompileMaterialAsync(InstanceRequest(fixture.Child, new Probe()));

        Assert.AreEqual(MaterialShaderCompileOutcome.Disabled, result.Outcome);
        Assert.AreEqual(0, resolver.Calls);
    }

    [TestMethod]
    public async Task InstanceCancelledBeforeStart_DoesNotResolve()
    {
        using var fixture = new InstanceFixture();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var resolver = new FakeResolver { OnResolve = _ => throw new AssertFailedException("resolver must not run") };

        var result = await WithResolver(new FakeBackend(), resolver)
            .CompileMaterialAsync(InstanceRequest(fixture.Child, new Probe()), null, cts.Token);

        Assert.AreEqual(MaterialShaderCompileOutcome.Cancelled, result.Outcome);
        Assert.AreEqual(0, resolver.Calls);
    }

    [TestMethod]
    public async Task InstanceFailureLaterInPipeline_StillCarriesInstanceResolution()
    {
        using var fixture = new InstanceFixture();
        var backend = new FakeBackend();
        backend.Session.OnTranslate = () => Fail("bad graph");

        var result = await WithResolver(backend, new LE3MaterialInstanceResolver())
            .CompileMaterialAsync(InstanceRequest(fixture.Child, new Probe()));

        Assert.AreEqual(MaterialShaderCompileOutcome.Failed, result.Outcome);
        Assert.AreEqual(MaterialShaderCompilePhase.Translation, result.Phase);
        Assert.IsNotNull(result.InstanceResolution);
        Assert.AreSame(fixture.Owner, result.InstanceResolution.PermutationOwner);
    }

    #endregion

    #region Opt-in integration

    private const string IntegrationEnvPackage = "LE3_SHADER_IT_PACKAGE";
    private const string IntegrationEnvMaterial = "LE3_SHADER_IT_MATERIAL";
    private const string IntegrationEnvSources = "LE3_SHADER_IT_SOURCES";

    /// <summary>
    /// Explicitly opt-in, machine-local smoke test of the real backend. Reported as Inconclusive (skipped)
    /// unless LE3_SHADER_IT_PACKAGE (an LE3 package path), LE3_SHADER_IT_MATERIAL (instanced full path of a
    /// base Material in it) and LE3_SHADER_IT_SOURCES (a directory of LE3 .usf sources) are set. The package
    /// is opened read-only and never saved; texture metadata is a fixed 2D default.
    /// </summary>
    [TestMethod]
    public async Task Integration_RealBackend_CompilesAgainstInstalledGame()
    {
        string packagePath = Environment.GetEnvironmentVariable(IntegrationEnvPackage);
        string materialPath = Environment.GetEnvironmentVariable(IntegrationEnvMaterial);
        string sourcesDir = Environment.GetEnvironmentVariable(IntegrationEnvSources);
        if (string.IsNullOrEmpty(packagePath) || string.IsNullOrEmpty(materialPath) || string.IsNullOrEmpty(sourcesDir))
            Assert.Inconclusive($"Set {IntegrationEnvPackage}, {IntegrationEnvMaterial} and {IntegrationEnvSources} to run this opt-in test.");

        using var package = MEPackageHandler.OpenMEPackage(packagePath);
        var material = package.FindExport(materialPath);
        Assert.IsNotNull(material, $"Material {materialPath} not found.");
        var resource = ObjectBinary.From<Material>(material).SM3MaterialResource;

        var service = new MaterialShaderCompilationService(new MaterialShaderCompilationOptions { Enabled = true });
        var result = await service.CompileMaterialAsync(new MaterialShaderCompileRequest
        {
            Material = material,
            Resource = resource,
            ResourceId = Guid.NewGuid(),
            FriendlyName = material.ObjectName.Instanced,
            SourceProvider = () => LE3ShaderSourceSnapshot.FromDirectory(sourcesDir),
            GetTextureMetadata = _ => new Compiler.TextureCompilationMetadata(Compiler.TextureDimension.Texture2D),
        });

        Assert.AreEqual(MaterialShaderCompileOutcome.SucceededFragment, result.Outcome,
            string.Join("; ", result.Errors) + (result.Exception != null ? " | " + result.Exception : ""));
        // An ordinary material planned in the shipping context reports native required membership.
        Assert.AreEqual(result.Fragment.IsCompleteShaderSet, result.IsCompleteShaderSet);
        Assert.IsTrue(result.IsCompleteShaderSet);
    }

    #endregion
}
