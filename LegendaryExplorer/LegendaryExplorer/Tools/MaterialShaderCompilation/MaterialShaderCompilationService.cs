using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Compiler = LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorer.Tools.MaterialShaderCompilation
{
    // Host-side adapter for the offline LE3 material shader compiler. Production use is disabled
    // by default: there is no UI or setting that enables it. A successful outcome is a detached,
    // incomplete shader-cache fragment and never permission to install it into a package.

    public enum MaterialShaderCompileOutcome
    {
        /// <summary>The activation gate is off; nothing was read, compiled or mutated.</summary>
        Disabled,
        /// <summary>The request is outside the supported boundary (wrong game, unresolved instance, ...).</summary>
        Unsupported,
        Failed,
        Cancelled,
        /// <summary>A detached fragment was assembled. Check IsCompleteShaderSet; neither implies installability.</summary>
        SucceededFragment
    }

    public enum MaterialShaderCompilePhase
    {
        Gate,
        Validation,
        InstanceResolution,
        SourceCapture,
        Translation,
        Planning,
        Compilation,
        Assembly,
        Completed
    }

    public sealed class MaterialShaderCompilationOptions
    {
        /// <summary>Activation gate. Defaults to false and must stay false in production for now.</summary>
        public bool Enabled { get; init; }
        /// <summary>Optional MaterialInstance resolver. Without one, instance requests are Unsupported.</summary>
        public IMaterialShaderInstanceResolver InstanceResolver { get; init; }
    }

    public sealed record MaterialShaderCompileRequest
    {
        /// <summary>The requested export: an LE3 base Material (including DecalMaterial), or a MaterialInstance
        /// when an instance resolver is configured. For instances, default textures, metadata and font pages
        /// must refer to the resolved base Material's package.</summary>
        public ExportEntry Material { get; init; }
        /// <summary>Effective, already-merged static parameters for a base Material; null means authored defaults.
        /// Must be null for an instance request, whose static parameters come from resolution.</summary>
        public StaticParameterSet ResolvedStaticParameters { get; init; }
        /// <summary>Optional caller-resolved default texture identities in the base graph's package table.</summary>
        public IEntry EngineDefaultTexture { get; init; }
        public IEntry WorldDefaultTexture { get; init; }
        /// <summary>Material resource context for planning. It is never modified. Optional for an instance
        /// request, where it defaults to the resolved permutation resource.</summary>
        public MaterialResource Resource { get; init; }
        public Guid ResourceId { get; init; }
        public string FriendlyName { get; init; }
        /// <summary>Invoked once, after the gate and validation pass. The same snapshot is used for every phase.</summary>
        public Func<LE3ShaderSourceSnapshot> SourceProvider { get; init; }
        /// <summary>Authoritative metadata provider. It must not load texture payloads.</summary>
        public Func<IEntry, Compiler.TextureCompilationMetadata> GetTextureMetadata { get; init; }
        public Func<IEntry, IReadOnlyList<IEntry>> GetFontTexturePages { get; init; }
        public bool ResourceForcesFloatUsage { get; init; }
        public Compiler.ShouldCacheEnvironmentContext EnvironmentContext { get; init; }
        /// <summary>Evidence-backed resource ShouldCache hook; null results mean unresolved.</summary>
        public Func<Compiler.ResolvedMaterialCompilationInput, Compiler.ShaderCompileTarget, bool?> MaterialResourceShouldCache { get; init; }
    }

    public readonly record struct MaterialShaderCompileProgress(int CompletedJobs, int SelectedJobs, string CurrentTarget);

    public sealed record MaterialShaderJobReport(int JobIndex, string Target, bool Success, string Diagnostics);

    public sealed class MaterialShaderCompileResult
    {
        public MaterialShaderCompileOutcome Outcome { get; init; }
        /// <summary>The phase reached: where a failure/cancellation happened, or Completed.</summary>
        public MaterialShaderCompilePhase Phase { get; init; }
        /// <summary>The job target being compiled when compilation failed or was cancelled.</summary>
        public string FailedTarget { get; init; }
        public IReadOnlyList<string> Errors { get; init; } = [];
        /// <summary>Unexpected exception thrown by the compiler, a callback or I/O; distinct from returned material errors.</summary>
        public Exception Exception { get; init; }
        /// <summary>True when the native D3D compiler backend could not be loaded.</summary>
        public bool IsBackendUnavailable { get; init; }
        /// <summary>Per-job diagnostics, including warnings from successful jobs.</summary>
        public IReadOnlyList<MaterialShaderJobReport> Jobs { get; init; } = [];
        public IReadOnlyList<string> PlannedTargets { get; init; } = [];
        public int SelectedJobCount => PlannedTargets.Count;
        public bool IsZeroJobPlan { get; init; }
        /// <summary>The compiler's native required-membership completeness, reported separately from success.
        /// True only for a full-inventory plan in the shipping context; it is not bytecode or runtime parity.</summary>
        public bool IsCompleteShaderSet { get; init; }
        /// <summary>Instance resolution, for MaterialInstance requests. Check SharesAncestorPermutation:
        /// a shared permutation compiles the ancestor's (or base Material's) shaders, not a new one.</summary>
        public MaterialInstanceResolution InstanceResolution { get; init; }

        // Real compiler artifacts when available. Kept alive together; do not mutate them.
        public Compiler.TranslationResult Translation { get; init; }
        public Compiler.ShaderCompilePlan Plan { get; init; }
        public IReadOnlyList<Compiler.ShaderCompilationResult> CompiledShaders { get; init; } = [];
        public Compiler.ShaderCacheFragmentResult Fragment { get; init; }
    }

    public interface IMaterialShaderCompilationService
    {
        Task<MaterialShaderCompileResult> CompileMaterialAsync(MaterialShaderCompileRequest request,
            IProgress<MaterialShaderCompileProgress> progress = null, CancellationToken cancellationToken = default);
    }

    /// <summary>MaterialInstance resolution stage. It must produce the effective base graph plus the
    /// static parameters the game uses, never parent defaults posing as the instance.</summary>
    public interface IMaterialShaderInstanceResolver
    {
        MaterialInstanceResolution Resolve(ExportEntry materialInstance);
    }

    /// <summary>Future, separately authorized installation stage (not implemented).
    /// Installation needs separate validation and authorization, even for a complete fragment.</summary>
    public interface IMaterialShaderFragmentInstaller
    {
        void Install(MaterialShaderCompileResult result, IMEPackage targetPackage);
    }

    /// <summary>Step outcome from a compiler session; Errors are returned material/compiler errors.</summary>
    public sealed record MaterialShaderStepResult(bool Success, IReadOnlyList<string> Errors, string Diagnostics = null);

    /// <summary>Test seam over the compiler calls for one request. All phases share one source snapshot.</summary>
    public interface IMaterialShaderCompilerSession
    {
        MaterialShaderStepResult Translate();
        MaterialShaderStepResult Plan();
        /// <summary>Selected job targets after a successful Plan.</summary>
        IReadOnlyList<string> PlannedTargets { get; }
        MaterialShaderStepResult CompileJob(int jobIndex);
        MaterialShaderStepResult Assemble();
        bool IsCompleteShaderSet { get; }
        Compiler.TranslationResult Translation { get; }
        Compiler.ShaderCompilePlan CompilePlan { get; }
        IReadOnlyList<Compiler.ShaderCompilationResult> CompiledShaders { get; }
        Compiler.ShaderCacheFragmentResult Fragment { get; }
    }

    public interface IMaterialShaderCompilerBackend
    {
        IMaterialShaderCompilerSession CreateSession(MaterialShaderCompileRequest request,
            Compiler.ResolvedMaterialCompilationInput input, LE3ShaderSourceSnapshot sources);
    }

    public sealed class MaterialShaderCompilationService : IMaterialShaderCompilationService
    {
        private readonly MaterialShaderCompilationOptions options;
        private readonly IMaterialShaderCompilerBackend backend;

        public MaterialShaderCompilationService(MaterialShaderCompilationOptions options = null,
            IMaterialShaderCompilerBackend backend = null)
        {
            this.options = options ?? new MaterialShaderCompilationOptions();
            this.backend = backend ?? new LE3MaterialShaderCompilerBackend();
        }

        public Task<MaterialShaderCompileResult> CompileMaterialAsync(MaterialShaderCompileRequest request,
            IProgress<MaterialShaderCompileProgress> progress = null, CancellationToken cancellationToken = default)
        {
            // Checked before any work is scheduled: a disabled request performs no I/O or mutation.
            if (!options.Enabled)
                return Task.FromResult(new MaterialShaderCompileResult
                {
                    Outcome = MaterialShaderCompileOutcome.Disabled,
                    Phase = MaterialShaderCompilePhase.Gate,
                    Errors = ["Offline LE3 material shader compilation is disabled."]
                });
            // One background task per request; jobs run sequentially inside it.
            return Task.Run(() => Compile(request, progress, cancellationToken), CancellationToken.None);
        }

        private MaterialShaderCompileResult Compile(MaterialShaderCompileRequest request,
            IProgress<MaterialShaderCompileProgress> progress, CancellationToken cancellationToken)
        {
            var phase = MaterialShaderCompilePhase.Validation;
            IMaterialShaderCompilerSession session = null;
            var jobs = new List<MaterialShaderJobReport>();
            string currentTarget = null;
            MaterialInstanceResolution resolution = null;

            MaterialShaderCompileResult Result(MaterialShaderCompileOutcome outcome, IReadOnlyList<string> errors = null,
                Exception exception = null, bool backendUnavailable = false) => new()
            {
                Outcome = outcome,
                Phase = outcome == MaterialShaderCompileOutcome.SucceededFragment ? MaterialShaderCompilePhase.Completed : phase,
                FailedTarget = outcome is MaterialShaderCompileOutcome.Failed or MaterialShaderCompileOutcome.Cancelled ? currentTarget : null,
                Errors = errors ?? [],
                Exception = exception,
                IsBackendUnavailable = backendUnavailable,
                Jobs = jobs.ToArray(),
                PlannedTargets = session?.PlannedTargets ?? [],
                IsZeroJobPlan = session?.PlannedTargets is { Count: 0 },
                IsCompleteShaderSet = session?.IsCompleteShaderSet ?? false,
                Translation = session?.Translation,
                Plan = session?.CompilePlan,
                CompiledShaders = session?.CompiledShaders ?? [],
                Fragment = session?.Fragment,
                InstanceResolution = resolution
            };

            try
            {
                if (cancellationToken.IsCancellationRequested) return Result(MaterialShaderCompileOutcome.Cancelled);
                var unsupported = ValidateRequest(request, options.InstanceResolver != null, out var invalid);
                if (unsupported != null) return Result(MaterialShaderCompileOutcome.Unsupported, [unsupported]);
                if (invalid != null) return Result(MaterialShaderCompileOutcome.Failed, [invalid]);

                var baseMaterial = request.Material;
                var staticParameters = request.ResolvedStaticParameters;
                ExportEntry requestedInstance = null;
                if (request.Material.IsA("MaterialInstance"))
                {
                    phase = MaterialShaderCompilePhase.InstanceResolution;
                    resolution = options.InstanceResolver.Resolve(request.Material);
                    if (resolution == null) return Result(MaterialShaderCompileOutcome.Failed, ["The instance resolver returned no result."]);
                    if (!resolution.Success)
                        return Result(resolution.IsUnsupported ? MaterialShaderCompileOutcome.Unsupported : MaterialShaderCompileOutcome.Failed,
                            resolution.Errors);
                    baseMaterial = resolution.BaseMaterial;
                    staticParameters = resolution.EffectiveStaticParameters;
                    requestedInstance = request.Material;
                    if (request.Resource == null)
                    {
                        if (resolution.PermutationResource == null)
                            return Result(MaterialShaderCompileOutcome.Failed, ["No material resource context was supplied or resolved."]);
                        request = request with { Resource = resolution.PermutationResource };
                    }
                }

                Compiler.ResolvedMaterialCompilationInput input;
                try
                {
                    input = new Compiler.ResolvedMaterialCompilationInput(baseMaterial, staticParameters,
                        requestedMaterialInstance: requestedInstance,
                        engineDefaultTexture: request.EngineDefaultTexture, worldDefaultTexture: request.WorldDefaultTexture);
                }
                catch (ArgumentException e)
                {
                    return Result(MaterialShaderCompileOutcome.Failed, [e.Message], e);
                }

                phase = MaterialShaderCompilePhase.SourceCapture;
                if (cancellationToken.IsCancellationRequested) return Result(MaterialShaderCompileOutcome.Cancelled);
                var sources = request.SourceProvider();
                if (sources == null) return Result(MaterialShaderCompileOutcome.Failed, ["The source provider returned no shader source snapshot."]);
                session = backend.CreateSession(request, input, sources);

                phase = MaterialShaderCompilePhase.Translation;
                if (cancellationToken.IsCancellationRequested) return Result(MaterialShaderCompileOutcome.Cancelled);
                var step = session.Translate();
                if (!step.Success) return Result(MaterialShaderCompileOutcome.Failed, step.Errors);

                phase = MaterialShaderCompilePhase.Planning;
                if (cancellationToken.IsCancellationRequested) return Result(MaterialShaderCompileOutcome.Cancelled);
                step = session.Plan();
                // An unresolved plan withholds every job; never recover a partial plan here.
                if (!step.Success) return Result(MaterialShaderCompileOutcome.Failed, step.Errors);

                phase = MaterialShaderCompilePhase.Compilation;
                var targets = session.PlannedTargets;
                progress?.Report(new MaterialShaderCompileProgress(0, targets.Count, null));
                for (int i = 0; i < targets.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested) return Result(MaterialShaderCompileOutcome.Cancelled);
                    currentTarget = targets[i];
                    // An in-flight native compile cannot be cancelled; cancellation is honoured between jobs.
                    step = session.CompileJob(i);
                    jobs.Add(new MaterialShaderJobReport(i, targets[i], step.Success, step.Diagnostics));
                    if (!step.Success)
                        return Result(MaterialShaderCompileOutcome.Failed,
                            step.Errors is { Count: > 0 } ? step.Errors : [$"Compilation failed for {targets[i]}."]);
                    progress?.Report(new MaterialShaderCompileProgress(i + 1, targets.Count, targets[i]));
                }
                currentTarget = null;

                phase = MaterialShaderCompilePhase.Assembly;
                if (cancellationToken.IsCancellationRequested) return Result(MaterialShaderCompileOutcome.Cancelled);
                step = session.Assemble();
                if (!step.Success) return Result(MaterialShaderCompileOutcome.Failed, step.Errors);
                return Result(MaterialShaderCompileOutcome.SucceededFragment);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Result(MaterialShaderCompileOutcome.Cancelled);
            }
            catch (Exception e) when (IsBackendLoadFailure(e))
            {
                return Result(MaterialShaderCompileOutcome.Failed, ["The native D3D shader compiler backend is unavailable: " + e.Message], e, backendUnavailable: true);
            }
            catch (Exception e)
            {
                return Result(MaterialShaderCompileOutcome.Failed, [$"Unexpected {e.GetType().Name} during {phase}: {e.Message}"], e);
            }
        }

        /// <summary>Returns an unsupported reason, or sets <paramref name="invalid"/> for a malformed request.</summary>
        private static string ValidateRequest(MaterialShaderCompileRequest request, bool canResolveInstances, out string invalid)
        {
            invalid = null;
            if (request?.Material == null) { invalid = "A material export is required."; return null; }
            if (request.Material.Game != MEGame.LE3) return $"Only LE3 materials are supported, not {request.Material.Game}.";
            bool isInstance = request.Material.IsA("MaterialInstance");
            if (isInstance && !canResolveInstances)
                return "MaterialInstance compilation requires an instance resolver, and none is configured.";
            if (!isInstance && !request.Material.IsA("Material")) return $"{request.Material.ClassName} is not a base Material.";
            if (isInstance && request.ResolvedStaticParameters != null)
                invalid = "Static parameters for an instance request come from instance resolution; do not supply them.";
            else if (!isInstance && request.Resource == null) invalid = "A material resource context is required.";
            else if (request.ResourceId == Guid.Empty) invalid = "A nonempty material resource id is required.";
            else if (string.IsNullOrEmpty(request.FriendlyName)) invalid = "A nonempty friendly name is required.";
            else if (request.SourceProvider == null) invalid = "A shader source provider is required.";
            else if (request.GetTextureMetadata == null) invalid = "A texture metadata provider is required.";
            return null;
        }

        private static bool IsBackendLoadFailure(Exception e) => e switch
        {
            DllNotFoundException or BadImageFormatException or EntryPointNotFoundException => true,
            TypeInitializationException { InnerException: { } inner } => IsBackendLoadFailure(inner),
            _ => false
        };
    }

    /// <summary>Production session over LE3MaterialShaderCompiler, one compiler per request.</summary>
    public sealed class LE3MaterialShaderCompilerBackend : IMaterialShaderCompilerBackend
    {
        public IMaterialShaderCompilerSession CreateSession(MaterialShaderCompileRequest request,
            Compiler.ResolvedMaterialCompilationInput input, LE3ShaderSourceSnapshot sources) =>
            new Session(request, input, sources);

        private sealed class Session(MaterialShaderCompileRequest request, Compiler.ResolvedMaterialCompilationInput input,
            LE3ShaderSourceSnapshot sources) : IMaterialShaderCompilerSession
        {
            private readonly Compiler compiler = new(input, getTextureMetadata: request.GetTextureMetadata,
                getFontTexturePages: request.GetFontTexturePages);
            private readonly List<Compiler.ShaderCompilationResult> compiled = [];

            public Compiler.TranslationResult Translation { get; private set; }
            public Compiler.ShaderCompilePlan CompilePlan { get; private set; }
            public IReadOnlyList<Compiler.ShaderCompilationResult> CompiledShaders => compiled;
            public Compiler.ShaderCacheFragmentResult Fragment { get; private set; }
            public IReadOnlyList<string> PlannedTargets { get; private set; }
            public bool IsCompleteShaderSet => Fragment?.IsCompleteShaderSet ?? false;

            public MaterialShaderStepResult Translate()
            {
                Translation = compiler.TranslateMaterialResourceHlsl(sources, request.ResourceForcesFloatUsage);
                return new MaterialShaderStepResult(Translation.Success, Translation.Errors ?? []);
            }

            public MaterialShaderStepResult Plan()
            {
                CompilePlan = compiler.BuildKnownSubsetCompilePlan(Translation, request.Resource,
                    request.EnvironmentContext, request.MaterialResourceShouldCache);
                if (CompilePlan.Success) PlannedTargets = CompilePlan.Jobs.Select(job => Describe(job.Target)).ToArray();
                return new MaterialShaderStepResult(CompilePlan.Success, CompilePlan.Errors);
            }

            public MaterialShaderStepResult CompileJob(int jobIndex)
            {
                var result = compiler.CompileShader(CompilePlan.Jobs[jobIndex], sources);
                if (result.Success) compiled.Add(result);
                return new MaterialShaderStepResult(result.Success,
                    result.Success || string.IsNullOrEmpty(result.Diagnostics) ? [] : [result.Diagnostics], result.Diagnostics);
            }

            public MaterialShaderStepResult Assemble()
            {
                Fragment = compiler.BuildShaderCacheFragment(Translation, CompilePlan, compiled,
                    request.ResourceId, request.FriendlyName, sources);
                return new MaterialShaderStepResult(Fragment.Success, Fragment.Errors);
            }

            private static string Describe(Compiler.ShaderCompileTarget target) => target.VertexFactoryTypeName == null
                ? target.ShaderTypeName
                : $"{target.ShaderTypeName} / {target.VertexFactoryTypeName}";
        }
    }
}
