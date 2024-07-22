using System.IO;
using Gammtek.Conduit.MassEffect3.SFXGame.QuestMap;
using Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap;
using LegendaryExplorerCore.Packages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorer.Tests.Tools.PlotEditor;

[TestClass]
public class BinaryBioStateEventMapTests
{
    [TestMethod]
    [DataRow("LE1ConsequenceMap.bin", MEGame.LE1)]
    [DataRow("LE1StateTransitionMap.bin", MEGame.LE1)]
    [DataRow("LE1StateTransitionMap2.bin", MEGame.LE1)]
    [DataRow("LE2StateTransitionMap.bin", MEGame.LE2)]
    [DataRow("LE2ConsequenceMap.bin", MEGame.LE2)]
    [DataRow("LE3StateTransitionMap.bin", MEGame.LE3)]
    public void BioStateEventMap_Reserializes(string filename, MEGame game)
    {
        var fileStream = TestData.GetTestDataStream("PlotEditor", filename);
        var stateEventMap = BinaryBioStateEventMap.Load(fileStream, game);

        var outputData = new MemoryStream();
        var binaryStateEventMap = new BinaryBioStateEventMap(stateEventMap.StateEvents);
        binaryStateEventMap.Save(outputData, game);
        
        var bin = TestData.GetTestDataBytes("PlotEditor", filename);
        CollectionAssert.AreEquivalent(bin, outputData.ToArray());
    }
}