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
public class LE3FactoryLeafTests
{
    [TestMethod]
    [DataRow("FoliageImpulseDirection", false, false)]
    [DataRow("FoliageImpulseDirection", false, true)]
    [DataRow("FoliageImpulseDirection", true, false)]
    [DataRow("FoliageImpulseDirection", true, true)]
    [DataRow("FoliageNormalizedRotationAxisAndAngle", false, false)]
    [DataRow("FoliageNormalizedRotationAxisAndAngle", false, true)]
    [DataRow("FoliageNormalizedRotationAxisAndAngle", true, false)]
    [DataRow("FoliageNormalizedRotationAxisAndAngle", true, true)]
    [DataRow("WindDirectionAndSpeed", false, false)]
    [DataRow("WindDirectionAndSpeed", false, true)]
    [DataRow("WindDirectionAndSpeed", true, false)]
    [DataRow("WindDirectionAndSpeed", true, true)]
    public void GlobalLeavesAreInlineInEitherStageWithoutUsageOrUniforms(string kind, bool vertex, bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Leaf(package, material, kind);
        string property = vertex ? "WorldPositionOffset" : "EmissiveColor"; material.WriteProperty(Input(property, node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half); Success(result);
        Assert.AreEqual("\treturn " + kind + (kind == "FoliageImpulseDirection" ? "" : ".rgb"), result.PropertyCode[property]);
        Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count + result.UniformVertexVectorExpressions.Count);
        NoUsage(result);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void MeshEmitterOverrideIsFixedWhiteVectorParameterInEitherStage(bool vertex, bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Leaf(package, material, "MeshEmitterDynamicParameter");
        node.WriteProperty(new NameProperty("IgnoredName", "ParameterName")); node.WriteProperty(new FloatProperty(7, "DefaultValue"));
        string property = vertex ? "WorldPositionOffset" : "EmissiveColor"; material.WriteProperty(Input(property, node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half); Success(result);
        Assert.AreEqual($"\treturn Uniform{(vertex ? "Vertex" : "Pixel")}Vector_0.rgb", result.PropertyCode[property]);
        var uniform = (MaterialUniformExpressionVectorParameter)(vertex ? result.UniformVertexVectorExpressions : result.UniformPixelVectorExpressions).Single();
        Assert.AreEqual("MeshEmitterDynamicParameter", uniform.ParameterName.Name); Assert.AreEqual(0, uniform.ParameterName.Number);
        Assert.AreEqual(1f, uniform.DefaultValue.R); Assert.AreEqual(1f, uniform.DefaultValue.G); Assert.AreEqual(1f, uniform.DefaultValue.B); Assert.AreEqual(1f, uniform.DefaultValue.A);
        NoUsage(result);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void ParticleMacroSelectsViewOrScreenLocalFloat2WithoutUserCoordinates(bool view, bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Leaf(package, material, "ParticleMacroUV"); node.WriteProperty(new BoolProperty(view, "bUseViewSpace")); material.WriteProperty(Input("Distortion", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half); Success(result);
        Assert.AreEqual($"\t{(half ? "half" : "float")}2 Local0 = Get{(view ? "View" : "Screen")}SpaceMacroUVs(Parameters);\r\n\treturn Local0", result.PropertyCode["Distortion"]);
        NoUsage(result); Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ParticleMacroRejectsVertexBeforeSideEffects(bool view)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Leaf(package, material, "ParticleMacroUV"); node.WriteProperty(new BoolProperty(view, "bUseViewSpace")); material.WriteProperty(Input("WorldPositionOffset", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()); Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
        CollectionAssert.AreEqual(new[] { "Invalid node used in vertex shader input!" }, result.Errors.ToArray()); NoUsage(result);
    }

    [TestMethod]
    public void AbsentMacroViewFlagDefaultsToScreenSpace()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); material.WriteProperty(Input("Distortion", Leaf(package, material, "ParticleMacroUV")));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()); Success(result); StringAssert.Contains(result.PropertyCode["Distortion"], "GetScreenSpaceMacroUVs(Parameters)");
    }

    [TestMethod]
    public void MeshOverrideDoesNotInheritOrdinaryDynamicParameterUsage()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var ordinary = Leaf(package, material, "DynamicParameter");
        var mesh = package.CreateExport("Mesh", "MaterialExpressionMeshEmitterDynamicParameter", material, indexed: false);
        var branch = package.CreateExport("Switch", "MaterialExpressionStaticSwitchParameter", material, indexed: false); branch.WriteProperty(Input("A", ordinary)); branch.WriteProperty(Input("B", mesh)); branch.WriteProperty(new BoolProperty(true, "DefaultValue")); material.WriteProperty(Input("EmissiveColor", branch));
        var compiler = new LE3MaterialShaderCompiler(material); var first = compiler.TranslateMaterialHlsl(Template()); Success(first); Assert.IsTrue(first.UsesDynamicParameter);
        branch.WriteProperty(new BoolProperty(false, "DefaultValue")); var second = compiler.TranslateMaterialHlsl(Template()); Success(second); Assert.IsFalse(second.UsesDynamicParameter); NoUsage(second);
        Assert.AreEqual(1, second.UniformPixelVectorExpressions.Count);
    }

    [TestMethod]
    public void MeshOverrideAndEquivalentAuthoredParameterInternAcrossStages()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var mesh = Leaf(package, material, "MeshEmitterDynamicParameter");
        var authored = package.CreateExport("Authored", "MaterialExpressionVectorParameter", material, indexed: false); authored.WriteProperty(new NameProperty("MeshEmitterDynamicParameter", "ParameterName"));
        authored.WriteProperty(new StructProperty("LinearColor", new PropertyCollection { new FloatProperty(1, "R"), new FloatProperty(1, "G"), new FloatProperty(1, "B"), new FloatProperty(1, "A") }, "DefaultValue", isImmutable: true));
        material.WriteProperty(Input("EmissiveColor", mesh)); material.WriteProperty(Input("WorldPositionOffset", authored));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()); Success(result); Assert.AreSame(result.UniformPixelVectorExpressions.Single(), result.UniformVertexVectorExpressions.Single());
    }

    private static void NoUsage(TranslationResult result)
    {
        Assert.IsFalse(result.UsesDynamicParameter); Assert.IsFalse(result.UsesVertexColor); Assert.IsFalse(result.UsesTime); Assert.IsFalse(result.UsesLightmapUVs);
        Assert.IsFalse(result.UsesSceneDepth); Assert.IsFalse(result.UsesSceneColor); Assert.AreEqual(0u, result.NumUserTexCoords); Assert.AreEqual(0u, result.UsingTransforms);
    }
    private static IMEPackage Package() => MEPackageHandler.CreateMemoryEmptyPackage("FactoryLeaves.pcc", MEGame.LE3);
    private static ExportEntry Material(IMEPackage package) => package.CreateExport("Material", "Material", indexed: false);
    private static ExportEntry Leaf(IMEPackage package, ExportEntry material, string kind) => package.CreateExport("Leaf", "MaterialExpression" + kind, material, indexed: false);
    private static StructProperty Input(string name, ExportEntry node) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(node.UIndex, "Expression") }, name);
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
    private static void Success(TranslationResult result) => Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
}
