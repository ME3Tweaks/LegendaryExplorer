using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3ResourceColorDefaultTests
{
    [TestMethod]
    [DataRow("DiffuseColor", 0, "Material")]
    [DataRow("DiffuseColor", 1, "Material")]
    [DataRow("DiffuseColor", 2, "Material")]
    [DataRow("SpecularColor", 0, "Material")]
    [DataRow("SpecularColor", 1, "Material")]
    [DataRow("SpecularColor", 2, "Material")]
    [DataRow("DiffuseColor", 0, "DecalMaterial")]
    [DataRow("DiffuseColor", 1, "DecalMaterial")]
    [DataRow("DiffuseColor", 2, "DecalMaterial")]
    [DataRow("SpecularColor", 0, "DecalMaterial")]
    [DataRow("SpecularColor", 1, "DecalMaterial")]
    [DataRow("SpecularColor", 2, "DecalMaterial")]
    public void DisconnectedDiffuseAndSpecularColorsUseNativeResourceClassFallback(string name, int inputKind, string resourceClass)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ResourceColorFallback.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", resourceClass, indexed: false);
        if (inputKind != 0)
        {
            var input = Input(name, 0);
            input.Properties.Add(Color(255, 17, 64));
            if (inputKind != 1) input.Properties.Add(new BoolProperty(false, "UseConstant"));
            material.WriteProperty(input);
        }

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode[name], Fallback(resourceClass));
    }

    [TestMethod]
    [DataRow("DiffuseColor", false, false)]
    [DataRow("DiffuseColor", false, true)]
    [DataRow("DiffuseColor", true, false)]
    [DataRow("DiffuseColor", true, true)]
    [DataRow("SpecularColor", false, false)]
    [DataRow("SpecularColor", false, true)]
    [DataRow("SpecularColor", true, false)]
    [DataRow("SpecularColor", true, true)]
    public void UseConstantFalseIgnoresStoredColorForBothFlagTypes(string name, bool integerFlag, bool missingExpression)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ResourceColorFalseConstant.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var input = Input(name, 0);
        if (missingExpression) input.Properties.RemoveNamedProperty("Expression");
        input.Properties.Add(integerFlag ? new IntProperty(0, "UseConstant") : new BoolProperty(false, "UseConstant"));
        input.Properties.Add(Color(255, 17, 64));
        material.WriteProperty(input);

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode[name], Fallback("Material"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UseConstantTrueOverridesExpressionAndUsesStoredColor(bool integerFlag)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ResourceColorTrueConstant.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var input = Input("SpecularColor", 123456);
        input.Properties.Add(integerFlag ? new IntProperty(3, "UseConstant") : new BoolProperty(true, "UseConstant"));
        input.Properties.Add(Color(64));
        material.WriteProperty(input);

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual("\treturn float3(0.04777575,0.00000000,0.00000000)", result.PropertyCode["SpecularColor"]);
        Assert.AreEqual(0, result.Graph.ReachableExpressions.Count);
    }

    [TestMethod]
    public void OtherResourceColorsDefaultToBlackAndTwoSidedColorDefaultsToWhite()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ResourceOtherColorFallback.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode["CustomLightingDiffuse"], "float3(0.00000000,0.00000000,0.00000000)");
        StringAssert.Contains(result.PropertyCode["TwoSidedLightingMask"], "float3(1.00000000,1.00000000,1.00000000)");
    }

    [TestMethod]
    public void InheritedUseConstantFalseUsesFallbackInsteadOfStoredColor()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3InheritedResourceColorFallback.pcc", MEGame.LE3);
        var archetype = package.CreateExport("Archetype", "Material", indexed: false);
        var input = Input("DiffuseColor", 0);
        input.Properties.Add(new BoolProperty(false, "UseConstant"));
        input.Properties.Add(Color(255, 17, 64));
        archetype.WriteProperty(input);
        var material = package.CreateExport("Material", "Material", indexed: false);
        material.Archetype = archetype;

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode["DiffuseColor"], Fallback("Material"));
        Assert.IsNull(material.GetProperties().GetProp<StructProperty>("DiffuseColor"));
    }

    [TestMethod]
    public void FailedExpressionKeepsDiagnosticAndCompilesTheColorFallback()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ResourceColorFailedExpression.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var failedExpression = package.CreateExport("UnsupportedColor", "MaterialExpressionWorldNormal", material, indexed: false);
        material.WriteProperty(Input("SpecularColor", failedExpression.UIndex));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());

        Assert.IsFalse(result.Success);
        Assert.IsNull(result.MaterialHlsl);
        Assert.IsTrue(result.Errors.Count > 0);
        StringAssert.Contains(result.PropertyCode["SpecularColor"], Fallback("Material"));
    }

    [TestMethod]
    public void LegacyTranslationDoesNotAddResourceColorDefaults()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3LegacyColorRoots.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var compiler = new LE3MaterialShaderCompiler(material);

        var result = compiler.TranslateMaterialHlsl(Template());

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.IsFalse(result.IncludesMaterialResourceInputs);
        Assert.AreEqual(0, result.PropertyCode.Count);
    }

    [TestMethod]
    [DataRow("Material", false, false)]
    [DataRow("Material", true, false)]
    [DataRow("DecalMaterial", false, false)]
    [DataRow("DecalMaterial", true, false)]
    [DataRow("Material", true, true)]
    [DataRow("DecalMaterial", false, true)]
    public void ResourceClassNotDecalUsageOrRequestedInstanceSelectsFallback(string resourceClass, bool usedWithDecals, bool requestedInstance)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ResourceClassFallback.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", resourceClass, indexed: false);
        material.WriteProperty(new BoolProperty(usedWithDecals, "bUsedWithDecals"));
        var instance = requestedInstance ? package.CreateExport("Instance", "MaterialInstanceConstant", indexed: false) : null;
        var result = new LE3MaterialShaderCompiler(new LE3MaterialShaderCompiler.ResolvedMaterialCompilationInput(
            material, requestedMaterialInstance: instance)).TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        foreach (string input in new[] { "DiffuseColor", "SpecularColor" })
            StringAssert.Contains(result.PropertyCode[input], Fallback(resourceClass));
        Assert.AreSame(instance ?? material, result.CompilationInput.RequestedMaterial);
    }

    private static string Fallback(string resourceClass) => resourceClass == "DecalMaterial"
        ? "float3(0.21951972,0.21951972,0.21951972)" : "float3(0.00000000,0.00000000,0.00000000)";

    private static StructProperty Input(string name, int expression) => new("ExpressionInput", new PropertyCollection
    {
        new ObjectProperty(expression, "Expression")
    }, name);

    private static StructProperty Color(byte red, byte? green = null, byte? blue = null)
    {
        // Immutable serialized colors have four positional BGRA bytes.
        var channels = new PropertyCollection
        {
            new ByteProperty(blue ?? 0, "B"), new ByteProperty(green ?? 0, "G"),
            new ByteProperty(red, "R"), new ByteProperty(0, "A")
        };
        return new StructProperty("Color", channels, "Constant", isImmutable: true);
    }

    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
}
