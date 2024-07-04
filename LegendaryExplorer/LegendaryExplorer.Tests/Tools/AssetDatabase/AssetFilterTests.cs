using System.Linq;
using LegendaryExplorer.Tools.AssetDatabase.Filters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace LegendaryExplorer.Tests.Tools.AssetDatabase
{
    [TestClass]
    public class AssetFilterTests
    {
        [TestMethod]
        public void TestGenericAssetFilter()
        {
            var match = Substitute.For<IAssetSpecification<int>>();
            match.MatchesSpecification(1).Returns(true);
            match.IsSelected.Returns(true);
            match.ShowInUI.Returns(true);

            var noMatch = Substitute.For<IAssetSpecification<int>>();
            noMatch.MatchesSpecification(1).Returns(false);
            noMatch.IsSelected.ReturnsForAnyArgs(false, true);
            noMatch.ShowInUI.Returns(true);

            var notInUI = Substitute.For<IAssetSpecification<int>>();
            notInUI.MatchesSpecification(1).Returns(true);
            notInUI.IsSelected.ReturnsForAnyArgs(false);
            notInUI.ShowInUI.Returns(false);

            var f = new GenericAssetFilter<int>([match, noMatch, notInUI]);

            Assert.IsTrue(f.Filter(1));
            match.Received(1).MatchesSpecification(1); // Selected
            noMatch.DidNotReceive().MatchesSpecification(1); // Is not selected
            notInUI.Received(1).MatchesSpecification(1); // Hidden in UI - Always on

            Assert.IsFalse(f.Filter(1)); // noMatch is now selected
            noMatch.Received(1).MatchesSpecification(1);

            Assert.IsFalse(f.Filter("Obj not of type T"));
        }


        [TestMethod]
        public void TestSingleOptionFilter()
        {
            var spec1 = new PredicateSpecification<int>("1", _ => true);
            var spec2 = new PredicateSpecification<int>("2", _ => true);
            var spec3 = new PredicateSpecification<int>("2", _ => true);
            var f = new SingleOptionFilter<int>(new[] {spec1, spec2, spec3});

            // Only one spec is selected at a time
            Assert.AreEqual(0, f.Filters.Count(s => s.IsSelected));
            f.SetSelected(spec1);
            Assert.AreEqual(1, f.Filters.Count(s => s.IsSelected));
            f.SetSelected(spec2);
            Assert.AreEqual(1, f.Filters.Count(s => s.IsSelected));
            f.SetSelected(spec3);
            Assert.AreEqual(1, f.Filters.Count(s => s.IsSelected));
            f.SetSelected(spec3);
            Assert.AreEqual(0, f.Filters.Count(s => s.IsSelected));
        }
    }
}