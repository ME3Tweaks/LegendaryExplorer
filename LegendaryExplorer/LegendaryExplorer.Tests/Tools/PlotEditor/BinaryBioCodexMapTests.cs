using System.IO;
using Gammtek.Conduit.MassEffect3.SFXGame.CodexMap;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Encoding = System.Text.Encoding;

namespace LegendaryExplorer.Tests.Tools.PlotEditor;

[TestClass]
public class BinaryBioCodexMapTests
{
    [TestMethod]
    [DataRow("ME3DataCodexMap.bin")]
    [DataRow("LE1DataCodexMap.bin")]
    [DataRow("LE2DataCodexMap.bin")]
    [DataRow("LE2DataCodexMap2.bin")]
    [DataRow("LE3DataCodexMap.bin")]
    public void BioCodexMap_Reserializes(string filename)
    {
        var fileStream = TestData.GetTestDataStream("PlotEditor", filename);
        var codexMap = BinaryBioCodexMap.Load(fileStream, Encoding.UTF8);

        var outputData = new MemoryStream();
        var binaryCodexMap = new BinaryBioCodexMap(codexMap.Sections, codexMap.Pages);
        binaryCodexMap.Save(outputData);
        
        var bin = TestData.GetTestDataBytes("PlotEditor", filename);
        CollectionAssert.AreEquivalent(bin, outputData.ToArray());
    }
}