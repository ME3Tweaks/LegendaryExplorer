using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests
{
    /// <summary>
    /// Tests the relinker system. Due to the complexity of this, it's unlikely we will ever
    /// have full coverage unless we included tons of packages 
    /// </summary>
    [TestClass]
    public class RelinkerTests
    {
        class ExpectedPortingResults
        {
            public ExpectedPortingResults() { }

            public ExpectedPortingResults(bool port, string originalIFP, string expectedDestIFP, bool destIsImport, bool useFreshPackage = false)
            {
                Port = port;
                OriginalIFP = originalIFP;
                ExpectedDestIFP = expectedDestIFP;
                DestinationIsImport = destIsImport;
                UseFreshPackage = useFreshPackage;
            }
            /// <summary>
            /// If the Original IFP object should be ported. If false only the expected dest IFP is checked.
            /// </summary>
            public bool Port { get; set; }
            /// <summary>
            /// Original item to port
            /// </summary>
            public string OriginalIFP { get; set; }
            /// <summary>
            /// What we expect the destination IFP will be (changes for global imports)
            /// </summary>
            public string ExpectedDestIFP { get; set; }
            /// <summary>
            /// Should the destination item be an import?
            /// </summary>
            public bool DestinationIsImport { get; set; }
            /// <summary>
            /// Clears the package before running this object as a test
            /// </summary>
            public bool UseFreshPackage { get; set; }
        }
        [TestMethod]
        public void TestRelinkerLE1()
        {
            GlobalTest.Init();
            MEPackageHandler.GlobalSharedCacheEnabled = false; // don't use cache for this

            var packagesPath = Path.Combine(GlobalTest.GetTestPackagesDirectory(), "PC", "LE1", "BioGame", "CookedPCConsole");

            // Test porting from global file to package to make sure imports resolve properly
            // with ForcedExport.
            var sfxgame = MEPackageHandler.OpenMEPackage(Path.Combine(packagesPath, "SFXGame.pcc"));
            var expectedResults = new[]
            {
                new ExpectedPortingResults(true, "Default__BioMusicVolume", "Default__BioMusicVolume", false, true),
                new ExpectedPortingResults(false, null,"SFXGame.BioMusicVolume" , true),
                new ExpectedPortingResults(false, null,"BioEngineResources", true),
                new ExpectedPortingResults(false, null,"BioEngineResources.Default", true),
                new ExpectedPortingResults(false, null,"BioEngineResources.Default.Default2DA" , true),
            };
            TestPorting(sfxgame, expectedResults);

            // Test porting from non-global files
            var nor = MEPackageHandler.OpenMEPackage(Path.Combine(packagesPath, "BIOA_NOR10_01escape_DSG.pcc"));
            expectedResults = new[]
            {
                // Port a package export. This should pull in the children as well (this test only tested some since if some work they all probably do)
                new ExpectedPortingResults(true, "sta20_escape_D", "sta20_escape_D", false, true),
                new ExpectedPortingResults(false, null,"sta20_escape_D.tlk" , false),
                new ExpectedPortingResults(false, null,"sta20_escape_D.tlk_ES" , false),
                new ExpectedPortingResults(false, null,"sta20_escape_D.TlkSet_sta20_escape" , false),
                new ExpectedPortingResults(false, null,"SFXGame.BioTlkFile" , true),
                new ExpectedPortingResults(false, null,"SFXGame.BioTlkFileSet" , true),

                // Port a mesh
                new ExpectedPortingResults(true, "BIOG_Placeables_DockingArm.Docking_Arm", "Docking_Arm", false), // DO NOT USE FRESH PACKAGE
                new ExpectedPortingResults(false, null,"BIOA_STA30_T.STA30_robotarm" , false),
                new ExpectedPortingResults(false, null,"BIOA_STA30_T.STA30-armsNorm" , false),
                new ExpectedPortingResults(false, null,"BIOA_STA30_T.HMM_HGR_HVYa_CUBE" , false),
                new ExpectedPortingResults(false, null,"Engine.SkeletalMesh" , true),
                new ExpectedPortingResults(false, null,"Engine.Material" , true),
                new ExpectedPortingResults(false, null,"Engine.Texture2D" , true),
                new ExpectedPortingResults(false, null,"Engine.TextureCube" , true),
            };
            TestPorting(nor, expectedResults);
        }

        private void TestPorting(IMEPackage sourcePackage, ExpectedPortingResults[] expectedResults)
        {
            IMEPackage destPackage = null;
            foreach (var testResult in expectedResults)
            {
                if (testResult.UseFreshPackage)
                    destPackage = GenerateBlankPackage(sourcePackage.Game);

                if (testResult.Port)
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourcePackage.FindEntry(testResult.OriginalIFP), destPackage, null, true, new RelinkerOptionsPackage(), out _);
                }

                // Test
                if (testResult.DestinationIsImport)
                {
                    Assert.IsNotNull(destPackage.FindImport(testResult.ExpectedDestIFP), $"Porting resulted in import that could not be found in dest package: {testResult.ExpectedDestIFP}, Source file: {sourcePackage.FileNameNoExtension}");
                }
                else
                {
                    Assert.IsNotNull(destPackage.FindExport(testResult.ExpectedDestIFP), $"Porting resulted in export that could not be found in dest package: {testResult.ExpectedDestIFP}, Source file: {sourcePackage.FileNameNoExtension}");
                }
            }
        }

        /// <summary>
        /// Generates blank level package for specified game. This file is not saved on disk and is loaded from stream.
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        private IMEPackage GenerateBlankPackage(MEGame game)
        {
            return MEPackageHandler.OpenMEPackageFromStream(MEPackageHandler.CreateEmptyLevelStream("LECTest", game));
        }
    }
}
