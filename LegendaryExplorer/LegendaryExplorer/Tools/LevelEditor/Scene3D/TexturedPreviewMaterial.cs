using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using LegendaryExplorerCore.Unreal.Classes;
using SharpDX.Direct3D11;
using System;
using System.Diagnostics;
using System.Linq;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

public class TexturedPreviewMaterial : ModelPreviewMaterial<WorldVertex>
{
    private readonly PreviewTextureCache.TextureEntry DiffTexture;

    public TexturedPreviewMaterial(MeshRenderContext renderContext, ExportEntry export) : base(renderContext, export)
    {
        Properties.Add("Name", export.ObjectName.Instanced);
        if (FindDiffuse(export, Material) is IEntry diffEntry)
        {
            DiffTexture = renderContext.TextureCache.LoadTexture(diffEntry, renderContext.PackageCache);
        }
    }

    /// <summary>
    /// Guesses which of a material's textures is the diffuse texture, based on texture names.
    /// </summary>
    internal static IEntry FindDiffuse(ExportEntry matExport, MaterialInstanceConstantLevelEditor mat)
    {
        string matPackage = matExport.Parent?.InstancedFullPath.ToLower();
        string matName = matExport.ObjectName.Instanced;
        foreach (var textureEntry in mat.Textures)
        {
            var texObjectName = textureEntry.InstancedFullPath.ToLower();
            if ((matPackage == null || texObjectName.StartsWith(matPackage)) && texObjectName.Contains("diff"))
            {
                // we have found the diffuse texture!
                //Debug.WriteLine("Diffuse texture of new material <" + matName + "> is " + textureEntry.InstancedFullPath);
                return textureEntry;
            }
        }

        foreach (var textureEntry in mat.Textures.Reverse())
        {
            var texObjectName = textureEntry.ObjectName.Name.ToLower();
            if (texObjectName.Contains("diff") || texObjectName.Contains("tex"))
            {
                // we have found the diffuse texture!
                //Debug.WriteLine("Diffuse texture of new material <" + matName + "> is " + textureEntry.InstancedFullPath);
                return textureEntry;
            }
        }
        foreach (var texparam in mat.Textures)
        {
            var texObjectName = texparam.ObjectName.Name.ToLower();

            if (texObjectName.Contains("detail"))
            {
                // I guess a detail texture is good enough if we didn't return for a diffuse texture earlier...
                //Debug.WriteLine("Diffuse (Detail) texture of new material <" + matName + "> is " + texparam.InstancedFullPath);
                return texparam;
            }
        }
        foreach (var texparam in mat.Textures)
        {
            var texObjectName = texparam.ObjectName.Name.ToLower();
            if (!texObjectName.Contains("norm") && !texObjectName.Contains("opac"))
            {
                //Anything is better than nothing I suppose
                //Debug.WriteLine("Using first found texture (last resort)  of new material <" + matName + "> as diffuse: " + texparam.InstancedFullPath);
                return texparam;
            }
        }
        return null;
    }

    /// <summary>
    /// Uses LEX's default shader to render the given <see cref="ModelPreviewSection"/> of a <see cref="ModelPreviewLOD"/>. 
    /// </summary>
    /// <param name="lod">The LOD to render.</param>
    /// <param name="s">Which faces to render.</param>
    public override void RenderSection(ModelPreviewLOD<WorldVertex> lod, ModelPreviewSection s, MeshRenderContext context)
    {
        context.DefaultEffect.PrepDraw(context.ImmediateContext, context.AlphaBlendState, context.GetWorldConstants(lod.Mesh.LocalToWorld));

        ShaderResourceView diffTextureView = DiffTexture?.GetView(context.TexturePixels(lod.Mesh.TransformedBounds)) ?? context.DefaultTextureView;

        context.DefaultEffect.RenderObject(
            context.ImmediateContext,
            lod.Mesh,
            (int)s.StartIndex,
            (int)s.TriangleCount * 3,
            context.Wireframe ? null : diffTextureView);
    }

    protected override MaterialInstanceConstantLevelEditor CreateMaterial(MeshRenderContext renderContext, ExportEntry export)
    {
        return new MaterialInstanceConstantLevelEditor(export, renderContext.PackageCache, true);
    }
}
