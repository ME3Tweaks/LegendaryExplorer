using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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


            var testFile = Path.Combine(GlobalTest.GetTestPackagesDirectory(), "PC", "ME1", "BIOA_NOR10_08_DSG.SFM");
            //var testFile = Path.Combine(GlobalTest.GetTestPackagesDirectory(), "PC", "ME2", "retail", "BioD_BlbGtl_205Evacuation.pcc");
            //var testFile = Path.Combine(GlobalTest.GetTestPackagesDirectory(), "PC", "ME3", "BioP_ProEar.pcc");


            var shortName = Path.GetFileName(testFile);
            void method(bool usePc)
            {
                using var testPackage = MEPackageHandler.OpenMEPackage(testFile);
                var testLib = new FileLib(testPackage);
                bool fileLibInitialized = testLib.Initialize(usePc ? new PackageCache() : null).Result;
                Assert.IsTrue(fileLibInitialized, $"{testPackage.Game} Script failed to compile {shortName} class definitions!");

                foreach (ExportEntry funcExport in testPackage.Exports.Where(exp => exp.ClassName == "Function"))
                {
                    (ASTNode astNode, string text) = UnrealScriptCompiler.DecompileExport(funcExport, testLib);

                    Assert.IsInstanceOfType(astNode, typeof(Function), $"#{funcExport.UIndex} {funcExport.InstancedFullPath} in {shortName} did not decompile!");

                    (_, MessageLog log) = UnrealScriptCompiler.CompileFunction(funcExport, text, testLib);

                    if (log.AllErrors.Any())
                    {
                        Assert.Fail($"#{funcExport.UIndex} {funcExport.InstancedFullPath} in {shortName} did not recompile!");
                    }
                }
            }

            Stopwatch sw = Stopwatch.StartNew();
            MEPackageHandler.GlobalSharedCacheEnabled = false;
            method(true);
            sw.Stop();
            Debug.WriteLine($"With packagecache took {sw.ElapsedMilliseconds}ms");

            FileLib.FreeLibs();
            GC.Collect();
            sw = Stopwatch.StartNew();
            MEPackageHandler.GlobalSharedCacheEnabled = true;
            method(false);
            sw.Stop();
            Debug.WriteLine($"With global cache took {sw.ElapsedMilliseconds}ms");
        }
    }
}
