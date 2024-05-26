using System;
using System.Collections.Generic;
using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Tools.AssetDatabase.Filters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorer.Tests.Tools.AssetDatabase
{
    [TestClass]
    public class MaterialSpecificationTests
    {
        #region Records to test against
        MaterialRecord match = new MaterialRecord()
        {
            MatSettings = new List<MatSetting>()
            {
                new MatSetting("bTestBool", "", "True"),
                new MatSetting("bOtherStuff", "Anything", "True")
            }
        };

        MaterialRecord noMatch = new MaterialRecord()
        {
            MatSettings = new List<MatSetting>()
            {
                new MatSetting("bTestBool", "", "False"),
                new MatSetting("bOtherStuff", "Nothing", "True"),
                new MatSetting("bTestBool", "True", "")
            }
        };

        MaterialRecord noProp = new MaterialRecord()
        {
            MatSettings = new List<MatSetting>()
            {
                new MatSetting("bSomethingElse", "", "True"),
                new MatSetting("bAnotherThing", "", "True")
            }
        };

        #endregion

        [TestMethod]
        public void TestMaterialBoolSpec()
        {
            var boolSpec = new MaterialBoolSpec()
            {
                FilterName = "TestSpec",
                IsSelected = true,
                PropertyName = "bTestBool"
            };
            Assert.IsTrue(boolSpec.MatchesSpecification(match));
            Assert.IsFalse(boolSpec.MatchesSpecification(noMatch));
            Assert.IsFalse(boolSpec.MatchesSpecification(noProp));
            Assert.ThrowsException<NullReferenceException>(() => { boolSpec.MatchesSpecification(null); });
        }

        [TestMethod]
        public void TestMaterialBoolSpecInverted()
        {
            var boolSpec = new MaterialBoolSpec()
            {
                FilterName = "TestSpec",
                IsSelected = true,
                PropertyName = "bTestBool",
                Inverted = true
            };
            Assert.IsFalse(boolSpec.MatchesSpecification(match));
            Assert.IsTrue(boolSpec.MatchesSpecification(noMatch));
            Assert.IsTrue(boolSpec.MatchesSpecification(noProp));
            Assert.ThrowsException<NullReferenceException>(() => { boolSpec.MatchesSpecification(null); });
        }

        [TestMethod]
        public void TestMaterialSettingSpec()
        {
            // Parm1
            var spec1 = new MaterialSettingSpec("", "bOtherStuff", param1: "Anything");
            Assert.IsTrue(spec1.MatchesSpecification(match));
            Assert.IsFalse(spec1.MatchesSpecification(noMatch));
            Assert.IsFalse(spec1.MatchesSpecification(noProp));

            // Parm1 and Parm2
            var spec2 = new MaterialSettingSpec("", "bOtherStuff", param1: "Anything", param2: "True");
            Assert.IsTrue(spec2.MatchesSpecification(match));
            Assert.IsFalse(spec2.MatchesSpecification(noMatch));
            Assert.IsFalse(spec2.MatchesSpecification(noProp));

            // Parm2
            var spec3 = new MaterialSettingSpec("", "bOtherStuff", param2: "True");
            Assert.IsTrue(spec3.MatchesSpecification(match));
            Assert.IsTrue(spec3.MatchesSpecification(noMatch));
            Assert.IsFalse(spec3.MatchesSpecification(noProp));

            // Setting exists?
            var spec4 = new MaterialSettingSpec("", "bOtherStuff");
            Assert.IsTrue(spec4.MatchesSpecification(match));
            Assert.IsTrue(spec4.MatchesSpecification(noMatch)); // Is true - contains the property
            Assert.IsFalse(spec4.MatchesSpecification(noProp));

            // Predicate
            var spec5 = new MaterialSettingSpec("", "bOtherStuff", (set) => set.Parm1.Contains("thing"));
            Assert.IsTrue(spec5.MatchesSpecification(match));
            Assert.IsTrue(spec5.MatchesSpecification(noMatch));
            Assert.IsFalse(spec5.MatchesSpecification(noProp));
        }

        [TestMethod]
        public void TestMaterialSettingSpecInverted()
        {
            var spec1 = new MaterialSettingSpec("", "bOtherStuff", param1: "Anything") { Inverted = true };
            Assert.IsFalse(spec1.MatchesSpecification(match));
            Assert.IsTrue(spec1.MatchesSpecification(noMatch));
            Assert.IsTrue(spec1.MatchesSpecification(noProp));

            var spec2 = new MaterialSettingSpec("", "bOtherStuff", (set) => set.Parm1.Contains("thing")) { Inverted = true };
            Assert.IsFalse(spec2.MatchesSpecification(match));
            Assert.IsFalse(spec2.MatchesSpecification(noMatch));
            Assert.IsTrue(spec2.MatchesSpecification(noProp));
        }
    }
}