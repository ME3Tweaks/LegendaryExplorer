using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3BioSimpleExpressionTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EffectFractionUsesFixedScalarParameterAndIgnoresAuthoredFields(bool half)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var effect = package.CreateExport("Effect", "BioMaterialExpressionEffectMatFractionEnabled", material, indexed: false);
        effect.WriteProperty(new NameProperty("AuthoredName", "ParameterName"));
        effect.WriteProperty(new FloatProperty(7f, "DefaultValue"));
        material.WriteProperty(Input("EmissiveColor", effect));
        material.WriteProperty(Input("WorldPositionOffset", effect));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        var pixel = (MaterialUniformExpressionScalarParameter)result.UniformPixelScalarExpressions.Single();
        var vertex = (MaterialUniformExpressionScalarParameter)result.UniformVertexScalarExpressions.Single();
        Assert.AreSame(pixel, vertex);
        Assert.AreEqual("FractionOfEffectEnabled", pixel.ParameterName.Name);
        Assert.AreEqual(0, pixel.ParameterName.Number);
        Assert.AreEqual(0f, pixel.DefaultValue);
        Assert.AreEqual(0u, result.UsingTransforms);
        Assert.IsFalse(result.UsesVertexColor);
        Assert.IsFalse(result.UsesLightmapUVs);
        Assert.IsFalse(result.UsesDynamicParameter);
        Assert.IsFalse(result.UsesTime);
        Assert.IsFalse(result.UsesSceneDepth);
        Assert.IsFalse(result.UsesSceneColor);
        Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count + result.UniformVertexVectorExpressions.Count);
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], "UniformPixelScalars_0");
        StringAssert.Contains(result.PropertyCode["WorldPositionOffset"], "UniformVertexScalars_0");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void HalfAngleIsLocalVectorInPixelAndVertex(bool half, bool vertex)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var halfAngle = package.CreateExport("HalfAngle", "BioMaterialExpressionHalfAngle", material, indexed: false);
        string property = vertex ? "WorldPositionOffset" : "EmissiveColor";
        material.WriteProperty(Input(property, halfAngle));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        string type = half ? "half3" : "float3";
        Assert.AreEqual($"\t{type} Local0 = normalize( Parameters.TangentLightVector + Parameters.TangentCameraVector );\r\n\treturn Local0", result.PropertyCode[property]);
        Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count + result.UniformPixelScalarExpressions.Count +
            result.UniformVertexVectorExpressions.Count + result.UniformVertexScalarExpressions.Count);
        Assert.AreEqual(0u, result.UsingTransforms);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void SpecPowerMultipliesConstantInputsByNativeScale(int width)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var input = Constant(package, material, width, 2f);
        var specPower = SpecPower(package, material, input);
        material.WriteProperty(Input("EmissiveColor", specPower));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], "2.00000000");
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], " * float(100.00000000)");
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SpecPowerPreservesScalarFirstDynamicMultiplyAndSharesStageUniformTree(bool half)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var parameter = ScalarParameter(package, material, "AuthoredSpecPower", 2f);
        var specPower = SpecPower(package, material, parameter);
        material.WriteProperty(Input("Opacity", specPower));
        material.WriteProperty(Input("WorldPositionOffset", specPower));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(2, result.UniformPixelScalarExpressions.Count);
        var pixelMultiply = (MaterialUniformExpressionFoldedMath)result.UniformPixelScalarExpressions.Last();
        var vertexMultiply = (MaterialUniformExpressionFoldedMath)result.UniformVertexScalarExpressions.Last();
        Assert.AreSame(pixelMultiply, vertexMultiply);
        Assert.AreEqual(MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Mul, pixelMultiply.Op);
        Assert.AreEqual("AuthoredSpecPower", ((MaterialUniformExpressionScalarParameter)pixelMultiply.A).ParameterName.Name);
        Assert.AreEqual(100f, ((MaterialUniformExpressionConstant)pixelMultiply.B).Value.R);
        var value = LinearColor.Black;
        pixelMultiply.GetNumberValue(new UniformExpressionRenderContext(new(), new(), 0, 0, null), ref value);
        Assert.AreEqual(200f, value.R);
        StringAssert.Contains(result.PropertyCode["Opacity"], "UniformPixelScalars_0");
        StringAssert.Contains(result.PropertyCode["WorldPositionOffset"], "UniformVertexScalars_0");
    }

    [TestMethod]
    [DataRow(1, "r")]
    [DataRow(2, "rg")]
    [DataRow(3, "rgb")]
    [DataRow(4, "rgba")]
    public void SpecPowerAppliesInputMaskBeforeGenericMultiply(int width, string channels)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var parameter = VectorParameter(package, material, "SpecInput");
        var specPower = SpecPower(package, material, parameter, mask: width);
        material.WriteProperty(Input("EmissiveColor", specPower));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], "UniformPixelVector_0." + channels);
        Assert.AreEqual(1, result.UniformPixelVectorExpressions.Count);
        Assert.IsInstanceOfType<MaterialUniformExpressionVectorParameter>(result.UniformPixelVectorExpressions[0]);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], "100.00000000");
    }

    [TestMethod]
    public void SpecPowerRejectsMissingInputBeforeVisitingAnythingElse()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var time = package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false);
        var specPower = package.CreateExport("SpecPower", "BioMaterialExpressionSpecPower", material, indexed: false);
        specPower.WriteProperty(Input("Input", time));
        specPower.WriteProperty(new StructProperty("ExpressionInput", new PropertyCollection { new ObjectProperty(0, "Expression") }, "Input"));
        material.WriteProperty(Input("Opacity", specPower));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());

        Assert.IsFalse(result.Success);
        Assert.IsNull(result.MaterialHlsl);
        CollectionAssert.AreEqual(new[] { "Missing SpecPower Input" }, result.Errors.ToArray());
        Assert.IsFalse(result.UsesTime);
    }

    [TestMethod]
    public void SpecPowerCycleFailsClosed()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var specPower = package.CreateExport("SpecPower", "BioMaterialExpressionSpecPower", material, indexed: false);
        specPower.WriteProperty(Input("Input", specPower));
        material.WriteProperty(Input("Opacity", specPower));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());

        Assert.IsFalse(result.Success);
        Assert.IsNull(result.MaterialHlsl);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("cycle", System.StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void SpecPowerPropagatesVisitedInputStageRestrictions()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var texCoord = package.CreateExport("TexCoord", "MaterialExpressionTextureCoordinate", material, indexed: false);
        var specPower = SpecPower(package, material, texCoord);
        material.WriteProperty(Input("WorldPositionOffset", specPower));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());

        Assert.IsFalse(result.Success);
        Assert.IsNull(result.MaterialHlsl);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("Invalid node used in vertex shader input!", System.StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SpecPowerPropagatesDynamicInputUsage(bool half)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var time = package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false);
        var specPower = SpecPower(package, material, time);
        material.WriteProperty(Input("Opacity", specPower));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.IsTrue(result.UsesTime);
        Assert.AreEqual(2, result.UniformPixelScalarExpressions.Count);
        Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
        Assert.IsInstanceOfType<MaterialUniformExpressionTime>(result.UniformPixelScalarExpressions[0]);
        var multiply = (MaterialUniformExpressionFoldedMath)result.UniformPixelScalarExpressions[1];
        Assert.AreSame(result.UniformPixelScalarExpressions[0], multiply.A);
        Assert.AreEqual(MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Mul, multiply.Op);
        Assert.AreEqual(100f, ((MaterialUniformExpressionConstant)multiply.B).Value.R);
        StringAssert.Contains(result.PropertyCode["Opacity"], "UniformPixelScalars_0.y");
    }

    private static IMEPackage Package() => MEPackageHandler.CreateMemoryEmptyPackage("LE3BioSimpleExpression.pcc", MEGame.LE3);
    private static ExportEntry Material(IMEPackage package) => package.CreateExport("Material", "Material", indexed: false);

    private static ExportEntry SpecPower(IMEPackage package, ExportEntry material, ExportEntry input, int mask = 0)
    {
        var node = package.CreateExport("SpecPower", "BioMaterialExpressionSpecPower", material, indexed: false);
        var connection = Input("Input", input);
        if (mask != 0)
        {
            connection.Properties.Add(new IntProperty(1, "Mask"));
            for (int i = 0; i < 4; i++) connection.Properties.Add(new IntProperty(i < mask ? 1 : 0, "Mask" + "RGBA"[i]));
        }
        node.WriteProperty(connection);
        return node;
    }

    private static ExportEntry Constant(IMEPackage package, ExportEntry material, int width, float value)
    {
        string className = width == 1 ? "MaterialExpressionConstant" : $"MaterialExpressionConstant{width}Vector";
        var node = package.CreateExport("Constant", className, material, indexed: false);
        node.WriteProperty(new FloatProperty(value, "R"));
        if (width > 1) node.WriteProperty(new FloatProperty(value, "G"));
        if (width > 2) node.WriteProperty(new FloatProperty(value, "B"));
        if (width > 3) node.WriteProperty(new FloatProperty(value, "A"));
        return node;
    }

    private static ExportEntry ScalarParameter(IMEPackage package, ExportEntry material, string name, float value)
    {
        var node = package.CreateExport(name, "MaterialExpressionScalarParameter", material, indexed: false);
        node.WriteProperty(new NameProperty(name, "ParameterName"));
        node.WriteProperty(new FloatProperty(value, "DefaultValue"));
        return node;
    }

    private static ExportEntry VectorParameter(IMEPackage package, ExportEntry material, string name)
    {
        var node = package.CreateExport(name, "MaterialExpressionVectorParameter", material, indexed: false);
        node.WriteProperty(new NameProperty(name, "ParameterName"));
        node.WriteProperty(new StructProperty("LinearColor", new PropertyCollection
        {
            new FloatProperty(1f, "R"), new FloatProperty(2f, "G"), new FloatProperty(3f, "B"), new FloatProperty(4f, "A")
        }, "DefaultValue", isImmutable: true));
        return node;
    }

    private static StructProperty Input(string name, ExportEntry expression) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression.UIndex, "Expression") }, name);
    private static string Template() => "/*\n%u\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n#line %u\n*/";
}
