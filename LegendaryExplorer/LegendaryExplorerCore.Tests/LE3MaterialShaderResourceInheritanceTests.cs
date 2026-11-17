using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3MaterialShaderResourceInheritanceTests
{
    [TestMethod]
    [DataRow("DiffuseColor", false)]
    [DataRow("DiffuseColor", true)]
    [DataRow("SpecularColor", false)]
    [DataRow("SpecularColor", true)]
    public void InheritedColorConstantTakesPrecedenceOverTheNativeDisconnectedFallback(string name, bool useConstant)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3InheritedColorInput.pcc", MEGame.LE3);
        var archetype = package.CreateExport("Archetype", "Material", indexed: false);
        var input = ColorInput(name, 255, useConstant); archetype.WriteProperty(input);
        var material = package.CreateExport("Material", "Material", indexed: false); material.Archetype = archetype;
        var compiler = new LE3MaterialShaderCompiler(material);
        var result = compiler.TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        // A distinct white inherited constant proves precedence over the native black fallback.
        string expected = useConstant ? "float3(1.00000000,1.00000000,1.00000000)" : "float3(0.00000000,0.00000000,0.00000000)";
        StringAssert.Contains(result.PropertyCode[name], expected);
        Assert.AreEqual(0, result.Graph.ConnectedInputs.Count); Assert.AreEqual(0, result.Graph.ReachableExpressions.Count);
        Assert.IsNull(material.GetProperties().GetProp<StructProperty>(name), "Translation must not write inherited input properties into the material.");
        Assert.AreEqual(useConstant, archetype.GetProperties().GetProp<StructProperty>(name).GetProp<BoolProperty>("UseConstant").Value);
        Assert.AreEqual(0, compiler.TranslateMaterialHlsl(Template()).PropertyCode.Count, "Legacy authored-root mode does not acquire resource defaults.");
    }

    [TestMethod]
    [DataRow("DiffuseColor", false)]
    [DataRow("DiffuseColor", true)]
    [DataRow("SpecularColor", false)]
    [DataRow("SpecularColor", true)]
    public void AuthoredInputOverridesTheWholeInheritedInputWithoutBorrowingItsConstant(string name, bool useConstant)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3AuthoredColorOverrides.pcc", MEGame.LE3);
        var archetype = package.CreateExport("Archetype", "Material", indexed: false); archetype.WriteProperty(ColorInput(name, 64, true));
        var material = package.CreateExport("Material", "Material", indexed: false); material.Archetype = archetype;
        material.WriteProperty(ColorInput(name, 255, useConstant));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode[name], useConstant ? "float3(1.00000000,1.00000000,1.00000000)" : "float3(0.00000000,0.00000000,0.00000000)");
        Assert.IsFalse(result.PropertyCode[name].Contains("0.04777575", StringComparison.Ordinal), "The inherited constant must not be borrowed.");
    }

    [TestMethod]
    [DataRow("Opacity")]
    [DataRow("SpecularPower")]
    [DataRow("Normal")]
    [DataRow("Distortion")]
    public void ResourceInputKindsUseInheritedLiteralPayloads(string name)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3InheritedTypedInputs.pcc", MEGame.LE3);
        var archetype = package.CreateExport("Archetype", "Material", indexed: false);
        var props = new PropertyCollection { new ObjectProperty(0, "Expression"), new BoolProperty(true, "UseConstant") };
        if (name == "Normal") props.Add(new StructProperty("Vector", new PropertyCollection
            { new FloatProperty(.25f, "X"), new FloatProperty(.5f, "Y"), new FloatProperty(.75f, "Z") }, "Constant", isImmutable: true));
        else if (name == "Distortion") { props.Add(new FloatProperty(.25f, "ConstantX")); props.Add(new FloatProperty(.5f, "ConstantY")); }
        else props.Add(new FloatProperty(.25f, "Constant"));
        archetype.WriteProperty(new StructProperty("ExpressionInput", props, name));
        var material = package.CreateExport("Material", "Material", indexed: false); material.Archetype = archetype;
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual("\treturn " + (name == "Normal" ? "float3(0.25000000,0.50000000,0.75000000)" :
            name == "Distortion" ? "float2(0.25000000,0.50000000)" : "float(0.25000000)"), result.PropertyCode[name]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InheritedExpressionRootsRequireResolutionUnlessUseConstantSuppressesThem(bool useConstant)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3InheritedGraphBoundary.pcc", MEGame.LE3);
        var archetype = package.CreateExport("Archetype", "Material", indexed: false);
        var node = package.CreateExport("InheritedNode", "MaterialExpressionScalarParameter", archetype, indexed: false);
        node.WriteProperty(new NameProperty("InheritedParameter", "ParameterName"));
        archetype.WriteProperty(new StructProperty("ExpressionInput", new PropertyCollection
            { new ObjectProperty(node.UIndex, "Expression"), new BoolProperty(useConstant, "UseConstant"), new FloatProperty(.25f, "Constant") }, "Opacity"));
        var material = package.CreateExport("Material", "Material", indexed: false); material.Archetype = archetype;
        var compiler = new LE3MaterialShaderCompiler(material);
        var analysis = compiler.AnalyzeMaterialResourceGraph();
        Assert.AreEqual(useConstant, analysis.IsValid); Assert.AreEqual(0, analysis.ReachableExpressions.Count);
        var result = compiler.TranslateMaterialResourceHlsl(Template());
        Assert.AreEqual(useConstant, result.Success);
        if (useConstant) Assert.AreEqual("\treturn float(0.25000000)", result.PropertyCode["Opacity"]);
        else
        {
            Assert.IsNull(result.MaterialHlsl);
            StringAssert.Contains(string.Join("; ", result.Errors), "inherited expression reference requiring graph/package resolution");
        }
    }

    [TestMethod]
    public void InheritedExpressionInventoryIsNotReinterpretedAsAuthoredPackageReferences()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3InheritedExpressionArray.pcc", MEGame.LE3);
        var archetype = package.CreateExport("Archetype", "Material", indexed: false);
        archetype.WriteProperty(new ArrayProperty<ObjectProperty>([new ObjectProperty(999999)], "Expressions"));
        archetype.WriteProperty(ColorInput("DiffuseColor", 0, true));
        var material = package.CreateExport("Material", "Material", indexed: false); material.Archetype = archetype;
        var compiler = new LE3MaterialShaderCompiler(material);
        var analysis = compiler.AnalyzeMaterialResourceGraph();
        Assert.IsTrue(analysis.IsValid, string.Join("; ", analysis.Errors));
        Assert.AreEqual(0, analysis.UnreachableExpressions.Count);
        Assert.IsTrue(compiler.TranslateMaterialResourceHlsl(Template()).Success);
    }

    [TestMethod]
    public void InheritedTwoSidedAuxiliaryColorAndMaskParticipateInTheResourceDispatch()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3InheritedTwoSidedInput.pcc", MEGame.LE3);
        var archetype = package.CreateExport("Archetype", "Material", indexed: false);
        archetype.WriteProperty(ColorInput("TwoSidedLightingColor", 0, true));
        archetype.WriteProperty(new StructProperty("ExpressionInput", new PropertyCollection
            { new ObjectProperty(0, "Expression"), new BoolProperty(true, "UseConstant"), new FloatProperty(.25f, "Constant") }, "TwoSidedLightingMask"));
        var material = package.CreateExport("Material", "Material", indexed: false); material.Archetype = archetype;
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual("\treturn (float(0.25000000) * float3(0.00000000,0.00000000,0.00000000))", result.PropertyCode["TwoSidedLightingMask"]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InheritedWorldPositionOffsetExpressionRootsStillRequireGraphResolutionButConstantsDoNot(bool useConstant)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3InheritedWPOBoundary.pcc", MEGame.LE3);
        var archetype = package.CreateExport("Archetype", "Material", indexed: false);
        var node = package.CreateExport("Offset", "MaterialExpressionConstant", archetype, indexed: false);
        var input = new PropertyCollection
            { new ObjectProperty(node.UIndex, "Expression"), new BoolProperty(useConstant, "UseConstant") };
        if (useConstant)
            input.Add(new StructProperty("Vector", new PropertyCollection
                { new FloatProperty(1, "X"), new FloatProperty(2, "Y"), new FloatProperty(3, "Z") }, "Constant", isImmutable: true));
        archetype.WriteProperty(new StructProperty("ExpressionInput", input, "WorldPositionOffset"));
        var material = package.CreateExport("Material", "Material", indexed: false); material.Archetype = archetype;
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        if (useConstant)
        {
            // UseConstant never consults the inherited expression, so no graph/package resolution is required.
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual("	return float3(1.00000000,2.00000000,3.00000000)", result.PropertyCode["WorldPositionOffset"]);
        }
        else
        {
            // An inherited expression reference is still rejected rather than silently compiled.
            Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
            StringAssert.Contains(string.Join("; ", result.Errors), "inherited expression reference requiring graph/package resolution");
            Assert.IsFalse(string.Join("; ", result.Errors).Contains("WorldPositionOffset is not verified"));
        }
    }

    private static StructProperty ColorInput(string name, byte value, bool useConstant) => new("ExpressionInput", new PropertyCollection
    {
        new ObjectProperty(0, "Expression"), new BoolProperty(useConstant, "UseConstant"),
        new StructProperty("Color", new PropertyCollection
            { new ByteProperty(value, "R"), new ByteProperty(value, "G"), new ByteProperty(value, "B"), new ByteProperty(255, "A") }, "Constant", isImmutable: true)
    }, name);
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
}
