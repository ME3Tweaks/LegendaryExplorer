using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.TLK.ME2ME3;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class TLKTests
    {
        [TestMethod]
        public void TestTLKs()
        {
            GlobalTest.Init();
            // Loads compressed packages and attempts to enumerate every object's properties.
            var tlkDataPath = GlobalTest.GetTestTLKDirectory();

            //ME1
            var packages = Directory.GetFiles(tlkDataPath, "*.*", SearchOption.AllDirectories)
                .Where(x => x.RepresentsPackageFilePath());
            foreach (var p in packages)
            {
                Console.WriteLine($"Opening package {p}");
                // Do not use package caching in tests
                using var package = MEPackageHandler.OpenMEPackage(p, forceLoadFromDisk: true);
                foreach (var export in package.Exports.Where(x => x.ClassName == "BioTlkFile"))
                {
                    var tf = new ME1TalkFile(export);
                    TestTalkFile(tf, export, null);
                }
            }

            // ME2/ME3
            var tlks = Directory.GetFiles(tlkDataPath, "*.tlk", SearchOption.AllDirectories);
            foreach (var tlkFilePath in tlks)
            {
                Debug.WriteLine($"Opening TLK file {tlkFilePath}");
                var tf = new ME2ME3TalkFile();
                tf.LoadTlkData(tlkFilePath);
                TestTalkFile(tf, null, tlkFilePath);
            }
        }

        private void TestTalkFile(ITalkFile tf, ExportEntry game1Export, string game23FilePath)
        {
            foreach (var stringId in tf.StringRefs)
            {
                var expected = stringId.Data;
                var found = tf.FindDataById(stringId.StringID);
                var testcache = found;
                // Strip single pair of quotes off. Trim() does multiple so if string ends with " it ruins it
                if (found.StartsWith('\"')) found = found.Substring(1);
                if (found.EndsWith('\"')) found = found.Substring(0, found.Length - 1);

                if (game23FilePath != null && expected == "Female") continue; //It seems we don't have a way to query female strings (Game2 and 3)
                Assert.AreEqual(string.IsNullOrEmpty(expected) ? "" : expected, found);

                // Ensure stripping quotes works correctly
                var foundQuoteless = tf.FindDataById(stringId.StringID, noQuotes: true);
                Assert.AreEqual(string.IsNullOrEmpty(found) ? "" : found, foundQuoteless);
            }

            // TEST LOCALIZATION
            var expectedLoc = game1Export != null ? GetExpectedLocGame1(game1Export.ObjectName.Name, game1Export.FileRef) : GetExpectedLocGame23(game23FilePath);
            Assert.AreEqual(expectedLoc, tf.Localization, "TLK did not parse correct localization");

            // TEST ADDING

            // TEST REPLACING

            // TEST SAVING
            // Todo: ITalkFile should have a way to save XML to stream so we have consistent way to test without saving to disk
        }

        // We use our own basic implementation so we don't use the same code to test if the result is accurate.
        // This is tailored specifically for TLK names
        private MELocalization GetExpectedLocGame1(string str, IMEPackage package)
        {
            if (package.FileNameNoExtension == "GlobalTlk") return MELocalization.INT;
            if (str is "GlobalTlk_tlk" or "GlobalTlk_tlk_M") return package.Localization != MELocalization.None ? package.Localization : MELocalization.INT;
            if (str == "tlk") return MELocalization.INT;
            if (str == "tlk_M") return MELocalization.INT;

            var parsed = str.Replace("tlk_M_", "");
            parsed = parsed.Replace("tlk_", ""); // for non-m

            // Convert for casting to enum.
            if (parsed == "DE") parsed = "DEU";
            if (parsed == "RA") parsed = "RUS";
            if (parsed == "IT") parsed = "ITA";
            if (parsed == "FR") parsed = "FRA";
            if (parsed == "JA") parsed = "JPN";
            if (parsed == "ES") parsed = "ESN";
            if (parsed is "RA" or "RU") parsed = "RUS";
            if (parsed is "PL" or "PLPC") parsed = "POL";

            if (Enum.TryParse<MELocalization>(parsed, out var expectedLoc))
            {
                return expectedLoc;
            }
            else
            {
                // Game 1 has some TLKs that we don't support, ignore these
                if (str.EndsWith("_CS")) return MELocalization.None; //Czech
                if (str.EndsWith("_HU")) return MELocalization.None; //Hungarian
                Assert.Fail($"TLK doesn't have proper localization name: {str}");
            }

            throw new Exception($"TLK has incorrect localization name: {str}");
        }

        // We use our own basic implementation so we don't use the same code to test if the result is accurate.
        // This is tailored specifically for TLK names
        private MELocalization GetExpectedLocGame23(string str)
        {
            var fName = Path.GetFileNameWithoutExtension(str);
            var locText = fName.Substring(fName.Length - 3);
            if (Enum.TryParse<MELocalization>(locText, out var expectedLoc))
            {
                return expectedLoc;
            }
            else
            {
                Assert.Fail($"TLK doesn't have proper localization name: {fName}");
            }

            throw new Exception($"TLK has incorrect localization name: {fName}");
        }
    }
}