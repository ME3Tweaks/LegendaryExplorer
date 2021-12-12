using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.TLK.ME2ME3;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class TLKTests
    {
        [TestMethod]
        public void TestTLKs()
        {
            GlobalTest.Init();
            // Loads compressed packages and attempts to enumerate every object's properties.
            var tlkDataPath = GlobalTest.GetTestTLKDirectory();

            //ME1
            var packages = Directory.GetFiles(tlkDataPath, "*.*", SearchOption.AllDirectories)
                .Where(x => x.RepresentsPackageFilePath());
            foreach (var p in packages)
            {
                Console.WriteLine($"Opening package {p}");
                (MEGame expectedGame, MEPackage.GamePlatform expectedPlatform) = GlobalTest.GetExpectedTypes(p);
                using var package = MEPackageHandler.OpenMEPackage(p, forceLoadFromDisk: true);

                foreach (var export in package.Exports.Where(x => x.ClassName == "BioTlkFile"))
                {
                    var me1Tf = new ME1TalkFile(export);
                    foreach (var stringId in me1Tf.StringRefs)
                    {
                        var expected = stringId.Data;
                        var found = me1Tf.FindDataById(stringId.StringID);

                        // Strip single pair of quotes off. Trim() does multiple so if string ends with " it ruins it
                        if (found.StartsWith('\"')) found = found.Substring(1);
                        if (found.EndsWith('\"')) found = found.Substring(0, found.Length - 1);
                        Assert.AreEqual(string.IsNullOrEmpty(expected) ? "" : expected, found);
                    }
                }
            }

            // ME2/ME3
            var tlks = Directory.GetFiles(tlkDataPath, "*.tlk", SearchOption.AllDirectories);
            foreach (var tlkFilePath in tlks)
            {
                // Do not use package caching in tests
                Debug.WriteLine($"Opening TLK file {tlkFilePath}");
                (MEGame expectedGame, MEPackage.GamePlatform expectedPlatform) = GlobalTest.GetExpectedTypes(tlkFilePath);
                var tf = new TalkFile();
                tf.LoadTlkData(tlkFilePath);

                foreach (var stringId in tf.StringRefs)
                {
                    var expected = stringId.Data;
                    var found = tf.FindDataById(stringId.StringID);
                    var testcache = found;
                    // Strip single pair of quotes off. Trim() does multiple so if string ends with " it ruins it
                    if (found.StartsWith('\"')) found = found.Substring(1);
                    if (found.EndsWith('\"')) found = found.Substring(0, found.Length - 1);

                    if (expected == "Female") continue; //It seems we don't have a way to query female strings.
                    Assert.AreEqual(string.IsNullOrEmpty(expected) ? "" : expected, found);
                }

            }
        }
    }
}
