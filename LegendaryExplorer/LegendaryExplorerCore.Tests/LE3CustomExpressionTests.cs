using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3CustomExpressionTests
{
    private const string Crlf = "\r\n";

    // ---- OutputType -----------------------------------------------------------------------

    [TestMethod]
    [DataRow("none", null, "float3", false)]
    [DataRow("enum", "CMOT_Float1", "float", true)]
    [DataRow("enum", "CMOT_Float2", "float2", false)]
    [DataRow("enum", "CMOT_Float3", "float3", false)]
    [DataRow("enum", "CMOT_Float4", "float4", false)]
    [DataRow("enum", "CMOT_MAX", "float", true)]
    [DataRow("byte", "0", "float", true)]
    [DataRow("byte", "1", "float2", false)]
    [DataRow("byte", "2", "float3", false)]
    [DataRow("byte", "3", "float4", false)]
    [DataRow("byte", "4", "float", true)]
    [DataRow("byte", "200", "float", true)]
    public void OutputTypeSelectsHelperTypeLocalTypeAndExplicitFloat1(string kind, string value, string type, bool explicitFloat1)
    {
        using var f = Fixture();
        var custom = Custom(f, code: "1", outputType: OutputType(kind, value));
        // Opacity is a scalar root: every width is accepted there (wider values truncate).
        f.Material.WriteProperty(Input("Opacity", custom));
        var result = Translate(f);
        Success(result);
        string helper = $"{type} CustomExpression0(){Crlf}{{{Crlf}return 1;{Crlf}}}{Crlf}{Crlf}{Crlf}";
        StringAssert.Contains(result.MaterialHlsl, helper);
        StringAssert.Contains(result.PropertyCode["Opacity"], $"\t{type} Local0 = CustomExpression0();");

        // The explicit Float1 native type is observable in vector arithmetic: a generic scalar broadcasts
        // against a float3, an explicit Float1 does not. Wider outputs are never Float1.
        using var g = Fixture();
        var vector = g.Package.CreateExport("Vector", "MaterialExpressionConstant3Vector", g.Material, indexed: false);
        var add = g.Package.CreateExport("Add", "MaterialExpressionAdd", g.Material, indexed: false);
        add.WriteProperty(Input("A", Custom(g, code: "1", outputType: OutputType(kind, value)))); add.WriteProperty(Input("B", vector));
        g.Material.WriteProperty(Input("EmissiveColor", add));
        var arithmetic = Translate(g);
        if (explicitFloat1) Assert.IsFalse(arithmetic.Success, "Explicit Float1 must not broadcast against float3.");
        else Assert.AreEqual(type == "float3", arithmetic.Success, string.Join("; ", arithmetic.Errors));

        using var control = Fixture();
        var controlAdd = control.Package.CreateExport("Add", "MaterialExpressionAdd", control.Material, indexed: false);
        var controlVector = control.Package.CreateExport("Vector", "MaterialExpressionConstant3Vector", control.Material, indexed: false);
        controlAdd.WriteProperty(Input("A", Constant(control, "Generic", 1f))); controlAdd.WriteProperty(Input("B", controlVector));
        control.Material.WriteProperty(Input("EmissiveColor", controlAdd));
        Success(Translate(control));
    }

    [TestMethod]
    public void UnknownEnumOutputTypeFailsClosed()
    {
        using var f = Fixture();
        var custom = Custom(f, code: "1", outputType: new EnumProperty("CMOT_Float5", "ECustomMaterialOutputType", MEGame.LE3, "OutputType"));
        f.Material.WriteProperty(Input("EmissiveColor", custom));
        var result = Translate(f);
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
        StringAssert.Contains(string.Join("; ", result.Errors), "unrecognized OutputType");
    }

    // ---- Description / Code defaults ------------------------------------------------------

    [TestMethod]
    [DataRow(null, "Custom")]
    [DataRow("", "")]
    [DataRow("Authored", "Authored")]
    public void DescriptionDefaultsToCustomButExplicitEmptyStaysEmpty(string description, string expected)
    {
        using var f = Fixture();
        var custom = Custom(f, code: "1", description: description, inputs: [CustomInput("A", null)]);
        f.Material.WriteProperty(Input("EmissiveColor", custom));
        var result = Translate(f);
        Failed(result, $"Custom material {expected} missing input 1 (A)");
        Assert.AreEqual($"Custom material {expected} missing input 1 (A)", result.Errors.Single());
    }

    [TestMethod]
    public void AbsentAndEmptyCodeBothBecomeReturnWithNothing()
    {
        foreach (string code in new[] { null, "" })
        {
            using var f = Fixture();
            f.Material.WriteProperty(Input("EmissiveColor", Custom(f, code: code)));
            var result = Translate(f); Success(result);
            StringAssert.Contains(result.MaterialHlsl, $"float3 CustomExpression0(){Crlf}{{{Crlf}return ;{Crlf}}}{Crlf}");
        }
    }

    // ---- Exact helper / call text ---------------------------------------------------------

    [TestMethod]
    public void HelperAndCallTextAreExactAndPlacedAfterUniformDeclarations()
    {
        using var f = Fixture();
        var scalar = Constant(f, "S", 0.25f);
        var vec2 = Constant2(f, "V2", 0.5f, 0.75f);
        var custom = Custom(f, code: "a + b.x", inputs: [CustomInput("a", scalar), CustomInput("b", vec2)]);
        f.Material.WriteProperty(Input("EmissiveColor", custom));
        var result = Translate(f); Success(result);
        string helper = $"float3 CustomExpression0(float a,float2 b){Crlf}{{{Crlf}return a + b.x;{Crlf}}}{Crlf}{Crlf}{Crlf}";
        // Template slot 1 is the declarations slot, directly after the leading numeric slot.
        Assert.IsTrue(result.MaterialHlsl.StartsWith("0\n" + helper + "\n", StringComparison.Ordinal), result.MaterialHlsl);
        string code = result.PropertyCode["EmissiveColor"];
        StringAssert.Contains(code, $"\tfloat3 Local0 = CustomExpression0(");
        AssertOrdered(code, "CustomExpression0(", "0.25000000", ",", "0.50000000", "0.75000000", ");");
        Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count + result.UniformPixelScalarExpressions.Count);
        Assert.IsTrue(code.EndsWith("\treturn Local0", StringComparison.Ordinal), code);
    }

    [TestMethod]
    public void BodyIsNotRewrittenAndReturnSubstringIsCaseSensitiveAndMatchesCommentsAndIdentifiers()
    {
        var cases = new (string Code, string Body)[]
        {
            ("float3 x = float3(1,0,0);\nreturn x;", "float3 x = float3(1,0,0);\nreturn x;"),
            ("// return something\nfloat3(1,0,0)", "// return something\nfloat3(1,0,0)"),
            ("returnValue", "returnValue"),
            ("/* no return here */ float3(1,1,1)", "/* no return here */ float3(1,1,1)"),
            ("Return float3(1,1,1)", "return Return float3(1,1,1);"),
            ("float3(1,1,1)", "return float3(1,1,1);"),
            ("a\r\nb", "return a\r\nb;"),
            ("a\nb", "return a\nb;"),
        };
        foreach (var (code, body) in cases)
        {
            using var f = Fixture();
            f.Material.WriteProperty(Input("EmissiveColor", Custom(f, code: code)));
            var result = Translate(f); Success(result);
            StringAssert.Contains(result.MaterialHlsl, $"float3 CustomExpression0(){Crlf}{{{Crlf}{body}{Crlf}}}{Crlf}", code);
        }
    }

    // ---- Argument types -------------------------------------------------------------------

    [TestMethod]
    [DataRow("MaterialExpressionConstant", "float")]
    [DataRow("MaterialExpressionConstant2Vector", "float2")]
    [DataRow("MaterialExpressionConstant3Vector", "float3")]
    [DataRow("MaterialExpressionConstant4Vector", "float4")]
    public void NumericArgumentsDeclareTheirNativeWidth(string className, string keyword)
    {
        using var f = Fixture();
        var node = f.Package.CreateExport("Arg", className, f.Material, indexed: false);
        f.Material.WriteProperty(Input("EmissiveColor", Custom(f, code: "1", inputs: [CustomInput("Value", node)])));
        var result = Translate(f); Success(result);
        StringAssert.Contains(result.MaterialHlsl, $"float3 CustomExpression0({keyword} Value){Crlf}");
    }

    [TestMethod]
    public void NestedCustomOutputsKeepTheirNativeWidthAsArguments()
    {
        using var f = Fixture();
        var f1 = Custom(f, "F1", "1", OutputType("enum", "CMOT_Float1"));
        var f2 = Custom(f, "F2", "1", OutputType("enum", "CMOT_Float2"));
        var f3 = Custom(f, "F3", "1", OutputType("none", null));
        var f4 = Custom(f, "F4", "1", OutputType("byte", "3"));
        var outer = Custom(f, "Outer", "1", inputs: [CustomInput("a", f1), CustomInput("b", f2), CustomInput("c", f3), CustomInput("d", f4)]);
        f.Material.WriteProperty(Input("EmissiveColor", outer));
        var result = Translate(f); Success(result);
        // Inner implementations are registered before the outer one.
        AssertOrdered(result.MaterialHlsl, "float CustomExpression0()", "float2 CustomExpression1()", "float3 CustomExpression2()",
            "float4 CustomExpression3()", "float3 CustomExpression4(float a,float2 b,float3 c,float4 d)");
        StringAssert.Contains(result.PropertyCode["EmissiveColor"],
            "\tfloat3 Local4 = CustomExpression4(Local0,Local1,Local2,Local3);");
    }

    // ---- Input ordering / skipping / errors ----------------------------------------------

    [TestMethod]
    public void NamedInputsKeepOriginalOrderAndEmptyNamesAreSkippedEvenWhenCyclicOrUnsupported()
    {
        using var f = Fixture();
        var a = Constant(f, "A", 0.25f);
        var b = Constant(f, "B", 0.75f);
        var unsupported = f.Package.CreateExport("Unsupported", "MaterialExpressionComment", f.Material, indexed: false);
        var time = f.Package.CreateExport("Time", "MaterialExpressionTime", f.Material, indexed: false);
        var custom = Custom(f, code: "A + B");
        custom.WriteProperty(Inputs(CustomInput("", custom), CustomInput("A", a), CustomInput("", unsupported),
            CustomInput("", null), CustomInput("B", b), CustomInput("", time)));
        f.Material.WriteProperty(Input("EmissiveColor", custom));
        var result = Translate(f); Success(result);
        StringAssert.Contains(result.MaterialHlsl, $"float3 CustomExpression0(float A,float B){Crlf}");
        AssertOrdered(result.PropertyCode["EmissiveColor"], "CustomExpression0(", "0.25000000", ",", "0.75000000", ")");
        Assert.IsFalse(result.UsesTime, "An empty-named input must not even be visited.");
    }

    [TestMethod]
    public void WhitespaceInputNamesAreRealNames()
    {
        using var f = Fixture();
        var a = Constant(f, "A", 0.25f);
        var linkedWhitespace = Custom(f, code: "1", inputs: [CustomInput(" ", a)]);
        f.Material.WriteProperty(Input("EmissiveColor", linkedWhitespace));
        var result = Translate(f); Success(result);
        StringAssert.Contains(result.MaterialHlsl, $"float3 CustomExpression0(float  ){Crlf}");

        using var g = Fixture();
        var unlinkedWhitespace = Custom(g, code: "1", inputs: [CustomInput(" ", null)]);
        g.Material.WriteProperty(Input("EmissiveColor", unlinkedWhitespace));
        Failed(Translate(g), "Custom material Custom missing input 1 ( )");
    }

    [TestMethod]
    public void MissingLinkErrorUsesOriginalZeroBasedSlotPlusOneAndStopsBeforeLaterInputs()
    {
        foreach (bool omitExpression in new[] { false, true })
        {
            using var f = Fixture();
            var time = f.Package.CreateExport("Time", "MaterialExpressionTime", f.Material, indexed: false);
            var later = f.Package.CreateExport("LaterTime", "MaterialExpressionTime", f.Material, indexed: false);
            var custom = Custom(f, "My Custom", "1");
            var missing = omitExpression
                ? new StructProperty("CustomInput", new PropertyCollection { new StrProperty("Slot", "InputName") }, "Inputs")
                : CustomInput("Slot", null);
            custom.WriteProperty(Inputs(CustomInput("", null), CustomInput("First", time), CustomInput("", null), missing, CustomInput("After", later)));
            f.Material.WriteProperty(Input("EmissiveColor", custom));
            var result = Translate(f);
            Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
            CollectionAssert.AreEqual(new[] { "Custom material My Custom missing input 4 (Slot)" }, result.Errors.ToArray());
            Assert.IsTrue(result.UsesTime, "Earlier inputs are compiled first.");
        }
    }

    [TestMethod]
    public void ChildFailureAbortsWithoutAnExtraCustomErrorOrLaterInputVisits()
    {
        using var f = Fixture();
        var unsupported = f.Package.CreateExport("Unsupported", "MaterialExpressionComment", f.Material, indexed: false);
        var time = f.Package.CreateExport("Time", "MaterialExpressionTime", f.Material, indexed: false);
        var custom = Custom(f, code: "1", inputs: [CustomInput("Bad", unsupported), CustomInput("Later", time)]);
        f.Material.WriteProperty(Input("EmissiveColor", custom));
        var result = Translate(f);
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
        Assert.AreEqual(1, result.Errors.Count, string.Join("; ", result.Errors));
        StringAssert.Contains(result.Errors[0], "MaterialExpressionComment");
        Assert.IsFalse(result.UsesTime);
        Assert.AreEqual(0, result.PropertyCode.Count);
    }

    // ---- Cube rejection / registration ---------------------------------------------------

    [TestMethod]
    public void CubeTextureArgumentIsRejectedBeforeAnyArgumentCodeAccessAndRegistersNothing()
    {
        using var f = Fixture();
        var plain = Texture(f, "Plain", "Texture2D");
        var cube = Texture(f, "Cube", "TextureCube");
        var scalarParameter = f.Package.CreateExport("Scalar", "MaterialExpressionScalarParameter", f.Material, indexed: false);
        scalarParameter.WriteProperty(new NameProperty("Strength", "ParameterName"));
        var custom = Custom(f, "Material Thing", "1", inputs:
        [
            CustomInput("Strength", scalarParameter),
            CustomInput("Plain", CustomTexture(f, plain)),
            CustomInput("Cubic", CustomTexture(f, cube))
        ]);
        f.Material.WriteProperty(Input("EmissiveColor", custom));
        var result = Translate(f);
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
        CollectionAssert.AreEqual(new[] { "Bad type textureCube for Material Thing input Cubic" }, result.Errors.ToArray());
        // The earlier valid arguments (including the 2D texture and the scalar parameter) were never code-accessed.
        Assert.AreEqual(0, result.UniformExpressionTextures.Count);
        Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count);
        Assert.AreEqual(0, result.UniformCubeTextureExpressions.Count);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
    }

    // ---- CustomTexture: values, laziness, reuse ------------------------------------------

    [TestMethod]
    [DataRow("Texture2D")]
    [DataRow("TextureRenderTarget2D")]
    [DataRow("TextureMovie")]
    public void TextureClassesBecomeSampler2DArgumentsWithoutSamplingOrMetadata(string className)
    {
        using var f = Fixture();
        var texture = Texture(f, "Tex", className);
        var custom = Custom(f, code: "float3(1,0,0)", inputs: [CustomInput("Tex", CustomTexture(f, texture))]);
        f.Material.WriteProperty(Input("EmissiveColor", custom));
        var result = Translate(f, _ => throw new AssertFailedException("CustomTexture needs no texture metadata."),
            resolveImport: _ => throw new AssertFailedException("No resolver calls expected."));
        Success(result);
        StringAssert.Contains(result.MaterialHlsl, $"float3 CustomExpression0(sampler2D Tex){Crlf}{{{Crlf}return float3(1,0,0);{Crlf}}}{Crlf}");
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], "\tfloat3 Local0 = CustomExpression0(Texture2D_0);");
        CollectionAssert.AreEqual(new[] { texture.UIndex }, result.UniformExpressionTextures.ToArray());
        var uniform = result.Uniform2DTextureExpressions.Single();
        Assert.AreEqual(typeof(MaterialUniformExpressionTexture), uniform.GetType());
        Assert.AreEqual("FMaterialUniformExpressionTexture", uniform.ExpressionType.Name);
        Assert.AreEqual(0, uniform.TextureIndex);
        Assert.AreEqual(0, result.UniformCubeTextureExpressions.Count);
    }

    [TestMethod]
    public void TextureUniformFollowsSamplerDeclarationsInTheSameTemplateSlot()
    {
        using var f = Fixture();
        var parameter = f.Package.CreateExport("Scalar", "MaterialExpressionScalarParameter", f.Material, indexed: false);
        parameter.WriteProperty(new NameProperty("Strength", "ParameterName"));
        var texture = Texture(f, "Tex", "Texture2D");
        var custom = Custom(f, code: "float3(Strength,0,0)",
            inputs: [CustomInput("Strength", parameter), CustomInput("Tex", CustomTexture(f, texture))]);
        f.Material.WriteProperty(Input("EmissiveColor", custom));
        var result = Translate(f); Success(result);
        string declarations = $"float4 UniformPixelScalars_0;{Crlf}SAMPLER2D( Texture2D_0 );{Crlf}";
        string helper = $"float3 CustomExpression0(float Strength,sampler2D Tex){Crlf}{{{Crlf}return float3(Strength,0,0);{Crlf}}}{Crlf}{Crlf}{Crlf}";
        Assert.IsTrue(result.MaterialHlsl.StartsWith("0\n" + declarations + helper + "\n", StringComparison.Ordinal), result.MaterialHlsl);
    }

    [TestMethod]
    public void TextureIsPassedAndRegisteredEvenWhenTheBodyNeverUsesIt()
    {
        using var f = Fixture();
        var texture = Texture(f, "Unused", "Texture2D");
        var custom = Custom(f, code: "float3(0,0,0)", inputs: [CustomInput("Ignored", CustomTexture(f, texture))]);
        f.Material.WriteProperty(Input("EmissiveColor", custom));
        var result = Translate(f); Success(result);
        Assert.AreEqual(1, result.UniformExpressionTextures.Count);
        Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], "CustomExpression0(Texture2D_0)");
    }

    [TestMethod]
    public void UnreachableOrUnaccessedCustomTexturesNeverAcquireRegistrations()
    {
        // 1) Skipped (empty-named) input, 2) Custom that fails on a later missing link before argument access,
        // 3) an unselected static-switch branch.
        using var f = Fixture();
        var skipped = Texture(f, "Skipped", "Texture2D");
        var beforeMissing = Texture(f, "BeforeMissing", "Texture2D");
        var inactive = Texture(f, "Inactive", "Texture2D");
        var choice = f.Package.CreateExport("Choice", "MaterialExpressionStaticSwitchParameter", f.Material, indexed: false);
        choice.WriteProperty(Input("A", Constant(f, "Selected", 0.5f))); choice.WriteProperty(Input("B", CustomTexture(f, inactive)));
        choice.WriteProperty(new BoolProperty(true, "DefaultValue"));
        var ok = Custom(f, "Ok", "1", inputs: [CustomInput("", CustomTexture(f, skipped)), CustomInput("Choice", choice)]);
        var failing = Custom(f, "Failing", "1", inputs: [CustomInput("Tex", CustomTexture(f, beforeMissing)), CustomInput("Gone", null)]);
        f.Material.WriteProperty(Input("EmissiveColor", ok));
        var result = Translate(f); Success(result);
        Assert.AreEqual(0, result.UniformExpressionTextures.Count + result.Uniform2DTextureExpressions.Count);

        f.Material.WriteProperty(Input("EmissiveColor", failing));
        result = Translate(f);
        Failed(result, "Custom material Failing missing input 2 (Gone)");
        Assert.AreEqual(0, result.UniformExpressionTextures.Count + result.Uniform2DTextureExpressions.Count);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CustomTextureGetsItsGlobalIndexOnArgumentAccessAfterEarlierSampledTextures(bool customTextureFirst)
    {
        using var f = Fixture();
        var sampled = Texture(f, "Sampled", "Texture2D");
        var passed = Texture(f, "Passed", "Texture2D");
        var sample = Sample(f, sampled);
        var inputs = new List<StructProperty> { CustomInput("Tex", CustomTexture(f, passed)), CustomInput("Rgb", sample, mask: "RGB") };
        if (!customTextureFirst) inputs.Reverse();
        f.Material.WriteProperty(Input("EmissiveColor", Custom(f, code: "Rgb", inputs: inputs.ToArray())));
        var result = Translate(f, _ => new(TextureDimension.Texture2D)); Success(result);
        // The ordinary sample is generated (global + sampler) while the Custom inputs compile; the passed
        // texture only gets its global index (and then its sampler slot) when argument code is accessed.
        CollectionAssert.AreEqual(new[] { sampled.UIndex, passed.UIndex }, result.UniformExpressionTextures.ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1 }, result.Uniform2DTextureExpressions.Select(u => u.TextureIndex).ToArray());
        string call = result.PropertyCode["EmissiveColor"];
        Assert.IsTrue(call.Contains("tex2D(Texture2D_0", StringComparison.Ordinal) || call.Contains("Texture2D_0", StringComparison.Ordinal), call);
        StringAssert.Contains(call, customTextureFirst ? "CustomExpression0(Texture2D_1,Local" : ",Texture2D_1)");
    }

    [TestMethod]
    public void SamplerCompiledAfterCustomAccessGetsTheLaterSlotAndGlobalIndex()
    {
        using var f = Fixture();
        var passed = Texture(f, "Passed", "Texture2D");
        var sampled = Texture(f, "Sampled", "Texture2D");
        var custom = Custom(f, code: "float3(1,1,1)", inputs: [CustomInput("Tex", CustomTexture(f, passed))]);
        var add = f.Package.CreateExport("Add", "MaterialExpressionAdd", f.Material, indexed: false);
        add.WriteProperty(Input("A", custom));
        add.WriteProperty(MaskedInput("B", Sample(f, sampled), "RGB"));
        f.Material.WriteProperty(Input("EmissiveColor", add));
        var result = Translate(f, _ => new(TextureDimension.Texture2D)); Success(result);
        CollectionAssert.AreEqual(new[] { passed.UIndex, sampled.UIndex }, result.UniformExpressionTextures.ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1 }, result.Uniform2DTextureExpressions.Select(u => u.TextureIndex).ToArray());
        AssertOrdered(result.PropertyCode["EmissiveColor"], "CustomExpression0(Texture2D_0)", "tex2D(Texture2D_1");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void OrdinaryTextureSampleAndCustomTextureShareOneUniform(bool customFirst)
    {
        using var f = Fixture();
        var texture = Texture(f, "Shared", "Texture2D");
        var custom = Custom(f, code: "float3(1,1,1)", inputs: [CustomInput("Tex", CustomTexture(f, texture)), CustomInput("Again", CustomTexture(f, texture))]);
        var add = f.Package.CreateExport("Add", "MaterialExpressionAdd", f.Material, indexed: false);
        var sampleInput = MaskedInput(customFirst ? "B" : "A", Sample(f, texture), "RGB");
        add.WriteProperty(customFirst ? Input("A", custom) : Input("B", custom));
        add.WriteProperty(sampleInput);
        f.Material.WriteProperty(Input("EmissiveColor", add));
        var result = Translate(f, _ => new(TextureDimension.Texture2D)); Success(result);
        CollectionAssert.AreEqual(new[] { texture.UIndex }, result.UniformExpressionTextures.ToArray());
        Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
        Assert.AreEqual(typeof(MaterialUniformExpressionTexture), result.Uniform2DTextureExpressions.Single().GetType());
        string code = result.PropertyCode["EmissiveColor"];
        StringAssert.Contains(code, "CustomExpression0(Texture2D_0,Texture2D_0)");
        StringAssert.Contains(code, "tex2D(Texture2D_0");
        Assert.IsFalse(code.Contains("Texture2D_1", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ImportedTextureReferencesNeedNoResolverOrMetadata()
    {
        using var f = Fixture();
        var import = new ImportEntry(f.Package, 0, "ImportedTexture") { ClassName = "Texture2D", PackageFile = "Donor" };
        f.Package.AddImport(import);
        var node = f.Package.CreateExport("CustomTexture", "MaterialExpressionCustomTexture", f.Material, indexed: false);
        node.WriteProperty(new ObjectProperty(import.UIndex, "Texture"));
        f.Material.WriteProperty(Input("EmissiveColor", Custom(f, code: "float3(1,1,1)", inputs: [CustomInput("Tex", node)])));
        var result = Translate(f, _ => throw new AssertFailedException("No metadata."), _ => throw new AssertFailedException("No resolver."));
        Success(result);
        CollectionAssert.AreEqual(new[] { import.UIndex }, result.UniformExpressionTextures.ToArray());
    }

    [TestMethod]
    public void UnsupportedTextureClassIsRejected()
    {
        using var f = Fixture();
        var notTexture = f.Package.CreateExport("NotATexture", "Material", indexed: false);
        var node = CustomTexture(f, notTexture);
        f.Material.WriteProperty(Input("EmissiveColor", Custom(f, code: "1", inputs: [CustomInput("Tex", node)])));
        var result = Translate(f);
        Failed(result, "which is not a supported CustomTexture class");
        Assert.AreEqual(0, result.UniformExpressionTextures.Count);
    }

    // ---- CustomTexture errors -------------------------------------------------------------

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MissingTextureFailsWithTheNativeMessageWithoutMetadata(bool explicitZero)
    {
        using var f = Fixture();
        var node = f.Package.CreateExport("CustomTexture", "MaterialExpressionCustomTexture", f.Material, indexed: false);
        if (explicitZero) node.WriteProperty(new ObjectProperty(0, "Texture"));
        f.Material.WriteProperty(Input("EmissiveColor", Custom(f, code: "1", inputs: [CustomInput("Tex", node)])));
        var result = Translate(f, _ => throw new AssertFailedException("No metadata."));
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
        CollectionAssert.AreEqual(new[] { "CustomTexture> Missing input texture" }, result.Errors.ToArray());
    }

    [TestMethod]
    public void VertexFrequencyCustomTextureFailsWhileNumericCustomStillTranslates()
    {
        using var f = Fixture();
        var texture = Texture(f, "Tex", "Texture2D");
        f.Material.WriteProperty(Input("WorldPositionOffset", Custom(f, code: "float3(1,2,3)")));
        var numeric = Translate(f); Success(numeric);
        StringAssert.Contains(numeric.PropertyCode["WorldPositionOffset"], "\tfloat3 Local0 = CustomExpression0();");

        f.Material.WriteProperty(Input("WorldPositionOffset", Custom(f, "WithTexture", "1", inputs: [CustomInput("Tex", CustomTexture(f, texture))])));
        var result = Translate(f, _ => throw new AssertFailedException("No metadata."));
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
        CollectionAssert.AreEqual(new[] { "Invalid node used in vertex shader input!" }, result.Errors.ToArray());
        Assert.AreEqual(0, result.UniformExpressionTextures.Count + result.Uniform2DTextureExpressions.Count);
    }

    [TestMethod]
    public void MissingTexturePrecedesTheVertexFrequencyGuard()
    {
        using var f = Fixture();
        var node = f.Package.CreateExport("CustomTexture", "MaterialExpressionCustomTexture", f.Material, indexed: false);
        f.Material.WriteProperty(Input("WorldPositionOffset", Custom(f, code: "1", inputs: [CustomInput("Tex", node)])));
        Failed(Translate(f), "CustomTexture> Missing input texture");
    }

    // ---- Texture values are never numeric ------------------------------------------------

    [TestMethod]
    public void TextureValuesAreRejectedInArithmeticAndAsMaterialRootsWithoutRegistration()
    {
        foreach (string place in new[] { "AddA", "AddB", "Root", "Switch" })
        {
            using var f = Fixture();
            var texture = Texture(f, "Tex", "Texture2D");
            var node = CustomTexture(f, texture);
            switch (place)
            {
                case "AddA":
                case "AddB":
                {
                    var add = f.Package.CreateExport("Add", "MaterialExpressionAdd", f.Material, indexed: false);
                    var constant = Constant(f, "C", 1f);
                    add.WriteProperty(Input(place == "AddA" ? "A" : "B", node));
                    add.WriteProperty(Input(place == "AddA" ? "B" : "A", constant));
                    f.Material.WriteProperty(Input("EmissiveColor", add));
                    break;
                }
                case "Root":
                    f.Material.WriteProperty(Input("EmissiveColor", node));
                    break;
                default:
                {
                    var choice = f.Package.CreateExport("Choice", "MaterialExpressionStaticSwitchParameter", f.Material, indexed: false);
                    choice.WriteProperty(Input("A", node)); choice.WriteProperty(Input("B", Constant(f, "Other", 1f)));
                    choice.WriteProperty(new BoolProperty(true, "DefaultValue"));
                    f.Material.WriteProperty(Input("EmissiveColor", choice));
                    break;
                }
            }
            var result = Translate(f, _ => throw new AssertFailedException("No metadata."));
            Assert.IsFalse(result.Success, place); Assert.IsNull(result.MaterialHlsl, place);
            StringAssert.Contains(string.Join("; ", result.Errors), "uses a texture value where a numeric value is required", place);
            Assert.AreEqual(0, result.UniformExpressionTextures.Count + result.Uniform2DTextureExpressions.Count, place);
        }
    }

    [TestMethod]
    [DataRow("custom")]
    [DataRow("add")]
    [DataRow("root")]
    public void MaskedTextureInputsFailWithTheMaskDiagnosticEvenWhereTexturesAreAllowed(string place)
    {
        using var f = Fixture();
        var node = CustomTexture(f, Texture(f, "Tex", "Texture2D"));
        switch (place)
        {
            case "custom":
                f.Material.WriteProperty(Input("EmissiveColor", Custom(f, code: "1", inputs: [CustomInput("Tex", node, mask: "R")])));
                break;
            case "add":
            {
                var add = f.Package.CreateExport("Add", "MaterialExpressionAdd", f.Material, indexed: false);
                add.WriteProperty(MaskedInput("A", node, "R")); add.WriteProperty(Input("B", Constant(f, "C", 1f)));
                f.Material.WriteProperty(Input("EmissiveColor", add));
                break;
            }
            default:
                f.Material.WriteProperty(MaskedInput("EmissiveColor", node, "RGB"));
                break;
        }
        var result = Translate(f);
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
        StringAssert.Contains(string.Join("; ", result.Errors), "applies a component mask to a texture value");
        Assert.AreEqual(0, result.UniformExpressionTextures.Count + result.Uniform2DTextureExpressions.Count);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void StaticSwitchPassesTheSelectedTextureThroughToCustom(bool selectA)
    {
        using var f = Fixture();
        var a = Texture(f, "A", "Texture2D");
        var b = Texture(f, "B", "Texture2D");
        var choice = f.Package.CreateExport("Choice", "MaterialExpressionStaticSwitchParameter", f.Material, indexed: false);
        choice.WriteProperty(Input("A", CustomTexture(f, a))); choice.WriteProperty(Input("B", CustomTexture(f, b)));
        choice.WriteProperty(new BoolProperty(selectA, "DefaultValue"));
        f.Material.WriteProperty(Input("EmissiveColor", Custom(f, code: "float3(1,1,1)", inputs: [CustomInput("Tex", choice)])));
        var result = Translate(f); Success(result);
        StringAssert.Contains(result.MaterialHlsl, $"float3 CustomExpression0(sampler2D Tex){Crlf}");
        CollectionAssert.AreEqual(new[] { (selectA ? a : b).UIndex }, result.UniformExpressionTextures.ToArray());
        Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
    }

    // ---- Numbering ------------------------------------------------------------------------

    [TestMethod]
    public void CustomExpressionsNumberAcrossPropertiesAndRestartForAFreshTranslation()
    {
        using var f = Fixture();
        f.Material.WriteProperty(Input("EmissiveColor", Custom(f, "First", "float3(1,0,0)")));
        f.Material.WriteProperty(Input("DiffuseColor", Custom(f, "Second", "float3(0,1,0)", OutputType("enum", "CMOT_Float4"))));
        f.Material.WriteProperty(Input("Opacity", Custom(f, "Third", "0.5", OutputType("byte", "0"))));
        var compiler = new LE3MaterialShaderCompiler(f.Material);
        var first = compiler.TranslateMaterialHlsl(Template()); Success(first);
        AssertOrdered(first.MaterialHlsl, "float3 CustomExpression0()", "return float3(1,0,0);",
            "float4 CustomExpression1()", "return float3(0,1,0);", "float CustomExpression2()", "return 0.5;");
        StringAssert.Contains(first.PropertyCode["EmissiveColor"], "Local0 = CustomExpression0()");
        StringAssert.Contains(first.PropertyCode["DiffuseColor"], "CustomExpression1()");
        StringAssert.Contains(first.PropertyCode["Opacity"], "CustomExpression2()");

        var second = compiler.TranslateMaterialHlsl(Template()); Success(second);
        Assert.AreEqual(first.MaterialHlsl, second.MaterialHlsl);
        Assert.AreEqual(1, CountOf(second.MaterialHlsl, "float3 CustomExpression0()"));

        // A fresh compiler instance starts at zero too.
        var third = new LE3MaterialShaderCompiler(f.Material).TranslateMaterialHlsl(Template()); Success(third);
        Assert.AreEqual(first.MaterialHlsl, third.MaterialHlsl);
    }

    [TestMethod]
    public void ReusingOneCustomNodeInsideAPropertyEmitsOneHelperAndOneLocal()
    {
        using var f = Fixture();
        var custom = Custom(f, code: "float3(1,1,1)");
        var add = f.Package.CreateExport("Add", "MaterialExpressionAdd", f.Material, indexed: false);
        add.WriteProperty(Input("A", custom)); add.WriteProperty(Input("B", custom));
        f.Material.WriteProperty(Input("EmissiveColor", add));
        var result = Translate(f); Success(result);
        Assert.AreEqual(1, CountOf(result.MaterialHlsl, "float3 CustomExpression0()"));
        Assert.AreEqual(0, CountOf(result.MaterialHlsl, "CustomExpression1"));
        Assert.AreEqual(1, CountOf(result.PropertyCode["EmissiveColor"], "= CustomExpression0();"));
    }

    // ---- Half path ------------------------------------------------------------------------

    [TestMethod]
    public void HalfPathKeepsHelpersAndArgumentsFloatButUsesHalfForTheLocal()
    {
        using var f = Fixture();
        var texture = Texture(f, "Tex", "Texture2D");
        var custom = Custom(f, code: "float3(Value,Value,Value)", inputs: [CustomInput("Value", Constant(f, "S", 0.5f)), CustomInput("Tex", CustomTexture(f, texture))]);
        f.Material.WriteProperty(Input("EmissiveColor", custom));
        var scalar = Custom(f, "Scalar", "Value", OutputType("enum", "CMOT_Float1"), [CustomInput("Value", Constant(f, "S2", 0.25f))]);
        f.Material.WriteProperty(Input("Opacity", scalar));
        var result = Translate(f, half: true); Success(result);
        Assert.IsTrue(result.UsesHalfTypes);
        StringAssert.Contains(result.MaterialHlsl, $"float3 CustomExpression0(float Value,sampler2D Tex){Crlf}");
        StringAssert.Contains(result.MaterialHlsl, $"float CustomExpression1(float Value){Crlf}");
        Assert.IsFalse(result.MaterialHlsl.Contains("half3 CustomExpression", StringComparison.Ordinal));
        Assert.IsFalse(result.MaterialHlsl.Contains("half CustomExpression", StringComparison.Ordinal));
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], "\thalf3 Local0 = CustomExpression0(");
        StringAssert.Contains(result.PropertyCode["Opacity"], "\thalf Local");
        StringAssert.Contains(result.PropertyCode["Opacity"], "= CustomExpression1(");
    }

    // ---- Support helpers ------------------------------------------------------------------

    private sealed record TestFixture(IMEPackage Package, ExportEntry Material) : IDisposable
    {
        private int counter;
        public string Next(string name) => $"{name}{counter++}";
        public void Dispose() => Package.Dispose();
    }

    private static TestFixture Fixture()
    {
        GlobalTest.Init();
        var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CustomExpression.pcc", MEGame.LE3);
        return new(package, package.CreateExport("Material", "Material", indexed: false));
    }

    private static Property OutputType(string kind, string value) => kind switch
    {
        "enum" => new EnumProperty(value, "ECustomMaterialOutputType", MEGame.LE3, "OutputType"),
        "byte" => new ByteProperty(byte.Parse(value), "OutputType"),
        _ => null
    };

    private static ExportEntry Custom(TestFixture f, string description = null, string code = null, Property outputType = null,
        StructProperty[] inputs = null)
    {
        var node = f.Package.CreateExport(f.Next("Custom"), "MaterialExpressionCustom", f.Material, indexed: false);
        if (description != null) node.WriteProperty(new StrProperty(description, "Description"));
        if (code != null) node.WriteProperty(new StrProperty(code, "Code"));
        if (outputType != null) node.WriteProperty(outputType);
        if (inputs != null) node.WriteProperty(Inputs(inputs));
        return node;
    }

    private static ExportEntry Constant(TestFixture f, string name, float value)
    {
        var node = f.Package.CreateExport(f.Next(name), "MaterialExpressionConstant", f.Material, indexed: false);
        node.WriteProperty(new FloatProperty(value, "R"));
        return node;
    }

    private static ExportEntry Constant2(TestFixture f, string name, float r, float g)
    {
        var node = f.Package.CreateExport(f.Next(name), "MaterialExpressionConstant2Vector", f.Material, indexed: false);
        node.WriteProperty(new FloatProperty(r, "R")); node.WriteProperty(new FloatProperty(g, "G"));
        return node;
    }

    private static ExportEntry Texture(TestFixture f, string name, string className) => f.Package.CreateExport(f.Next(name), className, indexed: false);

    private static ExportEntry CustomTexture(TestFixture f, ExportEntry texture)
    {
        var node = f.Package.CreateExport(f.Next("CustomTexture"), "MaterialExpressionCustomTexture", f.Material, indexed: false);
        node.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
        return node;
    }

    private static ExportEntry Sample(TestFixture f, ExportEntry texture)
    {
        var node = f.Package.CreateExport(f.Next("Sample"), "MaterialExpressionTextureSample", f.Material, indexed: false);
        node.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
        return node;
    }

    private static StructProperty Input(string name, ExportEntry node) => new("ExpressionInput", new PropertyCollection
    { new ObjectProperty(node?.UIndex ?? 0, "Expression") }, name);

    private static StructProperty MaskedInput(string name, ExportEntry node, string channels)
    {
        var collection = new PropertyCollection { new ObjectProperty(node.UIndex, "Expression"), new IntProperty(1, "Mask") };
        collection.Add(new IntProperty(channels.Contains('R') ? 1 : 0, "MaskR"));
        collection.Add(new IntProperty(channels.Contains('G') ? 1 : 0, "MaskG"));
        collection.Add(new IntProperty(channels.Contains('B') ? 1 : 0, "MaskB"));
        collection.Add(new IntProperty(channels.Contains('A') ? 1 : 0, "MaskA"));
        return new("ExpressionInput", collection, name);
    }

    private static StructProperty CustomInput(string name, ExportEntry node, string mask = null) =>
        new("CustomInput", new PropertyCollection
        {
            new StrProperty(name, "InputName"),
            mask != null ? MaskedInput("Input", node, mask) : Input("Input", node)
        }, "Inputs");

    private static ArrayProperty<StructProperty> Inputs(params StructProperty[] inputs) => new(inputs, "Inputs");

    private static TranslationResult Translate(TestFixture f, Func<IEntry, TextureCompilationMetadata> metadata = null,
        Func<ImportEntry, ExportEntry> resolveImport = null, bool half = false) =>
        new LE3MaterialShaderCompiler(f.Material, getTextureMetadata: metadata, resolveTextureImport: resolveImport)
            .TranslateMaterialHlsl(Template(), half);

    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
    private static int CountOf(string text, string token)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0) { count++; index += token.Length; }
        return count;
    }
    private static void Success(TranslationResult result) => Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
    private static void Failed(TranslationResult result, string message)
    {
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
        StringAssert.Contains(string.Join("; ", result.Errors), message);
    }
    private static void AssertOrdered(string text, params string[] tokens)
    {
        int position = -1;
        foreach (string token in tokens)
        {
            int next = text.IndexOf(token, position + 1, StringComparison.Ordinal);
            Assert.IsTrue(next > position, $"Expected '{token}' after offset {position} in: {text}");
            position = next;
        }
    }
}
