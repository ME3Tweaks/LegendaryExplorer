using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3TwoSidedSignTests
{
    [TestMethod]
    [DataRow(false, null)]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, null)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void PixelCompileEmitsInlineGenericScalarIndependentOfMaterialTwoSided(bool half, bool? twoSided)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("TwoSidedSign.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        if (twoSided.HasValue) material.WriteProperty(new BoolProperty(twoSided.Value, "TwoSided"));
        var sign = Sign(package, material);
        material.WriteProperty(Input("Opacity", sign));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual("\treturn Parameters.TwoSidedSign", result.PropertyCode["Opacity"]);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count +
            result.UniformVertexScalarExpressions.Count + result.UniformVertexVectorExpressions.Count);
        Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count + result.UniformCubeTextureExpressions.Count + result.UniformExpressionTextures.Count);
        Assert.AreEqual(0u, result.UsingTransforms);
        Assert.IsFalse(result.UsesTime);
        Assert.IsFalse(result.UsesSceneDepth);
        Assert.IsFalse(result.UsesSceneColor);
        Assert.IsFalse(result.UsesVertexColor);
    }

    [TestMethod]
    [DataRow("Add", false, false)]
    [DataRow("Add", false, true)]
    [DataRow("Add", true, false)]
    [DataRow("Add", true, true)]
    [DataRow("Multiply", false, false)]
    [DataRow("Multiply", false, true)]
    [DataRow("Multiply", true, false)]
    [DataRow("Multiply", true, true)]
    public void GenericScalarBroadcastsAcrossVectorArithmetic(string operation, bool half, bool scalarOnRight)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("TwoSidedSignArithmetic.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var sign = Sign(package, material);
        var vector = package.CreateExport("Vector", "MaterialExpressionConstant3Vector", material, indexed: false);
        vector.WriteProperty(new FloatProperty(1, "R"));
        vector.WriteProperty(new FloatProperty(2, "G"));
        vector.WriteProperty(new FloatProperty(3, "B"));
        var arithmetic = package.CreateExport("Arithmetic", "MaterialExpression" + operation, material, indexed: false);
        arithmetic.WriteProperty(Input("A", scalarOnRight ? vector : sign));
        arithmetic.WriteProperty(Input("B", scalarOnRight ? sign : vector));
        material.WriteProperty(Input("EmissiveColor", arithmetic));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        string numericType = half ? "half" : "float";
        string operand = scalarOnRight && operation != "Multiply" ? $"({numericType}3(1.00000000,2.00000000,3.00000000) {Operator(operation)} Parameters.TwoSidedSign)"
            : $"(Parameters.TwoSidedSign {Operator(operation)} {numericType}3(1.00000000,2.00000000,3.00000000))";
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], $"{numericType}3 Local0 = {operand};");
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(false)]
    [DataRow(true)]
    public void VertexCompileRejectsTwoSidedSign(bool? twoSided)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("TwoSidedSignVertex.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        if (twoSided.HasValue) material.WriteProperty(new BoolProperty(twoSided.Value, "TwoSided"));
        var sign = Sign(package, material);
        material.WriteProperty(Input("WorldPositionOffset", sign));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success);
        Assert.IsNull(result.MaterialHlsl);
        CollectionAssert.AreEqual(new[] { "Invalid node used in vertex shader input!" }, result.Errors.ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InactiveStaticBranchDoesNotVisitTwoSidedSign(bool signBranchActive)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("TwoSidedSignStaticBranch.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var sign = Sign(package, material);
        var constant = package.CreateExport("Constant", "MaterialExpressionConstant", material, indexed: false);
        constant.WriteProperty(new FloatProperty(.25f, "R"));
        var branch = package.CreateExport("Branch", "MaterialExpressionStaticSwitchParameter", material, indexed: false);
        branch.WriteProperty(new NameProperty("UseSign", "ParameterName"));
        branch.WriteProperty(new BoolProperty(signBranchActive, "DefaultValue"));
        branch.WriteProperty(Input("A", sign));
        branch.WriteProperty(Input("B", constant));
        material.WriteProperty(Input("Opacity", branch));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(signBranchActive ? "\treturn Parameters.TwoSidedSign" : "\treturn float(0.25000000)", result.PropertyCode["Opacity"]);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
        Assert.IsFalse(result.UsesTime);
    }

    [TestMethod]
    public void FailedVertexTranslationDoesNotPoisonFreshPixelTranslation()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("TwoSidedSignFreshState.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var sign = Sign(package, material);
        material.WriteProperty(Input("WorldPositionOffset", sign));
        var compiler = new LE3MaterialShaderCompiler(material);

        var rejected = compiler.TranslateMaterialHlsl(Template());
        Assert.IsFalse(rejected.Success);
        CollectionAssert.AreEqual(new[] { "Invalid node used in vertex shader input!" }, rejected.Errors.ToArray());

        material.WriteProperty(Input("WorldPositionOffset", null));
        material.WriteProperty(Input("Opacity", sign));
        var accepted = compiler.TranslateMaterialHlsl(Template());
        Assert.IsTrue(accepted.Success, string.Join("; ", accepted.Errors));
        CollectionAssert.AreEqual(Array.Empty<string>(), accepted.Errors.ToArray());
        Assert.AreEqual("\treturn Parameters.TwoSidedSign", accepted.PropertyCode["Opacity"]);
        Assert.IsFalse(accepted.UsesTime);
        Assert.AreEqual(0u, accepted.UsingTransforms);
    }

    private static ExportEntry Sign(IMEPackage package, ExportEntry material) =>
        package.CreateExport("Sign", "MaterialExpressionTwoSidedSign", material, indexed: false);

    private static string Operator(string operation) => operation == "Add" ? "+" : "*";

    private static StructProperty Input(string name, ExportEntry expression) =>
        new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression?.UIndex ?? 0, "Expression") }, name);

    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
}
