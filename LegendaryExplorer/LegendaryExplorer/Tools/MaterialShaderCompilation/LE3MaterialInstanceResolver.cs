using System;
using System.Collections.Generic;
using System.IO;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.MaterialShaderCompilation
{
    /// <summary>
    /// Result of resolving a requested LE3 MaterialInstance to the shader permutation the game uses.
    /// </summary>
    public sealed class MaterialInstanceResolution
    {
        public bool Success => Errors.Count == 0;
        /// <summary>True when the failure is a supported-boundary limit rather than malformed data.</summary>
        public bool IsUnsupported { get; init; }
        public IReadOnlyList<string> Errors { get; init; } = [];
        /// <summary>Non-fatal observations, e.g. a static key whose BaseMaterialId differs from the base resource.</summary>
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public ExportEntry RequestedInstance { get; init; }
        /// <summary>The requested instance first, then each parent, ending with the base Material.</summary>
        public IReadOnlyList<ExportEntry> Chain { get; init; } = [];
        public ExportEntry BaseMaterial { get; init; }
        /// <summary>The nearest instance (requested or ancestor) owning a static permutation resource;
        /// null when the chain renders with the base Material's own resource.</summary>
        public ExportEntry PermutationOwner { get; init; }
        /// <summary>The owner's cooked static parameter set; null means the base Material's authored defaults.</summary>
        public StaticParameterSet EffectiveStaticParameters { get; init; }
        /// <summary>The owner's static permutation resource, or the base Material's resource.</summary>
        public MaterialResource PermutationResource { get; init; }
        /// <summary>True when the requested instance has no permutation of its own and shares an ancestor's (or the base's).</summary>
        public bool SharesAncestorPermutation => Success && !ReferenceEquals(PermutationOwner, RequestedInstance);
    }

    /// <summary>
    /// Resolves a cooked LE3 MaterialInstance chain without recomputing static parameters, as the
    /// shipping game does: the serialized static parameter set of the nearest instance with
    /// bHasStaticPermutationResource is authoritative; otherwise the base Material's resource is used.
    /// </summary>
    public sealed class LE3MaterialInstanceResolver : IMaterialShaderInstanceResolver
    {
        private const int MaxChainLength = 64;
        private readonly Func<ImportEntry, ExportEntry> resolveImport;

        /// <param name="resolveImport">Optional caller-owned resolution of an imported parent to its loaded
        /// export. Without it, imported parents are unsupported. No package discovery happens here.</param>
        public LE3MaterialInstanceResolver(Func<ImportEntry, ExportEntry> resolveImport = null)
        {
            this.resolveImport = resolveImport;
        }

        public MaterialInstanceResolution Resolve(ExportEntry materialInstance)
        {
            MaterialInstanceResolution Fail(string error, bool unsupported, List<ExportEntry> chain = null) => new()
            {
                RequestedInstance = materialInstance,
                Errors = [error],
                IsUnsupported = unsupported,
                Chain = chain?.ToArray() ?? []
            };

            if (materialInstance == null) return Fail("A MaterialInstance export is required.", false);
            if (materialInstance.Game != MEGame.LE3) return Fail($"Only LE3 material instances are supported, not {materialInstance.Game}.", true);
            if (!materialInstance.IsA("MaterialInstance")) return Fail($"{materialInstance.ClassName} is not a MaterialInstance.", true);

            var chain = new List<ExportEntry>();
            var visited = new HashSet<ExportEntry>(ReferenceEqualityComparer.Instance);
            ExportEntry owner = null;
            ExportEntry current = materialInstance;
            while (true)
            {
                if (!visited.Add(current) || chain.Count >= MaxChainLength)
                    return Fail($"The parent chain of {materialInstance.InstancedFullPath} is cyclic or too deep.", false, chain);
                chain.Add(current);
                if (current.IsA("Material")) break;
                if (!current.IsA("MaterialInstance"))
                    return Fail($"{current.InstancedFullPath} ({current.ClassName}) is not a Material or MaterialInstance.", true, chain);

                var properties = current.GetProperties();
                if (owner == null && properties.GetProp<BoolProperty>("bHasStaticPermutationResource")?.Value == true)
                    owner = current;
                int parentIndex = properties.GetProp<ObjectProperty>("Parent")?.Value ?? 0;
                if (parentIndex == 0)
                    return Fail($"{current.InstancedFullPath} has no Parent; the game would render it with a default material.", true, chain);
                if (!current.FileRef.TryGetEntry(parentIndex, out var parentEntry) || parentEntry == null)
                    return Fail($"{current.InstancedFullPath} has an invalid Parent reference {parentIndex}.", false, chain);
                if (parentEntry is ImportEntry import)
                {
                    var resolved = resolveImport?.Invoke(import);
                    if (resolved == null)
                        return Fail($"{current.InstancedFullPath} has imported Parent {import.InstancedFullPath}, which was not resolved.", true, chain);
                    if (resolved.Game != MEGame.LE3 || !string.Equals(resolved.InstancedFullPath, import.InstancedFullPath, StringComparison.OrdinalIgnoreCase))
                        return Fail($"Imported Parent {import.InstancedFullPath} resolved to an unrelated export {resolved.InstancedFullPath}.", false, chain);
                    current = resolved;
                }
                else current = (ExportEntry)parentEntry;
            }

            var baseMaterial = chain[^1];
            var baseResource = ObjectBinary.From<Material>(baseMaterial)?.SM3MaterialResource;
            if (owner == null)
                return new MaterialInstanceResolution
                {
                    RequestedInstance = materialInstance, Chain = chain.ToArray(), BaseMaterial = baseMaterial,
                    PermutationResource = baseResource
                };

            MaterialInstance ownerBinary;
            try
            {
                ownerBinary = ObjectBinary.From<MaterialInstance>(owner);
            }
            catch (EndOfStreamException)
            {
                ownerBinary = null;
            }
            var staticParameters = ownerBinary?.SM3StaticParameterSet;
            if (staticParameters == null || ownerBinary.SM3StaticPermutationResource == null)
                return Fail($"{owner.InstancedFullPath} sets bHasStaticPermutationResource but has no serialized static permutation.", false, chain);

            var warnings = new List<string>();
            // The shipping game does not revalidate this key against the base resource; report only.
            if (baseResource != null && staticParameters.BaseMaterialId != Guid.Empty && staticParameters.BaseMaterialId != baseResource.ID)
                warnings.Add($"{owner.InstancedFullPath} static parameters reference base material id {staticParameters.BaseMaterialId}, " +
                    $"but {baseMaterial.InstancedFullPath}'s resource id is {baseResource.ID}.");
            return new MaterialInstanceResolution
            {
                RequestedInstance = materialInstance, Chain = chain.ToArray(), BaseMaterial = baseMaterial,
                PermutationOwner = owner, EffectiveStaticParameters = staticParameters,
                PermutationResource = ownerBinary.SM3StaticPermutationResource, Warnings = warnings.ToArray()
            };
        }
    }
}
