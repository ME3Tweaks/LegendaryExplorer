using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Texture2D = LegendaryExplorerCore.Unreal.Classes.Texture2D;

namespace LegendaryExplorer.Lightmaps.Game;

/// <summary>
/// A terrain's layer materials as UE3 combines them for rendering. ATerrain::CacheWeightMaps numbers the TerrainMaterials of its layers' setups
/// (FTerrainFilteredMaterial::BuildWeightMap: layers last to first, each setup's materials in order, one entry per distinct material), and each
/// TerrainComponent is drawn with the cached FTerrainMaterialResource whose mask is the set of those materials weighted anywhere in its quads
/// (UTerrainComponent::UpdatePatchBatches, FullBatch). The weights are read from the cooked TerrainWeightMapTextures, which UTerrainWeightMapTexture::UpdateData
/// fills with four materials per texel, in byte order.
/// </summary>
public sealed class TerrainLayerMaterials
{
    /// <summary>One FTerrainWeightedMaterial. <paramref name="Material"/> is the TerrainMaterial's material, or null if it has none</summary>
    public sealed record WeightedMaterial(ExportEntry TerrainMaterial, ExportEntry Material, bool Highlighted);

    public TerrainHeightField HeightField { get; }
    public IReadOnlyList<WeightedMaterial> Materials { get; }
    /// <summary>Terrain.WeightedTextureMaps: texture i holds the weights of materials 4i to 4i + 3. Null where an entry can't be resolved</summary>
    public IReadOnlyList<ExportEntry> WeightMaps { get; }
    /// <summary>Why the layers or weight maps couldn't be read completely, or null</summary>
    public string Error { get; }

    private readonly byte[][] WeightMapData;
    private readonly int[] WeightMapWidths;

    public TerrainLayerMaterials(TerrainHeightField heightField, Func<IEntry, ExportEntry> resolve)
    {
        HeightField = heightField;
        var materials = new List<WeightedMaterial>();
        var errors = new List<string>();
        List<StructProperty> layers = heightField.Properties.GetProp<ArrayProperty<StructProperty>>("Layers")?.ToList() ?? [];
        for (int layerIndex = layers.Count - 1; layerIndex >= 0; layerIndex--)
        {
            StructProperty layer = layers[layerIndex];
            if (layer.GetProp<BoolProperty>("Hidden")?.Value is true) continue;
            if (layer.GetProp<ObjectProperty>("Setup") is not { Value: not 0 } setupProp) continue;
            ExportEntry setup = Resolve(setupProp.Value, $"layer {layerIndex}'s setup");
            if (setup is null) continue;
            bool highlighted = layer.GetProp<BoolProperty>("Highlighted")?.Value ?? false;
            foreach (StructProperty filtered in setup.GetProperty<ArrayProperty<StructProperty>>("Materials") ?? Enumerable.Empty<StructProperty>())
            {
                if (filtered.GetProp<ObjectProperty>("Material") is not { Value: not 0 } terrainMaterialProp) continue;
                ExportEntry terrainMaterial = Resolve(terrainMaterialProp.Value, $"a material of {setup.InstancedFullPath}", setup.FileRef);
                if (terrainMaterial is null) continue;
                if (materials.Any(m => m.TerrainMaterial.InstancedFullPath == terrainMaterial.InstancedFullPath && m.Highlighted == highlighted)) continue;
                ExportEntry material = terrainMaterial.GetProperty<ObjectProperty>("Material") is { Value: not 0 } materialProp
                    ? Resolve(materialProp.Value, $"the material of {terrainMaterial.InstancedFullPath}", terrainMaterial.FileRef) : null;
                materials.Add(new WeightedMaterial(terrainMaterial, material, highlighted));
            }
        }
        Materials = materials;

        Terrain binary = heightField.Binary;
        var weightMaps = new ExportEntry[binary.WeightedTextureMaps.Length];
        WeightMapData = new byte[weightMaps.Length][];
        WeightMapWidths = new int[weightMaps.Length];
        for (int i = 0; i < weightMaps.Length; i++)
        {
            weightMaps[i] = Resolve(binary.WeightedTextureMaps[i], $"weight map {i}");
        }
        WeightMaps = weightMaps;
        if (weightMaps.Length < (materials.Count + 3) / 4)
            errors.Add($"{materials.Count} layer materials need {(materials.Count + 3) / 4} weight maps, but the terrain has {weightMaps.Length}");
        Error = errors.Count > 0 ? string.Join("; ", errors) : null;

        ExportEntry Resolve(int uIndex, string what, IMEPackage package = null)
        {
            package ??= heightField.Export.FileRef;
            IEntry entry = package.GetEntry(uIndex);
            ExportEntry export = entry is null ? null : resolve(entry);
            if (export is null) errors.Add($"{what} ({entry?.InstancedFullPath ?? uIndex.ToString()}) could not be found");
            return export;
        }
    }

    /// <summary>
    /// The weight map texels (four bytes each, rows of the texture's width), or null if the texture can't be read. Read once, when first needed
    /// </summary>
    private byte[] GetWeightMapData(int textureIndex, out int width)
    {
        width = 0;
        if ((uint)textureIndex >= WeightMapData.Length || WeightMaps[textureIndex] is not { } export) return null;
        if (WeightMapData[textureIndex] is null)
        {
            var texture = new Texture2D(export);
            if (texture.GetTopMip() is not { } mip || export.GetProperty<EnumProperty>("Format")?.Value.Name != "PF_A8R8G8B8") return null;
            byte[] data = Texture2D.GetTextureData(mip, export.Game);
            if (data is null || data.Length < mip.width * mip.height * 4) return null;
            WeightMapWidths[textureIndex] = mip.width;
            WeightMapData[textureIndex] = data;
        }
        width = WeightMapWidths[textureIndex];
        return WeightMapData[textureIndex];
    }

    /// <summary>A material's weight at a vertex (FTerrainWeightedMaterial::Weight, clamped to the grid), from its weight map. Null if that can't be read</summary>
    public byte? Weight(int materialIndex, int x, int y)
    {
        byte[] data = GetWeightMapData(materialIndex / 4, out int width);
        if (data is null) return null;
        x = Math.Clamp(x, 0, HeightField.NumVerticesX - 1);
        y = Math.Clamp(y, 0, HeightField.NumVerticesY - 1);
        int offset = (y * width + x) * 4 + materialIndex % 4;
        return offset < data.Length ? data[offset] : null;
    }

    /// <summary>
    /// UTerrainComponent::UpdatePatchBatches' full mask: the materials with weight at any corner of any of the section's quads. Null if a weight
    /// map can't be read
    /// </summary>
    public ulong? SectionMask(TerrainSection section) => Materials.Count > 64 ? null : SectionMask(section, Materials.Count, Weight);

    /// <inheritdoc cref="SectionMask(TerrainSection)"/>
    /// <param name="weight">A material's weight at a vertex, or null if it can't be read</param>
    public static ulong? SectionMask(TerrainSection section, int materialCount, Func<int, int, int, byte?> weight)
    {
        ulong mask = 0;
        for (int i = 0; i < materialCount; i++)
        {
            for (int y = section.BaseY; y <= section.BaseY + section.TrueSizeY && (mask & (1UL << i)) == 0; y++)
            for (int x = section.BaseX; x <= section.BaseX + section.TrueSizeX; x++)
            {
                if (weight(i, x, y) is not byte value) return null;
                if (value > 0)
                {
                    mask |= 1UL << i;
                    break;
                }
            }
        }
        return mask;
    }

    /// <summary>
    /// The weight maps a section drawn with <paramref name="mask"/> binds: the textures of the masked materials, each once in material order, named
    /// TWeightMap0, TWeightMap1… by their place in that list (FTerrainBatchInfo and UpdateData_RenderThread; LE1 RVAs 0x5FE320 and 0x60EE80). The
    /// compiled material samples TWeightMap{material / 4}, so the two agree when the mask uses every texture up to its last; otherwise the material
    /// samples its placeholder, as the game does
    /// </summary>
    public List<(string Name, ExportEntry Texture)> WeightMapParameters(ulong mask)
    {
        var textures = new List<ExportEntry>();
        for (int i = 0; i < Materials.Count; i++)
        {
            if ((mask & (1UL << i)) != 0 && i / 4 < WeightMaps.Count && WeightMaps[i / 4] is { } texture && !textures.Contains(texture))
                textures.Add(texture);
        }
        return textures.Select((texture, i) => ($"TWeightMap{i}", texture)).ToList();
    }

    /// <summary>Whether <see cref="WeightMapParameters"/> names every weight map as the compiled material samples it</summary>
    public bool WeightMapNamesMatch(ulong mask)
    {
        var names = WeightMapParameters(mask);
        for (int i = 0; i < Materials.Count; i++)
        {
            if ((mask & (1UL << i)) != 0 && i / 4 < WeightMaps.Count && !names.Contains(($"TWeightMap{i / 4}", WeightMaps[i / 4]))) return false;
        }
        return true;
    }

    /// <summary>ATerrain::GetCachedMaterial: the cached resource whose mask matches (same material count and bits). Null if there isn't one</summary>
    public TerrainMaterialResource FindCachedMaterial(ulong mask) => HeightField.Binary.CachedTerrainMaterials
        .FirstOrDefault(resource => resource?.Mask is { } m && m.NumBits == Materials.Count && m.BitMask == mask);

    /// <summary>
    /// Whether a cached resource was compiled from these materials: its MaterialIds are the base materials' resource IDs of its mask's materials,
    /// in order (FTerrainMaterialResource::PostLoad). Confirms the material numbering
    /// </summary>
    public bool MatchesMaterialIds(TerrainMaterialResource resource, Func<ExportEntry, Guid?> baseMaterialId)
    {
        if (resource.MaterialIds is not { } ids) return false;
        int idIndex = 0;
        for (int i = 0; i < Materials.Count; i++)
        {
            if ((resource.Mask.BitMask & (1UL << i)) == 0) continue;
            if (Materials[i].Material is not { } material || baseMaterialId(material) is not Guid id) continue;
            if (idIndex >= ids.Length || ids[idIndex++] != id) return false;
        }
        return idIndex == ids.Length;
    }
}
