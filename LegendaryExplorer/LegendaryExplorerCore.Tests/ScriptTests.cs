using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class ScriptTests
    {
        [TestMethod]
        public void TestScript()
        {
            GlobalTest.Init();

            var testFiles = Directory.GetFiles(GlobalTest.GetTestDataDirectory(), "*.*", SearchOption.AllDirectories)
                .Where(x => StringExtensions.RepresentsPackageFilePath(x)
                            && !x.Contains(@"Xenon", StringComparison.InvariantCultureIgnoreCase) && !x.Contains("PS3", StringComparison.InvariantCultureIgnoreCase) && !x.Contains("UDK", StringComparison.InvariantCultureIgnoreCase)).ToList();

            //var testFile = Path.Combine(GlobalTest.GetTestPackagesDirectory(), "PC", "ME1", "BIOA_NOR10_08_DSG.SFM");
            //var testFile = Path.Combine(GlobalTest.GetTestPackagesDirectory(), "PC", "ME2", "retail", "BioD_BlbGtl_205Evacuation.pcc");
            //var testFile = Path.Combine(GlobalTest.GetTestPackagesDirectory(), "PC", "ME3", "BioP_ProEar.pcc");

            foreach (var testFile in testFiles)
            {
                var shortName = Path.GetFileName(testFile);
                compileTest(testFile, shortName, true);
                compileTest(testFile, shortName, false);
            }
        }

        private void compileTest(string testFile, string shortName, bool usePackageCache)
        {
            // Ensure we don't not test with anything cached in this test case
            FileLib.FreeLibs();
            GC.Collect();

            MEPackageHandler.GlobalSharedCacheEnabled = !usePackageCache;

            using var testPackage = MEPackageHandler.OpenMEPackage(testFile);
            if (testPackage.Platform != MEPackage.GamePlatform.PC)
                return; // Skip this file.

            Stopwatch sw = Stopwatch.StartNew();
            var testLib = new FileLib(testPackage);
            bool fileLibInitialized = testLib.Initialize(usePackageCache ? new PackageCache() : null).Result;
            Assert.IsTrue(fileLibInitialized, $"{testPackage.Game} Script failed to compile {shortName} class definitions!");
            sw.Stop();
            Debug.WriteLine($"With {(usePackageCache ? "packagecache" : "globalcache")} took {sw.ElapsedMilliseconds}ms to initialize lib");

            foreach (ExportEntry funcExport in testPackage.Exports.Where(exp => exp.ClassName == "Function"))
            {
                (ASTNode astNode, string text) = UnrealScriptCompiler.DecompileExport(funcExport, testLib);

                Assert.IsInstanceOfType(astNode, typeof(Function), $"#{funcExport.UIndex} {funcExport.InstancedFullPath} in {shortName} did not decompile!");

                (_, MessageLog log) = UnrealScriptCompiler.CompileFunction(funcExport, text, testLib);

                if (Enumerable.Any(log.AllErrors))
                {
                    Assert.Fail($"#{funcExport.UIndex} {funcExport.InstancedFullPath} in {shortName} did not recompile!");
                }
            }
        }
    }
}
