using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Shaders;

/// <summary>
/// Builds the SM3 <see cref="MaterialResource"/> an installer writes next to a compiled shader map, from the
/// same translation that produced the map, in the field layout the game serializes. The same call serves
/// a base Material and an instance's static permutation resource; only the ID differs.
/// </summary>
public static class LE3MaterialResourceBuilder
{
    /// <param name="translation">A successful material-resource translation.</param>
    /// <param name="id">The resource ID. For a base Material this is the shader map key's BaseMaterialId.</param>
    /// <param name="uniformExpressionTextures">The translation's textures as UIndexes in the destination package,
    /// in translation order. Null keeps the translation's own UIndexes (same package).</param>
    /// <param name="instanceParameterTextures">Instance permutations only: the instance's effective texture for each
    /// name from <see cref="GetInstanceTextureParameterOrder"/>, in that order, as destination UIndexes; 0 where the
    /// instance overrides nothing. Shipping appends these after the compiled textures, skipping ones already listed.</param>
    public static MaterialResource Build(TranslationResult translation, Guid id, IReadOnlyList<int> uniformExpressionTextures = null,
        IEnumerable<int> instanceParameterTextures = null)
    {
        ArgumentNullException.ThrowIfNull(translation);
        if (!translation.Success) throw new ArgumentException("A successful translation is required.", nameof(translation));
        var textures = uniformExpressionTextures ?? translation.UniformExpressionTextures;
        if (textures.Count != translation.UniformExpressionTextures.Count)
            throw new ArgumentException("The destination texture list must map the translation's list one to one.", nameof(uniformExpressionTextures));
        var allTextures = textures.ToList();
        foreach (int texture in instanceParameterTextures ?? [])
            if (texture != 0 && !allTextures.Contains(texture)) allTextures.Add(texture);

        var resource = MaterialResource.Create();
        resource.ID = id;
        resource.NumUserTexCoords = translation.NumUserTexCoords;
        resource.UniformExpressionTextures = allTextures.ToArray();
        // Disk slots 2..5 are Time, DynamicParameter, LightmapUVs and WPO-connected; the field names are shifted.
        resource.bUsesSceneColor = translation.UsesSceneColor;
        resource.bUsesSceneDepth = translation.UsesSceneDepth;
        resource.bUsesDynamicParameter = translation.UsesTime;
        resource.bUsesLightmapUVs = translation.UsesDynamicParameter;
        resource.bUsesMaterialVertexPositionOffset = translation.UsesLightmapUVs;
        resource.bUsesVertexColor = translation.Material.GetProperty<StructProperty>("WorldPositionOffset")?
            .GetProp<ObjectProperty>("Expression")?.Value is > 0;
        resource.UsingTransforms = translation.UsingTransforms;
        resource.MaxTextureDependencyLength = translation.Compiler.ComputeMaxTextureDependencyLength(translation.Graph);
        resource.TextureLookups = BuildTextureLookups(translation);
        return resource;
    }

    /// <summary>
    /// Compiled texture parameters in the order an instance permutation resource appends their effective values:
    /// the 2D texture uniforms, then the cube ones. Matches 3576/3585 shipping LE3 instance resources.
    /// </summary>
    public static IReadOnlyList<string> GetInstanceTextureParameterOrder(TranslationResult translation)
    {
        ArgumentNullException.ThrowIfNull(translation);
        return translation.Uniform2DTextureExpressions.Concat(translation.UniformCubeTextureExpressions)
            .OfType<MaterialUniformExpressionTextureParameter>().Select(u => u.ParameterName.Instanced).ToArray();
    }

    /// <summary>The translation's textures as instanced full paths, for resolving into another package.</summary>
    public static IReadOnlyList<string> GetUniformExpressionTexturePaths(TranslationResult translation)
    {
        ArgumentNullException.ThrowIfNull(translation);
        var package = translation.Material.FileRef;
        return translation.UniformExpressionTextures.Select(uIndex => package.GetEntry(uIndex)?.InstancedFullPath
            ?? throw new InvalidOperationException($"Texture {uIndex} is not in {package.FilePath}.")).ToArray();
    }

    /// <summary>Resolves texture paths to entries already in <paramref name="package"/>. Throws on any that are missing.</summary>
    public static IReadOnlyList<int> ResolveTexturePaths(IMEPackage package, IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(paths);
        var missing = paths.Where(p => package.FindEntry(p) == null).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException($"{package.FilePath} has no entry for texture(s): {string.Join(", ", missing)}");
        return paths.Select(p => package.FindEntry(p).UIndex).ToArray();
    }

    /// <summary>
    /// The game's texture lookup rebuild: every texture sample with a texture in the base Material's
    /// Expressions, connected or not, whose coordinates are absent or a TextureCoordinate, matched to the
    /// first identical 2D texture uniform. Read only by the texel-density view mode.
    /// </summary>
    private static MaterialResource.TextureLookup[] BuildTextureLookups(TranslationResult translation)
    {
        var material = translation.Material;
        var package = material.FileRef;
        var lookups = new List<MaterialResource.TextureLookup>();
        var expressions = material.GetProperty<ArrayProperty<ObjectProperty>>("Expressions");
        if (expressions == null) return [];
        foreach (var reference in expressions)
        {
            if (!package.TryGetEntry(reference.Value, out var entry) || entry is not ExportEntry expression ||
                !expression.IsA("MaterialExpressionTextureSample")) continue;
            int texture = expression.GetProperty<ObjectProperty>("Texture")?.Value ?? 0;
            if (texture == 0) continue;

            int coordinateIndex = 0;
            float uScale = 1, vScale = 1;
            int coordinates = expression.GetProperty<StructProperty>("Coordinates")?.GetProp<ObjectProperty>("Expression")?.Value ?? 0;
            if (coordinates != 0)
            {
                if (!package.TryGetEntry(coordinates, out var coordinateEntry) || coordinateEntry is not ExportEntry coordinate ||
                    coordinate.ClassName != "MaterialExpressionTextureCoordinate") continue;
                coordinateIndex = coordinate.GetProperty<IntProperty>("CoordinateIndex")?.Value ?? 0;
                uScale = coordinate.GetProperty<FloatProperty>("UTiling")?.Value ?? 1;
                vScale = coordinate.GetProperty<FloatProperty>("VTiling")?.Value ?? 1;
            }

            bool isParameter = expression.IsA("MaterialExpressionTextureSampleParameter2D");
            string parameterName = isParameter ? expression.GetProperty<NameProperty>("ParameterName")?.Value.Instanced : null;
            int textureIndex = -1;
            for (int i = 0; i < translation.Uniform2DTextureExpressions.Count; i++)
            {
                var uniform = translation.Uniform2DTextureExpressions[i];
                bool sameType = isParameter
                    ? uniform.ExpressionType.Name == "FMaterialUniformExpressionTextureParameter"
                    : uniform.ExpressionType.Name == "FMaterialUniformExpressionTexture";
                if (!sameType || (uint)uniform.TextureIndex >= (uint)translation.UniformExpressionTextures.Count ||
                    translation.UniformExpressionTextures[uniform.TextureIndex] != texture) continue;
                if (isParameter && !string.Equals(((MaterialUniformExpressionTextureParameter)uniform).ParameterName.Instanced,
                        parameterName, StringComparison.OrdinalIgnoreCase)) continue;
                textureIndex = i;
                break;
            }
            if (textureIndex < 0) continue;
            lookups.Add(new MaterialResource.TextureLookup
            {
                TexCoordIndex = coordinateIndex, TextureIndex = textureIndex, UScale = uScale, VScale = vScale, Unk = 0
            });
        }
        return lookups.ToArray();
    }
}
