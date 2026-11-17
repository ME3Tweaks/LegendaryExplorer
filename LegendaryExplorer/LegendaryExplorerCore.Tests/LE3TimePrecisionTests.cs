using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3TimePrecisionTests
{
    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, false)]
    [DataRow(false, true, true)]
    [DataRow(true, false, false)]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    [DataRow(true, true, true)]
    public void SelectedClockForcesTextureCoordinateStorageWithoutChangingNumericMode(bool realTime, bool vertex, bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ClockPrecision.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var clock = package.CreateExport("Clock", "MaterialExpressionTime", material, indexed: false);
        clock.WriteProperty(new BoolProperty(realTime, "bIgnorePause"));
        material.WriteProperty(Input(vertex ? "WorldPositionOffset" : "Opacity", clock));
        var compiler = new LE3MaterialShaderCompiler(material);
        var result = compiler.TranslateMaterialHlsl(Template(), useHalfTypes: half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.IsTrue(result.ForcesFloatTexCoords); Assert.IsTrue(result.UsesTime);
        Assert.AreEqual(half, result.UsesHalfTypes);
        StringAssert.Contains(result.MaterialHlsl, "float2 TexCoords[NUM_MATERIAL_TEXCOORDS];");
        Assert.AreEqual(vertex ? 1 : 0, result.UniformVertexScalarExpressions.Count);
        Assert.AreEqual(vertex ? 0 : 1, result.UniformPixelScalarExpressions.Count);
        Assert.IsNull(material.GetProperties().GetProp<BoolProperty>("bForceFullPrecision"));
        if (!vertex)
        {
            var resource = compiler.TranslateMaterialResourceHlsl(Template());
            Assert.IsTrue(resource.Success, string.Join("; ", resource.Errors));
            Assert.IsTrue(resource.ForcesFloatTexCoords); Assert.IsFalse(resource.UsesHalfTypes);
            StringAssert.Contains(resource.MaterialHlsl, "float2 TexCoords[NUM_MATERIAL_TEXCOORDS];");
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void InactiveAndUnreachableClockDoesNotForceStorageInANewTranslation(bool selected, bool resourceMode)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ClockSelectedPrecision.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var clock = package.CreateExport("Clock", "MaterialExpressionTime", material, indexed: false);
        var literal = package.CreateExport("Literal", "MaterialExpressionConstant", material, indexed: false);
        var branch = package.CreateExport("Switch", "MaterialExpressionStaticSwitchParameter", material, indexed: false);
        branch.WriteProperty(Input("A", clock)); branch.WriteProperty(Input("B", literal));
        branch.WriteProperty(new BoolProperty(selected, "DefaultValue"));
        material.WriteProperty(Input("Opacity", branch));
        material.WriteProperty(new ArrayProperty<ObjectProperty>([new(clock.UIndex), new(literal.UIndex), new(branch.UIndex)], "Expressions"));
        var compiler = new LE3MaterialShaderCompiler(material);
        var first = Translate(); Assert.IsTrue(first.Success, string.Join("; ", first.Errors));
        Assert.AreEqual(selected, first.ForcesFloatTexCoords); Assert.AreEqual(selected, first.UsesTime);
        branch.WriteProperty(new BoolProperty(false, "DefaultValue"));
        var second = Translate(); Assert.IsTrue(second.Success, string.Join("; ", second.Errors));
        Assert.IsFalse(second.ForcesFloatTexCoords); Assert.IsFalse(second.UsesTime);
        StringAssert.Contains(second.MaterialHlsl, "half2 TexCoords[NUM_MATERIAL_TEXCOORDS];");

        LE3MaterialShaderCompiler.TranslationResult Translate() => resourceMode
            ? compiler.TranslateMaterialResourceHlsl(Template()) : compiler.TranslateMaterialHlsl(Template());
    }

    private static StructProperty Input(string name, ExportEntry expression) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression.UIndex, "Expression") }, name);
    private static string Template() => "half2 TexCoords[NUM_MATERIAL_TEXCOORDS];\n%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
}
