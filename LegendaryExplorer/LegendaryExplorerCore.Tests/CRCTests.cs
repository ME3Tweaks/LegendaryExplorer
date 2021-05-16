using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LegendaryExplorerCore.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class CRCTests
    {
        [TestMethod]
        public void TestParallelCRC()
        {
            GlobalTest.Init();
            // Loads compressed packages and attempts to enumerate every object's properties.
            var filesPath = GlobalTest.GetTestCRCDirectory();
            var files = Directory.GetFiles(filesPath, "*.*", SearchOption.AllDirectories);
            foreach (var f in files)
            {
                var expectedCrc = Path.GetFileNameWithoutExtension(f);
                var calculatedCrc = ParallelCRC.Compute(File.ReadAllBytes(f));
                Assert.AreEqual(expectedCrc, calculatedCrc.ToString("x8"));
            }
        }
    }
}
