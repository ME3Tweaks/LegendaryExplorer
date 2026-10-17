using System;
using System.Linq;
using System.Reflection;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UDK;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class UdkInterpActorLightingTests
{
    private static IMEPackage EmptyLevel(MEGame game) =>
        MEPackageHandler.OpenMEPackageFromStream(MEPackageHandler.CreateEmptyLevelStream("InterpLightingTest", game));

    private static ExportEntry Add(IMEPackage package, IEntry parent, string name, string className,
        PropertyCollection properties = null)
    {
        var entry = new ExportEntry(package, parent, new NameReference(name),
            prePropBinary: new byte[className.EndsWith("Component") ? 8 : 4], properties: properties ?? new())
        {
            Class = package.GetEntryOrAddImport("Engine." + className, "Class")
        };
        package.AddExport(entry);
        return entry;
    }

    [TestMethod]
    public void MoversRequireAnAuthoredPrecomputedOptInAndLevelMembership()
    {
        GlobalTest.Init();
        using var package = EmptyLevel(MEGame.LE3);
        var actor = Add(package, package.GetLevel(), "Mover", "InterpActor");
        var component = Add(package, actor, "Mesh", "StaticMeshComponent");
        var actors = new[] { actor.UIndex };
        Assert.IsFalse(ConvertToUDK.IsStaticLightingMeshComponent(component, actors));
        component.WriteProperty(new BoolProperty(true, "bUsePrecomputedShadows"));
        Assert.IsTrue(ConvertToUDK.IsStaticLightingMeshComponent(component, actors));
        Assert.IsFalse(ConvertToUDK.IsStaticLightingMeshComponent(component, Array.Empty<int>()));
        component.ObjectFlags |= UnrealFlags.EObjectFlags.ArchetypeObject;
        Assert.IsFalse(ConvertToUDK.IsStaticLightingMeshComponent(component, actors));
        component.ObjectFlags &= ~UnrealFlags.EObjectFlags.ArchetypeObject;
        component.WriteProperty(new BoolProperty(false, "bUsePrecomputedShadows"));
        component.WriteProperty(new BoolProperty(true, "bBioForcePrecomputedShadows"));
        Assert.IsTrue(ConvertToUDK.IsStaticLightingMeshComponent(component, actors));
        component.RemoveProperty("bBioForcePrecomputedShadows");
        var archetype = Add(package, null, "AuthoredMeshDefaults", "StaticMeshComponent",
            new() { new BoolProperty(true, "bUsePrecomputedShadows") });
        component.Archetype = archetype;
        Assert.IsFalse(ConvertToUDK.IsStaticLightingMeshComponent(component, actors), "Instance false overrides archetype true");
        component.RemoveProperty("bUsePrecomputedShadows");
        Assert.IsTrue(ConvertToUDK.IsStaticLightingMeshComponent(component, actors));
        Assert.IsNull(component.GetProperty<BoolProperty>("bUsePrecomputedShadows"), "Selection must not flatten source properties");
        Assert.AreEqual(archetype, component.Archetype);
        actor.Class = package.GetEntryOrAddImport("Engine.StaticMeshActor", "Class");
        component.Archetype = null;
        Assert.IsTrue(ConvertToUDK.IsStaticLightingMeshComponent(component, actors), "Ordinary static meshes remain eligible");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ImportPreservesExcludedLightingAndSharedShadowResources(bool explicitlyKeep)
    {
        GlobalTest.Init();
        using var source = EmptyLevel(MEGame.LE3);
        using var bake = EmptyLevel(MEGame.UDK);
        var texture = Add(source, null, "OriginalLightmap", "LightMapTexture2D");
        var shadowTexture = Add(source, null, "OriginalShadowTexture", "Texture2D");
        var shadow = Add(source, null, "OriginalShadow", "ShadowMap2D",
            new() { new ObjectProperty(shadowTexture.UIndex, "Texture") });
        var matchedActor = Add(source, source.GetLevel(), "MatchedActor", "StaticMeshActor");
        var matched = Add(source, matchedActor, "Mesh", "StaticMeshComponent");
        var mover = Add(source, source.GetLevel(), "MissingMover", "InterpActor");
        var missing = Add(source, mover, "Mesh", "StaticMeshComponent",
            new() { new BoolProperty(true, "bUsePrecomputedShadows") });
        foreach (var component in new[] { matched, missing })
            component.WriteBinary(new StaticMeshComponent { LODData = [new StaticMeshComponentLODInfo
            {
                LightMap = new LightMap_2D { LightMapType = ELightMapType.LMT_2D, Texture1 = texture.UIndex },
                ShadowMaps = [shadow.UIndex], ShadowVertexBuffers = []
            }] });
        var proxy = Add(bake, bake.GetLevel(), "Proxy", "StaticMeshActor",
            new() { new NameProperty(matched.InstancedFullPath, "Tag") });
        var bakedComponent = Add(bake, proxy, "Mesh", "StaticMeshComponent");
        bakedComponent.WriteBinary(new StaticMeshComponent { LODData = [new StaticMeshComponentLODInfo
        {
            LightMap = new LightMap { LightMapType = ELightMapType.LMT_None }, ShadowMaps = [], ShadowVertexBuffers = []
        }] });
        if (explicitlyKeep)
        {
            var keptProxy = Add(bake, bake.GetLevel(), "KeptProxy", "StaticMeshActor",
                new() { new NameProperty(missing.InstancedFullPath, "Tag") });
            var keptComponent = Add(bake, keptProxy, "Mesh", "StaticMeshComponent");
            keptComponent.WriteBinary(StaticMeshComponent.Create());
        }
        var setup = new LightingImportSetup { ShouldKeepLightMap = explicitlyKeep ? e => e == missing : null };
        var import = typeof(StaticLightingImporter).GetMethod("AssignStaticLighting", BindingFlags.Static | BindingFlags.NonPublic);
        import.Invoke(null, [source, bake, setup]);
        var retained = ObjectBinary.From<StaticMeshComponent>(missing).LODData[0];
        var retainedTexture = source.GetUExport(((LightMap_2D)retained.LightMap).Texture1);
        StringAssert.StartsWith(retainedTexture.ObjectName.Name, setup.KeptLightmapPrefix);
        var retainedShadow = source.GetUExport(retained.ShadowMaps.Single());
        StringAssert.StartsWith(retainedShadow.ObjectName.Name, setup.KeptLightmapPrefix);
        var retainedShadowTexture = source.GetUExport(retainedShadow.GetProperty<ObjectProperty>("Texture").Value);
        StringAssert.StartsWith(retainedShadowTexture.ObjectName.Name, setup.KeptLightmapPrefix);
        Assert.AreEqual(ELightMapType.LMT_None, ObjectBinary.From<StaticMeshComponent>(matched).LODData[0].LightMap.LightMapType);
        CollectionAssert.AreEquivalent(new[] { matched }, setup.RelitComponents.ToArray());
        int keptCount = source.Exports.Count(e => e.ObjectName.Name.StartsWith(setup.KeptLightmapPrefix));
        import.Invoke(null, [source, bake, setup]);
        Assert.AreEqual(keptCount, source.Exports.Count(e => e.ObjectName.Name.StartsWith(setup.KeptLightmapPrefix)),
            "Repeated import must reuse preserved resources");
    }
}
