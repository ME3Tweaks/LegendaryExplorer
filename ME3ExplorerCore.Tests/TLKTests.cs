using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.TLK.ME2ME3;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ME3ExplorerCore.Tests
{
    [TestClass]
    public class TLKTests
    {
        [TestMethod]
        public void TestTLKs()
        {
            GlobalTest.Init();
            // Loads compressed packages and attempts to enumerate every object's properties.
            var tlkDataPath = GlobalTest.GetTestTLKdirectory();
            var tlks = Directory.GetFiles(tlkDataPath, "*.tlk", SearchOption.AllDirectories);
            foreach (var tlkFilePath in tlks)
            {
                // Do not use package caching in tests
                Debug.WriteLine($"Opening TLK file {tlkFilePath}");
                (MEGame expectedGame, MEPackage.GamePlatform expectedPlatform) = GlobalTest.GetExpectedTypes(tlkFilePath);
                TalkFile tf = new TalkFile();
                tf.LoadTlkData(tlkFilePath);
            }
        }
    }
}
