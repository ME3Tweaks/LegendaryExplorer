using System;
using System.Collections.Generic;
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

            using (var biopProEar = MEPackageHandler.OpenMEPackage(Path.Combine(GlobalTest.GetTestPackagesDirectory(), "PC", "ME3", "BioP_ProEar.pcc")))
            {
                var biopProEarLib = new FileLib(biopProEar);
                bool fileLibInitialized = biopProEarLib.Initialize().Result;
                Assert.IsTrue(fileLibInitialized, "ME3 Script failed to compile BioP_ProEar class definitions!");

                foreach (ExportEntry funcExport in biopProEar.Exports.Where(exp => exp.ClassName == "Function"))
                {
                    (ASTNode astNode, string text) = UnrealScriptCompiler.DecompileExport(funcExport, biopProEarLib);

                    Assert.IsInstanceOfType(astNode, typeof(Function), $"#{funcExport.UIndex} {funcExport.InstancedFullPath} in BioP_ProEar did not decompile!");

                    (_, MessageLog log) = UnrealScriptCompiler.CompileFunction(funcExport, text, biopProEarLib);

                    if (log.AllErrors.Any())
                    {
                        Assert.Fail($"#{funcExport.UIndex} {funcExport.InstancedFullPath} in BioP_ProEar did not recompile!");
                    }
                }
            }

        }
    }
}
