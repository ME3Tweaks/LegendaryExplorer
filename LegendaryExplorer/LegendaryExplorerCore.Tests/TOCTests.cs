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
            var folderToToc = Path.Combine(gameFolder, "BIOGame");
            var comparisonTocFile = Path.Combine(gameFolder, "BIOGame\\PCConsoleTOC.bin");

            var toc = TOCCreator.CreateTOCForDirectory(folderToToc);
            CollectionAssert.AreEqual(File.ReadAllBytes(comparisonTocFile), toc.ToArray());

            // Test full reserialization
            foreach (var tocF in Directory.GetFiles(GlobalTest.GetTestDataDirectory(),"PCConsoleTOC.bin",SearchOption.AllDirectories))
            {
                var tocDiskBytes = File.ReadAllBytes(tocF);
                TOCBinFile tbf = new TOCBinFile(new MemoryStream(tocDiskBytes));
                var reserialized = tbf.Save();

                var reserializedArray = reserialized.ToArray();
                Assert.IsTrue(tocDiskBytes.SequenceEqual(reserializedArray), $"Re-serialized TOC file is not the same as the original! File: {tocF}");
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
            // We add one to this to include the TOC file itself
            Assert.AreEqual(tocEntryFiles.Length + 1,  allEntries.Count);

            foreach (var file in tocEntryFiles)
            {
                var size = new FileInfo(file).Length;
                Assert.IsTrue(allEntries.Any((e) => file.EndsWith(e.name) && size == e.size));
            }
        }
    }
}
