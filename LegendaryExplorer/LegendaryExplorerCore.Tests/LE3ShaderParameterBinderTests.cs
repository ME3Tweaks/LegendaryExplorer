using System;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3ShaderParameterBinderTests
{
    [TestMethod]
    public void AllocationsBindCaseInsensitivelyWithoutChangingOriginalMapCRC()
    {
        ShaderParameterBinding[] allocations = [Constant("A", 32, buffer: 3), Resource("Texture", 5, sampler: 2)];
        var binder = new LE3ShaderParameterBinder(allocations);
        var originalCRC = binder.ParameterMapCRC;
        Assert.AreEqual(2, binder.UnboundParameters.Count);
        var constant = binder.BindConstant("a");
        Assert.AreEqual((ushort)3, constant.BufferIndex);
        Assert.AreEqual((ushort)32, constant.BaseIndex);
        Assert.AreEqual((ushort)16, constant.NumBytes);
        Assert.IsFalse(constant.IsBound(), "Existing IsBound excludes shared buffers, but the binder must retain them.");
        var texture = binder.BindResource("TEXTURE");
        Assert.AreEqual((ushort)5, texture.BaseIndex);
        Assert.AreEqual((ushort)2, texture.SamplerIndex);
        Assert.AreEqual((ushort)1, texture.NumResources);
        Assert.AreEqual(0, binder.UnboundParameters.Count);
        Assert.AreEqual(originalCRC, binder.ParameterMapCRC);
        Assert.AreEqual(constant, binder.BindConstant("A"), "Native permits a second bind of an allocation.");
        allocations[0] = Constant("Changed", 100);
        Assert.AreEqual(constant, binder.BindConstant("A"), "Caller list mutations must not change the snapshot.");
    }

    [TestMethod]
    public void ParameterMapCRCUsesNativeUTF16AllocationLayoutAndInsertionOrder()
    {
        // Independently calculated non-reflected CRC-32, polynomial 0x04c11db7, over
        // 41 00 / 03 00 20 00 10 00 00 00 00 00 00 00. Not the usual reflected CRC-32.
        Assert.AreEqual(0xea8c309cu, new LE3ShaderParameterBinder([Constant("A", 32, buffer: 3)]).ParameterMapCRC);
        Assert.AreEqual(0u, new LE3ShaderParameterBinder([]).ParameterMapCRC);
        var first = Constant("A", 32, buffer: 3);
        var second = Resource("Texture", 5, sampler: 2);
        Assert.AreNotEqual(new LE3ShaderParameterBinder([first, second]).ParameterMapCRC,
            new LE3ShaderParameterBinder([second, first]).ParameterMapCRC);
        Assert.AreNotEqual(new LE3ShaderParameterBinder([first]).ParameterMapCRC,
            new LE3ShaderParameterBinder([first with { Name = "a" }]).ParameterMapCRC);
        Assert.AreNotEqual(new LE3ShaderParameterBinder([first]).ParameterMapCRC,
            new LE3ShaderParameterBinder([first with { BufferIndex = 4 }]).ParameterMapCRC);
    }

    [TestMethod]
    public void MissingOptionalParametersStayZeroButRequiredAndWrongKindsFail()
    {
        var binder = new LE3ShaderParameterBinder([Constant("A", 0), Resource("Texture", 2)]);
        Assert.AreEqual(default(FShaderParameter), binder.BindConstant("Missing"));
        Assert.AreEqual(default(FShaderResourceParameter), binder.BindResource("Missing"));
        Assert.ThrowsExactly<InvalidOperationException>(() => binder.BindConstant("Missing", optional: false));
        Assert.ThrowsExactly<InvalidOperationException>(() => binder.BindResource("Missing", optional: false));
        Assert.ThrowsExactly<InvalidOperationException>(() => binder.BindConstant("Texture"));
        Assert.ThrowsExactly<InvalidOperationException>(() => binder.BindResource("A"));
        Assert.AreEqual(2, binder.UnboundParameters.Count);
        Assert.AreEqual(ushort.MaxValue, binder.BindResource("Texture").SamplerIndex);
    }

    [TestMethod]
    [DataRow("ViewProjectionMatrix", 1, 0, 64, true)]
    [DataRow("CameraPosition", 1, 64, 16, true)]
    [DataRow("PreViewTranslation", 1, 80, 16, true)]
    [DataRow("ScreenPositionScaleBias", 2, 0, 16, false)]
    [DataRow("MinZ_MaxZRatio", 2, 16, 16, false)]
    [DataRow("DynamicScale", 2, 32, 16, false)]
    public void ViewOwnedExceptionsRequireExactNativeStageBufferOffsetSizeAndKind(string name, int buffer, int offset, int size, bool vertex)
    {
        var stage = vertex ? ShaderStage.Vertex : ShaderStage.Pixel;
        var allocation = Constant(name, (ushort)offset, (ushort)buffer, (ushort)size);
        Assert.IsTrue(LE3ShaderParameterBinder.IsRendererOwnedViewParameter(allocation, stage));
        Assert.IsFalse(LE3ShaderParameterBinder.IsRendererOwnedViewParameter(allocation, vertex ? ShaderStage.Pixel : ShaderStage.Vertex));
        Assert.IsFalse(LE3ShaderParameterBinder.IsRendererOwnedViewParameter(allocation with { BufferIndex = 0 }, stage));
        Assert.IsFalse(LE3ShaderParameterBinder.IsRendererOwnedViewParameter(allocation with { BaseIndex = (ushort)(offset + 1) }, stage));
        Assert.IsFalse(LE3ShaderParameterBinder.IsRendererOwnedViewParameter(allocation with { Size = 4 }, stage));
        Assert.IsFalse(LE3ShaderParameterBinder.IsRendererOwnedViewParameter(allocation with { Kind = ShaderParameterBindingKind.Resource }, stage));
        Assert.IsFalse(LE3ShaderParameterBinder.IsRendererOwnedViewParameter(allocation with { SamplerIndex = 1 }, stage));
        Assert.IsFalse(LE3ShaderParameterBinder.IsRendererOwnedViewParameter(allocation with { Name = "UnknownViewConstant" }, stage));
    }

    [TestMethod]
    public void DuplicateInvalidAndNullAllocationsFailExplicitly()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new LE3ShaderParameterBinder(null));
        Assert.ThrowsExactly<ArgumentException>(() => new LE3ShaderParameterBinder([Constant("A", 0), Constant("a", 16)]));
        Assert.ThrowsExactly<ArgumentException>(() => new LE3ShaderParameterBinder([null]));
        Assert.ThrowsExactly<ArgumentException>(() => new LE3ShaderParameterBinder([Constant("", 0)]));
        Assert.ThrowsExactly<ArgumentException>(() => new LE3ShaderParameterBinder([Constant("A\0B", 0)]));
        Assert.ThrowsExactly<ArgumentException>(() => new LE3ShaderParameterBinder([Constant("A", 0) with { Kind = (ShaderParameterBindingKind)99 }]));
        Assert.ThrowsExactly<ArgumentException>(() => new LE3ShaderParameterBinder([]).BindMaterialPixelParameters(new TranslationResult()));
    }

    [TestMethod]
    public void PixelAndVertexScalarsUseGroupsOfFourAndRetainSharedBufferAllocations()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3BindingPacking.pcc", MEGame.LE3);
        var translation = CreateUniformTranslation(package);
        Assert.AreEqual(5, translation.UniformPixelScalarExpressions.Count);
        Assert.AreEqual(5, translation.UniformVertexScalarExpressions.Count);
        var binder = new LE3ShaderParameterBinder([
            Constant("UniformPixelScalars_0", 32, buffer: 3), Constant("UniformPixelScalars_1", 48, buffer: 3),
            Constant("UniformVertexScalars_0", 80, buffer: 2), Constant("UniformVertexScalars_1", 96, buffer: 2),
            Constant("UniformPixelScalars_2", 64, buffer: 3)]);
        var pixel = binder.BindMaterialPixelParameters(translation);
        var vertex = binder.BindMaterialVertexParameters(translation);
        CollectionAssert.AreEqual(new[] { 0, 1 }, pixel.UniformPixelScalarShaderParameters.Select(p => p.Index).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1 }, vertex.UniformVertexScalarShaderParameters.Select(p => p.Index).ToArray());
        Assert.AreEqual((ushort)3, pixel.UniformPixelScalarShaderParameters[0].Param.BufferIndex);
        Assert.AreEqual((ushort)2, vertex.UniformVertexScalarShaderParameters[0].Param.BufferIndex);
        Assert.IsTrue(pixel.HasContiguousPixelScalarParameters);
        Assert.IsTrue(pixel.HasContiguousPixelVectorParameters, "Empty vector array is valid.");
        Assert.AreEqual("UniformPixelScalars_2", binder.UnboundParameters.Single().Name, "Binding must not guess extra uniform groups.");
    }

    [TestMethod]
    public void OptimizedOutUniformGroupsAreOmittedWithoutRenumbering()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3SparseBindings.pcc", MEGame.LE3);
        var translation = CreateUniformTranslation(package);
        var binder = new LE3ShaderParameterBinder([
            Constant("UniformPixelScalars_1", 32), Constant("UniformVertexScalars_0", 16, size: 0),
            Constant("UniformVertexScalars_1", 32)]);
        var pixel = binder.BindMaterialPixelParameters(translation);
        var vertex = binder.BindMaterialVertexParameters(translation);
        Assert.AreEqual(1, pixel.UniformPixelScalarShaderParameters.Single().Index);
        Assert.AreEqual(1, vertex.UniformVertexScalarShaderParameters.Single().Index);
        Assert.IsTrue(pixel.HasContiguousPixelScalarParameters, "Single reflected group can start at any expression index.");
        Assert.AreEqual(0, binder.UnboundParameters.Count, "A present zero-size allocation is found/bound but omitted from the array.");
    }

    [TestMethod]
    public void PixelFixedParametersUseLE3AliasesAndRoundTripTheValidityFlags()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3BindingsRoundTrip.pcc", MEGame.LE3);
        var translation = CreateUniformTranslation(package);
        string[] names = ["CameraWorldPos", "LocalToWorldMatrix", "WorldToLocalMatrix", "WorldToViewMatrix",
            "InvViewProjectionMatrix", "ViewProjectionMatrix", "MatInverseGamma", "DecalFarPlaneDistance",
            "bEnableScreenDoorFade", "WrapLightingParameters", "MinZ_MaxZRatio", "ScreenPositionScaleBias"];
        var allocations = names.Select((name, i) => Constant(name, (ushort)(16 * i))).ToList();
        allocations.Add(Resource("SceneColorTexture", 3, sampler: 4));
        allocations.Add(Resource("SceneDepthTexture", 5, sampler: 6));
        allocations.Add(Resource("ScreenDoorNoiseTexture", 7, sampler: 8));
        var binder = new LE3ShaderParameterBinder(allocations);
        var pixel = binder.BindMaterialPixelParameters(translation);
        Assert.AreEqual((ushort)96, pixel.InvGamma.BaseIndex);
        Assert.AreEqual((ushort)112, pixel.DecalFarPlaneDistance.BaseIndex);
        Assert.AreEqual((ushort)128, pixel.EnableScreenDoorFade.BaseIndex);
        Assert.AreEqual((ushort)144, pixel.WrapLightingParameters.BaseIndex);
        Assert.AreEqual((ushort)4, pixel.SceneTextureParameters.SceneColorTexture.SamplerIndex);
        Assert.AreEqual((ushort)6, pixel.SceneTextureParameters.SceneDepthTexture.SamplerIndex);
        Assert.AreEqual((ushort)8, pixel.ScreenDoorNoiseTexture.SamplerIndex);
        Assert.AreEqual(0, binder.UnboundParameters.Count);
        using var stream = new MemoryStream();
        pixel.Serialize(new SerializingContainer(stream, package));
        stream.Position = 0;
        var loaded = new FMaterialPixelShaderParameters();
        loaded.Serialize(new SerializingContainer(stream, package, isLoading: true));
        Assert.AreEqual(stream.Length, stream.Position);
        Assert.AreEqual(pixel.InvGamma, loaded.InvGamma);
        Assert.AreEqual(pixel.SceneTextureParameters.SceneColorTexture, loaded.SceneTextureParameters.SceneColorTexture);
        Assert.IsTrue(loaded.HasContiguousPixelScalarParameters);
        Assert.IsTrue(loaded.HasContiguousPixelVectorParameters);
    }

    [TestMethod]
    public void LocalFactoryAndHeightFogBindingsUseNativeNames()
    {
        string[] names = ["LocalToWorld", "LocalToWorldRotDeterminantFlip", "WorldToLocal", "FogMinHeight",
            "FogMaxHeight", "FogDistanceScale", "FogExtinctionDistance", "FogInScattering", "FogStartDistance"];
        var binder = new LE3ShaderParameterBinder(names.Select((n, i) => Constant(n, (ushort)(16 * i))).ToArray());
        var local = binder.BindLocalVertexFactoryParameters();
        var fog = binder.BindHeightFogParameters();
        Assert.AreEqual((ushort)16, local.LocalToWorldRotDeterminantFlip.BaseIndex);
        Assert.AreEqual((ushort)32, local.WorldToLocal.BaseIndex);
        Assert.AreEqual((ushort)48, fog.FogMinHeight.BaseIndex);
        Assert.AreEqual((ushort)80, fog.FogDistanceScale.BaseIndex);
        Assert.AreEqual((ushort)128, fog.FogStartDistance.BaseIndex);
        Assert.AreEqual(0, binder.UnboundParameters.Count);
    }

    [TestMethod]
    public void GPUSkinBinderRequiresLocalToWorldAndBonesAndIncludesBioWareWoundParameters()
    {
        string[] names = ["LocalToWorld", "WorldToLocal", "BoneMatrices", "MaxBoneInfluences", "MeshOrigin", "MeshExtension", "WoundEllipse0", "WoundEllipse1"];
        var binder = new LE3ShaderParameterBinder(names.Select((n, i) => Constant(n, (ushort)(16 * i))).ToArray());
        var parameters = binder.BindGPUSkinVertexFactoryParameters();
        Assert.AreEqual((ushort)32, parameters.BoneMatrices.BaseIndex);
        Assert.AreEqual((ushort)96, parameters.WoundEllipse0.BaseIndex);
        Assert.AreEqual((ushort)112, parameters.WoundEllipse1.BaseIndex);
        Assert.AreEqual(0, binder.UnboundParameters.Count);
        Assert.ThrowsExactly<InvalidOperationException>(() => new LE3ShaderParameterBinder([]).BindGPUSkinVertexFactoryParameters());
        Assert.ThrowsExactly<InvalidOperationException>(() => new LE3ShaderParameterBinder([Constant("LocalToWorld", 0)]).BindGPUSkinVertexFactoryParameters());
        var minimal = new LE3ShaderParameterBinder([Constant("LocalToWorld", 0), Constant("BoneMatrices", 64)]).BindGPUSkinVertexFactoryParameters();
        Assert.AreEqual(default(FShaderParameter), minimal.WoundEllipse0);
        Assert.AreEqual(default(FShaderParameter), minimal.WorldToLocal);
    }

    [TestMethod]
    public void SparseVectorBindingsPreserveExpressionIndicesInEachStage()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3VectorBindings.pcc", MEGame.LE3);
        var translation = CreateUniformTranslation(package, vectors: true);
        Assert.AreEqual(3, translation.UniformPixelVectorExpressions.Count);
        Assert.AreEqual(1, translation.UniformVertexVectorExpressions.Count);
        var binder = new LE3ShaderParameterBinder([
            Constant("UniformPixelVector_0", 16, buffer: 3), Constant("UniformPixelVector_2", 32, buffer: 3),
            Constant("UniformVertexVector_0", 64, buffer: 2)]);
        var pixel = binder.BindMaterialPixelParameters(translation);
        var vertex = binder.BindMaterialVertexParameters(translation);
        CollectionAssert.AreEqual(new[] { 0, 2 }, pixel.UniformPixelVectorShaderParameters.Select(p => p.Index).ToArray());
        Assert.IsFalse(pixel.HasContiguousPixelVectorParameters, "Register contiguity alone is insufficient when expression indices are sparse.");
        Assert.AreEqual(0, vertex.UniformVertexVectorShaderParameters.Single().Index);
        Assert.AreEqual((ushort)2, vertex.UniformVertexVectorShaderParameters.Single().Param.BufferIndex);
        Assert.AreEqual(0, binder.UnboundParameters.Count);
    }

    [TestMethod]
    public void TextureBindingsUseSeparate2DAndCubeIndicesAndOmitOptimizedOutResources()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TextureBindings.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        string[] properties = ["DiffuseColor", "EmissiveColor", "SpecularColor"];
        for (int i = 0; i < properties.Length; i++)
        {
            var texture = package.CreateExport("Texture" + i, i == 2 ? "TextureCube" : "Texture2D", indexed: false);
            var node = package.CreateExport("Sample" + i, "MaterialExpressionTextureSample", material, indexed: false);
            node.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
            if (i == 2)
            {
                var coords = package.CreateExport("CubeCoords", "MaterialExpressionConstant3Vector", material, indexed: false);
                node.WriteProperty(Input("Coordinates", coords.UIndex));
            }
            material.WriteProperty(RgbInput(properties[i], node.UIndex));
        }
        var translation = new LE3MaterialShaderCompiler(material, getTextureMetadata: entry =>
            new TextureCompilationMetadata(entry.ClassName == "TextureCube" ? TextureDimension.TextureCube : TextureDimension.Texture2D))
            .TranslateMaterialHlsl(MinimalTemplate());
        Assert.IsTrue(translation.Success, string.Join("; ", translation.Errors));
        Assert.AreEqual(2, translation.Uniform2DTextureExpressions.Count);
        Assert.AreEqual(1, translation.UniformCubeTextureExpressions.Count);
        var binder = new LE3ShaderParameterBinder([Resource("Texture2D_1", 4, sampler: 6), Resource("TextureCube_0", 8)]);
        var pixel = binder.BindMaterialPixelParameters(translation);
        Assert.AreEqual(1, pixel.UniformPixel2DShaderResourceParameters.Single().Index);
        Assert.AreEqual((ushort)6, pixel.UniformPixel2DShaderResourceParameters.Single().Param.SamplerIndex);
        Assert.AreEqual(0, pixel.UniformPixelCubeShaderResourceParameters.Single().Index);
        Assert.AreEqual(ushort.MaxValue, pixel.UniformPixelCubeShaderResourceParameters.Single().Param.SamplerIndex);
        Assert.AreEqual(0, binder.UnboundParameters.Count);
    }

    [TestMethod]
    [DataRow("empty", true)]
    [DataRow("normal", true)]
    [DataRow("indexGap", false)]
    [DataRow("offsetGap", false)]
    [DataRow("differentBuffer", false)]
    [DataRow("shortRegister", false)]
    [DataRow("descending", false)]
    public void ContiguityChecksMatchNativeLE3Rules(string shape, bool valid)
    {
        TUniformParameter<FShaderParameter>[] entries = shape == "empty" ? [] :
            [new() { Index = 3, Param = new() { BaseIndex = 32, NumBytes = 16, BufferIndex = 2 } },
             new() { Index = 4, Param = new() { BaseIndex = 48, NumBytes = 16, BufferIndex = 2 } }];
        if (shape == "indexGap") entries[1].Index++;
        if (shape == "offsetGap") entries[1].Param.BaseIndex++;
        if (shape == "differentBuffer") entries[1].Param.BufferIndex++;
        if (shape == "shortRegister") entries[1].Param.NumBytes = 4;
        if (shape == "descending") Array.Reverse(entries);
        var parameters = new FMaterialPixelShaderParameters
        {
            UniformPixelScalarShaderParameters = entries, UniformPixelVectorShaderParameters = []
        };
        parameters.RecomputeLE3UniformParameterValidity();
        Assert.AreEqual(valid, parameters.HasContiguousPixelScalarParameters);
        Assert.IsTrue(parameters.HasContiguousPixelVectorParameters);
        parameters.UniformPixelVectorShaderParameters = entries;
        parameters.UniformPixelScalarShaderParameters = [];
        parameters.RecomputeLE3UniformParameterValidity();
        Assert.AreEqual(valid, parameters.HasContiguousPixelVectorParameters);
        Assert.IsTrue(parameters.HasContiguousPixelScalarParameters);
    }

    private static ShaderParameterBinding Constant(string name, ushort offset, ushort buffer = 0, ushort size = 16) =>
        new(name, ShaderParameterBindingKind.Constant, buffer, offset, size, null);

    private static ShaderParameterBinding Resource(string name, ushort index, ushort? sampler = null) =>
        new(name, ShaderParameterBindingKind.Resource, 0, index, 1, sampler);

    private static TranslationResult CreateUniformTranslation(IMEPackage package, bool vectors = false)
    {
        var material = package.CreateExport("Material", "Material", indexed: false);
        var node = package.CreateExport("If", "MaterialExpressionIf", material, indexed: false);
        foreach (string name in new[] { "A", "B", "AGreaterThanB", "AEqualsB", "ALessThanB" })
        {
            var scalar = package.CreateExport(name, "MaterialExpressionScalarParameter", material, indexed: false);
            scalar.WriteProperty(new NameProperty(name, "ParameterName"));
            node.WriteProperty(Input(name, scalar.UIndex));
        }
        material.WriteProperty(Input("Opacity", node.UIndex));
        material.WriteProperty(Input("WorldPositionOffset", node.UIndex));
        if (vectors)
        {
            string[] properties = ["DiffuseColor", "EmissiveColor", "SpecularColor"];
            for (int i = 0; i < properties.Length; i++)
            {
                var parameter = package.CreateExport("Vector" + i, "MaterialExpressionVectorParameter", material, indexed: false);
                parameter.WriteProperty(new NameProperty("Vector" + i, "ParameterName"));
                material.WriteProperty(RgbInput(properties[i], parameter.UIndex));
                if (i == 0) material.WriteProperty(RgbInput("WorldPositionOffset", parameter.UIndex));
            }
        }
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        return result;
    }

    private static string MinimalTemplate() =>
        "%u\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n#line %u\n";

    private static StructProperty Input(string name, int expression) =>
        new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression, "Expression") }, name);

    private static StructProperty RgbInput(string name, int expression) =>
        new("ExpressionInput", new PropertyCollection
        {
            new ObjectProperty(expression, "Expression"), new BoolProperty(true, "Mask"),
            new BoolProperty(true, "MaskR"), new BoolProperty(true, "MaskG"), new BoolProperty(true, "MaskB")
        }, name);
}
