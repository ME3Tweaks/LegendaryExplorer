using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Unreal.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class CollectionTests
{
    [TestMethod]
    public void TestUMultiMap()
    {
        GlobalTest.Init();

        var map = new UMultiMap<string, int>(2)
        {
            { "A", 1 },
            { "B", 2 }
        };
        Assert.AreEqual(2, map.Count);

        map.Add("A", 3);
        Assert.AreEqual(3, map.Count);

        map["B"] = 4;
        Assert.AreEqual(3, map.Count);

        Assert.AreEqual(3, map["A"]);

        Assert.IsTrue(map.MultiFind("A").SequenceEqual(new[] { 3, 1 }));

        Assert.IsFalse(map.TryAddUnique("B", 4));
        Assert.IsTrue(map.TryAddUnique("B", 5));
        Assert.AreEqual(4, map.Count);

        //if nothing has been removed from the map, insertion order should be preserved
        Assert.IsTrue(map.ToArray().SequenceEqual(new KeyValuePair<string, int>[] { new("A", 1), new("B", 4), new("A", 3), new("B", 5) }));
        Assert.IsTrue(map.Keys.SequenceEqual(new[] { "A", "B" }));
        Assert.IsTrue(map.Values.SequenceEqual(new[] { 1, 4, 3, 5 }));

        map.Add("C", 3);
        map.Add("C", 3);
        map.Add("C", 3);

        Assert.IsFalse(map.RemoveSingle("X", 4));
        Assert.IsTrue(map.RemoveSingle("A", 3));
        Assert.AreEqual(6, map.Count);

        map.Compact();

        Assert.AreEqual(1, map["A"]);

        Assert.IsTrue(map.ContainsKey("B"));
        Assert.IsTrue(map.Remove("B"));
        Assert.IsFalse(map.ContainsKey("B"));
        Assert.AreEqual(4, map.Count);

        map.Empty();

        Assert.AreEqual(0, map.Count);
    }
}