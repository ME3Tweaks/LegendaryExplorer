using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.GameFilesystem;
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
                LocalProfile lp = LocalProfile.DeserializeLocalProfile(profileFile, expectedGame);


                //Assert.AreEqual(expectedGame, mf.Game, $"Mount file {mountName} parsed to the wrong game");
                //Assert.AreEqual(expectedMountPriority, mf.MountPriority, $"Mount file {mountName} has wrong parsed mount priority");
                //Assert.AreEqual(expectedMountTLK, mf.TLKID, $"Mount file {mountName} has wrong parsed mount TLKID");
                //Assert.AreEqual(expectedMountFlag, mf.MountFlags.FlagValue, $"Mount file {mountName} has wrong parsed mount flag");
                //if (expectedGame == MEGame.ME2)
                //{
                //    Assert.AreEqual(me2DlcName, mf.ME2Only_DLCFolderName, $"Mount file {mountName} has wrong DLC folder name");
                //    Assert.AreEqual(me2HRDlcName, mf.ME2Only_DLCHumanName, $"Mount file {mountName} has wrong human name");
                //}

                //var testStream = new MemoryStream();
                //mf.WriteMountFileToStream(testStream);

                //var mountBytes = File.ReadAllBytes(profileFile);
                //Assert.AreEqual(mountBytes.Length, testStream.Length, "Serialized mount file has wrong length, differs from original");
                //// We can't easily test if we serialized correctly as we don't write back the GUIDs used for validation
                //// maybe in future we will use it for testing.

            }
        }
    }
}
