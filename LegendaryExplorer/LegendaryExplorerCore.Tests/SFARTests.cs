using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class SFARTests
    {
        [TestMethod]
        public void TestSFARParsing()
        {
            GlobalTest.Init();
            // Loads compressed packages and attempts to enumerate every object's properties.
            var sfarsPath = GlobalTest.GetTestSFARsDirectory();
            var sfars = Directory.GetFiles(sfarsPath, "*.sfar", SearchOption.AllDirectories);
            foreach (var s in sfars)
            {
                var expectedCompressionType = Path.GetFileNameWithoutExtension(s);
                Console.WriteLine($"Opening SFAR {s}");
                DLCPackage dlc = new DLCPackage(s);
                Assert.AreEqual(expectedCompressionType, dlc.Header.CompressionScheme);
                
                // Enumerate files
                foreach (var f in dlc.Files)
                {
                    var lookup = dlc.FindFileEntry(Path.GetFileName(f.FileName));
                    Assert.AreEqual(dlc.Files[lookup], f);
                    var de = dlc.DecompressEntry(lookup);
                    Assert.AreEqual(f.RealUncompressedSize, de.Length);
                }
            }
        }
    }
}