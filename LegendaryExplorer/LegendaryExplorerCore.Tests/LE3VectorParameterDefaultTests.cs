using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3VectorParameterDefaultTests
{
    [TestMethod]
    public void MissingDefaultUsesOpaqueBlackWhileAuthoredColorsArePreserved()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3VectorDefaultValues.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var missing = VectorParameter(package, material, "Missing", "MissingDefault");
        var zeroAlpha = VectorParameter(package, material, "ZeroAlpha", "ZeroAlpha", new LinearColor(0.2f, 0.3f, 0.4f, 0f));
        var authored = VectorParameter(package, material, "Authored", "Authored", new LinearColor(0.2f, 0.3f, 0.4f, 0.65f));
        material.WriteProperty(MaskedInput("EmissiveColor", missing.UIndex, r: true, g: true, b: true));
        material.WriteProperty(MaskedInput("DiffuseColor", zeroAlpha.UIndex, r: true, g: true, b: true));
        material.WriteProperty(MaskedInput("SpecularColor", authored.UIndex, r: true, g: true, b: true));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        var uniforms = result.UniformPixelVectorExpressions.Cast<MaterialUniformExpressionVectorParameter>().ToArray();
        AssertColor(new(0f, 0f, 0f, 1f), uniforms.Single(p => p.ParameterName == "MissingDefault").DefaultValue);
        AssertColor(new(0.2f, 0.3f, 0.4f, 0f), uniforms.Single(p => p.ParameterName == "ZeroAlpha").DefaultValue);
        AssertColor(new(0.2f, 0.3f, 0.4f, 0.65f), uniforms.Single(p => p.ParameterName == "Authored").DefaultValue);

        var runtimeContext = new UniformExpressionRenderContext(new(), new() { ["MissingDefault"] = new(0f, 0f, 0f, 0f) }, 0f, 0f, null);
        LinearColor runtimeValue = default;
        uniforms.Single(p => p.ParameterName == "MissingDefault").GetNumberValue(runtimeContext, ref runtimeValue);
        AssertColor(new(0f, 0f, 0f, 0f), runtimeValue);
    }

    [TestMethod]
    public void AlphaMaskUsesPackedVectorAlphaLaneAndDefaultAlphaOne()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3VectorDefaultAlphaMask.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var parameter = VectorParameter(package, material, "Vector", "AlphaDefault");
        material.WriteProperty(MaskedInput("Opacity", parameter.UIndex, a: true));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(1, result.UniformPixelVectorExpressions.Count);
        var uniform = (MaterialUniformExpressionVectorParameter)result.UniformPixelVectorExpressions[0];
        Assert.AreEqual("AlphaDefault", uniform.ParameterName.Name);
        AssertColor(new(0f, 0f, 0f, 1f), uniform.DefaultValue);
        StringAssert.Contains(result.PropertyCode["Opacity"], "UniformPixelVector_0.a");
    }

    [TestMethod]
    public void SelectionColorBlackAndMeshEmitterWhiteKeepTheirExistingAlphaDefaults()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3VectorNamedDefaults.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var meshEmitter = package.CreateExport("EmitterColor", "MaterialExpressionMeshEmitterVertexColor", material, indexed: false);
        material.WriteProperty(Input("EmissiveColor", meshEmitter.UIndex));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(MinimalTemplate());

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        var uniforms = result.UniformPixelVectorExpressions.Cast<MaterialUniformExpressionVectorParameter>().ToArray();
        AssertColor(LinearColor.Black, uniforms.Single(p => p.ParameterName == "SelectionColor").DefaultValue);
        AssertColor(LinearColor.White, uniforms.Single(p => p.ParameterName == "MeshEmitterVertexColor").DefaultValue);
        Assert.AreEqual(1f, uniforms.Single(p => p.ParameterName == "SelectionColor").DefaultValue.A);
        Assert.AreEqual(1f, uniforms.Single(p => p.ParameterName == "MeshEmitterVertexColor").DefaultValue.A);
    }

    [TestMethod]
    public void MissingAndExplicitOneDefaultsDeduplicateButExplicitZeroDoesNot()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3VectorDefaultDedup.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var missing = VectorParameter(package, material, "Missing", "Shared");
        var explicitOne = VectorParameter(package, material, "ExplicitOne", "Shared", new LinearColor(0f, 0f, 0f, 1f));
        var explicitZero = VectorParameter(package, material, "ExplicitZero", "Shared", new LinearColor(0f, 0f, 0f, 0f));
        material.WriteProperty(MaskedInput("EmissiveColor", missing.UIndex, r: true, g: true, b: true));
        material.WriteProperty(MaskedInput("DiffuseColor", explicitOne.UIndex, r: true, g: true, b: true));
        material.WriteProperty(MaskedInput("SpecularColor", explicitZero.UIndex, r: true, g: true, b: true));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        var uniforms = result.UniformPixelVectorExpressions.Cast<MaterialUniformExpressionVectorParameter>().ToArray();
        Assert.AreEqual(2, uniforms.Length);
        Assert.AreEqual(1, uniforms.Count(p => p.ParameterName == "Shared" && p.DefaultValue.A == 1f));
        Assert.AreEqual(1, uniforms.Count(p => p.ParameterName == "Shared" && p.DefaultValue.A == 0f));
    }

    [TestMethod]
    public void MiniEngineVectorParameterCdoPinsMissingDefaultToOpaqueBlack()
    {
        GlobalTest.Init();
        string enginePath = Path.Combine(GlobalTest.GetTestMiniGamePath(MEGame.LE3), "BioGame", "CookedPCConsole", "Engine.pcc");
        using var package = MEPackageHandler.OpenMEPackage(enginePath);
        var header = package.Exports.Single(e => e.ObjectName.Name == "Default__MaterialExpressionVectorParameter");
        var cdo = header;
        Assert.IsTrue(cdo.IsDefaultObject);
        var defaultValue = cdo.GetPropertiesIncludingInherited().GetProp<StructProperty>("DefaultValue");
        Assert.IsNotNull(defaultValue);
        Assert.IsTrue(defaultValue.IsImmutable);
        CollectionAssert.AreEqual(new[] { "R", "G", "B", "A" }, defaultValue.Properties.Select(p => p.Name.Name).ToArray());
        AssertColor(new(0f, 0f, 0f, 1f), new(
            defaultValue.GetProp<FloatProperty>("R").Value,
            defaultValue.GetProp<FloatProperty>("G").Value,
            defaultValue.GetProp<FloatProperty>("B").Value,
            defaultValue.GetProp<FloatProperty>("A").Value));
    }

    private static ExportEntry VectorParameter(IMEPackage package, ExportEntry owner, string objectName, string parameterName, LinearColor? value = null)
    {
        var expression = package.CreateExport(objectName, "MaterialExpressionVectorParameter", owner, indexed: false);
        expression.WriteProperty(new NameProperty(parameterName, "ParameterName"));
        if (value is LinearColor color)
        {
            expression.WriteProperty(new StructProperty("LinearColor", new PropertyCollection
            {
                new FloatProperty(color.R, "R"),
                new FloatProperty(color.G, "G"),
                new FloatProperty(color.B, "B"),
                new FloatProperty(color.A, "A")
            }, "DefaultValue", isImmutable: true));
        }
        return expression;
    }

    private static StructProperty Input(string name, int expression) => new("ExpressionInput", new PropertyCollection
    {
        new ObjectProperty(expression, "Expression")
    }, name);

    private static StructProperty MaskedInput(string name, int expression, bool r = false, bool g = false, bool b = false, bool a = false) =>
        new("ExpressionInput", new PropertyCollection
        {
            new ObjectProperty(expression, "Expression"),
            new BoolProperty(r, "MaskR"),
            new BoolProperty(g, "MaskG"),
            new BoolProperty(b, "MaskB"),
            new BoolProperty(a, "MaskA"),
            new BoolProperty(r || g || b || a, "Mask")
        }, name);

    private static void AssertColor(LinearColor expected, LinearColor actual)
    {
        Assert.AreEqual(expected.R, actual.R);
        Assert.AreEqual(expected.G, actual.G);
        Assert.AreEqual(expected.B, actual.B);
        Assert.AreEqual(expected.A, actual.A);
    }

    private static string MinimalTemplate() =>
        "%u\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n#line %u\n";
}
