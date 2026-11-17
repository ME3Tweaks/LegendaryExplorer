using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3BioLightingExpressionTests
{
    [TestMethod]
    [DataRow("BioMaterialExpressionAnisoSpec", false)]
    [DataRow("BioMaterialExpressionAnisoSpec", true)]
    [DataRow("BioMaterialExpressionBlinnSpec", false)]
    [DataRow("BioMaterialExpressionBlinnSpec", true)]
    [DataRow("BioMaterialExpressionPhongSpec", false)]
    [DataRow("BioMaterialExpressionPhongSpec", true)]
    public void SpecularExpressionsEmitLocalsAtRequestedPrecision(string kind, bool half)
    {
        var (package, material, node) = SpecFixture(kind);
        using (package)
        {
            material.WriteProperty(Input("EmissiveColor", node));
            var result = Translate(material, half);
            Success(result);
            string code = result.PropertyCode["EmissiveColor"];
            StringAssert.Contains(code, half ? "half3 Local" : "float3 Local");
            Assert.AreEqual(kind == "BioMaterialExpressionPhongSpec" ? 1 : 2, Count(code, " = "));
            Assert.AreEqual(0, VectorUniforms(result));
            Assert.AreEqual(0u, result.UsingTransforms);
        }
    }

    [TestMethod]
    [DataRow("BioMaterialExpressionAnisoSpec", "Direction")]
    [DataRow("BioMaterialExpressionBlinnSpec", "Normal")]
    [DataRow("BioMaterialExpressionPhongSpec", "Normal")]
    public void SpecularDefaultsAndPhongFormulaAreNative(string kind, string directionName)
    {
        var (package, material, node) = SpecFixture(kind);
        using (package)
        {
            material.WriteProperty(Input("EmissiveColor", node));
            var result = Translate(material);
            Success(result);
            var code = result.PropertyCode["EmissiveColor"];
            StringAssert.Contains(code, "float3(0.00000000,0.00000000,1.00000000)");
            if (kind == "BioMaterialExpressionAnisoSpec") StringAssert.Contains(code, "sqrt( 1.0 - min(dot(");
            if (kind == "BioMaterialExpressionBlinnSpec") StringAssert.Contains(code, "pow( max( 0.0, min( 1.0, dot(");
            if (kind == "BioMaterialExpressionPhongSpec") StringAssert.Contains(code, "normalize( ( 2 * ");
            Assert.IsTrue(directionName == "Direction" || directionName == "Normal");
        }
    }

    [TestMethod]
    [DataRow("BioMaterialExpressionAnisoSpec", 1)] [DataRow("BioMaterialExpressionAnisoSpec", 3)]
    [DataRow("BioMaterialExpressionBlinnSpec", 1)] [DataRow("BioMaterialExpressionBlinnSpec", 3)]
    [DataRow("BioMaterialExpressionPhongSpec", 1)] [DataRow("BioMaterialExpressionPhongSpec", 3)]
    public void SpecularAcceptsScalarAndFloat3ForEachInput(string kind, int width)
    {
        var (package, material, node) = SpecFixture(kind);
        using (package)
        {
            var direction = Constant(package, material, width);
            var color = Constant(package, material, width);
            var power = Constant(package, material, width);
            SetInput(node, kind == "BioMaterialExpressionAnisoSpec" ? "Direction" : "Normal", direction);
            SetInput(node, "SpecColor", color); SetInput(node, "SpecPower", power);
            material.WriteProperty(Input("EmissiveColor", node));
            var result = Translate(material); Success(result);
            StringAssert.Contains(result.PropertyCode["EmissiveColor"], "float3(");
        }
    }

    [TestMethod]
    [DataRow("BioMaterialExpressionAnisoSpec", 2)] [DataRow("BioMaterialExpressionAnisoSpec", 4)]
    [DataRow("BioMaterialExpressionBlinnSpec", 2)] [DataRow("BioMaterialExpressionBlinnSpec", 4)]
    [DataRow("BioMaterialExpressionPhongSpec", 2)] [DataRow("BioMaterialExpressionPhongSpec", 4)]
    public void SpecularRejectsNonThreeComponentExplicitVectors(string kind, int width)
    {
        var (package, material, node) = SpecFixture(kind);
        using (package)
        {
            SetInput(node, "SpecColor", Constant(package, material, width));
            material.WriteProperty(Input("EmissiveColor", node));
            var result = Translate(material);
            Failure(result);
        }
    }

    [TestMethod]
    [DataRow("BioMaterialExpressionAnisoSpec", "Direction")]
    [DataRow("BioMaterialExpressionBlinnSpec", "Normal")]
    [DataRow("BioMaterialExpressionPhongSpec", "Normal")]
    public void SpecularVisitsLaterInputsBeforeAnEarlierInvalidInput(string kind, string directionName)
    {
        var (package, material, node) = SpecFixture(kind);
        using (package)
        {
            var bad = Constant(package, material, 2);
            var time = package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false);
            SetInput(node, directionName, bad); SetInput(node, "SpecPower", time);
            material.WriteProperty(Input("EmissiveColor", node));
            var result = Translate(material);
            Failure(result);
            Assert.IsTrue(result.UsesTime);
        }
    }

    [TestMethod]
    [DataRow("BioMaterialExpressionAnisoSpec", false)] [DataRow("BioMaterialExpressionBlinnSpec", true)]
    [DataRow("BioMaterialExpressionPhongSpec", false)]
    public void SpecularAllowsVertexStageUnlessAChildRejectsIt(string kind, bool withPixelOnlyChild)
    {
        var (package, material, node) = SpecFixture(kind);
        using (package)
        {
            if (withPixelOnlyChild) SetInput(node, "SpecPower", package.CreateExport("TexCoord", "MaterialExpressionTextureCoordinate", material, indexed: false));
            material.WriteProperty(Input("WorldPositionOffset", node));
            var result = Translate(material);
            if (withPixelOnlyChild) Failure(result);
            else { Success(result); Assert.AreEqual(0, VectorUniforms(result)); }
        }
    }

    [TestMethod]
    [DataRow("BioMaterialExpressionAnisoSpec")]
    [DataRow("BioMaterialExpressionBlinnSpec")]
    [DataRow("BioMaterialExpressionPhongSpec")]
    public void SpecularDynamicChildrenPackInAccessOrderAndReuseAcrossProperties(string kind)
    {
        var (package, material, node) = SpecFixture(kind);
        using (package)
        {
            var directionName = kind == "BioMaterialExpressionAnisoSpec" ? "Direction" : "Normal";
            var first = ScalarParameter(package, material, "DirectionParam");
            var second = ScalarParameter(package, material, "ColorParam");
            var third = ScalarParameter(package, material, "PowerParam");
            SetInput(node, directionName, first); SetInput(node, "SpecColor", second); SetInput(node, "SpecPower", third);
            material.WriteProperty(Input("EmissiveColor", node)); material.WriteProperty(Input("Opacity", node));
            var result = Translate(material); Success(result);
            var uniforms = result.UniformPixelScalarExpressions.OfType<MaterialUniformExpressionScalarParameter>().ToArray();
            CollectionAssert.AreEqual(new[] { "DirectionParam", "ColorParam", "PowerParam" }, uniforms.Select(u => u.ParameterName.Name).ToArray());
            Assert.AreEqual(0, VectorUniforms(result));
        }
    }

    [TestMethod]
    [DataRow(false, false, 1f, false)] [DataRow(true, true, 1f, false)]
    [DataRow(false, true, 2f, false)] [DataRow(true, false, 2f, false)]
    [DataRow(false, false, 1f, true)] [DataRow(true, true, 1f, true)]
    public void FresnelFormulaDefaultsPowerInvertsAndUsesRequestedPrecision(bool half, bool inverted, float power, bool suppliedNormal)
    {
        var (package, material, node) = FresnelFixture(power, inverted);
        using (package)
        {
            if (suppliedNormal) SetInput(node, "Normal", Constant(package, material, 3));
            material.WriteProperty(Input("Opacity", node));
            var result = Translate(material, half); Success(result);
            var code = result.PropertyCode["Opacity"];
            StringAssert.Contains(code, half ? "half Local0" : "float Local0");
            StringAssert.Contains(code, "max( 0.0, min( 1.0, dot(");
            if (inverted) StringAssert.Contains(code, "1.0 - max(");
            if (power != 1f) StringAssert.Contains(code, "pow("); else Assert.IsFalse(code.Contains("pow("));
            Assert.AreEqual(0, VectorUniforms(result)); Assert.AreEqual(0u, result.UsingTransforms);
        }
    }

    [TestMethod]
    [DataRow(0.9998f, true)] [DataRow(0.99995f, false)] [DataRow(0.99999f, false)]
    [DataRow(1.00001f, false)] [DataRow(1.00005f, false)] [DataRow(1.0002f, true)]
    [DataRow(float.NaN, false)] [DataRow(float.PositiveInfinity, true)] [DataRow(float.NegativeInfinity, true)]
    public void FresnelPowerUsesStrictFloatDifferenceThresholdAndInvariantLiteral(float power, bool emitsPower)
    {
        var (package, material, node) = FresnelFixture(power, false);
        using (package)
        {
            material.WriteProperty(Input("Opacity", node));
            var result = Translate(material); Success(result);
            var code = result.PropertyCode["Opacity"];
            Assert.AreEqual(emitsPower, code.Contains("pow("), code);
            if (emitsPower && float.IsFinite(power)) StringAssert.Contains(code, power.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    [TestMethod]
    [DataRow(1)] [DataRow(3)]
    public void FresnelAcceptsScalarAndFloat3Normals(int width)
    {
        var (package, material, node) = FresnelFixture(1f, false);
        using (package)
        {
            SetInput(node, "Normal", Constant(package, material, width)); material.WriteProperty(Input("Opacity", node));
            var result = Translate(material); Success(result); StringAssert.Contains(result.PropertyCode["Opacity"], "dot(");
        }
    }

    [TestMethod]
    [DataRow(2)] [DataRow(4)]
    public void FresnelRejectsTwoAndFourComponentNormal(int width)
    {
        var (package, material, node) = FresnelFixture(1f, false);
        using (package)
        {
            SetInput(node, "Normal", Constant(package, material, width)); material.WriteProperty(Input("Opacity", node));
            Failure(Translate(material));
        }
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void FresnelVisitsAndCoercesNormalBeforeVertexCameraVectorGuard(bool malformed)
    {
        var (package, material, node) = FresnelFixture(1f, false);
        using (package)
        {
            var normal = Constant(package, material, malformed ? 2 : 3);
            SetInput(node, "Normal", normal); material.WriteProperty(Input("WorldPositionOffset", node));
            var result = Translate(material);
            Failure(result);
            Assert.IsTrue(result.Errors.Any(error => error.Contains("vertex shader", StringComparison.Ordinal)));
        }
    }

    [TestMethod]
    public void FresnelMissingNormalDefaultsButMissingRequiredExpressionDoesNotMaskPixelGuard()
    {
        var (package, material, node) = FresnelFixture(1f, false);
        using (package)
        {
            material.WriteProperty(Input("Opacity", node));
            var pixel = Translate(material); Success(pixel); StringAssert.Contains(pixel.PropertyCode["Opacity"], "0.00000000,0.00000000,1.00000000");
            material.WriteProperty(Input("WorldPositionOffset", node));
            var vertex = Translate(material); Failure(vertex);
            Assert.IsTrue(vertex.Errors.Any(error => error.Contains("vertex shader", StringComparison.Ordinal)));
        }
    }

    [TestMethod]
    public void FresnelUsesShippingDefaultsWhenPropertiesAreAbsent()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        var node = package.CreateExport("Fresnel", "BioMaterialExpressionFresnel", material, indexed: false);
        material.WriteProperty(Input("Opacity", node));
        var result = Translate(material); Success(result);
        var code = result.PropertyCode["Opacity"];
        StringAssert.Contains(code, "float3(0.00000000,0.00000000,1.00000000)");
        Assert.IsFalse(code.Contains("pow("));
    }

    [TestMethod]
    [DataRow("BioMaterialExpressionAnisoSpec")]
    [DataRow("BioMaterialExpressionBlinnSpec")]
    [DataRow("BioMaterialExpressionPhongSpec")]
    public void SpecularCyclesAndUnsupportedChildrenFailClosed(string kind)
    {
        var (package, material, node) = SpecFixture(kind);
        using (package)
        {
            SetInput(node, "SpecPower", node); material.WriteProperty(Input("EmissiveColor", node));
            var result = Translate(material); Failure(result);
            Assert.IsTrue(result.Errors.Any(error => error.Contains("cycle", StringComparison.OrdinalIgnoreCase)));
        }
    }

    private static (IMEPackage Package, ExportEntry Material, ExportEntry Node) SpecFixture(string kind)
    {
        GlobalTest.Init(); var package = Package(); var material = Material(package);
        return (package, material, package.CreateExport("Lighting", kind, material, indexed: false));
    }

    private static (IMEPackage Package, ExportEntry Material, ExportEntry Node) FresnelFixture(float power, bool inverted)
    {
        GlobalTest.Init(); var package = Package(); var material = Material(package);
        var node = package.CreateExport("Fresnel", "BioMaterialExpressionFresnel", material, indexed: false);
        node.WriteProperty(new FloatProperty(power, "Power")); node.WriteProperty(new BoolProperty(inverted, "Inverted"));
        return (package, material, node);
    }

    private static LE3MaterialShaderCompiler.TranslationResult Translate(ExportEntry material, bool half = false) =>
        new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
    private static void Success(LE3MaterialShaderCompiler.TranslationResult result) => Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
    private static void Failure(LE3MaterialShaderCompiler.TranslationResult result) { Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl); }
    private static int VectorUniforms(LE3MaterialShaderCompiler.TranslationResult result) => result.UniformPixelVectorExpressions.Count + result.UniformVertexVectorExpressions.Count;
    private static int Count(string value, string needle) => value.Split(new[] { needle }, StringSplitOptions.None).Length - 1;
    private static IMEPackage Package() => MEPackageHandler.CreateMemoryEmptyPackage("LE3BioLightingExpression.pcc", MEGame.LE3);
    private static ExportEntry Material(IMEPackage package) => package.CreateExport("Material", "Material", indexed: false);

    private static ExportEntry Constant(IMEPackage package, ExportEntry material, int width)
    {
        var node = package.CreateExport($"Constant{width}_{package.Exports.Count}", width == 1 ? "MaterialExpressionConstant" : $"MaterialExpressionConstant{width}Vector", material, indexed: false);
        node.WriteProperty(new FloatProperty(0.25f, "R"));
        if (width > 1) node.WriteProperty(new FloatProperty(0.5f, "G"));
        if (width > 2) node.WriteProperty(new FloatProperty(0.75f, "B"));
        if (width > 3) node.WriteProperty(new FloatProperty(1f, "A"));
        return node;
    }

    private static ExportEntry ScalarParameter(IMEPackage package, ExportEntry material, string name)
    {
        var node = package.CreateExport(name, "MaterialExpressionScalarParameter", material, indexed: false);
        node.WriteProperty(new NameProperty(name, "ParameterName"));
        node.WriteProperty(new FloatProperty(1f, "DefaultValue"));
        return node;
    }

    private static void SetInput(ExportEntry node, string name, ExportEntry expression) => node.WriteProperty(Input(name, expression));
    private static StructProperty Input(string name, ExportEntry expression) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression.UIndex, "Expression") }, name);
    private static string Template() => "/*\n%u\n" + string.Concat(Enumerable.Repeat("%s\n", 18)) + "#line %u\n";
}
