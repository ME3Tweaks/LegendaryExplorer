using System;
using System.Diagnostics;
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
            string[] ignoreddirnames = { "demo", "retail" };
            // Loads compressed packages and attempts to enumerate every object's properties.
            var packagesPath = GlobalTest.GetTestPackagesDirectory();
            var packages = Directory.GetFiles(packagesPath, "*.*", SearchOption.AllDirectories);
            foreach (var p in packages)
            {
                if (p.RepresentsPackageFilePath())
                {
                    // Do not use package caching in tests
                    Debug.WriteLine($"Opening package {p}");

                    MEPackage.GamePlatform expectedPlatform = MEPackage.GamePlatform.PS3; // no unknown
                    MEGame expectedGame = MEGame.Unknown;

                    string parentname = Directory.GetParent(p).FullName;
                    int level = 0;
                    while (parentname != null)
                    {
                        var dirname = Path.GetFileName(parentname);
                        if (dirname == "retail" || dirname == "demo")
                        {
                            parentname = Directory.GetParent(parentname).FullName;
                            continue;
                        }

                        if (level == 0)
                        {
                            expectedGame = Enum.Parse<MEGame>(dirname);
                        }
                        else if (level == 1)
                        {
                            expectedPlatform = Enum.Parse<MEPackage.GamePlatform>(dirname);
                        }
                        else
                        {
                            break;
                        }

                        parentname = Directory.GetParent(parentname).FullName;
                        level++;
                    }

                    var package = MEPackageHandler.OpenMEPackage(p, forceLoadFromDisk: true);

                    Assert.AreEqual(expectedGame, package.Game, "The expected game and the resolved game do not match!");
                    Assert.AreEqual(expectedPlatform, package.Platform, "The expected platform and the resolved platform do not match!");
                    Console.WriteLine($" > Enumerating all exports for properties");

                    foreach (var exp in package.Exports)
                    {
                        if (exp.ClassName != "Class")
                        {
                            var props = exp.GetProperties(forceReload: true, includeNoneProperties: true);
                            Assert.IsInstanceOfType(props.LastOrDefault(), typeof(NoneProperty),
                                $"Error parsing properties on export {exp.UIndex} {exp.InstancedFullPath} in file {exp.FileRef.FilePath}");
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
            //var packages = Directory.GetFiles(packagesPath, "*.*", SearchOption.AllDirectories);
            var packages = Directory.GetFiles(packagesPath, "*.*", SearchOption.AllDirectories);
            foreach (var p in packages)
            {
                if (p.RepresentsPackageFilePath())
                {
                    // Do not use package caching in tests
                    Debug.WriteLine($"Opening package {p}");
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
