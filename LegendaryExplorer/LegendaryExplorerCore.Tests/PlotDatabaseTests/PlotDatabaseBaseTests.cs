using System;
using LegendaryExplorerCore.PlotDatabase;
using LegendaryExplorerCore.PlotDatabase.Databases;
using LegendaryExplorerCore.PlotDatabase.PlotElements;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests
{
    /// <summary>
    /// An implementation of PlotDatabaseBase used for testing the base class
    /// </summary>
    public class FakePDB : PlotDatabaseBase
    {
        public override bool IsBioware => false;

        public void SetRoot(PlotElement r) => Root = r;
    }
    
    [TestClass]
    public class PlotDatabaseBaseTests
    {
        private PlotDatabaseBase plotDb;

        [TestInitialize]
        public void Setup()
        {
            var pdb = new FakePDB();
            var root = new PlotElement(-1, 1, "Root", PlotElementType.Region, null);
            pdb.SetRoot(root);
            pdb.Organizational.Add(1, root);

            plotDb = pdb;
        }

        [TestMethod]
        public void TestElementAdd()
        {
            var a = new PlotElement(0, 2, "Category", PlotElementType.Category, null);
            var b = new PlotBool(1, 3, "Bool", PlotElementType.State, null);
            var c = new PlotConditional(1, 4, "Conditional", PlotElementType.Conditional, b);

            plotDb.AddElement(a, plotDb.Root);
            Assert.IsTrue(a.Parent == plotDb.Root);
            Assert.IsTrue(plotDb.Organizational.ContainsValue(a));
            Assert.IsTrue(plotDb.Organizational.ContainsKey(a.ElementId));

            // Null parent throws exception when element doesn't have parent
            Assert.ThrowsException<Exception>(() => plotDb.AddElement(b, null));

            plotDb.AddElement(b, a);
            Assert.IsTrue(plotDb.Bools.ContainsValue(b));
            Assert.IsTrue(plotDb.Bools.ContainsKey(b.PlotId));

            // Null parent works fine if element already has parent
            plotDb.AddElement(c, null);
            Assert.IsTrue(plotDb.Conditionals.ContainsValue(c));
            Assert.IsTrue(plotDb.Conditionals.ContainsKey(c.PlotId));
        }

        [TestMethod]
        public void TestElementRemoval()
        {
            // Setup
            var a = new PlotElement(0, 2, "Category", PlotElementType.Category, null);
            var b = new PlotBool(1, 3, "Bool", PlotElementType.State, null);
            var c = new PlotConditional(1, 4, "Conditional", PlotElementType.Conditional, null);
            var d = new PlotConditional(2, 5, "Conditional2", PlotElementType.Conditional, null);

            plotDb.AddElement(a, plotDb.Root);
            plotDb.AddElement(b, plotDb.Root);
            plotDb.AddElement(c, b);
            plotDb.AddElement(d, b);

            // Pre-flight checks
            Assert.IsTrue(b.Children.Count > 0);
            Assert.IsTrue(a.Parent == plotDb.Root);
            Assert.ThrowsException<ArgumentException>(() => plotDb.RemoveElement(plotDb.Root)); // Cannot remove root
            Assert.ThrowsException<ArgumentException>(() => plotDb.RemoveElement(b, removeAllChildren: false)); // If element has children, error is thrown by default

            // Actual testing
            plotDb.RemoveElement(a);
            Assert.IsNull(a.Parent);
            Assert.IsFalse(plotDb.Organizational.ContainsKey(a.RelevantId));
            Assert.IsFalse(plotDb.Organizational.ContainsValue(a));

            plotDb.RemoveElement(b, removeAllChildren:true);
            Assert.IsFalse(plotDb.Bools.ContainsValue(b));
            Assert.IsTrue(b.Children.Count == 0);
            Assert.IsFalse(plotDb.Conditionals.ContainsValue(c));
            Assert.IsFalse(plotDb.Conditionals.ContainsValue(d));
        }
    }
}