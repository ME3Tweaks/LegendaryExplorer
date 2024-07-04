using System.IO;
using Gammtek.Conduit.MassEffect3.SFXGame.QuestMap;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Encoding = System.Text.Encoding;

namespace LegendaryExplorer.Tests.Tools.PlotEditor;

[TestClass]
public class BinaryBioQuestMapTests
{
    [TestMethod]
    [DataRow("ME3QuestMap.bin")]
    [DataRow("LE1QuestMap.bin")]
    [DataRow("LE1QuestMap2.bin")]
    [DataRow("LE2QuestMap.bin")]
    [DataRow("LE3QuestMap.bin")]
    public void BioQuestMap_Reserializes(string filename)
    {
        var fileStream = TestData.GetTestDataStream("PlotEditor", filename);
        var questMap = BinaryBioQuestMap.Load(fileStream);

        var outputData = new MemoryStream();
        var binaryQuestMap = new BinaryBioQuestMap(questMap.Quests, questMap.BoolTaskEvals, questMap.IntTaskEvals, questMap.FloatTaskEvals);
        binaryQuestMap.Save(outputData);
        
        var bin = TestData.GetTestDataBytes("PlotEditor", filename);
        CollectionAssert.AreEquivalent(bin, outputData.ToArray());
    }
}