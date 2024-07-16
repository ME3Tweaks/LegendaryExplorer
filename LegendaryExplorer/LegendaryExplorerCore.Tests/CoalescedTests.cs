using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
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
            
            var bins = Directory.GetFiles(GlobalTest.GetTestCoalescedDirectory(), "Coalesced*.bin", SearchOption.AllDirectories);
            foreach (var coalBin in bins)
            {
                using var fs = File.OpenRead(coalBin);

                var expectedGame = GlobalTest.GetExpectedGame(coalBin);

                if (expectedGame.IsGame3())
                {
                    var decomp1 = CoalescedConverter.DecompileGame3ToMemory(fs);
                    var recomp1 = CoalescedConverter.CompileFromMemory(decomp1);
                    var decomp2 = CoalescedConverter.DecompileGame3ToMemory(recomp1);

                    // Compare
                    foreach (var pair1 in decomp1)
                    {
                        var pairValue2 = decomp2[pair1.Key];
                        Assert.AreEqual(pair1.Value, pairValue2, $"Decompilation/recomp/re-decomp failed for coalesced file! Results are not identical. File: {coalBin}. Subfile: {pair1.Key}");
                    }
                }
                else
                {
                    var decomp1 = CoalescedConverter.DecompileLE1LE2ToMemory(fs, "");
                    var recomp1 = CoalescedConverter.CompileLE1LE2FromMemory(decomp1, coalBin.GetUnrealLocalization());
                    var decomp2 = CoalescedConverter.DecompileLE1LE2ToMemory(recomp1, "");

                    // Compare
                    foreach (var pair1 in decomp1)
                    {
                        var pairValue2 = decomp2[pair1.Key].ToString();
                        Assert.AreEqual(pair1.Value.ToString(), pairValue2, $"Decompilation/recomp/re-decomp failed for coalesced file! Results are not identical. File: {coalBin}. Subfile: {pair1.Key}");
                    }

                    // Test internal name serialization
                    fs.Position = 0;
                    recomp1.Position = 0;
                    var originalNames = LECoalescedConverter.GetInternalFilePaths(fs);
                    var newNames = LECoalescedConverter.GetInternalFilePaths(recomp1);

                    for (int i = 0; i < originalNames.Count; i++)
                    {
                        Assert.IsTrue(originalNames[i].CaseInsensitiveEquals(newNames[i]), $"LECoalesced serialization produced different internal paths: Expected: {originalNames[i]}, Got: {newNames[i]}");
                    }
                }
            }
        }
    }
}
