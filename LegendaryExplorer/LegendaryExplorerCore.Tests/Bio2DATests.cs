using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Classes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests
{
    /// <summary>
    /// Tests Bio2DA operations
    /// </summary>
    [TestClass]
    public class Bio2DATests
    {
        [TestMethod]
        public void TestBasicReserializationOf2DAs()
        {
            GlobalTest.Init();
            // Loads compressed packages and attempts to enumerate every object's properties.
            var packagesPath = GlobalTest.GetTestPackagesDirectory();
            var packages = Directory.GetFiles(packagesPath, "*.*", SearchOption.AllDirectories);
            foreach (var p in packages)
            {
                if (p.RepresentsPackageFilePath())
                {
                    // Do not use package caching in tests
                    Console.WriteLine($"Opening package {p}");
                    var package = MEPackageHandler.OpenMEPackage(p, forceLoadFromDisk: true);
                    foreach (var twoDAExp in package.Exports.Where(x => !x.IsDefaultObject && x.ClassName == "Bio2DA" || x.ClassName == "Bio2DANumberedRows"))
                    {
                        // Tests reserialization of data via the Bio2DA WriteToExport method.
                        var data = twoDAExp.Data;
                        var twoDA = new Bio2DA(twoDAExp);
                        twoDA.Write2DAToExport();
                        var newData = twoDAExp.Data;
                        if (data.SequenceEqual(newData))
                        {
                            Assert.Fail($"Reserialization of 2DA {twoDA.Export.InstancedFullPath} in {p} failed via Bio2DA.Write2DAToExport(): Before and after data was different");
                        }

                    }
                }
            }
        }
    }
}
