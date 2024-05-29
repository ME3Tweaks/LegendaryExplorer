using System;
using LegendaryExplorer.Tools.AssetDatabase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LegendaryExplorer.Tools.AssetDatabase.Filters;

namespace LegendaryExplorer.Tests.Tools.AssetDatabase
{
    [TestClass]
    public class AssetSpecificationTests
    {
        [TestMethod]
        public void TestPredicateSpecification()
        {
            var filterName = "Name";
            var assertionString = "testString";

            var spec1 = new PredicateSpecification<string>(filterName, s => s == assertionString);
            Assert.AreEqual(filterName, spec1.FilterName);
            Assert.IsTrue(spec1.MatchesSpecification(assertionString));
            Assert.IsFalse(spec1.MatchesSpecification("blah"));

            Assert.ThrowsException<ArgumentNullException>(() =>
            {
                var _ = new PredicateSpecification<string>(null, null);
            });
        }

        [TestMethod]
        public void TestOrSpecification()
        {
            var spec1 = new PredicateSpecification<int>("1", i => i == 1) { IsSelected = true };
            var spec2 = new PredicateSpecification<int>("2", i => i == 2) { IsSelected = true };
            var or1 = new OrSpecification<int>(spec1, spec2);

            Assert.IsTrue(or1.IsSelected);
            Assert.IsTrue(or1.MatchesSpecification(1));
            Assert.IsTrue(or1.MatchesSpecification(2));
            Assert.IsFalse(or1.MatchesSpecification(3));

            // Empty OrSpecification should always match
            var or2 = new OrSpecification<int>();
            Assert.IsFalse(or2.IsSelected);
            Assert.IsTrue(or2.MatchesSpecification(12345));
        }

        [TestMethod]
        public void TestUISeparator()
        {
            var uiSep = new UISeparator<int>();
            Assert.IsNotNull(uiSep.FilterName);

            // This specification should hopefully never be run, but if it does it should always return true.
            Assert.IsTrue(uiSep.MatchesSpecification(12345));
        }

        [TestMethod]
        public void TestSearchSpecification()
        {
            var searchSpec = new SearchSpecification<string>((t) => t.Item1 == t.Item2);
            searchSpec.SearchText = "match";
            Assert.IsTrue(searchSpec.MatchesSpecification("match"));
            Assert.IsFalse(searchSpec.MatchesSpecification("nomatch"));
            Assert.IsFalse(searchSpec.MatchesSpecification(null));

            searchSpec.SearchText = "match2";
            Assert.IsTrue(searchSpec.MatchesSpecification("match2"));
        }

        [TestMethod]
        public void TestFileListSpecification()
        {
            var fls = new FileListSpecification();
            var test = new MeshRecord("test", false, false, 4);
            test.Usages.Add(new MeshUsage(10, 10, false));

            // Empty FileList should match any record
            Assert.IsTrue(fls.MatchesSpecification(test));

            fls.CustomFileList.Add(9, "BIOA_STA00.pcc");
            Assert.IsFalse(fls.MatchesSpecification(test)); // record does not match

            fls.CustomFileList.Add(10, "BIOA_STA00_DSG.pcc");
            Assert.IsTrue(fls.MatchesSpecification(test));
        }

        [TestMethod]
        public void TestActionSpecification()
        {
            var timesInvoked = 0;
            var spec = new ActionSpecification<int>("Test", () => timesInvoked++);
            Assert.IsFalse(spec.IsSelected);
            Assert.IsTrue(spec.MatchesSpecification(1));
            Assert.AreEqual(0, timesInvoked);

            // Once IsSelected setter is invoked, all should be the same except action has now been executed
            spec.IsSelected = true;
            Assert.IsFalse(spec.IsSelected);
            Assert.IsTrue(spec.MatchesSpecification(1));
            Assert.AreEqual(1, timesInvoked);

            spec.IsSelected = true;
            Assert.AreEqual(2, timesInvoked);
        }
    }
}