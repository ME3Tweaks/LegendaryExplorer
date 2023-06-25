using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Audio;
using LegendaryExplorerCore.Sound.ISACT;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]

    public class ISBTests
    {
        [TestMethod]
        public void TestISBParsing()
        {
            GlobalTest.Init();
            var packagesPath = GlobalTest.GetTestISBDirectory();
            var isbs = Directory.GetFiles(packagesPath, "*.isb", SearchOption.AllDirectories);

            foreach (var isbPath in isbs)
            {
                using var fs = File.OpenRead(isbPath);
                var expectedCount = getExpectedData(isbPath);
                var isb = new ISACTBank(fs);
                Assert.AreEqual(expectedCount, isb.GetAllBankChunks().Count(x=>x is DataBankChunk));
            }
        }

        private int getExpectedData(string isbPath)
        {
            // We unfortunately can't test music_bank.isb which is the problematic one for these games as it's the largest ISB

            // This can be expanded later

            // 0 = Game
            // 1 = Filename (doesn't have to match in game)
            // 2 = Expected file count in ISB
            var datas = Path.GetFileNameWithoutExtension(isbPath).Split('_');
            var expectedEntryCount = int.Parse(datas[2]);

            return expectedEntryCount;
        }
    }
}