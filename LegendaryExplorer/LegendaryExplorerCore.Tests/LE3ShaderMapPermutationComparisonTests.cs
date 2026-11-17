using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3ShaderMapPermutationComparisonTests
{
    private const string VertexType = "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy";
    private const string PixelType = "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight";
    private const string Factory = "FLocalVertexFactory";

    [TestMethod]
    public void IndependentlyAuthoredMapMembershipCanMatchAPlanWithoutIdentityOrCompletenessClaims()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3MapComparison.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        material.WriteProperty(new BoolProperty(true, "bUsedAsLightFunction"));
        var compiler = new LE3MaterialShaderCompiler(material);
        TryGetKnownPermutationEnvironment(VertexType, Factory, out var vertex);
        TryGetKnownPermutationEnvironment(PixelType, Factory, out var pixel);
        TryGetKnownPermutationEnvironment("FLightFunctionVertexShader", null, out var lightFunction);
        var plan = compiler.BuildProvisionalCompilePlan(compiler.TranslateMaterialHlsl(Template()), MaterialResource.Create(),
            [vertex.CreateCandidate(), pixel.CreateCandidate(), lightFunction.CreateCandidate()]);
        Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors)); Assert.AreEqual(3, plan.Jobs.Count);
        // Literal reference names are not generated from plan.Jobs or another planner.
        var map = EmptyMap();
        map.ID = Guid.NewGuid(); map.StaticParameters = (StaticParameterSet)Guid.NewGuid();
        map.Shaders.Add("FLightFunctionVertexShader", Reference("FLightFunctionVertexShader"));
        map.MeshShaderMaps = [new() { VertexFactoryType = Factory, Shaders = new()
        {
            { PixelType, Reference(PixelType) }, { VertexType, Reference(VertexType) }
        } }];
        var comparison = ComparePlannedPermutationsWithShaderMap(plan, map);
        Assert.IsTrue(comparison.MatchesExpectedPermutations, string.Join("; ", comparison.Errors));
        Assert.AreEqual(3, comparison.ExpectedPermutationCount); Assert.AreEqual(3, comparison.PlannedPermutationCount);
        Assert.IsFalse(comparison.IsCompleteShaderSet, "Matching membership does not compare map identity or prove a native required set.");
        Assert.AreEqual(1, map.Shaders.Count); Assert.AreEqual(2, map.MeshShaderMaps.Single().Shaders.Count);
    }

    [TestMethod]
    [DataRow("unknownType")]
    [DataRow("unknownFactory")]
    [DataRow("nonmeshInsteadOfMesh")]
    [DataRow("numberedType")]
    [DataRow("differentCase")]
    public void UnknownAndDifferentlyGroupedReferencesRemainVisibleAsMissing(string shape)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3MapUnknownMembership.pcc", MEGame.LE3);
        var plan = LocalPlan(package);
        var map = EmptyMap();
        NameReference type = shape == "unknownType" ? "FutureNativeType" : VertexType;
        if (shape == "numberedType") type = new NameReference(VertexType, 2);
        if (shape == "differentCase") type = new NameReference(VertexType.ToUpperInvariant());
        var refs = new UMultiMap<NameReference, ShaderReference> { { type, new() { Id = Guid.NewGuid(), ShaderType = type } } };
        if (shape == "nonmeshInsteadOfMesh") map.Shaders = refs;
        else map.MeshShaderMaps = [new() { VertexFactoryType = shape == "unknownFactory" ? "FutureNativeFactory" : Factory, Shaders = refs }];
        var comparison = ComparePlannedPermutationsWithShaderMap(plan, map);
        Assert.IsFalse(comparison.MatchesExpectedPermutations); Assert.AreEqual(0, comparison.Errors.Count);
        Assert.AreEqual(1, comparison.MissingPermutations.Count); Assert.AreEqual(1, comparison.UnexpectedPermutations.Count);
        Assert.AreEqual(type.Instanced, comparison.MissingPermutations.Single().ShaderTypeName);
        Assert.AreEqual(shape == "nonmeshInsteadOfMesh" ? null : shape == "unknownFactory" ? "FutureNativeFactory" : Factory,
            comparison.MissingPermutations.Single().VertexFactoryTypeName);
        Assert.AreEqual(new ShaderPermutationIdentity(VertexType, Factory), comparison.UnexpectedPermutations.Single());
    }

    [TestMethod]
    [DataRow("nullNonmesh")]
    [DataRow("nullMeshArray")]
    [DataRow("nullMesh")]
    [DataRow("nullMeshRefs")]
    [DataRow("emptyFactory")]
    [DataRow("nullReference")]
    [DataRow("emptyReferenceId")]
    [DataRow("conflictingReferenceType")]
    [DataRow("emptyType")]
    [DataRow("nullNumberedFactory")]
    [DataRow("nullNumberedReferenceType")]
    [DataRow("negativeTypeNumber")]
    [DataRow("duplicateType")]
    [DataRow("duplicateFactory")]
    public void MalformedMapCannotBeReportedAsAnExactMatch(string shape)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3MalformedMapInventory.pcc", MEGame.LE3);
        var plan = LocalPlan(package);
        var map = EmptyMap();
        var refs = new UMultiMap<NameReference, ShaderReference> { { VertexType, Reference(VertexType) } };
        map.MeshShaderMaps = [new() { VertexFactoryType = Factory, Shaders = refs }];
        switch (shape)
        {
            case "nullNonmesh": map.Shaders = null; break;
            case "nullMeshArray": map.MeshShaderMaps = null; break;
            case "nullMesh": map.MeshShaderMaps = [null]; break;
            case "nullMeshRefs": map.MeshShaderMaps[0].Shaders = null; break;
            case "emptyFactory": map.MeshShaderMaps[0].VertexFactoryType = " "; break;
            case "nullReference": refs.Clear(); refs.Add(VertexType, null); break;
            case "emptyReferenceId": refs.First().Value.Id = Guid.Empty; break;
            case "conflictingReferenceType": refs.First().Value.ShaderType = PixelType; break;
            case "emptyType": refs.Clear(); refs.Add(" ", Reference(" ")); break;
            case "nullNumberedFactory": map.MeshShaderMaps[0].VertexFactoryType = new NameReference(null, 2); break;
            case "nullNumberedReferenceType": refs.First().Value.ShaderType = new NameReference(null, 2); break;
            case "negativeTypeNumber": refs.Clear(); refs.Add(new NameReference(VertexType, -1), Reference(VertexType)); break;
            case "duplicateType": refs.Add(VertexType, Reference(VertexType)); break;
            case "duplicateFactory": map.MeshShaderMaps = [map.MeshShaderMaps[0], new() { VertexFactoryType = Factory, Shaders = new() }]; break;
        }
        var comparison = ComparePlannedPermutationsWithShaderMap(plan, map);
        Assert.IsTrue(comparison.PlanSucceeded); Assert.IsFalse(comparison.MatchesExpectedPermutations);
        Assert.IsTrue(comparison.Errors.Count > 0, shape); Assert.IsFalse(comparison.IsCompleteShaderSet);
    }

    [TestMethod]
    public void EmptyMapsAndEmptyFactoryGroupsAreInventoriesNotCompletenessCertificates()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3EmptyMapComparison.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        var plan = compiler.BuildProvisionalCompilePlan(compiler.TranslateMaterialHlsl(Template()), MaterialResource.Create(), []);
        var map = EmptyMap();
        map.MeshShaderMaps = [new() { VertexFactoryType = "FutureNativeFactory", Shaders = new() }];
        var comparison = ComparePlannedPermutationsWithShaderMap(plan, map);
        Assert.IsTrue(comparison.MatchesExpectedPermutations); Assert.AreEqual(0, comparison.ExpectedPermutationCount);
        Assert.IsFalse(comparison.IsCompleteShaderSet);
        Assert.ThrowsExactly<ArgumentNullException>(() => ComparePlannedPermutationsWithShaderMap(null, map));
        Assert.ThrowsExactly<ArgumentNullException>(() => ComparePlannedPermutationsWithShaderMap(plan, null));
    }

    [TestMethod]
    public void FailedPlanCannotMatchEvenAnEmptyMap()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FailedMapComparison.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        TryGetKnownPermutationEnvironment(VertexType, Factory, out var environment);
        var plan = compiler.BuildProvisionalCompilePlan(compiler.TranslateMaterialHlsl(Template()), MaterialResource.Create(),
            [environment.CreateCandidate() with { IsCompilationEnvironmentResolved = false }]);
        var result = ComparePlannedPermutationsWithShaderMap(plan, EmptyMap());
        Assert.IsFalse(result.PlanSucceeded); Assert.IsFalse(result.MatchesExpectedPermutations);
        Assert.IsTrue(result.Errors.Count > 0); Assert.IsFalse(result.IsCompleteShaderSet);
    }

    private static ShaderCompilePlan LocalPlan(IMEPackage package)
    {
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        TryGetKnownPermutationEnvironment(VertexType, Factory, out var environment);
        var plan = compiler.BuildProvisionalCompilePlan(compiler.TranslateMaterialHlsl(Template()), MaterialResource.Create(), [environment.CreateCandidate()]);
        Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors)); Assert.AreEqual(1, plan.Jobs.Count);
        return plan;
    }
    private static ShaderReference Reference(string type) => new() { Id = Guid.NewGuid(), ShaderType = type };
    private static MaterialShaderMap EmptyMap() => new() { Shaders = new(), MeshShaderMaps = [] };
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
}
