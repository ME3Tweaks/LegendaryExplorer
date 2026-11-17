using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3DecalWorldPositionOffsetTests
{
    [TestMethod]
    public void DecalResourceIgnoresValidParameterWorldPositionOffsetAndItsSideEffects()
    {
        GlobalTest.Init();
        using var package = Package();
        var decal = Material(package, "DecalMaterial");
        var parameter = package.CreateExport("Offset", "MaterialExpressionVectorParameter", decal, indexed: false);
        parameter.WriteProperty(new NameProperty("IgnoredOffset", "ParameterName"));
        decal.WriteProperty(Input("WorldPositionOffset", parameter.UIndex));
        var compiler = new LE3MaterialShaderCompiler(decal, getTextureMetadata: _ =>
            throw new System.InvalidOperationException("Ignored WPO must not read texture metadata."));

        var graph = compiler.AnalyzeMaterialResourceGraph();
        Assert.IsTrue(graph.IsValid);
        Assert.AreEqual(0, graph.ReachableExpressions.Count);
        var result = compiler.TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual("\treturn 0", result.PropertyCode["WorldPositionOffset"]);
        Assert.AreEqual(0, result.UniformVertexVectorExpressions.Count);
        Assert.AreEqual(0, result.UniformVertexScalarExpressions.Count);
        Assert.AreEqual(2u, result.NumUserTexCoords, "Decal usage reserves its baseline coordinates independently of ignored WPO.");
        Assert.IsFalse(result.UsesTime);
        Assert.IsFalse(result.UniformPixelVectorExpressions.OfType<MaterialUniformExpressionVectorParameter>()
            .Any(e => e.ParameterName.Name == "IgnoredOffset"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DecalResourceIgnoresInvalidReferenceAndMalformedConstantPayload(bool malformedUseConstant)
    {
        GlobalTest.Init();
        using var package = Package();
        var decal = Material(package, "DecalMaterial");
        var input = Input("WorldPositionOffset", 123456);
        if (malformedUseConstant) input.Properties.Add(new FloatProperty(1, "UseConstant"));
        else
        {
            input.Properties.Add(new BoolProperty(true, "UseConstant"));
            input.Properties.Add(new IntProperty(7, "Constant"));
        }
        decal.WriteProperty(input);

        var compiler = new LE3MaterialShaderCompiler(decal);
        var graph = compiler.AnalyzeMaterialResourceGraph();
        Assert.IsTrue(graph.IsValid);
        Assert.AreEqual(0, graph.ReachableExpressions.Count);
        var result = compiler.TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual("\treturn 0", result.PropertyCode["WorldPositionOffset"]);
        Assert.AreEqual(0, result.Errors.Count);
    }

    [TestMethod]
    public void AuthoredDecalWorldPositionOffsetStillCompiles()
    {
        GlobalTest.Init();
        using var package = Package();
        var decal = Material(package, "DecalMaterial");
        var offset = package.CreateExport("Offset", "MaterialExpressionConstant3Vector", decal, indexed: false);
        decal.WriteProperty(Input("WorldPositionOffset", offset.UIndex));

        var result = new LE3MaterialShaderCompiler(decal).TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode["WorldPositionOffset"], "float3(0.00000000,0.00000000,0.00000000)");
    }

    [TestMethod]
    public void IgnoredDecalTextureAndPannerGraphDoesNotRequestMetadataOrEmitUsage()
    {
        GlobalTest.Init();
        using var package = Package();
        var decal = Material(package, "DecalMaterial");
        decal.WriteProperty(new BoolProperty(false, "bUsedWithDecals"));
        var texture = package.CreateExport("Texture", "Texture2D", indexed: false);
        var panner = package.CreateExport("Panner", "MaterialExpressionPanner", decal, indexed: false);
        panner.WriteProperty(new FloatProperty(1, "SpeedX"));
        var sample = package.CreateExport("Sample", "MaterialExpressionTextureSample", decal, indexed: false);
        sample.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
        sample.WriteProperty(Input("Coordinates", panner.UIndex));
        decal.WriteProperty(Input("WorldPositionOffset", sample.UIndex));
        int metadataReads = 0;
        var result = new LE3MaterialShaderCompiler(decal, getTextureMetadata: _ =>
        {
            metadataReads++;
            return new(LE3MaterialShaderCompiler.TextureDimension.Texture2D);
        }).TranslateMaterialResourceHlsl(Template());

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual("\treturn 0", result.PropertyCode["WorldPositionOffset"]);
        Assert.IsFalse(result.Graph.ConnectedInputs.ContainsKey("WorldPositionOffset"));
        Assert.AreEqual(0, result.Graph.ReachableExpressions.Count);
        Assert.AreEqual(0, metadataReads);
        Assert.AreEqual(0, result.UniformExpressionTextures.Count);
        Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count);
        Assert.AreEqual(0, result.UniformVertexVectorExpressions.Count);
        Assert.AreEqual(0, result.UniformVertexScalarExpressions.Count);
        Assert.AreEqual(0u, result.NumUserTexCoords);
        Assert.IsFalse(result.UsesTime);
        Assert.AreEqual(0u, result.UsingTransforms);
    }

    [TestMethod]
    [DataRow("Material", false)]
    [DataRow("DecalMaterial", true)]
    public void OnlyDecalResourceSkipsInheritedExpressionRootResolution(string className, bool succeeds)
    {
        GlobalTest.Init();
        using var package = Package();
        var archetype = package.CreateExport("Archetype", className, indexed: false);
        var offset = package.CreateExport("Offset", "MaterialExpressionConstant3Vector", archetype, indexed: false);
        archetype.WriteProperty(Input("WorldPositionOffset", offset.UIndex));
        var material = Material(package, className);
        material.Archetype = archetype;
        var compiler = new LE3MaterialShaderCompiler(material);
        var result = compiler.TranslateMaterialResourceHlsl(Template());

        Assert.AreEqual(succeeds, result.Success, string.Join("; ", result.Errors));
        Assert.IsTrue(compiler.BuildShouldCacheMaterialContext().HasWorldPositionOffset);
        Assert.IsNull(material.GetProperties().GetProp<StructProperty>("WorldPositionOffset"));
        if (succeeds) Assert.AreEqual("\treturn 0", result.PropertyCode["WorldPositionOffset"]);
        else Assert.IsTrue(result.Errors.Any(e => e.Contains("inherited expression reference")));
    }

    [TestMethod]
    public void IgnoringWpoDoesNotPruneTheSameExpressionFromASelectedPixelRoot()
    {
        GlobalTest.Init();
        using var package = Package();
        var decal = Material(package, "DecalMaterial");
        var parameter = package.CreateExport("Shared", "MaterialExpressionVectorParameter", decal, indexed: false);
        parameter.WriteProperty(new NameProperty("SharedOffset", "ParameterName"));
        decal.WriteProperty(Input("WorldPositionOffset", parameter.UIndex));
        decal.WriteProperty(Input("EmissiveColor", parameter.UIndex));
        var compiler = new LE3MaterialShaderCompiler(decal);
        var result = compiler.TranslateMaterialResourceHlsl(Template());

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.IsTrue(result.Graph.ConnectedInputs.ContainsKey("EmissiveColor"));
        Assert.IsFalse(result.Graph.ConnectedInputs.ContainsKey("WorldPositionOffset"));
        Assert.AreEqual(1, result.Graph.ReachableExpressions.Count);
        Assert.AreSame(parameter, result.Graph.ReachableExpressions.Single());
        Assert.AreEqual("\treturn 0", result.PropertyCode["WorldPositionOffset"]);
        Assert.AreEqual(1, result.UniformPixelVectorExpressions.OfType<MaterialUniformExpressionVectorParameter>()
            .Count(e => e.ParameterName.Name == "SharedOffset"));
        Assert.AreEqual(0, result.UniformVertexVectorExpressions.Count);
        Assert.IsTrue(compiler.BuildShouldCacheMaterialContext().HasWorldPositionOffset);
    }

    [TestMethod]
    [DataRow("Material", false)]
    [DataRow("DecalMaterial", true)]
    public void ResourceClassDeterminesWhetherWpoCycleIsVisited(string className, bool succeeds)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package, className);
        var cycle = package.CreateExport("Cycle", "MaterialExpressionOneMinus", material, indexed: false);
        cycle.WriteProperty(Input("Input", cycle.UIndex));
        material.WriteProperty(Input("WorldPositionOffset", cycle.UIndex));
        material.WriteProperty(new ArrayProperty<ObjectProperty>(new[] { new ObjectProperty(cycle.UIndex) }, "Expressions"));
        var compiler = new LE3MaterialShaderCompiler(material);
        var graph = compiler.AnalyzeMaterialResourceGraph();
        Assert.AreEqual(succeeds, graph.IsValid, string.Join("; ", graph.Errors));
        var result = compiler.TranslateMaterialResourceHlsl(Template());
        Assert.AreEqual(succeeds, result.Success, string.Join("; ", result.Errors));
        if (succeeds)
        {
            Assert.AreEqual(0, graph.ReachableExpressions.Count);
            Assert.AreSame(cycle, graph.UnreachableExpressions.Single());
            Assert.AreEqual("\treturn 0", result.PropertyCode["WorldPositionOffset"]);
        }
        else Assert.IsNull(result.MaterialHlsl);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OrdinaryMaterialResourceStillCompilesConnectedAndConstantWorldPositionOffset(bool useConstant)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package, "Material");
        material.WriteProperty(new BoolProperty(true, "bUsedWithDecals"));
        var offset = package.CreateExport("Offset", "MaterialExpressionConstant3Vector", material, indexed: false);
        var input = Input("WorldPositionOffset", offset.UIndex);
        if (useConstant)
        {
            input.Properties.Add(new BoolProperty(true, "UseConstant"));
            input.Properties.Add(new StructProperty("Vector", new PropertyCollection
            {
                new FloatProperty(2, "X"), new FloatProperty(3, "Y"), new FloatProperty(4, "Z")
            }, "Constant", isImmutable: true));
        }
        material.WriteProperty(input);

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreNotEqual("\treturn 0", result.PropertyCode["WorldPositionOffset"]);
        if (useConstant) Assert.AreEqual("\treturn float3(2.00000000,3.00000000,4.00000000)", result.PropertyCode["WorldPositionOffset"]);
        else StringAssert.Contains(result.PropertyCode["WorldPositionOffset"], "float3(0.00000000,0.00000000,0.00000000)");
    }

    [TestMethod]
    [DataRow("Material")]
    [DataRow("DecalMaterial")]
    public void CacheContextWpoPredicateUsesStoredExpressionRegardlessOfConstantFlag(string className)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package, className);
        var input = Input("WorldPositionOffset", 345);
        input.Properties.Add(new BoolProperty(true, "UseConstant"));
        material.WriteProperty(input);

        Assert.IsTrue(new LE3MaterialShaderCompiler(material).BuildShouldCacheMaterialContext().HasWorldPositionOffset);
        input = Input("WorldPositionOffset", 0);
        input.Properties.Add(new BoolProperty(true, "UseConstant"));
        input.Properties.Add(new StructProperty("Vector", new PropertyCollection
        {
            new FloatProperty(1, "X"), new FloatProperty(2, "Y"), new FloatProperty(3, "Z")
        }, "Constant", isImmutable: true));
        material.WriteProperty(input);
        Assert.IsFalse(new LE3MaterialShaderCompiler(material).BuildShouldCacheMaterialContext().HasWorldPositionOffset);
    }

    private static IMEPackage Package() => MEPackageHandler.CreateMemoryEmptyPackage("LE3DecalWpo.pcc", MEGame.LE3);
    private static ExportEntry Material(IMEPackage package, string className) =>
        package.CreateExport(className, className, indexed: false);
    private static StructProperty Input(string name, int expression) =>
        new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression, "Expression") }, name);
    private static string Template() => "/*\n%u\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n#line %u\n*/";
}
