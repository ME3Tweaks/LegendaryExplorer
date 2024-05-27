using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class MountTests
    {
        [TestMethod]
        public void TestMounts()
        {
            GlobalTest.Init();
            // Loads compressed packages and attempts to enumerate every object's properties.
            var packagesPath = GlobalTest.GetTestMountsDirectory();
            var mounts = Directory.GetFiles(packagesPath, "*.dlc", SearchOption.AllDirectories);
            foreach (var mountFilePath in mounts)
            {
                // Do not use package caching in tests
                Debug.WriteLine($"Opening mount file {mountFilePath}");

                (MEGame expectedGame, MEPackage.GamePlatform expectedPlatform) = GlobalTest.GetExpectedTypes(mountFilePath);
                var mountFName = Path.GetFileNameWithoutExtension(mountFilePath);
                var mountProps = mountFName.Split('-');

                var mountName = mountProps[0];

                var expectedMountPriority = int.Parse(mountProps[1]);
                var expectedMountTLK = int.Parse(mountProps[2]);
                int expectedMountFlag = int.Parse(mountProps[3], NumberStyles.HexNumber);

                string me2DlcName = expectedGame == MEGame.ME2 ? mountProps[4] : null;
                string me2HRDlcName = expectedGame == MEGame.ME2 ? mountProps[5] : null;

                MountFile mf = new MountFile(mountFilePath);
                Assert.AreEqual(expectedGame, mf.Game, $"Mount file {mountName} parsed to the wrong game");
                Assert.AreEqual(expectedMountPriority, mf.MountPriority, $"Mount file {mountName} has wrong parsed mount priority");
                Assert.AreEqual(expectedMountTLK, mf.TLKID, $"Mount file {mountName} has wrong parsed mount TLKID");
                Assert.AreEqual(expectedMountFlag, mf.MountFlags.FlagValue, $"Mount file {mountName} has wrong parsed mount flag");
                if (expectedGame == MEGame.ME2)
                {
                    Assert.AreEqual(me2DlcName, mf.ME2Only_DLCFolderName, $"Mount file {mountName} has wrong DLC folder name");
                    Assert.AreEqual(me2HRDlcName, mf.ME2Only_DLCHumanName, $"Mount file {mountName} has wrong human name");
                }

                var testStream = new MemoryStream();
                mf.WriteMountFileToStream(testStream);

                var mountBytes = File.ReadAllBytes(mountFilePath);
                Assert.AreEqual(mountBytes.Length,testStream.Length, "Serialized mount file has wrong length, differs from original");
                // We can't easily test if we serialized correctly as we don't write back the GUIDs used for validation
                // maybe in future we will use it for testing.

            }
        }
    }
}
