using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Audio;
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
                var expectedCount = getExpectedData(isbPath);
                var isb = new ISBank(isbPath);
                Assert.AreEqual(expectedCount, isb.BankEntries.Count);

                // Have code from other methods execute just to make sure they don't throw exceptions.
                foreach (var isEntry in isb.BankEntries)
                {
                    var _ = isEntry.DisplayString;
                    isEntry.GetLength();
                }
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