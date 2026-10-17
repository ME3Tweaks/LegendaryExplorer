using LegendaryExplorerCore.UDK;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class UdkComponentVertexPaintTests
{
    private static StaticMeshComponentLODInfo Lod(bool painted, int texture = 0) => new()
    {
        ShadowMaps = texture == 0 ? [] : [texture + 1],
        ShadowVertexBuffers = texture == 0 ? [] : [texture + 2],
        LightMap = texture == 0 ? new LightMap { LightMapType = ELightMapType.LMT_None }
            : new LightMap_2D { LightMapType = ELightMapType.LMT_2D, Texture1 = texture },
        bLoadVertexColorData = (byte)(painted ? 1 : 0),
        OverrideVertexColors = painted ? new ColorVertexBuffer
        {
            Stride = 4, NumVertices = 2,
            VertexData = [new SharpDX.Color(12, 34, 56, 78), new SharpDX.Color(90, 123, 145, 167)]
        } : null
    };

    [TestMethod]
    public void UdkInputKeepsPaintAtEveryLodAndRemovesAllLightingReferences()
    {
        var source = new StaticMeshComponent { LODData = [Lod(false, 10), Lod(true, 20), Lod(true, 30)] };
        var input = StaticMeshComponentLightingData.CreateUdkInput(source);
        Assert.AreEqual(3, input.LODData.Length);
        for (int i = 0; i < input.LODData.Length; i++)
        {
            Assert.AreEqual(ELightMapType.LMT_None, input.LODData[i].LightMap.LightMapType);
            Assert.AreEqual(0, input.LODData[i].ShadowMaps.Length);
            Assert.AreEqual(0, input.LODData[i].ShadowVertexBuffers.Length);
            Assert.AreEqual(source.LODData[i].bLoadVertexColorData, input.LODData[i].bLoadVertexColorData);
            if (i > 0) CollectionAssert.AreEqual(source.LODData[i].OverrideVertexColors.VertexData,
                input.LODData[i].OverrideVertexColors.VertexData);
        }
        Assert.AreEqual(ELightMapType.LMT_2D, source.LODData[1].LightMap.LightMapType);
        Assert.AreEqual(0, StaticMeshComponentLightingData.CreateUdkInput(
            new StaticMeshComponent { LODData = [Lod(false, 10)] }).LODData.Length);
    }

    [TestMethod]
    public void ImportFromOldBakePreservesPaintAndHandlesDifferentLodCounts()
    {
        var target = new[] { Lod(true, 10), Lod(true, 20) };
        var baked = new[] { Lod(false, 100) };
        var merged = StaticMeshComponentLightingData.MergeLighting(target, baked);
        Assert.AreEqual(2, merged.Length);
        Assert.AreEqual(100, ((LightMap_2D)merged[0].LightMap).Texture1);
        CollectionAssert.AreEqual(new[] { 101 }, merged[0].ShadowMaps);
        CollectionAssert.AreEqual(new[] { 102 }, merged[0].ShadowVertexBuffers);
        for (int i = 0; i < merged.Length; i++)
        {
            Assert.AreEqual((byte)1, merged[i].bLoadVertexColorData);
            CollectionAssert.AreEqual(target[i].OverrideVertexColors.VertexData, merged[i].OverrideVertexColors.VertexData);
        }
        Assert.AreEqual(ELightMapType.LMT_None, merged[1].LightMap.LightMapType);
        Assert.AreEqual((byte)0, baked[0].bLoadVertexColorData);
        var moreBaked = StaticMeshComponentLightingData.MergeLighting([Lod(false)], [Lod(true, 100), Lod(true, 200)]);
        Assert.AreEqual((byte)0, moreBaked[0].bLoadVertexColorData);
        Assert.IsNull(moreBaked[0].OverrideVertexColors);
        Assert.AreEqual((byte)1, moreBaked[1].bLoadVertexColorData);
        Assert.AreEqual(200, ((LightMap_2D)moreBaked[1].LightMap).Texture1);
    }
}
