using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Coalesced;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class CoalescedTests
    {
        [TestMethod]
        public void TestCoalescedSerialization()
        {
            GlobalTest.Init();
            
            // TODO: Make LE1/LE2 coalesced format support compile and decompile to memory using streams so we don't have to write to disk here.
            var bins = Directory.GetFiles(GlobalTest.GetTestCoalescedDirectory(), "Coalesced.bin", SearchOption.AllDirectories);
            foreach (var coalBin in bins)
            {
                using var fs = File.OpenRead(coalBin);
                var decomp1 = CoalescedConverter.DecompileToMemory(fs);
                var recomp1 = CoalescedConverter.CompileFromMemory(decomp1);
                var decomp2 = CoalescedConverter.DecompileToMemory(recomp1);

                // Compare
                foreach (var pair1 in decomp1)
                {
                    var pairValue2 = decomp2[pair1.Key];
                    Assert.AreEqual(pair1.Value, pairValue2, $"Decompilation/recomp/re-decomp failed for coalesced file! Results are not identical. File: {coalBin}. Subfile: {pair1.Key}");
                }
            }
        }
    }
}
