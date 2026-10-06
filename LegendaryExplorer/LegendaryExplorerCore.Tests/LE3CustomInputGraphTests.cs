using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3CustomInputGraphTests
{
    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("UV")]
    public void CustomGraphTraversesOnlyNonemptyNamedNestedInputs(string name)
    {
        using var f = Fixture();
        var time = Node(f, "Time", "MaterialExpressionTime");
        f.Custom.WriteProperty(Inputs(CustomInput(name, time)));
        f.Custom.WriteProperty(Input("Coordinates", f.Custom));
        var compiler = new LE3MaterialShaderCompiler(f.Material);
        foreach (var graph in new[] { compiler.AnalyzeGraph(), compiler.AnalyzeMaterialResourceGraph() })
        {
            Assert.IsTrue(graph.IsValid, string.Join("; ", graph.Errors));
            Assert.AreEqual(name.Length == 0 ? 1 : 2, graph.ReachableExpressions.Count);
            Assert.AreEqual(name.Length != 0, graph.ReachableExpressions.Contains(time));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NamedCustomSelfCycleIsRejectedButUnnamedSelfLinkIsIgnored(bool named)
    {
        using var f = Fixture();
        f.Custom.WriteProperty(Inputs(CustomInput(named ? "Loop" : "", f.Custom)));
        var graph = new LE3MaterialShaderCompiler(f.Material).AnalyzeGraph();
        Assert.AreEqual(!named, graph.IsValid);
        if (named) StringAssert.Contains(string.Join("; ", graph.Errors).ToLowerInvariant(), "cycle");
    }

    [TestMethod]
    public void NamedInputsKeepArrayOrderAndSkipUnnamedReferences()
    {
        using var f = Fixture();
        var a = Node(f, "A", "MaterialExpressionConstant");
        var b = Node(f, "B", "MaterialExpressionConstant2Vector");
        f.Custom.WriteProperty(Inputs(CustomInput("", f.Custom), CustomInput("First", a), CustomInput("", f.Custom), CustomInput("Second", b)));
        var graph = new LE3MaterialShaderCompiler(f.Material).AnalyzeGraph();
        Assert.IsTrue(graph.IsValid, string.Join("; ", graph.Errors));
        CollectionAssert.AreEqual(new[] { a.UIndex, b.UIndex, f.Custom.UIndex }, graph.ReachableExpressions.Select(node => node.UIndex).ToArray());
    }

    [TestMethod]
    public void NestedStaticSelectionControlsWhetherCycleIsActive()
    {
        using var f = Fixture();
        var selected = Node(f, "Selected", "MaterialExpressionConstant");
        var choice = Node(f, "Choice", "MaterialExpressionStaticSwitchParameter");
        choice.WriteProperty(Input("A", selected)); choice.WriteProperty(Input("B", f.Custom));
        choice.WriteProperty(new BoolProperty(true, "DefaultValue"));
        f.Custom.WriteProperty(Inputs(CustomInput("Choice", choice)));
        var compiler = new LE3MaterialShaderCompiler(f.Material);
        var graph = compiler.AnalyzeGraph();
        Assert.IsTrue(graph.IsValid, string.Join("; ", graph.Errors));
        Assert.AreEqual(3, graph.ReachableExpressions.Count);
        choice.WriteProperty(new BoolProperty(false, "DefaultValue"));
        Assert.IsFalse(compiler.AnalyzeGraph().IsValid);
    }

    [TestMethod]
    public void CustomTextureIgnoresAuthoredFakeGraphInputs()
    {
        using var f = Fixture();
        var textureNode = Node(f, "Texture", "MaterialExpressionCustomTexture");
        textureNode.WriteProperty(Input("Coordinates", textureNode));
        textureNode.WriteProperty(Inputs(CustomInput("Fake", f.Custom)));
        f.Material.WriteProperty(Input("EmissiveColor", textureNode));
        var graph = new LE3MaterialShaderCompiler(f.Material).AnalyzeGraph();
        Assert.IsTrue(graph.IsValid, string.Join("; ", graph.Errors));
        Assert.AreEqual(1, graph.ReachableExpressions.Count);
        Assert.AreSame(textureNode, graph.ReachableExpressions.Single());
    }

    [TestMethod]
    public void CustomTranslationDoesNotReadTextureMetadataForNumericInputs()
    {
        using var f = Fixture();
        f.Custom.WriteProperty(new StrProperty("return Value;", "Code"));
        f.Custom.WriteProperty(Inputs(CustomInput("Value", Node(f, "Value", "MaterialExpressionConstant"))));
        var compiler = new LE3MaterialShaderCompiler(f.Material, getTextureMetadata: _ => throw new AssertFailedException("No texture metadata needed."));
        var result = compiler.TranslateMaterialHlsl("%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n");
        Assert.IsTrue(result.Graph.IsValid, string.Join("; ", result.Graph.Errors));
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.MaterialHlsl, "float3 CustomExpression0(float Value)\r\n{\r\nreturn Value;\r\n}\r\n");
        Assert.AreEqual(0, result.UniformExpressionTextures.Count);
    }

    private sealed record TestFixture(IMEPackage Package, ExportEntry Material, ExportEntry Custom) : IDisposable
    {
        public void Dispose() => Package.Dispose();
    }

    private static TestFixture Fixture()
    {
        GlobalTest.Init();
        var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CustomGraph.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var custom = package.CreateExport("Custom", "MaterialExpressionCustom", material, indexed: false);
        material.WriteProperty(Input("EmissiveColor", custom));
        return new(package, material, custom);
    }
    private static ExportEntry Node(TestFixture f, string name, string className) => f.Package.CreateExport(name, className, f.Material, indexed: false);
    private static StructProperty Input(string name, ExportEntry node) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(node.UIndex, "Expression") }, name);
    private static StructProperty CustomInput(string name, ExportEntry node) => new("CustomInput", new PropertyCollection { new StrProperty(name, "InputName"), Input("Input", node) }, "Inputs");
    private static ArrayProperty<StructProperty> Inputs(params StructProperty[] inputs) => new(inputs, "Inputs");
}
