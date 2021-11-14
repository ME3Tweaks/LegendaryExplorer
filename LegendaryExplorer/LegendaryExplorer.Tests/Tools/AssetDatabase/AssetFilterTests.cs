using System.Linq;
using LegendaryExplorer.Tools.AssetDatabase.Filters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace LegendaryExplorer.Tests.Tools.AssetDatabase
{
    [TestClass]
    public class AssetFilterTests
    {
        [TestMethod]
        public void TestGenericAssetFilter()
        {
            var match = new Mock<IAssetSpecification<int>>();
            match.Setup(x => x.MatchesSpecification(1)).Returns(true);
            match.Setup(x => x.IsSelected).Returns(true);
            match.Setup(x => x.ShowInUI).Returns(true);

            var noMatch = new Mock<IAssetSpecification<int>>();
            noMatch.Setup(x => x.MatchesSpecification(1)).Returns(false);
            noMatch.SetupSequence(x => x.IsSelected).Returns(false).Returns(true);
            noMatch.Setup(x => x.ShowInUI).Returns(true);

            var notInUI = new Mock<IAssetSpecification<int>>();
            notInUI.Setup(x => x.MatchesSpecification(1)).Returns(true);
            notInUI.SetupSequence(x => x.IsSelected).Returns(false);
            notInUI.Setup(x => x.ShowInUI).Returns(false);

            var f = new GenericAssetFilter<int>(new []{match.Object, noMatch.Object, notInUI.Object});

            Assert.IsTrue(f.Filter(1));
            match.Verify(x => x.MatchesSpecification(1), Times.Once); // Selected
            noMatch.Verify(x => x.MatchesSpecification(1), Times.Never); // Is not selected
            notInUI.Verify(x => x.MatchesSpecification(1), Times.Once); // Hidden in UI - Always on

            Assert.IsFalse(f.Filter(1)); // noMatch is now selected
            noMatch.Verify(x => x.MatchesSpecification(1), Times.Once);

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