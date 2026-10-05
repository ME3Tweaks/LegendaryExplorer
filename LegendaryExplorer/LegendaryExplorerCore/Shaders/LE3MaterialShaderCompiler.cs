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

            public TextureCompilationMetadata(TextureDimension dimension, string compressionSettings = "TC_Default",
                Vector4? unpackMin = null, Vector4? unpackMax = null)
            {
                if (!Enum.IsDefined(dimension)) throw new ArgumentOutOfRangeException(nameof(dimension));
                ArgumentException.ThrowIfNullOrWhiteSpace(compressionSettings);
                Dimension = dimension;
                CompressionSettings = compressionSettings;
                UnpackMin = unpackMin ?? Vector4.Zero;
                UnpackMax = unpackMax ?? Vector4.One;
            }

            /// <summary>
            /// Snapshots an already-read LE3 property collection; does not access an export's data.
            /// Missing properties have the same defaults as the loaded-export translation path.
            /// The caller is responsible for supplying the correct effective texture properties.
            /// </summary>
            public static TextureCompilationMetadata FromProperties(TextureDimension dimension, PropertyCollection properties)
            {
                ArgumentNullException.ThrowIfNull(properties);
                return new TextureCompilationMetadata(dimension,
                    properties.GetProp<EnumProperty>("CompressionSettings")?.Value.Name ?? "TC_Default",
                    ReadRange("UnpackMin", 0f), ReadRange("UnpackMax", 1f));

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
                if (texture.Game != MEGame.LE3 || (!texture.IsA("Texture2D") && !texture.IsA("TextureCube")))
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

            public ResolvedMaterialCompilationInput(ExportEntry baseMaterial, StaticParameterSet resolvedStaticParameters = null,
                ExportEntry requestedMaterialInstance = null)
            {
                ArgumentNullException.ThrowIfNull(baseMaterial);
                if (baseMaterial.Game != MEGame.LE3 || !baseMaterial.IsA("Material"))
                    throw new ArgumentException("An LE3 base Material export is required.", nameof(baseMaterial));
                if (requestedMaterialInstance != null && (requestedMaterialInstance.Game != MEGame.LE3 ||
                    !requestedMaterialInstance.IsA("MaterialInstance")))
                    throw new ArgumentException("The optional requested export must be an LE3 MaterialInstance.", nameof(requestedMaterialInstance));
                BaseMaterial = baseMaterial;
                RequestedMaterialInstance = requestedMaterialInstance;
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
        public LE3MaterialShaderCompiler(ExportEntry material, StaticParameterSet staticParameters = null,
            Func<ImportEntry, ExportEntry> resolveTextureImport = null,
            Func<IEntry, TextureCompilationMetadata> getTextureMetadata = null)
            : this(new ResolvedMaterialCompilationInput(material, staticParameters), resolveTextureImport, getTextureMetadata)
        {
        }

        public LE3MaterialShaderCompiler(ResolvedMaterialCompilationInput input,
            Func<ImportEntry, ExportEntry> resolveTextureImport = null,
            Func<IEntry, TextureCompilationMetadata> getTextureMetadata = null)
        {
            CompilationInput = input ?? throw new ArgumentNullException(nameof(input));
            material = input.BaseMaterial;
            staticParameters = input.GetStaticParametersSnapshot();
            this.resolveTextureImport = resolveTextureImport;
            this.getTextureMetadata = getTextureMetadata;
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
            /// <summary>Selected translation emitted a GameTime or RealTime uniform; not a cooked-resource flag mapping.</summary>
            public bool UsesTime { get; internal init; }
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
            Add("FCustomSimpleLightMapTexturePolicy", "Cone", "Constant");
            Add("FCustomSimpleVertexLightMapPolicy", "Cone", "Constant");
            Add("FCustomVectorLightMapTexturePolicy", "Cone", "Constant");
            Add("FCustomVectorVertexLightMapPolicy", "Cone", "Constant", "LinearHalfspace");
            Add("FDirectionalLightLightMapPolicy", "Cone", "Constant");
            Add("FDirectionalLightMapTexturePolicy", "Cone", "Constant");
            Add("FDirectionalVertexLightMapPolicy", "Cone", "Constant");
            Add("FNoLightMapPolicy", "Cone");
            Add("FSHLightLightMapPolicy", "Cone", "Constant");
            Add("FSimpleLightMapTexturePolicy", "Cone");
            Add("FSimpleVertexLightMapPolicy", "Cone");
            return result;
        }
        private static readonly IReadOnlyList<VertexFactoryRegistration> KnownVertexFactories = Array.AsReadOnly(new[]
        {
            new VertexFactoryRegistration("FLocalVertexFactory", "LocalVertexFactory", true, true, true, true),
            new VertexFactoryRegistration("FGPUSkinVertexFactory", "GpuSkinVertexFactory", true, false, true, false),
            new VertexFactoryRegistration("FGPUSkinMorphVertexFactory", "GpuSkinVertexFactory", true, false, true, false),
            new VertexFactoryRegistration("FInstancedStaticMeshVertexFactory", "LocalVertexFactory", true, true, true, true)
        });

        /// <summary>Snapshot of the recovered subset; not the engine's complete registration list.</summary>
        public static IReadOnlyList<ShaderTypeRegistration> GetKnownShaderTypeRegistrations()
            => Array.AsReadOnly(KnownShaderTypes.Values.OrderBy(r => r.Name, StringComparer.Ordinal).ToArray());

        /// <summary>Recovered vertex-factory subset; not the engine's complete registration list.</summary>
        public static IReadOnlyList<VertexFactoryRegistration> GetKnownVertexFactoryRegistrations() => KnownVertexFactories;

        public static bool TryGetKnownVertexFactoryRegistration(string name, out VertexFactoryRegistration registration)
        {
            ArgumentNullException.ThrowIfNull(name);
            registration = GetKnownVertexFactoryRegistrations().FirstOrDefault(r => r.Name == name);
            return registration != null;
        }

        /// <summary>
        /// Initial explicit registration inventory, verified against LE3 11.3.2 static initializers
        /// and packed descriptors. This is not the complete engine registration list.
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
            if (!TryGetKnownShaderTypeRegistration(shaderTypeName, out var shader) ||
                !TryGetKnownVertexFactoryRegistration(vertexFactoryTypeName, out var factory)) return false;

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
            if (foggedEnvironment != null)
                simpleLightmap = foggedEnvironment.LightmapPolicy is "FSimpleLightMapTexturePolicy" or "FSimpleVertexLightMapPolicy" ||
                    foggedEnvironment is { LightmapPolicy: "FCustomSimpleVertexLightMapPolicy", Density: "Constant" };
            Add("NUM_LIGHTMAP_COEFFICIENTS", simpleLightmap ? "1" : "2");

            // Explicit identities only: neither class-name substring matching nor shader-source
            // defaults constitute evidence of an engine registration's environment.
            switch (shaderTypeName)
            {
                case var foggedName when foggedEnvironment != null:
                    Add("FOGVOLUMEDENSITY_" + (foggedEnvironment.Density switch
                    {
                        "Cone" => "CONEDENSITY", "Constant" => "CONSTANT", "LinearHalfspace" => "LINEARHALFSPACE",
                        _ => throw new InvalidOperationException("Unverified fog density.")
                    }), evidence: ShaderEnvironmentEvidence.LE3RegistrationCallback);
                    // These extras reproduce exact ShaderClient policy injections, not NoDensity templates.
                    if (foggedEnvironment.LightmapPolicy is "FDirectionalLightLightMapPolicy" or "FSHLightLightMapPolicy")
                        Add("ENABLE_DIRECTIONAL_LIGHT");
                    if (foggedEnvironment.LightmapPolicy == "FDirectionalVertexLightMapPolicy") Add("VERTEX_LIGHTMAP");
                    if (foggedEnvironment.LightmapPolicy == "FSimpleVertexLightMapPolicy" ||
                        foggedEnvironment is { LightmapPolicy: "FCustomSimpleVertexLightMapPolicy", Density: "Constant" })
                        Add("SIMPLE_VERTEX_LIGHTMAP");
                    if (foggedEnvironment.LightmapPolicy == "FSimpleLightMapTexturePolicy" ||
                        foggedEnvironment.Density == "Constant" && foggedEnvironment.LightmapPolicy is
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
                case "TLightVertexShaderFSphericalHarmonicLightPolicyFShadowVertexBufferPolicy":
                    Add("STATICLIGHTING_VERTEXMASK", evidence: ShaderEnvironmentEvidence.LE3RegistrationCallback);
                    break;
                case "FVelocityVertexShader":
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
            if (factory.Name is "FGPUSkinVertexFactory" or "FGPUSkinMorphVertexFactory")
                Add("QUAT_SKINNING", "0", ShaderEnvironmentEvidence.LE3RegistrationCallback);
            if (factory.Name == "FGPUSkinMorphVertexFactory")
                Add("GPUSKIN_MORPH_BLEND", "1", ShaderEnvironmentEvidence.LE3RegistrationCallback);
            if (factory.Name == "FInstancedStaticMeshVertexFactory")
                Add("USE_INSTANCING", "1", ShaderEnvironmentEvidence.LE3RegistrationCallback);

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
                or "TLightPixelShaderFSphericalHarmonicLightPolicyFShadowVertexBufferPolicy"
                or "TLightPixelShaderFSphericalHarmonicLightPolicyFSignedDistanceFieldShadowTexturePolicy" ||
                KnownFoggedBasePassVertexTypes.ContainsKey(shaderTypeName) ||
                factory.Name is "FGPUSkinMorphVertexFactory" or "FInstancedStaticMeshVertexFactory"
                ? ShaderEnvironmentEvidence.ShaderClientReconstruction
                : ShaderEnvironmentEvidence.ShippingBytecode;
            environment = new KnownPermutationEnvironment
            {
                Target = shader.CreateTarget(flags, factory.Name, factory.SourceFileName),
                CompileFlagsEvidence = flagEvidence,
                MacroDefinitions = definitions.AsReadOnly(),
                VertexFactoryContext = factory.CreateCacheContext()
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
            ShouldCacheVertexFactoryContext VertexFactoryContext = null);

        /// <summary>
        /// Selection and provisional jobs for an explicit candidate list. Success means that every
        /// candidate was resolved, not that the list covers every engine registration. On any error,
        /// Jobs is empty so an unresolved permutation cannot silently produce a partial shader set.
        /// </summary>
        public sealed class ShaderCompilePlan
        {
            public ResolvedMaterialCompilationInput CompilationInput { get; internal init; }
            public IReadOnlyList<ShaderCompileJob> Jobs { get; internal init; }
            public IReadOnlyList<ShaderCompileTarget> ExcludedTargets { get; internal init; }
            public IReadOnlyList<string> Errors { get; internal init; }
            public IReadOnlyList<ShaderCompileDecision> Decisions { get; internal init; }
            public bool Success => Errors.Count == 0;
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
        /// It never certifies coverage of the engine registry and must not be installed as a complete
        /// shader set. Shader/VF source CRC maps and material-resource installation remain separate.
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
            public bool IsCompleteShaderSet => false;
        }

        /// <summary>A detached, serializable cache subset, never proof of a complete engine shader set.</summary>
        public sealed class ShaderCacheFragmentResult
        {
            public ShaderCache Cache { get; internal init; }
            public IReadOnlyList<int> UniformExpressionTextures { get; internal init; } = [];
            public IReadOnlyList<string> Errors { get; internal init; } = [];
            public bool Success => Cache != null && Errors.Count == 0;
            public bool IsCompleteShaderSet => false;
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
        /// is translation-derived; planners populate it when constructing their own context, while
        /// callers supplying an explicit context must provide the matching translated count.
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
            bool UsedWithInstancedMeshes = false);

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
        /// Builds the ordinary LE3 FMaterialResource view used by recovered ShouldCache callbacks.
        /// <paramref name="effectiveProperties"/> may supply already-resolved archetype/class defaults;
        /// otherwise LEC resolves the material's archetype chain. The stored bIsMasked flag is read
        /// directly, matching LE3's resource getter even if an edited graph would imply a new value.
        /// </summary>
        public ShouldCacheMaterialContext BuildShouldCacheMaterialContext(PropertyCollection effectiveProperties = null)
        {
            if (material.ClassName is not ("Material" or "DecalMaterial"))
            {
                throw new NotSupportedException($"ShouldCache context construction for {material.ClassName} has not been verified in LE3.");
            }

            var properties = effectiveProperties ?? material.GetPropertiesIncludingInherited();
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
                UsedWithInstancedMeshes: properties.GetProp<BoolProperty>("bUsedWithInstancedMeshes")?.Value == true);
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
                case "TLightVertexShaderFSphericalHarmonicLightPolicyFShadowTexturePolicy":
                case "TLightPixelShaderFSphericalHarmonicLightPolicyFShadowTexturePolicy":
                case "TLightVertexShaderFSphericalHarmonicLightPolicyFShadowVertexBufferPolicy":
                case "TLightPixelShaderFSphericalHarmonicLightPolicyFShadowVertexBufferPolicy":
                case "TLightPixelShaderFSphericalHarmonicLightPolicyFSignedDistanceFieldShadowTexturePolicy":
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
                    // b169fe0: ordinary FMaterialResource slot+0xa8 is the shared return-zero
                    // stub. Its derived-resource meaning is not inferred by this ordinary path.
                    shouldCache = materialContext.IsSpecialEngineMaterial || materialContext.IsMasked ||
                        (materialContext.IsTwoSided && !IsTranslucentBlend(materialContext.BlendMode));
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
        /// </summary>
        public static bool TryEvaluateKnownVertexFactoryShouldCache(string vertexFactoryTypeName,
            ShouldCacheMaterialContext materialContext, ShouldCacheVertexFactoryContext vertexFactoryContext,
            out bool shouldCache)
        {
            ArgumentNullException.ThrowIfNull(vertexFactoryTypeName);
            ArgumentNullException.ThrowIfNull(materialContext);
            ArgumentNullException.ThrowIfNull(vertexFactoryContext);

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
        /// CustomSkylightDiffuse/TwoSidedLightingColor routes. Does not certify shader completeness.</summary>
        public GraphAnalysis AnalyzeMaterialResourceGraph() => AnalyzeGraphCore(true);

        private GraphAnalysis AnalyzeGraphCore(bool includeMaterialResourceInputs)
        {
            var connected = new Dictionary<string, ExportEntry>(StringComparer.Ordinal);
            var reachable = new List<ExportEntry>();
            var visited = new HashSet<int>();
            var active = new HashSet<int>();
            var errors = new List<string>();
            var materialProperties = material.GetProperties();

            foreach (string name in includeMaterialResourceInputs
                         ? MaterialInputs.Where(n => n != "WorldPositionOffset").Append("TwoSidedLightingColor") : MaterialInputs)
            {
                var input = materialProperties.GetProp<StructProperty>(includeMaterialResourceInputs && name == "CustomLightingDiffuse" ? "CustomSkylightDiffuse" : name);
                if (includeMaterialResourceInputs && ResourceInputUsesConstant(input, name, errors))
                {
                    ValidateResourceConstant(input, name, errors);
                    continue;
                }
                if (input == null || input.GetProp<ObjectProperty>("Expression")?.Value is not int index || index == 0)
                {
                    continue;
                }

                var expression = Resolve(index, $"Material.{name}");
                if (expression == null) continue;
                connected.Add(name, expression);
                Visit(expression);
            }

            var unreachable = new List<ExportEntry>();
            var authoredExpressions = materialProperties.GetProp<ArrayProperty<ObjectProperty>>("Expressions");
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
        /// WorldPositionOffset has no case in the verified ordinary resource dispatch; connected
        /// requests fail closed until that path has been verified rather than silently compiling it.</summary>
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
            var effectiveMaterialProperties = material.GetPropertiesIncludingInherited();
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
            var materialProperties = material.GetProperties();
            var uniformExpressionPool = new List<MaterialUniformExpression>();
            var uniformPixelVectors = new List<MaterialUniformExpression>();
            var uniformPixelScalars = new List<MaterialUniformExpression>();
            var uniform2DTextures = new List<MaterialUniformExpressionTexture>();
            var uniformCubeTextures = new List<MaterialUniformExpressionTexture>();
            var uniformExpressionTextures = new List<int>();
            // Per-translation only: repeated samples share one metadata snapshot, but a later
            // translation can observe updated inputs without a stale compiler-global cache.
            var textureMetadata = new Dictionary<int, TextureCompilationMetadata>();
            var uniformVertexVectors = new List<MaterialUniformExpression>();
            var uniformVertexScalars = new List<MaterialUniformExpression>();
            bool compilingVertexProperty = false;
            bool usesVertexColor = false;
            bool usesTime = false;

            if (includeMaterialResourceInputs && materialProperties.GetProp<StructProperty>("WorldPositionOffset")?.GetProp<ObjectProperty>("Expression")?.Value is > 0)
                errors.Add("Connected WorldPositionOffset is not verified in the ordinary LE3 material-resource input dispatch.");

            foreach (string propertyName in MaterialInputs)
            {
                if (includeMaterialResourceInputs && propertyName == "WorldPositionOffset") continue;
                var input = materialProperties.GetProp<StructProperty>(includeMaterialResourceInputs && propertyName == "CustomLightingDiffuse" ? "CustomSkylightDiffuse" : propertyName);
                if (!includeMaterialResourceInputs && (input?.GetProp<ObjectProperty>("Expression")?.Value is not int expressionIndex || expressionIndex == 0))
                {
                    continue;
                }

                compilingVertexProperty = propertyName == "WorldPositionOffset";
                var statements = new List<string>();
                var cache = new Dictionary<(int UIndex, int OutputIndex), CodeValue>();
                CodeValue code;
                if (includeMaterialResourceInputs)
                {
                    // CompileProperty creates and RGB-masks this parameter before every pixel
                    // root, even when that root never consumes it. Mask access packs its slot.
                    var selection = ComponentMask(VectorParameter("SelectionColor", LinearColor.Black), true, true, true, false, propertyName);
                    if (propertyName == "DiffuseColor")
                    {
                        var factor = CompileArithmetic(Constant(1), selection, "Subtract", propertyName, statements);
                        code = CompileResourceInput(input, propertyName, statements, cache);
                        if (code != null) code = CompileArithmetic(ForceCastDefault(code, MaterialValueType.Vector3), factor, "Multiply", propertyName, statements);
                    }
                    else if (propertyName == "TwoSidedLightingMask")
                    {
                        var color = CompileResourceInput(materialProperties.GetProp<StructProperty>("TwoSidedLightingColor"), "TwoSidedLightingColor", statements, cache);
                        code = CompileResourceInput(input, propertyName, statements, cache);
                        if (code != null && color != null) code = CompileArithmetic(ForceCastDefault(code, MaterialValueType.Scalar), color, "Multiply", propertyName, statements);
                    }
                    else
                    {
                        code = CompileResourceInput(input, propertyName, statements, cache);
                        if (code != null && propertyName == "EmissiveColor")
                            code = CompileArithmetic(ForceCastDefault(code, MaterialValueType.Vector3), selection, "Add", propertyName, statements);
                    }
                }
                else code = CompileInput(input, propertyName, statements, cache);
                if (code == null) continue;

                MaterialValueType expected = ExpectedType(propertyName);
                // Translate ab58be0 invokes ForceCast with exact-match=false and replication=false
                // for every property. MCT_Float=0xf intersects all numeric targets; other widths
                // truncate inline or zero-pad, retaining the source uniform layout/type.
                code = ForceCastDefault(code, expected);

                var builder = new StringBuilder();
                foreach (string statement in statements) builder.Append(statement);
                builder.Append("\treturn ").Append(code.Code);
                propertyCode[propertyName] = builder.ToString();
            }

            string materialHlsl = null;
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
                UsesTime = usesTime,
                MaterialHlsl = materialHlsl
            };

            CodeValue VectorParameter(NameReference name, LinearColor defaultValue)
            {
                var uniform = uniformExpressionPool.OfType<MaterialUniformExpressionVectorParameter>()
                    .FirstOrDefault(p => p.ParameterName == name && p.DefaultValue.R == defaultValue.R &&
                        p.DefaultValue.G == defaultValue.G && p.DefaultValue.B == defaultValue.B && p.DefaultValue.A == defaultValue.A);
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

            CodeValue CompileResourceInput(StructProperty input, string name, List<string> statements, Dictionary<(int UIndex, int OutputIndex), CodeValue> cache)
            {
                bool constant = ResourceInputUsesConstant(input, name, errors);
                if (!constant && input?.GetProp<ObjectProperty>("Expression")?.Value is > 0)
                {
                    var compiled = CompileInput(input, name, statements, cache);
                    if (compiled != null) return compiled;
                }
                if (name is "DiffusePower" or "SpecularPower" or "Opacity" or "OpacityMask" or "TwoSidedLightingMask")
                    return Constant(constant ? input.GetProp<FloatProperty>("Constant")?.Value ?? 0f : name == "SpecularPower" ? 15f : name == "TwoSidedLightingMask" ? 0f : 1f);
                if (name == "Distortion")
                    return Vector(MaterialValueType.Vector2, constant ? [input.GetProp<FloatProperty>("ConstantX")?.Value ?? 0f, input.GetProp<FloatProperty>("ConstantY")?.Value ?? 0f] : [0f, 0f]);
                if (name is "Normal" or "AnisotropicDirection" or "WrapLightingCoefficients")
                {
                    var vector = constant ? input.GetProp<StructProperty>("Constant") : null;
                    float x = vector?.GetProp<FloatProperty>("X")?.Value ?? 0f;
                    float y = vector?.GetProp<FloatProperty>("Y")?.Value ?? (!constant && name == "AnisotropicDirection" ? 1f : 0f);
                    float z = vector?.GetProp<FloatProperty>("Z")?.Value ?? (!constant && name == "Normal" ? 1f : 0f);
                    return Vector(MaterialValueType.Vector3, [x, y, z]);
                }
                // Color input helper ab49c60 uses the native Pow22 byte table, not sRGB.
                var color = constant ? input.GetProp<StructProperty>("Constant") : null;
                byte fallback = name is "DiffuseColor" or "SpecularColor" ? (byte)128 : name == "TwoSidedLightingColor" ? (byte)255 : (byte)0;
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
                    : alpha.Type == MaterialValueType.Scalar ? $"{numericType}({alpha.Code})" : null;
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

            CodeValue CompileTime(bool realTime)
            {
                usesTime = true;
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

            CodeValue ForceCastDefault(CodeValue value, MaterialValueType target)
            {
                int nativeTarget = target switch { MaterialValueType.Scalar => 1, MaterialValueType.Vector2 => 2,
                    MaterialValueType.Vector3 => 4, _ => 8 };
                if ((value.NativeNumericType & nativeTarget) != 0) return value;
                string code = value.Code; // Dynamic uniforms must be accessed before the inline cast.
                int sourceWidth = (int)value.Type, targetWidth = (int)target;
                if (sourceWidth > targetWidth) code += "." + "rgba"[..targetWidth];
                else if (sourceWidth < targetWidth)
                    code = DescribeType(target) + "(" + code + string.Concat(Enumerable.Repeat(",0", targetWidth - sourceWidth)) + ")";
                return new CodeValue(target, code, true, ExplicitNativeNumericType: nativeTarget);
            }

            CodeValue CompileDefaultTextureCoordinate(List<string> statements)
            {
                if (compilingVertexProperty)
                {
                    errors.Add("Invalid node used in vertex shader input!");
                    return null;
                }
                numUserTexCoords = Math.Max(numUserTexCoords, 1u);
                string local = $"Local{localIndex++}";
                statements.Add($"\t{numericType}2 {local} = Parameters.TexCoords[0].xy;\r\n");
                return new CodeValue(MaterialValueType.Vector2, local, true);
            }

            CodeValue CompileInput(StructProperty input, string source, List<string> statements,
                Dictionary<(int UIndex, int OutputIndex), CodeValue> cache)
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
                if (value == null || !ReadInputMaskFlag(input, "Mask")) return value;
                return ApplyComponentMask(value, input, source);
            }

            CodeValue CompileExpression(ExportEntry expression, int outputIndex, string source, List<string> statements,
                Dictionary<(int UIndex, int OutputIndex), CodeValue> cache)
            {
                if (cache.TryGetValue((expression.UIndex, outputIndex), out var existing)) return existing;
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
                        var uniform = uniformExpressionPool.OfType<MaterialUniformExpressionScalarParameter>()
                            .FirstOrDefault(candidate => candidate.ParameterName == parameterName &&
                                                         candidate.DefaultValue == defaultValue);
                        if (uniform == null)
                        {
                            uniform = new MaterialUniformExpressionScalarParameter
                            {
                                ExpressionType = "FMaterialUniformExpressionScalarParameter",
                                ParameterName = parameterName,
                                DefaultValue = defaultValue
                            };
                            uniformExpressionPool.Add(uniform);
                        }

                        value = UniformValue(MaterialValueType.Scalar, null, uniform, isConstant: false);
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
                        float a = meshEmitter ? 1f : defaultValue?.GetProp<FloatProperty>("A")?.Value ?? 0f;
                        var uniform = uniformExpressionPool.OfType<MaterialUniformExpressionVectorParameter>()
                            .FirstOrDefault(candidate => candidate.ParameterName == parameterName &&
                                                         candidate.DefaultValue.R == r && candidate.DefaultValue.G == g &&
                                                         candidate.DefaultValue.B == b && candidate.DefaultValue.A == a);
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
                    case "MaterialExpressionTextureSample":
                    case "MaterialExpressionTextureSampleParameter":
                    case "MaterialExpressionTextureSampleParameter2D":
                    case "MaterialExpressionTextureSampleParameterCube":
                    case "MaterialExpressionTextureSampleParameterNormal":
                    {
                        if (compilingVertexProperty)
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
                        bool isParameter = expression.ClassName != "MaterialExpressionTextureSample";
                        NameReference parameterName = properties.GetProp<NameProperty>("ParameterName")?.Value ?? "None";
                        isParameter &= parameterName != NameReference.None;

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
                                compression = normalOverride.CompressionSettings switch
                                {
                                    0 => "TC_Default",
                                    1 => "TC_Normalmap",
                                    3 => "TC_NormalmapAlpha",
                                    7 => "TC_NormalmapUncompressed",
                                    8 => "TC_NormalmapBC5",
                                    10 => "TC_NormalmapHQ",
                                    12 => "TC_NormalmapBC7",
                                    _ => $"TC_#{normalOverride.CompressionSettings}"
                                };
                            }
                        }

                        string normalHelper = compression switch
                        {
                            "TC_Normalmap" => "BiasNormalizeNormalMap_DXT1",
                            "TC_NormalmapAlpha" => "BiasNormalizeNormalMap_DXT5",
                            "TC_NormalmapUncompressed" => "BiasNormalizeNormalMap_V8U8",
                            "TC_NormalmapBC5" => "BiasNormalizeNormalMap_BC5",
                            "TC_NormalmapBC7" => "BiasNormalizeNormalMap_BC7",
                            _ => null
                        };
                        // Native UnpackTexture ab30070 normal mask0x118a excludes HQ=10:
                        // it samples normally and always applies the affine tail, even for [-1,1].
                        if (compression is not ("TC_Default" or "TC_NormalmapHQ") && normalHelper == null)
                        {
                            errors.Add($"{expression.InstancedFullPath} uses CompressionSettings={compression}, which is not supported by the current texture path.");
                            return null;
                        }

                        MaterialUniformExpressionTexture uniform = isParameter
                            ? uniformExpressionPool.OfType<MaterialUniformExpressionTextureParameter>()
                                .FirstOrDefault(candidate => candidate.ParameterName == parameterName &&
                                                             candidate.TextureIndex >= 0 &&
                                                             uniformExpressionTextures[candidate.TextureIndex] == textureUIndex)
                            : uniformExpressionPool.OfType<MaterialUniformExpressionTexture>()
                                .FirstOrDefault(candidate => candidate is not MaterialUniformExpressionTextureParameter &&
                                                             candidate.TextureIndex >= 0 &&
                                                             uniformExpressionTextures[candidate.TextureIndex] == textureUIndex);
                        if (uniform == null)
                        {
                            int textureIndex = uniformExpressionTextures.IndexOf(textureUIndex);
                            if (textureIndex < 0)
                            {
                                textureIndex = uniformExpressionTextures.Count;
                                uniformExpressionTextures.Add(textureUIndex);
                            }

                            uniform = isParameter
                                ? new MaterialUniformExpressionTextureParameter
                                {
                                    ExpressionType = "FMaterialUniformExpressionTextureParameter",
                                    ParameterName = parameterName,
                                    TextureIndex = textureIndex
                                }
                                : new MaterialUniformExpressionTexture
                                {
                                    ExpressionType = "FMaterialUniformExpressionTexture",
                                    TextureIndex = textureIndex
                                };
                            uniformExpressionPool.Add(uniform);
                        }

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
                            numUserTexCoords = Math.Max(numUserTexCoords, 1u);
                            string coordinateLocal = $"Local{localIndex++}";
                            statements.Add($"\t{numericType}2 {coordinateLocal} = Parameters.TexCoords[0].xy;\r\n");
                            coordinates = new CodeValue(MaterialValueType.Vector2, coordinateLocal, true);
                        }

                        MaterialValueType requiredCoordinateType = isCube
                            ? MaterialValueType.Vector3
                            : MaterialValueType.Vector2;
                        if (coordinates.Type != requiredCoordinateType)
                        {
                            errors.Add($"{expression.InstancedFullPath} requires {requiredCoordinateType} coordinates, but received {coordinates.Type}.");
                            return null;
                        }

                        string sampleFunction = useHalfTypes
                            ? isCube ? "h4texCUBE" : "h4tex2D"
                            : isCube ? "texCUBE" : "tex2D";
                        // Native TextureSample coerces coordinates before accessing the texture.
                        // A nested coordinate sample can acquire an earlier sampler slot even
                        // though the outer texture already owns an earlier global texture index.
                        string coordinateCode = coordinates.Code;
                        var textureExpressions = isCube ? uniformCubeTextures : uniform2DTextures;
                        int textureExpressionIndex = FindOrAddTextureReference(textureExpressions, uniform);
                        string textureName = $"Texture{(isCube ? "Cube" : "2D")}_{textureExpressionIndex}";
                        string sampleLocal = $"Local{localIndex++}";
                        statements.Add($"\t{numericType}4 {sampleLocal} = {sampleFunction}({textureName},{coordinateCode});\r\n");
                        string unpackedLocal = sampleLocal;
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
                            // UnpackTexture always calls Mul then Add, including identity ranges.
                            // They remain distinct pixel code chunks even when D3D later optimizes them away.
                            unpackedLocal = EmitVectorOperation(unpackedLocal, "*", unpackScale, statements);
                            unpackedLocal = EmitVectorOperation(unpackedLocal, "+", unpackMin, statements);
                        }
                        value = new CodeValue(MaterialValueType.Vector4, unpackedLocal, true);
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
                        string code = $"{(cosine ? "cos" : "sin")}({x.Code})";
                        if (x.PerPixel)
                        {
                            string local = $"Local{localIndex++}";
                            statements.Add($"\t{DescribeType(x.Type)} {local} = {code};\r\n");
                            value = new CodeValue(x.Type, local, true);
                        }
                        else
                        {
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
                            // Native uniform callback returns MCT_Float regardless of X's
                            // width. Coercion to MCT_Float preserves vector parameter text.
                            value = UniformValue(MaterialValueType.Scalar, code, uniform, x.IsConstant);
                        }
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
                        height = ForceCastDefault(height, MaterialValueType.Scalar);
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
                        foreach (var bound in new[] { min, max }.Where(b => b != null))
                        {
                            if (x.Type != MaterialValueType.Scalar && bound.Type != MaterialValueType.Scalar && bound.Type != x.Type)
                            {
                                errors.Add($"{expression.InstancedFullPath}: Clamp bound coercion failed: {bound.Type} -> {x.Type}.");
                                return null;
                            }
                        }
                        // Clamp accesses Max, Min, Input; single-bound operations access bound,
                        // Input. Native coercion applies to both uniform and nonuniform paths,
                        // but uniform trees retain the original, unreplicated CPU children.
                        string maxCode = hasMax ? CoerceBound(max, x.Type) : null;
                        string minCode = hasMin ? CoerceBound(min, x.Type) : null;
                        string inputCode = x.Code;
                        string code = hasMin && hasMax ? $"min(max({inputCode},{minCode}),{maxCode})"
                            : hasMin ? $"max({inputCode},{minCode})" : $"min({inputCode},{maxCode})";
                        if (x.PerPixel || min?.PerPixel == true || max?.PerPixel == true)
                        {
                            string local = $"Local{localIndex++}";
                            statements.Add($"\t{DescribeType(x.Type)} {local} = {code};\r\n");
                            value = new CodeValue(x.Type, local, true);
                        }
                        else
                        {
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
                            value = UniformValue(x.Type, code, uniform, x.IsConstant && (!hasMin || min.IsConstant) && (!hasMax || max.IsConstant));
                        }
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
                        value = new CodeValue(type, local, true);
                        break;

                        int IfArithmeticType(int left, int right)
                        {
                            if ((left & right) != 0) return left == 0xf ? right : left;
                            errors.Add($"{expression.InstancedFullPath}: If arithmetic between native numeric types 0x{left:x} and 0x{right:x} is undefined.");
                            return 0;
                        }
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
                        if (selectedInput == null)
                        {
                            errors.Add($"{expression.InstancedFullPath} has no {(selected ? "A" : "B")} input.");
                            return null;
                        }
                        value = CompileInput(selectedInput, expression.InstancedFullPath, statements, cache);
                        if (value == null) return null;
                        break;
                    }
                    default:
                        errors.Add($"{expression.InstancedFullPath} uses unsupported {expression.ClassName}.");
                        return null;
                }

                cache[(expression.UIndex, outputIndex)] = value;
                return value;
            }

            CodeValue CompileUnary(CodeValue x, string operation, List<string> statements)
            {
                string code = $"{(operation == "SquareRoot" ? "sqrt" : operation.ToLowerInvariant())}({x.Code})";
                if (x.PerPixel)
                {
                    string local = $"Local{localIndex++}";
                    statements.Add($"\t{DescribeType(x.Type)} {local} = {code};\r\n");
                    return new CodeValue(x.Type, local, true);
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
                    .FirstOrDefault(p => p.ValueType == valueType && p.Value.R == color.R && p.Value.G == color.G &&
                                         p.Value.B == color.B && p.Value.A == color.A);
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
                // Capture the shader frequency now: code may be accessed after graph visitation.
                var stageExpressions = type == MaterialValueType.Scalar
                    ? compilingVertexProperty ? uniformVertexScalars : uniformPixelScalars
                    : compilingVertexProperty ? uniformVertexVectors : uniformPixelVectors;
                string stage = compilingVertexProperty ? "Vertex" : "Pixel";
                return new CodeValue(type, definition, false, isConstant, isConstant ? null : () =>
                {
                    int index = FindOrAddReference(stageExpressions, uniform);
                    return type == MaterialValueType.Scalar
                        ? $"Uniform{stage}Scalars_{index / 4}.{"xyzw"[index % 4]}"
                        : $"Uniform{stage}Vector_{index}" + (type == MaterialValueType.Vector4 ? "" : type == MaterialValueType.Vector3 ? ".rgb" : ".rg");
                }, uniform);
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
                    ExportEntry export = texture as ExportEntry;
                    if (texture is ImportEntry import)
                    {
                        export = resolveTextureImport?.Invoke(import);
                        if (export == null || export.Game != MEGame.LE3 ||
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
            materialContext ??= BuildShouldCacheMaterialContext() with { NumUserTexCoords = translation.NumUserTexCoords };

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

                    // ShaderClient filters ordinary materials' VFs before evaluating cache gates.
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
                    // ab56d40 delegates to an effects owner when present; only the ordinary
                    // non-effects path is unconditionally true. Preserve the resolved input.
                    gate = ShaderCompileGate.MaterialResourceShouldCache;
                    bool? resourceCaches = materialResourceShouldCache != null
                        ? materialResourceShouldCache(CompilationInput, target)
                        : material.Parent?.IsA("RvrEffectsMaterialUser") == true ? null : true;
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
                                candidate.VertexFactoryContext, out bool vertexFactoryCaches))
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
                CompilationInput = CompilationInput,
                Jobs = errors.Count == 0 ? jobs.AsReadOnly() : Array.Empty<ShaderCompileJob>(),
                ExcludedTargets = excluded.AsReadOnly(),
                Errors = errors.AsReadOnly(),
                Decisions = decisions.AsReadOnly()
            };
        }

        /// <summary>
        /// Plans every shader/VF pair in the recovered registry subset, using its evidence-labelled
        /// environment. This is a convenience over the explicit candidate API, not native registry
        /// completeness: newly recovered types can change the selected job set between versions.
        /// </summary>
        public ShaderCompilePlan BuildKnownSubsetCompilePlan(TranslationResult translation, MaterialResource resource,
            ShouldCacheEnvironmentContext environmentContext = null,
            Func<ResolvedMaterialCompilationInput, ShaderCompileTarget, bool?> materialResourceShouldCache = null)
        {
            ArgumentNullException.ThrowIfNull(resource);
            ValidateTranslation(translation);
            // FMaterial::Compile ab49d90 enables bUseHalfTypes only for SM_PS3 when !ForceFloat.
            // SM5 platform5 always passes false; h4tex* intrinsics are absent from its USF headers.
            if (translation.UsesHalfTypes) return new ShaderCompilePlan
            {
                CompilationInput = CompilationInput, Jobs = Array.Empty<ShaderCompileJob>(),
                ExcludedTargets = Array.Empty<ShaderCompileTarget>(), Decisions = Array.Empty<ShaderCompileDecision>(),
                Errors = Array.AsReadOnly(new[] { "Known LE3 SM5 planning requires float HLSL; half types are the native PS3-only branch." })
            };
            var candidates = new List<ShaderCompileCandidate>();
            foreach (var shader in GetKnownShaderTypeRegistrations())
            foreach (var factory in GetKnownVertexFactoryRegistrations())
            {
                if (!TryGetKnownPermutationEnvironment(shader.Name, factory.Name, out var environment))
                    throw new InvalidOperationException($"Missing recovered environment for {factory.Name}/{shader.Name}.");
                candidates.Add(environment.CreateCandidate());
            }
            return BuildProvisionalCompilePlan(translation, resource, candidates, environmentContext: environmentContext,
                materialResourceShouldCache: materialResourceShouldCache);
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
        /// Combines translated material HLSL, provisional material macros, and verified shader-type
        /// registration data into one compiler-independent job. Duplicate macros must agree exactly;
        /// conflicts are rejected instead of relying on preprocessor definition order.
        /// </summary>
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
                target.Stage != registration.Stage || !TryGetKnownVertexFactoryRegistration(target.VertexFactoryTypeName, out var factory) ||
                target.VertexFactoryShaderFileName != factory.SourceFileName)
                return Failure("Shader-object construction requires exact recovered shader and vertex-factory registration metadata.");

            var binder = new LE3ShaderParameterBinder(compiledShader.ParameterBindings);
            Shader shader;
            try
            {
                switch (target.ShaderTypeName)
                {
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
                        shader = CreateBasePassVertexShader(() => new FVertexLightMapPolicy.VertexParametersType
                            { LightMapScale = binder.BindConstant("LightMapScale", optional: false) }, binder.BindFogDensityParameters);
                        break;
                    case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFConeDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFSHLightLightMapPolicyFConeDensityPolicy":
                    case "TBasePassVertexShaderFSHLightLightMapPolicyFConstantDensityPolicy":
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
                !LE3ShaderParameterBinder.IsRendererOwnedViewParameter(p, target.Stage)).ToArray());
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
        /// parent/instance key. A supplied resolved static key retains its independent BaseMaterialId.
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
            if (plan.Errors == null || !plan.Success || plan.Jobs == null || plan.Jobs.Count == 0)
                return Failure("A successful, nonempty explicit shader compile plan is required.");
            if (plan.Jobs.Any(j => !ReferenceEquals(j.Translation, translation)))
                throw new ArgumentException("The plan does not belong to this exact material translation.", nameof(plan));

            var errors = new List<string>();
            var expected = plan.Jobs.ToDictionary(j => (j.Target.ShaderTypeName, j.Target.VertexFactoryTypeName), j => j.Target);
            var seen = new HashSet<(string, string)>();
            var grouped = new Dictionary<string, MeshShaderMap>(StringComparer.Ordinal);
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
            var map = new MaterialShaderMap
            {
                ID = materialResourceId, FriendlyName = friendlyName, StaticParameters = parameters,
                Shaders = new(), MeshShaderMaps = grouped.Values.ToArray(),
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
                UniformExpressionTextures = Array.AsReadOnly(translation.UniformExpressionTextures.ToArray())
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
                foreach (var target in plan.Jobs.Select(j => j.Target).DistinctBy(t => t.VertexFactoryTypeName))
                    cache.VertexFactoryTypeCRCMap.Add(target.VertexFactoryTypeName, sourceCrcs.Compute(target.VertexFactoryShaderFileName).Crc);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                return new ShaderCacheFragmentResult { Errors = Array.AsReadOnly(new[] { "Cannot build LE3 source CRC maps: " + exception.Message }) };
            }
            foreach (var pair in fragment.Shaders) cache.Shaders.Add(pair.Key, pair.Value);
            cache.MaterialShaderMaps.Add(fragment.Map.StaticParameters, fragment.Map);
            return new ShaderCacheFragmentResult { Cache = cache, UniformExpressionTextures = fragment.UniformExpressionTextures };
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
        }

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
            bool vector = name is "Normal" or "AnisotropicDirection" or "WrapLightingCoefficients";
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
        /// confirmed unmirror usage as well as corpus-derived rules. LIGHTMAP_UV_ACCESS has rules not
        /// available in the shipping executable. Shader-type and vertex-factory defines are separate.
        /// Do not treat this result as a complete environment for bytecode compilation.
        /// </summary>
        public IReadOnlyDictionary<string, string> BuildProvisionalMaterialMacros(MaterialResource resource, GraphAnalysis graph)
        {
            ArgumentNullException.ThrowIfNull(graph);
            return BuildProvisionalMaterialMacrosCore(resource, graph,
                graph.ReachableExpressions.Any(x => x.ClassName == "MaterialExpressionVertexColor"),
                InferGraphTransformUsage(graph));
        }

        /// <summary>Uses the current translation's permutation-specific usage state, not a parent's cooked resource flags.</summary>
        public IReadOnlyDictionary<string, string> BuildProvisionalMaterialMacros(MaterialResource resource, TranslationResult translation)
        {
            ValidateTranslation(translation);
            return BuildProvisionalMaterialMacrosCore(resource, translation.Graph, translation.UsesVertexColor, translation.UsingTransforms);
        }

        private IReadOnlyDictionary<string, string> BuildProvisionalMaterialMacrosCore(MaterialResource resource, GraphAnalysis graph,
            bool usesVertexColor, uint usingTransforms)
        {
            ArgumentNullException.ThrowIfNull(resource);
            ArgumentNullException.ThrowIfNull(graph);
            if (!graph.IsValid)
            {
                throw new InvalidOperationException("Cannot build a material environment from an invalid expression graph.");
            }

            var properties = material.GetPropertiesIncludingInherited();
            var macros = new Dictionary<string, string>(StringComparer.Ordinal);
            string blendMode = properties.GetProp<EnumProperty>("BlendMode")?.Value.Name ?? "BLEND_Opaque";
            string lightingModel = properties.GetProp<EnumProperty>("LightingModel")?.Value.Name ?? "MLM_Phong";

            macros.Add(blendMode switch
            {
                "BLEND_Opaque" => "MATERIALBLENDING_SOLID",
                "BLEND_Masked" => "MATERIALBLENDING_MASKED",
                "BLEND_SoftMasked" => "MATERIALBLENDING_SOFTMASKED",
                "BLEND_AlphaComposite" or "BLEND_Translucent" => "MATERIALBLENDING_TRANSLUCENT",
                "BLEND_Additive" => "MATERIALBLENDING_ADDITIVE",
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
            if (graph.ReachableExpressions.Any(x => x.ClassName == "MaterialExpressionLightmapUVs"))
            {
                macros.Add("LIGHTMAP_UV_ACCESS", "1");
            }

            return macros;
        }

        private bool IsUsedWithDecals() => material.GetPropertiesIncludingInherited()
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
            var properties = material.GetPropertiesIncludingInherited();
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

        private IEnumerable<StructProperty> EnumerateSelectedInputs(ExportEntry expression, PropertyCollection properties)
        {
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
