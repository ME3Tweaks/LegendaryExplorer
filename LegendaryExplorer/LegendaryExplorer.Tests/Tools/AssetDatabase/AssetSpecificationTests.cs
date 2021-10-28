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

            // Null predicate should always match
            var spec2 = new PredicateSpecification<string>(null, null);
            Assert.IsNull(spec2.FilterName);
            Assert.IsTrue(spec2.MatchesSpecification("anything"));
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
    }
}