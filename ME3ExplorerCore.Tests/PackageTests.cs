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
        public void TestPackages()
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

                        if (exp.ClassName == "Function")
                        {
                            // test function parsing
                        }

                        // Binary testing?
                    }
                }
            }
        }

        [TestMethod]
        public void TestCompression()
        {
            GlobalTest.Init();

            // Loads compressed packages, save them uncompressed. Load package, save re-compressed, compare results
            var packagesPath = GlobalTest.GetTestPackagesDirectory();
            var packages = Directory.GetFiles(packagesPath, "*.*", SearchOption.AllDirectories);
            foreach (var p in packages)
            {
                if (p.RepresentsPackageFilePath())
                {
                    // Do not use package caching in tests
                    Console.WriteLine($"Opening package {p}");
                    var originalLoadedPackage = MEPackageHandler.OpenMEPackage(p);
                    if (originalLoadedPackage.Platform != MEPackage.GamePlatform.PC)
                    {
                        Assert.ThrowsException<Exception>(() =>
                        {
                            originalLoadedPackage.SaveToStream(true);
                        }, "Non-PC platform package should not be saveable. An exception should have been thrown to stop this!");
                        continue;
                    }

                    // Is PC
                    var uncompressedPS = originalLoadedPackage.SaveToStream(false);
                    var compressedPS = originalLoadedPackage.SaveToStream(true);


                    uncompressedPS.Position = compressedPS.Position = 0;

                    var reopenedUCP = MEPackageHandler.OpenMEPackageFromStream(uncompressedPS);
                    var reopenedCCP = MEPackageHandler.OpenMEPackageFromStream(compressedPS);

                    Assert.AreEqual(reopenedCCP.NameCount, reopenedUCP.NameCount, $"Name count is not identical between compressed/uncompressed packages");
                    Assert.AreEqual(reopenedCCP.ImportCount, reopenedUCP.ImportCount, $"Import count is not identical between compressed/uncompressed packages");
                    Assert.AreEqual(reopenedCCP.ExportCount, reopenedUCP.ExportCount, $"Export count is not identical between compressed/uncompressed packages");

                    for (int i = 0; i < reopenedCCP.NameCount; i++)
                    {
                        var nameCCP = reopenedCCP.Names[i];
                        var nameUCP = reopenedUCP.Names[i];
                        Assert.AreEqual(nameCCP, nameUCP, $"Names are not identical between compressed/uncompressed packages, name index {i}");
                    }

                    for (int i = 0; i < reopenedCCP.ImportCount; i++)
                    {
                        var importCCP = reopenedCCP.Imports[i];
                        var importUCP = reopenedUCP.Imports[i];
                        Assert.IsTrue(importCCP.Header.SequenceEqual(importUCP.Header), $"Header data for import {-(i + 1)} are not identical between compressed/uncompressed packages");
                    }

                    for (int i = 0; i < reopenedCCP.ExportCount; i++)
                    {
                        var exportCCP = reopenedCCP.Exports[i];
                        var exportUCP = reopenedUCP.Exports[i];
                        Assert.IsTrue(exportCCP.Header.SequenceEqual(exportUCP.Header), $"Header data for xport {i + 1} are not identical between compressed/uncompressed packages");
                    }
                }
            }
        }
    }
}
