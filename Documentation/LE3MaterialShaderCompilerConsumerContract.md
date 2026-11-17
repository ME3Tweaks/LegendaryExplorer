# LE3 offline material shader compiler: consumer contract handoff

Last updated: 2026-10-06.

**Current checkpoint:**
- **New, additive: usage advisor.** `LE3MaterialUsageAdvisor.Check(baseMaterialProperties, intendedUses)` is a pure query that mirrors the game's CheckMaterialUsage.
  - It returns the `bUsedWith*` flags to set before compiling, plus uses the game vetoes for decal-only and fog-volume-only materials.
  - Pass the base Material's properties; instances defer to it.
  - Set the flags, then compile: flags change the required shader set.
- **Resource bitfield:** LEX's `MaterialResource` slots 2–5 are really Time, DynamicParameter, LightmapUVs and WPO-connected. Their names are shifted; the byte layout is correct.
  - Translation flags reproduce all six bits in every shipping cooked resource.
  - Installation remains out of scope and is being planned separately.
- **Base-material parity:** the full decooked corpus has been compared against shipping shader maps. 4017 of 4019 compiled ordinary exact-key materials and 1861 of 1861 EffectsMaterials exact-key materials are wholly byte-identical. The 2 exceptions are proven missing graph inputs.
- **Blend-mode define fixes:**
  - `BLEND_AlphaComposite` now compiles with `MATERIALBLENDING_ADDITIVE`.
  - `MATERIAL_ONELAYERDISTORTION` follows `bUseOneLayerDistortion` for translucent, additive and modulate output.
- **Unchanged:** no consumer-facing API or activation changes; production stays disabled. Results reported with `IsCompleteShaderSet` still mean membership only, never installation permission. MaterialInstance permutation parity has not been measured yet.
- **More detail:** see the top checkpoint of [LE3ExternalMaterialShaderCompiler.md](LE3ExternalMaterialShaderCompiler.md).

Latest checkpoint: both velocity shader stages now apply the proven native resource-class veto, using the existing nullable DecalFactoryFallback predicate (false Material, true DecalMaterial), independent of bUsedWithDecals. All294 extra jobs are removed in the21 historical target cases:17 exact-key cases have6851/6851 jobs byte-identical,4 unique-name cases1612/1612; required membership is present for all21. Shipping extra permutations and9 rounded numeric-default mismatches remain distinct. ScopedLE3 tests2608/2608 pass inDebug andRelease,2 opt-in skipped. Four-package regression: exact21/22 wholly match and1336/1341 jobs identical; unique4/4 compiled wholly match,549/549 jobs identical,0 absent shipping. No new caller argument/field/API, no activation change. Explicit derived-resource contexts retain conditional null/fail-closed behavior; normal adapter context construction supplies the class predicate automatically. Production stays disabled; full ordinary baseline and runtime compatibility are not refreshed/certified.

Previous checkpoint: an omitted VectorParameter DefaultValue now uses the installed LE3 class default RGBA(0,0,0,1), while an explicitly authored immutable color with alpha0 remains unchanged. SelectionColor and MeshEmitter defaults, full-RGBA uniform identity and runtime overrides are retained. Two unique-name shipping-material cases now have matching ordered uniforms and170/170 byte-identical jobs; identity matching remains less trusted than exact cooked keys. The parity harness now records every comparer-reported mismatching uniform root, not only example materials. Numeric comparisons remain strict: remaining decooked decimal rounding and Invisible's missing graph are input provenance issues, not normalization rules for the adapter. ScopedLE3 tests2601/2601 pass inDebug andRelease,2 opt-in skipped; installed/default inspection separately2/2 pass. The four-package parity regression remains21/22 exact materials and1336/1341 jobs byte-identical. No caller input/result contract or activation changes; production staysdisabled.

Latest resume: native allocation/vtable tracing distinguishes ordinary Material resources (disconnected DiffuseColor/SpecularColor use black) from DecalMaterial resources (byte128/Pow22 grey). This supersedes the universal-grey checkpoint and is not a bUsedWithDecals or material-name override. Fogged custom-simple texture andcustom-vector vertex Constant/LinearHalfspace/Sphere profiles now use shipping-bytecode-backed lightmap definitions. Scoped LE3 tests2596/2596 pass inDebug andRelease,2 opt-in skipped; installedCDO inspection separately passed. Same4-package raw-input parity:21/22 exact materials and1336/1341 jobs byte-identical; all328IceDecal jobs match. OnlyInvisible's5 missing-graph differences remain in that exact target; uniform/metadata/velocity cases are still unresolved. The prior98.6% full-corpus result remains historical, not refreshed. No new caller arguments/result APIs or activation changes. Callers must supply the intended connected graph: a temporary Invisible reconstruction matched all5 shipping shaders, but production does not guess missing authored connections.

Previous checkpoint retained: ordinary2D/cube samples accept generic scalar UVs through native replication;2D UV coercion forces float while cube retains normal numeric precision. Incompatible explicit widths fail, with diagnostic texture registration preserved and HLSL withheld. The opt-in parity harness has object-database donor fallback and property-stream FlipBook count reads; these are harness policies, **not automatic discovery in LEC or the consumer adapter**.

The adapter, MaterialInstance resolution and completeness reporting are implemented, and production remains disabled. The latest work is in the dated sections at the end of this document. For current status, test and harness recipes, and the prioritized next steps, start with the "Start here" section of [LE3ExternalMaterialShaderCompiler.md](LE3ExternalMaterialShaderCompiler.md).

## Assignment and activation boundary

Implement the consumer-facing interface and adapter for the offline LE3 material shader compiler. **Keep the production integration disabled by default.** This is preparation for using the compiler when its remaining coverage and compatibility work is complete, not authorization to enable compilation or install generated shaders in normal workflows.

The host project and its interface naming are the consumer agent's responsibility. The examples below distinguish existing LegendaryExplorerCore APIs from suggested consumer-owned abstractions. Do not introduce a second compiler implementation or copy its shader registrations, macros, optimization flags, binding layouts, or ShouldCache predicates into the consumer.

Implement an explicit activation gate at the consumer entry point, before package loading, shader-source capture, texture metadata reads, compilation, or output mutation. A disabled request should return a distinct disabled/unavailable outcome, not an empty successful shader set. It must not silently fall back to another compiler or replace existing shader data. Unit tests and an explicitly invoked offline test harness can exercise the adapter independently of production activation.

Enabling the consumer feature in the future is a separate decision from permitting installation: successful compilation is not currently proof of a complete or compatible shader set. Keep installation behind its own validation/authorization boundary.

## Current capability and validation boundary

LE3 PC / SM5 is the only supported target for this integration. LE1/LE2 are future work. The current pipeline can translate supported material graphs, select known permutations, compile them with D3DCompile, reflect bindings, construct typed shader objects, and return a detached serializable ShaderCache fragment.

At this checkpoint the compiler has 162 material shader registrations, twenty-seven implemented vertex factories, and 112 verified runtime expression classes, including Custom and CustomTexture. Specialized named texture callbacks retain even `None` as a named uniform; generic texture/movie/font parameter callbacks instead test the None name index, ignoring its number. Do not flatten their ordered uniform trees. Render-target/movie references require supplied compilation metadata, never payload reads. These are a status snapshot, **not constants to put into consumer code or a completeness guarantee**. Coverage will expand without requiring a fundamentally different caller pipeline. `GetObservedVertexFactoryRegistrations()` exposes all 29 observed static factory descriptors (28 materials-capable). All 27 potentially compiling material factories now have recovered gates/environments/binders; BioSPU has an always-false cache callback but no implemented compilation/layout path, and LocalShadow is not material-capable. Metadata presence alone does not make a factory compilable.

BioWare texture compositions reuse the supplied metadata-only texture path; they do not authorize loading donor texture payloads. Binary uniform expressions include the distinct `MaterialUniformExpressionBIOSineSubtend` and `MaterialUniformExpressionBIOMod` types. FlipBookSample uses the already-existing `MaterialUniformExpressionFlipBookTextureParameter` as a **numeric vector uniform**, despite that CLR class inheriting MaterialUniformExpressionTexture. Its TextureIndex refers to the shared global texture table; it does not itself occupy a sampler slot. Preserve the compiler's numeric/sampler array placement and serialize through LEC, not a consumer type-category guess or generic arithmetic substitute. The caller pipeline and material-instance resolution boundary are unchanged; additional metadata/default/font hooks are documented below.

Shader-set completeness now means **native required membership** (2026-10-06). `ShaderCompilePlan.CoversNativeRequiredSet` is true when all of these hold: the known or observed planner was used; it ran on a resource translation in the shipping context (debug view modes allowed, no caller resource predicate); and the plan succeeded. A fragment built from such a plan reports `IsCompleteShaderSet = true`. Explicit-candidate plans, caller resource predicates, the legacy authored-root translation and comparison helpers stay false. Completeness does not cover compile flags, bytecode, CRCs or rendering, and it does not authorize installation. Original engine bytecode parity, full native uniform evaluation, and rendered compatibility are not yet established for the general case. An offline compile/archive success is not runtime proof.

`BuildObservedSubsetCompilePlan` is an optional expanded planning path over the observed 29 factories and current material-type catalog. It includes BioSPU and LocalShadow exclusions and currently selects the same jobs as the known-subset planner for supported ordinary contexts. Success means this observed inventory resolved, **not** a live engine registry/complete map certificate. The normal adapter may retain BuildKnownSubsetCompilePlan; do not infer production activation or installation permission from the new path.

`ComparePlannedPermutationsWithShaderMap(plan, independentlySuppliedMap)` is an optional validation helper. It compares selected jobs with the map's mesh/non-mesh shader references without reading shader payloads or packages. Unknown and numbered names remain explicit mismatches; malformed references, conflicting key/type names, and duplicate factory maps fail comparison. This checks membership only: map/resource/static identity, shader availability, bytecode, bindings, uniform expressions and native required-set completeness remain separate. A matching existing/captured map is not an installation certificate. No change to the normal adapter pipeline is required.

The detailed compiler research/status handoff is [LE3ExternalMaterialShaderCompiler.md](LE3ExternalMaterialShaderCompiler.md). That document, not this interface handoff, owns the evolving reverse-engineering inventory and roadmap.

Resource-class routing is compiler-owned: ordinary Material compiles WorldPositionOffset as a vertex property, while DecalMaterial emits zero without traversing that input. The native stored WPO-connectivity predicate remains independent and can still affect planning for a decal. Do not prune or rewrite input graphs, clear connectivity flags, or reproduce these resource rules in the adapter. Legacy authored-root translation is not the resource route.

The 2026-10-06 decal WPO/missing-metadata checkpoint passes2626 scoped tests in each of Debug and Release, with2 opt-ins skipped. Bounded shipping regressions retain byte-identical comparable jobs for the21 historical decal cases and the same four-package baseline. This verifies compiler regression behavior, not rendered compatibility, live connected-Decal WPO parity or production activation. No request/response schema or consumer pipeline changes are needed.

Missing texture placeholders are not sufficient identities for metadata recovery. The opt-in parity harness now inventories all unresolved paths and attempted donor packages, separately from its bounded exception diagnostics. Three SnowRock placeholders have exact original LE3 T3D identities but no installed LE3 object-database candidates; they remain input-availability failures. This does not authorize a same-name texture substitution, fabricated metadata, donor payload access, or an adapter-specific alias heuristic. No caller contract or activation change is required.

`TranslationResult.UsesLightmapUVs` is an additive emitted-usage diagnostic: selected pixel LightmapUVs requests set it, vertex rejection occurs first, and fresh translations do not inherit a cooked resource's bUsesLightmapUVs. The translation-based material environment consumes this flag for LIGHTMAP_UV_ACCESS. A future detached resource adapter can map it to MaterialResource.bUsesLightmapUVs; no input resource/package is mutated. It remains diagnostic even if later validation fails, not a success or completeness certificate. PerInstanceRandom deliberately does not set UsesVertexColor; its native interpolant and explicitFloat1 type differ from the ordinary VertexColor expression.

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

Use the resource-translation entry point for this integration. `TranslateMaterialHlsl` is also public, but its authored-root path omits resource input defaults and selection-color operations. Connected local WorldPositionOffset now compiles as a vertex property without SelectionColor. Unsupported inherited/cross-package expression roots still fail closed. Do not switch paths or discard unsupported graph nodes to make a failing request succeed.

The bytecode backend uses SharpDX.D3DCompiler/D3DCompile. Treat this adapter as Windows/native-backend-dependent and surface missing backend dependencies distinctly from material errors. Reuse the host's normal LEC initialization; `GlobalTest.Init()` belongs to tests, not production integration.

## Inputs and ownership

| Input | Current contract | Consumer responsibility |
| --- | --- | --- |
| Base material | LE3 Material export, including supported DecalMaterial handling; authored expression graph available | Keep the package and relevant graph data alive and stable for the operation. Initial consumer support should target ordinary base Materials. |
| Resolved static parameters | Optional effective StaticParameterSet; null means authored defaults | Resolve inheritance before supplying an instance permutation. The compiler snapshots the set. |
| Requested instance | Optional MaterialInstance export in ResolvedMaterialCompilationInput | Provenance only at present; does not invoke parent or override resolution. |
| Texture metadata provider | `Func<IEntry, TextureCompilationMetadata>` | Resolve each original texture reference to authoritative metadata without loading texture payloads. |
| Default texture identities | Optional EngineDefaultTexture / WorldDefaultTexture on resolved input | Resolve separately into the base graph package; no engine/world discovery or package-table insertion is performed by the compiler. |
| Font pages | Optional `Func<IEntry,IReadOnlyList<IEntry>>` on compiler | Required for imported/unloaded fonts; return ordered base-package texture references, preserving null pages. No font glyph/image payload required. |
| Shader sources | One immutable LE3ShaderSourceSnapshot | Capture from caller-configured installed sources, use the same object throughout the operation, and invalidate/rebuild when deliberately changing sources. |
| Resource context | MaterialResource supplied to planning | Supply the correct context; do not assume an empty resource or a stale parent's resource is always appropriate. Current macro generation consumes resource UsingTransforms as well as translation-derived usage. |
| Precision override | Optional resourceForcesFloatUsage supplied to resource translation | Resolve resource-local precision requirements when applicable; authored bForceFullPrecision is also considered internally. Do not select the PS3 half-type path for SM5. |
| Map identity | Nonempty resource GUID and nonempty friendly name | Coordinate with the intended material resource. The resource GUID is not interchangeable with the static key's BaseMaterialId. |
| Optional planning context | Environment context and resource ShouldCache callback | Ordinary materials can use planner defaults. Special/derived effects-owned cases may require additional resolution; never turn unknown into true. |

Graph expressions are currently resolved from the base material's package. Do not assume that arbitrary imported/cross-package expression links are supported because texture imports are supported. Fail unsupported graph references explicitly.

Resource translation/analysis now selects effective inherited literal inputs (including class-default/archetype constants) when the material omits an input, respecting UseConstant and whole-input authored overrides. This is separate from the legacy authored-root translation mode, which retains its previous behavior. An inherited active expression root fails explicitly until graph/package resolution is supplied; its donor UIndex is never interpreted in the base material's table. Inherited Expressions arrays are not treated as the base graph's authored inventory. Connected inherited WorldPositionOffset remains rejected by that graph-ownership boundary, while local connected WorldPositionOffset is supported. This does not implement arbitrary inherited graphs; MaterialInstance resolution is separately handled by the host resolver described below.

Selected GameTime/RealTime translation now automatically sets `TranslationResult.ForcesFloatTexCoords` and performs the verified template `half2 TexCoords` -> `float2 TexCoords` substitution, including clocks reached through Panner or Rotator. Inactive/unreachable clocks and a fresh clock-free translation do not inherit this state. This does not change SM5 numeric chunks or the authored `bForceFullPrecision` property. The optional caller-resolved `resourceForcesFloatUsage` override remains for independently known resource outcomes; the ordinary planter capture no longer needs that override simply to account for time usage.

`TranslationResult.UsesSceneDepth` records selected SceneDepth/DestDepth/DepthBiasedAlpha/DepthBiasedBlend/DepthBiasBlend usage; additive `UsesSceneColor` records selected SceneTexture/DestColor/DepthBiasedBlend/DepthBiasBlend usage. Both are recomputed per translation, not inherited from a supplied cooked resource. They are diagnostic state even when later validation fails, not validity/completeness certificates. No cooked resource or caller input is mutated to persist them. The ordinary resource path rejects opaque/masked/softmasked scene reads unless `bUsedWithMaterialEffect` is true; decals independently reject scene color but not scene depth. Do not apply ShaderClient's opt-in opaque-depth diagnostic suppression here. No additional consumer pipeline step is required for these status properties.

SceneDepth and SceneTexture use screen-aligned UVs when disconnected, full-precision explicit scalar/float2 UV coercion, and reject vertex use or unmasked float3/float4 coordinates. SceneTexture supports SceneTex_Lighting only. Explicit aligned coordinates use ScreenAlignedUV; explicit unaligned coordinates apply DynamicScale.xy unless an aligned ScreenPosition was already compiled anywhere in the same property. The compiler owns this order-dependent state and resets it between properties. Scene textures use framebuffer bindings, not user texture metadata/resource slots. PixelDepth is inline ScreenPosition.w and sets neither scene-read flag. DestDepth/DestColor use PreviousDepth/PreviousLighting and the corresponding scene-read flags. The verified SceneDepth/PixelDepth/DestDepth callbacks ignore their Normalize options. These additions do not enable production integration, prove native bytecode/rendering parity, or complete remaining graph coverage.

DepthBiasedAlpha/DepthBiasedBlend are pixel-only helper expressions, use ordinary numeric precision, and do not introduce user-texture inputs or a consumer pipeline phase. Alpha defaults1/Bias.5/BiasScale1 and masks vector Alpha toR; Blend defaults RGB0/Alpha1/BiasScale1, requires connected Bias, requires scalar Alpha and appends it to the helper's RGB result. Normalize is ignored. Failed coercion can leave diagnostic scene-read flags set, but HLSL/cache output is withheld: flags alone must never authorize compilation.

The separate texture-derived DepthBiasBlend is now supported through the same metadata-only texture input path. It requires a2D/cube reference and the existing dimension-appropriate coordinate width, unconditionally unpacks texture metadata, then composes destination/depth reads, ordered bias arithmetic, Clamp/Lerp and two translator If calls. Bias is optional (disconnected starts from1, unlike DepthBiasedBlend); BiasScale defaults1. It is not a named texture parameter, does not consume static normal-parameter overrides, and ignores Normalize/bDecompressNormalMap. Both scene-read flags and ordinary resource gates apply. No new adapter phase, texture payload I/O, production activation or installation permission follows from this addition.

### Metadata-only textures

FluidNormal's additive input hook is `ResolvedMaterialCompilationInput.EngineDefaultTexture`, supplied with optional `engineDefaultTexture` on the input or legacy Material compiler constructor. Supply the caller-resolved engine default texture identity as an existing Texture2D or TextureRenderTarget2D export/import in the **base graph package's** table, not an export from the requested instance or a donor package. The compiler validates table ownership, does not add imports, and does not perform engine-default discovery. Null remains valid for graphs that do not select FluidNormal; a selected FluidNormal without this reference fails explicitly. Supply metadata through the same authoritative `getTextureMetadata` provider. This is a resource-identity hook, not a request to load the texture payload or to apply runtime parameter overrides. Retain the compilation input and keep its base package/table stable throughout the operation.

Font sampling adds separate optional `WorldDefaultTexture` / `worldDefaultTexture` to the same input and legacy constructor, with identical base-package reference validation. It is required only for a selected **null font page**, not for missing fonts or out-of-range pages. Never substitute EngineDefaultTexture: shipping callbacks use different default identities. Optional `getFontTexturePages: Func<IEntry,IReadOnlyList<IEntry>>` is available on both compiler constructors. Resolve each original Font reference to an ordered page list whose selected texture identities are already registered in the base graph package; null page entries retain their world-default meaning. A supplied provider is authoritative (null result fails), snapshotted once per font per translation. Without it only already-loaded local Font.Textures properties are read; imports/unloaded fonts fail without autoload or donor lookup. Page lists do not require font glyphs, image data, mip data or decoding. Selected page texture metadata still uses the ordinary provider. Font parameters produce ordinary/named **texture** uniform expressions, not a new font binary uniform type. Future effective instance font/page overrides should be resolved at this hook by the caller; the compiler does not resolve them now.

TextureCompilationMetadata contains dimension (2D/cube), CompressionSettings, UnpackMin, UnpackMax, and optional `Vector2? FlipBookScale`; no mip, image, bulk-data, texture binary, or TFC payload is required. Existing construction remains source-compatible: optional `flipBookScale` is appended after unpackMax, and `FromProperties` accepts it after properties. FlipBookSample requires a caller-supplied effective scale; do not invoke ConditionalPostLoad or decode a flipbook to get it. Native scale is1/HorizontalImages and1/VerticalImages without clamping, but current translation explicitly rejects nonfinite scale. The loaded-export adapter does not infer it, and FromProperties only snapshots an explicitly supplied scale. Ordinary textures ignore this optional field. Runtime flipbook offset/frame state is a numeric uniform, not compile-time time/frame data in the metadata. When an authoritative metadata provider is supplied, a null result fails translation; there is no automatic loaded-export fallback.

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

`ShouldCacheMaterialContext.DecalFactoryFallback` is the existing nullable native resource virtual +0xa8 result, not the bUsedWithDecals flag. Default context construction now supplies false for Material and true for DecalMaterial. Both velocity shader stages reject a true result even for masked/special/two-sided eligible inputs; the same result also participates in the existing decal vertex-factory fallback gates. Explicit derived-resource contexts must supply the actual result, or null if unresolved. An independent earlier exclusion may settle the decision without that value. Requested MaterialInstance provenance does not substitute for the base graph's resource class; the normal adapter needs no new argument.

Advanced explicit ShaderCompileCandidate inputs now have optional `IsCompilationEnvironmentResolved=true`. False permits a candidate to be excluded by an earlier cache gate, but if otherwise eligible it makes the CompilationEnvironment gate unresolved and withholds all jobs. Observed-only factory targets use this marker and zero compile flags as metadata-only gate inputs; they are never compile jobs or recovered flag profiles. Existing explicitly supplied candidates retain their prior default environment contract.

Particle factory selection uses `TranslationResult.UsesDynamicParameter`, recomputed from the selected graph/static branches. Default planners populate this in their own ShouldCache context and ignore a stale supplied MaterialResource.bUsesDynamicParameter for selection. An advanced caller supplying an explicit ShouldCacheMaterialContext must provide matching translation-derived UsesDynamicParameter and NumUserTexCoords. Do not read an invented export property to bypass translation or update the input resource in the adapter; resource installation remains separate. DynamicParameter currently follows the existing output-index-zero/root-mask contract; general nonzero expression output indices remain unsupported.

LensFlare, Foliage and Fluid usage come from effective/stored bUsedWithLensFlare, bUsedWithFoliage and bUsedWithFluidSurfaces metadata. Fluid's native factory predicate inspects the registered **source filename**, not the type name: both FVelocityVertexShader and FVelocityPixelShader use VelocityShader and are excluded. The planner passes exact registration metadata automatically. Advanced direct calls to `TryEvaluateKnownVertexFactoryShouldCache` can provide optional trailing `shaderTypeName` (resolves a known registration's source) and/or `shaderSourceFileName`; conflicting known metadata or missing/empty source is unresolved when it can affect the result, while independent usage/decal vetoes remain known exclusions. The native case-sensitive VelocityShader source substring is retained exactly. This addition does not change the normal consumer pipeline.

Terrain factory support covers six registrations, cache/environment callbacks and typed parameter layouts; it does not resolve FTerrainMaterialResource, terrain linkage/morph selection, or terrain graphs. Ordinary Material/DecalMaterial contexts remain IsTerrainMaterial=false. Explicit semantic terrain contexts require caller resolution, including the resource ShouldCache hook; merely setting that predicate does not implement a native terrain resource. Terrain-decal callbacks inspect the type name's case-sensitive VertexLightMapPolicy substring (not the source filename), and retain the nullable DecalFactoryFallback/debug-viewmode rules. The compiler emits an unbound canonical zero placeholder for the archive-only Unused field; it is not an input allocation or proof of native placeholder-byte parity.

Every selected job must appear exactly once in cache construction, from the same translation/compiler input and source snapshot. Unknown constructors/bindings, failed compiles, duplicate/missing jobs, or CRC failures must not yield a successful cache. A successful zero-job plan now produces an empty detached map/cache fragment with its identity, static key, ordered uniforms and texture references preserved; shader objects, mesh groups and shader/factory CRC maps are empty. Plans retain exact translation provenance internally even without jobs. Failed zero-job plans, a different translation, unexpected compiled entries and mismatched source snapshots remain rejected. No shader/factory source reads or native compile calls are needed for empty assembly. The adapter must distinguish this permitted subset outcome from Disabled and must never report it as a complete empty shader set or authorization to erase existing shaders.

The adapter should retain phase-specific errors, per-job compiler diagnostics/warnings, planning decisions, and the phase/target at which cancellation or failure occurred. Expected compiler errors are returned in result objects in several paths, but invalid inputs, I/O, missing backend dependencies, or callback failures can also throw. Handle these at the consumer boundary without misreporting them as unsupported material expressions.

ShaderCache construction includes typed shader objects, mesh maps grouped by vertex factory, direct non-mesh map entries, ordered uniform-expression arrays, the static-parameter key, and source CRC maps. No synthetic vertex factory should be assigned to non-mesh shaders. Source CRCs are not bytecode checksums.

The returned cache/container data is detached from packages. Some uniform-expression tree objects are shared with the translation; do not mutate published results or treat them as a fully deep-copied, thread-safe object graph. Keep translation/planning/results alive through assembly; do not try to reconstruct provenance from serialized job DTOs with the current API.

Optional `CompareUniformExpressionsWithShaderMap(translation, expectedMap)` adds an independent check of all six ordered uniform arrays: pixel vectors/scalars, 2D/cube textures, vertex vectors/scalars. Result `TranslationSucceeded`, aggregate top-level counts, `Differences`, `Errors`, and `MatchesExpectedUniformExpressions` are diagnostic only; `IsCompleteShaderSet` stays false. Count, stage, ordinal, concrete class, serialized leaf fields and recursive ordered children remain distinct. Null arrays/entries, unsupported classes, inconsistent type tags, invalid names/slots/opcodes, cycles and bounded-depth/visit-limit failures are not silently skipped, including unmatched trailing entries. Twenty translator-emitted uniform classes are supported; this is not a general cross-game comparer.

Important limits: stored texture indices compare resource slots, not texture UIndexes, resolved paths or native transient pointers. Native texture equality can instead compare both nonnull transient default pointers, so a stored-tree match is not full native texture identity. Parameter names use NameReference case/Number semantics, not shader-registry ordinal-name semantics. Native LE3 constant/parameter identity uses UCOMISS/JNZ without a parity branch: equal signed zero and unordered NaN lanes pass. This relation is not transitive mathematical equivalence and does not prove CPU/GPU evaluation or exceptional-float shader compilation. The translator's pool now follows first matching native identity and reuses a matching code chunk only within the current material property; caller inputs are not changed. No production activation or new mandatory adapter step is implied.

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
    bool resourceForcesFloatUsage = false,
    Func<IEntry, IReadOnlyList<IEntry>> metadataForFontPages = null)
{
    ArgumentNullException.ThrowIfNull(metadataForTexture);
    var compiler = new Compiler(input, getTextureMetadata: metadataForTexture,
        getFontTexturePages: metadataForFontPages);
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
    return fragment; // Detached; IsCompleteShaderSet is membership only, never installation permission.
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

The supplied static set retains a nonzero BaseMaterialId when the map's explicit resource ID is assigned. If the resolved base ID is empty, detached assembly initializes it from the nonempty resource ID, matching the ordinary native initialization rule; the input/static snapshot is not mutated. Do not overwrite a nonzero independent base ID or invent a parent/instance key. Native cache lookup uses static parameters plus platform before completeness; matching membership alone does not establish lookup identity or resource validity. The compiler's current static selection stage binds supported entries by first full-name match; bOverride and expression GUID do not filter selection at that stage. The resolver therefore must supply already-merged effective values.

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
- Successful fragment outcomes retain ordered texture references and the compiler's completeness flag without saving/installing anything.
- Test seams permit orchestration tests without a game installation/native compiler; optional offline integration tests are explicit and machine-local, not mandatory installed-game corpus scans.
- Do not hardcode current type/factory counts, duplicate ShouldCache/macros/flags, silently skip unsupported nodes, or pretend parent compilation resolves an instance.
- Preserve unrelated repository changes; no commits/push/staging unless the user asks. Compiler code comments must not reference external engine source code.
- Report the adapter's location, public host interface, disabled gate, tests performed, and deferred resolver/installer work back to the user. Record any needed contract changes for coordination with the compiler work.

## Roadmap / coordination

Consumer work can proceed now: interface, disabled gate, request validation, orchestration, metadata/source sessions, diagnostics, progress/cancellation, and test seams. The compiler work continues on independently verified required sets/live registry coverage, derived-resource semantics, graph/uniform/bytecode parity, and eventually user-approved runtime validation. The observed static factory inventory is accounted for, not a general runtime certificate. MaterialInstance resolution and production installation remain explicit later phases.

Update this document whenever a public input/result contract or activation boundary changes. Inventory-only progress belongs in the main compiler handoff. Neither document is a promise that current output can replace a shipping shader set.
## Custom/CustomTexture status (2026-10-06)

Custom and CustomTexture now translate, so 112/112 runtime expression classes are handled. No new caller arguments or result APIs were added. CustomTexture needs only the texture's class and no texture metadata. Its ordinary texture uniform gets its global texture index and sampler slot only when a Custom node uses it, so `UniformExpressionTextures` ordering follows that use. Native Custom declares texture inputs as `sampler2D Name` with no paired sampler argument. Authored `tex2D(Name, uv)` bodies therefore fail D3D compilation with an undeclared `NameSampler`; this is a returned compile diagnostic, not an adapter error.

## Consumer adapter implementation (2026-10-06)

The adapter lives in the host application, not LegendaryExplorerCore, so no facade names are frozen into LEC: `LegendaryExplorer/LegendaryExplorer/Tools/MaterialShaderCompilation/MaterialShaderCompilationService.cs`, namespace `LegendaryExplorer.Tools.MaterialShaderCompilation`.

- `IMaterialShaderCompilationService.CompileMaterialAsync(request, progress, cancellationToken)` returns a `MaterialShaderCompileResult` with outcome `Disabled`, `Unsupported`, `Failed`, `Cancelled` or `SucceededFragment`. The result records the phase reached, the failing job target, returned errors, any unexpected exception, `IsBackendUnavailable`, per-job diagnostics (warnings included), planned targets, `IsZeroJobPlan`, the compiler's `IsCompleteShaderSet` (reported separately from success), and the real translation, plan, compiled shaders and fragment.
- **Activation gate:** `MaterialShaderCompilationOptions.Enabled` defaults to false. A disabled request returns `Disabled` before any validation, source capture, metadata read, compile or mutation. Nothing in the UI or settings enables it.
- **Request validation:** LE3 base `Material`/`DecalMaterial` only. Other games, MaterialInstances and non-material exports return `Unsupported`. Missing resource, empty resource GUID or friendly name, and missing source or metadata providers return `Failed`/Validation.
- **Sources:** `SourceProvider` is invoked once after validation, and that single snapshot is used for translation, every job and assembly.
- **Execution:** jobs compile sequentially inside one background task. Cancellation is checked between phases and jobs; in-flight D3D compiles are not interrupted. Progress is reported as completed/selected jobs.
- **Test seam:** `IMaterialShaderCompilerBackend` / `IMaterialShaderCompilerSession`. The production `LE3MaterialShaderCompilerBackend` uses one compiler per request with `TranslateMaterialResourceHlsl` → `BuildKnownSubsetCompilePlan` → `CompileShader` → `BuildShaderCacheFragment`.
- **Deferred:** `IMaterialShaderInstanceResolver` and `IMaterialShaderFragmentInstaller` are declared interfaces only, with no implementations.
- **Tests:** `LegendaryExplorer.Tests/Tools/MaterialShaderCompilation/MaterialShaderCompilationServiceTests.cs`, 56 orchestration tests that need no game install. One opt-in integration test is skipped unless `LE3_SHADER_IT_PACKAGE`, `LE3_SHADER_IT_MATERIAL` and `LE3_SHADER_IT_SOURCES` are set.

## MaterialInstance resolution (2026-10-06)

`LE3MaterialInstanceResolver` (same folder as the adapter) implements `IMaterialShaderInstanceResolver`. It follows the shipping LE3 rules, which were checked against the shipping game:

- **Cooked static parameters are authoritative.** The game never recomputes them from the parent chain at load time. Recomputing happens only in the editor, after a property edit.
- **Selecting the resource**: starting from the requested instance, the first instance with `bHasStaticPermutationResource` owns the permutation. Its serialized `SM3StaticParameterSet` and `SM3StaticPermutationResource` are used. If no instance has the flag, the base Material's `SM3MaterialResource` is used with authored static defaults.
- **Missing parent:** PostLoad clears `bHasStaticPermutationResource` only when Parent is null. The resolver reports that case as Unsupported, because the game would render it with a default material.
- **Usage and material-level properties** (bUsedWith*, decals, distortion, blend/lighting) come from the base UMaterial through the resource's Material pointer. The compiler already reads them from `BaseMaterial`.
- **Imported parents** require a caller-supplied `Func<ImportEntry, ExportEntry>`. The resolver performs no package discovery. Cycles, invalid references, unrelated resolved imports, and flagged owners with no serialized permutation fail. A `BaseMaterialId` that differs from the base resource ID is reported as a warning only; the game does not revalidate it.
- **Not modelled:** On load, a constant instance re-syncs static parameter *names* to base-material expressions by ExpressionGUID. This should be a no-op on cooked data.

Adapter integration:
- Set `MaterialShaderCompilationOptions.InstanceResolver`. Without it, instance requests stay Unsupported.
- An instance request must not supply `ResolvedStaticParameters`.
- `Resource` defaults to the resolved permutation resource.
- The compilation input uses the resolved base Material and static set, with the requested instance as provenance. `MaterialShaderCompileResult.InstanceResolution` carries the chain, owner and warnings.
- **When an instance shares an ancestor's permutation:** `SharesAncestorPermutation` is true, and compiling it produces that ancestor's (or the base's) shaders, not a new permutation.
- For imported chains, default textures, texture metadata and font pages must refer to the resolved base Material's package.

Tests: `LE3MaterialInstanceResolverTests.cs` (43 cases) and 24 added service cases in `LegendaryExplorer.Tests/Tools/MaterialShaderCompilation/`. All 122 adapter/resolver tests pass; the one opt-in integration test is skipped.

## Parity round changes (2026-10-06)

No API was added or removed, but caller-visible behaviour changed:
- **Effects-owned materials** (Outer is RvrEffectsMaterialUser) now plan without a caller resource predicate. Only FLocalVertexFactory is excluded, and only when the owner's `m_bSupportsStaticMeshes` is false.
- **Connected WorldPositionOffset** and the TC_Grayscale/TC_OneBitAlpha/TC_BC7 texture compressions translate instead of failing closed.
- **Texture order:** `UniformExpressionTextures` is now in first-sampler-access order.
- **Colour defaults:** unconnected DiffuseColor/SpecularColor default to black for ordinary Material resources and Pow22 byte128 grey for DecalMaterial resources; stored constants apply only when UseConstant is active. Resource class is separate from decal usage flags.

The previous full non-Effects parity run measured98.6% fully byte-identical shipped base materials, before the resource-dispatch/profile fixes above. That is a historical baseline, not a refreshed global result. Current bounded verification is recorded at the top of this document; bytecode equality alone does not certify ordered uniform values or runtime installation. Production remains disabled.
