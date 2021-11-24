using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.PlotDatabase.Serialization;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.Databases
{
    public class BasegamePlotDatabase : PlotDatabaseBase
    {
        public override bool IsBioware => true;

        public void LoadPlotsFromLEC(MEGame game)
        {
            Game = game;

            string json = LegendaryExplorerCoreUtilities.LoadStringFromCompressedResource("PlotDatabases.zip",
                LegendaryExplorerCoreLib.CustomPlotFileName(game));

            var pdb = JsonConvert.DeserializeObject<SerializedPlotDatabase>(json, _jsonSerializerSettings);
            if (pdb != null)
            {
                pdb.BuildTree();
                ImportPlots(pdb);
                Root = Organizational[1];
            }
        }

        public static BasegamePlotDatabase CreateBasegamePlotDatabase(MEGame game)
        {
            var db = new BasegamePlotDatabase();
            db.LoadPlotsFromLEC(game);
            return db;
        }
    }
}