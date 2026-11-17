using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using LegendaryExplorerCore.Unreal.Collections;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorerCore.Shaders
{
    /// <summary>
    /// LE3 material compilation entry point. Graph analysis and the first exact HLSL expression subset
    /// are intentionally independent of the game's cooked material resource and shader cache: those do
    /// not contain the authored graph. Compile planning, D3D bytecode compilation and reflection are
    /// supported separately; shader-map construction still requires verified serialization rules.
    /// </summary>
    public sealed class LE3MaterialShaderCompiler
    {
        // FMaterial::CompileProperty order in LE3 MassEffect3.exe 11.3.2, as called by
        // FHLSLMaterialTranslator::Translate (0x7ff74ab58be0). Do not include other authored
        // UMaterial inputs here until the LE3 translator is shown to compile them.
        private static readonly string[] MaterialInputs =
        [
            "Normal", "EmissiveColor", "DiffuseColor", "DiffusePower", "SpecularColor",
            "SpecularPower", "Opacity", "OpacityMask", "Distortion", "TwoSidedLightingMask",
            "CustomLighting", "CustomLightingDiffuse", "AnisotropicDirection",
            "WorldPositionOffset", "WrapLightingCoefficients"
        ];

        private readonly ExportEntry material;
        private readonly StaticParameterSet staticParameters;
        private readonly Func<ImportEntry, ExportEntry> resolveTextureImport;
        private readonly Func<IEntry, TextureCompilationMetadata> getTextureMetadata;
        private readonly Func<IEntry, IReadOnlyList<IEntry>> getFontTexturePages;

        public enum TextureDimension
        {
            Texture2D,
            TextureCube
        }

        /// <summary>
        /// Immutable metadata used by the current texture translation subset. Contains no export,
        /// package, mip, pixel, or bulk-data payload. Texture identity remains the graph's original
        /// package reference; matching metadata does not make two textures the same resource.
        /// </summary>
        public sealed class TextureCompilationMetadata
        {
            public TextureDimension Dimension { get; }
            public string CompressionSettings { get; }
            public Vector4 UnpackMin { get; }
            public Vector4 UnpackMax { get; }
            /// <summary>Optional effective per-texture flipbook UV scale, without animation or image payloads.</summary>
            public Vector2? FlipBookScale { get; }

            public TextureCompilationMetadata(TextureDimension dimension, string compressionSettings = "TC_Default",
                Vector4? unpackMin = null, Vector4? unpackMax = null, Vector2? flipBookScale = null)
            {
                if (!Enum.IsDefined(dimension)) throw new ArgumentOutOfRangeException(nameof(dimension));
                ArgumentException.ThrowIfNullOrWhiteSpace(compressionSettings);
                Dimension = dimension;
                CompressionSettings = compressionSettings;
                UnpackMin = unpackMin ?? Vector4.Zero;
                UnpackMax = unpackMax ?? Vector4.One;
                FlipBookScale = flipBookScale;
            }

            /// <summary>
            /// Snapshots an already-read LE3 property collection; does not access an export's data.
            /// Missing properties have the same defaults as the loaded-export translation path.
            /// The caller is responsible for supplying the correct effective texture properties.
            /// </summary>
            public static TextureCompilationMetadata FromProperties(TextureDimension dimension, PropertyCollection properties,
                Vector2? flipBookScale = null)
            {
                ArgumentNullException.ThrowIfNull(properties);
                return new TextureCompilationMetadata(dimension,
                    properties.GetProp<EnumProperty>("CompressionSettings")?.Value.Name ?? "TC_Default",
                    ReadRange("UnpackMin", 0f), ReadRange("UnpackMax", 1f), flipBookScale);

                Vector4 ReadRange(string name, float fallback) => new(
                    properties.GetProp<FloatProperty>(name, 0)?.Value ?? fallback,
                    properties.GetProp<FloatProperty>(name, 1)?.Value ?? fallback,
                    properties.GetProp<FloatProperty>(name, 2)?.Value ?? fallback,
                    properties.GetProp<FloatProperty>(name, 3)?.Value ?? fallback);
            }

            /// <summary>
            /// Compatibility adapter for an already-loaded LE3 texture export. Reads properties once,
            /// without parsing texture binary/mips or loading an unloaded export.
            /// </summary>
            public static TextureCompilationMetadata FromExport(ExportEntry texture)
            {
                ArgumentNullException.ThrowIfNull(texture);
                if (!texture.Game.IsLEGame3() || (!texture.IsA("Texture2D") && !texture.IsA("TextureCube")))
                    throw new ArgumentException("An LE3 Texture2D or TextureCube export is required.", nameof(texture));
                if (!texture.IsDataLoaded())
                    throw new InvalidOperationException("Texture export data is not loaded; supply texture compilation metadata instead.");
                return FromProperties(texture.IsA("TextureCube") ? TextureDimension.TextureCube : TextureDimension.Texture2D,
                    texture.GetProperties());
            }
        }

        /// <summary>
        /// Resolved boundary for future MaterialInstance parent-chain/permutation resolution. The
        /// caller supplies the base graph and merged static parameters; this class does not resolve
        /// instance parents, determine whether an instance needs a shader map, or apply runtime
        /// scalar/vector/texture overrides. Static parameters are snapshotted, including override flags.
        /// </summary>
        public sealed class ResolvedMaterialCompilationInput
        {
            private readonly StaticParameterSet parameters;
            public ExportEntry BaseMaterial { get; }
            public ExportEntry RequestedMaterialInstance { get; }
            public ExportEntry RequestedMaterial => RequestedMaterialInstance ?? BaseMaterial;
            /// <summary>Optional caller-resolved engine default texture identity in the base
            /// graph's package table. No engine/package lookup or texture payload loading occurs.</summary>
            public IEntry EngineDefaultTexture { get; }
            /// <summary>Optional caller-resolved world-info default used for null font atlas pages.
            /// It is distinct from the engine default; neither identity is inferred from the other.</summary>
            public IEntry WorldDefaultTexture { get; }

            public ResolvedMaterialCompilationInput(ExportEntry baseMaterial, StaticParameterSet resolvedStaticParameters = null,
                ExportEntry requestedMaterialInstance = null, IEntry engineDefaultTexture = null, IEntry worldDefaultTexture = null)
            {
                ArgumentNullException.ThrowIfNull(baseMaterial);
                if (!baseMaterial.Game.IsLEGame3() || !baseMaterial.IsA("Material"))
                    throw new ArgumentException("An LE3 base Material export is required.", nameof(baseMaterial));
                if (requestedMaterialInstance != null && (!requestedMaterialInstance.Game.IsLEGame3() ||
                    !requestedMaterialInstance.IsA("MaterialInstance")))
                    throw new ArgumentException("The optional requested export must be an LE3 MaterialInstance.", nameof(requestedMaterialInstance));
                ValidateDefaultTextureReference(baseMaterial, engineDefaultTexture, nameof(engineDefaultTexture));
                ValidateDefaultTextureReference(baseMaterial, worldDefaultTexture, nameof(worldDefaultTexture));
                BaseMaterial = baseMaterial;
                RequestedMaterialInstance = requestedMaterialInstance;
                EngineDefaultTexture = engineDefaultTexture;
                WorldDefaultTexture = worldDefaultTexture;
                parameters = CopyStaticParameters(resolvedStaticParameters);
            }

            /// <summary>Detached copy; changing it cannot mutate this compilation input.</summary>
            public StaticParameterSet GetStaticParametersSnapshot() => CopyStaticParameters(parameters);
        }

        public ResolvedMaterialCompilationInput CompilationInput { get; }

        /// <param name="material">An LE3 Material or DecalMaterial with its expression graph loaded.</param>
        /// <param name="staticParameters">Effective resolved static parameters, including inherited values,
        /// for an instance permutation, or null for authored defaults. Switch/mask/normal entries bind by
        /// first full name match; bOverride and expression GUID do not filter this compilation stage.</param>
        /// <param name="resolveTextureImport">Resolves an imported default texture to its loaded LE3 export so unpack properties can be read.</param>
        /// <param name="getTextureMetadata">Optional authoritative metadata provider for original texture references,
        /// including imports and unloaded exports. Called once per selected texture per translation. Null results
        /// fail translation; no loaded-export/import-resolver fallback is used when this provider is supplied.</param>
        /// <param name="engineDefaultTexture">Optional resolved engine default texture reference in
        /// this graph's package table. Required by FluidNormal; metadata still comes from the normal provider.</param>
        /// <param name="worldDefaultTexture">Optional resolved world-info default texture reference
        /// in the base graph's table, used only for null font atlas pages.</param>
        /// <param name="getFontTexturePages">Optional authoritative font-page metadata provider.
        /// Returns atlas texture references in this graph's table; null pages use worldDefaultTexture.
        /// Null results fail with no automatic font loading or provider fallback.</param>
        public LE3MaterialShaderCompiler(ExportEntry material, StaticParameterSet staticParameters = null,
            Func<ImportEntry, ExportEntry> resolveTextureImport = null,
            Func<IEntry, TextureCompilationMetadata> getTextureMetadata = null, IEntry engineDefaultTexture = null,
            IEntry worldDefaultTexture = null, Func<IEntry, IReadOnlyList<IEntry>> getFontTexturePages = null)
            : this(new ResolvedMaterialCompilationInput(material, staticParameters, engineDefaultTexture: engineDefaultTexture,
                worldDefaultTexture: worldDefaultTexture), resolveTextureImport, getTextureMetadata, getFontTexturePages)
        {
        }

        public LE3MaterialShaderCompiler(ResolvedMaterialCompilationInput input,
            Func<ImportEntry, ExportEntry> resolveTextureImport = null,
            Func<IEntry, TextureCompilationMetadata> getTextureMetadata = null,
            Func<IEntry, IReadOnlyList<IEntry>> getFontTexturePages = null)
        {
            CompilationInput = input ?? throw new ArgumentNullException(nameof(input));
            material = input.BaseMaterial;
            staticParameters = input.GetStaticParametersSnapshot();
            this.resolveTextureImport = resolveTextureImport;
            this.getTextureMetadata = getTextureMetadata;
            this.getFontTexturePages = getFontTexturePages;
        }

        private static void ValidateDefaultTextureReference(ExportEntry baseMaterial, IEntry texture, string argumentName)
        {
            if (texture != null && (!ReferenceEquals(texture.FileRef, baseMaterial.FileRef) ||
                !baseMaterial.FileRef.TryGetEntry(texture.UIndex, out var registeredTexture) ||
                !ReferenceEquals(registeredTexture, texture) || (!texture.IsA("Texture2D") && !texture.IsA("TextureRenderTarget2D"))))
                throw new ArgumentException("The default texture must be a registered 2D texture reference in the base material's package.", argumentName);
        }

        private static StaticParameterSet CopyStaticParameters(StaticParameterSet source)
        {
            if (source == null) return null;
            return new StaticParameterSet
            {
                BaseMaterialId = source.BaseMaterialId,
                StaticSwitchParameters = (source.StaticSwitchParameters ?? []).Select(p => p == null
                    ? throw new ArgumentException("Static switch entries cannot be null.", nameof(source))
                    : new StaticParameterSet.StaticSwitchParameter { ParameterName = p.ParameterName, Value = p.Value,
                        bOverride = p.bOverride, ExpressionGUID = p.ExpressionGUID }).ToArray(),
                StaticComponentMaskParameters = (source.StaticComponentMaskParameters ?? []).Select(p => p == null
                    ? throw new ArgumentException("Static component-mask entries cannot be null.", nameof(source))
                    : new StaticParameterSet.StaticComponentMaskParameter { ParameterName = p.ParameterName, R = p.R,
                        G = p.G, B = p.B, A = p.A, bOverride = p.bOverride, ExpressionGUID = p.ExpressionGUID }).ToArray(),
                NormalParameters = (source.NormalParameters ?? []).Select(p => p == null
                    ? throw new ArgumentException("Normal parameter entries cannot be null.", nameof(source))
                    : new StaticParameterSet.NormalParameter { ParameterName = p.ParameterName, CompressionSettings = p.CompressionSettings,
                        bOverride = p.bOverride, ExpressionGUID = p.ExpressionGUID }).ToArray(),
                TerrainWeightParameters = (source.TerrainWeightParameters ?? []).Select(p => p == null
                    ? throw new ArgumentException("Terrain weight entries cannot be null.", nameof(source))
                    : new StaticParameterSet.TerrainWeightParameter { ParameterName = p.ParameterName, WeightmapIndex = p.WeightmapIndex,
                        bOverride = p.bOverride, ExpressionGUID = p.ExpressionGUID }).ToArray()
            };
        }

        public sealed class GraphAnalysis
        {
            public bool IncludesMaterialResourceInputs { get; internal init; }
            /// <summary>Connected material inputs and the expression selected at each root.</summary>
            public IReadOnlyDictionary<string, ExportEntry> ConnectedInputs { get; internal init; }

            /// <summary>Expressions visited through connected material inputs, after static-switch selection.</summary>
            public IReadOnlyList<ExportEntry> ReachableExpressions { get; internal init; }

            /// <summary>Authored expressions that no selected material input reaches.</summary>
            public IReadOnlyList<ExportEntry> UnreachableExpressions { get; internal init; }

            /// <summary>Invalid references, unsupported cross-package links, and graph cycles.</summary>
            public IReadOnlyList<string> Errors { get; internal init; }

            public bool IsValid => Errors.Count == 0;
        }

        public enum MaterialValueType
        {
            Scalar = 1,
            Vector2 = 2,
            Vector3 = 3,
            Vector4 = 4
        }

        public sealed class TranslationResult
        {
            /// <summary>True when the verified SM5 material-resource input dispatch was applied,
            /// including defaults and selection-color operations; not a native-parity certificate.</summary>
            public bool IncludesMaterialResourceInputs { get; internal init; }
            /// <summary>Native GetForceFloatUsage outcome used for the literal TexCoords
            /// template substitution. Independent of SM5 numeric chunk precision.</summary>
            public bool ForcesFloatTexCoords { get; internal init; }
            public LE3ShaderSourceSnapshot SourceSnapshot { get; internal set; }
            /// <summary>Native PS3-only half-type branch, exposed for primitive translation tests.
            /// LE3 SM5 compile jobs reject it; SM5 native FMaterial::Compile always selects float.</summary>
            public bool UsesHalfTypes { get; internal init; }
            internal ExportEntry Material { get; init; }
            internal LE3MaterialShaderCompiler Compiler { get; init; }
            public ResolvedMaterialCompilationInput CompilationInput { get; internal init; }
            public GraphAnalysis Graph { get; internal init; }
            public IReadOnlyDictionary<string, string> PropertyCode { get; internal init; }
            public IReadOnlyList<string> Errors { get; internal init; }
            public IReadOnlyList<MaterialUniformExpression> UniformPixelVectorExpressions { get; internal init; } = [];
            public IReadOnlyList<MaterialUniformExpression> UniformPixelScalarExpressions { get; internal init; } = [];
            public IReadOnlyList<MaterialUniformExpressionTexture> Uniform2DTextureExpressions { get; internal init; } = [];
            public IReadOnlyList<MaterialUniformExpressionTexture> UniformCubeTextureExpressions { get; internal init; } = [];
            /// <summary>
            /// Texture UIndexes in the order addressed by <see cref="MaterialUniformExpressionTexture.TextureIndex"/>.
            /// Install this list into the target <see cref="MaterialResource.UniformExpressionTextures"/>.
            /// </summary>
            public IReadOnlyList<int> UniformExpressionTextures { get; internal init; } = [];
            public IReadOnlyList<MaterialUniformExpression> UniformVertexVectorExpressions { get; internal init; } = [];
            public IReadOnlyList<MaterialUniformExpression> UniformVertexScalarExpressions { get; internal init; } = [];
            public uint NumUserTexCoords { get; internal init; }
            /// <summary>Transform requirements emitted by supported nodes in this translation (currently unmirror/world).</summary>
            public uint UsingTransforms { get; internal init; }
            public bool UsesVertexColor { get; internal init; }
            /// <summary>Current pixel translation requested lightmap UV access; not inherited
            /// from a cooked material resource. Maps to MaterialResource.bUsesLightmapUVs.</summary>
            public bool UsesLightmapUVs { get; internal init; }
            /// <summary>Current selected translation uses the particle dynamic-parameter primitive;
            /// not inherited from a supplied cooked material resource.</summary>
            public bool UsesDynamicParameter { get; internal init; }
            /// <summary>Selected translation emitted a GameTime or RealTime uniform; not a cooked-resource flag mapping.</summary>
            public bool UsesTime { get; internal init; }
            /// <summary>Selected pixel translation invoked a scene-depth primitive; recomputed rather
            /// than inherited from a supplied cooked resource. Not a completeness claim.</summary>
            public bool UsesSceneDepth { get; internal init; }
            /// <summary>Selected pixel translation invoked a supported scene-color primitive;
            /// recomputed rather than inherited from a supplied cooked resource.</summary>
            public bool UsesSceneColor { get; internal init; }
            public string MaterialHlsl { get; internal init; }
            public bool Success => Errors.Count == 0 && MaterialHlsl != null;
        }

        public enum ShaderStage
        {
            Vertex,
            Pixel
        }

        /// <summary>
        /// Engine registration metadata required to compile one shader permutation. This is supplied
        /// by a shader-type registry; it must not be inferred from the serialized shader class name.
        /// </summary>
        public sealed record ShaderCompileTarget(
            string ShaderTypeName,
            string SourceFileName,
            string EntryPoint,
            ShaderStage Stage,
            uint CompileFlags,
            string VertexFactoryTypeName = null,
            string VertexFactoryShaderFileName = null)
        {
            public string Profile => Stage == ShaderStage.Vertex ? "vs_5_0" : "ps_5_0";
        }

        /// <summary>
        /// Metadata recovered from an LE3 shader-type registration. Compilation flags and macros
        /// remain permutation/environment inputs; they are not inferred from the shader stage.
        /// </summary>
        public sealed record ShaderTypeRegistration(string Name, string SourceFileName, string EntryPoint,
            ShaderStage Stage, bool IsMeshMaterial)
        {
            public ShaderCompileTarget CreateTarget(uint compileFlags, string vertexFactoryTypeName = null,
                string vertexFactoryShaderFileName = null)
                => new(Name, SourceFileName, EntryPoint, Stage, compileFlags,
                    vertexFactoryTypeName, vertexFactoryShaderFileName);
        }

        public sealed record VertexFactoryRegistration(string Name, string SourceFileName,
            bool UsedWithMaterials, bool SupportsStaticLighting, bool? SupportsDynamicLighting = null,
            bool? SupportsPrecisePreviousPosition = null)
        {
            public ShouldCacheVertexFactoryContext CreateCacheContext() => new(SupportsStaticLighting, UsedWithMaterials,
                SupportsDynamicLighting, SupportsPrecisePreviousPosition);
        }

        private sealed record FoggedBasePassVertexPolicy(string LightmapPolicy, string Density);
        private static readonly IReadOnlyDictionary<string, FoggedBasePassVertexPolicy> KnownFoggedBasePassVertexTypes = CreateKnownFoggedBasePassVertexTypes();
        private static readonly IReadOnlyDictionary<string, ShaderTypeRegistration> KnownShaderTypes = CreateKnownShaderTypes();

        private static IReadOnlyDictionary<string, FoggedBasePassVertexPolicy> CreateKnownFoggedBasePassVertexTypes()
        {
            var result = new Dictionary<string, FoggedBasePassVertexPolicy>(StringComparer.Ordinal);
            void Add(string policy, params string[] densities)
            {
                foreach (string density in densities)
                    result.Add("TBasePassVertexShader" + policy + "F" + density + "DensityPolicy", new(policy, density));
            }
            // Exact initializer-backed pairs only; not the Cartesian product of policy templates.
            Add("FCustomSimpleLightMapTexturePolicy", "Cone", "Constant", "LinearHalfspace", "Sphere");
            Add("FCustomSimpleVertexLightMapPolicy", "Cone", "Constant", "LinearHalfspace", "Sphere");
            Add("FCustomVectorLightMapTexturePolicy", "Cone", "Constant", "LinearHalfspace", "Sphere");
            Add("FCustomVectorVertexLightMapPolicy", "Cone", "Constant", "LinearHalfspace", "Sphere");
            Add("FDirectionalLightLightMapPolicy", "Cone", "Constant", "LinearHalfspace", "Sphere");
            Add("FDirectionalLightMapTexturePolicy", "Cone", "Constant", "LinearHalfspace", "Sphere");
            Add("FDirectionalVertexLightMapPolicy", "Cone", "Constant", "LinearHalfspace", "Sphere");
            Add("FNoLightMapPolicy", "Cone");
            Add("FSHLightLightMapPolicy", "Cone", "Constant", "LinearHalfspace", "Sphere");
            Add("FSimpleLightMapTexturePolicy", "Cone", "Constant", "LinearHalfspace", "Sphere");
            Add("FSimpleVertexLightMapPolicy", "Cone", "Constant", "LinearHalfspace", "Sphere");
            return result;
        }
        private static readonly IReadOnlyList<VertexFactoryRegistration> KnownVertexFactories = Array.AsReadOnly(new[]
        {
            new VertexFactoryRegistration("FLocalVertexFactory", "LocalVertexFactory", true, true, true, true),
            new VertexFactoryRegistration("FGPUSkinVertexFactory", "GpuSkinVertexFactory", true, false, true, false),
            new VertexFactoryRegistration("FGPUSkinMorphVertexFactory", "GpuSkinVertexFactory", true, false, true, false),
            new VertexFactoryRegistration("FInstancedStaticMeshVertexFactory", "LocalVertexFactory", true, true, true, true),
            new VertexFactoryRegistration("FGPUSkinDecalVertexFactory", "GpuSkinVertexFactory", true, false, true, false),
            new VertexFactoryRegistration("FLocalDecalVertexFactory", "LocalVertexFactory", true, true, true, false),
            new VertexFactoryRegistration("FGPUSkinMorphDecalVertexFactory", "GpuSkinVertexFactory", true, false, true, false),
            new VertexFactoryRegistration("FSplineMeshVertexFactory", "LocalVertexFactory", true, true, true, true),
            new VertexFactoryRegistration("FParticleInstancedMeshVertexFactory", "MeshInstancedVertexFactory", true, false, true, false),
            new VertexFactoryRegistration("FLocalVertexFactoryApex", "LocalVertexFactory", true, false, false, false),
            new VertexFactoryRegistration("FGPUSkinVertexFactoryApex", "ApexGpuSkinVertexFactory_1Bone", true, false, true, false),
            new VertexFactoryRegistration("FParticleVertexFactory", "ParticleSpriteVertexFactory", true, false, true, false),
            new VertexFactoryRegistration("FParticleDynamicParameterVertexFactory", "ParticleSpriteVertexFactory", true, false, true, false),
            new VertexFactoryRegistration("FParticleSubUVVertexFactory", "ParticleSpriteVertexFactory", true, false, true, false),
            new VertexFactoryRegistration("FParticleSubUVDynamicParameterVertexFactory", "ParticleSpriteVertexFactory", true, false, true, false),
            new VertexFactoryRegistration("FParticleBeamTrailVertexFactory", "ParticleBeamTrailVertexFactory", true, false, true, false),
            new VertexFactoryRegistration("FParticleBeamTrailDynamicParameterVertexFactory", "ParticleBeamTrailVertexFactory", true, false, true, false),
            new VertexFactoryRegistration("FLensFlareVertexFactory", "LensFlareVertexFactory", true, false, false, false),
            new VertexFactoryRegistration("FFoliageVertexFactory", "FoliageVertexFactory", true, true, true, false),
            new VertexFactoryRegistration("FFluidVertexFactory", "FluidVertexFactory", true, true, true, false),
            new VertexFactoryRegistration("FFluidTessellationVertexFactory", "FluidVertexFactory", true, true, true, false),
            new VertexFactoryRegistration("FTerrainVertexFactory", "TerrainVertexFactory", true, true, true, false),
            new VertexFactoryRegistration("FTerrainMorphVertexFactory", "TerrainVertexFactory", true, true, true, false),
            new VertexFactoryRegistration("FTerrainFullMorphVertexFactory", "TerrainVertexFactory", true, true, true, false),
            new VertexFactoryRegistration("FTerrainDecalVertexFactory", "TerrainVertexFactory", true, true, true, false),
            new VertexFactoryRegistration("FTerrainMorphDecalVertexFactory", "TerrainVertexFactory", true, true, true, false),
            new VertexFactoryRegistration("FTerrainFullMorphDecalVertexFactory", "TerrainVertexFactory", true, true, true, false)
        });

        /// <summary>Observed static material-shader registrations for the targeted LE3 binary.
        /// Global shader types are excluded; this inventory does not establish a complete native
        /// required shader set, especially with the separately recovered factory subset.</summary>
        public static IReadOnlyList<ShaderTypeRegistration> GetKnownShaderTypeRegistrations()
            => Array.AsReadOnly(KnownShaderTypes.Values.OrderBy(r => r.Name, StringComparer.Ordinal).ToArray());

        /// <summary>Recovered vertex-factory subset; not the engine's complete registration list.</summary>
        public static IReadOnlyList<VertexFactoryRegistration> GetKnownVertexFactoryRegistrations() => KnownVertexFactories;

        /// <summary>Observed static factory registrations for the targeted LE3 binary, including
        /// unimplemented factories and the non-material LocalShadow registration. Metadata only:
        /// presence here does not provide a cache callback, environment, binder or complete set.</summary>
        public static IReadOnlyList<VertexFactoryRegistration> GetObservedVertexFactoryRegistrations()
            => Array.AsReadOnly(KnownVertexFactories.Concat(new VertexFactoryRegistration[]
            {
                new("FBioSPUSkinVertexFactory", "BioSPUSkinVertexFactory", true, false, true, false),
                new("FLocalShadowVertexFactory", "LocalVertexFactory", false, false, true, false)
            }).ToArray());

        public static bool TryGetKnownVertexFactoryRegistration(string name, out VertexFactoryRegistration registration)
        {
            ArgumentNullException.ThrowIfNull(name);
            registration = GetKnownVertexFactoryRegistrations().FirstOrDefault(r => r.Name == name);
            return registration != null;
        }

        /// <summary>
        /// Explicit material registration inventory, verified against the targeted LE3 binary's
        /// static initializers and packed descriptors. Global shader registrations are excluded.
        /// </summary>
        public static bool TryGetKnownShaderTypeRegistration(string shaderTypeName, out ShaderTypeRegistration registration)
        {
            ArgumentNullException.ThrowIfNull(shaderTypeName);
            return KnownShaderTypes.TryGetValue(shaderTypeName, out registration);
        }

        private static IReadOnlyDictionary<string, ShaderTypeRegistration> CreateKnownShaderTypes()
        {
            var registrations = new Dictionary<string, ShaderTypeRegistration>(StringComparer.Ordinal);
            void Add(string source, ShaderStage stage, params string[] names)
            {
                foreach (string name in names) registrations.Add(name, new(name, source, "Main", stage, IsMeshMaterial: true));
            }
            registrations.Add("FLightFunctionVertexShader", new("FLightFunctionVertexShader", "LightFunctionVertexShader", "Main", ShaderStage.Vertex, false));
            registrations.Add("FLightFunctionPixelShader", new("FLightFunctionPixelShader", "LightFunctionPixelShader", "Main", ShaderStage.Pixel, false));
            registrations.Add("FRadialBlurVertexShader", new("FRadialBlurVertexShader", "RadialBlurScreenShader", "MainRadialBlurVS", ShaderStage.Vertex, false));
            registrations.Add("FRadialBlurPixelShader", new("FRadialBlurPixelShader", "RadialBlurScreenShader", "MainRadialBlurPS", ShaderStage.Pixel, false));
            registrations.Add("FRadialBlurVelocityPixelShader", new("FRadialBlurVelocityPixelShader", "RadialBlurScreenShader", "MainRadialBlurVelocityPS", ShaderStage.Pixel, false));
            Add("BasePassPixelShader", ShaderStage.Pixel,
                "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight", "TBasePassPixelShaderFNoLightMapPolicySkyLight");
            Add("BasePassPixelShader", ShaderStage.Pixel,
                "TBasePassPixelShaderFDirectionalLightMapTexturePolicyNoSkyLight",
                "TBasePassPixelShaderFDirectionalLightMapTexturePolicySkyLight",
                "TBasePassPixelShaderFSimpleLightMapTexturePolicyNoSkyLight",
                "TBasePassPixelShaderFSimpleLightMapTexturePolicySkyLight",
                "TBasePassPixelShaderFSimpleVertexLightMapPolicyNoSkyLight",
                "TBasePassPixelShaderFSimpleVertexLightMapPolicySkyLight");
            Add("BasePassVertexShader", ShaderStage.Vertex,
                "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFNoDensityPolicy",
                "TBasePassVertexShaderFSimpleLightMapTexturePolicyFNoDensityPolicy",
                "TBasePassVertexShaderFSimpleVertexLightMapPolicyFNoDensityPolicy");
            foreach (string policy in new[] { "FDirectionalVertexLightMapPolicy", "FCustomVectorVertexLightMapPolicy", "FCustomSimpleVertexLightMapPolicy" })
            {
                Add("BasePassVertexShader", ShaderStage.Vertex, "TBasePassVertexShader" + policy + "FNoDensityPolicy");
                Add("BasePassPixelShader", ShaderStage.Pixel, "TBasePassPixelShader" + policy + "NoSkyLight", "TBasePassPixelShader" + policy + "SkyLight");
            }
            foreach (string policy in new[] { "FCustomVectorLightMapTexturePolicy", "FCustomSimpleLightMapTexturePolicy" })
            {
                Add("BasePassVertexShader", ShaderStage.Vertex, "TBasePassVertexShader" + policy + "FNoDensityPolicy");
                Add("BasePassPixelShader", ShaderStage.Pixel, "TBasePassPixelShader" + policy + "NoSkyLight", "TBasePassPixelShader" + policy + "SkyLight");
            }
            Add("BasePassVertexShader", ShaderStage.Vertex,
                "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy",
                "TBasePassVertexShaderFNoLightMapPolicyFConstantDensityPolicy",
                "TBasePassVertexShaderFNoLightMapPolicyFLinearHalfspaceDensityPolicy",
                "TBasePassVertexShaderFNoLightMapPolicyFSphereDensityPolicy");
            foreach (string policy in new[] { "FDirectionalLightLightMapPolicy", "FSHLightLightMapPolicy" })
            {
                Add("BasePassVertexShader", ShaderStage.Vertex, "TBasePassVertexShader" + policy + "FNoDensityPolicy");
                Add("BasePassPixelShader", ShaderStage.Pixel, "TBasePassPixelShader" + policy + "NoSkyLight", "TBasePassPixelShader" + policy + "SkyLight");
            }
            foreach (string name in KnownFoggedBasePassVertexTypes.Keys)
                Add("BasePassVertexShader", ShaderStage.Vertex, name);
            Add("DirectionalLightVertexShader", ShaderStage.Vertex, "TLightVertexShaderFDirectionalLightPolicyFNoStaticShadowingPolicy");
            Add("DirectionalLightPixelShader", ShaderStage.Pixel, "TLightPixelShaderFDirectionalLightPolicyFNoStaticShadowingPolicy");
            Add("PointLightPixelShader", ShaderStage.Pixel, "TLightPixelShaderFPointLightPolicyFNoStaticShadowingPolicy");
            Add("PointLightVertexShader", ShaderStage.Vertex, "TLightVertexShaderFPointLightPolicyFNoStaticShadowingPolicy");
            Add("SpotLightVertexShader", ShaderStage.Vertex, "TLightVertexShaderFSpotLightPolicyFNoStaticShadowingPolicy");
            Add("SpotLightPixelShader", ShaderStage.Pixel, "TLightPixelShaderFSpotLightPolicyFNoStaticShadowingPolicy");
            Add("SFXShadowedPointLightVertexShader", ShaderStage.Vertex, "TLightVertexShaderFSFXPointLightPolicyFNoStaticShadowingPolicy");
            Add("SFXShadowedPointLightPixelShader", ShaderStage.Pixel, "TLightPixelShaderFSFXPointLightPolicyFNoStaticShadowingPolicy");
            Add("SphericalHarmonicLightVertexShader", ShaderStage.Vertex, "TLightVertexShaderFSphericalHarmonicLightPolicyFNoStaticShadowingPolicy");
            Add("SphericalHarmonicLightPixelShader", ShaderStage.Pixel, "TLightPixelShaderFSphericalHarmonicLightPolicyFNoStaticShadowingPolicy");
            foreach (string shadow in new[] { "FShadowTexturePolicy", "FShadowVertexBufferPolicy" })
            {
                Add("SphericalHarmonicLightVertexShader", ShaderStage.Vertex, "TLightVertexShaderFSphericalHarmonicLightPolicy" + shadow);
                Add("SphericalHarmonicLightPixelShader", ShaderStage.Pixel, "TLightPixelShaderFSphericalHarmonicLightPolicy" + shadow);
            }
            Add("SphericalHarmonicLightPixelShader", ShaderStage.Pixel,
                "TLightPixelShaderFSphericalHarmonicLightPolicyFSignedDistanceFieldShadowTexturePolicy");
            Add("SphericalHarmonicLightVertexShader", ShaderStage.Vertex,
                "TLightVertexShaderFSphericalHarmonicLightPolicyFSignedDistanceFieldShadowTexturePolicy");
            Add("DirectionalLightVertexShader", ShaderStage.Vertex, "TLightVertexShaderFDirectionalLightPolicyFShadowTexturePolicy");
            Add("DirectionalLightPixelShader", ShaderStage.Pixel, "TLightPixelShaderFDirectionalLightPolicyFShadowTexturePolicy");
            Add("PointLightPixelShader", ShaderStage.Pixel, "TLightPixelShaderFPointLightPolicyFShadowTexturePolicy");
            Add("PointLightVertexShader", ShaderStage.Vertex, "TLightVertexShaderFPointLightPolicyFShadowTexturePolicy");
            Add("SpotLightVertexShader", ShaderStage.Vertex, "TLightVertexShaderFSpotLightPolicyFShadowTexturePolicy");
            Add("SpotLightPixelShader", ShaderStage.Pixel, "TLightPixelShaderFSpotLightPolicyFShadowTexturePolicy");
            // Native registration initializers confirm these policy identities and VS source/entry.
            // PS sources retain the ShaderClient reconstruction boundary (see handoff).
            foreach (string policy in new[] { "FShadowVertexBufferPolicy", "FSignedDistanceFieldShadowTexturePolicy" })
            {
                Add("DirectionalLightVertexShader", ShaderStage.Vertex, "TLightVertexShaderFDirectionalLightPolicy" + policy);
                Add("DirectionalLightPixelShader", ShaderStage.Pixel, "TLightPixelShaderFDirectionalLightPolicy" + policy);
                Add("PointLightVertexShader", ShaderStage.Vertex, "TLightVertexShaderFPointLightPolicy" + policy);
                Add("PointLightPixelShader", ShaderStage.Pixel, "TLightPixelShaderFPointLightPolicy" + policy);
                Add("SpotLightVertexShader", ShaderStage.Vertex, "TLightVertexShaderFSpotLightPolicy" + policy);
                Add("SpotLightPixelShader", ShaderStage.Pixel, "TLightPixelShaderFSpotLightPolicy" + policy);
            }
            Add("DepthOnlyPixelShader", ShaderStage.Pixel, "TDepthOnlySolidPixelShader", "TDepthOnlyScreenDoorPixelShader");
            Add("DepthOnlyVertexShader", ShaderStage.Vertex, "TDepthOnlyVertexShader<0>");
            Add("PositionOnlyDepthVertexShader", ShaderStage.Vertex, "TDepthOnlyVertexShader<1>");
            Add("HitProxyVertexShader", ShaderStage.Vertex, "FHitProxyVertexShader");
            Add("HitProxyPixelShader", ShaderStage.Pixel, "FHitProxyPixelShader");
            Add("HitMaskVertexShader", ShaderStage.Vertex, "FHitMaskVertexShader");
            Add("HitMaskPixelShader", ShaderStage.Pixel, "FHitMaskPixelShader");
            Add("FogVolumeApplyVertexShader", ShaderStage.Vertex, "FFogVolumeApplyVertexShader");
            Add("FogVolumeApplyPixelShader", ShaderStage.Pixel, "FFogVolumeApplyPixelShader");
            foreach (var (policy, entry) in new[] { ("FConeDensityPolicy", "ConeDensityMain"),
                ("FConstantDensityPolicy", "ConstantDensityMain"), ("FLinearHalfspaceDensityPolicy", "LinearHalfspaceDensityMain"),
                ("FSphereDensityPolicy", "SphericalDensityMain") })
            {
                string vertex = $"TFogIntegralVertexShader<{policy}>";
                string pixel = $"TFogIntegralPixelShader<{policy}>";
                registrations.Add(vertex, new(vertex, "FogIntegralVertexShader", "Main", ShaderStage.Vertex, true));
                registrations.Add(pixel, new(pixel, "FogIntegralPixelShader", entry, ShaderStage.Pixel, true));
            }
            registrations.Add("TAOMeshVertexShader<0>", new("TAOMeshVertexShader<0>", "AmbientOcclusionMeshShaders", "MeshAOMaskVertexMain", ShaderStage.Vertex, true));
            registrations.Add("TAOMeshVertexShader<1>", new("TAOMeshVertexShader<1>", "AmbientOcclusionMeshShaders", "MeshHistoryUpdateVertexMain", ShaderStage.Vertex, true));
            Add("ModShadowMeshAttenuationVS", ShaderStage.Vertex, "FModShadowMeshVertexShader");
            Add("ModShadowMeshAttenuationPS", ShaderStage.Pixel, "FModShadowMeshPixelShader");
            foreach (string policy in new[] { "FNoLightMapPolicy", "FDirectionalLightMapTexturePolicy", "FSimpleLightMapTexturePolicy", "FDummyLightMapTexturePolicy" })
            foreach (ShaderStage stage in new[] { ShaderStage.Vertex, ShaderStage.Pixel })
            {
                string name = $"TLightMapDensity{stage}Shader<{policy}>";
                registrations.Add(name, new(name, "LightMapDensityShader", stage == ShaderStage.Vertex ? "MainVertexShader" : "MainPixelShader", stage, IsMeshMaterial: true));
            }
            Add("TranslucencyPostRenderDepthPixelShader", ShaderStage.Pixel, "FTranslucencyPostRenderDepthPixelShader");
            Add("DistortAccumulateVertexShader", ShaderStage.Vertex, "TDistortionMeshVertexShader<FDistortMeshAccumulatePolicy>");
            Add("DistortAccumulatePixelShader", ShaderStage.Pixel, "TDistortionMeshPixelShader<FDistortMeshAccumulatePolicy>");
            Add("ShadowDepthVertexShader", ShaderStage.Vertex,
                "TShadowDepthVertexShader<ShadowDepth_OutputDepth>",
                "TShadowDepthVertexShader<ShadowDepth_PerspectiveCorrect>",
                "TShadowDepthVertexShader<ShadowDepth_OutputDepthToColor>");
            Add("ShadowDepthPixelShader", ShaderStage.Pixel,
                "TShadowDepthPixelShaderTRUETRUE", "TShadowDepthPixelShaderFALSETRUE",
                "TShadowDepthPixelShaderTRUEFALSE", "TShadowDepthPixelShaderFALSEFALSE");
            foreach (string family in new[] { "Velocity", "SFXWorldNormal", "TextureDensity" })
            foreach (ShaderStage stage in new[] { ShaderStage.Vertex, ShaderStage.Pixel })
            {
                string name = "F" + family + stage + "Shader";
                registrations.Add(name, new(name, family + "Shader", "Main" + stage + "Shader", stage, IsMeshMaterial: true));
            }
            return new ReadOnlyDictionary<string, ShaderTypeRegistration>(registrations);
        }

        public enum ShaderEnvironmentEvidence
        {
            ShaderClientReconstruction,
            LE3RegistrationCallback,
            ShippingBytecode
        }

        public sealed record ShaderEnvironmentMacro(string Name, string Definition, ShaderEnvironmentEvidence Evidence);

        /// <summary>
        /// Known registration/profile inputs for one permutation, excluding material-specific macros.
        /// Evidence is retained because a ShaderClient reconstruction is not an original editor rule.
        /// This does not certify complete native environment or registration coverage.
        /// </summary>
        public sealed class KnownPermutationEnvironment
        {
            public ShaderCompileTarget Target { get; internal init; }
            public ShaderEnvironmentEvidence CompileFlagsEvidence { get; internal init; }
            public IReadOnlyList<ShaderEnvironmentMacro> MacroDefinitions { get; internal init; }
            internal ShouldCacheVertexFactoryContext VertexFactoryContext { get; init; }

            public ShaderCompileCandidate CreateCandidate() => new(Target,
                new ReadOnlyDictionary<string, string>(MacroDefinitions.ToDictionary(m => m.Name, m => m.Definition,
                    StringComparer.Ordinal)), VertexFactoryContext);
        }

        /// <summary>
        /// Reconstructs SM5 inputs for the recovered shader/VF subset using current ShaderClient,
        /// live callback captures, and shipping RDEF flag evidence. Unknown identities fail rather
        /// than inheriting a filename, flags, or macro set from a similarly named type.
        /// </summary>
        public static bool TryGetKnownPermutationEnvironment(string shaderTypeName, string vertexFactoryTypeName,
            out KnownPermutationEnvironment environment)
        {
            environment = null;
            if (!TryGetKnownShaderTypeRegistration(shaderTypeName, out var shader)) return false;
            VertexFactoryRegistration factory = null;
            if (shader.IsMeshMaterial)
            {
                if (!TryGetKnownVertexFactoryRegistration(vertexFactoryTypeName, out factory)) return false;
            }
            else if (vertexFactoryTypeName != null) return false;

            var definitions = new List<ShaderEnvironmentMacro>();
            void Add(string name, string value = "1", ShaderEnvironmentEvidence evidence = ShaderEnvironmentEvidence.ShaderClientReconstruction)
                => definitions.Add(new(name, value, evidence));

            // Client.cpp BuildShaderMacros and MilanShaderCompiler.cpp AddCustomMacros. These
            // profile defaults describe ShaderClient's LE3 path, not arbitrary editor settings.
            Add("SM5_PROFILE");
            Add("SUPPORTS_DEPTH_TEXTURES");
            Add(shader.Stage == ShaderStage.Vertex ? "VERTEXSHADER" : "PIXELSHADER");
            Add("NUM_SAMPLES");
            bool simpleLightmap = shaderTypeName is
                "TLightMapDensityVertexShader<FSimpleLightMapTexturePolicy>" or
                "TLightMapDensityPixelShader<FSimpleLightMapTexturePolicy>" or
                "TBasePassPixelShaderFSimpleLightMapTexturePolicyNoSkyLight" or
                "TBasePassPixelShaderFSimpleLightMapTexturePolicySkyLight" or
                "TBasePassVertexShaderFSimpleLightMapTexturePolicyFNoDensityPolicy" or
                "TBasePassPixelShaderFSimpleVertexLightMapPolicyNoSkyLight" or
                "TBasePassPixelShaderFSimpleVertexLightMapPolicySkyLight" or
                "TBasePassVertexShaderFSimpleVertexLightMapPolicyFNoDensityPolicy" or
                "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFNoDensityPolicy" or
                "TBasePassPixelShaderFCustomSimpleVertexLightMapPolicySkyLight" or
                "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFNoDensityPolicy" or
                "TBasePassPixelShaderFCustomSimpleLightMapTexturePolicyNoSkyLight" or
                "TBasePassPixelShaderFCustomSimpleLightMapTexturePolicySkyLight";
            // CustomSimple NoSky retains captured ShaderClient coefficient2; VS/Sky use1.
            // Do not normalize the asymmetry into an unverified original editor rule.
            KnownFoggedBasePassVertexTypes.TryGetValue(shaderTypeName, out var foggedEnvironment);
            bool shippingFoggedSimpleTexture = foggedEnvironment is
                { LightmapPolicy: "FCustomSimpleLightMapTexturePolicy", Density: "Constant" or "LinearHalfspace" or "Sphere" };
            bool shippingFoggedVectorVertex = foggedEnvironment is
                { LightmapPolicy: "FCustomVectorVertexLightMapPolicy", Density: "Constant" or "LinearHalfspace" or "Sphere" };
            if (foggedEnvironment != null)
                simpleLightmap = foggedEnvironment.LightmapPolicy is "FSimpleLightMapTexturePolicy" or "FSimpleVertexLightMapPolicy" ||
                    foggedEnvironment is { LightmapPolicy: "FCustomSimpleVertexLightMapPolicy", Density: "Constant" or "LinearHalfspace" or "Sphere" } ||
                    shippingFoggedSimpleTexture;
            Add("NUM_LIGHTMAP_COEFFICIENTS", simpleLightmap ? "1" : "2", shippingFoggedSimpleTexture
                ? ShaderEnvironmentEvidence.ShippingBytecode : ShaderEnvironmentEvidence.ShaderClientReconstruction);

            // Explicit identities only: neither class-name substring matching nor shader-source
            // defaults constitute evidence of an engine registration's environment.
            switch (shaderTypeName)
            {
                case var foggedName when foggedEnvironment != null:
                    Add("FOGVOLUMEDENSITY_" + (foggedEnvironment.Density switch
                    {
                        "Cone" => "CONEDENSITY", "Constant" => "CONSTANT", "LinearHalfspace" => "LINEARHALFSPACE", "Sphere" => "SPHEREDENSITY",
                        _ => throw new InvalidOperationException("Unverified fog density.")
                    }), evidence: ShaderEnvironmentEvidence.LE3RegistrationCallback);
                    // The two custom fogged profiles below match shipping Local/LocalDecal
                    // bytecode even when custom-lightmap usage flags are absent. Cone remains
                    // unchanged; this is not proof of the original editor callback implementation.
                    if (shippingFoggedSimpleTexture) Add("TEXTURE_LIGHTMAP", evidence: ShaderEnvironmentEvidence.ShippingBytecode);
                    if (shippingFoggedVectorVertex)
                    {
                        Add("VERTEX_LIGHTMAP", evidence: ShaderEnvironmentEvidence.ShippingBytecode);
                        Add("USING_VECTORLIGHTMAPS", evidence: ShaderEnvironmentEvidence.ShippingBytecode);
                    }
                    // Other extras reproduce exact ShaderClient policy injections, not NoDensity templates.
                    if (foggedEnvironment.LightmapPolicy is "FDirectionalLightLightMapPolicy" or "FSHLightLightMapPolicy")
                        Add("ENABLE_DIRECTIONAL_LIGHT");
                    if (foggedEnvironment.LightmapPolicy == "FDirectionalVertexLightMapPolicy") Add("VERTEX_LIGHTMAP");
                    if (foggedEnvironment.LightmapPolicy == "FSimpleVertexLightMapPolicy" ||
                        foggedEnvironment is { LightmapPolicy: "FCustomSimpleVertexLightMapPolicy", Density: "Constant" or "LinearHalfspace" or "Sphere" })
                        Add("SIMPLE_VERTEX_LIGHTMAP");
                    if (foggedEnvironment.LightmapPolicy == "FSimpleLightMapTexturePolicy" ||
                        foggedEnvironment.Density is "Constant" or "LinearHalfspace" or "Sphere" && foggedEnvironment.LightmapPolicy is
                            "FCustomVectorLightMapTexturePolicy" or "FDirectionalLightMapTexturePolicy")
                        Add("TEXTURE_LIGHTMAP");
                    break;
                case "TLightMapDensityVertexShader<FDirectionalLightMapTexturePolicy>":
                case "TLightMapDensityPixelShader<FDirectionalLightMapTexturePolicy>":
                case "TLightMapDensityVertexShader<FSimpleLightMapTexturePolicy>":
                case "TLightMapDensityPixelShader<FSimpleLightMapTexturePolicy>":
                case "TLightMapDensityVertexShader<FDummyLightMapTexturePolicy>":
                case "TLightMapDensityPixelShader<FDummyLightMapTexturePolicy>":
                    Add("TEXTURE_LIGHTMAP");
                    Add("SIMPLE_TEXTURE_LIGHTMAP");
                    Add("SIMPLE_VERTEX_LIGHTMAP");
                    break;
                case "TLightMapDensityVertexShader<FNoLightMapPolicy>":
                case "TLightMapDensityPixelShader<FNoLightMapPolicy>":
                    // NoLightMap captures omit the static ShaderClient map's simple-vertex define.
                    // The native registration environment is a no-op; retain captured behavior.
                    break;
                case "TBasePassVertexShaderFSHLightLightMapPolicyFNoDensityPolicy":
                    Add("ENABLE_SH_LIGHT");
                    goto case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFNoDensityPolicy";
                case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFNoDensityPolicy":
                    Add("FOGVOLUMEDENSITY_NONE", evidence: ShaderEnvironmentEvidence.LE3RegistrationCallback);
                    Add("ENABLE_DIRECTIONAL_LIGHT");
                    Add("TRANSLUCENCY_ONEPASS_DYNAMICALLY_SHADOWED");
                    break;
                case "TBasePassPixelShaderFSHLightLightMapPolicySkyLight":
                    Add("ENABLE_SKY_LIGHT");
                    goto case "TBasePassPixelShaderFSHLightLightMapPolicyNoSkyLight";
                case "TBasePassPixelShaderFSHLightLightMapPolicyNoSkyLight":
                    Add("ENABLE_SH_LIGHT");
                    goto case "TBasePassPixelShaderFDirectionalLightLightMapPolicyNoSkyLight";
                case "TBasePassPixelShaderFDirectionalLightLightMapPolicySkyLight":
                    Add("ENABLE_SKY_LIGHT");
                    goto case "TBasePassPixelShaderFDirectionalLightLightMapPolicyNoSkyLight";
                case "TBasePassPixelShaderFDirectionalLightLightMapPolicyNoSkyLight":
                    Add("ENABLE_DIRECTIONAL_LIGHT");
                    Add("TRANSLUCENCY_ONEPASS_DYNAMICALLY_SHADOWED");
                    break;
                case "TBasePassPixelShaderFCustomSimpleLightMapTexturePolicySkyLight":
                    Add("ENABLE_SKY_LIGHT");
                    goto case "TBasePassPixelShaderFCustomSimpleLightMapTexturePolicyNoSkyLight";
                case "TBasePassPixelShaderFCustomSimpleLightMapTexturePolicyNoSkyLight":
                    Add("USING_SIMPLELIGHTMAPS");
                    Add("TEXTURE_LIGHTMAP");
                    break;
                case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFNoDensityPolicy":
                    Add("FOGVOLUMEDENSITY_NONE");
                    Add("TEXTURE_LIGHTMAP");
                    // SIMPLE_TEXTURE_LIGHTMAP is material-conditioned in BuildProvisionalCompileJob.
                    break;
                case "TBasePassPixelShaderFCustomVectorLightMapTexturePolicySkyLight":
                    Add("ENABLE_SKY_LIGHT");
                    goto case "TBasePassPixelShaderFCustomVectorLightMapTexturePolicyNoSkyLight";
                case "TBasePassPixelShaderFCustomVectorLightMapTexturePolicyNoSkyLight":
                    Add("USING_VECTORLIGHTMAPS");
                    Add("TEXTURE_LIGHTMAP");
                    break;
                case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFNoDensityPolicy":
                    Add("USING_VECTORLIGHTMAPS");
                    Add("TEXTURE_LIGHTMAP");
                    Add("FOGVOLUMEDENSITY_NONE");
                    break;
                case "TBasePassPixelShaderFCustomSimpleVertexLightMapPolicySkyLight":
                    Add("ENABLE_SKY_LIGHT");
                    goto case "TBasePassPixelShaderFCustomSimpleVertexLightMapPolicyNoSkyLight";
                case "TBasePassPixelShaderFCustomSimpleVertexLightMapPolicyNoSkyLight":
                    Add("SIMPLE_VERTEX_LIGHTMAP");
                    break;
                case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFNoDensityPolicy":
                    Add("FOGVOLUMEDENSITY_NONE");
                    Add("SIMPLE_VERTEX_LIGHTMAP");
                    break;
                case "TBasePassPixelShaderFCustomVectorVertexLightMapPolicySkyLight":
                    Add("ENABLE_SKY_LIGHT");
                    goto case "TBasePassPixelShaderFCustomVectorVertexLightMapPolicyNoSkyLight";
                case "TBasePassPixelShaderFCustomVectorVertexLightMapPolicyNoSkyLight":
                    Add("USING_VECTORLIGHTMAPS");
                    Add("VERTEX_LIGHTMAP");
                    break;
                case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFNoDensityPolicy":
                    Add("USING_VECTORLIGHTMAPS");
                    goto case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFNoDensityPolicy";
                case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFNoDensityPolicy":
                    Add("FOGVOLUMEDENSITY_NONE");
                    Add("SIMPLE_VERTEX_LIGHTMAP");
                    Add("VERTEX_LIGHTMAP");
                    break;
                case "TBasePassPixelShaderFDirectionalVertexLightMapPolicySkyLight":
                    Add("ENABLE_SKY_LIGHT");
                    goto case "TBasePassPixelShaderFDirectionalVertexLightMapPolicyNoSkyLight";
                case "TBasePassPixelShaderFDirectionalVertexLightMapPolicyNoSkyLight":
                    Add("VERTEX_LIGHTMAP");
                    break;
                case "TLightVertexShaderFDirectionalLightPolicyFShadowVertexBufferPolicy":
                case "TLightPixelShaderFDirectionalLightPolicyFShadowVertexBufferPolicy":
                    Add("STATICLIGHTING_VERTEXMASK");
                    goto case "TLightVertexShaderFDirectionalLightPolicyFNoStaticShadowingPolicy";
                case "TLightVertexShaderFPointLightPolicyFShadowVertexBufferPolicy":
                case "TLightPixelShaderFPointLightPolicyFShadowVertexBufferPolicy":
                case "TLightVertexShaderFSpotLightPolicyFShadowVertexBufferPolicy":
                case "TLightPixelShaderFSpotLightPolicyFShadowVertexBufferPolicy":
                    Add("STATICLIGHTING_VERTEXMASK");
                    goto case "TLightPixelShaderFPointLightPolicyFNoStaticShadowingPolicy";
                case "TLightVertexShaderFDirectionalLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                case "TLightPixelShaderFDirectionalLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                    Add("STATICLIGHTING_SIGNEDDISTANCEFIELD");
                    goto case "TLightVertexShaderFDirectionalLightPolicyFShadowTexturePolicy";
                case "TLightVertexShaderFPointLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                case "TLightPixelShaderFPointLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                case "TLightVertexShaderFSpotLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                case "TLightPixelShaderFSpotLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                    Add("STATICLIGHTING_SIGNEDDISTANCEFIELD");
                    goto case "TLightPixelShaderFPointLightPolicyFShadowTexturePolicy";
                case "TLightVertexShaderFDirectionalLightPolicyFShadowTexturePolicy":
                case "TLightPixelShaderFDirectionalLightPolicyFShadowTexturePolicy":
                    Add("STATICLIGHTING_TEXTUREMASK");
                    goto case "TLightVertexShaderFDirectionalLightPolicyFNoStaticShadowingPolicy";
                case "TLightPixelShaderFPointLightPolicyFShadowTexturePolicy":
                case "TLightVertexShaderFPointLightPolicyFShadowTexturePolicy":
                case "TLightVertexShaderFSpotLightPolicyFShadowTexturePolicy":
                case "TLightPixelShaderFSpotLightPolicyFShadowTexturePolicy":
                    Add("STATICLIGHTING_TEXTUREMASK");
                    goto case "TLightPixelShaderFPointLightPolicyFNoStaticShadowingPolicy";
                case "TBasePassPixelShaderFDirectionalLightMapTexturePolicySkyLight":
                    Add("ENABLE_SKY_LIGHT");
                    Add("TEXTURE_LIGHTMAP");
                    break;
                case "TBasePassPixelShaderFSimpleVertexLightMapPolicySkyLight":
                    Add("ENABLE_SKY_LIGHT");
                    Add("SIMPLE_VERTEX_LIGHTMAP");
                    break;
                case "TBasePassPixelShaderFSimpleVertexLightMapPolicyNoSkyLight":
                    Add("SIMPLE_VERTEX_LIGHTMAP");
                    break;
                case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFNoDensityPolicy":
                    Add("FOGVOLUMEDENSITY_NONE");
                    Add("SIMPLE_VERTEX_LIGHTMAP");
                    break;
                case "TBasePassPixelShaderFDirectionalLightMapTexturePolicyNoSkyLight":
                    Add("TEXTURE_LIGHTMAP");
                    break;
                case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFNoDensityPolicy":
                    Add("TEXTURE_LIGHTMAP");
                    Add("FOGVOLUMEDENSITY_NONE");
                    break;
                case "TBasePassPixelShaderFSimpleLightMapTexturePolicySkyLight":
                    Add("ENABLE_SKY_LIGHT");
                    Add("SIMPLE_TEXTURE_LIGHTMAP");
                    break;
                case "TBasePassPixelShaderFSimpleLightMapTexturePolicyNoSkyLight":
                    Add("SIMPLE_TEXTURE_LIGHTMAP");
                    break;
                case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFNoDensityPolicy":
                    Add("SIMPLE_TEXTURE_LIGHTMAP");
                    Add("FOGVOLUMEDENSITY_NONE");
                    // Present in the planter callback capture, although absent from ShaderClient's
                    // static map. Retain reconstruction evidence: the capture says source=milan.
                    Add("TEXTURE_LIGHTMAP");
                    break;
                case "TBasePassPixelShaderFNoLightMapPolicySkyLight":
                    Add("ENABLE_SKY_LIGHT");
                    break;
                case "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy":
                    Add("FOGVOLUMEDENSITY_NONE");
                    break;
                case "TBasePassVertexShaderFNoLightMapPolicyFConstantDensityPolicy":
                    Add("FOGVOLUMEDENSITY_CONSTANT", evidence: ShaderEnvironmentEvidence.LE3RegistrationCallback);
                    break;
                case "TBasePassVertexShaderFNoLightMapPolicyFLinearHalfspaceDensityPolicy":
                    Add("FOGVOLUMEDENSITY_LINEARHALFSPACE", evidence: ShaderEnvironmentEvidence.LE3RegistrationCallback);
                    break;
                case "TBasePassVertexShaderFNoLightMapPolicyFSphereDensityPolicy":
                    Add("FOGVOLUMEDENSITY_SPHEREDENSITY", evidence: ShaderEnvironmentEvidence.LE3RegistrationCallback);
                    break;
                case "TLightVertexShaderFDirectionalLightPolicyFNoStaticShadowingPolicy":
                case "TLightPixelShaderFDirectionalLightPolicyFNoStaticShadowingPolicy":
                    Add("SIMPLE_VERTEX_LIGHTMAP");
                    Add("ENABLE_DISTANCE_SHADOW_FADING");
                    break;
                case "TLightVertexShaderFSpotLightPolicyFNoStaticShadowingPolicy":
                case "TLightVertexShaderFPointLightPolicyFNoStaticShadowingPolicy":
                case "TLightPixelShaderFSpotLightPolicyFNoStaticShadowingPolicy":
                case "TLightPixelShaderFPointLightPolicyFNoStaticShadowingPolicy":
                case "TDepthOnlyVertexShader<0>":
                case "TDepthOnlyVertexShader<1>":
                case "TShadowDepthVertexShader<ShadowDepth_OutputDepth>":
                case "TShadowDepthVertexShader<ShadowDepth_OutputDepthToColor>":
                case "TShadowDepthPixelShaderFALSEFALSE":
                    Add("SIMPLE_VERTEX_LIGHTMAP");
                    break;
                case "TShadowDepthVertexShader<ShadowDepth_PerspectiveCorrect>":
                case "TShadowDepthPixelShaderTRUEFALSE":
                    Add("SIMPLE_VERTEX_LIGHTMAP");
                    Add("PERSPECTIVE_CORRECT_DEPTH");
                    break;
                case "TShadowDepthPixelShaderTRUETRUE":
                    Add("SIMPLE_VERTEX_LIGHTMAP");
                    Add("PERSPECTIVE_CORRECT_DEPTH");
                    Add("MATERIAL_USE_SCREEN_DOOR_FADE");
                    break;
                case "TShadowDepthPixelShaderFALSETRUE":
                    Add("SIMPLE_VERTEX_LIGHTMAP");
                    Add("MATERIAL_USE_SCREEN_DOOR_FADE");
                    break;
                case "FSFXWorldNormalVertexShader":
                case "FSFXWorldNormalPixelShader":
                case "FHitProxyVertexShader":
                case "FHitProxyPixelShader":
                case "FModShadowMeshVertexShader":
                case "FHitMaskVertexShader":
                case "FHitMaskPixelShader":
                case "TAOMeshVertexShader<0>":
                case "TAOMeshVertexShader<1>":
                case "FModShadowMeshPixelShader":
                case "TLightVertexShaderFSFXPointLightPolicyFNoStaticShadowingPolicy":
                case "TLightPixelShaderFSFXPointLightPolicyFNoStaticShadowingPolicy":
                case "TLightVertexShaderFSphericalHarmonicLightPolicyFNoStaticShadowingPolicy":
                case "TLightPixelShaderFSphericalHarmonicLightPolicyFNoStaticShadowingPolicy":
                    Add("SIMPLE_VERTEX_LIGHTMAP");
                    break;
                case "TDepthOnlyScreenDoorPixelShader":
                    Add("SIMPLE_VERTEX_LIGHTMAP");
                    Add("MATERIAL_USE_SCREEN_DOOR_FADE");
                    break;
                case "TLightVertexShaderFSphericalHarmonicLightPolicyFShadowTexturePolicy":
                    Add("STATICLIGHTING_TEXTUREMASK", evidence: ShaderEnvironmentEvidence.LE3RegistrationCallback);
                    break;
                case "TLightVertexShaderFSphericalHarmonicLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                    // ac31cf0 -> aa9c470 composes aa9c590's texture mask with the SDF define.
                    Add("STATICLIGHTING_TEXTUREMASK", evidence: ShaderEnvironmentEvidence.LE3RegistrationCallback);
                    Add("STATICLIGHTING_SIGNEDDISTANCEFIELD", evidence: ShaderEnvironmentEvidence.LE3RegistrationCallback);
                    break;
                case "TLightVertexShaderFSphericalHarmonicLightPolicyFShadowVertexBufferPolicy":
                    Add("STATICLIGHTING_VERTEXMASK", evidence: ShaderEnvironmentEvidence.LE3RegistrationCallback);
                    break;
                case "FVelocityVertexShader":
                case "FLightFunctionVertexShader":
                case "FLightFunctionPixelShader":
                case "FRadialBlurVertexShader":
                case "FRadialBlurPixelShader":
                case "FRadialBlurVelocityPixelShader":
                case "TLightPixelShaderFSphericalHarmonicLightPolicyFShadowTexturePolicy":
                case "TLightPixelShaderFSphericalHarmonicLightPolicyFShadowVertexBufferPolicy":
                case "TLightPixelShaderFSphericalHarmonicLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                case "FFogVolumeApplyVertexShader":
                case "FFogVolumeApplyPixelShader":
                case "TFogIntegralVertexShader<FConeDensityPolicy>":
                case "TFogIntegralPixelShader<FConeDensityPolicy>":
                case "TFogIntegralVertexShader<FConstantDensityPolicy>":
                case "TFogIntegralPixelShader<FConstantDensityPolicy>":
                case "TFogIntegralVertexShader<FLinearHalfspaceDensityPolicy>":
                case "TFogIntegralPixelShader<FLinearHalfspaceDensityPolicy>":
                case "TFogIntegralVertexShader<FSphereDensityPolicy>":
                case "TFogIntegralPixelShader<FSphereDensityPolicy>":
                case "FVelocityPixelShader":
                case "FTextureDensityVertexShader":
                case "FTextureDensityPixelShader":
                case "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight":
                case "TDepthOnlySolidPixelShader":
                case "FTranslucencyPostRenderDepthPixelShader":
                case "TDistortionMeshVertexShader<FDistortMeshAccumulatePolicy>":
                case "TDistortionMeshPixelShader<FDistortMeshAccumulatePolicy>":
                    break;
                default:
                    return false;
            }

            // ShaderClient's live GPUSkin callback and LE3 captures agree on matrix skinning.
            if (factory?.Name is "FGPUSkinVertexFactory" or "FGPUSkinMorphVertexFactory" or "FGPUSkinDecalVertexFactory" or "FGPUSkinMorphDecalVertexFactory")
                Add("QUAT_SKINNING", "0", ShaderEnvironmentEvidence.LE3RegistrationCallback);
            if (factory?.Name is "FGPUSkinMorphVertexFactory" or "FGPUSkinMorphDecalVertexFactory")
                Add("GPUSKIN_MORPH_BLEND", "1", ShaderEnvironmentEvidence.LE3RegistrationCallback);
            if (factory?.Name == "FInstancedStaticMeshVertexFactory")
                Add("USE_INSTANCING", "1", ShaderEnvironmentEvidence.LE3RegistrationCallback);
            if (factory?.Name == "FSplineMeshVertexFactory")
                Add("USE_SPLINEDEFORM", "1", ShaderEnvironmentEvidence.LE3RegistrationCallback);
            if (factory?.Name == "FParticleInstancedMeshVertexFactory")
            {
                Add("COLOR_OVER_LIFE", "1", ShaderEnvironmentEvidence.LE3RegistrationCallback);
                // ShaderClient supplies this factory default in addition to shader policy defines.
                if (!definitions.Any(m => m.Name == "SIMPLE_VERTEX_LIGHTMAP")) Add("SIMPLE_VERTEX_LIGHTMAP");
            }
            if (factory?.Name is "FParticleVertexFactory" or "FParticleDynamicParameterVertexFactory" or
                "FParticleSubUVVertexFactory" or "FParticleSubUVDynamicParameterVertexFactory" or
                "FParticleBeamTrailVertexFactory" or "FParticleBeamTrailDynamicParameterVertexFactory")
            {
                Add("PARTICLES_ALLOW_AXIS_ROTATION", "1", ShaderEnvironmentEvidence.LE3RegistrationCallback);
                Add("NUM_AXIS_ROTATION_VECTORS", "2", ShaderEnvironmentEvidence.LE3RegistrationCallback);
                Add("USE_OCCLUSION_PERCENTAGE", "1", ShaderEnvironmentEvidence.LE3RegistrationCallback);
                if (factory.Name is "FParticleSubUVVertexFactory" or "FParticleSubUVDynamicParameterVertexFactory")
                    Add("SUBUV_PARTICLES", "1", ShaderEnvironmentEvidence.LE3RegistrationCallback);
                if (factory.Name is "FParticleDynamicParameterVertexFactory" or "FParticleSubUVDynamicParameterVertexFactory" or
                    "FParticleBeamTrailDynamicParameterVertexFactory")
                {
                    Add("USE_DYNAMIC_PARAMETERS", "1", ShaderEnvironmentEvidence.LE3RegistrationCallback);
                    if (!definitions.Any(m => m.Name == "SIMPLE_VERTEX_LIGHTMAP")) Add("SIMPLE_VERTEX_LIGHTMAP");
                }
            }
            if (factory?.Name is "FTerrainVertexFactory" or "FTerrainMorphVertexFactory" or "FTerrainFullMorphVertexFactory" or
                "FTerrainDecalVertexFactory" or "FTerrainMorphDecalVertexFactory" or "FTerrainFullMorphDecalVertexFactory")
            {
                Add("WORLD_COORDS", "1", ShaderEnvironmentEvidence.ShaderClientReconstruction);
                if (factory.Name.Contains("Morph", StringComparison.Ordinal))
                    Add("TERRAIN_MORPHING_ENABLED", "1", ShaderEnvironmentEvidence.LE3RegistrationCallback);
                if (factory.Name.Contains("FullMorph", StringComparison.Ordinal))
                    Add("TERRAIN_MORPHING_GRADIENTS", "1", ShaderEnvironmentEvidence.LE3RegistrationCallback);
                if (factory.Name.Contains("Decal", StringComparison.Ordinal))
                    Add("DECAL_FACTORY", "1", ShaderEnvironmentEvidence.LE3RegistrationCallback);
            }
            if (factory?.Name is "FFluidVertexFactory" or "FFluidTessellationVertexFactory")
                Add("XBOXTESSELLATION", "0", ShaderEnvironmentEvidence.LE3RegistrationCallback);
            if (factory?.Name == "FLensFlareVertexFactory")
            {
                Add("USE_LENSFLARE", "1", ShaderEnvironmentEvidence.LE3RegistrationCallback);
                Add("USE_OCCLUSION_PERCENTAGE", "1", ShaderEnvironmentEvidence.LE3RegistrationCallback);
                if (!definitions.Any(m => m.Name == "SIMPLE_VERTEX_LIGHTMAP")) Add("SIMPLE_VERTEX_LIGHTMAP");
            }
            if (factory?.Name is "FGPUSkinDecalVertexFactory" or "FGPUSkinMorphDecalVertexFactory")
                Add("GPUSKIN_DECAL", "1", ShaderEnvironmentEvidence.LE3RegistrationCallback);
            if (factory?.Name == "FLocalDecalVertexFactory")
            {
                Add("DECAL_FACTORY", "1", ShaderEnvironmentEvidence.LE3RegistrationCallback);
                Add("WORLD_COORDS", "1", ShaderEnvironmentEvidence.ShaderClientReconstruction);
            }

            uint flags = shaderTypeName is
                "TLightPixelShaderFSFXPointLightPolicyFNoStaticShadowingPolicy" or
                "TLightPixelShaderFSphericalHarmonicLightPolicyFNoStaticShadowingPolicy" or
                "TLightPixelShaderFSphericalHarmonicLightPolicyFShadowTexturePolicy" or
                "TLightPixelShaderFSphericalHarmonicLightPolicyFShadowVertexBufferPolicy" or
                "TLightPixelShaderFSphericalHarmonicLightPolicyFSignedDistanceFieldShadowTexturePolicy" or
                "TLightPixelShaderFDirectionalLightPolicyFNoStaticShadowingPolicy" or
                "TLightPixelShaderFPointLightPolicyFNoStaticShadowingPolicy" or
                "TLightPixelShaderFSpotLightPolicyFNoStaticShadowingPolicy" or
                "TLightPixelShaderFDirectionalLightPolicyFShadowTexturePolicy" or
                "TLightPixelShaderFPointLightPolicyFShadowTexturePolicy" or
                "TLightPixelShaderFDirectionalLightPolicyFShadowVertexBufferPolicy" or
                "TLightPixelShaderFPointLightPolicyFShadowVertexBufferPolicy" or
                "TLightPixelShaderFSpotLightPolicyFShadowVertexBufferPolicy" or
                "TLightPixelShaderFDirectionalLightPolicyFSignedDistanceFieldShadowTexturePolicy" or
                "TLightPixelShaderFPointLightPolicyFSignedDistanceFieldShadowTexturePolicy" or
                "TLightPixelShaderFSpotLightPolicyFSignedDistanceFieldShadowTexturePolicy" or
                "TLightPixelShaderFSpotLightPolicyFShadowTexturePolicy" ? 0x9400u : 0x9000u;
            // Shipping SM5 samples confirm the calibrated types' flags after removing
            // RDEF's compiler-inserted NoPreshader bit (0x100). D3DCompile rejects that bit as
            // an explicit SM5 input; 0x9000/0x9400 produce stored 0x9100/0x9500 respectively.
            // This is bounded shipping evidence, not proof of every engine permutation.
            var flagEvidence = shaderTypeName is "FHitMaskVertexShader" or "FHitMaskPixelShader" or
                "TAOMeshVertexShader<0>" or "TAOMeshVertexShader<1>" or
                "FFogVolumeApplyVertexShader" or "FFogVolumeApplyPixelShader" or
                "TFogIntegralVertexShader<FConeDensityPolicy>" or "TFogIntegralPixelShader<FConeDensityPolicy>" or
                "TFogIntegralVertexShader<FConstantDensityPolicy>" or "TFogIntegralPixelShader<FConstantDensityPolicy>" or
                "TFogIntegralVertexShader<FLinearHalfspaceDensityPolicy>" or "TFogIntegralPixelShader<FLinearHalfspaceDensityPolicy>" or
                "TFogIntegralVertexShader<FSphereDensityPolicy>" or "TFogIntegralPixelShader<FSphereDensityPolicy>"
                or "TLightVertexShaderFSphericalHarmonicLightPolicyFShadowTexturePolicy"
                or "TLightPixelShaderFSphericalHarmonicLightPolicyFShadowTexturePolicy"
                or "TLightVertexShaderFSphericalHarmonicLightPolicyFShadowVertexBufferPolicy"
                or "TLightVertexShaderFSphericalHarmonicLightPolicyFSignedDistanceFieldShadowTexturePolicy"
                or "TLightPixelShaderFSphericalHarmonicLightPolicyFShadowVertexBufferPolicy"
                or "TLightPixelShaderFSphericalHarmonicLightPolicyFSignedDistanceFieldShadowTexturePolicy" ||
                KnownFoggedBasePassVertexTypes.ContainsKey(shaderTypeName) ||
                !shader.IsMeshMaterial || factory?.Name is "FGPUSkinMorphVertexFactory" or "FInstancedStaticMeshVertexFactory" or
                    "FGPUSkinDecalVertexFactory" or "FLocalDecalVertexFactory" or "FGPUSkinMorphDecalVertexFactory" or "FSplineMeshVertexFactory" or
                    "FParticleInstancedMeshVertexFactory" or "FLocalVertexFactoryApex" or "FGPUSkinVertexFactoryApex" or
                    "FParticleVertexFactory" or "FParticleDynamicParameterVertexFactory" or "FParticleSubUVVertexFactory" or
                    "FParticleSubUVDynamicParameterVertexFactory" or "FParticleBeamTrailVertexFactory" or "FParticleBeamTrailDynamicParameterVertexFactory" or
                    "FLensFlareVertexFactory" or "FFoliageVertexFactory" or "FFluidVertexFactory" or "FFluidTessellationVertexFactory" or
                    "FTerrainVertexFactory" or "FTerrainMorphVertexFactory" or "FTerrainFullMorphVertexFactory" or
                    "FTerrainDecalVertexFactory" or "FTerrainMorphDecalVertexFactory" or "FTerrainFullMorphDecalVertexFactory"
                ? ShaderEnvironmentEvidence.ShaderClientReconstruction
                : ShaderEnvironmentEvidence.ShippingBytecode;
            environment = new KnownPermutationEnvironment
            {
                Target = shader.CreateTarget(flags, factory?.Name, factory?.SourceFileName),
                CompileFlagsEvidence = flagEvidence,
                MacroDefinitions = definitions.AsReadOnly(),
                VertexFactoryContext = factory?.CreateCacheContext()
            };
            return true;
        }

        /// <summary>
        /// Complete, compiler-independent input for one D3D shader compile. Bytecode compilation and
        /// parameter-map reflection are intentionally separate from material translation.
        /// </summary>
        public sealed class ShaderCompileJob
        {
            internal TranslationResult Translation { get; init; }
            public ResolvedMaterialCompilationInput CompilationInput { get; internal init; }
            public ShaderCompileTarget Target { get; internal init; }
            public string MaterialHlsl { get; internal init; }
            public IReadOnlyDictionary<string, string> Macros { get; internal init; }
        }

        /// <summary>
        /// One registered shader/VF permutation and its caller-supplied compilation environment.
        /// Environment macros must belong to this permutation, not to a different captured material.
        /// </summary>
        public sealed record ShaderCompileCandidate(
            ShaderCompileTarget Target,
            IReadOnlyDictionary<string, string> EnvironmentMacros = null,
            ShouldCacheVertexFactoryContext VertexFactoryContext = null,
            bool IsCompilationEnvironmentResolved = true);

        /// <summary>
        /// Selection and provisional jobs for an explicit candidate list. Success means that every
        /// candidate was resolved, not that the list covers every engine registration. On any error,
        /// Jobs is empty so an unresolved permutation cannot silently produce a partial shader set.
        /// </summary>
        public sealed class ShaderCompilePlan
        {
            // Retain provenance even when every candidate is excluded and no job can carry it.
            internal TranslationResult Translation { get; init; }
            public ResolvedMaterialCompilationInput CompilationInput { get; internal init; }
            public IReadOnlyList<ShaderCompileJob> Jobs { get; internal init; }
            public IReadOnlyList<ShaderCompileTarget> ExcludedTargets { get; internal init; }
            public IReadOnlyList<string> Errors { get; internal init; }
            public IReadOnlyList<ShaderCompileDecision> Decisions { get; internal init; }
            public bool Success => Errors.Count == 0;
            /// <summary>
            /// True when the candidates were every registered material shader type crossed with every
            /// materials-capable vertex factory, evaluated in the shipping SM5 PC context (debug view
            /// modes allowed, no caller resource predicate) for a resource translation. The VF registry is closed at 29 static
            /// registrations and the material type inventory at 157 mesh + 5 non-mesh, so Jobs then
            /// equal native FShaderMap::IsComplete membership. Not flags, bytecode or runtime parity.
            /// </summary>
            public bool CoversNativeRequiredSet { get; internal init; }
        }

        /// <summary>
        /// Exact registered permutation identity. Null VF denotes a non-mesh material shader;
        /// an empty VF name is invalid. Names are ordinal and are not normalized or alias-matched.
        /// </summary>
        public sealed record ShaderPermutationIdentity(string ShaderTypeName, string VertexFactoryTypeName = null);

        /// <summary>
        /// Compares selected plan jobs with an independently supplied permutation inventory.
        /// Matching a capture or caller inventory does not certify that inventory's completeness,
        /// bytecode, bindings, material identity or runtime compatibility.
        /// </summary>
        public sealed class ShaderPermutationComparison
        {
            public bool PlanSucceeded { get; internal init; }
            public int ExpectedPermutationCount { get; internal init; }
            public int PlannedPermutationCount { get; internal init; }
            public IReadOnlyList<ShaderPermutationIdentity> MissingPermutations { get; internal init; } = [];
            public IReadOnlyList<ShaderPermutationIdentity> UnexpectedPermutations { get; internal init; } = [];
            public IReadOnlyList<string> Errors { get; internal init; } = [];
            public bool MatchesExpectedPermutations => PlanSucceeded && Errors.Count == 0 &&
                MissingPermutations.Count == 0 && UnexpectedPermutations.Count == 0;
            public bool IsCompleteShaderSet => false;
        }

        /// <summary>
        /// Ordered comparison of the six serialized uniform-expression arrays. This is an
        /// independent structural check, not texture-object identity, CPU evaluation, shader
        /// membership, bytecode or complete native shader-map compatibility validation.
        /// </summary>
        public sealed class UniformExpressionComparison
        {
            public bool TranslationSucceeded { get; internal init; }
            public int ExpectedExpressionCount { get; internal init; }
            public int TranslatedExpressionCount { get; internal init; }
            public IReadOnlyList<string> Differences { get; internal init; } = [];
            public IReadOnlyList<string> Errors { get; internal init; } = [];
            public bool MatchesExpectedUniformExpressions => TranslationSucceeded && Errors.Count == 0 && Differences.Count == 0;
            public bool IsCompleteShaderSet => false;
        }

        public enum ShaderCompileDisposition
        {
            Eligible,
            Excluded,
            Unresolved
        }

        public enum ShaderCompileGate
        {
            Registration,
            VertexFactoryUsage,
            ShaderTypeShouldCache,
            VertexFactoryShouldCache,
            CompilationEnvironment,
            MaterialResourceShouldCache
        }

        /// <summary>
        /// A decision for one candidate. Eligible means this candidate passed individually; Jobs
        /// is still withheld if another candidate failed. Gate identifies the decisive check.
        /// </summary>
        public sealed record ShaderCompileDecision(ShaderCompileTarget Target, ShaderCompileDisposition Disposition,
            ShaderCompileGate Gate, string Reason);

        public sealed class ShaderCompilationResult
        {
            public LE3ShaderSourceSnapshot SourceSnapshot { get; internal init; }
            internal TranslationResult Translation { get; init; }
            public ResolvedMaterialCompilationInput CompilationInput { get; internal init; }
            public ShaderCompileTarget Target { get; internal init; }
            public byte[] Bytecode { get; internal init; }
            public string Diagnostics { get; internal init; }
            public int InstructionCount { get; internal init; }
            public IReadOnlyList<ShaderParameterBinding> ParameterBindings { get; internal init; } = [];
            public bool Success => Bytecode is { Length: > 0 };
        }

        /// <summary>
        /// A fully bound typed shader for the explicitly supported constructor subset, not a
        /// complete shader map. Shader is withheld on an unsupported constructor or any unexplained
        /// allocation. The exact recovered renderer-owned view-buffer allocations are reported
        /// separately; they are not fabricated as material shader fields.
        /// </summary>
        public sealed class ShaderConstructionResult
        {
            public Shader Shader { get; internal init; }
            public IReadOnlyList<string> Errors { get; internal init; } = [];
            public IReadOnlyList<ShaderParameterBinding> UnboundParameters { get; internal init; } = [];
            public IReadOnlyList<ShaderParameterBinding> RendererOwnedParameters { get; internal init; } = [];
            public bool Success => Shader != null && Errors.Count == 0;
        }

        /// <summary>
        /// A serialization-ready fragment covering every eligible job in an explicit caller plan.
        /// IsCompleteShaderSet reports native required membership only for a plan that covers the native
        /// required set; it never authorizes installation. Source CRC maps and material-resource installation remain separate.
        /// Uniform-expression trees are shared with the translation; array/key containers are detached.
        /// </summary>
        public sealed class ShaderMapFragmentResult
        {
            public MaterialShaderMap Map { get; internal init; }
            public IReadOnlyDictionary<Guid, Shader> Shaders { get; internal init; } =
                new ReadOnlyDictionary<Guid, Shader>(new Dictionary<Guid, Shader>());
            public IReadOnlyList<int> UniformExpressionTextures { get; internal init; } = [];
            public IReadOnlyList<string> Errors { get; internal init; } = [];
            public bool Success => Map != null && Errors.Count == 0;
            /// <summary>Native required-membership completeness: every job of a plan that covers the
            /// native required set is present. Not bytecode, flags, CRC or rendering validation.</summary>
            public bool IsCompleteShaderSet { get; internal init; }
        }

        /// <summary>A detached, serializable cache fragment. Completeness is membership only, never installability.</summary>
        public sealed class ShaderCacheFragmentResult
        {
            public ShaderCache Cache { get; internal init; }
            public IReadOnlyList<int> UniformExpressionTextures { get; internal init; } = [];
            public IReadOnlyList<string> Errors { get; internal init; } = [];
            public bool Success => Cache != null && Errors.Count == 0;
            /// <summary>Native required-membership completeness, as for ShaderMapFragmentResult.</summary>
            public bool IsCompleteShaderSet { get; internal init; }
        }

        public enum ShaderParameterBindingKind
        {
            Constant,
            Resource
        }

        /// <summary>
        /// One D3D-reflected parameter-map allocation. Size is bytes for constants and a resource
        /// count for resources. SamplerIndex is present for a sampler or texture/sampler pair.
        /// </summary>
        public sealed record ShaderParameterBinding(
            string Name,
            ShaderParameterBindingKind Kind,
            ushort BufferIndex,
            ushort BaseIndex,
            ushort Size,
            ushort? SamplerIndex);

        /// <summary>
        /// Semantic material predicates consumed by the LE3 shader-type ShouldCache callbacks that
        /// have been recovered so far. This is deliberately separate from serialized UMaterial
        /// properties: some virtual getters add blend/lighting eligibility checks. NumUserTexCoords
        /// and UsesDynamicParameter are translation-derived; planners populate their own context,
        /// while explicit caller contexts must supply the matching translated values.
        /// DecalFactoryFallback is the caller-resolved native +0xa8 result: false for Material
        /// resources, true for DecalMaterial resources, null for unresolved derived resources.
        /// It is independent of the bUsedWithDecals usage flag and also vetoes velocity shaders.
        /// </summary>
        public sealed record ShouldCacheMaterialContext(
            bool IsUnlit,
            bool IsMasked,
            bool IsSpecialEngineMaterial,
            bool UsesLitTranslucencyDepthPass,
            bool UsesSurfaceTranslucencyDepthPass,
            bool CastLitTranslucencyShadowAsMasked,
            bool HasWorldPositionOffset,
            bool UsedWithStaticLighting,
            bool UsesLitTranslucencyDepthPostpass = false,
            EBlendMode BlendMode = EBlendMode.BLEND_Opaque,
            bool UsedWithFogVolumes = false,
            bool IsDistorted = false,
            bool UsedWithSkeletalMesh = false,
            bool UsedWithFracturedMeshes = false,
            bool UsedWithDecals = false,
            bool RequiresSimpleStaticLighting = false,
            bool IsTerrainMaterial = false,
            bool UsedWithVectorLightMaps = false,
            bool UsedWithSimpleLightMaps = false,
            bool IsTwoSided = false,
            uint NumUserTexCoords = 0,
            bool UsedWithMorphTargets = false,
            bool UsedWithInstancedMeshes = false,
            bool? DecalFactoryFallback = false,
            bool UsedWithSplineMeshes = false,
            bool UsedWithInstancedMeshParticles = false,
            bool IsLightFunction = false,
            bool UsedWithRadialBlur = false,
            bool UsedWithAPEXMeshes = false,
            bool UsedWithParticleSprites = false,
            bool UsedWithParticleSubUV = false,
            bool UsesDynamicParameter = false,
            bool UsedWithBeamTrails = false,
            bool UsedWithLensFlare = false,
            bool UsedWithFoliage = false,
            bool UsedWithFluidSurfaces = false);

        /// <summary>Caller-resolved engine-wide cache gates, not material properties. The default
        /// is ordinary PC SM5 compilation with debug viewmodes allowed, not cooking or seek-free
        /// server compilation. Supply the native outcome explicitly for those environments.</summary>
        public sealed record ShouldCacheEnvironmentContext(bool AllowsDebugViewModes = true);

        /// <summary>Vertex-factory registration predicates needed by recovered ShouldCache callbacks.
        /// Dynamic lighting and precise previous position are independently nullable: missing
        /// evidence is unresolved, not equivalent to static lighting or an automatic false exclusion.</summary>
        public sealed record ShouldCacheVertexFactoryContext(bool SupportsStaticLighting, bool UsedWithMaterials = true,
            bool? SupportsDynamicLighting = null, bool? SupportsPrecisePreviousPosition = null);

        /// <summary>
        /// Builds the LE3 Material/DecalMaterial resource view used by recovered ShouldCache callbacks.
        /// <paramref name="effectiveProperties"/> may supply already-resolved archetype/class defaults;
        /// otherwise LEC resolves the material's archetype chain. The stored bIsMasked flag is read
        /// directly, matching LE3's resource getter even if an edited graph would imply a new value.
        /// </summary>
        private PropertyCollection effectiveMaterialPropertiesCache;
        private byte[] effectiveMaterialPropertiesSource;

        // Inherited-property resolution is expensive and was repeated for every planned job. Reuse one
        // read while the material's serialized data is unchanged; treat the collection as read-only.
        private PropertyCollection EffectiveMaterialProperties()
        {
            if (effectiveMaterialPropertiesCache == null || !material.DataReadOnly.SequenceEqual(effectiveMaterialPropertiesSource))
            {
                effectiveMaterialPropertiesSource = material.DataReadOnly.ToArray();
                effectiveMaterialPropertiesCache = material.GetPropertiesIncludingInherited();
            }
            return effectiveMaterialPropertiesCache;
        }

        public ShouldCacheMaterialContext BuildShouldCacheMaterialContext(PropertyCollection effectiveProperties = null)
        {
            if (material.ClassName is not ("Material" or "DecalMaterial"))
            {
                throw new NotSupportedException($"ShouldCache context construction for {material.ClassName} has not been verified in LE3.");
            }

            var properties = effectiveProperties ?? EffectiveMaterialProperties();
            string lightingModel = properties.GetProp<EnumProperty>("LightingModel")?.Value.Name ?? "MLM_Phong";
            if (lightingModel is not ("MLM_Phong" or "MLM_NonDirectional" or "MLM_Unlit" or "MLM_SHPRT" or
                "MLM_Custom" or "MLM_Anisotropic"))
            {
                throw new NotSupportedException($"Unknown LE3 lighting model {lightingModel}.");
            }

            string blendMode = properties.GetProp<EnumProperty>("BlendMode")?.Value.Name ?? "BLEND_Opaque";
            if (blendMode is not ("BLEND_Opaque" or "BLEND_Masked" or "BLEND_Translucent" or "BLEND_Additive" or
                "BLEND_Modulate" or "BLEND_SoftMasked" or "BLEND_AlphaComposite"))
            {
                throw new NotSupportedException($"Unknown LE3 blend mode {blendMode}.");
            }

            bool isUnlit = lightingModel == "MLM_Unlit";
            bool eligibleTranslucent = blendMode is "BLEND_Translucent" or "BLEND_Additive" or
                "BLEND_Modulate" or "BLEND_AlphaComposite";
            // Both resource vtables share +0x1d8: stored Expression connectivity, even when
            // the decal CompileProperty callback ignores WPO or UseConstant overrides its graph.
            bool worldPositionOffsetConnected = properties.GetProp<StructProperty>("WorldPositionOffset")?
                .GetProp<ObjectProperty>("Expression")?.Value is not null and not 0;

            return new ShouldCacheMaterialContext(
                IsUnlit: isUnlit,
                IsMasked: properties.GetProp<BoolProperty>("bIsMasked")?.Value == true,
                IsSpecialEngineMaterial: properties.GetProp<BoolProperty>("bUsedAsSpecialEngineMaterial")?.Value == true,
                UsesLitTranslucencyDepthPass: eligibleTranslucent && !isUnlit &&
                    properties.GetProp<BoolProperty>("bUseLitTranslucencyDepthPass")?.Value == true,
                UsesSurfaceTranslucencyDepthPass:
                    properties.GetProp<BoolProperty>("bUseSurfaceTranslucencyDepthPass")?.Value == true,
                CastLitTranslucencyShadowAsMasked:
                    properties.GetProp<BoolProperty>("bCastLitTranslucencyShadowAsMasked")?.Value == true,
                HasWorldPositionOffset: worldPositionOffsetConnected,
                UsedWithStaticLighting: properties.GetProp<BoolProperty>("bUsedWithStaticLighting")?.Value ??
                    material.ClassName == "DecalMaterial",
                // LE3 +0x48 tests the post-render flag and translucent blend, without an unlit gate.
                UsesLitTranslucencyDepthPostpass: eligibleTranslucent &&
                    properties.GetProp<BoolProperty>("bUseLitTranslucencyPostRenderDepthPass")?.Value == true,
                BlendMode: Enum.Parse<EBlendMode>(blendMode),
                UsedWithFogVolumes: properties.GetProp<BoolProperty>("bUsedWithFogVolumes")?.Value == true,
                // Native +0x88 reads the stored bit; do not infer it merely from a connected graph.
                IsDistorted: properties.GetProp<BoolProperty>("bUsesDistortion")?.Value == true &&
                    properties.GetProp<BoolProperty>("bUseOneLayerDistortion")?.Value != true,
                UsedWithSkeletalMesh: properties.GetProp<BoolProperty>("bUsedWithSkeletalMesh")?.Value ??
                    material.ClassName == "DecalMaterial",
                UsedWithFracturedMeshes: properties.GetProp<BoolProperty>("bUsedWithFracturedMeshes")?.Value ??
                    material.ClassName == "DecalMaterial",
                UsedWithDecals: properties.GetProp<BoolProperty>("bUsedWithDecals")?.Value ??
                    material.ClassName == "DecalMaterial",
                // Native vtable +0x168/+0x170 read UMaterial +0x3c4 bits3/4, respectively.
                UsedWithVectorLightMaps: properties.GetProp<BoolProperty>("bUsedWithVectorLightMaps")?.Value == true,
                UsedWithSimpleLightMaps: properties.GetProp<BoolProperty>("bUsedWithSimpleLightMaps")?.Value == true,
                // FMaterialResource::IsTwoSided ab522a0 reads UMaterial+0x3c0 bit0.
                IsTwoSided: properties.GetProp<BoolProperty>("TwoSided")?.Value == true,
                // Resource virtuals +0x128/+0x138 read stored usage bits, not graph contents.
                UsedWithMorphTargets: properties.GetProp<BoolProperty>("bUsedWithMorphTargets")?.Value == true,
                UsedWithInstancedMeshes: properties.GetProp<BoolProperty>("bUsedWithInstancedMeshes")?.Value == true,
                // Native resource +0xa8: ordinary vtable b94b6b8 returns zero, decal b952a60 returns one.
                DecalFactoryFallback: material.ClassName == "DecalMaterial",
                // Native resource +0x140 reads UMaterial+0x3c4 bit0 (ab52580).
                UsedWithSplineMeshes: properties.GetProp<BoolProperty>("bUsedWithSplineMeshes")?.Value == true,
                // Native resource +0x108 reads UMaterial+0x3c0 bit25 (ab523c0).
                UsedWithInstancedMeshParticles: properties.GetProp<BoolProperty>("bUsedWithInstancedMeshParticles")?.Value == true,
                IsLightFunction: properties.GetProp<BoolProperty>("bUsedAsLightFunction")?.Value == true,
                UsedWithRadialBlur: properties.GetProp<BoolProperty>("bUsedWithRadialBlur")?.Value == true,
                // Resource +0x148 (ab522c0) reads UMaterial+0x3c4 bit1.
                UsedWithAPEXMeshes: properties.GetProp<BoolProperty>("bUsedWithAPEXMeshes")?.Value == true,
                UsedWithParticleSprites: properties.GetProp<BoolProperty>("bUsedWithParticleSprites")?.Value == true,
                UsedWithParticleSubUV: properties.GetProp<BoolProperty>("bUsedWithParticleSubUV")?.Value == true,
                UsedWithBeamTrails: properties.GetProp<BoolProperty>("bUsedWithBeamTrails")?.Value == true,
                UsedWithLensFlare: properties.GetProp<BoolProperty>("bUsedWithLensFlare")?.Value == true,
                UsedWithFoliage: properties.GetProp<BoolProperty>("bUsedWithFoliage")?.Value == true,
                UsedWithFluidSurfaces: properties.GetProp<BoolProperty>("bUsedWithFluidSurfaces")?.Value == true);
        }

        // Observed in captured LE3 compile jobs. Individual shader registrations may add flags such
        // as PreferFlowControl, so callers must preserve the target's exact CompileFlags value.
        public const uint CapturedDefaultCompileFlags = (1u << 12) | (1u << 15);
        public const uint PreferFlowControlCompileFlag = 1u << 10;

        // Native ab52280 excludes precisely blend modes0,1,5 from the translucent family.
        private static bool IsTranslucentBlend(EBlendMode blend)
            => blend is not (EBlendMode.BLEND_Opaque or EBlendMode.BLEND_Masked or EBlendMode.BLEND_SoftMasked);

        /// <summary>
        /// Evaluates the shader-type portion of ShouldCache for callbacks confirmed in LE3
        /// MassEffect3.exe 11.3.2. This does not evaluate FMaterial::ShouldCache or a vertex-factory
        /// callback, both of which are independent gates for mesh material shaders.
        /// </summary>
        /// <returns>True when the shader type is known and <paramref name="shouldCache"/> was evaluated.</returns>
        public static bool TryEvaluateKnownShaderTypeShouldCache(string shaderTypeName,
            ShouldCacheMaterialContext materialContext, out bool shouldCache)
            => TryEvaluateKnownShaderTypeShouldCache(shaderTypeName, materialContext, null, out shouldCache);

        /// <summary>
        /// SM5 shader-type gate, including callbacks that inspect the vertex-factory name.
        /// A missing factory for a factory-dependent predicate is unresolved, not an exclusion.
        /// </summary>
        public static bool TryEvaluateKnownShaderTypeShouldCache(string shaderTypeName,
            ShouldCacheMaterialContext materialContext, string vertexFactoryTypeName, out bool shouldCache)
            => TryEvaluateKnownShaderTypeShouldCache(shaderTypeName, materialContext, vertexFactoryTypeName,
                null, out shouldCache);

        /// <summary>
        /// SM5 shader-type gate with explicit factory capabilities. Texture-lightmap policies inspect
        /// SupportsStaticLighting; dynamic-lightmap policies inspect SupportsDynamicLighting.
        /// Omitting a required capability is unresolved, not an exclusion.
        /// Ordinary LE3 FMaterialResource::RequiresSimpleStaticLighting returns false; the explicit
        /// material predicate is retained for future verified derived-resource contexts.
        /// </summary>
        public static bool TryEvaluateKnownShaderTypeShouldCache(string shaderTypeName,
            ShouldCacheMaterialContext materialContext, string vertexFactoryTypeName,
            ShouldCacheVertexFactoryContext vertexFactoryContext, out bool shouldCache,
            ShouldCacheEnvironmentContext environmentContext = null)
        {
            ArgumentNullException.ThrowIfNull(shaderTypeName);
            ArgumentNullException.ThrowIfNull(materialContext);

            if (KnownFoggedBasePassVertexTypes.TryGetValue(shaderTypeName, out var fogged))
            {
                shouldCache = false;
                // aa2b880: Cone is disabled regardless of the queried blend or factory.
                if (fogged.Density == "Cone" || !IsTranslucentBlend(materialContext.BlendMode) ||
                    materialContext.BlendMode == EBlendMode.BLEND_Modulate) return true;
                if (!materialContext.UsedWithFogVolumes)
                {
                    if (string.IsNullOrWhiteSpace(vertexFactoryTypeName)) return false;
                    if (vertexFactoryTypeName.Contains("GPUSkin", StringComparison.Ordinal)) return true;
                }
                // Non-Cone bodies compose the fog/blend predicate with the same lightmap gate.
                return TryEvaluateKnownShaderTypeShouldCache("TBasePassVertexShader" + fogged.LightmapPolicy + "FNoDensityPolicy",
                    materialContext, vertexFactoryTypeName, vertexFactoryContext, out shouldCache, environmentContext);
            }

            switch (shaderTypeName)
            {
                case "FLightFunctionVertexShader":
                case "FLightFunctionPixelShader":
                    // aaffce0 tail-calls resource+78 (ab52200), UMaterial+3c0 bit11.
                    shouldCache = materialContext.IsLightFunction;
                    return true;
                case "FRadialBlurVertexShader":
                case "FRadialBlurPixelShader":
                case "FRadialBlurVelocityPixelShader":
                    // abcb6b0 reads resource+130 (ab524e0, bit30), then special+90.
                    shouldCache = materialContext.UsedWithRadialBlur || materialContext.IsSpecialEngineMaterial;
                    return true;
                case "TLightVertexShaderFSphericalHarmonicLightPolicyFShadowTexturePolicy":
                case "TLightPixelShaderFSphericalHarmonicLightPolicyFShadowTexturePolicy":
                case "TLightVertexShaderFSphericalHarmonicLightPolicyFShadowVertexBufferPolicy":
                case "TLightPixelShaderFSphericalHarmonicLightPolicyFShadowVertexBufferPolicy":
                case "TLightPixelShaderFSphericalHarmonicLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                case "TLightVertexShaderFSphericalHarmonicLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                    // ac35fe0/ac36030 perform preliminary checks, then unconditionally return zero.
                    shouldCache = false;
                    return true;
                case "TFogIntegralVertexShader<FConeDensityPolicy>":
                case "TFogIntegralPixelShader<FConeDensityPolicy>":
                    // aae7c90 calls the fog-usage getter but unconditionally returns false.
                    shouldCache = false;
                    return true;
                case "TFogIntegralVertexShader<FConstantDensityPolicy>":
                case "TFogIntegralPixelShader<FConstantDensityPolicy>":
                case "TFogIntegralVertexShader<FLinearHalfspaceDensityPolicy>":
                case "TFogIntegralPixelShader<FLinearHalfspaceDensityPolicy>":
                case "TFogIntegralVertexShader<FSphereDensityPolicy>":
                case "TFogIntegralPixelShader<FSphereDensityPolicy>":
                    // aae7cb0 tests material virtual+70 twice. For the ordinary stored-bit
                    // resource, the second true read bypasses its fallback VF-name comparison.
                    shouldCache = materialContext.UsedWithFogVolumes;
                    return true;
                case "FFogVolumeApplyVertexShader":
                case "FFogVolumeApplyPixelShader":
                    // aae7d90 tail-calls material virtual+70 (IsUsedWithFogVolumes).
                    shouldCache = materialContext.UsedWithFogVolumes;
                    return true;
                case "TAOMeshVertexShader<0>":
                    // a9f9fc0: special and platform !=3; PC SM5 passes the platform check.
                    shouldCache = materialContext.IsSpecialEngineMaterial;
                    return true;
                case "TAOMeshVertexShader<1>":
                    // a9f9f80: VF+24 bit3, special, then platform !=3.
                    if (!materialContext.IsSpecialEngineMaterial) { shouldCache = false; return true; }
                    if (vertexFactoryContext?.SupportsPrecisePreviousPosition is not bool precisePreviousPosition)
                    {
                        shouldCache = false;
                        return false;
                    }
                    shouldCache = precisePreviousPosition;
                    return true;
                case "FHitMaskVertexShader":
                case "FHitMaskPixelShader":
                    // aaffc00 tail-calls material virtual+90 (IsSpecialEngineMaterial).
                    shouldCache = materialContext.IsSpecialEngineMaterial;
                    return true;
                case "TLightVertexShaderFSFXPointLightPolicyFNoStaticShadowingPolicy":
                case "TLightPixelShaderFSFXPointLightPolicyFNoStaticShadowingPolicy":
                    // aa5abb0: lit and platform !=3; ordinary SM5 passes the platform check.
                    shouldCache = !materialContext.IsUnlit;
                    return true;
                case "TLightVertexShaderFSphericalHarmonicLightPolicyFNoStaticShadowingPolicy":
                case "TLightPixelShaderFSphericalHarmonicLightPolicyFNoStaticShadowingPolicy":
                    // c35fa0 additionally vetoes material virtual+98 (terrain).
                    shouldCache = !materialContext.IsUnlit && !materialContext.IsTerrainMaterial;
                    return true;
                case "TLightMapDensityVertexShader<FNoLightMapPolicy>":
                case "TLightMapDensityPixelShader<FNoLightMapPolicy>":
                    // b10730: SM5 passes, special or stored masked; no debug/UV-count gate.
                    shouldCache = materialContext.IsSpecialEngineMaterial || materialContext.IsMasked;
                    return true;
                case "TLightMapDensityVertexShader<FDirectionalLightMapTexturePolicy>":
                case "TLightMapDensityPixelShader<FDirectionalLightMapTexturePolicy>":
                case "TLightMapDensityVertexShader<FSimpleLightMapTexturePolicy>":
                case "TLightMapDensityPixelShader<FSimpleLightMapTexturePolicy>":
                case "TLightMapDensityVertexShader<FDummyLightMapTexturePolicy>":
                case "TLightMapDensityPixelShader<FDummyLightMapTexturePolicy>":
                    // b10630/b10790/b106b0: common gate then the texture policy. No terrain veto.
                    if (!(materialContext.IsSpecialEngineMaterial || materialContext.IsMasked))
                    {
                        shouldCache = false;
                        return true;
                    }
                    if (shaderTypeName is "TLightMapDensityVertexShader<FDirectionalLightMapTexturePolicy>" or
                        "TLightMapDensityPixelShader<FDirectionalLightMapTexturePolicy>" && materialContext.RequiresSimpleStaticLighting)
                    {
                        shouldCache = false;
                        return true;
                    }
                    if (vertexFactoryContext == null)
                    {
                        shouldCache = false;
                        return false;
                    }
                    shouldCache = !materialContext.IsUnlit && vertexFactoryContext.SupportsStaticLighting &&
                        (materialContext.UsedWithStaticLighting || materialContext.IsSpecialEngineMaterial);
                    return true;
                case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFNoDensityPolicy":
                case "TBasePassVertexShaderFSHLightLightMapPolicyFNoDensityPolicy":
                case "TBasePassPixelShaderFDirectionalLightLightMapPolicyNoSkyLight":
                case "TBasePassPixelShaderFDirectionalLightLightMapPolicySkyLight":
                case "TBasePassPixelShaderFSHLightLightMapPolicyNoSkyLight":
                case "TBasePassPixelShaderFSHLightLightMapPolicySkyLight":
                    // aa2b550/aa2b4d0/aa2bd10 require VF+24 mask4, lit and non-decal.
                    // The VS queries blend but does not gate it. Unknown capability is unresolved.
                    if (vertexFactoryContext?.SupportsDynamicLighting is not bool supportsDynamicLighting)
                    {
                        shouldCache = false;
                        return false;
                    }
                    shouldCache = supportsDynamicLighting && !materialContext.IsUnlit && !materialContext.UsedWithDecals;
                    return true;
                case "FHitProxyVertexShader":
                case "FHitProxyPixelShader":
                    // bebbd0: SM5 platform passes. ExtraBitfield0x20 is the connected WPO
                    // state; special-engine success bypasses both subsequent conditions.
                    shouldCache = materialContext.IsSpecialEngineMaterial || materialContext.IsMasked || materialContext.HasWorldPositionOffset;
                    return true;
                case "FTextureDensityVertexShader":
                case "FTextureDensityPixelShader":
                    // c528b0: no WPO alternative; translation's coordinate count is required.
                    shouldCache = materialContext.NumUserTexCoords != 0 &&
                        (materialContext.IsSpecialEngineMaterial || materialContext.IsMasked) &&
                        (environmentContext?.AllowsDebugViewModes ?? true);
                    return true;
                case "FSFXWorldNormalVertexShader":
                case "FSFXWorldNormalPixelShader":
                    // aa6bd00/aa6bc90: special bypasses blend, never the debug gate. SM5 passes
                    // both platform branches. Debug eligibility is an engine-wide input.
                    shouldCache = (materialContext.IsSpecialEngineMaterial || !IsTranslucentBlend(materialContext.BlendMode)) &&
                        (environmentContext?.AllowsDebugViewModes ?? true);
                    return true;
                case "FVelocityVertexShader":
                case "FVelocityPixelShader":
                    // b169fe0: both stages reject the decal resource, not decal usage on an ordinary material.
                    shouldCache = materialContext.IsSpecialEngineMaterial || materialContext.IsMasked ||
                        (materialContext.IsTwoSided && !IsTranslucentBlend(materialContext.BlendMode));
                    if (!shouldCache) return true;
                    if (materialContext.DecalFactoryFallback is not bool decalResource)
                    {
                        shouldCache = false;
                        return false;
                    }
                    shouldCache = !decalResource;
                    return true;
                // Policy0x7ff74aa2c610: simple-only veto, instancing veto, then sharedc7b0.
                case "TBasePassPixelShaderFDirectionalVertexLightMapPolicyNoSkyLight":
                case "TBasePassPixelShaderFDirectionalVertexLightMapPolicySkyLight":
                case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFNoDensityPolicy":
                    if (materialContext.RequiresSimpleStaticLighting)
                    {
                        shouldCache = false;
                        return true;
                    }
                    goto case "TBasePassPixelShaderFSimpleVertexLightMapPolicyNoSkyLight";

                // Native custom vector/simple policy gates c4a0/c2e0 use exact VF identities
                // and ordinary FMaterialResource getters ab525c0/ab52520, not guessed names.
                case "TBasePassPixelShaderFCustomVectorVertexLightMapPolicyNoSkyLight":
                case "TBasePassPixelShaderFCustomVectorVertexLightMapPolicySkyLight":
                case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFNoDensityPolicy":
                case "TBasePassPixelShaderFCustomSimpleVertexLightMapPolicyNoSkyLight":
                case "TBasePassPixelShaderFCustomSimpleVertexLightMapPolicySkyLight":
                case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFNoDensityPolicy":
                case "TBasePassPixelShaderFCustomVectorLightMapTexturePolicyNoSkyLight":
                case "TBasePassPixelShaderFCustomVectorLightMapTexturePolicySkyLight":
                case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFNoDensityPolicy":
                case "TBasePassPixelShaderFCustomSimpleLightMapTexturePolicyNoSkyLight":
                case "TBasePassPixelShaderFCustomSimpleLightMapTexturePolicySkyLight":
                case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFNoDensityPolicy":
                    if (materialContext.RequiresSimpleStaticLighting)
                    {
                        shouldCache = false;
                        return true;
                    }
                    if (string.IsNullOrWhiteSpace(vertexFactoryTypeName))
                    {
                        shouldCache = false;
                        return false;
                    }
                    if (vertexFactoryTypeName is not ("FLocalVertexFactory" or "FLocalDecalVertexFactory"))
                    {
                        shouldCache = false;
                        return true;
                    }
                    if (vertexFactoryContext == null)
                    {
                        shouldCache = false;
                        return false;
                    }
                    bool customUsage = shaderTypeName is "TBasePassPixelShaderFCustomVectorVertexLightMapPolicyNoSkyLight" or
                        "TBasePassPixelShaderFCustomVectorVertexLightMapPolicySkyLight" or
                        "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFNoDensityPolicy" or
                        "TBasePassPixelShaderFCustomVectorLightMapTexturePolicyNoSkyLight" or
                        "TBasePassPixelShaderFCustomVectorLightMapTexturePolicySkyLight" or
                        "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFNoDensityPolicy"
                        ? materialContext.UsedWithVectorLightMaps : materialContext.UsedWithSimpleLightMaps;
                    bool customTexturePolicy = shaderTypeName is "TBasePassPixelShaderFCustomVectorLightMapTexturePolicyNoSkyLight" or
                        "TBasePassPixelShaderFCustomVectorLightMapTexturePolicySkyLight" or
                        "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFNoDensityPolicy" or
                        "TBasePassPixelShaderFCustomSimpleLightMapTexturePolicyNoSkyLight" or
                        "TBasePassPixelShaderFCustomSimpleLightMapTexturePolicySkyLight" or
                        "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFNoDensityPolicy";
                    // Texture helpers c3c0/c200 reach c690 (no terrain veto), unlike vertex c7b0.
                    shouldCache = !materialContext.IsUnlit && (customTexturePolicy || !materialContext.IsTerrainMaterial) &&
                        vertexFactoryContext.SupportsStaticLighting &&
                        (materialContext.IsSpecialEngineMaterial || materialContext.UsedWithStaticLighting &&
                            (materialContext.UsedWithDecals || customUsage));
                    return true;
                // Directional policy 0x7ff74aa2c580, NoSky PS 0x7ff74aa2b620,
                // NoDensity VS 0x7ff74aa2be20. +0x178 rejects simple-only resources first.
                case "TBasePassPixelShaderFDirectionalLightMapTexturePolicyNoSkyLight":
                case "TBasePassPixelShaderFDirectionalLightMapTexturePolicySkyLight":
                case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFNoDensityPolicy":
                    if (materialContext.RequiresSimpleStaticLighting)
                    {
                        shouldCache = false;
                        return true;
                    }
                    goto case "TBasePassPixelShaderFSimpleLightMapTexturePolicyNoSkyLight";

                // Simple-vertex NoSky/Sky PS 0x7ff74aa2b810/0x7ff74aa2b780,
                // NoDensity VS 0x7ff74aa2c1a0. Shared gate +0x98 vetoes terrain;
                // ordinary Material/DecalMaterial resources are non-terrain in this API.
                case "TBasePassPixelShaderFSimpleVertexLightMapPolicyNoSkyLight":
                case "TBasePassPixelShaderFSimpleVertexLightMapPolicySkyLight":
                case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFNoDensityPolicy":
                    if (materialContext.IsTerrainMaterial)
                    {
                        shouldCache = false;
                        return true;
                    }
                    if (string.IsNullOrWhiteSpace(vertexFactoryTypeName))
                    {
                        shouldCache = false;
                        return false;
                    }
                    if (vertexFactoryTypeName.Contains("FInstancedStaticMeshVertex", StringComparison.Ordinal))
                    {
                        shouldCache = false;
                        return true;
                    }
                    goto case "TBasePassPixelShaderFSimpleLightMapTexturePolicyNoSkyLight";

                // Simple policy 0x7ff74aa2c720, NoSky/Sky PS 0x7ff74aa2b760/0x7ff74aa2b6f0,
                // NoDensity VS 0x7ff74aa2c0b0. SM5 is among its allowed PC platforms.
                // Shadow-texture registrations share 0x7ff74aa2c690, the same ordinary gate
                // with no platform restriction and no terrain veto (not vertex-buffer policy).
                case "TLightVertexShaderFDirectionalLightPolicyFShadowTexturePolicy":
                case "TLightPixelShaderFDirectionalLightPolicyFShadowTexturePolicy":
                case "TLightPixelShaderFPointLightPolicyFShadowTexturePolicy":
                case "TLightVertexShaderFPointLightPolicyFShadowTexturePolicy":
                case "TLightVertexShaderFSpotLightPolicyFShadowTexturePolicy":
                case "TLightPixelShaderFSpotLightPolicyFShadowTexturePolicy":
                case "TLightVertexShaderFDirectionalLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                case "TLightPixelShaderFDirectionalLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                case "TLightVertexShaderFPointLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                case "TLightPixelShaderFPointLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                case "TLightVertexShaderFSpotLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                case "TLightPixelShaderFSpotLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                case "TBasePassPixelShaderFSimpleLightMapTexturePolicyNoSkyLight":
                case "TBasePassPixelShaderFSimpleLightMapTexturePolicySkyLight":
                case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFNoDensityPolicy":
                    if (vertexFactoryContext == null)
                    {
                        shouldCache = false;
                        return false;
                    }
                    shouldCache = !materialContext.IsUnlit && vertexFactoryContext.SupportsStaticLighting &&
                        (materialContext.UsedWithStaticLighting || materialContext.IsSpecialEngineMaterial);
                    return true;

                // All six vertex-buffer light registrations share 0x7ff74aa2c7b0.
                case "TLightVertexShaderFDirectionalLightPolicyFShadowVertexBufferPolicy":
                case "TLightPixelShaderFDirectionalLightPolicyFShadowVertexBufferPolicy":
                case "TLightVertexShaderFPointLightPolicyFShadowVertexBufferPolicy":
                case "TLightPixelShaderFPointLightPolicyFShadowVertexBufferPolicy":
                case "TLightVertexShaderFSpotLightPolicyFShadowVertexBufferPolicy":
                case "TLightPixelShaderFSpotLightPolicyFShadowVertexBufferPolicy":
                    if (vertexFactoryContext == null)
                    {
                        shouldCache = false;
                        return false;
                    }
                    shouldCache = !materialContext.IsUnlit && vertexFactoryContext.SupportsStaticLighting &&
                        (materialContext.UsedWithStaticLighting || materialContext.IsSpecialEngineMaterial) &&
                        !materialContext.IsTerrainMaterial;
                    return true;

                // Callback 0x7ff74a916690 is unconditional.
                case "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight":
                // No-density VS callback 0x7ff74aa2bff0 returns true after querying blend mode.
                case "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy":
                    shouldCache = true;
                    return true;

                // Fog-density VS registrations share 0x7ff74aa2bf70, including the GPUSkin gate.
                case "TBasePassVertexShaderFNoLightMapPolicyFConstantDensityPolicy":
                case "TBasePassVertexShaderFNoLightMapPolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFNoLightMapPolicyFSphereDensityPolicy":
                    if (string.IsNullOrWhiteSpace(vertexFactoryTypeName))
                    {
                        shouldCache = false;
                        return false;
                    }
                    shouldCache = materialContext.BlendMode is EBlendMode.BLEND_Translucent or
                                      EBlendMode.BLEND_Additive or EBlendMode.BLEND_AlphaComposite
                                  && (materialContext.UsedWithFogVolumes ||
                                      !vertexFactoryTypeName.Contains("GPUSkin", StringComparison.Ordinal));
                    return true;

                // Callback 0x7ff74aa2b6d0: GetLightingModel() != MLM_Unlit (2).
                case "TBasePassPixelShaderFNoLightMapPolicySkyLight":
                case "TLightPixelShaderFDirectionalLightPolicyFNoStaticShadowingPolicy":
                case "TLightVertexShaderFDirectionalLightPolicyFNoStaticShadowingPolicy":
                case "TLightPixelShaderFPointLightPolicyFNoStaticShadowingPolicy":
                case "TLightVertexShaderFPointLightPolicyFNoStaticShadowingPolicy":
                case "TLightVertexShaderFSpotLightPolicyFNoStaticShadowingPolicy":
                case "TLightPixelShaderFSpotLightPolicyFNoStaticShadowingPolicy":
                    shouldCache = !materialContext.IsUnlit;
                    return true;

                // Callback 0x7ff74aaa7590.
                case "TDepthOnlySolidPixelShader":
                    shouldCache = materialContext.IsMasked
                                  || materialContext.UsesLitTranslucencyDepthPass
                                  || materialContext.UsesSurfaceTranslucencyDepthPass;
                    return true;
                case "FModShadowMeshVertexShader":
                case "FModShadowMeshPixelShader":
                    // Shared ac1fdd0: ordinary SM5 requires a lit, non-translucent material.
                    shouldCache = !materialContext.IsUnlit && !IsTranslucentBlend(materialContext.BlendMode);
                    return true;
                case "TDepthOnlyScreenDoorPixelShader":
                    // aaa7530 includes special-engine success; the solid variant does not.
                    shouldCache = materialContext.IsSpecialEngineMaterial || materialContext.IsMasked ||
                        materialContext.UsesLitTranslucencyDepthPass || materialContext.UsesSurfaceTranslucencyDepthPass;
                    return true;

                // Registration 0x7ff74a8ae040 -> 0x7ff74aaa7770.
                case "FTranslucencyPostRenderDepthPixelShader":
                    shouldCache = materialContext.UsesLitTranslucencyDepthPostpass ||
                                  materialContext.BlendMode == EBlendMode.BLEND_SoftMasked;
                    return true;

                // Both accumulated-distortion registrations share 0x7ff74aaa76d0.
                case "TDistortionMeshVertexShader<FDistortMeshAccumulatePolicy>":
                case "TDistortionMeshPixelShader<FDistortMeshAccumulatePolicy>":
                    shouldCache = materialContext.IsDistorted && materialContext.BlendMode is
                        EBlendMode.BLEND_Translucent or EBlendMode.BLEND_Additive or
                        EBlendMode.BLEND_Modulate or EBlendMode.BLEND_AlphaComposite;
                    return true;

                // Registration 0x7ff74a8ac930 -> 0x7ff74aaa7660. The postpass is an extra gate
                // beyond the solid depth PS; WPO uses the translator's copied ExtraBitfield bit.
                case "TDepthOnlyVertexShader<0>":
                    shouldCache = materialContext.IsSpecialEngineMaterial || materialContext.IsMasked
                                  || materialContext.HasWorldPositionOffset
                                  || materialContext.UsesLitTranslucencyDepthPass
                                  || materialContext.UsesLitTranslucencyDepthPostpass
                                  || materialContext.UsesSurfaceTranslucencyDepthPass;
                    return true;

                // Registration 0x7ff74a8ac810 -> 0x7ff74aaa75e0. These are substring tests.
                case "TDepthOnlyVertexShader<1>":
                    if (string.IsNullOrWhiteSpace(vertexFactoryTypeName))
                    {
                        shouldCache = false;
                        return false;
                    }
                    shouldCache = materialContext.IsSpecialEngineMaterial &&
                                  (vertexFactoryTypeName.Contains("FLocalVertex", StringComparison.Ordinal) ||
                                   vertexFactoryTypeName.Contains("FInstancedStaticMeshVertex", StringComparison.Ordinal) ||
                                   vertexFactoryTypeName.Contains("FSplineMeshVertex", StringComparison.Ordinal));
                    return true;

                // 0x7ff74ac1fcf0; OutputDepthToColor uses 0x7ff74ac1fd50, which accepts SP_SM5=5.
                case "TShadowDepthVertexShader<ShadowDepth_OutputDepth>":
                case "TShadowDepthVertexShader<ShadowDepth_PerspectiveCorrect>":
                case "TShadowDepthVertexShader<ShadowDepth_OutputDepthToColor>":
                    shouldCache = materialContext.IsSpecialEngineMaterial
                                  || materialContext.IsMasked
                                  || materialContext.CastLitTranslucencyShadowAsMasked
                                  || materialContext.HasWorldPositionOffset;
                    return true;

                // Callback 0x7ff74ac1fc80.
                case "TShadowDepthPixelShaderTRUETRUE":
                case "TShadowDepthPixelShaderFALSETRUE":
                    shouldCache = materialContext.IsSpecialEngineMaterial;
                    return true;

                // Both registrations assign 0x7ff74ac1fca0. WPO alone does not select these PSs.
                case "TShadowDepthPixelShaderTRUEFALSE":
                case "TShadowDepthPixelShaderFALSEFALSE":
                    shouldCache = materialContext.IsSpecialEngineMaterial || materialContext.IsMasked
                                  || materialContext.CastLitTranslucencyShadowAsMasked;
                    return true;

                default:
                    shouldCache = false;
                    return false;
            }
        }

        /// <summary>
        /// Evaluates a recovered vertex-factory ShouldCache callback. Context construction for
        /// ordinary Material/DecalMaterial produces IsTerrainMaterial=false; callers can supply
        /// the semantic terrain predicate without implying support for translating terrain graphs.
        /// Fluid callbacks inspect the exact registered source filename, not the type name. A known
        /// shaderTypeName resolves that filename; explicit source metadata supports direct gate audits.
        /// </summary>
        public static bool TryEvaluateKnownVertexFactoryShouldCache(string vertexFactoryTypeName,
            ShouldCacheMaterialContext materialContext, ShouldCacheVertexFactoryContext vertexFactoryContext,
            out bool shouldCache, ShouldCacheEnvironmentContext environmentContext = null, string shaderTypeName = null,
            string shaderSourceFileName = null)
        {
            ArgumentNullException.ThrowIfNull(vertexFactoryTypeName);
            ArgumentNullException.ThrowIfNull(materialContext);
            ArgumentNullException.ThrowIfNull(vertexFactoryContext);

            if (vertexFactoryTypeName == "FBioSPUSkinVertexFactory")
            {
                // a8a74c0 registers a913710 (XOR EAX,EAX; RET), regardless of material usage.
                // A known exclusion does not imply an implemented environment or archive layout.
                shouldCache = false;
                return true;
            }
            if (vertexFactoryTypeName == "FLocalVertexFactory")
            {
                // Registration 0x7ff74a8b3170 assigns 0x7ff74ab108e0, which returns
                // !material.IsTerrainMaterial(). Ordinary Material/DecalMaterial resources pass.
                // Static-lighting policy callbacks are shader-type gates, not this VF gate.
                shouldCache = !materialContext.IsTerrainMaterial;
                return true;
            }
            if (vertexFactoryTypeName == "FGPUSkinVertexFactory")
            {
                // Registration 0x7ff74a8b13c0 assigns 0x7ff74aaffba0. The decal veto applies
                // even to special-engine materials; it reads bUsedWithDecals, not the export class.
                shouldCache = (materialContext.UsedWithSkeletalMesh || materialContext.UsedWithFracturedMeshes ||
                               materialContext.IsSpecialEngineMaterial) && !materialContext.UsedWithDecals;
                return true;
            }
            if (vertexFactoryTypeName == "FGPUSkinMorphVertexFactory")
            {
                // aaffb60 -> aaff9c0; the decal veto also applies to special materials.
                shouldCache = (materialContext.UsedWithMorphTargets || materialContext.IsSpecialEngineMaterial) &&
                    (materialContext.UsedWithSkeletalMesh || materialContext.UsedWithFracturedMeshes ||
                     materialContext.IsSpecialEngineMaterial) && !materialContext.UsedWithDecals;
                return true;
            }
            if (vertexFactoryTypeName == "FInstancedStaticMeshVertexFactory")
            {
                // aaffc10 composes instanced usage, Local's non-terrain gate and a decal veto.
                shouldCache = (materialContext.UsedWithInstancedMeshes || materialContext.IsSpecialEngineMaterial) &&
                    !materialContext.IsTerrainMaterial && !materialContext.UsedWithDecals;
                return true;
            }
            if (vertexFactoryTypeName is "FLocalVertexFactoryApex" or "FGPUSkinVertexFactoryApex")
            {
                // ab64b40 has no terrain/decal veto or inherited Local cache gate.
                shouldCache = materialContext.UsedWithAPEXMeshes || materialContext.IsSpecialEngineMaterial;
                return true;
            }
            if (vertexFactoryTypeName is "FParticleVertexFactory" or "FParticleDynamicParameterVertexFactory" or
                "FParticleSubUVVertexFactory" or "FParticleSubUVDynamicParameterVertexFactory")
            {
                // aba58a0/aba5840 and ab86d10/ab86cb0 select opposite resource+ac bit3 values.
                bool subUV = vertexFactoryTypeName is "FParticleSubUVVertexFactory" or "FParticleSubUVDynamicParameterVertexFactory";
                bool dynamic = vertexFactoryTypeName is "FParticleDynamicParameterVertexFactory" or "FParticleSubUVDynamicParameterVertexFactory";
                bool usage = subUV ? materialContext.UsedWithParticleSubUV : materialContext.UsedWithParticleSprites;
                shouldCache = ((usage && materialContext.UsesDynamicParameter == dynamic) ||
                    materialContext.IsSpecialEngineMaterial) && !materialContext.UsedWithDecals;
                return true;
            }
            if (vertexFactoryTypeName is "FParticleBeamTrailVertexFactory" or "FParticleBeamTrailDynamicParameterVertexFactory")
            {
                // Standard ab74e90 does not veto dynamic usage; dynamic ab74e30 requires it.
                bool usage = materialContext.UsedWithBeamTrails &&
                    (vertexFactoryTypeName == "FParticleBeamTrailVertexFactory" || materialContext.UsesDynamicParameter);
                shouldCache = (usage || materialContext.IsSpecialEngineMaterial) && !materialContext.UsedWithDecals;
                return true;
            }
            if (vertexFactoryTypeName is "FTerrainVertexFactory" or "FTerrainMorphVertexFactory" or "FTerrainFullMorphVertexFactory")
            {
                // ac45560: IsTerrainMaterial (+98), not static-lighting usage, or special.
                shouldCache = (materialContext.IsTerrainMaterial || materialContext.IsSpecialEngineMaterial) && !materialContext.UsedWithDecals;
                return true;
            }
            if (vertexFactoryTypeName is "FTerrainDecalVertexFactory" or "FTerrainMorphDecalVertexFactory" or "FTerrainFullMorphDecalVertexFactory")
            {
                // ac454d0 inspects descriptor+0c (type name), unlike Fluid's source-file veto.
                shouldCache = false;
                if (shaderTypeName?.Contains("VertexLightMapPolicy", StringComparison.Ordinal) == true) return true;
                bool entry = materialContext.UsedWithDecals || materialContext.IsSpecialEngineMaterial &&
                    (environmentContext ?? new ShouldCacheEnvironmentContext()).AllowsDebugViewModes;
                if (!entry && materialContext.DecalFactoryFallback is not bool) return false;
                if (!(entry || materialContext.DecalFactoryFallback == true)) return true;
                if (string.IsNullOrEmpty(shaderTypeName)) return false;
                shouldCache = true;
                return true;
            }
            if (vertexFactoryTypeName is "FFluidVertexFactory" or "FFluidTessellationVertexFactory")
            {
                // aae7d10 uses case-sensitive wcsstr on descriptor+14 (source filename).
                // Descriptor+0c is the type name and is NOT inspected by this callback.
                shouldCache = false;
                if (!(materialContext.UsedWithFluidSurfaces || materialContext.IsSpecialEngineMaterial) || materialContext.UsedWithDecals)
                    return true;
                if (shaderTypeName != null && TryGetKnownShaderTypeRegistration(shaderTypeName, out var shaderType))
                {
                    if (shaderSourceFileName != null && shaderSourceFileName != shaderType.SourceFileName) return false;
                    shaderSourceFileName = shaderType.SourceFileName;
                }
                if (string.IsNullOrEmpty(shaderSourceFileName)) return false;
                shouldCache = !shaderSourceFileName.Contains("VelocityShader", StringComparison.Ordinal);
                return true;
            }
            if (vertexFactoryTypeName == "FFoliageVertexFactory")
            {
                // aae7da0: resource+e8 or special, then decal veto; environment callback is no-op.
                shouldCache = (materialContext.UsedWithFoliage || materialContext.IsSpecialEngineMaterial) &&
                    !materialContext.UsedWithDecals;
                return true;
            }
            if (vertexFactoryTypeName == "FLensFlareVertexFactory")
            {
                // aaffc90: resource+f8 or special, then decal veto; no terrain/light-model veto.
                shouldCache = (materialContext.UsedWithLensFlare || materialContext.IsSpecialEngineMaterial) &&
                    !materialContext.UsedWithDecals;
                return true;
            }
            if (vertexFactoryTypeName == "FSplineMeshVertexFactory")
            {
                // ac36090 also rejects platform 3; this compiler exclusively emits LE3 SM5 (5).
                shouldCache = (materialContext.UsedWithSplineMeshes || materialContext.IsSpecialEngineMaterial) &&
                    !materialContext.IsTerrainMaterial && !materialContext.UsedWithDecals;
                return true;
            }
            if (vertexFactoryTypeName == "FParticleInstancedMeshVertexFactory")
            {
                // ab74ee0 does not compose Local's terrain veto or ordinary instanced mesh usage.
                shouldCache = (materialContext.UsedWithInstancedMeshParticles || materialContext.IsSpecialEngineMaterial) &&
                    !materialContext.UsedWithDecals;
                return true;
            }
            if (vertexFactoryTypeName is "FGPUSkinDecalVertexFactory" or "FLocalDecalVertexFactory" or "FGPUSkinMorphDecalVertexFactory")
            {
                shouldCache = false;
                bool gpu = vertexFactoryTypeName != "FLocalDecalVertexFactory";
                // aaffad0 shares the decal entry gate, then aaff9c0's morph/skeletal gate.
                if (vertexFactoryTypeName == "FGPUSkinMorphDecalVertexFactory" &&
                    !materialContext.UsedWithMorphTargets && !materialContext.IsSpecialEngineMaterial)
                    return true;
                // Independent vetoes make an unknown fallback irrelevant to the final result.
                if (gpu ? !(materialContext.UsedWithSkeletalMesh || materialContext.UsedWithFracturedMeshes ||
                            materialContext.IsSpecialEngineMaterial) : materialContext.IsTerrainMaterial)
                    return true;
                bool ordinaryGate = materialContext.UsedWithDecals || materialContext.IsSpecialEngineMaterial &&
                    (environmentContext ?? new ShouldCacheEnvironmentContext()).AllowsDebugViewModes;
                if (!ordinaryGate && materialContext.DecalFactoryFallback is not bool)
                    return false;
                shouldCache = ordinaryGate || materialContext.DecalFactoryFallback == true;
                return true;
            }

            shouldCache = false;
            return false;
        }

        /// <summary>
        /// Resolve material inputs and expression links, following only the active branch of a static
        /// switch. A valid graph may still contain an expression whose Compile virtual has not been
        /// reproduced by <see cref="TranslateMaterialHlsl"/>.
        /// </summary>
        public GraphAnalysis AnalyzeGraph() => AnalyzeGraphCore(false);

        /// <summary>Analyzes selected resource inputs, respecting UseConstant and the native
        /// CustomSkylightDiffuse/TwoSidedLightingColor routes. Inherited literal inputs are supported;
        /// inherited expression roots require graph/package resolution. Does not certify completeness.</summary>
        public GraphAnalysis AnalyzeMaterialResourceGraph() => AnalyzeGraphCore(true);

        private GraphAnalysis AnalyzeGraphCore(bool includeMaterialResourceInputs)
        {
            var connected = new Dictionary<string, ExportEntry>(StringComparer.Ordinal);
            var reachable = new List<ExportEntry>();
            var visited = new HashSet<int>();
            var active = new HashSet<int>();
            var errors = new List<string>();
            var authoredMaterialProperties = material.GetProperties();
            var materialProperties = includeMaterialResourceInputs
                ? EffectiveMaterialProperties() : authoredMaterialProperties;

            foreach (string name in includeMaterialResourceInputs
                         ? MaterialInputs.Append("TwoSidedLightingColor") : MaterialInputs)
            {
                // Decal CompileProperty returns INDEX_NONE for WPO without reading the input.
                if (includeMaterialResourceInputs && name == "WorldPositionOffset" && material.ClassName == "DecalMaterial") continue;
                string inputName = includeMaterialResourceInputs && name == "CustomLightingDiffuse" ? "CustomSkylightDiffuse" : name;
                var input = materialProperties.GetProp<StructProperty>(inputName);
                if (includeMaterialResourceInputs && ResourceInputUsesConstant(input, name, errors))
                {
                    ValidateResourceConstant(input, name, errors);
                    continue;
                }
                if (input == null || input.GetProp<ObjectProperty>("Expression")?.Value is not int index || index == 0)
                {
                    continue;
                }

                if (includeMaterialResourceInputs && authoredMaterialProperties.GetProp<StructProperty>(inputName) == null)
                {
                    // An inherited UIndex belongs to its declaring graph package. Do not resolve
                    // it against this material's table, where the same index may name another node.
                    errors.Add($"Material.{inputName} has an inherited expression reference requiring graph/package resolution.");
                    continue;
                }

                var expression = Resolve(index, $"Material.{name}");
                if (expression == null) continue;
                connected.Add(name, expression);
                Visit(expression);
            }

            var unreachable = new List<ExportEntry>();
            var authoredExpressions = authoredMaterialProperties.GetProp<ArrayProperty<ObjectProperty>>("Expressions");
            if (authoredExpressions != null)
            {
                foreach (var reference in authoredExpressions)
                {
                    if (reference.Value == 0) continue;
                    var expression = Resolve(reference.Value, "Material.Expressions");
                    if (expression != null && !visited.Contains(expression.UIndex))
                    {
                        unreachable.Add(expression);
                    }
                }
            }

            return new GraphAnalysis
            {
                IncludesMaterialResourceInputs = includeMaterialResourceInputs,
                ConnectedInputs = connected,
                ReachableExpressions = reachable,
                UnreachableExpressions = unreachable,
                Errors = errors
            };

            ExportEntry Resolve(int index, string source)
            {
                if (!material.FileRef.TryGetEntry(index, out var entry) || entry is not ExportEntry export)
                {
                    errors.Add($"{source} references {index}, which is not a local MaterialExpression export.");
                    return null;
                }
                if (!export.IsA("MaterialExpression"))
                {
                    errors.Add($"{source} references {index}, which is not a MaterialExpression export.");
                    return null;
                }
                return export;
            }

            void Visit(ExportEntry expression)
            {
                if (visited.Contains(expression.UIndex)) return;
                if (!active.Add(expression.UIndex))
                {
                    errors.Add($"Expression cycle at {expression.InstancedFullPath}.");
                    return;
                }

                var properties = expression.GetProperties();
                foreach (var input in EnumerateSelectedInputs(expression, properties))
                {
                    int index = input.GetProp<ObjectProperty>("Expression")?.Value ?? 0;
                    if (index == 0) continue;
                    var child = Resolve(index, expression.InstancedFullPath + "." + input.Name.Instanced);
                    if (child != null) Visit(child);
                }

                active.Remove(expression.UIndex);
                visited.Add(expression.UIndex);
                reachable.Add(expression);
            }
        }

        /// <summary>Translates using this snapshot's native MaterialTemplate.usf and retains its
        /// identity for compilation and checked detached-cache construction.</summary>
        public TranslationResult TranslateMaterialHlsl(LE3ShaderSourceSnapshot sources, bool useHalfTypes = false)
        {
            ArgumentNullException.ThrowIfNull(sources);
            var result = TranslateMaterialHlsl(sources.ReadSource("MaterialTemplate.usf"), useHalfTypes);
            result.SourceSnapshot = sources;
            return result;
        }

        /// <summary>Translates the supported SM5 material-resource input dispatch, including
        /// disconnected defaults, selection color and two-sided lighting color. The source snapshot
        /// is retained for checked compilation/cache construction. resourceForcesFloatUsage is the
        /// caller-resolved resource-local precision override; it is ORed with bForceFullPrecision.
        /// This is not full native parity.</summary>
        public TranslationResult TranslateMaterialResourceHlsl(LE3ShaderSourceSnapshot sources, bool resourceForcesFloatUsage = false)
        {
            ArgumentNullException.ThrowIfNull(sources);
            var result = TranslateMaterialHlslCore(sources.ReadSource("MaterialTemplate.usf"), false, true, resourceForcesFloatUsage);
            result.SourceSnapshot = sources;
            return result;
        }

        /// <summary>SM5 material-resource translation against caller-supplied template text.
        /// Ordinary resources compile WorldPositionOffset as a vertex property; decal resources
        /// emit zero without compiling that input, independently of its stored connectivity predicate.</summary>
        public TranslationResult TranslateMaterialResourceHlsl(string materialTemplate, bool resourceForcesFloatUsage = false) =>
            TranslateMaterialHlslCore(materialTemplate, false, true, resourceForcesFloatUsage);

        /// <summary>
        /// Translates the currently supported LE3 expression subset and applies the result to the
        /// game's MaterialTemplate.usf text, with the native full-precision TexCoords substitution.
        /// This authored-root path omits resource input defaults and selection-color operations;
        /// use TranslateMaterialResourceHlsl for those. The template is supplied by the caller because
        /// the installed game owns it; LEC does not redistribute shader source.
        /// </summary>
        /// <remarks>
        /// The current subset is deliberately strict: constants, scalar/vector parameters, affine-unpack
        /// 2D/cube texture samples (including confirmed normal compression modes), Add/Multiply/
        /// Subtract/Divide (including folded numeric uniforms), DotProduct, CrossProduct, Power, Clamp,
        /// Abs/Floor/Ceil/Frac, Sine/Cosine, SquareRoot, OneMinus, Time, Normalize, Fmod,
        /// AppendVector, LinearInterpolate, VertexColor,
        /// TextureCoordinate, StaticSwitchParameter, and ordinary/static component masks.
        /// Unsupported nodes or input features return errors and no HLSL.
        /// </remarks>
        public TranslationResult TranslateMaterialHlsl(string materialTemplate, bool useHalfTypes = false) =>
            TranslateMaterialHlslCore(materialTemplate, useHalfTypes, false);

        private TranslationResult TranslateMaterialHlslCore(string materialTemplate, bool useHalfTypes, bool includeMaterialResourceInputs, bool resourceForcesFloatUsage = false)
        {
            ArgumentException.ThrowIfNullOrEmpty(materialTemplate);
            var graph = AnalyzeGraphCore(includeMaterialResourceInputs);
            var errors = new List<string>(graph.Errors);
            var propertyCode = new Dictionary<string, string>(StringComparer.Ordinal);
            var effectiveMaterialProperties = EffectiveMaterialProperties();
            bool forceFloatTexCoords = resourceForcesFloatUsage || effectiveMaterialProperties.GetProp<BoolProperty>("bForceFullPrecision")?.Value == true;
            if (includeMaterialResourceInputs && effectiveMaterialProperties.GetProp<EnumProperty>("BlendMode")?.Value.Name == "BLEND_Modulate" &&
                effectiveMaterialProperties.GetProp<EnumProperty>("LightingModel")?.Value.Name != "MLM_Unlit")
                errors.Add("Dynamically lit translucency is not supported for BLEND_Modulate materials.");
            if (!graph.IsValid)
            {
                return new TranslationResult
                {
                    IncludesMaterialResourceInputs = includeMaterialResourceInputs,
                    ForcesFloatTexCoords = forceFloatTexCoords,
                    UsesHalfTypes = useHalfTypes,
                    Material = material,
                    Compiler = this,
                    CompilationInput = CompilationInput,
                    Graph = graph,
                    PropertyCode = propertyCode,
                    Errors = errors,
                    NumUserTexCoords = 0,
                    MaterialHlsl = null
                };
            }

            string numericType = useHalfTypes ? "half" : "float";
            int localIndex = 0;
            bool usedWithDecals = IsUsedWithDecals();
            uint numUserTexCoords = usedWithDecals ? 2u : 0u;
            uint usingTransforms = 0;
            var authoredMaterialProperties = material.GetProperties();
            var materialProperties = includeMaterialResourceInputs ? effectiveMaterialProperties : authoredMaterialProperties;
            var uniformExpressionPool = new List<MaterialUniformExpression>();
            var uniformPixelVectors = new List<MaterialUniformExpression>();
            var uniformPixelScalars = new List<MaterialUniformExpression>();
            var uniform2DTextures = new List<MaterialUniformExpressionTexture>();
            var uniformCubeTextures = new List<MaterialUniformExpressionTexture>();
            var uniformExpressionTextures = new List<int>();
            // Texture uniforms created by CustomTexture before first code access have no global
            // texture index yet; native assigns it lazily, so remember the reference until then.
            var unregisteredTextureUniforms = new Dictionary<MaterialUniformExpressionTexture, int>(ReferenceEqualityComparer.Instance);
            // Native numbers CustomExpressionN across every property of one translation.
            var customExpressionImplementations = new List<string>();
            // Per-translation only: repeated samples share one metadata snapshot, but a later
            // translation can observe updated inputs without a stale compiler-global cache.
            var textureMetadata = new Dictionary<int, TextureCompilationMetadata>();
            var fontTexturePages = new Dictionary<int, IEntry[]>();
            var invalidFontPageReferences = new Dictionary<int, HashSet<int>>();
            var uniformVertexVectors = new List<MaterialUniformExpression>();
            var uniformVertexScalars = new List<MaterialUniformExpression>();
            // The native uniform emitter reuses the first matching code chunk within each
            // material property, independently of the cross-property expression pool.
            var propertyUniformCodeValues = new Dictionary<MaterialUniformExpression, CodeValue>(ReferenceEqualityComparer.Instance);
            bool compilingVertexProperty = false;
            bool usesVertexColor = false;
            bool usesLightmapUVs = false;
            bool usesDynamicParameter = false;
            bool usesTime = false;
            bool usesSceneDepth = false;
            bool usesSceneColor = false;
            bool propertyHasAlignedScreenPosition = false;

            // Ordinary connected (or UseConstant) WPO compiles as a vertex property in MaterialInputs order.
            // Decal CompileProperty returns INDEX_NONE; GetFixedParameterCode emits zero without code access.
            bool ignoresResourceWorldPositionOffset = includeMaterialResourceInputs && material.ClassName == "DecalMaterial";
            var resourceWorldPositionOffset = includeMaterialResourceInputs && !ignoresResourceWorldPositionOffset
                ? materialProperties.GetProp<StructProperty>("WorldPositionOffset") : null;
            bool resourceWorldPositionOffsetConnected = resourceWorldPositionOffset != null &&
                (resourceWorldPositionOffset.GetProp<ObjectProperty>("Expression")?.Value is > 0 ||
                 ResourceInputUsesConstant(resourceWorldPositionOffset, "WorldPositionOffset", errors));

            foreach (string propertyName in MaterialInputs)
            {
                if (ignoresResourceWorldPositionOffset && propertyName == "WorldPositionOffset")
                {
                    propertyCode[propertyName] = "\treturn 0";
                    continue;
                }
                if (includeMaterialResourceInputs && propertyName == "WorldPositionOffset" && !resourceWorldPositionOffsetConnected) continue;
                var input = materialProperties.GetProp<StructProperty>(includeMaterialResourceInputs && propertyName == "CustomLightingDiffuse" ? "CustomSkylightDiffuse" : propertyName);
                if (!includeMaterialResourceInputs && (input?.GetProp<ObjectProperty>("Expression")?.Value is not int expressionIndex || expressionIndex == 0))
                {
                    continue;
                }

                compilingVertexProperty = propertyName == "WorldPositionOffset";
                propertyUniformCodeValues.Clear();
                propertyHasAlignedScreenPosition = false;
                var statements = new List<string>();
                var cache = new Dictionary<(int UIndex, int OutputIndex), CodeValue>();
                CodeValue code;
                if (includeMaterialResourceInputs)
                {
                    // CompileProperty creates and RGB-masks this parameter before every pixel
                    // root, even when that root never consumes it. Mask access packs its slot.
                    var selection = propertyName == "WorldPositionOffset" ? null
                        : ComponentMask(VectorParameter("SelectionColor", LinearColor.Black), true, true, true, false, propertyName);
                    if (propertyName == "DiffuseColor")
                    {
                        var factor = CompileArithmetic(Constant(1), selection, "Subtract", propertyName, statements);
                        code = CompileResourceInput(input, propertyName, statements, cache);
                        if (code != null) code = CompileArithmetic(ForceCastAccessed(code, MaterialValueType.Vector3), factor, "Multiply", propertyName, statements);
                    }
                    else if (propertyName == "TwoSidedLightingMask")
                    {
                        var color = CompileResourceInput(materialProperties.GetProp<StructProperty>("TwoSidedLightingColor"), "TwoSidedLightingColor", statements, cache);
                        code = CompileResourceInput(input, propertyName, statements, cache);
                        if (code != null && color != null) code = CompileArithmetic(ForceCastAccessed(code, MaterialValueType.Scalar, 0xf), color, "Multiply", propertyName, statements);
                    }
                    else
                    {
                        code = CompileResourceInput(input, propertyName, statements, cache);
                        if (code != null && propertyName == "EmissiveColor")
                            code = CompileArithmetic(ForceCastAccessed(code, MaterialValueType.Vector3), selection, "Add", propertyName, statements);
                    }
                }
                else
                {
                    try { code = CompileInput(input, propertyName, statements, cache); }
                    catch (IndexNoneSignal) { code = null; }
                }
                if (code == null) continue;

                MaterialValueType expected = ExpectedType(propertyName);
                // Translate ab58be0 invokes ForceCast with exact-match=false and replication=false
                // for every property. MCT_Float=0xf intersects all numeric targets; other widths
                // truncate inline or zero-pad, retaining the source uniform layout/type.
                code = ForceCastAccessed(code, expected);

                var builder = new StringBuilder();
                foreach (string statement in statements) builder.Append(statement);
                builder.Append("\treturn ").Append(code.Code);
                propertyCode[propertyName] = builder.ToString();
            }

            string materialHlsl = null;
            if (includeMaterialResourceInputs && (usesSceneDepth || usesSceneColor))
            {
                string blendName = effectiveMaterialProperties.GetProp<EnumProperty>("BlendMode")?.Value.Name ?? "BLEND_Opaque";
                if (!Enum.TryParse<EBlendMode>(blendName, out var blend) || !Enum.IsDefined(blend))
                    errors.Add($"Unknown LE3 blend mode {blendName}.");
                else if (!IsTranslucentBlend(blend) && effectiveMaterialProperties.GetProp<BoolProperty>("bUsedWithMaterialEffect")?.Value != true)
                {
                    if (usesSceneDepth) errors.Add("Only transparent materials can read from scene depth.");
                    if (usesSceneColor) errors.Add("Only transparent materials can read from scene color.");
                }
            }
            if (includeMaterialResourceInputs && usedWithDecals && usesSceneColor)
                errors.Add("Materials used with decals must not read from scene color.");
            // Translate initializes decal count to two, then checks the final count after all
            // properties. UV remapping to index zero does not suppress requested-count growth.
            if (usedWithDecals && numUserTexCoords > 2)
                errors.Add($"Decal materials can only use UV0 and UV1, currently setup to use {numUserTexCoords} UV sets.");
            if (errors.Count == 0)
            {
                try
                {
                    string uniformDeclarations = BuildUniformDeclarations(numericType, uniformPixelVectors.Count,
                        uniformPixelScalars.Count, uniform2DTextures.Count, uniformCubeTextures.Count,
                        uniformVertexVectors.Count, uniformVertexScalars.Count);
                    // Custom implementations follow the uniform/sampler declarations in the same slot.
                    uniformDeclarations += string.Concat(customExpressionImplementations.Select(implementation => implementation + "\r\n\r\n"));
                    materialHlsl = ApplyMaterialTemplate(materialTemplate, propertyCode, numUserTexCoords, numericType,
                        uniformDeclarations, materialProperties.GetProp<FloatProperty>("OpacityMaskClipValue")?.Value ?? 0.3333f);
                    if (forceFloatTexCoords)
                        materialHlsl = materialHlsl.Replace("half2 TexCoords[NUM_MATERIAL_TEXCOORDS];", "float2 TexCoords[NUM_MATERIAL_TEXCOORDS];", StringComparison.Ordinal);
                }
                catch (Exception exception) when (exception is FormatException or ArgumentException)
                {
                    errors.Add(exception.Message);
                }
            }

            return new TranslationResult
            {
                IncludesMaterialResourceInputs = includeMaterialResourceInputs,
                ForcesFloatTexCoords = forceFloatTexCoords,
                UsesHalfTypes = useHalfTypes,
                Material = material,
                Compiler = this,
                CompilationInput = CompilationInput,
                Graph = graph,
                PropertyCode = propertyCode,
                Errors = errors,
                UniformPixelVectorExpressions = new ReadOnlyCollection<MaterialUniformExpression>(uniformPixelVectors),
                UniformPixelScalarExpressions = new ReadOnlyCollection<MaterialUniformExpression>(uniformPixelScalars),
                Uniform2DTextureExpressions = new ReadOnlyCollection<MaterialUniformExpressionTexture>(uniform2DTextures),
                UniformCubeTextureExpressions = new ReadOnlyCollection<MaterialUniformExpressionTexture>(uniformCubeTextures),
                UniformExpressionTextures = new ReadOnlyCollection<int>(uniformExpressionTextures),
                UniformVertexVectorExpressions = new ReadOnlyCollection<MaterialUniformExpression>(uniformVertexVectors),
                UniformVertexScalarExpressions = new ReadOnlyCollection<MaterialUniformExpression>(uniformVertexScalars),
                NumUserTexCoords = numUserTexCoords,
                UsingTransforms = usingTransforms,
                UsesVertexColor = usesVertexColor,
                UsesLightmapUVs = usesLightmapUVs,
                UsesDynamicParameter = usesDynamicParameter,
                UsesTime = usesTime,
                UsesSceneDepth = usesSceneDepth,
                UsesSceneColor = usesSceneColor,
                MaterialHlsl = materialHlsl
            };

            CodeValue VectorParameter(NameReference name, LinearColor defaultValue)
            {
                var uniform = uniformExpressionPool.OfType<MaterialUniformExpressionVectorParameter>()
                    .FirstOrDefault(p => p.ParameterName == name && LE3UniformExpressionComparer.ColorIdentical(p.DefaultValue, defaultValue));
                if (uniform == null)
                {
                    uniform = new MaterialUniformExpressionVectorParameter
                    {
                        ExpressionType = "FMaterialUniformExpressionVectorParameter", ParameterName = name, DefaultValue = defaultValue
                    };
                    uniformExpressionPool.Add(uniform);
                }
                return UniformValue(MaterialValueType.Vector4, null, uniform, false);
            }

            CodeValue ScalarParameter(NameReference name, float defaultValue)
            {
                var uniform = uniformExpressionPool.OfType<MaterialUniformExpressionScalarParameter>()
                    .FirstOrDefault(p => p.ParameterName == name && LE3UniformExpressionComparer.FloatIdentical(p.DefaultValue, defaultValue));
                if (uniform == null)
                {
                    uniform = new MaterialUniformExpressionScalarParameter
                    {
                        ExpressionType = "FMaterialUniformExpressionScalarParameter", ParameterName = name, DefaultValue = defaultValue
                    };
                    uniformExpressionPool.Add(uniform);
                }
                return UniformValue(MaterialValueType.Scalar, null, uniform, false);
            }

            CodeValue CompileResourceInput(StructProperty input, string name, List<string> statements, Dictionary<(int UIndex, int OutputIndex), CodeValue> cache)
            {
                bool constant = ResourceInputUsesConstant(input, name, errors);
                if (!constant && input?.GetProp<ObjectProperty>("Expression")?.Value is > 0)
                {
                    CodeValue compiled;
                    // FColorMaterialInput::Compile ab49c60 substitutes its default for a silent -1.
                    try { compiled = CompileInput(input, name, statements, cache); }
                    catch (IndexNoneSignal) { compiled = null; }
                    if (compiled != null) return compiled;
                }
                if (name is "DiffusePower" or "SpecularPower" or "Opacity" or "OpacityMask" or "TwoSidedLightingMask")
                    return Constant(constant ? input.GetProp<FloatProperty>("Constant")?.Value ?? 0f : name == "SpecularPower" ? 15f : name == "TwoSidedLightingMask" ? 0f : 1f);
                if (name == "Distortion")
                    return Vector(MaterialValueType.Vector2, constant ? [input.GetProp<FloatProperty>("ConstantX")?.Value ?? 0f, input.GetProp<FloatProperty>("ConstantY")?.Value ?? 0f] : [0f, 0f]);
                if (name is "Normal" or "AnisotropicDirection" or "WrapLightingCoefficients" or "WorldPositionOffset")
                {
                    var vector = constant ? input.GetProp<StructProperty>("Constant") : null;
                    float x = vector?.GetProp<FloatProperty>("X")?.Value ?? 0f;
                    float y = vector?.GetProp<FloatProperty>("Y")?.Value ?? (!constant && name == "AnisotropicDirection" ? 1f : 0f);
                    float z = vector?.GetProp<FloatProperty>("Z")?.Value ?? (!constant && name == "Normal" ? 1f : 0f);
                    return Vector(MaterialValueType.Vector3, [x, y, z]);
                }
                // Color input helper ab49c60 uses the native Pow22 byte table, not sRGB.
                var color = constant ? input.GetProp<StructProperty>("Constant") : null;
                // Ordinary resource b94b6b8+28 -> ab194a0 passes ff000000; the decal
                // resource b952a60+28 -> aa74560 passes ff808080 for diffuse/specular.
                // Resource class, not bUsedWithDecals, selects the callback/default.
                // UseConstant=false ignores the stored color and uses this caller fallback.
                byte fallback = name is "DiffuseColor" or "SpecularColor" && material.IsA("DecalMaterial") ? (byte)128 :
                    name == "TwoSidedLightingColor" ? (byte)255 : (byte)0;
                float Channel(string component) => NativeColorByteToLinear(color?.GetProp<ByteProperty>(component)?.Value ?? (constant ? (byte)0 : fallback));
                return Vector(MaterialValueType.Vector3, [Channel("R"), Channel("G"), Channel("B")]);
            }

            CodeValue CompileLerp(CodeValue a, CodeValue b, CodeValue alpha, string source, List<string> statements)
            {
                var resultType = ArithmeticResultType(a.Type, b.Type, source);
                if (resultType == null) return null;
                // CoerceParameter access order is Alpha, B, A. A mismatched scalar alpha
                // targets MCT_Float1=1 (numericType cast), not MCT_Float=0xf.
                string alphaCode = alpha.Type == resultType.Value ? alpha.Code
                    : alpha.Type == MaterialValueType.Scalar ? alpha.NativeNumericType == 1 ? alpha.Code : $"{numericType}({alpha.Code})" : null;
                if (alphaCode == null)
                {
                    errors.Add($"{source}: LinearInterpolate alpha coercion from {alpha.Type} to float1 is not supported.");
                    return null;
                }
                string bCode = CoerceScalarOperand(b, resultType.Value);
                string aCode = CoerceScalarOperand(a, resultType.Value);
                string local = $"Local{localIndex++}";
                statements.Add($"\t{DescribeType(resultType.Value)} {local} = lerp({aCode},{bCode},{alphaCode});\r\n");
                // Native Lerp never attaches a folded uniform expression, even for constants.
                return new CodeValue(resultType.Value, local, true);
            }

            CodeValue CompileIf(CodeValue a, CodeValue b, CodeValue greater, CodeValue equal, CodeValue less,
                string source, List<string> statements)
            {
                // Translator If 0x7ff74ab50bf0 promotes equal/less, then greater/result.
                int resultType = IfArithmeticType(equal.NativeNumericType, less.NativeNumericType);
                if (resultType == 0) return null;
                resultType = IfArithmeticType(greater.NativeNumericType, resultType);
                if (resultType == 0) return null;
                var type = resultType switch
                {
                    2 => MaterialValueType.Vector2, 4 => MaterialValueType.Vector3,
                    8 => MaterialValueType.Vector4, _ => MaterialValueType.Scalar
                };
                // ForceCast(default flags) accesses dynamic uniforms greater/equal/less
                // first. MCT_Float intersects vector types: native leaves those scalar
                // branch strings unchanged, relying on HLSL ternary conversion.
                string greaterCode = greater.Code, equalCode = equal.Code, lessCode = less.Code;
                // Subsequent GetParameterCode argument order is less/equal/greater/B/A/B/A.
                string bCode = b.Code, aCode = a.Code;
                string local = $"Local{localIndex++}";
                statements.Add($"\t{DescribeType(type)} {local} = (({aCode} >= {bCode}) ? ({aCode} > {bCode} ? {greaterCode} : {equalCode}) : {lessCode});\r\n");
                return new CodeValue(type, local, true, ExplicitNativeNumericType: resultType);
                int IfArithmeticType(int left, int right)
                {
                    if ((left & right) != 0) return left == 0xf ? right : left;
                    errors.Add($"{source}: If arithmetic between native numeric types 0x{left:x} and 0x{right:x} is undefined.");
                    return 0;
                }
            }

            CodeValue CompileTime(bool realTime)
            {
                usesTime = true;
                // Both clock producers set the ordinary resource's force-float bit before
                // emission. This affects the later TexCoords template substitution, not the
                // independently selected float/half numeric code-chunk mode.
                forceFloatTexCoords = true;
                Type uniformType = realTime ? typeof(MaterialUniformExpressionRealTime) : typeof(MaterialUniformExpressionTime);
                var uniform = uniformExpressionPool.FirstOrDefault(p => p.GetType() == uniformType);
                if (uniform == null)
                {
                    uniform = realTime ? new MaterialUniformExpressionRealTime() : new MaterialUniformExpressionTime();
                    uniform.ExpressionType = realTime ? "FMaterialUniformExpressionRealTime" : "FMaterialUniformExpressionTime";
                    uniformExpressionPool.Add(uniform);
                }
                // Native +68/+70 use empty definition text, MCT_Float, IsConstant=false.
                return UniformValue(MaterialValueType.Scalar, "", uniform, isConstant: false);
            }

            CodeValue CompileAppend(CodeValue a, CodeValue b, string source)
            {
                int width = (int)a.Type + (int)b.Type;
                if (width > 4)
                {
                    // Native yields MCT_Unknown, not a truncated vector.
                    errors.Add($"{source} AppendVector produces {width} components; only 2-4 are supported.");
                    return null;
                }
                string right = b.Code, left = a.Code;
                var type = (MaterialValueType)width;
                string code = $"{DescribeType(type)}({left},{right})";
                if (a.PerPixel || b.PerPixel) return new CodeValue(type, code, true);
                var uniform = uniformExpressionPool.OfType<MaterialUniformExpressionAppendVector>()
                    .FirstOrDefault(p => p.NumComponentsA == (uint)a.Type &&
                        ReferenceEquals(p.A, a.Uniform) && ReferenceEquals(p.B, b.Uniform));
                if (uniform == null)
                {
                    uniform = new MaterialUniformExpressionAppendVector
                    {
                        ExpressionType = "FMaterialUniformExpressionAppendVector", A = a.Uniform,
                        B = b.Uniform, NumComponentsA = (uint)a.Type
                    };
                    uniformExpressionPool.Add(uniform);
                }
                return UniformValue(type, code, uniform, a.IsConstant && b.IsConstant);
            }

            CodeValue CompilePeriodicHint(CodeValue input)
            {
                // Native ab536b0 leaves nonuniform chunks unchanged. Uniform hints retain
                // the original definition/type; fraction reduction happens on the CPU only.
                if (input.PerPixel) return input;
                string code = input.Code;
                var uniform = uniformExpressionPool.OfType<MaterialUniformExpressionPeriodic>()
                    .FirstOrDefault(p => ReferenceEquals(p.X, input.Uniform));
                if (uniform == null)
                {
                    uniform = new MaterialUniformExpressionPeriodic
                    {
                        ExpressionType = "FMaterialUniformExpressionPeriodic", X = input.Uniform
                    };
                    uniformExpressionPool.Add(uniform);
                }
                return UniformValue(input.Type, code, uniform, input.IsConstant);
            }

            CodeValue ForceCastDefault(CodeValue value, MaterialValueType target, int? nativeTargetOverride = null)
            {
                int nativeTarget = nativeTargetOverride ?? (target switch { MaterialValueType.Scalar => 1, MaterialValueType.Vector2 => 2,
                    MaterialValueType.Vector3 => 4, _ => 8 });
                if ((value.NativeNumericType & nativeTarget) != 0) return value;
                string code = value.Code; // Dynamic uniforms must be accessed before the inline cast.
                int sourceWidth = (int)value.Type, targetWidth = (int)target;
                if (sourceWidth > targetWidth) code += "." + "rgba"[..targetWidth];
                else if (sourceWidth < targetWidth)
                    code = DescribeType(target) + "(" + code + string.Concat(Enumerable.Repeat(",0", targetWidth - sourceWidth)) + ")";
                return new CodeValue(target, code, true, ExplicitNativeNumericType: nativeTarget);
            }

            CodeValue ForceCastAccessed(CodeValue value, MaterialValueType target, int? nativeTargetOverride = null)
            {
                // Native ForceCast accesses nonconstant uniform chunks BEFORE testing type
                // compatibility, even when its default bit-intersection test needs no cast.
                if (!value.PerPixel && !value.IsConstant)
                    value = new CodeValue(value.Type, value.Code, true, ExplicitNativeNumericType: value.NativeNumericType);
                return ForceCastDefault(value, target, nativeTargetOverride);
            }

            CodeValue CompileDefaultTextureCoordinate(List<string> statements, uint coordinateIndex = 0)
            {
                if (compilingVertexProperty)
                {
                    errors.Add("Invalid node used in vertex shader input!");
                    return null;
                }
                numUserTexCoords = Math.Max(numUserTexCoords, coordinateIndex + 1u);
                string local = $"Local{localIndex++}";
                statements.Add($"\t{numericType}2 {local} = Parameters.TexCoords[{(usedWithDecals ? 0u : coordinateIndex)}].xy;\r\n");
                return new CodeValue(MaterialValueType.Vector2, local, true);
            }

            CodeValue CompilePixelDestination(string kind, List<string> statements)
            {
                if (compilingVertexProperty)
                {
                    errors.Add("Invalid node used in vertex shader input!");
                    return null;
                }
                // Translator +140/+148/+150: Normalize arguments are unused. PixelDepth
                // is inline Float1 and does not mark either framebuffer read.
                if (kind == "PixelDepth")
                    return new CodeValue(MaterialValueType.Scalar, "Parameters.ScreenPosition.w", true, ExplicitNativeNumericType: 1);
                bool color = kind == "DestColor";
                if (color) usesSceneColor = true; else usesSceneDepth = true;
                var type = color ? MaterialValueType.Vector4 : MaterialValueType.Scalar;
                string local = $"Local{localIndex++}";
                statements.Add($"\t{DescribeType(type)} {local} = {(color ? "PreviousLighting" : "PreviousDepth")}(Parameters.ScreenPosition);\r\n");
                return new CodeValue(type, local, true, ExplicitNativeNumericType: color ? 8 : 1);
            }

            CodeValue CompileDepthBiasBlend(ExportEntry expression, PropertyCollection properties, CodeValue sample,
                List<string> statements, Dictionary<(int UIndex, int OutputIndex), CodeValue> cache)
            {
                // Compile ab15b80 composes ordinary callbacks, not DepthBiasedBlend's
                // three-argument helper. Keep creation and access order even for zero scale.
                var color = CompilePixelDestination("DestColor", statements);
                var pixelDepth = CompilePixelDestination("PixelDepth", statements);
                var destDepth = CompilePixelDestination("DestDepth", statements);
                if (color == null || pixelDepth == null || destDepth == null) return null;
                var zero = Constant(0f);
                var one = Constant(1f);
                var biasInput = properties.GetProp<StructProperty>("Bias");
                CodeValue bias = one;
                if (biasInput?.GetProp<ObjectProperty>("Expression")?.Value is not (null or 0))
                {
                    var input = CompileInput(biasInput, expression.InstancedFullPath + ".Bias", statements, cache);
                    if (input == null) return null;
                    bias = CompileArithmetic(one, input, "Subtract", expression.InstancedFullPath, statements);
                    if (bias == null) return null;
                }
                var scale = Constant(properties.GetProp<FloatProperty>("BiasScale")?.Value ?? 1f);
                var scaledBias = CompileArithmetic(bias, scale, "Multiply", expression.InstancedFullPath, statements);
                if (scaledBias == null) return null;
                var threshold = CompileArithmetic(destDepth, scaledBias, "Subtract", expression.InstancedFullPath, statements);
                var delta = CompileArithmetic(destDepth, pixelDepth, "Subtract", expression.InstancedFullPath, statements);
                if (threshold == null || delta == null) return null;
                var ratio = CompileArithmetic(delta, scaledBias, "Divide", expression.InstancedFullPath, statements);
                if (ratio == null) return null;
                var amount = CompileClamp(ratio, zero, one, expression.InstancedFullPath, statements);
                if (amount == null) return null;
                var blended = CompileLerp(color, sample, amount, expression.InstancedFullPath, statements);
                if (blended == null) return null;
                // Translator If permits explicit Float1 comparisons. The separate If
                // expression's exact MCT_Float input validation does not belong here.
                var inner = CompileIf(destDepth, pixelDepth, blended, blended, color, expression.InstancedFullPath, statements);
                return inner == null ? null : CompileIf(pixelDepth, threshold, inner, inner, sample, expression.InstancedFullPath, statements);
            }

            string CoerceSceneCoordinates(CodeValue coordinates)
            {
                if ((coordinates.NativeNumericType & 2) == 0)
                {
                    errors.Add($"Coercion failed: {coordinates.Definition}: {DescribeType(coordinates.Type)} -> {DescribeType(MaterialValueType.Vector2)}");
                    return null;
                }
                string uv = coordinates.Code.Replace("half", "float", StringComparison.Ordinal);
                return coordinates.NativeNumericType == 2 ? uv : $"float2({uv},{uv})";
            }

            string CoerceNumericOperand(CodeValue operand, MaterialValueType target, int? nativeTargetOverride = null)
            {
                int nativeTarget = nativeTargetOverride ?? 1 << ((int)target - 1);
                if (operand.NativeNumericType == nativeTarget) return operand.Code;
                if ((operand.NativeNumericType & nativeTarget) == 0)
                {
                    // A failed native coercion describes the definition without packing a
                    // dynamic uniform. Do not introduce a resource-array access on this path.
                    errors.Add($"Coercion failed: {operand.Definition}: {DescribeType(operand.Type)} -> {DescribeType(target)}");
                    return null;
                }
                string code = operand.Code;
                if (nativeTarget == 0xf) return code;
                return target == MaterialValueType.Scalar ? $"{numericType}({code})"
                    : $"{DescribeType(target)}({string.Join(',', Enumerable.Repeat(code, (int)target))})";
            }

            CodeValue BioHalfAngle(List<string> statements)
            {
                string local = $"Local{localIndex++}";
                statements.Add($"\t{numericType}3 {local} = normalize( Parameters.TangentLightVector + Parameters.TangentCameraVector );\r\n");
                return new CodeValue(MaterialValueType.Vector3, local, true);
            }

            CodeValue CompileRawTextureSample(MaterialUniformExpressionTexture uniform, bool isCube,
                CodeValue coordinates, List<string> statements)
            {
                if (compilingVertexProperty)
                {
                    errors.Add("Invalid node used in vertex shader input!");
                    return null;
                }
                if (coordinates == null) return null;
                // Native TextureSample uses full-precision Float2 coercion for 2D UVs,
                // but ordinary numeric precision for cube Float3 coordinates.
                string coordinateCode = isCube ? CoerceNumericOperand(coordinates, MaterialValueType.Vector3)
                    : CoerceSceneCoordinates(coordinates);
                var textureExpressions = isCube ? uniformCubeTextures : uniform2DTextures;
                RegisterTextureUniform(uniform);
                int index = FindOrAddTextureReference(textureExpressions, uniform);
                // Texture coercion still runs after a UV coercion error in the native callback.
                // Preserve its diagnostic resource access while withholding invalid HLSL.
                if (coordinateCode == null) return null;
                string textureName = $"Texture{(isCube ? "Cube" : "2D")}_{index}";
                string sampleFunction = useHalfTypes ? isCube ? "h4texCUBE" : "h4tex2D" : isCube ? "texCUBE" : "tex2D";
                string local = $"Local{localIndex++}";
                statements.Add($"\t{numericType}4 {local} = {sampleFunction}({textureName},{coordinateCode});\r\n");
                return new CodeValue(MaterialValueType.Vector4, local, true);
            }

            CodeValue CompileTextureUnpack(CodeValue sample, string normalHelper, float[] unpackMin,
                float[] unpackMax, List<string> statements)
            {
                if (sample == null) return null;
                string unpackedLocal = sample.Code;
                bool canonicalNormal = normalHelper != null &&
                    unpackMin[0] == -1f && unpackMin[1] == -1f && unpackMin[2] == -1f && unpackMin[3] == 0f &&
                    unpackMax[0] == 1f && unpackMax[1] == 1f && unpackMax[2] == 1f && unpackMax[3] == 1f;
                if (normalHelper != null)
                {
                    string normalLocal = $"Local{localIndex++}";
                    statements.Add($"\t{numericType}4 {normalLocal} = {normalHelper}({unpackedLocal});\r\n");
                    unpackedLocal = normalLocal;
                    if (!canonicalNormal)
                    {
                        unpackedLocal = EmitVectorOperation(unpackedLocal, "+", [1f, 1f, 1f, 0f], statements);
                        unpackedLocal = EmitVectorOperation(unpackedLocal, "*", [0.5f, 0.5f, 0.5f, 1f], statements);
                    }
                }
                if (!canonicalNormal)
                {
                    float[] unpackScale = new float[4];
                    for (int component = 0; component < 4; component++)
                        unpackScale[component] = unpackMax[component] - unpackMin[component];
                    // Identity unpacking still creates the native Mul and Add chunks.
                    unpackedLocal = EmitVectorOperation(unpackedLocal, "*", unpackScale, statements);
                    unpackedLocal = EmitVectorOperation(unpackedLocal, "+", unpackMin, statements);
                }
                return new CodeValue(MaterialValueType.Vector4, unpackedLocal, true);
            }

            CodeValue CompileBioMod(CodeValue a, CodeValue b, List<string> statements)
            {
                if (a == null || b == null) return null;
                string left = a.Code;
                string right = CoerceNumericOperand(b, a.Type, a.NativeNumericType);
                if (right == null) return null;
                string code = $"fmod({left},{right})";
                if (a.PerPixel || b.PerPixel)
                {
                    string local = $"Local{localIndex++}";
                    statements.Add($"\t{DescribeType(a.Type)} {local} = {code};\r\n");
                    return new CodeValue(a.Type, local, true, ExplicitNativeNumericType: a.NativeNumericType);
                }
                var uniform = uniformExpressionPool.OfType<MaterialUniformExpressionBIOMod>()
                    .FirstOrDefault(p => ReferenceEquals(p.A, a.Uniform) && ReferenceEquals(p.B, b.Uniform));
                if (uniform == null)
                {
                    uniform = new MaterialUniformExpressionBIOMod
                    { ExpressionType = "FMaterialUniformExpressionBIOMod", A = a.Uniform, B = b.Uniform };
                    uniformExpressionPool.Add(uniform);
                }
                return UniformValue(a.Type, code, uniform, a.IsConstant && b.IsConstant);
            }

            CodeValue CompileInput(StructProperty input, string source, List<string> statements,
                Dictionary<(int UIndex, int OutputIndex), CodeValue> cache, bool allowTexture = false)
            {
                int index = input.GetProp<ObjectProperty>("Expression")?.Value ?? 0;
                if (index == 0)
                {
                    errors.Add($"{source} is not connected.");
                    return null;
                }
                if (!material.FileRef.TryGetEntry(index, out var entry) || entry is not ExportEntry expression ||
                    !expression.IsA("MaterialExpression"))
                {
                    errors.Add($"{source} references {index}, which is not a local MaterialExpression export.");
                    return null;
                }

                int outputIndex = input.GetProp<IntProperty>("OutputIndex")?.Value ?? 0;
                if (outputIndex != 0)
                {
                    errors.Add($"{source} selects output {outputIndex} on {expression.InstancedFullPath}; multiple outputs are not supported yet.");
                    return null;
                }
                var value = CompileExpression(expression, outputIndex, source, statements, cache);
                // ExpressionInput_Compile ab49d00 returns -1 before masking; consumers then return it silently.
                if (ReferenceEquals(value, IndexNoneValue)) throw IndexNoneSignal.Instance;
                if (value?.IsTexture == true)
                {
                    // Fail closed: a texture value must never be masked, cast or used as a number.
                    // Native reports these at the consuming operation; the text here is not native.
                    if (ReadInputMaskFlag(input, "Mask"))
                    {
                        errors.Add($"{source} applies a component mask to a texture value.");
                        return null;
                    }
                    if (!allowTexture)
                    {
                        errors.Add($"{source} uses a texture value where a numeric value is required.");
                        return null;
                    }
                    return value;
                }
                if (value == null || !ReadInputMaskFlag(input, "Mask")) return value;
                return ApplyComponentMask(value, input, source);
            }

            CodeValue CompileExpression(ExportEntry expression, int outputIndex, string source, List<string> statements,
                Dictionary<(int UIndex, int OutputIndex), CodeValue> cache)
            {
                if (cache.TryGetValue((expression.UIndex, outputIndex), out var existing)) return existing;
                try
                {
                    return CompileExpressionCore(expression, outputIndex, source, statements, cache);
                }
                catch (IndexNoneSignal)
                {
                    // Native expression Compile checks connections, compiles every connected input
                    // (side effects included), and the translator op then returns -1 silently
                    // (Mul ab53220, Add ab465c0, Lerp ab52b10). CallExpression ab48ce0 caches the -1.
                    foreach (var input in expression.GetProperties().OfType<StructProperty>()
                                 .Where(p => p.StructType == "ExpressionInput" && p.GetProp<ObjectProperty>("Expression")?.Value is > 0))
                    {
                        try { CompileInput(input, expression.InstancedFullPath, statements, cache, allowTexture: true); }
                        catch (IndexNoneSignal) { }
                    }
                    cache[(expression.UIndex, outputIndex)] = IndexNoneValue;
                    return IndexNoneValue;
                }
            }

            CodeValue CompileExpressionCore(ExportEntry expression, int outputIndex, string source, List<string> statements,
                Dictionary<(int UIndex, int OutputIndex), CodeValue> cache)
            {
                var properties = expression.GetProperties();
                CodeValue value;

                switch (expression.ClassName)
                {
                    case "MaterialExpressionWorldPosition":
                        // Native ab5a530 marks WORLD_POS only at pixel frequency3, not vertex0.
                        if (!compilingVertexProperty) usingTransforms |= 0x8;
                        value = new CodeValue(MaterialValueType.Vector3, "Parameters.WorldPosition", true);
                        break;
                    case "MaterialExpressionWorldNormal":
                        if (!compilingVertexProperty)
                        {
                            errors.Add("Invalid node WorldNormal used in pixel shader input!");
                            return null;
                        }
                        value = new CodeValue(MaterialValueType.Vector3, "Parameters.WorldNormal", true);
                        break;
                    case "MaterialExpressionTransform":
                    {
                        var input = properties.GetProp<StructProperty>("Input");
                        if (input?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                        {
                            errors.Add("Missing Transform input vector");
                            return null;
                        }
                        var vector = CompileInput(input, expression.InstancedFullPath + ".Input", statements, cache);
                        if (vector == null) return null;
                        if (compilingVertexProperty)
                        {
                            errors.Add("Invalid node used in vertex shader input!");
                            return null;
                        }
                        if (vector.Type is not (MaterialValueType.Vector3 or MaterialValueType.Vector4))
                        {
                            errors.Add($"input must be a vector ({vector.Code}: {DescribeType(vector.Type)})");
                            return null;
                        }
                        string sourceSpace = ReadCoordinateSpace(properties, "TransformSourceType", "TRANSFORMSOURCE_Tangent", expression);
                        string targetSpace = ReadCoordinateSpace(properties, "TransformType", "TRANSFORM_World", expression);
                        if (sourceSpace == null || targetSpace == null) return null;
                        string sourceName = sourceSpace switch
                        {
                            "TRANSFORMSOURCE_World" => "World", "TRANSFORMSOURCE_Local" => "Local",
                            "TRANSFORMSOURCE_Tangent" => "Tangent", _ => null
                        };
                        string targetName = targetSpace switch
                        {
                            "TRANSFORM_World" => "World", "TRANSFORM_View" => "View", "TRANSFORM_Local" => "Local",
                            "TRANSFORM_Tangent" => "Tangent", _ => null
                        };
                        if (sourceName == null || targetName == null)
                        {
                            errors.Add($"{expression.InstancedFullPath}: invalid Transform coordinate space {sourceSpace} -> {targetSpace}.");
                            return null;
                        }
                        if (sourceName == targetName)
                        {
                            errors.Add("Source and Destination coordinate spaces must be different");
                            return null;
                        }
                        // Native ab583a0 converges on OR1 then CoerceParameter(Float3,false).
                        // Unlike ForceCast, that coercion cannot truncate explicit float4 input.
                        usingTransforms |= 1;
                        if (vector.Type == MaterialValueType.Vector4)
                        {
                            errors.Add($"Coercion failed: {vector.Definition}: {numericType}4 -> {numericType}3");
                            return null;
                        }
                        string prefix = sourceName == "Tangent" || targetName == "Tangent" ? "Parameters," : "";
                        string local = $"Local{localIndex++}";
                        statements.Add($"\t{DescribeType(MaterialValueType.Vector3)} {local} = Transform{sourceName}VectorTo{targetName}({prefix}{vector.Code});\r\n");
                        value = new CodeValue(MaterialValueType.Vector3, local, true);
                        break;
                    }
                    case "MaterialExpressionTransformPosition":
                    {
                        var input = properties.GetProp<StructProperty>("Input");
                        if (input?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                        {
                            errors.Add("Missing Transform Position input vector");
                            return null;
                        }
                        var vector = CompileInput(input, expression.InstancedFullPath + ".Input", statements, cache);
                        if (vector == null) return null;
                        if (compilingVertexProperty)
                        {
                            errors.Add("Invalid node used in vertex shader input!");
                            return null;
                        }
                        if (vector.Type != MaterialValueType.Vector4)
                        {
                            string message = vector.Type == MaterialValueType.Vector3
                                ? "Input must be a float4, append 1 to the vector" : "Input must be a float4";
                            errors.Add($"{message} ({vector.Code}: {DescribeType(vector.Type)})");
                            return null;
                        }
                        string space = ReadCoordinateSpace(properties, "TransformType", "TRANSFORMPOS_World", expression);
                        if (space == null) return null;
                        if (space != "TRANSFORMPOS_World")
                        {
                            errors.Add($"{expression.InstancedFullPath}: invalid TransformPosition coordinate space {space}.");
                            return null;
                        }
                        // Native ab58100 uses CoerceParameter(Float4,false), not an implicit append.
                        string local = $"Local{localIndex++}";
                        statements.Add($"\t{DescribeType(MaterialValueType.Vector4)} {local} = MulMatrix(InvViewProjectionMatrix, {vector.Code});\r\n");
                        value = new CodeValue(MaterialValueType.Vector4, local, true);
                        break;
                    }
                    case "MaterialExpressionReflectionVector":
                        if (compilingVertexProperty)
                        {
                            errors.Add("Invalid node used in vertex shader input!");
                            return null;
                        }
                        // Native ab53f90 queries the ordinary material getter+190, not graph usage.
                        if (materialProperties.GetProp<BoolProperty>("bPerPixelCameraVector")?.Value == true)
                            usingTransforms |= 0x9;
                        value = new CodeValue(MaterialValueType.Vector3, "Parameters.TangentReflectionVector", true);
                        break;
                    case "MaterialExpressionCameraVector":
                        if (compilingVertexProperty)
                        {
                            errors.Add("Invalid node used in vertex shader input!");
                            return null;
                        }
                        value = new CodeValue(MaterialValueType.Vector3, "Parameters.TangentCameraVector", true);
                        break;
                    case "MaterialExpressionCameraWorldPosition":
                        value = new CodeValue(MaterialValueType.Vector3, "CameraWorldPos", true);
                        break;
                    case "MaterialExpressionObjectRadius":
                        // Native ab534f0: inlined MCT_Float (0xf), not a uniform-expression leaf.
                        value = new CodeValue(MaterialValueType.Scalar, "ObjectWorldPositionAndRadius.w", true);
                        break;
                    case "MaterialExpressionObjectOrientation":
                        // Native ab534c0: inlined float3 with no frequency or transform-usage guard.
                        value = new CodeValue(MaterialValueType.Vector3, "ObjectOrientation", true);
                        break;
                    case "MaterialExpressionObjectWorldPosition":
                        // Native ab53520: inline float3, no WORLD_POS usage bit.
                        value = new CodeValue(MaterialValueType.Vector3, "ObjectWorldPositionAndRadius.xyz", true);
                        break;
                    case "MaterialExpressionScreenPosition":
                    {
                        if (compilingVertexProperty)
                        {
                            errors.Add("Invalid node used in vertex shader input!");
                            return null;
                        }
                        if (properties.GetProp<BoolProperty>("ScreenAlign")?.Value == true)
                        {
                            // Native ab54fd0 uses AddCodeChunk for aligned output, not inline.
                            string local = $"Local{localIndex++}";
                            statements.Add($"\t{DescribeType(MaterialValueType.Vector4)} {local} = ScreenAlignedPosition(Parameters.ScreenPosition);\r\n");
                            value = new CodeValue(MaterialValueType.Vector4, local, true);
                            propertyHasAlignedScreenPosition = true;
                        }
                        else value = new CodeValue(MaterialValueType.Vector4, "Parameters.ScreenPosition", true);
                        break;
                    }
                    case "MaterialExpressionVertexColor":
                        // LE3 0x7ff74ab5a490 sets resource ExtraBitfield 0x40 and emits an
                        // inlined float4. Usage is recomputed for the resolved permutation.
                        usesVertexColor = true;
                        value = new CodeValue(MaterialValueType.Vector4, "Parameters.VertexColor", true);
                        break;
                    case "MaterialExpressionDynamicParameter":
                    {
                        if (compilingVertexProperty)
                        {
                            errors.Add("Invalid node used in vertex shader input!");
                            return null;
                        }
                        // ab4c9d0 sets resource ExtraBitfield bit3 before adding a float4 local.
                        usesDynamicParameter = true;
                        string local = $"Local{localIndex++}";
                        statements.Add($"\t{DescribeType(MaterialValueType.Vector4)} {local} = GetDynamicParameter(Parameters);\r\n");
                        value = new CodeValue(MaterialValueType.Vector4, local, true);
                        break;
                    }
                    case "MaterialExpressionMeshEmitterDynamicParameter":
                        // This override is a fixed vector parameter, not the particle interpolant.
                        value = VectorParameter("MeshEmitterDynamicParameter", LinearColor.White);
                        break;
                    case "MaterialExpressionParticleMacroUV":
                    {
                        if (compilingVertexProperty)
                        { errors.Add("Invalid node used in vertex shader input!"); return null; }
                        bool viewSpace = properties.GetProp<BoolProperty>("bUseViewSpace")?.Value ?? false;
                        string local = $"Local{localIndex++}";
                        statements.Add($"\t{numericType}2 {local} = Get{(viewSpace ? "View" : "Screen")}SpaceMacroUVs(Parameters);\r\n");
                        value = new CodeValue(MaterialValueType.Vector2, local, true);
                        break;
                    }
                    case "MaterialExpressionFoliageImpulseDirection":
                        value = new CodeValue(MaterialValueType.Vector3, "FoliageImpulseDirection", true);
                        break;
                    case "MaterialExpressionFoliageNormalizedRotationAxisAndAngle":
                        value = new CodeValue(MaterialValueType.Vector4, "FoliageNormalizedRotationAxisAndAngle", true);
                        break;
                    case "MaterialExpressionWindDirectionAndSpeed":
                        value = new CodeValue(MaterialValueType.Vector4, "WindDirectionAndSpeed", true);
                        break;
                    case "MaterialExpressionSceneDepth":
                    {
                        var input = properties.GetProp<StructProperty>("Coordinates");
                        CodeValue coordinates = null;
                        if (input?.GetProp<ObjectProperty>("Expression")?.Value is not (null or 0))
                        {
                            coordinates = CompileInput(input, expression.InstancedFullPath + ".Coordinates", statements, cache);
                            if (coordinates == null) return null;
                        }
                        if (compilingVertexProperty)
                        {
                            errors.Add("Invalid node used in vertex shader input!");
                            return null;
                        }
                        // ab54710 sets resource+ac bit1 before coercion. bNormalize is unused.
                        usesSceneDepth = true;
                        string uv = "float2(ScreenAlignedPosition(Parameters.ScreenPosition).xy)";
                        if (coordinates != null)
                        {
                            // Full-precision GetParameterCode replaces half spelling even when
                            // the surrounding numeric chunks remain half precision.
                            uv = CoerceSceneCoordinates(coordinates);
                            if (uv == null) return null;
                        }
                        string local = $"Local{localIndex++}";
                        statements.Add($"\t{numericType} {local} = CalcSceneDepth({uv});\r\n");
                        value = new CodeValue(MaterialValueType.Scalar, local, true, ExplicitNativeNumericType: 1);
                        break;
                    }
                    case "MaterialExpressionSceneTexture":
                    {
                        var input = properties.GetProp<StructProperty>("Coordinates");
                        CodeValue coordinates = null;
                        if (input?.GetProp<ObjectProperty>("Expression")?.Value is not (null or 0))
                        {
                            coordinates = CompileInput(input, expression.InstancedFullPath + ".Coordinates", statements, cache);
                            if (coordinates == null) return null;
                        }
                        if (compilingVertexProperty)
                        {
                            errors.Add("Invalid node used in vertex shader input!");
                            return null;
                        }
                        var sceneType = properties.GetProp<Property>("SceneTextureType");
                        bool lighting = sceneType == null || sceneType is ByteProperty { Value: 0 } ||
                            sceneType is EnumProperty sceneEnum && sceneEnum.Value.Name == "SceneTex_Lighting" && sceneEnum.Value.Number == 0;
                        if (!lighting)
                        {
                            errors.Add("Scene texture type not supported.");
                            return null;
                        }
                        // ab549a0 sets OR1 before UV coercion. Its code-map scan considers all
                        // successfully compiled aligned ScreenPosition nodes in this property.
                        usesSceneColor = true;
                        string uv = "float2(ScreenAlignedPosition(Parameters.ScreenPosition).xy)";
                        if (coordinates != null)
                        {
                            uv = CoerceSceneCoordinates(coordinates);
                            if (uv == null) return null;
                            if (properties.GetProp<BoolProperty>("ScreenAlign")?.Value == true)
                                uv = $"ScreenAlignedUV({uv})";
                            else if (!propertyHasAlignedScreenPosition) uv += " * DynamicScale.xy";
                        }
                        string local = $"Local{localIndex++}";
                        statements.Add($"\t{DescribeType(MaterialValueType.Vector4)} {local} = tex2D(SceneColorTexture,{uv});\r\n");
                        value = new CodeValue(MaterialValueType.Vector4, local, true);
                        break;
                    }
                    case "MaterialExpressionPixelDepth":
                    case "MaterialExpressionDestDepth":
                    case "MaterialExpressionDestColor":
                    {
                        value = CompilePixelDestination(expression.ClassName["MaterialExpression".Length..], statements);
                        if (value == null) return null;
                        break;
                    }
                    case "MaterialExpressionDepthBiasedAlpha":
                    case "MaterialExpressionDepthBiasedBlend":
                    {
                        bool blend = expression.ClassName == "MaterialExpressionDepthBiasedBlend";
                        CodeValue OptionalInput(string name, Func<CodeValue> fallback)
                        {
                            var input = properties.GetProp<StructProperty>(name);
                            return input?.GetProp<ObjectProperty>("Expression")?.Value is not (null or 0)
                                ? CompileInput(input, expression.InstancedFullPath + "." + name, statements, cache)
                                : fallback();
                        }

                        // Compile callbacks ab15df0/ab15ed0 visit source, Alpha (Blend),
                        // Bias, then Constant(BiasScale). Normalize is never dispatched.
                        var rgb = blend ? OptionalInput("RGB", () => Vector(MaterialValueType.Vector3, 0f, 0f, 0f)) : null;
                        var alpha = OptionalInput("Alpha", () => Constant(1f));
                        var bias = OptionalInput("Bias", () => blend ? null : Constant(.5f));
                        var scale = Constant(properties.GetProp<FloatProperty>("BiasScale")?.Value ?? 1f);
                        if (blend && alpha != null && (alpha.NativeNumericType & 1) == 0)
                        {
                            errors.Add("Alpha input must be float1");
                            return null;
                        }
                        if (!blend && alpha != null && (alpha.NativeNumericType & 1) == 0)
                            alpha = ComponentMask(alpha, true, false, false, false, expression.InstancedFullPath + ".Alpha");
                        if (blend && rgb?.NativeNumericType == 8)
                            rgb = ComponentMask(rgb, true, true, true, false, expression.InstancedFullPath + ".RGB");
                        if (compilingVertexProperty)
                        {
                            errors.Add("Invalid node used in vertex shader input!");
                            return null;
                        }
                        // Unlike Alpha, Blend's missing Bias passes INDEX_NONE, not 0.5.
                        // Surface that withheld native result as an actionable managed error.
                        if (blend && bias == null && properties.GetProp<StructProperty>("Bias")?.GetProp<ObjectProperty>("Expression")?.Value is null or 0)
                            errors.Add(expression.InstancedFullPath + ".Bias is not connected.");
                        if (alpha == null || bias == null || blend && rgb == null) return null;

                        if (!blend) usesSceneDepth = true;
                        // Native code access is Scale, Bias, source, NOT formatting order.
                        // Coercion diagnostics do not stop the remaining accesses or Blend's
                        // post-emission usage setters, but no invalid HLSL is exposed here.
                        string scaleCode = CoerceNumericOperand(scale, MaterialValueType.Scalar);
                        string biasCode = CoerceNumericOperand(bias, MaterialValueType.Scalar);
                        var type = blend ? MaterialValueType.Vector3 : MaterialValueType.Scalar;
                        string sourceCode = CoerceNumericOperand(blend ? rgb : alpha, type);
                        if (blend) { usesSceneDepth = true; usesSceneColor = true; }
                        if (scaleCode == null || biasCode == null || sourceCode == null) return null;
                        string local = $"Local{localIndex++}";
                        statements.Add($"\t{DescribeType(type)} {local} = DepthBiased{(blend ? "Blend" : "Alpha")}(Parameters,{sourceCode},{biasCode},{scaleCode});\r\n");
                        value = new CodeValue(type, local, true, ExplicitNativeNumericType: blend ? 4 : 1);
                        if (blend) value = CompileAppend(value, alpha, expression.InstancedFullPath);
                        break;
                    }
                    case "MaterialExpressionTime":
                        value = CompileTime(properties.GetProp<BoolProperty>("bIgnorePause")?.Value == true);
                        break;
                    case "MaterialExpressionConstant":
                        value = Constant(properties.GetProp<FloatProperty>("R")?.Value ?? 0f);
                        break;
                    case "MaterialExpressionConstant2Vector":
                        value = Vector(MaterialValueType.Vector2,
                            properties.GetProp<FloatProperty>("R")?.Value ?? 0f,
                            properties.GetProp<FloatProperty>("G")?.Value ?? 0f);
                        break;
                    case "MaterialExpressionConstant3Vector":
                        value = Vector(MaterialValueType.Vector3,
                            properties.GetProp<FloatProperty>("R")?.Value ?? 0f,
                            properties.GetProp<FloatProperty>("G")?.Value ?? 0f,
                            properties.GetProp<FloatProperty>("B")?.Value ?? 0f);
                        break;
                    case "MaterialExpressionConstant4Vector":
                        value = Vector(MaterialValueType.Vector4,
                            properties.GetProp<FloatProperty>("R")?.Value ?? 0f,
                            properties.GetProp<FloatProperty>("G")?.Value ?? 0f,
                            properties.GetProp<FloatProperty>("B")?.Value ?? 0f,
                            properties.GetProp<FloatProperty>("A")?.Value ?? 0f);
                        break;
                    case "MaterialExpressionScalarParameter":
                    {
                        NameReference parameterName = properties.GetProp<NameProperty>("ParameterName")?.Value ?? "None";
                        float defaultValue = properties.GetProp<FloatProperty>("DefaultValue")?.Value ?? 0f;
                        value = ScalarParameter(parameterName, defaultValue);
                        break;
                    }
                    case "BioMaterialExpressionEffectMatFractionEnabled":
                        value = ScalarParameter("FractionOfEffectEnabled", 0f);
                        break;
                    case "BioMaterialExpressionHalfAngle":
                        value = BioHalfAngle(statements);
                        break;
                    case "BioMaterialExpressionEffectMatBaseProperty":
                    {
                        var authoredType = properties.GetProp<Property>("BaseProperty");
                        int propertyId = authoredType switch
                        {
                            null => 0,
                            ByteProperty raw => raw.Value,
                            EnumProperty named when named.Value.Number == 0 => named.Value.Name switch
                            {
                                "MP_EmissiveColor" => 0, "MP_Opacity" => 1, "MP_OpacityMask" => 2,
                                "MP_Distortion" => 3, "MP_TwoSidedLightingMask" => 4, "MP_DiffuseColor" => 5,
                                "MP_DiffusePower" => 6, "MP_SpecularColor" => 7, "MP_SpecularPower" => 8,
                                "MP_Normal" => 9, "MP_CustomLighting" => 10, "MP_CustomLightingDiffuse" => 11,
                                "MP_AnisotropicDirection" => 12, "MP_WorldPositionOffset" => 13,
                                "MP_TMissionMask" => 14, "MP_TMissionColor" => 15,
                                "MP_CustomSkylightDiffuse" => 16, "MP_WrapLightingCoefficients" => 17,
                                "MP_MAX" => 18, _ => -1
                            },
                            _ => -1
                        };
                        if (propertyId < 0)
                        { errors.Add($"{expression.InstancedFullPath}.BaseProperty is not a supported property identifier."); return null; }
                        var authoredInput = properties.GetProp<Property>("Input");
                        if (authoredInput != null && authoredInput is not ObjectProperty)
                        { errors.Add($"{expression.InstancedFullPath}.Input requires a direct expression object reference."); return null; }
                        var input = BioBasePropertyInput(properties);
                        if (input.GetProp<ObjectProperty>("Expression").Value == 0)
                        {
                            // Disconnected defaults use their own switch, not GetBasePropertyType.
                            value = propertyId switch
                            {
                                0 or 4 or 5 or 7 or 10 => Vector(MaterialValueType.Vector3, 0f, 0f, 0f),
                                1 or 2 => Constant(1f), 3 => Vector(MaterialValueType.Vector2, 0f, 0f),
                                8 => Constant(15f), 9 => Vector(MaterialValueType.Vector3, 0f, 0f, 1f),
                                15 => Vector(MaterialValueType.Vector3, 1f, 1f, 1f), _ => Constant(0f)
                            };
                            break;
                        }
                        var compiled = CompileInput(input, expression.InstancedFullPath + ".Input", statements, cache);
                        if (compiled == null) return null;
                        MaterialValueType? target = propertyId switch
                        {
                            0 or 4 or 5 or 7 or 9 or 10 or 15 => MaterialValueType.Vector3,
                            1 or 2 or 8 or 14 => MaterialValueType.Scalar,
                            3 => MaterialValueType.Vector2, _ => null
                        };
                        if (target == null)
                        {
                            // ForceCast accesses the original chunk before rejecting Unknown (0x40).
                            _ = compiled.Code;
                            errors.Add($"{expression.InstancedFullPath}.BaseProperty {propertyId} has an unsupported connected cast type.");
                            return null;
                        }
                        value = ForceCastAccessed(compiled, target.Value);
                        break;
                    }
                    case "BioMaterialExpressionWound":
                    {
                        if (compilingVertexProperty)
                        {
                            errors.Add("Invalid node used in vertex shader input!");
                            return null;
                        }
                        int wound = properties.GetProp<IntProperty>("m_nWoundNumber")?.Value ?? 0;
                        string local = $"Local{localIndex++}";
                        statements.Add($"\t{numericType}4 {local} = GetWoundParameters{wound.ToString(CultureInfo.InvariantCulture)}(Parameters);\r\n");
                        value = new CodeValue(MaterialValueType.Vector4, local, true);
                        break;
                    }
                    case "BioMaterialExpressionDirt":
                    {
                        var input = properties.GetProp<StructProperty>("Input");
                        if (input?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                        { errors.Add("Missing dirt input"); return null; }
                        int textureUIndex = properties.GetProp<ObjectProperty>("GrimeTex")?.Value ?? 0;
                        if (textureUIndex == 0) { errors.Add("Missing default textures"); return null; }
                        if (!material.FileRef.TryGetEntry(textureUIndex, out var texture) || texture == null || !texture.IsA("Texture2D"))
                        { errors.Add($"{expression.InstancedFullPath}.GrimeTex requires a Texture2D reference."); return null; }
                        if (ReadCompilationMetadata(texture, expression.InstancedFullPath) == null) return null;
                        NameReference textureName = properties.GetProp<NameProperty>("GrimeTexParamName")?.Value ?? "Tex__Grime";
                        // Registration precedes the input visit; sampler packing remains lazy.
                        MaterialUniformExpressionTexture uniform = null;
                        if (compilingVertexProperty) errors.Add("Invalid node used in vertex shader input!");
                        else uniform = CompileTextureUniform(textureUIndex, true, textureName);
                        var compiled = CompileInput(input, expression.InstancedFullPath + ".Input", statements, cache);
                        NameReference scaleName = properties.GetProp<NameProperty>("GrimeScaleParamName")?.Value ?? "Scalar__Grime";
                        var scale = ScalarParameter(scaleName, 0f);
                        var coordinates = CompileDefaultTextureCoordinate(statements);
                        var sample = CompileRawTextureSample(uniform, false, coordinates, statements);
                        if (compiled == null || sample == null) return null;
                        // This composition calls the raw sampler, without texture unpacking.
                        sample = ForceCastAccessed(sample, compiled.Type, compiled.NativeNumericType);
                        if (sample == null) return null;
                        var grime = CompileArithmetic(compiled, sample, "Multiply", expression.InstancedFullPath, statements);
                        var rgb = ComponentMask(compiled, true, true, true, false, expression.InstancedFullPath);
                        if (grime == null || rgb == null) return null;
                        value = CompileLerp(rgb, grime, scale, expression.InstancedFullPath, statements);
                        if (value == null) return null;
                        break;
                    }
                    case "BioMaterialExpressionKeyedFlipbook":
                    {
                        var timeInput = properties.GetProp<StructProperty>("Time");
                        if (timeInput?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                        { errors.Add("Missing KeyedFlipbook input Time"); return null; }
                        int textureUIndex = properties.GetProp<ObjectProperty>("FlipTex")?.Value ?? 0;
                        if (textureUIndex == 0) { errors.Add("Missing KeyedFlipbook Texture"); return null; }
                        if (!material.FileRef.TryGetEntry(textureUIndex, out var texture) || texture == null || !texture.IsA("Texture2D"))
                        { errors.Add($"{expression.InstancedFullPath}.FlipTex requires a Texture2D reference."); return null; }
                        if (ReadCompilationMetadata(texture, expression.InstancedFullPath) == null) return null;
                        var uniform = CompileTextureUniform(textureUIndex, false, NameReference.None);
                        int rows = properties.GetProp<IntProperty>("FlipTexRows")?.Value ?? 1;
                        int columns = properties.GetProp<IntProperty>("FlipTexCols")?.Value ?? 1;
                        var columnCount = Constant(columns);
                        var coordInput = properties.GetProp<StructProperty>("Coord");
                        var coord = coordInput?.GetProp<ObjectProperty>("Expression")?.Value is > 0
                            ? CompileInput(coordInput, expression.InstancedFullPath + ".Coord", statements, cache)
                            : CompileDefaultTextureCoordinate(statements);
                        if (coordInput?.GetProp<ObjectProperty>("Expression")?.Value is > 0 && coord != null)
                            coord = ForceCastAccessed(coord, MaterialValueType.Vector2);
                        var time = CompileInput(timeInput, expression.InstancedFullPath + ".Time", statements, cache);
                        if (time != null) time = ForceCastAccessed(time, MaterialValueType.Scalar);
                        if (coord == null || time == null) return null;
                        var reciprocal = Vector(MaterialValueType.Vector2, [1f / columns, 1f / rows]);
                        var indexProduct = CompileArithmetic(Constant(unchecked(rows * columns)), time, "Multiply", expression.InstancedFullPath, statements);
                        if (indexProduct == null) return null;
                        var index = CompileUnary(indexProduct, "Floor", statements);
                        // Native argument evaluation computes the Y quotient before BIOMod/X.
                        var quotient = CompileArithmetic(index, columnCount, "Divide", expression.InstancedFullPath, statements);
                        if (quotient == null) return null;
                        var y = CompileUnary(quotient, "Floor", statements);
                        var x = CompileBioMod(index, columnCount, statements);
                        if (x == null) return null;
                        var tile = CompileAppend(x, y, expression.InstancedFullPath);
                        if (tile == null) return null;
                        var offset = CompileArithmetic(reciprocal, tile, "Multiply", expression.InstancedFullPath, statements);
                        var scaledCoord = CompileArithmetic(coord, reciprocal, "Multiply", expression.InstancedFullPath, statements);
                        if (offset == null || scaledCoord == null) return null;
                        var uv = CompileArithmetic(scaledCoord, offset, "Add", expression.InstancedFullPath, statements);
                        if (uv == null) return null;
                        value = CompileRawTextureSample(uniform, false, uv, statements);
                        if (value == null) return null;
                        break;
                    }
                    case "BioMaterialExpressionAnisoSpec":
                    case "BioMaterialExpressionBlinnSpec":
                    case "BioMaterialExpressionPhongSpec":
                    {
                        bool aniso = expression.ClassName == "BioMaterialExpressionAnisoSpec";
                        bool phong = expression.ClassName == "BioMaterialExpressionPhongSpec";
                        string directionName = aniso ? "Direction" : "Normal";
                        CodeValue OptionalInput(string name, Func<CodeValue> fallback)
                        {
                            var input = properties.GetProp<StructProperty>(name);
                            return input?.GetProp<ObjectProperty>("Expression")?.Value is > 0
                                ? CompileInput(input, expression.InstancedFullPath + "." + name, statements, cache)
                                : fallback();
                        }
                        // All three input expressions are visited before the native error checks.
                        var direction = OptionalInput(directionName, () => Vector(MaterialValueType.Vector3, [0f, 0f, 1f]));
                        var color = OptionalInput("SpecColor", () => Constant(1f));
                        var power = OptionalInput("SpecPower", () => Constant(1f));
                        if (direction == null || color == null || power == null)
                        {
                            if (!phong) errors.Add(direction == null ? $"Missing input {directionName}"
                                : color == null ? "Missing input Spec Color" : "Missing input Spec Power");
                            return null;
                        }
                        string directionCode = CoerceNumericOperand(direction, MaterialValueType.Vector3);
                        string colorCode = CoerceNumericOperand(color, MaterialValueType.Vector3);
                        string powerCode = CoerceNumericOperand(power, MaterialValueType.Vector3);
                        string halfAngleCode = phong ? null : BioHalfAngle(statements).Code;
                        if (directionCode == null || colorCode == null || powerCode == null) return null;
                        string code = aniso
                            ? $"( {colorCode} * pow( max( 0.0, min( 1.0, sqrt( 1.0 - min(dot( {directionCode}, {halfAngleCode} ) * dot( {directionCode}, {halfAngleCode} ), 1.0) ) ) ), max( 0.0001, {powerCode} ) ) )"
                            : phong
                                ? $"{colorCode} * pow( max( 0.0, min( 1.0, dot( Parameters.TangentCameraVector, normalize( ( 2 * {directionCode} * dot( Parameters.TangentLightVector, {directionCode} ) - Parameters.TangentLightVector ) ) ) ) ), max( 0.0001, {powerCode} ) )"
                                : $"{colorCode} * pow( max( 0.0, min( 1.0, dot( {directionCode}, {halfAngleCode} ) ) ), max( 0.0001, {powerCode} ) )";
                        string local = $"Local{localIndex++}";
                        statements.Add($"\t{numericType}3 {local} = {code};\r\n");
                        value = new CodeValue(MaterialValueType.Vector3, local, true);
                        break;
                    }
                    case "BioMaterialExpressionFresnel":
                    {
                        var normalInput = properties.GetProp<StructProperty>("Normal");
                        var normal = normalInput?.GetProp<ObjectProperty>("Expression")?.Value is > 0
                            ? CompileInput(normalInput, expression.InstancedFullPath + ".Normal", statements, cache)
                            : Vector(MaterialValueType.Vector3, [0f, 0f, 1f]);
                        if (normal == null) { errors.Add("Missing input Normal"); return null; }
                        string normalCode = CoerceNumericOperand(normal, MaterialValueType.Vector3);
                        // CameraVector is requested only after the normal's coercion/access.
                        if (compilingVertexProperty)
                        {
                            errors.Add("Invalid node used in vertex shader input!");
                            return null;
                        }
                        if (normalCode == null) return null;
                        string code = $"max( 0.0, min( 1.0, dot( {normalCode}, Parameters.TangentCameraVector ) ) )";
                        if (properties.GetProp<BoolProperty>("Inverted")?.Value == true) code = "1.0 - " + code;
                        float power = properties.GetProp<FloatProperty>("Power")?.Value ?? 1f;
                        if ((double)MathF.Abs(power - 1f) > 0.0001d)
                        {
                            string powerLiteral = float.IsPositiveInfinity(power) ? "inf" : float.IsNegativeInfinity(power) ? "-inf"
                                : power.ToString("F6", CultureInfo.InvariantCulture);
                            code = $"pow( {code}, {powerLiteral} )";
                        }
                        string local = $"Local{localIndex++}";
                        statements.Add($"\t{numericType} {local} = {code};\r\n");
                        value = new CodeValue(MaterialValueType.Scalar, local, true);
                        break;
                    }
                    case "BioMaterialExpressionSpecPower":
                    {
                        var input = properties.GetProp<StructProperty>("Input");
                        if (input?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                        { errors.Add("Missing SpecPower Input"); return null; }
                        var compiled = CompileInput(input, expression.InstancedFullPath + ".Input", statements, cache);
                        if (compiled == null) return null;
                        value = CompileArithmetic(compiled, Constant(100), "Multiply", expression.InstancedFullPath, statements);
                        if (value == null) return null;
                        break;
                    }
                    case "BioMaterialExpressionSineSubtend":
                    {
                        var aInput = properties.GetProp<StructProperty>("A");
                        var bInput = properties.GetProp<StructProperty>("B");
                        foreach (var (name, input) in new[] { ("A", aInput), ("B", bInput) })
                        {
                            if (input?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                            { errors.Add($"Missing SineSubtend input {name}"); return null; }
                        }
                        var a = CompileInput(aInput, expression.InstancedFullPath + ".A", statements, cache);
                        var b = CompileInput(bInput, expression.InstancedFullPath + ".B", statements, cache);
                        if (a == null || b == null) return null;
                        // The native callback accesses A before coercing B to A's native type.
                        string left = a.Code;
                        string right = CoerceNumericOperand(b, a.Type, a.NativeNumericType);
                        if (right == null) return null;
                        string code = $"sqrt( max( 0.0, min( 1.0, 1 - dot({left},{right})*dot({left},{right}) ) ) )";
                        if (a.PerPixel || b.PerPixel)
                        {
                            string local = $"Local{localIndex++}";
                            statements.Add($"\t{numericType} {local} = {code};\r\n");
                            value = new CodeValue(MaterialValueType.Scalar, local, true);
                        }
                        else
                        {
                            var uniform = uniformExpressionPool.OfType<MaterialUniformExpressionBIOSineSubtend>()
                                .FirstOrDefault(p => ReferenceEquals(p.A, a.Uniform) && ReferenceEquals(p.B, b.Uniform));
                            if (uniform == null)
                            {
                                uniform = new MaterialUniformExpressionBIOSineSubtend
                                {
                                    ExpressionType = "FMaterialUniformExpressionBIOSineSubtend", A = a.Uniform, B = b.Uniform
                                };
                                uniformExpressionPool.Add(uniform);
                            }
                            value = UniformValue(MaterialValueType.Scalar, code, uniform, a.IsConstant && b.IsConstant);
                        }
                        break;
                    }
                    case "MaterialExpressionMeshEmitterVertexColor":
                    case "MaterialExpressionVectorParameter":
                    {
                        // Mesh emitter color is a fixed named uniform, NOT ordinary VertexColor.
                        bool meshEmitter = expression.ClassName == "MaterialExpressionMeshEmitterVertexColor";
                        NameReference parameterName = meshEmitter ? "MeshEmitterVertexColor" : properties.GetProp<NameProperty>("ParameterName")?.Value ?? "None";
                        var defaultValue = properties.GetProp<StructProperty>("DefaultValue");
                        float r = meshEmitter ? 1f : defaultValue?.GetProp<FloatProperty>("R")?.Value ?? 0f;
                        float g = meshEmitter ? 1f : defaultValue?.GetProp<FloatProperty>("G")?.Value ?? 0f;
                        float b = meshEmitter ? 1f : defaultValue?.GetProp<FloatProperty>("B")?.Value ?? 0f;
                        // The LE3 VectorParameter CDO initializes an omitted DefaultValue to
                        // (0,0,0,1). An explicitly authored color, including alpha zero, wins.
                        float a = meshEmitter || defaultValue == null ? 1f : defaultValue.GetProp<FloatProperty>("A")?.Value ?? 0f;
                        var uniform = uniformExpressionPool.OfType<MaterialUniformExpressionVectorParameter>()
                            .FirstOrDefault(candidate => candidate.ParameterName == parameterName &&
                                                         LE3UniformExpressionComparer.ColorIdentical(candidate.DefaultValue, new(r, g, b, a)));
                        if (uniform == null)
                        {
                            uniform = new MaterialUniformExpressionVectorParameter
                            {
                                ExpressionType = "FMaterialUniformExpressionVectorParameter",
                                ParameterName = parameterName,
                                DefaultValue = new LinearColor(r, g, b, a)
                            };
                            uniformExpressionPool.Add(uniform);
                        }

                        value = UniformValue(MaterialValueType.Vector4, null, uniform, isConstant: false);
                        break;
                    }
                    case "MaterialExpressionAntialiasedTextureMask":
                    {
                        int textureUIndex = properties.GetProp<ObjectProperty>("Texture")?.Value ?? 0;
                        if (textureUIndex == 0 || !material.FileRef.TryGetEntry(textureUIndex, out var texture) || texture == null)
                        {
                            errors.Add("UMaterialExpressionAntialiasedTextureMask> Missing input texture");
                            return null;
                        }
                        // Native ab150f0 visits UV BEFORE validating/registering the texture.
                        var coordinatesInput = properties.GetProp<StructProperty>("Coordinates");
                        var coordinates = coordinatesInput?.GetProp<ObjectProperty>("Expression")?.Value is > 0
                            ? CompileInput(coordinatesInput, expression.InstancedFullPath + ".Coordinates", statements, cache)
                            : CompileDefaultTextureCoordinate(statements);
                        if (coordinates == null) return null;
                        NameReference parameterName = properties.GetProp<NameProperty>("ParameterName")?.Value ?? "None";
                        // Native inherited validator accepts exact Texture2D plus render targets.
                        // Render-target metadata/compilation is not implemented by this contract.
                        if (texture.ClassName != "Texture2D")
                        {
                            errors.Add($"AAMasked Param2D '{parameterName.Instanced}'> Requires Texture2D");
                            return null;
                        }
                        if (ReadCompilationMetadata(texture, expression.InstancedFullPath) == null) return null;
                        var channelProperty = properties.GetProp<Property>("Channel");
                        int channel = channelProperty switch
                        {
                            null => 3,
                            ByteProperty raw => raw.Value,
                            EnumProperty named when named.Value.Number == 0 => named.Value.Name switch
                            { "TCC_Red" => 0, "TCC_Green" => 1, "TCC_Blue" => 2, "TCC_Alpha" => 3, _ => -1 },
                            _ => -1
                        };
                        if (channel < 0)
                        {
                            errors.Add($"{expression.InstancedFullPath}.Channel must be a texture color-channel enum or byte.");
                            return null;
                        }
                        bool isParameter = !string.Equals(parameterName.Name, "None", StringComparison.OrdinalIgnoreCase);
                        var uniform = CompileTextureUniform(textureUIndex, isParameter, parameterName);
                        var threshold = Constant(properties.GetProp<FloatProperty>("Threshold")?.Value ?? .5f);
                        var channelConstant = Constant(channel);
                        // +290 coerces the texture to its own type, not UV; there is no
                        // TextureSample/UnpackTexture callback or explicit vertex gate here.
                        RegisterTextureUniform(uniform);
                        int textureIndex = FindOrAddTextureReference(uniform2DTextures, uniform);
                        string textureName = $"Texture2D_{textureIndex}";
                        string channelCode = channelConstant.Code, thresholdCode = threshold.Code, coordinateCode = coordinates.Code;
                        string local = $"Local{localIndex++}";
                        statements.Add($"\t{numericType} {local} = AntialiasedTextureMask({textureName},{textureName}Sampler,{coordinateCode},{thresholdCode},{channelCode});\r\n");
                        value = new CodeValue(MaterialValueType.Scalar, local, true);
                        break;
                    }
                    case "MaterialExpressionComment":
                    case "MaterialExpressionCompound":
                    case "MaterialExpressionParameter":
                        errors.Add($"{expression.InstancedFullPath}: {expression.ClassName} has no runtime compile implementation (native INDEX_NONE).");
                        return null;
                    case "MaterialExpressionFlipBookSample":
                    {
                        int textureUIndex = properties.GetProp<ObjectProperty>("Texture")?.Value ?? 0;
                        material.FileRef.TryGetEntry(textureUIndex, out var texture);
                        if (texture == null)
                        { errors.Add("FlipBookSample> Missing input texture"); return null; }
                        if (!texture.IsA("TextureFlipBook"))
                        { errors.Add("FlipBookSample> Texture is not a FlipBook"); return null; }
                        var metadata = ReadCompilationMetadata(texture, expression.InstancedFullPath);
                        if (metadata == null) return null;
                        if (metadata.FlipBookScale is not Vector2 flipBookScale)
                        { errors.Add($"{expression.InstancedFullPath} requires effective FlipBookScale in texture compilation metadata."); return null; }
                        if (!float.IsFinite(flipBookScale.X) || !float.IsFinite(flipBookScale.Y))
                        { errors.Add($"{expression.InstancedFullPath} has nonfinite FlipBookScale, which is not supported."); return null; }
                        string normalHelper = TextureNormalHelper(metadata.CompressionSettings);
                        if (!IsSupportedUnpackCompression(metadata.CompressionSettings, normalHelper))
                        { errors.Add($"{expression.InstancedFullPath} uses unsupported CompressionSettings={metadata.CompressionSettings}."); return null; }
                        MaterialUniformExpressionTexture uniform = null;
                        if (compilingVertexProperty) errors.Add("Invalid node used in vertex shader input!");
                        else uniform = CompileTextureUniform(textureUIndex, false, NameReference.None);
                        var scale = Vector(MaterialValueType.Vector2, flipBookScale.X, flipBookScale.Y);
                        var authoredCoordinates = properties.GetProp<Property>("Coordinates");
                        var input = authoredCoordinates as StructProperty;
                        CodeValue coordinates;
                        if (authoredCoordinates != null && input == null)
                        { errors.Add($"{expression.InstancedFullPath}.Coordinates requires an expression input."); coordinates = null; }
                        else coordinates = input?.GetProp<ObjectProperty>("Expression")?.Value is not (null or 0)
                            ? CompileInput(input, expression.InstancedFullPath + ".Coordinates", statements, cache)
                            : CompileDefaultTextureCoordinate(statements);
                        var scaled = coordinates == null ? null : CompileArithmetic(scale, coordinates, "Multiply", expression.InstancedFullPath, statements);
                        var offset = CompileFlipBookOffset(textureUIndex);
                        var masked = offset == null ? null : ComponentMask(offset, true, true, false, false, expression.InstancedFullPath);
                        var uv = scaled == null || masked == null ? null : CompileArithmetic(scaled, masked, "Add", expression.InstancedFullPath, statements);
                        var sample = CompileRawTextureSample(uniform, false, uv, statements);
                        if (sample == null) return null;
                        value = CompileTextureUnpack(sample, normalHelper,
                            [metadata.UnpackMin.X, metadata.UnpackMin.Y, metadata.UnpackMin.Z, metadata.UnpackMin.W],
                            [metadata.UnpackMax.X, metadata.UnpackMax.Y, metadata.UnpackMax.Z, metadata.UnpackMax.W], statements);
                        break;
                    }
                    case "MaterialExpressionFontSample":
                    case "MaterialExpressionFontSampleParameter":
                    {
                        int fontUIndex = (properties.GetProp<Property>("Font") as ObjectProperty)?.Value ?? 0;
                        material.FileRef.TryGetEntry(fontUIndex, out var font);
                        if (font == null)
                        { errors.Add($"{expression.InstancedFullPath}: Missing input Font"); return null; }
                        if (!font.IsA("Font"))
                        { errors.Add($"{expression.InstancedFullPath}.Font requires a Font reference."); return null; }
                        var pages = ReadFontTexturePages(font, expression.InstancedFullPath);
                        if (pages == null) return null;
                        var pageProperty = properties.GetProp<Property>("FontTexturePage");
                        if (pageProperty != null && pageProperty is not IntProperty)
                        { errors.Add($"{expression.InstancedFullPath}.FontTexturePage must be an integer."); return null; }
                        int page = (pageProperty as IntProperty)?.Value ?? 0;
                        if (page < 0 || page >= pages.Length)
                        { errors.Add($"{expression.InstancedFullPath}: Invalid font page {page}. Max allowed is {pages.Length}"); return null; }
                        if (invalidFontPageReferences.TryGetValue(fontUIndex, out var invalidPages) && invalidPages.Contains(page))
                        { errors.Add($"{expression.InstancedFullPath} font {font.InstancedFullPath}.Textures[{page}] has an invalid reference."); return null; }
                        var texture = pages[page] ?? CompilationInput.WorldDefaultTexture;
                        if (texture == null)
                        { errors.Add($"{expression.InstancedFullPath} requires a caller-resolved WorldDefaultTexture for its null font page."); return null; }
                        if (!ReferenceEquals(texture.FileRef, material.FileRef) ||
                            !material.FileRef.TryGetEntry(texture.UIndex, out var registeredPage) || !ReferenceEquals(texture, registeredPage) ||
                            (!texture.IsA("Texture2D") && !texture.IsA("TextureRenderTarget2D")))
                        { errors.Add($"{expression.InstancedFullPath} requires a registered 2D font-page texture in the base material package."); return null; }
                        TextureCompilationMetadata metadata = null;
                        if (!compilingVertexProperty)
                        {
                            metadata = ReadCompilationMetadata(texture, expression.InstancedFullPath);
                            if (metadata == null) return null;
                        }
                        NameReference parameterName = properties.GetProp<NameProperty>("ParameterName")?.Value ?? NameReference.None;
                        bool parameterized = expression.ClassName == "MaterialExpressionFontSampleParameter" &&
                            !string.Equals(parameterName.Name, "None", StringComparison.OrdinalIgnoreCase);
                        MaterialUniformExpressionTexture uniform = null;
                        if (compilingVertexProperty) errors.Add("Invalid node used in vertex shader input!");
                        else uniform = CompileTextureUniform(texture.UIndex, parameterized, parameterName);
                        var coordinates = CompileDefaultTextureCoordinate(statements);
                        var sample = CompileRawTextureSample(uniform, false, coordinates, statements);
                        if (sample == null) return null;
                        // Font pages use an unconditional affine tail, independent of compression.
                        float[] min = [metadata.UnpackMin.X, metadata.UnpackMin.Y, metadata.UnpackMin.Z, metadata.UnpackMin.W];
                        float[] max = [metadata.UnpackMax.X, metadata.UnpackMax.Y, metadata.UnpackMax.Z, metadata.UnpackMax.W];
                        var scale = Vector(MaterialValueType.Vector4, max.Zip(min, (upper, lower) => upper - lower).ToArray());
                        var offset = Vector(MaterialValueType.Vector4, min);
                        var scaled = CompileArithmetic(sample, scale, "Multiply", expression.InstancedFullPath, statements);
                        if (scaled == null) return null;
                        value = CompileArithmetic(scaled, offset, "Add", expression.InstancedFullPath, statements);
                        if (value == null) return null;
                        break;
                    }
                    case "MaterialExpressionFluidNormal":
                    {
                        var coordinates = CompileDefaultTextureCoordinate(statements);
                        var offset = ForceCastAccessed(VectorParameter("DetailCoordOffset", LinearColor.Black), MaterialValueType.Vector2);
                        var scale = ForceCastAccessed(VectorParameter("DetailCoordScale", LinearColor.Black), MaterialValueType.Vector2);
                        var shifted = coordinates == null ? null : CompileArithmetic(coordinates, offset, "Subtract", expression.InstancedFullPath, statements);
                        var uv = shifted == null ? null : CompileArithmetic(shifted, scale, "Multiply", expression.InstancedFullPath, statements);
                        var texture = CompilationInput.EngineDefaultTexture;
                        if (texture == null)
                        { errors.Add($"{expression.InstancedFullPath} requires a caller-resolved EngineDefaultTexture reference."); return null; }
                        if (!compilingVertexProperty && ReadCompilationMetadata(texture, expression.InstancedFullPath) == null) return null;
                        MaterialUniformExpressionTexture uniform = null;
                        if (compilingVertexProperty) errors.Add("Invalid node used in vertex shader input!");
                        else uniform = CompileTextureUniform(texture.UIndex, true, "FluidDetailNormal");
                        // The detail normal composition samples raw XY; no texture unpack callback.
                        var sample = CompileRawTextureSample(uniform, false, uv, statements);
                        if (sample == null) return null;
                        var xy = ForceCastAccessed(sample, MaterialValueType.Vector2);
                        var squared = CompileDot(xy, xy, expression.InstancedFullPath, statements);
                        if (squared == null) return null;
                        var remainder = CompileArithmetic(Constant(1f), squared, "Subtract", expression.InstancedFullPath, statements);
                        if (remainder == null) return null;
                        var z = CompileUnary(remainder, "SquareRoot", statements);
                        var appended = CompileAppend(xy, z, expression.InstancedFullPath);
                        if (appended == null) return null;
                        value = ForceCastAccessed(appended, MaterialValueType.Vector3);
                        break;
                    }
                    case "MaterialExpressionMeshSubUV":
                    case "MaterialExpressionMeshSubUVBlend":
                    case "MaterialExpressionTextureSampleParameterMeshSubUV":
                    case "MaterialExpressionTextureSampleParameterMeshSubUVBlend":
                    {
                        bool parameterized = expression.ClassName.StartsWith("MaterialExpressionTextureSampleParameter", StringComparison.Ordinal);
                        bool blend = expression.ClassName.EndsWith("Blend", StringComparison.Ordinal);
                        int textureUIndex = properties.GetProp<ObjectProperty>("Texture")?.Value ?? 0;
                        material.FileRef.TryGetEntry(textureUIndex, out var texture);
                        if (texture == null || (parameterized
                                ? texture.ClassName != "Texture2D" && !texture.IsA("TextureRenderTarget2D")
                                : !texture.IsA("Texture2D") && !texture.IsA("TextureRenderTarget2D")))
                        {
                            errors.Add(parameterized ? $"Parameter MeshSubUV{(blend ? "Blend" : "")}> Requires a valid Texture2D"
                                : $"Mesh SubUV{(blend ? " Blend" : "")} missing texture");
                            return null;
                        }
                        var metadata = ReadCompilationMetadata(texture, expression.InstancedFullPath);
                        if (metadata == null) return null;
                        string normalHelper = TextureNormalHelper(metadata.CompressionSettings);
                        if (!IsSupportedUnpackCompression(metadata.CompressionSettings, normalHelper))
                        { errors.Add($"{expression.InstancedFullPath} uses unsupported CompressionSettings={metadata.CompressionSettings}."); return null; }
                        NameReference parameterName = properties.GetProp<NameProperty>("ParameterName")?.Value ?? NameReference.None;
                        MaterialUniformExpressionTexture uniform = null;
                        if (compilingVertexProperty) errors.Add("Invalid node used in vertex shader input!");
                        else uniform = CompileTextureUniform(textureUIndex, parameterized, parameterName);
                        float[] unpackMin = [metadata.UnpackMin.X, metadata.UnpackMin.Y, metadata.UnpackMin.Z, metadata.UnpackMin.W];
                        float[] unpackMax = [metadata.UnpackMax.X, metadata.UnpackMax.Y, metadata.UnpackMax.Z, metadata.UnpackMax.W];
                        CodeValue unpackScale = null, unpackOffset = null;
                        if (blend)
                        {
                            unpackScale = Vector(MaterialValueType.Vector4, unpackMax.Zip(unpackMin, (max, min) => max - min).ToArray());
                            unpackOffset = Vector(MaterialValueType.Vector4, unpackMin);
                        }
                        CodeValue MaskedParameter(string name, bool blue = false) => ComponentMask(
                            VectorParameter(name, name == "TextureScaleParameter" ? LinearColor.White : LinearColor.Black),
                            !blue, !blue, blue, false, expression.InstancedFullPath);
                        CodeValue offset, scale;
                        // Single-frame packs offset first; blended compiles scale first.
                        if (blend) { scale = MaskedParameter("TextureScaleParameter"); offset = MaskedParameter("TextureOffsetParameter"); }
                        else { offset = MaskedParameter("TextureOffsetParameter"); scale = MaskedParameter("TextureScaleParameter"); }
                        CodeValue Frame(CodeValue frameOffset)
                        {
                            var authoredCoordinates = properties.GetProp<Property>("Coordinates");
                            var input = authoredCoordinates as StructProperty;
                            CodeValue coordinates;
                            if (authoredCoordinates != null && input == null)
                            { errors.Add($"{expression.InstancedFullPath}.Coordinates requires an expression input."); coordinates = null; }
                            else coordinates = input?.GetProp<ObjectProperty>("Expression")?.Value is not (null or 0)
                                ? CompileInput(input, expression.InstancedFullPath + ".Coordinates", statements, cache)
                                : CompileDefaultTextureCoordinate(statements);
                            var scaled = coordinates == null || scale == null ? null
                                : CompileArithmetic(coordinates, scale, "Multiply", expression.InstancedFullPath, statements);
                            var uv = scaled == null || frameOffset == null ? null
                                : CompileArithmetic(scaled, frameOffset, "Add", expression.InstancedFullPath, statements);
                            return CompileRawTextureSample(uniform, false, uv, statements);
                        }
                        var a = Frame(offset);
                        if (!blend)
                        {
                            value = CompileTextureUnpack(a, normalHelper, unpackMin, unpackMax, statements);
                            if (value == null) return null;
                            break;
                        }
                        var b = Frame(MaskedParameter("TextureOffset1Parameter"));
                        CodeValue UnpackFrame(CodeValue sample)
                        {
                            if (sample == null) return null;
                            // Mesh blending calls the normal helper directly: no affine recovery.
                            if (normalHelper != null)
                                return CompileTextureUnpack(sample, normalHelper, [-1f, -1f, -1f, 0f], [1f, 1f, 1f, 1f], statements);
                            var product = CompileArithmetic(sample, unpackScale, "Multiply", expression.InstancedFullPath, statements);
                            return product == null ? null : CompileArithmetic(product, unpackOffset, "Add", expression.InstancedFullPath, statements);
                        }
                        a = UnpackFrame(a);
                        b = UnpackFrame(b);
                        var alpha = MaskedParameter("TextureOffsetParameter", true);
                        if (a == null || b == null || alpha == null) return null;
                        value = CompileLerp(a, b, alpha, expression.InstancedFullPath, statements);
                        if (value == null) return null;
                        break;
                    }
                    case "MaterialExpressionParticleSubUV":
                    case "MaterialExpressionTextureSampleParameterSubUV":
                    {
                        bool parameterized = expression.ClassName == "MaterialExpressionTextureSampleParameterSubUV";
                        int textureUIndex = properties.GetProp<ObjectProperty>("Texture")?.Value ?? 0;
                        material.FileRef.TryGetEntry(textureUIndex, out var texture);
                        if (texture == null || (parameterized
                                ? texture.ClassName != "Texture2D" && !texture.IsA("TextureRenderTarget2D")
                                : !texture.IsA("Texture2D") && !texture.IsA("TextureRenderTarget2D")))
                        {
                            errors.Add(parameterized ? "ParameterSubUV> Requires a valid Texture2D" : "Missing ParticleSubUV input texture");
                            return null;
                        }
                        var metadata = ReadCompilationMetadata(texture, expression.InstancedFullPath);
                        if (metadata == null) return null;
                        string normalHelper = TextureNormalHelper(metadata.CompressionSettings);
                        if (!IsSupportedUnpackCompression(metadata.CompressionSettings, normalHelper))
                        { errors.Add($"{expression.InstancedFullPath} uses unsupported CompressionSettings={metadata.CompressionSettings}."); return null; }
                        NameReference parameterName = properties.GetProp<NameProperty>("ParameterName")?.Value ?? NameReference.None;
                        MaterialUniformExpressionTexture uniform = null;
                        if (compilingVertexProperty)
                        {
                            // Texture and TextureParameter reject frequency before registration.
                            errors.Add("Invalid node used in vertex shader input!");
                            if (!parameterized) errors.Add("Invalid node used in vertex shader input!");
                        }
                        else
                        {
                            uniform = CompileTextureUniform(textureUIndex, parameterized, parameterName);
                            if (!parameterized) CompileTextureUniform(textureUIndex, false, NameReference.None);
                        }
                        var sampleA = CompileRawTextureSample(uniform, false, CompileDefaultTextureCoordinate(statements), statements);
                        // LE3 parameterized SubUV deliberately calls TextureCoordinate(0) twice.
                        var sampleB = CompileRawTextureSample(uniform, false,
                            CompileDefaultTextureCoordinate(statements, parameterized ? 0u : 1u), statements);
                        float[] unpackMin = [metadata.UnpackMin.X, metadata.UnpackMin.Y, metadata.UnpackMin.Z, metadata.UnpackMin.W];
                        float[] unpackMax = [metadata.UnpackMax.X, metadata.UnpackMax.Y, metadata.UnpackMax.Z, metadata.UnpackMax.W];
                        var a = CompileTextureUnpack(sampleA, normalHelper, unpackMin, unpackMax, statements);
                        var b = CompileTextureUnpack(sampleB, normalHelper, unpackMin, unpackMax, statements);
                        var coordinates = CompileDefaultTextureCoordinate(statements, 2u);
                        var alpha = coordinates == null ? null : ComponentMask(coordinates, true, false, false, false, expression.InstancedFullPath);
                        if (a == null || b == null || alpha == null) return null;
                        value = CompileLerp(a, b, alpha, expression.InstancedFullPath, statements);
                        if (value == null) return null;
                        break;
                    }
                    case "MaterialExpressionTextureSampleParameterMovie":
                    {
                        int textureUIndex = properties.GetProp<ObjectProperty>("Texture")?.Value ?? 0;
                        material.FileRef.TryGetEntry(textureUIndex, out var texture);
                        if (texture?.ClassName != "TextureMovie")
                        { errors.Add($"{expression.InstancedFullPath}> Requires TextureMovie"); return null; }
                        TextureCompilationMetadata metadata = null;
                        if (!compilingVertexProperty)
                        {
                            metadata = ReadCompilationMetadata(texture, expression.InstancedFullPath);
                            if (metadata == null) return null;
                        }
                        string normalHelper = metadata == null ? null : TextureNormalHelper(metadata.CompressionSettings);
                        if (metadata != null && !IsSupportedUnpackCompression(metadata.CompressionSettings, normalHelper))
                        { errors.Add($"{expression.InstancedFullPath} uses unsupported CompressionSettings={metadata.CompressionSettings}."); return null; }
                        NameReference parameterName = properties.GetProp<NameProperty>("ParameterName")?.Value ?? NameReference.None;
                        MaterialUniformExpressionTexture uniform = null;
                        if (compilingVertexProperty) errors.Add("Invalid node used in vertex shader input!");
                        else uniform = CompileTextureUniform(textureUIndex,
                            !string.Equals(parameterName.Name, "None", StringComparison.OrdinalIgnoreCase), parameterName);
                        var input = properties.GetProp<StructProperty>("Coordinates");
                        var coordinates = input?.GetProp<ObjectProperty>("Expression")?.Value is not (null or 0)
                            ? CompileInput(input, expression.InstancedFullPath + ".Coordinates", statements, cache)
                            : CompileDefaultTextureCoordinate(statements);
                        var sample = CompileRawTextureSample(uniform, false, coordinates, statements);
                        if (sample == null) return null;
                        value = CompileTextureUnpack(sample, normalHelper,
                            [metadata.UnpackMin.X, metadata.UnpackMin.Y, metadata.UnpackMin.Z, metadata.UnpackMin.W],
                            [metadata.UnpackMax.X, metadata.UnpackMax.Y, metadata.UnpackMax.Z, metadata.UnpackMax.W], statements);
                        break;
                    }
                    case "MaterialExpressionTextureSample":
                    case "MaterialExpressionTextureSampleParameter":
                    case "MaterialExpressionTextureSampleParameter2D":
                    case "MaterialExpressionTextureSampleParameterCube":
                    case "MaterialExpressionTextureSampleParameterNormal":
                    case "MaterialExpressionDepthBiasBlend":
                    {
                        bool depthBiasBlend = expression.ClassName == "MaterialExpressionDepthBiasBlend";
                        if (compilingVertexProperty && !depthBiasBlend)
                        {
                            errors.Add("Invalid node used in vertex shader input!");
                            return null;
                        }

                        int textureUIndex = properties.GetProp<ObjectProperty>("Texture")?.Value ?? 0;
                        if (textureUIndex == 0 || !material.FileRef.TryGetEntry(textureUIndex, out var texture) ||
                            texture == null || (!texture.IsA("Texture2D") && !texture.IsA("TextureCube")))
                        {
                            errors.Add("TextureSample> Missing input texture");
                            return null;
                        }

                        var metadata = ReadCompilationMetadata(texture, expression.InstancedFullPath);
                        if (metadata == null) return null;
                        float[] unpackMin = [metadata.UnpackMin.X, metadata.UnpackMin.Y, metadata.UnpackMin.Z, metadata.UnpackMin.W];
                        float[] unpackMax = [metadata.UnpackMax.X, metadata.UnpackMax.Y, metadata.UnpackMax.Z, metadata.UnpackMax.W];

                        bool isCube = texture.IsA("TextureCube");
                        bool isParameter = expression.ClassName != "MaterialExpressionTextureSample" && !depthBiasBlend;
                        NameReference parameterName = properties.GetProp<NameProperty>("ParameterName")?.Value ?? "None";
                        // Generic parameter Compile checks the NAME_None index, not its number.
                        isParameter &= !string.Equals(parameterName.Name, "None", StringComparison.OrdinalIgnoreCase);

                        string compression = metadata.CompressionSettings;
                        if (isParameter && expression.ClassName == "MaterialExpressionTextureSampleParameterNormal")
                        {
                            // LE3 binds the first FName match, irrespective of bOverride/GUID.
                            // Compile consumes its compression byte directly; NAME_None instead
                            // takes the ordinary TextureSample path and texture metadata.
                            var normalOverride = staticParameters?.NormalParameters?
                                .FirstOrDefault(candidate => candidate.ParameterName == parameterName);
                            if (normalOverride != null)
                            {
                                // Full installed Engine.pcc Texture.TextureCompressionSettings order; any value
                                // can be stored, e.g. a Grayscale specular bound to a normal parameter.
                                compression = normalOverride.CompressionSettings < TextureCompressionSettingsNames.Length
                                    ? TextureCompressionSettingsNames[normalOverride.CompressionSettings]
                                    : $"TC_#{normalOverride.CompressionSettings}";
                            }
                        }

                        string normalHelper = TextureNormalHelper(compression);
                        // Native UnpackTexture ab30070 normal mask0x118a excludes HQ=10:
                        // it samples normally and always applies the affine tail, even for [-1,1].
                        if (!IsSupportedUnpackCompression(compression, normalHelper))
                        {
                            errors.Add($"{expression.InstancedFullPath} uses CompressionSettings={compression}, which is not supported by the current texture path.");
                            return null;
                        }

                        var uniform = CompileTextureUniform(textureUIndex, isParameter, parameterName);

                        var coordinatesInput = properties.GetProp<StructProperty>("Coordinates");
                        CodeValue coordinates;
                        if (coordinatesInput?.GetProp<ObjectProperty>("Expression")?.Value is > 0)
                        {
                            coordinates = CompileInput(coordinatesInput, expression.InstancedFullPath + ".Coordinates",
                                statements, cache);
                            if (coordinates == null) return null;
                        }
                        else
                        {
                            coordinates = CompileDefaultTextureCoordinate(statements);
                            if (coordinates == null) return null;
                        }

                        if (depthBiasBlend && compilingVertexProperty)
                        {
                            errors.Add("Invalid node used in vertex shader input!");
                            return null;
                        }

                        // Native TextureSample coerces coordinates before accessing the texture.
                        // Global texture identity and sampler ordinals are both lazy: nested
                        // coordinate samples may acquire both before the outer texture.
                        var sample = CompileRawTextureSample(uniform, isCube, coordinates, statements);
                        if (sample == null) return null;
                        value = CompileTextureUnpack(sample, normalHelper, unpackMin, unpackMax, statements);
                        if (depthBiasBlend)
                        {
                            value = CompileDepthBiasBlend(expression, properties, value, statements, cache);
                            if (value == null) return null;
                        }
                        break;
                    }
                    case "MaterialExpressionAdd":
                    case "MaterialExpressionMultiply":
                    case "MaterialExpressionSubtract":
                    case "MaterialExpressionDivide":
                    {
                        var aInput = properties.GetProp<StructProperty>("A");
                        var bInput = properties.GetProp<StructProperty>("B");
                        string operation = expression.ClassName["MaterialExpression".Length..];
                        if (aInput?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                        {
                            errors.Add($"Missing {operation} input A");
                            return null;
                        }
                        if (bInput?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                        {
                            errors.Add($"Missing {operation} input B");
                            return null;
                        }

                        var a = CompileInput(aInput, expression.InstancedFullPath + ".A", statements, cache);
                        var b = CompileInput(bInput, expression.InstancedFullPath + ".B", statements, cache);
                        if (a == null || b == null) return null;
                        value = CompileArithmetic(a, b, operation, expression.InstancedFullPath, statements);
                        if (value == null) return null;
                        break;
                    }
                    case "MaterialExpressionSine":
                    case "MaterialExpressionCosine":
                    {
                        bool cosine = expression.ClassName == "MaterialExpressionCosine";
                        string operation = cosine ? "Cosine" : "Sine";
                        var input = properties.GetProp<StructProperty>("Input");
                        if (input?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                        {
                            errors.Add($"Missing {operation} input");
                            return null;
                        }
                        float period = properties.GetProp<FloatProperty>("Period")?.Value ?? 1f;
                        // COMISS/JBE treats unordered Period as nonpositive. Cosine's branch
                        // passes CODE INDEX 0, not Constant(0); without a native chunk-index
                        // model, reproducing that accidental earlier-chunk dependency is unsafe.
                        if (cosine && !(period > 0f))
                        {
                            errors.Add("Cosine with nonpositive or NaN Period uses native code-chunk index 0; that path is not yet supported.");
                            return null;
                        }
                        // LE3 creates the scale before visiting Input; 0x40c90fdb is the
                        // shipping binary's float32 2*pi. Reuse the actual Mul callback path.
                        var scale = period > 0f ? Constant(6.2831855f / period) : null;
                        var x = CompileInput(input, expression.InstancedFullPath + ".Input", statements, cache);
                        if (x == null) return null;
                        if (scale != null) x = CompileArithmetic(x, scale, "Multiply", expression.InstancedFullPath, statements);
                        if (x == null) return null;
                        value = CompileTrig(x, cosine, statements);
                        break;
                    }
                    case "MaterialExpressionOneMinus":
                    {
                        var input = properties.GetProp<StructProperty>("Input");
                        if (input?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                        {
                            errors.Add("Missing 1-x input");
                            return null;
                        }
                        // LE3 0x7ff74ab17920 visits Input before Constant(1), then Sub(1,X).
                        // Do not replace the scalar constant with a replicated vector on the
                        // uniform CPU path: shipping scalar constants retain only R.
                        var x = CompileInput(input, expression.InstancedFullPath + ".Input", statements, cache);
                        if (x == null) return null;
                        value = CompileArithmetic(Constant(1f), x, "Subtract", expression.InstancedFullPath, statements);
                        if (value == null) return null;
                        break;
                    }
                    case "MaterialExpressionPanner":
                    {
                        var timeInput = properties.GetProp<StructProperty>("Time");
                        CodeValue CompileOffset(string speedName)
                        {
                            // Native ab179a0 creates each speed before visiting Time, including zero.
                            var speed = Constant(properties.GetProp<FloatProperty>(speedName)?.Value ?? 0f);
                            var time = timeInput?.GetProp<ObjectProperty>("Expression")?.Value is > 0
                                ? CompileInput(timeInput, expression.InstancedFullPath + ".Time", statements, cache)
                                : CompileTime(false);
                            if (time == null) return null;
                            var product = CompileArithmetic(time, speed, "Multiply", expression.InstancedFullPath, statements);
                            return product == null ? null : CompilePeriodicHint(product);
                        }
                        var x = CompileOffset("SpeedX");
                        if (x == null) return null;
                        var y = CompileOffset("SpeedY");
                        if (y == null) return null;
                        var coordinateInput = properties.GetProp<StructProperty>("Coordinate");
                        var coordinate = coordinateInput?.GetProp<ObjectProperty>("Expression")?.Value is > 0
                            ? CompileInput(coordinateInput, expression.InstancedFullPath + ".Coordinate", statements, cache)
                            : CompileDefaultTextureCoordinate(statements);
                        if (coordinate == null) return null;
                        var offset = CompileAppend(x, y, expression.InstancedFullPath);
                        if (offset == null) return null;
                        value = CompileArithmetic(offset, coordinate, "Add", expression.InstancedFullPath, statements);
                        if (value == null) return null;
                        break;
                    }
                    case "MaterialExpressionRotator":
                    {
                        var timeInput = properties.GetProp<StructProperty>("Time");
                        CodeValue Angle(bool cosine)
                        {
                            // Native ab17e40 creates Speed BEFORE visiting Time on both paths.
                            var speed = Constant(properties.GetProp<FloatProperty>("Speed")?.Value ?? .25f);
                            var time = timeInput?.GetProp<ObjectProperty>("Expression")?.Value is > 0
                                ? CompileInput(timeInput, expression.InstancedFullPath + ".Time", statements, cache) : CompileTime(false);
                            if (time == null) return null;
                            var angle = CompileArithmetic(time, speed, "Multiply", expression.InstancedFullPath, statements);
                            return angle == null ? null : CompileTrig(angle, cosine, statements);
                        }
                        var cos = Angle(true); if (cos == null) return null;
                        var sin = Angle(false); if (sin == null) return null;
                        var negativeSin = CompileArithmetic(Constant(-1f), sin, "Multiply", expression.InstancedFullPath, statements);
                        if (negativeSin == null) return null;
                        var row0 = CompileAppend(cos, negativeSin, expression.InstancedFullPath); if (row0 == null) return null;
                        var row1 = CompileAppend(sin, cos, expression.InstancedFullPath); if (row1 == null) return null;
                        var center = Vector(MaterialValueType.Vector2, [properties.GetProp<FloatProperty>("CenterX")?.Value ?? .5f,
                            properties.GetProp<FloatProperty>("CenterY")?.Value ?? .5f]);
                        var coordinateInput = properties.GetProp<StructProperty>("Coordinate");
                        var coordinate = coordinateInput?.GetProp<ObjectProperty>("Expression")?.Value is > 0
                            ? CompileInput(coordinateInput, expression.InstancedFullPath + ".Coordinate", statements, cache) : CompileDefaultTextureCoordinate(statements);
                        if (coordinate == null) return null;
                        CodeValue Project(CodeValue row, bool mask)
                        {
                            var xy = mask ? ComponentMask(coordinate, true, true, false, false, expression.InstancedFullPath) : coordinate;
                            if (xy == null) return null;
                            var offset = CompileArithmetic(xy, center, "Subtract", expression.InstancedFullPath, statements);
                            return offset == null ? null : CompileDot(row, offset, expression.InstancedFullPath, statements);
                        }
                        // Both masked projections are emitted before checking the coordinate
                        // width, even when the non-float3 path later discards their results.
                        var x = Project(row0, true); if (x == null) return null;
                        var y = Project(row1, true); if (y == null) return null;
                        CodeValue blue = null;
                        if (coordinate.Type == MaterialValueType.Vector3)
                            blue = ComponentMask(coordinate, false, false, true, false, expression.InstancedFullPath);
                        else
                        {
                            x = Project(row0, false); if (x == null) return null;
                            y = Project(row1, false); if (y == null) return null;
                        }
                        var rotated = CompileAppend(x, y, expression.InstancedFullPath); if (rotated == null) return null;
                        var shifted = CompileArithmetic(rotated, center, "Add", expression.InstancedFullPath, statements); if (shifted == null) return null;
                        value = coordinate.Type == MaterialValueType.Vector3 ? blue == null ? null : CompileAppend(shifted, blue, expression.InstancedFullPath) : shifted;
                        if (value == null) return null;
                        break;
                    }
                    case "MaterialExpressionBumpOffset":
                    {
                        var heightInput = properties.GetProp<StructProperty>("Height");
                        if (heightInput?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                        {
                            errors.Add("Missing Height input");
                            return null;
                        }
                        // Native ab152d0 visits coordinates first and has no HeightRatioInput.
                        var coordinateInput = properties.GetProp<StructProperty>("Coordinate");
                        var coordinate = coordinateInput?.GetProp<ObjectProperty>("Expression")?.Value is > 0
                            ? CompileInput(coordinateInput, expression.InstancedFullPath + ".Coordinate", statements, cache)
                            : CompileDefaultTextureCoordinate(statements);
                        if (coordinate == null) return null;
                        // Shipping Engine.pcc CDO stores HeightRatio=.05 and ReferencePlane=.5.
                        float ratio = properties.GetProp<FloatProperty>("HeightRatio")?.Value ?? 0.05f;
                        float plane = properties.GetProp<FloatProperty>("ReferencePlane")?.Value ?? 0.5f;
                        var reference = Constant(-(plane * ratio));
                        var height = CompileInput(heightInput, expression.InstancedFullPath + ".Height", statements, cache);
                        if (height == null) return null;
                        height = ForceCastAccessed(height, MaterialValueType.Scalar);
                        var scaledHeight = CompileArithmetic(Constant(ratio), height, "Multiply", expression.InstancedFullPath, statements);
                        if (scaledHeight == null) return null;
                        var offset = CompileArithmetic(scaledHeight, reference, "Add", expression.InstancedFullPath, statements);
                        if (offset == null) return null;
                        if (compilingVertexProperty)
                        {
                            errors.Add("Invalid node used in vertex shader input!");
                            return null;
                        }
                        var camera = new CodeValue(MaterialValueType.Vector2, "Parameters.TangentCameraVector.rg", true);
                        var displaced = CompileArithmetic(camera, offset, "Multiply", expression.InstancedFullPath, statements);
                        if (displaced == null) return null;
                        value = CompileArithmetic(displaced, coordinate, "Add", expression.InstancedFullPath, statements);
                        if (value == null) return null;
                        break;
                    }
                    case "MaterialExpressionDesaturation":
                    {
                        var input = properties.GetProp<StructProperty>("Input");
                        if (input?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                        {
                            errors.Add("Missing Desaturation input");
                            return null;
                        }
                        // Native ab16100: Color, Constant3(weights), Dot, optional Percent, Lerp.
                        var color = CompileInput(input, expression.InstancedFullPath + ".Input", statements, cache);
                        if (color == null) return null;
                        var factors = properties.GetProp<StructProperty>("LuminanceFactors")?.Properties;
                        var weights = Vector(MaterialValueType.Vector3, [factors?.GetProp<FloatProperty>("R")?.Value ?? 0.3f,
                            factors?.GetProp<FloatProperty>("G")?.Value ?? 0.59f, factors?.GetProp<FloatProperty>("B")?.Value ?? 0.11f]);
                        var grey = CompileDot(color, weights, expression.InstancedFullPath, statements);
                        if (grey == null) return null;
                        var percentInput = properties.GetProp<StructProperty>("Percent");
                        if (percentInput?.GetProp<ObjectProperty>("Expression")?.Value is > 0)
                        {
                            var percent = CompileInput(percentInput, expression.InstancedFullPath + ".Percent", statements, cache);
                            if (percent == null) return null;
                            value = CompileLerp(color, grey, percent, expression.InstancedFullPath, statements);
                            if (value == null) return null;
                        }
                        else value = grey;
                        break;
                    }
                    case "MaterialExpressionFresnel":
                    {
                        // Native ab16cc0: Normal + scalar Exponent only; no ExponentIn.
                        var normalInput = properties.GetProp<StructProperty>("Normal");
                        var normal = normalInput?.GetProp<ObjectProperty>("Expression")?.Value is > 0
                            ? CompileInput(normalInput, expression.InstancedFullPath + ".Normal", statements, cache)
                            : Vector(MaterialValueType.Vector3, [0f, 0f, 1f]);
                        if (normal == null) return null;
                        if (compilingVertexProperty)
                        {
                            errors.Add("Invalid node used in vertex shader input!");
                            return null;
                        }
                        var camera = new CodeValue(MaterialValueType.Vector3, "Parameters.TangentCameraVector", true);
                        var dot = CompileDot(normal, camera, expression.InstancedFullPath, statements);
                        if (dot == null) return null;
                        string maxLocal = $"Local{localIndex++}";
                        statements.Add($"\t{numericType} {maxLocal} = max({Constant(0f).Code},{dot.Code});\r\n");
                        var difference = CompileArithmetic(Constant(1f), new CodeValue(MaterialValueType.Scalar, maxLocal, true),
                            "Subtract", expression.InstancedFullPath, statements);
                        // Shipping Engine.pcc CDO stores Exponent=3.
                        var exponent = Constant(properties.GetProp<FloatProperty>("Exponent")?.Value ?? 3f);
                        string exponentCode = exponent.Code, baseCode = difference.Code;
                        string powerLocal = $"Local{localIndex++}";
                        statements.Add($"\t{numericType} {powerLocal} = pow({baseCode},{exponentCode});\r\n");
                        value = new CodeValue(MaterialValueType.Scalar, powerLocal, true);
                        break;
                    }
                    case "MaterialExpressionAbs":
                    case "MaterialExpressionFloor":
                    case "MaterialExpressionCeil":
                    case "MaterialExpressionFrac":
                    case "MaterialExpressionSquareRoot":
                    {
                        string operation = expression.ClassName["MaterialExpression".Length..];
                        var input = properties.GetProp<StructProperty>("Input");
                        if (input?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                        {
                            errors.Add(operation == "SquareRoot" ? "Missing square root input" : $"Missing {operation} input");
                            return null;
                        }
                        var x = CompileInput(input, expression.InstancedFullPath + ".Input", statements, cache);
                        if (x == null) return null;
                        value = CompileUnary(x, operation, statements);
                        break;
                    }
                    case "MaterialExpressionRotateAboutAxis":
                    {
                        string[] names = ["NormalizedRotationAxisAndAngle", "PositionOnAxis", "Position"];
                        var inputs = names.Select(name => properties.GetProp<StructProperty>(name)).ToArray();
                        for (int i = 0; i < inputs.Length; i++)
                        {
                            if (inputs[i]?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                            { errors.Add("Missing RotateAboutAxis input " + names[i]); return null; }
                        }
                        // Native ab17d50 validates axis/pivot/position, then visits in reverse.
                        var position = CompileInput(inputs[2], expression.InstancedFullPath + ".Position", statements, cache);
                        var pivot = CompileInput(inputs[1], expression.InstancedFullPath + ".PositionOnAxis", statements, cache);
                        var axis = CompileInput(inputs[0], expression.InstancedFullPath + ".NormalizedRotationAxisAndAngle", statements, cache);
                        if (position == null || pivot == null || axis == null) return null;
                        // +250 ab54150 uses CoerceParameter, not truncating ForceCast.
                        string positionCode = CoerceNumericOperand(position, MaterialValueType.Vector3);
                        string pivotCode = CoerceNumericOperand(pivot, MaterialValueType.Vector3);
                        string axisCode = CoerceNumericOperand(axis, MaterialValueType.Vector4);
                        if (positionCode == null || pivotCode == null || axisCode == null) return null;
                        string local = $"Local{localIndex++}";
                        statements.Add($"\t{numericType}3 {local} = RotateAboutAxis({axisCode},{pivotCode},{positionCode});\r\n");
                        value = new CodeValue(MaterialValueType.Vector3, local, true);
                        break;
                    }
                    case "MaterialExpressionLightVector":
                    case "MaterialExpressionPerInstanceRandom":
                    case "MaterialExpressionOcclusionPercentage":
                    case "MaterialExpressionLightmapUVs":
                    case "MaterialExpressionLensFlareIntensity":
                    case "MaterialExpressionLensFlareOcclusion":
                    case "MaterialExpressionLensFlareRadialDistance":
                    case "MaterialExpressionLensFlareRayDistance":
                    case "MaterialExpressionLensFlareSourceDistance":
                    {
                        if (compilingVertexProperty)
                        { errors.Add("Invalid node used in vertex shader input!"); return null; }
                        string kind = expression.ClassName["MaterialExpression".Length..];
                        if (kind == "LightVector")
                            value = new CodeValue(MaterialValueType.Vector3, "Parameters.TangentLightVector", true);
                        else if (kind == "PerInstanceRandom")
                            // Native +288 uses explicit Float1, without setting vertex-color usage.
                            value = new CodeValue(MaterialValueType.Scalar, "Parameters.VertexColor.x", true, ExplicitNativeNumericType: 1);
                        else
                        {
                            bool lightmap = kind == "LightmapUVs";
                            if (lightmap) usesLightmapUVs = true; // Material+ac |= 0x10.
                            string local = $"Local{localIndex++}";
                            var type = lightmap ? MaterialValueType.Vector2 : MaterialValueType.Scalar;
                            // LensFlareOcclusion shares the OcclusionPercentage callback.
                            string code = kind switch
                            {
                                "LightmapUVs" => "GetLightmapUVs(Parameters)",
                                "LensFlareIntensity" or "LensFlareRadialDistance" or "LensFlareRayDistance" or "LensFlareSourceDistance" => $"Get{kind}(Parameters)",
                                _ => "GetOcclusionPercentage()"
                            };
                            statements.Add($"\t{DescribeType(type)} {local} = {code};\r\n");
                            value = new CodeValue(type, local, true, ExplicitNativeNumericType: lightmap ? 2 : 1);
                        }
                        break;
                    }
                    case "MaterialExpressionTwoSidedSign":
                    {
                        if (compilingVertexProperty)
                        {
                            errors.Add("Invalid node used in vertex shader input!");
                            return null;
                        }
                        // Native translator ab5a380 emits generic Float, independent of
                        // the material's TwoSided flag. The source initializes this field.
                        value = new CodeValue(MaterialValueType.Scalar, "Parameters.TwoSidedSign", true);
                        break;
                    }
                    case "MaterialExpressionLightmassReplace":
                    {
                        var realtimeInput = properties.GetProp<StructProperty>("Realtime");
                        var lightmassInput = properties.GetProp<StructProperty>("Lightmass");
                        if (realtimeInput?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                        { errors.Add("Missing LightmassReplace input Realtime"); return null; }
                        if (lightmassInput?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                        { errors.Add("Missing LightmassReplace input Lightmass"); return null; }
                        // Native ab16fb0 visits BOTH inputs before +228 returns Realtime.
                        // Do not short-circuit the Lightmass branch: its flags/errors count.
                        var realtime = CompileInput(realtimeInput, expression.InstancedFullPath + ".Realtime", statements, cache);
                        var lightmass = CompileInput(lightmassInput, expression.InstancedFullPath + ".Lightmass", statements, cache);
                        if (realtime == null || lightmass == null) return null;
                        value = realtime;
                        break;
                    }
                    case "MaterialExpressionConstantBiasScale":
                    {
                        var input = properties.GetProp<StructProperty>("Input");
                        if (input?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                        {
                            errors.Add("Missing ConstantBiasScale input");
                            return null;
                        }
                        // Native ab156e0 creates Scale before visiting Input, then Bias.
                        var scale = Constant(properties.GetProp<FloatProperty>("Scale")?.Value ?? .5f);
                        var x = CompileInput(input, expression.InstancedFullPath + ".Input", statements, cache);
                        if (x == null) return null;
                        var bias = Constant(properties.GetProp<FloatProperty>("Bias")?.Value ?? 1f);
                        var shifted = CompileArithmetic(bias, x, "Add", expression.InstancedFullPath, statements);
                        if (shifted == null) return null;
                        value = CompileArithmetic(shifted, scale, "Multiply", expression.InstancedFullPath, statements);
                        if (value == null) return null;
                        break;
                    }
                    case "MaterialExpressionDistance":
                    {
                        var aInput = properties.GetProp<StructProperty>("A");
                        var bInput = properties.GetProp<StructProperty>("B");
                        if (aInput?.GetProp<ObjectProperty>("Expression")?.Value is not > 0) { errors.Add("Missing input A"); return null; }
                        if (bInput?.GetProp<ObjectProperty>("Expression")?.Value is not > 0) { errors.Add("Missing input B"); return null; }
                        // Native ab16200 visits A then B and composes Sub, Dot, SquareRoot.
                        var a = CompileInput(aInput, expression.InstancedFullPath + ".A", statements, cache);
                        var b = CompileInput(bInput, expression.InstancedFullPath + ".B", statements, cache);
                        if (a == null || b == null) return null;
                        var delta = CompileArithmetic(a, b, "Subtract", expression.InstancedFullPath, statements);
                        if (delta == null) return null;
                        var squared = CompileDot(delta, delta, expression.InstancedFullPath, statements);
                        if (squared == null) return null;
                        value = CompileUnary(squared, "SquareRoot", statements);
                        break;
                    }
                    case "MaterialExpressionDeriveNormalZ":
                    {
                        var input = properties.GetProp<StructProperty>("InXY");
                        if (input?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                        {
                            errors.Add("Missing input normal xy vector whose z should be derived.");
                            return null;
                        }
                        var xy = CompileInput(input, expression.InstancedFullPath + ".InXY", statements, cache);
                        if (xy == null) return null;
                        // Native ab16010: ForceCast(default flags), Dot, Sub(1,dot), sqrt,
                        // AppendVector, ForceCast(default flags). No saturate/epsilon guard.
                        xy = ForceCastAccessed(xy, MaterialValueType.Vector2);
                        var squared = CompileDot(xy, xy, expression.InstancedFullPath, statements);
                        if (squared == null) return null;
                        var remainder = CompileArithmetic(Constant(1f), squared, "Subtract", expression.InstancedFullPath, statements);
                        if (remainder == null) return null;
                        var z = CompileUnary(remainder, "SquareRoot", statements);
                        var appended = CompileAppend(xy, z, expression.InstancedFullPath);
                        if (appended == null) return null;
                        value = ForceCastAccessed(appended, MaterialValueType.Vector3);
                        break;
                    }
                    case "MaterialExpressionNormalize":
                    {
                        var input = properties.GetProp<StructProperty>("VectorInput");
                        if (input?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                        {
                            errors.Add("Missing Normalize input");
                            return null;
                        }
                        // LE3 0x7ff74ab17870: visit V once, then Dot(V,V), SquareRoot,
                        // Div(V,length). This is not the HLSL normalize intrinsic. Preserve
                        // the same callbacks' uniform trees, CPU lanes and lazy access order.
                        var x = CompileInput(input, expression.InstancedFullPath + ".VectorInput", statements, cache);
                        if (x == null) return null;
                        var dot = CompileDot(x, x, expression.InstancedFullPath, statements);
                        if (dot == null) return null;
                        var length = CompileUnary(dot, "SquareRoot", statements);
                        value = CompileArithmetic(x, length, "Divide", expression.InstancedFullPath, statements);
                        if (value == null) return null;
                        break;
                    }
                    case "MaterialExpressionFmod":
                    {
                        var aInput = properties.GetProp<StructProperty>("A");
                        var bInput = properties.GetProp<StructProperty>("B");
                        foreach (var (name, input) in new[] { ("A", aInput), ("B", bInput) })
                        {
                            if (input?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                            {
                                errors.Add($"Missing Fmod input {name}");
                                return null;
                            }
                        }
                        // Native Compile 0x7ff74ab16800 checks A then B, but visits B
                        // before A. Callback +a8 retains A's type and coerces B first.
                        var b = CompileInput(bInput, expression.InstancedFullPath + ".B", statements, cache);
                        var a = CompileInput(aInput, expression.InstancedFullPath + ".A", statements, cache);
                        if (a == null || b == null) return null;
                        if (a.Type != b.Type && a.Type != MaterialValueType.Scalar && b.Type != MaterialValueType.Scalar)
                        {
                            errors.Add($"{expression.InstancedFullPath}: Fmod coercion failed: {b.Type} -> {a.Type}.");
                            return null;
                        }
                        string right = CoerceBound(b, a.Type);
                        string code = $"fmod({a.Code},{right})";
                        if (a.PerPixel || b.PerPixel)
                        {
                            string local = $"Local{localIndex++}";
                            statements.Add($"\t{DescribeType(a.Type)} {local} = {code};\r\n");
                            value = new CodeValue(a.Type, local, true);
                        }
                        else
                        {
                            var uniform = uniformExpressionPool.OfType<MaterialUniformExpressionFmod>()
                                .FirstOrDefault(p => ReferenceEquals(p.A, a.Uniform) && ReferenceEquals(p.B, b.Uniform));
                            if (uniform == null)
                            {
                                uniform = new MaterialUniformExpressionFmod
                                {
                                    ExpressionType = "FMaterialUniformExpressionFmod", A = a.Uniform, B = b.Uniform
                                };
                                uniformExpressionPool.Add(uniform);
                            }
                            value = UniformValue(a.Type, code, uniform, a.IsConstant && b.IsConstant);
                        }
                        break;
                    }
                    case "MaterialExpressionConstantClamp":
                    case "MaterialExpressionClamp":
                    {
                        bool constantBounds = expression.ClassName == "MaterialExpressionConstantClamp";
                        var input = properties.GetProp<StructProperty>("Input");
                        var minInput = constantBounds ? null : properties.GetProp<StructProperty>("Min");
                        var maxInput = constantBounds ? null : properties.GetProp<StructProperty>("Max");
                        if (input?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                        {
                            errors.Add(constantBounds ? expression.InstancedFullPath + ".Input is not connected." : "Missing Clamp input");
                            return null;
                        }
                        // Native ConstantClamp ab157a0 creates Max, Min, then compiles Input.
                        // Shipping Engine.pcc CDO stores Max=1; Min retains the zero default.
                        var constantMax = constantBounds ? Constant(properties.GetProp<FloatProperty>("Max")?.Value ?? 1f) : null;
                        var constantMin = constantBounds ? Constant(properties.GetProp<FloatProperty>("Min")?.Value ?? 0f) : null;
                        var x = CompileInput(input, expression.InstancedFullPath + ".Input", statements, cache);
                        bool hasMin = constantBounds || minInput?.GetProp<ObjectProperty>("Expression")?.Value is > 0;
                        bool hasMax = constantBounds || maxInput?.GetProp<ObjectProperty>("Expression")?.Value is > 0;
                        var min = constantBounds ? constantMin : hasMin ? CompileInput(minInput, expression.InstancedFullPath + ".Min", statements, cache) : null;
                        var max = constantBounds ? constantMax : hasMax ? CompileInput(maxInput, expression.InstancedFullPath + ".Max", statements, cache) : null;
                        if (x == null || hasMin && min == null || hasMax && max == null) return null;
                        if (!hasMin && !hasMax)
                        {
                            value = x;
                            break;
                        }
                        value = CompileClamp(x, min, max, expression.InstancedFullPath, statements);
                        if (value == null) return null;
                        break;
                    }
                    case "MaterialExpressionSphereMask":
                    {
                        var aInput = properties.GetProp<StructProperty>("A");
                        var bInput = properties.GetProp<StructProperty>("B");
                        if (aInput?.GetProp<ObjectProperty>("Expression")?.Value is not > 0) { errors.Add("Missing input A"); return null; }
                        if (bInput?.GetProp<ObjectProperty>("Expression")?.Value is not > 0) { errors.Add("Missing input B"); return null; }
                        var a = CompileInput(aInput, expression.InstancedFullPath + ".A", statements, cache);
                        var b = CompileInput(bInput, expression.InstancedFullPath + ".B", statements, cache);
                        if (a == null || b == null) return null;
                        // Native ab182b0 uses the existing callbacks in this order. The stored
                        // hardness is scaled directly, with no (1-hardness) transformation.
                        var delta = CompileArithmetic(a, b, "Subtract", expression.InstancedFullPath, statements);
                        if (delta == null) return null;
                        var squared = CompileDot(delta, delta, expression.InstancedFullPath, statements);
                        if (squared == null) return null;
                        var distance = CompileUnary(squared, "SquareRoot", statements);
                        float radius = properties.GetProp<FloatProperty>("AttenuationRadius")?.Value ?? 256f;
                        float hardness = (properties.GetProp<FloatProperty>("HardnessPercent")?.Value ?? 100f) * .01f;
                        // MAXSS chooses its second operand (epsilon) on equality/unordered.
                        var inverseRadius = Constant(1f / (radius > .00001f ? radius : .00001f));
                        var normalized = CompileArithmetic(distance, inverseRadius, "Multiply", expression.InstancedFullPath, statements);
                        if (normalized == null) return null;
                        var inverted = CompileArithmetic(Constant(1f), normalized, "Subtract", expression.InstancedFullPath, statements);
                        if (inverted == null) return null;
                        var inverseHardness = Constant(1f / (hardness > .00001f ? hardness : .00001f));
                        var shape = CompileArithmetic(inverted, inverseHardness, "Multiply", expression.InstancedFullPath, statements);
                        if (shape == null) return null;
                        var max = Constant(1f); var min = Constant(0f);
                        value = CompileClamp(shape, min, max, expression.InstancedFullPath, statements);
                        if (value == null) return null;
                        break;
                    }
                    case "MaterialExpressionPower":
                    {
                        var baseInput = properties.GetProp<StructProperty>("Base");
                        var exponentInput = properties.GetProp<StructProperty>("Exponent");
                        foreach (var (name, input) in new[] { ("Base", baseInput), ("Exponent", exponentInput) })
                        {
                            if (input?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                            {
                                errors.Add($"Missing Power {name} input");
                                return null;
                            }
                        }
                        var @base = CompileInput(baseInput, expression.InstancedFullPath + ".Base", statements, cache);
                        var exponent = CompileInput(exponentInput, expression.InstancedFullPath + ".Exponent", statements, cache);
                        if (@base == null || exponent == null) return null;
                        // Coercing to MCT_Float=0xf keeps any numeric vector code unchanged.
                        string right = exponent.Code;
                        // Native strips only the first/last characters before _wtof. Every
                        // supported constant definition starts with a type/function/operator,
                        // not a numeric prefix after that trim; even float(1) parses as zero.
                        // Preserve rendered-text parsing rather than evaluating the exponent tree.
                        string left = @base.Code;
                        string local = $"Local{localIndex++}";
                        statements.Add($"\t{DescribeType(@base.Type)} {local} = pow({left},{right});\r\n");
                        value = new CodeValue(@base.Type, local, true);
                        break;
                    }
                    case "MaterialExpressionCrossProduct":
                    {
                        var aInput = properties.GetProp<StructProperty>("A");
                        var bInput = properties.GetProp<StructProperty>("B");
                        foreach (var (name, input) in new[] { ("A", aInput), ("B", bInput) })
                        {
                            if (input?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                            {
                                errors.Add($"Missing CrossProduct input {name}");
                                return null;
                            }
                        }
                        var a = CompileInput(aInput, expression.InstancedFullPath + ".A", statements, cache);
                        var b = CompileInput(bInput, expression.InstancedFullPath + ".B", statements, cache);
                        if (a == null || b == null) return null;
                        if (a.Type is not (MaterialValueType.Scalar or MaterialValueType.Vector3) ||
                            b.Type is not (MaterialValueType.Scalar or MaterialValueType.Vector3))
                        {
                            errors.Add($"{expression.InstancedFullPath}: CrossProduct coercion to Vector3 failed for {a.Type}, {b.Type}.");
                            return null;
                        }
                        string right = CoerceScalarOperand(b, MaterialValueType.Vector3);
                        string left = CoerceScalarOperand(a, MaterialValueType.Vector3);
                        string local = $"Local{localIndex++}";
                        statements.Add($"\t{DescribeType(MaterialValueType.Vector3)} {local} = cross({left},{right});\r\n");
                        // Cross never preserves uniform objects, including two constants.
                        value = new CodeValue(MaterialValueType.Vector3, local, true);
                        break;
                    }
                    case "MaterialExpressionDotProduct":
                    {
                        var aInput = properties.GetProp<StructProperty>("A");
                        var bInput = properties.GetProp<StructProperty>("B");
                        foreach (var (name, input) in new[] { ("A", aInput), ("B", bInput) })
                        {
                            if (input?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                            {
                                errors.Add($"Missing DotProduct input {name}");
                                return null;
                            }
                        }
                        var a = CompileInput(aInput, expression.InstancedFullPath + ".A", statements, cache);
                        var b = CompileInput(bInput, expression.InstancedFullPath + ".B", statements, cache);
                        if (a == null || b == null) return null;
                        value = CompileDot(a, b, expression.InstancedFullPath, statements);
                        if (value == null) return null;
                        break;
                    }
                    case "MaterialExpressionAppendVector":
                    {
                        var aInput = properties.GetProp<StructProperty>("A");
                        var bInput = properties.GetProp<StructProperty>("B");
                        foreach (var (name, input) in new[] { ("A", aInput), ("B", bInput) })
                        {
                            if (input?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                            {
                                errors.Add($"Missing AppendVector input {name}");
                                return null;
                            }
                        }
                        var a = CompileInput(aInput, expression.InstancedFullPath + ".A", statements, cache);
                        var b = CompileInput(bInput, expression.InstancedFullPath + ".B", statements, cache);
                        if (a == null || b == null) return null;
                        value = CompileAppend(a, b, expression.InstancedFullPath);
                        if (value == null) return null;
                        break;
                    }
                    case "MaterialExpressionIf":
                    {
                        // LE3 Compile 0x7ff74ab16da0 requires all five connections before visiting
                        // A then B. Unlike newer UE revisions there is no threshold/default input.
                        string[] names = ["A", "B", "AGreaterThanB", "AEqualsB", "ALessThanB"];
                        var inputs = names.Select(name => properties.GetProp<StructProperty>(name)).ToArray();
                        for (int i = 0; i < inputs.Length; i++)
                        {
                            if (inputs[i]?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                            {
                                errors.Add($"Missing If {names[i]} input");
                                return null;
                            }
                        }
                        var a = CompileInput(inputs[0], expression.InstancedFullPath + ".A", statements, cache);
                        var b = CompileInput(inputs[1], expression.InstancedFullPath + ".B", statements, cache);
                        if (a == null || b == null) return null;
                        // Exact MCT_Float=0xf check. LE3's one-channel ComponentMask also returns
                        // 0xf (not MCT_Float1=1), so those masked comparisons remain valid.
                        if (a.NativeNumericType != 0xf || b.NativeNumericType != 0xf)
                        {
                            errors.Add($"If input {(a.NativeNumericType != 0xf ? "A" : "B")} must be of type float.");
                            return null;
                        }
                        var greater = CompileInput(inputs[2], expression.InstancedFullPath + ".AGreaterThanB", statements, cache);
                        var equal = CompileInput(inputs[3], expression.InstancedFullPath + ".AEqualsB", statements, cache);
                        var less = CompileInput(inputs[4], expression.InstancedFullPath + ".ALessThanB", statements, cache);
                        if (greater == null || equal == null || less == null) return null;
                        value = CompileIf(a, b, greater, equal, less, expression.InstancedFullPath, statements);
                        if (value == null) return null;
                        break;
                    }
                    case "MaterialExpressionLinearInterpolate":
                    {
                        var aInput = properties.GetProp<StructProperty>("A");
                        var bInput = properties.GetProp<StructProperty>("B");
                        var alphaInput = properties.GetProp<StructProperty>("Alpha");
                        foreach (var (name, input) in new[] { ("A", aInput), ("B", bInput), ("Alpha", alphaInput) })
                        {
                            if (input?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                            {
                                errors.Add($"Missing LinearInterpolate input {name}");
                                return null;
                            }
                        }
                        var a = CompileInput(aInput, expression.InstancedFullPath + ".A", statements, cache);
                        var b = CompileInput(bInput, expression.InstancedFullPath + ".B", statements, cache);
                        var alpha = CompileInput(alphaInput, expression.InstancedFullPath + ".Alpha", statements, cache);
                        if (a == null || b == null || alpha == null) return null;
                        value = CompileLerp(a, b, alpha, expression.InstancedFullPath, statements);
                        if (value == null) return null;
                        break;
                    }
                    case "MaterialExpressionTextureCoordinate":
                    {
                        if (compilingVertexProperty)
                        {
                            errors.Add("Invalid node used in vertex shader input!");
                            return null;
                        }
                        int coordinateIndex = properties.GetProp<IntProperty>("CoordinateIndex")?.Value ?? 0;
                        float uTiling = properties.GetProp<FloatProperty>("UTiling")?.Value ?? 1f;
                        float vTiling = properties.GetProp<FloatProperty>("VTiling")?.Value ?? 1f;
                        // LE3 expression Compile subtracts in float32, then promotes the absolute
                        // difference to double for a strict > 1e-8 comparison. The scalar path uses
                        // U only. Even (1,1) must emit the subsequent Mul; it is not an inline UV.
                        var tiling = (double)MathF.Abs(uTiling - vTiling) > 1e-8
                            ? Vector(MaterialValueType.Vector2, uTiling, vTiling) : Constant(uTiling);

                        bool unMirrorU = properties.GetProp<BoolProperty>("UnMirrorU")?.Value ?? false;
                        bool unMirrorV = properties.GetProp<BoolProperty>("UnMirrorV")?.Value ?? false;
                        uint actualIndex = usedWithDecals ? 0u : unchecked((uint)coordinateIndex);
                        numUserTexCoords = Math.Max(numUserTexCoords, unchecked((uint)coordinateIndex + 1u));
                        string coordinate = $"Parameters.TexCoords[{actualIndex}].xy";
                        if (numUserTexCoords == 0)
                        {
                            coordinate = "float2(0,0)";
                        }
                        else
                        {
                            coordinate = (unMirrorU, unMirrorV) switch
                            {
                                (true, true) => $"UnMirrorUV({coordinate}, Parameters)",
                                (true, false) => $"UnMirrorU({coordinate}, Parameters)",
                                (false, true) => $"UnMirrorV({coordinate}, Parameters)",
                                _ => coordinate
                            };
                            if (unMirrorU || unMirrorV) usingTransforms |= 1; // Native UsedCoord_World.
                        }
                        string local = $"Local{localIndex++}";
                        statements.Add($"\t{DescribeType(MaterialValueType.Vector2)} {local} = {coordinate};\r\n");
                        string tiledLocal = $"Local{localIndex++}";
                        // This is the nonuniform Mul branch: scalar tiling swaps ahead of UV,
                        // whereas a float2 tiling preserves UV * tiling order.
                        string multiplication = tiling.Type == MaterialValueType.Scalar
                            ? $"({tiling.Code} * {local})" : $"({local} * {tiling.Code})";
                        statements.Add($"\t{DescribeType(MaterialValueType.Vector2)} {tiledLocal} = {multiplication};\r\n");
                        value = new CodeValue(MaterialValueType.Vector2, tiledLocal, true);
                        break;
                    }
                    case "MaterialExpressionComponentMask":
                    case "MaterialExpressionStaticComponentMaskParameter":
                    {
                        var input = properties.GetProp<StructProperty>("Input");
                        if (input?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                        {
                            errors.Add("Missing ComponentMask input");
                            return null;
                        }
                        bool isStaticMask = expression.ClassName == "MaterialExpressionStaticComponentMaskParameter";
                        // LE3 UMaterial::UpdateStaticExpressionParameters (0x7ff74ab2e320) binds
                        // the first full FName match, ignoring bOverride and ExpressionGUID here.
                        // This set is already resolved; instance inheritance is the caller's job.
                        NameReference parameterName = properties.GetProp<NameProperty>("ParameterName")?.Value ?? NameReference.None;
                        var parameter = isStaticMask
                            ? staticParameters?.StaticComponentMaskParameters?.FirstOrDefault(p => p.ParameterName == parameterName)
                            : null;
                        var inputValue = CompileInput(input, expression.InstancedFullPath + ".Input", statements, cache);
                        if (inputValue == null) return null;
                        string prefix = isStaticMask ? "Default" : "";
                        value = ComponentMask(inputValue,
                            parameter?.R ?? properties.GetProp<BoolProperty>(prefix + "R")?.Value == true,
                            parameter?.G ?? properties.GetProp<BoolProperty>(prefix + "G")?.Value == true,
                            parameter?.B ?? properties.GetProp<BoolProperty>(prefix + "B")?.Value == true,
                            parameter?.A ?? properties.GetProp<BoolProperty>(prefix + "A")?.Value == true, expression.InstancedFullPath);
                        if (value == null) return null;
                        break;
                    }
                    case "MaterialExpressionStaticSwitchParameter":
                    {
                        bool selected = ResolveStaticSwitch(properties);
                        var selectedInput = properties.GetProp<StructProperty>(selected ? "A" : "B");
                        // Compile ab185b0 tail-jumps into ExpressionInput_Compile ab49d00, which returns
                        // -1 for a null Expression without Errorf. Shipping instances rely on this.
                        if (selectedInput?.GetProp<ObjectProperty>("Expression")?.Value is not > 0)
                        {
                            value = IndexNoneValue;
                            break;
                        }
                        // Selected static inputs pass chunks through unchanged, including textures.
                        value = CompileInput(selectedInput, expression.InstancedFullPath, statements, cache, allowTexture: true);
                        if (value == null) return null;
                        break;
                    }
                    case "MaterialExpressionCustomTexture":
                    {
                        int textureUIndex = properties.GetProp<ObjectProperty>("Texture")?.Value ?? 0;
                        if (textureUIndex == 0 || !material.FileRef.TryGetEntry(textureUIndex, out var texture) || texture == null)
                        {
                            errors.Add("CustomTexture> Missing input texture");
                            return null;
                        }
                        // Ordinary Texture compile: frequency guard, then an unregistered uniform.
                        if (compilingVertexProperty)
                        {
                            errors.Add("Invalid node used in vertex shader input!");
                            return null;
                        }
                        bool isCube;
                        if (texture.IsA("TextureCube")) isCube = true;
                        else if (texture.IsA("Texture2D") || texture.IsA("TextureRenderTarget2D") || texture.IsA("TextureMovie")) isCube = false;
                        else
                        {
                            errors.Add($"{expression.InstancedFullPath} references {texture.ClassName}, which is not a supported CustomTexture class.");
                            return null;
                        }
                        value = CompileTextureObject(textureUIndex, isCube);
                        break;
                    }
                    case "MaterialExpressionCustom":
                        value = CompileCustom(expression, properties, statements, cache);
                        if (value == null) return null;
                        break;
                    default:
                        errors.Add($"{expression.InstancedFullPath} uses unsupported {expression.ClassName}.");
                        return null;
                }

                cache[(expression.UIndex, outputIndex)] = value;
                return value;
            }

            CodeValue CompileTrig(CodeValue x, bool cosine, List<string> statements)
            {
                string code = $"{(cosine ? "cos" : "sin")}({x.Code})";
                if (x.PerPixel)
                {
                    string local = $"Local{localIndex++}";
                    statements.Add($"\t{DescribeType(x.Type)} {local} = {code};\r\n");
                    return new CodeValue(x.Type, local, true);
                }
                var uniform = uniformExpressionPool.OfType<MaterialUniformExpressionSine>()
                    .FirstOrDefault(p => p.bIsCosine == cosine && ReferenceEquals(p.X, x.Uniform));
                if (uniform == null)
                {
                    uniform = new MaterialUniformExpressionSine
                    {
                        ExpressionType = "FMaterialUniformExpressionSine", X = x.Uniform, bIsCosine = cosine
                    };
                    uniformExpressionPool.Add(uniform);
                }
                // Uniform trig returns MCT_Float independent of original child width.
                return UniformValue(MaterialValueType.Scalar, code, uniform, x.IsConstant);
            }

            CodeValue CompileClamp(CodeValue x, CodeValue min, CodeValue max, string source, List<string> statements)
            {
                bool hasMin = min != null, hasMax = max != null;
                if (!hasMin && !hasMax) return x;
                foreach (var bound in new[] { min, max }.Where(b => b != null))
                {
                    if (x.Type != MaterialValueType.Scalar && bound.Type != MaterialValueType.Scalar && bound.Type != x.Type)
                    {
                        errors.Add($"{source}: Clamp bound coercion failed: {bound.Type} -> {x.Type}.");
                        return null;
                    }
                }
                // Access Max, Min, Input; retain original CPU children without replication.
                string maxCode = hasMax ? x.NativeNumericType == 1 ? CoerceNumericOperand(max, MaterialValueType.Scalar) : CoerceBound(max, x.Type) : null;
                string minCode = hasMin ? x.NativeNumericType == 1 ? CoerceNumericOperand(min, MaterialValueType.Scalar) : CoerceBound(min, x.Type) : null;
                if (hasMax && maxCode == null || hasMin && minCode == null) return null;
                string inputCode = x.Code;
                string code = hasMin && hasMax ? $"min(max({inputCode},{minCode}),{maxCode})"
                    : hasMin ? $"max({inputCode},{minCode})" : $"min({inputCode},{maxCode})";
                if (x.PerPixel || min?.PerPixel == true || max?.PerPixel == true)
                {
                    string local = $"Local{localIndex++}";
                    statements.Add($"\t{DescribeType(x.Type)} {local} = {code};\r\n");
                    return new CodeValue(x.Type, local, true, ExplicitNativeNumericType: x.NativeNumericType);
                }
                MaterialUniformExpression uniform;
                if (hasMin && hasMax)
                {
                    uniform = uniformExpressionPool.OfType<MaterialUniformExpressionClamp>()
                        .FirstOrDefault(p => ReferenceEquals(p.Input, x.Uniform) && ReferenceEquals(p.Min, min.Uniform) && ReferenceEquals(p.Max, max.Uniform));
                    uniform ??= new MaterialUniformExpressionClamp
                    {
                        ExpressionType = "FMaterialUniformExpressionClamp", Input = x.Uniform, Min = min.Uniform, Max = max.Uniform
                    };
                }
                else
                {
                    var bound = hasMin ? min : max;
                    uniform = uniformExpressionPool.OfType<MaterialUniformExpressionBinaryOp>()
                        .FirstOrDefault(p => p.GetType() == (hasMin ? typeof(MaterialUniformExpressionMax) : typeof(MaterialUniformExpressionMin)) &&
                            ReferenceEquals(p.A, x.Uniform) && ReferenceEquals(p.B, bound.Uniform));
                    uniform ??= hasMin ? new MaterialUniformExpressionMax
                    {
                        ExpressionType = "FMaterialUniformExpressionMax", A = x.Uniform, B = bound.Uniform
                    } : new MaterialUniformExpressionMin
                    {
                        ExpressionType = "FMaterialUniformExpressionMin", A = x.Uniform, B = bound.Uniform
                    };
                }
                if (!uniformExpressionPool.Contains(uniform)) uniformExpressionPool.Add(uniform);
                return UniformValue(x.Type, code, uniform, x.IsConstant && (!hasMin || min.IsConstant) && (!hasMax || max.IsConstant));
            }

            CodeValue CompileUnary(CodeValue x, string operation, List<string> statements)
            {
                string code = $"{(operation == "SquareRoot" ? "sqrt" : operation.ToLowerInvariant())}({x.Code})";
                if (x.PerPixel)
                {
                    string local = $"Local{localIndex++}";
                    statements.Add($"\t{DescribeType(x.Type)} {local} = {code};\r\n");
                    return new CodeValue(x.Type, local, true, ExplicitNativeNumericType: x.NativeNumericType);
                }
                Type uniformType = operation switch
                {
                    "Abs" => typeof(MaterialUniformExpressionAbs), "Floor" => typeof(MaterialUniformExpressionFloor),
                    "Ceil" => typeof(MaterialUniformExpressionCeil), "SquareRoot" => typeof(MaterialUniformExpressionSquareRoot),
                    _ => typeof(MaterialUniformExpressionFrac)
                };
                var uniform = uniformExpressionPool.OfType<MaterialUniformExpressionUnaryOp>()
                    .FirstOrDefault(p => p.GetType() == uniformType && ReferenceEquals(p.X, x.Uniform));
                if (uniform == null)
                {
                    uniform = operation switch
                    {
                        "Abs" => new MaterialUniformExpressionAbs(), "Floor" => new MaterialUniformExpressionFloor(),
                        "Ceil" => new MaterialUniformExpressionCeil(), "SquareRoot" => new MaterialUniformExpressionSquareRoot(),
                        _ => new MaterialUniformExpressionFrac()
                    };
                    uniform.ExpressionType = "FMaterialUniformExpression" + operation;
                    uniform.X = x.Uniform;
                    uniformExpressionPool.Add(uniform);
                }
                return UniformValue(x.Type, code, uniform, x.IsConstant);
            }

            CodeValue CompileDot(CodeValue a, CodeValue b, string source, List<string> statements)
            {
                bool nonuniform = a.PerPixel || b.PerPixel;
                // Only B is coerced on the nonuniform path; uniform children stay original.
                if (nonuniform && a.Type != b.Type && a.Type != MaterialValueType.Scalar && b.Type != MaterialValueType.Scalar)
                {
                    errors.Add($"{source}: DotProduct coercion failed: {b.Type} -> {a.Type}.");
                    return null;
                }
                string right = nonuniform && b.Type == MaterialValueType.Scalar && a.Type != MaterialValueType.Scalar
                    ? CoerceScalarOperand(b, a.Type) : b.Code;
                string left = a.Code;
                string code = $"dot({left},{right})";
                if (nonuniform)
                {
                    string local = $"Local{localIndex++}";
                    statements.Add($"\t{numericType} {local} = {code};\r\n");
                    return new CodeValue(MaterialValueType.Scalar, local, true);
                }
                var uniform = uniformExpressionPool.OfType<MaterialUniformExpressionFoldedMath>()
                    .FirstOrDefault(p => p.Op == MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Dot &&
                        ReferenceEquals(p.A, a.Uniform) && ReferenceEquals(p.B, b.Uniform));
                if (uniform == null)
                {
                    uniform = new MaterialUniformExpressionFoldedMath
                    {
                        ExpressionType = "FMaterialUniformExpressionFoldedMath", A = a.Uniform, B = b.Uniform,
                        Op = MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Dot
                    };
                    uniformExpressionPool.Add(uniform);
                }
                return UniformValue(MaterialValueType.Scalar, code, uniform, a.IsConstant && b.IsConstant);
            }

            CodeValue CompileArithmetic(CodeValue a, CodeValue b, string operation, string source, List<string> statements)
            {
                // ForceCast can produce explicit float1 (1), which cannot broadcast like Float (0xf).
                if ((a.NativeNumericType & b.NativeNumericType) == 0 &&
                    (a.NativeNumericType == 1 || b.NativeNumericType == 1))
                {
                    errors.Add($"{source}: arithmetic between native numeric types 0x{a.NativeNumericType:x} and 0x{b.NativeNumericType:x} is undefined.");
                    return null;
                }
                var resultType = ArithmeticResultType(a.Type, b.Type, source);
                if (resultType == null) return null;
                string op = operation switch { "Add" => "+", "Multiply" => "*", "Subtract" => "-", _ => "/" };
                int nativeResultType = a.NativeNumericType == 0xf ? b.NativeNumericType : a.NativeNumericType;
                if (op == "*" && (a.PerPixel || b.PerPixel) && b.NativeNumericType == 0xf && a.NativeNumericType != 0xf)
                    (a, b) = (b, a);
                // Native code access is right-before-left, after Mul's scalar-first swap.
                string right = b.Code;
                string left = a.Code;
                string arithmetic = $"({left} {op} {right})";
                if (a.PerPixel || b.PerPixel)
                {
                    string local = $"Local{localIndex++}";
                    statements.Add($"\t{DescribeType(resultType.Value)} {local} = {arithmetic};\r\n");
                    return new CodeValue(resultType.Value, local, true, ExplicitNativeNumericType: nativeResultType);
                }
                var foldedOp = operation switch
                {
                    "Add" => MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Add,
                    "Multiply" => MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Mul,
                    "Subtract" => MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Sub,
                    _ => MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Div
                };
                // Interned children give native ordered recursive identity by reference.
                var uniform = uniformExpressionPool.OfType<MaterialUniformExpressionFoldedMath>()
                    .FirstOrDefault(p => p.Op == foldedOp && ReferenceEquals(p.A, a.Uniform) && ReferenceEquals(p.B, b.Uniform));
                if (uniform == null)
                {
                    uniform = new MaterialUniformExpressionFoldedMath
                    {
                        ExpressionType = "FMaterialUniformExpressionFoldedMath", A = a.Uniform, B = b.Uniform, Op = foldedOp
                    };
                    uniformExpressionPool.Add(uniform);
                }
                return UniformValue(resultType.Value, arithmetic, uniform, a.IsConstant && b.IsConstant);
            }

            CodeValue Constant(float scalar) => ConstantValue(MaterialValueType.Scalar,
                // Shipping LE3 constants/scalar parameters retain R only; folded CPU arithmetic
                // does not replicate R into G/B/A.
                new LinearColor(scalar, 0, 0, 0), $"{numericType}({FormatFloat(scalar)})");

            CodeValue Vector(MaterialValueType type, params float[] components) => ConstantValue(type,
                new LinearColor(components[0], components[1], components.Length > 2 ? components[2] : 0,
                    components.Length > 3 ? components[3] : 0),
                $"{numericType}{(int)type}({string.Join(',', components.Select(FormatFloat))})");

            CodeValue ConstantValue(MaterialValueType type, LinearColor color, string code)
            {
                byte valueType = type switch { MaterialValueType.Scalar => 0xf, MaterialValueType.Vector2 => 2,
                    MaterialValueType.Vector3 => 4, _ => 8 };
                var uniform = uniformExpressionPool.OfType<MaterialUniformExpressionConstant>()
                    .FirstOrDefault(p => p.ValueType == valueType && LE3UniformExpressionComparer.ColorIdentical(p.Value, color));
                if (uniform == null)
                {
                    uniform = new MaterialUniformExpressionConstant
                    {
                        ExpressionType = "FMaterialUniformExpressionConstant", ValueType = valueType, Value = color
                    };
                    uniformExpressionPool.Add(uniform);
                }
                return UniformValue(type, code, uniform, isConstant: true);
            }

            CodeValue UniformValue(MaterialValueType type, string definition, MaterialUniformExpression uniform, bool isConstant)
            {
                if (propertyUniformCodeValues.TryGetValue(uniform, out var existing)) return existing;
                // Capture the shader frequency now: code may be accessed after graph visitation.
                var stageExpressions = type == MaterialValueType.Scalar
                    ? compilingVertexProperty ? uniformVertexScalars : uniformPixelScalars
                    : compilingVertexProperty ? uniformVertexVectors : uniformPixelVectors;
                string stage = compilingVertexProperty ? "Vertex" : "Pixel";
                var value = new CodeValue(type, definition, false, isConstant, isConstant ? null : () =>
                {
                    int index = FindOrAddReference(stageExpressions, uniform);
                    return type == MaterialValueType.Scalar
                        ? $"Uniform{stage}Scalars_{index / 4}.{"xyzw"[index % 4]}"
                        : $"Uniform{stage}Vector_{index}" + (type == MaterialValueType.Vector4 ? "" : type == MaterialValueType.Vector3 ? ".rgb" : ".rg");
                }, uniform);
                propertyUniformCodeValues.Add(uniform, value);
                return value;
            }

            string EmitVectorOperation(string left, string operation, float[] right, List<string> statements)
            {
                string local = $"Local{localIndex++}";
                string operand = Vector(MaterialValueType.Vector4, right).Code;
                statements.Add($"\t{numericType}4 {local} = ({left} {operation} {operand});\r\n");
                return local;
            }

            static int FindOrAddReference(List<MaterialUniformExpression> expressions,
                MaterialUniformExpression expression)
            {
                int index = expressions.FindIndex(candidate => ReferenceEquals(candidate, expression));
                if (index >= 0) return index;
                expressions.Add(expression);
                return expressions.Count - 1;
            }

            static int FindOrAddTextureReference(List<MaterialUniformExpressionTexture> expressions,
                MaterialUniformExpressionTexture expression)
            {
                int index = expressions.FindIndex(candidate => ReferenceEquals(candidate, expression));
                if (index >= 0) return index;
                expressions.Add(expression);
                return expressions.Count - 1;
            }

            // Native Texture/TextureParameter create the uniform with no texture index; the global
            // texture identity is assigned when a sampler first accesses it (RegisterTextureUniform).
            MaterialUniformExpressionTexture CompileTextureUniform(int textureUIndex, bool isParameter, NameReference parameterName)
            {
                MaterialUniformExpressionTexture uniform = isParameter
                    ? uniformExpressionPool.OfType<MaterialUniformExpressionTextureParameter>()
                        .FirstOrDefault(candidate => candidate.ParameterName == parameterName && TextureUIndexOf(candidate) == textureUIndex)
                    : FindOrdinaryTextureUniform(textureUIndex);
                if (uniform != null) return uniform;
                uniform = isParameter
                    ? new MaterialUniformExpressionTextureParameter { ExpressionType = "FMaterialUniformExpressionTextureParameter", ParameterName = parameterName, TextureIndex = -1 }
                    : new MaterialUniformExpressionTexture { ExpressionType = "FMaterialUniformExpressionTexture", TextureIndex = -1 };
                unregisteredTextureUniforms.Add(uniform, textureUIndex);
                uniformExpressionPool.Add(uniform);
                return uniform;
            }

            int TextureUIndexOf(MaterialUniformExpressionTexture uniform) => uniform.TextureIndex >= 0
                ? uniformExpressionTextures[uniform.TextureIndex]
                : unregisteredTextureUniforms.TryGetValue(uniform, out int pending) ? pending : 0;

            MaterialUniformExpressionTexture FindOrdinaryTextureUniform(int textureUIndex) =>
                uniformExpressionPool.OfType<MaterialUniformExpressionTexture>()
                    .FirstOrDefault(candidate => candidate.GetType() == typeof(MaterialUniformExpressionTexture) &&
                        TextureUIndexOf(candidate) == textureUIndex);

            void RegisterTextureUniform(MaterialUniformExpressionTexture uniform)
            {
                if (uniform == null || !unregisteredTextureUniforms.Remove(uniform, out int textureUIndex)) return;
                int textureIndex = uniformExpressionTextures.IndexOf(textureUIndex);
                if (textureIndex < 0)
                {
                    textureIndex = uniformExpressionTextures.Count;
                    uniformExpressionTextures.Add(textureUIndex);
                }
                uniform.TextureIndex = textureIndex;
            }

            // Native ordinary Texture compile interns the uniform with no texture index. Code
            // access assigns the global texture identity first, then the first-use sampler slot.
            CodeValue CompileTextureObject(int textureUIndex, bool isCube)
            {
                var uniform = FindOrdinaryTextureUniform(textureUIndex);
                if (uniform == null)
                {
                    uniform = new MaterialUniformExpressionTexture { ExpressionType = "FMaterialUniformExpressionTexture", TextureIndex = -1 };
                    unregisteredTextureUniforms.Add(uniform, textureUIndex);
                    uniformExpressionPool.Add(uniform);
                }
                var samplers = isCube ? uniformCubeTextures : uniform2DTextures;
                string Access()
                {
                    RegisterTextureUniform(uniform);
                    return $"Texture{(isCube ? "Cube" : "2D")}_{FindOrAddTextureReference(samplers, uniform)}";
                }
                return new CodeValue(MaterialValueType.Vector4, "", false, AccessUniform: Access,
                    ExplicitNativeNumericType: isCube ? TextureValueCube : TextureValue2D);
            }

            CodeValue CompileCustom(ExportEntry expression, PropertyCollection properties, List<string> statements,
                Dictionary<(int UIndex, int OutputIndex), CodeValue> cache)
            {
                // Effective class defaults: Description "Custom", OutputType CMOT_Float3, no Code.
                string description = properties.GetProp<StrProperty>("Description")?.Value ?? "Custom";
                string code = properties.GetProp<StrProperty>("Code")?.Value ?? "";
                int outputType;
                switch (properties.GetProp<Property>("OutputType"))
                {
                    case null: outputType = 2; break;
                    case ByteProperty byteProperty: outputType = byteProperty.Value; break;
                    case EnumProperty { Value.Number: 0 } enumProperty when enumProperty.Value.Name is
                        "CMOT_Float1" or "CMOT_Float2" or "CMOT_Float3" or "CMOT_Float4" or "CMOT_MAX":
                        outputType = enumProperty.Value.Name switch
                        {
                            "CMOT_Float1" => 0, "CMOT_Float2" => 1, "CMOT_Float3" => 2, "CMOT_Float4" => 3, _ => 4
                        };
                        break;
                    default:
                        errors.Add($"{expression.InstancedFullPath} has an unrecognized OutputType.");
                        return null;
                }

                var arguments = new List<(string Name, CodeValue Value)>();
                var inputsProperty = properties.GetProp<Property>("Inputs");
                if (inputsProperty != null && inputsProperty is not ArrayProperty<StructProperty>)
                {
                    errors.Add($"{expression.InstancedFullPath}.Inputs is not a CustomInput array.");
                    return null;
                }
                if (inputsProperty is ArrayProperty<StructProperty> inputs)
                    for (int slot = 0; slot < inputs.Count; slot++)
                    {
                        // Only an empty string is skipped; whitespace remains an authored name.
                        string inputName = (inputs[slot].GetProp<Property>("InputName") as StrProperty)?.Value;
                        if (string.IsNullOrEmpty(inputName)) continue;
                        var input = inputs[slot].GetProp<Property>("Input") as StructProperty;
                        if (input?.GetProp<ObjectProperty>("Expression")?.Value is null or 0)
                        {
                            errors.Add($"Custom material {description} missing input {slot + 1} ({inputName})");
                            return null;
                        }
                        var argument = CompileInput(input, $"{expression.InstancedFullPath}.Inputs[{slot}]", statements, cache, allowTexture: true);
                        if (argument == null) return null;
                        arguments.Add((inputName, argument));
                    }

                // Every argument type is validated before any argument code is accessed, so a
                // rejected texture never acquires a global or sampler registration.
                var declarations = new List<string>(arguments.Count);
                foreach (var (name, argument) in arguments)
                {
                    string keyword = argument.NativeNumericType switch
                    {
                        1 or 0xf => "float", 2 => "float2", 4 => "float3", 8 => "float4", TextureValue2D => "sampler2D", _ => null
                    };
                    if (keyword == null)
                    {
                        string typeName = argument.NativeNumericType == TextureValueCube ? "textureCube" : $"type 0x{argument.NativeNumericType:x}";
                        errors.Add($"Bad type {typeName} for {description} input {name}");
                        return null;
                    }
                    declarations.Add($"{keyword} {name}");
                }

                (MaterialValueType resultType, string resultTypeName, int? nativeType) = outputType switch
                {
                    1 => (MaterialValueType.Vector2, "float2", (int?)null),
                    2 => (MaterialValueType.Vector3, "float3", null),
                    3 => (MaterialValueType.Vector4, "float4", null),
                    _ => (MaterialValueType.Scalar, "float", 1)
                };
                // Case-sensitive substring test, including comments and identifiers.
                if (!code.Contains("return", StringComparison.Ordinal)) code = $"return {code};";
                int customIndex = customExpressionImplementations.Count;
                customExpressionImplementations.Add($"{resultTypeName} CustomExpression{customIndex}({string.Join(",", declarations)})\r\n{{\r\n{code}\r\n}}\r\n");
                string call = $"CustomExpression{customIndex}({string.Join(",", arguments.Select(argument => argument.Value.Code))})";
                string local = $"Local{localIndex++}";
                statements.Add($"\t{DescribeType(resultType)} {local} = {call};\r\n");
                return new CodeValue(resultType, local, true, ExplicitNativeNumericType: nativeType);
            }

            CodeValue CompileFlipBookOffset(int textureUIndex)
            {
                if (compilingVertexProperty)
                { errors.Add("Invalid node used in vertex shader input!"); return null; }
                int textureIndex = uniformExpressionTextures.IndexOf(textureUIndex);
                if (textureIndex < 0)
                { textureIndex = uniformExpressionTextures.Count; uniformExpressionTextures.Add(textureUIndex); }
                var uniform = uniformExpressionPool.OfType<MaterialUniformExpressionFlipBookTextureParameter>()
                    .FirstOrDefault(candidate => candidate.TextureIndex == textureIndex);
                if (uniform == null)
                {
                    uniform = new MaterialUniformExpressionFlipBookTextureParameter
                    { ExpressionType = "FMaterialUniformExpressionFlipBookTextureParameter", TextureIndex = textureIndex };
                    uniformExpressionPool.Add(uniform);
                }
                // This is a numeric float4 leaf that also owns a global texture identity.
                return UniformValue(MaterialValueType.Vector4, "", uniform, isConstant: false);
            }

            IEntry[] ReadFontTexturePages(IEntry font, string source)
            {
                if (fontTexturePages.TryGetValue(font.UIndex, out var cached)) return cached;
                fontTexturePages.Add(font.UIndex, null);
                IEntry[] pages;
                if (getFontTexturePages != null)
                {
                    var provided = getFontTexturePages(font);
                    if (provided == null)
                    { errors.Add($"{source} has no font-page metadata for {font.InstancedFullPath}; the supplied font-page provider must resolve it."); return null; }
                    pages = provided.ToArray();
                }
                else
                {
                    if (font is not ExportEntry export || !export.IsDataLoaded())
                    { errors.Add($"{source} references imported or unloaded font {font.InstancedFullPath}; supply font-page metadata."); return null; }
                    var pageProperty = export.GetProperties().GetProp<Property>("Textures");
                    if (pageProperty != null && pageProperty is not ArrayProperty<ObjectProperty>)
                    { errors.Add($"{source} font {font.InstancedFullPath}.Textures must be an object-reference array."); return null; }
                    var references = pageProperty as ArrayProperty<ObjectProperty>;
                    pages = new IEntry[references?.Count ?? 0];
                    for (int i = 0; i < pages.Length; ++i)
                    {
                        int uIndex = references[i].Value;
                        if (uIndex == 0) continue;
                        if (!material.FileRef.TryGetEntry(uIndex, out pages[i]))
                        {
                            if (!invalidFontPageReferences.TryGetValue(font.UIndex, out var invalidPages))
                                invalidFontPageReferences.Add(font.UIndex, invalidPages = new HashSet<int>());
                            invalidPages.Add(i);
                        }
                    }
                }
                fontTexturePages[font.UIndex] = pages;
                return pages;
            }

            TextureCompilationMetadata ReadCompilationMetadata(IEntry texture, string source)
            {
                if (textureMetadata.TryGetValue(texture.UIndex, out var cached)) return cached;
                // Cache failures as well, so repeated nodes do not retry I/O through the provider.
                textureMetadata.Add(texture.UIndex, null);
                TextureDimension expected = texture.IsA("TextureCube") ? TextureDimension.TextureCube : TextureDimension.Texture2D;
                TextureCompilationMetadata metadata;
                if (getTextureMetadata != null)
                {
                    metadata = getTextureMetadata(texture);
                    if (metadata == null)
                    {
                        errors.Add($"{source} has no compilation metadata for texture {texture.InstancedFullPath}; the supplied metadata provider must resolve it.");
                        return null;
                    }
                }
                else
                {
                    if (texture.IsA("TextureRenderTarget2D") || texture.IsA("TextureMovie"))
                    {
                        errors.Add($"{source} references {texture.ClassName} {texture.InstancedFullPath}; supply texture compilation metadata.");
                        return null;
                    }
                    ExportEntry export = texture as ExportEntry;
                    if (texture is ImportEntry import)
                    {
                        export = resolveTextureImport?.Invoke(import);
                        if (export == null || !export.Game.IsLEGame3() ||
                            !export.IsA(expected == TextureDimension.TextureCube ? "TextureCube" : "Texture2D"))
                        {
                            errors.Add($"{source} references imported texture {texture.InstancedFullPath}; supply a resolved LE3 texture export or compilation metadata to read its unpack properties.");
                            return null;
                        }
                    }
                    if (export == null || !export.IsDataLoaded())
                    {
                        errors.Add($"{source} references unloaded texture {texture.InstancedFullPath}; supply texture compilation metadata.");
                        return null;
                    }
                    metadata = TextureCompilationMetadata.FromExport(export);
                }
                if (metadata.Dimension != expected)
                {
                    errors.Add($"{source} texture {texture.InstancedFullPath} requires {expected} metadata, but received {metadata.Dimension}.");
                    return null;
                }
                textureMetadata[texture.UIndex] = metadata;
                return metadata;
            }

            string ReadCoordinateSpace(PropertyCollection properties, string name, string defaultValue, ExportEntry expression)
            {
                var property = properties.FirstOrDefault(p => p.Name == name);
                if (property == null) return defaultValue;
                if (property is EnumProperty coordinate) return coordinate.Value.Name;
                errors.Add($"{expression.InstancedFullPath}.{name} must be an enum coordinate-space property.");
                return null;
            }

            string DescribeType(MaterialValueType type) =>
                type == MaterialValueType.Scalar ? numericType : numericType + (int)type;

            string CoerceScalarOperand(CodeValue value, MaterialValueType target) => value.Type == target
                ? value.Code
                : $"{DescribeType(target)}({string.Join(',', Enumerable.Repeat(value.Code, (int)target))})";

            string CoerceBound(CodeValue value, MaterialValueType target) => target == MaterialValueType.Scalar
                ? value.Code : CoerceScalarOperand(value, target);

            MaterialValueType? ArithmeticResultType(MaterialValueType a, MaterialValueType b, string source)
            {
                if (a == b) return a;
                if (a == MaterialValueType.Scalar) return b;
                if (b == MaterialValueType.Scalar) return a;
                errors.Add($"Arithmetic between {a} and {b} is undefined at {source}.");
                return null;
            }

            bool ReadInputMaskFlag(StructProperty input, string name)
            {
                // Native FExpressionInput stores these as INTs. Accept authored bool adapters too;
                // nonzero signed values are true, without a typed lookup that asserts on INTs.
                var property = input.GetProp<Property>(name);
                if (property == null) return false;
                if (property is IntProperty integer) return integer.Value != 0;
                if (property is BoolProperty boolean) return boolean.Value;
                errors.Add($"Expression input {input.Name}.{name} must be an integer or boolean mask flag.");
                return false;
            }

            CodeValue ApplyComponentMask(CodeValue value, StructProperty input, string source) =>
                ComponentMask(value,
                    ReadInputMaskFlag(input, "MaskR"),
                    ReadInputMaskFlag(input, "MaskG"),
                    ReadInputMaskFlag(input, "MaskB"),
                    ReadInputMaskFlag(input, "MaskA"), source);

            CodeValue ComponentMask(CodeValue value, bool r, bool g, bool b, bool a, string source)
            {
                int componentCount = (r ? 1 : 0) + (g ? 1 : 0) + (b ? 1 : 0) + (a ? 1 : 0);
                if (componentCount == 0)
                {
                    errors.Add($"Couldn't determine result type of component mask 0000 at {source}.");
                    return null;
                }

                int sourceComponents = (int)value.Type;
                if (value.Type != MaterialValueType.Scalar &&
                    (g && sourceComponents < 2 || b && sourceComponents < 3 || a && sourceComponents < 4))
                {
                    errors.Add($"Not enough components in {value.Type} for component mask " +
                               $"{(r ? 1 : 0)}{(g ? 1 : 0)}{(b ? 1 : 0)}{(a ? 1 : 0)} at {source}.");
                    return null;
                }

                var swizzle = new StringBuilder(4);
                if (r) swizzle.Append('r');
                if (g) swizzle.Append(value.Type == MaterialValueType.Scalar ? 'r' : 'g');
                if (b) swizzle.Append(value.Type == MaterialValueType.Scalar ? 'r' : 'b');
                if (a) swizzle.Append(value.Type == MaterialValueType.Scalar ? 'r' : 'a');
                // ComponentMask is an inline NONuniform chunk, even for a constant/uniform
                // source; it does not carry a folded expression object into later arithmetic.
                return new CodeValue((MaterialValueType)componentCount, value.Code + "." + swizzle, true);
            }
        }

        /// <summary>
        /// Applies known SM5 cache gates to an explicit registration list, then prepares provisional
        /// compile jobs. Resource gates for effects-owned materials require caller resolution.
        /// Unknown callbacks or conflicting metadata make the plan fail with no compilable jobs.
        /// </summary>
        /// <param name="materialResourceShouldCache">Optional caller-resolved resource predicate
        /// for the exact compilation input and permutation. Null results are unresolved. Without
        /// this callback, ordinary resources pass but effects-owned base materials are unresolved.
        /// This does not implement effects-owner logic or MaterialInstance parent resolution.</param>
        public ShaderCompilePlan BuildProvisionalCompilePlan(TranslationResult translation, MaterialResource resource,
            IReadOnlyList<ShaderCompileCandidate> candidates, ShouldCacheMaterialContext materialContext = null,
            ShouldCacheEnvironmentContext environmentContext = null,
            Func<ResolvedMaterialCompilationInput, ShaderCompileTarget, bool?> materialResourceShouldCache = null)
        {
            ArgumentNullException.ThrowIfNull(resource);
            ArgumentNullException.ThrowIfNull(candidates);
            ValidateTranslation(translation);
            if (material.ClassName is not ("Material" or "DecalMaterial"))
            {
                throw new NotSupportedException($"Compile planning for {material.ClassName} has not been verified in LE3.");
            }
            materialContext ??= BuildShouldCacheMaterialContext() with
                { NumUserTexCoords = translation.NumUserTexCoords, UsesDynamicParameter = translation.UsesDynamicParameter };

            var jobs = new List<ShaderCompileJob>();
            var excluded = new List<ShaderCompileTarget>();
            var errors = new List<string>();
            var decisions = new List<ShaderCompileDecision>();
            var identities = new HashSet<(string ShaderType, string VertexFactory)>();
            foreach (var candidate in candidates)
            {
                if (candidate?.Target == null)
                {
                    const string reason = "Every compile candidate must supply a shader target.";
                    errors.Add(reason);
                    decisions.Add(new(null, ShaderCompileDisposition.Unresolved, ShaderCompileGate.Registration, reason));
                    continue;
                }
                var target = candidate.Target;
                string identity = $"{target.ShaderTypeName} / {target.VertexFactoryTypeName ?? "non-mesh"}";
                ShaderCompileGate gate = ShaderCompileGate.Registration;
                try
                {
                    ValidateCompileTarget(target);
                    if (!TryGetKnownShaderTypeRegistration(target.ShaderTypeName, out var registration))
                    {
                        string reason = $"{identity}: shader-type registration is unresolved.";
                        errors.Add(reason);
                        decisions.Add(new(target, ShaderCompileDisposition.Unresolved, gate, reason));
                        continue;
                    }
                    if (target.Stage != registration.Stage || target.EntryPoint != registration.EntryPoint ||
                        (target.SourceFileName != registration.SourceFileName &&
                         target.SourceFileName != registration.SourceFileName + ".usf") ||
                        (target.VertexFactoryTypeName != null) != registration.IsMeshMaterial)
                    {
                        throw new ArgumentException("Source, entry point, stage or mesh classification conflicts with the recovered shader registration.");
                    }
                    if (target.VertexFactoryTypeName != null &&
                        TryGetKnownVertexFactoryRegistration(target.VertexFactoryTypeName, out var factory) &&
                        target.VertexFactoryShaderFileName != factory.SourceFileName &&
                        target.VertexFactoryShaderFileName != factory.SourceFileName + ".usf")
                    {
                        throw new ArgumentException("Vertex-factory source conflicts with its recovered registration.");
                    }
                    if (!identities.Add((target.ShaderTypeName, target.VertexFactoryTypeName)))
                    {
                        throw new ArgumentException("Duplicate shader-type/vertex-factory permutation.");
                    }
                    bool mesh = target.VertexFactoryTypeName != null;
                    if (mesh != (candidate.VertexFactoryContext != null))
                    {
                        throw new ArgumentException("Mesh candidates require a vertex-factory context; non-mesh candidates must omit it.");
                    }

                    // Native completeness ab3e400 and ShaderClient filter materials-capable VFs
                    // before the type -> material-resource -> vertex-factory cache gates.
                    if (mesh && !candidate.VertexFactoryContext.UsedWithMaterials)
                    {
                        excluded.Add(target);
                        decisions.Add(new(target, ShaderCompileDisposition.Excluded, ShaderCompileGate.VertexFactoryUsage,
                            "The vertex factory is not used with ordinary materials."));
                        continue;
                    }
                    if (!TryEvaluateKnownShaderTypeShouldCache(target.ShaderTypeName, materialContext,
                            target.VertexFactoryTypeName, candidate.VertexFactoryContext, out bool shaderTypeCaches, environmentContext))
                    {
                        string reason = $"{identity}: shader-type ShouldCache is unresolved.";
                        errors.Add(reason);
                        decisions.Add(new(target, ShaderCompileDisposition.Unresolved, ShaderCompileGate.ShaderTypeShouldCache, reason));
                        continue;
                    }
                    if (!shaderTypeCaches)
                    {
                        excluded.Add(target);
                        decisions.Add(new(target, ShaderCompileDisposition.Excluded, ShaderCompileGate.ShaderTypeShouldCache,
                            "The shader-type ShouldCache predicate returned false."));
                        continue;
                    }
                    // ab56d40 delegates to an effects owner (Outer IsA RvrEffectsMaterialUser) via
                    // owner slot 127 (0x7ff74abdda10); otherwise the resource gate is true.
                    gate = ShaderCompileGate.MaterialResourceShouldCache;
                    bool? resourceCaches = materialResourceShouldCache != null
                        ? materialResourceShouldCache(CompilationInput, target)
                        : material.Parent?.IsA("RvrEffectsMaterialUser") == true ? EffectsOwnerShouldCache(target) : true;
                    if (resourceCaches == null)
                    {
                        string reason = $"{identity}: material-resource ShouldCache is unresolved.";
                        errors.Add(reason);
                        decisions.Add(new(target, ShaderCompileDisposition.Unresolved, gate, reason));
                        continue;
                    }
                    if (!resourceCaches.Value)
                    {
                        excluded.Add(target);
                        decisions.Add(new(target, ShaderCompileDisposition.Excluded, gate,
                            "The material-resource ShouldCache predicate returned false."));
                        continue;
                    }
                    if (mesh)
                    {
                        if (!TryEvaluateKnownVertexFactoryShouldCache(target.VertexFactoryTypeName, materialContext,
                                candidate.VertexFactoryContext, out bool vertexFactoryCaches, environmentContext,
                                target.ShaderTypeName, registration.SourceFileName))
                        {
                            string reason = $"{identity}: vertex-factory ShouldCache is unresolved.";
                            errors.Add(reason);
                            decisions.Add(new(target, ShaderCompileDisposition.Unresolved, ShaderCompileGate.VertexFactoryShouldCache, reason));
                            continue;
                        }
                        if (!vertexFactoryCaches)
                        {
                            excluded.Add(target);
                            decisions.Add(new(target, ShaderCompileDisposition.Excluded, ShaderCompileGate.VertexFactoryShouldCache,
                                "The vertex-factory ShouldCache predicate returned false."));
                            continue;
                        }
                    }
                    gate = ShaderCompileGate.CompilationEnvironment;
                    if (!candidate.IsCompilationEnvironmentResolved)
                    {
                        string reason = $"{identity}: compilation environment is unresolved.";
                        errors.Add(reason);
                        decisions.Add(new(target, ShaderCompileDisposition.Unresolved, gate, reason));
                        continue;
                    }
                    jobs.Add(BuildProvisionalCompileJob(translation, resource, target, candidate.EnvironmentMacros));
                    decisions.Add(new(target, ShaderCompileDisposition.Eligible, gate,
                        "The cache gates passed and a provisional compile job was prepared."));
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    string reason = $"{identity}: {ex.Message}";
                    errors.Add(reason);
                    decisions.Add(new(target, ShaderCompileDisposition.Unresolved, gate, reason));
                }
            }

            return new ShaderCompilePlan
            {
                Translation = translation,
                CompilationInput = CompilationInput,
                Jobs = errors.Count == 0 ? jobs.AsReadOnly() : Array.Empty<ShaderCompileJob>(),
                ExcludedTargets = excluded.AsReadOnly(),
                Errors = errors.AsReadOnly(),
                Decisions = decisions.AsReadOnly()
            };
        }

        /// <summary>
        /// Plans every shader/VF pair in the recovered registry, using its evidence-labelled environment.
        /// The registry is closed for this binary (all materials-capable factories and material types), so
        /// in the shipping context the plan reports CoversNativeRequiredSet; flags/bytecode remain separate.
        /// </summary>
        public ShaderCompilePlan BuildKnownSubsetCompilePlan(TranslationResult translation, MaterialResource resource,
            ShouldCacheEnvironmentContext environmentContext = null,
            Func<ResolvedMaterialCompilationInput, ShaderCompileTarget, bool?> materialResourceShouldCache = null)
            => BuildRegisteredSubsetCompilePlan(translation, resource, GetKnownVertexFactoryRegistrations(),
                environmentContext, materialResourceShouldCache);

        /// <summary>
        /// Plans against the observed static factory inventory, including known non-material and
        /// always-false registrations. Missing environments may resolve as exclusions, never jobs.
        /// Success covers this observed inventory only, not live engine completeness or installation.
        /// This does not resolve derived terrain resources or MaterialInstance parent chains.
        /// </summary>
        public ShaderCompilePlan BuildObservedSubsetCompilePlan(TranslationResult translation, MaterialResource resource,
            ShouldCacheEnvironmentContext environmentContext = null,
            Func<ResolvedMaterialCompilationInput, ShaderCompileTarget, bool?> materialResourceShouldCache = null)
            => BuildRegisteredSubsetCompilePlan(translation, resource, GetObservedVertexFactoryRegistrations(),
                environmentContext, materialResourceShouldCache);

        private ShaderCompilePlan BuildRegisteredSubsetCompilePlan(TranslationResult translation, MaterialResource resource,
            IReadOnlyList<VertexFactoryRegistration> factories, ShouldCacheEnvironmentContext environmentContext,
            Func<ResolvedMaterialCompilationInput, ShaderCompileTarget, bool?> materialResourceShouldCache)
        {
            ArgumentNullException.ThrowIfNull(resource);
            ValidateTranslation(translation);
            // FMaterial::Compile ab49d90 enables bUseHalfTypes only for SM_PS3 when !ForceFloat.
            // SM5 platform5 always passes false; h4tex* intrinsics are absent from its USF headers.
            if (translation.UsesHalfTypes) return new ShaderCompilePlan
            {
                Translation = translation, CompilationInput = CompilationInput, Jobs = Array.Empty<ShaderCompileJob>(),
                ExcludedTargets = Array.Empty<ShaderCompileTarget>(), Decisions = Array.Empty<ShaderCompileDecision>(),
                Errors = Array.AsReadOnly(new[] { "Known LE3 SM5 planning requires float HLSL; half types are the native PS3-only branch." })
            };
            var candidates = new List<ShaderCompileCandidate>();
            foreach (var shader in GetKnownShaderTypeRegistrations())
            {
                if (shader.IsMeshMaterial)
                {
                    foreach (var factory in factories) AddCandidate(shader, factory);
                }
                else AddCandidate(shader, null);
            }
            void AddCandidate(ShaderTypeRegistration shader, VertexFactoryRegistration factory)
            {
                if (TryGetKnownPermutationEnvironment(shader.Name, factory?.Name, out var environment))
                    candidates.Add(environment.CreateCandidate());
                else if (factory != null && !TryGetKnownVertexFactoryRegistration(factory.Name, out _))
                {
                    // Metadata-only target for cache evaluation; zero flags are not a recovered
                    // compile profile. The explicit unresolved marker prohibits preparing a job.
                    candidates.Add(new(shader.CreateTarget(0, factory.Name, factory.SourceFileName),
                        VertexFactoryContext: factory.CreateCacheContext(), IsCompilationEnvironmentResolved: false));
                }
                else throw new InvalidOperationException($"Missing recovered environment for {factory?.Name ?? "non-mesh"}/{shader.Name}.");
            }
            var plan = BuildProvisionalCompilePlan(translation, resource, candidates, environmentContext: environmentContext,
                materialResourceShouldCache: materialResourceShouldCache);
            // LE3 AllowDebugViewmodes (0x7ff74abfd600) is TRUE for SM5 in the PC client. A caller
            // resource predicate is outside the verified native evidence, so it is not certified.
            // The legacy authored-root translation omits resource inputs, so only resource translations qualify.
            bool nativeContext = (environmentContext?.AllowsDebugViewModes ?? true) && materialResourceShouldCache == null &&
                translation.IncludesMaterialResourceInputs;
            return new ShaderCompilePlan
            {
                Translation = plan.Translation, CompilationInput = plan.CompilationInput, Jobs = plan.Jobs,
                ExcludedTargets = plan.ExcludedTargets, Errors = plan.Errors, Decisions = plan.Decisions,
                CoversNativeRequiredSet = plan.Success && nativeContext
            };
        }

        /// <summary>
        /// Checks selected jobs, not individually eligible decisions from a failed plan. Unknown
        /// expected shader/VF names are preserved as missing rather than silently filtered through
        /// the recovered registry. Duplicate or malformed inventories fail the comparison.
        /// </summary>
        public static ShaderPermutationComparison ComparePlannedPermutations(ShaderCompilePlan plan,
            IReadOnlyList<ShaderPermutationIdentity> expectedPermutations)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(expectedPermutations);
            var errors = new List<string>();
            var expected = new List<ShaderPermutationIdentity>();
            var planned = new List<ShaderPermutationIdentity>();
            var expectedSet = new HashSet<ShaderPermutationIdentity>();
            var plannedSet = new HashSet<ShaderPermutationIdentity>();

            bool planSucceeded = plan.Errors != null && plan.Success && plan.Jobs != null;
            if (!planSucceeded)
                errors.Add("A successful shader compile plan with selected jobs is required for permutation comparison.");
            for (int i = 0; i < expectedPermutations.Count; i++)
                Add(expectedPermutations[i], $"Expected permutation {i}", expected, expectedSet);
            if (plan.Jobs != null)
                for (int i = 0; i < plan.Jobs.Count; i++)
                {
                    var target = plan.Jobs[i]?.Target;
                    Add(target == null ? null : new(target.ShaderTypeName, target.VertexFactoryTypeName),
                        $"Planned permutation {i}", planned, plannedSet);
                }
            return new ShaderPermutationComparison
            {
                PlanSucceeded = planSucceeded,
                ExpectedPermutationCount = expected.Count, PlannedPermutationCount = planned.Count,
                MissingPermutations = Array.AsReadOnly(expected.Where(p => !plannedSet.Contains(p)).ToArray()),
                UnexpectedPermutations = Array.AsReadOnly(planned.Where(p => !expectedSet.Contains(p)).ToArray()),
                Errors = errors.AsReadOnly()
            };

            void Add(ShaderPermutationIdentity permutation, string source, List<ShaderPermutationIdentity> ordered,
                HashSet<ShaderPermutationIdentity> unique)
            {
                if (permutation == null || string.IsNullOrWhiteSpace(permutation.ShaderTypeName) ||
                    permutation.VertexFactoryTypeName != null && string.IsNullOrWhiteSpace(permutation.VertexFactoryTypeName))
                {
                    errors.Add($"{source} requires a shader-type name and a null or nonempty vertex-factory name.");
                    return;
                }
                if (!unique.Add(permutation))
                {
                    errors.Add($"{source} duplicates {permutation.ShaderTypeName} / {permutation.VertexFactoryTypeName ?? "non-mesh"}.");
                    return;
                }
                ordered.Add(permutation);
            }
        }

        /// <summary>
        /// Compares selected plan permutations against references in an independently supplied map.
        /// This reads only map membership, not shader payloads or packages. A matching stored map is
        /// not proof of the native required set, matching material/static identity, bindings or code.
        /// Malformed references and duplicate factory maps fail the comparison rather than vanish.
        /// </summary>
        public static ShaderPermutationComparison ComparePlannedPermutationsWithShaderMap(ShaderCompilePlan plan,
            MaterialShaderMap expectedMap)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(expectedMap);
            var inventory = new List<ShaderPermutationIdentity>();
            var errors = new List<string>();
            AddReferences(expectedMap.Shaders, null, "MaterialShaderMap.Shaders");
            if (expectedMap.MeshShaderMaps == null)
                errors.Add("MaterialShaderMap.MeshShaderMaps must be supplied, even when empty.");
            else
            {
                var factories = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < expectedMap.MeshShaderMaps.Length; i++)
                {
                    var mesh = expectedMap.MeshShaderMaps[i];
                    string location = $"MaterialShaderMap.MeshShaderMaps[{i}]";
                    if (mesh == null)
                    {
                        errors.Add($"{location} cannot be null.");
                        continue;
                    }
                    string factory = ReadName(mesh.VertexFactoryType, location + ".VertexFactoryType");
                    if (string.IsNullOrWhiteSpace(factory))
                    {
                        errors.Add($"{location} requires a nonempty vertex-factory name.");
                        continue;
                    }
                    if (!factories.Add(factory)) errors.Add($"{location} duplicates vertex factory {factory}.");
                    AddReferences(mesh.Shaders, factory, location + ".Shaders");
                }
            }
            var comparison = ComparePlannedPermutations(plan, inventory);
            return new ShaderPermutationComparison
            {
                PlanSucceeded = comparison.PlanSucceeded,
                ExpectedPermutationCount = comparison.ExpectedPermutationCount,
                PlannedPermutationCount = comparison.PlannedPermutationCount,
                MissingPermutations = comparison.MissingPermutations,
                UnexpectedPermutations = comparison.UnexpectedPermutations,
                Errors = Array.AsReadOnly(comparison.Errors.Concat(errors).ToArray())
            };

            void AddReferences(UMultiMap<NameReference, ShaderReference> references, string factory, string location)
            {
                if (references == null)
                {
                    errors.Add($"{location} must be supplied, even when empty.");
                    return;
                }
                int index = 0;
                foreach (var pair in references)
                {
                    string entry = $"{location}[{index++}]";
                    string name = ReadName(pair.Key, entry + ".Key");
                    // Preserve even unknown map keys for missing-permutation diagnostics; do not
                    // silently filter a captured inventory through the compiler's registration list.
                    inventory.Add(new(name, factory));
                    if (pair.Value == null)
                        errors.Add($"{entry} has a null shader reference.");
                    else
                    {
                        if (pair.Value.Id == Guid.Empty) errors.Add($"{entry} has an empty shader reference ID.");
                        if (!string.Equals(name, ReadName(pair.Value.ShaderType, entry + ".ShaderType"), StringComparison.Ordinal))
                            errors.Add($"{entry} key and shader-reference type disagree.");
                    }
                }
            }

            string ReadName(NameReference name, string location)
            {
                if (string.IsNullOrWhiteSpace(name.Name) || name.Number < 0)
                {
                    errors.Add($"{location} requires a nonempty name and a nonnegative instance number.");
                    return name.Name;
                }
                return name.Instanced;
            }
        }

        /// <summary>
        /// Compares successful translation uniforms with an independently supplied map, retaining
        /// stage, array and ordinal distinctions. Texture indices compare only stored slots; no
        /// texture properties, resource UIndexes or native transient texture pointers are read.
        /// Unsupported or malformed trees fail closed, including cycles and excessive nesting.
        /// This optional diagnostic does not enable shader installation or completeness claims.
        /// </summary>
        public static UniformExpressionComparison CompareUniformExpressionsWithShaderMap(TranslationResult translation,
            MaterialShaderMap expectedMap)
            => LE3UniformExpressionComparer.Compare(translation, expectedMap);

        /// <summary>
        /// Combines translated material HLSL, provisional material macros, and verified shader-type
        /// registration data into one compiler-independent job. Duplicate macros must agree exactly;
        /// conflicts are rejected instead of relying on preprocessor definition order.
        /// </summary>
        // URvrEffectsMaterialUser slot 127 (0x7ff74abdda10) ignores platform and shader type: it rejects
        // only the exact FLocalVertexFactory, and only when the owner's m_bSupportsStaticMeshes is false.
        private bool? EffectsOwnerShouldCache(ShaderCompileTarget target)
        {
            if (material.Parent is not ExportEntry owner) return null;
            if (target.VertexFactoryTypeName != "FLocalVertexFactory") return true;
            return owner.GetProperties().GetProp<BoolProperty>("m_bSupportsStaticMeshes")?.Value == true;
        }

        public ShaderCompileJob BuildProvisionalCompileJob(TranslationResult translation, MaterialResource resource,
            ShaderCompileTarget target, IReadOnlyDictionary<string, string> shaderMacros = null)
        {
            ArgumentNullException.ThrowIfNull(resource);
            ValidateTranslation(translation);
            ValidateCompileTarget(target);
            if (translation.UsesHalfTypes)
                throw new InvalidOperationException("LE3 SM5 compile jobs require float HLSL; half types are the native PS3-only branch.");

            var macros = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                // ShaderClient applies these before the registration-specific environment.
                ["SM5_PROFILE"] = "1",
                [target.Stage == ShaderStage.Vertex ? "VERTEXSHADER" : "PIXELSHADER"] = "1"
            };
            foreach (var pair in BuildProvisionalMaterialMacros(resource, translation))
            {
                macros.Add(pair.Key, pair.Value);
            }
            // ShaderClient AddCustomMacros conditions this VS-only custom-simple texture flag on
            // the material getter; the PS correction instead uses USING_SIMPLELIGHTMAPS/TEXTURE_LIGHTMAP.
            if (target.ShaderTypeName == "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFNoDensityPolicy" &&
                BuildShouldCacheMaterialContext().UsedWithSimpleLightMaps)
                macros.Add("SIMPLE_TEXTURE_LIGHTMAP", "1");
            if (shaderMacros != null)
            {
                foreach (var pair in shaderMacros)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                    {
                        throw new ArgumentException("Shader macro names must be nonempty and definitions cannot be null.", nameof(shaderMacros));
                    }
                    if (macros.TryGetValue(pair.Key, out string existing) && existing != pair.Value)
                    {
                        throw new InvalidOperationException(
                            $"Shader macro {pair.Key} conflicts with an existing definition ({existing} versus {pair.Value}).");
                    }
                    macros[pair.Key] = pair.Value;
                }
            }

            if (KnownFoggedBasePassVertexTypes.TryGetValue(target.ShaderTypeName, out var foggedPolicy))
            {
                var context = BuildShouldCacheMaterialContext();
                // ShaderClient material-conditioned overrides follow its type-only macro map.
                if (foggedPolicy.LightmapPolicy == "FCustomSimpleLightMapTexturePolicy" && context.UsedWithSimpleLightMaps)
                {
                    macros["SIMPLE_TEXTURE_LIGHTMAP"] = "1";
                    macros["NUM_LIGHTMAP_COEFFICIENTS"] = "1";
                }
                if (foggedPolicy.LightmapPolicy == "FCustomVectorVertexLightMapPolicy" && context.UsedWithVectorLightMaps)
                {
                    macros["USING_VECTORLIGHTMAPS"] = "1";
                    macros["VERTEX_LIGHTMAP"] = "1";
                }
            }

            // ShaderClient's final policy cleanup runs after both factory and material overrides.
            // These dynamic/no-light policies cannot inherit a factory's static-lightmap fallback.
            if (target.ShaderTypeName.Contains("FNoLightMapPolicy", StringComparison.Ordinal) ||
                target.ShaderTypeName.Contains("FDirectionalLightLightMapPolicy", StringComparison.Ordinal) ||
                target.ShaderTypeName.Contains("FSHLightLightMapPolicy", StringComparison.Ordinal))
            {
                macros.Remove("SIMPLE_VERTEX_LIGHTMAP");
                macros.Remove("TEXTURE_LIGHTMAP");
            }

            return new ShaderCompileJob
            {
                Translation = translation,
                CompilationInput = CompilationInput,
                Target = target,
                MaterialHlsl = translation.MaterialHlsl,
                Macros = new ReadOnlyDictionary<string, string>(macros)
            };
        }

        private void ValidateTranslation(TranslationResult translation)
        {
            ArgumentNullException.ThrowIfNull(translation);
            if (translation.Errors == null || !translation.Success)
            {
                throw new InvalidOperationException("Cannot build a shader compile job from a failed material translation.");
            }
            if (!ReferenceEquals(translation.Compiler, this))
            {
                throw new ArgumentException("The translation was produced for a different material compiler instance.", nameof(translation));
            }
        }

        private static void ValidateCompileTarget(ShaderCompileTarget target)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (string.IsNullOrWhiteSpace(target.ShaderTypeName) || string.IsNullOrWhiteSpace(target.SourceFileName) ||
                string.IsNullOrWhiteSpace(target.EntryPoint))
            {
                throw new ArgumentException("Shader type, source file, and entry point are required.", nameof(target));
            }

            bool hasVertexFactoryType = !string.IsNullOrWhiteSpace(target.VertexFactoryTypeName);
            bool hasVertexFactorySource = !string.IsNullOrWhiteSpace(target.VertexFactoryShaderFileName);
            if (hasVertexFactoryType != hasVertexFactorySource)
            {
                throw new ArgumentException("Vertex-factory type and shader source must either both be supplied or both be absent.", nameof(target));
            }
            if (!Enum.IsDefined(target.Stage))
            {
                throw new ArgumentException("Only LE3 SM5 vertex and pixel stages are supported.", nameof(target));
            }
            if ((!hasVertexFactoryType && target.VertexFactoryTypeName != null) ||
                (!hasVertexFactorySource && target.VertexFactoryShaderFileName != null))
            {
                throw new ArgumentException("Absent vertex-factory metadata must be null, not empty or whitespace.", nameof(target));
            }
        }

        /// <summary>
        /// Compiles one fully prepared LE3 job with D3DCompile. Material.usf is supplied from the
        /// translated job, VertexFactory.usf is redirected to the target's registered factory source,
        /// and all other includes are read from <paramref name="shaderDirectory"/>.
        /// </summary>
        public ShaderCompilationResult CompileShader(ShaderCompileJob job, string shaderDirectory)
        {
            ArgumentNullException.ThrowIfNull(job);
            if (job.Translation?.SourceSnapshot != null)
                throw new InvalidOperationException("Snapshot translations must compile against their original source snapshot.");
            ArgumentException.ThrowIfNullOrWhiteSpace(shaderDirectory);
            if (!Directory.Exists(shaderDirectory))
            {
                throw new DirectoryNotFoundException($"LE3 shader directory does not exist: {shaderDirectory}");
            }

            string baseShaderPath = ResolveShaderPath(shaderDirectory, job.Target.SourceFileName);
            string shaderSource = File.ReadAllText(baseShaderPath);
            using var include = new LE3ShaderInclude(shaderDirectory, job.MaterialHlsl,
                job.Target.VertexFactoryShaderFileName);
            return CompileShaderCore(job, Encoding.UTF8.GetBytes(shaderSource), Path.GetFileName(baseShaderPath), include, null);
        }

        /// <summary>Compiles from the exact immutable source set used by translation. Physical
        /// sources are decoded once, then sent as UTF8 to D3D; virtual material/VF routing is unchanged.</summary>
        public ShaderCompilationResult CompileShader(ShaderCompileJob job, LE3ShaderSourceSnapshot sources)
        {
            ArgumentNullException.ThrowIfNull(job);
            ArgumentNullException.ThrowIfNull(sources);
            if (!ReferenceEquals(job.Translation?.SourceSnapshot, sources))
                throw new InvalidOperationException("Compile job must originate from this exact source snapshot.");
            using var include = new LE3ShaderInclude(sources, job.MaterialHlsl, job.Target.VertexFactoryShaderFileName);
            return CompileShaderCore(job, Encoding.UTF8.GetBytes(sources.ReadSource(job.Target.SourceFileName)),
                Path.ChangeExtension(job.Target.SourceFileName, ".usf"), include, sources);
        }

        private static ShaderCompilationResult CompileShaderCore(ShaderCompileJob job, byte[] shaderSource,
            string sourceName, LE3ShaderInclude include, LE3ShaderSourceSnapshot sources)
        {
            var defines = job.Macros.Select(pair => new global::SharpDX.Direct3D.ShaderMacro(pair.Key, pair.Value)).ToArray();
            try
            {
                using var result = global::SharpDX.D3DCompiler.ShaderBytecode.Compile(
                    shaderSource,
                    job.Target.EntryPoint,
                    job.Target.Profile,
                    (global::SharpDX.D3DCompiler.ShaderFlags)job.Target.CompileFlags,
                    global::SharpDX.D3DCompiler.EffectFlags.None,
                    defines,
                    include,
                    sourceName);
                byte[] bytecode = result.Bytecode?.Data;
                int instructionCount = 0;
                IReadOnlyList<ShaderParameterBinding> parameterBindings = [];
                if (bytecode is { Length: > 0 })
                {
                    parameterBindings = ReflectShaderBytecode(bytecode, out instructionCount);
                }
                return new ShaderCompilationResult
                {
                    SourceSnapshot = sources,
                    Translation = job.Translation,
                    CompilationInput = job.CompilationInput,
                    Target = job.Target,
                    Bytecode = bytecode,
                    Diagnostics = result.Message,
                    InstructionCount = instructionCount,
                    ParameterBindings = parameterBindings
                };
            }
            catch (global::SharpDX.CompilationException exception)
            {
                return new ShaderCompilationResult
                {
                    SourceSnapshot = sources,
                    Translation = job.Translation,
                    CompilationInput = job.CompilationInput,
                    Target = job.Target,
                    Bytecode = null,
                    Diagnostics = exception.Message
                };
            }
        }

        /// <summary>
        /// Constructs recovered basepass, depth/shadow-depth, fog, distortion and light shader objects,
        /// using the explicitly supported vertex-factory layouts.
        /// Uses the exact translation that supplied the compiled job's uniform-expression layout.
        /// This performs no package writes and does not certify shader-set completeness or runtime parity.
        /// </summary>
        public ShaderConstructionResult ConstructKnownShader(TranslationResult translation, ShaderCompilationResult compiledShader)
        {
            ValidateTranslation(translation);
            ArgumentNullException.ThrowIfNull(compiledShader);
            if (!ReferenceEquals(compiledShader.Translation, translation) ||
                !ReferenceEquals(compiledShader.CompilationInput, CompilationInput))
                throw new ArgumentException("The compiled shader does not belong to this exact material translation.", nameof(compiledShader));
            if (!compiledShader.Success) return Failure("Cannot construct an LE3 shader from a failed bytecode compilation.");
            var target = compiledShader.Target;
            if (!TryGetKnownShaderTypeRegistration(target.ShaderTypeName, out var registration) ||
                target.SourceFileName != registration.SourceFileName || target.EntryPoint != registration.EntryPoint ||
                target.Stage != registration.Stage || (target.VertexFactoryTypeName != null) != registration.IsMeshMaterial)
                return Failure("Shader-object construction requires exact recovered shader registration metadata.");
            VertexFactoryRegistration factory = null;
            if (registration.IsMeshMaterial &&
                (!TryGetKnownVertexFactoryRegistration(target.VertexFactoryTypeName, out factory) ||
                 target.VertexFactoryShaderFileName != factory.SourceFileName) ||
                !registration.IsMeshMaterial && target.VertexFactoryShaderFileName != null)
                return Failure("Shader-object construction requires exact recovered shader and vertex-factory registration metadata.");

            var binder = new LE3ShaderParameterBinder(compiledShader.ParameterBindings);
            Shader shader;
            try
            {
                switch (target.ShaderTypeName)
                {
                    case "FLightFunctionVertexShader":
                        shader = new FLightFunctionVertexShader();
                        break;
                    case "FLightFunctionPixelShader":
                        // aaf2200 explicitly passes optional=1 for all five scene/light fields.
                        shader = new FLightFunctionPixelShader
                        {
                            SceneTextureParameters = binder.BindSceneTextureParameters(),
                            ScreenToLight = binder.BindConstant("ScreenToLight"),
                            MaterialParameters = binder.BindMaterialPixelParameters(translation)
                        };
                        break;
                    case "FRadialBlurVertexShader":
                        shader = new FRadialBlurVertexShader { WorldCenterPos = binder.BindConstant("WorldCenterPos") };
                        break;
                    case "FRadialBlurPixelShader":
                    case "FRadialBlurVelocityPixelShader":
                        shader = new FRadialBlurPixelShader
                        {
                            RadialBlurScale = binder.BindConstant("RadialBlurScale"),
                            RadialBlurFalloffExp = binder.BindConstant("RadialBlurFalloffExp"),
                            RadialBlurOpacity = binder.BindConstant("RadialBlurOpacity"),
                            SceneTextureParameters = binder.BindSceneTextureParameters(),
                            MaterialParameters = binder.BindMaterialPixelParameters(translation)
                        };
                        break;
                    case "TLightVertexShaderFSFXPointLightPolicyFNoStaticShadowingPolicy":
                        shader = CreateLightVertexShader(() => new FSFXPointLightPolicy.VertexParametersType
                        {
                            LightPositionAndInvRadius = binder.BindConstant("LightPositionAndInvRadius", optional: false),
                            ShadowViewProjection = binder.BindConstant("ShadowViewProjection")
                        }, () => new FNullPolicy());
                        break;
                    case "TLightPixelShaderFSFXPointLightPolicyFNoStaticShadowingPolicy":
                        shader = CreateLightPixelShader(() => new FSFXPointLightPolicy.PixelParametersType
                        {
                            LightSpaceShadowMap = binder.BindResource("LightSpaceShadowMap"),
                            LightColorAndFalloffExponent = binder.BindConstant("LightColorAndFalloffExponent"),
                            ShadowFilter = binder.BindConstant("ShadowFilter"),
                            ShadowTextureRegion = binder.BindConstant("ShadowTextureRegion"),
                            MaxVarianceShadowAttenuation = binder.BindConstant("MaxVarianceShadowAttenuation")
                        }, () => new FNullPolicy());
                        break;
                    case "TLightVertexShaderFSphericalHarmonicLightPolicyFNoStaticShadowingPolicy":
                    case "TLightVertexShaderFSphericalHarmonicLightPolicyFShadowVertexBufferPolicy":
                        shader = CreateLightVertexShader(() => new FNullPolicy(), () => new FNullPolicy());
                        break;
                    case "TLightPixelShaderFSphericalHarmonicLightPolicyFNoStaticShadowingPolicy":
                    case "TLightPixelShaderFSphericalHarmonicLightPolicyFShadowVertexBufferPolicy":
                        shader = CreateLightPixelShader(() => new FSphericalHarmonicLightPolicy.PixelParametersType
                            { WorldIncidentLighting = binder.BindConstant("WorldIncidentLighting") }, () => new FNullPolicy());
                        break;
                    case "TLightVertexShaderFSphericalHarmonicLightPolicyFShadowTexturePolicy":
                    case "TLightVertexShaderFSphericalHarmonicLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                        // Folded b05860: VF, optional coordinate scale/bias, then material VS.
                        shader = CreateLightVertexShader(() => new FNullPolicy(), binder.BindShadowTextureVertexParameters);
                        break;
                    case "TLightPixelShaderFSphericalHarmonicLightPolicyFShadowTexturePolicy":
                        // ac26c40: optional SH lighting and texture shadow policy precede material PS.
                        shader = CreateLightPixelShader(() => new FSphericalHarmonicLightPolicy.PixelParametersType
                            { WorldIncidentLighting = binder.BindConstant("WorldIncidentLighting") }, binder.BindShadowTexturePixelParameters);
                        break;
                    case "TLightPixelShaderFSphericalHarmonicLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                        // ac26d90 adds optional distance-field parameters before its shadow texture.
                        shader = CreateLightPixelShader(() => new FSphericalHarmonicLightPolicy.PixelParametersType
                            { WorldIncidentLighting = binder.BindConstant("WorldIncidentLighting") }, binder.BindSignedDistanceFieldShadowPixelParameters);
                        break;
                    case "TLightMapDensityVertexShader<FNoLightMapPolicy>":
                        shader = CreateLightMapDensityVertexShader(() => new FNullPolicy());
                        break;
                    case "TLightMapDensityVertexShader<FDirectionalLightMapTexturePolicy>":
                    case "TLightMapDensityVertexShader<FSimpleLightMapTexturePolicy>":
                    case "TLightMapDensityVertexShader<FDummyLightMapTexturePolicy>":
                        // All three initializers install b05860: VF, optional scale/bias, material.
                        shader = CreateLightMapDensityVertexShader(() => new FLightMapTexturePolicy.VertexParametersType
                            { LightmapCoordinateScaleBias = binder.BindConstant("LightmapCoordinateScaleBias") });
                        break;
                    case "TLightMapDensityPixelShader<FNoLightMapPolicy>":
                        shader = CreateLightMapDensityPixelShader(new FNullPolicy());
                        break;
                    case "TLightMapDensityPixelShader<FDirectionalLightMapTexturePolicy>":
                    case "TLightMapDensityPixelShader<FSimpleLightMapTexturePolicy>":
                    case "TLightMapDensityPixelShader<FDummyLightMapTexturePolicy>":
                        // Shared b05490: both texture-policy allocations optional, before material.
                        shader = CreateLightMapDensityPixelShader(new FLightMapTexturePolicy.PixelParametersType
                        {
                            LightMapTextures = binder.BindResource("LightMapTextures"),
                            LightMapScale = binder.BindConstant("LightMapScale")
                        });
                        break;
                    // Native registration pointers establish shared no-static/vertex-mask bodies.
                    case "TLightVertexShaderFDirectionalLightPolicyFNoStaticShadowingPolicy":
                    case "TLightVertexShaderFDirectionalLightPolicyFShadowVertexBufferPolicy":
                        shader = CreateLightVertexShader(binder.BindDirectionalLightVertexParameters, () => new FNullPolicy());
                        break;
                    case "TLightVertexShaderFPointLightPolicyFNoStaticShadowingPolicy":
                    case "TLightVertexShaderFPointLightPolicyFShadowVertexBufferPolicy":
                        shader = CreateLightVertexShader(binder.BindPointLightVertexParameters, () => new FNullPolicy());
                        break;
                    case "TLightVertexShaderFSpotLightPolicyFNoStaticShadowingPolicy":
                    case "TLightVertexShaderFSpotLightPolicyFShadowVertexBufferPolicy":
                        shader = CreateLightVertexShader(binder.BindSpotLightVertexParameters, () => new FNullPolicy());
                        break;
                    // SDF VS registrations fold to the texture-mask body; same serialized fields.
                    case "TLightVertexShaderFDirectionalLightPolicyFShadowTexturePolicy":
                    case "TLightVertexShaderFDirectionalLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                        shader = CreateLightVertexShader(binder.BindDirectionalLightVertexParameters, binder.BindShadowTextureVertexParameters);
                        break;
                    case "TLightVertexShaderFPointLightPolicyFShadowTexturePolicy":
                    case "TLightVertexShaderFPointLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                        shader = CreateLightVertexShader(binder.BindPointLightVertexParameters, binder.BindShadowTextureVertexParameters);
                        break;
                    case "TLightVertexShaderFSpotLightPolicyFShadowTexturePolicy":
                    case "TLightVertexShaderFSpotLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                        shader = CreateLightVertexShader(binder.BindSpotLightVertexParameters, binder.BindShadowTextureVertexParameters);
                        break;
                    case "TLightPixelShaderFDirectionalLightPolicyFNoStaticShadowingPolicy":
                    case "TLightPixelShaderFDirectionalLightPolicyFShadowVertexBufferPolicy":
                        shader = CreateLightPixelShader(binder.BindDirectionalLightPixelParameters, () => new FNullPolicy());
                        break;
                    case "TLightPixelShaderFPointLightPolicyFNoStaticShadowingPolicy":
                    case "TLightPixelShaderFPointLightPolicyFShadowVertexBufferPolicy":
                        shader = CreateLightPixelShader(binder.BindPointLightPixelParameters, () => new FNullPolicy());
                        break;
                    case "TLightPixelShaderFSpotLightPolicyFNoStaticShadowingPolicy":
                    case "TLightPixelShaderFSpotLightPolicyFShadowVertexBufferPolicy":
                        shader = CreateLightPixelShader(binder.BindSpotLightPixelParameters, () => new FNullPolicy());
                        break;
                    case "TLightPixelShaderFDirectionalLightPolicyFShadowTexturePolicy":
                        shader = CreateLightPixelShader(binder.BindDirectionalLightPixelParameters, binder.BindShadowTexturePixelParameters);
                        break;
                    case "TLightPixelShaderFPointLightPolicyFShadowTexturePolicy":
                        shader = CreateLightPixelShader(binder.BindPointLightPixelParameters, binder.BindShadowTexturePixelParameters);
                        break;
                    case "TLightPixelShaderFSpotLightPolicyFShadowTexturePolicy":
                        shader = CreateLightPixelShader(binder.BindSpotLightPixelParameters, binder.BindShadowTexturePixelParameters);
                        break;
                    case "TLightPixelShaderFDirectionalLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                        shader = CreateLightPixelShader(binder.BindDirectionalLightPixelParameters, binder.BindSignedDistanceFieldShadowPixelParameters);
                        break;
                    case "TLightPixelShaderFPointLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                        shader = CreateLightPixelShader(binder.BindPointLightPixelParameters, binder.BindSignedDistanceFieldShadowPixelParameters);
                        break;
                    case "TLightPixelShaderFSpotLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                        shader = CreateLightPixelShader(binder.BindSpotLightPixelParameters, binder.BindSignedDistanceFieldShadowPixelParameters);
                        break;
                    case "TFogIntegralVertexShader<FConeDensityPolicy>":
                    case "TFogIntegralVertexShader<FConstantDensityPolicy>":
                    case "TFogIntegralVertexShader<FLinearHalfspaceDensityPolicy>":
                    case "TFogIntegralVertexShader<FSphereDensityPolicy>":
                        // aacd3a0: VF, material VS, then six optional height-fog fields.
                        shader = new TFogIntegralVertexShader
                        {
                            VertexFactoryParameters = BindVertexFactory(),
                            MaterialParameters = binder.BindMaterialVertexParameters(translation), VertexFactoryType = factory.Name,
                            HeightFogParameters = binder.BindHeightFogParameters()
                        };
                        break;
                    case "TFogIntegralPixelShader<FConeDensityPolicy>":
                    case "TFogIntegralPixelShader<FConstantDensityPolicy>":
                    case "TFogIntegralPixelShader<FLinearHalfspaceDensityPolicy>":
                    case "TFogIntegralPixelShader<FSphereDensityPolicy>":
                        // aacd1a0: material PS followed by these optional density allocations.
                        shader = new TFogIntegralPixelShader
                        {
                            MaterialParameters = binder.BindMaterialPixelParameters(translation),
                            DepthFilterSampleOffsets = binder.BindConstant("DepthFilterSampleOffsets"),
                            ScreenToWorld = binder.BindConstant("ScreenToWorld"), FogCameraPosition = binder.BindConstant("FogCameraPosition"),
                            FaceScale = binder.BindConstant("FaceScale"), FirstDensityFunctionParameters = binder.BindConstant("FirstDensityFunctionParameters"),
                            SecondDensityFunctionParameters = binder.BindConstant("SecondDensityFunctionParameters"),
                            StartDistance = binder.BindConstant("StartDistance"), InvMaxIntegral = binder.BindConstant("InvMaxIntegral")
                        };
                        break;
                    case "FFogVolumeApplyVertexShader":
                        // Folded aa61170 compiled constructor: VF then material VS.
                        shader = new FFogVolumeApplyVertexShader
                        {
                            VertexFactoryParameters = BindVertexFactory(),
                            MaterialParameters = binder.BindMaterialVertexParameters(translation), VertexFactoryType = factory.Name
                        };
                        break;
                    case "FFogVolumeApplyPixelShader":
                        // aacd780: optional MaxIntegral, material PS, then optional integral textures.
                        var maxIntegral = binder.BindConstant("MaxIntegral");
                        shader = new FFogVolumeApplyPixelShader
                        {
                            MaxIntegral = maxIntegral, MaterialParameters = binder.BindMaterialPixelParameters(translation),
                            AccumulatedFrontfacesLineIntegralTexture = binder.BindResource("AccumulatedFrontfacesLineIntegralTexture"),
                            AccumulatedBackfacesLineIntegralTexture = binder.BindResource("AccumulatedBackfacesLineIntegralTexture")
                        };
                        break;
                    case "TAOMeshVertexShader<0>":
                    case "TAOMeshVertexShader<1>":
                        // a9e7300: VF then two optional matrices. No material-parameter bind.
                        shader = new TAOMeshVertexShader
                        {
                            VertexFactoryParameters = BindVertexFactory(), VertexFactoryType = factory.Name,
                            PrevViewProjectionMatrix = binder.BindConstant("PrevViewProjectionMatrix"),
                            PreviousLocalToWorld = binder.BindConstant("PreviousLocalToWorld")
                        };
                        break;
                    case "FHitMaskVertexShader":
                        // aaf20e0: VF, optional pixel-center offset, then material VS.
                        var hitMaskFactory = BindVertexFactory();
                        var pixelCenterOffset = binder.BindConstant("PixelCenterOffset");
                        shader = new FHitMaskVertexShader
                        {
                            VertexFactoryParameters = hitMaskFactory, PixelCenterOffset = pixelCenterOffset,
                            MaterialParameters = binder.BindMaterialVertexParameters(translation), VertexFactoryType = factory.Name
                        };
                        break;
                    case "FHitMaskPixelShader":
                        // aaf1f60: material PS precedes these optional hit-mask allocations.
                        shader = new FHitMaskPixelShader
                        {
                            MaterialParameters = binder.BindMaterialPixelParameters(translation),
                            HitLocation = binder.BindConstant("HitLocation"), HitStartLocation = binder.BindConstant("HitStartLocation"),
                            HitRadius = binder.BindConstant("HitRadius"), HitCullDistance = binder.BindConstant("HitCullDistance"),
                            CurrentMaskTexture = binder.BindResource("CurrentMaskTexture")
                        };
                        break;
                    case "FVelocityVertexShader":
                        // b1608e0: VF, material, then one required and two optional constants.
                        shader = new FVelocityVertexShader
                        {
                            VertexFactoryParameters = BindVertexFactory(),
                            MaterialParameters = binder.BindMaterialVertexParameters(translation), VertexFactoryType = factory.Name,
                            PrevViewProjectionMatrix = binder.BindConstant("PrevViewProjectionMatrix", optional: false),
                            PreviousLocalToWorld = binder.BindConstant("PreviousLocalToWorld"),
                            StretchTimeScale = binder.BindConstant("StretchTimeScale")
                        };
                        break;
                    case "FVelocityPixelShader":
                        // b1607c0: material PS before the three optional velocity allocations.
                        shader = new FVelocityPixelShader
                        {
                            MaterialParameters = binder.BindMaterialPixelParameters(translation),
                            VelocityScaleOffset = binder.BindConstant("VelocityScaleOffset"),
                            IndividualVelocityScale = binder.BindConstant("IndividualVelocityScale"),
                            ObjectVelocity = binder.BindConstant("ObjectVelocity")
                        };
                        break;
                    case "FSFXWorldNormalVertexShader":
                        // Shared aa61170 compiled constructor: VF then material VS.
                        shader = new FSFXWorldNormalVertexShader
                        {
                            VertexFactoryParameters = BindVertexFactory(),
                            MaterialParameters = binder.BindMaterialVertexParameters(translation), VertexFactoryType = factory.Name
                        };
                        break;
                    case "FSFXWorldNormalPixelShader":
                        shader = new FSFXWorldNormalPixelShader { MaterialParameters = binder.BindMaterialPixelParameters(translation) };
                        break;
                    case "FTextureDensityVertexShader":
                        shader = new FTextureDensityVertexShader
                        {
                            VertexFactoryParameters = BindVertexFactory(),
                            MaterialParameters = binder.BindMaterialVertexParameters(translation), VertexFactoryType = factory.Name
                        };
                        break;
                    case "FTextureDensityPixelShader":
                        // c49500: material PS, then both density allocations are required.
                        shader = new FTextureDensityPixelShader
                        {
                            MaterialParameters = binder.BindMaterialPixelParameters(translation),
                            TextureDensityParameters = binder.BindConstant("TextureDensityParameters", optional: false),
                            TextureLookupInfo = binder.BindConstant("TextureLookupInfo", optional: false)
                        };
                        break;
                    case "FHitProxyVertexShader":
                        shader = new FHitProxyVertexShader
                        {
                            VertexFactoryParameters = BindVertexFactory(),
                            MaterialParameters = binder.BindMaterialVertexParameters(translation), VertexFactoryType = factory.Name
                        };
                        break;
                    case "FHitProxyPixelShader":
                        // be1e00 binds material first and requires HitProxyId.
                        shader = new FHitProxyPixelShader
                        {
                            MaterialParameters = binder.BindMaterialPixelParameters(translation),
                            HitProxyId = binder.BindConstant("HitProxyId", optional: false)
                        };
                        break;
                    case "FModShadowMeshVertexShader":
                    {
                        // ac085e0 binds VF, required LightPosition, then material VS parameters.
                        var vertexFactory = BindVertexFactory();
                        var lightPosition = binder.BindConstant("LightPosition", optional: false);
                        var materialParameters = binder.BindMaterialVertexParameters(translation);
                        shader = new FModShadowMeshVertexShader
                        {
                            VertexFactoryParameters = vertexFactory, LightPosition = lightPosition,
                            MaterialParameters = materialParameters, VertexFactoryType = factory.Name
                        };
                        break;
                    }
                    case "FModShadowMeshPixelShader":
                        // ac10d90 binds material PS parameters before required AttenAllowed.
                        shader = new FModShadowMeshPixelShader
                        {
                            MaterialParameters = binder.BindMaterialPixelParameters(translation),
                            AttenAllowed = binder.BindConstant("AttenAllowed", optional: false)
                        };
                        break;
                    // Both depth-only registrations fold to 0x7ff74aa61170: VF then material VS.
                    case "TDepthOnlyVertexShader<0>":
                    case "TDepthOnlyVertexShader<1>":
                        shader = new TDepthOnlyVertexShader
                        {
                            VertexFactoryParameters = BindVertexFactory(),
                            MaterialParameters = binder.BindMaterialVertexParameters(translation), VertexFactoryType = factory.Name
                        };
                        break;
                    // Native initializer 0x7ff74a8acb70 uses the same material mesh VS body.
                    case "TDistortionMeshVertexShader<FDistortMeshAccumulatePolicy>":
                        shader = new TDistortionMeshVertexShader
                        {
                            VertexFactoryParameters = BindVertexFactory(),
                            MaterialParameters = binder.BindMaterialVertexParameters(translation), VertexFactoryType = factory.Name
                        };
                        break;
                    // Native 0x7ff74aa610d0 adds no distortion-specific allocations.
                    case "TDistortionMeshPixelShader<FDistortMeshAccumulatePolicy>":
                        shader = new TDistortionMeshPixelShader { MaterialParameters = binder.BindMaterialPixelParameters(translation) };
                        break;
                    // Shared body 0x7ff74aa86990 contains only material PS binding.
                    case "TDepthOnlySolidPixelShader":
                        shader = new TDepthOnlySolidPixelShader { MaterialParameters = binder.BindMaterialPixelParameters(translation) };
                        break;
                    case "TDepthOnlyScreenDoorPixelShader":
                        // Registration a8ac5d0 folds compiled construction to aa610d0.
                        shader = new TDepthOnlyScreenDoorPixelShader { MaterialParameters = binder.BindMaterialPixelParameters(translation) };
                        break;
                    case "FTranslucencyPostRenderDepthPixelShader":
                        shader = new FTranslucencyPostRenderDepthPixelShader { MaterialParameters = binder.BindMaterialPixelParameters(translation) };
                        break;
                    // Three registrations share 0x7ff74ac10be0 -> 0x7ff74ac09ae0.
                    case "TShadowDepthVertexShader<ShadowDepth_OutputDepth>":
                    case "TShadowDepthVertexShader<ShadowDepth_PerspectiveCorrect>":
                    case "TShadowDepthVertexShader<ShadowDepth_OutputDepthToColor>":
                        shader = new TShadowDepthVertexShader
                        {
                            VertexFactoryParameters = BindVertexFactory(),
                            MaterialParameters = binder.BindMaterialVertexParameters(translation), VertexFactoryType = factory.Name,
                            ProjectionMatrix = binder.BindConstant("ProjectionMatrix", optional: false),
                            InvMaxSubjectDepth = binder.BindConstant("InvMaxSubjectDepth"),
                            DepthBias = binder.BindConstant("DepthBias"), bClampToNearPlane = binder.BindConstant("bClampToNearPlane")
                        };
                        break;
                    // Four registrations share 0x7ff74ac10b70 -> 0x7ff74ac09830, both extras optional.
                    case "TShadowDepthPixelShaderTRUETRUE":
                    case "TShadowDepthPixelShaderFALSETRUE":
                    case "TShadowDepthPixelShaderTRUEFALSE":
                    case "TShadowDepthPixelShaderFALSEFALSE":
                        shader = new TShadowDepthPixelShader
                        {
                            MaterialParameters = binder.BindMaterialPixelParameters(translation),
                            InvMaxSubjectDepth = binder.BindConstant("InvMaxSubjectDepth"), DepthBias = binder.BindConstant("DepthBias")
                        };
                        break;
                    // aa0a8f0/aa0abc0/aa0ae80: VF, policy, material, height fog, density.
                    case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFConeDensityPolicy":
                    case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFConeDensityPolicy":
                    case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFConeDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFConeDensityPolicy":
                    case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFSphereDensityPolicy":
                    case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFSphereDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFSphereDensityPolicy":
                    case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFSphereDensityPolicy":
                        shader = CreateBasePassVertexShader(() => new FLightMapTexturePolicy.VertexParametersType
                            { LightmapCoordinateScaleBias = binder.BindConstant("LightmapCoordinateScaleBias") }, binder.BindFogDensityParameters);
                        break;
                    case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFConeDensityPolicy":
                    case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFConeDensityPolicy":
                    case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFConeDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFConeDensityPolicy":
                    case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFSphereDensityPolicy":
                    case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFSphereDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFSphereDensityPolicy":
                    case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFSphereDensityPolicy":
                        shader = CreateBasePassVertexShader(() => new FVertexLightMapPolicy.VertexParametersType
                            { LightMapScale = binder.BindConstant("LightMapScale", optional: false) }, binder.BindFogDensityParameters);
                        break;
                    case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFConeDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFSHLightLightMapPolicyFConeDensityPolicy":
                    case "TBasePassVertexShaderFSHLightLightMapPolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFSphereDensityPolicy":
                    case "TBasePassVertexShaderFSHLightLightMapPolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFSHLightLightMapPolicyFSphereDensityPolicy":
                        shader = CreateBasePassVertexShader(() => new FDirectionalLightLightMapPolicy.VertexParametersType
                            { LightDirectionAndbDirectional = binder.BindConstant("LightDirectionAndbDirectional") }, binder.BindFogDensityParameters);
                        break;
                    // Compiled construction callback 0x7ff74aa0b2a0: VF, material VS, height fog.
                    case "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy":
                        shader = CreateVertexShader(() => new FNullPolicy());
                        break;
                    // All four density registrations share aa0b150: VF, material, height fog, density.
                    case "TBasePassVertexShaderFNoLightMapPolicyFConeDensityPolicy":
                    case "TBasePassVertexShaderFNoLightMapPolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFNoLightMapPolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFNoLightMapPolicyFSphereDensityPolicy":
                        shader = CreateBasePassVertexShader(() => new FNullPolicy(), binder.BindFogDensityParameters);
                        break;
                    // Shared native callback 0x7ff74aa0aa70; policy bind is optional.
                    case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFNoDensityPolicy":
                    case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFNoDensityPolicy":
                    case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFNoDensityPolicy":
                    case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFNoDensityPolicy":
                        shader = CreateVertexShader(() => new FLightMapTexturePolicy.VertexParametersType
                            { LightmapCoordinateScaleBias = binder.BindConstant("LightmapCoordinateScaleBias") });
                        break;
                    // Folded callback 0x7ff74aa0ad30: unlike texture policy, scale is required.
                    case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFNoDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFNoDensityPolicy":
                    case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFNoDensityPolicy":
                    case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFNoDensityPolicy":
                        shader = CreateVertexShader(() => new FVertexLightMapPolicy.VertexParametersType
                            { LightMapScale = binder.BindConstant("LightMapScale", optional: false) });
                        break;
                    case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFNoDensityPolicy":
                    case "TBasePassVertexShaderFSHLightLightMapPolicyFNoDensityPolicy":
                        // Both initializers install aa0b000; VF, optional direction, material, fog.
                        // Existing archive reader uses the equivalent one-parameter directional layout.
                        shader = CreateVertexShader(() => new FDirectionalLightPolicy.VertexParametersType
                            { LightDirection = binder.BindConstant("LightDirectionAndbDirectional") });
                        break;
                    case "TBasePassPixelShaderFDirectionalLightLightMapPolicyNoSkyLight":
                    case "TBasePassPixelShaderFDirectionalLightLightMapPolicySkyLight":
                        // aa039a0 binds policy before material and standard base-pass parameters.
                        shader = CreatePixelShader(new FDirectionalLightLightMapPolicy.PixelParametersType
                        {
                            LightColorAndFalloffExponent = binder.BindConstant("LightColorAndFalloffExponent"),
                            bReceiveDynamicShadows = binder.BindConstant("bReceiveDynamicShadows")
                        });
                        break;
                    case "TBasePassPixelShaderFSHLightLightMapPolicyNoSkyLight":
                    case "TBasePassPixelShaderFSHLightLightMapPolicySkyLight":
                        // aa03e40 additionally binds optional WorldIncidentLighting before material.
                        shader = CreatePixelShader(new FSHLightLightMapPolicy.PixelParametersType
                        {
                            LightColorAndFalloffExponent = binder.BindConstant("LightColorAndFalloffExponent"),
                            bReceiveDynamicShadows = binder.BindConstant("bReceiveDynamicShadows"),
                            WorldIncidentLighting = binder.BindConstant("WorldIncidentLighting")
                        });
                        break;
                    // NoLightMap and SimpleVertex PS registrations share folded callback
                    // 0x7ff74aa0a730 -> body 0x7ff74aa037b0; no serialized lightmap fields.
                    case "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight":
                    case "TBasePassPixelShaderFNoLightMapPolicySkyLight":
                    case "TBasePassPixelShaderFSimpleVertexLightMapPolicyNoSkyLight":
                    case "TBasePassPixelShaderFSimpleVertexLightMapPolicySkyLight":
                    case "TBasePassPixelShaderFDirectionalVertexLightMapPolicyNoSkyLight":
                    case "TBasePassPixelShaderFDirectionalVertexLightMapPolicySkyLight":
                    case "TBasePassPixelShaderFCustomVectorVertexLightMapPolicyNoSkyLight":
                    case "TBasePassPixelShaderFCustomVectorVertexLightMapPolicySkyLight":
                    case "TBasePassPixelShaderFCustomSimpleVertexLightMapPolicyNoSkyLight":
                    case "TBasePassPixelShaderFCustomSimpleVertexLightMapPolicySkyLight":
                        shader = CreatePixelShader(new FNullPolicy());
                        break;
                    // Native policy/material binding body 0x7ff74aa03bf0, both optional.
                    case "TBasePassPixelShaderFDirectionalLightMapTexturePolicyNoSkyLight":
                    case "TBasePassPixelShaderFDirectionalLightMapTexturePolicySkyLight":
                    case "TBasePassPixelShaderFSimpleLightMapTexturePolicyNoSkyLight":
                    case "TBasePassPixelShaderFSimpleLightMapTexturePolicySkyLight":
                        shader = CreatePixelShader(new FLightMapTexturePolicy.PixelParametersType
                        {
                            LightMapTextures = binder.BindResource("LightMapTextures"),
                            LightMapScale = binder.BindConstant("LightMapScale")
                        });
                        break;
                    // Custom texture-lightmap body aa03540 includes optional Bias in addition to
                    // ordinary texture fields. Allocation wrapper aa0a6c0 is shared by all four PS.
                    case "TBasePassPixelShaderFCustomVectorLightMapTexturePolicyNoSkyLight":
                    case "TBasePassPixelShaderFCustomVectorLightMapTexturePolicySkyLight":
                    case "TBasePassPixelShaderFCustomSimpleLightMapTexturePolicyNoSkyLight":
                    case "TBasePassPixelShaderFCustomSimpleLightMapTexturePolicySkyLight":
                        shader = CreatePixelShader(new FCustomLightMapTexturePolicy.PixelParametersType
                        {
                            LightMapTextures = binder.BindResource("LightMapTextures"),
                            LightMapScale = binder.BindConstant("LightMapScale"), LightMapBias = binder.BindConstant("LightMapBias")
                        });
                        break;
                    default:
                        return Failure($"LE3 shader-object constructor not yet recovered: {target.ShaderTypeName}");
                }
            }
            catch (InvalidOperationException exception) { return Failure(exception.Message); }
            var rendererOwned = Array.AsReadOnly(binder.UnboundParameters.Where(p =>
                LE3ShaderParameterBinder.IsRendererOwnedViewParameter(p, target.Stage)).ToArray());
            var unbound = Array.AsReadOnly(binder.UnboundParameters.Where(p =>
                !LE3ShaderParameterBinder.IsRendererOwnedViewParameter(p, target.Stage) &&
                !LE3ShaderParameterBinder.IsNativelyUnboundDecalPixelParameter(p, target.Stage, factory.Name)).ToArray());
            if (unbound.Count != 0) return new ShaderConstructionResult
            {
                RendererOwnedParameters = rendererOwned,
                UnboundParameters = unbound,
                Errors = Array.AsReadOnly(new[] { "Unexplained LE3 shader parameter allocations: " + string.Join(", ", unbound.Select(p => p.Name)) })
            };
            shader.ShaderType = target.ShaderTypeName;
            shader.Guid = Guid.NewGuid();
            shader.Platform = 5; // LE3 SP_SM5
            shader.Frequency = target.Stage == ShaderStage.Vertex ? Shader.ShaderFrequency.Vertex : Shader.ShaderFrequency.PixelSM5;
            shader.ShaderByteCode = (byte[])compiledShader.Bytecode.Clone();
            shader.ParameterMapCRC = binder.ParameterMapCRC;
            shader.InstructionCount = compiledShader.InstructionCount;
            return new ShaderConstructionResult { Shader = shader, RendererOwnedParameters = rendererOwned };

            Shader CreateVertexShader<TPolicy>(Func<TPolicy> bindPolicy) where TPolicy : struct, IVertexParametersType
                => CreateBasePassVertexShader(bindPolicy, () => new FNullPolicy());

            Shader CreateBasePassVertexShader<TPolicy, TDensity>(Func<TPolicy> bindPolicy, Func<TDensity> bindDensity)
                where TPolicy : struct, IVertexParametersType where TDensity : struct, IVertexShaderParametersType
            {
                // Native VF bind precedes policy bind, then material VS and height fog.
                var vf = BindVertexFactory();
                return new TBasePassVertexShader<TPolicy, TDensity>
                {
                    VertexFactoryParameters = vf,
                    LightMapVertexParams = bindPolicy(), MaterialParameters = binder.BindMaterialVertexParameters(translation),
                    HeightFogParameters = binder.BindHeightFogParameters(), DensityVertexShaderParams = bindDensity(), VertexFactoryType = factory.Name
                };
            }

            FVertexFactoryParameterRef BindVertexFactory() => new()
            {
                VertexFactoryType = factory.Name,
                Parameters = binder.BindVertexFactoryParameters(factory.Name)
            };

            Shader CreateLightVertexShader<TLight, TShadow>(Func<TLight> bindLight, Func<TShadow> bindShadow)
                where TLight : struct, IVertexParametersType where TShadow : struct, IVertexParametersType
            {
                // Compiled bodies bind VF before light/shadow policy, despite serialized policy-prefix order.
                var vf = BindVertexFactory();
                return new TLightVertexShader<TLight, TShadow>
                {
                    LightTypeVertexParams = bindLight(), ShadowingVertexParams = bindShadow(), VertexFactoryParameters = vf,
                    MaterialParameters = binder.BindMaterialVertexParameters(translation), VertexFactoryType = factory.Name
                };
            }

            Shader CreateLightPixelShader<TLight, TShadow>(Func<TLight> bindLight, Func<TShadow> bindShadow)
                where TLight : struct, IPixelParametersType where TShadow : struct, IPixelParametersType => new TLightPixelShader<TLight, TShadow>
            {
                LightTypePixelParams = bindLight(), ShadowingPixelParams = bindShadow(), MaterialParameters = binder.BindMaterialPixelParameters(translation),
                LightAttenuationTexture = binder.BindResource("LightAttenuationTexture"), bReceiveDynamicShadows = binder.BindConstant("bReceiveDynamicShadows")
            };

            Shader CreatePixelShader<TPolicy>(TPolicy policy) where TPolicy : struct, IPixelParametersType => new TBasePassPixelShader<TPolicy>
            {
                PixelParams = policy, MaterialParameters = binder.BindMaterialPixelParameters(translation),
                AmbientColorAndSkyFactor = binder.BindConstant("AmbientColorAndSkyFactor"),
                UpperSkyColor = binder.BindConstant("UpperSkyColor"), LowerSkyColor = binder.BindConstant("LowerSkyColor"),
                MotionBlurMask = binder.BindConstant("MotionBlurMask"), CharacterMask = binder.BindConstant("CharacterMask"),
                TranslucencyDepth = binder.BindConstant("TranslucencyDepthParameters")
            };

            Shader CreateLightMapDensityVertexShader<TPolicy>(Func<TPolicy> bindPolicy) where TPolicy : struct, IVertexParametersType
            {
                var vf = BindVertexFactory();
                return new TLightMapDensityVertexShader<TPolicy>
                {
                    VertexFactoryParameters = vf, LightTypeVertexParams = bindPolicy(),
                    MaterialParameters = binder.BindMaterialVertexParameters(translation), VertexFactoryType = factory.Name
                };
            }

            Shader CreateLightMapDensityPixelShader<TPolicy>(TPolicy policy) where TPolicy : struct, IPixelParametersType => new TLightMapDensityPixelShader<TPolicy>
            {
                PixelParams = policy, MaterialParameters = binder.BindMaterialPixelParameters(translation),
                LightMapDensityParameters = binder.BindConstant("LightMapDensityParameters"),
                BuiltLightingAndSelectedFlags = binder.BindConstant("BuiltLightingAndSelectedFlags"),
                DensitySelectedColor = binder.BindConstant("DensitySelectedColor"),
                LightMapResolutionScale = binder.BindConstant("LightMapResolutionScale"),
                LightMapDensityDisplayOptions = binder.BindConstant("LightMapDensityDisplayOptions"),
                VertexMappedColor = binder.BindConstant("VertexMappedColor"), GridTexture = binder.BindResource("GridTexture")
            };

            static ShaderConstructionResult Failure(string error) => new() { Errors = Array.AsReadOnly(new[] { error }) };
        }

        /// <summary>
        /// Groups successfully constructed shaders into a MaterialShaderMap fragment. Every plan
        /// job must be present exactly once and belong to this translation; failures withhold the
        /// entire fragment. The explicit map ID is the caller's material-resource ID, not a guessed
        /// parent/instance key. A nonzero resolved BaseMaterialId is preserved; an empty base ID
        /// is initialized from the resource ID in the detached snapshot, without altering input.
        /// A successful zero-job plan produces an empty detached fragment, not a complete shader set.
        /// No packages or resources are modified, and no source CRC values are fabricated.
        /// </summary>
        public ShaderMapFragmentResult BuildShaderMapFragment(TranslationResult translation, ShaderCompilePlan plan,
            IReadOnlyList<ShaderCompilationResult> compiledShaders, Guid materialResourceId, string friendlyName)
        {
            ValidateTranslation(translation);
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(compiledShaders);
            ArgumentException.ThrowIfNullOrWhiteSpace(friendlyName);
            if (materialResourceId == Guid.Empty) throw new ArgumentException("A nonempty material-resource ID is required.", nameof(materialResourceId));
            if (!ReferenceEquals(plan.CompilationInput, CompilationInput))
                throw new ArgumentException("The plan was produced for a different compilation input.", nameof(plan));
            if (!ReferenceEquals(plan.Translation, translation))
                throw new ArgumentException("The plan does not belong to this exact material translation.", nameof(plan));
            if (plan.Errors == null || !plan.Success || plan.Jobs == null)
                return Failure("A successful explicit shader compile plan is required.");
            if (plan.Jobs.Any(j => j == null || !ReferenceEquals(j.Translation, translation)))
                throw new ArgumentException("Every plan job must belong to this exact material translation.", nameof(plan));

            var errors = new List<string>();
            var expected = plan.Jobs.ToDictionary(j => (j.Target.ShaderTypeName, j.Target.VertexFactoryTypeName), j => j.Target);
            var seen = new HashSet<(string, string)>();
            var grouped = new Dictionary<string, MeshShaderMap>(StringComparer.Ordinal);
            var nonMeshShaders = new UMultiMap<NameReference, ShaderReference>();
            var shaders = new Dictionary<Guid, Shader>();
            foreach (var compiled in compiledShaders)
            {
                if (compiled == null || compiled.Target == null)
                {
                    errors.Add("Compiled shader entries and targets cannot be null.");
                    continue;
                }
                var target = compiled.Target;
                var key = (target.ShaderTypeName, target.VertexFactoryTypeName);
                if (!expected.TryGetValue(key, out var expectedTarget) || target != expectedTarget)
                {
                    errors.Add($"Compiled shader target is not an exact plan job: {key}.");
                    continue;
                }
                if (!seen.Add(key))
                {
                    errors.Add($"Duplicate compiled shader job: {key}.");
                    continue;
                }
                var construction = ConstructKnownShader(translation, compiled);
                if (!construction.Success)
                {
                    errors.AddRange(construction.Errors.Select(e => $"{key}: {e}"));
                    continue;
                }
                var shader = construction.Shader;
                shaders.Add(shader.Guid, shader);
                if (target.VertexFactoryTypeName == null)
                {
                    nonMeshShaders.Add(shader.ShaderType, new ShaderReference { ShaderType = shader.ShaderType, Id = shader.Guid });
                    continue;
                }
                // Pixel objects have no serialized VF parameter reference. Group using the job's
                // registration metadata, never Shader.VertexFactoryType or a guessed class name.
                if (!grouped.TryGetValue(target.VertexFactoryTypeName, out var meshMap))
                {
                    meshMap = new MeshShaderMap { VertexFactoryType = target.VertexFactoryTypeName, Shaders = new() };
                    grouped.Add(target.VertexFactoryTypeName, meshMap);
                }
                meshMap.Shaders.Add(shader.ShaderType, new ShaderReference { ShaderType = shader.ShaderType, Id = shader.Guid });
            }
            foreach (var key in expected.Keys.Where(k => !seen.Contains(k))) errors.Add($"Missing compiled shader job: {key}.");
            if (errors.Count != 0) return new ShaderMapFragmentResult { Errors = errors.AsReadOnly() };

            var parameters = CompilationInput.GetStaticParametersSnapshot() ?? (StaticParameterSet)materialResourceId;
            // InitShaderMap ab51290 fills only a zero BaseMaterialId from the material's ID.
            if (parameters.BaseMaterialId == Guid.Empty) parameters.BaseMaterialId = materialResourceId;
            var map = new MaterialShaderMap
            {
                ID = materialResourceId, FriendlyName = friendlyName, StaticParameters = parameters,
                Shaders = nonMeshShaders, MeshShaderMaps = grouped.Values.ToArray(),
                UniformPixelVectorExpressions = translation.UniformPixelVectorExpressions.ToArray(),
                UniformPixelScalarExpressions = translation.UniformPixelScalarExpressions.ToArray(),
                Uniform2DTextureExpressions = translation.Uniform2DTextureExpressions.ToArray(),
                UniformCubeTextureExpressions = translation.UniformCubeTextureExpressions.ToArray(),
                UniformVertexVectorExpressions = translation.UniformVertexVectorExpressions.ToArray(),
                UniformVertexScalarExpressions = translation.UniformVertexScalarExpressions.ToArray()
            };
            return new ShaderMapFragmentResult
            {
                Map = map, Shaders = new ReadOnlyDictionary<Guid, Shader>(shaders),
                UniformExpressionTextures = Array.AsReadOnly(translation.UniformExpressionTextures.ToArray()),
                IsCompleteShaderSet = plan.CoversNativeRequiredSet
            };

            static ShaderMapFragmentResult Failure(string error) => new() { Errors = Array.AsReadOnly(new[] { error }) };
        }

        /// <summary>
        /// Wraps an explicitly planned map fragment and its typed objects in a detached ShaderCache,
        /// with source CRCs computed from a caller-supplied LE3 source-text session. The caller must
        /// use the same immutable source tree as compilation; these CRCs are not bytecode checksums.
        /// Registry completeness, package installation and runtime compatibility remain unproven.
        /// Any constructor or source-CRC failure withholds the entire cache; no package is modified.
        /// </summary>
        public ShaderCacheFragmentResult BuildShaderCacheFragment(TranslationResult translation, ShaderCompilePlan plan,
            IReadOnlyList<ShaderCompilationResult> compiledShaders, Guid materialResourceId, string friendlyName,
            LE3ShaderSourceCrc sourceCrcs)
        {
            ArgumentNullException.ThrowIfNull(sourceCrcs);
            var fragment = BuildShaderMapFragment(translation, plan, compiledShaders, materialResourceId, friendlyName);
            if (!fragment.Success) return new ShaderCacheFragmentResult { Errors = fragment.Errors };
            var cache = ShaderCache.Create();
            try
            {
                foreach (var target in plan.Jobs.Select(j => j.Target).DistinctBy(t => t.ShaderTypeName))
                    cache.ShaderTypeCRCMap.Add(target.ShaderTypeName, sourceCrcs.Compute(target.SourceFileName).Crc);
                // PS objects omit VF refs, but their jobs still depend on that factory source.
                foreach (var target in plan.Jobs.Select(j => j.Target).Where(t => t.VertexFactoryTypeName != null)
                             .DistinctBy(t => t.VertexFactoryTypeName))
                    cache.VertexFactoryTypeCRCMap.Add(target.VertexFactoryTypeName, sourceCrcs.Compute(target.VertexFactoryShaderFileName).Crc);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                return new ShaderCacheFragmentResult { Errors = Array.AsReadOnly(new[] { "Cannot build LE3 source CRC maps: " + exception.Message }) };
            }
            foreach (var pair in fragment.Shaders) cache.Shaders.Add(pair.Key, pair.Value);
            cache.MaterialShaderMaps.Add(fragment.Map.StaticParameters, fragment.Map);
            return new ShaderCacheFragmentResult
            {
                Cache = cache, UniformExpressionTextures = fragment.UniformExpressionTextures,
                IsCompleteShaderSet = fragment.IsCompleteShaderSet
            };
        }

        /// <summary>Checked source-snapshot path. Rejects results from directory compilation,
        /// other snapshots or a translation using a different MaterialTemplate before computing CRCs.</summary>
        public ShaderCacheFragmentResult BuildShaderCacheFragment(TranslationResult translation, ShaderCompilePlan plan,
            IReadOnlyList<ShaderCompilationResult> compiledShaders, Guid materialResourceId, string friendlyName,
            LE3ShaderSourceSnapshot sources)
        {
            ArgumentNullException.ThrowIfNull(sources);
            ArgumentNullException.ThrowIfNull(compiledShaders);
            if (translation == null || !ReferenceEquals(translation.SourceSnapshot, sources) ||
                compiledShaders.Any(result => result == null || !ReferenceEquals(result.SourceSnapshot, sources)))
                return new ShaderCacheFragmentResult { Errors = Array.AsReadOnly(new[] { "Translation and every compiled shader must use the exact same LE3 source snapshot." }) };
            return BuildShaderCacheFragment(translation, plan, compiledShaders, materialResourceId, friendlyName, sources.CreateCrcSession());
        }

        // PerPixel means a nonuniform code chunk, even in a vertex input. Uniform carries the
        // canonical tree; IsConstant controls inline definitions versus lazy packed access.
        private sealed record CodeValue(MaterialValueType Type, string Definition, bool PerPixel,
            bool IsConstant = false, Func<string> AccessUniform = null, MaterialUniformExpression Uniform = null,
            int? ExplicitNativeNumericType = null)
        {
            public string Code => AccessUniform?.Invoke() ?? Definition;
            // Ordinary scalar producers/masks use Float=0xf; ForceCast can produce float1=1.
            public int NativeNumericType => ExplicitNativeNumericType ?? (Type == MaterialValueType.Scalar ? 0xf : 1 << ((int)Type - 1));
            // Texture-valued chunks (native 0x10 sampler2D / 0x20 cube) are not numeric; only
            // Custom arguments and static-switch passthrough may consume them.
            public bool IsTexture => ExplicitNativeNumericType is TextureValue2D or TextureValueCube;
        }

        /// <summary>Native INDEX_NONE (-1) code chunk: a silent "no value" that consumers propagate.</summary>
        private static readonly CodeValue IndexNoneValue = new(MaterialValueType.Scalar, "INDEX_NONE", false);

        private sealed class IndexNoneSignal : Exception
        {
            public static readonly IndexNoneSignal Instance = new();
        }

        private const int TextureValue2D = 0x10;
        private const int TextureValueCube = 0x20;

        private sealed class LE3ShaderInclude : global::SharpDX.D3DCompiler.Include
        {
            private readonly string shaderDirectory;
            private readonly LE3ShaderSourceSnapshot sources;
            private readonly string materialHlsl;
            private readonly string vertexFactoryShaderFileName;

            public LE3ShaderInclude(string shaderDirectory, string materialHlsl, string vertexFactoryShaderFileName)
            {
                this.shaderDirectory = shaderDirectory;
                this.materialHlsl = materialHlsl;
                this.vertexFactoryShaderFileName = vertexFactoryShaderFileName;
            }

            public LE3ShaderInclude(LE3ShaderSourceSnapshot sources, string materialHlsl, string vertexFactoryShaderFileName)
            {
                this.sources = sources;
                this.materialHlsl = materialHlsl;
                this.vertexFactoryShaderFileName = vertexFactoryShaderFileName;
            }

            public IDisposable Shadow { get; set; }

            public Stream Open(global::SharpDX.D3DCompiler.IncludeType type, string fileName, Stream parentStream)
            {
                if (fileName.Equals("Material.usf", StringComparison.OrdinalIgnoreCase))
                {
                    return new MemoryStream(Encoding.UTF8.GetBytes(materialHlsl), writable: false);
                }
                if (fileName.Equals("VertexFactory.usf", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(vertexFactoryShaderFileName))
                    {
                        throw new InvalidOperationException("The shader requested VertexFactory.usf but the compile target has no vertex-factory source.");
                    }
                    return OpenPhysicalSource(vertexFactoryShaderFileName);
                }
                return OpenPhysicalSource(fileName);
            }

            private Stream OpenPhysicalSource(string fileName) => sources != null ? sources.OpenCompileSource(fileName)
                : File.OpenRead(ResolveShaderPath(shaderDirectory, fileName));

            public void Close(Stream stream) => stream.Dispose();

            public void Dispose() => Shadow?.Dispose();
        }

        private static string ResolveShaderPath(string shaderDirectory, string shaderFileName)
        {
            if (string.IsNullOrWhiteSpace(shaderFileName))
            {
                throw new ArgumentException("Shader source name is required.", nameof(shaderFileName));
            }

            string root = Path.GetFullPath(shaderDirectory);
            string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;
            string relative = Path.ChangeExtension(shaderFileName, ".usf")
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            string path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Shader include escapes the LE3 shader directory: {shaderFileName}");
            }
            return path;
        }

        /// <summary>
        /// Inspects LE3 SM5 DXBC in compiler reflection order without a material or package write.
        /// This can also inspect shipping shader objects for calibration. Reflection alone does not
        /// establish translation provenance and cannot bypass ConstructKnownShader's checks.
        /// </summary>
        public static IReadOnlyList<ShaderParameterBinding> ReflectShaderBytecode(byte[] bytecode, out int instructionCount)
        {
            ArgumentNullException.ThrowIfNull(bytecode);
            if (bytecode.Length == 0) throw new ArgumentException("Shader bytecode cannot be empty.", nameof(bytecode));
            var bindings = new List<ShaderParameterBinding>();
            var emittedResources = new HashSet<string>(StringComparer.Ordinal);
            using var reflection = new global::SharpDX.D3DCompiler.ShaderReflection(bytecode);
            var shaderDescription = reflection.Description;
            instructionCount = shaderDescription.InstructionCount;

            for (int resourceIndex = 0; resourceIndex < shaderDescription.BoundResources; resourceIndex++)
            {
                var binding = reflection.GetResourceBindingDescription(resourceIndex);
                if (binding.Type is global::SharpDX.D3DCompiler.ShaderInputType.ConstantBuffer or
                    global::SharpDX.D3DCompiler.ShaderInputType.TextureBuffer)
                {
                    ushort bufferIndex = checked((ushort)binding.BindPoint);
                    var constantBuffer = reflection.GetConstantBuffer(binding.Name);
                    var bufferDescription = constantBuffer.Description;
                    for (int variableIndex = 0; variableIndex < bufferDescription.VariableCount; variableIndex++)
                    {
                        var variableDescription = constantBuffer.GetVariable(variableIndex).Description;
                        if ((variableDescription.Flags & global::SharpDX.D3DCompiler.ShaderVariableFlags.Used) == 0) continue;
                        bindings.Add(new ShaderParameterBinding(
                            variableDescription.Name,
                            ShaderParameterBindingKind.Constant,
                            bufferIndex,
                            checked((ushort)variableDescription.StartOffset),
                            checked((ushort)variableDescription.Size),
                            null));
                    }
                }
                else if (binding.Type == global::SharpDX.D3DCompiler.ShaderInputType.Texture)
                {
                    bool hasSampler = false;
                    for (int otherIndex = 0; otherIndex < shaderDescription.BoundResources; otherIndex++)
                    {
                        var other = reflection.GetResourceBindingDescription(otherIndex);
                        if (other.Type == global::SharpDX.D3DCompiler.ShaderInputType.Sampler &&
                            SamplerMatchesTexture(other.Name, binding.Name))
                        {
                            hasSampler = true;
                            break;
                        }
                    }
                    if (hasSampler) continue;

                    string name = StripArraySuffix(binding.Name);
                    if (!emittedResources.Add(name)) continue;
                    int baseIndex = binding.BindPoint;
                    int count = 0;
                    for (int otherIndex = 0; otherIndex < shaderDescription.BoundResources; otherIndex++)
                    {
                        var other = reflection.GetResourceBindingDescription(otherIndex);
                        if (other.Type != global::SharpDX.D3DCompiler.ShaderInputType.Texture ||
                            StripArraySuffix(other.Name) != name) continue;
                        baseIndex = Math.Min(baseIndex, other.BindPoint);
                        count += other.BindCount;
                    }
                    bindings.Add(new ShaderParameterBinding(name, ShaderParameterBindingKind.Resource, 0,
                        checked((ushort)baseIndex), checked((ushort)count), null));
                }
                else if (binding.Type == global::SharpDX.D3DCompiler.ShaderInputType.UnorderedAccessViewRWTyped)
                {
                    string name = StripArraySuffix(binding.Name);
                    if (!emittedResources.Add(name)) continue;
                    int baseIndex = binding.BindPoint;
                    int count = 0;
                    for (int otherIndex = 0; otherIndex < shaderDescription.BoundResources; otherIndex++)
                    {
                        var other = reflection.GetResourceBindingDescription(otherIndex);
                        if (other.Type != global::SharpDX.D3DCompiler.ShaderInputType.UnorderedAccessViewRWTyped ||
                            StripArraySuffix(other.Name) != name) continue;
                        baseIndex = Math.Min(baseIndex, other.BindPoint);
                        count += other.BindCount;
                    }
                    bindings.Add(new ShaderParameterBinding(name, ShaderParameterBindingKind.Resource, 0,
                        checked((ushort)baseIndex), checked((ushort)count), null));
                }
                else if (binding.Type == global::SharpDX.D3DCompiler.ShaderInputType.Sampler)
                {
                    string samplerBaseName = StripSamplerSuffix(StripArraySuffix(binding.Name));
                    if (!emittedResources.Add(samplerBaseName)) continue;
                    int textureBase = int.MaxValue;
                    int textureCount = 0;
                    int samplerBase = int.MaxValue;
                    int samplerCount = 0;
                    for (int textureIndex = 0; textureIndex < shaderDescription.BoundResources; textureIndex++)
                    {
                        var other = reflection.GetResourceBindingDescription(textureIndex);
                        if (other.Type == global::SharpDX.D3DCompiler.ShaderInputType.Texture &&
                            StripArraySuffix(other.Name) == samplerBaseName)
                        {
                            textureBase = Math.Min(textureBase, other.BindPoint);
                            textureCount += other.BindCount;
                        }
                        else if (other.Type == global::SharpDX.D3DCompiler.ShaderInputType.Sampler &&
                                 StripSamplerSuffix(StripArraySuffix(other.Name)) == samplerBaseName)
                        {
                            samplerBase = Math.Min(samplerBase, other.BindPoint);
                            samplerCount += other.BindCount;
                        }
                    }
                    if (textureCount > 0)
                    {
                        bindings.Add(new ShaderParameterBinding(samplerBaseName, ShaderParameterBindingKind.Resource, 0,
                            checked((ushort)textureBase), checked((ushort)Math.Min(textureCount, samplerCount)),
                            checked((ushort)samplerBase)));
                    }
                    else
                    {
                        bindings.Add(new ShaderParameterBinding(samplerBaseName, ShaderParameterBindingKind.Resource, 0,
                            ushort.MaxValue, checked((ushort)samplerCount), checked((ushort)samplerBase)));
                    }
                }
            }

            return new ReadOnlyCollection<ShaderParameterBinding>(bindings);
        }

        private static bool SamplerMatchesTexture(string samplerName, string textureName)
        {
            samplerName = StripArraySuffix(samplerName);
            textureName = StripArraySuffix(textureName);
            return samplerName.Equals(textureName, StringComparison.Ordinal) ||
                   samplerName.EndsWith("Sampler", StringComparison.Ordinal) &&
                   samplerName.AsSpan()[..^"Sampler".Length].Equals(textureName.AsSpan(), StringComparison.Ordinal);
        }

        private static string StripArraySuffix(string name)
        {
            int bracket = name.IndexOf('[');
            return bracket >= 0 ? name[..bracket] : name;
        }

        private static string StripSamplerSuffix(string name) => name.EndsWith("Sampler", StringComparison.Ordinal)
            ? name[..^"Sampler".Length]
            : name;

        private static float NativeColorByteToLinear(byte value) => (float)Math.Pow(value / 255.0, 2.2);

        private static bool ResourceInputUsesConstant(StructProperty input, string name, ICollection<string> errors)
        {
            var flag = input?.Properties.FirstOrDefault(p => p.Name == "UseConstant");
            if (flag == null) return false;
            if (flag is BoolProperty boolean) return boolean.Value;
            if (flag is IntProperty integer) return (integer.Value & 1) != 0;
            errors.Add($"Material.{name}.UseConstant must be an integer or boolean flag.");
            return false;
        }

        private static void ValidateResourceConstant(StructProperty input, string name, ICollection<string> errors)
        {
            void RequireFloat(string field)
            {
                var value = input.Properties.FirstOrDefault(p => p.Name == field);
                if (value != null && value is not FloatProperty) errors.Add($"Material.{name}.{field} must be a float constant.");
            }
            if (name is "DiffusePower" or "SpecularPower" or "Opacity" or "OpacityMask" or "TwoSidedLightingMask")
            {
                RequireFloat("Constant");
                return;
            }
            if (name == "Distortion")
            {
                RequireFloat("ConstantX"); RequireFloat("ConstantY");
                return;
            }
            var constant = input.Properties.FirstOrDefault(p => p.Name == "Constant");
            if (constant == null) return;
            bool vector = name is "Normal" or "AnisotropicDirection" or "WrapLightingCoefficients" or "WorldPositionOffset";
            string expected = vector ? "Vector" : "Color";
            if (constant is not StructProperty structure || structure.StructType != expected)
            {
                errors.Add($"Material.{name}.Constant must be a {expected} struct.");
                return;
            }
            foreach (string channel in vector ? new[] { "X", "Y", "Z" } : new[] { "R", "G", "B", "A" })
            {
                var component = structure.Properties.FirstOrDefault(p => p.Name == channel);
                if (component != null && (vector ? component is not FloatProperty : component is not ByteProperty))
                    errors.Add($"Material.{name}.Constant.{channel} must be a {(vector ? "float" : "byte")} channel.");
            }
        }

        private static string FormatFloat(float value) => value.ToString("F8", CultureInfo.InvariantCulture);

        private static MaterialValueType ExpectedType(string propertyName) => propertyName switch
        {
            "DiffusePower" or "SpecularPower" or "Opacity" or "OpacityMask" => MaterialValueType.Scalar,
            "Distortion" => MaterialValueType.Vector2,
            _ => MaterialValueType.Vector3
        };

        private static string BuildUniformDeclarations(string numericType, int pixelVectorCount, int pixelScalarCount,
            int texture2DCount, int textureCubeCount, int vertexVectorCount, int vertexScalarCount)
        {
            var declarations = new StringBuilder();
            for (int i = 0; i < pixelVectorCount; i++)
                declarations.Append(numericType).Append("4 UniformPixelVector_").Append(i).Append(";\r\n");
            for (int i = 0; i < (pixelScalarCount + 3) / 4; i++)
                declarations.Append(numericType).Append("4 UniformPixelScalars_").Append(i).Append(";\r\n");
            for (int i = 0; i < texture2DCount; i++)
                declarations.Append("SAMPLER2D( Texture2D_").Append(i).Append(" );\r\n");
            for (int i = 0; i < textureCubeCount; i++)
                declarations.Append("SAMPLERCUBE( TextureCube_").Append(i).Append(" );\r\n");
            for (int i = 0; i < vertexVectorCount; i++)
                declarations.Append(numericType).Append("4 UniformVertexVector_").Append(i).Append(";\r\n");
            for (int i = 0; i < (vertexScalarCount + 3) / 4; i++)
                declarations.Append(numericType).Append("4 UniformVertexScalars_").Append(i).Append(";\r\n");
            return declarations.ToString();
        }

        private static string ApplyMaterialTemplate(string template, IReadOnlyDictionary<string, string> code,
            uint numUserTexCoords, string numericType, string uniformDeclarations, float opacityMaskClipValue)
        {
            string Scalar(float value) => $"{numericType}({FormatFloat(value)})";
            string Vector3(float x, float y, float z) =>
                $"{numericType}3({FormatFloat(x)},{FormatFloat(y)},{FormatFloat(z)})";
            string Get(string property, string fallback) => code.TryGetValue(property, out string value) ? value : "\treturn " + fallback;

            string mask = Get("OpacityMask", Scalar(1));
            int returnPosition = mask.LastIndexOf("\treturn ", StringComparison.Ordinal);
            string maskStatements = returnPosition > 0 ? mask[..returnPosition] : string.Empty;
            string maskValue = returnPosition >= 0 ? mask[(returnPosition + 8)..] : mask;

            int linePlaceholder = template.IndexOf("#line %u", StringComparison.Ordinal);
            if (linePlaceholder < 0) throw new FormatException("MaterialTemplate.usf is missing its #line %u placeholder.");
            uint templateLineNumber = (uint)(template.AsSpan(0, linePlaceholder).Count('\n') + 3);

            object[] arguments =
            [
                (uint)numUserTexCoords,
                uniformDeclarations,
                Get("Normal", Vector3(0, 0, 1)),
                Get("EmissiveColor", Vector3(0, 0, 0)),
                Get("DiffuseColor", Vector3(0, 0, 0)),
                Get("DiffusePower", Scalar(1)),
                Get("SpecularColor", Vector3(0, 0, 0)),
                Get("SpecularPower", Scalar(15)),
                "return " + opacityMaskClipValue.ToString("F5", CultureInfo.InvariantCulture),
                Get("Opacity", Scalar(1)),
                maskStatements,
                maskValue,
                Get("Distortion", $"{numericType}2({FormatFloat(0)},{FormatFloat(0)})"),
                Get("TwoSidedLightingMask", $"({Scalar(0)} * {Vector3(1, 1, 1)})"),
                Get("CustomLighting", Vector3(0, 0, 0)),
                Get("CustomLightingDiffuse", Vector3(0, 0, 0)),
                Get("AnisotropicDirection", Vector3(0, 1, 0)),
                Get("WorldPositionOffset", Vector3(0, 0, 0)),
                Get("WrapLightingCoefficients", Vector3(0, 0, 0)),
                templateLineNumber
            ];
            return UnrealPrintf(template, arguments);
        }

        private static string UnrealPrintf(string format, IReadOnlyList<object> arguments)
        {
            var result = new StringBuilder(format.Length + 1024);
            int argumentIndex = 0;
            for (int i = 0; i < format.Length; i++)
            {
                if (format[i] != '%')
                {
                    result.Append(format[i]);
                    continue;
                }
                if (++i >= format.Length) throw new FormatException("MaterialTemplate.usf ends with an incomplete format specifier.");
                if (format[i] == '%')
                {
                    result.Append('%');
                    continue;
                }
                if (argumentIndex >= arguments.Count) throw new FormatException("MaterialTemplate.usf has more placeholders than expected.");
                object argument = arguments[argumentIndex++];
                switch (format[i])
                {
                    case 's': result.Append(argument as string ?? throw new FormatException("Expected a string template argument.")); break;
                    case 'u': result.Append(Convert.ToUInt32(argument, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)); break;
                    default: throw new FormatException($"MaterialTemplate.usf contains unsupported %{format[i]} placeholder.");
                }
            }
            if (argumentIndex != arguments.Count) throw new FormatException($"MaterialTemplate.usf consumed {argumentIndex} of {arguments.Count} expected arguments.");
            return result.ToString();
        }

        /// <summary>
        /// Builds the material-only portion of a provisional LE3 shader environment. Blend, lighting,
        /// and two-sided rules mirror ShaderClient's tested reconstruction; WORLD_COORDS incorporates
        /// confirmed unmirror usage as well as corpus-derived rules. LIGHTMAP_UV_ACCESS reflects
        /// the translated lightmap-UV request. Shader-type and vertex-factory defines are separate.
        /// Do not treat this result as a complete environment for bytecode compilation.
        /// </summary>
        public IReadOnlyDictionary<string, string> BuildProvisionalMaterialMacros(MaterialResource resource, GraphAnalysis graph)
        {
            ArgumentNullException.ThrowIfNull(graph);
            return BuildProvisionalMaterialMacrosCore(resource, graph,
                graph.ReachableExpressions.Any(x => x.ClassName == "MaterialExpressionVertexColor"),
                InferGraphTransformUsage(graph), graph.ReachableExpressions.Any(x => x.ClassName == "MaterialExpressionLightmapUVs"));
        }

        /// <summary>Uses the current translation's permutation-specific usage state, not a parent's cooked resource flags.</summary>
        public IReadOnlyDictionary<string, string> BuildProvisionalMaterialMacros(MaterialResource resource, TranslationResult translation)
        {
            ValidateTranslation(translation);
            return BuildProvisionalMaterialMacrosCore(resource, translation.Graph, translation.UsesVertexColor, translation.UsingTransforms, translation.UsesLightmapUVs);
        }

        private IReadOnlyDictionary<string, string> BuildProvisionalMaterialMacrosCore(MaterialResource resource, GraphAnalysis graph,
            bool usesVertexColor, uint usingTransforms, bool usesLightmapUVs)
        {
            ArgumentNullException.ThrowIfNull(resource);
            ArgumentNullException.ThrowIfNull(graph);
            if (!graph.IsValid)
            {
                throw new InvalidOperationException("Cannot build a material environment from an invalid expression graph.");
            }

            var properties = EffectiveMaterialProperties();
            var macros = new Dictionary<string, string>(StringComparer.Ordinal);
            string blendMode = properties.GetProp<EnumProperty>("BlendMode")?.Value.Name ?? "BLEND_Opaque";
            string lightingModel = properties.GetProp<EnumProperty>("LightingModel")?.Value.Name ?? "MLM_Phong";

            macros.Add(blendMode switch
            {
                "BLEND_Opaque" => "MATERIALBLENDING_SOLID",
                "BLEND_Masked" => "MATERIALBLENDING_MASKED",
                "BLEND_SoftMasked" => "MATERIALBLENDING_SOFTMASKED",
                "BLEND_Translucent" => "MATERIALBLENDING_TRANSLUCENT",
                // Premultiplied alpha: shipping bytecode uses the additive output (Color * Fog.a * Opacity,
                // alpha 0, luminance DoF opacity); the composite blend state is applied outside the shader.
                "BLEND_AlphaComposite" or "BLEND_Additive" => "MATERIALBLENDING_ADDITIVE",
                "BLEND_Modulate" => "MATERIALBLENDING_MODULATE",
                _ => throw new NotSupportedException($"Unknown LE3 blend mode {blendMode}.")
            }, "1");

            macros.Add(lightingModel switch
            {
                "MLM_SHPRT" or "MLM_Phong" => "MATERIAL_LIGHTINGMODEL_PHONG",
                "MLM_NonDirectional" => "MATERIAL_LIGHTINGMODEL_NONDIRECTIONAL",
                "MLM_Unlit" => "MATERIAL_LIGHTINGMODEL_UNLIT",
                "MLM_Custom" => "MATERIAL_LIGHTINGMODEL_CUSTOM",
                "MLM_Anisotropic" => "MATERIAL_LIGHTINGMODEL_ANISOTROPIC",
                _ => throw new NotSupportedException($"Unknown LE3 lighting model {lightingModel}.")
            }, "1");

            if (properties.GetProp<BoolProperty>("TwoSided")?.Value == true)
            {
                macros.Add("MATERIAL_TWOSIDED", "1");
            }

            // Ordinary LE3 getters read these raw fields. Separate-pass is independent of
            // TwoSided and blend mode (0x7ff74ab54130).
            if (properties.GetProp<BoolProperty>("TwoSidedSeparatePass")?.Value == true)
                macros.Add("MATERIAL_TWOSIDED_SEPARATE_PASS", "1");
            if (properties.GetProp<BoolProperty>("bUsedWithGammaCorrection")?.Value == true)
                macros.Add("MATERIAL_USE_GAMMA_CORRECTION", "1");
            // The shipping environment code is stripped. Shipping bytecode selects the one-layer branch
            // from UMaterial+0x3c0 bit5 alone (no bUsesDistortion, no connected Distortion). The base-pass
            // branch only assigns a color for translucent/additive/modulate output, so other modes never set it.
            if (properties.GetProp<BoolProperty>("bUseOneLayerDistortion")?.Value == true &&
                macros.Keys.Any(k => k is "MATERIALBLENDING_TRANSLUCENT" or "MATERIALBLENDING_ADDITIVE" or "MATERIALBLENDING_MODULATE"))
                macros.Add("MATERIAL_ONELAYERDISTORTION", "1");
            // NumWounds (0x7ff74ab534b0) returns the signed UMaterial int unmodified.
            // Current ShaderClient emits positive values only, without a guessed upper clamp.
            int numWounds = properties.GetProp<IntProperty>("nNumWounds")?.Value ?? 0;
            if (numWounds > 0) macros.Add("NUM_WOUNDS", numWounds.ToString(CultureInfo.InvariantCulture));

            // LE3 vertex-color usage is set by the translator primitive, not a UMaterial flag.
            // Never inherit a stale parent's bUsesVertexColor from the supplied cooked resource.
            if (usesVertexColor) macros.Add("MATERIAL_USES_VERTEX_COLOR", "1");

            // Recovered material environment names, LE3 getter predicates, and ShaderClient's
            // current reconstruction agree on these consumers. Unset macros default to zero.
            var cacheContext = BuildShouldCacheMaterialContext(properties);
            if (cacheContext.UsesLitTranslucencyDepthPass) macros.Add("MATERIAL_LIT_TRANSLUCENCY_PREPASS", "1");
            if (cacheContext.UsesLitTranslucencyDepthPostpass) macros.Add("MATERIAL_LIT_TRANSLUCENCY_DEPTH_POSTPASS", "1");
            if (cacheContext.UsesSurfaceTranslucencyDepthPass) macros.Add("MATERIAL_TRANSLUCENT_SURFACE_PREPASS", "1");
            if (cacheContext.CastLitTranslucencyShadowAsMasked) macros.Add("MATERIAL_CAST_LIT_TRANSLUCENCY_SHADOW_AS_MASKED", "1");

            bool isDecal = IsUsedWithDecals();
            if (isDecal)
            {
                macros.Add("MATERIAL_DECAL", "1");
            }

            // 0x1 World, 0x2 View, 0x4 Local. The special-engine clause is measured on the
            // ProjectMilan LE3 shader corpus; the original editor-side rule is not in shipping LE3.
            // Decals are a separate measured clause and require the world-space uniforms too.
            if (((resource.UsingTransforms | usingTransforms) & 0x7) != 0 || isDecal ||
                properties.GetProp<BoolProperty>("bUsedAsSpecialEngineMaterial")?.Value == true)
            {
                macros.Add("WORLD_COORDS", "1");
            }
            if (((resource.UsingTransforms | usingTransforms) & 0x8) != 0)
            {
                macros.Add("WORLD_POS", "1");
            }
            if (usesLightmapUVs)
            {
                macros.Add("LIGHTMAP_UV_ACCESS", "1");
            }

            return macros;
        }

        private bool IsUsedWithDecals() => EffectiveMaterialProperties()
            .GetProp<BoolProperty>("bUsedWithDecals")?.Value ?? material.ClassName == "DecalMaterial";

        // Graph-only planning is provisional and may include unsupported nodes. This conservatively
        // infers unmirror usage from selected nodes; the translation overload has exact emitted state.
        private uint InferGraphTransformUsage(GraphAnalysis graph)
        {
            bool hasCoordinateData = IsUsedWithDecals() || graph.ReachableExpressions.Any(expression =>
                (expression.ClassName == "MaterialExpressionTextureCoordinate" &&
                 unchecked((uint)(expression.GetProperty<IntProperty>("CoordinateIndex")?.Value ?? 0) + 1u) != 0) ||
                expression.ClassName.StartsWith("MaterialExpressionTextureSample", StringComparison.Ordinal));
            uint flags = hasCoordinateData && graph.ReachableExpressions.Any(expression =>
                expression.ClassName == "MaterialExpressionTextureCoordinate" &&
                (expression.GetProperty<BoolProperty>("UnMirrorU")?.Value == true ||
                 expression.GetProperty<BoolProperty>("UnMirrorV")?.Value == true)) ? 1u : 0u;
            // WorldPosition, ReflectionVector and Transform usage is pixel-only. Traverse selected roots
            // separately so vertex-only/shared static branches cannot fabricate usage flags.
            var visited = new HashSet<int>();
            var properties = EffectiveMaterialProperties();
            bool perPixelCameraVector = properties.GetProp<BoolProperty>("bPerPixelCameraVector")?.Value == true;
            foreach (string property in graph.IncludesMaterialResourceInputs ? MaterialInputs.Append("TwoSidedLightingColor") : MaterialInputs)
            {
                if (property == "WorldPositionOffset") continue;
                if (graph.IncludesMaterialResourceInputs && !graph.ConnectedInputs.ContainsKey(property)) continue;
                string name = graph.IncludesMaterialResourceInputs && property == "CustomLightingDiffuse" ? "CustomSkylightDiffuse" : property;
                flags |= PixelUsage(properties.GetProp<StructProperty>(name));
            }
            return flags;

            uint PixelUsage(StructProperty input)
            {
                int index = input?.GetProp<ObjectProperty>("Expression")?.Value ?? 0;
                if (index == 0 || !visited.Add(index) || !material.FileRef.TryGetEntry(index, out var entry) || entry is not ExportEntry expression)
                    return 0;
                if (expression.ClassName == "MaterialExpressionWorldPosition") return 8;
                if (expression.ClassName == "MaterialExpressionReflectionVector") return perPixelCameraVector ? 9u : 0u;
                uint usage = expression.ClassName == "MaterialExpressionTransform" ? 1u : 0u;
                foreach (var selectedInput in EnumerateSelectedInputs(expression, expression.GetProperties())) usage |= PixelUsage(selectedInput);
                return usage;
            }
        }

        /// <summary>
        /// Longest chain of dependent texture fetches over the selected graph (each compiled expression keeps the
        /// max of its inputs' lengths; a texture sample is its coordinate's + 1).
        /// Serialized as MaxTextureDependencyLength; editor data, no runtime reader.
        /// </summary>
        internal int ComputeMaxTextureDependencyLength(GraphAnalysis graph)
        {
            var lengths = new Dictionary<int, int>();
            int max = 0;
            foreach (var root in graph.ConnectedInputs.Values) max = Math.Max(max, Length(root, 0));
            return max;

            int Length(ExportEntry expression, int depth)
            {
                if (lengths.TryGetValue(expression.UIndex, out int known)) return known;
                if (depth > 512) return 0; // AnalyzeGraph reports cycles; never recurse forever here.
                int inputs = 0;
                foreach (var input in EnumerateSelectedInputs(expression, expression.GetProperties()))
                {
                    int index = input.GetProp<ObjectProperty>("Expression")?.Value ?? 0;
                    if (index != 0 && material.FileRef.TryGetEntry(index, out var entry) && entry is ExportEntry child)
                        inputs = Math.Max(inputs, Length(child, depth + 1));
                }
                // Depth-biased nodes read scene depth at the screen position: an independent fetch.
                int length = expression.ClassName is "MaterialExpressionDepthBiasedAlpha" or "MaterialExpressionDepthBiasedBlend"
                    ? Math.Max(inputs, 1) : FetchesTexture(expression) ? inputs + 1 : inputs;
                lengths[expression.UIndex] = length;
                return length;
            }

            // Nodes whose compile ends in a texture or scene-depth fetch.
            static bool FetchesTexture(ExportEntry expression) => expression.IsA("MaterialExpressionTextureSample") ||
                expression.ClassName is "BioMaterialExpressionKeyedFlipbook" or "BioMaterialExpressionDirt" or
                    "MaterialExpressionFontSample" or "MaterialExpressionFontSampleParameter" or "MaterialExpressionFluidNormal" or
                    "MaterialExpressionSceneTexture" or "MaterialExpressionDestDepth";
        }

        private IEnumerable<StructProperty> EnumerateSelectedInputs(ExportEntry expression, PropertyCollection properties)
        {
            if (expression.ClassName is "MaterialExpressionParticleSubUV" or "MaterialExpressionTextureSampleParameterSubUV" or "MaterialExpressionFluidNormal" or
                "MaterialExpressionComment" or "MaterialExpressionCompound" or "MaterialExpressionParameter" or
                "MaterialExpressionFontSample" or "MaterialExpressionFontSampleParameter" or "MaterialExpressionCustomTexture")
                yield break;
            if (expression.ClassName == "MaterialExpressionCustom")
            {
                if (properties.GetProp<Property>("Inputs") is ArrayProperty<StructProperty> inputs)
                    foreach (var customInput in inputs)
                    {
                        // Only an empty string is skipped; whitespace remains an authored name.
                        if (string.IsNullOrEmpty((customInput.GetProp<Property>("InputName") as StrProperty)?.Value)) continue;
                        if (customInput.GetProp<Property>("Input") is StructProperty input) yield return input;
                    }
                yield break;
            }
            if (expression.ClassName is "MaterialExpressionMeshSubUV" or "MaterialExpressionMeshSubUVBlend" or
                "MaterialExpressionTextureSampleParameterMeshSubUV" or "MaterialExpressionTextureSampleParameterMeshSubUVBlend" or
                "MaterialExpressionTextureSampleParameterMovie" or "MaterialExpressionFlipBookSample")
            {
                if (properties.GetProp<Property>("Coordinates") is StructProperty coordinates) yield return coordinates;
                yield break;
            }
            if (expression.ClassName == "BioMaterialExpressionEffectMatBaseProperty")
            {
                yield return BioBasePropertyInput(properties);
                yield break;
            }
            if (expression.ClassName == "MaterialExpressionStaticSwitchParameter")
            {
                bool selected = ResolveStaticSwitch(properties);
                var input = properties.GetProp<StructProperty>(selected ? "A" : "B");
                if (input != null) yield return input;
                yield break;
            }

            foreach (var property in properties)
            {
                if (property is StructProperty input && input.GetProp<ObjectProperty>("Expression") != null)
                {
                    yield return input;
                }
            }
        }

        // The effects bridge stores UMaterialExpression* directly. Adapt it without changing
        // the authored package; direct calls always use output zero and have no input mask.
        private static StructProperty BioBasePropertyInput(PropertyCollection properties) =>
            new("ExpressionInput", new PropertyCollection
            {
                new ObjectProperty((properties.GetProp<Property>("Input") as ObjectProperty)?.Value ?? 0, "Expression")
            }, "Input");

        // Non-normal Grayscale/OneBitAlpha/BC7 use the ordinary affine unpack path: byte-identical to shipping
        // RefShaderCache bytecode in the parity harness. Other special compressions remain unsupported.
        private static readonly string[] TextureCompressionSettingsNames =
        [
            "TC_Default", "TC_Normalmap", "TC_Displacementmap", "TC_NormalmapAlpha", "TC_Grayscale", "TC_HighDynamicRange",
            "TC_OneBitAlpha", "TC_NormalmapUncompressed", "TC_NormalmapBC5", "TC_OneBitMonochrome", "TC_NormalmapHQ",
            "TC_BC7", "TC_NormalmapBC7"
        ];

        private static bool IsSupportedUnpackCompression(string compression, string normalHelper) =>
            normalHelper != null || compression is "TC_Default" or "TC_NormalmapHQ" or "TC_Grayscale" or "TC_OneBitAlpha" or "TC_BC7";

        private static string TextureNormalHelper(string compression) => compression switch
        {
            "TC_Normalmap" => "BiasNormalizeNormalMap_DXT1",
            "TC_NormalmapAlpha" => "BiasNormalizeNormalMap_DXT5",
            "TC_NormalmapUncompressed" => "BiasNormalizeNormalMap_V8U8",
            "TC_NormalmapBC5" => "BiasNormalizeNormalMap_BC5",
            "TC_NormalmapBC7" => "BiasNormalizeNormalMap_BC7", _ => null
        };

        private bool ResolveStaticSwitch(PropertyCollection properties)
        {
            bool authoredDefault = properties.GetProp<BoolProperty>("DefaultValue")?.Value ?? false;
            NameReference parameterName = properties.GetProp<NameProperty>("ParameterName")?.Value ?? NameReference.None;
            // UMaterial::UpdateStaticExpressionParameters binds first name match (+number),
            // not first bOverride/GUID match. Compile at 0x7ff74ab185b0 reads Value directly.
            var parameter = staticParameters?.StaticSwitchParameters?.FirstOrDefault(p => p.ParameterName == parameterName);
            return parameter?.Value ?? authoredDefault;
        }
    }
}
