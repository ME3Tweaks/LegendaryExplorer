using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3DepthDestinationTests
{
    [TestMethod]
    [DataRow("PixelDepth", false, false)]
    [DataRow("PixelDepth", false, true)]
    [DataRow("PixelDepth", true, false)]
    [DataRow("PixelDepth", true, true)]
    [DataRow("DestDepth", false, false)]
    [DataRow("DestDepth", false, true)]
    [DataRow("DestDepth", true, false)]
    [DataRow("DestDepth", true, true)]
    [DataRow("DestColor", false, false)]
    [DataRow("DestColor", false, true)]
    [DataRow("DestColor", true, false)]
    [DataRow("DestColor", true, true)]
    public void PixelStageKeepsInlineVersusLocalShapeAndIgnoresNormalize(string kind, bool half, bool normalize)
    {
        GlobalTest.Init(); using var package = Package(); var material = package.CreateExport("Material", "Material", indexed: false);
        var node = package.CreateExport("Node", "MaterialExpression" + kind, material, indexed: false); node.WriteProperty(new BoolProperty(normalize, "bNormalize"));
        string root = kind == "DestColor" ? "EmissiveColor" : "Opacity"; material.WriteProperty(Input(root, node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors)); Assert.AreEqual(kind == "DestColor", result.UsesSceneColor); Assert.AreEqual(kind == "DestDepth", result.UsesSceneDepth);
        string type = half ? "half" : "float";
        string expected = kind switch { "PixelDepth" => "\treturn Parameters.ScreenPosition.w", "DestDepth" => $"\t{type} Local0 = PreviousDepth(Parameters.ScreenPosition);\r\n\treturn Local0", _ => $"\t{type}4 Local0 = PreviousLighting(Parameters.ScreenPosition);\r\n\treturn Local0.rgb" };
        Assert.AreEqual(expected, result.PropertyCode[root]); Assert.AreEqual(0u, result.NumUserTexCoords);
        Assert.IsFalse(result.ForcesFloatTexCoords); Assert.IsFalse(result.UsesTime); Assert.AreEqual(0, result.UniformExpressionTextures.Count);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count);
    }

    [TestMethod]
    [DataRow("PixelDepth")]
    [DataRow("DestDepth")]
    [DataRow("DestColor")]
    public void VertexStageRejectsBeforeSceneUsage(string kind)
    {
        GlobalTest.Init(); using var package = Package(); var material = package.CreateExport("Material", "Material", indexed: false);
        var node = package.CreateExport("Node", "MaterialExpression" + kind, material, indexed: false); material.WriteProperty(Input("WorldPositionOffset", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsFalse(result.UsesSceneColor); Assert.IsFalse(result.UsesSceneDepth);
        CollectionAssert.AreEqual(new[] { "Invalid node used in vertex shader input!" }, result.Errors.ToArray());
    }

    [TestMethod]
    [DataRow("PixelDepth", false, false, true)]
    [DataRow("DestDepth", false, false, false)]
    [DataRow("DestDepth", true, true, true)]
    [DataRow("DestColor", false, false, false)]
    [DataRow("DestColor", true, false, true)]
    [DataRow("DestColor", true, true, false)]
    public void OrdinaryResourceSceneRulesApplyOnlyToDestinationReads(string kind, bool effect, bool decal, bool success)
    {
        GlobalTest.Init(); using var package = Package(); var material = package.CreateExport("Material", "Material", indexed: false);
        material.WriteProperty(new BoolProperty(effect, "bUsedWithMaterialEffect")); material.WriteProperty(new BoolProperty(decal, "bUsedWithDecals"));
        var node = package.CreateExport("Node", "MaterialExpression" + kind, material, indexed: false); material.WriteProperty(Input("EmissiveColor", node));
        var compiler = new LE3MaterialShaderCompiler(material); var result = compiler.TranslateMaterialResourceHlsl(Template());
        Assert.AreEqual(success, result.Success, string.Join("; ", result.Errors));
        if (!success) StringAssert.Contains(string.Join("; ", result.Errors), decal ? "Materials used with decals must not read from scene color." : "Only transparent materials can read from scene " + (kind == "DestColor" ? "color." : "depth."));
        material.WriteProperty(new StructProperty("ExpressionInput", new PropertyCollection { new ObjectProperty(0, "Expression") }, "EmissiveColor"));
        result = compiler.TranslateMaterialResourceHlsl(Template()); Assert.IsTrue(result.Success); Assert.IsFalse(result.UsesSceneColor); Assert.IsFalse(result.UsesSceneDepth);
    }

    [TestMethod]
    [DataRow("PixelDepth")]
    [DataRow("DestDepth")]
    public void ExplicitFloat1DoesNotGainGenericScalarVectorBroadcast(string kind)
    {
        GlobalTest.Init(); using var package = Package(); var material = package.CreateExport("Material", "Material", indexed: false);
        var node = package.CreateExport("Node", "MaterialExpression" + kind, material, indexed: false); var vector = package.CreateExport("Vector", "MaterialExpressionConstant3Vector", material, indexed: false);
        var multiply = package.CreateExport("Multiply", "MaterialExpressionMultiply", material, indexed: false); multiply.WriteProperty(Input("A", node)); multiply.WriteProperty(Input("B", vector)); material.WriteProperty(Input("EmissiveColor", multiply));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()); Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
    }
    private static IMEPackage Package() => MEPackageHandler.CreateMemoryEmptyPackage("LE3DepthDestination.pcc", MEGame.LE3);
    private static StructProperty Input(string name, ExportEntry expression) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression.UIndex, "Expression") }, name);
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
}
