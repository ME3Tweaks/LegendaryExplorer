using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ME3ExplorerCore.Tests
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
            CollectionAssert.AreEqual(File.ReadAllBytes(comparisonTocFile).ToArray(), toc.ToArray());
        }

        [TestMethod]
        public void TestTOCParsing()
        {
            GlobalTest.Init();
            var gameFolder = GlobalTest.GetTestMiniGamePath(MEGame.ME3);
            var tocFile = Path.Combine(gameFolder, "BIOGame\\PCConsoleTOC.bin");
            var tocEntryFiles = Directory.GetFiles(Path.Combine(gameFolder, "BIOGame\\CookedPCConsole"));

            var TOC = new TOCBinFile(tocFile);

            // We add one to this to include the TOC file itself
            Assert.AreEqual(tocEntryFiles.Length + 1, TOC.Entries.Count);

            foreach (var file in tocEntryFiles)
            {
                var size = new FileInfo(file).Length;
                Assert.IsTrue(TOC.Entries.Any((e) => file.EndsWith(e.name) && size == e.size));
            }
        }
    }
}
