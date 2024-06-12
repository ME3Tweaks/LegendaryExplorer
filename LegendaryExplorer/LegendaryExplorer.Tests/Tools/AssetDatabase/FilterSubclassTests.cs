using System.Collections.Generic;
using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Tools.AssetDatabase.Filters;
using LegendaryExplorerCore.Unreal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TextureFilter = LegendaryExplorer.Tools.AssetDatabase.Filters.TextureFilter;

namespace LegendaryExplorer.Tests.Tools.AssetDatabase
{
    [TestClass]
    public class FilterSubclassTests
    {
        [TestMethod]
        public void MaterialFilterLoadsFromDatabase()
        {
            var mf = new MaterialFilter(new FileListSpecification());
            var adb = new AssetDB();
            var dbSpecs = new List<MaterialBoolSpec>() { new MaterialBoolSpec(), new MaterialBoolSpec(new BoolProperty(false, "TestBool")) };
            adb.MaterialBoolSpecs = dbSpecs;

            mf.LoadFromDatabase(adb);
            CollectionAssert.AreEqual(dbSpecs, mf.GeneratedOptions);
        }

        [TestMethod]
        public void TestTextureSearch()
        {
            var tf = new TextureFilter(new FileListSpecification());
            var r1 = new TextureRecord("Name", "RandomPackage", false, false,
                "format", "LOD", 512, 512, "ABCDE");

            var r2 = new TextureRecord("Name", "RandomPackage", false, false,
                "format", "LOD", 512, 1024, "ABCDE");

            Assert.IsTrue(tf.TextureSearch(("name", r1))); // Can search against name
            Assert.IsTrue(tf.TextureSearch(("ompack", r1))); // Parent Package
            Assert.IsTrue(tf.TextureSearch(("ABCDE", r1))); // Or CRC

            // Test size parsing
            Assert.IsFalse(tf.TextureSearch(("size:", r1)));
            Assert.IsFalse(tf.TextureSearch(("size: 256x256", r1)));
            Assert.IsFalse(tf.TextureSearch(("size: 512x513", r1)));
            Assert.IsFalse(tf.TextureSearch(("size: 512xhellox", r1)));
            Assert.IsTrue(tf.TextureSearch(("size: 512x512", r1)));

            Assert.IsTrue(tf.TextureSearch(("size: 512x1024", r2)));
            Assert.IsFalse(tf.TextureSearch(("size: 1024x512", r2)));
        }

        [TestMethod]
        public void TestMeshSearch()
        {
            var r1 = new MeshRecord("NameOfMesh", true, false, 101);
            Assert.IsTrue(AssetFilters.MeshSearch(("NameOfMesh", r1)));
            Assert.IsTrue(AssetFilters.MeshSearch(("nameofmesh", r1)));
            Assert.IsTrue(AssetFilters.MeshSearch(("OfMesh", r1)));
            Assert.IsFalse(AssetFilters.MeshSearch(("Nothing", r1)));

            Assert.IsTrue(AssetFilters.MeshSearch(("bones:101", r1)));
            Assert.IsFalse(AssetFilters.MeshSearch(("bones:", r1)));
            Assert.IsFalse(AssetFilters.MeshSearch(("bones:5000", r1)));
        }

    }
}