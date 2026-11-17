using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3NoncompilingExpressionTests
{
    private const string Template = "/*\n%u\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n#line %u\n*/";

    [TestMethod]
    [DataRow("MaterialExpressionComment")]
    [DataRow("MaterialExpressionCompound")]
    [DataRow("MaterialExpressionParameter")]
    public void SelectedNativeIndexNoneExpressionFailsActionably(string className)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var expression = Expression(package, material, className);
        material.WriteProperty(Connected("EmissiveColor", expression));
        var compiler = new LE3MaterialShaderCompiler(material);

        var graph = compiler.AnalyzeGraph();
        var resourceGraph = compiler.AnalyzeMaterialResourceGraph();
        var result = compiler.TranslateMaterialHlsl(Template);

        Assert.IsTrue(graph.IsValid, string.Join("; ", graph.Errors));
        Assert.IsTrue(resourceGraph.IsValid, string.Join("; ", resourceGraph.Errors));
        Assert.IsTrue(graph.ReachableExpressions.Contains(expression));
        Assert.IsFalse(result.Success);
        Assert.IsNull(result.MaterialHlsl);
        Assert.IsTrue(result.Errors.Any(error => error.Contains(className + " has no runtime compile implementation (native INDEX_NONE)", StringComparison.Ordinal)));
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
        Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
        Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count);
        Assert.AreEqual(0, result.UniformCubeTextureExpressions.Count);
        Assert.AreEqual(0, result.UniformExpressionTextures.Count);
        Assert.IsFalse(result.UsesTime);
        Assert.IsFalse(result.UsesVertexColor);
        Assert.AreEqual(0u, result.UsingTransforms);
        Assert.AreEqual(0u, result.NumUserTexCoords);
        Assert.IsFalse(result.UsesLightmapUVs);
        Assert.IsFalse(result.UsesDynamicParameter);
    }

    [TestMethod]
    [DataRow("MaterialExpressionComment")]
    [DataRow("MaterialExpressionCompound")]
    [DataRow("MaterialExpressionParameter")]
    public void NativeIndexNoneExpressionsIgnoreFakeInputs(string className)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var unsupported = Expression(package, material, className);
        var time = Expression(package, material, "MaterialExpressionTime");
        var worldPosition = Expression(package, material, "MaterialExpressionWorldPosition");
        unsupported.WriteProperty(new ObjectProperty(time.UIndex, "Input"));
        unsupported.WriteProperty(Connected("Coordinate", worldPosition));
        time.WriteProperty(new ObjectProperty(unsupported.UIndex, "Input"));
        material.WriteProperty(Connected("EmissiveColor", unsupported));
        var compiler = new LE3MaterialShaderCompiler(material);

        var graph = compiler.AnalyzeGraph();
        var resourceGraph = compiler.AnalyzeMaterialResourceGraph();

        Assert.IsTrue(graph.IsValid, string.Join("; ", graph.Errors));
        Assert.IsTrue(resourceGraph.IsValid, string.Join("; ", resourceGraph.Errors));
        Assert.AreEqual(1, graph.ReachableExpressions.Count);
        Assert.AreSame(unsupported, graph.ReachableExpressions[0]);
        Assert.AreEqual(1, resourceGraph.ReachableExpressions.Count);
        Assert.AreSame(unsupported, resourceGraph.ReachableExpressions[0]);
        Assert.IsFalse(graph.ReachableExpressions.Contains(time));
        Assert.IsFalse(graph.ReachableExpressions.Contains(worldPosition));
        Assert.IsFalse(resourceGraph.ReachableExpressions.Contains(time));
        Assert.IsFalse(resourceGraph.ReachableExpressions.Contains(worldPosition));
    }

    [TestMethod]
    public void InactiveStaticSwitchBranchMayContainNativeIndexNoneExpression()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var inactive = Expression(package, material, "MaterialExpressionComment");
        var selected = Constant(package, material, 3, 2f);
        var selector = Expression(package, material, "MaterialExpressionStaticSwitchParameter");
        selector.WriteProperty(new NameProperty("InactiveFeature", "ParameterName"));
        selector.WriteProperty(new BoolProperty(false, "DefaultValue"));
        selector.WriteProperty(Connected("A", inactive));
        selector.WriteProperty(Connected("B", selected));
        material.WriteProperty(Connected("EmissiveColor", selector));
        var compiler = new LE3MaterialShaderCompiler(material);

        var graph = compiler.AnalyzeGraph();
        var resourceGraph = compiler.AnalyzeMaterialResourceGraph();
        var result = compiler.TranslateMaterialHlsl(Template);

        Assert.IsTrue(graph.IsValid, string.Join("; ", graph.Errors));
        Assert.IsTrue(resourceGraph.IsValid, string.Join("; ", resourceGraph.Errors));
        Assert.IsFalse(graph.ReachableExpressions.Contains(inactive));
        Assert.IsTrue(graph.ReachableExpressions.Contains(selected));
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
    }

    [TestMethod]
    public void UnreachableEditorExpressionIsListedWithoutRejectingSelectedGraph()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var selected = Constant(package, material, 3, 1f);
        var editorOnly = Expression(package, material, "MaterialExpressionComment");
        material.WriteProperty(Connected("EmissiveColor", selected));
        material.WriteProperty(new ArrayProperty<ObjectProperty>(new[]
        {
            new ObjectProperty(editorOnly.UIndex, "Expressions")
        }, "Expressions"));
        var compiler = new LE3MaterialShaderCompiler(material);

        var graph = compiler.AnalyzeGraph();
        var resourceGraph = compiler.AnalyzeMaterialResourceGraph();
        var result = compiler.TranslateMaterialHlsl(Template);

        Assert.IsTrue(graph.IsValid, string.Join("; ", graph.Errors));
        Assert.IsTrue(resourceGraph.IsValid, string.Join("; ", resourceGraph.Errors));
        Assert.IsTrue(graph.UnreachableExpressions.Contains(editorOnly));
        Assert.IsTrue(resourceGraph.UnreachableExpressions.Contains(editorOnly));
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
    }

    [TestMethod]
    public void FakeInputsOnAllNativeIndexNoneClassesAreIgnoredByBothAnalyzers()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        foreach (string className in new[] { "MaterialExpressionComment", "MaterialExpressionCompound", "MaterialExpressionParameter" })
        {
            var expression = Expression(package, material, className);
            var input = Expression(package, material, "MaterialExpressionTime");
            expression.WriteProperty(Connected("Input", input));
            material.WriteProperty(new ArrayProperty<ObjectProperty>(new[] { new ObjectProperty(expression.UIndex, "Expressions") }, "Expressions"));
        }

        var compiler = new LE3MaterialShaderCompiler(material);
        var graph = compiler.AnalyzeGraph();
        var resourceGraph = compiler.AnalyzeMaterialResourceGraph();

        Assert.IsTrue(graph.IsValid, string.Join("; ", graph.Errors));
        Assert.IsTrue(resourceGraph.IsValid, string.Join("; ", resourceGraph.Errors));
        Assert.AreEqual(0, graph.ReachableExpressions.Count);
        Assert.AreEqual(0, resourceGraph.ReachableExpressions.Count);
    }

    private static IMEPackage Package() => MEPackageHandler.CreateMemoryEmptyPackage("LE3NoncompilingExpression.pcc", MEGame.LE3);
    private static ExportEntry Material(IMEPackage package) => package.CreateExport("Material", "Material", indexed: false);
    private static ExportEntry Expression(IMEPackage package, ExportEntry material, string className) =>
        package.CreateExport(className, className, material, indexed: false);
    private static ExportEntry Constant(IMEPackage package, ExportEntry material, int width, float value)
    {
        string className = width == 1 ? "MaterialExpressionConstant" : $"MaterialExpressionConstant{width}Vector";
        var expression = Expression(package, material, className);
        expression.WriteProperty(new FloatProperty(value, "R"));
        if (width > 1) expression.WriteProperty(new FloatProperty(value, "G"));
        if (width > 2) expression.WriteProperty(new FloatProperty(value, "B"));
        if (width > 3) expression.WriteProperty(new FloatProperty(value, "A"));
        return expression;
    }

    private static StructProperty Connected(string name, ExportEntry expression) => new("ExpressionInput", new PropertyCollection
    {
        new ObjectProperty(expression.UIndex, "Expression")
    }, name);
}
