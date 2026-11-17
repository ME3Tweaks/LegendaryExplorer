using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3MaterialUsageAdvisorTests
{
    [TestMethod]
    public void EveryUsageFlagIsAnLE3MaterialProperty()
    {
        GlobalTest.Init();
        var properties = GlobalUnrealObjectInfo.GetAllProperties(MEGame.LE3, "Material");
        foreach (var usage in Enum.GetValues<LE3MaterialUsage>())
        {
            string name = LE3MaterialUsageAdvisor.GetFlagName(usage);
            Assert.IsTrue(properties.ContainsKey(name), $"{usage}: {name} is not an LE3 Material property");
        }
        Assert.IsTrue(properties.ContainsKey("bUsedAsSpecialEngineMaterial"));
    }

    [TestMethod]
    public void ReportsOnlyTheClearFlags()
    {
        var props = new PropertyCollection { new BoolProperty(true, "bUsedWithStaticLighting"), new BoolProperty(false, "bUsedWithSkeletalMesh") };
        var report = LE3MaterialUsageAdvisor.Check(props,
            [LE3MaterialUsage.SkeletalMesh, .. LE3MaterialUsageAdvisor.LightmappedStaticMesh(simpleLightMaps: false), LE3MaterialUsage.SkeletalMesh]);
        CollectionAssert.AreEqual(new[] { "bUsedWithSkeletalMesh", "bUsedWithVectorLightMaps" }, report.MissingFlags.ToArray());
        Assert.AreEqual(0, report.Vetoed.Count);
        Assert.IsFalse(report.IsReady);
    }

    [DataTestMethod]
    [DataRow("bUsedWithDecals", LE3MaterialUsage.SkeletalMesh, true)]
    [DataRow("bUsedWithDecals", LE3MaterialUsage.SplineMesh, true)]
    [DataRow("bUsedWithDecals", LE3MaterialUsage.FogVolumes, true)]
    [DataRow("bUsedWithDecals", LE3MaterialUsage.Decals, false)]
    [DataRow("bUsedWithDecals", LE3MaterialUsage.StaticLighting, false)]
    [DataRow("bUsedWithDecals", LE3MaterialUsage.ScreenDoorFade, false)]
    [DataRow("bUsedWithFogVolumes", LE3MaterialUsage.Decals, true)]
    [DataRow("bUsedWithFogVolumes", LE3MaterialUsage.ParticleSprites, true)]
    [DataRow("bUsedWithFogVolumes", LE3MaterialUsage.FogVolumes, false)]
    [DataRow("bUsedWithFogVolumes", LE3MaterialUsage.MaterialEffect, false)]
    public void ExclusiveUseMaterialsVetoOtherUses(string exclusiveFlag, LE3MaterialUsage usage, bool vetoed)
    {
        // Special engine status cannot override a veto; it only accepts clear flags.
        var props = new PropertyCollection { new BoolProperty(true, exclusiveFlag), new BoolProperty(true, "bUsedAsSpecialEngineMaterial") };
        var report = LE3MaterialUsageAdvisor.Check(props, [usage]);
        Assert.AreEqual(vetoed, report.Vetoed.Contains(usage));
        Assert.AreEqual(0, report.MissingFlags.Count);
    }

    [TestMethod]
    public void DecalMaterialIsUsedWithDecalsByClassDefault()
    {
        var report = LE3MaterialUsageAdvisor.Check([], [LE3MaterialUsage.Decals, LE3MaterialUsage.SkeletalMesh], "DecalMaterial");
        Assert.AreEqual(0, report.MissingFlags.Count);
        CollectionAssert.AreEqual(new[] { LE3MaterialUsage.SkeletalMesh }, report.Vetoed.ToArray());
        // An explicit false still wins over the class default.
        var cleared = LE3MaterialUsageAdvisor.Check([new BoolProperty(false, "bUsedWithDecals")], [LE3MaterialUsage.Decals], "DecalMaterial");
        CollectionAssert.AreEqual(new[] { "bUsedWithDecals" }, cleared.MissingFlags.ToArray());
    }

    [TestMethod]
    public void SpecialEngineMaterialAcceptsClearFlags()
    {
        var props = new PropertyCollection { new BoolProperty(true, "bUsedAsSpecialEngineMaterial") };
        var report = LE3MaterialUsageAdvisor.Check(props, Enum.GetValues<LE3MaterialUsage>());
        Assert.IsTrue(report.IsSpecialEngineMaterial);
        Assert.IsTrue(report.IsReady);
    }
}
