using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3EmptyShaderFragmentTests
{
    [TestMethod]
    [DataRow("explicitEmpty")]
    [DataRow("explicitExcluded")]
    [DataRow("observedExcluded")]
    public void EmptyPlanPreservesDetachedIdentityUniformsAndTexturesWithoutSourceReads(string shape)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3EmptyFragment.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var parameter = package.CreateExport("Scale", "MaterialExpressionScalarParameter", material, indexed: false);
        parameter.WriteProperty(new NameProperty("Scale", "ParameterName"));
        parameter.WriteProperty(new FloatProperty(.4f, "DefaultValue"));
        material.WriteProperty(Input("Opacity", parameter.UIndex));
        material.WriteProperty(Input("WorldPositionOffset", parameter.UIndex));
        var color = package.CreateExport("Tint", "MaterialExpressionVectorParameter", material, indexed: false);
        color.WriteProperty(new NameProperty("Tint", "ParameterName"));
        material.WriteProperty(Input("DiffuseColor", color.UIndex));
        var texture = package.CreateExport("Texture", "Texture2D", indexed: false);
        var sample = package.CreateExport("Sample", "MaterialExpressionTextureSample", material, indexed: false);
        sample.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
        material.WriteProperty(Input("EmissiveColor", sample.UIndex));
        var instance = package.CreateExport("Instance", "MaterialInstanceConstant", indexed: false);
        var baseId = Guid.NewGuid();
        var staticParameters = (StaticParameterSet)baseId;
        staticParameters.StaticSwitchParameters = [new() { ParameterName = "InheritedSwitch", Value = true, bOverride = false }];
        var input = new ResolvedMaterialCompilationInput(material, staticParameters, instance);
        int metadataReads = 0;
        var compiler = new LE3MaterialShaderCompiler(input, getTextureMetadata: reference =>
        {
            Assert.AreSame(texture, reference); metadataReads++;
            return new(TextureDimension.Texture2D);
        });
        var sources = Sources();
        var translation = compiler.TranslateMaterialHlsl(sources);
        Assert.IsTrue(translation.Success, string.Join("; ", translation.Errors));
        Assert.AreEqual(1, metadataReads);
        Assert.AreEqual(1, translation.UniformPixelScalarExpressions.Count);
        Assert.AreEqual(1, translation.UniformVertexScalarExpressions.Count);
        Assert.AreEqual(1, translation.UniformPixelVectorExpressions.Count);
        Assert.AreEqual(1, translation.Uniform2DTextureExpressions.Count);
        CollectionAssert.AreEqual(new[] { texture.UIndex }, translation.UniformExpressionTextures.ToArray());
        var plan = shape == "observedExcluded"
            ? compiler.BuildObservedSubsetCompilePlan(translation, MaterialResource.Create(), materialResourceShouldCache: (_, _) => false)
            : compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(),
                shape == "explicitEmpty" ? [] : [Candidate()], materialResourceShouldCache: (_, _) => false);
        Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors)); Assert.AreEqual(0, plan.Jobs.Count);
        Assert.AreSame(input, plan.CompilationInput);
        Assert.AreEqual(shape == "observedExcluded" ? 4558 : shape == "explicitExcluded" ? 1 : 0, plan.Decisions.Count);
        Guid resourceId = Guid.NewGuid();
        var fragment = compiler.BuildShaderMapFragment(translation, plan, [], resourceId, "EmptySubset");
        Assert.IsTrue(fragment.Success, string.Join("; ", fragment.Errors)); Assert.IsFalse(fragment.IsCompleteShaderSet);
        Assert.AreEqual(0, fragment.Shaders.Count); Assert.AreEqual(0, fragment.Map.Shaders.Count);
        Assert.AreEqual(0, fragment.Map.MeshShaderMaps.Length); Assert.AreEqual(resourceId, fragment.Map.ID);
        Assert.AreEqual(baseId, fragment.Map.StaticParameters.BaseMaterialId);
        Assert.AreNotSame(staticParameters.StaticSwitchParameters[0], fragment.Map.StaticParameters.StaticSwitchParameters[0]);
        CollectionAssert.AreEqual(translation.UniformPixelVectorExpressions.ToArray(), fragment.Map.UniformPixelVectorExpressions);
        CollectionAssert.AreEqual(translation.UniformPixelScalarExpressions.ToArray(), fragment.Map.UniformPixelScalarExpressions);
        CollectionAssert.AreEqual(translation.UniformVertexVectorExpressions.ToArray(), fragment.Map.UniformVertexVectorExpressions);
        CollectionAssert.AreEqual(translation.UniformVertexScalarExpressions.ToArray(), fragment.Map.UniformVertexScalarExpressions);
        CollectionAssert.AreEqual(translation.Uniform2DTextureExpressions.ToArray(), fragment.Map.Uniform2DTextureExpressions);
        CollectionAssert.AreEqual(translation.UniformCubeTextureExpressions.ToArray(), fragment.Map.UniformCubeTextureExpressions);
        Assert.AreNotSame(translation.UniformPixelScalarExpressions, fragment.Map.UniformPixelScalarExpressions);
        CollectionAssert.AreEqual(translation.UniformExpressionTextures.ToArray(), fragment.UniformExpressionTextures.ToArray());
        var cacheFragment = compiler.BuildShaderCacheFragment(translation, plan, [], resourceId, "EmptySubset", sources);
        Assert.IsTrue(cacheFragment.Success, string.Join("; ", cacheFragment.Errors)); Assert.IsFalse(cacheFragment.IsCompleteShaderSet);
        Assert.AreEqual(0, cacheFragment.Cache.Shaders.Count); Assert.AreEqual(0, cacheFragment.Cache.ShaderTypeCRCMap.Count);
        Assert.AreEqual(0, cacheFragment.Cache.VertexFactoryTypeCRCMap.Count);
        // No selected jobs means no shader/factory CRC source reads or native compiler call.
        var noSourceReads = compiler.BuildShaderCacheFragment(translation, plan, [], resourceId, "EmptySubset",
            new LE3ShaderSourceCrc(_ => throw new InvalidOperationException("An empty plan has no shader/factory sources.")));
        Assert.IsTrue(noSourceReads.Success, string.Join("; ", noSourceReads.Errors)); Assert.AreEqual(1, metadataReads);
        using var stream = new MemoryStream();
        cacheFragment.Cache.PublicSerialize(new SerializingContainer(stream, package));
        stream.Position = 0;
        var loaded = ShaderCache.Create();
        loaded.PublicSerialize(new SerializingContainer(stream, package, isLoading: true));
        Assert.AreEqual(stream.Length, stream.Position); Assert.AreEqual(0, loaded.Shaders.Count);
        Assert.AreEqual(0, loaded.ShaderTypeCRCMap.Count); Assert.AreEqual(0, loaded.VertexFactoryTypeCRCMap.Count);
        var pair = loaded.MaterialShaderMaps.Single();
        Assert.AreEqual(baseId, pair.Key.BaseMaterialId); Assert.AreEqual(baseId, pair.Value.StaticParameters.BaseMaterialId);
        Assert.AreEqual(resourceId, pair.Value.ID); Assert.AreEqual("EmptySubset", pair.Value.FriendlyName);
        Assert.AreEqual(0, pair.Value.Shaders.Count); Assert.AreEqual(0, pair.Value.MeshShaderMaps.Length);
        Assert.AreEqual(1, pair.Value.UniformPixelScalarExpressions.Length);
        Assert.AreEqual(1, pair.Value.UniformVertexScalarExpressions.Length);
        Assert.AreEqual(1, pair.Value.UniformPixelVectorExpressions.Length);
        Assert.AreEqual(1, pair.Value.Uniform2DTextureExpressions.Length);
        Assert.AreEqual(0, pair.Value.UniformCubeTextureExpressions.Length);
        Assert.IsFalse(pair.Value.StaticParameters.StaticSwitchParameters.Single().bOverride);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EmptyPlanCannotBorrowAnotherTranslationEvenOfTheSameCompilerInput(bool excluded)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3EmptyTranslationIdentity.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        var sources = Sources();
        var translation = compiler.TranslateMaterialHlsl(sources);
        var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), excluded ? [Candidate()] : [],
            materialResourceShouldCache: (_, _) => false);
        var other = compiler.TranslateMaterialHlsl(sources);
        Assert.AreSame(other.CompilationInput, plan.CompilationInput); Assert.AreSame(other.SourceSnapshot, translation.SourceSnapshot);
        Assert.ThrowsExactly<ArgumentException>(() => compiler.BuildShaderMapFragment(other, plan, [], Guid.NewGuid(), "WrongTranslation"));
        Assert.ThrowsExactly<ArgumentException>(() => compiler.BuildShaderCacheFragment(other, plan, [], Guid.NewGuid(), "WrongTranslation", sources));
    }

    [TestMethod]
    [DataRow("resourceUnknown")]
    [DataRow("environmentUnknown")]
    [DataRow("half")]
    [DataRow("registrationUnknown")]
    public void FailedZeroJobPlanDoesNotBecomeAnEmptySuccessfulFragment(string shape)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3EmptyFailedPlan.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        var translation = compiler.TranslateMaterialHlsl(Sources(), useHalfTypes: shape == "half");
        var candidate = Candidate();
        if (shape == "environmentUnknown") candidate = candidate with { IsCompilationEnvironmentResolved = false };
        if (shape == "registrationUnknown") candidate = candidate with { Target = candidate.Target with { ShaderTypeName = "UnknownNativeType" } };
        var plan = shape == "half" ? compiler.BuildObservedSubsetCompilePlan(translation, MaterialResource.Create())
            : compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), [candidate],
                materialResourceShouldCache: (_, _) => shape == "resourceUnknown" ? null : true);
        Assert.IsFalse(plan.Success); Assert.AreEqual(0, plan.Jobs.Count);
        var map = compiler.BuildShaderMapFragment(translation, plan, [], Guid.NewGuid(), "Failed");
        Assert.IsFalse(map.Success); Assert.IsNull(map.Map); Assert.IsFalse(map.IsCompleteShaderSet);
        var cache = compiler.BuildShaderCacheFragment(translation, plan, [], Guid.NewGuid(), "Failed", translation.SourceSnapshot);
        Assert.IsFalse(cache.Success); Assert.IsNull(cache.Cache); Assert.IsFalse(cache.IsCompleteShaderSet);
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("missingTarget")]
    [DataRow("unplannedTarget")]
    public void EmptyPlanRejectsEveryUnexpectedCompiledEntry(string shape)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3EmptyUnexpectedResult.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        var translation = compiler.TranslateMaterialHlsl(Sources());
        var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), []);
        ShaderCompilationResult extra = shape == "null" ? null : new();
        if (shape == "unplannedTarget")
            typeof(ShaderCompilationResult).GetProperty(nameof(ShaderCompilationResult.Target))!.SetValue(extra, Candidate().Target);
        var map = compiler.BuildShaderMapFragment(translation, plan, [extra], Guid.NewGuid(), "Unexpected");
        Assert.IsFalse(map.Success); Assert.IsNull(map.Map); Assert.AreEqual(0, map.Shaders.Count);
        var cache = compiler.BuildShaderCacheFragment(translation, plan, [extra], Guid.NewGuid(), "Unexpected",
            new LE3ShaderSourceCrc(_ => throw new InvalidOperationException("Reject results before reading sources.")));
        Assert.IsFalse(cache.Success); Assert.IsNull(cache.Cache);
    }

    [TestMethod]
    [DataRow("equivalentSnapshot")]
    [DataRow("directoryTranslation")]
    public void EmptyCacheStillRequiresExactTemplateSnapshotIdentity(string shape)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3EmptySnapshotIdentity.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        var sources = Sources();
        var translation = shape == "directoryTranslation" ? compiler.TranslateMaterialHlsl(Template()) : compiler.TranslateMaterialHlsl(sources);
        var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), []);
        var result = compiler.BuildShaderCacheFragment(translation, plan, [], Guid.NewGuid(), "WrongSnapshot",
            shape == "equivalentSnapshot" ? Sources() : sources);
        Assert.IsFalse(result.Success); Assert.IsNull(result.Cache); Assert.IsFalse(result.IsCompleteShaderSet);
        StringAssert.Contains(string.Join("; ", result.Errors), "same LE3 source snapshot");
    }

    [TestMethod]
    public void EmptyPlanCannotBeReusedByAnotherCompilerSharingResolvedInput()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3EmptyCompilerIdentity.pcc", MEGame.LE3);
        var input = new ResolvedMaterialCompilationInput(package.CreateExport("Material", "Material", indexed: false));
        var first = new LE3MaterialShaderCompiler(input); var second = new LE3MaterialShaderCompiler(input);
        var sources = Sources(); var translation = first.TranslateMaterialHlsl(sources);
        var plan = first.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), []);
        Assert.ThrowsExactly<ArgumentException>(() => second.BuildShaderMapFragment(translation, plan, [], Guid.NewGuid(), "OtherCompiler"));
        var otherTranslation = second.TranslateMaterialHlsl(sources);
        Assert.ThrowsExactly<ArgumentException>(() => second.BuildShaderMapFragment(otherTranslation, plan, [], Guid.NewGuid(), "OtherCompiler"));
    }

    [TestMethod]
    [DataRow("authoredDefault")]
    [DataRow("emptyResolvedBase")]
    [DataRow("nonemptyResolvedBase")]
    public void DetachedMapFillsOnlyAnEmptyBaseIdWithoutMutatingResolvedInput(string shape)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3MapBaseIdInitialization.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        Guid baseId = shape == "nonemptyResolvedBase" ? Guid.NewGuid() : Guid.Empty;
        var parameters = shape == "authoredDefault" ? null : (StaticParameterSet)baseId;
        if (parameters != null) parameters.StaticSwitchParameters = [new() { ParameterName = "InheritedSwitch", Value = true, bOverride = false }];
        var input = new ResolvedMaterialCompilationInput(material, parameters);
        var compiler = new LE3MaterialShaderCompiler(input);
        var sources = Sources(); var translation = compiler.TranslateMaterialHlsl(sources);
        var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), []);
        Guid resourceId = Guid.NewGuid();
        var result = compiler.BuildShaderCacheFragment(translation, plan, [], resourceId, "BaseIdInitialization", sources);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        var entry = result.Cache.MaterialShaderMaps.Single();
        Assert.AreEqual(resourceId, entry.Value.ID);
        Assert.AreEqual(baseId == Guid.Empty ? resourceId : baseId, entry.Key.BaseMaterialId);
        Assert.AreEqual(entry.Key.BaseMaterialId, entry.Value.StaticParameters.BaseMaterialId);
        if (parameters == null) Assert.IsNull(input.GetStaticParametersSnapshot());
        else
        {
            Assert.AreEqual(baseId, parameters.BaseMaterialId); Assert.AreEqual(baseId, input.GetStaticParametersSnapshot().BaseMaterialId);
            Assert.AreEqual("InheritedSwitch", entry.Key.StaticSwitchParameters.Single().ParameterName.Name);
            Assert.IsFalse(entry.Key.StaticSwitchParameters.Single().bOverride);
        }
        Assert.IsFalse(result.IsCompleteShaderSet);
    }

    private static ShaderCompileCandidate Candidate()
    {
        Assert.IsTrue(TryGetKnownPermutationEnvironment("TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", "FLocalVertexFactory", out var environment));
        return environment.CreateCandidate();
    }

    private static StructProperty Input(string name, int expression) => new("ExpressionInput",
        new PropertyCollection { new ObjectProperty(expression, "Expression") }, name);
    private static LE3ShaderSourceSnapshot Sources() => new(new Dictionary<string, string> { ["MaterialTemplate.usf"] = Template() });
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
}
