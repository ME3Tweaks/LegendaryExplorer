using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.PlotDatabase;
using LegendaryExplorerCore.PlotDatabase.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class PlotDBDeserializationTests
    {
        [TestMethod]
        public void TestDBSerialization()
        {
            GlobalTest.Init();
            var le1File =
                new StreamReader(LegendaryExplorerCoreUtilities.LoadFileFromCompressedResource("PlotDatabases.zip", "le1.json")).ReadToEnd();
            var db1 = JsonConvert.DeserializeObject<SerializedPlotDatabase>(le1File);
            db1.BuildTree();

            var le2File =
                new StreamReader(LegendaryExplorerCoreUtilities.LoadFileFromCompressedResource("PlotDatabases.zip", "le2.json")).ReadToEnd();
            var db2 = JsonConvert.DeserializeObject<SerializedPlotDatabase>(le2File);
            db2.BuildTree();

            var le3File =
                new StreamReader(LegendaryExplorerCoreUtilities.LoadFileFromCompressedResource("PlotDatabases.zip", "le3.json")).ReadToEnd();
            var db3 = JsonConvert.DeserializeObject<SerializedPlotDatabase>(le3File);
            db3.BuildTree();
        }
    }
}
