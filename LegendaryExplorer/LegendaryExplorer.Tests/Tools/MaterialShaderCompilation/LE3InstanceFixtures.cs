using System;
using System.Threading;
using System.Threading.Tasks;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tests.Tools.MaterialShaderCompilation;

/// <summary>In-memory LE3 Material / MaterialInstanceConstant fixtures shared by the resolver and service tests.</summary>
internal static class LE3InstanceFixtures
{
    private static readonly object InitLock = new();

    public static void InitLib()
    {
        lock (InitLock)
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            // Idempotent: the library ignores repeat initialization.
            LegendaryExplorerCoreLib.InitLib(TaskScheduler.FromCurrentSynchronizationContext(), _ => { });
        }
    }

    public static IMEPackage NewPackage(string name = "InstanceFixtures.pcc", MEGame game = MEGame.LE3) =>
        MEPackageHandler.CreateMemoryEmptyPackage(name, game);

    /// <summary>A base Material (or DecalMaterial) whose SM3 resource carries <paramref name="resourceId"/>.</summary>
    public static ExportEntry BaseMaterial(IMEPackage package, string name = "BaseMat", Guid? resourceId = null,
        string className = "Material", IEntry parent = null)
    {
        var export = package.CreateExport(name, className, parent, indexed: false);
        var binary = LegendaryExplorerCore.Unreal.BinaryConverters.Material.Create();
        binary.SM3MaterialResource.ID = resourceId ?? Guid.NewGuid();
        export.WriteBinary(binary);
        return export;
    }

    public static StaticParameterSet StaticSet(Guid baseMaterialId, string switchName = "Sw", bool value = true) =>
        new()
        {
            BaseMaterialId = baseMaterialId,
            StaticSwitchParameters =
            [
                new StaticParameterSet.StaticSwitchParameter
                {
                    ParameterName = switchName, Value = value, bOverride = true, ExpressionGUID = Guid.NewGuid()
                }
            ],
            StaticComponentMaskParameters = [],
            NormalParameters = [],
            TerrainWeightParameters = []
        };

    /// <summary>
    /// A MaterialInstanceConstant. <paramref name="permutationId"/> non-null serializes a static permutation resource with that
    /// id and <paramref name="staticParameters"/>. <paramref name="hasPermutationFlag"/> sets bHasStaticPermutationResource
    /// independently so a flagged-but-unserialized owner can be built.
    /// </summary>
    public static ExportEntry Instance(IMEPackage package, string name, IEntry parent, bool hasPermutationFlag = false,
        Guid? permutationId = null, StaticParameterSet staticParameters = null, int? rawParentIndex = null,
        string className = "MaterialInstanceConstant", bool writeBinary = true)
    {
        var export = package.CreateExport(name, className, indexed: false);
        WriteInstanceProperties(export, parent, hasPermutationFlag, rawParentIndex);
        if (writeBinary)
        {
            var binary = MaterialInstance.Create();
            if (permutationId != null)
            {
                binary.SM3StaticPermutationResource.ID = permutationId.Value;
                binary.SM3StaticParameterSet = staticParameters ?? StaticSet(Guid.Empty);
            }
            export.WriteBinary(binary);
        }
        return export;
    }

    public static void WriteInstanceProperties(ExportEntry export, IEntry parent, bool hasPermutationFlag, int? rawParentIndex = null)
    {
        var props = new PropertyCollection();
        if (rawParentIndex != null) props.Add(new ObjectProperty(rawParentIndex.Value, "Parent"));
        else if (parent != null) props.Add(new ObjectProperty(parent, "Parent"));
        if (hasPermutationFlag) props.Add(new BoolProperty(true, "bHasStaticPermutationResource"));
        export.WriteProperties(props);
    }

    public static void SetParent(ExportEntry instance, IEntry parent)
    {
        var props = instance.GetProperties();
        props.AddOrReplaceProp(new ObjectProperty(parent, "Parent"));
        instance.WriteProperties(props);
    }
}
