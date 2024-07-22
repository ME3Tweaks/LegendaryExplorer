using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Dialogue;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class BioConversationTests
    {
        [TestMethod]
        public void TestBioConversationReserialization()
        {
            GlobalTest.Init();
            var packagesPath = GlobalTest.GetTestPackagesDirectory();
            var packages = Directory.GetFiles(packagesPath, "*.*", SearchOption.AllDirectories);
            foreach (var p in packages)
            {
                if (p.RepresentsPackageFilePath())
                {
                    // Do not use package caching in tests
                    Console.WriteLine($"Opening package {p}");
                    (var game, var platform) = GlobalTest.GetExpectedTypes(p);
                    if (platform == MEPackage.GamePlatform.PC)
                    {
                        var loadedPackage = MEPackageHandler.OpenMEPackage(p, forceLoadFromDisk: true);
                        foreach (var bioConv in loadedPackage.Exports.Where(x => !x.IsDefaultObject && x.ClassName == "BioConversation"))
                        {
                            Console.WriteLine($"Testing reserialization of {bioConv.InstancedFullPath}");
                            var startData = bioConv.Data;
                            var startProps = bioConv.GetProperties();
                            ConversationExtended ce = new ConversationExtended(bioConv);
                            ce.LoadConversation(null, true); // no tlk lookup
                            ce.SerializeNodes();
                            var endData = bioConv.Data;
                            var endProps = bioConv.GetProperties();

                            var areEqual = startData.SequenceEqual(endData);

#if DEBUG
                            if (!areEqual)
                            {
                                Debug.WriteLine($"Prop count at root: {startProps.Count} vs {endProps.Count}");
                                var propNames = endProps.Select(x => x.Name.Name).ToList();
                                propNames = propNames.Except(startProps.Select(x => x.Name.Name)).ToList();
                                Debug.WriteLine("Extra props:");
                                foreach(var v in propNames) Debug.WriteLine(v);

                                DebugTools.DebugUtilities.CompareByteArrays(startData, endData);
                            }
#endif
                            Assert.IsTrue(areEqual, $"Reserialization of conversation {bioConv.InstancedFullPath} yielded different export data");
                        }
                    }
                }
            }
        }
    }
}
