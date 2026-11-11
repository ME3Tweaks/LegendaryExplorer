using System;
using System.Linq;
using LegendaryExplorer.Tools.MaterialShaderCompilation;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using F = LegendaryExplorer.Tests.Tools.MaterialShaderCompilation.LE3InstanceFixtures;

namespace LegendaryExplorer.Tests.Tools.MaterialShaderCompilation;

[TestClass]
public class LE3MaterialInstanceResolverTests
{
    [TestInitialize]
    public void Setup() => F.InitLib();

    private static LE3MaterialInstanceResolver Resolver(Func<ImportEntry, ExportEntry> resolveImport = null) => new(resolveImport);

    private static Guid ResourceIdOf(ExportEntry material) => ObjectBinary.From<LegendaryExplorerCore.Unreal.BinaryConverters.Material>(material).SM3MaterialResource.ID;

    private static IEntry[] ChainOf(MaterialInstanceResolution result) => result.Chain.Cast<IEntry>().ToArray();

    #region Binary fixture sanity

    [TestMethod]
    public void Fixture_MaterialInstanceBinary_RoundTrips()
    {
        using var package = F.NewPackage();
        var baseId = Guid.NewGuid();
        var permId = Guid.NewGuid();
        var set = F.StaticSet(baseId, "Roundtrip", true);
        var material = F.BaseMaterial(package, resourceId: baseId);
        var mic = F.Instance(package, "Mic", material, hasPermutationFlag: true, permutationId: permId, staticParameters: set);

        var read = ObjectBinary.From<MaterialInstance>(mic);

        Assert.IsNotNull(read);
        Assert.AreEqual(permId, read.SM3StaticPermutationResource.ID);
        Assert.AreEqual(baseId, read.SM3StaticParameterSet.BaseMaterialId);
        Assert.AreEqual(1, read.SM3StaticParameterSet.StaticSwitchParameters.Length);
        Assert.AreEqual("Roundtrip", read.SM3StaticParameterSet.StaticSwitchParameters[0].ParameterName.Instanced);
        Assert.IsTrue(read.SM3StaticParameterSet.StaticSwitchParameters[0].Value);
        Assert.IsTrue(set.Equals(read.SM3StaticParameterSet));
        var props = mic.GetProperties();
        Assert.IsTrue(props.GetProp<BoolProperty>("bHasStaticPermutationResource").Value);
        Assert.AreSame(material, package.GetEntry(props.GetProp<ObjectProperty>("Parent").Value));
    }

    [TestMethod]
    public void Fixture_MaterialBinary_RoundTrips()
    {
        using var package = F.NewPackage();
        var id = Guid.NewGuid();
        var material = F.BaseMaterial(package, resourceId: id);
        Assert.AreEqual(id, ResourceIdOf(material));
    }

    #endregion

    #region Rejections before walking

    [TestMethod]
    public void NullInstance_IsErrorNotUnsupported()
    {
        var result = Resolver().Resolve(null);
        Assert.IsFalse(result.Success);
        Assert.IsFalse(result.IsUnsupported);
        Assert.AreEqual(1, result.Errors.Count);
        Assert.IsNull(result.RequestedInstance);
        Assert.IsNull(result.BaseMaterial);
        Assert.IsFalse(result.SharesAncestorPermutation);
    }

    [TestMethod]
    [DataRow(MEGame.ME3)]
    [DataRow(MEGame.LE2)]
    [DataRow(MEGame.LE1)]
    public void NonLE3Instance_IsUnsupported(MEGame game)
    {
        using var package = F.NewPackage("Other.pcc", game);
        var mic = package.CreateExport("Mic", "MaterialInstanceConstant", indexed: false);

        var result = Resolver().Resolve(mic);

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.IsUnsupported);
        Assert.AreSame(mic, result.RequestedInstance);
    }

    [TestMethod]
    [DataRow("Material")]
    [DataRow("DecalMaterial")]
    [DataRow("Texture2D")]
    public void NonInstanceExport_IsUnsupported(string className)
    {
        using var package = F.NewPackage();
        var export = package.CreateExport("NotAnInstance", className, indexed: false);

        var result = Resolver().Resolve(export);

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.IsUnsupported);
        StringAssert.Contains(result.Errors.Single(), "MaterialInstance");
    }

    #endregion

    #region Successful resolution and ownership

    [TestMethod]
    public void NoOwner_UsesBaseMaterialResource_AndSharesPermutation()
    {
        using var package = F.NewPackage();
        var baseId = Guid.NewGuid();
        var material = F.BaseMaterial(package, resourceId: baseId);
        var mic = F.Instance(package, "Mic", material);

        var result = Resolver().Resolve(mic);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.IsFalse(result.IsUnsupported);
        Assert.AreSame(mic, result.RequestedInstance);
        Assert.AreSame(material, result.BaseMaterial);
        Assert.IsNull(result.PermutationOwner);
        Assert.IsNull(result.EffectiveStaticParameters);
        Assert.IsNotNull(result.PermutationResource);
        Assert.AreEqual(baseId, result.PermutationResource.ID);
        Assert.IsTrue(result.SharesAncestorPermutation);
        Assert.AreEqual(0, result.Warnings.Count);
        CollectionAssert.AreEqual(new IEntry[] { mic, material }, ChainOf(result));
    }

    [TestMethod]
    public void RequestedInstanceOwnsPermutation_UsesItsSerializedSetAndResource()
    {
        using var package = F.NewPackage();
        var baseId = Guid.NewGuid();
        var permId = Guid.NewGuid();
        var material = F.BaseMaterial(package, resourceId: baseId);
        var set = F.StaticSet(baseId, "Sw", true);
        var mic = F.Instance(package, "Mic", material, hasPermutationFlag: true, permutationId: permId, staticParameters: set);

        var result = Resolver().Resolve(mic);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreSame(mic, result.PermutationOwner);
        Assert.AreEqual(permId, result.PermutationResource.ID);
        Assert.AreNotEqual(baseId, result.PermutationResource.ID);
        Assert.IsNotNull(result.EffectiveStaticParameters);
        Assert.IsTrue(set.Equals(result.EffectiveStaticParameters));
        Assert.AreEqual(baseId, result.EffectiveStaticParameters.BaseMaterialId);
        Assert.IsFalse(result.SharesAncestorPermutation);
        Assert.AreEqual(0, result.Warnings.Count);
    }

    [TestMethod]
    public void ParentOwnsPermutation_RequestedSharesAncestorPermutation()
    {
        using var package = F.NewPackage();
        var baseId = Guid.NewGuid();
        var permId = Guid.NewGuid();
        var material = F.BaseMaterial(package, resourceId: baseId);
        var set = F.StaticSet(baseId, "ParentSw", false);
        var parent = F.Instance(package, "Parent", material, hasPermutationFlag: true, permutationId: permId, staticParameters: set);
        var child = F.Instance(package, "Child", parent);

        var result = Resolver().Resolve(child);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreSame(parent, result.PermutationOwner);
        Assert.AreEqual(permId, result.PermutationResource.ID);
        Assert.IsTrue(set.Equals(result.EffectiveStaticParameters));
        Assert.IsTrue(result.SharesAncestorPermutation);
        CollectionAssert.AreEqual(new IEntry[] { child, parent, material }, ChainOf(result));
        Assert.AreSame(material, result.BaseMaterial);
        Assert.AreSame(child, result.RequestedInstance);
    }

    [TestMethod]
    public void NearestFlaggedInstanceWins_RequestedBeatsAncestors()
    {
        using var package = F.NewPackage();
        var baseId = Guid.NewGuid();
        var material = F.BaseMaterial(package, resourceId: baseId);
        var grand = F.Instance(package, "Grand", material, true, Guid.NewGuid(), F.StaticSet(baseId, "Grand"));
        var midPerm = Guid.NewGuid();
        var mid = F.Instance(package, "Mid", grand, true, midPerm, F.StaticSet(baseId, "Mid"));
        var topPerm = Guid.NewGuid();
        var top = F.Instance(package, "Top", mid, true, topPerm, F.StaticSet(baseId, "Top"));

        var fromTop = Resolver().Resolve(top);
        var fromMid = Resolver().Resolve(mid);

        Assert.AreSame(top, fromTop.PermutationOwner);
        Assert.AreEqual(topPerm, fromTop.PermutationResource.ID);
        Assert.AreEqual("Top", fromTop.EffectiveStaticParameters.StaticSwitchParameters.Single().ParameterName.Instanced);
        Assert.IsFalse(fromTop.SharesAncestorPermutation);
        CollectionAssert.AreEqual(new IEntry[] { top, mid, grand, material }, ChainOf(fromTop));

        Assert.AreSame(mid, fromMid.PermutationOwner);
        Assert.AreEqual(midPerm, fromMid.PermutationResource.ID);
        Assert.AreEqual("Mid", fromMid.EffectiveStaticParameters.StaticSwitchParameters.Single().ParameterName.Instanced);
        Assert.IsFalse(fromMid.SharesAncestorPermutation);
    }

    [TestMethod]
    public void NearestOwnerIsFirstFlaggedWalkingUp_NotAnUnflaggedInstanceWithBinary()
    {
        using var package = F.NewPackage();
        var baseId = Guid.NewGuid();
        var material = F.BaseMaterial(package, resourceId: baseId);
        var grandPerm = Guid.NewGuid();
        var grand = F.Instance(package, "Grand", material, true, grandPerm, F.StaticSet(baseId, "Grand"));
        // Serialized permutation but the flag is not set: not an owner.
        var mid = F.Instance(package, "Mid", grand, false, Guid.NewGuid(), F.StaticSet(baseId, "Mid"));
        var top = F.Instance(package, "Top", mid);

        var result = Resolver().Resolve(top);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreSame(grand, result.PermutationOwner);
        Assert.AreEqual(grandPerm, result.PermutationResource.ID);
        Assert.IsTrue(result.SharesAncestorPermutation);
    }

    [TestMethod]
    public void UnflaggedInstanceWithSerializedPermutation_IsNotAnOwner()
    {
        using var package = F.NewPackage();
        var baseId = Guid.NewGuid();
        var material = F.BaseMaterial(package, resourceId: baseId);
        var mic = F.Instance(package, "Mic", material, false, Guid.NewGuid(), F.StaticSet(baseId));

        var result = Resolver().Resolve(mic);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.PermutationOwner);
        Assert.IsNull(result.EffectiveStaticParameters);
        Assert.AreEqual(baseId, result.PermutationResource.ID);
    }

    [TestMethod]
    public void ExplicitFalseFlag_IsNotAnOwner()
    {
        using var package = F.NewPackage();
        var material = F.BaseMaterial(package);
        var mic = F.Instance(package, "Mic", material);
        var props = mic.GetProperties();
        props.AddOrReplaceProp(new BoolProperty(false, "bHasStaticPermutationResource"));
        mic.WriteProperties(props);

        var result = Resolver().Resolve(mic);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.PermutationOwner);
    }

    [TestMethod]
    public void DecalMaterialBase_IsAccepted()
    {
        using var package = F.NewPackage();
        var material = F.BaseMaterial(package, className: "DecalMaterial");
        var mic = F.Instance(package, "Mic", material);

        var result = Resolver().Resolve(mic);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreSame(material, result.BaseMaterial);
    }

    [TestMethod]
    public void MaterialInstanceTimeVarying_IsTreatedAsAnInstance()
    {
        using var package = F.NewPackage();
        var material = F.BaseMaterial(package);
        var mitv = F.Instance(package, "Mitv", material, className: "MaterialInstanceTimeVarying");

        var result = Resolver().Resolve(mitv);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreSame(material, result.BaseMaterial);
    }

    [TestMethod]
    public void Resolve_DoesNotMutateExports()
    {
        using var package = F.NewPackage();
        var baseId = Guid.NewGuid();
        var material = F.BaseMaterial(package, resourceId: baseId);
        var mic = F.Instance(package, "Mic", material, true, Guid.NewGuid(), F.StaticSet(baseId));
        var micData = mic.Data.ToArray();
        var materialData = material.Data.ToArray();
        var modifiedBefore = package.IsModified;

        Resolver().Resolve(mic);

        CollectionAssert.AreEqual(micData, mic.Data);
        CollectionAssert.AreEqual(materialData, material.Data);
        Assert.AreEqual(modifiedBefore, package.IsModified);
    }

    #endregion

    #region Parent problems

    [TestMethod]
    public void NoParent_IsUnsupported()
    {
        using var package = F.NewPackage();
        var mic = F.Instance(package, "Mic", null);

        var result = Resolver().Resolve(mic);

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.IsUnsupported);
        CollectionAssert.AreEqual(new IEntry[] { mic }, ChainOf(result));
    }

    [TestMethod]
    public void AncestorWithoutParent_IsUnsupported_WithPartialChain()
    {
        using var package = F.NewPackage();
        var orphan = F.Instance(package, "Orphan", null);
        var child = F.Instance(package, "Child", orphan);

        var result = Resolver().Resolve(child);

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.IsUnsupported);
        CollectionAssert.AreEqual(new IEntry[] { child, orphan }, ChainOf(result));
        Assert.IsNull(result.BaseMaterial);
        Assert.IsFalse(result.SharesAncestorPermutation);
    }

    [TestMethod]
    [DataRow(9999)]
    [DataRow(-9999)]
    public void InvalidParentIndex_IsErrorNotUnsupported(int index)
    {
        using var package = F.NewPackage();
        var mic = F.Instance(package, "Mic", null, rawParentIndex: index);

        var result = Resolver().Resolve(mic);

        Assert.IsFalse(result.Success);
        Assert.IsFalse(result.IsUnsupported);
        StringAssert.Contains(result.Errors.Single(), index.ToString());
    }

    [TestMethod]
    public void TwoInstanceCycle_IsErrorNotUnsupported()
    {
        using var package = F.NewPackage();
        var a = F.Instance(package, "A", null);
        var b = F.Instance(package, "B", a);
        F.SetParent(a, b);

        var result = Resolver().Resolve(a);

        Assert.IsFalse(result.Success);
        Assert.IsFalse(result.IsUnsupported);
        Assert.AreEqual(1, result.Errors.Count);
        CollectionAssert.AreEqual(new IEntry[] { a, b }, ChainOf(result));
    }

    [TestMethod]
    public void SelfParent_IsErrorNotUnsupported()
    {
        using var package = F.NewPackage();
        var a = F.Instance(package, "A", null);
        F.SetParent(a, a);

        var result = Resolver().Resolve(a);

        Assert.IsFalse(result.Success);
        Assert.IsFalse(result.IsUnsupported);
    }

    [TestMethod]
    [DataRow("Texture2D")]
    [DataRow("StaticMesh")]
    public void NonMaterialParent_IsUnsupported(string parentClass)
    {
        using var package = F.NewPackage();
        var parent = package.CreateExport("NotAMaterial", parentClass, indexed: false);
        var mic = F.Instance(package, "Mic", parent);

        var result = Resolver().Resolve(mic);

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.IsUnsupported);
        CollectionAssert.AreEqual(new IEntry[] { mic, parent }, ChainOf(result));
    }

    #endregion

    #region Imported parents

    private static ImportEntry ImportOf(IMEPackage package, string className, string packageName, string objectName)
    {
        var pkgImport = package.CreatePackageImport(packageName);
        return (ImportEntry)package.CreateImport(className, objectName, pkgImport);
    }

    private static ExportEntry ExportIn(IMEPackage package, string className, string packageName, string objectName, Guid? resourceId = null)
    {
        var pkgExport = package.CreatePackageExport(packageName);
        var export = package.CreateExport(objectName, className, pkgExport, indexed: false);
        if (className is "Material" or "DecalMaterial")
        {
            var binary = LegendaryExplorerCore.Unreal.BinaryConverters.Material.Create();
            binary.SM3MaterialResource.ID = resourceId ?? Guid.NewGuid();
            export.WriteBinary(binary);
        }
        return export;
    }

    [TestMethod]
    public void ImportedParent_WithoutCallback_IsUnsupported()
    {
        using var package = F.NewPackage();
        var import = ImportOf(package, "Material", "Other", "Base");
        var mic = F.Instance(package, "Mic", import);

        var result = Resolver().Resolve(mic);

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.IsUnsupported);
        CollectionAssert.AreEqual(new IEntry[] { mic }, ChainOf(result));
        StringAssert.Contains(result.Errors.Single(), "Other.Base");
    }

    [TestMethod]
    public void ImportedParent_WithCallback_UsesReturnedExportFromAnotherPackage()
    {
        using var package = F.NewPackage("Child.pcc");
        using var other = F.NewPackage("Other.pcc");
        var baseId = Guid.NewGuid();
        var otherBase = ExportIn(other, "Material", "Other", "Base", baseId);
        var import = ImportOf(package, "Material", "Other", "Base");
        var mic = F.Instance(package, "Mic", import);
        ImportEntry seen = null;
        int calls = 0;

        var result = Resolver(i => { calls++; seen = i; return otherBase; }).Resolve(mic);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(1, calls);
        Assert.AreSame(import, seen);
        Assert.AreSame(otherBase, result.BaseMaterial);
        Assert.AreSame(other, result.BaseMaterial.FileRef);
        Assert.IsNull(result.PermutationOwner);
        Assert.AreEqual(baseId, result.PermutationResource.ID);
        CollectionAssert.AreEqual(new IEntry[] { mic, otherBase }, ChainOf(result));
        Assert.IsTrue(result.SharesAncestorPermutation);
    }

    [TestMethod]
    public void ImportedParentInstance_ContinuesWalkInOtherPackage_AndUsesItsOwner()
    {
        using var package = F.NewPackage("Child.pcc");
        using var other = F.NewPackage("Other.pcc");
        var baseId = Guid.NewGuid();
        var permId = Guid.NewGuid();
        var otherPkg = other.CreatePackageExport("Other");
        var otherBase = ExportIn(other, "Material", "Other", "Base", baseId);
        var otherMic = other.CreateExport("ParentMic", "MaterialInstanceConstant", otherPkg, indexed: false);
        F.WriteInstanceProperties(otherMic, otherBase, true);
        var binary = MaterialInstance.Create();
        binary.SM3StaticPermutationResource.ID = permId;
        binary.SM3StaticParameterSet = F.StaticSet(baseId, "Remote");
        otherMic.WriteBinary(binary);

        var import = ImportOf(package, "MaterialInstanceConstant", "Other", "ParentMic");
        var mic = F.Instance(package, "Mic", import);

        var result = Resolver(_ => otherMic).Resolve(mic);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreSame(otherMic, result.PermutationOwner);
        Assert.AreEqual(permId, result.PermutationResource.ID);
        Assert.AreEqual("Remote", result.EffectiveStaticParameters.StaticSwitchParameters.Single().ParameterName.Instanced);
        Assert.AreSame(otherBase, result.BaseMaterial);
        CollectionAssert.AreEqual(new IEntry[] { mic, otherMic, otherBase }, ChainOf(result));
        Assert.IsTrue(result.SharesAncestorPermutation);
        Assert.AreEqual(0, result.Warnings.Count);
    }

    [TestMethod]
    public void ImportedParent_CallbackReturnsNull_IsUnsupported()
    {
        using var package = F.NewPackage();
        var import = ImportOf(package, "Material", "Other", "Base");
        var mic = F.Instance(package, "Mic", import);

        var result = Resolver(_ => null).Resolve(mic);

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.IsUnsupported);
    }

    [TestMethod]
    public void ImportedParent_CallbackReturnsDifferentPath_IsErrorNotUnsupported()
    {
        using var package = F.NewPackage("Child.pcc");
        using var other = F.NewPackage("Other.pcc");
        var wrong = ExportIn(other, "Material", "Other", "SomethingElse");
        var import = ImportOf(package, "Material", "Other", "Base");
        var mic = F.Instance(package, "Mic", import);

        var result = Resolver(_ => wrong).Resolve(mic);

        Assert.IsFalse(result.Success);
        Assert.IsFalse(result.IsUnsupported);
        StringAssert.Contains(result.Errors.Single(), "Other.Base");
    }

    [TestMethod]
    public void ImportedParent_CallbackReturnsNonLE3Export_IsError()
    {
        using var package = F.NewPackage("Child.pcc");
        using var other = F.NewPackage("Other.pcc", MEGame.ME3);
        var wrong = ExportIn(other, "Material", "Other", "Base");
        var import = ImportOf(package, "Material", "Other", "Base");
        var mic = F.Instance(package, "Mic", import);

        var result = Resolver(_ => wrong).Resolve(mic);

        Assert.IsFalse(result.Success);
        Assert.IsFalse(result.IsUnsupported);
    }

    [TestMethod]
    public void ImportedParent_PathComparisonIgnoresCase()
    {
        using var package = F.NewPackage("Child.pcc");
        using var other = F.NewPackage("Other.pcc");
        var otherBase = ExportIn(other, "Material", "other", "base");
        var import = ImportOf(package, "Material", "Other", "Base");
        var mic = F.Instance(package, "Mic", import);

        var result = Resolver(_ => otherBase).Resolve(mic);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
    }

    #endregion

    #region Owner without a serialized permutation

    private static MaterialInstanceResolution ResolveNoThrow(ExportEntry export)
    {
        try { return Resolver().Resolve(export); }
        catch (Exception e) { Assert.Fail($"Resolve threw {e.GetType().Name}: {e.Message}"); return null; }
    }

    [TestMethod]
    public void FlaggedOwnerWithNoBinary_IsErrorNotUnsupported()
    {
        using var package = F.NewPackage();
        var material = F.BaseMaterial(package);
        var mic = F.Instance(package, "Mic", material, hasPermutationFlag: true, writeBinary: false);

        var result = ResolveNoThrow(mic);

        Assert.IsFalse(result.Success);
        Assert.IsFalse(result.IsUnsupported);
        StringAssert.Contains(result.Errors.Single(), "bHasStaticPermutationResource");
    }

    [TestMethod]
    public void FlaggedOwnerWithEmptyBinary_IsErrorNotUnsupported()
    {
        using var package = F.NewPackage();
        var material = F.BaseMaterial(package);
        var mic = F.Instance(package, "Mic", material, hasPermutationFlag: true, writeBinary: false);
        mic.WriteBinary(Array.Empty<byte>());

        var result = ResolveNoThrow(mic);

        Assert.IsFalse(result.Success);
        Assert.IsFalse(result.IsUnsupported);
    }

    [TestMethod]
    public void FlaggedAncestorWithNoBinary_IsErrorEvenWhenRequestedIsPlain()
    {
        using var package = F.NewPackage();
        var material = F.BaseMaterial(package);
        var parent = F.Instance(package, "Parent", material, hasPermutationFlag: true, writeBinary: false);
        var child = F.Instance(package, "Child", parent);

        var result = ResolveNoThrow(child);

        Assert.IsFalse(result.Success);
        Assert.IsFalse(result.IsUnsupported);
    }

    #endregion

    #region BaseMaterialId warnings

    [TestMethod]
    public void BaseMaterialIdMismatch_SucceedsWithWarning()
    {
        using var package = F.NewPackage();
        var baseId = Guid.NewGuid();
        var staleId = Guid.NewGuid();
        var material = F.BaseMaterial(package, resourceId: baseId);
        var mic = F.Instance(package, "Mic", material, true, Guid.NewGuid(), F.StaticSet(staleId));

        var result = Resolver().Resolve(mic);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(1, result.Warnings.Count);
        StringAssert.Contains(result.Warnings[0], staleId.ToString());
        StringAssert.Contains(result.Warnings[0], baseId.ToString());
        Assert.AreSame(mic, result.PermutationOwner);
        Assert.AreEqual(staleId, result.EffectiveStaticParameters.BaseMaterialId, "The serialized set is used as-is, not rewritten.");
    }

    [TestMethod]
    public void MatchingBaseMaterialId_HasNoWarning()
    {
        using var package = F.NewPackage();
        var baseId = Guid.NewGuid();
        var material = F.BaseMaterial(package, resourceId: baseId);
        var mic = F.Instance(package, "Mic", material, true, Guid.NewGuid(), F.StaticSet(baseId));

        Assert.AreEqual(0, Resolver().Resolve(mic).Warnings.Count);
    }

    [TestMethod]
    public void EmptyBaseMaterialId_HasNoWarning()
    {
        using var package = F.NewPackage();
        var material = F.BaseMaterial(package, resourceId: Guid.NewGuid());
        var mic = F.Instance(package, "Mic", material, true, Guid.NewGuid(), F.StaticSet(Guid.Empty));

        var result = Resolver().Resolve(mic);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.Warnings.Count);
    }

    [TestMethod]
    public void BaseMaterialIdMismatch_OnAncestorOwner_WarnsForRequestedChild()
    {
        using var package = F.NewPackage();
        var material = F.BaseMaterial(package, resourceId: Guid.NewGuid());
        var parent = F.Instance(package, "Parent", material, true, Guid.NewGuid(), F.StaticSet(Guid.NewGuid()));
        var child = F.Instance(package, "Child", parent);

        var result = Resolver().Resolve(child);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.Warnings.Count);
        StringAssert.Contains(result.Warnings[0], "Parent");
    }

    [TestMethod]
    public void NonOwnerInstance_DoesNotWarn()
    {
        using var package = F.NewPackage();
        var material = F.BaseMaterial(package, resourceId: Guid.NewGuid());
        // A mismatching set on an unflagged instance is never consulted.
        var plain = F.Instance(package, "Plain", material, false, Guid.NewGuid(), F.StaticSet(Guid.NewGuid()));

        Assert.AreEqual(0, Resolver().Resolve(plain).Warnings.Count);
    }

    #endregion

    [TestMethod]
    public void Resolver_ImplementsServiceInterface() =>
        Assert.IsInstanceOfType<IMaterialShaderInstanceResolver>(new LE3MaterialInstanceResolver());
}
