using LegendaryExplorerCore.Packages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class ExtensionTests
    {
        [TestMethod]
        public void TestUnrealLocalizationTests()
        {
            // Test GetLocalization()
            Dictionary<string, MELocalization> testData = new Dictionary<string, MELocalization>()
            {
                { "Startup_INT", MELocalization.INT },
                { "Startup_FRA", MELocalization.FRA },
                { "EntryMenu_RUS", MELocalization.RUS },
                { "TestFile_GE", MELocalization.DEU },
                { "PackageFile.pcc", MELocalization.None },
                { "PackageFile_200.pcc", MELocalization.None },
                { "AhernsCrabShack_FR_DEU.pcc", MELocalization.DEU },
                { "AhernsCrabShack_FR_JP.pcc", MELocalization.JPN },
                { "LocTest1_LOC_JPN.pcc", MELocalization.JPN },
                { "LocTest2_LOC_HERP.pcc", MELocalization.None },
                { "LocTest2_LOC_INT.pcc", MELocalization.INT },
                { "C:\\Users\\CrabMan_FR\\Package_ITA.pcc", MELocalization.ITA },
                { "C:\\Users\\CrabMan_FR\\Package_LOC_RUS.pcc", MELocalization.RUS },
            };

            foreach (var v in testData)
            {
                Assert.AreEqual(v.Value, v.Key.GetUnrealLocalization(), @"GetUnrealLocalization() returned the wrong localization!");
            }

            // Test StripLocalization()
            Dictionary<string, string> testStrings = new Dictionary<string, string>()
            {
                { "Startup_INT", "Startup" },
                { "Startup_FRA", "Startup" },
                { "Startup_Bonk.pcc", "Startup" },
                { "Startup_Derp", "Startup" },
                { "FR_DEU_JP_INT.pcc", "FR_DEU_JP.pcc" },
                { "BioD_Level1_200_LOC_INT", "Startup" },
                { "HerpAFlerpFlerp_LOC_DEU_LOC_FRA_RUS", "MyWifeIsWeird_LOC_DEU_LOC_FRA" },
                { "C:\\Users\\OhNo.pcc", "C:\\Users\\OhNo.pcc" },
                { "C:\\Users\\OhNo_LOC_DEU.pcc", "C:\\Users\\OhNo.pcc" },
                { "C:\\Users\\OhNo_LOC_LOC_Gulp.pcc", "C:\\Users\\OhNo.pcc" },
                { "C:\\Users\\OhNo_FR.pcc", "C:\\Users\\OhNo.pcc" },
                { "C:\\Users\\OhNo_GE.ini", "C:\\Users\\OhNo.ini" },
            };

            foreach (var v in testStrings)
            {
                Assert.AreEqual(v.Value, v.Key.StripUnrealLocalization(), @"StripUnrealLocalization() returned the wrong stripped string!");
            }

        }
    }
}