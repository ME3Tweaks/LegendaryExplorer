using Microsoft.VisualStudio.TestTools.UnitTesting;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.PlotDatabase;
using LegendaryExplorerCore.PlotDatabase.PlotElements;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class PlotElementTests
    {
        [TestMethod]
        public void TestPlotElementOperations()
        {
            var parent = new PlotElement()
            {
                PlotId = 0,
                ElementId = 1,
                Label = "parent",
                Type = PlotElementType.None
            };
            var child = new PlotElement()
            {
                PlotId = 10,
                ElementId = 2,
                Label = "child",
                Type = PlotElementType.State
            };

            Assert.IsTrue(parent.Children.Count == 0);
            Assert.IsNull(child.Parent);
            Assert.AreEqual("child", child.Path);

            // Test parent assignment
            child.AssignParent(parent);
            Assert.IsTrue(parent.Children.Contains(child));
            Assert.AreEqual(parent.ElementId, child.ParentElementId);
            Assert.AreEqual(parent, child.Parent);
            Assert.AreEqual("parent.child", child.Path);

            // Test parent removal
            var result = child.RemoveFromParent();
            Assert.IsTrue(result);
            Assert.IsNull(child.Parent);
            Assert.IsFalse(parent.Children.Contains(child));
            Assert.AreEqual(-1, child.ParentElementId);

            // Should return false, as there is no parent to remove
            result = child.RemoveFromParent();
            Assert.IsFalse(result);

            // Test assigning null parent
            child.AssignParent(parent);
            child.AssignParent(null);
            Assert.IsNull(child.Parent);
            Assert.IsFalse(parent.Children.Contains(child));
            Assert.AreEqual(-1, child.ParentElementId);

            // Test is a game state and relevant ID
            Assert.IsTrue(child.IsAGameState);
            Assert.IsFalse(parent.IsAGameState);

            Assert.AreEqual(child.PlotId, child.RelevantId);
            Assert.AreEqual(parent.ElementId, parent.RelevantId);
        }
    }
}