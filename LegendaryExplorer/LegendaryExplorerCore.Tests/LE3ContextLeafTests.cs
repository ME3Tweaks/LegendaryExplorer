using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3ContextLeafTests
{
    [TestMethod]
    [DataRow("LightVector", false)]
    [DataRow("LightVector", true)]
    [DataRow("LightmapUVs", false)]
    [DataRow("LightmapUVs", true)]
    [DataRow("PerInstanceRandom", false)]
    [DataRow("PerInstanceRandom", true)]
    [DataRow("OcclusionPercentage", false)]
    [DataRow("OcclusionPercentage", true)]
    [DataRow("LensFlareIntensity", false)]
    [DataRow("LensFlareIntensity", true)]
    [DataRow("LensFlareOcclusion", false)]
    [DataRow("LensFlareOcclusion", true)]
    [DataRow("LensFlareRadialDistance", false)]
    [DataRow("LensFlareRadialDistance", true)]
    [DataRow("LensFlareRayDistance", false)]
    [DataRow("LensFlareRayDistance", true)]
    [DataRow("LensFlareSourceDistance", false)]
    [DataRow("LensFlareSourceDistance", true)]
    public void PixelLeavesHaveExactNativeTypeTextAndNoUniforms(string kind, bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Leaf(package, material, kind);
        string property = kind == "LightmapUVs" ? "Distortion" : kind == "LightVector" ? "EmissiveColor" : "Opacity";
        material.WriteProperty(Input(property, node)); var compiler = new LE3MaterialShaderCompiler(material); var result = compiler.TranslateMaterialHlsl(Template(), half); Success(result);
        string t = half ? "half" : "float";
        Assert.AreEqual(kind switch
        {
            "LightVector" => "\treturn Parameters.TangentLightVector",
            "LightmapUVs" => $"\t{t}2 Local0 = GetLightmapUVs(Parameters);\r\n\treturn Local0",
            "PerInstanceRandom" => "\treturn Parameters.VertexColor.x",
            "LensFlareIntensity" or "LensFlareRadialDistance" or "LensFlareRayDistance" or "LensFlareSourceDistance" => $"\t{t} Local0 = Get{kind}(Parameters);\r\n\treturn Local0",
            _ => $"\t{t} Local0 = GetOcclusionPercentage();\r\n\treturn Local0"
        }, result.PropertyCode[property]);
        Assert.AreEqual(kind == "LightmapUVs", result.UsesLightmapUVs); Assert.IsFalse(result.UsesVertexColor); Assert.IsFalse(result.UsesTime);
        Assert.IsFalse(result.UsesSceneDepth); Assert.IsFalse(result.UsesSceneColor); Assert.AreEqual(0u, result.NumUserTexCoords); Assert.AreEqual(0u, result.UsingTransforms);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count + result.Uniform2DTextureExpressions.Count);
        Assert.AreEqual(kind == "LightmapUVs", compiler.BuildProvisionalMaterialMacros(MaterialResource.Create(), result).ContainsKey("LIGHTMAP_UV_ACCESS"));
        Assert.IsFalse(compiler.BuildProvisionalMaterialMacros(MaterialResource.Create(), result).ContainsKey("MATERIAL_USES_VERTEX_COLOR"));
    }

    [TestMethod]
    [DataRow("LightVector")]
    [DataRow("LightmapUVs")]
    [DataRow("PerInstanceRandom")]
    [DataRow("OcclusionPercentage")]
    [DataRow("LensFlareIntensity")]
    [DataRow("LensFlareOcclusion")]
    [DataRow("LensFlareRadialDistance")]
    [DataRow("LensFlareRayDistance")]
    [DataRow("LensFlareSourceDistance")]
    public void ContextLeavesRejectVertexBeforeUsageMutation(string kind)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); material.WriteProperty(Input("WorldPositionOffset", Leaf(package, material, kind)));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()); Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
        CollectionAssert.AreEqual(new[] { "Invalid node used in vertex shader input!" }, result.Errors.ToArray()); Assert.IsFalse(result.UsesLightmapUVs); Assert.IsFalse(result.UsesVertexColor);
    }

    [TestMethod]
    [DataRow("PerInstanceRandom", false)]
    [DataRow("PerInstanceRandom", true)]
    [DataRow("OcclusionPercentage", false)]
    [DataRow("OcclusionPercentage", true)]
    [DataRow("LensFlareIntensity", false)]
    [DataRow("LensFlareIntensity", true)]
    [DataRow("LensFlareOcclusion", false)]
    [DataRow("LensFlareOcclusion", true)]
    [DataRow("LensFlareRadialDistance", false)]
    [DataRow("LensFlareRadialDistance", true)]
    [DataRow("LensFlareRayDistance", false)]
    [DataRow("LensFlareRayDistance", true)]
    [DataRow("LensFlareSourceDistance", false)]
    [DataRow("LensFlareSourceDistance", true)]
    public void ExplicitFloat1CannotBroadcastIntoVectorArithmetic(string kind, bool scalarOnRight)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var leaf = Leaf(package, material, kind);
        var vector = package.CreateExport("Vector", "MaterialExpressionConstant3Vector", material, indexed: false);
        var add = package.CreateExport("Add", "MaterialExpressionAdd", material, indexed: false); add.WriteProperty(Input("A", scalarOnRight ? vector : leaf)); add.WriteProperty(Input("B", scalarOnRight ? leaf : vector)); material.WriteProperty(Input("EmissiveColor", add));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()); Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl); StringAssert.Contains(string.Join("; ", result.Errors), "native numeric types");
    }

    [TestMethod]
    [DataRow("PerInstanceRandom")]
    [DataRow("OcclusionPercentage")]
    [DataRow("LensFlareIntensity")]
    [DataRow("LensFlareOcclusion")]
    [DataRow("LensFlareRadialDistance")]
    [DataRow("LensFlareRayDistance")]
    [DataRow("LensFlareSourceDistance")]
    public void MaskedExplicitFloat1RegainsGenericScalarArithmetic(string kind)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var leaf = Leaf(package, material, kind);
        var vector = package.CreateExport("Vector", "MaterialExpressionConstant3Vector", material, indexed: false);
        var add = package.CreateExport("Add", "MaterialExpressionAdd", material, indexed: false);
        var masked = Input("A", leaf); masked.Properties.Add(new IntProperty(1, "Mask")); masked.Properties.Add(new IntProperty(1, "MaskR")); add.WriteProperty(masked); add.WriteProperty(Input("B", vector)); material.WriteProperty(Input("EmissiveColor", add));
        Success(new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SelectedLightmapUsageDoesNotInheritCookedFlagsAndResetsPerTranslation(bool resourceMode)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var leaf = Leaf(package, material, "LightmapUVs");
        var constant = package.CreateExport("Constant", "MaterialExpressionConstant2Vector", material, indexed: false);
        var branch = package.CreateExport("Switch", "MaterialExpressionStaticSwitchParameter", material, indexed: false); branch.WriteProperty(Input("A", leaf)); branch.WriteProperty(Input("B", constant)); branch.WriteProperty(new BoolProperty(true, "DefaultValue")); material.WriteProperty(Input("Distortion", branch));
        var compiler = new LE3MaterialShaderCompiler(material); var resource = MaterialResource.Create(); resource.bUsesLightmapUVs = true;
        var first = Translate(); Success(first); Assert.IsTrue(first.UsesLightmapUVs); Assert.IsTrue(compiler.BuildProvisionalMaterialMacros(resource, first).ContainsKey("LIGHTMAP_UV_ACCESS"));
        branch.WriteProperty(new BoolProperty(false, "DefaultValue")); var second = Translate(); Success(second); Assert.IsFalse(second.UsesLightmapUVs);
        Assert.IsFalse(compiler.BuildProvisionalMaterialMacros(resource, second).ContainsKey("LIGHTMAP_UV_ACCESS")); Assert.IsTrue(resource.bUsesLightmapUVs);
        Assert.IsFalse(compiler.BuildProvisionalMaterialMacros(resource, second.Graph).ContainsKey("LIGHTMAP_UV_ACCESS"));
        TranslationResult Translate() => resourceMode ? compiler.TranslateMaterialResourceHlsl(Template()) : compiler.TranslateMaterialHlsl(Template());
    }

    [TestMethod]
    public void DiscardedLightmassLightmapBranchStillSetsUsageWithoutTextureCoordinateCount()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        var leaf = Leaf(package, material, "LightmapUVs"); var literal = package.CreateExport("Value", "MaterialExpressionConstant", material, indexed: false);
        var replace = package.CreateExport("Replace", "MaterialExpressionLightmassReplace", material, indexed: false); replace.WriteProperty(Input("Realtime", literal)); replace.WriteProperty(Input("Lightmass", leaf)); material.WriteProperty(Input("Opacity", replace));
        var compiler = new LE3MaterialShaderCompiler(material); var result = compiler.TranslateMaterialHlsl(Template()); Success(result); Assert.IsTrue(result.UsesLightmapUVs); Assert.AreEqual(0u, result.NumUserTexCoords);
        Assert.IsTrue(compiler.BuildProvisionalMaterialMacros(MaterialResource.Create(), result).ContainsKey("LIGHTMAP_UV_ACCESS")); StringAssert.Contains(result.PropertyCode["Opacity"], "GetLightmapUVs(Parameters)");
    }

    [TestMethod]
    public void LightmapUsageRemainsDiagnosticIfLaterRootFails()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        material.WriteProperty(Input("EmissiveColor", Leaf(package, material, "LightmapUVs"))); material.WriteProperty(Input("Opacity", package.CreateExport("Bad", "MaterialExpressionComment", material, indexed: false)));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()); Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl); Assert.IsTrue(result.UsesLightmapUVs);
    }

    [TestMethod]
    [DataRow("LightVector")]
    [DataRow("LightmapUVs")]
    [DataRow("PerInstanceRandom")]
    [DataRow("OcclusionPercentage")]
    [DataRow("LensFlareIntensity")]
    [DataRow("LensFlareOcclusion")]
    [DataRow("LensFlareRadialDistance")]
    [DataRow("LensFlareRayDistance")]
    [DataRow("LensFlareSourceDistance")]
    public void PropertyLocalCachingReusesCallbackOnlyWithinEachRoot(string kind)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var leaf = Leaf(package, material, kind);
        var add = package.CreateExport("Add", "MaterialExpressionAdd", material, indexed: false); add.WriteProperty(Input("A", leaf)); add.WriteProperty(Input("B", leaf)); material.WriteProperty(Input("EmissiveColor", add)); material.WriteProperty(Input("DiffuseColor", add));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()); Success(result);
        bool hasLocal = kind != "LightVector" && kind != "PerInstanceRandom";
        string helper = kind is "OcclusionPercentage" or "LensFlareOcclusion" ? "GetOcclusionPercentage(" : $"Get{kind}(";
        if (hasLocal)
        {
            Assert.AreEqual(1, result.PropertyCode["EmissiveColor"].Split(helper).Length - 1);
            Assert.AreEqual(1, result.PropertyCode["DiffuseColor"].Split(helper).Length - 1);
        }
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count);
    }

    [TestMethod]
    [DataRow("LensFlareIntensity")]
    [DataRow("LensFlareOcclusion")]
    [DataRow("LensFlareRadialDistance")]
    [DataRow("LensFlareRayDistance")]
    [DataRow("LensFlareSourceDistance")]
    public void InactiveLensFlareBranchIsNotVisitedByVertexTranslation(string kind)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        var leaf = Leaf(package, material, kind); var literal = package.CreateExport("Value", "MaterialExpressionConstant", material, indexed: false);
        var branch = package.CreateExport("Switch", "MaterialExpressionStaticSwitchParameter", material, indexed: false);
        branch.WriteProperty(Input("A", leaf)); branch.WriteProperty(Input("B", literal)); branch.WriteProperty(new BoolProperty(false, "DefaultValue"));
        material.WriteProperty(Input("WorldPositionOffset", branch));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()); Success(result);
        Assert.IsFalse(result.PropertyCode["WorldPositionOffset"].Contains("GetLensFlare"));
        Assert.IsFalse(result.PropertyCode["WorldPositionOffset"].Contains("GetOcclusionPercentage"));
        Assert.IsFalse(result.UsesLightmapUVs); Assert.IsFalse(result.UsesVertexColor);
    }

    private static IMEPackage Package() => MEPackageHandler.CreateMemoryEmptyPackage("ContextLeaf.pcc", MEGame.LE3);
    private static ExportEntry Material(IMEPackage package) => package.CreateExport("Material", "Material", indexed: false);
    private static ExportEntry Leaf(IMEPackage package, ExportEntry material, string kind) => package.CreateExport("Leaf", "MaterialExpression" + kind, material, indexed: false);
    private static StructProperty Input(string name, ExportEntry node) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(node.UIndex, "Expression") }, name);
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
    private static void Success(TranslationResult result) => Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
}
