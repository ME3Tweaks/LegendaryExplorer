using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorerCore.Helpers
{
    public enum NormalCompressionStatus { Match, Mismatch, Inactive, Unresolved, Unsupported }

    public sealed record NormalCompressionFinding
    {
        public string InstancePath { get; init; }
        public int InstanceUIndex { get; init; }
        public string Quality { get; init; }
        public int ParameterIndex { get; init; }
        public string ParameterName { get; init; }
        public Guid ExpressionGuid { get; init; }
        public bool Override { get; init; }
        public byte StoredCompression { get; init; }
        public byte? ExpectedCompression { get; init; }
        public string TexturePath { get; init; }
        public string TexturePackage { get; init; }
        public string TextureFormat { get; init; }
        public string TextureCompression { get; init; }
        public bool TextureCompressionIsNativeDefault { get; init; }
        public string Provenance { get; init; }
        public MEGame Game { get; init; }
        public NormalCompressionStatus Status { get; init; }
        public string Reason { get; init; }
        public bool Applied { get; internal set; }
    }

    /// <summary>
    /// Checks serialized normal-decoding parameters against effective textures in an assembled package.
    /// Intended primarily for editable/decooked assets before shader compilation. Cooked callers must
    /// supply a verified default binding when the expression graph is absent.
    /// The caller owns import resolution and its cache. These methods never save packages or change dependencies.
    /// </summary>
    public static class NormalCompressionHelper
    {
        /// <param name="cookedDefaultTextureResolver">Optional verified default binding for a cooked
        /// material with no expression graph. Explicit instance overrides still take precedence; the
        /// returned texture is subject to the same format, game and compression checks as graph defaults.</param>
        public static IReadOnlyList<NormalCompressionFinding> AnalyzeNormalCompressionOverrides(
            IMEPackage package, Func<IEntry, ExportEntry> resolver = null,
            Func<ExportEntry, StaticParameterSet.NormalParameter, ExportEntry> cookedDefaultTextureResolver = null)
        {
            resolver ??= entry => entry as ExportEntry;
            var findings = new List<NormalCompressionFinding>();
            foreach (var instance in package.Exports.Where(e => e.IsA("MaterialInstance")
                         && !e.IsDefaultObject && e.GetProperty<BoolProperty>("bHasStaticPermutationResource")?.Value == true))
            {
                try
                {
                    var binary = ObjectBinary.From<MaterialInstance>(instance);
                    AnalyzeSet(binary.SM3StaticParameterSet, "SM3/High");

                    void AnalyzeSet(StaticParameterSet set, string quality)
                    {
                        if (set?.NormalParameters == null) return;
                        for (int index = 0; index < set.NormalParameters.Length; index++)
                        {
                            var normal = set.NormalParameters[index];
                            var finding = new NormalCompressionFinding
                            {
                                InstancePath = instance.InstancedFullPath, InstanceUIndex = instance.UIndex,
                                Quality = quality, ParameterIndex = index, ParameterName = normal.ParameterName.Instanced,
                                ExpressionGuid = normal.ExpressionGUID, Override = normal.bOverride,
                                StoredCompression = normal.CompressionSettings, Game = package.Game
                            };
                            if (!normal.bOverride)
                            {
                                findings.Add(finding with { Status = NormalCompressionStatus.Inactive,
                                    Reason = "Inactive entries are preserved; their native selection semantics are not assumed." });
                                continue;
                            }
                            try { findings.Add(AnalyzeParameter(instance, normal, finding, resolver, cookedDefaultTextureResolver)); }
                            catch (Exception ex) { findings.Add(finding with { Status = NormalCompressionStatus.Unresolved, Reason = ex.Message }); }
                        }
                    }
                }
                catch (Exception ex)
                {
                    findings.Add(new NormalCompressionFinding { InstancePath = instance.InstancedFullPath,
                        InstanceUIndex = instance.UIndex, Game = package.Game, Status = NormalCompressionStatus.Unresolved,
                        Reason = $"Cannot read instance static parameters: {ex.Message}" });
                }
            }
            return findings;
        }

        /// <summary>
        /// Reanalyzes the package and repairs only verified, enabled mismatches. Clears the affected quality's
        /// compiled instance resource so its old bytecode cannot be reused. Preserves all other static fields,
        /// authored texture/vector/scalar properties, names and parent identities. Does not save the package.
        /// </summary>
        public static IReadOnlyList<NormalCompressionFinding> FixNormalCompressionOverrides(
            IMEPackage package, Func<IEntry, ExportEntry> resolver = null)
        {
            var findings = AnalyzeNormalCompressionOverrides(package, resolver);
            foreach (var group in findings.Where(f => f.Status == NormalCompressionStatus.Mismatch).GroupBy(f => f.InstanceUIndex))
            {
                var instance = package.GetUExport(group.Key);
                var binary = ObjectBinary.From<MaterialInstance>(instance);
                foreach (var finding in group)
                {
                    var set = finding.Quality == "SM3/High" ? binary.SM3StaticParameterSet : binary.SM2StaticParameterSet;
                    var parameter = set.NormalParameters[finding.ParameterIndex];
                    if (!parameter.bOverride || parameter.ParameterName.Instanced != finding.ParameterName
                        || parameter.ExpressionGUID != finding.ExpressionGuid || parameter.CompressionSettings != finding.StoredCompression)
                        throw new InvalidOperationException("Static parameter changed during normal-compression analysis.");
                    parameter.CompressionSettings = finding.ExpectedCompression.Value;
                    if (finding.Quality == "SM3/High") binary.SM3StaticPermutationResource = MaterialResource.Create();
                    else binary.SM2StaticPermutationResource = MaterialResource.Create();
                }
                instance.WriteBinary(binary);
                foreach (var finding in group) finding.Applied = true;
            }
            return findings;
        }

        private static NormalCompressionFinding AnalyzeParameter(ExportEntry instance, StaticParameterSet.NormalParameter normal,
            NormalCompressionFinding finding, Func<IEntry, ExportEntry> resolver,
            Func<ExportEntry, StaticParameterSet.NormalParameter, ExportEntry> cookedDefaultTextureResolver)
        {
            var chain = new List<ExportEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var current = instance;
            ExportEntry texture = null;
            string provenance = null;
            while (current != null && chain.Count < 64)
            {
                if (!seen.Add($"{current.FileRef.FilePath}:{current.UIndex}"))
                    return Failure("Cycle in material parent chain.");
                chain.Add(current);
                if (current.IsA("Material")) break; // Includes DecalMaterial.
                if (current.IsA("MaterialInstance"))
                {
                    var values = current.GetProperty<ArrayProperty<StructProperty>>("TextureParameterValues")?
                        .Where(v => v.GetProp<NameProperty>("ParameterName")?.Value == normal.ParameterName).ToList();
                    if (values is { Count: > 0 })
                    {
                        if (values.Count != 1) return Failure("Ambiguous instance texture parameter values.");
                        var value = values[0].GetProp<ObjectProperty>("ParameterValue");
                        if (value == null || value.Value == 0) return Failure("Null texture override; no parent fallback assumed.");
                        texture = Resolve(current, value.Value);
                        provenance = current.InstancedFullPath;
                        break; // A known nearest override does not require a readable base graph.
                    }
                }
                var parentProperty = current.IsA("MaterialInstance") ? "Parent"
                    : current.IsA("RvrEffectsMaterialUser") ? "m_pBaseMaterial" : null;
                if (parentProperty == null) return Failure($"Unsupported parent class {current.ClassName}.");
                current = Resolve(current, current.GetProperty<ObjectProperty>(parentProperty)?.Value ?? 0);
            }
            if (provenance == null)
            {
                if (chain.Count == 0 || !chain[^1].IsA("Material")) return Failure("Unresolved parent or excessive parent depth.");
                var material = chain[^1];
                var expressions = material.GetProperty<ArrayProperty<ObjectProperty>>("Expressions");
                if (expressions == null)
                {
                    texture = cookedDefaultTextureResolver?.Invoke(instance, normal);
                    if (texture == null) return Failure("Base material expression graph is absent.");
                    provenance = "Verified cooked default: " + material.InstancedFullPath;
                }
                else
                {
                    var candidates = new List<ExportEntry>();
                    foreach (var reference in expressions)
                    {
                        var expression = Resolve(material, reference.Value);
                        if (expression == null) return Failure("Unresolved expression in base material graph.");
                        if (expression.IsA("MaterialExpressionTextureSampleParameterNormal")
                            && expression.GetProperty<NameProperty>("ParameterName")?.Value == normal.ParameterName)
                            candidates.Add(expression);
                    }
                    if (normal.ExpressionGUID != Guid.Empty)
                        candidates = candidates.Where(e => ReadGuid(e.GetProperty<StructProperty>("ExpressionGUID")) == normal.ExpressionGUID).ToList();
                    if (candidates.Count != 1) return Failure("Missing or ambiguous normal expression name/GUID.");
                    var expressionDefault = candidates[0];
                    texture = Resolve(expressionDefault, expressionDefault.GetProperty<ObjectProperty>("Texture")?.Value ?? 0);
                    provenance = expressionDefault.InstancedFullPath;
                }
            }
            finding = finding with { Provenance = provenance, TexturePath = texture?.InstancedFullPath, TexturePackage = texture?.FileRef.FilePath };
            if (texture == null || !texture.IsA("Texture2D")) return Failure("Effective texture is unresolved or is not Texture2D.");
            if (texture.InstancedFullPath.Contains("MissingTextures", StringComparison.OrdinalIgnoreCase)
                || texture.ObjectName.Name.Equals("MissingTexture", StringComparison.OrdinalIgnoreCase))
                return Failure("Placeholder texture does not establish a safe normal decoder.");
            if (texture.FileRef.Game != instance.FileRef.Game) return Unsupported("Resolved texture belongs to a different target game.");
            var compression = texture.GetProperty<EnumProperty>("CompressionSettings")?.Value;
            var format = texture.GetProperty<EnumProperty>("Format")?.Value;
            // Native UDK resaves omit TC_Default because it is the zero-valued class default.
            // Infer it only for the same explicitly established affine normal encoding; other
            // missing metadata remains unresolved, including all LE packages.
            bool nativeAffineDefault = compression is null && instance.FileRef.Game == MEGame.UDK && format is not null
                && new[] { "PF_DXT1", "PF_DXT5" }.Contains(format.Value.Name, StringComparer.OrdinalIgnoreCase)
                && HasSignedRgbNormalRange(texture);
            if (nativeAffineDefault) compression = new NameReference("TC_Default");
            finding = finding with { TextureCompression = compression?.Instanced, TextureFormat = format?.Instanced,
                TextureCompressionIsNativeDefault = nativeAffineDefault };
            // NameReference overloads == with strings; use a pattern null check on the nullable struct.
            if (compression is null || format is null) return Failure("Explicit texture compression/format metadata is absent.");
            var enumValues = GlobalUnrealObjectInfo.GetEnumValues(instance.FileRef.Game, "TextureCompressionSettings");
            int expected = enumValues?.FindIndex(n => n.Number == compression.Value.Number
                && n.Name.Equals(compression.Value.Name, StringComparison.OrdinalIgnoreCase)) ?? -1;
            if (expected < 0 || expected > byte.MaxValue) return Unsupported("Compression enum is unavailable in the target game.");

            // Permit recognized normal encodings only. In particular, UDK TC_Default is not a safe
            // substitute for LE's TC_NormalmapHQ merely because both textures report PF_DXT5.
            bool compatible = compression.Value.Name.ToUpperInvariant() switch
            {
                "TC_NORMALMAP" => format.Value.Name.Equals("PF_DXT1", StringComparison.OrdinalIgnoreCase)
                    || (instance.FileRef.Game == MEGame.UDK && format.Value.Name.Equals("PF_A8R8G8B8", StringComparison.OrdinalIgnoreCase)
                        && HasSignedRgbNormalRange(texture)),
                "TC_NORMALMAPALPHA" => format.Value.Name.Equals("PF_DXT5", StringComparison.OrdinalIgnoreCase),
                "TC_NORMALMAPUNCOMPRESSED" => format.Value.Name.Equals("PF_V8U8", StringComparison.OrdinalIgnoreCase),
                "TC_NORMALMAPBC5" => new[] { "PF_BC5", "PF_NormalMap_HQ", "PF_NormalMap_LQ" }.Contains(format.Value.Name, StringComparer.OrdinalIgnoreCase),
                "TC_NORMALMAPBC7" => format.Value.Name.Equals("PF_BC7", StringComparison.OrdinalIgnoreCase),
                "TC_NORMALMAPHQ" => new[] { "PF_DXT1", "PF_DXT5" }.Contains(format.Value.Name, StringComparer.OrdinalIgnoreCase),
                // LE3 HQ uses the generic affine unpack path. UDK trims its unsupported enum name
                // to TC_Default but retains the pixels and UnpackMin/Max. UDK UnpackTexture
                // (analysis address 0x7ff64de12410) treats the copied byte 10 and target byte 0 identically.
                // Require the explicit signed RGB range and linear sampling before accepting this case.
                "TC_DEFAULT" => instance.FileRef.Game == MEGame.UDK
                    && new[] { "PF_DXT1", "PF_DXT5" }.Contains(format.Value.Name, StringComparer.OrdinalIgnoreCase)
                    && HasSignedRgbNormalRange(texture),
                _ => false
            };
            if (!compatible) return Unsupported("Texture format/compression pair is not a verified normal encoding.");
            return finding with { ExpectedCompression = (byte)expected,
                Status = normal.CompressionSettings == expected ? NormalCompressionStatus.Match : NormalCompressionStatus.Mismatch,
                Reason = normal.CompressionSettings == expected ? nativeAffineDefault
                        ? "Stored normal compression agrees with native UDK TC_Default and the signed RGB unpack range."
                        : "Stored normal decoder agrees with effective texture."
                    : compression.Value.Name.Equals("TC_Default", StringComparison.OrdinalIgnoreCase)
                        ? "Enabled static normal compression differs from the target affine texture setting; preserve UnpackMin/Max."
                        : "Enabled static normal decoder differs from effective texture compression." };

            ExportEntry Resolve(ExportEntry owner, int index) => index == 0 ? null : resolver(owner.FileRef.GetEntry(index));
            NormalCompressionFinding Failure(string reason) => finding with { Status = NormalCompressionStatus.Unresolved, Reason = reason };
            NormalCompressionFinding Unsupported(string reason) => finding with { Status = NormalCompressionStatus.Unsupported, Reason = reason };
        }

        private static Guid? ReadGuid(StructProperty property) => property?.StructType == "Guid" ? new FGuid(property).ToGuid() : null;

        private static bool HasSignedRgbNormalRange(ExportEntry texture)
        {
            var properties = texture.GetProperties();
            return properties.GetProp<BoolProperty>("SRGB")?.Value == false
                && Enumerable.Range(0, 3).All(i => properties.GetProp<FloatProperty>("UnpackMin", i)?.Value == -1f
                    && (properties.GetProp<FloatProperty>("UnpackMax", i)?.Value ?? 1f) == 1f)
                && (properties.GetProp<FloatProperty>("UnpackMin", 3)?.Value ?? 0f) == 0f
                && (properties.GetProp<FloatProperty>("UnpackMax", 3)?.Value ?? 1f) == 1f;
        }
    }
}
