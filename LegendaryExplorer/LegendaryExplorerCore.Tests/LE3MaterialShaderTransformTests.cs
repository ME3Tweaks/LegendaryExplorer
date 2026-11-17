using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3MaterialShaderTransformTests
{
    [TestMethod]
    [DataRow("World", "View", false)]
    [DataRow("World", "View", true)]
    [DataRow("World", "Local", false)]
    [DataRow("World", "Local", true)]
    [DataRow("World", "Tangent", false)]
    [DataRow("World", "Tangent", true)]
    [DataRow("Local", "World", false)]
    [DataRow("Local", "World", true)]
    [DataRow("Local", "View", false)]
    [DataRow("Local", "View", true)]
    [DataRow("Local", "Tangent", false)]
    [DataRow("Local", "Tangent", true)]
    [DataRow("Tangent", "World", false)]
    [DataRow("Tangent", "World", true)]
    [DataRow("Tangent", "View", false)]
    [DataRow("Tangent", "View", true)]
    [DataRow("Tangent", "Local", false)]
    [DataRow("Tangent", "Local", true)]
    public void VectorTransformUsesEveryNativeHelperWithoutFolding(string source, string destination, bool half)
    {
        GlobalTest.Init();
        foreach (string kind in new[] { "Constant3Vector", "ObjectOrientation", "VectorParameter" })
        {
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3Transform.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var input = package.CreateExport("Input", "MaterialExpression" + kind, material, indexed: false);
            input.WriteProperty(new NameProperty("Vector", "ParameterName"));
            var transform = package.CreateExport("Transform", "MaterialExpressionTransform", material, indexed: false);
            var connection = Input("Input", input.UIndex);
            if (kind == "VectorParameter") Mask(connection, 1, 1, 1, 0);
            transform.WriteProperty(connection);
            transform.WriteProperty(new EnumProperty("TRANSFORMSOURCE_" + source, "EMaterialVectorCoordTransformSource", MEGame.LE3, "TransformSourceType"));
            transform.WriteProperty(new EnumProperty("TRANSFORM_" + destination, "EMaterialVectorCoordTransform", MEGame.LE3, "TransformType"));
            material.WriteProperty(Input("DiffuseColor", transform.UIndex));
            var compiler = new LE3MaterialShaderCompiler(material);
            var result = compiler.TranslateMaterialHlsl(Template(), half);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            string type = half ? "half" : "float";
            string prefix = source == "Tangent" || destination == "Tangent" ? "Parameters," : "";
            string argument = kind switch
            {
                "ObjectOrientation" => "ObjectOrientation", "VectorParameter" => "UniformPixelVector_0.rgb",
                _ => type + "3(0.00000000,0.00000000,0.00000000)"
            };
            Assert.AreEqual($"\t{type}3 Local0 = Transform{source}VectorTo{destination}({prefix}{argument});\r\n\treturn Local0", result.PropertyCode["DiffuseColor"]);
            Assert.AreEqual(1u, result.UsingTransforms);
            Assert.AreEqual(kind == "VectorParameter" ? 1 : 0, result.UniformPixelVectorExpressions.Count);
            Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformVertexVectorExpressions.Count);
            foreach (var macros in new[] { compiler.BuildProvisionalMaterialMacros(MaterialResource.Create(), result), compiler.BuildProvisionalMaterialMacros(MaterialResource.Create(), result.Graph) })
            {
                Assert.IsTrue(macros.ContainsKey("WORLD_COORDS"));
                Assert.IsFalse(macros.ContainsKey("WORLD_POS"));
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void VectorTransformShippingDefaultsAreTangentToWorld(bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TransformDefault.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var input = package.CreateExport("Input", "MaterialExpressionObjectOrientation", material, indexed: false);
        var transform = package.CreateExport("Transform", "MaterialExpressionTransform", material, indexed: false);
        transform.WriteProperty(Input("Input", input.UIndex)); material.WriteProperty(Input("Normal", transform.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode["Normal"], "TransformTangentVectorToWorld(Parameters,ObjectOrientation)");
    }

    [TestMethod]
    [DataRow("World", false)]
    [DataRow("World", true)]
    [DataRow("Local", false)]
    [DataRow("Local", true)]
    [DataRow("Tangent", false)]
    [DataRow("Tangent", true)]
    public void SameVectorSpaceRejectsBeforeUsageOrUniformAccess(string space, bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TransformSame.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var input = package.CreateExport("Input", "MaterialExpressionVectorParameter", material, indexed: false);
        var transform = package.CreateExport("Transform", "MaterialExpressionTransform", material, indexed: false);
        transform.WriteProperty(Input("Input", input.UIndex));
        transform.WriteProperty(new EnumProperty("TRANSFORMSOURCE_" + space, "EMaterialVectorCoordTransformSource", MEGame.LE3, "TransformSourceType"));
        transform.WriteProperty(new EnumProperty("TRANSFORM_" + space, "EMaterialVectorCoordTransform", MEGame.LE3, "TransformType"));
        material.WriteProperty(Input("DiffuseColor", transform.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsFalse(result.Success);
        CollectionAssert.Contains(result.Errors.ToArray(), "Source and Destination coordinate spaces must be different");
        Assert.AreEqual(0u, result.UsingTransforms);
        Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
    }

    [TestMethod]
    [DataRow("Constant4Vector", false)]
    [DataRow("Constant4Vector", true)]
    [DataRow("VectorParameter", false)]
    [DataRow("VectorParameter", true)]
    public void Float4PassesVectorGuardButFailsCoercionWithoutUniformPacking(string kind, bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TransformFloat4.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var input = package.CreateExport("Input", "MaterialExpression" + kind, material, indexed: false);
        var transform = package.CreateExport("Transform", "MaterialExpressionTransform", material, indexed: false);
        transform.WriteProperty(Input("Input", input.UIndex)); material.WriteProperty(Input("DiffuseColor", transform.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsFalse(result.Success);
        string type = half ? "half" : "float";
        string definition = kind == "VectorParameter" ? "" : type + "4(0.00000000,0.00000000,0.00000000,0.00000000)";
        CollectionAssert.Contains(result.Errors.ToArray(), $"Coercion failed: {definition}: {type}4 -> {type}3");
        Assert.AreEqual(1u, result.UsingTransforms);
        Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
    }

    [TestMethod]
    [DataRow("Transform", "Constant", false)]
    [DataRow("Transform", "Constant", true)]
    [DataRow("Transform", "Constant2Vector", false)]
    [DataRow("Transform", "Constant2Vector", true)]
    [DataRow("TransformPosition", "Constant", false)]
    [DataRow("TransformPosition", "Constant", true)]
    [DataRow("TransformPosition", "Constant2Vector", false)]
    [DataRow("TransformPosition", "Constant2Vector", true)]
    [DataRow("TransformPosition", "Constant3Vector", false)]
    [DataRow("TransformPosition", "Constant3Vector", true)]
    public void InvalidWidthsPreserveDiagnosticCodeAndPrecision(string node, string kind, bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TransformWidths.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var input = package.CreateExport("Input", "MaterialExpression" + kind, material, indexed: false);
        var transform = package.CreateExport("Transform", "MaterialExpression" + node, material, indexed: false);
        transform.WriteProperty(Input("Input", input.UIndex)); material.WriteProperty(Input("DiffuseColor", transform.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsFalse(result.Success);
        string type = (half ? "half" : "float") + (kind == "Constant" ? "" : kind == "Constant2Vector" ? "2" : "3");
        string message = node == "Transform" ? "input must be a vector" : kind == "Constant3Vector" ? "Input must be a float4, append 1 to the vector" : "Input must be a float4";
        Assert.IsTrue(result.Errors.Any(e => e.StartsWith(message + " (") && e.EndsWith(": " + type + ")")), string.Join("; ", result.Errors));
        Assert.AreEqual(0u, result.UsingTransforms);
    }

    [TestMethod]
    [DataRow("Constant4Vector", false)]
    [DataRow("Constant4Vector", true)]
    [DataRow("VectorParameter", false)]
    [DataRow("VectorParameter", true)]
    [DataRow("ScreenPosition", false)]
    [DataRow("ScreenPosition", true)]
    [DataRow("VertexColor", false)]
    [DataRow("VertexColor", true)]
    public void PositionTransformIsFloat4LocalWithNoOwnUsageOrUniformTree(string kind, bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3Position.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var input = package.CreateExport("Input", "MaterialExpression" + kind, material, indexed: false);
        input.WriteProperty(new NameProperty("Position", "ParameterName"));
        var transform = package.CreateExport("Transform", "MaterialExpressionTransformPosition", material, indexed: false);
        transform.WriteProperty(Input("Input", input.UIndex)); material.WriteProperty(Input("DiffuseColor", transform.UIndex));
        var compiler = new LE3MaterialShaderCompiler(material);
        var result = compiler.TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        string type = half ? "half" : "float";
        string argument = kind switch
        {
            "VectorParameter" => "UniformPixelVector_0", "ScreenPosition" => "Parameters.ScreenPosition", "VertexColor" => "Parameters.VertexColor",
            _ => type + "4(0.00000000,0.00000000,0.00000000,0.00000000)"
        };
        Assert.AreEqual($"\t{type}4 Local0 = MulMatrix(InvViewProjectionMatrix, {argument});\r\n\treturn Local0.rgb", result.PropertyCode["DiffuseColor"]);
        Assert.AreEqual(0u, result.UsingTransforms);
        Assert.AreEqual(kind == "VectorParameter" ? 1 : 0, result.UniformPixelVectorExpressions.Count);
        Assert.AreEqual(kind == "VertexColor", result.UsesVertexColor);
        Assert.IsFalse(compiler.BuildProvisionalMaterialMacros(MaterialResource.Create(), result.Graph).ContainsKey("WORLD_COORDS"));
    }

    [TestMethod]
    [DataRow("Transform", false)]
    [DataRow("Transform", true)]
    [DataRow("TransformPosition", false)]
    [DataRow("TransformPosition", true)]
    public void MissingAndVertexInputsFailWithNativeOrdering(string node, bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TransformStage.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var transform = package.CreateExport("Transform", "MaterialExpression" + node, material, indexed: false);
        material.WriteProperty(Input("WorldPositionOffset", transform.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsFalse(result.Success);
        CollectionAssert.Contains(result.Errors.ToArray(), node == "Transform" ? "Missing Transform input vector" : "Missing Transform Position input vector");
        var input = package.CreateExport("Input", "MaterialExpressionScalarParameter", material, indexed: false);
        transform.WriteProperty(Input("Input", input.UIndex));
        var compiler = new LE3MaterialShaderCompiler(material);
        result = compiler.TranslateMaterialHlsl(Template(), half);
        Assert.IsFalse(result.Success);
        CollectionAssert.Contains(result.Errors.ToArray(), "Invalid node used in vertex shader input!");
        Assert.AreEqual(0, result.UniformVertexScalarExpressions.Count);
        Assert.AreEqual(0u, result.UsingTransforms);
        Assert.IsFalse(compiler.BuildProvisionalMaterialMacros(MaterialResource.Create(), result.Graph).ContainsKey("WORLD_COORDS"));
    }

    [TestMethod]
    [DataRow("Transform")]
    [DataRow("TransformPosition")]
    public void MalformedCoordinatePropertiesDoNotSilentlySelectDefaults(string node)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TransformMalformed.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var input = package.CreateExport("Input", "MaterialExpression" + (node == "Transform" ? "Constant3Vector" : "Constant4Vector"), material, indexed: false);
        var transform = package.CreateExport("Transform", "MaterialExpression" + node, material, indexed: false);
        transform.WriteProperty(Input("Input", input.UIndex));
        transform.WriteProperty(new IntProperty(0, "TransformType")); material.WriteProperty(Input("DiffuseColor", transform.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Errors.Any(e => e.EndsWith("TransformType must be an enum coordinate-space property.")));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void VectorUsageFollowsSelectedStaticPixelBranch(bool selected)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TransformStatic.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var input = package.CreateExport("Input", "MaterialExpressionObjectOrientation", material, indexed: false);
        var transform = package.CreateExport("Transform", "MaterialExpressionTransform", material, indexed: false);
        transform.WriteProperty(Input("Input", input.UIndex));
        var branch = package.CreateExport("Branch", "MaterialExpressionStaticSwitchParameter", material, indexed: false);
        branch.WriteProperty(new BoolProperty(selected, "DefaultValue"));
        branch.WriteProperty(Input("A", transform.UIndex)); branch.WriteProperty(Input("B", input.UIndex));
        material.WriteProperty(Input("DiffuseColor", branch.UIndex));
        var compiler = new LE3MaterialShaderCompiler(material);
        var result = compiler.TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(selected ? 1u : 0u, result.UsingTransforms);
        Assert.AreEqual(selected, compiler.BuildProvisionalMaterialMacros(MaterialResource.Create(), result.Graph).ContainsKey("WORLD_COORDS"));
    }

    private static void Mask(StructProperty input, int r, int g, int b, int a)
    {
        input.Properties.Add(new IntProperty(1, "Mask"));
        input.Properties.Add(new IntProperty(r, "MaskR")); input.Properties.Add(new IntProperty(g, "MaskG"));
        input.Properties.Add(new IntProperty(b, "MaskB")); input.Properties.Add(new IntProperty(a, "MaskA"));
    }
    private static StructProperty Input(string name, int expression) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression, "Expression") }, name);
    private static string Template() => "/*\n%u\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n#line %u\n*/";
}
