using System;
using System.IO;
using System.Linq;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Tests.helpers;
using ME3ExplorerCore.Unreal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ME3ExplorerCore.Tests
{
    [TestClass]
    public class PackageTests
    {
        [TestMethod]
        public void TestPackageLoadingAndProperties()
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
                    Console.WriteLine($" > Enumerating all exports for properties");

                    foreach (var exp in package.Exports)
                    {
                        if (exp.ClassName != "Class")
                        {
                            var props = exp.GetProperties(forceReload: true, includeNoneProperties: true);
                            Assert.IsInstanceOfType(props.LastOrDefault(), typeof(NoneProperty),
                                $"Error parsing properties on export {exp.UIndex} {exp.InstancedFullPath} in file {exp.FileRef.FilePath}");
                        }
                    }
                }
            }
        }
    }
}
