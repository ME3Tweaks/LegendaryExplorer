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
public class LE3MaterialShaderLeafTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void ReflectionVectorUsesPerPixelCameraFlagForConditionalTransformUsage(bool perPixel, bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3Reflection.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        material.WriteProperty(new BoolProperty(perPixel, "bPerPixelCameraVector"));
        var reflection = package.CreateExport("Reflection", "MaterialExpressionReflectionVector", material, indexed: false);
        material.WriteProperty(Input("EmissiveColor", reflection.UIndex));
        var compiler = new LE3MaterialShaderCompiler(material);
        var result = compiler.TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual("\treturn Parameters.TangentReflectionVector", result.PropertyCode["EmissiveColor"]);
        Assert.AreEqual(perPixel ? 9u : 0u, result.UsingTransforms);
        var resource = MaterialResource.Create();
        foreach (var macros in new[] { compiler.BuildProvisionalMaterialMacros(resource, result), compiler.BuildProvisionalMaterialMacros(resource, result.Graph) })
        {
            Assert.AreEqual(perPixel, macros.ContainsKey("WORLD_POS"));
            Assert.AreEqual(perPixel, macros.ContainsKey("WORLD_COORDS"));
        }
        Assert.IsFalse(result.UsesVertexColor);
        Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count + result.UniformPixelScalarExpressions.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MeshEmitterVertexColorIsWhiteNamedUniformSharedAcrossFrequencies(bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3MeshEmitterColor.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var mesh = package.CreateExport("Mesh", "MaterialExpressionMeshEmitterVertexColor", material, indexed: false);
        // The node has no authored parameter/default fields; unrelated properties cannot override it.
        mesh.WriteProperty(new NameProperty("Wrong", "ParameterName"));
        material.WriteProperty(Input("EmissiveColor", mesh.UIndex));
        material.WriteProperty(Input("WorldPositionOffset", mesh.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual("\treturn UniformPixelVector_0.rgb", result.PropertyCode["EmissiveColor"]);
        Assert.AreEqual("\treturn UniformVertexVector_0.rgb", result.PropertyCode["WorldPositionOffset"]);
        var uniform = (MaterialUniformExpressionVectorParameter)result.UniformPixelVectorExpressions.Single();
        Assert.AreEqual("MeshEmitterVertexColor", uniform.ParameterName.Name);
        Assert.AreEqual(0, uniform.ParameterName.Number);
        Assert.AreEqual(LinearColor.White, uniform.DefaultValue);
        Assert.AreSame(uniform, result.UniformVertexVectorExpressions.Single());
        Assert.IsFalse(result.UsesVertexColor);
        Assert.IsFalse(new LE3MaterialShaderCompiler(material).BuildProvisionalMaterialMacros(MaterialResource.Create(), result.Graph).ContainsKey("MATERIAL_USES_VERTEX_COLOR"));
        LinearColor value = default;
        uniform.GetNumberValue(new UniformExpressionRenderContext(new(), new() { [uniform.ParameterName] = new LinearColor(2, 3, 4, 5) }, 0, 0, null), ref value);
        Assert.AreEqual(new LinearColor(2, 3, 4, 5), value);
    }

    [TestMethod]
    [DataRow("WorldPosition", false)]
    [DataRow("WorldPosition", true)]
    [DataRow("WorldNormal", false)]
    [DataRow("WorldNormal", true)]
    [DataRow("CameraVector", false)]
    [DataRow("CameraVector", true)]
    [DataRow("ReflectionVector", false)]
    [DataRow("ReflectionVector", true)]
    [DataRow("CameraWorldPosition", false)]
    [DataRow("CameraWorldPosition", true)]
    [DataRow("ObjectRadius", false)]
    [DataRow("ObjectRadius", true)]
    [DataRow("ObjectOrientation", false)]
    [DataRow("ObjectOrientation", true)]
    [DataRow("ObjectWorldPosition", false)]
    [DataRow("ObjectWorldPosition", true)]
    [DataRow("ScreenPosition", false)]
    [DataRow("ScreenPosition", true)]
    public void WorldAndCameraLeavesUseExactInlineNamesNativeTypesAndStageGuards(string leaf, bool vertex)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3LeafStages.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var node = package.CreateExport("Leaf", "MaterialExpression" + leaf, material, indexed: false);
        string property = vertex ? "WorldPositionOffset" : "DiffuseColor";
        var connection = Input(property, node.UIndex);
        if (leaf == "ScreenPosition")
        {
            connection.Properties.Add(new BoolProperty(true, "Mask"));
            foreach (string channel in new[] { "MaskR", "MaskG", "MaskB" }) connection.Properties.Add(new BoolProperty(true, channel));
        }
        material.WriteProperty(connection);
        var compiler = new LE3MaterialShaderCompiler(material);
        foreach (bool half in new[] { false, true })
        {
            var result = compiler.TranslateMaterialHlsl(Template(), half);
            if (leaf == "WorldNormal" && !vertex || (leaf is "CameraVector" or "ReflectionVector" or "ScreenPosition") && vertex)
            {
                Assert.IsFalse(result.Success);
                Assert.IsNull(result.MaterialHlsl);
                CollectionAssert.AreEqual(new[] { leaf == "WorldNormal" ? "Invalid node WorldNormal used in pixel shader input!" : "Invalid node used in vertex shader input!" }, result.Errors.ToArray());
                continue;
            }
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            string code = leaf switch { "WorldPosition" => "Parameters.WorldPosition", "WorldNormal" => "Parameters.WorldNormal",
                "CameraVector" => "Parameters.TangentCameraVector", "ReflectionVector" => "Parameters.TangentReflectionVector", "ObjectRadius" => "ObjectWorldPositionAndRadius.w",
                "ObjectOrientation" => "ObjectOrientation", "ObjectWorldPosition" => "ObjectWorldPositionAndRadius.xyz",
                "ScreenPosition" => "Parameters.ScreenPosition", _ => "CameraWorldPos" };
            Assert.AreEqual("\treturn " + code + (leaf == "ScreenPosition" ? ".rgb" : ""), result.PropertyCode[property]);
            Assert.AreEqual(leaf == "WorldPosition" && !vertex ? 8u : 0u, result.UsingTransforms);
            Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count + result.UniformPixelScalarExpressions.Count +
                result.UniformVertexVectorExpressions.Count + result.UniformVertexScalarExpressions.Count);
            var resource = MaterialResource.Create();
            var macros = compiler.BuildProvisionalMaterialMacros(resource, result);
            Assert.AreEqual(leaf == "WorldPosition" && !vertex, macros.ContainsKey("WORLD_POS"));
            Assert.AreEqual(0u, resource.UsingTransforms, "Fresh usage must not mutate the supplied cooked resource.");
            Assert.AreEqual(macros.ContainsKey("WORLD_POS"), compiler.BuildProvisionalMaterialMacros(resource, result.Graph).ContainsKey("WORLD_POS"));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WorldPositionUsageFollowsStaticPixelBranchEvenWhenVertexStillUsesTheLeaf(bool selected)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3WorldPositionStatic.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var world = package.CreateExport("World", "MaterialExpressionWorldPosition", material, indexed: false);
        var camera = package.CreateExport("Camera", "MaterialExpressionCameraWorldPosition", material, indexed: false);
        var branch = package.CreateExport("Branch", "MaterialExpressionStaticSwitchParameter", material, indexed: false);
        branch.WriteProperty(new NameProperty("UseWorld", "ParameterName"));
        branch.WriteProperty(new BoolProperty(selected, "DefaultValue"));
        branch.WriteProperty(Input("A", world.UIndex)); branch.WriteProperty(Input("B", camera.UIndex));
        material.WriteProperty(Input("DiffuseColor", branch.UIndex));
        material.WriteProperty(Input("WorldPositionOffset", world.UIndex));
        var compiler = new LE3MaterialShaderCompiler(material);
        var result = compiler.TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success);
        Assert.AreEqual(selected ? 8u : 0u, result.UsingTransforms);
        Assert.AreEqual("\treturn Parameters.WorldPosition", result.PropertyCode["WorldPositionOffset"]);
        Assert.AreEqual(selected, compiler.BuildProvisionalMaterialMacros(MaterialResource.Create(), result).ContainsKey("WORLD_POS"));
        Assert.AreEqual(selected, compiler.BuildProvisionalMaterialMacros(MaterialResource.Create(), result.Graph).ContainsKey("WORLD_POS"));
    }

    [TestMethod]
    [DataRow("CameraWorldPosition", "CameraWorldPos", "float3")]
    [DataRow("ObjectRadius", "ObjectWorldPositionAndRadius.w", "float")]
    [DataRow("ObjectOrientation", "ObjectOrientation", "float3")]
    [DataRow("ObjectWorldPosition", "ObjectWorldPositionAndRadius.xyz", "float3")]
    public void InlineGlobalLeafIsNonuniformAndUsesExistingAliasInBothShaderStages(string leaf, string code, string type)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CameraBinding.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var camera = package.CreateExport("Camera", "MaterialExpression" + leaf, material, indexed: false);
        var one = package.CreateExport("One", "MaterialExpressionConstant", material, indexed: false);
        one.WriteProperty(new FloatProperty(1, "R"));
        var add = package.CreateExport("Add", "MaterialExpressionAdd", material, indexed: false);
        add.WriteProperty(Input("A", camera.UIndex)); add.WriteProperty(Input("B", one.UIndex));
        material.WriteProperty(Input("DiffuseColor", add.UIndex));
        material.WriteProperty(Input("WorldPositionOffset", add.UIndex));
        var compiler = new LE3MaterialShaderCompiler(material);
        string declarations = "float3 CameraWorldPos; float4 ObjectWorldPositionAndRadius; float3 ObjectOrientation; ";
        string returned = type == "float" ? "(" + code + ").xxx" : code;
        var sources = new LE3ShaderSourceSnapshot(new Dictionary<string, string>
        {
            ["MaterialTemplate.usf"] = Template(),
            ["BasePassPixelShader.usf"] = declarations + "float4 Main(float4 p:SV_Position):SV_Target{return float4(" + returned + ",1);}",
            ["BasePassVertexShader.usf"] = declarations + "float4 Main(float4 p:POSITION):SV_Position{return float4(" + returned + ",1);}",
            ["LocalVertexFactory.usf"] = "// camera binding fixture"
        });
        var result = compiler.TranslateMaterialHlsl(sources);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        foreach (string property in new[] { "DiffuseColor", "WorldPositionOffset" })
        {
            string local = property == "DiffuseColor" ? "Local0" : "Local1";
            StringAssert.Contains(result.PropertyCode[property], $"{type} {local} = ({code} + float(1.00000000));");
            StringAssert.Contains(result.PropertyCode[property], "return " + local);
        }
        Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count + result.UniformVertexVectorExpressions.Count);
        foreach (bool vertex in new[] { false, true })
        {
            string name = vertex ? "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy" : "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight";
            Assert.IsTrue(TryGetKnownPermutationEnvironment(name, "FLocalVertexFactory", out var environment));
            var job = compiler.BuildProvisionalCompileJob(result, MaterialResource.Create(), environment.Target, environment.CreateCandidate().EnvironmentMacros);
            var compiled = compiler.CompileShader(job, sources);
            Assert.IsTrue(compiled.Success, compiled.Diagnostics);
            CollectionAssert.AreEqual(new[] { code.Split('.')[0] }, compiled.ParameterBindings.Select(p => p.Name).ToArray());
            var construction = compiler.ConstructKnownShader(result, compiled);
            Assert.IsTrue(construction.Success, string.Join("; ", construction.Errors));
            Assert.AreEqual(0, construction.UnboundParameters.Count);
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void ScreenPositionAlignmentPreservesChunkMaskAndHalfBehavior(bool aligned, bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ScreenAligned.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var node = package.CreateExport("Screen", "MaterialExpressionScreenPosition", material, indexed: false);
        node.WriteProperty(new BoolProperty(aligned, "ScreenAlign"));
        var input = Input("DiffuseColor", node.UIndex);
        input.Properties.Add(new BoolProperty(true, "Mask"));
        foreach (string channel in new[] { "MaskR", "MaskG", "MaskB" }) input.Properties.Add(new BoolProperty(true, channel));
        material.WriteProperty(input);
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(aligned ? $"\t{(half ? "half" : "float")}4 Local0 = ScreenAlignedPosition(Parameters.ScreenPosition);\r\n\treturn Local0.rgb"
            : "\treturn Parameters.ScreenPosition.rgb", result.PropertyCode["DiffuseColor"]);
        Assert.AreEqual(0u, result.UsingTransforms);
        Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
        material.WriteProperty(Input("WorldPositionOffset", node.UIndex));
        var invalid = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsFalse(invalid.Success);
        CollectionAssert.AreEqual(new[] { "Invalid node used in vertex shader input!" }, invalid.Errors.ToArray());
    }

    [TestMethod]
    [DataRow(0x9000u)]
    [DataRow(0x9400u)]
    public void Sm5CompilerInsertsNoPreshaderInBytecodeButRejectsItAsExplicitInput(uint flags)
    {
        byte[] source = System.Text.Encoding.UTF8.GetBytes("float4 Main(float4 p:SV_Position):SV_Target{return p;}");
        using var compiled = global::SharpDX.D3DCompiler.ShaderBytecode.Compile(source, "Main", "ps_5_0",
            (global::SharpDX.D3DCompiler.ShaderFlags)flags, global::SharpDX.D3DCompiler.EffectFlags.None,
            null, null, "FlagCalibration.usf");
        using var reflection = new global::SharpDX.D3DCompiler.ShaderReflection(compiled.Bytecode);
        Assert.AreEqual(flags | 0x100u, (uint)reflection.Description.Flags);
        Assert.ThrowsExactly<global::SharpDX.CompilationException>(() =>
        {
            using var rejected = global::SharpDX.D3DCompiler.ShaderBytecode.Compile(source, "Main", "ps_5_0",
                (global::SharpDX.D3DCompiler.ShaderFlags)(flags | 0x100u), global::SharpDX.D3DCompiler.EffectFlags.None,
                null, null, "FlagCalibration.usf");
        });
    }

    [TestMethod]
    public void ObjectRadiusHasNativeScalarTypeForIfComparisons()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3RadiusIf.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var radius = package.CreateExport("Radius", "MaterialExpressionObjectRadius", material, indexed: false);
        var one = package.CreateExport("One", "MaterialExpressionConstant", material, indexed: false);
        one.WriteProperty(new FloatProperty(1, "R"));
        var node = package.CreateExport("If", "MaterialExpressionIf", material, indexed: false);
        node.WriteProperty(Input("A", radius.UIndex));
        foreach (string field in new[] { "B", "AGreaterThanB", "AEqualsB", "ALessThanB" })
            node.WriteProperty(Input(field, one.UIndex));
        material.WriteProperty(Input("Opacity", node.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode["Opacity"], "ObjectWorldPositionAndRadius.w >= float(1.00000000)");
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
    }

    private static StructProperty Input(string name, int expression) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression, "Expression") }, name);
    private static string Template() => "/*\n%u\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n#line %u\n*/";
}
