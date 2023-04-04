using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Coalesced;
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
                if (expectedGame is MEGame.LE2 or MEGame.LE3)
                {
                    LocalProfile lp = LocalProfile.DeserializeLocalProfile(profileFile, expectedGame);
                    var reserialized = lp.Serialize();
                    var reserializedBytes = reserialized.ToArray();

                    Assert.IsTrue(originalBytes.SequenceEqual(reserializedBytes), $"{nameof(LocalProfile)} {profileFile} failed to reserialize identically!");
                }
                else if (expectedGame is MEGame.LE1)
                {
                    var lp = LocalProfileLE1.DeserializeLocalProfile(profileFile);
                    var reserialized = lp.Serialize();
                    var reserializedBytes = reserialized.ToArray();
                    Assert.IsTrue(originalBytes.SequenceEqual(reserializedBytes), $"{nameof(LocalProfileLE1)} {profileFile} failed to reserialize identically!");
                }
            }
        }
    }
}
