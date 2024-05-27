using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class TOCTests
    {
        [TestMethod]
        public void TestTOCCreation()
        {
            GlobalTest.Init();

            // Test against the testdata/dynamiclookupminigame/ME3 folder.
            // Generate a TOC and compare against the TOC already in that folder
            var gameFolder = GlobalTest.GetTestMiniGamePath(MEGame.ME3);
            var comparisonTocFile = Path.Combine(gameFolder, "BIOGame", "PCConsoleTOC.bin");

            var tocDiskBytes = File.ReadAllBytes(comparisonTocFile);

            // Test full reserialization
            foreach (var tocF in Directory.GetFiles(GlobalTest.GetTestDataDirectory(), "*PCConsoleTOC.bin", SearchOption.AllDirectories))
            {
                tocDiskBytes = File.ReadAllBytes(tocF);
                var tbf = new TOCBinFile(new MemoryStream(tocDiskBytes));
                var reserialized = tbf.Save();
                reserialized.Position = 0;

                var tbf2 = new TOCBinFile(reserialized);
                Assert.AreEqual(tbf.HashBuckets.Sum(x=>x.TOCEntries.Count), tbf2.HashBuckets.Sum(x => x.TOCEntries.Count), $"Re-serialized TOC file has different amount of files! File: {tocF}");
            }
        }

        [TestMethod]
        public void TestTOCParsing()
        {
            GlobalTest.Init();
            var gameFolder = GlobalTest.GetTestMiniGamePath(MEGame.ME3);
            var tocFile = Path.Combine(gameFolder, "BIOGame\\PCConsoleTOC.bin");
            var tocEntryFiles = Directory.GetFiles(Path.Combine(gameFolder, "BIOGame\\CookedPCConsole"));

            var TOC = new TOCBinFile(tocFile);
            var allEntries = TOC.GetAllEntries();
            Assert.AreEqual(tocEntryFiles.Length, allEntries.Count);

            foreach (var file in tocEntryFiles)
            {
                var size = new FileInfo(file).Length;
                Assert.IsTrue(allEntries.Any((e) => file.EndsWith(e.name) && size == e.size));
            }
        }
    }
}
