using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3RotateAboutAxisTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ConstantInputsAlwaysEmitLocalAndAreLegalInBothStages(bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        var node = Node(package, material, Vector(package, material, "Axis", 4), Vector(package, material, "Pivot", 3), Vector(package, material, "Position", 3));
        material.WriteProperty(Input("EmissiveColor", node)); material.WriteProperty(Input("WorldPositionOffset", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half); Success(result);
        string t = half ? "half" : "float";
        Assert.AreEqual($"\t{t}3 Local0 = RotateAboutAxis({t}4(1.00000000,2.00000000,3.00000000,4.00000000),{t}3(1.00000000,2.00000000,3.00000000),{t}3(1.00000000,2.00000000,3.00000000));\r\n\treturn Local0", result.PropertyCode["EmissiveColor"]);
        StringAssert.Contains(result.PropertyCode["WorldPositionOffset"], "Local1 = RotateAboutAxis(");
        Assert.AreEqual(0u, result.UsingTransforms); Assert.IsFalse(result.UsesTime);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count);
    }

    [TestMethod]
    [DataRow("NormalizedRotationAxisAndAngle", 1, true)]
    [DataRow("NormalizedRotationAxisAndAngle", 2, false)]
    [DataRow("NormalizedRotationAxisAndAngle", 3, false)]
    [DataRow("NormalizedRotationAxisAndAngle", 4, true)]
    [DataRow("PositionOnAxis", 1, true)]
    [DataRow("PositionOnAxis", 2, false)]
    [DataRow("PositionOnAxis", 3, true)]
    [DataRow("PositionOnAxis", 4, false)]
    [DataRow("Position", 1, true)]
    [DataRow("Position", 2, false)]
    [DataRow("Position", 3, true)]
    [DataRow("Position", 4, false)]
    public void NumericCoercionReplicatesOnlyGenericScalarsAndDoesNotTruncate(string input, int width, bool success)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        var node = Node(package, material, Vector(package, material, "Axis", 4), Vector(package, material, "Pivot", 3), Vector(package, material, "Position", 3));
        node.WriteProperty(Input(input, Vector(package, material, "Replacement", width))); material.WriteProperty(Input("EmissiveColor", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()); Assert.AreEqual(success, result.Success, string.Join("; ", result.Errors));
        if (!success) { Assert.IsNull(result.MaterialHlsl); StringAssert.Contains(string.Join("; ", result.Errors), "Coercion failed:"); }
        else if (width == 1) StringAssert.Contains(result.PropertyCode["EmissiveColor"], input == "NormalizedRotationAxisAndAngle"
            ? "float4(float(1.00000000),float(1.00000000),float(1.00000000),float(1.00000000))"
            : "float3(float(1.00000000),float(1.00000000),float(1.00000000))");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DynamicScalarPackingAndCallbackAccessArePositionPivotAxis(bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        var node = Node(package, material, Parameter(package, material, "Axis"), Parameter(package, material, "Pivot"), Parameter(package, material, "Position"));
        material.WriteProperty(Input("EmissiveColor", node)); material.WriteProperty(Input("WorldPositionOffset", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half); Success(result);
        CollectionAssert.AreEqual(new[] { "Position", "Pivot", "Axis" }, result.UniformPixelScalarExpressions.Cast<MaterialUniformExpressionScalarParameter>().Select(p => p.ParameterName.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "Position", "Pivot", "Axis" }, result.UniformVertexScalarExpressions.Cast<MaterialUniformExpressionScalarParameter>().Select(p => p.ParameterName.Name).ToArray());
        string t = half ? "half" : "float";
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], $"RotateAboutAxis({t}4(UniformPixelScalars_0.z,UniformPixelScalars_0.z,UniformPixelScalars_0.z,UniformPixelScalars_0.z),{t}3(UniformPixelScalars_0.y,UniformPixelScalars_0.y,UniformPixelScalars_0.y),{t}3(UniformPixelScalars_0.x,UniformPixelScalars_0.x,UniformPixelScalars_0.x))");
        Assert.IsFalse(result.UniformPixelScalarExpressions.Any(e => e is MaterialUniformExpressionFoldedMath));
    }

    [TestMethod]
    [DataRow("NormalizedRotationAxisAndAngle")]
    [DataRow("PositionOnAxis")]
    [DataRow("Position")]
    public void RequiredInputChecksHappenBeforeAnyVisits(string missing)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        var time = package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false); var node = Node(package, material, time, time, time);
        node.WriteProperty(new StructProperty("ExpressionInput", new PropertyCollection { new ObjectProperty(0, "Expression") }, missing)); material.WriteProperty(Input("EmissiveColor", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()); Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl); Assert.IsFalse(result.UsesTime);
        CollectionAssert.AreEqual(new[] { "Missing RotateAboutAxis input " + missing }, result.Errors.ToArray());
    }

    [TestMethod]
    [DataRow("NormalizedRotationAxisAndAngle")]
    [DataRow("PositionOnAxis")]
    [DataRow("Position")]
    public void CyclicInputsFailAndFreshTranslationsRecover(string input)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        var axis = Vector(package, material, "Axis", 4); var pivot = Vector(package, material, "Pivot", 3); var position = Vector(package, material, "Position", 3);
        var node = Node(package, material, axis, pivot, position); node.WriteProperty(Input(input, node)); material.WriteProperty(Input("EmissiveColor", node));
        var compiler = new LE3MaterialShaderCompiler(material); var failed = compiler.TranslateMaterialHlsl(Template()); Assert.IsFalse(failed.Success); Assert.IsNull(failed.MaterialHlsl);
        node.WriteProperty(Input(input, input == "Position" ? position : input == "PositionOnAxis" ? pivot : axis)); Success(compiler.TranslateMaterialHlsl(Template()));
    }

    [TestMethod]
    [DataRow("NormalizedRotationAxisAndAngle")]
    [DataRow("PositionOnAxis")]
    [DataRow("Position")]
    public void ExplicitFloat1CannotBroadcastIntoAnyVectorArgument(string input)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        var node = Node(package, material, Vector(package, material, "Axis", 4), Vector(package, material, "Pivot", 3), Vector(package, material, "Position", 3));
        var depth = package.CreateExport("Depth", "MaterialExpressionPixelDepth", material, indexed: false); node.WriteProperty(Input(input, depth)); material.WriteProperty(Input("EmissiveColor", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()); Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl); StringAssert.Contains(string.Join("; ", result.Errors), "Coercion failed:");
    }

    [TestMethod]
    public void CallbackCannotTruncateDynamicVectorParameterAndDoesNotPackFailedCoercion()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        var vector = package.CreateExport("Position", "MaterialExpressionVectorParameter", material, indexed: false); vector.WriteProperty(new NameProperty("Position", "ParameterName"));
        var node = Node(package, material, Vector(package, material, "Axis", 4), Vector(package, material, "Pivot", 3), vector); material.WriteProperty(Input("EmissiveColor", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()); Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl); Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
    }

    [TestMethod]
    public void FailedPositionStillVisitsAxisAndPivotBeforeCallbackFailure()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        var axis = package.CreateExport("Axis", "MaterialExpressionTime", material, indexed: false);
        var bad = package.CreateExport("Position", "MaterialExpressionComment", material, indexed: false);
        var node = Node(package, material, axis, Vector(package, material, "Pivot", 3), bad); material.WriteProperty(Input("EmissiveColor", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()); Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl); Assert.IsTrue(result.UsesTime);
    }

    [TestMethod]
    public void VisitationAndCoercionOrderPreserveDiagnosticTimeAndTransformState()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        var world = package.CreateExport("Position", "MaterialExpressionWorldPosition", material, indexed: false);
        var axis = package.CreateExport("Axis", "MaterialExpressionTime", material, indexed: false);
        var node = Node(package, material, axis, Vector(package, material, "Pivot", 3), world); material.WriteProperty(Input("EmissiveColor", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()); Success(result); Assert.IsTrue(result.UsesTime); Assert.AreEqual(8u, result.UsingTransforms);
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], ",Parameters.WorldPosition)");
    }

    private static IMEPackage Package() => MEPackageHandler.CreateMemoryEmptyPackage("RotateAboutAxis.pcc", MEGame.LE3);
    private static ExportEntry Material(IMEPackage package) => package.CreateExport("Material", "Material", indexed: false);
    private static ExportEntry Node(IMEPackage package, ExportEntry material, ExportEntry axis, ExportEntry pivot, ExportEntry position)
    { var node = package.CreateExport("Rotate", "MaterialExpressionRotateAboutAxis", material, indexed: false); node.WriteProperty(Input("NormalizedRotationAxisAndAngle", axis)); node.WriteProperty(Input("PositionOnAxis", pivot)); node.WriteProperty(Input("Position", position)); return node; }
    private static ExportEntry Vector(IMEPackage package, ExportEntry material, string name, int width)
    { var node = package.CreateExport(name, width == 1 ? "MaterialExpressionConstant" : $"MaterialExpressionConstant{width}Vector", material, indexed: false); for (int i = 0; i < width; i++) node.WriteProperty(new FloatProperty(i + 1, "RGBA"[i].ToString())); return node; }
    private static ExportEntry Parameter(IMEPackage package, ExportEntry material, string name)
    { var node = package.CreateExport(name, "MaterialExpressionScalarParameter", material, indexed: false); node.WriteProperty(new NameProperty(name, "ParameterName")); return node; }
    private static StructProperty Input(string name, ExportEntry expression) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression.UIndex, "Expression") }, name);
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
    private static void Success(LE3MaterialShaderCompiler.TranslationResult result) => Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
}
