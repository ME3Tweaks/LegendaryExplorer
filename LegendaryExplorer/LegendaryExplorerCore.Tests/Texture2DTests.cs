using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class Texture2DTests
    {
        [TestMethod]
        public void TestStorageTypeDetermination()
        {
            // Empty and LZMA always return the same
            Assert.AreEqual(Texture2D.CalculateStorageType(StorageTypes.empty, MEGame.ME3, false), StorageTypes.empty);
            Assert.AreEqual(Texture2D.CalculateStorageType(StorageTypes.empty, MEGame.ME3, true), StorageTypes.empty);
            Assert.AreEqual(Texture2D.CalculateStorageType(StorageTypes.extLZMA, MEGame.ME3, false), StorageTypes.extLZMA);

            // ME3 - Following storage types should become Zlib
            Assert.AreEqual(Texture2D.CalculateStorageType(StorageTypes.extLZO, MEGame.ME3, false), StorageTypes.extZlib);
            Assert.AreEqual(Texture2D.CalculateStorageType(StorageTypes.extUnc, MEGame.ME3, false), StorageTypes.extZlib);
            Assert.AreEqual(Texture2D.CalculateStorageType(StorageTypes.pccLZO, MEGame.ME3, false), StorageTypes.extZlib);
            Assert.AreEqual(Texture2D.CalculateStorageType(StorageTypes.pccUnc, MEGame.ME3, false), StorageTypes.extZlib);

            // ME2 - Following storage types should become LZO
            Assert.AreEqual(Texture2D.CalculateStorageType(StorageTypes.extZlib, MEGame.ME2, false), StorageTypes.extLZO);
            Assert.AreEqual(Texture2D.CalculateStorageType(StorageTypes.extUnc, MEGame.ME2, false), StorageTypes.extLZO);
            Assert.AreEqual(Texture2D.CalculateStorageType(StorageTypes.pccZlib, MEGame.ME2, false), StorageTypes.extLZO);
            Assert.AreEqual(Texture2D.CalculateStorageType(StorageTypes.pccUnc, MEGame.ME2, false), StorageTypes.extLZO);

            // LE - Following storage types should become Oodle
            Assert.AreEqual(Texture2D.CalculateStorageType(StorageTypes.extUnc, MEGame.LE3, false), StorageTypes.extOodle);
            Assert.AreEqual(Texture2D.CalculateStorageType(StorageTypes.pccUnc, MEGame.LE3, false), StorageTypes.extOodle);

            var pccTypes = new List<StorageTypes>()
                { StorageTypes.pccOodle, StorageTypes.pccUnc, StorageTypes.pccZlib, StorageTypes.pccLZO };
            
            var extTypes = new List<StorageTypes>()
                { StorageTypes.extOodle, StorageTypes.extUnc, StorageTypes.extZlib, StorageTypes.extLZO }; // extLZMA not included - only for console games
            
            // All ext types should become pcc when isPackageStored is true
            foreach (var t in extTypes)
            {
                Assert.IsTrue(pccTypes.Contains(Texture2D.CalculateStorageType(t, MEGame.LE3, true)));
            }
            
            // All pcc types should become ext types when isPackageStored is false
            foreach (var t in pccTypes)
            {
                Assert.IsTrue(extTypes.Contains(Texture2D.CalculateStorageType(t, MEGame.LE3, false)));
            }
        }
    }
}