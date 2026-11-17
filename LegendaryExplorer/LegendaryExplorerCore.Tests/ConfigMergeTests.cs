using System;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Coalesced.Config;
using LegendaryExplorerCore.Packages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class ConfigMergeTests
    {
        private const string Section = "SFXGame.Test";
        private const string DeltaHeader = "[BioGame.ini " + Section + "]\n";

        /// <summary>
        /// Builds an empty DLC config bundle, seeds it with the given delta, then clears the change flag.
        /// </summary>
        private static ConfigAssetBundle CreateSeededBundle(MEGame game, string seedDelta)
        {
            var emptyCookedDir = Path.Combine(Path.GetTempPath(), "ConfigMergeTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(emptyCookedDir);
            try
            {
                var bundle = ConfigAssetBundle.FromDLCFolder(game, emptyCookedDir, "DLC_MOD_ConfigMergeTest");
                Assert.IsNotNull(bundle, "Failed to create empty config bundle");
                ConfigMerge.PerformMerge(bundle, ConfigFileProxy.ParseIni(DeltaHeader + seedDelta));
                bundle.HasChanges = false;
                return bundle;
            }
            finally
            {
                Directory.Delete(emptyCookedDir, true);
            }
        }

        private static bool Merge(ConfigAssetBundle bundle, string delta)
        {
            bundle.HasChanges = false;
            ConfigMerge.PerformMerge(bundle, ConfigFileProxy.ParseIni(DeltaHeader + delta));
            return bundle.HasChanges;
        }

        private static string[] GetKeyValues(ConfigAssetBundle bundle) =>
            bundle.GetAsset("BioGame.ini").Sections[Section]["Key"].Select(x => x.Value).ToArray();

        [TestMethod]
        public void TestOverwriteExistingPropertyFlagsChange()
        {
            GlobalTest.Init();
            var bundle = CreateSeededBundle(MEGame.LE2, "Key=Old");

            Assert.IsTrue(Merge(bundle, ">Key=New"), "Overwriting an existing property with a different value must flag the bundle as changed");
            CollectionAssert.AreEqual(new[] { "New" }, GetKeyValues(bundle));

            Assert.IsFalse(Merge(bundle, ">Key=New"), "Overwriting a property with its current value should not flag the bundle as changed");
            CollectionAssert.AreEqual(new[] { "New" }, GetKeyValues(bundle));

            Assert.IsTrue(Merge(bundle, ">Key=new"), "A case-only change to a value must flag the bundle as changed");
            CollectionAssert.AreEqual(new[] { "new" }, GetKeyValues(bundle));
        }

        [TestMethod]
        public void TestOverwriteMultiValuePropertyFlagsChange()
        {
            GlobalTest.Init();
            var bundle = CreateSeededBundle(MEGame.LE2, "Key=A\n.Key=A");

            Assert.IsTrue(Merge(bundle, ">Key=A"), "Collapsing multiple values to a single value must flag the bundle as changed");
            CollectionAssert.AreEqual(new[] { "A" }, GetKeyValues(bundle));
        }

        [TestMethod]
        public void TestOverwriteExistingPropertyFlagsChangeLE1()
        {
            GlobalTest.Init();
            // LE1 converts overwrites to Add typing
            var bundle = CreateSeededBundle(MEGame.LE1, "Key=Old");

            Assert.IsTrue(Merge(bundle, ">Key=Other"), "Overwriting an existing LE1 property with a different value must flag the bundle as changed");
            CollectionAssert.AreEqual(new[] { "Other" }, GetKeyValues(bundle));

            Assert.IsFalse(Merge(bundle, ">Key=Other"), "Overwriting an LE1 property with its current value should not flag the bundle as changed");
        }
    }
}
