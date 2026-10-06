# LE3 offline material shader compiler: consumer contract handoff

Last updated: 2026-10-05.

## Assignment and activation boundary

Implement the consumer-facing interface and adapter for the offline LE3 material shader compiler. **Keep the production integration disabled by default.** This is preparation for using the compiler when its remaining coverage and compatibility work is complete, not authorization to enable compilation or install generated shaders in normal workflows.

The host project and its interface naming are the consumer agent's responsibility. The examples below distinguish existing LegendaryExplorerCore APIs from suggested consumer-owned abstractions. Do not introduce a second compiler implementation or copy its shader registrations, macros, optimization flags, binding layouts, or ShouldCache predicates into the consumer.

Implement an explicit activation gate at the consumer entry point, before package loading, shader-source capture, texture metadata reads, compilation, or output mutation. A disabled request should return a distinct disabled/unavailable outcome, not an empty successful shader set. It must not silently fall back to another compiler or replace existing shader data. Unit tests and an explicitly invoked offline test harness can exercise the adapter independently of production activation.

Enabling the consumer feature in the future is a separate decision from permitting installation: successful compilation is not currently proof of a complete or compatible shader set. Keep installation behind its own validation/authorization boundary.

## Current capability and validation boundary

LE3 PC / SM5 is the only supported target for this integration. LE1/LE2 are future work. The current pipeline can translate supported material graphs, select known permutations, compile them with D3DCompile, reflect bindings, construct typed shader objects, and return a detached serializable ShaderCache fragment.

At this checkpoint the compiler has 162 material shader registrations, eleven implemented vertex factories, and 55 expression classes. These are a status snapshot, **not constants to put into consumer code or a completeness guarantee**. Coverage will expand without requiring a fundamentally different caller pipeline.

All current shader-set completeness indicators return false. The known-subset planner can succeed without covering every factory or every required engine permutation. Original engine bytecode parity, full native uniform evaluation, and rendered compatibility are not yet established for the general case. An offline compile/archive success is not runtime proof.

The detailed compiler research/status handoff is [LE3ExternalMaterialShaderCompiler.md](LE3ExternalMaterialShaderCompiler.md). That document, not this interface handoff, owns the evolving reverse-engineering inventory and roadmap.

## Existing source and public API

Paths below are relative to the LegendaryExplorer repository root:

- `LegendaryExplorer/LegendaryExplorerCore/Shaders/LE3MaterialShaderCompiler.cs`
- `LegendaryExplorer/LegendaryExplorerCore/Shaders/LE3ShaderSourceSnapshot.cs`
- `LegendaryExplorer/LegendaryExplorerCore/Shaders/LE3TextureMetadataReader.cs`
- `LegendaryExplorer/LegendaryExplorerCore/Shaders/LE3ShaderSourceCrc.cs`
- `LegendaryExplorer/LegendaryExplorerCore/Unreal/BinaryConverters/Material.cs`

Use namespace `LegendaryExplorerCore.Shaders`; most compiler input/result types are nested in `LE3MaterialShaderCompiler`. MaterialResource, StaticParameterSet, MaterialShaderMap, and ShaderCache are existing binary-converter types.

Existing entry points, in their ordinary calling order:

1. Construct `ResolvedMaterialCompilationInput` and `LE3MaterialShaderCompiler`.
2. `TranslateMaterialResourceHlsl(LE3ShaderSourceSnapshot, bool resourceForcesFloatUsage = false)`.
3. `BuildKnownSubsetCompilePlan(TranslationResult, MaterialResource, ...)`.
4. `CompileShader(ShaderCompileJob, LE3ShaderSourceSnapshot)` for each selected job.
5. `BuildShaderCacheFragment(TranslationResult, ShaderCompilePlan, IReadOnlyList<ShaderCompilationResult>, Guid materialResourceId, string friendlyName, LE3ShaderSourceSnapshot)`.

Use the resource-translation entry point for this integration. `TranslateMaterialHlsl` is also public, but its authored-root path omits resource input defaults and selection-color operations. The resource path deliberately fails closed for unsupported dispatch/features, including currently unverified connected WorldPositionOffset requests. Do not switch paths or discard unsupported graph nodes to make a failing request succeed.

The bytecode backend uses SharpDX.D3DCompiler/D3DCompile. Treat this adapter as Windows/native-backend-dependent and surface missing backend dependencies distinctly from material errors. Reuse the host's normal LEC initialization; `GlobalTest.Init()` belongs to tests, not production integration.

## Inputs and ownership

| Input | Current contract | Consumer responsibility |
| --- | --- | --- |
| Base material | LE3 Material export, including supported DecalMaterial handling; authored expression graph available | Keep the package and relevant graph data alive and stable for the operation. Initial consumer support should target ordinary base Materials. |
| Resolved static parameters | Optional effective StaticParameterSet; null means authored defaults | Resolve inheritance before supplying an instance permutation. The compiler snapshots the set. |
| Requested instance | Optional MaterialInstance export in ResolvedMaterialCompilationInput | Provenance only at present; does not invoke parent or override resolution. |
| Texture metadata provider | `Func<IEntry, TextureCompilationMetadata>` | Resolve each original texture reference to authoritative metadata without loading texture payloads. |
| Shader sources | One immutable LE3ShaderSourceSnapshot | Capture from caller-configured installed sources, use the same object throughout the operation, and invalidate/rebuild when deliberately changing sources. |
| Resource context | MaterialResource supplied to planning | Supply the correct context; do not assume an empty resource or a stale parent's resource is always appropriate. Current macro generation consumes resource UsingTransforms as well as translation-derived usage. |
| Precision override | Optional resourceForcesFloatUsage supplied to resource translation | Resolve resource-local precision requirements when applicable; authored bForceFullPrecision is also considered internally. Do not select the PS3 half-type path for SM5. |
| Map identity | Nonempty resource GUID and nonempty friendly name | Coordinate with the intended material resource. The resource GUID is not interchangeable with the static key's BaseMaterialId. |
| Optional planning context | Environment context and resource ShouldCache callback | Ordinary materials can use planner defaults. Special/derived effects-owned cases may require additional resolution; never turn unknown into true. |

Graph expressions are currently resolved from the base material's package. Do not assume that arbitrary imported/cross-package expression links are supported because texture imports are supported. Fail unsupported graph references explicitly.

### Metadata-only textures

TextureCompilationMetadata contains dimension (2D/cube), CompressionSettings, UnpackMin, and UnpackMax; no mip, image, bulk-data, texture binary, or TFC payload is required. When an authoritative metadata provider is supplied, a null result fails translation; there is no automatic loaded-export fallback.

LE3TextureMetadataReader provides a read-only package-table/property session and caches metadata per export. Resolve imports to the appropriate donor reader/export in the consumer. It does not perform automatic package discovery. Its reader, package, and stream are not thread-safe; do not edit/save the exposed lazy package. A compressed block can contain adjacent binary bytes, so this avoids export-sized payload loading, not every incidental compression-block read.

Preserve texture identity separately from metadata. Equal metadata does not mean equal resources. The compiler callback receives the graph's original reference; the returned metadata must not replace its package-reference identity. The resulting UniformExpressionTextures list uses UIndexes relative to the base graph package. Another target package requires explicit relinking.

### Source snapshot

Use `LE3ShaderSourceSnapshot.FromDirectory(shaderDirectory, windowsAnsiCodePage: ...)` or construct it from exact caller-owned decoded sources. Supply the appropriate explicit Windows ANSI code page for untagged source text; 1252 is the currently tested installation setting, not a universal guarantee. Preserve source content/line endings. Sources belong to the game installation; do not redistribute them as consumer assets.

The snapshot copies decoded text and can be reused across compilations. Translation, every compiled result, and checked cache construction must refer to the **same snapshot object**. The APIs reject mismatched provenance. Do not mix directory-overload compilation with a snapshot translation or reconstruct an equivalent snapshot halfway through a request.

## Result contract and failure handling

| Phase/result | Useful output | Meaning of success |
| --- | --- | --- |
| TranslationResult | MaterialHlsl, graph analysis, ordered uniform arrays, texture UIndexes, usage metadata, Errors | Supported graph/resource translation succeeded; not native parity. |
| ShaderCompilePlan | Jobs, Decisions, ExcludedTargets, Errors | All supplied/known-subset candidates resolved; not full engine coverage. |
| ShaderCompilationResult | Target, Bytecode, Diagnostics, InstructionCount, ParameterBindings | Nonempty bytecode; retain warnings as well as errors. |
| ShaderCacheFragmentResult | Cache, UniformExpressionTextures, Errors, IsCompleteShaderSet | Every job in the explicit successful plan constructed and the detached cache was assembled; not permission to install. |

Planner decisions have Eligible, Excluded, and Unresolved dispositions and identify the decisive gate. Excluded is an expected ShouldCache outcome, not a compile failure. An unresolved candidate withholds all plan Jobs; do not recover individually eligible decisions into a partial plan. Retain the decision inventory for diagnostics.

Every selected job must appear exactly once in cache construction, from the same translation/compiler input and source snapshot. Unknown constructors/bindings, failed compiles, duplicate/missing jobs, or CRC failures must not yield a successful cache. The current fragment builder also rejects an empty plan: handle a zero-job outcome explicitly rather than treating it as a complete empty shader set.

The adapter should retain phase-specific errors, per-job compiler diagnostics/warnings, planning decisions, and the phase/target at which cancellation or failure occurred. Expected compiler errors are returned in result objects in several paths, but invalid inputs, I/O, missing backend dependencies, or callback failures can also throw. Handle these at the consumer boundary without misreporting them as unsupported material expressions.

ShaderCache construction includes typed shader objects, mesh maps grouped by vertex factory, direct non-mesh map entries, ordered uniform-expression arrays, the static-parameter key, and source CRC maps. No synthetic vertex factory should be assigned to non-mesh shaders. Source CRCs are not bytecode checksums.

The returned cache/container data is detached from packages. Some uniform-expression tree objects are shared with the translation; do not mutate published results or treat them as a fully deep-copied, thread-safe object graph. Keep translation/planning/results alive through assembly; do not try to reconstruct provenance from serialized job DTOs with the current API.

## Example using the current API

This helper illustrates the underlying pipeline, **not the production activation gate**. Call it only from a permitted adapter path/test harness. It throws for brevity; the production adapter should return structured outcomes and preserve all diagnostics. The parameters are already resolved by the caller.

```csharp
using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Compiler = LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

static Compiler.ShaderCacheFragmentResult CompileOfflineFragment(
    Compiler.ResolvedMaterialCompilationInput input,
    MaterialResource resource,
    Guid resourceId,
    string friendlyName,
    LE3ShaderSourceSnapshot sources,
    Func<IEntry, Compiler.TextureCompilationMetadata> metadataForTexture,
    bool resourceForcesFloatUsage = false)
{
    ArgumentNullException.ThrowIfNull(metadataForTexture);
    var compiler = new Compiler(input, getTextureMetadata: metadataForTexture);
    var translation = compiler.TranslateMaterialResourceHlsl(
        sources, resourceForcesFloatUsage);
    if (!translation.Success)
        throw new InvalidOperationException(
            string.Join(Environment.NewLine, translation.Errors));

    var plan = compiler.BuildKnownSubsetCompilePlan(translation, resource);
    if (!plan.Success)
        throw new InvalidOperationException(
            string.Join(Environment.NewLine, plan.Errors));

    var compiled = new List<Compiler.ShaderCompilationResult>();
    foreach (var job in plan.Jobs)
    {
        var result = compiler.CompileShader(job, sources);
        if (!result.Success)
            throw new InvalidOperationException(result.Diagnostics);
        compiled.Add(result);
    }

    var fragment = compiler.BuildShaderCacheFragment(
        translation, plan, compiled, resourceId, friendlyName, sources);
    if (!fragment.Success)
        throw new InvalidOperationException(
            string.Join(Environment.NewLine, fragment.Errors));
    return fragment; // Detached subset; IsCompleteShaderSet is currently false.
}
```

For a base material:

```csharp
var input = new Compiler.ResolvedMaterialCompilationInput(baseMaterial);
```

For a future resolved instance permutation:

```csharp
var input = new Compiler.ResolvedMaterialCompilationInput(
    baseMaterial,
    resolvedStaticParameters: mergedStaticParameters,
    requestedMaterialInstance: instance);
```

## MaterialInstance extension seam

MaterialInstance parent-chain resolution, determining whether an instance needs a new shader map, and applying runtime scalar/vector/texture overrides are **not implemented by the compiler input constructor**. The existing ResolvedMaterialCompilationInput is a downstream boundary for future resolution, not an instance resolver.

Keep a consumer-side resolution stage separate from compilation. Initially it may accept ordinary base Materials and return a distinct not-yet-supported outcome for unresolved instances. Do not quietly compile an instance's parent defaults and return that as the requested permutation.

Future resolution will need to identify the base graph, merge inherited static values, resolve effective compile-affecting texture metadata/resource semantics, preserve requested-instance provenance, and coordinate resource/static-map identity. Runtime-only overrides do not inherently imply a new static permutation, but do not classify all texture overrides as irrelevant to compilation. Actual instance rules remain to be verified.

The resource ShouldCache hook currently accepts `Func<ResolvedMaterialCompilationInput, ShaderCompileTarget, bool?>`. Null means unresolved. Effects-owned materials without a resolved hook can remain unresolved; the callback must represent evidence-backed resource behavior, not an unconditional escape hatch.

The supplied static set retains its own BaseMaterialId when the map's explicit resource ID is assigned. Do not force those GUIDs to match or invent a parent/instance key. The compiler's current static selection stage binds supported entries by first full-name match; bOverride and expression GUID do not filter selection at that stage. The resolver therefore must supply already-merged effective values.

## Suggested consumer-owned facade

Names here are suggestions, **not existing LEC APIs**:

- `CompileMaterialAsync(request, progress, cancellationToken)` on a host-owned service/interface.
- Request: resolved compiler input, resource context/identity, source snapshot, metadata provider, precision override, and any explicitly resolved planning context.
- Outcome: Disabled, Unsupported, Failed, Cancelled, or SucceededFragment; retain translation, plan/decisions, per-job results, detached fragment, and diagnostics where available.
- Completeness: carry the compiler's status separately from outcome success. No successful outcome implies installability.
- Resolver and installer: separate interfaces, so future instance resolution and validated cache/resource installation do not require consumers to depend on compiler internals.

Keep host-local paths/backend discovery/progress/UI concerns in the consumer, not in portable material metadata. Do not freeze proposed facade names into LEC without coordination. Small input/context additions may still be needed as resource semantics are recovered; isolate the current calls in one adapter.

Start with one compiler instance per request and a sequential job loop. Compiler instances, live packages, metadata readers, and result graphs have no blanket thread-safety guarantee. Share immutable source snapshots, not mutable reader streams. Current compilation is synchronous and has no cancellation token; check cancellation between phases/jobs and report progress as completed jobs out of selected plan Jobs. Do not promise cancellation of an in-flight native D3DCompile call. Avoid unbounded Task.Run per permutation.

Do not add a persistent compiled-result cache keyed only by export name or GUID. A correct cache would also need graph/static input identity, effective metadata/resource semantics, source content, target environment/flags and compiler version. Source snapshot reuse and request-scoped metadata caching are suitable first steps; defer broader cache identity until specified.

## Package installation is a separate, currently blocked boundary

None of the above compiler methods saves a package, modifies a material resource, or merges with an existing shader cache. Returning a detached fragment is the required first integration target.

Future installation needs validated completeness/runtime compatibility, coordinated material-resource ID and static key, texture-reference relinking, uniform-texture/resource metadata updates, shader/map/CRC merging, and preservation of existing entries/references. Do not assume setting UniformExpressionTextures alone completes resource installation. Do not add production package saves, game launches, or installed-game writes as part of this interface assignment.

## Acceptance criteria for the consumer agent

- Production feature is disabled by default; the disabled path performs no compiler-related I/O or mutation and reports Disabled distinctly.
- Ordinary LE3 base Materials can reach the adapter in tests; wrong games, unresolved instances, and unsupported contexts fail explicitly.
- Tests cover phase errors, returned errors versus exceptions, expected exclusions, unresolved plans, failed jobs, zero-job plans, cancellation between jobs, and retained warnings.
- Tests verify the same source snapshot is passed to translation, compilation, and cache assembly, and preserve resource identity/static-key separation.
- Metadata resolution does not load texture mips/payloads or TFC data; donor/import resolution and missing metadata are tested independently.
- Successful fragment outcomes retain ordered texture references and false completeness without saving/installing anything.
- Test seams permit orchestration tests without a game installation/native compiler; optional offline integration tests are explicit and machine-local, not mandatory installed-game corpus scans.
- Do not hardcode current type/factory counts, duplicate ShouldCache/macros/flags, silently skip unsupported nodes, or pretend parent compilation resolves an instance.
- Preserve unrelated repository changes; no commits/push/staging unless the user asks. Compiler code comments must not reference external engine source code.
- Report the adapter's location, public host interface, disabled gate, tests performed, and deferred resolver/installer work back to the user. Record any needed contract changes for coordination with the compiler work.

## Roadmap / coordination

Consumer work can proceed now: interface, disabled gate, request validation, orchestration, metadata/source sessions, diagnostics, progress/cancellation, and test seams. The compiler work continues on required-set completeness, remaining vertex factories, graph/uniform/bytecode parity, and eventually user-approved runtime validation. MaterialInstance resolution and production installation remain explicit later phases.

Update this document whenever a public input/result contract or activation boundary changes. Inventory-only progress belongs in the main compiler handoff. Neither document is a promise that current output can replace a shipping shader set.
