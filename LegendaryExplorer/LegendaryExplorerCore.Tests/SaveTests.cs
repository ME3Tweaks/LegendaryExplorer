using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Save;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class SaveTests
    {
        [TestMethod]
        public void TestLocalProfileSerializer()
        {
            GlobalTest.Init();

            // Deserialize and re-serialize all profile files to ensure serialization is correct
            var packagesPath = GlobalTest.GetLocalProfileDirectory();
            var profileFiles = Directory.GetFiles(packagesPath, "*", SearchOption.AllDirectories);
            foreach (var profileFile in profileFiles)
            {
                // Do not use package caching in tests
                Debug.WriteLine($"Opening profile file {profileFile}");

                var expectedGame = GlobalTest.GetExpectedGame(profileFile);
                var originalBytes = File.ReadAllBytes(profileFile);
                LocalProfile lp = LocalProfile.DeserializeLocalProfile(profileFile, expectedGame);
                var reserialized = lp.Serialize();
                reserialized.WriteToFile(@"B:\UserProfile\Documents\BioWare\Mass Effect Legendary Edition\Save\ME3\Reserialized_Comp.bin");
                var reserializedBytes = reserialized.ToArray();

                Assert.IsTrue(originalBytes.SequenceEqual(reserializedBytes));

            }
        }
    }
}
