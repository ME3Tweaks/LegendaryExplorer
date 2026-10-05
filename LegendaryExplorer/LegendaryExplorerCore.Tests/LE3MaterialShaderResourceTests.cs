using System;
using System.Buffers.Binary;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3MaterialShaderResourceTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ResourceCompositeSharesSelectionPannerNestedSamplersAndTwoSidedUniforms(bool resourceFloat)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ResourceComposite.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var outerTexture = package.CreateExport("OuterTexture", "Texture2D", indexed: false);
        var innerTexture = package.CreateExport("InnerTexture", "Texture2D", indexed: false);
        var panner = package.CreateExport("Panner", "MaterialExpressionPanner", material, indexed: false);
        panner.WriteProperty(new FloatProperty(.2f, "SpeedX")); panner.WriteProperty(new FloatProperty(.3f, "SpeedY"));
        var inner = package.CreateExport("Inner", "MaterialExpressionTextureSample", material, indexed: false);
        inner.WriteProperty(new ObjectProperty(innerTexture.UIndex, "Texture")); inner.WriteProperty(Input("Coordinates", panner.UIndex));
        var outer = package.CreateExport("Outer", "MaterialExpressionTextureSample", material, indexed: false);
        outer.WriteProperty(new ObjectProperty(outerTexture.UIndex, "Texture"));
        var coordinates = Input("Coordinates", inner.UIndex);
        foreach (string mask in new[] { "Mask", "MaskR", "MaskG" }) coordinates.Properties.Add(new IntProperty(1, mask));
        outer.WriteProperty(coordinates);
        foreach (string root in new[] { "Normal", "DiffuseColor", "OpacityMask" })
        {
            var input = Input(root, outer.UIndex);
            foreach (string mask in root == "OpacityMask" ? new[] { "Mask", "MaskA" } : new[] { "Mask", "MaskR", "MaskG", "MaskB" })
                input.Properties.Add(new IntProperty(1, mask));
            material.WriteProperty(input);
        }
        var color = package.CreateExport("Color", "MaterialExpressionVectorParameter", material, indexed: false);
        color.WriteProperty(new NameProperty("SSS_Color", "ParameterName"));
        var maskParameter = package.CreateExport("Mask", "MaterialExpressionScalarParameter", material, indexed: false);
        maskParameter.WriteProperty(new NameProperty("SSS_Mask", "ParameterName"));
        material.WriteProperty(Input("TwoSidedLightingColor", color.UIndex));
        material.WriteProperty(Input("TwoSidedLightingMask", maskParameter.UIndex));
        int reads = 0;
        var compiler = new LE3MaterialShaderCompiler(material, getTextureMetadata: _ =>
        {
            reads++;
            return new(LE3MaterialShaderCompiler.TextureDimension.Texture2D);
        });
        string template = "half2 TexCoords[NUM_MATERIAL_TEXCOORDS];\n" + Template();
        var sources = new LE3ShaderSourceSnapshot(new System.Collections.Generic.Dictionary<string, string> { ["MaterialTemplate.usf"] = template });
        var result = compiler.TranslateMaterialResourceHlsl(sources, resourceFloat);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreSame(sources, result.SourceSnapshot);
        Assert.AreEqual(template, sources.ReadSource("MaterialTemplate"));
        Assert.AreEqual(2, reads, "Metadata cached across roots, not re-read per sample.");
        CollectionAssert.AreEqual(new[] { outerTexture.UIndex, innerTexture.UIndex }, result.UniformExpressionTextures.ToArray());
        CollectionAssert.AreEqual(new[] { 1, 0 }, result.Uniform2DTextureExpressions.Select(t => t.TextureIndex).ToArray());
        Assert.AreEqual(4, result.UniformPixelVectorExpressions.Count);
        Assert.AreEqual("SelectionColor", ((MaterialUniformExpressionVectorParameter)result.UniformPixelVectorExpressions[0]).ParameterName.Name);
        Assert.AreEqual("SSS_Color", ((MaterialUniformExpressionVectorParameter)result.UniformPixelVectorExpressions[2]).ParameterName.Name);
        // Native code-access side effects retain Time, both products and periodic hints, plus
        // the two-sided scalar even though the final multiply is a packed vector tree.
        Assert.AreEqual(6, result.UniformPixelScalarExpressions.Count);
        Assert.AreEqual("SSS_Mask", result.UniformPixelScalarExpressions.OfType<MaterialUniformExpressionScalarParameter>().Single().ParameterName.Name);
        Assert.IsTrue(result.UsesTime);
        foreach (string root in new[] { "Normal", "DiffuseColor", "OpacityMask" })
        {
            string code = result.PropertyCode[root];
            StringAssert.Contains(code, "UniformPixelVector_1.rg");
            Assert.IsTrue(code.IndexOf("tex2D(Texture2D_0,", StringComparison.Ordinal) < code.IndexOf("tex2D(Texture2D_1,", StringComparison.Ordinal));
        }
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], "UniformPixelVector_0.rgb");
        StringAssert.Contains(result.PropertyCode["DiffuseColor"], "(float(1.00000000) - UniformPixelVector_0.rgb)");
        Assert.AreEqual("\treturn UniformPixelVector_3.rgb", result.PropertyCode["TwoSidedLightingMask"]);
        var folded = (MaterialUniformExpressionFoldedMath)result.UniformPixelVectorExpressions[3];
        Assert.AreEqual("SSS_Mask", ((MaterialUniformExpressionScalarParameter)folded.A).ParameterName.Name);
        Assert.AreSame(result.UniformPixelVectorExpressions[2], folded.B);
        Assert.AreEqual(resourceFloat, result.ForcesFloatTexCoords);
    }

    [TestMethod]
    public void ResourceDefaultsIncludeSelectionColorAndNativeGreyColorConversion()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ResourceDefaults.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var compiler = new LE3MaterialShaderCompiler(material);
        var result = compiler.TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.IsTrue(result.IncludesMaterialResourceInputs);
        Assert.IsTrue(result.Graph.IncludesMaterialResourceInputs);
        Assert.AreEqual(14, result.PropertyCode.Count);
        Assert.AreEqual("\treturn float3(0.00000000,0.00000000,1.00000000)", result.PropertyCode["Normal"]);
        Assert.AreEqual("\tfloat3 Local0 = (float3(0.00000000,0.00000000,0.00000000) + UniformPixelVector_0.rgb);\r\n\treturn Local0", result.PropertyCode["EmissiveColor"]);
        Assert.AreEqual("\tfloat3 Local1 = (float(1.00000000) - UniformPixelVector_0.rgb);\r\n\tfloat3 Local2 = (float3(0.21951972,0.21951972,0.21951972) * Local1);\r\n\treturn Local2", result.PropertyCode["DiffuseColor"]);
        Assert.AreEqual("\treturn float3(0.21951972,0.21951972,0.21951972)", result.PropertyCode["SpecularColor"]);
        Assert.AreEqual("\treturn float(15.00000000)", result.PropertyCode["SpecularPower"]);
        foreach (string name in new[] { "DiffusePower", "Opacity", "OpacityMask" }) Assert.AreEqual("\treturn float(1.00000000)", result.PropertyCode[name]);
        Assert.AreEqual("\treturn (float(0.00000000) * float3(1.00000000,1.00000000,1.00000000))", result.PropertyCode["TwoSidedLightingMask"]);
        Assert.AreEqual("\treturn float2(0.00000000,0.00000000)", result.PropertyCode["Distortion"]);
        Assert.AreEqual("\treturn float3(0.00000000,1.00000000,0.00000000)", result.PropertyCode["AnisotropicDirection"]);
        var selection = (MaterialUniformExpressionVectorParameter)result.UniformPixelVectorExpressions.Single();
        Assert.AreEqual("SelectionColor", selection.ParameterName.Name); Assert.AreEqual(0, selection.ParameterName.Number);
        Assert.AreEqual(LinearColor.Black, selection.DefaultValue);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformVertexVectorExpressions.Count);
        Assert.AreEqual(0u, result.UsingTransforms);
        Assert.IsFalse(compiler.TranslateMaterialHlsl(Template()).IncludesMaterialResourceInputs);
        Assert.AreEqual(0, compiler.TranslateMaterialHlsl(Template()).UniformPixelVectorExpressions.Count);
    }

    [TestMethod]
    public void ColorConversionMatchesEveryShippingPow22TableBit()
    {
        byte[] bytes = new byte[1024];
        var convert = typeof(LE3MaterialShaderCompiler).GetMethod("NativeColorByteToLinear", BindingFlags.Static | BindingFlags.NonPublic).CreateDelegate<Func<byte, float>>();
        for (int i = 0; i < 256; i++)
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 4, 4), BitConverter.SingleToInt32Bits(convert((byte)i)));
        Assert.AreEqual("452341880904301051CD9F1FA36DE41AD75E53FCDAA97DAEB8692E8A4B7C16F7", Convert.ToHexString(SHA256.HashData(bytes)));
    }

    [TestMethod]
    [DataRow("MLM_Phong", false)]
    [DataRow("MLM_NonDirectional", false)]
    [DataRow("MLM_Unlit", true)]
    public void ResourceRejectsLitModulateWithNativeDiagnostic(string model, bool succeeds)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ResourceModulate.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        material.WriteProperty(new EnumProperty("BLEND_Modulate", "EBlendMode", MEGame.LE3, "BlendMode"));
        material.WriteProperty(new EnumProperty(model, "EMaterialLightingModel", MEGame.LE3, "LightingModel"));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        Assert.AreEqual(succeeds, result.Success, string.Join("; ", result.Errors));
        if (!succeeds) CollectionAssert.Contains(result.Errors.ToArray(), "Dynamically lit translucency is not supported for BLEND_Modulate materials.");
    }

    [TestMethod]
    [DataRow("Normal")]
    [DataRow("AnisotropicDirection")]
    [DataRow("WrapLightingCoefficients")]
    [DataRow("SpecularColor")]
    [DataRow("CustomLighting")]
    [DataRow("CustomSkylightDiffuse")]
    [DataRow("DiffusePower")]
    [DataRow("SpecularPower")]
    [DataRow("Opacity")]
    [DataRow("OpacityMask")]
    [DataRow("Distortion")]
    public void ResourceUseConstantIgnoresInvalidConnectedExpressionAndReadsTypedPayload(string name)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ResourceConstant.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var input = Input(name, 123456);
        input.Properties.Add(new IntProperty(3, "UseConstant"));
        string expected;
        if (name is "Normal" or "AnisotropicDirection" or "WrapLightingCoefficients")
        {
            input.Properties.Add(new StructProperty("Vector", new PropertyCollection { new FloatProperty(2, "X"), new FloatProperty(3, "Y"), new FloatProperty(4, "Z") }, "Constant", isImmutable: true));
            expected = "float3(2.00000000,3.00000000,4.00000000)";
        }
        else if (name is "SpecularColor" or "CustomLighting" or "CustomSkylightDiffuse")
        {
            input.Properties.Add(new StructProperty("Color", new PropertyCollection { new ByteProperty(255, "B"), new ByteProperty(64, "G"), new ByteProperty(128, "R"), new ByteProperty(0, "A") }, "Constant", isImmutable: true));
            expected = "float3(0.21951972,0.04777575,1.00000000)";
        }
        else if (name == "Distortion")
        {
            input.Properties.Add(new FloatProperty(2, "ConstantX")); input.Properties.Add(new FloatProperty(3, "ConstantY"));
            expected = "float2(2.00000000,3.00000000)";
        }
        else { input.Properties.Add(new FloatProperty(2, "Constant")); expected = "float(2.00000000)"; }
        material.WriteProperty(input);
        var compiler = new LE3MaterialShaderCompiler(material);
        Assert.IsTrue(compiler.AnalyzeMaterialResourceGraph().IsValid);
        Assert.AreEqual(0, compiler.AnalyzeMaterialResourceGraph().ReachableExpressions.Count);
        var result = compiler.TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        string key = name == "CustomSkylightDiffuse" ? "CustomLightingDiffuse" : name;
        Assert.AreEqual("\treturn " + expected, result.PropertyCode[key]);
    }

    [TestMethod]
    public void TwoSidedColorIsCompiledBeforeMaskAndIncludedInSelectedGraphAndUsage()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ResourceTwoSided.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var color = package.CreateExport("Color", "MaterialExpressionWorldPosition", material, indexed: false);
        var mask = package.CreateExport("Mask", "MaterialExpressionScalarParameter", material, indexed: false);
        mask.WriteProperty(new NameProperty("Mask", "ParameterName"));
        material.WriteProperty(Input("TwoSidedLightingColor", color.UIndex));
        material.WriteProperty(Input("TwoSidedLightingMask", mask.UIndex));
        var compiler = new LE3MaterialShaderCompiler(material);
        var result = compiler.TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.IsTrue(result.Graph.ConnectedInputs.ContainsKey("TwoSidedLightingColor"));
        Assert.AreEqual(2, result.Graph.ReachableExpressions.Count);
        StringAssert.Contains(result.PropertyCode["TwoSidedLightingMask"], "(UniformPixelScalars_0.x * Parameters.WorldPosition)");
        Assert.AreEqual(8u, result.UsingTransforms);
        Assert.IsTrue(compiler.BuildProvisionalMaterialMacros(MaterialResource.Create(), result.Graph).ContainsKey("WORLD_POS"));
        var constantColor = Input("TwoSidedLightingColor", color.UIndex);
        constantColor.Properties.Add(new BoolProperty(true, "UseConstant")); material.WriteProperty(constantColor);
        result = compiler.TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(0u, result.UsingTransforms);
        Assert.IsFalse(compiler.BuildProvisionalMaterialMacros(MaterialResource.Create(), result.Graph).ContainsKey("WORLD_POS"));
    }

    [TestMethod]
    public void ResourceRouteUsesCustomSkylightDiffuseAndSharesSelectionParameterIdentity()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ResourceRouting.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var selection = package.CreateExport("Selection", "MaterialExpressionVectorParameter", material, indexed: false);
        selection.WriteProperty(new NameProperty("SelectionColor", "ParameterName"));
        selection.WriteProperty(new StructProperty("LinearColor", new PropertyCollection { new FloatProperty(0, "R"), new FloatProperty(0, "G"), new FloatProperty(0, "B"), new FloatProperty(1, "A") }, "DefaultValue", isImmutable: true));
        material.WriteProperty(Input("CustomSkylightDiffuse", selection.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual("\treturn UniformPixelVector_0.rgb", result.PropertyCode["CustomLightingDiffuse"]);
        Assert.AreEqual(1, result.UniformPixelVectorExpressions.Count);
    }

    [TestMethod]
    public void ConnectedResourceWorldPositionOffsetFailsClosedButAuthoredPathStillWorks()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ResourceWpo.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var offset = package.CreateExport("Offset", "MaterialExpressionConstant3Vector", material, indexed: false);
        material.WriteProperty(Input("WorldPositionOffset", offset.UIndex));
        var compiler = new LE3MaterialShaderCompiler(material);
        Assert.IsTrue(compiler.TranslateMaterialHlsl(Template()).Success);
        var result = compiler.TranslateMaterialResourceHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("WorldPositionOffset is not verified")));
    }

    [TestMethod]
    public void MalformedResourceConstantFlagReturnsDiagnosticInsteadOfUsingTheGraph()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ResourceMalformed.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var input = Input("Normal", 123456); input.Properties.Add(new FloatProperty(1, "UseConstant")); material.WriteProperty(input);
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
        Assert.IsTrue(result.Errors.Any(e => e.EndsWith("UseConstant must be an integer or boolean flag.")));
    }

    [TestMethod]
    [DataRow("Normal")]
    [DataRow("SpecularColor")]
    [DataRow("Opacity")]
    [DataRow("Distortion")]
    public void ResourceRejectsMalformedTypedConstantPayloads(string name)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ResourceMalformedConstant.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var input = Input(name, 0); input.Properties.Add(new BoolProperty(true, "UseConstant"));
        input.Properties.Add(new IntProperty(1, name == "Distortion" ? "ConstantX" : "Constant")); material.WriteProperty(input);
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Material." + name + ".Constant")));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void NativeForceFloatLiteralSubstitutionUsesMaterialOrResourceOverride(bool materialFlag, bool resourceFlag)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ResourcePrecision.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        material.WriteProperty(new BoolProperty(materialFlag, "bForceFullPrecision"));
        const string declaration = "half2 TexCoords[NUM_MATERIAL_TEXCOORDS];";
        string template = declaration + "\nHalf2 TexCoords[NUM_MATERIAL_TEXCOORDS];\n" + Template();
        var compiler = new LE3MaterialShaderCompiler(material);
        var result = compiler.TranslateMaterialResourceHlsl(template, resourceFlag);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(materialFlag || resourceFlag, result.ForcesFloatTexCoords);
        StringAssert.StartsWith(result.MaterialHlsl, (materialFlag || resourceFlag ? "float2" : "half2") + " TexCoords[NUM_MATERIAL_TEXCOORDS];");
        StringAssert.Contains(result.MaterialHlsl, "Half2 TexCoords[NUM_MATERIAL_TEXCOORDS];");
        Assert.IsFalse(result.UsesHalfTypes);
        Assert.AreEqual(materialFlag, compiler.TranslateMaterialHlsl(template).ForcesFloatTexCoords);
    }

    private static StructProperty Input(string name, int expression) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression, "Expression") }, name);
    private static string Template() => "/*\n%u\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n#line %u\n*/";
}
